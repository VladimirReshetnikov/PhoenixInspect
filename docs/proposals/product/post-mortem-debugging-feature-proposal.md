# Design Doc: Post-Mortem Expression Evaluator for .NET Dumps

> **Roadmap relation:** Active for the read-only dump-evidence and restricted-query slices and for the admitted W4
> branchless counterfactual-method contract. W3 implements the prerequisite interpreter/memory architecture proof.
> W4.1's fixture gate, W4.2's dump-free provenance-aware arithmetic kernel, W4.3's dump-free non-exact field
> continuation, and W4.4's dump-free direct-MethodDef graph preparation are implemented; W4.4 landed at exact
> checkpoints `2e596c117`/`742ef2c4f`. No counterfactual-method
> product request, result, facade, or dump path has landed. Branches, handler transfer, virtual scratch objects,
> async/dynamic lifting, and advanced query workflows remain research backlog rather than inherited commitments.

## 1) Summary

When debugging a crash dump, engineers frequently need answers that are “one computed step away” from what the raw heap shows:

* “What’s `request.User.Email` if I follow null checks and wrappers?”
* “Which items in this list match predicate X?”
* “What’s the effective configuration value after overrides?”
* “What’s inside this `Task` / `ValueTask` / `Lazy<T>` / `AsyncLocal<T>`?”

Today, post-mortem workflows force users into manual object-walking and mental evaluation. A live debugger solves this with expression evaluation, but a dump has no running runtime to execute code. The active feature is a **deterministic, policy-constrained, read-only evaluator** grounded in dump evidence. W2 implements its first restricted C# query surface. The closed W3 getter proof validates architecture below the product boundary. The admitted W4 contract adds one branchless counterfactual method question to the delivery plan. W4.1 freezes that question's exact generated fixture, CoreCLR result, and W3 admission gap. W4.2 implements the dump-free value-precision, explained-unknown arithmetic, and canonical lineage kernel at `e89e43498`; W4.3 extends that same machine with canonical partial/unavailable field continuation at `7479b1ad4`; and W4.4 freezes the exact direct-call dependency graph at `2e596c117`/`742ef2c4f`, still below the product boundary and without executing the call. W4.5–W4.9 must add call transfer, models, the facade/result contract, and generated-dump replay. An isolated virtual heap and broader method workflows remain research.

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
**“Rider/VS Immediate Window, but for dumps, with safety rails and deterministic behavior.”**

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

3. **Safety-first execution model**

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

### Output and trust indicators

Each evaluation returns separate, machine-readable axes:

* semantic mode (`Observation`, `DerivedQuery`, W4 `CounterfactualExecution`, or later-research `AbstractAnalysis`)
* completion status (completed, blocked, budget-exhausted, cancelled, decision-needed, or failed)
* completeness (complete, partial, or none)
* evidence status (exact, partial, unavailable, conflicting, or invalid)
* effects/virtual-write summary
* value + type, provenance, and diagnostics

A host may synthesize a compact trust indicator from those fields, but the badge must not replace them or imply more certainty than the evidence supports.

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

- Grammar: one ordinal, case-sensitive `root.field`, optionally followed by `?? null`, a signed decimal `Int32`, or
  a bounded string literal.
- Root evidence: typed `ExactObject`, `ExhaustiveAbsence`, `Partial`, `Unavailable`, `Conflict`, and `Invalid` states.
  Only an exact non-null object can produce a plan; missing or non-exact evidence is never treated as null.
- Pipeline: parse, prepare/bind, freeze an immutable object-specific plan, then evaluate that plan. Preparation selects
  the outer field exactly once; evaluation reads through the selected descriptor without rebinding it.
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
* Safe pretty-printing of common BCL types

**W2 v1 front-end and plan boundary**

* Parse only the closed one-hop grammar with deterministic expression, identifier, and decoded-string caps.
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
* The closed type and syntax boundary gives later scenario-driven increments a trustworthy compatibility baseline.

---

### Phase 2 — Branchless counterfactual method evaluation (W4.1–W4.4 landed; product execution pending)

Goal: implement the admitted
[`Counterfactual Method Evaluation Contract`](../architecture/counterfactual-method-evaluation-contract-proposal.md)
for one generated-dump getter/helper chain. Results in this phase are **counterfactual execution**, not historical
replay and not evidence of why the original process reached its captured state.

**Implemented prerequisite evidence, not a product capability**

Exact hardened implementation checkpoint `19c292f9f` proves a deliberately closed W3 execution seam:

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
verification at the hardened checkpoint passed a zero-warning 15-project Release build, 103 non-cybersecurity
unit tests, 67 fast integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3
lane, and both documentation guards, all with zero skips. The primary checkpoint `12b6ef942` passed all four jobs in
[GitHub Actions run
29372661656](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29372661656). [Run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at exact
hardened checkpoint `19c292f9f`. [Run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
non-cybersecurity architecture-proof scope.
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
that specific dump-free machine/domain gap. W4.4 subsequently closes direct-call metadata and graph preparation, but
direct-call transfer, call models, the counterfactual request/plan/result, facade, and generated-dump product result
remain W4.5–W4.9 work.

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
both repository guards, and zero skips, with `Scope!=Cybersecurity` on every test command.

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
Cumulative W4 realization is 10,679 LOC. With 10,500–16,100 LOC estimated for W4.5–W4.9, the current projection is
21,179–26,779 LOC; the original 16,860–25,310 baseline remains preserved. Headless verification passed locked restore,
the strict fifteen-project Release build at zero warnings/errors, planner 35/35, W4 fixture 6/6, complete unit 250/250,
fast 73/73, ordinary-dump regression 5/5, optimized-dump regression 1/1, both guards, and zero skips. Every behavioral
command used `Scope!=Cybersecurity`.

This is a frozen admission artifact, not counterfactual execution. W4.4 adds no frame push/return, configured depth
enforcement, call transform, model, product result, or dump-grounded W4 path.

**Admitted scenario (fixture, unknown, field, and graph kernels implemented; counterfactual product not implemented)**

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
  W4.5 must consume that exact plan for call transfer and depth enforcement; later precision loss, call/effect
  decisions, and budget stops must have stable provenance and diagnostics.
* The retained exact typed-null `ldfld` outcome stops counterfactual execution and receives a standalone canonical
  result projection through dump-free conformance. It carries no fabricated rooted request or plan identity because
  the closed W4 product request requires an exact non-null root. The non-throwing helper/model may not fabricate a
  target exception. Interpreted handler search/transfer is not part of this slice, and EH-bearing bodies remain
  rejected before execution.
* Closure requires exact, degraded-evidence, differential, budget, and same/fresh-session canonical replay coverage,
  followed by the required non-cybersecurity headless Release, fast, dump, and focused W4 gates with zero skips at the
  exact pushed commit. W4.2–W4.4 supply dump-free domain, machine, and preparation evidence only; the complete product and
  generated-dump closure results do not exist yet.

**Deferred beyond the admitted slice**

Branches and path forks, CFG merge/fixpoint/widening, loops, handler-transfer EH, virtual or generic dispatch,
constructors and virtual allocations, broad BCL/pure-method catalogs, collection traversal, PDB-backed frame context,
async/dynamic lifting, and virtual stepping retain their separate research gates. They are not implied by the W4
contract or by W3 scaffolding.

---


### Phase 2.5 — Async + dynamic semantic lifting

**Roadmap relation:** Research backlog. Entry requires validated method execution, calls, generics, the scenario-derived `MoveNext` opcode closure, and required EH behavior.

Goal: preserve debugger trust for modern C# language features without executing runtime internals.

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
* Support native code execution, P/Invoke, COM, unsafe pointer dereferences
* Perform network/file/process operations during evaluation
* Reconstruct missing locals/values when the dump or optimizations don’t contain them
* Guarantee identical behavior to the original process for all code paths (we prioritize safe, useful, deterministic evaluation)

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
* Median time-to-first-useful-answer in dump sessions (proxy via telemetry events)
* Reduction in manual object-walking actions per answer

### Reliability & safety

* Crash-free evaluation rate
* Deterministic budget-exhaustion and host-cancellation rates, tracked separately
* Instances of blocked side-effect attempts (signals rule clarity and user needs)

### Adoption

* Frequency of Watch/Immediate usage in dump sessions
* Repeat usage across sessions (saved watches/queries)

---

## 11) UX Details Worth Calling Out

### “Purity” / trust UI

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

4. **Security/privacy concerns (dumps may contain secrets)**

   * Mitigation: treat dump/PE/PDB/source inputs as untrusted; keep network retrieval off by default; bound reads, traversal, parsing, and decompression; never place dump values in telemetry by default; redact diagnostics; require explicit consent for source/symbol egress; and isolate artifact processing before external use. “Interpreter sandbox” policy is not a substitute for a parser/host security boundary.

---

## 13) Milestones (Proposed)

The authoritative sequence is in `docs/plans/future-work-planning.md`:

* **W0:** truthful baseline, CI, and deterministic smoke evidence.
* **W1:** real dump-memory field/string read with typed evidence outcomes.
* **W2:** restricted query v1 with typed snapshot roots, immutable object/field plans, exact
  `String`/`Int32`/`Nullable<Int32>` behavior, and complete-corpus canonical replay; complete for its
  non-cybersecurity scope at exact closure commit `5bed47100`.
* **W3:** closed scenario-derived concrete E1/E2 IL slice plus CoreCLR and real-dump getter evidence implemented at
  hardened checkpoint `19c292f9f`; formally closed for its defined non-cybersecurity architecture-proof scope at exact
  documentation commit `de6cea124` after [GitHub Actions run
  29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs.
* **W4:** active branchless generated-dump `GetMarkerSummary`/`CombineMarkers` scenario under the normative
  counterfactual-method contract. W4.1's exact fixture/CoreCLR/current-W3-boundary gate is implemented at `82363585b`,
  W4.2's dump-free explained-unknown arithmetic and canonical lineage kernel is implemented at `e89e43498`, and
  W4.3's structured non-exact `ldfld` continuation plus `FieldLoadTransform` is implemented at `7479b1ad4`, and W4.4's
  direct-MethodDef resolution plus complete immutable graph preparation is implemented at `2e596c117`/`742ef2c4f`.
  Call execution, models, the facade and product result, and generated-dump closure remain W4.5–W4.9 work. Branches, broader
  calls/opcodes, generics, allocation, PDB-backed context, and whole-method abstract analysis remain gated.

Virtual scratch objects, advanced queries, async/dynamic lifting, and virtual stepping remain research rather than implied follow-on milestones.

---

## 14) Open Questions (for later product increments)

The W2 v1 grammar, typed-root model, value domain, plan identity, and replay gate are closed decisions. The following
questions stay with later Phase 1 increments or their research phase and do not reopen W2.

1. **Default safety stance**

   * Should method execution be opt-in per session/dump, or enabled with strict limits by default?

2. **Next restricted syntax increment**

   * Which concrete incident question justifies the next member, root, null-handling, literal, or operator form, and
     what evidence/type/diagnostic/replay rules must accompany it? Statements remain research backlog.

3. **Symbol/source retrieval policy**

   * Network access remains off by default. If retrieval is later enabled, what explicit consent, allowlist, cache, and credential policy should the host require?

4. **Team workflows**

   * Do we want “shareable watch sets” or query scripts that can be attached to an incident report?
