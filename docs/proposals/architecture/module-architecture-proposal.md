> **Superseded as a physical package plan (2026-07-13).** The catalog below is retained as a historical logical-decomposition exercise, not as a target solution layout. The active topology and split criteria are defined in `architecture-overview-proposal.md`: logical seams do not receive assemblies until implementation and tests justify them. Empty placeholder projects have been removed.

Below is a **granular logical architecture catalog** for the interpreter framework and its possible ecosystem (metadata, symbols, decompilation, dump/live hosts, analysis, and semantic models). It can inform later splits without committing those splits today:

* post-mortem dump debugging
* speculative/live debugging
* static analysis
* “no JIT” execution hosts / bounded runtimes

The catalog is a vocabulary of possible logical seams. A seam becomes a physical project only when current implementation, dependency direction, and independently useful tests justify that split.

---

## Design principles

1. **Core VM must not depend on** ClrMD, SRM/AsmResolver, ILSpy, Roslyn, symbol servers, or OS-specific code.
   It should be runnable in a pure unit test with a fake method body provider + fake heap.

2. **All “world knowledge” lives in hosts and models**
   Time/env, IO, scheduler, reflection, thread context… are *policies/models*, not VM semantics.

3. **Metadata, symbols, decompilation are optional services**
   The VM only needs a method body + token resolution. Debug maps/source are for stepping UX.

4. **No cycles**: every package should sit in a clear layer.

---

## Layered dependency map

Think of this as “allowed arrows”. Everything points downward.

```
[Products / Tools]
    |
    v
[Hosts: Dump / Live / bounded runtime] -----> [Artifacts: binaries/PDB/source acquisition]
    |                                        |
    |                                        v
    |                              [Metadata+Symbols+Decompiler]
    |                                        |
    v                                        v
[Semantic Models & Pattern Rewriters] ---> [VM Integration Bridges]
    |                                        |
    v                                        v
[Core VM + IR + Tracing] <-------------- [Shared Abstractions]
    |
    v
[Foundations: primitives, diagnostics, immutable utils]
```

The important relationship: **Models depend on the VM abstractions and (optionally) metadata**, but **the VM never depends on models**.

---

## Package catalog (granular)

I’ll group packages by layer, and for each package list:

* Purpose
* Depends on
* Used by

### 0) Foundations (no dependencies except BCL)

#### `Interpreter.Foundation`

**Purpose**

* Basic utility types shared everywhere:

  * `Result<T>`, `Diagnostic`, `Confidence`, `EffectTag`, `Budget`
  * stable hashing, interning helpers
  * lightweight collections (or wrappers around `ImmutableArray` etc.)

**Depends on:** BCL only
**Used by:** everything

---

## 1) Core semantics (VM + IR), metadata-agnostic

#### `Interpreter.Types`

**Purpose**

* Structural type/signature model (no reader attached):

  * `TypeSig` (primitives, arrays, pointers/byrefs, generic vars, instantiated types)
  * `MethodSig`, `FieldSig`
  * `GenericContext` (type args + method args, can contain “unknown/canonical” placeholders)
* These are *pure data*. No token reading inside.

**Depends on:** `Interpreter.Foundation`
**Used by:** VM, models, metadata bridges, analysis

---

#### `Interpreter.IL`

**Purpose**

* ECMA-335 IL decoding and method-body representation:

  * `Instruction` (opcode + operand + offset)
  * `MethodBody` (IL bytes, decoded instructions, maxstack, locals sig handle, EH clauses)
  * minimal IL verification checks (stack transitions sanity, optional)

**Depends on:** `Interpreter.Foundation`, `Interpreter.Types`
**Used by:** VM execution, IR builder, analysis, rewrite pipeline

---

#### `Interpreter.Core.Abstractions`

**Purpose**

* The VM’s public contract:

  * `IValueDomain<TValue>`
  * `IMemoryModel<TValue, TMem>`
  * `ICallDispatcher<TValue,TMem>` / call modeling interfaces
  * `IMethodResolver` (fetch method bodies + resolve tokens *via host*)
  * `IBranchPolicy`, `ICallPolicy`, `IBudgetPolicy`
  * `EffectSummary`, `UnknownOrigin`, `Provenance` hooks

**Depends on:** `Interpreter.Foundation`, `Interpreter.Types`, `Interpreter.IL`
**Used by:** VM engine, domains, hosts, models, tooling

---

#### `Interpreter.Core.Execution`

**Purpose**

* The actual interpreter engine:

  * `MachineState` (call stack, frames, IP, locals, eval stack)
  * `StepOne` (micro-step), `RunUntil`
  * exception propagation model (at least “throw stops”; optionally EH-aware execution)
  * debug events emission (writes, call enter/exit, unknown minted)

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** debugger stepping host, no-JIT runtime host, testing harness

---

#### `Interpreter.Core.IR`

**Purpose**

* Optional normalized IR for analysis and pattern recognition:

  * stack-to-temp lowering
  * CFG builder (basic blocks, edges, EH edges optionally)
  * typed temporaries (stack SSA-ish)
  * mapping IR nodes ↔ IL offsets

**Depends on:** `Interpreter.IL`, `Interpreter.Types`, `Interpreter.Foundation`
**Used by:** analysis, pattern rewriters, advanced stepping maps

---

#### `Interpreter.Core.Analysis`

**Purpose**

* Abstract interpretation framework:

  * fixpoint engine (join/widen/narrow)
  * interprocedural summaries (optional module)
  * analysis runners with strategies (conservative EH, modeled EH)

**Depends on:** `Interpreter.Core.Abstractions`, `Interpreter.Core.IR`
**Used by:** static analyzer products, “predictive debugging” features, effect inference tooling

---

#### `Interpreter.Core.Tracing`

**Purpose**

* Trace and event formats:

  * `ExecutionTrace` stream
  * stable serialization (for “save session”, diagnostics, replay)
  * “explain value” graph extraction helpers

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** debugger UI, replay tooling, fuzz harness, diagnostic output

---

## 2) Domains (value lattices) and memory models

These are pluggable and should be independent of metadata readers.

#### `Interpreter.Domain.Concrete`

**Purpose**

* Concrete values: primitives, exact refs, exact structs
* Used for runtime execution / deterministic evaluation when data is known

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** dump debugging, live speculative, no-JIT runtime mode (initially)

---

#### `Interpreter.Domain.CNTypeOriginLabels` (your MVP domain)

**Purpose**

* Constants + Nullness + Runtime Type-set + origin labels
* plus optional small-struct field sensitivity

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** dump debugging, static analysis baseline, speculative debugging

---

#### `Interpreter.Domain.Range` (optional)

**Purpose**

* Numeric intervals and simple predicate refinement
* Works as a product domain with CNTypeOriginLabels

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** analysis, predictive stepping

---

#### `Interpreter.Memory.VirtualHeap`

**Purpose**

* A pure interpreter-owned heap:

  * object allocation, arrays, strings (virtual)
  * byref addressables to locals/fields/array elements
  * supports persistence (structural sharing) for Undo/replay

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** all hosts as the writable layer

---

#### `Interpreter.Memory.Overlay`

**Purpose**

* Copy-on-write overlay that can sit atop a read-only backend:

  * read: overlay first, fallback backend
  * write: overlay only
* supports diffs and undo cheaply

**Depends on:** `Interpreter.Core.Abstractions`, `Interpreter.Memory.VirtualHeap`
**Used by:** dump host, live snapshot host, “no-JIT runtime”

---

#### `Interpreter.Memory.SummaryHeap` (analysis-focused)

**Purpose**

* Points-to sets, weak updates, summary regions, havoc regions

**Depends on:** `Interpreter.Core.Abstractions`
**Used by:** `Interpreter.Core.Analysis`

---

## 3) Semantic models and pattern rewriters (“lift intent”)

These packages depend on the core abstractions, and optionally on metadata abstractions (not concrete readers).

### 3.1 Modeling interfaces (base)

#### `Interpreter.Models.Abstractions`

**Purpose**

* A uniform plugin interface for:

  * call models (intrinsics)
  * pattern rewrites (IL/IR → semantic ops)
  * heap projections (“view ConcurrentDictionary”)
* plus `ModelInfo`/`RewriteInfo` with provenance/effects/confidence

**Depends on:** `Interpreter.Core.Abstractions`, `Interpreter.Core.IR` (optional), `Interpreter.Foundation`
**Used by:** all model packages, hosts

---

### 3.2 Core BCL models

#### `Interpreter.Models.CoreLib.Primitives`

**Purpose**

* High-ROI pure models:

  * `string` ops, `Nullable<T>`, boxing/unboxing helpers
  * small math intrinsics
* bounded formatting helpers (debugger view)

**Depends on:** `Interpreter.Models.Abstractions`
**Used by:** dump/live/runtime

---

#### `Interpreter.Models.Environment`

**Purpose**

* Environment/time/random modeling:

  * `DateTime.Now/UtcNow`, `Environment.*`, `Guid.NewGuid`, `Random`
* operates against a `SessionSnapshot` service supplied by host

**Depends on:** `Interpreter.Models.Abstractions`
**Used by:** dump/live/runtime (policy-driven)

---

### 3.3 Big “intent lifts”

#### `Interpreter.Models.Async`

**Purpose**

* Async/await semantic runtime:

  * recognize async kickoff/state machine patterns
  * virtual Task/ValueTask objects + virtual scheduler
  * intercept builder/awaiter calls
* Exposes a “virtual continuation” interface used by stepping and replay

**Depends on:** `Interpreter.Models.Abstractions`, `Interpreter.Memory.VirtualHeap`
**Optionally depends on:** `Interpreter.Metadata.Abstractions` (for `AsyncStateMachineAttribute` / PDB mapping)
**Used by:** dump stepping, speculative stepping, no-JIT runtime (cooperative)

---

#### `Interpreter.Models.Dynamic`

**Purpose**

* Lift DLR call-site patterns into a semantic `DynInvoke*` op
* Overload selection:

  * minimal internal resolver, or
  * optional Roslyn-backed resolver via separate adapter package

**Depends on:** `Interpreter.Models.Abstractions`, `Interpreter.Core.IR` (pattern matching), `Interpreter.Types`
**Optionally depends on:** `Interpreter.Metadata.Abstractions` (to resolve candidate method sets)
**Used by:** dump/live evaluation

---

#### `Interpreter.Models.Collections`

**Purpose**

* Projections + models for:

  * `Dictionary<,>`, `List<T>`, `ConcurrentDictionary<,>`, queues/stacks
* Copy-on-write materialization for dump-backed instances

**Depends on:** `Interpreter.Models.Abstractions`, `Interpreter.Memory.Overlay`
**Optionally depends on:** `Interpreter.Metadata.Abstractions` (field discovery heuristics)
**Used by:** dump/live evaluation

---

#### `Interpreter.Models.CompilerPatterns`

**Purpose**

* Pattern rewrites for noisy IL:

  * `lock` / `Monitor.Enter/Exit` try/finally
  * `foreach` enumerator scaffolding
  * `using` / `Dispose` finally
  * throw helpers / guards
  * interpolated string handler patterns

**Depends on:** `Interpreter.Models.Abstractions`, `Interpreter.Core.IR`
**Used by:** stepping, readability, analysis

---

### 3.4 Optional: Roslyn integration

#### `Interpreter.Models.RoslynAdapter`

**Purpose**

* Uses Roslyn as a *resolver* (not an executor):

  * dynamic overload resolution
  * expression compilation to a synthetic method for “debug this expression”
* Maps `IMethodSymbol` → `MethodKey`

**Depends on:** `Interpreter.Metadata.Abstractions`, `Interpreter.Models.Dynamic` (or shared contract), Roslyn packages
**Used by:** IDE products only
**Important:** keep this out of core so the VM stays lightweight.

---

## 4) Metadata, symbols, decompiler: reader implementations behind abstractions

### 4.1 Abstractions (no heavy deps)

#### `Interpreter.Metadata.Abstractions`

**Purpose**

* Stable identities + resolution contracts:

  * `ModuleKey` (MVID + stamp/build-id)
  * `MethodKey` / `TypeKey` / `FieldKey`
  * `IMetadataUniverse` (resolve tokens → `TypeSig/MethodSig/FieldSig`)
  * `IMethodBodyProvider` (method → `MethodBody`)
  * `ISymbolInfo` (sequence points, scopes, locals)
  * `IDebugMapProvider` (best map: PDB → decompiler → IL fallback)
  * `ISourceTextProvider`

**Depends on:** `Interpreter.Types`, `Interpreter.IL`, `Interpreter.Foundation`
**Used by:** artifacts layer, hosts, decompiler integration, some models

---

### 4.2 Concrete metadata readers

#### `Interpreter.Metadata.SRM`

**Purpose**

* Implementation of `IMetadataUniverse` + `IMethodBodyProvider` using:

  * `System.Reflection.Metadata` + `PEReader`

**Depends on:** `Interpreter.Metadata.Abstractions`, SRM/PEReader
**Used by:** most products as the default metadata backend

---

#### `Interpreter.Metadata.AsmResolver` (optional)

**Purpose**

* Alternative metadata backend using AsmResolver object model
* Useful if you want rewriting or Windows PDB synergy via AsmResolver suite

**Depends on:** `Interpreter.Metadata.Abstractions`, AsmResolver
**Used by:** optional

---

### 4.3 Symbols

#### `Interpreter.Symbols.PortablePdb`

**Purpose**

* Portable PDB reader implementing `ISymbolInfo` using SRM
* Also exposes SourceLink + EmbeddedSource info to source providers

**Depends on:** `Interpreter.Metadata.Abstractions`, SRM
**Used by:** debugger stepping maps, source lookup

---

#### `Interpreter.Symbols.WindowsPdb.Dia` (optional, Windows-only)

**Purpose**

* Windows PDB reader using DiaSymReader native components (or similar)
* Implements `ISymbolInfo`

**Depends on:** `Interpreter.Metadata.Abstractions`, DIA packages
**Used by:** optional Windows support

---

#### `Interpreter.Symbols.WindowsPdb.Managed` (optional)

**Purpose**

* Cross-platform Windows PDB reading (dnlib or AsmResolver.Symbols.Pdb), best-effort
* Implements `ISymbolInfo`

**Depends on:** `Interpreter.Metadata.Abstractions`, dnlib or AsmResolver PDB package
**Used by:** optional

---

### 4.4 Decompiler integration + debug maps

#### `Interpreter.Decompiler.ILSpy`

**Purpose**

* Wrap ILSpy (`ICSharpCode.Decompiler`) to provide:

  * decompiled C# text for a method
  * IL↔text sequence points for stepping fallback
  * `CodeMappingInfo` (state machine/lambda mapping)

**Depends on:** `Interpreter.Metadata.Abstractions`, ILSpy decompiler packages
**Used by:** `IDebugMapProvider` fallback pipeline, UI source view

---

#### `Interpreter.DebugMaps`

**Purpose**

* The unified `DebugMap` data model and builders:

  * from Portable PDB sequence points
  * from ILSpy sequence points
  * synthetic IL/block maps fallback

**Depends on:** `Interpreter.Metadata.Abstractions`, `Interpreter.Foundation`
**Used by:** stepping engine, UI

---

## 5) Artifact acquisition (binaries/PDB/source) – separate from parsing

#### `Interpreter.Artifacts.Abstractions`

**Purpose**

* `IArtifactLocator` (find/open PE/PDB/source)
* identities: `ModuleIdentity`, `PdbIdentity`, `DocumentIdentity`
* policies for symbol server layouts, local caches (policy lives here; caching implementation can be separate)

**Depends on:** `Interpreter.Foundation`
**Used by:** dump/live hosts, metadata backends, source providers

---

#### `Interpreter.Artifacts.SymbolStore` (optional)

**Purpose**

* Implementation that understands symbol-server conventions, local caches, etc.

**Depends on:** `Interpreter.Artifacts.Abstractions` (+ whatever symbol store libs you choose)
**Used by:** product deployments

---

#### `Interpreter.Source.SourceLink` (optional)

**Purpose**

* Resolve SourceLink URLs to source text (policy-driven, offline/online modes)
* Also handles embedded sources from portable PDB when available

**Depends on:** `Interpreter.Artifacts.Abstractions`, `Interpreter.Metadata.Abstractions` (for embedded source info)
**Used by:** source text provider, UI

---

## 6) Hosts: dump, live snapshot, no-JIT runtime

These are the *only* packages that talk to ClrMD / debugger APIs / OS.

### 6.1 Host abstractions

#### `Interpreter.Host.Abstractions`

**Purpose**

* Common host services VM/models may request:

  * `ISessionSnapshot` (time/env/random policy + dump metadata)
  * `IThreadContext` (selected thread/frame identity)
  * `IHeapBackend` (read-only heap reads for dump/live)
  * `ILiveSnapshotProvider` (for live debugging mode)
  * optional “policy surfaces”: network/file/time allowed?

**Depends on:** `Interpreter.Core.Abstractions`, `Interpreter.Artifacts.Abstractions`
**Used by:** host implementations + models like Environment/Async

---

### 6.2 Dump host (ClrMD)

#### `Interpreter.Host.Dump.ClrMD`

**Purpose**

* Implements:

  * dump load, runtime selection
  * heap reads, object/type inspection
  * frame seeding (this/args/locals best-effort)
  * runtime↔metadata bridging (`ClrMethod` → `MethodKey`, module identity resolution)
* Provides a read-only heap backend and seeds an overlay memory

**Depends on:** `Interpreter.Host.Abstractions`, ClrMD, `Interpreter.Metadata.Abstractions`
**Used by:** post-mortem debugger engine

---

### 6.3 Live debugging host (optional)

#### `Interpreter.Host.Live.Snapshot`

**Purpose**

* Build a stable “read-only snapshot” of live process state:

  * stop-the-world briefly, capture registers and key heap pages, resume
  * or best-effort reads with inconsistency detection
* Exposes `ILiveSnapshotProvider` to the VM

**Depends on:** `Interpreter.Host.Abstractions` + whichever live-debug API you use
**Used by:** speculative debugging engine

---

### 6.4 no-JIT runtime host (no-JIT execution)

#### `Interpreter.Host.Runtime.NoJit`

**Purpose**

* A host ABI for executing IL “for real” in a constrained environment:

  * object allocation
  * string/array primitives
  * optional time/env/io gated by policy
* Uses concrete domain + virtual heap, no ClrMD, no PDB, no decompiler

**Depends on:** `Interpreter.Host.Abstractions`, `Interpreter.Memory.VirtualHeap`, `Interpreter.Domain.Concrete`
**Used by:** IL-on-no-JIT runtime product

---

## 7) Debugger stepping engine (VM + DebugMap + policies)

Keep stepping orchestration separate from the VM itself.

#### `Interpreter.Debugger.Engine`

**Purpose**

* Implements Step Into/Over/Out on top of:

  * `Interpreter.Core.Execution` micro-step runner
  * `Interpreter.DebugMaps` statement boundaries
  * model frames (async/dynamic/environment)
  * undo/redo history and branching (if you support “choose true/false”)
* Owns “stop predicates”, not the VM

**Depends on:** `Interpreter.Core.Execution`, `Interpreter.DebugMaps`, `Interpreter.Host.Abstractions`, `Interpreter.Models.Abstractions`
**Used by:** dump debugger product, speculative debugger product

---

## 8) Product compositions (what you ship as “top-level” packages)

These are “glue” packages that wire everything together with reasonable defaults.

### A) Post-mortem dump debugger engine

#### `Interpreter.Product.DumpDebugging`

**Includes/wires**

* `Interpreter.Host.Dump.ClrMD`
* `Interpreter.Metadata.SRM`
* `Interpreter.Symbols.PortablePdb` (+ optional WindowsPdb plugin)
* `Interpreter.Decompiler.ILSpy` + `Interpreter.DebugMaps`
* `Interpreter.Debugger.Engine`
* Domains: `CNTypeOriginLabels` + VirtualHeap + Overlay
* Models: CoreLib, Environment, Async, Dynamic, Collections, CompilerPatterns

**Depends on:** everything above
**No other package should depend on this.**

---

### B) Static analysis toolchain

#### `Interpreter.Product.StaticAnalysis`

**Includes/wires**

* `Interpreter.Metadata.SRM`
* `Interpreter.Core.IR` + `Interpreter.Core.Analysis`
* Domains: CNTypeOriginLabels + Range + SummaryHeap
* Models: CoreLib, Environment (for effect tagging), CompilerPatterns
* Optional: decompiler only for presentation, not required

**Avoids**

* ClrMD, dump host, debug maps

---

### C) Speculative/live debugging augmentation

#### `Interpreter.Product.LiveSpeculation`

**Includes/wires**

* `Interpreter.Host.Live.Snapshot` (or whatever live snapshot provider)
* `Interpreter.Debugger.Engine`
* Domains: CNTypeOriginLabels + Overlay over live snapshot backend
* Models: Environment (target-derived snapshot), Async, Dynamic, Collections, CompilerPatterns
* Optional: RoslynAdapter for “compile expression to synthetic method” experience

---

### D) No-JIT runtime / bounded execution

#### `Interpreter.Product.NoJitRuntime`

**Includes/wires**

* `Interpreter.Host.Runtime.NoJit`
* `Interpreter.Core.Execution`
* `Interpreter.Domain.Concrete` (+ optional CNTypeOriginLabels for diagnostics mode)
* `Interpreter.Memory.VirtualHeap`
* Models: CoreLib.Primitives + a policy-driven set of environment/io services
* No PDB, no decompiler, no artifacts, no ClrMD

---

## Dependency sanity checks (the “things you must not do”)

These rules keep reuse intact:

* `Interpreter.Core.Execution` **must never** reference:

  * ClrMD
  * SRM/AsmResolver
  * ILSpy
  * Roslyn
  * symbol server acquisition
* `Interpreter.Metadata.*` packages **must never** reference:

  * ClrMD
  * debugger stepping
  * models
* `Interpreter.Decompiler.ILSpy` **must never** reference:

  * ClrMD
  * VM execution
* `Interpreter.Host.Dump.ClrMD` **may** reference metadata abstractions (to produce `MethodKey` etc.), but **should not** depend on ILSpy/Roslyn.

---

## A practical “merge later” plan (without losing clarity)

If you later decide granularity is too much, merge along *layer boundaries*, not across them:

Compatible merges (usually fine):

* `Interpreter.Foundation` + `Interpreter.Types` + `Interpreter.IL` into `Interpreter.Core.Common`
* `Interpreter.Core.Execution` + `Interpreter.Core.Abstractions` into `Interpreter.VM`
* `Interpreter.DebugMaps` + `Interpreter.Decompiler.ILSpy` into `Interpreter.Source`
* Model packages into `Interpreter.Models`

Merges I would **avoid** even long-term:

* Dump host (`ClrMD`) merged into core VM
* Roslyn adapter merged into model core
* Artifact acquisition merged into metadata readers

Those are the seams that keep “runtime mode” and “analysis mode” from inheriting dump-debug baggage.

---

## Extension points (how new subsystems plug in)

This modular split makes “add feature X” predictable:

* New value facts → new `Interpreter.Domain.*` package
* New layout projection (e.g., `ConcurrentQueue`) → extend `Interpreter.Models.Collections`
* New platform host (WASM, game engine) → new `Interpreter.Host.*`
* New decompiler (if you ever add one) → new `Interpreter.Decompiler.*`
* New symbol story → new `Interpreter.Symbols.*`
