# Opcode Support Matrix Proposal

## Status

Draft

## Scope

This proposal defines a planning matrix for IL opcode support in the conceptual-design phase, including:

- support tiers and readiness criteria,
- deterministic fallback behavior for unsupported opcodes,
- sequencing priorities across milestones,
- documentation and testing expectations tied to each opcode family.

The goal is to replace ad-hoc opcode discussions with a shared artifact that is easy to review and update as design assumptions evolve.

---

## 1) Why an opcode matrix is needed now

Current architecture proposals define interpreter contracts, state models, and testing strategy, but they do not yet provide a single answer to:

1. Which opcodes are in MVP scope?
2. Which opcodes are intentionally deferred?
3. How should unsupported behavior surface to hosts?

Without this matrix, planning conversations risk drift and inconsistent assumptions across product, architecture, and integration documents.

---

## 2) Design principles

1. **Determinism over breadth**: supporting fewer opcodes with predictable outcomes is better than broad but unstable behavior.
2. **Explainability by default**: every unsupported/approximated opcode must produce structured diagnostics and provenance.
3. **Family-level planning, opcode-level tracking**: sequence work by semantic families while still tracking per-opcode status.
4. **Safety-first fallback**: unsupported operations never silently execute best-effort concrete side effects.
5. **Incremental graduation**: opcodes move through explicit support tiers with test evidence.

---

## 3) Support tiers

Each opcode is assigned exactly one tier at any point in time.

### Tier A — Supported (Deterministic)

- Defined transfer/execution semantics in interpreter spec.
- Covered by unit + component tests.
- Participates in end-to-end scenario tests.
- Emits stable explainability artifacts.

### Tier B — Supported (Conservative Approximation)

- Opcode is executable but may widen precision.
- Approximation is explicit in diagnostics/provenance.
- Determinism and budget accounting requirements still apply.

### Tier C — Recognized but Blocked

- Opcode is recognized and decoded.
- Execution halts or call path is blocked according to policy.
- Host receives structured unsupported-opcode reason and location.

### Tier D — Not Yet Modeled

- No committed behavior beyond parse/decode safety.
- Treated as design backlog; not eligible for MVP claims.

---

## 4) Proposed MVP family matrix

The table below is a **planning baseline**, not a final commitment.

| Opcode family | Representative opcodes | Proposed MVP tier | Rationale |
|---|---|---|---|
| Constants and locals | `ldc.*`, `ldloc.*`, `stloc.*`, `ldarg.*`, `starg.*` | A | Required for almost all expression and stepping scenarios. |
| Stack manipulation | `dup`, `pop` | A | Core stack-machine mechanics; low semantic risk. |
| Arithmetic and comparisons | `add`, `sub`, `mul`, `div`, `rem`, `ceq`, `cgt`, `clt` | A/B | Mostly deterministic; overflow/NaN and numeric-width edges may begin in B. |
| Branching and control flow | `br*`, `switch`, `ret` | A | Required for path exploration and deterministic stepping. |
| Conversions | `conv.*`, `box`, `unbox.any` | B | Precision and exception-mode details need explicit conservative rules. |
| Object model basics | `newobj`, `ldfld`, `stfld`, `ldsfld`, `stsfld` | B/C | Needs policy and memory-model coupling for side effects and static state assumptions. |
| Calls (direct) | `call`, `callvirt`, `newobj` dispatch aspects | B | Call-model proposal defines effect lattice; initial behavior may block impure/unknown calls. |
| Arrays | `newarr`, `ldlen`, `ldelem.*`, `stelem.*` | B/C | Core for many methods, but bounds/type/runtime checks need phased precision. |
| Exceptions and EH flow | `throw`, `rethrow`, `leave`, handler transitions | C | Important but can be deferred from MVP if surfaced as explicit blocked behavior. |
| Indirect memory/pointers | `ldind.*`, `stind.*`, `cpblk`, `initblk`, `localloc` | C/D | Higher safety risk; likely post-MVP unless constrained subset is proven safe. |
| Concurrency and memory model hints | `volatile.`, `readonly.`, `constrained.` prefixes | C | Prefix semantics should be handled deliberately after baseline execution is stable. |
| Dynamic/async lowered machinery | call-site and state-machine related IL patterns | B/C | Prefer lifted semantics (`Dyn*`, virtual task ops) rather than literal opcode-only execution. |

---

## 5) Fallback contract for unsupported opcodes

When encountering Tier C/D opcodes in an active execution path, the engine should:

1. Emit a deterministic diagnostic envelope with:
   - opcode,
   - method and IL offset,
   - support tier,
   - policy branch taken (`blocked`, `unknown`, or `terminate-path`).
2. Emit provenance/event entries so hosts can explain *why* evaluation is partial.
3. Apply policy-defined continuation behavior:
   - terminate current path,
   - or inject unknown value (only when contractually safe),
   - never perform unmodeled concrete side effects.

This contract aligns unsupported-opcode behavior with the broader explainability model used for missing metadata or blocked calls.

---

## 6) Governance: where and how to track status

Maintain opcode status in two layers:

1. **This proposal**: family-level sequencing and principles.
2. **Companion matrix artifact** (future doc): opcode-by-opcode status with evidence links to tests/fixtures.

Recommended fields for the companion matrix:

- opcode name,
- family,
- current tier,
- first milestone introduced,
- known limitations,
- evidence links (unit/component/e2e fixture IDs),
- owner/reviewer.

---

## 7) Milestone alignment (initial draft)

### M1 — Core deterministic expression lane

Target families:

- constants/locals,
- stack manipulation,
- branching,
- baseline arithmetic/comparisons.

Exit signal:

- deterministic replay hash stability on core fixture corpus.

### M2 — Calls, objects, and arrays with conservative semantics

Target families:

- direct calls,
- object model basics,
- arrays,
- conversion hardening.

Exit signal:

- policy-governed blocked/unknown behavior validated in end-to-end scenarios.

### M3 — Advanced control and lowered-language features

Target families:

- exception flow,
- selected prefixes,
- lifted dynamic/async semantics integration.

Exit signal:

- stable diagnostics and step behavior on virtual debugger scenarios.

---

## 8) Testing implications

For any opcode family promoted to Tier A/B, require:

1. unit tests for transfer semantics and edge cases,
2. component tests for state transitions across merged control flow,
3. end-to-end fixtures asserting status labels and diagnostics,
4. determinism checks across repeated runs.

For Tier C/D, require at minimum:

- decode/recognition tests,
- fallback contract tests that verify stable blocked/unknown diagnostics.

---

## 9) Open questions

1. Should EH flow (`throw`/handlers) be mandatory for MVP, or acceptable as explicit blocked behavior?
2. Which pointer/indirect opcodes can be safely approximated without violating trust guarantees?
3. Should `constrained.` and `readonly.` be treated as first-class semantics early, or initially lowered into conservative call/model decisions?
4. What minimum opcode coverage threshold should gate preview readiness: percentage by opcode count, by scenario relevance, or both?

