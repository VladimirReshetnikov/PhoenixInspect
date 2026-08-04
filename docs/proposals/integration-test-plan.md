# Executable Evidence Plan

**Lifecycle:** Current
**Roadmap relation:** Active
**Last reconciled:** 2026-07-18

## Purpose

Tests in this repository answer specific architectural questions. They do not stand in for product completeness. Every important assertion states whether its input came from dump memory, runtime structure decoded by ClrMD, a whole-file-identified disk oracle, explicit fixture state, or interpreter policy.

The required current plan has two execution lanes:

- a fast, dump-free semantics lane for every change;
- a supported Windows lane that generates real full dumps and exercises the evidence boundary.

The Windows lane now evaluates both the complete W2 v1 admitted query shape and W3's closed counted-dump E2 getter over that
evidence. W4.1 adds a dump-free fixture gate for the exact two-field/direct-call closure while deliberately preserving
W3's rejection boundary; W4.2 adds the dump-free unknown-aware E1/E2 domain kernel and canonical lineage replay without
creating a product/dump result; W4.3 adds dump-free backend-neutral structured field evidence, approximation-domain
capability, precision events, and `FieldLoadTransform` continuation; and W4.4 adds body-independent exact direct-
MethodDef resolution plus deterministic complete graph preparation. W4.5a adds exact frame execution over that frozen
graph with no metadata re-resolution, and W4.5b adds canonical explained-unknown argument/return lineage through the
same call. W4.6a adds exact/no-effect structural pure-model selection, opaque body-free modeled leaves, and fail-
closed activation; W4.6b adds atomic modeled-return lineage construction while preserving prior canonical identities.
W4.6c invokes only the frozen capability and adds atomic exact/unknown caller transfer, attempt/counter/depth evidence,
and stable nontransfer taxonomy. W4.6d proves compiler/SRM exact, degraded, repeated, and fresh-session agreement. A
W4.7 adds dump-free issuer-certified complete target-outcome projection, a fixed standalone canonical fragment, and
direct/adjusted compiler/SRM fresh replay with capability poison/count evidence. W4.8 adds the rooted product runner,
and W4.9 adds the ClrMD evidence producer, detached binding, dump-grounded exact/degraded corpus, close/reopen replay,
and exact hosted closure. W5.1–W5.5b now add the focused expression classifier, typed acquisition facade, mode-
preserving evaluator, external headless consumer, canonical generated corpus, deterministic usefulness-report runner,
and twelve-incident/two-shape meaningful synthetic portfolio through pushed checkpoint `90ade6d92`. W5 is closed for
its defined milestone scope under the milestone-scoped owner exception recorded in the
[`Post-W4 Path Forward`](../plans/post-w4-path-forward.md); rejected hosted jobs are not test evidence.
The completed [`Post-W5 Path Forward`](../plans/post-w5-path-forward.md) and
[`C# Expression Front-End and Subset-Admission Contract`](architecture/csharp-expression-front-end-contract-proposal.md)
define W6's implemented pinned Roslyn front end, opt-in member-chain lanes, exact property/storage truth gate,
independent twenty-four-dump/four-shape synthetic portfolio, and same/fresh/reopen replay obligations.
The completed [`Post-W6 Path Forward`](../plans/post-w6-path-forward.md) records W7's fully qualified/contextual static-
field expression profile, selected-frame/Portable-PDB import producer, counted no-fallback static reads, unchanged
W2/W6 suffix evaluator, and sixteen-dump/four-shape synthetic gate. W7.1–W7.7 supply the physical slot/replay proof,
dump-free typed stage seam, product parser/binder/context/storage/value path, frozen artifact inputs, generated
conformance, and meaningful portfolio.
The [`Post-W7 Path Forward`](../plans/post-w7-path-forward.md) is the sole active W8 sequence. W8.1 is implemented and
locally validated under its [`physical-truth disposition`](../plans/w8-1-physical-truth-disposition.md). It proves exact
compiler/PDB, construction, storage, literal, frame-root, assignability, and replay facts before V2 contracts.
Constructed/thread-relative/RVA/literal/exact memory-frame branches are admitted; context-relative storage and
selected-frame generic arguments are typed non-admitted, and register homes are unproven. W8.2 through W8.8 are closed,
and W8.9 — the thirty-five-incident meaningful synthetic portfolio — is the active checkpoint; the plan document is
authoritative on each checkpoint's exact landed scope and declared boundaries. Its corpus lane executes one independent
hidden full dump per executable incident across four materially distinct application shapes, and reports every
produced-versus-predeclared divergence of an attempted incident as a finding rather than retuning the frozen
predeclaration. Coverage still required by that minimum spans nested and closed constructed owners, scoped imports and
aliases, exact runtime-construction identity, stored/literal values, evidence-qualified bare fields, and the separate
admitted frame-value profile.
Dump-free parser/admission, root, plan-identity, SRM projection, activation/admission,
memory-law, and CoreCLR differential checks remain fast because they require no DAC, process, dump, clock, or network.
Caveat: the current lanes establish behavior only for the named generated fixtures and explicitly admitted input
shapes. Earlier out-of-scope experiments have been removed.

## Fast semantics lane

The fast test project validates the code that should not depend on a DAC, child process, clock, network, or external artifact.

Current proof obligations are:

- the concrete domain satisfies bottom/top, order, join, meet, and widening laws with one canonical typed top;
- the provenance-aware concrete domain satisfies the same semantic laws while excluding lineage from equality, hash,
  and order; its versioned content-addressed `InputOrigin`, ordered `BinaryTransform`, and structured
  `FieldLoadTransform` graph captures and replays byte-identically across fresh domain objects;
- persistent memory forks and subsequent stores cannot mutate an earlier snapshot;
- metadata-projected constants, arguments, initialized locals, `add`, `sub`, `mul`, `ldfld`, and value/void `ret`
  execute with CoreCLR-compatible results for the closed E1/E2 profiles;
- compiler-emitted straight-line arithmetic and direct/adjusted getter methods agree with live CoreCLR results,
  including unchecked overflow and a typed-null receiver;
- the W4.1 `GetMarkerSummary`/`CombineMarkers` fixture has exact compiler-emitted bodies, relational FieldDef/MethodDef
  operands, signatures and header facts; CoreCLR returns `0x26AF37BD`; and current W3 rejects the caller's second
  `ldfld` before activation or memory access while the direct `call` remains visible later in the raw body;
- W4.2 policy admits only locally validated explained `Int32` unknowns, transports them through the existing
  argument/local/arithmetic/return handlers, preserves ordered origins through `add`/`sub`/`mul`, rejects ungrounded,
  foreign, bottom, or policy-disabled values atomically, and gives instruction exhaustion precedence at a valid
  admitted instruction boundary;
- W4.3 immutable backend-neutral field evidence records the exact field, status, reason, source, imported
  object/address, requested size, and bounded observed bytes; its optional approximation capability continues only
  policy-enabled partial/unavailable loads as typed unknowns, emits `ValuePrecisionLost`, preserves memory and budget
  truthfulness, and retains exact/conflict/invalid/typed-null compatibility without fabricating a scalar;
- W4.4 resolves only an exact same-module managed-IL MethodDef with a body-independent content-equal signature,
  rejects MemberRef/MethodSpec substitution and virtual/indirect dispatch, correlates every loaded definition, types
  complete root/callee bodies, freezes canonical nodes/fields/call sites, deduplicates shared callees, rejects cycles,
  calculates required logical depth, and returns no partial plan on any failure;
- W4.5a atomically activates one complete frozen graph, rejects insufficient configured logical depth before frame
  creation, executes exact direct `call`/`ret` with canonical return sites, charges one instruction per transfer, leaves
  memory unchanged, orders instruction events before frame events, records observed/active depth high-water facts, and
  rejects malformed replay state without re-resolving metadata;
- W4.5b probes an optional interpreted-call lineage capability only for explained unknowns; transforms the complete
  two-argument vector before publication and the return before caller mutation; preserves exact values; appends
  canonical parameter-indexed call/return transform nodes without changing legacy identities; distinguishes absent,
  failing, and invalid capability output atomically; and validates reachable-DAG capture/replay before mutation;
- W4.6a freezes bounded model identity/version/stable codes, an exact body-independent descriptor, and a non-generic
  two-`Int32` invocation/outcome/registry vocabulary; selects only `Exact` confidence plus `None` effects after caller-
  edge resolution/typing and before target-body acquisition; deduplicates opaque modeled leaves; retains graph
  equality independently of runtime capability identity; and fails every rejected selection without target-body
  fallback or partial plan;
- W4.6a compiler evidence freezes one interpreted root, one modeled leaf, two fields, one edge, five traversal units,
  required logical depth two, and deterministic PDB-free target PE SHA-256
  `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; modeled activation returns
  `EXEC_MODEL_EXECUTION_UNAVAILABLE` before depth, arguments, state, resolver, or model access;
- W4.6b appends schema-v1 kind-6 `ModeledReturnTransform` through optional
  `IPureCallModelLineageDomain<TValue>`; embeds exact operands, wraps explained operands in unchanged kind-4 nodes,
  prevalidates/interns the complete acyclic batch atomically, and validates structural capture/replay plus fresh-domain
  continuation without changing kind-1–5 bytes or identities;
- W4.6c invokes only the capability retained in the frozen leaf, with no registry/resolver/descriptor/body reread or
  fallback; exact and lineage-grounded unknown returns transfer atomically to the caller with one instruction event,
  unchanged memory, and no model frame/event; budget rejection precedes capability entry; immutable attempts,
  invocation/completion counters, independent logical/active depth witnesses, exact terminal depth retention, stable
  failures, and forged-chronology rejection cover both transfer and nontransfer;
- W4.6d directly compares interpreted/model/CoreCLR exact execution and interpreted/model partial/unavailable
  execution through the real compiler/SRM adapter, repeating the proof with fresh metadata readers, domains, and
  machines while freezing canonical graph identities;
- metadata-derived activation receives only the method, ordered values, and persistent memory; caller counts, local
  values/counts, and return disposition are rejected as inputs;
- whole-body typed admission rejects unsupported signatures, field identities/storage, suffixes, EH,
  malformed/truncated instructions, invalid slot/stack types, injected resume state, nested frames, decorated/multiple
  getter loads, and offsets inside operands before consuming budget, emitting events, or calling memory;
- direct and adjusted getters call the injected memory model exactly once, preserve memory, and distinguish exact,
  partial, unavailable, conflict, invalid, and structured target-exception loads;
- identical normalized inputs produce a byte-identical canonical transcript;
- repeated construction from fresh metadata/resolver/machine/memory inputs reproduces the canonical transcript and
  SHA-256 fingerprint;
- structural module, type, method, and field handles remain identical when the same PE is copied to a different path
  and do not alias across modules;
- module and method handles differ when IL bytes change even if metadata/MVID, PE timestamp, and image size are deliberately preserved, because complete-artifact length/SHA-256 participates in disk-backed identity;
- metadata not-found, ambiguity, malformed content, and module mismatch remain distinguishable.

These tests may use fixed IL where a C# compiler cannot conveniently emit the intended malformed or local-slot shape. Differential claims use actual compiler-emitted method bodies and invoke the same methods live on CoreCLR.

## Real dump-evidence lane

`tests/PhoenixInspect.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs` generates one full dump from `PhoenixInspect.TestTarget`. The target keeps one object alive through a strong GCHandle; that object contains:

- a known `Int32` marker;
- a known present and a known null `Nullable<Int32>`;
- a known non-null string;
- a null optional string;
- a one-instruction `Program.RetOnly` method; and
- a compiler-emitted fat method with locals and two EH regions.

The combined fast and ordinary dump lanes must prove all of the following through bounded runs. The omnibus
`DumpMemoryEvidenceIntegrationTests` run anchors the memory/evidence path; `ClrmdInstanceFieldInfoTests` proves the
layout-admission part of item 15, `ForeignSnapshotIsolationIntegrationTests` supplies the separate case in item 17, and
`ClrmdEvaluationResultExtensionsTests` proves item 18. Item 19 is the separately versioned scenario-corpus dump/run;
item 20 is W3's prepared-execution dump/run:

1. The dump is opened read-only and receives a path-independent SHA-256 snapshot identity.
2. The runtime-module catalog is immutable and preserves app-domain/module/image/metadata addresses separately from a target path hint.
3. The metadata root is read from dump memory with an exact counted read; its `BSJB` header and MVID are decoded from those bytes.
4. The dump metadata-root identity (MVID, exact metadata length, and metadata SHA-256) agrees with the independently opened disk artifact before their evidence is correlated. The disk artifact separately carries exact whole-file length and SHA-256 identity; metadata-root agreement neither proves full PE equality nor makes disk bytes dump evidence.
5. A bounded handle search reports `Partial` when its scan budget is exhausted; it never calls that prefix exhaustive.
6. Bounded handle enumeration discovers the unique strongly rooted fixture object; counted raw reads must show that `ClrHandle.Address` contains ClrMD's selected object pointer and that the object header contains the selected type's method table before field evidence is accepted.
7. The marker is decoded only after an exact four-byte dump read.
8. The string path retains the field-reference, method-table, length, and character reads; caller truncation reports a known partial prefix and null remains distinct from unavailable.
9. Missing fields/methods, type conflicts, invalid addresses, and unreadable memory return stable typed outcomes rather than defaults or incidental exceptions.
10. The method's MethodDef RVA is decoded from the counted dump metadata image. Its complete header, `maxstack`, init-locals flag, local-signature token, code, padding, and declared extra sections are read and validated from counted dump memory. The generated `RetOnly` case proves a tiny body; a compiler-emitted real-dump case proves a 12-byte fat header, locals, and two EH regions. A normalized body is exposed only when every required read is exact; the independently decoded disk body is only an equality oracle and supplies no executable input.
11. Root `ret` completes, consumes exactly one instruction of deterministic budget, and emits only the successful instruction/frame events.
12. Failure cleanup terminates the target, closes the dump, and removes the temporary file without blocking on redirected output.
13. The target process starts with a deterministic minimal environment plus dedicated working and temporary directories.
14. ClrMD's file locator is replaced before CLR discovery with a no-acquisition locator; the test proves only that every request routed through that seam is refused. Pinned ClrMD can probe target-reported full paths before or outside the locator, so other acquisition paths remain outside this proof.
15. W2 parses, prepares, and evaluates marker/string/nullable/exact-null/coalescing queries through a typed root and an
    immutable field-bound plan; it rejects null-conditional syntax, refuses missing and unsupported evidence,
    preserves partial strings without erroneous coalescing, and identifies each plan/request canonically. Nullable
    identity includes both child tokens, addresses, and sizes; duplicate/overlapping/out-of-extent/overflow layouts
    and forged same-snapshot owner address/method-table descriptors are rejected before value reads.
16. Deterministic admission rejects dumps above 8 GiB before hashing/ClrMD parsing and managed PEs above 512 MiB before SRM parsing; ClrMD's dump cache is capped at 256 MiB with stack-trace/root caching disabled.
17. Every dump-query outcome carries evidence source, explicit snapshot/module identity availability, explicit fallback,
    and only the deterministic bounds whose guarded operations were actually reached; canonical replay changes when
    this context changes and never fabricates a module or scan bound for an unavailable root. Reserved-name
    collisions, a missing field, and a foreign-snapshot root prove the distinct path-sensitive cases. Search-backed
    bindings retain the exact ordinal selector, disposition, issue, counters/caps, retained-match count/limit state,
    reads, and bounds. Canonical root-selection policy provenance hashes the selector, search/binding statuses and
    issue, counters/caps, retained-match count, and limit flag; reads and bounds remain separate provenance/context.
18. Generic field projection does not turn a retained partial `ClrmdInt32FieldObservation` wrapper into a decoded scalar
    answer. The wrapper remains explanatory evidence, while answer completeness is `None`.
19. `DumpQueryScenarioCorpusIntegrationTests` executes 22 versioned product cases spanning 20 distinct expression
    texts twice in one session, closes and
    reopens the same dump, rediscovers/rebinds the root, and reproduces the complete canonical result byte
    sequence/SHA-256 for all 22 cases
    plus the canonical plan projection string/SHA-256 for the 13 cases whose preparation succeeds. It also asserts
    exact axes, diagnostics, module/source context, independently expected path bounds, full ordered provenance
    payload, and value-read geometry. Distinct unpaired UTF-16 literals remain plan-distinct even when their fallbacks
    are unselected and their returned values match.
20. `W3DumpGetterExecutionIntegrationTests` derives a snapshot-scoped execution module and direct/adjusted getter
    method/signature/FieldDef shapes from counted dump metadata plus exact physical method-body evidence; proves that
    the one admitted `ldfld` operand is the correlated runtime `Int32` field; imports only its exact four bytes into a
    persistent-memory snapshot; and executes through `IlMachine`. Partial method or field evidence stops preparation
    before activation or import. A deliberately absent imported cell permits activation and the preceding
    `ldarg.0`, then blocks the attempted `ldfld` with one memory query but no field transfer, fabricated default, or
    consumed field-instruction budget. Same-machine, fresh-resolver, and dump close/reopen/rebind runs reproduce
    canonical identities, transcript bytes, and SHA-256. The disk PE is opened only after dump execution as an
    independent CoreCLR/body oracle.

A separate dump test generates an optimized Release modeled-incident target and retains `this`, argument, local,
static, and strong-root axes. Historical schema v1 recorded raw member bytes at 5/5, attributable context at 1/5, and
product-query availability at 1/5. W7.1 moves the runtime adapter to ClrMD 4.0 for .NET 10 static-base support and emits
schema v2 with attributable context at 2/5 while product-query availability remains 1/5. Those counts describe one
generated dump, not representative external recoverability. The W7.1 truth gate separately proves counted metadata,
a stable nonzero slot, exact raw pointer/header reads, reopen replay, and only then high-level/heap oracle agreement.

## W3 concrete-execution evidence lane

The [normative W3 contract](architecture/concrete-il-execution-contract-proposal.md) requires eleven headless evidence
gates. At exact strengthened implementation checkpoint `19c292f9f`, their status is:

1. Structural type, method, and field identities, including cross-module non-aliasing: passing in the dump-free
   metadata-identity suite.
2. SRM projection of static/instance arguments, `void`/`Int32` returns, initialized locals, and FieldDefs: passing from
   one content-identified metadata source, without Reflection-derived interpreter shape.
3. Structured rejection of unsupported signatures, local shapes, field tokens/definitions, EH, and opcodes: passing
   in metadata/admission negatives.
4. Activation without caller-supplied argument/local counts, local values, or return disposition: passing from frozen
   resolved method definitions.
5. Typed whole-body admission that prevents a supported prefix from executing before a rejected suffix: passing with
   expected bounded resolution calls, zero memory calls, and unchanged state, memory, budget, and events.
6. Concrete-domain and persistent-memory laws, including allocated defaults and absent imported fields: passing.
7. Direct and adjusted getters with exactly one injected memory-model load and unchanged memory: passing in fast
   machine tests and the counted-dump E2 run.
8. CoreCLR differential agreement for arithmetic, unchecked overflow, getters, and null receivers: passing over
   compiler-emitted fixture shapes.
9. Repeated and fresh-session canonical replay: passing for fresh metadata/resolver/machine/memory construction and
   for dump close/reopen/module-root-method-field rebind.
10. Generated real-dump E2 execution whose method metadata/body and field value come from counted dump evidence:
    passing in `W3DumpGetterExecutionIntegrationTests`; disk bytes supply only a late oracle.
11. Repository-wide Release build and required milestone-selected fast, ordinary-dump, and optimized-dump jobs: passing
    locally with the matrix below and in all four jobs of the exact-commit hosted implementation-checkpoint run.

The strengthened implementation checkpoint realizes `+8,842/-1,650` hand-written LOC (`+5,362/-928` production and
`+3,480/-722` tests/fixtures) plus 39 generated lock-file lines. All eleven gates pass locally, and [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29374585767) passed all four required jobs at
that exact pushed code checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237), formally closing W3 for its
defined milestone-selected scope.

## Evidence matrix

| Assertion | Evidence source | What it does not prove |
|---|---|---|
| Snapshot SHA-256 | Dump file bytes | Runtime or artifact identity by itself |
| Runtime module instance | ClrMD structures in the dump | That the target path exists on the analysis machine |
| Dump metadata-root identity | Metadata-root bytes read from dump memory, compared by MVID, exact metadata length, and metadata SHA-256 | Full PE image equality outside the metadata region |
| Complete disk-artifact identity | Exact whole-file length and SHA-256 of the independently opened PE | That the same complete PE image was captured in the dump |
| Marker/string | Counted target-memory reads plus same-runtime layout facts | Historical execution or uncaptured values |
| MethodDef RVA | Method row decoded from the counted dump metadata image | That the complete body range is captured/readable |
| Header, code, locals token, and EH count | Exact counted dump-memory reads of the tiny/fat header and all declared extra sections | That the interpreter supports executing locals or handlers |
| Disk method body | Independent SRM decoding from the whole-file-identified test artifact | Any fact in the dump-backed executable body's construction |
| W3 resolved method/field plan | Structural method/type/FieldDef projection from one exact counted metadata image plus the exact physical dump method body | Any signature, opcode, field family, inheritance conversion, or handler outside the closed E1/E2 profiles |
| W3 imported field cell | Exact owner/type/token correlation and exact four-byte dump observation retained in an immutable imported-object snapshot | A value for any absent, partial, unavailable, conflicting, or invalid field observation |
| W3 getter result | `IlMachine` execution through the injected value domain and persistent-memory capability, with load count, budget, events, state, and memory asserted | Historical target execution, broad method evaluation, or user-facing query semantics |
| Evaluation evidence context | Explicit dump snapshot/module identities, evaluator source/fallback policy, and only bounds whose guarded operations were reached | That an unavailable module/root was recovered or that an unapplied limit constrained the result |
| Generic partial-field projection | Retained typed wrapper, provenance, issue code, and `None` answer completeness | A decoded scalar value |
| Fresh-session replay | Complete canonical result byte sequence/SHA-256 for all 22 cases and canonical plan projection string/SHA-256 for the 13 prepared cases after dump close/reopen and module/root rediscovery | Historical process replay or equivalence across different snapshots |
| Modeled optimized report | One generated optimized Release full dump with five predeclared axes | Representative private-production context recoverability or a readiness percentage |
| Live result in differential tests | CoreCLR invocation in the test process | Dump recoverability |
| Interpreter result | Metadata-derived activation, frozen typed plan, explicit value domain, persistent memory, and deterministic budget/event policy | Historical replay or product-level expression evaluation |
| W7 selected-frame import context | Identity-validated bounded Portable PDB bytes, exact active LocalScope/ImportScope chain, and project-owned namespace/import/alias facts | Stack values, exact closed generic runtime arguments, lexical blocker completeness, or broader language binding |
| W7 static-field result | Counted metadata, one exact non-generic declaration, pinned runtime slot, counted raw value reads, and optional unchanged suffix evaluation | Nested/generic owners, literals, per-construction slot selection, or any W8 branch |
| Preview demo answers | One generated full dump of `samples/Contoso.OrderService`, with the exact rendered answer of every expression `eng/demo-session.pi` submits, an exhaustive strong-handle search reaching the same object, and the stalled frame resolved by name from snapshot metadata | That the demo's shapes generalize beyond the admitted subset, or that any published claim outside those expressions is proven |
| Active W8.2 contract gate | W8.1 physical facts plus immutable expression/frame syntax, caller-supplied lexical evidence, one shared bounded Core signature grammar and Product event adapter, exact source ends/token catalogs, TypeSpec/FieldSig/GenericParam/edge proof artifacts, provisional type classification, and Nullable topology | Mandatory downstream proof consumption, a host-owned lexical/metadata producer, complete V2 contract surface, binder, runtime/storage mapper, evaluator, report schema, or portfolio result |

## CI policy

Every managed restore/build/test step uses `./eng/Invoke-HeadlessProcess.ps1 dotnet ...`; repository policy rejects raw
workflow `dotnet` launches. The workflow uses the pinned .NET 10 SDK and locked packages, runs repository-owned
local-Markdown-link and headless-workflow consistency checks, builds Release with warnings as errors, runs the fast suite,
and then runs the required ordinary-dump, preview-demo, and optimized-context Windows lanes. The preview-demo
lane runs `eng/Invoke-PreviewDemo.ps1` end to end — build, target launch, readiness wait, dump capture, and
scripted replay — and uploads the transcript, because the demo is the preview's front door and its script is
not exercised by the test lanes. Every current test command runs all
remaining tests in its selected category, and restore/build cover the complete current 28-project solution. Caveat:
this establishes behavior only for the named fixture shapes. Third-party actions are pinned to verified release commit
SHAs. A missing DAC or inability to write/load a required dump is a failing infrastructure signal, not a passing skip.

That workflow passed service-side for exact pushed completion commit
`3ece32a36eccc06a61025b1b35b58c09f6e4ed09` in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT). Documentation consistency passed; the build/fast job passed locked restore,
a zero-warning Release build, 60 semantic/differential tests, and 40 fast adapter/harness tests; and the dependent
Windows job passed 3/3 required dump tests.

That service run is the historical W0 baseline only. It does not contain the later result-context, optimized
modeled-incident, headless-policy, topology, W2 query, or W3 execution packages. A later gate is CI-enforced only after
a successful hosted run records its exact pushed closure commit.

[GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`: documentation/headless consistency, the 15-project Release build and fast
suites, ordinary real-dump evidence, and optimized-context evidence.

Current local verification on 2026-07-14 at W2 implementation commit `ff7cd1965` passed locked restore; a strict
15-project Release build with 0 warnings and 0 errors; 64/64 milestone-selected `PhoenixInspect.Tests`; 67/67
`Category=Fast` integration
tests; 4/4 ordinary dump tests (including the 22-case W2 corpus); and 1/1 modeled optimized-context test. Every
managed command used `Invoke-HeadlessProcess`; there were no skips or UI. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29364905178) then passed all four required
jobs at exact W2 closure commit `5bed47100`.

Current local W3 verification at exact strengthened implementation checkpoint `19c292f9f` passed locked restore; a strict 15-project
Release build with 0 warnings and 0 errors; 103/103 `PhoenixInspect.Tests`; 67/67 fast integration tests; 5/5 ordinary dump
tests; 1/1 optimized modeled-context test; and a separate focused 2/2
`W3DumpGetterExecutionIntegrationTests` invocation. The focused invocation is a filtered view/re-run, not two
additional ordinary-dump facts. The Markdown-link and headless-workflow guards also passed. Every managed command used
`Invoke-HeadlessProcess`, every test command used the milestone-selected set at that commit, and there were zero skips and no UI. The
checkpoint realizes `+8,842/-1,650` hand-written LOC (`+5,362/-928` production and `+3,480/-722` tests/fixtures) plus
39 generated lock-file lines. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29374585767) passed all four required jobs on
the same exact pushed implementation checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` passed the same four-job workflow in [run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237), satisfying the closure gate.

Historical local W4.2 verification at exact pushed checkpoint `e89e43498` passed locked restore; a strict 15-project
Release build with 0 warnings and 0 errors; focused W4.2 53/53, split into 23 domain, 14 lineage, and 16 machine facts;
the complete 156/156 `PhoenixInspect.Tests` lane; 71/71 fast integration tests; 5/5 ordinary dump regression tests; and
the Markdown-link and headless-workflow guards. Every managed command was headless, every test command used
the milestone test selection, and there were zero skips. The checkpoint accounts for 3,454 LOC: 3,429 attributable W4.2
implementation LOC (1,521 production plus 1,908 tests) against the final refined 3,350–3,500 LOC estimate, plus 25
scope-segregation LOC. At that checkpoint W4 had 3,932 realized LOC and projected 18,532–26,132 LOC across the
then-seven remaining W4.3–W4.9 slices. Exact E2 field loads remained exact; partial/unavailable continuation and its
field transform/precision evidence remained W4.3 work. The ordinary dump lane is regression evidence only: W4.2 creates no
product facade or dump-grounded counterfactual result.

Historical local W4.3 verification at exact implementation checkpoint `7479b1ad4` passed locked restore; a strict
15-project Release build with 0 warnings and 0 errors; focused W4.3 field-evidence/domain/machine tests 55/55; the
complete 211/211 `PhoenixInspect.Tests` lane; 71/71 fast integration tests; 5/5 ordinary dump regression tests; 1/1
optimized dump regression test; and the Markdown-link and headless-workflow guards. Every managed command was
headless, every test command used the milestone test selection, there were zero skips, and no UI was displayed. The checkpoint
realizes 3,096 implementation LOC: 1,100 production LOC plus 1,996 test LOC. W4 has 7,028 realized checkpoint LOC through W4.3,
12,200–18,700 LOC remaining across W4.4–W4.9, and a current 19,228–25,728 LOC total projection. It implements
backend-neutral structured field evidence, its load-result/capability/event contracts, policy-gated partial/unavailable
continuation, and canonical `FieldLoadTransform` behavior while preserving exact/conflict/invalid/typed-null and atomic
failure/budget behavior. Both dump lanes are regression evidence only: W4.3 adds no ClrMD evidence producer, product
facade, dump-grounded W4 result, direct call, or hosted umbrella closure.

Current local W4.4 verification at pushed checkpoints `2e596c117` and `742ef2c4f` passed locked restore; a strict
15-project Release build with 0 warnings and 0 errors; focused graph-planner tests 35/35; focused W4 fixture tests 6/6;
the complete 250/250 `PhoenixInspect.Tests` lane; 73/73 fast integration tests; 5/5 ordinary dump regression tests; 1/1
optimized dump regression test; and the Markdown-link and headless-workflow guards. Every managed command was
headless, every behavioral test command used the milestone test selection, there were zero skips, and no UI was displayed.

W4.4 realizes 3,651 added LOC: 2,076 production LOC plus 1,575 test LOC. The implementation is split into W4.4a at
1,043 LOC and W4.4b at 2,608 LOC to preserve the per-slice ceiling. W4 has 10,679 realized LOC through W4.4,
10,500–16,100 LOC remaining across W4.5–W4.9, and a current 21,179–26,779 LOC total projection while retaining the
original 16,860–25,310 baseline.

The exact generated fixture now prepares as two method nodes, two distinct fields, one direct edge at caller IL
offset 12, required logical depth 2, and five fixed internal traversal units. The planner freezes definitions and typed
boundaries before exposing success, deduplicates a shared callee, rejects self/mutual recursion, and preserves
`Conflict` without exposing partial plans. Its 64-method and 1,024 method/field/edge-unit caps are fixed internal resource
guards, not the configurable product traversal budget. Both dump lanes remain regression evidence only: W4.4 does not
execute calls, enforce request depth, select models, construct a product result, or ground the graph in dump evidence.

Current local W4.5a verification at exact pushed commit `356c07037` passed locked restore; focused prepared-graph
execution tests 25/25; focused W4 fixture tests 7/7; the complete 275/275 `PhoenixInspect.Tests` lane; 74/74 fast
integration tests; 5/5 ordinary dump regression tests; 1/1 optimized dump regression test; the strict 15-project
Release solution build and strict unit/integration project builds with 0 warnings and 0 errors; the Markdown-link guard over 62 files/41 destinations; and the
headless-workflow guard over one workflow. Every managed command was headless, every behavioral filter used
the milestone test selection, there were zero skips, and no UI was displayed.

W4.5a realizes 3,334 added LOC: 1,590 production LOC plus 1,744 test LOC. W4 has 14,013 realized LOC through W4.5a.
W4.5b was then estimated at 1,800–2,700 LOC, projecting combined W4.5 at 5,134–6,034 LOC and full W4 at
24,013–29,313 LOC while preserving the original 16,860–25,310 baseline and all earlier checkpoint estimates.

The exact integration fixture now reaches the CoreCLR oracle through 10 executed instructions and two field loads,
leaves memory unchanged, reaches observed/active logical-depth high-water 2/2, emits ordered call/frame/return events,
and proves that the prepared execution path performs no metadata re-resolution. Insufficient configured depth fails
before execution. An independent audit found no remaining production findings after the checkpoint fixes, including
active/unwound/terminal depth and terminal empty-stack validation, failure classification, budget precedence, and
atomic session compatibility/rebinding. Explained-unknown call/return lineage still reports
`EXEC_CALL_LINEAGE_UNAVAILABLE` at that checkpoint; models, product, dump, and hosted closure remain pending.

Current local W4.5b verification at exact pushed commit `c72f6ee9e5545240433294cdca4f350808339aef` passed locked
restore; focused prepared-graph tests 40/40; the combined lineage/audit set 76/76, including 29 frozen legacy identity
cases; compiler-lineage fixtures 2/2; the W4 integration aggregate 9/9; complete unit 297/297; fast integration 76/76;
ordinary dump regression 5/5; optimized dump regression 1/1; the strict single-node 15-project Release build with
0 warnings and 0 errors; and both documentation guards. Every managed command was headless, every behavioral filter
used the milestone test selection, there were zero skips, and no UI was displayed. An independent audit found no production
or test findings.

W4.5b realizes 2,804 added LOC: 766 production LOC plus 2,038 test LOC. Combined W4.5 realizes 6,138 LOC, bringing
W4.1–W4.5 to 16,817 realized LOC. The historical W4.5b estimate was 1,800–2,700 LOC and the combined W4.5 projection
was 5,134–6,034 LOC; each upper bound was exceeded by 104 LOC. The W4.5-closure full-W4 projection was
25,017–29,417 LOC, preserving the original 16,860–25,310 baseline, original combined-W4.5 estimate of
2,300–3,500 LOC, and prior checkpoint projections of 18,532–26,132, 19,228–25,728, 21,179–26,779, and
24,013–29,313 LOC.

The mixed partial/exact compiler graph captures five reachable nodes: origin, field transform, parameter-zero call
transform, binary transform with the exact operand embedded, and return transform. The partial/unavailable graph
captures eight: two origins, two field transforms, two parameter-indexed call transforms, binary transform, and return
transform. Each executes 10 instructions, performs two field loads, leaves memory unchanged, reaches depth high-water
2/2, avoids metadata re-resolution, and replays in the same or a fresh session.

A subsequent W4.6 design audit split the former 2,300–3,400 LOC model estimate into W4.6a structural
registry/opaque modeled-leaf/effect-and-fallback admission at 1,800–2,600 LOC and the then-unified W4.6b typed
execution/attempts/modeled-lineage/conformance at 2,700–3,500 LOC, or 4,500–6,100 LOC combined. Those remain
historical planning facts.

Current local W4.6a verification at exact pushed commit `77c92789b16d9258c907d5026a36e39f8c957b41` passed
locked restore; the strict 15-project Release build at 0 warnings/0 errors; focused pure-model contracts 49/49; model
planner 25/25; legacy planner 35/35; the real SRM compiler case 1/1; lineage compatibility 2/2; complete unit 371/371;
fast 77/77; ordinary dump 5/5; optimized dump 1/1; and both guards, with zero skips and the milestone test selection on
every behavioral filter. Independent audits found no behavioral finding. W4.6a realizes 2,959 added LOC (1,210
production plus 1,749 tests/fixture support), 359 above its historical upper estimate, bringing W4.1–W4.6a to
19,776 LOC. The checkpoint full-W4 projection under the then-current remainder was 28,376–32,476 LOC.

Current local W4.6b verification at exact pushed commit `fd723a912` passed strict headless builds at zero warnings/
errors, focused modeled-lineage tests 8/8, combined legacy-plus-modeled lineage 44/44, and—through the standard
single-node integration build—`W4CallLineageIntegrationTests` 2/2. Every behavioral filter used
the milestone test selection; there were zero skips and no UI. W4.6b realizes 1,003 added LOC (481 production plus 522
tests), with 23 deletions, bringing W4.1–W4.6b to 20,779 LOC. Kind-6 modeled-return construction is delivered; model
execution/transfer and attempt records are not.

Current local W4.6c verification at exact pushed commit `877c9fb55` passed strict affected Release builds at zero
warnings/errors and focused model-machine conformance 34/34. It proves frozen-capability invocation, atomic exact or
lineage-grounded unknown caller transfer, pre-entry budget rejection, immutable attempts, separate invocation/
completion counters, independent logical/active depth witnesses, exact terminal depth retention, stable failure
taxonomy, and resume chronology. W4.6c realizes 2,734 added LOC: 1,425 production plus 1,309 tests.

Current local W4.6d verification at exact pushed commit `da5346813` passed focused compiler/SRM conformance 3/3 and
aggregate W4 integration 13/13. Exact evidence agrees among interpreted execution, model execution, and CoreCLR;
both partial/unavailable shapes agree between interpreted and model execution. The target PE SHA-256 is
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; the mixed case freezes graph hash
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`, while repeated and fresh sessions reproduce
the both-unknown graph hash `31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`. W4.6d realizes 956 test LOC.

Full exact-code-checkpoint closure passed locked restore, the strict fifteen-project Release build at zero warnings/
errors, unit 413/413, Fast 80/80, ordinary dump 5/5, and optimized dump 1/1. Every behavioral
invocation used `eng/Invoke-HeadlessProcess.ps1`, used the milestone test selection, and recorded zero skips. W4.6
totals 7,652 LOC and cumulative W4 realization is 24,469 LOC.

Historical full-W4 projections remain original 16,860–25,310; post-W4.2 18,532–26,132; post-W4.3
19,228–25,728; post-W4.4 21,179–26,779; post-W4.5a 24,013–29,313; W4.5 closure 25,017–29,417; design audit
27,217–32,117; W4.6a checkpoint 28,376–32,476; first W4.6b recalibration 28,876–33,276; post-split
28,826–33,726; post-W4.6b checkpoint 28,879–33,279; and pre-W4.6c/d closure 30,079–33,729 LOC. W4.6c/d
realized 3,690 LOC against their historical 3,400–3,750 estimate. The former W4.7 estimate of 2,200–3,150 LOC and
31,069–34,319 projection are historical. W4.7a/b realize 2,448/353 LOC, 2,801 total, bringing W4 to 27,270 LOC.
W4.8 subsequently realizes 11,924 LOC and W4.9 2,698 LOC, bringing full W4 implementation to 41,892 LOC. The
31,670–33,970 projection is historical calibration.

Roadmap restore and umbrella-build gates remain repository-wide topology/compilation-health checks. Current W4.6
evidence includes solution/affected-project strict builds plus focused contract, planner, compiler, lineage, machine,
differential, and compatibility lanes.
Historical test totals describe the milestone-selected sets at their exact commits. The current workflow runs every
remaining test in each selected category. Caveat: no behavior beyond the named fixture shapes is claimed.

No workflow uploads dumps, target output, heap values, paths, or expression results. The generated target contains
only source-controlled fixture data and all dumps remain temporary.

## W4 implementation and closure evidence gates

The generated-fixture W1–W2 path and its prior exact-commit hosted closure evidence remain unchanged. W3's structural
identity, SRM projection, metadata-derived activation, typed whole-body admission, concrete-domain/persistent-memory
laws, direct/adjusted getter, CoreCLR differential, same/fresh-session replay, and counted-dump E2 gates all pass at
exact strengthened implementation checkpoint `19c292f9f`, together with the full local repository matrix and all four jobs
of hosted run 29374585767. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` passed all four required jobs in [run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237). W3 is complete for its defined
milestone-selected scope.

W4.2 demonstrates the second meaningful unknown-aware domain over the shared E1/E2 kernel at `e89e43498` while
keeping explanatory lineage outside semantic lattice equality and replaying its reachable graph canonically. W4.3 now
demonstrates the dump-free backend-neutral field-evidence boundary at `7479b1ad4`: partial/unavailable observations can
become policy-approved typed unknowns with exact structured evidence, `ValuePrecisionLost`, and
`FieldLoadTransform`, while exact/conflict/invalid/typed-null outcomes and no-fabrication behavior remain intact. This
is not a ClrMD producer, product facade, or dump-grounded W4 result.

W4.4 now demonstrates body-independent direct MethodDef identity/signature resolution and complete frozen transitive
admission at `2e596c117`/`742ef2c4f`. Its deterministic rooted acyclic graph owns complete definitions, typed
boundaries, canonical field/call dependencies, shared-callee deduplication, required depth, and fixed internal resource
usage before success. This is preparation evidence only; the legacy `IlMachine` still rejects before the direct call.

W4.5a now demonstrates deterministic exact frame push/return execution over that frozen plan, pre-execution
request-depth enforcement, canonical return-site replay, high-water accounting, and no metadata re-resolution at
`356c07037`. It is exact-value kernel evidence only, not counterfactual product execution.

W4.5b now demonstrates canonical explained-unknown argument/return lineage across the interpreted call at
`c72f6ee9e`, including atomic whole-vector transformation, an append-only schema, stable failure taxonomy, and
same/fresh-session reachable-DAG replay. It completes the interpreted-call kernel, not counterfactual product execution.

W4.6a now demonstrates bounded structural model contracts, exact/no-effect body-free selection, opaque modeled-leaf
planning, deterministic graph/depth accounting, and fail-closed activation at `77c92789b`. Its real compiler fixture
replays from the deterministic PDB-free target PE and never acquires the modeled target body.

W4.6b now demonstrates atomic modeled-return lineage construction at `fd723a912`: one append-only kind-6 relation,
exact operands embedded, explained operands preserved by unchanged kind-4 nodes, kinds 1–5 frozen, and validated
same/fresh-domain replay. It is a domain/lineage checkpoint, not model-execution evidence.

W4.6c now demonstrates frozen-capability invocation, atomic exact/unknown caller transfer, attempts/counters/depth
witnesses, failure atomicity, and resume conformance at `877c9fb55`. W4.6d now demonstrates compiler/SRM exact,
degraded, repeated, and fresh-session execution conformance at `da5346813`.

W4.7 now demonstrates standalone target-outcome projection at `2e70fe76d`/`dad6a6dd4`: complete same-machine
issuer-certified IL-zero-to-null chronology, exact latch/location/accounting/events, optional idempotent re-step,
schema-v1 fragment SHA-256 `99cadd992d88ac481b570ec4bc1eb3c914f7d43565db414d9147225e01a9c754`, and fresh direct/adjusted compiler replay.
Closure passed the strict sixteen-project build 0/0, unit 430/430, Fast 80/80, dumps 5/5 and 1/1, focused 15/15 plus
2/2 (17/17 combined), compiler differential 23/23, and both guards with zero skips, headlessly and under
the milestone test selection.

W4.8 checkpoints through `44b050ec8` now demonstrate configurable traversal, canonical rooted request/observation/
plan/result artifacts, private typed bindings and recording memory, authoritative preparation, common standalone/
rooted projection, and transition-validating execution. Focused execution passes 10/10, the counterfactual family
77/77, and complete unit 502/502 with exact synthetic rooted result SHA-256
`6e87efdf3a6f8d73a5f5733aa8fe1eac99d822f184da3b88d46b8cbd67068592`.

W4.9 checkpoints `24bd8fe6f`/`2d41f528d`/`a8b5f32f0` now demonstrate the atomic ClrMD graph/field producer,
detached rooted product memory, and six exact/partial/unavailable interpreted/modeled generated-dump rows. ClrMD is
disposed before execution; close/reopen/rebind reproduces all canonical memory/request/plan/result artifacts; exact
rows agree with late CoreCLR and degraded rows remain typed unknown. Focused generated dump passes 1/1, ordinary dump
6/6, and Fast 88/88. W4.9d's local candidate additionally passes locked restore, strict Release 0/0, complete unit
502/502, Fast 88/88, dumps 6/6 and 1/1, aggregate W4 14/14, and both guards. [Hosted run
29463426083](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463426083) passed all four jobs at exact
implementation-closure commit `a819a08fd9ccdf926620c505732475990b242be9`; [run
29463847230](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463847230) passed them again at final
documentation-closure commit `aaec73c5b987089addb539d3628de67bd815bd8f`, closing W4. New opcodes or method families enter only
through a scenario-derived compiler fixture and its complete
dependency closure; opcode counts and percentage targets do not define readiness. Caveat: validation beyond the named
fixture shapes is outside active delivery. Representative production
measurement was non-gating for W1–W4; designed usefulness evidence became W5's gate before W6 design was admitted,
while representative measurement remains separately scoped.

## W5 implementation and selected usefulness decision

W5's generated dump lane runs one versioned nine-row scenario manifest through the repository-owned headless
consumer. It covers the unchanged W2 field query; exact interpreted and body-free modeled W4 evaluation; partial and
unavailable marker evidence; module acquisition failure; unsupported syntax; zero instruction budget; pre-
cancellation; repeated evaluation; fresh processes; and complete dump close/reopen/rebind. No test method assembles
W4 graph/domain/machine internals for this path.

The W5.5a runner consumes the resulting machine report plus a predeclared question portfolio. Its output retains user
task/expression, root/context attribution, member/method evidence quality, semantic mode, terminal product outcome,
first boundary, known manual object-walking operations, usefulness, decision impact, diagnostics, and dominant
blocker. Aggregates contain raw admission, exact, useful partial/unknown, decision-changing, acquisition-failure,
outcome, and blocker counts; they contain no headline percentage. Corpus kind is carried by both reports and mixing
is rejected.

Focused `Category=Dump&Corpus=W5UsefulnessGeneratedV1` verification passes 1/1 at pushed checkpoint `0f5230e13` with
zero skips. It launches two facade consumers and two usefulness runs through the headless wrapper, requires byte-
identical outputs, and proves that changing only the portfolio label cannot promote the generated evaluation report.
The controlled raw counts are 8/9 admitted, 3/9 exact, 0/4 useful partial-or-unknown, and 0/9 decision-changing; the
representative projection is deliberately 0 questions, 0 incidents, and 0 application shapes. This validates the
runner only.

Focused `Category=Dump&Corpus=W5MeaningfulSyntheticV2` verification passes 1/1 at pushed checkpoint `90ade6d92`.
The test launches twelve isolated hidden targets, writes one full dump per target, runs one fresh hidden consumer and
one predeclared question per dump, requires twelve distinct snapshot hashes across request-pipeline and batch-pipeline
root types, then runs two fresh portfolio processes and requires byte-identical reports. The raw baseline is 8/12
admitted, 4/12 exact, 2/3 useful partial-or-unknown, and 6/12 decision-changing. Four member-navigation blockers outrank
three context-acquisition and one execution-body blocker, selecting `AdmitFixedDepthMemberChain` for design.
The designed corpus contributes zero representative/external-observation rows and establishes no readiness rate.

The cumulative documentation candidate passes locked restore; strict 14-project Release 0 warnings/0 errors; unit
502/502; Fast 104/104; ordinary dump 10/10; optimized dump 1/1; focused W5 facade 3/3; focused generated usefulness
1/1; focused meaningful synthetic usefulness 1/1; Markdown 63 files/59 local destinations; and the one-workflow
headless guard. All behavioral lanes are wrapper-launched and have zero skips. These local facts satisfy the W5 local
closure matrix but not field readiness. Exact-commit hosted run
[29512657137](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29512657137) passes documentation and
Build/Fast. Its real-dump and optimized-dump jobs execute zero steps because GitHub rejects them for account payment/
spending-limit state; an unchanged retry has the same result. Run
[29513051897](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29513051897) likewise executed no jobs.
The owner explicitly waived W5's final hosted-only condition on 2026-07-16, so the milestone is closed on its complete
exact-source local matrix. This exception does not mark the rejected jobs passing, reduce the unchanged selection,
establish field readiness, or apply to a future milestone.

## Closed W6 bounded-member-chain evidence

W6 began with a physical-shape gate before the expression-front-end migration. Compiler-emitted fixtures and SRM
projections agree exactly on the terminal property, getter signature, trivial getter body, and backing field used by
the four literal W5-selected questions. Only that narrow certified data-property projection, plus a direct terminal
field, enters the opt-in `FixedDepthMemberChainV1` plan. Tests prove the getter is never invoked and that W2/W5
default-profile schemas, counts, classifications, and semantics remain unchanged.

W6.2 pins `Microsoft.CodeAnalysis.CSharp/5.3.0` and explicit C# 14 regular-source/full-text options. Its dump-free lane
proves one parse per classification, no parse during preparation, no Roslyn type outside the front-end
boundary, and no metadata or memory access during tree admission. A source-controlled three-bucket corpus distinguishes
valid-admitted profile shapes, complex valid-but-unsupported C# trees, and malformed/recovered/over-limit invalid
inputs. Patterns, lambdas/LINQ/interpolation, casts/indexers/chains, query expressions, and switch expressions provide
meaningful unsupported cases, each paired with a malformed neighbor. The lane preserves W2/W5 diagnostic and
canonical goldens, directly recognizes the W5 invocation, and deletes the production handwritten parser after
differential compatibility passes.

W6.3 is implemented through `6c36bd397`. Four independent hidden full-dump processes cover the request, batch,
coordinator, and certificate-profile graphs; ten exact detached certificates cover direct and property-backed
`String`, `Int32`, and `Nullable<Int32>` storage. Complete counted dump metadata supplies TypeDef/FieldDef/
PropertyDef/method-semantics/signature identity. A MethodDef-token path acquires an unexecuted getter's physical body
without runtime-method materialization or invocation and admits only the frozen trivial backing-field projection. The
adverse rows cover missing/inherited, indexed, static, virtual, computed, call-bearing, unsupported, mismatched,
partial/limited catalogs and bodies, foreign snapshots, and invalid tokens. The disk PE is consulted only after
certificate issuance as an equality oracle. Strict Release, unit, Fast, focused, and complete integration lanes pass
with zero skips.

W6.4 is implemented through `40ece4446`. Synthetic four/eight-byte projections cover exact non-null/null, partial,
unavailable, zero, and conflicting pointer/header evidence. Full-dump graphs prove truthful non-root identity and
extent, equal intrinsic identity but distinct alias-path provenance, internally consistent exact runtime-subtype
rejection, checked range/overflow behavior, and descriptor-only terminal-reader entry points. Complete plan tests
poison every evaluation operation during preparation, preserve certificate evidence/bounds, reject incompatible
coalescing and missing members without a partial plan, and replay canonical identities exactly. The closure matrix is
locked restore; strict serial Release at 0 warnings/0 errors; unit 502/502; Fast 121/121; complete integration 137/137;
Markdown 65 files/89 local destinations; one headless workflow; and zero skips.

W6.5 is implemented through `62c4bb157`. The generated lane exercises exact, exact-null, coalesced-null, partial,
unavailable, conflict, invalid, and unsupported outcomes across the outer reference and terminal storage boundaries.
It proves lifted nullable behavior for null-conditional non-nullable values and descriptor-only evaluation with
poisoned declaration sources. Eleven hidden targets and full dumps cover the four exact selected answers, all typed
root outcomes, an admitted binding miss, and complete Roslyn syntax outside the supported subset. Same-session
repetitions and two fresh reopened hidden consumers per row reproduce byte-identical machine and human reports; the
human report uses value shape only, and the historical schema-v1 consumer path remains passing.

W6.6 is implemented at `93c1f684b`. Twenty-four independent full dumps and one predeclared question per dump span
request, batch, coordinator, and workflow/dispatch object graphs, with four distinct actual root types. The report
retains exact/null/fallback/partial/unavailable/conflict/invalid and unsupported depth/indexer/method rows, raw outcome
and boundary counts, and zero representative/external observations. Six root/context-attribution incidents across all
four shapes uniquely clear the three-incident/two-shape/two-decision threshold and select only
`AdmitOneConcreteContextAcquisitionScenario`. A substantive tie explicitly defers, promotion is rejected, two fresh
portfolio processes produce byte-identical reports, and W5 schema-v1/v2 regression lanes remain passing.

W6.7 closes locally at exact source baseline `440053ad1`: locked restore; strict Release with 0 warnings/errors; unit
502/502; Fast 121/121; complete integration 144/144; focused W6 2/2; optimized context 1/1; all with zero skips; plus
Markdown, headless-workflow, and authored-scope vocabulary guards. The owner's explicit 2026-07-16 W6-only override
of the GitHub billing block is a governance disposition, not hosted execution or pass evidence, and the checked-in
workflow remains unchanged.

## Closed W7 static-expression and binding-context evidence

W7 first passed a physical truth test against the existing optimized target. It parses
`global::PhoenixInspect.OptimizedContextTestTarget.StaticContextProbe.Root` once with pinned Roslyn, uniquely binds its
ordinary static field from counted dump metadata, obtains the initialized slot from the pinned runtime, reads the
pointer through project-owned dump memory, and independently validates the exact target. Convenience value reads and
heap-type scans are late oracles only; the pinned runtime supplies the stable slot without reconstruction or fallback.

The parser/binder lane covers fully qualified, dot-qualified, current-namespace, namespace-import, type-alias, and
namespace-alias spellings; bounded static-field/suffix splits; symbol absence/ambiguity; complex valid-but-unadmitted
Roslyn trees; and malformed neighbors. Poison/count implementations prove one parse, deterministic candidate bounds,
no first/longest-prefix selection, and no context/metadata/dump calls for unsupported syntax. Fully qualified results
must remain byte-identical with stack and PDB sources absent.

The context-adapter lane selects one managed frame, correlates module/MethodDef/instruction evidence, validates bounded
Portable PDB bytes by exact module debug identity, and reconstructs the active LocalScope/ImportScope chain. It tests
exact current namespace/import/alias facts plus missing frame, missing/partial PDB, identity conflict, unresolved scope,
and competing imports. A dependent simple name stops at its first context boundary; the fully qualified control still
binds.

The storage/product lane distinguishes exact `Int32`, nullable, string, null, and validated object-reference values
from exhaustive symbol absence, partial/unavailable storage, ambiguity, conflict, invalid, and unsupported shapes
across module, TypeDef, FieldDef, application-domain, slot, decoder, pointer, and target boundaries. Counting proves the
frozen raw-read set, no candidate leakage, no strong-handle/value-read fallback, and no reparse/rebind. An exact
reference may feed unchanged W2/W6 suffix preparation/evaluation. Legacy W2/W5/W6 bytes stay unchanged.

Hidden same-session, fresh-process, and close/reopen consumers reproduce canonical syntax, context, symbol, storage,
plan, result, and report artifacts. The meaningful lane consists of sixteen independent full dumps over four unrelated
object-graph shapes, with exact fully qualified/contextual answers and explicit context-poison controls. Exact, null,
absent, partial, unavailable, ambiguous, conflict, invalid, and unsupported cases remain first-stop truthful; two fresh
reports are byte-identical; representative counts remain zero; and a substantive tie defers the successor. This
section records the implemented W7.2–W7.7 gates. Exact implementation source baseline `f99b12ee7` passes unit 507/507,
complete integration 242/242, Fast 184/184, ordinary dump 29/29, optimized context 1/1, focused W7 98/98,
`StaticFieldExpressionV1` 1/1, and `W7MeaningfulSyntheticV1` 1/1 with zero skips, plus the repository guards. The
portfolio selects `BindingContextPrecision`; that label alone does not choose a concrete successor rule. The W7-only owner
disposition records rejected hosted jobs without claiming execution evidence or weakening the workflow. That manifest
label permits a bounded successor choice but does not identify or prove one particular alias, import, frame, or generic
rule. The W8 plan makes the successor decision, and W8.1 now supplies its pre-contract physical evidence.

## Closed W8.1 physical gate and active W8.2+ evidence gates

W8.1 began with an executable truth gate rather than a product API. Compiler-emitted targets, independent SRM/Portable-
PDB projections, real full dumps, and late runtime/compiler oracles establish the physical relation among
source spelling, Roslyn tree, nested TypeDef arity, GenericParam ownership/order, TypeSpec bytes, exact runtime type
identity, declaring construction, static storage, and decoded value. Display-name parsing is probe evidence only. A V2
constructed-owner contract may now consume the proven ordered closed-argument source keyed to each runtime type
candidate and the distinct slots for simultaneously loaded constructions sharing a TypeDef/FieldDef.

The completed W8.1 fixture matrix distinguishes facts that must never be collapsed:

- class, value-type, and interface definition kinds; top-level and nested owners; direct, inherited, hidden, and
  unsupported neighboring members;
- exact stored values versus metadata literals, where literal plans perform zero runtime, slot, and memory calls;
- ordinary construction storage, admitted thread-relative and RVA-backed storage, and context-relative non-admission,
  each with its own identity/address/source evidence;
- address-backed values read only from counted dump memory; register-backed frame values remain unproven and absent
  from the planned contract; and
- selected-frame identity/import context, exact live frame-value location, and frame generic construction as three
  independent branches. Exact memory-homed frame roots are admitted; selected-frame generic construction is
  non-admitted. Success in one branch supplies no fact for another.

The landed W8.2 fast proof layer freezes immutable expression/frame foundations, caller-supplied lexical evidence,
and source-anchored metadata objects. Its shared Core grammar and Product event adapter cover complete TypeSpec and
FieldSig trees, direct named roots, nested generics, arrays, modifiers, function pointers, exact source/token ends,
TypeSpec graph cycles and omissions, FieldSig anchors, GenericParam table/owner/binding completeness, edge aggregates,
raw/provisional/exact type classification, and Nullable topology. Exact-cap, cap-plus-one, malformed, incomplete,
duplicate, crossed-owner, defensive-copy, canonical-equality, and public-XML cases expose no usable prefix on a
stop. Verification at `5fd87a3e5` passed strict Release with zero warnings/errors, unit 519/519, integration 344/344,
and focused metadata construction 18/18.

Remaining fast generated tests must freeze mandatory proof consumption, one-Roslyn-parse V2 projection,
namespace/type partitioning, nested per-segment arity, constraints, accessibility, definition-kind member lookup,
scoped alias/import precedence, lexical blocker completeness, constructed assignability, canonical product
identities, bounds, and typed first stops. Poison/count doubles must prove that unsupported syntax makes no metadata,
context, runtime, storage, or memory calls; fully qualified controls make no frame/PDB calls; and literal plans make no
runtime/storage/memory calls.

The ordinary generated-dump lane then covers fully qualified, contextual, extern-alias, current-type, `using static`,
and any W8.1-admitted frame-value route through direct results and the unchanged W2/W6 suffixes. It requires
same-session, fresh-object, fresh-process, and close/reopen/rebind canonical replay. Every repository-invoked managed
command runs through `eng/Invoke-HeadlessProcess.ps1`; every target, helper, or consumer child process spawned by the
tests is configured as hidden and windowless.

The final meaningful synthetic lane contains thirty-five minimum independent full-dump incidents over four
materially distinct application shapes: thirty-two core plus thread-relative, RVA-backed, and frame-value rows. It includes exact and deliberately
imperfect context, TypeSpec, construction, slot, literal, frame, and suffix cases; validates attributable-stage,
usefulness, and counterfactual decision facts independently; keeps representative counts at zero; runs each incident
through its own fresh hidden consumer; and requires two additional fresh portfolio consumers to emit byte-identical
reports. W8 is not complete until this lane, all W1–W7 compatibility lanes, and the W8.1 physical regressions pass with zero skips under the complete
headless repository matrix. The required hosted jobs must then pass at the exact pushed W8 closure commit unless the
owner records a new W8-specific disposition; no earlier milestone disposition is W8 execution evidence.
