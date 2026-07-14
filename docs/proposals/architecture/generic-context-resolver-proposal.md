> **Roadmap status: research backlog beyond the first dump/query slice.** The supported baseline is `MethodTable`-anchored recovery for reified instance types plus typed-unavailable fallback. Private CLR dictionary decoding is a version-pinned research spike, not a promised general solution.

Below is a best-effort candidate design for reconstructing generic type/method instantiations from a dump, especially shared generic code and state-machine frames.

I’m going to treat this as a concrete subsystem: **`GenericContextResolver`**. It lives in the “bridge layer” between **ClrMD** (runtime view of the dump) and **metadata/PDB readers** (AsmResolver, etc.). It emits a *substitution map* you can feed into your IL interpreter and member resolver.

---

## Why generics are uniquely painful in dumps

### 1) Canonical instantiation (`System.__Canon`) is *real*, not corruption

CoreCLR uses a *canonical* instantiation where generic arguments are `System.__Canon` as a representative for “all compatible reference-type instantiations”. The runtime’s own docs define **Canonical Instantiation** as “all generic arguments are `System.__Canon`” and explicitly say it’s used to carry information shared by all instantiations. ([GitHub][1])

So when you see `Foo<System.__Canon>`, it’s often not “wrong”; it’s “this code/type is in a shared/canonical form”.

### 2) Shared generic code relies on *generic dictionaries*

The runtime+JIT uses **generic dictionaries** to recover instantiation-specific information at runtime. The CoreCLR “Shared Generics Design” doc spells out the mechanism:

* shared generic code uses a **generic context** (e.g., an `InstantiatedMethodDesc`)
* then loads a dictionary pointer (`m_pPerInstInfo`)
* then loads slots (type handles, method handles, field handles, entrypoints, etc.) ([GitHub][2])

It also states that:

* dictionaries are lazily populated
* and the **first N slots** (for N generic args) are always the **type handles of the instantiation arguments** ([GitHub][2])

That’s the key to de-canonicalizing.

### 3) ClrMD may surface `__Canon` even when a “more exact” view exists

This is not hypothetical. There are real cases where one ClrMD code path yields the “specific” type argument while another yields `__Canon`. For example, this issue shows `GetMethodByAddress` reporting `SZGenericArrayEnumerator<TaleWorlds.Core.PieceData>` while heap enumeration reports `SZGenericArrayEnumerator<System.__Canon>`. ([GitHub][3])

So: **you must design for contradictory evidence** and pick the best available instantiation per use-case, with graceful degradation.

### 4) Async/iterator state machines add a mapping layer

Your *physical* executing method is usually `MoveNext()` on a compiler-generated type, while the *logical* method the user cares about is the kickoff method (`Foo<T>()`, `Bar<T1,T2>()`, etc.). Generic parameters are often “lifted” into the state machine type’s generic parameters, so the generic context may be recoverable from the state machine instance (`this`) even when the original generic method instantiation is not.

---

## What we’re reconstructing, exactly

Your IL interpreter sees generic variables in signatures and IL as:

* **type (class) generic parameters**: `!0`, `!1`, …
* **method generic parameters**: `!!0`, `!!1`, …

(That distinction is foundational in IL generics. ([Microsoft][4]))

So the job is to build:

```text
GenericContext =
  TypeArgs   : [TType0, TType1, ...]   // substitutes !0, !1, ...
  MethodArgs : [TMethod0, ...]         // substitutes !!0, !!1, ...
  Evidence   : per-arg provenance + confidence
```

And we need it in two “coordinate systems”:

1. **Physical context**: what the currently executing frame is *actually* running (often state-machine `MoveNext`).
2. **Logical context**: what the debugger UI wants to show/evaluate (kickoff method in PDB terms).

---

## Core design: `GenericContextResolver`

### Inputs

* **ClrMD runtime view**

  * `ClrMethod` gives you `MethodDesc`, `MetadataToken`, module, etc. (and IL via `GetILInfo()` in ClrMD 3.x)
  * `ClrType` gives you `MethodTable`, `MetadataToken`, name, etc. ([GitHub][5])
  * thread stack roots enumeration (for “find `this`”) is available in ClrMD (`EnumerateStackObjects`) ([minidump.net][6])

* **Metadata/PDB view**

  * Method/type definitions, generic param counts, constraints, MethodSpec signatures, state machine mapping info.

### Output

* `GenericContextResolutionResult`:

  * `GenericContext Physical`
  * optionally `GenericContext Logical` (kickoff method)
  * `ResolutionReport` (diagnostics + confidence)

---

## Resolution strategy: multiple evidence sources, ranked, reconciled

Think of this as “reconstructing a generic instantiation in a partially observed system”.

### Evidence sources (highest value first)

#### A) Instance `this` object (best for type args, great for state machines)

If the frame is an instance method and you can identify `this` as a heap object:

* its **MethodTable** points at a runtime type that (in principle) carries enough information to recover actual arguments at runtime (reified generics). ([Performance is a Feature!][7])
* moreover, the generics design paper notes that class type parameters can be accessed via the `this` pointer because it provides exact instantiation information. ([Microsoft][4])

**Practical dump rule:** `this` is often the only place you can reliably anchor the type instantiation.

How to find `this` in a dump:

* enumerate stack roots for the thread (`EnumerateStackObjects`) ([minidump.net][6])
* filter roots in the stack range belonging to the frame
* score candidates by “assignable to declaring type” + “closest to frame base” heuristics

This is heuristic (because optimized code can move/spill), but it’s surprisingly effective in state-machine frames because the state machine instance is typically a GC-tracked root.

#### B) State machine “lift”: derive kickoff generics from `this` type args

For async/iterators:

* the executing method is typically `StateMachineType.MoveNext()`
* but the state machine type’s generic params are typically a *copy* of:

  * the enclosing type’s generic params, plus
  * the kickoff method’s generic params

So once you have the **closed constructed state machine type**, you can map its type arguments into:

* kickoff declaring type args
* kickoff method args

**Mapping rule (robust, metadata-driven):**

* Let `N = generic-arity(declaring type of kickoff method)`
* Let `M = generic-arity(kickoff method)`
* Let `SM = generic-arity(state machine type)`
* Expect `SM == N + M` (in the common C# patterns)
* Map:

  * `TypeArgs = SMArgs[0..N)`
  * `MethodArgs = SMArgs[N..N+M)`

If `SM != N+M`, don’t guess blindly:

* fall back to name/metadata alignment (e.g., match generic parameter names if present),
* else mark the “unmatched” ones unknown.

This gives you a *logical* generic context even if the physical method is not generic at all (because `MoveNext` is normally non-generic).

#### C) Generic dictionaries (best way to de-canonicalize `__Canon`)

This is the “get out of jail” card for shared generics.

The runtime docs state:

* the **generic dictionary** pointer for a generic method is in `InstantiatedMethodDesc::m_pPerInstInfo` ([GitHub][2])
* dictionaries contain type handles etc. ([GitHub][2])
* and crucially: **first N slots contain type handles of the instantiation arguments** ([GitHub][2])

So, if you can obtain:

* a generic method’s `InstantiatedMethodDesc` (or the `MethodDesc` representing the instantiated method),
* or a generic type’s `MethodTable`,
  you can attempt:
* read dictionary pointer
* read first N slots
* resolve each TypeHandle → `ClrType`

This is how you replace “`__Canon` everywhere” with actual type arguments **even when code is shared**.

**Where does the “generic context” come from?**
The shared generics doc shows the “generic context” is passed/available as a `MethodDesc` for generic methods (and similarly a `MethodTable`-based context exists for generic types). ([GitHub][2])
And a well-known explanation notes the runtime “smuggles” the actual type argument list in hidden places, including a hidden argument to generic calls and data in the type descriptor. ([research.swtch.com][8])

So the resolver should attempt to extract the dictionary via:

* methoddesc-based introspection (preferred)
* frame hidden-arg recovery (best-effort; not guaranteed under optimization)
* fall back to “canonical ref” when unavailable

> **Design choice:** Put this logic behind `IRuntimeGenericIntrospection` so the IL interpreter stays runtime-agnostic.

#### D) Callsite MethodSpec signatures (useful for *targets*, not the current frame)

Inside IL, a `call`/`callvirt` to a `MethodSpec` includes the method’s generic arguments in metadata. If those arguments reference `!i`/`!!j`, substitution requires the current context — but when they’re closed, they can be resolved *even if the current method’s own instantiation is unknown*.

This is huge for async state machines because their IL is full of generic helper calls.

So for each `MethodSpec` token:

1. parse MethodSpec signature from metadata (AsmResolver)
2. substitute generic variables using current `GenericContext` (partial substitution allowed)
3. if still open/unknown, keep it symbolic (don’t fail)

#### E) Constraints + observed values (fallback refinements)

If you cannot recover exact generic args, you can still recover *useful properties*:

* `class` / `struct` / `new()` constraints
* base type / interface constraints
  (type loader doc enumerates constraint kinds and terminology) ([GitHub][1])

Additionally, if a local/field of type `T` currently holds an actual object reference, you can refine “`T`” to “some subtype of object’s runtime type” (or exactly that runtime type if you’re willing to be optimistic).

---

## Handling `System.__Canon` correctly

### Treat `__Canon` as a typed “unknown reference type”, not as an error

The type loader design doc is very explicit: canonical instantiation uses `System.__Canon` and is used to represent all instantiations and shared info. ([GitHub][1])
Shared-generics design explains that the actual instantiation-specific handles are retrieved from generic dictionaries at runtime. ([GitHub][2])

So in your type lattice:

* `System.__Canon` ⇒ `AnyRef` (unknown *reference* type)
* unknown with `struct` constraint ⇒ `AnyVal` (unknown *value* type)
* unconstrained unknown ⇒ `Any` (could be ref or val)

That distinction matters for:

* boxing semantics
* field layout (value-type size vs pointer)
* constrained calls
* array element layout

### De-canonicalization policy

When you see `__Canon` in the reconstructed args list:

1. Attempt dictionary-based recovery (C above).
2. If dictionary not available:

   * keep it as `AnyRef`
   * but carry constraints/refinements (interfaces/base types)
3. Never “invent” a concrete type argument to satisfy constraints.
   The runtime itself special-cases constraint checking for `__Canon` (constraints are ignored for it). ([GitHub][1])
   So your analysis should also tolerate that mismatch.

---

## Degrading gracefully when instantiation can’t be recovered

This is where your earlier “unknown propagation” IL interpreter design pays off.

### Represent partial instantiations explicitly

Each type arg becomes:

```text
TypeArg =
  Known(ClrType)
| CanonicalRef          // represents __Canon
| TypeVar(index, constraints, maybeUpperBounds)
| UnknownAny
```

And each arg records `Evidence`:

* `FromThis`
* `FromDictionary`
* `FromNameString` (last resort)
* `FromConstraint`
* `FromObservedValue`
* etc.

### IL semantics that must not hard-fail on unknown generics

Some examples of “do not stop; propagate”:

#### `box !0`

The generics design describes `box` generalized so it becomes a no-op for reference types. ([Microsoft][4])
So:

* if `!0` is `AnyRef`/`CanonicalRef` ⇒ treat as no-op
* if `!0` is known value type ⇒ allocate boxed object in virtual heap
* if `!0` is `Any` (unknown ref/val) ⇒ result is `UnknownObject` with “maybe boxed” flag

#### `constrained. !0` followed by `callvirt`

Behavior depends on whether `!0` is a value type.

* if known value type ⇒ constrained call (potentially non-virtual direct call)
* if known ref type ⇒ normal callvirt
* if unknown ⇒ result becomes `Unknown` and side effects become “unknown”; but you still keep going

#### `ldtoken !0` / `typeof(T)`

If `T` unknown:

* return a symbolic `TypeHandleValue(unknown)` (not `null`)
* if constraints include a base type/interface, keep that attached

#### Field access (`ldfld`) when layout depends on `T`

If you’re reading a field from a concrete runtime object:

* prefer using ClrMD’s resolved field info for that runtime type (offset already correct)
  If you only have a metadata-open type:
* if `T` could be value-type ⇒ layout unknown ⇒ field read becomes `Unknown`
* if `T` is definitely ref-type (`AnyRef`) ⇒ layout stable in many cases, but still don’t assume; treat as “best-effort” only

### UI/UX for “unknown due to generics”

For an expression evaluator, “unknown” is acceptable *if it’s explainable*.

Return:

* value result: `Unknown<T>` (or `Unknown` with type bounds)
* plus a diagnostic trace:

  * “method instantiation not recoverable (optimized frame; no dictionary context)”
  * “type parameter `T` treated as `AnyRef` due to canonical instantiation”
  * “refined by constraint: `T : IDisposable`”
  * etc.

This prevents the tool from feeling flaky: it’s explicit about what it knows.

---

## Special focus: MethodSpec-heavy async/iterator `MoveNext`

This is the common hot path.

### What you can usually recover

* The state machine instance is often reachable as an object (boxed async state machine or iterator class instance).
* Therefore you can often recover **closed constructed state machine type**, hence generic args.

That gives you:

* the “logical” type/method args for kickoff method
* enough substitution to decode a lot of MethodSpec calls inside `MoveNext`

### What you often can’t recover

* exact instantiation of generic helper methods when they’re fully inferred and optimized away
* hidden generic context arguments for static/shared generic code in heavily optimized frames

So your policy should be:

* “use state machine `this` as the primary anchor”
* “use MethodSpec signatures for targets”
* “use dictionaries to de-canonicalize if possible”
* “otherwise propagate unknown”

And you’ll still get a *very* usable evaluator.

---

## Do we need a layer in between ClrMD and metadata? Yes — and this is why

Generic context reconstruction is exactly where you don’t want ClrMD concerns bleeding into your interpreter:

* ClrMD gives you addresses (`MethodDesc`, `MethodTable`), IL bytes, and heap/stack traversal ([GitHub][5])
* metadata gives you generic variables, constraints, MethodSpec signatures
* runtime docs tell you how shared generics use dictionaries to recover instantiation-specific handles ([GitHub][2])

So the “between layer” should expose *one clean thing*:

```csharp
interface IGenericContextResolver
{
    GenericContextResolutionResult ResolveForFrame(FrameContext frame);
    // Optionally: ResolveForMethodCallsite(MethodSpecToken, GenericContext current);
}
```

Internally it can have multiple strategies:

* “public ClrMD only” (fast, imperfect; may leave __Canon)
* “ClrMD + DAC/private introspection” (better)
* “ClrMD + dictionary decoding” (best if you can implement)
* name-string parsing fallback (always last)

The IL interpreter itself should only ever see:

* `GenericContext` (with unknowns allowed)
* `TypeValue`/`AbstractValue` lattice values

---

[1]: https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/coreclr/botr/type-loader.md "https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/coreclr/botr/type-loader.md"
[2]: https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/coreclr/botr/shared-generics.md "https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/coreclr/botr/shared-generics.md"
[3]: https://github.com/microsoft/clrmd/issues/579 "https://github.com/microsoft/clrmd/issues/579"
[4]: https://www.microsoft.com/en-us/research/wp-content/uploads/2001/01/designandimplementationofgenerics.pdf "generics.dvi"
[5]: https://raw.githubusercontent.com/microsoft/clrmd/main/src/Microsoft.Diagnostics.Runtime/ClrType.cs "raw.githubusercontent.com"
[6]: https://minidump.net/dumping-stack-objects-with-clrmd-c002dab4651b/ "https://minidump.net/dumping-stack-objects-with-clrmd-c002dab4651b/"
[7]: https://mattwarren.org/data/2018/03/clrgen-types.html "CLR Generic Types"
[8]: https://research.swtch.com/generic "https://research.swtch.com/generic"
