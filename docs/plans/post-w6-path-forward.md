# Post-W6 Path Forward: W7 Static-Field Root Context

> **Lifecycle:** Current · **Roadmap:** Active
>
> **Decision:** implement one opt-in root-context acquisition profile for an ordinary non-generic static reference
> field, then feed the exact acquired object into the unchanged W2/W5/W6 expression pipeline. Do not add expression
> syntax, member depth, collection navigation, method execution, or stack-frame reconstruction.
>
> **Front-end invariant:** the complete pinned Roslyn expression parser remains the sole syntax front end. W7 changes
> neither parsing nor tree admission; later expression growth, if evidence selects it, occurs only in versioned
> project-owned binding and evaluation over Roslyn trees.
>
> **Evidence boundary:** W7 is a prototype-design milestone grounded in generated and predeclared synthetic evidence.
> It does not establish field readiness. W6's hosted disposition does not carry forward.

## 1) Executive decision

W6 closed the complete-expression-parse and fixed-depth member-chain slice. Its twenty-four-incident usefulness
portfolio found six decision-changing root/context-attribution boundaries across all four application shapes. That
boundary was the unique qualified leader and selected exactly one permitted action:
`AdmitOneConcreteContextAcquisitionScenario`.

W7 chooses the following concrete scenario:

```text
root name:        root
context source:   ordinary static reference field
module:           Interpreter.OptimizedContextTestTarget.dll
declaring type:   Interpreter.OptimizedContextTestTarget.StaticContextProbe
field:            Root
expression:       root.Marker
expected value:   0x4A44C004
```

The existing optimized-context fixture already predeclares this axis. Its heap object and `Marker` bytes are exact,
but the current product cannot attribute that object through the static field, so the report retains
`raw-context-static=unavailable-static-field-observation` and a `QUERY_ROOT_UNAVAILABLE` product result. W7 closes
that one gap. It does not infer a static context from heap uniqueness and does not substitute a strong handle.

The scenario is narrow but architectural: a product-owned selector must resolve a runtime module, counted metadata
TypeDef/FieldDef, owning application domain, initialized static slot, exact pointer, and compatible target object into
one canonical root binding. Once that binding exists, Roslyn admission, W2 direct-field evaluation, W5 method routing,
and W6 member-chain preparation/evaluation remain unchanged.

The [C# Expression Front-End and Subset-Admission Contract](../proposals/architecture/csharp-expression-front-end-contract-proposal.md)
is a permanent dependency of this plan. Roslyn parses every bounded expression completely and once; project-owned
profiles admit, bind, and evaluate only selected trees. W7 does not introduce a second parser or a text-level special
case for static fields. Its selector is out-of-band context acquisition, and valid C# outside the enabled W2/W5/W6
profiles continues to stop as unsupported before metadata or dump-memory access.

W7's umbrella implementation scale is `~10K LOC`, split into `~1K LOC` checkpoints and a `~100 LOC` closure record.
These are logarithmic planning scales and may be revised when implementation evidence changes the work.

## 2) Why this scenario

### 2.1 Evidence-selected boundary

The six W6 incidents grouped under `RootContextAttribution` are deliberately heterogeneous:

| W6 incident | Shape | First stop | W7 interpretation |
|---|---|---|---|
| `w6-05-request-partial-reference` | Request | Partial nested pointer | Preserve as partial; W7 does not reconstruct pointer suffixes |
| `w6-11-batch-unavailable-progress` | Batch | Unavailable nested pointer | Preserve as unavailable; W7 does not search for a replacement target |
| `w6-18-coordinator-root-absence` | Coordinator | Declared root selector found no object | Demonstrates the need for one alternate attributable root source |
| `w6-19-workflow-unavailable-attempt` | Workflow | Unavailable nested pointer | Preserve as unavailable; W7 does not bypass the frozen outer field |
| `w6-21-request-target-conflict` | Request | Target identity conflict | Preserve as conflict; W7 does not reconcile contradictory facts |
| `w6-24-batch-invalid-progress-target` | Batch | Invalid target structure | Preserve as invalid; W7 does not repair malformed evidence |

The ranking selected a category, not permission to solve every member of that category. W7 therefore admits one new
source of an exact root object and leaves the other five stopping conditions truthful.

### 2.2 Existing independent motivation

The optimized-context corpus predates W6 and keeps five context axes in every denominator: `this`, argument, local,
static, and strong root. Raw member bytes are exact for all five, but only the strong-handle root is currently
attributable and queryable. The static axis is the adjacent candidate because:

1. the target already contains a named ordinary static reference field and pauses while it is live;
2. the pinned ClrMD API exposes the initialized state and field-slot address for a selected application domain;
3. the project-owned memory reader can read the pointer from that slot with counted evidence;
4. counted module metadata can validate the exact TypeDef/FieldDef rather than trusting a display-only lookup; and
5. the resulting object can enter the current root binding without changing expression semantics.

### 2.3 Rejected alternatives

W7 explicitly rejects these tempting expansions:

- **Stack `this`, arguments, and locals.** The current .NET 10 DAC boundary does not expose an admitted stack-slot
  observation. W7 does not infer slots from heap uniqueness, register guesses, or member values.
- **Arbitrary object-address input.** A caller-supplied address is not context attribution. It lacks a dump-derived
  source relation unless a separate scenario defines and validates that relation.
- **Heap scan by type.** Type uniqueness in one fixture is not a root relationship and remains only a late oracle.
- **Nested-reference recovery.** Partial or unavailable pointer bytes cannot be completed from nearby objects or a
  second search without a separately evidenced relation.
- **Target repair.** A header/type conflict remains conflict, and an invalid extent/header remains invalid.
- **Static member syntax.** W7 does not admit `Type.Field`, qualified names, or another Roslyn tree shape. The static
  field is an out-of-band root selector; the expression still begins with the configured `root` identifier.
- **Thread statics, context statics, generic statics, value-type statics, RVA fields, and literals.** Each has a
  different storage or context contract and requires its own evidence-led decision.

## 3) W7 admitted contract

### 3.1 Orthogonal profiles

Expression admission and root acquisition are separate axes:

| Axis | Existing/default | W7 opt-in |
|---|---|---|
| Expression language | `FrozenW5` or `FixedDepthMemberChainV1` | Unchanged |
| Root acquisition | Host-supplied/strong-handle binding | `StaticFieldRootV1` |
| Query semantics | Existing W2/W5/W6 result contracts | Unchanged after exact root acquisition |
| Root failure | Existing typed binding/classification outcomes | New typed acquisition outcome before classification |

The expression axis always begins with the same complete Roslyn parse. “Unchanged” here means that the W7 selector
cannot recognize syntax, reinterpret a valid tree, or bypass the enabled binder profile. Future syntax support must
extend the versioned recognizer/binder/evaluator chain and its three-bucket corpus, never parser code.

`StaticFieldRootV1` must never become an implicit fallback. A request names one acquisition profile before any dump
capability is consulted. Failure of the static selector does not run the strong-handle search, heap enumeration, or a
different selector.

### 3.2 Selector

The version-one static selector contains only:

- the product root identifier, bounded by the existing root-name limit;
- the exact runtime module file name, compared ordinal-ignore-case only for module candidate discovery;
- the exact full declaring runtime type name, compared ordinal;
- the exact static field name, compared ordinal;
- the root-acquisition profile identifier `StaticFieldRootV1`; and
- explicit deterministic catalog/name bounds applied by acquisition.

The selector does not contain a runtime address, method table, metadata token, application-domain address, MVID, or
expected query value. Those are acquired evidence, not caller claims. Stable metadata identity and snapshot-scoped
runtime identity enter the issued result only after validation.

### 3.3 Admissible field shape

W7 admits exactly one storage shape:

- one directly declared FieldDef on a non-generic TypeDef;
- `static`, non-literal, non-thread-static storage;
- a managed reference whose declared type is one non-generic concrete class TypeDef in the same module;
- one runtime module candidate and its owning application domain;
- a nonzero static slot address reported for that application domain;
- one pointer-width raw read from the slot; and
- an exact-null or exact non-null pointer, or a typed non-exact outcome.

For the successful W7 scenario, the pointer is non-null and the exact runtime target type equals the declared field
type. Interface, base-class, array, string, boxed value, cross-module, and runtime-subtype assignment are unsupported
in v1 even when the CLR would permit the assignment. This avoids importing a general assignability engine into root
acquisition.

### 3.4 Exact null versus unavailable storage

W7 distinguishes three facts that high-level convenience reads can collapse:

1. `GetAddress` returns zero: the product has no validated initialized field slot and reports unavailable context.
2. The slot address is nonzero but the raw pointer read is non-exact: the result retains partial/unavailable/conflict/
   invalid evidence and never claims null.
3. The slot read is exact and all pointer bytes are zero: the field is exactly null and acquisition returns a distinct
   exact-null outcome with no root object.

An exact-null root does not issue an expression request or apply an expression-level coalescing literal. W7 does not
invent a root-null expression language.

### 3.5 Successful result

Success returns one exact `DumpQueryRootBinding` carrying:

- the configured root name and dump snapshot identity;
- the exact selected target object and runtime module identity;
- the ordinary-static-field root-source discriminator;
- the static slot address, owning application-domain identity, and pointer-width read;
- the counted metadata identity, declaring TypeDef token, FieldDef token, declared target TypeDef token, and exact
  selector text;
- the target method-table read and validated runtime TypeDef/module relationship;
- only bounds actually reached; and
- a versioned canonical static-context identity and digest.

The binding may then be passed to the existing classifier/evaluator. No later phase may query the selector again.

## 4) Physical evidence chain

### 4.1 Module selection

Acquisition begins with the already bounded runtime-module catalog. Candidate discovery uses the selector's file name.
The W7 profile requires exactly one candidate. Zero candidates are exhaustive selector absence; multiple candidates
are conflict. The product does not choose by enumeration order, target path, image base, or metadata similarity.

For the sole candidate, counted dump metadata must produce the existing `ModuleContentIdentity` containing MVID,
metadata length, and metadata SHA-256. A target path is a display hint only. No analysis-machine PE is opened as input
to selection.

### 4.2 Metadata declaration

The adapter resolves the selector to exactly one TypeDef and one directly declared FieldDef from counted metadata.
It validates:

- non-nil and in-range tokens;
- exact ordinal type and field names;
- `static` and non-literal attributes;
- absence of generic declaring-type parameters;
- the exact reference-type signature and same-module concrete target TypeDef; and
- agreement between counted metadata tokens and the ClrMD runtime type/field projections used only to locate the
  runtime slot.

Missing declaration is exhaustive selector absence. Duplicate or token/name disagreement is conflict. Malformed
metadata is invalid. Incomplete metadata reads are partial or unavailable according to the existing evidence rules.

### 4.3 Application domain and slot

The selected `ClrmdModuleInfo` already carries a snapshot-scoped application-domain address. The adapter must map that
address back to exactly one ClrMD application domain and reject zero or multiple matches. It then obtains the ordinary
static field's storage address for that domain.

The ClrMD address operation is permitted only to locate the slot. It is not value evidence. The project-owned memory
reader performs the sole authoritative pointer read and retains its source identity, address, requested width, status,
observed bytes, and issue mapping.

### 4.4 Target validation

For an exact non-null pointer, acquisition validates before creating a root object:

1. the pointer is representable for the dump architecture and belongs to the same snapshot;
2. the heap projects one valid object at that exact address;
3. a raw pointer-width read at the object address yields the exact method table;
4. the raw method table agrees with ClrMD's target type;
5. runtime module and TypeDef identity equal the declared static-field target type; and
6. object extent is sufficient for the later selected member operation.

The last check may be completed during existing W2/W6 member preparation, but any target fact acquired during root
selection must be retained and correlated rather than silently reacquired with a different result.

### 4.5 Oracle boundary

`ClrStaticField.ReadObject`, heap enumeration, and an independently opened disk PE may be used only as late equality
oracles in tests. None may supply the product pointer, declaration, root identity, or result. Poison tests must prove
the product path succeeds without those oracle calls and fails truthfully when the counted slot read is non-exact.

## 5) Ownership and API direction

### 5.1 Responsibility map

| Stage | Owner | Input | Output |
|---|---|---|---|
| Declare selector | Product contract | Root name plus module/type/field text | Canonical bounded `StaticFieldRootV1` selector |
| Resolve declaration | ClrMD/SRM adapter | Selector plus immutable dump session | Counted module/type/field declaration result |
| Locate slot | ClrMD adapter | Exact runtime module/type/field/domain mapping | Slot address or typed miss |
| Read pointer | Host memory capability | Slot plus pointer width | Immutable raw memory evidence |
| Validate target | ClrMD/raw-memory adapter | Exact non-null pointer plus declaration | Exact rooted object or typed miss |
| Bind root | Dump-query product | Complete acquisition result | Exact binding or typed root-acquisition failure |
| Classify/prepare/evaluate | Existing product | Expression plus exact binding | Unchanged W2/W5/W6 outcome |

### 5.2 Adapter contracts

The host layer should add one immutable selector/result family rather than expose `ClrStaticField`:

- `ClrmdStaticFieldRootSelector` retains only bounded caller text and profile identity;
- `ClrmdStaticFieldDeclaration` retains counted module content, runtime module, app-domain, TypeDef, FieldDef, declared
  target type, field shape, and applied bounds;
- `ClrmdStaticFieldRootObservation` retains status, issue, declaration when complete, slot address, exact-null state,
  selected target when exact, raw reads, and canonical replay projection; and
- `ClrmdDumpSession.AcquireStaticFieldRoot` performs the fixed operation order once.

Names are draft direction, not compatibility promises. Every public type and method must carry detailed XML
documentation of intent, parameters/returns, failure behavior, and prototype caveats.

### 5.3 Product contracts

The product layer should add:

- a versioned root-acquisition request/identity orthogonal to `DumpExpressionLanguageProfile`;
- a product-owned facade that converts the adapter observation to either an exact `DumpQueryRootBinding` or a typed
  acquisition failure;
- an append-only `RootAcquisitionFailure` case in the expression outcome only for the new selector-taking facade; and
- a selector-taking evaluator overload that acquires once, then invokes the unchanged classifier/evaluator overload.

The existing overloads remain the compatibility path. A caller that already supplies `DumpQueryRootBinding` sees no
new default behavior.

### 5.4 Root-source model

`ClrmdHeapObjectInfo.RootAddress` and `RootKind` currently describe a CLR handle slot. W7 must not encode a static slot
under misleading handle terminology. The implementation must either:

1. introduce a typed, source-agnostic root provenance value with strong-handle and static-field cases while preserving
   existing public compatibility properties; or
2. introduce a separate exact object-context record and adapt both root sources into the product binding.

The choice is made at W7.2 after the physical gate. In either design, strong-handle canonical bytes and diagnostics
remain byte-for-byte frozen, and the static case retains its complete selector/declaration/slot identity.

### 5.5 Canonical identity

Legacy W2/W5/W6 requests, plans, results, root-selection provenance, and schema-v1/v2/v3 reports must remain identical.
The static route receives new versioned identities rather than changing old encodings in place:

- selector identity: caller text, profile, and configured bounds;
- declaration identity: snapshot/runtime module, counted module content, TypeDef/FieldDef/target tokens, and field
  signature shape;
- observation identity: declaration plus domain/slot, pointer evidence, target identity, status, issue, and reached
  bounds;
- root-binding identity: configured root name plus observation identity; and
- issued expression-request identity: existing expression/profile/policy plus the static root-binding identity.

Runtime addresses remain snapshot-scoped. Same-dump close/reopen must reproduce bytes exactly; a newly generated dump
is a distinct snapshot and is not expected to share its runtime identity.

## 6) Outcome matrix

The adapter and product retain these cases without exception-text diagnostics or fallback selection:

| Boundary | Evidence | Acquisition outcome | Expression issued? |
|---|---|---|---:|
| Selector exceeds name/catalog bound | Invalid | Invalid request | No |
| Runtime module catalog exceeded during session open | Partial/limit | Partial | No |
| No module candidate | Exact exhaustive | Exhaustive selector absence | No |
| Multiple module candidates | Conflict | Conflict | No |
| Module metadata unavailable | Unavailable | Unavailable | No |
| Module metadata partial | Partial | Partial | No |
| Module identity disagreement | Conflict | Conflict | No |
| Malformed metadata | Invalid | Invalid | No |
| Declaring TypeDef absent | Exact exhaustive | Exhaustive selector absence | No |
| FieldDef absent | Exact exhaustive | Exhaustive selector absence | No |
| Field is instance/literal/thread-static/generic/unsupported type | Exact declaration | Unsupported | No |
| Runtime TypeDef/FieldDef mapping disagrees | Conflict | Conflict | No |
| Owning application domain unavailable | Unavailable | Unavailable | No |
| Application-domain mapping ambiguous | Conflict | Conflict | No |
| Static slot address is zero | Unavailable | Uninitialized/unavailable | No |
| Slot pointer read partial | Partial | Partial | No |
| Slot pointer read unavailable | Unavailable | Unavailable | No |
| Slot pointer bytes malformed for pointer width | Invalid | Invalid | No |
| Slot pointer exactly zero | Exact | Exact null | No |
| Target object unavailable | Unavailable | Unavailable | No |
| Target method-table read partial | Partial | Partial | No |
| Target method table/type/module disagrees | Conflict | Conflict | No |
| Target structure/extent invalid | Invalid | Invalid | No |
| Complete compatible non-null target | Exact | Exact object binding | Yes |

Unsupported declaration shape is not invalid data. Exact null is not exhaustive selector absence. Partial or
unavailable evidence is not null. Conflict and invalid outcomes never expose a candidate object.

## 7) Deterministic bounds

W7 reuses or adds only bounds tied to operations it actually performs:

| Bound | Rule |
|---|---|
| Root/module/type/field text | Reuse existing bounded-name policy; reject before dump access |
| Runtime modules | Reuse the session's bounded module catalog; do not enumerate again in the product |
| Counted metadata | Reuse the managed metadata admission cap and record the exact read |
| Type/field declaration scan | Fixed bounded metadata traversal with exact reached counters |
| Application domains | Fixed bounded lookup over the session/runtime catalog |
| Static slot reads | Exactly one pointer-width read on the successful declaration path |
| Target-header reads | Exactly one pointer-width method-table read before exact-object issuance |
| Expression parser | Existing single complete Roslyn parse only after exact root acquisition |
| Member preparation/evaluation | Existing W2/W5/W6 bounds and no-rebinding rules |

Limits that are not reached do not appear in result context. Exhaustion fails before the next capability call. No W7
counter is a dormant promise for stack frames, generic statics, collection traversal, or expression depth.

## 8) Synthetic evidence design

### 8.1 Physical truth gate

W7.1 uses the existing optimized Release target unchanged. While
`Interpreter.OptimizedContextTestTarget.StaticContextProbe.Root` is live, the gate must prove:

- exactly one target runtime/module/type/ordinary static field/application-domain mapping;
- a nonzero initialized slot address;
- one exact raw pointer read from that slot;
- one exact raw method-table read from the pointed object;
- agreement with the counted TypeDef/FieldDef/signature projection;
- late agreement with `ClrStaticField.ReadObject` and the already discovered heap object; and
- the exact `Marker` value `0x4A44C004` through the existing W2 decoder after product integration.

If the pinned DAC cannot produce a stable slot address for this exact fixture, W7 stops at the truth gate. The project
must record the unavailable boundary and reconsider the selected scenario rather than adding a heuristic.

### 8.2 Meaningful multi-shape target

After the truth gate passes, a dedicated W7 target freezes four unrelated application graphs. Each process exposes
one ordinary static reference field as its only admitted product root source:

1. request pipeline with current request and optional failure;
2. batch pipeline with progress and optional failure;
3. coordinator pipeline with owner/job/shard/worker relationships; and
4. workflow/dispatch pipeline with current attempt, worker, attempts, and optional error.

The target is separate from `Interpreter.TestTarget` and `Interpreter.OptimizedContextTestTarget`, so W7 does not
silently refresh W2–W6 artifact identities. Every target launch and dump/consumer process is hidden and headless.

### 8.3 Predeclared sixteen-incident portfolio

Before implementation changes target bytes, a source-controlled manifest freezes sixteen independent dump incidents:

| # | Shape | Static-root condition | Expression / expected first result |
|---:|---|---|---|
| 1 | Request | Exact non-null | `root.CurrentRequest.Status` → exact string |
| 2 | Batch | Exact non-null | `root.Progress.CompletedPartitions` → exact `Int32` |
| 3 | Coordinator | Exact non-null | `root.Owner?.Name` → exact string |
| 4 | Workflow | Exact non-null | `root.CurrentAttempt.Status` → exact string |
| 5 | Request | Exact null slot value | Typed exact-null root acquisition; no parse |
| 6 | Workflow | Slot address unavailable/uninitialized | Unavailable; no pointer read or parse |
| 7 | Batch | Partial pointer bytes | Partial; no target lookup or parse |
| 8 | Coordinator | Pointer bytes unavailable | Unavailable; no target lookup or parse |
| 9 | Request | Module candidate conflict | Conflict before metadata/slot access |
| 10 | Batch | Counted metadata unavailable | Unavailable before TypeDef/FieldDef binding |
| 11 | Coordinator | Field selector absent | Exhaustive selector absence |
| 12 | Workflow | Thread-static comparison field | Unsupported declaration shape |
| 13 | Request | Runtime target type disagreement | Conflict before root binding |
| 14 | Batch | Target header partial | Partial before root binding |
| 15 | Coordinator | Target extent/header invalid | Invalid before root binding |
| 16 | Workflow | Exact static root, unsupported third-hop comparison | Root acquired exactly; syntax remains unsupported |

Every incident has its own full snapshot and predeclared target arguments, selector, expected acquisition status,
expected expression outcome when issued, usefulness classification, decision impact, first boundary, and application
shape. Evidence views may mask a captured read as non-exact or introduce an explicit conflict/invalid projection, but
they may not invent an exact pointer or value absent from the dump.

### 8.4 Required differential cases

The generated conformance lane additionally proves:

- the exact object selected through a static slot and, in an oracle-only comparison, a strong handle yields equal W2/
  W6 semantic values but distinct root-source/request/plan provenance;
- poisoning strong-handle and heap-search operations does not affect the static product path;
- poisoning `ClrStaticField.ReadObject` does not affect the counted raw-slot path;
- same-session repetition performs acquisition once per issued request and never during plan evaluation;
- two fresh headless consumers and dump close/reopen reproduce canonical selector, observation, binding, request, plan,
  result, and report bytes;
- exact-null and unavailable-slot cases remain different;
- W2/W5/W6 default entry points and canonical corpora remain unchanged; and
- valid C# outside the existing language profiles remains unsupported after an exact static root is acquired.

### 8.5 Corpus separation

W7 reports retain controlled/generated, designed/synthetic, and representative/external-observation kinds as separate
non-promotable categories. The sixteen incidents are designed evidence and contribute zero representative questions,
incidents, or application shapes. No percentage or production recoverability claim is emitted.

## 9) Delivery sequence

### W7.0 — contract and roadmap freeze

**Scale:** `~1K LOC` documentation.

Publish this plan, link it from active navigation, add traceability requirements, and freeze the exact static-field
scenario, selector, exclusions, evidence chain, portfolio outline, and closure rule before implementation.

**Exit gate**

- W6 remains closed and its evidence is not rewritten.
- Current status surfaces identify W7 as the sole active delivery sequence.
- The static-field choice is tied to both the W6 decision and the preexisting optimized-context axis.
- No document describes stack contexts, arbitrary addresses, new syntax, or pointer recovery as W7 work.
- Markdown and headless-workflow guards pass; the authored-scope vocabulary scan is clean.

### W7.1 — static-slot physical truth gate and fixture freeze

**Scale:** `~1K LOC` implementation and tests.

Prove the complete physical chain against the existing optimized target without changing product behavior. Freeze the
dedicated W7 target inputs and sixteen-incident draft before target-affecting implementation.

**Exit gate**

- The pinned ClrMD package returns one stable nonzero slot address for the exact static field/domain.
- Project memory reads the exact pointer and target method table once each.
- Counted metadata and runtime projection agree on module, TypeDef, FieldDef, signature, and target type.
- `ReadObject` and heap discovery are late oracles only.
- Uninitialized, exact-null, partial, unavailable, conflict, invalid, and unsupported distinctions are proven through
  dump-free seams before adapter implementation.
- If the exact slot cannot be proven, W7 records deferral and does not proceed to W7.2.

### W7.2 — immutable selector, declaration, observation, and root-source contracts

**Scale:** `~1K LOC` implementation and tests.

Add defensively immutable, content-equal host contracts and decide the typed root-source representation. Freeze
canonical encodings before composition.

**Exit gate**

- Selector and each result status validate complete/no-partial payload invariants.
- Strong-handle root behavior and canonical bytes remain unchanged.
- Static declaration/observation identities include all selector, metadata, domain, slot, read, target, status, issue,
  and reached-bound facts.
- Default arrays, malformed tokens, foreign snapshots, duplicate bounds, wrong pointer widths, and contradictory
  payloads are rejected.
- Public types/methods have detailed draft XML documentation.

### W7.3 — counted static-root adapter

**Scale:** `~1K LOC` implementation and tests.

Implement the fixed acquisition sequence in `ClrmdDumpSession` with one result and no fallback selection.

**Exit gate**

- Module/type/field/domain selection is deterministic and bounded.
- Counted metadata is authoritative for declaration; ClrMD runtime objects only correlate runtime storage.
- Exact success performs one slot read and one target-header read.
- Every outcome in section 6 is covered, with no candidate object on non-exact results.
- Poison/count tests prove no heap scan, strong-handle scan, high-level value read, disk PE, or repeated catalog lookup
  participates in success.
- Same-session and close/reopen acquisition replays canonically.

### W7.4 — product root acquisition and compatibility

**Scale:** `~1K LOC` implementation and tests.

Add the opt-in product facade, exact binding conversion, typed failure outcome, and orthogonal acquisition identity.

**Exit gate**

- `StaticFieldRootV1` is explicit and never a default/fallback.
- Only an exact non-null observation creates `DumpQueryRootBinding`.
- Exact null, exhaustive absence, partial, unavailable, conflict, invalid, and unsupported remain distinct failures.
- Static root provenance reaches request/plan/result context without altering semantic value equality.
- Existing binding-taking classifier/evaluator overloads and W2/W5/W6 canonical bytes remain unchanged.
- No expression parse occurs before exact root acquisition.

### W7.5 — expression composition and generated headless conformance

**Scale:** `~1K LOC` implementation and tests.

Route an exact static root into unchanged W2/W5/W6 classification, preparation, and evaluation. Add a versioned
headless report path without rewriting schema-v1/v2/v3 consumers.

**Exit gate**

- The optimized static axis changes from unavailable to exact only on the new opt-in path and returns `0x4A44C004`.
- Existing strong-root and all other optimized-context axes retain their historical classifications unless executable
  evidence independently changes them.
- Direct-field and fixed-depth member-chain exact/non-exact results preserve existing semantics after acquisition.
- Same process, fresh processes, and close/reopen reproduce canonical artifacts.
- All target, dump writer, consumer, and test processes are hidden/headless.
- The existing complex valid-but-unsupported Roslyn corpus remains passing and proves W7 adds no syntax shortcut.

### W7.6 — meaningful synthetic portfolio and next decision

**Scale:** `~1K LOC` implementation and tests.

Materialize the sixteen predeclared incidents across four unrelated shapes and run two fresh portfolio consumers.

**Exit gate**

- All sixteen snapshots are distinct and every row reproduces its predeclared acquisition and expression outcome.
- Exact answers span all four shapes and both W2 direct-field and W6 member-chain routes.
- Non-exact rows retain their first stopping boundary without fallback or candidate leakage.
- Two fresh reports are byte-identical; representative counts remain zero; corpus promotion is rejected.
- A unique threshold-qualified post-W7 action is selected or the successor is explicitly deferred.
- Enum order cannot choose between substantively tied candidates.

### W7.7 — repository closure

**Scale:** `~100 LOC` documentation.

Reconcile authoritative documents, run the complete headless matrix, record coarse realized scales, and close W7 only
at the exact pushed commit satisfying the required gates.

**Exit gate**

- Locked restore and strict Release build pass for the complete solution with zero warnings/errors.
- Complete unit, Fast, ordinary dump, optimized dump, focused W7, generated conformance, and meaningful synthetic
  lanes pass with zero skips.
- W2–W6 canonical encodings, default classifications, schemas, outcomes, and gates remain unchanged except for any
  explicit W7 target/report version introduced by this plan.
- Markdown, headless-workflow, and authored-scope vocabulary guards pass.
- Required hosted jobs execute and pass at the exact pushed closure commit, absent a new explicit W7-only owner
  disposition.
- Any selected post-W7 action is documented but not implemented as W7 work.

## 10) Verification matrix

All managed commands remain headless. Exact filters may be refined as tests land, but W7 requires:

| Layer | Required proof |
|---|---|
| Contract unit | Selector/result invariants, status payloads, canonical identities, legacy byte preservation |
| Adapter unit | Module/type/field/domain mapping, slot address, raw pointer, exact null, target validation, typed misses |
| Adapter integration | Existing optimized static field, counted reads, metadata correlation, oracle separation |
| Product Fast | Opt-in facade, no fallback, no pre-acquisition parse, strict outcome union, old overload preservation |
| Ordinary generated dump | Exact/non-exact static roots, W2/W6 evaluation, fresh process, close/reopen/rebind |
| Optimized context | Static axis exact on W7 path; other five-axis classifications remain explicit |
| Meaningful synthetic | Sixteen independent dumps, four shapes, raw counts, unique decision or deferral, zero representative rows |
| Repository policy | Markdown links, headless workflow, authored-scope vocabulary, clean strict build |

Expected command shape:

```powershell
.\eng\Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode
.\eng\Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --verbosity minimal --maxcpucount:1 --disable-build-servers /p:UseSharedCompilation=false
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast" --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=StaticFieldRootV1" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W7MeaningfulSyntheticV1" --verbosity normal
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-markdown-links.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-headless-workflows.ps1
```

A missing platform prerequisite is a failed/open gate, not a skipped success.

## 11) Post-W7 decision gate

W7.6 ranks first blockers by independent incident count, decision-changing count, useful-answer count, and exact
attributable-evidence count. Enum order may sort rows but may not select a successor. A branch is selected only when it
is the unique leader after all substantive comparisons and has at least three independent incidents across at least
two application shapes, including at least two decision-changing questions. Otherwise W7 records explicit deferral.

| Dominant recurring boundary after W7 | Permitted next action |
|---|---|
| Stack-frame context | Plan one concrete frame/slot observation only if the pinned runtime boundary supplies evidence |
| Nested reference source | Plan one attributable alternate reference source; do not reconstruct missing bytes |
| Target identity | Improve one concrete target-correlation source; do not repair conflicts or invalid objects |
| Third member hop | Admit one depth-three scenario with separately bounded planning |
| Collection navigation | Admit one concrete indexed collection layout |
| Repeated zero-argument method | Admit only that scenario's complete execution dependency closure |
| Result explanation | Improve the headless explanation without adding evaluation semantics |
| No recurring useful answer | Stop feature expansion and reconsider the product thesis |

The synthetic decision advances prototype design only. No W8 implementation is pre-approved by this plan, and later
representative observations may confirm, reverse, or stop the selected direction.

## 12) Completion definition

W7 closes only when all of the following are proven at current state:

- the pinned ClrMD/runtime combination physically exposes the exact optimized static slot without a product value read;
- one versioned static-field selector binds exactly one ordinary non-generic reference field from counted metadata;
- one raw slot read distinguishes exact null from unavailable/non-exact evidence;
- exact non-null acquisition validates target header/module/TypeDef before issuing a root binding;
- no non-exact result exposes a candidate or invokes an alternate selector;
- the exact optimized static context answers `root.Marker` through the existing W2 engine;
- W6 member-chain queries accept the same exact static root without parser or semantic expansion;
- Roslyn remains the sole complete expression parser, while supported behavior remains a versioned binder/evaluator
  subset with stable valid-but-unsupported outcomes;
- all section 6 outcomes and the sixteen-incident portfolio have executable evidence;
- W2/W5/W6 default APIs, canonical bytes, reports, and behavioral gates remain unchanged;
- fresh headless processes and dump close/reopen reproduce all W7 canonical artifacts;
- representative counts remain zero and no readiness percentage is claimed;
- complete local and repository-policy gates pass with zero skips;
- required hosted jobs execute and pass at the exact pushed closure commit, absent an explicit W7-only owner
  disposition; and
- any post-W7 selection remains documentation, not silently implemented successor work.

Until these conditions hold, W7 remains active. Stack frames, generic/thread statics, arbitrary address contexts,
deeper navigation, collection indexing, broader calls, and other product surfaces remain research.

## 13) Delivery discipline

Each W7 checkpoint is committed and pushed before the next begins. Commit messages record:

- the incident question and contract boundary;
- evidence ownership and canonical-identity effects;
- exact, null, degraded, conflicting, invalid, and unsupported cases added;
- exclusions preserved;
- coarse realized LOC scale (`~100`, `~1K`, or `~10K`); and
- headless validation commands and results.

The active plan, traceability map, product proposal, architecture overview, testing strategy, integration plan, and
repository navigation are reconciled whenever executable truth changes. Documentation does not claim an implemented
checkpoint merely because this plan names it.
