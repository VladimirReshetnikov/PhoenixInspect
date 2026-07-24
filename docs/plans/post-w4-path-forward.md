# Post-W4 Path Forward

> **Lifecycle:** Complete · **Roadmap:** Reference
>
> **Decision:** fund product composition and usefulness evidence before adding interpreter breadth.
>
> **Successor:** W5 selected the now-completed W6 bounded member-chain stage in
> [`Post-W5 Path Forward`](post-w5-path-forward.md).

## 1) Executive decision

W1–W4 prove a strong architecture: the repository can recover bounded dump evidence, answer one restricted derived
query, execute one branchless direct-call method graph with exact or provenance-bearing unknown values, substitute one
exact no-effect model, and reproduce canonical results after dump close/reopen. W4 implementation closure commit
`a819a08fd9ccdf926620c505732475990b242be9` passed every required job in hosted run
[29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083). Its final documentation-
closure commit `aaec73c5b987089addb539d3628de67bd815bd8f` then passed the same four-job matrix in run
[29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230).

The next bottleneck is no longer the interpreter kernel. The existing method evaluator is reachable only after a
caller performs the structural work demonstrated in integration tests: locate the module/type/method, acquire and
correlate bodies and fields, construct a resolver and frozen graph, bind the root and memory, prepare the request, and
run it. A user can supply expression text to W2, but cannot use expression text to reach W4. The project therefore has
an impressive execution proof without the smallest product-shaped route into it.

The successor implemented by this completed plan was **W5 — one expression in, one honest result out**:

```csharp
root.Marker
root.GetMarkerSummary()
```

The first expression continues through the unchanged W2 `DerivedQuery` path. The second is the only new syntax and
reaches the already-closed W4 `CounterfactualExecution` scenario. The host supplies an already-open dump, an exact
host-selected root, expression text, and policy. It does not supply metadata handles, method graphs, correlated field
vectors, or machine state.

W5 was product composition, not a general C# evaluator and not a new interpreter-semantics milestone. After W5 made
the current capability consumable, a meaningful multi-shape synthetic incident-question portfolio selected the next
prototype investment. This designed evidence does not establish field readiness. No W6 feature was pre-approved when
W5 began; the later W6 contract is owned separately by the [Post-W5 Path Forward](post-w5-path-forward.md).

## 2) Why this is the right next move

The architecture review's central warning was platform-first, value-last expansion. W0–W4 repaired almost all of its
foundational findings: scope lock, truthful topology, CI, headless execution, deterministic replay, stable identity,
typed evidence, lattice laws, a second meaningful domain, dump-sourced IL, differential tests, and an actual rooted
product result. Three gaps defined W5's work:

1. **Product reach:** W4 still requires expert, test-shaped structural assembly rather than one product request.
2. **Meaningful synthetic usefulness:** the original admitted success evidence came from one controlled generated
   shape, so the project needed an independent, multi-shape designed-incident denominator before selecting more
   prototype behavior. This denominator remains distinct from field-readiness evidence.
3. **Roadmap discipline:** without an explicit successor, it would have been easy to promote virtual stepping, branches,
   exception-handler transfer, CFG analysis, async/dynamic lifting, generics, or broad models merely because their
   research entry gates are now closer.

W5 closes the first gap and, through W5.5b checkpoint `90ade6d92`, measures the second while preserving the third.

## 3) Scope lock

### Admitted behavior

- Preserve the complete W2 v1 grammar and result identities unchanged.
- Add exactly one zero-argument instance-method expression shape: `root.GetMarkerSummary()`.
- Bind only the case-sensitive, directly declared, non-generic MethodDef matching the already-admitted W4 fixture.
- Acquire the existing W4 method/field graph and detached memory without caller-supplied structural objects.
- Return W2 and W4 results through a mode-preserving product union or discriminated facade; never flatten
  `DerivedQuery` and `CounterfactualExecution` into one ambiguous value.
- Preserve exact, partial, unavailable, conflict, invalid, budget, cancellation, model, effect, provenance, and
  diagnostic distinctions already implemented by W2/W4.
- Provide a headless reference consumer and a versioned scenario manifest so the full request can run outside a test
  method without declaring the prototype API stable.

### Explicit exclusions

- Arguments, overload resolution, inherited lookup, extension methods, properties, arbitrary calls, and general
  expression binding.
- New opcodes, branches, loops, handler-transfer exception handling, virtual/generic dispatch, allocation, or new
  semantic models.
- Frame, local, argument, static, PDB, source, or decompiler context.
- Multi-hop member navigation, `?.`, indexers, arrays, collections, LINQ, async/dynamic lifting, and virtual stepping.
- IDE UI, debugger protocol integration, packaging, compatibility guarantees, or a service surface.
- Artifact shapes beyond the named generated fixtures and admitted contracts. Caveat: W5 tests establish behavior
  only for those declared shapes; removed out-of-scope experiments are not part of W5.

These exclusions are not a judgment that the capabilities lack value. They prevent W5 from becoming a disguised
platform milestone before the existing evaluator has been tested as a product.

## 4) LOC scale and recalibration

Planning uses order-of-magnitude implementation scales only:

- `~100 LOC` — a small correction, fixture, or adapter;
- `~1K LOC` — a bounded vertical slice with its tests;
- `~10K LOC` — a milestone that must be decomposed into independently useful checkpoints.

Implementation LOC includes hand-written production code, tests, fixtures, harnesses, scripts, and material
configuration. Documentation and generated files remain outside the implementation ledger. Estimates are scale
signals, not schedule forecasts, and may be revised whenever implementation exposes a different boundary.

The detailed historical ledgers remain authoritative in
[`future-work-planning.md`](future-work-planning.md). At the coarser scale used from this point forward:

| Milestone | Closed or planned implementation order | Status |
|---|---:|---|
| W0 — baseline and feedback | `~1K LOC` | Closed |
| W1 — dump evidence | `~1K LOC` for the active milestone-selected scope | Closed |
| W2 — restricted query | `~10K LOC` | Closed |
| W3 — concrete IL proof | `~10K LOC` | Closed |
| W4 — unknown-aware method evaluation | `~10K LOC` | Closed |
| W5 — expression-to-result product composition and usefulness gate | `~10K LOC` | Closed |

W5's umbrella scale is intentionally conservative. Each implementation checkpoint below begins at `~1K LOC`; if a
checkpoint grows to `~10K LOC`, work stops at the preceding evidence boundary and the plan is split before more code
lands.

## 5) W5 delivery sequence

### W5.0 — truth reset and decision record

**Scale:** `~100 documentation LOC`; `0 implementation LOC`.

Reconcile authoritative documents with W4's exact hosted closure, publish this plan, link it from repository
navigation, and freeze the W5 scope before implementation. This checkpoint must not change runtime behavior.

**Exit gate**

- No current-state status or roadmap section describes the W4 product facade, generated-dump result, or hosted
  closure as pending; checkpoint history may still state what was absent at that checkpoint.
- `future-work-planning.md` remains the W0–W4 record and research-gate source; this document remains the completed W5
  sequence, while `post-w5-path-forward.md` governs the later W6 delivery record.
- Markdown links and headless-workflow guards pass.

### W5.1 — closed expression request and classifier

**Scale:** `~1K implementation LOC`.

Define one bounded, canonical product request that carries expression text, exact root selection, policy, and reached
bounds. Classification has only three outcomes:

1. an unchanged W2 expression;
2. the exact `root.GetMarkerSummary()` method expression; or
3. a stable unsupported/invalid result before evidence acquisition or execution.

Do not introduce Roslyn or a general AST for this grammar. The parser must preserve current W2 canonical identities
for every W2 request and add a separately versioned method-expression identity rather than silently changing schema
v1.

This was the closed W5.1 mechanism decision, not a permanent ban on a compiler parser. W6.2 supersedes the mechanism
with one pinned Roslyn expression parse plus compatibility-preserving W2/W5 tree recognizers; it does not rewrite this
historical evidence or broaden W5 binding/evaluation.

**Exit gate**

- Exact spelling, casing, punctuation, root-name, expression-length, and unsupported-suffix behavior are deterministic.
- Existing W2 corpus bytes and hashes remain unchanged.
- No classification outcome can execute a supported prefix before rejecting a suffix.
- Repeated and fresh-object classification reproduces canonical request bytes and SHA-256.

### W5.2 — dump acquisition and method binding facade

**Scale:** `~1K implementation LOC`.

Add a typed current-capability facade in the existing dump-debugging product boundary. Given the issued method
expression, already-open dump, and exact host-selected root, it owns the structural steps that tests currently perform:

- select the exact runtime module and declaring type;
- bind the single directly declared zero-argument MethodDef;
- acquire and correlate the admitted caller/helper bodies and required fields;
- build the existing complete frozen graph;
- detach root, field, and memory evidence from the ClrMD session; and
- return the existing W4 preparation candidate or one typed acquisition failure.

The facade must reuse `ClrmdDumpExecutionResolver`, `CounterfactualDumpExecutionBinder`, and the existing canonical W4
contracts. It must not create a parallel interpreter, graph, evidence, or result model.

**Exit gate**

- The caller supplies no `MethodHandle`, `FieldHandle`, resolver, graph, field vector, domain value, or machine state.
- Missing, ambiguous, incompatible, unsupported, partial, conflict, and invalid acquisition states remain distinct
  and fail before execution where the existing contract requires it.
- ClrMD can be disposed before product preparation/execution, preserving W4's detached-evidence boundary.
- Counting/poisonable tests prove deterministic acquisition order and no execution-time metadata re-resolution.

### W5.3 — mode-preserving evaluator facade

**Scale:** `~1K implementation LOC`.

Compose W2 preparation/evaluation and W4 preparation/execution behind one narrow product operation. Its output is a
closed union whose case retains the complete existing result type:

- W2 remains `DerivedQuery`;
- W4 remains `CounterfactualExecution`; and
- classification/acquisition failure retains a typed product diagnostic without inventing either semantic mode.

The union is a routing and ownership boundary, not a new lowest-common-denominator result schema. Any convenience
projection must preserve access to every underlying axis and canonical artifact. Prefer the existing dump-debugging
product assembly as the composition root; add no project unless an implemented dependency boundary and its tests
justify it, and never add a placeholder assembly.

**Exit gate**

- `root.Marker` returns the unchanged W2 exact result.
- `root.GetMarkerSummary()` returns W4's exact `0x26AF37BD` result for exact evidence.
- Partial or unavailable marker evidence returns the same completed provenance-bearing unknown as W4, never a
  concrete fallback.
- Interpreted and body-free modeled policies remain explicit and reproduce their existing results.
- Unsupported syntax or method shape performs no method execution.

### W5.4 — headless reference consumer and canonical corpus

**Scale:** `~1K implementation LOC`.

Add one repository-owned headless reference consumer that accepts a versioned local scenario manifest and renders:

- a stable machine-readable projection for comparison and automation; and
- a concise human-readable explanation of semantic mode, completion, completeness, evidence, effects, value,
  reached bounds, provenance, and diagnostics.

The consumer is a prototype composition root, not a shipping CLI contract. Its checked-in corpus uses generated
fixtures and covers W2 query, W4 exact interpreted, W4 exact modeled, degraded field evidence, acquisition failure,
unsupported expression, budget exhaustion, cancellation, repeated execution, and dump close/reopen/rebind.

**Exit gate**

- The complete scenario runs without a test method manually constructing W4 internals.
- Every managed launch is headless and displays no dialog or auxiliary UI.
- Same-session, fresh-process where practical, and reopened-dump runs reproduce the declared canonical artifacts.
- Required local and hosted gates pass at the exact pushed commit with zero skips.

### W5.5 — meaningful synthetic usefulness gate

**Scale:** `~1K implementation LOC` for W5.5a's manifest/report runner and `~1K implementation LOC` initially for
W5.5b's multi-snapshot target/orchestration/expectation corpus. Generated dump bytes are evidence, not implementation
LOC.

This checkpoint has two evidence boundaries:

- **W5.5a — deterministic runner:** join predeclared question metadata to W5.4 machine reports, retain every row,
  emit raw counts and composition, enforce corpus provenance, and defer the successor decision while the gate is open.
- **W5.5b — meaningful synthetic portfolio:** run predeclared designed incidents through independently captured dump
  snapshots and record the baseline. The scenarios must exercise multiple structural application shapes, expected
  investigation decisions, exact/degraded/blocked outcomes, and recurring blockers; duplicating the W5.4 routing rows
  or relabeling one fixture does not satisfy this boundary.

Run a predeclared portfolio of incident questions through the W5 facade. The design-validation baseline is on the
order of `~10` independently dumped synthetic incidents across more than one application shape. Each application
shape has a distinct root type and surrounding object graph, and every incident has a predeclared expected product
outcome. The simple W5.4 controlled fixture may validate routing and the report runner, but it does not satisfy this
gate. The qualifying synthetic portfolio validates prototype usefulness only and does not contribute to a field-
readiness rate.

For every question, retain at least:

- the user task and expression requested;
- the required root/context kind and whether it was attributable;
- whether the required member/method evidence was exact, partial, unavailable, conflicting, or invalid;
- the semantic mode and terminal product outcome;
- the first unsupported product or evidence boundary, if any; and
- the manual object-walking operations needed to obtain the same answer, when known.

Report raw numerators, denominators, and corpus composition before percentages. Retain unavailable and unsupported
questions in the denominator. Report both the qualifying synthetic denominator and the zero external-observation
denominator. Do not extrapolate from designed scenarios or claim field readiness.

**Exit gate**

- The checked-in report schema and replay tests are deterministic.
- At least 10 independent synthetic incidents across at least two distinct application shapes produce an honest raw-
  count baseline, or W5 remains open with the missing breadth named explicitly.
- The resulting blocker ranking is sufficient to make the decision in §6 without relying on architecture taste.

### W5 implementation ledger — 2026-07-16

| Checkpoint | Coarse realized scale | Pushed checkpoint | Current evidence |
|---|---:|---|---|
| W5.0 | `~100 documentation LOC` | `053bcb94f` | Scope, exclusions, LOC scale, and closure rule frozen. |
| W5.1 | `~1K LOC` | `7c3d52572` | Exact W2 syntax classification remains parser-owned; the sole method expression has a canonical policy/request identity and exact spelling boundary. |
| W5.2 | `~1K LOC` | `d88b13c2c` | One product-owned facade reacquires root/module/type/caller/helper/fields, returns typed acquisition failures, and detaches all evidence before preparation/execution. |
| W5.3 | `~1K LOC` | `fc8a43a7a` | One strict outcome union preserves the original W2 result, W4 execution result, preparation failure, classification failure, or acquisition failure without a fabricated common semantic mode. |
| W5.4 | `~1K LOC` | `59d9bb590` | The 14-project solution contains a headless reference consumer and nine-row generated corpus with fresh-process and dump-reopen deterministic replay. |
| W5.5a | `~1K LOC` | `0f5230e13`, `b788f4f66` | The schema-v2 usefulness runner separates generated routing, qualifying synthetic design evidence, and representative observations; it emits raw admission/outcome/usefulness/acquisition composition, rejects corpus-kind promotion, and requires predeclared synthetic root/scenario/outcome/value contracts plus independent snapshot identities. |
| W5.5b | `~1K LOC` | `90ade6d92` | Twelve independently dumped, predeclared incidents across request-pipeline and batch-pipeline root graphs replay headlessly. Their blocker ranking selects `AdmitFixedDepthMemberChain` as a prototype design decision while representative observations remain zero. |

The implemented W5 surface is cumulatively `~10K LOC` at order-of-magnitude scale. The generated validation report is
8/9 admitted and 3/9 exact, with 0/4 useful partial-or-unknown and 0/9 decision-changing answers. Those are runner-
validation counts only. The meaningful synthetic report is 8/12 admitted and 4/12 exact, with 2/3 useful partial-or-
unknown and 6/12 decision-changing answers across 12 incidents and two application shapes. It contains four
`MemberNavigation`, three `ContextAcquisition`, one `ExecutionBody`, and four no-blocker rows. These designed counts
select prototype direction only: they contribute nothing to a field-readiness rate, and the representative/external-
observation denominator remains 0 questions across 0 incidents and 0 application shapes.

W5.1–W5.5b and the W5 umbrella are closed for the defined prototype scope. Exact source baseline `26ba75fb1` passes
the meaningful portfolio and complete local matrix. The owner explicitly waived the final hosted-only condition for
W5 on 2026-07-16 because the external account state could not be repaired in this milestone. Run
[29512657137](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29512657137) passed documentation and
Build/Fast at exact commit `24825ce53`, but GitHub rejected both dependent dump jobs before their first step. Run
[29513051897](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29513051897) rejected every job before
execution, including repeated attempts. Those runs are infrastructure history, not passing hosted dump evidence.

The current documentation candidate passes the complete local headless matrix: locked restore; strict 14-project
Release build with warnings as errors at 0 warnings/0 errors; unit 502/502; Fast 104/104; ordinary dump 10/10;
optimized dump 1/1; focused W5 facade 3/3; focused generated usefulness 1/1; focused meaningful synthetic usefulness
1/1; Markdown links across 63 files/59 local
destinations; and the one-workflow headless guard. Every test lane has zero skips. This is complete local closure
evidence for the implemented checkpoints, not hosted dump evidence or field readiness. No test, selection, or workflow
was removed or relaxed. The owner exception applies only to W5's final hosted condition; future milestones inherit the
unchanged hosted-gate policy unless they receive their own explicit disposition.

## 6) The post-W5 decision gate

W5 does not automatically lead to more syntax or IL. Rank candidate increments by:

1. frequency across independent incident questions;
2. value of the blocked answer to the investigation;
3. availability and attribution of the required dump evidence;
4. ability to preserve exact/non-exact/result/replay honesty; and
5. implementation closure at `~1K` or decomposable `~10K` scale.

Then choose exactly one branch:

| Observed dominant blocker | Next action |
|---|---|
| Roots or frame context are usually unavailable or unattributable | Admit one context-acquisition scenario; do not expand expression or IL semantics. |
| Exact roots exist, but one-hop navigation blocks recurring questions | Admit a fixed-depth, null-aware member-chain slice; `root.Failure?.Message ?? "<none>"` is the fallback candidate, not a commitment. |
| The W5 method form is useful, but one repeated body shape is unsupported | Admit only that scenario's complete opcode/call/EH dependency closure. |
| Current answers are useful but difficult to consume | Improve the reference host/result explanation before adding semantics. |
| No recurring useful question can be answered from recoverable evidence | Stop feature expansion and reconsider the product thesis. |

A candidate that requires branches, handler transfer, CFG/fixpoint analysis, generic dispatch, or another large
subsystem receives a new contract and `~10K`-scale decomposition before implementation. Passing a historical research
entry gate is necessary but not sufficient; the qualifying W5.5b blocker ranking must select the next prototype work.
Because W5.5b uses designed scenarios, this decision advances prototype design only. External observations may later
confirm, reverse, or stop that direction before a field-readiness claim.

### W5.5b decision — fixed-depth member chain selected

Checkpoint `90ade6d92` satisfies the designed-evidence gate with 12 independently hashed dumps, 12 incident questions,
and two structurally distinct root/application shapes. Four questions independently fail at fixed-depth member
navigation: `root.Failure.Code`, `root.CurrentRequest.Status`, `root.LastFailure.Code`, and `root.Progress.State`.
Context acquisition appears in three incidents and the execution body in one. The deterministic ordering therefore
selects `AdmitFixedDepthMemberChain`.

This selection admits design work on one fixed-depth, null-aware member-chain slice after W5 closure. It does not yet
admit arbitrary properties, collections, indexers, calls, frame recovery, general expression binding, or additional IL
semantics. It is also not an external observation or a readiness claim; representative counts remain zero and may
later reverse the prototype direction.

## 7) Verification and delivery discipline

Every implementation slice preserves the existing gates and adds its focused corpus. Behavioral tests always exclude
the named-fixture scope. The expected command shape is:

```powershell
.\eng\Invoke-HeadlessProcess.ps1 dotnet restore PhoenixInspect.sln --locked-mode
.\eng\Invoke-HeadlessProcess.ps1 dotnet build PhoenixInspect.sln --configuration Release --no-restore --verbosity minimal --maxcpucount:1 --disable-build-servers /p:UseSharedCompilation=false
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.Tests/PhoenixInspect.Tests.csproj --configuration Release --no-build --no-restore  --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast" --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W5ExpressionFacadeV1" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W5UsefulnessGeneratedV1" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W5MeaningfulSyntheticV2" --verbosity normal
```

Documentation gates remain:

```powershell
pwsh -NoLogo -NoProfile -File .\eng\verify-markdown-links.ps1
pwsh -NoLogo -NoProfile -File .\eng\verify-headless-workflows.ps1
```

Each W5 checkpoint was committed and pushed before the next began. Commit messages record the scenario, ownership
boundary, evidence added, exclusions preserved, coarse LOC scale, and exact validation run. This plan,
traceability map, product proposal, and test strategy are updated at the same checkpoint so documentation cannot drift
behind executable truth again.

## 8) Completion definition

W5 closes only when all of the following are true:

- expression text reaches both the unchanged W2 query and the exact W4 method scenario through one product-owned
  entry path;
- callers no longer assemble W4 structural machinery;
- exact, degraded, failure, budget, cancellation, interpreted, and modeled outcomes retain their existing truth axes;
- a headless consumer and generated canonical corpus prove the complete request-to-result path;
- the meaningful synthetic usefulness gate has produced raw counts from at least 10 independent dumps across two
  structural application shapes and selected exactly one next prototype action; external observations remain a
  separate, explicitly zero-denominator field-readiness concern; and
- the exact pushed closure commit passes locked restore, strict Release build, complete unit/Fast/focused dump lanes,
  Markdown/headless guards, and hosted CI with zero skips within the milestone test selection.

All substantive product, evidence, replay, headless, synthetic-portfolio, and complete local-matrix conditions above
are satisfied. The owner explicitly accepted the missing hosted portion of the final condition as a W5-only closure
exception on 2026-07-16. Therefore W5 is closed for its defined prototype scope. The rejected hosted jobs remain
recorded as not executed and not passing; this exception neither establishes field readiness nor carries into the
selected fixed-depth member-chain slice.

Virtual stepping, broad interpreter expansion, and additional product surfaces remain research rather than active
delivery work. Only the selected fixed-depth member-chain design slice is eligible for a separately contracted next
prototype milestone; it is selected, not implemented by W5.

The selected milestone is now specified by [`Post-W5 Path Forward`](post-w5-path-forward.md). This completed W5 plan
remains the authoritative record of why that bounded member-chain direction was chosen.
