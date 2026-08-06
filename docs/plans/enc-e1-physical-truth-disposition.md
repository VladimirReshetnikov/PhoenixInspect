# Edit-and-Continue E1 Physical-Truth Disposition

> **Lifecycle:** Frozen · **Governs:** the E1 truth gate of the
> [Edit-and-Continue Support Plan](edit-and-continue-support-plan.md)
>
> This document freezes what the E1 probes measured over real artifacts — the payload files the pinned compiler
> wrote and full dumps of genuinely edited processes — together with the two typed evidence gaps E1 leaves open.
> Every finding below is asserted by a probe in `EncPhysicalTruthTests` or `EncFixturePrerequisiteTests` and
> replays over fresh dumps; none is restated runtime documentation. Later strides cite this document, not
> hypotheses.

## 1. The fixture and its verification discipline

The hidden `PhoenixInspect.EncTestTarget` loads a Debug-configuration payload baseline under the
modifiable-assemblies gate, applies one or two compiler-emitted generations through the runtime's own
`MetadataUpdater.ApplyUpdate` path, and declares readiness only after invoking the edited surface and observing
the post-edit value — a changed body's new sentinel, an added member's stored value, or the stacked
generation-two sentinel — with a distinct typed exit for every verification failure. Payloads are pure pinned
compiler output: deterministic Debug baselines with portable PDBs, and `EmitDifference` delta triples chained
through each generation's own emit baseline, with EnC local-slot and lambda maps and local signatures read from
the baseline's own artifacts. Every payload also carries an edit-enabled comparator assembly that the fixture
loads and invokes but never edits, so measurements can separate enablement, use, and applied edits. Transient
payload arrays are applied in their own frame and collected before the pause.

## 2. Measured probe answers

1. **Base-image invariance — holds.** The edited process's mapped base metadata is byte-identical to the
   unedited on-disk baseline.
2. **Delta reachability — answered, negative for durability.** Mapping every byte-identical delta copy through
   the dump's own memory-range directory: the metadata and Portable-PDB delta blobs exist only as dead
   managed-heap residue of the process's own payload arrays; no runtime-retained copy of either blob exists
   anywhere in the captured address space. The runtime integrates the metadata delta into its own structures.
   Delta acquisition from a dump therefore cannot locate original blobs; it must read runtime structures.
3. **Applied-state detectability — answered.** The base image is provably silent: after a verified executed
   edit, its Module row still names generation zero with empty edit identifiers and zero `EncLog`/`EncMap`
   rows. The contract-descriptor vocabulary is provably silent: no edit-module class or applied-changes count is
   named anywhere in the captured runtime bytes. The declared Module flags word marks enablement only —
   byte-identical between the edited module and the used-but-unedited comparator. The detector is an undeclared
   Module counter one pointer past the declared dynamic-metadata field: it reads one plus the
   applied-generation count, measured across zero, one, and two generations, and survives the enablement and
   use controls. As a non-contract offset it is a pinned-runtime fact that E2 must validate per descriptor
   rather than assume.
4. **Delta table shape — frozen, with a corrected finding.** A changed-body generation logs three
   default-operation rows, not one: the compiler's generation carries its own new `AssemblyRef` and `TypeRef`
   rows with RIDs extending past the baseline's table ends alongside the single updated `MethodDef` row, and
   the edit map assigns exactly those rows. Generation-aware composition must model reference-table extension
   even for body-only edits. The generation chain's lineage joins on the predecessor's edit identifier with an
   empty-identifier boundary at the chain root, and each generation shares the baseline Mvid.
5. **Added-member census — answered, negative for the host surface.** After a pure-Insert generation whose
   added members provably executed, the host runtime surface reports the pre-edit census: the added static
   field and accessors are absent from the type's surface while the baseline sentinel stays enumerable under
   its baseline token. Census must come from the generation's own delta tables.
6. **Added-static storage — typed evidence gap.** With the added field invisible to the host surface, no field
   object exists to supply the added slot's address, and the slot is not identifiable from any surface measured
   by E1. Its location must be derived from the runtime's edit structures once their layout is characterized;
   until then the storage answer for edit-added statics is a recorded gap, not a claim.
7. **Effective-body location — answered, negative for the host surface.** The runtime surface resolves the
   edited method's IL from the mapped base image — the address lies inside the module extent and the bytes are
   the generation-zero body — so the effective body is unreachable through it. The eleven-byte IL delta's
   native-memory occurrence is recorded but too short for uniqueness claims.
8. **Portable-PDB delta retention — answered, negative.** No runtime-retained copy of the PDB delta exists in
   the captured address space. Scope evidence for edited methods can only come from outside the dump through
   the host-supplied artifact seam, which is E6's evidence-gated arm; accordingly the extended-scope fixture
   profile is deferred to E6 and recorded as the second E1 gap rather than measured against structures that
   provably do not retain it.
9. **Host runtime surface — measured throughout.** Census, IL, and token behavior over edited modules all show
   the pre-edit world; token-anchored joins keep resolving while carrying no mark of the edit.

## 3. Controls

- **Filtered capture:** the same paused edited process captured fully and with the filtered normal type proves
  the delta copies locatable in the full capture are entirely absent from the filtered one. Delta evidence over
  a filtered dump is absent, not degraded; the typed unavailable disposition is the only honest answer.
- **Enablement and use:** the three-way module comparison — edited, enabled-and-used-but-unedited, and plain —
  is the discrimination every detector claim above rests on.

## 4. Consequences the next strides inherit

- Every surface a dump reader touches today shows the pre-edit world, so E2's typed refusal is mandatory before
  any admission, and the enablement flags ground a sound conservative refusal while the applied-state counter
  supplies best-effort precision.
- E3's acquisition design must target runtime edit structures, not blob location; the counter is its anchor
  evidence that structures exist and track generations.
- E6 proceeds only through the artifact seam, or freezes its non-admission.
- The two recorded gaps — added-static storage location and the extended-scope profile — travel with probe 6
  and stride E6 respectively and do not block the E1 exit gate, which requires a retained observation or a
  typed evidence gap per question.
