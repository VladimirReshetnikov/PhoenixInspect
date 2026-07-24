# Post-W6 Path Forward: W7 Static-Field Expressions and Debugger Binding Context

> **Lifecycle:** Complete · **Roadmap:** Reference
>
> **Decision:** implement one opt-in expression-binding profile that resolves an ordinary static field from either a
> non-ambiguous fully qualified non-generic class name or a simple type name justified by exact selected-frame and
> Portable PDB import context, reads its value from dump memory, and, for a reference-valued field, may feed the exact
> object into the existing member evaluator. Fully qualified binding is the context-independent minimum guarantee,
> not merely an out-of-band root selector.
>
> **Front-end invariant:** the complete pinned Roslyn expression parser remains the sole syntax front end. W7 changes
> versioned tree admission and binding, not parsing. Roslyn decides whether the complete text is valid C#; project-
> owned binders correlate selected syntax with stack, PDB, metadata, and dump evidence and may return a typed
> incomplete or ambiguous result when contextual evidence is insufficient.
>
> **Evidence boundary:** W7 is a development milestone grounded in generated and predeclared synthetic evidence.
> It does not establish field readiness. The owner recorded a separate W7-only disposition on 2026-07-17 after the
> complete local matrix closed; it does not claim hosted execution and does not weaken the workflow.
>
> **Successor:** the separately approved, active
> [Post-W7 Path Forward](post-w7-path-forward.md) defines W8 `StaticFieldExpressionV2`. This completed W7 record's
> statement that it did not pre-approve W8 remains historically true; W8 receives its authority from the successor
> plan rather than retroactively from W7.

## 1) Executive decision

W6 closed the complete-expression-parse and fixed-depth member-chain slice. Its twenty-four-incident usefulness
portfolio found six decision-changing root/context-attribution boundaries across all four application shapes. That
boundary was the unique qualified leader and selected exactly one permitted action:
`AdmitOneConcreteContextAcquisitionScenario`.

W7 chooses the following minimum concrete expressions:

```text
reference value:  global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root
member value:     global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root.Marker
expected member:  0x4A44C004
scalar fixture:   global::PhoenixInspect.W7TestTarget.StaticValues.Counter
expected scalar:  fixture-declared Int32
contextual pair:  StaticValues.Counter
required context: selected frame + exact `using PhoenixInspect.W7TestTarget;` import scope
```

The existing optimized-context fixture already predeclares this axis. Its heap object and `Marker` bytes are exact.
W7.1's ClrMD 4.0 dependency correction advances the raw modeled-context report to schema v2 and exact static-field
attribution, while the product result deliberately remains `QUERY_ROOT_UNAVAILABLE`. W7 closes that product gap
through the expression itself. It does not infer a static context from heap uniqueness and does not substitute a
strong handle.

The scenario is narrow but architectural. A project-owned name binder must project the Roslyn member-access tree into
a candidate fully qualified type name, directly declared static field name, and optional instance-member suffix. It
then resolves exactly one runtime module/TypeDef/FieldDef/application-domain chain from counted metadata, obtains the
initialized static slot, reads the value through project-owned dump memory, and issues either a terminal value or an
exact object binding for the unchanged member evaluator.

The [C# Expression Front-End and Subset-Admission Contract](../proposals/architecture/csharp-expression-front-end-contract-proposal.md)
is a permanent dependency of this plan. Roslyn parses every bounded expression completely and once; project-owned
profiles admit, bind, and evaluate only selected trees. W7 introduces no second parser or text-level special case. It
does add `StaticFieldExpressionV1`, a real binding profile over Roslyn trees. Valid C# outside the enabled profiles
continues to stop before metadata or dump-memory access.

This milestone also implements the first debugger binding-context slice. Fully qualified static-field binding must
work without a stack or PDB. A selected-frame path correlates a runtime frame with its MethodDef and instruction
location, validates one Portable PDB, reconstructs the active nested import scope, and uses the current namespace,
namespace imports, and simple aliases to bind `Type.Field`. Missing or inexact context may reduce precision or produce
a typed unavailable/ambiguous result; it must not make the evaluator silently choose a different symbol. Arguments,
locals, `this`, generic substitution, and the rest of C# binding remain later expansions over the same contract.

W7's realized umbrella implementation scale is `~10K LOC`. The coarse realized checkpoint scales are W7.0 `~1K`,
W7.1 `~1K`, W7.2 `~10K`, W7.3 `~1K`, W7.4 `~10K`, W7.5 `~10K`, W7.6 `~1K`, and W7.7 `~100`. These are
logarithmic scales: they describe the order of magnitude, not an exact line count. The larger W7.2/W7.4/W7.5 bands
reflect the immutable evidence families, real frame/PDB acquisition, runtime declaration/value mapping, and suffix
composition that became apparent during implementation.

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
static-field expression/source family and leaves the other five stopping conditions truthful.

### 2.2 Product requirement and existing independent motivation

The optimized-context corpus predates W6 and keeps five context axes in every denominator: `this`, argument, local,
static, and strong root. Raw member bytes are exact for all five, but only the strong-handle root is currently
attributable and queryable. The static axis is the adjacent candidate because:

1. the target already contains a named ordinary static reference field and pauses while it is live;
2. the pinned ClrMD API exposes the initialized state and field-slot address for a selected application domain;
3. the project-owned memory reader can read the pointer from that slot with counted evidence;
4. counted module metadata can validate the exact TypeDef/FieldDef rather than trusting a display-only lookup;
5. Roslyn already parses the fully qualified member-access forms, so the missing work is binding and evidence; and
6. a resulting object can enter the current root binding without changing existing instance-member semantics.

This supersedes the earlier W7 statement that rejected static-member syntax as “another Roslyn tree shape.” That
statement conflated an already parsed member-access tree with the absent semantic binder. The corrected boundary keeps
the complete Roslyn parser, admits a versioned static-access projection, and makes context, name resolution, storage,
and value evidence explicit product responsibilities.

A debugger expression evaluator is expected to bind names in context rather than require every root to be assembled
by its caller. Stack frames, PDB scopes, imports, aliases, and metadata collectively supply that context in mature
debuggers. W7 does not attempt that entire surface, but it must create a binder/context boundary that can grow into it.
The fully qualified static-field case is the context-independent floor: if one loaded class and one directly declared
ordinary static field match the complete name, their value must be readable even when stack or PDB context is absent.

### 2.3 Deferred or rejected alternatives

W7 keeps these boundaries explicit:

- **Stack `this`, arguments, and locals.** They are required future binding-context sources, not permanently rejected
  syntax. W7 defines their typed evidence seams but does not claim values until the pinned runtime supplies an admitted
  stack-slot observation. It does not infer slots from heap uniqueness, register guesses, or member values.
- **PDB imports and simple type names.** These are W7 work, not a rejected tree shape. The first implementation admits
  `Type.Field` when one selected runtime frame, one validated Portable PDB method/scope, current-namespace and active
  namespace-import/type-alias facts, and counted runtime metadata produce exactly one symbol. Fully qualified binding
  remains available when any of that context is absent. Broader lexical/name lookup is deferred rather than guessed.
- **Arbitrary object-address input.** A caller-supplied address is not context attribution. It lacks a dump-derived
  source relation unless a separate scenario defines and validates that relation.
- **Heap scan by type.** Type uniqueness in one fixture is not a root relationship and remains only a late oracle.
- **Nested-reference recovery.** Partial or unavailable pointer bytes cannot be completed from nearby objects or a
  second search without a separately evidenced relation.
- **Target repair.** A header/type conflict remains conflict, and an invalid extent/header remains invalid.
- **Broader static storage.** Thread statics, context statics, constructed generic statics, RVA fields, and literals
  have different storage contracts. W7 returns typed unsupported outcomes for them rather than treating them as an
  ordinary application-domain static.
- **General C# binding.** Nested/generic type syntax, inherited static-member lookup, bare members from `using static`,
  extern aliases, extension methods, static properties, method groups, overload resolution, conversions, and target-
  code execution remain later profiles. Their import facts may be represented in context before their corresponding
  expression forms are admitted. They are not parser limitations.

## 3) W7 admitted contract

### 3.1 Pipeline and profiles

W7 retains separate language and evidence identities, but the expression now selects its root source:

| Axis | Existing/default | W7 opt-in |
|---|---|---|
| Expression language | `FrozenW5` or `FixedDepthMemberChainV1` | `StaticFieldExpressionV1` |
| Name source | Configured `root` identifier | Fully qualified type plus static field parsed from the expression |
| Binding context | Already supplied exact root | Loaded-module metadata; optional selected-frame/PDB/import context required only for simple names |
| Storage acquisition | Host-supplied/strong-handle binding | `StaticFieldStorageV1` selected by the bound FieldDef/domain |
| Query semantics | Existing W2/W5/W6 result contracts | Direct static value or unchanged suffix evaluation after exact reference |

The W7 operation order is fixed:

```text
bounded expression text
    -> one complete pinned Roslyn parse and integrity check
    -> project-owned static-access tree projection
    -> bounded context/name binding against counted metadata
    -> static declaration/storage acquisition
    -> direct value projection or exact-reference suffix plan
    -> bounded evaluation
```

Invalid or valid-but-unadmitted trees stop before binding. Name absence, ambiguity, incomplete context, unsupported
symbol/storage shape, and non-exact memory remain distinct after syntax admission. No stage reparses the expression.

`StaticFieldExpressionV1` must never become an implicit reinterpretation of a failed `root` expression. The caller
selects the language profile before parsing. Failure of static binding does not run a strong-handle search, heap
enumeration, another name binder, or a caller-supplied root fallback.

### 3.2 Admitted syntax and fully qualified binding

The version-one profile admits these expression families:

```csharp
global::Namespace.Type.StaticField
Namespace.Type.StaticField
Type.StaticField
TypeAlias.StaticField
global::Namespace.Type.StaticReference.InstanceMember
Namespace.Type.StaticReference.InstanceMember
Type.StaticReference.InstanceMember
```

The suffix may use an already admitted W2 direct-member or W6 fixed-depth member-chain form, including W6's
conditional terminal access and compatible coalescing form. The profile initially admits only identifier-name
segments and an optional literal `global::` alias qualifier. Roslyn owns escaped/Unicode identifier decoding; the
descriptor copies `ValueText` and exact raw text.

The binder considers only bounded splits that could yield one complete top-level non-generic TypeDef, one directly
declared FieldDef, and either no suffix or one already admitted root-relative W2/W6 suffix. It never chooses the
longest textual prefix by convention. Exactly one complete binding is success; zero is exhaustive absence and
multiple are ambiguity. For the minimum guarantee:

- the type name is fully qualified and compared by decoded namespace plus type name;
- the TypeDef is a non-generic top-level class;
- the field is directly declared, ordinary static, and non-literal;
- accessibility does not block debugger inspection, but field identity and storage shape remain exact;
- all loaded managed modules are searched under fixed bounds using counted metadata;
- duplicate full type/field definitions across modules are ambiguous unless exact selected-frame/reference context
  narrows the module universe; and
- the expression does not contain an assembly name, runtime address, metadata token, domain address, expected value,
  or other caller-asserted physical identity.

The dot-qualified fully qualified form is accepted without a selected frame when it produces one binding across the
bounded module universe. A simple `Type.Field` or `TypeAlias.Field` form is accepted only when exact selected-frame
context supplies current-namespace/import candidates and those candidates produce one binding. `global::` suppresses
current-namespace/import interpretation but still cannot disambiguate duplicate type definitions in multiple loaded
modules.

### 3.3 Debugger binding context slice and trajectory

W7 introduces an immutable, snapshot-scoped `DumpExpressionBindingContext` design with independently typed evidence:

- current thread/frame and instruction location;
- current module, MethodDef, declaring type, namespace, and generic context;
- `this`, parameter, and local name/slot/type observations;
- Portable PDB document/method/scope identity and nested import scopes;
- namespace imports, type aliases, namespace aliases, `using static`, and extern-alias facts;
- loaded modules and metadata reference relationships; and
- exact/partial/unavailable/conflict/invalid status, retained reads, source identities, and reached bounds for every
  contributing source.

The context is additive, not all-or-nothing. A fully qualified static field binds from exact metadata even when the
frame or PDB is unavailable. A simple name that depends on missing imports cannot be guessed; it returns a typed
context-unavailable or ambiguous result. Exact context facts narrow candidates according to an explicitly versioned
name-resolution profile and the result records only the facts actually consulted.

W7 implements one real context producer. A request may select one managed thread/frame under fixed bounds. The
adapter correlates that frame to one module and MethodDef, derives the declaring namespace from counted metadata, and
resolves a Portable PDB by exact module debug identity. The PDB's MethodDebugInformation, LocalScope, and ImportScope
rows produce the active namespace imports and type/namespace aliases. The evaluator asks a bounded host artifact
resolver for candidate PDB bytes; a path or filename is never trusted, the bytes are bounded and hashed, and an
identity mismatch is conflict. If an exact IL location is unavailable, W7 may use imports only when every candidate
containing method scope yields the same effective import set; otherwise contextual lookup is unavailable while fully
qualified lookup remains valid.

The version-one contextual binder implements only:

1. an explicit type alias or namespace alias from the active nested import chain;
2. the declaring namespace and its enclosing namespace chain;
3. active namespace imports, including global imports when represented by exact PDB evidence; and
4. counted loaded/reference module metadata, with exactly one resulting TypeDef/FieldDef required.

It does not claim Roslyn-equivalent semantic binding. Unsupported import kinds remain retained context facts, and
partial PDB/frame evidence remains partial rather than being silently discarded. W7's executable fully qualified,
current-namespace, namespace-import, and simple-alias slice is `~10K LOC`. Mature debugger parity across locals,
generic substitution, all import kinds, assemblies, language versions, and expression forms remains an approximately
`~100K LOC` trajectory. A spike may compare the project-owned binder with a Roslyn semantic model built from
reconstructed exact context, but no semantic-model object or compiler diagnostic enters canonical product contracts.

### 3.4 Admissible static field and value shapes

W7 admits one ordinary application-domain static storage family:

- one directly declared FieldDef on a non-generic top-level class TypeDef;
- `static`, non-literal, non-thread-static storage;
- one runtime module candidate and owning application domain after name binding;
- a nonzero static slot address reported for that domain; and
- exactly the raw reads required by the declared value shape.

The first terminal value set is exact null, `Int32`, `Nullable<Int32>`, string, and a snapshot-scoped object reference.
The object-reference value carries address, runtime type/module/TypeDef, method table, extent, static slot provenance,
and canonical snapshot identity; legacy W2/W5/W6 paths never emit it. A reference-valued static may instead become the
receiver for one already admitted W2 direct-member or W6 fixed-depth member-chain suffix.

Interface, base-class, array, boxed value, cross-module declared reference, runtime-subtype assignment, value-type
aggregate, pointer, function-pointer, and other signatures are unsupported in v1 even when the CLR could represent
them. Static properties are not fields and remain unsupported rather than invoking a getter.

### 3.5 Exact null versus unavailable storage

W7 distinguishes facts that high-level convenience reads can collapse:

1. no unique module/type/field binding: absence, ambiguity, incomplete context, or invalid metadata;
2. `GetAddress` returns zero: no validated initialized field slot, reported as unavailable storage;
3. the slot exists but the raw read is non-exact: partial/unavailable/conflict/invalid evidence, never null;
4. an exact reference slot contains zero: an exact null value; and
5. an exact non-null reference fails target validation: unavailable/conflict/invalid according to the first failed
   evidence boundary.

For a terminal static-reference expression, exact zero returns the existing exact `Null` value. For a static
reference used as a receiver, direct access preserves the existing null-dereference blocking result; conditional
access produces exact null and may apply only an admitted compatible fallback. Parsing and name binding already
occurred, but no target or suffix read occurs after exact null.

### 3.6 Successful result

Successful binding freezes:

- exact raw expression and `StaticFieldExpressionV1` profile identity;
- decoded namespace/type/field names and optional instance suffix;
- binding-context identity and every context fact actually consulted;
- counted module identity, TypeDef/FieldDef/signature, owning application domain, and static slot;
- value-shape decoder, exact raw reads, target validation when applicable, and reached bounds;
- a terminal static value or exact object binding plus immutable suffix plan; and
- versioned canonical request, binding, plan, result, and digest identities.

No later phase repeats name binding, declaration lookup, slot acquisition, or decoder selection. Existing root-based
entry points and canonical bytes remain unchanged.

## 4) Physical evidence chain

### 4.1 Module selection

Binding begins with the already bounded runtime-module catalog and projected syntax candidates. A fully qualified
candidate searches the bounded module universe directly; a simple-name candidate is first expanded only by exact
current-namespace/import/alias facts from the selected context. Each relevant loaded managed module contributes
counted metadata or a typed non-exact module observation. W7 requires exactly one complete module/TypeDef/FieldDef
binding. Zero complete candidates are exhaustive symbol absence only when every relevant catalog, context, and
metadata observation is exact; multiple candidates are ambiguity.

The binder does not choose by enumeration order, target path, image base, filename similarity, import order, or first
namespace/type split. Exact selected-frame/reference context may narrow the module universe before comparison, but
missing context may not discard an otherwise viable fully qualified candidate. For the sole candidate, counted dump
metadata produces the existing `ModuleContentIdentity` containing MVID, metadata length, and metadata SHA-256. A
target path is a display hint only.
No analysis-machine PE is opened as type/field/value binding input. An independently identity-validated module
artifact may supply only its debug-directory identity to the optional context path described next.

### 4.2 Selected-frame and Portable PDB context

Contextual lookup begins only after syntax projection proves the name needs it. The fixed evidence sequence is:

1. select exactly one managed thread/frame from the context request under bounded stack enumeration;
2. correlate the frame's runtime method and instruction pointer to one runtime module and counted MethodDef;
3. derive the declaring namespace and bounded metadata-reference universe from that exact MethodDef/module;
4. acquire the module debug identity from counted dump/module evidence or an independently identity-validated module
   artifact;
5. ask the bounded artifact resolver for candidate Portable PDB bytes and accept exactly one identity match;
6. hash the accepted bytes and use SRM to project the MethodDebugInformation, candidate LocalScope rows, nested
   ImportScope chain, namespace imports, and type/namespace aliases; and
7. freeze the exact facts consulted by contextual name binding plus every non-exact source and reached bound.

Thread/frame ordinal, instruction address, runtime module, MethodDef token, module/PDB identity, PDB content hash,
scope handles/ranges, import handles, decoded names, assembly references, and source statuses remain separate evidence.
A source path is display-only. If the native instruction pointer cannot map to one IL location, imports are exact only
when all otherwise viable containing scopes have the same effective import chain. A mismatch is not repaired from a
source file, decompiler, neighboring frame, or filename. Fully qualified lookup does not execute this sequence.

Portable PDB bytes are artifact evidence, not dump bytes. A module artifact used only to obtain debug identity is also
artifact evidence and cannot supply TypeDef/FieldDef binding, static storage, or a value. Tests label those sources
independently and poison each capability in isolation.

### 4.3 Metadata declaration

The binder resolves the projected name to exactly one TypeDef and one directly declared FieldDef from counted
metadata. It validates:

- non-nil and in-range tokens;
- exact decoded namespace, top-level type, and field names;
- class TypeDef shape and absence of generic parameters;
- `static`, non-literal, non-thread-static attributes;
- an admitted scalar, string, nullable, or concrete reference signature; and
- agreement between counted metadata tokens and the ClrMD runtime type/field projections used only to locate the
  runtime slot.

Missing declaration is exhaustive symbol absence only after an exact bounded search. Duplicate bindings are
ambiguous, token/name disagreement is conflict, malformed metadata is invalid, and incomplete metadata is partial or
unavailable. A property, event, method, nested type, or inherited member with the same final identifier does not become
a field candidate.

### 4.4 Application domain and slot

The selected `ClrmdModuleInfo` already carries a snapshot-scoped application-domain address. The adapter must map that
address back to exactly one ClrMD application domain and reject zero or multiple matches. It then obtains the ordinary
static field's storage address for that domain.

The ClrMD address operation is permitted only to locate the slot. It is not value evidence. The project-owned memory
reader performs the sole authoritative reads required by the frozen decoder and retains source identity, address,
requested width, status, observed bytes, and issue mapping. `Int32` reads four bytes; nullable values use the frozen
relative layout; references and strings begin with one pointer-width slot read.

### 4.5 Target validation

For an exact non-null object pointer, acquisition validates before creating an object-reference value or suffix root:

1. the pointer is representable for the dump architecture and belongs to the same snapshot;
2. the heap projects one valid object at that exact address;
3. a raw pointer-width read at the object address yields the exact method table;
4. the raw method table agrees with ClrMD's target type;
5. runtime module and TypeDef identity equal the declared static-field target type; and
6. object extent is sufficient for the later selected member operation.

The last check may be completed during suffix preparation, but any target fact acquired during static-field
evaluation is retained and correlated rather than silently reacquired with a different result. String pointers follow
the existing bounded string-object decoder instead of the general object-reference path.

### 4.6 Oracle boundary

`ClrStaticField.ReadObject`, `Read<T>`, heap enumeration, reflection, and an independently opened disk PE may be used
only as late equality oracles in tests. None may supply the product symbol binding, value bytes, pointer, object
identity, or result. Poison tests prove the product path succeeds without those calls and fails truthfully when counted
metadata or raw storage is non-exact.

## 5) Ownership and API direction

### 5.1 Responsibility map

| Stage | Owner | Input | Output |
|---|---|---|---|
| Parse/project | Dump-query front end | Raw text plus selected profile | Static-access descriptor or syntax disposition |
| Gather context | Host context adapters | Optional frame/PDB/metadata sources | Immutable typed binding context |
| Bind name | Product metadata binder | Descriptor, context, module catalog | Unique TypeDef/FieldDef binding or typed failure |
| Locate slot | ClrMD adapter | Exact runtime module/type/field/domain mapping | Slot address or typed miss |
| Read value | Host memory capability | Slot plus frozen decoder | Immutable raw evidence and typed value observation |
| Validate reference | ClrMD/raw-memory adapter | Exact non-null pointer plus declaration | Object-reference value/root or typed miss |
| Prepare suffix | Existing dump-query product | Exact reference plus admitted suffix | Immutable W2/W6-compatible plan |
| Evaluate | Existing/new product route | Frozen static or suffix plan | Versioned derived-query outcome |

### 5.2 Adapter contracts

The host layer should add immutable binding/storage/context result families rather than expose `ClrStaticField`, PDB
reader objects, or live stack objects:

- `ClrmdSelectedFrameObservation` retains the bounded selector, runtime module/MethodDef/instruction correlation,
  independent evidence status, source identity, and reached bounds;
- `PortablePdbBindingContextObservation` retains exact debug/content identity, method/local/import-scope identities,
  decoded namespace/import/alias facts, non-exact status, and reached bounds;
- `DumpExpressionBindingContext` combines only project-owned frame/PDB/import/module facts and typed evidence status;
- `ClrmdStaticFieldDeclaration` retains counted module content, runtime module, domain, TypeDef, FieldDef, declared
  value shape, and applied bounds;
- `ClrmdStaticFieldValueObservation` retains status, issue, declaration when complete, slot address, decoder, exact
  null/value/object when available, raw reads, and canonical replay projection; and
- `ClrmdDumpSession.ReadStaticField` performs the fixed storage operation once for an already bound declaration.

Names are provisional direction, not compatibility promises. Every public type and method must carry detailed XML
documentation of intent, parameters/returns, failure behavior, and caveats.

### 5.3 Product contracts

The product layer should add:

- `StaticFieldExpressionV1` in `DumpExpressionLanguageProfile` and a project-owned static-access descriptor;
- a bounded metadata name binder that consumes the descriptor plus an internally frozen optional
  `DumpExpressionBindingContext`;
- immutable static declaration, value, object-reference, optional suffix-plan, and canonical identity records;
- append-only syntax, context, name-binding, declaration, storage, and suffix-preparation failure cases in the strict
  expression outcome union; and
- a high-level evaluator overload that accepts raw expression plus an optional `DumpExpressionContextRequest` naming a
  thread/frame selector, asks host stack/artifact capabilities to build the context only when the projected name needs
  it, then binds once, acquires storage once, and evaluates only a frozen result/plan.

The existing root-binding overloads remain the compatibility path and never reinterpret input as static syntax. The
new profile requires no caller-assembled binding context, TypeDef, FieldDef, address, or object root. A lower-level
context-taking binder remains an internal/dump-free-test seam, not the expected debugger-facing API.

### 5.4 Root-source model

`ClrmdHeapObjectInfo.RootAddress` and `RootKind` currently describe a CLR handle slot. W7 must not encode a static slot
under misleading handle terminology. The implementation should introduce a typed source-agnostic object provenance
value with at least:

1. strong-handle selection;
2. host-supplied exact object; and
3. static-field expression binding.

Existing compatibility properties remain unchanged. The static case retains expression, context, symbol,
declaration, domain, slot, read, and target identity. Semantic object equality may match a strong-handle-selected
object while provenance and canonical request identities remain different.

### 5.5 Canonical identity

Legacy W2/W5/W6 requests, plans, results, root-selection provenance, and schema-v1/v2/v3 reports must remain identical.
The static route receives new versioned identities rather than changing old encodings in place:

- syntax identity: exact raw text, profile, projected name segments, static-field/suffix shape, and parser bounds;
- context identity: snapshot plus only frame/PDB/import/module facts consulted by binding;
- symbol identity: candidate search, chosen module content, TypeDef/FieldDef tokens, signature, and ambiguity state;
- observation identity: declaration plus domain/slot, decoder, raw evidence, target/value, status, issue, and bounds;
- optional suffix-plan identity: static object provenance plus unchanged instance-member descriptor; and
- result identity: request, symbol, observation, optional plan, semantic value, and independent evidence axes.

Runtime addresses remain snapshot-scoped. Same-dump close/reopen must reproduce bytes exactly; a newly generated dump
is a distinct snapshot and is not expected to share its runtime identity.

## 6) Outcome matrix

The front end, binder, adapter, and product retain these cases without exception-text diagnostics or fallback
selection. Every bounded row is parsed once; the final column says whether value/suffix evaluation proceeds:

| Boundary | Evidence | Product outcome | Evaluate value/suffix? |
|---|---|---|---:|
| Missing/malformed/over-limit C# | Syntax | Invalid | No |
| Valid tree outside `StaticFieldExpressionV1` | Syntax | Unsupported | No |
| Static-access descriptor exceeds segment/depth bound | Syntax | Invalid limit | No |
| Fully qualified name plus unavailable frame/PDB | Context not consulted | Continue metadata binding | Not yet |
| Simple name with no unique selected frame/MethodDef | Context | Context unavailable/ambiguous | No |
| Portable PDB unavailable or instruction scope unresolved | Context | Context unavailable/partial | No |
| Portable PDB identity disagrees with module | Conflict | Context conflict | No |
| Exact current namespace/import/alias expansion | Context | Continue metadata binding | Not yet |
| Runtime module catalog incomplete | Partial/limit | Partial binding | No |
| Relevant module metadata unavailable/partial | Non-exact | Unavailable/partial binding | No |
| No TypeDef/FieldDef candidate after exact search | Exact exhaustive | Symbol absent | No |
| Multiple complete module/type/field candidates | Exact | Ambiguous symbol | No |
| Module/token/name/signature disagreement | Conflict | Conflict | No |
| Malformed metadata or binding payload | Invalid | Invalid | No |
| Instance/literal/thread/generic/property/unsupported field shape | Exact declaration | Unsupported | No |
| Owning application domain unavailable/ambiguous | Non-exact | Unavailable/conflict | No |
| Static slot address is zero | Unavailable | Storage unavailable | No |
| Raw slot/value read partial or unavailable | Non-exact | Partial/unavailable value | No |
| Raw bytes violate frozen decoder | Invalid | Invalid value | No |
| Exact scalar/string/nullable value | Exact | Exact value | Yes |
| Reference slot exactly zero, terminal expression | Exact | Exact null value | Yes |
| Reference slot exactly zero, direct suffix | Exact | Null receiver result | Yes, no member read |
| Reference slot exactly zero, conditional suffix | Exact | Exact null/fallback | Yes, no member read |
| Target object/header unavailable/partial | Non-exact | Unavailable/partial | No |
| Target method table/type/module disagrees | Conflict | Conflict | No |
| Target structure/extent invalid | Invalid | Invalid | No |
| Complete compatible non-null reference | Exact | Object value or frozen suffix plan | Yes |

Unsupported syntax/symbol/storage is not invalid data. Exact null is not symbol absence or unavailable storage.
Partial evidence is not null. Ambiguous, conflict, invalid, and non-exact outcomes never expose an arbitrarily chosen
symbol or object.

## 7) Deterministic bounds

W7 reuses or adds only bounds tied to operations it actually performs:

| Bound | Rule |
|---|---|
| Expression text | Existing pre-parse length cap |
| Syntax nodes/tokens/depth/identifier values | Existing post-parse bounds before binding |
| Qualified-name segments and candidate splits | Fixed new bounds before metadata traversal |
| Runtime modules | Reuse the session's bounded module catalog; do not enumerate again in the product |
| Counted metadata | Reuse the managed metadata admission cap and record the exact read |
| Type/field candidates | Fixed per-module and aggregate traversal/candidate caps with exact counters |
| Binding context | Fixed thread/frame/scope/import/alias/reference counts and PDB byte cap; record only consulted exact/non-exact facts |
| Application domains | Fixed bounded lookup over the session/runtime catalog |
| Static storage reads | Exactly the frozen decoder's fixed read set after unique binding |
| Target-header reads | Exactly one pointer-width method-table read before exact-object issuance |
| Expression parser | Exactly one complete Roslyn parse before any frame, PDB, metadata, or dump access |
| Suffix preparation/evaluation | Existing W2/W6 bounds and no-rebinding rules |

Limits that are not reached do not appear in result context. Exhaustion fails before the next capability call. Context
bounds apply to the selected-frame producer and remain extensible for later producers; they do not claim unavailable
frame/PDB facts were observed. No W7 counter is a dormant promise for generic statics, collection traversal, or
arbitrary C# semantics.

## 8) Synthetic evidence design

### 8.1 Physical truth gate

W7.1 uses the existing optimized Release target unchanged. While
`PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root` is live, the gate must prove:

- one complete Roslyn parse and one exact static-access projection for
  `global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root`;
- exactly one target runtime/module/type/ordinary static field/application-domain mapping;
- a nonzero initialized slot address;
- one exact raw pointer read from that slot;
- one exact raw method-table read from the pointed object;
- agreement with the counted TypeDef/FieldDef/signature projection;
- late agreement with `ClrStaticField.ReadObject` and the already discovered heap object;
- the same exact object as a terminal object-reference value and as the receiver of `.Marker`; and
- the exact `Marker` value `0x4A44C004` through the existing W2 decoder after product integration.

If the pinned DAC cannot produce a stable slot address for this exact fixture, W7 stops at the truth gate. The project
must record the unavailable boundary and reconsider the selected scenario rather than adding a heuristic.

### 8.2 Meaningful multi-shape target

After the truth gate passes, a dedicated W7 target freezes four unrelated application graphs and several static value
shapes. Each process pauses inside a no-inline method whose selected frame, MethodDef, declaring namespace, Portable
PDB identity, nested lexical scope, and active imports are predeclared evidence:

1. request pipeline with an `Int32` counter and current-request reference;
2. batch pipeline with a string state and nullable progress marker;
3. coordinator pipeline with owner/job/shard/worker relationships and a type alias; and
4. workflow/dispatch pipeline with current attempt, worker, attempts, and an intentionally competing imported type.

Every exact case has a fully qualified expression and, where the context profile admits it, a current-namespace,
namespace-import, or alias spelling that must resolve to the same symbol/value. Poison variants independently remove
the selected frame, MethodDef correlation, instruction scope, PDB, one import, or one module so the first unavailable,
partial, conflict, invalid, or ambiguous boundary is observable. Fully qualified variants must remain exact under
every poison that affects only contextual lookup. Separate frozen companion projects provide the identity-mismatching
Portable PDB and the second explicitly loaded assembly containing a duplicate fully qualified declaration; neither is
left to an unspecified test mutation.

The target is separate from `PhoenixInspect.TestTarget` and `PhoenixInspect.OptimizedContextTestTarget`, so W7 does not
silently refresh W2–W6 artifact identities. Every target launch and dump/consumer process is hidden and headless.

### 8.3 Predeclared sixteen-incident portfolio

Before implementation changes target bytes, a source-controlled manifest freezes sixteen independent dump incidents:

| # | Shape | Binding/storage condition | Expression / expected first result |
|---:|---|---|---|
| 1 | Request | Fully qualified scalar, no context required | `global::PhoenixInspect.W7TestTarget.StaticValues.Counter` → exact `Int32` |
| 2 | Batch | Exact namespace import plus one W2 direct-field suffix | `BatchStatics.Root.State` → exact string and same symbol/value as its fully qualified spelling |
| 3 | Coordinator | Exact type alias | `CoordinatorValues.Root.Owner?.Name` → exact string through a frozen conditional suffix |
| 4 | Workflow | Exact current namespace | `WorkflowStatics.Root.CurrentAttempt.Status` → exact string through a frozen W6 chain |
| 5 | Request | Exact null static reference | Fully qualified terminal expression → exact null after parse and binding |
| 6 | Batch | Exact nullable with no value | Imported simple name → exact `Nullable<Int32>` no-value result |
| 7 | Coordinator | Selected frame unavailable | Simple name → context unavailable; fully qualified control remains exact |
| 8 | Workflow | PDB bytes unavailable/partial | Imported `BatchStatics.Root` → unavailable/partial context; fully qualified control remains exact |
| 9 | Request | PDB identity disagrees with module | Simple name → context conflict; no metadata candidate search |
| 10 | Batch | Two active imports define the same simple type | Simple name → ambiguous symbol; fully qualified control remains exact |
| 11 | Coordinator | Duplicate fully qualified type/field in two loaded modules | Fully qualified name → ambiguous symbol; no slot read |
| 12 | Workflow | Exact field absence plus invalid projected-signature comparison | Exhaustive absence and invalid symbol payload remain distinct; no slot read in either view |
| 13 | Request | Ordinary static slot address unavailable | Storage unavailable after exact syntax/name binding |
| 14 | Batch | Partial scalar/reference slot bytes | Partial value; no decoder fabrication or target lookup |
| 15 | Coordinator | Reference target type/header conflict | Conflict before object value or suffix plan issuance |
| 16 | Workflow | Valid C# property/call/generic form outside v1 | Stable unsupported result before context, metadata, or dump-memory access |

Every incident has its own full snapshot and predeclared target arguments, raw expression, language profile, optional
thread/frame and PDB resolver inputs, expected syntax/context/symbol/storage/value outcome, usefulness classification,
decision impact, first boundary, and application shape. Evidence views may mask a captured read as non-exact or
introduce an explicit conflict/invalid projection, but they may not invent an exact context fact, symbol, pointer, or
value absent from the dump and associated PDB.

### 8.4 Required differential cases

The generated conformance lane additionally proves:

- `global::Namespace.Type.Field`, a unique dot-qualified spelling, an imported `Type.Field`, and a type-alias spelling
  bind the same FieldDef/value when their evidence is exact, while preserving distinct raw-request/context provenance;
- removing all stack and PDB evidence leaves the fully qualified result unchanged but makes a dependent simple name
  context-unavailable;
- replacing an active import with an unrelated import changes only contextual candidate construction and never causes
  a fallback to the first loaded type;
- two same-named types imported from different namespaces are ambiguous regardless of import enumeration order;
- the exact object selected through a static slot and, in an oracle-only comparison, a strong handle yields equal W2/
  W6 semantic values but distinct root-source/request/plan provenance;
- poisoning strong-handle lookup, heap search, `ClrStaticField.ReadObject`, and disk-PE lookup does not affect the raw
  static product path;
- one request performs one Roslyn parse, one context acquisition when needed, one bounded symbol binding, one storage
  acquisition, and no rebinding during suffix evaluation;
- malformed neighboring expressions and complex valid-but-unadmitted Roslyn trees never trigger frame, PDB, metadata,
  or dump-memory capabilities;
- two fresh headless consumers and dump close/reopen reproduce canonical syntax, context, symbol, observation, request,
  plan, result, and report bytes;
- exact-null and unavailable-slot cases remain different; and
- W2/W5/W6 default entry points and canonical corpora remain unchanged.

### 8.5 Corpus separation

W7 reports retain controlled/generated, designed/synthetic, and representative/external-observation kinds as separate
non-promotable categories. The sixteen incidents are designed evidence and contribute zero representative questions,
incidents, or application shapes. Dump-free binder vectors, real dump/PDB integration artifacts, and late semantic
oracles are also labeled separately; a synthetic import scope is never reported as a recovered runtime context. No
percentage or production recoverability claim is emitted.

## 9) Delivery sequence

### W7.0 — contract and roadmap freeze

**Scale:** `~1K LOC` documentation.

**Implemented checkpoint:** `d692d9ec4` established the initial roadmap; `40be322ae` incorporated the required
static-member syntax, fully qualified guarantee, selected-frame/PDB/import binding slice, and maximally inclusive
closure gates before product implementation.

Publish this plan, link it from active navigation, add traceability requirements, and freeze the exact static-field
expression profiles, context-independent guarantee, selected-frame/PDB import slice, exclusions, evidence chain,
portfolio outline, and closure rule before implementation.

**Exit gate**

- W6 remains closed and its evidence is not rewritten.
- At the W7.0 freeze, current status surfaces identify W7 as the sole active delivery sequence.
- The static-field choice is tied to both the W6 decision, the preexisting optimized-context axis, and the debugger-
  context product requirement.
- Documents require fully qualified access without context and the bounded selected-frame/PDB/import slice for simple
  names; arbitrary addresses, pointer recovery, locals, and general C# semantics remain outside W7.
- Markdown and headless-workflow guards pass; the authored-scope vocabulary scan is clean.

### W7.1 — static-slot physical truth gate and fixture freeze

**Scale:** `~1K LOC` implementation and tests.

**Implemented checkpoint (2026-07-17):** ClrMD 3.1.455904 correlated the exact module, TypeDef, and
FieldDef but returned a zero slot for the .NET 10 fixture. The pin therefore moved to ClrMD 4.0.732401 and diagnostics
client 0.2.661903, with the offline locator installed through `DataTargetOptions` at construction. The unchanged
optimized target now proves the full counted metadata → runtime declaration/domain → slot → raw pointer → raw method
table → late oracle chain, including dump close/reopen/rebind replay. The source-controlled v1 manifest freezes sixteen
independent designed incidents, four application shapes, qualified-control equality contracts, one-hop W2 and W6
suffix coverage, dedicated target/Portable-PDB/companion-assembly inputs, and the complete typed status taxonomy before
target implementation. A capability-injected dump-free stage seam proves short-circuit behavior for selected frame,
PDB identity/import, exact-null, partial, unavailable, ambiguous, conflict, invalid, and unsupported outcomes.

Prove the complete fully qualified physical chain against the existing optimized target without changing product
behavior. Freeze the dedicated W7 target source/PDB inputs and sixteen-incident draft before target-affecting
implementation.

**Exit gate**

- The pinned ClrMD package returns one stable nonzero slot address for the exact static field/domain.
- Roslyn's pinned parser produces the expected bounded member-access tree for the fully qualified and contextual
  spellings; no project parser or text splitter is introduced.
- Project memory reads the exact pointer and target method table once each.
- Counted metadata and runtime projection agree on module, TypeDef, FieldDef, signature, and target type.
- `ReadObject` and heap discovery are late oracles only.
- Selected-frame, PDB identity/import scope, exact-null, partial, unavailable, ambiguous, conflict, invalid, and
  unsupported distinctions are proven through dump-free seams before adapter implementation.
- If the exact slot cannot be proven, W7 records deferral and does not proceed to W7.2.

### W7.2 — immutable syntax, context, symbol, observation, and provenance contracts

**Scale:** `~10K LOC` implementation and tests. The initial `~1K LOC` scale understated the complete immutable contract
family, canonical encodings, defensive-copy rules, legacy golden tests, XML documentation, and invalid-input invariant
matrix required by this checkpoint.

**Implemented checkpoints:** `b38c13d9a` through `f6b692899` freeze the immutable syntax, frame/PDB/import, metadata,
runtime declaration, value, object/provenance, canonical encoding, defensive-copy, and invariant families while
preserving legacy identities.

Add defensively immutable, content-equal host/product contracts for the projected static-access tree, selected frame,
PDB/import evidence, symbol binding, storage observation, terminal value/object, and typed root provenance. Freeze
canonical encodings before composition.

**Exit gate**

- Syntax, context, symbol, and value statuses validate complete/no-partial payload invariants.
- Strong-handle root behavior and canonical bytes remain unchanged.
- Context identity includes only consulted frame/PDB/import/module facts and retains non-exact evidence without
  pretending it contributed an exact candidate.
- Static declaration/observation identities include all syntax, context, metadata, domain, slot, read, target, status,
  issue, and reached-bound facts.
- Default arrays, malformed tokens, foreign snapshots, mismatched PDB identities, duplicate bounds, wrong pointer
  widths, and contradictory payloads are rejected.
- Public types/methods have detailed XML documentation.

### W7.3 — bounded Roslyn projection and fully qualified metadata binder

**Scale:** `~1K LOC` implementation and tests.

**Implemented checkpoints:** `514b72c46`, `2cbc4790d`, `6441c214f`, and `ee0baa0fb` route W7 through the sole complete
Roslyn parse, project bounded structural candidates, expose one counted metadata catalog, and require one complete
fully qualified TypeDef/FieldDef result independent of frame/PDB capabilities.

Project the admitted Roslyn tree into bounded type/field/suffix candidates and implement the context-independent
metadata binder for `global::Namespace.Type.Field` and unique dot-qualified equivalents.

**Exit gate**

- The complete pinned Roslyn parse occurs once and all admitted/unsupported/invalid trees have stable dispositions.
- Candidate prefix/suffix splits are bounded; enumeration order and longest-prefix heuristics cannot choose a symbol.
- Counted metadata is authoritative for TypeDef/FieldDef identity and exactly one complete candidate is required.
- Fully qualified binding succeeds with frame/PDB capabilities poisoned or absent.
- Missing, duplicate, partial, unavailable, conflict, invalid, and unsupported declarations are distinct and no failed
  binding exposes a selected declaration.
- The binder performs no static slot/value read, heap scan, high-level value read, disk PE lookup, or repeated module
  catalog lookup.

### W7.4 — selected-frame, Portable PDB, and contextual name binding

**Scale:** `~10K LOC` implementation and tests. This includes selected-frame correlation, counted mapped-PE debug
identity, artifact resolution, Portable-PDB scope/import projection, contextual alias/reference binding, and real-dump
plus dump-free replay tests.

**Implemented checkpoints:** `077afcaa1` through `70c16b1d0` add current-namespace/import/type-alias/namespace-alias
expansion, exact selected-frame correlation, mapped-PE debug identity, injected bounded Portable-PDB acquisition,
LocalScope/ImportScope projection, contextual binding, real-dump composition, and lazy context acquisition that
fully qualified requests never invoke.

Implement the bounded selected-frame context producer, exact Portable PDB identity/scope/import reader, and the
version-one current-namespace, namespace-import, and type/namespace-alias name-resolution rules.

**Exit gate**

- Thread/frame selection, MethodDef correlation, instruction-scope selection, PDB resolution, and import expansion are
  independently bounded and typed.
- PDB bytes are accepted only after exact module debug-identity validation; paths and filenames are display hints.
- An exact current namespace, namespace import, type alias, or namespace alias can bind a simple static expression only
  when exactly one TypeDef/FieldDef remains.
- Missing frame/PDB/scope evidence is context-unavailable or partial; mismatched identity is conflict; equal candidates
  are ambiguity; none selects the first module or import.
- Context-only poison leaves fully qualified binding and canonical results unchanged.
- Dump-free import vectors and at least one real selected-frame/PDB dump case agree on contextual candidate expansion.

### W7.5 — counted static storage, value projection, and expression composition

**Scale:** `~10K LOC` implementation and tests. The initial `~1K LOC` estimate understated runtime type/declaration
mapping, nullable layout, assignability, raw value decoding, object validation, product composition, and reuse of the
existing W2/W6 suffix engines.

**Implemented checkpoints:** `777e85749` through `42cae2577`, followed by `122fafe0c`, implement the bounded runtime
declaration map, raw ordinary-static storage/value decoders, nullable layout, reference assignability/target
validation, terminal/object result projection, complete static-expression pipeline, and unchanged W2/W6 suffix
composition. `f99b12ee7` finally consolidates all profiles behind the sole complete Roslyn parse site.

Implement the fixed `ClrmdDumpSession` storage operation, direct scalar/string/nullable/object results, exact reference
validation, and composition of a reference-valued static with unchanged W2/W6 suffix planning. Add a versioned
headless report path without rewriting schema-v1/v2/v3 consumers.

**Exit gate**

- Exact success performs only the frozen value decoder's reads; a reference suffix adds one target-header validation
  and then only the existing W2/W6 bounded reads.
- `ClrStaticField.ReadObject`, heap enumeration, strong-handle lookup, disk PE lookup, and any target-code execution are
  late oracles or forbidden paths, never product evidence.
- Exact scalar, string, nullable, null, and object-reference results remain distinct from partial/unavailable storage.
- The optimized static axis changes from unavailable to exact only on the new opt-in path and returns `0x4A44C004`.
- Existing strong-root and all other optimized-context axes retain their historical classifications unless executable
  evidence independently changes them.
- Direct-field and fixed-depth member-chain exact/non-exact results preserve existing semantics after static binding.
- Static expression/context/symbol/storage provenance reaches the new request/plan/result identity without altering
  semantic value equality or legacy root-binding encodings.
- Same process, fresh processes, and close/reopen reproduce canonical artifacts.
- All target, dump writer, consumer, and test processes are hidden/headless.
- The existing complex valid-but-unsupported Roslyn corpus plus qualified-name neighbors prove W7 adds no syntax
  shortcut or second parse.

### W7.6 — meaningful synthetic portfolio and next decision

**Scale:** `~1K LOC` implementation and tests.

**Implemented checkpoints:** `c94c0a39d`, `7e4c8f0fe`, and `00a9b83f3` materialize the target, identity-conflict PDB,
duplicate-type assembly, sixteen independent full dumps, two fresh hidden consumers, exact and typed non-exact
views, context poison controls, deterministic reports, and complete solution configuration coverage. The unique
threshold-qualified leader is `BindingContextPrecision`; the selected documentation-only action is
`AddOneEvidenceBackedFramePdbImportAliasGenericRule`. A substantive tie still defers.

Materialize the sixteen predeclared incidents across four unrelated shapes and run two fresh portfolio consumers.

**Exit gate**

- All sixteen snapshots are distinct and every row reproduces its predeclared acquisition and expression outcome.
- Exact answers span all four shapes, fully qualified/current-namespace/import/alias lookup, direct static values, W2
  direct-field suffixes, and W6 member-chain suffixes.
- Non-exact rows retain their first stopping boundary without fallback or candidate leakage.
- Context poison differentials prove fully qualified expressions remain independent of stack/PDB availability.
- Two fresh reports are byte-identical; representative counts remain zero; corpus promotion is rejected.
- A unique threshold-qualified post-W7 action is selected or the successor is explicitly deferred.
- Enum order cannot choose between substantively tied candidates.

### W7.7 — repository closure

**Scale:** `~100 LOC` documentation.

Reconcile authoritative documents, run the complete headless matrix, record coarse realized scales, and close W7 only
at the exact pushed commit satisfying the required gates.

**Exit gate**

- Locked restore and strict Release build pass for the complete solution with zero warnings/errors.
- Complete unit, Fast, ordinary dump, optimized dump, focused W7 syntax/context/binder/storage, generated conformance,
  and meaningful synthetic lanes pass with zero skips.
- W2–W6 canonical encodings, default classifications, schemas, outcomes, and gates remain unchanged except for any
  explicit W7 target/report version introduced by this plan.
- Markdown, headless-workflow, and authored-scope vocabulary guards pass.
- Required hosted jobs execute and pass at the exact pushed closure commit, absent a new explicit W7-only owner
  disposition.
- Any selected post-W7 action is documented but not implemented as W7 work.

**Closure record (2026-07-17):** exact implementation source baseline
`f99b12ee74544194829eb90385c80ecfe80d3ebf` passes locked restore; strict serial Release solution build at zero
warnings/errors; unit 507/507; complete integration 242/242; Fast 184/184; ordinary CI dump 29/29; optimized context
1/1; focused W7 98/98; `StaticFieldExpressionV1` 1/1; and `W7MeaningfulSyntheticV1` 1/1, all with zero skips. Markdown
verification covers 66 files and 106 local destinations; the workflow guard verifies every managed restore/build/test
invocation is headless; and the authored-scope vocabulary guard passes. Only one production call to
`SyntaxFactory.ParseExpression` remains. The checked-in four-job workflow is unchanged. GitHub rejected required jobs
for the already-recorded billing condition; the owner's explicit W7-only override closes that hosted-only condition
without representing the rejected jobs as execution evidence.

## 10) Verification matrix

All managed commands remain headless. The exact filters were refined as tests landed; W7 requires:

| Layer | Required proof |
|---|---|
| Contract unit | Syntax/context/symbol/value invariants, status payloads, canonical identities, legacy byte preservation |
| Parser/binder unit | One Roslyn parse, bounded prefix/suffix projection, qualified/import/alias lookup, ambiguity, no capabilities on unsupported syntax |
| Context adapter unit | Frame/MethodDef correlation, PDB identity, IL/scope/import chains, typed non-exact cases, fixed counters |
| Storage adapter unit | Module/type/field/domain mapping, slot address, raw value/reference, exact null, target validation, typed misses |
| Adapter integration | Existing optimized static field, selected frame/PDB, counted reads, metadata correlation, oracle separation |
| Product Fast | Opt-in profile, no fallback/reparse/rebind, strict outcome union, old overload preservation |
| Ordinary generated dump | Qualified/contextual bindings, direct values, W2/W6 suffixes, fresh process, close/reopen/rebind |
| Optimized context | Fully qualified static axis exact on W7 path; other five-axis classifications remain explicit |
| Meaningful synthetic | Sixteen independent dumps, four shapes, context poisons, raw counts, unique decision or deferral, zero representative rows |
| Repository policy | Markdown links, headless workflow, authored-scope vocabulary, clean strict build |

Expected command shape:

```powershell
.\eng\Invoke-HeadlessProcess.ps1 dotnet restore PhoenixInspect.sln --locked-mode
.\eng\Invoke-HeadlessProcess.ps1 dotnet build PhoenixInspect.sln --configuration Release --no-restore --verbosity minimal --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:ContinuousIntegrationBuild=true
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.Tests/PhoenixInspect.Tests.csproj --configuration Release --no-build --no-restore --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast" --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=StaticFieldExpressionV1" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W7MeaningfulSyntheticV1" --verbosity normal
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-markdown-links.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-headless-workflows.ps1
```

A missing platform prerequisite is a failed/open gate, not a skipped success.

## 11) Post-W7 decision gate

W7.6 ranks first blockers by independent incident count, decision-changing count, useful-answer count, and exact
attributable-evidence count. Enum order may sort rows but may not select a successor. A branch is selected only when it
is the unique leader after all substantive comparisons and has at least three independent incidents across at least
two application shapes, including at least two decision-changing questions. Otherwise W7 records explicit deferral.

The broader debugger binding-context trajectory is a standing product requirement, not something this gate may
re-reject. The gate chooses the next bounded evidence source or binding rule only after W7 has delivered fully
qualified access and its first selected-frame/PDB/import slice.

| Dominant recurring boundary after W7 | Permitted next action |
|---|---|
| Binding-context precision | Add one evidence-backed frame/PDB/import/alias/generic rule without weakening qualified lookup |
| Stack values | Add one attributable `this`, parameter, or local slot shape only when the pinned runtime supplies exact evidence |
| Nested reference source | Plan one attributable alternate reference source; do not reconstruct missing bytes |
| Target identity | Improve one concrete target-correlation source; do not repair conflicts or invalid objects |
| Third member hop | Admit one depth-three scenario with separately bounded planning |
| Collection navigation | Admit one concrete indexed collection layout |
| Repeated zero-argument method | Admit only that scenario's complete execution dependency closure |
| Result explanation | Improve the headless explanation without adding evaluation semantics |
| No recurring useful answer | Stop feature expansion and reconsider the product thesis |

The synthetic decision advances design only. No W8 implementation is pre-approved by this plan, and later
representative observations may confirm, reverse, or stop the selected direction.

The completed W7 portfolio selected the manifest-assigned `BindingContextPrecision` label as its unique substantive
leader. Its permitted next action is the narrowly named `AddOneEvidenceBackedFramePdbImportAliasGenericRule`; this is
a successor-design input, not executable proof of a particular generic or alias rule. The separately approved
[Post-W7 Path Forward](post-w7-path-forward.md) makes the inclusive V2 product/architecture choice explicitly rather
than presenting it as a measurement directly inferred by W7.

## 12) Completion definition

W7 closes only when all of the following are proven at current state:

- the pinned ClrMD/runtime combination physically exposes the exact optimized static slot without a product value read;
- one versioned Roslyn-tree profile binds a non-ambiguous fully qualified ordinary static field from counted metadata
  even when every frame/PDB capability is unavailable;
- one bounded selected-frame/Portable-PDB producer reconstructs exact current-namespace, namespace-import, and simple-
  alias facts, and at least one `Type.Field` expression binds from each admitted contextual source;
- missing/partial/conflicting frame or PDB evidence and competing imported symbols return typed outcomes without
  suppressing a viable fully qualified binding or choosing an arbitrary candidate;
- raw static reads produce exact `Int32`, nullable, string, null, and validated object-reference results while
  distinguishing them from unavailable/non-exact storage;
- exact non-null object acquisition validates target header/module/TypeDef before issuing an object value or suffix
  plan, and no non-exact result exposes a candidate or invokes an alternate source;
- the exact optimized static expression
  `global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root.Marker` answers `0x4A44C004` through the
  existing W2 engine;
- W6 member-chain suffixes accept the same exact static object without a second parser or semantic expansion;
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

These conditions hold under the W7-specific owner disposition recorded above, so W7 is complete for its stated
synthetic milestone scope. `this`/argument/local values, generic/thread statics, extern aliases,
bare `using static` members, arbitrary address contexts, deeper navigation, collection indexing, broader calls, and
other product surfaces remain subsequent work over the W7 context and binding contracts.

## 13) Delivery discipline

Each W7 checkpoint is committed and pushed before the next begins. Commit messages record:

- the incident question and contract boundary;
- evidence ownership and canonical-identity effects;
- fully qualified and contextual name spellings exercised;
- exact, null, unavailable, partial, ambiguous, conflicting, invalid, and unsupported cases added;
- exclusions preserved;
- coarse realized LOC scale (`~100`, `~1K`, or `~10K`); and
- headless validation commands and results.

This completed plan, traceability map, product proposal, architecture overview, testing strategy, integration plan, and
repository navigation are reconciled whenever executable truth changes. Documentation does not claim an implemented
checkpoint merely because this plan names it.
