# Design Doc: Post-Mortem Expression Evaluator for .NET Dumps

> **Lifecycle:** Draft · **Roadmap:** Active
>
> W1–W5 are closed for their stated milestone-selected scopes. W5 made the W2/W4 capability reachable through one
> expression-to-result path and used twelve independently dumped synthetic incidents to select fixed-depth member
> navigation; it remains synthetic evidence rather than a
> field-readiness claim. The active successor is the
> [`Post-W5 Path Forward`](../../plans/post-w5-path-forward.md): an opt-in bounded member-chain query with counted
> reference evidence, narrowly certified field-backed terminal properties, one complete Roslyn expression parse with
> versioned subset admission, deterministic replay, and a new twenty-four-incident/four-shape synthetic decision
> corpus.
> Branches, handler transfer, virtual scratch objects, async/dynamic lifting, and advanced query workflows remain
> research backlog rather than inherited commitments.

## 1) Summary

When debugging a crash dump, engineers frequently need answers that are “one computed step away” from what the raw heap shows:

* “What’s `request.User.Email` if I follow null checks and wrappers?”
* “Which items in this list match predicate X?”
* “What’s the effective configuration value after overrides?”
* “What’s inside this `Task` / `ValueTask` / `Lazy<T>` / `AsyncLocal<T>`?”

Today, post-mortem workflows force users into manual object-walking and mental evaluation. A live debugger solves this
with expression evaluation, but a dump has no running runtime to execute code. The active feature is a
**deterministic, policy-constrained, read-only evaluator** grounded in dump evidence. W2 implements its first
restricted C# query surface. The closed W3 getter proof validates architecture below the product boundary. W4 adds one
branchless counterfactual method question. W4.1–W4.7 establish its fixture, unknown-aware kernel, direct-call graph/
execution, one body-free pure model, and standalone exact-null outcome. W4.8 checkpoints through `44b050ec8` add
configurable traversal, canonical request/plan/result contracts, private typed bindings, authoritative preparation/
execution, and common rooted/standalone projection. W4.9 checkpoints through `a8b5f32f0` add the ClrMD graph/field
producer, detached rooted dump memory, and six exact/degraded interpreted/modeled generated-dump rows with close/
reopen canonical replay. W4 implementation closed at exact commit `a819a08fd` in [hosted run
29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083); final documentation-closure
commit `aaec73c5b` passed the same matrix in [run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230). An isolated virtual heap and
broader method workflows remain research.

---

## 2) Problem Statement

**Dump analysis is high-friction for derived questions.**
The heap contains the data, but not the *answers*. Many useful questions require:

* chasing indirections (`Nullable<T>`, wrapper types, `Lazy<T>`, caching layers)
* interpreting simple logic (null coalescing, ternaries, comparisons)
* running lightweight helper methods (pure getters, `ToString`, formatters)
* filtering or projecting collections (LINQ-style questions)

Without an evaluator, users either:

* export data and write offline scripts, or
* do tedious manual steps in UI, or
* attach a live debugger (often impossible for production incidents)

We want dump debugging to feel closer to a modern IDE: **ask a question, get an answer**, with guardrails.

---

## 3) Vision

Bring a **first-class “Immediate/Watch” experience** to post-mortem .NET debugging:

* Evaluate common C# expressions in the context of a selected dump thread/frame.
* Treat the dump as immutable evidence; evaluation cannot mutate it.
* Later research may allow “what-if” exploration via a virtual scratchpad heap without affecting snapshot data.
* Provide clear semantic-mode, completeness, evidence, effect, and provenance signals.

The end-state experience should feel like:
**“Rider/VS Immediate Window, but for dumps, with explicit limits and deterministic behavior.”**

---

## 4) Target Users and Scenarios

### Primary users

* Engineers doing production incident response
* Performance engineers analyzing hangs and memory dumps
* Support/triage engineers doing initial dump investigation

### Core scenarios

The active slice starts with snapshot-grounded navigation and simple derived queries. The broader scenarios below are product hypotheses, not all MVP commitments.

1. **Crash triage**

   * Evaluate `ex.InnerException?.Message`
   * Inspect `httpContext.Request.Headers["X-Correlation-Id"]`
2. **Hang analysis**

   * Evaluate state machines / async continuations
   * Check queue lengths / blocked locks / pending tasks
3. **Memory investigation**

   * Filter large collections (`cache.Items.Where(...)`)
   * Summarize object graphs
4. **Data correctness**

   * Compute “effective configuration” from layered sources
   * Validate invariants on suspicious objects

---

## 5) Product Principles

1. **Snapshot integrity is sacred**

   * The dump is read-only. Results must not imply the dump has changed.

2. **Deterministic and bounded**

   * Evaluation should complete within deterministic resource budgets or report the exhausted budget clearly; host cancellation is reported separately.
   * No unbounded loops, recursion blowups, or runaway allocations.

3. **Conservative execution model**

   * No filesystem/network/process/thread/time access.
   * No arbitrary native interop.
   * Read-only derived queries by default; any later code execution requires explicit policy and counterfactual labeling.

4. **Honest results**

   * If a result is incomplete, approximated, or blocked, we say so explicitly.
   * No “looks right” guesses.

5. **Low cognitive overhead**

   * Familiar C# expression syntax.
   * Good completions and clear error messages.

---

## 6) User Experience Overview

### Entry points (where users interact)

* **Watch window**: add expressions, persistent across dump session
* **Immediate window**: ad-hoc evaluation
* **Data tips**: hover to evaluate `obj.Prop` or “Add to Watch”
* **Debug Visualizers**: richer views for known types (tasks, strings, dictionaries, spans)

### Context selection model

The end-state product runs evaluation in a specific **context**:

* Selected dump
* Selected thread and stack frame (when available)
* `this`, locals, and arguments (to the extent recoverable)
* Imported namespaces and type aliases (configurable)

W2 v1 deliberately uses only the selected dump plus one host-named, exactly selected non-null heap object. It does
not discover frame roots, locals, arguments, statics, or exact-null roots.

### Output and result indicators

Each evaluation returns separate, machine-readable axes:

* semantic mode (`Observation`, `DerivedQuery`, W4 `CounterfactualExecution`, or later-research `AbstractAnalysis`)
* completion status (completed, blocked, budget-exhausted, cancelled, decision-needed, or failed)
* completeness (complete, partial, or none)
* evidence status (exact, partial, unavailable, conflicting, or invalid)
* effects/virtual-write summary
* value + type, provenance, and diagnostics

A host may synthesize a compact result indicator from those fields, but the badge must not replace them or imply more certainty than the evidence supports.

---

## 7) Scope: What We Will Support

This is intentionally phased so we can deliver value early without promising “execute arbitrary .NET code from a dump.”

### Phase 1 — Read-only, high-confidence expressions (MVP)

Goal: prove that a small expression/query surface materially improves common “what’s in here?” investigations.

The Phase 1 capability set is admitted incrementally. W2 v1 is now a closed implemented subset, not a claim that the
broader Phase 1 candidates below are already available. Its normative definition is the
[Restricted Dump Query v1 Contract](../architecture/restricted-dump-query-contract-proposal.md).

**W2 v1 implemented subset**

The product question is intentionally singular: given one host-named, exactly selected non-null object in one
immutable dump, return one exact instance field, optionally replacing an exactly observed null with one bounded
literal.

- Admitted shape: one ordinal, case-sensitive `root.field`, optionally followed by `?? null`, a signed decimal
  `Int32`, or a bounded string literal. W2 closed with a handwritten parser; W6.2 plans its compatibility-preserving
  replacement under the common C# expression-front-end contract.
- Root evidence: typed `ExactObject`, `ExhaustiveAbsence`, `Partial`, `Unavailable`, `Conflict`, and `Invalid` states.
  Only an exact non-null object can produce a plan; missing or non-exact evidence is never treated as null.
- Pipeline: parse, admit/classify, prepare/bind, freeze an immutable object-specific plan, then evaluate that plan.
  Preparation selects the outer field exactly once; evaluation reads through the selected descriptor without
  rebinding or reparsing it.
- Field domain: direct `Int32`; exact/null `Nullable<Int32>` with `Int32` or `null` coalescing; and exact/null/partial
  `String` with string or `null` coalescing. A fallback is selected only for exact null.
- Identity and explanation: canonical versioned request, root-selection policy, and plan projections; complete nullable
  child layout in descriptor/plan identity; SHA-256 plan identity in successful provenance; stable diagnostic stages;
  complete result axes; and only bounds whose guarded operation was reached.
- Admission: duplicate/overlapping/out-of-extent nullable layouts, extent overflow, foreign snapshots, and forged
  same-snapshot owner address/method-table descriptors fail before value memory is read.
- Truth: every product-level result is a read-only `DerivedQuery`; its underlying counted field reads remain
  independently available `Observation` results. Neither mode implies historical or counterfactual execution.
- Replay: a versioned 22-case corpus over 20 distinct expression texts compares the complete canonical result byte
  sequence/fingerprint for all cases and the canonical plan projection string/fingerprint for the 13 cases whose
  preparation succeeds after repeated same-session evaluation and after the dump is reopened, rediscovered, and
  rebound. It also asserts exact axes,
  diagnostics, context, path bounds, ordered provenance payload, and value-read geometry.

The implementation and full corpus are locally headless-verified. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs at
exact W2 closure commit `5bed47100`.

**Later Phase 1 candidates (not W2 v1)**

* Names: `this`, locals/args (when available), statics
* Member access: fields, auto-properties (as data, not code)
* Indexers for arrays and common collection patterns (where structure is inspectable)
* Operators: `?.`, `??`, `?:`, `==`, `!=`, `<`, `>`, `&&`, `||`, `!`
* Simple method-like intrinsics implemented as evaluator primitives:

  * string formatting basics (`Length`, substring-like primitives)
  * numeric conversions, enum formatting
* `typeof(T)`, `default(T)`, literal values
* Bounded pretty-printing of common BCL types

**W2 v1 admission and plan boundary**

* Admit only the closed one-hop shape with deterministic expression, identifier, and decoded-string caps. At W2
  closure the handwritten parser enforced that boundary; the implemented W6.2 front end parses complete bounded C#
  and then projects only the versioned W2 shape.
* Bind only the supplied typed root and dump runtime/metadata evidence; do not discover roots or load assemblies.
* Select one exact instance-field descriptor during preparation and lower it to an immutable read-only plan rather
  than compiling a synthetic method.
* Preserve the exact snapshot, owner, selected field, decoder, optional literal, reached bounds, and canonical plan
  identity through evaluation and replay.
* Reject method/getter execution, backing-field inference, construction, reflection, unsupported syntax and types,
  foreign identity, and unavailable/partial/conflicting/invalid evidence with stable diagnostics.

**Not admitted in W2 v1**

* Null-conditional access, member chains, exact-null roots, and frame/local/argument/static discovery.
* Properties/getters, including auto-property backing-field inference; calls; constructors; indexers; and arrays.
* Reflection, implicit assembly loading, conversions, arithmetic, comparisons, Boolean operators, assignments,
  statements, `typeof`, `default`, and general pretty-printing/intrinsics.
* `await`, async execution, tasks “running,” LINQ, iterators, recursion, and every interpreter execution entry point.

**Why MVP is valuable**

* A single typed, replayable root-field query validates the product's parse/bind/evidence/explanation seam without
  pretending a dump is a live C# execution context.
* Complete parsing does not imply complete binding or evaluation. The closed type and admitted-tree boundary gives
  later scenario-driven increments an evidence-backed compatibility baseline.

---

### Phase 2 — Branchless counterfactual method evaluation (W4 closed)

Goal: implement the admitted
[`Counterfactual Method Evaluation Contract`](../architecture/counterfactual-method-evaluation-contract-proposal.md)
for one generated-dump getter/helper chain. Results in this phase are **counterfactual execution**, not historical
replay and not evidence of why the original process reached its captured state.

**Implemented prerequisite evidence, not a product capability**

Exact strengthened implementation checkpoint `19c292f9f` proves a deliberately closed W3 execution seam:

* Structural module, type, MethodDef, and FieldDef identities are projected through SRM without leaking reader handles.
* Method body, signature, return shape, and locals form one immutable resolution result; activation derives frame
  shape from it, and typed whole-body admission finishes before instruction zero.
* E1 executes branchless, EH-free exact-`Int32` arithmetic. E2 executes one directly declared same-module
  `Int32` instance field getter, either directly or with exactly one integer constant followed by one unchecked
  `add`, `sub`, or `mul`, through the injected persistent-memory capability. Its receiver must use the exact compact
  `ldarg.0` encoding.
* The real-dump fixture derives the method and `ldfld` correlation from counted dump metadata/body evidence, imports
  only an exact four-byte field observation, and reproduces the prepared transcript after dump reopen/rebind. The disk
  PE remains an independent late oracle, not resolver input.
* CoreCLR differential tests cover the admitted arithmetic/getter/null outcomes. A null receiver becomes a latched
  terminal target exception rather than a resumable partial state.

The cumulative hand-written implementation range from `e7b6a4ace` is `+8,842/-1,650` LOC
(`+5,362/-928` production and `+3,480/-722` tests/fixtures), plus 39 generated lock-file lines. Local headless
verification at the strengthened checkpoint passed a zero-warning 15-project Release build, 103 milestone-selected
unit tests, 67 fast integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3
lane, and both documentation guards, all with zero skips. The primary checkpoint `12b6ef942` passed all four jobs in
[GitHub Actions run
29372661656](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29372661656). [Run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at exact
strengthened checkpoint `19c292f9f`. [Run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
milestone-selected architecture-proof scope.
The W2 query grammar continues to reject every method/getter execution entry point. W4 is a separate request, plan,
and result contract; it does not silently widen W2 syntax or reinterpret a `DerivedQuery` as method execution.

**Implemented W4.2 dump-free unknown kernel, not a product capability**

Exact implementation commit `e89e43498` adds the first provenance-aware continuation through the existing machine:

* `IValuePrecisionDomain<TValue>` optionally classifies values as `Exact`, `ExplainedUnknown`, or
  `UnexplainedUnknown` without widening the minimum `IValueDomain<TValue>` contract. `UnknownExecutionPolicy` defaults
  to `ExactOnly`; the opt-in `ExplainedInt32` policy admits only structurally typed, domain-validated explained
  `Int32` unknowns. Bare semantic top remains non-executable.
* `ProvenanceConcreteDomain` and `ProvenanceConcreteValue` retain the lifted-flat concrete semantics while carrying a
  separate explanation root. Equality, hashing, order, join, meet, and widening remain semantic and do not depend on
  lineage identity.
* Canonical `InputOrigin` and ordered `BinaryTransform` nodes form a versioned, content-addressed SHA-256 DAG. Exact
  integer operands are embedded in transform nodes; unknown operands reference predecessor IDs. Reachable-only
  `CaptureLineage` and validated fresh-domain `ReplayLineage` preserve node bytes, IDs, root, and graph fingerprint.
* Existing argument/local load, local store, arithmetic, and return handlers are reused. Exact receivers,
  metadata-initialized locals, and the W3 exact `ldfld` path stay exact, so the default policy preserves W3 behavior.

The W4.2 implementation checkpoint is 3,454 LOC: 3,429 LOC for the W4.2 implementation plus a 25-LOC scope
correction. Together with W4.1, 3,932 LOC of W4 are realized. These figures measure the checkpoint rather than a
forecast and may be revised as later slices expose better boundaries.

At the W4.2 checkpoint, non-exact `ldfld` evidence still stopped and `FieldLoadTransform` did not exist. W4.3 closes
that specific dump-free machine/domain gap. W4.4 subsequently closes direct-call metadata and graph preparation;
W4.5 closes exact/explained interpreted transfer; and W4.6 closes structural pure-model selection, modeled-return
lineage, frozen-capability execution/attempts, and compiler conformance. The counterfactual request/plan/result and
rooted facade and the generated-dump product result were subsequently delivered by W4.8–W4.9.

**Implemented W4.3 dump-free field continuation, not a product capability**

Exact implementation commit `7479b1ad4` admits one conservative continuation for the already frozen ordinary-instance
`Int32` `ldfld` shape:

* Versioned `FieldLoadEvidence` retains the dependency ordinal, complete frozen field, partial/unavailable status,
  canonical reason, source and imported-object SHA-256 identities, target address, four-byte request geometry, and a
  defensive copy of the observed prefix. `MemoryLoadResult.FromFieldEvidence` is the only structured producer seam.
* Continuation requires structured evidence, the explicit `ExplainedInt32` policy, and the optional
  `IFieldLoadApproximationDomain<TValue>` capability. Exact loads remain exact; code-only partial/unavailable results
  or missing policy/capability stop, conflict stays blocked, and invalid or mismatched evidence stays invalid.
* A successful approximation preserves persistent memory, consumes one instruction, pushes a structurally validated
  explained `Int32` unknown, and emits `InstructionExecuted` followed by `ValuePrecisionLost` carrying the evidence.
* Provenance adds one imported-field `InputOrigin` plus append-only `FieldLoadTransform = 3`. The transform identity
  contains the imported-receiver digest, complete frozen field identity, and its sole origin predecessor; it excludes
  raw target address, display strings, and process-local reference numbers. Replay validates canonical bytes,
  identities, ordering, uniqueness, reachability, acyclicity, and field/origin relationships before mutation while
  preserving every W4.2 golden identity under schema version 1.

W4.3 contributes 3,096 realized LOC: 1,100 production LOC and 1,996 test LOC. W4.1–W4.3 therefore realize 7,028 LOC.
With 12,200–18,700 LOC estimated for W4.4–W4.9, the current projection is 19,228–25,728 LOC; the original
16,860–25,310 LOC umbrella baseline remains recorded in the normative contract. Headless verification at
`7479b1ad4` passed locked restore, the strict fifteen-project Release build with zero warnings/errors, focused W4.3
55/55, complete unit 211/211, fast integration 71/71, ordinary-dump regression 5/5, optimized-dump regression 1/1,
both repository guards, and zero skips, with only the milestone-selected tests.

This evidence is intentionally dump-free. The current ClrMD execution descriptor still imports only exact E2 field
values; no W4 partial-field dump producer, counterfactual product request/result/facade, call execution, or
generated-dump W4 result exists yet.

**Implemented W4.4 direct-MethodDef graph preparation, not call execution**

W4.4 is split into two pushed implementation checkpoints so each slice stays below the 3,500-LOC ceiling:

* W4.4a checkpoint `2e596c117` adds content-equal body-independent `MethodCallSignatureShape` and exact managed-IL
  `ResolvedMethodCallTarget`. Contextual resolution accepts only a non-nil same-module direct MethodDef and proves
  ordinary managed IL before acquiring any body, RVA, local signature, or locals. MemberRef/MethodSpec substitution,
  virtual/indirect dispatch, cross-module targets, generics, varargs, and non-`static Int32(Int32,Int32)` helpers remain
  outside the admitted shape.
* W4.4b checkpoint `742ef2c4f` adds `MethodGraphPlanner.Prepare` and immutable graph/node/call-site projections. It
  loads and types every reachable definition, correlates call descriptors with definitions, represents a shared
  MethodDef once, rejects self/mutual cycles, and returns either canonical nodes/fields/edges plus required logical
  depth and internal usage or no plan. Identity/signature disagreement remains `Conflict`.
* The fixed 64-method and 1,024 distinct-method/field/call-site ceilings bound internal construction. They are not the
  configurable product traversal budget, and `RequiredLogicalDepth` is a frozen fact rather than an enforced request
  limit at this checkpoint.
* The exact `GetMarkerSummary` fixture freezes two nodes, two fields, one edge at caller IL offset 12, required depth
  two, and five internal units. The legacy `IlMachine` still uses call-free admission and rejects before the call.

W4.4 realizes 3,651 added LOC: 2,076 production plus 1,575 tests, split into 1,043-LOC W4.4a and 2,608-LOC W4.4b.
Cumulative W4 realization through W4.4 is 10,679 LOC. With 10,500–16,100 LOC then estimated for W4.5–W4.9, its
checkpoint projection was 21,179–26,779 LOC; the original 16,860–25,310 baseline remains preserved. Headless verification passed locked restore,
the strict fifteen-project Release build at zero warnings/errors, planner 35/35, W4 fixture 6/6, complete unit 250/250,
fast 73/73, ordinary-dump regression 5/5, optimized-dump regression 1/1, both guards, and zero skips. Every behavioral
command used the milestone test selection.

This is a frozen admission artifact, not counterfactual execution. W4.4 adds no frame push/return, configured depth
enforcement, call transform, model, product result, or dump-grounded W4 path.

**Implemented W4.5a exact prepared-graph execution, not product closure**

Exact implementation commit `356c07037` consumes the W4.4 graph without re-resolving metadata:

* `ActivatePreparedGraph` validates the complete frozen graph and configured maximum logical call depth before any
  interpreted frame exists, then binds those facts to the session atomically.
* `DirectCallSiteIdentity` and `FrameReturnSite` preserve the exact caller, call offset, callee, and caller resume
  offset. Exact direct `call` pushes one frame and exact `ret` unwinds it through that frozen return site.
* Each call and return consumes one instruction and leaves memory unchanged. The instruction event precedes the
  corresponding frame-entered or frame-exited event, and admission/capability/depth/invariant/budget failures create no
  partial transition.
* `MachineOperationalState` retains the configured and required logical depth plus observed-logical and active-frame
  high-water facts. The exact integration fixture reaches depth 2/2, executes 10 instructions and two field loads,
  matches the CoreCLR oracle, and proves that execution performs no metadata re-resolution.

W4.5a realizes 3,334 added LOC: 1,590 production plus 1,744 tests. W4.1–W4.5a therefore realize 14,013 LOC. W4.5b
remains estimated at 1,800–2,700 LOC, projecting combined W4.5 at 5,134–6,034 LOC and full W4 at 24,013–29,313 LOC;
the original 16,860–25,310 baseline and earlier checkpoint estimates remain preserved. Headless verification passed
locked restore, the strict fifteen-project Release solution build and strict unit/integration project builds at 0
warnings/0 errors, focused prepared-graph tests 25/25, the W4 fixture 7/7, complete unit 275/275, fast integration 74/74,
ordinary-dump regression 5/5, optimized-dump regression 1/1, the Markdown-link guard over 62 files/41 destinations,
and the headless-workflow guard over one workflow,
with zero skips and the milestone test selection on every behavioral filter. An independent audit found no remaining
production findings after the checkpoint fixes.

This checkpoint proves exact call execution only. Explained-unknown call/return lineage still reports
`EXEC_CALL_LINEAGE_UNAVAILABLE`; call models, product request/plan/result and facade, dump integration, and hosted
closure were the remaining later-slice work at that checkpoint.

**Implemented W4.5b explained-unknown prepared-call lineage, not product closure**

Exact implementation commit `c72f6ee9e5545240433294cdca4f350808339aef` completes the interpreted-call kernel:

* Optional `IInterpretedCallLineageDomain<TValue>` transforms the complete metadata-ordered two-argument vector before
  either argument is published and transforms the returned explained unknown before caller mutation. Exact values pass
  unchanged.
* Canonical `CallArgumentTransform` nodes retain the complete direct call site, parameter index, and predecessor;
  `InterpretedReturnTransform` retains the call site and predecessor. They append kinds 4 and 5 to schema version 1,
  preserving all prior canonical bytes and IDs across 29 frozen legacy identity cases.
* The machine probes the capability only after ordinary graph, stack, type, budget, and depth validation. Absence is
  blocked as `EXEC_CALL_LINEAGE_UNAVAILABLE`, capability failure is blocked as `EXEC_DOMAIN_FAILURE`, and malformed,
  foreign, non-executable, or semantically changed output is invalid as `EXEC_CALL_LINEAGE_INVALID`. Every failure is
  atomic across state, memory, budget, events, frames, and published lineage.
* Capture retains only the reachable lineage DAG. Replay validates canonical bytes/hashes, order, reachability,
  dependencies, types, call sites, parameter indices, and acyclicity before mutation, enabling deterministic
  continuation in the same or a fresh session.

The mixed partial/exact compiler graph has five nodes: origin, field transform, parameter-zero call transform, binary
transform with the exact operand embedded, and return transform. The partial/unavailable graph has eight: two origins,
two field transforms, two parameter-indexed call transforms, binary transform, and return transform. Both execute
10 instructions, perform two field loads, leave memory unchanged, record depth high-water 2/2, avoid metadata
re-resolution, and replay in the same or a fresh session.

W4.5b realizes 2,804 added LOC: 766 production plus 2,038 tests. Combined W4.5 realizes 6,138 LOC and W4.1–W4.5
therefore realize 16,817 LOC. The historical W4.5b estimate was 1,800–2,700 LOC and combined W4.5 projection was
5,134–6,034 LOC; each upper bound was exceeded by 104 LOC. The W4.5-closure full-W4 projection was
25,017–29,417 LOC. Headless verification at the exact commit passed locked restore; the strict single-node
fifteen-project Release build at 0 warnings/0 errors; prepared-graph 40/40; combined lineage/audit 76/76; compiler
lineage 2/2; W4 integration 9/9; complete unit 297/297; fast integration 76/76; ordinary dump 5/5; optimized dump 1/1;
both guards; and zero skips, with only the milestone-selected behavioral tests. An independent audit found no
production or test findings.

A subsequent W4.6 design audit split the former 2,300–3,400 LOC model estimate into W4.6a structural
registry/opaque modeled-leaf/effect-and-fallback admission at 1,800–2,600 LOC and the then-unified W4.6b typed
execution/attempts/modeled-lineage/conformance at 2,700–3,500 LOC, or 4,500–6,100 LOC combined. These are preserved
as historical planning facts.

**Implemented W4.6a structural pure-model admission, not model execution**

Exact implementation commit `77c92789b16d9258c907d5026a36e39f8c957b41` freezes the narrow scenario model
boundary:

* Bounded `PureCallModelIdentity`/`PureCallModelVersion`, exact structural descriptors, non-generic two-`Int32`
  invocation/outcome vocabulary, bounded `W4.Model.*` stable codes, and registry selection carry no memory, dump,
  session, ambient context, target delegate, display text, or lineage.
* The default graph planner remains interpreted-only. Explicit `RequirePureModel` resolves and types the direct edge,
  then selects the required target before acquiring its body. Only matching `Exact` confidence plus `None` effects is
  admitted; missing, throwing, invalid, mismatched, non-exact, or unsupported-effect selection cannot fall back to the
  target body or expose a partial plan.
* A successful edge points to one deduplicated body-free `FrozenPureModelLeaf`; runtime capability identity is excluded
  from structural equality/hashing and legacy interpreted call-site hashes stay frozen. The compiler graph is one
  interpreted root, one modeled leaf, two fields, one edge, five traversal units, and required depth two.
* Prepared activation of a graph with modeled leaves returns `EXEC_MODEL_EXECUTION_UNAVAILABLE` before checking depth
  or arguments and before creating state or consulting the resolver/model. No model outcome, attempt, transfer, or
  modeled lineage is produced.

The deterministic PDB-free target PE SHA-256 is
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`. Exact-checkpoint headless evidence passed
locked restore; the strict fifteen-project Release build at zero warnings/errors; unit 371/371; fast 77/77; ordinary
dump 5/5; optimized dump 1/1; pure-model contracts 49/49; model planner 25/25; legacy planner 35/35; SRM compiler 1/1;
lineage 2/2; both guards; and zero skips within the milestone test selection. Independent audits found no behavioral finding.
W4.6a realizes 2,959 added LOC (1,210 production plus 1,749 tests/fixture support), 359 above the historical upper
estimate, and brings W4.1–W4.6a to 19,776 LOC.

**Implemented W4.6b modeled-return lineage/domain, still not model execution**

Exact implementation commit `fd723a912` adds optional `IPureCallModelLineageDomain<TValue>` and append-only schema-v1
kind-6 `ModeledReturnTransform`. Exact operands are embedded in the modeled relation; explained operands receive
unchanged parameter-indexed kind-4 call nodes; and the complete acyclic batch is validated/interned atomically.
Structural capture/replay and fresh-domain continuation are tested while kind-1–5 bytes and IDs remain frozen. Strict
headless builds passed at zero warnings/errors; focused modeled lineage passed 8/8; combined legacy-plus-modeled
lineage passed 44/44; and the standard single-node integration build plus W4 call-lineage cases passed 2/2, with zero
skips and the milestone test selection. W4.6b realizes 1,003 added LOC (481 production plus 522 tests), with 23 deletions,
bringing W4.1–W4.6b to 20,779 LOC.

**Implemented W4.6c frozen-capability execution and audit witnesses**

Exact implementation commit `877c9fb55` dispatches only the model capability retained by the frozen leaf. It does not
re-query the registry/resolver, reselect a descriptor, acquire the modeled body, or fall back. Exact or lineage-
grounded unknown outcomes transfer atomically into the caller with one instruction event, unchanged memory, and no
model frame/event. Budget rejection precedes entry. Every actual entry records immutable callsite/model/depth/outcome/
transfer/code evidence; invocation/completion counters, independent logical/active depth witnesses, exact terminal
depth retention, stable failure taxonomy, and chronology validation cover failed as well as successful attempts.
W4.6c realizes 2,734 added LOC (1,425 production plus 1,309 tests); strict affected builds passed at zero warnings/
errors and focused conformance passed 34/34.

**Implemented W4.6d compiler/SRM conformance**

Exact implementation commit `da5346813` adds 956 test LOC. It directly proves interpreted/model/CoreCLR exact
agreement and interpreted/model agreement for both partial/unavailable shapes. The target PE SHA-256 remains
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; the mixed case freezes graph hash
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`, while repeated and fresh
metadata-reader/domain/machine sessions reproduce the both-unknown graph hash
`31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`. Focused W4.6d passed 3/3 and
aggregate W4 integration 13/13. Full exact-code-checkpoint closure passed locked restore, strict fifteen-project build
at zero warnings/errors, unit 413/413, Fast 80/80, ordinary dump 5/5, and optimized dump 1/1.
Every behavioral invocation used the headless wrapper and the milestone test selection, with zero skips. W4.6 totals
7,652 LOC and cumulative W4 realization is 24,469 LOC.

Historical full-W4 projections remain original 16,860–25,310; post-W4.2 18,532–26,132; post-W4.3
19,228–25,728; post-W4.4 21,179–26,779; post-W4.5a 24,013–29,313; W4.5 closure 25,017–29,417; post-design-audit
27,217–32,117; W4.6a checkpoint 28,376–32,476; first W4.6b recalibration 28,876–33,276; post-split
28,826–33,726; post-W4.6b checkpoint 28,879–33,279; and pre-W4.6c/d closure 30,079–33,729 LOC. W4.6c/d
realized 3,690 LOC against their historical 3,400–3,750 estimate. W4.7a/b realize 2,448/353 LOC, 2,801 total,
bringing W4 through W4.7 to 27,270 LOC. Its former 2,200–3,150 estimate and 31,069–34,319 projection are historical.
W4.8a–f realize 11,924 LOC; W4.9a–c realize 2,698 LOC; cumulative W4 realizes 41,892 LOC excluding documentation and
generated lockfile changes. The later 31,670–33,970 projection is retained as historical calibration.

The product runner now returns the exact `0x26AF37BD` summary from exact dump evidence and a completed typed unknown
with canonical field/call/return or modeled-return lineage from partial or unavailable `Marker` evidence. It reports
independent semantic, completion, completeness, evidence, effect, value, accounting, assumption, model, provenance,
and diagnostic axes. The modeled plan proves `CombineMarkers` remains body-free. Both ClrMD sessions are disposed
before execution, and all six memory/request/plan/result artifacts replay byte-for-byte after reopening and rebinding
the dump. Disk metadata and CoreCLR are consulted only afterward as independent comparisons.

**Implemented scenario (rooted exact/degraded interpreted/model product execution; hosted closure complete)**

The generated `DumpProbe` fixture asks: “Under the named evaluation policy and the captured marker evidence, what
would branchless `GetMarkerSummary` compute through its direct `CombineMarkers` helper?” This is the smallest selected
question that W2 cannot answer. W2 can select and decode one field; it cannot combine two field observations, execute
the getter IL, or enter the helper call.

* With exact method bodies, owner identity, and both marker fields, W4 must return the exact `Int32` summary and the
  test oracle must agree with direct CoreCLR execution.
* If either required field is partial or unavailable, the admitted arithmetic and call transfers continue with a
  typed unknown. The result retains stable field and transformation lineage and never turns missing evidence into
  zero, the remaining marker, or another concrete guess.
* Every host-facing answer is `CounterfactualExecution` under an explicit policy and assumption set. It states what
  the admitted IL computes from captured or explicitly unknown evidence; it never states that the target historically
  invoked `GetMarkerSummary` or `CombineMarkers`.
* The helper is the only admitted direct-call shape. Unsupported call shapes block with a typed outcome; the active
  contract does not inherit the broad model catalog from supporting research documents.

**Guardrails**

* Deterministic instruction and preparation-traversal units are consumed. Maximum logical call depth is proven before
  activation and logical/frame depth high-water marks are reported. Cooperative cancellation remains a separate
  host-responsiveness outcome.
* Allocation is not admitted. Its budget is absent/not applied until a separately admitted scenario consumes it;
  carrying a dormant counter would make a false guarantee.
* The complete branchless root/helper definition and typed dependency graph is now frozen before instruction zero.
  W4.5 consumes that plan for exact and explained-unknown call transfer, depth enforcement, and stable argument/return
  provenance. W4.6a freezes the one exact/no-effect structural model alternative, and W4.6b freezes its modeled-return
  lineage relation; W4.6c makes invocation, attempt, transfer, depth, and budget/event outcomes equally stable; and
  W4.6d proves compiler/SRM exact, degraded, and fresh-session conformance.
* The retained exact typed-null `ldfld` outcome stops counterfactual execution and receives a standalone canonical
  result projection through dump-free conformance. It carries no fabricated rooted request or plan identity because
  the closed W4 product request requires an exact non-null root. The non-throwing helper/model may not fabricate a
  target exception. Interpreted handler search/transfer is not part of this slice, and EH-bearing bodies remain
  rejected before execution.
* Closure required exact, degraded-evidence, differential, budget, and same/fresh-session canonical replay coverage,
  followed by the required milestone-selected headless Release, fast, dump, and focused W4 gates with zero skips at the
  exact pushed commit. W4.2–W4.6 supply the dump-free domain, machine, preparation, interpreted-call, and pure-model
  prerequisites; W4.8–W4.9 supply the complete product and generated-dump closure evidence. Hosted run 29463426083
  passed all required jobs at exact implementation-closure commit `a819a08fd9ccdf926620c505732475990b242be9`;
  run 29463847230 passed them again at final documentation-closure commit
  `aaec73c5b987089addb539d3628de67bd815bd8f`.

**Deferred beyond the admitted slice**

Branches and path forks, CFG merge/fixpoint/widening, loops, handler-transfer EH, virtual or generic dispatch,
constructors and virtual allocations, broad BCL/pure-method catalogs, collection traversal, PDB-backed frame context,
async/dynamic lifting, and virtual stepping retain their separate research gates. They are not implied by the W4
contract or by W3 scaffolding.

---


### Phase 2.5 — Async + dynamic semantic lifting

**Roadmap relation:** Research backlog. Entry requires validated method execution, calls, generics, the scenario-derived `MoveNext` opcode closure, and required EH behavior.

Goal: preserve debugger clarity for modern C# language features without executing runtime internals.

**Supported**

* Async state-machine interpretation backed by virtual task semantics (deterministic scheduler, explicit await/continuation diagnostics).
* Dynamic call-site lifting for common C# `dynamic` invocation shapes with transparent overload-resolution outcomes.
* Host-visible decision points when target selection cannot be resolved uniquely.

**Guardrails**

* No implicit execution of real thread-pool/runtime scheduling internals.
* Meta-object/COM dynamic paths default to explicit `DecisionNeeded`/`Partial` outcomes unless policy enables heuristic handling.
* Every async/dynamic transition emits a traceable diagnostic event to preserve explainability.

---

### Phase 3 — Virtual scratchpad objects and “what-if” exploration

**Roadmap relation:** Research backlog.

Goal: make the evaluator a real exploration tool, not only read-only.

**Supported**

* `new` for virtual objects (constructed in an isolated virtual heap)
* Virtual delegates/closures (compile small lambdas into an executable representation)
* Pure method calls on virtual objects
* Mixed evaluation: virtual objects can reference dump objects read-only

**Key UX rule**

* Virtual objects are clearly labeled as **virtual**, with lifetime scoped to the session (or a named scratchpad).
* No implication that virtual state exists in the original process.

---

### Phase 4 — Advanced query workflows and helpers

**Roadmap relation:** Research backlog.

Goal: bridge the gap to “Rider-grade data exploration.”

**Potential additions**

* Query helpers: `.Where(...)`, `.Select(...)`, `.GroupBy(...)` with strict limits
* Result visualization: tables, histograms, grouping by key
* Common “debugger helpers”: `dump(obj)`, `path(obj)`, `refs(obj)`, etc.
* Saved queries per dump type / per solution

---

## 8) Non-Goals (Explicit)

To avoid a “build a whole CLR” trap, we explicitly do **not** aim to:

* Execute arbitrary .NET code with full runtime fidelity (GC, JIT, thread scheduling)
* Support native code execution, P/Invoke, COM, pointer dereferences
* Perform network/file/process operations during evaluation
* Reconstruct missing locals/values when the dump or optimizations don’t contain them
* Guarantee identical behavior to the original process for all code paths (we prioritize bounded, useful, deterministic evaluation)

---

## 9) Constraints and Reality Checks (User-facing behavior)

### Dump variability

Users will open dumps that vary in what they contain. The feature must:

* degrade gracefully when locals/args aren’t available
* still work for heap-only investigations
* communicate “this context can’t supply locals” without being noisy

### Optimized / Release builds

The evaluator must tolerate:

* inlined frames, optimized-out locals, ambiguous stack values
* partial symbol information

We will emphasize a stable contract:

* “If we can’t ground it in snapshot data, we won’t pretend.”

---

## 10) Success Metrics

We’ll measure success through a mix of usability, reliability, and coverage:

### Effectiveness

* % of evaluations that succeed without fallback
* Median time-to-first-useful-answer in dump sessions (proxy via diagnostic output events)
* Reduction in manual object-walking actions per answer

### Reliability & limits

* Crash-free evaluation rate
* Deterministic budget-exhaustion and host-cancellation rates, tracked separately
* Instances of blocked side-effect attempts (signals rule clarity and user needs)

### Adoption

* Frequency of Watch/Immediate usage in dump sessions
* Repeat usage across sessions (saved watches/queries)

---

## 11) UX Details Worth Calling Out

### “Purity” / result UI

Every evaluated expression exposes the result axes from §6. A compact UI might render `Derived query · complete · exact evidence · read-only` or `Counterfactual execution · partial · modeled effects`; it should not call both cases merely “Pure.” Budget exhaustion, cancellation, unavailable evidence, and policy blocking remain distinct outcomes.

### Cancelability

All evaluation is cancelable and doesn’t block the rest of the UI.

### Discoverability

* “Try it” affordances: Add-to-watch buttons, suggested expressions (e.g., for exceptions)
* Autocomplete shows what’s in scope (locals/types/members), with warnings on potentially blocked calls

---

## 12) Risks and Mitigations (Product-level)

1. **Users expect full live-debugger parity**

   * Mitigation: phased rollout, clear badges, great error messages, explicit non-goals.

2. **Incorrect results due to partial context / optimizations**

   * Mitigation: conservative evaluation; prefer “Unavailable” over guesswork; show provenance.

3. **Performance pitfalls (huge graphs, LINQ on millions of objects)**

   * Mitigation: budgets, truncation, summarized outputs, explicit “expand more” UX.

4. **Input-shape caveat**

   * Current evidence covers only the named generated fixtures and explicitly admitted input shapes. Other artifact
     shapes require a separate scenario, result contract, bounded operations, and executable evidence.

---

## 13) Milestones (Proposed)

The detailed W0–W4 record is in `docs/plans/future-work-planning.md`; completed W5 sequencing is in
`docs/plans/post-w4-path-forward.md`; active W6 sequencing is in `docs/plans/post-w5-path-forward.md`:

* **W0:** truthful baseline, CI, and deterministic smoke evidence.
* **W1:** real dump-memory field/string read with typed evidence outcomes.
* **W2:** restricted query v1 with typed snapshot roots, immutable object/field plans, exact
  `String`/`Int32`/`Nullable<Int32>` behavior, and complete-corpus canonical replay; complete for its
  milestone-selected scope at exact closure commit `5bed47100`.
* **W3:** closed scenario-derived concrete E1/E2 IL slice plus CoreCLR and real-dump getter evidence implemented at
  strengthened checkpoint `19c292f9f`; formally closed for its defined milestone-selected architecture-proof scope at exact
  documentation commit `de6cea124` after [GitHub Actions run
  29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs.
* **W4:** closed branchless generated-dump `GetMarkerSummary`/`CombineMarkers` scenario under the normative
  counterfactual-method contract. W4.1's exact fixture/CoreCLR/current-W3-boundary gate is implemented at `82363585b`,
  W4.2's dump-free explained-unknown arithmetic and canonical lineage kernel is implemented at `e89e43498`, and
  W4.3's structured non-exact `ldfld` continuation plus `FieldLoadTransform` is implemented at `7479b1ad4`, W4.4's
  direct-MethodDef resolution plus complete immutable graph preparation is implemented at `2e596c117`/`742ef2c4f`,
  W4.5a's exact prepared-graph call execution is implemented at `356c07037`, and W4.5b's canonical explained-unknown
  argument/return lineage is implemented at `c72f6ee9e`, W4.6a's exact/no-effect body-free pure-model selection is
  implemented at `77c92789b`, W4.6b's atomic modeled-return lineage is implemented at `fd723a912`, W4.6c's frozen-
  capability execution/attempt/depth contract is implemented at `877c9fb55`, and W4.6d's compiler/SRM conformance is
  implemented at `da5346813`, and standalone target-outcome projection/replay at `2e70fe76d`/`dad6a6dd4`. W4.8's
  canonical rooted facade/runner lands through `44b050ec8`, and W4.9's detached ClrMD generated-dump corpus lands
  through `a8b5f32f0`. Exact W4 implementation-closure commit
  `a819a08fd9ccdf926620c505732475990b242be9` passed all four jobs in [hosted run
  29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083); final documentation-closure
  commit `aaec73c5b987089addb539d3628de67bd815bd8f` passed them again in [run
  29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230).
  Branches, broader
  calls/opcodes, generics, allocation, PDB-backed context, and whole-method abstract analysis remain gated.
* **W5:** closed product-composition and usefulness milestone under the
  [Post-W4 Path Forward](../../plans/post-w4-path-forward.md). It admits only unchanged W2 expressions plus
  `root.GetMarkerSummary()`, owns the current W4 acquisition/binding path, preserves semantic modes in one evaluator
  facade, adds a headless reference consumer, and requires meaningful predeclared incident-question evidence before
  another prototype capability is selected. W5.1–W5.5b are implemented through pushed checkpoint `90ade6d92`:
  generated fresh-process replay validates the facade and raw-count runner, while twelve independent designed dumps
  across request-pipeline and batch-pipeline roots supply the decision corpus. Their recurring blocker ranking selects
  `AdmitFixedDepthMemberChain` as the next prototype slice. Both corpora contribute zero representative/external-
  observation rows, so no field-readiness rate is claimed and external evidence may later reverse the direction. The
  owner closed W5 under an explicit milestone-only exception after hosted jobs were rejected before executing; those
  jobs remain absent evidence and the exception does not carry into W6.
* **W6:** active design and delivery sequence under the
  [Post-W5 Path Forward](../../plans/post-w5-path-forward.md). It adds only an explicit
  `FixedDepthMemberChainV1` profile: one root reference field, one direct terminal field or exactly certified trivial
  field-backed data property, null-aware access, and existing W2 terminal value decoding. The companion
  [C# Expression Front-End and Subset-Admission Contract](../architecture/csharp-expression-front-end-contract-proposal.md)
  replaces parser growth with one pinned Roslyn parse and ordered W2/W5/W6 tree recognizers; valid C# outside those
  profiles remains unsupported. W6 begins with emitted-shape proof and has no implementation or validation claim yet.
  Its closure requires a headless generated corpus,
  twenty-four independent synthetic dumps across four structural shapes, deterministic same/fresh/reopen replay, and
  either a threshold-qualified unique successor or explicit deferral.

Virtual scratch objects, advanced queries, async/dynamic lifting, and virtual stepping remain research rather than implied follow-on milestones.

---

## 14) Open Questions (for later product increments)

The W2 v1 admitted shape, typed-root model, value domain, plan identity, and replay gate are closed decisions. Roslyn
will own complete bounded expression parsing, but the following binding/evaluation questions stay with later Phase 1
increments or their research phase and do not reopen W2.

1. **Default execution stance**

   * Should method execution be opt-in per session/dump, or enabled with strict limits by default?

2. **Next restricted syntax increment**

   * Which concrete incident question justifies the next member, root, null-handling, literal, or operator form, and
     what evidence/type/diagnostic/replay rules must accompany it? Statements remain research backlog.

3. **Team workflows**

   * Do we want “shareable watch sets” or query scripts that can be attached to an incident report?
