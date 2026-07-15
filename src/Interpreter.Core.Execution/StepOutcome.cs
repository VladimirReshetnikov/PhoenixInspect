using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Represents the complete result of requesting one low-level machine transition.
/// </summary>
/// <typeparam name="TValue">The domain value representation carried by returned state.</typeparam>
/// <typeparam name="TMemory">The persistent memory representation carried by returned state.</typeparam>
/// <param name="State">The resulting state; it is unchanged when no instruction executed.</param>
/// <param name="OperationalState">
/// Updated deterministic bookkeeping. It is unchanged when no capability or instruction was entered, but a failed
/// pure-model invocation records one attempt and logical-boundary high water even though no instruction transferred.
/// </param>
/// <param name="Status">Whether the machine can continue, completed, exhausted budget, or became blocked/invalid.</param>
/// <param name="Events">Structured deterministic events emitted only for transfers that actually occurred.</param>
/// <param name="Failure">A structured failure when <paramref name="Status"/> is blocked or invalid.</param>
/// <param name="TargetException">
/// Structured target-exception information when <paramref name="Status"/> is
/// <see cref="MachineRunStatus.TargetException"/>.
/// </param>
public sealed record StepOutcome<TValue, TMemory>(
    MachineState<TValue, TMemory> State,
    MachineOperationalState OperationalState,
    MachineRunStatus Status,
    ImmutableArray<DebugEvent> Events,
    ExecutionFailure? Failure = null,
    TargetExceptionInfo? TargetException = null)
    where TMemory : IPersistentMemoryState<TMemory>
{
    private static readonly ConditionalWeakTable<
        StepOutcome<TValue, TMemory>,
        MachineTransitionCertificate> TransitionCertificates = new();

    /// <summary>
    /// Determines whether this exact outcome instance was issued by the core machine for the supplied exact input
    /// machine instance, input state, and operational-state instances.
    /// </summary>
    /// <param name="machine">The exact machine instance that was asked to perform the transition.</param>
    /// <param name="priorState">The immutable semantic-state instance supplied to the machine step.</param>
    /// <param name="priorOperationalState">
    /// The deterministic operational-state instance supplied to the same machine step.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when the supplied exact machine certified this exact outcome instance for both
    /// exact input references and none of its returned state, operational state, status, event, failure, or target-
    /// exception axes has changed; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Certification is process-local execution evidence, not canonical replay material. Constructed outcomes and
    /// record <c>with</c>-copies are deliberately uncertified even when their public values are structurally equal.
    /// Consumers must still validate the semantic transition appropriate to their product contract.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="machine"/>, <paramref name="priorState"/>, or <paramref name="priorOperationalState"/> is
    /// <see langword="null"/>.
    /// </exception>
    public bool IsMachineIssuedTransitionFrom(
        IlMachine<TValue, TMemory> machine,
        MachineState<TValue, TMemory> priorState,
        MachineOperationalState priorOperationalState)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(priorState);
        ArgumentNullException.ThrowIfNull(priorOperationalState);

        return TransitionCertificates.TryGetValue(this, out var certificate) &&
            certificate.Matches(machine, priorState, priorOperationalState, this);
    }

    internal StepOutcome<TValue, TMemory> CertifyTransitionFrom(
        IlMachine<TValue, TMemory> machine,
        MachineState<TValue, TMemory> priorState,
        MachineOperationalState priorOperationalState)
    {
        TransitionCertificates.Add(
            this,
            new MachineTransitionCertificate(machine, priorState, priorOperationalState, this));
        return this;
    }

    private sealed class MachineTransitionCertificate
    {
        private readonly IlMachine<TValue, TMemory> _machine;
        private readonly MachineState<TValue, TMemory> _priorState;
        private readonly MachineOperationalState _priorOperationalState;
        private readonly MachineState<TValue, TMemory> _returnedState;
        private readonly MachineOperationalState _returnedOperationalState;
        private readonly MachineRunStatus _status;
        private readonly ImmutableArray<DebugEvent> _events;
        private readonly ExecutionFailure? _failure;
        private readonly TargetExceptionInfo? _targetException;

        internal MachineTransitionCertificate(
            IlMachine<TValue, TMemory> machine,
            MachineState<TValue, TMemory> priorState,
            MachineOperationalState priorOperationalState,
            StepOutcome<TValue, TMemory> outcome)
        {
            _machine = machine;
            _priorState = priorState;
            _priorOperationalState = priorOperationalState;
            _returnedState = outcome.State;
            _returnedOperationalState = outcome.OperationalState;
            _status = outcome.Status;
            _events = outcome.Events.IsDefault
                ? default
                : ImmutableArray.CreateRange(outcome.Events.AsSpan().ToArray());
            _failure = outcome.Failure;
            _targetException = outcome.TargetException;
        }

        internal bool Matches(
            IlMachine<TValue, TMemory> machine,
            MachineState<TValue, TMemory> priorState,
            MachineOperationalState priorOperationalState,
            StepOutcome<TValue, TMemory> outcome)
        {
            if (!ReferenceEquals(_machine, machine) ||
                !ReferenceEquals(_priorState, priorState) ||
                !ReferenceEquals(_priorOperationalState, priorOperationalState) ||
                !ReferenceEquals(_returnedState, outcome.State) ||
                !ReferenceEquals(_returnedOperationalState, outcome.OperationalState) ||
                _status != outcome.Status ||
                !ReferenceEquals(_failure, outcome.Failure) ||
                !ReferenceEquals(_targetException, outcome.TargetException) ||
                _events.IsDefault != outcome.Events.IsDefault ||
                _events.Length != outcome.Events.Length)
            {
                return false;
            }

            for (var index = 0; index < _events.Length; index++)
            {
                if (!ReferenceEquals(_events[index], outcome.Events[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
