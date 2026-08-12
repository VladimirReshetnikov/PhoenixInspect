using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>
/// The declared variables of an immediate-window session. A statement declares, initializes, or reassigns a
/// variable whose value is any expression the constant evaluator folds to a scalar; later expressions then read
/// the variable by name, and compose with it, through the evaluator's local-name resolver.
/// </summary>
/// <remarks>
/// The store holds constant values only, because that is the whole domain the immediate window evaluates: a
/// variable is a named, reusable constant, not a live object. A declaration's optional type is checked against the
/// folded value's kind so <c>int x = "hi"</c> is rejected rather than silently stored, and a value outside the
/// operand domain — a sequence or a wide numeric — is reported as unsupported rather than truncated.
/// </remarks>
public sealed class ImmediateVariableStore
{
    private readonly Dictionary<string, StoredVariable> variables = new(StringComparer.Ordinal);

    /// <summary>Gets the resolver the evaluator consults for a bare identifier that names a variable.</summary>
    public Func<string, ConstantOperandResolution> LocalNameResolver => name =>
        variables.TryGetValue(name, out var stored)
            ? stored.Value
            : ConstantOperandResolution.OutsideDomain();

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
        Func<string, ConstantExpressionEvaluation> evaluateExpression,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(evaluateExpression);

        if (!TryParse(line, out var declaredType, out var name, out var initializer))
        {
            message = "Usage: TYPE name = expression;  |  var name = expression;  |  name = expression;";
            return false;
        }

        if (declaredType is null && !variables.ContainsKey(name!))
        {
            message = $"'{name}' is not declared; write a type or 'var' to declare it.";
            return false;
        }

        var evaluation = evaluateExpression(initializer!);
        if (evaluation.Status != ConstantExpressionStatus.Exact)
        {
            message = evaluation.DiagnosticMessage is { } diagnostic
                ? $"'{name}' was not assigned: {diagnostic}"
                : $"'{name}' was not assigned: the initializer is not a constant value.";
            return false;
        }

        if (!evaluation.TryProjectStoredValue(out var stored) || stored is null)
        {
            message = $"'{name}' was not assigned: {evaluation.StoredValueTypeName ?? "that value"} "
                + "cannot be stored as a variable yet.";
            return false;
        }

        var valueTypeName = evaluation.StoredValueTypeName ?? "value";
        if (declaredType is not null &&
            !string.Equals(declaredType, "var", StringComparison.Ordinal) &&
            !TypeMatchesValue(declaredType, evaluation))
        {
            message = $"'{name}' declared '{declaredType}' cannot hold a {valueTypeName} value.";
            return false;
        }

        variables[name!] = new StoredVariable(stored, valueTypeName);
        message = $"{name} = {evaluation.ValueText ?? evaluation.Expression}  // {valueTypeName}";
        return true;
    }

    private static bool TypeMatchesValue(string declaredType, ConstantExpressionEvaluation evaluation)
    {
        // The declared type is matched by its C# keyword or its BCL short or full name against the folded kind, so
        // 'int', 'Int32', and 'System.Int32' all accept an Int32 value while 'int x = "s"' is refused.
        var normalized = declaredType switch
        {
            "int" => "Int32",
            "long" => "Int64",
            "double" => "Double",
            "float" => "Single",
            "bool" => "Boolean",
            "char" => "Char",
            "string" => "String",
            _ => declaredType,
        };
        var trimmed = normalized.Contains('.', StringComparison.Ordinal)
            ? normalized[(normalized.LastIndexOf('.') + 1)..]
            : normalized;
        return string.Equals(trimmed, evaluation.StoredValueTypeName, StringComparison.Ordinal) ||
            (evaluation.Kind == ConstantValueKind.EnumMember &&
                string.Equals(normalized, evaluation.EnumTypeFullName, StringComparison.Ordinal));
    }

    private static bool TryParse(
        string line,
        out string? declaredType,
        out string? name,
        out string? initializer)
    {
        declaredType = null;
        name = null;
        initializer = null;

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
                return true;
            default:
                return false;
        }
    }

    private readonly record struct StoredVariable(ConstantOperandResolution Value, string TypeName);
}
