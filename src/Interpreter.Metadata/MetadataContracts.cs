using Interpreter.Abstractions;

namespace Interpreter.Metadata;

/// <summary>
/// Defines a draft prototype abstraction that resolves method metadata required by the interpreter.
/// </summary>
/// <remarks>
/// This interface is purposely narrow while architecture decisions are still fluid. Implementations are expected to wrap
/// <c>System.Reflection.Metadata</c>, ClrMD-backed symbol services, or synthetic test metadata.
/// </remarks>
public interface IMethodMetadataProvider
{
    /// <summary>
    /// Resolves metadata for the method identity declared in an execution request.
    /// </summary>
    /// <param name="request">The prototype execution request containing the target entry method identity.</param>
    /// <param name="cancellationToken">A cancellation token used to abort expensive metadata acquisition operations.</param>
    /// <returns>
    /// A metadata descriptor that includes method signature and IL body descriptors required for deterministic interpretation.
    /// </returns>
    ValueTask<MethodMetadataDescriptor> ResolveEntryMethodAsync(IExecutionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Captures canonical method metadata used by prototype interpreter components.
/// </summary>
/// <param name="MethodIdentity">Gets the fully qualified method identity as resolved by the metadata subsystem.</param>
/// <param name="IlBytes">Gets the raw IL bytes for the target method body in ECMA-335 instruction order.</param>
/// <param name="MaxStack">Gets the method's declared max stack value used for basic validation and diagnostics.</param>
/// <param name="LocalCount">Gets the number of declared local variables for initial frame-shape construction.</param>
/// <remarks>
/// This record intentionally avoids exposing metadata token internals until canonical token and symbol abstractions are
/// finalized in the architecture proposals.
/// </remarks>
public sealed record MethodMetadataDescriptor(
    string MethodIdentity,
    IReadOnlyList<byte> IlBytes,
    int MaxStack,
    int LocalCount);

/// <summary>
/// Captures one debug-map entry that associates an IL offset range with a statement and source span.
/// </summary>
/// <param name="StartInstructionOffset">Gets the inclusive starting IL offset for the mapped range.</param>
/// <param name="EndInstructionOffset">Gets the exclusive ending IL offset for the mapped range.</param>
/// <param name="StatementId">Gets a stable statement identifier used by stepping and replay features.</param>
/// <param name="SourceSpan">Gets the optional source span descriptor associated with the IL range.</param>
/// <remarks>
/// This record is intentionally shape-focused and does not yet encode lexical scopes, hidden sequence points, or
/// advanced compiler-generated state-machine mapping details that are expected to emerge in later design iterations.
/// </remarks>
public sealed record DebugMapEntryDescriptor(
    int StartInstructionOffset,
    int EndInstructionOffset,
    string StatementId,
    SourceSpanDescriptor? SourceSpan);

/// <summary>
/// Represents the draft debug-map payload for one method body consumed by stepping and explainability surfaces.
/// </summary>
/// <param name="MethodIdentity">Gets the fully qualified method identity corresponding to this debug map.</param>
/// <param name="Entries">Gets ordered debug-map entries that partition statement-level stepping boundaries.</param>
/// <param name="HasSyntheticFallback">Gets a value indicating whether the map was synthesized due to missing symbols.</param>
/// <remarks>
/// The prototype intentionally models a single normalized map representation so hosts can reason about source-level
/// navigation even when symbol quality varies across dump artifacts.
/// </remarks>
public sealed record MethodDebugMapDescriptor(
    string MethodIdentity,
    IReadOnlyList<DebugMapEntryDescriptor> Entries,
    bool HasSyntheticFallback);

/// <summary>
/// Defines a draft prototype abstraction that resolves statement-level debug maps for interpreted methods.
/// </summary>
/// <remarks>
/// The interface is intentionally separate from <see cref="IMethodMetadataProvider"/> so architecture experiments can
/// evaluate independent caching policies and symbol backends without hard-coupling method-body and debug-map lifecycles.
/// </remarks>
public interface IMethodDebugMapProvider
{
    /// <summary>
    /// Resolves a debug-map descriptor for a previously resolved method metadata descriptor.
    /// </summary>
    /// <param name="request">The execution request used for session correlation, policy, and diagnostics scoping.</param>
    /// <param name="method">The method metadata descriptor whose IL offsets should be mapped to statement boundaries.</param>
    /// <param name="cancellationToken">A cancellation token used to abort symbol loading or map synthesis work.</param>
    /// <returns>
    /// A value task that resolves to a normalized method debug map suitable for virtual stepping and explainability output.
    /// </returns>
    ValueTask<MethodDebugMapDescriptor> ResolveDebugMapAsync(
        IExecutionRequest request,
        MethodMetadataDescriptor method,
        CancellationToken cancellationToken);
}
