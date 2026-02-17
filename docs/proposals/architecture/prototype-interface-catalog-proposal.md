# Prototype Interface Catalog Proposal

> **Draft status notice:** The interfaces in this document are exploratory planning artifacts.
> They are intended to accelerate architecture learning and are not compatibility commitments.

## 1. Purpose

This proposal documents the current prototype interface surface under `src/` so design discussions can reference concrete seams, responsibilities, and dependency direction.

## 2. Prototype solution map

```text
src/
├── Interpreter.Abstractions  # Session lifecycle and request/result contracts
├── Interpreter.Metadata      # Entry-method metadata resolution seam
├── Interpreter.CallModel     # Call target/effect classification seam
├── Interpreter.MemoryModel   # Abstract memory read/write seam
├── Interpreter.Core          # Execution orchestration and step coordination seam
├── Interpreter.Diagnostics   # Explainability event sink seam
└── Interpreter.Hosting       # DI registration and host option seam
```

## 3. Dependency direction (current draft)

- `Interpreter.Abstractions` has no project dependencies.
- `Interpreter.Metadata` → `Interpreter.Abstractions`.
- `Interpreter.CallModel` → `Interpreter.Abstractions`.
- `Interpreter.MemoryModel` → `Interpreter.Abstractions`.
- `Interpreter.Core` → `Interpreter.Abstractions`, `Interpreter.Metadata`, `Interpreter.CallModel`, `Interpreter.MemoryModel`.
- `Interpreter.Diagnostics` → `Interpreter.Abstractions`.
- `Interpreter.Hosting` → all interpreter modules + `Microsoft.Extensions.DependencyInjection.Abstractions`.

This shape intentionally keeps dependency flow from host/coordination edges toward stable contracts.

## 4. Interface groups and rationale

### 4.1 Execution contracts

`Interpreter.Abstractions` keeps request/result/session primitives centralized so all other modules share a single execution vocabulary.

### 4.2 Metadata contracts

`Interpreter.Metadata` isolates method-body acquisition and descriptor shape decisions from core stepping and hosting concerns.

### 4.3 Call-model contracts

`Interpreter.CallModel` defines call-site target/effect classification responsibilities needed by call/effects architecture work.

### 4.4 Memory-model contracts

`Interpreter.MemoryModel` provides a first-pass abstraction for deterministic value reads/writes with provenance-friendly payloads.

### 4.5 Core orchestration contracts

`Interpreter.Core` continues to own execution orchestration and instruction stepping while introducing an explicit coordinator seam for call-model and memory-model collaboration.

### 4.6 Diagnostics and hosting contracts

`Interpreter.Diagnostics` and `Interpreter.Hosting` preserve explainability and composition concerns as explicit, separately evolvable layers.

## 5. Draft-phase guardrails

- All interfaces should be treated as **prototype design artifacts**, not stable APIs.
- Public XML documentation is intentionally detailed to preserve design rationale directly at contract boundaries.
- When adding or revising interfaces, update this catalog and related architecture proposals in the same change.
