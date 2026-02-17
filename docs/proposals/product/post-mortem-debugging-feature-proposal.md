# Design Doc: Post-Mortem Expression Evaluator for .NET Dumps

## 1) Summary

When debugging a crash dump, engineers frequently need answers that are “one computed step away” from what the raw heap shows:

* “What’s `request.User.Email` if I follow null checks and wrappers?”
* “Which items in this list match predicate X?”
* “What’s the effective configuration value after overrides?”
* “What’s inside this `Task` / `ValueTask` / `Lazy<T>` / `AsyncLocal<T>`?”

Today, post-mortem workflows force users into manual object-walking and mental evaluation. A live debugger solves this with expression evaluation, but a dump has no running runtime to execute code. This feature provides a **safe, sandboxed C# expression evaluator** that operates against the dump snapshot and (optionally) an isolated “virtual” heap for scratch objects—enabling faster root cause analysis without needing a repro.

---

## 2) Problem Statement

**Dump analysis is high-friction for “computed truth.”**
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
* Treat the dump as immutable truth; evaluation cannot mutate the dump.
* Allow advanced “what-if” exploration via a virtual scratchpad heap, without affecting snapshot data.
* Provide clear trust signals: what was evaluated purely vs what was blocked/approximated.

The end-state experience should feel like:
**“Rider/VS Immediate Window, but for dumps, with safety rails and deterministic behavior.”**

---

## 4) Target Users and Scenarios

### Primary users

* Engineers doing production incident response
* Performance engineers analyzing hangs and memory dumps
* Support/triage engineers doing initial dump investigation

### Core scenarios

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

   * Evaluation should complete quickly or fail clearly with timeouts/limits.
   * No unbounded loops, recursion blowups, or runaway allocations.

3. **Safety-first execution model**

   * No filesystem/network/process/thread/time access.
   * No arbitrary native interop.
   * “Pure” evaluation by default.

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

Each evaluation returns:

* Value + type
* A “purity” indicator (e.g., **Pure**, **Blocked**, **Partial**, **Timed Out**)
* Diagnostics when blocked (what capability was disallowed)

---

## 7) Scope: What We Will Support

This is intentionally phased so we can deliver value early without promising “execute arbitrary .NET code from a dump.”

### Phase 1 — Read-only, high-confidence expressions (MVP)

Goal: cover 70% of common “what’s in here?” questions.

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

### Phase 2 — Safe method/property evaluation (“pure code execution”)

Goal: unlock getters/helpers that compute values from already-present snapshot data.

**Supported**

* Evaluate property getters and methods that are **provably safe** under our rules:

  * no I/O, no time, no threading, no P/Invoke, no unsafe pointers
  * bounded loops (either proven or runtime-limited)
* Limited subset of BCL “pure” methods (string operations, numeric ops, simple LINQ on in-memory sequences with limits)

**Guardrails**

* Hard timeouts per evaluation
* Allocation and instruction-step budgets
* Clear “blocked due to side-effect risk” diagnostics
* Option to “Show evaluation plan” at a high level (e.g., “Calls A → B → C; blocked at D (File IO)”)

---

### Phase 3 — Virtual scratchpad objects and “what-if” exploration

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
* Evaluation timeouts rate (should be low; if high, our defaults are too permissive or UX encourages heavy queries)
* Instances of blocked side-effect attempts (signals rule clarity and user needs)

### Adoption

* Frequency of Watch/Immediate usage in dump sessions
* Repeat usage across sessions (saved watches/queries)

---

## 11) UX Details Worth Calling Out

### “Purity” / trust UI

Every evaluated expression carries a badge:

* **Pure (Read-only)**: derived from snapshot data only
* **Pure (Executed)**: executed under sandbox rules
* **Partial**: truncated due to budgets/limits
* **Blocked**: disallowed API/operation
* **Unavailable**: context missing (locals not present, corrupted heap, etc.)

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

   * Mitigation: evaluation is local-only by default; no implicit external access; any symbol/source retrieval is transparent and configurable.

---

## 13) Milestones (Proposed)

* **M1: MVP read-only evaluator**

  * field/property-as-data, operators, indexers, formatting, watch/immediate integration
* **M2: Safe execution**

  * allow a controlled set of getter/method evaluations + sandbox budgets + trust badges
* **M3: Virtual scratchpad**

  * `new`, virtual delegates, persistent scratch variables, clear virtual labeling
* **M4: Queries and visualization**

  * safe LINQ-like workflows with tables/grouping and strong truncation UX

---

## 14) Open Questions (for product decisions)

1. **Default safety stance**

   * Should method execution be opt-in per session/dump, or enabled with strict limits by default?

2. **How “C#-complete” should syntax be**

   * Expressions only vs allowing statements (`var x = ...; x`) in Immediate.

3. **Symbol/source retrieval policy**

   * Should we auto-download symbols by default, prompt, or require explicit configuration?

4. **Team workflows**

   * Do we want “shareable watch sets” or query scripts that can be attached to an incident report?