# ClrMD usage notes for this project

## Why it matters

ClrMD is the primary candidate for reading runtime state from dumps. In our architecture, it is the front door to:

- process/module inventory,
- thread/frame state,
- managed heap/object traversal,
- runtime method/type handles and related identity hints.

This maps directly to our runtime snapshot and value-provider layers described in integration proposals.

## Best-fit responsibilities (project-specific)

For our conceptual design, ClrMD should own:

1. **Dump runtime observation**
   - enumerate threads, stacks, frames, and loaded modules,
   - read memory for runtime-backed value retrieval.
2. **Runtime fact collection, not semantic interpretation**
   - provide raw runtime facts,
   - avoid embedding interpreter semantics in ClrMD adapters.
3. **Identity bridging inputs**
   - expose enough runtime identity data to map to project-owned `ModuleId` and method identity records.

## Recommended boundary rules

- Keep ClrMD types out of core interpreter contracts.
- Normalize to project-owned immutable records at adapter boundaries.
- Represent lookup failures explicitly (`NotAvailable`, `SymbolMissing`, `Ambiguous`, etc.) instead of leaking backend-specific exceptions.

## Risks and design pressure

1. **Version/runtime diversity pressure**
   - dump/runtime differences can affect field layout assumptions and method details.
2. **Artifact dependency pressure**
   - runtime state alone is insufficient for source-accurate stepping; we still need PE/PDB resolution.
3. **Leaky abstractions**
   - if ClrMD-specific handles flow upward, portability to non-dump scenarios decreases.

## Early action items

- Define a strict `RuntimeSnapshotAdapter` contract with deterministic miss reasons.
- Add a conformance checklist for adapter behavior under partial dumps.
- Capture a small matrix of dump shapes (full, minidump, symbol-poor) and expected degradation behavior.
