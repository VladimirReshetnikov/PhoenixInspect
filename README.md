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

### Core proposals

- `docs/il-interpreter-framework-proposal.md`  
  Foundational proposal for interpreter architecture and execution framework.
- `docs/mvp-abstract-domain-proposal.md`  
  Proposal for initial abstract domains and convergence behavior.
- `docs/technical-stack-proposal.md`  
  Proposed stack, package boundaries, dependencies, and hardening path.
- `docs/post-mortem-debugging-feature-proposal.md`  
  Product-facing proposal for dump-time expression evaluation.
- `docs/future-work-planning.md`  
  Milestones, strategic tracks, decision gates, and follow-up docs.

### Documentation governance

- `docs/documentation-organization-proposal.md`  
  Proposed structure, lifecycle, and quality standards for all project documentation.
- `docs/call-model-and-effects.md`
  Draft contract for call classification, fallback strategy, and effect semantics.

## How to Read the Docs (Suggested Order)

If you are new to the project, read in this order:

1. `docs/post-mortem-debugging-feature-proposal.md` (user/problem framing)
2. `docs/il-interpreter-framework-proposal.md` (engine architecture)
3. `docs/mvp-abstract-domain-proposal.md` (analysis model)
4. `docs/technical-stack-proposal.md` (implementation stack choices)
5. `docs/future-work-planning.md` (milestones and priorities)
6. `docs/documentation-organization-proposal.md` (documentation process and information architecture)

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
2. Add missing companion documents listed in `future-work-planning.md`.
3. Define crisp contracts for call modeling, effects, and unknown provenance.
4. Capture quality gates for moving from design to implementation.

## License

License information is not defined yet and should be added before public distribution.
