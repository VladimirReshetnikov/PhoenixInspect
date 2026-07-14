# Requirements-to-Evidence Traceability Map

This map distinguishes documented intent from implementation and validation. A proposal, planned fixture, empty project, or future milestone is not validation evidence.

## 1) Scope and status model

The active requirements cover the read-only dump evaluator through W3 in `future-work-planning.md`. Later method evaluation and virtual-debugger requirements are retained as research so they remain discoverable without appearing committed.

Status axes:

- **Roadmap:** `Active` or `Research`.
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
| PM-01 | Active | Treat the dump snapshot as immutable evidence. | Documented | `ClrmdDumpSession` holds one read-only stream for content identity and analysis lifetime; its public memory/object operations expose reads only. Interpreter virtual memory is a separate persistent value. | Passing for stated slice: `DumpMemoryEvidenceIntegrationTests.Full_dump_recovers_rooted_values_with_counted_memory_evidence` generates, hashes, reads, disposes, and deletes a dump without a target-write path. | Add an external-artifact worker boundary before untrusted product use; expression evaluation must retain the same no-write boundary. |
| PM-02 | Active | Read object and method-body facts from real dump memory with provenance and honest partialness. | Documented for the W1 slice | The ClrMD adapter enumerates strong GCHandles under a scan/match cap, validates the selected handle slot and object-header method table with counted raw reads, and reads `Int32`, bounded/null strings, metadata, and complete tiny/fat method-body shapes from dump memory with exact/partial/unavailable/conflict/invalid outcomes and stable issue codes. The body path obtains the MethodDef RVA from counted dump metadata and reads the header, code, local-signature token, and declared extra sections from dump memory. | Passing for stated slice: the real-dump test verifies handle/object pointers, values, string partialness/null, missing field/method, type conflict, invalid address, scan-budget partialness, source IDs, retained reads, and a fully dump-sourced tiny `RetOnly` body; fast parser fixtures cover fat headers and chained EH sections. A Normal-vs-Full fixture verifies that an omitted page stays partial/unavailable. | Add a representative corrupt/hostile artifact corpus; the generated sparse case is a boundary proof, not corpus coverage. |
| PM-03 | Active | Parse and bind a restricted C# expression subset into a read-only query plan. | Documented for the first closed grammar | `Interpreter.Product.DumpQuery` parses and binds one exact non-null root plus one direct `.` field and optional bounded null/`Int32`/string coalescing into internal immutable query shapes; the evaluator never synthesizes or executes IL, and `?.` is rejected until the root model can represent exact null. | Passing for stated slice: `DumpQueryParserTests` covers admitted syntax and twenty-one malformed/out-of-scope cases; the generated dump scenario covers exact values, exact nullable-field null/coalescing, `?.` rejection, missing root/field, case sensitivity, unsupported type, and partial strings. | Add an exact-null root union before admitting null-conditional access; add a second member hop or frame root only when an incident scenario justifies its evidence and budget behavior. |
| PM-04 | Active | Make evaluation deterministic and resource-budgeted. | Documented | Machine budget is operational state separate from semantic state; whole-body admission precedes budget use; query text/identifiers/literals, raw reads, strings, and handle search are capped; handles/content hashes avoid process-random identity. | Passing for stated slices: machine transcripts and product envelopes serialize canonically; repeated W2 evaluation yields the same fingerprint; domain/memory tests cover stable semantic equality and content hashes. | Add a fresh-process replay runner when a stable external host protocol exists; keep wall-clock cancellation outside deterministic budgets. |
| PM-05 | Active | Block filesystem, network, process, native, reflection, and implicit code execution by default. | Documented for current slices | The query product exposes only root/field evidence reads and literal coalescing. Its parser rejects calls, indexing, chaining, arithmetic, assignments, and expanded syntax. The IL kernel separately whole-body rejects instructions outside its closed set. | Passing for stated slices: parser negatives and unsupported Boolean/coalescing scenarios fail closed; synthetic/compiler unsupported-call bodies leave state, budget, and events unchanged. | External artifact processing still requires the worker boundary; later executable capabilities require explicit allow/model/block tests. |
| PM-06 | Active | Report semantic mode, completion, completeness, evidence status, effects, provenance, and diagnostics as separate axes. | Documented | `EvaluationResult<T>` implements the independent axes with validated ordered provenance/diagnostics; ClrMD observation projections and W2 derived-query results use it without collapsing exact evidence, completion, or answer completeness. | Passing for stated slices: envelope invariant/golden tests plus real-dump query assertions cover exact, partial, unavailable, blocked, invalid, no-effect, raw-read, runtime, and transformation provenance. | Extend the same envelope only when a later counterfactual execution host exists; do not reuse adapter or machine status as a substitute. |
| PM-07 | Active | Treat dump/PE/PDB/source inputs as untrusted and protect secret-bearing contents. | Partial | Raw reads, strings, parser inputs, and adapter-owned traversal are bounded; dump identity/disposal share one stream; dumps are limited to 8 GiB; ClrMD's dump cache is limited to 256 MiB with stack-derived caches disabled; managed PEs are limited to 512 MiB on the actually opened stream before hashing; the replacement ClrMD locator refuses locator-backed acquisition; diagnostics/value display are payload-safe; full-dump targets use a cleared allowlisted environment and isolated directories. Worker isolation is not implemented. | Partial: dump/cache/PE size and method/query caps, malformed dump/PE open, locator refusal, environment isolation, Normal-vs-Full sparse evidence, unavailable addresses, foreign-snapshot conflicts, disposal, and redaction have executable coverage. A representative corrupt/hostile corpus and OS worker/access-control limits do not. ClrMD may still probe target-reported paths and load a full-path unsigned DAC outside its locator seam. | Add representative corrupt/hostile fixtures; constrained no-network/access-control worker plus trusted-DAC and AppContainer/low-privilege/VM policy remains mandatory before arbitrary external artifact exposure. Resource caps and a Job Object alone are not a sandbox. |
| PM-08 | Active | Degrade cleanly when optimized dumps omit roots, locals, arguments, or `this`. | Documented | Missing roots/fields/methods produce typed unavailable results, and W2 never reinterprets missing evidence as null. Frame seeding still does not exist; the deliberately strong-GCHandle fixture cannot estimate optimized-frame recoverability. | Partial: W2 missing-root/member cases pass, including no erroneous coalescing; no representative optimized-frame corpus exists. | Execute the versioned measurement protocol in `future-work-planning.md` against representative Release incident dumps. Do not invent a rate from the synthetic strong root. |
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

1. A representative corrupt/hostile artifact corpus plus a constrained external worker and proven AppContainer, low-privilege account, or VM policy before arbitrary artifact exposure.
2. Representative optimized-dump context-recovery measurements; the current deliberately strong-GCHandle fixture cannot supply a rate.
3. Signature-derived frame admission and the first scenario-required memory opcode before expanding interpreted methods.
4. A second meaningful value domain before any shared multi-mode-engine claim.

These are implementation-and-test gaps, not requests for additional standalone specifications.

## 5) Maintenance rules

Update this map when:

1. a product requirement or roadmap relation changes,
2. executable implementation evidence lands or is removed,
3. a validation test changes its proof boundary,
4. a design claim is found to have no corresponding evidence gate.

Before declaring a milestone complete, every active row it claims must name passing validation evidence or an explicit, accepted exclusion. Research rows never gate an active milestone merely because their proposals exist.
