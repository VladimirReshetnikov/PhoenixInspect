using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Core.Execution;

/// <summary>
/// Represents one activation frame for the admitted draft IL semantics slice.
/// </summary>
/// <typeparam name="TValue">The domain value representation stored in frame slots.</typeparam>
/// <param name="Method">The executing method definition.</param>
/// <param name="IlOffset">The offset of the next instruction to execute.</param>
/// <param name="Arguments">Index-addressed argument values, including <c>this</c> at slot zero when seeded.</param>
/// <param name="Locals">Index-addressed local values pre-sized by frame seeding.</param>
/// <param name="EvalStack">Evaluation-stack values with the last item at the top.</param>
/// <remarks>
/// Exception state and generic instantiation remain future additions. Methods requiring those features must be
/// rejected before execution rather than represented implicitly in these fields.
/// Root frames are created by <see cref="IlMachine{TValue,TMemory}.ActivateRoot"/> from a resolved method signature.
/// Return disposition and local layout are therefore method-plan facts rather than caller-authored frame fields.
/// A root frame has no <see cref="ReturnSite"/>. W4.5 interpreted callees freeze the exact direct-call and resume
/// boundary there before the frame becomes observable; model-covered calls do not create frames.
/// Public immutable properties still allow diagnostic tests and future state deserialization to construct a changed
/// record through <c>with</c>; every machine step revalidates such state against the frozen typed plan.
/// </remarks>
public sealed record FrameState<TValue>(
    MethodHandle Method,
    int IlOffset,
    ImmutableArray<TValue> Arguments,
    ImmutableArray<TValue> Locals,
    ImmutableArray<TValue> EvalStack)
{
    /// <summary>
    /// Gets the frozen caller boundary for a non-root interpreted frame, or <see langword="null"/> for a root frame.
    /// </summary>
    /// <remarks>
    /// This init-only addition deliberately does not alter the original five-argument positional constructor or its
    /// five-value deconstruction shape. The draft machine validates root/non-root placement and graph agreement at
    /// executable boundaries because record <c>with</c> expressions can still materialize inconsistent diagnostics.
    /// </remarks>
    public FrameReturnSite? ReturnSite { get; init; }
}
