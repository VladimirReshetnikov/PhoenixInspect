# Internal Demo Host: PhoenixInspect Explorer

Status: Current · Active. Type: Guide.

`src/PhoenixInspect.Explorer.Wpf` is an internal WPF host for exercising the implemented dump-inspection and
expression-evaluation contracts interactively. It exists so a reviewer can open a real dump, see what the current
build can and cannot answer, and read the evidence behind each answer without writing a test.

## What it is not

- **Not a released product.** There is no compatibility promise, no installer, no telemetry, and no support story.
- **Not new capability.** The host adds no analysis of its own. Every fact it renders comes from an existing public
  contract and is rendered verbatim, including the product's own status, issue, completeness, and bound vocabulary.
- **Not evidence.** Interactive exploration is not a validation tier. The
  [`integration test plan`](proposals/integration-test-plan.md) remains the authority on what has been proven.

## Running it

```text
dotnet run --project src/PhoenixInspect.Explorer.Wpf -- <path-to-dump.dmp>
```

The path argument is optional; a dump can also be chosen through **Open dump…** or dropped onto the window. The host
requires Windows because ClrMD dump loading and WPF both do.

Only one session is open at a time, and every adapter call runs on one dedicated worker thread. The ClrMD adapter
exposes an immutable snapshot but does not promise concurrent use, so the host serializes access rather than
introducing a usage pattern the libraries do not support.

## Sections

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
`Some.Namespace.Type.Instance`, then adopt the resulting object and evaluate members relative to it.

## Reading a result honestly

The host deliberately does not reduce an outcome to success or failure. Each result carries:

- the product's terminal **status** and the **stage** it was reached at;
- the **value**, or an explicit statement that no value was produced;
- **binding facts** for every stage that ran, including counts of attempted name expansions, located candidates, and
  rejected declarations;
- **raw reads** with requested versus observed byte counts, so a partial read is visible as a partial read;
- only the **deterministic bounds actually reached**, never bounds that guarded an unvisited path;
- **stable diagnostic codes** for any non-exact outcome;
- a **canonical replay digest** where the contract publishes one.

An exhaustively proven absence and a bounded partial answer are shown as distinct outcomes with distinct colours,
because the product treats them as distinct and conflating them would misrepresent its result axes.

## Known limits of the host

- The adapter selects one frame at a time by snapshot-scoped ordinal and publishes no thread count. The call-stack
  panel therefore probes ordinals, and it states explicitly that it cannot distinguish a past-the-end ordinal from a
  live thread with no managed frames: both return the same typed unavailable observation.
- Frames beyond the first are loaded when a thread is expanded, so a wide probe stays cheap.
- The observable value domain is limited to what the product admits: null, `Int32`, `Nullable<Int32>`, bounded strings,
  and validated object references. Anything else surfaces as a typed stop.
