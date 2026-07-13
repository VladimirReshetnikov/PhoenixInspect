# Project FAQ (Concept Phase)

This FAQ answers the questions contributors, partner teams, and prospective adopters are most likely to ask while this repository is still in the conceptual-design stage.

## 1) What is this project trying to build?

We are designing an experimental .NET IL interpreter plus a dump-time evaluation system that can answer debugger-style questions from memory dumps in a deterministic, explainable, and safety-first way.

In practical terms, we want to support workflows such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) in dump-backed sessions,
- explicit explanations when a result is unknown/partial.

## 2) Is this already a production-ready implementation?

No. The repository is currently documentation-first and design-centric.

There is an intentionally narrow prototype under `src/` and `tests/`: draft public contracts, a budgeted `ret`-only IL micro-step, SRM method-body extraction, ClrMD dump/module discovery, and one end-to-end dump integration test. It validates initial plumbing only; it is not a production-ready evaluator. The main outputs remain the architecture, product, integration, planning, and governance documents under `docs/`.

## 3) Why focus on deterministic and bounded execution instead of "best effort" simulation?

Because dump-time debugging has incomplete runtime context by definition. Overly optimistic simulation can produce plausible-but-wrong answers.

This project prefers:

- deterministic behavior with explicit budgets,
- trustworthy unknowns over fabricated certainty,
- provenance and explanation attached to results.

## 4) What problem does this solve for debugger users?

It aims to reduce ambiguity in post-mortem investigations by:

- enabling controlled expression evaluation from dumps,
- modeling virtual stepping behavior where possible,
- surfacing clear stop reasons and confidence boundaries.

The goal is not to perfectly recreate runtime execution; it is to provide high-signal, auditable answers when full fidelity is impossible.

## 5) How does this relate to existing .NET diagnostics tooling?

The design assumes integration with ecosystem components (especially dump/metadata providers) rather than replacing them.

This project is intended to be a composable analysis layer that can consume metadata, symbols, and dump information from established tools and expose a predictable interpreter/evaluation contract to hosts.

## 6) What are the most important architecture principles?

1. Safety-first semantics.
2. Deterministic, budgeted interpretation.
3. Explainability and provenance in outputs.
4. Composability through explicit boundaries.
5. Incremental host adoption over all-or-nothing integration.

## 7) What does "unknown" mean here, and is it considered failure?

"Unknown" is a first-class, expected outcome when a sound answer cannot be derived with available data and policy constraints.

Unknown is not an error by default. It is often the most correct answer and should include enough provenance to explain _why_ certainty was not possible.

## 8) What is the expected output format of the design effort right now?

Primary outputs are design artifacts such as:

- feature proposals,
- architecture proposals,
- integration proposals,
- planning and sequencing documents,
- governance/process guidance.

As decisions mature, we expect more formal decision records and eventually normative specs.

## 9) What should a new contributor read first?

Recommended quick-start order:

1. Root `README.md` for project intent.
2. `docs/README.md` for the document map.
3. Product proposals to understand user-facing goals.
4. Architecture and integration proposals tied to your area.
5. Planning/governance docs for sequencing and doc lifecycle context.

## 10) How should contributors choose what to work on next?

Use gaps and dependency pressure as your guide:

- clarify ambiguous contracts between documents,
- tighten assumptions that affect multiple proposals,
- add missing companion docs identified in plans,
- resolve terminology drift before it spreads.

In this phase, documentation quality and coherence _are_ the core deliverables.

## 11) How stable are current decisions?

Most documents are still in draft status. Treat them as directional, not final.

Contributors should feel comfortable proposing changes when they improve consistency, reduce risk, or increase clarity.

## 12) What does good uncertainty handling look like in this project?

Good uncertainty handling is:

- explicit (never hidden),
- localized (attach uncertainty to specific outputs/steps),
- actionable (state what evidence would reduce it),
- deterministic (same inputs/policies produce same uncertainty markers).

## 13) How is this project moving from design to implementation?

The first walking-skeleton slice has begun step 4 below, while the earlier design-governance work remains incomplete. The intended progression is:

1. Mature and align conceptual proposals.
2. Convert key decisions into explicit ADRs/spec-like contracts.
3. Define MVP slices with measurable quality gates.
4. Implement in thin vertical slices with determinism and explainability checks.
5. Expand capability while preserving safety and provenance guarantees.

## 14) What are the main technical risks currently anticipated?

Representative risks include:

- unsound fallback behavior around calls/effects,
- metadata and symbol resolution inconsistencies,
- performance trade-offs under strict determinism constraints,
- drift between product expectations and architecture guarantees.

The documentation set is intentionally structured to expose and manage these risks early.

## 15) How can external consumers evaluate whether this direction is promising?

A practical evaluation rubric is:

- Are contracts explicit enough to integrate against?
- Are unknown/partial outcomes represented clearly?
- Are fallback policies deterministic and reviewable?
- Is virtual stepping behavior understandable and auditable?
- Is milestone sequencing realistic for incremental adoption?

If these answers become stronger over time, the design is moving in the right direction.

## 16) What is out of scope for this repository right now?

Out of scope in the current phase:

- claiming implementation completeness,
- promising production timelines,
- polishing runtime tooling UX beyond design-level proposals,
- publishing final API guarantees before ADR/spec stabilization.

## 17) How should people give feedback on the docs?

High-value feedback:

- points out contradictions across proposals,
- challenges assumptions with concrete scenarios,
- suggests better boundary definitions,
- identifies missing acceptance criteria or quality gates,
- proposes clearer terminology and cross-links.

Feedback that improves decision quality or traceability is especially valuable.
