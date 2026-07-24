# Technical Stack Proposal

> **Lifecycle:** Draft · **Roadmap:** Active

This document proposes the initial technical stack for the IL interpreter and dump-time evaluation initiative.

The objective is to pick technologies that maximize:

- maintainability over multiple years,
- deterministic behavior for analysis tooling,
- portability across hosts (CLI, IDE plugin, service), and
- ease of incremental strengthening from prototype to production.

---

## 1) Design constraints that drive stack choices

1. **Tight runtime control**
   Deterministic resource counters are required from the first executable slices; the prototype currently accounts
   for admitted instruction transfers. W4.4 additionally records fixed internal graph-construction use under
   64-method and 1,024 method/field/edge-unit resource caps; those are not the later configurable product traversal
   budget. W4.5 separately admits a configured logical-call-depth limit before activation and records required,
   observed, and active-frame depth facts without charging them as instruction budget. Allocation, path, join, and
   widening budgets remain later requirements. W4.6 preserves active-frame depth across atomic modeled calls while
   recording capability-entry logical depth and immutable attempt chronology separately.
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

- `src/PhoenixInspect.Core.Execution`
  - metadata-derived activation, typed whole-body admission, and deterministic IL micro-step engine.
- `src/PhoenixInspect.Core.Abstractions`
  - draft structural type/method/field identities, atomic resolution, value/memory, evidence-result, and budget
    contracts consumed by the engine.
- `src/PhoenixInspect.Metadata.Abstractions` and `src/PhoenixInspect.Metadata.SRM`
  - projected metadata contracts and the active SRM/PEReader adapter.
- `src/PhoenixInspect.Domain.Concrete`
  - concrete validation domain and persistent allocated/imported virtual memory.
- `src/PhoenixInspect.Host.Abstractions`
  - typed dump-memory/evidence contracts.
- `src/PhoenixInspect.Host.Dump.ClrMD`
  - dump loading, runtime discovery, raw evidence reads, and snapshot-scoped W3 execution resolution/import
    correlation.
- `src/PhoenixInspect.Product.DumpQuery`
  - the closed, bounded root-field query evaluator and result projection; W6.2 contains the internal C# expression
    front end and versioned tree-shape recognizers here.
- `tests/PhoenixInspect.Tests`, `tests/PhoenixInspect.IntegrationTests`, `tests/PhoenixInspect.TestTarget`, and
  `tests/PhoenixInspect.OptimizedContextTestTarget`
  - fast semantic/contract tests, real dump evidence, and the generated optimized-context report.

Every repository-managed restore/build/test invocation runs through `./eng/Invoke-HeadlessProcess.ps1 dotnet ...` so
the same no-dialog process policy applies locally and in CI.

### Package boundaries

- Keep `PhoenixInspect.Core.Execution` free of host-specific dependencies.
- Avoid cyclic dependencies; depend “inward” toward `PhoenixInspect.Core.Abstractions`.
- Add a physical project only with implementation, an independently useful dependency boundary, and a test that exercises it. Logical future seams stay in documentation.

### C# expression front-end dependency

W6.2 uses `Microsoft.CodeAnalysis.CSharp` as the sole production expression parser under the normative
[C# Expression Front-End and Subset-Admission Contract](csharp-expression-front-end-contract-proposal.md). The initial
package is centrally pinned at `5.3.0`, matching the Roslyn train in the repository's pinned .NET SDK 10.0.201. The
front end calls `SyntaxFactory.ParseExpression` with explicit C# 14 regular-source options and full-text consumption.

The dependency is contained in `PhoenixInspect.Product.DumpQuery`. Workspaces, Scripting, compilation, semantic models,
and emission are not part of the active product path. Internal tree visitors immediately project enabled W2/W5/W6/W7
shapes into project-owned immutable descriptors; no Roslyn object enters core execution, dump/metadata abstractions,
public prototype contracts, or canonical artifacts. A package or language-version change requires a three-bucket
parser/admission corpus diff and a new or explicitly revised front-end profile identity.

Complete expression parsing does not imply complete binding or evaluation. Valid C# outside an enabled profile is a
stable `Unsupported` product result.

The active [`Post-W7 Path Forward`](../../plans/post-w7-path-forward.md) keeps this dependency boundary for W8. W8.1
completed its physical-truth fixture and probes at `220be94b4`; the
[`W8.1 Physical-Truth Disposition`](../../plans/w8-1-physical-truth-disposition.md) freezes their consequences. W8.2
adds detached `StaticFieldExpressionV2` generic/nested/type/alias trees and the mandatory separate
`FrameValueExpressionV1` root descriptor for exact memory-homed `this`, parameters, and live locals. Register homes and
selected-frame generic substitution are outside the admitted surface. Constraint, accessibility,
constructed-assignability, storage, and value binding remain project-owned; neither Roslyn semantic models nor
another parser enter the product path. Checkpoint `5fd87a3e5` adds source-anchored metadata proof contracts, but those
proofs are not yet required inputs to the future V2 syntax/binder/runtime pipeline.

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

W8.2 uses one bounded ECMA signature grammar in `PhoenixInspect.Core.Abstractions` for TypeSpec, FieldSig,
MethodDefSig, and LocalVarSig positions. It emits structural parent-indexed events, exact full-consumption
certificates, and typed invalid/bound outcomes without resolving metadata tokens. `PhoenixInspect.Product.DumpQuery`
owns the event adapter and exact token-resolution catalog. It reconstructs immutable TypeSpec/FieldSig trees only
after exact source-end and token-domain checks, and retains no usable tree on incomplete, invalid, or cap-plus-one
paths. Direct `CLASS` and `VALUETYPE` TypeSpec roots are ordinary Type-grammar roots; later role/construction
classification decides exact, open, non-exact, or invalid use.

The same Product metadata layer now models raw versus role-classified TypeDefs, TypeSpec graph traversal,
GenericParam declaration/table/selected-owner/binding proofs, interface and constraint table aggregates, provisional
construction classification, exact FieldSig anchors, and Nullable construction preservation. These contracts are
detached proof artifacts. A host-owned metadata producer and mandatory downstream proof consumption have not landed.

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
- Explicit defaults with conservative behavior.

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
8. **Explained-unknown prepared-call lineage tests (W4.5b)**
   - Probe the optional `IInterpretedCallLineageDomain<TValue>` only for explained-unknown values after all ordinary
     graph, stack, type, budget, and depth checks pass. Exact values continue through the W4.5a path unchanged.
   - Require complete metadata-ordered two-argument transformation before either argument is published, and transform
     an explained-unknown return before caller mutation. Canonical `CallArgumentTransform` and
     `InterpretedReturnTransform` nodes retain complete call-site identity and predecessor links.
   - Assert the three-way failure taxonomy: missing capability is blocked with `EXEC_CALL_LINEAGE_UNAVAILABLE`,
     capability exceptions are blocked with `EXEC_DOMAIN_FAILURE`, and malformed or semantically changed output is
     invalid with `EXEC_CALL_LINEAGE_INVALID`. Every rejection preserves state, memory, budget, events, frames, and
     published lineage.
   - Capture only the reachable lineage DAG and prevalidate canonical bytes/hashes, ordering, dependencies,
     reachability, type, call-site identity, parameter index, and acyclicity before same-session or fresh-session replay.
     Keep modeled calls, product counterfactual contracts, dump integration, and hosted closure outside this lane.
9. **Structural pure-model contract, planner, and compiler tests (W4.6a)**
   - Freeze the bounded non-generic descriptor/invocation/outcome/registry vocabulary and stable payload-omitting codes.
   - Require exact/no-effect selection only after caller-edge resolution/typing and before prospective target-body
     acquisition; assert opaque modeled-leaf deduplication, graph equality independent of runtime capability identity,
     five-unit/depth-two compiler topology, no fallback/partial graph, and unchanged legacy plan hashes.
   - Assert that modeled-graph activation returns `EXEC_MODEL_EXECUTION_UNAVAILABLE` before depth, arguments, state,
     resolver, or model access. This lane makes no model-execution claim.
10. **Modeled-return lineage contract tests (W4.6b)**
    - Exercise `IPureCallModelLineageDomain<TValue>` directly: embed exact operands, wrap explained operands with
      unchanged kind-4 nodes, and publish one schema-v1 kind-6 modeled-return node in a single prevalidated batch.
    - Assert kinds 1–5 byte/identity compatibility, acyclicity, structural capture/replay validation, fresh-domain
      continuation, and failure atomicity. Model invocation and machine transfer remain outside this historical lane.
11. **Frozen pure-model machine conformance (W4.6c)**
    - Invoke only the capability frozen in the modeled leaf; prohibit registry/resolver/descriptor/body rereads,
      reselection, interpretation fallback, model frames/events, and memory mutation.
    - Assert atomic exact/grounded-unknown caller transfer, one-instruction charging, pre-entry budget rejection,
      immutable attempts, invocation/completion counters, independent logical/active depth witnesses, exact terminal
      depth retention, stable failure taxonomy, and forged-chronology rejection. The focused lane passes 34/34.
12. **Compiler/SRM modeled-call conformance (W4.6d)**
    - Prove direct interpreted/model/CoreCLR exact agreement and interpreted/model degraded-evidence agreement across
      repeated and fresh metadata-reader/domain/machine sessions, including frozen graph fingerprints.
    - The focused lane passes 3/3, aggregate W4 integration 13/13, and Fast 80/80. Every behavioral lane is headless
      and includes the milestone test selection.

Caveat: the current lanes establish behavior only for the named generated fixtures and explicitly admitted input
shapes. Earlier out-of-scope experiments have been removed.

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

## 10) Input-shape caveat

W1–W5 cover only the named generated fixtures and explicitly admitted input shapes. Earlier out-of-scope experiments
have been removed. Any later expansion must define its own scenario, bounded operations,
identity rules, result contract, and executable evidence before entering the product surface.

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

The historical W1 workflow used the same headless wrapper for locked restore, the then-current 15-project Release build, fast tests,
ordinary real-dump evidence, and optimized-context evidence. All four
required jobs passed at exact W1 closure commit `e2580a8a8` in [GitHub Actions run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889).

At W2 implementation checkpoint `ff7cd1965`, every command used the milestone-selected set at that commit and the
then-current 15-project solution. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact W2 closure commit `5bed47100`.

Strengthened W3 checkpoint `19c292f9f` passes locally through the same headless workflow: locked restore; the strict
15-project Release build with zero warnings/errors; Markdown-link and headless-workflow guards; 103 milestone-selected
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

W5 resolves the earlier test-host question: the repository now has one headless reference consumer for the closed
expression-to-result facade, without declaring it a stable shipping CLI contract. See the
completed [`Post-W4 Path Forward`](../../plans/post-w4-path-forward.md). W6 reuses that consumer as the implemented
external boundary for the opt-in member-chain profile recorded in the completed
[`Post-W5 Path Forward`](../../plans/post-w5-path-forward.md). Closed W7 reuses it through an append-only report mode
for fully qualified and selected-frame/PDB-contextual static-field expressions under the
[`Post-W6 Path Forward`](../../plans/post-w6-path-forward.md). The generated conformance and sixteen-dump portfolio
modes run through fresh hidden processes and preserve earlier report schemas; this does not turn the reference
consumer into a shipping CLI. W8.1 adds three generated evidence targets and physical probes without adding a consumer
mode. W8.2 onward adds append-only V2 modes and a 35-incident minimum portfolio: thirty-two core incidents plus one
thread-relative, one RVA-backed, and one exact memory-homed frame-value incident.


## 13) Prototype implementation snapshot (draft)

> **Draft status notice:** The current solution is a reduced ten-source-project prototype organized around executable evidence and a small set of dependency boundaries.
> Project names, dependencies, and interfaces are exploratory and may change without compatibility guarantees.

Current facts:

- The solution retains ten `src/` projects with active code/contracts plus ten test/target/evidence projects; 33 empty placeholders and three later experimental projects were removed, and the one-purpose `Types`/`IL` DTO assemblies were folded into core contracts.
- Handwritten prototype code exists in `PhoenixInspect.Core.Abstractions`, `PhoenixInspect.Core.Execution`, `PhoenixInspect.Domain.Concrete`, `PhoenixInspect.Metadata.Abstractions`, `PhoenixInspect.Metadata.SRM`, `PhoenixInspect.Host.Abstractions`, `PhoenixInspect.Host.Dump.ClrMD`, `PhoenixInspect.Product.DumpQuery`, `PhoenixInspect.Product.DumpDebugging`, and `PhoenixInspect.Headless.ReferenceConsumer`.
- Core execution now uses structural module/MethodDef/TypeDef/FieldDef identity, atomic method/signature/local projection,
  metadata-derived activation, frozen typed whole-body admission, an injected persistent-memory capability, and a
  terminal typed-null target outcome. W4.4 adds a separate frozen graph-preparation mode for exactly one direct
  MethodDef helper signature. W4.5 activates interpreted graphs for exact direct `call`/`ret` frames and, through an
  optional value-domain capability, propagates canonical explained-unknown argument and return lineage while the
  legacy single-body path remains call-free. W4.6a adds a separate exact/no-effect structural pure-model planning
  profile with body-free opaque leaves; W4.6b adds modeled-return lineage; W4.6c atomically executes the frozen
  capability with attempts/counters/depth witnesses; and W4.6d proves compiler/SRM exact and degraded conformance.
  These profiles do not imply branches, broader model dispatch, EH, statics outside
  the exact callee, byrefs, generics, or arbitrary instance methods.
- Dump integration reads the MethodDef RVA from counted dump metadata and decodes the tiny/fat header, code,
  `maxstack`, init-locals flag, local-signature token, and declared extra sections from counted dump memory. It projects
  the signature and exact `ldfld` FieldDef from the same metadata, correlates one exact runtime field observation, and
  imports only that cell. The independently opened disk PE carries exact whole-file length/SHA-256 identity and serves
  only as a comparison oracle; its body/signature contributes no fact to dump-backed execution.
- Resource ceilings are 8 GiB per dump before hashing/ClrMD parsing, a 256 MiB ClrMD dump cache with stack-trace/root caching disabled, and 512 MiB at the typed external-PE `Open` boundary before SRM parsing. Caveat: these bounds are validated only on the named fixture paths and do not admit other artifact shapes.
- `PhoenixInspect.Core.Execution` depends on core abstractions, not on SRM or ClrMD. `MetadataResolutionServices` supplies
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
  added LOC (1,590 production plus 1,744 tests), bringing W4.1–W4.5a to 14,013 realized LOC. W4.5b was then estimated at
  1,800–2,700 LOC; combined W4.5 is projected at 5,134–6,034 LOC and full W4 at 24,013–29,313 LOC, with the original
  16,860–25,310 baseline preserved. Headless evidence passed locked restore, the strict fifteen-project Release
  solution build and strict unit/integration project builds at 0 warnings/0 errors, focused prepared-graph tests 25/25,
  the W4 fixture 7/7, complete unit 275/275, fast integration 74/74, ordinary dump 5/5, optimized dump 1/1, and both
  documentation guards, with zero skips and the milestone test selection on every behavioral filter. An independent audit found no
  remaining production findings after the checkpoint fixes. This is retained as the historical first-half checkpoint.
- W4.5b commit `c72f6ee9e5545240433294cdca4f350808339aef` closes explained-unknown direct-call propagation through
  `IInterpretedCallLineageDomain<TValue>`. Complete metadata-ordered argument vectors and returns receive canonical
  `CallArgumentTransform` (kind 4) and `InterpretedReturnTransform` (kind 5) nodes; schema version 1 and kinds 1–3
  remain byte-for-byte frozen across all 29 legacy identity cases. Capability absence, exceptions, and invalid output
  remain distinct blocked/invalid outcomes, and capture/replay validates the reachable DAG before mutation.
  The checkpoint realizes 2,804 added LOC (766 production plus 2,038 tests), bringing combined W4.5 to 6,138 LOC and
  cumulative W4.1–W4.5 realization to 16,817 LOC. The historical W4.5b estimate was 1,800–2,700 LOC and the combined
  W4.5 projection was 5,134–6,034 LOC; each upper bound was exceeded by 104 LOC. The W4.5-closure projection was
  25,017–29,417 LOC. A later design audit split W4.6 into W4.6a at 1,800–2,600 LOC and the then-unified W4.6b at
  2,700–3,500 LOC (4,500–6,100 combined); this remains historical planning calibration. Preserve the original
  16,860–25,310 baseline, the original combined-W4.5 estimate of 2,300–3,500 LOC, and the successive full-W4
  projections of 18,532–26,132, 19,228–25,728, 21,179–26,779, and 24,013–29,313 LOC as historical planning facts.
  Headless evidence at the exact commit passed locked restore; the strict single-node fifteen-project Release build at
  0 warnings/0 errors; prepared-graph tests 40/40; combined audit/lineage tests 76/76, including 29 legacy identity
  cases; compiler lineage tests 2/2; the W4 integration aggregate 9/9; complete unit 297/297; fast integration 76/76;
  ordinary dump 5/5; optimized dump 1/1; and both documentation guards, with zero skips and
  the milestone test selection on every behavioral filter. An independent audit found no production or test findings.
  Product counterfactual contracts, ClrMD dump grounding, and hosted closure remained later work.
- W4.6a exact commit `77c92789b16d9258c907d5026a36e39f8c957b41` adds bounded `PureCallModelIdentity`/
  `PureCallModelVersion`, exact structural descriptors, a non-generic two-`Int32` invocation/outcome contract,
  registry selection, and `MethodGraphPlanner.RequirePureModel`. Selection occurs after direct-call resolution/typing
  and before target-body acquisition; successful edges point to deduplicated body-free `FrozenPureModelLeaf` values.
  Only `Exact` confidence and `None` effects are admitted. Missing, throwing, invalid, mismatched, non-exact, and
  unsupported-effect outcomes cannot fall back to a body or expose a partial plan. Runtime capability identity is
  excluded from graph equality/hashing; legacy interpreted call-site hashes stay frozen. Modeled-graph activation
  returns `EXEC_MODEL_EXECUTION_UNAVAILABLE` before runtime access. The PDB-free compiler fixture PE has SHA-256
  `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801` and freezes one interpreted root, one
  modeled leaf, two fields, one edge, five units, and depth two.
- W4.6a exact-checkpoint headless evidence passed locked restore; strict fifteen-project Release build at zero
  warnings/errors; complete unit 371/371; fast 77/77; ordinary dump 5/5; optimized dump 1/1; pure-model contracts
  49/49; model planner 25/25; legacy planner 35/35; compiler 1/1; lineage 2/2; both guards; and zero skips with
  the milestone test selection. Independent audits found no behavioral finding. It realizes 2,959 added LOC (1,210
  production plus 1,749 tests/fixture support), exceeding the historical upper estimate by 359 LOC and bringing
  W4.1–W4.6a to 19,776 LOC.
- W4.6b exact commit `fd723a912` adds optional `IPureCallModelLineageDomain<TValue>` and append-only schema-v1 kind-6
  `ModeledReturnTransform`. Exact operands are embedded; explained operands receive unchanged kind-4 call nodes; the
  whole batch is interned atomically with acyclicity and later structural replay validation. Kinds 1–5 remain byte- and
  identity-compatible, and fresh-domain continuation is covered. Strict headless builds passed at zero warnings/
  errors; focused 8/8, combined legacy-plus-modeled lineage 44/44, and the standard single-node integration build plus
  W4 call-lineage 2/2 passed with zero skips and the milestone test selection. It realizes 1,003 added LOC (481 production
  plus 522 tests), with 23 deletions, bringing W4.1–W4.6b to 20,779 LOC. It does not execute a model.
- W4.6c exact commit `877c9fb55` executes only the capability retained by the frozen modeled leaf. Exact and lineage-
  grounded unknown outcomes transfer atomically to the caller, with one instruction event, unchanged memory, and no
  model frame/event. Budget rejection precedes entry; actual entries produce immutable attempts and logical-depth
  witnesses. No registry/resolver/descriptor/body reread, reselection, or fallback is possible. Invocation/completion
  counters, active/logical high-water marks, exact terminal depth retention, and stable capability/outcome/lineage/
  invariant taxonomy are resume-validated. It realizes 2,734 added LOC (1,425 production plus 1,309 tests); strict
  affected builds passed at zero warnings/errors and focused conformance passed 34/34.
- W4.6d exact commit `da5346813` adds 956 test LOC. It directly proves compiler/SRM interpreted/model/CoreCLR exact
  agreement and interpreted/model agreement for both partial/unavailable shapes. The target PE SHA-256 remains
  `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; the mixed case freezes graph hash
  `451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`, while repeated and fresh sessions reproduce
  the both-unknown graph hash `31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`. Focused W4.6d passed 3/3 and aggregate
  W4 integration 13/13. Full exact-checkpoint closure passed locked restore, strict fifteen-project build at zero
  warnings/errors, unit 413/413, Fast 80/80, ordinary dump 5/5, and optimized dump 1/1, with
  zero skips. Every behavioral invocation used the headless wrapper and the milestone test selection.
- W4.6 realizes 7,652 LOC and brings cumulative W4 realization to 24,469 LOC.
- Historical later full-W4 projections are W4.5 closure 25,017–29,417, post-design-audit 27,217–32,117, W4.6a
  checkpoint 28,376–32,476, first concrete W4.6b recalibration 28,876–33,276, post-W4.6b-split 28,826–33,726,
  post-W4.6b checkpoint 28,879–33,279, and pre-W4.6c/d closure 30,079–33,729 LOC. W4.6c/d realized 3,690 LOC
  against their historical 3,400–3,750 estimate. W4.7 subsequently realizes 2,801 LOC, W4.8 11,924 LOC, and W4.9
  2,698 LOC, bringing full W4 implementation to 41,892 LOC; exact implementation closure passed in run 29463426083,
  and final documentation closure passed in run 29463847230.
- The first product composition is a deliberately closed root-field dump query. The complete Roslyn expression front
  end is implemented at W6.2, and W7 implements its bounded selected-frame/PDB/import plus fully qualified
  `StaticFieldExpressionV1` binder/value path. W8.1 adds physical compiler/PDB, runtime-construction, storage,
  assignability, and exact memory-homed frame-value evidence. W8.2 now adds immutable expression/frame syntax,
  caller-supplied selected-method lexical contracts, one shared bounded signature grammar, and source-anchored Product
  metadata proofs through `5fd87a3e5`. No V2 product binder, host-owned lexical/metadata producer, runtime/storage
  mapper, or frame-value consumer has landed, and the proof objects are not yet mandatory consumer inputs.
  There is no general C# binder/evaluator, production object-model breadth, orchestrator, debugger control plane, or
  analysis engine.
- Dump-query results retain explicit source/snapshot/module/fallback context and only the deterministic bounds whose
  operations were reached. Partial primitive wrappers remain explanatory evidence rather than decoded scalar answers,
  and the 22-case/20-expression fresh-session corpus reproduces all result identities plus all 13 prepared-plan
  identities.

This snapshot is a plumbing proof, not evidence that the proposed package decomposition or public contracts have converged.
