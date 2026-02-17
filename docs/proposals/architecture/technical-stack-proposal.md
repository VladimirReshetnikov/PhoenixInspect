# Technical Stack Proposal

This document proposes the initial technical stack for the IL interpreter and dump-time evaluation initiative.

The objective is to pick technologies that maximize:

- maintainability over multiple years,
- deterministic behavior for analysis tooling,
- portability across hosts (CLI, IDE plugin, service), and
- ease of incremental hardening from prototype to production.

---

## 1) Design constraints that drive stack choices

1. **Tight runtime control**
   We need deterministic and bounded execution (instruction budgets, cancellation, timeouts).
2. **Low-level metadata/IL fidelity**
   We must read and model ECMA-335 IL accurately, including signatures, generics, and exception regions.
3. **Pluggable architecture**
   Value domains, memory models, call models, and metadata providers should all be swappable.
4. **Embeddable components**
   The same engine should be usable from tests, CLI workflows, and future IDE integrations.
5. **Strong diagnostics and explainability**
   Unknown propagation and loss-of-precision events must be observable.

---

## 2) Proposed primary language and runtime

### Language: C# (latest stable LTS-compatible feature set)

**Why C#**

- Native fit for .NET metadata and IL concepts.
- Excellent ecosystem for analyzers, source generators, and testing.
- Familiar language for likely contributors to a .NET diagnostics library.

**Guideline**

- Prefer language features that are supported in current LTS SDKs used by our target consumers.

### Runtime target: .NET 8 (primary), with optional multi-targeting later

**Why .NET 8 first**

- Modern performance primitives and runtime stability.
- Long-term support window.
- Strong tooling and package ecosystem.

**Deferred decision**

- Multi-targeting (`netstandard2.1`, `net6.0`, etc.) should be evaluated after architecture stabilizes.

---

## 3) Repository and package layout proposal

Proposed solution structure:

- `src/Interpreter.Core`
  - IL semantics engine, abstract state machine, control flow stepping.
- `src/Interpreter.Metadata`
  - metadata adapters and canonical symbol model.
- `src/Interpreter.AbstractDomains`
  - reusable domains (constants, nullness, ranges, taint seed).
- `src/Interpreter.MemoryModels`
  - concrete heap, summary heap, dump-backed adapters.
- `src/Interpreter.CallModel`
  - intrinsic call models, effects, and fallback/havoc policies.
- `src/Interpreter.Diagnostics`
  - tracing APIs, provenance events, explainability payloads.
- `src/Interpreter.Hosting`
  - DI registration, host configuration, policy bundles.
- `src/Interpreter.Cli` (optional early)
  - smoke driver for manual experimentation.
- `tests/*`
  - dedicated test projects per module.

### Package boundaries

- Keep `Core` free of host-specific dependencies.
- Keep `Diagnostics` contracts lightweight and structured.
- Avoid cyclic dependencies; depend “inward” toward `Core` abstractions.

---

## 4) Metadata and IL decoding stack

### Default backend: `System.Reflection.Metadata` + `PEReader`

**Rationale**

- High-performance, low-level metadata reader from Microsoft.
- Good control over blobs, signatures, tokens, and Portable PDB access.
- Suitable for deterministic decoding and explicit handling of edge cases.

### Debug-map and source fallback stack

To align with virtual stepping proposals, use an explicit fallback pipeline rather than optional host heuristics:

1. Portable PDB sequence points/scopes when available.
2. Decompiler-generated source map when PDB is missing/incomplete.
3. IL-offset-only mapping as last resort.

Recommended decompiler backend for map generation: `ICSharpCode.Decompiler` (ILSpy engine).

### Artifact acquisition service

Add a dedicated artifact locator module (cache + symbol server aware) as part of integration packages so both dump-hosting and standalone analysis use identical PE/PDB lookup behavior.

### Optional adapter layer for alternative ecosystems

Support adapter implementations for consumers that already use Cecil/dnlib-like object models.

**Recommendation**

- Define an internal canonical metadata abstraction and isolate backend-specific logic.
- Keep adapters in separate assemblies to reduce transitive dependency footprint.
- Treat legacy Windows PDB readers as optional plugins behind a stable symbol-reader interface.

---

## 5) Dependency injection and configuration

### DI approach: `Microsoft.Extensions.DependencyInjection`

Use standard .NET DI for host-facing composition while allowing direct construction in low-level tests.

### Configuration model

- Strongly-typed options classes for budgets, policies, and feature flags.
- Immutable snapshots of effective configuration per execution session.
- Explicit defaults with “safe by default” posture.

---

## 6) Logging, tracing, and observability

### Logging

- Integrate with `Microsoft.Extensions.Logging` abstractions.
- Use structured logs with event IDs for key engine transitions.

### Tracing and diagnostics payloads

- Add domain-specific trace events (e.g., unknown value creation, state join/widen, blocked call).
- Provide optional machine-readable trace stream for tooling.
- Ensure tracing can be toggled by level to limit overhead.

---

## 7) Testing strategy stack

### Test frameworks

- Unit + integration tests: `xUnit`.
- Assertions: `FluentAssertions` (optional; keep style consistent if adopted).

### Test categories

1. **Instruction semantics tests**
   - One opcode family at a time with edge-case matrices.
2. **Domain law tests**
   - Lattice laws (`join`, `leq`, top/bottom behavior) where applicable.
3. **Fixpoint convergence tests**
   - Ensure analyses terminate under configured widen/budgets.
4. **Golden trace tests**
   - Validate explainability output format and key events.
5. **Regression corpus tests**
   - Preserve behavior for historically problematic IL snippets.

### Fuzz/property testing (later phase)

- Consider adding property-based generation for random IL fragments under constraints.

---

## 8) Performance and memory tooling

### Benchmarks

- `BenchmarkDotNet` for microbenchmarks on instruction stepping, state joins, and call modeling hot paths.

### Profiling guidance

- Track allocations on core stepping loops.
- Benchmark with both concrete and abstract domains.
- Include representative IL bodies from real-world workloads.

---

## 9) Packaging, versioning, and compatibility

### NuGet packaging

- Publish modular packages (core + optional adapters/extensions).
- Use semantic versioning with documented compatibility expectations.

### API governance

- Public API review checklist before each minor/major release.
- Keep experimental APIs behind explicit namespace or preview package boundaries.

---

## 10) Security and supply-chain posture

- Enable lock files and deterministic builds.
- Prefer first-party dependencies where feasible.
- Add baseline static analysis and dependency audit checks in CI.
- Define a policy for handling untrusted metadata inputs (validation and defensive parsing).

---

## 11) CI/CD proposal

### CI stages (minimum)

1. Restore/build
2. Unit/integration tests
3. Formatting/analyzers
4. Benchmark smoke (optional scheduled)
5. Package validation (on release branches)

### Platforms

- Primary: Linux + Windows runners.
- Add macOS only when a real host requirement emerges.

---

## 12) Open questions

1. Do we need early `netstandard2.1` support for host ecosystem compatibility?
2. Should we standardize on a single metadata backend first for speed of delivery?
3. How much trace detail is retained by default vs opt-in?
4. Do we publish a CLI with the first milestone or keep it internal-only initially?


## 13) Prototype implementation snapshot (draft)

> **Draft status notice:** The current `src/` solution is a prototype scaffold used to validate module seams.
> Project names, dependencies, and interfaces are exploratory and may change without compatibility guarantees.

Current prototype projects:

- `Interpreter.Abstractions`
- `Interpreter.Metadata`
- `Interpreter.Core`
- `Interpreter.Diagnostics`
- `Interpreter.Hosting`

Current dependency direction:

- `Abstractions` has no project dependencies.
- `Metadata` -> `Abstractions`.
- `Core` -> `Abstractions`, `Metadata`.
- `Diagnostics` -> `Abstractions`.
- `Hosting` -> `Abstractions`, `Metadata`, `Core`, `Diagnostics` (+ `Microsoft.Extensions.DependencyInjection.Abstractions`).

This snapshot is intentionally minimal and should be interpreted as a stepping stone toward the broader package layout described earlier in this proposal.
