# Concrete IL Execution Contract

**Lifecycle:** Current contract

**Roadmap relation:** Active for W3

**Normative scope:** W3 milestone-selected closure

**Implementation status:** complete and formally closed at exact documentation commit `de6cea124` in [GitHub Actions run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237)

## 1) Purpose

This document defines the complete W3 execution boundary. W3 is an architecture-validation milestone, not a new
dump-query language or a general managed-code runtime. It proves that one domain-parametric IL kernel can:

1. execute the existing branchless concrete `Int32` arithmetic corpus from metadata-derived activation shapes; and
2. execute one dump-grounded instance-field getter through the real persistent-memory capability.

Every admitted body belongs to one closed, scenario-derived opcode and type profile. Anything outside that profile is
rejected before instruction zero. Documentation, a recognized opcode, or a partially executable prefix is not support.

The contract is deliberately narrower than the broader interpreter proposals. Later work may extend it, but must not
silently reinterpret W3 evidence as support for calls, branches, handlers, arbitrary signatures, inheritance dispatch,
or counterfactual product evaluation.

## 2) Relationship to W1 and W2

W1 and W2 remain complete for their explicitly milestone-selected scopes. W3 consumes their typed dump evidence but
does not change the restricted query grammar, typed root binding, immutable query plans, or result classification.

The W3 field-getter proof is **dump-grounded prepared execution**:

1. the host selects one exact snapshot, runtime module, rooted object, method body, and instance-field descriptor;
2. method metadata, method-body bytes, receiver identity, field identity, and field bytes are validated before
   activation;
3. only exact field evidence is imported into an immutable concrete-memory snapshot; and
4. the IL machine executes the getter over that prepared snapshot through `IMemoryModel`.

The machine never owns or calls a live `ClrmdDumpSession`. A disposable host reader is not semantic state. Partial,
unavailable, conflicting, or invalid dump evidence never becomes a concrete default, lattice top, or guessed value.

This preparation boundary is the fresh-session replay point: reopening the dump must rediscover and revalidate the
same snapshot-scoped inputs before an equivalent persistent-memory snapshot can be rebuilt.

## 3) Closed W3 profiles

### 3.1 E1: concrete arithmetic

E1 admits static, branchless, EH-free methods over exact `Int32` parameters and initialized `Int32` locals. The closed
instruction families are:

- `nop`;
- `ldc.i4.*`, `ldc.i4.s`, and `ldc.i4`;
- `ldarg.*` for metadata-projected `Int32` parameters;
- `ldloc.*` and `stloc.*` for metadata-projected initialized `Int32` locals;
- unchecked `add`, `sub`, and `mul` over the CLI I4 stack category; and
- `ret` for `void` and `Int32` methods.

The existing compiler/CoreCLR corpus remains W3 evidence after it stops supplying interpreter argument counts, local
counts, or return disposition through reflection.

### 3.2 E2: dump-grounded field getter

E2 admits one exact instance-getter family derived from checked-in compiler output:

```text
ldarg.0
ldfld <same-module instance Int32 FieldDef>
ret
```

The sole adjusted form appends one exact `ldc.i4.*`, `ldc.i4.s`, or `ldc.i4` constant and then exactly one unchecked
`add`, `sub`, or `mul` before `ret`. This proves that memory results re-enter the E1 value-domain handlers rather than
a getter-specific shortcut; it does not admit a longer E1 instruction sequence inside an instance method.

The admitted E2 field must be:

- a non-nil same-module `FieldDef`, not a `MemberRef` or `TypeSpec`-dependent reference;
- an instance field, not static, literal, or RVA-backed;
- declared directly on the exact metadata-projected receiver type;
- exact `System.Int32`; and
- correlated with the exact snapshot, object, and ClrMD field descriptor used during preparation.

No inheritance conversion, interface conversion, generic substitution, boxed receiver, value-type receiver, or
managed-reference receiver is implied.

## 4) Explicit non-goals

W3 does not admit:

- branches, switches, calls, virtual dispatch, object creation opcodes, or array opcodes;
- exception regions, handler search, filters, unwind, `leave`, `finally`, or `fault`;
- generic methods or types, varargs, explicit-this signatures, function pointers, custom modifiers, or sentinels;
- byrefs, pointers, typed references, pinned locals, value-type receivers, or address-taking instructions;
- floating point, native integers, 64-bit arithmetic, checked overflow opcodes, conversions, or comparisons;
- static fields, `MemberRef` field operands, cross-module field resolution, or inherited-field receiver conversion;
- continuation after a target exception;
- unknown-aware or abstract continuation after missing memory evidence; or
- any user-facing claim of historical execution.

The W3 concrete result is an architecture proof. If a later product exposes method evaluation, it is classified as
counterfactual execution with explicit assumptions, never as an observation that the target historically ran it.

## 5) Structural execution identities

### 5.1 Types

Execution type identity is structural. A display name alone is never sufficient for a metadata-defined type.

The W3 type vocabulary contains:

- `Void`;
- exact CLI `Int32`; and
- an exact object-reference type identified by content-derived module identity plus a non-nil `TypeDef` token.

Display names are diagnostic evidence only. Two named types from different modules do not compare equal merely because
their namespace-qualified names match. Prototype-only named types may retain a clearly marked synthetic identity for
isolated memory-law tests, but they are not admissible metadata identities.

### 5.2 Methods

A method handle contains a content-derived module handle and a validated non-nil `MethodDef` token. A resolved method
definition atomically contains:

- that method handle;
- the exact immutable method body;
- calling-convention facts required by W3;
- the declaring type;
- whether the signature has an implicit receiver;
- ordered explicit parameter types;
- the return type, including explicit `Void`; and
- ordered local types decoded from the body's `StandAloneSig`, or an empty vector when no local signature exists.

The body and shape are one resolution snapshot. The machine must not combine a body from one resolver observation with
a signature from a later observation.

### 5.3 Fields

A field handle contains the defining module handle and a validated non-nil `FieldDef` token. A resolved instance-field
descriptor additionally freezes:

- the exact declaring `TypeDef` identity;
- the exact field type; and
- static, literal, and RVA facts needed by admission.

The raw four-byte `InlineField` operand is never passed directly to the memory model. Whole-body admission resolves it
once and freezes the typed descriptor in the admitted instruction plan.

## 6) Resolution contract

The VM-facing resolver supplies two structured operations:

1. resolve a complete method definition by `MethodHandle`; and
2. resolve an `InlineField` token in that method's module and generic context.

Each operation returns either an immutable value or a typed `Unavailable`, `Unsupported`, `Invalid`, or `Conflict`
failure with a stable code. Resolver exception text and target-derived strings do not become machine diagnostics.

The machine snapshots the first result of each method and field resolution for one execution session. Admission and
execution use only those snapshots. A mutable resolver cannot change a body, frame shape, or field binding halfway
through a run.

### 6.1 Disk-backed differential path

For dump-free compiler differential tests, `System.Reflection.Metadata` projects the body, method signature, local
signature, declaring type, and field definition from one content-identified PE. Reflection invokes the same fixture on
CoreCLR only as the outcome oracle; it does not seed interpreter counts, local types, or return disposition.

### 6.2 Dump-grounded path

For E2 dump evidence, the executable method body and signature/field projection come from the same exact counted dump
metadata and method-memory evidence. A separately acquired PE may be content-identity-validated and compared as an
independent oracle, but its bytes do not replace missing dump execution evidence.

A snapshot-scoped module execution handle must incorporate the validated metadata identity and stable runtime-module
source identity. Reopening the same dump and rediscovering the same runtime module reproduces the handle; a different
snapshot or loader instance cannot alias it merely because target addresses or names repeat.

## 7) Activation contract

An activation request supplies only:

- the method handle;
- ordered receiver/argument domain values; and
- an initial persistent-memory snapshot.

It does not supply argument count, local count, local values, or a `ReturnsValue` flag.

Before creating a root frame, the machine:

1. resolves and admits the complete method;
2. derives the exact frame argument vector, including `this` at slot zero for an instance method;
3. validates each supplied value against the exact projected type and stack category;
4. rejects lattice bottom and malformed/default immutable arrays;
5. derives the return behavior from the resolved return type;
6. initializes admitted locals through the value-domain default-value operation when `initlocals` is set; and
7. creates exactly one root frame at IL offset zero with an empty evaluation stack.

W3 rejects methods with locals when the body does not request initialized locals. Definite-assignment analysis for
uninitialized CLI locals is outside the slice.

Activation failure consumes no instruction budget, emits no debug event, calls no memory operation, and exposes no
partially initialized machine state.

## 8) Whole-body typed admission

Admission runs before the first transfer and decodes every instruction exactly once. It produces an immutable plan
containing normalized instruction kind, operand, size, offset, expected entry-stack types, and any resolved field
descriptor.

Admission validates:

- bounded body length, instruction count, frame slots, and declared `maxstack`;
- valid instruction and operand boundaries;
- the absence of exception regions;
- exact agreement between the body local-signature token and decoded local vector;
- the closed opcode set;
- argument and local slot bounds from metadata-derived vectors;
- exact typed stack pop/push behavior at every boundary;
- `ret` agreement with the metadata-derived return type;
- no instruction after the terminal `ret`; and
- every E2 field identity, owner, type, and storage disposition rule.

A supported prefix followed by any unsupported, unresolved, malformed, or type-incompatible suffix rejects the entire
body. No prefix executes. Admission does not consume instruction budget or call the memory model.

Resumed states are valid only at a frozen instruction boundary whose complete evaluation-stack type vector matches the
frame. Depth-only agreement is insufficient.

## 9) Value-domain requirements

The value domain supplies deterministic default construction in addition to the existing lattice, type, stack-kind,
constant, and arithmetic operations.

For the concrete W3 domain:

- `DefaultValue(Int32)` is exact zero;
- an exact object reference carries the exact structural receiver type;
- null is a typed reference value, distinct from lattice bottom and unknown;
- arithmetic preserves unchecked CLI I4 behavior; and
- semantic type equality does not depend on display strings alone.

At activation and every resumed boundary, the machine validates both the domain-reported static type and stack kind.
A Boolean or arbitrary I4-category value is not admitted where the metadata signature requires exact `Int32` merely
because both use the I4 evaluation-stack category.

## 10) Persistent-memory and import contract

`IlMachine<TValue,TMemory>` receives an explicit `IMemoryModel<TValue,TMemory>`. `ldfld` must call that capability; it
must not inspect a concrete memory implementation or bypass the domain.

Field loads accept the frozen resolved-field descriptor and return a typed memory-load result. The result distinguishes:

- `Exact`, which alone carries a value;
- `Partial`;
- `Unavailable`;
- `Conflict`;
- `Invalid`; and
- `TargetException` with structured exception information.

An allocated concrete object may use CLI zero/default field initialization. A dump-imported object is different:
fields not explicitly populated from exact evidence are unavailable, never silently zero or top. The concrete memory
model therefore preserves whether an object was freshly allocated or imported from external evidence.

Dump import requires:

- the exact snapshot and uniquely selected rooted-object evidence established by the earlier dump-evidence path; W3
  reuses those evidence facts directly and does not depend on a W2 product-query plan;
- an exact owner/type match between the ClrMD descriptor and metadata field descriptor;
- equal non-nil `FieldDef` tokens;
- an exact four-byte `Int32` observation; and
- a stable external-object evidence identity retained by the imported snapshot.

Any non-exact preparation result stops before activation and retains its existing dump evidence/provenance outside the
machine. No fallback memory cell is created.

Successful `ldfld` is read-only: it pushes exactly one value and threads an equivalent persistent-memory snapshot.
Sibling/fork isolation, deterministic identity, equality, and stable hashing remain executable memory laws.

## 11) Outcomes, budgets, and events

### 11.1 Successful instruction

A successful instruction:

- consumes exactly one instruction-budget unit;
- performs its complete semantic transfer;
- emits one `InstructionExecuted` event at the original method and IL offset; and
- returns `Ready` or, for root `ret`, `Completed` plus `FramePopped`.

A successful `ldfld` calls `LoadField` exactly once, verifies that the exact result has the projected `Int32` type and
I4 stack kind, preserves memory, and then emits its execution event.

### 11.2 Admission or evidence inability

Unsupported or malformed IL, resolution inability, and non-exact memory evidence execute no instruction. They leave
semantic state, persistent memory, operational budget, and events unchanged. Stable status and diagnostic fields name
the method, IL offset, and failure category without claiming a transfer occurred.

An ordinary exception thrown by a resolver, value domain, or memory plug-in is normalized to a payload-omitting
capability failure; catastrophic `OutOfMemoryException` and `StackOverflowException` are deliberately not caught.
Host exception text is not copied into a result.

### 11.3 Target null reference

Applying admitted `ldfld` to an exact typed null receiver terminates with:

- `MachineRunStatus.TargetException`;
- structured `TargetExceptionInfo` identifying `NullReference`, method, and IL offset;
- one consumed instruction-budget unit;
- no resumable successor state and no field value;
- one `TargetExceptionRaised` event; and
- no `InstructionExecuted` event, because no ordinary semantic transfer completed.

The successor is a latched terminal state with an empty call stack and the structured target exception retained on the
state. Stepping it again is idempotent: it returns the same state, operational budget, status, and exception with no
memory call or event. No handler transfer is attempted. This one explicit exceptional boundary is part of E2
differential evidence; broader stop-on-throw behavior and handler semantics remain later work.

Budget exhaustion before an instruction calls no domain or memory transfer and emits no event.

## 12) Determinism and replay

Repeated execution with equal resolved evidence, arguments, memory, and budget must reproduce:

- the same content/snapshot-derived module, method, type, and field identities;
- the same metadata-derived activation shape;
- the same admitted instruction plan and typed boundaries;
- the same ordered memory calls;
- semantically equal states and persistent memory;
- the same remaining budget and terminal status;
- the same ordered event sequence; and
- a byte-identical canonical transcript and SHA-256 fingerprint.

The replay corpus runs both in one resolver/machine session and after constructing a fresh metadata source, resolver,
machine, and equivalent memory snapshot. Dump-grounded replay additionally reopens the dump and rebinds the module,
root, method, and field before importing evidence.

Operational budget and event history remain outside semantic-state equality.

## 13) Required executable evidence

W3 closure requires all of the following, headlessly and with the milestone test selection:

1. structural type, method, and field identity tests, including cross-module non-aliasing;
2. SRM projection tests for static/instance arguments, `void`/`Int32` returns, initialized locals, and FieldDefs;
3. structured rejection tests for unsupported signatures, local shapes, field tokens, fields, EH, and opcodes;
4. activation tests proving caller counts, locals, and return disposition are no longer inputs;
5. typed whole-body admission tests proving supported prefixes never execute after a rejected suffix;
6. concrete-domain and persistent-memory laws, including allocated defaults and imported-field absence;
7. direct and adjusted getter tests proving exactly one real memory-model load and unchanged memory;
8. CoreCLR differential agreement for arithmetic, overflow wrapping, getters, and null-receiver outcome;
9. repeated and fresh-session canonical replay equality;
10. a generated real-dump E2 test whose method metadata/body and field value come from counted dump evidence; and
11. the repository-wide Release build plus all required milestone-selected fast, ordinary-dump, and optimized-dump jobs.

Tests must assert not only final values but also resolver/memory call counts, budget deltas, event truthfulness, state
preservation on failure, and emitted compiler opcode shapes.

### 13.1 Implementation evidence checkpoint

The cumulative diff from normative-contract checkpoint `e7b6a4ace` through strengthened implementation checkpoint
`19c292f9f` realizes this contract in 8,842 hand-written additions and 1,650 deletions: 5,362 production
additions/928 deletions and 3,480 test/fixture additions/722 deletions. It also contains 39 generated package-lock
additions. Generated locks and documentation are excluded from the hand-written ledger; its commit-level raw-diff
reconciliation is recorded in [Future Work Planning](../../plans/future-work-planning.md).

Local verification used only the repository's headless process wrapper and passed:

- locked restore and the fifteen-project Release build with zero warnings and errors;
- Markdown-link and managed-workflow headless guards;
- 103/103 milestone-selected semantic, admission, metadata, memory, and differential tests;
- 67/67 fast integration tests;
- 5/5 ordinary real-dump tests, including the counted W3 getter proof;
- 1/1 optimized modeled-context test; and
- the focused W3 dump lane at 2/2, all with zero skips.

These results satisfy the implementation and local portions of the gate. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at
the exact implementation commit and independently corroborates the code checkpoint. Exact documentation-closure commit
`de6cea124` subsequently passed all four required jobs in [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237), satisfying the remaining gate.

## 14) Completion and expansion rule

W3 required the exact pushed documentation-closure commit to pass every required hosted job and the realized
hand-written implementation LOC to be recorded in the roadmap. Exact commit `de6cea124` and [run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) satisfy that rule. Local success,
a compiler-only getter, a standalone memory test, or a dump read that bypasses `IlMachine` remains insufficient for any
later expansion claim.

After W3:

- adding a second meaningful value domain is a separate gate before any shared multi-mode-engine claim;
- adding a new opcode requires a compiled scenario and its complete dependency closure;
- exposing counterfactual product method evaluation requires a separate product contract and result semantics;
- broad target-exception stopping remains distinct from handler transfer; and
- branches, calls, byrefs, statics, generics, and EH each retain their documented later gates.

No future milestone may weaken W3's whole-body admission, structural identity, exact-evidence import, deterministic
budget/event, or no-fabrication rules merely to admit more methods.
