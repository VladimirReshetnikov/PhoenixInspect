using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// The <c>System.Collections.Immutable</c> surface. An immutable collection is a virtual sequence carrying its
/// real type identity: the factories (<c>Create</c>, <c>CreateRange</c>, <c>Empty</c>), the <c>ToImmutable*</c>
/// extensions, and the persistent instance operations — every mutator returns a new collection, exactly as the
/// BCL defines — all fold deterministically over the evaluator's element domains. Set enumeration is modeled as
/// first-insertion order (the BCL leaves hash-set order unspecified); a sorted set orders with the evaluator's
/// deterministic comparison, so a culture-sensitive string ordering stays a typed stop.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>Maps a bare immutable-collection type name to its collection kind.</summary>
    private static bool TryMapImmutableCollectionName(string name, out SequenceCollectionKind kind)
    {
        switch (name)
        {
            case "ImmutableArray":
                kind = SequenceCollectionKind.ImmutableArray;
                return true;
            case "ImmutableList":
                kind = SequenceCollectionKind.ImmutableList;
                return true;
            case "ImmutableHashSet":
                kind = SequenceCollectionKind.ImmutableHashSet;
                return true;
            case "ImmutableSortedSet":
                kind = SequenceCollectionKind.ImmutableSortedSet;
                return true;
            case "ImmutableQueue":
                kind = SequenceCollectionKind.ImmutableQueue;
                return true;
            case "ImmutableStack":
                kind = SequenceCollectionKind.ImmutableStack;
                return true;
            case "ImmutableDictionary":
                kind = SequenceCollectionKind.ImmutableDictionary;
                return true;
            case "ImmutableSortedDictionary":
                kind = SequenceCollectionKind.ImmutableSortedDictionary;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>Gets whether the collection kind is one of the two set kinds.</summary>
    private static bool IsSetCollection(SequenceCollectionKind kind) =>
        kind is SequenceCollectionKind.ImmutableHashSet or SequenceCollectionKind.ImmutableSortedSet;

    /// <summary>Gets whether the collection kind carries an indexer, as the BCL types do.</summary>
    private static bool HasIndexer(SequenceCollectionKind kind) =>
        kind is SequenceCollectionKind.Array or SequenceCollectionKind.ImmutableArray
            or SequenceCollectionKind.ImmutableList or SequenceCollectionKind.ImmutableSortedSet;

    /// <summary>
    /// Applies a kind's invariant to rebuilt items and tags the result: a hash set keeps distinct elements in
    /// first-insertion order, a sorted set keeps them distinct and ordered, and the other kinds keep the items
    /// as given.
    /// </summary>
    private static FoldOutcome NormalizeImmutable(
        SequencePayload source,
        ImmutableArray<Operand> items,
        SequenceCollectionKind kind)
    {
        if (IsSetCollection(kind))
        {
            var distinct = SequenceDistinct(source.With(items));
            if (distinct.Disposition != FoldDisposition.Folded)
            {
                return distinct;
            }

            items = PayloadOf(distinct.Operand).Items;
            if (kind == SequenceCollectionKind.ImmutableSortedSet && items.Length > 1)
            {
                var ordered = SequenceOrder(source.With(items), descending: false);
                if (ordered.Disposition != FoldDisposition.Folded)
                {
                    return ordered;
                }

                items = PayloadOf(ordered.Operand).Items;
            }
        }

        return CreateSequence(source.WithCollection(items, kind));
    }

    // ---- Factories --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Folds <c>ImmutableX.Empty</c> spelled through the generic receiver — <c>ImmutableArray&lt;int&gt;.Empty</c>
    /// — resolving the written element type exactly as array creation does.
    /// </summary>
    private static FoldOutcome DispatchImmutableEmpty(TypeReceiver receiver, FoldContext context)
    {
        if (IsDictionaryCollection(receiver.Collection))
        {
            if (receiver.GenericElement is not { } keyType || receiver.GenericValue is not { } valueSyntax)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    $"'{receiver.Collection}.Empty' needs its type arguments: write "
                    + $"'{receiver.Collection}<TKey, TValue>.Empty'.");
            }

            if (!TryResolveElementType(keyType, context, out var keyDescriptor, out var keyError))
            {
                return keyError ?? FoldOutcome.Error(
                    OperandTypeCode,
                    "The key type is outside the evaluator's modeled element domains.");
            }

            if (!TryResolveElementType(valueSyntax, context, out var valueDescriptor, out var valueError))
            {
                return valueError ?? FoldOutcome.Error(
                    OperandTypeCode,
                    "The value type is outside the evaluator's modeled element domains.");
            }

            return CreateImmutableDictionary(
                receiver.Collection,
                [],
                $"KeyValuePair<{keyDescriptor.DisplayName}, {valueDescriptor.DisplayName}>");
        }

        if (receiver.GenericElement is not { } elementType)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{receiver.Collection}.Empty' needs its element type: write "
                + $"'{receiver.Collection}<T>.Empty'.");
        }

        if (!TryResolveElementType(elementType, context, out var descriptor, out var elementError))
        {
            return elementError ?? FoldOutcome.Error(
                OperandTypeCode,
                "The element type is outside the evaluator's modeled element domains.");
        }

        return CreateSequence(descriptor.Payload([]).WithCollection([], receiver.Collection));
    }

    /// <summary>One static factory invocation over an immutable-collection receiver.</summary>
    private static FoldOutcome DispatchImmutableFactory(
        TypeReceiver receiver,
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext? context)
    {
        var kind = receiver.Collection;
        if (name is not ("Create" or "CreateRange"))
        {
            return MemberUnsupported($"{kind}.{name}");
        }

        if (typeArguments.Count > 1)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{kind}.{name}' takes one element type argument.");
        }

        ElementDescriptor? declared = null;
        if (typeArguments is [{ } declaredElement])
        {
            if (context is null)
            {
                return MemberUnsupported($"{kind}.{name} via reflection without an evaluation context");
            }

            if (!TryResolveElementType(declaredElement, context, out var descriptor, out var declaredError))
            {
                return declaredError ?? FoldOutcome.Error(
                    OperandTypeCode,
                    "The element type is outside the evaluator's modeled element domains.");
            }

            declared = descriptor;
        }

        ImmutableArray<Operand> source;
        SequencePayload? shape = null;
        if (name == "CreateRange")
        {
            if (arguments is not [{ Kind: OperandKind.Sequence } range])
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    $"'{kind}.CreateRange' takes one sequence argument.");
            }

            shape = PayloadOf(range);
            source = shape.Items;
        }
        else
        {
            source = [.. arguments];
        }

        // A stack's enumeration order is newest-first: Create and CreateRange push in argument order, so the
        // last element is the top, exactly as the BCL enumerates.
        if (kind == SequenceCollectionKind.ImmutableStack)
        {
            source = [.. source.Reverse()];
        }

        if (declared is { } declaredDescriptor)
        {
            if (source.IsEmpty)
            {
                return CreateSequence(declaredDescriptor.Payload([]).WithCollection([], kind));
            }

            var inferredForConversion = CreateInferredSequence(source);
            if (inferredForConversion.Disposition != FoldDisposition.Folded)
            {
                return inferredForConversion;
            }

            var converted = ConvertSequenceToElement(inferredForConversion.Operand, declaredDescriptor);
            if (converted.Disposition != FoldDisposition.Folded)
            {
                return converted;
            }

            var payload = PayloadOf(converted.Operand);
            return NormalizeImmutable(payload, payload.Items, kind);
        }

        if (source.IsEmpty)
        {
            // With no elements and no type argument there is nothing to infer, exactly as C# cannot infer one.
            if (shape is not null)
            {
                return NormalizeImmutable(shape, source, kind);
            }

            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{kind}.Create()' with no arguments needs an element type: write '{kind}.Create<T>()'.");
        }

        var inferred = CreateInferredSequence(source);
        if (inferred.Disposition != FoldDisposition.Folded)
        {
            return inferred;
        }

        var inferredPayload = PayloadOf(inferred.Operand);
        return NormalizeImmutable(inferredPayload, inferredPayload.Items, kind);
    }

    // ---- The persistent instance surface ----------------------------------------------------------------------------

    /// <summary>
    /// One instance member over an immutable collection: the persistent operations every kind defines, with
    /// every mutator returning a new collection. A member outside this surface stays not-arithmetic so the
    /// shared sequence surface — the whole LINQ family — keeps answering.
    /// </summary>
    private static FoldOutcome DispatchImmutableInstance(
        Operand receiver,
        SequencePayload payload,
        string name,
        List<Operand> arguments)
    {
        var kind = payload.Collection;
        if (IsDictionaryCollection(kind))
        {
            return DispatchImmutableDictionaryInstance(receiver, payload, name, arguments);
        }

        var items = payload.Items;
        var listLike = kind is SequenceCollectionKind.ImmutableArray or SequenceCollectionKind.ImmutableList;
        switch (name, arguments)
        {
            case ("Add", [{ } added]) when listLike:
                return NormalizeImmutable(payload, items.Add(added), kind);
            case ("Add", [{ } member]) when IsSetCollection(kind):
                return NormalizeImmutable(payload, items.Add(member), kind);
            case ("AddRange", [{ Kind: OperandKind.Sequence } range]) when listLike:
                return NormalizeImmutable(payload, items.AddRange(PayloadOf(range).Items), kind);
            case ("Insert", [{ } at, { } inserted]) when listLike && TryImplicitInt32(at, out var insertAt):
                return insertAt >= 0 && insertAt <= items.Length
                    ? NormalizeImmutable(payload, items.Insert(insertAt, inserted), kind)
                    : ImmutableIndexOutOfRange(insertAt, items.Length);
            case ("InsertRange", [{ } at, { Kind: OperandKind.Sequence } range])
                when listLike && TryImplicitInt32(at, out var insertRangeAt):
                return insertRangeAt >= 0 && insertRangeAt <= items.Length
                    ? NormalizeImmutable(
                        payload, items.InsertRange(insertRangeAt, PayloadOf(range).Items), kind)
                    : ImmutableIndexOutOfRange(insertRangeAt, items.Length);
            case ("RemoveAt", [{ } at]) when listLike && TryImplicitInt32(at, out var removeAt):
                return removeAt >= 0 && removeAt < items.Length
                    ? NormalizeImmutable(payload, items.RemoveAt(removeAt), kind)
                    : ImmutableIndexOutOfRange(removeAt, items.Length);
            case ("RemoveRange", [{ } at, { } count]) when listLike &&
                TryImplicitInt32(at, out var removeFrom) && TryImplicitInt32(count, out var removeCount):
                return removeFrom >= 0 && removeCount >= 0 && removeFrom + removeCount <= items.Length
                    ? NormalizeImmutable(payload, items.RemoveRange(removeFrom, removeCount), kind)
                    : ImmutableIndexOutOfRange(removeFrom, items.Length);
            case ("SetItem", [{ } at, { } replacement]) when listLike && TryImplicitInt32(at, out var setAt):
                return setAt >= 0 && setAt < items.Length
                    ? NormalizeImmutable(payload, items.SetItem(setAt, replacement), kind)
                    : ImmutableIndexOutOfRange(setAt, items.Length);
            case ("Remove", [{ } removed]) when listLike || IsSetCollection(kind):
            {
                var index = ImmutableIndexOf(items, removed, fromEnd: false);
                if (index.Disposition != FoldDisposition.Folded)
                {
                    return index;
                }

                // An absent element leaves the collection unchanged, exactly as the BCL returns this instance.
                return index.Operand.Int32 < 0
                    ? FoldOutcome.Folded(receiver)
                    : NormalizeImmutable(payload, items.RemoveAt(index.Operand.Int32), kind);
            }

            case ("IndexOf" or "LastIndexOf", [{ } sought])
                when listLike || kind == SequenceCollectionKind.ImmutableSortedSet:
                return name == "IndexOf"
                    ? ImmutableIndexOf(items, sought, fromEnd: false)
                    : ImmutableIndexOf(items, sought, fromEnd: true);
            case ("Clear", []):
                return CreateSequence(payload.WithCollection([], kind));
            case ("Sort", []) when kind == SequenceCollectionKind.ImmutableList:
            {
                var sorted = SequenceOrder(payload, descending: false);
                return sorted.Disposition == FoldDisposition.Folded
                    ? CreateSequence(payload.WithCollection(PayloadOf(sorted.Operand).Items, kind))
                    : sorted;
            }

            case ("Reverse", []) when kind == SequenceCollectionKind.ImmutableList:
                return CreateSequence(payload.WithCollection([.. items.Reverse()], kind));
            case ("Enqueue", [{ } enqueued]) when kind == SequenceCollectionKind.ImmutableQueue:
                return NormalizeImmutable(payload, items.Add(enqueued), kind);
            case ("Push", [{ } pushed]) when kind == SequenceCollectionKind.ImmutableStack:
                return NormalizeImmutable(payload, items.Insert(0, pushed), kind);
            case ("Dequeue", []) when kind == SequenceCollectionKind.ImmutableQueue:
            case ("Pop", []) when kind == SequenceCollectionKind.ImmutableStack:
                return items.IsEmpty
                    ? EmptyImmutable()
                    : NormalizeImmutable(payload, items.RemoveAt(0), kind);
            case ("Peek", []) when kind is SequenceCollectionKind.ImmutableQueue
                or SequenceCollectionKind.ImmutableStack:
                return items.IsEmpty ? EmptyImmutable() : FoldOutcome.Folded(items[0]);
            case ("Union", [{ Kind: OperandKind.Sequence } other]) when IsSetCollection(kind):
                return NormalizeImmutable(payload, items.AddRange(PayloadOf(other).Items), kind);
            case ("Intersect" or "Except", [{ Kind: OperandKind.Sequence } other]) when IsSetCollection(kind):
            {
                var filtered = SequenceSetOperation(payload, PayloadOf(other), keepMembers: name == "Intersect");
                return filtered.Disposition == FoldDisposition.Folded
                    ? NormalizeImmutable(payload, PayloadOf(filtered.Operand).Items, kind)
                    : filtered;
            }

            case ("SymmetricExcept", [{ Kind: OperandKind.Sequence } other]) when IsSetCollection(kind):
            {
                var otherPayload = PayloadOf(other);
                var mine = SequenceSetOperation(payload, otherPayload, keepMembers: false);
                if (mine.Disposition != FoldDisposition.Folded)
                {
                    return mine;
                }

                var theirs = SequenceSetOperation(otherPayload, payload, keepMembers: false);
                if (theirs.Disposition != FoldDisposition.Folded)
                {
                    return theirs;
                }

                return NormalizeImmutable(
                    payload,
                    PayloadOf(mine.Operand).Items.AddRange(PayloadOf(theirs.Operand).Items),
                    kind);
            }

            case ("SetEquals" or "Overlaps" or "IsSubsetOf" or "IsSupersetOf" or "IsProperSubsetOf"
                or "IsProperSupersetOf", [{ Kind: OperandKind.Sequence } other]) when IsSetCollection(kind):
                return ImmutableSetPredicate(payload, PayloadOf(other), name);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    /// <summary>
    /// One instance property over a sequence: an ordinary array answers the array trio, and an immutable
    /// collection answers exactly the properties its BCL type declares.
    /// </summary>
    private static FoldOutcome DispatchSequenceProperty(Operand receiver, string member)
    {
        var payload = PayloadOf(receiver);
        if (payload.Collection != SequenceCollectionKind.Array)
        {
            return DispatchImmutableProperty(payload, member);
        }

        return member switch
        {
            "Length" => FoldOutcome.Folded(Operand.FromInt32(payload.Items.Length)),
            "LongLength" => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Int64, (long)payload.Items.Length)),
            "Rank" => FoldOutcome.Folded(Operand.FromInt32(payload.Rank)),
            _ => FoldOutcome.NotArithmetic(),
        };
    }

    /// <summary>The instance properties the immutable kinds expose, per kind, as the BCL declares them.</summary>
    private static FoldOutcome DispatchImmutableProperty(SequencePayload payload, string member)
    {
        var kind = payload.Collection;
        if (IsDictionaryCollection(kind))
        {
            return DispatchImmutableDictionaryProperty(payload, member);
        }

        switch (member)
        {
            case "Length" when kind == SequenceCollectionKind.ImmutableArray:
                return FoldOutcome.Folded(Operand.FromInt32(payload.Items.Length));
            case "Count" when kind is SequenceCollectionKind.ImmutableList
                or SequenceCollectionKind.ImmutableHashSet or SequenceCollectionKind.ImmutableSortedSet:
                return FoldOutcome.Folded(Operand.FromInt32(payload.Items.Length));
            case "IsEmpty":
                return FoldOutcome.Folded(Operand.FromBoolean(payload.Items.IsEmpty));
            // The evaluator only ever builds initialized arrays, so a folded ImmutableArray is never default.
            case "IsDefault" when kind == SequenceCollectionKind.ImmutableArray:
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case "IsDefaultOrEmpty" when kind == SequenceCollectionKind.ImmutableArray:
                return FoldOutcome.Folded(Operand.FromBoolean(payload.Items.IsEmpty));
            case "Min" when kind == SequenceCollectionKind.ImmutableSortedSet:
                return FoldOutcome.Folded(payload.Items.IsEmpty
                    ? DefaultOfElement(payload)
                    : payload.Items[0]);
            case "Max" when kind == SequenceCollectionKind.ImmutableSortedSet:
                return FoldOutcome.Folded(payload.Items.IsEmpty
                    ? DefaultOfElement(payload)
                    : payload.Items[^1]);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    /// <summary>Answers one set-relation predicate, treating both operands as element sets.</summary>
    private static FoldOutcome ImmutableSetPredicate(SequencePayload mine, SequencePayload theirs, string name)
    {
        var distinctTheirs = SequenceDistinct(theirs);
        if (distinctTheirs.Disposition != FoldDisposition.Folded)
        {
            return distinctTheirs;
        }

        var mineItems = mine.Items;
        var theirItems = PayloadOf(distinctTheirs.Operand).Items;
        var mineInTheirs = 0;
        foreach (var item in mineItems)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var index = ImmutableIndexOf(theirItems, item, fromEnd: false);
            if (index.Disposition != FoldDisposition.Folded)
            {
                return index;
            }

            if (index.Operand.Int32 >= 0)
            {
                mineInTheirs++;
            }
        }

        var theirsInMine = 0;
        foreach (var item in theirItems)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var index = ImmutableIndexOf(mineItems, item, fromEnd: false);
            if (index.Disposition != FoldDisposition.Folded)
            {
                return index;
            }

            if (index.Operand.Int32 >= 0)
            {
                theirsInMine++;
            }
        }

        var result = name switch
        {
            "SetEquals" => mineInTheirs == mineItems.Length && theirsInMine == theirItems.Length,
            "Overlaps" => mineInTheirs > 0,
            "IsSubsetOf" => mineInTheirs == mineItems.Length,
            "IsSupersetOf" => theirsInMine == theirItems.Length,
            "IsProperSubsetOf" => mineInTheirs == mineItems.Length && theirItems.Length > mineItems.Length,
            _ => theirsInMine == theirItems.Length && mineItems.Length > theirItems.Length,
        };
        return FoldOutcome.Folded(Operand.FromBoolean(result));
    }

    /// <summary>Finds one element by the sequence equality the whole surface shares; −1 when absent.</summary>
    private static FoldOutcome ImmutableIndexOf(ImmutableArray<Operand> items, Operand sought, bool fromEnd)
    {
        var found = -1;
        for (var index = 0; index < items.Length; index++)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var equals = OperandsEqual(items[index], sought);
            if (equals.Disposition != FoldDisposition.Folded)
            {
                return equals;
            }

            if (equals.Operand.Boolean)
            {
                found = index;
                if (!fromEnd)
                {
                    break;
                }
            }
        }

        return FoldOutcome.Folded(Operand.FromInt32(found));
    }

    private static FoldOutcome EmptyImmutable() => FoldOutcome.Error(
        "System.InvalidOperationException",
        "This operation does not apply to an empty instance.");

    private static FoldOutcome ImmutableIndexOutOfRange(int index, int length) => FoldOutcome.Error(
        ArgumentOutOfRangeCode,
        $"Index {index.ToString(CultureInfo.InvariantCulture)} is outside the collection of length "
        + $"{length.ToString(CultureInfo.InvariantCulture)}.");
}
