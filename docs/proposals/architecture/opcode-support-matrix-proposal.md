# Opcode Admission and Evidence Matrix

**Lifecycle:** Current
**Roadmap relation:** Supporting; records W3 evidence and gates later expansion

## 1. Principle

Opcode support is admitted by compiled product scenarios, not by percentage coverage or perceived opcode popularity. Each executable slice is a **dependency-closed set**: every instruction, prefix, stack shape, metadata operation, memory operation, and exceptional behavior used by its fixtures is either implemented and tested or the fixture is rejected before execution.

An opcode is not “supported” because it appears in a proposal. The companion evidence is executable tests.

## 2. Status vocabulary

| Status | Meaning |
|---|---|
| `Exact` | Transfer semantics are implemented for the admitted operand/stack/type shapes and checked by unit plus differential tests. |
| `Conservative` | The admitted shape intentionally loses precision and emits a provenance-bearing diagnostic. This is not used by the first concrete slice. |
| `RecognizedBlocked` | Decode is deterministic, no semantic effects occur, and execution stops with an opcode/offset/reason diagnostic. |
| `Unadmitted` | No execution claim. A body containing the instruction is rejected by the slice admission check. |

Promotion requires an evidence link, not a documentation edit alone.

The E1/E2 implementation below is present at hardened checkpoint `19c292f9f` and passes all required local non-cybersecurity
verification plus all four jobs in [implementation-checkpoint run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). The normative boundary is
[Concrete IL Execution Contract](concrete-il-execution-contract-proposal.md). Exact hosted documentation closure
remains pending.

## 3. Executable slices

### E0 — Walking-skeleton return

Purpose: prove method-body acquisition, deterministic budget accounting, and root-frame completion.

| Instruction | Admitted shape | Status |
|---|---|---|
| `ret` | `void` root method with empty evaluation stack | `Exact` |

This was the original body-only prototype slice. W1 later executed a normalized `RetOnly` body acquired from counted
dump memory, and W3 migrated that path to atomic metadata-derived activation. The independently opened disk PE remains
artifact-oracle evidence, never an input to dump execution.

### E1 — Concrete arithmetic

Purpose: exercise the domain-parametric engine with real values and establish the CoreCLR differential oracle.

The exact implemented set is generated from checked-in compiled fixtures:

| Instruction family | Admitted E1 shape | Status |
|---|---|---|
| `nop` | Static W3 method; no stack effect | `Exact` |
| `ldc.i4.m1` … `ldc.i4.8`, `ldc.i4.s`, `ldc.i4` | Exact CLI I4 constant | `Exact` |
| `ldarg.0` … `ldarg.3`, `ldarg.s`, `ldarg` | Metadata-projected exact `Int32` parameter slot | `Exact` |
| `ldloc.0` … `ldloc.3`, `ldloc.s`, `ldloc` | Metadata-projected initialized exact `Int32` local | `Exact` |
| `stloc.0` … `stloc.3`, `stloc.s`, `stloc` | Store exact I4 into metadata-projected exact `Int32` local | `Exact` |
| `add`, `sub`, `mul` | Two exact `Int32`/I4 operands, unchecked CLI wraparound | `Exact` |
| `ret` | Empty stack for metadata `void`; one exact `Int32`/I4 for metadata `Int32` | `Exact` |

Required dependencies:

- evaluation-stack validation and deterministic instruction decoding;
- structural module/MethodDef/type identity and atomic metadata-derived receiver/parameter/return/local projection;
- metadata-derived activation with initialized local defaults; caller-provided counts, local values, and return flags
  are not inputs;
- arguments, locals, instruction offset, root return value, and immutable/persistent memory in machine state;
- typed whole-body admission and exact entry-stack vectors frozen before instruction zero;
- concrete-domain extraction and arithmetic semantics;
- no exception regions, calls, branches, byrefs, floating point, overflow prefixes, or implicit exceptional cases in admitted fixtures.

Exit evidence:

1. edge-focused transfer tests;
2. domain law tests, including `IsLessThanOrEqual`, `Join`, `Meet`, and `Widen` coverage laws;
3. repeated-run transcript equality;
4. compiled fixture results equal CoreCLR results over an input corpus.

Implemented evidence additionally covers compact/short/long slot encodings, fifth-argument access, void return,
unchecked overflow, invalid type/category boundaries, frozen resolver observations, and same/fresh-SRM-session canonical
replay. Reflection supplies only the CoreCLR result oracle; it does not seed activation shape.

### E2 — Dump-backed field getter

Purpose: connect interpreter execution to exact prepared dump evidence without broadening the W2 query grammar or
admitting arbitrary methods.

| Instruction family | Admitted E2 shape | Status |
|---|---|---|
| `ldarg.0` | Metadata-derived implicit receiver of the exact structural declaring TypeDef | `Exact` |
| `ldfld <FieldDef>` | Exactly one same-module instance, non-literal, non-RVA exact `Int32` field declared directly by that receiver type | `Exact` |
| `ldc.i4.*`, `ldc.i4.s`, `ldc.i4` | Optional one exact adjustment constant | `Exact` |
| `add`, `sub`, `mul` | Optional one exact unchecked adjustment after the field load | `Exact` |
| `ret` | One exact `Int32`/I4 return | `Exact` |

The only admitted instruction sequences are the direct getter
`ldarg.0; ldfld; ret` and the constant-adjusted getter
`ldarg.0; ldfld; ldc.i4.*; add|sub|mul; ret`. Additional instructions, locals, explicit parameters, multiple field
loads, decorated getters, and arbitrary instance methods reject the whole body.

Admission requirements:

- branchless, call-free, EH-free instance getter;
- exact structural receiver identity and contextual same-module FieldDef resolution frozen during admission;
- snapshot-scoped dump method/body/signature projection from counted metadata and physical method-memory bytes;
- proof that the admitted `ldfld` token is exactly the correlated runtime field;
- exact rooted-object owner/type/token correlation before a four-byte `Int32` cell is imported;
- dump-backed field load returns exact, partial, unavailable, conflict, invalid, or structured target-exception outcome;
- missing/sparse data produces partial/unavailable evidence, never a fabricated concrete value;
- successful `ldfld` calls `IMemoryModel.LoadField` exactly once and preserves persistent memory;
- exact typed null consumes one instruction unit, emits one `TargetExceptionRaised` event, and latches an idempotent
  terminal `NullReference` outcome; handler transfer is unavailable.

The executable evidence includes direct and adjusted compiler/CoreCLR differential getters, non-exact-memory and
negative admission cases, imported-missing-field behavior, same-machine and fresh-resolver replay, and a generated
real dump that is closed, reopened, rediscovered, rebound, reimported, and replayed. The disk PE is a late comparison
oracle and contributes no execution input to the dump resolver.

## 4. Later gates

- **Branches:** require explicit condition semantics, deterministic path policy, and closed fixtures.
- **Calls:** require a scenario-narrowed call/effect contract and admitted callee policy.
- **Indirect/byref operations:** require an addressable model and dump-layout evidence. Span is not an MVP commitment.
- **Exception regions:** first add stop-on-throw; handler search/unwind is a separate milestone required before `leave`, `endfinally`, filters, or debugger-grade Step Out claims.
- **Async/dynamic:** require their ordinary prerequisite opcode and EH sets before semantic lifting can be evaluated.

## 5. Unsupported behavior

Whole-body admission prevents an unexpected or malformed suffix from becoming a partially executed body. It produces:

- machine status `Blocked` or `InvalidProgram` as appropriate;
- stable diagnostic code, method identity, opcode bytes, and IL offset;
- no `InstructionExecuted` event;
- no evaluation-stack, local, or memory mutation;
- no instruction-budget consumption for an instruction that did not execute.

Likewise, resolution failure and non-exact memory evidence preserve state, memory, budget, and events. Ordinary
non-catastrophic exceptions from resolver/domain/memory capabilities are normalized into stable payload-safe failures;
out-of-memory and stack-overflow exceptions are not caught. The one different W3 boundary is admitted target-null
`ldfld`, whose consumed budget, target event, and terminal latch describe a target instruction that did execute
exceptionally.

The engine does not inject an unknown for an unsupported instruction unless a later hybrid slice defines and proves that continuation is sound for that exact stack/effect shape.

## 6. Evidence table maintenance

As each slice lands, maintain a small generated or hand-checked table containing:

- opcode and admitted operand/type shapes;
- first slice;
- unit test IDs;
- differential fixture IDs;
- limitations and exceptional behavior.

Do not create a full ~200-opcode tracking bureaucracy before implemented coverage makes it useful.
