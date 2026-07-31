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
    /// The expression is constant-shaped but could not produce a value: an arithmetic error, an argument error, a
    /// culture-sensitive or unsupported member, an unsupported literal type, or an ambiguous metadata declaration.
    /// </summary>
    Invalid = 3,
}

/// <summary>Identifies the value domain of one exact constant result.</summary>
public enum ConstantValueKind
{
    /// <summary>No value was produced.</summary>
    None = 0,

    /// <summary>A checked <see cref="int"/> value.</summary>
    Int32 = 1,

    /// <summary>A string value, from a literal, a literal field, or deterministic string operations.</summary>
    String = 2,

    /// <summary>An enum member with an Int32-family underlying value.</summary>
    EnumMember = 3,

    /// <summary>A UTF-16 code unit, from a char literal or deterministic char operations.</summary>
    Char = 4,

    /// <summary>A Boolean, from a literal, a comparison, logic, or a deterministic predicate.</summary>
    Boolean = 5,

    /// <summary>
    /// A numeric value in any C# numeric domain other than plain <see cref="int"/>: the other fixed-size integral
    /// types, <c>nint</c>/<c>nuint</c> (folded at 64 bits), <c>Int128</c>/<c>UInt128</c>,
    /// <see cref="System.Numerics.BigInteger"/>, <see cref="float"/>, <see cref="double"/>, and
    /// <see cref="decimal"/>. The exact kind is named by <see cref="ConstantExpressionEvaluation.ValueTypeName"/> and
    /// the value by its invariant-culture text in <see cref="ConstantExpressionEvaluation.ValueText"/>.
    /// </summary>
    Numeric = 6,

    /// <summary>An exact null, from the null literal, a lifted nullable operation, or a null dump value.</summary>
    Null = 7,
}

/// <summary>The complete outcome of one constant-expression evaluation attempt.</summary>
/// <remarks>
/// An exact enum or const-field result is metadata evidence: the value comes from the dump module's Constant table,
/// never from analysis-machine reflection, and the result retains the module content identity and exact tokens that
/// produced it. Folded arithmetic and deterministic string/char operations depend on no dump evidence at all, which
/// the result states rather than hides.
/// </remarks>
public sealed class ConstantExpressionEvaluation
{
    private const string CanonicalVersion = "dump-constant-expression-v3";

    internal ConstantExpressionEvaluation(
        ConstantExpressionStatus status,
        string expression,
        ConstantValueKind kind,
        int? int32Value,
        string? stringValue,
        char? charValue,
        bool? booleanValue,
        string? enumTypeFullName,
        string? enumMemberName,
        string? underlyingTypeName,
        string? moduleName,
        string? moduleContentSha256,
        int? typeToken,
        int? fieldToken,
        int modulesScanned,
        int moduleCount,
        int metadataLiteralsConsumed,
        string? diagnosticCode,
        string? diagnosticMessage,
        string? valueTypeName = null,
        string? valueText = null,
        int dumpValuesConsumed = 0)
    {
        ValueTypeName = valueTypeName;
        ValueText = valueText;
        DumpValuesConsumed = dumpValuesConsumed;
        Status = status;
        Expression = expression;
        Kind = kind;
        Int32Value = int32Value;
        StringValue = stringValue;
        CharValue = charValue;
        BooleanValue = booleanValue;
        EnumTypeFullName = enumTypeFullName;
        EnumMemberName = enumMemberName;
        UnderlyingTypeName = underlyingTypeName;
        ModuleName = moduleName;
        ModuleContentSha256 = moduleContentSha256;
        TypeToken = typeToken;
        FieldToken = fieldToken;
        ModulesScanned = modulesScanned;
        ModuleCount = moduleCount;
        MetadataLiteralsConsumed = metadataLiteralsConsumed;
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

    /// <summary>Gets the exact Int32 value for arithmetic, integer literal fields, and enum members.</summary>
    public int? Int32Value { get; }

    /// <summary>Gets the string value only for <see cref="ConstantValueKind.String"/>.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the char value only for <see cref="ConstantValueKind.Char"/>.</summary>
    public char? CharValue { get; }

    /// <summary>Gets the Boolean value only for <see cref="ConstantValueKind.Boolean"/>.</summary>
    public bool? BooleanValue { get; }

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

    /// <summary>Gets how many metadata literal fields a composed expression consumed as operands.</summary>
    public int MetadataLiteralsConsumed { get; }

    /// <summary>
    /// Gets the exact numeric type name of a <see cref="ConstantValueKind.Numeric"/> result — for example
    /// <c>Double</c>, <c>Int64</c>, <c>Decimal</c>, <c>IntPtr</c>, or <c>BigInteger</c>; otherwise null.
    /// </summary>
    public string? ValueTypeName { get; }

    /// <summary>
    /// Gets the invariant-culture text of a <see cref="ConstantValueKind.Numeric"/> result. IEEE-754 specials keep
    /// their invariant spellings: <c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>, and negative zero as <c>-0</c>.
    /// </summary>
    public string? ValueText { get; }

    /// <summary>Gets how many values extracted from the dump a composed expression consumed as operands.</summary>
    public int DumpValuesConsumed { get; }

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
        charValue: null,
        booleanValue: null,
        enumTypeFullName: null,
        enumMemberName: null,
        underlyingTypeName: null,
        moduleName: null,
        moduleContentSha256: null,
        typeToken: null,
        fieldToken: null,
        modulesScanned: 0,
        moduleCount: 0,
        metadataLiteralsConsumed: 0,
        diagnosticCode: null,
        diagnosticMessage: null);

    internal static ConstantExpressionEvaluation InvalidResult(
        string expression,
        string code,
        string message,
        int modulesScanned = 0,
        int moduleCount = 0,
        int metadataLiteralsConsumed = 0) => new(
        ConstantExpressionStatus.Invalid,
        expression,
        ConstantValueKind.None,
        int32Value: null,
        stringValue: null,
        charValue: null,
        booleanValue: null,
        enumTypeFullName: null,
        enumMemberName: null,
        underlyingTypeName: null,
        moduleName: null,
        moduleContentSha256: null,
        typeToken: null,
        fieldToken: null,
        modulesScanned,
        moduleCount,
        metadataLiteralsConsumed,
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
        Append(builder, CharValue is { } charValue ? ((int)charValue).ToString(CultureInfo.InvariantCulture) : "none");
        Append(builder, BooleanValue?.ToString() ?? "none");
        Append(builder, EnumTypeFullName ?? "none");
        Append(builder, EnumMemberName ?? "none");
        Append(builder, UnderlyingTypeName ?? "none");
        Append(builder, ModuleContentSha256 ?? "none");
        Append(builder, TypeToken?.ToString("x8", CultureInfo.InvariantCulture) ?? "none");
        Append(builder, FieldToken?.ToString("x8", CultureInfo.InvariantCulture) ?? "none");
        Append(builder, MetadataLiteralsConsumed.ToString(CultureInfo.InvariantCulture));
        Append(builder, ValueTypeName ?? "none");
        Append(builder, ValueText ?? "none");
        Append(builder, DumpValuesConsumed.ToString(CultureInfo.InvariantCulture));
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
/// Evaluates constant expressions: checked Int32 arithmetic, string and char literals with the deterministic
/// culture-independent BCL member surface, Boolean logic and comparisons, and fully qualified enum or const literal
/// fields read from dump module metadata.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator is a pre-stage in front of the frozen static-field pipeline and is deliberately three-state.
/// Anything outside the constant domain — names that bind to stored fields, contextual names, root expressions,
/// unsupported syntax — returns <see cref="ConstantExpressionStatus.NotConstant"/> so the existing paths answer
/// exactly as before.
/// </para>
/// <para>
/// The admitted BCL member set is a closed allowlist of deterministic, stateless, culture-independent operations:
/// ordinal string membership and search, substring and padding edits, invariant case mapping, and the
/// <see cref="char"/> classification predicates. A culture-sensitive member or overload — <c>ToLower()</c>,
/// <c>IndexOf(string)</c> without a comparison, a <see cref="StringComparison"/> other than <c>Ordinal</c> or
/// <c>OrdinalIgnoreCase</c> — is a typed stop naming the deterministic alternative, never silently evaluated.
/// Character classifications follow the pinned analysis runtime's Unicode tables. Argument errors such as an
/// out-of-range substring are typed stops, and no result is fabricated.
/// </para>
/// </remarks>
public static partial class ConstantExpressionEvaluator
{
    // Runtime-error analogues use the familiar exception type names; admission limits — members or operand
    // shapes the closed deterministic subset does not admit — keep descriptive codes because no exception
    // concept corresponds to them.
    private const string OverflowCode = "System.OverflowException";
    private const string DivisionByZeroCode = "System.DivideByZeroException";
    private const string ArgumentOutOfRangeCode = "System.ArgumentOutOfRangeException";
    private const string LiteralTypeUnsupportedCode = "CONSTANT_LITERAL_TYPE_UNSUPPORTED";
    private const string AmbiguousCode = "CONSTANT_DECLARATION_AMBIGUOUS";
    private const string OperandTypeCode = "CONSTANT_OPERAND_TYPE_UNSUPPORTED";
    private const string MemberUnsupportedCode = "CONSTANT_MEMBER_UNSUPPORTED";
    private const string CultureSensitiveCode = "CONSTANT_CULTURE_SENSITIVE_UNSUPPORTED";

    /// <summary>Attempts to evaluate one raw expression as a constant.</summary>
    /// <param name="session">
    /// The open dump session used to resolve literal fields; when null, qualified names cannot resolve and only
    /// literal-based evaluation can succeed.
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

        if (syntax.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            syntax.DescendantTokens(descendIntoTrivia: true).Any(static token => token.IsMissing))
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        if (syntax.DescendantNodesAndTokensAndSelf(descendIntoTrivia: false).Count() >
            CSharpExpressionFrontEnd.MaximumNodeTokenCount)
        {
            return ConstantExpressionEvaluation.NotConstantResult(expression);
        }

        // A bare qualified-name chain keeps its dedicated literal-field path so the result retains complete
        // module and token facts; a stored static field keeps falling through to the frozen pipeline. A chain
        // whose receiver is a known BCL type — 'Int32.MaxValue', 'System.Math.PI' — folds instead, because those
        // members are type statics, not metadata literals declared in dump modules.
        if (TryReadQualifiedName(syntax, out var nameParts) &&
            !(syntax is MemberAccessExpressionSyntax typeStaticCandidate &&
                TryReadTypeReceiver(typeStaticCandidate.Expression, out _)))
        {
            return session is null || nameParts.Length < 3
                ? ConstantExpressionEvaluation.NotConstantResult(expression)
                : ResolveLiteralField(session, expression, nameParts);
        }

        var context = new FoldContext(session);
        var outcome = Fold(syntax, context);
        return outcome.Disposition switch
        {
            FoldDisposition.Folded => FromOperand(expression, outcome.Operand, context),
            FoldDisposition.Error => ConstantExpressionEvaluation.InvalidResult(
                expression,
                outcome.Code!,
                outcome.Message!,
                metadataLiteralsConsumed: context.MetadataLiteralsConsumed),
            _ => ConstantExpressionEvaluation.NotConstantResult(expression),
        };
    }

    private sealed class FoldContext(ClrmdDumpSession? session)
    {
        internal ClrmdDumpSession? Session { get; } = session;

        internal int MetadataLiteralsConsumed { get; set; }
    }

    private enum OperandKind
    {
        Int32,
        Boolean,
        Char,
        String,
        Enum,
        Numeric,
        Null,
    }

    private readonly record struct Operand(
        OperandKind Kind,
        int Int32,
        bool Boolean,
        char Char,
        string? String,
        string? EnumTypeFullName,
        string? EnumMemberName,
        NumericKind NumericKind,
        object? Box)
    {
        internal static Operand FromInt32(int value) =>
            new(OperandKind.Int32, value, false, '\0', null, null, null, NumericKind.Int32, null);

        internal static Operand FromBoolean(bool value) =>
            new(OperandKind.Boolean, 0, value, '\0', null, null, null, default, null);

        internal static Operand FromChar(char value) =>
            new(OperandKind.Char, 0, false, value, null, null, null, default, null);

        internal static Operand FromString(string value) =>
            new(OperandKind.String, 0, false, '\0', value, null, null, default, null);

        internal static Operand FromEnum(int value, string typeFullName, string memberName) =>
            new(OperandKind.Enum, value, false, '\0', null, typeFullName, memberName, default, null);

        // Plain Int32 stays a first-class kind so the value composes with the string/char surface unchanged;
        // every other numeric domain rides in the boxed representation with its exact kind.
        internal static Operand FromNumeric(NumericKind kind, object value) => kind == NumericKind.Int32
            ? FromInt32((int)value)
            : new(OperandKind.Numeric, 0, false, '\0', null, null, null, kind, value);

        internal static Operand Null() =>
            new(OperandKind.Null, 0, false, '\0', null, null, null, default, null);

        internal bool IsNumeric =>
            Kind is OperandKind.Int32 or OperandKind.Char or OperandKind.Enum or OperandKind.Numeric;

        internal int AsInt32 => Kind switch
        {
            OperandKind.Char => Char,
            _ => Int32,
        };
    }

    private enum FoldDisposition
    {
        NotArithmetic,
        Folded,
        Error,
    }

    private readonly record struct FoldOutcome(
        FoldDisposition Disposition,
        Operand Operand,
        string? Code,
        string? Message)
    {
        internal static FoldOutcome Folded(Operand operand) => new(FoldDisposition.Folded, operand, null, null);

        internal static FoldOutcome NotArithmetic() => new(FoldDisposition.NotArithmetic, default, null, null);

        internal static FoldOutcome Error(string code, string message) =>
            new(FoldDisposition.Error, default, code, message);
    }

    private static ConstantExpressionEvaluation FromOperand(
        string expression,
        Operand operand,
        FoldContext context)
    {
        ConstantValueKind kind;
        int? int32 = null;
        string? text = null;
        char? character = null;
        bool? boolean = null;
        string? enumType = null;
        string? enumMember = null;
        string? valueTypeName = null;
        string? valueText = null;
        string? underlying;
        switch (operand.Kind)
        {
            case OperandKind.Int32:
                kind = ConstantValueKind.Int32;
                int32 = operand.Int32;
                underlying = "Int32";
                break;
            case OperandKind.String:
                kind = ConstantValueKind.String;
                text = operand.String;
                underlying = "String";
                break;
            case OperandKind.Char:
                kind = ConstantValueKind.Char;
                character = operand.Char;
                underlying = "Char";
                break;
            case OperandKind.Boolean:
                kind = ConstantValueKind.Boolean;
                boolean = operand.Boolean;
                underlying = "Boolean";
                break;
            case OperandKind.Numeric:
                kind = ConstantValueKind.Numeric;
                valueTypeName = operand.NumericKind.ToString();
                valueText = FormatNumeric(operand.NumericKind, operand.Box!);
                underlying = valueTypeName;
                break;
            case OperandKind.Null:
                kind = ConstantValueKind.Null;
                valueText = "null";
                underlying = null;
                break;
            default:
                kind = ConstantValueKind.EnumMember;
                int32 = operand.Int32;
                enumType = operand.EnumTypeFullName;
                enumMember = operand.EnumMemberName;
                underlying = "Int32";
                break;
        }
        return new ConstantExpressionEvaluation(
            ConstantExpressionStatus.Exact,
            expression,
            kind,
            int32,
            text,
            character,
            boolean,
            enumType,
            enumMember,
            underlying,
            moduleName: null,
            moduleContentSha256: null,
            typeToken: null,
            fieldToken: null,
            modulesScanned: 0,
            moduleCount: 0,
            context.MetadataLiteralsConsumed,
            diagnosticCode: null,
            diagnosticMessage: null,
            valueTypeName,
            valueText);
    }

    private static FoldOutcome Fold(ExpressionSyntax syntax, FoldContext context)
    {
        switch (syntax)
        {
            case LiteralExpressionSyntax literal:
                return FoldLiteral(literal);
            case ParenthesizedExpressionSyntax parenthesized:
                return Fold(parenthesized.Expression, context);
            case PrefixUnaryExpressionSyntax unary:
                return FoldUnary(unary, context);
            case BinaryExpressionSyntax binary:
                return FoldBinary(binary, context);
            case ConditionalExpressionSyntax conditional:
                return FoldConditional(conditional, context);
            case CastExpressionSyntax cast:
                return FoldCast(cast, context);
            case ElementAccessExpressionSyntax elementAccess:
                return FoldElementAccess(elementAccess, context);
            case MemberAccessExpressionSyntax memberAccess:
                return FoldMemberAccess(memberAccess, context);
            case InvocationExpressionSyntax invocation:
                return FoldInvocation(invocation, context);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    private static FoldOutcome FoldLiteral(LiteralExpressionSyntax literal)
    {
        if (literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            // The literal's own C# type is kept: 5 is Int32, 5u UInt32, 5L Int64, 5UL UInt64, 5f Single,
            // 5.0 Double, 5m Decimal.
            return literal.Token.Value switch
            {
                int value => FoldOutcome.Folded(Operand.FromInt32(value)),
                uint value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.UInt32, value)),
                long value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, value)),
                ulong value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.UInt64, value)),
                float value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, value)),
                double value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, value)),
                decimal value => FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Decimal, value)),
                _ => FoldOutcome.Error(
                    LiteralTypeUnsupportedCode,
                    "The numeric literal's type is outside the supported numeric domains."),
            };
        }

        if (literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return FoldOutcome.Folded(Operand.Null());
        }

        if (literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.Value is string text)
        {
            return FoldOutcome.Folded(Operand.FromString(text));
        }

        if (literal.IsKind(SyntaxKind.CharacterLiteralExpression) && literal.Token.Value is char character)
        {
            return FoldOutcome.Folded(Operand.FromChar(character));
        }

        if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return FoldOutcome.Folded(Operand.FromBoolean(true));
        }

        if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return FoldOutcome.Folded(Operand.FromBoolean(false));
        }

        return FoldOutcome.NotArithmetic();
    }

    private static FoldOutcome FoldUnary(PrefixUnaryExpressionSyntax unary, FoldContext context)
    {
        // C# admits -2147483648 and -9223372036854775808 even though the bare magnitudes overflow their signed
        // types, so the exact spellings are special-cased before the operand is folded.
        if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
        {
            switch (unary.Operand)
            {
                case LiteralExpressionSyntax { Token.Value: 2147483648u }:
                    return FoldOutcome.Folded(Operand.FromInt32(int.MinValue));
                case LiteralExpressionSyntax { Token.Value: 9223372036854775808ul }:
                    return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, long.MinValue));
            }
        }

        var operand = Fold(unary.Operand, context);
        if (operand.Disposition != FoldDisposition.Folded)
        {
            return operand;
        }

        if (unary.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return operand.Operand.Kind == OperandKind.Boolean
                ? FoldOutcome.Folded(Operand.FromBoolean(!operand.Operand.Boolean))
                : FoldOutcome.Error(OperandTypeCode, "Logical negation requires one Boolean operand.");
        }

        if (unary.Kind() is not (SyntaxKind.UnaryPlusExpression or SyntaxKind.UnaryMinusExpression
            or SyntaxKind.BitwiseNotExpression))
        {
            return FoldOutcome.NotArithmetic();
        }

        // Lifted unary arithmetic: an operand that is exactly null yields exactly null.
        if (operand.Operand.Kind == OperandKind.Null)
        {
            return FoldOutcome.Folded(Operand.Null());
        }

        if (!operand.Operand.IsNumeric)
        {
            return FoldOutcome.Error(OperandTypeCode, "Unary arithmetic requires one numeric operand.");
        }

        return ComputeNumericUnary(unary.Kind(), operand.Operand);
    }

    private static FoldOutcome FoldBinary(BinaryExpressionSyntax binary, FoldContext context)
    {
        var kind = binary.Kind();
        if (kind is not (
            SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or
            SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression or SyntaxKind.BitwiseAndExpression or
            SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression or SyntaxKind.LeftShiftExpression or
            SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression or
            SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression or
            SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression or
            SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.LogicalAndExpression or
            SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression))
        {
            return FoldOutcome.NotArithmetic();
        }

        var left = Fold(binary.Left, context);
        if (left.Disposition != FoldDisposition.Folded)
        {
            return left;
        }

        var right = Fold(binary.Right, context);
        if (right.Disposition != FoldDisposition.Folded)
        {
            return right;
        }

        var leftOperand = left.Operand;
        var rightOperand = right.Operand;

        if (kind == SyntaxKind.CoalesceExpression)
        {
            return FoldOutcome.Folded(leftOperand.Kind == OperandKind.Null ? rightOperand : leftOperand);
        }

        if (leftOperand.Kind == OperandKind.Null || rightOperand.Kind == OperandKind.Null)
        {
            return FoldNullLifted(kind, leftOperand, rightOperand);
        }

        if (kind == SyntaxKind.AddExpression &&
            (leftOperand.Kind == OperandKind.String || rightOperand.Kind == OperandKind.String))
        {
            if (!TryConcatOperand(leftOperand, out var leftText) || !TryConcatOperand(rightOperand, out var rightText))
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "String concatenation admits only string and char constant operands.");
            }

            return FoldOutcome.Folded(Operand.FromString(leftText + rightText));
        }

        if (kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
        {
            if (leftOperand.Kind != OperandKind.Boolean || rightOperand.Kind != OperandKind.Boolean)
            {
                return FoldOutcome.Error(OperandTypeCode, "Logical operators require Boolean constant operands.");
            }

            return FoldOutcome.Folded(Operand.FromBoolean(kind == SyntaxKind.LogicalAndExpression
                ? leftOperand.Boolean && rightOperand.Boolean
                : leftOperand.Boolean || rightOperand.Boolean));
        }

        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            bool? equal = (leftOperand.Kind, rightOperand.Kind) switch
            {
                (OperandKind.String, OperandKind.String) =>
                    string.Equals(leftOperand.String, rightOperand.String, StringComparison.Ordinal),
                (OperandKind.Boolean, OperandKind.Boolean) => leftOperand.Boolean == rightOperand.Boolean,
                _ => null,
            };
            if (equal is { } known)
            {
                return FoldOutcome.Folded(Operand.FromBoolean(kind == SyntaxKind.EqualsExpression
                    ? known
                    : !known));
            }

            if (leftOperand.IsNumeric && rightOperand.IsNumeric)
            {
                return ComputeNumericComparison(kind, leftOperand, rightOperand);
            }

            return FoldOutcome.Error(
                OperandTypeCode,
                "Equality requires two string, two Boolean, or two numeric constant operands.");
        }

        if (kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or
            SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression)
        {
            if (!leftOperand.IsNumeric || !rightOperand.IsNumeric)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "Relational comparison requires numeric constant operands.");
            }

            return ComputeNumericComparison(kind, leftOperand, rightOperand);
        }

        if (!leftOperand.IsNumeric || !rightOperand.IsNumeric)
        {
            return FoldOutcome.Error(OperandTypeCode, "Arithmetic requires numeric constant operands.");
        }

        return ComputeBinaryNumeric(kind, leftOperand, rightOperand);
    }

    private static FoldOutcome FoldNullLifted(SyntaxKind kind, Operand left, Operand right)
    {
        // Concatenating null contributes an empty string, exactly as string concatenation defines.
        if (kind == SyntaxKind.AddExpression &&
            (left.Kind == OperandKind.String || right.Kind == OperandKind.String))
        {
            return FoldOutcome.Folded(Operand.FromString(
                (left.Kind == OperandKind.String ? left.String : string.Empty) +
                (right.Kind == OperandKind.String ? right.String : string.Empty)));
        }

        switch (kind)
        {
            case SyntaxKind.EqualsExpression:
                return FoldOutcome.Folded(Operand.FromBoolean(
                    left.Kind == OperandKind.Null && right.Kind == OperandKind.Null));
            case SyntaxKind.NotEqualsExpression:
                return FoldOutcome.Folded(Operand.FromBoolean(
                    left.Kind != OperandKind.Null || right.Kind != OperandKind.Null));
            case SyntaxKind.LessThanExpression:
            case SyntaxKind.LessThanOrEqualExpression:
            case SyntaxKind.GreaterThanExpression:
            case SyntaxKind.GreaterThanOrEqualExpression:
                // A lifted comparison with a null operand is false, never null.
                return FoldOutcome.Folded(Operand.FromBoolean(false));
            case SyntaxKind.LogicalAndExpression:
            case SyntaxKind.LogicalOrExpression:
                return FoldOutcome.Error(OperandTypeCode, "Logical operators require Boolean constant operands.");
            default:
                // Lifted arithmetic: a null operand yields exactly null, and no operand error is fabricated.
                return FoldOutcome.Folded(Operand.Null());
        }
    }

    private static FoldOutcome FoldConditional(ConditionalExpressionSyntax conditional, FoldContext context)
    {
        var condition = Fold(conditional.Condition, context);
        if (condition.Disposition != FoldDisposition.Folded)
        {
            return condition;
        }

        if (condition.Operand.Kind != OperandKind.Boolean)
        {
            return FoldOutcome.Error(OperandTypeCode, "The conditional operator requires a Boolean condition.");
        }

        // Both branches fold, matching C# constant semantics where an erroneous unselected branch is still an error.
        var whenTrue = Fold(conditional.WhenTrue, context);
        if (whenTrue.Disposition != FoldDisposition.Folded)
        {
            return whenTrue;
        }

        var whenFalse = Fold(conditional.WhenFalse, context);
        if (whenFalse.Disposition != FoldDisposition.Folded)
        {
            return whenFalse;
        }

        var selected = condition.Operand.Boolean ? whenTrue.Operand : whenFalse.Operand;
        var other = condition.Operand.Boolean ? whenFalse.Operand : whenTrue.Operand;
        if (selected.Kind == other.Kind ||
            (selected.IsNumeric && other.IsNumeric) ||
            selected.Kind == OperandKind.Null ||
            other.Kind == OperandKind.Null)
        {
            return FoldOutcome.Folded(selected);
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "Both branches of a constant conditional must produce the same value domain.");
    }

    private static FoldOutcome FoldElementAccess(ElementAccessExpressionSyntax elementAccess, FoldContext context)
    {
        var receiver = Fold(elementAccess.Expression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return receiver;
        }

        if (receiver.Operand.Kind != OperandKind.String ||
            elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Constant element access requires one string receiver and one index or range.");
        }

        var text = receiver.Operand.String!;
        var argument = elementAccess.ArgumentList.Arguments[0].Expression;

        // A range argument slices with C# range semantics: s[a..b] over resolved from-start/from-end offsets.
        if (argument is RangeExpressionSyntax range)
        {
            var start = ResolveIndex(range.LeftOperand, text.Length, defaultOffset: 0, context);
            if (start.Disposition != FoldDisposition.Folded)
            {
                return start;
            }

            var end = ResolveIndex(range.RightOperand, text.Length, defaultOffset: text.Length, context);
            if (end.Disposition != FoldDisposition.Folded)
            {
                return end;
            }

            var startOffset = start.Operand.Int32;
            var endOffset = end.Operand.Int32;
            if (startOffset < 0 || endOffset > text.Length || startOffset > endOffset)
            {
                return FoldOutcome.Error(
                    ArgumentOutOfRangeCode,
                    $"Range [{startOffset.ToString(CultureInfo.InvariantCulture)}.."
                    + $"{endOffset.ToString(CultureInfo.InvariantCulture)}] is outside the string of length "
                    + $"{text.Length.ToString(CultureInfo.InvariantCulture)}.");
            }

            return FoldOutcome.Folded(Operand.FromString(text[startOffset..endOffset]));
        }

        var index = ResolveIndex(argument, text.Length, defaultOffset: 0, context);
        if (index.Disposition != FoldDisposition.Folded)
        {
            return index;
        }

        var position = index.Operand.Int32;
        if (position < 0 || position >= text.Length)
        {
            return FoldOutcome.Error(
                ArgumentOutOfRangeCode,
                $"Index {position.ToString(CultureInfo.InvariantCulture)} is outside the string of length "
                + $"{text.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        return FoldOutcome.Folded(Operand.FromChar(text[position]));
    }

    private static FoldOutcome ResolveIndex(
        ExpressionSyntax? syntax,
        int length,
        int defaultOffset,
        FoldContext context)
    {
        if (syntax is null)
        {
            return FoldOutcome.Folded(Operand.FromInt32(defaultOffset));
        }

        // ^n counts from the end, resolving to length - n exactly as System.Index does.
        if (syntax is PrefixUnaryExpressionSyntax fromEnd && fromEnd.IsKind(SyntaxKind.IndexExpression))
        {
            var operand = Fold(fromEnd.Operand, context);
            if (operand.Disposition != FoldDisposition.Folded)
            {
                return operand;
            }

            if (!TryImplicitInt32(operand.Operand, out var fromEndValue))
            {
                return FoldOutcome.Error(OperandTypeCode, "A from-end index must be an Int32 constant.");
            }
            if (fromEndValue < 0)
            {
                return FoldOutcome.Error(
                    ArgumentOutOfRangeCode,
                    "A from-end index cannot be negative.");
            }

            return FoldOutcome.Folded(Operand.FromInt32(length - fromEndValue));
        }

        var folded = Fold(syntax, context);
        if (folded.Disposition != FoldDisposition.Folded)
        {
            return folded;
        }

        if (!TryImplicitInt32(folded.Operand, out var offset))
        {
            return FoldOutcome.Error(OperandTypeCode, "A string index must be an Int32 constant.");
        }

        return FoldOutcome.Folded(Operand.FromInt32(offset));
    }

    private static FoldOutcome FoldMemberAccess(MemberAccessExpressionSyntax memberAccess, FoldContext context)
    {
        if (memberAccess.Name is not IdentifierNameSyntax member)
        {
            return FoldOutcome.NotArithmetic();
        }

        // A known type receiver wins over the metadata-literal path so 'Math.PI' or 'double.NaN' never triggers a
        // module scan; every other pure qualified-name chain is a metadata literal candidate, and a stored static
        // field stays not-constant.
        if (TryReadTypeReceiver(memberAccess.Expression, out var typeReceiver))
        {
            return DispatchTypeStatic(typeReceiver, member.Identifier.ValueText);
        }

        if (TryReadQualifiedName(memberAccess, out var parts))
        {
            return FoldQualifiedName(parts, context);
        }

        var receiver = Fold(memberAccess.Expression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return receiver;
        }

        return (receiver.Operand.Kind, member.Identifier.ValueText) switch
        {
            (OperandKind.String, "Length") => FoldOutcome.Folded(Operand.FromInt32(receiver.Operand.String!.Length)),
            _ => FoldOutcome.Error(
                MemberUnsupportedCode,
                $"'{member.Identifier.ValueText}' is not an admitted deterministic constant member."),
        };
    }

    private static FoldOutcome FoldQualifiedName(ImmutableArray<string> parts, FoldContext context)
    {
        if (context.Session is null || parts.Length < 3)
        {
            return FoldOutcome.NotArithmetic();
        }

        var resolved = ResolveLiteralField(context.Session, string.Join('.', parts), parts);
        switch (resolved.Status)
        {
            case ConstantExpressionStatus.Exact:
                context.MetadataLiteralsConsumed++;
                return FoldOutcome.Folded(resolved.Kind switch
                {
                    ConstantValueKind.EnumMember => Operand.FromEnum(
                        resolved.Int32Value!.Value,
                        resolved.EnumTypeFullName!,
                        resolved.EnumMemberName!),
                    ConstantValueKind.String => Operand.FromString(resolved.StringValue!),
                    _ => Operand.FromInt32(resolved.Int32Value!.Value),
                });
            case ConstantExpressionStatus.Invalid:
                return FoldOutcome.Error(resolved.DiagnosticCode!, resolved.DiagnosticMessage!);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    private static FoldOutcome FoldInvocation(InvocationExpressionSyntax invocation, FoldContext context)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: { } receiverExpression,
                Name: IdentifierNameSyntax method,
            })
        {
            return FoldOutcome.NotArithmetic();
        }

        var arguments = new List<Operand>(invocation.ArgumentList.Arguments.Count);
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.NameColon is not null || argument.RefKindKeyword != default)
            {
                return FoldOutcome.NotArithmetic();
            }

            var folded = Fold(argument.Expression, context);
            if (folded.Disposition != FoldDisposition.Folded)
            {
                return folded;
            }

            arguments.Add(folded.Operand);
        }

        var name = method.Identifier.ValueText;
        if (TryReadTypeReceiver(receiverExpression, out var typeReceiver))
        {
            return typeReceiver.Category switch
            {
                TypeReceiverCategory.String => DispatchStaticString(name, arguments),
                TypeReceiverCategory.Char => DispatchStaticChar(name, arguments),
                TypeReceiverCategory.Math => DispatchMath(name, arguments),
                _ => DispatchNumericTypeMethod(typeReceiver.Numeric, name, arguments),
            };
        }

        var receiver = Fold(receiverExpression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return receiver;
        }

        // Every numeric ToString evaluates under the invariant culture, with or without a format string, so the
        // answer never depends on the analysis machine's regional settings.
        if (name == "ToString" &&
            receiver.Operand.Kind is OperandKind.Int32 or OperandKind.Numeric)
        {
            switch (arguments)
            {
                case []:
                    return NumericToString(receiver.Operand, format: null);
                case [{ Kind: OperandKind.String } format]:
                    return NumericToString(receiver.Operand, format.String);
            }
        }

        return receiver.Operand.Kind switch
        {
            OperandKind.String => DispatchInstanceString(receiver.Operand.String!, name, arguments),
            OperandKind.Char => name == "ToString" && arguments.Count == 0
                ? FoldOutcome.Folded(Operand.FromString(receiver.Operand.Char.ToString()))
                : MemberUnsupported(name),
            OperandKind.Boolean => name == "ToString" && arguments.Count == 0
                ? FoldOutcome.Folded(Operand.FromString(receiver.Operand.Boolean ? "True" : "False"))
                : MemberUnsupported(name),
            OperandKind.Enum => name == "ToString" && arguments.Count == 0
                ? FoldOutcome.Folded(Operand.FromString(receiver.Operand.EnumMemberName!))
                : MemberUnsupported(name),
            _ => MemberUnsupported(name),
        };
    }

    private static FoldOutcome DispatchStaticString(string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "Concat" when arguments.Count >= 1:
                var builder = new StringBuilder();
                foreach (var argument in arguments)
                {
                    if (!TryConcatOperand(argument, out var text))
                    {
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            "string.Concat admits only string and char constant arguments.");
                    }

                    builder.Append(text);
                }

                return FoldOutcome.Folded(Operand.FromString(builder.ToString()));
            case "Join" when arguments.Count >= 2 && arguments[0].Kind == OperandKind.String:
                var parts = new List<string>(arguments.Count - 1);
                foreach (var argument in arguments.Skip(1))
                {
                    if (!TryConcatOperand(argument, out var text))
                    {
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            "string.Join admits only string and char constant values.");
                    }

                    parts.Add(text);
                }

                return FoldOutcome.Folded(Operand.FromString(string.Join(arguments[0].String, parts)));
            case "IsNullOrEmpty" when arguments is [{ Kind: OperandKind.String } single]:
                return FoldOutcome.Folded(Operand.FromBoolean(string.IsNullOrEmpty(single.String)));
            case "IsNullOrWhiteSpace" when arguments is [{ Kind: OperandKind.String } single]:
                return FoldOutcome.Folded(Operand.FromBoolean(string.IsNullOrWhiteSpace(single.String)));
            case "Equals" when arguments is
                [{ Kind: OperandKind.String } left, { Kind: OperandKind.String } right]:
                return FoldOutcome.Folded(Operand.FromBoolean(
                    string.Equals(left.String, right.String, StringComparison.Ordinal)));
            case "Equals" when arguments is
                [{ Kind: OperandKind.String } left, { Kind: OperandKind.String } right, { } comparison]:
                return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromBoolean(
                    string.Equals(left.String, right.String, mode))));
            case "CompareOrdinal" when arguments is
                [{ Kind: OperandKind.String } left, { Kind: OperandKind.String } right]:
                return FoldOutcome.Folded(Operand.FromInt32(
                    Math.Sign(string.CompareOrdinal(left.String, right.String))));
            case "Compare":
                return CultureSensitive(name, "string.CompareOrdinal or Equals with StringComparison.Ordinal");
            default:
                return MemberUnsupported(name);
        }
    }

    private static FoldOutcome DispatchStaticChar(string name, List<Operand> arguments)
    {
        if (name is "ToUpperInvariant" or "ToLowerInvariant" && arguments is [{ Kind: OperandKind.Char } mapped])
        {
            return FoldOutcome.Folded(Operand.FromChar(name == "ToUpperInvariant"
                ? char.ToUpperInvariant(mapped.Char)
                : char.ToLowerInvariant(mapped.Char)));
        }

        if (name is "ToUpper" or "ToLower")
        {
            return CultureSensitive(name, $"char.{name}Invariant");
        }

        if (arguments is not [{ Kind: OperandKind.Char } single])
        {
            return MemberUnsupported(name);
        }

        var value = single.Char;
        bool? predicate = name switch
        {
            "IsDigit" => char.IsDigit(value),
            "IsLetter" => char.IsLetter(value),
            "IsLetterOrDigit" => char.IsLetterOrDigit(value),
            "IsWhiteSpace" => char.IsWhiteSpace(value),
            "IsUpper" => char.IsUpper(value),
            "IsLower" => char.IsLower(value),
            "IsPunctuation" => char.IsPunctuation(value),
            "IsSymbol" => char.IsSymbol(value),
            "IsSeparator" => char.IsSeparator(value),
            "IsControl" => char.IsControl(value),
            "IsNumber" => char.IsNumber(value),
            "IsSurrogate" => char.IsSurrogate(value),
            "IsHighSurrogate" => char.IsHighSurrogate(value),
            "IsLowSurrogate" => char.IsLowSurrogate(value),
            "IsAscii" => char.IsAscii(value),
            "IsAsciiDigit" => char.IsAsciiDigit(value),
            "IsAsciiLetter" => char.IsAsciiLetter(value),
            "IsAsciiLetterOrDigit" => char.IsAsciiLetterOrDigit(value),
            "IsAsciiLetterLower" => char.IsAsciiLetterLower(value),
            "IsAsciiLetterUpper" => char.IsAsciiLetterUpper(value),
            "IsAsciiHexDigit" => char.IsAsciiHexDigit(value),
            _ => null,
        };
        return predicate is { } result
            ? FoldOutcome.Folded(Operand.FromBoolean(result))
            : MemberUnsupported(name);
    }

    private static FoldOutcome DispatchInstanceString(string receiver, string name, List<Operand> arguments)
    {
        try
        {
            switch (name)
            {
                case "Length":
                    return MemberUnsupported(name);
                case "ToString" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver));
                case "ToUpperInvariant" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver.ToUpperInvariant()));
                case "ToLowerInvariant" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver.ToLowerInvariant()));
                case "ToUpper" or "ToLower":
                    return CultureSensitive(name, $"{name}Invariant");
                case "Contains" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromBoolean(receiver.Contains(single.Char)));
                case "Contains" when arguments is [{ Kind: OperandKind.String } single]:
                    return FoldOutcome.Folded(Operand.FromBoolean(
                        receiver.Contains(single.String!, StringComparison.Ordinal)));
                case "Contains" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromBoolean(
                        receiver.Contains(single.String!, mode))));
                case "StartsWith" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromBoolean(receiver.StartsWith(single.Char)));
                case "EndsWith" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromBoolean(receiver.EndsWith(single.Char)));
                case "StartsWith" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromBoolean(
                        receiver.StartsWith(single.String!, mode))));
                case "EndsWith" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromBoolean(
                        receiver.EndsWith(single.String!, mode))));
                case "StartsWith" or "EndsWith" when arguments is [{ Kind: OperandKind.String }]:
                    return CultureSensitive(name, $"{name}(text, StringComparison.Ordinal)");
                case "IndexOf" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromInt32(receiver.IndexOf(single.Char)));
                case "IndexOf" when arguments is
                    [{ Kind: OperandKind.Char } single, { Kind: OperandKind.Int32 } start]:
                    return FoldOutcome.Folded(Operand.FromInt32(receiver.IndexOf(single.Char, start.Int32)));
                case "IndexOf" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromInt32(
                        receiver.IndexOf(single.String!, mode))));
                case "LastIndexOf" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromInt32(receiver.LastIndexOf(single.Char)));
                case "LastIndexOf" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromInt32(
                        receiver.LastIndexOf(single.String!, mode))));
                case "IndexOf" or "LastIndexOf" when arguments is [{ Kind: OperandKind.String }]:
                    return CultureSensitive(name, $"{name}(text, StringComparison.Ordinal)");
                case "Substring" when arguments is [{ Kind: OperandKind.Int32 } start]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Substring(start.Int32)));
                case "Substring" when arguments is
                    [{ Kind: OperandKind.Int32 } start, { Kind: OperandKind.Int32 } length]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Substring(start.Int32, length.Int32)));
                case "Replace" when arguments is
                    [{ Kind: OperandKind.Char } oldChar, { Kind: OperandKind.Char } newChar]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Replace(oldChar.Char, newChar.Char)));
                case "Replace" when arguments is
                    [{ Kind: OperandKind.String } oldText, { Kind: OperandKind.String } newText]:
                    return FoldOutcome.Folded(Operand.FromString(
                        receiver.Replace(oldText.String!, newText.String!, StringComparison.Ordinal)));
                case "Trim" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Trim()));
                case "TrimStart" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver.TrimStart()));
                case "TrimEnd" when arguments.Count == 0:
                    return FoldOutcome.Folded(Operand.FromString(receiver.TrimEnd()));
                case "Trim" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Trim(single.Char)));
                case "TrimStart" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.TrimStart(single.Char)));
                case "TrimEnd" when arguments is [{ Kind: OperandKind.Char } single]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.TrimEnd(single.Char)));
                case "PadLeft" when arguments is [{ Kind: OperandKind.Int32 } width]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.PadLeft(width.Int32)));
                case "PadLeft" when arguments is
                    [{ Kind: OperandKind.Int32 } width, { Kind: OperandKind.Char } padding]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.PadLeft(width.Int32, padding.Char)));
                case "PadRight" when arguments is [{ Kind: OperandKind.Int32 } width]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.PadRight(width.Int32)));
                case "PadRight" when arguments is
                    [{ Kind: OperandKind.Int32 } width, { Kind: OperandKind.Char } padding]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.PadRight(width.Int32, padding.Char)));
                case "Insert" when arguments is
                    [{ Kind: OperandKind.Int32 } start, { Kind: OperandKind.String } text]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Insert(start.Int32, text.String!)));
                case "Remove" when arguments is [{ Kind: OperandKind.Int32 } start]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Remove(start.Int32)));
                case "Remove" when arguments is
                    [{ Kind: OperandKind.Int32 } start, { Kind: OperandKind.Int32 } length]:
                    return FoldOutcome.Folded(Operand.FromString(receiver.Remove(start.Int32, length.Int32)));
                case "Equals" when arguments is [{ Kind: OperandKind.String } single]:
                    return FoldOutcome.Folded(Operand.FromBoolean(
                        string.Equals(receiver, single.String, StringComparison.Ordinal)));
                case "Equals" when arguments is [{ Kind: OperandKind.String } single, { } comparison]:
                    return WithComparison(comparison, mode => FoldOutcome.Folded(Operand.FromBoolean(
                        string.Equals(receiver, single.String, mode))));
                case "GetHashCode":
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "String hash codes are randomized per process and are deliberately not evaluated.");
                case "CompareTo":
                    return CultureSensitive(name, "string.CompareOrdinal");
                default:
                    return MemberUnsupported(name);
            }
        }
        catch (ArgumentException exception)
        {
            // The thrown exception's own type name is the diagnostic code, so an out-of-range substring reads
            // exactly like the exception the same call would produce in running code.
            return FoldOutcome.Error(exception.GetType().FullName!, exception.Message);
        }
    }

    private static FoldOutcome WithComparison(Operand comparison, Func<StringComparison, FoldOutcome> evaluate)
    {
        if (comparison is not
            {
                Kind: OperandKind.Enum,
                EnumTypeFullName: "System.StringComparison",
            })
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "The comparison argument must be a System.StringComparison enum member.");
        }

        return comparison.Int32 switch
        {
            (int)StringComparison.Ordinal => evaluate(StringComparison.Ordinal),
            (int)StringComparison.OrdinalIgnoreCase => evaluate(StringComparison.OrdinalIgnoreCase),
            _ => CultureSensitive(
                "StringComparison." + (comparison.EnumMemberName ?? comparison.Int32.ToString(CultureInfo.InvariantCulture)),
                "StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase"),
        };
    }

    private static FoldOutcome CultureSensitive(string member, string deterministicAlternative) =>
        FoldOutcome.Error(
            CultureSensitiveCode,
            $"'{member}' is culture-sensitive and not deterministic across machines; use {deterministicAlternative}.");

    private static FoldOutcome MemberUnsupported(string member) =>
        FoldOutcome.Error(
            MemberUnsupportedCode,
            $"'{member}' is not an admitted deterministic constant member.");

    private static bool TryConcatOperand(Operand operand, out string text)
    {
        switch (operand.Kind)
        {
            case OperandKind.String:
                text = operand.String!;
                return true;
            case OperandKind.Char:
                text = operand.Char.ToString();
                return true;
            case OperandKind.Int32:
            case OperandKind.Numeric:
                text = FormatNumeric(NumericKindOf(operand), BoxOf(operand));
                return true;
            case OperandKind.Boolean:
                text = operand.Boolean ? "True" : "False";
                return true;
            case OperandKind.Enum:
                text = operand.EnumMemberName!;
                return true;
            case OperandKind.Null:
                text = string.Empty;
                return true;
            default:
                text = string.Empty;
                return false;
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
                charValue: null,
                booleanValue: null,
                single.EnumTypeFullName,
                single.EnumMemberName,
                single.UnderlyingTypeName,
                single.ModuleName,
                single.ModuleContentSha256,
                single.TypeToken,
                single.FieldToken,
                scanned,
                session.Modules.Length,
                metadataLiteralsConsumed: 1,
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
                        charValue: null,
                        booleanValue: null,
                        isEnum ? fullTypeName : null,
                        isEnum ? memberName : null,
                        constant.TypeCode.ToString(),
                        moduleName,
                        metadataSha256,
                        MetadataTokens.GetToken(typeHandle),
                        MetadataTokens.GetToken(fieldHandle),
                        modulesScanned: 0,
                        moduleCount: 0,
                        metadataLiteralsConsumed: 1,
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
                        charValue: null,
                        booleanValue: null,
                        enumTypeFullName: null,
                        enumMemberName: null,
                        ConstantTypeCode.String.ToString(),
                        moduleName,
                        metadataSha256,
                        MetadataTokens.GetToken(typeHandle),
                        MetadataTokens.GetToken(fieldHandle),
                        modulesScanned: 0,
                        moduleCount: 0,
                        metadataLiteralsConsumed: 1,
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
