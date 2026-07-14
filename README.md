# Tools: IL Interpreter & Dump-Time Evaluation (Concept Design)

This repository is the **design home** for an experimental .NET IL interpreter and a safe, explainable system for evaluating expressions against memory dumps.

If you only read one thing: this project is about making dump-time debugging workflows more trustworthy through deterministic execution, bounded analysis, and explicit explanations when answers are partial or unknown.

## Project gist

We are designing and prototyping—not yet shipping—a library and architecture that can power experiences such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) over dump-backed sessions,
- explainable analysis when runtime behavior cannot be reproduced exactly.

Core principles:

- **Deterministic and budgeted execution** over unbounded simulation.
- **Safety-first behavior** over risky “best effort” guessing.
- **Explainability and provenance** over opaque results.
- **Composable architecture** so hosts can integrate incrementally.

## Current phase

- **Status:** conceptual design with an executable prototype, progressing through evidence-led vertical slices.
- **Active delivery target:** a deterministic, read-only evaluator for expressions grounded in a .NET dump.
- **Current evidence:** the Windows fixtures generate and open real dumps read-only, discover a strongly GCHandle-rooted object, validate both its handle slot and object-header method table with counted raw-memory reads, then read an `Int32`, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump memory. The MethodDef RVA, header, code, locals token, padding, and declared EH sections are dump evidence; an independently opened disk PE is a comparison oracle, never an input to the executable dump body. Every dump-query outcome carries explicit snapshot/module identity availability, evidence source, fallback, and only bounds whose guarded operation was reached. Partial observation wrappers remain explanatory evidence rather than fabricated scalar answers, and complete canonical result bytes/fingerprints survive disposing and reopening the dump session. A dump-free concrete kernel executes a closed branchless `Int32` subset and is compared with compiler-emitted methods running on CoreCLR.
- **Physical scope:** ten source projects contain active contracts or behavior; the two newest projects implement the narrow broker/runner boundary for one-shot external dump queries. The earlier 33 empty placeholders remain removed, and physical boundaries are still justified by executable evidence rather than speculative package maps.
- **Primary progress signal:** executable scenarios and tests, with the design under `docs/` kept just ahead of and consistent with that evidence. This remains prototype evidence, not a production-ready evaluator or interpreter.

The W1 dump-evidence slice is executable against generated full and intentionally sparse dumps; the W2 restricted query slice is executable against the generated full dump. W1 is complete for its revised non-security evidence scope: typed exact/partial/unavailable/conflict outcomes, honest answer completeness, stable identity/context/provenance, path-accurate bounds, fresh-session canonical replay, headless execution, truthful topology, and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

A versioned malformed-minidump mutation corpus and a Windows x64 one-shot worker are retained as separately landed, non-gating prototype work outside W1. The worker's malformed-artifact test passed locally at its checkpoint. Their presence does not make external artifacts a supported product input, and no additional cybersecurity work is part of W1.

A versioned optimized Release modeled-incident report keeps five predeclared axes in the denominator and records raw member bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5. It is explicitly generated evidence, not a representative private-production incident corpus, so no readiness rate is claimed and representative incident measurement is not a W1 completion gate. Current in-process caps (8 GiB dump admission, 256 MiB ClrMD dump cache, and 512 MiB managed PE admission) remain resource controls. Virtual stepping, whole-method abstract analysis, async/dynamic lifting, live speculation, sandbox runtime hosting, and additional product surfaces are **research backlog, not delivery commitments**.

## Where to go next

For structured topic lists, document inventory, and recommended reading paths, start here:

- **Repository-wide design and architecture review:** `DESIGN-ARCHITECTURE-REVIEW.md`
- **Documentation index and TOC-like navigation:** `docs/README.md`

For process and roadmap context:

- `docs/governance/project-faq.md`
- `docs/governance/documentation-organization-proposal.md`
- `docs/plans/future-work-planning.md`

## How to use this repository

1. Use this top-level README for intent and orientation.
2. Use `docs/README.md` as the canonical index of topics and reading paths.
3. Read proposals in the sequence that matches your goal (product, architecture, integration, or governance).

## Contribution focus (this phase)

High-value contributions advance or challenge the active executable evidence:

- harden dump reads, identity joins, partial/malformed evidence, and secret-safe failure behavior;
- harden the restricted read-only query slice and extend it only when a concrete incident scenario justifies the next grammar/evidence step;
- add deterministic, differential, and scenario tests at proven boundaries;
- tighten architecture and documentation when executable evidence changes a decision;
- keep design work just ahead of code rather than expanding speculative surface area.

## License

This repository is licensed under the **MIT-0 (MIT No Attribution)** license. See [`LICENSE`](LICENSE) for the full text.
