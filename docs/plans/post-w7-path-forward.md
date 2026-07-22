# Post-W7 Path Forward: W8 Context-Precise Static Binding and Constructed Owners

> **Lifecycle:** Current · **Roadmap:** Active
>
> **Decision:** implement one additive `StaticFieldExpressionV2` profile that preserves W7's context-independent
> fully qualified guarantee while admitting the bounded C# namespace/type/member-binding surface needed for nested
> types, closed constructed generic static owners, scope-precise imports and aliases, `extern alias`, ordinary stored
> fields, metadata literals, and evidence-qualified `using static` bare-field roots. Exact reference values continue
> through the unchanged W2/W6 suffix evaluators.
>
> **Implementation status:** W8.1 is implemented and locally validated through exact source baseline `220be94b4`;
> its authoritative branch record is the [W8.1 Physical-Truth Disposition](w8-1-physical-truth-disposition.md). W8.2
> is active. The immutable expression-contract foundation, detached frame-value syntax contract, one shared bounded
> Core ECMA signature grammar, its Product event adapter, and the caller-supplied selected-method lexical evidence
> envelope have landed. Checkpoint `5fd87a3e5` adds exact metadata source ends and token catalogs, raw and
> role-classified TypeDefs, a complete TypeSpec graph, exact FieldSig identity, GenericParam
> declaration/catalog/owner-set/binding ledgers, interface/constraint edge aggregates, provisional construction
> classification, and Nullable construction preservation. These proof objects are not yet mandatory consumer inputs.
> A host-owned lexical producer, downstream authority integration, the remaining V2 contract families, binder,
> runtime/storage mapping, evaluator, report schema, and portfolio result remain.
>
> **Evidence boundary:** W8 is a generated-fixture and meaningful-synthetic prototype milestone. Its planned corpus is
> not representative observation and cannot establish field readiness. W5, W6, and W7 milestone-specific hosted
> dispositions do not carry forward.

## 1) Executive decision

W8 turns W7's retained-but-incomplete binding context into a coherent second static-expression profile. The unit of
growth is not another text spelling. It is a bounded, project-owned implementation of the C# name-resolution steps
needed to answer stored or literal static-field questions whose owner is nested, constructed, imported, or exposed as
a bare member by `using static`.

The minimum invariant remains:

```csharp
global::Interpreter.W8TestTarget.GenericSlot<
    global::Interpreter.W8TestTarget.RequestContext>.Current
```

If exactly one loaded module, closed runtime construction, ordinary static field, application domain, and slot match
that expression, it must bind without consulting a stack frame or Portable PDB. Context may provide shorter spellings,
but it may never be required for the fully qualified route and may never redirect it.

The same physical field may also be reached through exact contextual forms such as:

```csharp
RequestSlot.Current
requestlib::Interpreter.W8TestTarget.GenericSlot<
    requestlib::Interpreter.W8TestTarget.RequestContext>.Current
Current.Owner?.Name ?? "none"
global::Interpreter.W8TestTarget.Outer<RequestContext>.Inner<BatchContext>.Count
```

In these examples `RequestSlot` is a type alias whose Portable-PDB target is a `TypeSpec`, `requestlib` is an exact
extern alias, and the bare `Current` is admitted only when an active `using static` fact and a complete lexical-blocker
certificate prove that no higher-precedence local, parameter, type parameter, member, alias, or type owns the name.

W8 uses the pinned complete Roslyn expression parser exactly once. Roslyn owns syntax; project code projects and
binds only the versioned V2 subset. No handwritten generic-name reader, secondary parse, compiler semantic model, or
textual name splitter becomes a product path.

The inclusive umbrella scale is `~100K LOC`. Individual evidence, contract, binder, runtime, product, and portfolio
checkpoints are generally `~10K LOC`; plan/closure checkpoints are `~1K LOC` or `~100 LOC`. These are logarithmic
orders of magnitude and may be revised when implementation exposes the actual volume.

## 2) Why this is the next stage

### 2.1 What W7 selected—and what it did not prove

The W7 portfolio's unique label was `BindingContextPrecision`, with the action name
`AddOneEvidenceBackedFramePdbImportAliasGenericRule`. That is permission to choose a bounded successor along a
frame/PDB/import/alias/generic trajectory. It is not executable proof that a particular generic or alias rule is the
best one:

- the four winning rows are manifest-assigned context failures: missing frame, partial PDB, mismatched PDB identity,
  and competing imports;
- all four stop before a terminal value;
- usefulness and decision-changing counts are predeclared portfolio facts rather than independently derived measures;
  and
- W7 contains no successful constructed-generic alias row.

W8 therefore treats the portfolio result as a direction, not as false precision. The concrete V2 scope is a separate
product and architecture decision based on the owner's inclusive mandate, the debugger-evaluator requirement, and
implementation seams W7 already retained.

### 2.2 Existing seams make one broad binding slice coherent

W7 already retains most of the evidence that a V2 binder needs:

- `DumpPortablePdbImportFact` preserves namespace imports, namespace/type aliases, `using static`, extern-alias facts,
  exact ImportScope coordinates, raw payloads, and TypeDef/TypeRef/TypeSpec tokens;
- the host retains a bounded display-derived projection of runtime generic arguments that is useful probe evidence but
  is not an exact V2 selection source;
- the product metadata model retains nested TypeDef chains, TypeRef resolution, base/interface ancestry, TypeSpec
  bytes, FieldDef signatures, and counted table sizes;
- one shared bounded Core grammar validates TypeSpec, FieldSig, MethodDefSig, and LocalVarSig positions and emits
  parent-indexed events; the Product adapter reconstructs resolved immutable TypeSpec and FieldSig trees, while the
  downstream binder does not yet require those proof identities; and
- the W7 storage/value pipeline already handles ordinary static `Int32`, `String`, `Nullable<Int32>`, exact null, and
  validated object/reference values.

The current gaps are concentrated and architectural rather than speculative:

1. the Roslyn projector rejects `GenericNameSyntax` and non-`global` alias-qualified names;
2. textual qualified candidates do not represent namespace/type partitions or per-segment generic arity;
3. the W7 binder deliberately rejects nested and generic owners and TypeSpec imports;
4. context binding flattens import scopes and loses alias shadowing semantics;
5. runtime mapping treats multiple closed constructions sharing one TypeDef token as ambiguity before comparing their
   ordered type arguments; and
6. no lexical-blocker completeness proof exists for a bare `using static` field root.

Addressing these together produces one durable V2 pipeline. Implementing only one spelling would leave the same
construction, scope, and runtime-identity gaps to be redesigned repeatedly.

### 2.3 Product requirement

A debugger expression evaluator reconstructs context from the selected frame, Portable PDB, module metadata, loader
state, and runtime type/storage observations. It cannot assume source or a Roslyn compilation is available. It must
still answer a non-ambiguous fully qualified static field when contextual artifacts are missing.

W8 therefore has two complementary routes:

- **explicit route:** a fully qualified namespace/type construction binds from counted module metadata and runtime
  construction evidence alone; and
- **contextual route:** a shorter type or bare member spelling additionally requires exact selected-frame and active
  import-scope evidence, plus any completeness facts needed to prove C# precedence.

Both routes converge on the same physical declaration/construction/storage identity. Their source-spelling and
consulted-context provenance remain different.

## 3) Scope and necessary boundaries

### 3.1 Mandatory W8 surface

W8 is complete only when the V2 profile supports all of the following within explicit operation-derived bounds:

1. top-level and nested non-generic type owners;
2. top-level and nested closed constructed generic class, value-type, and interface owners, including multiple arities,
   recursively closed type arguments, and exact stored static fields for every definition kind;
3. fully qualified `global::`, ordinary qualified, current/enclosing namespace, namespace-import, type-alias,
   namespace-alias, and exact extern-alias routes;
4. TypeDef-, TypeRef-, and TypeSpec-backed type aliases, with exact token/module resolution rather than display-text
   trust;
5. current/enclosing type and nested-type lookup plus active ImportScope precedence: aliases apply at their exact
   declaration level, namespace-declaration levels are
   evaluated from innermost to outermost, the first level with viable declarations or imported candidates stops the
   outward search, imports accumulate only within that winning level, and equal physical candidates converge while
   different surviving candidates remain ambiguous;
6. type-qualified static-field lookup on the selected constructed owner and its bounded exact constructed base chain,
   preserving hiding and the field's actual declaring construction;
7. stored ordinary static fields plus metadata literal constants, including primitive, enum, string, null, and every
   valid pinned-compiler literal encoding admitted by the V2 value contract;
8. bare-field roots from current/enclosing type member lookup and from `using static`; ordinary current-type lookup
   follows its constructed ancestry/hiding rule, while the directive contributes only directly declared accessible
   stored or literal fields because it does not import inherited members;
9. validation of every constructed head's substituted metadata generic constraints before runtime mapping;
10. exact substitution of owner `VAR` parameters through admitted field signatures, broadening W7 to all CLI fixed-width
    primitives, native-size integers, floating-point values, enums, strings, admitted nullable values, arrays, and
    constructed references;
11. distinct runtime construction and ordinary static-slot identity for multiple simultaneously loaded instantiations
    sharing one TypeDef/FieldDef pair, while literal constants require no runtime construction or storage;
12. exact constructed generic/interface/base and admitted array assignability for non-null reference validation;
13. direct value results and exact-reference composition through existing direct, conditional, and compatible fallback
    suffix semantics; and
14. exact/null/absent/partial/unavailable/ambiguous/conflict/invalid/unsupported/shadowed outcomes without fallback or
    first-match selection.

The bounded name-resolution behavior follows the official C# specification for
[namespace and type names](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/basic-concepts#78-namespace-and-type-names),
[qualified aliases](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/namespaces#148-qualified-alias-member),
and [`using static`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive#the-static-modifier).
The physical fixture also follows the current C# specification for
[interface static fields](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/interfaces#1942-interface-fields).
W8.1 proves the pinned compiler/runtime emission, definition-kind identity, construction mapping, and distinct slot
behavior; W8.2 consumes those facts without inferring them from source spelling.
Within the contextual route, W8 follows those rules left-to-right. It documents every intentional debugger-specific
difference—especially W7 dot-qualified compatibility precedence and inspection accessibility—rather than calling the
whole debugger profile identical to source C# binding.

### 3.2 Closed type-argument grammar

The V2 projector admits recursively closed type arguments composed from:

- every CLI fixed-width primitive keyword, native-size integers, `decimal` where its exact pinned-compiler metadata
  encoding is admitted, `string`, and `object`;
- namespace/type names with per-type-segment arity;
- nested and constructed named types;
- type and namespace aliases, including an exact extern-alias-qualified root;
- nullable value types;
- single-dimensional zero-based arrays and bounded multidimensional arrays where runtime topology is exact; and
- nested combinations of the preceding forms within fixed depth, argument-count, segment-count, and metadata-signature
  limits.

Every valid Roslyn tree outside that grammar is `Unsupported`, not `Invalid`. Unsupported type arguments never cause a
metadata search or runtime enumeration.

### 3.3 Bounds

W8 reuses existing bounds when the physical operation is unchanged and introduces named V2 bounds for new work:

| Operation | Bound direction |
|---|---|
| Complete expression | Preserve the common front-end text/token/node/depth limits |
| Qualified segments | Preserve the existing maximum of 32 decoded access segments |
| Nested TypeDef chain | Preserve the existing maximum depth of 16 |
| TypeRef resolution | Preserve the existing maximum chain length of 16 |
| TypeSpec signature | Preserve 256 bytes, depth 32, and 64 arguments per projected construction |
| Candidate partitions/constructions | Add cap-plus-one accounting; never retain only a prefix and call it exhaustive |
| Modules/TypeDefs/FieldDefs | Preserve counted W7 module and metadata-table caps |
| Base/member lookup | Reuse the bounded ancestry graph and add counted member-search facts |
| Generic constraints | Bound GenericParam rows, substituted constraint TypeSpecs, constructors, attributes, and ancestry |
| Accessibility | Bound owner/nested/member chains, requesting assembly facts, and friend-assembly declarations |
| Import scopes/facts | Reuse the exact active chain with cap-plus-one completeness at each scope |
| Lexical blockers | Bound locals, constants, parameters, type parameters, and member rows independently |
| Runtime constructions | Enumerate cap-plus-one candidates and compare complete construction identity |
| Constructed assignability | Bound substituted base/interface edges, variance comparisons, and array ancestry |
| Memory reads | Preserve fixed-width pointer/string/object bounds and add explicit primitive/enum/nullable widths |

Concrete new cap values are frozen in W8.2 after W8.1 records compiler/runtime cardinalities. A reached bound produces
`Partial` or `Unsupported` according to the operation; it never silently truncates a candidate universe.

### 3.4 Evidence-gated and necessary boundaries

These boundaries are not parser limitations and are not omitted merely to keep W8 small:

- literal constants are mandatory and come from exact metadata without runtime construction, slot acquisition, or a
  memory read;
- thread-relative storage is admitted and requires an exact selected-thread identity in addition to its exact closed
  owner; the two-worker/two-owner fixture proves four distinct slots and values;
- named RVA-backed storage is admitted as `ModuleRva`, with exact module-content identity, FieldRVA geometry, counted
  raw reads, and runtime construction/slot acquisition marked `NotRequired`;
- context-relative storage is non-admitted with `W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE`; the runtime exposes one
  ordinary static slot but no attributable context identity, so W8 creates no `ContextRelativeSlot` API;
- static properties, events, operators, and methods require invocation/counterfactual-execution semantics rather than
  a field read;
- an open generic definition has no unique stored static slot; W8 binds storage only for an exact loaded closed
  construction, although an exact literal needs no loaded construction;
- target code is never run to create a missing construction or execute a type initializer;
- pointer/function-pointer type arguments and valid trees outside the closed V2 type grammar remain typed
  unsupported;
- extension-member and overload resolution are not field-name binding; and
- arbitrary non-nullable value-type payloads beyond the explicitly admitted primitive/enum family lack a stable current
  product value representation and remain typed unsupported rather than being exposed as untyped bytes;
- exact memory-homed `this`, reference/value parameters, and active locals are admitted through a separate
  `FrameValueExpressionV1` profile with selected frame/thread, scope, type, liveness, address, width, copied bytes, and
  decoded value; register homes are not proven; and
- selected-frame declaring-type `VAR` and method `MVAR` arguments are non-admitted. Legacy enumeration is `E_NOTIMPL`,
  shared-code evidence may retain exact `System.__Canon` rather than the closed argument, and the DBI factory declines
  the required interface with `E_NOINTERFACE`. No frame-generic placeholder or substitution service is exposed.

These dispositions are frozen by W8.1. No register, spill, heap, name, context, or uniqueness guess is permitted.

## 4) Versioned contract

### 4.1 Profile and compatibility

W8 adds, rather than mutates:

- `StaticFieldExpressionV2` as an explicit product language profile;
- `BindingContextV2` as a scoped semantic projection over immutable W7 physical facts plus new blocker facts;
- a structured closed-type syntax and symbol identity;
- a constructed-owner/runtime-construction identity;
- V2 declaration, plan, result, and provenance encodings plus separate mandatory
  `FrameValueExpressionV1` root/location/result encodings; and
- append-only generated and meaningful-synthetic report schemas.

W8.1 resolved the predeclared evidence branches before public contracts freeze:

- exact thread-relative static storage extends `StaticFieldExpressionV2` with a required selected-thread identity;
- exact RVA-backed storage extends the same profile with a distinct storage descriptor and read geometry; and
- exact `this`/parameter/local locations admit a separate `FrameValueExpressionV1` profile for `this` or one active
  identifier root followed by an already-admitted W2/W6 suffix. It never falls through to static lookup.

Context-relative storage remains non-admitted and creates no public strategy. Selected-frame declaring-type and method
generic arguments also remain non-admitted and cannot close V2 type syntax; fully ground TypeSpecs require neither
capability. Each non-admitted branch creates no public profile member or placeholder service; its probe, typed outcome,
and no-call tests remain the contract rationale.

Every W1–W7 public contract, default route, canonical byte sequence, digest, manifest schema, report byte sequence, and
test remains golden. A caller must select V2 explicitly. V1 failure never falls through to V2, and V2 failure never
runs V1, a strong-handle search, heap enumeration, reflection, or another binder.

### 4.2 Syntax projection

One complete Roslyn parse feeds a V2 projector that detaches:

- exact raw expression and decoded identifier text;
- `global::`, ordinary `.`, and arbitrary alias `::` separators;
- namespace/type candidate segments without prematurely deciding their roles;
- each generic type-argument tree and per-segment arity;
- the candidate static field identifier;
- an optional already-admitted W2/W6 suffix descriptor; and
- every reached syntax/type-construction bound.

The projector does not decide which prefix is a namespace, top-level type, nested type, or field owner. It retains all
bounded structural partitions for the binder, which resolves each route one segment at a time in declared C# order. At
each resolution step the binder freezes the first winning lexical level and its complete candidate set; it groups equal
physical candidates only within that step, never across precedence levels or routes. An ambiguity, or a
higher-precedence candidate that later lacks the requested segment/member, is terminal. A later segment or W2/W6 suffix
cannot disambiguate an earlier name, and no longest-prefix convention chooses a route.

Bare `StaticFieldExpressionV2` roots first apply complete lexical shadowing and current/enclosing constructed-type member
lookup, then active `using static` imports under the frozen C# precedence rule. They are not reinterpreted as a configured
W2 root or implicit instance value; an instance member blocks the field-only profile truthfully.
`FrameValueExpressionV1` has a separate projector that accepts only `this` or one active identifier root plus the detached
W2/W6 suffix; profile selection therefore decides the meaning before any binder call and never falls through to static
lookup.

### 4.3 Scoped binding context

`BindingContextV2` preserves the selected declaring type/nesting and physical outer-to-inner ImportScope chain, then
derives a consulted semantic view:

- current/enclosing constructed types and their nested-type/member catalogs participate at the exact C# lookup step;
- selected type/method parameter names participate at their exact precedence layer as blockers, but no selected-frame
  closed argument is available for `VAR`/`MVAR` substitution;
- current and enclosing namespaces follow the exact selected declaring namespace and C# namespace lookup order;
- simple-name lookup evaluates namespace-declaration levels from innermost to outermost and stops at the first level
  that produces a viable alias, declaration, or imported candidate set;
- namespace imports contribute candidates only at their exact declaration level and combine only with other imports
  participating at that same winning level;
- an inner same-name type/namespace alias hides outer aliases;
- alias/declaration conflicts and same-level imported candidates follow the frozen C# rule rather than import order or
  an invented first-wins policy;
- TypeDef/TypeRef/TypeSpec tokens are authoritative; decoded target text is display/provenance only;
- an extern alias is paired with its exact AssemblyRef import facts and resolution scope; and
- unsupported or malformed raw imports remain retained evidence and may prevent an exhaustive result.

Fully qualified `global::` lookup creates no frame/PDB/context capability call and suppresses contextual alternatives.
V2 also retains W7's metadata-global dot-qualified compatibility route without context. If that route resolves its
leading namespace/type name, its later absence or ambiguity is terminal; context cannot redirect it. Only absence of a
metadata-global leading name makes the ordinary spelling eligible for the contextual C# route described above. This
debugger-profile compatibility rule is an explicit difference from applying relative source-name lookup alone. An
alias-qualified spelling consults only the exact alias path it names plus the metadata required to resolve its target.

### 4.4 Metadata name and member binding

The metadata binder performs these bounded steps:

1. resolve the leading namespace, type, `global::`, using alias, or extern alias according to the selected route;
2. resolve each remaining namespace or nested-type segment with exact arity;
3. resolve every type argument recursively to a closed metadata construction;
4. validate generic head/argument arity, exact module/reference identity, and every substituted generic constraint;
5. instantiate each base/interface TypeSpec edge through the current closed construction, require a closed exact result,
   and apply the definition-kind-specific class, value-type, or interface lookup rule frozen by W8.1;
6. search the resulting bounded constructed ancestry graph for the field name using one explicit V2 hiding rule;
7. select one static declaration and retain its actual declaring construction plus stored-versus-literal kind; and
8. instantiate a stored field signature or decode a literal's exact metadata constant before choosing a value decoder.

Nested generic identity has an explicit compiler-derived mapping contract. Source and Roslyn syntax retain type
arguments per named segment; emitted metadata records each nested TypeDef name/arity and its ordered GenericParam rows;
and runtime evidence may expose a flattened closed-argument vector. W8.1 freezes the pinned compiler's actual relation
for `Outer<T>.Inner<U>` and deeper mixed generic/non-generic chains. The product construction then records both
segment-local groups and one canonical flattened order, requires TypeSpec and runtime sources to map to the same vector,
and substitutes a declaring field's `VAR` index only through that declaring type's proven flattened order. It does not
assume that source-local arity equals metadata name arity.

W7's canonical TypeDef identity remains byte-for-byte unchanged and continues to describe the pinned compiler mapping it
already validates. W8 adds a raw TypeDef-row/enclosing-chain identity that does not impose that mapping, followed by a
separate compiler-arity certificate. A readable row from another producer whose flattened parameters cannot be assigned
to unique source segments remains exact physical metadata with a non-exact mapping disposition; it cannot drive argument
slicing, substitution, or closed construction until a corresponding mapping rule is admitted.

Base construction is recursive rather than a TypeDef-only ancestry walk. A fixture such as
`Derived<T> : Mid<List<T>>`, `Mid<U> : Base<U[]>` must map `Derived<int>` to the exact declaring
`Base<List<int>[]>` construction before substituting the selected field's signature. Each hop retains the original base
signature, substituted construction, definition identity, and counted stop evidence; an open, malformed, ambiguous, or
cap-limited hop stops before member or runtime selection.

Every constructed head has a bounded constraint certificate before it is called exact. The binder reads complete
GenericParam and GenericParamConstraint rows, special constraint flags, substituted constraint TypeSpecs, required
parameterless constructors, and the pinned compiler's recognized constraint attributes/modifiers. It partitions them
according to compiler semantics: violated hard construction constraints are `Invalid`; nullable annotations such as
`class?` and warning-only `notnull` findings are retained as stable non-fatal diagnostics/provenance; and anti-constraints
or ref-like encodings are admitted only when their exact rule/evidence is understood, otherwise `Unsupported`. An
incomplete row set is `Partial`; none of these cases is misreported as an absent runtime construction. Compiler semantic
differentials cover the hard/warning/anti-constraint partition, reference/value/default-constructor, enum/delegate,
unmanaged, nested dependent-parameter, and recursively substituted base/interface constraints.

Member accessibility and hiding are route-ordered. A contextual C# route first filters the declarations at each
constructed ancestry level by the exact use-site accessibility certificate; an inaccessible derived declaration does
not hide an accessible base candidate. The explicit debugger-inspection route applies its declared accessibility bypass
first, so the now-admitted derived declaration does hide its base counterpart. In either route, an accessible winning
instance field, property, event, method group, or unsupported static storage shape produces a typed member-lookup stop
rather than falling through to a base static field.

W8 retains effective accessibility for every owner, nested-type segment, and field. The explicit qualified debugger
inspection route may bypass source accessibility for the full owner/member chain, matching W7 behavior, but records both
declared accessibility and the bypass in the result. Contextual C# routes, including `using static`, use a frozen use-site
certificate: selected containing type/nesting, requesting assembly identity, complete AssemblyRef identity, exact
friend-assembly declarations including key material when present, and public/private/family/assembly/family-or-assembly/
family-and-assembly flags across the effective owner/member chain. Missing facts are not treated as public. Generated
compiler differentials cover same/other/friend assembly, nested private, protected-family, combined access flags, and
qualified-inspection bypass separately.

### 4.5 TypeSpec and generic substitution

A TypeSpec is decoded into an immutable bounded construction tree:

- every complete TypeSpec root admitted by the shared ECMA Type grammar is decoded, including direct
  `CLASS TypeDefOrRef`, direct `VALUETYPE TypeDefOrRef`, `GENERICINST CLASS`, `GENERICINST VALUETYPE`, `SZARRAY`,
  and bounded `ARRAY`; direct named roots follow the same role/construction classification as any other exact tree,
  so their physical proof may yield exact, open, non-exact, or invalid use without a root-shape-only rejection;
- named `CLASS`/`VALUETYPE` nodes and `GENERICINST` heads resolve only TypeDef/TypeRef coded indices to one exact
  TypeDef; a TypeSpec tag in one of those positions is `Invalid`. TypeSpec indirection is followed only in positions
  where the current runtime permits it, including custom modifiers, with the same depth/token/visited-set bounds;
  a forbidden tag, cycle, or non-definition terminal is `Invalid` rather than guessed;
- ordered arguments retain their complete structural and resolved identities;
- nested constructed types retain declaring construction and per-segment arity;
- array/nullable/reference topology is explicit;
- custom modifiers and unsupported element kinds remain typed facts rather than being discarded; and
- complete original signature bytes remain part of replay identity.

Field-signature instantiation substitutes owner `VAR` indices from the exact declaring construction recursively. It
preserves W7's exact `Int32`, `String`, `Nullable<Int32>`, object, and managed-reference behavior and deliberately adds
all CLI fixed-width integral signedness/widths, `Boolean`, `Char`, `Single`, `Double`, target-width native integers,
enum-underlying values, admitted nullable values, arrays, and constructed references. Out-of-range owner variables, an
open result, malformed signatures, or incompatible value-shape claims remain distinct invalid/unsupported outcomes.
`MVAR` is never legal in a FieldDef signature and is `Invalid`. Selected-frame method arguments are also non-admitted
for closing an expression owner/type tree.

Literal projection reads the FieldDef literal flag, exact field signature, and Constant row plus the pinned-compiler
literal attribute encodings proven by W8.1. Primitive, enum-underlying, floating-bit, string, null, and exact `decimal` constants
produce canonical V2 values with runtime construction and storage marked `NotRequired`; malformed, duplicate,
type-incompatible, or unknown literal encodings stop before any runtime capability call.

Literal primitive/enum/decimal/non-null-string values are direct results. A metadata literal has no fabricated target
address, so a suffix that needs object navigation is `Unsupported`; an exact null reference may still take the unchanged
W2/W6 conditional-null or compatible-fallback path without a read. Literal provenance distinguishes that semantic
short-circuit from a heap-backed reference.

Portable-PDB `AliasType` targets are required to be fully ground unless the compiler/PDB oracle proves another emitted
form; W8 does not fabricate a frame-relative alias import. Separately, expression syntax such as
`GenericSlot<T>.Current` inside a selected generic frame may contain type- or method-parameter references. W8.1 found
no exact selected-frame closed-argument source, so those forms produce the corresponding typed unsupported/unavailable
outcome and no `VAR`/`MVAR` frame-substitution contract exists. A fully ground TypeSpec does not require that capability.

### 4.6 Constructed runtime identity and storage

TypeDef plus FieldDef is not a storage identity for a generic static. W8 freezes one constructed runtime owner from:

- snapshot and runtime/application-domain identity;
- runtime assembly-load-context address or LoaderAllocator handle when the pinned runtime reports one;
- runtime module plus counted module-content identity;
- exact TypeDef and FieldDef;
- ordered recursive closed type arguments, including exact array element, rank, and SZ-versus-multidimensional topology;
- exact enclosing construction for nested types;
- runtime method table when reported; and
- the selected stored static slot address and storage decoder.

Runtime mapping enumerates every bounded same-TypeDef candidate, derives complete construction identities from an exact
ordered argument source keyed to that candidate's runtime type identity, groups equal identities, and requires exactly
one candidate matching the bound metadata construction. Enumeration order, runtime display text, parsing `ClrType.Name`,
global name lookup, or a first matching token may not choose the result. Multiple exact constructions with the same
TypeDef/FieldDef must prove distinct method-table/construction/slot/value evidence in the physical fixture.

Reference-target validation uses a separate exact constructed-assignability graph. It instantiates every target
base/interface TypeSpec edge, compares invariant generic arguments by complete construction identity, applies covariance
or contravariance only when the exact GenericParam flags and reference-type arguments permit it, and admits array
covariance only for exact equal rank/SZ topology and recursively assignable reference elements. Value-type arguments
remain invariant. A partial edge, unknown variance fact, cap, or runtime/metadata disagreement blocks suffix evaluation
rather than falling back to definition-only assignability.

One frozen storage-strategy discriminator prevents the operation order from pretending all values have a generic static
slot:

- `ConstructedSlot` requires exact runtime construction/domain and one per-construction slot;
- `ThreadRelativeSlot` additionally requires the exact selected-thread identity and attributable thread location;
- `ModuleRva` requires exact module-content identity, FieldRVA row, mapped RVA geometry, and no runtime construction when
  the emitted/runtime layout proves it unnecessary;
- `MetadataLiteral` requires only the frozen metadata constant and makes runtime/storage/memory calls `NotRequired`; and
- `FrameLocation` belongs only to `FrameValueExpressionV1` and consumes one exact memory-address descriptor. Register
  homes are not part of the admitted contract.

For address-backed stored/frame strategies the adapter locates evidence but does not supply the value; the project-owned
memory reader is the sole product byte source. Metadata is the sole literal value source. High-level runtime reads and
reflection remain late equality oracles in tests.

### 4.7 Evidence-qualified bare members and `using static`

After local/parameter/type-parameter names, current/enclosing constructed-type member lookup runs before imported static
members and applies exact ancestry, hiding, and accessibility. A same-name instance field, property, event, or method
group is a valid typed stop, not permission to skip to an import.

An active exact `using static T` fact—including a constructed TypeSpec owner—contributes directly declared accessible
stored/literal static members and nested types of `T`; it does not contribute inherited members. An imported nested type
may become the head of a later qualified construction. Accessibility is decided from the complete use-site certificate
above. Before a bare field may win, the binder requires a lexical-blocker
completeness certificate for the selected instruction location:

- exact selected MethodDef, declaring type, and active IL/PDB scope;
- complete parameter names and generic-parameter names;
- zero locals or a complete bounded local signature whose relevant slots are accounted for by active Portable-PDB
  local-variable/local-constant facts;
- complete selected-type and base-member name catalogs for higher-precedence unqualified member lookup;
- complete active using/type/namespace alias facts; and
- an explicit disposition for compiler-generated or unsupported lexical constructs that could own the spelling.

The lexical catalog covers ordinary locals/constants, pattern/deconstruction/foreach/catch/using/fixed/range variables,
parameters, type parameters, local functions, current/enclosing type members and type names, and every active alias/import
kind. W8.1 freezes which Portable-PDB records, metadata rows, and pinned-compiler generated-name associations prove each
kind and prove its active source scope. A missing artifact is never replaced by an assumption that the source construct
was absent.

Any unnamed/unaccounted local, stripped parameter, unsupported scope, incomplete local-function/member/type catalog, or
conflicting source stops before imported-field selection. A proven local/parameter/member/type/alias with the same name yields
`Shadowed` or valid-but-unsupported according to the symbol kind. Its value is not guessed or read.

Multiple imported stored/literal fields with the same name are ambiguous unless same-winning-level resolution proves
one physical declaration.
Properties, events, and method groups with the same bare name block field-only evaluation truthfully. No candidate is
selected by import order.

## 5) Operation order and outcome model

### 5.1 Fixed operation order

The shared W8 pipeline is:

1. select the caller-requested `StaticFieldExpressionV2` or admitted `FrameValueExpressionV1` profile and enforce common
   expression-input bounds;
2. perform the sole complete Roslyn expression parse;
3. apply common parse-integrity checks;
4. project the corresponding detached V2 syntax/type tree or frame-root descriptor;
5. determine explicit, contextual type, bare-static-member, or frame-value route within that selected descriptor;
6. acquire only the context capabilities required by that route;
7. resolve namespace/type/alias scope, constraints, and closed metadata construction for a static route, or attribute the
   exact selected-frame root for an admitted frame-value route;
8. perform definition-kind-specific qualified/bare/`using static` member lookup plus accessibility, or mark member lookup
   `NotRequired` for the frame root;
9. instantiate the declaring construction's FieldDef signature, decode its literal constant, or retain the frozen frame
   root type/location descriptor;
10. freeze the required `ConstructedSlot`, `ThreadRelativeSlot`, `ModuleRva`, `MetadataLiteral`, or `FrameLocation`
    strategy and acquire only its required runtime/thread/module identity;
11. locate its exact stored/frame address and geometry when a raw value read is required;
12. freeze the complete strategy-tagged plan before obtaining its value;
13. read/decode dump memory or project the frozen exact literal according to that strategy;
14. validate a non-null reference target through constructed assignability when required;
15. evaluate the unchanged frozen W2/W6 suffix, if any; and
16. project the selected profile's canonical result and provenance record.

No stage reparses, retries with a broader profile, rebuilds an earlier candidate universe after a value read, or
exposes a partial plan after a failed prerequisite.

### 5.2 Strict outcome axes

The W8 profiles keep at least these independent axes:

| Axis | Representative dispositions |
|---|---|
| Syntax | Admitted, Invalid, Unsupported |
| Context acquisition | NotRequired, Exact, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Root attribution | NotRequired, Exact, Shadowed, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Lexical completeness | NotRequired, Complete, Shadowed, Partial, Unsupported, NotReached |
| Namespace/type binding | NotRequired, Exact, Absent, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Type construction | NotRequired, Exact, Open, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Member lookup | NotRequired, Exact, Absent, Partial, Unavailable, HiddenByUnsupportedMember, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Runtime construction | NotRequired, Exact, Absent, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Storage | NotRequired, Exact, Partial, Unavailable, Ambiguous, Conflict, Invalid, Unsupported, NotReached |
| Value | ExactValue, ExactNull, Partial, Unavailable, Conflict, Invalid, Unsupported, NotReached |
| Suffix | NotRequested, Completed, Blocked, Conflict, Invalid, Unsupported, NotReached |
| Completeness | Complete, Partial, NoAnswer |

Stable product diagnostics map from these typed records. Exception text, runtime display names, and compiler diagnostic
wording do not enter canonical identity.

### 5.3 Provenance

An exact result records only evidence actually consulted, including:

- raw expression/profile and detached syntax/type identity;
- explicit/contextual/bare-static/frame-value route;
- selected frame/PDB/import facts when consulted;
- namespace/type partitions and rejected complete candidates;
- alias/extern/using-static resolution and lexical completeness;
- metadata definition/construction/field signature plus substitution;
- root-location evidence or runtime/thread/context/module construction/domain/slot/RVA mapping when required;
- raw memory reads and decoder, or exact literal metadata and proof that runtime capabilities were not called;
- reference-target validation; and
- unchanged suffix plan/result provenance.

The fully qualified route's provenance must prove absence of frame/PDB calls, not merely omit their successful values.

## 6) Physical truth gates and fixture

### 6.1 Dedicated emitted fixture

W8.1 added `Interpreter.W8TestTarget`, `Interpreter.W8AliasTarget`, `Interpreter.W8ForwarderTarget`, and the named
FieldRVA companion. The target materializes before each dump:

- at least four closed constructions of the same generic TypeDef;
- at least two constructions with the same FieldDef but distinct nonzero slots and distinct primitive values;
- constructed class, value-type, and interface owners with stored static fields, each with definition-kind, lookup-rule, slot,
  and value evidence;
- a `T`-typed reference field, exact null, nullable value, string, every primitive width/signedness, native-size values,
  enum-underlying values, and exact metadata literals;
- nested non-generic and nested constructed owners;
- `Outer<T>.Inner<U>` and a deeper mixed generic/non-generic chain whose syntax segments, TypeDef names/arity,
  GenericParam rows, TypeSpec vector, runtime argument vector, and field `VAR` substitution are compared explicitly;
- a multi-hop generic-base/derived-owner case whose base TypeSpec edges change construction shape at each hop;
- SZ-array and multidimensional-array constructed arguments with exact element/rank/topology evidence;
- TypeDef, TypeRef, and TypeSpec aliases, including fully ground generic, SZ-array, and multidimensional-array roots;
- nested ImportScopes with alias shadowing;
- exact non-generic and constructed-TypeSpec `using static`, imported nested-type-head, and extern-alias facts;
- positive/negative generic-constraint and same/other/friend-assembly accessibility matrices;
- exact bare-name blocker-free and shadowed methods; and
- selected frames in non-generic, generic-type, and generic-method contexts.

All target processes are hidden. Static constructors run only as normal target setup before the dump; the evaluator
never triggers them.

### 6.2 Compiler/PDB truth

The independent SRM/compiler oracle proves from emitted bytes that:

1. the whole-owner constructed alias is encoded as an `AliasType` import whose target token is a TypeSpec;
2. generic, SZ-array, and multidimensional-array TypeSpec bytes decode to the intended complete trees;
3. constructed `using static`, imported nested types, nested ImportScopes, every claimed lexical name kind, and
   extern-alias/AssemblyRef facts are physically emitted in the selected methods;
4. same-winning-level namespace-import/TypeRef-forwarder convergence agrees with an independent compiler semantic oracle;
5. constraint, accessibility, friend-assembly, and definition-kind facts are complete in emitted metadata;
6. method debug identity and active IL/PDB scope are exact; and
7. poison artifacts produce the intended partial/conflict/invalid physical evidence without changing unrelated facts.

The one planned named-local slot-reuse relation is not emitted and remains explicitly unavailable; all other required
facts above are exact. Tests do not fabricate a PDB fact and call it compiler evidence.

### 6.3 Completed runtime/static-storage truth

The W8.1 ClrMD/raw-memory probes prove:

1. all required closed constructions are present in the reopened dump;
2. a public runtime API or counted raw-runtime structure yields exact ordered closed arguments keyed to each candidate
   runtime type identity, including exact array element/rank/SZ topology, and complete argument identity distinguishes
   same-TypeDef instances;
3. class, value-type, and interface constructions with the same stored-static FieldDef have stable distinct slots and expected raw
   values under their separately frozen lookup/storage rules;
4. every admitted primitive width/signedness, floating representation, target-width native integer, enum underlying
   value, nullable form, string, reference, and exact null has unambiguous target layout and read geometry;
5. thread-relative storage has exact owner/thread/address/value mappings, RVA-backed storage has exact
   module/row/address/value mappings, and context-relative storage has the reproducible
   `W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE` non-admission;
6. nested and recursively substituted generic base/interface declaring constructions correlate with counted metadata;
7. constructed invariant/variant/interface/base and array-assignability graphs agree with exact runtime identities;
8. literal evaluation performs no runtime construction, storage, or memory capability call; and
9. close/reopen/rebind reproduces canonical construction, slot, read, value, and no-call evidence.

Parsing `ClrType.Name`, resolving those display fragments through global `GetTypeByName`, or observing metadata generic
parameters does not pass this gate. The proven candidate-keyed source is the bounded runtime descriptor/available-type/
PerInstInfo/dictionary path. Array rank and SZ topology also come from that exact structure rather than a display name.
The gate passed before W8.2 product contracts began.

### 6.4 Completed stack/generic-context feasibility probe

The selected-frame probe separately resolved whether the pinned runtime/artifacts can attribute:

- `this`;
- reference and value parameters;
- active locals;
- declaring-type generic arguments; and
- generic-method arguments

to exact runtime locations or closed type identities at the selected instruction. Exact memory-homed `this`,
reference/value parameters, and active locals pass with type, width, liveness, copied bytes, and value/reference
decoding. Register homes remain unproven. Declaring-type and method generic arguments fail exact attribution for the
reasons frozen in the W8.1 disposition and remain non-admitted. GC roots, context records, PDB slot numbers, hidden
tokens, runtime names, and method display text are individually insufficient. The later product path consumes only the
frozen memory-location descriptor and never repeats attribution.

## 7) Generated conformance

Generated conformance is a cross-product, not a few happy paths.

### 7.1 Syntax and binding classes

Cover at least:

- fully qualified, dot-qualified, `global::`, namespace alias, type alias, extern alias, and bare-member routes;
- top-level/nested, class/value-type/interface, non-generic/generic, one/multiple arities, nullable/array/nested type
  arguments, with separate qualified/inherited/hiding rules for each definition kind;
- direct and inherited qualified fields, hiding by every unsupported neighboring member kind, and actual declaring-owner
  retention;
- TypeDef/TypeRef/TypeSpec imports including array-root aliases, inner/outer alias precedence, same-winning-level
  TypeRef/forwarder convergence, and genuine ambiguity;
- nested namespace-declaration scopes where an inner viable level suppresses otherwise matching outer imports, plus an
  inner-empty control that proceeds to the outer level;
- nested generic segment-local/metadata/flattened arity mappings across mixed generic and non-generic owners;
- satisfied/violated/partial/unsupported generic constraints with exact substituted constraint ancestry;
- complete accessibility matrices and explicit qualified-inspection bypass across owner, nesting, and field;
- multi-hop constructed-base substitution where a higher-precedence declaration, an ambiguity, or a missing later
  segment is terminal even though a lower-precedence path could otherwise complete;
- complete/incomplete lexical-blocker certificates and each shadowing symbol kind;
- non-generic and constructed-TypeSpec `using static`, imported nested-type heads, stored fields, and literal fields;
- `VAR` substitution at every supported recursive field-signature position; and
- typed unsupported/unavailable expression-derived frame `VAR`/`MVAR` forms, with no compiler-emitted frame-relative
  alias claim and no metadata/runtime call after the frozen non-admission; and
- exact `FrameValueExpressionV1` `this`, reference/value parameter, and live local roots across proven memory homes,
  direct primitive/reference/null results, unchanged suffixes, dead/out-of-scope locations, same-name shadowing,
  profile-isolation no-fallback cases, and explicit non-admission of register homes;
- every added primitive width/signedness, enum underlying type, exact literal encoding, nullable form, constructed
  reference, variance/invariance case, and admitted array-covariance case; and
- valid-but-unsupported generic method invocation, other invocations, indexers, pointer type shapes, properties, events,
  and storage/value forms whose W8.1 evidence branch is non-admitted.

### 7.2 Evidence and failure classes

Independently poison or limit:

- frame, MethodDef, instruction mapping, debug identity, PDB bytes, LocalScope, ImportScope, and raw import payload;
- module catalog, TypeDef/TypeRef/TypeSpec/AssemblyRef, nested chain, arity, constraints, accessibility, ancestry,
  FieldDef, field signature, Constant row, and literal attribute;
- runtime construction enumeration, generic arguments, application domain, static field, and slot;
- primitive/reference bytes, target method table/extent/constructed assignability, and suffix reads; and
- every new cap at cap, cap-plus-one, malformed, unavailable, and conflicting states.

Every first failure proves that later capabilities were not called.

### 7.3 Differential and replay classes

- Compare admitted syntax projection with the pinned Roslyn tree without retaining Roslyn objects.
- Compare bounded namespace/type/member decisions with a compiler oracle over equivalent generated source, while keeping
  compiler semantic objects out of product contracts.
- Poison every left-to-right stop with a lower-precedence or later-suffix candidate that would succeed if consulted, and
  prove that it is not consulted.
- Compare raw values with high-level runtime/reflection reads only after the product result exists.
- Repeat in one session, fresh product objects, fresh hidden consumers, and close/reopen/rebind.
- Freeze V1 and all preceding canonical artifacts byte-for-byte.

## 8) Meaningful synthetic portfolio

### 8.1 Corpus contract

W8 predeclares the thirty-two independent core full-dump incidents below across request, batch, coordinator, and workflow
shapes. W8.1 admits three additional success branches: thread-relative storage, RVA-backed storage, and
`FrameValueExpressionV1`. Each receives one independent incident that freezes one expression, singular expected axes,
counterfactual, value, first boundary, shape, and decision facts before product implementation. Context-relative
storage and frame-generic arguments are non-admitted and add no fictitious success row. The final portfolio therefore
contains at least thirty-five incidents and retains all four shapes.
Each row owns one dump, expression, explicit profile, target invocation, selected-frame/PDB/artifact inputs, fully
qualified control where meaningful, expected typed axes, first boundary, usefulness, decision impact, attributable
stage evidence, and successor category. The four shapes use materially different generic/nested object graphs and
field value/suffix questions.

The planned incident questions are:

| # | Shape | Question / expected first result |
|---:|---|---|
| 1 | Request | Fully qualified `GenericSlot<RequestContext>.Sentinel` returns its exact `Int32` |
| 2 | Batch | TypeSpec whole-owner alias reaches the same construction/FieldDef/slot/value as its explicit control |
| 3 | Coordinator | Four same-TypeDef constructions coexist without enumeration order choosing one |
| 4 | Workflow | `T`-typed exact reference substitutes `VAR 0` and completes a W6 conditional chain |
| 5 | Request | `GenericSlot<RequestContext>` and `GenericSlot<BatchContext>` prove distinct slots and values |
| 6 | Batch | Nested `Outer<TKey>.Inner<TValue>.Count` binds with per-segment arity |
| 7 | Coordinator | A namespace alias inside one type argument resolves an exact closed generic value-type owner |
| 8 | Workflow | An extern-alias-qualified constructed interface owner binds its stored static field across the exact AssemblyRef |
| 9 | Request | An inner type alias shadows a same-named outer alias and records only consulted facts |
| 10 | Batch | Two same-level namespace-import TypeRef/forwarder paths converge on one physical TypeDef and produce one symbol |
| 11 | Coordinator | A qualified derived owner resolves the exact field declared on its constructed generic base |
| 12 | Workflow | A derived unsupported same-name member hides a base static field and stops truthfully |
| 13 | Request | A blocker-free constructed-TypeSpec `using static` bare primitive binds its directly declared stored field |
| 14 | Batch | A nested type imported by `using static` becomes the exact constructed head for a reference suffix pipeline |
| 15 | Coordinator | A closed nullable type argument and `VAR` field signature return one exact has-value form |
| 16 | Workflow | An array/nested constructed type argument returns exact null |
| 17 | Request | Missing selected frame stops an alias route while the fully qualified control remains exact |
| 18 | Batch | Partial PDB bytes stop before TypeSpec or runtime construction resolution |
| 19 | Coordinator | PDB/module identity conflict stops before candidate binding |
| 20 | Workflow | Competing surviving aliases produce ambiguity; import order cannot choose |
| 21 | Request | A malformed TypeSpec is `Invalid` with original bytes retained |
| 22 | Batch | TypeSpec depth cap-plus-one is `Partial` without a prefix candidate |
| 23 | Coordinator | Generic head/arity disagreement is `Invalid` before runtime enumeration |
| 24 | Workflow | Two metadata definitions matching the spelling remain ambiguous without exact alias narrowing |
| 25 | Request | Requested closed runtime construction is absent and never falls back to another argument |
| 26 | Batch | Runtime-construction candidate cap-plus-one is `Partial` and cannot select an in-prefix construction |
| 27 | Coordinator | An unavailable static slot preserves exact construction identity without a value |
| 28 | Workflow | Substituted reference target conflict blocks suffix evaluation |
| 29 | Request | An active local shadows a bare imported field; no local value is guessed |
| 30 | Batch | Incomplete local/parameter/member catalogs block `using static` selection |
| 31 | Coordinator | A property sharing the bare name is valid but unsupported, not a field fallback |
| 32 | Workflow | A fully ground TypeSpec alias inside a generic method returns an exact enum literal with no runtime-context call |

Every contextual incident includes a poisoned or absent context control for the fully qualified route where the
spelling has an equivalent. Every snapshot hash is distinct. No generated fixture row may be promoted to
representative observation.

### 8.2 Portfolio measurement corrections

W8 does not repeat W7's label/count shortcuts:

- usefulness and decision-changing are explicit per-row booleans with stable structured rationales;
- attributable evidence is measured at the named relevant stage, not inferred from terminal value text;
- the runner validates that a decision-changing row differs under a declared counterfactual action;
- successor categories and their actions are frozen in the manifest before target generation;
- a category qualifies only with at least four incidents, three application shapes, and three decision-changing rows;
- substantive equality includes incident, shape, decision-changing, and attributable-evidence counts; and
- any substantive tie defers rather than selecting by enum or manifest order.

Two fresh hidden consumers must emit byte-identical machine and human reports. Human reports remain shape-only where
values could expose unstable addresses or fixture-specific text.

### 8.3 Successor gate

The portfolio may select only one predeclared post-W8 action or explicitly defer. Candidate categories may cover an
evidence-backed stack-value root, another static storage family, one static property/method execution shape, a broader
frame-generic context, collection/indexing, or another observed boundary. W8 does not implement the winner.

Representative observation remains a separate denominator and may later confirm, reverse, or stop the synthetic
direction.

## 9) Delivery sequence and LOC bands

Each completed checkpoint receives a detailed multi-line commit and is pushed before the next checkpoint begins.
Checkpoint messages record the physical evidence, exact contract boundary, compatibility invariants, headless tests,
and remaining typed stops. LOC bands may be revised at any checkpoint.

### W8.0 — roadmap, requirements, and evidence-conditioned corpus envelope

**Scale:** `~1K LOC` documentation.

**Status:** Complete; the exit statements below record the state at W8.0 closure.

Publish this plan, activate PM-25/PM-26, reconcile navigation and active design surfaces, record W7's decision limits,
freeze the mandatory V2 core plus the predeclared thread/RVA/frame evidence branches, and predeclare the core corpus
schema and decision rules. At W8.0 closure, W8.1 was defined to select only among those declared branches and freeze
the final scope before contracts.

**Exit gate**

- W7 remains complete and its evidence is not rewritten.
- At W8.0 closure, W8 was the sole active design/implementation sequence and implementation had not begun.
- All current documents agree on V2 scope, V1 compatibility, `~100K LOC` umbrella scale, and headless policy.
- Markdown, headless-workflow, and authored-scope vocabulary guards pass.

### W8.1 — compiler, runtime, storage, and frame truth gates

**Scale:** `~10K LOC` target, oracle, probes, and tests.

**Status:** Complete at exact source baseline `220be94b4`; see the
[W8.1 Physical-Truth Disposition](w8-1-physical-truth-disposition.md).

Add the dedicated targets and prove emitted TypeSpec/using-static/extern/scope facts, multiple closed runtime
constructions, distinct stored static slots/values, literals, definition-kind lookup, nested/base ownership, exact
primitive/array topology, thread/RVA storage probes, reopen replay, and the frame/generic-context feasibility result
before product contracts.

**Exit gate**

- Every mandatory physical fact is exact in real compiler output and real full dumps.
- Constructed class/value-type/interface-kind evidence and nested-generic arity mappings are frozen from emitted
  metadata, TypeSpec bytes, and runtime structures rather than assumed from source spelling.
- An exact ordered closed-argument source keyed to each runtime type candidate is proven; display-name reconstruction is
  explicitly insufficient.
- Thread-relative, RVA-backed, and exact memory-homed frame-value branches are admitted; context-relative storage,
  register homes, and selected-frame generic arguments are typed non-admitted. The final W8 scope and thirty-five-row
  minimum corpus are frozen before W8.2.
- Negative artifacts are independent and preserve unrelated exact evidence.
- The stack/generic-context probe either proves an attributable source or records a typed executable non-admission.
- Every public target/probe type and public method introduced by W8.1 has detailed XML documentation and draft caveats.
- No product binder API or fabricated fixture fact precedes the gate.

### W8.2 — immutable V2 and evidence-admitted branch contracts

**Scale:** `~10K LOC` implementation and tests.

**Status:** Complete for its authority-cutover scope at `d4d5f745c`; see the
[W8.2 Metadata Authority Cutover](w8-2-metadata-authority-cutover.md) ledger. Immutable expression/frame foundations,
shared Core signature grammar, Product event projection, caller-supplied lexical evidence, and the source-anchored
metadata proof families through `5fd87a3e5` landed first.
The metadata layer retains exact source ends, prefix-free token and graph stops, raw versus role-classified TypeDefs,
TypeSpec graphs, FieldSig certificates, GenericParam owner/table/binding proofs, complete interface/constraint table
aggregates, provisional construction classification, and exact Nullable topology. Tests cover complex nested
generics, arrays, modifiers, function pointers, duplicate blobs, cycles, crossed owners, exact-cap/cap-plus-one,
direct named TypeSpec roots, incomplete sources, immutability, and public draft XML. The proof objects are not yet
required by substitution/member/context/runtime consumers, and no host producer, V2 binder, runtime/storage mapper,
or evaluator is implied by this status.

Freeze additive, defensively immutable, content-equal contracts for structured type syntax, scoped imports, lexical
blockers, metadata constructions, TypeSpec projection, substitution, member lookup, runtime construction, storage,
strategy-tagged plan/result/provenance, bounds, and diagnostics. Also freeze the mandatory
`FrameValueExpressionV1` detached root syntax, exact memory-location identity, direct value/result, and no-fallback
profile boundary.

**Exit gate**

- All public prototype types/methods have detailed XML documentation and draft caveats.
- Canonical bytes, equality/hash, defensive copies, invalid-input matrices, and cap-plus-one behavior pass.
- Every V1 and preceding golden artifact remains byte-identical.

### W8.3 — sole-Roslyn-parse W8 projection

**Scale:** `~10K LOC` implementation and tests; realized as `~2K LOC` because the detached syntax contracts had
already landed in W8.2 and only the two projectors remained.

**Status:** Complete at `8bb74c866`. `StaticFieldV2ExpressionParser` and `FrameValueV1ExpressionParser` project the
single pinned parse into the frozen detached descriptors, stop at the first reached boundary, and keep the two
profiles isolated.

Project generic, nested, alias-qualified, and bare-root Roslyn trees into detached V2 syntax/type candidates without
semantic binding, reparsing, or Roslyn leakage. For an admitted frame-value branch, the same parse site projects only a
bounded `ThisExpressionSyntax` or `IdentifierNameSyntax` root and an already-admitted suffix into its separate descriptor.

**Exit gate**

- One production `SyntaxFactory.ParseExpression` site remains.
- Complex admitted/unsupported/malformed buckets cover all new trees and bounds.
- Invalid/unsupported inputs make zero metadata/context/runtime/memory calls.
- W2/W5/W6/W7 classification and replay remain unchanged.

### W8.3b — host-owned metadata producer

**Scale:** `~10K LOC` implementation and tests.

**Status:** Added by plan revision after W8.3. The W8.2 authority families are complete but every catalog is currently
materialized from synthetic rows in tests. Nothing downstream can run against a real artifact until one host-owned
producer reads a loaded module's physical tables and issues the same catalogs.

Implement the producer that reads exact physical rows through the existing SRM/PE and ClrMD seams and materializes
the complete-table catalogs, definition authority, compiler-name mappings, reference tables, chain, resolution,
ancestry, and constraint portfolios for every loaded module in one snapshot. The producer performs no binding, no
name interpretation, and no runtime construction: it acquires counted rows and hands them to the existing guarded
`Create` entry points, so a producer defect appears as a typed catalog stop rather than a fabricated identity.

**Exit gate**

- A generated full dump materializes an exact portfolio over its real modules, including the runtime core library.
- Every acquisition bound, partial read, and malformed row surfaces as the already-declared typed stop with no
  fabricated row and no partial prefix.
- Producer output replays byte-identically across close/reopen/rebind and fresh product objects.
- Synthetic and produced catalogs are interchangeable at every downstream consumer.

### W8.4 — metadata namespace/type/member construction binder

**Scale:** `~10K LOC` implementation and tests.

**Status:** Complete at `6eb01b53e` for its definition-side scope. The FieldDef authority catalog, the exhaustive
explicit-route type-name binder, the closed-construction binder with three-valued constraint dispositions, and
definition-side member lookup with hiding and accessibility have landed. Substitution of a selected field's signature
onto its declaring construction, and the contextual routes, remain with W8.5 and the runtime slice. Interface
constraints, substituted constraint TypeSpecs, property and event tables, interface ancestry, and physical
friend-assembly attributes are recorded as declared coverage boundaries rather than silent gaps.

Implement exhaustive namespace/top-level/nested partitions, per-segment arity, TypeDef/TypeRef resolution, closed
generic construction and constraints, recursive constructed-base/interface substitution, definition-kind-specific
member lookup, hiding, declared-owner selection, literals, accessibility, and field-signature substitution.

**Exit gate**

- Fully qualified nested/generic controls bind with all context capabilities poisoned.
- Segment-local source arguments, emitted nested TypeDef arity/GenericParam rows, canonical flattened construction
  order, TypeSpec arguments, runtime arguments, and field `VAR` substitution agree for every nested-generic fixture.
- No longest-prefix, first-module, first-TypeDef, or first-member heuristic exists.
- Direct/inherited/hiding and `VAR` substitution differentials agree with the declared V2 rule.
- Class, value-type, and interface qualified/inherited lookup each agree with the compiler oracle; interface ancestry is
  not treated as a class base chain.
- Every known generic-constraint violation and incomplete/unknown constraint encoding stops before runtime enumeration.
- Multi-hop base TypeSpec substitution reaches the exact declaring construction, and every open/partial/ambiguous base
  hop stops before member or runtime selection.
- Valid neighboring member/storage kinds stop truthfully before runtime mapping.

### W8.5 — scope-precise PDB/import/alias/extern binding

**Scale:** `~10K LOC` implementation and tests.

Interpret exact active ImportScope chains, TypeSpec aliases, namespace/type alias hiding, namespace imports, global
imports when physically present, constructed `using static` owners/imported nested types, use-site accessibility, and
extern-alias/AssemblyRef correlation. Preserve explicit-route laziness.

**Exit gate**

- Contextual and explicit spellings converge on one construction/field identity with distinct provenance.
- Inner/outer alias, first-viable namespace-level stopping, same-level import accumulation, same-symbol convergence,
  ambiguity, partial PDB, and identity-conflict cases pass.
- Same-level TypeRef/forwarder convergence is admitted only after the compiler/SRM oracle proves one physical symbol.
- Full accessibility and lexical-name matrices either resolve exactly or stop before member/storage selection.
- Display strings never override token/signature identity.
- A fully qualified request performs zero frame/PDB calls.

### W8.6 — exact constructed-runtime mapping, value projection, and static storage

**Scale:** `~10K LOC` implementation and tests.

Map metadata constructions to exact runtime constructions, carry ordered generic/nested identity through declaration
and storage, select per-construction slots, project exact literals without runtime calls, implement every admitted
primitive/enum/nullable decoder, W8.1-selected storage branch, and exact frozen frame-value root; validate constructed
assignability and preserve raw-memory authority for every address-backed value. Register-backed roots remain outside
the admitted contract.

**Exit gate**

- Multiple same-TypeDef/FieldDef constructions return distinct expected slots and values.
- Absent/duplicate/partial/conflicting construction and slot outcomes never cross-fallback.
- Every primitive width/signedness, enum, literal, string, nullable, null, reference, and recursively substituted field
  shape passes; literal plans prove zero runtime/storage/memory calls.
- Constructed invariant/variant interface/base and admitted array assignability agree with runtime/compiler oracles.
- Every admitted thread-relative/RVA/frame-value branch passes end to end; context-relative storage, register homes,
  and selected-frame generic arguments retain their executable stops and expose no placeholder API.
- High-level runtime/reflection calls remain late oracles only.

### W8.7 — lexical completeness and `using static` bare roots

**Scale:** `~10K LOC` implementation and tests.

Project local variables/constants, parameters, type parameters, local functions, current/enclosing members/types, and
relevant name catalogs; freeze the lexical completeness certificate; implement current-type and directly declared
accessible using-static field lookup plus typed shadow stops. Bind the admitted frame profile's `this` or identifier to
one exact live frozen memory location and allow a direct root result or unchanged suffix without static fallback.

**Exit gate**

- Blocker-free constructed-owner stored/literal primitive/reference cases and imported nested-type heads are exact.
- Every local/parameter/member/type/alias shadow class and every incomplete catalog stops before member/storage
  selection.
- Multiple imported fields are grouped by physical identity and remain ambiguous when distinct.
- Inherited using-static members are not imported, matching the declared language rule.
- An admitted frame-value root proves exact name/scope/liveness/location/type and never invokes static binding; every
  duplicate, dead, partial, unsupported, or missing location stops before a read.

### W8.8 — product composition and generated conformance

**Scale:** `~10K LOC` implementation and tests.

Route only explicit V2 or W8.1-admitted frame-value requests through their declared bind/prepare/read/evaluate path,
reuse unchanged W2/W6 suffixes, add append-only consumer/report schemas, and execute the generated cross-product through
hidden full-dump targets. No profile falls through to another.

**Exit gate**

- Same-session, fresh-object, fresh-process, and close/reopen/rebind results are canonical and byte-identical.
- Direct, conditional, and compatible fallback suffixes preserve W2/W6 semantics and identities.
- Context/symbol/construction/storage/value/suffix outcome axes remain independent.
- All managed processes are hidden and every managed command uses the headless wrapper.

### W8.9 — thirty-two core incidents plus admitted branches: meaningful synthetic portfolio and decision

**Scale:** `~10K LOC` implementation, tests, dumps, and reports.

Materialize the frozen four-shape corpus, execute one fresh hidden consumer per incident plus two fresh portfolio
consumers, validate raw counts/counterfactual decision facts, and select exactly one qualified action or defer.

**Exit gate**

- The thirty-five minimum dumps—thirty-two core plus thread-relative, RVA-backed, and frame-value incidents—execute
  across four materially distinct shapes with distinct snapshot identities.
- Both portfolio reports are byte-identical; representative counts remain zero; promotion is rejected.
- Attributable-stage, usefulness, decision, shape, and boundary counts are independently validated.
- A substantive tie defers; W8 implements no selected successor action.

### W8.10 — repository and hosted closure

**Scale:** `~100 LOC` documentation, plus corrections exposed by final evidence.

Close only after the exact pushed source commit passes the complete local headless matrix and every required hosted job
executes and passes, unless the owner records a new W8-only disposition. Earlier milestone dispositions are not W8
evidence.

**Exit gate**

- Locked restore and strict Release solution build pass with zero warnings/errors.
- Complete unit, Fast, ordinary dump, optimized context, focused V2, generated conformance, and meaningful synthetic
  lanes pass with zero skips.
- Every W1–W7 regression and canonical golden remains unchanged except explicit new target identities.
- Markdown, headless-workflow, authored-scope vocabulary, one-parser-site, public-surface, and clean-tree guards pass.
- The exact closure commit is pushed and current documents distinguish design, implementation, local validation, and
  hosted evidence accurately.

## 10) Verification matrix

| Lane | W8 proof |
|---|---|
| Unit | V2/conditional-profile invariants, canonical bytes, bounds, TypeSpec/constraints/substitution, scope/member/access rules, result axes |
| Fast | SRM/PDB/compiler differentials, context poisons, runtime/assignability projection seams, literal no-call ordering |
| Ordinary generated dump | Definition kinds, constructions/slots/literals, explicit/contextual/bare/frame routes, values/suffixes, reopen replay |
| Optimized context | Existing five-axis report remains stable; only an explicit V2 schema may add new facts |
| Meaningful synthetic | Fixed thirty-two independent core dumps, four shapes, one independent dump per admitted branch, corrected decision metrics, zero representative rows |
| Compatibility | All W1–W7 profiles, manifests, reports, canonical bytes, and default routes remain golden |
| Repository | Markdown, headless workflow, authored vocabulary, one parser, XML docs, strict build, clean tree |

Expected managed command shape:

```powershell
.\eng\Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode
.\eng\Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --verbosity minimal --maxcpucount:1 --disable-build-servers --property:UseSharedCompilation=false --property:ContinuousIntegrationBuild=true
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast" --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=StaticFieldExpressionV2" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W8MeaningfulSyntheticV1" --verbosity normal
```

Repository scripts then verify Markdown links and workflow headlessness. The authored-scope vocabulary guard excludes
immutable upstream snapshots and required literal framework identifiers under the existing documented caveat.

## 11) Completion definition

W8 is complete only when all mandatory V2 forms share one proven end-to-end pipeline from the sole Roslyn parse to an
exact or typed non-exact result; the fully qualified construction route remains independent of frame/PDB evidence;
TypeSpec aliases and runtime constructed statics carry exact ordered identity; `using static` requires lexical
completeness; constraints, accessibility, definition-kind lookup, direct/inherited lookup, field substitution, and
constructed assignability are frozen; address-backed values remain raw-memory evidence, register homes remain
non-admitted, and literals remain exact metadata with proven zero runtime calls; every W8.1 branch has its admitted
implementation or typed executable non-admission;
suffix evaluation remains unchanged; all tests are headless; the complete generated corpus and synthetic portfolio of
thirty-five minimum incidents replay; and the exact pushed closure commit
satisfies repository and hosted governance.

Completing only the easiest alias or generic spelling does not close W8. Conversely, failure of an evidence-conditioned
stack/thread/context/RVA probe does not block the mandatory stored/literal V2 core when the repository records the exact
evidence gap, omits the unproven API branch, and retains a typed non-admission instead of guessing.

## 12) Delivery discipline

- Commit and push every completed checkpoint before beginning the next.
- Use detailed multi-line commit messages that record decisions, evidence, tests, compatibility, and remaining bounds.
- Update coarse logarithmic LOC bands whenever implementation changes the apparent order of magnitude.
- Use no duration estimates in plans, status, commits, or handoff notes.
- Run every managed command and helper/target/consumer process headlessly.
- Preserve unrelated user work and do not rewrite closed milestone evidence to make the active stage look cleaner.
- Treat documentation as part of the contract: update active navigation, traceability, architecture, product, testing,
  and integration surfaces at the checkpoint that changes their truth.
