# Counterfactual Method Evaluation Contract

> **Lifecycle:** Current
>
> **Roadmap relation:** Active · W4 normative contract
>
> **Implementation status:** W4.1 fixture gate implemented at `82363585b`; W4.2 unknown E1/E2 kernel implemented at
> `e89e43498`; W4.3 structured field-evidence continuation implemented at `7479b1ad4`; W4.4a body-free direct
> MethodDef resolution implemented at `2e596c117`; W4.4b frozen transitive graph implemented at `742ef2c4f`;
> W4.5a exact prepared-graph execution implemented at `356c07037`; W4.5b–W4.9 pending

## 1) Purpose and authority

This document is the normative contract for W4's first product-facing method-evaluation increment. It is deliberately
narrow. It admits one branchless generated-dump workflow, provenance-bearing unknown continuation over that workflow,
one direct call shape, one mandatory pure-model conformance disposition, deterministic consuming budgets,
stop-on-throw, and a canonical host result. It does not adopt the broader call, abstract-analysis, virtual-debugger, or
semantic-registry research designs.

Where this document conflicts with a supporting proposal, this document controls W4. The W2
[Restricted Dump Query Contract](restricted-dump-query-contract-proposal.md) continues to control the existing query
surface, and the W3 [Concrete IL Execution Contract](concrete-il-execution-contract-proposal.md) remains the compatibility
floor for structural identity, whole-body admission, memory isolation, instruction accounting, and replay.

The contract is just ahead of code. W4.1's fixture/emitted-shape/CoreCLR/current-W3-boundary evidence, W4.2's unknown
E1/E2 domain kernel, and W4.3's backend-neutral structured field-evidence continuation are implemented. W4.2 realizes
the semantic-value rules, the `InputOrigin` and `BinaryTransform` subset of lineage, policy-enabled explained-`Int32`
execution over shared W3 handlers, and fresh-object lineage replay. W4.3 adds immutable field evidence, an optional
approximation capability, policy-gated partial/unavailable continuation, precision-loss events, and
`FieldLoadTransform` lineage while retaining exact and terminal memory outcomes. W4.4 now realizes body-independent
direct MethodDef/signature resolution and a canonical complete acyclic interpreted-method graph under fixed internal
safety caps. W4.5a executes the exact frozen interpreted graph through structural call/return boundaries, enforces a
machine-supplied logical-depth limit before activation, and records logical/frame high water without re-resolution.
It does not carry explained unknowns across call/return boundaries, select a model, enforce a request-supplied
traversal limit, or produce a product result. W4.5b and every later behavior remain requirements, not implementation
claims, until the traceability map names passing executable evidence. API names shown here are provisional design
names; public prototype APIs that land must carry detailed XML documentation.

## 2) Product-value gate

### 2.1 Incident question

The selected workflow starts from the generated, strongly rooted `DumpProbe` already used by W1–W3 and asks:

> Under the named counterfactual policy and the captured marker evidence, what outcome would
> `DumpProbe.GetMarkerSummary()` produce?

The fixture is fixed before semantic expansion:

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

[MethodImpl(MethodImplOptions.NoInlining)]
private static int CombineMarkers(int marker, int alternateMarker) =>
    unchecked(marker + alternateMarker);
```

For the current generated values, `Marker == 0x13579BDF` and `AlternateMarker == 0x13579BDE`; direct CoreCLR execution
therefore returns `0x26AF37BD` (`649017277`). CoreCLR is an outcome oracle only. It does not supply a body, signature,
field value, call target, or plan to the evaluator.

### 2.2 Why W2 cannot answer it

W2 binds one exact root and one direct field descriptor. Its plan may decode that one field and apply one typed literal
coalescing fallback. It cannot:

- select both `Marker` and `AlternateMarker` into one computation;
- invoke `GetMarkerSummary` or `CombineMarkers`;
- interpret user IL; or
- represent a call graph, assumptions, models, or an execution budget.

Adding arithmetic and call syntax to W2 would erase the boundary between `DerivedQuery` and
`CounterfactualExecution`. W4 therefore introduces a separate request, prepared plan, runner, and result rather than
silently widening the W2 grammar.

### 2.3 Honest evidence claim

This is a specified, application-shaped product workflow designed to become executable over the generated dump. W4.1
now proves the exact fixture shape, structural operand/signature closure, CoreCLR outcome, and current W3 rejection
boundary at pushed checkpoint `82363585b`. It does not execute the caller through the interpreter or acquire its body
from dump memory. The workflow is not a representative private-production incident corpus and establishes no
recoverability or readiness rate. Frame, local, and argument recovery remain unavailable; the future product receiver
comes from the exact rooted-object path.

Pushed W4.2 checkpoint `e89e43498` proves the dump-free unknown-aware kernel, not this rooted workflow. It neither
admits the caller's direct call nor imports non-exact dump-field evidence, constructs a product result, or claims a
dump-grounded counterfactual answer.

Pushed W4.3 checkpoint `7479b1ad4` proves the dump-free structured field-evidence continuation. Pushed W4.4a
checkpoint `2e596c117` proves body-free direct MethodDef/signature resolution, and pushed W4.4b checkpoint
`742ef2c4f` proves complete frozen graph preparation over the exact emitted fixture. The W4.4 graph contains the two
methods, both fields, and the call at IL offset 12. Pushed W4.5a checkpoint `356c07037` executes that exact frozen graph
through two metadata-derived frames without consulting a resolver, reproduces the exact CoreCLR answer, and retains
deterministic instruction, frame-event, return-site, and depth facts. This is still dump-free machine evidence: the
explained-unknown call/return lineage, model, product facade, and dump-grounded W4 answer remain pending.

## 3) Closed W4 profile

### 3.1 Admitted methods and values

The primary interpreted plan contains exactly the rooted instance method and its same-module static helper. Both
interpreted methods must be:

- exact non-generic `MethodDef` identities in the same snapshot-scoped module;
- managed, non-vararg, non-P/Invoke, non-runtime-supplied bodies;
- branchless and EH-free;
- free of loops, tail calls, constrained calls, function pointers, and prefixes;
- typed only with the W3 reference receiver plus exact CLI `Int32` parameters, locals, stack values, and return; and
- for the primary interpreted request, fully represented by exact counted metadata and method-memory evidence before
  execution. The mandatory pure-model conformance disposition follows the narrower modeled-target evidence rule in
  section 5.1.

The product root receiver must be one exact non-null imported object reference of the exact projected declaring type.
Dump-free differential conformance may also activate the same projected method on an exact typed-null receiver solely
to preserve and retest W3's target-null boundary; missing root evidence is never converted to that null. Root `Int32`
arguments may be exact or explicitly unknown only when the prepared request names their evidence origin. The selected
workflow has no explicit root arguments and no uninitialized locals.

That typed-null activation is a conformance-only machine input, not a product request or a substitute rooted-object
selection. It produces the standalone canonical target-outcome fragment defined in section 10 and therefore has no
canonical W4 request or plan identity. W4 must not invent snapshot, root-selection, or plan facts merely to exercise
the projector.

The separate model-conformance plan retains the exact root body and call-site signature but applies section 5.1's
model-covered target rule to the helper. It does not infer body properties from model registration.

### 3.2 Admitted instruction closure

W4 retains the W3 encodings needed for:

- `nop`;
- the admitted compact and operand-bearing `ldarg` forms;
- the admitted compact and operand-bearing `ldloc`/`stloc` forms;
- the admitted `ldc.i4` forms;
- unchecked `add`, `sub`, and `mul` over exact CLI `Int32`;
- instance `ldfld` of a frozen same-module exact `Int32` `FieldDef`;
- direct `call` in the shape defined by section 7; and
- typed value or `void` `ret` as fixed by metadata.

W4 removes W3's one-field/getter-length product-shape restriction only for the closed branchless profile. It does not
weaken instruction decoding, stack typing, field ownership, or complete-body admission.

W4.4b implements that distinction as explicit legacy and W4 graph-admission modes. The legacy path used by
`IlMachine` retains W3's call-free, one-field/getter restrictions. The graph planner's W4 mode admits both fixed
instance fields in the primary root and direct `call` with exact two-`Int32` pop/one-`Int32` push typing, then requires
the helper's exact branchless body. W4.5a adds an opt-in prepared-graph machine mode that executes exact values across
that admitted call while leaving legacy activation unchanged. Explained-unknown call/return transfer remains W4.5b.

### 3.3 Explicit exclusions

The profile does not admit:

- conditional or unconditional branches, `switch`, backedges, path forks, CFG merge, fixpoint iteration, or widening;
- `callvirt`, `newobj`, `MemberRef` calls, cross-module calls, interpreted recursion or mutual recursion, delegates,
  reflection, or generic substitution;
- interpreted handler search or transfer, filters, unwind, `leave`, `finally`, `fault`, or EH-bearing interpreted
  bodies; no body-shape claim is made for an opaque model-covered target;
- field or array stores, static fields, arrays, object construction, virtual heaps, or any allocation instruction;
- byrefs, pointers, unsafe/native instructions, P/Invoke, unmanaged transitions, or native execution;
- floating point, native integers, 64-bit arithmetic, conversions, comparisons, or checked arithmetic;
- filesystem, network, process, thread, clock, randomness, environment, or ambient-runtime interaction;
- PDB/frame reconstruction, virtual stepping, async/dynamic lifting, broad BCL models, or debugger UI; or
- a historical or causal statement about what ran in the captured process.

Unsupported opcode, signature, body, graph, or call shape rejects preparation and maps to the blocked structural row in
section 11.3. Missing exact program evidence maps to blocked with its actual evidence status; structurally invalid
program evidence maps to invalid. Unsupported effects use the separate effect row. None falls back to target execution,
Reflection invocation, a JIT, a worker, or best-effort guessing.

## 4) Request and preparation boundary

### 4.1 Request

A W4 request identifies:

1. the snapshot and exact rooted-object selection;
2. the root `MethodHandle`;
3. ordered receiver/argument evidence;
4. an immutable policy identity and version;
5. instruction, maximum-call-depth, and preparation-traversal limits; and
6. an immutable model catalog identity, even when the catalog is empty.

It does not accept caller-authored argument counts, local counts, return disposition, field offsets, decoded method
shapes, or a preselected call target. Those facts come from the admitted evidence graph.

This is the only W4 product-request shape. The dump-free typed-null conformance activation from section 3.1 enters an
already admitted W3 machine fixture and tests the target-outcome/result projector directly. It does not pass product
preparation, cannot receive a rooted request/plan fingerprint, and is excluded from claims about product-facade
reachability.

### 4.2 Preparation phases

Preparation is deterministic and side-effect free:

1. validate the request, snapshot, root, policy, and applied bounds;
2. resolve and admit the complete root method definition from counted metadata/body evidence;
3. decode and type the current interpreted method's instructions and field/call operands in stable instruction order;
4. for each direct call, resolve the target's exact MethodDef identity, flags, and signature evidence;
5. select exactly one disposition—interpret, use one named model, or block—for that structurally resolved call site;
6. terminate discovery at a model-covered or blocked leaf; for an interpreted disposition, resolve/admit the complete
   callee body and recursively repeat phases 3–6 in stable call-site order;
7. prove the resulting interpreted/model-boundary graph acyclic and within the maximum logical call depth;
8. freeze ordered method, field, call, model, assumption, and budget facts; and
9. compute a versioned canonical plan projection and SHA-256 fingerprint.

Disposition selection therefore precedes any attempt to read a prospective callee body. Preparation never traverses
through a model-covered opaque leaf or infers its internal body/call graph. Cycle detection during recursive discovery
may terminate early, but successful preparation also validates the final frozen graph before plan creation.

No instruction executes before all phases succeed. A supported prefix in the root or any reachable callee does not run
when a later instruction, dependency, call disposition, or graph rule fails. Preparation does not consume instruction
budget or call-depth capacity and does not call the persistent memory model.

W4.4 implements the interpreted-only subset of these phases. W4.4a's `ResolvedMethodCallTarget` deliberately contains
no body/RVA/local facts, so the future W4.6 disposition selector can classify an exact structural target before a
prospective body read. W4.4b currently chooses only the explicit interpreted graph mode and then acquires and admits
every reachable definition. It has no model catalog or blocked/model leaf representation yet. Preparation failures
return `MethodGraphPreparationResult` without a plan and leave the legacy machine entirely unactivated. W4.5a consumes
only a successful frozen interpreted graph through a separate activation mode; it performs no resolution or graph
mutation during execution and still introduces no model/catalog disposition.

### 4.3 Preparation traversal units

The traversal limit is a real consuming bound. In stable discovery order, preparation charges one unit for each newly
visited structural method, resolved field dependency, and direct-call edge. Equal structural identities are charged
once per plan. The root method is the first charged unit. Exhaustion returns `BudgetExhausted` with no executable plan,
no machine activation, and the exact ordered charges retained in result context.

Resolution retries, hash-table probes, metadata rows not selected by the structural request, and diagnostic formatting
are not traversal units. Changing the unit definition requires a versioned policy change and replay update.

W4.4b implements the unit shape under fixed internal safety caps, not the request-configurable budget/result context
required above. It charges the root and each newly discovered structural method once, each distinct structural field
once, and every retained call-site edge once, including multiple edges to one shared callee. Discovery is root-first
depth-first in increasing IL-offset order. Per-request first-result caches retain successes and failures for method
definitions and contextual field/call operands, so resolver retries cannot change the result. A successful graph
reports only its total `TraversalUnitCount`; W4.8 still owns the caller-supplied limit, ordered charge projection,
remaining-unit accounting, and product budget outcome. Until then, preparation enforces fixed caps of 64 distinct
methods and 1,024 traversal units and reports cap rejection without a partial graph.

## 5) Dump evidence and degraded inputs

### 5.1 Evidence that must remain exact

W4 never interprets an approximate body or guesses program structure. The following must be exact and mutually
compatible:

- snapshot and runtime-module identity;
- counted metadata root and every referenced `TypeDef`, `MethodDef`, signature, local signature, and `FieldDef`;
- the complete physical root body and every interpreted-callee body, including declared headers and the proof that no
  EH section exists; a model-covered target instead requires exact identity/signature evidence and an explicit
  model-covered body disposition in the plan;
- rooted receiver selection, receiver type, object identity, field owner, field token, field type, and field storage
  geometry; and
- every call target and model selection identity.

Partial or unavailable program structure blocks preparation. Conflicting structure blocks with `Conflict`; structurally
invalid evidence returns `Invalid`. Unknown continuation is for missing runtime values, not missing executable meaning.

### 5.2 Field-value matrix

After exact owner and storage correlation, each required imported `Int32` field observation maps as follows:

| Observation | W4 value | Evaluation behavior |
|---|---|---|
| Exact four-byte value | Exact `Int32` | Continue exactly. |
| Partial bytes/facts | Typed unknown `Int32` with a `Partial` origin | Continue only under the admitted unknown policy. |
| Unavailable bytes | Typed unknown `Int32` with an `Unavailable` origin | Continue only under the admitted unknown policy. |
| Conflict or ambiguity | No value | Block; do not join disagreeing observations into top. |
| Invalid address/layout/encoding | No value | Return `Invalid`; do not reinterpret invalidity as uncertainty. |
| Exact null receiver | No field value | Produce the admitted target `NullReference` outcome. |

The import retains the exact field handle, imported-object evidence identity, evidence status, stable reason code, source
identity, address, requested byte count, and observed byte count. A missing imported field is never the CLI default of
a newly allocated object. Partial bytes are retained as evidence but are never decoded into a speculative scalar.

### 5.3 Evidence aggregation

The product result aggregates only dependencies actually present in the frozen plan and operations actually reached.
Exact evidence stays `Exact`. Any reached partial origin makes the aggregate at least `Partial`; a reached unavailable
origin makes it `Unavailable` unless a stronger `Conflict` or `Invalid` stop applies. An unexecuted sibling dependency
cannot improve or degrade the answer, although this branchless profile ordinarily reaches both field loads.

## 6) Unknown values and lineage

### 6.1 Semantic value

The W4 value domain is a lifted-flat, structurally typed domain over the closed W3 value set:

- typed bottom represents no feasible runtime value and is never a valid activation input;
- one canonical typed top represents an unknown value of that exact static type;
- exact constants and exact references retain their W3 meaning; and
- distinct exact values join to canonical typed top.

Lattice order, join, meet, widening, machine semantic equality, and state hashing ignore explanatory lineage. Therefore
two unknown `Int32` values with different origins are semantically equal while their host explanations remain distinct.
This preserves commutative lattice laws and prevents operational history from contaminating semantic comparison.

An implementation may attach a lineage key to a value for transport, but the domain operations above must ignore that
key when deciding semantic equality or order. The complete lineage graph is an immutable execution-evidence channel,
not part of the lattice payload.

### 6.2 Lineage graph

Every introduced or transformed unknown refers to one content-addressed node in a bounded immutable DAG. Nodes use a
versioned canonical encoding and stable SHA-256 identity. The admitted node kinds are:

- `InputOrigin`: request argument, receiver, or imported field plus evidence status and stable provenance key;
- `BinaryTransform`: ordered left/right predecessors plus `add`, `sub`, or `mul` and exact result type;
- `FieldLoadTransform`: receiver/field identity plus the input-origin predecessor;
- `CallArgumentTransform`: call-site, parameter index, and predecessor;
- `InterpretedReturnTransform`: call-site, callee, and returned predecessor; and
- `ModeledReturnTransform`: call-site, model identity/version, and an ordered dependency vector containing every
  outcome-relevant unknown argument lineage; relevant exact arguments are embedded canonically.

Exact operands need not receive lineage nodes solely because they participate in an unknown result; their canonical
typed value is embedded in the transformation node. Children remain operand ordered even for commutative operations so
the graph matches the admitted IL stack behavior. Equal canonical nodes intern to one identity.

Raw target strings, local paths, exception messages, and process-random object identities never enter a lineage node.

The frozen profile derives a finite lineage ceiling rather than inventing another budget dimension. Bounded request
inputs and field dependencies contribute at most one origin each. Distinct field origins are bounded by charged field
dependencies in the preparation-traversal limit. Request-argument origins are bounded separately by the exact
metadata-derived root argument count, including the receiver slot where applicable, which must already fit W3's
maximum frame-slot count. The largest admitted call has two arguments, so one executed instruction or modeled call can
create at most three transforms: two argument edges and one return edge. Therefore the canonical node ceiling is
`traversal limit + admitted root argument count + (3 × instruction limit)`, with request validation rejecting
overflow in that derivation. Every node uses fixed-shape fields plus already bounded codes and identities. An overlong
origin component is invalid before activation. There is no independent lineage limit or runtime lineage-exhaustion
outcome.

### 6.3 Transfer rules

For every admitted transfer:

- exact inputs preserve W3's exact unchecked result;
- any typed unknown arithmetic input produces typed unknown `Int32` plus a `BinaryTransform` node;
- an approximate admitted field load consumes one instruction, pushes typed unknown, emits truthful precision-loss
  evidence, and preserves memory;
- a conflict, invalidity, capability failure, or malformed value performs no semantic transfer;
- bottom at activation or a resumed boundary is invalid; and
- no operation may inspect a lineage reason to select a concrete semantic result.

The concrete W3 domain remains valid and provenance-free. W4 must add a second meaningful domain or equivalent explicit
unknown-evidence implementation over the same opcode handlers before claiming shared multi-mode reuse.

W4.2 satisfies that kernel requirement at checkpoint `e89e43498`. `ProvenanceConcreteDomain` retains one lifted-flat
semantic top per structural type while an optional precision capability distinguishes exact, explained unknown, and
unexplained unknown values at execution boundaries. `UnknownExecutionPolicy.ExplainedInt32` permits only owned,
structurally valid explained `Int32` values; exact-only remains the compatibility default, and bottom, bare top,
foreign roots, wrong types, or capability failures perform no semantic transfer. The lineage implementation is
deliberately limited to canonical `InputOrigin` and ordered `BinaryTransform` nodes. It embeds exact operands, interns
equal canonical nodes, captures only the reachable immutable DAG in identity order, and replays identical bytes,
roots, nodes, and graph SHA-256 in fresh domain and machine objects. Exact E2 `ldfld` remains exact through the second
domain. At that checkpoint, partial/unavailable field continuation, precision-loss events, and `FieldLoadTransform`
remained W4.3 work; call and modeled-return lineage remain owned by their later slices.

W4.3 satisfies that next dump-free boundary at checkpoint `7479b1ad4`. Immutable backend-neutral
`FieldLoadEvidence` and its structured `MemoryLoadResult` branch preserve the field, observation classification,
reason, source, imported object/address, and bounded observed bytes without inventing a scalar. The optional
`IFieldLoadApproximationDomain` capability may convert only policy-enabled partial or unavailable evidence into a typed
unknown. Successful approximation emits `ValuePrecisionLost` and appends canonical `FieldLoadTransform` lineage;
exact, conflict, invalid, and typed-null behavior remains compatible, while capability failure and budget exhaustion
remain atomic. This checkpoint adds no ClrMD evidence producer, W4 product facade, dump-grounded counterfactual result,
direct call, or modeled return.

W4.5a checkpoint `356c07037` preserves exact values unchanged across its call and interpreted-return boundaries. It
does not create degenerate provenance nodes for exact arguments. An explained unknown at either boundary blocks
atomically with `EXEC_CALL_LINEAGE_UNAVAILABLE`, before state, memory, budget, or events change. W4.5b therefore still
owns the canonical `CallArgumentTransform` and `InterpretedReturnTransform` nodes and their same/fresh-object replay.

## 7) Direct calls and transitive admission

### 7.1 Structural call target

The admitted `call` operand must resolve to a non-nil same-module `MethodDef`. Every frozen target must have exact
metadata proving it non-generic, static, managed, non-vararg, and exactly `Int32 (Int32, Int32)` for `CombineMarkers`.
The call site must have exactly two `Int32` stack arguments and must push one exact-static-type `Int32` result. An
interpreted target additionally requires an exact branchless, EH-free admitted body. A model-covered target is an
opaque leaf: preparation neither reads nor infers its body shape, EH, or internal call graph, and instead freezes the
exact target identity/signature plus selected pure-model/effect disposition.

No `MemberRef`, `MethodSpec`, `TypeSpec` substitution, cross-module resolution, virtual dispatch, interface dispatch,
constrained call, overload search, or name-based target selection is admitted. Display names are diagnostics only.

### 7.2 Graph rules

The plan graph is rooted, finite, and acyclic over interpreted call edges. A structural MethodDef may be interpreted
once and referenced from multiple call sites, but any interpreted self-cycle or mutual cycle rejects the plan. A
model-covered target terminates discovery as an opaque leaf and cannot invoke target code under the pure-model
contract. The prepared maximum depth is computed from the frozen interpreted/model-boundary graph and must not exceed
the request's maximum call depth.

Every interpreted callee is decoded, typed, and admitted before root instruction zero. A model-selected callee still
requires exact structural signature evidence, but its body is not interpreted and is recorded as model-covered rather
than silently omitted. The primary scenario interprets `CombineMarkers`; W4 closure must also prepare and execute a
separate conformance request that selects the same structural target through exactly one admitted pure model.

### 7.3 W4.4 implementation checkpoint

W4.4a checkpoint `2e596c117` implements contextual `ResolveMethod` across the abstraction, SRM module, and resolution
service layers. `MethodCallSignatureShape` is content-equal over the exact declaring TypeDef, calling-convention,
implicit/explicit receiver flags, generic arity, ordered explicit parameter types, and return type.
`ResolvedMethodCallTarget` pairs that signature with one exact same-module MethodDef and can only represent an
ordinary managed-IL target. It carries no body, RVA, local signature, or locals. SRM validates the MethodDef token and
ordinary managed-IL implementation flags without calling `GetMethodBody`; non-nil `MemberRef` and `MethodSpec`
operands are well-formed but unsupported, while malformed/nil/out-of-range identities remain invalid. This is the
required disposition-before-body seam; no disposition or model is selected in W4.4.

W4.4b checkpoint `742ef2c4f` adds `MethodGraphPlanner`, `FrozenMethodGraphPlan`, method nodes, and retained direct-call
sites. Graph-mode admission resolves and types every direct edge, correlates each loaded definition with its frozen
identity/signature, admits each body completely, and validates the final public graph again before construction.
Equal reachable MethodDefs share one node and one admitted body even when multiple call sites retain separate charged
edges. Equal FieldDefs share one canonical descriptor; disagreement between equal structural identities is a conflict,
not last-writer-wins. Self/mutual cycles, unsupported dispatch/suffixes, resolver inability, definition/signature
conflict, fixed-cap exhaustion, and invalid graph invariants produce stable blocked/invalid results with no partial
plan. Canonical vectors are structurally ordered independently of discovery storage, while failure precedence follows
deterministic root-first call-site-ordered discovery.

The exact fixture prepares two method nodes, the two marker fields, the direct edge at caller IL offset 12, required
logical depth two, and five traversal units (two methods, two fields, one edge). The graph retains the internal admitted
plans needed by W4.5, but W4.4 neither runs them nor changes `IlMachine` activation. No call transfer, frame event,
model boundary, request-configurable traversal/depth enforcement, or product result is implemented by this checkpoint.

### 7.4 W4.5a exact execution checkpoint

W4.5a checkpoint `356c07037` binds exactly one `FrozenMethodGraphPlan` and one configured logical-depth limit to an
opt-in machine session. It creates root and callee frames from already admitted metadata, uses
`DirectCallSiteIdentity` and `FrameReturnSite` to retain the exact caller/call/callee/resume boundaries, and never
re-resolves a method, field, or call operand during execution. The legacy W3 activation path remains a mutually
exclusive call-free mode.

Each exact interpreted `call` advances the caller, pushes its callee, consumes one instruction, and emits
`InstructionExecuted` followed by `FramePushed`. Each helper `ret` consumes one instruction, validates and pops the
callee, transfers its exact result to the retained caller return site, and emits `InstructionExecuted` followed by
`FramePopped`. Root `ret` retains W3 completion behavior. Machine operational state carries configured/required
logical depth and observed logical/frame high-water facts; every step validates them against the frozen graph and
completed exact branchless execution additionally validates its depth witness.

Prepared-depth underflow rejects before activation. Instruction availability retains precedence at a nonterminal
boundary; graph/state disagreement, forged return sites, impossible high-water facts, and incompatible session modes
are atomic invalid outcomes. Ordinary domain or memory exceptions are normalized to stable blocked capability
failures without copying capability-controlled messages. Explained-unknown call/return lineage remains excluded by
the explicit W4.5a boundary in section 6.3.

## 8) Call execution, models, and effects

### 8.1 Interpreted call

An interpreted call:

1. validates available instruction budget and the frozen call-depth invariant;
2. pops the structurally exact `Int32` arguments, each of which may be an exact value or a provenance-bearing typed
   unknown, from the caller;
3. advances and freezes the caller return site;
4. pushes one callee frame with metadata-derived arguments, locals, and return type;
5. consumes one instruction unit for the completed `call` transfer; and
6. emits `InstructionExecuted` for the call plus one truthful frame-push event.

Returning from the helper consumes one instruction unit for `ret`, pops the helper frame, pushes its result onto the
caller stack, and emits the ordered return/frame-pop events. Root `ret` preserves the W3 completion behavior. Frame
push/pop events identify the actual method and IL boundary; they are not synthesized for a call that did not occur.

W4.5a implements this transfer for exact values over a frozen interpreted graph, including three-level and repeated
call fixtures, exact return-site replay, ordered frame events, unchanged persistent memory, and the two-frame
`GetMarkerSummary`/`CombineMarkers` differential result. The implementation performs no resolver call after graph
activation. Explained unknowns are intentionally rejected before transfer pending W4.5b's lineage capability.

### 8.2 Pure call model

W4 closure registers and exercises exactly one scenario-owned pure model for the structural `CombineMarkers` handle.
The primary product request still interprets the helper; model selection is a separate mandatory conformance request.
Selection is frozen during preparation and includes a stable model ID, semantic version, target signature, confidence
classification, and normalized effect declaration. Runtime lookup by display name is forbidden.

The model returns one typed outcome:

- `ExactReturn(Int32)`;
- `UnknownReturn(Int32, lineage)`;
- `Blocked(stable code)`; or
- `Invalid(stable code)`.

The model receives immutable typed arguments and policy facts only. It cannot receive or read persistent memory, read
ambient time/environment, access the dump session, mutate memory, allocate, invoke target code, or throw host exception
text across the boundary. Ordinary model exceptions are normalized to a payload-safe capability failure; catastrophic
process exceptions retain the W3 policy.

An unknown model return must be derived from admitted unknown arguments and receives a `ModeledReturnTransform` whose
ordered dependency vector contains every unknown argument that can influence the result. Both `CombineMarkers`
parameters are outcome-relevant, so if both are unknown neither origin may be discarded. A limitation that is not
grounded in an unknown input returns `Blocked(W4.Model.Limitation)` rather than creating an origin from nothing. A model
cannot replace missing structural evidence or convert conflict/invalid evidence into an answer.

Instruction availability is checked before invoking the model capability. Pre-instruction exhaustion performs no model
call, creates no model-attempt record, increments no call/model count, and leaves state, memory, budget, and events
unchanged.

Model invocation observes immutable arguments only. The engine retains the current immutable memory value outside the
capability call and preserves it unchanged. The caller frame is not mutated until a typed outcome has been validated:

- exact or unknown return atomically pops the arguments, advances the caller, pushes the typed result, consumes one
  instruction unit, and emits one ordinary instruction-executed event;
- blocked, invalid, or normalized capability failure preserves caller state, memory, instruction budget, and semantic
  events and creates no result value; and
- every invocation, including a non-transferring one, is retained in a separate deterministic model-attempt record
  containing call site, model identity, typed outcome category, and stable code. That operational record never claims
  that an IL transfer completed.

No model outcome mutates persistent memory. Exact and unknown outcomes count as one model invocation and one attempted
call. Blocked, invalid, and capability outcomes count as one attempted model invocation but zero executed instructions.
These counts and ordered model-attempt records participate in canonical product replay.

### 8.3 Fallback and effects

Call disposition never changes at runtime. If exact interpretation and the selected model are both unavailable, the
plan blocks; it does not try one after executing a prefix of the other. The v1 policy has no havoc, native fallback,
reflection fallback, or "assume pure" escape hatch.

The primary interpreted call and admitted pure model have `EvaluationEffectStatus.None`. Model use is exposed on the
model axis independently of effects. A call classified as requiring a write, allocation, I/O, time, thread, process,
native, reflection, or other ambient effect is blocked with `EvaluationEffectStatus.Unsupported`. W4 does not claim
`VirtualOnly` or `Modeled` effects because no admitted operation represents a write.

## 9) Deterministic budgets

### 9.1 Instruction budget

Instruction units retain the W3 definition: one unit per successfully completed ordinary instruction, or one unit for
an admitted instruction that produces a target exception. Admission, preparation, evidence inability, pre-instruction
budget exhaustion, and call-depth exhaustion consume no instruction unit and emit no instruction-executed event.

An approximate field load is a completed semantic transfer and therefore consumes one unit. A modeled call that
returns exact or unknown consumes one unit for the call instruction. A model that blocks or returns invalid before a
semantic outcome consumes no instruction unit.

### 9.2 Call-depth bound

Maximum logical call depth counts the root at depth one and every nested direct call boundary, whether that call is
interpreted or modeled. Both the primary interpreted request and the model-conformance request therefore require depth
two. Preparation computes the frozen graph's required logical depth; a smaller request limit returns
`BudgetExhausted` before plan creation or activation.

Execution records the observed logical-depth high-water mark. Interpreted calls additionally record active-frame-depth
high water; modeled calls enter the logical boundary without pushing a frame. The machine rechecks that an interpreted
frame push agrees with the frozen plan. A mismatch in an already admitted plan/state is `Invalid`, not a second
reachable budget outcome, and leaves caller state, stack, memory, instruction budget, and events unchanged.

W4.5a implements the interpreted exact subset. Prepared-graph activation rejects a configured limit below the frozen
required depth before creating machine state, initializes both high-water marks at the root depth of one, and updates
them only on a completed frame push. Runtime checks validate configured depth, required depth, active frame count, and
both high-water facts against the bound graph. Modeled logical boundaries remain W4.6 work.

### 9.3 Preparation-traversal budget

Traversal charging is defined in section 4.3 and occurs only during preparation. Execution cannot consume or replenish
it. Canonical context records the limit, units used, ordered charge identities, and whether exhaustion occurred.

### 9.4 Allocation bound

Allocation is unadmitted. `newobj`, `newarr`, allocating models, and virtual heap operations reject before execution.
Consequently no allocation operation consumes a budget and W4 carries no dormant allocation counter. Product context
records allocation as absent/not applied, not zero-used under a supposedly enforced limit. A later allocation scenario
must define its operation, unit, charge point, exhaustion semantics, evidence, and tests in a separate LOC-bounded
increment before an allocation budget becomes active.

### 9.5 Cancellation and precedence

Host cancellation is not a deterministic budget. It maps to `Cancelled` and is excluded from canonical equality unless
the host supplies a separately versioned deterministic cancellation fixture. At an instruction boundary, an already
latched terminal result wins. Request validation precedes traversal charging; traversal exhaustion terminates
preparation before plan creation or activation. For an admitted plan, instruction availability precedes invariant
checks and semantic transfer. The exact precedence is covered by pairwise tests.

## 10) Stop-on-throw

W4 retains W3's exact typed-null `ldfld` target-exception boundary and gives it a standalone product-outcome projection
in the dump-free conformance profile. This is the only target-exception source in the closed W4 contract. It is not
reachable through W4's exact non-null rooted request. The admitted unchecked `CombineMarkers` helper and its truthful
pure model cannot throw for any admitted `Int32` inputs; neither may fabricate a target exception merely to exercise
infrastructure. Adding a genuinely throwing product request or model requires a separate scenario, exception kind,
location contract, request/plan semantics, and LOC-bounded work slice.

The retained target exception carries its stable kind/code and exact throwing method/IL offset. It terminates the
complete evaluation. No handler search, filter, unwind, `finally`, `fault`, or continuation occurs, even if later
metadata work could describe such regions; EH-bearing interpreted bodies are rejected first.

The exceptional instruction consumes exactly one instruction unit and emits one target-exception event, not an
ordinary instruction-executed event. The terminal state has no resumable call stack or return value, but the product
outcome retains the frozen call trace. Re-evaluation of the terminal latch is idempotent and consumes no budget, calls
no capability, and emits no event.

The standalone projector maps an exact target exception to `Completion=Completed` and a complete `TargetException`
value projection, not to host failure. Its canonical fragment answers only "what outcome did this admitted machine
activation produce?" and carries no synthetic claim that a W4 product request reached it. An ordinary resolver,
domain, memory, or model capability exception is not a target exception and maps to `Blocked`; a typed invalid result
or violated structural invariant maps to `Invalid`.

## 11) Product plan and result

### 11.1 Separate product surface

W4 introduces a product-specific request, preparation result, immutable plan, bounded runner, and outcome projection.
It may reuse `EvaluationResult<T>` and existing identity/context types, but it must not depend on the W2 parser or label
user-IL execution as `DerivedQuery`.

Every returned envelope has `SemanticMode=CounterfactualExecution`. The product value is one of:

- `ExactReturn`, carrying the exact typed `Int32`;
- `UnknownReturn`, carrying exact static type plus a root lineage identity;
- `TargetException`, carrying structured target exception and call chain; or
- `ExecutionPrefix`, carrying a truthful bounded transcript when a budget stops after execution began.

Preparation failures with no executed prefix carry no product value. The envelope independently records completion,
completeness, evidence, effects, evidence context, provenance, and diagnostics.

`TargetException` is exercised in W4 only through the conformance-only projector input described in sections 3.1 and
10. The rooted product runner cannot produce it under this closed non-throwing scenario. The common result vocabulary
and projector are intentional; product-facade reachability is not implied.

### 11.2 Required visible facts

For a rooted product request, the immutable plan and result expose, in bounded canonical form:

- snapshot/root/module/method/field structural identities;
- policy ID/version and every applied bound;
- exact request assumptions, including every explicit unknown input;
- ordered interpreted and modeled call dispositions;
- model ID/version/confidence and normalized effect declaration;
- exact/degraded input evidence and read geometry;
- instruction units used/remaining; configured and prepared logical call depth; observed logical and active-frame
  depth high-water marks; and preparation-traversal units used/remaining;
- exact return, unknown lineage root/graph, target exception, or executed prefix;
- ordered semantic events and logical call chain;
- stable diagnostics and ordered provenance; and
- canonical request, plan, and result schema versions and SHA-256 fingerprints.

The standalone target-outcome fragment exposes only its schema version, terminal machine outcome, exact exception
identity/location, frozen call trace, instruction/event accounting, evidence/effect axes, diagnostics, and canonical
bytes/fingerprint. Snapshot/root/request/plan identities and preparation-traversal facts are absent/not applicable.

No display string, local path, raw target string, exception message, or process-random hash participates in identity.
Canonical replay is an audit artifact and is not automatically telemetry-safe.

### 11.3 Outcome matrix

| Condition | Completion | Completeness | Evidence | Effects | Product value |
|---|---|---|---|---|---|
| Invalid request, policy identity, or bound value | `Invalid` | `None` | `Invalid` | `None` | None |
| Both marker fields exact; interpreted/pure call succeeds | `Completed` | `Complete` | `Exact` | `None` | Exact `0x26AF37BD` |
| A reached marker is partial | `Completed` | `Partial` | `Partial` | `None` | Typed unknown plus lineage |
| A reached marker is unavailable | `Completed` | `Partial` | `Unavailable` | `None` | Typed unknown plus lineage |
| Partial/unavailable exact-program evidence | `Blocked` | `None` | Actual `Partial`/`Unavailable` | `None` | None |
| Unsupported opcode/signature/body/graph/call shape with exact evidence | `Blocked` | `None` | `Exact` | `None` | None |
| Reached evidence conflicts | `Blocked` | `None` | `Conflict` | `None` | None |
| Reached evidence or a bounded lineage input is structurally invalid | `Invalid` | `None` | `Invalid` | `None` | None |
| Conformance-only exact typed-null `ldfld` raises the retained target exception | `Completed` | `Complete` | Reached aggregate | `None` | Standalone structured target-exception fragment |
| Instruction/call-depth/traversal limit stops before execution | `BudgetExhausted` | `None` | Reached aggregate | `None` | None |
| Instruction limit stops after a prefix | `BudgetExhausted` | `Partial` | Reached aggregate | `None` | Execution prefix |
| Pure model returns a grounded unknown | `Completed` | `Partial` | Reached aggregate | `None` | Typed unknown plus model lineage |
| Pure model returns blocked or throws an ordinary capability failure | `Blocked` | `None` | Reached aggregate | `None` | None |
| Pure model returns invalid | `Invalid` | `None` | Reached aggregate | `None` | None |
| Resolver/domain/memory throws an ordinary capability failure | `Blocked` | `None` | Reached aggregate | `None` | None |
| Call requires an unsupported effect | `Blocked` | `None` | Reached aggregate | `Unsupported` | None |
| Host cancellation | `Cancelled` | `None` or truthful `Partial` | Reached aggregate | Reached effect status | None or prefix |

Unknown is a value precision classification, not a completion failure. Conversely, a complete product projection of an
exact target exception does not imply a normal method return.

## 12) Determinism and replay

Equal request, dump content, rediscovered structural evidence, policy, model catalog, and bounds must reproduce:

1. byte-identical canonical request and fingerprint;
2. identical preparation traversal charges and plan disposition;
3. byte-identical canonical plan and fingerprint;
4. identical admitted method/field/call graph and typed instruction boundaries;
5. identical memory calls, call/model decisions, assumptions, budgets, and events;
6. semantically equal states, values, and persistent memory;
7. identical lineage nodes and content identities;
8. identical completion/completeness/evidence/effect axes, return/prefix value, and diagnostics; and
9. byte-identical canonical result and fingerprint.

The corpus proves this repeatedly in one session, with fresh resolver/domain/machine/product objects, and after closing
and reopening the dump and rediscovering/rebinding the module, root, methods, fields, and imported memory. A separately
opened full-content-identified disk PE may compare metadata/body bytes after the dump-grounded result, but cannot repair
or seed missing dump evidence.

Operational usage, events, lineage, and provenance remain outside semantic-state equality while remaining mandatory in
canonical product replay.

Checkpoint `7479b1ad4` proves the dump-free W4.3 subset with same-object and fresh-object evidence, domain, and machine
construction: structured field evidence, `FieldLoadTransform`, precision events, state, and canonical lineage replay
remain identical. It does not prove product request/plan/result replay or the dump close/reopen/rebind obligation above;
those remain owned by W4.8 and W4.9.

Checkpoint `742ef2c4f` additionally proves content-equal frozen-graph reconstruction across fresh SRM modules and
resolution/planner objects. Canonical structural ordering reproduces the same nodes, fields, retained edges, admission
facts, required depth, and traversal-unit total. This is not yet the versioned canonical plan byte projection or
SHA-256 required by item 3 above; W4.8 owns that product identity, and W4.9 owns dump close/reopen/rebind replay.

Checkpoint `356c07037` proves exact prepared-graph machine replay across fresh machine instances without resolution
during execution. Structural call/return sites, state, instruction accounting, ordered frame events, completion,
unchanged memory, and logical/frame high-water facts reproduce for the admitted exact fixtures. This does not yet
prove explained-unknown call/return lineage replay, the versioned product request/plan/result identity, or dump
close/reopen/rebind replay.

The target-exception conformance case has a separate replay rule because it has no W4 product request or plan. Equal
versioned machine fixture, exact typed-null activation, instruction limit, and projector version must reproduce the
same terminal outcome, accounting, diagnostics, and byte-identical canonical target-outcome fragment/fingerprint.
Tests must assert the absence of snapshot/root/request/plan identities rather than manufacturing them.

## 13) Stable diagnostics

Diagnostics use bounded stable codes and payload-safe explanations. The initial code families are:

- `W4.Request.*` for request, policy, root, and applied-bound invalidity;
- `W4.Evidence.*` for exact-program requirements and runtime-value evidence outcomes;
- `W4.Admission.*` for opcode, signature, body, graph, field, and call-profile rejection;
- `W4.Unknown.*` for introduction, bounded-input validation, lineage, and canonicalization failures;
- `W4.Model.*` for model selection, outcome, capability, and effect classification;
- `W4.Budget.Instruction`, `.CallDepth`, and `.Traversal` for deterministic exhaustion;
- `W4.TargetException.*` for admitted target exceptions; and
- `W4.Replay.*` for canonical schema or reconstruction disagreement.

Tests assert codes and structured fields, never parse prose. Host exception text, target strings, query literals not
already authorized for canonical replay, raw file paths, and secrets are not copied into diagnostics.

## 14) Required executable evidence

W4 closure requires all of the following, headlessly and with `Scope!=Cybersecurity`:

1. the compiled `GetMarkerSummary`/`CombineMarkers` fixture, emitted-IL dependency closure, and exact CoreCLR oracle;
2. a second meaningful unknown-aware value domain over every admitted W3 value transfer, with semantic and lineage laws;
3. exact, partial, unavailable, conflict, invalid, and typed-null memory-boundary tests with no fabricated concrete value;
4. exact dump-sourced root/helper metadata and bodies plus both correlated field descriptors/observations;
5. frozen transitive admission negatives for unsupported suffixes, tokens, signatures, dispatch, cycles, EH, and graph
   depth, proving no prefix execution;
6. multi-frame argument, return-site, instruction, call-depth, memory, and event assertions;
7. interpreted and mandatory selected-pure-model agreement for exact and unknown inputs, plus blocked, invalid,
   capability, effect, and fallback cases with exact atomicity/accounting assertions;
8. retained typed-null stop-on-throw, exact location, budget/event truthfulness, standalone product-outcome mapping,
   fragment replay, absent request/plan identities, and terminal idempotence without fabricating a throwing
   helper/model outcome;
9. path-accurate instruction/call-depth/traversal exhaustion and allocation-absent assertions;
10. product tests that assert all result axes, assumptions, models, effects, bounds, lineage, provenance, diagnostics, and
    explicitly reject historical/causal replay wording;
11. byte-identical same-session and fresh-object canonical request/plan/result replay for exact, degraded, blocked,
    budget, and modeled requests; separate byte-identical fragment replay for the target-exception conformance case;
    and dump-close/reopen replay for every dump-grounded case; and
12. the repository-wide locked restore, zero-warning Release build, Markdown/headless guards, non-cybersecurity fast,
    ordinary-dump, optimized-dump, and focused W4 lanes with zero skips at the exact pushed closure commit.

Through W4.5a, item 2, the dump-free field-boundary core of item 3, the dump-free frozen interpreted-admission core of
item 5, the exact-value subset of item 6, the interpreted-depth subset of item 9, and corresponding same/fresh-object
subsets of item 11 are implemented. W4.4 proves direct MethodDef token/signature negatives, unsupported
dispatch/suffixes, self/mutual cycles, shared-callee deduplication, fixed graph caps, complete no-prefix admission, and
required-depth calculation. W4.5a adds exact multi-frame execution, return-site and event ordering, pre-activation
logical-depth enforcement, runtime invariant checking, depth high-water reporting, and resolver-free replay.

Checkpoints `2e596c117` and `742ef2c4f` together passed locked restore; the strict fifteen-project Release build with
zero warnings/errors; planner 35/35; W4 fixture 6/6; complete unit 250/250; fast 73/73; ordinary dump 5/5; optimized
dump 1/1; and both guards, with zero skips. At checkpoint `356c07037`, headless validation passed locked restore; the
strict fifteen-project Release solution build and strict Release builds of the unit and integration projects, all with
zero warnings/errors; prepared-graph tests 25/25; W4
fixture 7/7; complete unit 275/275; fast integration 74/74; ordinary dump 5/5; optimized dump 1/1; the Markdown guard
across 62 files and 41 local destinations; and the headless guard across one workflow. Every test lane had zero skips
and every behavioral command used `Scope!=Cybersecurity`. An independent audit closed every identified W4.5a blocker.
The ordinary and optimized dump filters were `Category=Dump&Corpus!=ModeledIncidentContextV1&Scope!=Cybersecurity`
and `Category=Dump&Corpus=ModeledIncidentContextV1&Scope!=Cybersecurity`, respectively.

Explained-unknown call/return lineage in item 6, dump-sourced evidence in item 4, models, product projection, dump
reopen/rebind replay, and hosted exact-commit umbrella closure remain pending; no ClrMD W4 producer, product result,
dump-grounded W4 result, or W4 umbrella closure is claimed.

Concrete and differential coverage is required for every admitted opcode and call family. Degraded-evidence coverage is
required for every transfer that can carry an unknown. Tests assert final outcome plus resolver/memory/model call counts,
state preservation, budget deltas, events, lineage, and canonical replay—not only the displayed value.

## 15) LOC-bounded implementation work slices

Planning counts hand-written production code, tests, fixtures, harnesses, scripts, and material CI logic. Documentation,
generated output, locks, solution churn, vendored snapshots, and deletion-only scaffold removal are excluded. Additions
and materially rewritten lines are counted once. These ranges describe implementation surface only.

| Slice | Independently valuable responsibility | Estimated implementation LOC | Realized LOC |
|---|---|---:|---:|
| W4.1 | Generated value-gate fixture, exact emitted-shape assertions, exact CoreCLR oracle, and one current-W3 rejection checkpoint | 350–480 | 478 |
| W4.2 | Unknown E1/E2 domain kernel, content-addressed lineage, domain/transfer laws, and replay | 3,350–3,500 | 3,454 |
| W4.3 | Backend-neutral structured field evidence/continuation, evidence matrix, precision events, and dump-free domain/machine tests | 2,400–3,500 | 3,096 |
| W4.4a | Body-free direct MethodDef/signature resolution, managed-IL certification, and SRM token/implementation matrix | Post-audit sub-slice of original combined W4.4 estimate, 1,700–2,600 | 1,043 |
| W4.4b | Explicit graph admission, deterministic resolution/discovery, canonical acyclic closure, fixed safety caps, and required depth | Post-audit sub-slice of original combined W4.4 estimate, 1,700–2,600 | 2,608 |
| W4.5a | Exact multi-frame interpreted calls, return sites, frame events, maximum-logical-depth enforcement, invariant replay, and depth high-water reporting | Post-audit sub-slice of original combined W4.5 estimate, 2,300–3,500 | 3,334 |
| W4.5b | Explained-unknown call/return boundary capability, `CallArgumentTransform`/`InterpretedReturnTransform`, and canonical replay | 1,800–2,700 | — |
| W4.6 | Structural pure model, typed outcomes, model/effect/fallback policy, and conformance tests | 2,300–3,400 | — |
| W4.7 | Retained-null target-outcome contract, terminal projection, canonical fragment without request/plan identity, and dump-free differential/idempotence tests | 1,500–2,500 | — |
| W4.8 | Canonical product request/plan/result/runner, configurable traversal charging, target-fragment result-projector integration, and product tests | 2,400–3,500 | — |
| W4.9 | Generated-dump exact/degraded/model corpus, CoreCLR integration oracle, reopen/rebind replay, CI closure, and realized LOC ledger | 2,000–3,200 | — |
| **Initial umbrella** | **Nine original work slices; W4.4 and W4.5 were later split into independently tracked sub-slices** | **16,860–25,310** | **14,013 through W4.5a** |

The original **16,860–25,310 LOC** baseline remains recorded above rather than being rewritten after implementation
evidence. The historical projection after W4.2 was **18,532–26,132 LOC**; replacing W4.3's estimate with its realized
value then produced **19,228–25,728 LOC**. W4.4 realizes 3,651 added LOC: W4.4a contributes 1,043 (665 production
plus 378 tests), and W4.4b contributes 2,608 (1,411 production plus 1,197 tests), for 2,076 production plus 1,575
test LOC. Its original combined 1,700–2,600 estimate proved low, so the realized audit records two independently
delivered sub-slices, each below the 3,500-LOC ceiling, without inventing retroactive per-slice estimates.

W4.1–W4.4 cumulatively realize **10,679 LOC**; the then-remaining W4.5–W4.9 envelope of **10,500–16,100 LOC** produced
the historical **21,179–26,779 LOC** projection. W4.5a now realizes **3,334 LOC**: 1,590 production plus 1,744 tests.
That brings W4.1–W4.5a to **14,013 LOC**. The original combined W4.5 estimate of 2,300–3,500 remains recorded rather
than being rewritten; the post-implementation audit separates its exact execution core from the independently
valuable W4.5b lineage package, now estimated at 1,800–2,700 LOC. Combined W4.5 is therefore currently projected at
**5,134–6,034 LOC**.

The remaining W4.5b–W4.9 envelope is **10,000–15,300 LOC**, giving a current total projection of
**24,013–29,313 LOC**. The W4.2 checkpoint remains 3,454 realized LOC: 3,429 attributable implementation LOC (1,521
production plus 1,908 focused tests) and 25 LOC that segregate an excluded test scope from the milestone lane. W4.3
remains 3,096 realized LOC: 1,100 production plus 1,996 tests. The current table contains eleven implementation rows
because the originally admitted W4.4 and W4.5 rows are now W4.4a/W4.4b and W4.5a/W4.5b pairs. The original nine-slice
umbrella baseline and historical 18,532–26,132, 19,228–25,728, and 21,179–26,779 projections remain calibration facts.

Ownership boundaries are explicit. W4.1 freezes the fixture and current W3 rejection only. W4.2 owns the second
domain, execution-boundary precision policy, and `InputOrigin`/`BinaryTransform` lineage only; it keeps exact E2 field
loads compatible but does not continue from non-exact field evidence. W4.3 implements the backend-neutral, dump-free
structured field-evidence capability, continuation, precision event, and `FieldLoadTransform`; it does not implement a
ClrMD evidence producer or dump-grounded result. W4.4a owns body-independent structural target resolution and
managed-IL/signature certification; W4.4b owns interpreted-call admission, canonical complete graph construction,
first-result caching, per-edge retention/charging, required-depth calculation, and fixed internal safety caps. W4.5a
owns exact interpreted call/return transfer, frames, machine-level logical-depth enforcement, invariant replay, and
depth high-water reporting. W4.5b owns explained-unknown call/return lineage; W4.8 owns configurable traversal charging
and product projection. W4.9 owns generated-dump and reopen/rebind integration. W4.7 owns the standalone
target-outcome/canonical fragment; W4.8
integrates it into the common result projector without inventing rooted-facade reachability, and W4.9 only aggregates
the already-tested behavior into closure evidence. Realized LOC is attributed once.

Before starting or while implementing a work slice, its estimate may be refined from detailed design, current-code
audit, or landed implementation evidence. Its upper bound must remain at or below 3,500 LOC or the slice must split.
At closure, record realized attributable LOC and use it to refine later slices. W3's realized 8,842 hand-written
additions is the reason these ranges are deliberately conservative.

## 16) Cybersecurity scope

External-input cybersecurity behavior is explicitly outside W4, as requested. W4 does not run, repair, expand, or use
the hostile-artifact corpus or external-worker containment lane as behavioral evidence. Every W4 test command includes
`Scope!=Cybersecurity`. Repository-wide restore/build may still compile those projects solely as topology and
compilation-health evidence.

This accepted exclusion does not widen functional semantics. Filesystem, network, process, thread, time, P/Invoke,
native/unsafe, reflection, and other ambient interactions remain unsupported effect classes and block the W4 plan.
That is an evaluator-language rule, not a claim that W4 establishes a security boundary for arbitrary artifacts.

## 17) Completion and expansion rule

W4 is complete only when all eleven current table rows are implemented (the original W4.4 and W4.5 rows are now
W4.4a/W4.4b and W4.5a/W4.5b pairs), their realized LOC is recorded, the complete required matrix passes locally
through the repository headless wrapper, the exact pushed closure commit passes every required hosted job, and the
traceability map links each requirement to that evidence. A contract, fixture, unit-only domain, compiler-only call
test, or successful local run
is not umbrella closure. The preserved initial baseline still describes the nine-slice plan admitted before those
post-audit splits.

After W4, each of the following still requires a separate scenario-derived contract and LOC-bounded package:

- branches, path enumeration, CFG/fixpoint analysis, or an `AbstractAnalysis` result;
- additional opcode, type, call, dispatch, model, effect, or cross-module families;
- allocation, writes, virtual heaps, havoc, collection traversal, or environment intrinsics;
- handler search/transfer, filters, unwind, `finally`, or `fault`;
- generics, PDB/frame reconstruction, async/dynamic lifting, or virtual stepping; and
- any external-artifact product surface or cybersecurity initiative.

No expansion may weaken W3/W4 structural identity, exact-program-evidence, complete transitive admission,
no-fabrication, deterministic budget/event, immutable-memory, explicit-counterfactual-language, or replay rules.
