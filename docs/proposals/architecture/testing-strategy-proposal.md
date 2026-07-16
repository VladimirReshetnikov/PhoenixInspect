# Testing Strategy

> **Current delivery policy (2026-07).** W0–W4 are the closed dump-evaluator evidence baseline; tests for active W5
> follow the [`Post-W4 Path Forward`](../../plans/post-w4-path-forward.md). The repository must distinguish checked-in
> test code, a locally verified result, a CI-enforced gate, and a research proposal. The broad abstract-analysis,
> virtual-stepping, dynamic, async, and performance matrices are deferred until their entry gates pass.

## Status

Current for active delivery. Research suites are collected separately in section 9.

The W4 counterfactual-method contract is closed. W4.1's checked-in fixture gate landed at pushed
checkpoint `82363585b`, W4.2's unknown-aware E1/E2 kernel at `e89e43498`, W4.3's backend-neutral structured
field-evidence continuation at `7479b1ad4`, and W4.4's body-independent direct-MethodDef resolution plus complete
frozen graph preparation at `2e596c117`/`742ef2c4f`. Exact W4.5a commit `356c07037` now activates that graph and
executes exact direct `call`/`ret` frames with deterministic return sites, depth accounting, and frame events. W4.5b
commit `c72f6ee9e5545240433294cdca4f350808339aef` closes the dump-free execution kernel by propagating canonical
explained-unknown argument and return lineage through the same prepared call. These are prerequisite W4 implementation
checkpoints. W4.6a commit `77c92789b16d9258c907d5026a36e39f8c957b41` adds exact structural pure-model
selection and opaque body-free leaves while deliberately blocking model execution. W4.6b commit `fd723a912` adds
atomic modeled-return lineage construction. W4.6c commit `877c9fb55` executes only the frozen capability with atomic
return transfer and attempt/depth evidence; W4.6d commit `da5346813` closes compiler/SRM exact/degraded/fresh-session
conformance. W4.7a `2e70fe76d` adds standalone issuer-certified target-outcome projection, and W4.7b `dad6a6dd4`
adds compiler/SRM fresh replay plus capability poison/count evidence. W4.8 checkpoints through `44b050ec8` add the
canonical rooted product runner, and W4.9 checkpoints through `a8b5f32f0` add detached ClrMD exact/degraded
generated-dump execution plus close/reopen replay. Exact W4 implementation closure passed in hosted run 29463426083;
final documentation closure passed the same matrix in run 29463847230.

W5 preserves all of those gates while adding focused expression classification, acquisition-facade, mode-preserving
composition, headless consumer, canonical-corpus, and usefulness evidence. W5.1–W5.5b are implemented through pushed
checkpoints `7c3d52572`/`d88b13c2c`/`fc8a43a7a`/`59d9bb590`/`0f5230e13`/`b788f4f66`/`90ade6d92`. The generated lane
validates deterministic raw-count reporting and corpus-kind enforcement. The meaningful synthetic lane supplies 12
independent incidents across two shapes and selects fixed-depth member navigation for the next prototype design. Both
lanes contribute zero representative/external-observation rows. W5 umbrella closure verification remains open; no
unimplemented research matrix is added in advance.

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
WER, and .NET no-dialog state inside the test process. Child targets additionally launch with hidden/no-window process
policy. A raw `dotnet test` command is not an approved unattended test entry point.

## 2) Present executable evidence

### Fast concrete, admission, determinism, and metadata proofs

`tests/Interpreter.Tests` is dump-free. Its checked-in corpus covers:

1. lifted-flat concrete-domain order, join, meet, widening, canonical unknowns, and value-omitting display;
2. provenance-aware lifted-flat laws whose semantic equality/hash/order ignore explanation, plus versioned
   content-addressed `InputOrigin`, ordered `BinaryTransform`, and structured `FieldLoadTransform` lineage, interning,
   capture, and fresh-object replay;
3. persistent allocated/imported object and array snapshot/fork isolation, deterministic allocation, bounded arrays,
   stable content hashes, allocated defaults, and unavailable absent imported fields;
4. SRM-derived structural module/type/method/field identities plus atomic body/signature/local projection for static and
   instance methods, `void`/`Int32` returns, initialized `Int32` locals, and directly declared instance FieldDefs;
5. metadata-derived root activation and a typed, whole-body-admitted E1 arithmetic profile covering compact, short,
   and long argument/local encodings, plus an E2 direct or constant-adjusted `Int32` instance getter whose receiver
   must use the one-byte compact `ldarg.0`; equivalent short `ldarg.s 0` and long `ldarg 0` receiver encodings are
   negative admission cases;
6. fail-closed behavior for unsupported signatures, fields, suffixes, EH, malformed/truncated IL, invalid
   slots/stack/type shape, injected offsets/state, nested frames, decorated or multiple-field getters, oversized
   bodies/`maxstack`, and exhausted budget before any forbidden transfer;
7. immutable structured exact/non-exact/exceptional memory-load evidence; one-load `ldfld`; policy-gated partial and
   unavailable continuation; truthful precision-loss events and atomic capability-failure normalization; and an
   idempotent terminal null-receiver outcome with exact budget, state, and event assertions;
8. byte-identical canonical outcomes for repeated normalized inputs and for fresh metadata/resolver/machine/memory
   reconstruction; and
9. compiler-emitted arithmetic and getter methods invoked on CoreCLR and interpreted through the same value-domain and
   memory handlers, including unchecked overflow and null-receiver outcomes; and
10. body-independent same-module direct-MethodDef resolution; exact managed-IL call signatures; deterministic rooted,
     acyclic graph preparation; shared-callee deduplication; definition/signature correlation; canonical nodes, fields,
     and edges; required logical depth; fixed internal method/traversal resource limits; and no-partial-plan failures; and
11. exact prepared-graph activation and direct `call`/`ret` frame execution; canonical call and return sites; configured,
     required, observed, and active-frame logical-depth facts; deterministic instruction/frame-event ordering; unchanged
     memory; terminal replay validation; insufficient-depth and malformed-state rejection; and no metadata re-resolution;
     and
12. optional explained-unknown call-lineage transformation over a complete metadata-ordered argument vector and the
    returned value; canonical parameter-indexed `CallArgumentTransform` and `InterpretedReturnTransform` nodes; atomic
    missing/failing/invalid capability outcomes; legacy-byte compatibility; reachable-DAG capture; and validated
    same-session and fresh-session replay.
13. W4.7 complete same-machine IL-zero-to-null transition certification, stable failure taxonomy, exact latch/
    accounting/event validation, fixed schema-v1 bytes/SHA, absent synthetic reachability, optional idempotent re-step,
    direct/adjusted compiler getter replay, and poison/count proof of no re-step capability access or repeated load.

The differential harness proves the two closed, branchless, EH-free W3 profiles only. W4.2 separately demonstrates
that a second meaningful domain reuses those opcode handlers for exact and explained-unknown values. W4.3 separately
demonstrates structured partial/unavailable field continuation and `FieldLoadTransform` over those same closed
handlers. W4.4 admits the exact direct `call` only into an immutable preparation graph; the legacy `IlMachine` remains
call-free. W4.5a executes exact values through an explicitly activated prepared-graph path, and W4.5b propagates
explained-unknown arguments and returns through an optional value-domain capability without changing exact semantics.
None is evidence for modeled calls, branches, handlers, arbitrary signatures, inherited/static fields, a ClrMD
field-evidence producer, or a dump-grounded W4 result.

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
escapes with stable payload-omitting codes. Preparation consumes a typed, snapshot-bound root result and selects the outer
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

Exact strengthened implementation commit `19c292f9f` closes the in-tree and local-execution portion of the
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
closed for its defined milestone-selected scope.

### Backend-failure normalization

Backend memory-read exceptions are normalized into typed invalid evidence rather than escaping as incidental
exceptions. Caveat: this validates only the enumerated backend failures and named fixture shapes. Earlier
out-of-scope experiments have been removed.

### Optimized modeled-incident measurement

One generated optimized Release full dump keeps predeclared `this`, argument, local, static, and strong-root probes in
a versioned canonical v1 report. It records raw member bytes at 5/5, attributable context at 1/5, and product-query
availability at 1/5. Unavailable cases remain in every denominator; the report contains no percentage. Stack-slot
observation is deliberately not admitted under the pinned .NET 10 DAC support boundary, static attribution remains
unavailable, and the exact attributable/queryable case is the strong root. This is W1 generated-context evidence, not
a representative private-production corpus or a production recoverability rate; representative production measurement
is not a W1 gate.

### W0 signal status

| Signal | In-tree evidence | Service-side evidence / remaining distinction |
|---|---|---|
| Repository build | Stable .NET 10.0.2xx feature-band/minimum-patch pin, central versions, committed lock files, deterministic Release build, warnings-as-errors under `CI=true`. | `CI-enforced` for exact completion commit `3ece32a36eccc06a61025b1b35b58c09f6e4ed09`: locked restore and the zero-warning Release build passed in GitHub run 29309374548. |
| Fast tests | Unit/domain/admission/differential/determinism/metadata suite plus artifact-text-free harness start/readiness failure coverage is checked in. | The same run passed 60 semantic/differential tests and 40 fast adapter/harness tests. |
| Dump integration | Required Windows dump category and a bounded target/dump harness are checked in. | The dependent Windows job passed 3/3 dump tests. An inability to create/load the dump remains a failure, not a passing skip. |
| Determinism | Canonical UTF-8 machine transcripts and multi-axis W1/W2 result envelopes carry explicit replayable evidence context. W3's successful prepared-execution test projection separately retains owner-evidence identity, structural method/field facts, imported memory, resolver/load counts, state, budget, events, and return outcome, then serializes those documented observables canonically. The same dump reopened in a fresh session reproduces module/root selection, W2 result/plan bytes, and W3 successful prepared-execution identity/transcript/fingerprint. Target-null idempotence and CoreCLR agreement are asserted separately rather than claimed as a fresh-session canonical transcript. | W1 passed at exact closure commit `e2580a8a8` in run 29353198889; W3's expanded replay passed at strengthened implementation checkpoint `19c292f9f` and formally closed at exact documentation commit `de6cea124` in [run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). |
| Documentation truth | The evidence matrix distinguishes raw dump bytes, dump metadata-derived facts, ClrMD-decoded runtime structures, whole-file-identified disk oracle facts, and explicit fixture inputs. `eng/verify-markdown-links.ps1` validates repository-local inline/reference destinations with stable file/line diagnostics. | The dedicated documentation-consistency job passed on the exact completion commit. Keep the evidence matrix synchronized whenever an evidence fallback changes. |

The workflow in `.github/workflows/ci.yml` is checked in and has reported successful exact-commit runs, recorded below.
`CI-enforced` applies only to the gates that the successful workflow actually executed. The exact W1 closure commit is
green, but its historical fast totals describe only the milestone-selected set at that commit. The current workflow
runs every remaining test in each selected category and builds all 13 current projects. Caveat: this is not evidence
beyond the named fixture shapes. Representative private-production measurement remains a separate product-readiness
question.

### Local verification record — 2026-07-13

On Windows with the SDK selected by `global.json`:

The result column is the historical record. The command column uses the current approved headless equivalent; it does
not imply that the later wrapper existed when the original W0 run was recorded.

| Gate | Command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode --disable-parallel --disable-build-servers` | Passed. |
| Full prototype build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln -c Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false` | Passed, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj -c Release --no-build --no-restore ` | Passed, 60/60. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj -c Release --no-build --no-restore --filter "Category=Fast"` | Passed, 40/40. |
| Real dump evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 3/3 on the W0 completion tree; the earlier reset tree also passed three consecutive runs (9/9 executions). |

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
the milestone test selection.

### Historical local W1–W2 verification — 2026-07-14

Every command below ran through `./eng/Invoke-HeadlessProcess.ps1`; no test was skipped and no UI was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers /p:UseSharedCompilation=false` | Passed across 15 projects, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 64/64 at W2 implementation commit `ff7cd1965`. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 67/67 at W2 implementation commit `ff7cd1965`. |
| Ordinary real-dump evidence | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 4/4 at W2 implementation commit `ff7cd1965`, including the 22-case W2 corpus. |
| Optimized modeled-context evidence | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1 at W2 implementation commit `ff7cd1965`. |

Both W1 and W2 local results are corroborated by their exact-commit hosted closure runs above. Their historical project
and test counts describe the repositories at those commits. The current workflow builds all 14 projects and runs every
remaining test in each selected category. Caveat: those tests establish behavior only for the named fixture shapes.

### Current local W3 implementation verification — 2026-07-14

Exact strengthened implementation commit `19c292f9f` passed the following local matrix. Every managed command ran through
`./eng/Invoke-HeadlessProcess.ps1`; every command used the milestone-selected set at that commit, no test was skipped,
and no UI was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false` | Passed across 15 projects, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 103/103. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 67/67. |
| Ordinary real-dump evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. |
| Optimized modeled-context evidence | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1. |
| Focused W3 evidence | the same wrapped integration-project command with `--filter "FullyQualifiedName~W3DumpGetterExecutionIntegrationTests"` | Passed, 2/2. This is a focused re-run/view, not two additional ordinary-dump facts. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

This verifies all behavioral portions of W3 and the implementation-checkpoint portion of its repository-wide gate.
[GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs
at the same exact pushed implementation commit. Exact documentation-closure commit `de6cea124` and [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently satisfied the
separate hosted closure requirement. The implementation checkpoint realizes `+8,842/-1,650` hand-written LOC
(`+5,362/-928` production and `+3,480/-722` tests) plus 39 generated lock-file lines.

### Historical local W4.2 implementation verification — 2026-07-14

Exact pushed checkpoint `e89e43498` passed the following local matrix. Every managed command ran through
`./eng/Invoke-HeadlessProcess.ps1`; every test filter used the milestone test selection; no test was skipped; and no UI was
displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false` | Passed across 15 projects, 0 warnings / 0 errors. |
| Focused W4.2 kernel | the wrapped unit-project command filtered to `ProvenanceConcreteDomainTests`, `ProvenanceLineageTests`, and `ProvenanceMachineTransferTests`, together with the milestone test selection | Passed, 53/53: 23 domain, 14 lineage, and 16 machine facts. |
| Complete semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 156/156. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 71/71. |
| Ordinary real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. This is regression evidence only; W4.2 creates no dump-grounded result. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

The checkpoint accounts for 3,454 LOC: 3,429 attributable W4.2 implementation LOC (1,521 production plus 1,908
tests) against its final refined 3,350–3,500 LOC estimate, plus 25 LOC that segregate a pre-existing resource-admission
fact from milestone evidence. At that checkpoint, W4 had 3,932 realized LOC through W4.2 and projected
18,532–26,132 LOC across the then-seven remaining W4.3–W4.9 slices. Exact E2 field loads remained exact;
partial/unavailable field continuation, `FieldLoadTransform`, and precision-loss events remained W4.3 work. No W4
product facade, dump-grounded counterfactual result, direct call, or hosted umbrella closure is claimed.

### Historical local W4.3 implementation verification — 2026-07-14

Exact implementation checkpoint `7479b1ad4` passed the following local matrix. Every managed command ran through
`./eng/Invoke-HeadlessProcess.ps1`; every test filter used the milestone test selection; no test was skipped; and no UI was
displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:TreatWarningsAsErrors=true` | Passed across 15 projects, 0 warnings / 0 errors. |
| Focused W4.3 field-evidence kernel | the wrapped unit-project command filtered to `FieldLoadEvidenceTests`, `ProvenanceFieldLineageTests`, and `ProvenanceFieldTransferTests`, together with the milestone test selection | Passed, 55/55. |
| Complete semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 211/211. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 71/71. |
| Ordinary real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. This is regression evidence only; W4.3 creates no dump-grounded result. |
| Optimized real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1. This is regression evidence only; W4.3 creates no dump-grounded result. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

W4.3 realizes 3,096 implementation LOC: 1,100 production LOC plus 1,996 test LOC. W4 therefore has 7,028 realized checkpoint
LOC through W4.3, with 12,200–18,700 LOC remaining across W4.4–W4.9 and a current total projection of
19,228–25,728 LOC. The checkpoint implements immutable backend-neutral field evidence, a structured load-result
branch, an optional approximation-domain capability, policy-gated partial/unavailable continuation,
`ValuePrecisionLost`, and canonical `FieldLoadTransform` lineage. Exact, conflict, invalid, and typed-null behavior and
atomic failure/budget behavior remain compatible. No ClrMD evidence producer, product facade, dump-grounded W4 result,
direct call, or hosted umbrella closure is claimed.

### Current local W4.4 implementation verification — 2026-07-14

Pushed checkpoints `2e596c117` (W4.4a) and `742ef2c4f` (W4.4b) passed the following cumulative local matrix. Every
managed command ran through `./eng/Invoke-HeadlessProcess.ps1`; every behavioral filter used
the milestone test selection; no test was skipped; and no UI was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:TreatWarningsAsErrors=true` | Passed across 15 projects, 0 warnings / 0 errors. |
| Focused W4.4 graph planner | the wrapped unit-project command filtered to `MethodGraphPlannerTests`, together with the milestone test selection | Passed, 35/35. |
| Focused W4 fixture | the wrapped integration-project command filtered to `W4GateFixtureTests`, together with the milestone test selection | Passed, 6/6. |
| Complete semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 250/250. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 73/73. |
| Ordinary real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. This is regression evidence only. |
| Optimized real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1. This is regression evidence only. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

W4.4 realizes 3,651 added LOC: 2,076 production LOC plus 1,575 test LOC. It is recorded as W4.4a at 1,043 LOC and
W4.4b at 2,608 LOC so each implementation slice remains beneath the 3,500-LOC ceiling. W4 therefore has 10,679
realized LOC through W4.4, with 10,500–16,100 LOC remaining across W4.5–W4.9 and a current total projection of
21,179–26,779 LOC; the original 16,860–25,310 baseline remains preserved.

W4.4 resolves an exact same-module managed-IL MethodDef and body-independent signature before body acquisition, then
freezes complete definitions, typed boundaries, distinct fields, and direct-call edges into a deterministic rooted
acyclic graph. Shared callees are one node, cycles fail, conflicts remain conflicts, and failures expose no partial
plan. The fixed 64-method and 1,024 method/field/edge-unit ceilings are internal resource guards, not the configurable
product traversal budget. The exact fixture freezes two nodes, two fields, one edge at IL offset 12, required logical
depth 2, and five internal units. The legacy machine still rejects before the call: W4.4 implements no call transfer,
frame/depth enforcement, model, product facade/result, or dump-grounded W4 execution.

### Current local W4.5a implementation verification — 2026-07-14

Exact pushed commit `356c07037` passed the following cumulative local matrix. Every managed command ran through
`./eng/Invoke-HeadlessProcess.ps1`; every behavioral filter used the milestone test selection; no test was skipped; and no UI
was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:TreatWarningsAsErrors=true` | Passed across 15 projects, 0 warnings / 0 errors. |
| Strict unit project build | wrapped Release build of `tests/Interpreter.Tests/Interpreter.Tests.csproj` with restore disabled, build servers disabled, shared compilation disabled, and warnings as errors | Passed, 0 warnings / 0 errors. |
| Strict integration project build | wrapped Release build of `tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj` with restore disabled, build servers disabled, shared compilation disabled, and warnings as errors | Passed, 0 warnings / 0 errors. |
| Focused prepared-graph execution | the wrapped unit-project command filtered to `PreparedGraphExecutionTests`, together with the milestone test selection | Passed, 25/25. |
| Focused W4 fixture | the wrapped integration-project command filtered to `W4GateFixtureTests`, together with the milestone test selection | Passed, 7/7. |
| Complete semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 275/275. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 74/74. |
| Ordinary real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. This is regression evidence only. |
| Optimized real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1. This is regression evidence only. |
| Markdown-link guard | `./eng/verify-markdown-links.ps1` | Passed, 62 files / 41 destinations. |
| Headless-workflow guard | `./eng/verify-headless-workflows.ps1` | Passed, 1 workflow. |

W4.5a realizes 3,334 added LOC: 1,590 production LOC plus 1,744 test LOC. W4 therefore has 14,013 realized LOC
through W4.5a. W4.5b was then estimated at 1,800–2,700 LOC, projecting combined W4.5 at 5,134–6,034 LOC and full W4
at 24,013–29,313 LOC; the original 16,860–25,310 baseline and the earlier W4.2, W4.3, and W4.4 checkpoint projections
remain preserved.

The exact W4 fixture now reaches the CoreCLR result through 10 instructions, two field loads, two logical frames, and
unchanged memory without resolving metadata again. Call and return each consume one instruction; instruction events
precede frame-entered/frame-exited events; the operational depth high-water facts are 2/2; and insufficient configured
depth fails before execution. An independent audit found no remaining production findings after the checkpoint fixes:
capability failures are structurally blocked, activation/session compatibility is checked before capability use and
rebound atomically, budget availability precedes invariants/capabilities, and active/unwound/terminal high-water facts
plus empty-stack terminal results are validated. Explained-unknown call/return lineage still reports
`EXEC_CALL_LINEAGE_UNAVAILABLE` at that checkpoint; models, product, dump, and hosted closure remain pending.

### Current local W4.5b implementation verification — 2026-07-14

Exact pushed commit `c72f6ee9e5545240433294cdca4f350808339aef` passed the following cumulative local matrix. Every
managed command ran through `./eng/Invoke-HeadlessProcess.ps1`; every behavioral filter used
the milestone test selection; no test was skipped; and no UI was displayed.

| Gate | Headless command shape | Result |
|---|---|---|
| Locked dependency graph | `./eng/Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode` | Passed. |
| Strict solution build | `./eng/Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:TreatWarningsAsErrors=true` | Passed across 15 projects, 0 warnings / 0 errors. |
| Focused prepared-graph execution | the wrapped unit-project command filtered to `PreparedGraphExecutionTests`, together with the milestone test selection | Passed, 40/40. |
| Combined lineage/audit regression | the wrapped unit-project command selecting the complete prepared-call lineage and compatibility audit set, together with the milestone test selection | Passed, 76/76, including 29 frozen legacy identity cases. |
| Compiler-emitted lineage fixture | the wrapped integration-project command filtered to the compiler lineage cases, together with the milestone test selection | Passed, 2/2. |
| Focused W4 fixture | the wrapped integration-project command filtered to `W4GateFixtureTests`, together with the milestone test selection | Passed, 9/9. |
| Complete semantic/admission/differential suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore ` | Passed, 297/297. |
| Fast adapter suite | `./eng/Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast"` | Passed, 76/76. |
| Ordinary real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus!=ModeledIncidentContextV1"` | Passed, 5/5. This is regression evidence only. |
| Optimized real-dump regression | the same wrapped integration-project command with `--filter "Category=Dump&Corpus=ModeledIncidentContextV1"` | Passed, 1/1. This is regression evidence only. |
| Documentation/workflow guards | `./eng/verify-markdown-links.ps1` and `./eng/verify-headless-workflows.ps1` | Passed. |

W4.5b realizes 2,804 added LOC: 766 production LOC plus 2,038 test LOC. Combined W4.5 realizes 6,138 LOC, and W4
therefore has 16,817 realized LOC through W4.5. The historical W4.5b estimate was 1,800–2,700 LOC and the combined
W4.5 projection was 5,134–6,034 LOC; each upper bound was exceeded by 104 LOC. The W4.5-closure projection was
25,017–29,417 LOC. The subsequent design-audit projection was 27,217–32,117 LOC after splitting W4.6a at
1,800–2,600 LOC and the then-unified W4.6b at 2,700–3,500 LOC (4,500–6,100 combined). These are historical, not
current-state claims. The original 16,860–25,310 baseline, original combined-W4.5 estimate of 2,300–3,500 LOC, and
W4.2, W4.3, W4.4, and W4.5a checkpoint projections of 18,532–26,132, 19,228–25,728, 21,179–26,779, and
24,013–29,313 LOC also remain historical.

The mixed partial/exact compiler graph contains five reachable lineage nodes: origin, field transform, parameter-zero
call transform, arithmetic with its exact operand embedded, and return transform. The partial/unavailable graph
contains eight: two origins, two field transforms, two parameter-indexed call transforms, binary transform, and return
transform. Both execute 10 instructions, perform two field loads, leave memory unchanged, record depth high-water 2/2,
avoid metadata re-resolution, and replay in the same or a fresh session. Canonical schema version 1 remains unchanged;
new kinds 4 and 5 are append-only, and all prior bytes/IDs remain frozen across 29 legacy cases. An independent audit
found no production or test findings. At this checkpoint, models, product counterfactual contracts, ClrMD dump
grounding, and hosted closure remained later work.

### Current local W4.6a implementation verification — 2026-07-14

Exact pushed commit `77c92789b16d9258c907d5026a36e39f8c957b41` passed the following cumulative headless
matrix. Every behavioral filter used the milestone test selection; no test was skipped; and no UI was displayed.

| Gate | Evidence scope | Result |
|---|---|---|
| Locked dependency graph | repository restore in locked mode through the headless wrapper | Passed. |
| Strict solution build | fifteen-project Release solution build with warnings as errors | Passed, 0 warnings / 0 errors. |
| Pure-model contract vocabulary | bounded identity/version/stable codes, descriptor confidence/effects, non-generic invocation/outcome, registry selection | Passed, 49/49. |
| Model-aware graph planner | exact/no-effect body-free selection, failure precedence, leaf deduplication, equality, traversal/depth, and activation block | Passed, 25/25. |
| Legacy graph planner | unchanged interpreted-only behavior and frozen identities | Passed, 35/35. |
| Real SRM compiler fixture | deterministic PDB-free modeled target selection and fresh replay | Passed, 1/1. |
| Compiler-lineage compatibility | deterministic target PE and re-frozen path-independent lineage | Passed, 2/2. |
| Complete semantic/admission/differential suite | wrapped unit project with the milestone test selection | Passed, 371/371. |
| Fast adapter suite | wrapped integration project with `Category=Fast` | Passed, 77/77. |
| Ordinary real-dump regression | wrapped non-modeled dump selection with the milestone test selection | Passed, 5/5; regression only. |
| Optimized real-dump regression | wrapped modeled-incident dump selection with the milestone test selection | Passed, 1/1; regression only. |
| Documentation/workflow guards | Markdown-link and headless-workflow verification | Passed, 62 Markdown files / 41 local destinations and 1 workflow. |

The admitted compiler graph has one interpreted root, one opaque modeled leaf, two fields, one call edge, five
traversal units, and required logical depth two. The deterministic PDB-free target PE SHA-256 is
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`. Selection occurs after direct-call
resolution/typing and before prospective target-body acquisition. Only `Exact` confidence plus `None` effects is
admitted; every missing, throwing, invalid, mismatched, non-exact, or unsupported-effect selection fails without a
target-body fallback or partial graph. Modeled activation returns `EXEC_MODEL_EXECUTION_UNAVAILABLE` before depth,
arguments, state, resolver, or model access. This is structural preparation evidence, not model-execution evidence.

W4.6a realizes 2,959 added LOC: 1,210 production plus 1,749 tests/fixture support. It exceeded the historical
1,800–2,600 estimate by 359 LOC at the upper bound and brought W4.1–W4.6a realization to 19,776 LOC. Its checkpoint
full-W4 projection under the then-current remainder was 28,376–32,476 LOC.

### Current local W4.6b implementation verification — 2026-07-14

Exact pushed commit `fd723a912` passed the following headless compatibility matrix. Every behavioral filter included
the milestone test selection, no test was skipped, and no UI was displayed.

| Gate | Evidence scope | Result |
|---|---|---|
| Strict builds | warnings-as-errors Release compilation for the affected projects | Passed, 0 warnings / 0 errors. |
| Focused modeled-return lineage | `IPureCallModelLineageDomain<TValue>`, kind-6 construction, atomicity, replay, and fresh-domain continuation | Passed, 8/8. |
| Combined legacy/model lineage | all lineage compatibility cases, including the frozen kind-1–5 identities | Passed, 44/44. |
| Standard single-node integration build | headless `/m:1` Release integration-project build | Passed, 0 warnings / 0 errors. |
| W4 call-lineage integration | `W4CallLineageIntegrationTests` | Passed, 2/2. |

W4.6b appends schema-v1 `ModeledReturnTransform` kind 6. Exact arguments are embedded in the modeled relation;
explained arguments receive unchanged parameter-indexed kind-4 nodes; and the complete call-node/return-node batch is
validated and interned atomically with acyclicity. Structural replay validation and fresh-domain continuation are
covered while kinds 1–5 retain their exact bytes and IDs. No machine invokes a model at this checkpoint.

W4.6b realizes 1,003 added LOC: 481 production plus 522 tests, with 23 deletions. W4.1–W4.6b realization is therefore
20,779 LOC. Historical projections remain original 16,860–25,310; post-W4.2 18,532–26,132; post-W4.3
19,228–25,728; post-W4.4 21,179–26,779; post-W4.5a 24,013–29,313; W4.5 closure 25,017–29,417; post-design-audit
27,217–32,117; W4.6a checkpoint 28,376–32,476; first W4.6b recalibration 28,876–33,276; post-split
28,826–33,726; post-W4.6b checkpoint 28,879–33,279; and pre-W4.6c/d closure 30,079–33,729 LOC.

### Current local W4.6c/d implementation verification — 2026-07-15

Exact commits `877c9fb55` and `da5346813` complete the narrow pure-model execution profile. W4.6c invokes only the
capability frozen in the leaf, never re-reads registry/resolver/descriptor/body state, and never falls back. Exact and
lineage-grounded unknown returns transfer atomically to the caller with one instruction event, unchanged memory, and
no model frame/event. Pre-entry budget rejection, immutable attempts, invocation/completion counters, independent
logical/active depth witnesses, exact terminal depth retention, stable failure taxonomy, and resume chronology are
covered by the focused 34/34 lane.

W4.6d directly proves compiler/SRM interpreted/model/CoreCLR exact agreement and interpreted/model agreement for both
partial/unavailable shapes. The deterministic target PE SHA-256 is
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; the mixed case freezes graph hash
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`, while repeated and fresh
metadata-reader/domain/machine sessions reproduce the both-unknown graph hash
`31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`.

| Gate | Evidence scope | Result |
|---|---|---|
| Locked dependency graph | repository restore in locked mode through the headless wrapper | Passed. |
| Strict solution build | fifteen-project Release solution build with warnings as errors | Passed, 0 warnings / 0 errors. |
| W4.6c focused machine conformance | invocation/transfer, attempts, failures, counters, depth, chronology, and exact terminal witness | Passed, 34/34. |
| W4.6d compiler/SRM conformance | exact, degraded, repeated, and fresh-session differential evidence | Passed, 3/3. |
| Aggregate W4 integration | fixture, call lineage, model planning, and model execution | Passed, 13/13. |
| Complete semantic/admission/differential suite | wrapped unit project with the milestone test selection | Passed, 413/413. |
| Fast adapter suite | wrapped integration project with `Category=Fast` | Passed, 80/80. |
| Ordinary real-dump regression | wrapped non-modeled dump selection with the milestone test selection | Passed, 5/5; regression only. |
| Optimized real-dump regression | wrapped modeled-incident dump selection with the milestone test selection | Passed, 1/1; regression only. |

Every behavioral invocation used `eng/Invoke-HeadlessProcess.ps1`, used the milestone test selection, and recorded zero
skips. W4.6c realizes 2,734 added LOC (1,425 production plus 1,309 tests), W4.6d realizes 956 test LOC, W4.6 totals
7,652 LOC, and cumulative W4 realization is 24,469 LOC. W4.6c/d realized 3,690 LOC against their historical
3,400–3,750 estimate. The post-W4.6 plan left W4.7 at 2,200–3,150 LOC and projected 31,069–34,319 LOC; both are now
historical.

### Current local W4.7 implementation verification — 2026-07-15

Exact pushed commits `2e70fe76d` and `dad6a6dd4` complete the standalone exact-null target-outcome slice. W4.7a
requires a complete same-machine sequence of issuer-certified transitions from legacy IL-zero activation through the
first null-reference latch, optionally plus one certified idempotent re-step. It validates location, memory identity,
budget, and events; rejects caller-authored or malformed evidence by stable code; and freezes literal fragment SHA-256
`a9b98e46583dcf90ac108571c126d8d86cec0465c595e2689fae767e33ff108e`. W4.7b proves direct/adjusted compiler
getters, fresh SRM/module/domain/machine replay, and no re-step resolver/domain/memory access or repeated field load.

| Gate | Evidence scope | Result |
|---|---|---|
| Locked dependency graph | repository restore through the headless wrapper | Passed. |
| Strict solution build | sixteen projects / eleven source projects, warnings as errors | Passed, 0 warnings / 0 errors. |
| W4.7a focused projector | issuer, chronology, latch, accounting, events, canonical fragment, negatives | Passed, 15/15. |
| W4.7b compiler replay | direct/adjusted fresh replay and poison/count evidence | Passed, 2/2. |
| Combined W4.7 | both focused slices | Passed, 17/17. |
| Compiler differential class | all compiler-emitted differential facts | Passed, 23/23. |
| Complete unit | wrapped unit project | Passed, 430/430. |
| Fast / ordinary dump / optimized dump | standard milestone-selected lanes | Passed, 80/80, 5/5, and 1/1. |
| Documentation/workflow guards | Markdown links and headless workflow | Passed, 62 files / 41 destinations and 1 workflow. |

Every behavioral lane was headless, used the milestone test selection, and had zero skips. W4.7a/b realize
2,448/353 LOC, 2,801 total, bringing W4 through W4.7 to 27,270 LOC. W4.8 later realizes 11,924 LOC and W4.9
2,698 LOC, bringing full W4 implementation to 41,892 LOC.

### Closed W4.8–W4.9 implementation verification — 2026-07-15

W4.8's final runner checkpoint passes focused execution 10/10, the complete counterfactual family 77/77, and complete
milestone-selected unit 502/502 with a strict zero-warning Release build. W4.9a validates the atomic ClrMD graph/field
producer; W4.9b passes binder 5/5, counterfactual 77/77, and Fast 88/88; W4.9c passes generated dump 1/1, ordinary
dump 6/6, and Fast 88/88. All behavioral commands use the headless wrapper and the milestone test selection, with zero
skips. W4.9d records repository-wide local and exact pushed hosted closure.

W4.9d's local candidate passes locked restore, a strict sixteen-project Release build at 0 warnings/errors, complete
unit 502/502, Fast 88/88, ordinary dump 6/6, optimized dump 1/1, aggregate W4 integration 14/14, Markdown 62/44, and
the one-workflow headless guard. [Hosted run
29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083) passed all four jobs at exact
implementation-closure commit `a819a08fd9ccdf926620c505732475990b242be9`. [Run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230) passed the same jobs at final
documentation-closure commit `aaec73c5b987089addb539d3628de67bd815bd8f`.

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
6. Start the target with a deterministic minimal environment and dedicated working and temporary directories.
7. Assert the offline locator and explicit resource policy: reject dumps above 8 GiB, cap ClrMD's dump cache at 256 MiB with stack-trace/root caching disabled, and reject managed PE artifacts above 512 MiB at the typed external `Open` boundary. Caveat: these bounds validate only the named paths.

### D. Product scenario tests (W2)

The checked-in `w2-root-field-v1` corpus contains 22 cases over 20 distinct expression texts. Each includes input expression, typed root evidence,
policy, preparation/plan outcome, value/evidence result, diagnostics, provenance, and canonical replay output. Every
case is repeated in-session and after fresh-session rebind; the 13 prepared plans carry an injective canonical identity.
Later syntax expands only from a scenario whose evidence and resource behavior are explicit.

### E. Concrete differential tests (W3)

For each admitted concrete opcode/method shape, run a tiny compiled fixture on CoreCLR and through the interpreter, then compare normalized value or exception outcomes. Reject unsupported bodies before execution; never treat partial execution as a differential pass.

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

Do not check in a large dump merely to make CI convenient. If a binary fixture becomes necessary, generate it from
source-controlled input, document provenance, and define an explicit refresh procedure.

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

All four current `dotnet test` commands run every remaining test in their selected category. Restore and build cover
all 13 current projects. Caveat: this validates only the named fixture and input shapes.

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
11. The repository-wide Release build and all required milestone-selected fast, ordinary-dump, and optimized-dump lanes
    pass headlessly with zero skips.

All eleven pass locally at strengthened implementation commit `19c292f9f` with the exact matrix recorded in section 2;
[implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four required
jobs at that exact commit. Exact documentation-closure commit `de6cea124` passed all four required jobs in [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), formally closing W3.

### W4 — unknown-aware method evaluation

The normative gate is the
[`Counterfactual Method Evaluation Contract`](counterfactual-method-evaluation-contract-proposal.md). It admits one
branchless, EH-free generated-dump scenario: `DumpProbe.GetMarkerSummary` reads the two marker fields and reaches the
direct `CombineMarkers` helper. This question is intentionally beyond W2: a W2 plan selects one field and never
executes user IL, so it cannot combine the two observations or cross the call boundary.

W4.1 checkpoint `82363585b` proves the exact 18-byte caller/four-byte helper closure, relational metadata
operands and signature/header facts, CoreCLR result, and the actual W3 first rejection at the second `ldfld` before the
direct `call`. Local headless verification passed focused 4/4, complete fast 71/71, and ordinary dump 5/5 with zero
skips after locked restore and a zero-warning Release build. It realizes 478 added or materially revised LOC and does
not claim dump-sourced W4 bodies, unknown continuation, call execution, a product result, or hosted closure.

W4.2 checkpoint `e89e43498` adds the second meaningful domain, explicit explained-unknown execution policy,
lineage-independent semantic equality, versioned content-addressed `InputOrigin`/`BinaryTransform` graphs, and exact
or explained-unknown transport through the shared E1/E2 handlers. Focused W4.2 verification passed 53/53 (23 domain,
14 lineage, 16 machine), alongside the 156/156 full unit, 71/71 fast, and 5/5 ordinary-dump regression lanes, strict
15-project zero-warning build, and both guards. The 3,454-LOC checkpoint contains 3,429 W4.2 implementation LOC plus
25 scope-segregation LOC; at that checkpoint W4 totaled 3,932 realized LOC and projected 18,532–26,132 LOC.
Exact E2 field loads remained exact, and non-exact field continuation remained blocked for W4.3. This is a dump-free
kernel/reuse result, not a W4 product or dump result.

W4.3 checkpoint `7479b1ad4` adds immutable backend-neutral field evidence, the optional approximation capability,
policy-gated partial/unavailable continuation, `ValuePrecisionLost`, and canonical `FieldLoadTransform` behavior while
preserving exact/conflict/invalid/typed-null and atomic failure/budget semantics. Its headless local matrix passed
focused 55/55, complete unit 211/211, fast 71/71, ordinary-dump regression 5/5, optimized-dump regression 1/1, locked
restore, a strict 15-project build with 0 warnings and 0 errors, and both guards, with the milestone test selection and zero
skips. The 3,096-LOC checkpoint contains 1,100 production LOC and 1,996 test LOC; W4 totals 7,028 realized LOC through W4.3
and projects to 19,228–25,728 LOC. This is still dump-free evidence, not a ClrMD producer, product facade, dump-grounded
W4 result, direct call, or hosted umbrella closure.

W4.4 checkpoints `2e596c117` and `742ef2c4f` add body-independent exact direct-MethodDef resolution and a complete
immutable call graph. Its focused planner and fixture lanes passed 35/35 and 6/6, alongside complete unit 250/250,
fast 73/73, ordinary-dump regression 5/5, optimized-dump regression 1/1, locked restore, the strict 15-project build,
and both guards. Its 3,651 added LOC comprise 2,076 production and 1,575 tests, split into 1,043-LOC W4.4a and
2,608-LOC W4.4b slices. W4 totals 10,679 realized LOC through W4.4 and projects to 21,179–26,779 LOC. Preparation
freezes the exact fixture's two nodes, two fields, one edge, depth two, and five internal units, but does not execute
the call or enforce a request depth.

W4.5a checkpoint `356c07037` activates that exact graph and executes exact values across the direct call and return.
Focused prepared-graph and W4 fixture lanes passed 25/25 and 7/7, alongside complete unit 275/275, fast 74/74,
ordinary-dump regression 5/5, optimized-dump regression 1/1, locked restore, the strict 15-project solution build,
strict unit/integration project builds, and both guards. Its 3,334 added LOC comprise 1,590 production and 1,744 tests. W4 totals 14,013 realized LOC through
W4.5a and projects to 24,013–29,313 LOC. The checkpoint proves exact call execution and depth/replay accounting only;
explained-unknown call/return lineage, models, product, dump, and hosted closure remain pending at that checkpoint.

W4.5b checkpoint `c72f6ee9e5545240433294cdca4f350808339aef` closes the dump-free interpreted-call kernel. Its
optional domain capability transforms a complete two-argument vector and return into canonical parameter-indexed
lineage without changing exact values; capture and replay preserve only the validated reachable DAG. Focused
prepared-graph, combined lineage/audit, compiler-lineage, and W4 fixture lanes passed 40/40, 76/76, 2/2, and 9/9,
alongside complete unit 297/297, fast 76/76, ordinary-dump 5/5, optimized-dump 1/1, locked restore, the strict
15-project solution build, and both guards. Its 2,804 added LOC comprise 766 production and 2,038 tests. Combined
W4.5 realizes 6,138 LOC and W4 totals 16,817 realized LOC through W4.5. The W4.5-closure projection was
25,017–29,417 LOC.

A subsequent W4.6 design audit split the former 2,300–3,400 LOC model estimate into W4.6a structural
registry/opaque modeled-leaf/effect-and-fallback admission at 1,800–2,600 LOC and the then-unified W4.6b typed
execution/attempts/modeled-lineage/conformance at 2,700–3,500 LOC, or 4,500–6,100 LOC combined. Those remain
historical planning facts.

W4.6a checkpoint `77c92789b16d9258c907d5026a36e39f8c957b41` delivers the structural portion: exact
body-independent descriptor selection, opaque modeled-leaf planning, traversal/depth accounting, and fail-closed
activation. Its exact matrix is recorded above. W4.6b checkpoint `fd723a912` delivers the atomic modeled-return
lineage/domain portion, appending kind 6 while preserving kinds 1–5. W4.6a realizes 2,959 added LOC and W4.6b realizes
1,003, bringing W4 to 20,779 realized LOC. Neither historical checkpoint executes a model.

W4.6c checkpoint `877c9fb55` delivers frozen-capability execution, atomic exact/unknown transfer, attempts, counters,
depth witnesses, failure taxonomy, and unit conformance in 2,734 LOC. W4.6d checkpoint `da5346813` delivers 956 test
LOC of compiler/SRM exact, degraded, repeated, and fresh-session conformance. Combined W4.6 realizes 7,652 LOC and
brings W4 to 24,469 realized LOC. W4.7a/b checkpoints `2e70fe76d`/`dad6a6dd4` deliver complete issuer-certified
target projection and compiler/SRM replay in 2,801 LOC, bringing W4 to 27,270 realized LOC. W4.8/W4.9 implementation
realizes 11,924/2,698 LOC and full W4 realizes 41,892 LOC. The earlier 25,017–29,417,
27,217–32,117, 28,376–32,476, 28,876–33,276, 28,826–33,726, 28,879–33,279, 30,079–33,729,
31,069–34,319, and 31,670–33,970 projections remain historical.

W4.8/W4.9 implement the rooted product and dump portions below. Items 1–11 are satisfied; exact pushed W4.9d
implementation closure passed all four hosted jobs in run 29463426083 at
`a819a08fd9ccdf926620c505732475990b242be9`, and final documentation closure passed them again in run 29463847230 at
`aaec73c5b987089addb539d3628de67bd815bd8f`. W4.4 satisfies
interpreted structural graph preparation; W4.5 satisfies
exact and explained-unknown interpreted direct-call/depth/lineage behavior; W4.6 satisfies structural model behavior;
W4.7 satisfies the standalone target fragment; W4.8 satisfies product preparation/execution/projection; and W4.9
satisfies detached dump production and replay:

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
5. The separate mandatory conformance request uses W4.6a's selected structural pure model and W4.6b's modeled-return
   lineage. W4.6c proves actual invocation, atomic exact/unknown transfer and nontransfer, model attempts, state/memory
   atomicity, instruction charging, depth witnesses, and semantic-event truthfulness; W4.6d proves exact, degraded,
   repeated, and fresh-session compiler/SRM agreement. Broader fallback/effect policies remain outside W4.
6. **Satisfied by W4.7.** Retained exact typed-null `ldfld` behavior is projected as the only W4 target-exception outcome through a standalone
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
    headlessly with zero skips under the milestone-selected test selection at the exact pushed commit.

Branches and path forks, CFG state merge/fixpoint/widening, loops, handler-transfer EH, virtual or generic dispatch,
broad intrinsic/model catalogs, allocation, async/dynamic lifting, and virtual stepping remain outside this gate. A
later research proposal or existing scaffold does not count as W4 evidence.

### Current W5.1–W5.5b implementation verification — 2026-07-16

The pushed implementation checkpoints establish six distinct test boundaries:

1. W5.1 reuses the exact W2 parser for syntax classification and tests canonical requests, exact method spelling,
   casing, punctuation, suffix rejection, bounds, and fresh-object replay.
2. W5.2 reacquires real dump evidence through counting/poisonable sources, disposes ClrMD before W4 work, proves the
   exact result, and exercises missing, ambiguous, partial, unavailable, unsupported, incompatible, conflicting, and
   invalid acquisition failures.
3. W5.3 compares the underlying canonical W2/W4 bytes and hashes through the strict outcome union and covers exact,
   degraded, preparation, acquisition, unsupported, budget, depth, and cancellation outcomes.
4. W5.4 launches the reference consumer through `eng/Invoke-HeadlessProcess.ps1` in fresh processes, reopens the same
   generated dump, and requires byte-identical machine/human reports across all nine scenarios.
5. W5.5a joins the checked-in question portfolio to those evaluated rows in two more fresh processes, requires byte-
   identical raw-count reports, retains unsupported/unavailable rows, emits no percentages, and rejects an attempted
   generated-to-representative corpus-kind promotion.
6. W5.5b launches twelve isolated hidden targets with predeclared arguments across request-pipeline and batch-pipeline
   root types, writes one full dump and runs one fresh consumer per incident, enforces twelve distinct snapshot hashes,
   validates exact/degraded/unavailable/unsupported expected outcomes, and requires byte-identical fresh-process
   portfolio replay.

The focused `Category=Dump&Corpus=W5UsefulnessGeneratedV1` lane passes 1/1 at checkpoint `0f5230e13`, including the
W5.4 replay it consumes, with zero skips and no UI. The generated report records 8/9 admitted, 3/9 exact, 0/4 useful
partial-or-unknown, and 0/9 decision-changing questions, but its representative projection is 0/0 and its successor
selection is deferred. These counts validate the runner, not product usefulness.

The focused `Category=Dump&Corpus=W5MeaningfulSyntheticV2` lane passes 1/1 at checkpoint `90ade6d92` with zero skips
and no UI. It records 8/12 admitted, 4/12 exact, 2/3 useful partial-or-unknown, and 6/12 decision-changing questions.
Four `MemberNavigation` incidents outrank three `ContextAcquisition` and one `ExecutionBody` incident, so the stable
decision is `AdmitFixedDepthMemberChain`. This is designed prototype evidence, not field evidence: representative/
external-observation counts remain 0 questions, 0 incidents, and 0 shapes, and no readiness rate is claimed.

The cumulative documentation candidate also passes locked restore; strict 14-project Release build with warnings as
errors at 0 warnings/0 errors; complete unit 502/502; Fast 104/104; ordinary dump 10/10; optimized dump 1/1; focused W5
facade 3/3; focused generated usefulness 1/1; focused meaningful synthetic usefulness 1/1; Markdown 63 files/59
destinations; and the one-workflow headless guard. All
behavioral invocations use `eng/Invoke-HeadlessProcess.ps1` and have zero skips. The full matrix is regression and
complete local closure evidence at pushed checkpoint `56ec08149`; exact pushed hosted W5 umbrella verification remains
open. Run [29512657137](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29512657137) passes documentation
and Build/Fast at exact commit `24825ce53`, while GitHub rejects both dump jobs with zero executed steps because the
account's payments/spending limit requires attention. The same-commit retry repeats that infrastructure rejection;
neither attempt is dump-test evidence.

## 9) Deferred research suites

The following are not active CI commitments:

- branch/path-fork behavior and CFG merge/fixpoint convergence or widening properties;
- CN-O, range, origin labels, and multi-domain precision scorecards;
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
- `BoundsRegression`
- `PerformanceRegression` (only after a baseline is approved)
- `InfrastructureFailure`

Record the failing fixture/test, exact command and environment, expected versus actual normalized outcome, evidence source, and whether a host-visible contract changed.

## 11) Selected decision and remaining evidence question

W5.5b answers the prototype-design question with twelve predeclared designed incidents: fixed-depth member navigation
is the recurring blocker and is the one selected post-W5 slice. The remaining question is deliberately separate:
whether later external observations confirm, reverse, or stop that direction. No readiness threshold is inferred from
the designed corpus, and broader external-input handling remains separately scoped.
