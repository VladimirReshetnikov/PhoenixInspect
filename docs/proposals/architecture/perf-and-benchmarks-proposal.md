# Performance and Benchmark Proposal

> **Roadmap status: supporting research.** Numerical SLAs and broad matrices below are hypotheses until the active dump/query and concrete IL corpora produce baselines. Near-term gates track correctness, determinism, boundedness, and gross regressions only.

## Status

Draft

## Scope

This document defines the performance model, benchmark corpus strategy, measurement methodology, and acceptance thresholds for the IL interpreter and dump-time evaluation engine.

It complements:

- `testing-strategy-proposal.md` (quality/test taxonomy),
- `state-and-domain-model-proposal.md` (semantic state contracts),
- `il-interpreter-framework-proposal.md` (execution architecture).

---

## 1) Performance goals

Primary objective: keep execution **predictable, bounded, and explainable** under realistic debug/evaluation workloads.

Non-goals:

- maximizing peak throughput at the cost of determinism,
- speculative optimizations that hide approximation behavior,
- benchmark-only optimizations that degrade maintainability.

### 1.1 Service-level targets (initial proposal)

For curated MVP corpus, under `balanced` policy:

1. p50 method evaluation latency: <= 5 ms
2. p95 method evaluation latency: <= 30 ms
3. timeout rate under default budgets: < 2%
4. deterministic replay hash mismatch rate: 0%

These targets are provisional and should be revised after first real corpus capture.

---

## 2) Workload model

We benchmark three workload classes.

## A. Interactive expression workloads

Characteristics:

- short methods,
- repeated evaluations with similar context,
- user-visible latency sensitivity.

Primary metrics:

- p50/p95 latency,
- startup overhead,
- trace/artifact emission overhead.

## B. Method analysis workloads (abstract mode)

Characteristics:

- CFG traversal,
- join/widen cycles,
- higher sensitivity to path explosion.

Primary metrics:

- total iterations,
- joins/widen invocations,
- convergence time.

## C. Dump-backed integration workloads

Characteristics:

- metadata indirections,
- partial/missing memory,
- higher unknown/blocked rates.

Primary metrics:

- adapter round-trip cost,
- fallback frequency,
- diagnostic generation overhead.

---

## 3) Benchmark corpus design

### 3.1 Corpus tiers

1. **Smoke** (fast CI):
   - 20–40 representative methods,
   - < 1 minute total runtime.

2. **Core** (PR confidence):
   - 100–250 methods spanning opcode/control-flow families,
   - mixed concrete/hybrid/abstract scenarios.

3. **Extended** (scheduled/deep):
   - 500+ methods,
   - includes stress/pathological fixtures.

### 3.2 Corpus dimensions

Each method fixture is tagged by:

- opcode families used,
- control-flow shape (straight-line, branching, loop-heavy),
- call intensity,
- generic complexity,
- dump-adapter dependency level,
- expected precision class (high/medium/low).

### 3.3 Benchmark metadata schema

```text
BenchmarkCase = {
  CaseId,
  MethodRef,
  Mode,
  PolicyPreset,
  InputProfile,
  ExpectedStatusEnvelope,
  Tags,
  Owner
}
```

---

## 4) Metrics and instrumentation

## 4.1 Required metrics

Per benchmark run capture:

- wall-clock duration,
- CPU time (if available),
- allocated bytes,
- GC collections by generation,
- executed instruction count,
- join/widen counts,
- unknown introductions,
- blocked call count,
- budget exhaustion events.

## 4.2 Derived metrics

- unknowns per 1k instructions,
- joins per block,
- latency contribution by phase (decode/execute/join/adapter),
- approximation density score.

## 4.3 Trace correlation

Every metric sample should include correlation identifiers linking to:

- scenario trace,
- approximation event list,
- policy snapshot.

This supports “why slower?” and “why less precise?” debugging from the same run.

---

## 5) Measurement methodology

### 5.1 Run configuration

- execute in release configuration,
- pin runtime version for baseline comparability,
- warmup iterations before timed runs,
- fixed random seed for generative cases,
- isolated process execution for determinism checks.

### 5.2 Statistical reporting

For each case/preset report:

- min, p50, p90, p95, p99,
- standard deviation,
- coefficient of variation,
- sample count and outlier policy.

### 5.3 Noise controls

- run on dedicated CI agents for protected branches,
- avoid concurrent heavy jobs on perf runners,
- annotate environment changes (runtime update, VM class change).

---

## 6) Regression gating policy

## 6.1 Gate phases

1. **Observe-only** (M1–M2): publish results, no hard failure.
2. **Soft-gate** (M3–M4): warn when thresholds exceeded.
3. **Hard-gate** (M5+): fail PRs that exceed approved regression budgets.

## 6.2 Default regression budgets

Proposed starting budgets (case-normalized):

- latency regression > 10% on p95 => warning,
- latency regression > 20% on p95 => fail (hard-gate phase),
- allocation regression > 15% => warning/fail by phase,
- determinism mismatch => immediate fail at all phases.

### 6.3 Waiver model

A waiver may be granted only with:

1. explicit rationale,
2. linked issue tracking remediation,
3. sunset date/milestone for removal.

---

## 7) Optimization playbook (when regressions happen)

Prioritized order:

1. algorithmic complexity issues (path splitting, join frequency),
2. hot allocation reduction,
3. metadata adapter caching,
4. domain-specific micro-optimizations,
5. low-level runtime tuning.

Rules:

- do not bypass provenance/diagnostics for speed,
- do not weaken determinism to reduce tail latency,
- quantify precision impact before merging optimization.

---

## 8) Policy preset performance envelopes

Define expected envelopes per preset:

- `fast`: lower precision, minimal path splitting, strict budgets.
- `balanced`: default developer experience target.
- `deep`: higher precision exploration, relaxed budgets.

Benchmark outputs must report preset context to avoid invalid comparisons.

---

## 9) Tooling and artifact outputs

Benchmark pipeline should publish:

1. machine-readable result file (`bench-results.v1.json`),
2. summarized markdown report with deltas,
3. trend dashboard data points,
4. links to correlated trace/provenance artifacts for sampled outliers.

Retention recommendation:

- keep summary results for all protected-branch runs,
- keep full raw samples for rolling window (e.g., 30 days),
- archive milestone-baseline runs permanently.

---

## 10) Milestone-aligned acceptance criteria

### M2 readiness

- core corpus defined and stable IDs assigned,
- instrumentation captures required metrics,
- baseline report generated in CI.

### M3 readiness

- soft-gate warnings enabled,
- regression triage workflow documented,
- at least one optimization postmortem completed.

### M5 readiness

- hard-gate thresholds active,
- benchmark trend history available,
- release checklist includes perf sign-off.

---

## 11) Open questions

1. Should we maintain separate baseline families for workstation vs CI environments?
2. What percentile/metric should be the primary release gate: p95 latency, timeout rate, or weighted composite?
3. How should dump-adapter variability be normalized across different snapshot sizes?
4. Which benchmark cases should be treated as “must not regress” sentinel scenarios?
