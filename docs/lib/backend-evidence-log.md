# Backend capability evidence log

> **Current record plus preserved history.** The SRM/PEReader rows dated 2026-07 are executable evidence for the active backend decision. The source-review rows dated 2026-02-17 are intentionally retained as historical research; they do not reopen the backend decision or establish implemented capability.

This log records concrete evidence for the capability axes in `backend-capability-matrix.md` while keeping the distinction between source inspection and running fixtures explicit. The active choice is recorded in `mvp-backend-decision-record.md`.

## How to use this log

For each executable experiment or prototype slice:

1. add one row to **Current executable evidence**,
2. link to a checked-in test and, where useful, captured output,
3. classify confidence as `Low`, `Medium`, or `High`,
4. state the exact evidence boundary and open follow-up required before raising confidence.

Source-reading observations remain useful in the historical section, but they stay `Low` confidence and cannot by themselves change the active backend.

## Evidence quality rubric

| Confidence | Meaning | Typical proof quality |
|---|---|---|
| Low | Directional hypothesis only. | Design reasoning, source inspection, or single anecdotal read-through. |
| Medium | Reproducible in a narrow scenario. | One or more prototype runs with captured output and explicit constraints. |
| High | Reproducible across representative scenarios. | Repeated runs, edge-case coverage, and cross-backend comparison where applicable. |

## Current executable evidence

| Date | Backend | Capability axis | Executable claim | Evidence artifact | Confidence | Boundary / follow-up |
|---|---|---|---|---|---|---|
| 2026-07-13 | SRM/PEReader | Stable module/method identity, MethodDef lookup, and body facts | The fast metadata corpus copies one PE to a different path and obtains equal module/method handles; it distinguishes missing type/method, ambiguous name, invalid token, and module mismatch. The dump fixture independently opens the test-target PE to obtain MVID plus `maxstack`/locals/EH admission facts. | [`MetadataIdentityTests.cs`](../../tests/Interpreter.Tests/MetadataIdentityTests.cs), [`DumpMemoryEvidenceIntegrationTests.cs`](../../tests/Interpreter.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs) | Medium | This W1-era row proves a narrow managed-PE path and honest identity join. By itself it does not cover malformed/truncated PE files, signatures/generics, Portable PDBs, or deliberate dump/disk conflicts; the W3 row below adds the closed signature/field projection proof without claiming those broader shapes. |
| 2026-07-13 | ClrMD + raw process memory | Snapshot/runtime identity and bounded object/string/metadata/IL evidence | A generated full dump is content-identified and opened read-only. Bounded enumeration finds a strong GCHandle; counted reads validate both the handle's selected object pointer and the object's method-table header before exact `Int32`, bounded/null strings, metadata MVID, and method code bytes are read. The dump-sourced `ret` code is executed after its MVID agrees with the independently opened artifact. | [`DumpMemoryEvidenceIntegrationTests.cs`](../../tests/Interpreter.IntegrationTests/DumpMemoryEvidenceIntegrationTests.cs) | Medium | This W1-era row proves one strongly rooted/full-dump scenario, including partial/unavailable/conflict classifications and scan/read caps. Its original tiny-body path did not establish arbitrary frame/root recovery, corrupt/sparse dump handling, or fully dump-derived getter admission; the W3 row below closes only that last gap for one exact getter profile. |
| 2026-07-14 | ClrMD + project-owned query front end | Typed root binding, immutable field-bound plan, and deterministic read-only query replay | A 22-case/20-expression generated-dump corpus distinguishes exact, exhaustive-absence, and bounded-partial roots; retains the exact selector, disposition, issue, counters, caps, and retained-match state; binds one ordinal field exactly once; includes complete nullable child layout in descriptor/plan identity; decodes exact/null `Nullable<Int32>`, exact/partial strings, and bounded literals; and rejects unsupported syntax/types without executing user IL. The complete canonical result byte sequence/hash for all 22 cases and canonical plan projection string/hash for the 13 prepared cases are identical within one session and after close/reopen/rediscovery/rebind. | [`DumpQueryScenarioCorpusIntegrationTests.cs`](../../tests/Interpreter.IntegrationTests/DumpQueryScenarioCorpusIntegrationTests.cs), [`DumpQueryPlanTests.cs`](../../tests/Interpreter.IntegrationTests/DumpQueryPlanTests.cs), [`ClrmdInstanceFieldInfoTests.cs`](../../tests/Interpreter.IntegrationTests/ClrmdInstanceFieldInfoTests.cs) | Medium | This proves the deliberately closed W2 v1 grammar over one generated strong root. It does not establish frames/statics, exact-null roots, member chains, `?.`, getters, arrays, broader operators, or representative production usefulness. |
| 2026-07-14 | SRM projection + ClrMD/counted raw memory | Structural signature/field projection, frozen admission, and dump-grounded getter execution | Project-owned structural module, type, and MethodDef identities feed one immutable method body/signature/return/local result; structural FieldDef identity is projected contextually and frozen separately into the admitted instruction plan. Closed E1/E2 admission rejects unsupported bodies before instruction zero. The real-dump E2 fixture reparses the counted physical header/body, proves the admitted `ldfld` operand is the correlated runtime `Int32` field, imports only exact four-byte evidence, executes direct and adjusted getters through `IMemoryModel`, and reproduces the prepared-memory transcript after dump reopen/rebind. | [`SrmMetadataProjection.cs`](../../src/Interpreter.Metadata.SRM/SrmMetadataProjection.cs), [`MetadataIdentityTests.cs`](../../tests/Interpreter.Tests/MetadataIdentityTests.cs), [`CompilerEmittedDifferentialTests.cs`](../../tests/Interpreter.Tests/CompilerEmittedDifferentialTests.cs), [`W3DumpGetterExecutionIntegrationTests.cs`](../../tests/Interpreter.IntegrationTests/W3DumpGetterExecutionIntegrationTests.cs) | Medium | This proves only non-generic, branchless, EH-free exact-`Int32` E1/E2 profiles with one directly declared same-module instance `FieldDef` and one concrete domain. Calls, branches, broader opcodes, generics, Portable PDBs, a second domain, product method evaluation, and evidence beyond the named fixture shapes remain outside the checkpoint. |
| 2026-07-15 | ClrMD/counted raw memory + product runner | Detached counterfactual method graph, exact/degraded fields, and close/reopen product replay | One generated full dump supplies the exact counted `GetMarkerSummary` and interpreted `CombineMarkers` bodies, exact root selection/owner/layout facts, and two correlated `Int32` fields. A product-owned binder copies those facts into canonical rooted identity and immutable persistent memory, after which ClrMD is disposed. Interpreted and body-free modeled plans each execute exact, partial, and unavailable rows; exact returns `0x26AF37BD`, degraded rows return typed provenance-bearing unknowns, and all six memory/request/plan/result artifacts reproduce after reopening and rebinding the dump. Disk SRM and CoreCLR are late comparison oracles only. | [`ClrmdDumpExecutionResolver.cs`](../../src/Interpreter.Host.Dump.ClrMD/ClrmdDumpExecutionResolver.cs), [`CounterfactualDumpExecutionBinder.cs`](../../src/Interpreter.Product.DumpDebugging/CounterfactualDumpExecutionBinder.cs), [`W4DumpCounterfactualExecutionIntegrationTests.cs`](../../tests/Interpreter.IntegrationTests/W4DumpCounterfactualExecutionIntegrationTests.cs) | Medium | This proves one branchless, EH-free, non-generic, strongly rooted, two-field/direct-call scenario and one exact no-effect pure model. It does not establish frame/argument recovery, arbitrary methods, representative incident recoverability, broader calls/opcodes/models, or behavior beyond the named fixture shapes. |

The strengthened W3 implementation checkpoint is exact commit `19c292f9f`; it follows the primary `12b6ef942`
implementation checkpoint with exact E2 receiver-encoding enforcement and positive `nop`, `sub`, and `mul` coverage.
The cumulative hand-written implementation range from `e7b6a4ace` is `+8,842/-1,650` LOC: `+5,362/-928`
production and `+3,480/-722` test/fixture LOC, plus 39 generated lock-file lines. Local headless verification passed a
zero-warning 15-project Release build, 103 milestone-selected unit tests, 67 fast integration tests, 5 ordinary dump
tests, 1 optimized-context dump test, the focused 2-test W3 lane, and both documentation guards, all with zero skips.
[GitHub Actions run 29372661656](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29372661656)
historically passed all four jobs at `12b6ef942`. [Run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at exact
strengthened checkpoint `19c292f9f`. [Run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
milestone-selected scope. That closure does not add product method evaluation or evidence beyond the named fixture shapes.

W4 exact closure commit `a819a08fd9ccdf926620c505732475990b242be9` passed documentation consistency,
build/Fast/unit, real-dump, and optimized-dump jobs in [GitHub Actions run
29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083). The real-dump job therefore
reproduces the W4.9 generated-dump row in the hosted headless environment within the milestone test selection; it does not
expand the scenario or establish evidence beyond the named fixture shapes. Final documentation-closure commit
`aaec73c5b987089addb539d3628de67bd815bd8f` passed the same four jobs in [run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230).

## Active decision and remaining evidence gaps

- SRM/PEReader is the decided backend for active prototype slices; see `mvp-backend-decision-record.md`.
- Alternatives are reconsidered only after an active fixture demonstrates a material SRM deficiency and a candidate adapter runs the same projected-contract corpus.
- The closed W3 structural signature, method-body, and FieldDef corpus is implemented. Confidence beyond that profile
  still requires representative generic/signature shapes, broader method bodies, malformed/partial artifacts, and
  (when admitted by a milestone) Portable PDB fixtures.

## Historical source-review record (2026-02-17)

The material below is preserved verbatim where possible because it records useful library behavior and the reasoning that preceded executable evidence. Terms such as “candidate,” “planned,” and “primary” describe that 2026-02 research context, not the current roadmap.

### AsmResolver evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Metadata completeness | `ModuleDefinition` + `DotNetDirectory` indicate strong metadata/CLR directory traversal surface for projection. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Validate with generic-heavy assembly projection prototype. |
| 2026-02-17 | IL body fidelity | `CilMethodBody` and related builder/serializer types indicate full instruction/EH modeling path. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Capture malformed-body behavior and diagnostics mapping. |
| 2026-02-17 | Symbol/PDB ingestion | `PdbImage` APIs support file/byte/reader loading and symbol/module enumeration. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Verify sequence-point parity against SRM path. |

### dnlib evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Metadata completeness | `ModuleDefMD` exposes high-level load APIs and direct metadata stream access. | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Validate normalization parity with AsmResolver for identity reconstruction. |
| 2026-02-17 | IL body fidelity | `MethodDef`, `CilBody`, and `MethodBodyReader` indicate robust method-body decoding controls (including generic context input). | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Run corrupted/partial method fixtures and capture miss-reason mapping. |
| 2026-02-17 | Symbol strategy breadth | Distinct portable/managed/windows PDB reader paths exist in `DotNet/Pdb` namespaces. | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Decide policy defaults for reader selection and fallback ordering. |

### ClrMD evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Runtime snapshot capability | `DataTarget`, `ClrRuntime`, `ClrHeap`, and `ClrThread` provide core dump/runtime observation surface required by our snapshot adapter. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Build thin adapter and document behavior on full vs partial dumps. |
| 2026-02-17 | Determinism and boundedness pressure | Thread/frame/root enumeration paths include caveats that require explicit budget and completeness handling in our contracts. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Implement budgeted enumeration wrapper and record partial outcomes. |
| 2026-02-17 | Cache policy relevance | `CacheOptions` indicates behavior/perf knobs that can alter repeated-call characteristics. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Evaluate fixed cache presets and measure drift/reproducibility. |

### Roslyn evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Expression parsing | `SyntaxFactory.ParseExpression` and syntax tree creation APIs provide stable front-end entry points. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Verify parser option effects across representative expression corpus. |
| 2026-02-17 | Semantic assistance | `CSharpCompilation.Create`/`CreateScriptCompilation` and `GetSemanticModel` support optional semantic-binding workflows. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Compare semantic outputs against runtime metadata truth in mismatch scenarios. |
| 2026-02-17 | Determinism constraints | Parse/compilation options are explicit and must be part of reproducible analysis inputs. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Define deterministic input bundle and replay harness. |

### Cross-backend conformance evidence

| Date | Scenario | Backends compared | Expected normalized parity | Evidence artifact | Status |
|---|---|---|---|---|---|
| 2026-02-17 | Source review parity pass | ClrMD, AsmResolver, dnlib, Roslyn | Library notes now map each backend to explicit adapter boundaries and risk categories. | `docs/lib/<library>/usage-notes.md` updates in this change. | Completed (analysis-only) |
| TBD | Generic-heavy method body with partial symbols | AsmResolver vs dnlib | Same result category + same miss-reason family | TBD | Historical plan (inactive) |
| TBD | Portable PDB sequence-point mapping with async method | AsmResolver vs SRM-oriented layer | Equivalent statement boundary semantics in debug-map contract | TBD | Historical plan (inactive) |

### 2026-02-17 deep-dive evidence additions

| Date | Backend | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|---|
| 2026-02-17 | ClrMD | Stack/root enumeration semantics | `ClrThread` stack/root enumeration includes bounded frame logic and cache-toggle behavior that must be normalized for completeness reporting. | `docs/lib/source-review-deep-dive.md`. | Low | Add adapter conformance case for cache-on/off and bounded frame stop reasons. |
| 2026-02-17 | AsmResolver | Reader policy controllability | `ModuleReaderParameters` + pluggable `IMethodBodyReader` indicate strong deterministic policy injection points. | `docs/lib/source-review-deep-dive.md`. | Low | Define and test one fixed reader policy profile end-to-end. |
| 2026-02-17 | dnlib | Portable PDB fidelity detail | `PortablePdbReader` sequence-point decoding preserves hidden points and document record transitions. | `docs/lib/source-review-deep-dive.md`. | Low | Validate debug-map projection retains hidden/document transition semantics. |
| 2026-02-17 | Roslyn | Parse strictness and mode variability | `ParseExpression(..., consumeFullText)` and `Create` vs `CreateScriptCompilation` show policy-sensitive parse/bind behavior. | `docs/lib/source-review-deep-dive.md`. | Low | Add deterministic corpus comparing strictness + compilation mode outputs. |

### 2026-02-17 source-review expansion pass

| Date | Backend | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|---|
| 2026-02-17 | ClrMD | Runtime ingestion and cache control | `DataTarget`, `ClrInfo`, `ClrRuntime`, and `CacheOptions` provide explicit lifecycle + cache semantics suitable for deterministic snapshot adapters when normalized. | Expanded review in `docs/lib/clrmd/usage-notes.md`. | Low | Validate with adapter spike on flush/cache-policy drift scenarios. |
| 2026-02-17 | AsmResolver | Metadata/CIL + symbol package layering | `AsmResolver.DotNet`, `AsmResolver.PE*`, and `AsmResolver.Symbols.Pdb` package split supports clean adapter seams for metadata, PE, and symbol projection. | Expanded review in `docs/lib/asmresolver/usage-notes.md`. | Low | Execute one end-to-end normalized method-body + sequence-point projection spike. |
| 2026-02-17 | dnlib | Method-body and symbol policy controllability | `ModuleDefMD`, `MethodBodyReader`, and `DotNet/Pdb/SymbolReaderFactory` provide controllable decode and symbol-path selection surfaces for failure-taxonomy experiments. | Expanded review in `docs/lib/dnlib/usage-notes.md`. | Low | Run malformed IL + embedded portable PDB parity scenarios. |
| 2026-02-17 | Roslyn | Deterministic expression front-end potential | `SyntaxFactory.ParseExpression`, `ParseSyntaxTree`, `CSharpCompilation.Create*`, and `GetSemanticModel` support a constrained parse/bind front-end contract with provenance capture. | Expanded review in `docs/lib/roslyn/usage-notes.md`. | Low | Build deterministic input-bundle prototype and compare script vs standard compilation modes. |
