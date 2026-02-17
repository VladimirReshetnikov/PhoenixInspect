# dnlib usage notes for this project

## Why it matters

dnlib remains an important reference point for this project even if it is not our default backend.

It is historically strong for:

- broad .NET assembly metadata handling,
- IL read/write scenarios,
- practical support patterns across reverse-engineering/debugging tooling,
- potential Windows PDB interoperability paths.

## Potential project applications

1. **Alternative metadata backend**
   - used behind the same normalized contracts as AsmResolver/SRM-based implementations.
2. **Compatibility fallback option**
   - useful where another backend struggles with specific edge-case binaries.
3. **Cross-checking backend during design**
   - can validate assumptions in metadata normalization and method-body parsing.

## Boundary and architecture guidance

- Do not bake dnlib object models into interpreter or public project contracts.
- Keep dnlib support as a pluggable adapter path until explicit MVP commitments are made.
- Prefer decision records documenting when dnlib is required vs optional.

## Risks and design pressure

1. **Third-stack overhead**
   - running SRM + AsmResolver + dnlib in parallel can fragment design effort.
2. **Contract drift**
   - if backend-specific quirks leak upward, normalized semantics become unstable.
3. **Maintenance cost**
   - each additional backend multiplies regression coverage needs.

## Early action items

- Document a minimum backend capability matrix and map dnlib against it.
- Prototype one thin dnlib adapter only after normalized contracts stabilize.
- Define explicit criteria for graduating dnlib from “reference/fallback” to “supported backend.”
