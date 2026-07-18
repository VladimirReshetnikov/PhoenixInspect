# Integration proposal: Dump evaluator ↔ ClrMD ↔ SRM/PE/PDB

> **Lifecycle:** Current · **Roadmap:** Active · **Last reconciled:** 2026-07-18

## Implemented W3 boundary (2026-07-14)

Exact strengthened implementation checkpoint `19c292f9f` turns the central binding seam into executable evidence for one closed
milestone-selected profile:

- project-owned structural module, type, MethodDef, and FieldDef identities isolate the interpreter from ClrMD and
  SRM object identity;
- SRM projects a method's body, signature, return shape, and local vector atomically from one metadata image, and
  resolves the admitted `ldfld` in that frozen method context;
- the dump host reparses the counted physical body, proves that its sole `ldfld` operand is the correlated runtime
  `Int32` field, imports only an exact four-byte observation into persistent memory, and never gives the machine a live
  `ClrmdDumpSession`; and
- reopen/rebind replay reconstructs the same prepared-memory transcript, while a disk PE remains a late independent
  CoreCLR/equality oracle rather than resolver input.

Local headless verification passed a zero-warning 15-project Release build, 103 milestone-selected unit tests, 67 fast
integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3 lane, and both
documentation guards. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four required jobs at
the same exact pushed implementation checkpoint. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237). W3 is complete for its defined
milestone-selected scope.

This checkpoint does not implement product method evaluation, calls, branches, broader opcode families, generic
context reconstruction, Portable PDB projection, a second meaningful value domain, or validation beyond the named
fixture shapes. The completed [Post-W6 Path Forward](../../plans/post-w6-path-forward.md) later implements W7's
selected-frame/Portable-PDB import projection for static-expression binding; that work does not alter W3's
implemented boundary.

## Closed W4/W7 additions, completed W8.1 disposition, and active W8.2 boundary (2026-07-18)

Later closed milestones extend the host without rewriting the historical W3 checkpoint. W4.2 implements the second
meaningful, provenance-aware value domain; W4.3–W4.9 carry typed non-exact field evidence through the execution kernel
and add the detached ClrMD evidence producer and dump-grounded product composition. W7 implements one selected-frame
and identity-validated Portable-PDB LocalScope/ImportScope projection, then binds and reads the admitted non-generic
`StaticFieldExpressionV1` shapes. A non-ambiguous fully qualified ordinary static field remains independent of frame
and PDB evidence. These are implemented capabilities, not future services.

The [Post-W7 Path Forward](../../plans/post-w7-path-forward.md) governs W8. W8.1 completed at `220be94b4`; the
[`W8.1 Physical-Truth Disposition`](../../plans/w8-1-physical-truth-disposition.md) freezes the exact checkpoint ledger
and branch table. The emitted nested/generic metadata and TypeSpecs, candidate-keyed ordered closed arguments,
distinct per-construction slots/values, and close/reopen replay are proved. Display-derived runtime type names remain
probe evidence only and cannot select a construction. Class, value-type, and interface definition kinds all have an
exact disposition. W8.2 is the active product-contract checkpoint.

W8 also keeps value sources separate. A metadata literal performs no runtime, slot, or memory call. An ordinary stored
static follows exact construction, declaring field, application domain, slot, and counted raw-memory evidence.
Thread-relative and RVA-backed storage are admitted. Context-relative storage is non-admitted because no attributable
context identity exists; W8 creates no corresponding strategy or API. Exact memory-homed `this`, parameters, and live
locals are admitted through mandatory separate `FrameValueExpressionV1` contracts after exact
name/scope/liveness/location/type proof. Register homes are unproved and excluded. Selected-frame generic
substitution is non-admitted because the available legacy, CDAC, and DAC/DBI routes do not provide exact arguments.

Every repository-invoked W8 managed command runs through the headless wrapper, and every generated target, helper, or
consumer child process is configured as hidden and windowless. Fast compiler/SRM/PDB differentials precede full-dump
generated conformance. The decision gate uses thirty-two fixed core independent dumps over four materially distinct
application shapes plus thread-relative, RVA-backed, and frame-value incidents: 35 independent incidents minimum.

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
|  - Frozen method/field projections and W7 static binding      |
|  - W7 selected-frame/Portable-PDB import context              |
|  - Exact dump evidence import into persistent memory          |
|  - W8 constructed-owner and admitted frame/storage contracts  |
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

* The interpreter core executes only frozen project-owned plans through project-owned resolution and memory
  interfaces. Dump-free differentials are projected from a content-identified PE by SRM; dump-backed plans are
  prepared from counted metadata/body/owner/field evidence and detached memory. The machine has no live ClrMD backend.
* W4.2 implements the provenance-aware second domain, and W4.9 composes it with the detached dump producer. Neither
  permits a disk artifact to fill a missing dump body or value.
* W7's product path adds counted ordinary static-field reads plus optional selected-frame/PDB binding context outside
  the core machine. W8.2 must add constructed-owner, thread-relative, RVA-backed, and exact memory-homed frame-value
  routes through new frozen contracts, never by widening a V1 artifact implicitly. Context-relative identity,
  register homes, and selected-frame generic substitution have no route.

---

## 3) Core abstraction: stable IDs + resolvers

### 3.1 Stable identity types (don’t leak ClrMD/AsmResolver objects)

Define internal identifiers that are cheap and comparable:

* Dump metadata-root identity: **MVID** plus exact metadata-image length and SHA-256.
* Complete disk-artifact identity: exact whole-file length plus SHA-256, carried in addition to metadata identity and optional PE timestamp/image size. A path is a location hint, never identity.
* `MethodId`: `(ModuleId, MethodDefToken)` in W3/W4. `MemberRef`/`MethodSpec` and method-generic context remain separate
  structural extensions, not aliases for an admitted MethodDef.
* W7's non-generic static identity retains module, declaring TypeDef, and FieldDef. W8 V2 must add a canonical closed
  construction with nested segment groups and ordered flattened arguments; `(ModuleId, TypeDefToken)` alone is not a
  runtime construction identity.
* TypeDef/TypeRef identities and bounded TypeSpec signatures remain token/signature based. Display strings are never
  identity or tie breakers.

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

All dump reads are bounded and return exact byte counts. Sparse pages, invalid addresses, inconsistent runtime structures, and policy limits are ordinary typed evidence outcomes. Dumps are rejected above 8 GiB, and ClrMD's dump cache is capped at 256 MiB with stack-trace/root caching disabled; the typed PE `Open` boundary rejects artifacts above 512 MiB. Caveat: these controls and outcomes are validated only for the named generated fixtures and explicitly admitted input shapes.

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

* decoding the closed W3/W4 method/local/field signature vocabulary;
* resolving MethodDef, TypeDef/TypeRef/TypeSpec, and FieldDef identities plus their declaring types;
* mapping tokens and W7 Portable-PDB scopes/imports into structural project-owned records; and
* under the active W8 gates, decoding closed generic construction/substitution/constraints, nested ownership, literal
  constants, extern-alias AssemblyRef correlation, and the exact Portable-PDB facts required by V2. MemberRef/
  MethodSpec execution and richer custom-debug records remain independently admitted capabilities.

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

## 6) Symbols/PDB integration: keep it as a separate capability

ClrMD’s modern file-location API (`IFileLocator`) is about locating **images**, not PDBs. ([GitHub][9])
The repository therefore keeps symbol projection as a separately invoked service. W7 implements its first bounded
Portable-PDB use; absence remains a typed context outcome and never blocks an equivalent fully qualified static route:

### 6.1 Interfaces

* `ISymbolResolver`:

  * `GetLocals(MethodId) -> IReadOnlyList<LocalSymbol>`
  * `GetSequencePoints(MethodId) -> IReadOnlyList<SeqPoint>`
  * `GetScopes(MethodId) -> ScopeTree`
  * `GetImports(MethodId, InstructionLocation) -> BindingImportContext`

### 6.2 Backends

* Portable PDB:

  * W7 uses SRM to validate bounded candidate bytes against exact module debug identity and to project the active
    LocalScope/ImportScope chain for one selected frame. W8.2 extends the predeclared scoped import, alias, extern, and
    lexical-catalog routes plus the mandatory exact memory-homed frame-value route. It adds no context-relative,
    register-home, or selected-frame generic route.
* Windows PDB:

  * Defer backend selection until a Windows-PDB fixture becomes an active requirement; DIA, DiaSymReader, dnlib, and AsmResolver notes remain research inputs rather than dependencies.

### 6.3 Locating the PDB

An artifact resolver may search configured locations and symbol stores, but the product binding layer should:

1. Parse the PE debug directory to obtain PDB identity (GUID/age, path).
2. Resolve bounded candidate bytes from:

   * “same directory as PE”
   * user-configured symbol caches
   * symbol server (SSQP conventions)
3. Accept a candidate only when its Portable PDB identity exactly matches the module debug identity; retain a bounded
   content hash and treat the path as a display hint.

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

W8 must resolve admitted static-owner tokens under an exact **generic context**:

* `GenericContext` = (declaring type instantiation args, method instantiation args)
* Tokens might resolve to TypeSpec/MethodSpec which embed signatures containing generic variables.

The metadata construction and substitution resolver remains SRM-based. Runtime construction mapping is a separate
host operation: W8.1 proved an exact ordered argument source keyed to the candidate runtime type, with absent,
duplicate, partial, or conflicting constructions rejected without display-name fallback. Selected-frame method-generic
context is non-admitted and supplies no substitution API. Future execution scenarios may separately ask ClrMD for
runtime handles to:

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

### Staged Flow A: evaluate an expression with selected-frame evidence

1. The user selects a thread and frame. W7 already correlates that selection to one managed method/instruction location
   for PDB import binding; it does not recover stack values.
2. W8.1 proves that the ClrMD adapter can provide:

   * exact live memory locations for `this`, arguments, and locals; and
   * source, width, liveness, and location provenance for every retained value.
   Register homes and selected-frame method-generic construction are non-admitted.
3. Binding layer:

   * maps `ClrMethod` → `MethodId`
   * resolves a dump-backed method body from counted dump metadata/header/code/extra-section reads; incomplete evidence blocks that path rather than silently substituting disk bytes
   * may resolve an independently identified PE through SRM/PEReader for symbols, static-artifact workflows, or comparison, with source provenance kept distinct ([GitHub][2])
   * uses W7's identity-validated Portable-PDB import context independently from any W8 frame-value result
4. The mandatory separate W8 `FrameValueExpressionV1` route may seed a root only from admitted evidence:

   * address-backed values use counted dump-memory reads;
   * register-backed values are excluded;
   * exact name/scope/liveness/location/type is mandatory, and missing or duplicate evidence stops without static-field
     fallback; and
   * the direct result or unchanged W2/W6 suffix runs only from the frozen root descriptor.

### Future Flow B: “evaluate property getter on arbitrary heap object”

1. ClrMD identifies object address + runtime type.
2. Binding layer:

   * resolves target method (getter MethodDef/MemberRef)
   * resolves IL + metadata
3. Interpreter executes; any “world interaction” APIs:

   * are modeled as “unknown / effectful” (your earlier design)
   * may return unknown values and record side-effect traces

Arbitrary-object getter execution in Flow B is not a current product capability. Direct calls, deterministic
unknown-aware propagation, detached dump composition, selected-frame/PDB import context, and certified body-free W6
property projection are implemented in their closed scopes. Broader arbitrary dispatch/effects remain separate gates.
W8.1 admits exact memory-homed frame values and rejects selected-frame generic recovery; W8.2 must reflect both results
without a fallback route.

---

## 9) Why SRM/PEReader is the active match

Not marketing — just architectural fit:

* You need a **real** PE/metadata reader because ClrMD intentionally stopped being one. ([GitHub][3])
* SRM/PEReader covers the active W3 requirements:

  * PE/module identity and method bodies;
  * the closed method/local/field signature and token projection.

The same library reads Portable PDB metadata. W3 contains no symbol fixture; W7 supplies the first implemented one:
selected-frame/MethodDef/instruction evidence plus LocalScope/ImportScope projection for current namespace, namespace
imports, type/namespace aliases, and retained TypeSpec/`using static`/extern facts. W7 admits only its V1 subset. W8 must
prove exact lexical scope precedence, TypeSpec construction identity, extern-alias AssemblyRef correlation, and lexical
blocker completeness before those retained facts can drive V2. Missing or mismatched symbol evidence remains typed,
and fully qualified static lookup never depends on it.

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

* **ClrMD plus counted raw reads** is the active dump truth: heap objects, field layout, metadata-root identity, complete
  captured method bodies, W7 ordinary static slots, and selected-frame correlation. `GetILInfo()` remains a useful
  library capability, but it is not an input to the active body decoder. W8.1 proves exact per-construction runtime
  identity, thread-relative and RVA-backed storage, and exact memory-homed frame values; it excludes context-relative
  identity and register homes. ([GitHub][1])
* **SRM/PEReader** is the active metadata decoder over exact counted dump metadata and over independently identified
  disk artifacts. W3/W4 use it for closed structural method/signature/local/FieldDef projections; the disk PE remains
  an oracle. W7 implements the first bounded Portable-PDB identity/scope/import projection and static metadata binder.
  W8.2 extends generic/nested/TypeSpec/literal/scoped binding from the completed physical dispositions.
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
