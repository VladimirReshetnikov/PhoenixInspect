Below is a concrete, end-to-end plan for the **smallest “real” integration test** that proves the plumbing works:

* we can **load a dump**
* we can **find a managed module / method**
* we can **read the method’s IL**
* we can **execute one interpreter step**
* and we can assert the resulting VM state and events

The target IL is exactly **one instruction**: `ret` (opcode `0x2A`). That implies the method must be **`void`** and have an empty body.

---

## 0) Success criteria (what this test must prove)

**Given** a dump file created from a running .NET process with your test assembly loaded,

**When** we load the dump and resolve `Program.RetOnly()`,

**Then**:

1. We can locate the module (by name/MVID) via **ClrMD**.
2. We can locate the method’s `MethodDef` (token or handle) via **metadata reading**.
3. We can read the IL bytes and they are exactly `[ 0x2A ]`.
4. We can seed the interpreter with a single frame for that method and step once.
5. After the step:

   * the frame returns and is popped
   * the call stack is empty (program completed)
   * the step result contains a deterministic event trail (at least “executed ret”, “frame popped”)

This is intentionally “boring” because any flakiness here means your integration seams aren’t stable yet.

---

## 1) Test asset: a “RetOnly” target process

### 1.1 Create a dedicated small executable project

Create a separate project in your solution (or under `tests/assets/`) e.g.:

* `tests/Interpreter.TestTarget/Interpreter.TestTarget.csproj`

It should build a **normal managed app** (no single-file, no trimming, no NativeAOT), so the IL is present on disk.

### 1.2 The method: guaranteed single `ret`

Use this exact shape:

```csharp
public static class Program
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void RetOnly()
    {
        // intentionally empty
    }

    public static void Main(string[] args)
    {
        // Ensure the assembly and type are loaded.
        RetOnly();

        Console.WriteLine("READY");
        Console.Out.Flush();

        // Keep process alive so the test can dump it deterministically.
        Thread.Sleep(Timeout.Infinite);
    }
}
```

**Why `NoInlining`?**
Not strictly required for IL existence, but it reduces “smart JIT” surprises if you later decide to map by code address or stack frame.

### 1.3 Build configuration constraints

Build the target as **Release**. In Debug builds, the compiler commonly injects `nop` sequence point scaffolding, which would break the “single ret” promise.

Recommended csproj settings for the target:

* `Optimize=true`
* `DebugType=portable` (optional; you can keep PDBs for later tests)
* Do **not** enable trimming or AOT.

**Verification step (important):**
Before you even generate a dump, your integration test should open the built `Interpreter.TestTarget.dll` with the metadata reader and verify the method body bytes are exactly `0x2A`. If that fails, the test should stop immediately with a clear message (“expected ret-only; got …”).

This makes the test robust across compiler changes and prevents a lot of head-scratching.

---

## 2) Dump creation (self-contained, no external tools)

You want the integration test to generate the dump on demand (so you don’t check in huge binary dumps).

### 2.1 Spawn the target process

In your integration test project (e.g., `tests/Interpreter.IntegrationTests`):

* Start `Interpreter.TestTarget` as a child process.
* Capture stdout.
* Wait until you read the line `READY`.

This gives you a stable synchronization point.

### 2.2 Write a “full” dump via DiagnosticsClient

Use the managed API:

* NuGet: `Microsoft.Diagnostics.NETCore.Client`
* Call `new DiagnosticsClient(pid).WriteDump(DumpType.Full, path, logDumpGeneration: false)`

Notes:

* This is cross-platform in principle; it uses the runtime’s dump mechanism.
* “Full” is best for “all info available”, and it reduces surprises with missing memory pages.
* You still rely on the PE/PDB being accessible on disk for metadata/symbols; but full dumps make runtime inspection easier.

### 2.3 Terminate target process

After the dump is written:

* kill the target process (or send a shutdown signal if you want to be polite)
* wait for exit
* keep the dump file path for the next phase

---

## 3) Load dump + create runtime (ClrMD)

This phase proves the **dump host** seam.

### 3.1 Load the dump

* `DataTarget.LoadDump(dumpPath)` (ClrMD)
* Assert it returns successfully.

### 3.2 Create a ClrRuntime

* pick `ClrInfo` (typically `dataTarget.ClrVersions[0]`)
* call `CreateRuntime()`

**Important for stability:**
ClrMD needs the matching DAC for the runtime version. The easiest way to make this test reliable is:

* run the target with the **same .NET runtime installation** as the test runner
* let ClrMD find DAC locally (don’t depend on network symbol servers)

If you find DAC resolution flaky in CI, add an explicit configuration step in your host to point ClrMD at the local runtime directory (later tests will need that anyway).

### 3.3 Find your module in the runtime

From `ClrRuntime`:

* enumerate modules (`runtime.Modules`)
* locate the module whose name/path ends with `Interpreter.TestTarget.dll`

Assertions to include:

* module is found
* module has a file path (or at least enough identity to locate PE bytes)
* module’s identity matches your on-disk module (see next section)

This proves: dump → runtime → module enumeration works.

---

## 4) Metadata resolution (SRM backend) and “identity agreement”

This phase proves the **metadata** seam and ensures you’re interpreting the correct IL.

### 4.1 Open the module PE from disk

Use the module’s file path from ClrMD and open it with:

* `System.Reflection.PortableExecutable.PEReader`
* `System.Reflection.Metadata.MetadataReader`

### 4.2 Verify module identity (MVID) matches the dump module

Compute `MVID` from metadata (`ModuleDefinition.Mvid`) and compare to what you can obtain/compute for the dump module.

Minimal approach (good enough for MVP test):

* verify the module path matches the expected build output path
* verify MVID read from disk is consistent for the run

Better approach (recommended if your ClrMD host exposes it):

* store `ModuleId` as `(MVID, name, PE timestamp/size)`
* verify ClrMD module → `ModuleId` equals SRM module → `ModuleId`

This prevents “oops, we resolved a different DLL with same name”.

### 4.3 Find `Program.RetOnly` in metadata

Using SRM:

* find type `Program` in the correct namespace
* enumerate `MethodDefinition`s
* select method named `RetOnly` with no parameters and `void` return

From it, extract:

* `MethodDefinitionHandle` (or token)
* method body RVA
* IL bytes

### 4.4 Assert IL is exactly one `ret`

Read the method body IL bytes and assert:

* length == 1
* bytes[0] == 0x2A

If this fails:

* print out the actual bytes (e.g., `00 2A`), and fail with a message:
  “RetOnly was expected to compile to ret-only; build config likely emitted NOPs.”

At this point, you have proven: module + metadata + method body reading works end-to-end.

---

## 5) Minimal `Interpreter` wiring for this test

Now we actually execute.

### 5.1 Components you need (minimal set)

From your architecture, the smallest “real” wiring looks like:

* `Interpreter.Core.Execution` (the stepper / micro-step engine)
* A trivial `IValueDomain<TValue>` implementation:

  * easiest: use `Interpreter.Domain.Concrete`
* A trivial `IMemoryModel<TValue, TMem>` implementation:

  * can be a “no-op memory” model because `ret` touches none of it
* An `IResolutionServices` implementation that can:

  * return the `MethodBody` for the `MethodHandle`
  * (everything else can throw `NotSupportedException` in *this specific test* because it won’t be called)

You do **not** need:

* call models
* debug maps
* symbol info
* heap access
  for this “ret-only” execution test.

### 5.2 Create the method handle and method body provider

In the test, you can keep it minimal:

* create a single `MethodHandle` for `RetOnly`
* store the method body in a dictionary keyed by `MethodHandle`
* `TryGetMethodBody(methodHandle)` returns that body

This validates the VM execution path without requiring full token resolution.

(Next integration tests will do calls/field loads and will force you to implement the full resolver.)

### 5.3 Seed the initial machine state

Construct a `MachineState` with exactly one frame:

* `Method = RetOnly`
* `IlOffset = 0`
* `EvalStack = empty`
* `Locals = empty`
* `Args = empty`
* `This = (no this; static method)`
  (depending on your frame schema: `This = null`, or omit for static)
* `ReturnSite = null` (root frame)

### 5.4 Step once

Call:

* `StepOne(state, options)`

This should decode instruction at offset 0 as `ret` and return.

### 5.5 Assert post-step invariants

Your exact assertions depend on your engine’s result type, but typically:

* Stop reason is `Completed` (root returned)
* Call stack length is `0`
* Memory state unchanged
* Effects unchanged
* Events include (at least):

  * `InstructionExecuted(opcode=ret, offset=0)`
  * `FramePopped(method=RetOnly)`
    (or equivalent)

Also assert:

* no diagnostics of severity error
* budget not exceeded
* no unknown values were minted

This is the simplest “green light” that the interpreter is actually executing IL and unwinding frames correctly.

---

## 6) Optional: add a *stepping* integration assertion (statement-level)

If you want to prove the stepping glue (without adding PDB/decompiler yet), you can add a synthetic IL debug map:

* one statement covering `[0..1)` in IL
* mapping to a synthetic document `il://Interpreter.TestTarget/Program.RetOnly`

Then drive:

* `Interpreter.Debugger.Engine.StepInto` (or StepOver) once

Expect:

* session completes in one step
* the “current statement” moves to “completed” / no statement

This is optional for the minimal integration test; it’s a nice sanity check that your stop predicates don’t rely on source info.

---

## 7) Pitfalls and how to prevent flakiness

### Pitfall A: `RetOnly` isn’t really ret-only

Cause: Debug build, compiler inserts `nop`.
Fix: build Release, and verify IL bytes before dumping.

### Pitfall B: ClrMD can’t create runtime (DAC mismatch)

Cause: different runtime versions or missing DAC.
Fix: generate the dump from a process using the same runtime as the test runner; ensure CI uses the same installed runtime for both; optionally provide explicit DAC path in your ClrMD host.

### Pitfall C: module path in dump isn’t usable

Cause: single-file, custom load contexts, deleted build output.
Fix: keep artifacts in place, don’t use single-file/trimming/AOT for this test.

### Pitfall D: dump creation fails under restricted environments

Cause: OS permissions / ptrace restrictions (Linux), sandboxed CI.
Fix: mark the test as an integration test; allow skipping when dump creation not permitted; keep a diagnostic message showing the failure reason.

---

## 8) Checklist summary (copy/paste into a tracking issue)

1. **Asset**

* [ ] Create `Interpreter.TestTarget` executable project
* [ ] Add `Program.RetOnly()` empty method, `NoInlining`, `READY` handshake, infinite sleep
* [ ] Ensure Release build emits IL `[0x2A]` (verify with SRM)

2. **Dump generation**

* [ ] Spawn target, wait for `READY`
* [ ] Use `DiagnosticsClient.WriteDump(DumpType.Full)`
* [ ] Terminate target

3. **Dump load**

* [ ] `ClrMD DataTarget.LoadDump`
* [ ] `CreateRuntime`
* [ ] Find `Interpreter.TestTarget.dll` module

4. **Metadata**

* [ ] Open module with SRM
* [ ] Verify identity (at least path; ideally MVID)
* [ ] Find `Program.RetOnly` methoddef
* [ ] Read IL bytes and assert `0x2A`

5. **Interpreter execution**

* [ ] Create minimal resolver providing just this method body
* [ ] Seed state with one frame at IL offset 0
* [ ] `StepOne` once
* [ ] Assert: stack empty, Completed, events include ret + frame pop, no unknowns

---

Below is a “what you actually need to write” document for the **minimal integration test**:

> Load a dump (full dump, all artifacts available), locate a managed module + method, read its IL, and execute a single `ret` instruction in your IL interpreter.

I’m going to assume you’re following the modular architecture we discussed, with these abstraction packages already defined:

* `Interpreter.Core.Abstractions`
* `Interpreter.Metadata.Abstractions`
* `Interpreter.Host.Abstractions`

…and concrete implementations like:

* `Interpreter.Host.Dump.ClrMD`
* `Interpreter.Metadata.SRM`
* `Interpreter.Core.Execution`

This document focuses on the **smallest set of concrete classes/methods** you must implement to make that integration test pass, and what each one must do.

---

# 1) The minimal end-to-end call graph

This is the “spine” of your integration test:

1. **Start test target** → get PID
2. **Write dump** (full dump)
3. **Load dump** via `ClrMD`
4. **Find module** `Interpreter.TestTarget.dll` in `runtime.Modules`
5. **Load module PE** from the module file path via SRM (`PEReader`)
6. **Find method** `Program.RetOnly` in metadata and read its method body
7. **Construct MethodHandle** + `MethodBody` (your internal representation)
8. **Seed interpreter state** with a single frame
9. **Step once** → decode `ret` → pop frame → session completes
10. Assert: stack empty + “frame popped” event

So, you need **one concrete implementation** in each of these areas:

* Dump host / module discovery (ClrMD)
* Metadata / IL body extraction (SRM)
* VM execution (interpret 1 instruction)

Everything else can be stubbed for this test.

---

# 2) What you need to implement (by subsystem)

## 2.1 Test harness (in your test project)

### A) `TestTargetRunner`

You need a helper that:

* starts the test target process
* waits for the “READY” line
* exposes PID
* kills the process after dump is written

Minimal API:

```csharp
public sealed class TestTargetRunner : IDisposable
{
    public int Pid { get; }
    public string ExecutablePath { get; }

    public static TestTargetRunner StartAndWaitReady(string exePath);
    public void Dispose(); // kills process if still alive
}
```

### B) `DumpWriter`

Use `Microsoft.Diagnostics.NETCore.Client`:

```csharp
public static class DumpWriter
{
    public static void WriteFullDump(int pid, string dumpPath);
}
```

Implementation is basically `new DiagnosticsClient(pid).WriteDump(DumpType.Full, dumpPath, logDumpGeneration: false);`

That’s not your interpreter framework, but it’s required for the test to be self-contained.

---

## 2.2 `Interpreter.Host.Dump.ClrMD` (dump loading + module discovery)

For this minimal test, you do **not** need heap walking, locals recovery, field reads, etc. You need only:

* load dump
* create runtime
* enumerate modules
* get module file path

### A) `ClrmdDumpSession`

```csharp
public sealed class ClrmdDumpSession : IDisposable
{
    public static ClrmdDumpSession Load(string dumpPath);

    public Microsoft.Diagnostics.Runtime.ClrRuntime Runtime { get; }

    public IReadOnlyList<ClrmdModuleInfo> Modules { get; }

    public bool TryFindModuleByFileName(string fileName, out ClrmdModuleInfo module);

    public void Dispose();
}
```

### B) `ClrmdModuleInfo`

This is a simple DTO you control (don’t leak ClrMD module objects outward unless you want to).

```csharp
public sealed record ClrmdModuleInfo(
    string Name,       // e.g. "Interpreter.TestTarget.dll"
    string? FilePath   // where the PE lives on disk (critical for this test)
);
```

### Minimal implementation details

* `Load(dumpPath)`:

  * `var dt = DataTarget.LoadDump(dumpPath);`
  * `var clr = dt.ClrVersions[0];`
  * `var runtime = clr.CreateRuntime();`
  * `Modules = runtime.Modules.Select(m => new ClrmdModuleInfo(Path.GetFileName(m.Name), m.FileName)).ToList()`
* `TryFindModuleByFileName`:

  * find module by file name case-insensitively
  * assert `FilePath` not null and file exists (for this integration test)

That’s it.

> Why this is enough:
> The interpreter doesn’t need the dump memory to execute `ret`. But the test wants to prove you can load the dump and locate the relevant module “through the dump path” rather than relying on external knowledge.

---

## 2.3 `Interpreter.Metadata.SRM` (metadata + IL body extraction)

For this minimal test, metadata work is also tiny:

* open PE file
* find one method by name
* extract IL bytes and decode the method body header

### A) `SrmMetadataModule` (implements `Interpreter.Metadata.Abstractions.IMetadataModule`)

At minimum, implement these methods:

* `ModuleId Id { get; }`
* `ModuleHandle ModuleHandle { get; }` (opaque; can be hash of MVID)
* `MethodHandle GetMethodHandle(int metadataToken, GenericContext ctx)` (or a simpler helper for test)
* `bool TryGetMethodBody(MethodHandle method, out MethodBody body)`

You do **not** need to implement:

* virtual dispatch
* field signatures
* generic instantiation handling
  …for the `ret` test. You can throw `NotSupportedException` for methods that won’t be called.

### Minimal internal representation you’ll want inside `SrmMetadataModule`

* `PEReader _pe;`
* `MetadataReader _md;`
* A mapping from `MethodHandle` → `MethodDefinitionHandle` (or token)

Example:

```csharp
private readonly Dictionary<MethodHandle, MethodDefinitionHandle> _methodMap = new();
```

### B) Helper: `FindMethodDefToken(...)` (test-only or utility)

This isn’t part of the abstraction, but it makes the test readable.

```csharp
public int FindMethodDefToken(string typeFullName, string methodName)
```

It can:

* locate the type `Program`
* locate method `RetOnly`
* return its metadata token (MethodDef token)

### C) Implementation: `TryGetMethodBody`

This is the key method you must implement correctly.

Pseudo-code:

```csharp
public bool TryGetMethodBody(MethodHandle method, out MethodBody body)
{
    body = default;

    if (!_methodMap.TryGetValue(method, out var mdefHandle))
        return false;

    var mdef = _md.GetMethodDefinition(mdefHandle);
    int rva = mdef.RelativeVirtualAddress;
    if (rva == 0) return false; // abstract/external

    var mb = _pe.GetMethodBody(rva);

    // mb provides maxstack, local sig token, exception regions, IL bytes...
    var ilBytes = mb.GetILBytes(); // returns BlobReader-like; copy to byte[]
    byte[] il = ilBytes.ToArray(); // or manual copy

    body = new MethodBody(
        ilBytes: il,
        maxStack: mb.MaxStack,
        initLocals: mb.LocalVariablesInitialized,
        // locals sig token: mb.LocalSignature (handle/token) if you track it
        // EH: mb.ExceptionRegions if you choose to carry them
    );
    return true;
}
```

### D) Your internal `MethodBody` type

For this test, it can be minimal:

```csharp
namespace Interpreter.IL;

public sealed record MethodBody(
    ReadOnlyMemory<byte> IlBytes,
    int MaxStack,
    bool InitLocals
    // you can omit locals signature and EH for this test
);
```

### E) Handle creation

You can keep handles trivial for this test.

**Simplest approach (fine for integration test):**

* `MethodHandle.Value = (ulong)(uint)metadataToken;`
* `ModuleHandle.Value = hash(MVID)` (or even `1` if you have one module)

But be consistent: `TryGetMethodBody` must be able to map the handle back to the method definition.

A practical implementation is:

* When `GetMethodHandle(token, ctx)` is called, resolve token to `MethodDefinitionHandle`, store it in `_methodMap` and return a fresh handle.

---

## 2.4 A minimal `Interpreter.Core.Abstractions.IResolutionServices`

Your VM engine wants a resolver to fetch method bodies. For this test, you can implement a tiny adapter:

### `MetadataResolutionServices`

```csharp
public sealed class MetadataResolutionServices : Interpreter.Core.Abstractions.IResolutionServices
{
    private readonly Interpreter.Metadata.Abstractions.IMetadataModule _module;

    public MetadataResolutionServices(IMetadataModule module) => _module = module;

    public bool TryGetMethodBody(MethodHandle method, out Interpreter.IL.MethodBody body)
        => _module.TryGetMethodBody(method, out body);

    // Everything else can throw for this test:
    public ResolvedType ResolveType(...) => throw new NotSupportedException();
    public ResolvedField ResolveField(...) => throw new NotSupportedException();
    public ResolvedMethod ResolveMethod(...) => throw new NotSupportedException();
    public MethodHandle ResolveVirtualOverride(...) => throw new NotSupportedException();
}
```

That’s enough for `ret`.

---

## 2.5 `Interpreter.Core.Execution` (the minimal IL execution engine)

This is where your actual “step the IL” capability lives.

### A) State objects (minimal)

You need only:

* `MachineState<TValue, TMem>`: has call stack and memory (even if unused)
* `FrameState<TValue>`: current method + IL offset + eval stack
* `StepResult` / `StepOutcome`: includes next state + stop reason + events

Minimal definitions:

```csharp
public sealed record MachineState<TValue, TMem>(
    ImmutableArray<FrameState<TValue>> CallStack,
    TMem Memory,
    BudgetState Budget);

public sealed record FrameState<TValue>(
    MethodHandle Method,
    int IlOffset,
    ImmutableArray<TValue> EvalStack);
```

### B) Debug events (minimal)

```csharp
public enum DebugEventKind { InstructionExecuted, FramePopped }

public sealed record DebugEvent(DebugEventKind Kind, string? Detail = null);
```

### C) Stop reasons

```csharp
public enum StopReason { Running, Completed, BudgetExceeded, Faulted }
```

### D) The interpreter engine class

```csharp
public sealed class IlMachine<TValue, TMem>
{
    private readonly IResolutionServices _resolver;

    public IlMachine(IResolutionServices resolver) => _resolver = resolver;

    public StepOutcome<TValue, TMem> StepOne(MachineState<TValue, TMem> state)
    {
        // implement ret-only semantics
    }
}
```

### E) The one opcode you must implement: `ret` (0x2A)

Inside `StepOne`:

1. Assert call stack not empty
2. Get top frame
3. Fetch method body via `_resolver.TryGetMethodBody(frame.Method, out body)`
4. Read opcode byte at `frame.IlOffset`
5. If opcode == 0x2A:

   * pop frame
   * if call stack becomes empty => StopReason.Completed
   * else => resume caller frame (not needed here)
6. Emit events:

   * `InstructionExecuted("ret @0")`
   * `FramePopped("RetOnly")` (name optional)

Minimal code shape:

```csharp
public StepOutcome<TValue, TMem> StepOne(MachineState<TValue, TMem> state)
{
    if (state.CallStack.Length == 0)
        return new(state, StopReason.Completed, ImmutableArray<DebugEvent>.Empty);

    if (!budget.TryConsumeInstruction(...))
        return new(state, StopReason.BudgetExceeded, ...);

    var frame = state.CallStack[^1];

    if (!_resolver.TryGetMethodBody(frame.Method, out var body))
        return new(state, StopReason.Faulted, Events("Missing method body"));

    byte op = body.IlBytes.Span[frame.IlOffset];

    if (op != 0x2A)
        return new(state, StopReason.Faulted, Events($"Unexpected opcode {op:X2}"));

    var events = ImmutableArray.Create(
        new DebugEvent(DebugEventKind.InstructionExecuted, "ret"),
        new DebugEvent(DebugEventKind.FramePopped));

    var newStack = state.CallStack.RemoveAt(state.CallStack.Length - 1);
    var next = state with { CallStack = newStack };

    return new(next, newStack.Length == 0 ? StopReason.Completed : StopReason.Running, events);
}
```

That’s enough to pass the minimal test.

> Note: you can ignore return value stack rules here because the method is `void`.
> Later tests (`ldc.i4.1; ret`) will force you to model return values.

---

## 2.6 Domain and memory model (stubbed)

For this single `ret` test:

* no values are created
* no memory is read/written

So you can pass “dummy” implementations. But if your types require them, implement:

### A) `UnitValueDomain` (never actually used)

```csharp
public sealed class UnitValueDomain : IValueDomain<Unit>
{
    // Either throw or return defaults; StepOne(ret) won’t call it.
}
```

### B) `UnitMemoryModel` (never actually used)

```csharp
public sealed class UnitMemoryModel : IMemoryModel<Unit, Unit>
{
    public bool CanAllocate => false;
    // Everything throws; not reached in this test.
}
```

Or, simpler: have `IlMachine.StepOne` not depend on domain/memory at all yet. For a true “ret-only” opcode test, it can.

---

# 3) How the integration test should be written (so it uses your implementations)

This matters because your “what to implement” depends on what the test calls.

### Recommended test structure

1. Build path to `Interpreter.TestTarget.exe`
2. Run target, wait READY
3. Write full dump
4. Load dump via `ClrmdDumpSession`
5. Find module path (`Interpreter.TestTarget.dll`)
6. Load module via `SrmMetadataModule.Load(filePath)`
7. Find method token for `Program.RetOnly`
8. Convert token → `MethodHandle`
9. Assert method body IL is `[0x2A]`
10. Create `MetadataResolutionServices` for module
11. Create `IlMachine`
12. Seed state with one frame
13. StepOne once
14. Assert Completed and stack empty and events

This test will fail loudly if any seam is missing:

* ClrMD isn’t loading dumps
* module identity/path isn’t surfaced
* SRM can’t read the method body
* your VM can’t decode/execute `ret`

---

# 4) Minimum implementation checklist (copy/paste)

If you want a strict “do these and the test will pass” list:

### Test utilities

* [ ] `TestTargetRunner.StartAndWaitReady(exePath)`
* [ ] `DumpWriter.WriteFullDump(pid, dumpPath)`

### `Interpreter.Host.Dump.ClrMD`

* [ ] `ClrmdDumpSession.Load(dumpPath)`
* [ ] `ClrmdDumpSession.Modules` (name + file path)
* [ ] `ClrmdDumpSession.TryFindModuleByFileName("Interpreter.TestTarget.dll", out module)`

### `Interpreter.Metadata.SRM`

* [ ] `SrmMetadataModule.LoadFromFile(path)`
* [ ] `SrmMetadataModule.FindMethodDefToken("Program", "RetOnly")` (helper)
* [ ] `SrmMetadataModule.GetMethodHandle(token, GenericContext.Empty)` (or equivalent)
* [ ] `SrmMetadataModule.TryGetMethodBody(methodHandle, out MethodBody)` using `PEReader.GetMethodBody(rva)`

### Bridge

* [ ] `MetadataResolutionServices.TryGetMethodBody(...)`

### `Interpreter.Core.Execution`

* [ ] `MachineState`, `FrameState`
* [ ] `IlMachine.StepOne(...)` with `ret` (`0x2A`) support
* [ ] `StopReason.Completed` when root frame popped
* [ ] debug events: at least `InstructionExecuted`, `FramePopped`

---

# 5) What you can *explicitly* leave unimplemented for this test

To keep scope minimal, these can throw `NotSupportedException` in this first integration test:

* Token resolution (Type/Field/Method)
* Virtual dispatch
* Locals signature decoding
* EH clause parsing
* Stack typing verification
* Call models registry
* Debug maps / stepping engine (statement-level)
* Heap/object reading from the dump

You’ll add them in the *next* integration tests (call, field load, etc.).

