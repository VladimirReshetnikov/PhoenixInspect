# Project FAQ (Concept Phase)

> **Lifecycle:** Draft · **Roadmap:** Supporting

This FAQ answers the questions contributors, partner teams, and prospective adopters are most likely to ask while this repository is still in the conceptual-design stage.

## 1) What is this project trying to build?

The active product target is a deterministic, read-only expression evaluator grounded in .NET dumps. The IL
interpreter supports bounded counterfactual method evaluation, but it is enabling technology rather than the product
by itself. W1–W7 are closed for their named milestone scopes. Most recently, W7 implemented an opt-in
`StaticFieldExpressionV1`: a non-ambiguous fully qualified ordinary static field works without frame/PDB context,
while exact selected-frame/Portable-PDB namespace, import, and simple-alias facts can bind contextual `Type.Field`
forms. Direct values and exact references can continue through the unchanged W2/W6 suffix evaluator.

W8 is the sole active design/implementation sequence. W8.1's physical-truth fixture and probes are complete at
`220be94b4`; W8.2 is the active product-contract checkpoint. The additive
`StaticFieldExpressionV2` profile expands the same pipeline across nested types, closed constructed generic owners,
scope-precise aliases/imports, constraints, accessibility, constructed assignability, ordinary stored fields,
metadata literals, and evidence-qualified bare static fields. W8.1 admits constructed ordinary, thread-relative, and
RVA-backed storage plus exact memory-homed frame values. Context-relative identity, register homes, and selected-frame
generic substitution are non-admitted and have no corresponding API or success row. The exact checkpoint ledger and
branch table live in the [`W8.1 Physical-Truth Disposition`](../plans/w8-1-physical-truth-disposition.md).

The committed sequence started with direct snapshot reads and a restricted expression/query front end and now advances
one evidence-led profile at a time. Virtual stepping, whole-method abstract analysis, async/dynamic lifting, live
speculation, no-JIT runtime hosting, and other applications remain research backlog until their evidence gates pass.

Longer-term workflows may include:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) in dump-backed sessions,
- explicit explanations when a result is unknown/partial.

## 2) Is this already a production-ready implementation?

No. The repository remains in early development with a narrow executable slice.

The implementation under `src/` and `tests/` now includes the closed W1–W7 evidence: typed dump reads; restricted rooted
queries; bounded concrete IL and counterfactual method execution; a product facade and hidden reference consumer;
fixed-depth field/data-property navigation; one complete pinned Roslyn parse with versioned profile admission; and
fully qualified/contextual ordinary static-field reads with direct or suffix results. W7's closure baseline passes its
complete local headless matrix and repository guards, including sixteen independent full dumps across four application
shapes. W8.1 adds three generated targets and executable compiler/PDB, runtime-construction, storage, assignability,
and selected-frame probes. Its focused gates pass 25/25, 1/1, one test across six dumps/profiles, and 8/8,
respectively. This remains generated and meaningful-synthetic evidence with zero representative observations; it does
not establish production readiness. W8.2 product behavior requires its own implementation and validation evidence.

The following W3 checkpoint record is retained as historical evidence for one of those foundations. At that point the
implementation contained public contracts; structural module/type/method/field identities; SRM-derived method
signatures, locals, bodies, and field definitions; metadata-derived root activation; frozen typed whole-body admission;
a persistent concrete validation heap; and bounded ClrMD dump/module/object/field/string/metadata/IL evidence. The
closed W3 E1/E2 profiles execute branchless `Int32` arithmetic and one exact direct or constant-adjusted instance-field
getter through the injected memory capability. The getter fixture reopens and rebinds the dump, reconstructs the
prepared memory snapshot from counted evidence, and reproduces its canonical execution transcript without using the
disk PE as resolver input.

Strengthened checkpoint `19c292f9f` passed local headless verification with a zero-warning 15-project Release build,
103 milestone-selected unit tests, 67 fast integration tests, 5 ordinary dump tests, 1 optimized-context dump test,
the focused 2-test W3 lane, and both documentation guards, all with zero skips. Its cumulative hand-written
implementation range from `e7b6a4ace` is `+8,842/-1,650` LOC (`+5,362/-928` production and `+3,480/-722`
tests/fixtures), plus 39 generated lock-file lines. The primary checkpoint `12b6ef942` passed all four jobs in
[GitHub Actions run 29372661656](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29372661656);
[run 29374585767](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29374585767) passed all four jobs at
exact strengthened checkpoint `19c292f9f`. [Run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237) subsequently passed every
required job at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
milestone-selected scope. That historical closure did not by itself create the later product-facing capabilities.

## 3) Why focus on deterministic and bounded execution instead of "best effort" simulation?

Because dump-time debugging has incomplete runtime context by definition. Overly optimistic simulation can produce plausible-but-wrong answers.

This project prefers:

- deterministic behavior with explicit budgets,
- evidence-backed unknowns over fabricated certainty,
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

1. Conservative semantics.
2. Deterministic, budgeted interpretation.
3. Explainability and provenance in outputs.
4. Composability through explicit boundaries.
5. Incremental host adoption over all-or-nothing integration.

## 7) What does "unknown" mean here, and is it considered failure?

"Unknown" is a first-class, expected outcome when a sound answer cannot be derived with available data and policy constraints.

Unknown is not an error by default. It is often the most correct answer and should include enough provenance to explain _why_ certainty was not possible.

## 8) What is the expected output format of the design effort right now?

The primary output is executable evidence for the active vertical slice, accompanied by only the design needed to make that evidence evidence-backed. Useful artifacts include:

- an end-to-end scenario and its tests,
- lightweight code that exercises the real boundary,
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

Start with the active milestone in `docs/plans/post-w7-path-forward.md`; use
`docs/plans/post-w6-path-forward.md` for the completed W7 sequence,
`docs/plans/post-w5-path-forward.md` for the completed W6 sequence,
`docs/plans/post-w4-path-forward.md` for the completed W5 sequence, and
`docs/plans/future-work-planning.md` for the detailed W0–W4 record and research entry gates:

- advance its executable scenario or validation evidence,
- repair a contract that directly blocks that scenario,
- remove a contradiction that could make results misleading,
- add tests for determinism, partialness, or failure behavior.

Do not begin a new subsystem proposal simply because it appears in the research backlog. The project keeps one active vertical slice at a time.

## 11) What exactly does the active W8 plan include?

W8 keeps W7's fully qualified route independent of frame/PDB context and adds one coherent V2 binder rather than a
sequence of spelling-specific parsers:

- **Owners and construction:** top-level and nested non-generic types, plus recursively closed constructed generic
  class, value-type, and interface owners represented through exact TypeDef/TypeRef/TypeSpec and runtime-construction
  identity. Different loaded constructions of one TypeDef/FieldDef pair retain different slots and values.
- **Context:** current/enclosing namespaces and types, namespace imports, type and namespace aliases, exact extern
  aliases, TypeSpec aliases, and evidence-qualified current-type/`using static` bare fields. Import scopes follow
  lexical precedence; incomplete blocker facts stop bare-field binding rather than selecting a convenient candidate.
- **Fields and values:** ordinary stored static fields use exact construction/storage and counted value evidence;
  metadata literals cover the admitted primitive, enum, decimal, string, and null forms without runtime construction, slot
  lookup, or memory reads. Exact references may use the unchanged W2/W6 suffix evaluator.
- **Validation:** substituted generic constraints, inspection accessibility, field `VAR` substitution, declaration
  hiding, and constructed base/interface/array assignability are checked before a non-null value is accepted.
- **Physical branch dispositions:** W8.1 admits thread-relative and RVA-backed storage. It admits exact memory-homed
  `this`/parameter/live-local values through mandatory separate `FrameValueExpressionV1`. Context-relative identity,
  register homes, and selected-frame generic substitution are non-admitted and remain typed executable results.

The plan is intentionally broad (`~100K LOC` umbrella scale, mostly `~10K LOC` checkpoints), but it is still a bounded
versioned profile. It does not imply arbitrary C# binding or evaluation. W8.1 is physical implementation evidence;
later W8 product behavior is evidenced only by its own landed checkpoints.

## 12) How stable are current decisions?

Most documents are still in draft status. Treat them as directional, not final.

Contributors should feel comfortable proposing changes when they improve consistency, reduce risk, or increase clarity.

## 13) What does good uncertainty handling look like in this project?

Good uncertainty handling is:

- explicit (never hidden),
- localized (attach uncertainty to specific outputs/steps),
- actionable (state what evidence would reduce it),
- deterministic (same inputs/policies produce same uncertainty markers).

## 14) How is this project moving from design to implementation?

The walking skeleton and W1–W7 vertical slices are implemented. Progression remains evidence-led:

1. Keep one product scope and one active slice explicit.
2. Write the minimum contract that the next slice needs.
3. Implement the slice against real artifacts.
4. Validate success, partialness, failure, and determinism.
5. Revise the design from that evidence before expanding scope.

W3 demonstrated this progression at the interpreter/memory seam; W4–W7 subsequently delivered bounded call/model,
product-composition, member-navigation, sole-parser, and static-context slices without widening their closed profiles.
W8 now applies the same progression to constructed static owners and complete bounded context rules. Its completed
physical TypeSpec/runtime/storage/frame probes constrain W8.2 public contracts; implementation, generated conformance,
and the 35-incident minimum meaningful synthetic decision corpus must then agree before closure. Branches, broader
opcode and exception-transfer
behavior, general method/property execution, and research workflows retain their own scenario gates.

## 15) What are the main technical risks currently anticipated?

Representative risks include:

- capacity and scope dispersion in a single-maintainer project,
- optimized dumps that omit the context a requested expression needs,
- input shapes outside the validated fixture set and artifact-derived dump contents,
- unsound fallback behavior around calls/effects,
- metadata and symbol resolution inconsistencies,
- performance trade-offs under strict determinism constraints,
- drift between product expectations, design claims, and executable evidence.

The documentation set is intentionally structured to expose and manage these risks early.
Caveat: W1–W8.1 evidence covers only the named generated fixtures and explicitly admitted input shapes. Current test
claims do not establish behavior for any other artifact shape, and later W8 plans are not test evidence.

## 16) How can external consumers evaluate whether this direction is promising?

A practical evaluation rubric is:

- Does the current milestone deliver a useful dump-backed answer?
- Are contracts explicit enough to integrate against?
- Are unknown/partial outcomes represented clearly?
- Are fallback policies deterministic and reviewable?
- Is each claimed capability backed by a passing scenario rather than only a proposal?
- Is milestone sequencing realistic for the stated capacity?

If these answers become stronger over time, the design is moving in the right direction.

## 17) What is out of scope for this repository right now?

Out of scope in the current phase:

- claiming production or general-interpreter completeness from the closed W1–W7 early-development evidence,
- presenting W8.2+ V2 product behavior as implemented before its checkpoint evidence lands,
- promising production timelines,
- polishing runtime tooling UX beyond design-level proposals,
- publishing final API guarantees before executable evidence stabilizes them,
- broad method/property execution beyond W4's closed shape, broader opcode/EH families, virtual stepping, abstract
  analysis, async/dynamic lifting, live speculation, and no-JIT hosting.

## 18) How should people give feedback on the docs?

High-value feedback:

- points out contradictions across proposals,
- challenges assumptions with concrete scenarios,
- suggests better boundary definitions,
- identifies missing acceptance criteria or quality gates,
- contributes executable evidence for or against a design claim.

Feedback that improves decision quality or traceability is especially valuable.

## 19) Where should I go next?

1. Read the active [Post-W7 W8 plan](../plans/post-w7-path-forward.md) for the current scope, branch consequences,
   checkpoints, and closure rules.
2. Read the completed
   [W8.1 physical-truth disposition](../plans/w8-1-physical-truth-disposition.md) for the exact evidence ledger and
   frozen W8.2 API consequences.
3. Read the completed [Post-W6 W7 plan](../plans/post-w6-path-forward.md) for the implemented static-expression
   baseline and its evidence limits.
4. Read the [product proposal](../proposals/product/post-mortem-debugging-feature-proposal.md) for the user-facing
   capability sequence and research boundary.
5. Read the [C# expression front-end contract](../proposals/architecture/csharp-expression-front-end-contract-proposal.md)
   for the sole-Roslyn-parse and versioned-admission rule.
6. Return to the [documentation index](../README.md) for architecture, testing, integration, governance, and
   historical reading paths.
