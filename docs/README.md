# Documentation Index

This directory contains conceptual design artifacts for the IL interpreter and dump-time evaluation project.

## Current delivery focus

The only active product target is a deterministic, read-only expression evaluator grounded in a .NET dump. The W0
repository/toolchain reset and W1–W3 generated-dump vertical slices are implemented in-tree. W1/W2 provide
exact/partial evidence, fully dump-sourced tiny and compiler-emitted fat method bodies, a closed root-field query
grammar, typed snapshot-scoped root bindings, immutable plans that select their field once, exact-null and compatible
coalescing over `String` and `Nullable<Int32>` alongside direct `Int32`, canonical request/root-selection/plan
identities, path-accurate bounds, and honest no-answer completeness. The versioned W2 corpus contains 22 cases over
20 distinct expression texts and reproduces every complete result plus each successfully prepared plan after a fresh
open and rebind. W2 is complete for its non-cybersecurity scope: [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact closure commit `5bed47100`.

W3 hardened implementation checkpoint `19c292f9f` adds structural execution identities; SRM-projected static/instance signatures,
initialized locals, and FieldDefs; metadata-derived activation; immutable typed whole-body plans; injected persistent
memory; exact `ldfld`; and a latched target-null outcome. Its generated-dump proof replays the counted physical body,
correlates exactly one getter operand with one exact owner/field observation, imports that evidence without fabricating
missing cells, executes direct and adjusted getters, and reproduces the canonical prepared-memory result after dump
reopen/rebind. Local headless verification passed locked restore, the zero-warning fifteen-project Release build,
103 unit tests, 67 fast tests, 5 ordinary dump tests, 1 optimized-context test, and the focused 2-test W3 lane with no
skips. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at the
exact implementation commit. [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) then passed all four required
jobs at exact documentation-closure commit `de6cea124`, so W3 is formally closed for its defined non-cybersecurity
scope. A whole-file-identified disk PE remains only an independent oracle, never dump resolver input.

W4 is now admitted as an active design contract at
`proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`; its implementation has not landed.
The first scenario is deliberately branchless: from a generated dump, `DumpProbe.GetMarkerSummary` reads the two
marker fields and calls the direct `CombineMarkers` helper. W2 cannot express that question because its plan selects
only one field and executes no user IL. Exact evidence must yield the exact CoreCLR-agreeing `Int32` result. An admitted
partial or unavailable required marker must yield a typed unknown carrying stable evidence and transformation
provenance, never a concrete default; conflict and invalid evidence retain their distinct failure outcomes. Host
results use `CounterfactualExecution` with explicit policy, assumptions,
models/effects, and applied bounds; they do not claim historical replay. Instruction and preparation-traversal units
are consumed; maximum logical call depth is prepared/enforced and logical/frame high-water marks are reported.
Allocation remains unadmitted and is reported as absent/not applied rather than as a dormant guarantee. The exact,
degraded, differential, budget, and
same/fresh-session replay gates remain required future headless evidence, not current test results.

W1 is complete for its revised non-security dump-evidence scope: real reads; typed exact/partial/unavailable/conflict outcomes; honest answer completeness; stable identity/context/provenance; path-accurate bounds; fresh-session canonical replay; repository-wide headless execution; truthful topology; and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

A versioned malformed-minidump mutation corpus and a Windows x64 one-shot external-worker prototype are retained as
separately landed, non-gating work outside W1–W4. The worker's malformed-artifact checkpoint passed locally. Their
five hostile-corpus facts and the ExternalWorker test project provide no milestone validation: all current W1–W3 test
invocations exclude `Scope=Cybersecurity`. Restore/build intentionally remains repository-wide across all 15 projects,
including the worker projects and IntegrationTests assembly, as topology/compilation-health evidence only—not
cybersecurity behavioral evidence. Their presence does not admit arbitrary external artifacts.

The versioned optimized Release modeled-incident report is W1 generated-context evidence and records raw member bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5 while retaining all unavailable cases. It explicitly describes one generated modeled dump, not a representative private-production incident corpus, and therefore establishes no production recoverability rate; representative production measurement is not a W1 gate. Current in-process resource admission still limits dumps to 8 GiB, ClrMD's dump cache to 256 MiB with stack-derived caches disabled, and managed PEs to 512 MiB on the actually opened stream before hashing. Branches, CFG merge/fixpoint analysis, handler-transfer EH, allocation, broad call/model catalogs, generics, virtual stepping, whole-method abstract analysis, async/dynamic lifting, multi-application hosting, and other platform extensions are research backlog until their entry gates pass.

Current, active, and supporting documents use two dimensions: lifecycle (`Draft`, `Current`, `Complete`, `Historical`, `Superseded`) and roadmap relation (`Active`, `Supporting`, `Research`, `Reference`, `Historical`). Historical source/library records may use one combined status when a second axis would add no decision value. A designed or documented capability is not thereby implemented or validated. `Historical` and `Superseded` documents are preserved for rationale and source research; they are not instructions to recreate their proposed topology or backend choice.

## Information architecture

```text
docs/
├── lib/             # Project-specific notes for library snapshots under /lib
├── governance/      # Documentation process, standards, and lifecycle
├── plans/           # Milestones, sequencing, and decision gates
└── proposals/
    ├── architecture/ # Interpreter/core/runtime contracts
    ├── integration/  # External systems and host integration boundaries
    └── product/      # Product-facing experience and requirements
```

## Current documents

| Document | Area | Type | Lifecycle / roadmap | Summary |
|---|---|---|---|---|
| `../DESIGN-ARCHITECTURE-REVIEW.md` | Cross-cutting | Review | Complete · Reference | Repository-wide assessment and prioritized dump-first reset plan. |
| `proposals/product/post-mortem-debugging-feature-proposal.md` | Product | Proposal | Draft · Active | Active read-only dump evaluator, including the admitted-but-unimplemented W4 counterfactual-method slice, plus explicitly gated research phases. |
| `proposals/product/virtual-step-debugging-feature-proposal.md` | Product | Proposal | Draft · Research | Counterfactual virtual-stepping concept; not on the active roadmap. |
| `proposals/product/other-potential-applications.md` | Product | Strategy Note | Draft · Research | Speculative applications and reuse hypotheses; not delivery commitments. |
| `proposals/architecture/architecture-overview-proposal.md` | Architecture | Proposal | Current · Supporting | Top-level component map, runtime boundaries, and canonical data flow. |
| `proposals/architecture/restricted-dump-query-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W2 v1 grammar, typed root binding, immutable-plan, value-domain, diagnostics, provenance, and all-scenario replay contract. |
| `proposals/architecture/concrete-il-execution-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W3 metadata-derived activation, typed whole-body admission, dump-grounded field import, memory-opcode, exception-boundary, and replay contract. |
| `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md` | Architecture | Contract | Current · Active | Normative admitted W4 branchless `GetMarkerSummary`/`CombineMarkers` method-evaluation contract; implementation and validation are pending. |
| `proposals/architecture/module-architecture-proposal.md` | Architecture | Proposal | Superseded · Reference | Granular responsibility catalog; not the active physical-package plan. |
| `proposals/architecture/minimal-interfaces-proposal.md` | Architecture | Design Sketch | Historical · Reference | Pre-evidence API sketches; current prototype contracts and contract-just-ahead-of-code policy supersede them. |
| `proposals/architecture/il-interpreter-framework-proposal.md` | Architecture | Proposal | Draft · Supporting | Core interpreter architecture and execution model. |
| `proposals/architecture/mvp-abstract-domain-proposal.md` | Architecture | Proposal | Draft · Research | Candidate abstract domains and analysis behavior for a later research phase. |
| `proposals/architecture/technical-stack-proposal.md` | Architecture | Proposal | Draft · Active | Language/runtime/dependency and package layout choices. |
| `proposals/architecture/call-model-and-effects.md` | Architecture | Proposal | Draft · Supporting | Call classification, fallback behavior, and effect lattice contract for later method evaluation. |
| `proposals/architecture/generic-context-resolver-proposal.md` | Architecture | Proposal | Draft · Research | Generic-context reconstruction strategy for later dump-backed method interpretation. |
| `proposals/architecture/prototype-solution-structure-proposal.md` | Architecture | Current Snapshot | Current · Active | Physical prototype layout and narrow implemented proof boundary. |
| `proposals/architecture/prototype-interface-catalog-proposal.md` | Architecture | Current Snapshot | Current · Active | Narrow contracts exercised by the dump-evidence and concrete-IL proofs; explicitly unstable. |
| `proposals/architecture/state-and-domain-model-proposal.md` | Architecture | Proposal | Draft · Supporting | Candidate execution-state, domain, and provenance semantics. |
| `proposals/architecture/testing-strategy-proposal.md` | Architecture | Strategy | Current · Active | Present executable evidence, W0–W4 test gates, and explicitly deferred research suites. |
| `proposals/architecture/semantic-modeling-proposal.md` | Architecture | Proposal | Draft · Research | BCL/IL semantic lifting and projection concepts for later phases. |
| `proposals/architecture/special-semantics-registry-proposal.md` | Architecture | Proposal | Draft · Research | Candidate unified registry for later modeled semantics. |
| `proposals/architecture/opcode-support-matrix-proposal.md` | Architecture | Proposal | Current · Supporting | Implemented closed W3 E1/E2 encodings and explicit gates for later opcode expansion. |
| `proposals/architecture/perf-and-benchmarks-proposal.md` | Architecture | Proposal | Draft · Research | Aspirational performance model and benchmark gates. |
| `proposals/architecture/virtual-step-debugging-implementation-proposal.md` | Architecture | Proposal | Draft · Research | Candidate stepping control plane, stop reasons, model frames, and undo. |
| `proposals/architecture/debug-map-design-proposal.md` | Architecture | Proposal | Draft · Research | Candidate source/IL mapping strategy for virtual stepping. |
| `proposals/architecture/virtual-tasks-proposal.md` | Architecture | Proposal | Draft · Research | Async/await virtualization research. |
| `proposals/architecture/dynamic-calls-proposal.md` | Architecture | Proposal | Draft · Research | Dynamic call-site lifting research. |
| `proposals/integration/clrmd-integration-proposal.md` | Integration | Proposal | Draft · Active | ClrMD + PE/PDB integration architecture and binding-layer model. |
| `proposals/integration/clrmd-apis-usage-tutorial-proposal.md` | Integration | Tutorial Proposal | Draft · Reference | Scenario-first tutorial mapping ClrMD APIs to our runtime snapshot and IL acquisition workflows. |
| `proposals/integration/pe-pdb-reader-integration-proposal.md` | Integration | Proposal | Draft · Active | Artifact-resolution design for PE/PDB loading and identity validation. |
| `proposals/integration-test-plan.md` | Integration | Verification Plan | Current · Active | Fast semantic/differential coverage plus the bounded real dump-memory evidence lane and provenance matrix. |
| `plans/future-work-planning.md` | Planning | Plan | Current · Active | LOC-bounded, evidence-led W0–W4 delivery plan and research gates. |
| `plans/requirements-traceability-map.md` | Planning | Matrix | Current · Active | Requirements mapped separately to design, implementation, and validation evidence. |
| `lib/README.md` | Library Notes | Index | Current · Reference | Navigation and policy for active decisions versus preserved source research. |
| `lib/clrmd/usage-notes.md` | Library Notes | Note | Historical · Reference | Preserved ClrMD source research; active behavior is established by adapter code and executable fixtures. |
| `lib/clrmd/intro-tutorial.md` | Library Notes | Tutorial | Historical · Reference | Contributor introduction to ClrMD dump/runtime/heap/stack inspection workflows. |
| `lib/clrmd/source-scan.md` | Library Notes | Source Scan | Historical · Research | Source-driven ClrMD lifecycle, cache, stack, and heap adapter observations. |
| `lib/asmresolver/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved AsmResolver candidate research; SRM/PEReader is the active backend. |
| `lib/asmresolver/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for the AsmResolver object model; not an implementation roadmap. |
| `lib/asmresolver/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved AsmResolver reader-policy, CIL, and PDB ingestion findings. |
| `lib/dnlib/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved dnlib fallback/compatibility research. |
| `lib/dnlib/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for dnlib metadata, method-body, and symbol APIs. |
| `lib/dnlib/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved dnlib module, method-body, and Portable PDB findings. |
| `lib/cecil/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved Mono.Cecil reader-policy and adapter research. |
| `lib/cecil/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for Mono.Cecil policy, method bodies, symbols, and resolution. |
| `lib/cecil/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved Mono.Cecil load-mode, symbol-provider, and resolver findings. |
| `lib/roslyn/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved Roslyn front-end and conservative-inference research for W2. |
| `lib/roslyn/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for Roslyn parse/bind workflows. |
| `lib/roslyn/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved Roslyn parse/compilation/semantic-boundary findings. |
| `lib/backend-capability-matrix.md` | Library Notes | Matrix | Historical · Research | Pre-decision comparison retained alongside the active SRM decision. |
| `lib/adapter-conformance-checklist.md` | Library Notes | Checklist | Draft · Reference | Backend-neutral adapter and miss-reason guidance; apply only when an active fixture needs it. |
| `lib/backend-evidence-log.md` | Library Notes | Evidence Log | Current · Reference | Current executable SRM and ClrMD/raw-memory evidence followed by the preserved 2026-02 source-review record. |
| `lib/mvp-backend-decision-record.md` | Library Notes | Decision Record | Current · Active | SRM/PEReader decision for active prototype slices, with evidence-based revisit triggers. |
| `lib/pe-pdb-reader-api-comparison.md` | Library Notes | Comparison Note | Historical · Research | Preserved pre-decision API comparison; not the active backend recommendation. |
| `lib/source-review-deep-dive.md` | Library Notes | Source Review | Historical · Research | Preserved cross-library source review. |
| `lib/source-tour-workbook.md` | Library Notes | Workbook | Historical · Research | Optional source-tour exercises for a future evidence-triggered backend evaluation. |
| `governance/documentation-organization-proposal.md` | Governance | Policy | Current · Supporting | Lightweight information architecture, status vocabulary, and evidence-language rules. |
| `governance/project-faq.md` | Governance | Guide | Draft · Supporting | FAQ for contributors and stakeholders about scope, expectations, and direction in the concept phase. |
| `governance/terminology-glossary.md` | Governance | Glossary | Draft · Supporting | Canonical terminology definitions and cross-document language alignment guidance. |

## Suggested reading paths

### Active delivery path

1. `../DESIGN-ARCHITECTURE-REVIEW.md`
2. `proposals/product/post-mortem-debugging-feature-proposal.md`
3. `proposals/architecture/restricted-dump-query-contract-proposal.md`
4. `proposals/architecture/concrete-il-execution-contract-proposal.md`
5. `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`
6. `proposals/architecture/architecture-overview-proposal.md`
7. `proposals/architecture/prototype-solution-structure-proposal.md`
8. `lib/mvp-backend-decision-record.md`
9. `plans/future-work-planning.md`
10. `proposals/architecture/testing-strategy-proposal.md`
11. `proposals/integration-test-plan.md`
12. `proposals/integration/clrmd-integration-proposal.md`
13. `proposals/integration/pe-pdb-reader-integration-proposal.md`

The paths below explore broader design possibilities. They are research/reference paths, not delivery sequences; inclusion does not imply roadmap commitment or implementation evidence.

### Product research path

1. `proposals/product/post-mortem-debugging-feature-proposal.md`
2. `proposals/product/virtual-step-debugging-feature-proposal.md`
3. `proposals/product/other-potential-applications.md`
4. `proposals/architecture/il-interpreter-framework-proposal.md`
5. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
6. `proposals/architecture/debug-map-design-proposal.md`

### Platform strategy research path

1. `proposals/architecture/architecture-overview-proposal.md`
2. `proposals/product/other-potential-applications.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`
5. `proposals/architecture/semantic-modeling-proposal.md`
6. `proposals/architecture/mvp-abstract-domain-proposal.md`

### Architecture research path

1. `proposals/architecture/architecture-overview-proposal.md`
2. `proposals/architecture/il-interpreter-framework-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
5. `proposals/architecture/mvp-abstract-domain-proposal.md`
6. `proposals/architecture/call-model-and-effects.md`
7. `proposals/architecture/debug-map-design-proposal.md`
8. `proposals/architecture/dynamic-calls-proposal.md`
9. `proposals/architecture/virtual-tasks-proposal.md`
10. `proposals/architecture/semantic-modeling-proposal.md`
11. `proposals/architecture/special-semantics-registry-proposal.md`
12. `proposals/architecture/opcode-support-matrix-proposal.md`
13. `proposals/architecture/generic-context-resolver-proposal.md`

### Historical architecture sketches

1. `proposals/architecture/minimal-interfaces-proposal.md`
2. `proposals/architecture/module-architecture-proposal.md`

These sketches explain earlier reasoning, but neither their API shapes nor their project-per-seam package map is normative. Start from the current prototype source and `proposals/architecture/prototype-solution-structure-proposal.md` instead.

### Semantic-modeling research path

1. `proposals/architecture/call-model-and-effects.md`
2. `proposals/architecture/state-and-domain-model-proposal.md`
3. `proposals/architecture/semantic-modeling-proposal.md`
4. `proposals/architecture/special-semantics-registry-proposal.md`
5. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`

### Virtual-debugger research path

1. `proposals/product/virtual-step-debugging-feature-proposal.md`
2. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`
5. `proposals/architecture/debug-map-design-proposal.md`
6. `proposals/architecture/virtual-tasks-proposal.md`

### Dynamic-dispatch research path

1. `proposals/architecture/call-model-and-effects.md`
2. `proposals/architecture/dynamic-calls-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`

### Runtime integration path

1. `lib/mvp-backend-decision-record.md`
2. `proposals/integration/clrmd-integration-proposal.md`
3. `proposals/integration/clrmd-apis-usage-tutorial-proposal.md`
4. `proposals/integration/pe-pdb-reader-integration-proposal.md`
5. `lib/backend-evidence-log.md`

Generic-context reconstruction and broad call/effect modeling remain later research topics. The active W4 contract
admits only its named direct `GetMarkerSummary` to `CombineMarkers` call shape; it does not promote those broader
research designs.

### Process and governance path

1. `governance/project-faq.md`
2. `governance/documentation-organization-proposal.md`
3. `governance/terminology-glossary.md`
4. `plans/future-work-planning.md`
5. `plans/requirements-traceability-map.md`

## Repository license

This project is distributed under the **MIT-0 (MIT No Attribution)** license. See the repository-level [`LICENSE`](../LICENSE) file for full terms.

## Naming and placement conventions

- Place new design docs under `docs/proposals/<area>/` with `*-proposal.md` suffix.
- Place sequencing/roadmap artifacts under `docs/plans/`.
- Place process and standards docs under `docs/governance/`.
- Prefer stable filenames; if renaming, update links in both root `README.md` and this index.

## Documentation WIP policy

For current-state questions, prefer evidence in this order: executable tests and their recorded results, prototype source/solution, active decision records and plans, then supporting proposals. Historical/research documents explain possibilities and prior reasoning; they do not override those sources.

Do not add a new subsystem proposal merely because an existing plan mentions one. Add or deepen design only when the active vertical slice needs the contract, and pair the change with an executable validation step whenever practical. When evidence changes, update the active snapshot/traceability entry and mark superseded guidance explicitly instead of silently leaving two “current” answers.
