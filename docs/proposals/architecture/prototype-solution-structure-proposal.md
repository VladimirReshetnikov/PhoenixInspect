# Prototype Solution Structure

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Compatibility:** Draft and intentionally reversible

## 1. Why the solution was collapsed

The earlier solution contained 42 source projects: 34 were project-file-only placeholders before this pass, including the now-implemented concrete domain. That physical decomposition encoded an unvalidated multi-product architecture and imposed build/dependency surface without behavior.

The 33 remaining empty placeholders were removed. The one-purpose `Interpreter.Types` and `Interpreter.IL` DTO assemblies were then folded into core contracts. The W2 query slice subsequently justified one behavior-bearing product boundary. A separately landed, non-gating external-worker prototype added exactly two more behavior-bearing boundaries—a trusted Windows broker/protocol assembly and a one-request AppContainer runner—leaving ten source projects. The logical catalog remains in `module-architecture-proposal.md` as historical research, while `architecture-overview-proposal.md` defines the active topology and the rule for future splits.

## 2. Current source projects

The solution retains ten source projects, each containing contracts or behavior exercised by a realized slice:

| Project | Current responsibility |
|---|---|
| `Interpreter.Core.Abstractions` | Structural type/method/field identities, atomic resolution shapes, value-domain and optional value-precision classification, typed memory-result, persistent-memory, and budget contracts. |
| `Interpreter.Core.Execution` | Metadata-derived activation, frozen typed whole-body admission, exact-only-by-default unknown policy, and deterministic micro-step/machine-outcome protocol. |
| `Interpreter.Domain.Concrete` | Concrete validation values, persistent allocated/imported object and field memory, and W4.2's provenance-aware value/domain plus canonical lineage graph. |
| `Interpreter.Metadata.Abstractions` | Project-owned metadata identities and complete method/field projection contracts. |
| `Interpreter.Metadata.SRM` | Active SRM/PEReader artifact adapter and reusable method/signature/local/field projection over a `MetadataReader`. |
| `Interpreter.Host.Abstractions` | Typed host/dump evidence contracts. |
| `Interpreter.Host.Dump.ClrMD` | Dump loading, runtime/module discovery, raw evidence, and W3 snapshot-scoped execution resolution/import correlation through ClrMD. |
| `Interpreter.Product.DumpQuery` | Closed W2 grammar, typed snapshot-root binding, one-time field selection into immutable canonical plans, bounded `Evaluate(plan)`, closed value projection, and complete-corpus replay. |
| `Interpreter.Host.ExternalWorker` | Trusted Windows x64 staging broker, bounded protocol/contracts, response validation, AppContainer/Job/handle policy, payload-free telemetry projection, and observable cleanup. |
| `Interpreter.Host.ExternalWorker.Runner` | One-request framework-dependent AppContainer executable that re-verifies containment, pins the trusted DAC, disables ambient capabilities, evaluates the admitted dump query, and exits. |

Tests are separated into a fast semantic/contract suite, a real dump integration suite, a Windows external-worker
suite, and two generated target executables: the general dump target and the optimized modeled-incident target. The
real-dump suite contains an independently versioned 22-case/20-expression W2 corpus rather than treating one query in the W1
omnibus test as query-product closure evidence. It also contains a dedicated W3 direct/adjusted getter lane that
executes only exact counted dump evidence and reopens/rebinds the dump for replay. The W4.1 generated fixture freezes
the branchless target and CoreCLR oracle; dump-free W4.2 domain and machine suites exercise precision policy,
explained-unknown arithmetic, lattice laws, canonical lineage identities, capture, and fresh-domain replay.

## 3. Dependency rules

- Core execution depends only on core contracts, never ClrMD or SRM.
- Concrete backends depend inward on project-owned contracts.
- Dump runtime identity and artifact identity are joined through explicit mapping/evidence, not conflated by paths.
- Dependency edges remain acyclic and point toward smaller stable concepts.
- Public prototype APIs carry detailed XML documentation and no compatibility promise.

## 4. Evidence boundary

The active integration seam proves:

```text
write full dump
  -> content-identify and open it read-only
  -> discover a runtime module and bounded strong-GCHandle root
  -> perform counted dump-memory reads for primitive/string/metadata/IL evidence
  -> convert root-search evidence into exact-object/absence/partial/unavailable/conflict/invalid typed binding state
  -> parse the closed W2 grammar and select the requested instance field exactly once during preparation
  -> freeze snapshot, owner, field, decoder, optional literal, and reached bounds into an immutable canonical plan
  -> evaluate that plan through its selected Int32, Nullable<Int32>, or String decoder without member rebinding
  -> decode MethodDef RVA, tiny/fat header, code, locals, padding, and declared extra sections from counted dump evidence
  -> project body + signature + locals + receiver + exact ldfld FieldDef from that counted dump evidence
  -> correlate the rooted runtime owner and exact four-byte Int32 observation with the admitted field operand
  -> import only that exact cell into persistent concrete memory
  -> derive root activation from metadata and freeze a typed whole-body plan before instruction zero
  -> execute the direct and constant-adjusted getters through one real IMemoryModel ldfld transfer
  -> compare with a full-content-identified disk artifact as an independent late fixture oracle
  -> report explicit snapshot/module availability, source, fallback, and only bounds whose operation was reached
  -> preserve partial wrappers as explanatory evidence without manufacturing a scalar answer
  -> repeat all 22 versioned query cases in one session, then close/reopen and reconstruct the root binding
  -> reproduce every canonical result byte sequence/SHA-256 and each successfully prepared plan projection/SHA-256
  -> close/reopen/rebind the W3 module, root, method, field, and import, then reproduce execution transcripts
```

W4.2 adds a separate dump-free evidence boundary within the existing three core projects:

```text
bounded partial/unavailable input origin
  -> content-addressed InputOrigin node + explained Int32 semantic top
  -> optional ExplainedInt32 machine policy
  -> existing argument/local/store/arithmetic/return transfers
  -> ordered BinaryTransform nodes with embedded exact operands
  -> reachable-only canonical graph capture
  -> validated replay in a fresh provenance-domain instance
```

Semantic equality, hashing, lattice order, join, meet, and widening ignore the optional lineage root. The default
`ExactOnly` policy preserves W3 behavior; exact receivers, initialized locals, and exact-classified field loads remain
exact. Bare top is not executable.

The runtime binding identity is the counted metadata root's MVID, exact metadata length, and metadata SHA-256. W3's
execution module handle additionally incorporates stable snapshot/runtime-module evidence, so different loader
instances cannot alias through repeated names or addresses. The independently opened disk PE has a whole-file
identity (exact artifact length plus SHA-256), so changing IL outside the metadata root changes disk
artifact/module/method handles even if an incorrectly preserved MVID and metadata root would not. That disk identity
is not derivable from the dump metadata root and does not authenticate dump code. The disk bodies are used only to
assert equality: the MethodDef RVA, tiny/fat header, `maxstack`, init-locals flag, local-signature token, code, padding,
and exception sections are decoded from exact counted dump metadata and memory reads. The real-dump evidence includes
tiny `RetOnly`, a compiler-emitted fat body with locals and two EH regions, and exact direct/adjusted `Int32` getter
bodies admitted through the counted-dump resolver.

W2 proves typed binding and bounded evaluation for one host-named, exactly selected non-null object and one exact
instance field. Its admitted value domain is `String`, `Int32`, and `Nullable<Int32>` with only compatible literal
coalescing. A canonical plan includes the grammar version, root/field names, snapshot-scoped owner and field identity,
decoder, and exact optional literal; request identity also preserves bounded failures that never produce a plan. The
full 22-case/20-expression corpus reproduces every result byte sequence/fingerprint and the 13 successfully prepared
plan projection strings/fingerprints within and across dump sessions. This implementation and local headless
verification passed all four required hosted jobs at exact closure commit `5bed47100` in [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178).
It does not prove exact-null query roots, frame/local/argument/static recovery, arbitrary heap-root discovery, chained
or null-conditional query access, product-level properties/getters, calls, indexers, arrays, reflection, construction,
general operators, broad IL semantics, or debugger stepping. Separately, W3 proves only counterfactual execution of
the closed E1 arithmetic and E2 direct/constant-adjusted getter profiles; it does not expose that capability through
the W2 product grammar.

Hardened W3 checkpoint `19c292f9f` is locally verified through locked restore, the 15-project zero-warning Release
build, Markdown/headless guards, 103 non-cybersecurity unit tests, 67 fast integration tests, 5 ordinary dump tests,
1 optimized-context dump test, and the focused 2-test W3 lane. All four hosted jobs also passed at that exact
implementation checkpoint in [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767). W3 formally closed at exact
documentation commit `de6cea124`; [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required jobs
at that exact commit.

W4.1 and W4.2 are landed. Exact W4.2 implementation commit `e89e43498` adds the optional precision seam,
`UnknownExecutionPolicy`, `ProvenanceConcreteDomain`/`ProvenanceConcreteValue`, canonical `InputOrigin` and
`BinaryTransform` lineage, and shared-handler unknown arithmetic. Its current implementation checkpoint is 3,454 LOC:
3,429 LOC for W4.2 plus a 25-LOC scope correction. The cumulative W4 realization through W4.2 is 3,932 LOC. Non-exact
`ldfld`/`FieldLoadTransform`, calls, models, the counterfactual facade/product result, and generated-dump closure remain
absent and are W4.3–W4.9 work.

The external-worker projects are separately executable, and their four-test package includes a locally passing real
malformed-artifact process checkpoint. This is non-gating prototype work outside W1–W4; its presence does not admit
an external artifact product surface.

## 5. Rule for adding a project

A new assembly must satisfy all three conditions:

1. it contains implementation required by an active milestone;
2. its dependency boundary is independently useful (for example, it prevents a concrete backend dependency from entering the core);
3. an executable test crosses that boundary.

A desired namespace, future product, candidate backend, or possible plugin is not sufficient. Start as a logical seam or an internal type; split only when evidence makes the boundary real.

## 6. Toolchain

- Stable .NET 10 LTS SDK selected through `global.json`.
- `net10.0` development target; consumer multi-targeting deferred until demanded.
- Central package versions and committed restore lock files.
- Canonical unattended managed entry point: `./eng/Invoke-HeadlessProcess.ps1 dotnet ...`; every test assembly also
  reasserts Win32, thread, WER, and .NET no-dialog policy.
- Required W1 CI target: local-Markdown-link and headless-workflow consistency, locked restore, Release build with
  warnings as errors, fast tests, then the supported ordinary-dump and optimized-context Windows lanes. The worker
  projects remain solution-build-checked, but their tests are outside the default W1 workflow. The historical exact pushed W0 commit
  `3ece32a36eccc06a61025b1b35b58c09f6e4ed09` passed the documentation job, the build/fast job (60
  semantic/differential and 40 fast adapter/harness tests), and the dependent 3-test dump job in
  [GitHub Actions run 29309374548](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29309374548),
  completed 2026-07-14 UTC (2026-07-13 PDT). Third-party actions are pinned to verified release commit SHAs.

That hosted run is the W0 baseline. The malformed corpus and external worker are separately landed, non-gating
prototypes outside W1–W4.

Historical unfiltered local verification on 2026-07-14 passed locked restore, the strict 15-project Release build with
0 warnings/errors, 64/64 core tests, 63/63 fast integration tests, 3/3 ordinary dump tests, and 1/1 optimized-context
test through the headless wrapper. [Hosted run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`.

The later filtered W2 v1 local checkpoint `ff7cd1965` passes the 15-project zero-warning build, 64 core tests, 67 fast
integration tests, 4 ordinary dump tests (including its 22-case corpus), and 1 optimized-context test. All test
invocations exclude `Scope=Cybersecurity`. Restore/build intentionally remains repository-wide, including worker
projects and the integration assembly, as topology/compilation-health evidence only; worker projects/tests provide no
cybersecurity or milestone behavioral evidence. [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact W2 closure commit `5bed47100`; the W1 run above remains W1-only history.

The later hardened W3 checkpoint `19c292f9f` passes the same headless, non-cybersecurity workflow locally with
103 unit, 67 fast integration, 5 ordinary dump, 1 optimized-context, and 2 focused W3 tests; [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passes all four jobs at that
implementation commit. Formal W3 closure is recorded at exact documentation commit `de6cea124`, whose [GitHub Actions
run 29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) passed all four required
jobs.

The physical layout and contracts remain prototype hypotheses. They may change freely as W4.3–W4.9 force better boundaries.
