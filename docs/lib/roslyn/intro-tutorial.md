# Roslyn intro tutorial for this project

## Audience and goal

This tutorial is for contributors who are comfortable with C# but new to Roslyn internals.

By the end, you should understand:

- which Roslyn layers matter for our dump-time interpreter design,
- the minimum API surface we should rely on,
- how to build a small parse + bind pipeline without over-coupling our architecture to compiler internals.

> Important: the snapshot under `lib/roslyn` is for source study only. Production implementation should reference Roslyn packages from NuGet through our adapters.

## 1) Roslyn mental model in 5 minutes

For this project, think of Roslyn as four layers:

1. **Text + parse options**  
   Input text and parser configuration (`CSharpParseOptions`) establish language mode and syntax behavior.
2. **Syntax tree**  
   Immutable tree produced by APIs like `SyntaxFactory.ParseExpression(...)` and `CSharpSyntaxTree.ParseText(...)`.
3. **Compilation context**  
   `CSharpCompilation.Create(...)` or `CreateScriptCompilation(...)` + metadata references define symbol resolution rules.
4. **Semantic model**  
   `GetSemanticModel(...)` maps syntax to symbols/types and gives binding diagnostics.

For our architecture, Roslyn is a **front-end service**. It helps parse and optionally bind expressions; it is not our execution engine.

## 2) What we reviewed in `lib/roslyn`

The source snapshot is broad, but a practical onboarding subset is:

- `src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs`  
  Parse entry points such as `ParseExpression(...)`, `ParseName(...)`, and `ParseSyntaxTree(...)`.
- `src/Compilers/CSharp/Portable/Syntax/CSharpSyntaxTree.cs`  
  Tree creation/parsing overloads, parse options, file path handling, and text/checksum plumbing.
- `src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs`  
  Standard vs script compilation creation and semantic model retrieval.
- `src/Compilers/CSharp/Portable/Parser/LanguageParser.cs`  
  Internal parser complexity and error-recovery/terminator-state behavior.

If you are new to Roslyn, start with those files before exploring flow analysis, lowering, or emit pipelines.

## 3) Step-by-step workflow you can map to our adapters

## Step A: Parse an expression

Use `SyntaxFactory.ParseExpression(...)` as the default expression gateway.

```csharp
var parseOptions = new CSharpParseOptions(languageVersion: LanguageVersion.Preview);
var expr = SyntaxFactory.ParseExpression(
    text: "customer.Total + tax",
    options: parseOptions,
    consumeFullText: true);
```

Why this matters:

- `consumeFullText: true` catches trailing tokens instead of silently accepting partial parses.
- We should store parse options and strictness in provenance so replay behavior stays deterministic.

## Step B: Parse a full snippet when needed

When expression parsing is not enough (e.g., script-like context), use `CSharpSyntaxTree.ParseText(...)`.

```csharp
var text = SourceText.From("customer.Total + tax");
var tree = CSharpSyntaxTree.ParseText(
    text,
    options: parseOptions.WithKind(SourceCodeKind.Script),
    path: "debugger://expr/1");
```

Why this matters:

- `path` and parse options influence diagnostics and should be treated as deterministic input fields.
- `SourceText` carries encoding/checksum metadata that can help provenance and replay fidelity.

## Step C: Pick compilation mode deliberately

Roslyn has two relevant creation patterns:

- `CSharpCompilation.Create(...)` for standard compilation behavior.
- `CSharpCompilation.CreateScriptCompilation(...)` for submission/script behavior.

```csharp
var compilation = CSharpCompilation.Create(
    assemblyName: "ExpressionFrontEnd",
    syntaxTrees: new[] { tree },
    references: references,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
```

```csharp
var scriptCompilation = CSharpCompilation.CreateScriptCompilation(
    assemblyName: "ExpressionFrontEnd.Script",
    syntaxTree: tree,
    references: references,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
```

Project guidance:

- Keep script mode policy-controlled and explicit.
- Capture which mode was used in front-end output metadata.

## Step D: Get semantic info (advisory, not authoritative)

```csharp
var model = compilation.GetSemanticModel(tree);
var typeInfo = model.GetTypeInfo(expr);
var symbolInfo = model.GetSymbolInfo(expr);
```

Important nuance for adapter design:

- `GetSemanticModel(...)` requires that the tree is part of the compilation; otherwise Roslyn throws.
- Semantic results should be treated as **advisory** when dump-runtime facts disagree.

## Step E: Normalize diagnostics for our contracts

Roslyn diagnostics are rich but compiler-oriented. Our adapter should project them into stable, product-facing categories.

Suggested normalization buckets:

- Parse failure / malformed expression
- Missing member/type/reference
- Ambiguous bind result
- Unsupported language feature for current policy
- Internal front-end failure (guardrail category)

Each normalized result should include:

- confidence,
- evidence (diagnostic IDs, spans),
- fallback decision,
- deterministic input fingerprint fields (options, mode, references).

## 4) Why we should not couple to parser internals

`LanguageParser` is intentionally complex and optimized for full C# recovery scenarios. It tracks many terminator states and reset points to preserve parse resilience.

Design takeaway: depend on public parse outcomes and diagnostics, not parser-internal recovery behavior. Internal recovery can shift between Roslyn versions and should not become part of our contract semantics.

## 5) Minimal adapter contract shape (draft)

Use this as a practical starting point:

- **Input**
  - expression text
  - language kind/options
  - compilation mode (standard/script)
  - reference set identity
  - strictness flags (`consumeFullText`, policy switches)
- **Output**
  - syntax success + normalized diagnostics
  - optional semantic payload (symbol/type candidates)
  - confidence and provenance bundle
  - explicit fallback recommendation when semantic confidence is low

Keep Roslyn object types inside adapter boundaries. Downstream interpreter layers should consume project-defined DTOs/contracts.

## 6) Common beginner pitfalls (and project defaults)

1. **Pitfall:** treating semantic model results as runtime truth.
   **Default:** treat semantic facts as hints unless runtime metadata confirms them.
2. **Pitfall:** forgetting to include tree in compilation before asking for semantic model.  
   **Default:** enforce a single builder path that creates tree + compilation together.
3. **Pitfall:** changing parse options between requests without tracking it.  
   **Default:** always include options/mode/reference identity in provenance.
4. **Pitfall:** growing scope into emit/codegen APIs.  
   **Default:** block non-front-end Roslyn APIs in adapter review checklists unless explicitly approved.

## 7) Suggested onboarding exercise (1-2 hours)

1. Build a tiny spike that parses 10 debugger-style expressions with `ParseExpression(...)`.
2. Re-run the same corpus with script-mode tree + `CreateScriptCompilation(...)`.
3. Compare diagnostics and symbol results between modes.
4. Produce a short evidence table:
   - expression,
   - mode,
   - normalized result,
   - confidence,
   - fallback action.

This gives immediate intuition for why our architecture treats Roslyn as a bounded, optional semantic assistant.

## 8) Where to read next

- `docs/lib/roslyn/usage-notes.md` for decision-oriented boundaries and risks.
- `docs/lib/source-review-deep-dive.md` for cross-library comparisons.
- `docs/proposals/architecture/semantic-modeling-proposal.md` for how semantic enrichment fits interpreter policy.


---

## 9) Source-backed deep dive: what matters beyond basic parse/bind samples

A deeper read of the Roslyn snapshot clarifies a few boundary conditions we should encode in adapter contracts.

### `SyntaxFactory.ParseExpression` always goes through lexer + parser, with optional full-text enforcement

`ParseExpression(...)` builds a lexer/parser pair and optionally calls `ConsumeUnexpectedTokens`. This means strictness is not just an external policy flag; it directly changes produced syntax/diagnostic behavior.

Adapter guidance:

- model strictness (`consumeFullText`) as a required input field,
- persist parse options + strictness in provenance,
- keep a stable normalization for trailing-token conditions.

### Script compilation differs by construction, not just by option toggles

`CreateScriptCompilation(...)` validates submission parameters and uses submission-oriented defaults (including references superseding lower versions). It is not equivalent to calling `Create(...)` with a few option tweaks.

Adapter guidance:

- treat "script vs regular" as a mode switch with its own policy review,
- include mode in cache keys and result fingerprints,
- avoid implicit auto-switching between modes based on heuristic text detection.

### Semantic model acquisition has hard preconditions

`GetSemanticModel(...)` throws when the syntax tree is not part of the compilation. This is a predictable failure mode that should be normalized rather than leaked as raw exceptions.

Adapter guidance:

- enforce one construction pipeline that binds parse tree + compilation together,
- map tree-membership failures to internal adapter error categories,
- avoid passing semantic model objects across layers.

### Parser internals (`LanguageParser`) optimize recovery, not debugger semantics

`LanguageParser` contains extensive terminator state handling and recovery heuristics. These are implementation details that may evolve between Roslyn versions.

Adapter guidance:

- rely on public syntax + diagnostics + semantic outputs only,
- do not encode parser-internal recovery assumptions in product behavior,
- maintain golden tests over normalized output, not Roslyn internal state.

## 10) Advanced onboarding lab (2-3 hours)

1. Parse the same corpus in strict and non-strict modes.
2. Build both regular and script compilations for the corpus.
3. Collect semantic diagnostics and primary symbol/type hints.
4. Normalize each result to project DTOs and compare:
   - syntax status,
   - semantic confidence,
   - fallback recommendations,
   - provenance fingerprint fields.
5. Document one policy recommendation in `docs/lib/roslyn/usage-notes.md` based on observed deltas.

## 10) Additional source-backed findings from `lib/roslyn` review

A wider scan of Roslyn sources adds a few practical tutorial points that are easy to miss when using only top-level APIs.

### Tree path and source identity are not cosmetic

`CSharpSyntaxTree.ParseText(...)` overloads accept `path`, `SourceText`, and encoding/hash inputs that can affect diagnostics and location mapping.

Practical guidance:

- include `path` and source identity in deterministic request fingerprints,
- avoid per-request random/ephemeral paths unless intentionally modeling transient REPL behavior,
- keep path policy stable across replay scenarios.

### Script compilation enforces submission-specific assumptions

`CreateScriptCompilation(...)` uses submission-oriented options and lower-version reference supersedence behavior.

Practical guidance:

- keep script mode behind explicit policy flags,
- track script/regular mode in cache keys and provenance,
- avoid comparing script and regular semantic results without mode-aware normalization.

### Semantic-model retrieval has strict ownership expectations

Compilation APIs require tree membership for semantic-model access and will throw when invariants are violated.

Practical guidance:

- centralize parse->compilation->semantic flow in one builder path,
- prevent ad-hoc semantic queries from detached trees,
- normalize these invariant failures into internal adapter diagnostics, not user-facing crashes.

### Parser recovery internals are intentionally complex and unstable for contracts

The parser includes extensive recovery/terminator handling to maximize language service resilience.

Practical guidance:

- depend on diagnostics and syntax outputs only,
- keep normalization categories stable even when Roslyn internals evolve,
- include Roslyn package version in provenance to aid drift triage.

## 11) Source-tour checkpoints (new)

Use this source pass before changing Roslyn front-end policy defaults:

1. **Parse-entry strictness behavior**
   - Read `lib/roslyn/src/Compilers/CSharp/Portable/Syntax/SyntaxFactory.cs` around `ParseExpression(...)` and `ParseSyntaxTree(...)` overloads.
2. **Compilation-mode semantics**
   - Read `lib/roslyn/src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs` around `Create(...)`, `CreateScriptCompilation(...)`, and script submission validation.
3. **Semantic-model ownership constraints**
   - In the same compilation file, trace `GetSemanticModel(...)` invariants and failure conditions for trees not in the compilation.
4. **Recovery complexity boundary**
   - Read `lib/roslyn/src/Compilers/CSharp/Portable/Parser/LanguageParser.cs` terminator-state/recovery pathways as a reminder to avoid parser-internal coupling.

Expected design artifact:

- one normalization matrix comparing strict vs non-strict parse + regular vs script compilation modes over the same expression corpus.
