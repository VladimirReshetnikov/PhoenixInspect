using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Identifies the portable CodeView record and matching Portable-PDB content identifier for one module.
/// </summary>
/// <remarks>
/// Portable identity is exactly GUID plus stamp with CodeView age one. A CodeView path or file name is deliberately
/// absent because neither is an artifact identity or a trustworthy binding input.
/// </remarks>
public sealed class DumpPortablePdbDebugIdentity : IEquatable<DumpPortablePdbDebugIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbDebugIdentity(Guid guid, uint stamp)
    {
        Guid = guid;
        Stamp = stamp;

        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-debug-identity", 1);
        writer.WriteRawBytes(guid.ToByteArray());
        writer.WriteUInt32(stamp);
        writer.WriteInt32(Age);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact portable content-id GUID shared by the module CodeView entry and Portable PDB.</summary>
    public Guid Guid { get; }

    /// <summary>Gets the portable content-id stamp shared by the module CodeView entry and Portable PDB.</summary>
    public uint Stamp { get; }

    /// <summary>Gets the required Portable-PDB CodeView age, which is always one.</summary>
    public int Age => 1;

    /// <summary>Gets a defensive copy of the canonical GUID/stamp/age bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact Portable-PDB debug identity.</summary>
    /// <param name="guid">The exact portable content-id GUID, including an observed all-zero value.</param>
    /// <param name="stamp">The portable content-id stamp; zero remains a valid observed value.</param>
    /// <param name="age">The CodeView age, which must be exactly one for a Portable PDB.</param>
    /// <returns>A path-independent debug identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="age"/> is not one.</exception>
    public static DumpPortablePdbDebugIdentity Create(Guid guid, uint stamp, int age = 1)
    {
        if (age != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "A Portable-PDB CodeView age must be exactly one.");
        }

        return new DumpPortablePdbDebugIdentity(guid, stamp);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The debug identity to compare.</param>
    /// <returns><see langword="true"/> when GUID, stamp, and portable age are equal.</returns>
    public bool Equals(DumpPortablePdbDebugIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbDebugIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>Identifies the complete bounded Portable-PDB artifact bytes without retaining the bytes or a path.</summary>
/// <remarks>The positive byte count and SHA-256 digest are artifact evidence, not dump-memory evidence.</remarks>
public sealed class DumpPortablePdbContentIdentity : IEquatable<DumpPortablePdbContentIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbContentIdentity(int byteLength, string sha256)
    {
        ByteLength = byteLength;
        Sha256 = sha256;

        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-content-identity", 1);
        writer.WriteInt32(byteLength);
        writer.WriteSha256(sha256, nameof(sha256));
        canonicalBytes = writer.ToImmutableArray();
        CanonicalSha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact positive number of bytes in the complete Portable-PDB artifact.</summary>
    public int ByteLength { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the complete Portable-PDB artifact bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Gets a defensive copy of the versioned content-identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string CanonicalSha256 { get; }

    /// <summary>Creates a complete Portable-PDB content identity from a persisted length and digest.</summary>
    /// <param name="byteLength">The exact positive artifact byte count.</param>
    /// <param name="sha256">A complete 64-character SHA-256 digest.</param>
    /// <returns>A path-independent artifact-content identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="byteLength"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="sha256"/> is not a SHA-256 digest.</exception>
    public static DumpPortablePdbContentIdentity Create(int byteLength, string sha256)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "A Portable-PDB artifact cannot be empty.");
        }

        return new DumpPortablePdbContentIdentity(
            byteLength,
            CanonicalReplayEncoding.NormalizeSha256(sha256, nameof(sha256)));
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The artifact-content identity to compare.</param>
    /// <returns><see langword="true"/> when length and content digest are equal.</returns>
    public bool Equals(DumpPortablePdbContentIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbContentIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>Combines complete Portable-PDB content identity with the content identifier decoded from those bytes.</summary>
public sealed class DumpPortablePdbArtifactIdentity : IEquatable<DumpPortablePdbArtifactIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbArtifactIdentity(
        DumpPortablePdbContentIdentity content,
        DumpPortablePdbDebugIdentity debugIdentity)
    {
        Content = content;
        DebugIdentity = debugIdentity;
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-artifact-identity", 1);
        writer.WriteLengthPrefixedBytes(content.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(debugIdentity.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact length and digest of the complete artifact bytes.</summary>
    public DumpPortablePdbContentIdentity Content { get; }

    /// <summary>Gets the GUID/stamp identifier decoded from the Portable-PDB metadata header.</summary>
    public DumpPortablePdbDebugIdentity DebugIdentity { get; }

    /// <summary>Gets a defensive copy of the canonical artifact-identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an identity for one completely hashed and successfully decoded Portable PDB artifact.</summary>
    /// <param name="content">The complete artifact-content identity.</param>
    /// <param name="debugIdentity">The content identifier decoded from the same bytes.</param>
    /// <returns>An immutable exact artifact identity.</returns>
    public static DumpPortablePdbArtifactIdentity Create(
        DumpPortablePdbContentIdentity content,
        DumpPortablePdbDebugIdentity debugIdentity)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(debugIdentity);
        return new DumpPortablePdbArtifactIdentity(content, debugIdentity);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The artifact identity to compare.</param>
    /// <returns><see langword="true"/> when content and decoded debug identities are equal.</returns>
    public bool Equals(DumpPortablePdbArtifactIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbArtifactIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>Freezes the Portable-PDB CodeView identity acquired for one counted runtime module.</summary>
/// <remarks>Module paths and CodeView paths are excluded; runtime and metadata content identities remain separate.</remarks>
public sealed class DumpModulePortablePdbDebugIdentity : IEquatable<DumpModulePortablePdbDebugIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpModulePortablePdbDebugIdentity(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        DumpPortablePdbDebugIdentity debugIdentity)
    {
        RuntimeModule = runtimeModule;
        ModuleContent = moduleContent;
        DebugIdentity = debugIdentity;
        var writer = new CanonicalReplayEncoding.Writer("dump-module-portable-pdb-debug-identity", 1);
        DumpContextContractEncoding.WriteRuntimeModule(writer, runtimeModule);
        DumpContextContractEncoding.WriteModuleContent(writer, moduleContent);
        writer.WriteLengthPrefixedBytes(debugIdentity.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the snapshot-scoped module from which the CodeView identity was acquired.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the complete counted metadata-content identity of the runtime module.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the path-independent portable CodeView GUID, stamp, and age.</summary>
    public DumpPortablePdbDebugIdentity DebugIdentity { get; }

    /// <summary>Gets a defensive copy of the canonical module/debug-identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a validated module Portable-PDB debug identity.</summary>
    /// <param name="runtimeModule">The non-default snapshot-scoped runtime module.</param>
    /// <param name="moduleContent">The complete counted metadata identity for that module.</param>
    /// <param name="debugIdentity">The Portable-PDB CodeView identity acquired for the module.</param>
    /// <returns>A path-independent module/debug relation.</returns>
    public static DumpModulePortablePdbDebugIdentity Create(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        DumpPortablePdbDebugIdentity debugIdentity)
    {
        DumpContextContractEncoding.ValidateRuntimeModule(runtimeModule, nameof(runtimeModule));
        ArgumentNullException.ThrowIfNull(moduleContent);
        ArgumentNullException.ThrowIfNull(debugIdentity);
        return new DumpModulePortablePdbDebugIdentity(runtimeModule, moduleContent, debugIdentity);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The module debug identity to compare.</param>
    /// <returns><see langword="true"/> when module, metadata, and CodeView identities are equal.</returns>
    public bool Equals(DumpModulePortablePdbDebugIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpModulePortablePdbDebugIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Retains the immutable source boundary reached while locating and validating a Portable PDB.
/// </summary>
/// <remarks>
/// A source may stop at the snapshot, at module debug identity, at hashed candidate bytes, or after decoding a
/// candidate content identifier. The candidate is not an exact PDB context payload. In particular, a mismatching
/// candidate is retained here only so a conflict observation can explain the disagreement without expanding imports.
/// </remarks>
public sealed class DumpPortablePdbEvidenceSource : IEquatable<DumpPortablePdbEvidenceSource>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbEvidenceSource(
        ClrmdSnapshotIdentity snapshot,
        DumpModulePortablePdbDebugIdentity? moduleDebugIdentity,
        DumpPortablePdbContentIdentity? candidateContent,
        DumpPortablePdbDebugIdentity? observedDebugIdentity)
    {
        Snapshot = snapshot;
        ModuleDebugIdentity = moduleDebugIdentity;
        CandidateContent = candidateContent;
        ObservedDebugIdentity = observedDebugIdentity;

        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-evidence-source", 1);
        DumpContextContractEncoding.WriteSnapshot(writer, snapshot);
        WriteOptional(writer, moduleDebugIdentity?.CanonicalBytes ?? ImmutableArray<byte>.Empty, moduleDebugIdentity is not null);
        WriteOptional(writer, candidateContent?.CanonicalBytes ?? ImmutableArray<byte>.Empty, candidateContent is not null);
        WriteOptional(writer, observedDebugIdentity?.CanonicalBytes ?? ImmutableArray<byte>.Empty, observedDebugIdentity is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the immutable dump snapshot from which context acquisition began.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the exact module CodeView identity when that stage was reached; otherwise gets null.</summary>
    public DumpModulePortablePdbDebugIdentity? ModuleDebugIdentity { get; }

    /// <summary>Gets hashed candidate artifact content when candidate bytes were acquired; otherwise gets null.</summary>
    public DumpPortablePdbContentIdentity? CandidateContent { get; }

    /// <summary>Gets the candidate's decoded content identifier when decoding succeeded; otherwise gets null.</summary>
    public DumpPortablePdbDebugIdentity? ObservedDebugIdentity { get; }

    /// <summary>Gets whether decoded candidate identity disagrees with the module CodeView GUID or stamp.</summary>
    public bool HasIdentityMismatch =>
        ModuleDebugIdentity is not null &&
        ObservedDebugIdentity is not null &&
        !ModuleDebugIdentity.DebugIdentity.Equals(ObservedDebugIdentity);

    /// <summary>Gets a defensive copy of the canonical source-boundary bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the source retained when PDB acquisition did not progress beyond one snapshot.</summary>
    /// <param name="snapshot">The immutable dump snapshot.</param>
    /// <returns>A snapshot-only evidence source carrying no module or artifact assertion.</returns>
    public static DumpPortablePdbEvidenceSource ForSnapshot(ClrmdSnapshotIdentity snapshot)
    {
        DumpContextContractEncoding.ValidateSnapshot(snapshot, nameof(snapshot));
        return new DumpPortablePdbEvidenceSource(snapshot, null, null, null);
    }

    /// <summary>Creates the source retained after exact module CodeView identity acquisition.</summary>
    /// <param name="moduleDebugIdentity">The exact counted module/debug relation.</param>
    /// <returns>A source carrying no assertion that candidate PDB bytes were found.</returns>
    public static DumpPortablePdbEvidenceSource ForModule(
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity)
    {
        ArgumentNullException.ThrowIfNull(moduleDebugIdentity);
        return new DumpPortablePdbEvidenceSource(
            moduleDebugIdentity.RuntimeModule.Snapshot,
            moduleDebugIdentity,
            null,
            null);
    }

    /// <summary>Creates the source retained after bounded candidate bytes were hashed and optionally decoded.</summary>
    /// <param name="moduleDebugIdentity">The module's expected CodeView identity.</param>
    /// <param name="candidateContent">The exact length and digest of the acquired candidate bytes.</param>
    /// <param name="observedDebugIdentity">
    /// The content identifier decoded from the candidate, or null when malformed bytes prevented decoding.
    /// </param>
    /// <returns>A complete candidate-source boundary; identity match is not implied.</returns>
    public static DumpPortablePdbEvidenceSource ForCandidate(
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        DumpPortablePdbContentIdentity candidateContent,
        DumpPortablePdbDebugIdentity? observedDebugIdentity)
    {
        ArgumentNullException.ThrowIfNull(moduleDebugIdentity);
        ArgumentNullException.ThrowIfNull(candidateContent);
        return new DumpPortablePdbEvidenceSource(
            moduleDebugIdentity.RuntimeModule.Snapshot,
            moduleDebugIdentity,
            candidateContent,
            observedDebugIdentity);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The evidence source to compare.</param>
    /// <returns><see langword="true"/> when every reached path-independent source fact is equal.</returns>
    public bool Equals(DumpPortablePdbEvidenceSource? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbEvidenceSource);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static void WriteOptional(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<byte> bytes,
        bool present)
    {
        writer.WriteBoolean(present);
        if (present)
        {
            writer.WriteLengthPrefixedBytes(bytes.AsSpan());
        }
    }
}

/// <summary>Identifies one exact Portable-PDB document row without retaining its display name or source path.</summary>
public sealed class DumpPortablePdbDocumentIdentity : IEquatable<DumpPortablePdbDocumentIdentity>
{
    private readonly ImmutableArray<byte> checksum;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbDocumentIdentity(
        int documentToken,
        Guid language,
        Guid checksumAlgorithm,
        ImmutableArray<byte> checksum)
    {
        DocumentToken = documentToken;
        Language = language;
        ChecksumAlgorithm = checksumAlgorithm;
        this.checksum = CanonicalReplayEncoding.Copy(checksum);
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-document-identity", 1);
        writer.WriteInt32(documentToken);
        writer.WriteRawBytes(language.ToByteArray());
        writer.WriteRawBytes(checksumAlgorithm.ToByteArray());
        writer.WriteLengthPrefixedBytes(checksum.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil Document-table token.</summary>
    public int DocumentToken { get; }

    /// <summary>Gets the exact document language GUID from Portable PDB metadata.</summary>
    public Guid Language { get; }

    /// <summary>Gets the checksum-algorithm GUID, or empty when the document row has a nil hash-algorithm handle.</summary>
    public Guid ChecksumAlgorithm { get; }

    /// <summary>Gets a defensive copy of the exact checksum, which is empty only with an empty algorithm GUID.</summary>
    public ImmutableArray<byte> Checksum => CanonicalReplayEncoding.Copy(checksum);

    /// <summary>Gets a defensive copy of the canonical document identity, excluding document name and path.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a path-independent exact Portable-PDB document identity.</summary>
    /// <param name="documentToken">A non-nil Document-table token.</param>
    /// <param name="language">The exact language GUID, including an observed empty GUID.</param>
    /// <param name="checksumAlgorithm">
    /// The checksum-algorithm GUID, or empty when Portable PDB metadata carries a nil hash-algorithm handle.
    /// </param>
    /// <param name="checksum">
    /// An initialized exact checksum. It may be empty exactly when <paramref name="checksumAlgorithm"/> is empty.
    /// </param>
    /// <returns>An immutable exact document identity.</returns>
    public static DumpPortablePdbDocumentIdentity Create(
        int documentToken,
        Guid language,
        Guid checksumAlgorithm,
        ImmutableArray<byte> checksum)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(documentToken, 0x30, nameof(documentToken));
        if (checksum.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized document checksum is required.", nameof(checksum));
        }

        if ((checksumAlgorithm == Guid.Empty) != checksum.IsEmpty)
        {
            throw new ArgumentException(
                "Document hash algorithm and checksum handles must either both be nil or both be present.",
                nameof(checksum));
        }

        return new DumpPortablePdbDocumentIdentity(
            documentToken,
            language,
            checksumAlgorithm,
            CanonicalReplayEncoding.Copy(checksum));
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The document identity to compare.</param>
    /// <returns><see langword="true"/> when token, language, algorithm, and checksum are equal.</returns>
    public bool Equals(DumpPortablePdbDocumentIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbDocumentIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>Classifies the bounded subset of decoded Portable-PDB import definitions retained by W7.</summary>
public enum DumpPortablePdbImportKind : byte
{
    /// <summary>A namespace import contributes one namespace candidate prefix.</summary>
    Namespace = 1,

    /// <summary>A type alias maps one simple alias to one decoded type name.</summary>
    TypeAlias = 2,

    /// <summary>A namespace alias maps one simple alias to one decoded namespace.</summary>
    NamespaceAlias = 3,

    /// <summary>A using-static import is retained exactly but is not a W7 static-root candidate source.</summary>
    UsingStatic = 4,

    /// <summary>An extern alias and AssemblyRef identity are retained exactly.</summary>
    ExternAlias = 5,

    /// <summary>An unimplemented raw import kind and payload are retained without interpretation.</summary>
    UnsupportedRaw = 255,
}

/// <summary>Freezes one exact physical Portable-PDB import definition and its decoded bounded projection.</summary>
public sealed class DumpPortablePdbImportFact : IEquatable<DumpPortablePdbImportFact>
{
    private readonly ImmutableArray<byte> rawPayload;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbImportFact(
        DumpPortablePdbImportKind kind,
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string? alias,
        string? target,
        int? assemblyReferenceToken,
        int? targetTypeToken,
        ImmutableArray<byte> rawPayload)
    {
        Kind = kind;
        ImportScopeToken = importScopeToken;
        Ordinal = ordinal;
        RawKind = rawKind;
        Alias = alias;
        Target = target;
        AssemblyReferenceToken = assemblyReferenceToken;
        TargetTypeToken = targetTypeToken;
        this.rawPayload = CanonicalReplayEncoding.Copy(rawPayload);
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-import-fact", 1);
        writer.WriteInt32((int)kind);
        writer.WriteInt32(importScopeToken);
        writer.WriteInt32(ordinal);
        writer.WriteInt32(rawKind);
        WriteNullableString(writer, alias);
        WriteNullableString(writer, target);
        writer.WriteBoolean(assemblyReferenceToken.HasValue);
        if (assemblyReferenceToken.HasValue)
        {
            writer.WriteInt32(assemblyReferenceToken.Value);
        }

        writer.WriteBoolean(targetTypeToken.HasValue);
        if (targetTypeToken.HasValue)
        {
            writer.WriteInt32(targetTypeToken.Value);
        }

        writer.WriteLengthPrefixedBytes(rawPayload.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the decoded supported category or explicit unsupported-raw category.</summary>
    public DumpPortablePdbImportKind Kind { get; }

    /// <summary>Gets the non-nil ImportScope-table token containing this definition.</summary>
    public int ImportScopeToken { get; }

    /// <summary>Gets the zero-based physical import ordinal within the decoded scope blob.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the exact raw Portable-PDB import-kind tag, including unknown future values.</summary>
    public byte RawKind { get; }

    /// <summary>Gets the decoded simple alias when the import kind has one; otherwise gets null.</summary>
    public string? Alias { get; }

    /// <summary>Gets the decoded namespace or type target when the import kind has one; otherwise gets null.</summary>
    public string? Target { get; }

    /// <summary>
    /// Gets the target AssemblyRef for assembly-qualified namespace/alias forms, or null when the raw form has none.
    /// </summary>
    public int? AssemblyReferenceToken { get; }

    /// <summary>
    /// Gets the exact TypeDef, TypeRef, or retained TypeSpec token for type-bearing imports; otherwise gets null.
    /// </summary>
    /// <remarks>A TypeSpec fact is retained exactly but remains unsupported by the W7 non-generic symbol binder.</remarks>
    public int? TargetTypeToken { get; }

    /// <summary>Gets a defensive copy of the exact raw import payload retained for replay and unsupported kinds.</summary>
    public ImmutableArray<byte> RawPayload => CanonicalReplayEncoding.Copy(rawPayload);

    /// <summary>Gets a defensive copy of the canonical physical/decoded import identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact namespace-import fact.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact raw import kind.</param>
    /// <param name="targetNamespace">The decoded imported namespace.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <param name="assemblyReferenceToken">
    /// Null for raw ImportNamespace (1), or the non-nil AssemblyRef for ImportAssemblyNamespace (2).
    /// </param>
    /// <returns>An immutable namespace-import fact.</returns>
    public static DumpPortablePdbImportFact NamespaceImport(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string targetNamespace,
        ImmutableArray<byte> rawPayload,
        int? assemblyReferenceToken = null) =>
        Create(
            DumpPortablePdbImportKind.Namespace,
            importScopeToken,
            ordinal,
            rawKind,
            null,
            targetNamespace,
            assemblyReferenceToken,
            targetTypeToken: null,
            rawPayload);

    /// <summary>Creates an exact type-alias import fact.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact raw import kind.</param>
    /// <param name="alias">The decoded simple type alias.</param>
    /// <param name="targetType">The decoded target type name.</param>
    /// <param name="targetTypeToken">The exact non-nil TypeDef, TypeRef, or retained TypeSpec token.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <returns>An immutable type-alias fact.</returns>
    public static DumpPortablePdbImportFact TypeAlias(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string alias,
        string targetType,
        int targetTypeToken,
        ImmutableArray<byte> rawPayload) =>
        Create(
            DumpPortablePdbImportKind.TypeAlias,
            importScopeToken,
            ordinal,
            rawKind,
            alias,
            targetType,
            assemblyReferenceToken: null,
            targetTypeToken,
            rawPayload);

    /// <summary>Creates an exact namespace-alias import fact.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact raw import kind.</param>
    /// <param name="alias">The decoded simple namespace alias.</param>
    /// <param name="targetNamespace">The decoded target namespace.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <param name="assemblyReferenceToken">
    /// Null for raw AliasNamespace (7), or the non-nil AssemblyRef for AliasAssemblyNamespace (8).
    /// </param>
    /// <returns>An immutable namespace-alias fact.</returns>
    public static DumpPortablePdbImportFact NamespaceAlias(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string alias,
        string targetNamespace,
        ImmutableArray<byte> rawPayload,
        int? assemblyReferenceToken = null) =>
        Create(
            DumpPortablePdbImportKind.NamespaceAlias,
            importScopeToken,
            ordinal,
            rawKind,
            alias,
            targetNamespace,
            assemblyReferenceToken,
            targetTypeToken: null,
            rawPayload);

    /// <summary>Creates an exact using-static import fact retained for context completeness.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact raw import kind.</param>
    /// <param name="targetType">The decoded static type name.</param>
    /// <param name="targetTypeToken">The exact non-nil TypeDef, TypeRef, or retained TypeSpec token.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <returns>An immutable using-static fact.</returns>
    public static DumpPortablePdbImportFact UsingStatic(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string targetType,
        int targetTypeToken,
        ImmutableArray<byte> rawPayload) =>
        Create(
            DumpPortablePdbImportKind.UsingStatic,
            importScopeToken,
            ordinal,
            rawKind,
            null,
            targetType,
            assemblyReferenceToken: null,
            targetTypeToken,
            rawPayload);

    /// <summary>Creates an exact extern-alias import fact.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact raw import kind.</param>
    /// <param name="alias">The decoded extern alias.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <param name="assemblyReferenceToken">
    /// Null for raw ImportAssemblyReferenceAlias (5), or the non-nil AssemblyRef for AliasAssemblyReference (6).
    /// </param>
    /// <returns>An immutable extern-alias fact.</returns>
    public static DumpPortablePdbImportFact ExternAlias(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string alias,
        ImmutableArray<byte> rawPayload,
        int? assemblyReferenceToken = null) =>
        Create(
            DumpPortablePdbImportKind.ExternAlias,
            importScopeToken,
            ordinal,
            rawKind,
            alias,
            target: null,
            assemblyReferenceToken,
            targetTypeToken: null,
            rawPayload);

    /// <summary>Retains an unsupported raw import kind and its exact payload without semantic interpretation.</summary>
    /// <param name="importScopeToken">The containing ImportScope token.</param>
    /// <param name="ordinal">The zero-based physical import ordinal.</param>
    /// <param name="rawKind">The exact unimplemented raw kind tag.</param>
    /// <param name="rawPayload">The explicitly initialized exact raw payload.</param>
    /// <returns>An immutable unsupported-raw fact that contributes no name candidate.</returns>
    public static DumpPortablePdbImportFact UnsupportedRaw(
        int importScopeToken,
        int ordinal,
        byte rawKind,
        ImmutableArray<byte> rawPayload) =>
        Create(
            DumpPortablePdbImportKind.UnsupportedRaw,
            importScopeToken,
            ordinal,
            rawKind,
            null,
            null,
            null,
            null,
            rawPayload);

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The import fact to compare.</param>
    /// <returns><see langword="true"/> when physical identity, decoded values, and raw payload are equal.</returns>
    public bool Equals(DumpPortablePdbImportFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbImportFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static DumpPortablePdbImportFact Create(
        DumpPortablePdbImportKind kind,
        int importScopeToken,
        int ordinal,
        byte rawKind,
        string? alias,
        string? target,
        int? assemblyReferenceToken,
        int? targetTypeToken,
        ImmutableArray<byte> rawPayload)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(importScopeToken, 0x35, nameof(importScopeToken));
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "An import ordinal cannot be negative.");
        }

        if (rawPayload.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized raw import payload is required.", nameof(rawPayload));
        }

        switch (kind)
        {
            case DumpPortablePdbImportKind.Namespace:
                DumpContextContractEncoding.ValidateRequiredText(target!, nameof(target));
                ValidateRawKindWithOptionalAssemblyReference(
                    rawKind,
                    unqualifiedKind: 1,
                    assemblyQualifiedKind: 2,
                    assemblyReferenceToken,
                    nameof(assemblyReferenceToken));
                break;
            case DumpPortablePdbImportKind.TypeAlias:
                DumpContextContractEncoding.ValidateAlias(alias!, nameof(alias));
                DumpContextContractEncoding.ValidateRequiredText(target!, nameof(target));
                ValidateRawKind(rawKind, expected: 9, kind);
                ValidateTypeToken(targetTypeToken, nameof(targetTypeToken));
                EnsureAbsent(assemblyReferenceToken, nameof(assemblyReferenceToken));
                break;
            case DumpPortablePdbImportKind.NamespaceAlias:
                DumpContextContractEncoding.ValidateAlias(alias!, nameof(alias));
                DumpContextContractEncoding.ValidateRequiredText(target!, nameof(target));
                ValidateRawKindWithOptionalAssemblyReference(
                    rawKind,
                    unqualifiedKind: 7,
                    assemblyQualifiedKind: 8,
                    assemblyReferenceToken,
                    nameof(assemblyReferenceToken));
                EnsureAbsent(targetTypeToken, nameof(targetTypeToken));
                break;
            case DumpPortablePdbImportKind.UsingStatic:
                DumpContextContractEncoding.ValidateRequiredText(target!, nameof(target));
                ValidateRawKind(rawKind, expected: 3, kind);
                ValidateTypeToken(targetTypeToken, nameof(targetTypeToken));
                EnsureAbsent(assemblyReferenceToken, nameof(assemblyReferenceToken));
                break;
            case DumpPortablePdbImportKind.ExternAlias:
                DumpContextContractEncoding.ValidateAlias(alias!, nameof(alias));
                ValidateRawKindWithOptionalAssemblyReference(
                    rawKind,
                    unqualifiedKind: 5,
                    assemblyQualifiedKind: 6,
                    assemblyReferenceToken,
                    nameof(assemblyReferenceToken));
                EnsureAbsent(targetTypeToken, nameof(targetTypeToken));
                break;
            case DumpPortablePdbImportKind.UnsupportedRaw:
                if (rawKind is 1 or 2 or 3 or 5 or 6 or 7 or 8 or 9)
                {
                    throw new ArgumentException(
                        "A supported Portable-PDB raw kind cannot be retained as UnsupportedRaw.",
                        nameof(rawKind));
                }

                EnsureAbsent(assemblyReferenceToken, nameof(assemblyReferenceToken));
                EnsureAbsent(targetTypeToken, nameof(targetTypeToken));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new DumpPortablePdbImportFact(
            kind,
            importScopeToken,
            ordinal,
            rawKind,
            alias,
            target,
            assemblyReferenceToken,
            targetTypeToken,
            CanonicalReplayEncoding.Copy(rawPayload));
    }

    private static void ValidateRawKind(
        byte rawKind,
        byte expected,
        DumpPortablePdbImportKind decodedKind)
    {
        if (rawKind != expected)
        {
            throw new ArgumentException(
                $"Raw import kind {rawKind} contradicts decoded {decodedKind} facts.",
                nameof(rawKind));
        }
    }

    private static void ValidateRawKindWithOptionalAssemblyReference(
        byte rawKind,
        byte unqualifiedKind,
        byte assemblyQualifiedKind,
        int? assemblyReferenceToken,
        string parameterName)
    {
        if (rawKind == unqualifiedKind)
        {
            EnsureAbsent(assemblyReferenceToken, parameterName);
            return;
        }

        if (rawKind != assemblyQualifiedKind)
        {
            throw new ArgumentException(
                $"Raw import kind {rawKind} contradicts the decoded import category.",
                nameof(rawKind));
        }

        CanonicalReplayEncoding.ValidateMetadataToken(
            assemblyReferenceToken ?? 0,
            0x23,
            parameterName);
    }

    private static void ValidateTypeToken(int? targetTypeToken, string parameterName)
    {
        if (targetTypeToken is not { } token ||
            (!CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x01) &&
             !CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x02) &&
             !CanonicalReplayEncoding.IsMetadataTokenForTable(token, 0x1B)))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A non-nil TypeDef, TypeRef, or retained TypeSpec token is required.");
        }
    }

    private static void EnsureAbsent(int? value, string parameterName)
    {
        if (value.HasValue)
        {
            throw new ArgumentException("This raw import form cannot carry that metadata token.", parameterName);
        }
    }

    private static void WriteNullableString(CanonicalReplayEncoding.Writer writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }
}

/// <summary>Identifies one exact active Portable-PDB LocalScope row and its IL range.</summary>
public sealed class DumpPortablePdbLocalScopeIdentity : IEquatable<DumpPortablePdbLocalScopeIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbLocalScopeIdentity(
        int localScopeToken,
        int methodDefinitionToken,
        int? importScopeToken,
        int startOffset,
        int length,
        int nestingDepth)
    {
        LocalScopeToken = localScopeToken;
        MethodDefinitionToken = methodDefinitionToken;
        ImportScopeToken = importScopeToken;
        StartOffset = startOffset;
        Length = length;
        NestingDepth = nestingDepth;
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-local-scope-identity", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil LocalScope-table token.</summary>
    public int LocalScopeToken { get; }

    /// <summary>Gets the non-nil MethodDef token associated with the LocalScope row.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the associated non-nil ImportScope token, or null for a nil handle.</summary>
    public int? ImportScopeToken { get; }

    /// <summary>Gets the non-negative inclusive scope start IL offset.</summary>
    public int StartOffset { get; }

    /// <summary>Gets the positive scope length in IL bytes.</summary>
    public int Length { get; }

    /// <summary>Gets the zero-based outer-to-inner depth in the exact active local-scope chain.</summary>
    public int NestingDepth { get; }

    /// <summary>Gets a defensive copy of the canonical scope-row identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact active LocalScope identity.</summary>
    /// <param name="localScopeToken">A non-nil LocalScope token.</param>
    /// <param name="methodDefinitionToken">The associated non-nil MethodDef token.</param>
    /// <param name="importScopeToken">The associated non-nil ImportScope token, or null for nil.</param>
    /// <param name="startOffset">The non-negative inclusive IL start offset.</param>
    /// <param name="length">The positive, nonoverflowing IL range length.</param>
    /// <param name="nestingDepth">The non-negative active-chain depth.</param>
    /// <returns>An immutable exact local-scope identity.</returns>
    public static DumpPortablePdbLocalScopeIdentity Create(
        int localScopeToken,
        int methodDefinitionToken,
        int? importScopeToken,
        int startOffset,
        int length,
        int nestingDepth)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(localScopeToken, 0x32, nameof(localScopeToken));
        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        if (importScopeToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(importScopeToken.Value, 0x35, nameof(importScopeToken));
        }

        if (startOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (length <= 0 || startOffset > int.MaxValue - length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A positive, nonoverflowing scope length is required.");
        }

        if (nestingDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nestingDepth));
        }

        return new DumpPortablePdbLocalScopeIdentity(
            localScopeToken,
            methodDefinitionToken,
            importScopeToken,
            startOffset,
            length,
            nestingDepth);
    }

    /// <summary>Determines whether an exact IL offset lies in this half-open scope range.</summary>
    /// <param name="ilOffset">A non-negative IL offset.</param>
    /// <returns><see langword="true"/> when the offset is at least the start and below start plus length.</returns>
    public bool Contains(int ilOffset) => ilOffset >= StartOffset && ilOffset < StartOffset + Length;

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The local-scope identity to compare.</param>
    /// <returns><see langword="true"/> when row identity, range, import handle, and depth are equal.</returns>
    public bool Equals(DumpPortablePdbLocalScopeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbLocalScopeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(LocalScopeToken);
        writer.WriteInt32(MethodDefinitionToken);
        writer.WriteBoolean(ImportScopeToken.HasValue);
        if (ImportScopeToken.HasValue)
        {
            writer.WriteInt32(ImportScopeToken.Value);
        }

        writer.WriteInt32(StartOffset);
        writer.WriteInt32(Length);
        writer.WriteInt32(NestingDepth);
    }
}

/// <summary>Freezes one exact active ImportScope row and its ordered physical import definitions.</summary>
public sealed class DumpPortablePdbImportScopeIdentity : IEquatable<DumpPortablePdbImportScopeIdentity>
{
    private readonly ImmutableArray<DumpPortablePdbImportFact> imports;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbImportScopeIdentity(
        int importScopeToken,
        int? parentImportScopeToken,
        int nestingDepth,
        ImmutableArray<DumpPortablePdbImportFact> imports)
    {
        ImportScopeToken = importScopeToken;
        ParentImportScopeToken = parentImportScopeToken;
        NestingDepth = nestingDepth;
        this.imports = CanonicalReplayEncoding.Copy(imports);
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-import-scope-identity", 1);
        WriteCanonical(writer, includeImports: true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil ImportScope-table token.</summary>
    public int ImportScopeToken { get; }

    /// <summary>Gets the non-nil parent ImportScope token, or null for the root scope.</summary>
    public int? ParentImportScopeToken { get; }

    /// <summary>Gets the zero-based outer-to-inner depth in the exact active import chain.</summary>
    public int NestingDepth { get; }

    /// <summary>Gets a defensive copy of imports ordered by physical ordinal after duplicate rejection.</summary>
    public ImmutableArray<DumpPortablePdbImportFact> Imports => CanonicalReplayEncoding.Copy(imports);

    /// <summary>Gets a defensive copy of the complete canonical scope/import identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact active ImportScope identity.</summary>
    /// <param name="importScopeToken">A non-nil ImportScope token.</param>
    /// <param name="parentImportScopeToken">The non-nil parent token, or null for the root.</param>
    /// <param name="nestingDepth">The non-negative outer-to-inner chain depth.</param>
    /// <param name="imports">
    /// An initialized array of exact imports. Caller order is ignored; any repeated physical ordinal is rejected as
    /// a producer defect.
    /// </param>
    /// <returns>An immutable, deterministically ordered import-scope identity.</returns>
    public static DumpPortablePdbImportScopeIdentity Create(
        int importScopeToken,
        int? parentImportScopeToken,
        int nestingDepth,
        ImmutableArray<DumpPortablePdbImportFact> imports)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(importScopeToken, 0x35, nameof(importScopeToken));
        if (parentImportScopeToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(
                parentImportScopeToken.Value,
                0x35,
                nameof(parentImportScopeToken));
            if (parentImportScopeToken.Value == importScopeToken)
            {
                throw new ArgumentException("An ImportScope cannot be its own parent.", nameof(parentImportScopeToken));
            }
        }

        if (nestingDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nestingDepth));
        }

        if (imports.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized import array is required.", nameof(imports));
        }

        var ordered = imports
            .Select(static item => item ?? throw new ArgumentException("Import arrays cannot contain null entries.", nameof(imports)))
            .OrderBy(static item => item.Ordinal)
            .ThenBy(static item => item.Sha256, StringComparer.Ordinal)
            .ToArray();
        var normalized = new List<DumpPortablePdbImportFact>(ordered.Length);
        foreach (var import in ordered)
        {
            if (import.ImportScopeToken != importScopeToken)
            {
                throw new ArgumentException("Every import must identify its containing ImportScope.", nameof(imports));
            }

            if (normalized.Count == 0 || normalized[^1].Ordinal != import.Ordinal)
            {
                normalized.Add(import);
                continue;
            }

            throw new ArgumentException(
                $"Import ordinal {import.Ordinal} occurs more than once.",
                nameof(imports));
        }

        return new DumpPortablePdbImportScopeIdentity(
            importScopeToken,
            parentImportScopeToken,
            nestingDepth,
            ImmutableArray.CreateRange(normalized));
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The import-scope identity to compare.</param>
    /// <returns><see langword="true"/> when row, parent, depth, and ordered physical imports are equal.</returns>
    public bool Equals(DumpPortablePdbImportScopeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbImportScopeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer, bool includeImports)
    {
        writer.WriteInt32(ImportScopeToken);
        writer.WriteBoolean(ParentImportScopeToken.HasValue);
        if (ParentImportScopeToken.HasValue)
        {
            writer.WriteInt32(ParentImportScopeToken.Value);
        }

        writer.WriteInt32(NestingDepth);
        if (!includeImports)
        {
            return;
        }

        writer.WriteInt32(imports.Length);
        foreach (var import in imports)
        {
            writer.WriteLengthPrefixedBytes(import.CanonicalBytes.AsSpan());
        }
    }
}

/// <summary>
/// Freezes one exact selected-frame Portable-PDB method, document, active scopes, and import chain.
/// </summary>
/// <remarks>
/// The artifact is independently content-identified and must match the selected module's GUID and stamp. The exact
/// import set may contain competing aliases: that is exact context evidence and becomes symbol ambiguity only if the
/// binder consults those facts and more than one declaration survives.
/// </remarks>
public sealed class DumpPortablePdbContextFacts : IEquatable<DumpPortablePdbContextFacts>
{
    private readonly ImmutableArray<DumpPortablePdbLocalScopeIdentity> localScopes;
    private readonly ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbContextFacts(
        DumpSelectedFrameIdentity selectedFrame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        DumpPortablePdbArtifactIdentity artifact,
        int methodDebugInformationToken,
        DumpPortablePdbDocumentIdentity? document,
        ImmutableArray<DumpPortablePdbLocalScopeIdentity> localScopes,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes)
    {
        SelectedFrame = selectedFrame;
        ModuleDebugIdentity = moduleDebugIdentity;
        Artifact = artifact;
        MethodDebugInformationToken = methodDebugInformationToken;
        Document = document;
        this.localScopes = CanonicalReplayEncoding.Copy(localScopes);
        this.importScopes = CanonicalReplayEncoding.Copy(importScopes);
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-context-facts", 1);
        WriteCanonical(writer, includeImports: true);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact selected frame whose method and IL offset selected these scopes.</summary>
    public DumpSelectedFrameIdentity SelectedFrame { get; }

    /// <summary>Gets the exact module CodeView identity used to locate the artifact.</summary>
    public DumpModulePortablePdbDebugIdentity ModuleDebugIdentity { get; }

    /// <summary>Gets the exact hashed artifact and decoded content identifier.</summary>
    public DumpPortablePdbArtifactIdentity Artifact { get; }

    /// <summary>Gets the non-nil MethodDebugInformation token whose RID equals the selected MethodDef RID.</summary>
    public int MethodDebugInformationToken { get; }

    /// <summary>Gets the exact document row used by method debug information, or null for a nil document handle.</summary>
    public DumpPortablePdbDocumentIdentity? Document { get; }

    /// <summary>Gets a defensive copy of exact containing LocalScope rows in outer-to-inner depth order.</summary>
    public ImmutableArray<DumpPortablePdbLocalScopeIdentity> LocalScopes =>
        CanonicalReplayEncoding.Copy(localScopes);

    /// <summary>Gets a defensive copy of the exact active ImportScope chain in outer-to-inner depth order.</summary>
    public ImmutableArray<DumpPortablePdbImportScopeIdentity> ImportScopes =>
        CanonicalReplayEncoding.Copy(importScopes);

    /// <summary>Gets all exact import facts in deterministic scope-depth and physical-ordinal order.</summary>
    public ImmutableArray<DumpPortablePdbImportFact> Imports =>
        ImmutableArray.CreateRange(importScopes.SelectMany(static scope => scope.Imports));

    /// <summary>Gets a defensive copy of the complete canonical PDB context fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Acquires a validated immutable exact Portable-PDB context fact set.</summary>
    /// <param name="selectedFrame">The exact selected frame that chose the method and IL offset.</param>
    /// <param name="moduleDebugIdentity">The exact CodeView identity for the same module.</param>
    /// <param name="artifact">The complete Portable PDB artifact whose GUID and stamp must match the module.</param>
    /// <param name="methodDebugInformationToken">A non-nil MethodDebugInformation token with the MethodDef RID.</param>
    /// <param name="document">The optional exact document identity, excluding path and name.</param>
    /// <param name="localScopes">
    /// An initialized exact active-scope array. Order is normalized; conflicting depth/token identities and all
    /// repeated physical rows are rejected.
    /// </param>
    /// <param name="importScopes">
    /// An initialized exact active import-chain array. Order is normalized and the parent chain is validated.
    /// </param>
    /// <returns>An exact context fact set suitable for additive binding context acquisition.</returns>
    public static DumpPortablePdbContextFacts Acquire(
        DumpSelectedFrameIdentity selectedFrame,
        DumpModulePortablePdbDebugIdentity moduleDebugIdentity,
        DumpPortablePdbArtifactIdentity artifact,
        int methodDebugInformationToken,
        DumpPortablePdbDocumentIdentity? document,
        ImmutableArray<DumpPortablePdbLocalScopeIdentity> localScopes,
        ImmutableArray<DumpPortablePdbImportScopeIdentity> importScopes)
    {
        ArgumentNullException.ThrowIfNull(selectedFrame);
        ArgumentNullException.ThrowIfNull(moduleDebugIdentity);
        ArgumentNullException.ThrowIfNull(artifact);
        if (!selectedFrame.RuntimeModule.Equals(moduleDebugIdentity.RuntimeModule) ||
            !selectedFrame.ModuleContent.Equals(moduleDebugIdentity.ModuleContent))
        {
            throw new ArgumentException(
                "Selected-frame and module debug identity must identify the same runtime module and metadata content.",
                nameof(moduleDebugIdentity));
        }

        if (!moduleDebugIdentity.DebugIdentity.Equals(artifact.DebugIdentity))
        {
            throw new ArgumentException(
                "An exact Portable-PDB context cannot contain a module/artifact identity mismatch.",
                nameof(artifact));
        }

        CanonicalReplayEncoding.ValidateMetadataToken(
            methodDebugInformationToken,
            0x31,
            nameof(methodDebugInformationToken));
        if (CanonicalReplayEncoding.MetadataTokenRowId(methodDebugInformationToken) !=
            CanonicalReplayEncoding.MetadataTokenRowId(selectedFrame.MethodDefinitionToken))
        {
            throw new ArgumentException(
                "MethodDebugInformation and selected MethodDef row identifiers must agree.",
                nameof(methodDebugInformationToken));
        }

        var normalizedLocals = NormalizeLocalScopes(localScopes, selectedFrame);
        var normalizedImports = NormalizeImportScopes(importScopes);
        var importTokens = normalizedImports.Select(static scope => scope.ImportScopeToken).ToHashSet();
        if (normalizedLocals.Any(scope => scope.ImportScopeToken is { } token && !importTokens.Contains(token)))
        {
            throw new ArgumentException(
                "Every non-nil LocalScope import handle must occur in the exact active import chain.",
                nameof(importScopes));
        }

        return new DumpPortablePdbContextFacts(
            selectedFrame,
            moduleDebugIdentity,
            artifact,
            methodDebugInformationToken,
            document,
            normalizedLocals,
            normalizedImports);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The exact PDB context facts to compare.</param>
    /// <returns><see langword="true"/> when artifact, method, document, scopes, and imports are equal.</returns>
    public bool Equals(DumpPortablePdbContextFacts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbContextFacts);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer, bool includeImports)
    {
        writer.WriteLengthPrefixedBytes(SelectedFrame.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(ModuleDebugIdentity.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(Artifact.CanonicalBytes.AsSpan());
        writer.WriteInt32(MethodDebugInformationToken);
        writer.WriteBoolean(Document is not null);
        if (Document is not null)
        {
            writer.WriteLengthPrefixedBytes(Document.CanonicalBytes.AsSpan());
        }

        writer.WriteInt32(localScopes.Length);
        foreach (var scope in localScopes)
        {
            scope.WriteCanonical(writer);
        }

        writer.WriteInt32(importScopes.Length);
        foreach (var scope in importScopes)
        {
            scope.WriteCanonical(writer, includeImports);
        }
    }

    private static ImmutableArray<DumpPortablePdbLocalScopeIdentity> NormalizeLocalScopes(
        ImmutableArray<DumpPortablePdbLocalScopeIdentity> scopes,
        DumpSelectedFrameIdentity selectedFrame)
    {
        if (scopes.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized local-scope array is required.", nameof(scopes));
        }

        var ordered = scopes
            .Select(static scope => scope ?? throw new ArgumentException("Local scopes cannot contain null.", nameof(scopes)))
            .OrderBy(static scope => scope.NestingDepth)
            .ThenBy(static scope => scope.LocalScopeToken)
            .ThenBy(static scope => scope.Sha256, StringComparer.Ordinal)
            .ToArray();
        var result = new List<DumpPortablePdbLocalScopeIdentity>(ordered.Length);
        foreach (var scope in ordered)
        {
            if (scope.MethodDefinitionToken != selectedFrame.MethodDefinitionToken ||
                !scope.Contains(selectedFrame.Instruction.IlOffset))
            {
                throw new ArgumentException(
                    "Every active LocalScope must identify the selected MethodDef and contain its exact IL offset.",
                    nameof(scopes));
            }

            var priorSameToken = result.LastOrDefault(candidate => candidate.LocalScopeToken == scope.LocalScopeToken);
            if (priorSameToken is not null)
            {
                throw new ArgumentException("A LocalScope token occurs more than once.", nameof(scopes));
            }

            if (result.Any(candidate => candidate.NestingDepth == scope.NestingDepth))
            {
                throw new ArgumentException("Two different active LocalScopes cannot claim the same depth.", nameof(scopes));
            }

            result.Add(scope);
        }

        for (var index = 0; index < result.Count; index++)
        {
            if (result[index].NestingDepth != index)
            {
                throw new ArgumentException("Active LocalScope depths must form a contiguous zero-based chain.", nameof(scopes));
            }
        }

        return ImmutableArray.CreateRange(result);
    }

    private static ImmutableArray<DumpPortablePdbImportScopeIdentity> NormalizeImportScopes(
        ImmutableArray<DumpPortablePdbImportScopeIdentity> scopes)
    {
        if (scopes.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized import-scope array is required.", nameof(scopes));
        }

        var ordered = scopes
            .Select(static scope => scope ?? throw new ArgumentException("Import scopes cannot contain null.", nameof(scopes)))
            .OrderBy(static scope => scope.NestingDepth)
            .ThenBy(static scope => scope.ImportScopeToken)
            .ThenBy(static scope => scope.Sha256, StringComparer.Ordinal)
            .ToArray();
        var result = new List<DumpPortablePdbImportScopeIdentity>(ordered.Length);
        foreach (var scope in ordered)
        {
            var priorSameToken = result.LastOrDefault(candidate => candidate.ImportScopeToken == scope.ImportScopeToken);
            if (priorSameToken is not null)
            {
                throw new ArgumentException("An ImportScope token occurs more than once.", nameof(scopes));
            }

            if (result.Any(candidate => candidate.NestingDepth == scope.NestingDepth))
            {
                throw new ArgumentException("Two different active ImportScopes cannot claim the same depth.", nameof(scopes));
            }

            result.Add(scope);
        }

        for (var index = 0; index < result.Count; index++)
        {
            var scope = result[index];
            int? expectedParent = index == 0 ? null : result[index - 1].ImportScopeToken;
            if (scope.NestingDepth != index || scope.ParentImportScopeToken != expectedParent)
            {
                throw new ArgumentException(
                    "Active ImportScopes must form one contiguous zero-based parent chain.",
                    nameof(scopes));
            }
        }

        return ImmutableArray.CreateRange(result);
    }
}

/// <summary>Represents the complete disposition of bounded module-debug, artifact, scope, and import acquisition.</summary>
/// <remarks>
/// Exact is the sole status carrying <see cref="Facts"/>. Every non-exact status retains the reached source and bounds
/// but exposes no exact import expansion. A decoded GUID/stamp mismatch is legal only as a conflict observation.
/// </remarks>
public sealed class DumpPortablePdbObservation : IEquatable<DumpPortablePdbObservation>
{
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpPortablePdbObservation(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpPortablePdbEvidenceSource source,
        DumpPortablePdbContextFacts? facts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        DumpContextContractEncoding.ValidateStatusIssue(status, issue);
        DumpContextContractEncoding.ValidatePortablePdbStatusIssue(status, issue);
        if ((status == DumpContextEvidenceStatus.Exact) != (facts is not null))
        {
            throw new ArgumentException("Exactly the Exact status must carry Portable-PDB context facts.", nameof(facts));
        }

        if (source.HasIdentityMismatch !=
            (status == DumpContextEvidenceStatus.Conflict &&
             issue == DumpContextEvidenceIssue.PortablePdbIdentityMismatch))
        {
            throw new ArgumentException(
                "A Portable-PDB GUID/stamp mismatch must be represented only and exactly as identity conflict.",
                nameof(source));
        }

        if (facts is not null)
        {
            if (source.ModuleDebugIdentity is null || source.CandidateContent is null || source.ObservedDebugIdentity is null ||
                !source.ModuleDebugIdentity.Equals(facts.ModuleDebugIdentity) ||
                !source.CandidateContent.Equals(facts.Artifact.Content) ||
                !source.ObservedDebugIdentity.Equals(facts.Artifact.DebugIdentity))
            {
                throw new ArgumentException(
                    "Exact PDB facts must reproduce every module and artifact source identity.",
                    nameof(facts));
            }
        }

        Status = status;
        Issue = issue;
        Source = source;
        Facts = facts;
        this.reachedBounds = CanonicalReplayEncoding.Copy(reachedBounds);
        var writer = new CanonicalReplayEncoding.Writer("dump-portable-pdb-observation", 1);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(source.CanonicalBytes.AsSpan());
        DumpContextContractEncoding.WriteBounds(writer, reachedBounds);
        writer.WriteBoolean(facts is not null);
        if (facts is not null)
        {
            writer.WriteLengthPrefixedBytes(facts.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the typed evidence precision and disposition.</summary>
    public DumpContextEvidenceStatus Status { get; }

    /// <summary>Gets the stable first-boundary issue; exact evidence always uses <c>None</c>.</summary>
    public DumpContextEvidenceIssue Issue { get; }

    /// <summary>Gets the immutable source boundary reached by the PDB producer.</summary>
    public DumpPortablePdbEvidenceSource Source { get; }

    /// <summary>Gets exact artifact/scope/import facts only for exact status; otherwise gets null.</summary>
    public DumpPortablePdbContextFacts? Facts { get; }

    /// <summary>Gets whether this observation carries complete exact Portable-PDB facts.</summary>
    public bool HasExactFacts => Facts is not null;

    /// <summary>Gets a defensive copy of deterministic bounds reached by this source path.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        CanonicalReplayEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the canonical observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact PDB observation carrying complete artifact, scope, and import facts.</summary>
    /// <param name="facts">The complete exact PDB context facts.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An exact observation with a source reconstructed from the exact facts.</returns>
    public static DumpPortablePdbObservation Exact(
        DumpPortablePdbContextFacts facts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var source = DumpPortablePdbEvidenceSource.ForCandidate(
            facts.ModuleDebugIdentity,
            facts.Artifact.Content,
            facts.Artifact.DebugIdentity);
        return Create(DumpContextEvidenceStatus.Exact, DumpContextEvidenceIssue.None, source, facts, reachedBounds);
    }

    /// <summary>Creates a partial PDB observation carrying no exact scope or import expansion.</summary>
    /// <param name="source">The immutable source boundary reached before acquisition became incomplete.</param>
    /// <param name="issue">A PDB-meaningful partial issue such as a reached bound or incomplete source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A partial observation that cannot expose exact import candidates.</returns>
    public static DumpPortablePdbObservation Partial(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Partial, issue, source, null, reachedBounds);

    /// <summary>Creates an unavailable PDB observation carrying no exact scope or import expansion.</summary>
    /// <param name="source">The immutable source boundary reached before the missing source.</param>
    /// <param name="issue">A PDB-meaningful unavailable issue naming the first missing source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unavailable observation that cannot expose exact import candidates.</returns>
    public static DumpPortablePdbObservation Unavailable(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unavailable, issue, source, null, reachedBounds);

    /// <summary>Creates an ambiguous PDB observation without selecting the first candidate artifact or scope.</summary>
    /// <param name="source">The immutable common source boundary reached by the competing candidates.</param>
    /// <param name="issue">A PDB artifact or scope ambiguity issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An ambiguous observation that cannot expose any candidate as exact.</returns>
    public static DumpPortablePdbObservation Ambiguous(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Ambiguous, issue, source, null, reachedBounds);

    /// <summary>Creates a PDB conflict observation, including the sole legal representation of identity mismatch.</summary>
    /// <param name="source">The source retaining both expected and observed identities when they disagree.</param>
    /// <param name="issue">A PDB-meaningful conflict issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A conflict observation carrying no exact artifact, scope, or import expansion.</returns>
    public static DumpPortablePdbObservation Conflict(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Conflict, issue, source, null, reachedBounds);

    /// <summary>Creates an invalid PDB observation without retaining malformed exact facts.</summary>
    /// <param name="source">The immutable source boundary reached before structural validation failed.</param>
    /// <param name="issue">A PDB-meaningful invalid-data issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An invalid observation carrying no malformed exact payload.</returns>
    public static DumpPortablePdbObservation Invalid(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Invalid, issue, source, null, reachedBounds);

    /// <summary>Creates an unsupported PDB observation without manufacturing supported import facts.</summary>
    /// <param name="source">The immutable source boundary containing the unsupported representation.</param>
    /// <param name="issue">A PDB-meaningful unsupported-representation issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unsupported observation carrying no supported candidate expansion.</returns>
    public static DumpPortablePdbObservation Unsupported(
        DumpPortablePdbEvidenceSource source,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unsupported, issue, source, null, reachedBounds);

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The PDB observation to compare.</param>
    /// <returns><see langword="true"/> when status, source, bounds, and optional exact facts are equal.</returns>
    public bool Equals(DumpPortablePdbObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpPortablePdbObservation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static DumpPortablePdbObservation Create(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpPortablePdbEvidenceSource source,
        DumpPortablePdbContextFacts? facts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        return new DumpPortablePdbObservation(status, issue, source, facts, normalizedBounds);
    }
}
