> **Historical design sketch.** These interfaces predate executable evidence and are not normative API commitments. The current prototype source is authoritative for implemented contracts; new public surface is added contract-just-ahead-of-code and must preserve typed evidence/provenance at partial-data boundaries. See `architecture-overview-proposal.md`.

Below is an earlier candidate public interface surface for three abstraction packages:

* `Interpreter.Core.Abstractions`
* `Interpreter.Metadata.Abstractions`
* `Interpreter.Host.Abstractions`

The goal is to make every leaf subsystem (ClrMD host, SRM metadata reader, ILSpy decompiler integration, async/dynamic models, analysis) depend only on **small contracts** instead of each other’s concrete types. This keeps dependencies directional and keeps mocking/testing easy.

I’m going to:

1. list the **responsibility boundaries** per abstraction package,
2. give a compact set of **DTOs + interfaces** (C# sketches),
3. note **what must NOT** live in each package to avoid creeping coupling,
4. show how the pieces fit together in a typical “dump debugging + stepping” pipeline.

---

# 1) `Interpreter.Core.Abstractions`

## 1.1 What belongs here

Everything the IL engine needs to be reusable across:

* dump debugging,
* live speculative debugging,
* static analysis,
* bounded runtime “no JIT” execution.

So this package owns:

* the **value domain** interface (concrete/abstract/hybrid values),
* the **memory model** interface (virtual heap, overlay, summary heap),
* the **call modeling** interface (intrinsics / semantics lifts),
* token/member resolution **as an abstract service** (not tied to SRM/AsmResolver),
* budgets, effects, unknown provenance, diagnostics sinks.

### Key design choice

Call models should **not** require direct access to your concrete `MachineState` records.
Instead, they operate on a *call context* and return:

* return value or throw,
* memory delta (new `TMem`),
* effects + diagnostics,
* optional “decision needed” / “fork” if your engine supports it.

That keeps models isolated from execution engine internals.

---

## 1.2 Core identity handles (opaque, stable inside a session)

You want identities the VM can pass around without knowing *who* resolves them.

```csharp
namespace Interpreter.Core.Abstractions;

public readonly record struct ModuleHandle(ulong Value);
public readonly record struct TypeHandle(ulong Value);
public readonly record struct MethodHandle(ulong Value);
public readonly record struct FieldHandle(ulong Value);
```

These are *opaque*. Metadata implementations decide how they’re constructed (often `hash(MVID, token)`).

---

## 1.3 Effects, unknown provenance, diagnostics

```csharp
namespace Interpreter.Core.Abstractions;

[Flags]
public enum EffectKind
{
    None        = 0,
    Alloc       = 1 << 0,
    ReadEnv     = 1 << 1,
    WriteEnv    = 1 << 2,
    Time        = 1 << 3,
    Random      = 1 << 4,
    Threading   = 1 << 5,
    Native      = 1 << 6,
    Reflection  = 1 << 7,
    Throw       = 1 << 8,
    Unsupported = 1 << 9,
}

public readonly record struct EffectEvent(
    EffectKind Kind,
    string Code,          // short stable code: "Env.DateTime.Now", "Native.PInvoke", ...
    string? Details = null);

public sealed record EffectSummary(
    EffectKind Kinds,
    IReadOnlyList<EffectEvent> Events);

public enum UnknownOriginKind
{
    MissingData,
    UnsupportedIL,
    ExternalCall,
    NativeCall,
    EnvRead,
    BudgetExceeded,
    AmbiguousDispatch,
}

public readonly record struct UnknownOrigin(
    UnknownOriginKind Kind,
    string? Detail = null);

public enum DiagnosticSeverity { Info, Warning, Error }

public readonly record struct Diagnostic(
    DiagnosticSeverity Severity,
    string Code,      // stable: "INTP0012"
    string Message);

public interface IDiagnosticSink
{
    void Report(in Diagnostic diagnostic);
}
```

---

## 1.4 Budgets, policies, and “decision needed”

These are essential for:

* stepping (avoid infinite loops),
* static analysis (fork control),
* bounded execution (resource limits).

```csharp
namespace Interpreter.Core.Abstractions;

public sealed record BudgetState(
    long InstructionBudget,
    long AllocationBudget,
    int  MaxCallDepth,
    int  MaxForks);

public interface IBudgetPolicy
{
    bool TryConsumeInstruction(ref BudgetState budget, int cost = 1);
    bool TryConsumeAllocation(ref BudgetState budget, long bytes);
}
```

Branch decisions:

```csharp
namespace Interpreter.Core.Abstractions;

public enum BranchDecisionKind
{
    TakeTrue,
    TakeFalse,
    Fork,
    StopForUserChoice,
    JoinBoth // analysis-mode convenience
}

public sealed record BranchInfo(
    string Description,
    object? Payload = null); // optional: condition provenance

public readonly record struct BranchDecision(
    BranchDecisionKind Kind,
    BranchInfo? Info = null);
```

---

## 1.5 Value domain (minimal API that still supports IL)

Your earlier domain was richer; this is the “minimum interface that scales”:

```csharp
namespace Interpreter.Core.Abstractions;

public enum Nullness { NotApplicable, Null, NonNull, MaybeNull }

public enum StackKind
{
    I4, I8, R4, R8, NativeInt,
    Ref, ByRef, ValueType
}

public enum UnaryOp { Neg, Not }
public enum BinaryOp
{
    Add, Sub, Mul, Div, Rem,
    And, Or, Xor,
    Shl, Shr, ShrUn,
    Eq, Ne, Lt, Le, Gt, Ge,
}

public enum ConvOp
{
    I1, U1, I2, U2, I4, U4, I8, U8,
    R4, R8, NativeInt, NativeUInt
}

public interface IValueDomain<TValue>
{
    // Construction
    TValue Top(Interpreter.Types.TypeSig type, UnknownOrigin origin);
    TValue ConstNull(Interpreter.Types.TypeSig refType);
    TValue ConstInt32(int value);
    TValue ConstInt64(long value);
    TValue ConstBool(bool value);
    TValue ConstString(string value);

    TValue FreshUnknown(Interpreter.Types.TypeSig type, UnknownOrigin origin);

    // Lattice
    TValue Join(TValue a, TValue b);
    TValue Widen(TValue prev, TValue next);

    // Queries / refinement
    Interpreter.Types.TypeSig GetStaticType(TValue value);
    StackKind GetStackKind(TValue value);

    Nullness GetNullness(TValue value);
    TValue RefineNonNull(TValue value);
    TValue RefineNull(TValue value);

    bool TryGetConstInt32(TValue value, out int c);
    bool TryGetConstBool(TValue value, out bool b);

    // Primitive operations
    TValue ApplyUnary(UnaryOp op, TValue v);
    TValue ApplyBinary(BinaryOp op, TValue a, TValue b);
    TValue Convert(ConvOp op, TValue v, bool checkedOverflow);

    // Boxing / type ops (needed for C#/BCL patterns)
    TValue Box(TValue v, Interpreter.Types.TypeSig boxedType);
    TValue UnboxAny(TValue boxed, Interpreter.Types.TypeSig targetType);

    // Attach/propagate effects/origin labels is domain-specific; keep it inside TValue.
}
```

This is intentionally “IL-complete enough” but doesn’t hardcode fancy facets like ranges/origin labels/type-sets; those are embedded in `TValue` implementations.

---

## 1.6 Memory model (virtual heap + overlay + summary heap)

This is the minimum the engine and models need. It supports:

* object/array allocation (optional),
* fields/elements,
* byref addressables,
* havoc (critical for unknown/native calls).

```csharp
namespace Interpreter.Core.Abstractions;

public enum HavocRegionKind
{
    Everything,
    ReachableFromArgs,
    SpecificObject,
    SpecificAddressable,
}

public readonly record struct HavocRegion(HavocRegionKind Kind, object? Payload = null);

public interface IMemoryModel<TValue, TMem>
{
    bool CanAllocate { get; }

    // Allocation
    (TValue objRef, TMem mem) NewObject(TMem mem, Interpreter.Types.TypeSig type);
    (TValue arrRef, TMem mem) NewArray(TMem mem, Interpreter.Types.TypeSig elemType, TValue length);

    // Field access
    TValue LoadField(TMem mem, TValue objRef, FieldHandle field);
    TMem   StoreField(TMem mem, TValue objRef, FieldHandle field, TValue value);

    // Array access
    TValue LoadElement(TMem mem, TValue arrRef, TValue index);
    TMem   StoreElement(TMem mem, TValue arrRef, TValue index, TValue value);

    // ByRef support
    TValue MakeByRefToField(TMem mem, TValue objRef, FieldHandle field);
    TValue MakeByRefToLocal(int localIndex);
    TValue LoadIndirect(TMem mem, TValue byRef);
    TMem   StoreIndirect(TMem mem, TValue byRef, TValue value);

    // Unknown side effects
    TMem Havoc(TMem mem, HavocRegion region, UnknownOrigin origin);
}
```

---

## 1.7 Member/token resolution (VM-facing)

The VM can’t decode signatures or resolve tokens itself. It asks a resolver.

```csharp
namespace Interpreter.Core.Abstractions;

public readonly record struct ResolvedType(TypeHandle Type, Interpreter.Types.TypeSig Sig);

public readonly record struct ResolvedField(
    FieldHandle Field,
    Interpreter.Types.FieldSig Sig,
    TypeHandle DeclaringType);

public readonly record struct ResolvedMethod(
    MethodHandle Definition,
    Interpreter.Types.MethodSig Signature,
    Interpreter.Types.GenericContext CalleeGenericContext,
    TypeHandle DeclaringType);

public interface IResolutionServices
{
    // Resolve metadata token operands used by IL instructions
    ResolvedType  ResolveType(ModuleHandle module, int metadataToken, Interpreter.Types.GenericContext ctx);
    ResolvedField ResolveField(ModuleHandle module, int metadataToken, Interpreter.Types.GenericContext ctx);
    ResolvedMethod ResolveMethod(ModuleHandle module, int metadataToken, Interpreter.Types.GenericContext ctx);

    // Method bodies (for interpretation)
    bool TryGetMethodBody(MethodHandle method, out Interpreter.IL.MethodBody body);

    // Virtual/interface dispatch (metadata-level)
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}
```

This is deliberately “just enough” for:

* `call`, `callvirt`, `newobj`
* `ldfld`/`stfld`, `ldsfld`/`stsfld`
* `ldtoken` patterns (handled via type/method resolution)
* virtual dispatch

---

## 1.8 Call modeling interface (intrinsics / semantic lifts)

```csharp
namespace Interpreter.Core.Abstractions;

public enum CallKind { Call, CallVirt, NewObj }

public readonly record struct CallSite(
    CallKind Kind,
    ModuleHandle Module,
    int MethodToken,                      // original operand token for provenance
    ResolvedMethod Target,                // resolved methoddef + signature + ctx
    Interpreter.Types.TypeSig? ConstrainedType = null);

public enum CallOutcomeKind { NotHandled, Returned, Threw, Forked, StopForDecision }

public sealed record CallOutcome<TValue, TMem>(
    CallOutcomeKind Kind,
    TValue? ReturnValue = default,
    TValue? ThrownException = default,
    TMem? Memory = default,
    EffectSummary? Effects = null,
    IReadOnlyList<CallOutcome<TValue, TMem>>? Forks = null,
    BranchInfo? DecisionInfo = null);

public sealed record CallModelContext<TValue, TMem>(
    IValueDomain<TValue> Domain,
    IMemoryModel<TValue, TMem> MemoryModel,
    IResolutionServices Resolver,
    IDiagnosticSink Diagnostics,
    BudgetState Budget,
    TMem Memory,
    CallSite Site,
    IReadOnlyList<TValue> Arguments,
    IServiceProvider? Services = null);

public interface ICallModel<TValue, TMem>
{
    CallOutcome<TValue, TMem> TryCall(CallModelContext<TValue, TMem> ctx);
}
```

Key properties of this shape:

* Models don’t touch the VM’s call stack directly.
* Models can implement async/dynamic/etc. by returning `StopForDecision` or `Forked`.
* Host-specific services (session snapshot, symbol lookup, dump helpers) are accessed via `Services` (typed retrieval), not hard dependencies.

---

## 1.9 What must NOT live in `Interpreter.Core.Abstractions`

* ClrMD types
* SRM/AsmResolver types
* ILSpy or Roslyn types
* Debug maps/source spans (those are metadata/symbol/decompiler concerns)
* “Task model”, “Dynamic model”, etc. (those are model packages)

---

# 2) `Interpreter.Metadata.Abstractions`

## 2.1 What belongs here

This package defines stable contracts around:

* **module identity**
* **metadata lookup**
* **method body acquisition**
* **symbols** (PDB) and **debug maps** for stepping
* source documents and “best available source” abstraction

It should not care whether the implementation uses:

* SRM (`System.Reflection.Metadata`),
* AsmResolver,
* dnlib,
* DiaSymReader,
* ILSpy.

---

## 2.2 Stable identities

Make module identity explicit and comparable.

```csharp
namespace Interpreter.Metadata.Abstractions;

public readonly record struct ModuleId(
    Guid Mvid,
    string? Name = null,
    string? PathHint = null,
    (uint TimeDateStamp, uint ImageSize)? PeStamp = null);

public readonly record struct DocumentId(Guid Value);

public readonly record struct PdbId(Guid Guid, int Age);
```

---

## 2.3 Metadata universe contracts

The metadata layer is where you turn “token + generic context” into:

* `TypeSig/MethodSig/FieldSig`
* and the **Core handles** (`TypeHandle/MethodHandle/FieldHandle`) used by the VM.

```csharp
using Interpreter.Core.Abstractions;
using Interpreter.Types;
using Interpreter.IL;

namespace Interpreter.Metadata.Abstractions;

public interface IMetadataModule
{
    ModuleId Id { get; }
    ModuleHandle ModuleHandle { get; }

    // Handle construction (stable within module)
    TypeHandle   GetTypeHandle(int metadataToken, GenericContext ctx);
    MethodHandle GetMethodHandle(int metadataToken, GenericContext ctx);
    FieldHandle  GetFieldHandle(int metadataToken, GenericContext ctx);

    // Signature resolution
    TypeSig   GetTypeSignature(TypeHandle type);
    MethodSig GetMethodSignature(MethodHandle method);
    FieldSig  GetFieldSignature(FieldHandle field);

    // High-level resolution helpers used by Core.IResolutionServices
    ResolvedType  ResolveTypeToken(int token, GenericContext ctx);
    ResolvedField ResolveFieldToken(int token, GenericContext ctx);
    ResolvedMethod ResolveMethodToken(int token, GenericContext ctx);

    // Method body
    bool TryGetMethodBody(MethodHandle method, out MethodBody body);

    // Virtual dispatch mapping (metadata-level best-effort)
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}

public interface IMetadataUniverse
{
    bool TryGetModule(ModuleId id, out IMetadataModule module);
}
```

This keeps the VM’s `IResolutionServices` trivial to implement: it delegates to `IMetadataModule`.

---

## 2.4 Symbols and debug maps

Instead of exposing raw “Portable PDB reader objects”, provide a normalized form.

### Sequence points and locals

```csharp
namespace Interpreter.Metadata.Abstractions;

public readonly record struct SequencePoint(
    int IlOffset,
    int IlEndOffset,           // inferred if needed
    DocumentId Document,
    int StartLine, int StartColumn,
    int EndLine, int EndColumn,
    bool IsHidden);

public sealed record LocalInfo(int Slot, string Name, TypeSig? Type = null);

public sealed record LocalScope(
    int StartOffset,
    int EndOffset,
    IReadOnlyList<LocalInfo> Locals);

public interface ISymbolInfo
{
    bool TryGetSequencePoints(MethodHandle method, out IReadOnlyList<SequencePoint> points);
    bool TryGetLocalScopes(MethodHandle method, out IReadOnlyList<LocalScope> scopes);

    // Documents
    bool TryGetDocument(DocumentId id, out DebugDocument doc);

    // Optional: async/lambda mapping (can be empty)
    bool TryGetStateMachineKickoff(MethodHandle moveNext, out MethodHandle kickoff);
}
```

### DebugMap abstraction (what stepping uses)

This matches the “unified DebugMap model” we discussed earlier, but here it’s only the interface; concrete debug maps can come from PDB or decompiler.

```csharp
namespace Interpreter.Metadata.Abstractions;

public enum DebugDocumentKind { RealFile, Embedded, SourceLink, Decompiled, IL }

public sealed record DebugDocument(
    DocumentId Id,
    string UrlOrPath,
    DebugDocumentKind Kind);

public readonly record struct StatementId(int Value);

public sealed record SourceSpan(
    DocumentId Document,
    int StartLine, int StartCol,
    int EndLine, int EndCol,
    bool IsHidden);

public interface IDebugMap
{
    MethodHandle Method { get; }
    StatementId GetStatementAtIlOffset(int ilOffset);

    // For highlighting
    bool TryGetSourceSpan(int ilOffset, out SourceSpan span);

    // For stepping
    int? TryGetNextStatementOffset(int ilOffset);
    int? TryGetPreviousStatementOffset(int ilOffset);
}

public interface IDebugMapProvider
{
    // “Best available” map:
    //   PDB map → decompiler map → synthetic IL map
    IDebugMap GetBestMap(IMetadataModule module, MethodHandle method);
}
```

### Source text abstraction

Source can come from:

* file path,
* embedded source in PDB,
* SourceLink fetch,
* decompiler output.

```csharp
namespace Interpreter.Metadata.Abstractions;

public interface ISourceTextProvider
{
    // Returns UTF-8/UTF-16 text, caller decides how to display
    bool TryGetSourceText(DebugDocument doc, out string text);
}
```

---

## 2.5 What must NOT live in `Interpreter.Metadata.Abstractions`

* ClrMD types (those are host concerns)
* any “symbol server download” logic (artifact acquisition)
* ILSpy/Roslyn concrete APIs (only wrappers implement these abstractions)
* VM execution state

---

# 3) `Interpreter.Host.Abstractions`

## 3.1 What belongs here

Host abstractions are about:

* “Where do values come from?” (dump snapshot, live snapshot, no-JIT runtime)
* “How do I seed a frame?” (this/args/locals)
* “How do I read heap state behind external object refs?”
* “What environment snapshot do we expose?” (time/env/random policy)
* “How do I bridge runtime identity to metadata identity?” (method/module mapping)

Host abstractions should be *domain-agnostic* where possible, but in practice you often want to materialize values into the interpreter domain. The clean compromise is:

* Host provides **external references + raw primitives** (`ExternalValue`).
* A separate adapter (in host implementation packages) turns them into `TValue` using the current `IValueDomain<TValue>`.

---

## 3.2 External references and values

```csharp
namespace Interpreter.Host.Abstractions;

public readonly record struct ExternalObjectRef(ulong Address);
public readonly record struct ExternalThreadId(uint OsId);
public readonly record struct ExternalFrameId(int Index);

public enum ExternalValueKind
{
    Unavailable,
    Int32, Int64,
    Float64,
    Boolean,
    ObjectRef,
    StringRef,    // address of a string object
    RawBytes      // for value types / blobs (optional)
}

public sealed record ExternalValue(
    ExternalValueKind Kind,
    long I64 = 0,
    double F64 = 0,
    ExternalObjectRef Obj = default,
    ReadOnlyMemory<byte>? Bytes = null);
```

---

## 3.3 Session snapshot (time/env/random, etc.)

```csharp
namespace Interpreter.Host.Abstractions;

public sealed record SessionSnapshot(
    DateTimeOffset? DumpCaptureTimeUtc,
    TimeSpan? TargetLocalOffset,
    string? TargetMachineName,
    int? TargetProcessId,
    IReadOnlyDictionary<string, string>? EnvironmentVariables);

public interface ISessionSnapshotProvider
{
    SessionSnapshot GetSnapshot();
}
```

---

## 3.4 External heap/object model (read-only)

This is what overlay memory and projections use.

```csharp
using Interpreter.Core.Abstractions; // for TypeHandle/FieldHandle
using Interpreter.Metadata.Abstractions; // for ModuleId, etc.

namespace Interpreter.Host.Abstractions;

public interface IExternalObjectModel
{
    // Basic type query
    bool TryGetObjectType(ExternalObjectRef obj, out TypeHandle runtimeType);

    // Strings (common and special)
    bool TryReadString(ExternalObjectRef obj, int maxChars, out string? value);

    // Arrays
    bool TryGetArrayLength(ExternalObjectRef arrayObj, out int length);

    // Field reads: returns raw-ish values; interpretation into TValue happens elsewhere
    bool TryReadField(ExternalObjectRef obj, FieldHandle field, out ExternalValue value);

    // Array element reads (for common primitive/refs; advanced: element address)
    bool TryReadArrayElement(ExternalObjectRef arrayObj, int index, out ExternalValue value);
}
```

This is intentionally **not** a “read raw memory by address” API; ClrMD can implement this robustly even when internal layouts vary.

If you *also* want a byte-level API for advanced decoders, add:

```csharp
public interface IProcessMemoryReader
{
    bool TryRead(ulong address, Span<byte> destination);
}
```

…but keep it optional.

---

## 3.5 Frame seeding (this/args/locals)

This keeps “how to get locals from a dump frame” out of the VM.

```csharp
namespace Interpreter.Host.Abstractions;

public sealed record FrameSeed(
    ExternalObjectRef? ThisObject,
    IReadOnlyList<ExternalValue> Arguments,
    IReadOnlyDictionary<string, ExternalValue> LocalsByName);

public interface IFrameSeeder
{
    // Host-specific selection (thread/frame)
    bool TrySeedFrame(ExternalThreadId thread, ExternalFrameId frame, out FrameSeed seed);
}
```

If locals aren’t available, `LocalsByName` can be empty; the interpreter will create Unknown values.

---

## 3.6 Runtime ↔ metadata bridge

This is where you connect “runtime method at IP” to `ModuleId` + metadata token.

```csharp
using Interpreter.Metadata.Abstractions;

namespace Interpreter.Host.Abstractions;

public readonly record struct RuntimeMethodId(ulong Value);  // MethodDesc, or code address, host-defined
public readonly record struct RuntimeModuleId(ulong Value);  // module base, host-defined

public sealed record RuntimeMethodInfo(
    RuntimeMethodId RuntimeId,
    ModuleId Module,
    int MethodToken); // MethodDef token when known

public interface IRuntimeMetadataBridge
{
    bool TryMapMethod(RuntimeMethodId runtimeMethod, out RuntimeMethodInfo info);
    bool TryMapModule(RuntimeModuleId runtimeModule, out ModuleId module);
}
```

Dump hosts can fill this via ClrMD’s method/module info; live hosts via debugger APIs.

---

## 3.7 Optional: generic context resolver

This is hard and host-dependent; make it optional.

```csharp
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

public interface IGenericContextResolver
{
    bool TryResolveGenericContext(RuntimeMethodId runtimeMethod, ExternalObjectRef? thisObj, out GenericContext ctx);
}
```

If it fails, the interpreter still runs with unknown/canonical generic args.

---

## 3.8 What must NOT live in `Interpreter.Host.Abstractions`

* ClrMD types (those go in `Interpreter.Host.Dump.ClrMD`)
* metadata reader types (SRM/AsmResolver)
* IL interpreter state and stepping logic
* symbol server / artifact acquisition policy (separate layer)

---

# 4) How these abstractions compose (quick wiring example)

In a dump-debugging product, the “composition root” usually builds:

1. `IMetadataUniverse` (SRM-based)
2. `IDebugMapProvider` (PDB first, then ILSpy, then IL synthetic)
3. Dump host services:

   * `IExternalObjectModel` (ClrMD-backed)
   * `IFrameSeeder` (ClrMD-backed)
   * `IRuntimeMetadataBridge` (ClrMD-backed)
   * `ISessionSnapshotProvider` (dump header + best-effort)
4. `IResolutionServices` implementation that:

   * uses `IMetadataUniverse` to resolve token operands + bodies + virtual overrides
5. The VM engine (in `Interpreter.Core.Execution`) uses:

   * `IValueDomain<TValue>`
   * `IMemoryModel<TValue,TMem>`
   * `IResolutionServices`
   * `ICallModel<TValue,TMem>` chain (async/dynamic/env/etc.) via call dispatcher

All the heavy libraries stay in leaf packages, not in these abstractions.
