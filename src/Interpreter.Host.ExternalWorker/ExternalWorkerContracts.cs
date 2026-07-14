namespace Interpreter.Host.ExternalWorker;

/// <summary>Classifies the one operation admitted by the W1 external-artifact worker.</summary>
public enum ExternalWorkerOperation
{
    /// <summary>Evaluates one bounded W2 root-field query over one inherited dump artifact.</summary>
    DumpQuery,
}

/// <summary>Classifies a worker response without exposing artifact-derived payloads through status text.</summary>
public enum ExternalWorkerOutcome
{
    /// <summary>The worker returned a structurally valid query result.</summary>
    Completed,

    /// <summary>The caller request violated the fixed protocol contract.</summary>
    InvalidRequest,

    /// <summary>The staged artifact was unavailable, malformed, unsupported, or exceeded a limit.</summary>
    ArtifactRejected,

    /// <summary>The required runtime-adjacent DAC did not satisfy the pinned trust policy.</summary>
    TrustedDacRejected,

    /// <summary>The operating system could not establish every required containment control.</summary>
    ContainmentUnavailable,

    /// <summary>The worker exceeded a host resource boundary.</summary>
    ResourceLimit,

    /// <summary>The worker exited or failed without returning a valid bounded result.</summary>
    WorkerFailure,
}

/// <summary>Classifies the intentionally coarse resource signal permitted in worker telemetry.</summary>
public enum ExternalWorkerResourceBucket
{
    /// <summary>The operation returned inside every enforced host boundary.</summary>
    WithinLimits,

    /// <summary>A host resource boundary terminated or rejected the operation.</summary>
    LimitReached,

    /// <summary>No trustworthy resource classification was available.</summary>
    Unknown,
}

/// <summary>Specifies one bounded query request carried over the inherited request pipe.</summary>
/// <param name="RootTypeName">Exact runtime type name used for bounded strong-root selection.</param>
/// <param name="RootName">Exact case-sensitive root identifier admitted by the W2 grammar.</param>
/// <param name="Expression">Untrusted W2 expression text.</param>
public sealed record ExternalDumpQueryRequest(
    string RootTypeName,
    string RootName,
    string Expression);

/// <summary>Provides one diagnostic already normalized by the query or worker boundary.</summary>
/// <param name="Code">Stable machine-readable reason code.</param>
/// <param name="Message">Fixed payload-safe explanation.</param>
public sealed record ExternalWorkerDiagnostic(string Code, string Message);

/// <summary>Projects the closed W2 value union across the worker protocol.</summary>
/// <param name="Kind">One of <c>Null</c>, <c>Int32</c>, or <c>String</c>.</param>
/// <param name="Int32Value">Integer payload when <paramref name="Kind"/> is <c>Int32</c>.</param>
/// <param name="StringValue">String payload when <paramref name="Kind"/> is <c>String</c>.</param>
/// <remarks>This value can contain target data and must never be copied into telemetry.</remarks>
public sealed record ExternalDumpQueryValue(
    string Kind,
    int? Int32Value,
    string? StringValue);

/// <summary>Identifies the immutable dump snapshot that supplied an authorized query result.</summary>
/// <param name="Sha256">Lowercase SHA-256 digest of the complete staged dump.</param>
/// <param name="MemorySourceId">Canonical dump-memory evidence source identifier.</param>
public sealed record ExternalDumpSnapshotIdentity(
    string Sha256,
    string MemorySourceId);

/// <summary>Identifies one runtime module instance inside one immutable dump snapshot.</summary>
/// <param name="SnapshotSha256">SHA-256 identity of the containing dump snapshot.</param>
/// <param name="AppDomainAddress">Target address of the owning CLR application domain.</param>
/// <param name="ModuleAddress">Target address of the CLR module structure.</param>
/// <param name="ImageBase">Target base address of the mapped module image, or zero when unavailable.</param>
/// <param name="ImageSize">Observed mapped-image size in bytes.</param>
public sealed record ExternalDumpModuleIdentity(
    string SnapshotSha256,
    ulong AppDomainAddress,
    ulong ModuleAddress,
    ulong ImageBase,
    ulong ImageSize);

/// <summary>Reports every deterministic admission and traversal bound applied to a worker request.</summary>
/// <param name="MaximumArtifactBytes">Maximum staged artifact length.</param>
/// <param name="MaximumClrmdCacheBytes">Maximum ClrMD dump-reader cache size.</param>
/// <param name="MaximumRootMatches">Maximum strong-root matches materialized by root selection.</param>
/// <param name="MaximumHandlesScanned">Maximum strong handles inspected by root selection.</param>
/// <param name="MaximumObservedStringCharacters">Maximum observed string payload retained in a result.</param>
/// <param name="MaximumRootTypeNameCharacters">Maximum root runtime-type-name length.</param>
/// <param name="MaximumRootNameCharacters">Maximum query root-identifier length.</param>
/// <param name="MaximumExpressionCharacters">Maximum query expression length.</param>
/// <param name="MaximumRequestPayloadBytes">Maximum framed request payload length.</param>
/// <param name="MaximumResponsePayloadBytes">Maximum framed response payload length.</param>
/// <param name="MaximumProcessMemoryBytes">Maximum worker process and aggregate Job memory.</param>
/// <param name="MaximumProcessUserTimeTicks">Maximum worker process user-mode CPU duration in ticks.</param>
/// <param name="MaximumNetworkProbeMilliseconds">Maximum duration of the empirical loopback-denial probe.</param>
/// <param name="MaximumWallDurationMilliseconds">Maximum broker-observed wall duration in milliseconds.</param>
public sealed record ExternalWorkerAppliedBounds(
    long MaximumArtifactBytes,
    long MaximumClrmdCacheBytes,
    int MaximumRootMatches,
    int MaximumHandlesScanned,
    int MaximumObservedStringCharacters,
    int MaximumRootTypeNameCharacters,
    int MaximumRootNameCharacters,
    int MaximumExpressionCharacters,
    int MaximumRequestPayloadBytes,
    int MaximumResponsePayloadBytes,
    long MaximumProcessMemoryBytes,
    long MaximumProcessUserTimeTicks,
    int MaximumNetworkProbeMilliseconds,
    int MaximumWallDurationMilliseconds);

/// <summary>Classifies the payload-free result of establishing the worker's broker-owned private scratch boundary.</summary>
public enum ExternalWorkerScratchStatus
{
    /// <summary>The worker established and re-verified its private current, temporary, and local-app-data paths.</summary>
    Established,

    /// <summary>One of the broker-required scratch environment values was absent.</summary>
    EnvironmentUnavailable,

    /// <summary>A broker-provided scratch path was not fully qualified.</summary>
    InvalidPath,

    /// <summary>The broker-created per-request scratch directory was unavailable to the worker.</summary>
    ScratchDirectoryUnavailable,

    /// <summary>The per-request scratch directory was not contained by the AppContainer profile.</summary>
    OutsideProfile,

    /// <summary>Windows rejected the worker's attempt to select its private scratch directory.</summary>
    EstablishmentRejected,

    /// <summary>The environment did not retain the complete private-scratch policy after it was applied.</summary>
    VerificationFailed,
}

/// <summary>Records containment facts needed by executable boundary tests.</summary>
/// <param name="AppContainerToken">The runner observed an AppContainer token.</param>
/// <param name="JobMembership">The runner observed membership in a Windows Job Object.</param>
/// <param name="JobLimitFlags">Effective <c>JOB_OBJECT_LIMIT_*</c> flags observed by the runner.</param>
/// <param name="JobActiveProcessLimit">Effective active-process limit observed by the runner.</param>
/// <param name="JobProcessMemoryBytes">Effective per-process memory limit observed by the runner.</param>
/// <param name="JobMemoryBytes">Effective aggregate Job memory limit observed by the runner.</param>
/// <param name="JobProcessUserTimeTicks">Effective per-process user-mode CPU limit observed by the runner.</param>
/// <param name="ZeroCapabilityLaunch">The runner observed zero capability SIDs in its effective token.</param>
/// <param name="ExactHandleListLaunch">The broker supplied only the artifact and two protocol pipe handles.</param>
/// <param name="AtomicJobLaunch">The broker used <c>PROC_THREAD_ATTRIBUTE_JOB_LIST</c>.</param>
/// <param name="ChildProcessDenied">
/// A deliberate runner spawn probe was denied while the effective Job limited active processes to one and prohibited
/// both breakaway modes.
/// </param>
/// <param name="DiagnosticsDisabled">The runner observed disabled .NET diagnostics policy.</param>
/// <param name="ScratchStatus">Payload-free status of the runner's private scratch establishment.</param>
/// <param name="EnvironmentCleared">The runner observed the fixed allowlisted environment marker.</param>
/// <param name="NetworkDenied">A runner loopback connection to a broker-owned listener was denied.</param>
/// <param name="HeadlessErrorPolicy">The runner observed Win32, WER, and .NET no-dialog policy.</param>
/// <param name="ArtifactReadOnly">The runner opened the inherited artifact handle without write access.</param>
/// <param name="TrustedDacPinned">The runner matched the runtime-adjacent DAC to the compiled SHA-256 pin.</param>
public sealed record ExternalWorkerContainmentAttestation(
    bool AppContainerToken,
    bool JobMembership,
    uint JobLimitFlags,
    uint JobActiveProcessLimit,
    long JobProcessMemoryBytes,
    long JobMemoryBytes,
    long JobProcessUserTimeTicks,
    bool ZeroCapabilityLaunch,
    bool ExactHandleListLaunch,
    bool AtomicJobLaunch,
    bool ChildProcessDenied,
    bool DiagnosticsDisabled,
    ExternalWorkerScratchStatus ScratchStatus,
    bool EnvironmentCleared,
    bool NetworkDenied,
    bool HeadlessErrorPolicy,
    bool ArtifactReadOnly,
    bool TrustedDacPinned)
{
    internal static ExternalWorkerContainmentAttestation Empty { get; } = new(
        false, false, 0, 0, 0, 0, 0, false, false, false, false, false,
        ExternalWorkerScratchStatus.EnvironmentUnavailable, false, false, false, false, false);
}

/// <summary>Returns one bounded external-worker result frame.</summary>
/// <param name="Outcome">Coarse operation outcome.</param>
/// <param name="Code">Stable payload-safe worker code.</param>
/// <param name="Message">Fixed payload-safe worker explanation.</param>
/// <param name="SemanticMode">Query result semantic mode, when evaluation ran.</param>
/// <param name="Completion">Query completion axis, when evaluation ran.</param>
/// <param name="Completeness">Query completeness axis, when evaluation ran.</param>
/// <param name="Evidence">Query evidence axis, when evaluation ran.</param>
/// <param name="Effects">Query effects axis, when evaluation ran.</param>
/// <param name="SnapshotIdentity">Immutable identity of the dump evidence source, when the dump opened.</param>
/// <param name="ModuleIdentity">Snapshot-scoped identity of the selected root module, when exactly one root was selected.</param>
/// <param name="EvidenceSource">Stable classification of the evidence source, or <c>None</c> before evidence opened.</param>
/// <param name="AppliedBounds">Complete deterministic admission and traversal bounds applied to the operation.</param>
/// <param name="Fallback">Stable name of the fallback used; <c>None</c> means no fallback occurred.</param>
/// <param name="Value">Authorized result payload; never telemetry-safe.</param>
/// <param name="ProvenanceCount">Number of provenance entries retained in-process.</param>
/// <param name="Diagnostics">Stable query diagnostics.</param>
/// <param name="Attestation">Runner-observed containment facts.</param>
public sealed record ExternalDumpQueryResponse(
    ExternalWorkerOutcome Outcome,
    string Code,
    string Message,
    string? SemanticMode,
    string? Completion,
    string? Completeness,
    string? Evidence,
    string? Effects,
    ExternalDumpSnapshotIdentity? SnapshotIdentity,
    ExternalDumpModuleIdentity? ModuleIdentity,
    string EvidenceSource,
    ExternalWorkerAppliedBounds AppliedBounds,
    string Fallback,
    ExternalDumpQueryValue? Value,
    int ProvenanceCount,
    ExternalWorkerDiagnostic[] Diagnostics,
    ExternalWorkerContainmentAttestation Attestation)
{
    internal static ExternalDumpQueryResponse Failure(
        ExternalWorkerOutcome outcome,
        string code,
        string message,
        ExternalWorkerContainmentAttestation? attestation = null) =>
        new(
            outcome,
            code,
            message,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "None",
            ExternalWorkerPolicy.AppliedBounds,
            "None",
            null,
            0,
            [],
            attestation ?? ExternalWorkerContainmentAttestation.Empty);
}

/// <summary>Provides the only data shape allowed to enter external-worker telemetry.</summary>
/// <param name="Operation">Coarse operation kind.</param>
/// <param name="Outcome">Coarse normalized outcome.</param>
/// <param name="ResourceBucket">Coarse resource classification.</param>
/// <param name="ContainmentProfile">Fixed containment profile identifier.</param>
public sealed record ExternalWorkerTelemetry(
    ExternalWorkerOperation Operation,
    ExternalWorkerOutcome Outcome,
    ExternalWorkerResourceBucket ResourceBucket,
    string ContainmentProfile);

/// <summary>Separates the authorized result payload from the payload-free telemetry projection.</summary>
/// <param name="Response">Authorized bounded worker response.</param>
/// <param name="Telemetry">Payload-free coarse telemetry.</param>
public sealed record ExternalWorkerExecutionResult(
    ExternalDumpQueryResponse Response,
    ExternalWorkerTelemetry Telemetry);

internal static class ExternalWorkerPolicy
{
    internal const long MaximumArtifactBytes = 8L * 1024 * 1024 * 1024;
    internal const long MaximumClrmdCacheBytes = 256L * 1024 * 1024;
    internal const int MaximumRootMatches = 2;
    internal const int MaximumHandlesScanned = 100_000;
    internal const int MaximumObservedStringCharacters = 4096;
    internal const int MaximumRootTypeNameCharacters = 4096;
    internal const int MaximumRootNameCharacters = 64;
    internal const int MaximumExpressionCharacters = 512;
    internal const int MaximumRequestPayloadBytes = 64 * 1024;
    internal const int MaximumResponsePayloadBytes = 256 * 1024;
    internal const long MaximumProcessMemoryBytes = 1536L * 1024 * 1024;
    internal const long MaximumProcessUserTimeTicks = 60L * TimeSpan.TicksPerSecond;
    internal const int MaximumNetworkProbeMilliseconds = 1_000;
    internal const int MaximumWallDurationMilliseconds = 90_000;

    internal static ExternalWorkerAppliedBounds AppliedBounds { get; } = new(
        MaximumArtifactBytes,
        MaximumClrmdCacheBytes,
        MaximumRootMatches,
        MaximumHandlesScanned,
        MaximumObservedStringCharacters,
        MaximumRootTypeNameCharacters,
        MaximumRootNameCharacters,
        MaximumExpressionCharacters,
        MaximumRequestPayloadBytes,
        MaximumResponsePayloadBytes,
        MaximumProcessMemoryBytes,
        MaximumProcessUserTimeTicks,
        MaximumNetworkProbeMilliseconds,
        MaximumWallDurationMilliseconds);
}
