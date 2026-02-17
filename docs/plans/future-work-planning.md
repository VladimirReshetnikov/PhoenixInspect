# Future Work Planning

This document tracks forward-looking work for the interpreter and dump-time evaluation initiative.

It is intentionally split into:

- near-term roadmap (execution-oriented),
- strategic tracks (cross-cutting investments), and
- decision gates (questions we should answer before scaling scope).

---

## 1) Planning assumptions

1. We are still in conceptual design and architecture shaping.
2. We should prioritize leverage: foundations before broad feature surface.
3. Every new capability should preserve deterministic, bounded, and explainable execution.

---

## 2) Milestone roadmap (proposed)

## M0 — Architecture baseline and contracts

**Goal:** lock core interfaces and execution model boundaries.

**Deliverables**

- Core interfaces for value domains, memory models, call dispatch, and metadata providers.
- Canonical execution state model and error/diagnostic envelopes.
- Initial “unknown value provenance” data contract.

**Exit criteria**

- Can run no-op/smoke interpretation across basic IL bodies.
- API review sign-off on core abstractions.

---

## M1 — Concrete + hybrid stepping MVP

**Goal:** execute straightforward IL while tolerating unknown inputs.

**Deliverables**

- Instruction semantics for fundamental opcode families.
- Concrete value domain + hybrid unknown-aware value domain.
- Deterministic budgets and cancellation support.
- Basic call-model fallback (`block`, `unknown return`, `havoc`).

**Exit criteria**

- Demonstrated end-to-end stepping on representative methods.
- Trace events show where unknownness is introduced.

---

## M2 — CFG and fixpoint abstract interpretation

**Goal:** perform whole-method over-approx analysis with joins/widening.

**Deliverables**

- CFG builder for method bodies (with phased EH handling).
- Worklist/fixpoint engine.
- Initial abstract domains (nullness and constants).
- Convergence controls (join/widen policy hooks).

**Exit criteria**

- Stable fixpoint on selected real-world methods.
- Invariants available at block/offset granularity.

---

## M3 — Call modeling and effects maturity

**Goal:** improve practical precision by taming call boundaries.

**Deliverables**

- Intrinsic models for high-value BCL methods.
- Effect model (`reads`, `writes`, `allocates`, `throws`, `impure`).
- Method summary format for reusable call summaries.

**Exit criteria**

- Reduced unknown propagation in benchmark corpus.
- Explainable blocked/approximated call diagnostics.

---


## M3.5 — Virtual step-debugging control plane

**Goal:** provide debugger-grade virtual stepping semantics on top of the interpreter core.

**Deliverables**

- Session model with explicit machine/call-stack state and stable pause reasons.
- Step-command controller for `StepInto`, `StepOver`, `StepOut`, and branch decisions.
- Stop-point history with `Undo` and branch-aware timeline semantics.
- Source mapping pipeline (PDB first, decompiler map second, IL fallback).

**Exit criteria**

- End-to-end virtual stepping demo over curated dump-backed scenarios.
- Deterministic replay of identical step-command sequences.
- Decision-needed flow for unknown branch conditions is host-visible and actionable.

---

## M4 — Dump-aware hosting integration prototype

**Goal:** prove the engine works against snapshot-backed metadata/memory.

**Deliverables**

- Dump-backed metadata provider adapter.
- Read-only memory model integration.
- Host-facing evaluation API suitable for watch/immediate-style usage.

**Exit criteria**

- Demonstrable dump-time expression evaluation for curated scenarios.
- Clear trust labels (pure/partial/blocked/timed out).

---

## M5 — Hardening and developer experience

**Goal:** make the system maintainable and contributor-friendly.

**Deliverables**

- Comprehensive test matrix and regression corpus.
- Performance baselines with targeted optimization plan.
- Contributor docs, architecture diagrams, and extension samples.

**Exit criteria**

- Predictable CI signal and acceptable performance envelope.
- Onboarding path for new contributors is documented and validated.

---

## 3) Cross-cutting strategic tracks

### Track A: Explainability and trust

- Rich provenance for unknown values.
- User-facing explanation messages derived from trace events.
- Consistent severity taxonomy for approximation events.

### Track B: Precision vs performance controls

- Policy presets (`fast`, `balanced`, `deep`).
- Domain-specific widening strategies.
- Optional path splitting limits and heuristics.

### Track C: Extensibility model

- Stable extension points for custom domains and call models.
- Versioned plugin contracts.
- Compatibility guidance for external contributors.

### Track D: Reliability and operability

- Deterministic replay of execution traces.
- Metrics for convergence and precision degradation.
- Failure mode catalog and recovery guidance.

---

## 4) Priority backlog candidates

### High priority

1. Define canonical unknown provenance schema.
2. Specify effect lattice and call-model contract.
3. Create opcode support matrix with implementation status.
4. Build initial benchmark corpus from realistic IL methods.
5. Specify virtual debug session contracts (step commands, stop reasons, undo/branch behavior).

### Medium priority

1. Add visualization helpers for state diffs across CFG blocks.
2. Draft method summary serialization format.
3. Introduce policy presets and tuning docs.

### Lower priority (but valuable)

1. Property-based IL fuzzing framework.
2. Optional symbolic constraints plugin prototype.
3. Experimental query DSL for dump-time workflows.

---

## 5) Risks and mitigations

## Risk: Scope expansion outruns architecture stability

**Mitigation**

- Gate user-visible features behind milestone exit criteria.
- Enforce architecture review before adding major surface area.

## Risk: Call modeling becomes ad hoc and inconsistent

**Mitigation**

- Define standardized call-model metadata and review process.
- Prefer reusable summaries/intrinsics over one-off special cases.

## Risk: Precision complaints without actionable diagnostics

**Mitigation**

- Make provenance mandatory for unknown introductions.
- Maintain clear taxonomy for blocked vs approximated behavior.

## Risk: Performance regressions in abstract interpretation mode

**Mitigation**

- Track benchmark deltas per PR for core hot paths.
- Add convergence watchdogs and tune widening defaults.

---

## 6) Decision gates

Before moving beyond M2, explicitly decide:

1. Which metadata backend(s) are first-class in v1.
2. Whether EH is mandatory for first public preview.
3. Which domains are “in the box” vs extension-only.
4. What host surfaces (CLI, SDK-only, IDE integration) are first release targets.

Before moving beyond M4, explicitly decide:

1. Public API stabilization policy.
2. Minimum compatibility target (`net8.0` only vs multi-targeting).
3. What telemetry/diagnostic artifacts are enabled by default.
4. Whether modeled calls are always represented as pseudo-frames or may be collapsed per policy.

---

## 7) Documentation follow-ups

To keep design docs coherent, add or maintain the following companion docs:

- `docs/proposals/architecture/architecture-overview-proposal.md` (high-level system map)
- `docs/proposals/architecture/state-and-domain-model-proposal.md` (formal semantics of value/memory domains)
- `docs/proposals/architecture/call-model-and-effects.md` (contracts and intrinsic policy)
- `docs/proposals/architecture/testing-strategy-proposal.md` (test taxonomy and quality gates)
- `docs/proposals/architecture/perf-and-benchmarks-proposal.md` (benchmark plan and acceptance thresholds)
- `docs/proposals/architecture/virtual-step-debugging-implementation-proposal.md` (session/stepping control-plane contracts)

Status note: `architecture-overview-proposal.md`, `state-and-domain-model-proposal.md`, `testing-strategy-proposal.md`, and `perf-and-benchmarks-proposal.md` are now present and should be iterated rather than re-proposed.
