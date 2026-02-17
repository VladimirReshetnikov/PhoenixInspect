# dnlib intro tutorial (project-oriented)

This tutorial is a practical onboarding guide for developers in this repository who are new to dnlib.

It is based on a direct source pass over `lib/dnlib` and is intentionally biased toward our design goals:

- dump-time read-heavy analysis,
- deterministic behavior under partial data,
- backend-neutral adapter contracts,
- explainable miss/fallback outcomes.

> Important: `lib/dnlib` is a snapshot for study only. When we prototype under `src/`, consume dnlib via NuGet and keep dnlib types inside adapter internals.

---

## 1. Mental model: what dnlib gives you

At a high level, dnlib is one integrated stack for:

1. reading PE + .NET metadata,
2. materializing a high-level object model (`ModuleDef`, `TypeDef`, `MethodDef`, `CilBody`),
3. reading/writing symbols (portable, embedded portable, Windows PDB),
4. writing modified assemblies back to disk/streams.

For our project, the most relevant entry point is `ModuleDefMD`, which exposes many `Load(...)` overloads (file, bytes, streams, reflection module) and metadata stream access (`TablesStream`, `StringsStream`, `BlobStream`, etc.).

---

## 2. Source-map first: where to look in `lib/dnlib/src`

When navigating dnlib for the first time, this folder-level map is enough:

- `DotNet/`
  - core definitions (`ModuleDefMD`, `TypeDef`, `MethodDef`, signatures, resolver stack)
- `DotNet/Emit/`
  - CIL object model + parser (`CilBody`, `Instruction`, `MethodBodyReader`)
- `DotNet/MD/`
  - lower-level metadata/table/heap readers
- `DotNet/Pdb/`
  - symbol dispatch + concrete symbol readers
- `PE/`, `IO/`, `W32Resources/`
  - binary substrate and supporting primitives

If you are short on time, start with these files:

- `DotNet/ModuleDefMD.cs`
- `DotNet/ModuleCreationOptions.cs`
- `DotNet/Emit/MethodBody.cs`
- `DotNet/Emit/MethodBodyReader.cs`
- `DotNet/Pdb/SymbolReaderFactory.cs`
- `DotNet/Pdb/Portable/PortablePdbReader.cs`
- `DotNet/AssemblyResolver.cs`

---

## 3. Quick start: open a module safely

```csharp
using dnlib.DotNet;

var context = ModuleDef.CreateModuleContext();
var options = new ModuleCreationOptions(context)
{
    // key policy knobs for our project:
    TryToLoadPdbFromDisk = true,
    // optionally: PdbFileOrData = ...
};

using var module = ModuleDefMD.Load(@"path/to/SomeAssembly.dll", options);
```

Why this matters:

- `ModuleCreationOptions` centralizes reader policy (runtime kind, symbol loading, explicit PDB input).
- Keeping a shared `ModuleContext` aligns resolver behavior across related modules.

For design-phase adapters, treat this as the top of the ingestion pipeline and project normalized DTOs from here (instead of leaking dnlib object types upward).

---

## 4. Explore metadata and members

Basic traversal pattern:

```csharp
foreach (var type in module.GetTypes())
{
    Console.WriteLine(type.FullName);

    foreach (var method in type.Methods)
    {
        Console.WriteLine($"  {method.FullName}");
    }
}
```

Notes for beginners:

- `module.Types` only returns non-nested types.
- `module.GetTypes()` includes nested types.
- For low-level needs, `module.Metadata` exposes table/heap views directly.

This is enough for first-pass inventory tooling (e.g., method-body coverage scans, generic-shape census, token-to-identity mapping experiments).

---

## 5. Read IL with `MethodDef.Body` and `CilBody`

The typical read path is high-level and simple:

```csharp
if (method.HasBody && method.Body is dnlib.DotNet.Emit.CilBody body)
{
    foreach (var instr in body.Instructions)
    {
        Console.WriteLine($"{instr.Offset:X4}: {instr.OpCode} {instr.Operand}");
    }
}
```

`CilBody` also exposes:

- locals (`Variables`),
- exception handlers (`ExceptionHandlers`),
- body-level flags (`InitLocals`, `MaxStack`, header mode).

For our architecture, this maps cleanly to a backend adapter that emits a backend-neutral method-body projection.

---

## 6. Advanced IL decode: `MethodBodyReader`

When you need more control than `MethodDef.Body` gives you (malformed input, custom decode paths, split EH data), use `MethodBodyReader.CreateCilBody(...)` overloads.

Why it is useful in this repository:

- accepts explicit operand resolver and generic context,
- supports separate exception-handler bytes,
- can be used in deterministic malformed-fixture experiments.

That makes it a strong candidate for building miss-reason taxonomy tests (e.g., decode failure class A vs B) while keeping behavior explicit.

---

## 7. Symbols: how dnlib chooses a reader

`DotNet/Pdb/SymbolReaderFactory` is policy-heavy and important.

Key behavior to understand:

1. It inspects debug-directory metadata (`PdbReaderContext`).
2. Embedded portable PDB is checked first in managed flow.
3. If configured and on Windows, COM reader path can be used for Windows PDB.
4. Otherwise it falls back to managed/portable readers.

Project implication: symbol policy must be explicit in our adapter config and captured as provenance (why a path was chosen, why fallback happened).

---

## 8. Portable PDB details worth knowing

`PortablePdbReader.GetMethod(...)` reconstructs method-level symbol info from metadata tables and blobs.

The sequence-point reader includes several behaviors that matter for us:

- compressed/delta-encoded fields,
- hidden-sequence-point markers,
- document record switches inside one method,
- kickoff-method linkage for async/iterator metadata.

If we flatten these details away, our virtual-step/debug-map layer can lose fidelity. Preserve them in normalized form.

---

## 9. Resolution behavior and context hygiene

`AssemblyResolver` is configurable (`UseGAC`, search paths, exact match vs redirects, type-def cache).

Beginner rule of thumb for this project:

- Use one shared `ModuleContext` per analysis session.
- Add explicitly loaded assemblies to resolver cache.
- Treat auto-resolution as policy, not an implicit default.

This reduces non-determinism and makes partialness easier to explain.

---

## 10. Writing modules (useful, but secondary for us)

dnlib is very capable at rewriting assemblies (`module.Write`, writer options/events, strong-name options, custom heaps/sections).

In our current phase, this is mostly useful for:

- generating synthetic fixtures,
- mutating test corpora,
- stress-testing reader robustness.

Do not anchor architecture decisions on rewrite convenience; our primary runtime scenario is dump-time reading/interpretation.

---

## 11. dnlib examples: what each one teaches quickly

The snapshot’s `lib/dnlib/Examples` folder is worth reading in this order:

1. `Example1.cs`: open module + enumerate type/member model.
2. `Example2.cs`: add type/field/method + emit IL + save.
3. `Example3.cs`: build assembly from scratch.
4. `Example4.cs` / `Example5.cs`: stronger writing/signing scenarios.
5. `Example6.cs`: writer event hooks + PE/metadata customization.
6. `Example7.cs`: experimental opcodes + roundtrip behavior.

For new contributors, Example1 + Example2 + Example7 usually provide enough intuition for adapter prototyping.

---

## 12. Suggested onboarding exercises (for this repository)

1. **Metadata projection drill**
   - Build a tiny prototype that loads a module and emits a backend-neutral JSON summary of types/method signatures.

2. **Method body projection drill**
   - For 20 methods, map dnlib `CilBody` to our draft canonical instruction model.

3. **Malformed IL drill**
   - Feed hand-edited method bytes through `MethodBodyReader` and classify decode outcomes.

4. **Symbol provenance drill**
   - Run one module with: (a) explicit PDB bytes, (b) disk discovery, (c) embedded PDB.
   - Record which path is chosen and why.

5. **Parity drill**
   - Compare normalized output from dnlib and AsmResolver over the same generic-heavy sample.

---

## 13. Practical do/don’t list for project contributors

**Do**

- Keep dnlib confined to adapter internals.
- Encode reader/symbol/resolver choices as explicit policy.
- Preserve partialness and miss reasons.
- Keep prototype code reversible and heavily documented.

**Don’t**

- Expose dnlib model types in core contracts.
- Assume one symbol path behaves the same as another.
- Hide resolver fallbacks.
- Treat snapshot source as our production dependency.

---

## 14. One-page checklist before opening an adapter PR

- [ ] I can explain why `ModuleCreationOptions` values are set as they are.
- [ ] Resolver policy is explicit (context, paths, GAC/redirect decisions).
- [ ] Method-body projection handles locals/EH/operands and malformed cases.
- [ ] Symbol pipeline emits provenance (selected reader path + fallback reason).
- [ ] No dnlib types escape adapter boundaries.
- [ ] Findings are captured in docs (`docs/lib/dnlib/*` + evidence log updates as needed).

This checklist helps keep dnlib usage aligned with our architecture constraints while still letting us move quickly in prototype spikes.
