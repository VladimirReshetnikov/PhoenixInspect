## Integration proposal: IL Interpreter ↔ ClrMD ↔ PE/PDB (AsmResolver)

You can make this integration feel “debugger-grade” *without* welding your IL interpreter to any single dump/metadata stack — but you’ll want one deliberate layer in between. Otherwise you end up with an interpreter whose “type system” is a Frankenstein of ClrMD objects + metadata tokens + PDB concepts, and it becomes painful to reuse for static analysis or alternate runtimes.

### Executive answer to “do we need a layer in between?”

Yes — you want a **binding layer** (call it `ProgramModel` / `RuntimeBinding` / `ExecutionHost`) that:

1. **Maps runtime identity → metadata identity** (ClrMD’s `ClrMethod` / `ClrType` / `ClrModule` ⇄ PE metadata tokens, MVIDs, MethodDefs/MemberRefs/MethodSpecs).
2. **Chooses the best source of truth** for IL/metadata/symbols (dump memory if present, on-disk image if available, symbol server if configured).
3. **Presents a stable, interpreter-friendly API**: “give me a method body + signature + EH clauses + local sig + (optional) symbols”.

That keeps your IL interpreter reusable (static analysis, abstract interpretation, fuzzing IL) while the host layer deals with dump reality.

---

## 1) Three “universes” you must reconcile

### A) Runtime universe (ClrMD)

ClrMD gives you:

* Object graph snapshots (heap, references, field layouts) and raw memory reads via the target’s data reader.
* Runtime method/type identities and call stacks.
* For many methods, **IL location + size** via `ClrMethod.GetILInfo()` (it computes the IL address/length and locals signature token). ([GitHub][1])

ClrMD also has first-class support for loading dumps and managing symbol-path-like lookup for binaries through `DataTarget.FileLocator` and `SetSymbolPath`. ([GitHub][2])

### B) Metadata/IL universe (PE + ECMA-335)

You need a real metadata reader for:

* Token resolution (TypeDef/TypeRef/TypeSpec, MethodDef/MemberRef/MethodSpec, FieldDef/MemberRef, etc.)
* Signature decoding (generic instantiation, modifiers, locals)
* Parsing *managed* method bodies including EH sections, maxstack, initlocals

ClrMD explicitly moved away from being a “PE reader toolkit”; it even notes its PE/ELF helpers became internal and suggests using a real PE/metadata library instead (e.g., `System.Reflection.Metadata`). ([GitHub][3])
That’s exactly where AsmResolver is a good fit.

### C) Symbols universe (PDB / Portable PDB)

For expression evaluation, you want:

* Local names + scopes
* Sequence points (IL offset ↔ source)
* Async state machine mapping / hoisted locals patterns (nice-to-have early, essential later)

AsmResolver has:

* High-level managed method body tooling (CIL disassembly/assembly). ([Washi Docs][4])
* Portable PDB as a metadata extension (`PdbStream`). ([Washi Docs][5])
* Windows PDB models (`AsmResolver.Symbols.Pdb`) including rich record types. ([NuGet][6])

---

## 2) Proposed layering (keep the interpreter clean)

Here’s the layering that scales:

```
+--------------------------------------------------------------+
|                        IL Interpreter Core                    |
|  - Stack machine, call/ret, domains (concrete/unknown/etc.)   |
|  - No I/O, no dump knowledge, no ClrMD/AsmResolver types      |
+---------------------------^----------------------------------+
                            |
                            | (Interpreter-facing interfaces)
                            |
+---------------------------+----------------------------------+
|                 ProgramModel / RuntimeBinding                |
|  - MethodBodyResolver (dump vs PE)                            |
|  - TokenResolver (metadata tables + generic context)          |
|  - SymbolResolver (locals, scopes, seq points)                |
|  - RuntimeValueProvider (locals/args/this from ClrMD frames)  |
|  - HeapBridge (dump heap + virtual heap)                      |
+------------^---------------------------^----------------------+
             |                           |
             |                           |
+------------+-----------+     +---------+----------------------+
|       ClrMD Adapter    |     |     PE/PDB Adapter (AsmResolver)|
|  - DataTarget/ClrRuntime|     |  - ModuleDefinition/Metadata    |
|  - heap/threads/frames  |     |  - CIL bodies / EH parsing      |
|  - Read memory bytes    |     |  - PDB/Portable PDB reading     |
+------------------------+     +---------------------------------+
```

### Why this boundary matters

* The interpreter can now run:

  * on dumps (ClrMD backend)
  * on static binaries (AsmResolver backend)
  * in “hybrid” mode (dump values + disk IL)
* Your unknown/abstract interpretation features remain usable outside debugging.

---

## 3) Core abstraction: stable IDs + resolvers

### 3.1 Stable identity types (don’t leak ClrMD/AsmResolver objects)

Define internal identifiers that are cheap and comparable:

* `ModuleId`: ideally **MVID** (GUID in the metadata `Module` table) + fallback (path + timestamp + size) when MVID is unavailable.
* `MethodId`: `(ModuleId, MetadataToken)` — but be mindful tokens can be `MemberRef`/`MethodSpec` too.
* `TypeId`: `(ModuleId, MetadataToken)` or `(ModuleId, TypeSpecSigHash)` for TypeSpec-heavy cases.

In the binding layer, you convert:

* `ClrMethod` → `MethodId`
* `ClrType` → `TypeId`
* `ClrModule`/`ModuleInfo` → `ModuleId`

ClrMD already tracks modules (and has binary-location mechanisms via `IFileLocator`). ([GitHub][2])

---

## 4) Method body acquisition strategy (dump-first, PE-second)

### 4.1 The decision tree

When the interpreter needs IL for a `MethodId`:

1. **Try dump memory** (best fidelity for dynamic methods / EnC / in-memory-only):

   * If you have a `ClrMethod`, call `GetILInfo()` to get IL address and length and locals signature token. ([GitHub][1])
   * Read bytes from the dump using `DataTarget.DataReader` (via your ClrMD adapter).

2. **Fallback to PE file** (essential for heap-only dumps missing module pages):

   * Use `DataTarget.FileLocator` (symbol-path aware) to obtain a local PE path when possible. ([GitHub][2])
   * Parse with AsmResolver: `ModuleDefinition.FromFile(...)`, locate MethodDef/MethodSpec, then obtain CIL body. ([Washi Docs][7])

3. **If both unavailable**:

   * Return an “unavailable body” sentinel and let the interpreter produce “unknown outcome” (per your framework’s philosophy).

### 4.2 Why dump-memory isn’t enough

Heap-only dumps often exclude mapped images; the runtime state exists but the PE pages (and sometimes IL) aren’t present. Your design must not assume IL is readable from the snapshot. That’s why the PE fallback is not optional.

### 4.3 Parsing EH clauses

ClrMD’s `ILInfo` gives you IL bytes boundaries and header flags, but you still need to parse any “extra sections” (EH tables). AsmResolver already models “extra sections” for CIL method bodies. ([Washi Docs][8])
So a very pragmatic approach is:

* If you read IL bytes from dump memory:

  * Feed them into a small “CIL body parser” module (can be your own, or reuse AsmResolver’s reader if you can provide a stream abstraction).
* If you read from PE:

  * Let AsmResolver decode the full method body (including EH) directly.

---

## 5) Metadata/token resolution: pick one metadata engine

You’ll be tempted to use:

* ClrMD runtime structures to resolve some things,
* AsmResolver for others,
* and a little string parsing in the corners.

Don’t. Pick one “metadata truth” for **ECMA-335 identity resolution**.

### Recommendation

Use AsmResolver (or `System.Reflection.Metadata`) as your canonical metadata engine for:

* decoding signatures,
* resolving MemberRefs/MethodSpecs,
* mapping tokens to declaring types/method names,
* reading custom attributes used for compiler patterns.

AsmResolver’s .NET abstraction stack explicitly treats methods as including method bodies etc., and exposes mutable high-level models you can treat as read-only. ([Washi Docs][7])

### What ClrMD remains best at

Use ClrMD as the canonical source for:

* object layout + field offsets in the *actual runtime*,
* addresses, heap segmentation, GCDesc, etc.

In other words:

* **metadata** answers “what the code says”
* **ClrMD** answers “what memory looks like”

Your binding layer ties them together.

---

## 6) Symbols/PDB integration: treat it as a separate optional service

ClrMD’s modern file-location API (`IFileLocator`) is about locating **images**, not PDBs. ([GitHub][9])
So you should architect symbols as an *optional* service:

### 6.1 Interfaces

* `ISymbolResolver`:

  * `GetLocals(MethodId) -> IReadOnlyList<LocalSymbol>`
  * `GetSequencePoints(MethodId) -> IReadOnlyList<SeqPoint>`
  * `GetScopes(MethodId) -> ScopeTree`

### 6.2 Backends

* Portable PDB:

  * Use AsmResolver’s Portable PDB support via the metadata `PdbStream`. ([Washi Docs][5])
* Windows PDB:

  * Use `AsmResolver.Symbols.Pdb` and its record model when needed. ([NuGet][6])

### 6.3 Locating the PDB

Your `ProgramModel` should:

1. Parse the PE debug directory to obtain PDB identity (GUID/age, path).
2. Search in:

   * “same directory as PE”
   * user-configured symbol caches
   * symbol server (SSQP conventions)

ClrMD’s symbol-path handling is already built around SSQP conventions for *binaries* and symbol-server style paths. ([GitHub][3])
For PDBs specifically, you’ll likely implement a tiny PDB-locator using the same symbol-store conventions (or integrate with a symstore-like library). Keep this separate from the interpreter.

---

## 7) The “bridge” layer’s key responsibilities

### 7.1 `ModuleImageStore` (cache + provenance)

A central service that answers: “given ModuleId, give me the bytes/stream for PE + optional PDB”.

Design points:

* Cache by `(ModuleId, source)`:

  * `DumpMappedImage` (if present in dump)
  * `OnDiskPath`
  * `DownloadedSymbolCache`
* Validate identity:

  * If you locate a PE by name+timestamp+size, still verify MVID matches what you expect (when you can).

### 7.2 `MethodBodyResolver`

Given a `MethodId`:

* Return:

  * IL bytes
  * maxstack, initlocals
  * locals signature token / decoded locals signature
  * EH clauses
  * “body provenance” (dump vs PE) for diagnostics

### 7.3 `TokenResolver` with generic context

For interpretation you need to resolve tokens under a **generic context**:

* `GenericContext` = (declaring type instantiation args, method instantiation args)
* Tokens might resolve to TypeSpec/MethodSpec which embed signatures containing generic variables.

This resolver should be purely metadata-based (AsmResolver), but it should be able to *ask ClrMD* for runtime type handles when you need to:

* allocate objects of a resolved type,
* compute field offsets or size,
* do virtual dispatch based on runtime type.

### 7.4 `HeapBridge`: dump heap + virtual heap

You described “virtual new objects” and “virtual delegates”. The binding layer is where this lives:

* **DumpHeapArena** (read-only):

  * addresses are dump addresses (`ulong`)
  * reads use ClrMD memory
* **VirtualHeapArena** (mutable):

  * allocate “virtual objects” not present in dump
  * store them in your interpreter’s memory model
  * optionally allow “materializing” simple framework types (like `string`) without touching dump

The interpreter itself just sees an `IHeap` + `IObjectModel`; it doesn’t know where the object came from.

---

## 8) Practical integration flows

### Flow A: “evaluate expression at stack frame”

1. User selects thread + frame.
2. ClrMD adapter provides:

   * `this`, args, locals (as addresses/values)
   * current `ClrMethod`
3. Binding layer:

   * maps `ClrMethod` → `MethodId`
   * resolves method IL:

     * dump memory via `GetILInfo()` if possible ([GitHub][1])
     * else PE via `FileLocator` and AsmResolver ([GitHub][2])
   * resolves symbols (optional)
4. Interpreter executes in a context:

   * locals/args seeded from frame
   * heap reads go through DumpHeapArena
   * unknowns injected when data is missing

### Flow B: “evaluate property getter on arbitrary heap object”

1. ClrMD identifies object address + runtime type.
2. Binding layer:

   * resolves target method (getter MethodDef/MemberRef)
   * resolves IL + metadata
3. Interpreter executes; any “world interaction” APIs:

   * are modeled as “unknown / effectful” (your earlier design)
   * may return unknown values and record side-effect traces

---

## 9) Why AsmResolver is a good match here

Not marketing — just architectural fit:

* You need a **real** PE/metadata reader because ClrMD intentionally stopped being one. ([GitHub][3])
* AsmResolver covers:

  * CIL method bodies & extra sections (EH) ([Washi Docs][4])
  * signature decoding patterns you’ll need for MethodSpec/TypeSpec-heavy code ([Washi Docs][10])
  * both Portable PDB (metadata extension) and Windows PDB models ([Washi Docs][5])

That aligns with your goal: one pipeline that works even when dump memory is missing, and still supports post-mortem symbol-rich UX when artifacts can be found.

---

## 10) Concrete “bridge API” sketch

Not implementation, but the shape you want:

```csharp
public interface IProgramModel
{
    IRuntimeSnapshot Runtime { get; }        // ClrMD-backed
    IMetadataUniverse Metadata { get; }      // AsmResolver-backed
    IMethodBodyResolver MethodBodies { get; }
    ISymbolResolver? Symbols { get; }        // optional
    IHeapBridge Heap { get; }                // dump + virtual
}

public interface IMethodBodyResolver
{
    MethodBodyInfo ResolveBody(MethodId method);
}

public readonly record struct MethodBodyInfo(
    ReadOnlyMemory<byte> IlBytes,
    int MaxStack,
    bool InitLocals,
    LocalSignatureInfo Locals,
    IReadOnlyList<ExceptionHandlerInfo> ExceptionHandlers,
    MethodBodyProvenance Provenance);
```

The important part is: **these types don’t mention ClrMD or AsmResolver**.

---

## Summary

* **ClrMD** is your runtime/dump truth: heap objects, field layout, stack frames, and often IL addresses via `ClrMethod.GetILInfo()`. ([GitHub][1])
* **AsmResolver** (or SRM) is your metadata truth: token resolution, signature decoding, full method body parsing (including EH), and PDB parsing. ([Washi Docs][4])
* You absolutely want a **binding layer in between** to:

  * unify identity,
  * arbitrate data sources (dump vs PE vs symbol cache),
  * and present a clean API to the interpreter.

[1]: https://raw.githubusercontent.com/microsoft/clrmd/main/src/Microsoft.Diagnostics.Runtime/ClrMethod.cs "raw.githubusercontent.com"
[2]: https://raw.githubusercontent.com/microsoft/clrmd/main/src/Microsoft.Diagnostics.Runtime/DataTarget.cs "raw.githubusercontent.com"
[3]: https://raw.githubusercontent.com/microsoft/clrmd/main/doc/Migrating21.md "raw.githubusercontent.com"
[4]: https://docs.washi.dev/asmresolver/guides/dotnet/managed-method-bodies.html?utm_source=chatgpt.com "CIL Method Bodies | AsmResolver - Washi"
[5]: https://docs.washi.dev/asmresolver/api/pe/AsmResolver.PE.DotNet.Metadata.html?utm_source=chatgpt.com "Namespace AsmResolver.PE.DotNet.Metadata - Washi"
[6]: https://www.nuget.org/packages/AsmResolver.Symbols.Pdb/?utm_source=chatgpt.com "AsmResolver.Symbols.Pdb 5.5.1"
[7]: https://docs.washi.dev/asmresolver/guides/dotnet/index.html?utm_source=chatgpt.com "Overview | AsmResolver - Washi"
[8]: https://docs.washi.dev/asmresolver/api/pe/AsmResolver.PE.DotNet.Cil.html?utm_source=chatgpt.com "Namespace AsmResolver.PE.DotNet.Cil - Washi"
[9]: https://raw.githubusercontent.com/microsoft/clrmd/main/src/Microsoft.Diagnostics.Runtime/IFileLocator.cs "raw.githubusercontent.com"
[10]: https://docs.washi.dev/asmresolver/guides/dotnet/type-signatures.html?utm_source=chatgpt.com "Type Signatures | AsmResolver - Washi"
