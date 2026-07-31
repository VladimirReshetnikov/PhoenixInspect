using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the outcome of one constant-expression evaluation attempt.</summary>
public enum ConstantExpressionStatus
{
    /// <summary>The expression is not in the constant domain; another evaluation path should handle it.</summary>
    NotConstant = 1,

    /// <summary>The expression evaluated to one exact constant value.</summary>
    Exact = 2,

    /// <summary>
    /// The expression is constant-shaped but could not produce a value: an arithmetic error, an unsupported
    /// literal type, or an ambiguous metadata declaration.
    /// </summary>
    Invalid = 3,
}

/// <summary>Identifies the value domain of one exact constant result.</summary>
public enum ConstantValueKind
{
    /// <summary>No value was produced.</summary>
    None = 0,

    /// <summary>A checked <see cref="int"/> value, from folded arithmetic or a non-enum integer literal field.</summary>
    Int32 = 1,

    /// <summary>A decoded string literal field.</summary>
    String = 2,

    /// <summary>An enum member with an Int32-family underlying value.</summary>
    EnumMember = 3,
}

/// <summary>The complete outcome of one constant-expression evaluation attempt.</summary>
/// <remarks>
/// An exact enum or const-field result is metadata evidence: the value comes from the dump module's Constant table,
/// never from analysis-machine reflection, and the result retains the module content identity and exact tokens that
/// produced it. Folded arithmetic depends on no dump evidence at all, which the result states rather than hides.
/// </remarks>
public sealed class ConstantExpressionEvaluation
{
    private const string CanonicalVersion = "dump-constant-expression-v1";

    internal ConstantExpressionEvaluation(
        ConstantExpressionStatus status,
        string expression,
        ConstantValueKind kind,
        int? int32Value,
        string? stringValue,
        string? enumTypeFullName,
        string? enumMemberName,
        string? underlyingTypeName,
        string? moduleName,
        string? moduleContentSha256,
        int? typeToken,
        int? fieldToken,
        int modulesScanned,
        int moduleCount,
        string? diagnosticCode,
        string? diagnosticMessage)
    {
        Status = status;
        Expression = expression;
        Kind = kind;
        Int32Value = int32Value;
        StringValue = stringValue;
        EnumTypeFullName = enumTypeFullName;
        EnumMemberName = enumMemberName;
        UnderlyingTypeName = underlyingTypeName;
        ModuleName = moduleName;
        ModuleContentSha256 = moduleContentSha256;
        TypeToken = typeToken;
        FieldToken = fieldToken;
        ModulesScanned = modulesScanned;
        ModuleCount = moduleCount;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        Sha256 = ComputeSha256();
    }

    /// <summary>Gets the evaluation disposition.</summary>
    public ConstantExpressionStatus Status { get; }

    /// <summary>Gets the exact raw expression text.</summary>
    public string Expression { get; }

    /// <summary>Gets the value domain of an exact result; otherwise <see cref="ConstantValueKind.None"/>.</summary>
    public ConstantValueKind Kind { get; }

    /// <summary>Gets the exact Int32 value for folded arithmetic, integer literal fields, and enum members.</summary>
    public int? Int32Value { get; }

    /// <summary>Gets the decoded string value only for <see cref="ConstantValueKind.String"/>.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the declaring enum's full metadata name for an enum member; otherwise null.</summary>
    public string? EnumTypeFullName { get; }

    /// <summary>Gets the enum member's exact metadata name; otherwise null.</summary>
    public string? EnumMemberName { get; }

    /// <summary>Gets the display name of the constant's metadata type code, when a literal field produced it.</summary>
    public string? UnderlyingTypeName { get; }

    /// <summary>Gets the runtime-reported name of the module that declared the literal, when one did.</summary>
    public string? ModuleName { get; }

    /// <summary>Gets the complete metadata content identity of the declaring module, when one did.</summary>
    public string? ModuleContentSha256 { get; }

    /// <summary>Gets the declaring TypeDef token, when a literal field produced the value.</summary>
    public int? TypeToken { get; }

    /// <summary>Gets the literal FieldDef token, when a literal field produced the value.</summary>
    public int? FieldToken { get; }

    /// <summary>Gets how many module metadata images were read exactly during name resolution.</summary>
    public int ModulesScanned { get; }

    /// <summary>Gets how many module instances the snapshot reports.</summary>
    public int ModuleCount { get; }

    /// <summary>Gets the stable diagnostic code for an invalid outcome; otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the artifact-independent explanation for an invalid outcome; otherwise null.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets the lowercase SHA-256 identity of the canonical outcome projection.</summary>
    public string Sha256 { get; }

    internal static ConstantExpressionEvaluation NotConstantResult(string expression) => new(
        ConstantExpressionStatus.NotConstant,
        expression,
        ConstantValueKind.None,
        int32Value: null,
        stringValue: null,
        enumTypeFullName: null,
        enumMemberName: null,
        underlyingTypeName: null,
        moduleName: null,
        moduleContentSha256: null,
        typeToken: null,
        fieldToken: null,
        modulesScanned: 0,
        moduleCount: 0,
        diagnosticCode: null,
        diagnosticMessage: null);

    internal static ConstantExpressionEvaluation InvalidResult(
        string expression,
        string code,
        string message,
        int modulesScanned = 0,
        int moduleCount = 0) => new(
        ConstantExpressionStatus.Invalid,
        expression,
        ConstantValueKind.None,
        int32Value: null,
        stringValue: null,
        enumTypeFullName: null,
        enumMemberName: null,
        underlyingTypeName: null,
        moduleName: null,
        moduleContentSha256: null,
        typeToken: null,
        fieldToken: null,
        modulesScanned,
        moduleCount,
        code,
        message);

    private string ComputeSha256()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Expression);
        Append(builder, ((int)Status).ToString(CultureInfo.InvariantCulture));
        Append(builder, ((int)Kind).ToString(CultureInfo.InvariantCulture));
        Append(builder, Int32Value?.ToString(CultureInfo.InvariantCulture) ?? "none");
        Append(builder, StringValue ?? "none");
        Append(builder, EnumTypeFullName ?? "none");
        Append(builder, EnumMemberName ?? "none");
        Append(builder, UnderlyingTypeName ?? "none");
        Append(builder, ModuleContentSha256 ?? "none");
        Append(builder, TypeToken?.ToString("x8", CultureInfo.InvariantCulture) ?? "none");
        Append(builder, FieldToken?.ToString("x8", CultureInfo.InvariantCulture) ?? "none");
        Append(builder, DiagnosticCode ?? "none");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }
}

/// <summary>
/// Evaluates constant expressions: checked Int32 arithmetic over integer literals, and fully qualified enum or
/// const literal fields read from dump module metadata.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator is a pre-stage in front of the frozen static-field pipeline and is deliberately three-state.
/// Anything outside the constant domain — names that bind to stored fields, contextual names, unsupported syntax —
/// returns <see cref="ConstantExpressionStatus.NotConstant"/> so the existing paths answer exactly as before. Only
/// expressions the frozen paths could never answer (literal folding and literal fields, which W7 storage admission
/// rejects by design) can newly succeed here.
/// </para>
/// <para>
/// Arithmetic follows C# constant-expression semantics: <see cref="int"/> operands, checked overflow, division by
/// zero as an error, and shift counts masked to five bits. Literal fields come from the Constant table of counted,
/// completely read module metadata; the declared uniqueness claim spans the modules whose metadata was exactly
/// readable, and the result records that scanned-versus-total count. Nested types and two-part names that need
/// import context are outside this version.
/// </para>
/// </remarks>
public static class ConstantExpressionEvaluator
{
    private const string UnsupportedConstantCode = "CONSTANT_EXPRESSION_UNSUPPORTED";
    private const string OverflowCode = "CONSTANT_OVERFLOW";
    private const string DivisionByZeroCode = "CONSTANT_DIVISION_BY_ZERO";
    private const string LiteralTypeUnsupportedCode = "CONSTANT_LITERAL_TYPE_UNSUPPORTED";
    private const string AmbiguousCode = "CONSTANT_DECLARATION_AMBIGUOUS";

    /// <summary>Attempts to evaluate one raw expression as a constant.</summary>
    /// <param name="session">
    /// The open dump session used to resolve literal fields; when null, only literal arithmetic can succeed and
    /// every qualified name is <see cref="ConstantExpressionStatus.NotConstant"/>.
    /// </param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <returns>An exact value, a typed constant-domain error, or a not-constant disposition.</returns>
    public static ConstantExpressionEvaluation Evaluate(ClrmdDumpSession? session, string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression) ||
            expression.Length > CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression ?? string.Empty);
        }

        ExpressionSyntax syntax;
        try
        {
            syntax = CSharpExpressionFrontEnd.ParseCompleteExpression(expression);
        }
        catch (ArgumentException)
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        if (syntax.GetDiagnostics().Any(static diagnostic =>
                diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error) ||
            syntax.DescendantTokens(descendIntoTrivia: true).Any(static token => token.IsMissing))
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        if (syntax.DescendantNodesAndTokensAndSelf(descendIntoTrivia: false).Count() >
            CSharpExpressionFrontEnd.MaximumNodeTokenCount)
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        // A qualified-name chain may be an enum member or const field; anything else without a single name in it
        // is the arithmetic domain.
        if (TryReadQualifiedName(syntax, out var nameParts))
        {
            return session is null || nameParts.Length < 3
                ? ConstantExpressionEvaluation.NotConstantResult(expression)
                : ResolveLiteralField(session, expression, nameParts);
        }

        if (syntax.DescendantNodesAndSelf().Any(static node =>
                node is IdentifierNameSyntax or GenericNameSyntax or AliasQualifiedNameSyntax))
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        var outcome = Fold(syntax);
        return outcome.Disposition switch
        {
            FoldDisposition.Folded => new ConstantExpressionEvaluation(
                ConstantExpressionStatus.Exact,
                expression,
                ConstantValueKind.Int32,
                outcome.Value,
                stringValue: null,
                enumTypeFullName: null,
                enumMemberName: null,
                underlyingTypeName: "Int32",
                moduleName: null,
                moduleContentSha256: null,
                typeToken: null,
                fieldToken: null,
                modulesScanned: 0,
                moduleCount: 0,
                diagnosticCode: null,
                diagnosticMessage: null),
            FoldDisposition.Error => ConstantExpressionEvaluation.InvalidResult(
                expression,
                outcome.Code!,
                outcome.Message!),
            _ => ConstantExpressionEvaluation.NotConstantResult(expression),
        };
    }

    private enum FoldDisposition
    {
        NotArithmetic,
        Folded,
        Error,
    }

    private readonly record struct FoldOutcome(FoldDisposition Disposition, int Value, string? Code, string? Message)
    {
        internal static FoldOutcome Folded(int value) => new(FoldDisposition.Folded, value, null, null);

        internal static FoldOutcome NotArithmetic() => new(FoldDisposition.NotArithmetic, 0, null, null);

        internal static FoldOutcome Error(string code, string message) =>
            new(FoldDisposition.Error, 0, code, message);
    }

    private static FoldOutcome Fold(ExpressionSyntax syntax)
    {
        switch (syntax)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression):
                return literal.Token.Value is int value
                    ? FoldOutcome.Folded(value)
                    : FoldOutcome.Error(
                        LiteralTypeUnsupportedCode,
                        "Only Int32 numeric literals participate in constant folding.");
            case ParenthesizedExpressionSyntax parenthesized:
                return Fold(parenthesized.Expression);
            case PrefixUnaryExpressionSyntax unary:
                return FoldUnary(unary);
            case BinaryExpressionSyntax binary:
                return FoldBinary(binary);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    private static FoldOutcome FoldUnary(PrefixUnaryExpressionSyntax unary)
    {
        // C# admits -2147483648 even though 2147483648 alone overflows Int32, so the exact spelling is
        // special-cased before the operand is folded.
        if (unary.IsKind(SyntaxKind.UnaryMinusExpression) &&
            unary.Operand is LiteralExpressionSyntax { Token.Value: 2147483648u })
        {
            return FoldOutcome.Folded(int.MinValue);
        }

        var operand = Fold(unary.Operand);
        if (operand.Disposition != FoldDisposition.Folded)
        {
            return operand;
        }

        try
        {
            return unary.Kind() switch
            {
                SyntaxKind.UnaryPlusExpression => FoldOutcome.Folded(operand.Value),
                SyntaxKind.UnaryMinusExpression => FoldOutcome.Folded(checked(-operand.Value)),
                SyntaxKind.BitwiseNotExpression => FoldOutcome.Folded(~operand.Value),
                _ => FoldOutcome.NotArithmetic(),
            };
        }
        catch (OverflowException)
        {
            return FoldOutcome.Error(
                OverflowCode,
                "The constant expression overflows Int32 under checked evaluation.");
        }
    }

    private static FoldOutcome FoldBinary(BinaryExpressionSyntax binary)
    {
        var kind = binary.Kind();
        if (kind is not (
            SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or
            SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression or SyntaxKind.BitwiseAndExpression or
            SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression or SyntaxKind.LeftShiftExpression or
            SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression))
        {
            return FoldOutcome.NotArithmetic();
        }

        var left = Fold(binary.Left);
        if (left.Disposition != FoldDisposition.Folded)
        {
            return left;
        }

        var right = Fold(binary.Right);
        if (right.Disposition != FoldDisposition.Folded)
        {
            return right;
        }

        if (kind is SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression && right.Value == 0)
        {
            return FoldOutcome.Error(
                DivisionByZeroCode,
                "The constant expression divides by zero.");
        }

        try
        {
            return FoldOutcome.Folded(kind switch
            {
                SyntaxKind.AddExpression => checked(left.Value + right.Value),
                SyntaxKind.SubtractExpression => checked(left.Value - right.Value),
                SyntaxKind.MultiplyExpression => checked(left.Value * right.Value),
                SyntaxKind.DivideExpression => checked(left.Value / right.Value),
                SyntaxKind.ModuloExpression => left.Value % right.Value,
                SyntaxKind.BitwiseAndExpression => left.Value & right.Value,
                SyntaxKind.BitwiseOrExpression => left.Value | right.Value,
                SyntaxKind.ExclusiveOrExpression => left.Value ^ right.Value,
                SyntaxKind.LeftShiftExpression => left.Value << right.Value,
                SyntaxKind.RightShiftExpression => left.Value >> right.Value,
                _ => left.Value >>> right.Value,
            });
        }
        catch (OverflowException)
        {
            return FoldOutcome.Error(
                OverflowCode,
                "The constant expression overflows Int32 under checked evaluation.");
        }
    }

    private static bool TryReadQualifiedName(ExpressionSyntax syntax, out ImmutableArray<string> parts)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var current = syntax;
        while (current is MemberAccessExpressionSyntax
        {
            RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
            Expression: { } inner,
            Name: IdentifierNameSyntax member,
        })
        {
            builder.Insert(0, member.Identifier.ValueText);
            current = inner;
        }

        switch (current)
        {
            case IdentifierNameSyntax head:
                builder.Insert(0, head.Identifier.ValueText);
                break;
            case AliasQualifiedNameSyntax
            {
                Alias.Identifier.ValueText: "global",
                Name: IdentifierNameSyntax aliased,
            }:
                builder.Insert(0, aliased.Identifier.ValueText);
                break;
            default:
                parts = default;
                return false;
        }

        parts = builder.ToImmutable();
        return parts.Length >= 2;
    }

    private static ConstantExpressionEvaluation ResolveLiteralField(
        ClrmdDumpSession session,
        string expression,
        ImmutableArray<string> parts)
    {
        var typeNamespace = string.Join('.', parts[..^2]);
        var typeName = parts[^2];
        var memberName = parts[^1];
        var scanned = 0;
        var matches = new List<ConstantExpressionEvaluation>();
        string? invalidCode = null;
        string? invalidMessage = null;

        foreach (var module in session.Modules)
        {
            var metadata = session.ReadModuleContentIdentity(module);
            if (metadata.Status != ClrmdEvidenceStatus.Exact ||
                metadata.Value is null ||
                metadata.Evidence.Length != 1 ||
                metadata.Evidence[0].Status != MemoryReadStatus.Exact)
            {
                continue;
            }

            scanned++;
            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(metadata.Evidence[0].Bytes);
                var reader = provider.GetMetadataReader();
                foreach (var handle in reader.TypeDefinitions)
                {
                    var typeDefinition = reader.GetTypeDefinition(handle);
                    if (!typeDefinition.GetDeclaringType().IsNil ||
                        !reader.StringComparer.Equals(typeDefinition.Name, typeName) ||
                        !reader.StringComparer.Equals(typeDefinition.Namespace, typeNamespace))
                    {
                        continue;
                    }

                    var projected = ProjectLiteralField(
                        reader,
                        handle,
                        typeDefinition,
                        expression,
                        typeNamespace,
                        typeName,
                        memberName,
                        module.Name,
                        metadata.Value.MetadataSha256,
                        out var projectionCode,
                        out var projectionMessage);
                    if (projected is not null)
                    {
                        matches.Add(projected);
                    }
                    else if (projectionCode is not null)
                    {
                        invalidCode = projectionCode;
                        invalidMessage = projectionMessage;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // A malformed metadata image cannot contribute a declaration; other modules still can.
            }
        }

        if (matches.Count == 1)
        {
            var single = matches[0];
            return new ConstantExpressionEvaluation(
                ConstantExpressionStatus.Exact,
                single.Expression,
                single.Kind,
                single.Int32Value,
                single.StringValue,
                single.EnumTypeFullName,
                single.EnumMemberName,
                single.UnderlyingTypeName,
                single.ModuleName,
                single.ModuleContentSha256,
                single.TypeToken,
                single.FieldToken,
                scanned,
                session.Modules.Length,
                diagnosticCode: null,
                diagnosticMessage: null);
        }

        if (matches.Count > 1)
        {
            return ConstantExpressionEvaluation.InvalidResult(
                expression,
                AmbiguousCode,
                $"{matches.Count} module instances declare literal '{typeNamespace}.{typeName}.{memberName}'; "
                + "no instance is selected by enumeration order.",
                scanned,
                session.Modules.Length);
        }

        if (invalidCode is not null)
        {
            return ConstantExpressionEvaluation.InvalidResult(
                expression,
                invalidCode,
                invalidMessage!,
                scanned,
                session.Modules.Length);
        }

        return ConstantExpressionEvaluation.NotConstantResult(expression);
    }

    private static ConstantExpressionEvaluation? ProjectLiteralField(
        MetadataReader reader,
        TypeDefinitionHandle typeHandle,
        TypeDefinition typeDefinition,
        string expression,
        string typeNamespace,
        string typeName,
        string memberName,
        string moduleName,
        string metadataSha256,
        out string? invalidCode,
        out string? invalidMessage)
    {
        invalidCode = null;
        invalidMessage = null;
        foreach (var fieldHandle in typeDefinition.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            if (!reader.StringComparer.Equals(field.Name, memberName))
            {
                continue;
            }

            const FieldAttributes required = FieldAttributes.Literal | FieldAttributes.Static;
            if ((field.Attributes & required) != required)
            {
                return null;
            }

            var constantHandle = field.GetDefaultValue();
            if (constantHandle.IsNil)
            {
                return null;
            }

            var constant = reader.GetConstant(constantHandle);
            var blob = reader.GetBlobReader(constant.Value);
            var isEnum = IsEnumType(reader, typeDefinition);
            var fullTypeName = typeNamespace.Length == 0 ? typeName : $"{typeNamespace}.{typeName}";
            switch (constant.TypeCode)
            {
                case ConstantTypeCode.SByte:
                case ConstantTypeCode.Byte:
                case ConstantTypeCode.Int16:
                case ConstantTypeCode.UInt16:
                case ConstantTypeCode.Int32:
                    var value = constant.TypeCode switch
                    {
                        ConstantTypeCode.SByte => blob.ReadSByte(),
                        ConstantTypeCode.Byte => blob.ReadByte(),
                        ConstantTypeCode.Int16 => blob.ReadInt16(),
                        ConstantTypeCode.UInt16 => blob.ReadUInt16(),
                        _ => blob.ReadInt32(),
                    };
                    return new ConstantExpressionEvaluation(
                        ConstantExpressionStatus.Exact,
                        expression,
                        isEnum ? ConstantValueKind.EnumMember : ConstantValueKind.Int32,
                        value,
                        stringValue: null,
                        isEnum ? fullTypeName : null,
                        isEnum ? memberName : null,
                        constant.TypeCode.ToString(),
                        moduleName,
                        metadataSha256,
                        MetadataTokens.GetToken(typeHandle),
                        MetadataTokens.GetToken(fieldHandle),
                        modulesScanned: 0,
                        moduleCount: 0,
                        diagnosticCode: null,
                        diagnosticMessage: null);
                case ConstantTypeCode.String:
                    var text = blob.Length == 0 ? string.Empty : blob.ReadUTF16(blob.Length);
                    return new ConstantExpressionEvaluation(
                        ConstantExpressionStatus.Exact,
                        expression,
                        ConstantValueKind.String,
                        int32Value: null,
                        text,
                        enumTypeFullName: null,
                        enumMemberName: null,
                        ConstantTypeCode.String.ToString(),
                        moduleName,
                        metadataSha256,
                        MetadataTokens.GetToken(typeHandle),
                        MetadataTokens.GetToken(fieldHandle),
                        modulesScanned: 0,
                        moduleCount: 0,
                        diagnosticCode: null,
                        diagnosticMessage: null);
                default:
                    invalidCode = LiteralTypeUnsupportedCode;
                    invalidMessage =
                        $"Literal '{fullTypeName}.{memberName}' has constant type {constant.TypeCode}, which is "
                        + "outside the supported Int32-family and string set.";
                    return null;
            }
        }

        return null;
    }

    private static bool IsEnumType(MetadataReader reader, TypeDefinition typeDefinition)
    {
        var baseType = typeDefinition.BaseType;
        return baseType.Kind switch
        {
            HandleKind.TypeReference when reader.GetTypeReference((TypeReferenceHandle)baseType) is var reference =>
                reader.StringComparer.Equals(reference.Name, "Enum") &&
                reader.StringComparer.Equals(reference.Namespace, "System"),
            HandleKind.TypeDefinition when reader.GetTypeDefinition((TypeDefinitionHandle)baseType) is var definition =>
                reader.StringComparer.Equals(definition.Name, "Enum") &&
                reader.StringComparer.Equals(definition.Namespace, "System"),
            _ => false,
        };
    }
}
