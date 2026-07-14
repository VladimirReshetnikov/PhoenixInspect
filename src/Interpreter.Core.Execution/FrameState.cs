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
/// <param name="ReturnsValue">Whether <c>ret</c> must transfer exactly one value.</param>
/// <remarks>
/// Exception state, generic instantiation, and return-site metadata remain future additions. Methods requiring
/// those features must be rejected before execution rather than represented implicitly in these fields.
/// For the E1 arithmetic slice, a trusted fixture seeder is responsible for decoding the method signature and
/// supplying the exact argument/local counts. Whole-body admission checks every referenced slot and the machine
/// independently requires all seeded/current values to have the CLI I4 stack kind; arbitrary production signature
/// binding is not claimed until metadata signature decoding is added at this boundary.
/// </remarks>
public sealed record FrameState<TValue>(
    MethodHandle Method,
    int IlOffset,
    ImmutableArray<TValue> Arguments,
    ImmutableArray<TValue> Locals,
    ImmutableArray<TValue> EvalStack,
    bool ReturnsValue);
