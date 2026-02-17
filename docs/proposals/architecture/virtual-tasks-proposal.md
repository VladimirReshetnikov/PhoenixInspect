# Proposal: First-class async/await + virtual `Task` semantics in a dump-time IL interpreter

## 1) Motivation and constraints

Interpreting *user IL* in a post-mortem dump is feasible as long as we avoid “real world” side effects. Interpreting *TPL internals* to construct and schedule `Task`s is a different beast: it drags in thread pool scheduling, execution context flow, timer infrastructure, cancellation registration, and a lot of implementation details that are irrelevant to what the user *means* by `async` code.

We want the same move we discussed for `dynamic`:

* **Recognize the compiler pattern** (async state machine + method builder + awaiter),
* **lift it into a semantic model** the interpreter understands,
* and **provide a virtual implementation** that is deterministic, side-effect free, and stepping-friendly.

The key enabler is that C# async code is *already compiled into an explicit state machine*, and the runtime contract is expressed via `IAsyncStateMachine` (`MoveNext`, `SetStateMachine`) and async method builders (`AsyncTaskMethodBuilder`, `AsyncTaskMethodBuilder<T>`, …). ([Microsoft Learn][1])

---

## 2) Background: what the compiler emits (what we’ll exploit)

### 2.1 Kickoff method + state machine type

For an `async` method, the compiler emits:

* a **stub/kickoff method** with the original signature, and
* a **generated state machine type** (often a struct in Release) that contains the logic,
* and it annotates the stub with `AsyncStateMachineAttribute` pointing to the state machine type. ([Microsoft Learn][1])

That attribute exists specifically so tools can identify the corresponding state machine. ([Microsoft Learn][1])

### 2.2 `IAsyncStateMachine` contract

The generated type implements `IAsyncStateMachine`, which defines:

* `MoveNext()` — advance the state machine
* `SetStateMachine(IAsyncStateMachine)` — configure it with a heap-allocated replica ([Microsoft Learn][2])

### 2.3 The “await” lowering shape inside `MoveNext`

A typical `MoveNext` has the classic structure:

* switch/if on `state` to select resume point
* compute awaiter
* if `!awaiter.IsCompleted`:

  * set `state`
  * stash awaiter into a field
  * call `builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine)`
  * `return`
* on resume:

  * reload awaiter field
  * set `state = -1`
  * `awaiter.GetResult()`
* then `builder.SetResult()` / `builder.SetException(ex)` ([Aaron Bos][3])

The builder APIs are explicitly described as scheduling the state machine to proceed when the awaiter completes, and as setting success/failure on the associated task. ([Microsoft Learn][4])

### 2.4 Awaiter APIs we care about

For “normal” tasks, the compiler uses:

* `TaskAwaiter` / `TaskAwaiter<TResult>` (with `IsCompleted`, `GetResult`, `OnCompleted` / `UnsafeOnCompleted`). ([Microsoft Learn][5])
* `ConfiguredTaskAwaitable.ConfiguredTaskAwaiter` for `ConfigureAwait(...)`. ([Microsoft Learn][6])
* often also `ValueTaskAwaiter` / `ValueTaskAwaiter<TResult>` in modern codebases. ([Microsoft Learn][7])

Stephen Toub also notes an important semantic wrinkle: default async task builders treat `OperationCanceledException` specially and translate it into a canceled task rather than a faulted task. ([Microsoft for Developers][8])

### 2.5 Debug info: mapping user method ↔ `MoveNext`

Portable PDB explicitly includes tables like `StateMachineMethod` and others that exist to describe async state machines. ([GitHub][9])
This lets us present the execution as if we’re in the original method, even though we’re executing `MoveNext`.

---

## 3) High-level idea

We add an **Async Runtime Model** inside the interpreter host:

* It introduces a **virtual Task object model** (not backed by TPL internals).
* It intercepts async method builder calls (and common awaiter calls) as **intrinsics**.
* It provides a **deterministic “virtual scheduler”** that can resume suspended state machines without any OS threads.

The interpreter still executes IL for user code and the compiler-generated `MoveNext`, but whenever it would cross into TPL to do scheduling/state transitions, we replace that with our model.

---

## 4) Core entities and data model

### 4.1 `AsyncMethodDescriptor`

Produced by an analysis step from metadata + symbols:

```csharp
sealed record AsyncMethodDescriptor(
    MethodKey KickoffMethod,             // original signature
    MethodKey MoveNextMethod,            // state-machine body
    TypeKey StateMachineType,
    BuilderKind BuilderKind,             // Task | Task<T> | Void | Custom
    TypeKey BuilderType,                 // AsyncTaskMethodBuilder, AsyncTaskMethodBuilder<T>, …
    TypeKey ReturnType,
    IReadOnlyList<AwaitPoint> AwaitPoints // optional, derived from IL scan
);
```

How we compute this:

* Primary: `AsyncStateMachineAttribute.StateMachineType` on kickoff. ([Microsoft Learn][1])
* Verify the state machine implements `IAsyncStateMachine` and locate `MoveNext`. ([Microsoft Learn][2])
* Prefer symbol/PDB mapping (`StateMachineMethod`) when available. ([GitHub][9])
* Determine builder type:

  * Default builders for `Task` / `Task<T>` are `AsyncTaskMethodBuilder` / `AsyncTaskMethodBuilder<T>`. ([Microsoft Learn][4])
  * But the builder can be overridden via `AsyncMethodBuilderAttribute` (including on methods, per the C# feature spec). ([Microsoft Learn][10])

### 4.2 Virtual task model

We create an interpreter-side payload (not TPL-backed):

```csharp
enum VirtualTaskStatus { Created, Running, Waiting, RanToCompletion, Faulted, Canceled }

sealed class VirtualTaskState
{
    public VirtualTaskStatus Status;
    public object? Result;                 // boxed result for Task<T>
    public ExceptionState? Exception;      // interpreter exception payload
    public CancellationState? Cancellation;

    // The async method that produced this task (if any)
    public AsyncActivation? Producer;

    // Continuations waiting on this task
    public List<AsyncContinuation> Continuations = new();
}
```

The interpreter’s “heap object” for a task can be:

* a *real* `System.Threading.Tasks.Task` object instance in the virtual heap (so the type system/UI sees “Task”), **plus**
* an attached `VirtualTaskState` payload (side table keyed by object identity).

This gives you:

* correct static typing from metadata,
* but semantics fully controlled by the interpreter.

### 4.3 Async activation (a resumable state machine instance)

```csharp
sealed class AsyncActivation
{
    public AsyncMethodDescriptor Method;
    public ObjectRef StateMachineObject;     // virtual object representing the generated SM
    public ObjectRef TaskObject;             // the returned virtual Task/Task<T>
}
```

We can always resume by calling `MoveNext` again on the same state machine object. That’s the whole point of the lowering. ([Aaron Bos][3])

---

## 5) Intrinsics: where we “cut” TPL out of the execution

This is the key implementation technique: we register **call models** for specific framework methods, so the interpreter never enters their IL.

### 5.1 Async method builder intrinsics (Task / Task<T>)

For `AsyncTaskMethodBuilder` and `AsyncTaskMethodBuilder<TResult>`, we model these methods (at minimum):

* `Create()` — returns a builder value (we bind a `VirtualTaskState` to the builder storage location).
* `Start<TStateMachine>(ref TStateMachine)` — triggers the first `MoveNext` call.
* `get_Task` — returns the associated Task object. ([Microsoft Learn][4])
* `SetResult(...)` — completes task successfully. ([Microsoft Learn][4])
* `SetException(Exception)` — completes task faulted/canceled. ([Microsoft Learn][4])
* `AwaitOnCompleted` / `AwaitUnsafeOnCompleted` — turns “await” into suspension + continuation registration. ([Microsoft Learn][4])
* `SetStateMachine(IAsyncStateMachine)` — generally a no-op in the virtual world (we already keep the SM object stable), but we accept it. ([Microsoft Learn][4])

> Why by “builder storage location”?
> Because the builder is a struct field on the state machine; the IL takes its address (`ldflda`) and calls methods on it. We need a stable identity for “that builder instance”, and in an interpreter the easiest stable identity is the lvalue location (“address”) of that struct field.

#### `SetException` special casing for cancellation

When `SetException(ex)` is called:

* if `ex` is (or may be) `OperationCanceledException`, mark the virtual task **Canceled** rather than Faulted. ([Microsoft for Developers][8])
* otherwise mark **Faulted**.

### 5.2 Task / awaiter intrinsics (to keep us out of TPL)

We also need to avoid interpreting framework awaiter code. For the common set:

**`Task` / `Task<T>`**

* `GetAwaiter()` → returns a *virtual awaiter value* that references the task. ([Microsoft for Developers][8])

**`TaskAwaiter` / `TaskAwaiter<T>`**

* `get_IsCompleted` → query virtual task status. ([Microsoft Learn][5])
* `GetResult()` → if completed successfully, return result/void; if canceled/faulted, throw virtual exception payload. ([Microsoft Learn][5])

**`Task.ConfigureAwait(bool)` and `ConfiguredTaskAwaitable.ConfiguredTaskAwaiter`**

* `ConfigureAwait(bool)` returns a wrapper awaitable that carries `continueOnCapturedContext`.
* its awaiter exposes `IsCompleted`, `GetResult`, `OnCompleted`/`UnsafeOnCompleted`. ([Microsoft Learn][6])
  We don’t need real `SynchronizationContext`; we’ll treat the flag as a scheduling hint we can record.

**(Optional but recommended) `ValueTask` / `ValueTaskAwaiter`**
Modern code uses it a lot; you get the same shape (`IsCompleted`, `GetResult`, `OnCompleted`). ([Microsoft Learn][7])

---

## 6) Execution semantics: how an async method “runs” in our model

### 6.1 Calling an async method (kickoff semantics)

When interpreter evaluates a call to the **kickoff method**:

1. Allocate state machine object (virtual).

2. Initialize fields:

   * captured `this`, parameters, locals are copied per the kickoff IL.

3. Allocate/associate the builder with a `VirtualTaskState`.

4. Run `builder.Start(ref sm)` intrinsic → which runs `MoveNext` until it:

   * completes, or
   * suspends on an await.

5. Return `builder.Task` intrinsic result (the virtual Task object). ([Microsoft Learn][4])

**Important design choice**:
We can either:

* interpret the kickoff method IL (but with builder intrinsics), or
* replace kickoff IL entirely with a single “StartAsync” intrinsic.

I recommend: **interpret kickoff IL but treat it as hidden for stepping**. It is robust across compiler versions and custom builders, and it naturally performs the field copies correctly.

We still present the user as stepping into the *logical method*, using PDB mapping to `MoveNext`. ([GitHub][9])

### 6.2 Interpreting `MoveNext` until suspension

We execute IL normally until we hit the await-suspension call:

* `builder.Await(On|UnsafeOn)Completed(ref awaiter, ref stateMachine)` ([Microsoft Learn][4])

At that intrinsic, we implement **semantic await**:

1. Identify the awaited operation from the awaiter:

   * For `TaskAwaiter`, it references a `Task` (virtual or dump-backed). ([Microsoft Learn][5])
   * For `ConfiguredTaskAwaiter`, same but with scheduling hint. ([Microsoft Learn][6])
   * For unknown awaiter types:

     * either treat as UnknownAwaitable (propagate unknown), or
     * fall back to “pattern await” (call `IsCompleted`, etc.) — but that risks executing user/framework code.

2. Register a continuation:

   * “When awaited completes, call `MoveNext` on this state machine.”

3. Transition the producer task status:

   * from Running → Waiting (or stay Running if you want a finer lattice).

4. Stop interpretation with a structured stop reason:

```csharp
sealed record AwaitSuspension(
    ObjectRef ProducerTask,
    ObjectRef AwaitedTask,
    AsyncActivation Activation
);
```

This is what lets stepping show: “await suspended on task X”.

### 6.3 Completion

When `MoveNext` reaches:

* `builder.SetResult()` / `builder.SetResult(TResult)` → mark virtual task completed and store result. ([Microsoft Learn][4])
* `builder.SetException(ex)` → mark faulted/canceled. ([Microsoft Learn][4])

Then schedule continuations waiting on this task (see next section).

---

## 7) The “virtual scheduler”: resuming without threads

We need a deterministic mechanism for “task completion triggers resumption” without relying on thread pool / native schedulers.

### 7.1 Continuation representation

```csharp
sealed record AsyncContinuation(
    AsyncActivation Activation,
    ContinuationSchedulingHint Hint // e.g. ContinueOnCapturedContext from ConfigureAwait
);
```

### 7.2 Scheduler contract (minimal)

A single-threaded cooperative scheduler is enough:

```csharp
interface IVirtualScheduler
{
    void Enqueue(AsyncContinuation cont);
    bool TryDequeue(out AsyncContinuation cont);

    // Optional: to keep stepping deterministic
    void RunOne();
    void RunUntil(Func<bool> stopPredicate);
}
```

### 7.3 When do continuations run?

We have to pick a policy. For post-mortem debugging, the most important property is: **predictable and explainable**.

Recommended default: **queued, never inline**.

* When awaited completes, we enqueue all continuations.
* They only run when the user (or the debugger engine) explicitly advances execution.

Why? Inline continuations introduce reentrancy surprises, and you’re already in a synthetic world. You can offer an option later to mimic inline execution, but don’t start there.

### 7.4 Resuming

To resume a suspended async method:

* the scheduler creates a fresh interpreter frame for `MoveNext` with the same state machine object as `this`,
* runs again until next suspension/completion.

This matches the `IAsyncStateMachine.MoveNext` contract. ([Microsoft Learn][2])

---

## 8) Handling external tasks from the dump

This proposal is primarily about **virtual** tasks created by the interpreter, but in real post-mortem usage you will await tasks that already exist in the snapshot.

We should model two kinds of task references:

* **VirtualTaskRef**: created by interpreter; full semantics.
* **ExternalTaskRef**: points to a heap object in the dump.

For ExternalTaskRef we need at least `IsCompleted` (for await branching) and preferably “completed successfully / faulted / canceled + result”.

Two approaches:

1. **Conservative default**: external tasks are “Unknown completion” unless you can prove completion from snapshot.

   * `IsCompleted` returns UnknownBool
   * `await` on it yields Unknown / suspends

2. **Pluggable runtime-specific Task projection** (later, but worth designing for):

   * use ClrMD to read Task internal fields for the specific CLR version
   * decode status/result/exception from memory

The key is: keep it behind an interface so the async engine doesn’t depend on fragile field layouts.

---

## 9) Stepping and UX hooks (what this enables)

Even though you asked for interpreter mechanics, it’s useful to call out the debugger-visible outcomes that this proposal makes straightforward:

### 9.1 Step Into an async method

* Call kickoff → start `MoveNext`.
* Show source mapped to the original method (Portable PDB `StateMachineMethod` helps; sequence points typically refer to original source docs). ([GitHub][9])

### 9.2 Step Over an `await`

When `AwaitUnsafeOnCompleted` intrinsic triggers:

* stop with “suspended on task X”
* allow user to:

  * inspect the awaited task
  * “jump to awaited task producer” (follow activation graph)
  * run scheduler once / until resumed / until next user sequence point

### 9.3 Step Into `await FooAsync()`

Because `FooAsync()` returns a virtual task and starts running immediately (until its first suspension), Step Into can:

* run into Foo’s `MoveNext` until it hits first await/suspension,
* then come back to the awaiting point.

This looks very similar to real async debugging, but without threads.

---

## 10) Custom builders: don’t paint yourself into a corner

Modern C# allows overriding async method builders via `AsyncMethodBuilderAttribute` (including on methods). ([Microsoft Learn][10])

Design implication:

* The async engine should not hard-code *only* `AsyncTaskMethodBuilder`.
* It should have a “builder adapter” abstraction:

```csharp
interface IAsyncMethodBuilderAdapter
{
    bool CanHandle(TypeKey builderType);
    BuilderInstance CreateBuilder(Location builderStorage);
    ObjectRef GetTask(BuilderInstance b);
    void SetResult(BuilderInstance b, Value result);
    void SetException(BuilderInstance b, ExceptionState ex);
    AwaitRegistration RegisterAwait(BuilderInstance b, AwaiterValue awaiter, AsyncActivation activation);
}
```

For MVP:

* implement adapters for:

  * `AsyncTaskMethodBuilder`
  * `AsyncTaskMethodBuilder<T>` ([Microsoft Learn][4])
  * (optional) `AsyncVoidMethodBuilder` for completeness. ([Microsoft Learn][11])

Then the mechanism extends cleanly to pool-based builders etc.

---

## 11) What changes (if any) are needed in the IL interpreter framework?

This approach mostly fits a well-designed interpreter, but it benefits from two explicit capabilities:

### 11.1 “Yield/suspend” as a first-class interpreter outcome

You need a structured way for an intrinsic to say:

> “I’m suspending execution here; the rest of this method will continue later via a continuation.”

This is not the same as returning a value or throwing an exception.

So add:

```csharp
sealed record StepOutcome
{
    public StepStopReason Reason; // Breakpoint, StepComplete, Exception, AwaitSuspended, …
    public object? Payload;       // e.g. AwaitSuspension
}
```

### 11.2 Stable lvalue identity (“virtual byref locations”)

Builder intrinsics must associate state with *the builder instance*, which is a struct field/local passed by ref. That requires your interpreter to expose a stable identity for byref locations (“address handles”) so you can key side tables by them.

If you already support byrefs for normal IL, you almost certainly already have this concept; you just need to expose it to intrinsics.

---

## 12) Summary of the minimal MVP surface

If you want a crisp MVP boundary that already gives excellent async stepping and expression evaluation:

1. Detect async methods via `AsyncStateMachineAttribute` + `IAsyncStateMachine`. ([Microsoft Learn][1])
2. Implement virtual task payload + status transitions.
3. Intrinsics for:

   * `AsyncTaskMethodBuilder{<T>}.Create/Start/Task/SetResult/SetException/AwaitUnsafeOnCompleted/AwaitOnCompleted` ([Microsoft Learn][4])
   * `Task{<T>}.GetAwaiter`
   * `TaskAwaiter{<T>}.IsCompleted/GetResult` ([Microsoft Learn][5])
   * `ConfiguredTaskAwaiter` equivalents for `ConfigureAwait` (very common). ([Microsoft Learn][6])
4. A deterministic queued virtual scheduler (no threads).
5. PDB mapping for user-facing stepping (Portable PDB `StateMachineMethod`). ([GitHub][9])

---

[1]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncstatemachineattribute?view=net-10.0 "AsyncStateMachineAttribute Class (System.Runtime.CompilerServices) | Microsoft Learn"
[2]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.iasyncstatemachine?view=net-10.0 "IAsyncStateMachine Interface (System.Runtime.CompilerServices) | Microsoft Learn"
[3]: https://aaronbos.dev/posts/async-csharp-below-surface "Asynchronous C#: Below the Surface"
[4]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asynctaskmethodbuilder?view=net-10.0 "AsyncTaskMethodBuilder Struct (System.Runtime.CompilerServices) | Microsoft Learn"
[5]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.taskawaiter?view=net-10.0 "TaskAwaiter Struct (System.Runtime.CompilerServices) | Microsoft Learn"
[6]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.configuredtaskawaitable.configuredtaskawaiter?view=net-10.0 "ConfiguredTaskAwaitable.ConfiguredTaskAwaiter Struct (System.Runtime.CompilerServices) | Microsoft Learn"
[7]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.valuetaskawaiter?view=net-10.0 "ValueTaskAwaiter Struct (System.Runtime.CompilerServices) | Microsoft Learn"
[8]: https://devblogs.microsoft.com/dotnet/how-async-await-really-works/?utm_source=chatgpt.com "How Async/Await Really Works in C# - .NET Blog"
[9]: https://raw.githubusercontent.com/dotnet/runtime/main/docs/design/specs/PortablePdb-Metadata.md "raw.githubusercontent.com"
[10]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncmethodbuilderattribute?view=net-10.0 "AsyncMethodBuilderAttribute Class (System.Runtime.CompilerServices) | Microsoft Learn"
[11]: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncvoidmethodbuilder?view=net-10.0&utm_source=chatgpt.com "AsyncVoidMethodBuilder Struct (System.Runtime. ..."
