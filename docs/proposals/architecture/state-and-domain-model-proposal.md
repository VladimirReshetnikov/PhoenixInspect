# State and Domain Model Proposal

## Status

Supporting design for W3/W4. Concrete execution and lattice laws are active; fixpoint, async, dynamic, and virtual-debug state are gated research.

## Scope

This document defines the canonical execution state and abstract domain contracts for the IL interpreter.
It is intended to be the semantic companion to:

- `il-interpreter-framework-proposal.md` (runtime architecture),
- `mvp-abstract-domain-proposal.md` (initial domains and convergence policy), and
- `call-model-and-effects.md` (inter-procedural boundaries).

The focus here is on **formal-ish, implementation-guiding contracts**:

1. how interpreter state is represented,
2. how value/memory domains compose,
3. how unknownness and provenance are encoded,
4. how transfer functions communicate guarantees.

---

## 1) Design goals

1. **Deterministic semantics** for both concrete and abstract stepping.
2. **Monotonic domain behavior** so fixpoint mode converges under explicit policies.
3. **Explainable uncertainty** via first-class provenance on unknown values.
4. **Composable extension points** so domains can evolve independently.
5. **Host-safe envelopes** so every approximation is externally visible.

---

## 2) Canonical execution state

To support virtual step-debugging, the canonical runtime state is now a **machine state with an explicit frame stack**, not a single-frame `ExecState`.

```text
MachineState = {
  Semantic: SemanticState,
  Operational: OperationalContext
}

SemanticState = {
  CallStack: list<FrameState>,
  HeapState,
  PathFacts,
  PendingTargetException?,
  EffectSummary
}

OperationalContext = {
  TraceContext,
  BudgetState,
  DeterminismState,
  Cancellation,
  CachesAndMetrics
}
```

Compatibility note:

- A historical single-frame `ExecState` can still be used as an internal helper, but host-facing contracts, replay artifacts, and stepping semantics should be defined in terms of `MachineState`.

### 2.1 FrameState and ControlState

`FrameState` is the control/data unit used by stepping commands.

```text
FrameState = {
  MethodInstance,
  ILOffset,
  EvalStack,
  Locals,
  Arguments,
  This,
  ReturnSite?,
  EHState?,
  Flags: {JustEntered, IsModelFrame, HiddenNonUserCode}
}

ReturnSite = {
  CallerMethodInstance,
  CallerResumeOffset,
  CallerCallStatementId
}
```

Rules:

- `ILOffset` must correspond to a valid instruction boundary.
- Top-frame `JustEntered` is set exactly on the micro-step that pushes a new frame.
- `ReturnSite` is required for non-root frames so `StepOut` can stop at caller-correct boundaries.

### 2.2 EvalStack

The evaluation stack is an ordered sequence of `StackSlot` values.

```text
StackSlot = {
  TypeHint,
  Value: DomainValue,
  Origin: ValueOrigin
}
```

Rules:

- Stack shape is part of frame validity.
- Join between stack states requires equal depth in the merge scope.
- `TypeHint` can be broader than runtime type in abstract mode.

### 2.3 Locals and Arguments

Both are index-addressed maps with stable slot descriptors.

```text
Locals    : LocalIndex -> DomainCell
Arguments : ArgIndex   -> DomainCell

DomainCell = {
  DeclaredType,
  Value: DomainValue,
  Mutability: {Mutable | ReadOnly},
  LastWriteOrigin?
}
```

### 2.4 HeapState

Heap is represented through abstract locations, not raw addresses.

```text
HeapState = {
  Objects : ObjectRef -> ObjectState,
  Arrays  : ArrayRef  -> ArrayState,
  Aliases : AliasClassId -> set<LocationRef>
}
```

`ObjectState` and `ArrayState` contain domain values per field/element bucket and optional shape metadata.

MVP simplification:

- Read-only snapshot heaps may mark writes as blocked or havoced, based on policy.
- Fine-grained alias analysis is optional; coarse alias classes are acceptable initially.

### 2.5 PathFacts

Path-sensitive assumptions accumulated along control flow.

```text
PathFacts = {
  Predicates: set<PredicateFact>,
  NullFacts : map<SymbolRef, {Null | NonNull | Unknown}>,
  TypeFacts : map<SymbolRef, set<TypeConstraint>>
}
```

`PathFacts` are consumable by domains (nullness, constants, interval-like plugins).

### 2.6 Operational context: trace, budgets, and determinism

```text
TraceContext = {
  EventCursor,
  ApproximationEvents: list<ApproxEventRef>,
  UnknownIntroductions: list<UnknownId>,
  LastStepDiff?
}

BudgetState = {
  StepsRemaining,
  JoinBudgetRemaining?,
  WidenBudgetRemaining?,
  DeadlineUtc?
}

DeterminismState = {
  UnknownIdSeed,
  BranchDecisionLog,
  ReplayFingerprint
}
```

All budget decrements and deterministic-ID allocations must happen at documented transfer points so command replay remains stable. Operational context is not part of semantic equality and must never be joined or widened at CFG merge points. Otherwise decreasing budgets, growing traces, allocation order, and traversal-dependent provenance IDs can prevent convergence or make semantic equality unstable.

### 2.7 AsyncState and LiftedCallState (research)

If async/dynamic lifted semantics later pass their entry gates, semantic state may gain explicit virtual-runtime and lifted-call bookkeeping. These fields are not part of the active state contract.

```text
AsyncState = {
  VirtualTasks: map<VirtualTaskId, VirtualTaskState>,
  ContinuationQueue: queue<ContinuationToken>,
  AwaitProvenance: map<AwaitPointId, UnknownId?>
}

VirtualTaskState = {
  Status: {Created | Running | Waiting | RanToCompletion | Faulted | Canceled},
  ResultValue?,
  Fault?,
  AwaiterLinks: set<AwaitPointId>
}

LiftedCallState = {
  DynamicSites: map<DynamicSiteId, DynamicSiteRecord>,
  LastResolution?: DynamicResolutionRecord
}
```

Rules:

- `ContinuationQueue` dequeue order must be deterministic and replayable.
- `VirtualTaskState` transitions must be monotonic along valid lifecycle edges.
- `DynamicSiteRecord` must preserve candidate/selected target provenance for host explanation and transcript diffing.

---

## 3) Domain model

`DomainValue` is a tagged union that supports concrete and abstract payloads.

```text
DomainValue =
  | Bottom
  | Concrete(ConstantValue)
  | Abstract(AbstractPayload)
  | Top(UnknownPayload)
```

### 3.1 Lattice laws

Every domain implementation must supply:

- executable partial-order test `IsLessThanOrEqual(a, b)`,
- `join(a, b)`,
- `meet(a, b)`,
- `widen(prev, next)` when fixpoint acceleration is enabled.

Required properties:

1. `join` is commutative, associative, idempotent.
2. `a <= join(a, b)` and `b <= join(a, b)`.
3. `widen(prev, next)` must satisfy `next <= widen(prev, next)`.

If a domain cannot preserve these laws under a requested operation, it must return a structured domain failure diagnostic and trigger host policy fallback.

### 3.2 Product domains

Composite values use product-domain semantics.

```text
ProductValue = {
  NumericDomain,
  NullnessDomain,
  TypeDomain,
  TaintOrProvenanceDomain,
  ExtensionDomains...
}
```

Join/widen are field-wise unless policy states correlated widening.

### 3.3 Unknown payload and provenance

Unknown values are not plain `Top`; they carry provenance.

```text
UnknownPayload = {
  UnknownId,
  ReasonCode,
  Confidence: {CertainUnknown | PolicyUnknown | TimeoutUnknown},
  SourceSpan,
  RelatedCallSite?,
  EffectsObserved?: EffectSummaryRef,
  Notes?: map<string,string>
}
```

Minimum `ReasonCode` set for MVP:

- `UnsupportedOpcode`
- `BlockedCall`
- `MissingMetadata`
- `HeapReadUnavailable`
- `BudgetExceeded`
- `PolicyHavoc`
- `DynamicDispatchUnresolved`
- `DynamicDispatchAmbiguous`
- `AsyncAwaitPending`
- `AsyncContinuationResumed`
- `AsyncTaskFaulted`
- `AsyncTaskCanceled`

This enables precise host messaging without reverse-engineering trace logs.

---

## 4) Transfer-function contract

Each micro-step transfer returns a `MicroStepResult` and emits `DebugEvent` items used by the virtual debug control plane:

```text
SessionTransition =
  | Single(NextState: MachineState, Events: list<DebugEvent>)
  | Fork(NextStates: list<MachineState>, BranchInfo)
  | Stop(SameState: MachineState, Reason: SessionPauseReason, Events: list<DebugEvent>)

SessionPauseReason = {StepComplete | DecisionNeeded | ExceptionStop | BudgetStop | Cancelled | Completed}
```

This is a future session-controller protocol layered over low-level `StepOutcome`/`MachineRunStatus`; it does not
rename machine outcomes. The canonical mapping is in `architecture-overview-proposal.md`.

Rules:

1. `call/callvirt/newobj` that are interpreted must push a frame as a discrete, observable event (callee body does not execute in the same micro-step).
2. Any introduction of unknownness must emit both:
   - an `ApproxEvent`, and
   - a provenance-bearing `DomainValue`.
3. `DecisionNeeded` is host-resumable via explicit branch choice or policy-specified join/fork behavior.
4. Interpreter-internal invalidity still uses diagnostic failures, but should not be conflated with target-program exception flow (`ExceptionStop`).

---

## 5) State join semantics at CFG merge points

Given incoming semantic states `S1..Sn`, merged semantic state `Sm` is computed component-wise:

1. Validate stack shape compatibility.
2. Join locals/arguments slot-by-slot.
3. Join heap conservatively by abstract location.
4. Merge path facts with contradiction handling.
5. Merge semantic effects according to their lattice.

Budgets, cancellation, trace cursors, diagnostics already emitted, worklist order, provenance-ID allocation, caches, and metrics are deliberately excluded. Analysis orchestration retains them outside the lattice state.

Contradiction rule:

- If path-fact contradiction is detected, mark path as infeasible and optionally drop it before merge.
- If infeasibility cannot be proven, keep conservative facts and emit `PathFactPrecisionLoss`.

---

## 6) Policy hooks and host controls

The runtime policy object may tune semantics without changing domain law.

```text
ExecutionPolicy = {
  UnknownOnBlockedCall: bool,
  HavocOnImpureUnknownCall: bool,
  MaxTrackedArrayElements,
  MaxAliasSetSize,
  BranchDecisionMode: {StopForUserChoice | ForkBoth | JoinBoth | HeuristicPick},
  CallHandlingMode: {Interpret | Model | Stop},
  PathSplitLimit,
  JoinStrategy,
  WidenStrategy,
  StrictStackValidation
}
```

Policy must be part of trace metadata so replay/debug outputs are reproducible.

---

## 7) Serialization and diagnostics contract

To support tooling and offline comparison, `MachineState` snapshots should be serializable into stable schemas.

Recommended artifacts:

- `state-snapshot.v1.json` (selected checkpoints),
- `approx-events.v1.json` (all approximation events),
- `unknown-provenance.v1.json` (unknown lineage graph).

Versioning rules:

- Additive fields are minor-compatible.
- Semantic meaning changes require major version increment.

---

## 8) MVP constraints and deferred capabilities

In scope for the first concrete execution slice:

- concrete primitive values,
- a persistent memory snapshot contract,
- tested partial-order/join/meet/widen laws,
- deterministic budget accounting.

Deferred until the unknown-aware method slice or research gates:

- hybrid nullness/constant/type/taint products,
- provenance-bearing unknown values,
- coarse and summary heap abstractions,
- full relational numeric domains,
- high-precision alias analysis,
- full exception-region fixpoint semantics,
- cross-method summary caching with invalidation.

---

## 9) Open questions

1. How much of `EHState` is mandatory for first public virtual-stepping preview (`stop-on-throw` only vs handler transfer)?
2. Which later domains need a separate narrowing operator beyond `Meet`?
3. How aggressively should path-fact contradiction pruning run under `fast` policy presets?
4. Should unknown provenance graphs be DAG-enforced in v1, or permit cycles for simplicity?
5. Should modeled calls always appear as model frames in history, or may policy collapse them into atomic events?

---

## 10) Proposed acceptance criteria

This proposal is ready for sign-off when:

1. Core interfaces include explicit `MachineState`/`FrameState`, while any session controller keeps its transition and pause protocol distinct from the machine result.
2. At least one end-to-end sample can emit provenance-bearing unknowns.
3. Merge/join behavior is validated on a curated CFG fixture set.
4. Host API can surface session pause reason, machine status, debug events, and approximation diagnostics without conflating their vocabularies or leaking internal types.
