using Interpreter.Abstractions;

namespace Interpreter.RuntimeBinding;

/// <summary>
/// Identifies where a method body payload originated when the runtime-binding layer resolves IL for interpretation.
/// </summary>
/// <remarks>
/// This provenance is intentionally explicit so explainability surfaces can communicate whether data was recovered from
/// dump memory, from PE/PDB artifacts, or synthesized as a conservative fallback during prototype exploration.
/// </remarks>
public enum MethodBodyProvenance
{
    /// <summary>
    /// Indicates the IL payload was read directly from runtime/dump memory using runtime-specific APIs.
    /// </summary>
    RuntimeMemory,

    /// <summary>
    /// Indicates the IL payload was loaded from a PE artifact resolved through module-location policies.
    /// </summary>
    PortableExecutable,

    /// <summary>
    /// Indicates the IL payload was synthesized by a fallback policy due to missing runtime and PE data.
    /// </summary>
    SyntheticFallback,
}

/// <summary>
/// Represents a stable module identity used by interpreter-facing contracts without leaking backend-specific module objects.
/// </summary>
/// <param name="Mvid">Gets the module version identifier when available from metadata reconciliation.</param>
/// <param name="ModuleName">Gets a stable display-oriented module name used for diagnostics and fallback matching.</param>
/// <remarks>
/// The prototype keeps this identifier compact so multiple backends can participate in identity resolution while design
/// discussions continue around stronger provenance and version-signature policies.
/// </remarks>
public readonly record struct ModuleId(Guid? Mvid, string ModuleName);

/// <summary>
/// Represents a stable method identity composed of module identity and metadata token information.
/// </summary>
/// <param name="Module">Gets the module identity that declares or references the target method.</param>
/// <param name="MetadataToken">Gets the metadata token associated with the method identity in hexadecimal string form.</param>
/// <remarks>
/// Token representation is intentionally textual in this phase to keep parser choices reversible while the metadata
/// subsystem converges on canonical token value objects.
/// </remarks>
public readonly record struct MethodId(ModuleId Module, string MetadataToken);

/// <summary>
/// Captures one normalized runtime module snapshot entry used by metadata reconciliation and diagnostics.
/// </summary>
/// <param name="Module">Gets the stable module identity synthesized for the runtime module record.</param>
/// <param name="ImageBase">Gets the runtime image base address for correlation with low-level diagnostics.</param>
/// <param name="ImageSize">Gets the runtime image size in bytes when provided by the dump runtime backend.</param>
/// <param name="FilePath">Gets the optional module file path hint used by PE/PDB artifact resolution.</param>
public sealed record RuntimeModuleRecord(
    ModuleId Module,
    ulong ImageBase,
    int ImageSize,
    string? FilePath);

/// <summary>
/// Captures one managed stack-frame anchor normalized for interpreter session startup and expression evaluation.
/// </summary>
/// <param name="OsThreadId">Gets the operating-system thread identifier associated with the captured frame.</param>
/// <param name="FrameIndex">Gets the zero-based managed frame index within the owning thread snapshot.</param>
/// <param name="InstructionPointer">Gets the frame instruction pointer used for native-to-managed correlation diagnostics.</param>
/// <param name="Method">Gets the optional method identity resolved for the managed frame.</param>
/// <remarks>
/// The method identity can be absent for non-managed frames or partially recoverable traces; callers should combine this
/// shape with miss-reason diagnostics before initiating interpretation.
/// </remarks>
public sealed record RuntimeFrameRecord(
    uint OsThreadId,
    int FrameIndex,
    ulong InstructionPointer,
    MethodId? Method);

/// <summary>
/// Represents a normalized runtime snapshot envelope consumed by higher-level interpreter orchestration components.
/// </summary>
/// <param name="SessionId">Gets the parent interpreter session identifier used for end-to-end correlation.</param>
/// <param name="Modules">Gets the captured runtime module records used for artifact and token reconciliation.</param>
/// <param name="Frames">Gets the captured managed frame anchors available for expression-evaluation entrypoint selection.</param>
/// <remarks>
/// This envelope intentionally avoids heap and locals payloads so capture remains lightweight and deterministic in the
/// current design phase.
/// </remarks>
public sealed record RuntimeSnapshotDescriptor(
    string SessionId,
    IReadOnlyList<RuntimeModuleRecord> Modules,
    IReadOnlyList<RuntimeFrameRecord> Frames);

/// <summary>
/// Describes the method-body payload returned by the runtime-binding layer to interpreter metadata and execution services.
/// </summary>
/// <param name="Method">Gets the stable method identity that the returned method-body payload belongs to.</param>
/// <param name="IlBytes">Gets the normalized IL byte sequence in ECMA-335 instruction order.</param>
/// <param name="MaxStack">Gets the declared max-stack value associated with the method body.</param>
/// <param name="InitLocals">Gets a value indicating whether local variables should be zero-initialized by default.</param>
/// <param name="LocalCount">Gets the declared local variable count for initial frame construction heuristics.</param>
/// <param name="Provenance">Gets the source provenance describing how the method body was obtained.</param>
/// <remarks>
/// The prototype currently stores local-shape data in summarized form; richer local-signature details are expected once
/// metadata/token APIs are finalized.
/// </remarks>
public sealed record MethodBodyDescriptor(
    MethodId Method,
    IReadOnlyList<byte> IlBytes,
    int MaxStack,
    bool InitLocals,
    int LocalCount,
    MethodBodyProvenance Provenance);

/// <summary>
/// Defines the runtime-binding seam responsible for opening dump-backed sessions and capturing normalized runtime snapshots.
/// </summary>
/// <remarks>
/// Implementations are expected to wrap backend-specific runtime APIs (such as ClrMD) while emitting backend-neutral
/// records so downstream interpreter layers remain reusable in non-dump scenarios.
/// </remarks>
public interface IRuntimeSnapshotProvider
{
    /// <summary>
    /// Captures a normalized runtime snapshot envelope for the provided interpreter execution request.
    /// </summary>
    /// <param name="request">The execution request that provides session context and target selection identifiers.</param>
    /// <param name="cancellationToken">A token used to cancel expensive dump-loading or stack-enumeration operations.</param>
    /// <returns>
    /// A value task that resolves to a runtime snapshot descriptor suitable for metadata reconciliation and frame selection.
    /// </returns>
    ValueTask<RuntimeSnapshotDescriptor> CaptureSnapshotAsync(IExecutionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Defines the runtime-binding seam responsible for resolving method bodies with runtime-first and artifact fallback policy.
/// </summary>
/// <remarks>
/// Implementations should record deterministic provenance and avoid throwing backend-specific exceptions for expected miss
/// states; miss conditions should be surfaced using project diagnostics and conservative fallback descriptors.
/// </remarks>
public interface IRuntimeMethodBodyResolver
{
    /// <summary>
    /// Resolves a method-body descriptor for the specified method identity under the current execution request context.
    /// </summary>
    /// <param name="request">The execution request that defines session scope and policy decisions for resolution.</param>
    /// <param name="method">The method identity requiring IL and header metadata for interpretation startup.</param>
    /// <param name="cancellationToken">A token used to cancel runtime-memory reads or PE/PDB artifact loading operations.</param>
    /// <returns>
    /// A value task that resolves to a method-body descriptor including IL bytes, frame-shape hints, and provenance.
    /// </returns>
    ValueTask<MethodBodyDescriptor> ResolveMethodBodyAsync(
        IExecutionRequest request,
        MethodId method,
        CancellationToken cancellationToken);
}
