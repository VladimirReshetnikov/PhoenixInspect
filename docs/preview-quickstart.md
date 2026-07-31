# Preview Quickstart

Status: Current · Active. Type: Guide.

PhoenixInspect opens a .NET memory dump read-only and answers C# expressions against the values and objects that
dump actually contains. This page gets you from a clone to your first answered expression, then says plainly what the
preview can and cannot answer.

## Requirements

- Windows. ClrMD dump loading and the desktop shell both require it.
- The .NET SDK pinned in [`global.json`](../global.json).

## Run the demo

```text
./eng/Invoke-PreviewDemo.ps1
```

That builds the console host and a sample service, starts the service, waits for it to reach a stalled state,
captures a full dump of it, and replays a narrated session against that dump. Pass `-KeepDump` to retain the dump
afterwards; it is deleted by default because it is roughly 70 MB.

The sample is [`samples/Contoso.OrderService`](../samples/Contoso.OrderService): an ordinary service whose order
batch stopped moving because no carrier ever accepted the hand-off. The session asks the questions you would ask.

```text
phoenix> eval Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount

  [exact]    Contoso.OrderService.Diagnostics.ServiceState.ProcessedOrderCount
    =   84213   [Int32]
    status  Exact  ·  Complete
    via Static field expression  ·  31.7 ms  ·  replay 6356ea9b…ffb93a5f
```

## Inspect your own dump

```text
dotnet run --project src/PhoenixInspect.Cli -- <path-to-your.dmp>
```

That starts an interactive session. `help` lists the commands; the ones you need first are `info`, `threads`,
`objects`, `root`, and `eval`.

If you do not have a dump yet, PhoenixInspect can take one:

```text
dotnet run --project src/PhoenixInspect.Cli -- capture --pid <processId> --output <path-to-your.dmp>
```

A dump from any other collector — Windows Error Reporting, Task Manager, `dotnet-dump collect`, `procdump -ma` — is
equally valid input. The capture command exists so a first session does not require installing a second tool.

For a window instead of a prompt, run [`src/PhoenixInspect.Wpf`](../src/PhoenixInspect.Wpf). Both hosts are described
in [`Hosts`](hosts.md).

## The shape of a session

A dump has no stack you can step and no object you can name until you name one. A session therefore works outward
from a name you already know:

1. **Start from a static field.** Ordinary static fields are the part of a service that stays readable without a
   stack, a thread, or a debugger. A fully qualified name binds from module metadata alone and needs no other
   evidence.

   ```text
   eval Contoso.OrderService.Diagnostics.ServiceState.BuildLabel
   ```

2. **Adopt an object as the expression root.** When a static field holds an object reference, its exact value can
   become the root of instance expressions. This is the practical way into an object graph, because the strong-handle
   catalog alone cannot reach an object that no handle roots.

   ```text
   root Contoso.OrderService.Diagnostics.ServiceState.Dispatcher
   ```

   An object found by `objects <ExactTypeName>` can be adopted the same way with `root <#>`.

3. **Ask about members of that object.** An expression whose first identifier is the root identifier is evaluated
   against that object; everything else is treated as a static-field expression.

   ```text
   eval root.CurrentBatch.BatchId
   eval root.LastFailure?.Code ?? "none"
   ```

4. **Ask what the session was allowed to use.** `status` prints the adopted root, the frame supplying name context,
   and the Portable-PDB candidates offered — so a transcript can be read back to see which evidence produced an
   answer.

## What the preview can answer

**Expressions.** One complete C# parse by the pinned `Microsoft.CodeAnalysis.CSharp` front end admits a versioned
subset:

| Shape | Example |
|---|---|
| Fully qualified static field | `Some.Namespace.Type.Field` |
| … with an explicit global qualifier | `global::Some.Namespace.Type.Field` |
| Static field with one member suffix | `Some.Namespace.Type.Field.Member` |
| Contextual static name, given a selected frame and its Portable PDB | `Statics.Field` |
| Root-relative member | `root.Member` |
| Root-relative two-member chain | `root.Member.Member` |
| Conditional access | `root.Member?.Member` |
| Coalescing with a literal | `root.Member?.Member ?? "fallback"` |

**Values.** `Int32`, `Nullable<Int32>`, bounded `String`, exact null, and validated object references. Anything else
is reported as a typed stop rather than approximated.

**Inspection.** Snapshot identity and target facts; managed module instances and their counted metadata content
identity; managed threads and frames with method names resolved from snapshot metadata; and bounded strong-handle
object search with the traversal counters that say how exhaustive it was.

## What the preview does not do

- **It does not run your code.** Nothing is resumed, stepped, mutated, or re-executed. Evaluation reads storage; it
  does not invoke property getters or methods to produce a value.
- **It does not guess.** A name the metadata does not declare, a byte range the snapshot does not contain, or a shape
  outside the admitted subset produces a typed non-exact outcome with a stable diagnostic code — never a fabricated
  zero, empty string, or null.
- **It is not a general expression evaluator.** Arithmetic, comparisons, indexers, casts, method calls, generics, and
  arbitrary chain depth are outside the current subset.
- **It does not read your disk to fill gaps.** Names and values come from the snapshot. A Portable PDB is consulted
  only when you offer one and only after its identity is validated against the module.

## Reading an answer

Every answer carries the product's own terminal status, the stage it was reached at, and — with `--verbose` or
`verbose on` — the binding facts, raw reads with requested versus observed byte counts, the deterministic bounds
actually reached, and stable diagnostic codes.

| Marker | Meaning |
|---|---|
| `[exact]` | Every required stage produced an exact result. |
| `[absent]` | An exhaustive search proved absence. This is a complete, useful answer. |
| `[partial]` | A bound or an incomplete byte source retained only a prefix of the required evidence. |
| `[stopped]` | Required evidence was missing, ambiguous, conflicting, or invalid. |
| `[rejected]` | The expression itself was outside the admitted subset. |

`[absent]` and `[partial]` are deliberately distinct. An exhaustively proven absence and a bounded partial read are
different facts, and collapsing them into "not found" would misrepresent what the snapshot supports.

## Status of this preview

This is an early preview of an evidence-led project. The supported subset is narrow on purpose: everything listed
above is backed by executable tests over real dumps, and nothing is listed that is not. The
[integration test plan](proposals/integration-test-plan.md) is the authority on what has been proven, and the
[documentation index](README.md) records the milestone history behind it.
