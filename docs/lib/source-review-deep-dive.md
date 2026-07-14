# Library source review deep dive (snapshot-based, design phase)

> **Historical source research (2026-02).** This comparison preserves library behavior and earlier trade-offs; it is not a current backend-selection exercise. Use `mvp-backend-decision-record.md` for the active SRM/PEReader choice and `backend-evidence-log.md` for executable evidence.

This document captures a deeper, source-driven review of the library snapshots under `lib/` and translates those findings into adapter and documentation implications for this project.

It complements the per-library usage notes with a shared, side-by-side review format that is easier to use during architecture and backend decision discussions.

## Review scope and method

- Reviewed source under:
  - `lib/clrmd/src`
  - `lib/asmresolver/src`
  - `lib/dnlib/src`
  - `lib/cecil`
  - `lib/roslyn/src/Compilers/CSharp/Portable`
- Focused on entry points, lifecycle controls, method-body/symbol pipelines, and failure/partial-result behavior.
- Treated all findings as **design guidance**, not implementation commitments.

## ClrMD deep findings

### Entry-point and lifecycle surfaces

Key source surfaces reviewed:

- `DataTarget` lifecycle + symbol/dump loading (`LoadDump(...)`, `SetSymbolPath(...)`, internal PE cache behavior).
- `ClrInfo.CreateRuntime(...)` runtime materialization path.
- `ClrRuntime.FlushCachedData()` cache invalidation boundary.

Design implications:

- Model adapter lifetimes explicitly around `DataTarget` and runtime creation/disposal boundaries.
- Include cache-flush generation/version markers in runtime snapshot provenance.
- Normalize symbol-path and PE lookup failures into explicit miss reasons rather than exposing raw backend exceptions.

### Enumeration and boundedness pressure

Key source surfaces reviewed:

- `ClrRuntime` thread enumeration and refresh behavior.
- `ClrThread.EnumerateStackTrace(...)` and `ClrThread.EnumerateStackRoots()` including max-frame and caching behavior.
- `ClrHeap.EnumerateObjects(...)` and careful vs non-careful object iteration APIs.

Design implications:

- Our contracts should require completeness metadata for thread/frame/root and heap traversals.
- Budget and cancellation should wrap every potentially long-running enumeration path.
- Partial traversal states should be normalized (budget stop, corruption suspicion, unsupported shape, unavailable data).

### Cache policy as architectural input

Key source surfaces reviewed:

- `CacheOptions` usage across type/field/method/stack/root caching paths.

Design implications:

- Treat cache policy as an explicit input to deterministic replay (not an invisible runtime detail).
- Add fixed cache profiles for reproducible analysis vs exploratory analysis.

## AsmResolver deep findings

### Layering and load customization seams

Key source surfaces reviewed:

- `ModuleDefinition.FromFile/FromStream/FromBytes(...)` front-door load APIs.
- `Serialized/ModuleReaderParameters` (working directory, module resolver, method-body reader, PE reader parameters, runtime context).
- `Serialized/ModuleReaderContext` for read pipeline context ownership.

Design implications:

- AsmResolver exposes clear extension seams for deterministic read-policy profiles.
- Adapter design should codify a minimal parameter profile and avoid host-by-host drift.

### CIL body model and decode behavior

Key source surfaces reviewed:

- `Code/Cil/CilMethodBody` (lazy initialization, instruction + EH + locals model, build flags).
- `Serialized/DefaultMethodBodyReader` and `Serialized/SerializedMethodDefinition.GetMethodBody()` flow.

Design implications:

- Projection should carry decode-completeness flags to preserve lazy/partial decode semantics.
- Keep a normalized representation of IL + EH + locals that can be populated from multiple backends.

### PDB surface breadth

Key source surfaces reviewed:

- `AsmResolver.Symbols.Pdb/PdbImage` and MSF-backed loading paths from file, bytes, reader.
- Type/leaf-record lookup (`TryGetLeafRecord(...)`) and symbol record traversal capabilities.

Design implications:

- AsmResolver can serve both metadata/CIL and symbol projections in one backend option.
- Still enforce backend-neutral debug-map normalization to avoid lock-in to AsmResolver symbol terminology.

## dnlib deep findings

### Module creation policy controls

Key source surfaces reviewed:

- `ModuleCreationOptions` (`PdbOptions`, `PdbFileOrData`, `TryToLoadPdbFromDisk`, runtime reader kind).
- `ModuleDefMD.Load(...)` overload family.

Design implications:

- dnlib provides strong policy control for symbol loading behavior, useful for deterministic adapter testing.
- Establish project-owned defaults for PDB loading to keep behavior reproducible across environments.

### Metadata and IL projection surfaces

Key source surfaces reviewed:

- `ModuleDefMD` lazy metadata stream/table materialization and high-level row access.
- `MethodDef` + `CilBody` high-level method representation.

Design implications:

- dnlib remains a practical parity backend for normalized method-body and metadata identity projection.
- Keep backend-specific conveniences internal to avoid contract drift.

### Portable PDB decoding detail

Key source surfaces reviewed:

- `DotNet/Pdb/Portable/PortablePdbReader.GetMethod(...)` flow.
- Sequence-point decoding behavior (delta offsets, hidden points, document records, local scope reconstruction).

Design implications:

- Our debug-map model should preserve hidden sequence points and document-switch semantics.
- Symbol-scope/import/async kick-off metadata should be represented as optional structured data, not dropped.


## Mono.Cecil deep findings

### Reader policy and load-mode controls

Key source surfaces reviewed:

- `Mono.Cecil/ModuleDefinition.cs` (`ReaderParameters`, `ReadingMode`).
- `Mono.Cecil/AssemblyReader.cs` (`ModuleReader.CreateModule(...)`, immediate/deferred reader split).

Design implications:

- Cecil reader policy should be captured as a deterministic adapter preset (mode, symbol policy, resolver wiring, projections).
- Immediate vs deferred behavior needs explicit parity testing to keep normalized contracts stable.

### Method-body and symbol pipeline behavior

Key source surfaces reviewed:

- `Mono.Cecil/MethodDefinition.cs` and `Mono.Cecil.Cil/MethodBody.cs` (lazy body access + mutable instruction/EH model).
- `Mono.Cecil.Cil/Symbols.cs` and `Mono.Cecil.Cil/PortablePdb.cs` (provider fallback tree and portable/embedded PDB handling).

Design implications:

- Keep Cecil mutable method-body/symbol objects confined to adapter internals.
- Normalize symbol-path branch selection and mismatch/no-symbol outcomes into explicit provenance and miss reasons.

### Resolver-path detail and edge cases

Key source surfaces reviewed:

- `Mono.Cecil/BaseAssemblyResolver.cs`, `Mono.Cecil/DefaultAssemblyResolver.cs`, `Mono.Cecil/MetadataResolver.cs`.
- `Test/Mono.Cecil.Tests/ResolveTests.cs` (forwarders/exported-type/unresolved-shape scenarios).

Design implications:

- Resolver profiles should be explicit and replayable (search directories + known assemblies).
- Forwarder loops and unresolved member shapes should map to project taxonomy, not backend-specific exceptions.


## Roslyn deep findings

### Parse gateway behavior

Key source surfaces reviewed:

- `SyntaxFactory.ParseExpression(...)`, `ParseTypeName(...)`, `ParseStatement(...)`.
- `SyntaxFactory.ParseSyntaxTree(...)` overload set and parse-option/path metadata handling.

Design implications:

- `consumeFullText` semantics are important for debugger-style expression strictness and should be policy-controlled.
- Deterministic parse inputs must include parse options, language version, and source-path metadata.

### Compilation and semantic model boundaries

Key source surfaces reviewed:

- `CSharpCompilation.Create(...)` and `CreateScriptCompilation(...)` mode split.
- `GetSemanticModel(...)` requiring tree membership in the compilation and honoring options.

Design implications:

- Semantic assistance should remain optional enrichment with explicit confidence/provenance.
- Front-end contract should capture compilation mode and reference set to avoid replay drift.

### Recovery complexity and normalization need

Key source surfaces reviewed:

- `Parser/LanguageParser` terminator-state and recovery pathways.

Design implications:

- We should normalize parser diagnostics/recovery outcomes into project-owned categories.
- Avoid coupling interpreter semantics to Roslyn internal recovery behavior.

## Cross-library synthesis updates

### New synthesis points from this deep dive

1. **Policy objects are first-class architecture assets.**
   - All five libraries expose knobs that materially affect behavior; adapter contracts should capture those knobs in deterministic request context.
2. **Partialness is a normal outcome, not an edge case.**
   - Each library has meaningful partial/optional/missing pathways (cache invalidation, lazy decode, symbol fallbacks, parser recovery).
3. **Debug-map fidelity requires preserving nuance.**
   - Hidden sequence points, document transitions, and scope/import metadata should remain representable in project contracts.
4. **A strict normalization layer is non-negotiable.**
   - Rich object models are useful internally but must not shape external interpreter contracts.

### Documentation follow-up tasks

- Add explicit deterministic policy-schema draft for:
  - runtime/cache policy,
  - metadata reader policy,
  - symbol reader policy,
  - expression parse/bind policy.
- Extend conformance scenarios to include:
  - cache-flush drift checks,
  - hidden-sequence-point handling parity,
  - parser strict vs recovery mode comparisons.

## Source-tour execution guidance (new)

To keep future expansions consistent, pair this deep-dive note with `docs/lib/source-tour-workbook.md`:

- use the workbook for per-library source-reading tasks and evidence capture,
- use this deep dive for cross-library synthesis and architecture implications,
- update both when new snapshot reviews materially change policy or normalization guidance.

This split keeps tutorial onboarding actionable while preserving a stable cross-library design narrative.
