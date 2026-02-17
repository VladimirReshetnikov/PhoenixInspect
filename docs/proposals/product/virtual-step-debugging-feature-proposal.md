Below is a high-level feature proposal for **virtual step-by-step debugging** in **post-mortem dump analysis**, built on top of the **IL interpretation framework** we outlined (unknown propagation, sandboxed call modeling, virtual heap overlay, source mapping). It’s “high-level” in the sense of user experience + scope + constraints, but it stays technically grounded so it’s plausibly implementable.

---

# Feature Proposal: Virtual Step Debugging for Post-Mortem Dumps

## 1) Summary

Add an interactive **stepper** to dump debugging that can “execute” code *virtually* (no live process) starting from a **user-provided expression or method call**, allowing:

* **Step Into / Step Over / Step Out**
* View **locals**, **arguments**, **evaluation stack**, **virtual call stack**
* Step through **real source** when available (PDB + source), otherwise through **decompiled source**
* Safely handle missing data and side effects by propagating **unknown values** and/or using **models**
* Support **Undo Last Step** (reverse stepping), enabled by the purely virtual, side-effect-free execution model

The goal is not to recreate an exact runtime; it’s to provide a **debugger-like exploration workflow** over a dump snapshot, with explicit trust boundaries.

---

## 2) Why this is valuable (what users can’t do today)

In post-mortem analysis, users frequently want answers that require “just a bit of execution”:

* Follow computed properties without manually walking the heap
* Understand why some value became null / empty / default
* See how a cache key is built or which branch was taken
* Evaluate a predicate over a collection without exporting data
* Explore state machine logic (async/iterator) from a dump

A normal debugger solves this by executing code in the debuggee. A dump cannot. This feature fills that gap by providing a **virtual debugger** that runs on a **snapshot + sandbox**.

---

## 3) User-facing concept: “Virtual Execution Session”

A new concept appears in the UI:

### Virtual Execution Session

A session is a deterministic, bounded “execution trace” produced by the interpreter.

* Input:

  * A dump + selected context (thread/frame optional)
  * A user expression or method call (e.g., `obj.Compute(x)`)
* Output:

  * A **virtual call stack** and **virtual state** evolving step-by-step
  * Debugger-like stepping operations
  * Optional trace timeline/history, including undo/backtracking

### Big UX rule

This is **not stepping the original process**. It is stepping **a simulation** that reads from the dump and writes only to a **virtual overlay**.

We should make this obvious with subtle but constant cues:

* “Virtual” badge on the call stack, locals window, and session tab
* Provenance indicators (snapshot vs virtual values, modeled vs executed)

---

## 4) Entry points: how users start stepping

### A) From an expression (Immediate/Watch)

User writes:

```csharp
myObj.Foo(a, b).Bar
```

Then clicks:

* **Run** (evaluate once), or
* **Debug (Virtual)** (start stepping)

### B) From an object in the dump

On a heap object, context menu:

* **Debug method…**

  * pick a method/property
  * provide arguments (constants or other dump objects)
  * start stepping

### C) From a stack frame

From a real dump stack frame, user picks:

* a local/this + method call
* or “Debug expression in this frame”

This is especially useful for:

* state machine `MoveNext` frames
* exception handling frames

---

## 5) What “step” means in a virtual world

### Execution model: interpreter frames vs dump frames

* The dump provides an initial environment: `this`, locals, args (if available), and heap objects.
* After starting, the virtual debugger creates its own call stack of **interpreted frames**.
* Stepping operates on interpreted frames only; dump frames are just the “starting context.”

### Step Into

**Definition (user-facing):**

* Enter the next call at the current execution point, if it’s eligible to be interpreted.
* If the call is not eligible, show what happened via a model summary.

**Expected behavior:**

* If the next operation is a call to a “safe / interpretable” method:

  * Step Into enters it, showing its source (real or decompiled).
* If the call is:

  * external / native / reflection-heavy / unknown side effects
  * or otherwise disallowed by policy
  * or missing IL body
    then Step Into does not “teleport into nothing.”
    Instead it:
  * “steps into” a **model frame**: a single pseudo-frame showing an effect summary:

    * “Returns unknown string (tainted: Env_Socket)”
    * “Havoc: buffer contents now unknown”
    * “May throw: IOException”
  * then returns to the caller on next step

This makes stepping predictable: *there is always somewhere to go*.

### Step Over

**Definition:**
Advance to the next statement (or source line) in the current frame, treating calls as atomic.

**Important nuance:**
“Atomic” doesn’t mean “ignored.” It means:

* execute the call using:

  * interpretation if allowed, but without entering the callee UI, OR
  * a model if interpretation is disallowed
* update the virtual state accordingly
* stop at the next source boundary

This is critical because a user often wants “run through the boring helper” but still see how it changes locals.

### Step Out

**Definition:**
Run until the current interpreted frame returns (or throws), then stop in the caller after the call site.

In a virtual world, Step Out should also handle:

* returning **unknown values**
* propagating modeled effects (taint, havoc regions, maybe-throw)
* unwinding via exceptions (see below)

---

## 6) Source-level stepping: real source vs decompiled source

### Preferred: real source stepping

When PDB + source (or SourceLink retrieval) is available, stepping uses:

* sequence points (IL offset ↔ source file/line)
* local names/scopes
* async method mapping to user methods via state-machine metadata/PDB tables (required for async stepping quality)

The experience should resemble normal stepping:

* highlight current line
* show locals/args by their real names
* step boundaries follow sequence points

### Fallback: decompiled stepping

When you don’t have source:

* show a **decompiled view** of the current method body
* highlight decompiled statements corresponding to the current IL offset
* step boundaries still follow a “statement map” generated by the decompiler (best effort)

This must be honest:

* show an indicator “Decompiled”
* show confidence level for mapping (“exact”, “approximate”, “IL-level”)
* allow switching to a raw IL view when mapping is ambiguous

### When mapping is impossible

Some IL won’t map cleanly:

* optimized builds
* missing PDB
* compiler-generated transformations

In that case:

* stepping falls back to **IL instruction stepping**
* but we should still make it tolerable: group IL into blocks and show “virtual instruction pointer” at block granularity by default

---

## 7) Handling unknown data and side effects while stepping

This is where your interpreter framework becomes a product feature, not just a correctness hack.

### Unknowns as first-class values in the UI

When a value becomes unknown, we show:

* type shape (e.g., `string`, `int`, `T : IDisposable`)
* nullness (MaybeNull/NonNull)
* taint/effect tags (Env_Socket, Native, MissingData, UnsupportedIL)
* “why this became unknown” trace (clickable)

That turns “we can’t run this” into “we can still reason about this.”

### Branching on unknown conditions

When the current condition is unknown, the stepper can do one of three user-visible behaviors (configurable, with a sane default):

1. **Default (conservative): fork, then join**

   * Explore both branches internally but present the joined state.
   * Good for stability; less good for understanding “what happens if true.”

2. **Interactive fork (recommended for debugging UX)**

   * Pause and offer:

     * “Assume condition is true (create branch)”
     * “Assume condition is false (create branch)”
     * “Explore both (two tabs)”
   * This is where a virtual debugger becomes *better than a real one*: you can explore alternate realities.

3. **Pick one path (fast mode)**

   * Useful when the user wants “just keep going”
   * Mark decisions as assumptions in the trace

---

## 8) Exceptions in virtual stepping

Virtual execution should treat exceptions explicitly:

* If an operation may throw (based on models or interpreter semantics), the session can:

  * fork “normal” and “throw” states (interactive or conservative)
* If a throw path is taken:

  * the virtual call stack unwinds
  * you stop at the catch handler (if interpreted code has one)
  * otherwise you stop at the boundary where the exception leaves the user’s initiated call

UI should show:

* virtual exception object (may be unknown or dump-backed)
* throw site in source/decompiled view
* unwind trace

---

## 9) Undo Last Step: reverse stepping in a virtual world

This is one of the rare cases where post-mortem + interpretation can beat a live debugger.

### Why undo is feasible

* The dump heap is read-only.
* All mutations happen in a **virtual overlay** (virtual heap + locals/stack).
* Interpreter stepping is deterministic (given the same assumptions about unknown branching).
* Therefore, stepping forms a sequence:

  ```
  State0 -> State1 -> State2 -> ...
  ```

  with no external side effects.

### What “Undo” means

Undo should revert:

* instruction pointer (IL offset / source position)
* evaluation stack
* locals/args/this binding
* virtual heap allocations and mutations
* the current branch/assumption context

### UI/UX for undo

* Add **Step Back** (undo one step)
* Add **Rewind** slider / history list (optional)
* If unknown-branch choices exist, undo returns you to the decision point so you can choose a different assumption

### Where undo becomes tricky (but still solvable at the feature level)

* If you allow interactive forking, the history becomes a **tree**, not a list.

  * Undo should move within the current branch.
  * A “Branch chooser” UI can show alternate paths and let the user switch.

### Product constraints

Undo should be bounded:

* store history up to N steps or memory budget
* allow “checkpointing” every K steps for fast rewind
* evict old steps with clear UX (“history truncated”)

Even without perfect efficiency, “Undo last step” is extremely high value and worth shipping early with conservative limits.

---

## 10) Safety and guardrails (must-have)

A virtual stepper is powerful, and it must stay safe and predictable:

* Hard budgets:

  * max steps per operation (prevents infinite loops)
  * max allocations / virtual heap size
  * max recursion depth / call depth
* Cancellation always works (never block the UI)
* Clear policy boundaries:

  * disallowed calls don’t “fail”; they get modeled/havoc’ed
  * the stepper always shows when it used a model vs interpreted IL

---

## 11) Scope proposal (phased)

### Phase 1: Minimal virtual stepping (statement-level)

* Start from expression/method call
* Step Over / Into / Out
* Source view:

  * real source if available
  * otherwise decompiled view
* Unknown propagation visible in locals/watch
* Models for obvious “world” calls (socket/file/native) return unknown + effect tags
* **Undo last step** supported with a bounded linear history

### Phase 2: Branch exploration + better decompiled mapping

* Interactive branch choices on unknown conditions
* Branch history UI
* Better source mapping for decompiled statements (and easy IL fallback)
* “Run to cursor” within the interpreted method

### Phase 3: Time-travel + richer state visualization

* Step Back across larger traces (checkpoints)
* Call tree / trace timeline
* Saved “sessions” attached to dumps for sharing

---

## 12) Success criteria

You’ll know this feature is working when:

* Users can answer “why is this value X?” in minutes instead of manual heap spelunking
* Most sessions proceed without hard stops (“unsupported” becomes “modeled unknown”)
* Undo is used frequently (strong signal of value)
* Users trust the tool because it’s explicit about:

  * which values came from the dump
  * which were virtual
  * which were assumptions/models

---

## 13) Key risks (and how we frame them)

1. **Users expect live-debugger fidelity**

   * We frame this as “virtual execution over a snapshot.”
   * We provide provenance and trust badges everywhere.

2. **Optimized builds make source stepping messy**

   * We degrade to IL-level stepping and keep it usable.
   * Decompiler view is explicitly marked approximate.

3. **Explosions due to unknown branching**

   * Default conservative join; interactive fork is opt-in.
   * Strong budgets and UI prompts when complexity grows.

---

### A small but important “bonus”: Why this is uniquely good for dumps

A live debugger can’t safely offer “explore both branches” or “step back” without heavyweight time travel infrastructure. A virtual interpreter can. That’s not just a gimmick; it’s an honest advantage of this approach.
---


## 14) Design follow-ons from virtual tasks + dynamic call lifting

The architecture now includes dedicated proposals for async virtual-task semantics and dynamic call-site lifting. This feature spec should treat them as first-class stepping behavior rather than optional extras.

### Async stepping implications

* `await` suspension should produce stable stop/trace events (`AwaitPending`, then `ContinuationResumed`) that are replay-deterministic.
* Step views should project compiler-generated `MoveNext` execution back to user methods/frames whenever mapping metadata exists.
* Task completion states (`TaskCompleted`, `TaskFaulted`, `TaskCanceled`) must surface in session timeline diagnostics.

### Dynamic dispatch implications

* Dynamic call-sites should resolve as explicit semantic operations with outcomes (`Resolved`, `Ambiguous`, `Unresolved`, `MetaObjectRequired`).
* `Step Into` at unresolved multi-target sites may return `DecisionNeeded` so users can choose a target path.
* UI should display chosen overload and runtime binding types when resolution succeeds to maintain trust.

### Cross-cutting UX rule

If async or dynamic behavior cannot be deterministically resolved, the tool should prefer transparent partial results over silent fallback.

---
