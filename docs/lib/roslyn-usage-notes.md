# Roslyn usage notes for this project

## Why it matters

Roslyn is potentially useful in this project as a **language and semantic front-end**, not as a dump-time runtime executor.

Relevant potential value includes:

- expression parsing/binding for debugger-like evaluation entry points,
- overload-resolution assistance in constrained scenarios,
- syntax/semantic tooling integration for IDE-hosted experiences.

## Potential project applications

1. **Expression front-end for evaluation UX**
   - parse user expressions into analyzable forms before lowering to interpreter-friendly IR.
2. **Selective semantic assistance**
   - resolve candidate members/types where runtime metadata is incomplete but source context exists.
3. **Host IDE integration layer**
   - align with diagnostics, source locations, and language-service affordances.

## Boundary and architecture guidance

- Roslyn should remain optional and host-facing in core architecture.
- The interpreter core should consume language-agnostic contracts, not Roslyn syntax trees or symbols.
- Any Roslyn-assisted result must carry provenance and uncertainty markers.

## Risks and design pressure

1. **Semantic mismatch risk**
   - compile-time binding assumptions may differ from dump runtime reality.
2. **Overreach risk**
   - treating Roslyn as an execution substitute can obscure deterministic interpreter design goals.
3. **Coupling risk**
   - tight Roslyn coupling may limit non-C# and non-IDE host scenarios.

## Early action items

- Define an `ExpressionFrontEnd` contract with explicit confidence/provenance output.
- Document which evaluator flows are Roslyn-assisted vs interpreter-only.
- Add test scenarios where Roslyn semantic guesses conflict with runtime facts and enforce conservative behavior.
