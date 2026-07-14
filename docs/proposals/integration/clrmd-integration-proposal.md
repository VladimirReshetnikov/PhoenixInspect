# Integration proposal: Dump evaluator ↔ ClrMD ↔ SRM/PE/PDB

> **Lifecycle:** Draft · **Roadmap:** Active

## Implemented W3 boundary (2026-07-14)

Exact hardened implementation checkpoint `19c292f9f` turns the central binding seam into executable evidence for one closed
non-cybersecurity profile:

- project-owned structural module, type, MethodDef, and FieldDef identities isolate the interpreter from ClrMD and
  SRM object identity;
- SRM projects a method's body, signature, return shape, and local vector atomically from one metadata image, and
  resolves the admitted `ldfld` in that frozen method context;
- the dump host reparses the counted physical body, proves that its sole `ldfld` operand is the correlated runtime
  `Int32` field, imports only an exact four-byte observation into persistent memory, and never gives the machine a live
  `ClrmdDumpSession`; and
- reopen/rebind replay reconstructs the same prepared-memory transcript, while a disk PE remains a late independent
  CoreCLR/equality oracle rather than resolver input.

Local headless verification passed a zero-warning 15-project Release build, 103 non-cybersecurity unit tests, 67 fast
integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3 lane, and both
documentation guards. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs at
the same exact pushed implementation checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). W3 is complete for its defined
non-cybersecurity scope.

This checkpoint does not implement product method evaluation, calls, branches, broader opcode families, generic
context reconstruction, Portable PDB projection, a second meaningful value domain, or cybersecurity validation.

You can make this integration feel “debugger-grade” *without* welding your IL interpreter to any single dump/metadata stack — but you’ll want one deliberate layer in between. Otherwise you end up with an interpreter whose “type system” is a Frankenstein of ClrMD objects + metadata tokens + PDB concepts, and it becomes painful to reuse for static analysis or alternate runtimes.

### Executive answer to “do we need a layer in between?”

Yes — you want a **binding layer** (call it `ProgramModel` / `RuntimeBinding` / `ExecutionHost`) that:

1. **Maps runtime identity → metadata identity** (ClrMD’s `ClrMethod` / `ClrType` / `ClrModule` ⇄ PE metadata tokens, MVIDs, MethodDefs/MemberRefs/MethodSpecs).
2. **Keeps evidence sources explicit**: dump-backed execution uses only exact dump metadata/memory for its method body, while an on-disk image may serve static analysis, symbols, or an independently labeled comparison oracle.
3. **Presents a stable, interpreter-friendly API**: “give me a method body + signature + EH clauses + local sig + (optional) symbols”.
4. **Freezes the admitted execution view**: the W3 path resolves method shape and field operands once before
   instruction zero, and converts incomplete dump evidence into a typed stop rather than a default value.

That keeps your IL interpreter reusable (static analysis, abstract interpretation, fuzzing IL) while the host layer deals with dump reality.

---

## 1) Three “universes” you must reconcile

### A) Runtime universe (ClrMD)

ClrMD gives you:

* Object graph snapshots (heap, references, field layouts) and raw memory reads via the target’s data reader.
* Runtime method/type identities and call stacks.
* For many methods, IL location hints via `ClrMethod.GetILInfo()`. The active prototype deliberately does not use those hints to construct a body: it reads the MethodDef RVA from counted dump metadata and decodes the physical method body itself. ([GitHub][1])

ClrMD also has first-class support for loading dumps and managing symbol-path-like lookup for binaries through `DataTarget.FileLocator` and `SetSymbolPath`. ([GitHub][2])

### B) Metadata/IL universe (PE + ECMA-335)

You need a real metadata reader for:

* Token resolution (TypeDef/TypeRef/TypeSpec, MethodDef/MemberRef/MethodSpec, FieldDef/MemberRef, etc.)
* Signature decoding (generic instantiation, modifiers, locals)
* Parsing *managed* method bodies including EH sections, maxstack, initlocals

ClrMD explicitly moved away from being a “PE reader toolkit”; it even notes its PE/ELF helpers became internal and suggests using a real PE/metadata library instead (e.g., `System.Reflection.Metadata`). ([GitHub][3])
The active prototype uses `System.Reflection.Metadata`/`PEReader` for that role.

### C) Symbols universe (PDB / Portable PDB)

For expression evaluation, you want:

* Local names + scopes
* Sequence points (IL offset ↔ source)
* Async state machine mapping / hoisted locals patterns (nice-to-have early, essential later)

The longer-term symbol/decompilation candidates include:

* High-level managed method body tooling (CIL disassembly/assembly). ([Washi Docs][4])
* Portable PDB as a metadata extension (`PdbStream`). ([Washi Docs][5])
* Windows PDB models (`AsmResolver.Symbols.Pdb`) including rich record types. ([NuGet][6])

---

## 2) Proposed layering (keep the interpreter clean)

Here’s the layering that scales:

```
+--------------------------------------------------------------+
|                        IL Interpreter Core                    |
|  - W3 stack machine + closed E1/E2 concrete semantics        |
|  - No I/O, no dump knowledge, no ClrMD/SRM types              |
+---------------------------^----------------------------------+
                            |
                            | (Interpreter-facing interfaces)
                            |
+---------------------------+----------------------------------+
|                 ProgramModel / RuntimeBinding                |
|  - MethodBodyResolver (source-explicit; never silent mixing)  |
|  - Frozen W3 method/field projection; later token/generic     |
|  - Optional future symbol/frame services                      |
|  - Exact dump evidence import into persistent memory          |
|  - Future combined dump/virtual heap bridge                   |
+------------^---------------------------^----------------------+
             |                           |
             |                           |
+------------+-----------+     +---------+----------------------+
|       ClrMD Adapter    |     |       PE/PDB Adapter (SRM)       |
|  - DataTarget/ClrRuntime|     |  - PEReader/MetadataReader       |
|  - heap/threads/frames  |     |  - method bodies/signatures      |
|  - typed memory reads   |     |  - Portable PDB reading          |
+------------------------+     +---------------------------------+
```

### Why this boundary matters

* The current core executes only closed E1/E2 plans through project-owned resolution and memory interfaces.
  Dump-free differential plans are projected from a content-identified PE by SRM; the E2 dump plan is prepared from
  counted dump metadata/body/owner/field evidence and imported persistent memory. The machine has no live ClrMD
  backend, and no product artifact-execution mode exists.
* A future explicitly artifact-backed product mode requires its own evidence contract; it is not implied by the disk
  differential fixture.
* Unknown/abstract interpretation remains a later domain and product question, not current dump integration behavior.

---

## 3) Core abstraction: stable IDs + resolvers

### 3.1 Stable identity types (don’t leak ClrMD/AsmResolver objects)

Define internal identifiers that are cheap and comparable:

* Dump metadata-root identity: **MVID** plus exact metadata-image length and SHA-256.
* Complete disk-artifact identity: exact whole-file length plus SHA-256, carried in addition to metadata identity and optional PE timestamp/image size. A path is a location hint, never identity.
* `MethodId`: `(ModuleId, MethodDefToken)` in W3. `MemberRef`/`MethodSpec` and generic context are later structural
  extensions, not aliases for an admitted MethodDef.
* `TypeId`: `(ModuleId, MetadataToken)` or `(ModuleId, TypeSpecSigHash)` for TypeSpec-heavy cases.

In the binding layer, you convert:

* `ClrMethod` → `MethodId`
* `ClrType` → `TypeId`
* `ClrModule`/`ModuleInfo` → `ModuleId`

ClrMD already tracks modules (and has binary-location mechanisms via `IFileLocator`). ([GitHub][2])

---

## 4) Method body acquisition strategy (dump-first, PE-second)

### 4.1 The decision tree

When the active dump-backed path needs IL for a `MethodId`:

1. **Read and validate the complete required dump evidence**:

   * Read the selected module's complete metadata root through a counted memory read and validate the runtime MethodDef token.
   * Decode its implementation kind and RVA from that dump metadata, map the RVA only for admitted mapped/loaded PE layouts, and read the physical tiny/fat header through the bounded adapter.
   * Decode `maxstack`, init-locals, the StandAloneSig token, and code size from the header; validate the token against the counted dump metadata; then read the exact code and every declared extra section from dump memory.

2. **Fail honestly when dump evidence is incomplete**:

   * Partial, absent, out-of-image, malformed, or policy-limited metadata/header/code/section reads produce a typed outcome and never an executable body.
   * The active path does not replace a missing dump range with bytes or admission facts from disk.

3. **Keep disk PE use independent**:

   * An independently opened PE is identified by exact whole-file length/SHA-256 as well as metadata-root identity. SRM may decode it for a fixture equality assertion, a static-artifact scenario, or later symbol/token work, but it supplies no argument to the dump-backed body's construction.

### 4.2 Why dump-memory isn’t enough

Heap-only dumps often exclude mapped images; the runtime state exists but the metadata, header, code, or extra-section pages may be absent. The active evaluator reports that missing evidence. A future artifact-backed evaluation mode would need its own product semantics and provenance contract; silently presenting disk IL as a dump body is not a fallback.

All dump reads are bounded and return exact byte counts. Sparse pages, invalid addresses, corrupt runtime structures, and policy limits are ordinary typed evidence outcomes. External dumps are rejected above 8 GiB, and ClrMD's dump cache is capped at 256 MiB with stack-trace/root caching disabled; the typed external-PE `Open` boundary rejects artifacts above 512 MiB. Dump strings, paths, environment values, and raw bytes are secret-bearing and must not enter telemetry or exception text by default. These caps are resource controls, not a sandbox. Arbitrary external dumps require worker-process and access-control isolation before product exposure.

### 4.3 Parsing EH clauses

The active bounded decoder obtains a complete body without borrowing `ILInfo` or PE-body facts:

* get the MethodDef RVA and StandAloneSig table bound from the exact counted dump metadata image;
* parse tiny/fat headers, bounded code, four-byte alignment, and small/fat chained EH sections through counted dump reads;
* retain ordered header/code/extra-section evidence and expose a normalized body only when the required physical evidence is exact;
* let `PEReader` independently decode a disk artifact for comparison, without feeding its `maxstack`, locals, or EH facts into the dump result.

---

## 5) Metadata/token resolution: pick one metadata engine

You’ll be tempted to use:

* ClrMD runtime structures to resolve some things,
* another metadata library for others,
* and a little string parsing in the corners.

Don’t. Pick one “metadata truth” for **ECMA-335 identity resolution**.

### Recommendation

Use `System.Reflection.Metadata`/`PEReader` as the active metadata engine for:

* decoding the closed W3 method/local/field signature vocabulary;
* resolving MethodDef and same-module FieldDef identities plus their declaring types;
* mapping tokens to structural project-owned types, methods, and fields; and
* later, only when an admitted scenario requires them, resolving MemberRefs/MethodSpecs, generic substitution, custom
  attributes, and Portable PDB records.

Project the low-level reader results into project-owned, immutable identities and evidence results. Revisit alternative backends only when a checked-in corpus exposes a material limitation.

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

  * Use SRM's Portable PDB metadata reader when an active expression or source-mapping fixture requires symbols.
* Windows PDB:

  * Defer backend selection until a Windows-PDB fixture becomes an active requirement; DIA, DiaSymReader, dnlib, and AsmResolver notes remain research inputs rather than dependencies.

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

* The implemented W3 resolver returns one atomic method definition containing:

  * IL bytes
  * maxstack, initlocals
  * decoded calling shape, return type, and locals signature
  * declared exception-region count/presence
  * project-owned structural identities

  Counted dump provenance remains on `ClrmdDumpExecutionResolver` and
  `ClrmdExactInt32FieldExecutionEvidence`; it is deliberately not embedded in the core `ResolvedMethodDefinition`.

The active E1/E2 admission rejects every EH-bearing body. The host retains exact counted extra-section evidence, while
core retains the declared region count needed for admission; neither is a claim that handler clauses or transfer are
implemented.

### 7.3 `TokenResolver` with generic context

Broader interpretation will need to resolve tokens under a **generic context**:

* `GenericContext` = (declaring type instantiation args, method instantiation args)
* Tokens might resolve to TypeSpec/MethodSpec which embed signatures containing generic variables.

That future resolver should remain metadata-based (SRM/PEReader in the active prototype), but it may need to *ask ClrMD* for runtime type handles when a later scenario needs to:

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

### Implemented W3 flow: execute one prepared dump getter

1. The ClrMD adapter selects one exact rooted object, runtime module, and runtime instance-field descriptor.
2. Counted dump reads provide the exact metadata image and physical getter body; SRM projects the structural method
   shape and contextual FieldDef from those bytes.
3. The host proves the admitted body's sole `ldfld` names that same directly declared `Int32` field, then imports only
   its exact four-byte observation into an immutable persistent-memory snapshot.
4. Non-exact dump evidence stops preparation before activation and is never imported. After exact preparation,
   metadata-derived activation and frozen whole-body admission complete before instruction zero; the machine executes
   the direct or constant-adjusted getter through `IMemoryModel`. A deliberately absent imported cell instead blocks
   at `ldfld` after the preceding `ldarg.0`, without a field transfer or fabricated zero/unknown.
5. Reopening the dump repeats selection, correlation, and import, then reproduces the canonical execution transcript.

### Future Flow A: “evaluate expression at stack frame”

1. User selects thread + frame.
2. ClrMD adapter provides:

   * `this`, args, locals (as addresses/values)
   * current `ClrMethod`
3. Binding layer:

   * maps `ClrMethod` → `MethodId`
   * resolves a dump-backed method body from counted dump metadata/header/code/extra-section reads; incomplete evidence blocks that path rather than silently substituting disk bytes
   * may resolve an independently identified PE through SRM/PEReader for symbols, static-artifact workflows, or comparison, with source provenance kept distinct ([GitHub][2])
   * resolves symbols (optional)
4. Interpreter executes in a context:

   * locals/args seeded from frame
   * heap reads go through DumpHeapArena
   * a later unknown-aware domain may represent missing data only after its own contract and evidence gate; W3 stops
     on non-exact memory evidence

### Future Flow B: “evaluate property getter on arbitrary heap object”

1. ClrMD identifies object address + runtime type.
2. Binding layer:

   * resolves target method (getter MethodDef/MemberRef)
   * resolves IL + metadata
3. Interpreter executes; any “world interaction” APIs:

   * are modeled as “unknown / effectful” (your earlier design)
   * may return unknown values and record side-effect traces

Neither future flow is a current product capability. Calls, generic context, PDB-backed locals, effects, and an
unknown-aware second domain remain separate gates after the closed W3 proof.

---

## 9) Why SRM/PEReader is the active match

Not marketing — just architectural fit:

* You need a **real** PE/metadata reader because ClrMD intentionally stopped being one. ([GitHub][3])
* SRM/PEReader covers the active W3 requirements:

  * PE/module identity and method bodies;
  * the closed method/local/field signature and token projection.

The same library can read Portable PDB metadata, but that becomes project capability only when an admitted symbol
fixture exercises a project-owned projection. W3 contains no such fixture.

It is already exercised in-tree, aligns with the likely Portable PDB and ILSpy paths, and avoids funding a second object model before the first delivers product evidence. Windows PDB and richer object-model needs remain separate, evidence-gated decisions.

---

## 10) Concrete “bridge API” sketch

Not implementation, but the shape you want:

```csharp
public interface IProgramModel
{
    IRuntimeSnapshot Runtime { get; }        // ClrMD-backed
    IMetadataUniverse Metadata { get; }      // SRM-backed in the active prototype
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

The important part is: **these types don’t mention ClrMD or SRM** and partial acquisition is represented explicitly.

---

## Summary

* **ClrMD plus counted raw reads** is the active dump truth: heap objects, field layout, metadata-root identity, and
  complete captured method bodies. `GetILInfo()` remains a useful library capability, but it is not an input to the
  active body decoder. Frame recovery remains a later product concern. ([GitHub][1])
* **SRM/PEReader** is the active metadata decoder over exact counted dump metadata and over independently identified
  disk artifacts. W3 uses it for the closed structural method/signature/local/FieldDef projection; the disk PE remains
  an oracle, and Portable PDB projection remains unimplemented.
* You absolutely want a **binding layer in between** to:

  * unify identity,
  * keep data sources and misses explicit rather than silently mixing dump and PE body facts,
  * freeze admission facts before execution,
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
