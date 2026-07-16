# Research Backlog: Other Potential Applications

> **Roadmap relation: speculative research, not a delivery commitment.** The active product target is the read-only dump evaluator. No multi-application reuse claim is considered validated until at least two domains execute the same meaningful opcode set and a second real consumer demonstrates reuse.

This note records applications and architectural implications that may become relevant if the domain-parametric semantics hypothesis is validated.

The hypothesis is that a single, well-specified IL semantics could eventually support concrete, hybrid, and abstract modes and be reused beyond post-mortem debugging. The current prototype does not yet demonstrate that claim.

---

## 1) Hypothesized reusable core

The proposed interpreter **spine** looks like this:

* **IL semantics engine** (ECMA-335 stack machine) parameterized by:

  * `IValueDomain<TValue>` (constants/nullness/type-set/origin labels/etc.)
  * `IMemoryModel<TValue, TMem>` (virtual heap, dump overlay, summary heap, etc.)
* **Call dispatcher** (`ICallDispatcher`) that can:

  * interpret IL bodies (if allowed)
  * or run **models** (intrinsics, environment, execution policies)
  * or return unknown + havoc
* **Execution strategies**:

  * concrete (single path)
  * hybrid (unknown propagation, optional fork)
  * abstract interpretation (CFG + fixpoint with join/widen)
* **Explainability hooks** (value provenance, effect summary, “why unknown”)
* Optional “debugger runtime model” pieces (async, dynamic, etc.) implemented as *semantic intrinsics* rather than stepping into framework internals.

If these seams survive implementation in more than one mode and consumer, they may support multiple products. Today they are design hypotheses, and most corresponding projects contain no behavior.

---

## 2) Application: Static analysis and program reasoning

### 2.1 Use cases

This is the most “obvious” sibling of dump debugging because you already designed abstract interpretation:

* **Nullness + definite assignment** at IL level (useful for analyzing generated code, source generators, obfuscated code, etc.)
* **Origin-flow analysis** (“which external source influenced this value?”)
* **Effect/purity inference** (“getter does IO”, “method allocates”, “method reads ambient context”)
* **Exception analysis** (possible throw sites, uncaught exception paths)
* **Threading/async hazards** (ambient context usage; blocking calls in async)
* **Trimming/AOT friendliness checks** (reflection patterns, dynamic code paths)
* **API misuse** checks that are awkward in Roslyn syntax (because IL reveals post-rewrite truth)

### 2.2 What the framework needs for this (extensions/adjustments)

Your core already has CFG + fixpoint + join/widen, but serious static analysis benefits from a few upgrades:

#### A) An analysis-friendly IR (optional, but huge)

Interpreting raw IL for fixpoint works, but you’ll quickly want a normalized internal IR:

* canonicalize stack into explicit temporaries (“stack SSA”)
* normalize `dup`, `stloc/ldloc`, and short forms
* represent calls and throws as explicit nodes

This makes:

* joins well-defined (“stack height mismatch” becomes structural)
* transfer functions easier to reason about
* interprocedural summaries easier to compute

**Adjustment:** add a `MethodNormalizer` phase producing `IRGraph` (blocks + typed temps) while preserving a mapping back to IL offsets.

#### B) Exception-aware CFG

Most analyses are useless without at least conservative EH edges:

* `throw` edges
* “call may throw” edges into catch/finally

**Adjustment:** promote EH handling from “debugger nice-to-have” to “analysis mode must-have”, with selectable precision:

* `EH.Ignore` (fast)
* `EH.Conservative` (sound-ish)
* `EH.Modeled` (uses call models to mark “non-throwing”)

#### C) Interprocedural summaries + recursion handling

If you want whole-program effects/origin labels/purity, you need summaries:

* return value abstraction
* memory effects (which regions become unknown/origin-labeled)
* thrown exceptions set (optional)
* environment effects (IO/native/reflection/threading)

**Adjustment:** formalize a `Summary` format and a `SummaryProvider` contract. The core engine already emits `EffectSummary` and supports `Havoc`; you “just” need to cache and compose them.

#### D) Domain composition

Your MVP domain (const + nullness + type-set + origin labels) is a great baseline. Static analysis will want:

* numeric ranges
* “definitely initialized”
* simple string facets (empty/non-empty, maybe constant prefix)
* points-to sets (for aliasing)

**Adjustment:** make domain composition first-class:

* either product domains (`(Nullness × origin labels × Range × …)`)
* or “fact plugins” with stable join/widen rules

**Net result:** no fundamental redesign; mostly add a normalizing IR + EH CFG + summary plumbing.

---

## 3) Application: Predictive/speculative features in *live* debugging

Think of this as “shadow execution” that augments a normal debugger, without actually running user code in the target.

### 3.1 Use cases

* **Bounded expression evaluation**: “what would this evaluate to?” without func-eval side effects
* **Preview Step Over**: show which locals/fields are likely to change if you Step Over
* **Branch exploration**: “what if condition is true/false?” using forking + join
* **Side-effect detection**: warn if expression would touch IO/threading/native/reflection
* **Async/dynamic explanation**: show resolved overload/awaiter path without stepping runtime machinery

### 3.2 Key difference vs dump debugging

Instead of ClrMD reading from a dump snapshot, you read from a **live snapshot** of the target process.

You need an abstraction like:

```csharp
interface ISnapshotProvider
{
    // Create a stable read-only view of memory + registers for a thread at a point in time.
    LiveSnapshot Capture(ThreadId tid, SnapshotOptions opts);
}
```

The interpreter then runs on:

* heap reads via the snapshot
* writes to a virtual overlay (never to the real process)

### 3.3 Extensions/adjustments needed

#### A) Snapshot consistency + races

With a dump you assume the world is frozen. With a live process:

* other threads may mutate objects while you read
* object graphs may be transient/inconsistent

**Adjustment:** your `ISnapshotProvider` needs policy:

* “stop-the-world” snapshot (debugger suspends all threads briefly)
* or “best-effort” snapshot with inconsistency detection and unknown propagation

Your existing “CanWalkHeap” style checks map well to this.

#### B) “Predictive stepping” as a first-class strategy

You already have:

* single-path execution
* forking on unknown conditions

For predictive debugging you’ll want a strategy that:

* runs ahead to the next sequence point
* tracks diffs (locals + overlay writes)
* returns “likely changes” plus confidence

**Adjustment:** make `StepDiff` and “write events” part of the public interpreter protocol (not just UI sugar).

#### C) Handling ambient sources (time/random/env) correctly

In a live session, it’s tempting to call host `DateTime.Now`. Don’t.
You need:

* “target-derived” env/time when possible
* otherwise stable unknowns

This is the same “SessionSnapshot” concept you already wanted for dumps.

#### D) Interop with existing debugger protocols

If you integrate with DAP/ICorDebug, you’ll want:

* mapping from debugger stack frames to interpreter seed values
* symbol resolution integration
* consistent presentation of mixed “real” vs “virtual” state

**Net result:** the interpreter design stays the same; you add a new **memory backend** (live snapshot) and a new execution strategy (“run-ahead + diff”).

---

## 4) Application: Executing IL on platforms without JIT (AOT-only / no-JIT runtime)

This is where your framework morphs from “debugger VM” into a “runtime VM”.

### 4.1 Use cases

* Run IL on iOS / restricted platforms (no JIT)
* Game scripting / plugin execution (load arbitrary IL and run it)
* Deterministic execution environments (replay, simulation)
* “Debug build” interpreter fallback (e.g., interpret rarely used code paths)

### 4.2 What changes (this is the biggest delta)

Dump debugging tolerates “unknown”. Runtime execution does not.
You need a **fully concrete** domain and a **real runtime substrate**:

#### A) Concrete execution domain

* exact integer/float semantics
* full exception behavior
* full generics instantiation behavior (or a chosen subset)
* object layout consistency

This is doable, but it is a different ambition level than “evaluate within declared bounds.”

#### B) A “runtime library model” rather than “debug models”

Instead of modeling `DateTime.Now` as Unknown, you must define what it means:

* do you allow time?
* do you provide deterministic time?
* do you forbid it?

Same for:

* IO/network
* threading
* reflection
* native interop

In other words: you need a **policy-based host ABI**.

#### C) Threading and async

For a general runtime, eventually you need:

* real async scheduling semantics (or a cooperative scheduler)
* synchronization primitives
* maybe tasks

But you could ship a single-threaded runtime first (common for no-JIT environments).

### 4.3 Extensions/adjustments needed

#### A) Host ABI + intrinsics become a formal layer

Your current call-model registry becomes the runtime’s “system call interface”:

```csharp
interface IHostServices
{
    // allocation, type loading, strings, arrays, exceptions
    // optional: time/env/io, gated by policy
}
```

The runtime interpreter calls host services for:

* allocating objects/arrays/strings
* reflection
* interop
* GC (or a simplified managed heap)

#### B) Verification / constraints

For arbitrary IL you need:

* verification-like checks (or strict subset acceptance)
* quotas (CPU steps, memory)
* rejection of low-level opcodes / unverifiable constructs

Your existing budget system helps, but you’ll want:

* instruction filters
* strict stack typing enforcement
* restricted reflection, etc.

#### C) Performance considerations become central

A debugging interpreter can be slow; a runtime interpreter must be optimized:

* threaded interpreter / direct dispatch
* cached decoded IL
* value representations specialized for primitives
* possibly tiered “interpret then AOT compile” (future)

**Net result:** you can reuse the semantics engine, but you’ll need:

* a concrete domain + real heap implementation
* a host ABI
* stronger verification and performance engineering

(There’s overlap with existing OSS interpreters like ILRuntime/dotnow in spirit, but your unique differentiator would be the *same* engine supporting both abstract/unknown and concrete modes.)

---

## 5) Application: Deterministic replay, time-travel, and execution tracing

Even without a live target, an interpreter is excellent for “explain what happened” tooling.

### 5.1 Use cases

* record a “virtual execution trace” from an expression/method call
* show how a value was computed (dataflow provenance)
* enable time-travel stepping (Step Back) cheaply because state is virtual
* generate minimal repro steps (in a controlled bounded runtime)

### 5.2 Framework implications

You already planned:

* persistent memory overlays (for undo)
* event emission (“write happened”, “unknown minted”, “call entered/exited”)

For replay/tracing, formalize:

* **Trace format**:

  * instruction-level events (optional)
  * statement-level events (preferred)
  * value provenance graph

* **Determinism controls**:

  * stable unknown IDs
  * stable ordering of continuation scheduling
  * stable tie-breaking for ambiguous dispatch

No fundamental changes—just elevate tracing/determinism from “debug feature” to a public API.

---

## 6) Application: Differential testing, fuzzing, and decompiler/AOT validation

An IL interpreter is a fantastic oracle for “does this IL behave like the CLR?”

### 6.1 Use cases

* Differential execution:

  * run a method in CLR and in interpreter with random inputs
  * compare results/exceptions/side effects
* Fuzzing:

  * generate IL graphs, ensure interpreter doesn’t crash, check invariants
* Validate decompiler mappings:

  * correlate interpreter stepping with decompiler-generated sequence points
* Validate AOT toolchains:

  * compare interpreter outcome vs AOT output on supported subset

### 6.2 Framework implications

You’ll want a testing harness layer:

* “execute method with input corpus” API
* deterministic host services (time/random)
* systematic coverage data (which opcodes executed, which models triggered)

No redesign, but you’ll add instrumentation hooks and a “runner” API.

---

## 7) Application: Build-time partial evaluation and “semantic extraction”

This is the “use it as a controlled evaluator” angle.

### 7.1 Use cases

* Evaluate **attribute constructors** and arguments within declared bounds (to extract configuration, analyzers, trimming hints)
* Evaluate **source generator helpers** or embedded DSLs in IL form
* Infer “constant-ish” results (e.g., computed strings) without running full program
* Derive “effect summaries” (IO/reflection/native) for build gating

### 7.2 Framework implications

This mostly wants the **hybrid mode**:

* execute as far as possible
* substitute unknowns for environment reads
* return a partially known value with constraints/provenance

You might add:

* a “purity policy” preset (reject `ReadEnv`, `Native`, `Threading`, etc.)
* a “deterministic host” preset (stable env/time/random)
* a method summary export format (for build tooling)

Again, no redesign; more “productization” of existing concepts.

---

## 8) Cross-cutting adjustments that pay off across all applications

These are candidate investments only after an implemented scenario demonstrates a repeated need. They should not be pulled into the active dump-evaluator roadmap merely because several speculative applications could use them.

### 8.1 Make semantic modeling a first-class extension system

Everything you’ve discussed—dynamic lifting, async tasks, concurrent collections projections, environment modeling—fits best if you formalize:

* **Call models** (intrinsics)
* **Pattern rewriters** (IL → semantic IR)
* **Type projections** (heap object views)

A clean plugin model prevents the core VM from becoming a pile of ad-hoc `if (method == ...)`.

### 8.2 Treat “effects” as a primary output of execution

Effects aren’t just for bounded execution. They power:

* static analysis (“does this method read environment?”)
* predictive debugging (“this expression might do IO”)
* runtime policy enforcement (bounded runtime)
* trace explanation (“unknown because external call”)

So keep `EffectSummary` in the state and in summaries.

### 8.3 Determinism as a configurable contract

Different applications want different determinism:

* dump debugging: deterministic by definition
* live speculative: deterministic within a snapshot
* runtime: maybe deterministic or maybe “real env” depending on policy
* fuzzing: needs deterministic by seed

Build determinism controls into the host services and unknown-minting.

### 8.4 Add an analysis-friendly IR (even if the debugger executes IL directly)

If you only ever execute decoded IL, you’ll fight stack-height joins and duplicated patterns forever. A normalized IR helps:

* analysis
* decompiler mapping
* stepping boundaries
* pattern recognition (foreach/lock/async/dynamic)

You can still keep a low-level IL stepper; the IR can be optional.

---

## 9) “Do we need to change the previous design?” — quick verdict

### No: the big architectural idea still holds

The core bet—**domain + memory model parametrization** with **call/pattern modeling**—is what could make multi-application reuse possible. It remains unvalidated until shared semantics run in multiple modes and a second consumer exists.

### If platform expansion is funded, a few refinements become non-optional

If you want the framework to serve *all* these applications well, I’d explicitly add:

1. **MachineState with call stack + events** (already identified for stepping)
2. **EH-aware CFG and execution** (analysis needs it; stepping benefits)
3. **Normalized IR option** (analysis + pattern lifting + decompiler mapping)
4. **Host ABI abstraction** (needed for no-JIT runtime mode and deterministic policies)
5. **First-class effects + provenance + determinism controls** (cross-cutting)

Everything else could remain as plug-in packages (async models, dynamic models, collection projections, etc.). None of these refinements or packages is an active commitment under the current scope lock.
