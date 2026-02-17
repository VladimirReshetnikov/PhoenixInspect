# Backend capability evidence log (draft)

This log records concrete evidence for the capability axes in `backend-capability-matrix.md`.

It is intentionally lightweight in the conceptual phase: the goal is to make claims traceable, not to freeze implementation decisions.

## How to use this log

For each experiment/prototype spike:

1. add one row under the relevant backend section,
2. link to artifact(s) such as notes, scripts, or benchmark output,
3. classify confidence as `Low`, `Medium`, or `High`,
4. record open follow-ups required before raising confidence.

## Evidence quality rubric

| Confidence | Meaning | Typical proof quality |
|---|---|---|
| Low | Directional hypothesis only. | Design reasoning, unvalidated assumptions, or single anecdotal read-through. |
| Medium | Reproducible in a narrow scenario. | One or more prototype runs with captured output and explicit constraints. |
| High | Reproducible across representative scenarios. | Repeated runs, edge-case coverage, and cross-backend comparison where applicable. |

## AsmResolver evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| TBD | Metadata completeness | Can decode representative type/member metadata required by interpreter identity layer. | TBD | Low | Add prototype decoding transcript for generic-heavy assemblies. |
| TBD | IL body fidelity | Can provide instruction stream + EH regions for candidate MVP methods. | TBD | Low | Validate malformed-body handling and incomplete decode signaling. |
| TBD | Portable PDB consumption | Baseline sequence-point extraction appears viable for C# paths. | TBD | Low | Confirm mapping behavior for async/iterator state-machine methods. |

## dnlib evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| TBD | Metadata completeness | Suitable as fallback cross-check for method/type identity reconstruction. | TBD | Low | Compare identity normalization behavior against AsmResolver. |
| TBD | Generic signature handling | Generic shape coverage appears broad but normalization rules need project-owned mapping. | TBD | Low | Add explicit nested-generic stress scenario and output diff. |

## SRM-oriented layer evidence

| Date | Capability axis | Claim | Evidence artifact | Confidence | Follow-up |
|---|---|---|---|---|---|
| TBD | Portable PDB consumption | Strong candidate for symbol-first scenarios with project-owned mapping layer. | TBD | Low | Validate end-to-end path including debug-map normalization contract. |
| TBD | Explainability mapping | Could simplify deterministic miss-reason mapping due to narrow surface area. | TBD | Low | Prototype failure-mode taxonomy under symbol-missing and corrupt-artifact cases. |

## Cross-backend conformance evidence

| Date | Scenario | Backends compared | Expected normalized parity | Evidence artifact | Status |
|---|---|---|---|---|---|
| TBD | Generic-heavy method body with partial symbols | AsmResolver vs dnlib | Same result category + same miss-reason family | TBD | Planned |
| TBD | Portable PDB sequence-point mapping with async method | AsmResolver vs SRM-oriented layer | Equivalent statement boundary semantics in debug-map contract | TBD | Planned |

## Open decisions linked to this log

- When a capability axis reaches `Medium` confidence for a primary candidate, update `backend-capability-matrix.md` notes and rationale.
- When critical MVP axes reach consistent `Medium`/`High` confidence, capture a decision record in `mvp-backend-decision-record.md`.
