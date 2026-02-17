# AsmResolver source scan notes (snapshot: `lib/asmresolver`)

This note records a source-driven scan of AsmResolver focusing on metadata/CIL/symbol ingestion seams relevant to backend adapters.

## What was reviewed

Primary files and surfaces reviewed:

- `src/AsmResolver.DotNet/ModuleDefinition.cs`
- `src/AsmResolver.DotNet/Serialized/ModuleReaderParameters.cs`
- `src/AsmResolver.DotNet/Serialized/ModuleReaderContext.cs`
- `src/AsmResolver.DotNet/Code/Cil/CilMethodBody.cs`
- `src/AsmResolver.DotNet/Serialized/DefaultMethodBodyReader.cs`
- `src/AsmResolver.DotNet/Serialized/SerializedMethodDefinition.cs`
- `src/AsmResolver.Symbols.Pdb/PdbImage.cs`

## Structural observations

- AsmResolver exposes clean front-door module load APIs (`FromFile`, `FromStream`, `FromBytes`) and routes behavior through reader-parameter objects.
- The serialized reader stack separates context (`ModuleReaderContext`) from policy (`ModuleReaderParameters`) and lazy member realization.
- PDB support is broad and lives in dedicated symbol packages with lazy record access patterns.

## Source-backed findings

## 1) Reader parameterization is a strong deterministic seam

Observed behavior:

- `ModuleReaderParameters` centralizes working directory, resolver, method-body reader, PE-reader parameters, and runtime context knobs.
- `ModuleDefinition` factory methods consistently accept these parameters.

Design implication:

- Treat AsmResolver reader parameters as an adapter policy profile object.
- Freeze a project-default parameter profile and version it in documentation.

## 2) Module reader context keeps decode state local

Observed behavior:

- `ModuleReaderContext` is passed through serialized member factories and stream readers.
- Bad-image handling pathways are surfaced through context-mediated helper methods.

Design implication:

- Preserve "decode quality" in projections (clean/partial/invalid) instead of silently dropping malformed entries.
- Log per-method decode failures with metadata token identity.

## 3) Method bodies are modeled as rich, mutable CIL objects

Observed behavior:

- `CilMethodBody` represents locals, instructions, and exception handlers with lazy access patterns.
- `DefaultMethodBodyReader` and `SerializedMethodDefinition.GetMethodBody()` form the default decode path.

Design implication:

- Project to a backend-neutral immutable method-body shape before entering interpreter contracts.
- Capture incomplete decode markers and keep them visible to diagnostic/provenance output.

## 4) PDB ingestion supports multiple entry forms and lazy type lookup

Observed behavior in `PdbImage`:

- PDB can be loaded from file/bytes/reader/MSF file.
- Symbol/module collections and leaf-record retrieval are lazy and index-driven (`TryGetLeafRecord(...)`).

Design implication:

- AsmResolver can be a single-backend option for metadata + symbol projections.
- Keep our debug-map schema neutral and avoid exposing CodeView-specific terminology in external contracts.

## Adapter follow-through checklist

- Draft an AsmResolver read-policy profile (resolver + PE read + method-body read + PDB read).
- Add projection status fields for malformed metadata/method/PDB entries.
- Build token-centric logging so conformance tests can compare behavior across backends.
- Add hidden/sequence-point/document-switch parity checks against at least one alternate backend.

## Confidence and caveats

- Confidence is high for load/decode seams because APIs are explicit and modular.
- Confidence is medium for edge-case malformed binaries until we run corpus-driven adapter spikes.
