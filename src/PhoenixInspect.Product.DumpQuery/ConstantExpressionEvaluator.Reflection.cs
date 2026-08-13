using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The deterministic reflection surface: member info queries over <see cref="Type"/> references
/// (<c>GetMethods</c>, <c>GetProperty</c>, …), the read-only <see cref="MemberInfo"/> family, invocation of
/// public members, and <see cref="Activator"/> construction. The universe is the closed set of runtime types the
/// evaluator already models, so info queries read the pinned analysis runtime's own metadata — deterministic
/// facts of the BCL contract — and every invocation routes back through the evaluator's fold dispatch tables.
/// That routing is the safety property: <c>Invoke</c> can only ever compute what the corresponding direct
/// spelling computes, so every culture, non-determinism, and admission stop keeps holding under reflection.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    private static bool IsReflectionKind(BclValueKind kind) => kind is
        BclValueKind.MethodInfo or BclValueKind.ConstructorInfo or BclValueKind.PropertyInfo or
        BclValueKind.FieldInfo or BclValueKind.ParameterInfo;

    /// <summary>The full name MemberTypes members carry, matching the known-enum dispatch table.</summary>
    private const string MemberTypesFullName = "System.Reflection.MemberTypes";

    /// <summary>Public declared-and-inherited members, matching the BCL's parameterless Get* defaults.</summary>
    private const BindingFlags PublicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    // ---- The modeled runtime-type universe --------------------------------------------------------------------------

    /// <summary>One entry of the closed reflection universe.</summary>
    /// <param name="Ref">The evaluator's reference for the type.</param>
    /// <param name="Runtime">The pinned analysis runtime's <see cref="Type"/>.</param>
    /// <param name="BareName">Whether the namespace-free name alone resolves in <c>typeof(...)</c>.</param>
    private sealed record ReflectionNamedType(TypeRef Ref, Type Runtime, bool BareName);

    private static readonly ImmutableArray<ReflectionNamedType> ReflectionNamedTypes =
    [
        new(SystemRef("Object", "object"), typeof(object), false),
        new(SystemRef("String", "string"), typeof(string), false),
        new(SystemRef("Char", "char"), typeof(char), false),
        new(SystemRef("Boolean", "bool"), typeof(bool), false),
        new(SystemRef("SByte", "sbyte"), typeof(sbyte), false),
        new(SystemRef("Byte", "byte"), typeof(byte), false),
        new(SystemRef("Int16", "short"), typeof(short), false),
        new(SystemRef("UInt16", "ushort"), typeof(ushort), false),
        new(SystemRef("Int32", "int"), typeof(int), false),
        new(SystemRef("UInt32", "uint"), typeof(uint), false),
        new(SystemRef("Int64", "long"), typeof(long), false),
        new(SystemRef("UInt64", "ulong"), typeof(ulong), false),
        new(SystemRef("Single", "float"), typeof(float), false),
        new(SystemRef("Double", "double"), typeof(double), false),
        new(SystemRef("Decimal", "decimal"), typeof(decimal), false),
        new(SystemRef("Int128", "Int128"), typeof(Int128), false),
        new(SystemRef("UInt128", "UInt128"), typeof(UInt128), false),
        new(SystemRef("Void", "void"), typeof(void), false),
        new(SystemRef("Math", "Math"), typeof(Math), true),
        new(SystemRef("Guid", "Guid"), typeof(Guid), true),
        new(SystemRef("Version", "Version"), typeof(Version), true),
        new(SystemRef("DateTime", "DateTime"), typeof(DateTime), true),
        new(SystemRef("DateTimeOffset", "DateTimeOffset"), typeof(DateTimeOffset), true),
        new(SystemRef("TimeSpan", "TimeSpan"), typeof(TimeSpan), true),
        new(SystemRef("DateOnly", "DateOnly"), typeof(DateOnly), true),
        new(SystemRef("TimeOnly", "TimeOnly"), typeof(TimeOnly), true),
        new(SystemRef("Enum", "Enum"), typeof(Enum), false),
        new(SystemRef("Array", "Array"), typeof(Array), false),
        new(SystemRef("ValueType", "ValueType"), typeof(ValueType), false),
        new(SystemRef("IComparable", "IComparable", isInterface: true), typeof(IComparable), false),
        new(NamespacedRef("Encoding", "System.Text"), typeof(Encoding), true),
        new(NamespacedRef("Regex", "System.Text.RegularExpressions"), typeof(Regex), true),
        new(NamespacedRef("Match", "System.Text.RegularExpressions"), typeof(Match), true),
        new(NamespacedRef("Group", "System.Text.RegularExpressions"), typeof(Group), true),
        new(NamespacedRef("Capture", "System.Text.RegularExpressions"), typeof(Capture), true),
        new(NamespacedRef("MatchCollection", "System.Text.RegularExpressions"), typeof(MatchCollection), true),
        new(NamespacedRef("GroupCollection", "System.Text.RegularExpressions"), typeof(GroupCollection), true),
        new(NamespacedRef("CaptureCollection", "System.Text.RegularExpressions"), typeof(CaptureCollection), true),
        new(NamespacedRef("Enumerable", "System.Linq"), typeof(Enumerable), true),
    ];

    private static TypeRef NamespacedRef(string name, string typeNamespace) => new(
        name, $"{typeNamespace}.{name}", typeNamespace, name, IsEnum: false, Shape: null);

    /// <summary>The runtime types of the modeled generic definitions, aligned with the definition table.</summary>
    private static Type? RuntimeGenericDefinitionOf(GenericDef def) => def.FullName switch
    {
        "System.Nullable`1" => typeof(Nullable<>),
        "System.Collections.Generic.List`1" => typeof(List<>),
        "System.Collections.Generic.Dictionary`2" => typeof(Dictionary<,>),
        "System.Collections.Generic.KeyValuePair`2" => typeof(KeyValuePair<,>),
        "System.Collections.Generic.IEnumerable`1" => typeof(IEnumerable<>),
        "System.Collections.Generic.ICollection`1" => typeof(ICollection<>),
        "System.Collections.Generic.IList`1" => typeof(IList<>),
        "System.Collections.Generic.IReadOnlyCollection`1" => typeof(IReadOnlyCollection<>),
        "System.Collections.Generic.IReadOnlyList`1" => typeof(IReadOnlyList<>),
        "System.Collections.Generic.IDictionary`2" => typeof(IDictionary<,>),
        "System.Collections.Generic.IReadOnlyDictionary`2" => typeof(IReadOnlyDictionary<,>),
        "System.IEquatable`1" => typeof(IEquatable<>),
        "System.IComparable`1" => typeof(IComparable<>),
        _ => null,
    };

    /// <summary>Resolves one <c>typeof</c> name — bare, <c>System</c>-qualified, or fully dotted — in the universe.</summary>
    private static bool TryResolveReflectionNamedRef(string name, out TypeRef? resolved)
    {
        resolved = null;
        foreach (var entry in ReflectionNamedTypes)
        {
            if (string.Equals(entry.Ref.FullName, name, StringComparison.Ordinal) ||
                (entry.BareName && string.Equals(entry.Ref.Name, name, StringComparison.Ordinal)))
            {
                resolved = entry.Ref;
                return true;
            }
        }

        return false;
    }

    /// <summary>Maps one modeled reference to the pinned runtime's type, or null outside the universe.</summary>
    private static Type? RuntimeTypeOfRef(TypeRef type)
    {
        if (type.IsArray)
        {
            return RuntimeTypeOfRef(type.ElementType!)?.MakeArrayType();
        }

        if (type.Definition is { } definition)
        {
            var runtimeDefinition = RuntimeGenericDefinitionOf(definition);
            if (runtimeDefinition is null || type.IsGenericDefinition)
            {
                return runtimeDefinition;
            }

            var runtimeArguments = new Type[type.TypeArguments.Length];
            for (var index = 0; index < type.TypeArguments.Length; index++)
            {
                if (RuntimeTypeOfRef(type.TypeArguments[index]) is not { } argument)
                {
                    return null;
                }

                runtimeArguments[index] = argument;
            }

            return runtimeDefinition.MakeGenericType(runtimeArguments);
        }

        foreach (var entry in ReflectionNamedTypes)
        {
            if (string.Equals(entry.Ref.FullName, type.FullName, StringComparison.Ordinal))
            {
                return entry.Runtime;
            }
        }

        return null;
    }

    /// <summary>Maps one runtime type back to the evaluator's reference, or null outside the universe.</summary>
    private static TypeRef? TypeRefOfRuntime(Type runtime)
    {
        if (runtime.IsArray && runtime.GetArrayRank() == 1)
        {
            return TypeRefOfRuntime(runtime.GetElementType()!)?.MakeArray();
        }

        if (runtime.IsConstructedGenericType)
        {
            // A modeled definition constructs a modeled reference; any other constructed generic falls through
            // to the descriptive fallback below, keeping signature reads total.
            var runtimeDefinition = runtime.GetGenericTypeDefinition();
            foreach (var def in KnownGenericDefs)
            {
                if (RuntimeGenericDefinitionOf(def) != runtimeDefinition)
                {
                    continue;
                }

                var argumentRefs = ImmutableArray.CreateBuilder<TypeRef>(runtime.GenericTypeArguments.Length);
                var argumentsModeled = true;
                foreach (var argument in runtime.GenericTypeArguments)
                {
                    if (TypeRefOfRuntime(argument) is not { } argumentRef)
                    {
                        argumentsModeled = false;
                        break;
                    }

                    argumentRefs.Add(argumentRef);
                }

                if (argumentsModeled)
                {
                    return MakeConstructed(def, argumentRefs.MoveToImmutable());
                }

                break;
            }
        }

        foreach (var entry in ReflectionNamedTypes)
        {
            if (entry.Runtime == runtime)
            {
                return entry.Ref;
            }
        }

        if (runtime.IsByRef || runtime.IsPointer || runtime.IsGenericParameter)
        {
            return null;
        }

        // Any other signature type — a ReadOnlySpan<char> parameter, an unmodeled enum — becomes a descriptive
        // reference: its canonical name is deterministic (no assembly identity), equality compares by that name,
        // and the modeled relations simply do not extend to it. This keeps ReturnType, ParameterType, and
        // overload filtering total instead of poisoning a whole member query with one exotic signature.
        return new TypeRef(
            runtime.Name,
            CanonicalRuntimeName(runtime),
            string.IsNullOrEmpty(runtime.Namespace) ? null : runtime.Namespace,
            runtime.Name,
            runtime.IsEnum,
            Shape: null);
    }

    /// <summary>
    /// The deterministic full name of one runtime type: the metadata name, with constructed generics in the
    /// evaluator's own <c>Def`n[Arg,Arg]</c> grammar so no assembly identity leaks into the answer.
    /// </summary>
    private static string CanonicalRuntimeName(Type runtime)
    {
        if (runtime.IsArray && runtime.GetElementType() is { } element)
        {
            return $"{CanonicalRuntimeName(element)}[]";
        }

        if (runtime.IsConstructedGenericType)
        {
            var arguments = string.Join(',', runtime.GenericTypeArguments.Select(CanonicalRuntimeName));
            return $"{runtime.GetGenericTypeDefinition().FullName}[{arguments}]";
        }

        return runtime.FullName ?? runtime.Name;
    }

    /// <summary>Maps one modeled reference to its static-dispatch receiver, when the type has a static surface.</summary>
    private static bool TryTypeReceiverOfRef(TypeRef type, out TypeReceiver receiver)
    {
        receiver = default;
        if (type.IsArray || type.Definition is not null)
        {
            return false;
        }

        switch (type.FullName)
        {
            case "System.String":
                receiver = new TypeReceiver(TypeReceiverCategory.String, default);
                return true;
            case "System.Char":
                receiver = new TypeReceiver(TypeReceiverCategory.Char, default);
                return true;
            case "System.Math":
                receiver = new TypeReceiver(TypeReceiverCategory.Math, default);
                return true;
            case "System.Linq.Enumerable":
                receiver = new TypeReceiver(TypeReceiverCategory.Enumerable, default);
                return true;
            case "System.Enum":
                receiver = new TypeReceiver(TypeReceiverCategory.SystemEnum, default);
                return true;
            case "System.Array":
                receiver = new TypeReceiver(TypeReceiverCategory.SystemArray, default);
                return true;
            case "System.Text.Encoding":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Encoding);
                return true;
            case "System.Text.RegularExpressions.Regex":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Regex);
                return true;
            case "System.Guid":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Guid);
                return true;
            case "System.Version":
                receiver = new TypeReceiver(TypeReceiverCategory.BclValue, default, Value: BclValueKind.Version);
                return true;
            default:
                if (Enum.TryParse<TemporalKind>(type.Name, out var temporal) &&
                    type.FullName == $"System.{type.Name}")
                {
                    receiver = new TypeReceiver(TypeReceiverCategory.Temporal, default, Temporal: temporal);
                    return true;
                }

                if (Enum.TryParse<NumericKind>(type.Name, out var numeric) &&
                    type.FullName == $"System.{type.Name}")
                {
                    receiver = new TypeReceiver(TypeReceiverCategory.Numeric, numeric);
                    return true;
                }

                return false;
        }
    }

    // ---- Rendering --------------------------------------------------------------------------------------------------

    /// <summary>Renders one reflection info in the BCL's own <c>ToString</c> form, such as <c>Int32 Abs(Int32)</c>.</summary>
    private static string RenderReflectionValue(Operand operand) => operand.Box!.ToString()!;

    // ---- Member queries over Type references ------------------------------------------------------------------------

    /// <summary>
    /// Answers the public member queries of one <c>typeof</c> reference. Reflection enumeration order is
    /// unspecified by the runtime, so every list is sorted ordinally by name and signature to keep the answer
    /// canonical across runs.
    /// </summary>
    private static bool TryDispatchTypeReflectionQuery(
        TypeRef type,
        string name,
        List<Operand> arguments,
        out FoldOutcome outcome)
    {
        outcome = default;
        if (name is not ("GetMethods" or "GetProperties" or "GetFields" or "GetConstructors" or "GetMembers"
            or "GetMethod" or "GetProperty" or "GetField" or "GetMember"))
        {
            return false;
        }

        if (RuntimeTypeOfRef(type) is not { } runtime)
        {
            outcome = FoldOutcome.Error(
                MemberUnsupportedCode,
                $"Reflection over '{type.FullName}' is not modeled; a dump-declared enum answers through "
                + "GetEnumNames, GetEnumValues, and the Enum statics instead.");
            return true;
        }

        switch (name, arguments)
        {
            case ("GetMethods", []):
                outcome = MemberSequence(
                    runtime.GetMethods(PublicMembers).OrderBy(SignatureOf, StringComparer.Ordinal),
                    BclValueKind.MethodInfo,
                    "MethodInfo");
                return true;
            case ("GetProperties", []):
                outcome = MemberSequence(
                    runtime.GetProperties(PublicMembers).OrderBy(SignatureOf, StringComparer.Ordinal),
                    BclValueKind.PropertyInfo,
                    "PropertyInfo");
                return true;
            case ("GetFields", []):
                outcome = MemberSequence(
                    runtime.GetFields(PublicMembers).OrderBy(SignatureOf, StringComparer.Ordinal),
                    BclValueKind.FieldInfo,
                    "FieldInfo");
                return true;
            case ("GetConstructors", []):
                outcome = MemberSequence(
                    runtime.GetConstructors().OrderBy(SignatureOf, StringComparer.Ordinal),
                    BclValueKind.ConstructorInfo,
                    "ConstructorInfo");
                return true;
            case ("GetMembers", []):
                outcome = CreateSequence(new SequencePayload(
                    [.. EnumeratePublicMembers(runtime).Select(MemberOperand)],
                    OperandKind.BclValue,
                    default,
                    "MemberInfo"));
                return true;
            case ("GetMember", [{ Kind: OperandKind.String } memberName]):
                outcome = CreateSequence(new SequencePayload(
                    [
                        .. EnumeratePublicMembers(runtime)
                            .Where(member => string.Equals(member.Name, memberName.String, StringComparison.Ordinal))
                            .Select(MemberOperand),
                    ],
                    OperandKind.BclValue,
                    default,
                    "MemberInfo"));
                return true;
            case ("GetMethod", [{ Kind: OperandKind.String } methodName]):
                outcome = SingleMember(
                    runtime.GetMethods(PublicMembers)
                        .Where(method => string.Equals(method.Name, methodName.String, StringComparison.Ordinal)),
                    BclValueKind.MethodInfo,
                    $"Type.GetMethod(\"{methodName.String}\")");
                return true;
            case ("GetProperty", [{ Kind: OperandKind.String } propertyName]):
                outcome = SingleMember(
                    runtime.GetProperties(PublicMembers).Where(property =>
                        string.Equals(property.Name, propertyName.String, StringComparison.Ordinal)),
                    BclValueKind.PropertyInfo,
                    $"Type.GetProperty(\"{propertyName.String}\")");
                return true;
            case ("GetField", [{ Kind: OperandKind.String } fieldName]):
                outcome = SingleMember(
                    runtime.GetFields(PublicMembers).Where(field =>
                        string.Equals(field.Name, fieldName.String, StringComparison.Ordinal)),
                    BclValueKind.FieldInfo,
                    $"Type.GetField(\"{fieldName.String}\")");
                return true;
            default:
                outcome = FoldOutcome.Error(
                    MemberUnsupportedCode,
                    $"'Type.{name}' with this argument shape is outside the modeled surface, which answers the "
                    + "parameterless overloads and name lookups over public members.");
                return true;
        }
    }

    /// <summary>The canonical ordering key: the member's invariant signature string.</summary>
    private static string SignatureOf(MemberInfo member) => member.ToString()!;

    /// <summary>The public methods, properties, fields, and constructors, in canonical order.</summary>
    private static IEnumerable<MemberInfo> EnumeratePublicMembers(Type runtime) =>
        runtime.GetMethods(PublicMembers).Cast<MemberInfo>()
            .Concat(runtime.GetProperties(PublicMembers))
            .Concat(runtime.GetFields(PublicMembers))
            .Concat(runtime.GetConstructors())
            .OrderBy(static member => member.Name, StringComparer.Ordinal)
            .ThenBy(SignatureOf, StringComparer.Ordinal);

    private static Operand MemberOperand(MemberInfo member) => member switch
    {
        System.Reflection.MethodInfo method => Operand.FromBclValue(BclValueKind.MethodInfo, method),
        System.Reflection.ConstructorInfo constructor =>
            Operand.FromBclValue(BclValueKind.ConstructorInfo, constructor),
        System.Reflection.PropertyInfo property => Operand.FromBclValue(BclValueKind.PropertyInfo, property),
        _ => Operand.FromBclValue(BclValueKind.FieldInfo, (System.Reflection.FieldInfo)member),
    };

    private static FoldOutcome MemberSequence(
        IEnumerable<MemberInfo> members,
        BclValueKind kind,
        string displayName) => CreateSequence(new SequencePayload(
        [.. members.Select(member => Operand.FromBclValue(kind, member))],
        OperandKind.BclValue,
        default,
        displayName));

    /// <summary>One member by name: the match, exactly null for none, or the BCL's ambiguity refusal.</summary>
    private static FoldOutcome SingleMember(IEnumerable<MemberInfo> matches, BclValueKind kind, string query)
    {
        var found = matches.OrderBy(SignatureOf, StringComparer.Ordinal).Take(2).ToList();
        return found.Count switch
        {
            0 => FoldOutcome.Folded(Operand.Null()),
            1 => FoldOutcome.Folded(Operand.FromBclValue(kind, found[0])),
            _ => FoldOutcome.Error(
                "System.Reflection.AmbiguousMatchException",
                $"{query} matches more than one public member; enumerate the overloads with the plural query "
                + "and filter by GetParameters()."),
        };
    }

    // ---- Member info: read-only properties --------------------------------------------------------------------------

    private static FoldOutcome DispatchReflectionProperty(Operand receiver, string member)
    {
        switch (receiver.BclValueKind)
        {
            case BclValueKind.MethodInfo:
                var method = (MethodInfo)receiver.Box!;
                return member switch
                {
                    "Name" => FoldOutcome.Folded(Operand.FromString(method.Name)),
                    "IsStatic" => FoldOutcome.Folded(Operand.FromBoolean(method.IsStatic)),
                    "IsPublic" => FoldOutcome.Folded(Operand.FromBoolean(method.IsPublic)),
                    "IsGenericMethod" => FoldOutcome.Folded(Operand.FromBoolean(method.IsGenericMethod)),
                    "ReturnType" => TypeOperandOfRuntime(method.ReturnType, "MethodInfo.ReturnType"),
                    "DeclaringType" => TypeOperandOfRuntime(method.DeclaringType, "MethodInfo.DeclaringType"),
                    "MemberType" => MemberTypesOperand(method.MemberType),
                    _ => MemberUnsupported($"MethodInfo.{member}"),
                };
            case BclValueKind.ConstructorInfo:
                var constructor = (ConstructorInfo)receiver.Box!;
                return member switch
                {
                    "Name" => FoldOutcome.Folded(Operand.FromString(constructor.Name)),
                    "IsStatic" => FoldOutcome.Folded(Operand.FromBoolean(constructor.IsStatic)),
                    "IsPublic" => FoldOutcome.Folded(Operand.FromBoolean(constructor.IsPublic)),
                    "DeclaringType" =>
                        TypeOperandOfRuntime(constructor.DeclaringType, "ConstructorInfo.DeclaringType"),
                    "MemberType" => MemberTypesOperand(constructor.MemberType),
                    _ => MemberUnsupported($"ConstructorInfo.{member}"),
                };
            case BclValueKind.PropertyInfo:
                var property = (PropertyInfo)receiver.Box!;
                return member switch
                {
                    "Name" => FoldOutcome.Folded(Operand.FromString(property.Name)),
                    "CanRead" => FoldOutcome.Folded(Operand.FromBoolean(property.CanRead)),
                    "CanWrite" => FoldOutcome.Folded(Operand.FromBoolean(property.CanWrite)),
                    "PropertyType" => TypeOperandOfRuntime(property.PropertyType, "PropertyInfo.PropertyType"),
                    "DeclaringType" => TypeOperandOfRuntime(property.DeclaringType, "PropertyInfo.DeclaringType"),
                    "MemberType" => MemberTypesOperand(property.MemberType),
                    _ => MemberUnsupported($"PropertyInfo.{member}"),
                };
            case BclValueKind.FieldInfo:
                var field = (FieldInfo)receiver.Box!;
                return member switch
                {
                    "Name" => FoldOutcome.Folded(Operand.FromString(field.Name)),
                    "IsStatic" => FoldOutcome.Folded(Operand.FromBoolean(field.IsStatic)),
                    "IsPublic" => FoldOutcome.Folded(Operand.FromBoolean(field.IsPublic)),
                    "IsLiteral" => FoldOutcome.Folded(Operand.FromBoolean(field.IsLiteral)),
                    "IsInitOnly" => FoldOutcome.Folded(Operand.FromBoolean(field.IsInitOnly)),
                    "FieldType" => TypeOperandOfRuntime(field.FieldType, "FieldInfo.FieldType"),
                    "DeclaringType" => TypeOperandOfRuntime(field.DeclaringType, "FieldInfo.DeclaringType"),
                    "MemberType" => MemberTypesOperand(field.MemberType),
                    _ => MemberUnsupported($"FieldInfo.{member}"),
                };
            default:
                var parameter = (ParameterInfo)receiver.Box!;
                return member switch
                {
                    "Name" => parameter.Name is { } parameterName
                        ? FoldOutcome.Folded(Operand.FromString(parameterName))
                        : FoldOutcome.Folded(Operand.Null()),
                    "Position" => FoldOutcome.Folded(Operand.FromInt32(parameter.Position)),
                    "HasDefaultValue" => FoldOutcome.Folded(Operand.FromBoolean(parameter.HasDefaultValue)),
                    "IsOptional" => FoldOutcome.Folded(Operand.FromBoolean(parameter.IsOptional)),
                    "ParameterType" =>
                        TypeOperandOfRuntime(parameter.ParameterType, "ParameterInfo.ParameterType"),
                    _ => MemberUnsupported($"ParameterInfo.{member}"),
                };
        }
    }

    private static FoldOutcome TypeOperandOfRuntime(Type? runtime, string member) =>
        runtime is not null && TypeRefOfRuntime(runtime) is { } reference
            ? FoldOutcome.Folded(Operand.FromType(reference))
            : FoldOutcome.Error(
                MemberUnsupportedCode,
                $"'{member}' names a type outside the evaluator's modeled universe.");

    private static FoldOutcome MemberTypesOperand(MemberTypes memberType) => FoldOutcome.Folded(Operand.FromEnum(
        (int)memberType,
        "System.Reflection.MemberTypes",
        memberType.ToString()));

    // ---- Member info: methods and invocation ------------------------------------------------------------------------

    private static FoldOutcome DispatchReflectionMethod(
        Operand receiver,
        string name,
        List<Operand> arguments,
        FoldContext? context)
    {
        if (name == "ToString" && arguments.Count == 0)
        {
            return FoldOutcome.Folded(Operand.FromString(RenderReflectionValue(receiver)));
        }

        switch (receiver.BclValueKind)
        {
            case BclValueKind.MethodInfo:
                var method = (MethodInfo)receiver.Box!;
                return (name, arguments) switch
                {
                    ("GetParameters", []) => ParameterSequence(method.GetParameters()),
                    ("Invoke", [{ } target, { } invocationArguments]) =>
                        InvokeReflectionMethod(method, target, invocationArguments, context),
                    _ => MemberUnsupported($"MethodInfo.{name}"),
                };
            case BclValueKind.ConstructorInfo:
                var constructor = (ConstructorInfo)receiver.Box!;
                return (name, arguments) switch
                {
                    ("GetParameters", []) => ParameterSequence(constructor.GetParameters()),
                    ("Invoke", [{ } constructionArguments]) =>
                        InvokeReflectionConstructor(constructor, constructionArguments),
                    _ => MemberUnsupported($"ConstructorInfo.{name}"),
                };
            case BclValueKind.PropertyInfo:
                var property = (PropertyInfo)receiver.Box!;
                return (name, arguments) switch
                {
                    ("GetValue", [{ } target]) => ReadReflectionProperty(property, target),
                    ("GetValue", [{ } target, { Kind: OperandKind.Null }]) =>
                        ReadReflectionProperty(property, target),
                    ("GetGetMethod", []) => property.GetMethod is { IsPublic: true } getter
                        ? FoldOutcome.Folded(Operand.FromBclValue(BclValueKind.MethodInfo, getter))
                        : FoldOutcome.Folded(Operand.Null()),
                    ("GetSetMethod", []) => property.SetMethod is { IsPublic: true } setter
                        ? FoldOutcome.Folded(Operand.FromBclValue(BclValueKind.MethodInfo, setter))
                        : FoldOutcome.Folded(Operand.Null()),
                    ("GetIndexParameters", []) => ParameterSequence(property.GetIndexParameters()),
                    ("SetValue", _) => ImmutableEvidenceStop("PropertyInfo.SetValue"),
                    _ => MemberUnsupported($"PropertyInfo.{name}"),
                };
            case BclValueKind.FieldInfo:
                var field = (FieldInfo)receiver.Box!;
                return (name, arguments) switch
                {
                    ("GetValue", [{ } _]) => ReadReflectionField(field),
                    ("SetValue", _) => ImmutableEvidenceStop("FieldInfo.SetValue"),
                    _ => MemberUnsupported($"FieldInfo.{name}"),
                };
            default:
                return MemberUnsupported($"ParameterInfo.{name}");
        }
    }

    private static FoldOutcome ImmutableEvidenceStop(string member) => FoldOutcome.Error(
        MemberUnsupportedCode,
        $"'{member}' mutates state, and every evaluated value is immutable evidence; reflection is read-only "
        + "and invocation-only here.");

    private static FoldOutcome ParameterSequence(ParameterInfo[] parameters) => CreateSequence(new SequencePayload(
        [.. parameters.Select(static parameter => Operand.FromBclValue(BclValueKind.ParameterInfo, parameter))],
        OperandKind.BclValue,
        default,
        "ParameterInfo"));

    /// <summary>Reads the Invoke argument array: exactly null for no arguments, or one folded array's elements.</summary>
    private static bool TryReadInvocationArguments(
        Operand argumentsOperand,
        out List<Operand> arguments,
        out FoldOutcome error)
    {
        error = default;
        switch (argumentsOperand.Kind)
        {
            case OperandKind.Null:
                arguments = [];
                return true;
            case OperandKind.Sequence:
                arguments = [.. PayloadOf(argumentsOperand).Items];
                return true;
            default:
                arguments = [];
                error = FoldOutcome.Error(
                    OperandTypeCode,
                    "The arguments must be null or an array of constant values, such as 'new object[] { … }'.");
                return false;
        }
    }

    /// <summary>
    /// Invokes one public method by routing it through the evaluator's own dispatch tables. The method's name and
    /// the folded operands select the fold exactly as the direct spelling would, so the deterministic allowlist —
    /// including its culture and non-determinism stops — is enforced identically under reflection.
    /// </summary>
    private static FoldOutcome InvokeReflectionMethod(
        MethodInfo method,
        Operand target,
        Operand argumentsOperand,
        FoldContext? context)
    {
        if (!TryReadInvocationArguments(argumentsOperand, out var arguments, out var argumentError))
        {
            return argumentError;
        }

        if (arguments.Count != method.GetParameters().Length)
        {
            return FoldOutcome.Error(
                "System.Reflection.TargetParameterCountException",
                $"'{method}' takes {method.GetParameters().Length.ToString(CultureInfo.InvariantCulture)} "
                + $"argument(s); {arguments.Count.ToString(CultureInfo.InvariantCulture)} were supplied.");
        }

        if (method.IsStatic)
        {
            // The BCL ignores the target of a static invocation, and so does this routing.
            if (method.DeclaringType is not { } declaring ||
                TypeRefOfRuntime(declaring) is not { } declaringRef ||
                !TryTypeReceiverOfRef(declaringRef, out var typeReceiver))
            {
                return MemberUnsupported($"{method.DeclaringType?.Name}.{method.Name} via reflection");
            }

            return DispatchTypeReceiverInvocation(typeReceiver, method.Name, default, arguments, context);
        }

        if (target.Kind == OperandKind.Null)
        {
            return FoldOutcome.Error(
                "System.Reflection.TargetException",
                "A non-static method requires a target instance.");
        }

        return DispatchOperandInvocation(target, method.Name, arguments);
    }

    private static FoldOutcome InvokeReflectionConstructor(ConstructorInfo constructor, Operand argumentsOperand)
    {
        if (!TryReadInvocationArguments(argumentsOperand, out var arguments, out var argumentError))
        {
            return argumentError;
        }

        if (arguments.Count != constructor.GetParameters().Length)
        {
            return FoldOutcome.Error(
                "System.Reflection.TargetParameterCountException",
                $"'{constructor}' takes {constructor.GetParameters().Length.ToString(CultureInfo.InvariantCulture)} "
                + $"argument(s); {arguments.Count.ToString(CultureInfo.InvariantCulture)} were supplied.");
        }

        return constructor.DeclaringType is { } declaring && TypeRefOfRuntime(declaring) is { } declaringRef
            ? ConstructModeledInstance(declaringRef, arguments)
            : MemberUnsupported("ConstructorInfo.Invoke over this declaring type");
    }

    private static FoldOutcome ReadReflectionProperty(PropertyInfo property, Operand target)
    {
        if (property.GetMethod is not { IsPublic: true } getter)
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{property.Name}' has no public getter.");
        }

        if (property.GetIndexParameters().Length > 0)
        {
            return MemberUnsupported($"PropertyInfo.GetValue over the indexer '{property.Name}'; use element access");
        }

        if (getter.IsStatic)
        {
            return property.DeclaringType is { } declaring &&
                TypeRefOfRuntime(declaring) is { } declaringRef &&
                TryTypeReceiverOfRef(declaringRef, out var typeReceiver)
                ? DispatchTypeStatic(typeReceiver, property.Name)
                : MemberUnsupported($"{property.DeclaringType?.Name}.{property.Name} via reflection");
        }

        if (target.Kind == OperandKind.Null)
        {
            return FoldOutcome.Error(
                "System.Reflection.TargetException",
                "A non-static property requires a target instance.");
        }

        var outcome = DispatchOperandProperty(target, property.Name);
        return outcome.Disposition == FoldDisposition.NotArithmetic
            ? MemberUnsupported($"{property.Name} over this operand via reflection")
            : outcome;
    }

    private static FoldOutcome ReadReflectionField(FieldInfo field)
    {
        // An enum's literal fields are metadata constants: the value is read from the field's raw constant, not
        // computed, so no code runs.
        if (field.DeclaringType is { IsEnum: true } enumType && field.IsLiteral &&
            enumType.FullName is { } enumFullName &&
            Enum.TryParse<NumericKind>(Enum.GetUnderlyingType(enumType).Name, out var underlying))
        {
            long? bits = field.GetRawConstantValue() switch
            {
                sbyte value => value,
                byte value => value,
                short value => value,
                ushort value => value,
                int value => value,
                uint value => value,
                long value => value,
                ulong value => unchecked((long)value),
                _ => null,
            };
            return bits is { } enumBits
                ? FoldOutcome.Folded(Operand.FromEnum(enumBits, underlying, enumFullName, field.Name))
                : MemberUnsupported($"FieldInfo.GetValue over '{field.Name}'");
        }

        if (field.IsStatic)
        {
            return field.DeclaringType is { } declaring &&
                TypeRefOfRuntime(declaring) is { } declaringRef &&
                TryTypeReceiverOfRef(declaringRef, out var typeReceiver)
                ? DispatchTypeStatic(typeReceiver, field.Name)
                : MemberUnsupported($"{field.DeclaringType?.Name}.{field.Name} via reflection");
        }

        return MemberUnsupported($"FieldInfo.GetValue over the instance field '{field.Name}'");
    }

    // ---- System.Activator -------------------------------------------------------------------------------------------

    /// <summary>
    /// The <see cref="Activator"/> surface: parameterless and argument construction over the modeled value
    /// domains, in the generic and <c>Type</c>-argument spellings, routed through the same constructors the
    /// direct <c>new</c> spellings fold with.
    /// </summary>
    private static FoldOutcome DispatchActivator(
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext context)
    {
        if (name != "CreateInstance")
        {
            return MemberUnsupported($"Activator.{name}");
        }

        if (typeArguments.Count == 1)
        {
            if (!TryResolveTypeRef(typeArguments[0], context, out var genericTarget, out var resolveError))
            {
                return resolveError ?? MemberUnsupported("Activator.CreateInstance over this type argument");
            }

            return arguments.Count == 0
                ? ConstructModeledInstance(genericTarget!, [])
                : FoldOutcome.Error(
                    OperandTypeCode,
                    "Activator.CreateInstance<T> takes no arguments; pass a Type and arguments instead.");
        }

        return arguments switch
        {
            [{ Kind: OperandKind.Type } target] =>
                ConstructModeledInstance((TypeRef)target.Box!, []),
            [{ Kind: OperandKind.Type } target, { Kind: OperandKind.Null }] =>
                ConstructModeledInstance((TypeRef)target.Box!, []),
            [{ Kind: OperandKind.Type } target, { Kind: OperandKind.Sequence } constructorArguments] =>
                ConstructModeledInstance((TypeRef)target.Box!, [.. PayloadOf(constructorArguments).Items]),
            [{ Kind: OperandKind.Type } target, .. var rest] =>
                ConstructModeledInstance((TypeRef)target.Box!, [.. rest]),
            _ => FoldOutcome.Error(
                OperandTypeCode,
                "Activator.CreateInstance takes a Type reference and optional constant arguments."),
        };
    }

    /// <summary>Constructs one instance of a modeled type, with the BCL's missing-constructor refusals.</summary>
    private static FoldOutcome ConstructModeledInstance(TypeRef type, List<Operand> arguments)
    {
        if (type.Shape is { } shape)
        {
            return arguments.Count == 0
                ? FoldOutcome.Folded(EnumOperand(shape, 0))
                : FoldOutcome.Error(
                    "System.MissingMethodException",
                    $"'{type.FullName}' has no public constructor taking arguments.");
        }

        if (type.IsArray || type.IsInterfaceType || type.Definition is not null)
        {
            return MemberUnsupported($"construction of '{type.CSharpName}'");
        }

        switch (type.FullName)
        {
            case "System.Boolean":
                return ConstructedDefaultOnly(type, arguments, Operand.FromBoolean(false));
            case "System.Char":
                return ConstructedDefaultOnly(type, arguments, Operand.FromChar('\0'));
            case "System.String":
                return FoldOutcome.Error(
                    "System.MissingMethodException",
                    "'System.String' has no public parameterless constructor, and its char-buffer constructors "
                    + "are not modeled; write the string literal directly.");
            case "System.Guid" or "System.Version":
            case "System.Text.RegularExpressions.Regex":
                return DispatchBclValueConstruction(
                    Enum.Parse<BclValueKind>(type.Name),
                    arguments);
            case "System.Text.Encoding":
                return FoldOutcome.Error(
                    "System.MissingMethodException",
                    "'System.Text.Encoding' is abstract; obtain an instance from the Encoding statics.");
            default:
                if (Enum.TryParse<TemporalKind>(type.Name, out var temporal) &&
                    type.FullName == $"System.{type.Name}")
                {
                    if (arguments.Count == 0)
                    {
                        return Temporal(temporal, DefaultTemporalValue(temporal));
                    }

                    try
                    {
                        return ConstructTemporal(temporal, arguments);
                    }
                    catch (ArgumentException exception)
                    {
                        return FoldOutcome.Error(ArgumentOutOfRangeCode, exception.Message);
                    }
                }

                if (Enum.TryParse<NumericKind>(type.Name, out var numeric) &&
                    type.FullName == $"System.{type.Name}")
                {
                    return ConstructedDefaultOnly(type, arguments, numeric == NumericKind.Int32
                        ? Operand.FromInt32(0)
                        : Operand.FromNumeric(numeric, BoxFromBigInteger(numeric, 0)));
                }

                return MemberUnsupported($"construction of '{type.CSharpName}'");
        }
    }

    private static FoldOutcome ConstructedDefaultOnly(TypeRef type, List<Operand> arguments, Operand defaultValue) =>
        arguments.Count == 0
            ? FoldOutcome.Folded(defaultValue)
            : FoldOutcome.Error(
                "System.MissingMethodException",
                $"'{type.FullName}' has no public constructor taking arguments.");

    private static object DefaultTemporalValue(TemporalKind kind) => kind switch
    {
        TemporalKind.DateTime => default(DateTime),
        TemporalKind.DateTimeOffset => default(DateTimeOffset),
        TemporalKind.TimeSpan => default(TimeSpan),
        TemporalKind.DateOnly => default(DateOnly),
        _ => default(TimeOnly),
    };
}
