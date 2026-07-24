# Future Work Planning

> **Lifecycle:** Current · **Roadmap:** Supporting

This is the authoritative W0–W4 delivery record and research-entry-gate source for the interpreter and dump-time
evaluation initiative. The completed W5 sequence is the [Post-W4 Path Forward](post-w4-path-forward.md), and the
completed W6 sequence is the [Post-W5 Path Forward](post-w5-path-forward.md). The completed W7 sequence is the
[Post-W6 Path Forward](post-w6-path-forward.md). The active W8 sequence is the
[Post-W7 Path Forward](post-w7-path-forward.md); W8.1 is complete under its
[physical-truth disposition](w8-1-physical-truth-disposition.md), and W8.2 is active. All six documents remain
evidence-led: each admitted milestone must
produce a useful executable scenario or remove a concrete obstacle to the next one.

## 1) Scope lock

The active product target is a **deterministic evaluator grounded in a read-only .NET dump**. W1/W2 provide observed
and derived-query answers; closed W4 adds explicitly counterfactual method evaluation without mutating or claiming
historical execution of the snapshot. W5's closed expression-to-result composition and usefulness gate live in the
[Post-W4 Path Forward](post-w4-path-forward.md). The closed opt-in W6 bounded member-chain slice and its local
validation record live in the [Post-W5 Path Forward](post-w5-path-forward.md). Closed W7 adds one explicit
`StaticFieldExpressionV1` tree-admission/binding profile: fully qualified ordinary static fields bind from counted
metadata without frame/PDB context, while a bounded selected-frame/Portable-PDB slice supplies current namespace,
namespace imports, and simple aliases for `Type.Field`. Roslyn remains the sole complete expression parser, and a
reference-valued static reuses the unchanged W2/W6 suffix evaluator.

Active W8 defines an additive `StaticFieldExpressionV2` design over the same parser and stored/literal value paths.
It adds bounded nested and closed constructed generic owners, exact scoped import/type/namespace/extern aliases,
current-type and evidence-qualified `using static` bare fields, general TypeSpec construction, constraints,
accessibility, field-signature substitution, constructed assignability, per-construction runtime slots, and metadata
literals. W8.1 now supplies exact pre-contract compiler/PDB, constructed-runtime, thread-relative, module-RVA, literal,
memory-homed frame-root, and assignability evidence. Context-relative storage and selected-frame generic arguments are
typed non-admitted, and register homes are unproven. W8.2 owns the V2/frame product contracts. Its immutable common
expression foundation, detached frame-value syntax, shared bounded signature projection, and caller-supplied
selected-method lexical envelope have landed; remaining contract families and all binding/evaluation behavior remain
roadmap intent until their named checkpoints land.

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
- live speculation, static-analysis products, and no-JIT/no-JIT hosting.

Scope expands only through an explicit decision gate backed by executable evidence.

**Current milestone status:** W1 is complete for its revised milestone-selected evidence scope at exact closure commit
`e2580a8a8`. W2 v1 is complete for its milestone-selected scope at exact closure commit `5bed47100`; [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29364905178) passed all four required jobs.
Its deliberately narrow contract is the [Restricted Dump Query v1
Contract](../proposals/architecture/restricted-dump-query-contract-proposal.md), and any increment beyond that contract
requires an explicit scope decision. Representative private-production measurement is outside W1. W4 was admitted around the
`GetMarkerSummary` workflow below; W4.1's fixture gate, W4.2's unknown E1/E2 kernel, W4.3's dump-free non-exact
field seam, W4.4's body-free call resolution/frozen graph, W4.5's exact plus explained-unknown prepared-graph
execution, and W4.6's structural model admission, lineage, machine transfer, and compiler/SRM conformance have landed,
W4.7's issuer-certified standalone target-outcome projection/compiler replay, W4.8's complete canonical product
runner, and W4.9's detached ClrMD generated-dump corpus have landed. W4 is complete at exact closure commit
`a819a08fd9ccdf926620c505732475990b242be9`; [GitHub Actions run
29463426083](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463426083) passed all four jobs. Final
documentation-closure commit `aaec73c5b987089addb539d3628de67bd815bd8f` passed the same matrix in [run
29463847230](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463847230). W3 is complete for its defined milestone-selected architecture-validation scope
at exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237) passed all four required jobs.

## 2) LOC sizing and work-in-progress

New planning uses logarithmic orders of magnitude for **hand-written changed LOC**: `~100 LOC`, `~1K LOC`, `~10K LOC`,
`~100K LOC`, and so on. A `~1K LOC` band covers roughly 320–3,000 LOC. The bands include production code, tests,
fixtures, harnesses, scripts, material CI/configuration logic, and authored documentation. They exclude generated
output, package lock files, mechanical solution-file churn, vendored snapshots, and deletion-only scaffold removal.
Count additions and materially rewritten lines once rather than treating raw diff churn as delivered scope.

These bands describe changed surface, not duration or productivity. Revise past, current, and remaining bands whenever
implementation or documentation work exposes a different order of magnitude. The exact ranges and realized ledgers below are
preserved historical calibration and do not define the sizing policy for W8 or subsequent work.

Work-in-progress limit:

1. Keep one delivery milestone active at a time.
2. Do not add a subsystem proposal unless the active milestone needs its contract.
3. Do not create an empty project to reserve a hypothetical boundary.
4. A planned test is not validation; only a running artifact counts as evidence.

### Historical work-package LOC records for the closed path

The ranges below preserve the planning convention used when W0–W4 were active. New milestones use the logarithmic
bands above; historical values are not rewritten post hoc.

The W1 and W2 rows below and W3's original aggregate forecast preserve planning assumptions for calibration only. They
are superseded by the attributable realized-work ledgers in their milestone sections and must not be read as either
current scope or remaining work. Broader input-shape work is excluded from all three milestones.

| Milestone | Work package | Estimated implementation LOC |
|---|---|---:|
| W0 | Collapse speculative scaffolding; reconcile scope, status, and backend decisions | 100–250 |
| W0 | Pin/lock the toolchain and add build, fast, dump, and deterministic-replay gates | 250–500 |
| W0 | Repair identity, machine-status, lattice, persistence, and admission contracts exposed by the walking skeleton | 700–1,200 |
| W1 | Snapshot identity, read-only lifetime, exact/partial raw-memory reader, and module catalog | 700–1,100 |
| W1 | Bounded root/object/field/string/metadata/IL evidence path | 1,000–1,600 |
| W1 | Identity-conflict, sparse-input, provenance, artifact-text omission, and headless-runner strengthening | 900–1,500 |
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
[GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29309374548),
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
`PhoenixInspect.sln` contributes 60 additions/0 deletions. No documentation or generated `bin`/`obj` output is included.
The remaining paths are hand-written implementation/test/harness/configuration work. Dedicated package commits make the
additions attributable without assigning the same line to two packages:

| Landed package | W1 relation | Realized attributable LOC |
|---|---|---:|
| Dump-session opener and backend-failure normalization | W1 dump-session/evidence behavior | 344 |
| Compiler-emitted fat method-body dump-evidence proof | W1 evidence | 266 |
| Repository-wide managed headless launch controls | W1 evidence | 212 |
| Explicit result evidence context and canonical replay | W1 evidence | 813 |
| Versioned optimized modeled-incident measurement | W1 generated modeled-context evidence; representative production measurement is non-gating | 908 |
| Canonical runtime-module identity projection coverage | W1 evidence | 20 |
| Headless solution/CI wiring and workflow guard | W1 evidence; solution-file churn excluded | 88† |
| Path-accurate bounds, no-answer completeness, and fresh-session replay | W1 evidence | 730‡ |
| **Current total** |  | **7,423** |

† The final CI package introduced 23 workflow lines and 66 guard-script lines; one workflow line superseded a line
already attributed to the earlier headless package. Cumulative current-diff accounting therefore assigns 88 LOC to
this row instead of double-counting the replaced line.

This total records the historical review implementation stream, including 4,042 LOC of experiments that have since
been removed. After generated files and those deleted experiments are excluded, **3,381 additions/89 deletions are
attributable to active W1**. Do
not revise the original forecast into a post-hoc estimate. Hosted closure required **0 additional implementation LOC**,
and no further W1 implementation package is forecast. Any later defect correction must be estimated and recorded as a
separate package.

‡ Corrective commit `f85545c0c` contains 781 additions and 105 deletions. Against the original W0 baseline, 51 of
those additions replace lines already attributed to earlier W1 packages; cumulative current-diff accounting therefore
assigns 730 LOC to this row and counts every delivered line once.

**Status:** **Complete.** The generated named-fixture slice and its checked-in fast/dump lanes are implemented and verified.
It covers fully dump-sourced tiny and compiler-emitted fat method-body facts, metadata-root conflicts, whole-file
disk-artifact identity, typed invalid evidence, foreign-snapshot rejection, Normal-vs-Full sparse memory, explicit result
evidence context, path-accurate actually-applied bounds, honest no-answer completeness, and canonical replay across a
disposed/reopened dump session. Repository-owned headless launch policy and its workflow guard are implemented.

Local verification at `f85545c0c` on 2026-07-14 used only `Invoke-HeadlessProcess` and passed locked restore; a strict
15-project Release build with 0 warnings/errors; 64/64 core tests; 63/63 fast integration tests; 3/3 ordinary dump
tests; and 1/1 optimized-context test, with no skips or UI.

[GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29353198889) passed
all four required jobs at exact closure commit `e2580a8a8`: documentation/headless consistency, the 15-project
zero-warning Release build and fast suites, ordinary real-dump evidence, and optimized-context evidence.

Earlier out-of-scope experiments have been removed. Caveat: W1 evidence covers only the named
generated fixtures and explicitly admitted input shapes; no behavior is claimed for other artifact shapes. The
optimized generated modeled-incident report remains active W1 context evidence, while representative
private-production measurement remains a later, non-gating product-readiness question.

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

Caveat: W1 evidence covers only the named generated fixtures and explicitly admitted input shapes. Earlier
out-of-scope experiments have been removed, and W1 completion does not establish behavior for other shapes.

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
| Final identity, explanation, descriptor, replay, and local-gate strengthening | `ff7cd1965` | +1,306/−59 | 1,253 |
| Normative v1 contract | `09e980502` | Documentation; excluded | 0 |
| **Total** |  | **+3,781/−269 cumulative dedicated closure diff** | **5,301–5,361** |

The final strengthening commit's raw diff includes 53 additions that replace lines already counted in earlier closure
commits. Its attributable-current-surface column therefore records 1,253 LOC; the total is the exact cumulative
`.github`/`src`/`tests` diff from `09e980502` through `ff7cd1965`, so each delivered line is counted once.

**Status:** **Complete for the milestone-selected W2 v1 scope.** The generated strong-root fixture and dump-free contract
tests pass headlessly at the `ff7cd1965` implementation state. Documentation closure commit `5bed47100` passed all four
required hosted jobs in [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29364905178).

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
- Artifact shapes beyond the named generated fixtures and admitted contracts. Caveat: W2 tests establish behavior
  only for those declared shapes; earlier out-of-scope experiments have been removed.

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
  Actions run 29364905178](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29364905178).

### W3 — Concrete IL semantics and differential oracle

**Original forecast (superseded):** 2,000–3,500 LOC

**Realized dedicated implementation surface:** 8,842 hand-written additions and 1,650 deletions

The exact cumulative diff from normative-contract checkpoint `e7b6a4ace` through strengthened implementation checkpoint
`19c292f9f` contains 5,362 production additions/928 deletions and 3,480 test/fixture additions/722 deletions. It also
contains 39 generated package-lock additions required by the dump host's SRM dependency; those generated lines and the
separately committed normative documentation are excluded from the hand-written ledger. Primary implementation commit
`12b6ef942` has a raw `+8,717/-1,655` hand-written diff, and admission-strengthening commit `19c292f9f` has a raw
`+134/-4` diff. Nine replacement lines occur in both commit-level raw diffs; the cumulative checkpoint diff counts each
delivered line once and therefore supplies the authoritative `+8,842/-1,650` total. This stream does not reassign the
earlier arithmetic/domain/memory spike already represented in W0's mixed-file attribution range. The original forecast
was therefore materially low and remains visible rather than being rewritten post hoc.

**Status:** **Complete for the defined milestone-selected W3 architecture-validation scope.** Strengthened implementation
checkpoint `19c292f9f` passed locked
restore; a fifteen-project Release build with zero warnings/errors; Markdown-link and headless-workflow guards; 103
milestone-selected unit tests; 67 fast integration tests; 5 ordinary dump tests; 1 optimized-context test; and the
focused 2-test W3 dump lane, all headlessly with zero skips. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29374585767) passed all four jobs at the
exact implementation commit. Exact documentation-closure commit
`de6cea124488d503d13c61a4c8e67203a16d06f9` then passed all four required jobs in [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237), satisfying the final W3 gate.

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
11. the complete local and hosted milestone-selected Release/fast/dump/optimized gate.

### W4 — Unknown-aware method evaluation

**Status:** **Complete for the normative milestone-selected W4 scope. Implementation closure commit `a819a08fd`
passed all four jobs in run 29463426083; final documentation-closure commit `aaec73c5b` passed them again in run
29463847230.**

**Realized hand-written implementation surface:** 41,892 LOC: 27,270 through W4.7, 11,924 across W4.8a–f, and 2,698
across W4.9a–c. W4.9d changes documentation and executes unchanged gates, so it carries zero implementation LOC. The
admitted baseline was 16,860–25,310 LOC. The original nine-slice plan has 22 tracked implementation/closure rows
because W4.4, W4.5, W4.8, and W4.9 were delivery-split and W4.6 was split into four independently delivered
sub-slices. The earlier 18,532–26,132, 19,228–25,728,
21,179–26,779, 24,013–29,313, W4.5-closure 25,017–29,417, post-W4.6-design-audit 27,217–32,117,
W4.6a-checkpoint 28,376–32,476, first W4.6b recalibration 28,876–33,276, post-split 28,826–33,726,
post-W4.6b 28,879–33,279, pre-closure W4.6c/d 30,079–33,729, and post-W4.6 closure 31,069–34,319 projections
plus the later 31,670–33,970 projection remain historical calibration. W4.8e realized 3,605 LOC, 105 above the
3,500-LOC planning ceiling; the closure ledger records that miss explicitly.

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
the milestone test selection on behavioral test commands. The checkpoint realizes 3,454 LOC: 3,429 attributable W4.2
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
and both Markdown/headless guards, with zero skips; every test command used the milestone test selection. W4.3 realizes
3,096 LOC (1,100 production LOC plus 1,996 test LOC). W4.1–W4.3 therefore realize 7,028 LOC. This is deliberately dump-free
kernel evidence: no ClrMD non-exact import, counterfactual product facade, or dump-grounded W4 result has landed.

Pushed W4.4a checkpoint `2e596c117` adds body-independent contextual MethodDef resolution. Content-equal
`MethodCallSignatureShape` and `ResolvedMethodCallTarget` values freeze the non-nil same-module target, exact declaring
TypeDef/calling-convention/receiver/generic/parameter/return facts, and ordinary managed-IL certification without
acquiring a body, RVA, local signature, or locals. SRM classifies `MemberRef` and `MethodSpec` as unsupported direct-call
shapes, preserves invalidity for malformed/nil/out-of-range identities, and exposes the disposition-before-body seam
later consumed by W4.6a's opaque pure-model selection.

Pushed W4.4b checkpoint `742ef2c4f` adds an explicit W4 graph-admission mode alongside the unchanged legacy
single-method path. `MethodGraphPlanner` performs deterministic root-first, increasing-call-offset discovery with
first-result caches, retains and charges every direct-call edge, deduplicates equal methods/fields and shared callees,
and checks definition/signature/descriptor agreement before freezing canonical nodes, fields, and calls. The complete
graph is revalidated as rooted, reachable, acyclic, and correctly depth-labeled under fixed internal caps of 64 methods
and 1,024 traversal units. Any cycle, unsupported suffix/dispatch, resolver inability, conflict, cap exhaustion, or
invalid graph yields no partial plan and executes no prefix. The exact fixture freezes two methods, two fields, one
call at IL offset 12, required logical depth two, and five units.

Strict headless W4.4 verification passed locked restore; the fifteen-project Release build with zero warnings/errors;
planner 35/35; W4 fixture 6/6; complete unit 250/250; fast integration 73/73; ordinary dump 5/5; optimized dump 1/1;
and both Markdown/headless guards, with zero skips. Every behavioral command used the milestone test selection. W4.4
realizes 3,651 added LOC: W4.4a contributes 1,043 (665 production plus 378 tests) and W4.4b contributes 2,608 (1,411
production plus 1,197 tests), totaling 2,076 production plus 1,575 tests. The realized audit splits the original
combined W4.4 package so each delivered sub-slice remains below 3,500 LOC; it preserves rather than rewrites the
original combined 1,700–2,600 estimate. W4.1–W4.4 cumulatively realize 10,679 LOC. This remains admission evidence:
the remaining W4.5–W4.9 estimate is 10,500–16,100 LOC and the current total projection is 21,179–26,779 LOC. The
original 16,860–25,310 baseline and earlier 18,532–26,132 and 19,228–25,728 projections remain historical calibration
facts. At that checkpoint, call execution, frames, models, configurable request traversal limits, product projection,
and dump-grounded W4 results were still absent.

Pushed W4.5a checkpoint `356c07037` adds the exact execution half of the prepared graph. One opt-in machine session
binds one immutable graph and one configured logical-depth limit, derives exact root/callee frames from the admitted
metadata, and executes without re-resolution. Structural call-site and return-site identities retain exact resume
boundaries; completed `call` and helper `ret` transfers each consume one instruction and emit ordered
instruction/frame events; root `ret` preserves W3 completion; and persistent memory remains unchanged. The operational
envelope records configured depth, required graph depth, logical-depth high water, and active-frame high water. Depth
underflow rejects before activation, every step rechecks graph/state agreement atomically, and legacy W3 activation
remains call-free and isolated.

At the W4.5a checkpoint, only exact values crossed call and interpreted-return boundaries. Explained unknowns blocked
with `EXEC_CALL_LINEAGE_UNAVAILABLE`; canonical `CallArgumentTransform` and `InterpretedReturnTransform` lineage is
recorded here as the then-pending W4.5b work. An independent audit closed every identified W4.5a blocker. Headless validation passed locked restore;
the strict fifteen-project Release solution build and strict Release builds of the unit and integration projects, all
with zero warnings/errors;
prepared-graph tests 25/25; W4 fixture 7/7; complete unit 275/275; fast integration 74/74; ordinary dump 5/5;
optimized dump 1/1; the Markdown guard across 62 files and 41 local destinations; and the headless guard across one
workflow. Every test lane had zero skips and every behavioral command used the milestone test selection.
The ordinary and optimized dump filters were `Category=Dump&Corpus!=ModeledIncidentContextV1`
and `Category=Dump&Corpus=ModeledIncidentContextV1`, respectively.

Pushed W4.5b checkpoint `c72f6ee9e` adds optional `IInterpretedCallLineageDomain<TValue>` and completes the admitted
interpreted-call kernel. Exact arguments and returns bypass the capability unchanged. The complete metadata-ordered
two-argument vector is validated before any new node is interned; each explained unknown receives one
`CallArgumentTransform` containing the exact direct-call site, parameter index, and predecessor. An explained unknown
returned by the helper receives one `InterpretedReturnTransform` containing the call site and callee-side predecessor
before the caller changes. These are append-only schema-v1 node kinds 4 and 5; legacy kinds 1–3, canonical bytes, and
identities remain frozen.

The domain rejects default or malformed batches and bare, foreign, bottom, or wrong-type values before publishing a
partial batch. Machine failures remain atomic with stable taxonomy: missing optional capability is
`Blocked`/`EXEC_CALL_LINEAGE_UNAVAILABLE`; capability exceptions normalize to `Blocked`/`EXEC_DOMAIN_FAILURE`; and
default, incorrectly sized, foreign, non-executable, or semantically changed output is
`InvalidProgram`/`EXEC_CALL_LINEAGE_INVALID`. Reachable graph capture and fresh-domain replay validate canonical
bytes/identities, dependency order, `Int32` typing, complete call-site identity, and parameter indices before mutation,
then permit deterministic continuation. Compiler integration freezes the mixed exact/partial five-node graph and the
partial/unavailable eight-node graph and replays them across same and fresh sessions without metadata re-resolution.

Exact-commit headless validation at `c72f6ee9e5545240433294cdca4f350808339aef` passed locked restore; the strict
fifteen-project Release build with 0 warnings/0 errors; prepared-graph execution 40/40; the combined lineage/audit lane
76/76, including 29 frozen legacy identity cases; compiler lineage 2/2; aggregate W4 integration 9/9; complete unit
297/297; fast integration 76/76; ordinary dump 5/5; and optimized dump 1/1. Every behavioral filter used
the milestone test selection, there were zero skips, and an independent audit found no remaining finding.

W4.5a realizes 3,334 LOC (1,590 production plus 1,744 tests); W4.5b realizes 2,804 LOC (766 production plus 2,038
tests). Combined W4.5 therefore realizes 6,138 LOC, and W4.1–W4.5 cumulatively realize 16,817 LOC. The original
combined W4.5 estimate of 2,300–3,500 remains historical calibration. The later W4.5b estimate of 1,800–2,700 LOC and
combined 5,134–6,034 projection were each exceeded at the upper bound by 104 LOC. At W4.5 closure, the remaining
W4.6–W4.9 envelope was 8,200–12,600 LOC, producing the now-historical 25,017–29,417 LOC projection.

A subsequent design audit split the former W4.6 2,300–3,400 LOC estimate into W4.6a structural registry, opaque
modeled-leaf, effect, and fallback admission at 1,800–2,600 LOC and W4.6b typed execution, attempts, modeled lineage,
and conformance at 2,700–3,500 LOC. The combined W4.6 estimate is now 4,500–6,100 LOC. This is planning
recalibration, not delivered work at that point. The resulting 27,217–32,117 LOC projection remains historical.

Pushed W4.6a checkpoint `77c92789b` implements structural pure-model admission without model execution. Bounded
non-generic contracts freeze canonical ID/version, exact target, confidence, normalized effects, typed invocation
atoms/outcomes, and payload-omitting `W4.Model.*` codes. `RequirePureModel` selects only exact, `None`-effect descriptors
after caller call resolution/typing and before target-body acquisition; default `Prepare` remains interpret-only. A
successful graph freezes a body-free opaque leaf, explicit call disposition, canonical `ModeledLeaves`/lookup, depth,
and traversal accounting. The compiler graph is one interpreted root, one modeled leaf, two fields, and one edge:
five units at depth two. Selection failures, non-exact confidence, and unsupported effects never fall back to target
interpretation or expose a partial plan. Runtime capability objects are excluded from equality/hash, while the legacy
interpreted call-site hash remains frozen.

The machine rejects every modeled graph before activation as `EXEC_MODEL_EXECUTION_UNAVAILABLE`, without argument
validation, state creation, resolver use, or model invocation. Real SRM/compiler evidence replays the body-free graph
over deterministic PDB-free `PhoenixInspect.TestTarget` PE SHA-256
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`; W4.5 lineage hashes were deliberately
re-frozen to that path-independent artifact.

Exact-checkpoint headless validation passed locked restore; the strict fifteen-project Release build at 0 warnings and
0 errors; model-contract 49/49; model-planner 25/25; legacy planner 35/35; SRM 1/1; lineage 2/2; complete unit 371/371;
fast integration 77/77; ordinary dump 5/5; optimized dump 1/1; Markdown 62 files/41 destinations; and the one-workflow
headless guard. There were zero skips, every behavioral filter used the milestone test selection, and independent audits
found no behavioral findings.

W4.6a realizes 2,959 added LOC (1,210 production plus 1,749 tests/fixture support), exceeding its 1,800–2,600 upper
estimate by 359 LOC. W4.1–W4.6a cumulatively realize 19,776 LOC. The first post-checkpoint remaining-work estimate
produced the now-historical 28,376–32,476 projection; a concrete W4.6b audit then produced the now-historical
28,876–33,276 projection. The next plan split execution into W4.6b atomic modeled-return lineage/domain at
950–1,450 LOC and W4.6c machine transfer, attempts, depth witnesses, and compiler conformance at 2,200–3,300 LOC,
producing the historical 28,826–33,726 projection.

Pushed W4.6b checkpoint `fd723a912` adds optional `IPureCallModelLineageDomain<TValue>` and schema-v1 kind 6,
`ModeledReturnTransform`. Exact arguments are embedded canonically; explained arguments receive their unchanged kind-4
call transforms; and the complete dependency batch is validated and interned atomically. Kinds 1–5 preserve their
canonical bytes and identities, and fresh-domain replay validates the modeled relation before mutation. Strict headless
builds passed with zero warnings/errors; focused modeled lineage passed 8/8, combined legacy-plus-modeled lineage
44/44, and compiler call-lineage integration 2/2, with zero skips and the milestone test selection. W4.6b realizes 1,003
added LOC (481 production plus 522 tests), bringing W4.1–W4.6b to 20,779 LOC. Its post-checkpoint projection was the
now-historical 28,879–33,279 LOC.

Implementation planning then separated W4.6c machine invocation/transfer, attempt chronology, depth witnesses, and
unit conformance at 2,550–2,750 LOC from W4.6d compiler/SRM exact, degraded, and fresh-session conformance at
850–1,000 LOC. That fourteen-row plan produced the now-historical 30,079–33,729 full-W4 projection.

Pushed W4.6c checkpoint `877c9fb55` executes only the model capability frozen with the prepared graph. It performs no
resolver, registry, descriptor, target-body, or selection reread and never falls back to interpretation. A validated
exact or grounded explained-unknown result atomically transfers in the caller, preserves memory, consumes one
instruction, and emits one instruction event without a helper frame. Blocked, invalid, malformed, and normalized
capability outcomes preserve semantic state, memory, instruction budget, and semantic events while recording one
deterministic operational attempt. Logical-depth high water records every entered modeled boundary, including a
non-transferring attempt; active-frame high water does not advance. Terminal validation checks exact attempt chronology,
call/model counts, and depth witnesses. W4.6c realizes 2,734 LOC: 1,425 production additions plus 1,309 unit-test
additions.

Pushed W4.6d checkpoint `da5346813` proves the compiler-emitted caller through real SRM preparation. The frozen model
agrees with both interpretation and CoreCLR for exact input and with interpretation for mixed partial/exact and
partial/unavailable input. Exact execution consumes six caller instructions and two field loads, reaches logical/frame
high water 2/1, records one completed attempt, preserves memory, creates no helper frame, reads no helper body, and
performs no execution-time re-resolution or reselection. The mixed-evidence case freezes literal graph SHA-256
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`. Same-session and fresh SRM/domain/machine
runs reproduce the dual-unknown graph SHA-256 `31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`
over the PDB-free TestTarget SHA-256 `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`. W4.6d realizes 956
integration-test additions.

W4.6 closure passed locked restore; the strict fifteen-project Release build and strict unit/integration builds with
zero warnings/errors; focused W4.6c 34/34; focused W4.6d 3/3; aggregate W4 integration 13/13; complete unit 413/413;
fast 80/80; ordinary dump 5/5; and optimized dump 1/1. All lanes were headless with zero skips,
and every behavioral filter used the milestone test selection. Combined W4.6 realizes 7,652 LOC, and W4.1–W4.6d
cumulatively realize 24,469 LOC.

Pushed W4.7a checkpoint `2e70fe76d` adds the independently useful `PhoenixInspect.Product.DumpDebugging` boundary. Its
standalone projector consumes the complete exact sequence from same-machine legacy activation at IL offset zero to
the first exact null-reference target transition, optionally followed by one issuer-certified idempotent latch re-step.
Each outcome must have been issued by that exact machine for the exact predecessor state and operational-state
references; structural equality or a plausible terminal record is insufficient. Projection validates the legacy
one-frame envelope, exact exception kind/code/location, persistent-memory identity, unit-by-unit accounting, and the
complete event transcript. The schema-v1 fragment fixes the five result axes, retains only the exception, call trace,
accounting, events, stable diagnostic, canonical bytes, and SHA-256, and exposes no snapshot/root/request/plan/
traversal reachability claims. The literal fixture freezes digest
`99cadd992d88ac481b570ec4bc1eb3c914f7d43565db414d9147225e01a9c754`.

Pushed W4.7b checkpoint `dad6a6dd4` projects both direct and constant-adjusted compiler-emitted exact-null getters.
Fresh SRM/module/domain/machine reconstruction reproduces fragment content, bytes, and hash. Counting and poisonable
wrappers prove the optional terminal re-step does not consult the resolver, value domain, or memory model and does not
repeat the single field load. W4.7a realizes 2,448 LOC and W4.7b realizes 353 LOC, so W4.7 realizes 2,801 LOC and
cumulative W4 realization is 27,270 LOC.

W4.7 closure passed locked restore; a strict sixteen-project Release build over eleven source projects at zero
warnings/errors; complete unit 430/430; Fast 80/80; ordinary dump 5/5; optimized dump 1/1; focused W4.7a 15/15;
focused W4.7b 2/2; combined W4.7 17/17; compiler differential class 23/23; Markdown 62 files/41 destinations; and the
one-workflow headless guard. All behavioral lanes were headless, used the milestone test selection, and had zero skips.

The former W4.7 estimate of 2,200–3,150 LOC and its 31,069–34,319 full-W4 projection are historical. W4.8 checkpoints
`4f268a4bc` through `44b050ec8` implement configurable traversal, canonical request/observation/plan/result schemas,
private typed runtime binding and recording memory, authoritative preparation, common projection, and transition-
validating execution. Same/fresh-object coverage freezes the exact synthetic rooted result SHA-256
`6e87efdf3a6f8d73a5f5733aa8fe1eac99d822f184da3b88d46b8cbd67068592`. W4.8 realizes 11,924 LOC.

W4.9a `24bd8fe6f` implements the atomic ClrMD execution-evidence graph; W4.9b `2d41f528d` implements the product-owned
detached rooted binding/memory; and W4.9c `a8b5f32f0` executes exact, partial, and unavailable rows under interpreted
and body-free modeled plans. ClrMD is disposed before execution, and dump reopen/rebind reproduces all six canonical
memory/request/plan/result artifacts before late disk/CoreCLR comparison. W4.9a–c realize 1,389/791/518 LOC, or 2,698
total. Focused generated dump passes 1/1, ordinary dump 6/6, and Fast 88/88 headlessly with zero skips. W4.9d records
the complete local gate, exact pushed hosted evidence, and closure.

W4.9d local closure passes locked restore; a strict sixteen-project Release build at zero warnings/errors; complete
unit 502/502; Fast 88/88; ordinary dump 6/6; optimized dump 1/1; aggregate W4 integration 14/14; Markdown 62 files/44
destinations; and the one-workflow headless guard. Every behavioral filter includes the milestone test selection, with zero
skips. [Hosted run 29463426083](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463426083) passed all
four jobs at exact implementation-closure commit `a819a08fd9ccdf926620c505732475990b242be9`; [run
29463847230](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29463847230) passed them again at final
documentation-closure commit `aaec73c5b987089addb539d3628de67bd815bd8f`.

**Admitted work slices**

| Slice | Non-overlapping implementation responsibility | Estimated hand-written implementation LOC | Realized LOC |
|---|---|---:|---:|
| W4.1 — Gate fixture | Add the generated rooted workflow, exact emitted-shape assertions, exact CoreCLR oracle, and one current-W3 whole-body rejection checkpoint. | 350–480 | 478 |
| W4.2 — Unknown E1/E2 kernel | Introduce provenance-bearing unknown continuation for the existing W3 E1/E2 value transfers while preserving structural typing, deterministic events, and no fabricated concrete values. | 3,350–3,500 | 3,454 |
| W4.3 — Non-exact dump field continuation | Import partial/unavailable field values as typed explanatory unknowns while preserving conflict and invalid as distinct non-continuing outcomes. | 2,400–3,500 | 3,096 |
| W4.4a — Body-free direct-call metadata | Resolve and certify the exact ordinary managed-IL MethodDef/signature without acquiring prospective body/local facts, preserving disposition-before-body selection. | Post-audit sub-slice of original combined W4.4 estimate, 1,700–2,600 | 1,043 |
| W4.4b — Frozen transitive graph | Admit the interpreted call closure, retain/deduplicate structural dependencies, validate the canonical acyclic graph, calculate required depth, and enforce fixed internal resource caps. | Post-audit sub-slice of original combined W4.4 estimate, 1,700–2,600 | 2,608 |
| W4.5a — Exact multi-frame execution and call depth | Push/pop exact interpreted frames at discrete observable boundaries, preserve return-site identity, enforce the prepared maximum logical depth, validate replay invariants, and record logical/frame high water. | Post-audit sub-slice of original combined W4.5 estimate, 2,300–3,500 | 3,334 |
| W4.5b — Explained-unknown call/return lineage | Add the call-boundary capability, canonical `CallArgumentTransform`/`InterpretedReturnTransform`, and same/fresh-object replay without changing exact transfer. | 1,800–2,700 | 2,804 |
| W4.6a — Structural model admission | Add the scenario-narrowed structural registry, opaque modeled leaf, normalized effect contract, and explicit fallback admission without executing a model or admitting ambient/arbitrary target behavior. | 1,800–2,600 | 2,959 |
| W4.6b — Modeled-return lineage/domain | Add the atomic modeled-return transform and domain capability, preserving exact/unknown semantic value and dependency truth without machine transfer. | 950–1,450 | 1,003 |
| W4.6c — Modeled machine transfer | Add frozen-capability-only invocation/transfer, attempt records, logical-versus-active depth witnesses, exact terminal validation, and unit conformance. | 2,550–2,750 | 2,734 |
| W4.6d — Compiler/SRM conformance | Prove exact, degraded, repeated, and fresh-session interpreted/model agreement over the compiler-emitted W4 fixture without helper-body acquisition or execution-time metadata/model rereads. | 850–1,000 | 956 |
| W4.7 — Target-outcome stop-on-throw contract | Define the standalone target-outcome/canonical fragment for W3's exact-null latch without admitting a fabricated helper/model throw; handler search and transfer remain excluded. | 2,200–3,150 | 2,801 |
| W4.8a — Traversal accounting | Add configurable ordered charges, exact exhaustion, and retained budget context without weakening fixed internal caps. | Delivery split of original W4.8 2,400–3,500 estimate | 737 |
| W4.8b — Canonical request and issued plan | Freeze schema-v1 request, field observations, complete plan identity, issuer authority, and defensive projections. | Delivery split of original W4.8 2,400–3,500 estimate | 2,389 |
| W4.8c — Typed runtime binding | Privately bind domain, memory, receiver, arguments, and recording evidence to the exact canonical request. | Delivery split of original W4.8 2,400–3,500 estimate | 1,564 |
| W4.8d — Authoritative preparation | Validate raw input, evidence, graph, traversal, depth, observations, and activation in a deterministic fail-closed pipeline. | Delivery split of original W4.8 2,400–3,500 estimate | 1,732 |
| W4.8e — Common canonical result | Project rooted and standalone axes, accounting, context, values/prefixes, diagnostics, and terminal attempts into one immutable result. | Delivery split of original W4.8 2,400–3,500 estimate | 3,605 |
| W4.8f — Execution runner | Validate issued transitions and cover exact/degraded/budget/cancel/failure/model/replay behavior without late resolution. | Delivery split of original W4.8 2,400–3,500 estimate | 1,897 |
| W4.9a — ClrMD execution graph | Correlate a bounded counted method graph and exact/partial/unavailable required-field evidence atomically. | Delivery split of original W4.9 2,000–3,200 estimate | 1,389 |
| W4.9b — Detached dump binding | Derive rooted canonical identity and persistent read-only memory from issuer-validated dump evidence. | Delivery split of original W4.9 2,000–3,200 estimate | 791 |
| W4.9c — Generated-dump corpus | Execute six interpreted/modeled exact/degraded rows and reproduce canonical artifacts after close/reopen/rebind. | Delivery split of original W4.9 2,000–3,200 estimate | 518 |
| W4.9d — Local/hosted closure | Reconcile documentation, run every local gate, and record exact pushed hosted evidence. | 0 implementation LOC; documentation and unchanged gate execution excluded | 0 |

Instruction budget remains the implemented W3 baseline. W4.4 computes required logical depth and graph traversal
usage under fixed internal resource caps, but does not apply request policy. W4.5 applies a machine-supplied maximum
logical call depth before prepared-graph activation, reports logical/frame high water, and carries exact or explained
unknown values across admitted interpreted calls. W4.6 applies the same required/configured logical-depth envelope to
the frame-free model boundary, charging one instruction only on a completed exact/grounded-unknown transfer and
recording non-transferring attempts without semantic budget/event mutation. W4.8 adds configurable traversal charging,
ordered charge/result context, canonical product projection, and authoritative execution to the already frozen graph.
Allocation is unadmitted by the selected workflow, so no allocation operation consumes a budget and W4 adds no
dormant allocation counter. A later allocation scenario must define the operation, charging point, result behavior, and
tests before an allocation budget becomes an applied contract.

**Entry criteria**

- W1–W3 are closed for their stated scopes. **Satisfied.**
- The selected `GetMarkerSummary` workflow demonstrates method-execution value beyond W2's one-root/one-field query.
  **Satisfied by W4.1 fixture evidence and W4.9 generated-dump product execution.**
- Effects, deterministic budgets, degraded-evidence behavior, product truth language, and explicit exclusions are fixed
  by the normative W4 contract. **Satisfied; W4.1–W4.9 implementation evidence is recorded above.**
- Each admitted W4 work slice had an explicit estimate of at most 3,500 hand-written implementation LOC before work
  began. **Historically satisfied by the pre-work plan; W4.8e later realized 3,605 LOC and the overrun is retained.
  W8 and subsequent planning instead use the logarithmic policy in section 2.**

**Exit criteria for the umbrella**

- Selected methods tolerate missing inputs without fabricating concrete values. **Satisfied locally.**
- Differential and degraded-evidence tests cover every supported opcode/call family. **Satisfied locally.**
- Product language consistently describes results as counterfactual, not historical replay. **Satisfied locally.**
- The exact pushed closure commit passes all required headless hosted jobs with the milestone test selection, and the realized
  hand-written implementation LOC ledger is recorded without counting documentation or generated output. **Ledger
  satisfied. Exact pushed implementation closure passed in run 29463426083 at
  `a819a08fd9ccdf926620c505732475990b242be9`; final documentation closure passed in run 29463847230 at
  `aaec73c5b987089addb539d3628de67bd815bd8f`.**
- Artifact shapes beyond the named generated fixtures remain an accepted exclusion, not an unfilled W4 evidence gate.
  **Satisfied.**

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

A UI result badge may summarize them but never replaces them in contracts or tests.

### Exception handling

- W3 admits EH-free bodies only and latches one exact typed-null `ldfld` as a terminal target exception without handler
  transfer or continuation.
- W4.7 defines the retained exact-null target-outcome/canonical fragment integrated by W4.8's common result projector.
  It has no rooted product request or plan identity. The closed non-throwing helper/model cannot fabricate
  a broader exception case, and handler transfer remains separate.
- Full handler search, filters, unwind, `finally`/`fault`, and cross-frame propagation are prerequisites for debugger-grade exception stepping, not implicit refinements.

## 6) Risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scope disperses across research subsystems | High | Critical | Scope lock, one active milestone, explicit research gates. |
| A single maintainer cannot sustain a platform-sized surface | High | Critical | Give every active slice a logarithmic implementation-LOC band, push coherent checkpoints, and preserve one product path. |
| Maintainer unavailability leaves the active slice without continuity | Medium | Critical | Keep one canonical vertical-slice path, executable fixtures, explicit evidence boundaries, and a current handoff map; avoid private operational knowledge. |
| Optimized dumps omit roots, locals, arguments, or `this` | High | High | Make unavailable/partial expected outcomes; measure scenario recovery rather than guessing. |
| Artifact shapes outside the validated fixture set produce unsupported behavior | Medium | Critical | Keep those shapes outside the validated milestone surfaces; each later initiative owns its own requirements and evidence before admitting a broader artifact product surface. |
| Documentation volume is mistaken for capability | High | High | Track implementation and validation separately; design just ahead of code. |
| Backend or identity mismatch yields plausible wrong reads | Medium | Critical | Identity validation, conflict outcomes, real-dump fixtures, no silent fallback. |
| The evaluator does not materially improve incident workflows | Medium | High | Keep W4 tied to the exact two-field `GetMarkerSummary` question; require the gate fixture, CoreCLR comparison, dump corpus, and product result to demonstrate value beyond W2 before claiming the admitted design is useful. W3 alone remains architecture validation. |

## 7) Decision gates

W2 decisions applied at its closure commit:

1. The implemented subset used a project-owned bounded parser for one root/field and optional literal coalescing.
   W6.2 later supersedes that mechanism with the common pinned Roslyn parser while retaining project-owned admission.
2. Project-owned immutable parse/query shapes stay internal; no Roslyn object enters a core contract.
3. Diagnostics use stable artifact-text-free messages, value display omits values, and canonical replay is explicitly not a diagnostic output format.
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
3. The original nine admitted work slices in the W4 section were non-overlapping hand-written implementation-LOC
   envelopes with upper estimates no larger than 3,500 LOC. Realized audits split W4.4 into W4.4a/W4.4b, W4.5 into
   W4.5a/W4.5b, and W4.6 into W4.6a/W4.6b/W4.6c/W4.6d, so the current table has fourteen rows while preserving the
   original umbrella baseline and recording each sub-slice once.
4. Instruction and traversal units are consumed only by their defined operations. Maximum logical call depth is a
   preparation-time bound with execution high-water reporting, not a consumable unit. Allocation remains unadmitted
   and has no dormant counter.
5. W4 evidence covers only the named generated fixtures and explicitly admitted input shapes.
6. W4.2 keeps explanation outside lattice and machine semantic equality. Execution distinguishes exact, explained
   unknown, and ungrounded top only through an optional precision capability and admits explained values only under an
   explicit policy.
7. W4.2 owns `InputOrigin` and ordered `BinaryTransform` lineage. W4.3 adds structured non-exact field continuation,
   precision-loss events, and `FieldLoadTransform` through the same generic `ldfld` handler while preserving exact
   behavior. W4.4 adds body-free direct MethodDef resolution and complete interpreted-graph admission with fixed
   internal resource caps. W4.5a adds resolver-free exact multi-frame call execution, retained return sites, atomic
   depth enforcement, frame events, and logical/frame high-water reporting. W4.5b adds atomic explained-unknown
   call/return lineage, append-only canonical node kinds, and same/fresh-session replay. W4.6 adds exact/no-effect
   structural model selection, atomic modeled-return lineage, frozen-capability-only invocation/transfer, operational
   attempts, separate logical/frame depth witnesses, and exact/degraded/fresh compiler conformance. W4.7 adds the
   standalone target fragment, W4.8 adds configurable traversal plus common product preparation/execution/projection,
   and W4.9 adds detached ClrMD grounding plus close/reopen generated-dump replay. Exact hosted umbrella closure passed
   in run 29463426083 at `a819a08fd9ccdf926620c505732475990b242be9`; documentation closure then passed run
   29463847230 at `aaec73c5b987089addb539d3628de67bd815bd8f`. No W4 implementation or closure step remains.

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

The historical schema-v1 report retained `this`, argument, local, static, and strong-root axes and recorded raw member
bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5. W7's current canonical schema-v2 report
records attributable context at 2/5 because both the static field and strong root are exact at their relevant stages;
product-query availability remains 1/5 at the strong root. That W7 report still does not attribute its `this`, argument,
or local axes. Separately, W8.1's six dedicated selected-frame profiles prove exact memory-homed `this`, reference/value
parameter, and active-local roots; register homes and selected-frame generic arguments remain non-admitted. These are
raw counts and exact branch facts from generated modeled dumps, not a representative private-production corpus or a
production recoverability rate.

Continue to report raw counts and corpus composition before any aggregate percentage. Do not extrapolate from the
modeled fixture to production incidents, and do not set a readiness threshold until a representative
private-production incident corpus exists. Until then, the W7 schema-v2 product route remains available only at its
validated strong-root path, while W8.1's exact memory-homed frame roots remain pre-contract evidence rather than a
product route.

## 8) Documentation policy

- Update this plan when executable evidence changes sequencing or scope.
- Keep current implementation facts in the root README and prototype-status documents; link rather than duplicating volatile counts.
- Mark research documents clearly, but do not expand them merely to make their taxonomies agree.
- Prefer a short contract plus a running fixture over another comprehensive up-front specification.
