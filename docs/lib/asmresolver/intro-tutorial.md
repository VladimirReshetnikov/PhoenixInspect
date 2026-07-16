# AsmResolver Intro Tutorial (for this project)

## Audience and goal

This tutorial is for contributors who are new to AsmResolver and need to become productive quickly in **our** design context (dump-time IL interpretation and virtual stepping).

You will learn how to:

1. understand AsmResolver's package layout,
2. load modules and inspect metadata within declared bounds,
3. extract CIL method bodies for normalization,
4. peek at PE and PDB layers without coupling core logic to AsmResolver types, and
5. map this knowledge to adapter boundaries in this repository.

> Design-phase reminder: `lib/asmresolver` is a local source snapshot for study. Do not wire `src/` code directly to snapshot projects; use NuGet packages and keep adapter seams strict.

---

## 1) Mental model: AsmResolver is layered by concern

A practical way to navigate AsmResolver is to think in vertical layers:

- **Managed metadata & CIL**: `AsmResolver.DotNet`
- **PE container/image**: `AsmResolver.PE` and `AsmResolver.PE.File`
- **PDB/symbols**: `AsmResolver.Symbols.Pdb`
- **Shared primitives**: `AsmResolver` and lower-level I/O/utilities

For our architecture, this maps cleanly to separate adapters:

- metadata/method adapter,
- PE facts adapter,
- symbol/debug-map adapter.

Keep those adapters independent, then project all outputs into project-owned contracts.

---

## 2) First API to know: `ModuleDefinition`

`ModuleDefinition` is the main entry point for managed modules. It intentionally supports multiple ingestion paths:

- `FromFile(...)`
- `FromStream(...)`
- `FromBytes(...)`
- `FromImage(...)`

This is valuable for us because dump-time workflows can produce bytes, streams, on-disk files, or already-parsed PE images depending on the host pipeline.

### Why reader parameters matter

Most `ModuleDefinition` load paths accept `ModuleReaderParameters`, which centralizes parser policy (resolver behavior, method body reader strategy, PE reader parameters, runtime context).

Treat this as an explicit policy object in our adapters and record it in provenance when possible.

### Minimal loading example

```csharp
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;

var parameters = new ModuleReaderParameters(workingDirectory: @"C:\symbols");
var module = ModuleDefinition.FromFile(@"C:\artifacts\Target.dll", parameters);

Console.WriteLine(module.Name);
Console.WriteLine(module.TopLevelTypes.Count);
```

### Adapter guidance

- Keep exceptions behind adapter-level miss reasons.
- Preserve whether failure occurred in PE parsing vs CLR metadata access.
- Avoid exposing `ModuleDefinition` outside adapter boundaries.

---

## 3) Navigating metadata: types and methods

From a loaded module, you typically traverse:

- module → top-level types,
- type → methods,
- method → signature/body/debug-relevant traits.

AsmResolver uses lazy loading heavily. Properties such as method body and several metadata collections are materialized on first access. For us, that means:

- first access can trigger parsing work and failures,
- deterministic behavior requires predictable traversal order,
- provenance should mention what was (or was not) materialized.

### Discovery snippet

```csharp
foreach (var type in module.GetAllTypes())
{
    foreach (var method in type.Methods)
    {
        Console.WriteLine($"{method.FullName} :: HasBody={method.HasMethodBody}");
    }
}
```

---

## 4) Extracting CIL method bodies (core for interpreter planning)

`MethodDefinition` exposes both generic and typed body access:

- `MethodBody` (`MethodBody?`) for managed/unmanaged abstraction,
- `CilMethodBody` (`CilMethodBody?`) for managed IL,
- `NativeMethodBody` for unmanaged bodies.

For MVP interpreter planning, we mostly care about `CilMethodBody`.

### What you get from `CilMethodBody`

- `Instructions`
- `LocalVariables`
- `ExceptionHandlers`
- `MaxStack`
- build/validation flags for reconstruction scenarios

### CIL extraction snippet

```csharp
using AsmResolver.DotNet.Code.Cil;

if (method.CilMethodBody is CilMethodBody body)
{
    foreach (var instruction in body.Instructions)
    {
        Console.WriteLine($"IL_{instruction.Offset:X4}: {instruction.OpCode} {instruction.Operand}");
    }

    Console.WriteLine($"Locals: {body.LocalVariables.Count}");
    Console.WriteLine($"EH clauses: {body.ExceptionHandlers.Count}");
}
```

### Important design implication

In serialized modules, body materialization flows through a pluggable `IMethodBodyReader` from `ModuleReaderParameters`. This is a strong extension seam for normalization and controlled error handling.

---

## 5) Understanding parse policy: `ModuleReaderParameters`

`ModuleReaderParameters` gives us knobs that matter for deterministic, explainable analysis:

- `ModuleResolver`
- `MethodBodyReader`
- `FieldRvaDataReader`
- `PEReaderParameters`
- `RuntimeContext`

Use these deliberately instead of relying on implicit defaults. Defaults are useful for exploration, but adapters should define and version their own policy profile.

### Practical recommendation for this repo

Create one project-owned adapter policy DTO and map it to AsmResolver parameters in exactly one place. That keeps reproducibility high and prevents option drift across call sites.

---

## 6) PE-level inspection with `PEImage`

When we need low-level artifact facts (headers, sections, directories), use `PEImage` APIs (`FromFile`, `FromBytes`, `FromDataSource`, etc.) and keep this separate from metadata projection.

Why this matters:

- A PE can be syntactically readable while metadata is unusable.
- Separating these layers improves miss taxonomy and user-facing explanations.

If an operation is "PE-only," avoid going through full `ModuleDefinition` unless needed.

---

## 7) PDB entry point: `PdbImage`

For symbols, `PdbImage` mirrors the same ingestion style (`FromFile`, `FromBytes`, `FromReader`) and exposes lazy collections such as modules and symbols.

For our debug-map design:

- ingest symbols through dedicated symbol adapters,
- project to backend-neutral sequence points / scopes / locals,
- never let consumer layers depend on raw PDB record classes.

---

## 8) Suggested "first week" practice workflow

If you are new to AsmResolver in this repo, use this sequence:

1. Load 2-3 representative assemblies with `ModuleDefinition.FromFile`.
2. Enumerate types/methods and capture counts + anomalies.
3. Extract `CilMethodBody` for a curated method set and serialize normalized instruction records.
4. Repeat under a custom `ModuleReaderParameters` policy (resolver/method-body reader tweaks).
5. Load companion PDBs via `PdbImage` and map a minimal symbol projection.
6. Record findings in `docs/lib/backend-evidence-log.md`.

This builds intuition while producing reusable design evidence.

---

## 9) Common pitfalls for new contributors

- **Coupling leak**: returning AsmResolver objects from our core interfaces.
- **Policy drift**: loading modules with ad-hoc reader parameters at each call site.
- **Over-eager feature use**: adopting advanced APIs before MVP adapter contracts stabilize.
- **Partialness blind spots**: treating "no body" as one condition instead of distinguishing unsupported/native/malformed states.
- **Traversal nondeterminism**: relying on unspecified ordering when collecting diagnostics.

---

## 10) How this tutorial connects to existing notes

- Use `usage-notes.md` for architecture fit, risks, and experiments.
- Use this tutorial for practical onboarding and day-1/day-7 contributor actions.

When you learn something concrete from prototype experiments, update both docs:

- tutorial (how to work effectively), and
- usage notes (why this affects design decisions).

---

## Quick reference checklist

Before opening a PR that touches AsmResolver integration design:

- [ ] I used NuGet package assumptions, not direct `lib/asmresolver` project references.
- [ ] I documented which ingestion path (`FromFile`/`FromBytes`/...) is assumed.
- [ ] I explicitly described reader-parameter policy.
- [ ] I separated metadata, PE, and symbol concerns in the proposed adapter design.
- [ ] I mapped failures to backend-neutral miss reasons.
- [ ] I captured at least one evidence item in backend evidence tracking docs when applicable.

---

## 11) Source-backed deep dive: module reader policy and lazy ownership

After scanning the snapshot source, a few implementation details are especially relevant to adapter design:

### `ModuleDefinition` is both the root object and the policy entrance

`ModuleDefinition.FromFile`, `FromStream`, and `FromBytes` all converge on image + reader-parameter flow. In practice this means any adapter-level defaults (resolver, method-body reader, metadata behavior) should be captured once in a `ModuleReaderParameters` factory and reused consistently.

Project impact:

- define one project-owned reader-policy profile per scenario (deterministic replay, exploratory diagnostics),
- avoid ad-hoc per-call reader parameter construction,
- include reader-policy identity in provenance.

### Serialized members are intentionally lazy

`SerializedMethodDefinition` defers most expensive work (name/signature/body/custom attributes/generic parameter lists) until properties are actually read. This is an important reminder that traversal order and "which fields were touched" can affect performance and sometimes behavior around malformed metadata.

Project impact:

- keep adapter projections explicit about which properties they materialize,
- avoid broad eager hydration when only method identity is needed,
- separate cheap indexing passes from expensive body/symbol passes.

### Method body loading is pluggable through reader parameters

`SerializedMethodDefinition.GetMethodBody()` delegates to `_context.Parameters.MethodBodyReader.ReadMethodBody(...)`. This makes method-body decoding strategy a first-class policy seam.

Project impact:

- standardize our method body reader choice (and fallback behavior) in one place,
- classify body-load outcomes into stable categories (`ManagedBody`, `NativeBody`, `UnreadableBody`, `MissingBody`),
- keep raw backend exceptions internal and expose normalized miss reasons.

### Build pipeline (`ManagedPEImageBuilder`) explains why read/write concerns should stay separate

The builder constructs PE scaffolding, .NET directory, imported/exported symbols, relocations, and debug data with diagnostics aggregation. Even if we rarely write images in MVP, this pipeline clarifies object graph expectations and ownership assumptions in the model.

Project impact:

- leverage write-path knowledge only for validation/test fixtures,
- keep production architecture read-oriented for dump evaluation,
- avoid introducing write-path coupling into runtime adapters.

## 12) Suggested "advanced onboarding" review pass (half-day)

1. Read `ModuleDefinition` ingestion APIs and document your expected reader-policy defaults.
2. Trace one method from `SerializedMethodDefinition` token to body materialization.
3. Record when lazy properties are evaluated in your spike run.
4. Compare observed behavior for: metadata-only pass vs full-body pass.
5. Update evidence log with one concrete "policy -> behavior" observation.

## 13) Additional source-backed findings from `lib/asmresolver` review

A broader pass over `AsmResolver.DotNet`, `AsmResolver.PE`, and `AsmResolver.Symbols.Pdb` reveals a few additional tutorial-level points that help contributors avoid common integration mistakes.

### `ModuleReaderParameters` constructors imply different default policy baselines

The snapshot exposes multiple constructors (`workingDirectory`, `IErrorListener`, `RuntimeContext`, `IFileService`, and clone constructor), and each establishes subtly different defaults for resolver and PE reader behavior.

Practical guidance:

- pick a single constructor path per adapter profile,
- avoid mixing constructor styles across call sites,
- document constructor choice in evidence logs because it affects default resolver/error pathways.

### PE ingestion can be performed independently of CLR metadata ingestion

`ModuleDefinition.From...` routes through `PEImage` first, and `PEImage` itself has rich load options. This lets us decouple:

- PE readability checks,
- metadata-table extraction,
- method-body extraction.

Practical guidance:

- keep separate result envelopes for PE vs metadata failures,
- avoid collapsing all read failures into one generic "cannot load module" message,
- preserve stage-at-failure to improve miss-reason diagnostics.

### Body decode strategy is delegated, so body outcomes are policy-dependent

`SerializedMethodDefinition.GetMethodBody()` delegates to the configured `IMethodBodyReader`. Contributors should assume body-read outcomes may differ if this strategy changes.

Practical guidance:

- include method-body reader identity in provenance,
- normalize output into stable categories before returning from adapter,
- add cross-profile parity checks for instruction count/locals/EH extraction.

### PDB leaf-record access is lazy and type-index based

`PdbImage.TryGetLeafRecord(...)` and typed retrieval are index-driven and can return unresolved paths for unknown indices.

Practical guidance:

- avoid assuming type records are eagerly complete,
- capture missing/unresolved type-index cases in debug-map diagnostics,
- keep CodeView/TPI details internal and project-neutral in external DTOs.

## 14) Source-tour checkpoints (new)

Before changing AsmResolver adapter behavior, run this focused source pass:

1. **Ingestion surfaces and policy defaults**
   - Review `lib/asmresolver/src/AsmResolver.DotNet/ModuleDefinition.cs` static `From...` overloads.
   - Review `lib/asmresolver/src/AsmResolver.DotNet/Serialized/ModuleReaderParameters.cs` constructor set and default fields.
2. **Decode context and error surfacing**
   - Review `lib/asmresolver/src/AsmResolver.DotNet/Serialized/ModuleReaderContext.cs` for how bad-image and decode context are carried.
3. **Method-body decode seam**
   - Review `lib/asmresolver/src/AsmResolver.DotNet/Serialized/DefaultMethodBodyReader.cs` and `SerializedMethodDefinition.GetMethodBody()`.
4. **PDB lazy access model**
   - Review `lib/asmresolver/src/AsmResolver.Symbols.Pdb/PdbImage.cs` symbol/module/leaf-record retrieval behavior.

Expected output in adapter review:

- a policy matrix row showing which reader-parameter fields influence: load success, body decode completeness, and symbol/type-record availability.
