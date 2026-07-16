# Post-W5 Path Forward: W6 Bounded Member-Chain Query

> **Lifecycle:** Current · **Roadmap:** Active
>
> **Decision:** implement one fixed-depth, null-aware member-chain query slice selected by W5.5b; no other successor
> is pre-approved.
>
> **Evidence boundary:** W6 is a prototype-design milestone grounded in predeclared synthetic incidents. It does not
> establish field readiness, and W5's hosted-gate exception does not carry forward.

## 1) Executive decision

W5 closed the expression-to-result product path and then measured twelve independently dumped incident questions
across request-pipeline and batch-pipeline shapes. Four questions stopped first at member navigation, compared with
three at context acquisition and one at method execution. The deterministic W5.5b ranking therefore selected
`AdmitFixedDepthMemberChain`.

The next stage is **W6 — bounded member-chain query**:

```csharp
root.Failure.Code
root.Failure?.Code ?? "<none>"
root.CurrentRequest.Status
root.LastFailure?.Code ?? "<none>"
root.Progress.State
root.Progress.CompletedPartitions
```

W6 adds one reference hop and one certified terminal data-member read to the derived-query path. It does not add general expression
binding, arbitrary graph walking, a new interpreter opcode, another method shape, or a debugger subsystem. The
existing W2 one-field query and W5 method expression remain byte-for-byte stable and continue through their existing
paths.

This is deliberately more than a parser increment. A truthful member chain requires:

1. a closed syntax and canonical chain identity;
2. declared-type binding of the terminal data member even when the intermediate reference is exactly null;
3. a counted raw-memory observation of the intermediate reference;
4. a non-root identity for the referenced object, without inventing root provenance;
5. an immutable plan that freezes both member selections before evaluation and never rebinds them;
6. exact, null, partial, unavailable, conflict, invalid, and direct-null outcomes that remain distinct; and
7. same-session, fresh-session, and dump-close/reopen canonical replay through the headless product consumer.

W6 is estimated at `~10K LOC` and is split into `~1K LOC` vertical checkpoints. This detailed contract and navigation
reset is itself `~1K documentation LOC`; a later small closure reconciliation remains `~100 documentation LOC` scale.
These are order-of-magnitude implementation scales, not schedule forecasts.

## 2) Evidence basis and success question

### 2.1 What W5 established

The W5.5b designed portfolio recorded:

| Evidence fact | Raw count |
|---|---:|
| Independent dumps / incident questions | 12 / 12 |
| Structural application shapes | 2 |
| Admitted questions | 8 / 12 |
| Exact answers | 4 / 12 |
| Useful answers among partial-or-unknown outcomes | 2 / 3 |
| Decision-changing answers | 6 / 12 |
| Member-navigation blockers | 4 |
| Context-acquisition blockers | 3 |
| Execution-body blockers | 1 |
| Representative or external observations | 0 |

The four member-navigation questions were:

- `root.Failure.Code`;
- `root.CurrentRequest.Status`;
- `root.LastFailure.Code`; and
- `root.Progress.State`.

Each had an exact selected root, and the predeclared portfolio classified the required terminal-member evidence as
exact. W5 stopped at syntax and did not bind those terminal members, so that classification is a scenario assertion,
not validation evidence. W6 must prove or refute it through an emitted-shape gate before implementing value reads.

That gate matters because the current terminal names are positional-record auto-properties: `Code`, `Status`,
`State`, and `CompletedPartitions` are not FieldDef names. Their storage is compiler-generated. Rewriting those
fixtures into easier direct fields after they selected the roadmap would change the question to fit the implementation.
W6 instead admits one narrowly certified field-backed data-property shape, reads its correlated storage directly, and
still excludes arbitrary property behavior. This keeps the literal W5 questions intact.

The missing capability is therefore the product path from one exact outer reference through either a direct terminal
field or a certified terminal data property. This remains a better next experiment than adding unrelated IL, context
discovery, or presentation behavior.

### 2.2 The W6 product question

W6 asks:

> Can one exact dump root, one ordinary object-reference field, and one terminal supported field be bound and
> evaluated as a deterministic derived query, including honest null and degraded-evidence behavior, without caller-
> assembled runtime structure or repeated member lookup?

The milestone succeeds only if an external headless consumer can answer that question against real generated dumps
and a predeclared multi-shape synthetic portfolio can use the resulting answers to choose the following prototype
direction.

### 2.3 What the evidence does not establish

The W5 corpus is designed evidence over source-controlled fixtures. It does not establish how often a member chain is
available or useful in external incidents. W6 may advance prototype design on the strength of the selected synthetic
decision, but it must continue to report a representative/external-observation denominator of zero unless separately
qualified observations are supplied later.

## 3) Scope lock

### 3.1 Opt-in language profile and admitted grammar

W6 adds exactly one opt-in language profile, `FixedDepthMemberChainV1`. Existing APIs and manifest schema versions
continue to default to the frozen W5 language profile. In particular, the four historical W5.5b rows remain
`Unsupported`, their report bytes/counts remain unchanged, and `w5-usefulness-meaningful-synthetic-v2.json` is never
rewritten to look retrospectively admitted.

Only when the caller or versioned W6 manifest explicitly selects `FixedDepthMemberChainV1` does classification try
this grammar after the unchanged W2 and W5 classifiers:

```text
chain        := root-name "." reference-member hop terminal-member coalesce?
hop          := "." | "?."
coalesce     := "??" literal
literal      := null | Int32-literal | string-literal
```

Additional rules:

- There are exactly two member identifiers after the root: one intermediate reference and one terminal data member.
- The root-to-reference operator is always ordinary `.` because W6 still requires one exact non-null host-selected
  root.
- The second operator is ordinary `.` or null-conditional `?.`.
- `??` is optional and consumes only the existing W2 literal set.
- Identifiers, casing, whitespace positions, expression length, identifier length, and decoded string-literal length
  follow the existing bounded W2 lexical rules.
- Classification is syntax-only. It performs no field catalog traversal and no dump-memory read.
- Classifier precedence inside the opt-in profile is unchanged W2 first, the exact W5 method form second, and the W6
  chain grammar third. The frozen W5 profile never tries the third branch. A W6 addition must not change which existing
  expression path is selected.

The milestone examples are normative scenario inputs, not a promise that arbitrary members with the same spelling
shape will bind.

### 3.2 Admitted object and data-member shape

A W6 plan may bind only when all of the following are exact:

- the existing host-selected root binding;
- one directly declared ordinary instance field on the root's exact runtime type;
- that field's ordinary managed object-reference classification and declared reference type;
- one directly declared terminal data member on that declared reference type; and
- a terminal storage decoder already implemented by W2: `Int32`, `Nullable<Int32>`, or `String`.

The terminal data member is either:

1. a directly declared ordinary instance FieldDef; or
2. a directly declared, non-indexed instance PropertyDef whose getter is certified as a trivial field projection.

A property certificate requires all of the following from counted dump metadata and physical method-body evidence:

- one exact ordinal PropertyDef and associated non-static, non-generic, zero-parameter getter MethodDef on the exact
  declared TypeDef;
- exact property/getter/return signatures agreeing with one existing W2 terminal decoder;
- one unique same-TypeDef instance backing FieldDef with the same value type;
- an exact admitted getter body equivalent to `ldarg.0; ldfld <that FieldDef>; ret`, with no locals, EH, prefixes,
  branches, calls, or additional behavior;
- exact runtime storage whose FieldDef token agrees with the certified backing field; and
- one immutable certificate retaining PropertyDef, getter, body, backing FieldDef, type, and storage identities.

The getter is never invoked. The property certificate proves that reading the backing storage is the exact data
projection requested by the member name. A name pattern such as `<Name>k__BackingField` is neither necessary nor
sufficient by itself.

The intermediate declared type and an exact non-null runtime target must match ordinally by snapshot, module, type
name, and non-nil TypeDef identity. W6 does not perform base-type, interface, variance, proxy, or derived-type member
binding. A later evidence-selected slice may revisit that rule.

The terminal member and, for a certified data property, its backing storage are selected from the intermediate field's
declared type during preparation, not from the eventual runtime object during evaluation. This is essential for three
reasons:

1. an exactly null `?.` receiver must still reject a nonexistent or incompatible terminal member; and
2. evaluation must never repeat field/property catalog lookup or silently select a different member on a runtime
   subtype; and
3. the W5-selected record properties must be admitted from physical evidence rather than inferred from spelling.

### 3.3 Admitted values and transformations

W6 reuses `DumpQueryValue` without adding a value kind:

- exact or partial `String`;
- exact `Int32`; and
- exact `Null`, including a null-conditional short circuit or a null terminal `String`/`Nullable<Int32>`.

Literal coalescing remains a deterministic derived-query transformation. It applies only to an exact null. It never
turns a partial, unavailable, conflicting, invalid, or direct-null-receiver outcome into a concrete fallback.

Null-conditional access deliberately lifts a non-nullable `Int32` terminal to `Int32-or-null`, so
`root.ActiveJob?.RetryCount ?? 0` is admitted even though unchanged W2 correctly rejects `??` on a direct non-nullable
`Int32`. Effective compatibility is:

| Terminal storage | Direct `.` result | Conditional `?.` result | Compatible fallback |
|---|---|---|---|
| `Int32` | non-null `Int32` | `Int32` or null | none for direct; `Int32` or null for conditional |
| `Nullable<Int32>` | `Int32` or null | `Int32` or null | `Int32` or null |
| `String` | string or null | string or null | string or null |

### 3.4 Explicit exclusions

W6 does not admit:

- three or more member identifiers after the root;
- a null-conditional root, repeated null-conditional hops, or a conditional operator;
- computed, indexed, static, inherited, virtual, interface-dispatched, or otherwise uncertified properties/getters;
- arbitrary methods, constructors, extension methods, or overload resolution;
- indexers, arrays, dictionaries, collections, enumeration, or LINQ;
- static, thread-static, frame, local, argument, or register context;
- inherited or interface member lookup, derived runtime targets, casts, conversions, or generics;
- arithmetic, comparisons, Boolean operators, assignments, statements, or user-defined operators;
- new IL opcodes, branches, handler transfer, allocation, virtual dispatch, or new pure models;
- implicit artifact lookup, source/PDB lookup, or caller-supplied metadata handles;
- a stable shipping command-line or public compatibility promise; or
- any representative-usefulness or field-readiness claim.

An expression outside the closed grammar is `Unsupported`, not malformed merely because W6 does not implement it.
Captured evidence that violates a supported invariant remains `Invalid`; available evidence that disagrees remains
`Conflict`.

## 4) Normative staging and ownership

### 4.1 Three-stage query contract

W6 preserves the W2/W5 separation between syntax, binding, and value reads:

| Stage | May do | Must not do | Success artifact |
|---|---|---|---|
| Classify | Parse the closed grammar, apply lexical bounds, freeze raw syntax/operators/literal | Traverse metadata, inspect a runtime type, or read dump memory | Canonical W6 chain request |
| Prepare | Validate the exact root; bind the outer field, declared intermediate type, terminal field or certified data property, and physical terminal storage once; freeze all descriptors and bounds | Read the reference value, inspect the referenced runtime object, execute a getter, or decode the terminal value | Immutable complete member-chain plan |
| Evaluate | Read the frozen outer reference, short-circuit or validate the referenced target, compute the frozen terminal storage location, decode it, and apply exact-null coalescing | Repeat root selection, field/property lookup, property certification, declared-type lookup, or syntax parsing | Derived-query result |

Every failure exposes only the evidence accumulated through its stopping boundary. No partial plan escapes
preparation.

### 4.2 End-to-end data flow

```mermaid
flowchart LR
    E["Raw expression + exact root + policy"] --> C["W2 / W5 / W6 syntax classification"]
    C -->|"opt-in W6 chain"| P["Bind outer field + declared target + terminal field/property certificate"]
    P --> I["Freeze canonical member-chain plan"]
    I --> R["Counted outer-reference read"]
    R -->|"exact null + ?."| N["Exact null / exact fallback"]
    R -->|"exact non-null"| V["Validate non-root target identity"]
    V --> L["Read frozen terminal storage; never invoke getter"]
    L --> O["Derived-query result"]
    R -->|"partial / unavailable / conflict / invalid"| X["Typed non-value outcome"]
```

### 4.3 Physical ownership

W6 adds no project. Responsibilities remain aligned with the current fourteen-project solution:

| Project / area | W6 responsibility |
|---|---|
| `Interpreter.Product.DumpQuery` | Separate closed chain parser, immutable parsed shape, declared-member plan, derived-query evaluator, null/coalesce semantics, canonical plan replay; W2 parser/plan remain unchanged |
| `Interpreter.Host.Dump.ClrMD` | Immutable declared-type/field/property certificate projection from SRM plus dump evidence, counted object-reference observation, exact non-root target identity, descriptor-consuming terminal reads |
| `Interpreter.Product.DumpDebugging` | Explicit opt-in language profile, append-only expression-kind routing, W6 request identity, product outcome projection, preservation of W2/W4 payloads |
| `Interpreter.Headless.ReferenceConsumer` | Versioned W6 manifest/report execution and usefulness reporting; frozen W5 manifests/reports remain unchanged; no reusable query semantics |
| `Interpreter.TestTarget` | Source-controlled multi-shape object graphs and readiness oracles |
| Unit/integration corpus | Parser laws, adapter evidence, plan/evaluation semantics, real dumps, fresh-process replay, and usefulness decision |

Illustrative type names in this plan express ownership, not a frozen public API. Any public prototype type or method
introduced during implementation requires complete XML documentation and an explicit draft-phase caveat.

## 5) Identity, evidence, and replay contract

### 5.1 Existing identities remain frozen

For every previously admitted or rejected W2/W5 input:

- W2 request, plan, result bytes, projections, and SHA-256 values remain byte-identical;
- W5 policy, request, method-expression, result, and report identities remain byte-identical; and
- the existing nine-row generated corpus and twelve-row W5.5b corpus remain replayable without schema rewriting.

W6 uses an explicit opt-in language-profile identity, an append-only expression kind, and a separately tagged chain
identity. Existing overloads/default manifests remain on the frozen W5 profile. A new encoding may add a profile or
chain payload only for the opt-in request; it must not append even a false/default marker to old canonical byte
sequences. Golden legacy tests prove this before W6 evaluation code lands.

### 5.2 Chain request identity

The canonical W6 chain request includes:

- its own schema and grammar identity;
- the explicit `FixedDepthMemberChainV1` profile identity;
- the exact raw expression, including whitespace;
- root name and the complete existing exact root binding;
- ordered intermediate/terminal identifiers;
- direct versus null-conditional hop;
- the complete optional literal kind and payload;
- every actually reached parser bound; and
- the unchanged product policy identity supplied with the request.

Syntactically bounded unsupported/invalid inputs retain a canonical raw request where the existing W5 issuance rules
permit one. Oversized input and non-exact roots continue to have no issued canonical request.

### 5.3 Declared member-chain plan identity

A successful plan freezes:

- the chain request identity;
- root snapshot/module/type/object and root-selection evidence;
- the complete outer field descriptor and absolute storage address;
- the intermediate declared module and TypeDef identity;
- the public terminal member identity: either one FieldDef or one PropertyDef/getter/body certificate;
- the complete terminal storage FieldDef/type descriptor relative to the declared intermediate type;
- for a certified property, the correlated getter body and backing-field identity proving the data projection;
- the terminal decoder kind;
- direct/null-conditional and coalesce semantics;
- every applied root, parser, field-catalog, type, and read-size bound that preparation actually reached; and
- an explicit declaration that the runtime reference value and terminal value have not yet been read.

The terminal descriptor must carry enough relative-layout information for evaluation to derive its absolute address
after validating an exact non-null target. Evaluation must not call `GetInstanceField`, enumerate a field catalog, or
resolve a metadata token.

### 5.4 Non-root object identity

The current root object projection carries a handle slot and handle kind. A referenced object has neither. W6 must not
populate those properties with the parent field address or another invented value.

Introduce a distinct immutable referenced-object projection, or factor an internal common object identity used by
rooted and referenced projections while preserving every existing public and canonical root property. The W6
referenced identity contains at least:

- snapshot and memory-source identity;
- target object address;
- exact runtime type name, TypeDef, method table, module identity, and validated object size/extent;
- parent object identity and frozen outer FieldDef/storage identity;
- the counted reference-pointer read; and
- counted object-header/type-validation evidence actually used.

This identity is path-specific: two fields that point to the same object have distinct selection provenance even
though their target object identities agree.

### 5.5 Replay obligations

For every successful or typed terminal corpus row:

- repeated evaluation of the same immutable plan in one session performs no member lookup and reproduces canonical
  result bytes;
- a fresh product object over the same open session reproduces request, plan, reference observation, and result;
- complete dump close/reopen, root rediscovery, and plan reconstruction reproduce all canonical artifacts; and
- a plan or descriptor from a different snapshot fails with typed conflict before decoding a value.

Target addresses remain valid replay material only within the content-identified immutable snapshot. They are never
global object identities.

## 6) Result semantics

### 6.1 Semantic mode and effects

Every successful W6 value is `DerivedQuery` with `None` effects. No W6 result claims user IL execution, historical
execution, abstract analysis, or a target write.

The product facade may reuse `EvaluationResult<DumpQueryValue>` because W6 uses the same truth mode and value union as
W2. It must still retain `DumpExpressionKind.MemberChain` in the request so grammar admission is not confused with the
unchanged W2 single-field path.

### 6.2 Required outcome matrix

| Intermediate reference evidence | Hop | Terminal evidence | Coalesce | Required outcome |
|---|---|---|---|---|
| Exact non-null, exact matching target | `.` or `?.` | Exact non-null value | absent | Completed, complete, exact value |
| Exact non-null, exact matching target | `.` or `?.` | Exact null | absent | Completed, complete, exact `Null` |
| Exact non-null, exact matching target | `.` or `?.` | Exact null | compatible literal | Completed, complete, exact literal plus coalesce provenance |
| Exact null | `?.` | Declared leaf was prepared; value read not reached | absent | Completed, complete, exact `Null`; no target or leaf read |
| Exact null | `?.` | Declared leaf was prepared; value read not reached | compatible literal | Completed, complete, exact literal; no target or leaf read |
| Exact null | `.` | Value read cannot continue | any | `DerivedQuery`; blocked, no value, exact evidence, stable `QUERY_CHAIN_NULL_RECEIVER`; coalesce does not run |
| Partial pointer | either | Not reached | any | Blocked, no value, partial evidence; coalesce does not run |
| Unavailable pointer | either | Not reached | any | Blocked, no value, unavailable evidence; coalesce does not run |
| Exact pointer, runtime target disagrees with declared target | either | Not reached | any | Conflict, no value; leaf address is not derived |
| Exact pointer, invalid target structure | either | Not reached | any | Invalid, no value; leaf address is not derived |
| Exact matching target | either | Partial string prefix | any | Completed, partial prefix, partial evidence; coalesce does not run |
| Exact matching target | either | Unavailable leaf | any | Blocked, no value, unavailable evidence; coalesce does not run |
| Exact matching target | either | Conflicting/invalid leaf | any | Typed conflict/invalid result with no value |

The direct-null row remains an admitted `EvaluationResult<DumpQueryValue>` rather than a classification or facade
failure. Its separate completion/evidence axes say that evaluation could not continue even though the receiver state
was exact. The stable diagnostic, request/plan identity, pointer read, null hop index, context, and provenance preserve
the distinction from missing evidence, exact null produced by `?.`, and invalid input.

### 6.3 Preparation failures

Preparation returns no plan when:

- the outer member is missing, ambiguous, inherited, static, or not an ordinary object reference;
- the declared target type is unavailable, conflicting, invalid, generic, or outside the exact-type rule;
- the terminal member is missing, ambiguous, inherited, static, or outside the three existing decoder kinds;
- a PropertyDef is indexed, behavior-bearing, lacks exact getter/body/backing storage evidence, or fails any certificate
  correlation rule;
- the coalescing literal is incompatible with the terminal result type;
- a catalog/type/layout bound is exhausted; or
- any frozen identity or relative storage range is inconsistent.

These are typed preparation failures. They do not receive `DerivedQuery`, `CounterfactualExecution`, or another
fabricated semantic mode.

### 6.4 Read and traversal accounting

W6 reports only bounds actually reached:

- existing expression/root/identifier/literal bounds;
- one outer field-catalog traversal;
- one declared-target terminal field/property-catalog traversal;
- counted getter metadata/body acquisition when the terminal name resolves as a property;
- one fixed reference-hop/depth bound;
- one pointer-width raw read when evaluation reaches the reference;
- object-header/type-validation reads for an exact non-null target; and
- existing terminal read and observed-string bounds when evaluation reaches the leaf.

An exact null-conditional short circuit reports the pointer read but no target-validation or terminal-value read. A
syntax or preparation failure reports no value-read bound. A partial pointer read reports its requested and observed
width and stops.

## 7) Delivery sequence

### W6.0 — contract and roadmap freeze

**Scale:** `~1K LOC` documentation.

Publish this plan, link it from active navigation, add the planned traceability requirement, and freeze the grammar,
semantics, exclusions, and closure rule before implementation.

**Exit gate**

- W5 remains closed and historical evidence is not rewritten.
- All current status surfaces name W6, not W5, as active delivery.
- The selected examples, certified-property dependency, outcome matrix, checkpoint sequence, and post-W6 decision
  gate agree.
- Markdown links, headless-workflow checks, and repository caveat vocabulary checks pass.

### W6.1 — emitted-shape and fixture truth gate

**Scale:** `~1K LOC`.

Freeze the actual emitted forms selected by W5 before admitting new syntax. Prove the current positional-record
PropertyDefs/getters/backing FieldDefs/getter bodies from physical target evidence. Add the coordinator and workflow/
dispatch graphs needed by the W6.6 portfolio now, then intentionally refresh the current TestTarget PE and every
derived W4/W5 graph identity once. Preserve older hashes as commit-scoped evidence and accept new goldens only after
relational metadata/body facts pass.

**Required evidence**

- exact PropertyDef/getter/backing FieldDef/signature/body facts for `Code`, `Status`, `State`, and
  `CompletedPartitions`;
- exact proof that each admitted getter is the one frozen trivial load/return profile and no broader getter shape;
- negative computed, indexed, static, inherited, mismatched-field, extra-instruction, call, branch, local, and EH
  property profiles;
- complete relational target-fixture facts before any PE/hash refresh;
- one explicit current PE/derived-identity refresh with historical hashes left untouched; and
- no chain parser, product admission, or terminal value read yet.

### W6.2 — opt-in syntax, request identity, and routing

**Scale:** `~1K LOC`.

Add `FixedDepthMemberChainV1`, the separate exact two-member parser, append-only expression kind, canonical chain
identity, and syntax-only routing. Keep the W2 parser, exact W5 method spelling, default API profile, W5 manifests, and
W5 report bytes/counts unchanged.

**Required evidence**

- direct and null-conditional forms, optional literals, whitespace, casing, and all lexical bounds;
- rejection of one-member duplication, three-hop chains, repeated `?.`, calls, indexers, and suffixes;
- syntax accepts a member name without pretending to know whether metadata later binds it as a field or property;
- exact unsupported-versus-invalid diagnostic classification;
- zero calls to metadata, field, memory, method, or execution capabilities during classification;
- byte-identical legacy W2/W5 request, plan, result, manifest, and report fixtures, including the four historical
  W5.5b rows remaining unsupported under the default profile; and
- same/fresh-object canonical opt-in chain-request replay.

### W6.3 — declared reference target and terminal data-member certificate

**Scale:** `~1K LOC`.

Extend the ClrMD/SRM boundary with immutable projections for an outer reference field's declared target type and one
terminal direct field or certified field-backed property. Freeze TypeDef, FieldDef, PropertyDef/getter/body, decoder,
and relative storage identities without reading the outer reference value or executing the getter.

**Required evidence**

- exact request/batch fixture target types and their certified `Code`, `Status`, `State`, and `CompletedPartitions`
  data properties, plus direct terminal-field conformance rows;
- exact counted dump metadata and physical getter-body evidence; an independently identified disk PE may be a late
  equality oracle but never the source of the certificate;
- PropertyDef/getter/backing FieldDef/signature/body correlation and direct-declaration enforcement;
- ordinal field/property duplicate detection;
- terminal decoder classification for `String`, `Int32`, and `Nullable<Int32>` only;
- missing, inherited, indexed, static, computed, unsupported type/body, partial catalog/body, unavailable, conflict,
  invalid, and limit outcomes;
- counting/poisonable capability tests proving no reference or leaf value read; and
- canonical projection equality, defensive immutability, foreign-snapshot rejection, and complete XML documentation.

### W6.4 — reference observation, non-root identity, and immutable plan

**Scale:** `~1K LOC`.

Add a counted object-reference field observation, validate exact non-null targets without giving them fabricated root
facts, and freeze the complete member-chain plan. Retain validated target size/extent and relative terminal/nullable
child layouts so evaluation can checked-add and range-check every address. Factor only the minimal common internal
object identity needed by root and referenced-object descriptor-consuming reads; preserve all existing public and
canonical root shapes.

**Required evidence**

- exact non-null and exact null pointer decoding at both supported pointer widths where the adapter contract permits;
- partial/unavailable pointer reads with retained prefixes and no pointer fabrication;
- conflicting owner/snapshot/non-assignable type, invalid pointer/object/header/layout, and overflow-safe address
  derivation;
- a valid assignable derived target reported as an exact-evidence unsupported shape rather than a conflict;
- distinct selection provenance for aliasing fields that point to one object;
- complete plan identity with no partial plan on failure;
- poison tests proving preparation performs no pointer or terminal-value read; and
- poison tests proving evaluation never repeats member/type catalog lookup.

### W6.5 — derived-query evaluation, product facade, and headless conformance

**Scale:** `~1K LOC`.

Evaluate the frozen plan through the strict matrix in section 6. Reuse the W2 terminal decoders and value union through
descriptor-consuming storage overloads; never invoke a certified getter, duplicate decoding logic, or re-enter W2
parsing/preparation. Route the opt-in result through the existing facade and a versioned W6 consumer manifest/report.

**Required evidence**

- exact direct and conditional string/int/nullable results;
- exact intermediate null with and without coalescing, and zero target/leaf reads after the short circuit;
- a direct-null receiver result that is blocked with no value, exact pointer evidence, and its stable diagnostic;
- partial/unavailable references and leaves, partial string prefixes, conflict, invalid, and incompatible coalescing;
- coalescing only after exact null, never after uncertainty;
- complete independent result axes, ordered provenance, and exact reached-bound sets;
- repeated/same-session/fresh-session canonical replay; and
- exact output for the four W5-selected questions while the historical W5 profile/report still shows them unsupported;
- the exact selected answers are `request-failed`, `running`, `batch-failed`, and `running` for the frozen W5 target
  arguments, with property/getter/storage provenance retained rather than inferred;
- one expression, exact root, and policy enter the consumer; no caller supplies field descriptors, target addresses,
  plans, or adapter internals;
- fresh hidden target and consumer processes for every row;
- exact human/machine reports that omit target-derived values from diagnostics and shape-only logs;
- repeated evaluation, fresh process, and dump close/reopen/rebind produce byte-identical artifacts;
- unsupported W6 syntax and all typed failures remain present in the report rather than being dropped; and
- every managed launch uses `eng/Invoke-HeadlessProcess.ps1` or equivalent hidden no-UI process creation.

### W6.6 — meaningful synthetic portfolio and next decision

**Scale:** `~1K LOC`.

Create a predeclared schema-v3 portfolio with twenty-four independent dumps and questions across four structurally
different application graphs. Extend the usefulness runner append-only with a W6 admission path and post-W6 blocker
vocabulary; do not rewrite W5 schema-v2 reports or counts.

The portfolio must be complex enough to exercise real object-graph distinctions, not twenty-four value substitutions on
one shape. The initial scenario design is in section 8.

**Required evidence**

- one full independent snapshot and one question per incident, with twenty-four distinct snapshot hashes;
- four distinct root types and materially different optional/non-optional/reference/leaf layouts;
- predeclared target arguments, root selector, expression, evidence view, expected outcome/value or prefix, usefulness,
  decision impact, and first stopping boundary;
- exact, null-short-circuit, fallback, partial, unavailable, conflict, invalid, direct-null, and unsupported rows;
- two fresh portfolio processes produce byte-identical machine and human reports;
- raw counts only, with generated, synthetic, and representative corpus kinds still non-promotable; and
- a unique post-W6 prototype action selected only if it clears the evidence threshold in section 10; otherwise the
  successor remains explicitly deferred rather than being chosen by enum order or architecture taste.

### W6.7 — repository closure

**Scale:** `~100 LOC` documentation.

Reconcile every authoritative document, run the complete headless matrix, record the coarse realized scale, and close
W6 only at the exact pushed commit that satisfies its required gates.

**Exit gate**

- locked restore and strict Release build pass for the complete solution with zero warnings/errors;
- complete unit, Fast, ordinary dump, optimized dump, focused W6, generated corpus, and meaningful synthetic lanes
  pass with zero skips;
- W2/W4/W5 golden identities and reports remain byte-identical;
- Markdown and headless-workflow guards pass;
- the exact pushed commit's required hosted jobs actually execute and pass; and
- the selected post-W6 action is documented but not silently implemented as W6 work.

W5's 2026-07-16 hosted-only exception applies only to W5. W6 remains open if its hosted jobs do not execute, unless
the owner gives a new explicit milestone-specific disposition.

## 8) Synthetic scenario design

### 8.1 Structural shapes

The W6.6 portfolio starts with four source-controlled graph families:

1. **Request pipeline** — optional `Failure`, required `CurrentRequest`, strings, correlation state, and retry data.
2. **Batch pipeline** — optional `LastFailure`, required `Progress`, string and integer leaves, and partition data.
3. **Coordinator pipeline** — optional owner/job references, a deeper shard/health branch, worker collection, and one
   zero-argument status method retained as an unsupported comparison case.
4. **Workflow/dispatch pipeline** — current-attempt and optional-error references, worker assignment, attempt
   collection, and a second zero-argument status method independent of the coordinator shape.

The added shapes must not inherit from either existing root or each other. They distinguish fixed-depth success from
deeper navigation, collection, context, and call blockers after W6 lands. They are frozen in W6.1 so later corpus work
does not cause unexplained target-artifact churn.

### 8.2 Predeclared twenty-four-row baseline

The implementation may refine names and values before the W6.6 fixture-freeze commit, but it must preserve these
semantic rows and publish their final expected artifacts before evaluating them:

| # | Shape | Expression / condition | Expected W6 boundary |
|---:|---|---|---|
| 1 | Request | `root.CurrentRequest.Status`, exact non-null reference | Exact string |
| 2 | Request | `root.Failure?.Code`, exact non-null reference | Exact string |
| 3 | Request | `root.Failure?.Code ?? "<none>"`, exact null reference | Exact fallback, no target/leaf read |
| 4 | Request | `root.Failure.Code`, exact null reference | `DerivedQuery`, blocked/no value/exact evidence, `QUERY_CHAIN_NULL_RECEIVER` |
| 5 | Request | `root.Failure?.Code ?? "<none>"`, partial pointer | Partial/blocked; fallback not applied |
| 6 | Request | `root.CurrentRequest.Status`, partial terminal string | Partial prefix with complete provenance |
| 7 | Batch | `root.Progress.State`, exact non-null reference | Exact string |
| 8 | Batch | `root.Progress.CompletedPartitions`, exact non-null reference | Exact `Int32` |
| 9 | Batch | `root.LastFailure?.Code`, exact null reference | Exact null |
| 10 | Batch | `root.LastFailure?.Code ?? "<none>"`, exact non-null reference | Exact code; fallback not applied |
| 11 | Batch | `root.Progress.State`, unavailable pointer | Unavailable/blocked |
| 12 | Workflow | `root.CurrentAttempt.Status`, certified terminal data property | Exact string on the fourth graph shape |
| 13 | Coordinator | `root.Owner?.Name ?? "<unassigned>"`, exact optional owner | Exact name or exact fallback as predeclared |
| 14 | Coordinator | `root.ActiveJob?.RetryCount ?? 0`, exact supported integer leaf | Exact integer or exact fallback as predeclared |
| 15 | Coordinator | `root.ActiveShard.Health.State` | Unsupported third member hop |
| 16 | Coordinator | `root.Workers[0].State` | Unsupported collection/indexer navigation |
| 17 | Coordinator | `root.CurrentTask.GetState()` | Unsupported method shape |
| 18 | Coordinator | no exact unique root under the declared selector | Typed context-acquisition outcome |
| 19 | Workflow | `root.CurrentAttempt.Status`, unavailable attempt reference | Unavailable/blocked context outcome |
| 20 | Workflow | `root.CurrentAttempt.GetDisplayStatus()` | Unsupported second independently motivated method shape |
| 21 | Request | `root.CurrentRequest.Metadata.Region` | Unsupported third member hop on the first graph |
| 22 | Workflow | `root.CurrentAttempt.Worker.State` | Unsupported third member hop on the fourth graph |
| 23 | Workflow | `root.Attempts[0].Status` | Unsupported collection/indexer on the fourth graph |
| 24 | Batch | `root.PartitionMarkers["primary"]` | Unsupported keyed collection access on the second graph |

Evidence views may mask a real captured read as partial/unavailable or introduce an explicitly conflicting/invalid
test projection, but they may not invent an exact value absent from the dump. Every altered view retains the original
snapshot identity and a stable reason code explaining the transformation.

### 8.3 Generated conformance cases

The generated W6.5 corpus additionally covers mechanical boundaries that should not distort the meaningful incident
denominator:

- maximum expression/identifier/literal lengths and one-over-limit inputs;
- whitespace/casing variants and unsupported suffixes;
- missing, duplicate, inherited, static, unsupported-type, and over-limit members;
- foreign snapshots and descriptors;
- pointer/address overflow and invalid object-header/type facts through injected adapter seams;
- null terminal strings and nullable integers with compatible/incompatible literals;
- alias fields selecting one target with distinct provenance;
- same-plan repeated evaluation with counting capabilities; and
- old W2/W5 golden identity and report regression.

These rows validate the implementation. They contribute nothing to the synthetic or representative usefulness
denominators.

## 9) Verification matrix

All managed commands remain headless. Exact filters may be refined as tests land, but the required coverage layers are:

| Layer | Required proof |
|---|---|
| Parser/classifier unit | Closed grammar, bounds, precedence, diagnostics, zero capability use, legacy identity preservation |
| Adapter unit/integration | Declared-type projection, direct FieldDef or certified PropertyDef/getter/backing-field selection, pointer evidence, target validation, relative address derivation, typed misses |
| Query unit/integration | Complete/no-partial plan, no rebinding, null/coalesce matrix, decoder reuse, result axes, provenance, canonical replay |
| Product Fast | Strict union routing, old W2/W4 payload preservation, preparation/direct-null terminals, deterministic reports |
| Ordinary generated dump | Exact/non-exact reference and leaf evidence, real object graphs, fresh process, close/reopen/rebind |
| Optimized dump regression | Existing optimized-context evidence remains passing; W6 claims only rows explicitly admitted there |
| Meaningful synthetic | Twenty-four isolated dumps, four shapes, raw counts, a threshold-qualified unique decision or explicit deferral, zero representative rows |
| Repository guards | Markdown links, headless workflow, caveat vocabulary, clean strict build |

The expected command shape remains:

```powershell
.\eng\Invoke-HeadlessProcess.ps1 dotnet restore Interpreter.sln --locked-mode
.\eng\Invoke-HeadlessProcess.ps1 dotnet build Interpreter.sln --configuration Release --no-restore --verbosity minimal --maxcpucount:1 --disable-build-servers /p:UseSharedCompilation=false
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.Tests/Interpreter.Tests.csproj --configuration Release --no-build --no-restore --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Fast" --verbosity minimal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W6MemberChainV1" --verbosity normal
.\eng\Invoke-HeadlessProcess.ps1 dotnet test tests/Interpreter.IntegrationTests/Interpreter.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "Category=Dump&Corpus=W6MeaningfulSyntheticV3" --verbosity normal
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-markdown-links.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File .\eng\verify-headless-workflows.ps1
```

Every behavioral filter uses the milestone test selection. A missing platform prerequisite is a failed/open gate, not
a skipped success.

## 10) Post-W6 decision gate

W6.6 ranks first blockers by independent incident count, decision-changing count, useful-answer count, then exact
attributable-evidence count. Enum order may sort equal report rows but may not select a successor. One branch is
selected only when it is the unique leader after all four substantive comparisons and has at least three independent
incidents across at least two application shapes, including at least two decision-changing questions. A tie or a
leader below that floor leaves the successor explicitly deferred.

| Dominant recurring boundary after W6 | Permitted next action |
|---|---|
| Root/context attribution | Admit one concrete context-acquisition scenario; do not add expression depth |
| Third member hop | Admit one depth-three scenario with a separately bounded plan; do not also add collections |
| Collection/indexer navigation | Admit one concrete indexed collection shape; do not also add arbitrary depth |
| Repeated zero-argument method body | Admit only that scenario's complete execution dependency closure |
| Result explanation/consumption | Improve the headless result explanation before adding semantics |
| No recurring useful answer | Stop feature expansion and reconsider the product thesis |

The synthetic decision advances prototype design only. Later external observations may confirm, reverse, or stop it.
Explicit deferral is a valid W6 result; a report must never manufacture a winner merely to name W7. No W7
implementation is pre-approved by this document.

## 11) Completion definition

W6 closes only when all of the following are proven at current state:

- the exact grammar in section 3 is the only new syntax admitted;
- one complete plan freezes the outer field, declared target type, terminal FieldDef or certified data property, and
  physical terminal storage before value evaluation;
- evaluation performs one counted reference read, never fabricates a pointer, and never repeats member binding;
- rooted and referenced object identities remain truthful and distinct;
- every row in the section 6 outcome matrix has executable evidence;
- W2/W4/W5 identities, reports, and behavioral gates remain unchanged;
- the headless generated corpus replays in fresh processes and after dump close/reopen/rebind;
- the twenty-four-incident, four-shape synthetic portfolio runs from independent dumps and either selects one
  threshold-qualified unique next action or records explicit deferral while retaining zero representative rows;
- complete local restore/build/unit/Fast/dump/focused/guard gates pass with zero skips; and
- every required hosted job actually executes and passes at the exact pushed closure commit, absent a new explicit
  W6-only owner disposition.

Until those conditions hold, W6 remains active. Broader navigation, new execution semantics, virtual stepping, and
additional product surfaces remain research.

## 12) Delivery discipline

Each W6 checkpoint is committed and pushed before the next begins. Commit messages record:

- the scenario and contract boundary;
- ownership and canonical-identity effects;
- exact positive, degraded, and negative evidence added;
- exclusions preserved;
- coarse realized LOC scale (`~100`, `~1K`, or `~10K`); and
- headless validation commands and results.

The active plan, requirements traceability, product proposal, architecture overview, testing strategy, integration
plan, and repository navigation are reconciled whenever executable truth changes. Documentation does not claim a
checkpoint implemented merely because this plan names it.
