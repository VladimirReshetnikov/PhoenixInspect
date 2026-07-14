> **Roadmap status: research backlog.** Debug maps become active only when an admitted interpreted-method or stepping slice needs source boundaries. PDB-to-decompiler-to-IL fallback is a design hypothesis, not implemented capability.

Below is a deeper technical write-up for:

1. a **`DebugMap` data model** that can drive stepping/highlighting regardless of where the “source” comes from, and
2. an **ILSpy (ICSharpCode.Decompiler) integration pattern** that produces **decompiled C# text + IL↔statement mapping** (sequence points) that your IL interpreter can step through.

---

# 1) `DebugMap`: a unified model for stepping over real or decompiled source

## 1.1 What `DebugMap` must support (practically)

Your virtual stepper and expression evaluator need three things, and they must work with either:

* Portable PDB + real source (best case),
* no PDB but **decompiled source** (fallback),
* worst case: IL view only.

### Required queries

A `DebugMap` needs to answer:

1. **Where am I?**
   `(method, ilOffset) -> source span (doc, line/col) + “statement id”`

2. **Where do I stop next?**
   Given current `(method, ilOffset)` and a stepping mode (into/over/out), what IL offset(s) represent the next stoppable boundary?

3. **Where does this source span come from?** (for “Run to cursor”, breakpoints, etc.)
   `(doc, span) -> candidate IL offsets` (often multiple, return the best)

The “stoppable boundary” concept is crucial: you’ll likely have many IL instructions per statement.

---

## 1.2 Normalize everything to “sequence points + grouping”

Portable PDB already gives you sequence points. ILSpy can generate sequence points for decompiled code. And even in “IL view only”, you can synthesize sequence points (basic blocks or instruction offsets).

So the best approach is:

* Normalize all maps to a set of **sequence points**:

  * IL start offset
  * IL end offset (or inferred end)
  * document identity (or a synthetic “decompiled://…” doc)
  * start/end line+column
  * hidden/non-user markers

Then build “statements” by grouping sequence points in a deterministic way.

### Why we care about `EndOffset`

ILSpy’s `SequencePoint` has `Offset` and `EndOffset`, and explicitly notes that `EndOffset` is used internally for “hidden sequence points for IL fragments not covered by any sequence point.” ([GitHub][1])

Portable PDB sequence points don’t store `EndOffset` explicitly; you often infer it from the next point. Your internal model should always have an IL range.

---

## 1.3 Data model proposal

### Method identity (input key)

Your map is per method body.

```csharp
public readonly record struct MethodKey(
    ModuleKey Module,          // stable module identity: MVID + etc.
    int MethodDefToken,        // metadata token for MethodDef
    GenericContextKey Gc,      // optional: for UI display; IL offsets are per generic body
    MethodBodyVariant Variant  // IL vs ReadyToRun, etc. (optional)
);
```

You’ll get `MethodDefToken` either from:

* ClrMD’s `ClrMethod.MetadataToken` / `ClrType.MetadataToken`, or
* metadata lookup (MethodSpec → MethodDef) in the artifacts layer.

### Documents

```csharp
public sealed record DebugDocument(
    DebugDocumentId Id,
    string UrlOrPath,          // PDB DocumentUrl or “decompiled://{mvid}/{token}”
    DebugDocumentKind Kind,    // RealFile | EmbeddedSource | SourceLink | Decompiled | IL
    byte[]? ContentHash        // optional, for integrity
);
```

For ILSpy sequence points, `SequencePoint.DocumentUrl` exists. ([GitHub][1])
You can set that to your synthetic decompiled doc URL.

### Sequence points (normalized form)

```csharp
public sealed record DebugSequencePoint(
    int IlStart,               // inclusive
    int IlEnd,                 // exclusive
    DebugDocumentId Document,
    int StartLine, int StartCol,
    int EndLine, int EndCol,
    bool IsHidden,
    DebugPointKind Kind        // UserCode | CompilerGenerated | Synthetic
);
```

Hidden points: ILSpy uses the standard “hidden” sentinel `0xFEEFEE` (`IsHidden` checks StartLine). ([GitHub][1])
Portable PDB uses the same sentinel convention.

### Statements (what stepping targets)

Statements are *groups* of sequence points (typically 1:1, but grouping helps).

```csharp
public readonly record struct StatementId(int Value);

public sealed record DebugStatement(
    StatementId Id,
    DebugDocumentId Document,
    SourceSpan Span,                // [start..end] in doc
    ImmutableArray<IlRange> Ranges, // one statement may cover multiple IL fragments
    bool IsHidden,
    StatementKind Kind              // Normal | Entry | Exit | HiddenGap
);

public readonly record struct IlRange(int Start, int End); // [Start, End)
public readonly record struct SourceSpan(int sl, int sc, int el, int ec);
```

### DebugMap (per method)

```csharp
public sealed class DebugMap
{
    public MethodKey Method { get; }
    public ImmutableArray<DebugDocument> Documents { get; }
    public ImmutableArray<DebugStatement> Statements { get; }

    // Core queries:
    public StatementId GetStatementAtIlOffset(int ilOffset);
    public SourceLocation? TryGetLocation(int ilOffset);
    public int? TryGetNextStatementOffset(int ilOffset); // for basic stepping
    public ImmutableArray<int> FindIlOffsets(SourceQuery query);
}
```

---

## 1.4 Invariants that make stepping sane

If you want Step Over/Into/Out to be stable, enforce:

1. **Statement ordering is monotonic by IL**
   Sort statements by `min(Ranges.Start)`.

2. **Statements cover the method body’s IL range**
   If the upstream source (PDB or decompiler) has gaps, synthesize “hidden gap” statements (Kind = `HiddenGap`) using inferred IL spans. This matches the reason ILSpy has `EndOffset`: it needs to cover uncovered fragments with hidden points. ([GitHub][1])

3. **Method entry/exit sentinels exist**
   Real compilers tend to have sequence points at braces, but generated maps can miss them. ILSpy even has a long-standing request to add brace sequence points to improve debugging behavior. ([GitHub][2])
   For a virtual stepper, you can simply add:

* `Entry`: `il=0` mapped to `{`
* `Exit`: last reachable IL offset mapped to `}`
  …in the decompiled document (or IL doc) if missing.

This prevents “Step Into lands in the wrong statement” and gives consistent “top-of-method” highlighting.

---

## 1.5 Building a DebugMap from **Portable PDB**

(Just enough detail to define the map; not going deep into PDB acquisition/caching.)

Pipeline:

1. Read method’s **sequence points** from Portable PDB (SRM).
2. Convert each to `DebugSequencePoint`.
3. Compute `IlEnd`:

   * for each point `i`, `IlEnd = next.IlStart`
   * last point: `IlEnd = methodBodyLength` (or last instruction end)
4. Detect IL gaps not covered by any point:

   * create `HiddenGap` statements for uncovered ranges
   * mark hidden using the hidden sentinel convention (or `IsHidden=true`)
5. Group into `DebugStatement`s:

   * simplest: one statement per sequence point
   * optional: group adjacent points with identical source span

**Important:** you should treat “hidden” points (0xFEEFEE) as non-user-stoppable unless your UI explicitly wants to show compiler-generated stepping.

---

## 1.6 Building a DebugMap from **decompiled** source

This is where ILSpy comes in. You’ll get:

* decompiled text
* a set of sequence points mapping IL offsets ↔ text locations

ILSpy’s `CreateSequencePoints(SyntaxTree)` produces a `Dictionary<ILFunction, List<SequencePoint>>` ([DNDOCS][3]), and ILSpy’s `SequencePoint` contains:

* `Offset`, `EndOffset`
* `StartLine/StartColumn/EndLine/EndColumn`
* `DocumentUrl`
* `IsHidden` ([GitHub][1])

That’s basically your normalized `DebugSequencePoint` already.

---

## 1.7 Async/lambda/state-machine mapping support (`CodeMappingInfo`)

Stepping through dumps often lands you in:

* lambda bodies
* `MoveNext()` of async/yield state machines

ILSpy has `CodeMappingInfo` specifically for this:

* it maps “parts” (lambda bodies, MoveNext) back to “parent methods” ([GitHub][4])
  and `CSharpDecompiler.GetCodeMappingInfo(PEFile, EntityHandle)` exposes it ([DNDOCS][3]).

How it fits `DebugMap`:

* `DebugMap` remains per method body.
* But you attach optional `MethodPresentationInfo`:

  * “this method is a part of parent method X”
  * for UI: show parent method header, allow “jump to parent”
  * for logical stepping: you can optionally remap the call stack frame label

This doesn’t change stepping correctness, but it dramatically improves UX.

---

# 3) ILSpy integration: producing decompiled C# + sequence points correctly

This section is the “how to wire it without guessing.”

## 3.1 The critical gotcha: AST nodes must have locations

ILSpy’s docs for `CreateSequencePoints` explicitly warn:

> “This only works correctly when the nodes in the syntax tree have line/column information.” ([DNDOCS][3])

ILSpy itself solves this by wrapping the token writer:

```csharp
tokenWriter = TokenWriter.WrapInWriterThatSetsLocationsInAST(tokenWriter);
syntaxTree.AcceptVisitor(new CSharpOutputVisitor(tokenWriter, formattingOptions));
```

This pattern is visible in ILSpy’s own “IL with C#” mixed language implementation. ([GitHub][5])

So your decompiler integration should follow that exact structure.

---

## 3.2 High-level flow

Given a module (PE) and a `MethodDefinitionHandle`:

1. Create `PEFile`
2. Create `IAssemblyResolver` (either ILSpy’s `UniversalAssemblyResolver` or your own)
3. Create `CSharpDecompiler`
4. Decompile the method into a `SyntaxTree`
5. Emit C# text **using a TokenWriter that sets AST locations**
6. Generate sequence points from that syntax tree
7. Pick the `ILFunction` entry corresponding to the method
8. Convert ILSpy `SequencePoint`s → your `DebugSequencePoint`s
9. Emit `DebugMap` + source text

This is precisely what ILSpy does to support its “C# with IL” view (and by extension, the stepping maps you want). ([GitHub][6])

---

## 3.3 Concrete ILSpy wiring (pseudo-code, close to real code)

```csharp
DecompiledResult DecompileMethodWithMap(
    Stream peStream,
    string moduleFileNameHint,
    MethodDefinitionHandle methodHandle,
    IArtifactLocator locator,
    DecompilerSettings settings,
    CancellationToken ct)
{
    // 1) PEFile (ILSpy metadata facade)
    var peFile = new PEFile(moduleFileNameHint, peStream);

    // 2) Assembly resolver
    // Option A: UniversalAssemblyResolver + search dirs
    var resolver = new UniversalAssemblyResolver(
        mainAssemblyFileName: moduleFileNameHint,
        throwOnError: false,
        targetFramework: DetectTfm(peFile)); // heuristics

    // Option B: custom resolver that calls locator.TryOpenModuleAsync(...)
    // and returns PEFile instances. (More work, more control.)

    // 3) Decompiler
    var decompiler = new CSharpDecompiler(peFile, resolver, settings)
    {
        CancellationToken = ct,
        // DebugInfoProvider = ... (optional: feed PDB-derived debug info)
    };

    // 4) Decompile a single member
    SyntaxTree tree = decompiler.Decompile(methodHandle); // via EntityHandle overloads :contentReference[oaicite:12]{index=12}

    // 5) Write code and assign node locations
    var sw = new StringWriter();
    WriteCodeAssigningAstLocations(sw, settings, tree); // see below

    string csharpText = sw.ToString();

    // 6) Create sequence points (ILFunction -> list)
    var seqPointsByFunc = decompiler.CreateSequencePoints(tree); :contentReference[oaicite:13]{index=13}

    // 7) Select the right ILFunction for this method
    // ILSpy uses MoveNextMethod ?? Method metadata token matching. :contentReference[oaicite:14]{index=14}
    var seqPoints = FindMatchingSequencePoints(seqPointsByFunc, methodHandle);

    // 8) Convert to our internal model (already almost identical)
    var normalized = seqPoints.Select(sp => new DebugSequencePoint(...));

    // 9) Build DebugMap + attach source doc
    return new DecompiledResult(csharpText, normalized.ToArray());
}
```

And `WriteCodeAssigningAstLocations` should mirror ILSpy’s:

* `InsertParenthesesVisitor`
* `TextWriterTokenWriter`
* `WrapInWriterThatSetsLocationsInAST`
* `CSharpOutputVisitor`

That exact stack appears in ILSpy’s own code path. ([GitHub][5])

---

## 3.4 Selecting the right `ILFunction` entry

`CreateSequencePoints` returns a dictionary keyed by `ILFunction`. One decompilation can produce multiple IL functions (method body, state machine parts, etc.).

ILSpy’s mixed view resolves this by matching metadata token:

```csharp
(kvp.Key.MoveNextMethod ?? kvp.Key.Method)?.MetadataToken == handle
```

…then taking that `kvp.Value`. ([GitHub][5])

You should do the same, because it handles the common “I asked for MoveNext / got mapping for parent” shapes.

---

## 3.5 Feeding *real* debug info into ILSpy (optional but beneficial)

`CSharpDecompiler` exposes a `DebugInfoProvider` property. ([DNDOCS][3])

Why it matters:

* Better local names
* Better async/iterator reconstruction
* Better decision-making about hiding compiler-generated members

This is optional for initial stepping support (you can step with decompiled maps alone), but it improves readability and reduces surprises.

---

## 3.6 Handling multi-module assemblies

ILSpy explicitly notes that multi-module decompilation is limited and you must construct `CSharpDecompiler` with the `PEFile` that actually contains the type/member you’re decompiling. ([GitHub][7])

So your artifacts layer should:

* identify which module contains the method
* create the decompiler against that module’s PEFile, not the “main” assembly module

---

## 3.7 Assembly resolution strategy: why you probably need a custom resolver

ILSpy includes `UniversalAssemblyResolver` which:

* supports adding search directories (`AddSearchDirectory`)
* resolves references to framework/shared assemblies and nearby binaries
* can be configured not to throw on missing deps (`throwOnError`) ([GitHub][8])

For a dump debugger, this is *okay* as a first pass, but your “correct” resolver likely wants:

* **ClrMD-first resolution:** if the dump says module X is loaded, prefer that identity
* **Symbol-server-backed fallback:** ask your `IArtifactLocator` for module bytes when ILSpy needs referenced assemblies

So I’d recommend:

* Start with `UniversalAssemblyResolver` for MVP.
* Graduate to `DumpAwareAssemblyResolver : IAssemblyResolver` that:

  * resolves by strong name / MVID / path hints where possible
  * calls your locator to fetch missing assemblies
  * returns `PEFile` created from the fetched stream

You still keep `UniversalAssemblyResolver` around as a fallback strategy (it’s good at finding framework assemblies). ([GitHub][8])

---

# The “connection layer” you need (ClrMD ↔ Artifacts ↔ DebugMap ↔ Interpreter)

Even without caching, you need a clean boundary so the interpreter doesn’t “know” about PDBs or ILSpy.

## Minimal set of services

### `IMethodBodyProvider`

* Input: `MethodKey`
* Output: IL bytes + EH + locals sig
* Source:

  * from module PE (SRM)
  * optionally from dump memory if present (ClrMD has module memory; depends on dump type)

### `IDebugMapProvider`

* Input: `MethodKey`
* Output: `DebugMap` + associated “best source view”

  * If PDB available: real source map
  * Else: decompiled map from ILSpy (`CreateSequencePoints`) ([DNDOCS][3])
  * Else: IL map (synthetic)

### `ISourceTextProvider`

* Input: `DebugDocumentId`
* Output: text (real file / embedded / sourcelink / decompiled / IL)

### `IRuntimeToMetadataBridge`

* Input: ClrMD `ClrMethod` / IP
* Output: `MethodKey` (module identity + token), plus generic context (optional for display)

Your interpreter/stepper consumes only:

* IL bodies
* `DebugMap` queries
* source text to display/highlight

…and is blissfully ignorant of whether the map came from PDB or a decompiler.

---

# Practical “definition of done” for these two components

If you implement the `DebugMap` model above and the ILSpy integration mirroring ILSpy’s own mixed-language path:

* You can show decompiled code for *any* method where the PE is available.
* You can compute IL↔text “statement” mapping via ILSpy sequence points. ([DNDOCS][3])
* Your interpreter can do true statement stepping (Step Over/Into/Out) using statement IDs instead of raw IL offsets.
* You have a clean spot (`CodeMappingInfo`) to later improve async/lambda UX. ([DNDOCS][3])

[1]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ICSharpCode.Decompiler/DebugInfo/SequencePoint.cs "raw.githubusercontent.com"
[2]: https://github.com/icsharpcode/ilspy/issues/1245?utm_source=chatgpt.com "Add sequence points on opening/closing braces #1245"
[3]: https://docs.dndocs.com/n/ICSharpCode.Decompiler/8.2.0.7535/api/ICSharpCode.Decompiler.CSharp.CSharpDecompiler.html "Class CSharpDecompiler
 \| ICSharpCode.Decompiler 8.2.0.7535 | DNDocs "
[4]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ICSharpCode.Decompiler/Metadata/CodeMappingInfo.cs "raw.githubusercontent.com"
[5]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ILSpy/Languages/CSharpILMixedLanguage.cs "raw.githubusercontent.com"
[6]: https://github.com/icsharpcode/ILSpy/discussions/2226?utm_source=chatgpt.com "Is there a way to decompile a single function/method call?"
[7]: https://github.com/icsharpcode/ILSpy/discussions/2797?utm_source=chatgpt.com "Decompiling types that aren't in the main module #2797"
[8]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ICSharpCode.Decompiler/Metadata/UniversalAssemblyResolver.cs "raw.githubusercontent.com"
