# ClrMD APIs tutorial (project-focused usage scenarios)

> **Lifecycle:** Draft · **Roadmap:** Reference

## Purpose and audience

This document is a practical tutorial for how we expect to use ClrMD in this project’s dump-time evaluation pipeline.
It is intentionally scenario-first: each section maps ClrMD APIs to one of our architecture responsibilities, with draft code snippets that illustrate boundaries rather than final implementation details.

> Draft status note: API names and exact adapter shapes are examples, not commitments.

---

## 1) Mental model: where ClrMD fits

In this project, ClrMD is the runtime-observation backend. It should answer questions about **what existed in memory** at dump time, while our metadata/symbol backend answers **what code and symbols describe that memory**.

Use ClrMD for:

- loading dump targets and creating a runtime,
- enumerating modules, threads, stacks, heap objects,
- reading runtime-backed memory and identity hints,
- gathering method IL location hints when available.

Do not use ClrMD as the core interpreter contract. Normalize data into project-owned records at adapter boundaries.

---

## 2) Scenario A: Open dump and initialize runtime snapshot access

### Goal

Create a stable session object that owns `DataTarget`, one selected `ClrRuntime`, and project-owned snapshot metadata.

### Key ClrMD APIs

- `DataTarget.LoadDump(...)`
- `DataTarget.ClrVersions`
- `ClrInfo.CreateRuntime(...)`

### Draft pattern

```csharp
using Microsoft.Diagnostics.Runtime;

public sealed class ClrmdSession : IDisposable
{
    public DataTarget Target { get; }
    public ClrRuntime Runtime { get; }

    private ClrmdSession(DataTarget target, ClrRuntime runtime)
    {
        Target = target;
        Runtime = runtime;
    }

    public static ClrmdSession Open(string dumpPath)
    {
        var target = DataTarget.LoadDump(dumpPath);

        // In early phases we select the first CLR, but keep this policy explicit.
        var clr = target.ClrVersions.Single();
        var runtime = clr.CreateRuntime();

        return new ClrmdSession(target, runtime);
    }

    public void Dispose() => Target.Dispose();
}
```

### Project guidance

- Keep runtime-selection policy explicit (single CLR assumption may fail for edge dumps).
- Treat session lifetime as deterministic and disposable.
- Convert setup failures into project miss reasons (`RuntimeUnavailable`, `UnsupportedRuntimeFlavor`) rather than bubbling raw backend exceptions.

---

## 3) Scenario B: Build module identity bridge inputs

### Goal

Enumerate loaded modules and normalize identity fields needed by our `ModuleId` and `MethodId` mapping.

### Key ClrMD APIs

- `ClrRuntime.Modules`
- `ClrModule` identity properties (name/path/base address metadata as available in the dump/runtime)

### Draft pattern

```csharp
public readonly record struct RuntimeModuleRecord(
    string DisplayName,
    ulong ImageBase,
    int Size,
    Guid? MvidHint,
    string? FileName);

public static IReadOnlyList<RuntimeModuleRecord> CaptureModules(ClrRuntime runtime)
{
    return runtime.Modules
        .Select(m => new RuntimeModuleRecord(
            DisplayName: m.Name,
            ImageBase: m.ImageBase,
            Size: m.Size,
            MvidHint: null, // fill later from PE metadata pass
            FileName: m.FileName))
        .ToArray();
}
```

### Project guidance

- Expect partial data: file names can be missing or non-resolvable.
- Prefer module identity reconciliation through PE metadata (MVID) when artifact resolution succeeds.
- Keep adapter output immutable and backend-neutral.

---

## 4) Scenario C: Enumerate threads and managed stack frames

### Goal

Capture thread/frame state for expression-evaluation context and virtual stepping anchors.

### Key ClrMD APIs

- `ClrRuntime.Threads`
- per-thread stack frame enumeration
- `ClrStackFrame` properties (instruction pointer, stack pointer, associated method when available)

### Draft pattern

```csharp
public readonly record struct RuntimeFrameRecord(
    uint OsThreadId,
    string? ManagedMethodName,
    ulong InstructionPointer,
    ulong StackPointer);

public static IReadOnlyList<RuntimeFrameRecord> CaptureFrames(ClrRuntime runtime)
{
    var frames = new List<RuntimeFrameRecord>();

    foreach (var thread in runtime.Threads)
    {
        foreach (var frame in thread.EnumerateStackTrace())
        {
            frames.Add(new RuntimeFrameRecord(
                OsThreadId: thread.OSThreadId,
                ManagedMethodName: frame.Method?.Signature,
                InstructionPointer: frame.InstructionPointer,
                StackPointer: frame.StackPointer));
        }
    }

    return frames;
}
```

### Project guidance

- Mark non-managed or unresolved frames explicitly (`FrameKind.Native`, `MethodUnavailable`).
- Do not assume every frame can produce method identity or IL mapping.
- Preserve raw addresses for diagnostics and provenance.

---

## 5) Scenario D: Heap traversal for runtime value acquisition

### Goal

Read heap objects and fields for dump-backed value materialization in interpreter state.

### Key ClrMD APIs

- `ClrRuntime.Heap`
- object enumeration and type lookup
- field access through ClrMD type/field APIs

### Draft pattern

```csharp
public readonly record struct HeapObjectRecord(
    ulong Address,
    string TypeName,
    ulong Size);

public static IEnumerable<HeapObjectRecord> EnumerateObjects(ClrRuntime runtime)
{
    var heap = runtime.Heap;
    if (!heap.CanWalkHeap)
        yield break;

    foreach (var obj in heap.EnumerateObjects())
    {
        var type = heap.GetObjectType(obj);
        if (type is null)
            continue;

        yield return new HeapObjectRecord(
            Address: obj,
            TypeName: type.Name ?? "<unknown>",
            Size: type.GetSize(obj));
    }
}
```

### Project guidance

- Always guard on heap walk capability and null type cases.
- Separate raw reads from semantic conversion into interpreter domain values.
- Cap traversal by budget to keep analysis deterministic.

---

## 6) Scenario E: Counted dump method body + independent PE oracle

### Goal

Construct a dump-backed method body only from exact counted dump metadata and memory, while keeping disk-artifact decoding as an independent source.

### Key ClrMD APIs

- `ClrMethod.MetadataToken` for the runtime-selected MethodDef identity
- data-reader-backed counted memory reads through the project adapter
- `MetadataReaderProvider.FromMetadataImage` over the exact dump metadata-root read
- `PEReader` only for an independently opened, whole-file-identified disk oracle or static-artifact workflow

### Active pattern

1. Read the module's complete metadata image from dump memory and retain that counted read.
2. Validate the runtime MethodDef token, decode its implementation kind and RVA from those metadata bytes, and map the RVA only for supported mapped/loaded layouts.
3. Parse the physical tiny/fat header from counted memory reads, validate the local StandAloneSig row against the dump metadata, then read the exact code and every declared extra section under fixed caps.
4. Expose a normalized body only when all required ranges are exact. Partial/missing/malformed evidence remains a typed result and is never filled from disk.
5. In tests, compare the result with SRM's independent decode of a disk PE whose identity includes exact whole-file length and SHA-256. That comparison supplies no input to step 3.

### Project guidance

- Treat captured metadata/header/code/section reads as fallible evidence with exact byte counts.
- Keep miss reasons structured (`MetadataUnavailable`, `MethodBodyLayoutUnsupported`, `MethodHeaderInvalid`, `MemoryUnavailable`, `LimitExceeded`).
- Do not call an identity-correlated disk body “dump sourced.” A future artifact-backed evaluation mode needs separate semantics and provenance.

---

## 7) Scenario F: Failure taxonomy for partial/fragile dumps

### Goal

Standardize adapter outputs so upper layers see deterministic miss reasons instead of backend-specific exception shapes.

### Recommended miss categories

- `DumpOpenFailed`
- `RuntimeUnavailable`
- `HeapUnavailable`
- `ThreadDataUnavailable`
- `MethodUnavailable`
- `IlInfoUnavailable`
- `IlBytesMissingFromDump`
- `ModuleArtifactNotFound`
- `PdbArtifactNotFound`
- `AmbiguousIdentity`

### Project guidance

- Attach provenance (`source=ClrMD`, module address/token, thread ID) to misses.
- Prefer “known unknown” propagation over speculative reconstruction.
- Ensure miss categories are shared across ClrMD and PE/PDB adapters.

---

## 8) Suggested adapter shape for our prototype

```csharp
public interface IRuntimeSnapshotAdapter
{
    Result<RuntimeSessionRecord> OpenDump(string dumpPath);
    Result<IReadOnlyList<RuntimeModuleRecord>> GetModules(RuntimeSessionRecord session);
    Result<IReadOnlyList<RuntimeFrameRecord>> GetFrames(RuntimeSessionRecord session);
    Result<IlBytesResult> TryReadIl(RuntimeSessionRecord session, RuntimeMethodHandle method);
}
```

Design notes:

- Keep this interface intentionally small in early prototype cycles.
- Expand only when a scenario requires new data, not when backend APIs expose more knobs.
- Ensure every method is side-effect-free from the interpreter’s perspective.

---

## 9) Minimal implementation checklist

1. Open/close dump session with deterministic disposal.
2. Enumerate modules into immutable records.
3. Enumerate thread frames with explicit unresolved-frame handling.
4. Attempt IL read via `GetILInfo` + `DataReader.Read`.
5. Emit structured miss reasons for every failure path.
6. Add conformance tests using at least one “good” dump and one intentionally partial dump.

---

## 10) Out of scope for this tutorial (for now)

- Advanced DAC/runtime flavor compatibility matrices.
- Full generic-context reconstruction details.
- Deep PDB symbol decoding pipelines.
- Any claim that current draft snippets represent final public API commitments.

This tutorial should evolve together with `clrmd-integration-proposal.md` and `docs/lib/clrmd/usage-notes.md` as we validate dump shapes and adapter behavior in prototype experiments.
