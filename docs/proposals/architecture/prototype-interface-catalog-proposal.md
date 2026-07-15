# Prototype Contract Inventory

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Stability:** Draft and reversible

## Purpose

This inventory records the small public contract surface exercised by the current dump-evidence, restricted-query,
W3 concrete-IL, and W4.2–W4.4 dump-free explained-unknown and graph-preparation proofs. It is descriptive, not a
promise of compatibility.
Hardened W3 checkpoint `19c292f9f`
passed the required local non-cybersecurity lanes and all four jobs in [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally closed at exact
documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit. W4.1–W4.4 have since landed. Exact W4.2 implementation commit `e89e43498` remains the
explained-arithmetic checkpoint; exact W4.3 implementation commit `7479b1ad4` is the structured field-continuation
checkpoint; and W4.4 checkpoints `2e596c117`/`742ef2c4f` are the current direct-MethodDef graph-preparation checkpoint.
Its headless local evidence passed the strict fifteen-project Release build, focused planner 35/35, focused fixture
6/6, complete unit 250/250, fast 73/73, ordinary dump 5/5, optimized-context dump 1/1, and both documentation guards
with zero skips under `Scope!=Cybersecurity`. W4.4 realizes 3,651 added LOC (2,076 production plus 1,575 tests), split
into 1,043-LOC W4.4a and 2,608-LOC W4.4b slices. W4 has 10,679 realized LOC and a 21,179–26,779 LOC projection; the
original 16,860–25,310 baseline remains preserved. A contract is added only with an executable
consumer and is removed when it gets ahead of code.

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
- `IValueDomain<TValue>`, including default-value construction, exact type/stack-kind inspection, concrete constants,
  arithmetic, and executable order/meet/join/widen operations;
- optional `IValuePrecisionDomain<TValue>` and `ValuePrecisionKind`, which classify an executable value as `Exact`,
  `ExplainedUnknown`, or `UnexplainedUnknown` without making explanation identity part of the minimum value-domain
  contract;
- optional `IFieldLoadApproximationDomain<TValue>`, which extends the precision capability only for the admitted
  structured ordinary-instance `Int32` field-load continuation;
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

- semantic `MachineState` and separate `MachineOperationalState` budget bookkeeping;
- metadata-derived `ActivateRoot`, root-frame state, optional root return values, terminal target-exception state, and
  a semantic comparer that excludes operational history;
- whole-body `MethodAdmissionResult`, structured `ExecutionFailure`, and low-level `MachineRunStatus`;
- `MethodGraphPlanner.Prepare`, which returns either a complete immutable `FrozenMethodGraphPlan` or a status/failure
  with no partial plan. A successful graph owns canonical `FrozenMethodGraphNode` and `FrozenMethodCallSite` vectors,
  complete definitions/admission, distinct fields, direct edges, required logical depth, and fixed internal traversal
  use; shared MethodDefs are represented once and cycles are rejected; and
- `IlMachine.StepOne`, whose current closed set is `nop`, integer constants, argument/local loads, local stores,
  `add`, `sub`, `mul`, one ordinary-instance `Int32` `ldfld`, and `ret`; and
- `UnknownExecutionPolicy`, whose default `ExactOnly` value preserves the W3 execution boundary and whose opt-in
  `ExplainedInt32` value permits only structurally typed, domain-validated explained `Int32` values; and
- `DebugEventKind.ValuePrecisionLost`, whose successful field-load occurrence follows that instruction's
  `InstructionExecuted` event at the same method and IL offset and carries its canonical `FieldLoadEvidence`.

Activation receives only a method handle, receiver/arguments, and persistent memory. It atomically resolves the body
and metadata shape, validates supplied structural types and execution precision under the selected policy, constructs
exact initialized local defaults, fixes the empty entry stack, and exposes no state on failure. Whole-body admission
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
traversal budget. The legacy `IlMachine` path remains call-free and still rejects the W4 fixture before the call.
Arbitrary signatures, generic contexts, MemberRefs/MethodSpecs, branches, call execution, EH, byrefs, statics outside
the exact callee profile, and broader instance methods remain rejected.

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

The implemented lineage vocabulary is deliberately closed through W4.3 to canonical `InputOrigin`, ordered
`BinaryTransform`, and `FieldLoadTransform` nodes. `FieldLoadTransform` is append-only node kind 3 under the existing
schema, and the hard-coded W4.2 input/binary bytes and IDs remain unchanged. Each node ID is the lowercase SHA-256 of
its versioned canonical bytes. A binary transform embeds exact `Int32` operands directly and references unknown
operands by predecessor ID, preserving left/right IL stack order even for commutative operations. Domain interning is
content-based, and field origin/transform insertion preflights both nodes before mutating the intern table.
`CaptureLineage` freezes only the graph reachable from one explained root in deterministic identity order;
`ReplayLineage` prevalidates canonical bytes, IDs, node ordering, reachability, dependency shape, and the imported-field
origin relationship before interning anything in a fresh domain, preserving node bytes, IDs, root identity, and graph
SHA-256.

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

The size/cache caps are deterministic resource controls. A narrow Windows x64 external-worker prototype has locally
passed one real malformed-artifact checkpoint, but it is non-gating work outside W1–W4 and does not create an
admitted external artifact product surface.

### Read-only dump query

`Interpreter.Product.DumpQuery` exposes the bounded `DumpQueryEngine` and the closed `DumpQueryValue` projection for one
host-named root, one direct field, and an optional admitted coalescing literal. The compact normative contract is
[Restricted Dump Query v1](restricted-dump-query-contract-proposal.md). Its active public staging surface is:

- `DumpQueryRootBinding` plus `DumpQueryRootBindingStatus`, which retain exact object, exhaustive absence, partial,
  unavailable, conflicting, and invalid host-selection states without converting any non-exact state to null;
- `DumpQueryEngine.Prepare(session, expression, rootBinding)`, which parses the closed grammar, verifies snapshot
  identity, performs the exact outer-field lookup once, checks the `Int32`/`Nullable<Int32>`/`String` and coalescing
  combination, and returns `DumpQueryPreparationResult`;
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

## Deliberately absent

The query product surface still does not contain frame/local/argument/static roots, exact-null roots, member chains,
null-conditional access, interpreted properties/getters, calls, indexers, arrays, reflection, construction, implicit
loading, conversions, or general operators. W3's public interpreter activation, W4.2–W4.3's provenance-aware
domain/machine extensions, and W4.4's graph planner are architecture proofs, not query-language features or a
counterfactual-method facade. A ClrMD producer for W4.3 structured non-exact field evidence remains absent, as do call
execution/frame transfer, call transforms, call models, configured depth enforcement, the W4 request/plan/result and
facade, and a generated-dump product result with reopen/replay closure. Those are W4.5–W4.9 work. Speculative
debugger sessions, generic reconstruction, symbol/debug-map providers, async/dynamic models, abstract-analysis
worklists, and service locators also remain absent; their research documents do not reserve API or assembly names.

## Change rule

A public contract change must include:

1. detailed XML documentation of intent, failure behavior, parameters/returns, and draft caveats;
2. an executable test at the boundary it introduces;
3. deterministic identity and ordering where observable;
4. explicit partial/unavailable behavior for evidence-dependent operations;
5. an update to this inventory when the responsibility materially changes.
