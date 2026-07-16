> **Roadmap status: research backlog.** This is no longer the active MVP. The active milestones first prove dump evidence, a restricted derived-query front end, and a concrete scenario-derived IL slice. CN-O enters the roadmap only after the concrete corpus and domain laws pass. Span, Task, and framework models are explicitly outside that gate.

This document records a candidate later precision package: a minimal abstract value domain (constants, nullness, runtime type sets, and origin labels) plus selected BCL models.

I’m going to assume the broader architecture we discussed: **domain-parametric IL semantics**, plus a **call dispatcher** that can replace “interpret the callee IL” with **models** that are (a) bounded, (b) deterministic, and (c) explicitly conservative in abstract mode.

---

# MVP Abstract Domain Spec: `CN-O` (Constants, Nullness, Type-set, origin labels)

## 1) What the domain must accomplish

The MVP domain is deliberately not “smart”; it is **predictable** and **composable**.

It must:

1. Keep evaluation/analysis running when data is missing or operations are unsupported (by minting *typed unknowns*).
2. Preserve enough facts to make common code paths meaningful:

   * null checks
   * simple numeric / boolean computations
   * type tests/casts
   * “pure-ish” string / nullable / task / span questions
3. Be cheap to join/widen (static analysis will do it thousands of times).
4. Support **control-flow sensitivity**: when you branch, you can refine facts.

The core idea: every value carries **(Facts)** and an optional **(Payload)** for structure/identity. Facts are what you asked for (C/N/TypeSet/origin labels). Payload is how the interpreter still represents structs, refs, byrefs, etc., without bloating the fact lattice.

---

## 2) Value representation

### 2.1 Stack category (mandatory)

We keep a separate, small “stack kind” tag because IL semantics depends on it:

```csharp
enum StackKind
{
    I4, I8, R4, R8, NativeInt,
    Ref,     // O
    ByRef,   // &
    ValueType, // inline struct on stack (incl. Nullable<T>, Span<T>)
    ManagedPtrLike, // optional (TypedRef), can start unsupported
}
```

### 2.2 Facts (the MVP lattice product)

```csharp
sealed record Facts(
    Nullness Null,            // only meaningful for Ref/ByRef
    ConstValue Const,         // “exact” constant when known
    RuntimeTypeSet TypeSet,   // only meaningful for Ref (and optionally boxed)
    OriginLabelSet OriginLabels            // set of tags
);
```

#### Nullness lattice

For reference-ish values:

```
        MaybeNull
       /        \
    Null       NonNull
```

* Join:

  * `Null ⊔ NonNull = MaybeNull`
  * `Null ⊔ Null = Null`, `NonNull ⊔ NonNull = NonNull`
* Widen: same as join (nullness is finite).

For non-reference values: treat as `N/A` (or always `NonNull` for ByRef if you only accept verifiable IL; for robustness allow `MaybeNull`).

#### Constant lattice

For each primitive category, constants are:

* `Const(k)` or `Top` (unknown).
  Optionally keep `Bottom` for unreachable (but you can also represent unreachable states outside the domain by absence of state).

* Join:

  * same constant joins to itself
  * different constants join to `Top`

* Widen: same as join.

What counts as a “constant” in MVP:

* `bool`, `char`, integer types, float types if you’re okay with IEEE semantics, `null` for refs, and **string literal** (`ldstr`) as a special “constant ref”.

#### Runtime type-set lattice (refs)

Represent as a bounded finite set + a top element.

```csharp
abstract record RuntimeTypeSet
{
    sealed record AnySubtypeOf(TypeSig Static) : RuntimeTypeSet;
    sealed record Finite(ImmutableHashSet<TypeHandle> Types) : RuntimeTypeSet;
    sealed record Empty : RuntimeTypeSet; // only for unreachable
}
```

* Join:

  * union if both finite and union size ≤ `MaxTypeSet` else `AnySubtypeOf(static)`
  * joining with `AnySubtypeOf` yields `AnySubtypeOf`
* Widen:

  * after N joins, force `AnySubtypeOf` (prevents blowups in loops).

**Important:** type-set is about **runtime types**, not static typing. Static type still exists on the value (`StaticType`), type-set refines it.

#### Origin-label lattice

A set of tags:

```csharp
enum OriginLabelTag
{
    Env_Socket, Env_File, Env_Time, Env_Random,
    Native, Reflection, Threading,
    MissingData, UnsupportedIL, BudgetExceeded,
    ExternalUnknown
}
```

* Join: set union.
* Widen: union.

Origin labeling is conservative and monotone: once origin-labeled, operations typically stay origin-labeled.

---

## 3) Payload (structural / identity information)

Facts alone can’t execute IL. You need payload for:

* object identity / points-to sets (refs)
* addressables (byrefs)
* struct field contents (value types) *if you want useful Nullable/Span behavior*

So we explicitly separate **Facts** from **Payload**.

### 3.1 Reference payload: points-to

```csharp
abstract record RefTarget
{
    sealed record Concrete(ObjectId Id) : RefTarget;       // virtual heap object
    sealed record External(ulong Address) : RefTarget;     // dump-backed address
    sealed record Summary(SummaryId Id) : RefTarget;       // abstract heap region
}

sealed record RefPayload(ImmutableHashSet<RefTarget> Targets, bool IsTop);
```

* If `IsTop=true`, means “could point anywhere” (used when you give up).
* If `Targets` grows beyond `MaxPointsTo`, collapse to `IsTop=true`.

### 3.2 ByRef payload: addressables

ByRef isn’t a pointer; it’s “a reference to a location you can load/store”.

```csharp
abstract record Addressable
{
    sealed record Local(int Index) : Addressable;
    sealed record Arg(int Index) : Addressable;
    sealed record Field(RefTarget Obj, FieldHandle Field) : Addressable;
    sealed record ArrayElem(RefTarget Arr, TValue Index) : Addressable; // Index may be const/unknown
    sealed record Interior(Addressable Base, int OffsetBytes) : Addressable; // optional
}

sealed record ByRefPayload(ImmutableHashSet<Addressable> Locs, bool IsTop);
```

### 3.3 ValueType payload: field-sensitive struct

To make `Nullable<T>` and `Span<T>` pay off, MVP should be **field-sensitive for small structs**, with a cap.

```csharp
sealed record StructPayload(
    TypeHandle StructType,
    ImmutableArray<FieldValue> Fields, // in a stable “layout order”
    bool IsTop // “unknown struct”
);

sealed record FieldValue(FieldHandle Field, TValue Value);
```

Rules:

* If struct has too many fields, or layout can’t be resolved, collapse to `IsTop=true`.
* Join two structs fieldwise only if same `StructType` and both not-top.
* Otherwise join to top-struct (facts preserved at struct level, field contents lost).

This is the minimum needed for `Nullable<T>` (2 fields) and common span-ish structs (pointer+length).

---

## 4) `TValue` itself

Putting it together:

```csharp
sealed record TValue(
    TypeSig StaticType,
    StackKind Kind,
    Facts Facts,
    object? Payload,        // RefPayload / ByRefPayload / StructPayload / null
    ConditionInfo? CondInfo // optional (see next section)
);
```

---

## 5) Control-flow sensitivity: `AssumeTrue/AssumeFalse`

Without a mechanism to refine facts on branches, you’ll get “everything unknown” quickly.

### 5.1 ConditionInfo: a tiny “explainable predicate” channel

We do **not** add a full constraint solver in MVP. We add a tiny hook that captures common predicate shapes and lets the domain refine operands.

```csharp
abstract record ConditionInfo
{
    sealed record IsNull(TValue Value) : ConditionInfo;
    sealed record TypeTest(TValue Value, TypeSig TargetType) : ConditionInfo; // from isinst/as
    sealed record Equals(TValue A, TValue B) : ConditionInfo;
    sealed record ModeledPredicate(string Key, ImmutableArray<TValue> Args) : ConditionInfo;
}
```

Then:

* `AssumeTrue(state, condValue)` inspects `condValue`:

  * if `condValue` is const true => no change
  * if const false => state unreachable
  * if `CondInfo` recognized => apply targeted refinements (below)
* `AssumeFalse` likewise.

### 5.2 Mandatory refinements (MVP)

Refinement rules that matter immediately:

1. **Null checks**

   * If cond is `IsNull(v)`:

     * assume true ⇒ refine `v.Nullness = Null`, `v.RefTargets = {}` (or keep if you model null as empty)
     * assume false ⇒ refine `v.Nullness = NonNull`
   * For `Equals(v, null)` treat as IsNull.

2. **Type tests / casts**

   * If cond is `TypeTest(v, T)`:

     * assume true ⇒ refine:

       * `v.Nullness = NonNull` (for `is` patterns)
       * `v.TypeSet = v.TypeSet ∩ {subtypes of T}` (bounded; if you can’t intersect precisely, force `AnySubtypeOf(T)`)
     * assume false ⇒ refine:

       * `v.TypeSet = v.TypeSet \ {subtypes of T}` (often collapses to AnySubtype; still useful if set is small)

3. **ModeledPredicate hooks**

   * Models can attach `ModeledPredicate("String.IsNullOrEmpty", [s])`, etc.
   * Domain applies rule-specific refinements (examples in BCL section).

This tiny mechanism is the cheapest path to “analysis that’s not useless”.

---

## 6) Unknown propagation rules (MVP contract)

When something becomes unknown, it **does not erase everything**. It becomes unknown *with shape*:

* **StaticType** is always set.
* **Nullness**:

  * for ref: default unknown is `MaybeNull`
  * for value type: N/A
* **TypeSet**:

  * for ref: `AnySubtypeOf(StaticType)` (unless you know more)
* **Const**: `Top`
* **origin labels**: includes:

  * at least one of `{MissingData, UnsupportedIL, BudgetExceeded, ExternalUnknown}` depending on origin
  * plus union of input origin labels

This is what prevents the domain from devolving into “all values are just Top”.

---

# Minimal BCL Model Set That Buys Disproportionate Precision

## 7) Model interface (what a “model” returns)

A model should be able to:

* return a value
* optionally modify memory (or havoc it)
* emit effects and origin labels
* optionally attach a `ConditionInfo` to the returned value

```csharp
sealed record ModelResult<TValue, TMem>(
    ExecState<TValue, TMem> State,
    TValue ReturnValue,
    EffectSummary Effects,
    bool IsHandled
);
```

Additionally, your dispatcher should let models attach:

* `ReturnValue.CondInfo = ModeledPredicate(key, args)` for boolean-ish results.

---

## 8) “Don’t be clever” modeling philosophy

For MVP, every model must be one of:

1. **Exact** for constant inputs
2. **Fact-preserving unknown** for non-constant inputs
   (typed unknown + origin labels union + sometimes nullness/type refinements)

If you try to model too much behavior, you’ll either:

* blow your budgets,
* become unsound in abstract mode,
* or become version-sensitive to private implementation details.

So: **small contracts, big payoff**.

---

## 9) String models (highest ROI)

### 9.1 `string.Length` (instance property)

Signature: `int get_Length()`

Model:

* If receiver is `null` ⇒ potential `NullReferenceException` (in abstract mode emit exceptional state; in concrete mode throw).
* If receiver is constant string literal ⇒ return constant length.
* Else return `Unknown(int)` with `originLabels = receiver.OriginLabels`.

### 9.2 `string.Concat(...)` and `op_Addition` patterns

Relevant overloads:

* `Concat(string, string)`
* `Concat(object, object)` (often appears from boxing)
* `Concat(ReadOnlySpan<char>, ...)` (later phase)

Model:

* If all arguments are constant strings (or null treated as empty per .NET semantics if you want) ⇒ return constant.
* Otherwise return `Unknown(string)` with:

  * nullness = NonNull (Concat returns non-null string)
  * originLabels = Union(args.OriginLabels)

### 9.3 `string.IsNullOrEmpty(string)`

Signature: `static bool IsNullOrEmpty(string value)`

Model return:

* If `value` is definitely null ⇒ const true
* If `value` is definitely non-null and constant string ⇒ const (`len==0`)
* Else return unknown bool, but attach predicate:

```csharp
ret.CondInfo = ModeledPredicate("String.IsNullOrEmpty", [value]);
```

Refinements:

* AssumeTrue ⇒ you can refine `value` to `MaybeNull` (no win) unless you also track “maybe empty”. MVP doesn’t.
* AssumeFalse ⇒ refine `value.Nullness = NonNull` (big win; prevents NRE noise).

Even with no “empty-string” facet, this is worth it.

### 9.4 Equality: `string.Equals`, `op_Equality`, `object.Equals`

Model:

* If both are constant strings ⇒ const bool
* If one is null and the other definitely non-null ⇒ const false
* Else unknown bool with optional `ConditionInfo.Equals(a,b)` so nullness can refine slightly.

---

## 10) Object and Type models (small but essential)

### 10.1 `object.ReferenceEquals(object a, object b)`

Model:

* If you have concrete ref targets:

  * if both point-to sets are singletons with same target ⇒ const true
  * if disjoint singleton sets ⇒ const false
* Else unknown bool with `ConditionInfo.Equals(a,b)`.

### 10.2 `object.GetType()`

Model:

* If receiver definitely null ⇒ may throw NRE.
* If receiver has a finite runtime type-set:

  * return a **Type object** whose “type payload” is that set.
* Else return unknown `System.Type`, but origin labels union.

Practical representation:

* Either treat `System.Type` values as ordinary refs with an intrinsic payload (preferred),
* or keep it as an opaque ref with type-set `AnySubtypeOf(System.Type)` and rely on models for `Type.GetTypeFromHandle`/`IsAssignableFrom`.

### 10.3 `Type.GetTypeFromHandle(RuntimeTypeHandle)`

Needed for `typeof(T)` and reflection-ish patterns in IL.

* If token is known ⇒ return constant “Type-of(T)” (intrinsic payload).

---

## 11) Nullable models (high ROI; can be mostly structural)

You can get most Nullable precision **without heavy BCL models** if you keep value types field-sensitive. But two models are still valuable because they show up a lot and affect control-flow:

### 11.1 `Nullable<T>.HasValue`

Signature: `bool get_HasValue()`

Model options:

* **Structural**: return the `hasValue` field constant/unknown.
* Or explicit model:

  * If you represent nullable as `StructPayload` and can read its `hasValue` field ⇒ return that.
  * Else return unknown bool with predicate:

```csharp
ret.CondInfo = ModeledPredicate("Nullable.HasValue", [nullableValue]);
```

Refinements:

* AssumeTrue ⇒ refine nullable struct’s `hasValue` field to true
* AssumeFalse ⇒ refine to false, and possibly refine its `value` field to default/top (optional)

### 11.2 `Nullable<T>.GetValueOrDefault()` and `GetValueOrDefault(T defaultValue)`

Model:

* If `hasValue` definitely true ⇒ return underlying `value`
* If definitely false ⇒ return default (or provided defaultValue)
* Else return join(value, default)

This is hugely important for both dump evaluation and abstract analysis, because it prevents “throw or unknown” from poisoning everything.

### 11.3 `Nullable<T>.Value`

Model:

* If `hasValue` true ⇒ return underlying value
* If `hasValue` false ⇒ throws `InvalidOperationException` (abstract: emit exceptional state)
* If unknown ⇒ return unknown of `T` and mark “may-throw” (or fork normal/exception states in analysis strategy)

---

## 12) Span and low-level helper models (the “modern .NET IL survival kit”)

This is where most naïve interpreters die—not because user code uses low-level operations, but because **BCL uses ref-like tricks**.

The MVP goal for spans is modest:

* Preserve **(base, length)** when spans are created from arrays or other spans.
* Make `Length`, `Slice`, and indexing usable.
* Do it without modeling the entire low-level helper surface.

### 12.1 Minimal semantic representation

Represent span-like values as value types with a structural payload that includes:

* an addressable reference to first element (`ByRefPayload`)
* an `int length`

You can do this either:

* as actual `StructPayload` for `Span<T>` / `ReadOnlySpan<T>` with known fields, **or**
* as an interpreter-level intrinsic “SpanView” payload (less version-sensitive).

For MVP I’d recommend intrinsic payload, because it’s stable and does not depend on private field names.

### 12.2 Models to implement

#### Span constructors from arrays

* `Span<T>(T[] array)`
* `Span<T>(T[] array, int start, int length)`
* ReadOnlySpan equivalents

Semantics:

* If `array` is null ⇒ span becomes “empty” (or throws depending on overload; be conservative).
* If `start/length` are constants and `array.Length` known (dump-backed) ⇒ you can bound-check and refine; else treat as unknown but still produce a view.

Output:

* span view with:

  * base = `ByRef(ArrayElem(array, start))` if start known, else `TopByRef`
  * length = known if possible else unknown int
* `originLabels = array.OriginLabels`

#### `Span<T>.get_Length`

Returns stored length if you have a view; else unknown int.

#### `Span<T>.Slice(int)` / `Slice(int,int)`

If receiver is a view and slice args are constants:

* base shifts by offset
* length becomes new length
  Else return unknown span view, preserving origin labels and element type.

#### Indexing: `get_Item(int)`

For `Span<T>` indexer returns `ref T` (in IL you’ll see `call` returning `&`), for `ReadOnlySpan<T>` it returns `ref readonly T` (still `&`).
Model:

* if view base is precise and index const ⇒ return `ByRef` to that element
* else `TopByRef` of element type, origin labels union

This alone makes an enormous amount of code “interpretable enough”.

### 12.3 Minimal low-level helper models (only what cannot be avoided)

Even if you model spans directly, you will see some low-level helper usage in real IL (especially if you ever interpret BCL IL bodies).

Implement only “identity and simple byref arithmetic” first:

* ref reinterpretation between admitted element types
  Treat as “same addressable, different static type”.

  * return byref payload identical; update `StaticType`.

* bounded ref offset addition
  If `source` is `ArrayElem(arr, constIndex)` and offset const ⇒ new element addressable.
  Else return `TopByRef`.

* bounded ref byte-offset calculation
  Return unknown nativeint unless both are precise and from same array (optional).

Everything else can fall back to unknown without stopping the interpreter.

---

## 13) Task / ValueTask models (usable without “running async”)

The MVP objective is not to *execute* async; it’s to make “task-shaped” values not poison everything.

### 13.1 Represent “known completed task”

Introduce an intrinsic object payload (or a memory tag) for tasks created by known factories:

* `Task.FromResult<T>(T result)` ⇒ return a ref to a task object tagged:

  * kind = `CompletedTask<T>`
  * stored result = `TValue result`

* `Task.CompletedTask` ⇒ return a ref tagged:

  * kind = `CompletedTaskVoid`

* `ValueTask<T>(T result)` ⇒ a value type tagged similarly, or structural fields.

### 13.2 Key models

#### `Task.FromResult<T>(T)`

* Return: NonNull ref `Task<T>`, origin labels union with result
* Payload: completed-result = result

#### `Task<T>.get_Result`

* If receiver is a “known completed task” payload ⇒ return stored result
* Else return unknown `T` with `originLabels = receiver.OriginLabels`, and mark effect `Threading`?
  (In pure semantics, `.Result` can block; in “model world” treat as “may block / may throw”. For dump-time evaluation you’d likely forbid blocking; for static analysis you mark `Threading`.)

#### `Task.get_IsCompletedSuccessfully` / `Task.get_IsCompleted`

If receiver is known completed task ⇒ const true.
Else unknown bool, attach predicate hook if you want to refine paths.

#### `ValueTask<T>.get_Result`

Same as Task<T>.Result when it’s a “known completed ValueTask”.

This buys you:

* evaluation of code that returns cached/FromResult tasks
* analysis that doesn’t collapse at first async boundary

---

## 14) A few “cheap but huge” extra models (optional MVP+)

These aren’t in your example list, but in practice they remove a lot of noise:

### 14.1 `System.Array.get_Length` and `ldlen`

`ldlen` is IL-level already; ensure it returns constant when array is concrete.
For unknown arrays, return unknown int (`originLabels = array.OriginLabels`).

### 14.2 `System.Math` simple pure functions

* `Min/Max/Abs` on constant inputs ⇒ constant
* else ⇒ unknown, origin labels union
  This improves branch pruning in analysis more than you’d expect.

### 14.3 `EqualityComparer<T>.Default` + `IEquatable<T>`

Don’t model the comparer; just avoid crashing:

* return unknown comparer object; calling `Equals` returns unknown bool with origin labels union.

---

# How these pieces interact (small worked examples)

## Example 1: `if (!string.IsNullOrEmpty(s)) return s.Length;`

* `IsNullOrEmpty(s)` returns unknown bool with predicate hook.
* `AssumeFalse` refines `s.Nullness = NonNull`.
* `s.Length` model no longer produces “may throw NRE”; it returns unknown int (or const if `s` was literal).

This is the single most common “precision cliff” in C# code; the predicate hook prevents it.

## Example 2: `if (x is Foo f) return f.Bar;`

* `isinst Foo` produces value with `CondInfo = TypeTest(x, Foo)`.
* branching refines `x` (and/or the isinst result) to runtime type `Foo`, non-null.
* downstream `callvirt Foo.get_Bar` has a better dispatch target and can avoid “unknown override” blowups.

## Example 3: `nullable.GetValueOrDefault()`

* If field-sensitive, the result is exact.
* If not, the model still returns `join(value, default)` instead of “unknown with maybe-throw”.

## Example 4: `span.Slice(1)[0]`

* Slice shifts base by +1 if index is const.
* Indexer returns byref to precise location (or TopByRef).
* `ldind.*` can then read concrete elements in dump-backed mode or remain unknown in static mode.

## Example 5: `Task.FromResult(123).Result`

* FromResult tags the task with known result.
* Result returns constant 123.
* Without this, anything async-shaped becomes unknown immediately.

---

# Suggested MVP defaults (so it stays usable)

* `MaxTypeSet = 8` (beyond that, collapse to `AnySubtypeOf`)
* `MaxPointsTo = 16` (beyond that, collapse to top ref target)
* `MaxStructFields = 16` (field-sensitive only for “small structs”; else top-struct)
* `Unknown minting`: stable IDs (method+ILoffset+counter), origin labels include origin
* Analysis mode: over-approx join at merges, widen at backedges, optional path-splitting off by default
