# Source Tour Workbook for `lib/` Snapshots

This workbook is a practical, source-driven tutorial companion for contributors who need to move from **"library familiarity"** to **"adapter decision readiness"** quickly.

Use it when preparing architecture notes, adapter contract updates, or conformance test proposals.

## How to use this workbook

For each library section:

1. read the listed source files in `lib/<library>`,
2. run the checklist questions,
3. produce the requested artifact row(s) in `docs/lib/backend-evidence-log.md`,
4. update the corresponding `docs/lib/<library>/usage-notes.md` if your findings change a recommendation.

---

## ClrMD workbook pass

### Priority source files

- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/DataTarget.cs`
- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/ClrInfo.cs`
- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/ClrRuntime.cs`
- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/ClrThread.cs`
- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/ClrHeap.cs`
- `lib/clrmd/src/Microsoft.Diagnostics.Runtime/CacheOptions.cs`

### Questions to answer

- Which policy inputs can change runtime/object/stack traversal shape?
- Which APIs can return partial data and how do they signal it?
- What is invalidated when cache flush is invoked?

### Artifact to produce

- One evidence row capturing: cache profile, traversal scope, completeness flag, and stop reason taxonomy.

---

## AsmResolver workbook pass

### Priority source files

- `lib/asmresolver/src/AsmResolver.DotNet/ModuleDefinition.cs`
- `lib/asmresolver/src/AsmResolver.DotNet/Serialized/ModuleReaderParameters.cs`
- `lib/asmresolver/src/AsmResolver.DotNet/Serialized/ModuleReaderContext.cs`
- `lib/asmresolver/src/AsmResolver.DotNet/Serialized/DefaultMethodBodyReader.cs`
- `lib/asmresolver/src/AsmResolver.DotNet/Serialized/SerializedMethodDefinition.cs`
- `lib/asmresolver/src/AsmResolver.Symbols.Pdb/PdbImage.cs`

### Questions to answer

- Which constructor/default choices in `ModuleReaderParameters` must be pinned for deterministic operation?
- Which decode failures should map to `UnreadableBody` vs `MalformedImage` vs `MissingSymbolData`?
- Which PDB retrieval paths are lazy and therefore potentially partial?

### Artifact to produce

- One policy matrix row mapping reader-parameter knobs to normalized ingest/body/symbol outcomes.

---

## dnlib workbook pass

### Priority source files

- `lib/dnlib/src/DotNet/ModuleDefMD.cs`
- `lib/dnlib/src/DotNet/ModuleCreationOptions.cs`
- `lib/dnlib/src/DotNet/Emit/MethodBodyReader.cs`
- `lib/dnlib/src/DotNet/Pdb/SymbolReaderFactory.cs`
- `lib/dnlib/src/DotNet/Pdb/Portable/PortablePdbReader.cs`
- `lib/dnlib/Examples/Example3.cs`

### Questions to answer

- Which `Load(...)` overloads are implementation details vs public adapter capabilities?
- Which options affect symbol loading branch selection?
- Which sequence-point/scope details must be preserved for debug-map parity?

### Artifact to produce

- One comparison row for two `ModuleCreationOptions` presets and resulting symbol/body completeness deltas.

---

## Mono.Cecil workbook pass

### Priority source files

- `lib/cecil/Mono.Cecil/ModuleDefinition.cs`
- `lib/cecil/Mono.Cecil/AssemblyReader.cs`
- `lib/cecil/Mono.Cecil/MethodDefinition.cs`
- `lib/cecil/Mono.Cecil.Cil/MethodBody.cs`
- `lib/cecil/Mono.Cecil.Cil/Symbols.cs`
- `lib/cecil/Mono.Cecil.Cil/PortablePdb.cs`
- `lib/cecil/Mono.Cecil/DefaultAssemblyResolver.cs`
- `lib/cecil/Mono.Cecil/MetadataResolver.cs`
- `lib/cecil/Test/Mono.Cecil.Tests/ResolveTests.cs`

### Questions to answer

- Which `ReaderParameters` fields must become explicit adapter policy fields?
- Which resolver failures should become stable miss reasons?
- Which symbol-provider fallback branches need explicit provenance tagging?

### Artifact to produce

- One row mapping load mode + symbol policy + resolver profile to deterministic output differences.

---

## Roslyn workbook pass

### Priority source files

- `lib/roslyn/src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs`
- `lib/roslyn/src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs`
- `lib/roslyn/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs`

### Questions to answer

- Which parse inputs (options, path, strictness) are required for replay fidelity?
- How should we classify parse-vs-bind-vs-semantic-model ownership failures?
- Where do script/regular compilation modes diverge in ways that must be visible in provenance?

### Artifact to produce

- One normalization matrix row for (strict+regular), (strict+script), (non-strict+regular), (non-strict+script).

---

## Cross-library synthesis pass (required)

After completing all library passes, add a short synthesis note answering:

1. Which policy inputs are common across all backends?
2. Which partialness patterns are library-specific?
3. Which normalized miss-reason categories remain ambiguous and need taxonomy refinement?

Publish this synthesis as either:

- an update to `docs/lib/source-review-deep-dive.md`, or
- a dated entry in `docs/lib/backend-evidence-log.md` with links to per-library findings.
