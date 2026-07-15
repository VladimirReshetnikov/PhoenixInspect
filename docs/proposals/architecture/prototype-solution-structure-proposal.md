# Prototype Solution Structure

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Compatibility:** Draft and intentionally reversible

## 1. Why the solution was collapsed

The earlier solution contained 42 source projects: 34 were project-file-only placeholders before this pass, including the now-implemented concrete domain. That physical decomposition encoded an unvalidated multi-product architecture and imposed build/dependency surface without behavior.

The 33 remaining empty placeholders were removed. The one-purpose `Interpreter.Types` and `Interpreter.IL` DTO assemblies were then folded into core contracts. The W2 query slice subsequently justified one behavior-bearing product boundary. A separately landed, non-gating external-worker prototype added exactly two more behavior-bearing boundaries—a trusted Windows broker/protocol assembly and a one-request AppContainer runner—leaving ten source projects. The logical catalog remains in `module-architecture-proposal.md` as historical research, while `architecture-overview-proposal.md` defines the active topology and the rule for future splits.

## 2. Current source projects

The solution retains ten source projects, each containing contracts or behavior exercised by a realized slice:

| Project | Current responsibility |
|---|---|
| `Interpreter.Core.Abstractions` | Structural type/method/field and direct-call-site identities, atomic body and body-independent direct-call resolution shapes, value-domain plus optional value-precision/field-approximation/interpreted-call-lineage capabilities, canonical structured field evidence, typed memory-result, persistent-memory, and budget contracts. |
| `Interpreter.Core.Execution` | Metadata-derived legacy and prepared-graph activation, frozen typed whole-body admission, deterministic rooted direct-call graph preparation and exact/explained-unknown multi-frame execution, structural return sites, exact-only-by-default unknown policy, policy-gated approximate field/call transfer, precision/frame events, and deterministic micro-step/machine-outcome protocol. |
| `Interpreter.Domain.Concrete` | Concrete validation values, persistent allocated/imported object and field memory, and W4.2–W4.5's provenance-aware value/domain plus canonical input, binary, field, call-argument, and interpreted-return lineage graph. |
| `Interpreter.Metadata.Abstractions` | Project-owned metadata identities and complete method/field plus contextual direct-MethodDef projection contracts. |
| `Interpreter.Metadata.SRM` | Active SRM/PEReader artifact adapter and reusable body, body-independent call-signature, local, and field projection over a `MetadataReader`. |
| `Interpreter.Host.Abstractions` | Typed host/dump evidence contracts. |
| `Interpreter.Host.Dump.ClrMD` | Dump loading, runtime/module discovery, raw evidence, and W3 snapshot-scoped execution resolution/import correlation through ClrMD. |
| `Interpreter.Product.DumpQuery` | Closed W2 grammar, typed snapshot-root binding, one-time field selection into immutable canonical plans, bounded `Evaluate(plan)`, closed value projection, and complete-corpus replay. |
| `Interpreter.Host.ExternalWorker` | Trusted Windows x64 staging broker, bounded protocol/contracts, response validation, AppContainer/Job/handle policy, payload-free telemetry projection, and observable cleanup. |
| `Interpreter.Host.ExternalWorker.Runner` | One-request framework-dependent AppContainer executable that re-verifies containment, pins the trusted DAC, disables ambient capabilities, evaluates the admitted dump query, and exits. |

Tests are separated into a fast semantic/contract suite, a real dump integration suite, a Windows external-worker
suite, and two generated target executables: the general dump target and the optimized modeled-incident target. The
real-dump suite contains an independently versioned 22-case/20-expression W2 corpus rather than treating one query in the W1
omnibus test as query-product closure evidence. It also contains a dedicated W3 direct/adjusted getter lane that
executes only exact counted dump evidence and reopens/rebinds the dump for replay. The W4.1 generated fixture freezes
the branchless target and CoreCLR oracle; dump-free W4.2 domain and machine suites exercise precision policy,
explained-unknown arithmetic, lattice laws, canonical lineage identities, capture, and fresh-domain replay. Dump-free
W4.3 evidence/domain/machine suites add canonical structured field evidence, policy-and-capability-gated non-exact
`ldfld`, truthful precision events, imported-field lineage, atomicity, and prevalidated fresh-domain replay. W4.4
metadata/planner suites add body-independent direct-MethodDef signatures, complete rooted-acyclic graph admission,
shared-callee deduplication, deterministic failure precedence, fixed internal safety caps, and exact fixture topology.
W4.5a prepared-machine tests add exact multi-frame call/return transfers, structural return sites, configured/required
depth facts, high-water integrity, event and budget ordering, resolver-free replay, failure atomicity, legacy-session
isolation, and the exact ten-instruction CoreCLR-agreeing fixture execution. W4.5b domain/machine/compiler tests add
atomic explained-unknown argument/return transforms, frozen legacy identity compatibility, stable failure taxonomy,
five-/eight-node graph shape, and same/fresh-session replay without re-resolution.

## 3. Dependency rules

- Core execution depends only on core contracts, never ClrMD or SRM.
- Concrete backends depend inward on project-owned contracts.
- Dump runtime identity and artifact identity are joined through explicit mapping/evidence, not conflated by paths.
- Dependency edges remain acyclic and point toward smaller stable concepts.
- Public prototype APIs carry detailed XML documentation and no compatibility promise.

## 4. Evidence boundary

The active integration seam proves:

```text
write full dump
  -> content-identify and open it read-only
  -> discover a runtime module and bounded strong-GCHandle root
  -> perform counted dump-memory reads for primitive/string/metadata/IL evidence
  -> convert root-search evidence into exact-object/absence/partial/unavailable/conflict/invalid typed binding state
  -> parse the closed W2 grammar and select the requested instance field exactly once during preparation
  -> freeze snapshot, owner, field, decoder, optional literal, and reached bounds into an immutable canonical plan
  -> evaluate that plan through its selected Int32, Nullable<Int32>, or String decoder without member rebinding
  -> decode MethodDef RVA, tiny/fat header, code, locals, padding, and declared extra sections from counted dump evidence
  -> project body + signature + locals + receiver + exact ldfld FieldDef from that counted dump evidence
  -> correlate the rooted runtime owner and exact four-byte Int32 observation with the admitted field operand
  -> import only that exact cell into persistent concrete memory
  -> derive root activation from metadata and freeze a typed whole-body plan before instruction zero
  -> execute the direct and constant-adjusted getters through one real IMemoryModel ldfld transfer
  -> compare with a full-content-identified disk artifact as an independent late fixture oracle
  -> report explicit snapshot/module availability, source, fallback, and only bounds whose operation was reached
  -> preserve partial wrappers as explanatory evidence without manufacturing a scalar answer
  -> repeat all 22 versioned query cases in one session, then close/reopen and reconstruct the root binding
  -> reproduce every canonical result byte sequence/SHA-256 and each successfully prepared plan projection/SHA-256
  -> close/reopen/rebind the W3 module, root, method, field, and import, then reproduce execution transcripts
```

W4.2–W4.4 add separate dump-free evidence boundaries within the existing core and metadata projects:

```text
bounded partial/unavailable input origin
  -> content-addressed InputOrigin node + explained Int32 semantic top
  -> optional ExplainedInt32 machine policy
  -> existing argument/local/store/arithmetic/return transfers
  -> ordered BinaryTransform nodes with embedded exact operands

exact local receiver + canonical partial/unavailable FieldLoadEvidence v1
  -> MemoryLoadResult.FromFieldEvidence
  -> matching frozen field + ExplainedInt32 policy + optional IFieldLoadApproximationDomain capability
  -> existing ldfld transfer + unchanged memory
  -> InstructionExecuted then ValuePrecisionLost carrying the evidence
  -> ImportedField InputOrigin + FieldLoadTransform(receiver digest, frozen field, origin)

either explanation root
  -> reachable-only canonical graph capture
  -> full graph prevalidation before replay mutation in a fresh provenance-domain instance

root MethodDef + contextual direct-call token
  -> body-independent same-module managed-IL ResolvedMethodCallTarget + exact call signature
  -> complete root/callee definition acquisition and typed whole-body admission
  -> deterministic root-first, call-site-ordered traversal with shared-callee deduplication and cycle rejection
  -> canonical FrozenMethodGraphPlan(nodes, fields, call sites, required logical depth, internal units)
  -> no machine activation, call execution, or partial plan exposure
```

Semantic equality, hashing, lattice order, join, meet, and widening ignore the optional lineage root. The default
`ExactOnly` policy preserves W3 behavior; exact receivers, initialized locals, and exact-classified field loads remain
exact. Bare top is not executable. Code-only partial/unavailable results remain non-continuing, conflict remains
blocked, and invalid or mismatched evidence remains invalid. `FieldLoadTransform` is append-only node kind 3 under
the existing schema; W4.2's hard-coded input/binary canonical bytes and IDs remain unchanged.

The runtime binding identity is the counted metadata root's MVID, exact metadata length, and metadata SHA-256. W3's
execution module handle additionally incorporates stable snapshot/runtime-module evidence, so different loader
instances cannot alias through repeated names or addresses. The independently opened disk PE has a whole-file
identity (exact artifact length plus SHA-256), so changing IL outside the metadata root changes disk
artifact/module/method handles even if an incorrectly preserved MVID and metadata root would not. That disk identity
is not derivable from the dump metadata root and does not authenticate dump code. The disk bodies are used only to
assert equality: the MethodDef RVA, tiny/fat header, `maxstack`, init-locals flag, local-signature token, code, padding,
and exception sections are decoded from exact counted dump metadata and memory reads. The real-dump evidence includes
tiny `RetOnly`, a compiler-emitted fat body with locals and two EH regions, and exact direct/adjusted `Int32` getter
bodies admitted through the counted-dump resolver.

W2 proves typed binding and bounded evaluation for one host-named, exactly selected non-null object and one exact
instance field. Its admitted value domain is `String`, `Int32`, and `Nullable<Int32>` with only compatible literal
coalescing. A canonical plan includes the grammar version, root/field names, snapshot-scoped owner and field identity,
decoder, and exact optional literal; request identity also preserves bounded failures that never produce a plan. The
full 22-case/20-expression corpus reproduces every result byte sequence/fingerprint and the 13 successfully prepared
plan projection strings/fingerprints within and across dump sessions. This implementation and local headless
verification passed all four required hosted jobs at exact closure commit `5bed47100` in [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).
It does not prove exact-null query roots, frame/local/argument/static recovery, arbitrary heap-root discovery, chained
or null-conditional query access, product-level properties/getters, calls, indexers, arrays, reflection, construction,
general operators, broad IL semantics, or debugger stepping. Separately, W3 proves only counterfactual execution of
the closed E1 arithmetic and E2 direct/constant-adjusted getter profiles; it does not expose that capability through
the W2 product grammar.

Hardened W3 checkpoint `19c292f9f` is locally verified through locked restore, the 15-project zero-warning Release
build, Markdown/headless guards, 103 non-cybersecurity unit tests, 67 fast integration tests, 5 ordinary dump tests,
1 optimized-context dump test, and the focused 2-test W3 lane. All four hosted jobs also passed at that exact
implementation checkpoint in [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally closed at exact
documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit.

W4.1–W4.5 are landed. Exact W4.2 implementation commit `e89e43498` adds the optional precision seam,
`UnknownExecutionPolicy`, `ProvenanceConcreteDomain`/`ProvenanceConcreteValue`, canonical `InputOrigin` and
`BinaryTransform` lineage, and shared-handler unknown arithmetic. That historical implementation checkpoint is 3,454
LOC: 3,429 LOC for W4.2 plus a 25-LOC scope correction; cumulative W4 realization through W4.2 was 3,932 LOC.

Exact W4.3 implementation commit `7479b1ad4` adds canonical `FieldLoadEvidence` v1 and
`MemoryLoadResult.FromFieldEvidence`, optional `IFieldLoadApproximationDomain`, the field transfer jointly gated by
evidence, policy, and capability, ordered `InstructionExecuted`/`ValuePrecisionLost` events, and atomic imported-field
lineage/replay. Its 3,096 realized LOC comprise 1,100 production LOC plus 1,996 test LOC, bringing cumulative W4 realization
to 7,028 LOC. Replacing the first three estimates with realized values projects W4 at 19,228–25,728 LOC; the original
16,860–25,310 baseline remains preserved. Headless local verification passed the strict fifteen-project Release
build, focused W4.3 55/55, complete unit 211/211, fast 71/71, ordinary dump 5/5, optimized-context dump 1/1, and both
documentation guards with zero skips under `Scope!=Cybersecurity`.

W4.4a checkpoint `2e596c117` adds body-independent content-equal `MethodCallSignatureShape`, exact managed-IL
`ResolvedMethodCallTarget`, and contextual direct-MethodDef resolution without acquiring a body. W4.4b checkpoint
`742ef2c4f` adds `MethodGraphPlanner` and immutable graph/node/call-site projections. The planner freezes complete
definitions and typed boundaries, canonical nodes/fields/edges, shared-callee deduplication, required logical depth,
and internal traversal usage; cycles and disagreement fail without exposing a partial plan. The exact fixture has two
method nodes, two fields, one edge at IL offset 12, depth two, and five method/field/edge units. Fixed ceilings of 64
methods and 1,024 units are internal safety guards, not the configurable product traversal budget.

W4.4 realizes 3,651 added LOC (2,076 production plus 1,575 tests), split into 1,043-LOC W4.4a and 2,608-LOC W4.4b.
Cumulative W4 realization through W4.4 is 10,679 LOC. Headless local verification
passed locked restore, the strict fifteen-project Release build at 0 warnings/0 errors, planner 35/35, W4 fixture 6/6,
complete unit 250/250, fast 73/73, ordinary dump 5/5, optimized dump 1/1, and both guards with zero skips; every
behavioral test command used `Scope!=Cybersecurity`.

Pushed W4.5a checkpoint `356c07037` adds `DirectCallSiteIdentity`, `FrameReturnSite`, prepared-graph activation and
operational-state construction, exact multi-frame call/return transfers, ordered frame events, and configured,
required, observed-logical, and active-frame depth facts. The frozen graph is consumed without re-resolution; exact
calls preserve memory and remain isolated from the legacy one-frame session. Independent audit closed capability
exception classification, operational-depth retention, high-water validation after unwind, instruction-availability
precedence, empty-stack terminal validation, and session compatibility/locking with no remaining production finding.
The checkpoint realizes 3,334 LOC (1,590 production plus 1,744 tests), bringing W4.1–W4.5a to 14,013 LOC. Locked
restore, the strict fifteen-project Release solution build and strict unit/integration project builds at zero
warnings/errors, focused prepared-graph tests 25/25, the W4 fixture 7/7, complete unit 275/275, fast integration 74/74,
ordinary dump 5/5, optimized dump 1/1, and both documentation guards passed headlessly under
`Scope!=Cybersecurity` on behavioral filters with zero skips.

Pushed W4.5b checkpoint `c72f6ee9e` adds `IInterpretedCallLineageDomain<TValue>` and append-only canonical kind-4
`CallArgumentTransform`/kind-5 `InterpretedReturnTransform` nodes. Exact values bypass the capability; explained
argument batches validate and intern atomically before frame creation, and explained returns transform before caller
mutation. Schema v1 and legacy identities remain frozen. Missing/throwing/malformed capability outcomes preserve
stable blocked/invalid taxonomy and all state, memory, budget, event, and lineage facts. Capture and fresh replay
validate complete identity, dependency, type, call-site, and parameter-index relationships before mutation.

Exact-commit validation passed locked restore, the strict fifteen-project Release build at 0 warnings/errors,
prepared graph 40/40, combined lineage/audit 76/76 including 29 legacy identity cases, compiler lineage 2/2, W4
integration 9/9, unit 297/297, fast 76/76, ordinary dump 5/5, and optimized dump 1/1, with zero skips and
`Scope!=Cybersecurity` on behavioral filters. Independent audit found no remaining finding.

W4.5b realizes 2,804 LOC (766 production plus 2,038 tests), so combined W4.5 realizes 6,138 LOC and cumulative
W4.1–W4.5 realization is 16,817 LOC. The historical W4.5b estimate of 1,800–2,700 and combined projection of
5,134–6,034 were each exceeded at the upper bound by 104 LOC. The W4.5-closure projection was 25,017–29,417 LOC. A
later design audit split W4.6 into W4.6a at 1,800–2,600 LOC and W4.6b at 2,700–3,500 LOC (4,500–6,100 combined);
this is planning recalibration, not delivered work. Remaining W4.6a–W4.9 is 10,400–15,300 LOC and full W4 projects
to 27,217–32,117 LOC; the original 16,860–25,310 baseline, the original combined W4.5 estimate of
2,300–3,500 LOC, and every earlier projection remain historical calibration facts.

The existing ClrMD field descriptor remains exact-only; W4.3 adds no partial-field dump producer or generated-dump
counterfactual result. W4.5 executes exact and explained-unknown values across interpreted call boundaries with
canonical call/return transforms; models, the counterfactual facade/product result, generated-dump execution, and
hosted closure remain absent and are W4.6a–W4.9 work. The legacy `IlMachine` still rejects the W4 fixture before the
call, while the opt-in prepared session consumes the already frozen graph.

The external-worker projects are separately executable, and their four-test package includes a locally passing real
malformed-artifact process checkpoint. This is non-gating prototype work outside W1–W4; its presence does not admit
an external artifact product surface.

## 5. Rule for adding a project

A new assembly must satisfy all three conditions:

1. it contains implementation required by an active milestone;
2. its dependency boundary is independently useful (for example, it prevents a concrete backend dependency from entering the core);
3. an executable test crosses that boundary.

A desired namespace, future product, candidate backend, or possible plugin is not sufficient. Start as a logical seam or an internal type; split only when evidence makes the boundary real.

## 6. Toolchain

- Stable .NET 10 LTS SDK selected through `global.json`.
- `net10.0` development target; consumer multi-targeting deferred until demanded.
- Central package versions and committed restore lock files.
- Canonical unattended managed entry point: `./eng/Invoke-HeadlessProcess.ps1 dotnet ...`; every test assembly also
  reasserts Win32, thread, WER, and .NET no-dialog policy.
- Required W1 CI target: local-Markdown-link and headless-workflow consistency, locked restore, Release build with
  warnings as errors, fast tests, then the supported ordinary-dump and optimized-context Windows lanes. The worker
  projects remain solution-build-checked, but their tests are outside the default W1 workflow. The historical exact pushed W0 commit
  `3ece32a36eccc06a61025b1b35b58c09f6e4ed09` passed the documentation job, the build/fast job (60
  semantic/differential and 40 fast adapter/harness tests), and the dependent 3-test dump job in
  [GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
  completed 2026-07-14 UTC (2026-07-13 PDT). Third-party actions are pinned to verified release commit SHAs.

That hosted run is the W0 baseline. The malformed corpus and external worker are separately landed, non-gating
prototypes outside W1–W4.

Historical unfiltered local verification on 2026-07-14 passed locked restore, the strict 15-project Release build with
0 warnings/errors, 64/64 core tests, 63/63 fast integration tests, 3/3 ordinary dump tests, and 1/1 optimized-context
test through the headless wrapper. [Hosted run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`.

The later filtered W2 v1 local checkpoint `ff7cd1965` passes the 15-project zero-warning build, 64 core tests, 67 fast
integration tests, 4 ordinary dump tests (including its 22-case corpus), and 1 optimized-context test. All test
invocations exclude `Scope=Cybersecurity`. Restore/build intentionally remains repository-wide, including worker
projects and the integration assembly, as topology/compilation-health evidence only; worker projects/tests provide no
cybersecurity or milestone behavioral evidence. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact W2 closure commit `5bed47100`; the W1 run above remains W1-only history.

The later hardened W3 checkpoint `19c292f9f` passes the same headless, non-cybersecurity workflow locally with
103 unit, 67 fast integration, 5 ordinary dump, 1 optimized-context, and 2 focused W3 tests; [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passes all four jobs at that
implementation commit. Formal W3 closure is recorded at exact documentation commit `de6cea124`, whose [GitHub Actions
run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required
jobs.

The physical layout and contracts remain prototype hypotheses. They may change freely as W4.6–W4.9 force better boundaries.
