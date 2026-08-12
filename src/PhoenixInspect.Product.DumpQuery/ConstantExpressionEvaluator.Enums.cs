using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Full enum semantics and <c>typeof</c>: conversions between enums and their exact underlying types, the flags
/// algebra, the deterministic <see cref="Enum"/> API in both its generic and <c>typeof</c> spellings, and a typed
/// <c>typeof(...)</c> reference for every type domain the evaluator models. Enum shapes come from two evidence
/// sources on the same footing as everything else: a small closed table for BCL enums whose members are fixed by
/// contract, and the counted metadata of dump modules for enums the target itself declares.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    /// <summary>The complete declared shape of one enum type: underlying kind, members, and flags-ness.</summary>
    /// <param name="TypeFullName">The metadata full name, with namespaces separated by <c>.</c>.</param>
    /// <param name="IsFlags">Whether the declaration carries <see cref="FlagsAttribute"/>.</param>
    /// <param name="Underlying">The exact underlying integral kind.</param>
    /// <param name="Members">The literal members in declaration order, values as raw bits.</param>
    private sealed record EnumShape(
        string TypeFullName,
        bool IsFlags,
        NumericKind Underlying,
        ImmutableArray<(string Name, long Bits)> Members)
    {
        /// <summary>Gets the namespace-free display name.</summary>
        public string ShortName
        {
            get
            {
                var separator = TypeFullName.LastIndexOf('.');
                return separator < 0 ? TypeFullName : TypeFullName[(separator + 1)..];
            }
        }
    }

    /// <summary>A resolved <c>typeof(...)</c> reference to a type domain the evaluator models.</summary>
    /// <param name="Name">The namespace-free metadata name.</param>
    /// <param name="FullName">The metadata full name.</param>
    /// <param name="Namespace">The namespace, or null for a global-namespace type.</param>
    /// <param name="CSharpName">The C# spelling used in display, such as <c>int</c> or <c>DayOfWeek</c>.</param>
    /// <param name="IsEnum">Whether the type is an enum.</param>
    /// <param name="Shape">The enum shape when <paramref name="IsEnum"/>; otherwise null.</param>
    /// <param name="ElementType">The element type when this reference is an array type; otherwise null.</param>
    /// <param name="Definition">The modeled generic definition when the type is generic; otherwise null.</param>
    /// <param name="TypeArguments">The generic arguments when constructed; default for everything else.</param>
    /// <param name="IsInterfaceType">Whether the reference names an interface.</param>
    private sealed record TypeRef(
        string Name,
        string FullName,
        string? Namespace,
        string CSharpName,
        bool IsEnum,
        EnumShape? Shape,
        TypeRef? ElementType = null,
        GenericDef? Definition = null,
        ImmutableArray<TypeRef> TypeArguments = default,
        bool IsInterfaceType = false)
    {
        /// <summary>Gets whether this reference is a single-rank array type.</summary>
        public bool IsArray => ElementType is not null;

        /// <summary>Gets whether this reference is a generic type definition, such as <c>List&lt;&gt;</c>.</summary>
        public bool IsGenericDefinition => Definition is not null && TypeArguments.IsDefaultOrEmpty;

        /// <summary>Gets whether this reference is a constructed generic, such as <c>List&lt;int&gt;</c>.</summary>
        public bool IsConstructedGeneric => Definition is not null && !TypeArguments.IsDefaultOrEmpty;

        /// <summary>Builds the single-rank array type over this element type.</summary>
        public TypeRef MakeArray() => new(
            $"{Name}[]",
            $"{FullName}[]",
            Namespace,
            $"{CSharpName}[]",
            IsEnum: false,
            Shape: null,
            ElementType: this);
    }

    private static readonly ImmutableArray<EnumShape> KnownEnumShapes =
    [
        new("System.DayOfWeek", false, NumericKind.Int32,
        [
            ("Sunday", 0), ("Monday", 1), ("Tuesday", 2), ("Wednesday", 3), ("Thursday", 4), ("Friday", 5),
            ("Saturday", 6),
        ]),
        new("System.DateTimeKind", false, NumericKind.Int32,
        [
            ("Unspecified", 0), ("Utc", 1), ("Local", 2),
        ]),
        new("System.StringComparison", false, NumericKind.Int32,
        [
            ("CurrentCulture", 0), ("CurrentCultureIgnoreCase", 1), ("InvariantCulture", 2),
            ("InvariantCultureIgnoreCase", 3), ("Ordinal", 4), ("OrdinalIgnoreCase", 5),
        ]),
        new("System.StringSplitOptions", true, NumericKind.Int32,
        [
            ("None", 0), ("RemoveEmptyEntries", 1), ("TrimEntries", 2),
        ]),
    ];

    /// <summary>Renders one enum's raw bits as its invariant decimal text under its underlying kind.</summary>
    private static string FormatEnumBitsInvariant(NumericKind underlying, long bits) =>
        underlying == NumericKind.UInt64
            ? unchecked((ulong)bits).ToString(CultureInfo.InvariantCulture)
            : bits.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Names one enum value the way <see cref="Enum.ToString()"/> does: an exact member name, a flags
    /// decomposition when the declaration is flags-attributed, or the invariant decimal text.
    /// </summary>
    private static string NameEnumValue(EnumShape shape, long bits)
    {
        foreach (var (name, memberBits) in shape.Members)
        {
            if (memberBits == bits)
            {
                return name;
            }
        }

        if (shape.IsFlags && bits != 0)
        {
            // The standard flags rendering: consume members from the highest value downward; a remainder that no
            // member covers falls back to the decimal form of the whole value.
            var remaining = bits;
            var parts = new List<string>();
            foreach (var (name, memberBits) in shape.Members.OrderByDescending(static member =>
                unchecked((ulong)member.Bits)))
            {
                if (memberBits != 0 && (remaining & memberBits) == memberBits)
                {
                    parts.Insert(0, name);
                    remaining &= ~memberBits;
                }
            }

            if (remaining == 0 && parts.Count > 0)
            {
                return string.Join(", ", parts);
            }
        }

        return FormatEnumBitsInvariant(shape.Underlying, bits);
    }

    private static Operand EnumOperand(EnumShape shape, long bits) =>
        Operand.FromEnum(bits, shape.Underlying, shape.TypeFullName, NameEnumValue(shape, bits));

    // ---- Shape resolution -------------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves one enum shape by name: the closed BCL table first, then the counted metadata of dump modules.
    /// The outcome is cached per evaluation, and a name declared by several module instances is a typed
    /// ambiguity, never an arbitrary pick.
    /// </summary>
    private static FoldOutcome? TryResolveEnumShape(
        string typeFullName,
        FoldContext context,
        out EnumShape? shape)
    {
        shape = null;
        foreach (var known in KnownEnumShapes)
        {
            if (string.Equals(known.TypeFullName, typeFullName, StringComparison.Ordinal) ||
                string.Equals(known.ShortName, typeFullName, StringComparison.Ordinal))
            {
                shape = known;
                return null;
            }
        }

        if (context.EnumShapes.TryGetValue(typeFullName, out var cached))
        {
            shape = cached.Shape;
            return cached.Error;
        }

        var resolved = ResolveEnumShapeFromMetadata(typeFullName, context);
        context.EnumShapes[typeFullName] = resolved;
        shape = resolved.Shape;
        return resolved.Error;
    }

    private static (EnumShape? Shape, FoldOutcome? Error) ResolveEnumShapeFromMetadata(
        string typeFullName,
        FoldContext context)
    {
        // An alias-qualified enum name (aliasSeparator > 0 below) resolves only through the references carrying that
        // alias; an unqualified name resolves through the session's modules and the global-scope references.
        var aliasSeparator = typeFullName.IndexOf("::", StringComparison.Ordinal);
        var alias = aliasSeparator > 0 ? typeFullName[..aliasSeparator] : null;
        var scopedName = aliasSeparator > 0 ? typeFullName[(aliasSeparator + 2)..] : typeFullName;
        var applicableReferences = ApplicableReferences(context.References, alias);

        if (alias is null && context.Session is null && applicableReferences.IsEmpty)
        {
            // The evidence-free probe with no references cannot distinguish an unsupported type from an enum
            // declared in dump-module metadata. Retain that precise dependency so its provisional result cannot
            // preempt admission and the authoritative session-backed pass.
            context.DeferSessionAuthority();
            return (null, null);
        }

        var separator = scopedName.LastIndexOf('.');
        var typeNamespace = separator < 0 ? string.Empty : scopedName[..separator];
        var typeName = separator < 0 ? scopedName : scopedName[(separator + 1)..];
        var matches = new List<EnumShape>();

        void ScanImage(ImmutableArray<byte> metadataBytes)
        {
            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes);
                var reader = provider.GetMetadataReader();
                foreach (var handle in reader.TypeDefinitions)
                {
                    var typeDefinition = reader.GetTypeDefinition(handle);
                    if (!typeDefinition.GetDeclaringType().IsNil ||
                        !reader.StringComparer.Equals(typeDefinition.Name, typeName) ||
                        !reader.StringComparer.Equals(typeDefinition.Namespace, typeNamespace) ||
                        !IsEnumType(reader, typeDefinition))
                    {
                        continue;
                    }

                    if (ReadEnumShape(reader, typeDefinition, scopedName) is { } read)
                    {
                        matches.Add(read);
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // A malformed metadata image cannot contribute a declaration; other sources still can.
            }
        }

        if (alias is null && context.Session is { } session)
        {
            foreach (var module in session.Modules)
            {
                var metadata = session.ReadModuleContentIdentity(module);
                if (metadata.Status != ClrmdEvidenceStatus.Exact ||
                    metadata.Value is null ||
                    metadata.Evidence.Length != 1 ||
                    metadata.Evidence[0].Status != MemoryReadStatus.Exact)
                {
                    continue;
                }

                ScanImage(metadata.Evidence[0].Bytes);
            }
        }

        foreach (var reference in applicableReferences)
        {
            ScanImage(reference.MetadataBytesCore);
        }

        return matches.Count switch
        {
            0 => (null, null),
            1 => (matches[0], null),
            _ => (null, FoldOutcome.Error(
                AmbiguousCode,
                $"{matches.Count.ToString(CultureInfo.InvariantCulture)} module instances declare enum "
                + $"'{typeFullName}'; no instance is selected by enumeration order.")),
        };
    }

    /// <summary>Reads one enum declaration completely: underlying kind, literal members, and flags-ness.</summary>
    private static EnumShape? ReadEnumShape(
        MetadataReader reader,
        TypeDefinition typeDefinition,
        string typeFullName)
    {
        NumericKind? underlying = null;
        var members = ImmutableArray.CreateBuilder<(string Name, long Bits)>();
        foreach (var fieldHandle in typeDefinition.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & FieldAttributes.Static) == 0)
            {
                // The instance field is the storage slot whose signature names the underlying type.
                var signature = reader.GetBlobReader(field.Signature);
                signature.ReadSignatureHeader();
                underlying = signature.ReadSignatureTypeCode() switch
                {
                    SignatureTypeCode.SByte => NumericKind.SByte,
                    SignatureTypeCode.Byte => NumericKind.Byte,
                    SignatureTypeCode.Int16 => NumericKind.Int16,
                    SignatureTypeCode.UInt16 => NumericKind.UInt16,
                    SignatureTypeCode.Int32 => NumericKind.Int32,
                    SignatureTypeCode.UInt32 => NumericKind.UInt32,
                    SignatureTypeCode.Int64 => NumericKind.Int64,
                    SignatureTypeCode.UInt64 => NumericKind.UInt64,
                    _ => null,
                };
                continue;
            }

            const FieldAttributes literal = FieldAttributes.Literal | FieldAttributes.Static;
            if ((field.Attributes & literal) != literal)
            {
                continue;
            }

            var constantHandle = field.GetDefaultValue();
            if (constantHandle.IsNil)
            {
                continue;
            }

            var constant = reader.GetConstant(constantHandle);
            var blob = reader.GetBlobReader(constant.Value);
            long? bits = constant.TypeCode switch
            {
                ConstantTypeCode.SByte => blob.ReadSByte(),
                ConstantTypeCode.Byte => blob.ReadByte(),
                ConstantTypeCode.Int16 => blob.ReadInt16(),
                ConstantTypeCode.UInt16 => blob.ReadUInt16(),
                ConstantTypeCode.Int32 => blob.ReadInt32(),
                ConstantTypeCode.UInt32 => blob.ReadUInt32(),
                ConstantTypeCode.Int64 => blob.ReadInt64(),
                ConstantTypeCode.UInt64 => unchecked((long)blob.ReadUInt64()),
                _ => null,
            };
            if (bits is { } value)
            {
                members.Add((reader.GetString(field.Name), value));
            }
        }

        if (underlying is not { } underlyingKind)
        {
            return null;
        }

        var isFlags = false;
        foreach (var attributeHandle in typeDefinition.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var (attributeName, attributeNamespace) = attribute.Constructor.Kind switch
            {
                HandleKind.MemberReference when
                    reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent is
                        { Kind: HandleKind.TypeReference } parent =>
                    (reader.GetString(reader.GetTypeReference((TypeReferenceHandle)parent).Name),
                        reader.GetString(reader.GetTypeReference((TypeReferenceHandle)parent).Namespace)),
                HandleKind.MethodDefinition when
                    reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor)
                        .GetDeclaringType() is { IsNil: false } declaring =>
                    (reader.GetString(reader.GetTypeDefinition(declaring).Name),
                        reader.GetString(reader.GetTypeDefinition(declaring).Namespace)),
                _ => (string.Empty, string.Empty),
            };
            if (attributeName == "FlagsAttribute" && attributeNamespace == "System")
            {
                isFlags = true;
                break;
            }
        }

        return new EnumShape(typeFullName, isFlags, underlyingKind, members.ToImmutable());
    }

    // ---- typeof -----------------------------------------------------------------------------------------------------

    /// <summary>Folds <c>typeof(...)</c> into a typed reference for the type domains the evaluator models.</summary>
    private static FoldOutcome FoldTypeOf(TypeOfExpressionSyntax typeOf, FoldContext context)
    {
        if (TryResolveTypeRef(typeOf.Type, context, out var type, out var error))
        {
            return FoldOutcome.Folded(Operand.FromType(type!));
        }

        return error ?? FoldOutcome.Error(
            MemberUnsupportedCode,
            "typeof is supported for the evaluator's value domains — primitives, string, char, bool, the date "
            + "and time kinds, Guid, Version — for enum types, including enums declared in dump modules, for "
            + "single-rank arrays, and for the modeled generic types (Nullable<T>, List<T>, Dictionary<K,V>, "
            + "KeyValuePair<K,V>, and the generic collection interfaces), open or constructed.");
    }

    private static bool TryResolveTypeRef(
        TypeSyntax type,
        FoldContext context,
        out TypeRef? resolved,
        out FoldOutcome? error)
    {
        resolved = null;
        error = null;

        if (type is ArrayTypeSyntax
            {
                RankSpecifiers: [{ Sizes: [OmittedArraySizeExpressionSyntax] }],
                ElementType: { } arrayElement,
            })
        {
            if (!TryResolveTypeRef(arrayElement, context, out var element, out error))
            {
                return false;
            }

            resolved = element!.MakeArray();
            return true;
        }

        if (type is PredefinedTypeSyntax predefined)
        {
            var keyword = predefined.Keyword.ValueText;
            var runtimeName = predefined.Keyword.Kind() switch
            {
                SyntaxKind.StringKeyword => "String",
                SyntaxKind.CharKeyword => "Char",
                SyntaxKind.BoolKeyword => "Boolean",
                SyntaxKind.SByteKeyword => "SByte",
                SyntaxKind.ByteKeyword => "Byte",
                SyntaxKind.ShortKeyword => "Int16",
                SyntaxKind.UShortKeyword => "UInt16",
                SyntaxKind.IntKeyword => "Int32",
                SyntaxKind.UIntKeyword => "UInt32",
                SyntaxKind.LongKeyword => "Int64",
                SyntaxKind.ULongKeyword => "UInt64",
                SyntaxKind.FloatKeyword => "Single",
                SyntaxKind.DoubleKeyword => "Double",
                SyntaxKind.DecimalKeyword => "Decimal",
                SyntaxKind.ObjectKeyword => "Object",
                _ => null,
            };
            if (runtimeName is null)
            {
                return false;
            }

            resolved = new TypeRef(runtimeName, $"System.{runtimeName}", "System", keyword, IsEnum: false, null);
            return true;
        }

        if (TryReadTemporalTypeName(type, out var temporalKind))
        {
            var name = temporalKind.ToString();
            resolved = new TypeRef(name, $"System.{name}", "System", name, IsEnum: false, null);
            return true;
        }

        if (TryReadBclValueTypeName(type, out var valueKind))
        {
            var name = valueKind.ToString();
            resolved = new TypeRef(name, $"System.{name}", "System", name, IsEnum: false, null);
            return true;
        }

        // T? is Nullable<T> over a value-type element.
        if (type is NullableTypeSyntax nullable)
        {
            if (!TryResolveTypeRef(nullable.ElementType, context, out var lifted, out error))
            {
                return false;
            }

            if (!IsValueTypeRef(lifted!) || lifted!.Definition?.FullName == "System.Nullable`1")
            {
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    $"'{lifted!.CSharpName}?' is not a valid nullable type: the element must be a non-nullable "
                    + "value type.");
                return false;
            }

            resolved = MakeConstructed(FindGenericDef("Nullable", 1, null)!, [lifted]);
            return true;
        }

        // Generic names: constructed (List<int>) or open definitions (List<>, Dictionary<,>), optionally
        // namespace-qualified. The definitions come from a closed table pinned by the BCL's contract.
        var (genericName, genericQualifier) = type switch
        {
            GenericNameSyntax bare => (bare, (string?)null),
            QualifiedNameSyntax { Right: GenericNameSyntax right } qualifiedGeneric
                when TryReadDottedName(qualifiedGeneric.Left, out var prefix) => (right, prefix),
            _ => (null, null),
        };
        if (genericName is not null)
        {
            var argumentList = genericName.TypeArgumentList.Arguments;
            var def = FindGenericDef(genericName.Identifier.ValueText, argumentList.Count, genericQualifier);
            if (def is null)
            {
                return false;
            }

            if (argumentList.All(static argument => argument is OmittedTypeArgumentSyntax))
            {
                resolved = DefinitionRef(def);
                return true;
            }

            var argumentRefs = ImmutableArray.CreateBuilder<TypeRef>(argumentList.Count);
            foreach (var argumentSyntax in argumentList)
            {
                if (!TryResolveTypeRef(argumentSyntax, context, out var argumentRef, out error))
                {
                    return false;
                }

                if (argumentRef!.IsGenericDefinition)
                {
                    error = FoldOutcome.Error(
                        OperandTypeCode,
                        "An open generic definition cannot be a generic argument.");
                    return false;
                }

                argumentRefs.Add(argumentRef);
            }

            if (def.FullName == "System.Nullable`1"
                && (!IsValueTypeRef(argumentRefs[0])
                    || argumentRefs[0].Definition?.FullName == "System.Nullable`1"))
            {
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    "Nullable<T> requires a non-nullable value type argument.");
                return false;
            }

            resolved = MakeConstructed(def, argumentRefs.MoveToImmutable());
            return true;
        }

        // Dotted names resolve as enums: the closed BCL table first, then dump-module or referenced-assembly
        // metadata. An extern-alias qualifier is preserved as an 'alias::' prefix, which enum-shape resolution
        // reads to scope the lookup to that alias's references alone.
        string? aliasQualifier = null;
        var aliasQualifiedType = type;
        if (type is AliasQualifiedNameSyntax { Alias.Identifier.ValueText: var directAlias, Name: { } directName }
            && directAlias != "global")
        {
            aliasQualifier = directAlias;
            aliasQualifiedType = directName;
        }
        else if (type is QualifiedNameSyntax
            {
                Left: AliasQualifiedNameSyntax { Alias.Identifier.ValueText: var chainAlias },
            }
            && chainAlias != "global")
        {
            aliasQualifier = chainAlias;
        }

        var fullName = aliasQualifiedType switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified when TryReadDottedName(qualified, out var dotted) => dotted,
            _ => null,
        };
        if (fullName is null)
        {
            return false;
        }

        // A global-scope name (no alias) still consults the BCL table; an alias names references only.
        if (aliasQualifier is null && TryResolveNamedTypeRef(fullName, out resolved))
        {
            return true;
        }

        var scopedFullName = aliasQualifier is null ? fullName : $"{aliasQualifier}::{fullName}";
        error = TryResolveEnumShape(scopedFullName, context, out var shape);
        if (error is not null)
        {
            return false;
        }

        if (shape is null)
        {
            return false;
        }

        var shapeSeparator = shape.TypeFullName.LastIndexOf('.');
        resolved = new TypeRef(
            shape.ShortName,
            shape.TypeFullName,
            shapeSeparator < 0 ? null : shape.TypeFullName[..shapeSeparator],
            shape.ShortName,
            IsEnum: true,
            shape);
        return true;
    }

    private static bool TryReadDottedName(NameSyntax name, out string dotted)
    {
        dotted = string.Empty;
        var parts = new List<string>();
        var current = name;
        while (true)
        {
            switch (current)
            {
                case QualifiedNameSyntax qualified when qualified.Right is IdentifierNameSyntax right:
                    parts.Insert(0, right.Identifier.ValueText);
                    current = qualified.Left;
                    continue;
                case IdentifierNameSyntax identifier:
                    parts.Insert(0, identifier.Identifier.ValueText);
                    dotted = string.Join('.', parts);
                    return true;
                // The alias qualifier is read separately by the caller; here it contributes only its type name.
                case AliasQualifiedNameSyntax { Name: IdentifierNameSyntax aliased }:
                    parts.Insert(0, aliased.Identifier.ValueText);
                    dotted = string.Join('.', parts);
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>The deterministic members of a <c>typeof</c> reference.</summary>
    private static FoldOutcome DispatchTypeRefProperty(Operand receiver, string member)
    {
        var type = (TypeRef)receiver.Box!;
        return member switch
        {
            "Name" => FoldOutcome.Folded(Operand.FromString(type.Name)),
            // The runtime renders a constructed generic's FullName with assembly-qualified arguments, an identity
            // of the inspecting process's own core library — not evidence the dump provides.
            "FullName" when HasConstructedGenericComponent(type) => MemberUnsupported(
                "Type.FullName over a constructed generic, whose runtime form embeds assembly identities"),
            "FullName" => FoldOutcome.Folded(Operand.FromString(type.FullName)),
            "AssemblyQualifiedName" => MemberUnsupported(
                "Type.AssemblyQualifiedName, which embeds the inspecting runtime's assembly identity"),
            "Namespace" => type.Namespace is { } typeNamespace
                ? FoldOutcome.Folded(Operand.FromString(typeNamespace))
                : FoldOutcome.Folded(Operand.Null()),
            "IsEnum" => FoldOutcome.Folded(Operand.FromBoolean(type.IsEnum)),
            "IsArray" => FoldOutcome.Folded(Operand.FromBoolean(type.IsArray)),
            "HasElementType" => FoldOutcome.Folded(Operand.FromBoolean(type.IsArray)),
            "IsValueType" => FoldOutcome.Folded(Operand.FromBoolean(IsValueTypeRef(type))),
            "IsClass" => FoldOutcome.Folded(Operand.FromBoolean(
                !IsValueTypeRef(type) && !type.IsInterfaceType)),
            "IsInterface" => FoldOutcome.Folded(Operand.FromBoolean(type.IsInterfaceType)),
            "IsPrimitive" => FoldOutcome.Folded(Operand.FromBoolean(IsPrimitiveTypeRef(type))),
            "IsGenericType" => FoldOutcome.Folded(Operand.FromBoolean(type.Definition is not null)),
            "IsGenericTypeDefinition" => FoldOutcome.Folded(Operand.FromBoolean(type.IsGenericDefinition)),
            "IsConstructedGenericType" => FoldOutcome.Folded(Operand.FromBoolean(type.IsConstructedGeneric)),
            "ContainsGenericParameters" => FoldOutcome.Folded(Operand.FromBoolean(type.IsGenericDefinition)),
            "GenericTypeArguments" => TypeArgumentsSequence(type),
            "BaseType" => BaseTypeOf(type) is { } baseType
                ? FoldOutcome.Folded(Operand.FromType(baseType))
                : FoldOutcome.Folded(Operand.Null()),
            "UnderlyingSystemType" => FoldOutcome.Folded(receiver),
            _ => MemberUnsupported($"Type.{member}"),
        };
    }

    /// <summary>Whether a reference names a value type among the evaluator's modeled domains.</summary>
    private static bool IsValueTypeRef(TypeRef type) =>
        !type.IsArray
        && !type.IsInterfaceType
        && (type.Definition is { } definition
            ? definition.IsValue
            : type.FullName is not ("System.String" or "System.Object" or "System.Version" or "System.Array"
                or "System.Enum" or "System.ValueType"));

    /// <summary>Whether a reference names a CLR primitive: the fixed-size numerics, char, and bool.</summary>
    private static bool IsPrimitiveTypeRef(TypeRef type) => type.FullName is
        "System.SByte" or "System.Byte" or "System.Int16" or "System.UInt16" or "System.Int32" or
        "System.UInt32" or "System.Int64" or "System.UInt64" or "System.Single" or "System.Double" or
        "System.Char" or "System.Boolean";

    /// <summary>The deterministic methods of a <c>typeof</c> reference, including the enum helper family.</summary>
    private static FoldOutcome DispatchTypeRefMethod(Operand receiver, string name, List<Operand> arguments)
    {
        var type = (TypeRef)receiver.Box!;
        switch (name, arguments)
        {
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(type.FullName));
            case ("Equals", [{ Kind: OperandKind.Type } other]):
                return FoldOutcome.Folded(Operand.FromBoolean(string.Equals(
                    type.FullName,
                    ((TypeRef)other.Box!).FullName,
                    StringComparison.Ordinal)));
            case ("Equals", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case ("GetElementType", []):
                return type.ElementType is { } element
                    ? FoldOutcome.Folded(Operand.FromType(element))
                    : FoldOutcome.Folded(Operand.Null());
            case ("MakeArrayType", []):
                return type.IsArray
                    ? MemberUnsupported("Type.MakeArrayType over an array type")
                    : FoldOutcome.Folded(Operand.FromType(type.MakeArray()));
            case ("IsAssignableFrom", [{ Kind: OperandKind.Type } fromSource]):
                return FoldOutcome.Folded(Operand.FromBoolean(
                    IsAssignableBetween(type, (TypeRef)fromSource.Box!)));
            case ("IsAssignableFrom", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case ("IsAssignableTo", [{ Kind: OperandKind.Type } toTarget]):
                return FoldOutcome.Folded(Operand.FromBoolean(
                    IsAssignableBetween((TypeRef)toTarget.Box!, type)));
            case ("IsAssignableTo", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case ("IsSubclassOf", [{ Kind: OperandKind.Type } superclass]):
                var superRef = (TypeRef)superclass.Box!;
                return FoldOutcome.Folded(Operand.FromBoolean(BaseChainOf(type).Any(baseType =>
                    string.Equals(baseType.FullName, superRef.FullName, StringComparison.Ordinal))));
            case ("IsSubclassOf", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Error("System.ArgumentNullException", "IsSubclassOf requires a type.");
            case ("IsInstanceOfType", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case ("IsInstanceOfType", [{ } instance]):
                return TryDescribeRuntimeType(instance) is { } runtimeType
                    ? FoldOutcome.Folded(Operand.FromBoolean(IsAssignableBetween(type, runtimeType)))
                    : MemberUnsupported("Type.IsInstanceOfType over this operand's runtime identity");
            case ("MakeGenericType", _):
                return MakeGenericTypeOf(type, arguments);
            case ("GetGenericTypeDefinition", []):
                return type.IsConstructedGeneric
                    ? FoldOutcome.Folded(Operand.FromType(DefinitionRef(type.Definition!)))
                    : FoldOutcome.Error(
                        "System.InvalidOperationException",
                        "This operation is only valid on generic types.");
            case ("GetGenericArguments", []) when type.IsGenericDefinition:
                return MemberUnsupported(
                    "Type.GetGenericArguments over an open definition, whose generic parameters are not modeled");
            case ("GetGenericArguments", []):
                return TypeArgumentsSequence(type);
            case ("GetEnumNames", []) when type.Shape is { } namesShape:
                return CreateStringSequence(namesShape.Members.Select(static member => member.Name));
            case ("GetEnumValues", []) when type.Shape is { } valuesShape:
                return CreateSequence(new SequencePayload(
                    [.. valuesShape.Members.Select(member => EnumOperand(valuesShape, member.Bits))],
                    OperandKind.Enum,
                    default,
                    valuesShape.ShortName));
            case ("GetEnumUnderlyingType", []) when type.Shape is { } underlyingShape:
                var underlyingName = underlyingShape.Underlying.ToString();
                return FoldOutcome.Folded(Operand.FromType(new TypeRef(
                    underlyingName,
                    $"System.{underlyingName}",
                    "System",
                    CSharpNameOfNumeric(underlyingShape.Underlying),
                    IsEnum: false,
                    null)));
            case ("GetEnumName", [{ } value]) when type.Shape is { } nameShape:
                if (TryEnumArgumentBits(nameShape, value, out var namedBits))
                {
                    foreach (var (memberName, memberBits) in nameShape.Members)
                    {
                        if (memberBits == namedBits)
                        {
                            return FoldOutcome.Folded(Operand.FromString(memberName));
                        }
                    }

                    return FoldOutcome.Folded(Operand.Null());
                }

                return FoldOutcome.Error(OperandTypeCode, "GetEnumName takes an enum or integral value.");
            case ("IsEnumDefined", [{ } probe]) when type.Shape is { } definedShape:
                if (probe.Kind == OperandKind.String)
                {
                    return FoldOutcome.Folded(Operand.FromBoolean(definedShape.Members.Any(member =>
                        string.Equals(member.Name, probe.String, StringComparison.Ordinal))));
                }

                if (TryEnumArgumentBits(definedShape, probe, out var definedBits))
                {
                    return FoldOutcome.Folded(Operand.FromBoolean(definedShape.Members.Any(member =>
                        member.Bits == definedBits)));
                }

                return FoldOutcome.Error(
                    OperandTypeCode,
                    "IsEnumDefined takes an enum value, an integral value, or a member name.");
            case ("GetEnumNames" or "GetEnumValues" or "GetEnumUnderlyingType" or "GetEnumName" or "IsEnumDefined", _)
                when type.Shape is null:
                return FoldOutcome.Error(
                    "System.ArgumentException",
                    $"'{type.FullName}' is not an enum type the evaluator can enumerate.");
            default:
                return MemberUnsupported($"Type.{name}");
        }
    }

    // ---- Enum conversions and operators -----------------------------------------------------------------------------

    /// <summary>Folds a cast whose target names an enum type, known or dump-declared.</summary>
    private static FoldOutcome FoldEnumCast(CastExpressionSyntax cast, FoldContext context)
    {
        var typeName = cast.Type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified when TryReadDottedName(qualified, out var dotted) => dotted,
            _ => null,
        };
        if (typeName is null)
        {
            return FoldOutcome.NotArithmetic();
        }

        if (TryResolveEnumShape(typeName, context, out var shape) is { } shapeError)
        {
            return shapeError;
        }

        if (shape is null)
        {
            return FoldOutcome.NotArithmetic();
        }

        var operand = Fold(cast.Expression, context);
        if (operand.Disposition != FoldDisposition.Folded)
        {
            return operand;
        }

        return CastToEnum(shape, operand.Operand);
    }

    /// <summary>
    /// Casts a numeric or enum value to an enum type with C# semantics: the bits truncate unchecked to the
    /// underlying width, and the result is named through the declaration's members.
    /// </summary>
    private static FoldOutcome CastToEnum(EnumShape shape, Operand value)
    {
        if (!value.IsNumeric)
        {
            return FoldOutcome.Error(OperandTypeCode, "Only a numeric, char, or enum value converts to an enum type.");
        }

        var wide = ToBigInteger(NumericKindOf(value), BoxOf(value));
        var width = IntegralBitWidth(shape.Underlying);
        var mask = width == 64 ? ~default(ulong) : (1ul << width) - 1;
        var truncated = (ulong)(wide & mask);
        var bits = IsSigned(shape.Underlying) && width < 64 && (truncated & (1ul << (width - 1))) != 0
            ? unchecked((long)(truncated | ~mask))
            : unchecked((long)truncated);
        return FoldOutcome.Folded(EnumOperand(shape, bits));
    }

    /// <summary>
    /// The enum operator algebra C# defines beyond plain numeric folding: same-type bitwise operators keep the
    /// enum type, and an enum shifted by its underlying type through <c>+</c>/<c>-</c> keeps the enum type. A
    /// null return falls through to the numeric tower, which already gives <c>E - E</c> its underlying result
    /// and comparisons their meaning.
    /// </summary>
    private static FoldOutcome? TryComputeEnumBinary(
        SyntaxKind kind,
        Operand left,
        Operand right,
        FoldContext context)
    {
        var sameEnum = left.Kind == OperandKind.Enum && right.Kind == OperandKind.Enum &&
            string.Equals(left.EnumTypeFullName, right.EnumTypeFullName, StringComparison.Ordinal);
        if (sameEnum &&
            kind is SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or
                SyntaxKind.ExclusiveOrExpression)
        {
            var bits = kind switch
            {
                SyntaxKind.BitwiseAndExpression => left.EnumBits & right.EnumBits,
                SyntaxKind.BitwiseOrExpression => left.EnumBits | right.EnumBits,
                _ => left.EnumBits ^ right.EnumBits,
            };
            return FoldOutcome.Folded(NameEnumBits(left, bits, context));
        }

        // E + n and n + E keep the enum type; E - n keeps it too. E - E falls through to the numeric tower,
        // which produces the underlying difference exactly as C# defines.
        if (kind is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression &&
            (left.Kind == OperandKind.Enum) != (right.Kind == OperandKind.Enum))
        {
            var enumOperand = left.Kind == OperandKind.Enum ? left : right;
            var numericOperand = left.Kind == OperandKind.Enum ? right : left;
            if (kind == SyntaxKind.SubtractExpression && right.Kind == OperandKind.Enum)
            {
                // n - E is not defined by C#.
                return FoldOutcome.Error(OperandTypeCode, "A number cannot subtract an enum value.");
            }

            if (numericOperand.Kind is OperandKind.Int32 or OperandKind.Numeric &&
                IsIntegral(NumericKindOf(numericOperand)))
            {
                var offset = (long)ToBigInteger(NumericKindOf(numericOperand), BoxOf(numericOperand));
                var bits = kind == SyntaxKind.AddExpression
                    ? unchecked(enumOperand.EnumBits + offset)
                    : unchecked(enumOperand.EnumBits - offset);
                return FoldOutcome.Folded(NameEnumBits(enumOperand, bits, context));
            }
        }

        return null;
    }

    /// <summary>Rebuilds an enum operand with new bits, naming it through the resolved shape when one exists.</summary>
    private static Operand NameEnumBits(Operand prototype, long bits, FoldContext context)
    {
        var typeFullName = prototype.EnumTypeFullName!;
        if (TryResolveEnumShape(typeFullName, context, out var shape) is null && shape is not null)
        {
            return EnumOperand(shape, bits);
        }

        return Operand.FromEnum(
            bits,
            prototype.NumericKind,
            typeFullName,
            FormatEnumBitsInvariant(prototype.NumericKind, bits));
    }

    /// <summary>The bitwise complement keeps the enum type, masked to the underlying width.</summary>
    private static FoldOutcome ComputeEnumComplement(Operand operand, FoldContext context)
    {
        var width = IntegralBitWidth(operand.NumericKind);
        var mask = width == 64 ? ~0L : (1L << width) - 1;
        var complemented = ~operand.EnumBits & mask;
        var bits = IsSigned(operand.NumericKind) && width < 64 && (complemented & (1L << (width - 1))) != 0
            ? complemented | ~mask
            : complemented;
        return FoldOutcome.Folded(NameEnumBits(operand, bits, context));
    }

    // ---- Instance API -----------------------------------------------------------------------------------------------

    /// <summary>The deterministic instance surface of an enum value.</summary>
    private static FoldOutcome DispatchEnumMethod(Operand receiver, string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(receiver.EnumMemberName!));
            case ("ToString", [{ Kind: OperandKind.String } format]):
                return EnumToStringWithFormat(receiver, format.String!);
            case ("HasFlag", [{ Kind: OperandKind.Enum } flag]):
                if (!string.Equals(receiver.EnumTypeFullName, flag.EnumTypeFullName, StringComparison.Ordinal))
                {
                    return FoldOutcome.Error(
                        "System.ArgumentException",
                        "HasFlag requires a flag of the same enum type as the value.");
                }

                return FoldOutcome.Folded(Operand.FromBoolean(
                    (receiver.EnumBits & flag.EnumBits) == flag.EnumBits));
            case ("CompareTo", [{ Kind: OperandKind.Enum } other]):
                if (!string.Equals(receiver.EnumTypeFullName, other.EnumTypeFullName, StringComparison.Ordinal))
                {
                    return FoldOutcome.Error(
                        "System.ArgumentException",
                        "CompareTo requires a value of the same enum type.");
                }

                return FoldOutcome.Folded(Operand.FromInt32(receiver.NumericKind == NumericKind.UInt64
                    ? unchecked((ulong)receiver.EnumBits).CompareTo(unchecked((ulong)other.EnumBits))
                    : receiver.EnumBits.CompareTo(other.EnumBits)));
            default:
                return MemberUnsupported($"{receiver.EnumTypeFullName}.{name}");
        }
    }

    private static FoldOutcome EnumToStringWithFormat(Operand receiver, string format) =>
        format.Length == 1
            ? char.ToUpperInvariant(format[0]) switch
            {
                'G' or 'F' => FoldOutcome.Folded(Operand.FromString(receiver.EnumMemberName!)),
                'D' => FoldOutcome.Folded(Operand.FromString(
                    FormatEnumBitsInvariant(receiver.NumericKind, receiver.EnumBits))),
                'X' => FoldOutcome.Folded(Operand.FromString(FormatEnumBitsHex(receiver))),
                _ => FoldOutcome.Error("System.FormatException", $"'{format}' is not a valid enum format."),
            }
            : FoldOutcome.Error("System.FormatException", $"'{format}' is not a valid enum format.");

    private static string FormatEnumBitsHex(Operand receiver)
    {
        var digits = IntegralBitWidth(receiver.NumericKind) / 4;
        var mask = digits == 16 ? ~0L : (1L << (digits * 4)) - 1;
        return (receiver.EnumBits & mask).ToString(
            "X" + digits.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);
    }

    // ---- The System.Enum static API ---------------------------------------------------------------------------------

    /// <summary>
    /// The deterministic <see cref="Enum"/> statics in both spellings — <c>Enum.GetNames&lt;T&gt;()</c> and
    /// <c>Enum.GetNames(typeof(T))</c> — over known and dump-declared enums. <c>TryParse</c> needs an out
    /// parameter this expression model does not have, and is a typed stop naming <c>Parse</c> instead.
    /// </summary>
    private static FoldOutcome DispatchSystemEnum(
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext context)
    {
        EnumShape? shape = null;
        if (typeArguments.Count == 1)
        {
            if (!TryResolveTypeRef(typeArguments[0], context, out var generic, out var genericError))
            {
                return genericError ?? FoldOutcome.Error(
                    MemberUnsupportedCode,
                    $"The type argument of Enum.{name} does not name an enum the evaluator can resolve.");
            }

            shape = generic!.Shape;
        }
        else if (arguments is [{ Kind: OperandKind.Type } typeOperand, ..])
        {
            shape = ((TypeRef)typeOperand.Box!).Shape;
            arguments = arguments[1..];
        }

        if (name == "TryParse")
        {
            return FoldOutcome.Error(
                MemberUnsupportedCode,
                "Enum.TryParse needs an out parameter this expression model does not have; use Enum.Parse.");
        }

        if (shape is null)
        {
            return FoldOutcome.Error(
                MemberUnsupportedCode,
                $"Enum.{name} needs an enum type: a generic argument or a typeof(...) first argument naming an "
                + "enum the evaluator can resolve.");
        }

        switch (name, arguments)
        {
            case ("GetNames", []):
                return CreateStringSequence(shape.Members.Select(static member => member.Name));
            case ("GetValues", []):
                return CreateSequence(new SequencePayload(
                    [.. shape.Members.Select(member => EnumOperand(shape, member.Bits))],
                    OperandKind.Enum,
                    default,
                    shape.ShortName));
            case ("GetUnderlyingType", []):
                var underlyingName = shape.Underlying.ToString();
                return FoldOutcome.Folded(Operand.FromType(new TypeRef(
                    underlyingName,
                    $"System.{underlyingName}",
                    "System",
                    CSharpNameOfNumeric(shape.Underlying),
                    IsEnum: false,
                    null)));
            case ("GetName", [{ } value]):
                if (TryEnumArgumentBits(shape, value, out var namedBits))
                {
                    foreach (var (memberName, memberBits) in shape.Members)
                    {
                        if (memberBits == namedBits)
                        {
                            return FoldOutcome.Folded(Operand.FromString(memberName));
                        }
                    }

                    return FoldOutcome.Folded(Operand.Null());
                }

                return FoldOutcome.Error(OperandTypeCode, "Enum.GetName takes an enum or integral value.");
            case ("IsDefined", [{ } probe]):
                if (probe.Kind == OperandKind.String)
                {
                    return FoldOutcome.Folded(Operand.FromBoolean(shape.Members.Any(member =>
                        string.Equals(member.Name, probe.String, StringComparison.Ordinal))));
                }

                if (TryEnumArgumentBits(shape, probe, out var definedBits))
                {
                    return FoldOutcome.Folded(Operand.FromBoolean(shape.Members.Any(member =>
                        member.Bits == definedBits)));
                }

                return FoldOutcome.Error(
                    OperandTypeCode,
                    "Enum.IsDefined takes an enum value, an integral value, or a member name.");
            case ("Parse", [{ Kind: OperandKind.String } text]):
                return ParseEnum(shape, text.String!, ignoreCase: false);
            case ("Parse", [{ Kind: OperandKind.String } text, { Kind: OperandKind.Boolean } ignoreCase]):
                return ParseEnum(shape, text.String!, ignoreCase.Boolean);
            case ("ToObject", [{ } value]) when value.IsNumeric:
                return CastToEnum(shape, value);
            default:
                return MemberUnsupported($"Enum.{name}");
        }
    }

    private static bool TryEnumArgumentBits(EnumShape shape, Operand value, out long bits)
    {
        bits = 0;
        if (value.Kind == OperandKind.Enum &&
            string.Equals(value.EnumTypeFullName, shape.TypeFullName, StringComparison.Ordinal))
        {
            bits = value.EnumBits;
            return true;
        }

        if (value.IsNumeric && value.Kind != OperandKind.Enum && IsIntegral(NumericKindOf(value)))
        {
            bits = (long)ToBigInteger(NumericKindOf(value), BoxOf(value));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses with <see cref="Enum.Parse(Type, string, bool)"/> semantics: comma-separated member names combine
    /// by flags-or, and a plain integer spelling is the value itself; anything else is the argument error the
    /// BCL throws.
    /// </summary>
    private static FoldOutcome ParseEnum(EnumShape shape, string text, bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        long combined = 0;
        var any = false;
        foreach (var rawPart in text.Split(','))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            long? partBits = null;
            foreach (var (memberName, memberBits) in shape.Members)
            {
                if (string.Equals(memberName, part, comparison))
                {
                    partBits = memberBits;
                    break;
                }
            }

            if (partBits is null && long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var numeric))
            {
                partBits = numeric;
            }

            if (partBits is null)
            {
                return FoldOutcome.Error(
                    "System.ArgumentException",
                    $"Requested value '{part}' was not found in {shape.TypeFullName}.");
            }

            combined |= partBits.Value;
            any = true;
        }

        return any
            ? FoldOutcome.Folded(EnumOperand(shape, combined))
            : FoldOutcome.Error("System.ArgumentException", "An empty string is not a member of any enum.");
    }

    private static string CSharpNameOfNumeric(NumericKind kind) => kind switch
    {
        NumericKind.SByte => "sbyte",
        NumericKind.Byte => "byte",
        NumericKind.Int16 => "short",
        NumericKind.UInt16 => "ushort",
        NumericKind.UInt32 => "uint",
        NumericKind.Int64 => "long",
        NumericKind.UInt64 => "ulong",
        _ => "int",
    };
}
