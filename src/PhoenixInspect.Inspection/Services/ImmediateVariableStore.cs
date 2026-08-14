using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>
/// The declared variables of an immediate-window session. A statement declares, initializes, or reassigns a
/// variable whose value is any expression the evaluator folds to a scalar; later expressions then read
/// the variable by name, and compose with it, through the evaluator's local-name resolver.
/// </summary>
/// <remarks>
/// The store holds exactly folded values only, because that is the whole domain the immediate window evaluates: a
/// variable is a named, reusable value, not a live object. The stored domains span the evaluator's operand
/// carriers — scalars, every numeric kind, date and time values, deterministic BCL values such as Guid and the
/// Regex family, and sequences of those elements. A declaration's optional type is checked against the folded
/// value's kind so <c>int x = "hi"</c> is rejected rather than silently stored, and a value outside the operand
/// domain — a tuple or an anonymous object — is reported as unsupported rather than truncated. A
/// <c>dynamic</c> declaration admits any storable value: late binding is how the evaluator dispatches anyway.
/// </remarks>
public sealed class ImmediateVariableStore
{
    private readonly Dictionary<string, StoredVariable> variables = new(StringComparer.Ordinal);

    /// <summary>Gets the resolver the evaluator consults for a bare identifier that names a variable.</summary>
    public Func<string, OperandResolution> LocalNameResolver => name =>
        variables.TryGetValue(name, out var stored)
            ? stored.Value
            : OperandResolution.OutsideDomain();

    /// <summary>Lists the declared variables as completion items, annotated with their value types.</summary>
    /// <returns>The items, ordered by name.</returns>
    public ImmutableArray<CompletionItem> ListCompletions() =>
    [
        .. variables
            .Select(static pair => new CompletionItem(pair.Key, CompletionItemKind.Local, pair.Value.TypeName))
            .OrderBy(static item => item.Text, StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Gets whether one submitted line is a declaration or an assignment statement.</summary>
    /// <param name="line">The submitted line, already trimmed.</param>
    /// <returns><see langword="true"/> when the line is a variable statement.</returns>
    public static bool IsStatement(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return TryParse(line, out _, out _, out _);
    }

    /// <summary>Declares, initializes, or reassigns a variable from one statement.</summary>
    /// <param name="line">The complete statement.</param>
    /// <param name="evaluateExpression">Evaluates the initializer expression with the variables already in scope.</param>
    /// <param name="message">A human-readable confirmation or the typed failure reason.</param>
    /// <returns><see langword="true"/> when the statement was applied and a value stored.</returns>
    public bool TryApply(
        string line,
        Func<string, ExpressionEvaluation> evaluateExpression,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(evaluateExpression);

        if (!TryParse(line, out var declaredType, out var name, out var initializer, out var initializerIsLambda))
        {
            message = "Usage: TYPE name = expression;  |  var name = expression;  |  name = expression;";
            return false;
        }

        if (declaredType is null && !variables.ContainsKey(name!))
        {
            message = $"'{name}' is not declared; write a type or 'var' to declare it.";
            return false;
        }

        // A lambda has no value without a target type; a typed declaration supplies one, so the initializer
        // evaluates as the conversion the declaration spells: 'Func<int, int> f = x => x + 1;'. 'var' and
        // 'dynamic' supply none, exactly as C# refuses 'dynamic d = x => x + 1;'.
        if (initializerIsLambda &&
            declaredType is not null &&
            !string.Equals(declaredType, "var", StringComparison.Ordinal) &&
            !string.Equals(declaredType, "dynamic", StringComparison.Ordinal))
        {
            initializer = $"({declaredType})({initializer})";
        }

        var evaluation = evaluateExpression(initializer!);
        if (evaluation.Status != ExpressionEvaluationStatus.Exact)
        {
            message = evaluation.DiagnosticMessage is { } diagnostic
                ? $"'{name}' was not assigned: {diagnostic}"
                : $"'{name}' was not assigned: the initializer did not fold to an exact value.";
            return false;
        }

        if (!evaluation.TryProjectStoredValue(out var stored) || stored is null)
        {
            message = $"'{name}' was not assigned: {evaluation.StoredValueTypeName ?? "that value"} "
                + "cannot be stored as a variable yet.";
            return false;
        }

        var valueTypeName = evaluation.StoredValueTypeName ?? "value";

        // 'var' infers and freezes the value's type; 'dynamic' declares late binding, so any value is admitted
        // and every later member dispatch follows the stored value's runtime kind — the evaluator's native mode.
        if (declaredType is not null &&
            !string.Equals(declaredType, "var", StringComparison.Ordinal) &&
            !string.Equals(declaredType, "dynamic", StringComparison.Ordinal) &&
            !TypeMatchesValue(declaredType, evaluation))
        {
            // A numeric declaration converts a numeric initializer exactly as the cast it spells would:
            // 'nint x = 5;' folds '(nint)(5)', with C#'s checked conversion semantics deciding the outcome.
            if (IsNumericTypeName(declaredType) &&
                evaluation.Kind is ExpressionValueKind.Int32 or ExpressionValueKind.Numeric)
            {
                var converted = evaluateExpression($"({declaredType})({initializer})");
                if (converted.Status == ExpressionEvaluationStatus.Exact &&
                    converted.TryProjectStoredValue(out var convertedStored) &&
                    convertedStored is not null &&
                    TypeMatchesValue(declaredType, converted))
                {
                    var convertedTypeName = converted.StoredValueTypeName ?? "value";
                    variables[name!] = new StoredVariable(convertedStored, convertedTypeName);
                    message = $"{name} = {RenderValue(converted)}  // {convertedTypeName}";
                    return true;
                }

                if (converted.DiagnosticMessage is { } conversionDiagnostic)
                {
                    message = $"'{name}' was not assigned: {conversionDiagnostic}";
                    return false;
                }
            }

            message = $"'{name}' declared '{declaredType}' cannot hold a {valueTypeName} value.";
            return false;
        }

        variables[name!] = new StoredVariable(stored, valueTypeName);
        message = $"{name} = {RenderValue(evaluation)}  // {valueTypeName}";
        return true;
    }

    /// <summary>
    /// Renders the stored value for the confirmation line, preferring the value itself over the initializer
    /// text so a rewritten statement such as <c>x += 2</c> confirms with the result, not the rewrite.
    /// </summary>
    private static string RenderValue(ExpressionEvaluation evaluation) =>
        evaluation.ValueText
        ?? evaluation.EnumMemberName
        ?? evaluation.Int32Value?.ToString(System.Globalization.CultureInfo.InvariantCulture)
        ?? (evaluation.BooleanValue is { } boolean ? boolean ? "true" : "false" : null)
        ?? (evaluation.StringValue is { } text ? $"\"{text}\"" : null)
        ?? (evaluation.CharValue is { } character ? $"'{character}'" : null)
        ?? evaluation.Expression;

    private static bool TypeMatchesValue(string declaredType, ExpressionEvaluation evaluation)
    {
        // The declared type is matched by its C# keyword or its BCL short or full name against the folded kind, so
        // 'int', 'Int32', and 'System.Int32' all accept an Int32 value while 'int x = "s"' is refused. An array
        // type normalizes elementwise, so 'byte[]' accepts a Byte[] sequence.
        var arraySuffix = string.Empty;
        var elementType = declaredType;
        while (elementType.EndsWith("[]", StringComparison.Ordinal))
        {
            arraySuffix += "[]";
            elementType = elementType[..^2];
        }

        var normalized = NormalizeTypeKeyword(elementType);
        var trimmed = normalized.Contains('.', StringComparison.Ordinal)
            ? normalized[(normalized.LastIndexOf('.') + 1)..]
            : normalized;

        // Type names carry no meaningful spaces, so 'Func<int,int>' matches the stored 'Func<int, int>'.
        return string.Equals(
                (trimmed + arraySuffix).Replace(" ", string.Empty, StringComparison.Ordinal),
                evaluation.StoredValueTypeName?.Replace(" ", string.Empty, StringComparison.Ordinal),
                StringComparison.Ordinal) ||
            (evaluation.Kind == ExpressionValueKind.EnumMember && arraySuffix.Length == 0 &&
                string.Equals(normalized, evaluation.EnumTypeFullName, StringComparison.Ordinal));
    }

    /// <summary>Maps a C# type keyword to its BCL short name; other spellings pass through unchanged.</summary>
    private static string NormalizeTypeKeyword(string typeName) => typeName switch
    {
        "int" => "Int32",
        "uint" => "UInt32",
        "long" => "Int64",
        "ulong" => "UInt64",
        "short" => "Int16",
        "ushort" => "UInt16",
        "byte" => "Byte",
        "sbyte" => "SByte",
        "nint" => "IntPtr",
        "nuint" => "UIntPtr",
        "double" => "Double",
        "float" => "Single",
        "decimal" => "Decimal",
        "bool" => "Boolean",
        "char" => "Char",
        "string" => "String",
        _ => typeName,
    };

    private static readonly ImmutableHashSet<string> NumericTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "SByte", "Byte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "IntPtr", "UIntPtr",
        "Int128", "UInt128", "BigInteger", "Half", "NFloat", "Single", "Double", "Decimal");

    /// <summary>Gets whether a declared type names a numeric domain the cast-conversion fallback covers.</summary>
    private static bool IsNumericTypeName(string declaredType)
    {
        if (declaredType.EndsWith("[]", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = NormalizeTypeKeyword(declaredType);
        var trimmed = normalized.Contains('.', StringComparison.Ordinal)
            ? normalized[(normalized.LastIndexOf('.') + 1)..]
            : normalized;
        return NumericTypeNames.Contains(trimmed);
    }

    private static bool TryParse(
        string line,
        out string? declaredType,
        out string? name,
        out string? initializer) =>
        TryParse(line, out declaredType, out name, out initializer, out _);

    private static bool TryParse(
        string line,
        out string? declaredType,
        out string? name,
        out string? initializer,
        out bool initializerIsLambda)
    {
        declaredType = null;
        name = null;
        initializer = null;
        initializerIsLambda = false;

        var statement = SyntaxFactory.ParseStatement(
            line.TrimEnd().EndsWith(';') ? line : line + ";");
        if (statement.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return false;
        }

        switch (statement)
        {
            case LocalDeclarationStatementSyntax
            {
                Declaration:
                {
                    Type: { } type,
                    Variables: [{ Identifier.ValueText: { } variableName, Initializer.Value: { } value }],
                },
            }:
                declaredType = type.ToString();
                name = variableName;
                initializer = value.ToString();
                initializerIsLambda = value is LambdaExpressionSyntax;
                return true;
            case ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax { Identifier.ValueText: { } target },
                    Right: { } assigned,
                },
            }:
                name = target;
                initializer = assigned.ToString();
                initializerIsLambda = assigned is LambdaExpressionSyntax;
                return true;
            case ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax { Identifier.ValueText: { } compoundTarget },
                    Right: { } operand,
                } compound,
            } when CompoundOperator(compound.Kind()) is { } op:
                // A compound assignment rewrites as the operation it abbreviates, with the operand
                // parenthesized so 'x *= 2 + 1' means 'x * (2 + 1)', exactly as C# defines it.
                name = compoundTarget;
                initializer = $"{compoundTarget} {op} ({operand})";
                return true;
            case ExpressionStatementSyntax
            {
                Expression: PostfixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression,
                    Operand: IdentifierNameSyntax { Identifier.ValueText: { } postfixTarget },
                } postfix,
            }:
                name = postfixTarget;
                initializer = $"{postfixTarget} {(postfix.IsKind(
                    SyntaxKind.PostIncrementExpression) ? "+" : "-")} 1";
                return true;
            case ExpressionStatementSyntax
            {
                Expression: PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression,
                    Operand: IdentifierNameSyntax { Identifier.ValueText: { } prefixTarget },
                } prefix,
            }:
                name = prefixTarget;
                initializer = $"{prefixTarget} {(prefix.IsKind(
                    SyntaxKind.PreIncrementExpression) ? "+" : "-")} 1";
                return true;
            default:
                return false;
        }
    }

    /// <summary>Maps a compound-assignment kind to the binary operator it abbreviates; null for others.</summary>
    private static string? CompoundOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.AddAssignmentExpression => "+",
        SyntaxKind.SubtractAssignmentExpression => "-",
        SyntaxKind.MultiplyAssignmentExpression => "*",
        SyntaxKind.DivideAssignmentExpression => "/",
        SyntaxKind.ModuloAssignmentExpression => "%",
        SyntaxKind.AndAssignmentExpression => "&",
        SyntaxKind.OrAssignmentExpression => "|",
        SyntaxKind.ExclusiveOrAssignmentExpression => "^",
        SyntaxKind.LeftShiftAssignmentExpression => "<<",
        SyntaxKind.RightShiftAssignmentExpression => ">>",
        SyntaxKind.UnsignedRightShiftAssignmentExpression => ">>>",
        SyntaxKind.CoalesceAssignmentExpression => "??",
        _ => null,
    };

    private readonly record struct StoredVariable(OperandResolution Value, string TypeName);
}
