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

## 2.1) Immediate alignment updates from virtual-stepping proposals

Based on the latest product + implementation proposals for virtual stepping, the following cross-document updates are now required and should be treated as near-term planning work:

1. Promote `MachineState`/`FrameState` as canonical terminology across architecture docs (deprecate single-frame `ExecState` as host contract wording).
2. Standardize one stop-reason taxonomy (`StepComplete`, `DecisionNeeded`, `ExceptionStop`, `BudgetStop`, `Completed`) across host APIs, testing docs, and replay artifacts.
3. Lock policy terminology for branch/call handling (`StopForUserChoice` vs fork/join modes; `Interpret` vs `Model` vs `Stop`).
4. Add explicit EH-phasing gate: MVP must at least support `stop-on-throw`; debugger-grade handler transfer is a later milestone requirement.
5. Require source-step mapping fallback order (PDB -> decompiler -> IL) in all UX and integration proposals.
6. Define a single artifact-resolution contract (`ModuleId`, artifact provenance, deterministic miss reasons) shared by ClrMD and PE/PDB integration layers.

## 2.2) Immediate alignment updates from async + dynamic proposals

Based on the new `virtual-tasks` and `dynamic-calls` architecture proposals, the following cross-document updates are now required:

1. Extend the call-classification contract to include lifted semantic callsites (`DynamicDispatch` and `AsyncRuntimeIntrinsic`) instead of treating them as opaque fallback calls.
2. Standardize a shared diagnostic taxonomy for dynamic binding and async scheduling outcomes (`Resolved`, `Ambiguous`, `Unresolved`, `MetaObjectRequired`, `AwaitPending`, `ContinuationResumed`, `TaskCompleted`, `TaskFaulted`, `TaskCanceled`).
3. Update state-model terminology to include virtual async entities (`VirtualTaskState`, continuation queue/scheduler state, and await-point provenance) as first-class machine-state components.
4. Clarify debugger control-plane behavior for async and dynamic decision points:
   - dynamic unresolved multi-target dispatch may raise `DecisionNeeded` for Step Into,
   - async suspension/resume points should emit stable stop/trace events that map to user-method frames.
5. Expand integration contracts for required artifacts and metadata:
   - async method/state-machine mapping (`AsyncStateMachineAttribute`, PDB `StateMachineMethod`),
   - runtime binding type reconstruction for dynamic call arguments.
6. Expand benchmark + test corpus requirements with dedicated async/dynamic fixture sets and determinism checks for virtual-scheduler replay.

---

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

- Session model with explicit `MachineState`/`FrameState` contracts and stable pause reasons.
- Step-command controller for `StepInto`, `StepOver`, `StepOut`, `Resume`, and branch decisions (`StopForUserChoice`/fork/join).
- Stop-point history with `Undo` and branch-aware timeline semantics.
- Source mapping pipeline (PDB first, decompiler map second, IL fallback).
- Session transcript contract (commands, stop reasons, event batches, and step diffs) for deterministic replay and host hydration.

**Exit criteria**

- End-to-end virtual stepping demo over curated dump-backed scenarios.
- Deterministic replay of identical step-command sequences.
- Decision-needed flow for unknown branch conditions is host-visible and actionable.
- Model-call behavior is explicit (`pseudo-frame` or `atomic effect event`) and policy-configured.

---

## M3.6 — Async + dynamic semantic lifting integration

**Goal:** operationalize the new virtual-task and dynamic-dispatch designs as stable interpreter capabilities.

**Deliverables**

- Lifted callsite classification pipeline (`DynamicDispatch`, `AsyncRuntimeIntrinsic`) wired into call-model policy and trace events.
- Virtual async runtime state integrated into canonical `MachineState` contracts (task store, continuation queue, await-point provenance).
- Deterministic decision protocol for unresolved dynamic Step Into (`DecisionNeeded` with candidate metadata).
- Async and dynamic fixture corpus added to replay/perf/test suites with deterministic transcript assertions.

**Exit criteria**

- Curated async fixtures emit stable `AwaitPending -> ContinuationResumed -> Task*` lifecycle traces across replay runs.
- Curated dynamic fixtures emit stable binder outcomes with explicit ambiguity/unresolved diagnostics.
- Host APIs and docs expose one shared taxonomy for async/dynamic outcomes and decision points.

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
6. Define a step-diff schema (`locals/stack/memory/effects/unknowns`) shared across UI and replay tests.
7. Specify symbol/source acquisition policy defaults (offline-first vs auto-download) and user-consent UX hooks for hosts.

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
- `docs/proposals/architecture/debug-map-design-proposal.md` (debug-map schema and fallback mapping rules)
- `docs/proposals/architecture/dynamic-calls-proposal.md` (dynamic call-site lifting and overload-resolution policy)
- `docs/proposals/architecture/virtual-tasks-proposal.md` (async/await virtualization and scheduler semantics)
- `docs/proposals/architecture/testing-strategy-proposal.md` (test taxonomy and quality gates)
- `docs/proposals/architecture/perf-and-benchmarks-proposal.md` (benchmark plan and acceptance thresholds)
- `docs/proposals/architecture/virtual-step-debugging-implementation-proposal.md` (session/stepping control-plane contracts)

Status note: `architecture-overview-proposal.md`, `state-and-domain-model-proposal.md`, `debug-map-design-proposal.md`, `dynamic-calls-proposal.md`, `virtual-tasks-proposal.md`, `testing-strategy-proposal.md`, and `perf-and-benchmarks-proposal.md` are now present and should be iterated rather than re-proposed.
