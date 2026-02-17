# Prototype Solution Structure Proposal

> **Draft status notice:** This document is a scaffolding snapshot for the current design phase.
> The active module direction is defined by `module-architecture-proposal.md`, and this file tracks how `src/` is currently aligned.

## 1. Purpose

This proposal records the current `src/` scaffolding after retiring the earlier prototype layout. It exists to:

- keep conceptual architecture and repository structure synchronized,
- validate dependency direction at project granularity,
- provide a clean baseline for incremental implementation experiments.

## 2. Current scaffolding layout

The current source tree contains project-only scaffolding (no prototype types yet) for the module architecture catalog:

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

- `src/` currently contains only project files and dependency edges.
- Public interfaces/types are intentionally deferred until design convergence for each module seam.
- Any future addition of prototype APIs should be accompanied by corresponding architecture doc updates in the same PR.

## 5. Next updates expected

1. Add architecture-decision notes for selected optional projects (for example, AsmResolver and Windows PDB options).
2. Capture package version strategy and compatibility constraints once dependency evaluation is complete.
3. Add focused prototype interfaces in a subset of modules after dependency seams are validated through review.

## 6. Dependency alignment note (build-fix follow-up)

Comparison against the current project scaffolding identified two additional dependency edges required for the solution to build:

- `src/Interpreter.Host.Abstractions/Interpreter.Host.Abstractions.csproj` now references `../Interpreter.Metadata.Abstractions/Interpreter.Metadata.Abstractions.csproj`.
- `src/Interpreter.Metadata.Abstractions/Interpreter.Metadata.Abstractions.csproj` now references `../Interpreter.Core.Abstractions/Interpreter.Core.Abstractions.csproj`.

This keeps dependency direction aligned with the architecture intent (host abstractions can consume metadata abstractions, and metadata abstractions can consume core contracts) while documenting the concrete build-validated edges in `src/`.
