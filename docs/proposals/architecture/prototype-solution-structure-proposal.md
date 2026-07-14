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
| `Interpreter.Core.Abstractions` | Type/body shapes plus identity, value-domain, persistent-memory, budget, and resolution contracts needed by the engine. |
| `Interpreter.Core.Execution` | Deterministic micro-step engine and machine-state protocol. |
| `Interpreter.Domain.Concrete` | Concrete validation values and persistent virtual memory. |
| `Interpreter.Metadata.Abstractions` | Project-owned metadata identities/projections required by active code. |
| `Interpreter.Metadata.SRM` | Active SRM/PEReader artifact adapter. |
| `Interpreter.Host.Abstractions` | Typed host/dump evidence contracts. |
| `Interpreter.Host.Dump.ClrMD` | Dump loading, runtime/module discovery, and raw memory evidence through ClrMD. |
| `Interpreter.Product.DumpQuery` | Closed W2 grammar, ordinal root/member binding, bounded read-only evaluation, redacted value projection, and replay integration. |
| `Interpreter.Host.ExternalWorker` | Trusted Windows x64 staging broker, bounded protocol/contracts, response validation, AppContainer/Job/handle policy, payload-free telemetry projection, and observable cleanup. |
| `Interpreter.Host.ExternalWorker.Runner` | One-request framework-dependent AppContainer executable that re-verifies containment, pins the trusted DAC, disables ambient capabilities, evaluates the admitted dump query, and exits. |

Tests are separated into a fast semantic/contract suite, a real dump integration suite, a Windows external-worker
suite, and two generated target executables: the general dump target and the optimized modeled-incident target.

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
  -> decode MethodDef RVA, tiny/fat header, code, locals, padding, and declared extra sections from counted dump evidence
  -> compare with a full-content-identified disk artifact as an independent fixture oracle
  -> execute the normalized body built solely from exact dump evidence
  -> report explicit snapshot/module availability, source, fallback, and only bounds whose operation was reached
  -> preserve partial wrappers as explanatory evidence without manufacturing a scalar answer
  -> close/reopen the dump, rediscover module/root, and reproduce canonical result bytes and their SHA-256
```

The runtime binding identity is the counted metadata root's MVID, exact metadata length, and metadata SHA-256. The independently opened disk PE additionally has a whole-file identity (exact artifact length plus SHA-256), so changing IL outside the metadata root changes artifact/module/method handles even if an incorrectly preserved MVID and metadata root would not. That disk identity is not derivable from the dump metadata root and does not authenticate dump code. The disk bodies are used only to assert equality: the MethodDef RVA, tiny/fat header, `maxstack`, init-locals flag, local-signature token, code, padding, and exception sections are decoded from exact counted dump metadata and memory reads. The real-dump evidence now includes tiny `RetOnly` and a compiler-emitted fat body with locals and two EH regions. W2 proves bounded binding/evaluation for one selected root and one field; it does not prove frame recovery, arbitrary heap-root discovery, chained expressions, broad IL semantics, or debugger stepping.

The external-worker projects are separately executable, and their four-test package includes a locally passing real
malformed-artifact process checkpoint. This is non-gating prototype work outside W1; its presence does not admit an
external artifact product surface.

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
prototypes outside W1.

Local verification on 2026-07-14 passed locked restore, the strict 15-project Release build with 0 warnings/errors,
64/64 core tests, 63/63 fast integration tests, 3/3 ordinary dump tests, and 1/1 optimized-context test through the
headless wrapper. [Hosted run
29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs
at exact W1 closure commit `e2580a8a8`.

The physical layout and contracts remain prototype hypotheses. They may change freely as W1–W4 force better boundaries.
