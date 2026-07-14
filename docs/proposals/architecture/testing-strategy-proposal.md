# Testing Strategy

> **Current delivery policy (2026-07).** Tests follow the active W0–W4 dump-evaluator roadmap. The repository must distinguish checked-in test code, a locally verified result, a CI-enforced gate, and a research proposal. The broad abstract-analysis, virtual-stepping, dynamic, async, and performance matrices are deferred until their entry gates pass.

## Status

Current for active delivery. Research suites are collected separately in section 9.

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

## 2) Present executable evidence

### Fast concrete, admission, determinism, and metadata proofs

`tests/Interpreter.Tests` is dump-free. Its checked-in corpus covers:

1. lifted-flat concrete-domain order, join, meet, widening, canonical unknowns, and redacted display;
2. persistent object/array snapshot and fork isolation, deterministic allocation, bounded arrays, and stable content hashes;
3. a whole-body-admitted branchless `Int32` kernel for constants, arguments, locals, `add`, `sub`, `mul`, and value/void `ret`;
4. fail-closed behavior for unsupported suffixes, EH, malformed/truncated IL, invalid slots/stack/type shape, injected offsets/state, nested frames, oversized bodies, and exhausted budget;
5. byte-identical canonical outcome transcripts for repeated normalized inputs;
6. compiler-emitted methods invoked on CoreCLR and interpreted through the same opcode handlers, including unchecked overflow boundaries;
7. path-independent module/method identity and distinct metadata unavailable/conflict/invalid outcomes.

The differential harness proves only its closed, branchless, EH-free, `Int32` fixture set. It is not evidence for calls, fields, branches, exceptions, arbitrary signatures, or an unknown-aware domain.

### Real dump-memory proof

`tests/Interpreter.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs` generates a full Windows process dump and:

1. retains one read-only dump stream for SHA-256 identity and ClrMD session lifetime;
2. discovers the runtime module and reads its metadata root from dump memory;
3. decodes the dump metadata-root identity and validates its MVID, exact metadata length, and metadata SHA-256 against an independently opened SRM artifact, whose separate whole-file length/SHA-256 identity is also retained;
4. discovers a strong GCHandle under an explicit scan cap, validates its slot pointer and the selected object's method-table header through counted raw-memory reads, and reads an `Int32`, a bounded string prefix, and a null string through counted evidence;
5. distinguishes exact, partial, unavailable, and conflict outcomes with stable issue codes;
6. reads the `Program.RetOnly` MethodDef RVA from counted dump metadata, decodes its complete tiny header and code from counted dump memory, and executes only the normalized exact dump-backed body; fast parser fixtures separately cover fat headers, local-signature tokens, and chained EH sections, while the disk body is an independent equality oracle and supplies no constructor input;
7. verifies deterministic scan/instruction budgets, cleanup, disposal, and invalid-address behavior.

This fixture does not prove arbitrary root/frame recovery, a representative corrupt/hostile-dump corpus, chained expression binding, broad IL semantics, or debugger stepping. It does prove the common result envelope, the first bounded root-field query surface, and one fully dump-sourced tiny method body; fast tests prove the bounded fat/chained-section parser seam without claiming real-dump coverage for those shapes. A separate Normal-vs-Full fixture proves that an omitted page remains partial/unavailable rather than being zero-filled; it is not yet a representative hostile corpus.

### Restricted dump-query proof

`Interpreter.Product.DumpQuery` is exercised both without a dump and through the generated full-dump scenario. The
fast corpus admits exactly one exact non-null ordinal root, `.`, one field, and optional bounded literal coalescing,
while rejecting `?.`, calls, indexing, chaining, arithmetic, oversized inputs, malformed literals, and unsupported
escapes with stable payload-safe codes. The real dump covers `Int32`, exact and partial strings, exact nullable-field
null, coalescing, `?.` rejection, missing root/member evidence, case sensitivity, unsupported field type,
incompatible coalescing, ordered provenance, and repeated canonical replay. Missing or partial evidence is never
reclassified as null merely to apply `??`.

### W0 signal status

| Signal | In-tree evidence | Remaining evidence distinction |
|---|---|---|
| Repository build | Stable .NET 10.0.2xx feature-band/minimum-patch pin, central versions, committed lock files, deterministic Release build, warnings-as-errors under `CI=true`. | Record the exact local result after changes and a GitHub run before calling it remotely enforced. |
| Fast tests | Unit/domain/admission/differential/determinism/metadata suite is checked in. | A successful local run is `Verified`; the workflow's successful run is `CI-enforced`. |
| Dump integration | Required Windows dump category and a bounded target/dump harness are checked in. | An inability to create/load the dump fails the required lane; it is not converted to a passing skip. |
| Determinism | Canonical UTF-8 machine transcripts and multi-axis W1/W2 result envelopes, plus stable identity/content-hash assertions, are checked in. | Add a fresh-process replay runner when a stable external host protocol exists. |
| Documentation truth | The evidence matrix distinguishes raw dump bytes, dump metadata-derived facts, ClrMD-decoded runtime structures, whole-file-identified disk oracle facts, and explicit fixture inputs. | Keep this synchronized whenever an evidence fallback changes. |

The workflow in `.github/workflows/ci.yml` is checked in. That establishes intended automation, not a successful service-side run; `CI-enforced` is claimed only after GitHub reports one.

### Local verification record — 2026-07-13

On Windows with the SDK selected by `global.json`:

| Gate | Command shape | Result |
|---|---|---|
| Locked dependency graph | `dotnet restore Interpreter.sln --locked-mode --disable-parallel --disable-build-servers` | Passed. |
| Full prototype build | `dotnet build Interpreter.sln -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false` | Passed, 0 warnings / 0 errors. |
| Semantic/admission/differential suite | `dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj -c Release --no-build --no-restore` | Passed, 60/60. |
| Fast adapter suite | `dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj -c Release --no-build --no-restore --filter "Category=Fast"` | Passed, 35/35. |
| Real dump evidence | same integration project with `--filter "Category=Dump"` | Passed, 3/3 on three consecutive final-tree runs (9/9 executions). |

This is a local verification record, not evidence that GitHub has run the checked-in workflow.

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

The first scenarios are active. Each includes input expression, admitted syntax, roots, policy, value/evidence result, diagnostics, provenance, and canonical replay output. Later syntax expands only from a scenario whose evidence and resource behavior are explicit.

### E. Differential tests (W3+)

For each admitted concrete opcode/method shape, run a tiny compiled fixture on CoreCLR and through the interpreter, then compare normalized value or exception outcomes. Reject unsupported bodies before execution; never treat partial execution as a differential pass.

## 4) Fixture policy

### Small generated targets first

Prefer purpose-built, source-controlled test targets that expose one risk at a time:

- fixed primitive fields and bounded strings for W1;
- null/member/coalescing cases for W2;
- branchless EH-free arithmetic/getter bodies for W3;
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

- semantic mode (`Observation`, `DerivedQuery`, later `CounterfactualExecution` or `AbstractAnalysis`);
- completion status;
- completeness (`Complete`, `Partial`, or `None`);
- evidence status (`Exact`, `Partial`, `Unavailable`, `Conflict`, or `Invalid`);
- evidence source and provenance;
- effects or virtual writes;
- diagnostics and stable miss reason.

Negative fixtures are first-class acceptance cases. Missing pages, bad addresses, truncated strings, absent or wrong disk artifacts, metadata-root mismatch, whole-file artifact changes, malformed IL, and unsupported syntax must produce deterministic typed outcomes rather than guesses or incidental exceptions.

## 7) CI shape

The W0 pipeline targets `net10.0` and should remain small enough to diagnose:

1. locked restore and Release build;
2. fast semantic/contract tests;
3. a Windows dump-integration lane;
4. documentation/link consistency checks with stable signal.

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
- Every result identifies snapshot/module identity, evidence source, completeness, fallback, and bound.
- Reads and traversals are bounded; diagnostics do not disclose dump contents by default.

### W2 — restricted expression/query slice

- At least ten scenario expressions cover success, exact nullable-field null/coalescing, unavailable roots, invalid syntax, unsupported syntax (including `?.`), and partial evidence.
- Parse/bind/query behavior is deterministic and results are classified as `Observation` or `DerivedQuery`.
- Member access remains read-only and never silently invokes getters, reflection, or user IL.

### W3 — concrete IL semantics and differential oracle

- Implemented domain and persistent-memory laws pass.
- A fixture-derived, branchless, EH-free opcode closure runs through the real domain/memory seam.
- Tiny compiled fixtures agree with CoreCLR on normalized outcomes, including documented exception boundaries.
- The admission check rejects unsupported opcode/EH shapes before execution.

### W4 — unknown-aware method evaluation

- Each supported opcode/call family has concrete, differential, and degraded-evidence coverage.
- Precision loss, modeled calls/effects, and budget stops carry stable provenance and diagnostics.
- Results are explicitly `CounterfactualExecution`; tests reject language that implies historical replay.

## 9) Deferred research suites

The following are not active CI commitments:

- CFG merge/fixpoint convergence and widening properties;
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

1. Should the Windows dump lane generate every negative fixture on demand, or should hard-to-generate sparse/corrupt cases use one small, non-sensitive, provenance-recorded artifact?
2. Which stable external host protocol should own the first fresh-process W2 replay test?
3. What representative optimized incident corpus can supply an honest root/frame-context denominator?
