using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The expression-lambda sequence surface: <c>Select</c>, <c>Where</c>, predicated element operators, selector
/// aggregates, and key-ordered sorts over virtual sequences. A lambda body is folded once per element with its
/// parameter bound to that element's exact value, so the surface stays deterministic and purely functional: the
/// lambda never captures mutable state, never runs target code, and never widens what evidence is read.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    private const string LambdaUnsupportedCode = "CONSTANT_LAMBDA_UNSUPPORTED";

    /// <summary>One admitted expression lambda: its parameter names and its body expression.</summary>
    /// <param name="Parameters">The declared parameter names, in order.</param>
    /// <param name="Body">The expression body folded per element.</param>
    private readonly record struct LambdaShape(ImmutableArray<string> Parameters, ExpressionSyntax Body);

    /// <summary>
    /// Reads one admitted expression lambda: an expression body, one or two by-value parameters without defaults,
    /// and no modifiers. Everything else — block bodies, <c>ref</c>/async modifiers, defaults — is a typed stop
    /// naming what is not modeled, never a silent fallback.
    /// </summary>
    private static bool TryReadLambda(
        ExpressionSyntax syntax,
        int maximumParameters,
        out LambdaShape lambda,
        out FoldOutcome error)
    {
        lambda = default;
        error = default;
        ParameterSyntax[] parameters;
        ExpressionSyntax? body;
        switch (syntax)
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
                error = FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    "Only lambda expressions are admitted here.");
                return false;
        }

        if (syntax is LambdaExpressionSyntax { Modifiers.Count: > 0 })
        {
            error = FoldOutcome.Error(
                LambdaUnsupportedCode,
                "Lambda modifiers such as 'static' or 'async' are not modeled; write a plain expression lambda.");
            return false;
        }

        if (body is null)
        {
            error = FoldOutcome.Error(
                LambdaUnsupportedCode,
                "Only expression-bodied lambdas are supported; a block body would need statement execution "
                + "this evaluator deliberately does not model.");
            return false;
        }

        if (parameters.Length is 0 || parameters.Length > maximumParameters)
        {
            error = FoldOutcome.Error(
                LambdaUnsupportedCode,
                maximumParameters == 1
                    ? "This operator takes a lambda with exactly one parameter."
                    : "This operator takes a lambda with one parameter, or two where the second is the element index.");
            return false;
        }

        var names = ImmutableArray.CreateBuilder<string>(parameters.Length);
        foreach (var parameter in parameters)
        {
            if (parameter.Default is not null || parameter.Modifiers.Count > 0)
            {
                error = FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    "Lambda parameters must be plain by-value names without defaults or modifiers.");
                return false;
            }

            names.Add(parameter.Identifier.ValueText);
        }

        lambda = new LambdaShape(names.ToImmutable(), body);
        return true;
    }

    /// <summary>Folds the lambda body for one element, with the parameter (and optional index) bound.</summary>
    private static FoldOutcome InvokeLambda(LambdaShape lambda, Operand element, int index, FoldContext context)
    {
        context.PushBinding(lambda.Parameters[0], element);
        if (lambda.Parameters.Length == 2)
        {
            context.PushBinding(lambda.Parameters[1], Operand.FromInt32(index));
        }

        try
        {
            return Fold(lambda.Body, context);
        }
        finally
        {
            context.PopBindings(lambda.Parameters.Length);
        }
    }

    /// <summary>Folds the lambda body for one element and requires a Boolean result.</summary>
    private static FoldOutcome InvokePredicate(LambdaShape lambda, Operand element, int index, FoldContext context)
    {
        var outcome = InvokeLambda(lambda, element, index, context);
        if (outcome.Disposition != FoldDisposition.Folded)
        {
            return outcome;
        }

        return outcome.Operand.Kind == OperandKind.Boolean
            ? outcome
            : FoldOutcome.Error(OperandTypeCode, "The predicate lambda must produce a Boolean for every element.");
    }

    /// <summary>
    /// Dispatches an invocation that carries a lambda argument: the instance form over a folded sequence receiver,
    /// or the equivalent <c>System.Linq.Enumerable</c> static form whose first argument is the sequence.
    /// </summary>
    private static FoldOutcome FoldLambdaInvocation(
        ExpressionSyntax receiverExpression,
        string name,
        ArgumentListSyntax argumentList,
        FoldContext context)
    {
        foreach (var argument in argumentList.Arguments)
        {
            if (argument.NameColon is not null || argument.RefKindKeyword != default)
            {
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    "Named and ref arguments are not modeled on the lambda sequence surface.");
            }
        }

        ExpressionSyntax sourceExpression;
        IReadOnlyList<ExpressionSyntax> operatorArguments;
        var receiverIsBound = receiverExpression is IdentifierNameSyntax { Identifier.ValueText: { } receiverName }
            && context.IsBound(receiverName);
        if (!receiverIsBound &&
            TryReadTypeReceiver(receiverExpression, out var typeReceiver) &&
            typeReceiver.Category is TypeReceiverCategory.Enumerable or TypeReceiverCategory.SystemArray)
        {
            if (argumentList.Arguments.Count < 2)
            {
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    $"{(typeReceiver.Category == TypeReceiverCategory.Enumerable ? "Enumerable" : "Array")}.{name} "
                    + "takes the source sequence and at least one lambda.");
            }

            // The Array delegate family maps onto the sequence operators with identical semantics.
            if (typeReceiver.Category == TypeReceiverCategory.SystemArray)
            {
                name = name switch
                {
                    "Exists" => "Any",
                    "TrueForAll" => "All",
                    "Find" => "FirstOrDefault",
                    "FindLast" => "LastOrDefault",
                    "FindAll" => "Where",
                    "ConvertAll" => "Select",
                    _ => name,
                };
            }

            sourceExpression = argumentList.Arguments[0].Expression;
            operatorArguments = [.. argumentList.Arguments.Skip(1).Select(static argument => argument.Expression)];
        }
        else
        {
            sourceExpression = receiverExpression;
            operatorArguments = [.. argumentList.Arguments.Select(static argument => argument.Expression)];
        }

        var source = Fold(sourceExpression, context);
        if (source.Disposition != FoldDisposition.Folded)
        {
            return source;
        }

        if (!TryAsSequence(source.Operand, out var sourcePayload))
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                "Lambda operators need an array-like sequence receiver; a string becomes one via ToCharArray().");
        }

        if (name is "SelectMany" or "GroupBy" or "Join" or "GroupJoin" or "ThenBy" or "ThenByDescending")
        {
            return DispatchMultiLambdaOperator(sourcePayload, name, operatorArguments, context);
        }

        if (operatorArguments is not [LambdaExpressionSyntax lambdaExpression])
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                $"'{name}' takes exactly one lambda argument on the sequence surface.");
        }

        var maximumParameters = name is "Select" or "Where" ? 2 : 1;
        if (!TryReadLambda(lambdaExpression, maximumParameters, out var lambda, out var lambdaError))
        {
            return lambdaError;
        }

        return DispatchSequenceLambda(sourcePayload, name, lambda, context);
    }

    /// <summary>The lambda-taking operator set over one sequence, with real <c>Enumerable</c> semantics.</summary>
    private static FoldOutcome DispatchSequenceLambda(
        SequencePayload payload,
        string name,
        LambdaShape lambda,
        FoldContext context)
    {
        var items = payload.Items;
        switch (name)
        {
            case "Select":
                var projected = ImmutableArray.CreateBuilder<Operand>(items.Length);
                for (var index = 0; index < items.Length; index++)
                {
                    var value = InvokeLambda(lambda, items[index], index, context);
                    if (value.Disposition != FoldDisposition.Folded)
                    {
                        return value;
                    }

                    projected.Add(value.Operand);
                }

                // The projected element domain is inferred from the produced values, mirroring how the array
                // initializer infers its best common type. An empty source stays an empty sequence; its element
                // domain is unobservable, so the source's domain is carried for display.
                return items.Length == 0
                    ? CreateSequence(payload.With([]))
                    : CreateInferredSequence(projected.ToImmutable());
            case "Where":
            case "TakeWhile" when lambda.Parameters.Length == 1:
            case "SkipWhile" when lambda.Parameters.Length == 1:
                var kept = ImmutableArray.CreateBuilder<Operand>();
                var skipping = name == "SkipWhile";
                for (var index = 0; index < items.Length; index++)
                {
                    var verdict = InvokePredicate(lambda, items[index], index, context);
                    if (verdict.Disposition != FoldDisposition.Folded)
                    {
                        return verdict;
                    }

                    var matched = verdict.Operand.Boolean;
                    if (name == "Where")
                    {
                        if (matched)
                        {
                            kept.Add(items[index]);
                        }

                        continue;
                    }

                    if (name == "TakeWhile")
                    {
                        if (!matched)
                        {
                            break;
                        }

                        kept.Add(items[index]);
                        continue;
                    }

                    if (skipping && matched)
                    {
                        continue;
                    }

                    skipping = false;
                    kept.Add(items[index]);
                }

                return CreateSequence(payload.With(kept.ToImmutable()));
            case "Any":
            case "All":
                for (var index = 0; index < items.Length; index++)
                {
                    var verdict = InvokePredicate(lambda, items[index], index, context);
                    if (verdict.Disposition != FoldDisposition.Folded)
                    {
                        return verdict;
                    }

                    if (verdict.Operand.Boolean == (name == "Any"))
                    {
                        return FoldOutcome.Folded(Operand.FromBoolean(name == "Any"));
                    }
                }

                return FoldOutcome.Folded(Operand.FromBoolean(name == "All"));
            case "Count":
                var matches = 0;
                for (var index = 0; index < items.Length; index++)
                {
                    var verdict = InvokePredicate(lambda, items[index], index, context);
                    if (verdict.Disposition != FoldDisposition.Folded)
                    {
                        return verdict;
                    }

                    if (verdict.Operand.Boolean)
                    {
                        matches++;
                    }
                }

                return FoldOutcome.Folded(Operand.FromInt32(matches));
            case "FindIndex":
            case "FindLastIndex":
                var matchedIndex = -1;
                for (var index = 0; index < items.Length; index++)
                {
                    var verdict = InvokePredicate(lambda, items[index], index, context);
                    if (verdict.Disposition != FoldDisposition.Folded)
                    {
                        return verdict;
                    }

                    if (verdict.Operand.Boolean)
                    {
                        matchedIndex = index;
                        if (name == "FindIndex")
                        {
                            break;
                        }
                    }
                }

                return FoldOutcome.Folded(Operand.FromInt32(matchedIndex));
            case "First":
            case "FirstOrDefault":
            case "Last":
            case "LastOrDefault":
            case "Single":
            case "SingleOrDefault":
                Operand? found = null;
                for (var index = 0; index < items.Length; index++)
                {
                    var verdict = InvokePredicate(lambda, items[index], index, context);
                    if (verdict.Disposition != FoldDisposition.Folded)
                    {
                        return verdict;
                    }

                    if (!verdict.Operand.Boolean)
                    {
                        continue;
                    }

                    if (name is "First" or "FirstOrDefault")
                    {
                        return FoldOutcome.Folded(items[index]);
                    }

                    if (name is "Single" or "SingleOrDefault" && found is not null)
                    {
                        return FoldOutcome.Error(
                            "System.InvalidOperationException",
                            "Sequence contains more than one matching element.");
                    }

                    found = items[index];
                }

                if (found is { } match)
                {
                    return FoldOutcome.Folded(match);
                }

                return name is "FirstOrDefault" or "LastOrDefault" or "SingleOrDefault"
                    ? FoldOutcome.Folded(DefaultOfElement(payload))
                    : FoldOutcome.Error(
                        "System.InvalidOperationException",
                        "Sequence contains no matching element.");
            case "Sum":
            case "Min":
            case "Max":
            case "Average":
                var selected = SelectWith(payload, lambda, context);
                if (selected.Disposition != FoldDisposition.Folded)
                {
                    return selected;
                }

                var selectedPayload = PayloadOf(selected.Operand);
                return name switch
                {
                    "Sum" => SequenceSum(selectedPayload),
                    "Min" => SequenceMinMax(selectedPayload, wantMin: true),
                    "Max" => SequenceMinMax(selectedPayload, wantMin: false),
                    _ => SequenceAverage(selectedPayload),
                };
            case "OrderBy":
            case "OrderByDescending":
                return SequenceOrderBy(payload, lambda, name == "OrderByDescending", context);
            default:
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    $"'{name}' is not an admitted lambda operator; supported are Select, Where, Any, All, Count, "
                    + "First/Last/Single (and their OrDefault forms), Sum, Min, Max, Average, OrderBy, "
                    + "OrderByDescending, TakeWhile, and SkipWhile.");
        }
    }

    /// <summary>Projects every element through the selector, producing the inferred-domain sequence.</summary>
    private static FoldOutcome SelectWith(SequencePayload payload, LambdaShape lambda, FoldContext context)
    {
        if (payload.Items.Length == 0)
        {
            return CreateSequence(payload.With([]));
        }

        var projected = ImmutableArray.CreateBuilder<Operand>(payload.Items.Length);
        for (var index = 0; index < payload.Items.Length; index++)
        {
            var value = InvokeLambda(lambda, payload.Items[index], index, context);
            if (value.Disposition != FoldDisposition.Folded)
            {
                return value;
            }

            projected.Add(value.Operand);
        }

        return CreateInferredSequence(projected.ToImmutable());
    }

    /// <summary>Stable key-ordered sort with the same comparison domains as <c>Order</c>.</summary>
    private static FoldOutcome SequenceOrderBy(
        SequencePayload payload,
        LambdaShape lambda,
        bool descending,
        FoldContext context) =>
        SequenceOrderByLevels(payload, lambda, descending, priorOrdering: null, context);

    /// <summary>
    /// Sorts by a new key level after any carried levels, exactly as <c>OrderBy…ThenBy</c> chains compose: the
    /// sort is stable, each level compares in its own domain with its own direction, and the result carries the
    /// combined ordering evidence so a further <c>ThenBy</c> can refine it.
    /// </summary>
    private static FoldOutcome SequenceOrderByLevels(
        SequencePayload payload,
        LambdaShape lambda,
        bool descending,
        SequenceOrdering? priorOrdering,
        FoldContext context)
    {
        var newKeys = new Operand[payload.Items.Length];
        for (var index = 0; index < payload.Items.Length; index++)
        {
            var key = InvokeLambda(lambda, payload.Items[index], index, context);
            if (key.Disposition != FoldDisposition.Folded)
            {
                return key;
            }

            if (key.Operand.Kind == OperandKind.String)
            {
                return CultureSensitive(
                    "OrderBy over string keys",
                    "an explicit ordinal transformation; default string ordering is culture-sensitive");
            }

            if (!key.Operand.IsNumeric &&
                key.Operand.Kind is not (OperandKind.Boolean or OperandKind.Temporal or OperandKind.BclValue))
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "Ordering keys must be numeric, char, Boolean, date/time, Guid, or Version values.");
            }

            newKeys[index] = key.Operand;
        }

        var levels = priorOrdering?.Descending.Length + 1 ?? 1;
        var keysPerItem = new ImmutableArray<Operand>[payload.Items.Length];
        for (var index = 0; index < payload.Items.Length; index++)
        {
            keysPerItem[index] = priorOrdering is null
                ? [newKeys[index]]
                : [.. priorOrdering.KeysPerItem[index], newKeys[index]];
        }

        var directions = priorOrdering is null
            ? ImmutableArray.Create(descending)
            : priorOrdering.Descending.Add(descending);

        string? failure = null;
        int Compare(int left, int right)
        {
            for (var level = 0; level < levels && failure is null; level++)
            {
                var comparison = CompareOrderingKeys(
                    keysPerItem[left][level],
                    keysPerItem[right][level],
                    ref failure);
                if (comparison != 0)
                {
                    return directions[level] ? -comparison : comparison;
                }
            }

            return 0;
        }

        // Enumerable.OrderBy is a stable sort, and stability is part of the published semantics. Every level is
        // applied in one comparison, so refinement never disturbs the primary order.
        var indices = Enumerable.Range(0, payload.Items.Length);
        var ordered = indices.OrderBy(static index => index, Comparer<int>.Create(Compare)).ToArray();
        if (failure is not null)
        {
            return FoldOutcome.Error(OperandTypeCode, failure);
        }

        var materialized = ordered.Select(index => payload.Items[index]).ToImmutableArray();
        var orderedKeys = ordered.Select(index => keysPerItem[index]).ToImmutableArray();
        return CreateSequence(new SequencePayload(
            materialized,
            payload.ElementKind,
            payload.ElementNumeric,
            payload.DisplayName,
            new SequenceOrdering(orderedKeys, directions)));
    }

    /// <summary>Compares two ordering keys in their shared domain; a mix reports a failure.</summary>
    private static int CompareOrderingKeys(Operand leftKey, Operand rightKey, ref string? failure)
    {
        if (leftKey.Kind == OperandKind.Boolean && rightKey.Kind == OperandKind.Boolean)
        {
            return leftKey.Boolean.CompareTo(rightKey.Boolean);
        }

        if (TryCompareTemporal(leftKey, rightKey, out var temporalComparison))
        {
            return temporalComparison;
        }

        if (TryCompareBclValue(leftKey, rightKey, out var valueComparison))
        {
            return valueComparison;
        }

        if (leftKey.IsNumeric && rightKey.IsNumeric)
        {
            if (!TryPromote(leftKey, rightKey, out var target, out _))
            {
                failure = "The keys mix numeric domains that no comparison combines.";
                return 0;
            }

            return target switch
            {
                NumericKind.Single or NumericKind.Double => Comparer<double>.Default.Compare(
                    ToDoubleValue(NumericKindOf(leftKey), BoxOf(leftKey)),
                    ToDoubleValue(NumericKindOf(rightKey), BoxOf(rightKey))),
                NumericKind.Decimal => ToDecimalValue(leftKey).CompareTo(ToDecimalValue(rightKey)),
                _ => ToBigInteger(NumericKindOf(leftKey), BoxOf(leftKey)).CompareTo(
                    ToBigInteger(NumericKindOf(rightKey), BoxOf(rightKey))),
            };
        }

        failure = "The keys mix value domains that no comparison combines.";
        return 0;
    }
}
