# Library Reference Notes (`lib/` snapshots)

This section indexes design-oriented notes for library snapshots stored under `lib/` and separates them from active implementation decisions.

> **Current policy (2026-07):** SRM/PEReader is the active metadata backend and ClrMD is the active dump adapter. The source scans, tutorials, and comparisons in this directory are preserved research, not package-selection instructions. Executable evidence lives in `backend-evidence-log.md`; the active choice and revisit triggers live in `mvp-backend-decision-record.md`.

## Scope and intent

- `lib/` folders are local snapshots for study and prototyping context.
- They are **not** our runtime dependency source of truth.
- Production-facing implementation should consume official NuGet packages and keep clear adapter boundaries.

## Documentation structure for `docs/lib`

To keep source-review output navigable as the snapshots evolve, this folder uses a two-level layout:

1. **Library-specific subdirectories** (`docs/lib/<library>/`)
   - `usage-notes.md`: design guidance and project-facing integration boundaries.
   - `source-scan.md`: source-driven review notes (entry points, lifecycle, policy knobs, and adapter implications) tied to the corresponding `lib/<library>` snapshot.
2. **Cross-library documents in `docs/lib/` root**
   - matrices, decision records, checklists, and synthesis artifacts that compare or coordinate multiple libraries.

Rule of thumb: if a note is primarily about one library, keep it in that library’s subdirectory; if it compares multiple libraries, keep it at `docs/lib/` root.

## Current library notes

| Library | Snapshot path | Notes |
|---|---|---|
| ClrMD | `lib/clrmd` | `clrmd/usage-notes.md`, `clrmd/source-scan.md` |
| ClrMD intro tutorial | `lib/clrmd` | `clrmd/intro-tutorial.md` |
| AsmResolver | `lib/asmresolver` | `asmresolver/usage-notes.md`, `asmresolver/source-scan.md` |
| AsmResolver intro tutorial | `lib/asmresolver` | `asmresolver/intro-tutorial.md` |
| dnlib | `lib/dnlib` | `dnlib/usage-notes.md`, `dnlib/source-scan.md` |
| dnlib intro tutorial | `lib/dnlib` | `dnlib/intro-tutorial.md` |
| Mono.Cecil | `lib/cecil` | `cecil/usage-notes.md`, `cecil/source-scan.md` |
| Mono.Cecil intro tutorial | `lib/cecil` | `cecil/intro-tutorial.md` |
| Roslyn | `lib/roslyn` | `roslyn/usage-notes.md`, `roslyn/source-scan.md` |
| Roslyn intro tutorial | `lib/roslyn` | `roslyn/intro-tutorial.md` |
| Backend capability matrix | N/A (cross-library) | `backend-capability-matrix.md` |
| Adapter conformance checklist | N/A (cross-library) | `adapter-conformance-checklist.md` |
| Backend capability evidence log | N/A (cross-library) | `backend-evidence-log.md` |
| MVP backend decision record | N/A (cross-library) | `mvp-backend-decision-record.md` |
| PE/PDB API comparison | N/A (cross-library) | `pe-pdb-reader-api-comparison.md` |
| Source review deep dive | N/A (cross-library) | `source-review-deep-dive.md` |
| Source tour workbook | N/A (cross-library tutorial) | `source-tour-workbook.md` |

## How to use these notes

Use this folder as a bridge between:

1. high-level architecture and integration proposals under `docs/proposals/`, and
2. executable package-selection and adapter evidence in the prototype.

Do not infer current capability from a source scan. Add a checked-in fixture to the evidence log, then update the decision record and dependent proposals if that evidence changes direction.


## Preserved 2026-02 source-review focus

The current pass expands source-driven notes and tutorials for all five snapshot libraries:

- ClrMD runtime lifecycle/caching and stack/heap access boundaries
- AsmResolver package layering and module/CIL/PDB entry points
- dnlib module/body/symbol reader pipeline behavior
- Mono.Cecil load-policy, resolver, method-body, and symbol-provider behavior
- Roslyn parse/tree/compilation/semantic boundaries for expression front-end design

See each `<library>/usage-notes.md` + `<library>/source-scan.md` pair for design guidance and source-backed detail, and use each `intro-tutorial.md` for contributor onboarding workflows.


## Source-tour workbook

For contributors doing direct snapshot review, use `source-tour-workbook.md` as the default tutorial playbook. It provides per-library file lists, review questions, and required evidence artifacts so source reading produces decision-ready outputs.

## Active maintenance policy

- Add current executable evidence per capability axis to the leading section of `backend-evidence-log.md`.
- Keep miss-reason taxonomy synchronized with architecture-level provenance terminology.
- Change `mvp-backend-decision-record.md` only when its evidence-based revisit triggers are met.
- Do not expand an alternative-backend tutorial or comparison unless an active scenario exposes a concrete gap.

## Historical source-review baseline

The library notes in this folder now include source-structure and API-surface review findings from the local snapshots under `lib/`.

Current emphasis:

- ClrMD runtime ingestion entry points and cache/enumeration behavior
- AsmResolver module/CIL/PDB package layering
- dnlib metadata + method-body + symbol reader pipeline structure
- Mono.Cecil reader-parameter policy, deferred/immediate load paths, resolver flow, and portable PDB handling
- Roslyn C# parser/compilation/semantic entry points for expression front-end design

These findings remain useful design history. They do not supersede the executable SRM path or constitute a reason to add another adapter.

## Tutorial expansion status

The per-library intro tutorials now include source-backed deep-dive sections derived from direct review of snapshot code under `lib/`:

- ClrMD: session lifecycle, cache semantics, stack/heap partialness boundaries.
- AsmResolver: reader-parameter policy, stage-specific load failures, method-body reader seam, and PDB type-index behavior.
- dnlib: loader/policy surface breadth, method-body decode context, and symbol-reader branch selection.
- Mono.Cecil: reader-parameter policy, symbol-provider fallback tree, resolver edge behavior, and mutable CIL model boundaries.
- Roslyn: parse/tree identity inputs, submission-mode semantics, and semantic-model ownership invariants.

Use these tutorial sections as contributor onboarding material before proposing adapter contract changes.

## Deep-dive companion

Use `source-review-deep-dive.md` when you need a side-by-side, source-backed comparison of:

- lifecycle and policy controls,
- method-body/symbol pipeline details,
- partialness and determinism pressure across all five libraries.

The per-library notes remain the primary location for library-owned details; the deep-dive doc is intended as a synthesis layer for architecture and backend decision reviews.
