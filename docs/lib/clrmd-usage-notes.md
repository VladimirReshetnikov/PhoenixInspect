# ClrMD usage notes for this project

## Why it matters

ClrMD is the primary candidate for reading runtime state from dumps. In our architecture, it is the front door to:

- process/module inventory,
- thread/frame state,
- managed heap/object traversal,
- runtime method/type handles and related identity hints.

This maps directly to our runtime snapshot and value-provider layers described in integration proposals.

## Snapshot review highlights

The `lib/clrmd/src` snapshot shows a practical split between:

1. `Microsoft.Diagnostics.Runtime` (main public API surface),
2. `Microsoft.Diagnostics.Runtime.Utilities` (native debugger/data-reader support utilities),
3. `Microsoft.Diagnostics.Runtime.Tests` (behavior and regression coverage),
4. sample tools under `src/Samples/*` (usage examples for stack, heap, and root workflows).

For project design, most adapter-facing behavior is concentrated in `Microsoft.Diagnostics.Runtime`.

## Source-level API surfaces relevant to our adapters

### 1) Data-target lifecycle and symbol/file resolution

`DataTarget` centralizes:

- reader ownership (`IDataReader`),
- runtime inventory (`ClrVersions` via `ClrInfo` providers),
- symbol/file lookup through `IFileLocator`,
- PE image caching and disposal behavior.

Design implication:

- treat `DataTarget` as an adapter-internal lifetime root,
- normalize symbol and file-resolution misses into project miss reasons,
- avoid exposing mutable `DataTarget` state to interpreter-facing contracts.

### 2) Runtime materialization and cache flush semantics

`ClrInfo.CreateRuntime(...)` drives DAC binding and runtime creation; `ClrRuntime.FlushCachedData()` explicitly invalidates previously materialized runtime objects.

Design implication:

- cache invalidation must be explicit in our runtime snapshot adapter,
- object identity from prior reads must be treated as stale after flush,
- provenance metadata should record whether values came from pre- or post-flush snapshots.

### 3) Thread and stack enumeration behavior

`ClrRuntime.Threads` and related stack APIs are lazily materialized and bounded in internal loops; tests in `Microsoft.Diagnostics.Runtime.Tests` also emphasize partial/unavailable scenarios.

Design implication:

- always emit completeness metadata for stack-walk outcomes,
- keep deterministic ordering rules in our projected frame lists,
- encode "walk stopped" reasons (budget, corruption, unsupported shape) explicitly.

### 4) Heap/value access boundary

`ClrRuntime.Heap` and heap object APIs provide high-value runtime facts but also imply potentially expensive traversals for object graph operations.

Design implication:

- keep heap operations budgeted and cancellation-aware,
- separate object identity lookup from deep object expansion,
- preserve "known unknown" outcomes when objects cannot be fully materialized.

### 5) CacheOptions as determinism/perf control plane

`CacheOptions` exposes toggles for method/type/field caching, stack/root caching, string caching, and max dump-cache size.

Design implication:

- define project-level cache presets (e.g., deterministic analysis vs interactive exploration),
- include active cache policy in evaluation provenance,
- prevent host-specific defaults from silently changing behavior.

### 6) Runtime diversity and DAC selection pressure

`ClrInfo` encapsulates runtime identity (version/build metadata, DAC lookup paths), and runtime creation can fail or vary based on matching constraints.

Design implication:

- treat runtime identity as first-class input to method-resolution and type-identity logic,
- surface DAC mismatch/lookup failure as explicit normalized error categories,
- keep fallback and retry policy out of core interpreter logic.

## Best-fit responsibilities (project-specific)

ClrMD should own:

1. **Dump runtime observation**
   - enumerate threads, frames, handles, appdomains, modules, and roots.
2. **Runtime fact collection (not semantic execution)**
   - provide runtime facts required for interpretation without embedding interpreter behavior.
3. **Identity-bridge inputs**
   - provide method-table/method-handle/module identity hints for project-owned identity projection.

## Boundary rules refined from source review

- Keep ClrMD types out of core interpreter contracts.
- Normalize to project-owned immutable records at adapter boundaries.
- Represent backend failures explicitly (`NotAvailable`, `SymbolMissing`, `Ambiguous`, etc.).
- Treat enumeration APIs as partial by default and attach completeness/provenance metadata.
- Record runtime/cache configuration in provenance for replayability.

## Risks and design pressure

1. **Runtime/version diversity**
   - different CLR flavors and DAC matches can affect discoverability and shape of runtime facts.
2. **Artifact dependence**
   - runtime inspection alone does not solve source mapping without PE/PDB paths.
3. **Leaky abstractions**
   - exposing ClrMD handles/types upward reduces portability and contract stability.
4. **Walk robustness**
   - damaged dumps can produce truncated stack/root data requiring explicit partial-result handling.
5. **Policy drift**
   - untracked cache/symbol path changes can produce non-reproducible evaluation outcomes.

## Recommended next experiments

1. Build a thin `RuntimeSnapshotAdapter` that projects deterministic records only.
2. Add a budgeted stack-walk wrapper with normalized stop reasons.
3. Validate cache-policy presets against identical dumps to quantify drift.
4. Capture an evidence set for full dump, minidump, and symbol-poor artifact configurations.
5. Add explicit DAC-resolution failure scenarios to conformance documentation.
