# Mono.Cecil usage notes for this project

## Why it matters

Mono.Cecil is a mature .NET assembly inspection and rewriting library with first-class CIL and symbol abstractions.

For this project, it is especially relevant as:

- a metadata + IL projection candidate for dump-time analysis,
- a rich cross-check backend when comparing adapter behavior across libraries,
- a practical source of edge-case semantics (generic context, debug info, symbol mismatch handling).

## Snapshot orientation

The `lib/cecil` snapshot is organized as a compact multi-project solution centered on `Mono.Cecil`:

- `Mono.Cecil`, `Mono.Cecil.Cil`, `Mono.Cecil.Metadata`, `Mono.Cecil.PE`: core read/write + CIL + metadata table/heap + PE support.
- `symbols/pdb` and `symbols/mdb`: optional symbol providers and tests.
- `rocks/Mono.Cecil.Rocks`: convenience extension APIs for IL and definition manipulation.
- `Test/Mono.Cecil.Tests`: broad behavioral regression suite for metadata, CIL, symbols, and resolver paths.

Design implication:

- Cecil exposes a complete "single-stack" experience (metadata + CIL + symbols + write path), so adapter boundaries must be explicit to avoid backend lock-in.

## Source-level surfaces with direct adapter impact

### 1) Reader policy is centralized in `ReaderParameters`

`ReaderParameters` controls core ingest behavior such as:

- reading mode (`Immediate` vs `Deferred`),
- symbol path and provider (`ReadSymbols`, `SymbolReaderProvider`, `SymbolStream`),
- symbol strictness (`ThrowIfSymbolsAreNotMatching`),
- resolver injection (`AssemblyResolver`, `MetadataResolver`),
- projection toggles (`ApplyWindowsRuntimeProjections`),
- read/write and in-memory loading modes.

Design implication:

- treat reader-parameter presets as versioned policy objects in our adapter layer.
- avoid implicit defaults in host integrations; persist preset identity in provenance.

### 2) Deferred vs immediate load behavior is explicit and architecturally meaningful

`ModuleReader` selects `ImmediateModuleReader` or `DeferredModuleReader` based on `ReadingMode`, and immediate mode eagerly walks deeper metadata/member surfaces.

Design implication:

- deterministic replay should include load mode because it affects materialization timing and cost profile.
- adapter conformance tests should assert equivalent normalized output under both modes when data is valid.

### 3) Method-body decode path is lazy and tied to module readers

`MethodDefinition.Body` reads via module reader callbacks; the body model (`MethodBody`) includes instruction lists, locals, exception handlers, and `ILProcessor` mutation support.

Design implication:

- normalize Cecil method bodies to immutable project DTOs before interpreter consumption.
- capture decode status and provenance separately from mutable Cecil objects.

### 4) Symbol selection uses multi-path fallback behavior

`DefaultSymbolReaderProvider` branches through embedded portable PDB, standalone portable PDB, native PDB provider, and MDB provider depending on headers/files/availability.

Design implication:

- symbol resolution behavior must be policy-controlled and observable.
- always emit selected symbol path + fallback reason/miss reason in debug-map provenance.

### 5) Resolver model is extensible but can drift without guardrails

`BaseAssemblyResolver`/`DefaultAssemblyResolver` plus `MetadataResolver` provide customizable type/member resolution behavior, including exported type and forwarded type handling.

Design implication:

- keep resolver configuration explicit in adapter policy and test fixtures.
- map unresolved references to project-owned miss categories rather than backend-specific exception details.

## Potential project roles

1. **Parity backend for metadata/CIL projection checks**
   - Compare normalized output against AsmResolver and dnlib for confidence in backend-neutral contracts.
2. **Symbol-path behavior reference**
   - Exercise embedded portable + standalone portable + mismatch scenarios.
3. **Resolver stress backend**
   - Validate type-forwarder/exported-type behavior and miss-reason taxonomy.
4. **Write-path experimentation source (design-only)**
   - Useful for future prototype experiments around transformed method bodies and deterministic output constraints.

## Boundary guidance

- Keep Cecil types confined to adapter internals.
- Do not let mutable `MethodBody`/`ILProcessor` semantics leak into core interpreter contracts.
- Treat symbol/provider and resolver selection as explicit policy inputs.
- Encode symbol mismatch, missing symbol, and unresolved member states as first-class non-success outcomes.

## Risks and design pressure

1. **Rich mutable object model pressure**
   - convenient mutability can blur the line between analysis projection and transformation.
2. **Fallback complexity in symbols**
   - implicit provider probing can hide reproducibility differences.
3. **Resolver-environment coupling**
   - search paths and runtime context affect outcomes if not pinned.
4. **Backend convenience leakage**
   - project contracts may drift toward Cecil concepts if normalization is weak.

## Recommended next experiments

1. Define named Cecil reader-policy presets (`strict-symbols`, `lenient-symbols`, `deferred-fastscan`) and document them.
2. Run cross-backend parity tests for method-body + sequence-point projection on generic-heavy samples.
3. Add conformance fixtures for symbol mismatch and symbol-absent outcomes.
4. Validate type-forwarder/exported-type resolution outcomes against project miss taxonomy.
5. Compare load-time and memory profile impact of immediate vs deferred reading under the same corpus.
