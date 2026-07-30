# Restored Original Proposals (Pre-Redesign Planned Scope)

> **Lifecycle:** Historical · **Roadmap relation:** Research

This directory restores the original pre-redesign design documents whose content was replaced in place by the
2026-07-13 architecture reset (tag `before-redesign`, reset commit `c35f1e97a`). Together with the surviving
research proposals under `../architecture/` and `../product/`, they record the original planned scope of the
project — including virtual step debugging, live speculative debugging, static analysis via abstract
interpretation, and sandboxed (no-JIT) IL interpretation — as **tentative future directions**.

Ground rules for reading this directory:

- Every document here is a restored historical original, updated only for project naming (`Interpreter.*` →
  `PhoenixInspect.*`) and prefixed with a status note. The technical content is otherwise unrevised.
- These documents may be outdated and need revision to match the architecture that has crystallized since the
  reset. Where they disagree with a current document or with executable evidence, the current source is
  authoritative.
- Nothing here is an active implementation commitment. The empty placeholder projects (and projects that held only
  minimal API surface drafts) removed by the reset are deliberately **not** restored; their layout is recorded in
  `prototype-solution-structure-proposal.md` as a historical hypothesis only.

## Contents

| Document | Original location | Current counterpart |
|---|---|---|
| `architecture-overview-proposal.md` | `docs/proposals/architecture/` | [`../architecture/architecture-overview-proposal.md`](../architecture/architecture-overview-proposal.md) |
| `opcode-support-matrix-proposal.md` | `docs/proposals/architecture/` | [`../architecture/opcode-support-matrix-proposal.md`](../architecture/opcode-support-matrix-proposal.md) |
| `testing-strategy-proposal.md` | `docs/proposals/architecture/` | [`../architecture/testing-strategy-proposal.md`](../architecture/testing-strategy-proposal.md) |
| `prototype-solution-structure-proposal.md` | `docs/proposals/architecture/` | [`../architecture/prototype-solution-structure-proposal.md`](../architecture/prototype-solution-structure-proposal.md) |
| `prototype-interface-catalog-proposal.md` | `docs/proposals/architecture/` | [`../architecture/prototype-interface-catalog-proposal.md`](../architecture/prototype-interface-catalog-proposal.md) |
| `integration-test-plan.md` | `docs/proposals/` | [`../integration-test-plan.md`](../integration-test-plan.md) |
| `future-work-planning.md` | `docs/plans/` | [`../../plans/future-work-planning.md`](../../plans/future-work-planning.md) |

Original proposals that still survive at their current locations (re-toned with research banners rather than
replaced) were not duplicated here. In particular, the virtual-debugging, semantic-modeling, abstract-domain,
module-architecture, minimal-interfaces, async, and dynamic-call proposals remain at
`../architecture/` and `../product/`.
