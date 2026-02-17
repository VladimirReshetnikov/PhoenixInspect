# PE/PDB reader API comparison and onboarding guide (source-driven draft)

This document compares PE/metadata/IL/PDB reader stacks represented in `lib/` with a specific goal:

- accelerate onboarding when moving between stacks,
- make adapter design choices explicit,
- and ground claims in concrete source APIs rather than package-level marketing language.

This rewrite expands the previous draft with deeper source-level details about:

1. load pipelines and parameter surfaces,
2. method-body decode behavior and failure semantics,
3. symbol routing and PDB format branching,
4. mutability/laziness traps that affect deterministic interpretation,
5. and concrete normalization guidance for our backend-neutral contracts.

---

## Scope and evidence baseline

### Included source snapshots

- **AsmResolver**: `lib/asmresolver/src/AsmResolver.PE*`, `AsmResolver.DotNet*`, `AsmResolver.Symbols.Pdb*`
- **dnlib**: `lib/dnlib/src/DotNet/*`, `lib/dnlib/src/PE/*`
- **Mono.Cecil**: `lib/cecil/Mono.Cecil*`, `lib/cecil/Mono.Cecil.Cil*`, `lib/cecil/symbols/pdb/*`

### Reference-only comparator

- **System.Reflection.Metadata (SRM) + PEReader** remains a design comparator for contract stability and low-level determinism; no local source snapshot is hosted under `lib/`.

### Out-of-scope for this note

- high-level decompiler features,
- editing/rewriting authoring workflows except where they affect read-only semantics,
- language front-end concerns (Roslyn) beyond symbol/IL interoperability boundaries.

---

## Reader stacks and canonical entry points

## 1) AsmResolver

### Module / PE load surface

- `ModuleDefinition.FromFile/FromStream/FromBytes/FromReader/FromDataSource/FromModuleBaseAddress/...`
- `PEImage.FromFile/FromStream/FromBytes/...`
- `PEFile.FromFile/FromStream/FromBytes/...`

The surface is intentionally broad and mostly symmetric across roots.

### Parameter objects

- `ModuleReaderParameters`
  - exposes `MethodBodyReader`, `FieldRvaDataReader`, `PEReaderParameters`, `ModuleResolver`, `RuntimeContext`.
- `PEReaderParameters`
  - exposes `ErrorListener`, `MetadataStreamReader`, `DebugDataReader`, `CertificateReader`, `FileService`, `ReadyToRunSectionReader`.
- `PdbReaderParameters`
  - focused and minimal: `ErrorListener` only.

### PDB root

- `PdbImage.FromFile/FromBytes/FromReader/...` gives a first-class symbol root independent of module load.

**Design implication:** AsmResolver naturally supports “PE discovery step” and “PDB parse step” as separate adapter phases, which aligns well with deterministic diagnostics.

---

## 2) dnlib

### Module load surface

- central root: `ModuleDefMD.Load(...)` with many overloads.
- config via `ModuleCreationOptions` (including PDB options and runtime reader behavior).

### Metadata and decrypter hooks

- `ModuleDefMD` directly exposes metadata streams and tables (`TablesStream`, `StringsStream`, `BlobStream`, `GuidStream`, `USStream`).
- explicit deobfuscation hooks:
  - `IMethodDecrypter MethodDecrypter`
  - `IStringDecrypter StringDecrypter`

### Method body decode surface

- `Emit.MethodBodyReader.CreateCilBody(...)` has many overload families:
  - decode from `DataReader` or raw `byte[]`,
  - optional separate EH reader,
  - explicit `GenericParamContext`,
  - optional `ModuleContext`.

### Symbol root/routing

- symbol abstraction starts at `DotNet.Pdb.Symbols.SymbolReader`.
- routing logic concentrated in `DotNet.Pdb.SymbolReaderFactory`.
- `PdbReaderContext` derives debug-directory context (CodeView guid/age/filename).

**Design implication:** dnlib offers unusually rich low-level control and compatibility routing, but adapter policy must tame option combinations for predictable behavior.

---

## 3) Mono.Cecil

### Module load surface

- `ModuleDefinition.ReadModule(...)` and related factory paths.
- behavior controlled via `ReaderParameters`.

### `ReaderParameters` shape

Important toggles that affect runtime behavior:

- `ReadingMode` (deferred vs immediate),
- `ReadSymbols`,
- `SymbolReaderProvider`,
- `ThrowIfSymbolsAreNotMatching`,
- `InMemory`, `ReadWrite`, projection/resolver knobs.

### IL and method body model

- `MethodDefinition.Body` -> `Mono.Cecil.Cil.MethodBody` with instruction/EH/local collections.
- `ILProcessor` is readily available from method body and strongly mutation-oriented.

### Symbol routing

- generic route: `DefaultSymbolReaderProvider` (portable/native/mdb heuristics, embedded portable support).
- pdb-specific route in snapshot: `symbols/pdb/Mono.Cecil.Pdb/PdbReaderProvider` chooses embedded portable, portable, or native PDB.

**Design implication:** Cecil is very ergonomic for a “load module + read or rewrite” workflow, but interpretation adapters should guard against accidental mutation semantics leaking into read paths.

---

## Quick concept translation (expanded)

| Concept | AsmResolver | dnlib | Mono.Cecil | Normalized contract target |
|---|---|---|---|---|
| Module root | `ModuleDefinition` | `ModuleDefMD` | `ModuleDefinition` | `IMetadataModule` |
| PE root | `PEFile` / `PEImage` | `Metadata.PEImage` path via module | internal `Image` (`ImageReader`) | `IPEArtifact` |
| Method body | `CilMethodBody` | `CilBody` | `Cil.MethodBody` | `INormalizedMethodBody` |
| Instruction collection | `CilInstructionCollection` | `CilBody.Instructions` | `MethodBody.Instructions` | `IReadOnlyList<NormalizedInstruction>` |
| EH collection | `IList<CilExceptionHandler>` | `CilBody.ExceptionHandlers` | `MethodBody.ExceptionHandlers` | `IReadOnlyList<NormalizedExceptionRegion>` |
| Sequence points | PDB model projection | symbol reader projection | `MethodDebugInformation.SequencePoints` | `IReadOnlyList<NormalizedSequencePoint>` |
| Source document | PDB source abstractions | `PdbDocument`-style model | `Document` | `NormalizedSourceDocument` |
| Symbol root abstraction | `PdbImage` | `SymbolReader` | `ISymbolReader` | `IDebugSymbolSource` |
| Error channel | listener + diagnostics | null/exception/factory fallback mix | provider fallback + specific symbol exceptions | `BackendMissReason + DiagnosticsEnvelope` |

---

## Load pipeline comparison in detail

## AsmResolver pipeline profile

1. **Artifact open**
   - raw PE possible (`PEFile`/`PEImage`) or immediate module load (`ModuleDefinition`).
2. **Reader-parameter shaping**
   - configurable error listener and sub-readers at PE layer.
3. **Metadata / method-body decode**
   - method-body parser pluggable through `ModuleReaderParameters.MethodBodyReader`.
4. **PDB parse (optional, independent root)**
   - `PdbImage` can be loaded independently from PE/module.

**Key benefit:** layered architecture makes it straightforward to publish phase-specific diagnostics.

## dnlib pipeline profile

1. **ModuleDefMD.Load(...)** as canonical entry.
2. **Creation options** tune runtime interpretation and PDB behavior.
3. **Metadata + body decode** heavily lazy, with on-demand `MethodDef.Body` materialization.
4. **PDB resolution** through `SymbolReaderFactory` using PE debug data + options.

**Key benefit:** strong control over difficult binaries and obfuscation-adjacent workflows.

**Risk:** option combinatorics and fallback paths can produce subtle behavior drift without a strict policy preset.

## Cecil pipeline profile

1. **Module read** with `ReaderParameters`.
2. **Optional symbol read** from `ReadSymbols` or explicit provider path.
3. **Method body and debug info** resolved through lazily populated object model.
4. **Provider-based symbol branching** (embedded/portable/native/etc.).

**Key benefit:** concise, high-level onboarding.

**Risk:** mutation-first APIs (e.g., `ILProcessor`) encourage editing mental models, which can contaminate deterministic read-only adapter contracts unless explicitly frozen.

---

## Method body decode: semantics and failure behavior

## AsmResolver (`CilMethodBody`)

Notable properties useful for adapter preflight:

- `BuildFlags` with validation-oriented options,
- `ComputeMaxStackOnBuild`,
- `VerifyLabelsOnBuild`,
- explicit helpers (`ComputeMaxStack`, `VerifyLabels` in body lifecycle).

**Adapter recommendation:** run a normalization preflight stage that captures validation failures as structured diagnostics before interpretation starts.

## dnlib (`MethodBodyReader` + `MethodDef.Body`)

Characteristics visible in source:

- large overload surface for controlled decode contexts,
- supports split code/EH readers,
- explicit generic parameter context threading,
- convenience paths may return an empty `CilBody` on caught errors in selected factory paths.

**Adapter recommendation:** never treat empty body as automatically valid. Distinguish:

- true empty IL body,
- decode-failed-and-coerced-empty,
- unsupported encoding/operand scenario.

These should map to different `BackendMissReason` values.

## Cecil (`MethodDefinition.Body`)

Characteristics:

- method body is object-model-centric and editable,
- instruction graph maintenance is integrated (next/previous links and offset maintenance in collections),
- `ILProcessor` makes mutation operations trivially accessible.

**Adapter recommendation:** project to immutable DTOs immediately on read, and discard/avoid retaining mutable Cecil body objects in interpreter runtime state.

---

## Symbol/PDB routing behavior (deeper)

## AsmResolver

- PDB modeled as its own domain (`PdbImage`, module/symbol records).
- RSDS/CodeView data represented at PE debug layer (`RsdsDataSegment` with GUID/age/path parsing).
- clean distinction between:
  1. reading PE debug directory hints,
  2. opening the actual PDB artifact.

**Consequence:** straightforward to emit diagnostics for “linkage metadata present but PDB parse failed” vs “no linkage metadata found.”

## dnlib

- `PdbReaderContext` extracts CodeView info and validates RSDS shape.
- `SymbolReaderFactory` resolves symbol reader kind based on options and data:
  - managed/portable,
  - embedded portable,
  - Windows COM reader paths depending on platform/options.
- logic includes platform checks and explicit option gates (`PdbReaderOptions` flags).

**Consequence:** very capable compatibility matrix, but important to pin policy (e.g., whether COM readers are allowed in a given environment profile).

## Cecil

- `DefaultSymbolReaderProvider` and `PdbReaderProvider` both contain format dispatch logic.
- dispatch commonly checks:
  - embedded portable PDB entries in debug header,
  - portable-vs-native inference from file/stream.
- explicit symbol exceptions exist (`SymbolsNotFoundException`, `SymbolsNotMatchingException`).

**Consequence:** concise API, but behavior depends significantly on provider selection and throw/mismatch flags in `ReaderParameters`.

---

## Laziness, caching, and determinism traps

1. **Body hydration timing differs**
   - Reading module metadata does not equal method-body decode in all stacks.
   - Benchmark and diagnostics should separate “module open latency” from “first body decode latency”.

2. **Symbol hydration timing differs**
   - Some flows load symbol state during module read (`ReadSymbols = true`), others only on explicit symbol reader calls.

3. **Mutable object graphs vs immutable interpretation state**
   - dnlib/Cecil/AsmResolver all expose mutable graph APIs to varying extents.
   - interpretation pipeline must snapshot and freeze normalized representation.

4. **Fallback style mismatch**
   - mix of throw, return-null, return-empty-body, and provider fallback exists across stacks.
   - deterministic host behavior requires immediate normalization to a single failure taxonomy.

---

## Proposed normalized failure taxonomy (adapter-facing)

These reason codes are intended to absorb cross-library differences:

- `ArtifactOpenFailed`
- `InvalidPeShape`
- `MissingCliMetadata`
- `MethodBodyUnreadable`
- `MethodBodyPartiallyDecoded`
- `UnsupportedIlEncoding`
- `OperandResolutionFailed`
- `DebugDirectoryMissing`
- `CodeViewSignatureInvalid`
- `PdbNotFound`
- `PdbFormatUnsupported`
- `PdbGuidAgeMismatch`
- `SymbolReaderUnavailableInEnvironment`
- `SymbolDataCorrupt`
- `SymbolProjectionPartial`

Each normalized miss should carry:

- backend id + backend version,
- backend-native exception (if any),
- phase (`OpenModule`, `DecodeMethodBody`, `OpenSymbols`, `ProjectSequencePoints`, ...),
- artifact identity (PE path/hash, PDB path/hash where known).

---

## Adapter profile guidance (updated)

## Profile A — Rapid object-model prototype

- Prefer AsmResolver or dnlib for initial integration speed.
- Add strict immutable projection layer immediately.
- Use Cecil as parity check for sequence points and odd IL layouts.

## Profile B — Deterministic core with optional rich backends

- Define SRM-like normalized contracts first.
- Implement one high-level backend for velocity (AsmResolver or dnlib).
- Add others only as conformance backends driven by mismatch logs.

## Profile C — Windows-PDB intensive enterprise environments

- Keep metadata/IL path backend-independent.
- Enable platform-specific symbol capabilities explicitly (COM/native readers) behind capability flags.
- Require policy-driven allow/deny decisions for native symbol dependencies.

---

## Onboarding recipes teams should add next

1. **Minimal recipe snippets per stack**
   - open module,
   - pick one method,
   - decode IL + EH,
   - resolve sequence points.

2. **Parity fixture matrix**
   - tiny, fat, and invalid method headers,
   - split EH data,
   - embedded portable PDB,
   - RSDS mismatch and missing PDB,
   - malformed debug directory entries.

3. **Failure mapping appendix**
   - map concrete thrown exceptions / null / empty-body outcomes in each stack to taxonomy codes above.

4. **Policy presets**
   - define deterministic defaults for:
     - symbol reader provider selection,
     - mismatch throwing behavior,
     - platform-specific native reader enablement,
     - diagnostics verbosity.

---

## Updated draft design position

- **AsmResolver + dnlib** remain the strongest source-driven onboarding anchors for PE/metadata/IL/PDB depth.
- **Cecil** remains a high-value ergonomics and cross-check backend, especially for symbol/debug interop and mutation-oriented object-model behavior observations.
- **SRM-style normalized contracts** remain the long-term hedge against backend API drift.
- **Dia/native reader paths** should stay capability-composed and policy-controlled, not assumed as always-available core behavior.

This position should be revisited after we add explicit parity fixtures and promote evidence rows in `docs/lib/backend-evidence-log.md` for body decode and symbol projection mismatch classes.
