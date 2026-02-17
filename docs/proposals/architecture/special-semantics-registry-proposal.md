# Special Semantics Registry Proposal

## 1) Purpose

The semantic-modeling proposal introduced three complementary mechanisms that must behave as one cohesive capability:

1. call intrinsics,
2. IL-pattern rewrites, and
3. object projections.

This document defines a single registry contract and resolution model so these mechanisms do not drift into incompatible extension points with different diagnostics, precedence, or trust semantics.

---

## 2) Design goals

1. **One modeled-outcome envelope** across call, pattern, and projection paths.
2. **Deterministic lookup and conflict resolution** for reproducible replay.
3. **Fail-closed behavior** when projection/layout assumptions do not hold.
4. **Policy-controlled extensibility** for host- and package-provided model packs.
5. **Versioned compatibility surface** that can evolve without silent behavior changes.

---

## 3) Registry surface (conceptual)

```csharp
public interface ISpecialSemanticsRegistry
{
    bool TryRewriteMethod(MethodKey method, MethodBodyInfo body, out RewrittenBody rewritten, out ModeledOutcome outcome);

    bool TryModelCall(CallContext context, ref MachineState state, out Value result, out ModeledOutcome outcome);

    bool TryProjectObject(ObjectRef reference, ProjectionRequest request, out IObjectProjection projection, out ModeledOutcome outcome);
}
```

`ModeledOutcome` is shared and required for all successful modeled paths.

---

## 4) Modeled outcome envelope (required)

Every modeled operation must emit:

- `Category`: `PureIntrinsic | EnvironmentIntrinsic | ProjectionIntrinsic | PatternIntrinsic | LiftedSemanticSite | SummaryModeled`
- `Confidence`: `Exact | BestEffort | Partial | UnsupportedLayout`
- `ReasonCode`: stable machine-readable code (`Env_Time`, `UnsupportedLayout_ConcurrentDictionary`, etc.)
- `DecoderIdentity` (projection paths only): decoder name/version/runtime-range
- `Assumptions`: explicit assumptions used by the model
- `Diagnostics`: user-displayable explanation payload

This envelope is a compatibility contract: hosts should render one UX shape regardless of which modeling path produced the result.

---

## 5) Resolution order and conflict rules

To avoid ambiguous behavior, resolution order is fixed:

1. **Pattern rewrite phase** (method-level pre-pass)
2. **Call modeling phase** (per-call dispatch)
3. **Projection lookup phase** (heap/object inspection)
4. Fallback call policy (`Interpret | Model summary | Stop | Unknown+Havoc`) when no modeled rule applies

Conflict rules:

- More specific match wins over generic match (exact member signature > namespace/type wildcard).
- Host policy pack overrides built-in pack when both have equal specificity.
- Ties at identical scope are deterministic by configured pack order.
- If a selected projection decoder fails invariants, outcome must be `UnsupportedLayout`; no fallback to weaker decoder unless policy explicitly opts in.

---

## 6) Extensibility and versioning

### 6.1 Model packs

Registry entries are loaded as ordered packs:

- `builtin-core` (required)
- `builtin-optional` (runtime/version-specific)
- `host-custom` (integration-specific)

Each pack must declare:

- semantic contract version,
- supported runtime families/versions,
- declared categories it contributes,
- deterministic ordering key.

### 6.2 Compatibility policy

- Adding a new modeled rule with no overlap is a minor-version change.
- Changing precedence or reason-code meanings is a major-version change.
- Removing reason codes or confidence states is breaking and requires migration notes.

---

## 7) Determinism and replay requirements

The registry must be replay-stable given:

- identical assembly/method inputs,
- identical `SessionSnapshot`,
- identical model-pack set and ordering,
- identical policy preset.

Replay artifacts must include the selected rule identity (`PackId`, `RuleId`, `Version`) for each modeled step.

---

## 8) Minimum test obligations

1. **Cross-path envelope parity tests**: call/pattern/projection each emit a complete `ModeledOutcome` shape.
2. **Conflict-resolution determinism tests**: overlapping rules resolve identically across runs.
3. **Unsupported-layout tests**: invariant failures produce `UnsupportedLayout` and explicit diagnostics.
4. **Replay identity tests**: transcript includes rule identity and remains stable.

---

## 9) Near-term integration updates required

1. Link this contract from:
   - `architecture-overview-proposal.md`,
   - `call-model-and-effects.md`,
   - `semantic-modeling-proposal.md`.
2. Add integration guidance for host pack loading and policy override boundaries.
3. Add traceability row updates to mark the special-semantics unification requirement as covered at design level.

---

## 10) Non-goals (for this phase)

- Defining every concrete intrinsic/projection implementation.
- Locking binary plugin format.
- Finalizing host UI rendering for modeled outcomes.

These remain follow-up design tasks after the registry contract is accepted.
