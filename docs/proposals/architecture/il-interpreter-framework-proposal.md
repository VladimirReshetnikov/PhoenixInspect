> **Roadmap status: supporting design with substantial research content.** The active product is the dump-backed read-
> only evaluator. The closed W3 concrete opcode/memory proof was strengthened at implementation checkpoint `19c292f9f` and
> formally closed at exact documentation commit `de6cea124`; [GitHub Actions run
> 29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237) passed all four required jobs
> at that exact closure commit. W4.2–W4.5 now prove a narrow dump-free explained-unknown, graph-preparation, and
> interpreted-call path. Exact W4.6a commit `77c92789b16d9258c907d5026a36e39f8c957b41` adds structural pure-model
> selection, exact W4.6b commit `fd723a912` adds modeled-return lineage construction, exact W4.6c commit `877c9fb55`
> executes the frozen capability, and exact W4.6d commit `da5346813` closes compiler/SRM conformance. CFG/fixpoint
> analysis, broad unknown propagation, generalized call dispatch, and abstract-
> analysis reuse remain gated research. API sketches below are illustrative, not current contracts.

Below is a **low-level technical proposal** for a reusable **CIL (ECMA-335) interpreter framework** whose distinguishing hypothesis is that incomplete concrete state, provenance-bearing unknowns, and later abstract interpretation can share opcode semantics.

The implemented checkpoint is intentionally narrower than that hypothesis. Its normative contract is
[Concrete IL Execution](concrete-il-execution-contract-proposal.md): structural method/type/field identities, atomic
metadata-derived activation, frozen typed whole-body admission, concrete E1 arithmetic, and E2 direct or
constant-adjusted `Int32` getter execution through an injected persistent-memory capability. Missing/non-exact memory
evidence blocks without transfer under that W3/default contract. W4.3 adds one conservative dump-free overlay: a
canonical structured partial/unavailable observation for the same frozen ordinary-instance `Int32` field may become
an explained unknown only when the explicit policy and optional field-approximation domain capability are both
present. The current ClrMD execution descriptor remains exact-only.

W4.4/W4.5 extend this implemented boundary with body-independent direct-MethodDef resolution, complete immutable
rooted graph preparation, and exact/explained-unknown interpreted call/return execution. W4.6a adds an explicit
`RequirePureModel` planning profile: after caller-edge resolution/typing and before prospective target-body acquisition,
it selects one exact-confidence/no-effect structural model and freezes a body-free opaque modeled leaf. The default
planner remains interpreted-only; rejected selection never falls back to the target body or exposes a partial graph.
The real compiler graph has one interpreted root, one modeled leaf, two fields, one edge, five traversal units, and
required depth two. Its deterministic PDB-free target PE SHA-256 is
`fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`.

W4.6b adds optional `IPureCallModelLineageDomain<TValue>` and schema-v1 kind-6 `ModeledReturnTransform`: exact operands
are embedded, explained operands receive unchanged kind-4 call nodes, and the complete acyclic batch is interned
atomically and validated for structural replay/fresh-domain continuation. Kinds 1–5 retain their bytes and identities.
W4.6c dispatches only that frozen disposition/capability, with no registry/resolver/descriptor/body reread and no
fallback. Exact or lineage-grounded unknown returns transfer atomically into the caller, memory is unchanged, one
instruction is charged, and no callee frame or frame event is created. Budget rejection occurs before capability
entry. Every actual entry produces an immutable attempt record and advances the logical-depth witness; invocations,
completed transfers, active-frame depth, and logical depth remain separately auditable. Failed or malformed outcomes
leave semantic state unchanged and receive stable capability/outcome/lineage/invariant failure codes.

W4.6d proves the resulting path through the real compiler and SRM adapter: interpreted/model/CoreCLR exact agreement
and interpreted/model agreement for both degraded-evidence shapes. The mixed case freezes modeled graph SHA-256
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`; repeated and fresh
metadata-reader/domain/machine runs reproduce the both-unknown graph SHA-256
`31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`.

W4.6a realizes 2,959 added LOC (1,210 production plus 1,749 tests/fixture support) and brings W4.1–W4.6a to
19,776 LOC. Its headless matrix passed the strict fifteen-project Release build at zero warnings/errors; unit 371/371;
fast 77/77; dump regressions 5/5 and 1/1; pure-model contracts 49/49; model planner 25/25; legacy planner 35/35; SRM
compiler 1/1; lineage 2/2; both guards; and zero skips with the milestone test selection. W4.6b realizes 1,003 added LOC
(481 production plus 522 tests), with 23 deletions, bringing W4.1–W4.6b to 20,779 LOC. Strict headless builds passed
at zero warnings/errors; focused 8/8, combined legacy-plus-modeled lineage 44/44, and integration call-lineage 2/2
passed with zero skips and the milestone test selection.

W4.6c realizes 2,734 added LOC (1,425 production plus 1,309 tests) at `877c9fb55`; strict affected builds passed at
zero warnings/errors and its focused lane passed 34/34. W4.6d realizes 956 test LOC at `da5346813`; its focused lane
passed 3/3, aggregate W4 integration passed 13/13, and Fast passed 80/80. Every behavioral invocation used the
headless wrapper and the milestone test selection. W4.6 totals 7,652 LOC and brings W4.1–W4.6 to 24,469 LOC.

Historical full-W4 projections remain 16,860–25,310, 18,532–26,132, 19,228–25,728, 21,179–26,779,
24,013–29,313, 25,017–29,417, 27,217–32,117, 28,376–32,476, 28,876–33,276, 28,826–33,726, and
28,879–33,279, and pre-W4.6c/d closure 30,079–33,729 LOC. W4.6c/d realized 3,690 LOC against their historical
3,400–3,750 estimate. W4.7 subsequently realized 2,801 LOC, W4.8 11,924 LOC, and W4.9 2,698 LOC, bringing full W4
implementation to 41,892 LOC; exact hosted closure passed in run 29463426083.

This is written as if you were going to **build and maintain this as a core library**—usable for:

* dump-time evaluation (no live runtime)
* partial interpretation (some values concrete, others unknown)
* symbolic-ish execution (optional)
* abstract interpretation for static analysis (nullness, origin labels, ranges, effects, etc.)
* method purity checks (“does this getter touch the world?”)
* “explainability” tooling (why did this become unknown?)

---

# IL Interpreter Framework Proposal

## 0. Executive intent (technical, not marketing)

Build a library that can **execute IL in a virtual machine**. The first bullet is implemented in W3/W4.5. W4.2–W4.5
implement the branchless, dump-free explained-unknown subset of the second bullet across one interpreted call;
W4.6 adds a frozen executable pure-model boundary and canonical modeled-return lineage. Branching, fixpoint,
broader unknown-source behavior, and generalized modeled dispatch still describe gated research modes:

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

For the landed W4.2–W4.5 profile, admitted arithmetic, one exact-shape `ldfld`, and one interpreted direct call may
continue from a validated explained unknown. W4.6 can also construct and transfer the canonical relation for its one
frozen pure-model boundary. Any later missing-state or broader modeled boundary may continue only
after its own scenario-specific admission. Such a value is:

* typed (at least as well as IL types allow)
* composable through subsequent instructions
* optionally constrained (range/nullness/type set)
* traceable (“unknown because: Socket.Receive”, “unknown because: missing field data”, “unknown because: calli/low-level”, etc.)

W3 deliberately stops before transfer on unsupported IL or non-exact evidence. W4.3 changes only canonical structured
partial/unavailable evidence for the already admitted field shape; code-only outcomes, conflicts, invalidity, missing
policy/capability, and every unsupported instruction still stop. Unsupported IL is never reinterpreted as an
unknown-producing operation.

### G2: Pluggable abstract domains and memory models

Support **concrete**, **hybrid**, and **pure abstract** execution by swapping:

* `IValueDomain<TValue>` (constants, nullness, ranges, origin labels, etc.)
* `IMemoryModel<TValue,TMemory>` plus `IPersistentMemoryState<TMemory>` (virtual heap, summary heap with aliasing,
  prepared dump-evidence adapter, etc.)

### G3: Sound(ish) abstract interpretation on real IL

Build CFG (including EH optionally), run fixpoint iteration with `join/widen`, and expose invariants at IL offsets / blocks.

### G4: Practical call modeling

Calls are where interpreters die. The current implementation deliberately proves only one interpreted direct-call
shape plus one exact/no-effect body-free model-selection shape. The following broader facilities remain goals:

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
* Not required to exactly replicate CLR corner behavior for unverifiable or low-level IL
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

W3 realizes the backend-neutral seam as `IResolutionServices`: one operation returns an atomic body/signature/local
`ResolvedMethodDefinition`, and another resolves a contextual field operand to a frozen `ResolvedField`. The SRM
adapter projects disk metadata for differential tests; the ClrMD host projects exact counted dump metadata and
revalidates the physical method bytes without placing either backend in core execution.

### Layer B: Core execution semantics (stack machine)

Implements instruction semantics in terms of:

* push/pop `TValue` on evaluation stack
* read/write locals and arguments
* load/store through `IMemoryModel`
* control transfers (branches, switch, leave)
* call/newobj dispatch via `ICallDispatcher`

The dependency-closed current subset is `nop`, I4 constants, argument/local loads, local stores, unchecked
`add`/`sub`/`mul`, exact or admitted explained-unknown `ldfld`, one prepared static direct `call`, and `ret`. W4.5
executes that call for frozen interpreted edges. W4.6 can instead freeze and execute the required exact/no-effect
target as a body-free modeled edge. Branches, allocation opcodes, writes, EH,
byrefs, generalized calls, and broader unknown-aware continuation remain research. Whole-body admission rejects an
unsupported suffix before any prefix executes.

### Layer C: Analysis engines (strategies)

* **Concrete stepper** (single path)
* **Hybrid stepper** (unknown propagation)
* **Over-approx executor** (joins at merges)
* **Abstract interpreter** (CFG + fixpoint)
* (Optional) **path explorer** (fork states on unknown branches)

All reuse Layer B semantics.

### Layer D: Models and policies

The implemented layer is currently just the bounded non-generic pure-model identity/descriptor/invocation/outcome/
registry vocabulary, exact/no-effect structural selection, and optional modeled-return-lineage domain operation. It
has no model execution, general effect dispatcher, or ambient inputs. The broader research layer includes:

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

The sketches in this section show the intended extension shape. They are not source-compatible transcriptions of the
current implementation; the current surface is recorded in
[Contract Inventory](prototype-interface-catalog-proposal.md).

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

Current W3 narrows this interface for executable evidence. `LoadField` accepts a frozen `ResolvedField` and returns
`MemoryLoadResult<TValue>` (`Exact`, `Partial`, `Unavailable`, `Conflict`, `Invalid`, or structured
`TargetException`). The machine injects this capability and performs exactly one load for its one admitted `ldfld`.
Other members in the sketch are concrete-memory experiments or later opcode requirements, not admitted transfers.

### 4.4 Calls and effects

The following generic dispatcher is a research sketch, not the implemented W4 contract. Current W4.5 interpreted-call
execution and W4.6 non-generic pure-model execution are bound directly to a frozen graph; the latter has one exact,
no-effect capability and no fallback.

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
* or “attach origin labels to anything that depends on `ReadEnv`”
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
* plus domain-specific facts (constant/range/nullness/type set/origin labels)

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
* origin-label propagation
* “does this method read environment?”
* identifying potential exceptions
* inferring simple invariants

---

## 8. Method calls: the hard part, designed explicitly

Current W4 resolution is intentionally narrower than the generalized design below. `call` accepts only one exact
same-module static `Int32(Int32,Int32)` MethodDef shape. Default preparation recursively admits its interpreted body;
explicit W4.6a preparation selects the required exact/no-effect descriptor after edge resolution/typing and before
target-body access. There is no fallback: all failed selections reject atomically. W4.6c executes the already-frozen
model boundary without making any of the broader dispatch policies candidates.

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

The following dispatcher chain is future research. The implemented W4.6a registry is structural, required, exact-
only, and fail-closed; it does not cascade to interpretation or havoc. A generalized dispatcher might consult:

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

### 8.3 Effects and result confidence

The dispatcher emits effect summaries:

* `ReadEnv` for socket/file/time
* `Native` for P/Invoke
* `Reflection` for APIs that can observe runtime state or load types
* `Threading` for APIs that imply concurrency

Consumers can then implement policies:

* “expression evaluator in dump mode: disallow any `ReadEnv|WriteEnv|Threading|Native`”
* “static analysis: propagate origin labels if `ReadEnv`”
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
* buffer elements `buf[0..n)` become unknown bytes (origin-labeled `Env:Socket`)
* may throw `SocketException`

Subsequent IL reads bytes and computes:

* if it reads `buf[0]`, result is unknown byte (origin-labeled)
* arithmetic with origin-labeled unknown yields origin-labeled unknown
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

The W3 corpus currently compares metadata-derived E1 arithmetic, unchecked overflow, void return, and E2
direct/adjusted getter plus typed-null behavior with CoreCLR. Reflection supplies only the result/exception oracle;
SRM supplies the interpreter activation shape. The same/fresh metadata replay cases reproduce canonical transcripts.

The generated dump E2 case goes further: method shape/body and the exact correlated `Int32` field cell come from
counted dump evidence, and closing/reopening/rebinding the dump reproduces structural identities, state, memory,
budget, events, and transcript. This passed locally at strengthened checkpoint `19c292f9f`, whose four hosted jobs passed in
[implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29374585767). W3 then closed formally at
exact documentation commit `de6cea124` when [run
29375584237](https://github.com/VladimirReshetnikov/PhoenixInspect/actions/runs/29375584237) passed all four required jobs.

For later supported verifiable IL:

* generate small methods, run in real CLR, compare to interpreter concrete mode
* cover tricky instructions: `box`, `unbox.any`, `constrained`, `initobj`, EH, generics

### 13.2 Abstract soundness sanity tests

Not formal proofs, but “no false negatives” checks:

* if abstract says condition impossible, ensure concrete cannot satisfy (fuzz)

### 13.3 Model correctness tests

W4.6a tests structural model contracts and planning rather than model output: 49/49 contract cases, 25/25
model-planner cases, 35/35 legacy-planner cases, and one real SRM compiler case. W4.6b tests the lineage operation in
focused 8/8, combined 44/44, and integration 2/2 lanes. W4.6c adds exact/unknown output agreement, attempt, charging,
depth, atomicity, and unit conformance in a 34/34 lane; W4.6d adds compiler/SRM exact, degraded, and fresh-session
execution conformance in a 3/3 lane. For each later intrinsic/BCL model:

* property-based tests comparing model vs real execution on random inputs
* explicitly test “effects emitted match expectations”

---

## 14. Proposed deliverables / packages

This is a capability catalog, not the active package plan. Current W3–W4.6 realizes the core single-path E1/E2
semantics, one prepared interpreted call, one prepared pure-model call, `ConcreteDomain`, provenance-aware modeled-
return lineage, persistent allocated/imported `ConcreteMemory`, and two resolver adapters inside the ten
evidence-bearing source projects. It does not create the speculative generalized domain/model/analysis packages below.

1. **Core**

* IL decoder, signature parsing adapters
* instruction semantics engine
* execution strategies (single path / over-approx)

2. **Domains**

* `ConcreteDomain` (boxed primitives + virtual heap refs)
* `HybridDomain` (optional concrete + abstract facts)
* `NullnessDomain`, `RangeDomain`, `TypeSetDomain`, `OriginLabelDomain`
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

The long-term design aims to avoid two classic failure modes:

1. **“We can’t evaluate that, stop.”**
   W4.2–W4.6 now return an explained unknown and continue for their closed arithmetic, structured-field, interpreted-
   call, and pure-model shapes. Unsupported IL/non-exact evidence and broader continuation require separately admitted
   contracts.

2. **“We built a concrete interpreter and now want static analysis.”**
   Instead: the semantics are domain-parametric from day one; abstract interpretation is a strategy, not a rewrite.

And it gives you an honest, composable way to say:

* “this value is known”
* “this value is unknown but origin-labeled from socket”
* “this branch refines nullness”
* “this method is pure except for a native call”
* “this invariant holds at this IL offset”
