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

For a window instead of a prompt, run [`src/PhoenixInspect.Desktop`](../src/PhoenixInspect.Desktop). Both hosts are described
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

4. **Or stand where the code stood.** Adopting a frame supplies its namespace, imports, and aliases as name
   context, so a name can be written the way the source writes it. Frames are named from snapshot metadata, so a
   frame can be selected by method name rather than by a thread ordinal that shifts between runs. Contextual names
   additionally need import facts from a Portable PDB whose identity matches the module.

   ```text
   context Program.Main
   pdb auto
   eval ServiceState.BuildLabel
   ```

   `pdb auto` probes paths derived from target-side module hints on the analysis machine and says exactly what it
   offered. A target path hint is not identity: every candidate is still validated against the module before any name
   binds through it, and a candidate that does not match is rejected.

5. **Ask what the session was allowed to use.** `status` prints the adopted root, the frame supplying name context,
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
| Root-relative member chain of any depth | `root.Member.Member.Member.Member` |
| Conditional access at any hop after the first | `root.Member?.Member.Member?.Member` |
| Coalescing with a literal | `root.Member?.Member ?? "fallback"` |
| Constant arithmetic over every C# numeric type | `(86400 / 24) / 60`, `0.1 + 0.2`, `10m / 4`, `1UL << 40` |
| Numeric casts and conversions, including nullable targets | `(long)int.MaxValue + 1`, `(int?)null` |
| Enum member or const field | `System.DayOfWeek.Monday` |
| `System.Math`, `BigInteger`, and numeric type statics | `Math.Round(Math.PI, 4)`, `BigInteger.Pow(2, 100)`, `double.NaN` |
| Invariant `ToString`, with or without a format | `(255).ToString("X4")`, `(0.1 + 0.2).ToString()` |
| Deterministic string/char operations over constants | `("a" + "b").ToUpperInvariant()`, `"text".Contains('x')` |
| Index and range expressions on constant strings | `"hello"[^1]`, `"hello"[1..^1]` |
| Boolean logic and comparisons over constants | `"abc".Length > 2 && char.IsDigit('5')` |
| Dump values as operands in composed expressions | `root.QueueDepth * 2 + 1`, `Some.Type.Count + 1`, `root.Batch.Id[6..^5]` |
| Array initializers and array-producing BCL members | `new[] { 1, 2, 3 }`, `"a,b".Split(',')`, `"abc".ToCharArray()` |
| Lambda-free `System.Linq.Enumerable` over sequences | `"a,b".Split(',').Length`, `xs.Distinct().Order()`, `xs.Contains(2)` |
| Expression lambdas over sequences: `Select`/`Where` (with index), `Any`/`All`/`Count`, `First`/`Last`/`Single` (+`OrDefault`), `Sum`/`Min`/`Max`/`Average` selectors, `OrderBy`(`Descending`), `TakeWhile`/`SkipWhile`, `ToArray`/`ToList` | `xs.Where(x => x % 2 == 1).Select(x => x * x).Sum()`, `root.DurationsMs.Select(ms => ms / 1000.0).ToArray()` — expression bodies only; block bodies, captures of mutable state, and `Aggregate` are typed stops |
| Read-only arrays from the dump heap | `root.Batch.Tags[..]`, `root.DurationsMs.Max()`, `Some.Type.Corridors[0]` |
| Interpolated strings, invariant, with alignment and formats | `$"depth {root.QueueDepth,4}"`, `$"{255:X4}"` |
| `is` patterns: constant, `null`, relational, `and`/`or`/`not` | `root.QueueDepth is > 0 and < 100`, `root.Failure?.Code is not null` |
| `switch` expressions over the same patterns, with `when` | `root.QueueDepth switch { > 10 => "busy", _ => "idle" }` |
| `nameof`, `default(T)`, `sizeof(T)` | `nameof(root.Batch.Id)`, `default(int?)`, `sizeof(decimal)` |
| Date and time computations: `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` | Constructors, ticks, deterministic factories (`TimeSpan.FromSeconds`, `DateTimeOffset.FromUnixTimeSeconds`), calendar/clock members, the full operator algebra (`end - start`, `span * 2`, comparisons), invariant `ToString` and interpolation formats — `new DateTime(2026, 7, 30) + TimeSpan.FromHours(6)`, `TimeSpan.FromMilliseconds(root.DurationsMs.Max()).TotalSeconds`. `Now`/`Today`/`UtcNow`, local-time-zone conversions, and culture-dependent parsing are typed stops: a post-mortem answer never depends on when or where the dump is inspected |
| Deterministic BCL values: `Guid` and `Version` | Fixed-grammar construction and parsing (`new Guid("…")`, `Guid.Parse`, `Guid.ParseExact`, `Guid.Empty`, `new Version(1, 2, 3)`, `Version.Parse`), members, same-kind comparisons, invariant formats (`guid.ToString("N")`, `$"{guid:B}"`), and sequence/lambda composition. `Guid.NewGuid()` is a typed stop: a freshly generated value is not evidence |
| Full enum semantics, `typeof`, and the `System.Enum` API | Casts both ways with unchecked underlying truncation (`(DayOfWeek)3`, `(int)flags`), the flags algebra (`\|`, `&`, `^`, `~`, `HasFlag`), `E + n` and `E − E` with C# typing, enum `ToString` formats (`G`/`F`/`D`/`X`), `typeof(...)` references with `Name`/`FullName`/`Namespace`/`IsEnum` and equality, and `Enum.GetNames`/`GetValues`/`GetName`/`IsDefined`/`Parse`/`GetUnderlyingType`/`ToObject` in both generic and `typeof` spellings. Enum shapes come from a closed BCL table or from the dump module's own counted metadata — including underlying kinds up to `ulong` — so `(Contoso.Status)2` names the member the target's assembly declares. `Enum.TryParse` needs an out parameter and is a typed stop naming `Parse` |
| Array creation in every spelling | Sized with C#'s zero-fill semantics (`new int[3]` → `{ 0, 0, 0 }`, `new Guid[1]`), initializers (`new int[3] { 1, 2, 3 }` with length checked), typed empties (`new int[0]`, `new string[] { }`), collection expressions with spreads (`[1, .. xs, 9]`), and `Array.Empty<T>()`. A negative length raises the exact BCL exception; a length past the sequence bound is a typed stop |
| Pure `System.Array` statics and instance members | `Array.IndexOf`/`LastIndexOf`/`BinarySearch` (exact BCL complement encoding), `Array.MaxLength`, the lambda `Find` family (`Exists`, `TrueForAll`, `Find`, `FindAll`, `FindIndex`, `FindLastIndex`, `ConvertAll`), and instance `Rank`/`LongLength`/`GetValue`/`GetLength`/`GetLowerBound`/`GetUpperBound`. Mutators (`Sort`, `Reverse`, `Fill`, `Clear`, `Copy`, `Resize`, `SetValue`) are typed stops: the evaluator observes evidence, it never rearranges it |
| Pure `System.Type` members on `typeof(...)` | `IsArray`, `IsValueType`/`IsClass`/`IsInterface`/`IsPrimitive`, `HasElementType`, `GetElementType()`, `MakeArrayType()`, `UnderlyingSystemType`, `ToString`/`Equals`, and the enum projections `GetEnumNames`/`GetEnumValues`/`GetEnumName`/`IsEnumDefined`/`GetEnumUnderlyingType` — `typeof(int).MakeArrayType().FullName`, `typeof(DayOfWeek).GetEnumName(5)` |
| Type relationships: assignability, subclassing, instance tests | `IsAssignableFrom`/`IsAssignableTo` with the runtime's exact rules — identity, base chains, boxing to `object`/`ValueType`/`Enum`/`Array`, `Nullable<T>` lifting, array covariance, implemented interfaces, and declared variance (`typeof(IEnumerable<object>).IsAssignableFrom(typeof(string[]))` → `true`) — plus `IsSubclassOf`, `BaseType`, and `IsInstanceOfType` over evaluated values. The relation runs over a closed table pinned by the BCL contract, so an answer is exact or a typed stop, never a guess |
| Generic types: `typeof`, `MakeGenericType`, introspection | Constructed generics (`typeof(List<int>)`, `typeof(int?)`, `typeof(Dictionary<string, List<int>>)`) and open definitions (`typeof(List<>)`, `typeof(Dictionary<,>)`) over `Nullable<T>`, `List<T>`, `Dictionary<K,V>`, `KeyValuePair<K,V>`, and the generic collection/comparison interfaces; `MakeGenericType` with the runtime's exact failure semantics (arity → `ArgumentException`, non-definition receiver → `InvalidOperationException`, `Nullable<T>` constraint checks), `GetGenericTypeDefinition`, `GenericTypeArguments`/`GetGenericArguments`, and the `IsGenericType`/`IsGenericTypeDefinition`/`IsConstructedGenericType`/`ContainsGenericParameters` family. A constructed generic's `FullName`/`AssemblyQualifiedName` embed the inspecting runtime's assembly identity and are typed stops |
| `checked`/`unchecked` wrappers | `checked(int.MaxValue + 0)` — `unchecked` wrap-around is a typed stop |

Type, declaration, property, and list patterns need runtime type identity the constant domain does not model, so
they are typed stops rather than guesses; an interpolated value must be a scalar constant.

A member chain has no hop-count limit: depth is bounded only by the front end's expression-length and node-count
limits. Each intermediate hop must be a directly declared reference field whose exact declared type is present in
the snapshot's runtime type catalog and agrees with the referenced object, validated with the same counted evidence
at every link; a `?.` whose receiver is exactly null short-circuits the whole chain to the coalescing fallback. A
hop whose declared type the captured process never loaded is a typed stop, because the snapshot cannot validate what
it never materialized.

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
Constant expressions fold with the semantics C# defines, across the full numeric tower: every fixed-size integral
type, `nint`/`nuint` (folded at 64 bits, matching the x64 processes the preview targets), `Int128`/`UInt128`,
`System.Numerics.BigInteger`, `float`, `double`, and `decimal`, with C# numeric promotion, checked integral
arithmetic, and casts between all of them (overflow, division by zero, and argument errors are typed stops reported
under their familiar exception names). Floating point is IEEE-754 faithful — signed zeros, infinities, and NaN
behave exactly as in running code — and `decimal` keeps exact scale. `System.Math`, the numeric type statics
(`MinValue`/`MaxValue`, `double.NaN`, `Epsilon`, `Pi`), the `double`/`float` classification predicates, and the
`BigInteger` factories evaluate deterministically; hardware-dependent estimates are typed stops. Numeric `ToString`
— with or without a format string — always evaluates under the invariant culture. Nullable semantics are lifted
exactly: `null` and `(int?)null` are exact null results, arithmetic with a null operand yields exactly null, and
`??` coalesces. A fully qualified enum member or `const` field is read from the declaring module's metadata Constant
table in the dump — Int32-family and string constants are supported, and other constant types are a typed stop. A
closed allowlist of deterministic, stateless, culture-independent `string` and `char` members evaluates over
constant operands: concatenation, ordinal `Contains`/`StartsWith`/`EndsWith`/`IndexOf`, `Substring`, `Trim`, `Pad`,
`Insert`, `Remove`, ordinal `Replace`, `ToUpperInvariant`/`ToLowerInvariant`,
`string.Concat`/`Join`/`IsNullOrEmpty`/`CompareOrdinal`, the `char` classification predicates, indexing and range
slicing with from-end `^n` indexes, `Length`, equality, relational comparison, Boolean logic, and the conditional
operator. A culture-sensitive member or overload — `ToLower()`, `IndexOf(string)` without a `StringComparison`, or a
culture-based comparison — is a typed stop naming the deterministic alternative; `StringComparison.Ordinal` and
`OrdinalIgnoreCase` arguments are accepted. Character classification follows the pinned analysis runtime's Unicode
tables. Nested types and names that need import context are outside this version.

**Dump values compose.** Inside a composed expression, a static-field name or a root-relative member chain resolves
through the same frozen pipeline that answers it alone, and its exact Int32, string, or null value becomes an
operand: `root.QueueDepth * 2 + 1`, `Some.Type.ProcessedCount + 1`, `root.Batch.Id[6..^5]`, or a stored
`Nullable<Int32>` behind `?? 0`. A bare name or bare chain never enters this path — it keeps its full evidence
report — and an operand that is not exact is a typed stop carrying the pipeline's own diagnostic, never a guessed
value. The answer reports how many dump values it consumed.

**Arrays are virtual sequences.** An array initializer (`new[] { 1, 2, 3 }`), an array-producing BCL member
(`Split`, `ToCharArray`, `Enumerable.Range`/`Repeat`), or a single-dimension array read from the dump heap
materializes as a virtual sequence: it exists only while the one expression evaluates, is never persisted anywhere,
and is transformed purely functionally. Dump-heap arrays are read-only evidence — a static array field or a plain
root-relative chain ending at an array member materializes its exact elements (integral, floating-point, Boolean,
char, and string element domains; other shapes are typed stops). Sequences answer indexing and range slicing,
`Length`, and the deterministic lambda-free `System.Linq.Enumerable` surface — `Count`, `Any`, `Contains`,
`First`/`Last`/`Single` and their `OrDefault` forms, `ElementAt`, `Skip`/`Take`/`SkipLast`/`TakeLast`, `Reverse`,
`Distinct`, `Append`/`Prepend`/`Concat`, `Union`/`Except`/`Intersect`, `SequenceEqual`, `Order`/`OrderDescending`,
and the `Sum`/`Min`/`Max`/`Average` aggregates — with the real BCL semantics, including overflow and empty-sequence
stops under their familiar exception names. Sequences are bounded at 4096 elements as a deterministic limit;
ordering or `Min`/`Max` over strings is a culture-sensitive typed stop; overloads that take a lambda are outside
this version.

- **It is not a general expression evaluator.** Method calls on runtime objects, generics, lambda expressions, and
  operands beyond the scalar, string, null, and single-dimension array value surface are outside the current
  subset.
- **It does not read your disk to fill gaps.** Names and values come from the snapshot. A Portable PDB is consulted
  only when you offer one — in the console host explicitly, via `pdb <path>` or `pdb auto`; in the desktop shell the
  hint-derived probe runs automatically — and only after its identity is validated against the module. The desktop shell's source
  view holds the same line: a file is presented as a frame's source only when its bytes reproduce the PDB's document
  checksum. When no matching PDB or verified source exists, the shell falls back to C# **decompiled from the
  module's IL** by the ILSpy engine — and only from an on-disk assembly whose complete metadata content identity
  reproduces the dump module's. The view is labelled as a reconstruction (`⚠ decompiled from IL`), never presented
  as the original source.

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
