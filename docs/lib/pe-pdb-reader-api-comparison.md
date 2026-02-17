# PE/PDB reader API comparison and onboarding guide (source-driven draft)

This note expands the backend comparison with **source-level API onboarding guidance** for the PE/PDB reader stacks represented under `lib/`.

Primary objective: help developers who already know one stack quickly map concepts, entry points, and pitfalls in the others.

## Scope and evidence baseline

This comparison is based on source snapshots in:

- `lib/asmresolver/src/*`
- `lib/dnlib/src/*`
- `lib/cecil/*`
- selected symbol/interop integration points in dnlib and Cecil that expose DiaSymReader-style workflows.

`System.Reflection.Metadata` (SRM) is still included as a design reference because we intend to keep an SRM-oriented path viable, but this document focuses most deeply on the stacks with source snapshots in this repository.

## Reader stacks covered

1. **AsmResolver**
   - PE layer (`AsmResolver.PE.File.PEFile`, `AsmResolver.PE.PEImage`)
   - .NET layer (`AsmResolver.DotNet.ModuleDefinition`)
   - PDB layer (`AsmResolver.Symbols.Pdb.PdbImage`)
2. **dnlib**
   - metadata/module layer (`dnlib.DotNet.ModuleDefMD`)
   - IL body decode (`dnlib.DotNet.Emit.MethodBodyReader`)
   - symbol layer (`dnlib.DotNet.Pdb.Symbols.SymbolReader` + factories)
3. **Mono.Cecil**
   - module layer (`Mono.Cecil.ModuleDefinition`, `ReaderParameters`)
   - IL layer (`Mono.Cecil.Cil.MethodBody`, `Instruction`)
   - symbol providers (`PdbReaderProvider`, portable/native provider split)
4. **SRM + PEReader model** (design comparator)
5. **DiaSymReader-focused flows** as consumed by dnlib/Cecil symbol subsystems.

---

## Quick onboarding matrix: “If you know X, look at Y”

| Task | AsmResolver | dnlib | Mono.Cecil | SRM mental equivalent |
|---|---|---|---|---|
| Load module from file | `ModuleDefinition.FromFile(...)` | `ModuleDefMD.Load(...)` | `ModuleDefinition.ReadModule(...)` + `ReaderParameters` | `PEReader` + `MetadataReader` creation |
| Read raw PE headers/sections | `PEFile.FromFile(...)`, `PEFile.Sections` | `module.Metadata.PEImage`-oriented internals | lower-level via image internals (less commonly first API) | `PEReader.PEHeaders` |
| Get method body | `MethodDefinition.CilMethodBody` / `CilMethodBody` graph | `MethodDef.Body` / `MethodBodyReader` decode path | `MethodDefinition.Body` / `Cil.MethodBody` | `PEReader.GetMethodBody(rva)` (`MethodBodyBlock`) |
| Enumerate IL instructions | `CilMethodBody.Instructions` (`CilInstructionCollection`) | `CilBody.Instructions` | `MethodBody.Instructions` | decode IL bytes manually/with helper logic |
| Enumerate EH regions | `CilMethodBody.ExceptionHandlers` | `CilBody.ExceptionHandlers` | `MethodBody.ExceptionHandlers` | `ExceptionRegion` on `MethodBodyBlock` |
| Read PDB symbols | `PdbImage.FromFile(...)` + module/symbol model | symbol reader factories choose portable/managed/Dia path | `PdbReaderProvider` chooses portable/native provider | `MetadataReaderProvider` (portable); external for Windows PDB |
| Sequence points | PDB records projected from symbol model | `PdbMethod`/`SequencePoint` model | `MethodDebugInformation.SequencePoints` | `MethodDebugInformationHandle` decode |

---

## Source-driven API shape comparison

### 1) Artifact loading and reader parameterization

#### AsmResolver

- Very broad static factory surface (`FromBytes`, `FromStream`, `FromFile`, mapped-module forms) on `ModuleDefinition` and `PEFile`.
- Distinct parameter types (`ModuleReaderParameters`, `PEReaderParameters`, `PdbReaderParameters`) encourage explicit configuration of diagnostics and mapping mode.
- `PdbImage` mirrors module-loading ergonomics (`FromFile`, `FromBytes`, `FromReader`, plus MSF-aware overloads), which makes symbol-only workflows straightforward.

**Onboarding implication:** teams coming from Cecil/dnlib typically adapt quickly because factories are discoverable and symmetric across PE/.NET/PDB layers.

#### dnlib

- Central entry is `ModuleDefMD.Load(...)` with `ModuleCreationOptions`.
- API favors one root object (`ModuleDefMD`) with rich, lazy access to streams/tables and strong hooks for decrypters (`IMethodDecrypter`, `IStringDecrypter`).
- Symbol creation is delegated to factories rather than simple static constructors on a single PDB root type.

**Onboarding implication:** familiar to reverse-engineering workflows; less symmetric for “standalone PDB as first-class root” than AsmResolver.

#### Mono.Cecil

- Configuration is front-loaded in `ReaderParameters` (`ReadingMode`, `ReadSymbols`, symbol provider, streams, etc.).
- `ModuleDefinition` remains the central object, with deferred/immediate read strategies.
- Symbol attachment is usually provider-driven (e.g., `PdbReaderProvider`) rather than separate PDB-domain root APIs.

**Onboarding implication:** ergonomic for “module + optional symbols” pipeline; less modular if you want symbol parsing independent from module load.

#### SRM model

- Explicitly low-level: create a `PEReader`, obtain `MetadataReader`, then compose additional readers for symbols/method bodies.

**Onboarding implication:** best for precise control and contract stability, but high adapter lift for graph-like ergonomics.

---

### 2) Method body and IL decode model

#### AsmResolver

- High-level `CilMethodBody` abstraction with:
  - instruction collection (`CilInstructionCollection`),
  - local variable collection,
  - EH collection,
  - validation/build knobs (`BuildFlags`, `ComputeMaxStackOnBuild`, `VerifyLabelsOnBuild`).
- Includes explicit helper methods (`VerifyLabels`, `ComputeMaxStack`) that are useful for deterministic preflight checks.

**Strength:** rich model with built-in sanity operations aligned with our explainability goals.

#### dnlib

- `MethodDef.MethodBody` plus `Emit.MethodBodyReader` with multiple `CreateCilBody(...)` overloads.
- Decode path is highly configurable via operand resolvers, generic parameter context, and optional EH reader separation.
- Can return empty body if parsing fails in some convenience paths, which is powerful but requires careful miss-reason normalization.

**Strength:** flexible decode for difficult artifacts; good for fuzzing/failure taxonomy work.

#### Mono.Cecil

- `MethodDefinition.Body` lazily reads into `Cil.MethodBody`.
- `MethodBody` offers instruction/EH collections and editor-oriented helpers via `ILProcessor`.
- Symbol-driven updates (sequence points / async debug info adjustments) are embedded in the method-body edit lifecycle.

**Strength:** excellent ergonomics for manipulation-centric scenarios; interpretation workflows must avoid accidental mutation assumptions.

#### SRM model

- `MethodBodyBlock` is intentionally low-level: bytecode + metadata tokens + exception regions.
- No high-level mutable instruction graph by default.

**Strength:** deterministic and compact; requires project-owned lifting to reach parity with graph APIs.

---

### 3) Symbol/PDB model and provider strategy

#### AsmResolver

- Dedicated PDB domain model (`PdbImage`, `PdbModule`, symbols, record types).
- Windows PDB (MSF-based) is a first-class parse target; debug data at PE level is exposed via CodeView structures (`RsdsDataSegment`, etc.).
- Clean separation between PE debug directory read and PDB content read.

**Adapter note:** easier to model “PE says which PDB” and “PDB content parse result” as two independently diagnosable steps.

#### dnlib

- Symbol abstraction anchored by `SymbolReader` (initialize, documents, get-method, custom debug info).
- Factory logic can route to portable, managed, embedded portable, or DiaSymReader-backed implementations.
- Contains explicit options controlling DiaSymReader use (`NoDiaSymReader`, `NoOldDiaSymReader` flags), exposing compatibility strategy directly.

**Adapter note:** broad compatibility surface, but behavior matrix expands quickly and needs strict policy presets.

#### Mono.Cecil

- Provider abstraction is explicit (`ISymbolReaderProvider`).
- `PdbReaderProvider` dynamically dispatches between portable and native PDB readers (including embedded portable PDB checks).
- Debug info lands in Cecil-centric types (`MethodDebugInformation`, `SequencePoint`, scope/variable models).

**Adapter note:** concise API, but provider/version behavior should be pinned with tests due to ecosystem drift history.

#### DiaSymReader-focused model

- In this repo’s evidence, DiaSymReader is mostly consumed through dnlib/Cecil interop layers (COM interfaces, native loaders), not as a standalone project API baseline.

**Adapter note:** keep it as a capability module for Windows PDB, not as the canonical abstraction.

---

## Cross-stack concept translation table

| Concept | AsmResolver term/API | dnlib term/API | Mono.Cecil term/API | Normalized contract suggestion |
|---|---|---|---|---|
| Module root | `ModuleDefinition` | `ModuleDefMD` / `ModuleDef` | `ModuleDefinition` | `IMetadataModule` |
| Method body object | `CilMethodBody` | `CilBody`/`MethodBody` | `Cil.MethodBody` | `INormalizedMethodBody` |
| Instruction object | `CilInstruction` | `Instruction` | `Instruction` | `NormalizedInstruction` |
| EH region | `CilExceptionHandler` | `ExceptionHandler` | `ExceptionHandler` | `NormalizedExceptionRegion` |
| PDB root | `PdbImage` | `SymbolReader` + PDB model (`PdbState`, etc.) | symbol reader provider + debug info objects | `IDebugSymbolSource` |
| Sequence point | symbol records mapped via PDB model | `SequencePoint` | `SequencePoint` | `NormalizedSequencePoint` |
| Document/source | PDB source file abstractions | `PdbDocument` / symbol document | `Document` | `NormalizedSourceDocument` |
| Failure channel | reader contexts + error listeners | options/factories + null/empty fallbacks + exceptions | provider mismatch policies | `BackendMissReason` + diagnostics envelope |

---

## Practical interoperability pitfalls (important for onboarding)

1. **Lazy loading semantics differ subtly**
   - All three object-model stacks are lazy in places, but trigger points differ (e.g., method body materialization, debug info hydration).
   - Contract tests should separate “metadata open” from “method body decode” from “symbol projection” costs.

2. **Portable vs Windows PDB branching happens at different layers**
   - AsmResolver: explicit PDB-image domain and CodeView linkage.
   - dnlib/Cecil: provider/factory dispatch with backend-specific fallbacks.
   - Normalization should avoid leaking backend routing details into interpreter-facing APIs.

3. **Error surfacing style is not uniform**
   - Some flows throw immediately; others return null/empty and continue.
   - Our adapters must translate all paths into deterministic miss reasons with provenance.

4. **Mutation-oriented APIs can hide read-only assumptions**
   - Cecil and dnlib contain strong editing workflows; interpreter pipeline should treat read results as immutable snapshots after projection.

---

## Suggested adapter profiles (for faster team onboarding)

### Profile A: “Object-graph first, quickest prototype”

- Primary reader: AsmResolver or dnlib.
- Use high-level instruction/EH models for rapid interpreter wiring.
- Add strict projection layer to freeze normalized contracts early.

### Profile B: “Control-first, long-term stable core”

- Start with SRM-inspired normalized model.
- Use AsmResolver/dnlib only for parity checks and fallback evidence.
- Higher initial cost, lower long-term API drift.

### Profile C: “Windows PDB-heavy environments”

- Keep metadata/IL reader independent.
- Add DiaSymReader-backed module behind `ISymbolSource` capability flag.
- Require explicit diagnostics for “symbol format unsupported/partial”.

---

## Recommended near-term documentation follow-ups

1. Add **API recipe snippets** (one per stack) showing:
   - open module,
   - select one method,
   - enumerate IL + EH,
   - project sequence points.
2. Add a **failure taxonomy appendix** mapping concrete exception/fallback cases in each stack to `BackendMissReason` candidates.
3. Link this note to `adapter-conformance-checklist.md` with mandatory parity scenarios.

---

## Draft design position (updated)

- Keep **AsmResolver** and **dnlib** as primary source-driven onboarding references and prototype accelerators.
- Keep **Mono.Cecil** as an ergonomics and behavior cross-check backend.
- Keep **SRM-style normalized projection** as the long-term contract-stability hedge.
- Treat **DiaSymReader integration** as explicit Windows-PDB capability composition, not core metadata/IL ownership.

This remains provisional until evidence rows in `backend-evidence-log.md` are upgraded for method-body parity, sequence-point parity, and corrupted-artifact miss classification.
