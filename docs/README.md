# Documentation Index

This directory contains conceptual design artifacts for the IL interpreter and dump-time evaluation project.

## Current delivery focus

The only active product target is a deterministic, read-only expression evaluator grounded in a .NET dump. The W0
repository/toolchain reset and W1–W3 generated-dump vertical slices are implemented in-tree. W1/W2 provide
exact/partial evidence, fully dump-sourced tiny and compiler-emitted fat method bodies, a closed root-field query
grammar, typed snapshot-scoped root bindings, immutable plans that select their field once, exact-null and compatible
coalescing over `String` and `Nullable<Int32>` alongside direct `Int32`, canonical request/root-selection/plan
identities, path-accurate bounds, and honest no-answer completeness. The versioned W2 corpus contains 22 cases over
20 distinct expression texts and reproduces every complete result plus each successfully prepared plan after a fresh
open and rebind. W2 is complete for its milestone-selected scope: [GitHub Actions run
29364905178](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29364905178) passed all four required jobs
at exact closure commit `5bed47100`.

W3 strengthened implementation checkpoint `19c292f9f` adds structural execution identities; SRM-projected static/instance signatures,
initialized locals, and FieldDefs; metadata-derived activation; immutable typed whole-body plans; injected persistent
memory; exact `ldfld`; and a latched target-null outcome. Its generated-dump proof replays the counted physical body,
correlates exactly one getter operand with one exact owner/field observation, imports that evidence without fabricating
missing cells, executes direct and adjusted getters, and reproduces the canonical prepared-memory result after dump
reopen/rebind. Local headless verification passed locked restore, the zero-warning fifteen-project Release build,
103 unit tests, 67 fast tests, 5 ordinary dump tests, 1 optimized-context test, and the focused 2-test W3 lane with no
skips. [GitHub Actions run
29374585767](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29374585767) passed all four jobs at the
exact implementation commit. [GitHub Actions run
29375584237](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29375584237) then passed all four required
jobs at exact documentation-closure commit `de6cea124`, so W3 is formally closed for its defined milestone-selected
scope. A whole-file-identified disk PE remains only an independent oracle, never dump resolver input.

W4's closed design contract is
`proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`. The completed
`plans/post-w4-path-forward.md` records W5's expression-to-result path and meaningful synthetic selection, and the
completed `plans/post-w5-path-forward.md` records W6's Roslyn migration and fixed-depth member-chain slice. The active
successor is `plans/post-w6-path-forward.md`: acquire one ordinary non-generic static reference field as an explicit
root context, then feed that exact binding into the unchanged W2/W5/W6 pipeline. The complete pinned Roslyn expression
parser remains the sole syntax front end; supported behavior remains a versioned project-owned binder/evaluator
subset.

W6.1–W6.7 are closed at exact locally verified source baseline `440053ad1`. One pinned complete Roslyn parse feeds only
versioned project-owned admitted shapes; counted metadata and physical getter bodies issue detached declared-member
certificates; four/eight-byte reference observations preserve exact/null/partial evidence; intrinsic non-root identity
is separate from alias-path selection; and a complete canonical plan freezes the request, certificate, relative
terminal layout, semantics, evidence, and bounds before any reference or leaf value read. Frozen-plan evaluation now
reuses the W2 value decoders without rebinding declarations or invoking getters, and the append-only schema-v2
consumer replays exact and typed non-exact rows through fresh hidden processes while preserving schema v1. The
schema-v3 usefulness lane now evaluates twenty-four independent incidents across four graph/root shapes, retains zero
representative rows, rejects promotion and enum-order tie selection, and selects one concrete context-acquisition
scenario from a uniquely qualified six-incident/four-shape boundary. The complete local matrix and repository guards
pass with zero skips. The owner explicitly overrode the W6 hosted-only block on 2026-07-16 because the GitHub billing
condition cannot be remediated in this milestone; this is not hosted pass evidence. W6's selected context category
now informs the separately approved W7 static-field-root plan; it is not retroactive W6 implementation evidence.

W7 is planned, not implemented. Its physical truth gate must prove an initialized static field slot and one exact raw
pointer without using a product value read. Exact null and every non-exact outcome remain distinct; no failure falls
back to a strong-handle search, heap-type scan, arbitrary address, or repaired target. W7 adds a separate generated
fixture and sixteen independent full-dump incidents across four unrelated shapes, all through hidden processes. It
does not add expression syntax: Roslyn still parses the complete bounded expression once, and the existing project
binder/evaluator subset still decides what is supported.

The earlier W4 history remains recorded here. W4.1's fixture gate is implemented at
pushed checkpoint `82363585b`, and W4.2's provenance-aware execution kernel is implemented at pushed checkpoint
`e89e43498`. W4.3's dump-free non-exact field continuation is implemented at checkpoint `7479b1ad4`. W4.4a's
body-free direct MethodDef resolution is implemented at pushed checkpoint `2e596c117`, and W4.4b's complete frozen
graph is implemented at pushed checkpoint `742ef2c4f`. W4.5a's exact prepared-graph execution is implemented at pushed
checkpoint `356c07037`, and W4.5b's explained-unknown call/return lineage is implemented at pushed checkpoint
`c72f6ee9e`. W4.6a structural pure-model admission is implemented at pushed checkpoint `77c92789b`, W4.6b modeled-
return lineage at `fd723a912`, W4.6c machine invocation/transfer at `877c9fb55`, and W4.6d compiler/SRM conformance at
`da5346813`. W4.7a standalone target-outcome projection is implemented at `2e70fe76d`, and W4.7b compiler/SRM
replay at `dad6a6dd4`. W4.8's configurable traversal, canonical facade, typed binding, preparation, result, and runner
land through `44b050ec8`. W4.9's ClrMD evidence graph, detached dump binding, and six-row generated-dump reopen/replay
land through `a8b5f32f0`. W4 is closed at exact commit `a819a08fd`; [GitHub Actions run
29463426083](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463426083) passed all four jobs.
Final documentation-closure commit `aaec73c5b` passed the same matrix in [run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230).
W5.1–W5.5b are now implemented through pushed checkpoints `7c3d52572`, `d88b13c2c`, `fc8a43a7a`, `59d9bb590`,
`0f5230e13`, `b788f4f66`, and `90ade6d92`. The product owns canonical expression admission, dump-to-method acquisition, and mode-preserving W2/W4
evaluation. A repository-owned headless consumer runs the nine-row generated facade corpus in fresh processes, and
its usefulness mode joins predeclared question metadata to evaluated machine reports, emits raw counts/composition,
retains unsupported/unavailable rows, and rejects generated-to-representative promotion. Generated validation is 8/9
admitted and 3/9 exact, with 0/4 useful partial-or-unknown and 0/9 decision-changing answers; it contributes zero
representative rows. W5.5b adds 12 independently dumped, predeclared synthetic incidents over request-pipeline and
batch-pipeline root graphs. Its raw counts are 8/12 admitted, 4/12 exact, 2/3 useful partial-or-unknown, and 6/12
decision-changing; four recurring `MemberNavigation` blockers select `AdmitFixedDepthMemberChain` as the next
prototype design slice. The designed corpus is not field evidence: representative/external observations remain 0
questions across 0 incidents and 0 application shapes, and no readiness rate is claimed. W5 is closed for its defined
prototype scope under the owner's explicit 2026-07-16 waiver of the hosted-only closure condition. Exact source
baseline `26ba75fb1` passes the complete local headless matrix. Hosted run
[29512657137](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29512657137) passed documentation and
Build/Fast but never executed either dump job; [run
29513051897](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29513051897) executed no jobs. The
milestone-scoped waiver does not convert either infrastructure rejection into hosted evidence, establish field
readiness, or apply to future milestones.
The first scenario is deliberately branchless: from a generated dump, `DumpProbe.GetMarkerSummary` reads the two
marker fields and calls the direct `CombineMarkers` helper. W2 cannot express that question because its plan selects
only one field and executes no user IL. Exact evidence must yield the exact CoreCLR-agreeing `Int32` result. An admitted
partial or unavailable required marker must yield a typed unknown carrying stable evidence and transformation
provenance, never a concrete default; conflict and invalid evidence retain their distinct failure outcomes. Host
results use `CounterfactualExecution` with explicit policy, assumptions,
models/effects, and applied bounds; they do not claim historical replay. Instruction and preparation-traversal units
are consumed; maximum logical call depth is prepared/enforced and logical/frame high-water marks are reported.
Allocation remains unadmitted and is reported as absent/not applied rather than as a dormant guarantee. The exact,
degraded, differential, budget, and product/dump same/fresh-session replay gates are closed W4 headless evidence.
W4.1 contributes
four fast fixture facts: exact caller/helper IL and metadata closure, CoreCLR agreement, and truthful W3 rejection at
the second field load before the still-unadmitted direct call. W4.2 adds a second meaningful lifted-flat value domain,
policy-enabled explained `Int32` execution, semantic equality independent of explanation, and canonical
content-addressed `InputOrigin`/ordered-`BinaryTransform` lineage capture and fresh-object replay over the shared W3
transport and arithmetic handlers. Exact E2 `ldfld` remains executable; at that checkpoint partial/unavailable field
continuation and `FieldLoadTransform` were still W4.3 work. Its headless evidence remains focused W4.2 53/53,
complete unit 156/156, fast 71/71, ordinary dump 5/5, both documentation guards, zero skips, and a strict
fifteen-project Release build with zero warnings/errors within the milestone test selection for behavioral tests.

W4.3 adds immutable canonical `FieldLoadEvidence` plus `MemoryLoadResult.FromFieldEvidence`, retaining the frozen
ordinary instance `Int32` field, partial/unavailable status, reason, complete source/imported-object identities, and
read geometry/bytes. The shared machine handler continues only under the explicit explained-`Int32` policy and the
optional field-approximation domain capability. Exact remains exact; code-only partial/unavailable and absent
policy/capability block atomically, conflict remains blocked, and invalid or mismatched structured evidence remains
invalid. Successful approximation preserves memory and emits `InstructionExecuted` then `ValuePrecisionLost` at the
same `ldfld`. `ProvenanceConcreteDomain` binds the evidence into canonical `InputOrigin` and `FieldLoadTransform`
nodes with stable same/fresh-domain replay and unchanged W4.2 node identities. This is a dump-free evidence/domain/machine
proof: no ClrMD non-exact import, product facade, or dump-grounded W4 result is claimed.

At checkpoint `7479b1ad4`, the strict fifteen-project Release build passed with zero warnings/errors; focused W4.3
passed 55/55, complete unit 211/211, fast 71/71, ordinary-dump regression 5/5, optimized-dump regression 1/1, and both
Markdown/headless guards passed with zero skips. All test commands were headless and used the milestone test selection.
The historical W4.2 checkpoint remains 3,454 LOC: 3,429 attributable kernel LOC (1,521 production plus 1,908 focused
tests) and 25 LOC segregating an excluded test scope; W4.1–W4.2 had realized 3,932 LOC and projected
18,532–26,132 LOC. W4.3 realizes 3,096 LOC (1,100 production LOC plus 1,996 test LOC), so W4.1–W4.3 realized 7,028 LOC
and projected 19,228–25,728 LOC.

W4.4a freezes a content-equal, body-independent `MethodCallSignatureShape`/`ResolvedMethodCallTarget`: exact non-nil
same-module MethodDef and declaring TypeDef, ordinary managed-IL certification, and complete call-signature facts,
without an RVA, body, local signature, or locals. Structurally valid `MemberRef` and `MethodSpec` operands are
unsupported, and the resolver seam permits W4.6 to choose a future model disposition before any prospective body
read. W4.4b adds explicit legacy versus W4 graph admission modes and a public `MethodGraphPlanner`. Deterministic
first-result caching and root-first call-site discovery retain and charge each direct edge, deduplicate shared method
and field dependencies, reject cycles and descriptor conflicts, and freeze canonical method, field, call-site,
admission, and required-depth facts under fixed 64-method/1,024-unit resource caps. Every failure carries no partial
plan and executes nothing. The exact fixture graph contains two methods, two fields, one call at IL offset 12, logical
depth two, and five traversal units.

W4.4 verification passed locked restore; a strict fifteen-project Release build with zero warnings/errors; planner
35/35; W4 fixture 6/6; complete unit 250/250; fast 73/73; ordinary dump 5/5; optimized dump 1/1; and both guards, with
zero skips. Every behavioral command was headless and used the milestone test selection. W4.4 realizes 3,651 added LOC:
W4.4a contributes 1,043 (665 production plus 378 tests) and W4.4b contributes 2,608 (1,411 production plus 1,197
tests). The post-audit split preserves each delivered package below 3,500 LOC and retains the original combined
W4.4 estimate of 1,700–2,600 as historical calibration. W4.1–W4.4 cumulatively realize 10,679 LOC and produced the
historical 21,179–26,779 projection.

W4.5a binds one frozen graph to an opt-in machine session, creates metadata-derived exact root/callee frames without
re-resolution, retains structural call and return sites, and executes exact `call`/helper-`ret` transfers with one
instruction unit and ordered instruction/frame events per boundary. Its replayable operational envelope enforces and
retains configured/required logical depth plus logical/frame high-water facts while preserving legacy W3 isolation and
persistent memory. At that checkpoint explained unknowns deliberately stopped before a call or interpreted return with
`EXEC_CALL_LINEAGE_UNAVAILABLE`; W4.5b still owned canonical `CallArgumentTransform` and
`InterpretedReturnTransform` lineage.

An independent audit closed every identified W4.5a blocker. Headless validation passed locked restore; the strict
fifteen-project Release solution build and strict Release builds of the unit and integration projects, all with zero
warnings/errors; prepared-graph
tests 25/25; W4 fixture 7/7; complete unit 275/275; fast integration 74/74; ordinary dump 5/5; optimized dump 1/1;
the Markdown guard across 62 files and 41 local destinations; and the headless guard across one workflow. Every test
lane had zero skips and every behavioral command used the milestone test selection. W4.5a realizes 3,334 LOC (1,590
production plus 1,744 tests), bringing W4.1–W4.5a to 14,013 LOC.
The ordinary and optimized dump filters were `Category=Dump&Corpus!=ModeledIncidentContextV1`
and `Category=Dump&Corpus=ModeledIncidentContextV1`, respectively.

W4.5b adds optional `IInterpretedCallLineageDomain<TValue>` capability and append-only lineage node kinds 4 and 5.
Exact values cross calls and returns unchanged. Each explained-unknown argument receives one metadata-indexed
`CallArgumentTransform` over the complete direct-call identity and predecessor; each explained-unknown helper result
receives one `InterpretedReturnTransform` before caller mutation. The complete two-argument vector is preflighted and
interned atomically. Schema v1, node kinds 1–3, and every earlier canonical identity remain frozen. Reachable capture
and fresh-domain replay validate bytes, identities, types, dependencies, call-site identity, and parameter indices.
Missing capability, throwing capability, malformed output, and default/bare/foreign/bottom/wrong-type input or output
retain distinct stable blocked/invalid taxonomy without partial state, memory, budget, event, or lineage mutation.

At pushed checkpoint `c72f6ee9e`, locked restore and the strict fifteen-project Release build passed with zero warnings/errors;
prepared-graph execution passed 40/40; the combined lineage/audit lane 76/76, including 29 frozen legacy identity
cases; compiler lineage 2/2; aggregate W4 integration 9/9; complete unit 297/297; fast integration 76/76; ordinary
dump 5/5; and optimized dump 1/1. All behavioral filters used the milestone test selection, there were zero skips, and an
independent audit found no remaining finding.

W4.6a adds bounded non-generic model identity/version, descriptor, invocation/outcome, and registry contracts. Explicit
`RequirePureModel` selects only exact/no-effect structural descriptors after call resolution/typing and before target
body access; default `Prepare` remains interpret-only. Canonical call disposition, `ModeledLeaves`, lookup,
deduplication, traversal, and depth freeze one body-free opaque leaf. Selection failures expose no partial plan or
fallback; capability identity is excluded from structural equality/hash; legacy interpreted hashes remain frozen.
The compiler graph contains one root, one modeled leaf, two fields, and one edge: five units at depth two. At that
checkpoint machine activation still blocked it as `EXEC_MODEL_EXECUTION_UNAVAILABLE`; W4.6c later replaces that
temporary boundary with the frozen-capability-only execution described below.

At exact pushed checkpoint `77c92789b16d9258c907d5026a36e39f8c957b41`, locked restore; strict Release build
0/0; contract 49/49; model planner 25/25; legacy planner 35/35; SRM 1/1; lineage 2/2; unit 371/371; fast 77/77;
ordinary dump 5/5; optimized dump 1/1; Markdown 62/41; and workflow guard 1 all passed with zero skips. Behavioral
filters used the milestone test selection, and independent audits found no behavioral findings. The deterministic PDB-free
TestTarget PE SHA-256 is `fae40c5805d619845b3d28e6f64e612d1ce520617f6bd369ef8b309609c5a801`.

W4.6b adds optional modeled-return lineage/domain behavior. Exact arguments are embedded in canonical kind-6
`ModeledReturnTransform` nodes; explained arguments first retain their unchanged kind-4 call transforms. The complete
dependency batch is validated and interned atomically, and schema-v1 kinds 1–5 preserve their bytes and identities.
W4.6b realizes 1,003 LOC (481 production plus 522 tests), bringing W4.1–W4.6b to 20,779 LOC.

W4.6c checkpoint `877c9fb55` executes only the capability frozen into the opaque leaf: it performs no resolver,
registry, target-body, descriptor, or selection reread and has no interpretation fallback. Exact and grounded-unknown
returns transfer atomically in the caller, preserve memory, consume one instruction, and emit one instruction event
without a helper frame. A blocked, invalid, malformed, or normalized capability outcome leaves semantic state, memory,
budget, and semantic events unchanged but records one deterministic operational attempt. Logical-depth high water
advances for an entered model boundary, even when it does not transfer; active-frame depth does not. Attempt chronology,
model/call counts, and exact terminal witnesses are invariant-checked. W4.6c realizes 2,734 LOC: 1,425 production
additions plus 1,309 unit-test additions.

W4.6d checkpoint `da5346813` proves compiler/SRM conformance. Model execution agrees with interpreted execution and
CoreCLR for exact values, and with interpreted execution for mixed partial/exact and partial/unavailable values. The
exact run uses six caller instructions, two field loads, logical/frame high water 2/1, one completed attempt, unchanged
memory, no helper body/frame, and no execution-time reread. The mixed case freezes graph SHA-256
`451d0054771d42b541d48459dd038250869a62bf941e60b0bce06a7ee19761ff`; repeated and fresh SRM/domain/machine runs
reproduce the dual-unknown graph SHA-256 `31c45f6902e446d179cc2a5205363e0d5892416ed85c5907135bb79128d6c42f`.
W4.6d realizes 956 integration-test additions. W4.6 closure passed locked restore; strict fifteen-project and
unit/integration Release builds at zero
warnings/errors; focused W4.6c 34/34; focused W4.6d 3/3; aggregate W4 integration 13/13; complete unit 413/413; fast
80/80; ordinary dump 5/5; and optimized dump 1/1, all headlessly with zero skips and
the milestone test selection on behavioral filters.

W4.7a checkpoint `2e70fe76d` adds `Interpreter.Product.DumpDebugging` and projects only a complete sequence of exact
transitions issued by the same `IlMachine`, from one legacy root activation at IL offset zero through the first exact
null-reference target latch. The sequence may include one certified idempotent terminal re-step. Issuer binding is
validated per outcome against exact machine/predecessor references; it does not participate in fragment identity.
Projection rejects incomplete, forged, cross-machine, malformed-latch, non-null, non-legacy, misaccounted, or
event-inconsistent evidence with stable `W4.TargetException.*` codes.

The successful schema-v1 fragment fixes `CounterfactualExecution`/`Completed`/`Complete`/`Exact`/`None`, retains only
the exact exception and structural location, one-method call trace, initial/used/remaining instruction units, complete
events, stable diagnostic, canonical bytes, and lowercase digest, and deliberately exposes no snapshot, root, request,
plan, or traversal identity. The literal standalone fixture freezes SHA-256
`a9b98e46583dcf90ac108571c126d8d86cec0465c595e2689fae767e33ff108e`. W4.7b checkpoint `dad6a6dd4`
reconstructs direct and adjusted compiler-emitted null getters through fresh SRM/module/domain/machine objects and
reproduces identical fragment content, bytes, and hash. Counting/poisonable wrappers prove the optional terminal
re-step consults no resolver, value-domain, or memory capability and that each execution performs exactly one load.

W4.7 closure passed locked restore; the strict sixteen-project Release build over eleven source projects at zero
warnings/errors; complete unit 430/430; Fast 80/80; ordinary dump 5/5; optimized dump 1/1; W4.7a 15/15; W4.7b 2/2;
combined W4.7 17/17; compiler differential class 23/23; Markdown 62 files/41 destinations; and the one-workflow
headless guard. All behavioral lanes were headless, used the milestone test selection, and had zero skips.

W4.5b realizes 2,804 added LOC (766 production plus 2,038 tests). W4.6a/b/c/d realize 2,959/1,003/2,734/956 LOC,
respectively, so combined W4.6 realizes 7,652 LOC and W4.1–W4.6d realize 24,469 LOC. W4.7a/b realize 2,448/353 LOC,
so W4.7 realizes 2,801 LOC and W4.1–W4.7 realize 27,270 LOC. W4.8's six delivery sub-slices realize 11,924 LOC;
W4.9a–c realize 1,389/791/518 LOC, or 2,698 total. Current W4 realization is therefore 41,892 hand-written
implementation LOC excluding documentation and generated lockfile changes. The original 16,860–25,310 baseline and
historical 18,532–26,132, 19,228–25,728, 21,179–26,779, 24,013–29,313, 25,017–29,417, 27,217–32,117,
28,376–32,476, 28,876–33,276, 28,826–33,726, 28,879–33,279, 30,079–33,729, 31,069–34,319, and
31,670–33,970 projections remain calibration facts. W4.9d's local closure passes locked restore, strict Release 0/0,
unit 502/502, Fast 88/88, dumps 6/6 and 1/1, aggregate W4 14/14, and guards 62/44 and 1, with zero skips. Exact pushed
implementation closure passed in run 29463426083 at exact commit
`a819a08fd9ccdf926620c505732475990b242be9`; final documentation closure passed in [run
29463847230](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29463847230) at
`aaec73c5b987089addb539d3628de67bd815bd8f`.

W1 is complete for its revised milestone-selected dump-evidence scope: real reads; typed exact/partial/unavailable/conflict outcomes; honest answer completeness; stable identity/context/provenance; path-accurate bounds; fresh-session canonical replay; repository-wide headless execution; truthful topology; and exact-HEAD hosted CI. [GitHub Actions run 29353198889](https://github.com/VladimirReshetnikov/Interpreter/actions/runs/29353198889) passed all four required jobs at exact closure commit `e2580a8a8`.

Caveat: current evidence covers only the named generated fixtures and explicitly admitted input shapes. Earlier
out-of-scope experiments have been removed, and the workflow now runs every remaining test in
each selected category. No result in W1–W6 establishes behavior for other artifact shapes.
The terminology cleanup renamed a fixture-only environment canary and therefore intentionally re-froze the current
TestTarget PE and its four derived canonical graph identities in the executable tests. Historical hashes remain
evidence for their named commits rather than claims about the current fixture binary.
Immutable upstream snapshots under `lib/` remain verbatim reference material; their vocabulary is not project-authored
scope or a claim of supported behavior.
Required framework namespace and member identifiers also remain literal API names; they do not widen project scope.

The versioned optimized Release modeled-incident report is W1 generated-context evidence and records raw member bytes at 5/5, attributable context at 1/5, and product-query availability at 1/5 while retaining all unavailable cases. It explicitly describes one generated modeled dump, not a representative private-production incident corpus, and therefore establishes no production recoverability rate; representative production measurement is not a W1 gate. Current in-process resource admission still limits dumps to 8 GiB, ClrMD's dump cache to 256 MiB with stack-derived caches disabled, and managed PEs to 512 MiB on the actually opened stream before hashing. Branches, CFG merge/fixpoint analysis, handler-transfer EH, allocation, broad call/model catalogs, generics, virtual stepping, whole-method abstract analysis, async/dynamic lifting, multi-application hosting, and other platform extensions are research backlog until their entry gates pass.

Current, active, and supporting documents use two dimensions: lifecycle (`Draft`, `Current`, `Complete`, `Historical`, `Superseded`) and roadmap relation (`Active`, `Supporting`, `Research`, `Reference`, `Historical`). Historical source/library records may use one combined status when a second axis would add no decision value. A designed or documented capability is not thereby implemented or validated. `Historical` and `Superseded` documents are preserved for rationale and source research; they are not instructions to recreate their proposed topology or backend choice.

## Information architecture

```text
docs/
├── lib/             # Project-specific notes for library snapshots under /lib
├── governance/      # Documentation process, standards, and lifecycle
├── plans/           # Milestones, sequencing, and decision gates
└── proposals/
    ├── architecture/ # Interpreter/core/runtime contracts
    ├── integration/  # External systems and host integration boundaries
    └── product/      # Product-facing experience and requirements
```

## Current documents

| Document | Area | Type | Lifecycle / roadmap | Summary |
|---|---|---|---|---|
| `../DESIGN-ARCHITECTURE-REVIEW.md` | Cross-cutting | Review | Complete · Reference | Repository-wide assessment and prioritized dump-first reset plan. |
| `plans/post-w6-path-forward.md` | Planning | Plan | Current · Active | Planned W7 ordinary static-reference-field root profile, physical truth gate, typed evidence/result matrix, unchanged Roslyn/binder boundary, headless generated conformance, and sixteen-incident/four-shape decision corpus. |
| `plans/post-w5-path-forward.md` | Planning | Plan | Complete · Reference | Closed W6 opt-in bounded member-chain contract, certified terminal data-property dependency, evidence/result matrix, headless synthetic corpus, selected context-acquisition successor, and milestone-scoped hosted disposition. |
| `plans/post-w4-path-forward.md` | Planning | Plan | Complete · Reference | Closed W5 expression-to-result product composition, headless reference consumer, meaningful synthetic usefulness gate, milestone-scoped hosted-gate waiver, and evidence-selected W6 decision. |
| `proposals/product/post-mortem-debugging-feature-proposal.md` | Product | Proposal | Draft · Active | Read-only dump evaluator direction through closed W6, planned W7 static-field-root acquisition, and explicitly gated research phases. |
| `proposals/product/virtual-step-debugging-feature-proposal.md` | Product | Proposal | Draft · Research | Counterfactual virtual-stepping concept; not on the active roadmap. |
| `proposals/product/other-potential-applications.md` | Product | Strategy Note | Draft · Research | Speculative applications and reuse hypotheses; not delivery commitments. |
| `proposals/architecture/architecture-overview-proposal.md` | Architecture | Proposal | Current · Supporting | Top-level component map, runtime boundaries, and canonical data flow. |
| `proposals/architecture/csharp-expression-front-end-contract-proposal.md` | Architecture | Contract | Current · Supporting | Normative pinned Roslyn expression parse, stable invalid/unsupported mapping, versioned W2/W5/W6 tree admission, project-owned projection, compatibility migration, and complex three-bucket conformance contract. |
| `proposals/architecture/restricted-dump-query-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W2 v1 admitted shape, typed root binding, immutable-plan, value-domain, diagnostics, provenance, and all-scenario replay contract. |
| `proposals/architecture/concrete-il-execution-contract-proposal.md` | Architecture | Contract | Current · Active | Normative W3 metadata-derived activation, typed whole-body admission, dump-grounded field import, memory-opcode, exception-boundary, and replay contract. |
| `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md` | Architecture | Contract | Current · Reference | Normative closed W4 branchless `GetMarkerSummary`/`CombineMarkers` method-evaluation contract and executable evidence record. |
| `proposals/architecture/module-architecture-proposal.md` | Architecture | Proposal | Superseded · Reference | Granular responsibility catalog; not the active physical-package plan. |
| `proposals/architecture/minimal-interfaces-proposal.md` | Architecture | Design Sketch | Historical · Reference | Pre-evidence API sketches; current prototype contracts and contract-just-ahead-of-code policy supersede them. |
| `proposals/architecture/il-interpreter-framework-proposal.md` | Architecture | Proposal | Draft · Supporting | Core interpreter architecture and execution model. |
| `proposals/architecture/mvp-abstract-domain-proposal.md` | Architecture | Proposal | Draft · Research | Candidate abstract domains and analysis behavior for a later research phase. |
| `proposals/architecture/technical-stack-proposal.md` | Architecture | Proposal | Draft · Active | Language/runtime/dependency and package layout choices, including the durable sole-Roslyn-parser boundary and planned W7 consumer reuse. |
| `proposals/architecture/call-model-and-effects.md` | Architecture | Proposal | Draft · Supporting | Call classification, fallback behavior, and effect lattice contract for later method evaluation. |
| `proposals/architecture/generic-context-resolver-proposal.md` | Architecture | Proposal | Draft · Research | Generic-context reconstruction strategy for later dump-backed method interpretation. |
| `proposals/architecture/prototype-solution-structure-proposal.md` | Architecture | Current Snapshot | Current · Active | Physical prototype layout and narrow implemented proof boundary. |
| `proposals/architecture/prototype-interface-catalog-proposal.md` | Architecture | Current Snapshot | Current · Active | Narrow contracts exercised through closed W6, explicitly excluding planned-but-absent W7 surfaces; unstable. |
| `proposals/architecture/state-and-domain-model-proposal.md` | Architecture | Proposal | Draft · Supporting | Candidate execution-state, domain, and provenance semantics. |
| `proposals/architecture/testing-strategy-proposal.md` | Architecture | Strategy | Current · Active | Present executable evidence through closed W6 plus planned W7 static-slot/root, parser-regression, and sixteen-incident headless gates. |
| `proposals/architecture/semantic-modeling-proposal.md` | Architecture | Proposal | Draft · Research | BCL/IL semantic lifting and projection concepts for later phases. |
| `proposals/architecture/special-semantics-registry-proposal.md` | Architecture | Proposal | Draft · Research | Candidate unified registry for later modeled semantics. |
| `proposals/architecture/opcode-support-matrix-proposal.md` | Architecture | Proposal | Current · Supporting | Implemented closed W3 E1/E2 encodings and explicit gates for later opcode expansion. |
| `proposals/architecture/perf-and-benchmarks-proposal.md` | Architecture | Proposal | Draft · Research | Aspirational performance model and benchmark gates. |
| `proposals/architecture/virtual-step-debugging-implementation-proposal.md` | Architecture | Proposal | Draft · Research | Candidate stepping control plane, stop reasons, model frames, and undo. |
| `proposals/architecture/debug-map-design-proposal.md` | Architecture | Proposal | Draft · Research | Candidate source/IL mapping strategy for virtual stepping. |
| `proposals/architecture/virtual-tasks-proposal.md` | Architecture | Proposal | Draft · Research | Async/await virtualization research. |
| `proposals/architecture/dynamic-calls-proposal.md` | Architecture | Proposal | Draft · Research | Dynamic call-site lifting research. |
| `proposals/integration/clrmd-integration-proposal.md` | Integration | Proposal | Draft · Active | ClrMD + PE/PDB integration architecture and binding-layer model. |
| `proposals/integration/clrmd-apis-usage-tutorial-proposal.md` | Integration | Tutorial Proposal | Draft · Reference | Scenario-first tutorial mapping ClrMD APIs to our runtime snapshot and IL acquisition workflows. |
| `proposals/integration/pe-pdb-reader-integration-proposal.md` | Integration | Proposal | Draft · Active | Artifact-resolution design for PE/PDB loading and identity validation. |
| `proposals/integration-test-plan.md` | Integration | Verification Plan | Current · Active | Fast semantic/differential and real dump-memory evidence through closed W6, plus planned W7 static-root verification. |
| `plans/future-work-planning.md` | Planning | Plan | Current · Supporting | Detailed evidence-led W0–W4 delivery record and research entry gates. |
| `plans/requirements-traceability-map.md` | Planning | Matrix | Current · Active | Requirements mapped separately to design, implementation, and validation evidence. |
| `lib/README.md` | Library Notes | Index | Current · Reference | Navigation and policy for active decisions versus preserved source research. |
| `lib/clrmd/usage-notes.md` | Library Notes | Note | Historical · Reference | Preserved ClrMD source research; active behavior is established by adapter code and executable fixtures. |
| `lib/clrmd/intro-tutorial.md` | Library Notes | Tutorial | Historical · Reference | Contributor introduction to ClrMD dump/runtime/heap/stack inspection workflows. |
| `lib/clrmd/source-scan.md` | Library Notes | Source Scan | Historical · Research | Source-driven ClrMD lifecycle, cache, stack, and heap adapter observations. |
| `lib/asmresolver/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved AsmResolver candidate research; SRM/PEReader is the active backend. |
| `lib/asmresolver/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for the AsmResolver object model; not an implementation roadmap. |
| `lib/asmresolver/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved AsmResolver reader-policy, CIL, and PDB ingestion findings. |
| `lib/dnlib/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved dnlib fallback/compatibility research. |
| `lib/dnlib/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for dnlib metadata, method-body, and symbol APIs. |
| `lib/dnlib/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved dnlib module, method-body, and Portable PDB findings. |
| `lib/cecil/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved Mono.Cecil reader-policy and adapter research. |
| `lib/cecil/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for Mono.Cecil policy, method bodies, symbols, and resolution. |
| `lib/cecil/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved Mono.Cecil load-mode, symbol-provider, and resolver findings. |
| `lib/roslyn/usage-notes.md` | Library Notes | Note | Historical · Research | Preserved Roslyn front-end and conservative-inference research for W2. |
| `lib/roslyn/intro-tutorial.md` | Library Notes | Tutorial | Historical · Research | Contributor reference for Roslyn parse/bind workflows. |
| `lib/roslyn/source-scan.md` | Library Notes | Source Scan | Historical · Research | Preserved Roslyn parse/compilation/semantic-boundary findings. |
| `lib/backend-capability-matrix.md` | Library Notes | Matrix | Historical · Research | Pre-decision comparison retained alongside the active SRM decision. |
| `lib/adapter-conformance-checklist.md` | Library Notes | Checklist | Draft · Reference | Backend-neutral adapter and miss-reason guidance; apply only when an active fixture needs it. |
| `lib/backend-evidence-log.md` | Library Notes | Evidence Log | Current · Reference | Current executable SRM and ClrMD/raw-memory evidence followed by the preserved 2026-02 source-review record. |
| `lib/mvp-backend-decision-record.md` | Library Notes | Decision Record | Current · Active | SRM/PEReader decision for active prototype slices, with evidence-based revisit triggers. |
| `lib/pe-pdb-reader-api-comparison.md` | Library Notes | Comparison Note | Historical · Research | Preserved pre-decision API comparison; not the active backend recommendation. |
| `lib/source-review-deep-dive.md` | Library Notes | Source Review | Historical · Research | Preserved cross-library source review. |
| `lib/source-tour-workbook.md` | Library Notes | Workbook | Historical · Research | Optional source-tour exercises for a future evidence-triggered backend evaluation. |
| `governance/documentation-organization-proposal.md` | Governance | Policy | Current · Supporting | Lightweight information architecture, status vocabulary, and evidence-language rules. |
| `governance/project-faq.md` | Governance | Guide | Draft · Supporting | FAQ for contributors and stakeholders about scope, expectations, and direction in the concept phase. |
| `governance/terminology-glossary.md` | Governance | Glossary | Draft · Supporting | Canonical terminology definitions and cross-document language alignment guidance. |

## Suggested reading paths

### Active delivery path

1. `../DESIGN-ARCHITECTURE-REVIEW.md`
2. `plans/post-w6-path-forward.md`
3. `plans/post-w5-path-forward.md`
4. `proposals/architecture/csharp-expression-front-end-contract-proposal.md`
5. `plans/post-w4-path-forward.md`
6. `proposals/product/post-mortem-debugging-feature-proposal.md`
7. `proposals/architecture/restricted-dump-query-contract-proposal.md`
8. `proposals/architecture/concrete-il-execution-contract-proposal.md`
9. `proposals/architecture/counterfactual-method-evaluation-contract-proposal.md`
10. `proposals/architecture/architecture-overview-proposal.md`
11. `proposals/architecture/prototype-solution-structure-proposal.md`
12. `lib/mvp-backend-decision-record.md`
13. `plans/future-work-planning.md`
14. `proposals/architecture/testing-strategy-proposal.md`
15. `proposals/integration-test-plan.md`
16. `proposals/integration/clrmd-integration-proposal.md`
17. `proposals/integration/pe-pdb-reader-integration-proposal.md`

The paths below explore broader design possibilities. They are research/reference paths, not delivery sequences; inclusion does not imply roadmap commitment or implementation evidence.

### Product research path

1. `proposals/product/post-mortem-debugging-feature-proposal.md`
2. `proposals/product/virtual-step-debugging-feature-proposal.md`
3. `proposals/product/other-potential-applications.md`
4. `proposals/architecture/il-interpreter-framework-proposal.md`
5. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
6. `proposals/architecture/debug-map-design-proposal.md`

### Platform strategy research path

1. `proposals/architecture/architecture-overview-proposal.md`
2. `proposals/product/other-potential-applications.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`
5. `proposals/architecture/semantic-modeling-proposal.md`
6. `proposals/architecture/mvp-abstract-domain-proposal.md`

### Architecture research path

1. `proposals/architecture/architecture-overview-proposal.md`
2. `proposals/architecture/il-interpreter-framework-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
5. `proposals/architecture/mvp-abstract-domain-proposal.md`
6. `proposals/architecture/call-model-and-effects.md`
7. `proposals/architecture/debug-map-design-proposal.md`
8. `proposals/architecture/dynamic-calls-proposal.md`
9. `proposals/architecture/virtual-tasks-proposal.md`
10. `proposals/architecture/semantic-modeling-proposal.md`
11. `proposals/architecture/special-semantics-registry-proposal.md`
12. `proposals/architecture/opcode-support-matrix-proposal.md`
13. `proposals/architecture/generic-context-resolver-proposal.md`

### Historical architecture sketches

1. `proposals/architecture/minimal-interfaces-proposal.md`
2. `proposals/architecture/module-architecture-proposal.md`

These sketches explain earlier reasoning, but neither their API shapes nor their project-per-seam package map is normative. Start from the current prototype source and `proposals/architecture/prototype-solution-structure-proposal.md` instead.

### Semantic-modeling research path

1. `proposals/architecture/call-model-and-effects.md`
2. `proposals/architecture/state-and-domain-model-proposal.md`
3. `proposals/architecture/semantic-modeling-proposal.md`
4. `proposals/architecture/special-semantics-registry-proposal.md`
5. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`

### Virtual-debugger research path

1. `proposals/product/virtual-step-debugging-feature-proposal.md`
2. `proposals/architecture/virtual-step-debugging-implementation-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`
4. `proposals/architecture/call-model-and-effects.md`
5. `proposals/architecture/debug-map-design-proposal.md`
6. `proposals/architecture/virtual-tasks-proposal.md`

### Dynamic-dispatch research path

1. `proposals/architecture/call-model-and-effects.md`
2. `proposals/architecture/dynamic-calls-proposal.md`
3. `proposals/architecture/state-and-domain-model-proposal.md`

### Runtime integration path

1. `lib/mvp-backend-decision-record.md`
2. `proposals/integration/clrmd-integration-proposal.md`
3. `proposals/integration/clrmd-apis-usage-tutorial-proposal.md`
4. `proposals/integration/pe-pdb-reader-integration-proposal.md`
5. `lib/backend-evidence-log.md`

Generic-context reconstruction and broad call/effect modeling remain later research topics. The closed W4 contract
admits only its named direct `GetMarkerSummary` to `CombineMarkers` call shape; it does not promote those broader
research designs.

### Process and governance path

1. `governance/project-faq.md`
2. `governance/documentation-organization-proposal.md`
3. `governance/terminology-glossary.md`
4. `plans/post-w6-path-forward.md`
5. `plans/post-w5-path-forward.md`
6. `plans/post-w4-path-forward.md`
7. `plans/future-work-planning.md`
8. `plans/requirements-traceability-map.md`

## Repository license

This project is distributed under the **MIT-0 (MIT No Attribution)** license. See the repository-level [`LICENSE`](../LICENSE) file for full terms.

## Naming and placement conventions

- Place new design docs under `docs/proposals/<area>/` with `*-proposal.md` suffix.
- Place sequencing/roadmap artifacts under `docs/plans/`.
- Place process and standards docs under `docs/governance/`.
- Prefer stable filenames; if renaming, update links in both root `README.md` and this index.

## Documentation WIP policy

For current-state questions, prefer evidence in this order: executable tests and their recorded results, prototype source/solution, active decision records and plans, then supporting proposals. Historical/research documents explain possibilities and prior reasoning; they do not override those sources.

Do not add a new subsystem proposal merely because an existing plan mentions one. Add or deepen design only when the active vertical slice needs the contract, and pair the change with an executable validation step whenever practical. When evidence changes, update the active snapshot/traceability entry and mark superseded guidance explicitly instead of silently leaving two “current” answers.
