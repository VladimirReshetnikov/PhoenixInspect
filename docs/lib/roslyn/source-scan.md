# Roslyn source scan notes (snapshot: `lib/roslyn`)

This note records a source-driven scan of Roslyn C# compiler surfaces relevant to expression parsing/binding support in dump-time workflows.

## What was reviewed

Primary files and surfaces reviewed:

- `src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs`
- `src/Compilers/CSharp/Portable/Syntax/CSharpSyntaxTree.cs`
- `src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs`
- `src/Compilers/CSharp/Portable/Parser/LanguageParser.cs`

## Structural observations

- Roslyn exposes high-level parse and compilation APIs while hiding large internal parser/binder machinery.
- Syntax parsing entry points include policy-relevant knobs (options, paths, source text, strictness).
- Semantic model access is guarded by compilation ownership and option checks.

## Source-backed findings

## 1) `SyntaxFactory.ParseExpression` strictness is policy-sensitive

Observed behavior:

- `ParseExpression` accepts `consumeFullText` to decide whether trailing tokens are tolerated or converted into diagnostics.

Design implication:

- Make parse strictness explicit in our expression-front-end request schema.
- Default debugger scenarios to strict mode and allow opt-in recovery mode for exploratory tooling.

## 2) Syntax-tree parsing carries deterministic metadata inputs

Observed behavior:

- `ParseSyntaxTree` overloads accept parse options, path, and source text/encoding data.
- Parsing routes through `CSharpSyntaxTree` construction with option-bearing context.

Design implication:

- Deterministic replay bundles should include parse options, path/source identity, and reference set identity.
- Avoid hidden host defaults for parse options.

## 3) Compilation mode split matters (`Create` vs `CreateScriptCompilation`)

Observed behavior:

- `CreateScriptCompilation` validates script-specific options and creates submission-style contexts distinct from standard compilation.

Design implication:

- Capture compilation mode as part of provenance.
- Keep script mode optional and tightly scoped to scenarios that require REPL/submission semantics.

## 4) Semantic model APIs require tree membership and option agreement

Observed behavior in `GetSemanticModel(...)`:

- Throws if syntax tree is not part of the compilation.
- Uses semantic-model provider when available, otherwise creates model directly.

Design implication:

- Wrap semantic model requests behind project-owned contracts that always include compilation identity.
- Treat semantic outputs as advisory when runtime facts conflict.

## 5) Parser recovery internals are complex and should remain encapsulated

Observed behavior in `LanguageParser`:

- Extensive terminator and recovery-state logic across many grammar contexts.

Design implication:

- Normalize diagnostics and recovery outcomes into stable project categories.
- Do not expose Roslyn parser-internal states in interpreter-facing APIs.

## Adapter follow-through checklist

- Define an expression front-end policy object: parse options, strictness, compilation mode, reference profile.
- Add conformance scenarios for strict vs recovery parse behavior.
- Add mismatch scenarios where semantic model and dump runtime disagree, validating conservative fallback behavior.
- Ensure no Roslyn syntax/symbol objects leak beyond adapter boundary.

## Confidence and caveats

- Confidence is high for front-door parse/compilation APIs.
- Confidence is medium for corner-case recovery semantics until validated against debugger-focused expression corpora.
