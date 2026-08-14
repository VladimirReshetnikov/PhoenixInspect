using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ExceptionServices;
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

    /// <summary>Constant-shaped evaluation was blocked because required dump authority was unavailable.</summary>
    Unavailable = 4,
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

    /// <summary>
    /// A virtual array sequence: elements produced by an array-creating BCL member, an array initializer
    /// expression, a <c>System.Linq.Enumerable</c> transformation, or a read-only array read from the dump heap.
    /// The sequence exists only while the expression evaluates and is never persisted. The element type is named by
    /// <see cref="ConstantExpressionEvaluation.ValueTypeName"/> and the rendered elements by
    /// <see cref="ConstantExpressionEvaluation.ValueText"/>.
    /// </summary>
    Sequence = 8,

    /// <summary>
    /// A date or time value: <see cref="System.DateTime"/>, <see cref="System.DateTimeOffset"/>,
    /// <see cref="System.TimeSpan"/>, <see cref="System.DateOnly"/>, or <see cref="System.TimeOnly"/>, computed
    /// with the exact BCL semantics under the invariant culture. The exact kind is named by
    /// <see cref="ConstantExpressionEvaluation.ValueTypeName"/> and the round-trip text by
    /// <see cref="ConstantExpressionEvaluation.ValueText"/>.
    /// </summary>
    Temporal = 9,

    /// <summary>
    /// A deterministic BCL value outside the other domains — <see cref="System.Guid"/> or
    /// <see cref="System.Version"/> — computed with the exact BCL semantics and rendered in its invariant text
    /// form. The exact kind is named by <see cref="ConstantExpressionEvaluation.ValueTypeName"/> and the text by
    /// <see cref="ConstantExpressionEvaluation.ValueText"/>.
    /// </summary>
    BclValue = 10,

    /// <summary>
    /// A <c>typeof(...)</c> reference to a type the evaluator models: a primitive, string, char, Boolean, a date
    /// or time kind, <see cref="System.Guid"/>, <see cref="System.Version"/>, or an enum — including enums
    /// declared in dump modules. The display text is <c>typeof(...)</c> with the C# spelling in
    /// <see cref="ConstantExpressionEvaluation.ValueText"/>.
    /// </summary>
    Type = 11,

    /// <summary>
    /// A tuple value folded from a tuple literal, with C#'s element-wise semantics. The shape is named by
    /// <see cref="ConstantExpressionEvaluation.ValueTypeName"/> (such as <c>(Int32, String)</c>), the rendered
    /// form by <see cref="ConstantExpressionEvaluation.ValueText"/>, and the elements are exposed as structured
    /// <see cref="ConstantExpressionEvaluation.Children"/>.
    /// </summary>
    Tuple = 12,

    /// <summary>
    /// An anonymous object folded from <c>new { … }</c>, with C#'s member semantics. The shape is named by
    /// <see cref="ConstantExpressionEvaluation.ValueTypeName"/> (such as <c>new { Name, Total }</c>), the
    /// rendered form by <see cref="ConstantExpressionEvaluation.ValueText"/>, and the members are exposed as
    /// structured <see cref="ConstantExpressionEvaluation.Children"/>.
    /// </summary>
    Anonymous = 13,
}

/// <summary>
/// One structured child of a compound constant value — a sequence element or tuple element — realized for
/// expandable display: <c>[i]</c> rows for sequences, <c>ItemN</c> or declared-name rows for tuples, recursively
/// for nested compounds.
/// </summary>
/// <param name="Name">The child's display name.</param>
/// <param name="ValueText">The child's rendered value.</param>
/// <param name="ValueTypeName">The child's display type name, or null for a null element.</param>
/// <param name="Children">The child's own structured children; empty for scalars.</param>
public sealed record ConstantValueChild(
    string Name,
    string ValueText,
    string? ValueTypeName,
    ImmutableArray<ConstantValueChild> Children);

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
        int dumpValuesConsumed = 0,
        ImmutableArray<ConstantValueChild> children = default,
        ClrmdModuleEditAdmission? moduleEditAdmission = null)
    {
        ValueTypeName = valueTypeName;
        ValueText = valueText;
        DumpValuesConsumed = dumpValuesConsumed;
        Children = children.IsDefault ? [] : children;
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
        ModuleEditAdmission = moduleEditAdmission;
        if (moduleEditAdmission is { } admission)
        {
            var expectedStatus = admission.Disposition == ClrmdModuleEditAdmissionDisposition.Invalid
                ? ConstantExpressionStatus.Invalid
                : ConstantExpressionStatus.Unavailable;
            if (admission.IsAdmitted || status != expectedStatus || kind != ConstantValueKind.None ||
                moduleName is not null || moduleContentSha256 is not null || typeToken is not null ||
                fieldToken is not null || modulesScanned != 0 || moduleCount != admission.TotalModuleCount ||
                metadataLiteralsConsumed != 0 || dumpValuesConsumed != 0 ||
                !string.Equals(diagnosticCode, ModuleEditAdmissionPolicy.Code(admission), StringComparison.Ordinal))
            {
                throw new ArgumentException("The constant admission result and retained Host refusal disagree.");
            }
        }
        else if (status == ConstantExpressionStatus.Unavailable)
        {
            throw new ArgumentException("An unavailable constant result requires a retained admission refusal.");
        }
        Sha256 = ComputeSha256();
    }

    /// <summary>Gets the evaluation disposition.</summary>
    public ConstantExpressionStatus Status { get; }

    /// <summary>Gets the exact raw expression text.</summary>
    public string Expression { get; }

    /// <summary>Gets the value domain of an exact result; otherwise <see cref="ConstantValueKind.None"/>.</summary>
    public ConstantValueKind Kind { get; }

    /// <summary>
    /// Gets the exact Int32 value for arithmetic, integer literal fields, and enum members; for a
    /// <see cref="ConstantValueKind.Sequence"/> result it carries the exact element count.
    /// </summary>
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

    /// <summary>
    /// Gets the structured children of a compound value — sequence elements and tuple elements — realized for
    /// expandable display; empty for scalar values. Children are a bounded projection of the same payload the
    /// rendered <see cref="ValueText"/> shows, so they never affect the canonical replay digest.
    /// </summary>
    public ImmutableArray<ConstantValueChild> Children { get; }

    /// <summary>Gets the stable diagnostic code for an invalid or unavailable outcome; otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the artifact-independent explanation for an invalid or unavailable outcome; otherwise null.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets the cached Host admission refusal when session authority blocked constant evaluation.</summary>
    public ClrmdModuleEditAdmission? ModuleEditAdmission { get; }

    /// <summary>
    /// Gets the exact operand projection the evaluator attached for the value domains the public scalar fields
    /// cannot reconstruct — sequences, boxed numerics, temporal values, and BCL values — or null when the value
    /// is a legacy scalar or has no operand carrier. Derived from the same payload as the rendered value, so it
    /// never participates in the canonical replay digest.
    /// </summary>
    internal ConstantOperandResolution? ExactProjection { get; init; }

    /// <summary>Gets the lowercase SHA-256 identity of the canonical outcome projection.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Projects this exact result into a reusable operand resolution, so a scratchpad may store it as a variable
    /// value and feed it back into later expressions.
    /// </summary>
    /// <param name="stored">The stored value on success, otherwise null.</param>
    /// <returns><see langword="true"/> when the value is a scalar the operand domain carries.</returns>
    /// <remarks>
    /// The kinds the operand resolution carries round-trip: the integral, floating, Boolean, char, string,
    /// enum-as-underlying, and null scalars; every numeric domain including decimals and wide integers; date and
    /// time values; deterministic BCL values such as Guid, Version, Encoding, and the Regex family; and
    /// sequences of any of those element domains. A tuple, anonymous, grouping, or type result has no operand
    /// carrier and cannot be stored, so the caller reports it as an unsupported variable value rather than
    /// silently truncating it.
    /// </remarks>
    public bool TryProjectStoredValue(out ConstantOperandResolution? stored)
    {
        stored = null;
        if (Status != ConstantExpressionStatus.Exact)
        {
            return false;
        }

        // The attached projection is exact by construction; the scalar switch below reconstructs the kinds that
        // predate it from the public fields, so evaluations built by the literal-field paths keep storing.
        if (ExactProjection is { } exact)
        {
            stored = exact;
            return true;
        }

        switch (Kind)
        {
            case ConstantValueKind.Int32:
            case ConstantValueKind.EnumMember:
                stored = ConstantOperandResolution.FromInt32(Int32Value!.Value);
                return true;
            case ConstantValueKind.String:
                stored = ConstantOperandResolution.FromString(StringValue!);
                return true;
            case ConstantValueKind.Char:
                stored = ConstantOperandResolution.FromChar(CharValue!.Value);
                return true;
            case ConstantValueKind.Boolean:
                stored = ConstantOperandResolution.FromBoolean(BooleanValue!.Value);
                return true;
            case ConstantValueKind.Null:
                stored = ConstantOperandResolution.ExactNull();
                return true;
            case ConstantValueKind.Numeric:
                return TryProjectNumeric(out stored);
            default:
                return false;
        }
    }

    private bool TryProjectNumeric(out ConstantOperandResolution? stored)
    {
        stored = ValueTypeName switch
        {
            "Int64" when long.TryParse(ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) =>
                ConstantOperandResolution.FromInt64(l),
            "Double" when double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) =>
                ConstantOperandResolution.FromDouble(d),
            "Single" when float.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) =>
                ConstantOperandResolution.FromSingle(f),
            _ => null,
        };
        return stored is not null;
    }

    /// <summary>Gets the display type name of this exact scalar value, for a variable declaration echo.</summary>
    public string? StoredValueTypeName => Kind switch
    {
        ConstantValueKind.Int32 => "Int32",
        ConstantValueKind.EnumMember => EnumTypeFullName,
        ConstantValueKind.String => "String",
        ConstantValueKind.Char => "Char",
        ConstantValueKind.Boolean => "Boolean",
        ConstantValueKind.Numeric => ValueTypeName,
        ConstantValueKind.Null => "null",
        ConstantValueKind.Sequence => ValueTypeName,
        ConstantValueKind.Temporal => ValueTypeName,
        ConstantValueKind.BclValue => ValueTypeName,
        _ => null,
    };

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
        int metadataLiteralsConsumed = 0,
        int dumpValuesConsumed = 0) => new(
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
        message,
        dumpValuesConsumed: dumpValuesConsumed);

    internal static ConstantExpressionEvaluation AdmissionResult(
        string expression,
        ClrmdModuleEditAdmission admission) => new(
        admission.Disposition == ClrmdModuleEditAdmissionDisposition.Invalid
            ? ConstantExpressionStatus.Invalid
            : ConstantExpressionStatus.Unavailable,
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
        moduleCount: admission.TotalModuleCount,
        metadataLiteralsConsumed: 0,
        diagnosticCode: ModuleEditAdmissionPolicy.Code(admission),
        diagnosticMessage: ModuleEditAdmissionPolicy.Message(admission),
        moduleEditAdmission: admission);

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
        // Preserve every pre-existing hash literally; only the new admission result appends its typed authority arm.
        if (ModuleEditAdmission is not null)
        {
            Append(builder, "module-edit-admission-v1");
            Append(builder, ((int)ModuleEditAdmission.Disposition).ToString(CultureInfo.InvariantCulture));
            Append(builder, ((int)ModuleEditAdmission.Status).ToString(CultureInfo.InvariantCulture));
            Append(builder, ((int)ModuleEditAdmission.Issue).ToString(CultureInfo.InvariantCulture));
            Append(builder, ModuleEditAdmission.InspectedModuleCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, ModuleEditAdmission.TotalModuleCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, ModuleEditAdmission.StoppedModule?.Identity.SourceId ?? "none");
            foreach (var read in ModuleEditAdmission.Evidence)
            {
                Append(builder, read.SourceId);
                Append(builder, read.Address.ToString("x16", CultureInfo.InvariantCulture));
                Append(builder, read.RequestedLength.ToString(CultureInfo.InvariantCulture));
                Append(builder, ((int)read.Status).ToString(CultureInfo.InvariantCulture));
                Append(builder, Convert.ToHexString(read.Bytes.AsSpan()).ToLowerInvariant());
            }
        }
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

/// <summary>Identifies how one dump-value operand resolution ended.</summary>
public enum ConstantOperandResolutionKind
{
    /// <summary>The name is outside the resolver's domain; the whole expression stays not-constant.</summary>
    Outside = 0,

    /// <summary>The operand resolved to an exact Int32 value, including a Nullable&lt;Int32&gt; holding one.</summary>
    Int32 = 1,

    /// <summary>The operand resolved to an exact complete string value.</summary>
    String = 2,

    /// <summary>The operand resolved to exactly null: a null reference or a Nullable without a value.</summary>
    Null = 3,

    /// <summary>The operand resolved but cannot participate; the expression becomes a typed stop.</summary>
    Stop = 4,

    /// <summary>The operand resolved to an exact Int64 value.</summary>
    Int64 = 5,

    /// <summary>The operand resolved to an exact IEEE-754 double.</summary>
    Double = 6,

    /// <summary>The operand resolved to an exact IEEE-754 single.</summary>
    Single = 7,

    /// <summary>The operand resolved to an exact Boolean.</summary>
    Boolean = 8,

    /// <summary>The operand resolved to an exact UTF-16 code unit.</summary>
    Char = 9,

    /// <summary>
    /// The operand resolved to a read-only array from the dump heap, materialized as a virtual sequence whose
    /// elements are themselves scalar resolutions.
    /// </summary>
    Sequence = 10,

    /// <summary>
    /// The operand resolved to an exact numeric value outside the dedicated kinds — a byte, a decimal, a wide
    /// integer — carried as its exact boxed CLR value with its numeric domain name.
    /// </summary>
    Numeric = 11,

    /// <summary>
    /// The operand resolved to an exact date or time value, carried as its exact boxed BCL struct with its
    /// temporal domain name.
    /// </summary>
    Temporal = 12,

    /// <summary>
    /// The operand resolved to an exact deterministic BCL value — a Guid, Version, Encoding, or Regex-family
    /// value — carried as its exact boxed BCL object with its value domain name.
    /// </summary>
    BclValue = 13,
}

/// <summary>One value extracted from the dump for use as an operand inside a composed expression.</summary>
public sealed class ConstantOperandResolution
{
    private ConstantOperandResolution(
        ConstantOperandResolutionKind kind,
        int? int32Value,
        string? stringValue,
        string? diagnosticCode,
        string? diagnosticMessage,
        long? int64Value = null,
        double? doubleValue = null,
        float? singleValue = null,
        bool? booleanValue = null,
        char? charValue = null,
        ImmutableArray<ConstantOperandResolution> elements = default,
        string? elementTypeName = null,
        string? valueTypeName = null,
        object? boxedValue = null)
    {
        Kind = kind;
        Int32Value = int32Value;
        StringValue = stringValue;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        Int64Value = int64Value;
        DoubleValue = doubleValue;
        SingleValue = singleValue;
        BooleanValue = booleanValue;
        CharValue = charValue;
        Elements = elements.IsDefault ? [] : elements;
        ElementTypeName = elementTypeName;
        ValueTypeName = valueTypeName;
        BoxedValue = boxedValue;
    }

    /// <summary>Gets the resolution discriminator.</summary>
    public ConstantOperandResolutionKind Kind { get; }

    /// <summary>Gets the exact Int32 value only for <see cref="ConstantOperandResolutionKind.Int32"/>.</summary>
    public int? Int32Value { get; }

    /// <summary>Gets the exact string value only for <see cref="ConstantOperandResolutionKind.String"/>.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the stable diagnostic code only for <see cref="ConstantOperandResolutionKind.Stop"/>.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the stop explanation only for <see cref="ConstantOperandResolutionKind.Stop"/>.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets the exact Int64 value only for <see cref="ConstantOperandResolutionKind.Int64"/>.</summary>
    public long? Int64Value { get; }

    /// <summary>Gets the exact double value only for <see cref="ConstantOperandResolutionKind.Double"/>.</summary>
    public double? DoubleValue { get; }

    /// <summary>Gets the exact single value only for <see cref="ConstantOperandResolutionKind.Single"/>.</summary>
    public float? SingleValue { get; }

    /// <summary>Gets the exact Boolean value only for <see cref="ConstantOperandResolutionKind.Boolean"/>.</summary>
    public bool? BooleanValue { get; }

    /// <summary>Gets the exact char value only for <see cref="ConstantOperandResolutionKind.Char"/>.</summary>
    public char? CharValue { get; }

    /// <summary>Gets the scalar element resolutions only for <see cref="ConstantOperandResolutionKind.Sequence"/>.</summary>
    public ImmutableArray<ConstantOperandResolution> Elements { get; }

    /// <summary>
    /// Gets the element domain name — a numeric kind name such as <c>Int32</c> or <c>Byte</c>, a temporal or BCL
    /// value kind name such as <c>DateTime</c> or <c>Guid</c>, <c>Boolean</c>, <c>Char</c>, or <c>String</c> —
    /// only for <see cref="ConstantOperandResolutionKind.Sequence"/>.
    /// </summary>
    public string? ElementTypeName { get; }

    /// <summary>
    /// Gets the value domain name — a numeric kind such as <c>Byte</c> or <c>Decimal</c>, a temporal kind such
    /// as <c>DateTime</c>, or a BCL value kind such as <c>Guid</c> — only for the
    /// <see cref="ConstantOperandResolutionKind.Numeric"/>, <see cref="ConstantOperandResolutionKind.Temporal"/>,
    /// and <see cref="ConstantOperandResolutionKind.BclValue"/> kinds.
    /// </summary>
    public string? ValueTypeName { get; }

    /// <summary>
    /// Gets the exact boxed CLR value for the same three kinds. The box is the value the evaluator itself
    /// produced — a <see cref="decimal"/>, a <see cref="DateTime"/>, a <see cref="Guid"/> — handed back
    /// unchanged within the same process, so the round-trip is exact by construction.
    /// </summary>
    public object? BoxedValue { get; }

    /// <summary>Creates an exact Int32 operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>An Int32 resolution.</returns>
    public static ConstantOperandResolution FromInt32(int value) =>
        new(ConstantOperandResolutionKind.Int32, value, null, null, null);

    /// <summary>Creates an exact string operand.</summary>
    /// <param name="value">The exact complete string read from the dump.</param>
    /// <returns>A string resolution.</returns>
    public static ConstantOperandResolution FromString(string value) =>
        new(ConstantOperandResolutionKind.String, null,
            value ?? throw new ArgumentNullException(nameof(value)), null, null);

    /// <summary>Creates an exactly-null operand.</summary>
    /// <returns>A null resolution.</returns>
    public static ConstantOperandResolution ExactNull() =>
        new(ConstantOperandResolutionKind.Null, null, null, null, null);

    /// <summary>Creates a typed stop that halts the composed expression with the sub-expression's own facts.</summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The artifact-independent explanation.</param>
    /// <returns>A stop resolution.</returns>
    public static ConstantOperandResolution ForStop(string code, string message) =>
        new(ConstantOperandResolutionKind.Stop, null, null,
            code ?? throw new ArgumentNullException(nameof(code)),
            message ?? throw new ArgumentNullException(nameof(message)));

    /// <summary>Creates an outside-the-domain resolution that keeps the whole expression not-constant.</summary>
    /// <returns>An outside resolution.</returns>
    public static ConstantOperandResolution OutsideDomain() =>
        new(ConstantOperandResolutionKind.Outside, null, null, null, null);

    /// <summary>Creates an exact Int64 operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>An Int64 resolution.</returns>
    public static ConstantOperandResolution FromInt64(long value) =>
        new(ConstantOperandResolutionKind.Int64, null, null, null, null, int64Value: value);

    /// <summary>Creates an exact double operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>A double resolution.</returns>
    public static ConstantOperandResolution FromDouble(double value) =>
        new(ConstantOperandResolutionKind.Double, null, null, null, null, doubleValue: value);

    /// <summary>Creates an exact single operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>A single resolution.</returns>
    public static ConstantOperandResolution FromSingle(float value) =>
        new(ConstantOperandResolutionKind.Single, null, null, null, null, singleValue: value);

    /// <summary>Creates an exact Boolean operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>A Boolean resolution.</returns>
    public static ConstantOperandResolution FromBoolean(bool value) =>
        new(ConstantOperandResolutionKind.Boolean, null, null, null, null, booleanValue: value);

    /// <summary>Creates an exact char operand.</summary>
    /// <param name="value">The exact value read from the dump.</param>
    /// <returns>A char resolution.</returns>
    public static ConstantOperandResolution FromChar(char value) =>
        new(ConstantOperandResolutionKind.Char, null, null, null, null, charValue: value);

    /// <summary>Creates a read-only array operand materialized from the dump heap.</summary>
    /// <param name="elements">The scalar element resolutions, in array order.</param>
    /// <param name="elementTypeName">The element domain name.</param>
    /// <returns>A sequence resolution.</returns>
    public static ConstantOperandResolution FromSequence(
        ImmutableArray<ConstantOperandResolution> elements,
        string elementTypeName) =>
        new(ConstantOperandResolutionKind.Sequence, null, null, null, null,
            elements: elements,
            elementTypeName: elementTypeName ?? throw new ArgumentNullException(nameof(elementTypeName)));

    /// <summary>Creates an exact numeric operand outside the dedicated Int32/Int64/Double/Single kinds.</summary>
    /// <param name="numericKindName">The numeric domain name, such as <c>Byte</c> or <c>Decimal</c>.</param>
    /// <param name="value">The exact boxed CLR value of that domain.</param>
    /// <returns>A boxed numeric resolution.</returns>
    public static ConstantOperandResolution FromNumericValue(string numericKindName, object value) =>
        new(ConstantOperandResolutionKind.Numeric, null, null, null, null,
            valueTypeName: numericKindName ?? throw new ArgumentNullException(nameof(numericKindName)),
            boxedValue: value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>Creates an exact date or time operand.</summary>
    /// <param name="temporalKindName">The temporal domain name, such as <c>DateTime</c> or <c>TimeSpan</c>.</param>
    /// <param name="value">The exact boxed BCL struct of that domain.</param>
    /// <returns>A boxed temporal resolution.</returns>
    public static ConstantOperandResolution FromTemporalValue(string temporalKindName, object value) =>
        new(ConstantOperandResolutionKind.Temporal, null, null, null, null,
            valueTypeName: temporalKindName ?? throw new ArgumentNullException(nameof(temporalKindName)),
            boxedValue: value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>Creates an exact deterministic BCL value operand.</summary>
    /// <param name="bclValueKindName">The value domain name, such as <c>Guid</c> or <c>Regex</c>.</param>
    /// <param name="value">The exact boxed BCL value of that domain.</param>
    /// <returns>A boxed BCL value resolution.</returns>
    public static ConstantOperandResolution FromBclValue(string bclValueKindName, object value) =>
        new(ConstantOperandResolutionKind.BclValue, null, null, null, null,
            valueTypeName: bclValueKindName ?? throw new ArgumentNullException(nameof(bclValueKindName)),
            boxedValue: value ?? throw new ArgumentNullException(nameof(value)));
}

/// <summary>
/// Caller-supplied bridges that let a composed expression consume values extracted from the dump as operands.
/// </summary>
/// <remarks>
/// The evaluator itself never reads storage. Each bridge delegates one dotted name or one root-relative chain to
/// the caller — the host layer that owns the frozen static-field and root-relative pipelines — and receives back a
/// typed resolution. A bare name or bare chain never consults a bridge: the whole expression stays not-constant so
/// the frozen paths keep answering those exactly as before, with their complete evidence reports.
/// </remarks>
public sealed class ConstantOperandResolvers
{
    /// <summary>Gets the resolver for dotted static-field names, or null when stored statics cannot compose.</summary>
    public Func<string, ConstantOperandResolution>? StaticName { get; init; }

    /// <summary>Gets the resolver for root-relative member chains, or null outside a root-relative evaluation.</summary>
    public Func<string, ConstantOperandResolution>? RootChain { get; init; }

    /// <summary>Gets the case-sensitive root identifier that anchors a root-relative chain, or null.</summary>
    public string? RootIdentifier { get; init; }

    /// <summary>
    /// Gets the resolver for a bare identifier that names a declared session variable, or null when none are
    /// declared. It receives one identifier and returns that variable's stored constant value, or an outside
    /// resolution when the name is not a variable so the identifier keeps its ordinary not-constant path.
    /// </summary>
    public Func<string, ConstantOperandResolution>? LocalName { get; init; }
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
    public static ConstantExpressionEvaluation Evaluate(ClrmdDumpSession? session, string? expression) =>
        Evaluate(session, expression, resolvers: null);

    /// <summary>
    /// The stable diagnostic code of a cooperatively cancelled evaluation: the fold engine observed the host's
    /// cancellation request at one of its safe boundaries and stopped with this typed outcome instead of a value.
    /// </summary>
    public const string CancellationCode = "CONSTANT_EVALUATION_CANCELLED";

    /// <summary>
    /// Attempts to evaluate one raw expression as a constant, optionally consuming values extracted from the dump
    /// as operands through caller-supplied resolvers.
    /// </summary>
    /// <param name="session">
    /// The open dump session used to resolve literal fields; when null, qualified names cannot resolve and only
    /// literal-based evaluation can succeed.
    /// </param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <param name="resolvers">
    /// Bridges to the frozen static-field and root-relative pipelines, or null when only pure constants and
    /// metadata literals may participate. A bare name or bare chain never consults a resolver.
    /// </param>
    /// <returns>An exact value, a typed constant-domain error, or a not-constant disposition.</returns>
    public static ConstantExpressionEvaluation Evaluate(
        ClrmdDumpSession? session,
        string? expression,
        ConstantOperandResolvers? resolvers) =>
        RunEvaluationPass(
            session,
            expression,
            resolvers,
            recordDeferredSessionAuthority: false).Evaluation;

    /// <summary>
    /// Attempts to evaluate one raw expression as a constant, cooperating with a host cancellation token: the
    /// fold engine observes the token at its safe boundaries — every sub-expression fold, every per-element
    /// lambda application, every delegate entry, and the comparison loops of the set operators — and stops with
    /// the typed <see cref="CancellationCode"/> outcome. Nothing is aborted; the pass simply unwinds.
    /// </summary>
    /// <param name="session">The open dump session, or null when only references and literals may resolve.</param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <param name="resolvers">Bridges to the frozen dump pipelines, or null.</param>
    /// <param name="references">Caller-referenced assemblies, empty by default.</param>
    /// <param name="usings">The active using directives, or null for none.</param>
    /// <param name="cancellationToken">The token the fold engine cooperates with.</param>
    /// <returns>An exact value, a typed constant-domain error, or a not-constant disposition.</returns>
    public static ConstantExpressionEvaluation Evaluate(
        ClrmdDumpSession? session,
        string? expression,
        ConstantOperandResolvers? resolvers,
        ImmutableArray<ConstantReferenceAssembly> references,
        ConstantUsingDirectiveSet? usings,
        CancellationToken cancellationToken) =>
        RunEvaluationPass(
            session,
            expression,
            resolvers,
            recordDeferredSessionAuthority: false,
            references,
            usings,
            cancellationToken).Evaluation;

    /// <summary>
    /// Attempts to evaluate one raw expression as a constant, additionally resolving names through caller-referenced
    /// assemblies.
    /// </summary>
    /// <param name="session">The open dump session, or null when only references and literals may resolve.</param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <param name="resolvers">Bridges to the frozen dump pipelines, or null.</param>
    /// <param name="references">
    /// Caller-referenced assemblies whose literal fields, enum declarations, and type names participate in
    /// resolution. An unaliased reference joins the global scope beside the session's modules; an aliased
    /// reference is reachable only through its alias qualifier.
    /// </param>
    /// <param name="usings">
    /// The active <c>using</c> directives, or null for none. They expand a prefix-less name into the candidate
    /// fully qualified names the resolvers try, so an imported namespace, statically imported type, or alias binds
    /// a short name exactly as it would in source.
    /// </param>
    /// <returns>An exact value, a typed constant-domain error, or a not-constant disposition.</returns>
    public static ConstantExpressionEvaluation Evaluate(
        ClrmdDumpSession? session,
        string? expression,
        ConstantOperandResolvers? resolvers,
        ImmutableArray<ConstantReferenceAssembly> references,
        ConstantUsingDirectiveSet? usings = null) =>
        RunEvaluationPass(
            session,
            expression,
            resolvers,
            recordDeferredSessionAuthority: false,
            references,
            usings).Evaluation;

    /// <summary>
    /// The constant evaluator's own input bounds, wider than the dump-query wire profile's: the wire profile is
    /// a frozen replay contract over snapshot requests, while these bound only the in-process interpreter,
    /// whose expressions — combinators, nested delegates — legitimately outgrow the query grammar's budget.
    /// </summary>
    private const int EvaluatorMaximumExpressionLength = 2048;

    private const int EvaluatorMaximumNodeTokenCount = 2048;

    /// <summary>The stack size of the dedicated evaluation thread.</summary>
    /// <remarks>
    /// Folding is recursive descent, and delegate recursion multiplies its depth; a controlled 16 MB stack plus
    /// the delegate-invocation depth bound gives a deterministic budget — the typed depth stop always fires
    /// before the physical stack can, which a caller-thread stack of unknown size could not guarantee.
    /// </remarks>
    private const int EvaluationStackBytes = 16 * 1024 * 1024;

    [ThreadStatic]
    private static bool onEvaluationThread;

    // The evaluation thread's ambient cancellation token. The fold engine's hot paths — element-comparison loops,
    // regex dispatch — sit several static hops below any context-carrying frame, so the token rides the dedicated
    // evaluation thread instead of every signature. Each pass owns its thread, and a nested pass on the same
    // thread restores the outer token, so the scope is exactly one evaluation.
    [ThreadStatic]
    private static CancellationToken evaluationCancellation;

    /// <summary>
    /// Produces the typed cancellation stop when the host has requested cancellation, or null to continue. Every
    /// safe boundary calls this; the resulting error outcome unwinds through the ordinary disposition checks, so
    /// cancellation is always a returned result, never an abort.
    /// </summary>
    private static FoldOutcome? CancellationStop() =>
        evaluationCancellation.IsCancellationRequested
            ? FoldOutcome.Error(
                CancellationCode,
                "The evaluation observed the host's cancellation request at a safe boundary; no value was produced.")
            : null;

    private static EvaluationPassResult RunEvaluationPass(
        ClrmdDumpSession? session,
        string? expression,
        ConstantOperandResolvers? resolvers,
        bool recordDeferredSessionAuthority,
        ImmutableArray<ConstantReferenceAssembly> references = default,
        ConstantUsingDirectiveSet? usings = null,
        CancellationToken cancellationToken = default)
    {
        if (onEvaluationThread)
        {
            // A nested pass — a resolver re-entering the evaluator — keeps the outer pass's token unless it
            // carries its own, and always restores the outer one on the way out.
            var outerCancellation = evaluationCancellation;
            if (cancellationToken.CanBeCanceled)
            {
                evaluationCancellation = cancellationToken;
            }

            try
            {
                return RunEvaluationPassCore(
                    session, expression, resolvers, recordDeferredSessionAuthority, references, usings);
            }
            finally
            {
                evaluationCancellation = outerCancellation;
            }
        }

        var result = default(EvaluationPassResult);
        ExceptionDispatchInfo? failure = null;
        var evaluation = new Thread(
            () =>
            {
                onEvaluationThread = true;
                evaluationCancellation = cancellationToken;
                try
                {
                    result = RunEvaluationPassCore(
                        session, expression, resolvers, recordDeferredSessionAuthority, references, usings);
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            },
            EvaluationStackBytes)
        {
            IsBackground = true,
            Name = "PhoenixInspect.ConstantEvaluation",
        };
        evaluation.Start();
        evaluation.Join();
        failure?.Throw();
        return result;
    }

    private static EvaluationPassResult RunEvaluationPassCore(
        ClrmdDumpSession? session,
        string? expression,
        ConstantOperandResolvers? resolvers,
        bool recordDeferredSessionAuthority,
        ImmutableArray<ConstantReferenceAssembly> references = default,
        ConstantUsingDirectiveSet? usings = null)
    {
        var effectiveUsings = usings ?? ConstantUsingDirectiveSet.Empty;
        if (string.IsNullOrWhiteSpace(expression) ||
            expression.Length > EvaluatorMaximumExpressionLength)
        {
            return EvaluationPassResult.Completed(
                ConstantExpressionEvaluation.NotConstantResult(expression ?? string.Empty));
        }

        ExpressionSyntax syntax;
        try
        {
            syntax = CSharpExpressionFrontEnd.ParseCompleteExpression(expression);
        }
        catch (ArgumentException)
        {
            return EvaluationPassResult.Completed(ConstantExpressionEvaluation.NotConstantResult(expression));
        }

        if (syntax.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            syntax.DescendantTokens(descendIntoTrivia: true).Any(static token => token.IsMissing))
        {
            return EvaluationPassResult.Completed(ConstantExpressionEvaluation.NotConstantResult(expression));
        }

        if (syntax.DescendantNodesAndTokensAndSelf(descendIntoTrivia: false).Count() >
            EvaluatorMaximumNodeTokenCount)
        {
            return EvaluationPassResult.Completed(ConstantExpressionEvaluation.NotConstantResult(expression));
        }

        // A bare root-relative expression — the root alone, a chain, an invocation, an element access, or any of
        // those under a coalescing fallback — keeps its frozen evaluation path untouched, with its complete
        // evidence report and replay identity. Only composed expressions consume dump values as operands.
        if (resolvers?.RootIdentifier is { } bareRootIdentifier &&
            IsBareDumpExpression(syntax, bareRootIdentifier))
        {
            return EvaluationPassResult.Completed(ConstantExpressionEvaluation.NotConstantResult(expression));
        }

        if (session is not null)
        {
            // Preserve the evidence-free subset (including its typed arithmetic/member errors) before consulting
            // session admission. The null-session probe cannot read metadata or invoke dump resolvers, and it records
            // when an otherwise-invalid fold reached the one resolver whose answer can change with module metadata.
            // References are session-independent authority, so they participate in the probe: a reference-declared
            // literal or enum answers before, and without, module-edit admission.
            var pure = RunEvaluationPass(
                session: null,
                expression,
                resolvers: null,
                recordDeferredSessionAuthority: true,
                references,
                effectiveUsings);
            if (!pure.DeferredSessionAuthority &&
                pure.Evaluation.Status != ConstantExpressionStatus.NotConstant)
            {
                return pure;
            }

            var admission = session.ReadModuleEditAdmission();
            if (!admission.IsAdmitted)
            {
                return EvaluationPassResult.Completed(
                    ConstantExpressionEvaluation.AdmissionResult(expression, admission));
            }
        }

        // A bare qualified-name chain keeps its dedicated literal-field path so the result retains complete
        // module and token facts; a stored static field keeps falling through to the frozen pipeline. A chain
        // whose receiver or head is a known BCL type — 'Int32.MaxValue', 'System.Math.PI',
        // 'Guid.Empty.Version' — folds instead, because those members are type statics and their values, not
        // metadata literals declared in dump modules; and a chain ending in '.Length' folds so an array or
        // string field's length can answer.
        // A declared session variable is a lone identifier that shadows everything, exactly as a local shadows an
        // import in source, so it is never treated as a static-import literal candidate and instead folds through
        // its resolver below. A lone identifier is otherwise a literal candidate only when a static import could
        // promote it.
        var namesDeclaredVariable =
            syntax is IdentifierNameSyntax bareIdentifier &&
            resolvers?.LocalName is { } declaredVariableResolver &&
            declaredVariableResolver(bareIdentifier.Identifier.ValueText).Kind !=
                ConstantOperandResolutionKind.Outside;
        var allowSingleIdentifier = !effectiveUsings.IsEmpty && !namesDeclaredVariable;
        if (TryReadQualifiedName(syntax, out var nameParts, out var nameAlias, allowSingleIdentifier) &&
            nameParts[^1] != "Length" &&
            !IsKnownTypeHead(nameParts) &&
            !(syntax is MemberAccessExpressionSyntax typeStaticCandidate &&
                TryReadTypeReceiver(typeStaticCandidate.Expression, out _)) &&
            // A chain whose head names a declared variable is member access over that variable's value, never a
            // literal-field candidate: the variable shadows the import scope exactly as a local shadows an
            // import in source, so 'g.Version' reads the stored Guid rather than scanning for a static.
            !(nameAlias is null &&
                resolvers?.LocalName is { } chainHeadResolver &&
                chainHeadResolver(nameParts[0]).Kind != ConstantOperandResolutionKind.Outside))
        {
            // An alias-qualified name resolves only through the references carrying that alias, never through the
            // session's modules, exactly as extern-alias scoping works in source. An unqualified name expands
            // through the active using directives before it resolves.
            var applicableReferences = ApplicableReferences(references, nameAlias);
            if (session is null && applicableReferences.IsEmpty)
            {
                return EvaluationPassResult.Completed(ConstantExpressionEvaluation.NotConstantResult(expression));
            }

            var candidates = nameAlias is null
                ? effectiveUsings.ExpandMemberCandidates(nameParts)
                : [string.Join('.', nameParts)];
            return EvaluationPassResult.Completed(ResolveLiteralFieldCandidates(
                nameAlias is null ? session : null,
                applicableReferences,
                expression,
                candidates));
        }

        var context = new FoldContext(
            session,
            resolvers,
            recordDeferredSessionAuthority,
            references,
            effectiveUsings);
        var outcome = Fold(syntax, context);
        var evaluation = outcome.Disposition switch
        {
            FoldDisposition.Folded => FromOperand(expression, outcome.Operand, context),
            FoldDisposition.Error => ConstantExpressionEvaluation.InvalidResult(
                expression,
                outcome.Code!,
                outcome.Message!,
                metadataLiteralsConsumed: context.MetadataLiteralsConsumed,
                dumpValuesConsumed: context.DumpValuesConsumed),
            _ => ConstantExpressionEvaluation.NotConstantResult(expression),
        };
        return new EvaluationPassResult(evaluation, context.DeferredSessionAuthority);
    }

    private readonly record struct EvaluationPassResult(
        ConstantExpressionEvaluation Evaluation,
        bool DeferredSessionAuthority)
    {
        internal static EvaluationPassResult Completed(ConstantExpressionEvaluation evaluation) =>
            new(evaluation, DeferredSessionAuthority: false);
    }

    private sealed class FoldContext(
        ClrmdDumpSession? session,
        ConstantOperandResolvers? resolvers,
        bool recordDeferredSessionAuthority,
        ImmutableArray<ConstantReferenceAssembly> references = default,
        ConstantUsingDirectiveSet? usings = null)
    {
        // Lambda-parameter bindings, innermost last. Folding is single-threaded recursive descent, so a simple
        // push/pop stack gives correct lexical scoping and shadowing without allocating per scope.
        private readonly List<(string Name, Operand Value)> bindings = [];

        internal ClrmdDumpSession? Session { get; } = session;

        internal ConstantOperandResolvers? Resolvers { get; } = resolvers;

        internal ImmutableArray<ConstantReferenceAssembly> References { get; } =
            references.IsDefault ? [] : references;

        internal ConstantUsingDirectiveSet Usings { get; } = usings ?? ConstantUsingDirectiveSet.Empty;

        internal bool DeferredSessionAuthority { get; private set; }

        internal int MetadataLiteralsConsumed { get; set; }

        internal int DumpValuesConsumed { get; set; }

        internal void DeferSessionAuthority()
        {
            if (recordDeferredSessionAuthority)
            {
                DeferredSessionAuthority = true;
            }
        }

        /// <summary>Caches enum-shape resolutions so one expression scans module metadata at most once per name.</summary>
        internal Dictionary<string, (EnumShape? Shape, FoldOutcome? Error)> EnumShapes { get; } =
            new(StringComparer.Ordinal);

        /// <summary>Gets or sets the current delegate-invocation nesting depth, bounding recursion.</summary>
        internal int DelegateInvocationDepth { get; set; }

        internal void PushBinding(string name, Operand value) => bindings.Add((name, value));

        internal void PopBindings(int count) => bindings.RemoveRange(bindings.Count - count, count);

        internal bool IsBound(string name)
        {
            foreach (var (bound, _) in bindings)
            {
                if (string.Equals(bound, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool TryResolveBinding(string name, out Operand value)
        {
            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                if (string.Equals(bindings[index].Name, name, StringComparison.Ordinal))
                {
                    value = bindings[index].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    /// <summary>States whether a dotted chain is anchored at a known BCL type name, optionally System-qualified.</summary>
    /// <remarks>
    /// Such a chain can never be a namespace-rooted static field of a dump module without shadowing a BCL type
    /// name, which the two-segment type-static shortcut already precludes; folding it keeps 'Guid.Empty.Version'
    /// and 'DateTime.MaxValue.Year' in the constant domain.
    /// </remarks>
    private static bool IsKnownTypeHead(ImmutableArray<string> parts) =>
        TryMapReceiverName(parts[0], out _) ||
        (parts.Length > 1 && parts[0] == "System" && TryMapReceiverName(parts[1], out _)) ||
        (parts.Length > 2 && parts[0] == "System" && parts[1] == "Text" &&
            (parts[2] == "Encoding" ||
                (parts.Length > 3 && parts[2] == "RegularExpressions" &&
                    parts[3] is "Regex" or "RegexOptions")));

    /// <summary>Returns the leftmost identifier of an access chain, stepping through every access shape.</summary>
    private static string? LeftmostIdentifier(ExpressionSyntax syntax)
    {
        var current = syntax;
        while (true)
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } member:
                    current = member.Expression;
                    break;
                case ConditionalAccessExpressionSyntax conditional:
                    current = conditional.Expression;
                    break;
                case InvocationExpressionSyntax invocation:
                    current = invocation.Expression;
                    break;
                case ElementAccessExpressionSyntax element:
                    current = element.Expression;
                    break;
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;
                default:
                    return null;
            }
        }
    }

    private static bool IsBareDumpExpression(ExpressionSyntax syntax, string rootIdentifier)
    {
        if (syntax is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce)
        {
            return IsBareDumpExpression(coalesce.Left, rootIdentifier);
        }

        // Only the root identifier itself and plain or conditional member chains are bare: those are exactly the
        // shapes the frozen root-relative path answers. Indexing a chain's value and calling a method on a chain's
        // value are composition — 'root.X.Name[6..^5]' and 'root.X.Values.Max()' fold here over the chain's exact
        // value, while a method invoked on the root itself still reaches the counterfactual path, because folding
        // has no value for the bare root and yields not-constant.
        if (syntax is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText == rootIdentifier;
        }

        // A chain ending in '.Length' composes: the resolver still gives the frozen pipeline first chance at a
        // genuine field of that name, and falls back to the receiver value's length for arrays and strings.
        if (syntax is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" })
        {
            return false;
        }

        return IsRootChainOperand(syntax, rootIdentifier);
    }

    /// <summary>Matches a plain or conditional member chain anchored at the root identifier, used as an operand.</summary>
    private static bool IsRootChainOperand(ExpressionSyntax syntax, string rootIdentifier) =>
        syntax is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax &&
        LeftmostIdentifier(syntax) == rootIdentifier;

    /// <summary>Maps one declared-variable resolution to an operand without counting it as dump evidence.</summary>
    private static FoldOutcome ResolveLocalOperand(ConstantOperandResolution resolution) =>
        resolution.Kind switch
        {
            ConstantOperandResolutionKind.Int32 => FoldOutcome.Folded(Operand.FromInt32(resolution.Int32Value!.Value)),
            ConstantOperandResolutionKind.String => FoldOutcome.Folded(Operand.FromString(resolution.StringValue!)),
            ConstantOperandResolutionKind.Null => FoldOutcome.Folded(Operand.Null()),
            ConstantOperandResolutionKind.Int64 =>
                FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, resolution.Int64Value!.Value)),
            ConstantOperandResolutionKind.Double =>
                FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, resolution.DoubleValue!.Value)),
            ConstantOperandResolutionKind.Single =>
                FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, resolution.SingleValue!.Value)),
            ConstantOperandResolutionKind.Boolean =>
                FoldOutcome.Folded(Operand.FromBoolean(resolution.BooleanValue!.Value)),
            ConstantOperandResolutionKind.Char => FoldOutcome.Folded(Operand.FromChar(resolution.CharValue!.Value)),
            ConstantOperandResolutionKind.Sequence => MaterializeSequenceResolution(resolution),
            ConstantOperandResolutionKind.Numeric or ConstantOperandResolutionKind.Temporal or
                ConstantOperandResolutionKind.BclValue => MaterializeBoxedResolution(resolution),
            ConstantOperandResolutionKind.Stop =>
                FoldOutcome.Error(resolution.DiagnosticCode!, resolution.DiagnosticMessage!),
            _ => FoldOutcome.NotArithmetic(),
        };

    private static FoldOutcome ResolveDumpOperand(
        FoldContext context,
        ConstantOperandResolution resolution)
    {
        switch (resolution.Kind)
        {
            case ConstantOperandResolutionKind.Int32:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromInt32(resolution.Int32Value!.Value));
            case ConstantOperandResolutionKind.String:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromString(resolution.StringValue!));
            case ConstantOperandResolutionKind.Null:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.Null());
            case ConstantOperandResolutionKind.Int64:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Int64, resolution.Int64Value!.Value));
            case ConstantOperandResolutionKind.Double:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Double, resolution.DoubleValue!.Value));
            case ConstantOperandResolutionKind.Single:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromNumeric(NumericKind.Single, resolution.SingleValue!.Value));
            case ConstantOperandResolutionKind.Boolean:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromBoolean(resolution.BooleanValue!.Value));
            case ConstantOperandResolutionKind.Char:
                context.DumpValuesConsumed++;
                return FoldOutcome.Folded(Operand.FromChar(resolution.CharValue!.Value));
            case ConstantOperandResolutionKind.Sequence:
                context.DumpValuesConsumed++;
                return MaterializeSequenceResolution(resolution);
            case ConstantOperandResolutionKind.Numeric:
            case ConstantOperandResolutionKind.Temporal:
            case ConstantOperandResolutionKind.BclValue:
                context.DumpValuesConsumed++;
                return MaterializeBoxedResolution(resolution);
            case ConstantOperandResolutionKind.Stop:
                return FoldOutcome.Error(resolution.DiagnosticCode!, resolution.DiagnosticMessage!);
            default:
                return FoldOutcome.NotArithmetic();
        }
    }

    private static FoldOutcome MaterializeSequenceResolution(ConstantOperandResolution resolution)
    {
        var (elementKind, elementNumeric) = resolution.ElementTypeName switch
        {
            "Int32" => (OperandKind.Int32, NumericKind.Int32),
            "Int64" => (OperandKind.Numeric, NumericKind.Int64),
            "Double" => (OperandKind.Numeric, NumericKind.Double),
            "Single" => (OperandKind.Numeric, NumericKind.Single),
            "Boolean" => (OperandKind.Boolean, default),
            "Char" => (OperandKind.Char, default),
            "String" or null => (OperandKind.String, default),
            // Every remaining numeric domain — Byte, Decimal, the wide integers — and the temporal and BCL value
            // domains carry their kind name, so a byte[] variable materializes back as exactly a byte[].
            { } numericName when Enum.TryParse<NumericKind>(numericName, out var numeric) =>
                (OperandKind.Numeric, numeric),
            { } temporalName when Enum.TryParse<TemporalKind>(temporalName, out _) =>
                (OperandKind.Temporal, default),
            { } valueName when Enum.TryParse<BclValueKind>(valueName, out _) =>
                (OperandKind.BclValue, default),
            _ => (OperandKind.String, default(NumericKind)),
        };
        var items = ImmutableArray.CreateBuilder<Operand>(resolution.Elements.Length);
        foreach (var element in resolution.Elements)
        {
            items.Add(element.Kind switch
            {
                ConstantOperandResolutionKind.Int32 => Operand.FromInt32(element.Int32Value!.Value),
                ConstantOperandResolutionKind.Int64 => Operand.FromNumeric(
                    NumericKind.Int64,
                    element.Int64Value!.Value),
                ConstantOperandResolutionKind.Double => Operand.FromNumeric(
                    NumericKind.Double,
                    element.DoubleValue!.Value),
                ConstantOperandResolutionKind.Single => Operand.FromNumeric(
                    NumericKind.Single,
                    element.SingleValue!.Value),
                ConstantOperandResolutionKind.Boolean => Operand.FromBoolean(element.BooleanValue!.Value),
                ConstantOperandResolutionKind.Char => Operand.FromChar(element.CharValue!.Value),
                ConstantOperandResolutionKind.String => Operand.FromString(element.StringValue!),
                ConstantOperandResolutionKind.Numeric or ConstantOperandResolutionKind.Temporal or
                    ConstantOperandResolutionKind.BclValue when
                    MaterializeBoxedResolution(element) is
                    {
                        Disposition: FoldDisposition.Folded,
                    } boxed => boxed.Operand,
                _ => Operand.Null(),
            });
        }

        return CreateSequence(new SequencePayload(
            items.ToImmutable(),
            elementKind,
            elementNumeric,
            resolution.ElementTypeName ?? "String"));
    }

    /// <summary>
    /// Materializes one boxed resolution — a numeric outside the dedicated kinds, a temporal value, or a BCL
    /// value — back into its exact operand. The box is the value the evaluator itself stored, so an unknown
    /// domain name or a mismatched box means the resolution was not produced by this evaluator and stays a
    /// typed stop rather than a fabricated value.
    /// </summary>
    private static FoldOutcome MaterializeBoxedResolution(ConstantOperandResolution resolution)
    {
        if (resolution.BoxedValue is { } box && resolution.ValueTypeName is { } domain)
        {
            switch (resolution.Kind)
            {
                case ConstantOperandResolutionKind.Numeric
                    when Enum.TryParse<NumericKind>(domain, out var numericKind):
                    return FoldOutcome.Folded(Operand.FromNumeric(numericKind, box));
                case ConstantOperandResolutionKind.Temporal
                    when Enum.TryParse<TemporalKind>(domain, out var temporalKind):
                    return FoldOutcome.Folded(Operand.FromTemporal(temporalKind, box));
                case ConstantOperandResolutionKind.BclValue
                    when domain == "Delegate" && box is DelegatePayload storedDelegate:
                    return FoldOutcome.Folded(Operand.FromDelegate(storedDelegate));
                case ConstantOperandResolutionKind.BclValue
                    when Enum.TryParse<BclValueKind>(domain, out var valueKind):
                    return FoldOutcome.Folded(Operand.FromBclValue(valueKind, box));
            }
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "The stored value's domain is not one this evaluator produces; reassign the variable.");
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
        Sequence,
        Temporal,
        BclValue,
        Type,
        Tuple,
        Anonymous,
        Grouping,
        Delegate,
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
        object? Box,
        TemporalKind TemporalKind = default,
        BclValueKind BclValueKind = default)
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
            FromEnum(value, NumericKind.Int32, typeFullName, memberName);

        // Enum values carry their raw bits and exact underlying kind, so byte-, long-, and ulong-underlying
        // enums box and unbox with C# semantics; the Int32 mirror keeps the narrow consumers working.
        internal static Operand FromEnum(long bits, NumericKind underlying, string typeFullName, string memberName) =>
            new(OperandKind.Enum, unchecked((int)bits), false, '\0', null, typeFullName, memberName, underlying, bits);

        internal static Operand FromType(TypeRef type) =>
            new(OperandKind.Type, 0, false, '\0', null, null, null, default, type);

        internal static Operand FromTuple(TuplePayload payload) =>
            new(OperandKind.Tuple, 0, false, '\0', null, null, null, default, payload);

        internal static Operand FromAnonymous(AnonymousPayload payload) =>
            new(OperandKind.Anonymous, 0, false, '\0', null, null, null, default, payload);

        internal static Operand FromDelegate(DelegatePayload payload) =>
            new(OperandKind.Delegate, 0, false, '\0', null, null, null, default, payload);

        internal static Operand FromGrouping(GroupingPayload payload) =>
            new(OperandKind.Grouping, 0, false, '\0', null, null, null, default, payload);

        internal long EnumBits => Box is long bits ? bits : Int32;

        // Plain Int32 stays a first-class kind so the value composes with the string/char surface unchanged;
        // every other numeric domain rides in the boxed representation with its exact kind.
        internal static Operand FromNumeric(NumericKind kind, object value) => kind == NumericKind.Int32
            ? FromInt32((int)value)
            : new(OperandKind.Numeric, 0, false, '\0', null, null, null, kind, value);

        internal static Operand Null() =>
            new(OperandKind.Null, 0, false, '\0', null, null, null, default, null);

        internal static Operand FromSequence(SequencePayload payload) =>
            new(OperandKind.Sequence, 0, false, '\0', null, null, null, default, payload);

        // The boxed value is the exact BCL struct; every temporal computation unboxes, computes with the real
        // BCL member, and reboxes, so the semantics are the BCL's own rather than a re-implementation.
        internal static Operand FromTemporal(TemporalKind kind, object value) =>
            new(OperandKind.Temporal, 0, false, '\0', null, null, null, default, value, kind);

        internal static Operand FromBclValue(BclValueKind kind, object value) =>
            new(OperandKind.BclValue, 0, false, '\0', null, null, null, default, value, default, kind);

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
            case OperandKind.Sequence:
                var payload = PayloadOf(operand);
                kind = ConstantValueKind.Sequence;
                int32 = payload.Items.Length;
                valueTypeName = payload.DisplayName + payload.TypeSuffix;
                valueText = RenderSequence(payload);
                underlying = valueTypeName;
                break;
            case OperandKind.Tuple:
                kind = ConstantValueKind.Tuple;
                valueTypeName = TupleTypeName(operand);
                valueText = RenderTuple(operand);
                underlying = valueTypeName;
                break;
            case OperandKind.Anonymous:
                kind = ConstantValueKind.Anonymous;
                valueTypeName = AnonymousTypeName(operand);
                valueText = RenderAnonymous(operand);
                underlying = valueTypeName;
                break;
            case OperandKind.Grouping:
                kind = ConstantValueKind.Sequence;
                int32 = PayloadOfGrouping(operand).Items.Items.Length;
                valueTypeName = "IGrouping";
                valueText = RenderGrouping(operand);
                underlying = valueTypeName;
                break;
            case OperandKind.Temporal:
                kind = ConstantValueKind.Temporal;
                valueTypeName = operand.TemporalKind.ToString();
                valueText = RenderTemporal(operand);
                underlying = valueTypeName;
                break;
            case OperandKind.BclValue:
                kind = ConstantValueKind.BclValue;
                valueTypeName = operand.BclValueKind.ToString();
                valueText = RenderBclValue(operand);
                underlying = valueTypeName;
                break;
            case OperandKind.Type:
                kind = ConstantValueKind.Type;
                valueTypeName = "Type";
                valueText = $"typeof({((TypeRef)operand.Box!).CSharpName})";
                underlying = valueTypeName;
                break;
            case OperandKind.Delegate:
                var delegatePayload = DelegatePayloadOf(operand);
                kind = ConstantValueKind.BclValue;
                valueTypeName = delegatePayload.Type.CSharpName;
                valueText = RenderDelegate(delegatePayload);
                underlying = valueTypeName;
                break;
            default:
                kind = ConstantValueKind.EnumMember;
                int32 = operand.EnumBits >= int.MinValue && operand.EnumBits <= int.MaxValue
                    ? unchecked((int)operand.EnumBits)
                    : null;
                enumType = operand.EnumTypeFullName;
                enumMember = operand.EnumMemberName;
                underlying = operand.NumericKind.ToString();
                valueText = FormatEnumBitsInvariant(operand.NumericKind, operand.EnumBits);
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
            valueText,
            context.DumpValuesConsumed,
            ChildrenOf(operand))
        {
            ExactProjection = ProjectOperandForStorage(operand),
        };
    }

    /// <summary>
    /// Projects one folded operand into a stored-variable resolution for the value domains the evaluation's
    /// public scalar fields cannot reconstruct: boxed numerics, temporal values, BCL values, and sequences of
    /// storable elements. Domains the legacy scalar projection already reconstructs return null so their stored
    /// form is unchanged, and domains with no operand carrier — tuples, anonymous values, groupings, type
    /// references — return null so the caller keeps refusing them with its typed message.
    /// </summary>
    private static ConstantOperandResolution? ProjectOperandForStorage(Operand operand)
    {
        switch (operand.Kind)
        {
            case OperandKind.Numeric when operand.NumericKind is not
                (NumericKind.Int64 or NumericKind.Double or NumericKind.Single):
                return ConstantOperandResolution.FromNumericValue(operand.NumericKind.ToString(), operand.Box!);
            case OperandKind.Temporal:
                return ConstantOperandResolution.FromTemporalValue(operand.TemporalKind.ToString(), operand.Box!);
            case OperandKind.BclValue:
                return ConstantOperandResolution.FromBclValue(operand.BclValueKind.ToString(), operand.Box!);
            case OperandKind.Delegate:
                // The delegate payload rides the same boxed carrier under its own domain sentinel.
                return ConstantOperandResolution.FromBclValue("Delegate", operand.Box!);
            case OperandKind.Sequence:
                var payload = PayloadOf(operand);
                var elements = ImmutableArray.CreateBuilder<ConstantOperandResolution>(payload.Items.Length);
                foreach (var item in payload.Items)
                {
                    if (ProjectElementForStorage(item) is not { } element)
                    {
                        return null;
                    }

                    elements.Add(element);
                }

                return ConstantOperandResolution.FromSequence(elements.ToImmutable(), payload.DisplayName);
            default:
                return null;
        }
    }

    /// <summary>Projects one sequence element, or null when the element domain has no operand carrier.</summary>
    private static ConstantOperandResolution? ProjectElementForStorage(Operand item) => item.Kind switch
    {
        OperandKind.Int32 => ConstantOperandResolution.FromInt32(item.Int32),
        OperandKind.String => ConstantOperandResolution.FromString(item.String!),
        OperandKind.Char => ConstantOperandResolution.FromChar(item.Char),
        OperandKind.Boolean => ConstantOperandResolution.FromBoolean(item.Boolean),
        OperandKind.Null => ConstantOperandResolution.ExactNull(),
        OperandKind.Numeric => item.NumericKind switch
        {
            NumericKind.Int64 => ConstantOperandResolution.FromInt64((long)item.Box!),
            NumericKind.Double => ConstantOperandResolution.FromDouble((double)item.Box!),
            NumericKind.Single => ConstantOperandResolution.FromSingle((float)item.Box!),
            _ => ConstantOperandResolution.FromNumericValue(item.NumericKind.ToString(), item.Box!),
        },
        OperandKind.Temporal => ConstantOperandResolution.FromTemporalValue(
            item.TemporalKind.ToString(), item.Box!),
        OperandKind.BclValue => ConstantOperandResolution.FromBclValue(item.BclValueKind.ToString(), item.Box!),
        OperandKind.Delegate => ConstantOperandResolution.FromBclValue("Delegate", item.Box!),
        _ => null,
    };

    private static FoldOutcome Fold(ExpressionSyntax syntax, FoldContext context)
    {
        // Every sub-expression, lambda body, delegate entry, and operand resolution re-enters here, so this one
        // check is the fold engine's principal cooperative-cancellation boundary.
        if (CancellationStop() is { } cancelled)
        {
            return cancelled;
        }

        // A member chain anchored at the root identifier is one operand: the frozen root-relative pipeline
        // evaluates the whole chain — including its ?. short-circuit semantics — and hands back the exact value.
        // A chain ending in '.Length' that has no value of its own falls back to the receiver chain's value, so
        // 'root.X.Tags.Length' answers over the array and 'root.Region.Length' over the string.
        if (context.Resolvers is { RootChain: { } rootResolver, RootIdentifier: { } rootIdentifier } &&
            !context.IsBound(rootIdentifier) &&
            IsRootChainOperand(syntax, rootIdentifier))
        {
            var resolved = ResolveDumpOperand(context, rootResolver(syntax.ToString()));
            if (resolved.Disposition == FoldDisposition.Folded ||
                syntax is not MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Length",
                    Expression: { } lengthReceiver,
                } ||
                !IsRootChainOperand(lengthReceiver, rootIdentifier))
            {
                return resolved;
            }

            var receiverValue = ResolveDumpOperand(context, rootResolver(lengthReceiver.ToString()));
            return receiverValue.Disposition == FoldDisposition.Folded
                ? receiverValue.Operand.Kind switch
                {
                    OperandKind.Sequence => FoldOutcome.Folded(Operand.FromInt32(
                        PayloadOf(receiverValue.Operand).Items.Length)),
                    OperandKind.String => FoldOutcome.Folded(Operand.FromInt32(
                        receiverValue.Operand.String!.Length)),
                    _ => resolved,
                }
                : resolved;
        }

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
            case ArrayCreationExpressionSyntax arrayCreation:
                return FoldArrayCreation(arrayCreation, context);
            case ImplicitArrayCreationExpressionSyntax implicitCreation:
                return FoldInitializer(implicitCreation.Initializer, context);
            case ElementAccessExpressionSyntax elementAccess:
                return FoldElementAccess(elementAccess, context);
            case MemberAccessExpressionSyntax memberAccess:
                return FoldMemberAccess(memberAccess, context);
            case InvocationExpressionSyntax invocation:
                return FoldInvocation(invocation, context);
            case InterpolatedStringExpressionSyntax interpolated:
                return FoldInterpolatedString(interpolated, context);
            case IsPatternExpressionSyntax isPattern:
                return FoldIsPattern(isPattern, context);
            case SwitchExpressionSyntax switchExpression:
                return FoldSwitchExpression(switchExpression, context);
            case DefaultExpressionSyntax defaultExpression:
                return FoldDefaultExpression(defaultExpression);
            case SizeOfExpressionSyntax sizeOf:
                return FoldSizeOf(sizeOf);
            case CheckedExpressionSyntax checkedExpression:
                return FoldCheckedExpression(checkedExpression, context);
            case ObjectCreationExpressionSyntax objectCreation:
                return FoldObjectCreation(objectCreation, context);
            case TypeOfExpressionSyntax typeOf:
                return FoldTypeOf(typeOf, context);
            case CollectionExpressionSyntax collection:
                return FoldCollectionExpression(collection, context);
            case TupleExpressionSyntax tuple:
                return FoldTuple(tuple, context);
            case AnonymousObjectCreationExpressionSyntax anonymous:
                return FoldAnonymousObject(anonymous, context);
            case QueryExpressionSyntax query:
                return FoldQuery(query, context);
            case PostfixUnaryExpressionSyntax postfix
                when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                // The null-forgiving operator is a compile-time annotation with no run-time effect.
                return Fold(postfix.Operand, context);
            case IdentifierNameSyntax identifier
                when context.TryResolveBinding(identifier.Identifier.ValueText, out var bound):
                // A lambda parameter in scope is a value; it shadows a declared variable of the same name exactly
                // as a nested scope shadows an outer one.
                return FoldOutcome.Folded(bound);
            case IdentifierNameSyntax localName
                when context.Resolvers?.LocalName is { } localResolver:
                // A declared session variable resolves as its stored constant value; an unknown identifier stays
                // not-constant so every bare-identifier path without a variable is unchanged.
                return ResolveLocalOperand(localResolver(localName.Identifier.ValueText));
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

        // The bitwise complement of an enum keeps the enum type, masked to its underlying width.
        if (unary.IsKind(SyntaxKind.BitwiseNotExpression) && operand.Operand.Kind == OperandKind.Enum)
        {
            return ComputeEnumComplement(operand.Operand, context);
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
            // Short-circuit semantics: when the left operand already decides a logical operator — false for &&,
            // true for || — the right operand is never evaluated at run time, so a right side that stops or
            // stays outside the constant domain does not poison the decided answer. A right side that folds
            // keeps full operand-type checking below, so 'false && 5' still reports its type error.
            if (kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression &&
                left.Operand.Kind == OperandKind.Boolean &&
                left.Operand.Boolean == (kind == SyntaxKind.LogicalOrExpression))
            {
                return FoldOutcome.Folded(Operand.FromBoolean(left.Operand.Boolean));
            }

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

        // Delegates carry the multicast algebra: + combines, - removes, and equality compares invocation lists.
        if (leftOperand.Kind == OperandKind.Delegate || rightOperand.Kind == OperandKind.Delegate)
        {
            return ComputeDelegateBinary(kind, leftOperand, rightOperand);
        }

        // Date and time operands carry their own operator algebra — DateTime − DateTime is a TimeSpan, TimeSpan
        // scales by a number — so they dispatch before the numeric tower sees them.
        if (leftOperand.Kind == OperandKind.Temporal || rightOperand.Kind == OperandKind.Temporal)
        {
            return ComputeTemporalBinary(kind, leftOperand, rightOperand);
        }

        if (leftOperand.Kind == OperandKind.BclValue || rightOperand.Kind == OperandKind.BclValue)
        {
            return ComputeBclValueBinary(kind, leftOperand, rightOperand);
        }

        if (leftOperand.Kind == OperandKind.Anonymous || rightOperand.Kind == OperandKind.Anonymous)
        {
            return ComputeAnonymousBinary(kind);
        }

        if (leftOperand.Kind == OperandKind.Tuple || rightOperand.Kind == OperandKind.Tuple)
        {
            return ComputeTupleBinary(kind, leftOperand, rightOperand);
        }

        if (leftOperand.Kind == OperandKind.Type || rightOperand.Kind == OperandKind.Type)
        {
            if (kind is (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression) &&
                leftOperand.Kind == OperandKind.Type && rightOperand.Kind == OperandKind.Type)
            {
                var sameType = string.Equals(
                    ((TypeRef)leftOperand.Box!).FullName,
                    ((TypeRef)rightOperand.Box!).FullName,
                    StringComparison.Ordinal);
                return FoldOutcome.Folded(Operand.FromBoolean(
                    kind == SyntaxKind.EqualsExpression ? sameType : !sameType));
            }

            return FoldOutcome.Error(OperandTypeCode, "Type references define equality only.");
        }

        // Enum operators that keep the enum type dispatch first; everything else falls through to the numeric
        // tower, which already gives E − E its underlying result and comparisons their meaning.
        if (leftOperand.Kind == OperandKind.Enum || rightOperand.Kind == OperandKind.Enum)
        {
            if (TryComputeEnumBinary(kind, leftOperand, rightOperand, context) is { } enumOutcome)
            {
                return enumOutcome;
            }
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

        // The delegate algebra lifts by Delegate.Combine and Remove: null + d and d + null are d, and
        // d - null is d, while null - d stays null through the default lifted arm below.
        if (kind == SyntaxKind.AddExpression &&
            (left.Kind == OperandKind.Delegate || right.Kind == OperandKind.Delegate))
        {
            return FoldOutcome.Folded(left.Kind == OperandKind.Delegate ? left : right);
        }

        if (kind == SyntaxKind.SubtractExpression && left.Kind == OperandKind.Delegate)
        {
            return FoldOutcome.Folded(left);
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

        // Only the selected branch evaluates, exactly as the conditional operator executes at run time. The
        // unselected branch may stop, stay outside the constant domain, or recurse without bound — a recursive
        // delegate reaches its base case precisely because the recursive arm is never entered there.
        return Fold(condition.Operand.Boolean ? conditional.WhenTrue : conditional.WhenFalse, context);
    }

    private static FoldOutcome FoldElementAccess(ElementAccessExpressionSyntax elementAccess, FoldContext context)
    {
        var receiver = Fold(elementAccess.Expression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return receiver;
        }

        if (receiver.Operand.Kind == OperandKind.Sequence)
        {
            return FoldSequenceElementAccess(receiver.Operand, elementAccess, context);
        }

        if (receiver.Operand.Kind == OperandKind.BclValue)
        {
            return DispatchBclValueElementAccess(receiver.Operand, elementAccess, context);
        }

        if (receiver.Operand.Kind != OperandKind.String ||
            elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Constant element access requires one string or array receiver and one index or range.");
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

        // A chain anchored at a lambda parameter is value access, never a type static, a metadata literal, or a
        // stored field, so the binding wins before any name-shaped interpretation.
        var leftmostBound = LeftmostIdentifier(memberAccess) is { } leftmost && context.IsBound(leftmost);

        // A known type receiver wins over the metadata-literal path so 'Math.PI' or 'double.NaN' never triggers a
        // module scan; every other pure qualified-name chain is a metadata literal candidate, and a stored static
        // field stays not-constant.
        if (!leftmostBound && TryReadTypeReceiver(memberAccess.Expression, out var typeReceiver))
        {
            return DispatchTypeStatic(typeReceiver, member.Identifier.ValueText);
        }

        // A whole qualified chain resolves first, so metadata literals and stored fields keep their answers. When
        // the whole chain has no value of its own, the receiver's value gets a chance: that is how
        // 'Some.Type.Field.Length' answers over a stored array or string and 'Guid.Empty.Version' over a folded
        // value. A chain neither path can value stays not-constant, so the frozen pipelines keep rejecting it
        // with their own vocabulary.
        FoldOutcome? qualifiedOutcome = null;
        if (!leftmostBound && TryReadQualifiedName(memberAccess, out var parts, out var chainAlias))
        {
            var qualified = FoldQualifiedName(parts, chainAlias, context);
            if (qualified.Disposition != FoldDisposition.NotArithmetic)
            {
                return qualified;
            }

            qualifiedOutcome = qualified;
        }

        var receiver = Fold(memberAccess.Expression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return qualifiedOutcome ?? receiver;
        }

        var dispatched = DispatchOperandProperty(receiver.Operand, member.Identifier.ValueText);
        return dispatched.Disposition == FoldDisposition.NotArithmetic
            ? qualifiedOutcome ?? FoldOutcome.Error(
                MemberUnsupportedCode,
                $"'{member.Identifier.ValueText}' is not an admitted deterministic constant member.")
            : dispatched;
    }

    /// <summary>
    /// One instance property read over a folded operand, shared by source spelling and reflection. A member no
    /// operand domain answers is not-arithmetic, so the caller keeps its own fallback vocabulary.
    /// </summary>
    private static FoldOutcome DispatchOperandProperty(Operand receiver, string member) =>
        (receiver.Kind, member) switch
        {
            (OperandKind.String, "Length") => FoldOutcome.Folded(Operand.FromInt32(receiver.String!.Length)),
            (OperandKind.Sequence, "Length") => FoldOutcome.Folded(Operand.FromInt32(
                PayloadOf(receiver).Items.Length)),
            (OperandKind.Sequence, "LongLength") => FoldOutcome.Folded(Operand.FromNumeric(
                NumericKind.Int64,
                (long)PayloadOf(receiver).Items.Length)),
            (OperandKind.Sequence, "Rank") => FoldOutcome.Folded(Operand.FromInt32(PayloadOf(receiver).Rank)),
            (OperandKind.Temporal, var temporalProperty) =>
                DispatchTemporalProperty(receiver, temporalProperty),
            (OperandKind.BclValue, var valueProperty) =>
                DispatchBclValueProperty(receiver, valueProperty),
            (OperandKind.Type, var typeProperty) =>
                DispatchTypeRefProperty(receiver, typeProperty),
            (OperandKind.Tuple, var tupleMember) =>
                DispatchTupleProperty(receiver, tupleMember),
            (OperandKind.Anonymous, var anonymousMember) =>
                DispatchAnonymousProperty(receiver, anonymousMember),
            (OperandKind.Grouping, "Key") =>
                FoldOutcome.Folded(PayloadOfGrouping(receiver).Key),
            (OperandKind.Delegate, var delegateMember) =>
                DispatchDelegateProperty(receiver, delegateMember),
            _ => FoldOutcome.NotArithmetic(),
        };

    private static FoldOutcome FoldQualifiedName(
        ImmutableArray<string> parts,
        string? alias,
        FoldContext context)
    {
        // A metadata literal wins when one declares the name; a stored static field then resolves through the
        // caller's bridge to the frozen pipeline, so composed expressions can consume its exact value. An
        // alias-qualified name resolves only through the references carrying that alias; an unqualified name
        // expands through the active using directives first.
        var applicableReferences = ApplicableReferences(context.References, alias);
        if (context.Session is not null || !applicableReferences.IsEmpty)
        {
            var candidates = alias is null
                ? context.Usings.ExpandMemberCandidates(parts)
                : [string.Join('.', parts)];
            var resolved = ResolveLiteralFieldCandidates(
                alias is null ? context.Session : null,
                applicableReferences,
                string.Join('.', parts),
                candidates);
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
            }
        }

        // The dump's static-field bridge speaks the global scope only; an alias names a reference, not a module.
        if (alias is null && context.Resolvers?.StaticName is { } staticResolver)
        {
            return ResolveDumpOperand(context, staticResolver(string.Join('.', parts)));
        }

        return FoldOutcome.NotArithmetic();
    }

    /// <summary>Selects the references one name may resolve through, honoring extern-alias scoping.</summary>
    private static ImmutableArray<ConstantReferenceAssembly> ApplicableReferences(
        ImmutableArray<ConstantReferenceAssembly> references,
        string? alias) =>
        references.IsDefaultOrEmpty
            ? []
            :
            [
                .. references.Where(reference => alias is null
                    ? reference.Alias is null
                    : string.Equals(reference.Alias, alias, StringComparison.Ordinal)),
            ];

    private static FoldOutcome FoldInvocation(InvocationExpressionSyntax invocation, FoldContext context)
    {
        // nameof is a contextual operator, not a call: its argument is a name that is never evaluated.
        if (invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
        {
            return FoldNameOf(invocation);
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: { } receiverExpression,
                Name: SimpleNameSyntax method,
            })
        {
            // 'f(3)', '((Action)(…))()', 'delegates[0](x)': an invocation whose expression folds to a delegate
            // value invokes it; every other non-member shape keeps its not-constant path.
            return FoldDelegateValueInvocation(invocation, context);
        }

        // A generic method name is admitted only on the System.Enum, System.Array, and System.Activator
        // surfaces — Enum.GetNames<T>(), Array.Empty<T>(), Activator.CreateInstance<T>() — so every other
        // generic invocation keeps its existing not-constant path.
        var typeArguments = (method as GenericNameSyntax)?.TypeArgumentList.Arguments ?? default;
        if (typeArguments.Count > 0 &&
            !(TryReadTypeReceiver(receiverExpression, out var genericReceiver) &&
                genericReceiver.Category is TypeReceiverCategory.SystemEnum or TypeReceiverCategory.SystemArray
                    or TypeReceiverCategory.Activator))
        {
            return FoldOutcome.NotArithmetic();
        }

        // A lambda argument is not a value to fold: it routes to the expression-lambda sequence surface, which
        // folds the body once per element under the parameter binding.
        if (invocation.ArgumentList.Arguments.Any(
                static argument => argument.Expression is LambdaExpressionSyntax))
        {
            return FoldLambdaInvocation(
                receiverExpression,
                method.Identifier.ValueText,
                invocation.ArgumentList,
                context);
        }

        var receiverIsBound = receiverExpression is IdentifierNameSyntax { Identifier.ValueText: { } receiverName }
            && context.IsBound(receiverName);
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
        if (!receiverIsBound && TryReadTypeReceiver(receiverExpression, out var typeReceiver))
        {
            return DispatchTypeReceiverInvocation(typeReceiver, name, typeArguments, arguments, context);
        }

        var receiver = Fold(receiverExpression, context);
        if (receiver.Disposition != FoldDisposition.Folded)
        {
            return receiver;
        }

        // A reflection info value invokes with the fold context in hand, so Invoke over the Enum and Array
        // static surfaces can still resolve enum shapes from dump metadata.
        if (receiver.Operand.Kind == OperandKind.BclValue && IsReflectionKind(receiver.Operand.BclValueKind))
        {
            return DispatchReflectionMethod(receiver.Operand, name, arguments, context);
        }

        // A delegate's Invoke and DynamicInvoke fold lambda entries in the caller's context.
        if (receiver.Operand.Kind == OperandKind.Delegate)
        {
            return DispatchDelegateMethod(receiver.Operand, name, arguments, context);
        }

        return DispatchOperandInvocation(receiver.Operand, name, arguments);
    }

    /// <summary>One static invocation over a recognized type receiver, shared by source spelling and reflection.</summary>
    /// <remarks>
    /// The context is null only when reflection invokes without a fold in flight; the Enum and Array surfaces
    /// are the two that read it, and they stop rather than resolve shapes without one.
    /// </remarks>
    private static FoldOutcome DispatchTypeReceiverInvocation(
        TypeReceiver typeReceiver,
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext? context)
    {
        // A property accessor reached as a MethodInfo — through Invoke or a delegate — answers as the property
        // it accesses, so 'get_UtcNow' hits the same fold, and the same stops, as 'DateTime.UtcNow'.
        if (arguments.Count == 0 && name.StartsWith("get_", StringComparison.Ordinal))
        {
            return DispatchTypeStatic(typeReceiver, name[4..]);
        }

        switch (typeReceiver.Category)
        {
            case TypeReceiverCategory.String:
                return DispatchStaticString(name, arguments);
            case TypeReceiverCategory.Char:
                return DispatchStaticChar(name, arguments);
            case TypeReceiverCategory.Math:
                return DispatchMath(name, arguments);
            case TypeReceiverCategory.Enumerable:
                return DispatchEnumerable(name, arguments);
            case TypeReceiverCategory.KnownEnum:
                return MemberUnsupported(name);
            case TypeReceiverCategory.Temporal:
                return DispatchTemporalStaticMethod(typeReceiver.Temporal, name, arguments);
            case TypeReceiverCategory.BclValue:
                return DispatchBclValueStaticMethod(typeReceiver.Value, name, arguments);
            case TypeReceiverCategory.SystemEnum when context is not null:
                return DispatchSystemEnum(name, typeArguments, arguments, context);
            case TypeReceiverCategory.SystemArray when context is not null:
                return DispatchSystemArray(name, typeArguments, arguments, context);
            case TypeReceiverCategory.SystemEnum:
            case TypeReceiverCategory.SystemArray:
                return MemberUnsupported($"{name} via reflection without an evaluation context");
            case TypeReceiverCategory.Activator when context is not null:
                return DispatchActivator(name, typeArguments, arguments, context);
            case TypeReceiverCategory.Activator:
                return MemberUnsupported($"Activator.{name} via reflection");
            case TypeReceiverCategory.SystemDelegate:
                return DispatchDelegateStatic(name, arguments);
            case TypeReceiverCategory.CharUnicodeInfo:
                return DispatchCharUnicodeInfo(name, arguments);
            case TypeReceiverCategory.SystemConvert:
                return DispatchConvert(name, arguments);
            default:
                return DispatchNumericTypeMethod(typeReceiver.Numeric, name, arguments);
        }
    }

    /// <summary>One instance invocation over a folded operand, shared by source spelling and reflection.</summary>
    private static FoldOutcome DispatchOperandInvocation(Operand receiver, string name, List<Operand> arguments)
    {
        // Every numeric ToString evaluates under the invariant culture, with or without a format string, so the
        // answer never depends on the analysis machine's regional settings.
        if (name == "ToString" &&
            receiver.Kind is OperandKind.Int32 or OperandKind.Numeric)
        {
            switch (arguments)
            {
                case []:
                    return NumericToString(receiver, format: null);
                case [{ Kind: OperandKind.String } format]:
                    return NumericToString(receiver, format.String);
            }
        }

        // GetType is the reflective entry every value shares: the exact runtime identity of the folded operand.
        if (name == "GetType" && arguments.Count == 0 && receiver.Kind != OperandKind.Null)
        {
            return TryDescribeRuntimeType(receiver) is { } runtimeType
                ? FoldOutcome.Folded(Operand.FromType(runtimeType))
                : MemberUnsupported("GetType over this operand's runtime identity");
        }

        // A property accessor reached as a MethodInfo answers as the property it accesses, so a bound
        // 'get_Length' delegate folds exactly as reading Length on the target does.
        if (arguments.Count == 0 && name.StartsWith("get_", StringComparison.Ordinal))
        {
            var accessed = DispatchOperandProperty(receiver, name[4..]);
            return accessed.Disposition == FoldDisposition.NotArithmetic
                ? MemberUnsupported(name)
                : accessed;
        }

        return receiver.Kind switch
        {
            OperandKind.String => DispatchInstanceString(receiver.String!, name, arguments),
            OperandKind.Sequence => DispatchSequence(receiver, name, arguments),
            OperandKind.Temporal => DispatchTemporalMethod(receiver, name, arguments),
            OperandKind.BclValue => DispatchBclValueMethod(receiver, name, arguments),
            OperandKind.Char => DispatchCharInstanceMethod(receiver.Char, name, arguments),
            OperandKind.Boolean => name == "ToString" && arguments.Count == 0
                ? FoldOutcome.Folded(Operand.FromString(receiver.Boolean ? "True" : "False"))
                : MemberUnsupported(name),
            OperandKind.Enum => DispatchEnumMethod(receiver, name, arguments),
            OperandKind.Type => DispatchTypeRefMethod(receiver, name, arguments),
            OperandKind.Tuple => DispatchTupleMethod(receiver, name, arguments),
            OperandKind.Anonymous => DispatchAnonymousMethod(receiver, name, arguments),
            OperandKind.Grouping => DispatchSequence(
                Operand.FromSequence(PayloadOfGrouping(receiver).Items), name, arguments),
            _ => MemberUnsupported(name),
        };
    }

    private static FoldOutcome DispatchStaticString(string name, List<Operand> arguments)
    {
        switch (name)
        {
            case "Concat" or "Join" when
                (name == "Concat" && arguments is [{ Kind: OperandKind.Sequence }]) ||
                (name == "Join" && arguments is [{ Kind: OperandKind.String }, { Kind: OperandKind.Sequence }]):
                var sequence = arguments[^1];
                var joined = new List<string>(PayloadOf(sequence).Items.Length);
                foreach (var item in PayloadOf(sequence).Items)
                {
                    if (!TryConcatOperand(item, out var text))
                    {
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            $"string.{name} admits only string, char, numeric, and Boolean elements.");
                    }

                    joined.Add(text);
                }

                return FoldOutcome.Folded(Operand.FromString(
                    string.Join(name == "Join" ? arguments[0].String : string.Empty, joined)));
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

    private static FoldOutcome DispatchInstanceString(string receiver, string name, List<Operand> arguments)
    {
        try
        {
            switch (name)
            {
                case "Length":
                    return MemberUnsupported(name);
                case "EnumerateRunes" when arguments.Count == 0:
                    return CreateRuneSequence(receiver);
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
                case "ToCharArray" when arguments.Count == 0:
                    return CreateCharSequence(receiver.ToCharArray());
                case "Split" when arguments.Count >= 1 &&
                    arguments.All(static argument => argument.Kind == OperandKind.Char):
                    return CreateStringSequence(receiver.Split(
                        [.. arguments.Select(static argument => argument.Char)]));
                case "Split" when arguments is [{ Kind: OperandKind.Char } separator, { } options] &&
                    TryGetSplitOptions(options, out var charSplitOptions):
                    return CreateStringSequence(receiver.Split(separator.Char, charSplitOptions));
                case "Split" when arguments is [{ Kind: OperandKind.String } separator]:
                    return CreateStringSequence(receiver.Split(separator.String));
                case "Split" when arguments is [{ Kind: OperandKind.String } separator, { } options] &&
                    TryGetSplitOptions(options, out var stringSplitOptions):
                    return CreateStringSequence(receiver.Split(separator.String, stringSplitOptions));
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

    private static bool TryReadQualifiedName(
        ExpressionSyntax syntax,
        out ImmutableArray<string> parts,
        out string? alias,
        bool allowSingleIdentifier = false)
    {
        alias = null;
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
                Alias.Identifier.ValueText: var aliasText,
                Name: IdentifierNameSyntax aliased,
            }:
                builder.Insert(0, aliased.Identifier.ValueText);
                // 'global' is the language's name for the default scope, not an extern alias.
                alias = aliasText == "global" ? null : aliasText;
                break;
            default:
                parts = default;
                return false;
        }

        parts = builder.ToImmutable();

        // A lone identifier is admitted only when a static import could promote it to a member; without one it is
        // not a qualified name and keeps its previous not-a-name path, so no behavior changes without a directive.
        return parts.Length >= 2 || (parts.Length == 1 && allowSingleIdentifier);
    }

    /// <summary>
    /// Resolves the first using-expanded candidate that names a literal, and reports a genuine cross-candidate
    /// collision as ambiguity rather than silently taking the first import.
    /// </summary>
    private static ConstantExpressionEvaluation ResolveLiteralFieldCandidates(
        ClrmdDumpSession? session,
        ImmutableArray<ConstantReferenceAssembly> references,
        string expression,
        ImmutableArray<string> candidateNames)
    {
        ConstantExpressionEvaluation? firstExact = null;
        ConstantExpressionEvaluation? firstInvalid = null;
        foreach (var candidateName in candidateNames)
        {
            var candidateParts = candidateName.Split('.').ToImmutableArray();
            if (candidateParts.Length < 3)
            {
                continue;
            }

            var resolved = ResolveLiteralField(session, references, expression, candidateParts);
            switch (resolved.Status)
            {
                case ConstantExpressionStatus.Exact:
                    // The written name is the first candidate, so a fully qualified name never collides. Two
                    // distinct imports that both resolve the same short name to different declarations are the
                    // ambiguity C# reports; the same declaration reached two ways is not.
                    if (firstExact is { } prior &&
                        !(prior.EnumTypeFullName == resolved.EnumTypeFullName &&
                          prior.EnumMemberName == resolved.EnumMemberName &&
                          prior.Int32Value == resolved.Int32Value &&
                          prior.StringValue == resolved.StringValue &&
                          prior.TypeToken == resolved.TypeToken))
                    {
                        return ConstantExpressionEvaluation.InvalidResult(
                            expression,
                            AmbiguousCode,
                            "The name is ambiguous between imported scopes; qualify it to select one.",
                            resolved.ModulesScanned,
                            resolved.ModuleCount);
                    }

                    firstExact ??= resolved;
                    break;
                case ConstantExpressionStatus.Invalid:
                    firstInvalid ??= resolved;
                    break;
            }
        }

        return firstExact ?? firstInvalid ?? ConstantExpressionEvaluation.NotConstantResult(expression);
    }

    private static ConstantExpressionEvaluation ResolveLiteralField(
        ClrmdDumpSession? session,
        ImmutableArray<ConstantReferenceAssembly> references,
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

        void ScanImage(ImmutableArray<byte> metadataBytes, string sourceName, string contentSha256)
        {
            scanned++;
            try
            {
                using var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes);
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
                        sourceName,
                        contentSha256,
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
                // A malformed metadata image cannot contribute a declaration; other sources still can.
            }
        }

        if (session is not null)
        {
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

                ScanImage(metadata.Evidence[0].Bytes, module.Name, metadata.Value.MetadataSha256);
            }
        }

        // Caller-referenced assemblies join the scan beside the session's modules; alias scoping already selected
        // which references apply, so a declaration here carries the reference's own name and content digest.
        foreach (var reference in references)
        {
            ScanImage(reference.MetadataBytesCore, reference.DisplayName, reference.MetadataSha256);
        }

        var totalSources = (session?.Modules.Length ?? 0) + references.Length;
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
                totalSources,
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
                totalSources);
        }

        if (invalidCode is not null)
        {
            return ConstantExpressionEvaluation.InvalidResult(
                expression,
                invalidCode,
                invalidMessage!,
                scanned,
                totalSources);
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
