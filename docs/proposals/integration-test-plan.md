# Executable Evidence Plan

**Lifecycle:** Current verification plan
**Roadmap relation:** Active
**Last reset:** 2026-07-14

## Purpose

Tests in this repository answer specific architectural questions. They do not stand in for product completeness. Every important assertion states whether its input came from dump memory, runtime structure decoded by ClrMD, a whole-file-identified disk oracle, explicit fixture state, or interpreter policy.

The current plan has three execution lanes:

- a fast, dump-free semantics lane for every change;
- a supported Windows lane that generates real full dumps and exercises the evidence boundary; and
- an independent Windows x64 external-worker prototype-regression lane outside W1.

The Windows lane now also evaluates the first W2 product grammar over that evidence. Parser/admission negatives remain
in the fast category even though they live with integration tests because they require no DAC, process, dump, clock,
or network. The worker lane retains a locally passing malformed-artifact checkpoint, but it is non-gating prototype
work and no additional cybersecurity validation belongs to W1.

## Fast semantics lane

The fast test project validates the code that should not depend on a DAC, child process, clock, network, or external artifact.

Current proof obligations are:

- the concrete domain satisfies bottom/top, order, join, meet, and widening laws, including distinct unknown origins;
- persistent memory forks and subsequent stores cannot mutate an earlier snapshot;
- constants, arguments, pre-seeded locals, `add`, `sub`, `mul`, and value/void `ret` execute with CoreCLR-compatible results for the admitted slice;
- compiler-emitted straight-line methods agree with live CoreCLR results across a small differential corpus;
- whole-body admission rejects unsupported suffixes, EH, malformed/truncated instructions, invalid slot/stack shapes, injected resume state, nested frames, and offsets inside operands before consuming budget or emitting events;
- identical normalized inputs produce a byte-identical canonical transcript;
- module and method handles remain identical when the same PE is copied to a different path;
- module and method handles differ when IL bytes change even if metadata/MVID, PE timestamp, and image size are deliberately preserved, because complete-artifact length/SHA-256 participates in disk-backed identity;
- metadata not-found, ambiguity, malformed content, and module mismatch remain distinguishable.

These tests may use fixed IL where a C# compiler cannot conveniently emit the intended malformed or local-slot shape. Differential claims use actual compiler-emitted method bodies and invoke the same methods live on CoreCLR.

## Real dump-evidence lane

`tests/Interpreter.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs` generates one full dump from `Interpreter.TestTarget`. The target keeps one object alive through a strong GCHandle; that object contains:

- a known `Int32` marker;
- a known non-null string;
- a null optional string;
- a one-instruction `Program.RetOnly` method; and
- a compiler-emitted fat method with locals and two EH regions.

The dump test must prove all of the following in one bounded run:

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
15. W2 evaluates marker/string/exact-null-field/coalescing queries, rejects null-conditional syntax, refuses missing and unsupported evidence, preserves partial strings without erroneous coalescing, and produces deterministic multi-axis replay fingerprints.
16. Deterministic admission rejects dumps above 8 GiB before hashing/ClrMD parsing and managed PEs above 512 MiB before SRM parsing; ClrMD's dump cache is capped at 256 MiB with stack-trace/root caching disabled.
17. Every dump-query outcome carries evidence source, explicit snapshot/module identity availability, explicit fallback,
    and only the deterministic bounds whose guarded operations were actually reached; canonical replay changes when
    this context changes and never fabricates a module or scan bound for an unavailable root. Reserved-name
    collisions, a missing field, and a foreign-snapshot root prove the distinct path-sensitive cases.
18. Generic field projection does not turn a retained partial `ClrmdInt32FieldObservation` wrapper into a decoded scalar
    answer. The wrapper remains explanatory evidence, while answer completeness is `None`.
19. Deterministic replay closes and reopens the same dump in a fresh session, rediscovers the module and root, evaluates
    the query again, and compares both the complete canonical result bytes and their SHA-256 fingerprint.

A separate dump test generates an optimized Release modeled-incident target, retains `this`, argument, local, static,
and strong-root axes, and emits a canonical report with raw member bytes at 5/5, attributable context at 1/5, and
product-query availability at 1/5. Those counts describe one generated dump, not representative private-production
recoverability. The generated report is W1 context evidence; representative production measurement is not a W1 gate.

## Non-gating malformed-artifact corpus

`HostileArtifactCorpus` generates a versioned, deterministic, payload-free manifest for bounded minidump mutations:
all 0–31-byte header truncations, bounded garbage, invalid signature/version, directory overflow/overlap,
`MemoryList`/`Memory64List` truncation, bounded header/directory bit flips, appended junk, and a sparse file just above
the 8 GiB admission boundary. Fast tests prove stable names/order/bytes, coverage, and case/count/size ceilings.

This already-landed corpus is retained as a non-gating prototype outside W1. Its future scope is not an active W1 test
decision.

## Non-gating one-shot external-worker lane

The implemented Windows x64 broker/runner projects occupy real solution boundaries and have their own headless test
project. Its four-test package, including a real malformed-artifact process checkpoint, passed locally at
`9fcf00934`. The projects and regression lane are retained as non-gating prototype work outside W1; their presence does
not admit an external artifact product surface.

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
| Evaluation evidence context | Explicit dump snapshot/module identities, evaluator source/fallback policy, and only bounds whose guarded operations were reached | That an unavailable module/root was recovered or that an unapplied limit constrained the result |
| Generic partial-field projection | Retained typed wrapper, provenance, issue code, and `None` answer completeness | A decoded scalar value |
| Fresh-session replay | Complete canonical result bytes and SHA-256 after dump close/reopen and module/root rediscovery | Historical process replay or equivalence across different snapshots |
| Malformed corpus fast result | Deterministic generated mutation bytes and in-process admission outcome | Any W1 completion requirement; this corpus is non-gating prototype work |
| Worker prototype checkpoint | One locally verified malformed-artifact process result | Any W1 completion requirement or external product admission |
| Modeled optimized report | One generated optimized Release full dump with five predeclared axes | Representative private-production context recoverability or a readiness percentage |
| Live result in differential tests | CoreCLR invocation in the test process | Dump recoverability |
| Interpreter result | Explicit frame, policy, domain, memory, and admitted body | Historical replay or product-level expression evaluation |

## CI policy

Every managed restore/build/test step uses `./eng/Invoke-HeadlessProcess.ps1 dotnet ...`; repository policy rejects raw
workflow `dotnet` launches. The workflow uses the pinned .NET 10 SDK and locked packages, runs repository-owned
local-Markdown-link and headless-workflow consistency checks, builds Release with warnings as errors, runs the fast suite,
and then runs the required ordinary-dump and optimized-context Windows lanes. The worker projects remain build-checked
through the solution, but their tests are outside the default W1 workflow. Third-party actions are pinned to verified
release commit SHAs. A missing DAC or inability to write/load a required W1 dump is a failing infrastructure signal,
not a passing skip.

That workflow passed service-side for exact pushed completion commit
`3ece32a36eccc06a61025b1b35b58c09f6e4ed09` in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT). Documentation consistency passed; the build/fast job passed locked restore,
a zero-warning Release build, 60 semantic/differential tests, and 40 fast adapter/harness tests; and the dependent
Windows job passed 3/3 required dump tests.

That service run is the historical W0 baseline only. It does not contain the later result-context, optimized
modeled-incident, headless-policy, topology, or external-worker packages. Do not call the later W1 packages CI-enforced until a
successful hosted run records their exact pushed commit; the external-worker package remains non-gating for W1.

[GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`: documentation/headless consistency, the 15-project Release build and fast
suites, ordinary real-dump evidence, and optimized-context evidence.

Current local verification on 2026-07-14 passed locked restore; a strict 15-project Release build with 0 warnings and
0 errors; 64/64 `Interpreter.Tests`; 63/63 `Category=Fast` integration tests; 3/3 ordinary dump tests; and 1/1 modeled
optimized-context test. Every managed command used `Invoke-HeadlessProcess`; there were no skips or UI.

No workflow uploads dumps, target output, heap values, paths, or expression results. The generated target contains only non-sensitive fixture data and all dumps remain temporary.

## Next evidence gates

The generated-fixture W1–W2 path, foreign-snapshot rejection, Normal-vs-Full sparse-memory case, explicit replayable
result context, path-accurate bounds, honest no-answer projection, fresh-session replay, headless workflow guard, and
optimized modeled-incident report are implemented in-tree.
W1 is complete for its revised scope. The malformed corpus and external worker are separately landed, non-gating
prototypes; representative private-production measurement is a later product-readiness question. W3 expands the
differential corpus only from scenario-derived compiler IL; opcode counts or percentage targets do not define readiness.
