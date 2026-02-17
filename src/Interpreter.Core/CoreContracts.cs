using Interpreter.Abstractions;
using Interpreter.CallModel;
using Interpreter.MemoryModel;
using Interpreter.Metadata;

namespace Interpreter.Core;

/// <summary>
/// Orchestrates the end-to-end execution flow for a single prototype interpretation request.
/// </summary>
/// <remarks>
/// This contract is explicitly draft-only and should be considered unstable while we evaluate control-plane responsibilities
/// between core execution services and host-provided policy coordinators.
/// </remarks>
public interface IInterpreterEngine
{
    /// <summary>
    /// Executes the requested entry method using the supplied metadata descriptor and returns a deterministic result snapshot.
    /// </summary>
    /// <param name="request">The immutable request that defines the target method and execution budgets.</param>
    /// <param name="entryMethod">The metadata descriptor resolved for the request entry point.</param>
    /// <param name="cancellationToken">A token used to cancel interpretation when the host session ends or exceeds limits.</param>
    /// <returns>A result snapshot describing lifecycle state, stop reason, and explainability notes.</returns>
    ValueTask<IExecutionResult> ExecuteAsync(
        IExecutionRequest request,
        MethodMetadataDescriptor entryMethod,
        CancellationToken cancellationToken);
}

/// <summary>
/// Defines the prototype contract for stepping a single IL instruction against an abstract frame.
/// </summary>
/// <remarks>
/// The frame and step-result abstractions are intentionally represented as string dictionaries in the prototype so we can
/// validate host integration shape before hardening value-domain and stack-machine object models.
/// </remarks>
public interface IInstructionStepper
{
    /// <summary>
    /// Applies one IL instruction to the current abstract frame and returns the next frame snapshot.
    /// </summary>
    /// <param name="instructionOffset">The IL offset of the instruction being interpreted.</param>
    /// <param name="operationCode">The opcode mnemonic used for diagnostics and dispatch decisions.</param>
    /// <param name="frameState">The mutable-by-convention frame state represented as key/value diagnostic slots.</param>
    /// <param name="cancellationToken">A token used to abort stepping if host or budget cancellation is requested.</param>
    /// <returns>The next frame snapshot and any explainability notes produced by the step.</returns>
    ValueTask<InstructionStepResult> StepAsync(
        int instructionOffset,
        string operationCode,
        IReadOnlyDictionary<string, string> frameState,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the output from a single-step IL interpretation operation.
/// </summary>
/// <param name="NextInstructionOffset">Gets the next IL offset chosen by control-flow semantics after the step completes.</param>
/// <param name="FrameState">Gets the frame snapshot that should be used for the next interpretation cycle.</param>
/// <param name="ExplainabilityNotes">Gets explainability notes that justify conservative or unknown outcomes.</param>
/// <remarks>
/// This draft result keeps data structures lightweight so we can quickly iterate on stepping semantics during concept validation.
/// </remarks>
public sealed record InstructionStepResult(
    int NextInstructionOffset,
    IReadOnlyDictionary<string, string> FrameState,
    IReadOnlyList<string> ExplainabilityNotes);


/// <summary>
/// Coordinates cross-cutting prototype services required to evaluate one call instruction within an interpreter session.
/// </summary>
/// <remarks>
/// This contract keeps orchestration responsibilities explicit while project boundaries are still exploratory.
/// It is intentionally lightweight and may be split or merged as the prototype dependency model matures.
/// </remarks>
public interface IExecutionSessionCoordinator
{
    /// <summary>
    /// Classifies the specified call site and returns the resulting target/effect metadata for downstream step logic.
    /// </summary>
    /// <param name="callSite">The call-site descriptor representing the call instruction currently being interpreted.</param>
    /// <param name="executionRequest">The parent execution request carrying budget and session context.</param>
    /// <param name="cancellationToken">A token used to stop classification when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the call-site classification to apply for this instruction.</returns>
    ValueTask<CallSiteClassification> ClassifyCallAsync(
        CallSiteDescriptor callSite,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads a value from the current abstract memory snapshot so instruction evaluation can consume operands deterministically.
    /// </summary>
    /// <param name="readRequest">The memory-read request describing which abstract location to query.</param>
    /// <param name="executionRequest">The parent execution request used for policy and diagnostics context.</param>
    /// <param name="cancellationToken">A token used to abort read operations when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the abstract value stored at the requested location.</returns>
    ValueTask<AbstractValue> ReadMemoryAsync(
        MemoryReadRequest readRequest,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes a value into the current abstract memory snapshot after an instruction produces a new result.
    /// </summary>
    /// <param name="writeRequest">The memory-write request identifying the destination abstract location, slot index, and write intent.</param>
    /// <param name="value">The abstract value that should be stored into the destination location.</param>
    /// <param name="executionRequest">The parent execution request used for policy and diagnostics context.</param>
    /// <param name="cancellationToken">A token used to abort write operations when host cancellation is requested.</param>
    /// <returns>A value task that completes once the write operation has been applied.</returns>
    ValueTask WriteMemoryAsync(
        MemoryWriteRequest writeRequest,
        AbstractValue value,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a virtual task artifact for async method execution so stepping and explainability can track async lifecycles.
    /// </summary>
    /// <param name="sessionId">The parent execution session identifier that owns the virtual task.</param>
    /// <param name="producerMethodIdentity">The fully qualified async method identity creating the virtual task.</param>
    /// <param name="resultTypeDisplayName">The display name of the virtual task result type.</param>
    /// <param name="cancellationToken">A token used to stop async task creation when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the created virtual task snapshot.</returns>
    ValueTask<VirtualTaskSnapshot> CreateVirtualTaskAsync(
        string sessionId,
        string producerMethodIdentity,
        string resultTypeDisplayName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an await operation for the current async activation and returns how stepping should proceed.
    /// </summary>
    /// <param name="request">The await-registration request describing activation identity and awaiter metadata.</param>
    /// <param name="cancellationToken">A token used to cancel await registration when host policy ends execution.</param>
    /// <returns>A value task that resolves to the await-registration result including outcome and rationale.</returns>
    ValueTask<AwaitRegistrationResult> RegisterAwaitAsync(
        AwaitRegistrationRequest request,
        CancellationToken cancellationToken);
}
