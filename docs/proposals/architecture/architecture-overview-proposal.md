# Architecture Overview

## 1. Purpose and scope

This document provides a high-level but implementation-oriented map of the system architecture for the IL interpreter and dump-time evaluation platform.

It complements lower-level documents by answering:

- what major components exist,
- what contracts are stable between them,
- what data flows through those boundaries,
- where policy is configured,
- and where capability should be added over milestones.

This is a design map, not a full interface reference. Detailed semantics remain in the companion proposals.

---

## 2. System context

The platform evaluates .NET IL methods in two broad operating contexts:

1. **Live-host or test execution context**
   - metadata and method bodies are fully available,
   - memory may be virtualized in-process,
   - useful for semantics validation and concrete/hybrid stepping.

2. **Dump/snapshot context**
   - metadata may be partially available,
   - object graph reads are read-only and incomplete,
   - external calls are frequently blocked or approximated,
   - explainability and trust labels are first-class outputs.

Across both contexts, architecture must preserve deterministic, bounded, and explainable behavior.

---

## 3. Top-level component model

```text
+-----------------------------------------------------------------------------------+
| Host Surface (CLI / SDK / Debugger integration / automated analysis pipeline)     |
+----------------------------------------------+------------------------------------+
                                               |
                                               v
+-----------------------------------------------------------------------------------+
| Evaluation Orchestrator                                                           |
| - request shaping (expression/method/policy)                                     |
| - budget + cancellation wiring                                                    |
| - mode selection (concrete/hybrid/abstract)                                      |
| - trust label synthesis + diagnostic packaging                                   |
+----------------------------------------------+------------------------------------+
                                               |
                       +-----------------------+------------------------+
                       |                                                |
                       v                                                v
+---------------------------------------+               +-----------------------------------+
| Semantics Runtime                     |               | Analysis Runtime                   |
| - IL stack machine stepper            |               | - CFG builder                      |
| - opcode transfer semantics           |               | - fixpoint engine                  |
| - call dispatch and effect emission   |               | - join/widen strategy              |
| - lifted semantic sites (`dynamic`/`async`) |        | - path split/join policies         |
+--------------------+------------------+               +------------------+----------------+
                     |                                                     |
                     +------------------------+----------------------------+
                                              |
                                              v
+-----------------------------------------------------------------------------------+
| Shared Execution State and Domain Layer                                           |
| - value domain(s), memory model(s), provenance, diagnostics, effects              |
+----------------------------------------------+------------------------------------+
                                               |
                                               v
+-----------------------------------------------------------------------------------+
| Environment Adapters                                                               |
| - metadata + IL body providers                                                    |
| - type/generic context resolver                                                   |
| - dump-backed memory readers + overlay writes                                     |
| - intrinsic/model registry                                                         |
+----------------------------------------------+------------------------------------+
                                               |
                                               v
+-----------------------------------------------------------------------------------+
| Data Sources                                                                        |
| PE/PDB, metadata tables, CLRMD dump artifacts, host policy config, caches         |
+-----------------------------------------------------------------------------------+
```

---

## 4. Core subsystems and responsibilities

### 4.1 Host Surface

The host-facing API should remain narrow and policy-driven:

- Submit request (`EvaluateMethod`, `EvaluateExpression`, `AnalyzeMethod`, `StartVirtualDebugSession`).
- Select policy preset (`fast`, `balanced`, `deep`) with explicit overrides.
- Receive structured result:
  - status (`Completed`, `Partial`, `Blocked`, `TimedOut`, `DecisionNeeded`),
  - result value or abstract summary,
  - trace/provenance bundle,
  - trust label and explanation.

Host code should not access low-level interpreter internals directly.

### 4.2 Evaluation Orchestrator

This layer is the composition root. It:

- validates request shape,
- binds adapters for metadata/memory/model lookup,
- allocates deterministic ID spaces for unknown value provenance,
- enforces budgets (instruction steps, path splits, state joins, wall-clock guardrails),
- routes execution to semantics runtime or analysis runtime,
- merges diagnostics and converts them into user-facing trust labels.

### 4.3 Virtual Debug Control Plane

This subsystem coordinates debugger-like commands over interpreter state. It sits between host UI and semantics runtime.

Responsibilities:

- maintain a session-scoped machine state with explicit call stack and instruction pointers,
- translate `StepInto`/`StepOver`/`StepOut` into stop predicates over micro-steps,
- broker branch decisions (`choose true/false`, `fork`, `join`) when conditions are unknown,
- maintain stop-point history and undo/redo semantics,
- surface debugger events (`FramePushed`, `FramePopped`, `ExceptionThrown`, `BudgetExceeded`) with source mapping metadata.

This layer should stay policy-driven and avoid opcode semantics; it is a controller, not another interpreter.

### 4.3.1 Session lifecycle contract (refinement)

To align product-level virtual stepping semantics with engine behavior, the control plane should expose a stable command/result protocol.

Required commands:

- `StartVirtualSession` (from expression, method entry, or seeded dump frame),
- `StepInto`, `StepOver`, `StepOut`,
- `Resume` (continue-to-stop under current policy),
- `ChooseBranch` (when a branch decision is deferred to the host),
- `Undo` (move to prior history checkpoint),
- `ForkSession` / `CheckoutHistoryNode` (optional in first release, but model must not preclude it).

Required stop-reason categories:

- `StepComplete`,
- `DecisionNeeded`,
- `ExceptionStop`,
- `BudgetStop`,
- `Completed`.

Each stop result should include:

- deterministic state fingerprint,
- top-frame source location (PDB/decompiler/IL provenance),
- event batch emitted since prior command,
- summary diff (`locals`, `stack`, `overlay writes`, `effect events`, `unknown introductions`).

This contract is intentionally host-facing and mode-independent so the same surface can power CLI scripts, IDE UI, and regression replay harnesses.

### 4.4 Semantics Runtime

This runtime performs instruction-level transfer for single-state stepping.

Responsibilities:

- decode/cached instruction stream,
- maintain evaluation stack, locals, arguments, temporary state,
- execute opcode semantics against abstract domain interfaces,
- route call instructions through a unified special-semantics registry (call intrinsics, pattern intrinsics, projection intrinsics),
- recognize lifted semantic callsites (`DynamicDispatch`, `AsyncRuntimeIntrinsic`) before generic fallback handling,
- enforce modeled-call confidence labeling (`Exact`, `BestEffort`, `Partial`, `UnsupportedLayout`) as part of result metadata,
- emit effect and provenance events (including lifted-site resolution/scheduler lifecycle events).

Output is either a terminal state/value or bounded partial state on stop conditions.

### 4.5 Analysis Runtime

This runtime performs whole-method over-approximation via CFG and fixpoint iteration.

Responsibilities:

- materialize CFG and exception-flow edges according to configured support level,
- compute per-block and optional per-offset invariants,
- apply join and widening policies,
- track precision degradation events (e.g., widening applied, summary heap merge),
- produce reusable summaries for call modeling and cache hints.

### 4.6 Shared Execution State + Domain Layer

Domain-parametric semantics are centered here.

Stable contracts include:

- typed values with unknown/provenance payloads,
- memory abstraction (concrete, summary, dump overlay),
- effect lattice and emission API,
- diagnostic envelope (`IssueCode`, severity, location, recommendation),
- deterministic state hashing/fingerprints for caching and replay.

### 4.7 Environment Adapters

Adapters isolate external systems:

- metadata/token resolution,
- method body retrieval,
- generic context reconstruction,
- dump-backed object/field/array reads,
- `SessionSnapshot` extraction for deterministic environment/time intrinsics,
- projection layout decoder registration (with runtime-version support metadata),
- unified special-semantics registry and model summaries.

Adapters are replaceable; core runtimes remain backend-agnostic.


### 4.7.1 Projection overlays and decoder governance (refinement)

Projection-backed operations require explicit governance because private runtime layouts are version-sensitive.

Required contracts:

- Copy-on-write overlay semantics for dump-backed instances: reads may come from projection decoders, writes must be redirected to virtual overlay storage.
- Decoder identity in diagnostics (`decoder package`, `decoder version`, `runtime family/range`) so host output is auditable.
- Fail-closed behavior: when invariants fail, return `UnsupportedLayout` confidence with explicit diagnostics instead of speculative guesses.
- Projection budgets (max nodes/items/depth) with deterministic partial-result behavior when limits are exceeded.

These rules apply uniformly across collection projections and any future projection-heavy semantics.

### 4.8 Artifact resolution and debug-map pipeline

Virtual stepping quality depends on a dedicated artifact pipeline rather than ad hoc symbol lookups.

Required responsibilities:

- normalize module/method identity (`ModuleId` + token/spec identity) across ClrMD and PE readers,
- acquire PE/PDB artifacts through policy-guarded sources (local paths, cache, symbol server),
- produce `DebugMap` for each method with mandatory fallback order: `PDB -> decompiler map -> IL offsets`,
- provide source payload provenance (`real source`, `embedded source`, `decompiled`, `IL-only`) to host surfaces,
- resolve async state-machine projection metadata (`AsyncStateMachineAttribute`, PDB `StateMachineMethod`) to map `MoveNext` execution back to user-facing method/step locations,
- expose deterministic miss/failure diagnostics when symbols or source are unavailable.

This pipeline should be shared by both the virtual-debug control plane and any expression-evaluation hosts so line mapping, locals/scopes, and provenance labels remain consistent.

---

## 5. Canonical data flow

1. Host submits evaluation request with policy and budget (or a step command against an existing virtual-debug session).
2. Orchestrator binds adapters and validates preconditions.
3. Method body + metadata + generic context are resolved.
4. Artifact pipeline resolves debug map and best-available source (PDB/decompiler/IL) for host-visible stepping.
5. Initial state is created from arguments/locals and memory roots.
6. Runtime executes (single-state, micro-step loop, or CFG/fixpoint) under budget guards.
7. Calls are classified (`intrinsic`, `summary`, `reentrant`, `lifted semantic site`, `fallback`).
8. Lifted sites emit deterministic outcomes (dynamic binding diagnostics; async continuation/task lifecycle events).
9. Effects and provenance are recorded continuously.
10. Runtime exits with terminal/partial result, or pause reason (`StepComplete`, `DecisionNeeded`, `ExceptionStop`, `BudgetStop`, `Completed`).
11. Orchestrator computes trust label and assembles explanation payload.
12. Host receives stable result envelope.

---

## 6. Cross-cutting quality attributes and architecture decisions

### 6.1 Determinism

- Deterministic iteration order in worklists and map/set traversals.
- Stable unknown identity allocation.
- Stable serialization of diagnostics and provenance trees.

### 6.2 Boundedness and cancellation

- Independent counters for hot resources (steps, merges, forks, allocations).
- Cooperative cancellation checks in long-running loops.
- Deterministic stop reasons in result envelope.
- For virtual sessions, command-level budgets are resettable but auditable (include both per-command and cumulative counters).

### 6.3 Explainability

- Every unknown introduction has a cause code and source location.
- Every blocked action maps to a policy rationale.
- Trust label synthesis references concrete evidence in trace/events.
- Virtual-debug surfaces preserve provenance labels (dump-backed vs virtual overlay, interpreted vs modeled).
- Modeled calls surface as explicit pseudo-frame or explicit atomic effect event (policy-selected, never silent).

### 6.4 Extensibility

- New abstract domains can be composed without changing opcode handlers.
- New memory backends can be added via adapter interfaces.
- New intrinsic model packs can be versioned and independently tested.

### 6.5 Performance

- Cache decoded IL, CFG artifacts, and resolved signatures.
- Reuse object pools for transient state structures.
- Enable summary/result caches with explicit invalidation boundaries.

---

## 7. Deployment and packaging view (proposed)

Suggested package decomposition:

- `Interpreter.Core`
  - instruction semantics, state machine contracts, shared diagnostics.
- `Interpreter.Analysis`
  - CFG/fixpoint framework and abstract-analysis orchestration.
- `Interpreter.Models`
  - intrinsic call models and summary format.
- `Interpreter.Integration.ClrMd`
  - dump/metadata adapter layer.
- `Interpreter.Hosting`
  - host-facing request/response contracts and policy presets.

This split enables independent iteration while keeping stable boundaries explicit.

---

## 8. Milestone mapping

### M0–M1

- establish Orchestrator + Semantics Runtime + Shared State contracts,
- provide concrete/hybrid stepping with basic call fallback,
- emit minimal trust labels and provenance.

### M2–M3

- add explicit machine state/call-stack contracts for virtual stepping,
- introduce virtual debug control plane with stop reasons and branch-decision hooks,
- strengthen call summaries/effect model including model-frame behavior.
- add deterministic command/result transcript format for replay and UI hydration.

### M4–M5

- harden dump adapters and source-map backends (PDB/decompiler/IL fallback),
- stabilize hosting contracts for session lifecycle (`Start`, `Step`, `Undo`, `ChooseBranch`),
- expand diagnostics, replay tests, and benchmark-driven tuning.

---

## 9. Open design questions

1. Should trust-label synthesis happen only in Orchestrator, or partially in runtimes for richer context?
2. What minimal event schema enables both machine comparison (regression checks) and user-readable explanations?
3. Where should method-summary caches live: in `Analysis` package or shared `Models` package?
4. How strict should compatibility be between intrinsic model versions and engine minor versions?
5. What is the default fallback policy in dump mode for unknown external calls (`block` vs `unknown+havoc`)?
6. Should modeled calls always materialize as pseudo-frames, or can policy collapse them into atomic effects for performance?

---

## 10. Document maintenance notes

When companion proposals change materially (state model, call effects, integration boundaries), update this overview first to keep the architecture map coherent.

This document should remain concise enough to onboard new contributors, while still being concrete enough to guide package and interface decisions.
