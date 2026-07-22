# Tools: IL Interpreter & Dump-Time Evaluation (Concept Design)

This repository is the **design home** for an experimental .NET IL interpreter and a bounded, explainable system for evaluating expressions against memory dumps.

If you only read one thing: this project is about making dump-time debugging workflows more evidence-backed through deterministic execution, bounded analysis, and explicit explanations when answers are partial or unknown.

## Project gist

We are designing and prototyping—not yet shipping—a library and architecture that can power experiences such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) over dump-backed sessions,
- explainable analysis when runtime behavior cannot be reproduced exactly.

Core principles:

- **Deterministic and budgeted execution** over unbounded simulation.
- **Conservative behavior** over risky “best effort” guessing.
- **Explainability and provenance** over opaque results.
- **Composable architecture** so hosts can integrate incrementally.

## Current phase

- **Status:** conceptual design with an executable prototype, progressing through evidence-led vertical slices.
- **Delivery status:** W8 is the sole active design/implementation sequence. W8.1 is implemented and locally validated
  through exact compiler/PDB, constructed-runtime, selected-frame, storage, literal, and assignability evidence. Its
  [`physical-truth disposition`](docs/plans/w8-1-physical-truth-disposition.md) admits constructed, thread-relative,
  module-RVA, literal, and exact memory-homed frame-value branches; context-relative storage and selected-frame generic
  arguments remain typed non-admitted. W8.2 is the active checkpoint. The
  [`Post-W7 Path Forward`](docs/plans/post-w7-path-forward.md) requires additive `StaticFieldExpressionV2`,
  `BindingContextV2`, and `FrameValueExpressionV1` contracts. W8.2 has landed the immutable expression-contract
  foundation, the detached frame-value syntax contract, a shared bounded ECMA signature projection, and a
  selected-method lexical evidence envelope. The lexical envelope is still constructed from caller-supplied physical
  rows; a host-owned ClrMD producer and the V2 product binder/evaluator have not landed. Its
  inclusive umbrella scale is `~100K LOC`, generally split into `~10K LOC` implementation/evidence checkpoints. W7's
  context-independent fully qualified guarantee, raw-memory authority, result axes, and unchanged W2/W6 suffix
  semantics remain mandatory controls.
- **Most recently completed active checkpoint:** W8.1, through exact source baseline `220be94b4`. Its generated
  artifacts and real full dumps prove candidate-keyed ordered closed constructions; distinct construction and
  thread-relative slots; exact metadata literals and named FieldRVA bytes; exact `this`, parameter, and active-local
  memory homes; and bounded constructed assignability. The minimum W8 meaningful portfolio is now thirty-five
  independent incidents: thirty-two core plus thread-relative, RVA-backed, and frame-value rows. This is pre-contract
  physical evidence, not product behavior or representative observation.
- **Most recently closed delivery target:** W7. Its opt-in `StaticFieldExpressionV1`
  profile uses the sole complete Roslyn parse, counted module metadata, optional selected-frame/identity-validated
  Portable-PDB import context, and raw dump-memory reads. Non-ambiguous fully qualified ordinary static fields bind
  without stack or PDB evidence; exact current-namespace, namespace-import, type-alias, and namespace-alias facts may
  bind contextual names. Direct scalar, string, nullable, exact-null, and validated object-reference values are
  supported, and exact references feed the unchanged W2/W6 suffix evaluators. Sixteen independent full dumps across
  four application shapes reproduce exact and deliberately imperfect context/storage outcomes through two fresh
  hidden consumers. The manifest-assigned post-W7 leader is `BindingContextPrecision`; W7 permits choosing one
  successor rule along its frame/PDB/import/alias/generic trajectory but does not itself select or implement that
  concrete rule. The separately approved W8 plan makes the broader V2 decision explicitly.
  W7's exact implementation source baseline `f99b12ee7` passes locked
  restore; a strict zero-warning Release solution build; unit 507/507; complete integration 242/242; Fast 184/184;
  ordinary dump 29/29; optimized-context 1/1; focused W7 98/98; static-expression 1/1; and meaningful synthetic 1/1,
  all with zero skips. Markdown, headless-workflow, and authored-scope vocabulary guards pass. The owner explicitly
  overrode W7's hosted-only condition after GitHub rejected required jobs for the known billing condition; that
  W7-only disposition is not hosted execution evidence and does not weaken the four-job workflow.
- **Prior closed delivery target:** W6, a deterministic, read-only, fixed-depth member-chain query grounded in
  a .NET dump. W5 closed the W2/W4 expression-to-result path and selected this slice from meaningful multi-shape
  synthetic incident evidence. The completed [`Post-W5 Path Forward`](docs/plans/post-w5-path-forward.md) freezes an opt-in two-member
  tree shape, one complete pinned Roslyn C# expression parse with versioned subset admission, counted reference
  evidence, truthful non-root identity, a narrowly certified field-backed data-property shape, null/coalesce semantics,
  headless replay, and a new multi-shape decision gate. Its W4 baseline has a closed branchless counterfactual-method contract, a validated W4.1
  value-gate fixture, a validated W4.2
  provenance-aware execution kernel, a validated W4.3 dump-free non-exact field seam, and W4.4's validated body-free
  direct-MethodDef resolution plus complete frozen call graph. W4.5 now executes exact and explained-unknown values
  across frozen direct-call graphs with deterministic multi-frame accounting and canonical call/return lineage. W4.6
  now freezes one exact/no-effect structural pure model as a body-free opaque graph leaf, executes only that frozen
  capability through exact or grounded-unknown atomic caller transfer, records truthful model attempts and distinct
  logical/frame depth witnesses, and proves compiler/SRM exact and degraded conformance. W4.7 now projects a complete,
  same-machine IL-zero-to-exact-null transition sequence into a standalone canonical target-outcome fragment, with
  per-outcome issuer certification and optional idempotent terminal re-step. W4.8 now supplies configurable traversal
  accounting, canonical request/plan/result contracts, private typed runtime bindings, the authoritative preparation
  and execution runner, and common projection of rooted and standalone outcomes. W4.9 now supplies the ClrMD method/
  field evidence graph, a detached product-owned dump-memory binding, and six exact/degraded interpreted/modeled
  generated-dump rows with close/reopen canonical replay. W4 implementation is formally closed for this scope at exact commit
  `a819a08fd`; [GitHub Actions run 29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083)
  passed all four required jobs. Final documentation-closure commit `aaec73c5b` passed the same matrix in
  [run 29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230). The completed
  [`Post-W4 Path Forward`](docs/plans/post-w4-path-forward.md) records W5 and its selection decision. The completed
  [`Post-W5 Path Forward`](docs/plans/post-w5-path-forward.md) records W6, and the completed
  [`Post-W6 Path Forward`](docs/plans/post-w6-path-forward.md) records W7. The active sequence is the
  [`Post-W7 Path Forward`](docs/plans/post-w7-path-forward.md).
- **W5 implementation status:** W5.1–W5.5b are implemented and pushed through checkpoints `7c3d52572`, `d88b13c2c`,
  `fc8a43a7a`, `59d9bb590`, `0f5230e13`, `b788f4f66`, and `90ade6d92`. One canonical request classifier now admits the unchanged W2 grammar or
  exact `root.GetMarkerSummary()` method form; one product facade owns dump reacquisition and detached W4 binding; one
  strict result union preserves the original W2/W4 modes and typed failure cases; and the then-current 14-project solution includes
  a headless reference consumer plus deterministic usefulness-report mode. The generated nine-question validation
  report is 8/9 admitted and 3/9 exact, with 0/4 useful partial-or-unknown and 0/9 decision-changing answers. A second
  predeclared portfolio runs 12 independently dumped incidents over request-pipeline and batch-pipeline roots: 8/12
  are admitted, 4/12 exact, 2/3 useful among partial-or-unknown answers, and 6/12 decision-changing. Its recurring
  blocker ranking selects `AdmitFixedDepthMemberChain` for the next prototype design slice. The evidence is designed,
  not representative: the external-observation denominator remains 0 questions across 0 incidents and 0 application
  shapes, and no readiness rate is claimed. W5 is closed for its defined prototype scope under the owner's explicit
  2026-07-16 waiver of the hosted-only closure condition. The exact source baseline `26ba75fb1` passes the complete
  local headless matrix. [Run 29512657137](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29512657137)
  passed documentation and Build/Fast at exact commit `24825ce53`, but GitHub rejected both dump jobs before their
  first step; [run 29513051897](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29513051897) was
  rejected before any job executed. The waiver is a W5-only governance exception, not hosted dump evidence, a
  field-readiness claim, or permission to weaken the unchanged workflow.
- **W6 closure record:** the detailed W6 and
  [`C# expression-front-end`](docs/proposals/architecture/csharp-expression-front-end-contract-proposal.md) contracts
  are current. W6.1 is implemented and pushed at `0dc055e66`: an independent PE/SRM oracle freezes the actual
  record-property/getter/backing-field relations selected by W5, two additional graph families complete the
  twenty-four-scenario target-shape baseline, all managed target launches are hidden, and the one intentional current
  TestTarget identity refresh is explicit. W6.2 is implemented and pushed at `68aaf418f`: the sole production parser
  is now pinned `Microsoft.CodeAnalysis.CSharp/5.3.0`, every bounded input receives one complete C# 14 expression
  parse, ordered project-owned W2/W5/opt-in W6 recognizers retain only admitted descriptors, the W5 default
  language/report behavior remains frozen, and the handwritten production reader is deleted. W6.3 is implemented
  and pushed through `6c36bd397`: complete counted dump metadata binds the directly declared reference target and
  terminal storage, unexecuted getters are acquired by exact MethodDef token and certified from physical target
  bytes, all three existing W2 decoders have frozen relative layouts, and preparation reads neither the reference nor
  terminal value. W6.4 is implemented and pushed through `ee788e414` and `40ece4446`: counted four/eight-byte
  reference observations never fabricate missing suffixes; non-root intrinsic identity is separate from alias-path
  provenance; exact targets retain validated extent; descriptor-consuming terminal readers need no catalog lookup;
  and one canonical complete plan freezes the accepted Roslyn-derived request, certificate, relative layout,
  semantics, evidence, and bounds before any value read. W6.5 is implemented and pushed through `62c4bb157`: the
  frozen plan now evaluates without declaration rebinding or getter invocation; exact/null/partial/unavailable/
  conflict/invalid outcomes retain independent result axes and ordered provenance; the product facade routes only an
  explicit `FixedDepthMemberChainV1` request; and schema-v2 headless reports replay eleven independent snapshots while
  schema v1 remains unchanged. W6.6 is implemented and pushed at `93c1f684b`: schema v3 materializes twenty-four
  independent dumps over four graph/root shapes, preserves exact/partial/unavailable/conflict/invalid/unsupported
  rows, keeps representative counts at zero, rejects promotion, and defers an exact substantive tie. Six independent
  context-attribution boundaries across all four shapes uniquely clear the decision floor and select only
  `AdmitOneConcreteContextAcquisitionScenario`; no successor semantics are implemented by W6. W6.7 repository closure
  passed at exact source baseline `440053ad1`: locked restore; strict Release at 0 warnings/errors; unit 502/502; Fast
  121/121; complete integration 144/144; focused W6 2/2; optimized context 1/1; all with zero skips; plus Markdown,
  headless-workflow, and authored-vocabulary guards. The owner explicitly overrode W6's hosted-only block on
  2026-07-16 because the GitHub billing condition cannot be remediated in this milestone. That disposition is not a
  hosted pass and does not weaken the workflow. W6 is closed; its selected context-acquisition category now informs
  the completed W7 static-field-expression and debugger-context slice without retroactively implementing successor
  work. W6's umbrella scale is `~10K LOC` split into `~1K LOC` evidence checkpoints.
- **Current evidence:** the Windows fixtures generate and open real dumps read-only, discover a strongly GCHandle-rooted object, validate both its handle slot and object-header method table with counted raw-memory reads, then read `Int32`, `Nullable<Int32>`, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump memory. The MethodDef RVA, header, code, locals token, padding, and declared EH sections are dump evidence; an independently opened disk PE is a comparison oracle, never an input to the executable dump body. The query path parses each bounded expression once with the pinned complete Roslyn expression parser, admits only the versioned W2/W5/W6/W7 tree subsets, binds either a typed snapshot root or one exact static declaration, and evaluates immutable plans without reparsing or rebinding. W7 adds counted selected-frame/PDB/import context, fully qualified and contextual static binding, ordinary static scalar/string/nullable/reference storage, target validation, and unchanged W2/W6 suffix reuse. W8.1 adds pre-contract emitted TypeSpec/import/constraint/literal evidence, candidate-keyed runtime construction identity, exact strategy-specific storage facts, exact memory-homed frame roots, typed context/frame-generic non-admissions, and close/reopen replay. Canonical request, context, symbol, storage, plan, root-selection, and complete-result identities preserve the distinctions needed for deterministic replay. The legacy 22-case W2 corpus, W3/W4 generated-dump lanes, W6 conformance/portfolio lanes, W7's sixteen-dump/four-shape portfolio, and W8.1 physical gates all remain passing; CoreCLR and high-level runtime reads remain late oracles, not inputs to interpreter shape, dump evidence, preparation, or execution.
- **Physical scope:** ten source projects contain active contracts or behavior in a twenty-project solution.
  `Interpreter.Product.DumpDebugging` owns standalone target projection, W5 expression classification/acquisition/
  evaluation, canonical rooted preparation/execution, and detached ClrMD binding without exposing live dump resources.
  `Interpreter.Headless.ReferenceConsumer` is the independently launched prototype composition root and report runner;
  it is not a shipping CLI contract. The earlier empty placeholders and later out-of-scope experiments remain removed,
  and physical boundaries are still justified by executable evidence rather than speculative package maps.
- **Primary progress signal:** executable scenarios and tests, with the design under `docs/` kept just ahead of and consistent with that evidence. This remains prototype evidence, not a production-ready evaluator or interpreter.

The normative W4 contract is
`docs/proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`. Its generated-dump incident
question asks what branchless `DumpProbe.GetMarkerSummary` computes through the direct `CombineMarkers` helper from
the captured marker fields. W2 cannot answer that question: its immutable query plan reads one selected field and may
coalesce an exact null, but it neither combines two field observations nor executes user IL. Under W4, exact admitted
method/body and field evidence must produce the exact `Int32` answer; an admitted partial or unavailable required
field must instead produce a provenance-bearing typed unknown, never a fabricated zero or another concrete fallback.
Conflict and invalid evidence retain their typed failure outcomes rather than masquerading as unknown values. Every host result is
`CounterfactualExecution` under a named policy and explicit assumptions, not evidence that the target historically
executed either method.

W4.1 implementation checkpoint `82363585b` adds the exact optimized fixture and four fast facts: the 18-byte caller
and four-byte helper bodies, relational FieldDef/MethodDef and signature/header facts, the exact CoreCLR value, and the
current W3 whole-body boundary. At that checkpoint the boundary was the second `ldfld` at IL offset 7; the raw direct
`call` was fixed at offset 12 but not admitted. Headless local verification passed locked restore, a fifteen-project Release
build with zero warnings/errors, the focused W4.1 lane at 4/4, the complete milestone-selected fast lane at 71/71, and
the ordinary dump regression at 5/5 with zero skips. The realized W4.1 surface is 478 added or materially revised LOC.

W4.2 implementation checkpoint `e89e43498` adds a second meaningful value domain over the shared W3 handlers. It
admits policy-enabled, provenance-bearing unknown `Int32` arguments while rejecting bare top, bottom, foreign roots,
and structurally incompatible values at executable boundaries. Semantic equality, hashing, order, join, meet, and
widening ignore explanations; the separate immutable lineage DAG canonically records only W4.2's `InputOrigin` and
ordered `BinaryTransform` nodes, embeds exact operands, and replays byte-for-byte in fresh domain and machine objects.
Exact E2 `ldfld` remains executable through the second domain; partial or unavailable field continuation and its
`FieldLoadTransform` were intentionally left to W4.3. Headless verification passed the fifteen-project Release build with zero
warnings/errors, focused W4.2 tests at 53/53, the full unit suite at 156/156, fast integration at 71/71, ordinary dump
at 5/5, and both documentation guards, all with zero skips and the milestone test selection on behavioral test commands.

W4.3 implementation checkpoint `7479b1ad4` closes that dump-free field seam without adding a ClrMD adapter or product
surface. Immutable, content-equal `FieldLoadEvidence` retains the exact field, partial/unavailable status, stable
reason, complete source/imported-object identities, address, requested width, observed width, and copied byte prefix;
`MemoryLoadResult.FromFieldEvidence` carries it without changing legacy code-only results. The shared `ldfld` handler
continues only when `UnknownExecutionPolicy.ExplainedInt32`, structured evidence matching the frozen field, and the
optional `IFieldLoadApproximationDomain<TValue>` capability all agree. Exact loads remain exact; code-only
partial/unavailable results and missing policy/capability remain blocked, conflict remains blocked, and invalid or
mismatched structured evidence remains invalid without consuming the failed instruction. A successful approximate
load preserves memory, emits `InstructionExecuted` followed by `ValuePrecisionLost` at the `ldfld`, and creates
canonical `InputOrigin` plus `FieldLoadTransform` lineage that replays byte-for-byte without changing W4.2 identities.

Headless verification at the W4.3 checkpoint passed the strict fifteen-project Release build with zero warnings and
errors, focused W4.3 tests at 55/55, the complete unit suite at 211/211, fast integration at 71/71, ordinary dump
regression at 5/5, optimized dump regression at 1/1, and both Markdown/headless guards, with zero skips. Every test
command was headless and used the milestone test selection.

Pushed W4.4a checkpoint `2e596c117` adds body-free contextual direct-call resolution. The content-equal
`MethodCallSignatureShape` and `ResolvedMethodCallTarget` freeze a non-nil same-module MethodDef, its declaring
TypeDef, exact calling-convention/receiver/generic/parameter/return facts, and ordinary managed-IL certification
without acquiring an RVA, body, local signature, or locals. SRM classifies structurally valid `MemberRef` and
`MethodSpec` operands as unsupported rather than malformed, rejects cross-module or incompatible identities, and
preserves the disposition-before-body seam needed by W4.6's future opaque-model selection.

Pushed W4.4b checkpoint `742ef2c4f` adds a separate W4 graph-admission mode while leaving the legacy single-method
machine path unchanged. `MethodGraphPlanner` uses deterministic root-first, call-site-ordered discovery and
first-result resolution caches, retains and charges every direct-call edge, deduplicates equal method and field
dependencies, and enforces fixed internal caps of 64 distinct methods and 1,024 traversal units. Success exposes one
canonical complete acyclic graph with fully admitted method nodes, fields, call sites, shared-callee deduplication,
signature/definition correlation, and longest-path required logical depth; cycles, descriptor conflicts, unsupported
suffixes, cap exhaustion, and resolver failures expose no partial plan and execute no instruction. The exact W4
fixture freezes two methods, two fields, one call at IL offset 12, required depth two, and five traversal units.

Headless W4.4 verification passed locked restore; the strict fifteen-project Release build with zero warnings/errors;
the planner lane at 35/35; the W4 fixture lane at 6/6; the complete unit suite at 250/250; fast integration at 73/73;
ordinary dump regression at 5/5; optimized dump regression at 1/1; and both Markdown/headless guards, with zero skips.
Every behavioral command ran through the headless wrapper and used the milestone test selection.

Pushed W4.5a checkpoint `356c07037` binds one immutable frozen graph to an opt-in machine session and executes exact
direct calls without re-resolution. Structural call-site and return-site identities make caller advancement, callee
activation, helper return, and root completion replayable. Each completed `call` or `ret` consumes one instruction;
events expose the ordered instruction/frame boundary; persistent memory is preserved; and the operational envelope
retains configured depth, required graph depth, logical-depth high water, and active-frame high water. Prepared-depth
underflow rejects before activation, while runtime graph/state disagreement remains an atomic invalid invariant.
Legacy W3 activation stays call-free and mutually exclusive with prepared-graph execution.

At the W4.5a checkpoint, only exact call arguments and returns executed. Explained unknowns blocked atomically at call
and interpreted-return boundaries with `EXEC_CALL_LINEAGE_UNAVAILABLE`; canonical `CallArgumentTransform` and
`InterpretedReturnTransform` lineage remained W4.5b work. An independent audit closed every identified W4.5a blocker.
Headless validation passed locked restore; the strict fifteen-project Release solution build and strict Release builds
of the unit and integration projects, all with zero warnings/errors; focused prepared-graph tests at 25/25; the W4 fixture at 7/7; the complete unit
suite at 275/275; fast integration at 74/74; ordinary dump regression at 5/5; optimized dump regression at 1/1; the
Markdown guard across 62 files and 41 local destinations; and the headless guard across one workflow. Every test lane
had zero skips and every behavioral command used the milestone test selection. The dump filters were
`Category=Dump&Corpus!=ModeledIncidentContextV1` and
`Category=Dump&Corpus=ModeledIncidentContextV1`, respectively.

Pushed W4.5b checkpoint `c72f6ee9e` completes that interpreted-call kernel. The optional
`IInterpretedCallLineageDomain<TValue>` capability extends the precision contract: exact arguments and returns pass
through unchanged, while each explained-unknown argument receives an atomic metadata-ordered
`CallArgumentTransform` containing the complete direct-call site, parameter index, and predecessor. An explained
unknown returned by the helper receives one `InterpretedReturnTransform` containing that same structural call site and
its callee-side predecessor before caller mutation. Node kinds 4 and 5 append to canonical schema v1; the first three
node kinds and every prior canonical identity remain byte-for-byte frozen.

The domain preflights the entire two-argument vector before interning any call nodes, captures only the reachable DAG,
and validates canonical bytes, identities, dependencies, types, call sites, and parameter indices before fresh-domain
replay. Default/malformed vectors and bare, foreign, bottom, or wrong-type values are rejected without partial
interning. At the machine boundary, absent capability remains `Blocked`/`EXEC_CALL_LINEAGE_UNAVAILABLE`, capability
exceptions normalize to `Blocked`/`EXEC_DOMAIN_FAILURE`, and malformed or semantically changed capability output is
`InvalidProgram`/`EXEC_CALL_LINEAGE_INVALID`; every failed boundary preserves state, memory, budget, and events.
Compiler evidence freezes a five-node mixed exact/partial graph and an eight-node partial/unavailable graph, including
same-session and fresh-session replay without metadata re-resolution.

Headless validation at `c72f6ee9e` passed locked restore and the strict fifteen-project Release build with zero warnings/errors;
prepared-graph execution 40/40; the combined lineage/audit lane 76/76, including 29 frozen legacy identity cases;
compiler lineage 2/2; aggregate W4 integration 9/9; complete unit 297/297; fast integration 76/76; ordinary dump 5/5;
and optimized dump 1/1. There were zero skips, every behavioral filter used the milestone test selection, and an independent
audit found no remaining production or test finding.

Pushed W4.6a checkpoint `77c92789b` adds bounded, non-generic pure-model identity/version, descriptor, typed
invocation/outcome, and structural registry contracts. Explicit `RequirePureModel` selects only an exact,
`EvaluationEffectStatus.None` descriptor after caller call resolution/typing and before target-body acquisition;
default `Prepare` stays interpret-only. The frozen graph exposes canonical `PureModel` call disposition,
`ModeledLeaves`, and lookup over a body-free opaque leaf. Selection is deduplicated, traversal/depth remain truthful,
no failure falls back to target interpretation or exposes a partial plan, runtime capability identity is excluded from
graph equality/hash, and the legacy interpreted call hash remains unchanged. The exact compiler graph is root plus
modeled leaf, two fields, and one edge: five units at depth two.

W4.6a does not execute a model. `ActivatePreparedGraph` rejects every modeled graph before argument validation or
state creation with `EXEC_MODEL_EXECUTION_UNAVAILABLE`. Headless validation at exact commit
`77c92789b16d9258c907d5026a36e39f8c957b41` passed locked restore; strict fifteen-project Release build 0/0;
contract 49/49; model planner 25/25; legacy planner 35/35; SRM 1/1; lineage 2/2; unit 371/371; fast 77/77; ordinary
dump 5/5; optimized dump 1/1; Markdown 62/41; workflow guard 1; and zero skips. Behavioral filters used
the milestone test selection, and independent audits found no behavioral findings. The deterministic PDB-free TestTarget PE
SHA-256 is `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; W4.5 lineage hashes were deliberately
re-frozen to it.

Pushed W4.6b checkpoint `fd723a912` adds optional `IPureCallModelLineageDomain<TValue>` and append-only schema-v1
kind 6, `ModeledReturnTransform`. Exact arguments are embedded canonically; explained-unknown arguments first receive
their unchanged kind-4 call-boundary nodes, and the complete modeled-return dependency batch is validated and interned
atomically. Kinds 1–5 retain their canonical bytes and identities. W4.6b realizes 1,003 added LOC (481 production plus
522 tests), bringing W4.1–W4.6b to 20,779 LOC.

Pushed W4.6c checkpoint `877c9fb55` removes W4.6a's temporary activation block and invokes only the capability frozen
with the prepared leaf. Execution never re-queries a resolver, registry, descriptor, target body, or model selector and
never falls back to interpretation. A validated exact or grounded explained-unknown outcome atomically pops the two
arguments, advances the caller, pushes the return, consumes one instruction, and emits one ordinary instruction event
without creating a helper frame or changing memory. Blocked, invalid, malformed, and normalized capability outcomes
preserve semantic state, memory, instruction budget, and semantic events while retaining a deterministic operational
attempt. Logical-depth high water records every entered modeled boundary, including a non-transferring attempt, while
active-frame high water remains one; terminal validation checks the attempt chronology, call/model counts, and exact
completion witnesses. W4.6c realizes 2,734 LOC: 1,425 production additions plus 1,309 unit-test additions.

Pushed W4.6d checkpoint `da5346813` proves that behavior against the compiler-emitted caller and real SRM preparation.
The selected model agrees with both interpretation and CoreCLR for exact marker inputs, and with interpretation for
mixed partial/exact and partial/unavailable inputs. Exact execution completes in six caller instructions and two field
loads, at logical/active-frame high water 2/1, with one completed model attempt, unchanged memory, no helper frame or
helper-body read, and no execution-time re-resolution or reselection. The mixed-evidence case freezes literal graph
SHA-256 `451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`. Same-session and fresh
SRM/domain/machine runs reproduce the dual-unknown graph SHA-256
`31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f` over the deterministic PDB-free TestTarget
artifact above. W4.6d realizes 956 integration-test additions.

W4.6 closure passed locked restore; the strict fifteen-project Release build and strict unit/integration Release builds
with zero warnings/errors; focused W4.6c 34/34; focused W4.6d 3/3; aggregate W4 integration 13/13; complete unit
413/413; fast 80/80; ordinary dump 5/5; and optimized dump 1/1. Every lane was headless, every
behavioral filter used the milestone test selection, and there were zero skips.

Pushed W4.7a checkpoint `2e70fe76d` adds the first counterfactual product assembly and a deliberately standalone
`CounterfactualTargetOutcomeProjector`. Projection accepts only the complete sequence of exact `IlMachine.StepOne`
outcomes issued by the same machine from one legacy root activation at IL offset zero through the first exact
`NullReference` target latch; an optional single idempotent re-step must preserve state and operational-state identity,
budget, events, and exception. A structurally plausible caller-authored outcome is not execution evidence: every
transition is issuer-bound to its exact machine and predecessor references. Successful projection validates the
one-frame envelope, exception kind/code/location, memory identity, instruction accounting, and complete ordered event
transcript before producing fixed `CounterfactualExecution`/`Completed`/`Complete`/`Exact`/`None` axes.

The schema-v1 fragment retains only target-exception identity/location, the structural call trace, instruction
accounting, events, one stable diagnostic, canonical bytes, and lowercase SHA-256. Snapshot, root, request, plan, and
traversal identities are absent because no rooted product request reached this conformance case; issuer certification
is validation authority, not canonical content. The synthetic literal fixture freezes SHA-256
`a9b98e46583dcf90ac108571c126d8d86cec0465c595e2689fae767e33ff108e`. Pushed W4.7b checkpoint `dad6a6dd4`
proves direct and constant-adjusted compiler-emitted typed-null getters through fresh SRM/module/domain/machine
reconstruction. Counting and poisonable resolver, domain, and memory wrappers prove a terminal re-step consults no
capability and repeats no field load; both projections remain byte-, content-, and hash-identical.

W4.7 closure passed locked restore; the strict sixteen-project Release build, covering eleven source projects, with
zero warnings/errors; complete unit 430/430; Fast 80/80; ordinary dump 5/5; optimized dump 1/1; focused W4.7a 15/15;
focused W4.7b 2/2; combined W4.7 17/17; compiler differential class 23/23; Markdown 62 files/41 destinations; and the
one-workflow headless guard. Every behavioral lane was headless, used the milestone test selection, and had zero skips.

W4.8 checkpoints `4f268a4bc` through `44b050ec8` turn those kernels into the product contract. Configurable ordered
traversal charges fail before the next capability at exact exhaustion. Schema-v1 request, field-observation, plan, and
result artifacts retain every applied bound and structural dependency behind defensive copies and issuer authority.
Private typed bindings materialize exact or explained inputs into a fresh domain and a read-only recording memory;
`Prepare` validates authority, evidence, traversal, depth, graph, observations, and activation in a fixed order.
`Run` then validates every issued machine transition and projects completed, budget-exhausted, cancelled, blocked,
invalid, modeled, and standalone-target outcomes without execution-time resolver or registry queries. Same/fresh-
object replay freezes the exact synthetic rooted result SHA-256
`8eb243d415e739a9101f0dd7b8e1cdb4de38208069caa495b8c1bd02a37b7d09`.

W4.9a checkpoint `24bd8fe6f` adds a canonical ClrMD method graph and three-state correlated field evidence; W4.9b
checkpoint `2d41f528d` derives rooted product identities and a detached persistent memory from issuer-validated dump
evidence; W4.9c checkpoint `a8b5f32f0` executes six interpreted/modeled exact, partial, and unavailable rows from one
generated full dump. Both ClrMD sessions are disposed before product preparation/execution. Reopen/rebind reproduces
byte-identical detached memory, request, plan, and result artifacts, while disk SRM and CoreCLR remain late independent
oracles. The focused generated-dump lane passes 1/1, ordinary dump 6/6, and Fast 88/88 headlessly with zero skips.
W4.9d local closure passes locked restore; a strict 16-project Release build at 0 warnings/errors; complete unit
502/502; Fast 88/88; ordinary dump 6/6; optimized dump 1/1; aggregate W4 integration 14/14; Markdown 62 files/44
local destinations; and the one-workflow headless guard. Every behavioral filter uses the milestone test selection and every
lane has zero skips. [GitHub Actions run 29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083)
passed documentation, build/Fast/unit, real-dump, and optimized-dump jobs at exact W4 closure commit
`a819a08fd9ccdf926620c505732475990b242be9`. Final documentation-closure commit
`aaec73c5b987089addb539d3628de67bd815bd8f` passed those same four jobs in [run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230).

The historical W4.2 checkpoint records 3,454 realized LOC: 3,429 attributable implementation LOC (1,521 production
plus 1,908 focused tests) and 25 LOC that segregate an excluded test scope from the milestone lane. Together with
W4.1, that checkpoint had realized 3,932 LOC and projected 18,532–26,132 LOC. W4.3 realizes 3,096 LOC (1,100
production LOC plus 1,996 test LOC), so W4.1–W4.3 cumulatively realized 7,028 LOC and projected 19,228–25,728 LOC.
W4.4 realizes 3,651 added LOC: W4.4a contributes 1,043 (665 production plus 378 tests), and W4.4b contributes 2,608
(1,411 production plus 1,197 tests). The post-audit split keeps each independently delivered sub-slice below the
3,500-LOC ceiling while preserving W4.4's original combined 1,700–2,600 estimate as historical calibration. W4.1–W4.4
therefore cumulatively realize 10,679 LOC and produced the historical 21,179–26,779 projection. W4.5a realizes 3,334
LOC (1,590 production plus 1,744 tests). W4.5b realizes 2,804 added LOC (766 production plus 2,038 tests), so combined
W4.5 realizes 6,138 LOC and W4.1–W4.5 cumulatively realize 16,817 LOC. The historical W4.5b estimate of
1,800–2,700 LOC and combined W4.5 projection of 5,134–6,034 LOC were each exceeded at the upper bound by 104 LOC.
The W4.5-closure projection was 25,017–29,417 LOC, followed by historical 27,217–32,117, 28,376–32,476,
28,876–33,276, 28,826–33,726, 28,879–33,279, and 30,079–33,729 projections. W4.6a realizes 2,959 added LOC (1,210
production plus 1,749 tests/fixture support), W4.6b 1,003, W4.6c 2,734, and W4.6d 956. Combined W4.6 therefore
realizes 7,652 LOC and W4.1–W4.6d cumulatively realize 24,469 LOC. W4.7a realizes 2,448 LOC and W4.7b 353 LOC, so
W4.7 realizes 2,801 LOC and cumulative W4 realization through W4.7 is 27,270 LOC. Its former 2,200–3,150 estimate and
the resulting 31,069–34,319 full-W4 projection are historical calibration. Delivery split W4.8 into six attributable
sub-slices totaling 11,924 LOC and W4.9a–c into 1,389/791/518 LOC, totaling 2,698 LOC. Cumulative W4 realization is
therefore 41,892 hand-written implementation LOC after excluding documentation and generated lockfile changes. This
exceeds the last 31,670–33,970 projection and the original 16,860–25,310 baseline; both remain recorded as calibration,
not rewritten estimates. Allocation remains unadmitted and its bound is absent/not applied until a later allocation
scenario. All specified exact, degraded-evidence, budget, differential, same/fresh-object, and dump-close/reopen cases
are implemented, and exact pushed hosted evidence is recorded above. W4 is closed for its defined milestone-selected
scope.

The W1 dump-evidence slice is executable against generated full and intentionally sparse dumps. W2's restricted dump-query v1 is complete for its milestone-selected scope: typed root states, `Parse`/`Prepare`/`Evaluate(plan)` staging, immutable object-specific plans, exact `String`/`Int32`/`Nullable<Int32>` behavior, stable diagnostics, and all-case same/fresh-session replay are exercised against the generated full dump. [GitHub Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs at exact W2 closure commit `5bed47100`. W1 remains complete for its revised milestone-selected evidence scope: typed exact/partial/unavailable/conflict outcomes, honest answer completeness, stable identity/context/provenance, path-accurate bounds, fresh-session canonical replay, headless execution, truthful topology, and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

W3 strengthened implementation checkpoint `19c292f9f` completes the code and local evidence for its deliberately closed
milestone-selected architecture proof. Headless local verification passed locked restore; a fifteen-project Release
build with zero warnings and errors; 103 semantic/admission/differential tests; 67 fast integration tests; 5 ordinary
dump tests; 1 optimized-context dump test; and the focused 2-test W3 dump lane, all with zero skips. W3 does not add a
product-facing method evaluator or claim historical execution. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four jobs at
the exact implementation commit. [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
milestone-selected scope.

Caveat: current evidence covers only the named generated fixtures and explicitly admitted input shapes. Earlier
out-of-scope experiments have been removed, and the workflow now runs every remaining test in
each selected category. No result through W8.1 establishes behavior for other artifact shapes. W8.1 is physical
evidence for its named branches. Only the W8.2 contract families identified above are implementation evidence;
unlanded W8.2 work and W8.3–W8.10 remain plans rather than behavior evidence.
The terminology cleanup renamed a fixture-only environment canary and therefore intentionally re-froze the current
TestTarget PE and its four derived canonical graph identities in the executable tests. Historical hashes above remain
evidence for their named commits; they are not claims about the current fixture binary.
Immutable upstream snapshots under `lib/` remain verbatim reference material; their vocabulary is not project-authored
scope or a claim of supported behavior.
Required framework namespace and member identifiers also remain literal API names; they do not widen project scope.

A versioned optimized Release modeled-incident report keeps five predeclared axes in the denominator. The current v2
report records raw member bytes at 5/5, attributable context at 2/5 after the ClrMD 4.0 static-slot correction, and
unchanged product-query availability at 1/5. This is explicitly generated evidence, not a representative external
incident corpus, so no readiness rate is claimed. Current in-process caps (8 GiB dump admission, 256 MiB ClrMD dump
cache, and 512 MiB managed PE admission) remain resource controls. Branches, CFG merge/fixpoint analysis, handler-
transfer EH, virtual stepping, broad call/model catalogs, open generic execution/context reconstruction beyond W8's
bounded closed-construction owner grammar and evidence-conditioned branches, allocation, async/dynamic lifting, live
speculation, no-JIT runtime hosting, and additional product surfaces are **research backlog, not delivery commitments**.

## Where to go next

For structured topic lists, document inventory, and recommended reading paths, start here:

- **Repository-wide design and architecture review:** `DESIGN-ARCHITECTURE-REVIEW.md`
- **Active post-W7 W8 sequence (W8.1 complete; W8.2 active):** `docs/plans/post-w7-path-forward.md`
- **W8.1 physical-truth disposition:** `docs/plans/w8-1-physical-truth-disposition.md`
- **Completed post-W6 W7 sequence:** `docs/plans/post-w6-path-forward.md`
- **Completed post-W5 W6 sequence:** `docs/plans/post-w5-path-forward.md`
- **C# parsing and versioned subset-admission contract:** `docs/proposals/architecture/csharp-expression-front-end-contract-proposal.md`
- **Completed W5 selection record:** `docs/plans/post-w4-path-forward.md`
- **Documentation index and TOC-like navigation:** `docs/README.md`
- **Normative W2 language, binding, plan, evidence, and replay contract:** `docs/proposals/architecture/restricted-dump-query-contract-proposal.md`
- **Normative W3 concrete activation, admission, memory, outcome, and replay contract:** `docs/proposals/architecture/concrete-il-execution-contract-proposal.md`
- **Normative W4 branchless counterfactual method, unknown, call, budget, and replay contract:** `docs/proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`

For process and roadmap context:

- `docs/governance/project-faq.md`
- `docs/governance/documentation-organization-proposal.md`
- `docs/plans/post-w7-path-forward.md`
- `docs/plans/post-w6-path-forward.md`
- `docs/plans/post-w5-path-forward.md`
- `docs/plans/post-w4-path-forward.md`
- `docs/plans/future-work-planning.md`

## How to use this repository

1. Use this top-level README for intent and orientation.
2. Use `docs/README.md` as the canonical index of topics and reading paths.
3. Read proposals in the sequence that matches your goal (product, architecture, integration, or governance).

## Contribution focus (this phase)

High-value contributions advance or challenge the active executable evidence:

- strengthen dump reads, identity joins, partial evidence, and truthful failure behavior within the supported generated
  fixture boundary;
- preserve the closed restricted dump-query v1 behavior and the pinned Roslyn front end as the sole expression parser,
  and extend only versioned binding/evaluation when a concrete incident scenario justifies the next tree/evidence step;
- preserve W3's structural activation, typed whole-body admission, exact-evidence import, and deterministic outcome boundaries;
- preserve the closed W4 `GetMarkerSummary`/`CombineMarkers` contract and make it reachable through W5's bounded
  expression-to-result product path without widening interpreter semantics;
- preserve W6's one-parse/versioned-admission boundary and opt-in fixed-depth member-chain contract,
  including complex valid-but-unsupported parser tests, certified terminal data properties, counted reference evidence,
  no-rebinding plans, null/coalesce truth, and unchanged W5 default reports;
- preserve or challenge W7's fully qualified/contextual static-field expression profile, selected-frame/PDB/import
  evidence, counted slot/value reads, exact-null/non-exact outcomes, and no-fallback binding;
- implement or challenge W8's additive V2 plan as one coherent bounded name-binding pipeline: nested/constructed
  owners, scope-precise aliases/imports, constraints/accessibility/assignability, stored/literal value strategies,
  lexical completeness for bare members/`using static`, evidence-conditioned frame/storage branches, and unchanged
  fully qualified/raw-memory/suffix controls;
- preserve W5.5b's designed-evidence caveat; do not substitute broader syntax, context, opcode, EH, model, or debugger
  work without a new evidence decision;
- add deterministic, differential, and scenario tests at proven boundaries;
- tighten architecture and documentation when executable evidence changes a decision;
- keep design work just ahead of code rather than expanding speculative surface area.

## License

This repository is licensed under the **MIT-0 (MIT No Attribution)** license. See [`LICENSE`](LICENSE) for the full text.
