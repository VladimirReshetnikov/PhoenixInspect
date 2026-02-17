# Roslyn usage notes for this project

## Why it matters

Roslyn is potentially useful in this project as a **language and semantic front-end**, not as a dump-time runtime executor.

Relevant value includes:

- expression parsing/binding for debugger-like evaluation entry points,
- overload-resolution assistance in constrained scenarios,
- syntax/semantic tooling integration for IDE-hosted experiences.

## Snapshot review highlights

The `lib/roslyn/src` snapshot is large, with most relevant code under `Compilers/CSharp/Portable`:

- compilation model (`Compilation/CSharpCompilation.cs`),
- syntax tree model (`Syntax/CSharpSyntaxTree*.cs`),
- parser and syntax factory (`Parser/LanguageParser.cs`, `Syntax/SyntaxFactory.cs`),
- parse and compilation options (`CSharpParseOptions`, `CSharpCompilationOptions`).

For our scope, we only need a narrow subset: parse expressions, bind semantics, and capture diagnostics/provenance.

## Source-level API surfaces relevant to our adapters

### 1) Expression parsing entry points

`SyntaxFactory.ParseExpression(...)` is an explicit API that routes through Roslyn parser internals.

Practical implication: we can define a deterministic expression front-end contract without exposing Roslyn parser internals.

### 2) Syntax tree creation and control

`CSharpSyntaxTree.Create(...)` and `ParseSyntaxTree(...)` provide controlled tree creation with parse options and path/encoding metadata.

Practical implication: expression requests can carry source-context metadata that later feeds debug-map and diagnostics provenance.

### 3) Compilation and script mode entry points

`CSharpCompilation.Create(...)` and `CreateScriptCompilation(...)` demonstrate two useful modes:

- standard compilation-like binding contexts,
- submission/script-style contexts that may resemble debugger expression scenarios.

Practical implication: we should evaluate script compilation only as an optional assistive mode and not as a requirement for dump-time evaluation.

### 4) Semantic model access

`CSharpCompilation.GetSemanticModel(...)` is the core semantic-binding gateway.

Practical implication: semantic assistance should be isolated in an optional adapter that emits confidence/provenance metadata and can be disabled.

### 5) Parser internals and complexity warning

`LanguageParser` is feature-rich and handles broad C# grammar recovery rules.

Practical implication: we should not couple core architecture to Roslyn internal parse behavior details; only consume stable public entry points and normalize outputs.

## Potential project applications

1. **Expression front-end for evaluation UX**
   - parse user expressions into analyzable forms before lowering to interpreter-friendly IR.
2. **Selective semantic assistance**
   - resolve candidate members/types where runtime metadata is incomplete but source context exists.
3. **Host IDE integration layer**
   - align with diagnostics, source locations, and language-service affordances.
4. **Conflict detector**
   - compare Roslyn semantic expectations against runtime facts and downgrade confidence when mismatched.

## Boundary and architecture guidance

- Roslyn should remain optional and host-facing in core architecture.
- The interpreter core should consume language-agnostic contracts, not Roslyn syntax trees or symbols.
- Any Roslyn-assisted result must carry provenance and uncertainty markers.
- Parse options and language version must be treated as explicit inputs to preserve determinism.

## Risks and design pressure

1. **Semantic mismatch risk**
   - compile-time binding assumptions may differ from dump runtime reality.
2. **Overreach risk**
   - treating Roslyn as an execution substitute can obscure deterministic interpreter design goals.
3. **Coupling risk**
   - tight Roslyn coupling may limit non-C# and non-IDE host scenarios.
4. **Option drift risk**
   - inconsistent parse/compilation options can cause non-reproducible semantic outcomes.

## Recommended next experiments

1. Define an `ExpressionFrontEnd` contract that includes parse options, diagnostics, and confidence grading.
2. Prototype two Roslyn modes (`Create` vs `CreateScriptCompilation`) and compare semantic outputs for debugger-like expressions.
3. Add mismatch scenarios where Roslyn-binding results conflict with runtime metadata and ensure conservative fallback behavior.
4. Record a deterministic input bundle schema (expression text + language version + references) for replayable analysis.
