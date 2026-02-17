# Requirements-to-Architecture Traceability Map (Draft)

This document links product requirements to architecture decisions and validation artifacts so we can evaluate design readiness before implementation.

It is intentionally lightweight and will evolve as proposals are refined.

---

## 1) Scope and maintenance model

### In scope

- Product requirements captured in:
  - `docs/proposals/product/post-mortem-debugging-feature-proposal.md`
  - `docs/proposals/product/virtual-step-debugging-feature-proposal.md`
- Architecture and integration design responses under:
  - `docs/proposals/architecture/`
  - `docs/proposals/integration/`
- Planned validation artifacts under:
  - `docs/proposals/architecture/testing-strategy-proposal.md`
  - `docs/plans/future-work-planning.md`

### Out of scope (for now)

- Per-opcode-level tracing (tracked separately in opcode support docs).
- CI-level test case inventory (to be added once implementation test projects exist).

### Update cadence

Update this map whenever one of these events occurs:

1. A new product requirement or user-visible behavior is added.
2. An architecture proposal introduces/changes a contract.
3. A milestone exit criterion changes in `future-work-planning.md`.
4. A requirement has no mapped validation plan.

---

## 2) Requirement ID conventions

Requirement IDs are stable handles for cross-document discussion and review comments.

- `PM-xx`: post-mortem evaluator requirements.
- `VS-xx`: virtual stepping requirements.
- `X-xx`: shared cross-cutting requirements spanning both features.

Status values:

- `Covered`: architecture + validation links exist.
- `Partial`: architecture exists but validation or policy details are incomplete.
- `Gap`: requirement is recognized but no adequate architecture mapping exists yet.

---

## 3) Traceability matrix

| Requirement ID | Product requirement (summary) | Architecture / integration response | Validation artifacts | Status | Notes / follow-up |
|---|---|---|---|---|---|
| PM-01 | Dump snapshot must remain immutable during evaluation. | Read-only memory model and effect boundaries in state/call model docs; dump-backed adapter model in ClrMD integration proposal. | Determinism + safety categories in testing strategy; M4 dump-aware integration exit criteria. | Covered | Keep host UX language aligned with "snapshot integrity" vocabulary. |
| PM-02 | Evaluation must be deterministic and budget-bounded. | Budget tracker and execution-bounds contracts in interpreter framework/testing docs; milestone gating for deterministic replay. | Deterministic replay expectations and benchmark planning docs; M1/M3.5 exit criteria. | Covered | Add explicit default budget presets to product docs after policy doc is drafted. |
| PM-03 | Block unsafe capabilities (filesystem/network/process/native interop). | Call-model policy (`Interpret`/`Model`/`Stop`) plus effect tags and miss reasons; integration boundaries avoid live runtime execution. | Safety-focused fixture classes and blocked-capability assertions in testing strategy. | Partial | Add a dedicated allow/deny policy matrix proposal for host configuration. |
| PM-04 | Return honest trust labels (Pure/Partial/Blocked/Timed Out) with provenance. | Unknown provenance and diagnostic envelope contracts in state-and-domain + architecture overview docs. | Trace assertions and explanation-focused checks in testing strategy; future-work explainability track. | Partial | Define canonical label mapping table between engine diagnostics and UI labels. |
| VS-01 | Step Into/Over/Out semantics should remain predictable for interpreted and modeled calls. | Virtual stepping control-plane contracts and call-model pseudo-frame behavior. | Virtual stepping scenario fixtures and transcript replay assertions in testing strategy; M3.5 exit criteria. | Covered | Ensure stop-reason taxonomy remains single-sourced across docs. |
| VS-02 | Branches on unknown conditions should support conservative join and user-directed choice. | Branch decision policies and `DecisionNeeded` stop behavior across virtual stepping + state model docs. | Branching transcript validation and decision-flow fixtures in testing strategy. | Covered | Add host API examples for branch-choice UX in integration docs. |
| VS-03 | Source stepping fallback order should be PDB → decompiled map → IL. | Debug map proposal and PE/PDB integration proposal define fallback mapping responsibilities. | Mapping-confidence fixtures (exact/approximate/IL-only) in testing strategy; M3.5 criteria. | Covered | Add explicit failure-mode messaging examples for mapping misses. |
| VS-04 | Async and dynamic behaviors should produce explicit outcomes and decision points. | `virtual-tasks` + `dynamic-calls` proposals define lifted callsites and outcome taxonomy. | Async/dynamic fixture corpus and deterministic outcome assertions in future-work milestones + testing strategy. | Covered | Keep taxonomy synchronized with stop reasons and host contracts. |
| X-01 | Host APIs should expose stable miss-reason taxonomy and artifact provenance. | Generic context resolver + integration proposals define artifact resolution and miss-reason handling. | Conformance checklist pattern in `docs/lib` and integration-focused tests. | Partial | Add governance doc for miss-reason taxonomy stability/versioning. |
| X-02 | Framework-heavy stepping should prioritize user intent over runtime machinery noise. | Semantic-modeling proposal defines pattern intrinsics (`lock`, `foreach`, throw-helper forms, interpolation handlers) and modeled-step explainability hooks for virtual stepping. | Virtual stepping fixture corpus comparing baseline vs modeled transcripts; M3.7 semantic-modeling milestone exit criteria. | Partial | Add explicit transcript-diff metrics for modeled-noise reduction and guard against hidden control-flow loss. |
| X-03 | Environment/time/random APIs in dump sessions must be deterministic and honest about missing data. | Semantic-modeling + architecture overview define `SessionSnapshot` and environment intrinsics with explicit unknown-origin tags when data cannot be recovered. | SessionSnapshot extraction fixtures per dump type; deterministic replay assertions for `DateTime`/`Environment` intrinsics. | Partial | Add integration doc section detailing data-source precedence and confidence levels across OS/dump variants. |
| X-04 | Projection-backed collection operations must be bounded, version-aware, and fail-closed on unsupported layouts. | Semantic-modeling proposal introduces object projections, copy-on-write overlays, confidence labels, and decoder identity requirements. | Decoder conformance fixtures and unsupported-layout diagnostics checks; M3.7 exit criteria. | Gap | Publish layout-decoder governance doc and add benchmark limits for projection traversal budgets. |
| X-05 | Special-semantics handling must be unified across call intrinsics, IL-pattern rewrites, and object projections so host diagnostics stay consistent. | Architecture overview + call-model contract are now anchored by `special-semantics-registry-proposal.md`, defining lookup precedence, conflict resolution, modeled-outcome envelope parity, and extension/versioning policy. | Cross-layer contract tests asserting identical provenance/confidence envelopes for call, pattern, and projection modeled steps. | Covered | Next step is implementation-facing conformance tests that lock replay identity (`PackId`/`RuleId`) across modeled transcripts. |

---

## 4) Open gaps to close next

1. **Capability policy matrix**: author a focused proposal that defines default blocked operations, configurable exceptions, and host override constraints.
2. **Trust-label mapping spec**: add a normative table from engine diagnostic categories to user-facing labels and colors/icons.
3. **Miss-reason governance**: publish naming/stability/versioning rules under `docs/governance/` and reference them from integration docs.
4. **Traceability automation plan**: define a lightweight checklist so PR authors update this map when adding or changing requirements.
5. **SessionSnapshot integration contract**: document extraction precedence and fallback behavior for time/environment data across dump formats.
6. **Layout decoder governance**: define decoder versioning, invariant checks, and confidence policy so projections remain trustworthy.

---

## 5) How to use this map during reviews

- When reviewing a product proposal change, verify an existing requirement ID is updated or add a new one.
- When reviewing an architecture proposal change, confirm at least one requirement row is updated.
- Before marking a milestone ready, ensure all related rows are `Covered` or have explicit risk acceptance documented.

This keeps design conversations decision-oriented and prevents unvalidated requirements from silently accumulating.
