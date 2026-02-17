# PE/PDB reader API and capability comparison (draft)

This note complements `backend-capability-matrix.md` with API-shape-level details for candidate PE/PDB readers.

It focuses on how each option affects our adapter design for dump-time IL interpretation and virtual stepping.

## Scope

- Compare practical API surfaces for reading PE metadata, method bodies, and symbols.
- Highlight capability gaps that must be normalized in project-owned contracts.
- Keep implementation optionality open while we gather evidence.

## Candidate readers covered

1. **AsmResolver** (metadata + CIL + PE + symbol-oriented packages)
2. **dnlib** (metadata + CIL + broad reverse-engineering workflows)
3. **System.Reflection.Metadata (SRM) stack**
   - `PEReader`
   - `MetadataReader`
   - `MetadataReaderProvider`
   - `MethodBodyBlock`
4. **Microsoft.DiaSymReader stack** (primarily Windows PDB-focused symbol access)
5. **Mono.Cecil** (secondary comparison point for API ergonomics and ecosystem fit)

## API shape comparison (reader ergonomics)

| Reader | Primary read model | Method body access | Symbol access model | Typical adapter burden |
|---|---|---|---|---|
| AsmResolver | Object-graph style models for modules, members, signatures, and CIL constructs. | High-level CIL instruction graph and exception regions are readily navigable. | Portable and Windows PDB support depends on package paths and integration choices. | Moderate: map rich models to normalized contracts without leaking backend-specific identity rules. |
| dnlib | Object-graph style metadata + CIL model with mature editing/inspection workflows. | High-level instruction collections + EH metadata are straightforward to enumerate. | Portable/Windows PDB paths exist but require explicit policy shaping for parity semantics. | Moderate-to-high: strong capabilities, but backend-specific behaviors must be normalized carefully. |
| SRM stack | Token/handle + reader-oriented low-level API (`Handle`/`Row` patterns). | `PEReader` + `MethodBodyBlock` provide lower-level decode primitives with explicit control. | Portable PDB is first-class via metadata readers; Windows PDB requires additional stack. | High initially: more project-owned lifting for convenience abstractions and semantic mapping. |
| DiaSymReader stack | Symbol-centric COM/interop-oriented APIs rather than full metadata/CIL surface. | Not intended as primary IL/body reader. | Strong Windows PDB historical coverage in debugger ecosystems. | High if used directly: must pair with separate metadata/IL reader and normalize interop complexity. |
| Mono.Cecil | Object-model-first metadata/CIL API with strong IL manipulation heritage. | High-level method body access similar to other object-model readers. | Symbol support available, with behavior depending on symbol package/version choices. | Moderate: similar normalization needs as AsmResolver/dnlib plus package/version governance work. |

## Capability comparison by PE/PDB concerns

Legend: **Strong**, **Partial**, **Weak**, **Unknown (project-unvalidated)**.

| Capability concern | AsmResolver | dnlib | SRM stack | DiaSymReader stack | Mono.Cecil |
|---|---|---|---|---|---|
| PE structure access (headers/sections) | Strong | Partial | Strong | Weak | Partial |
| ECMA-335 metadata traversal | Strong | Strong | Strong | Weak | Strong |
| IL body decoding fidelity | Strong | Strong | Partial (low-level) | Weak | Strong |
| Generic signature reconstruction | Strong | Partial | Partial | Weak | Partial |
| Portable PDB sequence points | Partial | Partial | Strong | Weak | Partial |
| Windows PDB workflow | Partial | Partial | Weak (needs companion stack) | Strong | Partial |
| Deterministic failure classification potential | Partial | Partial | Strong | Partial | Partial |
| Effort to keep backend-neutral contract stable | Moderate | Moderate | High upfront, lower long-term drift | High | Moderate |

## API implications for our adapter contracts

### 1) Separate *artifact ingestion* from *semantic projection*

Across all readers, we should keep a strict two-phase adapter boundary:

1. **Ingestion phase:** backend-native parsing, diagnostics, and low-level decode.
2. **Projection phase:** conversion into project-owned contracts for:
   - metadata identity,
   - method body structure,
   - sequence-point/debug-map payload,
   - normalized miss reasons.

This reduces contract drift when swapping readers and helps explainability.

### 2) Keep method-body contract independent from reader instruction objects

Object-model readers expose rich instruction objects, while SRM exposes lower-level decode blocks.

Our method-body contract should therefore prefer:

- stable opcode + operand representation,
- explicit EH region schema,
- explicit local/signature descriptors,
- explicit decode diagnostics.

### 3) Use a unified symbol projection regardless of PDB format

Portable and Windows PDB support differs significantly by reader stack.

To avoid split debugger behavior, project contracts should normalize both into a single symbol model:

- source document identity,
- sequence point ranges,
- hidden/step-filter markers,
- symbol-quality provenance and miss reasons.

### 4) Treat Windows PDB support as a composable capability flag

No single stack gives a perfect PE + IL + Portable PDB + Windows PDB story with equal ergonomics.

For MVP planning, keep Windows PDB as an explicit capability flag rather than an implicit guarantee.

## Recommended evidence spikes (next)

1. **Tri-reader method-body parity test**
   - Compare AsmResolver vs dnlib vs SRM decode output for the same generic-heavy methods.
2. **Portable PDB sequence-point parity test**
   - Compare AsmResolver-backed and SRM-backed symbol projections into the same debug-map schema.
3. **Windows PDB risk probe**
   - Validate at least one real artifact path through DiaSymReader-backed symbol extraction.
4. **Failure taxonomy trial**
   - Inject corrupted/partial artifacts and verify normalized miss reasons are backend-agnostic.

## Design position (current draft)

- Keep **AsmResolver** and **dnlib** as high-productivity object-model candidates for early adapter prototyping.
- Keep an **SRM-oriented projection path** as the long-term control/maintainability hedge.
- Treat **DiaSymReader** as a targeted Windows PDB capability module, not a full backend replacement.
- Keep **Mono.Cecil** as an optional comparison baseline, not a required MVP backend candidate.

This position remains provisional until `backend-evidence-log.md` records medium-confidence evidence for key axes.
