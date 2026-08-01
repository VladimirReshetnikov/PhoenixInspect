# PhoenixInspect

**Post-mortem .NET inspection: open a memory dump and ask it C# questions.**

[![CI](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/workflows/ci.yml)
[![License: Unlicense OR MIT-0](https://img.shields.io/badge/license-Unlicense%20OR%20MIT--0-blue)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4)
![Status: early preview](https://img.shields.io/badge/status-early%20preview-orange)

A dump is a process reduced to ashes. PhoenixInspect restores enough structure to walk through it — modules, threads
and named frames, rooted objects — and answers C# expressions against the values and objects the snapshot actually
contains. The resemblance to a live debugger is deliberate, because that is the shape a post-mortem user already
thinks in. It stops at the evidence boundary: nothing is resumed, stepped, mutated, or re-executed, and an answer the
snapshot cannot support is reported as a typed limit rather than filled in.

## Try it

```powershell
./eng/Invoke-PreviewDemo.ps1
```

One command builds a sample order-fulfillment service, stalls it on a batch no carrier ever accepted, captures a full
dump, and replays a narrated inspection session against that dump:

```text
phoenix> eval Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount

  [exact]    Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount
    =   84213   [Int32]
    status  Exact  ·  Complete
    via Static field expression  ·  31.7 ms  ·  replay 6356ea9b…ffb93a5f

phoenix> root Contoso.OrderService.Diagnostics.ServiceState.Dispatcher
  Root is Contoso.OrderService.Fulfillment.ShipmentDispatcher @ 0x000001C49700A0A8

phoenix> eval root.CurrentBatch.BatchId

  [exact]    root.CurrentBatch.BatchId
    =   "batch-2026-07-30-0042"   [String (length 21)]

phoenix> eval root.CurrentBatch.Route.Corridor.Name

  [exact]    root.CurrentBatch.Route.Corridor.Name
    =   "NL-BE overnight corridor"   [String (length 24)]

phoenix> eval root.AssignedCarrier?.Name ?? "no carrier ever accepted the batch"

  [exact]    root.AssignedCarrier?.Name ?? "no carrier ever accepted the batch"
    =   "no carrier ever accepted the batch"   [String (length 34)]

phoenix> eval System.DayOfWeek.Monday

  [exact]    System.DayOfWeek.Monday
    =   DayOfWeek.Monday (1)   [Enum System.DayOfWeek · underlying Int32]

phoenix> eval root.RetryBudgetRemaining

  [stopped]  root.RetryBudgetRemaining
    =   No value was produced.
    DUMP_FIELD_UNAVAILABLE  The requested runtime field is unavailable.
```

To open your own dump:

```bash
dotnet run --project src/PhoenixInspect.Cli -- <path-to-your.dmp>
```

The [preview quickstart](docs/preview-quickstart.md) explains the session model, the exact expression subset and
value domain supported today, what the preview deliberately does not do, and how to read a non-exact answer.
[`Hosts`](docs/hosts.md) covers the console host and the Windows desktop shell — a docked debugger-style workspace
that can also resolve a selected stack frame to its source through an identity-validated Portable PDB, rendering the
file only when its bytes reproduce the PDB's document checksum.

## What the preview answers today

Everything below is backed by executable tests over real dumps, and nothing is listed that is not. The
[preview quickstart](docs/preview-quickstart.md) is the complete, authoritative statement of the supported surface.

- **Static fields by name, no debugger context needed.** A fully qualified name
  (`Some.Namespace.Type.Field`) binds from module metadata alone; adopting a stack frame and its
  identity-validated Portable PDB additionally lets names be written the way the source writes them.
- **Object-graph walks of any depth.** Adopt an object as `root`, then evaluate member chains with
  conditional access and coalescing: `root.CurrentBatch.Route.Corridor.Name`,
  `root.LastFailure?.Code ?? "none"`. Every hop is validated against counted snapshot evidence.
- **The full C# numeric tower, folded exactly.** Constant arithmetic across every fixed-size integral type,
  `Int128`/`UInt128`, `BigInteger`, IEEE-754-faithful `float`/`double`, and exact-scale `decimal`, with C#
  promotion and checked semantics; `System.Math`, numeric statics, and invariant `ToString` included.
- **Deterministic string, char, and sequence operations.** The culture-independent `string`/`char` surface,
  index and range slicing (`"hello"[^1]`, `root.Batch.Id[6..^5]`), array initializers, and dump-heap arrays
  read as immutable virtual sequences with the lambda-free `System.Linq.Enumerable` surface
  (`root.DurationsMs.Max()`, `xs.Distinct().Order()`).
- **Dump values compose with constants.** `root.QueueDepth * 2 + 1` resolves the dump value through the same
  frozen pipeline that answers it alone, then folds the arithmetic.
- **Answers you can audit.** Every result carries its status (`[exact]`, `[absent]`, `[partial]`, `[stopped]`,
  `[rejected]`), the binding facts and raw reads behind it, stable diagnostic codes, and a canonical replay
  digest. An exhaustively proven absence and a bounded partial read are reported as the distinct facts they are.
- **Source, verified before it is shown.** The desktop shell renders a frame's source file only when its bytes
  reproduce the Portable PDB's document checksum; otherwise it falls back to C# decompiled from the module's IL
  by the ILSpy engine, explicitly labelled as a reconstruction.

What it deliberately does not do: run your code, guess at missing evidence, or read your disk to fill gaps. The
[quickstart](docs/preview-quickstart.md#what-the-preview-does-not-do) states the non-goals precisely.

## Project gist

We are designing and developing a library and architecture that can power experiences such as:

- post-mortem expression evaluation,
- virtual stepping (Step Into/Over/Out) over dump-backed sessions,
- explainable analysis when runtime behavior cannot be reproduced exactly.

Core principles:

- **Deterministic and budgeted execution** over unbounded simulation.
- **Conservative behavior** over risky “best effort” guessing.
- **Explainability and provenance** over opaque results.
- **Composable architecture** so hosts can integrate incrementally.

## Status

- **Phase:** early development, progressing toward a first public preview through evidence-led vertical slices
  (W1–W8). Each slice closes only when its behavior is proven by headless tests over real dumps, and each claim
  in the preview documentation is backed by a passing test.
- **Closed:** W1–W7 — real dump evidence and typed outcomes; the restricted dump-query v1; concrete IL execution;
  counterfactual method evaluation; the expression-to-result product path; fixed-depth member chains over the
  pinned Roslyn front end; and static-field expressions with selected-frame/Portable-PDB context.
- **Active:** W8 — the additive `StaticFieldExpressionV2` design under the
  [Post-W7 Path Forward](docs/plans/post-w7-path-forward.md). W8.1 (physical-truth evidence) is complete;
  W8.2 (immutable contracts and source-anchored metadata proofs) is the active checkpoint.

The complete delivery ledger — every closed milestone with its exact closure commits, verification counts, hosted CI
runs, and realized-scale accounting — is the [milestone history](docs/plans/milestone-history.md). Current evidence
covers the named generated fixtures and explicitly admitted input shapes; this is early-development evidence, not a
production-ready evaluator or interpreter.

## Tentative future directions

The project's original planned scope was broader than the currently funded dump-evaluator slice: a
multi-application execution platform. That scope is retained as **tentative future directions** — research-gated
possibilities, not active implementation commitments:

- **Virtual debugging:** counterfactual Step Into/Over/Out over dump snapshots plus virtual state
  ([feature proposal](docs/proposals/product/virtual-step-debugging-feature-proposal.md),
  [implementation proposal](docs/proposals/architecture/virtual-step-debugging-implementation-proposal.md)).
- **Static analysis:** whole-method abstract interpretation over the same IL semantics
  ([abstract-domain proposal](docs/proposals/architecture/mvp-abstract-domain-proposal.md)).
- **Sandboxed IL interpretation:** bounded no-JIT execution hosts reusing the interpreter core
  ([applications note](docs/proposals/product/other-potential-applications.md)).
- **Live speculative debugging, async/dynamic lifting, and semantic modeling**
  ([virtual tasks](docs/proposals/architecture/virtual-tasks-proposal.md),
  [dynamic calls](docs/proposals/architecture/dynamic-calls-proposal.md),
  [semantic modeling](docs/proposals/architecture/semantic-modeling-proposal.md)).

The original pre-redesign design documents for this scope are restored under
[`docs/proposals/original/`](docs/proposals/original/README.md). They may be outdated and need revision to match
the architecture that has crystallized since then; current documents and executable evidence are authoritative on
any conflict. The removed placeholder projects are not being recreated ahead of evidence.

## Repository tour

| Path | What it holds |
|---|---|
| [`src/`](src) | Thirteen source projects: core execution and metadata contracts, the SRM metadata reader, the ClrMD dump adapter, the dump-query and dump-debugging product facades, the host-independent inspection layer, and the console ([`PhoenixInspect.Cli`](src/PhoenixInspect.Cli)) and desktop ([`PhoenixInspect.Desktop`](src/PhoenixInspect.Desktop)) hosts. |
| [`samples/`](samples) | [`Contoso.OrderService`](samples/Contoso.OrderService), the preview demo target: an ordinary service frozen in one deterministic stalled state so a dump captured on any machine answers the same expressions the same way. |
| [`tests/`](tests) | Unit, integration, and real-dump evidence suites, plus the generated fixture targets they capture and inspect. Every preview claim traces to a test here. |
| [`docs/`](docs) | The design and evidence record: [quickstart](docs/preview-quickstart.md), [hosts guide](docs/hosts.md), [plans](docs/plans), [proposals](docs/proposals), [governance](docs/governance), and the [documentation index](docs/README.md). |
| [`eng/`](eng) | The preview demo, headless process wrapper, and repository verification scripts used locally and in CI. |

## Where to go next

To **use** PhoenixInspect:

1. [Preview quickstart](docs/preview-quickstart.md) — run the demo, open your own dump, and see exactly what is
   supported today.
2. [Hosts](docs/hosts.md) — the console host and the desktop shell.

To **understand or contribute to** the project:

- [Documentation index](docs/README.md) — the canonical inventory of topics and reading paths.
- [Milestone history](docs/plans/milestone-history.md) — the complete delivery ledger behind the preview.
- [Post-W7 Path Forward](docs/plans/post-w7-path-forward.md) — the active W8 design sequence.
- [Integration test plan](docs/proposals/integration-test-plan.md) — the authority on what has been proven.
- Normative contracts: [W2 restricted dump query](docs/proposals/architecture/restricted-dump-query-contract-proposal.md),
  [W3 concrete IL execution](docs/proposals/architecture/concrete-il-execution-contract-proposal.md),
  [W4 counterfactual method evaluation](docs/proposals/architecture/counterfactual-method-evaluation-contract-proposal.md),
  [C# expression front end](docs/proposals/architecture/csharp-expression-front-end-contract-proposal.md).
- [Project FAQ](docs/governance/project-faq.md) — scope, expectations, and direction.

## Contribution focus (this phase)

High-value contributions advance or challenge the active executable evidence rather than widening speculative
surface area:

- strengthen dump reads, identity joins, partial evidence, and truthful failure behavior within the supported
  generated fixture boundary;
- preserve the closed W2–W7 contracts — the pinned Roslyn front end as the sole expression parser, versioned subset
  admission, no-rebinding plans, counted evidence, and no-fallback binding — and extend them only when a concrete
  incident scenario justifies the next versioned step;
- implement or challenge W8's additive V2 plan as one coherent bounded name-binding pipeline;
- add deterministic, differential, and scenario tests at proven boundaries;
- tighten architecture and documentation when executable evidence changes a decision;
- keep design work just ahead of code rather than expanding speculative surface area.

The [milestone history](docs/plans/milestone-history.md) records the per-milestone boundaries this phase preserves,
and the [documentation index](docs/README.md) lists the contracts in force.

## License

This repository is released into the **public domain** under [the Unlicense](https://unlicense.org/) in
jurisdictions that recognize and permit that dedication. Where such a dedication is not legally effective, the
**MIT No Attribution (MIT-0)** license applies instead. See [`LICENSE`](LICENSE) for both texts and how they
compose. In SPDX terms: `Unlicense OR MIT-0`.
