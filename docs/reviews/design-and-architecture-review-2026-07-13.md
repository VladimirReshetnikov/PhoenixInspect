# Design and Architecture Review — 2026-07-13

## Review metadata

| Field | Value |
|---|---|
| Review type | Independent design, architecture, planning, and prototype review |
| Repository revision | `1eb9d97e7` (`Fix package versions and other errors`) |
| Review date | 2026-07-13 |
| Project phase assessed | Documentation-first conceptual design with an early prototype seam |
| Scope | Product goals, semantic model, architecture, integrations, roadmap, governance, prototype APIs/code, tests, build, and dependencies |
| Overall recommendation | **Conditional go after scope and contract reset** |

This is a point-in-time review, not a normative replacement for the proposals it assesses. It is deliberately candid because the project has a promising core idea and is early enough to correct structural problems cheaply.

---

## 1. Executive assessment

The project's central product thesis is plausible and valuable:

> A dump-aware evaluator can answer many read-only questions by combining snapshot facts, metadata, bounded interpretation, conservative unknowns, and explanations.

The strongest parts of the design are its insistence on immutable dump state, explicit uncertainty, deterministic resource bounds, adapter isolation, and provenance. Those are exactly the right instincts for post-mortem tooling, where a plausible wrong answer is worse than an explicit unknown.

The project is not yet ready to lock its public contracts or proceed down its current roadmap. The present corpus combines at least four substantially different systems:

1. a dump object/query evaluator,
2. a counterfactual IL execution and virtual-debugging engine,
3. an abstract-interpretation/static-analysis framework, and
4. a concrete sandbox/no-JIT runtime.

They can share selected primitives, decoded IL, metadata identities, and some opcode transfer logic. They do **not** naturally share one state model, one notion of correctness, one effect contract, or one delivery plan. Treating all four as one platform from the outset has already produced premature abstractions, 42 source projects (34 empty), 103 direct project-reference edges, multiple competing “canonical” contracts, and a roadmap that implements abstract analysis and async/dynamic lifting before proving the first useful dump-backed expression slice.

The most important conceptual correction is to separate facts from simulations. A dump-backed method interpretation does not reconstruct history and usually cannot answer “why did this value become X?” It answers a different, still useful question:

> “Starting from the values visible in this snapshot, what would this supported code compute under these explicit models and assumptions?”

The product and trust model should distinguish snapshot observation, derived query, counterfactual execution, and abstract possibility. “Pure/Partial/Blocked” is not enough to communicate those distinct epistemic claims.

### Recommendation in one sentence

Continue the project, but narrow the first product to a read-only dump query/evaluation vertical slice; define a small normative semantic core and evidence model; defer generalized analysis, async/dynamic virtualization, projection mutation, and sandbox runtime work until dump-backed IL evaluation has demonstrated value and correctness on a representative corpus.

### Decision summary

| Question | Assessment |
|---|---|
| Is the user problem real? | **Yes.** Dump analysis often requires tedious manual derivation from available state. |
| Is read-only dump expression evaluation plausible? | **Yes, with a deliberately constrained language and explicit completeness limits.** |
| Is bounded interpretation of selected managed methods plausible? | **Yes, as counterfactual execution over a supported subset—not as CLR replay.** |
| Is source-level virtual stepping plausible? | **Plausible but expensive.** IL stepping is a realistic intermediate product; debugger-grade source stepping is later. |
| Is general async/dynamic virtualization plausible? | **Only for curated compiler/runtime patterns at first.** General-fidelity claims would be misleading. |
| Can one engine also become a serious static analyzer and no-JIT runtime without major divergence? | **Not as currently planned.** Transfer kernels may be shared; products and state semantics should diverge. |
| Is the current roadmap plausible for a small team? | **No.** It is breadth-first and dependency-inverted relative to product value. |
| Are current prototype APIs ready to stabilize? | **No.** Several cannot express mandatory partialness, provenance, identity, or IL semantics. |

---

## 2. Review method and evidence

The review covered:

- the required entry points, [root README](../../README.md) and [documentation index](../README.md);
- every product, architecture, integration, planning, and governance proposal;
- the backend decision/evidence material under [`docs/lib`](../lib/README.md);
- all handwritten source in `src/` and `tests/`;
- the full project graph and package references;
- a clean restore, clean build, and test run;
- current NuGet vulnerability, deprecation, and update information;
- targeted primary upstream sources for ClrMD, Portable PDB, ILSpy, and modern ECMA-335 extensions.

Observed repository facts:

| Measure | Observation |
|---|---:|
| Markdown documents reviewed under `docs/` (excluding this report) | 56 |
| Documents with a visible status marker near the top | 4 |
| Documents absent from the canonical `docs/README.md` inventory | 8 |
| Broken relative Markdown links within the `docs/` corpus | 0 |
| Source projects | 42 |
| Source projects with handwritten `.cs` files | 8 |
| Empty source projects | 34 |
| Direct project-reference edges | 103 |
| Test projects | 2 |
| Test cases | 1 |
| Clean-build result | Success; 98 warnings, including one nullable warning |
| Test result | 1 passed, 0 failed, 0 skipped |
| Known vulnerable packages reported by NuGet | None |
| Deprecated direct packages reported by NuGet | `xunit` 2.x (“Legacy”; xUnit v3 suggested) |

The successful build and test are useful evidence that the current seam compiles and that a same-machine full dump can be opened. They are not evidence of meaningful interpreter coverage or dump-backed value evaluation.

---

## 3. What is strong and worth preserving

### 3.1 The product problem and safety posture are excellent

The [post-mortem evaluator proposal](../proposals/product/post-mortem-debugging-feature-proposal.md) identifies a genuine gap between raw heap inspection and computed answers. Its principles—snapshot immutability, bounded execution, safety, honesty, and familiar syntax—are sound and mutually reinforcing.

The explicit rejection of arbitrary native execution, I/O, process mutation, and fabricated missing locals is especially important. This should remain the project's identity.

### 3.2 Unknowns, effects, and provenance are correctly treated as product features

The [interpreter framework](../proposals/architecture/il-interpreter-framework-proposal.md), [state/domain model](../proposals/architecture/state-and-domain-model-proposal.md), and [call/effects contract](../proposals/architecture/call-model-and-effects.md) consistently recognize that unsupported or missing information must remain visible and composable.

That is a meaningful differentiator from evaluators that simply fail or silently substitute defaults. The “why unknown?” goal is product-grade, not merely an analysis implementation detail.

### 3.3 Backend isolation is directionally correct

The proposed separation of runtime observation, artifact acquisition, metadata/symbol projection, execution binding, and interpreter semantics is sound. ClrMD, metadata readers, ILSpy, Roslyn, and symbol acquisition should not leak into opcode semantics.

The library source-review notes show good awareness of lifecycle, lazy decoding, cache policy, mutable backend object graphs, and format-specific failure behavior. These observations are more mature than the current prototype contracts that are supposed to embody them.

### 3.4 The design acknowledges real hard problems

The proposals do not pretend that calls, generics, EH, optimized frames, symbols, dynamic binding, async state machines, and version-dependent layouts are easy. The dedicated investigations are useful research artifacts even where their proposed rollout order is too aggressive.

### 3.5 The testing philosophy is strong

Differential execution, lattice-law tests, determinism replay, golden explanations, malformed artifacts, and adapter conformance are all appropriate. The [testing strategy](../proposals/architecture/testing-strategy-proposal.md) is a good catalog of eventual techniques.

What is missing is prioritization: the first tests should validate user-value vertical slices and the most dangerous trust boundaries, not a broad matrix before a coherent semantic contract exists.

---

## 4. Fundamental product and semantic findings

### F1 — Critical: the product lacks an explicit truth model

The proposals move between four different meanings of a result without naming the distinction:

| Mode | What it can honestly claim | Example |
|---|---|---|
| Snapshot observation | “This data was present/readable in the captured process state.” | A field value or array element read from the dump |
| Derived snapshot query | “This result follows from supported pure operations over observed values.” | Null-safe navigation, arithmetic, bounded projection |
| Counterfactual virtual execution | “This code would produce this result from the snapshot state under listed models and assumptions.” | Interpreting a getter with virtual writes and modeled time |
| Abstract analysis | “These outcomes are possible under this abstraction.” | Joined branch states, ranges, taint, possible calls |

Current badges such as “Pure (Executed),” “Partial,” and “Blocked” conflate evidence source, semantic mode, capability coverage, and confidence. A fully deterministic counterfactual can still be semantically unlike what happened in the dead process. An abstract join can be sound but not correspond to any concrete state. A value can be exactly read from a dump whose module identity is only weakly matched.

This matters most in claims that virtual stepping helps answer “why is this value X?” Re-executing code from a post-mortem state is not causal replay. Without the earlier state, actual path decisions, external inputs, thread interleaving, and runtime events, it cannot explain how the captured value historically arose.

**Required change:** make semantic mode part of every result and adjust product language from historical explanation to counterfactual exploration. Preserve causal language only for actual captured trace/provenance evidence.

### F2 — Critical: “safe/pure method execution” is underspecified

The product proposal promises methods/getters that are “provably safe,” but no sound proof boundary is defined. Purity is not just absence of a few effect tags:

- a call may not write externally but can diverge, throw, allocate, observe object identity, trigger type initialization, depend on culture, or read mutable static/thread state;
- a model may describe a call's *possible* effects, while a trace reports *observed simulated* effects; those are different quantities;
- an unknown call that may mutate reachable state cannot safely continue with only an unknown return unless memory is conservatively invalidated;
- a method classified from incomplete IL/call summaries is “safe within this model and coverage,” not proved pure in an absolute sense.

The current `EffectKind` sets also vary across documents (`reads_memory`, `writes_memory`, and `impure_external` in one place; `Alloc`, `ReadEnv`, `Time`, `Native`, and others in code). There is no canonical lattice, no separation between dependency and side effect, and no preflight-versus-runtime distinction.

**Required change:** replace the single effect summary with at least:

- `MayEffects` (conservative capability/effect envelope),
- `SimulatedEffects` (events actually applied in this virtual run),
- `Dependencies` (snapshot, ambient, model, heuristic),
- `Assumptions`, and
- `Coverage/Confidence`.

Use wording such as “no disallowed effects found within supported coverage,” not “provably pure,” until a much stronger analysis contract exists.

### F3 — Critical: deterministic instruction budgets cannot be deferred

The [technical-stack proposal](../proposals/architecture/technical-stack-proposal.md) and Phase 2 product text defer explicit instruction/time budgeting and rely on cooperative cancellation. That directly contradicts the top-level bounded-execution principle and the virtual-stepper's “hard budget” requirement.

Cancellation is not a bound. A synchronous tight interpreter loop, pathological metadata graph, projection cycle, or model that fails to poll can ignore it indefinitely. Wall-clock deadlines are also nondeterministic as semantic cut points.

**Required change:** deterministic counters for instructions, calls, allocations, artifact bytes, object visits, projection items, path forks, and fixpoint work must exist in the first executable vertical slice. Cancellation is an additional responsiveness mechanism, not a substitute.

### F4 — High: the 70% MVP coverage claim is unsupported

The product proposal says Phase 1 should cover 70% of common inspection questions, but there is no workload corpus, telemetry baseline, user study, or definition of an evaluation denominator. The proposed opcode and data coverage also omits common constructs needed by ordinary compiled C#.

**Required change:** turn this into a hypothesis. Build a corpus of real dump questions and report coverage by scenario class, not an ungrounded aggregate percentage.

### F5 — High: expression evaluation is the primary product but the front end is not designed

The repository has rich designs for async, dynamic, generic contexts, decompilation, and abstract domains, but no dedicated design for the C# expression front end. The virtual-step proposal briefly suggests compiling a synthetic method with Roslyn, yet the dump product composition does not reference the Roslyn adapter.

Missing decisions include:

- expression-only grammar versus statements/scripts;
- how locals, arguments, `this`, pseudo-variables, aliases, and imports enter binding;
- how metadata from the exact dump modules becomes Roslyn references;
- how type forwarders, duplicate load contexts, missing reference assemblies, and runtime/compiler version skew are handled;
- how synthetic IL identities map back into the interpreter universe;
- how debugger-specific member access differs from ordinary C# accessibility;
- how side-effect diagnostics appear before and during evaluation;
- how field-as-data/auto-property access avoids invoking user code.

**Required change:** make “Expression Front End and Binding” a first-class architecture track before async/dynamic work. Phase 1 may be better implemented as a constrained query AST over snapshot values rather than compiling arbitrary C# immediately.

---

## 5. Scope and roadmap findings

### F6 — Critical: the roadmap is platform-first rather than value-first

The [future-work plan](../plans/future-work-planning.md) schedules CFG/fixpoint analysis at M2, async/dynamic at M3.6, semantic projections at M3.7, and dump-aware hosting at M4. This means several expensive reusable-platform bets precede proof that the product can answer a useful dump question.

The plan also treats a static analyzer, live speculative debugger, dump debugger, and sandbox runtime as sibling product compositions from the start. A concrete runtime cannot tolerate the unknowns acceptable in dump analysis; a sound analyzer needs monotone abstract transfer and EH-aware CFGs; an interactive debugger needs occurrence-sensitive stepping and persistent histories. Sharing package names does not erase those semantic differences.

**Required change:** organize milestones as thin vertical slices through dump acquisition, identity, value materialization, expression binding, execution, and result explanation. Promote reusable pieces only after two real consumers demonstrate the seam.

### F7 — High: major features are being treated as “models” before their semantic contracts exist

Async, dynamic, `foreach`, locking, concurrent collections, interpolation, and LINQ are individually large features. The proposals offer credible research directions, but collectively they amount to a partial C# compiler/runtime/decompiler/debugger stack.

Examples of semantic risk:

- the async proposal's “queued, never inline” scheduler intentionally differs from real continuation semantics and can change reentrancy-sensitive behavior;
- an incomplete external task in a frozen dump has no future event that can complete it, so suspension needs an explicit assumption/branch protocol rather than an ordinary scheduler;
- replacing `DateTime.Now` with dump-capture time is an assumption, not an exact environment read;
- treating locks as no-ops must still preserve null checks, `lockTaken`, exception/finally behavior, and memory effects;
- copying a partially enumerated dump collection into a virtual mutable collection can manufacture false completeness;
- Roslyn-based overload selection over cast runtime types is useful but does not automatically reproduce every DLR binder rule, meta-object path, COM behavior, or historical compiler/runtime version.

**Required change:** classify each semantic lift as one of `ExactByContract`, `ExactForRecognizedShape`, `Conservative`, `Heuristic`, or `AssumptionDriven`. Do not place async/dynamic in an MVP exit criterion until ordinary calls, EH, byrefs, and artifact identity are proven.

### F8 — High: opcode sequencing is not aligned with real C# IL

The [opcode matrix](../proposals/architecture/opcode-support-matrix-proposal.md) is useful as a family-level start, but it defers or omits constructs required by routine modern C#:

- `ldloca`, `ldarga`, `ldflda`, array element addresses, and indirect loads/stores;
- `initobj`, `isinst`, `castclass`, `ldtoken`, `sizeof`, and static initialization behavior;
- `constrained.` calls, which are central to generic and value-type code;
- exception regions and `finally`, common in `using`, `lock`, iterators, and async lowering;
- checked arithmetic/conversions, unsigned comparisons, floating unordered comparisons, native-int width, and runtime-profile-specific behavior;
- static, thread-static, and generic static storage.

Current .NET's ECMA-335 addendum includes newer semantics such as static interface dispatch under `constrained.`, byref fields, byref-like generic allowances, and checked operator naming. A target semantic/runtime profile must therefore be explicit; “ECMA-335 IL” is not one frozen behavior set. See the [.NET ECMA-335 addendum](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Ecma-335-Augments.md).

**Required change:** derive the first opcode slice from compiled scenario fixtures, not opcode popularity. Make unsupported stack effects explicit; injecting an unknown is only valid when the instruction's stack transition and side-effect envelope are known.

---

## 6. Architecture and contract findings

### F9 — Critical: there is no single normative architecture/specification

The repository says key terms should have one canonical definition, but multiple documents independently define:

- `MachineState`, `FrameState`, and `StepOutcome`;
- stop reasons;
- effects and trust labels;
- method bodies and identities;
- the special-semantics registry;
- package boundaries and dependency direction.

Those definitions disagree. Examples include:

- five stop reasons in the state/stepping docs versus `Running`, `Completed`, `BudgetExceeded`, and `Faulted` in code;
- state models with async/path/effect/determinism fields versus the prototype's call stack, memory, and budget only;
- method bodies with locals, flags, and EH in proposals versus only `MaxStack` and raw bytes in code;
- AsmResolver as the provisional chosen backend versus SRM in implemented code and the integration proposal's concluding recommendation;
- documents claiming `src/` is scaffolding-only after concrete interfaces and execution code exist.

The right answer is not to force the prototype to implement every aspirational field. It is to state which document is normative, which sketches are historical, and which contracts are experimental.

**Required change:** create a short accepted architecture decision set and a normative `execution-semantics-v0` specification. Mark overlapping proposal API sketches non-normative or superseded.

### F10 — Critical: mandatory partialness/provenance cannot be represented by current APIs

The documentation repeatedly requires structured `Success`, `Partial`, `Unavailable`, and `Conflict` outcomes with reason codes and provenance. The prototype largely uses `bool Try*`, raw return values, or exceptions:

- [`IResolutionServices`](../../src/Interpreter.Core.Abstractions/IResolutionServices.cs) cannot return missing/corrupt/ambiguous metadata with provenance;
- [`IMemoryModel`](../../src/Interpreter.Core.Abstractions/IMemoryModel.cs) cannot distinguish an unavailable read, target exception, abstract unknown, or policy block;
- [`IExternalObjectModel`](../../src/Interpreter.Host.Abstractions/IExternalObjectModel.cs) discards completeness and miss reasons;
- [`IProcessMemoryReader`](../../src/Interpreter.Host.Abstractions/IProcessMemoryReader.cs) cannot report partial reads;
- [`IGenericContextResolver`](../../src/Interpreter.Host.Abstractions/IGenericContextResolver.cs) returns all-or-nothing despite the generic design requiring per-argument evidence and partial contexts;
- [`IDebugMapProvider`](../../src/Interpreter.Metadata.Abstractions/IDebugMapProvider.cs) cannot report which fallback won or why stronger maps failed.

`NotSupportedException` is acceptable inside a deliberately tiny spike, but these interfaces are described as public boundary prototypes. Their shapes push the implementation away from the project's defining explainability requirement.

**Required change:** use typed result envelopes at evidence-bearing boundaries. Exceptions should represent programming bugs or unrecoverable process failures, not ordinary artifact absence.

### F11 — High: the value-domain abstraction is too small for its declared consumers

[`IValueDomain<TValue>`](../../src/Interpreter.Core.Abstractions/IValueDomain.cs) has construction, join/widen, a few queries, arithmetic, conversion, boxing, and null refinement. It lacks capabilities required by the analysis and stepping documents:

- partial order or semantic equality for fixpoint convergence;
- bottom/unreachable;
- branch truth classification and refinement of state locations;
- exception-producing operations;
- unsigned/ordered/unordered comparison distinctions;
- overflow and divide-by-zero outcomes;
- runtime-size/profile inputs;
- reduced-product normalization to prevent inconsistent combinations such as nullness/type-set/constant contradictions;
- stable symbolic identity for facts about locals, fields, and aliases.

The `CN-T` design also describes a plain product of facts but not the reduction function that keeps facets consistent. Its `ConditionInfo` sketches values referring to values, while actual state refinement requires stable locations/symbols rather than replacement of one immutable value instance.

**Required change:** avoid stabilizing a universal public domain interface now. First specify opcode transfer outcomes and implement one concrete/hybrid domain. Extract a reusable analysis interface only after the fixpoint engine exists.

### F12 — High: the memory model omits essential CLR locations and outcomes

The current abstraction has object/array allocation, fields/elements, two byref constructors, indirect access, and havoc. It has no contract for:

- static, thread-static, RVA, and generic-static fields;
- argument, array-element, static-field, or nested-struct addressables;
- value-type copy semantics and `initobj`;
- array length/rank/bounds and covariance checks;
- strings, type objects, boxed values, and object runtime types;
- type initialization;
- target exceptions versus missing data;
- dump-backed versus virtual reference namespaces;
- snapshot read provenance and overlay versioning.

This is not merely missing implementation; it makes ordinary C# execution impossible to express faithfully.

**Required change:** model `Location`/`Addressable` explicitly, separate read evidence from domain value, and define an arena-qualified object identity covering dump, virtual, summary, and intrinsic objects.

### F13 — High: the call outcome and model capability surface are unsafe

[`CallOutcome<TValue,TMem>`](../../src/Interpreter.Core.Abstractions/CallOutcome.cs) is a nullable record with a discriminator but no invariant enforcement. Invalid combinations are constructible; `default` can be both “not supplied” and a valid value/memory state. It also lacks the modeled outcome confidence, selected rule identity, assumptions, and diagnostics required by the special-semantics proposal.

`CallModelContext.Services` exposes an unrestricted `IServiceProvider`. That is convenient but undercuts a capability-based safety architecture: a model can resolve arbitrary host services unless composition discipline is perfect.

**Required change:** use closed outcome variants and pass an explicit, capability-limited model context. Keep host I/O unavailable by construction in dump mode.

### F14 — High: identities are not stable enough for replay or cross-layer binding

The architecture correctly emphasizes stable IDs, but the prototype violates that requirement:

- [`SrmMetadataModule.ComputeStableHandleValue`](../../src/Interpreter.Metadata.SRM/SrmMetadataModule.cs) uses `StringComparer.OrdinalIgnoreCase.GetHashCode(path)`, which is process-randomized and collision-prone;
- method handles are allocated in access order, making them schedule/order dependent;
- `GenericContext` contains `IReadOnlyList` values whose record equality is reference equality, so equivalent contexts can produce different handles;
- [`ModuleId`](../../src/Interpreter.Metadata.Abstractions/ModuleId.cs) is a record whose equality includes optional name, path hint, and PE stamp, so the same MVID with different hints compares unequal;
- opaque session handles cannot by themselves identify methods in serialized replay artifacts.

**Required change:** distinguish canonical identity from evidence/hints. Prefer structural IDs such as module content identity plus metadata entity handle plus structural generic instantiation. Session-local dense IDs may exist as caches, but serialized events must carry canonical keys.

### F15 — High: source-stepping semantics are control-flow and occurrence insensitive

The debug-map proposals use “next statement offset” and stop when a statement ID changes. That is insufficient:

- control flow does not proceed in lexical/IL offset order across branches and loops;
- revisiting the same sequence point on another loop iteration should usually stop again even though its statement ID did not change;
- one source statement may have discontinuous IL ranges;
- hidden points, filters/finally, async remapping, and decompiled ranges can overlap;
- stepping is based on executed sequence-point occurrences and call/return events, not simply the next greater offset.

The prototype [`SequencePoint`](../../src/Interpreter.Metadata.Abstractions/SequencePoint.cs) explicitly leaves its end offset “inclusive or inferred boundary depending on backend,” which defeats normalization. Portable PDB sequence points contain a start IL offset but no IL end; the project must define its own single exclusive-range inference rule. The upstream [Portable PDB specification](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#sequence-points-blob) confirms this representation.

**Required change:** represent an ordered set of mapping intervals with explicit provenance and use execution events to detect a *new stoppable occurrence*. Treat forward/backward offset lookup as a UI navigation helper, not execution semantics.

### F16 — Medium: raw IL and normalized IR responsibilities are conflated

Sharing opcode semantics between a raw stepper and analysis IR is attractive, but the documents alternate between executing raw decoded IL, rewriting IL patterns, and executing a normalized typed-temp IR. Pattern lifting before execution can change the unit and fidelity of stepping; analysis needs explicit exceptional edges and block transfer; debugger micro-stepping needs exact IL offsets and call events.

**Required change:** start with decoded IL plus a semantics kernel. Let optional recognizers annotate/lift recognized regions without deleting raw provenance. Introduce a separate IR only when the first analysis consumer proves the need.

---

## 7. Dump and artifact integration findings

### F17 — Critical: the current integration test does not prove dump-backed interpretation

The [ret-only integration test](../../tests/Interpreter.IntegrationTests/RetOnlyDumpIntegrationTests.cs) passes, but its dump is only used to create a ClrRuntime and find a module by filename. The test then opens the module path from the analysis machine and reads IL from that file. Its MVID comparison compares two reads of the same still-present local build artifact; it does not obtain and compare a dump-derived module identity.

The test does not:

- read method IL from dump memory;
- validate a located PE against identity captured from the dump;
- seed `this`, arguments, locals, statics, or heap values from the dump;
- execute a method active in a dump frame;
- read any dump-backed object or field;
- test missing/mismatched artifact behavior;
- exercise token resolution, return values, calls, EH, or overlays.

The target calls `RetOnly` before printing `READY`, so the method is not even active when the dump is captured. That is fine for a module-discovery smoke test, but the test and plan should name the evidence accurately.

There is also a failure-path hang: if readiness times out or the wrong line is read, `StartAndWaitReady` calls `StandardError.ReadToEnd()` before killing the sleeping process, so the read can wait forever for EOF.

**Required change:** rename this test as a smoke test and add vertical integration tests described in Section 13.

### F18 — High: the ClrMD adapter treats target paths as trusted local artifacts

[`ClrmdDumpSession`](../../src/Interpreter.Host.Dump.ClrMD/ClrmdDumpSession.cs) converts `module.Name` to `Path.GetFullPath` and exposes it as a local file path. A target path is only a hint; it may belong to another machine/OS, may no longer exist, or may now identify different bytes.

The adapter also selects `ClrVersions.First()`, has no multi-runtime policy, exposes ClrMD's `ClrRuntime` publicly, loses module address/identity/provenance, and translates ordinary failures into exceptions rather than structured outcomes.

Current upstream ClrMD's [`ClrStackFrame`](https://github.com/microsoft/clrmd/blob/main/src/Microsoft.Diagnostics.Runtime/ClrStackFrame.cs) exposes frame context, instruction pointer, stack pointer, kind, and method—not a general locals/arguments API. Frame seeding therefore requires architecture-specific context decoding, symbols, GC/root evidence, or explicit unavailability; it cannot be treated as a routine ClrMD projection.

**Required change:** make the runtime adapter emit target path hints plus runtime/module identity facts. Artifact acquisition must independently locate bytes and verify identity before metadata use.

### F19 — High: method body provenance and representation are incomplete

The current SRM adapter returns only max stack and IL code bytes. It drops init-locals, local signature, EH regions, body header form, and decoding diagnostics required by the proposals.

ClrMD's current [`ClrMethod.GetILInfo`](https://github.com/microsoft/clrmd/blob/main/src/Microsoft.Diagnostics.Runtime/ClrMethod.cs) returns the code address/length, flags, and local-signature token. It does not hand the interpreter a normalized full method body or decoded EH regions. A dump-memory body path therefore needs careful header/extra-section acquisition and parsing, not merely reading the reported code span.

**Required change:** define one immutable normalized method body with explicit `[start,end)` instruction boundaries, locals, EH, header flags, body source, artifact identity, decode completeness, and diagnostics.

### F20 — High: module and PDB identity need redesign

MVID alone is not a cryptographic identity and optional hints must not participate accidentally in equality. PE debug identity, content hashes, build IDs for non-Windows images, in-memory/EnC variants, ReadyToRun/native-image relationships, and dynamic methods all require explicit variants.

The current `PdbId(Guid, Age)` also models CodeView-style identity but Portable PDB's standalone `#Pdb` stream has a 20-byte content ID. These concepts should not be silently collapsed.

**Required change:** use discriminated identities and preserve both expected identity and acquired artifact evidence. Make mismatch a first-class result and default to fail closed for execution.

---

## 8. Prototype code assessment

The prototype should be judged as a seam experiment, not as incomplete production code. Within that framing, it has successfully demonstrated solution wiring, basic project layering, SRM method-body access, same-machine dump loading, and a deterministic one-opcode root completion.

The important issue is that several experimental shapes have already been promoted in docs as the “first implemented abstraction wave,” even though they encode decisions that conflict with the declared architecture.

### 8.1 Specific correctness and contract issues

| Area | Observation | Architectural consequence |
|---|---|---|
| `ret` semantics | [`IlMachine.StepOne`](../../src/Interpreter.Core.Execution/IlMachine.cs) simply pops the frame. It does not validate void/non-void stack shape, transfer a return value, or resume the caller IP. | It supports only root void `ret`, not the `ret` opcode generally. |
| Debug events | Budget exhaustion before decode, missing bodies, invalid offsets, and unsupported opcodes emit `InstructionExecuted`. | Replay/explanations assert events that did not occur. |
| Stop reasons | Missing body and unsupported opcode are both `Faulted`; budget is `BudgetExceeded`. | Target failure, capability miss, invalid IL, and engine bug are conflated. |
| Resolution | `MetadataResolutionServices` ignores the supplied module handle and wraps a single module. | Cross-module tokens and canonical universe semantics cannot work. |
| Method discovery | `TryFindMethodDefToken` ignores namespace, signature, arity, and overloads, selecting the first name match. | Fine as a test helper; unsafe as reusable API. |
| Handle allocation | Method handles depend on lookup order and reference-equality generic contexts. | Replay and cache keys are unstable. |
| Metadata errors | Arbitrary tokens are converted to method-definition handles without validation; many operations throw `NotSupportedException`. | Ordinary partial artifacts are not explainable outcomes. |
| External values | `ExternalValue` lacks static/runtime type, unsigned/native-width distinctions, provenance, confidence, truncation, and miss reason. | It cannot seed typed IL state honestly. |
| Frame seed | Locals are keyed by display name, not IL slot/scope. | Shadowed names and actual `stloc/ldloc` mapping are lost. |
| Immutability | Records contain arbitrary `IReadOnlyList`/dictionary implementations and opaque `object` payloads. | State advertised as immutable/serializable can mutate or fail stable hashing. |

### 8.2 Build and repository hygiene

The clean build succeeds but reports 98 warnings. Most are missing XML documentation warnings, and one is a real nullable warning in module projection. The repository explicitly requires detailed XML comments on public prototype APIs, but the build sets `TreatWarningsAsErrors=false`, so the rule is not enforced.

There is no `global.json`; on the review machine the solution selected .NET SDK 11 preview while targeting .NET 8 and using `LangVersion=latest`. That makes compiler/SDK behavior environment-dependent. No NuGet lock files were found despite the technical-stack proposal calling for lock files and deterministic builds.

**Required change:** pin the SDK, pin or centrally manage packages, enable lock files, and make the documentation requirement enforceable in code-bearing projects. A prototype can suppress selected warnings explicitly; it should not normalize a noisy build.

### 8.3 Project granularity

Forty-two source assemblies and 103 direct project edges are premature for eight projects containing code. The four empty product-composition projects reference up to fifteen assemblies each. This increases restore/build/IDE cost and creates false confidence that boundaries have been validated.

**Required change:** retain the conceptual module map in documentation but collapse the executable prototype to roughly 6–10 projects until ownership and deployment boundaries are proven. Split assemblies for actual dependency, platform, security, or versioning reasons—not one speculative capability per assembly.

---

## 9. Backend and dependency assessment

### 9.1 The backend decision is internally inconsistent

The [MVP backend decision record](../lib/mvp-backend-decision-record.md) selects AsmResolver and declares gates with a “Medium confidence minimum,” while every relevant row in the [evidence log](../lib/backend-evidence-log.md) remains Low confidence and mostly source-review based. The gates are marked “Provisional pass” despite not meeting their stated threshold.

At the same time:

- the only implemented metadata backend is SRM;
- product composition references SRM, not AsmResolver;
- the PE/PDB integration proposal starts with an AsmResolver decision update but ends by recommending SRM/PEReader as canonical;
- the AsmResolver project was pinned to a beta even though a stable 6.0.0 is now available.

**Assessment:** choosing one backend for a spike is reasonable; calling it an architecture decision before the spike evidence exists is not. The present code is actually useful evidence in favor of SRM for the minimal path.

**Required change:** downgrade the record to `Proposed/Experiment`, run the same normalized method-body + Portable PDB + generic-signature fixtures through AsmResolver and SRM, then decide based on measured adapter complexity, failure behavior, performance, and ILSpy impedance.

### 9.2 Current package health

A 2026-07-13 NuGet audit reported no known vulnerable direct or transitive packages from configured sources. It reported xUnit 2.x as legacy and multiple available updates:

| Package | Requested | Current latest reported |
|---|---:|---:|
| `ICSharpCode.Decompiler` | 8.2.0.7535 | 10.1.0.8386 |
| `Microsoft.Diagnostics.Runtime` | 3.1.455904 | 4.0.732401 |
| `AsmResolver.DotNet` | 6.0.0-beta.5 | 6.0.0 |
| `System.Reflection.Metadata` | 9.0.0 | 10.0.9 |
| `Microsoft.CodeAnalysis.CSharp` | 4.11.0 | 5.6.0 |
| `Microsoft.DiaSymReader` | 2.0.0 | 2.2.9 |

This is not a recommendation to update blindly. ClrMD and ILSpy major versions can change the very APIs under design. It is evidence that the current source reviews and proposals are version-stale and need a declared compatibility baseline.

The “Managed” Windows PDB project currently references the same `Microsoft.DiaSymReader` package as the DIA-named project, so its name does not yet represent a different managed cross-platform strategy.

---

## 10. Documentation and governance assessment

The repository has a large amount of thoughtful material, but it does not yet operate as a design system.

### 10.1 Governance rules are mostly aspirational

The [documentation organization proposal](../governance/documentation-organization-proposal.md) requires document types/statuses, owners, lifecycle, canonical definitions, ADRs/specs, subfolder indexes, and explicit supersession. In the current corpus:

- only 4 of 56 documents expose a status marker near the top;
- no consistent owner/last-reviewed metadata exists;
- the proposed ADR/spec system has not been established;
- overlapping documents do not identify a normative winner;
- eight documents are absent from the canonical index, including the module architecture and minimal-interface proposals;
- several appendices incorrectly state that source remains scaffolding-only;
- the root README and FAQ say there is no production code, which is technically defensible, but fail to mention the now-material prototype and integration test.

No broken relative links were found within the `docs/` corpus, which is a positive baseline.

### 10.2 Documents preserve conversational history instead of decisions

Many proposals begin with phrases such as “Below is…” or refer to “the framework we discussed.” They contain multiple generations of API sketches and appended “alignment updates.” This is useful as design-session history but costly as a source of truth.

**Required change:** preserve research notes, but extract accepted decisions and normative contracts into shorter documents. A reader should not need to infer which paragraph is newest.

### 10.3 Traceability overstates coverage

The [requirements traceability map](../plans/requirements-traceability-map.md) marks several requirements “Covered” when only proposal links and planned tests exist. Design coverage, prototype evidence, and validated coverage should be separate columns.

**Required change:** use readiness states such as `Proposed`, `Specified`, `Prototyped`, `Validated`, and `Product-Proven`. Do not let a planned validation artifact count as validation.

---

## 11. Security, privacy, and operational gaps

The safety discussion focuses primarily on preventing target-code side effects. A production dump evaluator also processes hostile and secret-bearing inputs inside the analysis process.

A threat model should cover:

- malicious/corrupt PE, metadata, PDB, SourceLink, and dump structures;
- parser/decompiler/model bugs causing host code execution, memory exhaustion, stack overflow, or long CPU use;
- symbol/source server egress that reveals module names, hashes, repository locations, or incident details;
- SourceLink credentials, redirects, untrusted URLs, cache poisoning, and source-hash validation;
- private symbol/source retention and cache lifecycle;
- plugins/model packs with arbitrary host capabilities;
- path traversal and target-path/local-path confusion;
- decompression bombs and huge graph/projection traversals;
- secrets in environment-variable snapshots, watches, telemetry, traces, and saved sessions;
- denial of service through path explosion, recursion, provenance DAG explosion, or model output volume.

**Recommended security posture:** run artifact parsing/decompilation and interpretation in a resource-limited worker process with network disabled by default; perform acquisition in a separate policy-controlled component; pass immutable verified blobs into the worker; cap every input/output dimension; redact telemetry by default; make model packs trusted code with explicit capability manifests.

The raw `IServiceProvider` model hook should not cross this boundary.

---

## 12. Recommended target architecture

The architecture should be centered on evidence and execution mode, not on a universal VM.

```text
Host / SDK / eventual IDE
        |
        v
Request + policy + explicit semantic mode
        |
        v
Evaluation orchestrator
   |             |                  |
   |             |                  +--> Explanation/trust synthesis
   |             +--> Expression front end / query binder
   +--> Evidence resolver
           |-- Runtime snapshot adapter (ClrMD)
           |-- Artifact acquisition + identity verification
           |-- Metadata/method-body projection
           |-- Symbols/source/debug-map projection
        |
        v
Canonical session evidence
   |-- program image and canonical identities
   |-- snapshot values with completeness/provenance
   |-- deterministic environment assumptions
        |
        +--> Snapshot query evaluator (first product)
        |
        +--> Counterfactual IL executor (second product slice)
        |       |-- concrete/hybrid domain
        |       |-- overlay memory
        |       |-- call/model policy
        |       +-- deterministic budgets/events
        |
        +--> Analysis engine (later, separate state/fixpoint contracts)
```

### 12.1 Canonical result envelope

Every host-visible operation should return something conceptually like:

```text
EvaluationResult = {
  SemanticMode: Observation | DerivedQuery | CounterfactualExecution | AbstractAnalysis,
  Status: Completed | Partial | Blocked | DecisionNeeded | BudgetExceeded | Unavailable | InvalidInput,
  ValueOrStates,
  Evidence: [source, identity, completeness],
  Assumptions,
  MayEffects,
  SimulatedEffects,
  Coverage,
  Diagnostics,
  DeterminismInputs,
  TraceRef?
}
```

Trust UI should be synthesized from these orthogonal fields rather than stored as a second, potentially contradictory truth.

### 12.2 Stable evidence/result types

Use a common typed outcome for adapter operations:

```text
EvidenceResult<T> =
  | Exact(T, evidence)
  | Partial(T, gaps, evidence)
  | Unavailable(reason, hints)
  | Conflict(expected, candidates, policyDecision)
  | Invalid(reason, evidence)
```

This directly realizes the design's strongest principle and avoids lossy `bool Try*` chains.

### 12.3 Keep products separate until sharing is proven

- The dump query evaluator may use no IL at all for much of Phase 1.
- The counterfactual stepper should use one concrete/hybrid state and explicit `DecisionNeeded`, not silently present abstract joins as a concrete debugger state.
- The abstract analyzer should have its own block state, ordering, bottom, convergence, and summary contracts.
- The sandbox runtime should be a future project or separate repository once concrete CLR-like behavior, verification, host ABI, and security become actual goals.

Shared code should include canonical types/identities, decoded IL, selected transfer kernels, diagnostics vocabulary, and tested intrinsic semantics where their contracts truly match.

---

## 13. Recommended development plan

### Stage 0 — Reset and specify

**Goal:** make the repository internally coherent before adding features.

Deliverables:

1. Accepted scope decision: dump query/evaluation is the first product; other applications are research horizons.
2. Truth/result model separating observation, derivation, counterfactual execution, and analysis.
3. Normative `MethodBody`, identity, evidence-result, budget, diagnostic, and execution-outcome specifications.
4. Target runtime/IL semantic profile.
5. Threat model and worker-isolation decision.
6. Pinned SDK/package baseline and quiet build.
7. Consolidated prototype solution.

Exit gate:

- one canonical definition for every boundary used by the next slice;
- no roadmap item depends on an unresolved meaning of “pure,” “exact,” “unknown,” or “partial.”

### Stage 1 — Factual dump query vertical slice

**Goal:** answer useful questions without executing user IL.

Deliverables:

- load full and partial dumps through a structured result API;
- capture runtime/module identity evidence;
- locate and verify a PE artifact independently of target path;
- enumerate a selected object and read instance/static fields, strings, and arrays with provenance and caps;
- constrained expression/query AST supporting names, field navigation, null propagation, literals, basic operators, arrays, and selected exact projections;
- host-neutral result envelope and text/JSON demo CLI.

Exit gate:

- representative real questions answered from at least Windows and Linux dumps;
- identity mismatch and missing artifact tests fail closed;
- every missing read reports a stable reason;
- no target module file is required to remain at its original path.

### Stage 2 — Minimal counterfactual IL evaluation

**Goal:** execute small, ordinary getters/helpers with explicit limits.

Deliverables:

- normalized full method bodies including locals and EH metadata;
- deterministic decoder and stack validation;
- scenario-derived opcodes for locals/args/constants/branches/fields/statics/arrays/conversions/type tests/byrefs/basic calls/returns;
- exact target-exception outcomes for supported operations;
- overlay memory and arena-qualified references;
- a concrete/hybrid domain, stable unknown IDs, deterministic budgets, and call fallback;
- a minimal exact intrinsic set for strings, arrays, and nullable values.

Exit gate:

- differential tests against CLR for the fully concrete supported subset;
- dump-backed getter evaluation reads at least one real object and returns a derived result;
- unsupported instructions never corrupt stack shape or silently mutate memory;
- return/call/EH behavior is covered beyond root `void ret`.

### Stage 3 — IL and source stepping

**Goal:** expose counterfactual execution interactively.

Sequence:

1. IL instruction stepping with calls/returns, decisions, exceptions, diffs, and undo.
2. Portable PDB occurrence-sensitive stepping.
3. Source acquisition and hash verification.
4. Decompiled mapping as explicitly approximate fallback.

Exit gate:

- loops stop on repeated sequence-point occurrences;
- Step Into/Over/Out work across calls, exceptions, and same-line multiple calls;
- replay transcript is stable for fixed evidence/policy/model versions;
- UX always labels the session counterfactual.

### Stage 4 — Models and precision expansion

Prioritize models from measured corpus cliffs. Likely first candidates are strings, nullable, arrays, enums, common collections, and compiler throw helpers. Introduce layout projections only with invariant checks and fixture coverage across supported runtime versions.

Do not permit mutation of a partially materialized projection unless the virtual value preserves an explicit “unknown remainder” and operations remain conservative.

### Stage 5 — Optional research tracks

Only after the dump product has evidence:

- async state-machine exploration for recognized task/awaiter shapes;
- dynamic call-site lifting for curated binder forms;
- normalized IR and abstract-analysis engine;
- speculative live snapshots;
- sandbox/no-JIT runtime feasibility study.

Each should have an independent product case and correctness contract.

---

## 14. Test plan corrections and next vertical tests

Retain the current test as `CanOpenSameMachineFullDumpAndDiscoverModuleByName`. Then add:

1. **Artifact identity positive test**
   - capture dump identity facts;
   - resolve a separately supplied artifact;
   - prove exact match before reading IL.

2. **Artifact mismatch negative test**
   - present a same-name/different-MVID assembly;
   - assert execution is blocked with an identity-conflict result.

3. **Missing original path test**
   - move/delete the target build artifact after dump capture;
   - resolve from an explicit artifact directory/cache or return `Unavailable` cleanly.

4. **Dump-backed field query test**
   - keep a rooted object with known nested/string/array values;
   - find it using a deterministic root/handle;
   - read and derive a result through the product API.

5. **Partial memory test**
   - use a smaller dump or a controlled unreadable reference;
   - assert partial result and exact miss reason.

6. **Real call/return test**
   - execute `ldarg`, arithmetic, a direct call, non-void `ret`, and caller resume;
   - compare against CLR.

7. **EH test**
   - supported throw/catch/finally path plus unsupported/missing exception evidence.

8. **Stepping occurrence test**
   - a loop whose body maps to the same sequence point on each iteration;
   - assert each occurrence can stop.

9. **Deterministic identity test**
   - run in separate processes and vary lookup order;
   - assert canonical IDs and transcript hashes remain stable.

10. **Hostile artifact budget test**
    - malformed metadata, huge PDB/source, cyclic/corrupt object graph;
    - assert resource-bounded structured failure.

---

## 15. Prioritized action register

### P0 — before expanding implementation

1. Adopt the four-mode truth/result model and correct causal product claims.
2. Choose the first product: read-only dump query/evaluation.
3. Replace the current roadmap with vertical slices.
4. Select normative docs and mark competing sketches historical/non-normative.
5. Specify structured evidence results, canonical identities, full method bodies, and deterministic budgets.
6. Design the expression front end.
7. Add a threat model and decide worker-process isolation.
8. Pin SDK/package inputs and enforce a quiet build.

### P1 — first implementation cycle

1. Consolidate the 42-project scaffold.
2. Redesign module/method/generic/PDB identities.
3. Replace boolean adapter results at critical boundaries.
4. Build artifact identity verification and remove target-path trust.
5. Add the dump-backed field-query vertical test.
6. Correct `ret`, event, stop-reason, and process-timeout behavior.
7. Define one exclusive sequence-point range convention and occurrence-sensitive stepping model.
8. Re-run SRM versus AsmResolver evidence before accepting a backend ADR.

### P2 — after first useful result

1. Expand opcode support from compiled scenarios.
2. Add overlay memory and exact core intrinsics.
3. Add IL stepping, then Portable PDB stepping.
4. Establish corpus-based coverage and performance baselines.
5. Introduce projections based on measured user value.

### Deferred

- general async virtualization;
- broad DLR emulation;
- abstract analysis as a public product;
- live speculative debugging;
- no-JIT sandbox runtime;
- generalized binary plugin ABI.

---

## 16. Plausibility by declared capability

| Capability | Plausibility | Conditions |
|---|---|---|
| Read-only field/array/string navigation | High | Robust identity, typed snapshot values, bounded reads, constrained front end |
| Basic derived expressions | High | Clear exact-operation subset and target numeric/runtime profile |
| Common BCL projections | Medium–High | Versioned invariants, explicit completeness, no false exactness |
| Selected getter/helper IL execution | Medium | Broader core IL/EH/byref/static support, exact exceptions, artifact verification |
| Dump-frame locals/arguments | Medium–Low | Architecture/platform-specific recovery; must degrade gracefully |
| IL-level virtual stepping | Medium–High | Correct micro-step semantics, events, budgets, persistent state |
| Source-level stepping with PDB | Medium | Occurrence semantics, symbols/source integrity, optimized-code UX |
| Decompiled source stepping | Medium–Low | Approximate UX only; mapping instability across ILSpy versions |
| Generic context recovery | Medium–Low | Multiple evidence sources, runtime-version adapters, partial result model |
| Async virtual tasks | Medium for curated shapes; low for general fidelity | Explicit synthetic scheduling and external-task assumptions |
| Dynamic dispatch lifting | Medium for common binder shapes; low for general fidelity | Versioned recognizer, candidate evidence, explicit unresolved/meta-object outcomes |
| Whole-method abstract interpretation | Medium as a separate engine | Formal domain/state contracts, EH CFG, soundness testing |
| Shared engine for serious static analysis and interactive stepping | Medium for opcode kernels; low for one unified runtime API | Separate orchestration/state semantics |
| No-JIT/sandbox runtime | Low as an extension of the MVP | Concrete completeness, verification, host ABI, performance, separate security case |
| 70% of common questions in MVP | Unknown | Requires corpus and denominator |

---

## 17. Final conclusion

This project has a defensible and potentially distinctive core: safe, bounded, explainable computation over dump evidence. The design team has correctly identified most of the hard technical terrain and has produced useful research notes on it.

The current risk is not that the idea is impossible. It is that the project is trying to capitalize every possible future use of the interpreter before demonstrating one trustworthy user answer. That has inverted sequencing, multiplied contracts, and blurred the difference between facts, simulations, and abstract possibilities.

The best path is ambitious in depth rather than breadth:

1. make evidence and result meaning rigorous;
2. answer a small set of real dump questions end to end;
3. add selected IL execution with differential evidence;
4. expose counterfactual stepping honestly;
5. grow models and additional products only from observed pressure.

With that reset, the declared dump-time evaluation goal is plausible. Without it, the all-products platform plan is likely to spend substantial effort validating package seams and sophisticated semantic lifts before establishing correctness, demand, or a stable semantic center.
