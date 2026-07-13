# Prototype Solution Structure Proposal

> **Draft status notice:** This document records the current module scaffold and the first executable integration slice.
> The active module direction is defined by `module-architecture-proposal.md`; project names and boundaries remain prototype hypotheses.

## 1. Purpose

This proposal records the current `src/` scaffold and implemented walking skeleton after retiring the earlier prototype layout. It exists to:

- keep conceptual architecture and repository structure synchronized,
- validate dependency direction at project granularity,
- provide a clean baseline for incremental implementation experiments.

## 2. Current scaffold and implementation layout

The source tree contains 42 source projects matching the module architecture catalog. Most remain project-only placeholders; eight currently contain handwritten prototype types. The implemented slice spans core abstractions/execution, metadata abstractions/SRM, host abstractions/ClrMD, and the supporting type/IL shapes.

- Foundations: `Interpreter.Foundation`
- Core semantics: `Interpreter.Types`, `Interpreter.IL`, `Interpreter.Core.Abstractions`, `Interpreter.Core.Execution`, `Interpreter.Core.IR`, `Interpreter.Core.Analysis`, `Interpreter.Core.Tracing`
- Domains/memory: `Interpreter.Domain.Concrete`, `Interpreter.Domain.CNTypeTaint`, `Interpreter.Domain.Range`, `Interpreter.Memory.VirtualHeap`, `Interpreter.Memory.Overlay`, `Interpreter.Memory.SummaryHeap`
- Models: `Interpreter.Models.Abstractions`, `Interpreter.Models.CoreLib.Primitives`, `Interpreter.Models.Environment`, `Interpreter.Models.Async`, `Interpreter.Models.Dynamic`, `Interpreter.Models.Collections`, `Interpreter.Models.CompilerPatterns`, `Interpreter.Models.RoslynAdapter`
- Metadata/symbols/source: `Interpreter.Metadata.Abstractions`, `Interpreter.Metadata.SRM`, `Interpreter.Metadata.AsmResolver`, `Interpreter.Symbols.PortablePdb`, `Interpreter.Symbols.WindowsPdb.Dia`, `Interpreter.Symbols.WindowsPdb.Managed`, `Interpreter.Decompiler.ILSpy`, `Interpreter.DebugMaps`
- Artifacts/hosts/debugger: `Interpreter.Artifacts.Abstractions`, `Interpreter.Artifacts.SymbolStore`, `Interpreter.Source.SourceLink`, `Interpreter.Host.Abstractions`, `Interpreter.Host.Dump.ClrMD`, `Interpreter.Host.Live.Snapshot`, `Interpreter.Host.Runtime.Sandbox`, `Interpreter.Debugger.Engine`
- Product compositions: `Interpreter.Product.DumpDebugging`, `Interpreter.Product.StaticAnalysis`, `Interpreter.Product.LiveSpeculation`, `Interpreter.Product.SandboxRuntime`

## 3. Dependency policy

The repository scaffolding follows the same layering policy defined in `module-architecture-proposal.md`:

- Dependencies point downward by layer; no reverse edges.
- Core execution remains free of host/metadata/decompiler-specific dependencies.
- Optional integrations are represented as optional projects with explicit package-level isolation.

## 4. Scope boundary for this phase

- Public interfaces and DTOs now exist in the core, metadata, and host abstraction projects. They are draft and reversible; their presence is not an API-stability commitment.
- `Interpreter.Core.Execution` implements deterministic instruction/allocation budget consumption and a single `ret` micro-step.
- `Interpreter.Metadata.SRM` can open an on-disk managed PE, identify a method definition, and return its raw method body for the integration slice.
- `Interpreter.Host.Dump.ClrMD` can load a dump and enumerate managed modules.
- `tests/Interpreter.IntegrationTests` generates a full dump and validates the `dump -> module discovery -> disk PE/SRM -> ret micro-step` path. It does not yet read heap values from dump memory.
- Any future addition of prototype APIs should be accompanied by corresponding architecture doc updates in the same PR.

## 5. Next updates expected

1. Reconcile the provisional AsmResolver backend decision with the SRM implementation used by the current vertical slice.
2. Validate the next vertical slice against actual dump-backed heap data rather than only an on-disk module path.
3. Capture package-version, target-framework, and compatibility constraints before treating project boundaries as durable.

## 6. Dependency alignment note (build-fix follow-up)

Comparison against the project scaffold identified two additional dependency edges required for the solution to build:

- `src/Interpreter.Host.Abstractions/Interpreter.Host.Abstractions.csproj` now references `../Interpreter.Metadata.Abstractions/Interpreter.Metadata.Abstractions.csproj`.
- `src/Interpreter.Metadata.Abstractions/Interpreter.Metadata.Abstractions.csproj` now references `../Interpreter.Core.Abstractions/Interpreter.Core.Abstractions.csproj`.

This keeps dependency direction aligned with the architecture intent (host abstractions can consume metadata abstractions, and metadata abstractions can consume core contracts) while documenting the concrete build-validated edges in `src/`.
