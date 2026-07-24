using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Captures one canonical partial or unavailable four-byte observation for an ordinary instance
/// <see cref="TypeSig.Int32"/> field load.
/// </summary>
/// <remarks>
/// This immutable value is the evidence boundary between a memory model and an optional approximation-capable
/// value domain. It records the complete frozen field identity, imported-object identity, source identity, read
/// geometry, evidence classification, reason, and observed bytes. Its content identity deliberately excludes
/// diagnostic display names and process-local object identity. Construction copies the supplied byte span, and
/// equality compares canonical content rather than object identity.
///
/// This is a contract. It admits only the W4 ordinary instance <c>Int32</c> field profile and must be
/// versioned before its canonical encoding or admitted field shape changes.
/// </remarks>
public sealed class FieldLoadEvidence : IEquatable<FieldLoadEvidence>
{
    private static ReadOnlySpan<byte> CanonicalDomain => "PhoenixInspect.FieldLoadEvidence"u8;
    private readonly ImmutableArray<byte> observedBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    /// <summary>Gets the current canonical binary schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    /// <summary>Gets the maximum admitted canonical reason-code length.</summary>
    public const int MaximumReasonCodeLength = 128;

    /// <summary>
    /// Creates canonical evidence for one partial or unavailable ordinary instance <c>Int32</c> field observation.
    /// </summary>
    /// <param name="dependencyOrdinal">The nonnegative ordinal of the field dependency in the frozen plan.</param>
    /// <param name="field">
    /// The complete frozen field descriptor. It must identify a nonstatic, nonliteral, non-RVA <c>Int32</c> field.
    /// </param>
    /// <param name="evidenceStatus">
    /// <see cref="EvaluationEvidenceStatus.Partial"/> or <see cref="EvaluationEvidenceStatus.Unavailable"/>.
    /// </param>
    /// <param name="reasonCode">
    /// A canonical code of at most <see cref="MaximumReasonCodeLength"/> ASCII characters, composed of
    /// alphanumeric segments separated by one period, underscore, or hyphen.
    /// </param>
    /// <param name="sourceSha256">A complete SHA-256 digest identifying the memory-evidence source.</param>
    /// <param name="importedObjectSha256">
    /// A complete SHA-256 digest identifying the prepared imported object on which the field was observed.
    /// </param>
    /// <param name="address">The nonzero target address at which the four-byte observation began.</param>
    /// <param name="requestedLength">The requested byte count, which must be exactly four.</param>
    /// <param name="observedBytes">
    /// The observed byte prefix. Partial evidence requires one to three bytes; unavailable evidence requires none.
    /// The bytes are copied before this constructor returns.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dependencyOrdinal"/> is negative, <paramref name="evidenceStatus"/> is undefined,
    /// <paramref name="address"/> is zero or its four-byte range overflows, or <paramref name="requestedLength"/>
    /// is not four.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="field"/> is outside the ordinary instance <c>Int32</c> profile,
    /// <paramref name="evidenceStatus"/> is a defined status other than partial or unavailable, a digest is not a
    /// complete SHA-256 value, <paramref name="reasonCode"/> is not canonical, or the observed-byte count does not
    /// match <paramref name="evidenceStatus"/>.
    /// </exception>
    public FieldLoadEvidence(
        int dependencyOrdinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidenceStatus,
        string reasonCode,
        string sourceSha256,
        string importedObjectSha256,
        ulong address,
        int requestedLength,
        ReadOnlySpan<byte> observedBytes)
    {
        if (dependencyOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dependencyOrdinal));
        }

        ArgumentNullException.ThrowIfNull(field);
        if (field.FieldType != TypeSig.Int32 || field.IsStatic || field.IsLiteral || field.HasRva)
        {
            throw new ArgumentException(
                "Field-load evidence requires an ordinary instance Int32 field descriptor.",
                nameof(field));
        }

        if (!Enum.IsDefined(evidenceStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceStatus),
                "Field-load evidence requires a defined evidence status.");
        }

        if (evidenceStatus is not (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            throw new ArgumentException(
                "Field-load evidence must be Partial or Unavailable.",
                nameof(evidenceStatus));
        }

        ValidateReasonCode(reasonCode);
        var normalizedSourceSha256 = NormalizeSha256(sourceSha256, nameof(sourceSha256));
        var normalizedImportedObjectSha256 = NormalizeSha256(
            importedObjectSha256,
            nameof(importedObjectSha256));

        if (requestedLength != sizeof(int))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedLength),
                "An Int32 field observation must request exactly four bytes.");
        }

        if (address == 0 || address > ulong.MaxValue - (sizeof(int) - 1UL))
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                "A nonzero address with a nonoverflowing four-byte range is required.");
        }

        if (evidenceStatus == EvaluationEvidenceStatus.Partial && observedBytes.Length is not (>= 1 and <= 3))
        {
            throw new ArgumentException(
                "Partial Int32 field evidence must retain a one-to-three-byte prefix.",
                nameof(observedBytes));
        }

        if (evidenceStatus == EvaluationEvidenceStatus.Unavailable && observedBytes.Length != 0)
        {
            throw new ArgumentException(
                "Unavailable Int32 field evidence cannot retain observed bytes.",
                nameof(observedBytes));
        }

        DependencyOrdinal = dependencyOrdinal;
        Field = field;
        EvidenceStatus = evidenceStatus;
        ReasonCode = reasonCode;
        SourceSha256 = normalizedSourceSha256;
        ImportedObjectSha256 = normalizedImportedObjectSha256;
        Address = address;
        RequestedLength = requestedLength;
        this.observedBytes = ImmutableArray.CreateRange(observedBytes.ToArray());
        canonicalBytes = EncodeCanonical();
        Sha256 = Convert.ToHexString(SHA256.HashData(canonicalBytes.AsSpan())).ToLowerInvariant();
    }

    /// <summary>Gets the nonnegative frozen-plan field-dependency ordinal.</summary>
    public int DependencyOrdinal { get; }

    /// <summary>Gets the complete frozen ordinary instance <c>Int32</c> field descriptor.</summary>
    public ResolvedField Field { get; }

    /// <summary>Gets the partial or unavailable evidence classification.</summary>
    public EvaluationEvidenceStatus EvidenceStatus { get; }

    /// <summary>Gets the bounded canonical reason code.</summary>
    public string ReasonCode { get; }

    /// <summary>Gets the normalized lowercase complete SHA-256 digest of the memory-evidence source.</summary>
    public string SourceSha256 { get; }

    /// <summary>Gets the normalized lowercase complete SHA-256 digest of the prepared imported object.</summary>
    public string ImportedObjectSha256 { get; }

    /// <summary>Gets the nonzero starting target address of the four-byte observation.</summary>
    public ulong Address { get; }

    /// <summary>Gets the requested byte count, which is always four.</summary>
    public int RequestedLength { get; }

    /// <summary>Gets the observed byte count: one to three for partial evidence and zero for unavailable evidence.</summary>
    public int ObservedLength => observedBytes.Length;

    /// <summary>Gets the immutable defensive copy of the observed byte prefix.</summary>
    public ImmutableArray<byte> ObservedBytes => Copy(observedBytes);

    /// <summary>
    /// Gets a defensive copy of the versioned, domain-separated, big-endian canonical binary representation of
    /// every identity axis.
    /// </summary>
    public ImmutableArray<byte> CanonicalBytes => Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <inheritdoc />
    public bool Equals(FieldLoadEvidence? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null &&
            DependencyOrdinal == other.DependencyOrdinal &&
            Field == other.Field &&
            EvidenceStatus == other.EvidenceStatus &&
            string.Equals(ReasonCode, other.ReasonCode, StringComparison.Ordinal) &&
            string.Equals(SourceSha256, other.SourceSha256, StringComparison.Ordinal) &&
            string.Equals(ImportedObjectSha256, other.ImportedObjectSha256, StringComparison.Ordinal) &&
            Address == other.Address &&
            RequestedLength == other.RequestedLength &&
            observedBytes.AsSpan().SequenceEqual(other.observedBytes.AsSpan()) &&
            canonicalBytes.AsSpan().SequenceEqual(other.canonicalBytes.AsSpan());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FieldLoadEvidence);

    /// <inheritdoc />
    public override int GetHashCode() =>
        BinaryPrimitives.ReadInt32BigEndian(Convert.FromHexString(Sha256));

    /// <summary>Compares two evidence values by canonical content.</summary>
    /// <param name="left">The first evidence value, or <see langword="null"/>.</param>
    /// <param name="right">The second evidence value, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both operands carry identical canonical evidence content.</returns>
    public static bool operator ==(FieldLoadEvidence? left, FieldLoadEvidence? right) => Equals(left, right);

    /// <summary>Compares two evidence values for canonical-content inequality.</summary>
    /// <param name="left">The first evidence value, or <see langword="null"/>.</param>
    /// <param name="right">The second evidence value, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the operands do not carry identical canonical evidence content.</returns>
    public static bool operator !=(FieldLoadEvidence? left, FieldLoadEvidence? right) => !Equals(left, right);

    /// <summary>Returns the canonical lowercase evidence digest.</summary>
    /// <returns><see cref="Sha256"/>.</returns>
    public override string ToString() => Sha256;

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CanonicalWriter();
        writer.WriteBytes(CanonicalDomain);
        writer.WriteInt32(CanonicalSchemaVersion);
        writer.WriteInt32(DependencyOrdinal);

        writer.WriteUInt64(Field.Handle.Module.High);
        writer.WriteUInt64(Field.Handle.Module.Low);
        writer.WriteInt32(Field.Handle.MetadataToken);

        writer.WriteInt32(EncodeTypeKind(Field.DeclaringType.Kind));
        var declaringModule = Field.DeclaringType.Module!.Value;
        writer.WriteUInt64(declaringModule.High);
        writer.WriteUInt64(declaringModule.Low);
        writer.WriteInt32(Field.DeclaringType.MetadataToken);

        writer.WriteInt32(EncodeTypeKind(Field.FieldType.Kind));
        writer.WriteInt32(EncodeIntrinsic(Field.FieldType.IntrinsicKind!.Value));
        writer.WriteBoolean(Field.IsStatic);
        writer.WriteBoolean(Field.IsLiteral);
        writer.WriteBoolean(Field.HasRva);

        writer.WriteInt32(EncodeEvidenceStatus(EvidenceStatus));
        writer.WriteString(ReasonCode);
        writer.WriteDigest(SourceSha256);
        writer.WriteDigest(ImportedObjectSha256);
        writer.WriteUInt64(Address);
        writer.WriteInt32(RequestedLength);
        writer.WriteInt32(ObservedLength);
        writer.WriteBytes(observedBytes.AsSpan());
        return writer.ToImmutableArray();
    }

    private static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    private static int EncodeTypeKind(TypeSigKind kind) => kind switch
    {
        TypeSigKind.Intrinsic => 2,
        TypeSigKind.TypeDefinition => 3,
        _ => throw new InvalidOperationException("Field-load evidence contains an unsupported structural type kind."),
    };

    private static int EncodeIntrinsic(IntrinsicTypeKind kind) => kind switch
    {
        IntrinsicTypeKind.Int32 => 2,
        _ => throw new InvalidOperationException("Field-load evidence contains an unsupported intrinsic field type."),
    };

    private static int EncodeEvidenceStatus(EvaluationEvidenceStatus status) => status switch
    {
        EvaluationEvidenceStatus.Partial => 1,
        EvaluationEvidenceStatus.Unavailable => 2,
        _ => throw new InvalidOperationException("Field-load evidence contains an unsupported evidence status."),
    };

    private static void ValidateReasonCode(string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > MaximumReasonCodeLength)
        {
            throw new ArgumentException(
                $"A reason code must contain 1 to {MaximumReasonCodeLength} characters.",
                nameof(reasonCode));
        }

        static bool IsAlphaNumeric(char character) =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
        static bool IsSeparator(char character) => character is '.' or '_' or '-';

        if (!IsAlphaNumeric(reasonCode[0]) || !IsAlphaNumeric(reasonCode[^1]))
        {
            throw new ArgumentException(
                "A reason code must begin and end with an ASCII letter or digit.",
                nameof(reasonCode));
        }

        var previousWasSeparator = false;
        foreach (var character in reasonCode)
        {
            if (IsAlphaNumeric(character))
            {
                previousWasSeparator = false;
                continue;
            }

            if (!IsSeparator(character) || previousWasSeparator)
            {
                throw new ArgumentException(
                    "A reason code requires alphanumeric segments separated by one period, underscore, or hyphen.",
                    nameof(reasonCode));
            }

            previousWasSeparator = true;
        }
    }

    private static string NormalizeSha256(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != SHA256.HashSizeInBytes * 2)
        {
            throw new ArgumentException("A complete 64-character SHA-256 digest is required.", parameterName);
        }

        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "A SHA-256 digest must contain only hexadecimal characters.",
                parameterName,
                exception);
        }

        return value.ToLowerInvariant();
    }

    private sealed class CanonicalWriter
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        internal void WriteBoolean(bool value) => WriteInt32(value ? 1 : 0);

        internal void WriteInt32(int value)
        {
            var span = buffer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.Advance(sizeof(int));
        }

        internal void WriteUInt64(ulong value)
        {
            var span = buffer.GetSpan(sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.Advance(sizeof(ulong));
        }

        internal void WriteString(string value) => WriteBytes(Encoding.UTF8.GetBytes(value));

        internal void WriteDigest(string sha256) => WriteRaw(Convert.FromHexString(sha256));

        internal void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteInt32(value.Length);
            WriteRaw(value);
        }

        internal ImmutableArray<byte> ToImmutableArray() =>
            ImmutableArray.CreateRange(buffer.WrittenSpan.ToArray());

        private void WriteRaw(ReadOnlySpan<byte> value)
        {
            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }
    }
}
