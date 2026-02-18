using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents a single activation frame for draft interpreter execution.
/// </summary>
/// <typeparam name="TValue">Abstract value representation used by the evaluation stack.</typeparam>
/// <param name="Method">Method identity associated with the frame.</param>
/// <param name="IlOffset">Current IL offset within <paramref name="Method"/>.</param>
/// <param name="EvalStack">Current evaluation stack values with the last value representing the top of stack.</param>
public sealed record FrameState<TValue>(MethodHandle Method, int IlOffset, ImmutableArray<TValue> EvalStack);
