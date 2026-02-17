# AsmResolver usage notes for this project

## Why it matters

AsmResolver is a strong candidate metadata and CIL body backend for our design phase because it gives rich, navigable models for:

- .NET metadata,
- method bodies and IL structures,
- PE-level constructs,
- Windows PDB modeling support through companion packages.

For this project, that capability aligns with artifact resolution, method-body reconstruction, and debug-map generation paths.

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

## Risks and design pressure

1. **Abstraction creep**
   - direct dependency on AsmResolver model types in core planning docs can overfit architecture.
2. **Multi-backend complexity**
   - supporting alternative readers requires strict normalization points.
3. **PDB strategy ambiguity**
   - if Portable PDB and Windows PDB pipelines diverge too much, we risk split debug-map behavior.

## Early action items

- Define a single normalized metadata model used by the interpreter and debug-map layers.
- Specify which AsmResolver features are required for MVP vs optional enhancements.
- Add comparison notes against SRM/dnlib for each required metadata/symbol capability.
