# dnlib source scan notes (snapshot: `lib/dnlib`)

This note records a source-driven scan of dnlib focused on module creation policy, metadata/IL projection, and Portable PDB decoding paths.

## What was reviewed

Primary files and surfaces reviewed:

- `src/DotNet/ModuleDefMD.cs`
- `src/DotNet/ModuleCreationOptions.cs`
- `src/DotNet/MethodDef.cs`
- `src/DotNet/Pdb/Portable/PortablePdbReader.cs`

## Structural observations

- dnlib centers around `ModuleDefMD` with extensive lazy metadata row projection.
- Module creation behavior is policy-rich and explicit through `ModuleCreationOptions`.
- Method bodies and symbol data are loaded lazily and can be explicitly freed/reloaded in some paths.

## Source-backed findings

## 1) Module creation options directly influence symbol/runtime behavior

Observed behavior in `ModuleCreationOptions`:

- Explicit `PdbOptions`, `PdbFileOrData`, and `TryToLoadPdbFromDisk` controls.
- Runtime-reader selection (`CLR` vs `Mono`) is a first-class knob.

Design implication:

- Treat module creation options as required adapter inputs (not defaults hidden in implementation).
- Persist runtime-reader choice in provenance for deterministic replay and mismatch analysis.

## 2) `ModuleDefMD` is highly lazy and stream-backed

Observed behavior:

- Metadata streams (`TablesStream`, `StringsStream`, `BlobStream`, etc.) are exposed from module state.
- Many collections are lazy-initialized and materialized on demand.

Design implication:

- Adapter projections should avoid holding live dnlib objects beyond extraction windows.
- Add projection completeness markers when rows/streams are unavailable or malformed.

## 3) Method body lifecycle allows freeing/reloading

Observed behavior in `MethodDef`:

- Method body initialization is lazy.
- `FreeMethodBody()` can drop loaded body state when supported.

Design implication:

- If using dnlib in long-running tools, treat method-body caching policy as explicit and measurable.
- For deterministic runs, avoid policy that depends on incidental reuse of mutable method objects.

## 4) Portable PDB reader preserves nuanced sequence-point/scope data

Observed behavior in `PortablePdbReader`:

- Validates module/PDB match metadata.
- Decodes hidden sequence points, delta-encoded offsets/locations, local scopes, import scopes, locals, constants, and custom debug info.

Design implication:

- Our debug-map model must represent hidden points, document transitions, and nested scopes without lossy flattening.
- Import-scope and custom-debug-info payloads should be optional structured subgraphs, not discarded text blobs.

## Adapter follow-through checklist

- Define dnlib loader policy profile with explicit PDB/runtime settings.
- Add conformance fixtures for mismatched PDB identity and missing-PDB fallback.
- Validate method-body projection parity across repeated extraction cycles.
- Include hidden-sequence-point and nested-scope fidelity checks in debug-map conformance suite.

## Confidence and caveats

- Confidence is high for policy and lazy-loading behavior due to direct source visibility.
- Confidence is medium for malformed/obfuscated edge cases until benchmark corpus tests are executed.
