# Backend capability evidence log (draft)

This log records concrete evidence for the capability axes in `backend-capability-matrix.md`.

It is intentionally lightweight in the conceptual phase: the goal is to make claims traceable, not to freeze implementation decisions.

## How to use this log

For each experiment/prototype spike:

1. add one row under the relevant backend section,
2. link to artifact(s) such as notes, scripts, or benchmark output,
3. classify confidence as `Low`, `Medium`, or `High`,
4. record open follow-ups required before raising confidence.

## Evidence quality rubric

| Confidence | Meaning | Typical proof quality |
|---|---|---|
| Low | Directional hypothesis only. | Design reasoning, source inspection, or single anecdotal read-through. |
| Medium | Reproducible in a narrow scenario. | One or more prototype runs with captured output and explicit constraints. |
| High | Reproducible across representative scenarios. | Repeated runs, edge-case coverage, and cross-backend comparison where applicable. |

## AsmResolver evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Metadata completeness | `ModuleDefinition` + `DotNetDirectory` indicate strong metadata/CLR directory traversal surface for projection. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Validate with generic-heavy assembly projection prototype. |
| 2026-02-17 | IL body fidelity | `CilMethodBody` and related builder/serializer types indicate full instruction/EH modeling path. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Capture malformed-body behavior and diagnostics mapping. |
| 2026-02-17 | Symbol/PDB ingestion | `PdbImage` APIs support file/byte/reader loading and symbol/module enumeration. | Source review in `docs/lib/asmresolver/usage-notes.md`. | Low | Verify sequence-point parity against SRM path. |

## dnlib evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Metadata completeness | `ModuleDefMD` exposes high-level load APIs and direct metadata stream access. | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Validate normalization parity with AsmResolver for identity reconstruction. |
| 2026-02-17 | IL body fidelity | `MethodDef`, `CilBody`, and `MethodBodyReader` indicate robust method-body decoding controls (including generic context input). | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Run corrupted/partial method fixtures and capture miss-reason mapping. |
| 2026-02-17 | Symbol strategy breadth | Distinct portable/managed/windows PDB reader paths exist in `DotNet/Pdb` namespaces. | Source review in `docs/lib/dnlib/usage-notes.md`. | Low | Decide policy defaults for reader selection and fallback ordering. |

## ClrMD evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Runtime snapshot capability | `DataTarget`, `ClrRuntime`, `ClrHeap`, and `ClrThread` provide core dump/runtime observation surface required by our snapshot adapter. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Build thin adapter and document behavior on full vs partial dumps. |
| 2026-02-17 | Determinism and boundedness pressure | Thread/frame/root enumeration paths include caveats that require explicit budget and completeness handling in our contracts. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Implement budgeted enumeration wrapper and record partial outcomes. |
| 2026-02-17 | Cache policy relevance | `CacheOptions` indicates behavior/perf knobs that can alter repeated-call characteristics. | Source review in `docs/lib/clrmd/usage-notes.md`. | Low | Evaluate fixed cache presets and measure drift/reproducibility. |

## Roslyn evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| 2026-02-17 | Expression parsing | `SyntaxFactory.ParseExpression` and syntax tree creation APIs provide stable front-end entry points. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Verify parser option effects across representative expression corpus. |
| 2026-02-17 | Semantic assistance | `CSharpCompilation.Create`/`CreateScriptCompilation` and `GetSemanticModel` support optional semantic-binding workflows. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Compare semantic outputs against runtime metadata truth in mismatch scenarios. |
| 2026-02-17 | Determinism constraints | Parse/compilation options are explicit and must be part of reproducible analysis inputs. | Source review in `docs/lib/roslyn/usage-notes.md`. | Low | Define deterministic input bundle and replay harness. |

## Cross-backend conformance evidence

| Date | Scenario | Backends compared | Expected normalized parity | Evidence artifact | Status |
|---|---|---|---|---|---|
| 2026-02-17 | Source review parity pass | ClrMD, AsmResolver, dnlib, Roslyn | Library notes now map each backend to explicit adapter boundaries and risk categories. | `docs/lib/<library>/usage-notes.md` updates in this change. | Completed (analysis-only) |
| TBD | Generic-heavy method body with partial symbols | AsmResolver vs dnlib | Same result category + same miss-reason family | TBD | Planned |
| TBD | Portable PDB sequence-point mapping with async method | AsmResolver vs SRM-oriented layer | Equivalent statement boundary semantics in debug-map contract | TBD | Planned |

## 2026-02-17 deep-dive evidence additions

| Date | Backend | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|---|
| 2026-02-17 | ClrMD | Stack/root enumeration semantics | `ClrThread` stack/root enumeration includes bounded frame logic and cache-toggle behavior that must be normalized for completeness reporting. | `docs/lib/source-review-deep-dive.md`. | Low | Add adapter conformance case for cache-on/off and bounded frame stop reasons. |
| 2026-02-17 | AsmResolver | Reader policy controllability | `ModuleReaderParameters` + pluggable `IMethodBodyReader` indicate strong deterministic policy injection points. | `docs/lib/source-review-deep-dive.md`. | Low | Define and test one fixed reader policy profile end-to-end. |
| 2026-02-17 | dnlib | Portable PDB fidelity detail | `PortablePdbReader` sequence-point decoding preserves hidden points and document record transitions. | `docs/lib/source-review-deep-dive.md`. | Low | Validate debug-map projection retains hidden/document transition semantics. |
| 2026-02-17 | Roslyn | Parse strictness and mode variability | `ParseExpression(..., consumeFullText)` and `Create` vs `CreateScriptCompilation` show policy-sensitive parse/bind behavior. | `docs/lib/source-review-deep-dive.md`. | Low | Add deterministic corpus comparing strictness + compilation mode outputs. |

## Open decisions linked to this log

- When a capability axis reaches `Medium` confidence for a primary candidate, update `backend-capability-matrix.md` notes and rationale.
- When critical MVP axes reach consistent `Medium`/`High` confidence, capture a decision record in `mvp-backend-decision-record.md`.

## 2026-02-17 source-review expansion pass

| Date | Backend | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|---|
| 2026-02-17 | ClrMD | Runtime ingestion and cache control | `DataTarget`, `ClrInfo`, `ClrRuntime`, and `CacheOptions` provide explicit lifecycle + cache semantics suitable for deterministic snapshot adapters when normalized. | Expanded review in `docs/lib/clrmd/usage-notes.md`. | Low | Validate with adapter spike on flush/cache-policy drift scenarios. |
| 2026-02-17 | AsmResolver | Metadata/CIL + symbol package layering | `AsmResolver.DotNet`, `AsmResolver.PE*`, and `AsmResolver.Symbols.Pdb` package split supports clean adapter seams for metadata, PE, and symbol projection. | Expanded review in `docs/lib/asmresolver/usage-notes.md`. | Low | Execute one end-to-end normalized method-body + sequence-point projection spike. |
| 2026-02-17 | dnlib | Method-body and symbol policy controllability | `ModuleDefMD`, `MethodBodyReader`, and `DotNet/Pdb/SymbolReaderFactory` provide controllable decode and symbol-path selection surfaces for failure-taxonomy experiments. | Expanded review in `docs/lib/dnlib/usage-notes.md`. | Low | Run malformed IL + embedded portable PDB parity scenarios. |
| 2026-02-17 | Roslyn | Deterministic expression front-end potential | `SyntaxFactory.ParseExpression`, `ParseSyntaxTree`, `CSharpCompilation.Create*`, and `GetSemanticModel` support a constrained parse/bind front-end contract with provenance capture. | Expanded review in `docs/lib/roslyn/usage-notes.md`. | Low | Build deterministic input-bundle prototype and compare script vs standard compilation modes. |
