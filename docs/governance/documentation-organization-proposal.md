# Documentation Maintenance Policy

**Lifecycle:** Current · **Roadmap:** Supporting
**Supersedes:** the unimplemented ADR/spec/owner/template migration proposal

## 1. Purpose

Documentation exists to improve product and engineering decisions. It is not a parallel measure of implementation progress. This solo/small-team design-phase repository keeps only the governance that has demonstrated value.

## 2. Maintained information architecture

```text
docs/
├── README.md        canonical inventory and reading paths
├── governance/      terminology and maintenance policy
├── plans/           active sequencing and evidence traceability
├── proposals/
│   ├── product/
│   ├── architecture/
│   └── integration/
└── lib/             source/library research notes
```

The root `DESIGN-ARCHITECTURE-REVIEW.md` is the point-in-time review that motivated the 2026-07 reset.

No per-folder index, owner field, freshness timestamp, ADR directory, normative-spec directory, or template automation is required now. Add such machinery only when a recurring coordination problem demonstrates its value.

## 3. Two independent status axes

Every current, active, or supporting document in the canonical index has:

1. a **lifecycle** — `Current`, `Draft`, `Complete`, `Superseded`, or `Historical`;
2. a **roadmap relation** — `Active`, `Supporting`, `Research`, `Reference`, or `Historical`.

Historical library notes and point-in-time source records may use a single combined status when a second axis would add no decision value. The index, not an inferred filename convention, is authoritative for status.

Examples:

- a draft restricted-expression design can be `Draft / Active`;
- a polished async proposal can be `Complete / Research backlog`;
- an old package catalog can be `Superseded / Historical`.

“Complete” means the document is complete as a document. It does not mean the capability is implemented.

## 4. Design-just-ahead-of-code rule

- The active milestone may add or refine the smallest contract needed for its next executable slice.
- A new subsystem proposal requires an active-roadmap dependency; curiosity alone goes to a short research note or issue, not a normative-looking proposal.
- Do not create empty projects, interface catalogs, support matrices, registries, or governance artifacts in anticipation of possible implementation.
- Prefer updating or superseding contradictory documents over adding another reconciliation layer.
- When implementation disproves a design, update the design in the same change.

## 5. Evidence language

Documents distinguish:

- **designed:** a behavior or boundary is described;
- **implemented:** code exists;
- **validated:** an executable test or measured corpus supports the claim;
- **committed:** it is on the active roadmap with an exit criterion.

Planned tests are not validation. Empty projects are not implementation. A source scan is not backend conformance evidence.

## 6. Placement and naming

- Keep stable, descriptive kebab-case filenames.
- Product behavior belongs under `proposals/product`; technical boundaries under `proposals/architecture`; external-system seams under `proposals/integration`; sequencing under `plans`; process under `governance`.
- Point-in-time reviews may live at the repository root when explicitly requested as a top-level handoff artifact.
- If a file moves or is deleted, update `README.md`, `docs/README.md`, and inbound links in the same change.

## 7. Required document shape

Use only the structure the content needs. For active or supporting design, include:

- lifecycle and roadmap relation near the top;
- scope/non-scope;
- decisions and assumptions;
- evidence and gaps;
- entry/exit criteria when sequencing matters.

Long templates and mandatory sections are intentionally avoided.

## 8. Review triggers

Review affected documentation when:

- a public prototype contract changes;
- a milestone or scope lock changes;
- implementation evidence changes a capability claim;
- terminology or identity/result semantics change;
- a backend/dependency/validated-input boundary changes;
- a link target is moved or removed.

## 9. Definition of done

A documentation change is done when:

1. the relevant claims match repository evidence;
2. lifecycle and roadmap relation are honest in the index;
3. active terminology agrees with the glossary or explicitly explains a local distinction;
4. inbound links and reading paths work;
5. it does not present research design as implementation, validation, or commitment.

This policy is deliberately smaller than the regime it replaces. If the team grows, revise it in response to observed coordination needs.
