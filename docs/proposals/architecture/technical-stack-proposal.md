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
   Deterministic resource counters are required from the first executable slices; the prototype currently accounts
   for admitted instruction transfers. W4.4 additionally records fixed internal graph-construction use under
   64-method and 1,024 method/field/edge-unit safety caps; those are not the later configurable product traversal
   budget. W4.5a separately admits a configured logical-call-depth limit before activation and records required,
   observed, and active-frame depth facts without charging them as instruction budget. Allocation, path, join, and
   widening budgets remain later requirements.
   Cooperative `CancellationToken` cancellation remains a separate host-responsiveness mechanism and must not replace
   replay-stable budgets.
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
  - metadata-derived activation, typed whole-body admission, and deterministic IL micro-step engine.
- `src/Interpreter.Core.Abstractions`
  - draft structural type/method/field identities, atomic resolution, value/memory, evidence-result, and budget
    contracts consumed by the engine.
- `src/Interpreter.Metadata.Abstractions` and `src/Interpreter.Metadata.SRM`
  - projected metadata contracts and the active SRM/PEReader adapter.
- `src/Interpreter.Domain.Concrete`
  - concrete validation domain and persistent allocated/imported virtual memory.
- `src/Interpreter.Host.Abstractions`
  - typed dump-memory/evidence contracts.
- `src/Interpreter.Host.Dump.ClrMD`
  - dump loading, runtime discovery, raw evidence reads, and snapshot-scoped W3 execution resolution/import
    correlation.
- `src/Interpreter.Product.DumpQuery`
  - the closed, bounded root-field query evaluator and result projection.
- `src/Interpreter.Host.ExternalWorker` and `src/Interpreter.Host.ExternalWorker.Runner`
  - a separately landed, non-gating Windows process-boundary prototype outside W1–W4.
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

This is the active prototype backend because it is exercised by executable integration evidence and aligns with the
planned Portable PDB path. W3's reusable SRM projection atomically derives body, calling convention, structural
declaring type, receiver/parameters/return, initialized locals, and contextual FieldDefs from one `MetadataReader`.
W4.4 adds body-independent contextual direct-MethodDef resolution: it proves same-module ordinary managed IL and
decodes a content-equal call signature without reading the target body, RVA, local signature, or locals. The graph
planner subsequently acquires and correlates complete definitions through project-owned abstractions.
Disk-backed differential tests use it over a content-identified PE; the dump resolver uses it over exact counted
metadata bytes and separately revalidates the counted physical method body. The earlier source-scan-only AsmResolver
choice is superseded. Backend-neutral projected contracts remain a goal; an alternative adapter is justified only by
a recorded fixture/corpus gap.

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
   - Compare metadata-derived E1 arithmetic/overflow/void and E2 direct/adjusted getter/null behavior with CoreCLR.
   - Reflection invokes the oracle only; SRM supplies the interpreter's complete activation shape.
5. **Prepared dump-execution tests (W3)**
   - Resolve method/signature/field from exact counted dump evidence, correlate and import one exact `Int32` cell,
     execute the admitted getter through `IMemoryModel`, and replay after closing/reopening/rebinding the dump.
   - Assert structural identities, typed plan boundaries, resolver/memory call counts, state/memory equality, budget,
     events, terminal null behavior, and canonical transcript equality.
6. **Direct-call graph-preparation tests (W4.4)**
   - Resolve only exact same-module managed-IL MethodDefs, then freeze complete typed rooted acyclic graphs with
     deterministic shared-callee deduplication, required logical depth, canonical dependency order, fixed internal
     limits, and no partial-plan failures.
   - Keep this lane dump-free and distinguish graph admission from later execution and product charging.
7. **Exact prepared-graph execution tests (W4.5a)**
   - Activate only a complete frozen graph, reject insufficient configured logical depth before a frame exists, and
     execute exact direct `call`/`ret` transfers without resolving metadata again.
   - Assert one-instruction accounting per call/return, unchanged memory, return-site fidelity, ordered frame events,
     observed/active depth high-water facts, terminal replay invariants, and failure atomicity.
   - Keep explained-unknown call/return lineage, call models, counterfactual product contracts, dump integration, and
     hosted closure outside this lane.

The external-worker regression project is compiled through the solution, but its tests are not invoked. The five
hostile-corpus facts in the integration assembly are tagged `Scope=Cybersecurity`, and all current W1–W4 milestone test commands
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

Cybersecurity work for external artifacts is explicitly outside W1–W4. The following remains product-entry
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
projects as topology/compilation-health evidence. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact W2 closure commit `5bed47100`.

Hardened W3 checkpoint `19c292f9f` passes locally through the same headless workflow: locked restore; the strict
15-project Release build with zero warnings/errors; Markdown-link and headless-workflow guards; 103 non-cybersecurity
unit tests; 67 fast integration tests; 5 ordinary dump tests; 1 optimized-context test; and the focused 2-test W3
lane. All four jobs also pass at that exact implementation commit in [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). Formal W3 closure is recorded
at exact documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit.

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
- Core execution now uses structural module/MethodDef/TypeDef/FieldDef identity, atomic method/signature/local projection,
  metadata-derived activation, frozen typed whole-body admission, an injected persistent-memory capability, and a
  terminal typed-null target outcome. W4.4 adds a separate frozen graph-preparation mode for exactly one direct
  MethodDef helper signature; W4.5a adds an explicitly activated prepared-graph path that executes exact direct
  `call`/`ret` frames while the legacy single-body path remains call-free. These profiles do not imply branches,
  explained-unknown call/return lineage, modeled calls, EH, statics outside the exact callee, byrefs,
  generics, or arbitrary instance methods.
- Dump integration reads the MethodDef RVA from counted dump metadata and decodes the tiny/fat header, code,
  `maxstack`, init-locals flag, local-signature token, and declared extra sections from counted dump memory. It projects
  the signature and exact `ldfld` FieldDef from the same metadata, correlates one exact runtime field observation, and
  imports only that cell. The independently opened disk PE carries exact whole-file length/SHA-256 identity and serves
  only as a comparison oracle; its body/signature contributes no fact to dump-backed execution.
- External-input resource ceilings are 8 GiB per dump before hashing/ClrMD parsing, a 256 MiB ClrMD dump cache with stack-trace/root caching disabled, and 512 MiB at the typed external-PE `Open` boundary before SRM parsing. These bounds reduce resource-exhaustion risk but are not a parser/DAC sandbox; trusted-fixture convenience APIs are not external admission boundaries.
- The Windows x64 one-shot worker and malformed-minidump corpus are separately landed, non-gating prototypes outside
  W1–W4. The five hostile-corpus facts and worker test project provide no current milestone validation; all W1–W4
  test invocations exclude `Scope=Cybersecurity`, while repository-wide compilation retains the projects only as
  topology/compilation-health evidence. The projects do not create an admitted external artifact product surface.
- `Interpreter.Core.Execution` depends on core abstractions, not on SRM or ClrMD. `MetadataResolutionServices` supplies
  the disk bridge; `ClrmdDumpExecutionResolver` supplies the counted-dump bridge while implementing the same
  `IResolutionServices` contract, including body-independent contextual direct-MethodDef targets.
- W4.4 checkpoints `2e596c117`/`742ef2c4f` freeze complete definitions, typed boundaries, canonical nodes/fields/call
  sites, shared-callee deduplication, required logical depth, and internal traversal use before exposing a plan. The
  exact fixture is two nodes, two fields, one edge at IL offset 12, depth two, and five units. W4.4 realizes 3,651
  added LOC (2,076 production plus 1,575 tests), split 1,043/2,608; cumulative realization through W4.4 is 10,679 LOC
  and its checkpoint projection was 21,179–26,779 LOC. These historical figures remain preserved.
- Exact W4.5a commit `356c07037` activates that frozen graph for exact direct calls with canonical call-site/return-site
  identity, configured and required logical-depth facts, observed and active-frame high-water accounting, ordered frame
  events, one-instruction call/return charging, unchanged memory, and no metadata re-resolution. It realizes 3,334
  added LOC (1,590 production plus 1,744 tests), bringing W4.1–W4.5a to 14,013 realized LOC. W4.5b remains estimated at
  1,800–2,700 LOC; combined W4.5 is projected at 5,134–6,034 LOC and full W4 at 24,013–29,313 LOC, with the original
  16,860–25,310 baseline preserved. Headless evidence passed locked restore, the strict fifteen-project Release
  solution build and strict unit/integration project builds at 0 warnings/0 errors, focused prepared-graph tests 25/25,
  the W4 fixture 7/7, complete unit 275/275, fast integration 74/74, ordinary dump 5/5, optimized dump 1/1, and both
  documentation guards, with zero skips and `Scope!=Cybersecurity` on every behavioral filter. An independent audit found no
  remaining production findings after the checkpoint fixes. Explained-unknown call/return lineage, models, product,
  dump, and hosted closure remain pending.
- The first product composition is a deliberately closed root-field dump query. There is not yet a frame-root binder, general C# expression front end, production object-model breadth, orchestrator, debugger control plane, or analysis engine.
- Dump-query results retain explicit source/snapshot/module/fallback context and only the deterministic bounds whose
  operations were reached. Partial primitive wrappers remain explanatory evidence rather than decoded scalar answers,
  and the 22-case/20-expression fresh-session corpus reproduces all result identities plus all 13 prepared-plan
  identities.

This snapshot is a plumbing proof, not evidence that the proposed package decomposition or public contracts have converged.
