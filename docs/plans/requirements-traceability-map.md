# Requirements-to-Evidence Traceability Map

This map distinguishes documented intent from implementation and validation. A proposal, planned fixture, empty project, or future milestone is not validation evidence.

## 1) Scope and status model

The active requirements cover the read-only dump evaluator through W3 in `future-work-planning.md`. Later method evaluation and virtual-debugger requirements are retained as research so they remain discoverable without appearing committed.

Status axes:

- **Roadmap:** `Active`, `Supporting`, or `Research`. Supporting rows record non-gating work outside W1.
- **Design:** `Documented`, `Partial`, or `Gap`.
- **Implementation:** `None`, `Prototype partial`, or `Implemented for stated scope`.
- **Validation:** `None`, `Partial`, or `Passing for stated scope`.

“Passing for stated scope” must name an executable test and its proof boundary.

## 2) Requirement ID conventions

- `PM-xx`: active post-mortem evaluator requirements.
- `VS-xx`: virtual-stepping research requirements.
- `X-xx`: cross-cutting requirements shared by multiple potential phases.

## 3) Traceability matrix

| ID | Roadmap | Requirement | Design | Implementation evidence | Validation evidence | Next evidence gate |
|---|---|---|---|---|---|---|
| PM-01 | Active | Treat the dump snapshot as immutable evidence. | Documented | `ClrmdDumpSession` holds one read-only stream for content identity and analysis lifetime; its public memory/object operations expose reads only. Interpreter virtual memory is a separate persistent value. | Passing for stated slice: `DumpMemoryEvidenceIntegrationTests.Full_dump_recovers_rooted_values_with_counted_memory_evidence` generates, hashes, reads, disposes, and deletes a dump without a target-write path. Exact-HEAD hosted dump evidence passed in run 29353198889. | Complete for W1; retain the lane when later evidence operations are added. |
| PM-02 | Active | Read object and method-body facts from real dump memory with provenance and honest partialness. | Documented for the W1 slice | The ClrMD adapter enumerates strong GCHandles under a scan/match cap, validates the selected handle slot and object-header method table with counted raw reads, and reads `Int32`, bounded/null strings, metadata, and complete tiny/fat method-body shapes from dump memory with exact/partial/unavailable/conflict/invalid outcomes and stable issue codes. The body path obtains the MethodDef RVA from counted dump metadata and reads the header, code, local-signature token, padding, and declared extra sections from dump memory. | Passing for stated slice: real-dump tests verify handle/object pointers, values, string partialness/null, missing field/method, type conflict, invalid address, scan-budget partialness, source IDs, retained reads, a fully dump-sourced tiny `RetOnly` body, and a compiler-emitted fat body with locals and two EH regions. A Normal-vs-Full fixture verifies that an omitted page stays partial/unavailable. Disposing/reopening the same dump preserves selected identities and complete replay bytes. Exact-HEAD hosted dump evidence passed in run 29353198889. | Complete for W1; extend only with a later admitted evidence shape. |
| PM-03 | Active | Parse and bind a restricted C# expression subset into a read-only query plan. | Documented for the first closed grammar | `Interpreter.Product.DumpQuery` parses and binds one exact non-null root plus one direct `.` field and optional bounded null/`Int32`/string coalescing into internal immutable query shapes; the evaluator never synthesizes or executes IL, and `?.` is rejected until the root model can represent exact null. | Passing for stated slice: `DumpQueryParserTests` covers admitted syntax and twenty-one malformed/out-of-scope cases; the generated dump scenario covers exact values, exact nullable-field null/coalescing, `?.` rejection, missing root/field, case sensitivity, unsupported type, and partial strings. | Add an exact-null root union before admitting null-conditional access; add a second member hop or frame root only when an incident scenario justifies its evidence and budget behavior. |
| PM-04 | Active | Make evaluation deterministic and resource-budgeted. | Documented | Machine budget is operational state separate from semantic state; whole-body admission precedes budget use; query text/identifiers/literals, raw reads, strings, and handle search are capped; handles/content hashes avoid process-random identity. | Passing for stated slices: result context records only bounds whose guarded operation was reached; parser, missing-root/member, foreign-snapshot, null, scalar, and string paths assert their distinct bound sets. Canonical result bytes and SHA-256 fingerprint remain identical after a fresh dump session rediscovers the module/root. Exact-HEAD hosted replay/bounds evidence passed in run 29353198889. | Complete for W1; each later bound type requires its own path-sensitive assertions. |
| PM-05 | Active | Block filesystem, network, process, native, reflection, and implicit code execution by default. | Documented for current query semantics | The query product exposes only root/field evidence reads and literal coalescing. Its parser rejects calls, indexing, chaining, arithmetic, assignments, and expanded syntax. The IL kernel separately whole-body rejects instructions outside its closed set. | Passing for stated slices: parser negatives and unsupported Boolean/coalescing scenarios fail closed; synthetic/compiler unsupported-call bodies leave state, budget, and events unchanged. Exact-HEAD hosted results passed in run 29353198889. | Complete for the current slices; later executable capabilities require explicit admission tests. |
| PM-06 | Active | Report semantic mode, completion, completeness, evidence status, effects, provenance, diagnostics, and evidence context as separate axes. | Documented | `EvaluationResult<T>` implements the independent axes with validated ordered provenance/diagnostics and an immutable context containing evidence source, explicit snapshot/module identity availability, fallback, and actually-applied deterministic bounds. Every DumpQuery path supplies this context and canonical replay includes it. | Passing for stated slices: envelope/context invariants and real-dump query assertions cover exact, partial, unavailable, blocked, invalid, no-effect, raw-read, runtime, transformation, foreign-snapshot, no-fallback, and bound-order behavior. Generic ClrMD projection proves a partial Int32 observation wrapper retains explanation but reports no decoded answer. Exact-HEAD hosted results passed in run 29353198889. | Complete for the current slices; extend the envelope only with a later admitted semantic mode. |
| PM-07 | Supporting | Treat external dump/PE/PDB/source inputs as untrusted and protect secret-bearing contents. | Documented outside W1 | A versioned malformed-minidump corpus and one-shot Windows worker are implemented as non-gating prototypes. | Partial, non-gating: fast corpus tests and one locally passing malformed-artifact worker checkpoint are retained. | No W1 gate. External-input cybersecurity requires a separately scoped future initiative before any external artifact product surface is proposed. |
| PM-08 | Active | Degrade cleanly when optimized dumps omit roots, locals, arguments, or `this`. | Documented | Missing roots/fields/methods produce typed unavailable results, and W2 never reinterprets missing evidence as null. Frame seeding still does not exist. A versioned generated optimized Release modeled-incident target keeps `this`, argument, local, static, and strong-root axes in a canonical report. | Passing for W1 missing-evidence semantics. Supporting measurement: raw member bytes are 5/5, attributable context 1/5, and product-query availability 1/5; only the strong root is attributable/queryable. Exact-HEAD hosted optimized-context evidence passed in run 29353198889. This is not representative private-production evidence. | Complete for W1 generated evidence. Representative private-production measurement remains a non-gating product-readiness follow-up. |
| VS-01 | Research | Define predictable Step Into/Over/Out across interpreted and modeled calls. | Documented research | No debugger controller/product implementation. | None. | Entry gate: W4 method execution plus deterministic pause/event contracts; Step Out through handlers additionally requires handler-transfer EH. |
| VS-02 | Research | Explore unknown branches through explicit counterfactual choices. | Documented research | None. | None. | Entry gate: validated unknown-aware method execution and bounded branch policy. |
| VS-03 | Research | Map steps through PDB, decompiled source, then IL fallback. | Documented research | No symbol/decompiler/debug-map implementation. | None. | Entry gate: virtual stepping is approved and a real stepping fixture needs source mapping. |
| VS-04 | Research | Represent async and dynamic outcomes explicitly. | Documented research | None. | None. | Entry gate: scenario-derived interpreter, call, generic, and EH prerequisites pass. |
| X-01 | Active | Preserve stable runtime/metadata/artifact identity, evidence provenance, and normalized miss reasons. | Documented for current slices | Dump metadata-root identity is MVID plus exact metadata length/SHA-256. A disk-backed module/method handle additionally carries whole-file artifact length/SHA-256, so a PE with changed IL cannot alias merely because its metadata root, MVID, timestamp, and image size were preserved. Dump snapshot/runtime-module identities remain distinct; evidence carries source IDs, counted reads, status, and issue codes. | Partial: copy-path identity, patched-IL artifact distinction, lookup ordering, wrong-artifact conflict, and foreign-snapshot rejection are covered; the dump fixture compares independently decoded metadata-root identities before correlation. The disk body remains only an equality oracle. | Add repeated-process runtime-instance identity coverage and a representative artifact-resolution candidate set. |
| X-02 | Active | Keep machine execution status, host pause reasons, diagnostics, and adapter miss reasons distinct but explicitly mapped. | Documented | Implemented `MachineRunStatus`/`ExecutionFailure` and `ClrmdEvidenceStatus`/`ClrmdValueIssue` contracts remain distinct. The future session-pause vocabulary and allowed mapping live only in architecture until a controller needs code. | Partial: engine tests cover ready/completed/budget/blocked/invalid behavior and adapter tests cover evidence categories. A session controller does not yet exist. | Introduce and test a session-pause contract only when a W4 controller is admitted; do not reserve it in the machine API meanwhile. |
| X-03 | Research | Suppress framework machinery through semantic models while preserving effects and explanations. | Documented research | None. | None. | Entry gate: two implemented models demonstrate a repeated extension need. |
| X-04 | Research | Keep environment/time/random behavior session-stable and explicit. | Documented research | None. | None. | Entry gate: an approved counterfactual scenario requires an environment intrinsic. |
| X-05 | Research | Make projection-backed collection reads bounded, version-aware, and fail-closed. | Documented research | None. | None. | Entry gate: a W2/W4 user scenario cannot be served by public-layout reads. |
| X-06 | Research | Reuse one semantic engine across concrete, unknown-aware, and abstract modes. | Partial hypothesis | A concrete domain and persistent memory execute constants, arguments, locals, arithmetic, and return through the domain-parametric machine. No second meaningful domain exists, so multi-mode reuse remains unproven. | Partial: lattice/memory tests plus a compiler-emitted CoreCLR differential corpus exercise the concrete seam. | A later gate requires a second domain over the same meaningful opcode handlers before any “one engine, all modes” claim. |

## 4) Current evidence gaps, in order

The W0 service-side gates are no longer an evidence gap: exact pushed completion commit
`3ece32a36eccc06a61025b1b35b58c09f6e4ed09` passed documentation consistency, locked restore, a zero-warning Release
build, 60 semantic and differential tests, 40 fast adapter/harness tests, and 3 required Windows dump tests in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT). This run includes the harness start/readiness failure regressions and the
repository-owned local-Markdown-link gate; it supersedes the narrower initial service baseline.

For the current W1 matrix, local headless verification at `f85545c0c` on 2026-07-14 passed locked restore, a
zero-warning/error 15-project Release build, 64/64 core tests, 63/63 fast integration tests, 3/3 ordinary dump tests,
and 1/1 optimized-context test with no skips or UI. [GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at
exact closure commit `e2580a8a8`.

1. Signature-derived frame admission and the first scenario-required memory opcode before expanding interpreted methods.
2. A second meaningful value domain before any shared multi-mode-engine claim.

These are post-W1 entry gates and do not reopen the completed W1 slice. Representative private-production measurement
and external-input cybersecurity are separately scoped, non-gating follow-ups rather than W1 evidence gaps.

## 5) Maintenance rules

Update this map when:

1. a product requirement or roadmap relation changes,
2. executable implementation evidence lands or is removed,
3. a validation test changes its proof boundary,
4. a design claim is found to have no corresponding evidence gate.

Before declaring a milestone complete, every active row it claims must name passing validation evidence or an explicit, accepted exclusion. Research rows never gate an active milestone merely because their proposals exist.
