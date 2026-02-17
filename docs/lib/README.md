# Library Reference Notes (`lib/` snapshots)

This section captures design-oriented notes for library snapshots stored under `lib/`.

These notes are intentionally project-specific: they focus on how each library can help (or complicate) our dump-time IL interpretation and virtual-debugging architecture.

## Scope and intent

- `lib/` folders are local snapshots for study and prototyping context.
- They are **not** our runtime dependency source of truth.
- Production-facing implementation should consume official NuGet packages and keep clear adapter boundaries.

## Current library notes

| Library | Snapshot path | Note |
|---|---|---|
| ClrMD | `lib/clrmd` | `clrmd-usage-notes.md` |
| AsmResolver | `lib/asmresolver` | `asmresolver-usage-notes.md` |
| dnlib | `lib/dnlib` | `dnlib-usage-notes.md` |
| Roslyn | `lib/roslyn` | `roslyn-usage-notes.md` |
| Backend capability matrix | N/A (cross-library) | `backend-capability-matrix.md` |
| Adapter conformance checklist | N/A (cross-library) | `adapter-conformance-checklist.md` |
| Backend capability evidence log | N/A (cross-library) | `backend-evidence-log.md` |
| MVP backend decision record | N/A (cross-library) | `mvp-backend-decision-record.md` |
| PE/PDB API comparison | N/A (cross-library) | `pe-pdb-reader-api-comparison.md` |

## How to use these notes

Use this folder as a bridge between:

1. high-level architecture and integration proposals under `docs/proposals/`, and
2. concrete package-selection/adapter decisions in future implementation milestones.

When design decisions evolve, update these notes first and then reconcile proposal docs that depend on them.

## Expansion backlog

- Populate `backend-evidence-log.md` with concrete evidence per capability axis as prototype experiments are run.
- Keep miss-reason taxonomy synchronized with architecture-level provenance terminology.
- Progressively fill `mvp-backend-decision-record.md` and finalize once MVP backend gates are met.
