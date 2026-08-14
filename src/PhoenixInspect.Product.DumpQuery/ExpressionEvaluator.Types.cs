using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The type-composition and type-relationship surface of <see cref="Type"/>: generic type definitions and
/// constructed generics (<c>typeof(List&lt;int&gt;)</c>, <c>MakeGenericType</c>), and the assignability relation
/// (<c>IsAssignableFrom</c>, <c>IsSubclassOf</c>, <c>BaseType</c>). The universe is closed on purpose: every
/// definition, base edge, implemented interface, and variance annotation comes from a fixed table whose facts are
/// pinned by the BCL's public contract, so an answer is either exactly the runtime's or a typed stop — never a
/// guess about a type the table does not model.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>The variance of one generic parameter, as declared by the definition.</summary>
    private enum GenericVariance
    {
        /// <summary>No variance: arguments must be identical.</summary>
        Invariant = 0,

        /// <summary>Covariant (<c>out T</c>): a reference-conversion from source to target argument suffices.</summary>
        Out = 1,

        /// <summary>Contravariant (<c>in T</c>): a reference-conversion from target to source argument suffices.</summary>
        In = 2,
    }

    /// <summary>One well-known generic type definition the evaluator models.</summary>
    /// <param name="FullName">The metadata full name including arity, such as <c>System.Collections.Generic.List`1</c>.</param>
    /// <param name="Name">The namespace-free metadata name, such as <c>List`1</c>.</param>
    /// <param name="Namespace">The declaring namespace.</param>
    /// <param name="ShortDisplay">The C# identifier without arity, such as <c>List</c>.</param>
    /// <param name="Arity">The number of generic parameters.</param>
    /// <param name="IsInterface">Whether the definition declares an interface.</param>
    /// <param name="IsValue">Whether constructed instances are value types.</param>
    /// <param name="Variance">The declared variance of each parameter, in order.</param>
    private sealed record GenericDef(
        string FullName,
        string Name,
        string Namespace,
        string ShortDisplay,
        int Arity,
        bool IsInterface,
        bool IsValue,
        ImmutableArray<GenericVariance> Variance);

    private static readonly ImmutableArray<GenericDef> KnownGenericDefs =
    [
        new("System.Nullable`1", "Nullable`1", "System", "Nullable", 1, false, true,
            [GenericVariance.Invariant]),
        new("System.Collections.Generic.List`1", "List`1", "System.Collections.Generic", "List", 1, false, false,
            [GenericVariance.Invariant]),
        new("System.Collections.Generic.Dictionary`2", "Dictionary`2", "System.Collections.Generic", "Dictionary",
            2, false, false, [GenericVariance.Invariant, GenericVariance.Invariant]),
        new("System.Collections.Generic.KeyValuePair`2", "KeyValuePair`2", "System.Collections.Generic",
            "KeyValuePair", 2, false, true, [GenericVariance.Invariant, GenericVariance.Invariant]),
        new("System.Collections.Generic.IEnumerable`1", "IEnumerable`1", "System.Collections.Generic", "IEnumerable",
            1, true, false, [GenericVariance.Out]),
        new("System.Collections.Generic.ICollection`1", "ICollection`1", "System.Collections.Generic", "ICollection",
            1, true, false, [GenericVariance.Invariant]),
        new("System.Collections.Generic.IList`1", "IList`1", "System.Collections.Generic", "IList", 1, true, false,
            [GenericVariance.Invariant]),
        new("System.Collections.Generic.IReadOnlyCollection`1", "IReadOnlyCollection`1", "System.Collections.Generic",
            "IReadOnlyCollection", 1, true, false, [GenericVariance.Out]),
        new("System.Collections.Generic.IReadOnlyList`1", "IReadOnlyList`1", "System.Collections.Generic",
            "IReadOnlyList", 1, true, false, [GenericVariance.Out]),
        new("System.Collections.Generic.IDictionary`2", "IDictionary`2", "System.Collections.Generic", "IDictionary",
            2, true, false, [GenericVariance.Invariant, GenericVariance.Invariant]),
        new("System.Collections.Generic.IReadOnlyDictionary`2", "IReadOnlyDictionary`2", "System.Collections.Generic",
            "IReadOnlyDictionary", 2, true, false, [GenericVariance.Invariant, GenericVariance.Invariant]),
        new("System.IEquatable`1", "IEquatable`1", "System", "IEquatable", 1, true, false,
            [GenericVariance.Invariant]),
        new("System.IComparable`1", "IComparable`1", "System", "IComparable", 1, true, false,
            [GenericVariance.In]),
        new("System.Action`1", "Action`1", "System", "Action", 1, false, false, [GenericVariance.In]),
        new("System.Action`2", "Action`2", "System", "Action", 2, false, false,
            [GenericVariance.In, GenericVariance.In]),
        new("System.Action`3", "Action`3", "System", "Action", 3, false, false,
            [GenericVariance.In, GenericVariance.In, GenericVariance.In]),
        new("System.Action`4", "Action`4", "System", "Action", 4, false, false,
            [GenericVariance.In, GenericVariance.In, GenericVariance.In, GenericVariance.In]),
        new("System.Func`1", "Func`1", "System", "Func", 1, false, false, [GenericVariance.Out]),
        new("System.Func`2", "Func`2", "System", "Func", 2, false, false,
            [GenericVariance.In, GenericVariance.Out]),
        new("System.Func`3", "Func`3", "System", "Func", 3, false, false,
            [GenericVariance.In, GenericVariance.In, GenericVariance.Out]),
        new("System.Func`4", "Func`4", "System", "Func", 4, false, false,
            [GenericVariance.In, GenericVariance.In, GenericVariance.In, GenericVariance.Out]),
        new("System.Func`5", "Func`5", "System", "Func", 5, false, false,
            [GenericVariance.In, GenericVariance.In, GenericVariance.In, GenericVariance.In,
                GenericVariance.Out]),
        new("System.Predicate`1", "Predicate`1", "System", "Predicate", 1, false, false, [GenericVariance.In]),
        new("System.Comparison`1", "Comparison`1", "System", "Comparison", 1, false, false, [GenericVariance.In]),
    ];

    /// <summary>
    /// Non-generic types resolvable by name beyond the C# keywords: the reflection root classes, the CLR names of
    /// the primitives, and the non-generic <see cref="IComparable"/> interface.
    /// </summary>
    private static readonly ImmutableArray<(string ClrName, string CSharp, bool IsInterface)> KnownNamedTypes =
    [
        ("Object", "object", false), ("String", "string", false), ("ValueType", "ValueType", false),
        ("Enum", "Enum", false), ("Array", "Array", false), ("IComparable", "IComparable", true),
        ("Action", "Action", false), ("Delegate", "Delegate", false),
        ("MulticastDelegate", "MulticastDelegate", false),
        ("SByte", "sbyte", false), ("Byte", "byte", false), ("Int16", "short", false), ("UInt16", "ushort", false),
        ("Int32", "int", false), ("UInt32", "uint", false), ("Int64", "long", false), ("UInt64", "ulong", false),
        ("Single", "float", false), ("Double", "double", false), ("Decimal", "decimal", false),
        ("Char", "char", false), ("Boolean", "bool", false),
    ];

    /// <summary>Builds the reference for one <c>System</c>-namespace type named by its CLR name.</summary>
    private static TypeRef SystemRef(string clrName, string csharpName, bool isInterface = false) => new(
        clrName, $"System.{clrName}", "System", csharpName, IsEnum: false, Shape: null,
        IsInterfaceType: isInterface);

    private static TypeRef ObjectRef => SystemRef("Object", "object");

    private static TypeRef SystemValueTypeRef => SystemRef("ValueType", "ValueType");

    private static TypeRef EnumClassRef => SystemRef("Enum", "Enum");

    private static TypeRef ArrayClassRef => SystemRef("Array", "Array");

    private static TypeRef ComparableRef => SystemRef("IComparable", "IComparable", isInterface: true);

    /// <summary>Resolves a known non-generic type by bare or <c>System.</c>-qualified name.</summary>
    private static bool TryResolveNamedTypeRef(string fullName, out TypeRef? resolved)
    {
        resolved = null;
        var bare = fullName.StartsWith("System.", StringComparison.Ordinal) ? fullName[7..] : fullName;
        if (bare.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var (clrName, csharp, isInterface) in KnownNamedTypes)
        {
            if (string.Equals(clrName, bare, StringComparison.Ordinal))
            {
                resolved = SystemRef(clrName, csharp, isInterface);
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds a modeled generic definition by its C# identifier and arity.</summary>
    private static GenericDef? FindGenericDef(string shortDisplay, int arity, string? namespaceQualifier)
    {
        foreach (var def in KnownGenericDefs)
        {
            if (string.Equals(def.ShortDisplay, shortDisplay, StringComparison.Ordinal)
                && def.Arity == arity
                && (namespaceQualifier is null
                    || string.Equals(def.Namespace, namespaceQualifier, StringComparison.Ordinal)))
            {
                return def;
            }
        }

        return null;
    }

    /// <summary>Builds the reference for a generic type definition, such as <c>typeof(List&lt;&gt;)</c>.</summary>
    private static TypeRef DefinitionRef(GenericDef def) => new(
        def.Name,
        def.FullName,
        def.Namespace,
        $"{def.ShortDisplay}<{new string(',', def.Arity - 1)}>",
        IsEnum: false,
        Shape: null,
        Definition: def,
        IsInterfaceType: def.IsInterface);

    /// <summary>
    /// Builds a constructed generic reference. The internal full name uses the runtime's <c>ToString</c> grammar —
    /// <c>Def`n[Arg,Arg]</c> — which is deterministic and doubles as the identity Equals compares.
    /// </summary>
    private static TypeRef MakeConstructed(GenericDef def, ImmutableArray<TypeRef> args)
    {
        var csharp = def.FullName == "System.Nullable`1"
            ? $"{args[0].CSharpName}?"
            : $"{def.ShortDisplay}<{string.Join(", ", args.Select(static a => a.CSharpName))}>";
        return new TypeRef(
            def.Name,
            $"{def.FullName}[{string.Join(',', args.Select(static a => a.FullName))}]",
            def.Namespace,
            csharp,
            IsEnum: false,
            Shape: null,
            Definition: def,
            TypeArguments: args,
            IsInterfaceType: def.IsInterface);
    }

    /// <summary>Whether a reference is, or contains, a constructed generic component.</summary>
    private static bool HasConstructedGenericComponent(TypeRef type) =>
        type.IsConstructedGeneric
        || (type.ElementType is { } element && HasConstructedGenericComponent(element));

    // ---- Reference conversions --------------------------------------------------------------------------------------

    /// <summary>
    /// A reference conversion over the modeled universe: an upcast — including to <c>object</c> — folds to the
    /// same value, a downcast checks the operand's exact runtime identity against the target, and an
    /// incompatible cast is the runtime's own <see cref="InvalidCastException"/>. Unboxing null is the
    /// runtime's <see cref="NullReferenceException"/>; a reference target accepts null unchanged.
    /// </summary>
    private static FoldOutcome FoldReferenceCast(TypeRef target, ExpressionSyntax expression, FoldContext context)
    {
        var operand = Fold(expression, context);
        if (operand.Disposition != FoldDisposition.Folded)
        {
            return operand;
        }

        if (operand.Operand.Kind == OperandKind.Null)
        {
            return IsValueTypeRef(target)
                ? FoldOutcome.Error(
                    "System.NullReferenceException",
                    "Object reference not set to an instance of an object.")
                : FoldOutcome.Folded(Operand.Null());
        }

        if (TryDescribeRuntimeType(operand.Operand) is not { } runtime)
        {
            return MemberUnsupported($"a cast of this value to '{target.CSharpName}'");
        }

        return IsAssignableBetween(target, runtime)
            ? operand
            : FoldOutcome.Error(
                "System.InvalidCastException",
                $"Unable to cast an object of type '{runtime.CSharpName}' to type '{target.CSharpName}'.");
    }

    // ---- Assignability ----------------------------------------------------------------------------------------------

    /// <summary>
    /// The exact <see cref="Type.IsAssignableFrom"/> relation over the modeled universe: identity, object,
    /// the base-class chain, nullable lifting, array covariance, implemented interfaces, and declared variance.
    /// </summary>
    private static bool IsAssignableBetween(TypeRef target, TypeRef source)
    {
        if (string.Equals(target.FullName, source.FullName, StringComparison.Ordinal))
        {
            return true;
        }

        if (target.FullName == "System.Object")
        {
            return true;
        }

        // Nullable<T> is assignable from its own T; nothing else converts into it.
        if (target is { Definition.FullName: "System.Nullable`1", TypeArguments: [{ } lifted] })
        {
            return string.Equals(lifted.FullName, source.FullName, StringComparison.Ordinal);
        }

        if (target.IsArray && source.IsArray)
        {
            return ArrayElementCompatible(target.ElementType!, source.ElementType!);
        }

        if (target.IsInterfaceType)
        {
            // A generic type definition's interfaces bind its own parameters, never a caller's arguments.
            if (source.IsGenericDefinition)
            {
                return false;
            }

            foreach (var candidate in InterfaceClosureOf(source))
            {
                if (InterfaceMatches(target, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var baseType in BaseChainOf(source))
        {
            if (string.Equals(baseType.FullName, target.FullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Array-covariance element compatibility: identical elements always; otherwise a reference conversion
    /// between two reference-type elements. Value-type elements never vary.
    /// </summary>
    private static bool ArrayElementCompatible(TypeRef target, TypeRef source) =>
        string.Equals(target.FullName, source.FullName, StringComparison.Ordinal)
        || (!IsValueTypeRef(target) && !IsValueTypeRef(source) && IsAssignableBetween(target, source));

    /// <summary>Whether one implemented interface satisfies the target under the definition's variance.</summary>
    private static bool InterfaceMatches(TypeRef target, TypeRef candidate)
    {
        if (target.Definition is null || candidate.Definition is null)
        {
            return string.Equals(target.FullName, candidate.FullName, StringComparison.Ordinal);
        }

        if (!string.Equals(target.Definition.FullName, candidate.Definition.FullName, StringComparison.Ordinal)
            || target.TypeArguments.Length != candidate.TypeArguments.Length)
        {
            return false;
        }

        for (var index = 0; index < target.TypeArguments.Length; index++)
        {
            var targetArg = target.TypeArguments[index];
            var sourceArg = candidate.TypeArguments[index];
            var compatible = target.Definition.Variance[index] switch
            {
                GenericVariance.Out => ArrayElementCompatible(targetArg, sourceArg),
                GenericVariance.In => ArrayElementCompatible(sourceArg, targetArg),
                _ => string.Equals(targetArg.FullName, sourceArg.FullName, StringComparison.Ordinal),
            };
            if (!compatible)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The base-class chain of a reference, nearest first; empty for interfaces and object.</summary>
    private static IEnumerable<TypeRef> BaseChainOf(TypeRef type)
    {
        if (type.IsInterfaceType || type.FullName == "System.Object")
        {
            yield break;
        }

        if (type.IsArray)
        {
            yield return ArrayClassRef;
            yield return ObjectRef;
            yield break;
        }

        if (type.IsEnum)
        {
            yield return EnumClassRef;
            yield return SystemValueTypeRef;
            yield return ObjectRef;
            yield break;
        }

        if (type.FullName == "System.Enum")
        {
            yield return SystemValueTypeRef;
            yield return ObjectRef;
            yield break;
        }

        if (IsValueTypeRef(type))
        {
            yield return SystemValueTypeRef;
            yield return ObjectRef;
            yield break;
        }

        if (IsDelegateTypeRef(type))
        {
            yield return SystemRef("MulticastDelegate", "MulticastDelegate");
            yield return SystemRef("Delegate", "Delegate");
            yield return ObjectRef;
            yield break;
        }

        if (type.FullName == "System.MulticastDelegate")
        {
            yield return SystemRef("Delegate", "Delegate");
            yield return ObjectRef;
            yield break;
        }

        // The regex value classes declare Capture as the family root: Match extends Group extends Capture.
        if (type.FullName == "System.Text.RegularExpressions.Match")
        {
            yield return NamespacedRef("Group", "System.Text.RegularExpressions");
            yield return NamespacedRef("Capture", "System.Text.RegularExpressions");
            yield return ObjectRef;
            yield break;
        }

        if (type.FullName == "System.Text.RegularExpressions.Group")
        {
            yield return NamespacedRef("Capture", "System.Text.RegularExpressions");
            yield return ObjectRef;
            yield break;
        }

        yield return ObjectRef;
    }

    /// <summary>
    /// Every generic interface the modeled type implements, plus the non-generic <see cref="IComparable"/> where
    /// the BCL declares it, closed over interface inheritance. Interface sources include themselves.
    /// </summary>
    private static IEnumerable<TypeRef> InterfaceClosureOf(TypeRef source)
    {
        var pending = new Queue<TypeRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var direct in DirectInterfacesOf(source))
        {
            pending.Enqueue(direct);
        }

        if (source.IsInterfaceType)
        {
            pending.Enqueue(source);
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!seen.Add(current.FullName))
            {
                continue;
            }

            yield return current;
            foreach (var inherited in InheritedInterfacesOf(current))
            {
                pending.Enqueue(inherited);
            }
        }
    }

    /// <summary>The interfaces one modeled type declares directly.</summary>
    private static IEnumerable<TypeRef> DirectInterfacesOf(TypeRef source)
    {
        if (source.IsArray)
        {
            var element = source.ElementType!;
            yield return MakeConstructed(FindGenericDef("IList", 1, null)!, [element]);
            yield return MakeConstructed(FindGenericDef("IReadOnlyList", 1, null)!, [element]);
            yield break;
        }

        if (source.IsEnum || source.FullName == "System.Enum")
        {
            yield return ComparableRef;
            yield break;
        }

        if (source is { IsConstructedGeneric: true, Definition: { } def })
        {
            switch (def.ShortDisplay)
            {
                case "List":
                    yield return MakeConstructed(FindGenericDef("IList", 1, null)!, source.TypeArguments);
                    yield return MakeConstructed(FindGenericDef("IReadOnlyList", 1, null)!, source.TypeArguments);
                    break;
                case "Dictionary":
                    yield return MakeConstructed(FindGenericDef("IDictionary", 2, null)!, source.TypeArguments);
                    yield return MakeConstructed(
                        FindGenericDef("IReadOnlyDictionary", 2, null)!, source.TypeArguments);
                    break;
                default:
                    break;
            }

            yield break;
        }

        var comparableSelf = source.FullName is
            "System.SByte" or "System.Byte" or "System.Int16" or "System.UInt16" or "System.Int32" or
            "System.UInt32" or "System.Int64" or "System.UInt64" or "System.Single" or "System.Double" or
            "System.Decimal" or "System.Char" or "System.Boolean" or "System.String" or "System.DateTime" or
            "System.DateTimeOffset" or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly" or
            "System.Guid" or "System.Version";
        if (comparableSelf)
        {
            yield return ComparableRef;
            yield return MakeConstructed(FindGenericDef("IComparable", 1, null)!, [source]);
            yield return MakeConstructed(FindGenericDef("IEquatable", 1, null)!, [source]);
        }

        if (source.FullName == "System.String")
        {
            yield return MakeConstructed(FindGenericDef("IEnumerable", 1, null)!, [SystemRef("Char", "char")]);
        }
    }

    /// <summary>The interfaces one modeled interface inherits directly.</summary>
    private static IEnumerable<TypeRef> InheritedInterfacesOf(TypeRef current)
    {
        if (current is not { IsConstructedGeneric: true, Definition: { } def })
        {
            yield break;
        }

        var args = current.TypeArguments;
        switch (def.ShortDisplay)
        {
            case "IList":
                yield return MakeConstructed(FindGenericDef("ICollection", 1, null)!, args);
                break;
            case "ICollection":
                yield return MakeConstructed(FindGenericDef("IEnumerable", 1, null)!, args);
                break;
            case "IReadOnlyList":
                yield return MakeConstructed(FindGenericDef("IReadOnlyCollection", 1, null)!, args);
                break;
            case "IReadOnlyCollection":
                yield return MakeConstructed(FindGenericDef("IEnumerable", 1, null)!, args);
                break;
            case "IDictionary":
                yield return MakeConstructed(
                    FindGenericDef("ICollection", 1, null)!,
                    [MakeConstructed(FindGenericDef("KeyValuePair", 2, null)!, args)]);
                break;
            case "IReadOnlyDictionary":
                yield return MakeConstructed(
                    FindGenericDef("IReadOnlyCollection", 1, null)!,
                    [MakeConstructed(FindGenericDef("KeyValuePair", 2, null)!, args)]);
                break;
            default:
                break;
        }
    }

    /// <summary>The <see cref="Type.BaseType"/> of a modeled reference, or null for object and interfaces.</summary>
    private static TypeRef? BaseTypeOf(TypeRef type)
    {
        foreach (var baseType in BaseChainOf(type))
        {
            return baseType;
        }

        return null;
    }

    // ---- Runtime types of evaluated operands ------------------------------------------------------------------------

    /// <summary>
    /// Describes the exact runtime type of an evaluated operand, for <see cref="Type.IsInstanceOfType"/>. Null
    /// operands have no runtime type; kinds whose element identity the payload does not carry resolve to null.
    /// </summary>
    private static TypeRef? TryDescribeRuntimeType(Operand operand)
    {
        switch (operand.Kind)
        {
            case OperandKind.Int32:
                return SystemRef("Int32", "int");
            case OperandKind.Boolean:
                return SystemRef("Boolean", "bool");
            case OperandKind.Char:
                return SystemRef("Char", "char");
            case OperandKind.String:
                return SystemRef("String", "string");
            case OperandKind.Numeric:
                var numericName = NumericKindOf(operand).ToString();
                return SystemRef(numericName, CSharpNameOfNumeric(NumericKindOf(operand)));
            case OperandKind.Temporal:
                var temporalName = operand.TemporalKind.ToString();
                return SystemRef(temporalName, temporalName);
            case OperandKind.Delegate:
                return DelegatePayloadOf(operand).Type;
            case OperandKind.BclValue:
                var valueName = operand.BclValueKind.ToString();
                var valueNamespace = BclValueNamespaceOf(operand.BclValueKind);
                return valueNamespace == "System"
                    ? SystemRef(valueName, valueName)
                    : new TypeRef(
                        valueName,
                        $"{valueNamespace}.{valueName}",
                        valueNamespace,
                        valueName,
                        IsEnum: false,
                        Shape: null);
            case OperandKind.Enum when operand.EnumTypeFullName is { } enumFullName:
                var separator = enumFullName.LastIndexOf('.');
                var shortName = separator < 0 ? enumFullName : enumFullName[(separator + 1)..];
                return new TypeRef(
                    shortName,
                    enumFullName,
                    separator < 0 ? null : enumFullName[..separator],
                    shortName,
                    IsEnum: true,
                    Shape: null);
            case OperandKind.Sequence:
                var payload = PayloadOf(operand);
                var element = payload.ElementKind switch
                {
                    OperandKind.String => SystemRef("String", "string"),
                    OperandKind.Char => SystemRef("Char", "char"),
                    OperandKind.Boolean => SystemRef("Boolean", "bool"),
                    OperandKind.Int32 or OperandKind.Numeric => SystemRef(
                        payload.ElementNumeric.ToString(), CSharpNameOfNumeric(payload.ElementNumeric)),
                    _ => payload.Items.Length > 0 ? TryDescribeRuntimeType(payload.Items[0]) : null,
                };
                return element?.MakeArray();
            default:
                return null;
        }
    }

    // ---- Generic construction dispatch ------------------------------------------------------------------------------

    /// <summary>Implements <see cref="Type.MakeGenericType"/> with the runtime's exact failure semantics.</summary>
    private static FoldOutcome MakeGenericTypeOf(TypeRef type, List<Operand> arguments)
    {
        if (!type.IsGenericDefinition)
        {
            return FoldOutcome.Error(
                "System.InvalidOperationException",
                $"'{type.FullName}' is not a generic type definition; MakeGenericType is valid only on one.");
        }

        var args = ImmutableArray.CreateBuilder<TypeRef>(arguments.Count);
        foreach (var argument in arguments)
        {
            if (argument.Kind != OperandKind.Type)
            {
                return FoldOutcome.Error(OperandTypeCode, "MakeGenericType takes typeof(...) arguments.");
            }

            var argType = (TypeRef)argument.Box!;
            if (argType.IsGenericDefinition)
            {
                return MemberUnsupported("MakeGenericType with an open generic definition as an argument");
            }

            args.Add(argType);
        }

        if (args.Count != type.Definition!.Arity)
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                "The number of generic arguments provided doesn't equal the arity of the generic type definition.");
        }

        if (type.Definition.FullName == "System.Nullable`1"
            && (!IsValueTypeRef(args[0]) || args[0].Definition?.FullName == "System.Nullable`1"))
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"GenericArguments[0], '{args[0].FullName}', on 'System.Nullable`1[T]' violates the constraint "
                + "of type 'T': it must be a non-nullable value type.");
        }

        return FoldOutcome.Folded(Operand.FromType(MakeConstructed(type.Definition, args.MoveToImmutable())));
    }

    /// <summary>Builds the Type sequence for <c>GenericTypeArguments</c> and <c>GetGenericArguments</c>.</summary>
    private static FoldOutcome TypeArgumentsSequence(TypeRef type) => CreateSequence(new SequencePayload(
        type.TypeArguments.IsDefaultOrEmpty
            ? []
            : [.. type.TypeArguments.Select(Operand.FromType)],
        OperandKind.Type,
        default,
        "Type"));
}
