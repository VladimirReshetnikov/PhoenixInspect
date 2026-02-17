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
| `proposals/architecture/il-interpreter-framework-proposal.md` | Architecture | Proposal | Draft | Core interpreter architecture and execution model. |
| `proposals/architecture/mvp-abstract-domain-proposal.md` | Architecture | Proposal | Draft | Initial abstract domains and analysis behavior for MVP. |
| `proposals/architecture/technical-stack-proposal.md` | Architecture | Proposal | Draft | Language/runtime/dependency and package layout choices. |
| `proposals/architecture/call-model-and-effects.md` | Architecture | Proposal | Draft | Call classification, fallback behavior, and effect lattice contract. |
| `proposals/architecture/generic-context-resolver-proposal.md` | Architecture | Proposal | Draft | Generic-context reconstruction strategy for dump-backed interpretation. |
| `proposals/integration/clrmd-integration-proposal.md` | Integration | Proposal | Draft | ClrMD + PE/PDB integration architecture and binding-layer model. |
| `plans/future-work-planning.md` | Planning | Plan | Draft | Milestones, strategic tracks, and decision gates. |
| `governance/documentation-organization-proposal.md` | Governance | Proposal | Draft | Recommended information architecture and doc lifecycle model. |

## Suggested reading paths

### Product-first path

1. `proposals/product/post-mortem-debugging-feature-proposal.md`
2. `proposals/architecture/il-interpreter-framework-proposal.md`
3. `proposals/architecture/technical-stack-proposal.md`
4. `plans/future-work-planning.md`

### Architecture-first path

1. `proposals/architecture/il-interpreter-framework-proposal.md`
2. `proposals/architecture/mvp-abstract-domain-proposal.md`
3. `proposals/architecture/call-model-and-effects.md`
4. `proposals/architecture/generic-context-resolver-proposal.md`
5. `plans/future-work-planning.md`

### Runtime integration path

1. `proposals/integration/clrmd-integration-proposal.md`
2. `proposals/architecture/generic-context-resolver-proposal.md`
3. `proposals/architecture/call-model-and-effects.md`

### Process and governance path

1. `governance/documentation-organization-proposal.md`
2. `plans/future-work-planning.md`

## Naming and placement conventions

- Place new design docs under `docs/proposals/<area>/` with `*-proposal.md` suffix.
- Place sequencing/roadmap artifacts under `docs/plans/`.
- Place process and standards docs under `docs/governance/`.
- Prefer stable filenames; if renaming, update links in both root `README.md` and this index.

## Next documentation additions (proposed)

- `proposals/architecture/architecture-overview.md`
- `proposals/architecture/state-and-domain-model.md`
- `proposals/architecture/testing-strategy.md`
- `proposals/architecture/perf-and-benchmarks.md`
