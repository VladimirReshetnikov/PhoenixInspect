using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Virtually created delegates: <c>new Action(…)</c>, delegate-type casts over expression lambdas and method
/// groups, <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/>, invocation through <c>…(args)</c> and
/// <c>.Invoke</c>, the multicast algebra (<c>+</c>, <c>-</c>, equality, <c>Combine</c>/<c>Remove</c>), and the
/// <see cref="Delegate"/>/<see cref="MulticastDelegate"/> member surface. A virtual delegate is an invocation
/// list whose entries are either an expression lambda — folded per call under its captured bindings — or a
/// public method of the modeled reflection universe; invoking either routes through the evaluator's own fold
/// dispatch, so a delegate can never compute anything the direct spelling could not.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    /// <summary>How one invocation-list entry computes.</summary>
    private enum DelegateEntryKind
    {
        /// <summary>An expression lambda folded per invocation.</summary>
        Lambda,

        /// <summary>A public method of the modeled reflection universe, with an optional bound target.</summary>
        Method,
    }

    /// <summary>One invocation-list entry. Entry identity is reference identity, exactly like the runtime's.</summary>
    private sealed record DelegateEntry(
        DelegateEntryKind Kind,
        LambdaShape Lambda,
        ImmutableArray<(string Name, Operand Value)> CapturedBindings,
        string DisplayText,
        MethodInfo? Method,
        Operand? Target);

    /// <summary>One virtual delegate: its exact delegate type and its invocation list, in call order.</summary>
    private sealed record DelegatePayload(TypeRef Type, ImmutableArray<DelegateEntry> Invocations);

    private static DelegatePayload DelegatePayloadOf(Operand operand) => (DelegatePayload)operand.Box!;

    // ---- Delegate types ---------------------------------------------------------------------------------------------

    /// <summary>The call shape one delegate type declares: how many parameters, and whether a value returns.</summary>
    private static (int ParameterCount, bool ReturnsValue)? DelegateSignatureOf(TypeRef type)
    {
        if (type.FullName == "System.Action")
        {
            return (0, false);
        }

        if (type.Definition is not { } definition || !type.IsConstructedGeneric)
        {
            return null;
        }

        return definition.FullName switch
        {
            "System.Action`1" or "System.Action`2" or "System.Action`3" or "System.Action`4" =>
                (definition.Arity, false),
            "System.Func`1" or "System.Func`2" or "System.Func`3" or "System.Func`4" or "System.Func`5" =>
                (definition.Arity - 1, true),
            "System.Predicate`1" => (1, true),
            "System.Comparison`1" => (2, true),
            _ => null,
        };
    }

    private static bool IsDelegateTypeRef(TypeRef type) => DelegateSignatureOf(type) is not null;

    /// <summary>
    /// The exact parameter and return types one delegate type binds methods against, or null for the
    /// non-generic <c>Action</c>, whose empty signature the parameter-count check already pins.
    /// </summary>
    private static (ImmutableArray<TypeRef> ParameterTypes, string ReturnType)? DelegateBindingTypesOf(
        TypeRef type)
    {
        if (type.Definition is not { } definition || !type.IsConstructedGeneric)
        {
            return type.FullName == "System.Action" ? ([], "System.Void") : null;
        }

        var arguments = type.TypeArguments;
        return definition.FullName switch
        {
            "System.Action`1" or "System.Action`2" or "System.Action`3" or "System.Action`4" =>
                (arguments, "System.Void"),
            "System.Func`1" or "System.Func`2" or "System.Func`3" or "System.Func`4" or "System.Func`5" =>
                (arguments[..^1], arguments[^1].FullName),
            "System.Predicate`1" => (arguments, "System.Boolean"),
            "System.Comparison`1" => ((ImmutableArray<TypeRef>)[arguments[0], arguments[0]], "System.Int32"),
            _ => null,
        };
    }

    /// <summary>
    /// Resolves a delegate type spelling — <c>Action</c>, <c>Action&lt;…&gt;</c>, <c>Func&lt;…&gt;</c>,
    /// <c>Predicate&lt;T&gt;</c>, <c>Comparison&lt;T&gt;</c>, optionally <c>System</c>-qualified — without
    /// touching enum resolution, so creation and cast sites can probe cheaply.
    /// </summary>
    private static bool TryResolveDelegateTypeRef(TypeSyntax type, FoldContext context, out TypeRef? resolved)
    {
        resolved = null;
        var unqualified = type is QualifiedNameSyntax
        {
            Left: IdentifierNameSyntax { Identifier.ValueText: "System" },
            Right: { } systemQualified,
        }
            ? systemQualified
            : type;
        switch (unqualified)
        {
            case IdentifierNameSyntax { Identifier.ValueText: "Action" }:
                resolved = SystemRef("Action", "Action");
                return true;
            case GenericNameSyntax generic when generic.Identifier.ValueText is
                "Action" or "Func" or "Predicate" or "Comparison":
                return TryResolveTypeRef(type, context, out resolved, out _) &&
                    resolved is not null && IsDelegateTypeRef(resolved);
            default:
                return false;
        }
    }

    // ---- Creation and conversion ------------------------------------------------------------------------------------

    /// <summary>
    /// Converts one source expression to a delegate of the given type: an expression lambda with the declared
    /// parameter count, a method group over the modeled universe, or an existing delegate value re-typed.
    /// </summary>
    private static FoldOutcome ConvertToDelegate(TypeRef type, ExpressionSyntax source, FoldContext context)
    {
        var signature = DelegateSignatureOf(type)!.Value;
        var expression = source;
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is LambdaExpressionSyntax lambdaSyntax)
        {
            return CreateLambdaDelegate(type, signature.ParameterCount, lambdaSyntax, context);
        }

        // A member access that names a method of the modeled universe is a method group; every other member
        // access is a value whose fold below either yields a delegate to re-type or a typed refusal.
        if (expression is MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: { } groupReceiver,
                Name: IdentifierNameSyntax groupName,
            } &&
            TryConvertMethodGroup(
                type,
                signature.ParameterCount,
                groupReceiver,
                groupName.Identifier.ValueText,
                context,
                out var groupOutcome))
        {
            return groupOutcome;
        }

        var folded = Fold(expression, context);
        if (folded.Disposition != FoldDisposition.Folded)
        {
            return folded;
        }

        // A delegate is a reference type: null converts to every delegate type, as '(Action)null' spells.
        if (folded.Operand.Kind == OperandKind.Null)
        {
            return folded;
        }

        if (folded.Operand.Kind == OperandKind.Delegate)
        {
            var payload = DelegatePayloadOf(folded.Operand);
            var sourceSignature = DelegateSignatureOf(payload.Type)!.Value;
            return sourceSignature.ParameterCount == signature.ParameterCount
                ? FoldOutcome.Folded(Operand.FromDelegate(payload with { Type = type }))
                : FoldOutcome.Error(
                    OperandTypeCode,
                    $"'{payload.Type.CSharpName}' does not convert to '{type.CSharpName}': the parameter "
                    + "counts differ.");
        }

        if (folded.Operand.Kind == OperandKind.BclValue &&
            folded.Operand.BclValueKind == BclValueKind.MethodInfo)
        {
            return CreateDelegateFromMethod(type, (MethodInfo)folded.Operand.Box!, target: null);
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            $"'{type.CSharpName}' converts from an expression lambda, a method group, a MethodInfo, or "
            + "another delegate.");
    }

    /// <summary>Reads one expression lambda into an entry, capturing the bindings its body closes over.</summary>
    private static FoldOutcome CreateLambdaDelegate(
        TypeRef type,
        int parameterCount,
        LambdaExpressionSyntax lambdaSyntax,
        FoldContext context)
    {
        ParameterSyntax[] parameters;
        ExpressionSyntax? body;
        switch (lambdaSyntax)
        {
            case SimpleLambdaExpressionSyntax simple:
                parameters = [simple.Parameter];
                body = simple.ExpressionBody;
                break;
            case ParenthesizedLambdaExpressionSyntax parenthesized:
                parameters = [.. parenthesized.ParameterList.Parameters];
                body = parenthesized.ExpressionBody;
                break;
            default:
                return FoldOutcome.Error(LambdaUnsupportedCode, "Only expression lambdas convert to delegates.");
        }

        if (lambdaSyntax.Modifiers.Count > 0)
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                "Lambda modifiers such as 'static' or 'async' are not modeled; write a plain expression lambda.");
        }

        if (body is null)
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                "Only expression-bodied lambdas are supported; a block body would need statement execution "
                + "this evaluator deliberately does not model.");
        }

        if (parameters.Length != parameterCount)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{type.CSharpName}' takes {parameterCount.ToString(CultureInfo.InvariantCulture)} "
                + $"parameter(s); the lambda declares {parameters.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        var names = ImmutableArray.CreateBuilder<string>(parameters.Length);
        foreach (var parameter in parameters)
        {
            var parameterName = parameter.Identifier.ValueText;
            if (names.Contains(parameterName))
            {
                return FoldOutcome.Error(LambdaUnsupportedCode, "Lambda parameter names must be distinct.");
            }

            names.Add(parameterName);
        }

        // C# closures capture the environment at creation; a virtual lambda snapshots the lambda-parameter
        // bindings its body reads, so a delegate built inside an outer lambda keeps the outer value.
        var captured = ImmutableArray.CreateBuilder<(string Name, Operand Value)>();
        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var free = identifier.Identifier.ValueText;
            if (!names.Contains(free) &&
                !captured.Any(binding => string.Equals(binding.Name, free, StringComparison.Ordinal)) &&
                context.TryResolveBinding(free, out var bound))
            {
                captured.Add((free, bound));
            }
        }

        var shape = new LambdaShape(names.MoveToImmutable(), body);
        return FoldOutcome.Folded(Operand.FromDelegate(new DelegatePayload(
            type,
            [
                new DelegateEntry(
                    DelegateEntryKind.Lambda,
                    shape,
                    captured.ToImmutable(),
                    lambdaSyntax.ToString(),
                    Method: null,
                    Target: null),
            ])));
    }

    /// <summary>
    /// Converts one method group. A type-receiver group binds a public static method; a value-receiver group
    /// binds a public instance method with the folded receiver as the delegate's target. Overloads are matched
    /// by the delegate's parameter count; a genuinely ambiguous name is the BCL's ambiguity refusal, naming the
    /// CreateDelegate path that can disambiguate by exact signature.
    /// </summary>
    private static bool TryConvertMethodGroup(
        TypeRef type,
        int parameterCount,
        ExpressionSyntax receiverExpression,
        string methodName,
        FoldContext context,
        out FoldOutcome outcome)
    {
        outcome = default;
        Type? runtime = null;
        Operand? target = null;
        var isStatic = false;
        if (TryReadTypeReceiver(receiverExpression, out var typeReceiver))
        {
            runtime = RuntimeTypeOfReceiver(typeReceiver);
            isStatic = true;
        }
        else
        {
            var receiver = Fold(receiverExpression, context);
            if (receiver.Disposition != FoldDisposition.Folded)
            {
                return false;
            }

            if (TryDescribeRuntimeType(receiver.Operand) is { } described)
            {
                runtime = RuntimeTypeOfRef(described);
                target = receiver.Operand;
            }
        }

        if (runtime is null)
        {
            return false;
        }

        var flags = isStatic
            ? BindingFlags.Public | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.Instance;
        var named = runtime.GetMethods(flags)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToList();
        if (named.Count == 0)
        {
            // Not a method group at all; the caller folds the expression as a value instead.
            return false;
        }

        // Overload selection matches the delegate's exact signature, as C#'s method-group conversion does.
        var binding = DelegateBindingTypesOf(type);
        var candidates = named
            .Where(method => method.GetParameters().Length == parameterCount &&
                (binding is not { } required || MethodMatchesDelegate(method, required)))
            .OrderBy(SignatureOf, StringComparer.Ordinal)
            .Take(2)
            .ToList();
        switch (candidates.Count)
        {
            case 0:
                outcome = FoldOutcome.Error(
                    "System.ArgumentException",
                    $"No overload of '{methodName}' matches the signature of '{type.CSharpName}'.");
                return true;
            case 1:
                outcome = CreateDelegateFromMethod(type, candidates[0], target);
                return true;
            default:
                outcome = FoldOutcome.Error(
                    "System.Reflection.AmbiguousMatchException",
                    $"The method group '{methodName}' has more than one overload matching '{type.CSharpName}'; "
                    + "select the exact MethodInfo with GetMethods() and convert it with CreateDelegate.");
                return true;
        }
    }

    /// <summary>Whether one method's parameter and return types equal the delegate's, by canonical name.</summary>
    private static bool MethodMatchesDelegate(
        MethodInfo method,
        (ImmutableArray<TypeRef> ParameterTypes, string ReturnType) binding)
    {
        var parameters = method.GetParameters();
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!string.Equals(
                CanonicalRuntimeName(parameters[index].ParameterType),
                binding.ParameterTypes[index].FullName,
                StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(CanonicalRuntimeName(method.ReturnType), binding.ReturnType, StringComparison.Ordinal);
    }

    /// <summary>The runtime type behind one static-dispatch receiver, for method-group binding.</summary>
    private static Type? RuntimeTypeOfReceiver(TypeReceiver receiver) => receiver.Category switch
    {
        TypeReceiverCategory.String => typeof(string),
        TypeReceiverCategory.Char => typeof(char),
        TypeReceiverCategory.Math => typeof(Math),
        TypeReceiverCategory.Enumerable => typeof(Enumerable),
        TypeReceiverCategory.SystemEnum => typeof(Enum),
        TypeReceiverCategory.SystemArray => typeof(Array),
        TypeReceiverCategory.Temporal => receiver.Temporal switch
        {
            TemporalKind.DateTime => typeof(DateTime),
            TemporalKind.DateTimeOffset => typeof(DateTimeOffset),
            TemporalKind.TimeSpan => typeof(TimeSpan),
            TemporalKind.DateOnly => typeof(DateOnly),
            _ => typeof(TimeOnly),
        },
        TypeReceiverCategory.BclValue => receiver.Value switch
        {
            BclValueKind.Guid => typeof(Guid),
            BclValueKind.Version => typeof(Version),
            BclValueKind.Encoding => typeof(System.Text.Encoding),
            BclValueKind.Regex => typeof(System.Text.RegularExpressions.Regex),
            _ => null,
        },
        TypeReceiverCategory.Numeric => RuntimeTypeOfRef(SystemRef(
            receiver.Numeric.ToString(),
            receiver.Numeric.ToString())),
        _ => null,
    };

    /// <summary>Builds a single-entry delegate over one public method of the modeled universe.</summary>
    private static FoldOutcome CreateDelegateFromMethod(TypeRef type, MethodInfo method, Operand? target)
    {
        if (!IsDelegateTypeRef(type))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{type.CSharpName}' is not a delegate type the evaluator models.");
        }

        var signature = DelegateSignatureOf(type)!.Value;
        if (method.GetParameters().Length != signature.ParameterCount)
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{method}' takes {method.GetParameters().Length.ToString(CultureInfo.InvariantCulture)} "
                + $"parameter(s); '{type.CSharpName}' declares "
                + $"{signature.ParameterCount.ToString(CultureInfo.InvariantCulture)}.");
        }

        // The method's signature must match the delegate's exactly, as the runtime's binder requires; the
        // check compares the deterministic canonical names, so an int-parameter Func never binds Math.Sqrt.
        if (DelegateBindingTypesOf(type) is { } binding && !MethodMatchesDelegate(method, binding))
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{method}' does not match the signature of '{type.CSharpName}'.");
        }

        if (!method.IsStatic && target is null)
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{method.Name}' is an instance method; bind it through a receiver value or pass a target "
                + "to CreateDelegate.");
        }

        if (method.IsStatic && target is not null)
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{method.Name}' is static; closed static delegates are not modeled, so pass no target.");
        }

        return FoldOutcome.Folded(Operand.FromDelegate(new DelegatePayload(
            type,
            [
                new DelegateEntry(
                    DelegateEntryKind.Method,
                    default,
                    [],
                    $"{method.DeclaringType?.Name}.{method.Name}",
                    method,
                    target),
            ])));
    }

    // ---- Invocation -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Invokes one delegate with multicast semantics: every entry runs in list order, and the last entry's
    /// value is the answer — exactly null for the void delegate shapes.
    /// </summary>
    private static FoldOutcome InvokeDelegate(DelegatePayload payload, List<Operand> arguments, FoldContext context)
    {
        var signature = DelegateSignatureOf(payload.Type)!.Value;
        if (arguments.Count != signature.ParameterCount)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{payload.Type.CSharpName}' takes "
                + $"{signature.ParameterCount.ToString(CultureInfo.InvariantCulture)} argument(s); "
                + $"{arguments.Count.ToString(CultureInfo.InvariantCulture)} were supplied.");
        }

        var last = FoldOutcome.Folded(Operand.Null());
        var returnDelegateType = DelegateReturnDelegateTypeOf(payload.Type);
        foreach (var entry in payload.Invocations)
        {
            last = InvokeDelegateEntry(entry, arguments, returnDelegateType, context);
            if (last.Disposition != FoldDisposition.Folded)
            {
                return last;
            }
        }

        return signature.ReturnsValue ? last : FoldOutcome.Folded(Operand.Null());
    }

    /// <summary>The delegate's return type when that type is itself a delegate — the curried case — or null.</summary>
    private static TypeRef? DelegateReturnDelegateTypeOf(TypeRef type) =>
        type is { IsConstructedGeneric: true, Definition.FullName: var definitionName } &&
        definitionName is "System.Func`1" or "System.Func`2" or "System.Func`3" or "System.Func`4"
            or "System.Func`5" &&
        type.TypeArguments[^1] is { } returnType &&
        IsDelegateTypeRef(returnType)
            ? returnType
            : null;

    private static FoldOutcome InvokeDelegateEntry(
        DelegateEntry entry,
        List<Operand> arguments,
        TypeRef? returnDelegateType,
        FoldContext context)
    {
        if (entry.Kind == DelegateEntryKind.Lambda)
        {
            var pushed = 0;
            try
            {
                foreach (var (name, value) in entry.CapturedBindings)
                {
                    context.PushBinding(name, value);
                    pushed++;
                }

                for (var index = 0; index < entry.Lambda.Parameters.Length; index++)
                {
                    context.PushBinding(entry.Lambda.Parameters[index], arguments[index]);
                    pushed++;
                }

                // A curried lambda's body is itself a conversion target: 'x => y => x + y' returns a delegate
                // whose type the outer delegate declares, and converting under the pushed bindings captures
                // the outer parameters exactly as C# closures do.
                return returnDelegateType is not null
                    ? ConvertToDelegate(returnDelegateType, entry.Lambda.Body, context)
                    : Fold(entry.Lambda.Body, context);
            }
            finally
            {
                context.PopBindings(pushed);
            }
        }

        var method = entry.Method!;
        if (entry.Target is { } target)
        {
            return DispatchOperandInvocation(target, method.Name, arguments);
        }

        return method.DeclaringType is { } declaring &&
            TypeRefOfRuntime(declaring) is { } declaringRef &&
            TryTypeReceiverOfRef(declaringRef, out var typeReceiver)
            ? DispatchTypeReceiverInvocation(typeReceiver, method.Name, default, arguments, context)
            : MemberUnsupported($"{method.DeclaringType?.Name}.{method.Name} via a delegate");
    }

    /// <summary>
    /// Invokes a delegate-valued expression: the invocation target folds first, and only a delegate value
    /// proceeds, so every other non-member invocation shape keeps its existing not-constant path.
    /// </summary>
    private static FoldOutcome FoldDelegateValueInvocation(
        InvocationExpressionSyntax invocation,
        FoldContext context)
    {
        var target = Fold(invocation.Expression, context);
        if (target.Disposition == FoldDisposition.Error)
        {
            return target;
        }

        if (target.Disposition != FoldDisposition.Folded || target.Operand.Kind != OperandKind.Delegate)
        {
            return FoldOutcome.NotArithmetic();
        }

        var payload = DelegatePayloadOf(target.Operand);
        var parameterTypes = DelegateBindingTypesOf(payload.Type)?.ParameterTypes ?? default;
        var arguments = new List<Operand>(invocation.ArgumentList.Arguments.Count);
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon is not null || argument.RefKindKeyword != default)
            {
                return FoldOutcome.NotArithmetic();
            }

            // A lambda argument converts against the delegate's declared parameter type, so a higher-order
            // delegate accepts 'apply(x => x + 1)' exactly as C# target-typing does.
            var expression = argument.Expression;
            while (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
            }

            var folded = expression is LambdaExpressionSyntax &&
                !parameterTypes.IsDefaultOrEmpty &&
                arguments.Count < parameterTypes.Length &&
                IsDelegateTypeRef(parameterTypes[arguments.Count])
                ? ConvertToDelegate(parameterTypes[arguments.Count], expression, context)
                : Fold(argument.Expression, context);
            if (folded.Disposition != FoldDisposition.Folded)
            {
                return folded;
            }

            arguments.Add(folded.Operand);
        }

        return InvokeDelegate(payload, arguments, context);
    }

    /// <summary>The instance surface of a delegate value.</summary>
    private static FoldOutcome DispatchDelegateMethod(
        Operand receiver,
        string name,
        List<Operand> arguments,
        FoldContext context)
    {
        var payload = DelegatePayloadOf(receiver);
        switch (name, arguments)
        {
            case ("Invoke", _):
                return InvokeDelegate(payload, arguments, context);
            case ("DynamicInvoke", [{ Kind: OperandKind.Null }]):
                return InvokeDelegate(payload, [], context);
            case ("DynamicInvoke", [{ Kind: OperandKind.Sequence } packed]):
                return InvokeDelegate(payload, [.. PayloadOf(packed).Items], context);
            case ("DynamicInvoke", _):
                return InvokeDelegate(payload, arguments, context);
            case ("GetInvocationList", []):
                return CreateSequence(new SequencePayload(
                    [
                        .. payload.Invocations.Select(entry =>
                            Operand.FromDelegate(new DelegatePayload(payload.Type, [entry]))),
                    ],
                    OperandKind.Delegate,
                    default,
                    payload.Type.CSharpName));
            case ("Equals", [{ } other]):
                return FoldOutcome.Folded(Operand.FromBoolean(
                    other.Kind == OperandKind.Delegate && DelegatesEqual(payload, DelegatePayloadOf(other))));
            case ("GetType", []):
                return FoldOutcome.Folded(Operand.FromType(payload.Type));
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(payload.Type.FullName));
            default:
                return MemberUnsupported($"{payload.Type.CSharpName}.{name}");
        }
    }

    /// <summary>The read-only properties of a delegate value.</summary>
    /// <remarks>
    /// <c>Method</c> and <c>Target</c> answer for the last entry, exactly as <see cref="MulticastDelegate"/>
    /// defines them; a lambda entry has no runtime method identity, so <c>Method</c> over one is a typed stop.
    /// </remarks>
    private static FoldOutcome DispatchDelegateProperty(Operand receiver, string member)
    {
        var payload = DelegatePayloadOf(receiver);
        var lastEntry = payload.Invocations[^1];
        return member switch
        {
            "Method" => lastEntry.Kind == DelegateEntryKind.Method
                ? FoldOutcome.Folded(Operand.FromBclValue(BclValueKind.MethodInfo, lastEntry.Method!))
                : FoldOutcome.Error(
                    MemberUnsupportedCode,
                    "A virtual lambda delegate has no runtime MethodInfo; only method-group delegates carry one."),
            "Target" => lastEntry.Target is { } target
                ? FoldOutcome.Folded(target)
                : FoldOutcome.Folded(Operand.Null()),
            "HasSingleTarget" => FoldOutcome.Folded(Operand.FromBoolean(payload.Invocations.Length == 1)),
            _ => MemberUnsupported($"{payload.Type.CSharpName}.{member}"),
        };
    }

    // ---- The multicast algebra --------------------------------------------------------------------------------------

    /// <summary>Combination, removal, and equality over delegate operands, with the BCL's own semantics.</summary>
    private static FoldOutcome ComputeDelegateBinary(SyntaxKind kind, Operand left, Operand right)
    {
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            if (left.Kind != OperandKind.Delegate || right.Kind != OperandKind.Delegate)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "Delegate equality compares two delegates, or a delegate against null.");
            }

            var equal = DelegatesEqual(DelegatePayloadOf(left), DelegatePayloadOf(right));
            return FoldOutcome.Folded(Operand.FromBoolean(kind == SyntaxKind.EqualsExpression ? equal : !equal));
        }

        if (kind is not (SyntaxKind.AddExpression or SyntaxKind.SubtractExpression))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Delegates define combination (+), removal (-), and equality; no other operator applies.");
        }

        if (left.Kind != OperandKind.Delegate || right.Kind != OperandKind.Delegate)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Delegate combination and removal require two delegates of the same type.");
        }

        var leftPayload = DelegatePayloadOf(left);
        var rightPayload = DelegatePayloadOf(right);
        if (!string.Equals(leftPayload.Type.FullName, rightPayload.Type.FullName, StringComparison.Ordinal))
        {
            return FoldOutcome.Error(
                "System.ArgumentException",
                $"'{leftPayload.Type.CSharpName}' and '{rightPayload.Type.CSharpName}' are different delegate "
                + "types and cannot combine.");
        }

        if (kind == SyntaxKind.AddExpression)
        {
            return FoldOutcome.Folded(Operand.FromDelegate(leftPayload with
            {
                Invocations = leftPayload.Invocations.AddRange(rightPayload.Invocations),
            }));
        }

        return RemoveDelegate(leftPayload, rightPayload, removeAll: false);
    }

    /// <summary>
    /// <see cref="Delegate.Remove(Delegate, Delegate)"/> semantics: the last occurrence of the value's whole
    /// invocation list is removed as a contiguous sublist; no occurrence leaves the source unchanged, and an
    /// emptied list is exactly null.
    /// </summary>
    private static FoldOutcome RemoveDelegate(DelegatePayload source, DelegatePayload value, bool removeAll)
    {
        var invocations = source.Invocations;
        while (true)
        {
            var start = -1;
            for (var index = invocations.Length - value.Invocations.Length; index >= 0; index--)
            {
                var matches = true;
                for (var offset = 0; offset < value.Invocations.Length; offset++)
                {
                    if (!EntriesEqual(invocations[index + offset], value.Invocations[offset]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    start = index;
                    break;
                }
            }

            if (start < 0)
            {
                break;
            }

            invocations = invocations.RemoveRange(start, value.Invocations.Length);
            if (!removeAll || invocations.Length == 0)
            {
                break;
            }
        }

        return invocations.Length == 0
            ? FoldOutcome.Folded(Operand.Null())
            : FoldOutcome.Folded(Operand.FromDelegate(source with { Invocations = invocations }));
    }

    private static bool DelegatesEqual(DelegatePayload left, DelegatePayload right)
    {
        if (!string.Equals(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal) ||
            left.Invocations.Length != right.Invocations.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Invocations.Length; index++)
        {
            if (!EntriesEqual(left.Invocations[index], right.Invocations[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Entry equality: a lambda entry equals only itself — two identical lambda texts are distinct creations,
    /// as at run time — while method entries compare method identity and bound target value.
    /// </summary>
    private static bool EntriesEqual(DelegateEntry left, DelegateEntry right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Kind != DelegateEntryKind.Method || right.Kind != DelegateEntryKind.Method ||
            left.Method != right.Method)
        {
            return false;
        }

        return (left.Target, right.Target) switch
        {
            (null, null) => true,
            ({ } leftTarget, { } rightTarget) =>
                OperandsEqual(leftTarget, rightTarget) is
                {
                    Disposition: FoldDisposition.Folded,
                    Operand.Boolean: true,
                },
            _ => false,
        };
    }

    // ---- The System.Delegate static surface -------------------------------------------------------------------------

    /// <summary>
    /// <see cref="Delegate"/>'s statics: <c>Combine</c>, <c>Remove</c>, <c>RemoveAll</c>, and the
    /// <c>CreateDelegate</c> family over Type references and MethodInfo values.
    /// </summary>
    private static FoldOutcome DispatchDelegateStatic(string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("Combine", [{ Kind: OperandKind.Null }, { Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.Null());
            case ("Combine", [{ Kind: OperandKind.Null }, { Kind: OperandKind.Delegate } rightOnly]):
                return FoldOutcome.Folded(rightOnly);
            case ("Combine", [{ Kind: OperandKind.Delegate } leftOnly, { Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(leftOnly);
            case ("Combine", [{ Kind: OperandKind.Delegate } first, { Kind: OperandKind.Delegate } second]):
                return ComputeDelegateBinary(SyntaxKind.AddExpression, first, second);
            case ("Remove" or "RemoveAll", [{ Kind: OperandKind.Null }, _]):
                return FoldOutcome.Folded(Operand.Null());
            case ("Remove" or "RemoveAll", [{ Kind: OperandKind.Delegate } source, { Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(source);
            case ("Remove", [{ Kind: OperandKind.Delegate } source, { Kind: OperandKind.Delegate } value]):
                return ComputeDelegateBinary(SyntaxKind.SubtractExpression, source, value);
            case ("RemoveAll", [{ Kind: OperandKind.Delegate } source, { Kind: OperandKind.Delegate } value]):
                return string.Equals(
                    DelegatePayloadOf(source).Type.FullName,
                    DelegatePayloadOf(value).Type.FullName,
                    StringComparison.Ordinal)
                    ? RemoveDelegate(DelegatePayloadOf(source), DelegatePayloadOf(value), removeAll: true)
                    : FoldOutcome.Error(
                        "System.ArgumentException",
                        "RemoveAll requires two delegates of the same type.");
            case ("CreateDelegate", [
                { Kind: OperandKind.Type } type,
                { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.MethodInfo } method]):
                return CreateDelegateFromMethod((TypeRef)type.Box!, (MethodInfo)method.Box!, target: null);
            case ("CreateDelegate", [
                { Kind: OperandKind.Type } type,
                { Kind: OperandKind.Null },
                { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.MethodInfo } method]):
                return CreateDelegateFromMethod((TypeRef)type.Box!, (MethodInfo)method.Box!, target: null);
            case ("CreateDelegate", [
                { Kind: OperandKind.Type } type,
                { } target,
                { Kind: OperandKind.BclValue, BclValueKind: BclValueKind.MethodInfo } method]):
                return CreateDelegateFromMethod((TypeRef)type.Box!, (MethodInfo)method.Box!, target);
            default:
                return MemberUnsupported($"Delegate.{name}");
        }
    }

    // ---- Rendering --------------------------------------------------------------------------------------------------

    /// <summary>Renders a delegate as its entry texts, joined for a multicast list.</summary>
    private static string RenderDelegate(DelegatePayload payload) => payload.Invocations.Length == 1
        ? payload.Invocations[0].DisplayText
        : string.Join(" + ", payload.Invocations.Select(static entry => entry.DisplayText));
}
