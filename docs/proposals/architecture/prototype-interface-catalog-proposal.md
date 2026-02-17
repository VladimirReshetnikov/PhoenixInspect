# Prototype Interface Catalog Proposal

> **Draft status notice:** The previous interface catalog has been intentionally retired.
> Current prototype code contains no public types yet; this document now tracks planned interface-entry points by module.

## 1. Purpose

This document provides a placeholder catalog for future interface work while the repository is in scaffolding-only mode.

## 2. Current state

- All earlier prototype contracts were removed.
- `src/` contains project/dependency scaffolding only.
- Interface definitions will be introduced incrementally after module-level dependency review.

## 3. Planned interface-entry modules

The first interface waves are expected in:

- `Interpreter.Core.Abstractions` (execution and policy contracts)
- `Interpreter.Metadata.Abstractions` (metadata/token resolution contracts)
- `Interpreter.Models.Abstractions` (semantic-model extension contracts)
- `Interpreter.Host.Abstractions` (host integration contracts)
- `Interpreter.Artifacts.Abstractions` (artifact/source acquisition contracts)

## 4. Guardrails for future additions

When interfaces are added:

1. Keep contracts minimal and layered.
2. Add XML docs on all public types and members.
3. Update dependency and rationale docs in the same change.
4. Avoid adding product-assembly contracts until lower layers stabilize.
