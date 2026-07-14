# Active metadata backend decision record

## Decision status

- **Status:** Decided for the active prototype slices
- **Decision:** SRM/PEReader
- **Decision date:** 2026-07-13
- **Revisit scope:** prototype implementation choice, not a public object-model guarantee
- **Supersedes:** the 2026-02-17 provisional AsmResolver selection

## Decision

Use `System.Reflection.Metadata` plus `PEReader` as the metadata, PE, IL-body, and future Portable PDB foundation for the dump-evidence, restricted-expression, and first interpreted-method slices. W3 now exercises the metadata side of that decision through a reusable project-owned projection over both a content-identified disk PE and exact counted dump-metadata bytes. Portable PDB projection remains a later gate rather than an implemented W3 capability.

Keep project-owned identities and result/evidence contracts at the boundary. Do not leak SRM handles into the interpreter or host API. Do not implement a second backend without an executable corpus demonstrating a material SRM deficiency or cost.

## Why the decision changed

The earlier AsmResolver choice was based primarily on source scans and projected implementation velocity. Since then, the repository acquired a working SRM adapter, an end-to-end integration seam, and a reusable structural W3 projection for method shape, locals, bodies, declaring types, and FieldDefs. AsmResolver remained an empty project. The old decision therefore privileged anticipated convenience over repository evidence.

SRM is the lower-risk active choice because:

- it is already exercised by the prototype;
- its low-level behavior makes identity, partialness, and malformed-input policy explicit;
- Portable PDBs use the same metadata model;
- the expected decompiler fallback, ILSpy, is SRM-centric;
- removing the unused backend reduces dependency and package surface.

This does not assert that SRM is universally superior. It says the project will deepen one proven path before funding alternatives.

## Boundary rules

1. Artifact module identity is content-derived (MVID plus PE disambiguation where required); paths and discovery order are hints, not identity.
2. Runtime module instances and disk artifacts remain separate identities joined by explicit evidence.
3. Missing, sparse, conflicting, and malformed inputs produce typed evidence outcomes and stable diagnostics.
4. Method definitions use module identity plus metadata token. Generic instantiations, when admitted by a later
   milestone, must add deterministic structural context rather than allocation counters; W3 admits no generic method
   or type signatures.
5. Backend-specific objects do not cross projected contracts.
6. A method's body, signature, return shape, and locals are projected atomically, while field operands are resolved in
   the frozen method context before instruction zero. The interpreter receives only project-owned immutable records.

## Revisit triggers

Evaluate another backend only when at least one checked-in fixture demonstrates:

- an unsupported or disproportionately costly metadata/IL/PDB operation required by an active milestone;
- malformed-input behavior that cannot be made sufficiently bounded or diagnosable;
- a measured performance/memory problem relevant to an active acceptance criterion; or
- a Windows PDB requirement for which SRM is intentionally not the implementation.

An experiment must implement the same projected contract and run the same conformance fixtures. A source comparison or attractive API alone is not a trigger.

## Consequences

- `Interpreter.Metadata.AsmResolver` is removed rather than retained as an empty option.
- AsmResolver, dnlib, Cecil, and DIA notes under `docs/lib/` remain research references, not roadmap commitments.
- Package count and backend vocabulary shrink while the public boundary remains replaceable.
- Any future backend decision is based on executable differential evidence.
- Exact commit `19c292f9f` is the hardened checkpoint for the closed W3 SRM proof: structural identities, atomic
  method-shape projection, contextual FieldDef resolution, metadata-derived activation, compiler/CoreCLR plus
  real-dump getter fixtures, exact compact `ldarg.0` enforcement for E2, and positive coverage for every admitted
  arithmetic operator and `nop`. The primary checkpoint `12b6ef942` passed all four jobs in [GitHub Actions run
  29372661656](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29372661656). [Run
  29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at
  exact hardened checkpoint `19c292f9f`. Formal W3 closure remains pending on the later exact pushed
  documentation-closure commit.
- This evidence does not implement Portable PDBs, generic-context substitution, broader opcode/token families, a
  second value domain, or a product-facing method evaluator.

## Evidence status and remaining stabilization gates

Implemented evidence now includes:

- path-independent module/method identity, structural type/field identities, and explicit module/token mismatch
  outcomes;
- atomic body/signature/return/local projection for the closed static and instance `Int32` fixture set;
- contextual rejection of unsupported signatures, owners, field dispositions, value-type ancestry, EH, and opcodes;
- a dump reopen/rebind fixture whose counted metadata, physical body, admitted `ldfld`, runtime field descriptor, and
  exact four-byte observation reconstruct the same prepared execution evidence; and
- local headless verification at `19c292f9f`: a zero-warning 15-project Release build, 103 non-cybersecurity unit
  tests, 67 fast integration tests, 5 ordinary dump tests, 1 optimized-context dump test, the focused 2-test W3 lane,
  and both documentation guards, all with zero skips;
- a cumulative hand-written implementation range from `e7b6a4ace` of `+8,842/-1,650` LOC
  (`+5,362/-928` production and `+3,480/-722` tests/fixtures), plus 39 generated lock-file lines.

Remaining stabilization gates are deliberately capability-driven:

- generic signature and instantiation fixtures when generics enter an active milestone;
- Portable PDB sequence-point/local-scope fixtures when an expression or stepping path actually needs them;
- broader bounded malformed/truncated artifact tests before any broader artifact-input promise; and
- an exact pushed documentation-closure commit with every required hosted job passing before W3 is recorded closed.
