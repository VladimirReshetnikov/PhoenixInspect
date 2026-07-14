# Future Work Planning

> **Lifecycle:** Current · **Roadmap:** Active

This is the active delivery plan for the interpreter and dump-time evaluation initiative. It is intentionally evidence-led: each milestone must produce a useful executable scenario or remove a concrete obstacle to the next one.

## 1) Scope lock

The only active product target is a **deterministic, read-only expression evaluator grounded in a .NET dump**.

The current proof generates and opens dumps read-only, finds a strongly GCHandle-rooted object through bounded dump enumeration, validates the handle slot and object-header method table through counted raw-memory reads, and reads a primitive field, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump memory. It obtains each MethodDef RVA from counted dump metadata and reads the header, code, locals token, padding, and declared extra sections from dump memory; the tiny exact dump-sourced `ret` is executable, while the fat body proves locals and two EH regions without claiming interpreter support for them. The full-content-identified disk PE is only an independent oracle. Separately, a concrete branchless `Int32` kernel is checked against compiler-emitted methods running on CoreCLR. These are architectural proofs, not a production evaluator.

Until W1 and W2 pass their exit criteria, the following remain research backlog rather than delivery commitments:

- virtual Step Into/Over/Out, undo, and branch exploration,
- whole-method CFG/fixpoint abstract interpretation,
- async/Task and DLR `dynamic` lifting,
- broad BCL projection and semantic-registry systems,
- live speculation, static-analysis products, and no-JIT/sandbox hosting.

Scope expands only through an explicit decision gate backed by executable evidence.

**Current active milestone:** close the non-security W1 evidence slice on exact-HEAD hosted CI. W0, the bounded W2
root-field query, and the required W1 implementation/local gates are present in-tree. External-input cybersecurity and
representative private-production measurement are outside W1; already-landed worker/corpus code is retained only as
non-gating prototype work. The concrete W3 work is a bounded architecture-risk spike, W3 remains incomplete, and W4
remains gated.

## 2) LOC sizing and work-in-progress

Planning estimates use ranges of **hand-written implementation LOC**. They include production code, tests, fixtures,
harnesses, scripts, and material CI/configuration logic. They exclude documentation, generated output, package lock files,
solution-file churn, vendored snapshots, and deletion-only scaffold removal. Count additions and materially rewritten lines
once rather than treating raw diff churn as delivered scope.

LOC ranges are implementation-surface envelopes, not productivity or schedule forecasts. Record the realized count when a
milestone closes and recalibrate later ranges from that evidence. Split any independently deliverable work package whose
upper estimate exceeds 3,500 LOC.

Work-in-progress limit:

1. Keep one delivery milestone active at a time.
2. Do not add a subsystem proposal unless the active milestone needs its contract.
3. Do not create an empty project to reserve a hypothetical boundary.
4. A planned test is not validation; only a running artifact counts as evidence.

### Work-package LOC estimates for the funded path

The ranges make large milestones decomposable and expose scope growth without converting uncertain architecture work into
a calendar forecast. A milestone total is the sum of its non-overlapping package ranges.

The W1 rows below preserve the original planning assumptions for calibration only. They are superseded by the
attributable realized-work ledger in the W1 section and must not be read as either current scope or remaining work.
Cybersecurity portions of the original third row are no longer W1 requirements.

| Milestone | Work package | Estimated implementation LOC |
|---|---|---:|
| W0 | Collapse speculative scaffolding; reconcile scope, status, and backend decisions | 100–250 |
| W0 | Pin/lock the toolchain and add build, fast, dump, and deterministic-replay gates | 250–500 |
| W0 | Repair identity, machine-status, lattice, persistence, and admission contracts exposed by the walking skeleton | 700–1,200 |
| W1 | Snapshot identity, read-only lifetime, exact/partial raw-memory reader, and module catalog | 700–1,100 |
| W1 | Bounded root/object/field/string/metadata/IL evidence path | 1,000–1,600 |
| W1 | Identity-conflict, sparse/corrupt-input, provenance, redaction, and supported-runner hardening | 900–1,500 |
| W2 | Closed grammar and deterministic parse/admission diagnostics | 350–600 |
| W2 | Root/member binder and immutable read-only query plan | 350–700 |
| W2 | Bounded evaluator plus honest result/provenance envelope | 500–900 |
| W2 | Ten-plus scenario, negative, replay, and security-policy fixtures | 500–900 |

## 3) Evidence-led roadmap

### W0 — Truthful baseline and fast feedback

**Estimated implementation surface:** 1,050–1,950 LOC

**Realized implementation surface:** 2,502–5,304 LOC

The realized range is deliberately bounded rather than reduced to a false point count. In reset commit `c35f1e97a`,
1,605 changed LOC are in files exclusively attributable to W0's toolchain, identity, status, persistence, lattice,
and contract/test work. Another 2,802 changed LOC are in mixed W0–W3 files such as the machine, frame/body helpers,
common result envelope, harness, and machine tests; the history cannot defensibly assign those lines more narrowly.
Dedicated W0 closure commits `24d860678` and `3ece32a36` add 897 attributable LOC after their combined diff counts
the one line rewritten by both commits only once. The lower bound excludes every
mixed line and the upper bound includes all of them. Counts use the larger of additions or deletions per materially
changed hand-written file, while excluding documentation, generated output, locks, solution churn, and deletion-only
scaffolding as required by this plan's convention. Even the lower bound exceeded the estimate, so future milestone
ranges must be recalibrated from this evidence and kept attributable through dedicated commits or a path manifest.

**Status:** **Complete.** Implementation and local verification completed on 2026-07-13. All service-side W0 gates
passed for exact pushed commit `3ece32a36eccc06a61025b1b35b58c09f6e4ed09` in
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
completed 2026-07-14 UTC (2026-07-13 PDT): documentation consistency passed; the build/fast job passed locked
restore, a zero-warning Release build, 60 semantic/differential tests, and 40 fast adapter/harness tests; and the
dependent real-dump job passed 3 dump tests.

**Goal:** make the existing proof repeatable and ensure repository claims match executable behavior.

**Deliverables**

- Minimal CI that builds and runs the fast test set.
- Determinism smoke coverage for the implemented micro-step.
- Corrections for unstable identity, false instruction-executed events, and integration-harness failure behavior.
- One documented mapping between machine execution status, future session pause reasons, diagnostics, and adapter miss reasons; do not force them into one enum.
- Scope, roadmap, and traceability documents that distinguish design from implementation and validation.

**Exit criteria**

- A clean checkout builds and the fast tests pass in CI.
- Repeated execution over the same input produces the same serialized observable outcome.
- The walking-skeleton documentation states exactly what comes from the dump and what comes from the on-disk PE.

### W1 — Real dump-evidence slice

**Original forecast (superseded):** 2,600–4,200 LOC across the independently deliverable packages above

**Realized architecture-review implementation surface through `f85545c0c`:** 7,423 hand-written additions and 89
deletions. The exact cumulative diff from the preceding W0 evidence commit `7c6bf91f2` is 7,719 additions and 98
deletions. Classify generated churn first: five package-lock files contribute 236 additions/9 deletions and
`Interpreter.sln` contributes 60 additions/0 deletions. No documentation or generated `bin`/`obj` output is included.
The remaining paths are hand-written implementation/test/harness/configuration work. Dedicated package commits make the
additions attributable without assigning the same line to two packages:

| Landed package | W1 relation | Realized attributable LOC |
|---|---|---:|
| Explicit-DAC session seam and corrupt backend normalization | W1 dump-session/evidence behavior | 344 |
| Versioned malformed-minidump mutation corpus and fast contract tests | Non-gating prototype work outside W1 | 1,153 |
| Compiler-emitted fat method-body dump-evidence proof | W1 evidence | 266 |
| Repository-wide managed headless launch controls | W1 evidence | 212 |
| Explicit result evidence context and canonical replay | W1 evidence | 813 |
| Versioned optimized modeled-incident measurement | W1 generated modeled-context evidence; representative production measurement is non-gating | 908 |
| Canonical runtime-module identity projection coverage | W1 evidence | 20 |
| One-shot AppContainer broker, runner, protocol, and containment tests | Non-gating prototype work outside W1 | 2,889 |
| Headless solution/CI wiring and workflow guard | W1 evidence; solution-file churn excluded | 88† |
| Path-accurate bounds, no-answer completeness, and fresh-session replay | W1 evidence | 730‡ |
| **Current total** |  | **7,423** |

† The final CI package introduced 23 workflow lines and 66 guard-script lines; one workflow line superseded a line
already attributed to the earlier headless package. Cumulative current-diff accounting therefore assigns 88 LOC to
this row instead of double-counting the replaced line.

This total records the full review implementation stream, including separately landed non-gating prototypes; it is not
a claim that every row remains part of W1. After generated files are removed, the disjoint malformed-corpus and
external-worker paths contribute 4,042 additions/0 deletions (1,153 + 2,889) as now-non-gating security prototypes.
The remaining **3,381 additions/89 deletions are attributable to active non-security W1**. Do
not revise the original forecast into a post-hoc estimate. No further W1 implementation package is currently forecast:
**0 additional implementation LOC** is planned for the remaining hosted verification step. If that run exposes a
defect, estimate and record the resulting corrective package separately.

‡ Corrective commit `f85545c0c` contains 781 additions and 105 deletions. Against the original W0 baseline, 51 of
those additions replace lines already attributed to earlier W1 packages; cumulative current-diff accounting therefore
assigns 730 LOC to this row and counts every delivered line once.

**Status:** the generated trusted-fixture slice and its checked-in fast/dump lanes are implemented and locally verified.
It covers fully dump-sourced tiny and compiler-emitted fat method-body facts, metadata-root conflicts, whole-file
disk-artifact identity, typed malformed evidence, foreign-snapshot rejection, Normal-vs-Full sparse memory, explicit result
evidence context, path-accurate actually-applied bounds, honest no-answer completeness, and canonical replay across a
disposed/reopened dump session. Repository-owned headless launch policy and its workflow guard are implemented. Hosted
CI on the exact current commit remains the only W1 closure gate.

Local verification at `f85545c0c` on 2026-07-14 used only `Invoke-HeadlessProcess` and passed locked restore; a strict
15-project Release build with 0 warnings/errors; 64/64 core tests; 63/63 fast integration tests; 3/3 ordinary dump
tests; and 1/1 optimized-context test, with no skips or UI.

[GitHub Actions run 29352271781](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29352271781) passed
all four revised-scope jobs at exact semantic corrective commit `f85545c0c`: documentation/headless consistency, the
15-project zero-warning Release build and fast suites, ordinary real-dump evidence, and optimized-context evidence. It
is the strongest hosted implementation baseline, but this documentation advances HEAD; a post-documentation exact-HEAD
run is still required.

The checked-in malformed-minidump corpus and one-shot Windows worker remain useful separately landed evidence, but they
are not W1 completion requirements. The optimized generated modeled-incident report is active W1 context evidence;
representative private-production measurement remains a later, non-gating product-readiness question. External-input
cybersecurity is explicitly outside W1.

**Goal:** prove the product's highest-risk evidence path before expanding IL semantics.

**Scenario**

Given a generated dump containing a known object graph, locate a root and read a primitive field plus a bounded string from dump memory.

**Deliverables**

- Dump-backed object/type/field/string reads with explicit bounds.
- Canonical runtime-to-metadata identity for the scenario.
- Separate identities for the counted dump metadata root (MVID, metadata length, metadata SHA-256) and a complete disk artifact (whole-file length and SHA-256); metadata agreement never authenticates disk body bytes as dump evidence.
- Dump-backed MethodDef RVA, tiny/fat header, code, local-signature token, and declared extra-section reads; a normalized body is available only when all required evidence is exact.
- Typed evidence outcomes such as exact, partial, unavailable, conflict, and invalid, each with provenance and a stable miss reason.
- Backend-neutral result context carrying evidence source, explicit snapshot/module identity availability, fallback,
  and only the deterministic bounds whose guarded operation was actually reached, all included in canonical replay.
- Projection rules that keep a retained partial observation wrapper/provenance distinct from a decoded scalar answer.
- Tests for sparse/unreadable memory, invalid addresses, truncation, and identity mismatch.
- Fresh-session replay that disposes/reopens the dump, rediscovers module/root identity, and compares complete canonical
  result bytes and fingerprints.
- Repository-wide headless test launch policy and a workflow guard that rejects unwrapped `dotnet` test entry points.

**Exit criteria**

- The scenario returns the expected field/string values from dump memory without executing user IL.
- Missing or corrupt evidence produces a deterministic non-success outcome rather than an exception or guess.
- The result identifies evidence source, completeness, and every fallback used.
- A retained partial observation with no decoded value reports no answer while preserving its evidence and provenance.
- Canonical replay includes stable identity, result context, provenance, and every actually applied deterministic bound,
  and remains byte-identical after reopening the same dump in a fresh session.
- Every managed test/CI launch is headless, and the exact pushed W1 commit passes its required hosted jobs.

External-input cybersecurity is explicitly excluded from W1. The existing mutation corpus and worker are non-gating
prototype work, and W1 completion by itself does not admit an external artifact product surface.

### W2 — Restricted expression/query slice

**Estimated implementation surface:** 1,700–3,100 LOC

**Status:** implemented and locally verified for the generated strong-root fixture. The first grammar intentionally supports exactly one exact non-null ordinal root, one direct field through `.`, and optional bounded null/`Int32`/string coalescing; broader C# syntax, null-conditional access, and frame roots are not implied.

**Goal:** turn W1's evidence operations into the first useful product interaction.

**Scenario**

Evaluate a restricted expression such as `root.OptionalMessage ?? "<missing>"` against a known dump root.

**Deliverables**

- Deterministic parsing of an admitted C# expression subset.
- Binding against host-provided roots and the dump/metadata universe.
- A read-only query plan for direct field access, exact nullable-field observation/coalescing, and bounded literals.
- Stable parse, bind, unsupported-syntax, and missing-evidence diagnostics.
- A host-facing result carrying semantic mode, completion, completeness, evidence status, effects, value, and provenance.

**Non-goals for W2**

- Compiling a synthetic method or executing user IL.
- Method/getter invocation, overload resolution, construction, reflection, implicit assembly loading, LINQ, or loops.
- IDE completion and polished debugger UI.

**Exit criteria**

- At least ten scenario expressions cover success, exact null/coalescing, unavailable roots, invalid syntax, unsupported syntax (including `?.`), and partial evidence.
- Results are classified as `Observation` or `DerivedQuery`; no result language implies historical or counterfactual execution.
- Repeated runs produce identical values and machine-readable explanations.

### W3 — Concrete IL semantics and differential oracle

**Estimated implementation surface:** 2,000–3,500 LOC

**Status:** bounded architecture-risk spike partially implemented (concrete domain, persistent memory tests, I4 arithmetic kernel, whole-body admission, and differential oracle). W3 is not the active delivery milestone and its signature/memory-opcode requirements remain open.

**Goal:** validate the domain-parametric interpreter on a scenario-derived, closed opcode set.

**Deliverables**

- A concrete value domain and memory model exercised by real opcode handlers.
- The minimal opcode closure required by selected branchless, EH-free arithmetic and field-read getters.
- Metadata-projected argument, return, and local stack shapes; frame admission must not trust caller-supplied counts or `ReturnsValue` for untrusted methods.
- An admission check that rejects bodies outside the supported opcode/EH tier before execution.
- A differential harness that runs tiny methods on CoreCLR and the interpreter and compares outcomes.
- Deterministic budgets and event semantics for every admitted instruction.

**Exit criteria**

- The same opcode handlers execute through the intended domain seam rather than test-only shortcuts.
- Differential fixtures agree with CoreCLR on the admitted subset, including documented exceptional boundaries.
- Unsupported bodies are rejected with an explicit reason and no partial execution.

### W4 — Unknown-aware method evaluation

**Estimated implementation surface:** more than 3,500 LOC for the umbrella; decompose into independently valuable slices
of at most 3,500 LOC each

**Goal:** extend the product from read-only derived queries to explicitly counterfactual method evaluation.

**Candidate slices**

- Provenance-bearing unknown values over the W3 opcode set.
- Restricted calls and effects with typed model outcomes.
- Deterministic instruction, call-depth, allocation, and traversal budgets.
- Stop-on-throw behavior. Handler transfer is not part of the first W4 slice.
- Host results classified as `CounterfactualExecution`, with assumptions and models visible.

**Entry criteria**

- W1–W3 exit criteria pass in CI.
- Real scenarios demonstrate that method execution adds value beyond W2 queries.
- Each W4 slice has an explicit estimate of at most 3,500 implementation LOC before work begins.

**Exit criteria for the umbrella**

- Selected methods tolerate missing inputs without fabricating concrete values.
- Differential and degraded-evidence tests cover every supported opcode/call family.
- Product language consistently describes results as counterfactual, not historical replay.

## 4) Research backlog and entry gates

Research documents remain useful hypotheses. They become delivery work only through these gates:

| Research area | Earliest entry gate |
|---|---|
| Handler-transfer EH | A W4 scenario requires interpreted `catch`/`finally`; define search/unwind behavior just ahead of implementation. |
| Virtual stepping | W4 method execution, deterministic pause/event contracts, source mapping, and stop-on-throw are validated. Debugger-grade Step Out additionally requires handler-transfer EH. |
| Async/Task lifting | The interpreter supports the scenario-derived `MoveNext` opcode closure, generics, calls, and required EH behavior. |
| Dynamic dispatch lifting | Direct call/binding behavior is validated and a concrete dump scenario justifies DLR-specific work. |
| CFG/fixpoint abstract analysis | At least two value domains share the same meaningful opcode semantics; lattice order and convergence laws are executable tests. |
| Broad semantic registry/projections | Two independently implemented models demonstrate a repeated extension problem. |
| Additional products | A second consumer proves reuse against the implemented core rather than the package diagram. |

Exact shared-generic dictionary decoding remains a version-pinned research spike. The baseline is public `MethodTable`-anchored recovery where available plus typed-unknown fallback.

## 5) Cross-cutting contracts for active work

### Semantic mode

Every host-facing result identifies one of:

- `Observation`: decoded directly from snapshot evidence.
- `DerivedQuery`: computed over observed evidence without user-IL execution.
- `CounterfactualExecution`: interpreted from recovered or assumed state under explicit policy.
- `AbstractAnalysis`: may/must reasoning over possible states.

The active W1–W2 work uses only the first two modes.

### Result honesty

Keep these axes separate:

- semantic mode,
- completion status,
- completeness,
- evidence status,
- effects/virtual writes,
- provenance and diagnostics.

A UI trust badge may summarize them but never replaces them in contracts or tests.

### Exception handling

- W3 admits EH-free bodies only.
- The first W4 exception behavior is stop-on-throw without handler transfer.
- Full handler search, filters, unwind, `finally`/`fault`, and cross-frame propagation are prerequisites for debugger-grade exception stepping, not implicit refinements.

## 6) Risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope disperses across research subsystems | High | Critical | Scope lock, one active milestone, explicit research gates. |
| A single maintainer cannot sustain a platform-sized surface | High | Critical | Give every active slice an implementation-LOC envelope; split slices above 3,500 LOC; prefer one product path. |
| Maintainer unavailability leaves the active slice without continuity | Medium | Critical | Keep one canonical vertical-slice path, executable fixtures, explicit evidence boundaries, and a current handoff map; avoid private operational knowledge. |
| Optimized dumps omit roots, locals, arguments, or `this` | High | High | Make unavailable/partial expected outcomes; measure scenario recovery rather than guessing. |
| Hostile or malformed artifacts exhaust or compromise the analyzer | Medium | Critical | Keep external-input cybersecurity outside W1 and admit no external artifact product surface through W1 completion; any future initiative owns its separate requirements and evidence. |
| Documentation volume is mistaken for capability | High | High | Track implementation and validation separately; design just ahead of code. |
| Backend or identity mismatch yields plausible wrong reads | Medium | Critical | Identity validation, conflict outcomes, real-dump fixtures, no silent fallback. |
| The evaluator does not materially improve incident workflows | Medium | High | Test W1/W2 against concrete user questions before funding method execution. |

## 7) Decision gates

W2 decisions now applied:

1. The supported subset is a project-owned bounded parser for one root/field and optional literal coalescing.
2. Project-owned immutable parse/query shapes stay internal; no Roslyn object enters a core contract.
3. Diagnostics use stable payload-safe text, value display is redacted, and canonical replay is explicitly not telemetry-safe.

Before starting W3:

1. Select fixtures first, then derive the closed opcode set.
2. Confirm the active metadata backend from executable evidence.
3. Define the admission check for unsupported IL and EH.

Before starting any W4 slice:

1. Demonstrate user value not achievable through W2's read-only query plan.
2. Define its effects, budget, and degraded-evidence behavior.
3. Give it an explicit implementation estimate no larger than 3,500 LOC.

### Optimized-dump recoverability measurement

This is a supporting product-readiness measurement, not a W1 completion gate.

The generated strong-handle fixture proves decoding, not incident recoverability, and therefore contributes no
percentage to a frame-context claim. The first checked-in measurement now uses an explicitly versioned generated
optimized Release modeled-incident dump and records, for every predeclared expression scenario:

1. the target/runtime/build profile and capture mechanism;
2. whether the required root, `this`, argument, local, and member bytes are present independently;
3. whether selection is unique, ambiguous, partial, unavailable, conflicting, or invalid;
4. the exact query result axes and stable diagnostic code; and
5. the numerator and denominator for each context kind, with unsupported scenarios retained in the denominator.

Its canonical v1 report retains `this`, argument, local, static, and strong-root axes and records raw member bytes at
5/5, attributable context at 1/5, and product-query availability at 1/5. The exact attributable/product result is the
strong root. Stack-slot observation for `this`, argument, and local is deliberately not admitted under the pinned
.NET 10 DAC safety boundary, and static attribution remains unavailable. These are raw counts from one generated
modeled dump, not a representative private-production corpus and not a production recoverability rate.

Continue to report raw counts and corpus composition before any aggregate percentage. Do not extrapolate from the
modeled fixture to production incidents, and do not set a readiness threshold until a representative
private-production incident corpus exists. Until then, optimized frame-context support remains unavailable outside the
validated strong-root path even though the measurement machinery itself is implemented and versioned.

## 8) Documentation policy

- Update this plan when executable evidence changes sequencing or scope.
- Keep current implementation facts in the root README and prototype-status documents; link rather than duplicating volatile counts.
- Mark research documents clearly, but do not expand them merely to make their taxonomies agree.
- Prefer a short contract plus a running fixture over another comprehensive up-front specification.
