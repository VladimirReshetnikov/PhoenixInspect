# PhoenixInspect Hosts

Status: Current · Active. Type: Guide.

PhoenixInspect ships two hosts over the same implemented contracts. Both open one dump read-only and present it as an
inspectable session: modules, threads and frames, rooted objects, and C# expressions answered from the values and
objects the snapshot actually contains.

| Host | Project | Use it for |
|---|---|---|
| Console | [`src/PhoenixInspect.Cli`](../src/PhoenixInspect.Cli) | Scripted and interactive sessions, transcripts, CI. |
| Desktop shell | [`src/PhoenixInspect.Wpf`](../src/PhoenixInspect.Wpf) | Browsing a dump interactively on Windows. |

Neither host adds analysis of its own. Every fact both render comes from an existing public contract by way of
[`src/PhoenixInspect.Inspection`](../src/PhoenixInspect.Inspection), the shared host-independent projection layer, and
is rendered verbatim — including the product's own status, issue, completeness, evidence, bound, and diagnostic
vocabulary.

## The resemblance, and where it stops

A dump is not a live process. The hosts deliberately borrow a debugger's shape because that is the shape a
post-mortem user already thinks in, but the resemblance is presentational. Nothing resumes, mutates, or infers past
execution; every value is read from the snapshot under explicit bounds. Where a live debugger would simply evaluate,
PhoenixInspect reports what the evidence supported: an exact value, an exhaustively proven absence, a bounded partial
read, or a typed stop with a stable diagnostic code.

## Console host

```text
dotnet run --project src/PhoenixInspect.Cli -- <path-to-dump.dmp>
```

With no `--eval`, `--command`, or `--script`, the session starts an interactive prompt. `help` lists the commands.

| Option | Effect |
|---|---|
| `--eval <expression>` | Evaluate one expression, then continue. May be repeated. |
| `--command <text>` | Run one session command. May be repeated; order is preserved. |
| `--script <path>` | Run session commands from a file, one per line; `#` starts a comment. |
| `--verbose` | Print the complete evidence behind every answer. |
| `--no-color` | Suppress ANSI styling. |

Exit codes are `0` for a completed session, `2` for a usage error, `3` when the dump could not be opened exactly, and
`4` when a scripted command was rejected or could not run. A non-exact *answer* is not a failure and does not change
the exit code: it is a reported limit of the evidence, and the session summary counts it separately.

## Desktop shell

```text
dotnet run --project src/PhoenixInspect.Wpf -- <path-to-dump.dmp>
```

The path argument is optional; a dump can also be chosen through **Open dump…** or dropped onto the window. The shell
requires Windows because ClrMD dump loading and WPF both do.

Only one session is open at a time, and every adapter call runs on one dedicated worker thread. The ClrMD adapter
exposes an immutable snapshot but does not promise concurrent use, so both hosts serialize access rather than
introducing a usage pattern the libraries do not support.

| Section | Contracts exercised | What it shows |
|---|---|---|
| Overview | `ClrmdDumpSession.Open`, `Snapshot`, `TargetPlatform`, `TargetArchitecture`, `Modules` | Dump content identity (SHA-256 of the file, excluding the local path), target facts, the deterministic caps this build declares, the pinned expression front end, and a plain-language statement of the supported surface. |
| Modules | `Modules`, `ReadModuleContentIdentity` | Every managed module instance with its metadata root, reported length, and image layout. Selecting one reads its counted metadata from dump memory and reports the MVID, counted length, metadata digest, raw reads, and applied bounds. |
| Call stacks | `SelectExpressionFrame` | Bounded managed frames with their MethodDef/TypeDef tokens, declaring namespace, IL offset, and instruction pointer. A selected frame can be adopted as the name-binding context of the static-field path. |
| Heap objects | `FindStrongHandleObjectsByTypeName` | A bounded strong-handle search over an exact ordinal type-name predicate, with the traversal counters and caps that say how exhaustive the result actually was. A match can be adopted as an expression root. |
| Evaluate | `StaticFieldExpressionEvaluator`, `DumpExpressionEvaluator` | Both implemented expression entry points, each answer shown with its status, stage, value, raw reads, applied bounds, stable diagnostics, and canonical replay digest. |

## Selecting an expression root

The root-relative path needs one exact object. Two supported sources are offered, because the strong-handle catalog
alone cannot reach an object that no handle roots:

1. A match from a bounded strong-handle search, bound through `DumpQueryRootBinding.FromExactObject`.
2. The exact object value of a static-field expression, projected through
   `ClrmdDumpSession.ProjectExactObjectForInstanceEvaluation` and bound through
   `DumpQueryRootBinding.FromObjectBinding` so its authoritative typed provenance is preserved rather than restated as
   a handle.

The second source makes a static field the practical entry point into instance evaluation: evaluate
`Some.Namespace.Type.Instance`, then adopt the resulting object and evaluate members relative to it. In the console
host that is `root Some.Namespace.Type.Instance`, after which `root.Member` evaluates against that object.

## Reading a result honestly

Neither host reduces an outcome to success or failure. Each result carries:

- the product's terminal **status** and the **stage** it was reached at;
- the **value**, or an explicit statement that no value was produced;
- **binding facts** for every stage that ran, including counts of attempted name expansions, located candidates, and
  rejected declarations;
- **raw reads** with requested versus observed byte counts, so a partial read is visible as a partial read;
- only the **deterministic bounds actually reached**, never bounds that guarded an unvisited path;
- **stable diagnostic codes** for any non-exact outcome;
- a **canonical replay digest** where the contract publishes one.

An exhaustively proven absence and a bounded partial answer are shown as distinct outcomes, because the product treats
them as distinct and conflating them would misrepresent its result axes.

## Known limits

- The adapter selects one frame at a time by snapshot-scoped ordinal and publishes no thread count. Both hosts
  therefore probe ordinals, and state explicitly that they cannot distinguish a past-the-end ordinal from a live
  thread with no managed frames: both return the same typed unavailable observation.
- The observable value domain is limited to what the product admits: null, `Int32`, `Nullable<Int32>`, bounded
  strings, and validated object references. Anything else surfaces as a typed stop.
- Interactive exploration is not a validation tier. The [`integration test plan`](proposals/integration-test-plan.md)
  remains the authority on what has been proven.
