# Adapter conformance checklist (draft)

This checklist defines minimum behavioral expectations for runtime, metadata, and symbol adapters used by the project.

It complements architecture proposals by making backend-neutral behavior testable during the documentation-first phase.

## Goals

- enforce deterministic, explainable adapter behavior,
- prevent backend-specific exceptions and object models from leaking into core contracts,
- enable cross-backend comparison using the same conformance scenarios.

## Contract-shape checklist

Adapters SHOULD satisfy the following contract-shape rules:

1. **Project-owned types at boundaries**
   - adapter outputs are immutable project records/interfaces,
   - no public exposure of ClrMD/AsmResolver/dnlib/Roslyn-specific types.
2. **Explicit result categories**
   - success, partial, and unavailable outcomes are first-class result states,
   - errors include normalized reason codes.
3. **Stable identity model**
   - module, type, and method identity surfaces are deterministic and serialization-safe.
4. **Budget-aware operations**
   - expensive lookup/materialization calls accept budget and cancellation context.

## Behavioral checklist

### Runtime snapshot adapter behavior

- Enumerating threads/frames must preserve deterministic ordering rules.
- Missing memory or truncated dump regions must produce explicit miss reasons.
- Object/value reads must include provenance tags indicating runtime-origin confidence.

### Metadata adapter behavior

- Method-body materialization must include:
  - instruction list,
  - exception handling regions,
  - locals/signature metadata,
  - flags describing incomplete decoding.
- Generic context reconstruction must surface unresolved parameters explicitly.

### Symbol/debug-map behavior

- Sequence-point mapping must distinguish:
  - exact source mapping,
  - best-effort mapping,
  - no mapping available.
- Symbol lookup failures must not throw across interpreter boundary contracts.

## Miss-reason taxonomy (initial)

Use a normalized miss-reason set so hosts can explain partial outcomes consistently.

| Reason code | Typical source | Host-facing meaning |
|---|---|---|
| `NotAvailable` | Data absent in dump/artifacts | Required data is not present. |
| `SymbolMissing` | PDB unavailable/unresolvable | Source mapping cannot be established. |
| `Ambiguous` | Multiple plausible matches | Result is unsafe to pick deterministically. |
| `UnsupportedShape` | Unsupported metadata/IL construct | Pattern recognized but not handled in current scope. |
| `BudgetExceeded` | Budget/time bound reached | Analysis was intentionally bounded. |
| `CorruptData` | Invalid or inconsistent artifacts | Input appears malformed or incompatible. |

## Conformance scenario skeleton

Each adapter implementation should be evaluated against a shared scenario set:

1. **Happy path:** full dump + matching symbols.
2. **Symbol-poor path:** full dump + missing symbols.
3. **Truncated dump path:** partial runtime memory availability.
4. **Generic-heavy path:** nested generic methods/types requiring context reconstruction.
5. **Ambiguity path:** duplicate/ambiguous identity candidates.

For each scenario, document:

- normalized output category,
- emitted miss reasons/provenance,
- determinism notes (ordering, tie-breaking, stable IDs).

## Exit criteria for “adapter-ready” label

An adapter path may be considered “adapter-ready” for prototype integration when:

1. contract-shape checklist items are all satisfied,
2. miss-reason taxonomy is implemented without backend leakage,
3. all conformance scenarios have documented outcomes,
4. at least one cross-backend scenario comparison exists for semantic parity.
