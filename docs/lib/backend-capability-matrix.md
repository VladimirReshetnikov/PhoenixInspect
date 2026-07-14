# Metadata and symbol backend capability matrix (historical research)

> **Decision context:** this matrix captures the pre-decision, source-review comparison made in 2026-02. SRM/PEReader is now the active backend for prototype slices; see `mvp-backend-decision-record.md` and the current rows in `backend-evidence-log.md`. The matrix is retained to preserve research and future revisit criteria, not to imply that backend selection remains open.

This matrix turns the library-specific notes in `docs/lib/<library>/usage-notes.md` into a common evaluation frame.
For API-shape-level reader ergonomics and PE/PDB-specific trade-offs, see `pe-pdb-reader-api-comparison.md`.

Its ratings are source-review hypotheses, not executable capability claims.

## Purpose

- define a shared vocabulary for backend comparison,
- keep interpreter-facing contracts stable while adapters evolve,
- preserve the trade-offs considered before the SRM backend decision.

## Capability axes

The axes below are used when evaluating AsmResolver, dnlib, and SRM-oriented implementations.

| Capability axis | Why it matters to this project | MVP expectation |
|---|---|---|
| Metadata completeness | Required for method/type/member identity reconstruction from dumps. | Must support common managed assemblies with generic metadata fidelity. |
| IL body fidelity | Required for deterministic interpreter stepping and control-flow analysis. | Must provide instruction stream + EH regions + locals/signatures. |
| Generic signature handling | Required for generic-context resolver and instantiated method identity. | Must preserve generic parameter/argument structure without lossy normalization. |
| Portable PDB consumption | Required for source mapping and statement-level stepping experience. | Must support baseline sequence-point mapping for primary C# scenarios. |
| Windows PDB strategy | Important for non-portable-symbol environments. | Optional for strict MVP; roadmap item with explicit fallback behavior. |
| Error and ambiguity surfacing | Required for explainable “unknown/partial” outcomes. | Must map backend failures to normalized miss reasons. |
| Performance predictability | Required for budgeted execution and bounded analysis. | Must support deterministic budgets for metadata/method materialization paths. |
| API stability and maintainability | Reduces integration churn during design-to-implementation transition. | Must be shielded by project-owned adapter interfaces. |

## Historical comparison snapshot

Legend:

- **Strong**: likely suitable as primary path,
- **Partial**: suitable with constraints or additional layers,
- **Unknown**: not yet validated in this project context.

| Backend candidate | Metadata completeness | IL body fidelity | Generic signatures | Portable PDB | Windows PDB | Explainability mapping | Notes |
|---|---|---|---|---|---|---|---|
| AsmResolver | Strong | Strong | Strong | Partial | Partial | Partial | Leading design candidate in the 2026-02 source review; never established by an executable adapter. |
| dnlib | Strong | Strong | Partial | Partial | Partial | Partial | Valuable fallback/cross-check backend; avoid premature primary-path coupling. |
| SRM-oriented custom layer | Strong | Partial | Partial | Strong | Unknown | Strong | Potentially lean and controllable, but requires more project-owned lifting for IL workflows. |

## Design implications

1. **Primary + fallback model should remain explicit**
   - selecting a primary backend does not remove need for a backend-neutral contract.
2. **Capability ownership belongs to project contracts**
   - symbol lookup, method-body decoding, and error classification semantics should be defined by our interfaces, not inherited from backend defaults.
3. **PDB behavior needs one normalized story**
   - both Portable and Windows PDB cases should converge into the same debug-map and provenance outputs.

## Historical decision gates

The original matrix proposed the following gates before declaring a backend baseline:

1. a thin adapter prototype that exercises metadata + IL + symbol paths,
2. documented miss-reason mapping for partial/artifact-poor dumps,
3. a benchmark sketch for method materialization cost under realistic dump workloads,
4. at least one cross-backend conformance scenario (same input, same normalized outcome category).

## Current use

- Add new executable evidence to `backend-evidence-log.md`, not to the historical ratings above.
- Revisit a candidate only under the triggers in `mvp-backend-decision-record.md`.
- Keep project-owned identity, evidence, and miss-reason contracts independent of backend-specific object models.
