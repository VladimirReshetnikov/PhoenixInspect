using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The multi-lambda sequence operators query expressions translate into — <c>SelectMany</c>, <c>GroupBy</c> with
/// real groupings, <c>Join</c>, <c>GroupJoin</c>, and the <c>ThenBy</c> family over ordered sequences — with the
/// exact <c>System.Linq.Enumerable</c> semantics: first-occurrence group order, outer-then-inner join order, and
/// stable multi-key sorting.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>One folded grouping: its key and its member sequence, as <c>GroupBy</c> produced them.</summary>
    /// <param name="Key">The group key.</param>
    /// <param name="Items">The group's members, in source order.</param>
    private sealed record GroupingPayload(Operand Key, SequencePayload Items);

    private static GroupingPayload PayloadOfGrouping(Operand operand) => (GroupingPayload)operand.Box!;

    /// <summary>Views one operand as a sequence: a sequence directly, or a grouping through its members.</summary>
    private static bool TryAsSequence(Operand operand, out SequencePayload payload)
    {
        switch (operand.Kind)
        {
            case OperandKind.Sequence:
                payload = PayloadOf(operand);
                return true;
            case OperandKind.Grouping:
                payload = PayloadOfGrouping(operand).Items;
                return true;
            case OperandKind.BclValue when IsRegexCollection(operand.BclValueKind):
                // A match, group, or capture collection is a finished sequence of values, so the whole LINQ
                // surface composes over it exactly as over an array.
                payload = MaterializeRegexCollection(operand);
                return true;
            default:
                payload = null!;
                return false;
        }
    }

    /// <summary>Renders one grouping for display: its key, then its members.</summary>
    private static string RenderGrouping(Operand operand)
    {
        var grouping = PayloadOfGrouping(operand);
        return $"[{RenderCompoundOrElement(grouping.Key)}] {RenderSequence(grouping.Items)}";
    }

    /// <summary>
    /// Dispatches the operators that take more than one lambda or mix sequence and lambda arguments. Argument
    /// expressions arrive unfolded, so each lambda binds with its own parameter shape.
    /// </summary>
    private static FoldOutcome DispatchMultiLambdaOperator(
        SequencePayload payload,
        string name,
        IReadOnlyList<ExpressionSyntax> arguments,
        FoldContext context)
    {
        switch (name)
        {
            case "SelectMany" when arguments is [var collection]:
                return SequenceSelectMany(payload, collection, resultSelector: null, context);
            case "SelectMany" when arguments is [var collection, var result]:
                return SequenceSelectMany(payload, collection, result, context);
            case "GroupBy" when arguments is [var keySelector]:
                return SequenceGroupBy(payload, keySelector, elementSelector: null, context);
            case "GroupBy" when arguments is [var keySelector, var elementSelector]:
                return SequenceGroupBy(payload, keySelector, elementSelector, context);
            case "Join" when arguments is [var inner, var outerKey, var innerKey, var result]:
                return SequenceJoin(payload, inner, outerKey, innerKey, result, groupJoin: false, context);
            case "GroupJoin" when arguments is [var inner, var outerKey, var innerKey, var result]:
                return SequenceJoin(payload, inner, outerKey, innerKey, result, groupJoin: true, context);
            case "ThenBy" when arguments is [var ascendingKey]:
                return SequenceThenBy(payload, ascendingKey, descending: false, context);
            case "ThenByDescending" when arguments is [var descendingKey]:
                return SequenceThenBy(payload, descendingKey, descending: true, context);
            default:
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    $"'{name}' does not take this argument shape on the sequence surface.");
        }
    }

    private static FoldOutcome SequenceSelectMany(
        SequencePayload payload,
        ExpressionSyntax collectionSelector,
        ExpressionSyntax? resultSelector,
        FoldContext context)
    {
        if (!TryReadLambda(collectionSelector, 1, out var collectionLambda, out var collectionError))
        {
            return collectionError;
        }

        LambdaShape resultLambda = default;
        if (resultSelector is not null)
        {
            if (!TryReadLambda(resultSelector, 2, out resultLambda, out var resultError))
            {
                return resultError;
            }

            if (resultLambda.Parameters.Length != 2)
            {
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    "The SelectMany result selector takes the source element and the collection element.");
            }
        }

        var flattened = ImmutableArray.CreateBuilder<Operand>();
        for (var index = 0; index < payload.Items.Length; index++)
        {
            var item = payload.Items[index];
            var collection = InvokeLambda(collectionLambda, item, index, context);
            if (collection.Disposition != FoldDisposition.Folded)
            {
                return collection;
            }

            if (!TryAsSequence(collection.Operand, out var subsequence))
            {
                return FoldOutcome.Error(
                    LambdaUnsupportedCode,
                    "The SelectMany collection selector must produce a sequence for every element.");
            }

            foreach (var subitem in subsequence.Items)
            {
                if (resultSelector is null)
                {
                    flattened.Add(subitem);
                    continue;
                }

                context.PushBinding(resultLambda.Parameters[0], item);
                context.PushBinding(resultLambda.Parameters[1], subitem);
                FoldOutcome projected;
                try
                {
                    projected = Fold(resultLambda.Body, context);
                }
                finally
                {
                    context.PopBindings(2);
                }

                if (projected.Disposition != FoldDisposition.Folded)
                {
                    return projected;
                }

                flattened.Add(projected.Operand);
            }
        }

        return flattened.Count == 0
            ? CreateSequence(payload.With([]))
            : CreateInferredSequence(flattened.ToImmutable());
    }

    private static FoldOutcome SequenceGroupBy(
        SequencePayload payload,
        ExpressionSyntax keySelector,
        ExpressionSyntax? elementSelector,
        FoldContext context)
    {
        if (!TryReadLambda(keySelector, 1, out var keyLambda, out var keyError))
        {
            return keyError;
        }

        LambdaShape elementLambda = default;
        if (elementSelector is not null && !TryReadLambda(elementSelector, 1, out elementLambda, out var elementError))
        {
            return elementError;
        }

        // Enumerable.GroupBy yields groups in first-occurrence key order, and members in source order.
        var keys = new List<Operand>();
        var groups = new List<List<Operand>>();
        for (var index = 0; index < payload.Items.Length; index++)
        {
            var key = InvokeLambda(keyLambda, payload.Items[index], index, context);
            if (key.Disposition != FoldDisposition.Folded)
            {
                return key;
            }

            Operand element = payload.Items[index];
            if (elementSelector is not null)
            {
                var projected = InvokeLambda(elementLambda, payload.Items[index], index, context);
                if (projected.Disposition != FoldDisposition.Folded)
                {
                    return projected;
                }

                element = projected.Operand;
            }

            var groupIndex = -1;
            for (var probe = 0; probe < keys.Count; probe++)
            {
                var equal = OperandsEqual(keys[probe], key.Operand);
                if (equal.Disposition != FoldDisposition.Folded)
                {
                    return equal;
                }

                if (equal.Operand.Boolean)
                {
                    groupIndex = probe;
                    break;
                }
            }

            if (groupIndex < 0)
            {
                keys.Add(key.Operand);
                groups.Add([]);
                groupIndex = keys.Count - 1;
            }

            groups[groupIndex].Add(element);
        }

        var groupings = ImmutableArray.CreateBuilder<Operand>(keys.Count);
        for (var index = 0; index < keys.Count; index++)
        {
            var members = groups[index].ToImmutableArray();
            var membersPayload = elementSelector is null
                ? payload.With(members)
                : InferPayload(members);
            if (membersPayload is null)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "The group elements mix value domains that no sequence combines.");
            }

            groupings.Add(Operand.FromGrouping(new GroupingPayload(keys[index], membersPayload)));
        }

        return CreateSequence(new SequencePayload(
            groupings.MoveToImmutable(),
            OperandKind.Grouping,
            default,
            "IGrouping"));
    }

    private static FoldOutcome SequenceJoin(
        SequencePayload payload,
        ExpressionSyntax innerExpression,
        ExpressionSyntax outerKeySelector,
        ExpressionSyntax innerKeySelector,
        ExpressionSyntax resultSelector,
        bool groupJoin,
        FoldContext context)
    {
        var inner = Fold(innerExpression, context);
        if (inner.Disposition != FoldDisposition.Folded)
        {
            return inner;
        }

        if (!TryAsSequence(inner.Operand, out var innerPayload))
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                "The join inner source must be a sequence.");
        }

        if (!TryReadLambda(outerKeySelector, 1, out var outerKeyLambda, out var outerKeyError))
        {
            return outerKeyError;
        }

        if (!TryReadLambda(innerKeySelector, 1, out var innerKeyLambda, out var innerKeyError))
        {
            return innerKeyError;
        }

        if (!TryReadLambda(resultSelector, 2, out var resultLambda, out var resultError))
        {
            return resultError;
        }

        if (resultLambda.Parameters.Length != 2)
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                groupJoin
                    ? "The GroupJoin result selector takes the outer element and the matching inner sequence."
                    : "The Join result selector takes the outer and inner elements.");
        }

        var innerKeys = new Operand[innerPayload.Items.Length];
        for (var index = 0; index < innerPayload.Items.Length; index++)
        {
            var key = InvokeLambda(innerKeyLambda, innerPayload.Items[index], index, context);
            if (key.Disposition != FoldDisposition.Folded)
            {
                return key;
            }

            innerKeys[index] = key.Operand;
        }

        var results = ImmutableArray.CreateBuilder<Operand>();
        for (var outerIndex = 0; outerIndex < payload.Items.Length; outerIndex++)
        {
            var outerItem = payload.Items[outerIndex];
            var outerKey = InvokeLambda(outerKeyLambda, outerItem, outerIndex, context);
            if (outerKey.Disposition != FoldDisposition.Folded)
            {
                return outerKey;
            }

            var matches = ImmutableArray.CreateBuilder<Operand>();
            for (var innerIndex = 0; innerIndex < innerPayload.Items.Length; innerIndex++)
            {
                var equal = OperandsEqual(outerKey.Operand, innerKeys[innerIndex]);
                if (equal.Disposition != FoldDisposition.Folded)
                {
                    return equal;
                }

                if (equal.Operand.Boolean)
                {
                    matches.Add(innerPayload.Items[innerIndex]);
                }
            }

            if (groupJoin)
            {
                var matchesOperand = Operand.FromSequence(innerPayload.With(matches.ToImmutable()));
                var projected = InvokePairLambda(resultLambda, outerItem, matchesOperand, context);
                if (projected.Disposition != FoldDisposition.Folded)
                {
                    return projected;
                }

                results.Add(projected.Operand);
                continue;
            }

            foreach (var match in matches)
            {
                var projected = InvokePairLambda(resultLambda, outerItem, match, context);
                if (projected.Disposition != FoldDisposition.Folded)
                {
                    return projected;
                }

                results.Add(projected.Operand);
            }
        }

        return results.Count == 0
            ? CreateSequence(payload.With([]))
            : CreateInferredSequence(results.ToImmutable());
    }

    /// <summary>Folds a two-parameter lambda body with both parameters bound.</summary>
    private static FoldOutcome InvokePairLambda(
        LambdaShape lambda,
        Operand first,
        Operand second,
        FoldContext context)
    {
        context.PushBinding(lambda.Parameters[0], first);
        context.PushBinding(lambda.Parameters[1], second);
        try
        {
            return Fold(lambda.Body, context);
        }
        finally
        {
            context.PopBindings(2);
        }
    }

    private static FoldOutcome SequenceThenBy(
        SequencePayload payload,
        ExpressionSyntax keySelector,
        bool descending,
        FoldContext context)
    {
        if (payload.Ordering is null)
        {
            return FoldOutcome.Error(
                LambdaUnsupportedCode,
                "ThenBy refines an ordered sequence; call OrderBy or OrderByDescending first.");
        }

        if (!TryReadLambda(keySelector, 1, out var keyLambda, out var keyError))
        {
            return keyError;
        }

        return SequenceOrderByLevels(payload, keyLambda, descending, payload.Ordering, context);
    }

    /// <summary>Builds a sequence payload for already-folded members by inference, or null when they mix.</summary>
    private static SequencePayload? InferPayload(ImmutableArray<Operand> members)
    {
        if (members.Length == 0)
        {
            return new SequencePayload([], OperandKind.String, default, "String");
        }

        var inferred = CreateInferredSequence(members);
        return inferred.Disposition == FoldDisposition.Folded ? PayloadOf(inferred.Operand) : null;
    }
}
