# Documentation Index

This directory contains conceptual design artifacts for the IL interpreter and dump-time evaluation project.

## Information architecture

```text
docs/
├── governance/      # Documentation process, standards, and lifecycle
├── plans/           # Milestones, sequencing, and decision gates
└── proposals/
    ├── architecture/ # Interpreter/core/runtime contracts
    ├── integration/  # External systems and host integration boundaries
    └── product/      # Product-facing experience and requirements
```

## Current documents

| Document | Area | Type | Status | Summary |
|---|---|---|---|---|
| `proposals/product/post-mortem-debugging-feature-proposal.md` | Product | Proposal | Draft | User-facing dump-time evaluation capability and phased scope. |
| `proposals/product/virtual-step-debugging-feature-proposal.md` | Product | Proposal | Draft | Product design for virtual Step Into/Over/Out and session UX in dump analysis. |
| `proposals/architecture/architecture-overview-proposal.md` | Architecture | Proposal | Draft | Top-level component map, runtime boundaries, and canonical data flow. |
| `proposals/architecture/il-interpreter-framework-proposal.md` | Architecture | Proposal | Draft | Core interpreter architecture and execution model. |
| `proposals/architecture/mvp-abstract-domain-proposal.md` | Architecture | Proposal | Draft | Initial abstract domains and analysis behavior for MVP. |
| `proposals/architecture/technical-stack-proposal.md` | Architecture | Proposal | Draft | Language/runtime/dependency and package layout choices. |
| `proposals/architecture/call-model-and-effects.md` | Architecture | Proposal | Draft | Call classification, fallback behavior, and effect lattice contract. |
| `proposals/architecture/generic-context-resolver-proposal.md` | Architecture | Proposal | Draft | Generic-context reconstruction strategy for dump-backed interpretation. |
| `proposals/architecture/prototype-solution-structure-proposal.md` | Architecture | Proposal | Draft | Initial `src/` project layout, dependency direction, and prototype API boundary rationale. |
| `proposals/architecture/prototype-interface-catalog-proposal.md` | Architecture | Proposal | Draft | Catalog of current prototype interfaces, project dependencies, and contract rationale. |
| `proposals/architecture/state-and-domain-model-proposal.md` | Architecture | Proposal | Draft | Canonical execution-state model, domain contracts, and provenance semantics. |
| `proposals/architecture/testing-strategy-proposal.md` | Architecture | Proposal | Draft | Testing taxonomy, determinism checks, and milestone quality gates. |
| `proposals/architecture/perf-and-benchmarks-proposal.md` | Architecture | Proposal | Draft | Performance model, benchmark corpus strategy, and regression gates. |
| `proposals/architecture/virtual-step-debugging-implementation-proposal.md` | Architecture | Proposal | Draft | Low-level execution model for stepping control plane, stop reasons, model frames, and undo. |
| `proposals/integration/clrmd-integration-proposal.md` | Integration | Proposal | Draft | ClrMD + PE/PDB integration architecture and binding-layer model. |
| `proposals/integration/pe-pdb-reader-integration-proposal.md` | Integration | Proposal | Draft | Artifact-resolution design for PE/PDB loading, debug-map generation, and source fallback. |
| `plans/future-work-planning.md` | Planning | Plan | Draft | Milestones, strategic tracks, and decision gates. |
| `governance/documentation-organization-proposal.md` | Governance | Proposal | Draft | Recommended information architecture and doc lifecycle model. |
| `governance/project-faq.md` | Governance | Guide | Draft | FAQ for contributors and stakeholders about scope, expectations, and direction in the concept phase. |

## Suggested reading paths

### Product-first path

1. `proposals/product/post-mortem-debugging-feature-proposal.md`
2. `proposals/product/virtual-step-debugging-feature-proposal.md`
3. `proposals/architecture/il-interpreter-framework-proposal.md`
4. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
5. `proposals/architecture/technical-stack-proposal.md`
6. `plans/future-work-planning.md`

### Architecture-first path

1. `proposals/architecture/architecture-overview-proposal.md`
2. `proposals/architecture/il-interpreter-framework-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
5. `proposals/architecture/mvp-abstract-domain-proposal.md`
6. `proposals/architecture/call-model-and-effects.md`
7. `proposals/architecture/testing-strategy-proposal.md`
8. `proposals/architecture/generic-context-resolver-proposal.md`
9. `proposals/architecture/prototype-solution-structure-proposal.md`
10. `proposals/architecture/prototype-interface-catalog-proposal.md`
11. `plans/future-work-planning.md`


### Virtual debugger implementation path

1. `proposals/product/virtual-step-debugging-feature-proposal.md`
2. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`
5. `proposals/architecture/testing-strategy-proposal.md`

### Runtime integration path

1. `proposals/integration/clrmd-integration-proposal.md`
2. `proposals/integration/pe-pdb-reader-integration-proposal.md`
3. `proposals/architecture/generic-context-resolver-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`

### Process and governance path

1. `governance/project-faq.md`
2. `governance/documentation-organization-proposal.md`
3. `plans/future-work-planning.md`

## Naming and placement conventions

- Place new design docs under `docs/proposals/<area>/` with `*-proposal.md` suffix.
- Place sequencing/roadmap artifacts under `docs/plans/`.
- Place process and standards docs under `docs/governance/`.
- Prefer stable filenames; if renaming, update links in both root `README.md` and this index.

## Next documentation additions (proposed)

- `proposals/architecture/opcode-support-matrix-proposal.md`
