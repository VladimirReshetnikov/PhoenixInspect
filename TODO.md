# TODO

Self-assigned backlog for periods when no specific user task is provided.

1. Draft `docs/proposals/integration/session-snapshot-integration-contract-proposal.md` covering data-source precedence for time/environment recovery across dump formats and OS variants.
2. Author `docs/governance/layout-decoder-governance.md` with decoder versioning rules, invariant checks, and fail-closed policy requirements.
3. Extend `docs/proposals/architecture/testing-strategy-proposal.md` with transcript-diff metrics that quantify stepping-noise reduction for modeled patterns (`lock`, `foreach`, throw helpers).
4. Add registry conformance test-plan details to `docs/proposals/architecture/testing-strategy-proposal.md`, including replay identity assertions (`PackId`, `RuleId`, `Version`).
5. Draft a capability policy matrix proposal defining default blocked operations, configurable allowances, and host override constraints.
6. Add a normative trust-label mapping table translating engine diagnostics into user-facing labels and UX badges.
7. Define miss-reason taxonomy governance (naming, stability guarantees, and telemetry fields) under `docs/governance/`.
