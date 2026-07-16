# Restricted Dump Query v1 Contract

> **Lifecycle:** Current · **Roadmap:** Active · **Milestone:** W2

This document is the compact normative contract for the first product-facing dump query. It consolidates the W2
language, binding, evidence, and replay rules that were previously distributed across the roadmap, architecture
overview, product proposal, and test strategy. The prototype APIs remain draft-phase interfaces, but an implementation
does not satisfy W2 unless it preserves the behavior below.

W2 closed with a handwritten parser at commit `5bed47100`. W6.2 will replace that implementation with the common
[C# Expression Front-End and Subset-Admission Contract](csharp-expression-front-end-contract-proposal.md). The
accepted W2 v1 shape, default-profile spelling rules, diagnostics, and canonical bytes remain compatibility
requirements; this historical closure record is not evidence that the planned Roslyn front end is implemented.

## 1) Product question and scope

W2 answers one deliberately small question:

> Given one host-named, exactly selected non-null heap object in one immutable dump, what is the value of one exact
> instance field, optionally replacing an exactly observed null with one bounded literal?

This is a read-only snapshot query. It is neither historical execution nor a counterfactual. The implementation does
not compile a synthetic method or execute target IL.

The following are intentionally outside v1:

- null-conditional access, member chains, frame/local/argument/static discovery, and exact-null roots;
- properties or getters, including auto-property backing-field inference;
- calls, constructors, indexers, arrays, reflection, conversions, arithmetic, comparison, Boolean operators,
  assignments, statements, LINQ, loops, and implicit assembly loading; and
- every interpreter execution entry point.

Those forms remain later, scenario-gated Phase 1 or research work. Their omission is a closed language boundary, not
an invitation to approximate them.

## 2) W2 v1 admitted syntax-tree shape

The semantic notation is:

```text
query       := whitespace root whitespace "." whitespace field whitespace
               [ "??" whitespace literal whitespace ] end
root        := identifier
field       := identifier
identifier  := ascii-letter-or-underscore { ascii-letter-or-underscore-or-digit }
literal     := "null" | signed-decimal-int32 | bounded-string
```

Root and field comparison is ordinal and case-sensitive. Whitespace is accepted only at the positions shown. String
literals admit the explicitly implemented escape set; they are not the complete C# string-literal language.

At W2 closure this notation was also the handwritten grammar. Under the planned common front end, Roslyn first parses
the complete bounded C# expression and the frozen W2 profile then recognizes only the equivalent simple-member-access
tree with an optional outer coalesce. Its ASCII identifier, trivia-position, signed-decimal, and ordinary-string
spelling predicates remain profile admission rules, not a second tokenizer or recovery implementation.

The front end applies deterministic caps to the expression, each decoded identifier, the decoded string literal, and
its project-owned tree traversal. Roslyn-invalid input is `Invalid`; Roslyn-valid syntax outside the admitted W2 shape
is `Unsupported`. Both have stable, payload-omitting project diagnostic codes. In particular, `?.`, a second member
hop, calls, indexing, and trailing operators are never partially evaluated.

## 3) Staged pipeline

One evaluation has four explicit stages:

1. **Parse** uses the pinned complete C# expression front end and produces a valid bounded tree or stable invalid
   input result.
2. **Admit** recognizes the versioned W2 tree and projects an immutable project-owned syntax shape, or returns stable
   unsupported syntax.
3. **Bind** consumes a typed host root binding, verifies its immutable snapshot identity, selects the requested outer
   instance field exactly once, and classifies the admitted field/coalescing combination.
4. **Plan** freezes the root identity, selected field descriptor, value decoder, optional coalescing literal, reached
   policy bounds, and a canonical v1 plan identity.
5. **Evaluate** reads through the already selected descriptor. It must not repeat outer member lookup or parse.

The convenience parse-and-evaluate API is only composition over these stages. The explicit prepared plan is reusable
against the same immutable snapshot identity and owner identity. A foreign snapshot, owner, method table, or field
descriptor is a typed conflict, never an unchecked read.

## 4) Host root binding

The host supplies one ordinal name and one typed binding state:

| Root state | Meaning | May produce a plan? |
|---|---|---:|
| Exact object | One object is proven selected in the immutable snapshot. | Yes |
| Exhaustive absence | An exact bounded search completed with no match. | No |
| Partial | Root search or required evidence was truncated or incomplete. | No |
| Unavailable | Required root evidence does not exist. | No |
| Conflict | Candidates are ambiguous or evidence identities disagree. | No |
| Invalid | Captured root evidence violates a supported invariant. | No |

A partial search never chooses a retained candidate merely because one happens to be present. An exact search with
multiple candidates is conflict/ambiguity. A search-backed binding retains the exact ordinal type selector, adapter
search status and issue, handles scanned and scan cap, match cap, retained-match count, match-limit flag, ordered
counted reads, and the bounds actually applied by that search. Failed preparation/evaluation results retain the reads
and bounds plus a canonical root-selection policy provenance identity that hashes the selector/search state. Distinct
absent or partial predicates and search dispositions therefore cannot alias even though the result exposes the hash
rather than the selector and counters directly.

Exact absence and exact null are different concepts. W2 represents the former; an exact-null root is not admitted in
v1. Missing, partial, conflicting, or invalid root evidence never becomes null for the purpose of `??`.

## 5) Bound plan and canonical identity

The bound plan is immutable and object-specific. Its canonical v1 projection is injective over at least:

- grammar version;
- root and field identifiers;
- selected snapshot and owner identity;
- the complete outer field descriptor and, for `Nullable<Int32>`, both child metadata tokens, addresses, and sizes;
- admitted field value kind; and
- optional literal kind and exact literal value.

The plan exposes a SHA-256 fingerprint of that projection. Product result provenance includes the plan identity, so
two requests that happen to return the same value but contain different fallback literals do not have the same
machine-readable explanation.

Descriptor admission rejects duplicate nullable child tokens, overlapping storage, storage outside the outer field,
and address/extent arithmetic overflow. Evaluation revalidates snapshot, owner address, owner method table, and
descriptor ownership before memory reads; a forged same-snapshot descriptor is a conflict rather than a read attempt.

Canonical plan/result projections can contain expression literals or target-derived values. They are test/replay
artifacts and are not suitable for diagnostic display display strings.

## 6) Admitted types and coalescing

The v1 value union is exact null, signed `Int32`, string, or an explicitly partial bounded string prefix.

| Bound field | No `??` | `?? null` | `?? Int32` | `?? string` |
|---|---|---|---|---|
| `Int32` | Exact `Int32` | Invalid type combination | Invalid type combination | Invalid type combination |
| `Nullable<Int32>` | Exact `Int32` or exact null | Preserve value/null | Use fallback only for exact null | Invalid type combination |
| `String` | Exact string/null or partial prefix | Preserve value/null | Invalid type combination | Use fallback only for exact null |
| Any other type | Unsupported field type | Unsupported field type | Unsupported field type | Unsupported field type |

An exact non-null left operand does not select the fallback. Partial or unavailable left evidence never selects the
fallback. A partial string prefix remains partial even when a fallback literal is present.

`Nullable<Int32>` decoding validates its runtime layout and reads its discriminator and, only on the non-null path,
its payload through counted dump-memory operations. A missing discriminator or payload cannot fabricate null, zero,
or a scalar answer.

## 7) Result truth and provenance

Every product query result uses the common multi-axis envelope:

- semantic mode: `DerivedQuery` for the product-level root/member query; the underlying adapter field reads remain
  independently available as `Observation` results;
- completion and answer completeness;
- evidence status;
- effect status, always `None` for v1;
- optional value;
- evidence source and explicit snapshot/module identity availability;
- fallback status and only the deterministic bounds whose guarded operations were reached; and
- ordered provenance plus stable diagnostics.

The product result is `DerivedQuery` because it applies an admitted host root/member plan over one or more runtime and
memory observations, even when the final scalar decoder itself is a direct observation. It never uses historical,
executed, stepped, returned, or counterfactual language.

Successful evaluation provenance identifies the canonical plan, root-selection evidence, runtime root/field
structures, counted value reads, and any reached coalescing transformation in deterministic order. A failure retains
all explanation available before its stopping stage. Configured policies for unvisited paths are absent.

Successfully admitted requests have canonical request identity. Bounded invalid or unsupported input retains a
canonical raw-input identity where the product issuance rules permit it; input rejected for exceeding the expression
cap deliberately retains no raw identity. Successful preparation additionally supplies plan identity. Roslyn nodes,
numeric syntax-kind values, and diagnostics never enter these identities.

## 8) Diagnostic stages

Stable diagnostics distinguish at least:

- required/oversized/malformed expression input;
- root-name mismatch and unsupported syntax;
- exhaustive, partial, unavailable, conflicting, or invalid root binding;
- missing, ambiguous, foreign, or unsupported field binding;
- incompatible coalescing types; and
- partial, unavailable, conflicting, or invalid value evidence.

Diagnostic text is payload-omitting and deterministic. Diagnostic codes, ordered provenance, applied bounds, plan
identity, and all result axes participate in canonical replay.

## 9) W2 scenario and replay gate

The checked-in W2 v1 gate is a versioned corpus of 22 cases over 20 distinct expression texts covering:

- direct exact `Int32` and string observations;
- exact-null string and nullable-`Int32` observations;
- selected and unselected compatible fallbacks;
- `?? null`;
- exhaustive/unavailable root binding;
- missing and wrong-case fields;
- unsupported field type and incompatible coalescing;
- rejected `?.` and invalid syntax; and
- a partial bounded string that is not reclassified as null.

For every corpus case, the test constructs root and policy inputs explicitly, evaluates repeatedly in one session,
and compares the complete canonical result byte sequence and result SHA-256. It then closes and reopens the same dump,
rediscovers and rebinds the root, and reproduces all 22 results; the 13 cases whose preparation succeeds additionally
reproduce the plan's canonical projection string and plan SHA-256. The corpus asserts exact result axes, diagnostics,
module/source context, independently expected path bounds, full ordered provenance payload, and value-read geometry;
value-only equality, replay-only equality, or one representative query does not satisfy this gate.

This gate is satisfied for the milestone-selected W2 v1 scope at exact closure commit `5bed47100`; [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs.

## 10) Expansion rule

W2 v1 completed under the implementation recorded above. New admitted syntax requires a concrete product question
plus explicit tree shape, evidence, type, bound, diagnostic, identity, and replay behavior. The complete Roslyn parser
does not implicitly open W3 or any broader expression feature; binding and evaluation remain versioned subsets.
