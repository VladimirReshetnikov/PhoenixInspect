using System.Security.Cryptography;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>Classifies the bounded external subject that introduced an explained unknown.</summary>
public enum ProvenanceInputKind
{
    /// <summary>An explicit parameter of the root request.</summary>
    RequestArgument = 1,

    /// <summary>The root request's receiver slot.</summary>
    Receiver = 2,

    /// <summary>One imported field dependency; W4.3 owns its field-load transform.</summary>
    ImportedField = 3,
}

/// <summary>Classifies the append-only lineage-node vocabulary implemented through the W4.6 kernel.</summary>
public enum LineageNodeKind
{
    /// <summary>A partial or unavailable request input.</summary>
    InputOrigin = 1,

    /// <summary>An ordered arithmetic operation with at least one unknown operand.</summary>
    BinaryTransform = 2,

    /// <summary>An approximate imported-field read through one exact imported receiver.</summary>
    FieldLoadTransform = 3,

    /// <summary>An explained unknown crossing into one metadata-ordered admitted direct-call parameter.</summary>
    CallArgumentTransform = 4,

    /// <summary>An explained unknown returning from one interpreted callee to its frozen caller continuation.</summary>
    InterpretedReturnTransform = 5,

    /// <summary>An explained unknown returned by one versioned pure model over its ordered call arguments.</summary>
    ModeledReturnTransform = 6,
}

/// <summary>Classifies one exact or unknown operand embedded in a lineage transformation node.</summary>
public enum LineageOperandKind
{
    /// <summary>An exact signed 32-bit value embedded without allocating a lineage node.</summary>
    ExactInt32 = 1,

    /// <summary>A predecessor unknown identified by its content-addressed node.</summary>
    Unknown = 2,
}

/// <summary>Identifies a bounded canonical provenance source by its complete lowercase SHA-256 digest.</summary>
public readonly record struct ProvenanceSourceKey
{
    /// <summary>Creates a source key from a complete SHA-256 digest.</summary>
    /// <param name="sha256">Exactly 64 hexadecimal characters; accepted uppercase is normalized to lowercase.</param>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not one complete SHA-256 digest.</exception>
    public ProvenanceSourceKey(string sha256)
    {
        Sha256 = CanonicalSha256.Require(sha256, nameof(sha256));
    }

    /// <summary>Gets the canonical lowercase 64-character digest.</summary>
    public string Sha256 { get; }

    /// <summary>Hashes canonical source bytes into a provenance source key.</summary>
    /// <param name="canonicalBytes">The already bounded, deterministic source projection.</param>
    /// <returns>A complete SHA-256 identity for the supplied bytes.</returns>
    public static ProvenanceSourceKey Hash(ReadOnlySpan<byte> canonicalBytes) =>
        new(Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant());

    /// <summary>Returns the canonical lowercase digest.</summary>
    /// <returns><see cref="Sha256"/>.</returns>
    public override string ToString() => Sha256 ?? string.Empty;

    internal bool IsValid => Sha256 is not null;
}

/// <summary>
/// Identifies one imported receiver by the complete SHA-256 identity retained by its prepared object evidence.
/// </summary>
/// <remarks>
/// This identity is deliberately distinct from the concrete domain's numeric object-reference payload. Equal
/// imported evidence therefore produces equal lineage even when fixture allocation or import order assigns a
/// different local reference number.
/// </remarks>
public readonly record struct ImportedReceiverKey
{
    /// <summary>Creates a receiver key from one complete SHA-256 digest.</summary>
    /// <param name="sha256">Exactly 64 hexadecimal characters; accepted uppercase is normalized to lowercase.</param>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not one complete SHA-256 digest.</exception>
    public ImportedReceiverKey(string sha256)
    {
        Sha256 = CanonicalSha256.Require(sha256, nameof(sha256));
    }

    /// <summary>Gets the canonical lowercase 64-character digest.</summary>
    public string Sha256 { get; }

    /// <summary>Returns the canonical lowercase digest.</summary>
    /// <returns><see cref="Sha256"/>.</returns>
    public override string ToString() => Sha256 ?? string.Empty;

    internal bool IsValid => Sha256 is not null;
}

/// <summary>Identifies one immutable lineage node by the SHA-256 digest of its canonical bytes.</summary>
public readonly record struct LineageNodeId
{
    /// <summary>Creates a lineage identity from a complete SHA-256 digest.</summary>
    /// <param name="sha256">Exactly 64 hexadecimal characters; accepted uppercase is normalized to lowercase.</param>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not one complete SHA-256 digest.</exception>
    public LineageNodeId(string sha256)
    {
        Sha256 = CanonicalSha256.Require(sha256, nameof(sha256));
    }

    /// <summary>Gets the canonical lowercase 64-character digest.</summary>
    public string Sha256 { get; }

    /// <summary>Returns the canonical lowercase digest.</summary>
    /// <returns><see cref="Sha256"/>.</returns>
    public override string ToString() => Sha256 ?? string.Empty;

    internal bool IsValid => Sha256 is not null;

    internal static LineageNodeId Hash(ReadOnlySpan<byte> canonicalBytes) =>
        new(Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant());
}

/// <summary>Describes one validated partial or unavailable input that may ground an unknown value.</summary>
public sealed record ProvenanceInputOrigin
{
    /// <summary>Gets the maximum admitted stable reason-code length.</summary>
    public const int MaximumReasonCodeLength = 128;

    /// <summary>Creates a bounded, structured input origin.</summary>
    /// <param name="kind">The request subject category.</param>
    /// <param name="originIndex">
    /// A nonnegative parameter or prepared-dependency ordinal. A receiver must use zero.
    /// </param>
    /// <param name="evidence">The partial or unavailable evidence classification that caused unknownness.</param>
    /// <param name="sourceKey">A complete digest of the canonical request or evidence source identity.</param>
    /// <param name="reasonCode">A bounded stable code using ASCII letters, digits, period, underscore, or hyphen.</param>
    /// <param name="staticType">The exact non-void structural type of the unknown value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A discriminant is undefined, <paramref name="originIndex"/> is negative, or a receiver index is not zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Evidence is exact/conflicting/invalid, the source key is default, the reason code is noncanonical, or the
    /// static type is void or lacks a production structural identity.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="staticType"/> is <see langword="null"/>.</exception>
    public ProvenanceInputOrigin(
        ProvenanceInputKind kind,
        int originIndex,
        EvaluationEvidenceStatus evidence,
        ProvenanceSourceKey sourceKey,
        string reasonCode,
        TypeSig staticType)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (originIndex < 0 || kind == ProvenanceInputKind.Receiver && originIndex != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originIndex));
        }

        if (evidence is not (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            throw new ArgumentException(
                "An unknown input requires Partial or Unavailable evidence.",
                nameof(evidence));
        }

        if (!sourceKey.IsValid)
        {
            throw new ArgumentException("A non-default provenance source key is required.", nameof(sourceKey));
        }

        ValidateReasonCode(reasonCode);
        ArgumentNullException.ThrowIfNull(staticType);
        if (!HasCanonicalStructuralIdentity(staticType))
        {
            throw new ArgumentException(
                "A lineage origin requires a non-void production structural type identity.",
                nameof(staticType));
        }

        Kind = kind;
        OriginIndex = originIndex;
        Evidence = evidence;
        SourceKey = sourceKey;
        ReasonCode = reasonCode;
        StaticType = staticType;
    }

    /// <summary>Gets the request subject category.</summary>
    public ProvenanceInputKind Kind { get; }

    /// <summary>Gets the parameter or prepared-dependency ordinal.</summary>
    public int OriginIndex { get; }

    /// <summary>Gets the partial or unavailable evidence classification.</summary>
    public EvaluationEvidenceStatus Evidence { get; }

    /// <summary>Gets the canonical provenance source digest.</summary>
    public ProvenanceSourceKey SourceKey { get; }

    /// <summary>Gets the stable bounded reason code.</summary>
    public string ReasonCode { get; }

    /// <summary>Gets the exact structural type of the unknown input.</summary>
    public TypeSig StaticType { get; }

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
            throw new ArgumentException("A reason code must begin and end with an ASCII letter or digit.", nameof(reasonCode));
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

    private static bool HasCanonicalStructuralIdentity(TypeSig type) => type.Kind switch
    {
        TypeSigKind.Intrinsic => type.IntrinsicKind.HasValue,
        TypeSigKind.TypeDefinition => type.Module.HasValue && type.MetadataToken != 0,
        TypeSigKind.SzArray => type.ElementType is { } element && HasCanonicalStructuralIdentity(element),
        TypeSigKind.Void or TypeSigKind.Synthetic => false,
        _ => false,
    };
}

internal static class CanonicalSha256
{
    internal static string Require(string value, string parameterName)
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
            throw new ArgumentException("A SHA-256 digest must contain only hexadecimal characters.", parameterName, exception);
        }

        return value.ToLowerInvariant();
    }
}
