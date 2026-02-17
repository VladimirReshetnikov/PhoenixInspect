# Call Model and Effects Contract (Draft)

## Purpose

This document defines the conceptual contract for call handling in the IL interpreter, with emphasis on deterministic behavior, bounded execution, and explainable approximations.

It covers:

- how calls are classified and dispatched,
- how side effects are represented,
- how unknownness is introduced and propagated at call boundaries, and
- how hosts can understand trust and precision impact.

---

## 1) Design goals

1. **Safety first:** never execute arbitrary target code in dump-time scenarios.
2. **Determinism:** call outcomes must be reproducible under the same policy and inputs.
3. **Explainability:** approximations must emit structured diagnostics.
4. **Composable precision:** richer models should improve results without changing core contracts.
5. **Bounded cost:** call reasoning must honor the global budget model.

6. **Semantic lifting for compiler/runtime patterns:** dynamic call sites and async state-machine runtime plumbing should be recognized as lifted semantic operations before fallback policy is considered.

---

## 2) Call classification

Every call instruction is classified into one of the following categories before evaluation:

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
   - Safe and permitted to recursively interpret callee IL.
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

Confidence labels are mandatory for host trust synthesis and regression assertions.

---

## 3) Dispatch contract

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
- `TrustLabel` (`trusted`, `approximate`, `blocked`, `timed_out`).

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

When a call cannot be modeled precisely, one of three fallback strategies is selected by policy:

1. **Block**
   - Stop evaluating this path and produce `blocked` trust label.
   - Recommended for strict safety modes.

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

## 7) Initial intrinsic scope (MVP recommendation)

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

- **M1:** implement baseline fallback contract + diagnostics.
- **M2:** integrate effect joins with fixpoint engine.
- **M3:** add intrinsic catalog and summary format governance.
- **M4+:** tune trust labels and UX messaging using dump-backed validation data.
