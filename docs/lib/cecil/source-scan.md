# Mono.Cecil source scan notes (snapshot: `lib/cecil`)

This note records a source-driven scan of Mono.Cecil focused on load policy, metadata/CIL projection, symbol ingestion, and resolver behavior.

## What was reviewed

Primary files and surfaces reviewed:

- `Mono.Cecil/ModuleDefinition.cs`
- `Mono.Cecil/AssemblyReader.cs`
- `Mono.Cecil/MethodDefinition.cs`
- `Mono.Cecil/BaseAssemblyResolver.cs`
- `Mono.Cecil/DefaultAssemblyResolver.cs`
- `Mono.Cecil/MetadataResolver.cs`
- `Mono.Cecil.Cil/Symbols.cs`
- `Mono.Cecil.Cil/CodeReader.cs`
- `Mono.Cecil.Cil/PortablePdb.cs`
- `Test/Mono.Cecil.Tests/MethodBodyTests.cs`
- `Test/Mono.Cecil.Tests/ResolveTests.cs`

## Structural observations

- Cecil’s core package spans metadata, CIL, PE, and symbol handling with coordinated policy objects.
- Reader/writer behavior is policy-rich but centralized around explicit parameter classes.
- The test suite exercises many edge behaviors (method bodies, resolution, forwarders, symbol conditions), making Cecil a strong reference source for adapter conformance scenarios.

## Source-backed findings

## 1) Module ingest and policy wiring are centralized in `ModuleReader.CreateModule(...)`

Observed behavior:

- `CreateModule(...)` injects resolver/importer providers from `ReaderParameters`.
- Metadata kind selection respects `ApplyWindowsRuntimeProjections`.
- Symbol read path is coordinated in module creation flow.
- `ReadingMode.Immediate` clears metadata-system caches after eager read.

Design implication:

- represent ingest policy as a first-class adapter request object.
- include projection mode and symbol strictness in provenance to make replay and diagnostics deterministic.

## 2) Reading mode (`Immediate` vs `Deferred`) is a real semantic/operational switch

Observed behavior:

- `ModuleReader` chooses `ImmediateModuleReader` or `DeferredModuleReader` from `ReadingMode`.
- Immediate reader traverses references/resources/types and associated attributes/security declarations eagerly.
- Deferred mode leaves more materialization to lazy read paths.

Design implication:

- capture load mode in evidence logs and parity runs.
- include boundedness checks in deferred scenarios to avoid accidental deep scans in dump-time workflows.

## 3) Method bodies are lazy-loaded and mutable by design

Observed behavior:

- `MethodDefinition.Body` reads through module reader callbacks when needed.
- `MethodBody` exposes instructions, locals, exception handlers, and ILProcessor-driven mutation pathways.
- test fixtures cover branches, switches, function pointers, EH layout, and `this` parameter handling.

Design implication:

- normalize to immutable backend-neutral method-body records before interpreter use.
- preserve decode/mapping fidelity (locals, EH, operand shape) plus explicit partialness metadata.

## 4) Symbol loading is branchy and environment-sensitive

Observed behavior:

- `DefaultSymbolReaderProvider` probes embedded portable PDB, standalone portable PDB, native PDB, and MDB pathways.
- provider behavior differs between file-based and stream-based inputs.
- symbol errors include explicit exception types (`SymbolsNotFoundException`, `SymbolsNotMatchingException`).
- `PortablePdbReader` validates matching entries and populates method/module debug info surfaces.

Design implication:

- make symbol-reader policy explicit in adapter contracts (preferred paths + fallback allowances).
- always record selected branch and fallback/mismatch reason for diagnostic explainability.

## 5) Resolver chain behavior is broad and test-covered

Observed behavior:

- `MetadataResolver` resolves type/field/method/member references via assembly/module scopes.
- `DefaultAssemblyResolver` extends `BaseAssemblyResolver` search directory behavior.
- tests cover type forwarding, nested forwarding, exported-type resolution, and unresolved cases (arrays/function pointers/generic parameters).

Design implication:

- align resolver outputs with our miss taxonomy (`UnresolvedScope`, `ForwarderLoop`, `UnsupportedShape`, etc.).
- include resolver policy identity and search context in deterministic replay bundles.

## 6) Portable PDB path is integrated, not bolt-on

Observed behavior:

- `PortablePdbReaderProvider` and `EmbeddedPortablePdbReaderProvider` are first-class providers.
- `PortablePdbReader.Read(...)` loads sequence points, scopes, kickoff methods, and custom debug information.
- embedded PDB support includes decompression + provider wrapping logic.

Design implication:

- preserve sequence points/scopes/kickoff metadata in debug-map schema.
- keep symbol-origin provenance explicit (`embedded-portable`, `standalone-portable`, etc.).

## Adapter follow-through checklist

- Define and version Cecil reader-policy presets (mode, symbols, resolver settings, projection flags).
- Add conformance tests covering immediate vs deferred parity for normalized outputs.
- Add symbol-path provenance and mismatch/no-symbol miss reasons to projection records.
- Keep all Cecil mutable object models confined to adapter internals.
- Add resolver edge scenarios (forwarders/exported types/unresolved) to backend parity matrix.

## Confidence and caveats

- Confidence is high for load/symbol/resolver findings due to explicit source surfaces and broad tests.
- Confidence is medium for stress behavior under highly corrupted binaries until corpus-driven spikes validate miss classification quality.
