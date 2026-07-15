# Testing Strategy

> **Current delivery policy (2026-07).** Tests follow the active W0–W4 dump-evaluator roadmap. The repository must distinguish checked-in test code, a locally verified result, a CI-enforced gate, and a research proposal. The broad abstract-analysis, virtual-stepping, dynamic, async, and performance matrices are deferred until their entry gates pass.

## Status

Current for active delivery. Research suites are collected separately in section 9.

The W4 counterfactual-method contract is admitted and active design, but no W4 implementation, checked-in scenario
test, local verification result, or CI-enforced W4 gate exists yet. Section 8 therefore states required evidence, not
current capability.

## 1) Evidence language

Use these terms consistently in test plans, traceability tables, and PR descriptions:

| Term | Meaning |
|---|---|
| **Designed** | A document specifies intended behavior. No implementation or validation is implied. |
| **Checked in** | Test or harness code exists in the repository. It may still be platform-limited or failing. |
| **Verified** | The named command ran successfully in a stated environment and the result is available. |
| **CI-enforced** | A checked-in workflow runs the gate and reports failure to the change. |
| **Required** | A roadmap exit criterion; it is not a current capability claim. |
| **Research** | Deferred work whose entry gate has not passed. |

A planned test is not validation. A test name without an execution result proves only that the artifact is checked in.

### Headless execution policy

Every repository-owned managed restore, build, and test launch uses
`./eng/Invoke-HeadlessProcess.ps1 dotnet ...`. The wrapper establishes .NET no-GUI policy and the Windows error-mode
mask before starting the child on Windows; a module initializer linked into every managed test assembly reasserts Win32, thread,
WER, and .NET no-dialog state inside the test process. Child targets and the external worker additionally launch with
hidden/no-window process policy. A raw `dotnet test` command is not an approved unattended test entry point.

## 2) Present executable evidence

### Fast concrete, admission, determinism, and metadata proofs

`tests/Interpreter.Tests` is dump-free. Its checked-in corpus covers:

1. lifted-flat concrete-domain order, join, meet, widening, canonical unknowns, and redacted display;
2. persistent allocated/imported object and array snapshot/fork isolation, deterministic allocation, bounded arrays,
   stable content hashes, allocated defaults, and unavailable absent imported fields;
3. SRM-derived structural module/type/method/field identities plus atomic body/signature/local projection for static and
   instance methods, `void`/`Int32` returns, initialized `Int32` locals, and directly declared instance FieldDefs;
4. metadata-derived root activation and a typed, whole-body-admitted E1 arithmetic profile covering compact, short,
   and long argument/local encodings, plus an E2 direct or constant-adjusted `Int32` instance getter whose receiver
   must use the one-byte compact `ldarg.0`; equivalent short `ldarg.s 0` and long `ldarg 0` receiver encodings are
   negative admission cases;
5. fail-closed behavior for unsupported signatures, fields, suffixes, EH, malformed/truncated IL, invalid
   slots/stack/type shape, injected offsets/state, nested frames, decorated or multiple-field getters, oversized
   bodies/`maxstack`, and exhausted budget before any forbidden transfer;
6. exact/non-exact/exceptional memory-load behavior, one-load `ldfld`, truthful capability-failure origin, and an
   idempotent terminal null-receiver outcome with exact budget and event assertions;
7. byte-identical canonical outcomes for repeated normalized inputs and for fresh metadata/resolver/machine/memory
   reconstruction; and
8. compiler-emitted arithmetic and getter methods invoked on CoreCLR and interpreted through the same value-domain and
   memory handlers, including unchecked overflow and null-receiver outcomes.

The differential harness proves the two closed, branchless, EH-free W3 profiles only. It is not evidence for calls,
branches, handlers, arbitrary signatures, inherited/static fields, or an unknown-aware domain.

### Real dump-memory proof

The ordinary real-dump suite uses `DumpMemoryEvidenceIntegrationTests.cs` for W1/W2 evidence and
`W3DumpGetterExecutionIntegrationTests.cs` for the prepared E2 execution proof. Together they generate full Windows
process dumps and:

1. retains one read-only dump stream for SHA-256 identity and ClrMD session lifetime;
2. discovers the runtime module and reads its metadata root from dump memory;
3. decodes the dump metadata-root identity and validates its MVID, exact metadata length, and metadata SHA-256 against an independently opened SRM artifact, whose separate whole-file length/SHA-256 identity is also retained;
4. discovers a strong GCHandle under an explicit scan cap, validates its slot pointer and the selected object's method-table header through counted raw-memory reads, and reads an `Int32`, a bounded string prefix, and a null string through counted evidence;
5. distinguishes exact, partial, unavailable, and conflict outcomes with stable issue codes;
6. reads the `Program.RetOnly` MethodDef RVA from counted dump metadata, decodes its complete tiny header and code from counted dump memory, and executes only the normalized exact dump-backed body;
7. independently proves a compiler-emitted fat method body from counted dump metadata/memory, including its 12-byte header, code, padding, local-signature token, and two declared EH regions; the disk body remains an equality oracle and supplies no constructor input;
8. carries evidence source, explicit snapshot/module identity availability, fallback, and only bounds whose guarded operation was actually reached through every query result and canonical replay;
9. disposes and reopens the same dump, rediscovers the module/root, and reproduces complete canonical result bytes and their SHA-256 fingerprint; and
10. verifies deterministic scan/instruction budgets, cleanup, disposal, and invalid-address behavior; and
11. projects an E2 getter's method/signature/FieldDef from the same counted dump metadata/body evidence, proves the
    admitted `ldfld` operand names the correlated runtime field, imports only an exact four-byte `Int32`, executes the
    direct and adjusted getter through `IlMachine`/`IMemoryModel`, and repeats the canonical prepared-execution result
    after dump close/reopen/module-root-method-field rebind. The disk PE is only a post-acquisition equality oracle.

These fixtures do not prove arbitrary root/frame recovery, chained expression binding, broad IL semantics, or debugger
stepping. They do prove the common result envelope, the first bounded root-field query surface, fully dump-sourced tiny
and fat bodies, and one exact counted-dump E2 getter family executed through the real persistent-memory seam. Broader
malformed/chained-section shapes remain fast parser evidence. A separate Normal-vs-Full fixture proves that an omitted
page remains partial/unavailable rather than being zero-filled.

### Restricted dump-query proof

`Interpreter.Product.DumpQuery` is exercised both without a dump and through the generated full-dump scenario. The
fast corpus admits exactly one exact non-null ordinal root, `.`, one field, and optional bounded literal coalescing,
while rejecting `?.`, calls, indexing, chaining, arithmetic, oversized inputs, malformed literals, and unsupported
escapes with stable payload-safe codes. Preparation consumes a typed, snapshot-bound root result and selects the outer
field once into an immutable object-specific plan; evaluation reads only through that frozen descriptor. Exact
absence, bounded partial search, ambiguity, invalid evidence, and a foreign snapshot remain distinct and never expose
a retained partial candidate as an exact root.

The versioned `w2-root-field-v1` real-dump corpus contains 22 cases spanning 20 distinct expression texts. It covers
direct `Int32`, direct and coalesced `Nullable<Int32>`, exact and partial strings, exact null, selected and unselected
fallbacks, `?? null`, bounded root
search, `?.` rejection, missing/wrong-case members, unsupported types, incompatible coalescing, invalid syntax, and
root-name mismatch. Every case compares its complete canonical result byte sequence/result SHA-256 and, for the 13
cases whose preparation succeeds, the canonical plan projection string/plan SHA-256 twice in one session and again
after closing, reopening, rediscovering,
and rebinding the same dump. Distinct unpaired UTF-16 fallback literals prove plan hashing is injective even when the
fallback is not selected and the returned value is identical. Exact root-selection provenance retains the ordinal
selector, search disposition, issue, counters, caps, and retained-match state. Missing or partial evidence is never
reclassified as null merely to apply `??`; generic partial primitive wrappers retain explanation without becoming
decoded scalar answers.

### Concrete W3 execution proof

Exact hardened implementation commit `19c292f9f` closes the in-tree and local-execution portion of the
[normative W3 contract](concrete-il-execution-contract-proposal.md). It contains `+8,842/-1,650` hand-written LOC
(`+5,362/-928` production and `+3,480/-722` tests) plus 39 generated lock-file additions. The implementation replaces
caller-shaped/display-name execution with structural metadata identities, atomic resolution, metadata-derived
activation, a frozen typed whole-body plan, injected persistent memory, and a latched structured target-null terminal
outcome.

The W3 corpus spans structural/SRM projection, negative admission, domain/memory laws, direct and adjusted getters,
compiler/CoreCLR arithmetic/overflow/getter/null differential comparison, same/fresh-session replay, and generated
counted-dump import/execution/reopen replay. Tests assert resolver and memory call counts, exact budget deltas, ordered
events, failure-state preservation, and emitted compiler opcode shapes—not just final values. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs
at that exact implementation checkpoint. Exact documentation-closure commit `de6cea124` then passed all four required
jobs in [run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), so W3 is formally
closed for its defined non-cybersecurity scope.

### Corrupt-backend normalization and non-gating malformed-input corpus

Backend memory-read exceptions are normalized into typed invalid evidence rather than escaping as incidental
exceptions; this is part of W1's deterministic malformed-evidence behavior.

Separately, the versioned malformed-minidump corpus deterministically covers every 0–31-byte header truncation, bounded garbage,
signature/version failures, stream-directory overflow/overlap, `MemoryList`/`Memory64List` truncation, bounded header
and directory bit flips, appended junk, and a sparse artifact just above the 8 GiB admission limit. Its canonical
manifest and hard case/count/size caps have fast tests. This corpus is retained as non-gating prototype work outside
W1, W2, and W3. Its five facts are tagged `Scope=Cybersecurity` and excluded from every current milestone test
invocation; they provide no W1/W2/W3 validation.

### One-shot external-worker proof

`tests/Interpreter.Host.ExternalWorker.Tests` exercises the separately landed Windows x64 broker/runner prototype. Its
four-test package, including one real malformed-artifact process boundary, passed locally at checkpoint `9fcf00934`
under the headless wrapper. The worker remains non-gating prototype work outside W1–W4; its presence and test
result do not admit an external artifact product surface. Its test project is not invoked by the current milestone
workflow.

### Optimized modeled-incident measurement

One generated optimized Release full dump keeps predeclared `this`, argument, local, static, and strong-root probes in
a versioned canonical v1 report. It records raw member bytes at 5/5, attributable context at 1/5, and product-query
availability at 1/5. Unavailable cases remain in every denominator; the report contains no percentage. Stack-slot
observation is deliberately not admitted under the pinned .NET 10 DAC safety boundary, static attribution remains
unavailable, and the exact attributable/queryable case is the strong root. This is W1 generated-context evidence, not
a representative private-production corpus or a production recoverability rate; representative production measurement
is not a W1 gate.

### W0 signal status

| Signal | In-tree evidence | Service-side evidence / remaining distinction |
|---|---|---|
| Repository build | Stable .NET 10.0.2xx feature-band/minimum-patch pin, central versions, committed lock files, deterministic Release build, warnings-as-errors under `CI=true`. | `CI-enforced` for exact completion commit `3ece32a36eccc06a61025b1b35b58c09f6e4ed09`: locked restore and the zero-warning Release build passed in GitHub run 29309374548. |
| Fast tests | Unit/domain/admission/differential/determinism/metadata suite plus payload-safe harness start/readiness failure coverage is checked in. | The same run passed 60 semantic/differential tests and 40 fast adapter/harness tests. |
| Dump integration | Required Windows dump category and a bounded target/dump harness are checked in. | The dependent Windows job passed 3/3 dump tests. An inability to create/load the dump remains a failure, not a passing skip. |
| Determinism | Canonical UTF-8 machine transcripts and multi-axis W1/W2 result envelopes carry explicit replayable evidence context. W3's successful prepared-execution test projection separately retains owner-evidence identity, structural method/field facts, imported memory, resolver/load counts, state, budget, events, and return outcome, then serializes those documented observables canonically. The same dump reopened in a fresh session reproduces module/root selection, W2 result/plan bytes, and W3 successful prepared-execution identity/transcript/fingerprint. Target-null idempotence and CoreCLR agreement are asserted separately rather than claimed as a fresh-session canonical transcript. | W1 passed at exact closure commit `e2580a8a8` in run 29353198889; W3's expanded replay passed at hardened implementation checkpoint `19c292f9f` and formally closed at exact documentation commit `de6cea124` in [run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). |
| Documentation truth | The evidence matrix distinguishes raw dump bytes, dump metadata-derived facts, ClrMD-decoded runtime structures, whole-file-identified disk oracle facts, and explicit fixture inputs. `eng/verify-markdown-links.ps1` validates repository-local inline/reference destinations with stable file/line diagnostics. | The dedicated documentation-consistency job passed on the exact completion commit. Keep the evidence matrix synchronized whenever an evidence fallback changes. |

The workflow in `.github/workflows/ci.yml` is checked in and has reported successful exact-commit runs, recorded below.
`CI-enforced` applies only to the gates that the successful workflow actually executed. The exact W1 closure commit is
green, but its historical fast totals predate the explicit scope filter and must not be retroactively described as a
filtered cybersecurity result. The current workflow formalizes the exclusion with `Scope!=Cybersecurity` on all four
test commands. Repository-wide restore/build still compiles all 15 projects as topology/compilation-health evidence,
not cybersecurity behavioral evidence. Representative private-production measurement remains a separate
product-readiness question.

### Local verification record — 2026-07-13

On Windows with the SDK selected by `global.json`:

The result column is the historical record. The command column uses the current approved headless equivalent; it does
not imply that the later wrapper existed when the original W0 run was recorded.

| Gate | Command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode --disable-parallel --disable-build-servers` | Passed. |
| Full prototype build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln -c Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Passed, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj -c Release --no-build --no-restore --filter "Scope!=Cybersecurity"` | Passed, 60/60. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj -c Release --no-build --no-restore --filter "Category=Fast&Scope!=Cybersecurity"` | Passed, 40/40. |
| Real dump evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1&Scope!=Cybersecurity"` | Passed, 3/3 on the W0 completion tree; the earlier reset tree also passed three consecutive runs (9/9 executions). |

This table records local verification only; the independent service-side evidence follows.

### Service-side verification record — 2026-07-14 UTC (2026-07-13 PDT)

[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548)
executed exact pushed completion commit `3ece32a36eccc06a61025b1b35b58c09f6e4ed09` under .NET SDK 10.0.201:

| Job | Service-side result |
|---|---|
| Documentation consistency | Passed: 59 authored Markdown files and all 6 repository-local destinations validated with the repository-owned verifier. |
| Build and fast tests | Passed: locked restore; Release build with 0 warnings / 0 errors; 60/60 semantic and differential tests; 40/40 fast adapter/harness tests. |
| Real dump evidence | Passed after the fast job: locked restore; Release build with 0 warnings / 0 errors; 3/3 required Windows dump tests. |

This closes the W0 service-side documentation/build/fast/dump evidence distinction. It does not broaden the
generated-fixture proof boundary.

### Current revised-scope service evidence — 2026-07-14

[GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889)
passed all four required jobs at exact W1 closure commit `e2580a8a8`: documentation/headless consistency; the
15-project zero-warning Release build and fast suites; ordinary real-dump evidence; and optimized-context evidence.

[GitHub Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178)
passed the same four required jobs at exact W2 closure commit `5bed47100`, with all test commands filtered by
`Scope!=Cybersecurity`.

### Historical local W1–W2 verification — 2026-07-14

Every command below ran through `./eng/Invoke-HeadlessProcess.ps1`; no test was skipped and no UI was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers /p:UseSharedCompilation=false` | Passed across 15 projects, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore --filter "Scope!=Cybersecurity"` | Passed, 64/64 at W2 implementation commit `ff7cd1965`. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast&Scope!=Cybersecurity"` | Passed, 67/67 at W2 implementation commit `ff7cd1965`. |
| Ordinary real-dump evidence | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus!=ModeledIncidentContextV1&Scope!=Cybersecurity"` | Passed, 4/4 at W2 implementation commit `ff7cd1965`, including the 22-case W2 corpus. |
| Optimized modeled-context evidence | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=ModeledIncidentContextV1&Scope!=Cybersecurity"` | Passed, 1/1 at W2 implementation commit `ff7cd1965`. |

Both W1 and W2 local results are corroborated by their exact-commit hosted closure runs above. Restore and build remain
repository-wide and compile all 15 projects as topology and compilation-health evidence. Every current test command
excludes `Scope=Cybersecurity`; the five dedicated hostile-artifact corpus facts therefore contribute neither W2
validation nor a cybersecurity claim.

### Current local W3 implementation verification — 2026-07-14

Exact hardened implementation commit `19c292f9f` passed the following local matrix. Every managed command ran through
`./eng/Invoke-HeadlessProcess.ps1`; every test filter excluded `Scope=Cybersecurity`; no test was skipped; and no UI was
displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false` | Passed across 15 projects, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore --filter "Scope!=Cybersecurity"` | Passed, 103/103. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast&Scope!=Cybersecurity"` | Passed, 67/67. |
| Ordinary real-dump evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1&Scope!=Cybersecurity"` | Passed, 5/5. |
| Optimized modeled-context evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1&Scope!=Cybersecurity"` | Passed, 1/1. |
| Focused W3 evidence | the same wrapped integration-project command with `--filter "FullyQualifiedName~W3DumpGetterExecutionIntegrationTests&Scope!=Cybersecurity"` | Passed, 2/2. This is a focused re-run/view, not two additional ordinary-dump facts. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

This verifies all behavioral portions of W3 and the implementation-checkpoint portion of its repository-wide gate.
[GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs
at the same exact pushed implementation commit. Exact documentation-closure commit `de6cea124` and [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently satisfied the
separate hosted closure requirement. The implementation checkpoint realizes `+8,842/-1,650` hand-written LOC
(`+5,362/-928` production and `+3,480/-722` tests) plus 39 generated lock-file lines.

## 3) Active test layers

### A. Fast semantic and contract tests

These run without a dump, DAC, process launch, network, clock dependence, or external artifacts.

Current and near-term subjects:

- value-domain laws for implemented domains;
- persistent-memory isolation and missing-read behavior;
- instruction-budget and operational-state transitions;
- admitted opcode semantics over synthetic method bodies;
- stable failures for missing bodies, malformed offsets, unsupported opcodes, and invalid stack shapes;
- canonical serialization/replay for implemented outcomes.

Prefer observable contracts over private implementation details. Every regression test names the invariant it protects.

### B. Adapter contract tests

These exercise one external boundary through project-owned identities and evidence results.

- SRM: metadata-root and complete-artifact identity, token lookup, independently decoded method bodies, signatures, locals, EH, malformed/truncated/oversized artifacts, and later Portable PDB data as admitted by a milestone.
- ClrMD/raw memory: snapshot identity, module discovery, bounded reads, unreadable ranges, truncation, object/type/field/string evidence, counted method metadata/header/code/extra sections, cache policy, and disposal.
- Runtime-to-artifact binding: exact match, unavailable artifact, mismatch/conflict, and deterministic candidate ordering.

Backend-specific objects must not become expected values in core tests. Assert normalized identities, evidence status, provenance, and stable miss reasons.

### C. Windows dump integration tests

These create or consume a real dump and prove an entire evidence path. They are intentionally fewer and slower than fast tests.

Rules:

1. Synchronize target startup explicitly and enforce bounded waits.
2. Kill child processes and delete temporary dumps in `finally`/disposal paths.
3. Keep target runtime, test runtime, and DAC acquisition deterministic; do not silently use network symbol acquisition.
4. Assert the origin of every important value: dump memory, runtime metadata, disk PE/PDB, or test input.
5. Treat inability to create/load a required dump as an explicit infrastructure failure, not a passing skip in the required Windows lane.
6. Clear the target environment before launch, allowlist only runtime/diagnostic necessities, and isolate its working and temporary directories; a full dump must never inherit CI/developer credentials.
7. Assert the offline locator and explicit resource policy: reject dumps above 8 GiB, cap ClrMD's dump cache at 256 MiB with stack-trace/root caching disabled, and reject managed PE artifacts above 512 MiB at the typed external `Open` boundary. These are bounds, not sandbox evidence.

### D. Product scenario tests (W2)

The checked-in `w2-root-field-v1` corpus contains 22 cases over 20 distinct expression texts. Each includes input expression, typed root evidence,
policy, preparation/plan outcome, value/evidence result, diagnostics, provenance, and canonical replay output. Every
case is repeated in-session and after fresh-session rebind; the 13 prepared plans carry an injective canonical identity.
Later syntax expands only from a scenario whose evidence and resource behavior are explicit.

### E. Concrete differential tests (W3)

For each admitted concrete opcode/method shape, run a tiny compiled fixture on CoreCLR and through the interpreter, then compare normalized value or exception outcomes. Reject unsupported bodies before execution; never treat partial execution as a differential pass.

### F. Non-gating external-worker prototype tests

The separately landed package launches a fresh worker and checks its normalized outcome and termination. Its current
malformed-artifact test is implemented and locally verified. This layer is retained as a prototype regression suite,
not a W1 exit criterion.

## 4) Fixture policy

### Small generated targets first

Prefer purpose-built, source-controlled test targets that expose one risk at a time:

- fixed primitive fields and bounded strings for W1;
- null/member/coalescing cases for W2;
- closed branchless EH-free arithmetic/getter bodies for W3;
- missing, sparse, mismatched, and malformed evidence for negative coverage.

Build settings that affect emitted IL are part of the fixture contract. Verify the emitted method body before relying on its shape.

### Artifact provenance

Each fixture records:

1. how the dump or PE/PDB was produced;
2. runtime/SDK and platform constraints;
3. expected identity;
4. evidence source for each assertion;
5. cleanup and size bounds.

Do not check in a large or secret-bearing dump merely to make CI convenient. If a binary fixture becomes necessary, generate it from non-sensitive source, document provenance, and define an explicit refresh procedure.

### Golden artifacts

Use goldens only for a stable external contract. Canonicalize ordering and identifiers before serialization. A golden update must state which semantic contract changed; bulk snapshot churn is not an explanation.

## 5) Determinism and replay

Determinism is an active product requirement, but replay coverage grows only with implemented behavior.

For each admitted scenario:

1. construct all policy and budget inputs explicitly;
2. run the scenario repeatedly, preferably in fresh processes for integration paths;
3. serialize only documented observables in a canonical order;
4. compare byte-for-byte output or a cryptographic hash;
5. fail on drift unless the field is explicitly excluded with rationale.

The W0 observable should include at least machine run status, operational state, completion/failure detail, remaining deterministic budgets, ordered events, and stable method/module identity. Paths, process IDs, addresses, elapsed time, allocation counters used as identity, and random temporary names must not leak into the canonical result.

Cancellation-token timing is a host-responsiveness concern and is not a deterministic budget oracle.

## 6) Evidence-honesty assertions

Tests for dump-backed behavior assert independent axes rather than compressing them into one “success” flag:

- semantic mode (`Observation`, `DerivedQuery`, W4 `CounterfactualExecution`, or later-research `AbstractAnalysis`);
- completion status;
- completeness (`Complete`, `Partial`, or `None`);
- evidence status (`Exact`, `Partial`, `Unavailable`, `Conflict`, or `Invalid`);
- evidence source and provenance;
- effects or virtual writes;
- diagnostics and stable miss reason.

Negative fixtures are first-class acceptance cases. Missing pages, bad addresses, truncated strings, absent or wrong disk artifacts, metadata-root mismatch, whole-file artifact changes, malformed IL, and unsupported syntax must produce deterministic typed outcomes rather than guesses or incidental exceptions.

## 7) CI shape

The pipeline targets `net10.0` and should remain small enough to diagnose:

1. locked restore and Release build;
2. fast semantic/contract tests;
3. an ordinary Windows dump-evidence lane;
4. a separate Windows optimized modeled-context lane; and
5. documentation/link and headless-workflow consistency checks with stable signal.

All four current `dotnet test` commands include `Scope!=Cybersecurity`; the external-worker test project is not
invoked. Its projects remain solution-build inputs only because restore/build stays repository-wide across all 15
projects as topology/compilation-health evidence. That compilation is not cybersecurity behavioral validation.

The historical W0 run below proves only its original jobs. A new or changed gate becomes `CI-enforced` only after a
successful hosted run names the exact pushed closure commit; checked-in workflow text or local execution alone is
insufficient.

Do not create a matrix for `fast`/`balanced`/`deep` policies, concrete/abstract/hybrid modes, or multiple operating systems until those dimensions have distinct implemented behavior and fixtures. Performance jobs become scheduled or gating only after a representative corpus and baseline exist.

## 8) Exit criteria by active milestone

### W0 — truthful baseline and fast feedback

- A clean checkout restores, builds, and runs the fast suite in CI under .NET 10.
- The required Windows lane executes the walking skeleton.
- Repeated micro-step runs produce an identical canonical observable result.
- Failures for missing body, invalid offset, unsupported opcode, budget exhaustion, and harness startup are stable and tested where the behavior exists.
- Documentation says exactly what came from the dump and what came from the on-disk PE.

### W1 — real dump-evidence slice

- A generated dump yields a known primitive field and bounded string from dump memory, without user-IL execution.
- Exact, capped-partial, unavailable, invalid-address, unreadable-memory, and identity-conflict paths have deterministic assertions.
- A normalized method body is exposed only after its MethodDef RVA, complete header, code, and declared extra sections are exact counted dump evidence; the disk PE remains an independent test oracle.
- Every result identifies snapshot/module identity, evidence source, completeness, fallback, and only the deterministic
  bounds whose operations were reached.
- A partial observation wrapper with no decoded scalar reports no answer while preserving explanatory evidence.
- Fresh-session evaluation over the same dump reproduces canonical result bytes and fingerprint.
- Reads and traversals are bounded; diagnostics use stable reason codes.
- The exact pushed W1 closure commit passes all required hosted jobs.
- Repository workflow guards reject managed restore/build/test commands that bypass the headless wrapper.

### W2 — restricted expression/query slice

- Twenty-two versioned cases spanning 20 distinct expression texts cover direct `Int32`, direct/coalesced
  `Nullable<Int32>`, exact nullable-field null/coalescing, exhaustive and partial roots, invalid syntax, unsupported
  syntax (including `?.`), and partial value evidence.
- Parse/prepare/bound-plan/query behavior is deterministic; every product result is `DerivedQuery`, while its direct
  adapter reads remain `Observation` evidence.
- Every scenario reproduces complete result bytes/fingerprint across same-session repetition and fresh-session
  close/reopen/rebind; the 13 cases whose preparation succeeds also reproduce the canonical plan projection
  string/fingerprint.
- Member access remains read-only and never silently invokes getters, reflection, or user IL.
- Exact closure commit `5bed47100` passes all required hosted jobs in [GitHub Actions run
  29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).

### W3 — concrete IL semantics and differential oracle

W3 has eleven normative executable-evidence gates:

1. Structural type, method, and field identity tests pass, including cross-module non-aliasing.
2. SRM projection tests pass for static/instance arguments, `void`/`Int32` returns, initialized locals, and FieldDefs.
3. Structured rejection tests pass for unsupported signatures, local shapes, field tokens/definitions, EH, and opcodes.
4. Activation tests prove caller counts, local values/counts, and return disposition are no longer inputs.
5. Typed whole-body admission proves a supported prefix never executes when a suffix is rejected.
6. Concrete-domain and persistent-memory laws pass, including allocated defaults and imported-field absence.
7. Direct and adjusted getters perform exactly one real memory-model load and preserve memory.
8. Compiler-emitted arithmetic, unchecked-overflow, getter, and null-receiver outcomes agree with CoreCLR.
9. Canonical outcomes and fingerprints agree in repeated and fresh metadata/resolver/machine/memory sessions; the
   dump-grounded case also agrees after dump close/reopen and complete rebind.
10. A generated real-dump E2 test executes method metadata/body and field bytes obtained from counted dump evidence,
    with the independently opened PE used only as a late oracle.
11. The repository-wide Release build and all required non-cybersecurity fast, ordinary-dump, and optimized-dump lanes
    pass headlessly with zero skips.

All eleven pass locally at hardened implementation commit `19c292f9f` with the exact matrix recorded in section 2;
[implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four required
jobs at that exact commit. Exact documentation-closure commit `de6cea124` passed all four required jobs in [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), formally closing W3. Neither
the implementation nor its validation includes `Scope=Cybersecurity`.

### W4 — unknown-aware method evaluation

The normative gate is the
[`Counterfactual Method Evaluation Contract`](counterfactual-method-evaluation-contract-proposal.md). It admits one
branchless, EH-free generated-dump scenario: `DumpProbe.GetMarkerSummary` reads the two marker fields and reaches the
direct `CombineMarkers` helper. This question is intentionally beyond W2: a W2 plan selects one field and never
executes user IL, so it cannot combine the two observations or cross the call boundary.

The future W4 implementation must satisfy all of the following before the slice is described as implemented or
verified:

1. The complete root and reachable helper bodies are admitted before instruction zero from counted dump body and
   metadata evidence; a disk PE and direct CoreCLR invocation remain independent oracles, never execution inputs.
2. Exact method, owner, and two-field evidence produces the exact `Int32` summary and agrees with CoreCLR. Every
   supported opcode and the direct-call family has concrete and differential coverage.
3. Replacing either required marker observation with an admitted partial or unavailable outcome completes with a
   typed unknown whose stable lineage identifies the field evidence and subsequent arithmetic/call transformations.
   It never substitutes zero, the other marker, or any fabricated concrete value. Conflict and invalid evidence keep
   their distinct blocked/invalid outcomes. Degraded coverage is required for every supported transfer that can
   propagate the unknown.
4. The direct helper call has deterministic frame/return behavior, pre-execution call-depth enforcement, normalized
   effects, and stable call diagnostics. Unsupported call shapes block before a misleading partial execution.
5. A separate mandatory conformance request selects the one structural pure model and proves exact/unknown agreement
   with interpretation plus blocked, invalid, capability, effect, and fallback outcomes. Tests
   assert model-attempt records, state/memory atomicity, instruction charging, and semantic-event truthfulness.
6. Retained exact typed-null `ldfld` behavior is projected as the only W4 target-exception outcome through a standalone
   dump-free conformance fragment, not a fabricated rooted request. It stops without handler transfer, preserves exact
   exception location, charges and emits the promised instruction/event outcome once, carries no request/plan identity,
   and remains idempotent after terminal latching. The non-throwing helper/model may not fabricate an exception.
7. Instruction budget is consumed only by executed instructions, call depth is proven against the frozen graph before
   activation and observed on frame entry, and preparation traversal is consumed only while discovering and freezing
   the bounded method/field plan. Exhaustion reports the exact applied bound without relabeling host cancellation.
8. Allocation is outside the admitted scenario. No dormant allocation counter may appear as an enforced guarantee;
   the result and replay projection record the allocation bound as absent/not applied until an allocation-consuming
   scenario is separately admitted.
9. The product result is explicitly `CounterfactualExecution` and exposes its named policy, assumptions, call/model
   identities, effects, evidence, applied bounds, provenance, diagnostics, and exact or unknown outcome without
   implying that either method historically ran in the target process.
10. Repeated evaluation in one session and fresh-object reconstruction reproduce canonical request, plan, result bytes,
    and fingerprints for exact, degraded, structural/effect blocked, budget, and modeled requests. The target-exception
    conformance case reproduces its standalone fragment while asserting request/plan identities absent; every
    dump-grounded case additionally reproduces its artifacts after complete dump close/reopen/rebind.
11. The repository-wide Release build and required fast, ordinary-dump, optimized-dump, and focused W4 lanes pass
    headlessly with zero skips under the non-cybersecurity test selection at the exact pushed commit.

Branches and path forks, CFG state merge/fixpoint/widening, loops, handler-transfer EH, virtual or generic dispatch,
broad intrinsic/model catalogs, allocation, async/dynamic lifting, and virtual stepping remain outside this gate. A
later research proposal or existing scaffold does not count as W4 evidence.

## 9) Deferred research suites

The following are not active CI commitments:

- branch/path-fork behavior and CFG merge/fixpoint convergence or widening properties;
- CN-T, range, taint, and multi-domain precision scorecards;
- virtual Step Into/Over/Out, undo, branch history, and debug-map transcripts;
- dynamic-dispatch candidate traces and DLR outcome matrices;
- async virtual schedulers, task lifecycle traces, and source-level async framing;
- broad BCL model/semantic-registry conformance;
- large-corpus fuzzing, allocation ceilings, percentile SLAs, and cross-platform matrices.

Promote one only when its roadmap entry gate passes and a concrete fixture demonstrates why it is needed. Promotion requires moving the relevant assertions into an active test layer and updating the traceability map; a research document alone is insufficient.

## 10) Failure triage

Classify failures as one of:

- `SemanticRegression`
- `EvidenceRegression`
- `DeterminismRegression`
- `ContractRegression`
- `SecurityOrBoundsRegression`
- `PerformanceRegression` (only after a baseline is approved)
- `InfrastructureFailure`

Record the failing fixture/test, exact command and environment, expected versus actual normalized outcome, evidence source, and whether a host-visible contract changed.

## 11) Open decisions

1. Which representative private-production optimized incident corpus can supply an honest root/frame-context denominator?
2. What corpus composition would justify setting a recoverability readiness threshold without hiding unavailable cases?

Both are post-W1 product-readiness questions. External-input cybersecurity is separately scoped and is not an open W1,
W2, or W3 testing decision.
