# ClrMD Intro Tutorial (for this project)

## Audience and goal

This tutorial is for contributors who are new to ClrMD and need a practical path to use it in our dump-time interpreter design work.

You will learn how to:

1. treat `DataTarget` as the adapter lifetime root,
2. materialize `ClrRuntime` safely and explicitly,
3. enumerate threads/frames/roots/heap with partialness in mind,
4. use cache options as an explicit policy lever,
5. project ClrMD data into backend-neutral contracts.

> Design-phase reminder: `lib/clrmd` is a local source snapshot for study. Do not reference snapshot projects directly from `src/`; rely on NuGet packages and keep adapters isolated.

---

## 1) Mental model: lifecycle root -> runtime -> data surfaces

A practical orientation flow is:

- `DataTarget` (reader ownership, symbol/file location, cache policy)
- `ClrInfo` (runtime identity and creation capability)
- `ClrRuntime` (threads, heap, modules, handles, etc.)
- `ClrThread` / `ClrHeap` (high-value traversal surfaces)

For our architecture, this naturally maps to:

- runtime session adapter,
- stack/frames adapter,
- heap/object adapter,
- provenance and miss-reason normalization layer.

## 2) Source-backed files worth reading first

If you have one hour, start with these files in order:

1. `src/Microsoft.Diagnostics.Runtime/DataTarget.cs`
2. `src/Microsoft.Diagnostics.Runtime/ClrInfo.cs`
3. `src/Microsoft.Diagnostics.Runtime/ClrRuntime.cs`
4. `src/Microsoft.Diagnostics.Runtime/ClrThread.cs`
5. `src/Microsoft.Diagnostics.Runtime/ClrHeap.cs`
6. `src/Microsoft.Diagnostics.Runtime/CacheOptions.cs`

These cover most behavior we need for deterministic dump-time data projection.

## 3) Step-by-step workflow mapped to our adapter boundaries

### Step A: open dump context and set symbol/file policy

`DataTarget` owns reader + locator + cache settings. It can use `_NT_SYMBOL_PATH` defaults, but our adapter should avoid hidden environment dependence in deterministic modes.

Project guidance:

- construct `DataTarget` through one policy-owned entry point,
- log effective symbol/file policy in provenance,
- avoid leaking `DataTarget` beyond adapter lifetime.

### Step B: select runtime intentionally

`ClrInfo` represents available runtimes and runtime creation path. Runtime materialization can fail due to DAC mismatch/artifact issues.

Project guidance:

- persist runtime identity metadata (version/build hints),
- normalize runtime creation failures into stable categories,
- do not let fallback/retry logic leak into interpreter core.

### Step C: enumerate threads and stacks as partial-by-default

Thread/frame APIs are high-value but may stop early because of unreadable memory, corruption, unsupported frame shapes, or policy budget limits.

Project guidance:

- always output completeness metadata,
- include deterministic ordering and stop reasons,
- preserve partial results rather than failing whole requests.

### Step D: query heap with explicit budget boundaries

`ClrHeap.EnumerateObjects(...)` exposes careful vs faster traversal modes and can encounter invalid/incomplete objects.

Project guidance:

- separate cheap identity lookup from deep graph traversal,
- require budget/cancellation for expensive walks,
- project unknown/invalid object states explicitly.

### Step E: control caching as a declared contract input

`CacheOptions` controls type/field/method caching, stack/root caching, string caching, and max dump cache size.

Project guidance:

- define named cache profiles (e.g., deterministic vs exploratory),
- include active profile in every evaluation context,
- treat cache-profile changes as potential output-shape changes.

## 4) Source-level details to encode in our contracts

### `DataTarget` has PE image cache and disposal semantics

The source shows internal PE image caching keyed by file identity and explicit disposal cleanup. Adapter records should never expose cached image handles.

### `ClrRuntime.FlushCachedData()` can invalidate prior backend objects

When flush is used, previously materialized handles/types/frames may no longer be semantically fresh.

Contract implication:

- attach snapshot generation IDs to projected records,
- avoid mixing pre-flush and post-flush data in one logical result.

### `ClrHeap.EnumerateObjects(bool carefully)` expresses quality/perf trade-off

This is a direct policy knob we should expose as controlled behavior, not implicit heuristic behavior.

## 5) Common contributor pitfalls and project-safe defaults

1. **Pitfall:** letting symbol/file policy come from ambient environment accidentally.  
   **Default:** deterministic profiles should set explicit locator policy.
2. **Pitfall:** returning ClrMD handles/types from adapter APIs.  
   **Default:** convert to immutable project DTOs at boundary.
3. **Pitfall:** treating traversal success as complete data.  
   **Default:** always include completeness + stop reason fields.
4. **Pitfall:** changing cache options without recording it.  
   **Default:** include cache-profile identity in provenance fingerprint.

## 6) Suggested onboarding exercise (1-2 hours)

1. Open one representative dump and create runtime snapshot record.
2. Enumerate threads and collect frame counts + completeness flags.
3. Enumerate a bounded heap slice in fast and careful modes.
4. Compare results under two cache profiles.
5. Publish a short evidence table in `docs/lib/backend-evidence-log.md`:
   - profile,
   - query,
   - completeness,
   - stop reason,
   - confidence.

## 7) Where to read next

- `docs/lib/clrmd/usage-notes.md` for design boundaries and risks.
- `docs/lib/clrmd/source-scan.md` for source-backed lifecycle and policy details.
- `docs/proposals/integration/clrmd-integration-proposal.md` for architecture placement.
