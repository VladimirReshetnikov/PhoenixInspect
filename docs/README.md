# Documentation Index

This directory contains design artifacts for the IL interpreter and dump-time evaluation project.

## Start here

- **Using PhoenixInspect:** [`Preview Quickstart`](preview-quickstart.md) — run the demo, open your own dump, and
  see the exact expression subset and value domain supported today.
- **Choosing a host:** [`Hosts`](hosts.md) — the console host and the Windows desktop shell.
- **What has actually been proven:** [`Integration Test Plan`](proposals/integration-test-plan.md).

Everything below is the design and milestone record behind those three.

## Current delivery focus

The only active product target is a deterministic, read-only expression evaluator grounded in a .NET dump. Delivery
proceeds through evidence-led vertical slices: the W0 repository/toolchain reset and the W1–W7 slices are closed and
implemented in-tree, and W8 is the sole active design/implementation sequence. The consolidated record of every
closed milestone — exact closure commits, verification counts, hosted CI runs, realized-LOC accounting, the
milestone-scoped hosted-only waivers for W5/W6/W7, and the standing caveats — is the
[`Milestone History`](plans/milestone-history.md); the completed path-forward plans and normative contracts in the
table below remain the per-milestone authorities.

The active [`Post-W7 Path Forward`](plans/post-w7-path-forward.md) defines W8 as an inclusive additive
`StaticFieldExpressionV2` design. W8.1 is implemented and locally validated; its
[`physical-truth disposition`](plans/w8-1-physical-truth-disposition.md) freezes exact compiler/PDB, constructed-runtime,
storage, literal, frame-root, and assignability evidence before product contracts. Constructed, thread-relative,
module-RVA, literal, and exact memory-homed frame-value branches are admitted. Context-relative storage and
selected-frame generic arguments remain typed non-admitted, and register homes are unproven. W8.2 is active. Its
immutable expression-contract foundation, detached `FrameValueExpressionV1` syntax, one shared bounded Core ECMA
signature grammar, Product event adapter, and caller-supplied selected-method lexical evidence envelope have landed.
Checkpoint `5fd87a3e5` also lands exact metadata source ends and token catalogs, raw and role-classified TypeDefs, the
complete TypeSpec graph, exact FieldSig identity, GenericParam declaration/catalog/owner-set/binding ledgers,
interface/constraint edge aggregates, provisional construction classification, and Nullable construction
preservation. The [metadata-authority cutover](plans/w8-2-metadata-authority-cutover.md) is complete for its defined
scope at checkpoint `d4d5f745c`: physical tables, definition authority, compiler-name mappings, W7 compatibility
certificates and portfolios, constraint authority, reference tables, named-TypeDef chains, multi-module TypeRef
resolution, core-role selection, immediate-base edges, semantic classification, bounded ancestry, constraint-target
joins, and the retyped TypeSpec/closed-type/interface-edge surface are all catalog-issued, every caller-authored
issuer is deleted, and an assembly-wide reflection guard enforces the boundary. W8.3 through W8.8 have since landed
the host-owned metadata producer, the definition-side name/construction/member binder, scope-precise import and alias
binding — including a whole-owner TypeSpec alias whose target is decoded from its physical blob rather than from the
spelling — the runtime construction/storage/value mapping over real dumps, lexical completeness, interface-implementation
authority, and the sixteen-step product composition. W8.9's thirty-five-incident portfolio is the active work: eighteen
rows execute end to end today, and every row that does not stops at a named landed boundary recorded as a produced
finding. The umbrella scale remains `~100K LOC`, split primarily into `~10K LOC`
checkpoints, and the meaningful portfolio minimum is now thirty-five independent incidents.

Caveat: current evidence covers only the named generated fixtures and explicitly admitted input shapes. No result
through W8.1 establishes behavior for other artifact shapes, and only the landed W8.2 contract and metadata-proof
families are current implementation evidence; they do not yet govern downstream acquisition, binding,
runtime/storage, or evaluation. Unlanded W8.2 work and W8.3–W8.10 remain plans. The library snapshots formerly under
`lib/` were verbatim reference material, never project-authored scope; they have been removed from the working tree
and are preserved at the annotated tag `last-with-lib` (see [`lib/README.md`](lib/README.md)). The full caveat and
resource-bound record lives in the [`Milestone History`](plans/milestone-history.md#caveats-bounds-and-research-backlog).

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
    ├── original/     # Restored pre-redesign originals (historical planned scope)
    └── product/      # Product-facing experience and requirements
```

## Current documents

| Document | Area | Type | Lifecycle / roadmap | Summary |
|---|---|---|---|---|
| `plans/post-w7-path-forward.md` | Planning | Plan | Current · Active | W8 sequence with W8.1 physical gates complete and W8.2 active: additive V2/frame contracts, nested generic owners, scoped aliases/current-member/extern/using-static binding, constraints, accessibility, assignability, admitted stored/literal/frame strategies, suffix composition, thirty-five-incident minimum, and corrected decision gate. |
| `plans/w8-declared-member-catalog-plan.md` | Planning | Plan | Current · Active | Declaration-side Constant (0x0B) and Property (0x17) table catalogs: verified reader preconditions, the parent-side Constant completeness proof, ownership proofs that avoid the unreadable PropertyMap and MethodSemantics tables, the accessibility refusal that keeps a same-name property blocking without them, the ordered landing sequence with its enumerated golden re-freezes, and a closing outcome section recording the two places the plan predicted wrongly: incident 12 stayed manifest-only because its measured blocker is a second definition of its own owner, and the literal-constant counterfactual arm was left unwired because no executable row declares it. |
| `plans/w8-declared-member-catalog-step6-notes.md` | Planning | Plan | Current · Active | Measured preparation notes for retiring the Constant caller seam: the settled product-side shape, the schema bumps that keep a reused slot from silently re-meaning, and the synthetic Constant-catalog fixture the two consumer test files need before the seam can be deleted. |
| `plans/w8-1-physical-truth-disposition.md` | Planning | Evidence record | Complete · Active | Authoritative W8.1 checkpoint ledger and branch disposition: constructed/thread/RVA/literal/frame admission; context/frame-generic non-admission; exact array topology; W8.2 API and thirty-five-incident consequences. |
| `plans/w8-2-metadata-authority-cutover.md` | Planning | Evidence record | Complete · Active | Completed non-circular complete-table authority: TypeDef ownership, NestedClass parents, member pointers, GenericParam owners, MethodDef declarations, compiler-style arity mapping, reference tables, TypeRef resolution, core roles, semantic classification, bounded ancestry, constraint targets, and total public-issuer removal. |
| `plans/milestone-history.md` | Planning | Evidence record | Current · Reference | Consolidated delivery ledger for closed W1–W7 and the completed W8 checkpoints: exact closure commits, verification counts, hosted CI runs, realized-LOC accounting, waivers, and standing caveats. |
| `plans/post-w6-path-forward.md` | Planning | Plan | Complete · Reference | Closed W7 static-field expression/context sequence: physical truth, immutable contracts, one-parser admission, fully qualified and selected-frame/PDB/import binding, counted values, suffix composition, sixteen-dump portfolio, selected successor category, and milestone-scoped hosted disposition. |
| `plans/post-w5-path-forward.md` | Planning | Plan | Complete · Reference | Closed W6 opt-in bounded member-chain contract, certified terminal data-property dependency, evidence/result matrix, headless synthetic corpus, selected context-acquisition successor, and milestone-scoped hosted disposition. |
| `plans/post-w4-path-forward.md` | Planning | Plan | Complete · Reference | Closed W5 expression-to-result product composition, headless reference consumer, meaningful synthetic usefulness gate, milestone-scoped hosted-gate waiver, and evidence-selected W6 decision. |
| `proposals/product/post-mortem-debugging-feature-proposal.md` | Product | Proposal | Draft · Active | Read-only dump evaluator direction through closed W7, completed W8.1 physical evidence, and the W8.2+ nested/constructed static, scoped-context, stored/literal, and admitted frame-value product path. |
| `proposals/product/virtual-step-debugging-feature-proposal.md` | Product | Proposal | Draft · Research | Counterfactual virtual-stepping concept; not on the active roadmap. |
| `proposals/product/other-potential-applications.md` | Product | Strategy Note | Draft · Research | Speculative applications and reuse hypotheses; not delivery commitments. |
| `proposals/architecture/architecture-overview-proposal.md` | Architecture | Proposal | Current · Supporting | Top-level component map, runtime boundaries, canonical data flow, closed W1–W7 evidence, completed W8.1 physical gates, and the active W8.2 contract/proof frontier. |
| `proposals/architecture/csharp-expression-front-end-contract-proposal.md` | Architecture | Contract | Current · Supporting | Normative sole pinned Roslyn expression parse, stable invalid/unsupported mapping, implemented W2/W5/W6/W7 admission, completed W8.1 evidence dispositions, and unimplemented W8.2 V2/frame projection boundary. |
| `proposals/architecture/restricted-dump-query-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W2 v1 admitted shape, typed root binding, immutable-plan, value-domain, diagnostics, provenance, and all-scenario replay contract. |
| `proposals/architecture/concrete-il-execution-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W3 metadata-derived activation, typed whole-body admission, dump-grounded field import, memory-opcode, exception-boundary, and replay contract. |
| `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md` | Architecture | Contract | Current · Reference | Normative closed W4 branchless `GetMarkerSummary`/`CombineMarkers` method-evaluation contract and executable evidence record. |
| `proposals/architecture/module-architecture-proposal.md` | Architecture | Proposal | Superseded · Reference | Granular responsibility catalog; not the active physical-package plan. |
| `proposals/architecture/minimal-interfaces-proposal.md` | Architecture | Design Sketch | Historical · Reference | Pre-evidence API sketches; current contracts and contract-just-ahead-of-code policy supersede them. |
| `proposals/architecture/il-interpreter-framework-proposal.md` | Architecture | Proposal | Draft · Supporting | Core interpreter architecture and execution model. |
| `proposals/architecture/mvp-abstract-domain-proposal.md` | Architecture | Proposal | Draft · Research | Candidate abstract domains and analysis behavior for a later research phase. |
| `proposals/architecture/technical-stack-proposal.md` | Architecture | Proposal | Draft · Active | Language/runtime/dependency and package layout choices, including the durable sole-Roslyn-parser boundary, implemented W7 consumer/report reuse, W8.1 evidence projects, landed W8.2 expression/frame/lexical and metadata-proof contracts, and the missing host producer/binder/runtime/evaluator. |
| `proposals/architecture/call-model-and-effects.md` | Architecture | Proposal | Draft · Supporting | Call classification, fallback behavior, and effect lattice contract for later method evaluation. |
| `proposals/architecture/generic-context-resolver-proposal.md` | Architecture | Proposal | Draft · Research | Generic-context reconstruction strategy for later dump-backed method interpretation. |
| `proposals/architecture/prototype-solution-structure-proposal.md` | Architecture | Current Snapshot | Current · Active | Physical layout and narrow implemented proof boundary. |
| `proposals/architecture/prototype-interface-catalog-proposal.md` | Architecture | Current Snapshot | Current · Active | Narrow contracts exercised through closed W7 and completed W8.1, plus immutable W8.2 expression/frame/lexical and source-anchored metadata-proof families; consumers and host production remain incomplete; unstable. |
| `proposals/architecture/state-and-domain-model-proposal.md` | Architecture | Proposal | Draft · Supporting | Candidate execution-state, domain, and provenance semantics. |
| `proposals/architecture/testing-strategy-proposal.md` | Architecture | Strategy | Current · Active | Present executable evidence through closed W7, completed W8.1 physical gates, and landed W8.2 detached proof tests, plus remaining consumer/generated/headless/replay obligations and a thirty-five-incident portfolio minimum. |
| `proposals/architecture/semantic-modeling-proposal.md` | Architecture | Proposal | Draft · Research | BCL/IL semantic lifting and projection concepts for later phases. |
| `proposals/architecture/special-semantics-registry-proposal.md` | Architecture | Proposal | Draft · Research | Candidate unified registry for later modeled semantics. |
| `proposals/architecture/opcode-support-matrix-proposal.md` | Architecture | Proposal | Current · Supporting | Implemented closed W3 E1/E2 encodings and explicit gates for later opcode expansion. |
| `proposals/architecture/perf-and-benchmarks-proposal.md` | Architecture | Proposal | Draft · Research | Aspirational performance model and benchmark gates. |
| `proposals/architecture/fault-injected-dump-corpus-proposal.md` | Architecture | Proposal | Draft · Research | Adversarial capture corpus: fault-injected rich-application dumps as an invariant-accepted evidence tier stressing the evaluator's degradation and non-admission paths; determinism reconciliation, fault catalog, and phased sketch. |
| `proposals/architecture/virtual-step-debugging-implementation-proposal.md` | Architecture | Proposal | Draft · Research | Candidate stepping control plane, stop reasons, model frames, and undo. |
| `proposals/architecture/debug-map-design-proposal.md` | Architecture | Proposal | Draft · Research | Candidate source/IL mapping strategy for virtual stepping. |
| `proposals/architecture/virtual-tasks-proposal.md` | Architecture | Proposal | Draft · Research | Async/await virtualization research. |
| `proposals/architecture/dynamic-calls-proposal.md` | Architecture | Proposal | Draft · Research | Dynamic call-site lifting research. |
| `proposals/integration/clrmd-integration-proposal.md` | Integration | Proposal | Current · Active | ClrMD + PE/PDB binding architecture through W7 and completed W8.1 exact-construction, strategy-specific storage, memory-homed frame-root, and typed non-admission evidence. |
| `proposals/integration/clrmd-apis-usage-tutorial-proposal.md` | Integration | Tutorial Proposal | Draft · Reference | Scenario-first tutorial mapping ClrMD APIs to our runtime snapshot and IL acquisition workflows. |
| `proposals/integration/pe-pdb-reader-integration-proposal.md` | Integration | Proposal | Current · Active | Artifact-resolution design through W7, completed W8.1 physical evidence, landed W8.2 detached metadata proofs, and remaining host production/scoped binding. |
| `proposals/integration-test-plan.md` | Integration | Verification Plan | Current · Active | Fast semantic/differential and real dump-memory evidence through closed W7, completed W8.1, and landed W8.2 metadata proofs, plus remaining consumer/product replay and thirty-five-incident portfolio obligations. |
| `proposals/original/README.md` | Architecture | Index | Historical · Research | Index of the restored pre-redesign originals recording the tentative-future-direction planned scope. |
| `proposals/original/architecture-overview-proposal.md` | Architecture | Restored Proposal | Historical · Research | Original multi-application platform architecture map: dump debugging, live speculation, static analysis, sandboxed execution. |
| `proposals/original/opcode-support-matrix-proposal.md` | Architecture | Restored Proposal | Historical · Research | Original tiered opcode-support planning matrix, drafted ahead of executable evidence. |
| `proposals/original/testing-strategy-proposal.md` | Architecture | Restored Strategy | Historical · Research | Original testing taxonomy for the full planned interpreter scope, including abstract interpretation and virtual stepping. |
| `proposals/original/prototype-solution-structure-proposal.md` | Architecture | Restored Snapshot | Historical · Research | Original 42-project scaffold description; the placeholder projects themselves remain removed. |
| `proposals/original/prototype-interface-catalog-proposal.md` | Architecture | Restored Snapshot | Historical · Research | Original draft public-interface catalog of the scaffold. |
| `proposals/original/integration-test-plan.md` | Integration | Restored Plan | Historical · Reference | Original smallest-real-integration-test plan; implemented and superseded. |
| `proposals/original/future-work-planning.md` | Planning | Restored Plan | Historical · Research | Original milestone roadmap across virtual stepping, async/dynamic lifting, semantic modeling, analysis, and sandboxed execution. |
| `plans/future-work-planning.md` | Planning | Plan | Current · Supporting | Detailed evidence-led W0–W4 delivery record and research entry gates. |
| `plans/requirements-traceability-map.md` | Planning | Matrix | Current · Active | Requirements mapped separately to design, implementation, and validation evidence, including the W8.2 proof-versus-consumer boundary. |
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
| `lib/mvp-backend-decision-record.md` | Library Notes | Decision Record | Current · Active | SRM/PEReader decision for active development slices, with evidence-based revisit triggers. |
| `lib/pe-pdb-reader-api-comparison.md` | Library Notes | Comparison Note | Historical · Research | Preserved pre-decision API comparison; not the active backend recommendation. |
| `lib/source-review-deep-dive.md` | Library Notes | Source Review | Historical · Research | Preserved cross-library source review. |
| `lib/source-tour-workbook.md` | Library Notes | Workbook | Historical · Research | Optional source-tour exercises for a future evidence-triggered backend evaluation. |
| `hosts.md` | Product | Guide | Current · Active | The console and desktop hosts: what they exercise, how to run them, how to read a result honestly, their explicit non-claims, and the two supported expression-root sources. |
| `preview-quickstart.md` | Product | Guide | Current · Active | First-run guide: run the demo, open your own dump, the session model, the exact expression subset and value domain supported today, what the preview deliberately does not do, and how to read a non-exact answer. |
| `governance/documentation-organization-proposal.md` | Governance | Policy | Current · Supporting | Lightweight information architecture, status vocabulary, and evidence-language rules. |
| `governance/project-faq.md` | Governance | Guide | Draft · Supporting | FAQ for contributors and stakeholders about scope, expectations, and direction in the concept phase. |
| `governance/terminology-glossary.md` | Governance | Glossary | Draft · Supporting | Canonical terminology definitions and cross-document language alignment guidance. |

## Suggested reading paths

### Active delivery path

1. `plans/post-w7-path-forward.md`
2. `plans/w8-1-physical-truth-disposition.md`
3. `plans/post-w6-path-forward.md`
4. `plans/post-w5-path-forward.md`
5. `proposals/architecture/csharp-expression-front-end-contract-proposal.md`
6. `plans/post-w4-path-forward.md`
7. `proposals/product/post-mortem-debugging-feature-proposal.md`
8. `proposals/architecture/restricted-dump-query-contract-proposal.md`
9. `proposals/architecture/concrete-il-execution-contract-proposal.md`
10. `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`
11. `proposals/architecture/architecture-overview-proposal.md`
12. `proposals/architecture/prototype-solution-structure-proposal.md`
13. `lib/mvp-backend-decision-record.md`
14. `plans/future-work-planning.md`
15. `proposals/architecture/testing-strategy-proposal.md`
16. `proposals/integration-test-plan.md`
17. `proposals/integration/clrmd-integration-proposal.md`
18. `proposals/integration/pe-pdb-reader-integration-proposal.md`

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
14. `proposals/architecture/fault-injected-dump-corpus-proposal.md`

### Historical architecture sketches

1. `proposals/architecture/minimal-interfaces-proposal.md`
2. `proposals/architecture/module-architecture-proposal.md`

These sketches explain earlier reasoning, but neither their API shapes nor their project-per-seam package map is normative. Start from the current source and `proposals/architecture/prototype-solution-structure-proposal.md` instead.

### Original planned scope (restored)

The pre-redesign originals below record the project's initially planned scope — virtual debugging, live
speculation, static analysis, and sandboxed IL interpretation — as tentative future directions. They may be
outdated and need revision to match the crystallized architecture; current documents win on any conflict.

1. `proposals/original/README.md`
2. `proposals/original/architecture-overview-proposal.md`
3. `proposals/original/future-work-planning.md`
4. `proposals/original/prototype-solution-structure-proposal.md`
5. `proposals/original/prototype-interface-catalog-proposal.md`
6. `proposals/original/opcode-support-matrix-proposal.md`
7. `proposals/original/testing-strategy-proposal.md`
8. `proposals/original/integration-test-plan.md`

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

Generic-context reconstruction and broad call/effect modeling remain later research topics. The closed W4 contract
admits only its named direct `GetMarkerSummary` to `CombineMarkers` call shape; it does not promote those broader
research designs.

### Process and governance path

1. `governance/project-faq.md`
2. `governance/documentation-organization-proposal.md`
3. `governance/terminology-glossary.md`
4. `plans/post-w7-path-forward.md`
5. `plans/post-w6-path-forward.md`
6. `plans/post-w5-path-forward.md`
7. `plans/post-w4-path-forward.md`
8. `plans/milestone-history.md`
9. `plans/future-work-planning.md`
10. `plans/requirements-traceability-map.md`

## Repository license

This project is distributed under the **MIT-0 (MIT No Attribution)** license. See the repository-level [`LICENSE`](../LICENSE) file for full terms.

## Naming and placement conventions

- Place new design docs under `docs/proposals/<area>/` with `*-proposal.md` suffix.
- `docs/proposals/original/` holds restored pre-redesign originals; do not add new design work there.
- Place sequencing/roadmap artifacts under `docs/plans/`.
- Place process and standards docs under `docs/governance/`.
- Prefer stable filenames; if renaming, update links in both root `README.md` and this index.

## Documentation WIP policy

For current-state questions, prefer evidence in this order: executable tests and their recorded results, source/solution, active decision records and plans, then supporting proposals. Historical/research documents explain possibilities and prior reasoning; they do not override those sources.

Do not add a new subsystem proposal merely because an existing plan mentions one. Add or deepen design only when the active vertical slice needs the contract, and pair the change with an executable validation step whenever practical. When evidence changes, update the active snapshot/traceability entry and mark superseded guidance explicitly instead of silently leaving two “current” answers.
