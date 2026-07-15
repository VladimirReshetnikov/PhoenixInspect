# State and Domain Model Proposal

## Status

Supporting design for W3/W4. The closed W3 concrete state/domain contract is implemented and locally verified at
hardened checkpoint `19c292f9f`; all four jobs also passed in [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally closed at exact
documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit. W4.1–W4.4 are now implemented. Exact W4.2 commit `e89e43498` adds the dump-free
provenance-aware `Int32` arithmetic kernel; exact W4.3 commit `7479b1ad4` adds structured non-exact field continuation
through that same machine; and W4.4 checkpoints `2e596c117`/`742ef2c4f` add complete direct-MethodDef graph
preparation without changing machine state. W4.4's headless local evidence passed the strict fifteen-project Release
build, planner 35/35, fixture 6/6, complete unit 250/250, fast 73/73, ordinary dump 5/5, optimized-context dump 1/1,
and both documentation guards with zero skips under `Scope!=Cybersecurity`. W4.4 realizes 3,651 added LOC (2,076
production plus 1,575 tests), split 1,043/2,608, bringing W4 to 10,679 realized LOC and a 21,179–26,779 LOC projection
while the original 16,860–25,310 baseline remains preserved. W4.5–W4.9 and fixpoint, async, dynamic, and virtual-debug
state remain pending or gated research.

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

The implemented W3 subset is normatively defined by the
[Concrete IL Execution Contract](concrete-il-execution-contract-proposal.md). W4.2 extends that machine with an
optional precision capability, an opt-in explained-`Int32` policy, and dump-free provenance-aware arithmetic. W4.3
adds a second optional capability for structured partial/unavailable ordinary-instance `Int32` field loads plus their
precision event and canonical lineage. W4.4 adds a separate body-independent target and immutable graph-preparation
contract that freezes complete definitions, typed boundaries, canonical dependencies, and required logical depth; it
does not change `MachineState` or execute a call. Broader shapes in this document are research generalizations, not
claims that the current machine supports CFG joins, call transfer, writes, EH, a counterfactual product surface, or a
dump adapter that produces W4.3 evidence.

---

## 1) Design goals

1. **Deterministic semantics** for both concrete and abstract stepping.
2. **Monotonic domain behavior** so fixpoint mode converges under explicit policies.
3. **Explainable uncertainty** via first-class provenance on unknown values.
4. **Composable extension points** so domains can evolve independently.
5. **Host-safe envelopes** so every approximation is externally visible.

---

## 2) Canonical execution state

The implemented W3 runtime state is a **machine state with an explicit frame stack**, not a single-frame `ExecState`:

```text
MachineState<TValue,TMemory> = {
  CallStack: immutable list<FrameState<TValue>>,
  Memory: persistent TMemory,
  ReturnValue?: TValue,
  TerminalTargetException?: TargetExceptionInfo
}

MachineOperationalState = {
  Budget: deterministic instruction budget
}
```

Budget and events are not fields of semantic state. A normal root return empties the call stack and optionally sets
`ReturnValue`; typed-null `ldfld` empties it and sets `TerminalTargetException`. Re-stepping either terminal shape
cannot execute an instruction. The following is the candidate research superset for later analysis/debugging work:

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

The current `FrameState` contains the structural method handle, IL offset, immutable evaluation stack, locals, and
arguments. An instance receiver occupies argument slot zero; W3 has no separate `This` cell. `ActivateRoot` derives
the complete slot vectors from one atomic method/signature/local projection, initializes admitted locals through the
domain default operation, and creates exactly one frame at offset zero. The larger control/data shape below is for
later stepping commands.

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
- In W3, the frozen admitted plan must also prove the complete evaluation-stack type vector at that boundary; depth
  alone is insufficient.
- W3 rejects nested/resumed frames that do not match its single-root profile before budget or memory activity.
- Top-frame `JustEntered` is set exactly on the micro-step that pushes a new frame.
- `ReturnSite` is required for non-root frames so `StepOut` can stop at caller-correct boundaries.

### 2.2 EvalStack

The W3 evaluation stack is an immutable ordered sequence of domain values. Its static types and CLI stack kinds are
checked against the frozen admitted boundary on activation/resume and before/after transfers. The provenance-bearing
`StackSlot` shape below is a later generalization.

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

W3 arguments and locals are immutable ordered vectors with exact metadata-derived `TypeSig` vectors in the admitted
plan. E1 positively covers compact, short, and long argument/local encodings over those vectors. E2 requires the exact
one-byte compact `ldarg.0` receiver load; equivalent `ldarg.s 0` and long `ldarg 0` forms are negative admission cases.
Callers cannot seed counts, local values, or return disposition. Index-addressed maps with richer cells are a later
generalization.

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

The active machine carries an `IPersistentMemoryState<TSelf>` snapshot and calls only its injected
`IMemoryModel<TValue,TMemory>`. `ConcreteMemory` uses deterministic internal reference identities rather than raw
target addresses. Newly allocated objects have CLI default cells; imported dump objects retain a stable external
evidence identity and fields not explicitly imported from exact evidence remain unavailable. The abstract heap shape
below is a later generalization.

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

In W3, successful ordinary instructions consume one unit and emit one `InstructionExecuted` event. Exact target-null
`ldfld` also consumes one unit but emits only `TargetExceptionRaised`; admission/evidence failures and pre-instruction
budget exhaustion consume nothing and emit nothing. All budget decrements and deterministic-ID allocations must happen
at documented transfer points so command replay remains stable. Operational context is not part of semantic equality
and must never be joined or widened at CFG merge points. Otherwise decreasing budgets, growing traces, allocation
order, and traversal-dependent provenance IDs can prevent convergence or make semantic equality unstable.

W4.4 graph preparation occurs before activation and exposes no semantic or operational machine state. Its
`TraversalUnitCount` records distinct methods, fields, and direct-call edges beneath fixed internal safety ceilings;
it is neither instruction budget nor W4.8's configurable product traversal budget. A failure returns no partial graph,
does not consume machine budget, and emits no debug events. `RequiredLogicalDepth` is a frozen graph fact; W4.5 owns
request-limit enforcement and observed frame-depth accounting.

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

#### Implemented W4.2–W4.3 concrete realization

`IValuePrecisionDomain<TValue>` is an optional extension of `IValueDomain<TValue>` that classifies executable values
as `Exact`, `ExplainedUnknown`, or `UnexplainedUnknown`. `UnknownExecutionPolicy.ExactOnly` is the default and preserves
W3. `ExplainedInt32` opts into only structurally typed, domain-validated explained `Int32` values; bottom and bare top
remain non-executable.

`FieldLoadEvidence` v1 is the immutable canonical boundary for one partial or unavailable ordinary-instance `Int32`
observation. It retains the frozen-plan dependency ordinal, complete field descriptor, evidence status and reason,
source and imported-object SHA-256 identities, nonzero read address, exact four-byte request, and copied observed
prefix. `MemoryLoadResult<TValue>.FromFieldEvidence` is the structured union producer. A load can continue only when
that evidence matches the frozen field and result status, the policy is `ExplainedInt32`, and the domain also
implements `IFieldLoadApproximationDomain<TValue>`. Exact loads retain their W3 behavior; code-only non-exact results,
missing policy/capability, conflict, invalidity, or mismatched evidence cannot manufacture a value.

`ProvenanceConcreteDomain` wraps the existing lifted-flat `ConcreteDomain`. `ProvenanceConcreteValue` keeps semantic
value and optional explanation root separate: equality, hashing, order, join, meet, and widening inspect only the
semantic value. Thus all same-typed unknowns still denote one lattice top even when their explanations differ.

### 3.3 Unknown payload and provenance

At product boundaries, an executable unknown is not plain `Top`; it requires provenance. W4.2–W4.3 realize that rule
with a separate canonical lineage DAG rather than the aspirational monolithic payload below. Its closed node set is
`InputOrigin`, ordered `BinaryTransform`, and `FieldLoadTransform`. A W4.3 imported field creates an `ImportedField`
origin whose source key is the complete `FieldLoadEvidence` digest, followed by a field transform containing the
imported-receiver digest, complete frozen field, and origin predecessor. The transform excludes process-local object
numbers, display names, and raw addresses. `FieldLoadTransform` is append-only node kind 3 under the existing schema;
the hard-coded W4.2 input/binary canonical bytes and IDs are unchanged.

Node IDs are SHA-256 hashes of versioned canonical bytes; binary transforms embed exact `Int32` operands and reference
explained-unknown predecessors. Field origin/transform interning preflights both nodes before mutating the domain.
Reachable-only `CaptureLineage` and `ReplayLineage` prevalidation of canonical bytes, IDs, ordering, reachability,
dependency shape, and field-origin relationships preserve the canonical node set, root, and graph fingerprint across
domain instances without partially mutating a destination on rejection.

The richer payload sketch remains a future product/controller vocabulary:

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

The implemented low-level transfer returns `StepOutcome<TValue,TMemory>` containing resulting semantic state,
operational budget, `MachineRunStatus`, ordered events, and either a structured execution failure or target exception.
Statuses are `Ready`, `Completed`, `BudgetExhausted`, `Blocked`, `InvalidProgram`, and `TargetException`. Admission,
resolution, domain, and non-continuing memory inability preserve semantic state, memory, budget, and events. W4.3's
one successful exception to that inability rule is a validated structured partial/unavailable field result: the
read-only instruction completes with an explained `Int32` unknown and unchanged memory. Capability exceptions other
than out-of-memory and stack-overflow are normalized without copying provider exception text.

The following `SessionTransition` is a future controller result layered over one or more low-level outcomes:

```text
SessionTransition =
  | Single(NextState: MachineState, Events: list<DebugEvent>)
  | Fork(NextStates: list<MachineState>, BranchInfo)
  | Stop(SameState: MachineState, Reason: SessionPauseReason, Events: list<DebugEvent>)

SessionPauseReason = {StepComplete | DecisionNeeded | ExceptionStop | BudgetStop | Cancelled | Completed}
```

This is a future session-controller protocol layered over low-level `StepOutcome`/`MachineRunStatus`; it does not
rename machine outcomes. The canonical mapping is in `architecture-overview-proposal.md`.

Rules for the future controller and product layers follow. The W4.2 kernel reuses the existing argument/local load,
local store, arithmetic, and return handlers and their instruction events; W4.3 extends the existing `ldfld` transfer
rather than introducing a second pipeline:

1. W4.4 prepares only exact direct `call` edges; W4.5 must push an interpreted frame as a discrete, observable event
   (the callee body does not execute in the same micro-step). `callvirt` and `newobj` remain unadmitted.
2. W4.3 field precision loss emits `InstructionExecuted` first and then `ValuePrecisionLost` at the same method and IL
   offset; the latter carries the exact `FieldLoadEvidence`, and the pushed value carries the corresponding
   provenance root. Any later source of unknownness must define an equally truthful concrete event contract rather
   than assuming an unimplemented generic `ApproxEvent`.
3. `DecisionNeeded` is host-resumable via explicit branch choice or policy-specified join/fork behavior.
4. Interpreter-internal invalidity still uses diagnostic failures, but should not be conflated with target-program exception flow (`ExceptionStop`).
5. W3's typed-null `ldfld` is a terminal, non-resumable machine target exception; handler transfer and general
   stop-on-throw remain later work.

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

## 8) Implemented constraints and deferred capabilities

Implemented through W4.4:

- exact `Int32`, structural object references, typed null, and lifted-flat per-type top/bottom values,
- a persistent memory snapshot contract with allocated defaults and exact imported-field absence,
- tested partial-order/join/meet/widen laws,
- deterministic budget/event accounting,
- metadata-derived activation and frozen typed whole-body admission,
- exact direct/constant-adjusted getter `ldfld` through a typed memory-result contract, and
- terminal null-reference outcome, plus separate same/fresh-session replay of successful execution outcomes;
- optional `IValuePrecisionDomain<TValue>` classification and exact-only-by-default execution policy;
- explained `Int32` transport through existing argument/local/store/arithmetic/return handlers;
- canonical `InputOrigin`/`BinaryTransform` lineage identities with reachable capture and fresh-domain replay;
- canonical `FieldLoadEvidence` v1 and its exclusive structured `MemoryLoadResult.FromFieldEvidence` union arm;
- optional `IFieldLoadApproximationDomain<TValue>` continuation gated jointly by structured evidence,
  `ExplainedInt32`, and domain capability;
- successful partial/unavailable ordinary-instance `Int32` `ldfld` continuation with ordered
  `InstructionExecuted`/`ValuePrecisionLost` events and unchanged memory; and
- append-only `FieldLoadTransform` kind 3 plus `ImportedField` origins, atomic pair interning, and replay prevalidation
  without changing W4.2 canonical identities;
- body-independent content-equal direct-MethodDef target/signature projection with ordinary managed-IL proof before
  body acquisition; and
- deterministic complete rooted-acyclic graph preparation with definition/signature correlation, typed boundaries,
  canonical nodes/fields/call sites, shared-callee deduplication, required logical depth, fixed internal safety usage,
  and no partial plan on failure.

Deferred to W4.5–W4.9 or later research gates:

- hybrid nullness/constant/type/taint products,
- direct-call frame execution, configured depth enforcement, and call-transform lineage,
- call models, counterfactual request/plan/result and facade, and generated-dump product closure,
- a ClrMD producer for structured W4.3 field evidence,
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
4. Should later call/model provenance extend W4.3's canonical acyclic DAG or introduce a separately versioned graph?
5. Should modeled calls always appear as model frames in history, or may policy collapse them into atomic events?

---

## 10) Proposed acceptance criteria

W3 satisfies the concrete machine-state, persistent-memory, deterministic budget/event, and lattice-law portions of
this proposal locally at hardened implementation checkpoint `19c292f9f`. W4.2 satisfies the dump-free
provenance-bearing branchless-arithmetic portion at exact implementation commit `e89e43498`; W4.3 satisfies the
dump-free structured non-exact field-transfer, precision-event, and lineage/replay portion at exact implementation
commit `7479b1ad4`; and W4.4 satisfies complete dump-free direct-call graph preparation at exact checkpoints
`2e596c117`/`742ef2c4f`. None is an end-to-end dump product, and W4.4 does not execute calls. The broader research
proposal is ready for sign-off when:

1. Core interfaces include explicit `MachineState`/`FrameState`, while any session controller keeps its transition and pause protocol distinct from the machine result.
2. At least one product-level dump sample can emit and replay provenance-bearing unknowns; W4.2–W4.4 prove only the
   dump-free domain, evidence, machine, and preparation seams, and the existing ClrMD field descriptor remains exact-only.
3. Merge/join behavior is validated on a curated CFG fixture set.
4. Host API can surface session pause reason, machine status, debug events, and approximation diagnostics without conflating their vocabularies or leaking internal types.
