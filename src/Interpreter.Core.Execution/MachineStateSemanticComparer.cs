using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Compares independently materialized machine states by domain equivalence and persistent-memory content.
/// </summary>
/// <typeparam name="TValue">The domain value representation carried by the state.</typeparam>
/// <typeparam name="TMemory">The persistent memory representation carried by the state.</typeparam>
/// <remarks>
/// Operational budgets are excluded by construction because they live in <see cref="MachineOperationalState"/>.
/// Domain equivalence is mutual lattice ordering, so presentation-only differences need not perturb worklists.
/// The current prototype deliberately returns a constant hash code: <see cref="IValueDomain{TValue}"/> has no
/// semantic hashing contract, and correctness is preferable to a hash that disagrees with semantic equality.
/// Introduce a domain hashing capability alongside the first measured fixpoint implementation.
/// </remarks>
public sealed class MachineStateSemanticComparer<TValue, TMemory> : IEqualityComparer<MachineState<TValue, TMemory>>
    where TMemory : IPersistentMemoryState<TMemory>
{
    private readonly IValueDomain<TValue> domain;
    private readonly IEqualityComparer<TMemory> memoryComparer;

    /// <summary>
    /// Creates a semantic-state comparer.
    /// </summary>
    /// <param name="domain">The value domain whose ordering defines semantic value equivalence.</param>
    /// <param name="memoryComparer">
    /// An optional persistent-memory content comparer; the memory type's default comparer is used when omitted.
    /// </param>
    public MachineStateSemanticComparer(
        IValueDomain<TValue> domain,
        IEqualityComparer<TMemory>? memoryComparer = null)
    {
        this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
        this.memoryComparer = memoryComparer ?? EqualityComparer<TMemory>.Default;
    }

    /// <inheritdoc />
    public bool Equals(MachineState<TValue, TMemory>? x, MachineState<TValue, TMemory>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null ||
            x.CallStack.IsDefault ||
            y.CallStack.IsDefault ||
            x.CallStack.Length != y.CallStack.Length ||
            !memoryComparer.Equals(x.Memory, y.Memory) ||
            x.ReturnValue.HasValue != y.ReturnValue.HasValue ||
            x.TerminalTargetException != y.TerminalTargetException)
        {
            return false;
        }

        if (x.ReturnValue.HasValue && !ValuesEquivalent(x.ReturnValue.Value, y.ReturnValue.Value))
        {
            return false;
        }

        for (var index = 0; index < x.CallStack.Length; index++)
        {
            var leftFrame = x.CallStack[index];
            var rightFrame = y.CallStack[index];
            if (leftFrame is null || rightFrame is null || !FramesEquivalent(leftFrame, rightFrame))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public int GetHashCode(MachineState<TValue, TMemory> obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return 0;
    }

    private bool FramesEquivalent(FrameState<TValue> left, FrameState<TValue> right) =>
        left.Method == right.Method &&
        left.IlOffset == right.IlOffset &&
        SequencesEquivalent(left.Arguments, right.Arguments) &&
        SequencesEquivalent(left.Locals, right.Locals) &&
        SequencesEquivalent(left.EvalStack, right.EvalStack);

    private bool SequencesEquivalent(
        System.Collections.Immutable.ImmutableArray<TValue> left,
        System.Collections.Immutable.ImmutableArray<TValue> right)
    {
        if (left.IsDefault || right.IsDefault || left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!ValuesEquivalent(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValuesEquivalent(TValue left, TValue right) =>
        left is not null &&
        right is not null &&
        Equals(domain.GetStaticType(left), domain.GetStaticType(right)) &&
        domain.IsLessThanOrEqual(left, right) &&
        domain.IsLessThanOrEqual(right, left);
}
