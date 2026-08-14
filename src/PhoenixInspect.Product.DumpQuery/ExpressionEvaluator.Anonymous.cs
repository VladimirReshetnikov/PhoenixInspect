using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Anonymous types as a compound value domain: creation with explicit and projected member names, member access,
/// C#'s value-based <see cref="object.Equals(object)"/>, the invariant <c>ToString</c> form, and structured
/// children for expandable display. Anonymous objects also serve as the transparent identifiers of query
/// expressions, exactly as the C# specification translates them.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>The greatest anonymous-type arity the evaluator folds.</summary>
    private const int MaximumAnonymousMembers = 32;

    /// <summary>One folded anonymous object: its named member values, in declaration order.</summary>
    /// <param name="Members">The members, in order.</param>
    private sealed record AnonymousPayload(ImmutableArray<(string Name, Operand Value)> Members);

    private static AnonymousPayload PayloadOfAnonymous(Operand operand) => (AnonymousPayload)operand.Box!;

    /// <summary>Folds an anonymous object creation, with C#'s explicit and projected member names.</summary>
    private static FoldOutcome FoldAnonymousObject(
        AnonymousObjectCreationExpressionSyntax creation,
        FoldContext context)
    {
        if (creation.Initializers.Count > MaximumAnonymousMembers)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"Anonymous types fold up to {MaximumAnonymousMembers} members.");
        }

        var members = ImmutableArray.CreateBuilder<(string Name, Operand Value)>(creation.Initializers.Count);
        foreach (var initializer in creation.Initializers)
        {
            var name = initializer.NameEquals?.Name.Identifier.ValueText
                ?? initializer.Expression switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    _ => null,
                };
            if (name is null)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "An anonymous type member needs a name: write 'Name = expression', or project a simple "
                    + "identifier or member access.");
            }

            if (members.Any(existing => string.Equals(existing.Name, name, StringComparison.Ordinal)))
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    $"The anonymous type declares '{name}' more than once.");
            }

            var value = Fold(initializer.Expression, context);
            if (value.Disposition != FoldDisposition.Folded)
            {
                return value;
            }

            members.Add((name, value.Operand));
        }

        return FoldOutcome.Folded(Operand.FromAnonymous(new AnonymousPayload(members.ToImmutable())));
    }

    /// <summary>Resolves one anonymous member by name.</summary>
    private static FoldOutcome DispatchAnonymousProperty(Operand receiver, string member)
    {
        foreach (var (name, value) in PayloadOfAnonymous(receiver).Members)
        {
            if (string.Equals(name, member, StringComparison.Ordinal))
            {
                return FoldOutcome.Folded(value);
            }
        }

        return MemberUnsupported($"anonymous type member '{member}'");
    }

    /// <summary>The deterministic anonymous methods: invariant <c>ToString</c> and value-based <c>Equals</c>.</summary>
    private static FoldOutcome DispatchAnonymousMethod(Operand receiver, string name, List<Operand> arguments)
    {
        switch (name, arguments)
        {
            case ("ToString", []):
                return FoldOutcome.Folded(Operand.FromString(AnonymousToStringInvariant(receiver)));
            case ("Equals", [{ Kind: OperandKind.Anonymous } other]):
                return AnonymousEquals(receiver, other);
            case ("Equals", [_]):
                // A different shape is a different anonymous type, and Equals(object) answers false.
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            default:
                return MemberUnsupported($"anonymous type method '{name}'");
        }
    }

    /// <summary>
    /// C#'s anonymous equality: two instances are equal when they are the same anonymous type — the same member
    /// names in the same order — and every member pair is equal in its own domain.
    /// </summary>
    private static FoldOutcome AnonymousEquals(Operand left, Operand right)
    {
        var leftMembers = PayloadOfAnonymous(left).Members;
        var rightMembers = PayloadOfAnonymous(right).Members;
        if (leftMembers.Length != rightMembers.Length)
        {
            return FoldOutcome.Folded(Operand.FromBoolean(false));
        }

        for (var index = 0; index < leftMembers.Length; index++)
        {
            if (!string.Equals(leftMembers[index].Name, rightMembers[index].Name, StringComparison.Ordinal))
            {
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            }

            var equal = OperandsEqual(leftMembers[index].Value, rightMembers[index].Value);
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

    /// <summary>Computes anonymous-operand binary operators: equality is a typed stop, never a guess.</summary>
    private static FoldOutcome ComputeAnonymousBinary(SyntaxKind kind) =>
        kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            ? FoldOutcome.Error(
                OperandTypeCode,
                "'==' on anonymous types is reference equality, which the evaluator does not model; use Equals "
                + "for the value comparison.")
            : FoldOutcome.Error(OperandTypeCode, "Anonymous types define no operators.");

    /// <summary>Renders an anonymous object for display, with quoted strings.</summary>
    private static string RenderAnonymous(Operand operand) =>
        "{ " + string.Join(", ", PayloadOfAnonymous(operand).Members
            .Select(static member => $"{member.Name} = {RenderCompoundOrElement(member.Value)}")) + " }";

    /// <summary>
    /// Renders an anonymous object the way its generated <c>ToString</c> does — members unquoted — under the
    /// invariant culture, the evaluator's standing substitute for the runtime's current culture.
    /// </summary>
    private static string AnonymousToStringInvariant(Operand operand) =>
        "{ " + string.Join(", ", PayloadOfAnonymous(operand).Members
            .Select(static member => $"{member.Name} = {member.Value.Kind switch
            {
                OperandKind.Null => string.Empty,
                OperandKind.String => member.Value.String!,
                OperandKind.Char => member.Value.Char.ToString(),
                OperandKind.Boolean => member.Value.Boolean ? "True" : "False",
                OperandKind.Tuple => TupleToStringInvariant(member.Value),
                OperandKind.Anonymous => AnonymousToStringInvariant(member.Value),
                OperandKind.Sequence => RenderSequence(PayloadOf(member.Value)),
                _ => RenderElement(member.Value),
            }}")) + " }";

    /// <summary>Names an anonymous shape by its member names, such as <c>new { Name, Total }</c>.</summary>
    private static string AnonymousTypeName(Operand operand) =>
        "new { " + string.Join(", ", PayloadOfAnonymous(operand).Members.Select(static member => member.Name))
        + " }";
}
