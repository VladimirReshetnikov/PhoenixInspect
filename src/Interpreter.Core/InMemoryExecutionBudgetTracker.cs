using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

using Interpreter.Abstractions;

namespace Interpreter.Core;

/// <summary>
/// Provides a lightweight in-memory implementation of <see cref="IExecutionBudgetTracker"/> for prototype execution flows.
/// </summary>
/// <remarks>
/// The implementation intentionally prioritizes deterministic and explainable behavior over completeness.
/// It should be treated as draft scaffolding that helps validate budget semantics while storage and replay architecture evolve.
/// </remarks>
public sealed class InMemoryExecutionBudgetTracker : IExecutionBudgetTracker
{
    private readonly ConcurrentDictionary<string, SessionBudgetState> sessionStates = new(StringComparer.Ordinal);

    /// <summary>
    /// Applies one budget charge request and returns the resulting meter state for the associated session.
    /// </summary>
    /// <param name="request">The budget charge request containing meter kind, amount, and explainability metadata.</param>
    /// <param name="executionRequest">The execution request that carries baseline instruction and branch limits.</param>
    /// <param name="cancellationToken">A token used to stop budget accounting when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the resulting charge outcome and optional stop descriptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="executionRequest"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="BudgetChargeRequest.Amount"/> is negative.</exception>
    public ValueTask<BudgetChargeResult> ChargeAsync(
        BudgetChargeRequest request,
        IExecutionRequest executionRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(executionRequest);

        if (request.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Amount, "Budget charge amounts must be non-negative in the draft tracker.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        SessionBudgetState state = this.sessionStates.GetOrAdd(
            request.SessionId,
            sessionId => SessionBudgetState.Create(sessionId, executionRequest));

        BudgetChargeResult result = state.ApplyCharge(request);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Returns the current in-memory budget snapshot for a session, creating an empty draft snapshot when no charges exist yet.
    /// </summary>
    /// <param name="sessionId">The execution session identifier whose budget state should be returned.</param>
    /// <param name="cancellationToken">A token used to stop snapshot retrieval when host cancellation is requested.</param>
    /// <returns>A value task that resolves to the current budget snapshot for the requested session.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sessionId"/> is blank.</exception>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session identifiers must be non-empty for budget snapshot retrieval.", nameof(sessionId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!this.sessionStates.TryGetValue(sessionId, out SessionBudgetState? state))
        {
            state = SessionBudgetState.CreateEmpty(sessionId);
        }

        return ValueTask.FromResult(state.GetSnapshot());
    }

    private sealed class SessionBudgetState
    {
        private readonly object gate = new();
        private readonly string sessionId;
        private readonly Dictionary<BudgetMetricKind, int> remainingByMetric;
        private readonly Dictionary<BudgetMetricKind, int> consumedByMetric;
        private DateTimeOffset lastUpdatedUtc;

        private SessionBudgetState(
            string sessionId,
            Dictionary<BudgetMetricKind, int> remainingByMetric,
            Dictionary<BudgetMetricKind, int> consumedByMetric,
            DateTimeOffset lastUpdatedUtc)
        {
            this.sessionId = sessionId;
            this.remainingByMetric = remainingByMetric;
            this.consumedByMetric = consumedByMetric;
            this.lastUpdatedUtc = lastUpdatedUtc;
        }

        public static SessionBudgetState Create(string sessionId, IExecutionRequest executionRequest)
        {
            Dictionary<BudgetMetricKind, int> remaining = CreateDefaultRemaining(executionRequest.Budget);
            Dictionary<BudgetMetricKind, int> consumed = CreateDefaultConsumed();
            return new SessionBudgetState(sessionId, remaining, consumed, DateTimeOffset.UtcNow);
        }

        public static SessionBudgetState CreateEmpty(string sessionId)
        {
            return new SessionBudgetState(
                sessionId,
                CreateDefaultRemaining(new ExecutionBudget(0, 0, null)),
                CreateDefaultConsumed(),
                DateTimeOffset.UtcNow);
        }

        public BudgetChargeResult ApplyCharge(BudgetChargeRequest request)
        {
            lock (this.gate)
            {
                int remaining = this.remainingByMetric.GetValueOrDefault(request.Metric);
                int amountApplied = Math.Min(request.Amount, remaining);
                int newRemaining = remaining - amountApplied;

                this.remainingByMetric[request.Metric] = newRemaining;
                this.consumedByMetric[request.Metric] = this.consumedByMetric.GetValueOrDefault(request.Metric) + amountApplied;
                this.lastUpdatedUtc = DateTimeOffset.UtcNow;

                bool isLimitExceeded = request.Amount > amountApplied;
                ExecutionStopDescriptor? stopDescriptor = isLimitExceeded
                    ? new ExecutionStopDescriptor(
                        ExecutionStopCategory.BudgetExceeded,
                        $"budget:{request.Metric.ToString().ToLowerInvariant()}",
                        $"Budget meter '{request.Metric}' exceeded while applying reason code '{request.ReasonCode}'.")
                    : null;

                string message = isLimitExceeded
                    ? $"Budget meter '{request.Metric}' exhausted after applying {amountApplied} of requested {request.Amount}."
                    : $"Budget meter '{request.Metric}' charged by {amountApplied}; {newRemaining} units remain.";

                return new BudgetChargeResult(
                    request.Metric,
                    amountApplied,
                    newRemaining,
                    isLimitExceeded,
                    stopDescriptor,
                    message);
            }
        }

        public BudgetSnapshot GetSnapshot()
        {
            lock (this.gate)
            {
                return new BudgetSnapshot(
                    SessionId: this.sessionId,
                    RemainingByMetric: new Dictionary<BudgetMetricKind, int>(this.remainingByMetric),
                    ConsumedByMetric: new Dictionary<BudgetMetricKind, int>(this.consumedByMetric),
                    LastUpdatedUtc: this.lastUpdatedUtc);
            }
        }

        private static Dictionary<BudgetMetricKind, int> CreateDefaultRemaining(ExecutionBudget budget)
        {
            return Enum.GetValues<BudgetMetricKind>().ToDictionary(
                metric => metric,
                metric => metric switch
                {
                    BudgetMetricKind.Instruction => budget.InstructionBudget,
                    BudgetMetricKind.BranchFork => budget.BranchBudget,
                    _ => int.MaxValue,
                });
        }

        private static Dictionary<BudgetMetricKind, int> CreateDefaultConsumed()
        {
            return Enum.GetValues<BudgetMetricKind>().ToDictionary(metric => metric, _ => 0);
        }
    }
}
