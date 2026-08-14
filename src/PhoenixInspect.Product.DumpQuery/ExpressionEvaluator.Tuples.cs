using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Tuple literals as a compound value domain: construction from <c>(a, b)</c> syntax with optional element names,
/// positional <c>ItemN</c> and named element access, C#'s element-wise equality, invariant rendering, and the
/// structured children that let a front end expand a tuple the way Visual Studio's Watch window does. The child
/// row shape — <c>[i]</c> for sequence elements, <c>ItemN</c> or the declared name for tuple elements — follows
/// the Visual Studio watch model (adapted from the Helix IDE's WatchExpression tree, used with permission).
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>The greatest tuple arity the evaluator folds; C# itself nests beyond eight via Rest.</summary>
    private const int MaximumTupleArity = 32;

    /// <summary>The greatest number of children realized for one compound value's expansion.</summary>
    private const int MaximumRealizedChildren = 512;

    /// <summary>One folded tuple: its element operands and their optional declared names, in order.</summary>
    /// <param name="Items">The element operands.</param>
    /// <param name="Names">The declared element names, null where the literal gave none.</param>
    private sealed record TuplePayload(ImmutableArray<Operand> Items, ImmutableArray<string?> Names);

    private static TuplePayload PayloadOfTuple(Operand operand) => (TuplePayload)operand.Box!;

    /// <summary>Folds a tuple literal by folding each element, keeping declared element names.</summary>
    private static FoldOutcome FoldTuple(TupleExpressionSyntax tuple, FoldContext context)
    {
        if (tuple.Arguments.Count > MaximumTupleArity)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"Tuple literals fold up to arity {MaximumTupleArity}.");
        }

        var items = ImmutableArray.CreateBuilder<Operand>(tuple.Arguments.Count);
        var names = ImmutableArray.CreateBuilder<string?>(tuple.Arguments.Count);
        foreach (var argument in tuple.Arguments)
        {
            var element = Fold(argument.Expression, context);
            if (element.Disposition != FoldDisposition.Folded)
            {
                return element;
            }

            items.Add(element.Operand);
            names.Add(argument.NameColon?.Name.Identifier.ValueText);
        }

        return FoldOutcome.Folded(Operand.FromTuple(new TuplePayload(items.MoveToImmutable(), names.MoveToImmutable())));
    }

    /// <summary>Resolves <c>ItemN</c> or a declared element name to the element operand.</summary>
    private static FoldOutcome DispatchTupleProperty(Operand receiver, string member)
    {
        var payload = PayloadOfTuple(receiver);
        if (member.StartsWith("Item", StringComparison.Ordinal)
            && int.TryParse(member[4..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ordinal)
            && ordinal >= 1)
        {
            return ordinal <= payload.Items.Length
                ? FoldOutcome.Folded(payload.Items[ordinal - 1])
                : FoldOutcome.Error(
                    OperandTypeCode,
                    $"The tuple has {payload.Items.Length} elements; '{member}' does not exist.");
        }

        for (var index = 0; index < payload.Names.Length; index++)
        {
            if (string.Equals(payload.Names[index], member, StringComparison.Ordinal))
            {
                return FoldOutcome.Folded(payload.Items[index]);
            }
        }

        return MemberUnsupported($"tuple member '{member}'");
    }

    /// <summary>The deterministic tuple methods: invariant <c>ToString</c> and element-wise <c>Equals</c>.</summary>
    private static FoldOutcome DispatchTupleMethod(Operand receiver, string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(TupleToStringInvariant(receiver)));
            case ("Equals", [{ Kind: OperandKind.Tuple } other]):
                return TuplesEqual(receiver, other);
            case ("Equals", [{ Kind: OperandKind.Null }]):
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            default:
                return MemberUnsupported($"tuple method '{name}'");
        }
    }

    /// <summary>Computes tuple <c>==</c> and <c>!=</c> with C#'s element-wise semantics.</summary>
    private static FoldOutcome ComputeTupleBinary(SyntaxKind kind, Operand left, Operand right)
    {
        if (left.Kind != OperandKind.Tuple || right.Kind != OperandKind.Tuple)
        {
            return FoldOutcome.Error(OperandTypeCode, "Tuples compare only with tuples of the same arity.");
        }

        if (kind is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression))
        {
            return FoldOutcome.Error(OperandTypeCode, "Tuples define equality only.");
        }

        var equals = TuplesEqual(left, right);
        if (equals.Disposition != FoldDisposition.Folded)
        {
            return equals;
        }

        return FoldOutcome.Folded(Operand.FromBoolean(
            kind == SyntaxKind.EqualsExpression ? equals.Operand.Boolean : !equals.Operand.Boolean));
    }

    /// <summary>Element-wise tuple equality; element pairs compare in their own domains.</summary>
    private static FoldOutcome TuplesEqual(Operand left, Operand right)
    {
        var leftPayload = PayloadOfTuple(left);
        var rightPayload = PayloadOfTuple(right);
        if (leftPayload.Items.Length != rightPayload.Items.Length)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Tuples compare only with tuples of the same arity.");
        }

        for (var index = 0; index < leftPayload.Items.Length; index++)
        {
            var leftItem = leftPayload.Items[index];
            var rightItem = rightPayload.Items[index];
            var equal = leftItem.Kind == OperandKind.Tuple && rightItem.Kind == OperandKind.Tuple
                ? TuplesEqual(leftItem, rightItem)
                : OperandsEqual(leftItem, rightItem);
            if (equal.Disposition != FoldDisposition.Folded)
            {
                return equal;
            }

            if (!equal.Operand.Boolean)
            {
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            }
        }

        return FoldOutcome.Folded(Operand.FromBoolean(true));
    }

    /// <summary>Renders a tuple for display: elements in their quoted display forms, like Visual Studio.</summary>
    private static string RenderTuple(Operand tuple)
    {
        var payload = PayloadOfTuple(tuple);
        return "(" + string.Join(", ", payload.Items.Select(RenderCompoundOrElement)) + ")";
    }

    /// <summary>
    /// Renders a tuple the way <see cref="ValueTuple.ToString"/> does — elements unquoted — under the invariant
    /// culture, which is the evaluator's standing substitute for the runtime's current culture.
    /// </summary>
    private static string TupleToStringInvariant(Operand tuple)
    {
        var payload = PayloadOfTuple(tuple);
        return "(" + string.Join(", ", payload.Items.Select(item => item.Kind switch
        {
            OperandKind.Null => string.Empty,
            OperandKind.String => item.String!,
            OperandKind.Char => item.Char.ToString(),
            OperandKind.Boolean => item.Boolean ? "True" : "False",
            OperandKind.Tuple => TupleToStringInvariant(item),
            OperandKind.Sequence => RenderSequence(PayloadOf(item)),
            _ => RenderElement(item),
        })) + ")";
    }

    /// <summary>Names a tuple's shape with CLR element-type names, such as <c>(Int32, String)</c>.</summary>
    private static string TupleTypeName(Operand tuple)
    {
        var payload = PayloadOfTuple(tuple);
        return "(" + string.Join(", ", payload.Items.Select(DisplayTypeNameOf)) + ")";
    }

    /// <summary>Renders one operand fully: compound operands render their compound forms, scalars quote.</summary>
    private static string RenderCompoundOrElement(Operand operand) => operand.Kind switch
    {
        OperandKind.Sequence => RenderSequence(PayloadOf(operand)),
        OperandKind.Tuple => RenderTuple(operand),
        OperandKind.Anonymous => RenderAnonymous(operand),
        OperandKind.Grouping => RenderGrouping(operand),
        _ => RenderElement(operand),
    };

    /// <summary>Names the display type of one operand, for child rows and tuple shapes.</summary>
    private static string DisplayTypeNameOf(Operand operand) => operand.Kind switch
    {
        OperandKind.Int32 => "Int32",
        OperandKind.Boolean => "Boolean",
        OperandKind.Char => "Char",
        OperandKind.String => "String",
        OperandKind.Null => "null",
        OperandKind.Numeric => NumericKindOf(operand).ToString(),
        OperandKind.Enum => operand.EnumTypeFullName is { } enumName && enumName.LastIndexOf('.') is var dot && dot >= 0
            ? enumName[(dot + 1)..]
            : operand.EnumTypeFullName ?? "Enum",
        OperandKind.Temporal => operand.TemporalKind.ToString(),
        OperandKind.BclValue => operand.BclValueKind.ToString(),
        OperandKind.Sequence => PayloadOf(operand).DisplayName + PayloadOf(operand).TypeSuffix,
        OperandKind.Tuple => TupleTypeName(operand),
        OperandKind.Anonymous => AnonymousTypeName(operand),
        OperandKind.Grouping => "IGrouping",
        OperandKind.Type => "Type",
        _ => string.Empty,
    };

    // ---- Structured children ----------------------------------------------------------------------------------------

    /// <summary>
    /// Realizes the structured children of one compound operand: <c>[i]</c> rows for sequence elements, and
    /// <c>ItemN</c>-or-declared-name rows for tuple elements, recursively for nested compounds. Realization is
    /// bounded; a sequence longer than the bound gains one honest tail row stating how many elements were elided.
    /// </summary>
    private static ImmutableArray<ExpressionValueChild> ChildrenOf(Operand operand)
    {
        switch (operand.Kind)
        {
            case OperandKind.Sequence:
                var payload = PayloadOf(operand);
                var realized = Math.Min(payload.Items.Length, MaximumRealizedChildren);
                var rows = ImmutableArray.CreateBuilder<ExpressionValueChild>(
                    realized + (payload.Items.Length > realized ? 1 : 0));
                for (var index = 0; index < realized; index++)
                {
                    rows.Add(ChildOf($"[{index}]", payload.Items[index]));
                }

                if (payload.Items.Length > realized)
                {
                    rows.Add(new ExpressionValueChild(
                        "…",
                        $"({payload.Items.Length - realized} more elements not realized)",
                        null,
                        []));
                }

                return rows.MoveToImmutable();
            case OperandKind.Tuple:
                var tuplePayload = PayloadOfTuple(operand);
                var tupleRows = ImmutableArray.CreateBuilder<ExpressionValueChild>(tuplePayload.Items.Length);
                for (var index = 0; index < tuplePayload.Items.Length; index++)
                {
                    tupleRows.Add(ChildOf(
                        tuplePayload.Names[index] ?? $"Item{index + 1}",
                        tuplePayload.Items[index]));
                }

                return tupleRows.MoveToImmutable();
            case OperandKind.Anonymous:
                return [.. PayloadOfAnonymous(operand).Members
                    .Select(static member => ChildOf(member.Name, member.Value))];
            case OperandKind.Grouping:
                var grouping = PayloadOfGrouping(operand);
                return
                [
                    ChildOf("Key", grouping.Key),
                    .. ChildrenOf(Operand.FromSequence(grouping.Items)),
                ];
            case OperandKind.BclValue when IsRegexCollection(operand.BclValueKind):
                return ChildrenOf(Operand.FromSequence(MaterializeRegexCollection(operand)));
            default:
                return [];
        }
    }

    private static ExpressionValueChild ChildOf(string name, Operand item) => new(
        name,
        RenderCompoundOrElement(item),
        item.Kind == OperandKind.Null ? null : DisplayTypeNameOf(item),
        ChildrenOf(item));
}
