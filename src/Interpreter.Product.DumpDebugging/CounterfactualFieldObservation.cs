using System.Buffers.Binary;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Freezes one exact or non-exact ordinary instance <c>Int32</c> field observation relative to a prepared graph.
/// </summary>
/// <remarks>
/// The value is content-equal and canonical. It retains read geometry for every evidence disposition while exposing a
/// scalar only for exactly four exact little-endian bytes. Partial and unavailable observations additionally retain
/// the Core evidence value required by the future execution memory adapter. This draft schema is deliberately limited
/// to W4's ordinary instance <c>Int32</c> profile.
/// </remarks>
public sealed class CounterfactualFieldObservation : IEquatable<CounterfactualFieldObservation>
{
    /// <summary>Gets the current canonical binary schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    private readonly ImmutableArray<byte> observedBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private CounterfactualFieldObservation(
        int dependencyOrdinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidenceStatus,
        string? reasonCode,
        string sourceSha256,
        string importedObjectSha256,
        ulong address,
        int requestedLength,
        ReadOnlySpan<byte> observedBytes)
    {
        ValidateCommon(
            dependencyOrdinal,
            field,
            evidenceStatus,
            reasonCode,
            address,
            requestedLength,
            observedBytes.Length);

        SchemaVersion = CanonicalSchemaVersion;
        DependencyOrdinal = dependencyOrdinal;
        Field = field;
        EvidenceStatus = evidenceStatus;
        ReasonCode = reasonCode;
        SourceSha256 = CounterfactualCanonical.ValidateSha256(sourceSha256, nameof(sourceSha256));
        ImportedObjectSha256 = CounterfactualCanonical.ValidateSha256(
            importedObjectSha256,
            nameof(importedObjectSha256));
        Address = address;
        RequestedLength = requestedLength;
        this.observedBytes = ImmutableArray.CreateRange(observedBytes.ToArray());
        ExactInt32 = evidenceStatus == EvaluationEvidenceStatus.Exact
            ? BinaryPrimitives.ReadInt32LittleEndian(this.observedBytes.AsSpan())
            : null;
        RuntimeFieldEvidence = evidenceStatus is EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable
            ? new FieldLoadEvidence(
                dependencyOrdinal,
                field,
                evidenceStatus,
                reasonCode!,
                SourceSha256,
                ImportedObjectSha256,
                address,
                requestedLength,
                this.observedBytes.AsSpan())
            : null;
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the observation canonical schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the zero-based ordinal in the plan's canonical field-dependency vector.</summary>
    public int DependencyOrdinal { get; }

    /// <summary>Gets the complete frozen ordinary instance <c>Int32</c> field descriptor.</summary>
    public ResolvedField Field { get; }

    /// <summary>Gets the exact, partial, unavailable, conflict, or invalid evidence classification.</summary>
    public EvaluationEvidenceStatus EvidenceStatus { get; }

    /// <summary>
    /// Gets the bounded canonical reason for a non-exact observation, or <see langword="null"/> for exact evidence.
    /// </summary>
    public string? ReasonCode { get; }

    /// <summary>Gets the normalized complete SHA-256 digest of the memory-evidence source identity.</summary>
    public string SourceSha256 { get; }

    /// <summary>Gets the normalized complete SHA-256 digest of the prepared imported receiver.</summary>
    public string ImportedObjectSha256 { get; }

    /// <summary>Gets the nonzero starting target address of the nonoverflowing four-byte range.</summary>
    public ulong Address { get; }

    /// <summary>Gets the requested byte count, which is always four.</summary>
    public int RequestedLength { get; }

    /// <summary>Gets the number of bytes actually observed.</summary>
    public int ObservedLength => observedBytes.Length;

    /// <summary>Gets a defensive copy of the observed byte vector.</summary>
    public ImmutableArray<byte> ObservedBytes => CounterfactualCanonical.Copy(observedBytes);

    /// <summary>
    /// Gets the exact little-endian <c>Int32</c> only for exact four-byte evidence; otherwise gets
    /// <see langword="null"/>.
    /// </summary>
    public int? ExactInt32 { get; }

    /// <summary>
    /// Gets the canonical Core approximation-evidence digest for partial or unavailable evidence; otherwise gets
    /// <see langword="null"/>. The raw Core evidence object remains internal.
    /// </summary>
    public string? ApproximationEvidenceSha256 => RuntimeFieldEvidence?.Sha256;

    /// <summary>Gets a defensive copy of the domain-separated canonical schema-v1 bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CounterfactualCanonical.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal FieldLoadEvidence? RuntimeFieldEvidence { get; }

    /// <summary>Creates one exact four-byte little-endian <c>Int32</c> observation.</summary>
    /// <param name="dependencyOrdinal">The zero-based canonical graph-field ordinal.</param>
    /// <param name="field">The complete ordinary instance <c>Int32</c> field descriptor.</param>
    /// <param name="sourceSha256">The complete digest of the request's evidence-source identity.</param>
    /// <param name="importedObjectSha256">The complete digest of the imported rooted receiver.</param>
    /// <param name="address">The nonzero start of a nonoverflowing four-byte target range.</param>
    /// <param name="requestedLength">The requested width, which must be four.</param>
    /// <param name="observedBytes">Exactly four bytes in target little-endian order.</param>
    /// <returns>A canonical exact observation whose <see cref="ExactInt32"/> is derived from the copied bytes.</returns>
    public static CounterfactualFieldObservation CreateExactInt32(
        int dependencyOrdinal,
        ResolvedField field,
        string sourceSha256,
        string importedObjectSha256,
        ulong address,
        int requestedLength,
        ReadOnlySpan<byte> observedBytes) =>
        new(
            dependencyOrdinal,
            field,
            EvaluationEvidenceStatus.Exact,
            null,
            sourceSha256,
            importedObjectSha256,
            address,
            requestedLength,
            observedBytes);

    /// <summary>Creates one non-exact four-byte <c>Int32</c> read observation without inventing a scalar.</summary>
    /// <param name="dependencyOrdinal">The zero-based canonical graph-field ordinal.</param>
    /// <param name="field">The complete ordinary instance <c>Int32</c> field descriptor.</param>
    /// <param name="evidenceStatus">Partial, unavailable, conflict, or invalid evidence.</param>
    /// <param name="reasonCode">The bounded canonical reason for the non-exact disposition.</param>
    /// <param name="sourceSha256">The complete digest of the request's evidence-source identity.</param>
    /// <param name="importedObjectSha256">The complete digest of the imported rooted receiver.</param>
    /// <param name="address">The nonzero start of a nonoverflowing four-byte target range.</param>
    /// <param name="requestedLength">The requested width, which must be four.</param>
    /// <param name="observedBytes">
    /// One to three prefix bytes for partial, no bytes for unavailable, or zero to four bytes for conflict/invalid.
    /// </param>
    /// <returns>A canonical value-free observation.</returns>
    public static CounterfactualFieldObservation CreateNonExactInt32(
        int dependencyOrdinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidenceStatus,
        string reasonCode,
        string sourceSha256,
        string importedObjectSha256,
        ulong address,
        int requestedLength,
        ReadOnlySpan<byte> observedBytes) =>
        new(
            dependencyOrdinal,
            field,
            evidenceStatus,
            reasonCode,
            sourceSha256,
            importedObjectSha256,
            address,
            requestedLength,
            observedBytes);

    /// <inheritdoc />
    public bool Equals(CounterfactualFieldObservation? other) =>
        ReferenceEquals(this, other) ||
        other is not null && canonicalBytes.AsSpan().SequenceEqual(other.canonicalBytes.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CounterfactualFieldObservation);

    /// <inheritdoc />
    public override int GetHashCode() =>
        BinaryPrimitives.ReadInt32BigEndian(Convert.FromHexString(Sha256));

    private static void ValidateCommon(
        int dependencyOrdinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidenceStatus,
        string? reasonCode,
        ulong address,
        int requestedLength,
        int observedLength)
    {
        if (dependencyOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dependencyOrdinal));
        }

        ArgumentNullException.ThrowIfNull(field);
        if (field.FieldType != TypeSig.Int32 || field.IsStatic || field.IsLiteral || field.HasRva)
        {
            throw new ArgumentException(
                "A counterfactual observation requires an ordinary instance Int32 field.",
                nameof(field));
        }

        if (!Enum.IsDefined(evidenceStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceStatus));
        }

        if (evidenceStatus == EvaluationEvidenceStatus.Exact)
        {
            if (reasonCode is not null || observedLength != sizeof(int))
            {
                throw new ArgumentException("Exact evidence requires four bytes and no reason code.");
            }
        }
        else
        {
            ValidateReasonCode(reasonCode);
            var validLength = evidenceStatus switch
            {
                EvaluationEvidenceStatus.Partial => observedLength is >= 1 and <= 3,
                EvaluationEvidenceStatus.Unavailable => observedLength == 0,
                EvaluationEvidenceStatus.Conflict or EvaluationEvidenceStatus.Invalid =>
                    observedLength is >= 0 and <= sizeof(int),
                _ => false,
            };
            if (!validLength)
            {
                throw new ArgumentException(
                    "Observed bytes do not match the non-exact evidence classification.",
                    nameof(observedLength));
            }
        }

        if (requestedLength != sizeof(int))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLength));
        }

        if (address == 0 || address > ulong.MaxValue - (sizeof(int) - 1UL))
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
    }

    private static void ValidateReasonCode(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > FieldLoadEvidence.MaximumReasonCodeLength)
        {
            throw new ArgumentException("A bounded canonical non-exact reason code is required.", nameof(reasonCode));
        }

        static bool IsAlphaNumeric(char value) =>
            value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
        static bool IsSeparator(char value) => value is '.' or '_' or '-';

        if (!IsAlphaNumeric(reasonCode[0]) || !IsAlphaNumeric(reasonCode[^1]))
        {
            throw new ArgumentException("A reason must begin and end with an ASCII alphanumeric.", nameof(reasonCode));
        }

        var previousWasSeparator = false;
        foreach (var character in reasonCode)
        {
            if (IsAlphaNumeric(character))
            {
                previousWasSeparator = false;
            }
            else if (!IsSeparator(character) || previousWasSeparator)
            {
                throw new ArgumentException("A reason requires singly separated ASCII segments.", nameof(reasonCode));
            }
            else
            {
                previousWasSeparator = true;
            }
        }
    }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("Interpreter.CounterfactualFieldObservation");
        writer.WriteInt32(SchemaVersion);
        writer.WriteInt32(DependencyOrdinal);
        writer.WriteField(Field.Handle);
        writer.WriteType(Field.DeclaringType);
        writer.WriteType(Field.FieldType);
        writer.WriteBoolean(Field.IsStatic);
        writer.WriteBoolean(Field.IsLiteral);
        writer.WriteBoolean(Field.HasRva);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(EvidenceStatus));
        writer.WriteBoolean(ReasonCode is not null);
        if (ReasonCode is { } reason)
        {
            writer.WriteString(reason);
        }

        writer.WriteDigest(SourceSha256);
        writer.WriteDigest(ImportedObjectSha256);
        writer.WriteUInt64(Address);
        writer.WriteInt32(RequestedLength);
        writer.WriteInt32(ObservedLength);
        writer.WriteBytes(observedBytes.AsSpan());
        return writer.ToImmutableArray();
    }
}
