# ClrMD source scan notes (snapshot: `lib/clrmd`)

This note records a source-driven scan of ClrMD focused on adapter-relevant behavior for dump-time evaluation.

## What was reviewed

Primary files and surfaces reviewed:

- `src/Microsoft.Diagnostics.Runtime/DataTarget.cs`
- `src/Microsoft.Diagnostics.Runtime/ClrInfo.cs`
- `src/Microsoft.Diagnostics.Runtime/ClrRuntime.cs`
- `src/Microsoft.Diagnostics.Runtime/ClrThread.cs`
- `src/Microsoft.Diagnostics.Runtime/ClrHeap.cs`
- `src/Microsoft.Diagnostics.Runtime/CacheOptions.cs`

## Structural observations

- The public entry surface is strongly centered around lifecycle roots (`DataTarget` -> `ClrInfo` -> `ClrRuntime`).
- Caching and symbol/file lookup policy are explicit in constructor-time and runtime APIs, rather than hidden in opaque static singletons.
- Enumeration-heavy operations (stack roots, stack frames, heap objects) are exposed as iterators and can naturally yield partial results if the dump is incomplete or corrupted.

## Source-backed findings

## 1) `DataTarget` is the operational root and policy carrier

Observed behavior in `DataTarget`:

- Owns data reader and cache options.
- Builds default symbol/file lookup from `_NT_SYMBOL_PATH` when no explicit locator is provided.
- Maintains an internal PE image cache keyed by module identity traits.
- Exposes explicit disposal that clears PE cache and tears down owned data-target resources.

Design implication:

- Treat `DataTarget` as a short-lived adapter session object.
- Never leak `DataTarget` or `PEImage` references outside adapter boundaries.
- Capture symbol-path and file-locator policy in provenance so replay can reproduce behavior.

## 2) Runtime creation is explicit and version-dependent

Observed behavior:

- `ClrInfo` represents runtime identity and runtime creation path.
- Runtime creation requires DAC compatibility and can fail with environment-dependent causes.

Design implication:

- Persist runtime identity input alongside every snapshot.
- Normalize runtime-creation failures into project-defined categories (e.g., dac-mismatch, artifact-missing, unsupported-runtime-shape).

## 3) Cache behavior is configurable and semantically relevant

Observed behavior in `CacheOptions`:

- Separate toggles for type/field/method caches and stack/root caches.
- String-name caching strategy is configurable.
- Dump-memory cache size and OS-backed memory features are policy knobs.

Design implication:

- Define named cache-policy profiles (deterministic vs exploratory).
- Include active cache profile in every evaluation context.
- Add tests that compare outcomes across profile changes to detect determinism drift.

## 4) Stack and thread APIs are naturally partial

Observed behavior across `ClrRuntime` and `ClrThread`:

- Thread and frame access patterns are lazy and may depend on data availability and cache state.
- Stack-root and stack-frame enumeration is a high-value path but susceptible to missing data or walk failures.

Design implication:

- Our stack projection contracts should always include completeness metadata.
- Normalized stop reasons should include at least: budget exceeded, unreadable memory, unsupported frame shape, and backend failure.

## 5) Heap enumeration cost and quality trade-offs are explicit

Observed behavior in `ClrHeap`:

- `EnumerateObjects(bool carefully)` directly exposes a speed vs resiliency trade-off.
- Object materialization may return invalid/unknown-type outcomes for bad addresses.

Design implication:

- Heap queries in our architecture should require budget/cancellation inputs.
- Object projection must encode unknown/invalid type states rather than throwing away records.

## Adapter follow-through checklist

- Add runtime session envelope carrying: runtime identity, cache profile, symbol policy, and dump-reader mode.
- Add unified stop-reason taxonomy for thread/frame/root/heap traversal.
- Ensure every ClrMD exception path is converted to stable miss reasons.
- Add reproducibility checks that run the same query twice (same profile) and assert equivalent projection output.

## Confidence and caveats

- Confidence is high for lifecycle/policy surfaces because they are explicit public APIs.
- Confidence is medium for all behavior under corrupted/minimal dumps; this should be validated with focused adapter spikes and synthetic fixtures.
