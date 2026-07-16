# Call Model and Effects Contract (Draft)

> **Roadmap status: supporting research plus one implemented W4 profile.** W4.5 executes one closed interpreted direct-
> MethodDef call shape. Exact W4.6a commit `77c92789b16d9258c907d5026a36e39f8c957b41` freezes one narrower
> structural pure-model admission profile, exact W4.6b commit `fd723a912` freezes its modeled-return lineage/domain
> contract, exact W4.6c commit `877c9fb55` makes that frozen capability executable, and exact W4.6d commit
> `da5346813` closes compiler/SRM conformance. The broad taxonomy, effect lattice, fallbacks, intrinsic catalog,
> dynamic/async lifting, and host result vocabulary below remain research unless the current-profile section explicitly
> says otherwise.

## Current implemented W4.6 profile

The normative W4 contract, not this document's broad research taxonomy, controls the active slice. Its model boundary
is deliberately closed:

- `PureCallModelVersion` has exactly three numeric components, each in `0..65535`. `PureCallModelIdentity` adds a
  canonical lowercase ASCII stable ID of at most 128 characters, with alphanumeric segments separated singly by
  `.`, `-`, or `_`. Payload-omitting failure codes are at most 128 ASCII characters in the `W4.Model.*` family.
- `PureCallModelDescriptor` binds the identity/version to one exact body-independent structural target, confidence,
  and normalized effects. The vocabulary can represent `Exact`, `BestEffort`, `Partial`, and `UnsupportedLayout`, but
  the W4.6a planner admits only `Exact`. Descriptors may retain effects `None` or `Unsupported`; preparation admits only
  `None`. `VirtualOnly` and `Modeled` are rejected at descriptor construction.
- The non-generic `PureCallModelInvocation`/outcome contract carries exactly two metadata-ordered structural `Int32`
  atoms. Each atom is exact or explained unknown but carries no lineage. The envelope has no memory, dump, session,
  ambient context, target delegate, or display text. Outcomes are exact return, unknown return without lineage, or
  blocked/invalid with a stable code. `IPureCallModel`, `IPureCallModelRegistry`, and structural selection results form
  the remaining capability boundary.
- Default `MethodGraphPlanner.Prepare` remains interpreted-only. Explicit `RequirePureModel(root, target, registry)`
  resolves and types the caller edge, then performs model selection before acquiring the prospective target body. A
  selected target becomes one body-free opaque `FrozenPureModelLeaf`; repeated edges deduplicate selection and the
  leaf, while each call edge remains retained and charged. The graph exposes `Interpreted`/`PureModel` dispositions,
  canonical `ModeledLeaves`, and `TryGetModeledLeaf`.
- Only exact target, exact confidence, and no effects succeed. Missing, blocked, invalid, throwing, mismatched, non-
  exact, or unsupported-effect selection cannot fall back to target-body interpretation and never exposes a partial
  plan. Runtime capability object identity is excluded from structural graph equality/hashing; the legacy interpreted
  call-site hash is frozen.
- Traversal counts interpreted nodes, modeled leaves, fields, and edges; modeled depth is one logical boundary. The
  real compiler graph is one interpreted root plus one modeled leaf, two fields, and one edge: five units and required
  depth two. Its deterministic PDB-free target PE SHA-256 is
  `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`.
- W4.6c activation dispatches only the already-frozen call disposition and invokes only the capability retained by the
  prepared modeled leaf. It never re-queries the registry or resolver, reselects a descriptor, reads the target body,
  or falls back to interpretation. A modeled call pushes no callee frame and emits no frame event.
- W4.6b adds optional `IPureCallModelLineageDomain<TValue>` and append-only schema-v1 kind-6
  `ModeledReturnTransform`. It embeds exact operands, wraps explained operands with unchanged kind-4 call nodes,
  prevalidates and interns the complete acyclic batch atomically, and validates structural replay plus fresh-domain
  continuation. Kinds 1–5 retain their exact bytes and identities.
- W4.6c transfers exact or lineage-grounded unknown model returns atomically into the caller. One modeled call consumes
  one instruction event, leaves memory unchanged, and advances logical depth without changing active-frame depth.
  Budget rejection occurs before capability entry. A capability that blocks, fails, throws, or returns a malformed
  outcome transfers no semantic state and receives no fallback.
- Every actual capability entry appends one immutable `PureModelAttempt` with frozen callsite/model identity, entered
  logical depth, outcome, transfer status, and stable payload-omitting code. Operational counters distinguish invocations
  from completed modeled calls; attempt chronology and logical-depth high-water witnesses are validated on resume.
  The stable taxonomy separates capability failure, invalid/malformed outcome, lineage unavailability/invalidity,
  and forged attempt invariants. Exact terminal activation additionally preserves the completed depth witness.
- W4.6d binds this same profile through the real compiler and SRM path. It directly proves interpreted/model/CoreCLR
  agreement for exact evidence and interpreted/model agreement for both partial/unavailable shapes. The mixed case
  freezes canonical graph SHA-256 `451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`;
  repeated and fresh metadata-reader/domain/machine runs reproduce the both-unknown graph SHA-256
  `31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`.

W4.6a exact-checkpoint headless evidence passed locked restore; a strict fifteen-project Release build at zero
warnings/errors; unit 371/371; fast 77/77; ordinary dump 5/5; optimized dump 1/1; pure-model contracts 49/49; model
planner 25/25; legacy planner 35/35; compiler 1/1; lineage 2/2; both guards; and zero skips with
the milestone test selection. It realizes 2,959 added LOC (1,210 production plus 1,749 tests/fixture support), bringing
W4.1–W4.6a to 19,776 LOC. W4.6b strict headless builds passed at zero warnings/errors; focused 8/8, combined legacy-
plus-modeled lineage 44/44, and integration call-lineage 2/2 passed with zero skips and the milestone test selection. It
realizes 1,003 added LOC (481 production plus 522 tests), with 23 deletions, bringing W4.1–W4.6b to 20,779 LOC.

W4.6c exact commit `877c9fb55` realizes 2,734 added LOC (1,425 production plus 1,309 tests). Strict affected builds
passed at zero warnings/errors and its focused machine lane passed 34/34. W4.6d exact commit `da5346813` realizes
956 test LOC. Its compiler/SRM lane passed 3/3, the aggregate W4 integration lane passed 13/13, and the Fast lane
passed 80/80. Every behavioral invocation used `eng/Invoke-HeadlessProcess.ps1`, included
the milestone test selection, and recorded zero skips. W4.6 realizes 7,652 LOC in total and brings W4.1–W4.6 to
24,469 LOC.

Historical full-W4 projections remain original 16,860–25,310; post-W4.2 18,532–26,132; post-W4.3
19,228–25,728; post-W4.4 21,179–26,779; post-W4.5a 24,013–29,313; W4.5 closure 25,017–29,417; design audit
27,217–32,117; W4.6a checkpoint 28,376–32,476; first W4.6b recalibration 28,876–33,276; post-split
28,826–33,726; post-W4.6b checkpoint 28,879–33,279; and pre-W4.6c/d closure 30,079–33,729 LOC. W4.6c/d
realized 3,690 LOC against their 3,400–3,750 estimate, and W4.6 as a whole realizes 7,652 LOC. W4.7 subsequently
realized 2,801 LOC, W4.8 11,924 LOC, and W4.9 2,698 LOC, bringing full W4 implementation to 41,892 LOC. Exact
exact hosted closure passed in run 29463426083; all earlier projections remain historical calibration.

## Purpose

Beyond the implemented profile above, this document defines a conceptual research contract for call handling in the IL interpreter, with emphasis on deterministic behavior, bounded execution, and explainable approximations.

It covers:

- how calls are classified and dispatched,
- how side effects are represented,
- how unknownness is introduced and propagated at call boundaries, and
- how hosts can understand result and precision impact.

---

## 1) Design goals

1. **Conservative execution:** never execute arbitrary target code in dump-time scenarios.
2. **Determinism:** call outcomes must be reproducible under the same policy and inputs.
3. **Explainability:** approximations must emit structured diagnostics.
4. **Composable precision:** richer models should improve results without changing core contracts.
5. **Bounded cost:** call reasoning must honor the global budget model.

6. **Semantic lifting for compiler/runtime patterns:** dynamic call sites and async state-machine runtime plumbing should be recognized as lifted semantic operations before fallback policy is considered.

---

## 2) Call classification

The following is a future classification taxonomy, not the current W4 dispatcher. Current W4 graphs distinguish only
interpreted edges and the one required executable exact pure-model leaf described above. A later broad dispatcher may
classify calls into:

1. **PureIntrinsic**
   - Predefined semantic model exists for target member and is side-effect free.
   - Preferred for high-value BCL APIs and runtime primitives with stable contracts.

2. **EnvironmentIntrinsic**
   - Model reads session/environment-derived information (time, process identity, machine/environment fields).
   - Must return either deterministic session-stable values or explicit unknowns tagged with environment provenance.

3. **ProjectionIntrinsic**
   - Model answers by reading projected heap/object layouts (for example collection count/lookup operations).
   - Must be version-aware, bounded, and emit decoder identity + confidence labels.

4. **PatternIntrinsic**
   - Multi-instruction/runtime idioms are lifted into single modeled operations (`lock`, `foreach`, throw-helper guards, interpolation handlers).
   - Must preserve observable control-flow outcomes while suppressing framework plumbing noise.

5. **Summary-modeled**
   - A reusable summary describes input/output and effects.
   - Used for scalable precision where full modeling is unnecessary.

6. **Interpreter-reentrant**
   - Admitted and permitted to recursively interpret callee IL.
   - Subject to recursion depth and per-call budget limits.

7. **Lifted semantic callsite**
   - Compiler/runtime patterns are recognized and lifted into interpreter-owned operations.
   - Initial required lifted kinds: `DynamicDispatch` (DLR call-site rewrite) and `AsyncRuntimeIntrinsic` (builder/awaiter/task-runtime semantics).

8. **Fallback**
   - No precise model is available or policy disallows execution.
   - Must return an explainable approximation (`unknown return`, `block`, or `havoc`).

Classification output is a first-class trace event so hosts can explain why a given path became less precise.

For lifted callsites, classification metadata must include the lifted kind and site identity:

- `DynamicDispatch`: binder site ID, member name, static receiver context, and argument binding-type vector.
- `AsyncRuntimeIntrinsic`: async method descriptor ID, await-point ID (if any), and continuation/action identity.

All modeled classifications (`PureIntrinsic`, `EnvironmentIntrinsic`, `ProjectionIntrinsic`, `PatternIntrinsic`, `Summary-modeled`) must emit a confidence label:

- `Exact`
- `BestEffort`
- `Partial`
- `UnsupportedLayout`

Confidence labels are mandatory for host result synthesis and regression assertions.

---

## 3) Dispatch contract

This section is a future generalized envelope. It is not implemented by the narrow W4.6 profile, whose non-generic
model boundary has no memory/ambient inputs and whose frozen exact/no-effect capability has no fallback.

### 3.1 Inputs

Call dispatch receives:

- target method identity (stable metadata identity),
- receiver/argument abstract values,
- current abstract memory state,
- policy profile (`fast`, `balanced`, `deep`),
- remaining execution budget,
- ambient context (timeout token, host capabilities, dump-mode flags).

### 3.2 Outputs

Dispatch returns a `CallOutcome` envelope with:

- `ReturnValue` (concrete, abstract, or unknown),
- `UpdatedState` (memory/effect-applied state),
- `EffectSummary` (normalized effect set),
- `Diagnostics` (zero or more approximation/blocked reasons),
- `Confidence` (`Exact`, `BestEffort`, `Partial`, `UnsupportedLayout`),
- `ResultLabel` (`exact`, `approximate`, `blocked`, `timed_out`).

### 3.3 Required invariants

- Output must be deterministic for equal inputs and policy.
- Budget consumption must be monotonic and observable.
- Any precision loss must emit a diagnostic with provenance.
- Calls marked `pure` must not mutate abstract memory.

---

## 4) Effect model

Effects are represented as a small lattice intended to balance precision and practicality.

## 4.1 Effect dimensions

A call can advertise any combination of:

- `reads_memory`
- `writes_memory`
- `allocates`
- `throws`
- `impure_external` (depends on non-modeled ambient state)

## 4.2 Ordering intuition

- Lower elements represent weaker/more precise claims (fewer effects).
- Higher elements represent conservative uncertainty (more possible effects).
- Join operation is set union across dimensions.

This structure allows stable fixpoint behavior while still exposing actionable effect signals to hosts.

## 4.3 Purity policy

A method can be treated as **pure** only when all enabled models agree:

- no writes to reachable state,
- no externally visible side effects,
- deterministic return relation under modeled inputs.

If purity cannot be proven under active policy, downgrade to impure/unknown and emit diagnostic metadata.

---

## 5) Fallback behaviors

The following fallbacks are research candidates. The implemented W4.6a profile has exactly one fallback rule: none.
Any failed or inadmissible required-model selection rejects preparation without acquiring/interpreting the target body.
A future policy may instead select one of three strategies:

1. **Block**
   - Stop evaluating this path and produce `blocked` result summary label.
   - Recommended for strictly constrained modes.

2. **Unknown return**
   - Continue with unknown return value and conservative effect join.
   - Preserves progress while surfacing reduced confidence.

3. **Havoc**
   - Aggressively widen reachable memory and return value.
   - Reserved for deep exploratory analysis where continuation is preferred over precision.

Fallback choice must be explicit in diagnostics and trace output.

---

## 6) Unknown provenance at call boundaries

Whenever unknownness is introduced due to call handling, provenance must record:

- callsite IL offset,
- target method identity,
- classification result,
- fallback strategy (if any),
- blocked/approximation reason code,
- policy profile in effect.

This enables host experiences to answer "why is this value unknown?" without reverse-engineering trace internals.

---

## 7) Future intrinsic scope (research recommendation)

Prioritize intrinsically modeling methods that are:

1. common in watch/immediate expressions,
2. high fan-out in benchmark corpora, and
3. semantically simple and stable across runtime versions.

Suggested starting set:

- `System.String` length/indexing basics,
- `System.Array` length and rank accessors,
- simple `Nullable<T>` accessors,
- selected arithmetic and comparison helpers.

All intrinsics should include negative tests for unsupported argument/state shapes.

---

## 8) Lifted callsite diagnostics (required)

To keep host UX and replay deterministic, lifted callsites must emit standardized outcome diagnostics.

### 8.1 Dynamic dispatch outcomes

- `Resolved` (single best target selected)
- `Ambiguous` (multiple plausible targets)
- `Unresolved` (insufficient runtime type/value evidence)
- `MetaObjectRequired` (IDynamicMetaObjectProvider/COM path not modeled)

### 8.2 Async runtime outcomes

- `AwaitPending` (state machine suspended; continuation scheduled)
- `ContinuationResumed` (scheduled continuation dequeued and executed)
- `TaskCompleted`
- `TaskFaulted`
- `TaskCanceled`

All outcomes must carry provenance linking back to callsite IL offset, lifted site identity, and active policy profile.

---

## 9) Open questions

1. Should summary models be serialized as source-controlled JSON, C# descriptors, or both?
2. How should versioning work when runtime behavior diverges across framework versions?
3. What minimum evidence is required before promoting a fallback-heavy method to intrinsic status?
4. Should host policy be allowed to ban `havoc` entirely in dump-mode scenarios?

---

## 10) Milestone alignment

- **W4.6a (implemented):** structural exact/no-effect selection, opaque modeled leaves, and fail-closed activation.
- **W4.6b (implemented):** atomic modeled-return lineage/domain vocabulary with schema-v1 compatibility.
- **W4.6c (implemented):** invoke the selected model, transfer exact/unknown results, and prove atomic attempts, depth,
  charging, event, and unit conformance without broadening the active profile.
- **W4.6d (implemented):** prove compiler/SRM exact, degraded, and fresh-session execution conformance.
- **Later research:** generalized fallback diagnostics, effect joins/fixpoint integration, intrinsic catalog and summary
  governance, and host result/UX tuning using dump-backed validation data.
