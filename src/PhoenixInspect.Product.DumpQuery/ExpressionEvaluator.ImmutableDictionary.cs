using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The immutable dictionaries and their element domain. A <c>KeyValuePair</c> is a first-class pair value, and
/// an <c>ImmutableDictionary</c> or <c>ImmutableSortedDictionary</c> is a virtual sequence of pairs carrying the
/// dictionary identity: keys stay unique exactly as the BCL enforces, a sorted dictionary orders by key with the
/// evaluator's deterministic comparison, and lookup, <c>Add</c>, <c>SetItem</c>, and <c>Remove</c> answer with
/// the BCL's own semantics and exceptions.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>One key/value pair value.</summary>
    private sealed record KeyValuePairPayload(Operand Key, Operand Value);

    private static KeyValuePairPayload PayloadOfPair(Operand operand) => (KeyValuePairPayload)operand.Box!;

    /// <summary>Gets whether the collection kind is one of the two dictionary kinds.</summary>
    private static bool IsDictionaryCollection(SequenceCollectionKind kind) =>
        kind is SequenceCollectionKind.ImmutableDictionary or SequenceCollectionKind.ImmutableSortedDictionary;

    /// <summary>Names a pair's constructed type from its component values; null components read as Object.</summary>
    private static string KeyValuePairTypeName(KeyValuePairPayload pair) =>
        $"KeyValuePair<{ComponentTypeName(pair.Key)}, {ComponentTypeName(pair.Value)}>";

    private static string ComponentTypeName(Operand operand) =>
        operand.Kind == OperandKind.Null ? "Object" : DisplayTypeNameOf(operand);

    /// <summary>Renders a pair exactly as its <c>ToString</c> does: <c>[key, value]</c>.</summary>
    private static string RenderKeyValuePair(KeyValuePairPayload pair) =>
        $"[{RenderCompoundOrElement(pair.Key)}, {RenderCompoundOrElement(pair.Value)}]";

    // ---- Construction -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a dictionary from pairs: keys must be unique — the exact CreateRange semantics — and a sorted
    /// dictionary orders by key deterministically, so a culture-sensitive string ordering is a typed stop.
    /// </summary>
    private static FoldOutcome CreateImmutableDictionary(
        SequenceCollectionKind kind,
        ImmutableArray<Operand> pairs,
        string elementDisplayName)
    {
        for (var index = 0; index < pairs.Length; index++)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            for (var earlier = 0; earlier < index; earlier++)
            {
                var equals = OperandsEqual(
                    PayloadOfPair(pairs[index]).Key, PayloadOfPair(pairs[earlier]).Key);
                if (equals.Disposition != FoldDisposition.Folded)
                {
                    return equals;
                }

                if (equals.Operand.Boolean)
                {
                    return FoldOutcome.Error(
                        "System.ArgumentException",
                        "An item with the same key has already been added. Key: "
                        + RenderCompoundOrElement(PayloadOfPair(pairs[index]).Key));
                }
            }
        }

        if (kind == SequenceCollectionKind.ImmutableSortedDictionary && pairs.Length > 1)
        {
            var sorted = SortPairsByKey(pairs, out var failure);
            if (failure is { } sortStop)
            {
                return sortStop;
            }

            pairs = sorted;
        }

        return CreateSequence(new SequencePayload(
            pairs, OperandKind.KeyValuePair, default, elementDisplayName, collection: kind));
    }

    /// <summary>Orders pairs by key with the same deterministic comparison the sequence Order applies.</summary>
    private static ImmutableArray<Operand> SortPairsByKey(
        ImmutableArray<Operand> pairs,
        out FoldOutcome? failure)
    {
        var keys = ImmutableArray.CreateBuilder<Operand>(pairs.Length);
        foreach (var pair in pairs)
        {
            keys.Add(PayloadOfPair(pair).Key);
        }

        var keyInference = CreateInferredSequence(keys.ToImmutable());
        if (keyInference.Disposition != FoldDisposition.Folded)
        {
            failure = keyInference;
            return pairs;
        }

        var ordered = SequenceOrder(PayloadOf(keyInference.Operand), descending: false);
        if (ordered.Disposition != FoldDisposition.Folded)
        {
            failure = ordered;
            return pairs;
        }

        // Keys are unique, so matching each sorted key back to its pair is unambiguous.
        var remaining = pairs.ToList();
        var result = ImmutableArray.CreateBuilder<Operand>(pairs.Length);
        foreach (var key in PayloadOf(ordered.Operand).Items)
        {
            for (var index = 0; index < remaining.Count; index++)
            {
                var equals = OperandsEqual(PayloadOfPair(remaining[index]).Key, key);
                if (equals.Disposition != FoldDisposition.Folded)
                {
                    failure = equals;
                    return pairs;
                }

                if (equals.Operand.Boolean)
                {
                    result.Add(remaining[index]);
                    remaining.RemoveAt(index);
                    break;
                }
            }
        }

        failure = null;
        return result.MoveToImmutable();
    }

    /// <summary>One static factory invocation over an immutable-dictionary receiver.</summary>
    private static FoldOutcome DispatchImmutableDictionaryFactory(
        TypeReceiver receiver,
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext? context)
    {
        var kind = receiver.Collection;
        switch (name)
        {
            case "Create" when arguments.Count == 0 && typeArguments.Count == 2:
                if (context is null)
                {
                    return MemberUnsupported($"{kind}.Create via reflection without an evaluation context");
                }

                if (!TryResolveElementType(typeArguments[0], context, out var keyType, out var keyError))
                {
                    return keyError ?? FoldOutcome.Error(
                        OperandTypeCode,
                        "The key type is outside the evaluator's modeled element domains.");
                }

                if (!TryResolveElementType(typeArguments[1], context, out var valueType, out var valueError))
                {
                    return valueError ?? FoldOutcome.Error(
                        OperandTypeCode,
                        "The value type is outside the evaluator's modeled element domains.");
                }

                return CreateImmutableDictionary(
                    kind, [], $"KeyValuePair<{keyType.DisplayName}, {valueType.DisplayName}>");
            case "Create" when typeArguments.Count == 0 && arguments.Count == 0:
                return FoldOutcome.Error(
                    OperandTypeCode,
                    $"'{kind}.Create()' needs its type arguments: write '{kind}.Create<TKey, TValue>()'.");
            case "CreateRange" when arguments is [{ Kind: OperandKind.Sequence } range]:
                var payload = PayloadOf(range);
                foreach (var item in payload.Items)
                {
                    if (item.Kind != OperandKind.KeyValuePair)
                    {
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            $"'{kind}.CreateRange' takes a sequence of KeyValuePair elements.");
                    }
                }

                var elementDisplay =
                    payload.DisplayName.StartsWith("KeyValuePair<", StringComparison.Ordinal)
                        ? payload.DisplayName
                        : payload.Items.Length > 0
                            ? KeyValuePairTypeName(PayloadOfPair(payload.Items[0]))
                            : "KeyValuePair<Object, Object>";
                return CreateImmutableDictionary(kind, payload.Items, elementDisplay);
            default:
                return MemberUnsupported($"{kind}.{name}");
        }
    }

    /// <summary>
    /// The selector overloads of <c>ToImmutableDictionary</c>: each element yields its key, and its value is
    /// the element itself or the value selector's projection, with duplicate keys throwing exactly as the BCL.
    /// </summary>
    private static FoldOutcome SequenceToImmutableDictionary(
        SequencePayload payload,
        SequenceCollectionKind kind,
        ExpressionSyntax keySelector,
        ExpressionSyntax? valueSelector,
        FoldContext context)
    {
        if (!TryReadLambda(keySelector, 1, out var keyLambda, out var keyError))
        {
            return keyError;
        }

        LambdaShape valueLambda = default;
        if (valueSelector is not null && !TryReadLambda(valueSelector, 1, out valueLambda, out var valueError))
        {
            return valueError;
        }

        var pairs = ImmutableArray.CreateBuilder<Operand>(payload.Items.Length);
        for (var index = 0; index < payload.Items.Length; index++)
        {
            var key = InvokeLambda(keyLambda, payload.Items[index], index, context);
            if (key.Disposition != FoldDisposition.Folded)
            {
                return key;
            }

            var value = payload.Items[index];
            if (valueSelector is not null)
            {
                var projected = InvokeLambda(valueLambda, payload.Items[index], index, context);
                if (projected.Disposition != FoldDisposition.Folded)
                {
                    return projected;
                }

                value = projected.Operand;
            }

            pairs.Add(Operand.FromKeyValuePair(new KeyValuePairPayload(key.Operand, value)));
        }

        var built = pairs.MoveToImmutable();
        return CreateImmutableDictionary(
            kind,
            built,
            built.Length > 0
                ? KeyValuePairTypeName(PayloadOfPair(built[0]))
                : "KeyValuePair<Object, Object>");
    }

    /// <summary>Folds a dictionary lookup — <c>d[key]</c> — with the BCL's missing-key exception.</summary>
    private static FoldOutcome FoldDictionaryLookup(
        Operand receiver,
        ElementAccessExpressionSyntax elementAccess,
        FoldContext context)
    {
        if (elementAccess.ArgumentList.Arguments is not [{ NameColon: null } argument] ||
            argument.RefKindKeyword != default)
        {
            return FoldOutcome.Error(OperandTypeCode, "A dictionary lookup takes exactly one key.");
        }

        var key = Fold(argument.Expression, context);
        if (key.Disposition != FoldDisposition.Folded)
        {
            return key;
        }

        var found = FindPairByKey(PayloadOf(receiver).Items, key.Operand);
        if (found.Disposition != FoldDisposition.Folded)
        {
            return found;
        }

        return found.Operand.Int32 < 0
            ? FoldOutcome.Error(
                "System.Collections.Generic.KeyNotFoundException",
                $"The given key '{RenderCompoundOrElement(key.Operand)}' was not present in the dictionary.")
            : FoldOutcome.Folded(PayloadOfPair(PayloadOf(receiver).Items[found.Operand.Int32]).Value);
    }

    /// <summary>Finds the pair whose key equals the sought key; −1 when absent.</summary>
    private static FoldOutcome FindPairByKey(ImmutableArray<Operand> pairs, Operand key)
    {
        for (var index = 0; index < pairs.Length; index++)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var equals = OperandsEqual(PayloadOfPair(pairs[index]).Key, key);
            if (equals.Disposition != FoldDisposition.Folded)
            {
                return equals;
            }

            if (equals.Operand.Boolean)
            {
                return FoldOutcome.Folded(Operand.FromInt32(index));
            }
        }

        return FoldOutcome.Folded(Operand.FromInt32(-1));
    }

    /// <summary>The dictionary instance surface; not-arithmetic falls through to the shared sequence one.</summary>
    private static FoldOutcome DispatchImmutableDictionaryInstance(
        Operand receiver,
        SequencePayload payload,
        string name,
        List<Operand> arguments)
    {
        var kind = payload.Collection;
        var items = payload.Items;
        switch (name, arguments)
        {
            case ("Add", [{ } addKey, { } addValue]):
            {
                var found = FindPairByKey(items, addKey);
                if (found.Disposition != FoldDisposition.Folded)
                {
                    return found;
                }

                if (found.Operand.Int32 >= 0)
                {
                    // The exact Add semantics: an equal value answers this instance; a different one throws.
                    var same = OperandsEqual(PayloadOfPair(items[found.Operand.Int32]).Value, addValue);
                    if (same.Disposition != FoldDisposition.Folded)
                    {
                        return same;
                    }

                    return same.Operand.Boolean
                        ? FoldOutcome.Folded(receiver)
                        : FoldOutcome.Error(
                            "System.ArgumentException",
                            "An item with the same key has already been added. Key: "
                            + RenderCompoundOrElement(addKey));
                }

                return CreateImmutableDictionary(
                    kind,
                    items.Add(Operand.FromKeyValuePair(new KeyValuePairPayload(addKey, addValue))),
                    payload.DisplayName);
            }

            case ("SetItem", [{ } setKey, { } setValue]):
            {
                var found = FindPairByKey(items, setKey);
                if (found.Disposition != FoldDisposition.Folded)
                {
                    return found;
                }

                var pair = Operand.FromKeyValuePair(new KeyValuePairPayload(setKey, setValue));
                return CreateImmutableDictionary(
                    kind,
                    found.Operand.Int32 < 0
                        ? items.Add(pair)
                        : items.SetItem(found.Operand.Int32, pair),
                    payload.DisplayName);
            }

            case ("Remove", [{ } removeKey]):
            {
                var found = FindPairByKey(items, removeKey);
                if (found.Disposition != FoldDisposition.Folded)
                {
                    return found;
                }

                return found.Operand.Int32 < 0
                    ? FoldOutcome.Folded(receiver)
                    : CreateSequence(payload.WithCollection(items.RemoveAt(found.Operand.Int32), kind));
            }

            case ("ContainsKey", [{ } soughtKey]):
            {
                var found = FindPairByKey(items, soughtKey);
                return found.Disposition == FoldDisposition.Folded
                    ? FoldOutcome.Folded(Operand.FromBoolean(found.Operand.Int32 >= 0))
                    : found;
            }

            case ("ContainsValue", [{ } soughtValue]):
                foreach (var item in items)
                {
                    if (CancellationStop() is { } cancelled)
                    {
                        return cancelled;
                    }

                    var equals = OperandsEqual(PayloadOfPair(item).Value, soughtValue);
                    if (equals.Disposition != FoldDisposition.Folded)
                    {
                        return equals;
                    }

                    if (equals.Operand.Boolean)
                    {
                        return FoldOutcome.Folded(Operand.FromBoolean(true));
                    }
                }

                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case ("Clear", []):
                return CreateSequence(payload.WithCollection([], kind));
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    /// <summary>The dictionary properties: Count, IsEmpty, and the Keys and Values projections.</summary>
    private static FoldOutcome DispatchImmutableDictionaryProperty(SequencePayload payload, string member)
    {
        switch (member)
        {
            case "Count":
                return FoldOutcome.Folded(Operand.FromInt32(payload.Items.Length));
            case "IsEmpty":
                return FoldOutcome.Folded(Operand.FromBoolean(payload.Items.IsEmpty));
            case "Keys":
                return DictionaryProjection(payload, wantKeys: true);
            case "Values":
                return DictionaryProjection(payload, wantKeys: false);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    /// <summary>Projects the keys or the values as a plain sequence, in the dictionary's enumeration order.</summary>
    private static FoldOutcome DictionaryProjection(SequencePayload payload, bool wantKeys)
    {
        var projected = ImmutableArray.CreateBuilder<Operand>(payload.Items.Length);
        foreach (var item in payload.Items)
        {
            var pair = PayloadOfPair(item);
            projected.Add(wantKeys ? pair.Key : pair.Value);
        }

        if (projected.Count > 0)
        {
            return CreateInferredSequence(projected.MoveToImmutable());
        }

        // An empty projection names its component type from the dictionary's own spelling.
        var arguments = payload.DisplayName.StartsWith("KeyValuePair<", StringComparison.Ordinal)
            ? payload.DisplayName["KeyValuePair<".Length..^1]
            : "Object, Object";
        var comma = arguments.IndexOf(',', StringComparison.Ordinal);
        var componentName = comma < 0
            ? arguments
            : wantKeys
                ? arguments[..comma].Trim()
                : arguments[(comma + 1)..].Trim();
        return CreateSequence(new SequencePayload([], OperandKind.String, default, componentName));
    }
}
