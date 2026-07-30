# Prototype Solution Structure Proposal

> **Lifecycle:** Historical · **Roadmap relation:** Research
>
> **Restored original proposal — tentative future direction.** This is the original pre-redesign design document,
> restored from repository history (tag `before-redesign`) after the 2026-07-13 scope reset, with project naming
> updated to `PhoenixInspect`. The scope it describes — the original 42-project scaffold, including virtual-debugging, static-analysis, live-speculation, and sandboxed-runtime hosts; the placeholder projects themselves remain removed and are not being recreated — is a tentative future direction, not an active
> implementation commitment. The document may be outdated and needs revision to match the architecture that has
> crystallized since then; where it disagrees with a current document or with executable evidence, the current
> source is authoritative. Current counterpart: [`../architecture/prototype-solution-structure-proposal.md`](../architecture/prototype-solution-structure-proposal.md).


> **Draft status notice:** This document records the current module scaffold and the first executable integration slice.
> The active module direction is defined by `module-architecture-proposal.md`; project names and boundaries remain prototype hypotheses.

## 1. Purpose

This proposal records the current `src/` scaffold and implemented walking skeleton after retiring the earlier prototype layout. It exists to:

- keep conceptual architecture and repository structure synchronized,
- validate dependency direction at project granularity,
- provide a clean baseline for incremental implementation experiments.

## 2. Current scaffold and implementation layout

The source tree contains 42 source projects matching the module architecture catalog. Most remain project-only placeholders; eight currently contain handwritten prototype types. The implemented slice spans core abstractions/execution, metadata abstractions/SRM, host abstractions/ClrMD, and the supporting type/IL shapes.

- Foundations: `PhoenixInspect.Foundation`
- Core semantics: `PhoenixInspect.Types`, `PhoenixInspect.IL`, `PhoenixInspect.Core.Abstractions`, `PhoenixInspect.Core.Execution`, `PhoenixInspect.Core.IR`, `PhoenixInspect.Core.Analysis`, `PhoenixInspect.Core.Tracing`
- Domains/memory: `PhoenixInspect.Domain.Concrete`, `PhoenixInspect.Domain.CNTypeTaint`, `PhoenixInspect.Domain.Range`, `PhoenixInspect.Memory.VirtualHeap`, `PhoenixInspect.Memory.Overlay`, `PhoenixInspect.Memory.SummaryHeap`
- Models: `PhoenixInspect.Models.Abstractions`, `PhoenixInspect.Models.CoreLib.Primitives`, `PhoenixInspect.Models.Environment`, `PhoenixInspect.Models.Async`, `PhoenixInspect.Models.Dynamic`, `PhoenixInspect.Models.Collections`, `PhoenixInspect.Models.CompilerPatterns`, `PhoenixInspect.Models.RoslynAdapter`
- Metadata/symbols/source: `PhoenixInspect.Metadata.Abstractions`, `PhoenixInspect.Metadata.SRM`, `PhoenixInspect.Metadata.AsmResolver`, `PhoenixInspect.Symbols.PortablePdb`, `PhoenixInspect.Symbols.WindowsPdb.Dia`, `PhoenixInspect.Symbols.WindowsPdb.Managed`, `PhoenixInspect.Decompiler.ILSpy`, `PhoenixInspect.DebugMaps`
- Artifacts/hosts/debugger: `PhoenixInspect.Artifacts.Abstractions`, `PhoenixInspect.Artifacts.SymbolStore`, `PhoenixInspect.Source.SourceLink`, `PhoenixInspect.Host.Abstractions`, `PhoenixInspect.Host.Dump.ClrMD`, `PhoenixInspect.Host.Live.Snapshot`, `PhoenixInspect.Host.Runtime.Sandbox`, `PhoenixInspect.Debugger.Engine`
- Product compositions: `PhoenixInspect.Product.DumpDebugging`, `PhoenixInspect.Product.StaticAnalysis`, `PhoenixInspect.Product.LiveSpeculation`, `PhoenixInspect.Product.SandboxRuntime`

## 3. Dependency policy

The repository scaffolding follows the same layering policy defined in `module-architecture-proposal.md`:

- Dependencies point downward by layer; no reverse edges.
- Core execution remains free of host/metadata/decompiler-specific dependencies.
- Optional integrations are represented as optional projects with explicit package-level isolation.

## 4. Scope boundary for this phase

- Public interfaces and DTOs now exist in the core, metadata, and host abstraction projects. They are draft and reversible; their presence is not an API-stability commitment.
- `PhoenixInspect.Core.Execution` implements deterministic instruction/allocation budget consumption and a single `ret` micro-step.
- `PhoenixInspect.Metadata.SRM` can open an on-disk managed PE, identify a method definition, and return its raw method body for the integration slice.
- `PhoenixInspect.Host.Dump.ClrMD` can load a dump and enumerate managed modules.
- `tests/PhoenixInspect.IntegrationTests` generates a full dump and validates the `dump -> module discovery -> disk PE/SRM -> ret micro-step` path. It does not yet read heap values from dump memory.
- Any future addition of prototype APIs should be accompanied by corresponding architecture doc updates in the same PR.

## 5. Next updates expected

1. Reconcile the provisional AsmResolver backend decision with the SRM implementation used by the current vertical slice.
2. Validate the next vertical slice against actual dump-backed heap data rather than only an on-disk module path.
3. Capture package-version, target-framework, and compatibility constraints before treating project boundaries as durable.

## 6. Dependency alignment note (build-fix follow-up)

Comparison against the project scaffold identified two additional dependency edges required for the solution to build:

- `src/PhoenixInspect.Host.Abstractions/PhoenixInspect.Host.Abstractions.csproj` now references `../PhoenixInspect.Metadata.Abstractions/PhoenixInspect.Metadata.Abstractions.csproj`.
- `src/PhoenixInspect.Metadata.Abstractions/PhoenixInspect.Metadata.Abstractions.csproj` now references `../PhoenixInspect.Core.Abstractions/PhoenixInspect.Core.Abstractions.csproj`.

This keeps dependency direction aligned with the architecture intent (host abstractions can consume metadata abstractions, and metadata abstractions can consume core contracts) while documenting the concrete build-validated edges in `src/`.
