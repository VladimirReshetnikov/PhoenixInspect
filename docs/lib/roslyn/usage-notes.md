# Roslyn usage notes for this project

> **Lifecycle:** Historical · **Research**
>
> This source-review note predates the active
> [C# Expression Front-End and Subset-Admission Contract](../../proposals/architecture/csharp-expression-front-end-contract-proposal.md).
> Where this note recommends optional parsing or a no-Roslyn mode, the active W6.2 decision supersedes it: Roslyn is
> the required expression parser, while binding/evaluation and all downstream contracts remain project-owned.

## Why it matters

Roslyn is useful here as a **language + semantic front-end**, not as a dump-time execution backend.

Relevant value includes:

- parsing debugger-style expressions,
- optional semantic binding assistance,
- diagnostics and source-span capture for explainability,
- host-facing language-service alignment for IDE scenarios.

## Snapshot review highlights

The `lib/roslyn/src` snapshot is large; our relevant area is mostly `Compilers/CSharp/Portable`:

- `Syntax/SyntaxFactory.cs` (expression and syntax tree parse entry points),
- `Syntax/CSharpSyntaxTree*.cs` (tree creation and source metadata),
- `Compilation/CSharpCompilation.cs` (compilation + semantic model APIs),
- `Parser/LanguageParser.cs` (parser behavior and recovery complexity).

For this project, we should keep Roslyn usage narrow: parse, optionally bind, emit diagnostics/provenance.

## Source-level API surfaces relevant to our adapters

### 1) Explicit expression parse gateway

`SyntaxFactory.ParseExpression(...)` offers a stable API for parsing standalone expressions.

Design implication:

- expression parsing can be isolated as a deterministic front-end service,
- parser internals stay encapsulated behind project contracts.

### 2) Syntax tree construction controls

`SyntaxFactory.ParseSyntaxTree(...)` and `CSharpSyntaxTree` APIs carry parse options, path, and text metadata.

Design implication:

- include parse options and source-context metadata in deterministic input bundles,
- preserve source/diagnostic provenance for replay and explainability.

### 3) Compilation modes for debugger-like contexts

`CSharpCompilation.Create(...)` and `CreateScriptCompilation(...)` expose standard vs submission-style binding contexts.

Design implication:

- treat script mode as optional and policy-controlled,
- compare both modes in experiments and record divergence explicitly.

### 4) Semantic model access patterns

`CSharpCompilation.GetSemanticModel(...)` is the primary binding surface for symbol/type inference.

Design implication:

- keep semantic assistance as optional enrichment,
- ensure every semantic result includes confidence/provenance tags,
- avoid hard dependency of interpreter correctness on Roslyn semantic success.

### 5) Parser recovery complexity warning

`LanguageParser` shows substantial terminator-state and recovery logic across full C# grammar support.

Design implication:

- avoid coupling contract semantics to parser-internal recovery behavior,
- normalize diagnostics and error recovery outputs into project-defined categories.

### 6) Compiler-scale surface area and process boundary need

The snapshot includes broad flow-analysis/codegen machinery far beyond our use case.

Design implication:

- constrain our Roslyn integration boundary to a minimal subset,
- prevent accidental scope creep into compilation/codegen concerns.

## Potential project applications

1. **Expression front-end service**
   - parse and basic syntax validation before interpreter lowering.
2. **Optional semantic assist channel**
   - provide candidate member/type resolution with confidence grading.
3. **Diagnostic enrichment**
   - map parse/bind diagnostics into user-facing explanations.
4. **Host tooling bridge**
   - enable IDE-facing experiences without coupling core interpreter to Roslyn objects.

## Boundary and architecture guidance

- Keep Roslyn optional and host-facing.
- Project to language-agnostic contracts before entering interpreter core.
- Include parse/compilation options and reference set in request provenance.
- Treat semantic output as advisory when runtime facts disagree.
- Keep fallback behavior deterministic when Roslyn is unavailable or inconclusive.

## Risks and design pressure

1. **Semantic mismatch risk**
   - compile-time assumptions can diverge from dump runtime reality.
2. **Scope creep risk**
   - easy to drift from expression front-end into full compilation concerns.
3. **Coupling risk**
   - Roslyn-first abstractions can hurt portability and non-C# extensibility.
4. **Option drift risk**
   - inconsistent parse/reference inputs can break determinism.
5. **Recovery variability risk**
   - parser recovery details may produce unstable behavior if not normalized.

## Recommended next experiments

1. Define `ExpressionFrontEnd` input/output contracts with deterministic input bundle fields.
2. Compare `Create` vs `CreateScriptCompilation` for debugger-style expression suites.
3. Create mismatch scenarios where runtime metadata conflicts with Roslyn binding and verify conservative fallback.
4. Normalize parse/bind diagnostics into stable miss-reason/provenance categories.
5. Add one no-Roslyn mode conformance scenario to keep interpreter contracts language-front-end-agnostic.

## Deep-dive addendum (2026-02 source pass)

Additional source-backed details from Roslyn C# compiler layer:

- `SyntaxFactory.ParseExpression(...)` and related parse helpers expose `consumeFullText` behavior that directly affects strictness and trailing-token handling.
- `SyntaxFactory.ParseSyntaxTree(...)` overloads carry parse options, path, and text metadata that should be preserved for deterministic replay.
- `CSharpCompilation.Create(...)` vs `CreateScriptCompilation(...)` establishes distinct binding contexts that can produce divergent semantic outcomes.
- `GetSemanticModel(...)` enforces syntax-tree membership in the compilation and supports options shaping model behavior.

Design addendum:

1. Include parse strictness (`consumeFullText`) and compilation mode in front-end request provenance.
2. Treat semantic-model results as advisory artifacts with explicit confidence and mismatch handling against runtime truth.
3. Normalize parser recovery and diagnostics into stable project result categories.
