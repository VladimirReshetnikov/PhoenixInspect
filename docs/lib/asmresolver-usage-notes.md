# AsmResolver usage notes for this project

## Why it matters

AsmResolver is a strong candidate metadata and CIL backend for our design phase because it provides rich, navigable models for:

- .NET metadata,
- method bodies and IL structures,
- PE-level constructs,
- PDB processing (including dedicated symbols packages).

For this project, that capability aligns with artifact resolution, method-body reconstruction, and debug-map generation paths.

## Snapshot review highlights

The `lib/asmresolver/src` snapshot is split into focused packages:

- `AsmResolver.DotNet` (managed metadata/object model),
- `AsmResolver.PE` and `AsmResolver.PE.File` (PE image model and file access),
- `AsmResolver.Symbols.Pdb` (PDB readers and symbol model),
- `AsmResolver.PE.Win32Resources` (resource support),
- `AsmResolver.DotNet.Dynamic` (dynamic method support),
- `AsmResolver` (core primitives/utilities).

This package split matches our desired architecture: independent adapters for runtime metadata, PE artifacts, and symbol ingestion with a shared projection layer.

## Source-level API surfaces relevant to our adapters

### 1) Module entry and reader parameterization

`ModuleDefinition` provides a broad set of `From*` factories (bytes, streams, files, PE images), with `ModuleReaderParameters` controlling read behavior.

Practical implication: we can standardize artifact ingestion through one adapter gateway that records reader parameters as provenance metadata.

### 2) Explicit .NET directory and PE layering

`PEImage` and `DotNetDirectory` make the PE/.NET layering explicit rather than opaque.

Practical implication: this supports deterministic diagnostics when the PE is valid but CLR metadata is partial/malformed, which aligns with our explainability goals.

### 3) CIL method body model

`CilMethodBody` and related serializer/build infrastructure in `AsmResolver.DotNet.Code.Cil` provide structured instruction and handler access.

Practical implication: this is a strong fit for our normalized method-body contract (instructions + EH + locals + diagnostics).

### 4) End-to-end writing/building pipeline (design relevance)

Builder classes (`DotNetDirectoryFactory`, `DotNetDirectoryBuffer`, PE build results) indicate robust reconstruction support.

Practical implication: even though we are read-focused, this can help future fixture generation and "round-trip sanity" test tooling.

### 5) PDB image model breadth

`PdbImage` exposes APIs for loading from file/bytes/reader and retrieving records/modules/symbols.

Practical implication: AsmResolver can potentially host both metadata and symbol projection under one backend path, reducing early integration friction.

## Best-fit responsibilities (project-specific)

1. **Canonical metadata decoding backend (candidate)**
   - method/type/member metadata reads,
   - generic signature parsing inputs for interpreter dispatch.
2. **Method body and IL structure extraction**
   - instruction stream + exception handling data needed by interpreter execution planning.
3. **Artifact-oriented enrichment**
   - participation in debug-map materialization when symbol artifacts are available.

## Boundary and architecture guidance

- Treat AsmResolver as an adapter implementation detail.
- Convert AsmResolver models into project-owned contracts before crossing interpreter boundaries.
- Keep fallback backend optional (e.g., SRM/dnlib) behind the same interfaces.
- Normalize symbol quality/failure semantics so PDB-source differences do not leak to host UX.

## Risks and design pressure

1. **Abstraction creep**
   - direct dependency on AsmResolver model types in core planning docs can overfit architecture.
2. **Multi-backend complexity**
   - supporting alternative readers requires strict normalization points.
3. **PDB strategy ambiguity**
   - if Portable PDB and Windows PDB pipelines diverge too much, we risk split debug-map behavior.
4. **Large-surface temptation**
   - broad API coverage may encourage premature adoption of advanced features before contract stabilization.

## Recommended next experiments

1. Define a single normalized metadata projection from `ModuleDefinition`/`MethodDefinition`/`CilMethodBody`.
2. Run one malformed-artifact suite through configurable `ModuleReaderParameters` and catalog error-to-miss mappings.
3. Validate a portable symbol projection using `PdbImage` and compare parity with an SRM-based symbol path.
4. Add one round-trip fixture-generation concept using AsmResolver builders to strengthen future conformance tests.
