# Fault-Injected Dump Corpus

> **Lifecycle:** Draft · **Roadmap:** Research
>
> **Decision proposed:** stand up an *adversarial capture corpus* — full-process dumps taken from rich applications
> after deliberately injected harsh failures (force-closed handles, terminated threads, abandoned locks, torn writes)
> — as a distinct evidence tier that stresses the read-only evaluator's degradation and non-admission paths, which
> today are exercised only by clean generated fixtures.
>
> **Not a roadmap commitment.** W8 remains the sole active delivery sequence. This is a research proposal for a
> post-W8 evidence category. It does not convert any generated fixture into representative field observation, and it
> introduces no product capability.

## 1. Why this exists

Every dump the project has evaluated through W8 is a **healthy process paused at a known point**: a compiler-emitted
target that prints `READY`, parks its threads, and is captured mid-stride with its runtime structures fully coherent.
That discipline is what makes the generated corpus replayable and its digests freezable, and it is correct for
proving *that a named physical fact can be read exactly*.

It also means the evaluator's most load-bearing safety machinery has never met an artifact it was designed for. The
product's entire honesty model exists for **messy** dumps:

- the twelve independent outcome axes (`DumpExpressionV2OutcomeAxes`) with their `Partial` / `Unavailable` /
  `Conflict` / `Invalid` / `NotReached` dispositions;
- the rule that **absence is never claimed over incomplete evidence** (an incomplete ancestry chain yields `Partial`,
  an incomplete interface closure yields `Unprovable`, never `Absent`/`NotAssignable`);
- **prefix-free typed stops** (a non-exact outcome retains no partial result vector);
- the runtime-construction rule that requires *exactly one* matching construction and reports `Ambiguous` otherwise;
- the frame-value non-admissions (register homes, selected-frame generic arguments) and the storage non-admission
  (`W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE`);
- the counted-read and capability-ledger machinery that proves a literal touches no runtime.

Against a clean fixture these paths are mostly *asserted by construction* — the fixture is built so the exact branch
fires. What has never happened is the evaluator meeting a **torn** MethodTable, a static slot half-written by a thread
that was killed mid-store, a selected frame that no longer exists because its thread was terminated, or a dump whose
capture was itself degraded because the process was losing handles as it was written. Those are precisely the inputs
that would tell us whether the degradation model *holds* or merely *compiles*.

Fault injection into rich applications is the cheapest way to manufacture that input at volume. It is also the most
direct attack on the standing caveat that **representative observation remains zero**: it does not close that gap —
these are still synthetic, author-provoked failures, not observed field incidents — but it moves the corpus decisively
closer to the shapes real crashes produce, and it does so under controlled, repeatable capture.

## 2. What a "harsh failure" actually buys, by evaluator path

Not every dramatic failure stresses *this* tool. The evaluator reads PE/metadata, ClrMD runtime structures, the
Portable PDB, and raw memory; it does not run the application's code. A failure is valuable to the extent it perturbs
those four surfaces. Three distinct value classes are worth separating explicitly, because they demand different
injection mechanisms and different acceptance criteria.

| Failure class | What it perturbs | Evaluator path stressed | Example injection |
|---|---|---|---|
| **Degraded capture** | The dump is incomplete, truncated, or internally inconsistent because the process was failing *as it was written*. | Metadata acquisition producing a typed catalog stop rather than a fabricated row; the producer's `MetadataImageUnreadable` / partial-read paths; ClrMD's own missing-region handling surfaced as `Unavailable`. | Close file/pipe handles the runtime and dump writer depend on, then capture during the resulting cascade. |
| **Inconsistent runtime state** | The managed runtime structures *disagree with each other* because a thread was stopped mid-mutation. | Runtime-construction selection returning `Ambiguous`/`Conflict`/`Unavailable` instead of a value; static-slot acquisition finding a half-written slot; the "exactly one construction" rule under a partially-registered available-type entry; assignability/closure walks over a torn base chain terminating typed rather than looping or fabricating. | `TerminateThread` on a worker mid-way through a generic type load or a static initializer. |
| **Hostile-but-valid shape** | Nothing is corrupt, but the runtime is in a legal state the generated fixtures never produce. | Deep/wide real object graphs, many coexisting generic instantiations sharing one TypeDef, STA/COM apartment threads, dispatcher frames, finalizer-queue state — exercising bounds, cap-plus-one accounting, and the candidate-keyed selection at realistic scale. | Simply dump a live WPF app under load; the "failure" is optional. |

The single most valuable injection for this tool is **thread termination that abandons a mutation**, because it is the
only cheap way to manufacture *inconsistent runtime state* — the class the degradation model most needs to face and the
one a clean fixture structurally cannot produce. A force-closed *file* handle, by contrast, mostly produces
*degraded capture*: it does not corrupt a MethodTable, but it does test whether the acquisition stack returns a typed
stop when the dump it is handed is missing regions it expects. Both are worth having; the proposal should not pretend
they stress the same thing.

## 3. The determinism reconciliation (the load-bearing decision)

The project is built on canonical byte-identical replay and frozen SHA-256 digests. Fault injection is inherently
nondeterministic: it races timing, thread scheduling, and allocator state. These two facts appear irreconcilable, and
mishandling the tension is the fastest way to produce a flaky corpus that poisons the whole test matrix.

The reconciliation is a clean layering:

> **Capture is nondeterministic. The captured dump is a frozen binary input. Replay over that fixed input is
> deterministic and freezable.**

Once a fault-injected dump exists as a file, it is *exactly as fixed* as any generated dump — a sequence of bytes the
evaluator reads read-only. Two evaluations of the same dump file must still produce byte-identical canonical outcomes,
and that property *is* freezable per dump. What is **not** freezable is the mapping from *"run the target with fault
profile X"* to *"a specific dump"* — different runs yield different (but individually valid) dumps.

This forces a two-tier acceptance model:

- **Generated corpus (existing):** the target is deterministic, so both the dump *and* the expected result are frozen.
  Golden digests pin exact values.
- **Adversarial capture corpus (proposed):** the dump is a captured artifact accepted by **invariants**, not by an
  expected value, because no one predeclared what a race would leave in memory. The evaluator's *behavior over* a
  captured dump is then deterministic and may be replay-frozen per artifact.

The invariants (Section 8) are the acceptance criteria. This is the entire reason the tier is separate: it trades
"we predicted the exact answer" for "we proved the evaluator never lies, never crashes, and always answers in the
typed vocabulary — whatever the failure did to memory."

## 4. Architecture

### 4.1 Two injection tiers, self-inflicted preferred

**Tier A — self-inflicted, in-target fault profiles (preferred).** Extend the existing target convention. Today a
target runs as `<target> --truth-gate <profile>`, prints `READY`, and parks. A fault target additionally accepts
`--fault <fault-profile>`: a controller thread inside the process drives the application to a labeled point, injects
the fault against *its own* process (a controller thread terminating a worker thread, closing its own handles,
corrupting its own shared state through an `unsafe` path), signals a distinct readiness marker, and parks so the
existing `DumpWriter.WriteFullDump` captures it. This keeps the whole flow headless, requires no cross-process
injection privilege, and is *as reproducible as fault injection can be* because the injection point is a known label
in the target's own code.

Self-infliction has a hard limit: a thread cannot reliably `TerminateThread` the thread it is running on. The pattern
is a **controller/worker split** — a supervisor thread parks the worker at a mutation label (via a manual-reset event
the worker signals just before entering the unsafe region), terminates it there, then triggers capture. This makes the
*torn state* deterministic in shape even though addresses vary.

**Tier B — external injection (secondary, quarantined).** A separate harness process closes handles or kills threads
in an unmodified rich application (WPF, a real service) via OS APIs — `DuplicateHandle` with `DUPLICATE_CLOSE_SOURCE`,
`NtClose` through injection, `TerminateThread` on a foreign thread, or a Process-Explorer-class handle closer. This is
strictly more realistic (the target is not built to be broken) and strictly less deterministic and more dangerous. It
is a research-only, quarantined tier that never gates CI and never contributes a frozen artifact until a captured dump
has been curated and pinned.

### 4.2 The fault agent

A small `PhoenixInspect.FaultAgent` library, linked into Tier-A targets, exposing labeled injections invoked by profile
name. Every injection:

- runs behind the existing hidden/windowless execution policy and the headless wrapper `eng/Invoke-HeadlessProcess.ps1`;
- is bounded (a global watchdog terminates the whole process group after a fixed wall-clock ceiling so a hung target
  from a bad injection cannot wedge the lane);
- honors the documented resource caps (8 GiB dump ceiling, 256 MiB ClrMD cache) so a rich-app dump cannot blow the
  budget;
- emits a structured *injection record* (fault profile, injection label, thread/handle identities affected, timestamp)
  alongside the dump, so the invariant lane knows *what was done* even though it cannot know *what resulted*.

### 4.3 Capture and isolation

Capture reuses `TestTargetRunner` + `DumpWriter`. Additions the fault path needs:

- **Process-group isolation:** each fault run is its own job object so a `TerminateThread` that deadlocks the target
  cannot orphan threads into the test host.
- **Zombie sweeping:** a fault that leaves the process alive-but-wedged must be reaped; the watchdog owns this.
- **Capture-time degradation is expected, not an error:** if the dump writer itself hits a closed handle mid-write, a
  *partial* dump is a valid artifact for the degraded-capture class — the lane records it as such rather than failing.

## 5. Fault catalog

Concrete injections, each tagged by value class (§2), tier (§4.1), and its principal determinism hazard. This is a
menu to draw a first corpus from, not a commitment to implement all of it.

| Fault | Mechanism | Value class | Tier | Determinism hazard |
|---|---|---|---|---|
| **Worker thread terminated mid-generic-load** | Controller `TerminateThread`s a worker parked just before it triggers a new closed generic construction. | Inconsistent runtime | A | Which structures are half-written varies; *shape* is stable, addresses are not. |
| **Thread terminated inside a static initializer** | Same, parked at a class-`.cctor` entry, leaving a static slot partially stored and the type's init lock abandoned. | Inconsistent runtime | A | High — the exact byte state of the slot varies. |
| **Abandoned monitor / SRW lock** | Terminate a thread holding a `Monitor` or `ReaderWriterLockSlim`, leaving shared state torn. | Inconsistent runtime | A | Medium. |
| **File / pipe / socket handle force-closed** | `CloseHandle`/`NtClose` on handles the runtime or a live channel depends on, then capture during the fault cascade. | Degraded capture | A / B | Low for the injection; the resulting cascade varies. |
| **Finalizer thread killed** | Terminate the finalizer thread with objects queued, leaving the finalizer queue and freachable state inconsistent. | Inconsistent runtime | A | Medium. |
| **AssemblyLoadContext unload mid-flight** | Trigger a collectible-ALC unload and capture while it is in progress — collectible constructions, loader allocators, and the available-type table in a transitional state. | Inconsistent runtime + hostile shape | A | High; also directly exercises the *nonzero* loader-allocator / load-context identity paths. |
| **Out-of-memory / commit failure** | Drive controlled allocation pressure until an `OutOfMemoryException` unwinds, capturing torn partially-constructed graphs. | Inconsistent runtime | A | High. |
| **Access violation via `unsafe` scribble** | Corrupt a known managed structure through a pinned pointer, then capture. | Inconsistent runtime | A | Author-controlled *what*, variable *when*. |
| **Live WPF app under load, no fault** | Dump a running WPF app with a populated visual tree, data-binding graph, dispatcher frames, and STA/COM threads. | Hostile-but-valid shape | A / B | The dump is the whole point; capture timing varies the graph. |

The `AssemblyLoadContext`-unload and static-initializer injections are the two highest-yield entries: the first is the
only mechanism that produces *transitional loader/allocator state* — which is exactly the identity surface W8.6c had to
have its contract corrected for (zero-address admission) — and the second is the cleanest way to manufacture a
half-written static slot, the archetypal *inconsistent runtime* input for a static-field evaluator.

## 6. Rich-application targets and why "rich" matters

Generated fixtures are deliberately small: a handful of TypeDefs, a few constructions, one or two threads. That is
right for isolating a physical fact but wrong for stressing bounds, cap-plus-one accounting, and candidate-keyed
selection at scale. Rich applications supply, *for free*, the shapes the fixtures cannot cheaply reach:

- **WPF** — the canonical rich target. A UI/dispatcher thread with STA apartment and COM interop, a deep visual tree,
  a data-binding dependency graph full of generic collection instantiations, background dispatcher frames, and a
  finalizer queue with real pressure. Its threading model alone (dispatcher, render thread, worker pool) produces
  selected-frame and thread-relative-storage inputs no generated target replicates. The headless constraint is
  satisfiable: WPF runs without ever calling `ShowDialog`/`Show`, or with an off-screen window, so no visible UI
  appears — consistent with the repository's hidden/windowless policy.
- **ASP.NET-shaped host** — a request-pipeline object graph, thread-pool concurrency, and pooled/scoped generic
  services; a natural home for the `Request`/`Batch`/`Coordinator`/`Workflow` shapes the W8 portfolio already uses.
- **Async/`Task`-heavy console** — state-machine structs, `ExecutionContext` chains, and continuations captured
  mid-flight; cheap to run headless and a good first non-WPF rich target.

"Rich" is not decoration: the coexistence of many generic instantiations sharing one TypeDef is exactly what makes the
runtime-construction *"require exactly one match, otherwise Ambiguous"* rule non-trivial, and a real WPF binding graph
manufactures that condition far more thoroughly than a four-construction generated coordinator.

## 7. What this is *not*

- **Not a fuzzer of the evaluator's parser.** Malformed *expression text* is a separate concern already covered by the
  syntax projectors' Invalid/Unsupported buckets. This corpus fuzzes the *dump*, not the query.
- **Not deliberate metadata-image corruption.** Scribbling random bytes into the PE/metadata is a different, lower-value
  activity: it tests the signature grammar's byte-level robustness (partly covered by the W8.1 altered-artifact
  matrix), not the runtime-degradation model. Fault injection targets *runtime and capture* state, leaving the
  immutable image intact — which is also what real crashes do.
- **Not field evidence.** These are author-provoked synthetic failures. They do not raise the representative-observation
  count above zero and must never be described as if they did.
- **Not a W8 deliverable.** W8 closes on its predeclared generated and meaningful-synthetic gates. This is a candidate
  *successor* direction only.

## 8. Acceptance model — invariants, not golden values

A fault-injected dump enters the corpus by surviving an **invariant lane**, not by matching a predeclared result.
Across a broad sweep of queries against the captured dump, every one of the following must hold:

1. **No crash, no hang, no unbounded read.** Every query terminates within the resource caps and returns.
2. **Always a typed outcome.** Every query produces one of the twelve-axis dispositions — never an untyped exception,
   never a default value standing in for an answer.
3. **No fabricated identity or value.** No construction, slot, field, or value is reported unless its complete evidence
   was consulted; a torn or missing structure yields `Partial`/`Unavailable`/`Conflict`/`Ambiguous`/`Invalid`.
4. **Every non-exact stop is prefix-free** and carries its retained evidence and first boundary.
5. **Non-admissions still hold.** A killed thread's frame yields the frame-value typed stop; a register home is still
   refused; a context-relative marker still returns `W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE`; a literal still makes zero
   runtime capability calls (verifiable with the existing poisoned-probe technique).
6. **Replay is byte-identical over the fixed artifact.** Two evaluations of the same captured dump, and a
   close/reopen/rebind cycle, produce identical canonical bytes — the per-artifact freeze the determinism model allows.
7. **Absence is never claimed over the incompleteness the fault created.** If the fault removed evidence, the outcome
   is a typed incompleteness, not `Absent`/`No`/`NotAssignable`.

A dump that *provokes an invariant violation* is the most valuable possible result: it is a genuine defect in the
degradation model, found exactly where clean fixtures cannot look. The lane records the violating dump, the query, and
the injection record as a reproduction seed.

## 9. Storage and CI

Full dumps of rich applications are large (hundreds of MB to gigabytes), which the repository cannot absorb into git.
Three options, in ascending commitment:

- **Generate-on-demand (default).** A dump lane builds the fault target, injects, captures to a scratch path, runs the
  invariant sweep, and discards the artifact — nothing is committed. This is the natural fit for the existing headless
  dump lanes and the 8 GiB ceiling, and it never bloats the repo. Its cost: the *specific* dump that found a violation
  is gone unless the injection record reproduces it, so the injection record must be rich enough to re-provoke the
  *class* of state.
- **Curated pinned artifacts.** A small number of dumps that exposed real behavior are stored out-of-band (an artifact
  store or LFS, not git) and referenced by content hash, becoming permanent replay fixtures. Requires a storage
  mechanism the repository does not yet have — an open decision.
- **Reduced captures.** Targeted minidumps carrying only the runtime regions the evaluator reads, small enough to
  commit. Attractive but depends on whether a reduced dump still satisfies the acquisition stack's region expectations
  — itself a research question this corpus could answer.

The default is generate-on-demand; curated pinning is added only when a specific dump proves worth keeping.

## 10. Relationship to the roadmap

This corpus is a natural **successor category** candidate under the meaningful-synthetic portfolio's own successor gate,
which explicitly admits "another observed boundary" as a qualifying direction. It is deliberately *not* pre-selected
here — the portfolio selects its successor from measured evidence, and this document does not pre-empt that decision.

It composes cleanly with what W8 landed rather than duplicating it: it reuses the metadata producer, the runtime
acquisition session, the composed pipeline, and the twelve-axis result contract unchanged, feeding them a new *class of
input* rather than adding a new *stage*. That is the test of whether it belongs: it adds evidence, not surface.

## 11. Open questions

1. **Storage.** Generate-on-demand only, or stand up an out-of-band artifact store for curated dumps? (Section 9.)
2. **External injection scope.** Is Tier B (breaking unmodified third-party apps) worth the privilege and
   nondeterminism cost, or is Tier A self-infliction sufficient to manufacture every value class?
3. **Target selection.** WPF first (richest, but heaviest and Windows-desktop-coupled), or an async console target first
   (cheapest to run headless) with WPF as the flagship second?
4. **Corpus size.** How many curated dumps constitute meaningful coverage of the failure classes without turning the
   lane into a multi-hour job?
5. **Reduced captures.** Can a region-targeted minidump satisfy the acquisition stack, making a small committed corpus
   viable?

## 12. Phased sketch

Evidence-led, mirroring the project's checkpoint discipline; each phase is independently useful and none is committed
until the prior one measures out.

- **F0 — invariant harness.** Build the invariant lane against the *existing* generated dumps first: a broad query
  sweep asserting invariants 1–7 with no fault injection. This validates the acceptance machinery on inputs whose
  answers are already known, before any nondeterministic capture exists.
- **F1 — first self-inflicted fault.** One Tier-A target, one high-yield injection (static-initializer thread
  termination), generate-on-demand, invariant lane only. Prove the loop end to end.
- **F2 — fault catalog breadth.** Add the ALC-unload, abandoned-lock, and degraded-capture injections across the
  four existing application shapes.
- **F3 — WPF flagship.** A headless WPF target under load, both as a hostile-valid-shape dump and a fault carrier.
- **F4 — curation decision.** Decide storage; pin any dump that exposed real behavior; feed findings back into the
  degradation model.

## 13. Success criterion

The corpus succeeds if it either (a) hardens the degradation model by exposing a real invariant violation that clean
fixtures could not reach, or (b) demonstrates, over a broad adversarial sweep, that the model holds under exactly the
inconsistent-runtime and degraded-capture states it was designed for — turning a *designed* honesty guarantee into a
*validated* one. Both outcomes are wins; only silence (a corpus too clean to stress anything) is a failure, which is
why thread-termination-with-abandoned-mutation, not handle closure, is the injection to lead with.
