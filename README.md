# PhoenixInspect

**Post-mortem .NET inspection: open a memory dump and ask it C# questions.**

[![CI](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/workflows/ci.yml)
[![License: MIT-0](https://img.shields.io/badge/license-MIT--0-blue)](LICENSE)
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

![The PhoenixInspect desktop shell over the demo dump: modules docked left, the session document center, the evidence pane right, and a full-width bottom band holding the evaluation console beside the Call Stack and Threads tabs](docs/assets/desktop-shell.png)

*The desktop shell over the demo dump. The adopted `Program.Main` frame supplies namespace and import context, so
`ServiceState.BuildLabel` binds the way the source writes it — and the Result pane carries the complete evidence
behind the exact answer: binding facts, raw reads, reached bounds, and the canonical replay digest.*

## What the preview answers today

Everything below is backed by executable tests over real dumps, and nothing is listed that is not. The
[preview quickstart](docs/preview-quickstart.md) is the complete, authoritative statement of the supported surface.

- **Dump files or a live process.** Open an immutable dump, or pick a target from the Processes pane — a dockable
  list of the running .NET processes, each row stating how it was recognised as managed — and attach to it,
  suspended for the lifetime of the session and resumed when it closes. Either way you ask the same questions. The Snapshot pane states
  which source answered and what that means for identity: a dump has a content digest that replays anywhere, a live
  session has an attach-circumstances digest that replays within the session.
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
  read as immutable virtual sequences with the deterministic `System.Linq.Enumerable` surface — including
  expression lambdas: `root.DurationsMs.Where(ms => ms > 1000).Select(ms => ms / 1000.0).ToArray()`,
  `xs.Count(x => x is > 1 and < 9)`, `xs.OrderBy(x => -x)`, `xs.Sum(x => x * x)`.
- **Dump values compose with constants.** `root.QueueDepth * 2 + 1` resolves the dump value through the same
  frozen pipeline that answers it alone, then folds the arithmetic.
- **Date and time computations.** `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, and `TimeOnly` with
  exact BCL semantics: constructors and ticks, deterministic factories, calendar/clock members, the full
  operator algebra (`end - start`, `span * 2`, comparisons), and invariant formatting —
  `TimeSpan.FromMilliseconds(root.DurationsMs.Max()).TotalSeconds`. `Now`, local-time-zone conversions, and
  culture-dependent parsing are typed stops, because a post-mortem answer must never depend on when or where
  the dump is inspected.
- **Deterministic BCL values.** `Guid` and `Version` with fixed-grammar construction and parsing, comparisons,
  and invariant formats — `Guid.Parse(root.Batch.Id) == new Guid("…")`-style checks without leaving the
  evidence domain; `Guid.NewGuid()` is a typed stop, because a freshly generated value is not evidence.
- **The complete Unicode character surface.** Every `char` static in both spellings —
  `char.IsDigit("a5b", 1)`, `GetUnicodeCategory` with the `UnicodeCategory` enum, numeric values, the ASCII
  family, UTF-32 conversions, surrogate pairing — plus `CharUnicodeInfo` and first-class `System.Text.Rune`
  values: construction from chars, scalars, or surrogate pairs, the explicit `(Rune)`/`(int)`/`(char)`
  conversions, classification and invariant case mapping, ordering, and `"text".EnumerateRunes()` as a
  LINQ-composable sequence. All answers read the pinned runtime's Unicode tables — fixed data, never culture
  state.
- **Text encodings and regular expressions.** The `System.Text.Encoding` singletons with exact transcoding —
  `Encoding.UTF8.GetBytes(root.Batch.Id)`, `GetString`, `GetByteCount`, `Encoding.Convert` — and the full
  `Regex` family with BCL semantics: statics and instances, `Match`/`Groups`/`Captures` (named and numbered),
  `Replace`, `Split`, `Escape`, and match collections that compose with the LINQ surface
  (`Regex.Matches(root.Log, @"\d+").Select(m => m.Value)`). Case-insensitive matching without
  `RegexOptions.CultureInvariant` is a typed culture stop, and a catastrophically backtracking pattern stops
  with a fixed one-second budget named instead of hanging the prompt.
- **Full enum semantics, `typeof`, and the `System.Enum` API.** Casts both ways, the flags algebra
  (`a \| b`, `~x`, `HasFlag`), enum formats, `typeof(...)` references, and
  `Enum.GetNames`/`GetValues`/`IsDefined`/`Parse` in generic and `typeof` spellings — with enum shapes read
  from the dump module's own metadata, so `(Contoso.Status)2` names the member the captured assembly declares.
- **Array creation and the pure `System.Array`/`System.Type` API.** Every creation spelling — sized with C#'s
  zero-fill semantics (`new int[3]`), initializers, typed empties, collection expressions with spreads
  (`[1, .. root.DurationsMs, 9]`), `Array.Empty<T>()` — plus `Array.IndexOf`/`BinarySearch`/`Find`-family
  statics, instance members like `Rank`/`GetValue`/`GetUpperBound`, and the deterministic `Type` surface
  (`IsValueType`, `GetElementType`, `MakeArrayType`, `GetEnumNames`). Mutators such as `Array.Sort` are typed
  stops: the evaluator observes evidence, it never rearranges it.
- **Reflection over the modeled universe.** Public member info queries on `typeof(...)` references —
  `GetMethods()`, `GetProperty("Length")`, `GetConstructors()` — return read-only `MethodInfo`/`PropertyInfo`/
  `FieldInfo`/`ParameterInfo` values that compose with LINQ
  (`typeof(Math).GetMethods().Where(m => m.GetParameters().Length == 2)`), plus `GetType()` on any folded value
  and `Activator.CreateInstance` in both spellings. Invocation routes through the evaluator's own dispatch
  tables, so `GetMethod("Sqrt").Invoke(null, new object[] { 2.0 })` computes exactly what `Math.Sqrt(2.0)`
  folds to — and `Invoke` on a culture-sensitive or non-deterministic member hits the same typed stop as the
  direct spelling; reflection can never widen what evaluates. Member lists are canonically ordered, mutation
  (`SetValue`) is refused, and info reads are metadata facts of the pinned analysis runtime.
- **Virtually created delegates.** `Action`/`Func`/`Predicate`/`Comparison` values from every C# spelling —
  `new Func<int, int>(x => x + 1)`, `(Action)(…)` casts, method groups (`new Func<double, double>(Math.Sqrt)`,
  `(Func<string, bool>)"hello".Contains` with exact-signature overload selection), typed lambda declarations
  (`Func<int, int> f = x => x + 1;`), and the `CreateDelegate` family over reflection `MethodInfo`s — invoked
  through `f(3)`, `.Invoke`, or `DynamicInvoke`, with closures captured at creation. The multicast algebra is
  the runtime's own: `+`/`Combine`, `-`/`Remove`/`RemoveAll` with last-occurrence sublist semantics, list
  equality for `==`, plus `Method`, `Target`, `HasSingleTarget`, and `GetInvocationList()`. Every entry folds
  through the evaluator's own dispatch, so a delegate can never compute anything the direct spelling could not
  — culture and non-determinism stops hold identically through a delegate call.
- **Reference conversions with runtime semantics.** Upcasts fold to the same value — `(object) x`, a delegate
  to `Delegate`, a `Match` to `Capture`, an array to `IEnumerable<T>` — and downcasts check the operand's
  exact runtime identity: `(string)(object)"abc"` and `(DayOfWeek)(object)DayOfWeek.Friday` round-trip, while
  an incompatible cast stops with the runtime's own `InvalidCastException` and unboxing null with its
  `NullReferenceException`.
- **Type relationships and generic construction.** The runtime's exact assignability relation —
  `IsAssignableFrom`/`IsAssignableTo` with base chains, `Nullable<T>` lifting, array covariance, implemented
  interfaces, and declared variance — plus `IsSubclassOf`, `BaseType`, `IsInstanceOfType`, and generic types in
  both spellings: `typeof(List<int>)`/`typeof(List<>)` and `MakeGenericType` with the BCL's failure semantics,
  alongside `GetGenericTypeDefinition` and the `IsGenericType` introspection family.
- **C# query expressions and anonymous types.** The full query grammar — `from`, `where`, `let`, `orderby`,
  `group … into` with real groupings, `join`/`join … into`, continuations, and range-variable casts — translated
  by the specification's own rules onto the folded operator surface, with anonymous objects (`new { x, Total }`)
  as a first-class value domain: projected names, value equality, invariant `ToString`, and expandable members.
- **Modern C# expression forms.** Interpolated strings with invariant formatting and alignment
  (`$"depth {root.QueueDepth,4}"`), `is` patterns and `switch` expressions over constant, relational, and
  `and`/`or`/`not` patterns (`root.QueueDepth switch { > 10 => "busy", _ => "idle" }`), plus `nameof`,
  `default(T)`, `sizeof(T)`, and `checked`/`unchecked` wrappers — all deterministic, with unsupported pattern
  kinds reported as typed stops.
- **A Watch window with editable expressions.** Visual Studio's shape: edit any row in place, add a watch through
  the trailing row, delete by clearing, and every row re-evaluates when the adopted frame context or expression
  root changes. Routing between the static-field and root-relative paths is one deterministic API rule, and every
  row carries the full evidence report behind its value.
- **An Immediate window.** A prompt-and-transcript console beside the Watch window: expressions evaluate through
  the same routed entry point, answers append to a C#-syntax-colored transcript with their kind as a trailing
  comment, and explained stops render as comments rather than bare errors. History recall and the shared
  completion drop-down included.
- **Completion as you type, from evidence.** The Watch window offers a Visual Studio-style drop-down: C# keywords,
  the evaluator's modeled types and their members, the adopted root's declared fields read from its validated
  runtime type, and namespaces, types, and static fields enumerated from dump-module metadata. The candidate
  universe is exactly the evaluator's own — completion never suggests a name the product cannot answer about.
- **Expandable compound values, and tuples.** Arrays and tuples expand in the Watch grid the way Visual Studio
  expands them — `[i]` child rows for elements, `ItemN` or the declared name for tuple elements, recursively for
  nested compounds — with the children realized by the API as a bounded projection of the same evidence the
  rendered value shows. Tuple literals fold with C#'s exact semantics: element names, `ItemN` access, element-wise
  equality, and invariant `ToString`.
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
  [Post-W7 Path Forward](docs/plans/post-w7-path-forward.md). W8.1 through W8.8 are complete for their defined evidence,
  contract, acquisition, binding, runtime, and composition scopes. W8.9 preserves the frozen v1 record at 22 executed /
  13 manifest-only and adds a content-addressed corrected v2 with 35/35 independently executed baselines and zero
  manifest-only rows. The separate decision candidate
  `tests/corpus/w8-static-field-portfolio-decision-candidate-v1.json` (SHA-256
  `a6b35b67d35c00449dac632dc61ed4b269e9bfd679552a1e8dbea4cc34a20450`) conditionally computes 33 useful / 19
  decision-changing / 25 attributable incidents. It proposes retiring rows 20, 21, 22, 26, and 27; deferring rows 29
  and 34 with zero decision credit; and selecting `observed-boundary-hardening` with action “Harden the observed
  first-boundary reporting surface without extending binding reach.” The candidate is pending explicit owner approval,
  carries no owner authority, and closes neither W8.9 nor W8.10; W8 implements none of the proposed action. The distinct
  authority envelope `tests/corpus/w8-static-field-portfolio-decision-authority-v1.json` is absent and, if approved,
  must be created separately and bind the exact candidate SHA above. An authority-absent technical preflight at exact
  commit `6acfef2808d85e69f36f356813a9337507ea5b7b` passed all 22 commands and all 3,430 test executions across 11
  strict TRX lanes with zero failures or skips. Separate hosted
  [CI run 31618781452](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/31618781452) passed all six
  jobs and 2,129/2,129 hosted TRX executions at that same commit. These pre-authority technical checkpoints do not
  approve W8.9, satisfy the future authority-bound W8.10 local run and exact-commit hosted rerun, or close either
  milestone; the local record itself embeds no hosted-evidence claim.

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

[`MIT-0`](LICENSE).
