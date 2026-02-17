Below is a **low-level technical proposal** for a reusable **CIL (ECMA-335) interpreter framework** whose “killer feature” is that it does *not* require complete concrete runtime state. Instead, it supports **unknown / missing data**, **havoc’ed side effects**, and **abstract interpretation** (fixpoint dataflow), with the same core semantics engine.

This is written as if you were going to **build and maintain this as a core library**—usable for:

* dump-time evaluation (no live runtime)
* partial interpretation (some values concrete, others unknown)
* symbolic-ish execution (optional)
* abstract interpretation for static analysis (nullness, taint, ranges, effects, etc.)
* method purity checks (“does this getter touch the world?”)
* “explainability” tooling (why did this become unknown?)

---

# IL Interpreter Framework Proposal

## 0. Executive intent (technical, not marketing)

Build a library that can **execute IL in a virtual machine**, but where “execution” means:

* For some operations, produce a **concrete result** (e.g., `1 + 2 = 3`)
* For operations that depend on unavailable state (socket, native call, missing heap value, reflection, etc.), produce an **abstract/unknown result** while still advancing control flow
* When branching on unknown conditions, support either:

  * **over-approx** (join states), or
  * **path splitting** (symbolic-ish), or
  * **fixpoint analysis** on CFG (abstract interpretation)

A design goal: **the same instruction semantics** should serve all modes; only the **value domain** and **memory model** change.

---

## 1. Goals

### G1: First-class “unknownness” with provenance

If IL reads from the world, from missing memory, or from unsupported instructions, we should not stop; we should return a value that is:

* typed (at least as well as IL types allow)
* composable through subsequent instructions
* optionally constrained (range/nullness/type set)
* traceable (“unknown because: Socket.Receive”, “unknown because: missing field data”, “unknown because: calli/unsafe”, etc.)

### G2: Pluggable abstract domains and memory models

Support **concrete**, **hybrid**, and **pure abstract** execution by swapping:

* `IValueDomain<TValue>` (constants, nullness, ranges, taint, etc.)
* `IMemoryModel<TValue>` (virtual heap, summary heap with aliasing, dump-backed heap adapter, etc.)

### G3: Sound(ish) abstract interpretation on real IL

Build CFG (including EH optionally), run fixpoint iteration with `join/widen`, and expose invariants at IL offsets / blocks.

### G4: Practical call modeling

Calls are where interpreters die. Provide:

* intrinsic library models (BCL core)
* a method-summary mechanism (user-defined or computed)
* a “havoc policy” for unknown/native/external calls
* effect tracking (reads/writes env, alloc, throws, etc.)

### G5: Deterministic, bounded, debuggable

* budgets (steps, allocations, path splits)
* cancellation
* deterministic “fresh unknown” generation
* tracing with “why/where did precision get lost?”

---

## 2. Non-goals (explicit so the library doesn’t become “a CLR”)

* Not a JIT; no native code execution
* Not required to exactly replicate CLR corner behavior for unverifiable/unsafe IL
* Not a full verification engine
* Not a full symbolic theorem prover (constraint solving is optional and shallow)

---

## 3. Architecture overview (layers)

### Layer A: IL + metadata front-end

Responsible for turning “method token” into:

* decoded IL instruction stream
* exception handling clauses
* signatures (types, locals, params)
* generic instantiation context

**Key point:** the interpreter core must not care whether metadata comes from
`System.Reflection.Metadata`, Cecil, dnlib, runtime inspection, etc.

### Layer B: Core execution semantics (stack machine)

Implements instruction semantics in terms of:

* push/pop `TValue` on evaluation stack
* read/write locals and arguments
* load/store through `IMemoryModel`
* control transfers (branches, switch, leave)
* call/newobj dispatch via `ICallDispatcher`

### Layer C: Analysis engines (strategies)

* **Concrete stepper** (single path)
* **Hybrid stepper** (unknown propagation)
* **Over-approx executor** (joins at merges)
* **Abstract interpreter** (CFG + fixpoint)
* (Optional) **path explorer** (fork states on unknown branches)

All reuse Layer B semantics.

### Layer D: Models and policies

* BCL intrinsic models
* environment/IO models
* “native call” model
* effect policy (block/allow/havoc)
* instrumentation hooks

### Layer E: Virtual debug control plane (refinement)

To support `StepInto`/`StepOver`/`StepOut` over dump-backed sessions, the framework also needs:

* micro-step visibility (`FramePushed`, `FramePopped`, `BranchDecisionNeeded`, `ExceptionThrown`),
* stop reasons (`StepComplete`, `DecisionNeeded`, `ExceptionStop`, `BudgetStop`, `Completed`),
* deterministic session state snapshots suitable for `Undo` and replay,
* statement/source mapping abstraction (PDB first, decompiler second, IL fallback).

This control plane is policy + orchestration; opcode semantics remain in Layer B.

---

## 4. Core abstractions (API sketch)

### 4.1 Metadata and method bodies

```csharp
public interface IMethodBodyProvider
{
    bool TryGetBody(MethodHandle method, out MethodBody body);
}

public sealed record MethodBody(
    ReadOnlyMemory<byte> IlBytes,
    int MaxStack,
    bool InitLocals,
    ImmutableArray<LocalSig> Locals,
    ImmutableArray<ExceptionClause> ExceptionClauses);
```

```csharp
public interface IMetadataResolver
{
    TypeSig ResolveTypeToken(ModuleHandle module, int token, Instantiation inst);
    MethodSig ResolveMethodToken(ModuleHandle module, int token, Instantiation inst);
    FieldSig ResolveFieldToken(ModuleHandle module, int token, Instantiation inst);

    // For callvirt/constrained dispatch you need method semantics:
    MethodHandle ResolveMethodDefinition(MethodSig reference);
    TypeHandle GetRuntimeTypeOfObject(ObjectRef obj); // optional (dump-backed)
}
```

**Design note:** `MethodHandle/TypeHandle/ModuleHandle` are *library-owned opaque IDs* so backends can map them to whatever they have.

### 4.2 Values and domains

The interpreter must never manipulate “raw objects”; it manipulates `TValue`.

```csharp
public interface IValueDomain<TValue>
{
    // Construction
    TValue Top(TypeSig type);
    TValue ConstInt32(int value);
    TValue Null(TypeSig refType);
    TValue FreshUnknown(TypeSig type, UnknownOrigin origin);

    // Lattice
    TValue Join(TValue a, TValue b);
    TValue Widen(TValue prev, TValue next);

    // Queries & refinement
    bool TryGetConstInt32(TValue v, out int value);
    Nullness GetNullness(TValue v);
    TValue RefineNonNull(TValue v);
    TValue RefineNull(TValue v);

    // Primitive ops (domain-specific)
    TValue Add(TValue a, TValue b, NumericKind kind, bool overflowCheck);
    TValue Compare(TValue a, TValue b, ComparisonKind kind); // returns bool-ish TValue
    TValue Conv(TValue v, ConvKind kind);
    TValue Box(TValue v, TypeSig boxedType);
    TValue UnboxAny(TValue v, TypeSig targetType);
    // etc.
}
```

This domain interface is deliberately *not* minimal. You want the transfer functions centralized so instruction semantics aren’t filled with type-specific logic.

### 4.3 State and memory

Execution state for the modern stepping architecture is a machine-level stack of frames (rather than a single-frame `ExecState`).

```csharp
public sealed record MachineState<TValue, TMem>(
    ImmutableArray<FrameState<TValue>> CallStack,
    TMem Memory,
    PathCondition<TValue>? Path,
    EffectSummary Effects,
    BudgetState Budget,
    DeterminismState Determinism);

public sealed record FrameState<TValue>(
    MethodInstance Method,
    int IlOffset,
    EvalStack<TValue> Stack,
    ImmutableArray<TValue> Locals,
    ImmutableArray<TValue> Args,
    TValue This,
    ReturnSite? ReturnSite,
    EhState? Eh,
    FrameFlags Flags);
```

A single-frame helper representation can still exist internally, but host/session contracts should use `MachineState`.

Memory is abstracted:

```csharp
public interface IMemoryModel<TValue, TMem>
{
    // Object/array allocation in virtual heap (if supported)
    bool CanAllocate { get; }
    (TValue objRef, TMem mem) NewObject(TMem mem, TypeSig type);
    (TValue arrRef, TMem mem) NewArray(TMem mem, TypeSig elemType, TValue length);

    // Field/element access
    TValue LoadField(TMem mem, TValue objRef, FieldSig field);
    TMem StoreField(TMem mem, TValue objRef, FieldSig field, TValue value);

    TValue LoadElement(TMem mem, TValue arrRef, TValue index);
    TMem StoreElement(TMem mem, TValue arrRef, TValue index, TValue value);

    // By-ref / managed pointer support
    TValue MakeByRefToField(TMem mem, TValue objRef, FieldSig field);
    TValue MakeByRefToLocal(int localIndex);
    TValue LoadIndirect(TMem mem, TValue byRef);
    TMem StoreIndirect(TMem mem, TValue byRef, TValue value);

    // Havoc support (critical for unknown/native calls)
    TMem Havoc(TMem mem, HavocRegion region, UnknownOrigin origin);
}
```

**Key point:** `Havoc` is not a nice-to-have. It’s what makes “unknown call that writes to memory” analyzable.

### 4.4 Calls and effects

```csharp
public interface ICallDispatcher<TValue, TMem>
{
    CallResult<TValue, TMem> Call(
        ExecContext ctx,
        MachineState<TValue, TMem> state,
        CallSite site,
        ImmutableArray<TValue> args);

    CallResult<TValue, TMem> NewObj(...);
}
```

`CallResult` supports multiple outcomes:

* interpreted callee frame push
* modeled call (pseudo-frame or atomic effect, policy-defined)
* exceptional return
* nondeterminism (optional multiple states)

```csharp
public sealed record CallResult<TValue, TMem>(
    ImmutableArray<MachineState<TValue, TMem>> NextStates,
    ImmutableArray<DebugEvent> Events);
```

Effects are tracked separately:

```csharp
[Flags]
public enum EffectKind { None, Alloc, ReadEnv, WriteEnv, Native, Reflection, Throw, Threading }

public sealed record EffectSummary(
    EffectKind Kinds,
    ImmutableArray<EffectEvent> Events);
```

This enables downstream consumers to say:

* “allow evaluation only if `Effects.Kinds` subset of `Alloc`”
* or “taint anything that depends on `ReadEnv`”
* or “report potential IO in this getter”

---

## 5. Instruction semantics strategy

### 5.1 Decoder emits normalized instructions

Every instruction becomes a compact `Instruction`:

* `OpCode` (including prefixes normalized into flags)
* operand (int32, token, branch target, switch table, etc.)
* IL offset

Prefix handling:

* `constrained.` must be carried to following `callvirt`
* `tail.` can be recorded but may be ignored semantically initially
* `volatile.`/`unaligned.` can usually be ignored in a single-thread model, but preserve for completeness

### 5.2 Stack-typed semantics (you can’t ignore this)

CIL is “typed enough” that op semantics depends on stack type categories:

* I4, I8, R4, R8, native int
* managed ref (O), byref (&), typedref
* value types (inline) vs boxed objects

Even in abstract mode you need *at least* stack-kind tracking to avoid nonsense joins.

Proposal: each `TValue` carries:

* `TypeSig StaticType` (best-known static type)
* `StackKind Kind` (I4/I8/R/Ref/ByRef/ValueType/NativeInt)
* plus domain-specific facts (constant/range/nullness/type set/taint)

The domain API is responsible for conversions and normalizations.

### 5.3 Control transfers

For a branch instruction, interpreter asks domain about condition:

* if condition is **concrete true/false**, take that edge
* if condition is **unknown**, behavior depends on `ExecutionStrategy`:

Strategies:

1. **ConcreteSinglePath**: treat unknown as “cannot decide” ⇒ produce unknown result, or pick a default (not ideal for analysis)
2. **OverApproxJoin**: fork both edges, propagate states, join later (block-level join)
3. **PathExplore**: fork states and keep both with path conditions

All are supported with the same semantics engine.

---

## 6. Unknown / missing data: first-class design

### 6.1 Unknown is not “just Top”

If you want this library to be useful outside toy demos, unknown values must carry *origin* and *shape*:

* **Origin**: why unknown exists

  * environment read (socket/file/time/random)
  * native call return
  * missing heap data (dump gap)
  * unsupported IL feature
  * budget exceeded (timeout)
* **Shape**: constraints that still hold

  * type set (possible runtime types)
  * nullness
  * numeric interval
  * array length known/unknown
  * “this is a reference to some location(s)” (points-to set)

### 6.2 Fresh unknowns are minted deterministically

`FreshUnknown(type, origin)` must be deterministic across runs to keep analysis reproducible.

Implementation detail becomes an API contract:

* unknown IDs are derived from (method, ilOffset, counter, origin-kind)
* for fixpoint, avoid “new unknown each iteration” explosions by canonicalizing

### 6.3 Havoc regions for unknown side-effects

A native/external call shouldn’t necessarily set “entire heap unknown” (too imprecise). Instead:

* If we know it writes to a buffer parameter → havoc that region
* If it writes through `ref` → havoc that addressable
* If it’s completely unknown → havoc “may-write” regions:

  * all reachable objects from args (sound but heavy)
  * or “unknown heap region” summary object (common in abstract interpreters)

This is why memory model must support region selection.

---

## 7. Abstract interpretation mode (static analysis)

### 7.1 CFG construction

Build basic blocks from IL:

* leaders: entry, branch targets, fall-through after branch, switch targets
* terminate blocks at control transfer ops: `br*`, `ret`, `throw`, `leave`, `switch`

Exception handling CFG is optional but should exist:

* try regions and handlers define additional edges from “may-throw” instructions to catch/finally/filter

Configurable levels:

* **CFG.Normal**: ignore exceptions (fast, incomplete)
* **CFG.EHConservative**: assume calls and memory ops can throw, add edges (sounder)
* **CFG.EHPrecise**: whitelist “non-throwing” ops and models (best effort)

### 7.2 Fixpoint algorithm

Classic forward dataflow:

* `IN[block]` / `OUT[block]` as abstract `MachineState` (or a block-normalized projection thereof)
* transfer function is “interpret block from start state to end state” using the same instruction semantics, but in over-approx mode

At merges:

* join stacks/locals/memory using domain join
* if stack heights differ due to unverifiable IL → degrade to Top with diagnostic

Loops:

* widen at back-edges to guarantee termination (domain-provided widen)
* optionally narrow afterward for a few iterations

### 7.3 Interprocedural analysis (optional but planned)

Calls are treated via summaries:

* `Summary(method, abstractArgs) => abstractReturn + abstractSideEffects`

Compute summaries:

* bottom-up SCC analysis on call graph, or
* on-demand with memoization + fixpoint for recursion

If summary unavailable:

* apply configurable fallback:

  * pure unknown return, havoc memory regions, and mark effects
  * or inline up to depth limit

This makes the framework usable for:

* nullability analysis at IL level
* taint propagation
* “does this method read environment?”
* identifying potential exceptions
* inferring simple invariants

---

## 8. Method calls: the hard part, designed explicitly

### 8.1 Dispatch resolution

For `call`:

* resolve target statically from token/signature

For `callvirt`:

* if receiver runtime type known (dump-backed / concrete) → resolve vtable slot precisely
* if receiver type is a set → over-approx to union of possible overrides (join their results)
* if receiver is unknown → fall back to “any override” summary or Top

For `constrained.` + `callvirt`:

* avoid boxing for value types when possible
* for abstract mode, treat as either:

  * direct call on value type if constrained type is value type
  * or virtual dispatch on boxed (over-approx)

### 8.2 Modeling libraries and the environment

The dispatcher consults a chain:

1. **Intrinsic models** (cheap, precise)

   * arithmetic helpers, `string.Length`, `Nullable<T>.HasValue`, etc.

2. **BCL models** (handwritten summaries)

   * common `System.*` APIs with pure semantics or bounded effects

3. **User models**

   * allow host to provide summaries for application code or framework extensions

4. **Fallback policy**

   * interpret IL body if available and allowed (budgeted)
   * otherwise return `FreshUnknown(returnType, Origin.Call(method))`
   * and apply `Havoc` based on “may-write” heuristics

### 8.3 Effects and trust

The dispatcher emits effect summaries:

* `ReadEnv` for socket/file/time
* `Native` for P/Invoke
* `Reflection` for APIs that can observe runtime state or load types
* `Threading` for APIs that imply concurrency

Consumers can then implement policies:

* “expression evaluator in dump mode: disallow any `ReadEnv|WriteEnv|Threading|Native`”
* “static analysis: propagate taint if `ReadEnv`”
* “purity checker: fail if any env effect”

---

## 9. Memory model: concrete vs abstract

### 9.1 Concrete/virtual heap model

For scenarios like “evaluate a method with known input objects”:

* objects have identity
* fields contain `TValue` (often concrete)
* array elements stored
* byrefs point to specific addressables
* strong updates are common

This is also what a dump-backed adapter *resembles*, except reads come from a snapshot and writes go to an overlay.

### 9.2 Abstract/summary heap model

For static analysis:

* references are points-to sets: `{loc1, loc7, null}`
* loads: join values from all possible locations
* stores:

  * strong update if singleton points-to
  * weak update otherwise (join stored value with existing)

Support for a special “unknown heap region”:

* if aliasing too imprecise, collapse many locations into a summary location
* avoids state blowup while remaining conservative

### 9.3 Byrefs are addressables, not raw pointers

A managed pointer in IL can point to:

* local
* argument
* field inside an object
* array element
* interior of a struct

Represent byrefs as `AddressableRef` values that the memory model understands.
In abstract mode, byref may refer to multiple addressables.

This is essential for correctness of C# patterns using `ref` returns, `Span<T>`, etc.

---

## 10. “Unknown propagation” semantics: concrete examples

### Example A: socket read

IL calls a modeled method `Socket.Receive(byte[] buf, ...)`.

Model says:

* return value `n` is unknown int with constraint `0 <= n <= buf.Length`
* buffer elements `buf[0..n)` become unknown bytes (tainted `Env:Socket`)
* may throw `SocketException`

Subsequent IL reads bytes and computes:

* if it reads `buf[0]`, result is unknown byte (tainted)
* arithmetic with tainted unknown yields tainted unknown
* branch on `n > 0` forks/refines:

  * true branch refines `n >= 1`
  * false branch refines `n == 0`

### Example B: P/Invoke

A `call` to `[DllImport] Foo(int* p)`:

* returns unknown
* may write through pointer argument: havoc the pointee region
* mark effects: `Native`

Interpreter continues; analysis can now answer:

* “this method depends on Native”
* “after call, memory pointed to by `p` is unknown”

---

## 11. Explainability: why did I get “unknown”?

Every `TValue` should support:

* a compact **provenance graph** (DAG) of “produced by”

  * instruction offset
  * operation kind
  * operand provenances
  * origin tags

The library should expose:

* `Explain(value)` → a structured explanation tree
* `Explain(state)` → the top contributors to imprecision

This is what turns “unknown propagation” from annoying to usable.

---

## 12. Performance considerations (because interpreters die on overhead)

* Decode IL once; cache `MethodBody + decoded instructions + block map`
* Keep instruction representation blittable/struct-based if possible
* Make `EvalStack<T>` a value-type stack with pooled storage (or specialized for maxstack)
* Avoid allocation in join/widen loops:

  * represent locals/args as arrays with copy-on-write or persistent vectors
  * state interning for fixpoint may be worthwhile
* Deterministic hashing for abstract states to memoize block transfer results
* Budget enforcement must be cheap: decrement counters, not timestamps in tight loops

---

## 13. Test strategy (practical, brutal)

### 13.1 Differential execution (concrete vs CLR)

For supported verifiable IL:

* generate small methods, run in real CLR, compare to interpreter concrete mode
* cover tricky instructions: `box`, `unbox.any`, `constrained`, `initobj`, EH, generics

### 13.2 Abstract soundness sanity tests

Not formal proofs, but “no false negatives” checks:

* if abstract says condition impossible, ensure concrete cannot satisfy (fuzz)

### 13.3 Model correctness tests

For each intrinsic/BCL model:

* property-based tests comparing model vs real execution on random inputs
* explicitly test “effects emitted match expectations”

---

## 14. Proposed deliverables / packages

1. **Core**

* IL decoder, signature parsing adapters
* instruction semantics engine
* execution strategies (single path / over-approx)

2. **Domains**

* `ConcreteDomain` (boxed primitives + virtual heap refs)
* `HybridDomain` (optional concrete + abstract facts)
* `NullnessDomain`, `RangeDomain`, `TypeSetDomain`, `TaintDomain`
* product domain composition (configurable)

3. **Memory models**

* virtual heap (alloc + strong updates)
* summary heap (points-to + weak updates)
* “overlay heap” (write overlay over read-only backend; ideal for dump adapters)

4. **Models**

* minimal BCL intrinsics (string basics, Nullable, simple math)
* environment models (Stream/Socket as unknown sources)
* native call default model (havoc + effect)

5. **Analysis**

* CFG builder
* fixpoint runner
* summary cache infrastructure

---

## 15. Why this design is the right “spine”

It avoids the two classic failure modes:

1. **“We can’t evaluate that, stop.”**
   Instead: return unknown, keep going, and annotate why.

2. **“We built a concrete interpreter and now want static analysis.”**
   Instead: the semantics are domain-parametric from day one; abstract interpretation is a strategy, not a rewrite.

And it gives you an honest, composable way to say:

* “this value is known”
* “this value is unknown but tainted from socket”
* “this branch refines nullness”
* “this method is pure except for a native call”
* “this invariant holds at this IL offset”