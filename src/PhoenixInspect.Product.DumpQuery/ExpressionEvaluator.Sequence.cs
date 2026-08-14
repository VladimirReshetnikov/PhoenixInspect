using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Virtual array sequences: values produced by array-creating BCL members, array initializer expressions, and the
/// deterministic <c>System.Linq.Enumerable</c> surface — lambda-free operators here, with the expression-lambda
/// operators in the lambda partial. A sequence exists only while one expression evaluates — it is never persisted
/// and never written back anywhere — and every operation over it is purely functional: each step produces a new
/// sequence and leaves its input untouched.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>The deterministic bound on virtual sequence length; a longer sequence is a typed stop.</summary>
    public const int MaximumSequenceLength = 4096;

    private const string SequenceBoundCode = "EVAL_SEQUENCE_BOUND_EXCEEDED";
    private const string EmptySequenceMessage = "Sequence contains no elements.";

    /// <summary>
    /// The ordering evidence an <c>OrderBy</c> family sort leaves on its result: the per-item key vectors and the
    /// per-level directions, aligned with the sorted items so <c>ThenBy</c> can refine without re-deriving keys.
    /// </summary>
    /// <param name="KeysPerItem">One key vector per item, aligned with the sequence items.</param>
    /// <param name="Descending">The direction of each key level.</param>
    private sealed record SequenceOrdering(
        ImmutableArray<ImmutableArray<Operand>> KeysPerItem,
        ImmutableArray<bool> Descending);

    /// <summary>Describes the element domain of one sequence, so defaults and displays stay typed.</summary>
    private sealed class SequencePayload
    {
        internal SequencePayload(
            ImmutableArray<Operand> items,
            OperandKind elementKind,
            NumericKind elementNumeric,
            string displayName,
            SequenceOrdering? ordering = null,
            ImmutableArray<int> dimensions = default,
            ImmutableArray<int> lowerBounds = default)
        {
            Items = items;
            ElementKind = elementKind;
            ElementNumeric = elementNumeric;
            DisplayName = displayName;
            Ordering = ordering;
            Dimensions = dimensions.IsDefault ? [] : dimensions;
            LowerBounds = lowerBounds.IsDefault ? [] : lowerBounds;
        }

        internal ImmutableArray<Operand> Items { get; }

        internal OperandKind ElementKind { get; }

        internal NumericKind ElementNumeric { get; }

        internal string DisplayName { get; }

        /// <summary>Gets the ordering evidence, present only on the direct result of an OrderBy-family sort.</summary>
        internal SequenceOrdering? Ordering { get; }

        /// <summary>
        /// Gets the rectangular dimension lengths, empty for an ordinary one-dimensional sequence. Items stay a
        /// flat row-major list either way, exactly the order .NET enumerates a rectangular array in, so the whole
        /// sequence surface applies unchanged and Length stays the total element count.
        /// </summary>
        internal ImmutableArray<int> Dimensions { get; }

        /// <summary>Gets the per-dimension lower bounds, empty when every bound is zero.</summary>
        internal ImmutableArray<int> LowerBounds { get; }

        internal int Rank => Dimensions.IsEmpty ? 1 : Dimensions.Length;

        internal int LengthOf(int dimension) =>
            Dimensions.IsEmpty ? Items.Length : Dimensions[dimension];

        internal int LowerBoundOf(int dimension) =>
            LowerBounds.IsEmpty ? 0 : LowerBounds[dimension];

        /// <summary>Gets the C#-style bracket suffix of the sequence's type: <c>[]</c>, <c>[,]</c>, …</summary>
        internal string TypeSuffix =>
            Dimensions.IsEmpty ? "[]" : "[" + new string(',', Dimensions.Length - 1) + "]";

        // Any other operation drops the ordering evidence and the rectangular shape: its items no longer align
        // with the key vectors, and a derived sequence enumerates flat, exactly as LINQ over a .NET array does.
        internal SequencePayload With(ImmutableArray<Operand> items) =>
            new(items, ElementKind, ElementNumeric, DisplayName);

        /// <summary>Reshapes this payload as a rectangular array; the items are already flat row-major.</summary>
        internal SequencePayload WithShape(ImmutableArray<int> dimensions, ImmutableArray<int> lowerBounds) =>
            new(Items, ElementKind, ElementNumeric, DisplayName, ordering: null, dimensions, lowerBounds);
    }

    private static SequencePayload PayloadOf(Operand operand) => (SequencePayload)operand.Box!;

    private static bool TryGetSplitOptions(Operand operand, out StringSplitOptions options)
    {
        options = StringSplitOptions.None;
        if (operand is { Kind: OperandKind.Enum, EnumTypeFullName: "System.StringSplitOptions" } &&
            operand.Int32 is >= 0 and <= 3)
        {
            options = (StringSplitOptions)operand.Int32;
            return true;
        }

        return false;
    }

    private static FoldOutcome CreateSequence(SequencePayload payload) =>
        payload.Items.Length > MaximumSequenceLength
            ? FoldOutcome.Error(
                SequenceBoundCode,
                $"The sequence would hold {payload.Items.Length.ToString(CultureInfo.InvariantCulture)} elements, "
                + $"beyond the deterministic bound of {MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.")
            : FoldOutcome.Folded(Operand.FromSequence(payload));

    private static FoldOutcome CreateCharSequence(char[] characters) => CreateSequence(new SequencePayload(
        [.. characters.Select(Operand.FromChar)],
        OperandKind.Char,
        default,
        "Char"));

    private static FoldOutcome CreateStringSequence(IEnumerable<string?> values) => CreateSequence(new SequencePayload(
        [.. values.Select(static value => value is null ? Operand.Null() : Operand.FromString(value))],
        OperandKind.String,
        default,
        "String"));

    // ---- Array creation expressions ---------------------------------------------------------------------------------

    private static FoldOutcome FoldInitializer(InitializerExpressionSyntax initializer, FoldContext context)
    {
        var items = ImmutableArray.CreateBuilder<Operand>(initializer.Expressions.Count);
        foreach (var expression in initializer.Expressions)
        {
            var folded = Fold(expression, context);
            if (folded.Disposition != FoldDisposition.Folded)
            {
                return folded;
            }

            items.Add(folded.Operand);
        }

        return CreateInferredSequence(items.ToImmutable());
    }

    /// <summary>Infers the element domain of an initializer, mirroring C# best-common-type inference.</summary>
    private static FoldOutcome CreateInferredSequence(ImmutableArray<Operand> items)
    {
        if (items.Length == 0)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "An empty array initializer has no element type to infer.");
        }

        if (items.All(static item => item.Kind is OperandKind.String or OperandKind.Null))
        {
            return CreateSequence(new SequencePayload(items, OperandKind.String, default, "String"));
        }

        if (items.All(static item => item.Kind == OperandKind.Char))
        {
            return CreateSequence(new SequencePayload(items, OperandKind.Char, default, "Char"));
        }

        if (items.All(static item => item.Kind == OperandKind.Boolean))
        {
            return CreateSequence(new SequencePayload(items, OperandKind.Boolean, default, "Boolean"));
        }

        if (items[0].Kind == OperandKind.Temporal &&
            items.All(item => item.Kind == OperandKind.Temporal && item.TemporalKind == items[0].TemporalKind))
        {
            return CreateSequence(new SequencePayload(
                items,
                OperandKind.Temporal,
                default,
                items[0].TemporalKind.ToString()));
        }

        if (items[0].Kind == OperandKind.BclValue &&
            items.All(item => item.Kind == OperandKind.BclValue && item.BclValueKind == items[0].BclValueKind))
        {
            return CreateSequence(new SequencePayload(
                items,
                OperandKind.BclValue,
                default,
                items[0].BclValueKind.ToString()));
        }

        if (items[0].Kind == OperandKind.Enum &&
            items.All(item => item.Kind == OperandKind.Enum &&
                string.Equals(item.EnumTypeFullName, items[0].EnumTypeFullName, StringComparison.Ordinal)))
        {
            var enumSeparator = items[0].EnumTypeFullName!.LastIndexOf('.');
            return CreateSequence(new SequencePayload(
                items,
                OperandKind.Enum,
                items[0].NumericKind,
                enumSeparator < 0 ? items[0].EnumTypeFullName! : items[0].EnumTypeFullName![(enumSeparator + 1)..]));
        }

        if (items.All(static item => item.Kind == OperandKind.Tuple))
        {
            return CreateSequence(new SequencePayload(items, OperandKind.Tuple, default, TupleTypeName(items[0])));
        }

        if (items.All(static item => item.Kind == OperandKind.Anonymous))
        {
            return CreateSequence(new SequencePayload(
                items, OperandKind.Anonymous, default, AnonymousTypeName(items[0])));
        }

        if (items.All(static item => item.Kind == OperandKind.Grouping))
        {
            return CreateSequence(new SequencePayload(items, OperandKind.Grouping, default, "IGrouping"));
        }

        if (items[0].Kind == OperandKind.Delegate &&
            items.All(item => item.Kind == OperandKind.Delegate &&
                string.Equals(
                    DelegatePayloadOf(item).Type.FullName,
                    DelegatePayloadOf(items[0]).Type.FullName,
                    StringComparison.Ordinal)))
        {
            return CreateSequence(new SequencePayload(
                items,
                OperandKind.Delegate,
                default,
                DelegatePayloadOf(items[0]).Type.CSharpName));
        }

        if (items.All(static item => item.Kind == OperandKind.Sequence))
        {
            return CreateSequence(new SequencePayload(
                items, OperandKind.Sequence, default, PayloadOf(items[0]).DisplayName + "[]"));
        }

        if (items.All(static item => item.IsNumeric && item.Kind != OperandKind.Enum))
        {
            var common = NumericKindOf(items[0]);
            foreach (var item in items.Skip(1))
            {
                if (!TryPromote(
                    Operand.FromNumeric(common, BoxFromBigInteger(common, 0)),
                    item,
                    out common,
                    out var promoteError))
                {
                    return promoteError;
                }
            }

            var converted = ImmutableArray.CreateBuilder<Operand>(items.Length);
            foreach (var item in items)
            {
                var conversion = ConvertNumericOperand(item, common);
                if (conversion.Disposition != FoldDisposition.Folded)
                {
                    return conversion;
                }

                converted.Add(conversion.Operand);
            }

            return CreateSequence(new SequencePayload(
                converted.ToImmutable(),
                common == NumericKind.Int32 ? OperandKind.Int32 : OperandKind.Numeric,
                common,
                common.ToString()));
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "Array elements must share one domain: numeric, string, char, or Boolean.");
    }

    // ---- Element access and members ---------------------------------------------------------------------------------

    private static FoldOutcome FoldSequenceElementAccess(
        Operand receiver,
        ElementAccessExpressionSyntax elementAccess,
        FoldContext context)
    {
        var payload = PayloadOf(receiver);
        if (!payload.Dimensions.IsEmpty || elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return FoldRectangularElementAccess(payload, elementAccess, context);
        }

        var argument = elementAccess.ArgumentList.Arguments[0].Expression;
        var length = payload.Items.Length;
        if (argument is RangeExpressionSyntax range)
        {
            var start = ResolveIndex(range.LeftOperand, length, defaultOffset: 0, context);
            if (start.Disposition != FoldDisposition.Folded)
            {
                return start;
            }

            var end = ResolveIndex(range.RightOperand, length, defaultOffset: length, context);
            if (end.Disposition != FoldDisposition.Folded)
            {
                return end;
            }

            var startOffset = start.Operand.Int32;
            var endOffset = end.Operand.Int32;
            if (startOffset < 0 || endOffset > length || startOffset > endOffset)
            {
                return FoldOutcome.Error(
                    ArgumentOutOfRangeCode,
                    $"Range [{startOffset.ToString(CultureInfo.InvariantCulture)}.."
                    + $"{endOffset.ToString(CultureInfo.InvariantCulture)}] is outside the array of length "
                    + $"{length.ToString(CultureInfo.InvariantCulture)}.");
            }

            return CreateSequence(payload.With(payload.Items[startOffset..endOffset]));
        }

        var index = ResolveIndex(argument, length, defaultOffset: 0, context);
        if (index.Disposition != FoldDisposition.Folded)
        {
            return index;
        }

        var position = index.Operand.Int32;
        if (position < 0 || position >= length)
        {
            return FoldOutcome.Error(
                "System.IndexOutOfRangeException",
                $"Index {position.ToString(CultureInfo.InvariantCulture)} is outside the array of length "
                + $"{length.ToString(CultureInfo.InvariantCulture)}.");
        }

        return FoldOutcome.Folded(payload.Items[position]);
    }

    private static FoldOutcome DimensionOutOfRange(SequencePayload payload) => FoldOutcome.Error(
        "System.IndexOutOfRangeException",
        $"This array has rank {payload.Rank.ToString(CultureInfo.InvariantCulture)}; dimensions 0 through "
        + $"{(payload.Rank - 1).ToString(CultureInfo.InvariantCulture)} exist.");

    /// <summary>Faithful <see cref="Array.GetValue(int[])"/> over a rectangular array's indices.</summary>
    private static FoldOutcome RectangularGetValue(SequencePayload payload, List<Operand> arguments)
    {
        var rank = payload.Rank;
        if (arguments.Count != rank)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"This array has rank {rank.ToString(CultureInfo.InvariantCulture)}; GetValue takes "
                + $"{rank.ToString(CultureInfo.InvariantCulture)} index(es).");
        }

        var offset = 0;
        for (var dimension = 0; dimension < rank; dimension++)
        {
            if (!TryImplicitInt32(arguments[dimension], out var index))
            {
                return FoldOutcome.Error(OperandTypeCode, "An array index must be an exact Int32 value.");
            }

            var lower = payload.LowerBoundOf(dimension);
            var length = payload.LengthOf(dimension);
            if (index < lower || index >= lower + length)
            {
                return FoldOutcome.Error(
                    "System.IndexOutOfRangeException",
                    $"Index {index.ToString(CultureInfo.InvariantCulture)} is outside dimension "
                    + $"{dimension.ToString(CultureInfo.InvariantCulture)} with bounds "
                    + $"[{lower.ToString(CultureInfo.InvariantCulture)}.."
                    + $"{(lower + length - 1).ToString(CultureInfo.InvariantCulture)}].");
            }

            offset = (offset * length) + (index - lower);
        }

        return FoldOutcome.Folded(payload.Items[offset]);
    }

    /// <summary>
    /// Element access over a rectangular array: one index per dimension, each honoring its dimension's lower
    /// bound, flattened row-major. Ranges apply only to one-dimensional arrays, exactly as C# defines.
    /// </summary>
    private static FoldOutcome FoldRectangularElementAccess(
        SequencePayload payload,
        ElementAccessExpressionSyntax elementAccess,
        FoldContext context)
    {
        var rank = payload.Rank;
        if (elementAccess.ArgumentList.Arguments.Count != rank)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"This array has rank {rank.ToString(CultureInfo.InvariantCulture)}; element access takes "
                + $"{rank.ToString(CultureInfo.InvariantCulture)} index(es).");
        }

        var offset = 0;
        for (var dimension = 0; dimension < rank; dimension++)
        {
            var argument = elementAccess.ArgumentList.Arguments[dimension].Expression;
            if (argument is RangeExpressionSyntax)
            {
                return FoldOutcome.Error(
                    OperandTypeCode, "Range access applies to one-dimensional arrays only.");
            }

            var folded = Fold(argument, context);
            if (folded.Disposition != FoldDisposition.Folded)
            {
                return folded;
            }

            if (!TryImplicitInt32(folded.Operand, out var index))
            {
                return FoldOutcome.Error(OperandTypeCode, "An array index must be an exact Int32 value.");
            }

            var lower = payload.LowerBoundOf(dimension);
            var length = payload.LengthOf(dimension);
            if (index < lower || index >= lower + length)
            {
                return FoldOutcome.Error(
                    "System.IndexOutOfRangeException",
                    $"Index {index.ToString(CultureInfo.InvariantCulture)} is outside dimension "
                    + $"{dimension.ToString(CultureInfo.InvariantCulture)} with bounds "
                    + $"[{lower.ToString(CultureInfo.InvariantCulture)}.."
                    + $"{(lower + length - 1).ToString(CultureInfo.InvariantCulture)}].");
            }

            offset = (offset * length) + (index - lower);
        }

        return FoldOutcome.Folded(payload.Items[offset]);
    }

    // ---- The lambda-free Enumerable surface -------------------------------------------------------------------------

    private static FoldOutcome DispatchSequence(Operand receiver, string name, List<Operand> arguments)
    {
        var payload = PayloadOf(receiver);
        var items = payload.Items;
        switch (name)
        {
            case "Count" when arguments.Count == 0:
                return FoldOutcome.Folded(Operand.FromInt32(items.Length));
            case "LongCount" when arguments.Count == 0:
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, (long)items.Length));
            case "Any" when arguments.Count == 0:
                return FoldOutcome.Folded(Operand.FromBoolean(items.Length > 0));
            case "Contains" when arguments is [{ } probe]:
                foreach (var item in items)
                {
                    var equals = OperandsEqual(item, probe);
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
            case "First" when arguments.Count == 0:
                return items.Length > 0
                    ? FoldOutcome.Folded(items[0])
                    : EmptySequence();
            case "Last" when arguments.Count == 0:
                return items.Length > 0
                    ? FoldOutcome.Folded(items[^1])
                    : EmptySequence();
            case "FirstOrDefault" when arguments.Count == 0:
                return FoldOutcome.Folded(items.Length > 0 ? items[0] : DefaultOfElement(payload));
            case "LastOrDefault" when arguments.Count == 0:
                return FoldOutcome.Folded(items.Length > 0 ? items[^1] : DefaultOfElement(payload));
            case "Single" when arguments.Count == 0:
                return items.Length switch
                {
                    1 => FoldOutcome.Folded(items[0]),
                    0 => EmptySequence(),
                    _ => FoldOutcome.Error(
                        "System.InvalidOperationException",
                        "Sequence contains more than one element."),
                };
            case "SingleOrDefault" when arguments.Count == 0:
                return items.Length switch
                {
                    1 => FoldOutcome.Folded(items[0]),
                    0 => FoldOutcome.Folded(DefaultOfElement(payload)),
                    _ => FoldOutcome.Error(
                        "System.InvalidOperationException",
                        "Sequence contains more than one element."),
                };
            case "GetValue" when !payload.Dimensions.IsEmpty || arguments.Count > 1:
                return RectangularGetValue(payload, arguments);
            case "GetValue" when arguments is [{ } valueAt] && TryImplicitInt32(valueAt, out var valueIndex):
                return valueIndex >= 0 && valueIndex < items.Length
                    ? FoldOutcome.Folded(items[valueIndex])
                    : FoldOutcome.Error(
                        "System.IndexOutOfRangeException",
                        $"Index {valueIndex.ToString(CultureInfo.InvariantCulture)} is outside the array of "
                        + $"length {items.Length.ToString(CultureInfo.InvariantCulture)}.");
            case "GetLength" when arguments is [{ } lengthDimension] &&
                TryImplicitInt32(lengthDimension, out var lengthOf):
                return lengthOf >= 0 && lengthOf < payload.Rank
                    ? FoldOutcome.Folded(Operand.FromInt32(payload.LengthOf(lengthOf)))
                    : DimensionOutOfRange(payload);
            case "GetLowerBound" when arguments is [{ } lowerDimension] &&
                TryImplicitInt32(lowerDimension, out var lowerOf):
                return lowerOf >= 0 && lowerOf < payload.Rank
                    ? FoldOutcome.Folded(Operand.FromInt32(payload.LowerBoundOf(lowerOf)))
                    : DimensionOutOfRange(payload);
            case "GetUpperBound" when arguments is [{ } upperDimension] &&
                TryImplicitInt32(upperDimension, out var upperOf):
                return upperOf >= 0 && upperOf < payload.Rank
                    ? FoldOutcome.Folded(
                        Operand.FromInt32(payload.LowerBoundOf(upperOf) + payload.LengthOf(upperOf) - 1))
                    : DimensionOutOfRange(payload);
            case "ElementAt" when arguments is [{ } at] && TryImplicitInt32(at, out var index):
                return index >= 0 && index < items.Length
                    ? FoldOutcome.Folded(items[index])
                    : FoldOutcome.Error(
                        ArgumentOutOfRangeCode,
                        $"Index {index.ToString(CultureInfo.InvariantCulture)} is outside the sequence of length "
                        + $"{items.Length.ToString(CultureInfo.InvariantCulture)}.");
            case "ElementAtOrDefault" when arguments is [{ } at] && TryImplicitInt32(at, out var orDefault):
                return FoldOutcome.Folded(orDefault >= 0 && orDefault < items.Length
                    ? items[orDefault]
                    : DefaultOfElement(payload));
            case "Skip" when arguments is [{ } by] && TryImplicitInt32(by, out var skip):
                return CreateSequence(payload.With(items[Math.Clamp(skip, 0, items.Length)..]));
            case "Take" when arguments is [{ } by] && TryImplicitInt32(by, out var take):
                return CreateSequence(payload.With(items[..Math.Clamp(take, 0, items.Length)]));
            case "SkipLast" when arguments is [{ } by] && TryImplicitInt32(by, out var skipLast):
                return CreateSequence(payload.With(items[..(items.Length - Math.Clamp(skipLast, 0, items.Length))]));
            case "TakeLast" when arguments is [{ } by] && TryImplicitInt32(by, out var takeLast):
                return CreateSequence(payload.With(items[(items.Length - Math.Clamp(takeLast, 0, items.Length))..]));
            case "Reverse" when arguments.Count == 0:
                return CreateSequence(payload.With([.. items.Reverse()]));
            case "Distinct" when arguments.Count == 0:
                return SequenceDistinct(payload);
            case "Append" when arguments is [{ } appended]:
                return CreateSequence(payload.With(items.Add(appended)));
            case "Prepend" when arguments is [{ } prepended]:
                return CreateSequence(payload.With(items.Insert(0, prepended)));
            case "Concat" when arguments is [{ Kind: OperandKind.Sequence } other]:
                return CreateSequence(payload.With(items.AddRange(PayloadOf(other).Items)));
            case "Union" when arguments is [{ Kind: OperandKind.Sequence } other]:
                return SequenceDistinct(payload.With(items.AddRange(PayloadOf(other).Items)));
            case "Except" when arguments is [{ Kind: OperandKind.Sequence } other]:
                return SequenceSetOperation(payload, PayloadOf(other), keepMembers: false);
            case "Intersect" when arguments is [{ Kind: OperandKind.Sequence } other]:
                return SequenceSetOperation(payload, PayloadOf(other), keepMembers: true);
            case "SequenceEqual" when arguments is [{ Kind: OperandKind.Sequence } other]:
                return SequenceEquals(items, PayloadOf(other).Items);
            case "Order" when arguments.Count == 0:
                return SequenceOrder(payload, descending: false);
            case "OrderDescending" when arguments.Count == 0:
                return SequenceOrder(payload, descending: true);
            case "Sum" when arguments.Count == 0:
                return SequenceSum(payload);
            case "Min" when arguments.Count == 0:
                return SequenceMinMax(payload, wantMin: true);
            case "Max" when arguments.Count == 0:
                return SequenceMinMax(payload, wantMin: false);
            case "Average" when arguments.Count == 0:
                return SequenceAverage(payload);
            case "ToArray" when arguments.Count == 0:
            case "ToList" when arguments.Count == 0:
                // A virtual sequence is already materialized and immutable, so both are the identity.
                return FoldOutcome.Folded(receiver);
            default:
                return MemberUnsupported(name);
        }
    }

    private static FoldOutcome EmptySequence() =>
        FoldOutcome.Error("System.InvalidOperationException", EmptySequenceMessage);

    private static Operand DefaultOfElement(SequencePayload payload) => payload.ElementKind switch
    {
        OperandKind.String => Operand.Null(),
        OperandKind.Char => Operand.FromChar('\0'),
        OperandKind.Boolean => Operand.FromBoolean(false),
        OperandKind.Numeric => Operand.FromNumeric(payload.ElementNumeric, BoxFromBigInteger(
            payload.ElementNumeric,
            0)),
        OperandKind.Enum when payload.Items.Length > 0 => Operand.FromEnum(
            0,
            payload.Items[0].NumericKind,
            payload.Items[0].EnumTypeFullName!,
            "0"),
        // Anonymous types, groupings, and nested sequences are reference-shaped; their default is null. A tuple's
        // true default would be a zeroed tuple, but with no elements observed there is no shape to zero.
        OperandKind.Tuple or OperandKind.Anonymous or OperandKind.Grouping or OperandKind.Sequence =>
            Operand.Null(),
        _ => Operand.FromInt32(0),
    };

    /// <summary>Ordinal element equality: the same semantics EqualityComparer&lt;T&gt;.Default applies.</summary>
    private static FoldOutcome OperandsEqual(Operand left, Operand right)
    {
        if (left.Kind == OperandKind.Null || right.Kind == OperandKind.Null)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(
                left.Kind == OperandKind.Null && right.Kind == OperandKind.Null));
        }

        if (left.Kind == OperandKind.String && right.Kind == OperandKind.String)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(
                string.Equals(left.String, right.String, StringComparison.Ordinal)));
        }

        if (left.Kind == OperandKind.Boolean && right.Kind == OperandKind.Boolean)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(left.Boolean == right.Boolean));
        }

        if (left.Kind == OperandKind.Type && right.Kind == OperandKind.Type)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(string.Equals(
                ((TypeRef)left.Box!).FullName,
                ((TypeRef)right.Box!).FullName,
                StringComparison.Ordinal)));
        }

        if (left.Kind == OperandKind.Tuple && right.Kind == OperandKind.Tuple)
        {
            return TuplesEqual(left, right);
        }

        if (left.Kind == OperandKind.Anonymous && right.Kind == OperandKind.Anonymous)
        {
            return AnonymousEquals(left, right);
        }

        if (TryCompareTemporal(left, right, out var temporalComparison))
        {
            return FoldOutcome.Folded(Operand.FromBoolean(temporalComparison == 0));
        }

        if (TryCompareBclValue(left, right, out var valueComparison))
        {
            return FoldOutcome.Folded(Operand.FromBoolean(valueComparison == 0));
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            return ComputeNumericComparison(SyntaxKind.EqualsExpression, left, right);
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "Elements can only be compared within one domain: numeric, string, char, Boolean, or one date/time kind.");
    }

    private static FoldOutcome SequenceDistinct(SequencePayload payload)
    {
        var kept = ImmutableArray.CreateBuilder<Operand>();
        foreach (var item in payload.Items)
        {
            // The quadratic comparison loops never re-enter Fold, so each outer element is its own boundary.
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var seen = false;
            foreach (var existing in kept)
            {
                var equals = OperandsEqual(existing, item);
                if (equals.Disposition != FoldDisposition.Folded)
                {
                    return equals;
                }

                if (equals.Operand.Boolean)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                kept.Add(item);
            }
        }

        return CreateSequence(payload.With(kept.ToImmutable()));
    }

    private static FoldOutcome SequenceSetOperation(SequencePayload left, SequencePayload right, bool keepMembers)
    {
        var distinct = SequenceDistinct(left);
        if (distinct.Disposition != FoldDisposition.Folded)
        {
            return distinct;
        }

        var kept = ImmutableArray.CreateBuilder<Operand>();
        foreach (var item in PayloadOf(distinct.Operand).Items)
        {
            if (CancellationStop() is { } cancelled)
            {
                return cancelled;
            }

            var member = false;
            foreach (var other in right.Items)
            {
                var equals = OperandsEqual(item, other);
                if (equals.Disposition != FoldDisposition.Folded)
                {
                    return equals;
                }

                if (equals.Operand.Boolean)
                {
                    member = true;
                    break;
                }
            }

            if (member == keepMembers)
            {
                kept.Add(item);
            }
        }

        return CreateSequence(left.With(kept.ToImmutable()));
    }

    private static FoldOutcome SequenceEquals(ImmutableArray<Operand> left, ImmutableArray<Operand> right)
    {
        if (left.Length != right.Length)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(false));
        }

        for (var index = 0; index < left.Length; index++)
        {
            var equals = OperandsEqual(left[index], right[index]);
            if (equals.Disposition != FoldDisposition.Folded)
            {
                return equals;
            }

            if (!equals.Operand.Boolean)
            {
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            }
        }

        return FoldOutcome.Folded(Operand.FromBoolean(true));
    }

    private static FoldOutcome SequenceOrder(SequencePayload payload, bool descending)
    {
        if (payload.ElementKind == OperandKind.String)
        {
            return CultureSensitive(
                "Order over strings",
                "an explicit ordinal transformation; default string ordering is culture-sensitive");
        }

        string? failure = null;
        int Compare(Operand left, Operand right)
        {
            if (failure is not null)
            {
                return 0;
            }

            if (left.Kind == OperandKind.Boolean && right.Kind == OperandKind.Boolean)
            {
                return left.Boolean.CompareTo(right.Boolean);
            }

            if (TryCompareTemporal(left, right, out var temporalComparison))
            {
                return temporalComparison;
            }

            if (TryCompareBclValue(left, right, out var valueComparison))
            {
                return valueComparison;
            }

            if (left.IsNumeric && right.IsNumeric)
            {
                if (!TryPromote(left, right, out var target, out _))
                {
                    failure = "The sequence mixes numeric domains that no comparison combines.";
                    return 0;
                }

                return target switch
                {
                    // Comparer<double> ordering: NaN sorts before every other value.
                    NumericKind.Single or NumericKind.Double => Comparer<double>.Default.Compare(
                        ToDoubleValue(NumericKindOf(left), BoxOf(left)),
                        ToDoubleValue(NumericKindOf(right), BoxOf(right))),
                    NumericKind.Decimal => ToDecimalValue(left).CompareTo(ToDecimalValue(right)),
                    _ => ToBigInteger(NumericKindOf(left), BoxOf(left)).CompareTo(
                        ToBigInteger(NumericKindOf(right), BoxOf(right))),
                };
            }

            failure = "Ordering requires numeric, char, or Boolean elements.";
            return 0;
        }

        // Enumerable.OrderBy is a stable sort, and stability is part of the published semantics.
        var ordered = descending
            ? payload.Items.OrderByDescending(static item => item, Comparer<Operand>.Create(Compare))
            : payload.Items.OrderBy(static item => item, Comparer<Operand>.Create(Compare));
        var materialized = ordered.ToImmutableArray();
        return failure is null
            ? CreateSequence(payload.With(materialized))
            : FoldOutcome.Error(OperandTypeCode, failure);
    }

    private static FoldOutcome SequenceSum(SequencePayload payload)
    {
        if (!TryCommonNumericKind(payload, out var common, out var error))
        {
            return error;
        }

        try
        {
            return common switch
            {
                NumericKind.Int64 => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Int64,
                    MaterializeInt64(payload).Sum())),
                NumericKind.Single => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Single,
                    MaterializeSingle(payload).Sum())),
                NumericKind.Double => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    MaterializeDouble(payload).Sum())),
                NumericKind.Decimal => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Decimal,
                    MaterializeDecimal(payload).Sum())),
                _ => FoldOutcome.Folded(Operand.FromInt32(MaterializeInt32(payload).Sum())),
            };
        }
        catch (OverflowException exception)
        {
            return FoldOutcome.Error(OverflowCode, exception.Message);
        }
    }

    private static FoldOutcome SequenceAverage(SequencePayload payload)
    {
        if (!TryCommonNumericKind(payload, out var common, out var error))
        {
            return error;
        }

        if (payload.Items.Length == 0)
        {
            return EmptySequence();
        }

        try
        {
            return common switch
            {
                NumericKind.Int64 => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    MaterializeInt64(payload).Average())),
                NumericKind.Single => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Single,
                    MaterializeSingle(payload).Average())),
                NumericKind.Double => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    MaterializeDouble(payload).Average())),
                NumericKind.Decimal => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Decimal,
                    MaterializeDecimal(payload).Average())),
                _ => FoldOutcome.Folded(Operand.FromNumeric(
                    NumericKind.Double,
                    MaterializeInt32(payload).Average())),
            };
        }
        catch (OverflowException exception)
        {
            return FoldOutcome.Error(OverflowCode, exception.Message);
        }
    }

    private static FoldOutcome SequenceMinMax(SequencePayload payload, bool wantMin)
    {
        if (payload.Items.Length == 0)
        {
            return EmptySequence();
        }

        if (payload.ElementKind == OperandKind.Char &&
            payload.Items.All(static item => item.Kind == OperandKind.Char))
        {
            var characters = payload.Items.Select(static item => item.Char);
            return FoldOutcome.Folded(Operand.FromChar(wantMin ? characters.Min() : characters.Max()));
        }

        if (payload.ElementKind == OperandKind.String)
        {
            return CultureSensitive(
                $"{(wantMin ? "Min" : "Max")} over strings",
                "an explicit ordinal transformation; default string ordering is culture-sensitive");
        }

        if (payload.ElementKind is OperandKind.Temporal or OperandKind.BclValue)
        {
            var best = payload.Items[0];
            foreach (var item in payload.Items)
            {
                if (!TryCompareTemporal(item, best, out var comparison) &&
                    !TryCompareBclValue(item, best, out comparison))
                {
                    return FoldOutcome.Error(OperandTypeCode, "The aggregate requires one comparable element kind.");
                }

                if (wantMin ? comparison < 0 : comparison > 0)
                {
                    best = item;
                }
            }

            return FoldOutcome.Folded(best);
        }

        if (!TryCommonNumericKind(payload, out var common, out var error))
        {
            return error;
        }

        return common switch
        {
            // The real Enumerable semantics carry over exactly, including NaN propagation for floating point.
            NumericKind.Int64 => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Int64,
                wantMin ? MaterializeInt64(payload).Min() : MaterializeInt64(payload).Max())),
            NumericKind.Single => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Single,
                wantMin ? MaterializeSingle(payload).Min() : MaterializeSingle(payload).Max())),
            NumericKind.Double => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Double,
                wantMin ? MaterializeDouble(payload).Min() : MaterializeDouble(payload).Max())),
            NumericKind.Decimal => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Decimal,
                wantMin ? MaterializeDecimal(payload).Min() : MaterializeDecimal(payload).Max())),
            _ => FoldOutcome.Folded(Operand.FromInt32(
                wantMin ? MaterializeInt32(payload).Min() : MaterializeInt32(payload).Max())),
        };
    }

    /// <summary>
    /// Finds the aggregate domain: the numeric kinds LINQ defines overloads for, with the small kinds carried to
    /// Int32. Wider kinds have no <c>Enumerable</c> overloads and stay typed stops.
    /// </summary>
    private static bool TryCommonNumericKind(SequencePayload payload, out NumericKind common, out FoldOutcome error)
    {
        common = NumericKind.Int32;
        error = default;
        if (payload.Items.Length == 0)
        {
            common = payload.ElementKind == OperandKind.Numeric ? payload.ElementNumeric : NumericKind.Int32;
        }
        else
        {
            common = NumericKindOf(payload.Items[0]);
            foreach (var item in payload.Items)
            {
                if (!item.IsNumeric || item.Kind == OperandKind.Enum)
                {
                    error = FoldOutcome.Error(OperandTypeCode, "The aggregate requires numeric elements.");
                    return false;
                }

                if (!TryPromote(Operand.FromNumeric(common, BoxFromBigInteger(common, 0)), item, out common, out error))
                {
                    return false;
                }
            }
        }

        if (common is NumericKind.SByte or NumericKind.Byte or NumericKind.Int16 or NumericKind.UInt16
            or NumericKind.UInt32)
        {
            common = common == NumericKind.UInt32 ? NumericKind.Int64 : NumericKind.Int32;
        }

        if (common is NumericKind.Int32 or NumericKind.Int64 or NumericKind.Single or NumericKind.Double
            or NumericKind.Decimal)
        {
            return true;
        }

        error = FoldOutcome.Error(
            OperandTypeCode,
            $"System.Linq.Enumerable defines no aggregate over {common} elements.");
        return false;
    }

    private static int[] MaterializeInt32(SequencePayload payload) =>
        [.. payload.Items.Select(static item => (int)ToBigInteger(NumericKindOf(item), BoxOf(item)))];

    private static long[] MaterializeInt64(SequencePayload payload) =>
        [.. payload.Items.Select(static item => (long)ToBigInteger(NumericKindOf(item), BoxOf(item)))];

    private static float[] MaterializeSingle(SequencePayload payload) =>
        [.. payload.Items.Select(static item => ToSingleValue(NumericKindOf(item), BoxOf(item)))];

    private static double[] MaterializeDouble(SequencePayload payload) =>
        [.. payload.Items.Select(static item => ToDoubleValue(NumericKindOf(item), BoxOf(item)))];

    private static decimal[] MaterializeDecimal(SequencePayload payload) =>
        [.. payload.Items.Select(ToDecimalValue)];

    // ---- Enumerable factories ---------------------------------------------------------------------------------------

    private static FoldOutcome DispatchEnumerable(string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "Range" when arguments is [{ } startOperand, { } countOperand] &&
                TryImplicitInt32(startOperand, out var start) && TryImplicitInt32(countOperand, out var count):
                if (count < 0 || (long)start + count - 1 > int.MaxValue)
                {
                    return FoldOutcome.Error(
                        ArgumentOutOfRangeCode,
                        "Enumerable.Range requires a non-negative count that stays within Int32.");
                }

                if (count > MaximumSequenceLength)
                {
                    return FoldOutcome.Error(
                        SequenceBoundCode,
                        $"The sequence would hold {count.ToString(CultureInfo.InvariantCulture)} elements, "
                        + "beyond the deterministic bound of "
                        + $"{MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.");
                }

                return CreateSequence(new SequencePayload(
                    [.. Enumerable.Range(start, count).Select(Operand.FromInt32)],
                    OperandKind.Int32,
                    NumericKind.Int32,
                    "Int32"));
            case "Repeat" when arguments is [{ } element, { } countOperand] &&
                TryImplicitInt32(countOperand, out var repetitions):
                if (repetitions < 0)
                {
                    return FoldOutcome.Error(
                        ArgumentOutOfRangeCode,
                        "Enumerable.Repeat requires a non-negative count.");
                }

                if (repetitions > MaximumSequenceLength)
                {
                    return FoldOutcome.Error(
                        SequenceBoundCode,
                        $"The sequence would hold {repetitions.ToString(CultureInfo.InvariantCulture)} elements, "
                        + "beyond the deterministic bound of "
                        + $"{MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.");
                }

                return CreateInferredSequence([.. Enumerable.Repeat(element, repetitions)]);
            default:
                return MemberUnsupported($"Enumerable.{name}");
        }
    }

    // ---- Display ----------------------------------------------------------------------------------------------------

    private const int MaximumRenderedElements = 32;

    /// <summary>Renders a rectangular array with its nested brace shape, row-major.</summary>
    private static string RenderRectangular(SequencePayload payload)
    {
        var offset = 0;
        return Render(0);

        string Render(int dimension)
        {
            if (dimension == payload.Dimensions.Length)
            {
                return RenderElement(payload.Items[offset++]);
            }

            var length = payload.Dimensions[dimension];
            var parts = new List<string>(length);
            for (var index = 0; index < length; index++)
            {
                parts.Add(Render(dimension + 1));
            }

            return parts.Count == 0 ? "{ }" : "{ " + string.Join(", ", parts) + " }";
        }
    }

    private static string RenderSequence(SequencePayload payload)
    {
        // A rectangular array of display-friendly size renders with its nested shape; a larger one falls back
        // to the flat, capped rendering below.
        if (payload.Dimensions.Length > 1 && payload.Items.Length <= MaximumRenderedElements)
        {
            return RenderRectangular(payload);
        }

        if (payload.Items.Length == 0)
        {
            return "{ }";
        }

        var builder = new StringBuilder("{ ");
        var rendered = 0;
        foreach (var item in payload.Items)
        {
            if (rendered == MaximumRenderedElements)
            {
                builder.Append(", … (+");
                builder.Append((payload.Items.Length - rendered).ToString(CultureInfo.InvariantCulture));
                builder.Append(" more)");
                break;
            }

            if (rendered > 0)
            {
                builder.Append(", ");
            }

            builder.Append(RenderElement(item));
            rendered++;
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static string RenderElement(Operand item) => item.Kind switch
    {
        OperandKind.String => "\"" + item.String!.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
        OperandKind.Char => "'" + item.Char + "'",
        OperandKind.Boolean => item.Boolean ? "true" : "false",
        OperandKind.Null => "null",
        OperandKind.Enum => item.EnumMemberName!,
        OperandKind.Temporal => RenderTemporal(item),
        OperandKind.BclValue => RenderBclValue(item),
        OperandKind.Type => $"typeof({((TypeRef)item.Box!).CSharpName})",
        OperandKind.Delegate => RenderDelegate(DelegatePayloadOf(item)),
        OperandKind.Tuple => RenderTuple(item),
        OperandKind.Anonymous => RenderAnonymous(item),
        OperandKind.Grouping => RenderGrouping(item),
        OperandKind.Sequence => RenderSequence(PayloadOf(item)),
        _ => FormatNumeric(NumericKindOf(item), BoxOf(item)),
    };
}
