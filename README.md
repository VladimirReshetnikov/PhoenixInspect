# Tools: IL Interpreter & Dump-Time Evaluation (Concept Design)

This repository is the **design home** for an experimental .NET IL interpreter and a safe, explainable system for evaluating expressions against memory dumps.

If you only read one thing: this project is about making dump-time debugging workflows more trustworthy through deterministic execution, bounded analysis, and explicit explanations when answers are partial or unknown.

## Project gist

We are designing and prototyping—not yet shipping—a library and architecture that can power experiences such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) over dump-backed sessions,
- explainable analysis when runtime behavior cannot be reproduced exactly.

Core principles:

- **Deterministic and budgeted execution** over unbounded simulation.
- **Safety-first behavior** over risky “best effort” guessing.
- **Explainability and provenance** over opaque results.
- **Composable architecture** so hosts can integrate incrementally.

## Current phase

- **Status:** conceptual design with an executable prototype, progressing through evidence-led vertical slices.
- **Active delivery target:** a deterministic, read-only evaluator grounded in a .NET dump. W4 has an admitted
  branchless counterfactual-method contract, a validated W4.1 value-gate fixture, a validated W4.2
  provenance-aware execution kernel, a validated W4.3 dump-free non-exact field seam, and W4.4's validated body-free
  direct-MethodDef resolution plus complete frozen call graph. W4.5 now executes exact and explained-unknown values
  across frozen direct-call graphs with deterministic multi-frame accounting and canonical call/return lineage. W4.6
  now freezes one exact/no-effect structural pure model as a body-free opaque graph leaf, executes only that frozen
  capability through exact or grounded-unknown atomic caller transfer, records truthful model attempts and distinct
  logical/frame depth witnesses, and proves compiler/SRM exact and degraded conformance. The counterfactual product and
  dump-grounded W4 execution have not landed; W4.7 is the next unimplemented slice.
- **Current evidence:** the Windows fixtures generate and open real dumps read-only, discover a strongly GCHandle-rooted object, validate both its handle slot and object-header method table with counted raw-memory reads, then read `Int32`, `Nullable<Int32>`, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump memory. The MethodDef RVA, header, code, locals token, padding, and declared EH sections are dump evidence; an independently opened disk PE is a comparison oracle, never an input to the executable dump body. The W2 query path parses a closed root/field grammar, binds a typed snapshot-scoped root, selects the field once into an immutable plan, and evaluates that plan without rebinding. Canonical request, plan, root-selection policy, and complete-result identities preserve the exact literal, selector state, owner, full field layout, evidence, and applied-policy distinctions needed for replay. A versioned 22-case corpus spanning 20 distinct expression texts reproduces the complete canonical result byte sequence/SHA-256 for all cases and the canonical plan projection string/SHA-256 for the 13 cases whose preparation succeeds, both within one session and after disposing, reopening, rediscovering, and rebinding the dump. The W3 architecture proof adds structural module/type/method/field identities, SRM-derived signatures and initialized locals, metadata-derived activation, frozen typed whole-body admission, an injected persistent-memory capability, and closed branchless `Int32` arithmetic plus direct/constant-adjusted instance getters. Its generated-dump lane replays the counted physical body, correlates exactly one `ldfld` with one exact imported field observation, executes through the real memory model, terminates typed-null access in a latched target-exception state, and reproduces the canonical prepared-memory result after reopening and rebinding the dump. CoreCLR remains an outcome oracle, not an input to interpreter shape or dump evidence.
- **Physical scope:** ten source projects contain active contracts or behavior; the two newest projects implement the narrow broker/runner boundary for one-shot external dump queries. The earlier 33 empty placeholders remain removed, and physical boundaries are still justified by executable evidence rather than speculative package maps.
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
build with zero warnings/errors, the focused W4.1 lane at 4/4, the complete non-cybersecurity fast lane at 71/71, and
the ordinary dump regression at 5/5 with zero skips. The realized W4.1 surface is 478 added or materially revised LOC.

W4.2 implementation checkpoint `e89e43498` adds a second meaningful value domain over the shared W3 handlers. It
admits policy-enabled, provenance-bearing unknown `Int32` arguments while rejecting bare top, bottom, foreign roots,
and structurally incompatible values at executable boundaries. Semantic equality, hashing, order, join, meet, and
widening ignore explanations; the separate immutable lineage DAG canonically records only W4.2's `InputOrigin` and
ordered `BinaryTransform` nodes, embeds exact operands, and replays byte-for-byte in fresh domain and machine objects.
Exact E2 `ldfld` remains executable through the second domain; partial or unavailable field continuation and its
`FieldLoadTransform` were intentionally left to W4.3. Headless verification passed the fifteen-project Release build with zero
warnings/errors, focused W4.2 tests at 53/53, the full unit suite at 156/156, fast integration at 71/71, ordinary dump
at 5/5, and both documentation guards, all with zero skips and `Scope!=Cybersecurity` on behavioral test commands.

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
command was headless and used `Scope!=Cybersecurity`.

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
Every behavioral command ran through the headless wrapper and used `Scope!=Cybersecurity`.

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
had zero skips and every behavioral command used `Scope!=Cybersecurity`. The dump filters were
`Category=Dump&Corpus!=ModeledIncidentContextV1&Scope!=Cybersecurity` and
`Category=Dump&Corpus=ModeledIncidentContextV1&Scope!=Cybersecurity`, respectively.

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
and optimized dump 1/1. There were zero skips, every behavioral filter used `Scope!=Cybersecurity`, and an independent
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
`Scope!=Cybersecurity`, and independent audits found no behavioral findings. The deterministic PDB-free TestTarget PE
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
413/413; fast 80/80; ordinary dump 5/5; optimized dump 1/1; and external-worker 4/4. Every lane was headless, every
behavioral filter used `Scope!=Cybersecurity`, and there were zero skips.

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
realizes 7,652 LOC and W4.1–W4.6d cumulatively realize 24,469 LOC. Recalibrated W4.7 is 2,200–3,150 LOC, W4.8 remains
2,400–3,500 LOC, and W4.9 remains 2,000–3,200 LOC. The remaining W4.7–W4.9 envelope is 6,600–9,850 LOC, giving the
current full-W4 projection 31,069–34,319 LOC. Preserve the original 16,860–25,310 baseline and every earlier
projection. This remains machine/kernel and compiler-conformance evidence, not counterfactual product execution.
W4.7 target-outcome projection, configurable request traversal budget, product facade, ClrMD non-exact-field adapter,
generated-dump W4 result/corpus, hosted exact-commit evidence, and umbrella closure remain pending. Allocation remains unadmitted and its bound is therefore absent/not
applied until a later allocation scenario. Closure requires the specified exact, degraded-evidence, budget,
differential, and same/fresh-session replay cases to pass through the non-cybersecurity headless Release, fast, dump,
and focused W4 lanes with zero skips and at the exact pushed commit.

The W1 dump-evidence slice is executable against generated full and intentionally sparse dumps. W2's restricted dump-query v1 is complete for its non-cybersecurity scope: typed root states, `Parse`/`Prepare`/`Evaluate(plan)` staging, immutable object-specific plans, exact `String`/`Int32`/`Nullable<Int32>` behavior, stable diagnostics, and all-case same/fresh-session replay are exercised against the generated full dump. [GitHub Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs at exact W2 closure commit `5bed47100`. W1 remains complete for its revised non-security evidence scope: typed exact/partial/unavailable/conflict outcomes, honest answer completeness, stable identity/context/provenance, path-accurate bounds, fresh-session canonical replay, headless execution, truthful topology, and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

W3 hardened implementation checkpoint `19c292f9f` completes the code and local evidence for its deliberately closed
non-cybersecurity architecture proof. Headless local verification passed locked restore; a fifteen-project Release
build with zero warnings and errors; 103 semantic/admission/differential tests; 67 fast integration tests; 5 ordinary
dump tests; 1 optimized-context dump test; and the focused 2-test W3 dump lane, all with zero skips. W3 does not add a
product-facing method evaluator or claim historical execution. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four jobs at
the exact implementation commit. [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
non-cybersecurity scope.

A versioned malformed-minidump mutation corpus and a Windows x64 one-shot worker are retained as separately landed,
non-gating prototype work outside W1–W4 milestone evidence. The worker's malformed-artifact test passed locally at its checkpoint.
All current W1–W4 milestone test invocations exclude `Scope=Cybersecurity`; the five hostile-corpus facts and ExternalWorker test
project provide no milestone validation. Restore/build intentionally remains repository-wide across all 15 projects,
including the worker projects and IntegrationTests assembly, as topology/compilation-health evidence only—not
cybersecurity behavioral evidence. Their presence does not make external artifacts a supported product input.

A versioned optimized Release modeled-incident report keeps five predeclared axes in the denominator and records raw member bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5. It is explicitly generated evidence, not a representative private-production incident corpus, so no readiness rate is claimed and representative incident measurement is not a W1 completion gate. Current in-process caps (8 GiB dump admission, 256 MiB ClrMD dump cache, and 512 MiB managed PE admission) remain resource controls. Branches, CFG merge/fixpoint analysis, handler-transfer EH, virtual stepping, broad call/model catalogs, generics, allocation, async/dynamic lifting, live speculation, sandbox runtime hosting, and additional product surfaces are **research backlog, not delivery commitments**.

## Where to go next

For structured topic lists, document inventory, and recommended reading paths, start here:

- **Repository-wide design and architecture review:** `DESIGN-ARCHITECTURE-REVIEW.md`
- **Documentation index and TOC-like navigation:** `docs/README.md`
- **Normative W2 language, binding, plan, evidence, and replay contract:** `docs/proposals/architecture/restricted-dump-query-contract-proposal.md`
- **Normative W3 concrete activation, admission, memory, outcome, and replay contract:** `docs/proposals/architecture/concrete-il-execution-contract-proposal.md`
- **Normative W4 branchless counterfactual method, unknown, call, budget, and replay contract:** `docs/proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`

For process and roadmap context:

- `docs/governance/project-faq.md`
- `docs/governance/documentation-organization-proposal.md`
- `docs/plans/future-work-planning.md`

## How to use this repository

1. Use this top-level README for intent and orientation.
2. Use `docs/README.md` as the canonical index of topics and reading paths.
3. Read proposals in the sequence that matches your goal (product, architecture, integration, or governance).

## Contribution focus (this phase)

High-value contributions advance or challenge the active executable evidence:

- harden dump reads, identity joins, partial/malformed evidence, and secret-safe failure behavior;
- preserve the closed restricted dump-query v1 contract, and extend it only when a concrete incident scenario justifies the next grammar/evidence step;
- preserve W3's structural activation, typed whole-body admission, exact-evidence import, and deterministic outcome boundaries;
- implement or challenge the admitted W4 `GetMarkerSummary`/`CombineMarkers` contract without widening it to deferred
  branches, CFG/fixpoint analysis, or handler transfer;
- add deterministic, differential, and scenario tests at proven boundaries;
- tighten architecture and documentation when executable evidence changes a decision;
- keep design work just ahead of code rather than expanding speculative surface area.

## License

This repository is licensed under the **MIT-0 (MIT No Attribution)** license. See [`LICENSE`](LICENSE) for the full text.
