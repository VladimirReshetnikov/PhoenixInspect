> **Roadmap status: research backlog.** This is counterfactual stepping from snapshot/assumed state, not replay of
> historical execution. W3's interpreted-method/persistent-memory proof is implemented and locally/hosted-checkpoint
> verified at hardened checkpoint `19c292f9f` and formally closed at exact documentation commit `de6cea124`; [GitHub
> Actions run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four
> required jobs at that exact closure commit. Virtual stepping still
> requires an admitted W4 method-execution slice with deterministic pause/event contracts, source mapping, and
> generalized stop-on-throw behavior. Debugger-grade Step Out additionally requires handler-transfer EH.

Here is a low-level candidate design for virtual Step Into / Over / Out over dump snapshots plus virtual state.

---

## 1) What we’re actually building

### The object you’re debugging

Not the dead process. You’re debugging a **virtual machine state**:

* **Read-only backing store:** the dump (ClrMD heap + thread stacks + module list)
* **Writable overlay:** a virtual heap + overlay writes (fields/array elements/byrefs)
* **Execution model:** IL interpretation with:

  * deterministic evaluation
  * unknown-value propagation
  * “modeled calls” for world interaction / native / unsupported

### The stepping unit

You *can’t* do “instruction stepping” only; users want “source stepping”. So stepping is:

* **IL offset** is the ground truth
* A **debug map** translates IL offset → “statement/sequence point” → source span in:

  * real source (PDB sequence points), or
  * decompiled source (decompiler IL-range mapping), or
  * raw IL view (fallback)

---

## 2) Does this require refining the IL interpreter framework?

Yes. A clean implementation needs four extensions to the W3 kernel:

1. **Generalize the existing call-stack container to multi-frame execution.**
   W3 already stores an explicit root `FrameState` in `MachineState`, but its call-free profile admits exactly one
   frame and has no call/return transfer.

2. **Make calls/returns observable as micro-steps.**
   For Step Into to stop at callee entry, a `call` must push a frame as a discrete event.

3. **Add session-level pause/stop reasons and extend the existing low-level debug events** for call/return,
   exception, decision-needed, and budget boundaries. Machine status and events alone are not a stepping control plane.

4. **Build session history and Undo policy over snapshot-friendly state** via either:

   * the existing persistent immutable semantic state plus retained history (preferred), or
   * checkpoint + reversible write log (works, but harder)

Everything else (unknown propagation, havoc, models) already fits nicely.

---

## 3) Core execution state model for stepping

### 3.1 Machine state and frame state

You want an explicit machine state:

```csharp
public sealed record MachineState<TValue, TMem>(
    ImmutableArray<FrameState<TValue>> CallStack, // top is last
    TMem Memory,
    PathCondition<TValue>? Path,                  // optional
    EffectSummary Effects,
    BudgetState Budget,
    DeterminismState Determinism                  // unknown IDs, branch decisions, etc.
);

public sealed record FrameState<TValue>(
    MethodInstance Method,     // MethodId + generic context + module identity
    int IlOffset,              // instruction pointer
    EvalStack<TValue> Stack,   // evaluation stack
    ImmutableArray<TValue> Locals,
    ImmutableArray<TValue> Args,
    TValue This,
    ReturnSite? ReturnSite,    // null for root frame
    EhState? Eh,               // try/finally/catch tracking
    FrameFlags Flags           // e.g. JustEntered, IsModelFrame, HiddenNonUserCode
);

public sealed record ReturnSite(
    MethodInstance CallerMethod,
    int CallerResumeOffset,          // where to continue after return
    StatementId CallerCallStatement  // statement containing the call site
);
```

Notes:

* `MethodInstance` must include resolved generics context (possibly partial).
* `StatementId` is an opaque “source step identity” supplied by your debug map layer.

### 3.2 Debug events and stop reasons

The interpreter should emit discrete events:

```csharp
public enum DebugEventKind
{
    InstructionExecuted,
    FramePushed,
    FramePopped,
    ExceptionThrown,
    MemoryWrite,
    UnknownCreated,
    BranchDecisionNeeded,
    BudgetExceeded
}

public sealed record DebugEvent(
    DebugEventKind Kind,
    object Payload);
```

And the stepping driver returns:

```csharp
public enum SessionPauseReason
{
    StepComplete,
    DecisionNeeded,     // unknown branch, ambiguous dispatch, etc.
    ExceptionStop,      // thrown or uncaught exception
    BudgetStop,
    Completed           // root returned
}

public sealed record SessionStepOutcome<TValue, TMem>(
    MachineState<TValue, TMem> State,
    SessionPauseReason Reason,
    ImmutableArray<DebugEvent> Events,
    StepDiff? Diff // optional (see below)
);
```

---

## 4) Interpreter execution API needed for stepping

### 4.1 Micro-step: execute exactly one IL instruction in the current frame

You need a deterministic “micro-step” primitive:

```csharp
public interface IIlMachine<TValue, TMem>
{
    StepResult<TValue, TMem> StepOne(
        MachineState<TValue, TMem> state,
        ExecutionOptions options);
}
```

Where `StepResult` can include:

* 1 next state (deterministic mode)
* or 2+ next states (forked branch / nondeterministic dispatch)

For stepping UX, I recommend making nondeterminism explicit:

```csharp
public abstract record StepResult<TValue, TMem>
{
    public sealed record Single(MachineState<TValue, TMem> Next, ImmutableArray<DebugEvent> Events) : StepResult<TValue, TMem>;
    public sealed record Fork(ImmutableArray<MachineState<TValue, TMem>> NextStates, BranchInfo Info) : StepResult<TValue, TMem>;
    public sealed record Stop(MachineState<TValue, TMem> Same, SessionPauseReason Reason, ImmutableArray<DebugEvent> Events) : StepResult<TValue, TMem>;
}
```

### 4.2 “Run until predicate” helper (optional but highly practical)

Doing Step Over by stepping instruction-by-instruction inside a called method can be slow. Add a helper that loops internally:

```csharp
public interface IIlMachineRunner<TValue, TMem>
{
    StepOutcome<TValue, TMem> RunUntil(
        MachineState<TValue, TMem> start,
        Func<MachineState<TValue, TMem>, ImmutableArray<DebugEvent>, bool> stopPredicate,
        ExecutionOptions options);
}
```

This keeps control in one place and makes it easy to centralize budgets and determinism.

---

## 5) Step semantics as stop predicates

Everything reduces to:

* compute a **baseline**
* run until the baseline changes in a way that matches the command

### 5.1 Statement identity and debug maps

You need a `DebugMap` abstraction:

```csharp
public readonly record struct StatementId(int Value);

public sealed record SourceSpan(
    string DocumentId,
    int StartLine, int StartCol,
    int EndLine, int EndCol,
    bool IsHidden);

public interface IDebugMap
{
    // For a given IL offset, return the active statement / step id.
    StatementId GetStatement(int ilOffset);

    // Optional: get best source span for highlighting.
    SourceSpan? GetSourceSpan(int ilOffset);

    // Optional: find next "stoppable" IL offset after the current one.
    int? GetNextStatementOffset(int ilOffset);
}
```

Then `IDebugMapProvider` returns a map per `MethodInstance`:

* PDB-based map if available
* otherwise decompiler-based map
* otherwise IL map (each “statement” is maybe each instruction or basic block)

### 5.2 Step Over (line/statement stepping)

Baseline:

* `depth0 = callStackDepth`
* `frame0 = top frame`
* `stmt0 = DebugMap(frame0.Method).GetStatement(frame0.IlOffset)`

Stop when:

* call stack depth is **<= depth0** (we’re back in the frame or returned past it)
* and we are in the same frame (or its caller if it returned)
* and the **statement changed** relative to the statement in the original frame

Pseudo predicate:

```csharp
bool StopOver(state)
{
    var depth = state.CallStack.Length;
    if (depth < depth0) return true; // returned out of the frame; treat as done
    if (depth > depth0) return false; // inside callee, keep running
    var top = TopFrame(state);
    return DebugMap(top.Method).GetStatement(top.IlOffset) != stmt0;
}
```

### 5.3 Step Into

Stop when either:

* we entered a new frame (depth > depth0) and the top frame is at entry, OR
* we stayed in the same frame and statement changed (no call taken)

To make this clean, mark `FrameFlags.JustEntered` when pushing a frame.

```csharp
bool StopInto(state, events)
{
    if (state.CallStack.Length > depth0)
    {
        var top = TopFrame(state);
        return top.Flags.HasFlag(FrameFlags.JustEntered)
               && IsStoppable(top); // e.g. has a visible statement or IL boundary
    }
    if (state.CallStack.Length == depth0)
    {
        var top = TopFrame(state);
        return DebugMap(top.Method).GetStatement(top.IlOffset) != stmt0;
    }
    return true; // returned out of frame => stop
}
```

### 5.4 Step Out

Baseline: `depth0 = current depth`.
Stop when depth becomes `< depth0` and we reach the caller after return.

This is easiest if every frame has a `ReturnSite` containing the caller’s call-site statement id.

```csharp
bool StopOut(state)
{
    if (state.CallStack.Length >= depth0) return false;
    var top = TopFrame(state);
    return DebugMap(top.Method).GetStatement(top.IlOffset) != returnSite.CallerCallStatement;
}
```

---

## 6) Calls must become discrete stepping events

This is the single most important refinement to the interpreter semantics.

### 6.1 “call” instruction behavior in the interpreter

When executing `call/callvirt/newobj`:

1. resolve target (may yield one or multiple candidates)
2. if interpretable:

   * create a new `FrameState` for the callee
   * compute `ReturnSite` for the callee
   * push frame
   * **do not execute the callee body yet in the same micro-step**
3. if not interpretable or disallowed:

   * push a **model frame** (more below) OR apply model atomically
   * still emit an event so Step Into has something meaningful

This makes:

* Step Into stop at callee entry (`FramePushed + JustEntered`)
* Step Over run through callee without stopping (stop predicate ignores deeper depth)

### 6.2 Model frames (so Step Into works even for “uninterpretable” calls)

I strongly recommend representing modeled calls as pseudo-method frames with a tiny “method body” that:

* shows the summary/effects/unknowns
* optionally has a few internal “pseudo steps” (e.g., “return unknown”, “havoc buffer”, “throw maybe”)

Implementation technique:

* treat models as a `MethodInstance` with `FrameFlags.IsModelFrame`
* the “IL” is a custom bytecode (or a special opcode stream) consumed by a mini-interpreter
* this keeps the stepping UI uniform (call stack always changes on Step Into)

---

## 7) Source stepping: PDB + decompiler + IL fallback

### 7.1 PDB sequence point map (best)

* Build `IDebugMap` from sequence points:

  * Map IL offsets to a `StatementId` (typically the index of the last non-hidden sequence point ≤ offset)
  * Hidden sequence points (`0xFEEFEE`) are treated as “not stoppable”
* `GetSourceSpan` returns the sequence point’s file+span

### 7.2 Decompiled stepping map (second best)

You need a decompiler that can give:

* decompiled text
* an IL range per AST node / statement
* a mapping from IL offset → nearest statement node

Technique:

* produce a “statement table” where each entry has:

  * `StatementId`
  * `ILRange` (start..end)
  * `TextSpan` (start..end in the decompiled text buffer)
* then:

  * statement lookup is a range query (binary search over sorted ranges, with tie-breaking)

### 7.3 IL stepping fallback

If you have neither, create a trivial debug map:

* `StatementId = ilOffset` (instruction stepping), or
* group by basic blocks (`StatementId = blockId`)

---

## 8) Undo “last step” (and why persistent state pays off)

Because you control the entire virtual state, undo is not a “reverse debugger” problem. It’s a **state history** problem.

### 8.1 Minimal viable Undo (command-level)

Store state snapshots only at stop points (after each user step command):

```csharp
public sealed record StopPoint<TValue, TMem>(
    MachineState<TValue, TMem> State,
    DebugLocation Location,
    StepCommand CommandThatProducedThis,
    StepDiff Diff,
    BranchNodeId BranchNode
);
```

Then:

* Undo = move session cursor to previous StopPoint.
* Redo = move forward if you haven’t branched.

### 8.2 Efficient Undo requires one of these

**Option A (recommended): persistent immutable memory overlay**
Design `TMem` as a persistent structure:

* writes create new versions with structural sharing
* history only stores pointers

This makes restoring a previously retained semantic snapshot cheap, but Undo as a product feature is not free.
History retention, branch-tree indexing, eviction, checkpoint policy, transcript storage, allocation identity, and
reversible external effects still require explicit budgets and measured implementations. The dump-backed base heap
is immutable rather than versioned; only interpreter-owned overlays can participate in rewind.

**Option B: checkpoint + write log**

* at each stop, store:

  * snapshot of locals/stacks/IP
  * plus a list of memory writes performed during the command
* Undo applies the inverse writes
* More complicated because:

  * multiple writes to same location
  * havoc/unknown region updates need inverse semantics
  * allocation must be reversible (freeing objects)

This works, but it’s more bug-prone. In practice, persistent memory is cleaner.

### 8.3 Branching interacts with Undo

If you support “unknown branch: choose true/false”:

* history becomes a tree
* Undo should:

  * move up within the current branch
  * optionally allow switching to sibling branch at the decision node

You’ll want a `BranchTree` manager with persistent nodes:

```csharp
record BranchNode(
    BranchNodeId Id,
    StopPoint Base,
    List<BranchEdge> Children,
    BranchNodeId? Parent
);

record BranchEdge(string Label, BranchNodeId Child);
```

---

## 9) What else you need besides the interpreter

This feature is not “just interpretation”. You need a set of supporting components.

### 9.1 Program model & artifact resolution (the bridge layer)

You already sketched this; stepping needs it intensely:

* `MethodBodyResolver`: dump IL vs PE IL fallback
* `MetadataResolver`: token resolution + signatures + generics
* `SymbolResolver`: PDB sequence points + locals
* `SourceResolver`: find source text or use SourceLink; fallback to decompiler

### 9.2 Expression → entrypoint compiler (for “start from expression”)

You need a “root method” to interpret.

Two viable approaches:

**Approach 1: compile expression to a synthetic method (Roslyn)**

* Emit in-memory PE + portable PDB
* Load that PE into your metadata universe
* The interpreter starts at `SyntheticModule.Eval(closure)` and can step into real methods it calls
* Pros: very natural stepping; you get PDB for the expression itself
* Cons: more moving parts; must bind to correct assemblies from the dump

**Approach 2: don’t compile; start at target method entry**

* Restrict “Debug (Virtual)” to:

  * method calls and property getters where arguments are already values
* Pros: simpler
* Cons: you lose stepping “inside the expression” unless you build a separate expression interpreter

For the feature you described, I’d do approach 1 for completeness, and keep approach 2 as an MVP fallback.

### 9.3 Frame seeding from dump context (optional but important)

If the user starts in a dump frame (“debug this expression in frame X”), you need:

* a `FrameValueProvider` that can supply:

  * `this`
  * arguments
  * locals (when recoverable)
* missing locals become “unknown with origin MissingData”
* locals names/scopes from PDB if available

### 9.4 Heap bridge: dump-backed reads + overlay writes

This is a must-have component:

* `DumpHeapArena`: reads via ClrMD (objects at real addresses)
* `VirtualHeapArena`: allocates new virtual object IDs
* `OverlayStore`: persistent map of mutations keyed by:

  * `(objectRef, fieldId)` for fields
  * `(arrayRef, index)` for array elements
  * plus byref targets

Reads check overlay first; fallback to dump reads.

### 9.5 Debug presentation layer

Stepping isn’t useful if values look like raw addresses.

You need:

* value formatter (string, numbers, enums, nullable, tuples)
* object inspector with expansion
* provenance display (dump vs virtual, modeled vs interpreted, unknown reason)

This can be built later, but the core APIs should carry provenance hooks from day one.

---

## 10) Execution policies needed for a stable stepping UX

### 10.1 Branch decision policy

Your interpreter must be able to stop on unknown branches instead of forking silently.

Add:

```csharp
public interface IBranchPolicy<TValue>
{
    BranchDecision Decide(TValue condition, BranchContext ctx);
}

public abstract record BranchDecision
{
    public sealed record TakeTrue : BranchDecision;
    public sealed record TakeFalse : BranchDecision;
    public sealed record ForkBoth : BranchDecision;        // yields two states
    public sealed record StopForUserChoice(BranchInfo info) : BranchDecision;
    public sealed record JoinBoth : BranchDecision;        // execute both then join (analysis mode)
}
```

For stepping:

* default: `StopForUserChoice` (best UX)
* optional “fast mode”: `ForkBoth` into two sessions or `JoinBoth`

### 10.2 Call policy (interpret vs model vs block)

Calls should not cause “hard stop” unless configured.

Add:

```csharp
public interface ICallPolicy
{
    CallHandling Decide(MethodInstance target, CallContext ctx);
}

public enum CallHandling
{
    Interpret,
    Model,
    Stop // rare: user wants to stop at disallowed call
}
```

For stepping, “Stop” is useful as a debug setting (“break on IO”).

### 10.3 Budget manager

Stepping should never lock up.

Budget dimensions:

* max instructions per command
* max call depth
* max allocations / virtual heap size
* max branch forks

Budget exceed should produce machine `BudgetExhausted`; the controller maps it to `SessionPauseReason.BudgetStop` and a resumable session.

---

## 11) Exceptions and EH: what you need for stepping to feel real

You don’t need a full CLR exception model, but you do need **IL EH semantics** to avoid nonsense:

* `throw`, `rethrow`
* `leave`, `endfinally`
* handler selection based on IL ranges

You can stage this:

### Phase 1 (usable):

* If an exception occurs:

  * stop immediately (show throw site + exception object/unknown)
  * don’t attempt to run handlers
* Great for debugging “why did this throw?” in the interpreted code

### Phase 2 (debugger-grade):

* Implement EH region stack:

  * push when entering try regions
  * on throw, find nearest handler covering current offset
  * transfer control to handler start
* Step Out then naturally unwinds through handlers

This is “interpreter work”, not just UI, so plan it into the framework.

---

## 12) Optional but high-payoff refinement: StepDiff (what changed)

Users love “what changed after this step?” and it also supports Undo UX.

You can capture diffs cheaply if the interpreter reports writes:

* locals updates
* stack push/pop
* overlay writes (field/element store)
* unknown minted
* effects emitted

Represent as:

```csharp
public sealed record StepDiff(
    ImmutableArray<LocalChange> LocalChanges,
    ImmutableArray<MemoryWrite> MemoryWrites,
    ImmutableArray<EffectEvent> Effects,
    ImmutableArray<UnknownEvent> Unknowns
);
```

In a persistent memory model, memory writes are already known at write time; just record them.

---

## 13) Putting it together: the Virtual Debugger Engine

### 13.1 Session object

```csharp
public sealed class VirtualDebugSession
{
    public MachineState State { get; private set; }
    public HistoryCursor History { get; }
    public SessionOptions Options { get; }
    public IProgramModel Program { get; }              // ClrMD + metadata + symbols + source
    public IIlMachine Machine { get; }                 // interpreter
    public IDebugMapProvider DebugMaps { get; }
}
```

### 13.2 Step command execution

Pseudo:

```csharp
public StepOutcome Step(StepCommand cmd)
{
    var before = State;
    var plan = StepPlan.Create(cmd, before, DebugMaps);

    var outcome = MachineRunner.RunUntil(before,
        stopPredicate: (s, ev) => plan.ShouldStop(s, ev, DebugMaps),
        options: plan.ExecutionOptions);

    if (outcome.Reason == SessionPauseReason.StepComplete || outcome.Reason == SessionPauseReason.Completed)
        History.Push(before, outcome.State, cmd, outcome.Diff);

    State = outcome.State;
    return outcome;
}
```

Undo:

```csharp
public void Undo()
{
    var prev = History.Pop();
    State = prev.State;
}
```

Branching:

* DecisionNeeded yields a `Fork` or a `Stop` with branch candidates.
* UI chooses, session switches `State` to chosen branch and pushes a branch node in history.

---

## 14) Summary of required interpreter framework refinements

To make this feature clean (and not a pile of special-cases), extend the W3 interpreter framework with:

1. **Multi-frame call/return semantics** over the existing explicit `MachineState` + `FrameState` container
2. **Micro-step execution** with call/return as visible events
3. **SessionPauseReason + extended DebugEvents** for a stepping controller, distinct from low-level machine status
4. **Session history/retention policy** over the existing persistent semantic memory to enable Undo
5. **Branch policy hook** that can stop for user decision
6. **EH model** at least at “stop on throw”; ideally real handler transfer

Everything else (unknown propagation, taint/effects, call modeling, dump-backed heap) plugs into this naturally.
---

## Appendix A) Current prototype contract alignment (`src/`)

`src/` is no longer scaffolding-only. `Interpreter.Core.Execution` contains draft semantic/operational state,
`MachineActivationResult`, `StepOutcome`, `MachineRunStatus`, deterministic budgets/events, frozen typed whole-body
admission, and `IlMachine.ActivateRoot`/`StepOne`. Activation derives receiver, parameter, local, and return shapes
from one atomically resolved method definition; callers no longer seed counts, local values, or return disposition.

The admitted E1 subset is static, branchless, EH-free `Int32` constants/arguments/initialized locals plus `add`,
`sub`, `mul`, and `ret`. E2 admits only a direct or one-constant-adjusted instance `Int32` getter with exactly one
same-module FieldDef `ldfld`. The injected `IMemoryModel` returns exact/non-exact/target-exception outcomes; imported
objects do not fabricate defaults for absent fields. Exact typed null creates a budgeted/evented, idempotent terminal
`TargetException` state. Compiler/CoreCLR and generated real-dump tests cover direct/adjusted getters and fresh-session
replay. These implementation facts pass locally at hardened checkpoint `19c292f9f`, whose four jobs also pass in [implementation-
checkpoint run 29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally
closed at exact documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit.

This gives later stepping work a real frame stack, persistent semantic memory, truthful low-level events/statuses, and
terminal target-exception boundary. It does **not** provide command history, undo, stop predicates, source maps,
branches, calls/frame pushes, handler transfer, or a resumable exception stop. The candidate `SessionPauseReason`
below is deliberately not a current code contract.

The actual debugger control plane remains unimplemented; the speculative `Interpreter.Debugger.Engine` project was removed with the empty scaffolding. There is no Step Into/Over/Out, stop plan, history, branch-decision, or source-map orchestration. This research design must not be read as a current stepping contract.
