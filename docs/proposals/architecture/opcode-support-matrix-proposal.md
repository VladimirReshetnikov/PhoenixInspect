# Opcode Admission and Evidence Matrix

**Lifecycle:** Current supporting plan
**Roadmap relation:** Active only for W3 and later

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

## 3. Executable slices

### E0 — Walking-skeleton return

Purpose: prove method-body acquisition, deterministic budget accounting, and root-frame completion.

| Instruction | Admitted shape | Status |
|---|---|---|
| `ret` | `void` root method with empty evaluation stack | `Exact` |

This was the original prototype slice. Its disk-PE method body is artifact evidence, not dump-memory IL evidence.

### E1 — Concrete arithmetic

Purpose: exercise the domain-parametric engine with real values and establish the CoreCLR differential oracle.

The exact set is generated from checked-in compiled fixtures. Expected initial families are:

- `ldc.i4.*`, `ldc.i4.s`, `ldc.i4`;
- `ldarg.*` for admitted static primitive parameters;
- `ldloc.*` and `stloc.*` for admitted primitive locals;
- `add`, `sub`, and `mul` for `int32` shapes;
- `ret` for `int32` and `void` roots.

Required dependencies:

- evaluation-stack validation and deterministic instruction decoding;
- arguments, locals, instruction offset, root return value, and immutable/persistent memory in machine state;
- concrete-domain extraction and arithmetic semantics;
- no exception regions, calls, branches, byrefs, floating point, overflow prefixes, or implicit exceptional cases in admitted fixtures.

Exit evidence:

1. edge-focused transfer tests;
2. domain law tests, including `IsLessThanOrEqual`, `Join`, `Meet`, and `Widen` coverage laws;
3. repeated-run transcript equality;
4. compiled fixture results equal CoreCLR results over an input corpus.

### E2 — Dump-backed field getter

Purpose: connect interpreter execution to product evidence without broadening to arbitrary methods.

Expected candidate instructions are `ldarg.0`, `ldfld`, and `ret`, plus only the signature/token decoding forced by fixtures. Entry is gated on W1/W2 providing typed dump object/field evidence and identity mapping.

Admission requirements:

- branchless, call-free, EH-free instance getter;
- exact receiver identity and field-token resolution;
- dump-backed field read returns an explicit evidence outcome;
- missing/sparse data produces partial/unavailable evidence, never a fabricated concrete value;
- target exceptions stop the path; handler transfer is unavailable.

The final set is the union of instructions emitted by the checked-in getter fixtures, not this expected list.

## 4. Later gates

- **Branches:** require explicit condition semantics, deterministic path policy, and closed fixtures.
- **Calls:** require a scenario-narrowed call/effect contract and admitted callee policy.
- **Indirect/byref operations:** require an addressable model and dump-layout evidence. Span is not an MVP commitment.
- **Exception regions:** first add stop-on-throw; handler search/unwind is a separate milestone required before `leave`, `endfinally`, filters, or debugger-grade Step Out claims.
- **Async/dynamic:** require their ordinary prerequisite opcode and EH sets before semantic lifting can be evaluated.

## 5. Unsupported behavior

For a body already admitted to a slice, an unexpected or malformed instruction produces:

- machine status `Blocked` or `InvalidProgram` as appropriate;
- stable diagnostic code, method identity, opcode bytes, and IL offset;
- no `InstructionExecuted` event;
- no evaluation-stack, local, or memory mutation;
- no instruction-budget consumption for an instruction that did not execute.

The engine does not inject an unknown for an unsupported instruction unless a later hybrid slice defines and proves that continuation is sound for that exact stack/effect shape.

## 6. Evidence table maintenance

As each slice lands, maintain a small generated or hand-checked table containing:

- opcode and admitted operand/type shapes;
- first slice;
- unit test IDs;
- differential fixture IDs;
- limitations and exceptional behavior.

Do not create a full ~200-opcode tracking bureaucracy before implemented coverage makes it useful.
