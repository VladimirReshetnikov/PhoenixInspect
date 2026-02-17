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
