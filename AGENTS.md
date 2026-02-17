# Working Instructions

## First-step reading
- Read `README.md` and `docs/README.md` before making any changes.

## Current project phase
- The project is currently in **conceptual design**.
- The main deliverables are documentation artifacts in `docs/*.md`.
- Prototype source code is in `src`.
- Snapshots of some relevant libraries are under `lib`. They are provided as a reference to help with our design process, and should be treated as immutable. Do not add direct references to them from `src`, use corresponding NuGet packages. The set of libraries is not exhaustive; other can be used as needed.

## Documentation ownership and expectations
- Continuously expand and refine the documentation set.
- Keep documents well-structured, consistent, and easy to navigate.
- Treat documentation direction as an owned responsibility: make decisions proactively, and revise earlier decisions when better options emerge.
- Do not wait for fully detailed task breakdowns; take initiative and move the design forward.

## Collaboration mindset
- Expect iterative review feedback through PRs.
- Optimize for ambitious progress and thoughtful experimentation, while preserving architectural coherence.
- Take informed risks when they improve learning velocity or design quality.

## Prototype code in design phase
- While documentation remains the primary deliverable, writing **prototype code under `src/`** is a legitimate planning/design activity when it clarifies architecture decisions.
- Prototype code should remain intentionally lightweight, reversible, and explicitly marked as draft.
- Do not present prototype interfaces, project layouts, or API shapes as final commitments.

## Draft API documentation requirement
- All **public types and public methods** in prototype code must include detailed XML documentation comments.
- XML docs should explain intent, parameters/returns, and draft-phase caveats so design rationale remains discoverable in code.

## Autonomous backlog management
- Maintain a `TODO.md` file at the repository root as a self-assigned backlog for periods when no explicit user task is provided.
- Keep between **5 and 10** actionable tasks in `TODO.md`.
- The agent is empowered to curate the backlog autonomously, including rewording, re-prioritizing, adding follow-up tasks, and removing items that are no longer relevant or are low-priority.
- Prefer tasks that advance the conceptual design phase, especially documentation quality, architecture clarity, and decision-readiness.
