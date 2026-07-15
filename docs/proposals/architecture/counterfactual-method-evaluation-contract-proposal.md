# Counterfactual Method Evaluation Contract

> **Lifecycle:** Current
>
> **Roadmap relation:** Active · W4 normative contract
>
> **Implementation status:** W4.1 fixture gate implemented at `82363585b`; W4.2 unknown E1/E2 kernel implemented at
> `e89e43498`; W4.3 structured field-evidence continuation implemented at `7479b1ad4`; W4.4–W4.9 pending

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
`FieldLoadTransform` lineage while retaining exact and terminal memory outcomes. W4.4 and every later behavior remain
requirements, not implementation claims, until the traceability map names passing executable evidence. API names
shown here are provisional design names; public prototype APIs that land must carry detailed XML documentation.

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

### 4.3 Preparation traversal units

The traversal limit is a real consuming bound. In stable discovery order, preparation charges one unit for each newly
visited structural method, resolved field dependency, and direct-call edge. Equal structural identities are charged
once per plan. The root method is the first charged unit. Exhaustion returns `BudgetExhausted` with no executable plan,
no machine activation, and the exact ordered charges retained in result context.

Resolution retries, hash-table probes, metadata rows not selected by the structural request, and diagnostic formatting
are not traversal units. Changing the unit definition requires a versioned policy change and replay update.

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

Through W4.3, item 2, the dump-free field-boundary core of item 3, and the corresponding same/fresh-object subset of
item 11 are implemented. Checkpoint `7479b1ad4` also passes the local locked-restore, strict-build, test-lane, and guard
portion of item 12. Dump-sourced evidence in item 4, calls, models, product projection, dump reopen/rebind replay, and
hosted exact-commit umbrella closure remain pending; no ClrMD producer, product result, dump-grounded W4 result, or W4
umbrella closure is claimed.

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
| W4.4 | Direct MethodDef resolution, call signatures, acyclic graph construction/required-depth calculation, and frozen transitive admission | 1,700–2,600 | — |
| W4.5 | Multi-frame interpreted calls, return sites, frame events, maximum-logical-depth enforcement, and depth high-water reporting | 2,300–3,500 | — |
| W4.6 | Structural pure model, typed outcomes, model/effect/fallback policy, and conformance tests | 2,300–3,400 | — |
| W4.7 | Retained-null target-outcome contract, terminal projection, canonical fragment without request/plan identity, and dump-free differential/idempotence tests | 1,500–2,500 | — |
| W4.8 | Canonical product request/plan/result/runner, configurable traversal charging, target-fragment result-projector integration, and product tests | 2,400–3,500 | — |
| W4.9 | Generated-dump exact/degraded/model corpus, CoreCLR integration oracle, reopen/rebind replay, CI closure, and realized LOC ledger | 2,000–3,200 | — |
| **Initial umbrella** | **Nine non-overlapping work slices** | **16,860–25,310** | **7,028 through W4.3** |

Replacing W4.1, W4.2, and W4.3 estimates with their realized values gives a current W4 total projection of
**19,228–25,728 LOC**: 7,028 realized plus 12,200–18,700 estimated for W4.4–W4.9. The original
**16,860–25,310 LOC** baseline remains recorded above rather than being rewritten after implementation evidence. The
W4.2 checkpoint contributes 3,454 realized LOC: 3,429 attributable implementation LOC (1,521 production plus 1,908
focused tests) and 25 LOC that segregate an excluded test scope from the milestone lane. W4.3 contributes 3,096
realized implementation LOC: 1,100 production LOC plus 1,996 test LOC. The remaining W4.4–W4.9 slice envelopes are unchanged
pending their own design/code audits and implementation evidence.

Ownership boundaries are explicit. W4.1 freezes the fixture and current W3 rejection only. W4.2 owns the second
domain, execution-boundary precision policy, and `InputOrigin`/`BinaryTransform` lineage only; it keeps exact E2 field
loads compatible but does not continue from non-exact field evidence. W4.3 implements the backend-neutral, dump-free
structured field-evidence capability, continuation, precision event, and `FieldLoadTransform`; it does not implement a
ClrMD evidence producer or dump-grounded result. W4.4 is next and owns new call admission and graph construction under
a fixed internal safety cap, W4.5 owns logical-depth policy/enforcement, and W4.8 owns configurable traversal charging
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

W4 is complete only when all nine work slices are implemented, their realized LOC is recorded, the complete required
matrix passes locally through the repository headless wrapper, the exact pushed closure commit passes every required
hosted job, and the traceability map links each requirement to that evidence. A contract, fixture, unit-only domain,
compiler-only call test, or successful local run is not umbrella closure.

After W4, each of the following still requires a separate scenario-derived contract and LOC-bounded package:

- branches, path enumeration, CFG/fixpoint analysis, or an `AbstractAnalysis` result;
- additional opcode, type, call, dispatch, model, effect, or cross-module families;
- allocation, writes, virtual heaps, havoc, collection traversal, or environment intrinsics;
- handler search/transfer, filters, unwind, `finally`, or `fault`;
- generics, PDB/frame reconstruction, async/dynamic lifting, or virtual stepping; and
- any external-artifact product surface or cybersecurity initiative.

No expansion may weaken W3/W4 structural identity, exact-program-evidence, complete transitive admission,
no-fabrication, deterministic budget/event, immutable-memory, explicit-counterfactual-language, or replay rules.
