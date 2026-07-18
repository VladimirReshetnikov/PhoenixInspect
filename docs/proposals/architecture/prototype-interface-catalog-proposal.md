# Prototype Contract Inventory

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Stability:** Draft and reversible

## Purpose

This inventory records the small public contract surface exercised by the current dump-evidence, restricted-query,
W3 concrete-IL, W4.2–W4.7 dump-free explained-unknown, graph-preparation, interpreted-call, pure-model, target-outcome,
and closed W5/W6/W7 product proofs. It is descriptive, not a promise of compatibility. W7's implemented static-
expression, binding-context, symbol, storage, and value contracts are recorded below. W8's approved V2 contracts are
not implemented and therefore remain outside this implementation inventory.
Strengthened W3 checkpoint `19c292f9f`
passed the required local milestone-selected lanes and all four jobs in [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally closed at exact
documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit. W4.1–W4.5 have since landed. Exact W4.2 implementation commit `e89e43498` remains the
explained-arithmetic checkpoint; exact W4.3 implementation commit `7479b1ad4` is the structured field-continuation
checkpoint; and W4.4 checkpoints `2e596c117`/`742ef2c4f` are the current direct-MethodDef graph-preparation checkpoint.
Its headless local evidence passed the strict fifteen-project Release build, focused planner 35/35, focused fixture
6/6, complete unit 250/250, fast 73/73, ordinary dump 5/5, optimized-context dump 1/1, and both documentation guards
with zero skips within the milestone test selection. W4.4 realizes 3,651 added LOC (2,076 production plus 1,575 tests), split
into 1,043-LOC W4.4a and 2,608-LOC W4.4b slices.

Pushed W4.5a checkpoint `356c07037` is the exact prepared-graph execution checkpoint. It passed locked restore, the
strict fifteen-project Release solution build and strict unit/integration project builds at zero warnings/errors,
focused 25/25, W4 fixture 7/7, complete unit 275/275, fast 74/74, ordinary dump 5/5, optimized dump 1/1, and both
documentation guards with zero skips;
every behavioral command was headless and used the milestone test selection. Independent audit closed with no remaining
production finding. W4.5a realizes 3,334 LOC (1,590 production plus 1,744 tests).

Pushed W4.5b checkpoint `c72f6ee9e` completes the interpreted-call contract below. Exact-commit evidence passed locked
restore, the strict fifteen-project Release build at zero warnings/errors, prepared graph 40/40, combined
lineage/audit 76/76 including 29 legacy identity cases, compiler lineage 2/2, W4 integration 9/9, unit 297/297, fast
76/76, ordinary dump 5/5, and optimized dump 1/1, with zero skips and the milestone test selection on behavioral filters.
Independent audit found no remaining finding. W4.5b realizes 2,804 LOC (766 production plus 2,038 tests), bringing
combined W4.5 to 6,138 realized LOC and cumulative W4 realization to 16,817 LOC. The historical 1,800–2,700 W4.5b
estimate and 5,134–6,034 combined projection were each exceeded at the upper bound by 104 LOC.

Exact W4.6a implementation checkpoint `77c92789b16d9258c907d5026a36e39f8c957b41` adds the structural pure-model
contracts and explicit body-free planner profile described below. It passed locked restore; the strict fifteen-project
Release build at zero warnings/errors; complete unit 371/371; fast integration 77/77; ordinary dump 5/5; optimized
dump 1/1; focused pure-model contracts 49/49; model planner 25/25; legacy planner 35/35; SRM compiler 1/1; lineage
2/2; both guards; and zero skips, with only the milestone-selected behavioral tests. Independent audits found no
behavioral finding. W4.6a realizes 2,959 added LOC (1,210 production plus 1,749 tests/fixture support), 359 LOC above
its historical 1,800–2,600 upper estimate, and brings W4.1–W4.6a realization to 19,776 LOC.

Exact W4.6b implementation checkpoint `fd723a912` adds the optional modeled-return lineage capability and the
append-only schema-v1 kind-6 `ModeledReturnTransform` described below; it still adds no model execution. Strict
headless builds passed at zero warnings/errors, focused modeled-lineage tests passed 8/8, and combined legacy plus
modeled lineage passed 44/44 with zero skips and the milestone test selection. W4.6b realizes 1,003 added LOC (481
production plus 522 tests), with 23 deletions, bringing W4.1–W4.6b realization to 20,779 LOC.

Exact W4.6c implementation checkpoint `877c9fb55` executes only the capability retained by the frozen modeled leaf.
It adds atomic exact/grounded-unknown caller transfer, immutable attempt chronology, separate invocation/completion
counters, logical-depth and active-frame witnesses, and stable capability/outcome/lineage/invariant failure taxonomy.
There is no registry/resolver/descriptor/body reread, fallback, callee frame, frame event, or memory mutation. W4.6c
realizes 2,734 added LOC (1,425 production plus 1,309 tests); strict affected builds passed at zero warnings/errors
and its focused headless lane passed 34/34 with the milestone test selection.

Exact W4.6d checkpoint `da5346813` adds 956 test LOC and proves compiler/SRM interpreted/model/CoreCLR exact
agreement plus interpreted/model agreement for both partial/unavailable shapes. Its mixed case freezes one canonical
graph, while its both-unknown case repeats across same and fresh sessions. The focused lane
passed 3/3, aggregate W4 integration 13/13, and Fast 80/80, all through the headless wrapper with
the milestone test selection. W4.6 totals 7,652 LOC and brings W4.1–W4.6 to 24,469 LOC. The target PE SHA-256 remains
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; the mixed and both-unknown graph hashes are
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff` and
`31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`.

Exact W4.7a checkpoint `2e70fe76d` adds `Interpreter.Product.DumpDebugging` and the contracts described below.
Projection accepts only a complete exact transition sequence issued by the same machine from legacy IL-zero activation
through the first exact null-reference latch, optionally plus one certified idempotent re-step. Exact W4.7b checkpoint
`dad6a6dd4` proves direct/adjusted compiler getters, fresh SRM/module/domain/machine replay, and poison/count evidence
that re-step performs no capability call or repeated load. W4.7a/b realize 2,448/353 LOC, 2,801 total, bringing W4 to
27,270 LOC. Closure passed strict sixteen-project build 0/0, unit 430/430, Fast 80/80, dumps 5/5 and 1/1, focused
15/15 plus 2/2 (17/17 combined), compiler differential 23/23, and both guards, headlessly with zero skips and
the milestone test selection. The literal standalone fragment SHA-256 is
`a9b98e46583dcf90ac108571c126d8d86cec0465c595e2689fae767e33ff108e`.

The original full-W4 baseline 16,860–25,310 and checkpoint projections 18,532–26,132, 19,228–25,728,
21,179–26,779, 24,013–29,313, 25,017–29,417, 27,217–32,117, 28,376–32,476, 28,876–33,276,
28,826–33,726, 28,879–33,279, and pre-W4.6c/d closure 30,079–33,729 LOC remain historical calibration. W4.6c/d
realized 3,690 LOC against their 3,400–3,750 estimate. The former W4.7 estimate and 31,069–34,319 projection are now
historical. The final pre-implementation plan left W4.8 at 2,400–3,500 LOC and W4.9 at 2,000–3,200 LOC, yielding a
31,670–33,970 LOC full-W4 projection. That projection is now historical: W4.8 realizes 11,924 LOC, W4.9 2,698
LOC, and full W4 41,892 LOC. A contract is added only with an executable consumer and is removed when it
gets ahead of code.

## Active contracts

### Core semantics

`Interpreter.Core.Abstractions` contains:

- structural, path-independent `ModuleHandle`, `MethodHandle`, `FieldHandle`, and metadata-defined `TypeSig`
  identities. Disk handles derive from complete-artifact content; dump handles derive from counted metadata plus
  snapshot/runtime-module evidence;
- immutable `ResolvedMethodDefinition`, `MethodSignatureShape`, and `ResolvedField` projections that freeze the body,
  declaring type, receiver/parameter/return/local types, calling convention, and field storage facts;
- content-equal body-independent `MethodCallSignatureShape` and `ResolvedMethodCallTarget`, which freeze an exact
  same-module managed-IL MethodDef, declaring TypeDef, calling convention, receiver facts, generic arity, ordered
  explicit parameters, and return type without acquiring a method body, RVA, or locals;
- `DirectCallSiteIdentity`, which content-equally freezes the exact caller MethodDef, direct-call IL offset, and exact
  same-module callee MethodDef used by prepared frame state and canonical call/return lineage;
- `PureCallModelVersion` and `PureCallModelIdentity`, which freeze three bounded numeric components and a bounded
  canonical lowercase ASCII identifier; `PureCallModelDescriptor`, which binds that identity to one exact body-
  independent target, declared confidence, and normalized `None`/`Unsupported` effects; and bounded `W4.Model.*`
  stable-code validation for payload-omitting model failures;
- the deliberately non-generic `PureCallModelInvocation`, `PureCallModelArgument`, and `PureCallModelOutcome`
  vocabulary: exactly two metadata-ordered structural `Int32` atoms, exact or explained unknown without lineage,
  ambient context, memory, session, dump, target delegate, or display text; exact/unknown returns or stable-code
  blocked/invalid outcomes; plus `IPureCallModel`, `IPureCallModelRegistry`, and structural selection results;
- `IValueDomain<TValue>`, including default-value construction, exact type/stack-kind inspection, concrete constants,
  arithmetic, and executable order/meet/join/widen operations;
- optional `IValuePrecisionDomain<TValue>` and `ValuePrecisionKind`, which classify an executable value as `Exact`,
  `ExplainedUnknown`, or `UnexplainedUnknown` without making explanation identity part of the minimum value-domain
  contract;
- optional `IFieldLoadApproximationDomain<TValue>`, which extends the precision capability only for the admitted
  structured ordinary-instance `Int32` field-load continuation;
- optional `IInterpretedCallLineageDomain<TValue>`, which extends the precision capability with an atomic complete-
  vector argument transform and one interpreted-return transform for the admitted direct-call boundary while exact
  values pass unchanged;
- optional `IPureCallModelLineageDomain<TValue>`, which atomically grounds one modeled explained-unknown return in
  the complete original two-argument call relation. Exact operands are embedded in one modeled-return node; unknown
  operands receive unchanged parameter-indexed kind-4 call nodes in the same batch;
- `IMemoryModel<TValue,TMemory>` constrained by `IPersistentMemoryState<TSelf>`;
- canonical `FieldLoadEvidence` v1, which retains the dependency ordinal, complete frozen ordinary-instance `Int32`
  field, partial/unavailable status, bounded reason, source and imported-object SHA-256 identities, nonzero address,
  exact four-byte request, and defensively copied observed prefix;
- `MemoryLoadResult<TValue>`, which distinguishes exact values, partial/unavailable/conflicting/invalid evidence, and
  structured target exceptions; `FromFieldEvidence` is the exclusive structured partial/unavailable producer while
  existing code-only outcomes remain compatible;
- `IResolutionServices.GetMethodDefinition` plus contextual `ResolveField` and `ResolveMethod`. The call resolver
  accepts only an exact direct MethodDef in the context module and certifies ordinary managed IL before any body is
  acquired; MemberRef/MethodSpec substitution remains outside the contract; and
- immutable `TypeSig`/`MethodBody`, budget, operation, and stack-category shapes used by those contracts.

`Interpreter.Core.Execution` contains:

- semantic `MachineState` and separate `MachineOperationalState` instruction budget plus configured/required logical
  depth and observed logical/active-frame high-water bookkeeping;
- metadata-derived legacy `ActivateRoot` and opt-in `ActivatePreparedGraph`, `CreatePreparedOperationalState`,
  root/callee frame state, optional root return values, terminal target-exception state, and a semantic comparer that
  excludes operational history while including structural return-site identity;
- `FrameReturnSite`, carried only by non-root interpreted frames, which retains the exact direct call and already
  decoded caller continuation boundary;
- whole-body `MethodAdmissionResult`, structured `ExecutionFailure`, and low-level `MachineRunStatus`;
- `MethodGraphPlanner.Prepare`, which returns either a complete immutable `FrozenMethodGraphPlan` or a status/failure
  with no partial plan. A successful graph owns canonical `FrozenMethodGraphNode` and `FrozenMethodCallSite` vectors,
  complete definitions/admission, distinct fields, direct edges, required logical depth, and fixed internal traversal
  use; shared MethodDefs are represented once and cycles are rejected;
- explicit `MethodGraphPlanner.RequirePureModel`, which selects only one exact, no-effect, body-independent target
  after call resolution/typing and before target-body acquisition. Successful modeled edges expose disposition
  `PureModel`, one canonical body-free `FrozenPureModelLeaf`, `ModeledLeaves`, and `TryGetModeledLeaf`; missing,
  throwing, invalid, mismatched, non-exact, or unsupported-effect selection cannot fall back to the target body;
- W4.6c prepared modeled-call execution, which invokes only the capability retained by the frozen leaf, transfers an
  exact or lineage-grounded unknown return atomically, records immutable attempts/counters/depth witnesses, and has no
  target-body fallback or registry/resolver/descriptor reread;
- `IlMachine.StepOne`, whose current closed set is `nop`, integer constants, argument/local loads, local stores,
  `add`, `sub`, `mul`, one ordinary-instance `Int32` `ldfld`, direct `call` only in a bound prepared graph, and `ret`;
- `UnknownExecutionPolicy`, whose default `ExactOnly` value preserves the W3 execution boundary and whose opt-in
  `ExplainedInt32` value permits only structurally typed, domain-validated explained `Int32` values; and
- `DebugEventKind.ValuePrecisionLost`, whose successful field-load occurrence follows that instruction's
  `InstructionExecuted` event at the same method and IL offset and carries its canonical `FieldLoadEvidence`; and
- `DebugEventKind.FramePushed` plus the retained `FramePopped` event, emitted after the corresponding successful
  exact call/return instruction and identifying the actual callee entry or helper return boundary.
- `StepOutcome.IsMachineIssuedTransitionFrom`, which certifies that the exact machine issued an outcome from the exact
  predecessor state and operational-state references. W4.7 consumes this authority during projection; certification
  data is deliberately absent from semantic records and canonical fragment equality.

Legacy activation receives only a method handle, receiver/arguments, and persistent memory. It atomically resolves the
body and metadata shape, validates supplied structural types and execution precision under the selected policy,
constructs exact initialized local defaults, fixes the empty entry stack, and exposes no state on failure. Prepared
activation instead receives the already complete `FrozenMethodGraphPlan`, one maximum logical depth, root arguments,
and memory; it binds that immutable session without resolver use and rejects insufficient depth before creating state.
The two activation modes are mutually exclusive on one bounded machine. Whole-body admission
decodes every instruction once into a frozen typed plan; it rejects unsupported/malformed suffixes, invalid slot or
stack shapes, non-boundary resumed states, nested frames, and exception regions before budget consumption, state
mutation, memory calls, or instruction events. Bare top and any unknown without a validated domain-owned lineage root
remain non-executable under both policies.

`IlMachine<TValue,TMemory>` now receives `IMemoryModel<TValue,TMemory>`. The closed E2 profile admits only an exact
instance `Int32` getter, direct or with one constant arithmetic adjustment, and freezes exactly one contextual
same-module FieldDef before execution. Successful `ldfld` performs exactly one typed load and preserves memory.
W4.3 leaves that exact path unchanged. A partial or unavailable result continues only when it carries matching
`FieldLoadEvidence`, the machine policy is `ExplainedInt32`, and the value domain implements
`IFieldLoadApproximationDomain<TValue>`; the completed read-only transfer consumes the instruction, pushes one
explained `Int32` unknown, emits `InstructionExecuted` followed by `ValuePrecisionLost`, and preserves memory.
Code-only partial/unavailable results and missing policy/capability remain non-continuing compatibility outcomes;
conflict remains blocked, invalid or mismatched structured evidence remains invalid, and no branch fabricates a
scalar. Typed null consumes one instruction unit, emits one `TargetExceptionRaised` event, and produces an idempotent
terminal `TargetException` state without handler search.

The old caller-shaped body-only activation is gone. Argument count, local vector/defaults, implicit receiver, and
return disposition are metadata-derived facts. The admitted plan records exact entry-stack type vectors, not only
depth or CLI stack categories. W4.2 reuses the existing argument/local load, local store, arithmetic, and return
handlers; W4.3 adds the policy-gated non-exact branch inside the existing `ldfld` handler rather than creating a
parallel unknown interpreter. Exact receivers, initialized locals, and exact-classified field loads remain exact.
The separate W4 graph-preparation mode admits only the exact branchless root and static
`Int32 CombineMarkers(Int32,Int32)` callee shapes. It decodes and types direct `call`, resolves and correlates each
target, and freezes the complete rooted acyclic closure without exposing executable state. The fixed ceilings are 64
distinct methods and 1,024 method/field/call-site units; these are internal construction guards, not the later product
traversal budget. W4.5's prepared session consumes this retained runtime plan. `call` removes the two typed
arguments, freezes the caller return offset, pushes metadata-derived helper arguments/locals, consumes one instruction,
and emits ordered instruction/frame-push events; helper `ret` propagates the exact or explained result through that retained return
site and emits ordered instruction/frame-pop events. Every prepared step revalidates graph, frame, return-site, and
configured/required/high-water facts. Instruction availability has precedence and all failed transfers preserve state,
memory, operational facts, and events. The legacy path remains call-free and still rejects the W4 fixture before the call.
Arbitrary signatures, generic contexts, MemberRefs/MethodSpecs, branches, broader modeled calls, EH, byrefs, statics
outside the exact callee profile, and broader instance methods remain rejected.

### Concrete validation domain

`Interpreter.Domain.Concrete` supplies the first real implementation of both semantic seams:

- a lifted-flat concrete value lattice with one semantic top per static type;
- executable lattice order and meet/join laws;
- exact structural object references, typed null, and deterministic default construction;
- persistent allocated/imported object, array, and field snapshots;
- exact dump-object import identities and explicit field-cell import;
- branch isolation through immutable memory updates.

W4.2 adds `ProvenanceConcreteDomain` and `ProvenanceConcreteValue` over those same lifted-flat semantics. Exact values
carry no lineage; an explained unknown may carry one domain-owned root. Value equality, hashing, ordering, join, meet,
and widening deliberately ignore that root, so different explanations remain the same per-type semantic top.
`GetPrecision` is the execution-boundary capability that distinguishes exact values, grounded top, and bare top.

W4.3 makes that domain implement `IFieldLoadApproximationDomain<ProvenanceConcreteValue>`. It requires an exact local
object receiver of the frozen declaring type, then atomically interns an `ImportedField` `InputOrigin` and its
`FieldLoadTransform`. The origin retains partial/unavailable status and reason while its source key is the complete
`FieldLoadEvidence` digest. The transform canonically retains the imported-receiver digest, complete frozen field,
and origin predecessor; it never embeds the process-local object-reference number, display names, or a raw address.

W4.5b makes the domain implement `IInterpretedCallLineageDomain<ProvenanceConcreteValue>`. It preflights the complete
two-argument vector before interning a batch: exact positions remain identical, and each explained position becomes a
`CallArgumentTransform` over the complete `DirectCallSiteIdentity`, metadata parameter index, and predecessor. One
explained helper result becomes an `InterpretedReturnTransform` over that call site and predecessor before caller
mutation. Missing capability, capability exceptions, and malformed/non-equivalent capability output map to distinct
stable blocked/invalid machine results without partial frame, state, memory, budget, event, or node publication.

The implemented lineage vocabulary is deliberately closed through W4.6 to canonical `InputOrigin`, ordered
`BinaryTransform`, `FieldLoadTransform`, `CallArgumentTransform`, `InterpretedReturnTransform`, and
`ModeledReturnTransform` nodes. W4.6b appends kind 6 under schema v1; kinds 1–5 retain their exact bytes and IDs.
The modeled-return operation validates and interns its complete call-node/return-node batch atomically, preserves
acyclicity, embeds exact operands rather than creating degenerate nodes, and supports structural capture/replay plus
fresh-domain continuation. Each node ID is the lowercase SHA-256 of
its versioned canonical bytes. A binary transform embeds exact `Int32` operands directly and references unknown
operands by predecessor ID, preserving left/right IL stack order even for commutative operations. Domain interning is
content-based, and field or call insertion preflights the complete atomic group before mutating the intern table.
`CaptureLineage` freezes only the graph reachable from one explained root in deterministic identity order;
`ReplayLineage` prevalidates canonical bytes, IDs, node ordering, reachability, dependency shape, imported-field origin
relationships, call-site identities, `Int32` predecessor types, and parameter indices before interning anything in a
fresh domain, preserving node bytes, IDs, root identity, and graph SHA-256 and permitting deterministic continuation.

Allocated objects receive CLI defaults. Imported objects do not: a field absent from the imported exact evidence is
unavailable rather than zero or top. This remains a semantics-validation domain, not a CLR object-layout emulator or
production heap.

### Artifact metadata

`Interpreter.Metadata.Abstractions` retains module identity/descriptor and complete method/field projection contracts,
including contextual body-independent direct-MethodDef resolution. `Interpreter.Metadata.SRM` implements them with
`PEReader` and `System.Reflection.Metadata`; its reusable projection also operates over counted metadata supplied by
the dump host. The W4.4 projection distinguishes malformed/nil MethodDefs from structurally valid but unsupported
MemberRef/MethodSpec tokens, validates same-module ownership and ordinary managed IL, and decodes exact call signature
facts without reading the target body or local signature.

Paths and names are display or acquisition hints, never identity. A disk module carries both metadata-root identity
(MVID, metadata length, metadata SHA-256) and complete-artifact identity (whole-file length and SHA-256); the latter
prevents PE files with identical metadata but changed IL from aliasing. Projection atomically decodes method body,
calling convention, structural declaring type, implicit receiver, parameters, return, and locals. Field projection is
contextual and preserves declaring type, exact type, and static/literal/RVA dispositions. Unsupported signature/type
forms and value-type/interface receiver shapes return structured failures rather than partial execution shapes.

### Dump evidence

`Interpreter.Host.Abstractions` contains only the counted immutable process-memory read contract and its exact/partial/unavailable result.

`Interpreter.Host.Dump.ClrMD` owns ClrMD-specific evidence:

- content-identified dump sessions and snapshot-scoped runtime-module instances;
- immutable module catalogs and bounded object searches that retain the exact ordinal selector, status/issue,
  scan/match counters and caps, retained-match state, counted reads, and completeness;
- bounded strong-GCHandle discovery with raw handle/object-header validation, plus `Int32`, layout-validated
  `Nullable<Int32>`, bounded-string, metadata-root, and complete tiny/fat method-body observations;
- 8 GiB external-dump admission and a 256 MiB ClrMD cache with stack-derived caches disabled; SRM's typed external `Open` boundary separately admits managed PEs up to 512 MiB;
- coherent `Exact`, `Partial`, `Unavailable`, `Conflict`, and `Invalid` evidence statuses plus stable issue codes;
- ordered raw reads that retain source identity, address, requested length, returned bytes, and missing-byte count.

Nullable descriptors include the outer field plus both child metadata tokens, addresses, and sizes in their canonical
projection. Admission rejects duplicate child tokens, overlap, out-of-extent storage, and extent arithmetic overflow;
evaluation rejects a forged same-snapshot owner address or method table before issuing memory reads.

The dump body obtains its MethodDef RVA from counted dump metadata and its physical header, code, local-signature
token, padding, and declared extra sections from counted dump memory. `ClrmdDumpExecutionResolver` re-parses those
bytes, projects the complete method/field shape from the same counted metadata, derives a snapshot-scoped module
handle, and proves that the exact admitted `ldfld` operand names the correlated runtime field. A disk body's SRM
decode is only an independent test oracle. Dump evidence and disk-artifact evidence remain distinct even when their
metadata-root identities agree; MVID alone is not a sufficient binding.

`ClrmdExactInt32FieldExecutionEvidence` is created only after the rooted object, runtime owner, metadata declaring
type/FieldDef, and exact four-byte observation agree. It retains a stable bounded owner-evidence identity used when
the concrete memory snapshot imports the object and field. Partial or missing ClrMD preparation evidence never reaches
activation. A deliberately absent cell in an already imported test snapshot instead remains unavailable when
`ldfld` queries it; that runtime negative does not fabricate a default or retroactively invalidate activation.
W4.3 does not change that descriptor or add a ClrMD partial-field producer: its `FieldLoadEvidence` seam is exercised
dump-free through an injected memory model, so the existing dump field composition remains exact-only. W4.4 extends
the resolver's metadata side with the same body-independent exact direct-MethodDef target/signature proof; it does not
acquire a callee body, prepare a dump-grounded product graph, or execute a call.

The size/cache caps are deterministic resource controls. Caveat: their evidence covers only the named generated
fixtures and explicitly admitted input shapes.

### Read-only dump query

`Interpreter.Product.DumpQuery` exposes the bounded `DumpQueryEngine` and the closed `DumpQueryValue` projection for one
host-named root, one direct field, and an optional admitted coalescing literal. The compact normative contract is
[Restricted Dump Query v1](restricted-dump-query-contract-proposal.md). Its active public staging surface is:

- `DumpQueryRootBinding` plus `DumpQueryRootBindingStatus`, which retain exact object, exhaustive absence, partial,
  unavailable, conflicting, and invalid host-selection states without converting any non-exact state to null;
- `DumpQueryEngine.Prepare(session, expression, rootBinding)`, which uses the sole complete Roslyn expression parse,
  consumes only the admitted project descriptor, verifies snapshot identity, performs the exact outer-field lookup
  once, checks the `Int32`/`Nullable<Int32>`/`String` and coalescing combination, and returns
  `DumpQueryPreparationResult`;
- immutable `DumpQueryPlan`, which freezes the object-specific binding, selected field descriptor, decoder,
  optional literal, reached bounds, canonical v1 replay projection, and SHA-256 fingerprint; and
- `DumpQueryEngine.Evaluate(session, plan)`, which reads through the selected descriptor without repeating member
  binding and returns the shared `EvaluationResult<DumpQueryValue>` envelope.

The convenience expression/root overload composes preparation and plan evaluation. It is not an alternate binder or
execution path. Every product-level result is `DerivedQuery`; the counted field observations it consumes remain
independently available as `Observation` results.

The evidence context records explicit snapshot/module availability, source and fallback, and only bounds whose guarded
operations were reached on that result path. Exact null, unavailable evidence, a partial string prefix, and a retained
partial primitive-field wrapper remain distinct: the wrapper can explain a failure but cannot become a decoded scalar
answer. Successfully parsed requests have canonical request identities; bounded invalid input has canonical raw-input
identity, while oversized input deliberately does not retain one. Canonical plan identities are injective over the
grammar, root, selected owner and complete field layout, decoder, and exact optional literal. Root-selection policy
provenance separately hashes the selector, disposition, issue, counters, caps, and retained-match state. Successful
plan evaluation provenance includes the plan identity, so an unused fallback remains part of the explanation even
when two plans decode the same value.

The versioned W2 v1 corpus contains 22 product cases over 20 distinct expression texts. Every case runs the pipeline
twice in one session and again after the same dump is closed, reopened, its root rediscovered, and its typed binding
reconstructed; 13 cases proceed from preparation to bound-plan evaluation. Complete
canonical result byte sequences and result SHA-256 values for all 22 cases, plus canonical plan projection strings and
plan SHA-256 values for the 13 cases whose preparation succeeds, must match. The same corpus asserts exact axes,
diagnostics, context, path bounds, ordered provenance payload, and memory-read geometry. The implementation and full
corpus are locally verified and passed all four required hosted jobs at exact closure commit `5bed47100` in [GitHub
Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).

### Standalone counterfactual target outcome

`Interpreter.Product.DumpDebugging` exposes three W4.7 draft types:

- `CounterfactualTargetOutcomeProjector.Project`, which validates the exact machine, initial semantic/operational
  state, and complete immutable issued-transition sequence rather than relying on a naked terminal record;
- `CounterfactualTargetOutcomeProjectionResult`, a fragment-or-stable-failure discriminated result; and
- `CounterfactualTargetOutcomeFragment`, a defensively copied, content-equal schema-v1 record whose fixed axes,
  exact exception/location, one-method call trace, accounting, complete events, diagnostic, canonical bytes, and
  lowercase SHA-256 answer only which exact outcome the admitted activation produced.

The projector accepts the first exact null-reference transition and optionally one issuer-certified idempotent re-step.
It rejects forged or incomplete chronology, unsupported target kind, malformed latch/location, prepared-graph mode,
misaccounting, or an inconsistent transcript with stable `W4.TargetException.*` codes. The fragment has no snapshot,
root, request, plan, or traversal property. `Interpreter.Product.DumpDebugging` depends only on core abstractions and
execution; no host, metadata backend, dump session, query product, rooted facade, or runner dependency crosses it.

### W5/W6 expression-to-result facade

W5 checkpoints `7c3d52572`/`d88b13c2c`/`fc8a43a7a` add a separate rooted path without changing the standalone W4.7
fragment. W6 checkpoints extend only the explicitly selected language profile and derived-query route:

- `DumpQueryEngine.ClassifySyntax` reports whether text belongs to the unchanged W2 shape without preparing, binding,
  or reading a field. Its internal front end performs the one complete pinned Roslyn expression parse shared with the
  W5/W6 recognizers; no Roslyn type crosses the adapter boundary.
- `DumpExpressionLanguageProfile`, `DumpCSharpExpressionProfile`, `DumpExpressionPolicy`,
  `DumpMethodExpressionIdentity`, `DumpMemberChainExpressionIdentity`, `DumpExpressionRequest`,
  `DumpExpressionClassification`, and `DumpExpressionClassifier` preserve exact expression/root text, the admitted
  method or member-chain identity, evaluation mode, deterministic bounds, profile/model/policy versions, canonical
  bytes, and stable classification diagnostics. `FrozenW5` remains the default; `FixedDepthMemberChainV1` is opt-in.
- `DumpMethodAcquisitionFacade.Acquire` owns bounded root/module/type/caller/helper/field reacquisition and returns
  either a detached `CounterfactualDumpExecutionBinding` or a typed `DumpMethodAcquisitionFailure`; it exposes no
  live ClrMD lifetime to later preparation/execution.
- `ClrmdDeclaredDataMemberCertificate`, its declared-type/relative-field/property records, and
  `ClrmdObjectReferenceObservation`/`ClrmdReferencedObjectInfo` retain counted declaration, pointer, target-header,
  extent, intrinsic-identity, and alias-path evidence without invoking a getter or treating non-exact evidence as null.
- `DumpMemberChainPreparationFacade`, `DumpMemberChainPreparationResult`, and immutable `DumpMemberChainPlan` bind one
  reference hop plus one direct field or exactly certified field-backed terminal property and freeze every descriptor,
  decoder, access/fallback choice, evidence item, bound, canonical projection, and identity before evaluation.
- `DumpMemberChainEngine.Evaluate` consumes only that plan, performs one reference observation and descriptor-only
  terminal reads, applies null-conditional/coalescing behavior only to exact nulls, and never repeats declaration
  lookup, invokes a getter, or performs compiler binding.
- `DumpExpressionEvaluator.Evaluate` routes an accepted W2 request to the existing
  `EvaluationResult<DumpQueryValue>`, an accepted method request to the existing W4 preparation/execution path, and an
  explicitly profiled W6 member chain to the preparation facade and frozen-plan engine.
  `DumpExpressionEvaluationOutcome` is a strict union that preserves those original results plus typed
  classification, acquisition, and preparation failures; failure cases acquire no invented common semantic mode.

The [C# Expression Front-End and Subset-Admission Contract](csharp-expression-front-end-contract-proposal.md) governs
the implemented one-parse boundary, project-owned W2/W5/W6/W7 descriptors, and no-parse preparation/evaluation rule.

`Interpreter.Headless.ReferenceConsumer` remains an internal, independently launched prototype host rather than a
stable CLI. Schema v1 preserves W5 scenario replay, schema v2 adds generated W6 exact and typed non-exact rows without
changing the W5 path, and schema v3 evaluates twenty-four independent designed incidents across four graph shapes.
Controlled/generated, designed/synthetic, and representative rows remain separate; promotion is rejected and the
representative/external-observation denominator remains zero.

## Deliberately absent from the implemented W2/W5/W6/W7 profiles

The implemented expression profiles do not contain `this`/local/argument roots, member chains deeper than the one-
reference/one-terminal profile, collection/indexer navigation, arbitrary property execution, reflection, construction,
implicit loading, conversions, general operators, or any method expression except exact
`root.GetMarkerSummary()`. Exact-null member results, null-conditional access, and typed coalescing exist inside
`FixedDepthMemberChainV1` and as an admitted suffix after an exact W7 static reference; broader C# binding/evaluation
is not implemented. W3's public
interpreter activation and W4.2–W4.7's provenance-aware domain/machine, graph, interpreted-call, and pure-model
extensions remain architecture proofs rather than general query-language features. W4.6 supplies structural selection, modeled-return lineage, frozen-
capability execution, attempt/depth witnesses, and compiler/SRM exact/degraded/fresh conformance. W4.8 now supplies
the rooted request/plan/result facade and runner; W4.9 supplies the ClrMD exact/degraded field producer, detached dump
binding, and generated-dump reopen/replay result; W5/W6 expose only their explicitly admitted compositions. Exact
W4 hosted closure passed in run 29463426083. The active W8 design deliberately expands nested/generic static binding,
scoped imports, aliases, literals, and evidence-qualified bare roots, but approval does not create an implemented API.
Broader debugger sessions, async/dynamic models, abstract-analysis worklists, and service locators also remain absent;
their research documents do not reserve API or assembly names.

## Implemented W7 surfaces

The completed W7 plan implements `StaticFieldExpressionV1` over complete Roslyn trees. Roslyn remains the sole
complete expression parser; `StaticFieldExpressionParser` projects its detached tree into bounded type/field/suffix
candidates, `StaticFieldFullyQualifiedBinder` and `StaticFieldContextualBinder` require exactly one counted-metadata
declaration, and `StaticFieldExpressionEvaluator` composes the typed stages. Fully qualified lookup performs no
selected-frame/PDB acquisition. Contextual lookup may consume exact current-namespace, namespace-import, type-alias,
or namespace-alias facts and never chooses by enumeration order.

`Interpreter.Product.DumpQuery` now exposes immutable families for:

- parsed static-access descriptors and canonical expression identities;
- counted module/TypeDef/FieldDef declarations, contextual name expansions, rejected declaration evidence, symbol
  candidates, and strict binding outcomes;
- runtime type, field, ancestry, interface-implementation, nullable, method/member-reference, custom-attribute, and
  assignability identities needed to validate decoders and object targets;
- static value observations, direct terminal values, exact object identities/bindings, and distinct strong-handle,
  host-supplied, and static-expression provenance; and
- `StaticFieldExpressionEvaluationResult`, whose stage/status/suffix axes retain exact, null, absent, partial,
  unavailable, ambiguous, conflicting, invalid, and unsupported terminal cases without candidate leakage.

`Interpreter.Host.Dump.ClrMD` now exposes `DumpSelectedFrameSelector`/observation identities,
`DumpPortablePdbArtifactResolutionRequest` and `IDumpPortablePdbArtifactResolver`, immutable Portable-PDB debug/content/
artifact/scope/import facts, `DumpExpressionBindingContext`, runtime declaration mapping, nullable layout,
`ClrmdStaticFieldEvaluationRequest`, counted storage/raw-memory/target evidence, exact object/string/value results, and
the session operations that acquire those facts under fixed bounds. PDB paths remain hints until bounded bytes match
the module debug identity. High-level runtime value reads and heap enumeration remain late oracles.

The product composition overloads accept the W7 language profile, lazily acquire context only for dependent names,
map one bound declaration to one ordinary application-domain static slot, decode the admitted direct value shapes,
validate exact reference targets, and feed only exact non-null objects into the unchanged W2/W6 suffix planners. The
headless reference consumer adds append-only W7 conformance and sixteen-incident portfolio modes; it remains an
internal prototype host, not a stable CLI. Every public W7 type/method carries draft XML documentation and contract,
canonical, invalid-input, replay, and real-dump tests.

## Approved W8 contract frontier — not implemented

The active [`Post-W7 Path Forward`](../../plans/post-w7-path-forward.md) approves additive
`StaticFieldExpressionV2` and `BindingContextV2` profiles rather than mutating W7. The planned inventory includes
structured nested and closed generic type syntax, exact metadata/runtime construction identities, scope-precise import
and alias facts, extern aliases, lexical-blocker completeness, ordinary stored fields, metadata literals, constructed
field-signature substitution, and distinct plan/result/provenance encodings. If and only if W8.1 proves exact
attributable frame locations, the planned inventory also includes separate `FrameValueExpressionV1` root, location,
source, plan, result, and provenance contracts; it is never a fallback from the static profile. W8.1 must first prove
the physical compiler, metadata, runtime-argument, definition-kind, storage, frame-value, and frame-generic branch
dispositions. Until those gates land, no W8 public type, method, profile member, schema row, or validation result belongs
in this implementation inventory; every W1–W7 contract remains the current executable surface.

## Change rule

A public contract change must include:

1. detailed XML documentation of intent, failure behavior, parameters/returns, and draft caveats;
2. an executable test at the boundary it introduces;
3. deterministic identity and ordering where observable;
4. explicit partial/unavailable behavior for evidence-dependent operations;
5. an update to this inventory when the responsibility materially changes.
