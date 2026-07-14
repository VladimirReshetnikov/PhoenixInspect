# Active metadata backend decision record

## Decision status

- **Status:** Decided for the active prototype slices
- **Decision:** SRM/PEReader
- **Decision date:** 2026-07-13
- **Revisit scope:** prototype implementation choice, not a public object-model guarantee
- **Supersedes:** the 2026-02-17 provisional AsmResolver selection

## Decision

Use `System.Reflection.Metadata` plus `PEReader` as the metadata, PE, IL-body, and Portable PDB foundation for the dump-evidence, restricted-expression, and first interpreted-method slices.

Keep project-owned identities and result/evidence contracts at the boundary. Do not leak SRM handles into the interpreter or host API. Do not implement a second backend without an executable corpus demonstrating a material SRM deficiency or cost.

## Why the decision changed

The earlier AsmResolver choice was based primarily on source scans and projected implementation velocity. Since then, the repository acquired a working SRM adapter and an end-to-end integration seam. AsmResolver remained an empty project. The old decision therefore privileged anticipated convenience over repository evidence.

SRM is the lower-risk active choice because:

- it is already exercised by the prototype;
- its low-level behavior makes identity, partialness, and malformed-input policy explicit;
- Portable PDBs use the same metadata model;
- the expected decompiler fallback, ILSpy, is SRM-centric;
- removing the unused backend reduces dependency and package surface.

This does not assert that SRM is universally superior. It says the project will deepen one proven path before funding alternatives.

## Boundary rules

1. Artifact module identity is content-derived (MVID plus PE disambiguation where required); paths and discovery order are hints, not identity.
2. Runtime module instances and disk artifacts remain separate identities joined by explicit evidence.
3. Missing, sparse, conflicting, and malformed inputs produce typed evidence outcomes and stable diagnostics.
4. Method definitions use module identity plus metadata token; generic instantiations add deterministic context rather than allocation counters.
5. Backend-specific objects do not cross projected contracts.

## Revisit triggers

Evaluate another backend only when at least one checked-in fixture demonstrates:

- an unsupported or disproportionately costly metadata/IL/PDB operation required by an active milestone;
- malformed-input behavior that cannot be made sufficiently bounded or diagnosable;
- a measured performance/memory problem relevant to an active acceptance criterion; or
- a Windows PDB requirement for which SRM is intentionally not the implementation.

An experiment must implement the same projected contract and run the same conformance fixtures. A source comparison or attractive API alone is not a trigger.

## Consequences

- `Interpreter.Metadata.AsmResolver` is removed rather than retained as an empty option.
- AsmResolver, dnlib, Cecil, and DIA notes under `docs/lib/` remain research references, not roadmap commitments.
- Package count and backend vocabulary shrink while the public boundary remains replaceable.
- Any future backend decision is based on executable differential evidence.

## Evidence required before stabilization

- exact module/method identity across repeated process runs;
- PE identity mismatch and missing-artifact outcomes;
- method body, locals, EH region, and generic signature fixtures as their slices are admitted;
- Portable PDB sequence-point/local-scope fixtures when the expression or stepping path actually needs them;
- bounded malformed/truncated artifact tests.
