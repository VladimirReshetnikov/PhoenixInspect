using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// C# query expressions, by the specification's own translation: every query desugars mechanically into the
/// method-syntax operators the evaluator already folds — <c>Select</c>, <c>Where</c>, <c>SelectMany</c>,
/// <c>OrderBy</c>/<c>ThenBy</c>, <c>GroupBy</c>, <c>Join</c>, and <c>GroupJoin</c> — with anonymous objects as
/// the transparent identifiers, exactly as the compiler translates. The translated tree folds through the normal
/// pipeline, so query semantics, typed stops, and evidence reports are identical to writing the methods by hand.
/// </content>
public static partial class ConstantExpressionEvaluator
{
    private const string QueryUnsupportedCode = "CONSTANT_QUERY_UNSUPPORTED";

    /// <summary>Folds a query expression by translating it to method syntax and folding the translation.</summary>
    private static FoldOutcome FoldQuery(QueryExpressionSyntax query, FoldContext context)
    {
        var translation = new QueryTranslator();
        var translated = translation.Translate(query);
        return translated is null
            ? FoldOutcome.Error(QueryUnsupportedCode, translation.Failure!)
            : Fold(translated, context);
    }

    /// <summary>
    /// The specification's query translation. Range variables live in a substitution map from name to an accessor
    /// expression over the current lambda parameter; each variable introduction wraps the visible variables into a
    /// flat anonymous transparent identifier, so accessors always stay one member deep.
    /// </summary>
    private sealed class QueryTranslator
    {
        private int freshCounter;

        /// <summary>Gets the reason translation stopped, when it did.</summary>
        public string? Failure { get; private set; }

        /// <summary>Translates one query expression, or null with <see cref="Failure"/> set.</summary>
        public ExpressionSyntax? Translate(QueryExpressionSyntax query)
        {
            var source = ApplyRangeCast(query.FromClause.Expression, query.FromClause.Type);
            var parameter = query.FromClause.Identifier.ValueText;
            var map = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal)
            {
                [parameter] = IdentifierName(parameter),
            };
            return TranslateBody(query.Body, source, parameter, map);
        }

        private ExpressionSyntax? TranslateBody(
            QueryBodySyntax body,
            ExpressionSyntax source,
            string parameter,
            Dictionary<string, ExpressionSyntax> map)
        {
            foreach (var clause in body.Clauses)
            {
                switch (clause)
                {
                    case FromClauseSyntax from:
                        var collection = ApplyRangeCast(Rewrite(from.Expression, map), from.Type);
                        var fromVariable = from.Identifier.ValueText;
                        if (!TryIntroduce(map, fromVariable, out var fromTransparent))
                        {
                            return null;
                        }

                        source = Call(
                            source,
                            "SelectMany",
                            Lambda(parameter, collection),
                            PairLambda(parameter, fromVariable, fromTransparent.Initializer));
                        parameter = fromTransparent.Parameter;
                        break;
                    case LetClauseSyntax let:
                        var letValue = Rewrite(let.Expression, map);
                        var letVariable = let.Identifier.ValueText;
                        if (!TryIntroduce(map, letVariable, out var letTransparent, letValue))
                        {
                            return null;
                        }

                        source = Call(source, "Select", Lambda(parameter, letTransparent.Initializer));
                        parameter = letTransparent.Parameter;
                        break;
                    case WhereClauseSyntax where:
                        source = Call(source, "Where", Lambda(parameter, Rewrite(where.Condition, map)));
                        break;
                    case JoinClauseSyntax join:
                        // The inner source is evaluated outside any lambda, so range variables are not visible
                        // in it and it is deliberately not rewritten; the inner key sees only the join variable.
                        var inner = ApplyRangeCast(join.InExpression, join.Type);
                        var outerKey = Lambda(parameter, Rewrite(join.LeftExpression, map));
                        var innerKey = Lambda(join.Identifier.ValueText, join.RightExpression);
                        var introduced = join.Into?.Identifier.ValueText ?? join.Identifier.ValueText;
                        if (!TryIntroduce(map, introduced, out var joinTransparent))
                        {
                            return null;
                        }

                        source = Call(
                            source,
                            join.Into is null ? "Join" : "GroupJoin",
                            inner,
                            outerKey,
                            innerKey,
                            PairLambda(parameter, introduced, joinTransparent.Initializer));
                        parameter = joinTransparent.Parameter;
                        break;
                    case OrderByClauseSyntax orderBy:
                        for (var index = 0; index < orderBy.Orderings.Count; index++)
                        {
                            var ordering = orderBy.Orderings[index];
                            var descending = ordering.AscendingOrDescendingKeyword.IsKind(
                                SyntaxKind.DescendingKeyword);
                            var operatorName = (index == 0, descending) switch
                            {
                                (true, false) => "OrderBy",
                                (true, true) => "OrderByDescending",
                                (false, false) => "ThenBy",
                                (false, true) => "ThenByDescending",
                            };
                            source = Call(
                                source,
                                operatorName,
                                Lambda(parameter, Rewrite(ordering.Expression, map)));
                        }

                        break;
                    default:
                        Failure = $"The query clause '{clause.Kind()}' is outside the translated grammar.";
                        return null;
                }
            }

            switch (body.SelectOrGroup)
            {
                case SelectClauseSyntax select:
                    source = Call(source, "Select", Lambda(parameter, Rewrite(select.Expression, map)));
                    break;
                case GroupClauseSyntax group:
                    source = Call(
                        source,
                        "GroupBy",
                        Lambda(parameter, Rewrite(group.ByExpression, map)),
                        Lambda(parameter, Rewrite(group.GroupExpression, map)));
                    break;
                default:
                    Failure = "The query ends without a select or group clause.";
                    return null;
            }

            if (body.Continuation is { } continuation)
            {
                var continuationVariable = continuation.Identifier.ValueText;
                var continuationMap = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal)
                {
                    [continuationVariable] = IdentifierName(continuationVariable),
                };
                return TranslateBody(continuation.Body, source, continuationVariable, continuationMap);
            }

            return source;
        }

        /// <summary>
        /// Introduces one new range variable: wraps every visible variable plus the new one into a fresh anonymous
        /// transparent identifier, and rewrites the map so each name reads through the new parameter.
        /// </summary>
        /// <param name="map">The substitution map, updated in place.</param>
        /// <param name="newVariable">The variable being introduced.</param>
        /// <param name="transparent">The fresh parameter name and the anonymous initializer to produce it.</param>
        /// <param name="newValue">The new member's value; the bare variable identifier when null.</param>
        private bool TryIntroduce(
            Dictionary<string, ExpressionSyntax> map,
            string newVariable,
            out (string Parameter, AnonymousObjectCreationExpressionSyntax Initializer) transparent,
            ExpressionSyntax? newValue = null)
        {
            transparent = default!;
            if (map.ContainsKey(newVariable))
            {
                Failure = $"The range variable '{newVariable}' is already declared in this query scope.";
                return false;
            }

            if (map.Count + 1 > MaximumAnonymousMembers)
            {
                Failure = $"A query scope holds up to {MaximumAnonymousMembers.ToString(CultureInfo.InvariantCulture)} "
                    + "range variables.";
                return false;
            }

            var freshParameter = "__t" + freshCounter++.ToString(CultureInfo.InvariantCulture);
            var declarators = map
                .Select(static entry => AnonymousObjectMemberDeclarator(
                    NameEquals(IdentifierName(entry.Key)),
                    entry.Value))
                .Append(AnonymousObjectMemberDeclarator(
                    NameEquals(IdentifierName(newVariable)),
                    newValue ?? IdentifierName(newVariable)))
                .ToArray();
            transparent = (freshParameter, AnonymousObjectCreationExpression(SeparatedList(declarators)));

            var names = map.Keys.ToArray();
            foreach (var name in names)
            {
                map[name] = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(freshParameter),
                    IdentifierName(name));
            }

            map[newVariable] = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(freshParameter),
                IdentifierName(newVariable));
            return true;
        }

        /// <summary>Applies the <c>from T x in e</c> element conversion as a per-element cast.</summary>
        private ExpressionSyntax ApplyRangeCast(ExpressionSyntax source, TypeSyntax? type)
        {
            if (type is null)
            {
                return source;
            }

            var castParameter = "__c" + freshCounter++.ToString(CultureInfo.InvariantCulture);
            return Call(
                source,
                "Select",
                Lambda(castParameter, CastExpression(type, IdentifierName(castParameter))));
        }

        private static ExpressionSyntax Rewrite(
            ExpressionSyntax expression,
            Dictionary<string, ExpressionSyntax> map) =>
            (ExpressionSyntax)new RangeVariableRewriter(map).Visit(expression);

        private static InvocationExpressionSyntax Call(
            ExpressionSyntax receiver,
            string name,
            params ExpressionSyntax[] arguments) =>
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(receiver),
                    IdentifierName(name)),
                ArgumentList(SeparatedList(arguments.Select(Argument))));

        private static SimpleLambdaExpressionSyntax Lambda(string parameter, ExpressionSyntax body) =>
            SimpleLambdaExpression(Parameter(Identifier(parameter)), body);

        private static ParenthesizedLambdaExpressionSyntax PairLambda(
            string first,
            string second,
            ExpressionSyntax body) =>
            ParenthesizedLambdaExpression(
                ParameterList(SeparatedList(new[] { Parameter(Identifier(first)), Parameter(Identifier(second)) })),
                body);
    }

    /// <summary>
    /// Substitutes range-variable identifiers with their transparent-identifier accessors, respecting the scopes
    /// that shadow them: lambda parameters, nested query range variables, and name positions that are not
    /// expressions — member names, type slots, <c>nameof</c> arguments, and anonymous member names.
    /// </summary>
    private sealed class RangeVariableRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ExpressionSyntax> map;
        private readonly HashSet<string> shadowed = new(StringComparer.Ordinal);

        public RangeVariableRewriter(Dictionary<string, ExpressionSyntax> map) => this.map = map;

        public override Microsoft.CodeAnalysis.SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var name = node.Identifier.ValueText;
            if (shadowed.Contains(name) || !map.TryGetValue(name, out var accessor))
            {
                return base.VisitIdentifierName(node);
            }

            // Only expression positions substitute: the name of a member access, a type slot, an invocation
            // target's method name, or an anonymous member name keeps its spelling.
            if (node.Parent is MemberAccessExpressionSyntax member && member.Name == node)
            {
                return base.VisitIdentifierName(node);
            }

            if (node.Parent is QualifiedNameSyntax or NameEqualsSyntax or NameColonSyntax)
            {
                return base.VisitIdentifierName(node);
            }

            if (node.Parent is CastExpressionSyntax cast && cast.Type == node)
            {
                return base.VisitIdentifierName(node);
            }

            if (node.Parent is TypeOfExpressionSyntax typeOf && typeOf.Type == node)
            {
                return base.VisitIdentifierName(node);
            }

            if (node.Parent is ObjectCreationExpressionSyntax creation && creation.Type == node)
            {
                return base.VisitIdentifierName(node);
            }

            return accessor;
        }

        public override Microsoft.CodeAnalysis.SyntaxNode? VisitSimpleLambdaExpression(
            SimpleLambdaExpressionSyntax node) =>
            VisitWithShadowing(node, [node.Parameter.Identifier.ValueText], () =>
                base.VisitSimpleLambdaExpression(node));

        public override Microsoft.CodeAnalysis.SyntaxNode? VisitParenthesizedLambdaExpression(
            ParenthesizedLambdaExpressionSyntax node) =>
            VisitWithShadowing(
                node,
                [.. node.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText)],
                () => base.VisitParenthesizedLambdaExpression(node));

        public override Microsoft.CodeAnalysis.SyntaxNode? VisitQueryExpression(QueryExpressionSyntax node) =>
            VisitWithShadowing(node, [.. DeclaredRangeVariables(node)], () => base.VisitQueryExpression(node));

        private Microsoft.CodeAnalysis.SyntaxNode? VisitWithShadowing(
            Microsoft.CodeAnalysis.SyntaxNode node,
            ImmutableArray<string> names,
            Func<Microsoft.CodeAnalysis.SyntaxNode?> visit)
        {
            var added = new List<string>();
            foreach (var name in names)
            {
                if (shadowed.Add(name))
                {
                    added.Add(name);
                }
            }

            try
            {
                return visit();
            }
            finally
            {
                foreach (var name in added)
                {
                    shadowed.Remove(name);
                }
            }
        }

        /// <summary>Collects every range variable a nested query declares, across continuations.</summary>
        private static IEnumerable<string> DeclaredRangeVariables(QueryExpressionSyntax query)
        {
            yield return query.FromClause.Identifier.ValueText;
            var body = query.Body;
            while (body is not null)
            {
                foreach (var clause in body.Clauses)
                {
                    switch (clause)
                    {
                        case FromClauseSyntax from:
                            yield return from.Identifier.ValueText;
                            break;
                        case LetClauseSyntax let:
                            yield return let.Identifier.ValueText;
                            break;
                        case JoinClauseSyntax join:
                            yield return join.Identifier.ValueText;
                            if (join.Into is { } into)
                            {
                                yield return into.Identifier.ValueText;
                            }

                            break;
                        default:
                            break;
                    }
                }

                if (body.Continuation is { } continuation)
                {
                    yield return continuation.Identifier.ValueText;
                    body = continuation.Body;
                    continue;
                }

                body = null;
            }
        }
    }
}
