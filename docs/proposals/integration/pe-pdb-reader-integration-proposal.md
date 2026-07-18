# PE/PDB reader integration proposal (draft)

## Decision and W3 implementation alignment (2026-07-14)

For the active dump/query and first interpreter slices, this proposal aligns to `docs/lib/mvp-backend-decision-record.md`:

- **Active backend:** `System.Reflection.Metadata` + `PEReader`
- **Alternative adapters:** research candidates, introduced only by an executable corpus gap
- **Comparison set retained in docs:** AsmResolver, dnlib, and Mono.Cecil trade-off analysis remains useful reference material
- **Active dump-body boundary:** MethodDef RVA, tiny/fat header, code, local-signature token, and declared extra sections come from counted dump metadata/memory. A disk PE has separate whole-file length/SHA-256 identity and is only an independent oracle; it is never a source of hidden admission facts for that body.
- **Implemented W3 projection:** exact strengthened implementation checkpoint `19c292f9f` projects structural
  module/type/MethodDef/FieldDef identities,
  atomic method body/signature/return/local shape, and contextual same-module FieldDef resolution. Its real-dump getter
  resolver operates on counted dump metadata/body bytes, proves that the admitted `ldfld` is the correlated runtime
  field, and reproduces the prepared-memory transcript after dump reopen/rebind.
- **Still gated:** Portable/Windows PDB projection, SourceLink, generic context, MemberRef/MethodSpec execution,
  broader opcode families, a second meaningful domain, product method evaluation, and validation beyond the named fixture shapes.

The comparison material below is historical research. Concrete recommendations and implementation work use SRM while preserving project-owned identities, evidence outcomes, and decision-revisit triggers. Local headless verification at `19c292f9f` passed a zero-warning 15-project Release build, 103 milestone-selected unit tests, 67 fast integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3 lane, and both documentation guards. [GitHub Actions run 29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs at the same exact pushed implementation checkpoint. Exact documentation-closure commit `de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). W3 is complete for its defined milestone-selected scope.

---

I’m going to (1) survey the best OSS building blocks for PE/metadata + PDB + decompilation, then (2) propose the “glue layer” that makes them cooperate cleanly with **ClrMD** and your **IL interpretation framework**.

---

# PE/PDB + Decompilation: library choices, trade-offs, and how they fit together

In a post-mortem / dump-driven debugger, you’re always trying to answer the same questions:

1. **What code is this method?** (resolve a runtime method → metadata identity)
2. **What IL do I execute?** (get IL bytes + EH + locals sig)
3. **How do I map IL offsets to “something the user can read”?**

   * Prefer *real source* (PDB sequence points + SourceLink/embedded source)
   * Fall back to *decompiled C#* with a debug map that still supports stepping
4. **Where do I get binaries/symbols from when the dump doesn’t contain them?**

The hard part isn’t any single library — it’s stitching them into a coherent pipeline with caching, identity checks, and graceful degradation.

---

## 1) PE / .NET metadata reading

### Option A: **System.Reflection.Metadata + System.Reflection.PortableExecutable** (SRM/PEReader)

**What it is:** Microsoft’s low-level, high-performance readers for ECMA-335 metadata and PE files; also includes APIs to read Portable PDB structures such as `SequencePoint`. ([Microsoft Learn][1])

**Why you’d pick it**

* **Canonical** for modern .NET tooling; Portable PDB format is designed as an extension of ECMA-335 metadata, and SRM is the “native” way to read it. ([GitHub][2])
* **Portable PDB first-class** (sequence points, scopes, documents, custom debug info, etc.). ([GitHub][2])
* **Excellent fit with ILSpy’s decompiler**, which itself uses SRM types under the hood. ([GitHub][3])

**Why you might not**

* It’s *deliberately low-level*: you’ll write your own higher-level “model” (method body parsing, signature decoding helpers, convenience caches).

**Typical role in your stack**

* The “truth source” for managed metadata + Portable PDB.
* Feeds: your interpreter’s `IMetadataResolver` / `IMethodBodyProvider`, and also the decompiler (ILSpy) so you don’t end up doing lossy conversions.

---

### Option B: **AsmResolver**

**What it is:** A managed library for reading/modifying/reconstructing PE files and managed .NET metadata; MIT licensed. ([GitHub][4])

**Why you’d pick it**

* **Much higher-level object model** than SRM for many tasks (walking metadata, rewriting, etc.).
* Has explicit concepts for Portable PDB metadata streams (e.g., `PdbStream`) in its metadata model. ([Washi Docs][5])
* Has a dedicated package for “Windows PDB models” (`AsmResolver.Symbols.Pdb`). ([NuGet][6])
* The project explicitly discussed a split between Windows PDB (MSF) and Portable PDB support and the architectural differences. ([GitHub][7])

**Why you might not**

* **Decompilation ecosystem (ILSpy)** is SRM-centric; if AsmResolver becomes your “canonical metadata,” you’ll still likely load the module again via SRM for decompilation.
* Depending on your needs, you might end up maintaining two representations (AsmResolver object model + SRM readers), unless you standardize.

**Typical role**

* If you want a **friendlier metadata/PE abstraction** (especially if you ever want to patch/rebuild things).
* If you want **one library family** that also models Windows PDB structures (though you’ll still need to validate coverage vs DIA for edge cases).

---

### Option C: **dnlib**

**What it is:** Mature .NET assembly reader/writer library; MIT. ([NuGet][8])
It explicitly documents PDB support, including a **managed Windows PDB reader that supports all OSes**, while Windows PDB writing is Windows-only. ([GitHub][9])

**Why you’d pick it**

* Very pragmatic API for metadata + IL.
* Built-in story for Windows PDB reading without requiring DIA at runtime (per its docs). ([GitHub][9])

**Why you might not**

* Again: **ILSpy + SRM** is the dominant modern decompiler path.
* dnlib is excellent, but if you already commit to SRM for Portable PDB and ILSpy for decompilation, dnlib can become “third metadata stack.”

**Typical role**

* If you want **one library that “just handles” old Windows PDBs** in a managed way, dnlib is worth serious consideration. ([GitHub][9])

---

### Option D: **Mono.Cecil**

**What it is:** Classic library to inspect/modify/create .NET assemblies; MIT. ([GitHub][10])
NuGet description notes support for “some debugging symbol format.” ([NuGet][11])

**Why you’d pick it**

* Extremely widely used; lots of ecosystem knowledge.
* Pleasant model for many reflection-ish workflows.

**Why you might not**

* For **debugging-grade PDB + stepping maps + decompilation integration**, Cecil is usually not where modern tooling centers anymore (SRM + ILSpy is).
* If you need robust “debugger semantics” (scopes, async mapping, hidden sequence points correctness), you’ll likely end up leaning on SRM/DiaSymReader/ILSpy anyway.

**Typical role**

* Nice utility dependency, but I wouldn’t make it the core of a dump debugger pipeline.

---

## 2) PDB reading (Portable PDB vs Windows PDB)

### Key reality: two managed symbol worlds

.NET supports **Windows PDBs** and **Portable PDBs** for managed code. ([GitHub][12])
Portable PDBs are cross-platform and documented; Windows PDB tooling is historically Windows-centric. ([GitHub][13])

Also, Portable PDB isn’t just “a file format”: it’s literally **debugging metadata tables** (Document, MethodDebugInformation, LocalScope, plus custom debug info like EmbeddedSource and SourceLink). ([GitHub][2])

---

### Portable PDB: prefer **System.Reflection.Metadata**

The symreader-portable README explicitly recommends that *new* applications read Portable PDBs directly using `System.Reflection.Metadata` because it’s more efficient than DiaSymReader abstractions. ([GitHub][14])

**What you get (Portable PDB)**

* IL offset → source span mapping (sequence points)
* Local names/scopes, import scopes
* Async/iterator state machine info
* Custom debug info including **EmbeddedSource** and **SourceLink** ([GitHub][2])

---

### Windows PDB: pick one strategy and isolate it

Windows PDB is an MSF-based format; Microsoft has a repo documenting it. ([GitHub][15])
But for consumption, you have a few options:

1. **DIA (via Microsoft.DiaSymReader.Native)**
   `Microsoft.DiaSymReader.Native` is a native implementation package, with Windows-only platform support stated on NuGet. ([NuGet][16])
   This is the “most compatible” route on Windows, but not portable.

2. **dnlib managed Windows PDB reader**
   dnlib claims a managed Windows PDB reader that supports all OSes. ([GitHub][9])
   If you need cross-platform Windows-PDB reading, this is one of the few pragmatic OSS choices.

3. **AsmResolver.Symbols.Pdb**
   NuGet describes it as “Windows PDB models for the AsmResolver tool suite.” ([NuGet][6])
   It’s promising, but you’ll want to benchmark correctness/coverage for the specific debug info you need (locals/scopes/sequence points for managed code embedded in Windows PDBs, etc.).

**Architectural advice:** treat Windows PDB support as an **optional plugin** behind your own `ISymbolReader` interface. Windows PDB is the format most likely to force platform-specific or library-specific behavior.

---

## 3) Symbol and binary acquisition (when the dump doesn’t contain everything)

### The “acquisition” problem is big enough to deserve its own module

Your post-mortem debugger will constantly hit scenarios like:

* dump is minidump: no module bytes, only module identities
* module loaded from a path that doesn’t exist on analysis machine
* PDB isn’t next to the module (common)
* SourceLink points to remote git content (needs fetching/caching)

Good news: there’s a lot of OSS from the .NET diagnostics ecosystem.

### **dotnet-symbol** (tool) + **symstore / Microsoft.SymbolStore** (library code)

The `dotnet-symbol` tool (from dotnet/symstore) can download symbols/modules needed for debugging dumps (including PDBs and portable PDBs). ([GitHub][17])
The original `dotnet/symstore` repo is archived, and there’s an explicit continuation in `dotnet/diagnostics`. ([GitHub][18])

**What this implies for your design**

* You likely want a **SymbolStore-like client** internally:

  * understands symbol server layouts / caches
  * fetches PEs + PDBs by identity
  * supports Microsoft public symbol server and private servers
* Whether you literally reuse `Microsoft.SymbolStore` or reimplement the minimal subset, the important point is: **keep acquisition separate from parsing.**

**Input-shape caveat:** W1–W4 evidence covers only the named generated fixtures and explicitly admitted input shapes.
The local SRM opener's 512 MiB bound remains a deterministic evidence contract; other shapes require separate
executable evidence.

---

## 4) Source decompilation to C# (and debug mapping for stepping)

### The obvious choice: **ILSpy decompiler engine (ICSharpCode.Decompiler)**

ILSpy is MIT licensed. ([GitHub][19])
The `ICSharpCode.Decompiler` NuGet package is the engine used by ILSpy. ([NuGet][20])

**Why ILSpy is uniquely useful for your “dump stepping” feature**
It doesn’t just give you text — it has explicit APIs around debug mapping:

* `CSharpDecompiler` exposes `DebugInfoProvider` (hook PDB-derived info in) ([DNDOCS][21])
* It can **create sequence points** for a decompiled syntax tree (`CreateSequencePoints`) ([DNDOCS][21])
* It can compute **code mapping information** relating compiler-generated parts (lambdas, async/yield state machines `MoveNext`) back to user methods (`CodeMappingInfo`). ([DNDOCS][22])
* ILSpy maintainers explicitly describe their “C# with IL” approach as generating sequence points for decompiled code to map statements back to IL instructions. ([GitHub][23])

That last bit is *exactly* what you need for step-over/into/out in a **virtual interpreter** with decompiled fallback.

---

# The missing piece: a layer between ClrMD, PE/PDB readers, the decompiler, and your interpreter

Yes: you want a **dedicated “Artifacts & Debug Info” layer** between:

* **ClrMD** (runtime/dump inspection: modules/types/heap/stack/registers)
* **PE + metadata + PDB + SourceLink + decompiler**
* **Your IL interpretation framework** (method execution, unknown propagation, virtual heap)

This layer’s job is to unify identity, caching, and “best available information” selection.

---

## Proposed architecture: “Program Artifacts & Debug Map Service”

### Core design principles

1. **Stable internal representations** (don’t leak SRM vs AsmResolver vs dnlib types across your interpreter)
2. **Deterministic, layered identity** (dump metadata-root MVID/length/SHA-256; complete artifact whole-file length/SHA-256; PDB id/age). Metadata-root agreement does not imply full PE equality.
3. **Progressive enhancement**

   * best: real source + portable PDB
   * next: source via embedded source / SourceLink
   * next: decompiled C# + decompiler-generated sequence points
   * last: IL disassembly
4. **Cache everything** (module bytes, metadata readers, PDB readers, decompiled text, debug maps)

### Layer contract expansion: who owns ClrMD ↔ PE/PDB ↔ Interpreter interactions

To make the boundary operational (not just conceptual), define the service as five explicit layers with one-way dependencies:

1. **Runtime Snapshot Adapter (ClrMD-facing)**
2. **Artifact Acquisition Layer (PE/PDB discovery + identity validation)**
3. **Metadata & Symbol Projection Layer (reader-specific decoding)**
4. **Execution Binding Layer (interpreter-facing contracts)**
5. **Interpreter Core (execution semantics only)**

This keeps every “mixed concern” in exactly one place.

#### Layer 1: Runtime Snapshot Adapter (ClrMD-facing)

**Responsibility**

* Convert ClrMD runtime objects into stable IDs and runtime facts.
* Provide dump-backed memory and frame materialization.
* Never parse metadata tables or PDB records directly.

**Consumes**

* `DataTarget`, `ClrRuntime`, `ClrModule`, `ClrType`, `ClrMethod`, frame/heap primitives.

**Produces**

* `RuntimeMethodHandle`, `RuntimeTypeHandle`, `RuntimeModuleHandle`
* Raw frame values (`this`, args, locals) with confidence/provenance tags.
* Optional “runtime hint set” (IL RVA/size, native code ranges, module path hints).

**Why this matters**

If you allow ClrMD objects to flow upward, the interpreter eventually depends on dump APIs. Keeping this adapter strict preserves portability to non-dump scenarios (static binary analysis, replay sessions, synthetic tests).

#### Layer 2: Artifact Acquisition Layer (PE/PDB discovery + identity validation)

**Responsibility**

* Locate module/PDB bytes from dump, local disk, cache, or symbol server.
* Validate identities before handing streams to readers.
* Return immutable blobs/streams plus provenance.

**Consumes**

* Stable module/PDB identities from Layer 1 and user symbol-path policy.

**Produces**

* `ArtifactBlob` for PE/PDB + `ArtifactProvenance` (`DumpMapped`, `LocalPath`, `Cache`, `Server`).
* Validation report (`ExactMatch`, `WeakMatch`, `Mismatch`).

**Why this matters**

This is where correctness guardrails live. The interpreter should never execute IL from an unverified module silently.

#### Layer 3: Metadata & Symbol Projection Layer (reader-specific decoding)

**Responsibility**

* Decode method bodies, signatures, EH regions, locals, sequence points, and scope trees.
* Normalize active SRM output into project-owned records. An alternative backend must first reproduce the same
  projected-contract corpus; W3 does not carry simultaneous AsmResolver/dnlib implementations.
* Hide reader-specific quirks and represent missing features explicitly rather than silently falling back.

**Consumes**

* PE/PDB blobs from Layer 2.
* Optional runtime generic context hints from Layer 1 only after a generic scenario is admitted; W3 has none.

**Produces**

* `MethodBodyRecord`, `ResolvedToken`, `SymbolMap`, `DocumentMap`, `AsyncMap`.
* Decoder diagnostics for partial/unreadable records.

**Why this matters**

You can swap readers later (or run dual-read validation) without changing interpreter contracts.

#### Layer 4: Execution Binding Layer (interpreter-facing contracts)

**Responsibility**

* Join runtime facts (Layer 1) with decoded metadata/artifact facts (Layer 3).
* In W3, freeze one exact non-generic method/field execution view; resolve generic instantiation context for call sites
  only in a later admitted slice.
* Materialize interpreter start state and provide call/field/heap callbacks.

**Consumes**

* Runtime handles/facts, normalized metadata/symbol records, policy knobs.

**Produces**

* `InterpreterSessionContext` + `IBindingServices`:

  * `IMethodBodyProvider`
  * `ITokenResolver`
  * `ISymbolResolver`
  * `IRuntimeValueProvider`
  * `IHeapBridge`

**Why this matters**

This is the only layer allowed to “speak both languages” (runtime and metadata). It is the seam where provenance is combined and where fallback decisions become explicit stop reasons or unknowns.

#### Layer 5: Interpreter Core (execution semantics only)

**Responsibility**

* Execute IL using the binding contracts.
* Track domain values, effects, budgets, and determinism.
* Report precise requirements back to Layer 4 when information is missing.

**Consumes**

* Only project-owned interfaces, never ClrMD or metadata-reader types.

**Produces**

* Value results, effect summaries, stop reasons, and explainability traces.

---

### Cross-layer interaction scenarios (authoritative hand-off points)

#### Scenario A: Prepare and execute one rooted dump getter (implemented W3)

1. Layer 1 selects one exact strong-root object, runtime module, and direct runtime field descriptor.
2. The dump adapter reads the exact metadata root, MethodDef RVA, physical header, code, and declared extra sections
   through counted reads.
3. It returns a normalized body only when all required dump evidence is exact; any independently decoded PE body
   remains a comparison oracle.
4. Layer 4 projects the exact non-generic method shape and contextual FieldDef, proves the sole admitted
   `ldfld`/runtime-field correlation, and imports the exact four-byte field observation.
5. Layer 5 activates the explicit rooted receiver, freezes the typed whole-body plan, and executes the closed E2
   profile with deterministic outcome, budget, ordered events, and resulting memory. The host/test composition retains
   the cross-layer correlation provenance and fresh-session replay evidence around that execution.

#### Scenario B: Resolve a method from a dump frame (future)

1. Layer 1 maps `ClrStackFrame` + `ClrMethod` to runtime handles and attempts to recover `this`, arguments, and locals.
2. The same counted method-evidence boundary applies, but frame seeding, optimized-frame degradation, and generic
   context require a separately admitted product scenario before Layer 5 may execute it.

#### Scenario C: Missing PDB, decompilation fallback (future research)

1. Layer 2 fails PDB lookup, returns `ArtifactMissing(Pdb)`.
2. After a debug-map slice is admitted, Layer 3 may build “no source” markers and an explicitly lower-confidence
   decompiler mapping.
3. After a virtual-stepping controller is admitted, Layer 4 may classify that mapping as `DecompiledFallback`.
4. Layer 5 may continue with downgraded source confidence only after W4 method execution, deterministic pause/event,
   source-map/decompiler, and stepping gates have their own executable evidence. None is part of W3.

#### Scenario D: Identity mismatch between dump module and disk module

1. Layer 2 detects failed metadata-root identity verification or a changed complete-artifact identity.
2. The active dump-backed path blocks correlation; it does not substitute disk body facts or expose an executable mixed-source body.
3. A future separately classified artifact-only analysis mode may define a different policy, but it cannot relabel the artifact as dump evidence.

---

### Required error and provenance model between layers

Every layer boundary should return structured outcomes, not plain exceptions:

* `Success(value, provenance, diagnostics)`
* `Partial(value, gaps, provenance, diagnostics)`
* `Unavailable(reason, remediationHints)`
* `Conflict(expected, actual, policyDecision)`

That model prevents silent degradation and keeps interpreter behavior auditable when data quality is uneven (which is common for real-world dumps).

---

## Recommended internal interfaces (thin but powerful)

The sketches below describe a possible broader artifact/symbol platform. They are not the implemented W3 public API.
W3 uses narrower project-owned structural resolution contracts and no symbol, decompiler, acquisition, or generic
service.

### 1) Acquisition & identity

```csharp
public sealed record ModuleIdentity(
    Guid Mvid,
    string? SimpleName,
    string? FilePathHint,
    byte[]? BuildId /* ELF/MachO */,
    (uint TimeDateStamp, uint ImageSize)? PeStamp);

public sealed record PdbIdentity(Guid Guid, int Age);

public interface IArtifactLocator
{
    ValueTask<Stream?> TryOpenModuleAsync(ModuleIdentity id, CancellationToken ct);
    ValueTask<Stream?> TryOpenPdbAsync(ModuleIdentity module, PdbIdentity pdb, CancellationToken ct);
}
```

**Notes**

* `IArtifactLocator` is where SymbolStore / dotnet-symbol-like logic lives. ([GitHub][17])
* It should support:

  * “from dump memory if present”
  * “from local file path”
  * “from symbol cache/server”
  * “from user-provided artifacts directory”

---

### 2) Metadata and IL bodies (canonical “method truth”)

```csharp
public interface IManagedMetadata
{
    ModuleIdentity Identity { get; }

    // Token/handle resolution (type/method/field)
    MethodKey ResolveMethod(int metadataToken);
    TypeKey ResolveType(int metadataToken);

    // IL body
    MethodBodyInfo GetMethodBody(MethodKey method);
}
```

Implementations:

* `SrmManagedMetadata` (SRM + PEReader) is the active direction; W3's narrower SRM projection is implemented.
* `AsmResolverManagedMetadata` or `DnlibManagedMetadata` remains hypothetical unless an executable active-slice gap
  triggers a same-contract comparison.

**Why this interface exists**

* Your IL interpreter should **not care** which library provided the metadata; it cares that it can resolve tokens and get a method body.

---

### 3) Symbols and source (PDB-derived first)

```csharp
public interface ISymbolInfo
{
    PdbContentIdentity ContentIdentity { get; }
    IReadOnlyList<SequencePointSpan> GetSequencePoints(MethodKey method);
    LocalScopeTree GetLocalScopes(MethodKey method);
    BindingImportContext GetBindingImports(MethodKey method, InstructionLocation location);
    SourceDocumentInfo GetDocument(DocumentId doc);
    SourceLinkInfo? TryGetSourceLink();     // from Portable PDB custom debug info
    EmbeddedSourceInfo? TryGetEmbeddedSource(DocumentId doc);
}
```

Portable PDB gives you these concepts structurally (documents, method debug info, locals/scopes, embedded source, SourceLink). ([GitHub][2])
SRM is the recommended reader for portable PDB in new tools. ([GitHub][14])

The completed [Post-W6 Path Forward](../../plans/post-w6-path-forward.md) makes binding imports the first implemented W7
symbol use. The adapter validates bounded PDB bytes against the module's exact debug identity and projects nested
`LocalScope`/`ImportScope` records into project-owned namespace/type/alias facts. A missing instruction location may be
used only when all candidate method scopes yield the same effective imports. Decompiled source is never an import-
binding fallback, and unavailable symbol context never blocks a non-ambiguous fully qualified static expression.

Windows PDB support sits behind the same interface (if present), but as a plugin. ([GitHub][13])

---

### 4) Decompiled source fallback (with debug map)

```csharp
public interface IDecompilerService
{
    DecompiledMethod DecompileMethod(MethodKey method, DecompilerOptions opts);
}

public sealed record DecompiledMethod(
    string CSharpText,
    IReadOnlyList<DecompiledSequencePoint> SequencePoints,
    CodeMappingInfo? CodeMapping);
```

Implementation: ILSpy `ICSharpCode.Decompiler`:

* can generate sequence points for the decompiled syntax tree ([DNDOCS][21])
* can compute `CodeMappingInfo` to relate compiler-generated parts (lambdas, async MoveNext) back to user code ([DNDOCS][22])
* can accept a debug info provider to improve results when PDB exists ([DNDOCS][21])

This gives you a **consistent stepping substrate** even when real source is absent. ([GitHub][23])

---

## “Glue” service: a single place that decides what to use

Create a high-level façade:

```csharp
public interface IProgramArtifacts
{
    IManagedMetadata GetMetadata(ModuleIdentity module);
    ISymbolInfo? TryGetSymbols(ModuleIdentity module);
    IDecompilerService Decompiler { get; }

    DebugMap GetBestDebugMap(MethodKey method); // PDB if available else decompiler else IL
    SourceText GetBestSource(MethodKey method); // real source if available else decompiled
}
```

This is a prospective layer the **post-mortem debugger UI** and a broader **IL interpreter host** could call after
artifact and symbol scenarios are admitted. It is not part of the W3 proof.

---

# How this connects to ClrMD + the IL interpretation framework

ClrMD gives you runtime facts (loaded modules, method tables, object addresses, thread stacks) but it doesn’t *guarantee* you have all method-body pages, a disk module, PDB, or source. The active dump path does not hide those gaps with artifact bytes. The broader artifact integration looks like:

1. **ClrMD identifies** the runtime module/method
2. Your **Artifacts layer resolves** it to a stable `ModuleIdentity` + `MethodKey`
3. Artifacts layer **acquires** PE/PDB if explicitly authorized for symbols, static-artifact analysis, or an independent oracle (symbol cache/server)
4. Artifacts layer provides:

   * artifact IL body + metadata resolution → to explicitly artifact-backed/static consumers, not into an incomplete dump body
   * debug map + source/decompiled text → to the UI stepping experience

### Practical connection points

* **ClrMD → ModuleIdentity**

  * get module name/path hint
  * read module metadata in memory (if available) to obtain MVID
  * record PE timestamp/size for symbol server lookup when relevant

* **ModuleIdentity → PE + PDB**

  * `IArtifactLocator` uses local paths + symbol servers
  * optionally reuse logic from symstore/dotnet-symbol ecosystem ([GitHub][17])

* **PE + PDB → DebugMap**

  * Portable PDB: SRM reads sequence points/scopes; PDB also carries SourceLink/EmbeddedSource custom debug info ([GitHub][2])
  * No PDB: ILSpy decompile method + generate sequence points for decompiled code ([DNDOCS][21])

* **DebugMap + IL interpreter**

  * Interpreter’s “instruction pointer” is IL offset within a method body
  * DebugMap converts IL offset → (document, line/col span) or (decompiled text span)
  * Step operations use statement boundaries derived from the best available map

---

# Concrete recommendation (if you want the least-friction path)

Item 1's closed metadata/IL subset and W7's narrow Portable-PDB identity, scope, and import projection are implemented
at current state. W7 validates bounded artifact bytes against the module debug identity and projects the active nested
scope/import facts for one selected frame. Decompiler, broader acquisition, Windows-PDB, and optional-plugin items
remain evidence-gated product/research work.

If I had to pick a default stack that minimizes impedance mismatch:

1. **System.Reflection.Metadata / PEReader** as your canonical metadata + Portable PDB reader ([GitHub][14])
2. **ILSpy’s ICSharpCode.Decompiler** for decompilation + decompiled debug maps ([NuGet][20])
3. A dedicated **Artifacts layer** that:

   * normalizes module/method identity
   * handles symbol acquisition (SymbolStore/dotnet-symbol style)
   * exposes `GetBestDebugMap()` and `GetBestSource()` to the rest of the system ([GitHub][17])
4. Optional plugins:

   * **dnlib** (or AsmResolver) if you need cross-platform **Windows PDB** reading ([GitHub][9])
   * **Microsoft.DiaSymReader.Native** if you decide Windows-only is acceptable for Windows PDBs ([NuGet][16])

This keeps the “mainline” (portable PDB + decompiler) clean and cross-platform, and isolates legacy PDB complexity behind a plugin boundary.

[1]: https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata?view=net-10.0&utm_source=chatgpt.com "System.Reflection.Metadata Namespace"
[2]: https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/specs/PortablePdb-Metadata.md "raw.githubusercontent.com"
[3]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs "raw.githubusercontent.com"
[4]: https://github.com/Washi1337/AsmResolver?utm_source=chatgpt.com "Washi1337/AsmResolver: A library for creating, reading ..."
[5]: https://docs.washi.dev/asmresolver/api/pe/AsmResolver.PE.DotNet.Metadata.html?utm_source=chatgpt.com "Namespace AsmResolver.PE.DotNet.Metadata | AsmResolver - Washi"
[6]: https://www.nuget.org/packages/AsmResolver.Symbols.Pdb/?utm_source=chatgpt.com "AsmResolver.Symbols.Pdb 5.5.1"
[7]: https://github.com/Washi1337/AsmResolver/issues/297?utm_source=chatgpt.com "AsmResolver.Symbols · Issue #297 · Washi1337/ ..."
[8]: https://www.nuget.org/packages/dnlib?utm_source=chatgpt.com "dnlib 4.5.0 - Reads and writes .NET assemblies"
[9]: https://github.com/0xd4d/dnlib?utm_source=chatgpt.com "0xd4d/dnlib: Reads and writes .NET assemblies and ..."
[10]: https://github.com/jbevain/cecil?utm_source=chatgpt.com "jbevain/cecil: Cecil is a library to inspect, modify and create ..."
[11]: https://www.nuget.org/packages/mono.cecil/?utm_source=chatgpt.com "Mono.Cecil 0.11.6"
[12]: https://raw.githubusercontent.com/dotnet/designs/main/accepted/2020/diagnostics/debugging-with-symbols-and-sources.md "raw.githubusercontent.com"
[13]: https://raw.githubusercontent.com/dotnet/designs/main/accepted/2020/diagnostics/portable-pdb.md "raw.githubusercontent.com"
[14]: https://github.com/dotnet/symreader-portable?utm_source=chatgpt.com "dotnet/symreader-portable"
[15]: https://github.com/microsoft/microsoft-pdb?utm_source=chatgpt.com "Information from Microsoft about the PDB format. We'll try to ..."
[16]: https://www.nuget.org/packages/Microsoft.DiaSymReader.Native/?utm_source=chatgpt.com "Microsoft.DiaSymReader.Native 1.7.0"
[17]: https://raw.githubusercontent.com/dotnet/symstore/main/src/dotnet-symbol/README.md "raw.githubusercontent.com"
[18]: https://github.com/dotnet/symstore?utm_source=chatgpt.com "dotnet/symstore: Implements API for retrieval of symbols ..."
[19]: https://github.com/icsharpcode/ILSpy?utm_source=chatgpt.com "icsharpcode/ILSpy: .NET Decompiler with support for PDB ..."
[20]: https://www.nuget.org/packages/icsharpcode.decompiler/?utm_source=chatgpt.com "ICSharpCode.Decompiler 9.1.0.7988"
[21]: https://docs.dndocs.com/n/ICSharpCode.Decompiler/8.2.0.7535/api/ICSharpCode.Decompiler.CSharp.CSharpDecompiler.html "Class CSharpDecompiler
 \| ICSharpCode.Decompiler 8.2.0.7535 | DNDocs "
[22]: https://docs.dndocs.com/n/ICSharpCode.Decompiler/8.2.0.7535/api/ICSharpCode.Decompiler.Metadata.CodeMappingInfo.html "Class CodeMappingInfo
 \| ICSharpCode.Decompiler 8.2.0.7535 | DNDocs "
[23]: https://github.com/icsharpcode/ILSpy/discussions/2226?utm_source=chatgpt.com "Is there a way to decompile a single function/method call?"
