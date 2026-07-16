# C# Expression Front-End and Subset-Admission Contract

> **Lifecycle:** Current · **Roadmap:** Active · **Milestone:** W6
>
> **Decision:** use the complete Roslyn C# expression parser once per bounded request, then admit only explicitly
> versioned syntax-tree shapes into project-owned binding and evaluation plans.
>
> **Implementation status:** design only. W6.1 remains the next executable checkpoint; the front-end migration is
> W6.2 and is estimated at `~1K LOC` including its compatibility and conformance tests.

## 1) Decision

The project will not extend its handwritten C# lexer and parser. Roslyn owns C# lexical analysis, expression grammar,
error recovery, token values, and syntax-tree construction. The project owns every product decision after parsing:

- which versioned expression profiles are enabled;
- which well-formed syntax-tree shapes each profile admits;
- how identifiers and literal values are projected into project-owned immutable nodes;
- how those nodes bind to dump and metadata evidence;
- which operations may be evaluated;
- resource bounds, stable product diagnostics, canonical identities, and replay behavior; and
- the distinction between syntactic validity, product admission, evidence availability, and evaluation outcome.

The governing pipeline is:

```text
bounded raw text
    -> one pinned Roslyn expression parse
    -> structural integrity and complexity checks
    -> ordered versioned tree-shape admission
    -> project-owned immutable expression descriptor
    -> evidence binding and immutable plan preparation
    -> bounded evaluation
```

Parsing a complete C# expression language is not a commitment to bind or evaluate the complete language. Most valid
C# expression trees remain unsupported. This is intentional: syntax correctness is delegated to the mature compiler
front end while the product's semantic and evidence boundary stays narrow, reviewable, and scenario-driven.

## 2) Why this replaces parser growth

The W2 parser currently owns identifier tokenization, whitespace rules, punctuation, signed-decimal parsing, string
escape decoding, recovery, bounds, and diagnostics. W5 then recognizes its one method expression with an exact raw-
text comparison after the W2 parser rejects it. Extending that mechanism for conditional access, nested member
access, additional literal spellings, parentheses, invocations, or later C# syntax would duplicate compiler behavior
incrementally and create two sources of syntactic truth.

Roslyn already supplies the required expression grammar and lossless trees. Using it gives the project one answer to
questions such as whether a token is an identifier, whether a string is terminated, how a raw or verbatim literal is
decoded, where a conditional-access receiver ends, and whether trailing tokens were consumed. Project code can then
concentrate on the questions unique to this product: whether the tree shape is admitted, whether the dump contains
exact evidence for its members, and whether a bounded read can answer it honestly.

This decision also removes a current control-flow accident. W5 admission must not depend on a particular W2
diagnostic, and W6 admission must not depend on either earlier recognizer failing in a particular way. All enabled
recognizers receive the same valid parsed tree and participate in one explicit precedence table.

## 3) Responsibility boundary

### 3.1 Roslyn owns

The pinned Roslyn front end owns only:

- C# tokenization and trivia;
- the complete standalone expression grammar for the selected C# language version;
- syntax diagnostics and recovery representation;
- lossless source spans and token text; and
- compiler-defined token values such as decoded identifiers, strings, and numeric literal values.

### 3.2 The project owns

The project remains authoritative for:

- raw-input admission and the pre-parse character limit;
- rejection of recovered, missing, skipped, directive-bearing, or over-complex trees;
- normalized `Invalid` versus `Unsupported` classification;
- profile selection and recognizer precedence;
- identifier comparison, literal-type admission, and decoded-value limits;
- dump root, metadata member, runtime object, and storage binding;
- immutable plans and all value reads;
- product diagnostic codes and payload-omitting messages;
- canonical request, plan, result, and replay formats; and
- dependency-upgrade review and conformance baselines.

### 3.3 Roslyn does not own

W6 does not create a `Compilation`, request a `SemanticModel`, resolve symbols, run analyzers, execute scripts, emit an
assembly, or compile a synthetic method. C# source semantics are not used as a substitute for dump evidence. The
runtime module, counted metadata, exact root selection, declared members, and memory observations remain the binding
universe.

Roslyn syntax nodes, tokens, trivia, diagnostics, numeric `RawKind` values, and object identities must not cross the
front-end boundary. They are temporary implementation details used by internal recognizers. Downstream projects and
canonical codecs consume only project-owned immutable descriptors.

## 4) Dependency and parser profile

### 4.1 Package placement

W6.2 adds one direct package dependency:

```text
Microsoft.CodeAnalysis.CSharp 5.3.0
```

The version is centrally pinned in `Directory.Packages.props` and locked by the normal transitive project lock graph.
It is placed only on `Interpreter.Product.DumpQuery`, which owns the expression front-end boundary. The dependency is
not added to the interpreter kernel, metadata abstractions, dump host abstractions, or domain projects. Dependent
executables receive the runtime assets through ordinary project dependency flow.

The initial `5.3.0` pin matches the Roslyn train shipped by the repository's pinned .NET SDK 10.0.201. Workspaces,
Scripting, Features, and an explicit Common package reference are unnecessary. A package upgrade is a parser-profile
change with its own corpus diff and review; it is never a routine floating dependency update.

### 4.2 Exact parse operation

The version-one profile is conceptually equivalent to:

```csharp
private static readonly CSharpParseOptions Options = new(
    languageVersion: LanguageVersion.CSharp14,
    documentationMode: DocumentationMode.None,
    kind: SourceCodeKind.Regular,
    preprocessorSymbols: Array.Empty<string>());

ExpressionSyntax syntax = SyntaxFactory.ParseExpression(
    text,
    offset: 0,
    options: Options,
    consumeFullText: true);
```

`SyntaxFactory.ParseExpression` is used directly. The implementation must not wrap the expression in a fabricated
method or compilation unit, because doing so changes recovery, spans, context, and accepted constructs. Script source
kind is not used. `LanguageVersion.Latest` and `Preview` are prohibited because they make behavior depend on the
installed package rather than the declared profile.

The exact profile is named `RoslynCSharpExpressionV1` and freezes:

| Property | Version-one value |
|---|---|
| Package | `Microsoft.CodeAnalysis.CSharp/5.3.0` |
| Language | `CSharp14` |
| Documentation mode | `None` |
| Source kind | `Regular` |
| Preprocessor symbols | Empty |
| Feature flags | Empty |
| Offset | `0` |
| Full-text consumption | Required |
| Expression length | 512 UTF-16 code units |
| Nodes plus tokens | 256 |
| Syntax depth | 64 |
| Identifier value | 64 UTF-16 code units |
| Decoded string value | 256 UTF-16 code units |

The implementation may choose lower structural limits if fixture evidence requires them, but changing a frozen value
before W6.2 closure requires updating this contract and the profile identity together.

## 5) Parse and integrity contract

The front end applies checks in this order:

1. Reject a null, empty, or whitespace-only expression using the existing required-expression product code.
2. Apply the 512-UTF-16-code-unit expression limit before calling Roslyn. Oversized input receives no retained raw-
   expression identity, preserving the existing bounded-input rule.
3. Parse once with the exact profile in section 4.
4. Reject syntax diagnostics, missing nodes or tokens, skipped text, or incomplete full-text coverage as `Invalid`.
5. Reject directive or disabled-text structured trivia as `Unsupported`; expression requests do not establish a
   preprocessor context.
6. Require `FullSpan` to cover the complete input and `ToFullString()` to equal the input ordinally.
7. Apply the node-plus-token, maximum-depth, identifier-value, and decoded-string limits before profile recognition.
8. Run only the recognizers enabled by the selected product language profile.

The raw length limit is the hard pre-parse work bound because `ParseExpression` has no cancellation-token overload.
The post-parse structural limits bound project traversal and projection; they are not misrepresented as limits on
Roslyn's already completed parse.

Comments and ordinary whitespace are lossless trivia and are syntactically valid. A new profile admits or rejects a
tree based on its declared node and token policy rather than an accidental whitespace scanner. The frozen legacy
profile may retain stricter spelling/trivia predicates where compatibility requires them, but it still receives a
Roslyn tree and must not implement tokenization, delimiter matching, or syntax recovery.

## 6) Classification model

Parsing and admission are separate observable stages:

| Input state | Product classification | Stable policy |
|---|---|---|
| Missing or whitespace-only | `Invalid` | Existing required-expression code |
| Over 512 UTF-16 code units | `Invalid` | Existing expression-limit code; no raw identity |
| Roslyn reports an error or recovery artifact | `Invalid` | `QUERY_CSHARP_SYNTAX_INVALID` |
| Tree exceeds a structural or decoded-value bound | `Invalid` | Stage-specific stable limit code |
| Tree contains directives or disabled text | `Unsupported` | `QUERY_CSHARP_DIRECTIVE_UNSUPPORTED` |
| Valid tree is outside every enabled shape profile | `Unsupported` | `QUERY_SYNTAX_UNSUPPORTED` |
| Tree matches one enabled shape | `Accepted` | Project-owned descriptor issued |
| More than one recognizer claims the same tree unexpectedly | `Invalid` | Internal profile-overlap code; no arbitrary winner |

Localized Roslyn messages never become product messages or canonical bytes. Roslyn diagnostic IDs and spans are not
the stable product contract. If they are retained for local developer evidence, the implementation keeps at most
eight entries ordered by span start, span length, severity, and ID, and excludes them from canonical identity.

The public classification result continues to expose payload-omitting project codes and messages. The existing
result replay codec includes those project messages, so migration tests must freeze them deliberately rather than
copying compiler text.

## 7) Product-owned parsed descriptor

An admitted expression is immediately projected into a small immutable discriminated shape. The illustrative model is:

```text
AdmittedExpression
  profile identity
  original bounded text
  decoded root name
  operation
    DirectMember(member)
    EmptyInstanceInvocation(method)
    DirectMemberChain(referenceMember, terminalMember)
    ConditionalMemberChain(referenceMember, terminalMember)
  optional typed literal
    Null
    Int32(value)
    String(value)
  reached front-end bounds
```

This is a design shape, not a frozen public API. It contains no syntax node, syntax token, span object, diagnostic,
semantic symbol, or compilation. Names and literal values are copied into project-owned strings and scalars. Every
public prototype type or method introduced during implementation requires complete XML documentation and an explicit
draft-phase caveat.

Classification returns this descriptor internally with the public request. Preparation consumes it directly. The
W5 evaluator must not call the parser again, and the W2 convenience entry point is only composition over parse,
recognize, prepare, and evaluate.

## 8) Versioned tree-shape admission

### 8.1 Recognizer precedence

One parse is followed by an explicit, profile-owned recognizer table:

| Order | Recognizer | Enabled in frozen W5 profile | Enabled in `FixedDepthMemberChainV1` |
|---:|---|---:|---:|
| 1 | W2 direct member with optional coalesce | Yes | Yes |
| 2 | W5 exact empty instance invocation | Yes | Yes |
| 3 | W6 two-member direct or conditional chain | No | Yes |

Precedence is a compatibility policy, not a sequence of reparses. Recognizers either return no match or one complete
project descriptor. They do not return parser errors, acquire metadata, read memory, or invoke one another.

### 8.2 W2 direct-member shape

The semantic W2 shape is either:

```text
SimpleMemberAccess(
    IdentifierName(root),
    IdentifierName(member))
```

or an outer coalesce whose left operand is that shape and whose right operand is an admitted literal. The frozen W2
compatibility profile retains its existing ASCII identifier, trivia-position, ordinary-string escape, and signed-
decimal spelling predicates so existing classifications, codes, and canonical bytes remain stable. Those predicates
inspect an already valid tree and its source tokens; they do not constitute a second parser.

New profiles may deliberately use the complete C# lexical spellings represented by the same admitted nodes. Such a
change is versioned and tested rather than leaking in through a package update.

### 8.3 W5 invocation shape

The W5 shape is:

```text
Invocation(
    SimpleMemberAccess(
        IdentifierName(root),
        IdentifierName("GetMarkerSummary")),
    EmptyArgumentList)
```

The frozen W5 profile retains its exact raw spelling and trivia policy. Recognition is direct tree inspection; it is
no longer a string comparison conditional on W2 returning `QUERY_SYNTAX_UNSUPPORTED`.

### 8.4 W6 direct chain shape

The direct two-member shape is:

```text
SimpleMemberAccess(
    SimpleMemberAccess(
        IdentifierName(root),
        IdentifierName(referenceMember)),
    IdentifierName(terminalMember))
```

An optional outer coalesce may wrap this exact left operand. A third member, generic name, alias-qualified name,
element access, invocation, postfix operator, assignment, or other node is a valid-but-unsupported tree when Roslyn
accepts it.

### 8.5 W6 conditional chain shape

The conditional terminal hop is:

```text
ConditionalAccess(
    SimpleMemberAccess(
        IdentifierName(root),
        IdentifierName(referenceMember)),
    MemberBinding(IdentifierName(terminalMember)))
```

An optional outer coalesce may wrap this exact left operand. Conditional access on the root, repeated conditional
access, an invocation or indexer in `WhenNotNull`, and deeper chains remain unsupported.

### 8.6 Parentheses and other transparent-looking syntax

Parenthesized expressions are distinct syntax-tree shapes and are not silently unwrapped in version one. Neither are
null-forgiving, checked, cast, or suppression nodes. They may be semantically transparent in some C# contexts, but
admitting them is a product-language decision with identity and diagnostic consequences. They remain unsupported
until a profile explicitly adds a normalization rule and its tests.

## 9) Identifier and literal projection

### 9.1 Identifiers

New-profile semantic comparison uses `SyntaxToken.ValueText`, ordinal and case-sensitive. Raw spelling remains in the
bounded request identity. This permits the parser to understand escaped and Unicode identifiers without project code
recreating C# identifier rules. Whether a particular profile admits those spellings remains explicit.

Only `IdentifierNameSyntax` is admitted in W2/W5/W6 version-one shapes. Generic names, predefined-type keywords,
qualified aliases, and other name nodes remain valid but unsupported. Decoded identifier values longer than 64 UTF-
16 code units fail the deterministic identifier bound before recognition.

### 9.2 Strings

New profiles project an ordinary C# string-literal node only when the compiler token value is a `string`. The copied
decoded value must not exceed 256 UTF-16 code units. Ordinary escaped, verbatim, and raw string spellings can therefore
share one semantic value where a profile enables them. Interpolated strings, UTF-8 string literals, character
literals, and concatenations are different trees or value types and remain unsupported.

The frozen W2 compatibility profile retains its narrower existing ordinary-string spelling policy until a deliberate
profile change. No recognizer decodes escape sequences itself; Roslyn's token value is authoritative.

### 9.3 Integers

An admitted integer literal must project to one exact signed `Int32` value without a semantic model. New profiles may
accept a numeric literal token whose compiler value is `Int32`, plus the explicit prefix-unary cases needed for `+n`,
`-n`, and `-2147483648`. Numeric suffixes whose compiler token value has another type, floating-point literals,
unchecked wrapping, casts, constant expressions, and arithmetic remain unsupported even when their eventual runtime
value could fit in `Int32`.

Hexadecimal, binary, digit-separated, and decimal spellings are admitted only if the selected profile enables their
token spelling and the projection rule above yields `Int32`. The frozen W2 profile continues to admit only its
existing signed-decimal spellings.

## 10) Canonical identity and versioning

Canonical artifacts encode project semantics, never Roslyn implementation objects. In particular, a codec must not
write:

- numeric `SyntaxKind` or `RawKind` values;
- syntax-tree serialization, normalized source, or Roslyn object hashes;
- localized compiler messages;
- compiler diagnostic arrays; or
- incidental node/token enumeration order.

The new front-end profile has a canonical descriptor containing the explicit values in section 4.2 and a project-
owned schema version. A W6 request encodes that profile identity, its selected admission profile, exact bounded raw
text, decoded semantic descriptor, reached bounds, root binding, and policy under a new tagged schema.

Previously admitted W2 and W5 requests retain their existing canonical byte encodings. No false/default front-end
field is appended to a legacy codec. Their decoded semantic names and literal payloads remain identical after the
migration. Historical checked-in reports remain commit-scoped evidence and are not rewritten to pretend they were
produced by Roslyn.

The initial migration must also preserve the existing default-profile classification, diagnostic, and canonical
goldens. If an unrecorded edge case cannot be preserved without recreating parser behavior, the project versions that
admission behavior explicitly; it does not keep two general expression parsers indefinitely or hide the difference.

Changing the Roslyn package, language version, parse options, full-text policy, a structural limit, a spelling policy,
or an admitted tree pattern requires:

1. a new or explicitly revised front-end/admission profile identity;
2. a complete three-bucket corpus diff;
3. review of every classification and normalized diagnostic change;
4. intentional canonical-golden updates only where the profile changed; and
5. same-process, fresh-process, and applicable dump-reopen replay.

## 11) Test and conformance contract

### 11.1 Three-bucket corpus

W6.2 adds a source-controlled corpus with three independent expected buckets:

1. **Valid and admitted** — the exact W2, W5, and opt-in W6 shapes, including their boundary literal values, trivia
   policies, casing, direct/conditional operators, and coalesce variants.
2. **Valid but unsupported** — syntactically rich expressions that Roslyn accepts but every enabled recognizer must
   reject without metadata or memory access.
3. **Invalid** — malformed near-neighbors that require diagnostics, missing tokens, skipped text, incomplete
   consumption, or an exceeded front-end bound.

The valid-but-unsupported bucket must be complex enough to prove that the complete expression parser is active. It
includes, at minimum:

```csharp
root is { Failure.Code: "request-failed", CurrentRequest: { Status: not null } }
root.Items.Where(static item => item?.Code is not null).Select(item => $"{item.Code}:{item.Count}")
((IReadOnlyDictionary<string, RequestState>)root.States)[key]?.Failure?.Code ?? fallback
from request in root.Requests where request.Failure is not null select request.Failure.Code
root switch { { Progress.CompletedPartitions: > 0 } value => value.Progress.State, _ => "idle" }
```

Each has a malformed neighbor. Tests first establish that Roslyn reports no syntax error for the valid row, then
assert project `Unsupported`, zero evidence-capability calls, and stable product diagnostics. They do not evaluate or
bind these examples.

### 11.2 Compatibility corpus

The migration retains and strengthens:

- all W2 parser/admission diagnostic rows;
- the 22-case W2 same-session and dump-reopen canonical corpus;
- W2 plan-identity cases, including distinct unpaired-surrogate values;
- W5 exact-method spelling, bounds, request identity, and classification cases;
- W5 facade routing, method-acquisition guards, and foreign-snapshot behavior;
- headless W5 machine/human report replay; and
- the four W5.5b member-chain rows remaining unsupported in the frozen default profile.

Differential tests run the current handwritten parser as a temporary test oracle during migration. Once every frozen
compatibility case agrees, production routing switches atomically and the handwritten `Reader`, literal decoder, and
W5 fallback string recognizer are deleted. The legacy implementation is not retained as an alternative production
path.

### 11.3 Front-end invariants

Focused tests prove:

- the exact package and parse-option profile;
- full-text consumption and lossless raw-text roundtrip;
- invalid versus valid-but-unsupported normalization;
- missing/skipped/directive/disabled-text handling;
- expression, node/token, depth, identifier, and decoded-string limits;
- `-2147483648`, overflow, numeric suffix, raw/verbatim/escaped string, Unicode/escaped identifier, comments, and
  contextual-keyword cases;
- classification parses once and preparation never parses;
- no metadata, dump, field, method, body, memory, or execution capability is touched during parse/admission;
- no Roslyn type appears in a public API or a non-front-end assembly; and
- repeated and fresh-process normalized descriptors and canonical artifacts are byte-identical.

Every managed test or helper process remains headless through `eng/Invoke-HeadlessProcess.ps1` or equivalent hidden
no-UI creation.

## 12) Migration sequence

W6.1 remains first. It proves the compiler-emitted terminal property/getter/backing-field shapes selected by W5 and
does not add expression syntax.

W6.2 then performs one coherent front-end migration:

1. centrally pin the package and add the internal parse adapter;
2. freeze parse options, integrity checks, bounds, normalized diagnostics, and profile identity;
3. add the complex three-bucket corpus and package-upgrade baseline;
4. implement W2 and W5 tree recognizers and pass all legacy compatibility/differential goldens;
5. pass one project-owned admitted descriptor from classification to preparation, removing reparsing;
6. implement the opt-in W6 direct/conditional-chain recognizer and request identity;
7. delete the handwritten reader, escape decoder, signed-decimal lexer, and W5 diagnostic-dependent string route; and
8. regenerate locks, run the complete headless compatibility matrix, and push the checkpoint.

W6.3 and later checkpoints consume only the project-owned W6 descriptor. They do not know which parser produced it.

## 13) Explicit exclusions

This decision does not admit:

- general C# name or overload resolution;
- locals, arguments, frames, statics, namespaces, types, aliases, or `using` directives;
- arbitrary properties, indexers, calls, constructors, operators, conversions, assignments, lambdas, queries,
  patterns, tuples, collections, or object creation;
- compilation, scripting, source emission, target-code execution, or dynamic binding;
- implicit assembly or artifact acquisition;
- unbounded graph walking; or
- any evaluation behavior not independently selected by a scenario and frozen in a project-owned plan.

Roslyn can parse these constructs. The product returns `Unsupported` for their valid trees until a later evidence gate
adds one precise shape, binding rule, value domain, resource contract, identity, diagnostic matrix, and executable
test portfolio.

## 14) Completion gate

The decision is implemented only when W6.2 demonstrates all of the following at one pushed commit:

- Roslyn is the sole production expression parser;
- the package, language version, options, and resource profile are pinned;
- W2, W5, and W6 recognition share one parse and emit only project-owned descriptors;
- the handwritten parser and W5 fallback string route are absent from production source;
- valid-admitted, valid-unsupported, and malformed complex corpora pass;
- frozen W2/W5 compatibility classifications and canonical artifacts pass;
- classification performs no evidence acquisition and preparation performs no parsing;
- locked restore, strict Release build, focused tests, full relevant headless lanes, Markdown links, and repository
  guards pass; and
- the exact dependency and realized implementation scale are recorded without implying broader language support.

Until then, the current handwritten parser is implementation truth for W2/W5 and this document is the active design
contract for its W6.2 replacement.

## 15) Primary references

- Microsoft Learn: [`SyntaxFactory.ParseExpression`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.parseexpression?view=roslyn-dotnet-5.0.0)
- Microsoft Learn: [`CSharpParseOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpparseoptions?view=roslyn-dotnet-5.0.0)
- Microsoft Learn: [configure a C# language version](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version)
- NuGet: [`Microsoft.CodeAnalysis.CSharp` 5.3.0](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/5.3.0)

