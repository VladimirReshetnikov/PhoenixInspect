using Interpreter.Abstractions;
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
