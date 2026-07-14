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
- **Current evidence:** the Windows fixtures generate and open real dumps read-only, discover a strongly GCHandle-rooted object, validate both its handle slot and object-header method table with counted raw-memory reads, then read `Int32`, `Nullable<Int32>`, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump memory. The MethodDef RVA, header, code, locals token, padding, and declared EH sections are dump evidence; an independently opened disk PE is a comparison oracle, never an input to the executable dump body. The W2 query path parses a closed root/field grammar, binds a typed snapshot-scoped root, selects the field once into an immutable plan, and evaluates that plan without rebinding. Canonical request, plan, root-selection policy, and complete-result identities preserve the exact literal, selector state, owner, full field layout, evidence, and applied-policy distinctions needed for replay. A versioned 22-case corpus spanning 20 distinct expression texts reproduces the complete canonical result byte sequence/SHA-256 for all cases and the canonical plan projection string/SHA-256 for the 13 cases whose preparation succeeds, both within one session and after disposing, reopening, rediscovering, and rebinding the dump. The W3 architecture proof adds structural module/type/method/field identities, SRM-derived signatures and initialized locals, metadata-derived activation, frozen typed whole-body admission, an injected persistent-memory capability, and closed branchless `Int32` arithmetic plus direct/constant-adjusted instance getters. Its generated-dump lane replays the counted physical body, correlates exactly one `ldfld` with one exact imported field observation, executes through the real memory model, terminates typed-null access in a latched target-exception state, and reproduces the canonical prepared-memory result after reopening and rebinding the dump. CoreCLR remains an outcome oracle, not an input to interpreter shape or dump evidence.
- **Physical scope:** ten source projects contain active contracts or behavior; the two newest projects implement the narrow broker/runner boundary for one-shot external dump queries. The earlier 33 empty placeholders remain removed, and physical boundaries are still justified by executable evidence rather than speculative package maps.
- **Primary progress signal:** executable scenarios and tests, with the design under `docs/` kept just ahead of and consistent with that evidence. This remains prototype evidence, not a production-ready evaluator or interpreter.

The W1 dump-evidence slice is executable against generated full and intentionally sparse dumps. W2's restricted dump-query v1 is complete for its non-cybersecurity scope: typed root states, `Parse`/`Prepare`/`Evaluate(plan)` staging, immutable object-specific plans, exact `String`/`Int32`/`Nullable<Int32>` behavior, stable diagnostics, and all-case same/fresh-session replay are exercised against the generated full dump. [GitHub Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs at exact W2 closure commit `5bed47100`. W1 remains complete for its revised non-security evidence scope: typed exact/partial/unavailable/conflict outcomes, honest answer completeness, stable identity/context/provenance, path-accurate bounds, fresh-session canonical replay, headless execution, truthful topology, and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

W3 hardened implementation checkpoint `19c292f9f` completes the code and local evidence for its deliberately closed
non-cybersecurity architecture proof. Headless local verification passed locked restore; a fifteen-project Release
build with zero warnings and errors; 103 semantic/admission/differential tests; 67 fast integration tests; 5 ordinary
dump tests; 1 optimized-context dump test; and the focused 2-test W3 dump lane, all with zero skips. W3 does not add a
product-facing method evaluator or claim historical execution. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) also passed all four jobs at
the exact implementation commit. [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) subsequently passed all four
required jobs at exact documentation-closure commit `de6cea124`, formally closing W3 for its defined
non-cybersecurity scope.

A versioned malformed-minidump mutation corpus and a Windows x64 one-shot worker are retained as separately landed,
non-gating prototype work outside W1, W2, and W3. The worker's malformed-artifact test passed locally at its checkpoint.
All current W1–W3 test invocations exclude `Scope=Cybersecurity`; the five hostile-corpus facts and ExternalWorker test
project provide no milestone validation. Restore/build intentionally remains repository-wide across all 15 projects,
including the worker projects and IntegrationTests assembly, as topology/compilation-health evidence only—not
cybersecurity behavioral evidence. Their presence does not make external artifacts a supported product input.

A versioned optimized Release modeled-incident report keeps five predeclared axes in the denominator and records raw member bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5. It is explicitly generated evidence, not a representative private-production incident corpus, so no readiness rate is claimed and representative incident measurement is not a W1 completion gate. Current in-process caps (8 GiB dump admission, 256 MiB ClrMD dump cache, and 512 MiB managed PE admission) remain resource controls. Virtual stepping, whole-method abstract analysis, async/dynamic lifting, live speculation, sandbox runtime hosting, and additional product surfaces are **research backlog, not delivery commitments**.

## Where to go next

For structured topic lists, document inventory, and recommended reading paths, start here:

- **Repository-wide design and architecture review:** `DESIGN-ARCHITECTURE-REVIEW.md`
- **Documentation index and TOC-like navigation:** `docs/README.md`
- **Normative W2 language, binding, plan, evidence, and replay contract:** `docs/proposals/architecture/restricted-dump-query-contract-proposal.md`
- **Normative W3 concrete activation, admission, memory, outcome, and replay contract:** `docs/proposals/architecture/concrete-il-execution-contract-proposal.md`

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
- preserve the closed restricted dump-query v1 contract, and extend it only when a concrete incident scenario justifies the next grammar/evidence step;
- preserve W3's structural activation, typed whole-body admission, exact-evidence import, and deterministic outcome boundaries;
- add deterministic, differential, and scenario tests at proven boundaries;
- tighten architecture and documentation when executable evidence changes a decision;
- keep design work just ahead of code rather than expanding speculative surface area.

## License

This repository is licensed under the **MIT-0 (MIT No Attribution)** license. See [`LICENSE`](LICENSE) for the full text.
