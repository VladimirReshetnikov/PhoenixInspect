# Executable Evidence Plan

**Lifecycle:** Current verification plan
**Roadmap relation:** Active
**Last reset:** 2026-07-14

## Purpose

Tests in this repository answer specific architectural questions. They do not stand in for product completeness. Every important assertion states whether its input came from dump memory, runtime structure decoded by ClrMD, a whole-file-identified disk oracle, explicit fixture state, or interpreter policy.

The current plan has three execution lanes:

- a fast, dump-free semantics lane for every change;
- a supported Windows lane that generates real full dumps and exercises the evidence boundary; and
- an independent Windows x64 external-worker prototype-regression lane outside W1–W4.

The Windows lane now evaluates both the complete W2 v1 product grammar and W3's closed counted-dump E2 getter over that
evidence. Dump-free parser, root, plan-identity, SRM projection, activation/admission, memory-law, and CoreCLR
differential checks remain fast because they require no DAC, process, dump, clock, or network. The worker lane retains
a locally passing malformed-artifact checkpoint, but it is non-gating prototype work outside the non-cybersecurity
W1/W2/W3 closure and outside the admitted W4 contract.

## Fast semantics lane

The fast test project validates the code that should not depend on a DAC, child process, clock, network, or external artifact.

Current proof obligations are:

- the concrete domain satisfies bottom/top, order, join, meet, and widening laws with one canonical typed top;
- persistent memory forks and subsequent stores cannot mutate an earlier snapshot;
- metadata-projected constants, arguments, initialized locals, `add`, `sub`, `mul`, `ldfld`, and value/void `ret`
  execute with CoreCLR-compatible results for the closed E1/E2 profiles;
- compiler-emitted straight-line arithmetic and direct/adjusted getter methods agree with live CoreCLR results,
  including unchecked overflow and a typed-null receiver;
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

`tests/Interpreter.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs` generates one full dump from `Interpreter.TestTarget`. The target keeps one object alive through a strong GCHandle; that object contains:

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
6. Bounded handle enumeration discovers the unique strongly rooted fixture object; counted raw reads must show that `ClrHandle.Address` contains ClrMD's selected object pointer and that the object header contains the selected type's method table before field evidence is trusted.
7. The marker is decoded only after an exact four-byte dump read.
8. The string path retains the field-reference, method-table, length, and character reads; caller truncation reports a known partial prefix and null remains distinct from unavailable.
9. Missing fields/methods, type conflicts, invalid addresses, and unreadable memory return stable typed outcomes rather than defaults or incidental exceptions.
10. The method's MethodDef RVA is decoded from the counted dump metadata image. Its complete header, `maxstack`, init-locals flag, local-signature token, code, padding, and declared extra sections are read and validated from counted dump memory. The generated `RetOnly` case proves a tiny body; a compiler-emitted real-dump case proves a 12-byte fat header, locals, and two EH regions. A normalized body is exposed only when every required read is exact; the independently decoded disk body is only an equality oracle and supplies no executable input.
11. Root `ret` completes, consumes exactly one instruction of deterministic budget, and emits only the successful instruction/frame events.
12. Failure cleanup terminates the target, closes the dump, and removes the temporary file without blocking on redirected output.
13. The target process starts with a cleared, allowlisted environment plus isolated working/TEMP directories, so full dumps do not inherit analysis-process credentials.
14. ClrMD's file locator is replaced before CLR discovery with a no-acquisition locator; the test proves only that every request routed through that seam is refused. Pinned ClrMD can probe target-reported full paths before or outside the locator, so this is not network/filesystem isolation.
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

A separate dump test generates an optimized Release modeled-incident target, retains `this`, argument, local, static,
and strong-root axes, and emits a canonical report with raw member bytes at 5/5, attributable context at 1/5, and
product-query availability at 1/5. Those counts describe one generated dump, not representative private-production
recoverability. The generated report is W1 context evidence; representative production measurement is not a W1 gate.

## W3 concrete-execution evidence lane

The [normative W3 contract](architecture/concrete-il-execution-contract-proposal.md) requires eleven headless evidence
gates, all excluding `Scope=Cybersecurity`. At exact hardened implementation checkpoint `19c292f9f`, their status is:

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
11. Repository-wide Release build and required non-cybersecurity fast, ordinary-dump, and optimized-dump jobs: passing
    locally with the matrix below and in all four jobs of the exact-commit hosted implementation-checkpoint run.

The hardened implementation checkpoint realizes `+8,842/-1,650` hand-written LOC (`+5,362/-928` production and
`+3,480/-722` tests/fixtures) plus 39 generated lock-file lines. All eleven gates pass locally, and [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs at
that exact pushed code checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), formally closing W3 for its
defined non-cybersecurity scope.

## Non-gating malformed-artifact corpus

`HostileArtifactCorpus` generates a versioned, deterministic, payload-free manifest for bounded minidump mutations:
all 0–31-byte header truncations, bounded garbage, invalid signature/version, directory overflow/overlap,
`MemoryList`/`Memory64List` truncation, bounded header/directory bit flips, appended junk, and a sparse file just above
the 8 GiB admission boundary. Fast tests prove stable names/order/bytes, coverage, and case/count/size ceilings.

This already-landed corpus is retained as a non-gating prototype outside W1–W4. Its future scope is not an
active test decision for those milestones.

## Non-gating one-shot external-worker lane

The implemented Windows x64 broker/runner projects occupy real solution boundaries and have their own headless test
project. Its four-test package, including a real malformed-artifact process checkpoint, passed locally at
`9fcf00934`. The projects and regression lane are retained as non-gating prototype work outside W1–W4; their
presence does not admit an external artifact product surface.

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
| Malformed corpus fast result | Historical deterministic generated mutation bytes and in-process admission outcome; its five facts are now excluded by `Scope!=Cybersecurity` | Any W1/W2/W3 completion requirement; this corpus is non-gating prototype work |
| Worker prototype checkpoint | One historical locally verified malformed-artifact process result; its test project is not invoked now | Any W1/W2/W3 completion requirement or external product admission |
| Modeled optimized report | One generated optimized Release full dump with five predeclared axes | Representative private-production context recoverability or a readiness percentage |
| Live result in differential tests | CoreCLR invocation in the test process | Dump recoverability |
| Interpreter result | Metadata-derived activation, frozen typed plan, explicit value domain, persistent memory, and deterministic budget/event policy | Historical replay or product-level expression evaluation |

## CI policy

Every managed restore/build/test step uses `./eng/Invoke-HeadlessProcess.ps1 dotnet ...`; repository policy rejects raw
workflow `dotnet` launches. The workflow uses the pinned .NET 10 SDK and locked packages, runs repository-owned
local-Markdown-link and headless-workflow consistency checks, builds Release with warnings as errors, runs the fast suite,
and then runs the required ordinary-dump and optimized-context Windows lanes. Every current test command includes
`Scope!=Cybersecurity`; the worker test project is not invoked and the five hostile-corpus facts are excluded. Restore
and build intentionally remain repository-wide across all 15 projects as topology/compilation-health evidence only,
not cybersecurity behavioral evidence. Third-party actions are pinned to verified release commit SHAs. A missing DAC
or inability to write/load a required dump is a failing infrastructure signal, not a passing skip.

That workflow passed service-side for exact pushed completion commit
`3ece32a36eccc06a61025b1b35b58c09f6e4ed09` in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT). Documentation consistency passed; the build/fast job passed locked restore,
a zero-warning Release build, 60 semantic/differential tests, and 40 fast adapter/harness tests; and the dependent
Windows job passed 3/3 required dump tests.

That service run is the historical W0 baseline only. It does not contain the later result-context, optimized
modeled-incident, headless-policy, topology, external-worker, W2 query, or W3 execution packages. A later gate is
CI-enforced only after a successful hosted run records its exact pushed closure commit; the external-worker package
remains non-gating for W1, W2, and W3.

[GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`: documentation/headless consistency, the 15-project Release build and fast
suites, ordinary real-dump evidence, and optimized-context evidence.

Current local verification on 2026-07-14 at W2 implementation commit `ff7cd1965` passed locked restore; a strict
15-project Release build with 0 warnings and 0 errors; 64/64 non-cybersecurity `Interpreter.Tests`; 67/67
`Category=Fast&Scope!=Cybersecurity` integration
tests; 4/4 ordinary dump tests (including the 22-case W2 corpus); and 1/1 modeled optimized-context test. Every
managed command used `Invoke-HeadlessProcess`; there were no skips or UI. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) then passed all four required
jobs at exact W2 closure commit `5bed47100`.

Current local W3 verification at exact hardened implementation checkpoint `19c292f9f` passed locked restore; a strict 15-project
Release build with 0 warnings and 0 errors; 103/103 `Interpreter.Tests`; 67/67 fast integration tests; 5/5 ordinary dump
tests; 1/1 optimized modeled-context test; and a separate focused 2/2
`W3DumpGetterExecutionIntegrationTests` invocation. The focused invocation is a filtered view/re-run, not two
additional ordinary-dump facts. The Markdown-link and headless-workflow guards also passed. Every managed command used
`Invoke-HeadlessProcess`, every test command excluded `Scope=Cybersecurity`, and there were zero skips and no UI. The
checkpoint realizes `+8,842/-1,650` hand-written LOC (`+5,362/-928` production and `+3,480/-722` tests/fixtures) plus
39 generated lock-file lines. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs on
the same exact pushed implementation checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` passed the same four-job workflow in [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), satisfying the closure gate.

Restore and build remain repository-wide, so all 15 projects are compiled as topology/compilation-health evidence.
Every current test invocation includes `Scope!=Cybersecurity`; the five dedicated hostile-artifact corpus facts are
excluded and no cybersecurity validation is claimed.

No workflow uploads dumps, target output, heap values, paths, or expression results. The generated target contains only non-sensitive fixture data and all dumps remain temporary.

## Post-W3 evidence gates

The generated-fixture W1–W2 path and its prior exact-commit hosted closure evidence remain unchanged. W3's structural
identity, SRM projection, metadata-derived activation, typed whole-body admission, concrete-domain/persistent-memory
laws, direct/adjusted getter, CoreCLR differential, same/fresh-session replay, and counted-dump E2 gates all pass at
exact hardened implementation checkpoint `19c292f9f`, together with the full local repository matrix and all four jobs
of hosted run 29374585767. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` passed all four required jobs in [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). W3 is complete for its defined
non-cybersecurity scope.

W4's admitted but unimplemented contract requires a second meaningful unknown-aware domain. Its tests must keep
distinct explanatory lineage outside semantic lattice equality while reproducing that lineage canonically in product
replay. New opcodes or method families enter only through a scenario-derived compiler fixture and its complete
dependency closure; opcode counts and percentage targets do not define readiness. Separately landed malformed
corpus/worker prototypes and cybersecurity validation are outside W1–W4; representative private-production
measurement remains a separate non-gating readiness question.
