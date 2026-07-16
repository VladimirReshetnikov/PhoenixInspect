using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies the structural operator used by an admitted fixed-depth member chain.</summary>
/// <remarks>
/// This draft W6 value describes an already-admitted project shape. It neither widens syntax admission nor implies
/// support for arbitrary compiler parse trees.
/// </remarks>
public enum DumpMemberChainAccessKind
{
    /// <summary>Both member hops use ordinary direct member access.</summary>
    Direct = 1,

    /// <summary>The terminal hop uses conditional access on the selected reference member.</summary>
    Conditional = 2,
}

/// <summary>Identifies the optional typed coalescing literal retained by a member-chain plan.</summary>
/// <remarks>This draft W6 value is interpreted only after an exact null terminal result.</remarks>
public enum DumpMemberChainFallbackKind
{
    /// <summary>The expression has no coalescing literal.</summary>
    None = 0,

    /// <summary>The expression coalesces to the null literal.</summary>
    Null = 1,

    /// <summary>The expression coalesces to one exact <see cref="int"/> literal.</summary>
    Int32 = 2,

    /// <summary>The expression coalesces to one decoded <see cref="string"/> literal.</summary>
    String = 3,
}

/// <summary>
/// Freezes one completely prepared W6 fixed-depth member chain before either its reference or terminal value is read.
/// </summary>
/// <remarks>
/// This draft plan is snapshot-, root-, and request-specific. It retains the complete request identity, declared-data
/// certificate, physical relative layout, preparation evidence, and semantics needed by later evaluation. It contains
/// no compiler object and gives evaluation no reason to repeat member, property, or type-catalog lookup.
/// </remarks>
public sealed class DumpMemberChainPlan
{
    private const string CanonicalVersion = "dump-member-chain-plan-v1";
    private const string UnreadValuesMarker = "reference-and-terminal-values-not-read-during-preparation";

    private readonly ImmutableArray<byte> requestCanonicalBytes;
    private readonly ImmutableArray<byte> expressionIdentityCanonicalBytes;
    private readonly ImmutableArray<MemoryReadResult> preparationEvidence;
    private readonly ImmutableArray<EvaluationDeterministicBound> preparationBounds;
    private readonly string canonicalProjection;

    internal DumpMemberChainPlan(
        DumpQueryRootBinding rootBinding,
        ClrmdDeclaredDataMemberCertificate certificate,
        DumpMemberChainAccessKind accessKind,
        DumpMemberChainFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback,
        ImmutableArray<byte> requestCanonicalBytes,
        string requestSha256,
        ImmutableArray<byte> expressionIdentityCanonicalBytes,
        string expressionIdentitySha256,
        ImmutableArray<MemoryReadResult> preparationEvidence,
        ImmutableArray<EvaluationDeterministicBound> preparationBounds)
    {
        RootBinding = rootBinding ?? throw new ArgumentNullException(nameof(rootBinding));
        Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
        {
            throw new ArgumentException("A member-chain plan requires one exact root binding.", nameof(rootBinding));
        }

        if (!Enum.IsDefined(accessKind))
        {
            throw new ArgumentOutOfRangeException(nameof(accessKind));
        }

        if (!Enum.IsDefined(fallbackKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackKind));
        }

        ValidateFallback(fallbackKind, int32Fallback, stringFallback);
        this.requestCanonicalBytes = RequireCanonicalBytes(
            requestCanonicalBytes,
            requestSha256,
            nameof(requestCanonicalBytes),
            nameof(requestSha256));
        this.expressionIdentityCanonicalBytes = RequireCanonicalBytes(
            expressionIdentityCanonicalBytes,
            expressionIdentitySha256,
            nameof(expressionIdentityCanonicalBytes),
            nameof(expressionIdentitySha256));
        this.preparationEvidence = preparationEvidence.IsDefault
            ? ImmutableArray<MemoryReadResult>.Empty
            : ImmutableArray.CreateRange(preparationEvidence.AsSpan().ToArray());
        this.preparationBounds = NormalizeBounds(preparationBounds);
        RequestSha256 = requestSha256;
        ExpressionIdentitySha256 = expressionIdentitySha256;
        AccessKind = accessKind;
        FallbackKind = fallbackKind;
        Int32Fallback = int32Fallback;
        StringFallback = stringFallback;
        SemanticMode = EvaluationSemanticMode.DerivedQuery;
        canonicalProjection = CreateCanonicalProjection();
        Sha256 = ComputeDigest(Encoding.UTF8.GetBytes(canonicalProjection));
    }

    /// <summary>Gets the exact named root binding retained by the accepted request.</summary>
    public DumpQueryRootBinding RootBinding { get; }

    /// <summary>Gets the complete detached declaration, property, decoder, and relative-storage certificate.</summary>
    public ClrmdDeclaredDataMemberCertificate Certificate { get; }

    /// <summary>Gets whether the terminal hop is direct or null-conditional.</summary>
    public DumpMemberChainAccessKind AccessKind { get; }

    /// <summary>Gets the optional coalescing-literal discriminator validated against the terminal decoder.</summary>
    public DumpMemberChainFallbackKind FallbackKind { get; }

    /// <summary>Gets the exact integer fallback only for <see cref="DumpMemberChainFallbackKind.Int32"/>.</summary>
    public int? Int32Fallback { get; }

    /// <summary>Gets the decoded string fallback only for <see cref="DumpMemberChainFallbackKind.String"/>.</summary>
    public string? StringFallback { get; }

    /// <summary>Gets the derived-query truth mode used by every accepted W6 member chain.</summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets the SHA-256 identity of the complete accepted product request.</summary>
    public string RequestSha256 { get; }

    /// <summary>Gets the SHA-256 identity of the separately tagged member-chain expression.</summary>
    public string ExpressionIdentitySha256 { get; }

    /// <summary>Gets the SHA-256 identity of the complete canonical plan projection.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets the explicit invariant that preparation did not read the outer reference value.
    /// </summary>
    public bool ReferenceValueReadDuringPreparation => false;

    /// <summary>
    /// Gets the explicit invariant that preparation did not read the terminal value or invoke a certified getter.
    /// </summary>
    public bool TerminalValueReadDuringPreparation => false;

    /// <summary>Gets a defensive copy of the complete accepted-request canonical bytes.</summary>
    public ImmutableArray<byte> RequestCanonicalBytes =>
        ImmutableArray.CreateRange(requestCanonicalBytes.AsSpan().ToArray());

    /// <summary>Gets a defensive copy of the separately tagged member-chain expression identity bytes.</summary>
    public ImmutableArray<byte> ExpressionIdentityCanonicalBytes =>
        ImmutableArray.CreateRange(expressionIdentityCanonicalBytes.AsSpan().ToArray());

    /// <summary>
    /// Gets a defensive copy of the reads performed while producing the declaration certificate. The outer reference
    /// and terminal-value reads are absent because evaluation has not begun.
    /// </summary>
    public ImmutableArray<MemoryReadResult> PreparationEvidence =>
        ImmutableArray.CreateRange(preparationEvidence.AsSpan().ToArray());

    /// <summary>Gets an ordinal-name-ordered defensive copy of bounds actually reached during declaration binding.</summary>
    public ImmutableArray<EvaluationDeterministicBound> PreparationBounds =>
        ImmutableArray.CreateRange(preparationBounds.AsSpan().ToArray());

    /// <summary>Produces the injective versioned representation of the complete prepared member chain.</summary>
    /// <returns>
    /// A deterministic length-delimited projection containing exact request/certificate identities, preparation
    /// evidence and bounds, semantics, and explicit unread-value markers.
    /// </returns>
    /// <remarks>
    /// The draft representation contains dump addresses and evidence bytes. It is replay material and is not intended
    /// for diagnostic display.
    /// </remarks>
    public string ToCanonicalReplayProjection() => canonicalProjection;

    private static ImmutableArray<byte> RequireCanonicalBytes(
        ImmutableArray<byte> bytes,
        string digest,
        string bytesParameterName,
        string digestParameterName)
    {
        if (bytes.IsDefaultOrEmpty)
        {
            throw new ArgumentException("Canonical identity bytes are required.", bytesParameterName);
        }

        if (digest is null ||
            digest.Length != 64 ||
            !string.Equals(ComputeDigest(bytes.AsSpan()), digest, StringComparison.Ordinal))
        {
            throw new ArgumentException("The canonical identity digest does not match its bytes.", digestParameterName);
        }

        return ImmutableArray.CreateRange(bytes.AsSpan().ToArray());
    }

    private static void ValidateFallback(
        DumpMemberChainFallbackKind kind,
        int? int32Fallback,
        string? stringFallback)
    {
        var valid = kind switch
        {
            DumpMemberChainFallbackKind.None or DumpMemberChainFallbackKind.Null =>
                int32Fallback is null && stringFallback is null,
            DumpMemberChainFallbackKind.Int32 => int32Fallback is not null && stringFallback is null,
            DumpMemberChainFallbackKind.String => int32Fallback is null && stringFallback is not null,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("The member-chain fallback discriminator and payload disagree.");
        }
    }

    private static ImmutableArray<EvaluationDeterministicBound> NormalizeBounds(
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        var normalized = bounds.IsDefault
            ? ImmutableArray<EvaluationDeterministicBound>.Empty
            : bounds;
        if (normalized.Any(static bound => bound is null))
        {
            throw new ArgumentException("Preparation bounds cannot contain null entries.", nameof(bounds));
        }

        normalized = normalized.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToImmutableArray();
        for (var index = 1; index < normalized.Length; index++)
        {
            if (string.Equals(normalized[index - 1].Name, normalized[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Preparation bound '{normalized[index].Name}' occurs more than once.",
                    nameof(bounds));
            }
        }

        return normalized;
    }

    private string CreateCanonicalProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Convert.ToHexString(requestCanonicalBytes.AsSpan()));
        Append(builder, RequestSha256);
        Append(builder, Convert.ToHexString(expressionIdentityCanonicalBytes.AsSpan()));
        Append(builder, ExpressionIdentitySha256);
        Append(builder, Certificate.ToCanonicalReplayProjection());
        Append(builder, AccessKind.ToString());
        Append(builder, FallbackKind.ToString());
        Append(builder, Int32Fallback?.ToString(CultureInfo.InvariantCulture) ?? "none");
        Append(builder, StringFallback is null ? "none" : "value");
        Append(builder, StringFallback ?? string.Empty);
        Append(builder, SemanticMode.ToString());
        Append(builder, UnreadValuesMarker);
        Append(builder, preparationEvidence.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var read in preparationEvidence)
        {
            Append(builder, read.SourceId);
            Append(builder, read.Address.ToString("x16", CultureInfo.InvariantCulture));
            Append(builder, read.RequestedLength.ToString(CultureInfo.InvariantCulture));
            Append(builder, read.Status.ToString());
            Append(builder, Convert.ToHexString(read.Bytes.AsSpan()));
        }

        Append(builder, preparationBounds.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var bound in preparationBounds)
        {
            Append(builder, bound.Name);
            Append(builder, bound.Value.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string ComputeDigest(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}
