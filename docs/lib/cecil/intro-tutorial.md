# Mono.Cecil intro tutorial (project-oriented)

This tutorial is a practical onboarding guide for contributors who are new to Mono.Cecil and need to use it safely in this repository’s design phase.

It is source-driven from `lib/cecil` and intentionally focused on our goals:

- dump-time read-heavy analysis,
- deterministic + explainable outcomes,
- backend-neutral adapter contracts.

---

## 1. Quick mental model

Think of Cecil as a complete assembly graph + CIL + symbol toolkit.

Core pieces to know:

- `ModuleDefinition` and `AssemblyDefinition`: root metadata graph entry points.
- `ReaderParameters`: ingest behavior policy.
- `MethodDefinition` / `MethodBody`: method-level CIL model.
- `ISymbolReaderProvider` + `PortablePdbReader`: debug info ingestion.
- `IAssemblyResolver` + `MetadataResolver`: reference resolution pipeline.

For this project, Cecil is a backend implementation detail behind normalized projection contracts.

---

## 2. Choose and document reader policy first

Before loading anything, decide policy explicitly.

Important knobs in `ReaderParameters`:

- `ReadingMode` (`Deferred` or `Immediate`)
- `ReadSymbols`
- `SymbolReaderProvider` / `SymbolStream`
- `ThrowIfSymbolsAreNotMatching`
- `AssemblyResolver` / `MetadataResolver`
- `ApplyWindowsRuntimeProjections`

Project rule of thumb:

- never rely on implicit defaults in prototype adapters,
- always tag outputs with policy preset identity.

---

## 3. Understand load mode trade-offs

### Deferred mode

- lower initial cost,
- deeper data materializes lazily,
- easier to accidentally trigger expensive traversal later.

### Immediate mode

- eager graph walk,
- easier to reason about "fully touched" state,
- higher up-front cost.

Use both in parity tests so contract behavior is independent of materialization strategy.

---

## 4. Method body walkthrough

At method level, typical path is:

1. find a `MethodDefinition`,
2. read `Body` (lazy decode entry point),
3. inspect:
   - `Instructions`
   - `Variables`
   - `ExceptionHandlers`
   - debug info mappings (if symbols loaded).

Project guidance:

- convert this mutable model into immutable DTOs,
- classify decode quality explicitly,
- preserve operand and EH structure fidelity.

---

## 5. Symbol loading behavior you must account for

Default provider behavior can branch across:

- embedded portable PDB,
- standalone portable PDB,
- native PDB reader (if available),
- MDB reader (if available).

This means outcomes can vary by environment unless policy is pinned.

Project guidance:

- set preferred symbol path ordering in adapter config,
- persist selected path and fallback reason,
- treat symbol-unavailable and symbol-mismatch as first-class outcomes.

---

## 6. Resolver behavior and edge cases

Resolution involves:

- assembly scope/module scope checks,
- exported type and forwarder behavior,
- base-type walk for field/method lookup where applicable.

The Cecil tests include forwarder loops and unresolved shape cases.

Project guidance:

- keep resolver config explicit (search directories, known assemblies),
- normalize unresolved outcomes to miss reasons,
- never throw raw resolver exceptions across adapter boundaries.

---

## 7. Practical adapter skeleton (conceptual)

1. Build reader-policy preset.
2. Load module with explicit parameters.
3. Build lightweight identity index (types/method tokens).
4. Expand requested method bodies + symbols under budget.
5. Normalize into backend-neutral records.
6. Emit provenance:
   - policy preset id,
   - symbol path selected,
   - resolver profile id,
   - partialness/miss reasons.

---

## 8. Suggested first contributor exercises

1. **Method-body projection drill**
   - Pick one method with branches + EH; project to immutable shape.
2. **Symbol provenance drill**
   - Run with and without symbols; compare provenance fields only.
3. **Load-mode parity drill**
   - Compare deferred vs immediate normalized output on same assembly.
4. **Resolver edge-case drill**
   - Reproduce a type-forwarder scenario and classify outcomes.

---

## 9. Common pitfalls in this repo context

- Letting Cecil types escape adapter internals.
- Forgetting to record symbol fallback branch.
- Treating unresolved references as hard failures instead of explainable misses.
- Assuming load mode does not affect observed behavior/cost profile.

---

## 10. One-page pre-PR checklist for Cecil adapter work

- [ ] Reader policy is explicit and named.
- [ ] Load mode is chosen intentionally and documented.
- [ ] Method-body projection emits locals/EH/instruction fidelity.
- [ ] Symbol path and fallback reason are captured.
- [ ] Resolver misses are normalized to project taxonomy.
- [ ] No Cecil model types leak beyond adapter boundary.
- [ ] Evidence note updates are queued in `docs/lib/backend-evidence-log.md`.

This checklist keeps Cecil experiments high-velocity while preserving architecture discipline.
