# Design Doc: Post-Mortem Expression Evaluator for .NET Dumps

> **Roadmap relation:** Active for the read-only dump-evidence and restricted-query slices only. Method execution, virtual scratch objects, async/dynamic lifting, and advanced query workflows are research backlog gated by executable evidence; they are not current delivery commitments.

## 1) Summary

When debugging a crash dump, engineers frequently need answers that are “one computed step away” from what the raw heap shows:

* “What’s `request.User.Email` if I follow null checks and wrappers?”
* “Which items in this list match predicate X?”
* “What’s the effective configuration value after overrides?”
* “What’s inside this `Task` / `ValueTask` / `Lazy<T>` / `AsyncLocal<T>`?”

Today, post-mortem workflows force users into manual object-walking and mental evaluation. A live debugger solves this with expression evaluation, but a dump has no running runtime to execute code. The active feature is a **deterministic, policy-constrained, read-only evaluator** for a restricted C# expression subset over dump evidence. Later research may add counterfactual method execution and an isolated virtual heap, but those capabilities are not implied by the first product slice.

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

* semantic mode (`Observation`, `DerivedQuery`, and only in later phases `CounterfactualExecution` or `AbstractAnalysis`)
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

The implementation and full corpus are locally headless-verified. Exact-final-HEAD hosted evidence remains pending,
so W2 does not yet have a hosted closure claim.

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

### Phase 2 — Safe method/property evaluation (research gate)

Goal: investigate getters/helpers that would compute values from snapshot-derived or assumed state. Results in this phase are **counterfactual execution**, not historical replay and not evidence of why the original process reached its captured state.

**Supported**

* Evaluate property getters and methods that are **provably safe** under our rules:

  * no I/O, no time, no threading, no P/Invoke, no unsafe pointers
  * bounded loops (either proven or runtime-limited)
* Limited subset of BCL “pure” methods (string operations, numeric ops, simple LINQ on in-memory sequences with limits)

**Guardrails**

* Deterministic instruction, call-depth, allocation, and traversal budgets are mandatory; cooperative cancellation is a separate host-responsiveness mechanism.
* Clear “blocked due to side-effect risk” diagnostics
* Option to “Show evaluation plan” at a high level (e.g., “Calls A → B → C; blocked at D (File IO)”)
* Initial admission may be limited to EH-free bodies. The first exception behavior is stop-on-throw; interpreted handler transfer is a later prerequisite for `catch`/`finally` claims.

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
  `String`/`Int32`/`Nullable<Int32>` behavior, and complete-corpus canonical replay; implementation and local
  headless verification are complete, while exact-final-HEAD hosted evidence remains pending.
* **W3:** scenario-derived concrete IL slice plus CoreCLR differential oracle.
* **W4:** decomposed, evidence-gated unknown-aware counterfactual method evaluation.

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
