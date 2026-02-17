using Interpreter.Abstractions;

namespace Interpreter.MemoryModel;

/// <summary>
/// Identifies the abstract location category for a value tracked by the prototype memory model.
/// </summary>
/// <remarks>
/// These categories are intentionally coarse to keep the initial memory-model seam lightweight and open to rapid revision.
/// </remarks>
public enum MemoryLocationKind
{
    /// <summary>
    /// Indicates that the value is associated with the current stack frame evaluation stack.
    /// </summary>
    EvaluationStack,

    /// <summary>
    /// Indicates that the value is associated with a local-variable slot in the current frame.
    /// </summary>
    Local,

    /// <summary>
    /// Indicates that the value is associated with an argument slot in the current frame.
    /// </summary>
    Argument,

    /// <summary>
    /// Indicates that the value location is unknown and must be modeled conservatively.
    /// </summary>
    Unknown,
}

/// <summary>
/// Represents a draft abstract value tracked by prototype interpreter components.
/// </summary>
/// <param name="TypeDisplayName">Gets a human-readable type descriptor used for diagnostics and explainability output.</param>
/// <param name="ValueDisplay">Gets a printable value representation, which may contain symbolic placeholders such as <c>unknown:i32</c>.</param>
/// <param name="Provenance">Gets a concise provenance marker describing where this value originated.</param>
/// <remarks>
/// The record shape is intentionally textual in this phase to avoid premature commitment to a specific abstract-domain object model.
/// </remarks>
public sealed record AbstractValue(
    string TypeDisplayName,
    string ValueDisplay,
    string Provenance);

/// <summary>
/// Describes one memory-read request issued by the core execution pipeline.
/// </summary>
/// <param name="SessionId">Gets the parent execution session identifier used for correlation across diagnostics.</param>
/// <param name="LocationKind">Gets the category of location being read from the abstract memory snapshot.</param>
/// <param name="SlotIndex">Gets the slot index within the location category, such as local index or stack depth.</param>
/// <remarks>
/// This request format is a prototype contract and may be replaced by richer strongly typed addresses as design matures.
/// </remarks>
public sealed record MemoryReadRequest(
    string SessionId,
    MemoryLocationKind LocationKind,
    int SlotIndex);

/// <summary>
/// Describes one memory-write request issued by the core execution pipeline.
/// </summary>
/// <param name="SessionId">Gets the parent execution session identifier used for correlation across diagnostics.</param>
/// <param name="LocationKind">Gets the category of location being updated in the abstract memory snapshot.</param>
/// <param name="SlotIndex">Gets the slot index within the location category, such as local index or stack depth.</param>
/// <param name="WriteReason">Gets a concise reason label describing why the write is being performed.</param>
/// <remarks>
/// The write request mirrors <see cref="MemoryReadRequest"/> but adds intent metadata so diagnostics and replay traces can
/// explain memory transitions without requiring inference from opcode mnemonics.
/// </remarks>
public sealed record MemoryWriteRequest(
    string SessionId,
    MemoryLocationKind LocationKind,
    int SlotIndex,
    string WriteReason);

/// <summary>
/// Defines the prototype contract for reading and writing abstract memory values during interpretation.
/// </summary>
/// <remarks>
/// Implementations are expected to be deterministic and explainable; all unknown behavior should be surfaced through
/// result payloads and diagnostic channels rather than hidden side effects.
/// </remarks>
public interface IAbstractMemoryStore
{
    /// <summary>
    /// Reads an abstract value from the requested location in the current execution state snapshot.
    /// </summary>
    /// <param name="request">The memory-read request identifying the abstract location and slot index to access.</param>
    /// <param name="executionRequest">The parent execution request used to apply session-level policy and budget semantics.</param>
    /// <param name="cancellationToken">A token that aborts the read operation when execution is canceled.</param>
    /// <returns>A value task that resolves to the abstract value currently associated with the requested location.</returns>
    ValueTask<AbstractValue> ReadAsync(
        MemoryReadRequest request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes an abstract value into the requested location for the current execution state snapshot.
    /// </summary>
    /// <param name="request">The memory-write request identifying the location category and slot index to overwrite.</param>
    /// <param name="value">The abstract value to store, including display and provenance information.</param>
    /// <param name="executionRequest">The parent execution request used for policy and diagnostics context.</param>
    /// <param name="cancellationToken">A token that aborts the write operation when execution is canceled.</param>
    /// <returns>A value task that completes when the write has been applied to the prototype store.</returns>
    ValueTask WriteAsync(
        MemoryWriteRequest request,
        AbstractValue value,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken);
}
