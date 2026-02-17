# Testing Strategy Proposal

## Status

Draft

## Scope

This document defines the testing taxonomy, quality gates, and execution model for the interpreter project while it transitions from conceptual design to implementation.

It focuses on:

- semantic correctness of IL execution,
- soundness signals for abstract interpretation,
- determinism and budget behavior,
- explainability artifact quality.

---

## 1) Testing principles

1. **Determinism first**: identical inputs + policy must produce identical outputs/artifacts.
2. **Contract-level confidence**: prioritize tests that enforce interface semantics over implementation details.
3. **Explainability as testable output**: provenance and diagnostics are required artifacts, not optional logs.
4. **Layer isolation**: metadata/memory adapters should be testable without full-host integration.
5. **Progressive hardening**: start with golden fixtures, then add fuzzing and performance gates.

---

## 2) Test taxonomy

## A. Unit tests (domain-local)

Target:

- value-domain lattice laws,
- opcode transfer semantics,
- budget accounting,
- policy fallback mapping.

Examples:

- `join` idempotence for nullness domain,
- `ldc`, `ldloc`, `stloc`, arithmetic opcode edge behavior,
- `BudgetExceeded` generation at deterministic step boundary.

## B. Component tests (engine-internal integration)

Target:

- execution state transitions across multiple opcodes,
- branch merge behavior,
- unknown provenance propagation.

Examples:

- straight-line method with mixed concrete/unknown values,
- two-branch null-check merge with expected path facts,
- blocked call with configured `unknown return` fallback.

## C. Adapter contract tests (integration boundaries)

Target:

- metadata provider contract,
- read-only memory model behavior,
- symbol/type resolution fallbacks.

Examples:

- missing metadata token returns structured diagnostic,
- memory read failure yields provenance-tagged unknown,
- generic instantiation resolution failure is deterministic.

## D. End-to-end scenario tests

Target:

- host-facing evaluation flows,
- trust labels (`pure`, `partial`, `blocked`, `timed out`),
- diagnostic envelopes consumed by clients.

Examples:

- evaluate watch expression from snapshot-backed context,
- expression containing blocked impure call,
- timeout path with preserved partial trace.

## E. Non-functional tests

Target:

- performance envelopes,
- memory allocation ceilings,
- determinism/replay guarantees under load.

Examples:

- benchmark corpus regression checks,
- repeated-run trace hash equality,
- stress corpus with path-splitting limits.

---

## 3) Fixture strategy

### 3.1 IL fixture corpus

Maintain a curated corpus of method fixtures categorized by feature area:

- arithmetic/control-flow basics,
- object/array operations,
- generics and constrained calls,
- unsupported/edge opcode coverage,
- dump-specific metadata and memory gaps.

Each fixture includes:

1. IL snippet or compiled assembly reference,
2. policy preset,
3. expected status + diagnostics,
4. expected state invariants (not always exact raw values).

### 3.2 Golden artifacts

Use versioned golden files for stable external outputs:

- provenance graph,
- approximation event list,
- summarized final state,
- host response envelope.

Golden review rule:

- Every golden change must include rationale (semantic improvement vs behavior drift).

### 3.3 Differential fixtures

Where feasible, pair fixtures with baseline runtime behavior (when concretely executable) to detect semantic drift in straightforward cases.

---

## 4) Property-based and generative testing

Introduce property-based tests once core opcode support stabilizes (M2+).

Candidate properties:

1. **Lattice laws** hold for generated domain values.
2. **Join monotonicity** across randomly generated states.
3. **Deterministic stepping** for same seed/policy.
4. **Budget monotonicity**: remaining budget never increases unless explicitly reset.

Generator constraints:

- IL generation should remain verifiable/well-formed unless testing invalid-input handling.
- Generator metadata should include minimal shrinking hints for debugging failures.

---

## 5) Determinism and replay checks

Determinism is a release-blocking property.

Required checks:

1. Run selected scenario tests N times in isolated processes.
2. Canonicalize artifacts (ordering, IDs where appropriate).
3. Compare stable hashes of:
   - trace events,
   - final state summaries,
   - diagnostics envelopes.

Any hash mismatch is treated as a failure unless a nondeterministic field is explicitly allowed and documented.

---

## 6) Soundness and precision guardrails

Because abstract interpretation is an approximation, tests should enforce **soundness signals** rather than unattainable perfect precision.

Guardrails:

- no silent fallback to concrete guesses when blocked,
- every precision loss introduces explicit provenance,
- call-effects approximations follow declared effect lattice policy.

Precision scorecards (informational at first, gating later):

- unknown-propagation rate,
- unresolved-call rate,
- invariant coverage per block.

---

## 7) Performance and scalability gates

See companion benchmark proposal for full details; this section defines CI intent.

Gates by maturity:

- **M1–M2**: collect-only benchmarks, no fail gate.
- **M3–M4**: soft gate with warning threshold.
- **M5+**: hard gate for regressions beyond approved budget.

Track at minimum:

- execution time percentile on benchmark corpus,
- allocations/op for representative methods,
- fixpoint iteration count distributions.

---

## 8) CI matrix proposal

Minimum matrix dimensions:

1. Runtime target (`net8.0` baseline).
2. Policy preset (`fast`, `balanced`, `deep`).
3. Execution mode (concrete stepping, abstract fixpoint, hybrid).
4. Fixture profile (smoke, core, extended).

Recommended pipeline stages:

1. Lint/format/docs consistency.
2. Unit + component tests.
3. Scenario and adapter tests.
4. Determinism replay checks.
5. Performance job (scheduled or protected branch).

---

## 9) Failure triage model

When tests fail, classify into one of:

- `SemanticRegression`
- `DeterminismRegression`
- `ExplainabilityRegression`
- `PerformanceRegression`
- `InfrastructureFailure`

Required triage metadata in PR/issue:

- affected fixture IDs,
- first bad commit (if known),
- expected vs actual diagnostics,
- whether host-visible contract changed.

---

## 10) Exit criteria by milestone

### M0 exit (testing readiness)

- Unit tests for core state/domain interfaces exist.
- Baseline fixture harness can run smoke methods.

### M1 exit

- Core opcode families covered by component tests.
- Unknown provenance assertions present in fallback paths.

### M2 exit

- CFG merge/join fixtures and convergence watchdog tests pass.
- Determinism replay checks enabled for core scenarios.

### M3–M4 exit

- Call/effect model scenario coverage reaches agreed threshold.
- Adapter contract suite covers expected dump-time failure modes.

### M5 exit

- Performance gates active with documented thresholds.
- Regression corpus and contributor guidance are stable.

---

## 11) Open questions

1. Should golden artifacts be stored as full JSON snapshots or compact semantic assertions + generated snapshots?
2. At what point do we promote precision scorecards from informational to gating?
3. How large should the minimum public benchmark corpus be before preview release?
4. Which determinism fields (if any) may remain intentionally unstable?
