# Metadata and symbol backend capability matrix (draft)

This matrix turns the library-specific notes in `docs/lib/*-usage-notes.md` into a common evaluation frame.
For API-shape-level reader ergonomics and PE/PDB-specific trade-offs, see `pe-pdb-reader-api-comparison.md`.

It is intentionally design-phase guidance, not a final commitment to any concrete dependency set.

## Purpose

- define a shared vocabulary for backend comparison,
- keep interpreter-facing contracts stable while adapters evolve,
- make trade-offs explicit before MVP backend lock-in.

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

## Draft comparison snapshot

Legend:

- **Strong**: likely suitable as primary path,
- **Partial**: suitable with constraints or additional layers,
- **Unknown**: not yet validated in this project context.

| Backend candidate | Metadata completeness | IL body fidelity | Generic signatures | Portable PDB | Windows PDB | Explainability mapping | Notes |
|---|---|---|---|---|---|---|---|
| AsmResolver | Strong | Strong | Strong | Partial | Partial | Partial | Current leading design candidate; still requires strict normalization boundary. |
| dnlib | Strong | Strong | Partial | Partial | Partial | Partial | Valuable fallback/cross-check backend; avoid premature primary-path coupling. |
| SRM-oriented custom layer | Strong | Partial | Partial | Strong | Unknown | Strong | Potentially lean and controllable, but requires more project-owned lifting for IL workflows. |

## Design implications

1. **Primary + fallback model should remain explicit**
   - selecting a primary backend does not remove need for a backend-neutral contract.
2. **Capability ownership belongs to project contracts**
   - symbol lookup, method-body decoding, and error classification semantics should be defined by our interfaces, not inherited from backend defaults.
3. **PDB behavior needs one normalized story**
   - both Portable and Windows PDB cases should converge into the same debug-map and provenance outputs.

## Decision gates

Before declaring an MVP backend baseline, require:

1. a thin adapter prototype that exercises metadata + IL + symbol paths,
2. documented miss-reason mapping for partial/artifact-poor dumps,
3. a benchmark sketch for method materialization cost under realistic dump workloads,
4. at least one cross-backend conformance scenario (same input, same normalized outcome category).

## Follow-up documentation tasks

- Add per-axis evidence references as prototype experiments are completed.
- Split this matrix into “MVP required” vs “post-MVP desirable” rows once milestone gates solidify.
- Cross-link each axis to relevant architecture and integration proposals after terminology alignment.
