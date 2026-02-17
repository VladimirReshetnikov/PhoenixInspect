# AsmResolver usage notes for this project

## Why it matters

AsmResolver is a strong metadata/CIL backend candidate for our design phase because it provides rich, navigable models for:

- .NET metadata,
- method bodies and IL structures,
- PE-level constructs,
- PDB processing (via dedicated symbol packages).

For this project, that aligns with artifact resolution, method-body reconstruction, and debug-map generation.

## Snapshot review highlights

The `lib/asmresolver/src` snapshot is package-oriented and maps well to adapter seams:

- `AsmResolver.DotNet` (managed metadata model and method/type/member surfaces),
- `AsmResolver.PE` + `AsmResolver.PE.File` (PE image and binary reading),
- `AsmResolver.Symbols.Pdb` (PDB model and reader flows),
- `AsmResolver.PE.Win32Resources` (resource support),
- `AsmResolver.DotNet.Dynamic` (dynamic method support),
- `AsmResolver` (shared primitives/utilities).

This package split supports independent metadata, PE, and symbol adapters with a common projection layer.

## Source-level API surfaces relevant to our adapters

### 1) ModuleDefinition ingestion flexibility

`ModuleDefinition` exposes `FromFile`, `FromBytes`, `FromStream`, and PE-based loading flows, all parameterized by `ModuleReaderParameters`.

Design implication:

- centralize ingestion configuration in one adapter policy object,
- record reader parameters in provenance for replayability,
- normalize exceptions into stable miss reasons instead of leaking parser specifics.

### 2) Explicit PE/.NET directory layering

AsmResolver keeps PE and CLR metadata concerns explicit (`PEImage`, .NET directory access, and related builders).

Design implication:

- distinguish "PE readable" vs "metadata usable" failure modes,
- improve explainability for partially valid artifacts,
- keep our miss taxonomy aligned with those layered failure states.

### 3) Method model and CIL body structure

`MethodDefinition` and `CilMethodBody` provide structured access to:

- instruction list,
- local variables,
- EH regions,
- body-level metadata needed for control-flow preparation.

Design implication:

- AsmResolver can feed our normalized method-body contract with low projection friction,
- we should still avoid backend instruction objects beyond adapter boundaries.

### 4) Builder pipeline as fixture-generation lever

`AsmResolver.DotNet.Builder` and PE builder types show a full reconstruction pipeline.

Design implication:

- while runtime is read-focused, this pipeline is useful for synthetic fixture generation,
- we can generate targeted malformed/edge-case assemblies to harden miss-reason taxonomy.

### 5) PDB model breadth and symbol projection opportunities

`AsmResolver.Symbols.Pdb.PdbImage` supports file/byte/reader loading and symbol/module enumeration.

Design implication:

- feasible single-stack metadata + symbols experimentation for early prototypes,
- still require backend-neutral symbol projection to avoid long-term lock-in.

### 6) Dynamic method package as future edge-case path

`AsmResolver.DotNet.Dynamic` includes dynamic method readers/helpers.

Design implication:

- not an MVP requirement, but useful for future parity with runtime-generated method scenarios,
- should remain a capability flag until explicitly validated.

## Best-fit responsibilities (project-specific)

1. **Primary metadata/CIL candidate backend**
   - method/type/member decoding and generic signature extraction.
2. **Method-body extraction path**
   - instruction/EH/local payloads for interpreter planning and stepping.
3. **Artifact enrichment backend**
   - PE and symbol facts feeding debug-map generation.
4. **Fixture construction support**
   - optional controlled-artifact generation for conformance and robustness tests.

## Boundary and architecture guidance

- Treat AsmResolver as adapter-only implementation detail.
- Project into immutable project-owned contracts before crossing interpreter boundaries.
- Keep alternate backends (dnlib/SRM-oriented path) available behind the same interfaces.
- Normalize symbol quality/failure semantics independent of AsmResolver-specific diagnostics.

## Risks and design pressure

1. **Abstraction creep**
   - core designs may become overfit to AsmResolver object model vocabulary.
2. **Backend plurality cost**
   - maintaining parity with alternative backends requires strict normalization discipline.
3. **PDB strategy divergence**
   - Portable vs Windows PDB behavior can fragment debug-map behavior if unmanaged.
4. **Surface-area temptation**
   - broad API coverage may encourage premature adoption of non-MVP features.
5. **Dynamic-method scope drift**
   - dynamic method support can expand complexity before baseline deterministic contracts are stable.

## Recommended next experiments

1. Implement one normalized projection from `ModuleDefinition`/`MethodDefinition`/`CilMethodBody`.
2. Run malformed-artifact fixtures under varying `ModuleReaderParameters` and map failures.
3. Validate one symbol projection path from `PdbImage` into project debug-map schema.
4. Prototype fixture generation for edge-case IL/EH patterns using builder APIs.
5. Defer dynamic-method support behind an explicit capability gate and document the gate criteria.

## Deep-dive addendum (2026-02 source pass)

Additional source-backed details from AsmResolver package layers:

- `Serialized/ModuleReaderParameters` exposes high-value policy knobs (`ModuleResolver`, `MethodBodyReader`, `PEReaderParameters`, `RuntimeContext`).
- `Serialized/SerializedMethodDefinition.GetMethodBody()` routes body materialization through a pluggable `IMethodBodyReader`, reinforcing adapter customization potential.
- `Code/Cil/CilMethodBody` uses lazy initialization for instructions/EH collections and exposes build-time validation flags.
- `AsmResolver.Symbols.Pdb/PdbImage` provides multiple load routes (file, bytes, reader, MSF file) and typed leaf-record retrieval patterns.

Design addendum:

1. Standardize one project-owned "reader profile" abstraction that maps deterministically to AsmResolver reader parameters.
2. Preserve decode-state/completeness in projected method-body contracts to avoid hiding lazy-read or malformed-data outcomes.
3. Keep symbol record richness available internally, but map outward to backend-neutral debug-map records.
