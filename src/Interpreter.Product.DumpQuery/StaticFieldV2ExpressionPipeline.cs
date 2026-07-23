using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Names the single route one composed static-field draft evaluation selected and never revisited.</summary>
/// <remarks>
/// The route is decided exactly once, at draft step five of the frozen operation order, from the detached syntax
/// descriptor and the presence of a caller-supplied scoped-context seam. No later draft step may widen, retry, or
/// replace it.
/// </remarks>
public enum StaticFieldV2ExpressionRoute
{
    /// <summary>No route was selected because the draft evaluation stopped at syntax.</summary>
    NotSelected = 1,

    /// <summary>A <c>global::</c> or metadata-global dot-qualified spelling that consults no context at all.</summary>
    ExplicitMetadataGlobal = 2,

    /// <summary>A named-alias or ordinary contextual spelling resolved through the scoped draft context.</summary>
    Contextual = 3,

    /// <summary>A bare single-identifier root resolved through lexical completeness and <c>using static</c>.</summary>
    BareStaticMember = 4,
}

/// <summary>Names one caller-supplied evidence seam this composed draft pipeline can call.</summary>
/// <remarks>
/// Every seam call is metered so a fully qualified draft answer can prove the absence of frame and PDB calls with
/// zero counters rather than by omitting successful values.
/// </remarks>
public enum StaticFieldV2PipelineEvidenceKind
{
    /// <summary>The caller-supplied scoped-context projection request seam.</summary>
    ScopedContextProjection = 1,

    /// <summary>The caller-supplied selected-method lexical envelope seam.</summary>
    LexicalEnvelope = 2,

    /// <summary>The caller-supplied metadata Constant-row seam consulted for a literal declaration.</summary>
    MetadataConstantRow = 3,

    /// <summary>The caller-supplied runtime construction candidate seam.</summary>
    RuntimeConstructionCandidates = 4,

    /// <summary>The caller-supplied static-slot geometry seam.</summary>
    RuntimeSlotFacts = 5,

    /// <summary>The caller-supplied raw dump memory read seam.</summary>
    RawMemoryRead = 6,

    /// <summary>The caller-supplied unchanged W2/W6 detached-suffix evaluation seam.</summary>
    SuffixChainEvaluation = 7,

    /// <summary>The caller-supplied frame-root evaluation seam owned by the frame-value profile.</summary>
    FrameRootEvaluation = 8,
}

/// <summary>Identifies one declared coverage boundary retained by every composed draft evaluation.</summary>
/// <remarks>
/// Each boundary is an informational draft fact naming what this composition deliberately does not own, so no
/// consumer can mistake a deferred stage for a proven negative.
/// </remarks>
public enum StaticFieldV2PipelineCoverageBoundary
{
    /// <summary>The scoped-context and lexical envelope evidence is supplied by the caller through one seam.</summary>
    ScopedContextEvidenceSuppliedByCallerSeam = 1,

    /// <summary>The runtime construction, slot geometry, and raw bytes are supplied by the caller through one seam.</summary>
    RuntimeEvidenceSuppliedByCallerSeam = 2,

    /// <summary>The metadata Constant row of a literal declaration is supplied by the caller through one seam.</summary>
    MetadataConstantRowSuppliedByCallerSeam = 3,

    /// <summary>The declared field type is derived only from a ground primitive FieldSig in this draft.</summary>
    DeclaredFieldTypeLimitedToGroundPrimitiveSignature = 4,

    /// <summary>A contextual or bare route binds an owner definition rather than a closed draft construction.</summary>
    ContextualRouteClosedConstructionDeferred = 5,

    /// <summary>The unchanged W2/W6 suffix evaluator is rooted at the resolved reference through one caller seam.</summary>
    SuffixEvaluationSuppliedByCallerSeam = 6,

    /// <summary>Reference-target validation runs only when the caller supplies the exact expected target type.</summary>
    ReferenceTargetValidationRequiresSuppliedTarget = 7,

    /// <summary>The frame-value profile is owned by a separate entry point and never falls through to here.</summary>
    FrameValueProfileOwnedBySeparateEntryPoint = 8,

    /// <summary>The frame-root memory home and value are supplied by the caller through one seam.</summary>
    FrameRootEvidenceSuppliedByCallerSeam = 9,
}

/// <summary>Freezes how many times each caller-supplied evidence seam was called by one draft evaluation.</summary>
/// <remarks>
/// The ledger is the positive proof a fully qualified draft route demands: an explicit route retains zero context
/// counters, and a metadata-literal draft answer retains zero runtime counters.
/// </remarks>
public sealed class StaticFieldV2PipelineEvidenceLedger : IEquatable<StaticFieldV2PipelineEvidenceLedger>
{
    private const string CanonicalDomain = "static-field-v2-pipeline-evidence-ledger";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2PipelineEvidenceLedger(
        int scopedContextProjection,
        int lexicalEnvelope,
        int metadataConstantRow,
        int runtimeConstructionCandidates,
        int runtimeSlotFacts,
        int rawMemoryRead,
        int suffixChainEvaluation,
        int frameRootEvaluation)
    {
        ScopedContextProjectionCallCount = scopedContextProjection;
        LexicalEnvelopeCallCount = lexicalEnvelope;
        MetadataConstantRowCallCount = metadataConstantRow;
        RuntimeConstructionCandidatesCallCount = runtimeConstructionCandidates;
        RuntimeSlotFactsCallCount = runtimeSlotFacts;
        RawMemoryReadCallCount = rawMemoryRead;
        SuffixChainEvaluationCallCount = suffixChainEvaluation;
        FrameRootEvaluationCallCount = frameRootEvaluation;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(scopedContextProjection);
        writer.WriteInt32(lexicalEnvelope);
        writer.WriteInt32(metadataConstantRow);
        writer.WriteInt32(runtimeConstructionCandidates);
        writer.WriteInt32(runtimeSlotFacts);
        writer.WriteInt32(rawMemoryRead);
        writer.WriteInt32(suffixChainEvaluation);

        // The frame-root counter is appended only when the frame-value profile actually consulted its seam, so every
        // static-field ledger keeps its exact version-2 byte content and its frozen digest unchanged.
        if (frameRootEvaluation != 0)
        {
            writer.WriteInt32(frameRootEvaluation);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets how many times the scoped-context projection draft seam was called.</summary>
    public int ScopedContextProjectionCallCount { get; }

    /// <summary>Gets how many times the lexical envelope draft seam was called.</summary>
    public int LexicalEnvelopeCallCount { get; }

    /// <summary>Gets how many times the metadata Constant-row draft seam was called.</summary>
    public int MetadataConstantRowCallCount { get; }

    /// <summary>Gets how many times the runtime construction candidate draft seam was called.</summary>
    public int RuntimeConstructionCandidatesCallCount { get; }

    /// <summary>Gets how many times the static-slot geometry draft seam was called.</summary>
    public int RuntimeSlotFactsCallCount { get; }

    /// <summary>Gets how many times the raw dump memory read draft seam was called.</summary>
    public int RawMemoryReadCallCount { get; }

    /// <summary>Gets how many times the unchanged W2/W6 detached-suffix evaluation draft seam was called.</summary>
    public int SuffixChainEvaluationCallCount { get; }

    /// <summary>Gets how many times the caller-supplied frame-root evaluation draft seam was called.</summary>
    public int FrameRootEvaluationCallCount { get; }

    /// <summary>Gets the total frame, PDB, and import context draft calls this evaluation performed.</summary>
    public int ContextCallCount => ScopedContextProjectionCallCount + LexicalEnvelopeCallCount;

    /// <summary>Gets the total runtime, slot, and memory draft calls this evaluation performed.</summary>
    public int RuntimeCallCount =>
        RuntimeConstructionCandidatesCallCount + RuntimeSlotFactsCallCount + RawMemoryReadCallCount;

    /// <summary>Gets the total draft seam calls this evaluation performed.</summary>
    public int TotalCallCount =>
        ContextCallCount + MetadataConstantRowCallCount + RuntimeCallCount + SuffixChainEvaluationCallCount +
        FrameRootEvaluationCallCount;

    /// <summary>Gets whether this draft evaluation called no caller-supplied seam at all.</summary>
    public bool IsZero => TotalCallCount == 0;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft ledger bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft ledger.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the draft call count of one named evidence seam.</summary>
    /// <param name="kind">The evidence seam whose draft call count is requested.</param>
    /// <returns>The counted draft calls of that seam.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public int CallCount(StaticFieldV2PipelineEvidenceKind kind) => kind switch
    {
        StaticFieldV2PipelineEvidenceKind.ScopedContextProjection => ScopedContextProjectionCallCount,
        StaticFieldV2PipelineEvidenceKind.LexicalEnvelope => LexicalEnvelopeCallCount,
        StaticFieldV2PipelineEvidenceKind.MetadataConstantRow => MetadataConstantRowCallCount,
        StaticFieldV2PipelineEvidenceKind.RuntimeConstructionCandidates => RuntimeConstructionCandidatesCallCount,
        StaticFieldV2PipelineEvidenceKind.RuntimeSlotFacts => RuntimeSlotFactsCallCount,
        StaticFieldV2PipelineEvidenceKind.RawMemoryRead => RawMemoryReadCallCount,
        StaticFieldV2PipelineEvidenceKind.SuffixChainEvaluation => SuffixChainEvaluationCallCount,
        StaticFieldV2PipelineEvidenceKind.FrameRootEvaluation => FrameRootEvaluationCallCount,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Tests canonical equality between two draft evidence ledgers.</summary>
    /// <param name="other">The other draft ledger.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2PipelineEvidenceLedger? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests draft evidence ledger equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a ledger with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2PipelineEvidenceLedger);

    /// <summary>Computes a deterministic hash code from immutable canonical draft ledger content.</summary>
    /// <returns>A hash code for this canonical draft ledger.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2PipelineEvidenceLedger Issue(
        int scopedContextProjection,
        int lexicalEnvelope,
        int metadataConstantRow,
        int runtimeConstructionCandidates,
        int runtimeSlotFacts,
        int rawMemoryRead,
        int suffixChainEvaluation,
        int frameRootEvaluation) =>
        new(
            scopedContextProjection,
            lexicalEnvelope,
            metadataConstantRow,
            runtimeConstructionCandidates,
            runtimeSlotFacts,
            rawMemoryRead,
            suffixChainEvaluation,
            frameRootEvaluation);
}

/// <summary>Freezes one caller-supplied scoped-context draft seam that this composition never acquires itself.</summary>
/// <remarks>
/// The seam is a pair of caller-owned delegates rather than eager data, so an explicit draft route can prove it never
/// touched frame or PDB evidence by holding a poisoned seam that throws whenever it is invoked.
/// </remarks>
public sealed class StaticFieldV2ScopedContextSource
{
    private const string CanonicalDomain = "static-field-v2-pipeline-scoped-context-source";
    private readonly Func<StaticFieldV2ScopedContextRequest> scopedContextRequest;
    private readonly Func<DumpSelectedMethodLexicalObservation>? lexicalEnvelope;

    private StaticFieldV2ScopedContextSource(
        Func<StaticFieldV2ScopedContextRequest> scopedContextRequest,
        Func<DumpSelectedMethodLexicalObservation>? lexicalEnvelope)
    {
        this.scopedContextRequest = scopedContextRequest;
        this.lexicalEnvelope = lexicalEnvelope;
    }

    /// <summary>Gets whether this draft seam can supply a selected-method lexical envelope.</summary>
    public bool SuppliesLexicalEnvelope => lexicalEnvelope is not null;

    /// <summary>Creates one caller-owned scoped-context draft seam.</summary>
    /// <param name="scopedContextRequest">Produces the complete scoped-context projection draft request.</param>
    /// <param name="lexicalEnvelope">Produces the selected-method lexical draft envelope, or null when absent.</param>
    /// <returns>A sealed draft seam whose calls this composition meters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scopedContextRequest"/> is null.</exception>
    public static StaticFieldV2ScopedContextSource Create(
        Func<StaticFieldV2ScopedContextRequest> scopedContextRequest,
        Func<DumpSelectedMethodLexicalObservation>? lexicalEnvelope = null)
    {
        ArgumentNullException.ThrowIfNull(scopedContextRequest);
        return new StaticFieldV2ScopedContextSource(scopedContextRequest, lexicalEnvelope);
    }

    internal StaticFieldV2ScopedContextRequest AcquireScopedContextRequest() => scopedContextRequest();

    internal DumpSelectedMethodLexicalObservation? AcquireLexicalEnvelope() => lexicalEnvelope?.Invoke();
}

/// <summary>Freezes one caller-supplied static-slot geometry draft fact set for an address-backed strategy.</summary>
/// <remarks>
/// The facts carry exactly what the frozen per-strategy slot requirements consume. Supplying a fact a strategy is
/// forbidden to carry remains a typed draft slot stop rather than a silently ignored value.
/// </remarks>
public sealed class StaticFieldV2RuntimeSlotFacts : IEquatable<StaticFieldV2RuntimeSlotFacts>
{
    private const string CanonicalDomain = "static-field-v2-pipeline-runtime-slot-facts";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeSlotFacts(
        int readWidth,
        ulong? slotAddress,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ModuleContentIdentity? moduleContent,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress)
    {
        ReadWidth = readWidth;
        SlotAddress = slotAddress;
        SelectedThread = selectedThread;
        ModuleContent = moduleContent;
        FieldRvaRowToken = fieldRvaRowToken;
        MappedRelativeVirtualAddress = mappedRelativeVirtualAddress;
        MappedAddress = mappedAddress;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(readWidth);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, slotAddress);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedThread?.Sha256);
        writer.WriteBoolean(moduleContent is not null);
        if (moduleContent is not null)
        {
            writer.WriteString(moduleContent.Mvid.ToString("N"));
            writer.WriteInt32(moduleContent.MetadataLength);
            writer.WriteSha256(moduleContent.MetadataSha256, nameof(moduleContent));
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, fieldRvaRowToken);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(
            writer,
            mappedRelativeVirtualAddress.HasValue ? mappedRelativeVirtualAddress.Value : null);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, mappedAddress);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the positive counted read width in bytes of the draft slot's value.</summary>
    public int ReadWidth { get; }

    /// <summary>Gets the supplied exact static slot address, or null when none was supplied.</summary>
    public ulong? SlotAddress { get; }

    /// <summary>Gets the supplied exact selected-thread draft identity, or null when none was supplied.</summary>
    public StaticFieldV2SelectedThreadIdentity? SelectedThread { get; }

    /// <summary>Gets the supplied exact module content identity, or null when none was supplied.</summary>
    public ModuleContentIdentity? ModuleContent { get; }

    /// <summary>Gets the supplied exact FieldRVA row token, or null when none was supplied.</summary>
    public int? FieldRvaRowToken { get; }

    /// <summary>Gets the supplied exact mapped relative virtual address, or null when none was supplied.</summary>
    public uint? MappedRelativeVirtualAddress { get; }

    /// <summary>Gets the supplied exact mapped image address, or null when none was supplied.</summary>
    public ulong? MappedAddress { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft facts.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied static-slot geometry draft fact set.</summary>
    /// <param name="readWidth">The positive counted read width in bytes of the slot's value.</param>
    /// <param name="slotAddress">The supplied exact static slot address, or null.</param>
    /// <param name="selectedThread">The supplied exact selected-thread identity, or null.</param>
    /// <param name="moduleContent">The supplied exact module content identity, or null.</param>
    /// <param name="fieldRvaRowToken">The supplied exact FieldRVA row token, or null.</param>
    /// <param name="mappedRelativeVirtualAddress">The supplied exact mapped relative virtual address, or null.</param>
    /// <param name="mappedAddress">The supplied exact mapped image address, or null.</param>
    /// <returns>A sealed immutable draft fact set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The read width is outside the admitted draft range.</exception>
    public static StaticFieldV2RuntimeSlotFacts Create(
        int readWidth,
        ulong? slotAddress = null,
        StaticFieldV2SelectedThreadIdentity? selectedThread = null,
        ModuleContentIdentity? moduleContent = null,
        int? fieldRvaRowToken = null,
        uint? mappedRelativeVirtualAddress = null,
        ulong? mappedAddress = null)
    {
        if (readWidth is <= 0 or > StaticFieldV2StaticSlotRequest.MaximumReadWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readWidth),
                $"A counted read width of one through {StaticFieldV2StaticSlotRequest.MaximumReadWidth} is required.");
        }

        return new StaticFieldV2RuntimeSlotFacts(
            readWidth,
            slotAddress,
            selectedThread,
            moduleContent,
            fieldRvaRowToken,
            mappedRelativeVirtualAddress,
            mappedAddress);
    }

    /// <summary>Tests canonical equality between two static-slot geometry draft fact sets.</summary>
    /// <param name="other">The other draft fact set.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeSlotFacts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot geometry draft fact equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for facts with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeSlotFacts);

    /// <summary>Computes a deterministic hash code from immutable canonical draft fact content.</summary>
    /// <returns>A hash code for this canonical draft fact set.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one caller-supplied metadata Constant-row draft fact for a literal declaration.</summary>
/// <remarks>
/// Metadata remains the sole literal draft value source. The physical Constant table is not modeled by the landed
/// catalogs, so the exact row is admitted here as a caller-supplied metadata fact and never as a runtime read.
/// </remarks>
public sealed class StaticFieldV2LiteralConstantFact : IEquatable<StaticFieldV2LiteralConstantFact>
{
    private const string CanonicalDomain = "static-field-v2-pipeline-literal-constant-fact";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> constantValueBlob;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LiteralConstantFact(int constantTypeCode, ImmutableArray<byte> constantValueBlob)
    {
        ConstantTypeCode = constantTypeCode;
        this.constantValueBlob = constantValueBlob;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(constantTypeCode);
        writer.WriteLengthPrefixedBytes(constantValueBlob.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact ECMA Constant type code of the retained draft row.</summary>
    public int ConstantTypeCode { get; }

    /// <summary>Gets a defensive copy of the exact Constant value blob of the retained draft row.</summary>
    public ImmutableArray<byte> ConstantValueBlob => ExpressionV2ContractEncoding.Copy(constantValueBlob);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft fact.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied metadata Constant-row draft fact.</summary>
    /// <param name="constantTypeCode">The exact ECMA Constant type code of the physical row.</param>
    /// <param name="constantValueBlob">The exact initialized Constant value blob of the physical row.</param>
    /// <returns>A sealed immutable draft fact with a defensively copied blob.</returns>
    /// <exception cref="ArgumentException">The supplied blob is not initialized.</exception>
    public static StaticFieldV2LiteralConstantFact Create(int constantTypeCode, ImmutableArray<byte> constantValueBlob)
    {
        if (constantValueBlob.IsDefault)
        {
            throw new ArgumentException("An initialized Constant value blob is required.", nameof(constantValueBlob));
        }

        return new StaticFieldV2LiteralConstantFact(
            constantTypeCode,
            ExpressionV2ContractEncoding.Copy(constantValueBlob));
    }

    /// <summary>Tests canonical equality between two metadata Constant-row draft facts.</summary>
    /// <param name="other">The other draft fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2LiteralConstantFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests metadata Constant-row draft fact equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LiteralConstantFact);

    /// <summary>Computes a deterministic hash code from immutable canonical draft fact content.</summary>
    /// <returns>A hash code for this canonical draft fact.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one caller-supplied runtime-evidence draft seam this composition never acquires itself.</summary>
/// <remarks>
/// The seam keeps every constructed-runtime, slot geometry, and raw byte fact behind caller-owned delegates, so this
/// composition needs no dump host dependency and a metadata-literal draft answer can prove it called none of them.
/// </remarks>
public sealed class StaticFieldV2RuntimeEvidenceSource
{
    private const string CanonicalDomain = "static-field-v2-pipeline-runtime-evidence-source";

    private readonly Func<
        StaticFieldV2ClosedConstructionOutcome,
        StaticFieldV2StorageStrategyOutcome,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>>? constructionCandidates;

    private readonly Func<
        StaticFieldV2StorageStrategyOutcome,
        StaticFieldV2RuntimeConstructionSelection?,
        StaticFieldV2RuntimeSlotFacts?>? slotFacts;

    private readonly Func<ulong, int, ImmutableArray<byte>>? rawMemoryRead;

    private StaticFieldV2RuntimeEvidenceSource(
        Func<
            StaticFieldV2ClosedConstructionOutcome,
            StaticFieldV2StorageStrategyOutcome,
            ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>>? constructionCandidates,
        Func<
            StaticFieldV2StorageStrategyOutcome,
            StaticFieldV2RuntimeConstructionSelection?,
            StaticFieldV2RuntimeSlotFacts?>? slotFacts,
        Func<ulong, int, ImmutableArray<byte>>? rawMemoryRead)
    {
        this.constructionCandidates = constructionCandidates;
        this.slotFacts = slotFacts;
        this.rawMemoryRead = rawMemoryRead;
    }

    /// <summary>Creates one caller-owned runtime-evidence draft seam.</summary>
    /// <param name="constructionCandidates">Produces every bounded same-TypeDef runtime construction candidate.</param>
    /// <param name="slotFacts">Produces the exact static-slot geometry of the classified draft strategy.</param>
    /// <param name="rawMemoryRead">Copies the counted raw bytes at one exact address out of the dump.</param>
    /// <returns>A sealed draft seam whose calls this composition meters.</returns>
    public static StaticFieldV2RuntimeEvidenceSource Create(
        Func<
            StaticFieldV2ClosedConstructionOutcome,
            StaticFieldV2StorageStrategyOutcome,
            ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>>? constructionCandidates = null,
        Func<
            StaticFieldV2StorageStrategyOutcome,
            StaticFieldV2RuntimeConstructionSelection?,
            StaticFieldV2RuntimeSlotFacts?>? slotFacts = null,
        Func<ulong, int, ImmutableArray<byte>>? rawMemoryRead = null) =>
        new(constructionCandidates, slotFacts, rawMemoryRead);

    internal bool SuppliesConstructionCandidates => constructionCandidates is not null;

    internal bool SuppliesSlotFacts => slotFacts is not null;

    internal bool SuppliesRawMemoryRead => rawMemoryRead is not null;

    internal ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> AcquireConstructionCandidates(
        StaticFieldV2ClosedConstructionOutcome construction,
        StaticFieldV2StorageStrategyOutcome strategy) =>
        constructionCandidates!(construction, strategy);

    internal StaticFieldV2RuntimeSlotFacts? AcquireSlotFacts(
        StaticFieldV2StorageStrategyOutcome strategy,
        StaticFieldV2RuntimeConstructionSelection? selection) =>
        slotFacts!(strategy, selection);

    internal ImmutableArray<byte> AcquireRawBytes(ulong address, int width) => rawMemoryRead!(address, width);
}

/// <summary>Freezes one detached suffix request rooted at an exact resolved reference for the caller seam.</summary>
/// <remarks>
/// The request names only the exact non-null managed reference address the composition resolved and the unchanged
/// W2/W6 suffix descriptor the parser froze. A caller-owned seam roots the unchanged member-chain evaluator at that
/// reference; this draft request carries no ClrMD object so the composition acquires no dump host dependency itself.
/// </remarks>
public sealed class StaticFieldV2SuffixEvaluationRequest
{
    private const string CanonicalDomain = "static-field-v2-pipeline-suffix-evaluation-request";

    internal StaticFieldV2SuffixEvaluationRequest(ulong referenceAddress, DumpExpressionSuffixDescriptor suffix)
    {
        ReferenceAddress = referenceAddress;
        Suffix = suffix;
    }

    /// <summary>Gets the exact non-null managed reference address the composition resolved for the suffix root.</summary>
    public ulong ReferenceAddress { get; }

    /// <summary>Gets the unchanged frozen W2/W6 detached-suffix descriptor to evaluate at the reference.</summary>
    public DumpExpressionSuffixDescriptor Suffix { get; }
}

/// <summary>Freezes one caller-supplied unchanged W2/W6 suffix-evaluation draft seam this composition never owns.</summary>
/// <remarks>
/// The seam keeps the unchanged member-chain evaluator behind one caller-owned delegate, mirroring the runtime-evidence
/// seam. Fast callers supply a synthetic delegate; the real dump host roots <see cref="DumpMemberChainEngine"/> at the
/// resolved reference and returns its <see cref="EvaluationResult{DumpQueryValue}"/>. The composition meters every call
/// so a suffix-free or exact-null-short-circuited draft answer can prove it invoked the seam zero times.
/// </remarks>
public sealed class StaticFieldV2SuffixEvaluationSource
{
    private const string CanonicalDomain = "static-field-v2-pipeline-suffix-evaluation-source";

    private readonly Func<StaticFieldV2SuffixEvaluationRequest, EvaluationResult<DumpQueryValue>> evaluate;

    private StaticFieldV2SuffixEvaluationSource(
        Func<StaticFieldV2SuffixEvaluationRequest, EvaluationResult<DumpQueryValue>> evaluate) =>
        this.evaluate = evaluate;

    /// <summary>Creates one caller-owned unchanged W2/W6 suffix-evaluation draft seam.</summary>
    /// <param name="evaluate">Roots the unchanged member chain at the resolved reference and evaluates it.</param>
    /// <returns>A sealed draft seam whose calls this composition meters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="evaluate"/> is null.</exception>
    public static StaticFieldV2SuffixEvaluationSource Create(
        Func<StaticFieldV2SuffixEvaluationRequest, EvaluationResult<DumpQueryValue>> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new StaticFieldV2SuffixEvaluationSource(evaluate);
    }

    internal EvaluationResult<DumpQueryValue> Evaluate(StaticFieldV2SuffixEvaluationRequest request) =>
        evaluate(request);
}

/// <summary>Classifies one pipeline-level frame-root evaluation draft answer returned by the caller-owned seam.</summary>
/// <remarks>
/// Only <see cref="Exact"/> exposes an attributed memory home and a decoded value. Every other disposition is a
/// prefix-free typed draft stop. The two frozen W8.1 non-admissions, a register home and a selected frame's own generic
/// arguments, keep their own dispositions and surface their frozen diagnostic codes rather than an absent gap.
/// </remarks>
public enum StaticFieldV2FrameRootDisposition
{
    /// <summary>One exact frame root was attributed to an exact memory home with a decoded value.</summary>
    Exact = 1,

    /// <summary>The selected thread or frame context could not be acquired exactly.</summary>
    ContextUnavailable = 2,

    /// <summary>Two or more selected-thread candidates satisfied the token-keyed frame predicate.</summary>
    ContextAmbiguous = 3,

    /// <summary>The frame context was exact but the root reports no exact single memory home.</summary>
    RootUnavailable = 4,

    /// <summary>The frame context was exact but two or more live roots share the requested spelling.</summary>
    RootAmbiguous = 5,

    /// <summary>The frame context was exact but a higher-precedence declaration shadows the requested root.</summary>
    RootShadowed = 6,

    /// <summary>The frame context was exact but the acquired root evidence is malformed.</summary>
    RootInvalid = 7,

    /// <summary>The root homes in a register, a frozen W8.1 non-admission that is never read.</summary>
    RegisterHomeNotAdmitted = 8,

    /// <summary>The root is a selected frame's own generic argument, a frozen W8.1 non-admission.</summary>
    GenericArgumentNotAdmitted = 9,
}

/// <summary>Freezes one pipeline-level frame-root draft request projected from the detached frame descriptor.</summary>
/// <remarks>
/// The draft request names only the projected root the composition attributed: the <see langword="this"/>-or-identifier
/// root kind and, for an identifier root, its decoded value. It carries no physical thread, frame ordinal, or
/// Portable-PDB row; the caller-owned seam supplies that frame evidence itself, mirroring how the suffix seam owns the
/// member chain.
/// </remarks>
public sealed class StaticFieldV2FrameRootEvaluationRequest
{
    private const string CanonicalDomain = "static-field-v2-pipeline-frame-root-evaluation-request";

    internal StaticFieldV2FrameRootEvaluationRequest(
        FrameValueV1RootKind rootKind,
        DumpExpressionIdentifier? identifier)
    {
        RootKind = rootKind;
        Identifier = identifier;
    }

    /// <summary>Gets whether the projected root is <see langword="this"/> or one decoded identifier.</summary>
    public FrameValueV1RootKind RootKind { get; }

    /// <summary>Gets the decoded identifier root, or <see langword="null"/> for a <see langword="this"/> root.</summary>
    public DumpExpressionIdentifier? Identifier { get; }
}

/// <summary>Freezes one pipeline-level frame-root evaluation draft result the caller-owned seam returns.</summary>
/// <remarks>
/// An exact draft result exposes the attributed memory home (its address, counted width, and copied bytes), the
/// resolved root family, and one decoded value from the bounded dump-query domain. A non-null managed reference
/// additionally exposes the reference address so the composition can root the unchanged W2/W6 suffix at it. Every
/// non-exact disposition is a prefix-free typed draft stop that exposes no home, no value, and no reference, and a
/// non-admission retains its frozen code.
/// </remarks>
public sealed class StaticFieldV2FrameRootEvaluationResult : IEquatable<StaticFieldV2FrameRootEvaluationResult>
{
    private const string CanonicalDomain = "static-field-v2-pipeline-frame-root-evaluation-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> rootBytes;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2FrameRootEvaluationResult(
        StaticFieldV2FrameRootDisposition disposition,
        StaticFieldV2FrameValueRootKind rootKind,
        ulong? memoryHomeAddress,
        int? readWidth,
        ImmutableArray<byte> rootBytes,
        DumpQueryValue? value,
        ulong? referenceAddress,
        string? diagnosticCode)
    {
        Disposition = disposition;
        RootKind = rootKind;
        MemoryHomeAddress = memoryHomeAddress;
        ReadWidth = readWidth;
        this.rootBytes = rootBytes;
        Value = value;
        ReferenceAddress = referenceAddress;
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)disposition);
        writer.WriteInt32((int)rootKind);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, memoryHomeAddress);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, readWidth);
        writer.WriteBoolean(!rootBytes.IsDefault);
        if (!rootBytes.IsDefault)
        {
            writer.WriteLengthPrefixedBytes(rootBytes.AsSpan());
        }
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value.ToCanonicalReplayProjection());
        }
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, referenceAddress);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the typed frame-root evaluation disposition.</summary>
    public StaticFieldV2FrameRootDisposition Disposition { get; }

    /// <summary>Gets the resolved frame-root family the seam attributed for an exact answer.</summary>
    public StaticFieldV2FrameValueRootKind RootKind { get; }

    /// <summary>Gets the exact attributed memory-home address, or <see langword="null"/> on a stop.</summary>
    public ulong? MemoryHomeAddress { get; }

    /// <summary>Gets the exact counted read width in bytes of the home, or <see langword="null"/> on a stop.</summary>
    public int? ReadWidth { get; }

    /// <summary>Gets a defensive copy of the bytes copied from the attributed home, otherwise empty.</summary>
    public ImmutableArray<byte> RootBytes => ExpressionV2ContractEncoding.Copy(rootBytes);

    /// <summary>Gets the decoded root value for an exact answer, or <see langword="null"/> on a stop.</summary>
    public DumpQueryValue? Value { get; }

    /// <summary>Gets the non-null managed reference address for suffix rooting, otherwise <see langword="null"/>.</summary>
    public ulong? ReferenceAddress { get; }

    /// <summary>Gets the frozen diagnostic code retained by a typed non-admission, otherwise <see langword="null"/>.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical result bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical result.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact frame-root evaluation draft result carrying an attributed memory home and value.</summary>
    /// <param name="rootKind">The resolved <see langword="this"/>, parameter, or local root family.</param>
    /// <param name="memoryHomeAddress">The exact attributed memory-home address.</param>
    /// <param name="readWidth">The exact counted read width in bytes.</param>
    /// <param name="rootBytes">The exact bytes copied out of the pinned snapshot at the home.</param>
    /// <param name="value">The decoded root value from the bounded dump-query domain.</param>
    /// <param name="referenceAddress">The non-null managed reference address for suffix rooting, or null.</param>
    /// <returns>A sealed immutable exact result with a defensively copied byte payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">The root family is a non-admitted generic argument.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The read width is outside the admitted range.</exception>
    public static StaticFieldV2FrameRootEvaluationResult Exact(
        StaticFieldV2FrameValueRootKind rootKind,
        ulong memoryHomeAddress,
        int readWidth,
        ImmutableArray<byte> rootBytes,
        DumpQueryValue value,
        ulong? referenceAddress = null)
    {
        ExpressionV2ContractEncoding.RequireDefined(rootKind, nameof(rootKind));
        ArgumentNullException.ThrowIfNull(value);
        if (rootBytes.IsDefault)
        {
            throw new ArgumentException("An initialized copied-byte payload is required.", nameof(rootBytes));
        }
        if (readWidth is <= 0 or > FrameValueV1Limits.MaximumValueByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readWidth),
                $"A counted read width of one through {FrameValueV1Limits.MaximumValueByteCount} is required.");
        }
        if (rootKind is StaticFieldV2FrameValueRootKind.DeclaringTypeGenericArgument or
            StaticFieldV2FrameValueRootKind.MethodGenericArgument)
        {
            throw new ArgumentException("A generic-argument root can never be exact.", nameof(rootKind));
        }

        return new StaticFieldV2FrameRootEvaluationResult(
            StaticFieldV2FrameRootDisposition.Exact,
            rootKind,
            memoryHomeAddress,
            readWidth,
            ExpressionV2ContractEncoding.Copy(rootBytes),
            value,
            referenceAddress,
            null);
    }

    /// <summary>Creates one typed non-exact frame-root evaluation draft stop with no attributed home or value.</summary>
    /// <param name="disposition">One non-exact disposition.</param>
    /// <param name="diagnosticCode">The frozen non-admission code, required exactly for a frozen non-admission.</param>
    /// <returns>A sealed immutable prefix-free typed stop.</returns>
    /// <exception cref="ArgumentException">The disposition and diagnostic code disagree.</exception>
    public static StaticFieldV2FrameRootEvaluationResult Stop(
        StaticFieldV2FrameRootDisposition disposition,
        string? diagnosticCode = null)
    {
        ExpressionV2ContractEncoding.RequireDefined(disposition, nameof(disposition));
        var nonAdmission = disposition is StaticFieldV2FrameRootDisposition.RegisterHomeNotAdmitted or
            StaticFieldV2FrameRootDisposition.GenericArgumentNotAdmitted;
        if (disposition == StaticFieldV2FrameRootDisposition.Exact)
        {
            throw new ArgumentException("An exact result must be created through the exact factory.", nameof(disposition));
        }
        if (nonAdmission == string.IsNullOrEmpty(diagnosticCode))
        {
            throw new ArgumentException(
                "A frozen non-admission requires its diagnostic code and every other stop forbids one.",
                nameof(diagnosticCode));
        }
        if (diagnosticCode is not null)
        {
            ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        }

        return new StaticFieldV2FrameRootEvaluationResult(
            disposition,
            StaticFieldV2FrameValueRootKind.This,
            null,
            null,
            ImmutableArray<byte>.Empty,
            null,
            null,
            diagnosticCode);
    }

    /// <summary>Tests canonical equality between two frame-root evaluation results.</summary>
    /// <param name="other">The other result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2FrameRootEvaluationResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests frame-root evaluation result equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a result with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FrameRootEvaluationResult);

    /// <summary>Computes a deterministic hash code from immutable canonical result content.</summary>
    /// <returns>A hash code for this canonical result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one caller-supplied frame-root evaluation draft seam this composition never owns.</summary>
/// <remarks>
/// The seam keeps the frame-value acquisition behind one caller-owned delegate, mirroring the runtime-evidence and
/// suffix seams. Fast callers supply a synthetic delegate; the real dump host roots the seam at the pinned runtime's
/// <c>AcquireFrameValueRoot</c> and decodes the copied bytes into one bounded dump-query value. The composition meters
/// every call so a frame answer proves it consulted the frame evidence exactly once and no static binder at all.
/// </remarks>
public sealed class StaticFieldV2FrameRootEvaluationSource
{
    private const string CanonicalDomain = "static-field-v2-pipeline-frame-root-evaluation-source";

    private readonly Func<StaticFieldV2FrameRootEvaluationRequest, StaticFieldV2FrameRootEvaluationResult> evaluate;

    private StaticFieldV2FrameRootEvaluationSource(
        Func<StaticFieldV2FrameRootEvaluationRequest, StaticFieldV2FrameRootEvaluationResult> evaluate) =>
        this.evaluate = evaluate;

    /// <summary>Creates one caller-owned frame-root evaluation draft seam.</summary>
    /// <param name="evaluate">Attributes the projected frame root and decodes its exact memory-homed value.</param>
    /// <returns>A sealed draft seam whose calls this composition meters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="evaluate"/> is null.</exception>
    public static StaticFieldV2FrameRootEvaluationSource Create(
        Func<StaticFieldV2FrameRootEvaluationRequest, StaticFieldV2FrameRootEvaluationResult> evaluate)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        return new StaticFieldV2FrameRootEvaluationSource(evaluate);
    }

    internal StaticFieldV2FrameRootEvaluationResult Evaluate(StaticFieldV2FrameRootEvaluationRequest request) =>
        evaluate(request);
}

/// <summary>Freezes one complete composed static-field draft evaluation request.</summary>
/// <remarks>
/// The request names the raw expression, the explicitly selected profile, every metadata authority portfolio, the two
/// optional caller-owned evidence seams, and the capability probe set whose counters become the retained draft proof.
/// A caller must select the profile explicitly; nothing in the request lets one profile fall through to another.
/// </remarks>
public sealed class StaticFieldV2ExpressionRequest : IEquatable<StaticFieldV2ExpressionRequest>
{
    private const string CanonicalDomain = "static-field-v2-expression-pipeline-request";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs;
    private readonly ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ExpressionRequest(
        string? expressionText,
        DumpExpressionProfileKind profile,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants,
        StaticFieldV2ScopedContextSource? scopedContext,
        StaticFieldV2RuntimeEvidenceSource? runtimeEvidence,
        StaticFieldV2SuffixEvaluationSource? suffixEvaluation,
        Func<MetadataFieldDefinitionTableRowIdentity, StaticFieldV2LiteralConstantFact?>? literalConstantSource,
        MetadataClosedTypeIdentity? referenceTargetType,
        ExpressionV2CapabilityProbeSet? capabilityProbes,
        StaticFieldV2FrameRootEvaluationSource? frameRootEvaluation)
    {
        ExpressionText = expressionText;
        Profile = profile;
        AncestryPortfolio = ancestryPortfolio;
        ConstraintPortfolio = constraintPortfolio;
        InterfaceImplementationPortfolio = interfaceImplementationPortfolio;
        this.fieldCatalogs = fieldCatalogs;
        AccessibilityMode = accessibilityMode;
        RequestingAssembly = requestingAssembly;
        this.friendAssemblyGrants = friendAssemblyGrants;
        ScopedContext = scopedContext;
        RuntimeEvidence = runtimeEvidence;
        SuffixEvaluation = suffixEvaluation;
        LiteralConstantSource = literalConstantSource;
        ReferenceTargetType = referenceTargetType;
        CapabilityProbes = capabilityProbes;
        FrameRootEvaluation = frameRootEvaluation;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, expressionText);
        writer.WriteInt32((int)profile);
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteSha256(constraintPortfolio.Sha256, nameof(constraintPortfolio));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, interfaceImplementationPortfolio?.Sha256);
        writer.WriteInt32(fieldCatalogs.IsDefault ? -1 : fieldCatalogs.Length);
        if (!fieldCatalogs.IsDefault)
        {
            foreach (var catalog in fieldCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        writer.WriteInt32((int)accessibilityMode);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, requestingAssembly?.Sha256);
        writer.WriteInt32(friendAssemblyGrants.Length);
        foreach (var grant in friendAssemblyGrants)
        {
            writer.WriteSha256(grant.Sha256, nameof(friendAssemblyGrants));
        }
        writer.WriteBoolean(scopedContext is not null);
        writer.WriteBoolean(runtimeEvidence is not null);
        writer.WriteBoolean(suffixEvaluation is not null);
        writer.WriteBoolean(literalConstantSource is not null);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, referenceTargetType?.Sha256);
        writer.WriteBoolean(capabilityProbes is not null);

        // The frame-root seam presence is appended only when the frame-value profile actually supplies it, so every
        // static-field request keeps its exact version-2 byte content and its frozen digest unchanged.
        if (frameRootEvaluation is not null)
        {
            writer.WriteBoolean(true);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact raw expression text this draft evaluation parses exactly once.</summary>
    public string? ExpressionText { get; }

    /// <summary>Gets the profile the caller selected explicitly for this draft evaluation.</summary>
    public DumpExpressionProfileKind Profile { get; }

    /// <summary>Gets the ancestry authority draft portfolio supplying every metadata authority.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets the constraint-target resolution draft portfolio consumed by owner construction.</summary>
    public MetadataConstraintTargetResolutionPortfolioIdentity ConstraintPortfolio { get; }

    /// <summary>Gets the optional interface-implementation draft portfolio, or null when none was supplied.</summary>
    public MetadataInterfaceImplementationPortfolioIdentity? InterfaceImplementationPortfolio { get; }

    /// <summary>Gets a defensive copy of the supplied per-module FieldDef catalog draft vector.</summary>
    public ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs =>
        fieldCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(fieldCatalogs);

    /// <summary>Gets the caller-declared accessibility certificate governing every member admission.</summary>
    public StaticFieldV2AccessibilityMode AccessibilityMode { get; }

    /// <summary>Gets the requesting assembly for the use-site certificate, or null for qualified inspection.</summary>
    public StaticFieldContainingAssemblyIdentity? RequestingAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the caller-supplied friend-assembly draft grants.</summary>
    public ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrants =>
        ExpressionV2ContractEncoding.Copy(friendAssemblyGrants);

    /// <summary>Gets the optional caller-owned scoped-context draft seam, or null when none was supplied.</summary>
    public StaticFieldV2ScopedContextSource? ScopedContext { get; }

    /// <summary>Gets the optional caller-owned runtime-evidence draft seam, or null when none was supplied.</summary>
    public StaticFieldV2RuntimeEvidenceSource? RuntimeEvidence { get; }

    /// <summary>Gets the optional caller-owned unchanged W2/W6 suffix-evaluation draft seam, or null when absent.</summary>
    public StaticFieldV2SuffixEvaluationSource? SuffixEvaluation { get; }

    /// <summary>Gets the optional caller-owned metadata Constant-row draft seam, or null when none was supplied.</summary>
    public Func<MetadataFieldDefinitionTableRowIdentity, StaticFieldV2LiteralConstantFact?>? LiteralConstantSource
    {
        get;
    }

    /// <summary>Gets whether this draft request supplies a reference-target type for assignability validation.</summary>
    public bool SuppliesReferenceTargetType => ReferenceTargetType is not null;

    internal MetadataClosedTypeIdentity? ReferenceTargetType { get; }

    /// <summary>Gets the caller-owned capability probes every acquisition of this draft evaluation routes through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets the optional caller-owned frame-root evaluation draft seam, or null when none was supplied.</summary>
    /// <remarks>
    /// The frame-value entry point engages its composed binder only when this seam is present. A frame-profiled request
    /// that supplies no frame-root seam is declined as an unsupported profile exactly as before, so no frame request can
    /// fall through to a static binder and the separate-entry-point isolation stays frozen.
    /// </remarks>
    public StaticFieldV2FrameRootEvaluationSource? FrameRootEvaluation { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete composed static-field draft evaluation request.</summary>
    /// <param name="expressionText">The exact raw expression text, parsed exactly once.</param>
    /// <param name="profile">The explicitly selected product language profile.</param>
    /// <param name="ancestryPortfolio">The ancestry authority draft portfolio prerequisite.</param>
    /// <param name="constraintPortfolio">The constraint-target resolution draft portfolio prerequisite.</param>
    /// <param name="fieldCatalogs">One FieldDef catalog per ancestry-portfolio module, in any order.</param>
    /// <param name="interfaceImplementationPortfolio">The optional interface-implementation draft portfolio.</param>
    /// <param name="accessibilityMode">The caller-declared accessibility certificate.</param>
    /// <param name="requestingAssembly">The requesting assembly, required exactly for the use-site certificate.</param>
    /// <param name="friendAssemblyGrants">The caller-supplied friend grants, admitted only for the use-site mode.</param>
    /// <param name="scopedContext">The optional caller-owned scoped-context draft seam.</param>
    /// <param name="runtimeEvidence">The optional caller-owned runtime-evidence draft seam.</param>
    /// <param name="suffixEvaluation">The optional caller-owned unchanged W2/W6 suffix-evaluation draft seam.</param>
    /// <param name="literalConstantSource">The optional caller-owned metadata Constant-row draft seam.</param>
    /// <param name="referenceTargetType">The optional exact target type of reference-target draft validation.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <param name="frameRootEvaluation">The optional caller-owned frame-root evaluation draft seam.</param>
    /// <returns>A sealed immutable draft request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument is undefined.</exception>
    /// <exception cref="ArgumentException">The accessibility mode and its companions disagree.</exception>
    public static StaticFieldV2ExpressionRequest Create(
        string? expressionText,
        DumpExpressionProfileKind profile,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        MetadataConstraintTargetResolutionPortfolioIdentity constraintPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        MetadataInterfaceImplementationPortfolioIdentity? interfaceImplementationPortfolio = null,
        StaticFieldV2AccessibilityMode accessibilityMode =
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
        StaticFieldContainingAssemblyIdentity? requestingAssembly = null,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants = default,
        StaticFieldV2ScopedContextSource? scopedContext = null,
        StaticFieldV2RuntimeEvidenceSource? runtimeEvidence = null,
        StaticFieldV2SuffixEvaluationSource? suffixEvaluation = null,
        Func<MetadataFieldDefinitionTableRowIdentity, StaticFieldV2LiteralConstantFact?>? literalConstantSource = null,
        MetadataClosedTypeIdentity? referenceTargetType = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null,
        StaticFieldV2FrameRootEvaluationSource? frameRootEvaluation = null)
    {
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        ArgumentNullException.ThrowIfNull(constraintPortfolio);
        ExpressionV2ContractEncoding.RequireDefined(profile, nameof(profile));
        ExpressionV2ContractEncoding.RequireDefined(accessibilityMode, nameof(accessibilityMode));

        var useSite = accessibilityMode == StaticFieldV2AccessibilityMode.UseSiteCertificate;
        if (useSite == (requestingAssembly is null))
        {
            throw new ArgumentException(
                "A requesting assembly is required exactly for the use-site accessibility certificate.",
                nameof(requestingAssembly));
        }

        var grants = ExpressionV2ContractEncoding.CopyRequired(
            friendAssemblyGrants.IsDefault
                ? ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity>.Empty
                : friendAssemblyGrants,
            nameof(friendAssemblyGrants),
            StaticFieldV2Limits.MaximumFriendAssemblyDeclarationCount);
        if (!useSite && !grants.IsEmpty)
        {
            throw new ArgumentException(
                "Qualified inspection bypasses accessibility and therefore admits no friend-assembly grant.",
                nameof(friendAssemblyGrants));
        }

        return new StaticFieldV2ExpressionRequest(
            expressionText,
            profile,
            ancestryPortfolio,
            constraintPortfolio,
            interfaceImplementationPortfolio,
            fieldCatalogs.IsDefault ? default : ImmutableArray.CreateRange(fieldCatalogs),
            accessibilityMode,
            requestingAssembly,
            grants,
            scopedContext,
            runtimeEvidence,
            suffixEvaluation,
            literalConstantSource,
            referenceTargetType,
            capabilityProbes,
            frameRootEvaluation);
    }

    /// <summary>Tests canonical equality between two composed draft evaluation requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ExpressionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests composed draft evaluation request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ExpressionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogsCore => fieldCatalogs;

    internal ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrantsCore =>
        friendAssemblyGrants;
}

/// <summary>Retains only the evidence one composed static-field draft evaluation actually consulted.</summary>
/// <remarks>
/// A stage that never ran contributes nothing at all, so an absent value is proof of an absent call rather than a
/// discarded success. The metered draft evidence ledger completes that proof by naming the exact seam call counts.
/// </remarks>
public sealed class StaticFieldV2ExpressionProvenance : IEquatable<StaticFieldV2ExpressionProvenance>
{
    private const string CanonicalDomain = "static-field-v2-expression-pipeline-provenance";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> rawValueBytes;
    private readonly ImmutableArray<StaticFieldV2PipelineCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal StaticFieldV2ExpressionProvenance(
        string? rawExpression,
        DumpExpressionProfileKind profile,
        StaticFieldV2SyntaxOutcome? syntax,
        StaticFieldV2ExpressionRoute route,
        StaticFieldV2ScopedContextOutcome? scopedContext,
        StaticFieldV2ContextualBindingOutcome? contextualBinding,
        StaticFieldV2TypeNameBindingOutcome? explicitNameBinding,
        StaticFieldV2ClosedConstructionOutcome? ownerConstruction,
        StaticFieldV2LexicalCertificateOutcome? lexicalCertificate,
        StaticFieldV2BareRootOutcome? bareRoot,
        StaticFieldV2MemberLookupOutcome? memberLookup,
        StaticFieldV2StorageStrategyOutcome? storageStrategy,
        StaticFieldV2RuntimeConstructionSelection? runtimeConstruction,
        StaticFieldV2StaticSlotOutcome? staticSlot,
        ImmutableArray<byte> rawValueBytes,
        StaticFieldV2LiteralConstantFact? literalConstant,
        StaticFieldV2LiteralValueOutcome? literalValue,
        StaticFieldV2RuntimeValueOutcome? runtimeValue,
        StaticFieldV2AssignabilityOutcome? referenceTargetValidation,
        DumpExpressionSuffixDescriptor? suffix,
        DumpQueryValue? suffixValue,
        StaticFieldV2PipelineEvidenceLedger evidenceLedger,
        StaticFieldV2CapabilityCallLedger? capabilityCallLedger,
        StaticFieldV2FrameRootEvaluationResult? frameRoot,
        ImmutableArray<StaticFieldV2PipelineCoverageBoundary> declaredCoverageBoundaries)
    {
        RawExpression = rawExpression;
        Profile = profile;
        Syntax = syntax;
        Route = route;
        ScopedContext = scopedContext;
        ContextualBinding = contextualBinding;
        ExplicitNameBinding = explicitNameBinding;
        OwnerConstruction = ownerConstruction;
        LexicalCertificate = lexicalCertificate;
        BareRoot = bareRoot;
        MemberLookup = memberLookup;
        StorageStrategy = storageStrategy;
        RuntimeConstruction = runtimeConstruction;
        StaticSlot = staticSlot;
        this.rawValueBytes = rawValueBytes;
        LiteralConstant = literalConstant;
        LiteralValue = literalValue;
        RuntimeValue = runtimeValue;
        ReferenceTargetValidation = referenceTargetValidation;
        Suffix = suffix;
        SuffixValue = suffixValue;
        EvidenceLedger = evidenceLedger;
        CapabilityCallLedger = capabilityCallLedger;
        FrameRoot = frameRoot;
        this.declaredCoverageBoundaries = declaredCoverageBoundaries;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, rawExpression);
        writer.WriteInt32((int)profile);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, syntax?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, syntax?.Descriptor?.Sha256);
        writer.WriteInt32((int)route);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, scopedContext?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, contextualBinding?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, explicitNameBinding?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, ownerConstruction?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, lexicalCertificate?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, bareRoot?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, memberLookup?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, storageStrategy?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, storageStrategy?.CapabilityRequirements.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, runtimeConstruction?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, staticSlot?.Sha256);
        writer.WriteBoolean(!rawValueBytes.IsDefault);
        if (!rawValueBytes.IsDefault)
        {
            writer.WriteLengthPrefixedBytes(rawValueBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, literalConstant?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, literalValue?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, runtimeValue?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, referenceTargetValidation?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, suffix?.Sha256);
        writer.WriteBoolean(suffixValue is not null);
        if (suffixValue is not null)
        {
            writer.WriteString(suffixValue.ToCanonicalReplayProjection());
        }
        writer.WriteSha256(evidenceLedger.Sha256, nameof(evidenceLedger));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, capabilityCallLedger?.Sha256);
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }

        // The retained frame memory home and value are appended only for the frame-value profile, so every static-field
        // provenance keeps its exact version-2 byte content and its frozen digest unchanged.
        if (frameRoot is not null)
        {
            writer.WriteSha256(frameRoot.Sha256, nameof(frameRoot));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact raw expression text this draft evaluation consumed.</summary>
    public string? RawExpression { get; }

    /// <summary>Gets the explicitly selected product profile of this draft evaluation.</summary>
    public DumpExpressionProfileKind Profile { get; }

    /// <summary>Gets the detached syntax draft outcome, or null when the profile stopped before any parse.</summary>
    public StaticFieldV2SyntaxOutcome? Syntax { get; }

    /// <summary>Gets the single route this draft evaluation selected and never revisited.</summary>
    public StaticFieldV2ExpressionRoute Route { get; }

    /// <summary>Gets the projected scoped-context draft outcome, or null when no context was consulted.</summary>
    public StaticFieldV2ScopedContextOutcome? ScopedContext { get; }

    /// <summary>Gets the contextual name-binding draft outcome, or null when that route was not taken.</summary>
    public StaticFieldV2ContextualBindingOutcome? ContextualBinding { get; }

    /// <summary>Gets the explicit name-binding draft outcome, or null when that route was not taken.</summary>
    public StaticFieldV2TypeNameBindingOutcome? ExplicitNameBinding { get; }

    /// <summary>Gets the closed owner-construction draft outcome, or null when none was bound.</summary>
    public StaticFieldV2ClosedConstructionOutcome? OwnerConstruction { get; }

    /// <summary>Gets the lexical-completeness certificate draft outcome, or null when none was certified.</summary>
    public StaticFieldV2LexicalCertificateOutcome? LexicalCertificate { get; }

    /// <summary>Gets the bare static-root draft outcome, or null when that route was not taken.</summary>
    public StaticFieldV2BareRootOutcome? BareRoot { get; }

    /// <summary>Gets the qualified member-lookup draft outcome, or null when none was performed.</summary>
    public StaticFieldV2MemberLookupOutcome? MemberLookup { get; }

    /// <summary>Gets the classified storage-strategy draft outcome, or null when none was classified.</summary>
    public StaticFieldV2StorageStrategyOutcome? StorageStrategy { get; }

    /// <summary>Gets the constructed-runtime draft selection, or null when none was required.</summary>
    public StaticFieldV2RuntimeConstructionSelection? RuntimeConstruction { get; }

    /// <summary>Gets the static-slot draft acquisition, or null when none was required.</summary>
    public StaticFieldV2StaticSlotOutcome? StaticSlot { get; }

    /// <summary>Gets a defensive copy of the raw bytes copied out of the dump, default when none were read.</summary>
    public ImmutableArray<byte> RawValueBytes =>
        rawValueBytes.IsDefault ? default : ExpressionV2ContractEncoding.Copy(rawValueBytes);

    /// <summary>Gets the consulted metadata Constant-row draft fact, or null when none was consulted.</summary>
    public StaticFieldV2LiteralConstantFact? LiteralConstant { get; }

    /// <summary>Gets the projected metadata-literal draft value, or null when none was projected.</summary>
    public StaticFieldV2LiteralValueOutcome? LiteralValue { get; }

    /// <summary>Gets the decoded runtime draft value, or null when none was decoded.</summary>
    public StaticFieldV2RuntimeValueOutcome? RuntimeValue { get; }

    /// <summary>Gets the reference-target assignability draft decision, or null when none was required.</summary>
    public StaticFieldV2AssignabilityOutcome? ReferenceTargetValidation { get; }

    /// <summary>Gets the frozen detached W2/W6 suffix draft descriptor, or null before syntax projection.</summary>
    public DumpExpressionSuffixDescriptor? Suffix { get; }

    /// <summary>Gets the decoded W2/W6 suffix draft value retained on a completed suffix, or null when none.</summary>
    public DumpQueryValue? SuffixValue { get; }

    /// <summary>Gets the metered caller-supplied evidence draft ledger of this evaluation.</summary>
    public StaticFieldV2PipelineEvidenceLedger EvidenceLedger { get; }

    /// <summary>Gets the retained capability-call draft ledger of the deciding stage, or null when none ran.</summary>
    public StaticFieldV2CapabilityCallLedger? CapabilityCallLedger { get; }

    /// <summary>Gets the retained frame-root memory home and value, or null when no frame root was attributed.</summary>
    public StaticFieldV2FrameRootEvaluationResult? FrameRoot { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2PipelineCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft provenance bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft provenance.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two retained draft provenance records.</summary>
    /// <param name="other">The other draft provenance record.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ExpressionProvenance? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests retained draft provenance equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for provenance with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ExpressionProvenance);

    /// <summary>Computes a deterministic hash code from immutable canonical draft provenance content.</summary>
    /// <returns>A hash code for this canonical draft provenance.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the single canonical draft result of one composed static-field expression evaluation.</summary>
/// <remarks>
/// The result carries the twelve independent draft outcome axes, the single selected route, and only the evidence
/// actually consulted. Every stop is prefix-free: the first typed stage stop forces every later axis to
/// <c>NotReached</c> and overall completeness to <c>NoAnswer</c>, and no partial plan is exposed.
/// </remarks>
public sealed class StaticFieldV2ExpressionResult : IEquatable<StaticFieldV2ExpressionResult>
{
    private const string CanonicalDomain = "static-field-v2-expression-pipeline-result";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal StaticFieldV2ExpressionResult(
        StaticFieldV2ExpressionRequest request,
        DumpExpressionV2OutcomeAxes axes,
        StaticFieldV2ExpressionRoute route,
        StaticFieldV2ExpressionProvenance provenance)
    {
        Request = request;
        Axes = axes;
        Route = route;
        Provenance = provenance;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(axes.Sha256, nameof(axes));
        writer.WriteInt32((int)route);
        writer.WriteSha256(provenance.Sha256, nameof(provenance));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete draft request that produced this result.</summary>
    public StaticFieldV2ExpressionRequest Request { get; }

    /// <summary>Gets the twelve independent draft outcome axes of this evaluation.</summary>
    public DumpExpressionV2OutcomeAxes Axes { get; }

    /// <summary>Gets the single route this draft evaluation selected and never revisited.</summary>
    public StaticFieldV2ExpressionRoute Route { get; }

    /// <summary>Gets the retained draft provenance holding only the evidence actually consulted.</summary>
    public StaticFieldV2ExpressionProvenance Provenance { get; }

    /// <summary>Gets whether this draft evaluation produced one complete exact answer.</summary>
    public bool IsComplete => Axes.Completeness == DumpExpressionCompletenessOutcome.Complete;

    /// <summary>Gets the decoded signed draft value, or null when none was decoded.</summary>
    public long? SignedValue =>
        Provenance.LiteralValue?.SignedValue ??
        Provenance.RuntimeValue?.SignedValue ??
        (FrameValue is { Kind: DumpQueryValueKind.Int32, Int32Value: { } frameInt } ? frameInt : null);

    /// <summary>Gets the decoded unsigned draft value, or null when none was decoded.</summary>
    public ulong? UnsignedValue => Provenance.LiteralValue?.UnsignedValue ?? Provenance.RuntimeValue?.UnsignedValue;

    /// <summary>Gets the decoded literal string draft value, or null when none was decoded.</summary>
    public string? StringValue =>
        Provenance.LiteralValue?.StringValue ??
        (FrameValue is { Kind: DumpQueryValueKind.String } frameString ? frameString.StringValue : null);

    /// <summary>Gets the retained non-null managed reference address, or null when none was decoded.</summary>
    public ulong? ReferenceAddress => Provenance.RuntimeValue?.ReferenceAddress ?? Provenance.FrameRoot?.ReferenceAddress;

    /// <summary>Gets the decoded frame-root value retained on an exact frame answer, or null when none.</summary>
    public DumpQueryValue? FrameValue => Provenance.FrameRoot?.Value;

    /// <summary>Gets the decoded W2/W6 suffix draft value retained on a completed suffix, or null when none.</summary>
    public DumpQueryValue? SuffixValue => Provenance.SuffixValue;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft result bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft result.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two composed draft evaluation results.</summary>
    /// <param name="other">The other draft result.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2ExpressionResult? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests composed draft evaluation result equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a result with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ExpressionResult);

    /// <summary>Computes a deterministic hash code from immutable canonical draft result content.</summary>
    /// <returns>A hash code for this canonical draft result.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Composes every landed static-field V2 draft stage into one frozen no-fallback operation order.</summary>
/// <remarks>
/// <para>
/// This draft composition executes the sixteen-step order exactly once per evaluation. A private step cursor makes the
/// order structural: every stage announces its own step number, the cursor refuses a repeated or out-of-order step, and
/// no stage may reparse, retry with a broader profile, or rebuild an earlier candidate universe.
/// </para>
/// <para>
/// Profiles never fall through. <see cref="Evaluate"/> owns <c>StaticFieldExpressionV2</c> alone and rejects the frame
/// profile with a typed unsupported syntax draft stop; <see cref="EvaluateFrameValue"/> owns
/// <c>FrameValueExpressionV1</c> alone and rejects the static profile the same way. Neither entry point can call the
/// other, so a V1 or V2 failure can never reach a second binder.
/// </para>
/// <para>
/// Declared draft coverage boundaries: the scoped-context, runtime, and metadata Constant-row evidence are caller-owned
/// seams; the declared field type is derived only from a ground primitive FieldSig; a contextual or bare route binds an
/// owner definition rather than a closed construction, so a type-argument-bearing contextual spelling is a typed
/// unsupported construction; the unchanged W2/W6 suffix evaluator is wired by a later checkpoint; and reference-target
/// validation runs only when the caller supplies the exact expected target type.
/// </para>
/// </remarks>
public static class StaticFieldV2ExpressionPipeline
{
    private const byte FieldSignatureCallingConvention = 0x06;

    /// <summary>Evaluates one explicitly selected <c>StaticFieldExpressionV2</c> draft request end to end.</summary>
    /// <param name="request">The complete composed draft evaluation request.</param>
    /// <returns>One sealed canonical draft result carrying the twelve independent axes and its retained provenance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2ExpressionResult Evaluate(StaticFieldV2ExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Execution(request).Run(DumpExpressionProfileKind.StaticFieldExpressionV2);
    }

    /// <summary>Evaluates one explicitly selected <c>FrameValueExpressionV1</c> draft request in isolation.</summary>
    /// <param name="request">The complete composed draft evaluation request.</param>
    /// <remarks>
    /// This separate entry point exists so a static draft request can never fall through to a frame answer and a frame
    /// draft request can never fall through to static lookup. The admitted frame-value binder itself is wired by a later
    /// checkpoint, so an actually frame-profiled request stops with a typed unsupported syntax draft disposition.
    /// </remarks>
    /// <returns>One sealed canonical draft result carrying the twelve independent axes and its retained provenance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2ExpressionResult EvaluateFrameValue(StaticFieldV2ExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Execution(request).Run(DumpExpressionProfileKind.FrameValueExpressionV1);
    }

    private sealed class Execution
    {
        private readonly StaticFieldV2ExpressionRequest request;
        private readonly SortedSet<StaticFieldV2PipelineCoverageBoundary> boundaries = [];
        private int completedStep;
        private int scopedContextCalls;
        private int lexicalEnvelopeCalls;
        private int constantRowCalls;
        private int constructionCandidateCalls;
        private int slotFactCalls;
        private int memoryReadCalls;
        private int suffixEvaluationCalls;
        private int frameRootEvaluationCalls;

        private StaticFieldV2SyntaxOutcome? syntax;
        private StaticFieldV2ExpressionDescriptor? descriptor;
        private FrameValueV1ExpressionDescriptor? frameDescriptor;
        private StaticFieldV2FrameRootEvaluationResult? frameRoot;
        private StaticFieldV2ExpressionRoute route = StaticFieldV2ExpressionRoute.NotSelected;
        private StaticFieldV2ScopedContextOutcome? scopedContext;
        private StaticFieldV2ContextualBindingOutcome? contextualBinding;
        private StaticFieldV2TypeNameBindingOutcome? explicitNameBinding;
        private StaticFieldV2ClosedConstructionOutcome? ownerConstruction;
        private StaticFieldV2LexicalCertificateOutcome? lexicalCertificate;
        private StaticFieldV2BareRootOutcome? bareRoot;
        private StaticFieldV2MemberLookupOutcome? memberLookup;
        private StaticFieldV2StorageStrategyOutcome? storageStrategy;
        private StaticFieldV2RuntimeConstructionSelection? runtimeConstruction;
        private StaticFieldV2StaticSlotOutcome? staticSlot;
        private ImmutableArray<byte> rawValueBytes;
        private StaticFieldV2LiteralConstantFact? literalConstant;
        private StaticFieldV2LiteralValueOutcome? literalValue;
        private StaticFieldV2RuntimeValueOutcome? runtimeValue;
        private StaticFieldV2AssignabilityOutcome? referenceTargetValidation;
        private DumpExpressionSuffixDescriptor? suffix;
        private DumpQueryValue? suffixValue;
        private StaticFieldV2CapabilityCallLedger? capabilityCallLedger;

        internal Execution(StaticFieldV2ExpressionRequest request)
        {
            this.request = request;
            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.FrameValueProfileOwnedBySeparateEntryPoint);
            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.SuffixEvaluationSuppliedByCallerSeam);
        }

        internal StaticFieldV2ExpressionResult Run(DumpExpressionProfileKind ownedProfile)
        {
            // Step 1: select the caller-requested profile and enforce the common expression-input bounds. Neither entry
            // point can ever process the other profile, so a static request never reaches the frame binder and a frame
            // request never reaches a static binder.
            EnterStep(1);
            if (request.Profile != ownedProfile)
            {
                return ProfileRejection();
            }

            if (ownedProfile == DumpExpressionProfileKind.FrameValueExpressionV1)
            {
                // The frame-value binder engages only when the caller supplies its frame-root evidence seam. A
                // frame-profiled request without that seam is declined as an unsupported profile exactly as before, so
                // the frame binder never runs without its caller-owned dependency and no answer is fabricated.
                return request.FrameRootEvaluation is null
                    ? ProfileRejection()
                    : RunFrameValue();
            }

            // Steps 2 through 4: the sole complete parse, its integrity checks, and the detached projection.
            EnterStep(2);
            syntax = StaticFieldV2ExpressionParser.Parse(request.ExpressionText);
            EnterStep(3);
            if (syntax.Status != DumpExpressionSyntaxStatus.Admitted || syntax.Descriptor is null)
            {
                return SyntaxStop(syntax.Status);
            }
            EnterStep(4);
            descriptor = syntax.Descriptor;

            // Step 5: determine the single route inside the selected descriptor.
            EnterStep(5);
            route = SelectRoute(descriptor);

            // Step 6: acquire only the context capabilities the chosen route requires.
            EnterStep(6);
            var context = AcquireContext();
            if (context is not null)
            {
                return context;
            }

            // Step 7: resolve the namespace, type, alias scope, constraints, and closed metadata construction.
            EnterStep(7);
            var binding = BindOwner();
            if (binding is not null)
            {
                return binding;
            }

            // Step 8: perform definition-kind-specific qualified, bare, or using-static member lookup.
            EnterStep(8);
            var member = SelectMember();
            if (member is not null)
            {
                return member;
            }

            var selected = SelectedCandidate();
            var fieldRow = selected.FieldRow;

            // Step 9: instantiate the FieldDef signature and decode the literal Constant row when one applies.
            EnterStep(9);
            boundaries.Add(
                StaticFieldV2PipelineCoverageBoundary.DeclaredFieldTypeLimitedToGroundPrimitiveSignature);
            var declaredType = GroundDeclaredType(fieldRow);
            if (fieldRow.IsLiteral && fieldRow.HasDefault)
            {
                literalConstant = AcquireConstantRow(fieldRow);
            }

            // Step 10: freeze the storage strategy and acquire only its required runtime identity.
            EnterStep(10);
            storageStrategy = StaticFieldV2StorageStrategyBinder.ClassifyStrategy(
                StaticFieldV2StorageStrategyRequest.Create(fieldRow, fieldRow.DeclaringTypeDefinition));
            if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
                storageStrategy.Strategy is not { } strategy)
            {
                return StorageStop(MapStorageStrategyStop(storageStrategy.ResultKind));
            }

            var vector = storageStrategy.CapabilityRequirements;
            if (vector.RuntimeConstruction == StaticFieldV2CapabilityRequirement.Required)
            {
                var runtime = SelectRuntimeConstruction();
                if (runtime is not null)
                {
                    return runtime;
                }
            }

            // Step 11: locate the exact stored address and geometry when a raw value read is required.
            EnterStep(11);
            StaticFieldV2RuntimeSlotFacts? slotFacts = null;
            if (vector.MemoryRead == StaticFieldV2CapabilityRequirement.Required)
            {
                if (request.RuntimeEvidence is not { SuppliesSlotFacts: true } evidence)
                {
                    return StorageStop(DumpExpressionStorageOutcome.Unavailable);
                }
                slotFactCalls++;
                slotFacts = evidence.AcquireSlotFacts(storageStrategy, runtimeConstruction);
                if (slotFacts is null)
                {
                    return StorageStop(DumpExpressionStorageOutcome.Unavailable);
                }
            }

            // Step 12: freeze the complete strategy-tagged plan before obtaining any value.
            EnterStep(12);
            if (slotFacts is not null)
            {
                staticSlot = StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
                    StaticFieldV2StaticSlotRequest.Create(
                        storageStrategy,
                        slotFacts.ReadWidth,
                        runtimeConstruction,
                        slotFacts.SlotAddress,
                        slotFacts.SelectedThread,
                        slotFacts.ModuleContent,
                        slotFacts.FieldRvaRowToken,
                        slotFacts.MappedRelativeVirtualAddress,
                        slotFacts.MappedAddress,
                        request.CapabilityProbes));
                capabilityCallLedger = staticSlot.CapabilityCallLedger;
                if (staticSlot.ResultKind != StaticFieldV2StaticSlotResultKind.Exact || staticSlot.Slot is null)
                {
                    return StorageStop(
                        staticSlot.ResultKind == StaticFieldV2StaticSlotResultKind.Invalid
                            ? DumpExpressionStorageOutcome.Invalid
                            : DumpExpressionStorageOutcome.Unsupported);
                }
            }

            // Step 13: read and decode dump memory, or project the frozen exact literal.
            EnterStep(13);
            var value = strategy == StaticFieldV2StorageStrategy.MetadataLiteral
                ? ProjectLiteral(fieldRow)
                : DecodeStoredValue(declaredType, staticSlot!.Slot!);

            // Step 14: validate a non-null reference target through constructed assignability when required.
            EnterStep(14);
            var referenceTargetBlocked = ValidateReferenceTarget(value);

            // Step 15: evaluate the unchanged W2/W6 detached suffix rooted at the resolved reference.
            EnterStep(15);
            var suffixAxis = EvaluateSuffix(value, referenceTargetBlocked);

            // Step 16: project the single canonical result.
            EnterStep(16);
            return Complete(value, suffixAxis);
        }

        private StaticFieldV2ExpressionResult RunFrameValue()
        {
            // Step 2: the sole complete frame-value parse over the frozen FrameValueExpressionV1 grammar.
            EnterStep(2);
            var frameSyntax = FrameValueV1ExpressionParser.Parse(request.ExpressionText);

            // Step 3: a non-admitted parse is a typed syntax stop; no later frame axis is reached.
            EnterStep(3);
            if (frameSyntax.Status != DumpExpressionSyntaxStatus.Admitted || frameSyntax.Descriptor is null)
            {
                return SyntaxStop(frameSyntax.Status);
            }

            // Step 4: retain the detached descriptor and its frozen unchanged W2/W6 suffix.
            EnterStep(4);
            frameDescriptor = frameSyntax.Descriptor;
            suffix = frameDescriptor.Suffix;

            // Step 5: project the descriptor root into the frame-root evidence request and call the caller-owned seam.
            EnterStep(5);
            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.FrameRootEvidenceSuppliedByCallerSeam);
            frameRootEvaluationCalls++;
            frameRoot = request.FrameRootEvaluation!.Evaluate(
                new StaticFieldV2FrameRootEvaluationRequest(frameDescriptor.RootKind, frameDescriptor.Identifier));

            // Step 6: map the frame-root disposition onto the independent context and root-attribution axes.
            EnterStep(6);
            var rootStop = MapFrameRootStop(frameRoot.Disposition);
            if (rootStop is not null)
            {
                return rootStop;
            }

            // Step 7: project the exact frame value and its independent suffix, then compose the single result.
            EnterStep(7);
            var value = frameRoot.Value!.Kind == DumpQueryValueKind.Null
                ? DumpExpressionValueOutcome.ExactNull
                : DumpExpressionValueOutcome.ExactValue;
            var lexical = frameRoot.RootKind == StaticFieldV2FrameValueRootKind.Local
                ? DumpExpressionLexicalCompletenessOutcome.Complete
                : DumpExpressionLexicalCompletenessOutcome.NotRequired;
            var suffixAxis = EvaluateFrameSuffix(value);
            return FrameComplete(lexical, value, suffixAxis);
        }

        private StaticFieldV2ExpressionResult? MapFrameRootStop(StaticFieldV2FrameRootDisposition disposition) =>
            disposition switch
            {
                StaticFieldV2FrameRootDisposition.Exact => null,
                StaticFieldV2FrameRootDisposition.ContextUnavailable => FrameRootStop(
                    DumpExpressionContextOutcome.Unavailable,
                    DumpExpressionRootAttributionOutcome.NotReached),
                StaticFieldV2FrameRootDisposition.ContextAmbiguous => FrameRootStop(
                    DumpExpressionContextOutcome.Ambiguous,
                    DumpExpressionRootAttributionOutcome.NotReached),
                StaticFieldV2FrameRootDisposition.RootUnavailable => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Unavailable),
                StaticFieldV2FrameRootDisposition.RootAmbiguous => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Ambiguous),
                StaticFieldV2FrameRootDisposition.RootShadowed => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Shadowed),
                StaticFieldV2FrameRootDisposition.RootInvalid => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Invalid),

                // A register home and a selected frame's own generic arguments are the two frozen W8.1 non-admissions:
                // both map to a typed executable root-attribution non-admission that surfaces their frozen code through
                // the retained frame-root result, never an absent gap and never a crash.
                StaticFieldV2FrameRootDisposition.RegisterHomeNotAdmitted => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Unsupported),
                StaticFieldV2FrameRootDisposition.GenericArgumentNotAdmitted => FrameRootStop(
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Unsupported),
                _ => FrameRootStop(
                    DumpExpressionContextOutcome.Invalid,
                    DumpExpressionRootAttributionOutcome.NotReached),
            };

        private DumpExpressionSuffixOutcome EvaluateFrameSuffix(DumpExpressionValueOutcome value)
        {
            if (suffix is not { } descriptor || descriptor.Kind == DumpExpressionSuffixKind.NotRequested)
            {
                return DumpExpressionSuffixOutcome.NotRequested;
            }

            // An exact-null root roots no object, so reuse the unchanged W2/W6 null semantics without any suffix read.
            if (value == DumpExpressionValueOutcome.ExactNull)
            {
                return ResolveExactNullSuffix(descriptor);
            }

            // A primitive or value-typed frame root cannot root an object navigation, exactly as a literal cannot.
            if (frameRoot is not { ReferenceAddress: { } referenceAddress } || referenceAddress == 0)
            {
                return DumpExpressionSuffixOutcome.Unsupported;
            }

            // Root the unchanged W2/W6 member chain at the resolved exact reference through the same caller seam.
            if (request.SuffixEvaluation is not { } source)
            {
                return DumpExpressionSuffixOutcome.Blocked;
            }

            suffixEvaluationCalls++;
            var suffixResult = source.Evaluate(new StaticFieldV2SuffixEvaluationRequest(referenceAddress, descriptor));
            var mapped = MapSuffixResult(suffixResult);
            if (mapped == DumpExpressionSuffixOutcome.Completed)
            {
                suffixValue = suffixResult.Value;
            }
            return mapped;
        }

        private StaticFieldV2ExpressionResult FrameRootStop(
            DumpExpressionContextOutcome context,
            DumpExpressionRootAttributionOutcome rootAttribution) =>
            Project(
                DumpExpressionV2OutcomeAxes.Create(
                    DumpExpressionSyntaxStatus.Admitted,
                    context,
                    rootAttribution,
                    DumpExpressionLexicalCompletenessOutcome.NotReached,
                    DumpExpressionTypeBindingOutcome.NotReached,
                    DumpExpressionTypeConstructionOutcome.NotReached,
                    DumpExpressionMemberLookupOutcome.NotReached,
                    DumpExpressionRuntimeConstructionOutcome.NotReached,
                    DumpExpressionStorageOutcome.NotReached,
                    DumpExpressionValueOutcome.NotReached,
                    DumpExpressionSuffixOutcome.NotReached,
                    DumpExpressionCompletenessOutcome.NoAnswer));

        private StaticFieldV2ExpressionResult FrameComplete(
            DumpExpressionLexicalCompletenessOutcome lexical,
            DumpExpressionValueOutcome value,
            DumpExpressionSuffixOutcome suffixOutcome) =>
            Project(
                DumpExpressionV2OutcomeAxes.Create(
                    DumpExpressionSyntaxStatus.Admitted,
                    DumpExpressionContextOutcome.Exact,
                    DumpExpressionRootAttributionOutcome.Exact,
                    lexical,
                    DumpExpressionTypeBindingOutcome.NotRequired,
                    DumpExpressionTypeConstructionOutcome.NotRequired,
                    DumpExpressionMemberLookupOutcome.NotRequired,
                    DumpExpressionRuntimeConstructionOutcome.NotRequired,
                    DumpExpressionStorageOutcome.Exact,
                    value,
                    suffixOutcome,
                    ValueCompleteness(value, suffixOutcome)));

        private void EnterStep(int step)
        {
            if (step != completedStep + 1)
            {
                throw new InvalidOperationException(
                    $"The fixed operation order requires step {completedStep + 1} but step {step} was entered.");
            }
            completedStep = step;
        }

        private StaticFieldV2ExpressionRoute SelectRoute(StaticFieldV2ExpressionDescriptor projected)
        {
            if (projected.AliasQualifier.Kind == StaticFieldV2AliasKind.Global)
            {
                return StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal;
            }
            if (projected.AliasQualifier.Kind == StaticFieldV2AliasKind.Named)
            {
                return StaticFieldV2ExpressionRoute.Contextual;
            }

            var hasQualifiedOwner = false;
            foreach (var partition in projected.Partitions)
            {
                if (partition.CandidateKind == StaticFieldV2CandidateKind.QualifiedOwner)
                {
                    hasQualifiedOwner = true;
                    break;
                }
            }
            if (!hasQualifiedOwner)
            {
                return StaticFieldV2ExpressionRoute.BareStaticMember;
            }

            return request.ScopedContext is null
                ? StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal
                : StaticFieldV2ExpressionRoute.Contextual;
        }

        private StaticFieldV2ExpressionResult? AcquireContext()
        {
            if (route == StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal)
            {
                return null;
            }

            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.ScopedContextEvidenceSuppliedByCallerSeam);
            if (request.ScopedContext is not { } source)
            {
                return ContextStop(DumpExpressionContextOutcome.Unavailable);
            }

            scopedContextCalls++;
            scopedContext = StaticFieldV2ScopedContextBinder.ProjectContext(source.AcquireScopedContextRequest());
            return scopedContext.ResultKind switch
            {
                StaticFieldV2ScopedContextResultKind.Exact => null,
                StaticFieldV2ScopedContextResultKind.Invalid => ContextStop(DumpExpressionContextOutcome.Invalid),
                _ => ContextStop(DumpExpressionContextOutcome.Partial),
            };
        }

        private StaticFieldV2ExpressionResult? BindOwner()
        {
            switch (route)
            {
                case StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal:
                    explicitNameBinding = StaticFieldV2TypeNameBinder.BindExplicitRoute(
                        descriptor!,
                        request.AncestryPortfolio.ResolutionPortfolio.ChainPortfolio);
                    if (explicitNameBinding.ResultKind != StaticFieldV2TypeNameBindingResultKind.Exact)
                    {
                        return TypeBindingStop(MapExplicitBinding(explicitNameBinding.ResultKind));
                    }
                    ownerConstruction = StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                        explicitNameBinding,
                        request.AncestryPortfolio,
                        request.ConstraintPortfolio,
                        request.InterfaceImplementationPortfolio);
                    return ownerConstruction.ResultKind == StaticFieldV2ClosedConstructionResultKind.Exact
                        ? null
                        : ConstructionStop(MapConstruction(ownerConstruction.ResultKind));

                case StaticFieldV2ExpressionRoute.Contextual:
                    contextualBinding = StaticFieldV2ScopedContextBinder.BindContextualRoute(
                        descriptor!,
                        scopedContext!);
                    if (contextualBinding.ResultKind != StaticFieldV2ContextualBindingResultKind.Exact)
                    {
                        return TypeBindingStop(MapContextualBinding(contextualBinding.ResultKind));
                    }
                    boundaries.Add(
                        StaticFieldV2PipelineCoverageBoundary.ContextualRouteClosedConstructionDeferred);
                    return RequiresClosedConstruction(descriptor!)
                        ? ConstructionStop(DumpExpressionTypeConstructionOutcome.Unsupported)
                        : null;

                default:
                    return null;
            }
        }

        private StaticFieldV2ExpressionResult? SelectMember()
        {
            if (route == StaticFieldV2ExpressionRoute.BareStaticMember)
            {
                return BindBareRoot();
            }

            var (module, ownerToken, partitionIndex) = route == StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal
                ? OwnerFromExplicitBinding()
                : OwnerFromContextualBinding();
            var partition = descriptor!.Partitions[partitionIndex];
            suffix = partition.Suffix;
            memberLookup = StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
                module,
                ownerToken,
                descriptor.Segments[partition.FieldSegmentIndex].Identifier,
                request.AncestryPortfolio,
                request.FieldCatalogsCore,
                request.AccessibilityMode,
                request.RequestingAssembly,
                request.FriendAssemblyGrantsCore,
                request.InterfaceImplementationPortfolio));
            return memberLookup.ResultKind == StaticFieldV2MemberLookupResultKind.Exact
                ? null
                : MemberStop(MapMemberLookup(memberLookup.ResultKind));
        }

        private StaticFieldV2ExpressionResult? BindBareRoot()
        {
            var partition = descriptor!.Partitions[0];
            suffix = partition.Suffix;
            if (request.ScopedContext is not { SuppliesLexicalEnvelope: true } source)
            {
                return LexicalStop(DumpExpressionLexicalCompletenessOutcome.Partial);
            }

            lexicalEnvelopeCalls++;
            var envelope = source.AcquireLexicalEnvelope();
            if (envelope is null)
            {
                return LexicalStop(DumpExpressionLexicalCompletenessOutcome.Partial);
            }

            var certificateRequest = StaticFieldV2LexicalCertificateRequest.Create(
                envelope,
                descriptor.Segments[partition.FieldSegmentIndex].Identifier,
                scopedContext!.Request.SelectedModule,
                scopedContext.Request.SelectedTypeDefinition,
                request.AncestryPortfolio,
                request.FieldCatalogsCore);
            bareRoot = StaticFieldV2LexicalCompleteness.BindBareStaticRoot(StaticFieldV2BareRootRequest.Create(
                certificateRequest,
                scopedContext,
                request.AccessibilityMode,
                request.RequestingAssembly,
                request.FriendAssemblyGrantsCore));
            lexicalCertificate = bareRoot.Certificate;

            var lexical = MapCertificate(lexicalCertificate.ResultKind);
            if (lexical is not (DumpExpressionLexicalCompletenessOutcome.Complete or
                DumpExpressionLexicalCompletenessOutcome.NotRequired))
            {
                return LexicalStop(lexical);
            }
            return bareRoot.ResultKind == StaticFieldV2BareRootResultKind.Exact
                ? null
                : MemberStop(MapBareRoot(bareRoot.ResultKind));
        }

        private StaticFieldV2ExpressionResult? SelectRuntimeConstruction()
        {
            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.RuntimeEvidenceSuppliedByCallerSeam);
            if (ownerConstruction is not { } construction ||
                construction.ResultKind != StaticFieldV2ClosedConstructionResultKind.Exact)
            {
                return RuntimeStop(DumpExpressionRuntimeConstructionOutcome.Unsupported);
            }
            if (request.RuntimeEvidence is not { SuppliesConstructionCandidates: true } evidence)
            {
                return RuntimeStop(DumpExpressionRuntimeConstructionOutcome.Unavailable);
            }

            constructionCandidateCalls++;
            var candidates = evidence.AcquireConstructionCandidates(construction, storageStrategy!);
            runtimeConstruction = StaticFieldV2RuntimeConstructionBinder.SelectConstruction(
                StaticFieldV2RuntimeConstructionRequest.Create(
                    construction,
                    storageStrategy!,
                    candidates.IsDefault ? [] : candidates,
                    request.CapabilityProbes));
            capabilityCallLedger = runtimeConstruction.CapabilityCallLedger;
            return runtimeConstruction.ResultKind switch
            {
                StaticFieldV2RuntimeConstructionSelectionKind.Exact => null,
                StaticFieldV2RuntimeConstructionSelectionKind.Absent =>
                    RuntimeStop(DumpExpressionRuntimeConstructionOutcome.Absent),
                StaticFieldV2RuntimeConstructionSelectionKind.Ambiguous =>
                    RuntimeStop(DumpExpressionRuntimeConstructionOutcome.Ambiguous),
                StaticFieldV2RuntimeConstructionSelectionKind.NotRequired => null,
                _ => RuntimeStop(DumpExpressionRuntimeConstructionOutcome.Partial),
            };
        }

        private StaticFieldV2LiteralConstantFact? AcquireConstantRow(MetadataFieldDefinitionTableRowIdentity fieldRow)
        {
            if (request.LiteralConstantSource is not { } source)
            {
                return null;
            }
            boundaries.Add(StaticFieldV2PipelineCoverageBoundary.MetadataConstantRowSuppliedByCallerSeam);
            constantRowCalls++;
            return source(fieldRow);
        }

        private DumpExpressionValueOutcome ProjectLiteral(MetadataFieldDefinitionTableRowIdentity fieldRow)
        {
            if (literalConstant is not { } constant)
            {
                return DumpExpressionValueOutcome.Unavailable;
            }

            literalValue = StaticFieldV2StorageStrategyBinder.ProjectLiteral(
                StaticFieldV2LiteralProjectionRequest.Create(
                    fieldRow,
                    constant.ConstantTypeCode,
                    constant.ConstantValueBlob,
                    FieldCatalogFor(fieldRow),
                    request.CapabilityProbes));
            capabilityCallLedger = literalValue.CapabilityCallLedger;
            return literalValue.ResultKind switch
            {
                StaticFieldV2LiteralValueResultKind.Exact =>
                    literalValue.ValueKind == StaticFieldV2LiteralValueKind.Null
                        ? DumpExpressionValueOutcome.ExactNull
                        : DumpExpressionValueOutcome.ExactValue,
                StaticFieldV2LiteralValueResultKind.Invalid => DumpExpressionValueOutcome.Invalid,
                StaticFieldV2LiteralValueResultKind.Unsupported => DumpExpressionValueOutcome.Unsupported,
                _ => DumpExpressionValueOutcome.Partial,
            };
        }

        private DumpExpressionValueOutcome DecodeStoredValue(
            MetadataClosedTypeIdentity? declaredType,
            StaticFieldV2StaticSlotIdentity slot)
        {
            if (declaredType is null)
            {
                return DumpExpressionValueOutcome.Unsupported;
            }
            if (request.RuntimeEvidence is not { SuppliesRawMemoryRead: true } evidence)
            {
                return DumpExpressionValueOutcome.Unavailable;
            }

            request.CapabilityProbes?.Invoke(StaticFieldV2StorageCapability.MemoryRead);
            memoryReadCalls++;
            var copied = evidence.AcquireRawBytes(slot.EffectiveAddress, slot.ReadWidth);
            if (copied.IsDefault)
            {
                return DumpExpressionValueOutcome.Unavailable;
            }

            rawValueBytes = ExpressionV2ContractEncoding.Copy(copied);
            runtimeValue = StaticFieldV2ValueDecoder.DecodeValue(StaticFieldV2RuntimeValueRequest.Create(
                declaredType,
                rawValueBytes,
                OwnerModule().Module.PointerWidth,
                null,
                null,
                request.CapabilityProbes));
            return runtimeValue.ResultKind switch
            {
                StaticFieldV2RuntimeValueResultKind.Exact =>
                    runtimeValue.ValueKind == StaticFieldV2RuntimeValueKind.NullReference
                        ? DumpExpressionValueOutcome.ExactNull
                        : DumpExpressionValueOutcome.ExactValue,
                StaticFieldV2RuntimeValueResultKind.Invalid => DumpExpressionValueOutcome.Invalid,
                _ => DumpExpressionValueOutcome.Unsupported,
            };
        }

        private bool ValidateReferenceTarget(DumpExpressionValueOutcome value)
        {
            boundaries.Add(
                StaticFieldV2PipelineCoverageBoundary.ReferenceTargetValidationRequiresSuppliedTarget);
            if (value is not DumpExpressionValueOutcome.ExactValue ||
                runtimeValue is not { ReferenceAddress: > 0 } reference ||
                request.ReferenceTargetType is not { } target)
            {
                return false;
            }

            referenceTargetValidation = StaticFieldV2AssignabilityBinder.IsAssignable(
                StaticFieldV2AssignabilityRequest.Create(
                    reference.Request.DeclaredType,
                    target,
                    request.AncestryPortfolio,
                    request.InterfaceImplementationPortfolio));
            return referenceTargetValidation.ResultKind != StaticFieldV2AssignabilityResultKind.Assignable;
        }

        private DumpExpressionSuffixOutcome EvaluateSuffix(
            DumpExpressionValueOutcome value,
            bool referenceTargetBlocked)
        {
            // A blocked reference target blocks the suffix before any navigation is attempted.
            if (referenceTargetBlocked)
            {
                return DumpExpressionSuffixOutcome.Blocked;
            }

            // A non-exact root value maps its own disposition onto the independent suffix axis without any read.
            if (value is not (DumpExpressionValueOutcome.ExactValue or DumpExpressionValueOutcome.ExactNull))
            {
                return value switch
                {
                    DumpExpressionValueOutcome.Partial => DumpExpressionSuffixOutcome.Blocked,
                    DumpExpressionValueOutcome.Unavailable => DumpExpressionSuffixOutcome.Blocked,
                    DumpExpressionValueOutcome.Conflict => DumpExpressionSuffixOutcome.Conflict,
                    DumpExpressionValueOutcome.Invalid => DumpExpressionSuffixOutcome.Invalid,
                    _ => DumpExpressionSuffixOutcome.Unsupported,
                };
            }

            if (suffix is not { } descriptor || descriptor.Kind == DumpExpressionSuffixKind.NotRequested)
            {
                return DumpExpressionSuffixOutcome.NotRequested;
            }

            // An exact-null reference roots no object, so reuse the unchanged W2/W6 null semantics without a read.
            if (value == DumpExpressionValueOutcome.ExactNull)
            {
                return ResolveExactNullSuffix(descriptor);
            }

            // A value-type or primitive field value cannot root an object navigation, exactly as a literal cannot.
            if (runtimeValue is not { ReferenceAddress: > 0 } reference)
            {
                return DumpExpressionSuffixOutcome.Unsupported;
            }

            // Root the unchanged W2/W6 member chain at the resolved exact reference through the caller seam.
            if (request.SuffixEvaluation is not { } source)
            {
                return DumpExpressionSuffixOutcome.Blocked;
            }

            suffixEvaluationCalls++;
            var suffixResult = source.Evaluate(
                new StaticFieldV2SuffixEvaluationRequest(reference.ReferenceAddress!.Value, descriptor));
            var mapped = MapSuffixResult(suffixResult);
            if (mapped == DumpExpressionSuffixOutcome.Completed)
            {
                suffixValue = suffixResult.Value;
            }
            return mapped;
        }

        private DumpExpressionSuffixOutcome ResolveExactNullSuffix(DumpExpressionSuffixDescriptor descriptor)
        {
            // A direct '.' first edge over an exact-null root is the unchanged W2/W6 null-target block; a conditional
            // '?.' first edge short-circuits to the coalesce fallback when present, otherwise to the exact null.
            if (descriptor.Segments[0].AccessKind != DumpExpressionSuffixAccessKind.Conditional)
            {
                return DumpExpressionSuffixOutcome.Blocked;
            }

            suffixValue = descriptor.FallbackKind switch
            {
                DumpExpressionFallbackKind.Int32 => DumpQueryValue.FromInt32(descriptor.Int32Fallback!.Value),
                DumpExpressionFallbackKind.String => DumpQueryValue.FromString(descriptor.StringFallback!),
                _ => DumpQueryValue.FromNull(),
            };
            return DumpExpressionSuffixOutcome.Completed;
        }

        private static DumpExpressionSuffixOutcome MapSuffixResult(EvaluationResult<DumpQueryValue> result)
        {
            if (result.Completion == EvaluationCompletionStatus.Invalid ||
                result.Evidence == EvaluationEvidenceStatus.Invalid)
            {
                return DumpExpressionSuffixOutcome.Invalid;
            }
            if (result.Evidence == EvaluationEvidenceStatus.Conflict)
            {
                return DumpExpressionSuffixOutcome.Conflict;
            }
            if (result.Completion == EvaluationCompletionStatus.Completed &&
                result.Completeness == EvaluationCompleteness.Complete &&
                result.Evidence == EvaluationEvidenceStatus.Exact &&
                result.Value is not null)
            {
                return DumpExpressionSuffixOutcome.Completed;
            }
            return DumpExpressionSuffixOutcome.Blocked;
        }

        private (StaticFieldMetadataModuleIdentity Module, int OwnerToken, int PartitionIndex)
            OwnerFromExplicitBinding()
        {
            var group = explicitNameBinding!.SelectedCandidate!;
            return (group.SourceModule, group.FinalTypeDefinitionToken, group.Occurrences[0].PartitionIndex);
        }

        private (StaticFieldMetadataModuleIdentity Module, int OwnerToken, int PartitionIndex)
            OwnerFromContextualBinding()
        {
            var group = contextualBinding!.SelectedCandidate!;
            return (group.SourceModule, group.FinalTypeDefinitionToken, group.Candidates[0].PartitionIndex);
        }

        private StaticFieldMetadataModuleIdentity OwnerModule() =>
            route == StaticFieldV2ExpressionRoute.BareStaticMember
                ? bareRoot!.SelectedOwnerModule!
                : memberLookup!.Request.OwnerModule;

        private StaticFieldV2MemberCandidateIdentity SelectedCandidate() =>
            route == StaticFieldV2ExpressionRoute.BareStaticMember
                ? bareRoot!.SelectedField!
                : memberLookup!.SelectedCandidate!;

        private MetadataFieldDefinitionTableCatalogIdentity? FieldCatalogFor(
            MetadataFieldDefinitionTableRowIdentity fieldRow)
        {
            if (request.FieldCatalogsCore.IsDefault)
            {
                return null;
            }
            foreach (var catalog in request.FieldCatalogsCore)
            {
                if (catalog is not null && catalog.SourceEnds.Equals(fieldRow.SourceEnds))
                {
                    return catalog;
                }
            }
            return null;
        }

        private static bool RequiresClosedConstruction(StaticFieldV2ExpressionDescriptor projected)
        {
            foreach (var segment in projected.Segments)
            {
                if (segment.Arity != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static MetadataClosedTypeIdentity? GroundDeclaredType(
            MetadataFieldDefinitionTableRowIdentity fieldRow)
        {
            var signature = fieldRow.SignatureBytes;
            if (signature.Length != 2 || signature[0] != FieldSignatureCallingConvention)
            {
                return null;
            }
            return signature[1] switch
            {
                0x02 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Boolean),
                0x03 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Char),
                0x04 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int8),
                0x05 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.UInt8),
                0x06 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int16),
                0x07 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.UInt16),
                0x08 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int32),
                0x09 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.UInt32),
                0x0A => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int64),
                0x0B => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.UInt64),
                0x0C => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Single),
                0x0D => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Double),
                0x0E => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.String),
                0x18 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.NativeInt),
                0x19 => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.NativeUInt),
                0x1C => MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Object),
                _ => null,
            };
        }

        private static DumpExpressionTypeBindingOutcome MapExplicitBinding(
            StaticFieldV2TypeNameBindingResultKind resultKind) => resultKind switch
            {
                StaticFieldV2TypeNameBindingResultKind.Absent => DumpExpressionTypeBindingOutcome.Absent,
                StaticFieldV2TypeNameBindingResultKind.Ambiguous => DumpExpressionTypeBindingOutcome.Ambiguous,
                StaticFieldV2TypeNameBindingResultKind.Invalid => DumpExpressionTypeBindingOutcome.Invalid,
                StaticFieldV2TypeNameBindingResultKind.Unsupported => DumpExpressionTypeBindingOutcome.Unsupported,
                _ => DumpExpressionTypeBindingOutcome.Partial,
            };

        private static DumpExpressionTypeBindingOutcome MapContextualBinding(
            StaticFieldV2ContextualBindingResultKind resultKind) => resultKind switch
            {
                StaticFieldV2ContextualBindingResultKind.Absent => DumpExpressionTypeBindingOutcome.Absent,
                StaticFieldV2ContextualBindingResultKind.Ambiguous => DumpExpressionTypeBindingOutcome.Ambiguous,
                StaticFieldV2ContextualBindingResultKind.Invalid => DumpExpressionTypeBindingOutcome.Invalid,
                StaticFieldV2ContextualBindingResultKind.Unsupported => DumpExpressionTypeBindingOutcome.Unsupported,
                _ => DumpExpressionTypeBindingOutcome.Partial,
            };

        private static DumpExpressionTypeConstructionOutcome MapConstruction(
            StaticFieldV2ClosedConstructionResultKind resultKind) => resultKind switch
            {
                StaticFieldV2ClosedConstructionResultKind.Invalid =>
                    DumpExpressionTypeConstructionOutcome.Invalid,
                StaticFieldV2ClosedConstructionResultKind.Unsupported =>
                    DumpExpressionTypeConstructionOutcome.Unsupported,
                _ => DumpExpressionTypeConstructionOutcome.Partial,
            };

        private static DumpExpressionMemberLookupOutcome MapMemberLookup(
            StaticFieldV2MemberLookupResultKind resultKind) => resultKind switch
            {
                StaticFieldV2MemberLookupResultKind.Absent => DumpExpressionMemberLookupOutcome.Absent,
                StaticFieldV2MemberLookupResultKind.Ambiguous => DumpExpressionMemberLookupOutcome.Ambiguous,
                StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember =>
                    DumpExpressionMemberLookupOutcome.HiddenByUnsupportedMember,
                StaticFieldV2MemberLookupResultKind.Invalid => DumpExpressionMemberLookupOutcome.Invalid,
                StaticFieldV2MemberLookupResultKind.Unsupported => DumpExpressionMemberLookupOutcome.Unsupported,
                _ => DumpExpressionMemberLookupOutcome.Partial,
            };

        private static DumpExpressionMemberLookupOutcome MapBareRoot(
            StaticFieldV2BareRootResultKind resultKind) => resultKind switch
            {
                StaticFieldV2BareRootResultKind.Absent => DumpExpressionMemberLookupOutcome.Absent,
                StaticFieldV2BareRootResultKind.Ambiguous => DumpExpressionMemberLookupOutcome.Ambiguous,
                StaticFieldV2BareRootResultKind.HiddenByUnsupportedMember =>
                    DumpExpressionMemberLookupOutcome.HiddenByUnsupportedMember,
                StaticFieldV2BareRootResultKind.Invalid => DumpExpressionMemberLookupOutcome.Invalid,
                StaticFieldV2BareRootResultKind.Unsupported => DumpExpressionMemberLookupOutcome.Unsupported,
                _ => DumpExpressionMemberLookupOutcome.Partial,
            };

        private static DumpExpressionLexicalCompletenessOutcome MapCertificate(
            StaticFieldV2LexicalCertificateResultKind resultKind) => resultKind switch
            {
                StaticFieldV2LexicalCertificateResultKind.Complete =>
                    DumpExpressionLexicalCompletenessOutcome.Complete,
                StaticFieldV2LexicalCertificateResultKind.Shadowed =>
                    DumpExpressionLexicalCompletenessOutcome.Shadowed,
                StaticFieldV2LexicalCertificateResultKind.Unsupported =>
                    DumpExpressionLexicalCompletenessOutcome.Unsupported,
                StaticFieldV2LexicalCertificateResultKind.Invalid =>
                    DumpExpressionLexicalCompletenessOutcome.Unsupported,
                _ => DumpExpressionLexicalCompletenessOutcome.Partial,
            };

        private static DumpExpressionStorageOutcome MapStorageStrategyStop(
            StaticFieldV2StorageStrategyResultKind resultKind) => resultKind switch
            {
                StaticFieldV2StorageStrategyResultKind.Exact => DumpExpressionStorageOutcome.Exact,
                _ => DumpExpressionStorageOutcome.Unsupported,
            };

        private StaticFieldV2ExpressionResult ProfileRejection() =>
            Project(
                DumpExpressionV2OutcomeAxes.Create(
                    DumpExpressionSyntaxStatus.Unsupported,
                    DumpExpressionContextOutcome.NotReached,
                    DumpExpressionRootAttributionOutcome.NotReached,
                    DumpExpressionLexicalCompletenessOutcome.NotReached,
                    DumpExpressionTypeBindingOutcome.NotReached,
                    DumpExpressionTypeConstructionOutcome.NotReached,
                    DumpExpressionMemberLookupOutcome.NotReached,
                    DumpExpressionRuntimeConstructionOutcome.NotReached,
                    DumpExpressionStorageOutcome.NotReached,
                    DumpExpressionValueOutcome.NotReached,
                    DumpExpressionSuffixOutcome.NotReached,
                    DumpExpressionCompletenessOutcome.NoAnswer));

        private StaticFieldV2ExpressionResult SyntaxStop(DumpExpressionSyntaxStatus status) =>
            Project(
                DumpExpressionV2OutcomeAxes.Create(
                    status,
                    DumpExpressionContextOutcome.NotReached,
                    DumpExpressionRootAttributionOutcome.NotReached,
                    DumpExpressionLexicalCompletenessOutcome.NotReached,
                    DumpExpressionTypeBindingOutcome.NotReached,
                    DumpExpressionTypeConstructionOutcome.NotReached,
                    DumpExpressionMemberLookupOutcome.NotReached,
                    DumpExpressionRuntimeConstructionOutcome.NotReached,
                    DumpExpressionStorageOutcome.NotReached,
                    DumpExpressionValueOutcome.NotReached,
                    DumpExpressionSuffixOutcome.NotReached,
                    DumpExpressionCompletenessOutcome.NoAnswer));

        private StaticFieldV2ExpressionResult ContextStop(DumpExpressionContextOutcome context) =>
            Compose(
                context,
                DumpExpressionLexicalCompletenessOutcome.NotReached,
                DumpExpressionTypeBindingOutcome.NotReached,
                DumpExpressionTypeConstructionOutcome.NotReached,
                DumpExpressionMemberLookupOutcome.NotReached,
                DumpExpressionRuntimeConstructionOutcome.NotReached,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult LexicalStop(DumpExpressionLexicalCompletenessOutcome lexical) =>
            Compose(
                ContextAxis(),
                lexical,
                DumpExpressionTypeBindingOutcome.NotReached,
                DumpExpressionTypeConstructionOutcome.NotReached,
                DumpExpressionMemberLookupOutcome.NotReached,
                DumpExpressionRuntimeConstructionOutcome.NotReached,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult TypeBindingStop(DumpExpressionTypeBindingOutcome binding) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                binding,
                DumpExpressionTypeConstructionOutcome.NotReached,
                DumpExpressionMemberLookupOutcome.NotReached,
                DumpExpressionRuntimeConstructionOutcome.NotReached,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult ConstructionStop(
            DumpExpressionTypeConstructionOutcome construction) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                TypeBindingAxis(),
                construction,
                DumpExpressionMemberLookupOutcome.NotReached,
                DumpExpressionRuntimeConstructionOutcome.NotReached,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult MemberStop(DumpExpressionMemberLookupOutcome member) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                TypeBindingAxis(),
                ConstructionAxis(),
                member,
                DumpExpressionRuntimeConstructionOutcome.NotReached,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult RuntimeStop(DumpExpressionRuntimeConstructionOutcome runtime) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                TypeBindingAxis(),
                ConstructionAxis(),
                DumpExpressionMemberLookupOutcome.Exact,
                runtime,
                DumpExpressionStorageOutcome.NotReached,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult StorageStop(DumpExpressionStorageOutcome storage) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                TypeBindingAxis(),
                ConstructionAxis(),
                DumpExpressionMemberLookupOutcome.Exact,
                RuntimeAxis(),
                storage,
                DumpExpressionValueOutcome.NotReached,
                DumpExpressionSuffixOutcome.NotReached);

        private StaticFieldV2ExpressionResult Complete(
            DumpExpressionValueOutcome value,
            DumpExpressionSuffixOutcome suffixOutcome) =>
            Compose(
                ContextAxis(),
                LexicalAxis(),
                TypeBindingAxis(),
                ConstructionAxis(),
                DumpExpressionMemberLookupOutcome.Exact,
                RuntimeAxis(),
                StorageAxis(),
                value,
                suffixOutcome);

        private DumpExpressionContextOutcome ContextAxis() =>
            route == StaticFieldV2ExpressionRoute.ExplicitMetadataGlobal
                ? DumpExpressionContextOutcome.NotRequired
                : DumpExpressionContextOutcome.Exact;

        private DumpExpressionLexicalCompletenessOutcome LexicalAxis() =>
            route == StaticFieldV2ExpressionRoute.BareStaticMember
                ? DumpExpressionLexicalCompletenessOutcome.Complete
                : DumpExpressionLexicalCompletenessOutcome.NotRequired;

        private DumpExpressionTypeBindingOutcome TypeBindingAxis() =>
            route == StaticFieldV2ExpressionRoute.BareStaticMember
                ? DumpExpressionTypeBindingOutcome.NotRequired
                : DumpExpressionTypeBindingOutcome.Exact;

        private DumpExpressionTypeConstructionOutcome ConstructionAxis() =>
            ownerConstruction is not null
                ? DumpExpressionTypeConstructionOutcome.Exact
                : DumpExpressionTypeConstructionOutcome.NotRequired;

        private DumpExpressionRuntimeConstructionOutcome RuntimeAxis() =>
            runtimeConstruction is { ResultKind: StaticFieldV2RuntimeConstructionSelectionKind.Exact }
                ? DumpExpressionRuntimeConstructionOutcome.Exact
                : DumpExpressionRuntimeConstructionOutcome.NotRequired;

        private DumpExpressionStorageOutcome StorageAxis() =>
            staticSlot is { ResultKind: StaticFieldV2StaticSlotResultKind.Exact }
                ? DumpExpressionStorageOutcome.Exact
                : DumpExpressionStorageOutcome.NotRequired;

        private StaticFieldV2ExpressionResult Compose(
            DumpExpressionContextOutcome context,
            DumpExpressionLexicalCompletenessOutcome lexical,
            DumpExpressionTypeBindingOutcome binding,
            DumpExpressionTypeConstructionOutcome construction,
            DumpExpressionMemberLookupOutcome member,
            DumpExpressionRuntimeConstructionOutcome runtime,
            DumpExpressionStorageOutcome storage,
            DumpExpressionValueOutcome value,
            DumpExpressionSuffixOutcome suffixOutcome)
        {
            var rootAttribution = DumpExpressionRootAttributionOutcome.NotRequired;
            var stages = new[]
            {
                Continues(context is DumpExpressionContextOutcome.NotRequired or DumpExpressionContextOutcome.Exact,
                    context == DumpExpressionContextOutcome.Partial),
                Continues(true, false),
                Continues(
                    lexical is DumpExpressionLexicalCompletenessOutcome.NotRequired or
                        DumpExpressionLexicalCompletenessOutcome.Complete,
                    lexical == DumpExpressionLexicalCompletenessOutcome.Partial),
                Continues(
                    binding is DumpExpressionTypeBindingOutcome.NotRequired or DumpExpressionTypeBindingOutcome.Exact,
                    binding == DumpExpressionTypeBindingOutcome.Partial),
                Continues(
                    construction is DumpExpressionTypeConstructionOutcome.NotRequired or
                        DumpExpressionTypeConstructionOutcome.Exact,
                    construction == DumpExpressionTypeConstructionOutcome.Partial),
                Continues(
                    member is DumpExpressionMemberLookupOutcome.NotRequired or
                        DumpExpressionMemberLookupOutcome.Exact,
                    member == DumpExpressionMemberLookupOutcome.Partial),
                Continues(
                    runtime is DumpExpressionRuntimeConstructionOutcome.NotRequired or
                        DumpExpressionRuntimeConstructionOutcome.Exact,
                    runtime == DumpExpressionRuntimeConstructionOutcome.Partial),
                Continues(
                    storage is DumpExpressionStorageOutcome.NotRequired or DumpExpressionStorageOutcome.Exact,
                    storage == DumpExpressionStorageOutcome.Partial),
            };

            var stopIndex = -1;
            var partialStop = false;
            for (var index = 0; index < stages.Length; index++)
            {
                if (stages[index].CanContinue)
                {
                    continue;
                }
                stopIndex = index;
                partialStop = stages[index].Partial;
                break;
            }

            // Every stage after the first typed stop is forced NotReached, so no stop can expose a partial plan.
            if (stopIndex >= 0)
            {
                if (stopIndex < 1)
                {
                    rootAttribution = DumpExpressionRootAttributionOutcome.NotReached;
                }
                if (stopIndex < 2)
                {
                    lexical = DumpExpressionLexicalCompletenessOutcome.NotReached;
                }
                if (stopIndex < 3)
                {
                    binding = DumpExpressionTypeBindingOutcome.NotReached;
                }
                if (stopIndex < 4)
                {
                    construction = DumpExpressionTypeConstructionOutcome.NotReached;
                }
                if (stopIndex < 5)
                {
                    member = DumpExpressionMemberLookupOutcome.NotReached;
                }
                if (stopIndex < 6)
                {
                    runtime = DumpExpressionRuntimeConstructionOutcome.NotReached;
                }
                if (stopIndex < 7)
                {
                    storage = DumpExpressionStorageOutcome.NotReached;
                }
            }

            var completeness = stopIndex >= 0
                ? partialStop ? DumpExpressionCompletenessOutcome.Partial : DumpExpressionCompletenessOutcome.NoAnswer
                : ValueCompleteness(value, suffixOutcome);
            var effectiveValue = stopIndex >= 0 ? DumpExpressionValueOutcome.NotReached : value;
            var effectiveSuffix = stopIndex >= 0 ? DumpExpressionSuffixOutcome.NotReached : suffixOutcome;

            return Project(
                DumpExpressionV2OutcomeAxes.Create(
                    DumpExpressionSyntaxStatus.Admitted,
                    context,
                    rootAttribution,
                    lexical,
                    binding,
                    construction,
                    member,
                    runtime,
                    storage,
                    effectiveValue,
                    effectiveSuffix,
                    completeness));
        }

        private static DumpExpressionCompletenessOutcome ValueCompleteness(
            DumpExpressionValueOutcome value,
            DumpExpressionSuffixOutcome suffixOutcome) => value switch
            {
                DumpExpressionValueOutcome.ExactValue or DumpExpressionValueOutcome.ExactNull =>
                    suffixOutcome is DumpExpressionSuffixOutcome.NotRequested or DumpExpressionSuffixOutcome.Completed
                        ? DumpExpressionCompletenessOutcome.Complete
                        : DumpExpressionCompletenessOutcome.NoAnswer,
                DumpExpressionValueOutcome.Partial => DumpExpressionCompletenessOutcome.Partial,
                _ => DumpExpressionCompletenessOutcome.NoAnswer,
            };

        private static (bool CanContinue, bool Partial) Continues(bool canContinue, bool partial) =>
            (canContinue, partial);

        private StaticFieldV2ExpressionResult Project(DumpExpressionV2OutcomeAxes axes) =>
            new(
                request,
                axes,
                route,
                new StaticFieldV2ExpressionProvenance(
                    request.ExpressionText,
                    request.Profile,
                    syntax,
                    route,
                    scopedContext,
                    contextualBinding,
                    explicitNameBinding,
                    ownerConstruction,
                    lexicalCertificate,
                    bareRoot,
                    memberLookup,
                    storageStrategy,
                    runtimeConstruction,
                    staticSlot,
                    rawValueBytes,
                    literalConstant,
                    literalValue,
                    runtimeValue,
                    referenceTargetValidation,
                    suffix,
                    suffixValue,
                    StaticFieldV2PipelineEvidenceLedger.Issue(
                        scopedContextCalls,
                        lexicalEnvelopeCalls,
                        constantRowCalls,
                        constructionCandidateCalls,
                        slotFactCalls,
                        memoryReadCalls,
                        suffixEvaluationCalls,
                        frameRootEvaluationCalls),
                    capabilityCallLedger,
                    frameRoot,
                    [.. boundaries]));
    }
}
