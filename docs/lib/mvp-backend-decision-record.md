# MVP metadata/symbol backend decision record (draft)

This document captures the eventual MVP decision for metadata and symbol backends.

Until the decision is made, keep this record in a pre-decision state and update it incrementally as evidence accumulates.

## Decision status

- **Status:** Pending
- **Decision owner(s):** TBD
- **Target milestone:** TBD
- **Last updated:** TBD

## Decision statement template

> We select `<Primary backend>` as the primary metadata/IL/symbol backend for MVP, with `<Fallback backend>` as fallback/cross-check for defined scenarios.

## Context summary

Summarize the forces that shaped this decision:

- deterministic execution and explainability constraints,
- dump variability and artifact incompleteness,
- adapter contract stability and backend leakage risks,
- performance and maintainability trade-offs.

## Option set considered

| Option | Description | Included in final decision |
|---|---|---|
| A | AsmResolver-primary, dnlib fallback, SRM-oriented symbol bridge where needed. | TBD |
| B | dnlib-primary, AsmResolver fallback, project-owned symbol normalization. | TBD |
| C | SRM-oriented custom primary with selective library augmentation for IL-heavy paths. | TBD |
| D | Hybrid split: metadata/IL from one backend, symbols from another via strict adapters. | TBD |

## Capability gate summary

Populate this table from `backend-capability-matrix.md` and `backend-evidence-log.md` before final sign-off.

| Gate | Required evidence threshold | Result | Notes |
|---|---|---|---|
| Metadata completeness for MVP corpus | Medium confidence minimum | TBD |  |
| IL body fidelity (instruction + EH + locals) | Medium confidence minimum | TBD |  |
| Generic signature/context behavior | Medium confidence minimum | TBD |  |
| Portable PDB baseline mapping | Medium confidence minimum | TBD |  |
| Miss-reason normalization compatibility | Medium confidence minimum | TBD |  |
| Deterministic budget behavior | Medium confidence minimum | TBD |  |

## Rationale

Document the final why:

1. why the selected primary backend best fits MVP constraints,
2. why non-selected options were not chosen now,
3. why fallback strategy is sufficient for risk containment.

## Consequences

### Positive

- TBD

### Negative / trade-offs

- TBD

### Risk mitigations

- TBD

## Follow-up actions

| Action | Owner | Target milestone | Tracking link |
|---|---|---|---|
| Add/complete cross-backend conformance scenario set. | TBD | TBD | TBD |
| Lock adapter conformance checklist into prototype quality gate. | TBD | TBD | TBD |
| Update architecture/integration proposals with selected backend stance. | TBD | TBD | TBD |

## Change log

| Date | Change | Author |
|---|---|---|
| TBD | Initial decision-record scaffold created. | TBD |
