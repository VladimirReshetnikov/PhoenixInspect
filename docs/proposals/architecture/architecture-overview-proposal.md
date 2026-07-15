# Architecture Overview

**Lifecycle:** Current design direction
**Roadmap relation:** Active and supporting
**Last reset:** 2026-07-14

## 1. Product and scope lock

The funded product direction is a **deterministic, read-only expression evaluator grounded in a .NET dump**. The interpreter is enabling technology for expressions that eventually require user IL; it is not presently a general-purpose execution platform.

The proof obligations are deliberately ordered. The first three have exact-HEAD hosted closure evidence for their
revised non-cybersecurity scopes. W3's hardened implementation checkpoint is `19c292f9f`; exact documentation-closure
commit `de6cea124` passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). W4.1–W4.5a have since landed.
Exact W4.2 implementation commit `e89e43498` closes the dump-free explained-unknown arithmetic kernel; exact W4.3
implementation commit `7479b1ad4` closes the dump-free structured field-continuation seam, not the counterfactual
product. W4.4 checkpoints `2e596c117`/`742ef2c4f` close body-independent direct-MethodDef resolution and complete
frozen graph preparation. Their headless local evidence passed the strict fifteen-project Release
build, focused planner 35/35, focused fixture 6/6, complete unit 250/250, fast 73/73, ordinary dump 5/5,
optimized-context dump 1/1, and both documentation guards with zero skips under `Scope!=Cybersecurity`. W4.4 realizes
3,651 added LOC (2,076 production plus 1,575 tests), split 1,043/2,608.

Pushed W4.5a checkpoint `356c07037` now consumes that exact frozen graph through a resolver-free prepared-machine
session. It adds metadata-derived multi-frame `call`/`ret`, structural return sites, ordered frame events, configured
and required logical-depth facts, and observed logical/frame high-water reporting while preserving memory and the
legacy call-free path. Its headless non-cybersecurity evidence passed locked restore, the strict fifteen-project
Release solution build and strict unit/integration project builds at zero warnings/errors, focused prepared-graph tests
25/25, the W4 fixture 7/7, the full unit suite 275/275, fast integration 74/74, ordinary dump 5/5, optimized dump 1/1,
and both documentation guards with zero skips. Independent audit found no remaining production issue after capability-failure, depth-envelope,
high-water, budget-precedence, terminal-validation, and session-binding corrections. W4.5a realizes 3,334 LOC (1,590
production plus 1,744 tests), bringing W4.1–W4.5a to 14,013 realized LOC. W4.5b is recalibrated to 1,800–2,700 LOC;
combined W4.5 projects to 5,134–6,034 LOC, and full W4 now projects to 24,013–29,313 LOC while the original
16,860–25,310 baseline and earlier projections remain preserved:

1. recover a value from actual dump memory with explicit evidence and failure reasons;
2. parse a restricted expression, bind one typed snapshot root and field into an immutable plan, then evaluate that
   plan over dump evidence without repeating member selection;
3. execute a small, scenario-derived, EH-free IL subset through a concrete value and memory domain, checked against CoreCLR;
4. introduce provenance-bearing unknowns only when the exact slices above are trustworthy; W4.2 proves their
   branchless dump-free arithmetic transport, and W4.3 proves structured non-exact field continuation plus canonical
   precision lineage; W4.4 proves exact direct-call identity and complete rooted-acyclic graph preparation; and
   W4.5a executes exact direct calls with deterministic frames and depth accounting. Explained-unknown call/return
   lineage in W4.5b and W4.6–W4.9 remain pending.

Virtual stepping, CFG/fixpoint analysis, async and dynamic lifting, sandbox runtime hosting, live speculation, and other product surfaces are research backlog. They do not drive packages or active contracts.

## 2. Truth model

Every externally visible result names its semantic mode. A confidence or “purity” badge alone is not a semantic contract.

| Semantic mode | Meaning |
|---|---|
| `Observation` | A fact decoded directly from snapshot or artifact evidence. |
| `DerivedQuery` | A deterministic calculation over observations without executing user IL. |
| `CounterfactualExecution` | What the admitted IL would compute from recovered or explicitly assumed state under a named policy. It is not historical replay. |
| `AbstractAnalysis` | May/must reasoning over a set of possible states. This remains research. |

An evaluation result keeps these axes separate:

- **completion:** completed, blocked, budget exhausted, cancelled, or invalid;
- **completeness:** complete, partial, or none;
- **evidence:** exact, partial, unavailable, conflicting, or invalid;
- **effects:** none, virtual-only, modeled, or unsupported;
- **provenance:** the dump ranges, runtime identities, artifacts, policies, and transformations that support the value;
- **diagnostics:** stable reason codes plus actionable explanations; and
- **evidence context:** the top-level source, explicit snapshot/module identity availability, fallback outcome, and
  only deterministic bounds whose guarded operations were actually reached on this result path.

Hosts may project those axes into a compact badge, but the projection never replaces the underlying fields.
Canonical replay includes the complete evidence context and normalizes bound ordering; an unavailable identity or
no-fallback outcome is explicit rather than inferred from a missing property.

## 3. Active data flow

```text
Expression + typed host root evidence + deterministic policy
                             |
                             v
                     Parse closed grammar
                             |
                             v
Bind snapshot/root -> select field exactly once -> immutable plan + identity
                             |
                  +----------+----------+
                  |                     |
                  v                     v
          Dump evidence adapter    Artifact/metadata adapter
          (ClrMD + raw reads)      (SRM/PEReader)
                  |                     |
                  +----------+----------+
                             |
                             v
                     Evaluate(bound plan)
                             |
                             v
           Result axes + value + provenance + diagnostics

For the implemented W3 method proof, as a separate prepared-execution path:

counted dump metadata/body + exact rooted field evidence
  -> snapshot-scoped resolver + structural method/field identities
  -> exact imported-object memory snapshot
  -> metadata-derived activation + frozen typed whole-body plan
  -> interpreter kernel + concrete value/memory domain
  -> machine outcome, resulting state/memory, events, and budget
  -> host/test canonical replay projection, transcript, and fingerprint

For the implemented W4.2–W4.3 dump-free kernel extensions:

bounded partial/unavailable input facts
  -> canonical InputOrigin -> explained Int32 semantic top
  -> IlMachine with opt-in ExplainedInt32 policy
  -> shared argument/local/store/arithmetic/return transfers
  -> ordered BinaryTransform nodes with exact operands embedded

exact local receiver + structured partial/unavailable FieldLoadEvidence
  -> MemoryLoadResult.FromFieldEvidence
  -> matching frozen field + ExplainedInt32 policy + optional field-approximation capability
  -> existing ldfld transfer -> explained Int32 semantic top + unchanged memory
  -> InstructionExecuted then ValuePrecisionLost with the canonical evidence
  -> ImportedField InputOrigin -> FieldLoadTransform(receiver digest, frozen field, origin)

either lineage root
  -> reachable canonical graph capture -> prevalidated fresh-domain replay

For the implemented W4.4 dump-free preparation path:

root MethodDef + direct call operands
  -> body-independent same-module managed-IL targets + exact call signatures
  -> complete root/callee definition acquisition and typed whole-body admission
  -> deterministic rooted acyclic graph + shared-callee deduplication
  -> canonical nodes, fields, call sites, required logical depth, and fixed internal units
  -> no machine activation, call execution, or partial graph exposure
```

The dump path is not an implementation detail after the interpreter. It is the primary product path and therefore lands first.

The W4.2–W4.3 path is kernel evidence only. W4.3 adds no ClrMD producer for structured partial/unavailable fields,
model, product facade or result projection, and it consumes no generated dump. W4.4 is preparation evidence only: it
freezes a call graph but adds no frame transfer, request-depth enforcement, call model, or product/dump execution.

The active W1–W4 paths use generated, source-controlled fixtures directly. The worker described in section 4.5 is a
separately landed, non-gating prototype outside those milestones and is not part of this active data flow.

## 4. Active components

### 4.1 Dump evidence adapter

The ClrMD adapter owns dump loading, runtime/module discovery, heap layout queries, and raw target-memory reads. Reads return a count and a typed outcome; they do not collapse sparse memory, corruption, invalid addresses, and policy rejection into `false` or a default value.

Runtime module-instance evidence includes the runtime/app-domain identity, module address, image base and size, and metadata address when present. An on-disk path is only a hint to artifact acquisition and never proves that bytes came from the dump.

The generated optimized Release modeled-incident measurement keeps five predeclared context axes separate from raw
member-byte discovery. Its v1 report records raw member bytes at 5/5, attributable context at 1/5, and product-query
availability at 1/5. Only the strong-root case is attributable/queryable; this evidence does not represent
private-production incident recoverability.

### 4.2 Artifact and metadata adapter

For the active slices, `System.Reflection.Metadata` and `PEReader` are the evidence-backed metadata implementation.
The projected contract provides metadata and whole-file PE identity, bounded fixture lookup, immutable method bodies,
structural declaring types, calling-convention/receiver/parameter/return shapes, initialized local vectors, and
contextual same-module FieldDef projection. W4.4 adds contextual direct-MethodDef resolution that projects a
content-equal body-independent call signature and certifies ordinary managed IL without acquiring the target body,
RVA, locals, or local signature. Disk-backed differential tests obtain complete atomic method shapes from SRM;
Reflection invokes CoreCLR only as the result oracle.

The dump execution resolver applies the same SRM projection to exact counted metadata bytes, while the MethodDef RVA,
physical tiny/fat header, code, local-signature token, padding, and declared extra sections come from counted dump
memory. It re-parses those physical bytes, rejects disagreement, and never substitutes a disk body or signature.
Portable PDB support may use SRM when the expression slice needs names or source mapping.

AsmResolver, dnlib, Cecil, ILSpy, and Windows-PDB readers remain comparison or future adapter candidates. A new backend is introduced only when a fixture demonstrates a material gap or cost in the active SRM path.

Dump bytes and artifact bytes remain distinct evidence sources even when identity validation shows that they correspond.

### 4.3 Restricted expression front end

The first front end accepts only syntax that can be lowered to a bounded, read-only query plan. Roslyn may parse syntax, but the project owns binding, admission, lowering, and diagnostic policy. The normative language, binding, value, identity, and replay rules are consolidated in the [Restricted Dump Query v1 Contract](restricted-dump-query-contract-proposal.md).

The implemented W2 grammar is intentionally smaller than the eventual restricted-expression surface:

- one exact, ordinal host-provided root name whose typed binding is `ExactObject`, `ExhaustiveAbsence`, `Partial`,
  `Unavailable`, `Conflict`, or `Invalid`; only the exact non-null object state can produce a plan;
- one direct instance field selected with `.`; `?.` is rejected until the root model can carry exact null;
- an optional `??` literal restricted to `null`, `Int32`, or a bounded string;
- fields restricted to `Int32`, `Nullable<Int32>`, or `String`, with type-compatible coalescing decided during
  preparation; and
- result values restricted to exact `Int32`, exact null, exact string, or an explicitly partial bounded string prefix.

The parser caps expression, identifier, and decoded-literal length. Preparation verifies the binding's snapshot,
selects the exact outer field once, classifies its decoder and coalescing combination, and freezes those choices into
an object-specific plan. The evaluator caps string reads and preserves missing or partial evidence instead of treating
it as null. A selected nullable field may produce exact null and may then be coalesced; unavailable or partial evidence
never triggers a fallback. Null-conditional access, chained traversal, backing-field projection, arrays, operators
other than coalescing, and frame roots remain later scenario-driven increments.

Method calls, construction, reflection, implicit assembly loading, user-defined conversions, and unbounded enumeration are rejected in this slice. Parse, bind, admission, and evidence failures use different reason codes.

### 4.4 Read-only query evaluator

The query evaluator executes a finite project-owned plan, not synthesized user code. `Prepare` consumes the parse result
and typed root evidence, performs the only outer-field lookup, and returns either an immutable `DumpQueryPlan` or a
complete multi-axis failure result. `Evaluate(session, plan)` validates the plan's snapshot/owner/field relationship
and reads through the already selected descriptor; it never repeats outer member lookup. The convenience evaluation
entry point is composition over these stages rather than a separate semantic path.

The one-hop grammar is structurally bounded and subject to deterministic expression, identifier, literal,
handle-scan, field-catalog, and string-read caps; each evidence read produces either a value or a typed
partial/unavailable outcome. Result context records a cap only when its guarded operation was reached, so a
root-name mismatch, a missing field, and a foreign-snapshot root report different applied-bound sets. A retained
partial primitive-field wrapper remains explanatory evidence with no decoded scalar answer; generic projection does
not overstate completeness. It has no filesystem, network, process, native, or target-mutation capability.

Each admitted plan has a canonical v1 projection and SHA-256 identity that includes the grammar version, exact root
and field names, snapshot/owner identity, the complete selected field descriptor (including nullable child layout),
decoder kind, and exact optional literal. Successfully parsed requests have canonical request identity; bounded invalid
input retains a canonical raw-input identity, while deliberately oversized input is rejected before raw identity is
retained. Exact root-selection policy provenance independently preserves the ordinal selector, disposition, issue,
scan counters, caps, retained-match count, and match-limit state. Failures before plan creation and successful values
whose unused fallbacks differ therefore remain distinguishable. Results from successful plan evaluation carry the plan
identity in ordered provenance, and all product results are `DerivedQuery`; adapter reads beneath them remain
`Observation` results. The versioned corpus has 22 cases spanning 20 distinct expression texts and covers exact, null,
fallback, typed-root, binding, syntax, type, and partial-string outcomes. Every result and every successfully prepared
plan projection/fingerprint is identical when repeated within one session and when the dump is reopened, its root
rediscovered, and the query rebound. This implementation and corpus pass locally and at exact W2 closure commit
`5bed47100` in [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).

### 4.5 Non-gating one-shot external-artifact prototype

Two separately landed Windows x64 projects provide a trusted broker/protocol boundary and a one-request runner. They
have a dedicated headless test project; its four-test package, including a real malformed-artifact process checkpoint,
passed locally at `9fcf00934`. The projects remain useful topology and process-boundary experiments, but cybersecurity
work is outside W1–W4; this prototype is not a completion requirement for those milestones or an admitted external
product surface. Its test project is not invoked by the current milestone workflow, and the five hostile-corpus facts
in the integration assembly are tagged `Scope=Cybersecurity` and excluded from all current test commands.

### 4.6 Interpreter kernel

W3 hardened implementation commit `19c292f9f` closes two deliberately small profiles. E1 is static, branchless,
EH-free `Int32` arithmetic over metadata-projected parameters and initialized locals: `nop`, integer constants,
compact/short/long argument and local encodings, `add`, `sub`, `mul`, and `ret`. E2 is one exact instance `Int32`
getter, either direct or with one constant `add`/`sub`/`mul` adjustment, containing exactly one `ldfld`. Its receiver
load must use the one-byte compact `ldarg.0`; equivalent short `ldarg.s 0` and long `ldarg 0` encodings are deliberate
negative admission cases rather than E2 coverage.

`IResolutionServices` returns an atomic `ResolvedMethodDefinition`, contextual `ResolvedField`, and body-independent
contextual `ResolvedMethodCallTarget` descriptors.
`ActivateRoot` accepts only the method handle, receiver/arguments, and memory; it derives argument slots, local
defaults, and return disposition from metadata. Whole-body admission decodes and type-checks every instruction once,
freezes its typed boundaries and field descriptor, and rejects the entire body before instruction zero if any suffix
is unsupported or malformed.

The machine receives both `IValueDomain<TValue>` and `IMemoryModel<TValue,TMemory>`. A successful `ldfld` performs
exactly one typed memory load and threads equivalent persistent memory. Concrete memory distinguishes newly allocated
CLI defaults from dump-imported objects: an unimported field is unavailable rather than zero. Partial, unavailable,
conflicting, or invalid evidence performs no transfer. Exact typed null produces one budgeted/evented, terminal and
idempotent `TargetException` state; W3 performs no handler search or continuation.

Dump-free fixtures compare E1/E2 results, overflow, and null behavior with CoreCLR. The real-dump fixture derives the
snapshot-scoped module, method shape/body, exact receiver type, admitted `ldfld` token, and four-byte field value from
counted dump evidence; it reopens and rebinds the dump to reproduce identities, outcome, events, budget, memory, and
canonical transcript. The independently opened disk PE remains a late comparison oracle. These implementation facts
and all required non-cybersecurity lanes pass locally at `19c292f9f`; [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four jobs.
Exact documentation-closure commit `de6cea124` then passed all four required jobs in [closure run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), formally closing W3's defined
non-cybersecurity scope.

W4.2 exact implementation commit `e89e43498` extends the same machine rather than creating a parallel interpreter.
`IValuePrecisionDomain<TValue>` optionally reports `Exact`, `ExplainedUnknown`, or `UnexplainedUnknown`.
`UnknownExecutionPolicy` defaults to `ExactOnly`, preserving W3 behavior; `ExplainedInt32` opts into only structurally
typed, domain-validated explained `Int32` values. Bottom and bare semantic top remain non-executable. Exact receivers,
metadata-initialized locals, and exact-classified W3 field loads remain exact, while the existing argument/local load,
local store, arithmetic, and return handlers transport an admitted explained unknown.

`ProvenanceConcreteDomain` and `ProvenanceConcreteValue` keep the lifted-flat semantic value separate from its optional
lineage root. Equality, hashing, lattice order, join, meet, and widening ignore lineage. Runtime unknown arithmetic
creates canonical `BinaryTransform` nodes from ordered operands only when every unknown operand has a validated
explanation; exact `Int32` operands are embedded without spurious nodes. Canonical `InputOrigin` nodes ground partial
or unavailable inputs. Reachable-only graph capture and validated fresh-domain replay preserve versioned bytes,
content-derived node IDs, graph root, and graph SHA-256.

W4.3 exact implementation commit `7479b1ad4` extends the existing `ldfld` transfer. Canonical
`FieldLoadEvidence` v1 retains one frozen-plan dependency ordinal, the complete ordinary-instance `Int32` field,
partial/unavailable status and reason, source and imported-object SHA-256 identities, nonzero address, exact four-byte
request, and defensively copied observed prefix. `MemoryLoadResult<TValue>.FromFieldEvidence` is its structured union
producer. Approximation requires matching evidence, `UnknownExecutionPolicy.ExplainedInt32`, and an
`IFieldLoadApproximationDomain<TValue>` implementation; exact loads remain exact, while code-only non-exact results,
missing policy/capability, conflict, invalidity, or mismatched evidence cannot produce a value.

A successful approximate load consumes the instruction, preserves memory, and emits `InstructionExecuted` followed
by `ValuePrecisionLost` at the same method/offset with the evidence attached. `ProvenanceConcreteDomain` atomically
creates an `ImportedField` `InputOrigin` whose source key is the evidence digest and a `FieldLoadTransform` containing
the imported-receiver digest, complete frozen field, and origin predecessor. `FieldLoadTransform` is append-only kind
3 under the existing schema; W4.2's hard-coded input/binary bytes and IDs remain unchanged. Capture is reachable-only,
and replay validates canonical bytes, IDs, ordering, reachability, dependencies, and the field-origin relationship
before mutating a destination domain.

The ClrMD execution descriptor remains exact-only: W4.3 proves the injected memory/domain/machine boundary without a
dump producer.

W4.4a exact checkpoint `2e596c117` adds `MethodCallSignatureShape` and `ResolvedMethodCallTarget`. Resolution accepts
only a non-nil same-module direct MethodDef whose implementation is ordinary managed IL; the call signature carries
declaring TypeDef, calling convention, receiver facts, generic arity, ordered explicit parameters, and return type but
no body/local facts. MemberRef/MethodSpec substitution, virtual or indirect dispatch, cross-module targets, generics,
varargs, and non-`static Int32(Int32,Int32)` helper signatures are outside the admitted profile.

W4.4b exact checkpoint `742ef2c4f` adds `MethodGraphPlanner.Prepare`. A successful `FrozenMethodGraphPlan` owns every
reachable complete definition and typed admission, canonical structural node/field/call-site vectors, one node per
shared MethodDef, the longest root-to-method logical depth, and distinct-method/field/edge internal units. Discovery is
root-first and call-site ordered for deterministic failures; successful projections are canonically ordered. Cycles,
identity/signature disagreement, malformed/unsupported tokens, and cap exhaustion expose no partial plan; conflicts
remain conflicts. The fixed 64-method/1,024-unit caps are internal safety limits rather than W4.8's configurable
traversal budget.

The exact fixture freezes two nodes, two fields, one edge at caller IL offset 12, required logical depth two, and five
internal units. The legacy `IlMachine` continues using its call-free admission path and still rejects before the call.

W4.5a checkpoint `356c07037` adds a mutually exclusive prepared-graph machine session. `ActivatePreparedGraph`
binds the immutable graph plus one maximum logical depth without resolving again; `CreatePreparedOperationalState`
retains configured and required depth alongside instruction budget and both high-water facts. Exact interpreted calls
advance and freeze the caller continuation in `FrameReturnSite`, push a metadata-derived callee frame, and emit
`InstructionExecuted` then `FramePushed`. Nested `ret` restores that exact continuation, propagates the typed result,
and emits `InstructionExecuted` then `FramePopped`. Instruction availability precedes invariant/capability work,
depth mismatch and forged return state fail atomically, memory remains persistent, and terminal states retain exact
root-result or target-boundary evidence.

The emitted `GetMarkerSummary` fixture now executes ten exact instructions through `CombineMarkers`, performs exactly
two field loads, reaches logical and active-frame high water two, agrees with CoreCLR, and makes no resolution call
after graph preparation. Explained-unknown arguments and helper results still stop at the explicit
`EXEC_CALL_LINEAGE_UNAVAILABLE` seam: canonical `CallArgumentTransform`/`InterpretedReturnTransform` lineage is
W4.5b. Call models, the counterfactual facade/request/plan/result, dump-grounded execution, and hosted umbrella closure
remain W4.6–W4.9 work.

## 5. Identity model

Identity and location are separate concepts:

- a **dump metadata-root identity** is the MVID plus exact metadata length and SHA-256 decoded from one counted metadata image;
- a **complete artifact identity** is exact whole-file length plus SHA-256; a disk-backed module/method identity carries this in addition to its metadata-root identity and optional PE layout evidence;
- a **runtime module instance** identifies one loaded instance in one dump/runtime/app-domain;
- an **execution module handle** is derived either from complete disk-artifact identity or, for dump execution, from
  the exact counted metadata identity plus stable snapshot/runtime-module evidence; equal display names or addresses
  cannot alias different evidence;
- a **method definition** is that structural module handle plus a non-nil MethodDef token and one atomically resolved
  body/signature/local snapshot;
- a **metadata-defined receiver type** is its structural module handle plus a non-nil TypeDef token; display names are
  diagnostic only;
- a **resolved field** is its structural module/FieldDef handle plus exact declaring type, field type, and
  static/literal/RVA facts; the raw IL token is not a memory-model identity;
- an **imported object** retains a bounded, domain-separated owner identity derived from runtime-module source
  identity, owner address, and method table; the surrounding evidence descriptor and imported memory separately
  retain the structural owner type, correlated field descriptor, and exact observed value;
- a **constructed method/type context** adds a deterministic generic-instantiation identity;
- a **bound dump-query plan** adds the grammar version, ordinal root/field names, snapshot-scoped owner and selected
  field identities, admitted decoder kind, and exact optional literal value;
- a **provenance source key** is the complete SHA-256 of a bounded canonical request/evidence source projection;
- a **field-load evidence identity** is the SHA-256 of canonical `FieldLoadEvidence` v1, including the frozen field,
  evidence/source/imported-object axes, and read geometry but excluding display and process-local identity;
- a **lineage node** is the SHA-256 of versioned canonical `InputOrigin`, ordered `BinaryTransform`, or
  `FieldLoadTransform` bytes, independent of interning and traversal order; the field transform retains the imported
  receiver digest, frozen field, and origin predecessor rather than a raw address or local reference number;
- a **captured lineage graph** is the root plus exactly its reachable nodes in deterministic ID order, with its own
  canonical bytes and SHA-256 fingerprint;
- file paths, display names, enumeration order, process-random string hashes, and allocation counters are not stable identities.

Mappings between runtime instances and artifacts carry evidence status and mismatch diagnostics. Handles used in replay or tests must be derived from these identities, not from discovery order.

## 6. Evidence-result contract

Any boundary where missing or conflicting data is expected returns an explicit outcome equivalent to:

```text
EvidenceResult<T> = {
  Status: Exact | Partial | Unavailable | Conflict | Invalid,
  Value?: T,
  ReasonCode?,
  Provenance: ordered evidence references,
  Diagnostics: ordered diagnostics
}
```

Specialized zero-allocation outcomes are appropriate for hot paths such as `Read(address, Span<byte>)`, but they preserve the same distinctions and report the exact byte count. Exceptions are reserved for caller contract violations or internal defects, not ordinary dump sparsity.

The implemented W4.3 specialization is `MemoryLoadResult<TValue>.FromFieldEvidence`. Only a structured partial or
unavailable ordinary-instance `Int32` result carries canonical `FieldLoadEvidence`; older code-only results remain
valid non-continuing outcomes and cannot imply source, imported-object, or read-geometry facts they do not possess.

## 7. Execution state and determinism

Semantic state and operational bookkeeping are separate.

```text
SemanticState
  frames, instruction offsets, arguments, locals, evaluation stacks,
  semantic memory, path facts, target-exception state

OperationalContext
  remaining budgets, cancellation, trace cursor, traversal worklists,
  provenance-ID allocation, caches, metrics, replay bookkeeping
```

Only semantic state participates in abstract-domain equality, joins, or widening. Decreasing budgets, growing traces, object allocation order, cache state, and provenance IDs must not prevent a fixpoint or make semantic equality traversal-dependent.

W4.2 applies the same separation inside a value: `ProvenanceConcreteValue` equality and hashing inspect only its
lifted-flat semantic value. Its optional lineage root belongs to the explanation/replay channel. Because lineage IDs
are hashes of canonical node bytes, capture/replay remains deterministic without allowing explanation differences to
split a semantic lattice top.

Memory used for undo or branch snapshots must expose a documented persistent-snapshot contract. `TMemory` being a generic type is not by itself a persistence guarantee; mutable implementations are not eligible for rewind claims.

Determinism means identical normalized inputs, policies, artifacts, dump evidence, and engine version produce identical values, statuses, ordered diagnostics, and transcript fingerprints. Wall-clock timeout and host cancellation are operational interruptions and are not interchangeable with deterministic budget exhaustion.

The W1 replay proof crosses session lifetime rather than comparing two evaluations over shared adapter state: it closes
and reopens the same dump, rediscovers the module and selected root, and requires byte-identical complete canonical
results plus the same SHA-256 fingerprint.

The W2 proof applies that stronger test to every case in its versioned corpus, not only to one representative success.
For each of 22 cases spanning 20 distinct expression texts it repeats the pipeline in one session; the 13 cases whose
preparation succeeds proceed to bound-plan evaluation. It compares the complete canonical result byte sequence and
result SHA-256 plus those 13 plans' canonical projection strings and plan SHA-256 values, then closes and reopens the
dump, reconstructs typed root bindings, and reproduces those artifacts. Input, request, root-selection, plan, and
result identities are versioned and content-derived rather than enumeration- or allocation-derived. [GitHub Actions
run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required
jobs at exact W2 closure commit `5bed47100`.

The W3 implementation proof repeats compiler-derived E1/E2 executions through one frozen machine and through freshly
opened SRM modules. Its dump E2 proof additionally closes and reopens the dump, rebinds the runtime module and rooted
object, reprojects the method/field from counted evidence, reimports the exact field cell, and reproduces structural
identities plus the canonical execution transcript. This passes locally at hardened implementation commit `19c292f9f`, whose
four hosted jobs also passed in [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). Exact documentation-closure
commit `de6cea124` and [run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237)
close that evidence chain with all four required jobs passing.

The W4.2 dump-free proof independently freezes canonical bytes and hard-coded IDs for `InputOrigin` and
`BinaryTransform`, checks insertion-order-independent interning and reachable-only capture, then replays the graph in
a fresh domain while preserving root and graph fingerprints. It does not claim dump-session or product-result replay.

The W4.3 dump-free proof freezes canonical `FieldLoadEvidence`, `ImportedField` origin, `FieldLoadTransform`, and graph
bytes/IDs; asserts the W4.2 golden identities remain unchanged; covers atomic equal-node interning and rejection; and
prevalidates graph representation and field-origin relationships before fresh-domain replay. Fresh machines reproduce
the approximate load, ordered events, lineage, and subsequent arithmetic. This still does not claim that ClrMD
produces the evidence or that a dump-session/product result replays.

The W4.4 dump-free proof constructs the same content-equal target signatures and frozen graph from fresh resolver
instances, canonicalizes node/field/edge order independently of discovery storage, and validates the exact fixture
topology plus diamond sharing, cycles, conflicts, malformed targets, and fixed-cap failures. Preparation returns either
the complete graph or no plan. This is deterministic dependency preparation, not a canonical product replay result.

## 8. Status protocols

Different layers use different, explicitly mapped vocabularies:

- machine execution status: `Ready`, `Completed`, `BudgetExhausted`, `Blocked`, `InvalidProgram`, `TargetException`;
- machine debug-event kind: successful W4.3 approximation emits `InstructionExecuted` followed by
  `ValuePrecisionLost`; this event vocabulary is not a host pause reason or evidence status;
- future virtual-session pause reason: `StepComplete`, `DecisionNeeded`, `ExceptionStop`, `BudgetStop`, `Cancelled`, `Completed`;
- adapter traversal: evidence status plus a miss-reason code;
- async/task lifecycle: diagnostic events, not stop reasons.

Machine `BudgetExhausted` maps to session `BudgetStop`; `Ready` may map to `StepComplete` only when the session
controller's requested boundary is reached; `Completed` maps to session `Completed`; and `TargetException` may map to
session `ExceptionStop`. `Blocked` and `InvalidProgram` become diagnostics or terminal host results, not
target-program `ExceptionStop` values. Internal invalidity never masquerades as a target exception.

## 9. Exception-handling admission

The W3 interpreted-method slice rejects bodies with exception regions and bodies that require exception transfer. Its
one admitted exceptional boundary is exact typed-null `ldfld`, which produces structured `NullReference` information
and an empty-stack terminal latch. Handler search/unwind, filters, `fault`, `finally`, and cross-frame propagation are
a later milestone and are prerequisites for debugger-grade Step Out and async `MoveNext` claims.

## 10. External-input scope boundary

W1–W4 are restricted to generated, source-controlled fixture artifacts and non-security evidence behavior. Their
deterministic read, identity, context, provenance, replay, and resource-bound contracts remain active. External-input
cybersecurity is explicitly outside those milestones, and their completion does not create an external artifact product
surface. The already-landed malformed corpus and one-shot worker are retained only as non-gating prototypes; any future
external-input initiative must establish its own scope and evidence independently. Restore/build intentionally remains
repository-wide across all 15 projects, including the worker projects and integration assembly, as topology and
compilation-health evidence only. It is not cybersecurity behavioral evidence.

## 11. Physical topology

The prototype retains only projects containing behavior or contracts exercised by the active slices:

- `Interpreter.Core.Abstractions` and `Interpreter.Core.Execution` — backend-neutral type/body/call-target shapes,
  domain/memory contracts, optional value-precision and field-approximation capabilities, canonical structured field
  evidence, exact-only-by-default unknown policy, precision events, complete direct-call graph preparation, and the
  interpreter kernel;
- `Interpreter.Domain.Concrete` — concrete validation semantics and persistent memory plus W4.2–W4.3's
  provenance-aware value/domain and canonical input, binary, and field lineage graph;
- `Interpreter.Metadata.Abstractions` and `Interpreter.Metadata.SRM` — projected metadata contracts, including
  body-independent contextual direct-MethodDef resolution, and the active SRM adapter;
- `Interpreter.Host.Abstractions` and `Interpreter.Host.Dump.ClrMD` — typed dump evidence, ClrMD adapter, and exact
  counted W3 method/field composition into a snapshot-scoped resolver/import descriptor without introducing ClrMD
  into core execution;
- `Interpreter.Product.DumpQuery` — the bounded W2 parser, typed root binding, immutable prepared plan, read-only
  `Evaluate(plan)` path, canonical identities, and closed result-value projection;
- `Interpreter.Host.ExternalWorker` and `Interpreter.Host.ExternalWorker.Runner` — the narrow Windows broker/protocol
  and one-request AppContainer executable; they are not a generic hosting framework.

Logical seams may be documented without creating assemblies. A new project is justified only when it contains implementation, has an independently useful dependency boundary, and at least one test exercises that boundary. Empty product/model/backend projects are not placeholders.

## 12. Research entry gates

No research subsystem enters the active roadmap merely because a proposal exists.

- **Hybrid/abstract domains:** two domains execute the same meaningful opcode corpus and domain laws are tested.
- **Virtual stepping:** W4 must first validate method execution, deterministic instruction/frame/model events, and
  stop-on-throw. A later stepping increment must separately define pause semantics and source mapping; debugger-grade
  Step Out additionally requires handler-transfer EH.
- **Async/dynamic lifting:** ordinary compiler-generated prerequisite IL and EH are supported, with scenario fixtures.
- **Alternative products:** a second product demonstrates reuse without weakening the dump evaluator’s correctness and security model.
- **Alternative metadata backend:** a corpus records an SRM deficiency and an adapter test demonstrates a better trade-off.

The active sequence, sizing assumptions, and exit tests live in `docs/plans/future-work-planning.md`.
