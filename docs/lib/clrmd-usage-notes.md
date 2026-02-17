# ClrMD usage notes for this project

## Why it matters

ClrMD is the primary candidate for reading runtime state from dumps. In our architecture, it is the front door to:

- process/module inventory,
- thread/frame state,
- managed heap/object traversal,
- runtime method/type handles and related identity hints.

This maps directly to our runtime snapshot and value-provider layers described in integration proposals.

## Snapshot review highlights

The `lib/clrmd/src` snapshot is organized around three main projects:

1. `Microsoft.Diagnostics.Runtime` (core runtime and dump APIs)
2. `Microsoft.Diagnostics.Runtime.Utilities` (DbgEng helpers and utility interop)
3. `Microsoft.Diagnostics.Runtime.Tests` (conformance/regression coverage for the library itself)

For our design work, almost all required functionality sits in `Microsoft.Diagnostics.Runtime`.

## Source-level API surfaces relevant to our adapters

### 1) Data target and symbol/file resolution lifecycle

`DataTarget` is the root entry point and encapsulates:

- dump/live reader ownership,
- symbol path and file locator behavior,
- PE image caching,
- runtime discovery (`ClrInfo` inventory).

Practical implication: our runtime adapter should wrap `DataTarget` ownership and never leak it upward.

### 2) Runtime materialization and version-specific behavior

`ClrRuntime` is the per-CLR facade that exposes:

- heap, threads, modules, appdomains,
- architecture/pointer-size semantics,
- data-target linkage and cache behavior.

Practical implication: our snapshot contract should capture runtime identity explicitly (CLR flavor/version/build) so cross-dump comparisons remain deterministic.

### 3) Heap and object graph access

`ClrHeap` provides core object/segment enumeration and object lookup APIs. This is essential for:

- evaluating object/field reads,
- provenance tagging for value origin,
- explaining unknown values when objects cannot be materialized.

Practical implication: we should isolate heap reads behind a budget-aware value provider contract to avoid accidental unbounded traversal.

### 4) Thread, frame, and root enumeration semantics

`ClrThread` and `ClrStackFrame` expose stack enumeration and root extraction. The source includes cautionary notes about stack-walk robustness and potential non-termination in corrupted states.

Practical implication: our adapter must enforce explicit frame/root enumeration budgets and convert incomplete walks into normalized partial outcomes.

### 5) Cache behavior knobs that affect determinism/perf

`CacheOptions` controls what ClrMD caches (e.g., stack traces, roots, type info). These settings can trade memory for repeated-call cost.

Practical implication: adapter construction should own cache policy so behavior is stable and host-configurable, not environment-implicit.

## Best-fit responsibilities (project-specific)

For our conceptual design, ClrMD should own:

1. **Dump runtime observation**
   - enumerate threads, stacks, frames, and loaded modules,
   - read memory for runtime-backed value retrieval.
2. **Runtime fact collection, not semantic interpretation**
   - provide raw runtime facts,
   - avoid embedding interpreter semantics in ClrMD adapters.
3. **Identity bridging inputs**
   - expose enough runtime identity data to map to project-owned `ModuleId` and method identity records.

## Boundary rules refined from source review

- Keep ClrMD types out of core interpreter contracts.
- Normalize to project-owned immutable records at adapter boundaries.
- Represent lookup failures explicitly (`NotAvailable`, `SymbolMissing`, `Ambiguous`, etc.) instead of leaking backend-specific exceptions.
- Treat ClrMD enumeration APIs as potentially partial and always attach completeness metadata.

## Risks and design pressure

1. **Version/runtime diversity pressure**
   - dump/runtime differences can affect field layout assumptions and method details.
2. **Artifact dependency pressure**
   - runtime state alone is insufficient for source-accurate stepping; we still need PE/PDB resolution.
3. **Leaky abstractions**
   - if ClrMD-specific handles flow upward, portability to non-dump scenarios decreases.
4. **Walk robustness pressure**
   - stack and root enumeration may degrade on damaged dumps; adapter contract must expose this cleanly.

## Recommended next experiments

1. Build a thin `RuntimeSnapshotAdapter` prototype that only projects deterministic records (no backend types).
2. Add a budgeted stack-walk wrapper and record emitted partial-reason variants.
3. Capture one evidence run for each dump shape: full dump, minidump, and symbol-poor dump.
4. Define cache policy presets (`DeterministicLowMem`, `InteractiveDefault`) and measure behavioral drift.
