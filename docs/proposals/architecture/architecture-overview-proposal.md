# Architecture Overview

**Lifecycle:** Current design direction
**Roadmap relation:** Active and supporting
**Last reset:** 2026-07-13

## 1. Product and scope lock

The funded product direction is a **deterministic, read-only expression evaluator grounded in a .NET dump**. The interpreter is enabling technology for expressions that eventually require user IL; it is not presently a general-purpose execution platform.

The proof obligations are deliberately ordered; the first two now have executable generated-dump slices:

1. recover a value from actual dump memory with explicit evidence and failure reasons;
2. lower a restricted expression into a read-only query plan and evaluate it over that evidence;
3. execute a small, scenario-derived, EH-free IL subset through a concrete value and memory domain, checked against CoreCLR;
4. introduce provenance-bearing unknowns only when the exact slices above are trustworthy.

Virtual stepping, CFG/fixpoint analysis, async and dynamic lifting, sandbox runtime hosting, live speculation, and other product surfaces are research backlog. They do not drive packages or active contracts.

## 2. Truth model

Every externally visible result names its semantic mode. A confidence or “purity” badge alone is not a semantic contract.

| Semantic mode | Meaning |
|---|---|
| `Observation` | A fact decoded directly from snapshot or artifact evidence. |
| `DerivedQuery` | A deterministic calculation over observations without executing user IL. |
| `CounterfactualExecution` | What the admitted IL would compute from recovered or explicitly assumed state under a named policy. It is not historical replay. |
| `AbstractAnalysis` | May/must reasoning over a set of possible states. This remains research. |

An evaluation result keeps these axes separate:

- **completion:** completed, blocked, budget exhausted, cancelled, or invalid;
- **completeness:** complete, partial, or none;
- **evidence:** exact, partial, unavailable, conflicting, or invalid;
- **effects:** none, virtual-only, modeled, or unsupported;
- **provenance:** the dump ranges, runtime identities, artifacts, policies, and transformations that support the value;
- **diagnostics:** stable reason codes plus actionable explanations.

Hosts may project those axes into a compact badge, but the projection never replaces the underlying fields.

## 3. Active data flow

```text
Expression + selected dump context + deterministic policy
                         |
                         v
Restricted parser/binder -> admitted read-only query plan
                         |
              +----------+----------+
              |                     |
              v                     v
      Dump evidence adapter    Artifact/metadata adapter
      (ClrMD + raw reads)      (SRM/PEReader)
              |                     |
              +----------+----------+
                         |
                         v
                Read-only query evaluator
                         |
                         v
       Result axes + value + provenance + diagnostics

Later, only for admitted method bodies:
query plan -> interpreter kernel -> concrete/hybrid domain
```

The dump path is not an implementation detail after the interpreter. It is the primary product path and therefore lands first.

## 4. Active components

### 4.1 Dump evidence adapter

The ClrMD adapter owns dump loading, runtime/module discovery, heap layout queries, and raw target-memory reads. Reads return a count and a typed outcome; they do not collapse sparse memory, corruption, invalid addresses, and policy rejection into `false` or a default value.

Runtime module-instance evidence includes the runtime/app-domain identity, module address, image base and size, and metadata address when present. An on-disk path is only a hint to artifact acquisition and never proves that bytes came from the dump.

### 4.2 Artifact and metadata adapter

For the active slices, `System.Reflection.Metadata` and `PEReader` are the evidence-backed disk-artifact implementation. The current projected contract provides metadata and whole-file PE identity, unique simple-name MethodDef lookup for bounded fixtures, and independently decoded method bodies for tests and static-artifact scenarios. It supplies no execution fact to the dump-backed method-body path: that path reads the MethodDef RVA from counted dump metadata and decodes the tiny/fat header, code, local-signature token, and declared extra sections from counted dump memory. SRM can decode signatures, but signature projection is an unimplemented W3 requirement rather than a current contract. Portable PDB support may use SRM when the expression slice needs names or source mapping.

AsmResolver, dnlib, Cecil, ILSpy, and Windows-PDB readers remain comparison or future adapter candidates. A new backend is introduced only when a fixture demonstrates a material gap or cost in the active SRM path.

Dump bytes and artifact bytes remain distinct evidence sources even when identity validation shows that they correspond.

### 4.3 Restricted expression front end

The first front end accepts only syntax that can be lowered to a bounded, read-only query plan. Roslyn may parse syntax, but the project owns binding, admission, lowering, and diagnostic policy.

The implemented W2 grammar is intentionally smaller than the eventual restricted-expression surface:

- one exact, ordinal host-provided root name bound to an explicitly selected non-null heap object;
- one direct instance field selected with `.`; `?.` is rejected until the root model can carry exact null;
- an optional `??` literal restricted to `null`, `Int32`, or a bounded string;
- result values restricted to exact `Int32`, exact null, exact string, or an explicitly partial bounded string prefix.

The parser caps expression, identifier, and decoded-literal length. The evaluator caps string reads and preserves
missing or partial evidence instead of treating it as null. A selected nullable field may produce exact null and may
then be coalesced. Null-conditional access, chained traversal, backing-field projection, arrays, operators other than
coalescing, and frame roots remain later scenario-driven increments.

Method calls, construction, reflection, implicit assembly loading, user-defined conversions, and unbounded enumeration are rejected in this slice. Parse, bind, admission, and evidence failures use different reason codes.

### 4.4 Read-only query evaluator

The query evaluator executes a finite project-owned plan, not synthesized user code. The one-hop grammar is structurally bounded and subject to deterministic expression, identifier, literal, handle-scan, field-catalog, and string-read caps; each evidence read produces either a value or a typed partial/unavailable outcome. It has no filesystem, network, process, native, or target-mutation capability.

### 4.5 Interpreter kernel

The product delivery sequence admits method interpretation after the observation and query path. A bounded concrete arithmetic spike exists now because it retired a load-bearing architecture risk; it does not move W3 ahead of W1/W2. Its executable corpus determines the closed opcode set—opcode popularity or a desired percentage does not.

The implemented kernel is limited to branchless, EH-free I4 constants, arguments, locals, `add`, `sub`, `mul`, and `ret`. It executes through `IValueDomain<TValue>` while threading a self-constrained persistent `TMemory` unchanged. `IMemoryModel<TValue,TMemory>` is independently exercised by persistent concrete-memory tests and joins the machine only with the first admitted memory-touching opcode. Field-reading getters are the next W3 extension target, not current support.

Unsupported instructions and malformed bodies stop with structured diagnostics. They never emit an “instruction executed” event and never perform speculative concrete effects.

## 5. Identity model

Identity and location are separate concepts:

- a **dump metadata-root identity** is the MVID plus exact metadata length and SHA-256 decoded from one counted metadata image;
- a **complete artifact identity** is exact whole-file length plus SHA-256; a disk-backed module/method identity carries this in addition to its metadata-root identity and optional PE layout evidence;
- a **runtime module instance** identifies one loaded instance in one dump/runtime/app-domain;
- a **method definition** is the relevant module-content handle plus its MethodDef token; a disk-backed handle includes complete-artifact identity, while dump evidence retains the runtime-module instance and its independently observed metadata-root identity;
- a **constructed method/type context** adds a deterministic generic-instantiation identity;
- file paths, display names, enumeration order, process-random string hashes, and allocation counters are not stable identities.

Mappings between runtime instances and artifacts carry evidence status and mismatch diagnostics. Handles used in replay or tests must be derived from these identities, not from discovery order.

## 6. Evidence-result contract

Any boundary where missing or conflicting data is expected returns an explicit outcome equivalent to:

```text
EvidenceResult<T> = {
  Status: Exact | Partial | Unavailable | Conflict | Invalid,
  Value?: T,
  ReasonCode?,
  Provenance: ordered evidence references,
  Diagnostics: ordered diagnostics
}
```

Specialized zero-allocation outcomes are appropriate for hot paths such as `Read(address, Span<byte>)`, but they preserve the same distinctions and report the exact byte count. Exceptions are reserved for caller contract violations or internal defects, not ordinary dump sparsity.

## 7. Execution state and determinism

Semantic state and operational bookkeeping are separate.

```text
SemanticState
  frames, instruction offsets, arguments, locals, evaluation stacks,
  semantic memory, path facts, pending target exception

OperationalContext
  remaining budgets, cancellation, trace cursor, traversal worklists,
  provenance-ID allocation, caches, metrics, replay bookkeeping
```

Only semantic state participates in abstract-domain equality, joins, or widening. Decreasing budgets, growing traces, object allocation order, cache state, and provenance IDs must not prevent a fixpoint or make semantic equality traversal-dependent.

Memory used for undo or branch snapshots must expose a documented persistent-snapshot contract. `TMemory` being a generic type is not by itself a persistence guarantee; mutable implementations are not eligible for rewind claims.

Determinism means identical normalized inputs, policies, artifacts, dump evidence, and engine version produce identical values, statuses, ordered diagnostics, and transcript fingerprints. Wall-clock timeout and host cancellation are operational interruptions and are not interchangeable with deterministic budget exhaustion.

## 8. Status protocols

Different layers use different, explicitly mapped vocabularies:

- machine execution status: `Ready`, `Completed`, `BudgetExhausted`, `Blocked`, `InvalidProgram`;
- future virtual-session pause reason: `StepComplete`, `DecisionNeeded`, `ExceptionStop`, `BudgetStop`, `Cancelled`, `Completed`;
- adapter traversal: evidence status plus a miss-reason code;
- async/task lifecycle: diagnostic events, not stop reasons.

Machine `BudgetExhausted` maps to session `BudgetStop`; `Ready` may map to `StepComplete` only when the session controller's requested boundary is reached; `Completed` maps to session `Completed`. `Blocked` and `InvalidProgram` become diagnostics or terminal host results, not target-program `ExceptionStop` values. Internal invalidity never masquerades as a target exception.

## 9. Exception-handling admission

The first interpreted-method slice rejects bodies with exception regions and bodies that require exception transfer. A target throw may initially terminate the admitted path with an explicit stop-on-throw outcome. Handler search/unwind, filters, `fault`, `finally`, and cross-frame propagation are a later milestone and are prerequisites for debugger-grade Step Out and async `MoveNext` claims.

## 10. Security and data handling

Dumps, PE/PDB files, SourceLink documents, and symbol responses are untrusted, secret-bearing inputs.

Active generated-fixture defaults are:

- locator-backed acquisition is refused: the adapter replaces ClrMD's ambient/default locator immediately after dump construction and before runtime discovery;
- project-owned reads, strings, names, query shape, method bodies, and post-projection item scans have deterministic caps;
- no target code execution, native calls, or target mutation;
- no dump values, source text, paths, environment values, or expression results in telemetry by default;
- stable diagnostics redact payloads unless a local interactive host opts in;
- dump and disk metadata-root identities are verified before correlating their evidence, while whole-file artifact identity keeps distinct PE files distinct; neither comparison relabels disk bytes as dump evidence.
- external dump files are rejected above 8 GiB, ClrMD's dump cache is capped at 256 MiB with stack-trace/root caching disabled, and externally opened managed PE files are rejected above 512 MiB.

Those defaults are not a claim that in-process ClrMD is network-off or filesystem-confined. In the pinned version,
`DataTarget.LoadDump` constructs its default locator before replacement; CLR discovery and PE/DAC callbacks may probe
target-reported full paths outside `IFileLocator`; and parameterless runtime creation can accept a full-path DAC without
signature verification. ClrMD/DAC may also perform internal traversal before an adapter post-projection cap can apply.
Therefore only the generated same-toolchain fixture is admitted in-process. Arbitrary incident dumps require the
no-network/access-control worker and trusted-DAC policy below.

Generated full-dump fixtures also clear the target's inherited environment and use isolated working and temporary
directories. This prevents developer/CI credentials from becoming test evidence merely because the dump type includes
the environment block.

Before this library accepts arbitrary external artifacts in a product host, parsing/evaluation must run in a constrained worker process with resource limits and a narrow IPC contract. “Runs locally” is not a sandbox boundary.

The external-exposure gate permits one additional executable project, not a generic hosting subsystem. It must accept
one bounded request, an inherited read-only artifact handle, and return one bounded result frame before exiting. The
Windows launcher must atomically create the worker inside a Job Object, restrict inherited handles, cap process/job
memory and CPU time, forbid child processes, kill on job close/timeout/malformed IPC, clear the environment, use an
isolated working directory, disable .NET diagnostics, and retain the replacement no-acquisition ClrMD locator. IPC never carries an
ambient path authority or arbitrary memory-read operation. Telemetry is limited to operation/outcome and coarse
resource buckets; it excludes paths, dump identities, addresses, expressions, names, values, exception payloads, and
canonical replay bytes.

A Job Object is crash and resource containment, not a security sandbox: the worker otherwise retains the caller's
Windows token. If the threat model includes a compromised parser or DAC, product exposure additionally requires a
proven AppContainer with no network capability, a separate low-privilege account, or VM isolation. The project must
not claim hostile-artifact isolation until that access-control boundary and trusted-DAC policy have executable tests.

## 11. Physical topology

The prototype retains only projects containing behavior or contracts exercised by the active slices:

- `Interpreter.Core.Abstractions` and `Interpreter.Core.Execution` — backend-neutral type/body shapes, domain/memory contracts, and the interpreter kernel;
- `Interpreter.Domain.Concrete` — concrete validation domain and persistent memory;
- `Interpreter.Metadata.Abstractions` and `Interpreter.Metadata.SRM` — projected metadata contracts and active SRM adapter;
- `Interpreter.Host.Abstractions` and `Interpreter.Host.Dump.ClrMD` — typed dump evidence and ClrMD adapter.
- `Interpreter.Product.DumpQuery` — the bounded W2 parser, read-only evaluator, and closed result-value projection.

Logical seams may be documented without creating assemblies. A new project is justified only when it contains implementation, has an independently useful dependency boundary, and at least one test exercises that boundary. Empty product/model/backend projects are not placeholders.

## 12. Research entry gates

No research subsystem enters the active roadmap merely because a proposal exists.

- **Hybrid/abstract domains:** two domains execute the same meaningful opcode corpus and domain laws are tested.
- **Virtual stepping:** dump query and interpreted-method slices are trustworthy; persistent memory and handler-transfer EH exist.
- **Async/dynamic lifting:** ordinary compiler-generated prerequisite IL and EH are supported, with scenario fixtures.
- **Alternative products:** a second product demonstrates reuse without weakening the dump evaluator’s correctness and security model.
- **Alternative metadata backend:** a corpus records an SRM deficiency and an adapter test demonstrates a better trade-off.

The active sequence, sizing assumptions, and exit tests live in `docs/plans/future-work-planning.md`.
