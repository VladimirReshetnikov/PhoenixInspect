# Future Work Planning

> **Lifecycle:** Current · **Roadmap:** Active

This is the active delivery plan for the interpreter and dump-time evaluation initiative. It is intentionally evidence-led: each milestone must produce a useful executable scenario or remove a concrete obstacle to the next one.

## 1) Scope lock

The active product target is a **deterministic evaluator grounded in a read-only .NET dump**. W1/W2 provide observed
and derived-query answers; the now-admitted active W4 path adds explicitly counterfactual method evaluation without mutating or
claiming historical execution of the snapshot.

The current proof generates and opens dumps read-only, finds a strongly GCHandle-rooted object through bounded dump
enumeration, validates the handle slot and object-header method table through counted raw-memory reads, and reads a
primitive field, bounded/null strings, metadata, and complete tiny and compiler-emitted fat method bodies from dump
memory. It obtains each MethodDef RVA from counted dump metadata and reads the header, code, locals token, padding, and
declared extra sections from dump memory. W3 projects structural method/signature/local/field shapes from that counted
metadata, admits only a closed branchless `Int32` arithmetic or direct/adjusted getter profile, imports one exact field
observation into persistent memory, and executes the getter through the same domain-parametric machine. CoreCLR and the
full-content-identified disk PE are independent oracles only. These are architectural proofs, not a production method
evaluator.

With W1–W3 closed for their stated scopes, the following remain research backlog rather than delivery commitments until
their own entry gates pass:

- virtual Step Into/Over/Out, undo, and branch exploration,
- whole-method CFG/fixpoint abstract interpretation,
- async/Task and DLR `dynamic` lifting,
- broad BCL projection and semantic-registry systems,
- live speculation, static-analysis products, and no-JIT/sandbox hosting.

Scope expands only through an explicit decision gate backed by executable evidence.

**Current milestone status:** W1 is complete for its revised non-security evidence scope at exact closure commit
`e2580a8a8`. W2 v1 is complete for its non-cybersecurity scope at exact closure commit `5bed47100`; [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs.
Its deliberately narrow contract is the [Restricted Dump Query v1
Contract](../proposals/architecture/restricted-dump-query-contract-proposal.md), and any increment beyond that contract
requires an explicit scope decision. Representative private-production measurement is outside W1, and already-landed
worker/corpus code remains separately scoped non-gating prototype work. W4 is now active and admitted around the
`GetMarkerSummary` workflow below; W4.1's fixture gate, W4.2's unknown E1/E2 kernel, and W4.3's dump-free non-exact
field seam have landed, while W4.4–W4.9 remain admitted work. W3 is complete for its
defined non-cybersecurity architecture-validation scope at exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs.

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

The W1 and W2 rows below and W3's original aggregate forecast preserve planning assumptions for calibration only. They
are superseded by the attributable realized-work ledgers in their milestone sections and must not be read as either
current scope or remaining work. Cybersecurity work is excluded from all three milestones.

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
| W2 | Versioned ten-plus scenario, negative, and complete canonical replay corpus | 500–900 |

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
not revise the original forecast into a post-hoc estimate. Hosted closure required **0 additional implementation LOC**,
and no further W1 implementation package is forecast. Any later defect correction must be estimated and recorded as a
separate package.

‡ Corrective commit `f85545c0c` contains 781 additions and 105 deletions. Against the original W0 baseline, 51 of
those additions replace lines already attributed to earlier W1 packages; cumulative current-diff accounting therefore
assigns 730 LOC to this row and counts every delivered line once.

**Status:** **Complete.** The generated trusted-fixture slice and its checked-in fast/dump lanes are implemented and verified.
It covers fully dump-sourced tiny and compiler-emitted fat method-body facts, metadata-root conflicts, whole-file
disk-artifact identity, typed malformed evidence, foreign-snapshot rejection, Normal-vs-Full sparse memory, explicit result
evidence context, path-accurate actually-applied bounds, honest no-answer completeness, and canonical replay across a
disposed/reopened dump session. Repository-owned headless launch policy and its workflow guard are implemented.

Local verification at `f85545c0c` on 2026-07-14 used only `Invoke-HeadlessProcess` and passed locked restore; a strict
15-project Release build with 0 warnings/errors; 64/64 core tests; 63/63 fast integration tests; 3/3 ordinary dump
tests; and 1/1 optimized-context test, with no skips or UI.

[GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed
all four required jobs at exact closure commit `e2580a8a8`: documentation/headless consistency, the 15-project
zero-warning Release build and fast suites, ordinary real-dump evidence, and optimized-context evidence.

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

**Original forecast (superseded):** 1,700–3,100 LOC

**Realized implementation surface:** 5,301–5,361 LOC

The realized range combines the **1,520–1,580 LOC** attributable to the pre-closure W2 parser, value projection,
initial evaluator, and tests with **3,781 additions** in the dedicated closure stream. The pre-closure range reflects
the remaining attribution uncertainty in mixed files; the dedicated commits are exact path-level diff evidence. Under
this plan's convention, additions and replacement lines are counted once as delivered implementation while deletions
are reported as churn, not added again to the delivered total. Documentation is excluded:

| Landed package | Commit | Diff evidence | Realized attributable LOC |
|---|---|---:|---:|
| Pre-closure closed grammar, evaluator, projection, and tests | Before `09e980502` | Attribution range | 1,520–1,580 |
| Snapshot/owner-bound field descriptors and exact nullable-`Int32` evidence | `572c47781` | +857/−107 | 857 |
| Typed root binding, staged preparation, immutable plan, and single-bind product evaluation | `24f305474` | +1,182/−156 | 1,182 |
| Versioned 22-case complete replay corpus and fixture fields | `bf5642c09` | +489/−0 | 489 |
| Final identity, explanation, descriptor, replay, and local-gate hardening | `ff7cd1965` | +1,306/−59 | 1,253 |
| Normative v1 contract | `09e980502` | Documentation; excluded | 0 |
| **Total** |  | **+3,781/−269 cumulative dedicated closure diff** | **5,301–5,361** |

The final hardening commit's raw diff includes 53 additions that replace lines already counted in earlier closure
commits. Its attributable-current-surface column therefore records 1,253 LOC; the total is the exact cumulative
`.github`/`src`/`tests` diff from `09e980502` through `ff7cd1965`, so each delivered line is counted once.

**Status:** **Complete for the non-cybersecurity W2 v1 scope.** The generated strong-root fixture and dump-free contract
tests pass headlessly at the `ff7cd1965` implementation state. Documentation closure commit `5bed47100` passed all four
required hosted jobs in [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).

The first grammar intentionally supports exactly one exact non-null ordinal root, one direct field through `.`, and
optional bounded null/`Int32`/string coalescing. The staged parse/bind/plan/evaluate pipeline consumes a typed root
binding, selects and freezes the outer field descriptor once, and evaluates the immutable object-specific plan without
repeating member lookup. Broader C# syntax, null-conditional access, and frame roots are not implied. The normative
language, evidence, binding, type, diagnostic, and replay rules are in the
[Restricted Dump Query v1 Contract](../proposals/architecture/restricted-dump-query-contract-proposal.md).

**Goal:** turn W1's evidence operations into the first useful product interaction.

**Scenario**

Evaluate a restricted expression such as `root.OptionalMessage ?? "<missing>"` against a known dump root.

**Deliverables**

- Deterministic parsing of an admitted C# expression subset.
- Typed host root binding that distinguishes one exact object from exhaustive absence, partial, unavailable, conflict,
  and invalid evidence without converting missing evidence to null, while retaining the exact selector, search
  disposition, issue, counters/caps, retained-match state, reads, bounds, and canonical selection provenance.
- Staged binding against the immutable snapshot and dump/metadata universe, with one ordinal outer-field selection and
  explicit snapshot, owner, method-table, and descriptor conflict checks.
- An immutable, object-specific read-only plan for direct field access, exact string/nullable-`Int32`
  observation/coalescing, bounded literals, and an injective canonical v1 identity over the complete selected field
  layout. Invalid/overlapping/overflowing layouts and forged owner descriptors fail before value reads.
- Stable parse, bind, unsupported-syntax, and missing-evidence diagnostics.
- A host-facing result carrying semantic mode, completion, completeness, evidence status, effects, value, and provenance.
- A versioned 22-case/20-expression product corpus that compares the complete canonical result byte sequence and result
  SHA-256 for all cases and, for the 13 cases whose preparation succeeds, the canonical plan projection string and plan
  SHA-256, both within one session and after dump disposal, reopen, root rediscovery, and rebinding.

**Non-goals for W2**

- Compiling a synthetic method or executing user IL.
- Method/getter invocation, overload resolution, construction, reflection, implicit assembly loading, LINQ, or loops.
- IDE completion and polished debugger UI.
- External-input cybersecurity, hostile-artifact policy, isolation, and security validation. They are separately scoped,
  excluded from W2 estimates and completion gates, and are not implied by the closed functional grammar. Every current
  test invocation excludes `Scope=Cybersecurity`; repository-wide restore/build remains topology/compilation-health
  evidence only.

**Exit criteria**

- The versioned 22 cases over 20 distinct expression texts cover direct `Int32`, direct/coalesced
  `Nullable<Int32>`, exact/null/partial strings, selected and unselected compatible fallbacks, `?? null`, exhaustive
  and partial roots, missing/wrong-case/unsupported fields, incompatible coalescing, invalid syntax, unsupported syntax
  including `?.`, and a partial string that is not reclassified as null.
- Product results are `DerivedQuery`; their underlying adapter reads remain `Observation`. No result language implies
  historical or counterfactual execution.
- All 22 corpus cases produce an identical complete canonical result byte sequence and result SHA-256 within one
  session and after reopening the same dump and reconstructing its root binding. The 13 cases whose preparation
  succeeds additionally reproduce an equivalent fresh plan's canonical projection string and plan SHA-256 before
  evaluation.
- The exact pushed documentation-closure commit passes the required hosted jobs. Satisfied by `5bed47100` in [GitHub
  Actions run 29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).

### W3 — Concrete IL semantics and differential oracle

**Original forecast (superseded):** 2,000–3,500 LOC

**Realized dedicated implementation surface:** 8,842 hand-written additions and 1,650 deletions

The exact cumulative diff from normative-contract checkpoint `e7b6a4ace` through hardened implementation checkpoint
`19c292f9f` contains 5,362 production additions/928 deletions and 3,480 test/fixture additions/722 deletions. It also
contains 39 generated package-lock additions required by the dump host's SRM dependency; those generated lines and the
separately committed normative documentation are excluded from the hand-written ledger. Primary implementation commit
`12b6ef942` has a raw `+8,717/-1,655` hand-written diff, and admission-hardening commit `19c292f9f` has a raw
`+134/-4` diff. Nine replacement lines occur in both commit-level raw diffs; the cumulative checkpoint diff counts each
delivered line once and therefore supplies the authoritative `+8,842/-1,650` total. This stream does not reassign the
earlier arithmetic/domain/memory spike already represented in W0's mixed-file attribution range. The original forecast
was therefore materially low and remains visible rather than being rewritten post hoc.

**Status:** **Complete for the defined non-cybersecurity W3 architecture-validation scope.** Hardened implementation
checkpoint `19c292f9f` passed locked
restore; a fifteen-project Release build with zero warnings/errors; Markdown-link and headless-workflow guards; 103
non-cybersecurity unit tests; 67 fast integration tests; 5 ordinary dump tests; 1 optimized-context test; and the
focused 2-test W3 dump lane, all headlessly with zero skips. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at the
exact implementation commit. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), satisfying the final W3 gate.

**Goal:** validate the domain-parametric interpreter on a scenario-derived, closed opcode set.

**Deliverables**

- Structural module, type, MethodDef, and FieldDef identities rather than display-name equality.
- Atomic SRM projection of method body, calling convention, receiver/parameters, return type, and initialized locals;
  contextual FieldDefs resolve separately and are frozen into the admitted plan.
- `ActivateRoot(method, arguments, memory)` with no caller-supplied counts, local values, or `ReturnsValue` flag.
- Frozen typed whole-body plans for the closed E1 arithmetic and E2 direct/constant-adjusted getter profiles.
- An injected persistent-memory capability with distinct allocated defaults and imported missing-field semantics.
- Dump preparation that replays counted physical method evidence and correlates one exact owner, `ldfld`, and Int32 read.
- Deterministic budgets/events, normalized capability failures, and a latched typed-null target-exception boundary.
- Compiler/CoreCLR differential and same/fresh-session canonical replay, including dump disposal/reopen/rebind.

**Exit criteria**

The normative [Concrete IL Execution Contract](../proposals/architecture/concrete-il-execution-contract-proposal.md)
owns the exact behavior. Closure requires all of these executable gates:

1. structural type/method/field identity and cross-module non-aliasing;
2. SRM static/instance, `void`/`Int32`, initialized-local, and FieldDef projection;
3. structured rejection of unsupported signatures, locals, tokens, fields, EH, and opcodes;
4. metadata-derived activation without caller counts, locals, or return disposition;
5. typed whole-body rejection before any supported prefix executes;
6. concrete-domain and persistent-memory laws, including allocated defaults and imported absence;
7. direct and adjusted getters with exactly one real memory load and unchanged memory;
8. CoreCLR agreement for arithmetic, wraparound overflow, getters, and typed-null behavior;
9. same-session and fresh-session canonical replay;
10. generated real-dump execution from counted method, metadata, owner, and field evidence; and
11. the complete local and hosted non-cybersecurity Release/fast/dump/optimized gate.

### W4 — Unknown-aware method evaluation

**Status:** **Active; W4.1–W4.3 are implemented and locally validated, while W4.4–W4.9 remain admitted work.**

**Current projected hand-written implementation surface:** 19,228–25,728 LOC: 7,028 realized through W4.3 plus an
estimated 12,200–18,700 across six remaining non-overlapping, independently valuable slices. The admitted baseline
was 16,860–25,310 LOC. Every remaining slice has an upper estimate of at most 3,500 LOC; the umbrella is not one work
package.

**Goal:** extend the product from read-only derived queries to explicitly counterfactual method evaluation.

The normative behavior is defined by the [Counterfactual Method Evaluation
Contract](../proposals/architecture/counterfactual-method-evaluation-contract-proposal.md). The selected branchless
workflow asks what a strongly rooted generated `DumpProbe` would compute for:

```csharp
GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker)
```

This is useful application-shaped work that W2 cannot express. W2 can bind one exact root and read exactly one direct
field, optionally applying one literal coalescing fallback. It cannot select both `Marker` and `AlternateMarker`, invoke
`GetMarkerSummary` or `CombineMarkers`, or execute the user IL that defines their combination. W3 proves the underlying
single-frame arithmetic/getter seam but deliberately rejects calls and exposes no product method result. W4 therefore
answers a distinct question rather than widening the W2 grammar speculatively.

Pushed W4.1 checkpoint `82363585b` now freezes the exact generated fixture, 18-byte caller/four-byte helper closure,
relational metadata identities and signatures, CoreCLR result, and current W3 first rejection at the second `ldfld`
(offset 7) before the direct `call` at offset 12. Local headless evidence is 4/4 focused, 71/71 complete fast, and 5/5
ordinary dump with zero skips after a locked restore and zero-warning Release build. This is fixture/admission-gap
evidence only; it does not implement unknown continuation, direct-call execution, or the product facade.

Pushed W4.2 checkpoint `e89e43498` adds a second meaningful lifted-flat value domain over the shared W3 handlers.
Policy-enabled execution transports only owned, structurally valid explained `Int32` values; bare top, bottom, foreign
lineage, wrong types, and precision-capability failures remain atomic non-transfers. Semantic lattice operations and
machine equality ignore explanation, while a separate content-addressed immutable DAG records canonical `InputOrigin`
and ordered `BinaryTransform` nodes, embeds exact operands, and replays byte-for-byte in fresh domain and machine
objects. Exact E2 `ldfld` remains executable through the second domain. Partial/unavailable field continuation,
precision-loss events, and `FieldLoadTransform` were intentionally left to W4.3; calls, product results, and
dump-grounded W4 execution remain later work.

For W4.2, headless verification passed the fifteen-project Release build with zero warnings/errors, focused W4.2 53/53, full
unit 156/156, fast integration 71/71, ordinary dump 5/5, and both documentation guards, with zero skips and
`Scope!=Cybersecurity` on behavioral test commands. The checkpoint realizes 3,454 LOC: 3,429 attributable W4.2
implementation LOC (1,521 production plus 1,908 focused tests) and 25 LOC segregating an excluded test scope. Together
W4.1–W4.2 realize 3,932 LOC; the then-remaining W4.3–W4.9 envelope was 14,600–22,200 LOC and the corresponding
projection was 18,532–26,132 LOC.

W4.3 implementation checkpoint `7479b1ad4` adds an immutable, content-equal, versioned `FieldLoadEvidence` contract
for partial/unavailable ordinary instance `Int32` observations and `MemoryLoadResult.FromFieldEvidence` for structured
transport. The shared `ldfld` handler continues only when that evidence matches the frozen field, the explicit
explained-`Int32` policy is enabled, and the value domain implements the optional field-approximation capability.
Exact results remain exact; legacy code-only partial/unavailable results and missing policy/capability block without a
transfer, conflict remains blocked, and invalid or mismatched structured evidence remains invalid. Successful
approximation preserves memory, consumes exactly the successful instruction, and emits `InstructionExecuted` then
`ValuePrecisionLost` at the same `ldfld`. The provenance-aware domain creates a canonical imported-field
`InputOrigin` plus `FieldLoadTransform`, excludes local numeric receiver identity, preserves W4.2 identities, and
replays the same reachable graph in fresh domains.

Strict headless W4.3 verification passed the fifteen-project Release build with zero warnings/errors, focused W4.3
55/55, complete unit 211/211, fast integration 71/71, ordinary dump regression 5/5, optimized dump regression 1/1,
and both Markdown/headless guards, with zero skips; every test command used `Scope!=Cybersecurity`. W4.3 realizes
3,096 LOC (1,100 production LOC plus 1,996 test LOC). W4.1–W4.3 therefore realize 7,028 LOC. This is deliberately dump-free
kernel evidence: no ClrMD non-exact import, counterfactual product facade, or dump-grounded W4 result has landed.

**Admitted work slices**

| Slice | Non-overlapping implementation responsibility | Estimated hand-written implementation LOC | Realized LOC |
|---|---|---:|---:|
| W4.1 — Gate fixture | Add the generated rooted workflow, exact emitted-shape assertions, exact CoreCLR oracle, and one current-W3 whole-body rejection checkpoint. | 350–480 | 478 |
| W4.2 — Unknown E1/E2 kernel | Introduce provenance-bearing unknown continuation for the existing W3 E1/E2 value transfers while preserving structural typing, deterministic events, and no fabricated concrete values. | 3,350–3,500 | 3,454 |
| W4.3 — Non-exact dump field continuation | Import partial/unavailable field values as typed explanatory unknowns while preserving conflict and invalid as distinct non-continuing outcomes. | 2,400–3,500 | 3,096 |
| W4.4 — Direct-call metadata and frozen transitive admission | Resolve the one admitted direct helper call structurally, construct the acyclic dependency graph, calculate required depth, and freeze the complete closure before instruction zero; configurable traversal charging remains W4.8. | 1,700–2,600 | — |
| W4.5 — Multi-frame interpreted execution and call depth | Push/pop interpreted frames at discrete observable boundaries, preserve return-site identity, enforce the prepared maximum logical depth, and record logical/frame high water. | 2,300–3,500 | — |
| W4.6 — Typed pure model, effects, and fallback | Add the scenario-narrowed typed model outcome, normalized effect summary, and explicit blocked/unknown fallback behavior without admitting ambient or arbitrary target execution. | 2,300–3,400 | — |
| W4.7 — Target-outcome stop-on-throw contract | Define the standalone target-outcome/canonical fragment for W3's exact-null latch without admitting a fabricated helper/model throw; handler search and transfer remain excluded. | 1,500–2,500 | — |
| W4.8 — Product canonical facade, runner, and traversal budget | Expose a bounded `CounterfactualExecution` result with assumptions, models, effects, reached bounds, stable diagnostics, and common-projector integration of W4.7's standalone target fragment; add configurable traversal charging to graph preparation without claiming the non-null rooted facade reaches that fragment. | 2,400–3,500 | — |
| W4.9 — Dump corpus, replay, and CI closure | Execute exact and degraded generated-dump cases, compare CoreCLR where applicable, reproduce canonical results after reopen/rebind, and close the headless hosted gate. | 2,000–3,200 | — |

Instruction budget remains the implemented W3 baseline. W4.5 introduces the prepared maximum logical call depth plus
logical/frame high-water reporting; W4.8 adds configurable traversal charging to the already frozen graph discovery.
Allocation is unadmitted by the selected workflow, so no allocation operation consumes a budget and W4 adds no
dormant allocation counter. A later allocation scenario must define the operation, charging point, result behavior, and
tests before an allocation budget becomes an applied contract.

**Entry criteria**

- W1–W3 are closed for their stated scopes. **Satisfied.**
- The selected `GetMarkerSummary` workflow demonstrates method-execution value beyond W2's one-root/one-field query.
  **Satisfied for roadmap admission and fixture evidence by W4.1; product execution belongs to later slices.**
- Effects, deterministic budgets, degraded-evidence behavior, product truth language, and explicit exclusions are fixed
  by the normative W4 contract. **Satisfied for design admission; W4.1–W4.3 implementation evidence is recorded above.**
- Each admitted work slice has an explicit estimate of at most 3,500 hand-written implementation LOC before work begins.
  **Satisfied by the table above.**

**Exit criteria for the umbrella**

- Selected methods tolerate missing inputs without fabricating concrete values.
- Differential and degraded-evidence tests cover every supported opcode/call family.
- Product language consistently describes results as counterfactual, not historical replay.
- The exact pushed closure commit passes all required headless hosted jobs with `Scope!=Cybersecurity`, and the realized
  hand-written implementation LOC ledger is recorded without counting documentation or generated output.
- External-input cybersecurity behavior remains an explicit accepted exclusion, not an unfilled W4 evidence gate.

## 4) Research backlog and entry gates

Research documents remain useful hypotheses. They become delivery work only through these gates:

| Research area | Earliest entry gate |
|---|---|
| Handler-transfer EH | A W4 scenario requires interpreted `catch`/`finally`; define search/unwind behavior just ahead of implementation. |
| Virtual stepping | W4 method execution, deterministic instruction/frame/model events, and stop-on-throw are validated; a later stepping increment then defines pause semantics and source mapping. Debugger-grade Step Out additionally requires handler-transfer EH. |
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

The active W1/W2 product paths use only the first two modes. W3 is an internal architecture proof and exposes no
host-facing method result. W4's admitted product method evaluation must use `CounterfactualExecution`; it cannot relabel
W3 execution as an observation that the target historically ran the method.

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

- W3 admits EH-free bodies only and latches one exact typed-null `ldfld` as a terminal target exception without handler
  transfer or continuation.
- W4.7 must define the retained exact-null target-outcome/canonical fragment for later W4.8 result-projector
  integration. It has no rooted product request or plan identity. The closed non-throwing helper/model cannot fabricate
  a broader exception case, and handler transfer remains separate.
- Full handler search, filters, unwind, `finally`/`fault`, and cross-frame propagation are prerequisites for debugger-grade exception stepping, not implicit refinements.

## 6) Risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope disperses across research subsystems | High | Critical | Scope lock, one active milestone, explicit research gates. |
| A single maintainer cannot sustain a platform-sized surface | High | Critical | Give every active slice an implementation-LOC envelope; split slices above 3,500 LOC; prefer one product path. |
| Maintainer unavailability leaves the active slice without continuity | Medium | Critical | Keep one canonical vertical-slice path, executable fixtures, explicit evidence boundaries, and a current handoff map; avoid private operational knowledge. |
| Optimized dumps omit roots, locals, arguments, or `this` | High | High | Make unavailable/partial expected outcomes; measure scenario recovery rather than guessing. |
| Hostile or malformed artifacts exhaust or compromise the analyzer | Medium | Critical | Keep external-input cybersecurity outside W1–W4 and admit no external artifact product surface through their completion; any future initiative owns its separate requirements and evidence. |
| Documentation volume is mistaken for capability | High | High | Track implementation and validation separately; design just ahead of code. |
| Backend or identity mismatch yields plausible wrong reads | Medium | Critical | Identity validation, conflict outcomes, real-dump fixtures, no silent fallback. |
| The evaluator does not materially improve incident workflows | Medium | High | Keep W4 tied to the exact two-field `GetMarkerSummary` question; require the gate fixture, CoreCLR comparison, dump corpus, and product result to demonstrate value beyond W2 before claiming the admitted design is useful. W3 alone remains architecture validation. |

## 7) Decision gates

W2 decisions now applied:

1. The supported subset is a project-owned bounded parser for one root/field and optional literal coalescing.
2. Project-owned immutable parse/query shapes stay internal; no Roslyn object enters a core contract.
3. Diagnostics use stable payload-safe text, value display is redacted, and canonical replay is explicitly not telemetry-safe.
4. Typed root binding and an immutable, snapshot/object-specific plan make parse, bind, plan, and evaluate distinct stages;
   outer-member selection occurs once during preparation.
5. The first value domain admits exact/null `Nullable<Int32>` and string semantics; missing or partial evidence never
   selects a coalescing fallback.
6. Every case in the versioned corpus, not one representative expression, must replay canonically after reopening and
   rebinding the dump.

W3 decisions now applied:

1. Select fixtures first, then derive the closed opcode set.
2. Use SRM to project structural signatures, locals, and FieldDefs from one immutable evidence source.
3. Reject the complete typed body—including unsupported IL, EH, and non-E1/E2 shapes—before instruction zero.
4. Import only exact dump owner/field evidence into persistent memory and keep the machine independent of the live dump session.

W4 admission decisions now applied:

1. The first product method question is the branchless rooted `DumpProbe.GetMarkerSummary()` workflow. It is not
   expressible through W2 because its answer depends on two fields, user-method interpretation, and one direct helper
   call rather than one preselected direct field and optional literal fallback.
2. The normative counterfactual-method contract defines unknown continuation, effects, deterministic budget charging,
   degraded evidence, target exceptions, canonical product results, and historical-replay exclusions before code lands.
3. The nine admitted work slices in the W4 section are non-overlapping hand-written implementation-LOC envelopes, each with
   an upper estimate no larger than 3,500 LOC. Realized additions and material rewrites will be recorded once at closure.
4. Instruction and traversal units are consumed only by their defined operations. Maximum logical call depth is a
   preparation-time bound with execution high-water reporting, not a consumable unit. Allocation remains unadmitted
   and has no dormant counter.
5. W4 retains `Scope!=Cybersecurity` in every required test command. Malformed-artifact and external-worker behavior is
   neither a W4 deliverable nor W4 validation.
6. W4.2 keeps explanation outside lattice and machine semantic equality. Execution distinguishes exact, explained
   unknown, and ungrounded top only through an optional precision capability and admits explained values only under an
   explicit policy.
7. W4.2 owns `InputOrigin` and ordered `BinaryTransform` lineage. W4.3 adds structured non-exact field continuation,
   precision-loss events, and `FieldLoadTransform` through the same generic `ldfld` handler while preserving exact
   behavior. Direct calls, product projection, ClrMD non-exact import, and dump-grounded W4 results remain later work.

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
