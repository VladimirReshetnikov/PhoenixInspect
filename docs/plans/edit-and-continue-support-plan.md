# Edit-and-Continue and Hot Reload Support Plan

> **Lifecycle:** Proposed · **Roadmap:** Future (research-gated)
>
> **Decision:** treat runtime-applied metadata edits as first-class physical evidence in four ordered strides —
> measure the edited-process artifact surface, detect edits and refuse stale answers with typed dispositions,
> compose delta metadata into generation-aware authority, and only then answer exactly over edited modules. No stage
> may guess: an answer over an edited module is either generation-aware or typed non-exact, never silently stale.
>
> **Scale:** The inclusive umbrella is `~100K LOC`. Individual checkpoints are generally `~10K LOC`, with the
> detection stride at `~1K–10K LOC` and closure at `~1K LOC`. These are logarithmic orders of magnitude and may be
> revised when implementation exposes the actual volume.
>
> **Evidence boundary:** Section 2 records what the current code measurably does; everything else in this document
> is design intent. No stride after E1 may begin before E1's physical-truth disposition is frozen, because the
> runtime's delta retention, dump memory coverage, and Portable-PDB delta reachability are hypotheses until probed.

## 1) Purpose

A debugger expression evaluator reads dumps of processes it did not control. Some of those processes were edited
while running: Visual Studio Edit-and-Continue and .NET Hot Reload both apply metadata, IL, and Portable-PDB deltas
to loaded modules through the runtime's single edit application path, and a dump captured afterward holds a module
whose mapped image no longer tells the whole truth. Today the evaluator composes its entire metadata authority from
that mapped base image. For an edited module this produces the worst class of answer this project recognizes: a
*silently* stale one, presented with the same exact dispositions as a truthful answer.

This plan makes edits part of the evidence model. Its order is the project's standard order: physical truth first,
typed refusal second, admitted capability last. The refusal stride is independently shippable and removes the
silent-staleness hazard even if delta composition never lands.

## 2) Measured current state

These facts were established by direct code reading at commit `0eee5378b` and are the baseline this plan corrects:

1. Every metadata authority catalog is produced from the module's base metadata image as mapped in the dump
   (`ClrmdDumpSession`/`StaticFieldV2RuntimeAcquisitionSession.ReadModuleMetadata` feeding
   `MetadataAuthorityProducer`). No other metadata source exists.
2. The producer faithfully reads and retains the Module row's `Generation`, `GenerationId`, and `BaseGenerationId`
   (`AcquireManifestModuleIdentity`), and `StaticFieldModuleDefinitionIdentity` encodes them canonically — but no
   consumer branches on them. In a base image these values are zero and empty even after the runtime has applied
   deltas, so the retained fields cannot detect that edits happened.
3. Method bodies and IL are read from the base image by the W2–W4 execution and counterfactual paths. An edited
   method's updated body lives in runtime-owned memory the readers never consult, so those paths would execute the
   pre-edit body without any signal.
4. Static field values are read from runtime memory at pause time and are therefore current even after edits; it is
   the metadata describing them that is stale.
5. Portable-PDB identity validation compares CodeView identity against the base PDB, which still matches after
   edits. Lexical envelopes and import scopes therefore describe pre-edit source, and a scope-completeness
   certificate can call a scope complete that an edit has extended — an assumption standing in for a missing
   artifact.
6. Token-anchored joins give one accidental safety: a runtime type or member added by an edit carries a RID beyond
   the base table's row count, so it fails chain and catalog lookups and degrades to typed `Absent` or
   unprojectable stops. That is a byproduct of the join discipline, not a disposition; nothing tells the caller the
   module was edited.
7. No edited-process fixture, probe, corpus incident, or documented disposition exists anywhere in the repository.

## 3) Non-negotiable invariants

1. Detection precedes admission. Until generation-aware composition lands for a given consumer, that consumer must
   not answer exactly over a module with applied edits; it produces a typed non-exact disposition naming the edit
   state.
2. Edit evidence is physical. The applied-generation state comes from the runtime's own structures and delta
   allocations in the dump, never from display text, absence heuristics, or an assumption that a module is unedited
   because nothing says otherwise where nothing could say otherwise.
3. A missing or unreachable delta artifact is never silently replaced by the base row. It yields `Partial` or
   `Unavailable` with a named diagnostic code.
4. Zero-generation behavior is frozen. For modules with no applied edits, every existing canonical byte sequence,
   digest, golden, and default route remains byte-identical; every new contract input is absence-preserving
   additive encoding.
5. Superseded rows are retained, never erased. A generation-aware catalog exposes the effective row and keeps the
   physical history as replay identity, matching the complete-table discipline.
6. No fabricated deltas. If the dump's memory coverage excludes delta allocations (filtered minidumps), acquisition
   stops typed `Unavailable`; the runner never synthesizes delta bytes.
7. Per-generation lineage is proved. Each generation's delta carries its own content identity and source ends, and
   every cross-generation join proves it belongs to the same module lineage chain
   (`BaseGenerationId`/`GenerationId` pairing) before composition.
8. Portable-PDB deltas are separately evidence-gated. Their retention in process memory is not assumed; if E1 finds
   them unreachable, the lexical stride freezes a typed non-admission instead.
9. Every new operation is bounded with cap-plus-one accounting: applied-generation count, delta blob bytes, EncLog
   and EncMap rows, and runtime token-census width all carry named caps frozen after E1 records real cardinalities.
10. Typed non-admissions create no placeholder APIs, mirroring the W8.1 discipline.

## 4) Background the truth gate must confirm or refute

Both edit producers — debugger Edit-and-Continue and `MetadataUpdater.ApplyUpdate` Hot Reload — funnel through one
runtime application path taking three blobs per generation: a metadata delta (whose `EncLog`/`EncMap` tables spell
the logical row edits), an IL delta, and an optional Portable-PDB delta. The runtime retains applied metadata and IL
deltas in edit-manager-owned allocations for the module's lifetime; the mapped base image is not rewritten. Delta
Portable-PDB retention is weaker and may not survive into process memory at all. Added static fields receive storage
through the edit machinery rather than the ordinary static blocks.

Every sentence in this section is a hypothesis with good provenance and zero project-local proof. E1 exists to turn
each one into a retained observation or a corrected finding; no later stride may cite this section as evidence.

## 5) Stride sequence

### E1 — edited-process physical truth gate

**Scale:** `~10K LOC` fixture, probes, and tests.

Add a dedicated hidden fixture target that applies real compiled deltas via `MetadataUpdater.ApplyUpdate`, with the
deltas produced by the pinned Roslyn compiler's `EmitDifference` so the emitted artifact is compiler truth rather
than hand-authored bytes. Truth-gate profiles cover, each behind its own pause: a changed method body, an added
static field on an existing type, an added type with a static field, an added method, two-plus stacked generations
over one module, and an edit that extends a method's local scopes. Capture one full dump per profile plus one
deliberately filtered dump whose memory coverage drops the delta allocations.

Independent SRM, ClrMD, and counted raw-memory probes then decide, with retained evidence per question:

1. base-image invariance — the mapped image and its Module row are byte-identical before and after edits;
2. delta reachability — where each generation's metadata and IL delta bytes live, whether the dump contains them,
   and what runtime structure (contract descriptor or otherwise) anchors their addresses and sizes;
3. applied-state detectability — which physical runtime facts prove the applied-generation count for a module, and
   that they read zero for unedited modules;
4. `EncLog`/`EncMap` shape — that each delta's tables decode with SRM and describe exactly the fixture's edits;
5. added-member visibility — the runtime token census for added types, methods, and fields against base row counts;
6. added-static storage — the exact storage location and read geometry for an edit-added static field;
7. updated-body location — the exact address and header shape of an edited method's effective IL;
8. Portable-PDB delta retention — whether delta PDB bytes are reachable in the dump, partially reachable, or
   absent; and
9. ClrMD behavior — what `ClrType`, field, and method surfaces report over edited modules, so later strides know
   which host observations remain trustworthy.

**Exit gate**

- Every question above has a retained observation or a typed evidence gap in a frozen disposition document; none is
  answered from section 4.
- The filtered-dump profile proves the intended `Unavailable` evidence shape.
- No product contract changes in this stride.

**Status:** E1 is active; the owner opened the entry gate. Its first slice is landed and measured over the
changed-body profile in `EncPhysicalTruthTests`: probe 1 holds — the edited process's mapped base metadata is
byte-identical to the unedited on-disk baseline; the base-image half of probe 3 holds — the mapped Module row still
names generation zero with empty edit identifiers and zero `EncLog`/`EncMap` rows after a verified, executed edit,
so nothing produced from the base image alone can reveal the edit; probe 4 produced a corrected finding — the
changed-body delta logs *three* default-operation rows, not one, because the compiler's generation carries its own
new `AssemblyRef` and `TypeRef` rows whose RIDs extend past the baseline's table ends alongside the single updated
`MethodDef` row, and the edit map assigns exactly those three rows, so generation-aware composition must model
reference-table extension even for a body-only edit; and the host-surface half of probe 9 holds — the edited method
remains enumerable under its baseline token. The second slice landed the added-static profile — the target inserts
one static field and two accessors through a pure-Insert generation, stores and reads the value through the added
members in-process, and only then declares readiness — and measured probes 5 and 6 over its real dump: the host
runtime surface reports the pre-edit census even though the process provably executed the added members, with the
added field absent from the type's static fields, the added accessors absent from its methods, and the baseline
sentinel still enumerable under its baseline token; added-member census must therefore come from the generation's
own delta tables, and with no field object to ask, the added slot's storage location is a typed evidence gap for
this surface, to be answered from the runtime's edit structures. Two compiler facts were also corrected by
measurement: a pure Insert generation reports no updated methods, and the added-symbol predicate feeds
`EmitDifference` from the Insert edits themselves. Probes 2, 7, and 8, the runtime-structure half of probe 3, the
storage half of probe 6, the stacked-generation and extended-scope profiles, and the filtered-dump control remain
open for the following slices. The third slice landed the stacked-generation payload — two chained body-edit
generations emitted against each predecessor's own baseline — and measured the lineage chain from the delta module
rows alone: each generation shares the baseline Mvid and carries its own distinct nonempty edit identifier,
generation one's base identifier is empty because generation zero has no edit identifier at all, and generation
two's base identifier equals generation one's edit identifier exactly. Invariant 7's lineage join is therefore
pairing on the predecessor's edit identifier with an empty-identifier boundary condition at the chain root, now a
measured fact rather than a hypothesis. The fourth slice answered probe 2 with a design-changing finding: a
minimal reader over the dump's own memory-range directory located every byte-identical copy of the applied delta
blobs, and with the applying frame dead and two forced collections before the pause, the metadata and Portable-PDB
deltas exist only as managed-heap residue of the process's own payload arrays — no native runtime-retained copy of
either blob exists anywhere in the captured address space. The runtime integrates the metadata delta into its own
structures rather than retaining the blob, so E3 cannot acquire deltas by locating the original bytes in a dump; it
must read the runtime's edit structures, which makes the runtime-structure half of probe 3 the load-bearing open
question. The Portable-PDB delta's absence from any runtime-owned memory likewise points probe 8 toward the E6
non-admission arm unless the host-supplied artifact seam carries the delta from outside the dump. The fifth slice
measured probe 7's host-surface half and probe 3's descriptor direction: the runtime surface resolves the edited
method's IL from the mapped base image — the address sits inside the module extent and the bytes are the
generation-zero body — so the effective edited body is unreachable through that surface, which now makes every
surface measured so far (base image, Portable-PDB identity, member census, IL info) show the pre-edit world; and
the pinned runtime's captured bytes name no edit structure in the contract-descriptor vocabulary — neither the
edit module class nor an applied-changes count appears anywhere in the dump, while the descriptor's
dynamic-metadata field name does — so applied-state detection cannot come from the descriptor's declared
vocabulary, leaving non-contract runtime structures and the dynamic-metadata field's behavior over an edited
module as the open candidates.

### E2 — edit detection and typed non-admission

**Scale:** `~1K–10K LOC`.

Turn the E1 detection facts into one exact per-module edit-state identity — applied-generation count plus, where
reachable, each generation's delta content identity — acquired physically at session/world composition and joined
into the module lineage. Every consumer of that module's authority then refuses staleness with a typed disposition:
metadata-derived stages produce non-exact outcomes with a named code such as
`W8_MODULE_EDITED_GENERATIONS_NOT_COMPOSED`; IL-consuming paths refuse the base body the same way; lexical
envelopes and scope-completeness certificates for methods in an edited module refuse `Complete`. The existing
outcome axes carry these dispositions; no new axis is added.

All additions are absence-preserving: a zero-generation module composes byte-identically to today, which the frozen
W1–W8 goldens prove.

**Exit gate**

- Every E1 fixture profile that previously produced an exact-but-stale or silently pre-edit answer now produces its
  typed stop, asserted over real dumps.
- The zero-generation world keeps every canonical digest byte-identically.
- The silent-staleness hazard of section 2 is closed even if no later stride ever lands.

### E3 — delta metadata acquisition

**Scale:** `~10K LOC`.

Acquire each generation's metadata delta as counted raw reads with its own content identity and source ends, and
issue complete physical `EncLog` and `EncMap` table catalogs under the same complete-table discipline as every W8.2
catalog: full RID coverage, source correlation, retained raw rows, and prefix-free typed stops for missing,
truncated, foreign, or out-of-order deltas. The module's lineage chain — base, then each generation joined by its
`GenerationId`/`BaseGenerationId` pair — becomes an exact identity of its own.

**Exit gate**

- Every fixture generation composes to exact physical delta catalogs whose rows describe exactly the compiled
  edits.
- Poisoned inputs (truncated blob, foreign-module delta, gap in the generation chain) produce their typed stops.
- The filtered dump stops `Unavailable` before any catalog is issued.

### E4 — generation-aware authority composition

**Scale:** `~10K–100K LOC`, expected to split into per-table-family checkpoints.

Project the effective logical tables by applying `EncLog` operations across the generation chain onto the landed
complete-table model: updated rows supersede while the physical history is retained, added rows extend the row
domain past the base end, and every token join resolves against the effective state. Definition authority, chains,
classification, ancestry, constraints, member catalogs, and signature token resolution re-issue as
generation-aware identities carrying an explicit effective-generation fact. The E2 refusal for a consumer is lifted
in the same checkpoint that lands its generation-aware composition, never earlier.

**Exit gate**

- The added type, added method, and added static field bind exactly over their fixture dumps, with provenance
  naming the generation that introduced each.
- Superseded rows remain retrievable as history and never win a join.
- The zero-generation world keeps every canonical digest byte-identically.

### E5 — effective IL and edit-added storage

**Scale:** `~10K LOC`.

Route the execution and counterfactual paths to the effective method body proved by E1, with provenance naming the
body's generation, and admit edit-added static storage through the exact geometry the truth gate froze — or retain
a typed non-admission if E1 could not prove that geometry, with the probe evidence as the recorded rationale.

**Exit gate**

- A counterfactual run of the edited method matches its post-edit behavior over the fixture dump.
- The edit-added static reaches its exact value, or its typed non-admission stands with retained evidence.

### E6 — Portable-PDB delta composition (evidence-gated)

**Scale:** `~1K–10K LOC` if admitted.

Only if E1 proves delta PDBs reachable — in dump memory or through the existing caller-supplied artifact resolver
seam — compose per-generation documents, scopes, and import facts so lexical envelopes over edited methods can
again reach `Complete`. Otherwise the E2 refusal is the frozen disposition and this stride records why.

**Exit gate**

- Either edited-method lexical envelopes reach `Complete` from composed generations, or the typed non-admission is
  frozen with the E1 evidence attached. No middle state ships.

### E7 — corpus, conformance, and closure

**Scale:** `~10K LOC` plus `~1K LOC` documentation.

Predeclare meaningful-synthetic incidents over edited targets before implementation reaches them — an edited-body
value read, an added-field bind, an added-type spelling, a stacked-generation module, and the filtered-dump
poison — with the standard twelve predeclared axes, counterfactuals (`withhold-delta-blobs`,
`evaluate-before-edit-control`), and runner-execution ledger. Extend the generated conformance cross-product with
per-generation poisons, re-freeze report goldens, and close traceability, navigation, and status documents at the
checkpoint that changes their truth.

**Exit gate**

- Every predeclared incident executes or carries a measured finding under the corpus discipline.
- All W1–W8 compatibility lanes stay golden; repository guards pass; the closure commit is pushed.

## 6) Bounds

| Operation | Bound direction |
|---|---|
| Applied generations per module | Named cap with cap-plus-one accounting; concrete value frozen in E2 from E1 cardinalities |
| Delta blob bytes per generation | Named cap; a crossing is `Partial`, never a truncated read presented as complete |
| `EncLog`/`EncMap` rows | Complete-table validation with counted row ends per generation |
| Runtime token census | Bounded sweep with cap-plus-one, reusing the W8 candidate-accounting pattern |
| Lineage chain walk | Bounded by the generation cap; a cycle or pair mismatch is `Invalid` |
| Memory reads | Existing counted raw-read bounds; delta reads add explicit per-blob widths |

## 7) Verification matrix

| Lane | Proof |
|---|---|
| Unit | Edit-state identity, lineage chain, delta catalog invariants, canonical bytes, cap boundaries |
| Fast | SRM differentials over `EmitDifference` outputs; detection and refusal seams over synthetic edit states |
| Ordinary dump | E1 truth-gate probes; per-stride behavior over the edited-fixture dumps; filtered-dump poisons |
| Meaningful synthetic | The E7 predeclared incidents with counterfactuals and frozen reports |
| Compatibility | Every W1–W8 profile, canonical byte sequence, digest, and default route remains golden; zero-generation worlds byte-identical |
| Repository | Markdown, headless workflow, vocabulary, XML docs, strict build, clean tree |

## 8) Entry gate and completion definition

This plan is research-gated future work, entered through the
[Future Work Planning](future-work-planning.md) backlog: it becomes delivery work only when the W8 sequence closes
or the owner explicitly prioritizes it, and E1 must be its first scheduled checkpoint in either case. E2 is the
minimum shippable increment and is worth landing even alone.

The before-entry-gate prerequisites are landed and proven: the hidden `PhoenixInspect.EncTestTarget` fixture loads
a payload baseline, applies a generation through the runtime's own `MetadataUpdater.ApplyUpdate` path, and prints
readiness only after observing the edited body execute; the test-infrastructure delta compiler produces the
baseline and its delta triple with the pinned compiler's `EmitDifference`, reading the EnC local-slot and lambda
maps and local signatures from the baseline's own portable PDB and method bodies; the dump-target runner gained an
additive caller-declared environment seam for the `DOTNET_MODIFIABLE_ASSEMBLIES` gate; and the `EncFixtureV1` smoke
lane proves the edited process pauses verified and a full dump of it captures and reopens. E1 therefore starts at
its probes, not at its harness.

The capability is complete when every consumer either answers generation-aware over edited modules or carries a
frozen typed non-admission backed by E1 evidence; when zero-generation behavior has remained byte-frozen through
every stride; when the edited-process corpus incidents replay; and when the closure commit satisfies repository and
hosted governance. Completing detection without composition is an honest intermediate state; completing composition
without detection is not a state this plan permits.

## 9) Delivery discipline

- Commit and push every completed checkpoint before beginning the next, with detailed multi-line messages recording
  decisions, evidence, tests, compatibility, and remaining bounds.
- No duration estimates anywhere; revise logarithmic LOC bands when implementation changes the apparent magnitude.
- Run every managed command and every fixture process headlessly through the existing wrapper.
- Never rewrite closed milestone evidence; document dispositions at the checkpoint that changes their truth.
