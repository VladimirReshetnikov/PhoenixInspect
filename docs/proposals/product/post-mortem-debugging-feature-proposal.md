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

Evaluation runs in a specific **context**:

* Selected dump
* Selected thread and stack frame (when available)
* `this`, locals, and arguments (to the extent recoverable)
* Imported namespaces and type aliases (configurable)

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

The following is the target Phase 1 capability set, admitted incrementally. W2 intentionally begins with a smaller
closed syntax/operator subset: one exact non-null root, one direct field through `.`, and optional literal `??` over
an exact nullable field. Null-conditional access remains outside the admitted grammar until the root value model can
distinguish exact null from unavailable root evidence.

**Supported**

* Names: `this`, locals/args (when available), statics
* Member access: fields, auto-properties (as data, not code)
* Indexers for arrays and common collection patterns (where structure is inspectable)
* Operators: `?.`, `??`, `?:`, `==`, `!=`, `<`, `>`, `&&`, `||`, `!`
* Simple method-like intrinsics implemented as evaluator primitives:

  * string formatting basics (`Length`, substring-like primitives)
  * numeric conversions, enum formatting
* `typeof(T)`, `default(T)`, literal values
* Safe pretty-printing of common BCL types

**Expression front-end boundary**

* Parse a deliberately admitted C# expression subset with deterministic options.
* Bind only against host-provided roots and the dump/metadata universe; do not load assemblies implicitly.
* Lower admitted syntax into a read-only query plan rather than compiling a synthetic method.
* Reject method/getter execution, construction, reflection, unsupported syntax, and unavailable context with stable diagnostics.
* Treat an auto-property as data only when a backing-field projection is explicitly recognized and reported.

**Not supported (MVP)**

* Calling user code (methods/getters with IL bodies)
* `new` object creation
* `await`, async execution, tasks “running”
* Reflection that discovers/loading types beyond what’s already known
* Anything that can loop arbitrarily (LINQ, iterators, recursion)

**Why MVP is valuable**

* Most dump work is navigating + null-safe access + simple computation.
* We can provide “immediate relief” with minimal ambiguity.

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
* **W2:** restricted expression/query slice with no user-IL execution.
* **W3:** scenario-derived concrete IL slice plus CoreCLR differential oracle.
* **W4:** decomposed, evidence-gated unknown-aware counterfactual method evaluation.

Virtual scratch objects, advanced queries, async/dynamic lifting, and virtual stepping remain research rather than implied follow-on milestones.

---

## 14) Open Questions (for product decisions)

Only questions needed by W1/W2 should be decided now; the rest stay with their research phase.

1. **Default safety stance**

   * Should method execution be opt-in per session/dump, or enabled with strict limits by default?

2. **First restricted syntax closure**

   * Which member, null-handling, literal, and operator forms belong in the first W2 fixture corpus? Statements remain research backlog.

3. **Symbol/source retrieval policy**

   * Network access remains off by default. If retrieval is later enabled, what explicit consent, allowlist, cache, and credential policy should the host require?

4. **Team workflows**

   * Do we want “shareable watch sets” or query scripts that can be attached to an incident report?
