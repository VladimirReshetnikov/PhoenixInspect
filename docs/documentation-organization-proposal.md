# Proposal: Project Documentation Organization

## 1) Purpose

This proposal defines how documentation should be organized, authored, reviewed, and evolved for the IL interpreter and dump-time evaluation project.

The goal is to make docs:

- easy to navigate for new and returning contributors,
- explicit about decisions and uncertainty,
- resilient as the project moves from concept to implementation.

---

## 2) Context and Problem Statement

We currently have strong proposal content in `docs/`, but it is mostly a flat list of large files. As scope grows, this creates discoverability and maintenance risk:

1. Related decisions become scattered.
2. Contributors duplicate definitions or assumptions.
3. It becomes unclear what is stable vs exploratory.
4. Planning and architecture can drift apart silently.

We need an intentional documentation information architecture before implementation complexity arrives.

---

## 3) Documentation Principles

1. **Docs are part of architecture**
   Documentation is not a byproduct; it is a design artifact and source of truth.

2. **Decision visibility over narrative completeness**
   Every important choice should be findable with rationale and status.

3. **Explicit uncertainty**
   Unknowns and open questions must be documented clearly, not hidden.

4. **Stable entry points**
   New contributors should have a predictable reading path.

5. **Low-friction evolution**
   We should enable iterative changes without constant large rewrites.

---

## 4) Proposed Information Architecture

### 4.1 Top-level structure under `docs/`

Proposed directories:

- `docs/overview/`
  - project goals, glossary, architecture map, reading guides.
- `docs/proposals/`
  - active design proposals and concept docs.
- `docs/decisions/`
  - architecture decision records (ADRs), one decision per file.
- `docs/specs/`
  - stable technical contracts/specifications.
- `docs/planning/`
  - roadmap, milestones, sequencing, dependency maps.
- `docs/operations/`
  - quality gates, contribution workflow, review checklists.

### 4.2 Transitional policy

Because we already have documents at `docs/*.md`, migration should be phased:

- **Phase A (now):** keep existing files in place; add cross-links and ownership metadata.
- **Phase B:** move files into structured folders with lightweight redirects/index docs.
- **Phase C:** enforce naming and location conventions for all new docs.

This avoids breaking context while improving structure.

---

## 5) Document Types and Lifecycle

Each document should declare its type in a short metadata block near the top.

### 5.1 Types

- **Overview**: onboarding and high-level framing.
- **Proposal**: suggested design, alternatives, and recommendation.
- **Decision (ADR)**: accepted/rejected decision with rationale and consequences.
- **Spec**: normative behavior/contracts.
- **Plan**: sequencing, milestones, and prioritization.
- **Guide**: contributor workflows and standards.

### 5.2 Status model

Recommended status values:

- `draft`
- `in-review`
- `accepted`
- `superseded`
- `deprecated`

Status should be visible in each doc and in index pages.

---

## 6) Naming and Template Conventions

### 6.1 File naming

- Use kebab-case.
- Prefer descriptive names over abbreviations.
- Include suffix where useful, e.g. `*-proposal.md`, `adr-XXXX-*.md`.

### 6.2 Section conventions for proposals

Minimum required sections:

1. Problem statement
2. Goals and non-goals
3. Constraints
4. Proposed design
5. Alternatives considered
6. Risks and mitigations
7. Open questions
8. Recommendation and next step

### 6.3 ADR template (proposed)

- Context
- Decision
- Status
- Consequences
- Rejected alternatives
- Follow-up actions

---

## 7) Indexes and Navigation Contracts

To keep docs discoverable, add explicit index files:

- `docs/README.md` (master index)
- one `README.md` in each docs subfolder

Each index should include:

- purpose of that section,
- document list with one-line summary,
- status column,
- “read next” links.

Additionally:

- maintain a single glossary page under `docs/overview/glossary.md`,
- link key terms to glossary definitions from major docs.

---

## 8) Ownership and Review Model

### 8.1 Ownership

- Each document should include an owner (person or team).
- Owner is responsible for freshness and consistency.

### 8.2 Review triggers

A doc should be reviewed when:

- a related proposal is accepted/rejected,
- milestone boundaries change,
- core interfaces/contracts are modified,
- terminology changes.

### 8.3 Staleness policy

Every accepted decision/spec should include:

- `last-reviewed` date,
- expected next review trigger.

---

## 9) Cross-Document Consistency Rules

1. **One canonical definition per key term**
   Terms like “unknown value,” “effect model,” and “purity indicator” must resolve to one source.

2. **Decision-to-spec linkage**
   Accepted ADRs should link to the corresponding spec updates (and vice versa).

3. **Plan traceability**
   Roadmap items should reference the docs/specs they depend on.

4. **No silent supersession**
   Superseded docs must link to replacements at the top.

---

## 10) Migration Plan for Existing Docs

### Step 1 (immediate)

- Add repository-level `README.md` as canonical entry point.
- Add this documentation-organization proposal.
- Add a `docs/README.md` index.

### Step 2 (next)

- Rehome current proposals into `docs/proposals/`.
- Move roadmap content into `docs/planning/`.
- Introduce `docs/overview/architecture-overview.md` and glossary.

### Step 3 (after initial structure lands)

- Introduce ADR directory with first architecture decisions.
- Extract stable contract docs into `docs/specs/`.
- Enforce new-doc template via checklist or lightweight tooling.

---

## 11) Immediate Recommended Actions

1. Approve this organization direction.
2. Create `docs/README.md` with categorized links and statuses.
3. Start ADR practice with 1–2 high-impact decisions:
   - metadata backend boundary model,
   - call-model fallback semantics.
4. Draft first spec candidates:
   - unknown provenance schema,
   - effect lattice contract.

---

## 12) Risks and Mitigations

### Risk: Over-structuring too early

Mitigation:

- keep migration phased,
- avoid heavy process tooling initially,
- prioritize helpful indexes over rigid bureaucracy.

### Risk: Documentation drift remains

Mitigation:

- assign owner per doc,
- add review triggers and freshness metadata,
- make consistency checks part of PR review.

### Risk: Contributor friction from templates

Mitigation:

- keep templates short and pragmatic,
- allow exceptions with rationale,
- evolve templates from real usage feedback.

---

## 13) Definition of Done for Documentation Organization

This proposal is considered implemented when:

1. A stable docs index exists with categorized navigation.
2. Core docs are grouped by purpose (overview/proposals/planning/etc.).
3. At least one ADR and one spec are introduced using agreed templates.
4. New docs follow documented naming/type/status conventions.
5. Supersession links are present where documents overlap.
