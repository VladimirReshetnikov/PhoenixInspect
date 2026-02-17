# Tools: IL Interpreter & Dump-Time Evaluation Research

This repository contains the **conceptual design** for an experimental .NET IL interpreter and a safe, explainable dump-time expression evaluation system.

At this stage, we are intentionally investing in architecture and proposal quality before implementation. The goal is to establish a coherent technical direction for a long-lived library that can later power host experiences such as debugger watch/immediate windows and post-mortem diagnostics tooling.

## Current Status

- Phase: **Documentation-first conceptual design**.
- Code status: no production implementation in this repo yet.
- Primary artifacts: design and planning documents under `docs/`.

## Vision

We are designing a system that can:

1. Interpret IL deterministically with explicit budgets and cancellation.
2. Support both concrete stepping and abstract interpretation modes.
3. Operate safely against snapshot/dump-backed data sources.
4. Provide explainable results, especially when behavior is blocked or approximated.
5. Offer extension points for domains, memory models, metadata backends, and call models.

## Documentation Map

### Product and user experience

- `docs/proposals/product/post-mortem-debugging-feature-proposal.md`
  Product-facing proposal for dump-time expression evaluation.

### Architecture and engine design

- `docs/proposals/architecture/il-interpreter-framework-proposal.md`
  Foundational proposal for interpreter architecture and execution framework.
- `docs/proposals/architecture/architecture-overview-proposal.md`
  High-level system map linking runtime, analysis, adapters, and hosting boundaries.
- `docs/proposals/architecture/mvp-abstract-domain-proposal.md`
  Proposal for initial abstract domains and convergence behavior.
- `docs/proposals/architecture/technical-stack-proposal.md`
  Proposed stack, package boundaries, dependencies, and hardening path.
- `docs/proposals/architecture/call-model-and-effects.md`
  Draft contract for call classification, fallback strategy, and effect semantics.
- `docs/proposals/architecture/generic-context-resolver-proposal.md`
  Generic-context reconstruction strategy for dump-backed interpretation.
- `docs/proposals/architecture/state-and-domain-model-proposal.md`
  Canonical execution-state contract and domain/lattice semantics.
- `docs/proposals/architecture/testing-strategy-proposal.md`
  Test taxonomy, determinism checks, and milestone quality gates.
- `docs/proposals/architecture/perf-and-benchmarks-proposal.md`
  Performance model, benchmark corpus strategy, and regression gates.

### Integration design

- `docs/proposals/integration/clrmd-integration-proposal.md`
  ClrMD + PE/PDB integration layering and host boundary proposal.

### Planning and governance

- `docs/plans/future-work-planning.md`
  Milestones, strategic tracks, decision gates, and follow-up docs.
- `docs/governance/documentation-organization-proposal.md`
  Proposed structure, lifecycle, and quality standards for project documentation.

## How to Read the Docs (Suggested Order)

If you are new to the project, read in this order:

1. `docs/proposals/product/post-mortem-debugging-feature-proposal.md` (user/problem framing)
2. `docs/proposals/architecture/architecture-overview-proposal.md` (system map and boundaries)
3. `docs/proposals/architecture/il-interpreter-framework-proposal.md` (engine architecture)
4. `docs/proposals/architecture/mvp-abstract-domain-proposal.md` (analysis model)
5. `docs/proposals/architecture/state-and-domain-model-proposal.md` (formal state/domain semantics)
6. `docs/proposals/architecture/technical-stack-proposal.md` (implementation stack choices)
7. `docs/proposals/architecture/testing-strategy-proposal.md` (quality strategy and validation plan)
8. `docs/plans/future-work-planning.md` (milestones and priorities)
9. `docs/governance/documentation-organization-proposal.md` (documentation process and information architecture)

## Decision-Making Principles

When authoring or reviewing proposals in this repository, prioritize:

- **Determinism and bounded execution** over maximal behavioral fidelity.
- **Safety and honesty** over speculative “best effort” execution.
- **Explainability** over opaque precision claims.
- **Composable architecture** over one-off optimizations.
- **Incremental delivery** with explicit milestone gates.

## Contribution Guidelines (Documentation Phase)

During this phase, contributions should primarily improve clarity, coherence, and decision quality.

Preferred contribution types:

- Tightening proposal contracts and boundary definitions.
- Adding alternatives/trade-off sections where choices are non-obvious.
- Aligning terminology across documents.
- Filling missing companion docs identified in planning materials.

When adding docs:

- Use clear headings and explicit scope statements.
- Separate “current decision” from “open questions.”
- Include rationale for major claims.
- Keep naming stable; when renaming terms, include migration notes.

## Near-Term Documentation Priorities

The current priorities are:

1. Establish a stable top-level documentation architecture.
2. Add missing companion documents listed in `docs/plans/future-work-planning.md`.
3. Define crisp contracts for call modeling, effects, and unknown provenance.
4. Capture quality gates for moving from design to implementation.

## License

License information is not defined yet and should be added before public distribution.
