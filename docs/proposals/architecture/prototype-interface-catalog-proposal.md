# Prototype Contract Inventory

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Stability:** Draft and reversible

## Purpose

This inventory records the small public contract surface exercised by the current dump-evidence and concrete-IL proofs. It is descriptive, not a promise of compatibility. A contract is added only with an executable consumer and is removed when it gets ahead of code.

## Active contracts

### Core semantics

`Interpreter.Core.Abstractions` contains:

- path-independent `ModuleHandle` and definition-based `MethodHandle` identities, including complete-artifact content when a disk PE is the source;
- `IValueDomain<TValue>`, including executable order, meet, join, and widening operations;
- `IMemoryModel<TValue,TMemory>` constrained by `IPersistentMemoryState<TSelf>`;
- the deliberately narrow `IResolutionServices.GetMethodBody` boundary;
- immutable `TypeSig`/`MethodBody`, budget, operation, and stack-category shapes used by those contracts.

`Interpreter.Core.Execution` contains:

- semantic `MachineState` and separate `MachineOperationalState` budget bookkeeping;
- root-frame state, optional root return values, and a semantic comparer that excludes operational history;
- whole-body `MethodAdmissionResult`, structured `ExecutionFailure`, and low-level `MachineRunStatus`;
- `IlMachine.StepOne`, whose current closed set is `nop`, integer constants, argument/local loads, local stores, `add`, `sub`, `mul`, and `ret`.

Admission rejects unsupported opcodes, malformed operands, non-boundary offsets, incompatible seeded stack/slot shapes, nested frames, and exception regions before budget consumption, state mutation, or instruction events.

`TMemory` is carried as a persistent semantic snapshot but the arithmetic machine does not inject or call
`IMemoryModel`; the concrete memory model is an independently tested W3 spike. That capability enters the machine
only alongside the first memory-touching opcode and an end-to-end transfer test.

The current body contract does **not** decode a method signature: argument/local counts and `ReturnsValue` are
trusted frame-seeding inputs, then checked only against IL slot use, stack depth, and the admitted I4 stack category.
This is sufficient for the controlled differential corpus but is not an untrusted-method admission boundary. W3
must project signature/local types from metadata and validate seeded frames before widening this kernel's claim.

### Concrete validation domain

`Interpreter.Domain.Concrete` supplies the first real implementation of both semantic seams:

- a lifted-flat concrete value lattice with one semantic top per static type;
- executable lattice order and meet/join laws;
- persistent object, array, and field snapshots;
- branch isolation through immutable memory updates.

It is a semantics-validation domain, not a CLR object-layout emulator or production heap.

### Artifact metadata

`Interpreter.Metadata.Abstractions` retains only module identity/descriptor and method-definition/body acquisition contracts. `Interpreter.Metadata.SRM` implements them with `PEReader` and `System.Reflection.Metadata`.

Paths and names are display or acquisition hints, never identity. A disk module carries both metadata-root identity (MVID, metadata length, metadata SHA-256) and complete-artifact identity (whole-file length and SHA-256); the latter prevents PE files with identical metadata but changed IL from aliasing. Method lookup and body acquisition return structured unavailable/unsupported/conflict/invalid failures. The body projection preserves max stack, local-signature presence, local initialization, and exception-region count so execution admission cannot erase unsupported evidence.

### Dump evidence

`Interpreter.Host.Abstractions` contains only the counted immutable process-memory read contract and its exact/partial/unavailable result.

`Interpreter.Host.Dump.ClrMD` owns ClrMD-specific evidence:

- content-identified dump sessions and snapshot-scoped runtime-module instances;
- immutable module catalogs and bounded object searches with completeness status;
- bounded strong-GCHandle discovery with raw handle/object-header validation, plus primitive-field, bounded-string, metadata-root, and complete tiny/fat method-body observations;
- 8 GiB external-dump admission and a 256 MiB ClrMD cache with stack-derived caches disabled; SRM's typed external `Open` boundary separately admits managed PEs up to 512 MiB;
- coherent `Exact`, `Partial`, `Unavailable`, `Conflict`, and `Invalid` evidence statuses plus stable issue codes;
- ordered raw reads that retain source identity, address, requested length, returned bytes, and missing-byte count.

The dump body obtains its MethodDef RVA from counted dump metadata and its header, code, local-signature token, and declared extra sections from counted dump memory. A disk body's SRM decode is only an independent test oracle. Dump evidence and disk-artifact evidence remain distinct even when their complete metadata-root identities agree; MVID alone is not a sufficient binding.

The size/cache caps are deterministic resource controls. A narrow Windows x64 external-worker prototype has locally passed one real malformed-artifact checkpoint, but it is non-gating work outside W1 and does not create an admitted external artifact product surface.

### Read-only dump query

`Interpreter.Product.DumpQuery` exposes the bounded `DumpQueryEngine` and the closed `DumpQueryValue` projection for one
host-selected root, one direct field, and an optional admitted coalescing literal. Evaluation returns the shared
`EvaluationResult<DumpQueryValue>` envelope rather than collapsing completion, answer completeness, evidence status,
effects, provenance, diagnostics, and evidence context into one success flag.

The evidence context records explicit snapshot/module availability, source and fallback, and only bounds whose guarded
operations were reached on that result path. Exact null, unavailable evidence, a partial string prefix, and a retained
partial primitive-field wrapper remain distinct: the wrapper can explain a failure but cannot become a decoded scalar
answer. Canonical replay covers the complete result envelope; the integration proof closes and reopens the same dump,
rediscovers the module/root, and reproduces both its canonical bytes and SHA-256 fingerprint.

## Deliberately absent

The active public surface does not contain speculative debugger sessions, frame seeding, generic reconstruction, symbol/debug-map providers, call models, async/dynamic models, abstract-analysis worklists, product facades, or service locators. Their research documents do not reserve API or assembly names.

## Change rule

A public contract change must include:

1. detailed XML documentation of intent, failure behavior, parameters/returns, and draft caveats;
2. an executable test at the boundary it introduces;
3. deterministic identity and ordering where observable;
4. explicit partial/unavailable behavior for evidence-dependent operations;
5. an update to this inventory when the responsibility materially changes.
