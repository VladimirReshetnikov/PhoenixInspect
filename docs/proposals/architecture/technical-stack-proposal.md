# Technical Stack Proposal

> **Lifecycle:** Draft · **Roadmap:** Active

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

### Runtime target: .NET 10 LTS

**Decision (2026-07)**

- Development and CI target `net10.0`.
- `global.json` pins the stable .NET 10.0.2xx feature band and permits only later patches in that band.
- Multi-targeting is deferred until an actual consumer requires it.

**Lifecycle correction (2026-07)**

.NET 8 is in maintenance and reaches end of support on November 10, 2026. .NET 10 is active LTS through November 14, 2028. The project therefore moved now, before prototype compatibility becomes expensive. See the [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

**Deferred decision**

- Multi-targeting (`netstandard2.1`, `net6.0`, etc.) should be evaluated after architecture stabilizes.

---

## 3) Repository and package layout proposal

Current prototype structure:

- `src/Interpreter.Core.Execution`
  - IL semantics engine, abstract state machine, control flow stepping.
- `src/Interpreter.Core.Abstractions`
  - draft domain/memory/identity contracts and the small backend-neutral type/method-body shapes they consume.
- `src/Interpreter.Metadata.Abstractions` and `src/Interpreter.Metadata.SRM`
  - projected metadata contracts and the active SRM/PEReader adapter.
- `src/Interpreter.Domain.Concrete`
  - concrete validation domain and persistent virtual memory.
- `src/Interpreter.Host.Abstractions`
  - typed dump-memory/evidence contracts.
- `src/Interpreter.Host.Dump.ClrMD`
  - dump loading, runtime discovery, and raw evidence reads.
- `src/Interpreter.Product.DumpQuery`
  - the closed, bounded root-field query evaluator and result projection.
- `src/Interpreter.Host.ExternalWorker` and `src/Interpreter.Host.ExternalWorker.Runner`
  - a separately landed, non-gating Windows process-boundary prototype outside W1 and W2.
- `tests/Interpreter.Tests`, `tests/Interpreter.IntegrationTests`, `tests/Interpreter.TestTarget`, and
  `tests/Interpreter.OptimizedContextTestTarget`
  - fast semantic/contract tests, real dump evidence, and the generated optimized-context report.

Every repository-managed restore/build/test invocation runs through `./eng/Invoke-HeadlessProcess.ps1 dotnet ...` so
the same no-dialog process policy applies locally and in CI.

### Package boundaries

- Keep `Interpreter.Core.Execution` free of host-specific dependencies.
- Avoid cyclic dependencies; depend “inward” toward `Interpreter.Core.Abstractions`.
- Add a physical project only with implementation, an independently useful dependency boundary, and a test that exercises it. Logical future seams stay in documentation.

---

## 4) Metadata and IL decoding stack

### Current integration-spike backend: `System.Reflection.Metadata` + `PEReader`

**Rationale**

- High-performance, low-level metadata reader from Microsoft.
- Good control over blobs, signatures, tokens, and Portable PDB access.
- Suitable for deterministic decoding and explicit handling of edge cases.

This is the active prototype backend because it is exercised by executable integration evidence and aligns with the planned Portable PDB path. The earlier source-scan-only AsmResolver choice is superseded. Backend-neutral projected contracts remain a goal; an alternative adapter is justified only by a recorded fixture/corpus gap.

### Debug-map and source fallback stack

To align with virtual stepping proposals, use an explicit fallback pipeline rather than optional host heuristics:

1. Portable PDB sequence points/scopes when available.
2. Decompiler-generated source map when PDB is missing/incomplete.
3. IL-offset-only mapping as last resort.

Recommended decompiler backend for map generation: `ICSharpCode.Decompiler` (ILSpy engine).

### Artifact acquisition service

Artifact acquisition remains off by default in the active local slice. When required, add it behind explicit network policy, identity verification, bounded downloads/decompression, and provenance; do not create a placeholder project first.

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

1. **Fast domain, persistent-memory, budget, and admitted-opcode tests**
   - Run without dumps or external artifacts.
2. **Adapter contract tests**
   - Assert project-owned identity, evidence status, provenance, bounds, and stable miss reasons.
3. **Windows dump integration tests**
   - Exercise real dump creation/loading and state exactly which evidence came from dump memory versus disk artifacts.
   - For W2, reopen the same dump and reproduce the complete canonical result byte sequence/SHA-256 for all 22 cases
     plus the canonical plan projection string/SHA-256 for the 13 cases whose preparation succeeds after module/root
     rediscovery.
   - Run the optimized modeled-context target separately from ordinary dump evidence.
4. **Differential tests (W3+)**
   - Compare the fixture-derived concrete opcode subset with CoreCLR.

The external-worker regression project is compiled through the solution, but its tests are not invoked. The five
hostile-corpus facts in the integration assembly are tagged `Scope=Cybersecurity`, and all current W1/W2 test commands
exclude that scope. Repository-wide compilation is topology/compilation-health evidence only, not cybersecurity
behavioral validation.

CFG/fixpoint, multi-domain lattice, virtual-stepping, dynamic, async, and broad performance suites remain research until their roadmap entry gates pass. `testing-strategy-proposal.md` is the source of truth for current evidence and milestone gates.

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

- Do not publish packages during the conceptual prototype phase.
- Revisit modular package publication only after active boundaries have independent consumers and compatibility tests.
- Use semantic versioning with documented compatibility expectations.

### API governance

- Public API review checklist before each minor/major release.
- Keep experimental APIs behind explicit namespace or preview package boundaries.

---

## 10) Future external-input and supply-chain posture

Cybersecurity work for external artifacts is explicitly outside W1 and W2. The following remains product-entry
guidance, not a current completion gate; the landed worker and malformed corpus are non-gating prototypes and do not
admit an external artifact product surface.

- Treat dumps, PE/PDB files, symbol responses, SourceLink documents, and expression text as hostile and potentially secret-bearing.
- Keep network acquisition off unless a host/user explicitly enables it; verify identity before combining remote/disk artifacts with dump evidence.
- Bound raw reads, graph traversal, strings, parser work, downloads, and decompression.
- Never place dump contents, source text, file paths, environment data, or expression results in telemetry by default.
- Run arbitrary external artifacts in a constrained worker process before product exposure; local in-process parsing is prototype-only and is not a sandbox.
- Use central package versions, committed lock files, deterministic builds, dependency review, and minimal dependency surface.

---

## 11) CI/CD proposal

### W0 CI target

The stages below are required by the W0 exit gate. Successful local 2026-07-13 command results are recorded in
`testing-strategy-proposal.md`. The same gates are now service-side `CI-enforced` for exact pushed commit
`3ece32a36eccc06a61025b1b35b58c09f6e4ed09`: documentation consistency passed; the build/fast job passed locked
restore, a zero-warning Release build, 60 semantic/differential tests, and 40 fast adapter/harness tests; and the
dependent Windows job passed 3 real-dump tests in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT). Checkout and .NET setup actions are pinned to verified release commit
SHAs rather than movable major tags.

1. locked restore and Release build under stable .NET 10;
2. fast unit/domain/determinism tests;
3. real dump integration evidence on Windows;
4. deterministic local-Markdown-link consistency with repository-owned diagnostics.

### Revised W1 CI target

The current workflow uses the same headless wrapper for locked restore, the strict 15-project Release build, fast tests,
ordinary real-dump evidence, and optimized-context evidence; worker tests are outside the default W1 lane. All four
required jobs passed at exact W1 closure commit `e2580a8a8` in [GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889).

That historical W1 run predates the explicit scope filter. At W2 implementation checkpoint `ff7cd1965`, every current
test command includes `Scope!=Cybersecurity`; restore/build intentionally remains repository-wide across all 15
projects as topology/compilation-health evidence. Exact-final-HEAD hosted W2 closure remains pending.

Formatting/analyzers, dependency audit, and scheduled benchmarks are added when their signal is stable. Package validation waits until packages exist.

### Platforms

- Windows is required for the current full-dump test signal.
- Add Linux when a checked-in dump fixture or platform-specific live-dump test provides equivalent evidence.
- Add macOS only when a real host requirement emerges.

---

## 12) Open questions

1. Which concrete consumer, if any, justifies multi-targeting?
2. How much trace detail is retained by default versus explicit local opt-in?
3. Does the first restricted-expression slice need an internal CLI, or are tests the right host?


## 13) Prototype implementation snapshot (draft)

> **Draft status notice:** The current solution is a reduced ten-project prototype organized around executable evidence and a small set of dependency boundaries.
> Project names, dependencies, and interfaces are exploratory and may change without compatibility guarantees.

Current facts:

- The solution retains ten `src/` projects with active code/contracts plus test projects; 33 empty placeholders were removed and the one-purpose `Types`/`IL` DTO assemblies were folded into core contracts.
- Handwritten prototype code exists in `Interpreter.Core.Abstractions`, `Interpreter.Core.Execution`, `Interpreter.Domain.Concrete`, `Interpreter.Metadata.Abstractions`, `Interpreter.Metadata.SRM`, `Interpreter.Host.Abstractions`, `Interpreter.Host.Dump.ClrMD`, `Interpreter.Product.DumpQuery`, `Interpreter.Host.ExternalWorker`, and `Interpreter.Host.ExternalWorker.Runner`.
- Dump integration reads the MethodDef RVA from counted dump metadata and decodes the tiny/fat header, code, `maxstack`, init-locals flag, local-signature token, and declared extra sections from counted dump memory. The independently opened disk PE carries exact whole-file length/SHA-256 identity and serves only as a comparison oracle; its body contributes no fact to the dump-backed executable body.
- External-input resource ceilings are 8 GiB per dump before hashing/ClrMD parsing, a 256 MiB ClrMD dump cache with stack-trace/root caching disabled, and 512 MiB at the typed external-PE `Open` boundary before SRM parsing. These bounds reduce resource-exhaustion risk but are not a parser/DAC sandbox; trusted-fixture convenience APIs are not external admission boundaries.
- The Windows x64 one-shot worker and malformed-minidump corpus are separately landed, non-gating prototypes outside
  W1 and W2. The five hostile-corpus facts and worker test project provide no current milestone validation; all W1/W2
  test invocations exclude `Scope=Cybersecurity`, while repository-wide compilation retains the projects only as
  topology/compilation-health evidence. The projects do not create an admitted external artifact product surface.
- `Interpreter.Core.Execution` depends on core abstractions, not on a concrete metadata backend. `MetadataResolutionServices` supplies the current bridge.
- The first product composition is a deliberately closed root-field dump query. There is not yet a frame-root binder, general C# expression front end, production object-model breadth, orchestrator, debugger control plane, or analysis engine.
- Dump-query results retain explicit source/snapshot/module/fallback context and only the deterministic bounds whose
  operations were reached. Partial primitive wrappers remain explanatory evidence rather than decoded scalar answers,
  and the 22-case/20-expression fresh-session corpus reproduces all result identities plus all 13 prepared-plan
  identities.

This snapshot is a plumbing proof, not evidence that the proposed package decomposition or public contracts have converged.
