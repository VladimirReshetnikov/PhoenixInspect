# Prototype Solution Structure Proposal

> **Draft status notice:** This document describes an intentionally provisional project layout for concept validation.
> Names, boundaries, dependencies, and contract shapes are expected to evolve as design reviews continue.

## 1. Purpose

This proposal introduces an initial `src/` solution layout so the team can:

- make architecture discussions concrete,
- prototype cross-module contracts early,
- validate dependency direction and hosting seams,
- keep implementation effort intentionally lightweight while documentation-first design continues.

The structure below should be treated as a **working hypothesis**, not a final package architecture.

## 2. Proposed project layout

```text
src/
├── Interpreter.Abstractions   # Shared execution contracts and foundational DTOs
├── Interpreter.Metadata       # Metadata resolution contracts and canonical descriptors
├── Interpreter.Core           # Engine orchestration and instruction stepping contracts
├── Interpreter.Diagnostics    # Explainability and diagnostic event sink contracts
└── Interpreter.Hosting        # DI-facing host composition entry points and options
```

## 3. Dependency model (prototype)

The current dependency direction is deliberately inward toward stable concepts:

- `Interpreter.Abstractions` has **no project dependencies**.
- `Interpreter.Metadata` depends on `Interpreter.Abstractions`.
- `Interpreter.Core` depends on `Interpreter.Abstractions` and `Interpreter.Metadata`.
- `Interpreter.Diagnostics` depends on `Interpreter.Abstractions`.
- `Interpreter.Hosting` depends on all previous modules and `Microsoft.Extensions.DependencyInjection.Abstractions`.

This ordering supports experimentation while preserving clean layering constraints.

## 4. Why these names and seams now

### Interpreter.Abstractions
A dedicated contracts module allows all prototype components to agree on execution lifecycle and request/result payloads without importing runtime-specific behavior.

### Interpreter.Metadata
Metadata resolution is expected to have multiple backend options (PE/PDB reader, dump-backed providers, test fixtures). Isolating contracts here prevents metadata concerns from leaking directly into host composition.

### Interpreter.Core
The core assembly currently defines orchestration and stepper interfaces only. This keeps the "engine heart" explicit while still postponing irreversible object-model choices.

### Interpreter.Diagnostics
Explainability and provenance are first-class architectural goals. A dedicated diagnostics contract module makes these concerns visible and testable early.

### Interpreter.Hosting
Host composition normally changes fastest. Keeping DI-oriented registration in a separate module lets us iterate container setup without destabilizing core contracts.

## 5. Prototype caveats and guardrails

- Public APIs are documented and intentionally detailed, but **not stable**.
- Types favor simple strings/dictionaries in several places to accelerate discussion and iteration.
- No assumption should be made that these contracts will survive unchanged into MVP.
- When a contract changes, update this proposal and related architecture docs in the same PR.

## 6. Immediate next increments

1. Add an interface-only `Interpreter.CallModel` project to mirror call/effects proposals.
2. Add an interface-only `Interpreter.MemoryModels` project to validate heap/model seams.
3. Create a short architecture decision log that tracks why each dependency edge exists.
4. Add initial contract conformance tests once runtime tooling is introduced in CI.
