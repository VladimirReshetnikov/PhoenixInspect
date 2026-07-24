using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Classifies one bounded Portable-PDB artifact read returned by a host resolver.</summary>
/// <remarks>
/// Only byte acquisition is represented here. GUID/stamp validation, content hashing, candidate ambiguity, and PDB
/// decoding remain producer responsibilities. Numeric values are part of the draft W7 resolver contract.
/// </remarks>
public enum DumpPortablePdbArtifactReadStatus : byte
{
    /// <summary>Every declared artifact byte was acquired.</summary>
    Exact = 1,

    /// <summary>Only an immutable prefix of the declared artifact was acquired.</summary>
    Partial = 2,

    /// <summary>The resolver identified a candidate but could not acquire any bytes or determine its length.</summary>
    Unavailable = 3,
}

/// <summary>
/// Carries one defensively immutable, transient Portable-PDB candidate read from a host artifact resolver.
/// </summary>
/// <remarks>
/// <see cref="SourceId"/> is diagnostic resolver state and may be a path, URI, or store key. It is deliberately
/// excluded from every dump-expression identity; only complete bytes that pass producer validation can contribute
/// content identity. Caveat: this draft accepts standalone Portable PDB byte streams only.
/// </remarks>
public sealed class DumpPortablePdbArtifactRead
{
    private const int MaximumSourceIdCharacterCount = 4_096;
    private readonly ImmutableArray<byte> bytes;

    private DumpPortablePdbArtifactRead(
        string sourceId,
        DumpPortablePdbArtifactReadStatus status,
        long? declaredByteLength,
        ImmutableArray<byte> bytes)
    {
        SourceId = sourceId;
        Status = status;
        DeclaredByteLength = declaredByteLength;
        this.bytes = bytes;
    }

    /// <summary>Gets the nonempty transient resolver source identifier, excluded from canonical product evidence.</summary>
    public string SourceId { get; }

    /// <summary>Gets whether the resolver acquired all, some, or none of the candidate bytes.</summary>
    public DumpPortablePdbArtifactReadStatus Status { get; }

    /// <summary>Gets the positive declared complete length for exact/partial reads, or null when unavailable.</summary>
    public long? DeclaredByteLength { get; }

    /// <summary>Gets a defensive copy of the exact acquired bytes or partial prefix.</summary>
    public ImmutableArray<byte> Bytes => ImmutableArray.CreateRange(bytes);

    /// <summary>Creates a complete candidate read whose declared length is exactly the supplied byte count.</summary>
    /// <param name="sourceId">A nonempty transient resolver diagnostic identity.</param>
    /// <param name="bytes">An explicitly initialized complete artifact; empty bytes remain an invalid PDB candidate.</param>
    /// <returns>A defensively immutable exact candidate read.</returns>
    /// <exception cref="ArgumentException">The source is blank or the byte array is default.</exception>
    public static DumpPortablePdbArtifactRead Exact(string sourceId, ImmutableArray<byte> bytes)
    {
        ValidateSourceId(sourceId);
        if (bytes.IsDefault)
        {
            throw new ArgumentException("An exact Portable-PDB artifact byte array must be initialized.", nameof(bytes));
        }

        return new DumpPortablePdbArtifactRead(
            sourceId,
            DumpPortablePdbArtifactReadStatus.Exact,
            bytes.Length,
            ImmutableArray.CreateRange(bytes));
    }

    /// <summary>Creates an incomplete candidate read retaining only the observed immutable byte prefix.</summary>
    /// <param name="sourceId">A nonempty transient resolver diagnostic identity.</param>
    /// <param name="declaredByteLength">The positive declared complete artifact length.</param>
    /// <param name="observedPrefix">
    /// An explicitly initialized prefix strictly shorter than the declared length; an empty prefix is permitted.
    /// </param>
    /// <returns>A defensively immutable partial candidate read that cannot be decoded as a complete PDB.</returns>
    /// <exception cref="ArgumentException">The source/prefix is invalid or the prefix is not shorter than the artifact.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="declaredByteLength"/> is not positive.</exception>
    public static DumpPortablePdbArtifactRead Partial(
        string sourceId,
        long declaredByteLength,
        ImmutableArray<byte> observedPrefix)
    {
        ValidateSourceId(sourceId);
        if (declaredByteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredByteLength),
                "A partial artifact requires a positive declared length.");
        }

        if (observedPrefix.IsDefault || observedPrefix.Length >= declaredByteLength)
        {
            throw new ArgumentException(
                "A partial artifact prefix must be initialized and shorter than the declared artifact.",
                nameof(observedPrefix));
        }

        return new DumpPortablePdbArtifactRead(
            sourceId,
            DumpPortablePdbArtifactReadStatus.Partial,
            declaredByteLength,
            ImmutableArray.CreateRange(observedPrefix));
    }

    /// <summary>Creates a candidate identified by the resolver but unavailable as artifact bytes.</summary>
    /// <param name="sourceId">A nonempty transient resolver diagnostic identity.</param>
    /// <returns>An unavailable candidate read with no bytes or asserted length.</returns>
    /// <exception cref="ArgumentException"><paramref name="sourceId"/> is blank.</exception>
    public static DumpPortablePdbArtifactRead Unavailable(string sourceId)
    {
        ValidateSourceId(sourceId);
        return new DumpPortablePdbArtifactRead(
            sourceId,
            DumpPortablePdbArtifactReadStatus.Unavailable,
            null,
            ImmutableArray<byte>.Empty);
    }

    private static void ValidateSourceId(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > MaximumSourceIdCharacterCount)
        {
            throw new ArgumentException(
                $"A resolver source identity of at most {MaximumSourceIdCharacterCount} characters is required.",
                nameof(sourceId));
        }
    }
}

/// <summary>
/// Supplies the exact expected module debug identity and producer-enforced bounds to one host artifact-resolution call.
/// </summary>
/// <remarks>
/// This request is transient capability input rather than canonical expression evidence. A resolver may use the
/// GUID/stamp to query an artifact store, but it may not assert that returned bytes match; the producer revalidates
/// every complete candidate independently.
/// </remarks>
public sealed class DumpPortablePdbArtifactResolutionRequest
{
    internal DumpPortablePdbArtifactResolutionRequest(
        DumpModulePortablePdbDebugIdentity expectedModule,
        EvaluationDeterministicBound candidateBound,
        EvaluationDeterministicBound byteBound)
    {
        ExpectedModule = expectedModule;
        CandidateBound = candidateBound;
        ByteBound = byteBound;
    }

    /// <summary>Gets the exact counted module and CodeView GUID/stamp requiring a PDB candidate.</summary>
    public DumpModulePortablePdbDebugIdentity ExpectedModule { get; }

    /// <summary>Gets the maximum candidate-read count the producer will accept from this call.</summary>
    public EvaluationDeterministicBound CandidateBound { get; }

    /// <summary>Gets the maximum complete byte length the producer will accept for any one candidate.</summary>
    public EvaluationDeterministicBound ByteBound { get; }
}

/// <summary>Resolves bounded Portable-PDB artifact candidates for one exact mapped-module debug identity.</summary>
/// <remarks>
/// Implementations perform discovery/acquisition only and must not choose a winning candidate. Returned source ids
/// remain transient; the producer owns bounds, hashing, identity validation, ambiguity, and scope projection.
/// Caveat: implementations execute synchronously in the current draft host seam.
/// </remarks>
public interface IDumpPortablePdbArtifactResolver
{
    /// <summary>Resolves zero or more independent candidate reads under the producer-declared request bounds.</summary>
    /// <param name="request">The exact expected module debug identity and non-negotiable candidate/byte bounds.</param>
    /// <returns>
    /// An explicitly initialized candidate array. Empty means exhaustive absence; default is invalid resolver output.
    /// </returns>
    ImmutableArray<DumpPortablePdbArtifactRead> Resolve(DumpPortablePdbArtifactResolutionRequest request);
}
