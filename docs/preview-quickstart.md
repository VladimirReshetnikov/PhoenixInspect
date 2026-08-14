# Preview Quickstart

Status: Current · Active. Type: Guide.

PhoenixInspect opens a .NET memory dump read-only and answers C# expressions against the values and objects that
dump actually contains. This page gets you from a clone to your first answered expression, then says plainly what the
preview can and cannot answer.

## Requirements

- Windows. ClrMD dump loading and the desktop shell both require it.
- The .NET SDK pinned in [`global.json`](../global.json).
- PowerShell 7 (`pwsh`) for the repository automation scripts.

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

## Validate the unsigned Windows artifact layout locally

```text
pwsh ./eng/Publish-PrereleaseArtifacts.ps1
```

From an exact clean Git HEAD, this locked-restores and publishes exactly the CLI and desktop applications as
self-contained `win-x64` Release directories. Restore/build intermediates and NuGet packages live under a fresh
temporary root, so ignored repository `bin`/`obj` content and a pre-existing global package extraction cannot become
incremental build inputs. The publisher verifies every tracked working file's raw bytes against its HEAD Git blob —
in addition to requiring an empty tracked/untracked status — and rejects ignored files under `src` outside the exact
source-project `bin`/`obj` allowance. It repeats that source-state boundary after the build, before output staging,
and immediately before installing output.

Before the expensive restores, the publisher resolves Microsoft's pinned standalone Windows SBOM Tool 4.1.5. By
default it downloads the exact versioned asset into its disposable work root; an offline caller may instead pass
`-SbomToolPath` to a preseeded copy. Either path requires the exact size, SHA-256, version resources, command version,
and Microsoft Authenticode identity and never falls back to a latest tool. SBOM Tool child processes run with .NET
invariant globalization so the tool's culture-sensitive SPDX package-verification ordering is stable across Windows
host locales. The publisher then derives and verifies a
common third-party dependency/license-evidence bundle from the products' exact
`.deps.json` graphs, verifies the isolated post-restore project graph and runtime-pack download sets, records the
actual common runtime pack carried by the published products, retains the packaged CLI `--help` smoke, runs the
Desktop's exact non-UI `--smoke-test`, then runs its separate native `--native-window-smoke-test`, and writes two ZIPs
plus `SHA256SUMS.txt` under `artifacts/prerelease`. It also
locked-restores and Release-publishes `samples/Contoso.OrderService` as a framework-dependent disposable target under
the fresh temporary work root. The hidden target must print exact `READY` within 30 seconds; then the extracted
self-contained CLI must capture a nonempty dump and reopen it to evaluate
`Contoso.OrderService.Diagnostics.ServiceState.BuildLabel`, with a 60-second bound on each CLI operation. The target
is stopped and disposed before inspection, and the smoke requires the exact value `"2026.07.30-preview"`, an
`Exact`/`Complete` answer, one evaluated expression, and zero non-exact answers. The dump, target publish, and all
smoke logs remain outside the ZIPs and are deleted with the temporary work root; CI does not retain or upload them.
The target's bounded post-`READY` stdout permits only CoreCLR's measured two-line `[createdump]` start/success
diagnostic pair, bound to the exact target PID and requested dump path; target stderr and any unrelated application
output still fail the smoke.

Every ZIP contains byte-identical canonical `BUILD-EVIDENCE.json`, the generated third-party evidence, a canonical
product-specific SPDX 2.2 SBOM under `_manifest/spdx_2.2/manifest.spdx.json`, `SBOM-EVIDENCE.json`, and a complete
per-file SHA-256 manifest. The CLI SBOM contains exactly the CLI dependency inventory; the Desktop SBOM contains
exactly the Desktop inventory. Microsoft SBOM Tool validates the generated file hashes, and PhoenixInspect's stricter
validator independently requires the exact product package set, file set, hashes, relationships, metadata, and
canonical serialization before and after archive extraction. `ARTIFACT-MANIFEST.txt` hashes every payload file except
itself and the canonical SBOM; the SBOM in turn hashes `ARTIFACT-MANIFEST.txt` and every other non-SBOM payload file,
avoiding a hash cycle while the normalized ZIP and `SHA256SUMS.txt` bind the SBOM bytes. The tool's raw hash sidecar
is verified and then removed before the SBOM is canonically serialized and validated; exactly one validator-owned
SBOM JSON file ships. The build-evidence record binds the initial/final Git commit and tree, canonical tracked
repository URL, actual compatible stable .NET 10.0.4xx SDK, target/configuration/RID/self-contained settings, actual
runtime pack, third-party evidence manifest, and an ordinal list of selected source/configuration inputs and their
SHA-256 hashes. Entries are sorted and their timestamps are normalized to remove incidental sources of variation,
but byte-for-byte reproducibility is not yet a supported or tested claim, even when the same toolchain is used.

These files are unsigned local-validation outputs. **Do not redistribute them.** Each contains the repository
`LICENSE` plus a mechanically generated `THIRD-PARTY-NOTICES` evidence directory: every selected external
`.deps.json` asset and the SDK-generated apphost are attributed to verified NuGet archives or the .NET runtime pack,
and policy-pinned license/notice materials are hash-checked. The separate payload manifest covers every packaged file.
That evidence is deliberately not a legal-clearance assertion; human review of the bundled .NET runtime and NuGet
dependencies is still required. An SBOM is inventory evidence, not legal clearance or permission to redistribute.
CI builds and inspects the ZIPs only within the validation job; it does not upload or
otherwise distribute them. The first Desktop smoke verifies the packaged entry assembly and product version, loads a
selected Avalonia/product dependency surface, checks every selected `.deps.json` asset for presence and
managed-assembly metadata, and requires the exact fifteen compiled XAML registrations without initializing Avalonia.
The second smoke starts the extracted production application on Avalonia's Win32/Skia stack, constructs the real main
window and its initial active content, requires a nonzero `HWND`, completes native `Show` and a loaded-priority initial
layout/render dispatcher turn, and then shuts down cleanly. It intentionally does **not** claim human-observed pixels,
pixel fidelity, input, accessibility, file-picker behavior, every deferred pane, dump opening through the UI, or broad
Windows/GPU/display compatibility. The embedded build identity is unsigned local evidence, not SLSA/in-toto
provenance, a reproducible-build claim, an attestation, legal clearance, a signature, redistribution authorization,
or evidence of W8.10 release closure. These are not NuGet packages. CI does not create a
tag, artifact upload, or GitHub release from this lane.

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
| `System.Math`, `BigInteger`, and numeric type statics | `Math.Round(Math.PI, 4)`, `BigInteger.Pow(2, 100)`, `double.NaN`, `nint.MaxValue`, `IntPtr.Size`, `Half.MaxValue`, `NFloat.Epsilon`, `int.Parse("5")`, `decimal.Round(1.2345m, 2)` |
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
| `dynamic`: declarations, identity casts, `dynamic[]`, `default(dynamic)` | `dynamic d = root.Name;` then `d.Length`, `((dynamic)"a,b").Split(',')` — the evaluator dispatches on runtime kinds, so late binding is its native mode |
| Date and time computations: `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` | Constructors, ticks, deterministic factories (`TimeSpan.FromSeconds`, `DateTimeOffset.FromUnixTimeSeconds`), calendar/clock members, the full operator algebra (`end - start`, `span * 2`, comparisons), invariant `ToString` and interpolation formats — `new DateTime(2026, 7, 30) + TimeSpan.FromHours(6)`, `TimeSpan.FromMilliseconds(root.DurationsMs.Max()).TotalSeconds`. `Now`/`Today`/`UtcNow`, local-time-zone conversions, and culture-dependent parsing are typed stops: a post-mortem answer never depends on when or where the dump is inspected |
| Deterministic BCL values: `Guid` and `Version` | Fixed-grammar construction and parsing (`new Guid("…")`, `Guid.Parse`, `Guid.ParseExact`, `Guid.Empty`, `new Version(1, 2, 3)`, `Version.Parse`), members, same-kind comparisons, invariant formats (`guid.ToString("N")`, `$"{guid:B}"`), and sequence/lambda composition. `Guid.NewGuid()` is a typed stop: a freshly generated value is not evidence |
| Full enum semantics, `typeof`, and the `System.Enum` API | Casts both ways with unchecked underlying truncation (`(DayOfWeek)3`, `(int)flags`), the flags algebra (`\|`, `&`, `^`, `~`, `HasFlag`), `E + n` and `E − E` with C# typing, enum `ToString` formats (`G`/`F`/`D`/`X`), `typeof(...)` references with `Name`/`FullName`/`Namespace`/`IsEnum` and equality, and `Enum.GetNames`/`GetValues`/`GetName`/`IsDefined`/`Parse`/`GetUnderlyingType`/`ToObject` in both generic and `typeof` spellings. Enum shapes come from a closed BCL table or from the dump module's own counted metadata — including underlying kinds up to `ulong` — so `(Contoso.Status)2` names the member the target's assembly declares. `Enum.TryParse` needs an out parameter and is a typed stop naming `Parse` |
| Array creation in every spelling | Sized with C#'s zero-fill semantics (`new int[3]` → `{ 0, 0, 0 }`, `new Guid[1]`), initializers (`new int[3] { 1, 2, 3 }` with length checked), typed empties (`new int[0]`, `new string[] { }`), collection expressions with spreads (`[1, .. xs, 9]`), and `Array.Empty<T>()`. A negative length raises the exact BCL exception; a length past the sequence bound is a typed stop |
| Pure `System.Array` statics and instance members | `Array.IndexOf`/`LastIndexOf`/`BinarySearch` (exact BCL complement encoding), `Array.MaxLength`, the lambda `Find` family (`Exists`, `TrueForAll`, `Find`, `FindAll`, `FindIndex`, `FindLastIndex`, `ConvertAll`), and instance `Rank`/`LongLength`/`GetValue`/`GetLength`/`GetLowerBound`/`GetUpperBound`. Mutators (`Sort`, `Reverse`, `Fill`, `Clear`, `Copy`, `Resize`, `SetValue`) are typed stops: the evaluator observes evidence, it never rearranges it |
| Pure `System.Type` members on `typeof(...)` | `IsArray`, `IsValueType`/`IsClass`/`IsInterface`/`IsPrimitive`, `HasElementType`, `GetElementType()`, `MakeArrayType()`, `UnderlyingSystemType`, `ToString`/`Equals`, and the enum projections `GetEnumNames`/`GetEnumValues`/`GetEnumName`/`IsEnumDefined`/`GetEnumUnderlyingType` — `typeof(int).MakeArrayType().FullName`, `typeof(DayOfWeek).GetEnumName(5)` |
| Type relationships: assignability, subclassing, instance tests | `IsAssignableFrom`/`IsAssignableTo` with the runtime's exact rules — identity, base chains, boxing to `object`/`ValueType`/`Enum`/`Array`, `Nullable<T>` lifting, array covariance, implemented interfaces, and declared variance (`typeof(IEnumerable<object>).IsAssignableFrom(typeof(string[]))` → `true`) — plus `IsSubclassOf`, `BaseType`, and `IsInstanceOfType` over evaluated values. The relation runs over a closed table pinned by the BCL contract, so an answer is exact or a typed stop, never a guess |
| Generic types: `typeof`, `MakeGenericType`, introspection | Constructed generics (`typeof(List<int>)`, `typeof(int?)`, `typeof(Dictionary<string, List<int>>)`) and open definitions (`typeof(List<>)`, `typeof(Dictionary<,>)`) over `Nullable<T>`, `List<T>`, `Dictionary<K,V>`, `KeyValuePair<K,V>`, and the generic collection/comparison interfaces; `MakeGenericType` with the runtime's exact failure semantics (arity → `ArgumentException`, non-definition receiver → `InvalidOperationException`, `Nullable<T>` constraint checks), `GetGenericTypeDefinition`, `GenericTypeArguments`/`GetGenericArguments`, and the `IsGenericType`/`IsGenericTypeDefinition`/`IsConstructedGenericType`/`ContainsGenericParameters` family. A constructed generic's `FullName`/`AssemblyQualifiedName` embed the inspecting runtime's assembly identity and are typed stops |
| Tuple literals with C#'s exact semantics | `(1, "a")`, named elements (`(count: 1, name: "x").count`), `ItemN` access incl. nested (`((1, 2), 3).Item1.Item2`), element-wise `==`/`!=` with numeric promotion, invariant `ToString` (`(1, "a").ToString()` → `"(1, a)"`), and interpolation. Compound results — tuples and arrays — expose structured children (`ItemN`/declared names, `[i]`) so hosts expand them like Visual Studio's Watch window |
| C# query expressions, the full grammar | `from`/`where`/`let`/`orderby … descending`/`select`, multiple and dependent `from`, `group … by … into` with real groupings (`g.Key`, `g.Sum()`, iterable), `join` and `join … into`, query continuations, and `from T x in e` casts — translated by the specification's own rules onto the folded operator surface (`SelectMany`, `GroupBy`, `Join`, `GroupJoin`, `ThenBy`), with anonymous objects as the transparent identifiers, so results and typed stops are identical to the method syntax |
| Anonymous types | `new { root.QueueDepth, Total = xs.Sum() }` with explicit and projected member names, member access, C#'s value-based `Equals`, invariant `ToString`, interpolation, sequences of anonymous shapes, and structured children for watch expansion. `==` is reference equality and therefore a typed stop |
| `checked`/`unchecked` wrappers | `checked(int.MaxValue + 0)` — `unchecked` wrap-around is a typed stop |

Type, declaration, property, and list patterns need runtime type identity the evaluator's deterministic domain does not model, so
they are typed stops rather than guesses; an interpolated value must be a scalar value.

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

- **It does not compose Edit-and-Continue generations yet.** Before an expression may consult dump metadata,
  runtime storage, an adopted root, or method IL, the shared host service proves that every loaded managed module has
  an exact zero applied-generation count. An edited module, or a module whose edit state cannot be classified
  exactly, produces a typed session-admission stop instead of a plausible answer from the stale base image. Pure
  constants such as `2 + 2` remain available because they consume no dump evidence.
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
exact operands: concatenation, ordinal `Contains`/`StartsWith`/`EndsWith`/`IndexOf`, `Substring`, `Trim`, `Pad`,
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
