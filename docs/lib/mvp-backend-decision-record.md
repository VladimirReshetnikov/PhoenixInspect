# MVP metadata/symbol backend decision record

This document records the selected MVP primary backend strategy for metadata/IL/symbol ingestion.

## Decision status

- **Status:** Decided (provisional, design-phase)
- **Decision owner(s):** Architecture working group (design docs maintainers)
- **Target milestone:** MVP prototype baseline
- **Decision date:** 2026-02-17
- **Last updated:** 2026-02-17

## Decision statement

> We select **AsmResolver** as the **primary metadata/IL/symbol backend** for MVP prototyping, with **dnlib** as the **fallback and cross-check backend** for parity validation and Windows PDB contingency scenarios.

## Context summary

The decision is driven by the current design-phase constraints:

- deterministic and explainable execution behavior is mandatory,
- adapter contracts must stay backend-neutral,
- the MVP needs one high-velocity primary implementation path,
- fallback coverage is still needed for parity checks and format edge cases.

Evidence remains mostly source-scan level (low confidence overall), so this is intentionally marked as a **provisional lock** for MVP prototyping rather than a long-term irreversible dependency commitment.

## Option set considered

| Option | Description | Included in final decision |
|---|---|---|
| A | AsmResolver-primary, dnlib fallback, SRM-oriented symbol bridge where needed. | **Yes** |
| B | dnlib-primary, AsmResolver fallback, project-owned symbol normalization. | No |
| C | SRM-oriented custom primary with selective library augmentation for IL-heavy paths. | No |
| D | Hybrid split: metadata/IL from one backend, symbols from another via strict adapters. | No (deferred) |

## Capability gate summary

| Gate | Required evidence threshold | Result | Notes |
|---|---|---|---|
| Metadata completeness for MVP corpus | Medium confidence minimum | **Provisional pass** | Source evidence indicates strong coverage in both AsmResolver and dnlib; fixture validation still required. |
| IL body fidelity (instruction + EH + locals) | Medium confidence minimum | **Provisional pass** | AsmResolver method-body model appears suitable for normalized projection; malformed-body behavior still unproven. |
| Generic signature/context behavior | Medium confidence minimum | **Provisional pass** | Current notes indicate feasible projection path, but generic-heavy conformance scenarios remain open. |
| Portable PDB baseline mapping | Medium confidence minimum | **Provisional pass** | AsmResolver path exists; parity against SRM-oriented expectations still required. |
| Miss-reason normalization compatibility | Medium confidence minimum | **Provisional pass** | No blocker identified; requires adapter conformance fixtures before graduation to confirmed pass. |
| Deterministic budget behavior | Medium confidence minimum | **Provisional pass** | Policy injection points are present; deterministic replay proof still pending. |

## Rationale

1. **Why AsmResolver as primary now**
   - It offers a rich and layered object model across metadata, IL bodies, PE structures, and symbol-facing packages, which reduces MVP adapter bring-up friction.
   - Its package/layer boundaries align with the project’s planned separation between acquisition, projection, and interpreter-facing contracts.
   - Existing design notes already treat AsmResolver as the leading candidate, so choosing it now removes ambiguity and focuses prototype work.

2. **Why dnlib is not primary (but remains critical)**
   - dnlib is strong and remains highly valuable, especially for fallback and cross-check behavior.
   - However, selecting dnlib primary now would not materially reduce architectural risk versus AsmResolver and could increase churn against current design momentum.
   - dnlib remains the designated fallback and parity backend, preserving optionality.

3. **Why not SRM-custom or split-hybrid for MVP primary**
   - SRM-custom primary is promising long-term but imposes additional project-owned lifting during a phase where we need velocity and rapid evidence generation.
   - A split-hybrid primary/fused approach increases orchestration complexity before adapter conformance gates are mature.

## Consequences

### Positive

- Unblocks focused prototype work against a single primary backend path.
- Preserves architectural safety via explicit backend-neutral contract requirements.
- Keeps fallback optionality through dnlib-based parity and contingency coverage.

### Negative / trade-offs

- Increases short-term risk of accidental AsmResolver vocabulary leakage.
- May delay SRM-first hardening and hybrid experiments until after baseline prototype milestones.
- Current evidence confidence is still low; this decision requires active validation to remain credible.

### Risk mitigations

- Enforce the adapter conformance checklist as a quality gate for all backend-facing features.
- Keep dnlib parity scenarios mandatory for changed projection behavior.
- Track mismatch classes in the backend evidence log and escalate unresolved divergence as decision-review triggers.

## Follow-up actions

| Action | Owner | Target milestone | Tracking link |
|---|---|---|---|
| Implement baseline AsmResolver-to-normalized projection for method body + symbols. | TBD | MVP prototype baseline | `docs/lib/adapter-conformance-checklist.md` |
| Add dnlib parity scenarios for generic-heavy IL + partial symbol conditions. | TBD | MVP prototype baseline | `docs/lib/backend-evidence-log.md` |
| Define explicit trigger conditions for revisiting primary-backend choice (e.g., repeated parity divergence). | TBD | MVP prototype baseline | `docs/lib/backend-capability-matrix.md` |
| Update integration proposal language to reflect AsmResolver-primary / dnlib-fallback stance. | TBD | Next docs pass | `docs/proposals/integration/pe-pdb-reader-integration-proposal.md` |

## Change log

| Date | Change | Author |
|---|---|---|
| 2026-02-17 | Promoted record from pending template to provisional MVP decision: AsmResolver primary, dnlib fallback. | Codex |
