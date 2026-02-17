# Tools: IL Interpreter & Dump-Time Evaluation (Concept Design)

This repository is the **design home** for an experimental .NET IL interpreter and a safe, explainable system for evaluating expressions against memory dumps.

If you only read one thing: this project is about making dump-time debugging workflows more trustworthy through deterministic execution, bounded analysis, and explicit explanations when answers are partial or unknown.

## Project gist

We are designing—not yet implementing—a library and architecture that can power experiences such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) over dump-backed sessions,
- explainable analysis when runtime behavior cannot be reproduced exactly.

Core principles:

- **Deterministic and budgeted execution** over unbounded simulation.
- **Safety-first behavior** over risky “best effort” guessing.
- **Explainability and provenance** over opaque results.
- **Composable architecture** so hosts can integrate incrementally.

## Current phase

- **Status:** documentation-first conceptual design.
- **Implementation:** no production code in this repo yet.
- **Primary output:** proposals, plans, and governance docs under `docs/`.

## Where to go next

For structured topic lists, document inventory, and recommended reading paths, start here:

- **Documentation index and TOC-like navigation:** `docs/README.md`

For process and roadmap context:

- `docs/governance/documentation-organization-proposal.md`
- `docs/plans/future-work-planning.md`

## How to use this repository

1. Use this top-level README for intent and orientation.
2. Use `docs/README.md` as the canonical index of topics and reading paths.
3. Read proposals in the sequence that matches your goal (product, architecture, integration, or governance).

## Contribution focus (this phase)

High-value contributions are documentation improvements that sharpen decisions:

- tighten architecture and integration boundaries,
- clarify trade-offs and rationale,
- align terminology across proposals,
- add missing companion docs identified in planning artifacts.

## License

License information is not defined yet and should be added before public distribution.
