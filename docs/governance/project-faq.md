# Project FAQ (Concept Phase)

> **Lifecycle:** Draft · **Roadmap:** Supporting

This FAQ answers the questions contributors, partner teams, and prospective adopters are most likely to ask while this repository is still in the conceptual-design stage.

## 1) What is this project trying to build?

The active product target is a deterministic, read-only expression evaluator grounded in .NET dumps. The IL interpreter is enabling technology for later method evaluation, not the near-term product by itself.

The committed sequence starts with direct snapshot reads and a restricted expression/query front end. Virtual stepping, whole-method abstract analysis, async/dynamic lifting, live speculation, sandbox runtime hosting, and other applications remain research backlog until those evidence gates pass.

Longer-term workflows may include:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) in dump-backed sessions,
- explicit explanations when a result is unknown/partial.

## 2) Is this already a production-ready implementation?

No. The repository remains in conceptual design with a narrow executable prototype.

There is an intentionally narrow prototype under `src/` and `tests/`: draft public contracts, a whole-body-admitted integer arithmetic/local kernel, a persistent concrete validation heap, SRM method-body extraction, and bounded ClrMD dump/module/object/field/string/metadata/IL evidence. Unit, compiler-differential, fast adapter, and real-dump suites validate that limited surface; it is not yet a production-ready expression evaluator. Work is evidence-led: executable slices and tests are the progress signal, with concise documentation kept in sync.

## 3) Why focus on deterministic and bounded execution instead of "best effort" simulation?

Because dump-time debugging has incomplete runtime context by definition. Overly optimistic simulation can produce plausible-but-wrong answers.

This project prefers:

- deterministic behavior with explicit budgets,
- trustworthy unknowns over fabricated certainty,
- provenance and explanation attached to results.

## 4) What problem does this solve for debugger users?

It aims to reduce ambiguity in post-mortem investigations by:

- enabling controlled expression evaluation from dumps,
- deriving useful answers from recoverable snapshot evidence,
- surfacing clear stop reasons and confidence boundaries.

The goal is not to recreate runtime history. A read-only query answers what can be derived from the captured snapshot; a future interpreted method would answer what the code would compute from recovered or assumed state under explicit models and policies. Neither establishes why the original process historically reached that state.

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

The primary output is executable evidence for the active vertical slice, accompanied by only the design needed to make that evidence trustworthy. Useful artifacts include:

- an end-to-end scenario and its tests,
- lightweight prototype code that exercises the real boundary,
- concise contract or decision updates required by that scenario,
- an honest record of what the scenario did and did not prove.

Large speculative specifications are not progress by themselves. Formal contracts should solidify just ahead of implementation, after a real slice has exposed the distinctions they need.

## 9) What should a new contributor read first?

Recommended quick-start order:

1. Root `README.md` for project intent.
2. `docs/README.md` for the document map.
3. Follow the `Active delivery path` in that index.
4. Read research proposals only when the active milestone creates a concrete need for them.

## 10) How should contributors choose what to work on next?

Start with the active milestone in `docs/plans/future-work-planning.md`:

- advance its executable scenario or validation evidence,
- repair a contract that directly blocks that scenario,
- remove a contradiction that could make results misleading,
- add tests for determinism, partialness, or failure behavior.

Do not begin a new subsystem proposal simply because it appears in the research backlog. The project keeps one active vertical slice at a time.

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

The first walking skeleton is implemented. The intended progression is now evidence-led:

1. Keep one product scope and one active slice explicit.
2. Write the minimum contract that the next slice needs.
3. Implement the slice against real artifacts.
4. Validate success, partialness, failure, and determinism.
5. Revise the design from that evidence before expanding scope.

## 14) What are the main technical risks currently anticipated?

Representative risks include:

- capacity and scope dispersion in a single-maintainer project,
- optimized dumps that omit the context a requested expression needs,
- hostile or malformed dump/PE/PDB inputs and secret-bearing dump contents,
- unsound fallback behavior around calls/effects,
- metadata and symbol resolution inconsistencies,
- performance trade-offs under strict determinism constraints,
- drift between product expectations, design claims, and executable evidence.

The documentation set is intentionally structured to expose and manage these risks early.

## 15) How can external consumers evaluate whether this direction is promising?

A practical evaluation rubric is:

- Does the current milestone deliver a useful dump-backed answer?
- Are contracts explicit enough to integrate against?
- Are unknown/partial outcomes represented clearly?
- Are fallback policies deterministic and reviewable?
- Is each claimed capability backed by a passing scenario rather than only a proposal?
- Is milestone sequencing realistic for the stated capacity?

If these answers become stronger over time, the design is moving in the right direction.

## 16) What is out of scope for this repository right now?

Out of scope in the current phase:

- claiming implementation completeness,
- promising production timelines,
- polishing runtime tooling UX beyond design-level proposals,
- publishing final API guarantees before executable evidence stabilizes them,
- treating virtual stepping, abstract analysis, async/dynamic lifting, live speculation, or sandbox hosting as active commitments.

## 17) How should people give feedback on the docs?

High-value feedback:

- points out contradictions across proposals,
- challenges assumptions with concrete scenarios,
- suggests better boundary definitions,
- identifies missing acceptance criteria or quality gates,
- contributes executable evidence for or against a design claim.

Feedback that improves decision quality or traceability is especially valuable.
