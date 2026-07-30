# Prototype Interface Catalog Proposal

> **Lifecycle:** Historical · **Roadmap relation:** Research
>
> **Restored original proposal — tentative future direction.** This is the original pre-redesign design document,
> restored from repository history (tag `before-redesign`) after the 2026-07-13 scope reset, with project naming
> updated to `PhoenixInspect`. The scope it describes — the draft public interface surface of the original scaffold — is a tentative future direction, not an active
> implementation commitment. The document may be outdated and needs revision to match the architecture that has
> crystallized since then; where it disagrees with a current document or with executable evidence, the current
> source is authoritative. Current counterpart: [`../architecture/prototype-interface-catalog-proposal.md`](../architecture/prototype-interface-catalog-proposal.md).


> **Draft status notice:** The catalog below reflects the first implemented abstraction wave in `src/` and remains intentionally minimal.

## 1. Purpose

This catalog tracks the currently implemented prototype contracts that establish layering boundaries between interpreter core logic, metadata providers, and host integrations.

## 2. Current state

- `PhoenixInspect.Core.Abstractions` now defines opaque handles, execution effects/diagnostics, budget policy hooks, value and memory contracts, resolution services, and call-model contracts.
- `PhoenixInspect.Metadata.Abstractions` now defines module/document identities, metadata universe contracts, normalized symbol/debug-map contracts, and source text retrieval.
- `PhoenixInspect.Host.Abstractions` now defines external runtime references/values, session snapshot contracts, frame seeding, external object reads, runtime↔metadata bridging, and optional generic-context resolution.
- Supporting draft data-shape types were introduced in `PhoenixInspect.Types` and `PhoenixInspect.IL` so the abstraction projects can compile without committing to final runtime representations.

## 3. Module-level contract inventory

### 3.1 `PhoenixInspect.Core.Abstractions`

- Identity handles: `ModuleHandle`, `TypeHandle`, `MethodHandle`, `FieldHandle`
- Effects/diagnostics: `EffectKind`, `EffectEvent`, `EffectSummary`, `UnknownOrigin`, `Diagnostic`, `IDiagnosticSink`
- Budgeting/branching: `BudgetState`, `IBudgetPolicy`, `BranchDecision`
- Value domain: `IValueDomain<TValue>` and supporting enums
- Memory model: `IMemoryModel<TValue, TMem>`, `HavocRegion`
- Resolution contracts: `ResolvedType`, `ResolvedField`, `ResolvedMethod`, `IResolutionServices`
- Call modeling: `CallSite`, `CallOutcome<TValue, TMem>`, `CallModelContext<TValue, TMem>`, `ICallModel<TValue, TMem>`

### 3.2 `PhoenixInspect.Metadata.Abstractions`

- Identities: `ModuleId`, `DocumentId`, `PdbId`
- Metadata modules: `IMetadataModule`, `IMetadataUniverse`
- Symbol contracts: `SequencePoint`, `LocalInfo`, `LocalScope`, `ISymbolInfo`
- Debug map contracts: `DebugDocument`, `SourceSpan`, `IDebugMap`, `IDebugMapProvider`
- Source text contract: `ISourceTextProvider`

### 3.3 `PhoenixInspect.Host.Abstractions`

- Runtime references and values: `ExternalObjectRef`, `ExternalThreadId`, `ExternalFrameId`, `ExternalValue`
- Session/environment snapshot: `SessionSnapshot`, `ISessionSnapshotProvider`
- Object model and optional raw-memory access: `IExternalObjectModel`, `IProcessMemoryReader`
- Frame seeding: `FrameSeed`, `IFrameSeeder`
- Runtime bridge: `RuntimeMethodId`, `RuntimeModuleId`, `RuntimeMethodInfo`, `IRuntimeMetadataBridge`
- Optional generic context bridge: `IGenericContextResolver`

## 4. Design guardrails retained for next iterations

1. Keep abstraction projects dependency-light and free of concrete backend types (ClrMD, SRM, ILSpy, Roslyn).
2. Preserve XML documentation quality for all public types and methods because prototype rationale lives in API comments during design.
3. Add companion documentation updates whenever contracts are expanded so architecture intent remains synchronized with implementation.
4. Treat all signatures and DTO layouts as draft and reversible until MVP architecture checkpoints finalize stable public API boundaries.
