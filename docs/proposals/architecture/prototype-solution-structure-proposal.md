# Prototype Solution Structure

**Lifecycle:** Current implementation note
**Roadmap relation:** Active
**Compatibility:** Draft and intentionally reversible

## 1. Why the solution was collapsed

The earlier solution contained 42 source projects: 34 were project-file-only placeholders before this pass, including the now-implemented concrete domain. That physical decomposition encoded an unvalidated multi-product architecture and imposed build/dependency surface without behavior.

The 33 remaining empty placeholders were removed. The one-purpose `Interpreter.Types` and `Interpreter.IL` DTO assemblies were then folded into core contracts. The W2 query slice subsequently justified one behavior-bearing product boundary, leaving eight source projects. The logical catalog remains in `module-architecture-proposal.md` as historical research, while `architecture-overview-proposal.md` defines the active topology and the rule for future splits.

## 2. Current source projects

The solution retains eight source projects, each containing contracts or behavior exercised by an active slice:

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

Tests are separated into a fast semantic/contract suite, a real dump integration suite, and the dump target executable.

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
  -> decode MethodDef RVA, tiny/fat header, code, and declared extra sections from counted dump evidence
  -> compare with a full-content-identified disk artifact as an independent fixture oracle
  -> execute the normalized body built solely from exact dump evidence
```

The runtime binding identity is the counted metadata root's MVID, exact metadata length, and metadata SHA-256. The independently opened disk PE additionally has a whole-file identity (exact artifact length plus SHA-256), so changing IL outside the metadata root changes artifact/module/method handles even if an incorrectly preserved MVID and metadata root would not. That disk identity is not derivable from the dump metadata root and does not authenticate dump code. For the active fixture, the disk body is used only to assert equality: the MethodDef RVA, tiny/fat header, `maxstack`, init-locals flag, local-signature token, code, and exception-section count used by execution are decoded from exact counted dump metadata and memory reads. W2 proves bounded binding/evaluation for one selected root and one field; it does not prove frame recovery, arbitrary heap-root discovery, chained expressions, broad IL semantics, or debugger stepping.

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
- Checked-in CI target: locked restore, Release build with warnings as errors, fast tests, then the supported Windows dump test. It is CI-enforced only after a workflow run is recorded; existence of the YAML alone is not that evidence.

The physical layout and contracts remain prototype hypotheses. They may change freely as W1–W4 force better boundaries.
