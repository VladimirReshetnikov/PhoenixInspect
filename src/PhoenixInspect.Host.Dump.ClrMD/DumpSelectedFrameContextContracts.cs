using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Classifies the precision of one independently acquired debugger binding-context source.
/// </summary>
/// <remarks>
/// Numeric values are an explicit replay contract. An exact observation is the only status that may expose a
/// candidate-producing payload; every other status retains source identity and reached bounds without a candidate.
/// </remarks>
public enum DumpContextEvidenceStatus : byte
{
    /// <summary>The complete, internally consistent source fact was acquired.</summary>
    Exact = 1,

    /// <summary>The source was reached, but a bound or incomplete observation prevented exact projection.</summary>
    Partial = 2,

    /// <summary>The source or required prerequisite was not available in the immutable snapshot.</summary>
    Unavailable = 3,

    /// <summary>More than one source candidate remained and no candidate was selected by enumeration order.</summary>
    Ambiguous = 4,

    /// <summary>Independently acquired identities disagreed.</summary>
    Conflict = 5,

    /// <summary>The observed source violated a structural invariant.</summary>
    Invalid = 6,

    /// <summary>The source was well formed but used a deliberately unimplemented representation.</summary>
    Unsupported = 7,
}

/// <summary>
/// Identifies the stable first boundary that determined a debugger binding-context observation.
/// </summary>
/// <remarks>
/// These numeric tags are persisted in canonical identities. They describe evidence disposition and never contain an
/// exception message, local path, reader object, or other analysis-machine state.
/// </remarks>
public enum DumpContextEvidenceIssue : ushort
{
    /// <summary>No issue applies; this value is reserved for exact evidence.</summary>
    None = 0,

    /// <summary>A configured traversal or byte bound was reached before the next source operation.</summary>
    BoundReached = 1,

    /// <summary>The source returned an incomplete counted observation.</summary>
    SourceIncomplete = 2,

    /// <summary>A preceding source required by this producer was unavailable.</summary>
    PrerequisiteUnavailable = 3,

    /// <summary>The requested managed frame was not present under the applied bounds.</summary>
    FrameUnavailable = 4,

    /// <summary>The native instruction location could not be correlated exactly with one IL location.</summary>
    InstructionLocationUnavailable = 5,

    /// <summary>The selected runtime frame could not be correlated with one managed module and MethodDef.</summary>
    ModuleCorrelationUnavailable = 6,

    /// <summary>No exact Portable-PDB debug-directory identity was available for the selected module.</summary>
    PortablePdbDebugIdentityUnavailable = 7,

    /// <summary>No candidate Portable PDB artifact was available under the artifact bounds.</summary>
    PortablePdbUnavailable = 8,

    /// <summary>No exact active Portable-PDB scope could be established.</summary>
    ScopeUnavailable = 9,

    /// <summary>More than one managed frame satisfied the selector.</summary>
    FrameAmbiguous = 10,

    /// <summary>More than one identity-matching Portable PDB artifact remained.</summary>
    PortablePdbAmbiguous = 11,

    /// <summary>More than one incompatible active scope projection remained.</summary>
    ScopeAmbiguous = 12,

    /// <summary>The module CodeView identity and candidate Portable-PDB content identifier disagreed.</summary>
    PortablePdbIdentityMismatch = 13,

    /// <summary>A source unexpectedly referred to another immutable dump snapshot.</summary>
    SnapshotMismatch = 14,

    /// <summary>Frame, module, or metadata-content evidence unexpectedly referred to another module.</summary>
    ModuleMismatch = 15,

    /// <summary>The requested thread or frame ordinal was structurally invalid.</summary>
    InvalidFrameSelector = 16,

    /// <summary>The selected-frame payload was structurally invalid.</summary>
    InvalidFrame = 17,

    /// <summary>The module debug-directory payload was structurally invalid.</summary>
    InvalidModuleDebugIdentity = 18,

    /// <summary>The candidate Portable-PDB bytes were malformed.</summary>
    InvalidPortablePdb = 19,

    /// <summary>A Portable-PDB scope or import payload was malformed.</summary>
    InvalidScope = 20,

    /// <summary>The runtime frame kind is deliberately outside the selected-frame producer.</summary>
    UnsupportedFrame = 21,

    /// <summary>The Portable-PDB representation is deliberately outside the current reader.</summary>
    UnsupportedPortablePdb = 22,

    /// <summary>The active scope representation is deliberately outside the current reader.</summary>
    UnsupportedScope = 23,

    /// <summary>No non-hidden sequence point maps the selected IL offset to a source line.</summary>
    SequencePointsUnavailable = 24,

    /// <summary>The mapped sequence point does not name an exact source document.</summary>
    DocumentUnavailable = 25,
}

/// <summary>
/// Selects one managed stack frame by bounded zero-based thread and frame ordinals in one immutable snapshot.
/// </summary>
/// <remarks>
/// The selector contains no process-global current-thread state. A producer must enumerate the named snapshot under
/// separately recorded bounds and must not reinterpret an ordinal as a runtime address or operating-system thread id.
/// </remarks>
public sealed class DumpSelectedFrameSelector : IEquatable<DumpSelectedFrameSelector>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedFrameSelector(
        ClrmdSnapshotIdentity snapshot,
        int threadOrdinal,
        int frameOrdinal)
    {
        Snapshot = snapshot;
        ThreadOrdinal = threadOrdinal;
        FrameOrdinal = frameOrdinal;

        var writer = new CanonicalReplayEncoding.Writer("dump-selected-frame-selector", 1);
        DumpContextContractEncoding.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(threadOrdinal);
        writer.WriteInt32(frameOrdinal);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the immutable dump snapshot in which the requested frame must be selected.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the zero-based ordinal in the producer's bounded managed-thread enumeration.</summary>
    public int ThreadOrdinal { get; }

    /// <summary>Gets the zero-based ordinal in the selected thread's bounded managed-frame enumeration.</summary>
    public int FrameOrdinal { get; }

    /// <summary>Gets a defensive copy of the versioned, path-independent canonical selector bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a validated snapshot-scoped selected-frame request.</summary>
    /// <param name="snapshot">The exact immutable dump identity to which both ordinals apply.</param>
    /// <param name="threadOrdinal">A non-negative managed-thread enumeration ordinal.</param>
    /// <param name="frameOrdinal">A non-negative managed-frame enumeration ordinal.</param>
    /// <returns>A defensively immutable selector suitable for bounded acquisition and replay.</returns>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> is the default identity.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threadOrdinal"/> or <paramref name="frameOrdinal"/> is negative.
    /// </exception>
    public static DumpSelectedFrameSelector Create(
        ClrmdSnapshotIdentity snapshot,
        int threadOrdinal,
        int frameOrdinal)
    {
        DumpContextContractEncoding.ValidateSnapshot(snapshot, nameof(snapshot));
        if (threadOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threadOrdinal), "A thread ordinal cannot be negative.");
        }

        if (frameOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameOrdinal), "A frame ordinal cannot be negative.");
        }

        return new DumpSelectedFrameSelector(snapshot, threadOrdinal, frameOrdinal);
    }

    /// <summary>Determines content equality from canonical bytes rather than object identity.</summary>
    /// <param name="other">The selector to compare.</param>
    /// <returns><see langword="true"/> when every replay-significant field is equal.</returns>
    public bool Equals(DumpSelectedFrameSelector? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedFrameSelector);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Identifies one exact native-instruction-pointer to IL-offset correlation for a selected managed frame.
/// </summary>
/// <remarks>
/// This value never substitutes a nearest sequence point, a method-start fallback, or a decompiler estimate.
/// Absence of one exact mapping is represented by a non-exact frame observation rather than an approximate instance.
/// </remarks>
public sealed class DumpInstructionLocation : IEquatable<DumpInstructionLocation>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpInstructionLocation(ulong nativeInstructionPointer, int ilOffset)
    {
        NativeInstructionPointer = nativeInstructionPointer;
        IlOffset = ilOffset;

        var writer = new CanonicalReplayEncoding.Writer("dump-instruction-location", 1);
        writer.WriteUInt64(nativeInstructionPointer);
        writer.WriteInt32(ilOffset);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the nonzero target instruction pointer observed for the selected frame.</summary>
    public ulong NativeInstructionPointer { get; }

    /// <summary>Gets the exact non-negative IL offset correlated with the native instruction pointer.</summary>
    public int IlOffset { get; }

    /// <summary>Gets a defensive copy of the canonical exact-location bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact native-to-IL instruction location.</summary>
    /// <param name="nativeInstructionPointer">The nonzero target native instruction pointer.</param>
    /// <param name="ilOffset">The exact non-negative offset in the selected MethodDef body.</param>
    /// <returns>A validated exact instruction location.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="nativeInstructionPointer"/> is zero or <paramref name="ilOffset"/> is negative.
    /// </exception>
    public static DumpInstructionLocation Create(ulong nativeInstructionPointer, int ilOffset)
    {
        if (nativeInstructionPointer == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeInstructionPointer),
                "An exact instruction location requires a nonzero target instruction pointer.");
        }

        if (ilOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ilOffset), "An IL offset cannot be negative.");
        }

        return new DumpInstructionLocation(nativeInstructionPointer, ilOffset);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The instruction location to compare.</param>
    /// <returns><see langword="true"/> when the native pointer and IL offset are equal.</returns>
    public bool Equals(DumpInstructionLocation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpInstructionLocation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Freezes one exact selected managed frame and its counted metadata correlation.
/// </summary>
/// <remarks>
/// Identity includes the immutable snapshot, selector, runtime thread/frame addresses, runtime module, complete
/// metadata-content identity, MethodDef, declaring TypeDef, declaring namespace, and exact instruction location. It
/// deliberately excludes display names, source paths, live ClrMD objects, readers, and caught exceptions.
/// </remarks>
public sealed class DumpSelectedFrameIdentity : IEquatable<DumpSelectedFrameIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedFrameIdentity(
        DumpSelectedFrameSelector selector,
        uint managedThreadId,
        ulong runtimeThreadAddress,
        ulong stackPointer,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int methodDefinitionToken,
        int declaringTypeDefinitionToken,
        string declaringNamespace,
        DumpInstructionLocation instruction)
    {
        Selector = selector;
        ManagedThreadId = managedThreadId;
        RuntimeThreadAddress = runtimeThreadAddress;
        StackPointer = stackPointer;
        RuntimeModule = runtimeModule;
        ModuleContent = moduleContent;
        MethodDefinitionToken = methodDefinitionToken;
        DeclaringTypeDefinitionToken = declaringTypeDefinitionToken;
        DeclaringNamespace = declaringNamespace;
        Instruction = instruction;

        var writer = new CanonicalReplayEncoding.Writer("dump-selected-frame-identity", 1);
        writer.WriteLengthPrefixedBytes(selector.CanonicalBytes.AsSpan());
        writer.WriteUInt32(managedThreadId);
        writer.WriteUInt64(runtimeThreadAddress);
        writer.WriteUInt64(stackPointer);
        DumpContextContractEncoding.WriteRuntimeModule(writer, runtimeModule);
        DumpContextContractEncoding.WriteModuleContent(writer, moduleContent);
        writer.WriteInt32(methodDefinitionToken);
        writer.WriteInt32(declaringTypeDefinitionToken);
        writer.WriteString(declaringNamespace);
        writer.WriteLengthPrefixedBytes(instruction.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the snapshot-scoped thread/frame selector satisfied by this identity.</summary>
    public DumpSelectedFrameSelector Selector { get; }

    /// <summary>Gets the nonzero runtime managed-thread identifier observed for the selected thread.</summary>
    public uint ManagedThreadId { get; }

    /// <summary>Gets the nonzero target address of the selected runtime thread structure.</summary>
    public ulong RuntimeThreadAddress { get; }

    /// <summary>Gets the nonzero target stack pointer associated with the selected frame.</summary>
    public ulong StackPointer { get; }

    /// <summary>Gets the snapshot-scoped runtime module containing the selected MethodDef.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the complete counted metadata-content identity for <see cref="RuntimeModule"/>.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the non-nil MethodDef token correlated with the runtime frame.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the non-nil declaring TypeDef token read from the same counted metadata.</summary>
    public int DeclaringTypeDefinitionToken { get; }

    /// <summary>Gets the exact declaring metadata namespace; an empty string denotes the global namespace.</summary>
    public string DeclaringNamespace { get; }

    /// <summary>Gets the exact native instruction pointer and IL offset correlation.</summary>
    public DumpInstructionLocation Instruction { get; }

    /// <summary>Gets a defensive copy of the versioned selected-frame identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a validated exact selected-frame identity.</summary>
    /// <param name="selector">The exact selector satisfied by this frame.</param>
    /// <param name="managedThreadId">The nonzero runtime managed-thread identifier.</param>
    /// <param name="runtimeThreadAddress">The nonzero target runtime-thread address.</param>
    /// <param name="stackPointer">The nonzero target stack pointer for this frame.</param>
    /// <param name="runtimeModule">The snapshot-scoped runtime module containing the method.</param>
    /// <param name="moduleContent">The complete counted metadata identity for that module.</param>
    /// <param name="methodDefinitionToken">A non-nil MethodDef token.</param>
    /// <param name="declaringTypeDefinitionToken">A non-nil TypeDef token.</param>
    /// <param name="declaringNamespace">The exact metadata namespace, or an empty string for global namespace.</param>
    /// <param name="instruction">The exact native-to-IL instruction correlation.</param>
    /// <returns>An immutable, content-equal selected-frame identity.</returns>
    /// <exception cref="ArgumentException">
    /// Snapshot/module identities disagree, metadata tokens use the wrong tables, or the namespace is invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A required runtime thread, stack, or managed-thread identity is zero.
    /// </exception>
    public static DumpSelectedFrameIdentity Create(
        DumpSelectedFrameSelector selector,
        uint managedThreadId,
        ulong runtimeThreadAddress,
        ulong stackPointer,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int methodDefinitionToken,
        int declaringTypeDefinitionToken,
        string declaringNamespace,
        DumpInstructionLocation instruction)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(moduleContent);
        ArgumentNullException.ThrowIfNull(instruction);
        DumpContextContractEncoding.ValidateRuntimeModule(runtimeModule, nameof(runtimeModule));
        if (selector.Snapshot != runtimeModule.Snapshot)
        {
            throw new ArgumentException(
                "The selected frame and runtime module must identify the same dump snapshot.",
                nameof(runtimeModule));
        }

        if (managedThreadId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(managedThreadId), "A managed thread id cannot be zero.");
        }

        if (runtimeThreadAddress == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeThreadAddress),
                "A runtime thread address cannot be zero.");
        }

        if (stackPointer == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stackPointer), "A selected-frame stack pointer cannot be zero.");
        }

        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            declaringTypeDefinitionToken,
            0x02,
            nameof(declaringTypeDefinitionToken));
        DumpContextContractEncoding.ValidateNamespace(declaringNamespace, nameof(declaringNamespace));

        return new DumpSelectedFrameIdentity(
            selector,
            managedThreadId,
            runtimeThreadAddress,
            stackPointer,
            runtimeModule,
            moduleContent,
            methodDefinitionToken,
            declaringTypeDefinitionToken,
            declaringNamespace,
            instruction);
    }

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The selected-frame identity to compare.</param>
    /// <returns><see langword="true"/> when every replay-significant identity field is equal.</returns>
    public bool Equals(DumpSelectedFrameIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedFrameIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>
/// Represents the complete disposition of bounded selected-frame acquisition.
/// </summary>
/// <remarks>
/// Only <see cref="DumpContextEvidenceStatus.Exact"/> carries <see cref="Frame"/>. Non-exact observations preserve
/// the selector, typed issue, and reached bounds while making exact candidate expansion impossible through the API.
/// </remarks>
public sealed class DumpSelectedFrameObservation : IEquatable<DumpSelectedFrameObservation>
{
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedFrameObservation(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameSelector selector,
        DumpSelectedFrameIdentity? frame,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        DumpContextContractEncoding.ValidateStatusIssue(status, issue);
        DumpContextContractEncoding.ValidateSelectedFrameStatusIssue(status, issue);
        if ((status == DumpContextEvidenceStatus.Exact) != (frame is not null))
        {
            throw new ArgumentException("Exactly the Exact status must carry a selected-frame payload.", nameof(frame));
        }

        if (frame is not null && !frame.Selector.Equals(selector))
        {
            throw new ArgumentException("The exact frame must satisfy the observation selector.", nameof(frame));
        }

        Status = status;
        Issue = issue;
        Selector = selector;
        Frame = frame;
        this.reachedBounds = CanonicalReplayEncoding.Copy(reachedBounds);

        var writer = new CanonicalReplayEncoding.Writer("dump-selected-frame-observation", 1);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(selector.CanonicalBytes.AsSpan());
        DumpContextContractEncoding.WriteBounds(writer, reachedBounds);
        writer.WriteBoolean(frame is not null);
        if (frame is not null)
        {
            writer.WriteLengthPrefixedBytes(frame.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact, partial, unavailable, ambiguous, conflict, invalid, or unsupported status.</summary>
    public DumpContextEvidenceStatus Status { get; }

    /// <summary>Gets the stable first-boundary issue; exact evidence always uses <c>None</c>.</summary>
    public DumpContextEvidenceIssue Issue { get; }

    /// <summary>Gets the snapshot-scoped selector attempted by the producer.</summary>
    public DumpSelectedFrameSelector Selector { get; }

    /// <summary>Gets the exact frame only when <see cref="Status"/> is <c>Exact</c>; otherwise gets null.</summary>
    public DumpSelectedFrameIdentity? Frame { get; }

    /// <summary>Gets whether this observation carries one complete selected-frame payload.</summary>
    public bool HasExactFrame => Frame is not null;

    /// <summary>
    /// Gets a defensive copy of only the deterministic bounds reached during selected-frame acquisition.
    /// </summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        CanonicalReplayEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact observation carrying one complete frame.</summary>
    /// <param name="frame">The complete exact selected-frame identity.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An exact observation whose issue is <c>None</c>.</returns>
    public static DumpSelectedFrameObservation Exact(
        DumpSelectedFrameIdentity frame,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return Create(
            DumpContextEvidenceStatus.Exact,
            DumpContextEvidenceIssue.None,
            frame.Selector,
            frame,
            reachedBounds);
    }

    /// <summary>Creates a partial frame observation without an exact candidate.</summary>
    /// <param name="selector">The selector whose bounded acquisition was incomplete.</param>
    /// <param name="issue">A partial-status issue such as a reached bound or incomplete source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A partial observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Partial(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Partial, issue, selector, null, reachedBounds);

    /// <summary>Creates an unavailable frame observation without an exact candidate.</summary>
    /// <param name="selector">The selector whose requested source was unavailable.</param>
    /// <param name="issue">An unavailable-status issue naming the first missing source.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unavailable observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Unavailable(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unavailable, issue, selector, null, reachedBounds);

    /// <summary>Creates an ambiguous frame observation without selecting the first candidate.</summary>
    /// <param name="selector">The selector for which multiple candidates remained.</param>
    /// <param name="issue">An ambiguity issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An ambiguous observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Ambiguous(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Ambiguous, issue, selector, null, reachedBounds);

    /// <summary>Creates a conflicting frame observation without exposing either candidate as exact.</summary>
    /// <param name="selector">The selector associated with the conflicting evidence.</param>
    /// <param name="issue">A conflict-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>A conflict observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Conflict(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Conflict, issue, selector, null, reachedBounds);

    /// <summary>Creates an invalid frame observation without exposing malformed evidence.</summary>
    /// <param name="selector">The selector associated with the invalid source.</param>
    /// <param name="issue">An invalid-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An invalid observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Invalid(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Invalid, issue, selector, null, reachedBounds);

    /// <summary>Creates an unsupported frame observation without manufacturing a candidate.</summary>
    /// <param name="selector">The selector associated with the unsupported frame representation.</param>
    /// <param name="issue">An unsupported-status issue.</param>
    /// <param name="reachedBounds">An initialized set of reached bounds; default is rejected.</param>
    /// <returns>An unsupported observation carrying no frame payload.</returns>
    public static DumpSelectedFrameObservation Unsupported(
        DumpSelectedFrameSelector selector,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unsupported, issue, selector, null, reachedBounds);

    /// <summary>Determines content equality from canonical bytes.</summary>
    /// <param name="other">The selected-frame observation to compare.</param>
    /// <returns><see langword="true"/> when status, source, bounds, and exact payload are equal.</returns>
    public bool Equals(DumpSelectedFrameObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedFrameObservation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static DumpSelectedFrameObservation Create(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameSelector selector,
        DumpSelectedFrameIdentity? frame,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        return new DumpSelectedFrameObservation(status, issue, selector, frame, normalizedBounds);
    }
}

internal static class DumpContextContractEncoding
{
    private const int MaximumContextTextLength = 4_096;

    internal static void ValidateSnapshot(ClrmdSnapshotIdentity snapshot, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Sha256))
        {
            throw new ArgumentException("A non-default immutable dump snapshot identity is required.", parameterName);
        }
    }

    internal static void ValidateRuntimeModule(ClrmdRuntimeModuleIdentity module, string parameterName)
    {
        ValidateSnapshot(module.Snapshot, parameterName);
        if (module.ModuleAddress == 0)
        {
            throw new ArgumentException("A runtime module identity requires a nonzero CLR module address.", parameterName);
        }
    }

    internal static void ValidateNamespace(string value, string parameterName) =>
        ValidateContextText(value, parameterName, allowEmpty: true);

    internal static void ValidateRequiredText(string value, string parameterName) =>
        ValidateContextText(value, parameterName, allowEmpty: false);

    internal static void ValidateAlias(string value, string parameterName)
    {
        ValidateRequiredText(value, parameterName);
    }

    internal static void WriteSnapshot(CanonicalReplayEncoding.Writer writer, ClrmdSnapshotIdentity snapshot) =>
        writer.WriteSha256(snapshot.Sha256, nameof(snapshot));

    internal static void WriteRuntimeModule(
        CanonicalReplayEncoding.Writer writer,
        ClrmdRuntimeModuleIdentity module)
    {
        WriteSnapshot(writer, module.Snapshot);
        writer.WriteUInt64(module.AppDomainAddress);
        writer.WriteUInt64(module.ModuleAddress);
        writer.WriteUInt64(module.ImageBase);
        writer.WriteUInt64(module.ImageSize);
    }

    internal static void WriteModuleContent(
        CanonicalReplayEncoding.Writer writer,
        ModuleContentIdentity moduleContent)
    {
        writer.WriteRawBytes(moduleContent.Mvid.ToByteArray());
        writer.WriteInt32(moduleContent.MetadataLength);
        writer.WriteSha256(moduleContent.MetadataSha256, nameof(moduleContent));
    }

    internal static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    internal static void ValidateStatusIssue(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (!Enum.IsDefined(issue))
        {
            throw new ArgumentOutOfRangeException(nameof(issue));
        }

        var valid = status switch
        {
            DumpContextEvidenceStatus.Exact => issue == DumpContextEvidenceIssue.None,
            DumpContextEvidenceStatus.Partial => issue is
                DumpContextEvidenceIssue.BoundReached or
                DumpContextEvidenceIssue.SourceIncomplete or
                DumpContextEvidenceIssue.InstructionLocationUnavailable,
            DumpContextEvidenceStatus.Unavailable => issue is
                DumpContextEvidenceIssue.PrerequisiteUnavailable or
                DumpContextEvidenceIssue.FrameUnavailable or
                DumpContextEvidenceIssue.InstructionLocationUnavailable or
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable or
                DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable or
                DumpContextEvidenceIssue.PortablePdbUnavailable or
                DumpContextEvidenceIssue.ScopeUnavailable or
                DumpContextEvidenceIssue.SequencePointsUnavailable or
                DumpContextEvidenceIssue.DocumentUnavailable,
            DumpContextEvidenceStatus.Ambiguous => issue is
                DumpContextEvidenceIssue.FrameAmbiguous or
                DumpContextEvidenceIssue.PortablePdbAmbiguous or
                DumpContextEvidenceIssue.ScopeAmbiguous,
            DumpContextEvidenceStatus.Conflict => issue is
                DumpContextEvidenceIssue.PortablePdbIdentityMismatch or
                DumpContextEvidenceIssue.SnapshotMismatch or
                DumpContextEvidenceIssue.ModuleMismatch,
            DumpContextEvidenceStatus.Invalid => issue is
                DumpContextEvidenceIssue.InvalidFrameSelector or
                DumpContextEvidenceIssue.InvalidFrame or
                DumpContextEvidenceIssue.InvalidModuleDebugIdentity or
                DumpContextEvidenceIssue.InvalidPortablePdb or
                DumpContextEvidenceIssue.InvalidScope,
            DumpContextEvidenceStatus.Unsupported => issue is
                DumpContextEvidenceIssue.UnsupportedFrame or
                DumpContextEvidenceIssue.UnsupportedPortablePdb or
                DumpContextEvidenceIssue.UnsupportedScope,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Issue {issue} is not valid for context status {status}.",
                nameof(issue));
        }
    }

    internal static void ValidateSelectedFrameStatusIssue(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue)
    {
        var valid = status switch
        {
            DumpContextEvidenceStatus.Exact => issue == DumpContextEvidenceIssue.None,
            DumpContextEvidenceStatus.Partial => issue is
                DumpContextEvidenceIssue.BoundReached or
                DumpContextEvidenceIssue.SourceIncomplete or
                DumpContextEvidenceIssue.InstructionLocationUnavailable,
            DumpContextEvidenceStatus.Unavailable => issue is
                DumpContextEvidenceIssue.FrameUnavailable or
                DumpContextEvidenceIssue.InstructionLocationUnavailable or
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable,
            DumpContextEvidenceStatus.Ambiguous => issue == DumpContextEvidenceIssue.FrameAmbiguous,
            DumpContextEvidenceStatus.Conflict => issue is
                DumpContextEvidenceIssue.SnapshotMismatch or
                DumpContextEvidenceIssue.ModuleMismatch,
            DumpContextEvidenceStatus.Invalid => issue is
                DumpContextEvidenceIssue.InvalidFrameSelector or
                DumpContextEvidenceIssue.InvalidFrame,
            DumpContextEvidenceStatus.Unsupported => issue == DumpContextEvidenceIssue.UnsupportedFrame,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Issue {issue} is not meaningful for a selected-frame {status} observation.",
                nameof(issue));
        }
    }

    internal static void ValidatePortablePdbStatusIssue(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue)
    {
        var valid = status switch
        {
            DumpContextEvidenceStatus.Exact => issue == DumpContextEvidenceIssue.None,
            DumpContextEvidenceStatus.Partial => issue is
                DumpContextEvidenceIssue.BoundReached or
                DumpContextEvidenceIssue.SourceIncomplete or
                DumpContextEvidenceIssue.InstructionLocationUnavailable,
            DumpContextEvidenceStatus.Unavailable => issue is
                DumpContextEvidenceIssue.PrerequisiteUnavailable or
                DumpContextEvidenceIssue.InstructionLocationUnavailable or
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable or
                DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable or
                DumpContextEvidenceIssue.PortablePdbUnavailable or
                DumpContextEvidenceIssue.ScopeUnavailable,
            DumpContextEvidenceStatus.Ambiguous => issue is
                DumpContextEvidenceIssue.PortablePdbAmbiguous or
                DumpContextEvidenceIssue.ScopeAmbiguous,
            DumpContextEvidenceStatus.Conflict => issue is
                DumpContextEvidenceIssue.PortablePdbIdentityMismatch or
                DumpContextEvidenceIssue.SnapshotMismatch or
                DumpContextEvidenceIssue.ModuleMismatch,
            DumpContextEvidenceStatus.Invalid => issue is
                DumpContextEvidenceIssue.InvalidModuleDebugIdentity or
                DumpContextEvidenceIssue.InvalidPortablePdb or
                DumpContextEvidenceIssue.InvalidScope,
            DumpContextEvidenceStatus.Unsupported => issue is
                DumpContextEvidenceIssue.UnsupportedPortablePdb or
                DumpContextEvidenceIssue.UnsupportedScope,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Issue {issue} is not meaningful for a Portable-PDB {status} observation.",
                nameof(issue));
        }
    }

    internal static void ValidateFrameSourceStatusIssue(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue)
    {
        var valid = status switch
        {
            DumpContextEvidenceStatus.Exact => issue == DumpContextEvidenceIssue.None,
            DumpContextEvidenceStatus.Partial => issue is
                DumpContextEvidenceIssue.BoundReached or
                DumpContextEvidenceIssue.SourceIncomplete,
            DumpContextEvidenceStatus.Unavailable => issue is
                DumpContextEvidenceIssue.PrerequisiteUnavailable or
                DumpContextEvidenceIssue.ModuleCorrelationUnavailable or
                DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable or
                DumpContextEvidenceIssue.PortablePdbUnavailable or
                DumpContextEvidenceIssue.SequencePointsUnavailable or
                DumpContextEvidenceIssue.DocumentUnavailable,
            DumpContextEvidenceStatus.Ambiguous => issue == DumpContextEvidenceIssue.PortablePdbAmbiguous,
            DumpContextEvidenceStatus.Conflict => issue is
                DumpContextEvidenceIssue.PortablePdbIdentityMismatch or
                DumpContextEvidenceIssue.SnapshotMismatch or
                DumpContextEvidenceIssue.ModuleMismatch,
            DumpContextEvidenceStatus.Invalid => issue is
                DumpContextEvidenceIssue.InvalidModuleDebugIdentity or
                DumpContextEvidenceIssue.InvalidPortablePdb,
            DumpContextEvidenceStatus.Unsupported => issue == DumpContextEvidenceIssue.UnsupportedPortablePdb,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Issue {issue} is not meaningful for a frame-source {status} observation.",
                nameof(issue));
        }
    }

    private static void ValidateContextText(string value, string parameterName, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if ((!allowEmpty && value.Length == 0) ||
            value.Length > MaximumContextTextLength ||
            value.Any(static character => char.IsControl(character)))
        {
            throw new ArgumentException(
                allowEmpty
                    ? $"Context text must be at most {MaximumContextTextLength} non-control UTF-16 code units."
                    : $"Non-empty context text must be at most {MaximumContextTextLength} non-control UTF-16 code units.",
                parameterName);
        }
    }
}
