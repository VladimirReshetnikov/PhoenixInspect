using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

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
/// Exception state, generic instantiation, and return-site metadata remain future additions. Methods requiring
/// those features must be rejected before execution rather than represented implicitly in these fields.
/// Root frames are created by <see cref="IlMachine{TValue,TMemory}.ActivateRoot"/> from a resolved method signature.
/// Return disposition and local layout are therefore method-plan facts rather than caller-authored frame fields.
/// Public immutable properties still allow diagnostic tests and future state deserialization to construct a changed
/// record through <c>with</c>; every machine step revalidates such state against the frozen typed plan.
/// </remarks>
public sealed record FrameState<TValue>(
    MethodHandle Method,
    int IlOffset,
    ImmutableArray<TValue> Arguments,
    ImmutableArray<TValue> Locals,
    ImmutableArray<TValue> EvalStack);
