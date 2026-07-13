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
   Deterministic resource counters are required from the first executable slices; the prototype already accounts for instructions and abstract allocation units. Cooperative `CancellationToken` cancellation remains a separate host-responsiveness mechanism and must not replace replay-stable budgets.
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

### Runtime target: .NET 8 (current prototype), decision required before productization

**Why the prototype currently uses .NET 8**

- Modern performance primitives and runtime stability.
- Strong tooling and package ecosystem.

**Lifecycle correction (2026-07)**

.NET 8 is in maintenance and reaches end of support on November 10, 2026. A multi-year implementation should explicitly choose whether to move the development baseline to .NET 10 LTS (supported through November 2028) while retaining `net8.0` as a consumer target, or to accept the near-term migration cost. See the [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

**Deferred decision**

- Multi-targeting (`netstandard2.1`, `net6.0`, etc.) should be evaluated after architecture stabilizes.

---

## 3) Repository and package layout proposal

Proposed solution structure:

- `src/Interpreter.Core.Execution`
  - IL semantics engine, abstract state machine, control flow stepping.
- `src/Interpreter.Metadata.Abstractions` and `src/Interpreter.Metadata.SRM`
  - metadata adapters and canonical symbol model.
- `src/Interpreter.Domain.*`
  - reusable domains (concrete execution, CN/type-taint, optional range analysis).
- `src/Interpreter.Memory.*`
  - virtual heap, overlay memory, and summary-heap analysis components.
- `src/Interpreter.Models.Abstractions`
  - intrinsic call models, effects, and fallback/havoc policies.
- `src/Interpreter.Core.Tracing`
  - tracing APIs, provenance events, explainability payloads.
- `src/Interpreter.Host.Abstractions`
  - DI registration, host configuration, policy bundles.
- `src/Interpreter.Cli` (optional early)
  - smoke driver for manual experimentation.
- `tests/*`
  - dedicated test projects per module.

### Package boundaries

- Keep `Interpreter.Core.Execution` free of host-specific dependencies.
- Keep `Interpreter.Core.Tracing` contracts lightweight and structured.
- Avoid cyclic dependencies; depend “inward” toward `Interpreter.Core.Abstractions`.

---

## 4) Metadata and IL decoding stack

### Current integration-spike backend: `System.Reflection.Metadata` + `PEReader`

**Rationale**

- High-performance, low-level metadata reader from Microsoft.
- Good control over blobs, signatures, tokens, and Portable PDB access.
- Suitable for deterministic decoding and explicit handling of edge cases.

This is the backend exercised by the current `ret`-only integration test. It conflicts with the provisional MVP decision record that names AsmResolver as the chosen primary backend; that conflict must be resolved by an authoritative ADR before either backend is treated as the product default.

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

> **Draft status notice:** The current solution combines a broad module scaffold with one narrow executable vertical slice.
> Project names, dependencies, and interfaces are exploratory and may change without compatibility guarantees.

Current facts:

- The solution contains 42 `src/` projects plus two test projects; most source projects are placeholders.
- Handwritten prototype code currently exists in `Interpreter.Types`, `Interpreter.IL`, `Interpreter.Core.Abstractions`, `Interpreter.Core.Execution`, `Interpreter.Metadata.Abstractions`, `Interpreter.Metadata.SRM`, `Interpreter.Host.Abstractions`, and `Interpreter.Host.Dump.ClrMD`.
- `tests/Interpreter.IntegrationTests` generates a dump, discovers the target module with ClrMD, reads a `ret`-only body from the on-disk PE with SRM, and executes one budgeted micro-step.
- `Interpreter.Core.Execution` depends on core abstractions, not on a concrete metadata backend. `MetadataResolutionServices` supplies the current bridge.
- There is not yet a concrete value domain, dump-backed memory model, expression front end, orchestrator, debugger control plane, analysis engine, or product composition.

This snapshot is a plumbing proof, not evidence that the proposed package decomposition or public contracts have converged.
