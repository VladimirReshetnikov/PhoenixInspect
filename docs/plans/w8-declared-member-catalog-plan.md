# W8 declaration-side table catalogs: Constant and Property

**Status:** design selected and preconditions verified; steps 1 through 5 of section 8 landed. This document is the
working plan for the remaining steps, not a closed milestone record. A first pass through step 6 settled its product
side and measured its test migration; see the [step 6 preparation notes](w8-declared-member-catalog-step6-notes.md),
which also recommend one sequencing change to shorten the window in which the tree does not build.

This plan was selected from three independent proposals written under different lenses, each refuted by three
adversarial reviewers under the contract-discipline, physical-correctness, and frozen-artifact lenses. All three
proposals were refuted; the design below is the synthesis that answers the surviving objections.

## Preconditions verified against the shipped reader and the real compiled targets

These were checked directly rather than assumed, and two of them overturned premises every proposal shared.

- `System.Reflection.Metadata.MetadataReader` exposes **no row enumeration** for Constant (0x0B), PropertyMap (0x15),
  PropertyPtr (0x16), or MethodSemantics (0x18) — only the writer side exists for the latter two. The producer reads
  exclusively through that reader and the repository enables no unsafe blocks, so any completeness proof shaped as a
  walk of those tables is not implementable here.
- `MetadataReader.GetTableRowCount` **does** accept all four, so an independently observed exact end exists for every
  table in this slice even where the rows do not.
- Measured on the real `W8TestTarget`, `W8CoordinatorShapeTarget`, and `W8WorkflowShapeTarget` images: constants
  reached from the three parent tables sum to exactly the Constant row count (39=39, 3=3, 7=7) with zero
  parent-token mismatches; Property RIDs are contiguous; each declaring type owns exactly one contiguous Property
  block; `PropertyPtr` is empty; and accessors projected per property equal the MethodSemantics row count exactly.
- Every property on those targets carries attribute bits `0x0000`, so the admitted-attribute check must be a mask
  test ("no bits outside the admitted set") rather than a membership test, or it would reject ordinary metadata.

---

# 1. SELECTED DESIGN

**Selected: Proposal 3's consumer-backwards lens, corrected by Proposal 2's physical-fidelity discipline and Proposal 1's tagged-append encoding — plus one scope cut none of the three made: the Property blocker lands *without* MethodSemantics, by refusing to derive accessor accessibility and declaring that refusal as a boundary.**

### Why the three alternatives died, and how the survivor answers each

**P1 (smallest-frozen-set) died on physical acquisition.** Verified: `System.Reflection.Metadata` has no `MethodSemantics` row API — the only hits in `Microsoft.NETCore.App.Ref/10.0.5/ref/net10.0/System.Reflection.Metadata.xml` are `F:...TableIndex.MethodSemantics` (:3577) and the *writer* `M:...MetadataBuilder.AddMethodSemantics` (:2540). Same for PropertyMap: only `M:...MetadataBuilder.AddPropertyMap` (:2587). P1's "read them through the raw table via MetadataReader's table-row APIs" names nothing. **Survivor's answer: it models neither table.** It reads only what SRM exposes verbatim — `MetadataTokens.ConstantHandle(Int32)` (:2911), `Constant.{TypeCode,Value,Parent}` (:1105-1111), `MetadataTokens.PropertyDefinitionHandle(Int32)` (:3087), `PropertyDefinition.{Attributes,Name,Signature}` (:6379-6381), `PropertyDefinition.GetDeclaringType` (:6375). No fabricated `ReservedPadding`, no re-encoded coded index.

**P1 also died on its fact extension.** Verified fatal: `StaticFieldModuleSearchFact.Exact` at `StaticFieldSymbolContracts.cs:6039-6060` declares every table count as non-nullable `int` defaulting to `0` (`propertyDefinitionRowCount = 0`, `parameterPointerRowCount = 0`, …); only `typeDefinitionRowCount`/`fieldDefinitionRowCount` are `int?` and they default to the *examined* count. So `int? x = null` contradicts the factory's shape, and `int x = 0` makes a `is not null` trailer predicate unconditionally true — re-freezing every source-end digest. **Survivor's answer: an all-or-nothing tagged bundle** (§3), which is neither.

**P2 (maximum physical fidelity) died on its headline claim and on two frozen slots.** Its "a reconstruction cannot honestly claim a physical RID sequence" is disproved by landed code: `MetadataAuthorityProducer.ReadNestedClassRows` at `MetadataAuthorityProducer.cs:1328-1352` synthesizes NestedClass RIDs from `GetDeclaringType()`, and the producer remark at `:614-621` licenses it. It also never addressed `WriteOptionalDigest(writer, literalConstant?.Sha256)` at `StaticFieldV2ExpressionPipeline.cs:1415` or the ledger's third fixed `Int32` at `:132`. **Survivor's answer: it does not reconstruct PropertyMap at all** (owner comes per-row from `GetDeclaringType()`), and it **deliberately bumps four schema versions** (§6) so no positional slot is ever silently repurposed.

**P3 (consumer-backwards) died on over-reach in the Property half.** Its `PropertyMapOrderInvalid` (Parent nondecreasing → Invalid) and `PropertyMapRowCountConflict` (distinct-owner count *==* PropertyMapRowCount → Invalid) reject ECMA-legal images: PropertyMap is not in the II.24.2.6 sorted-table set, and a PropertyMap row with an empty run is not forbidden. `DuplicatePropertyName` per (owner, name) rejects overloaded indexers (`this[int]`/`this[string]` are two Property rows named `Item`), and no fixture under `tests/` declares an indexer so the oracle would ship green over it. **Survivor's answer: the only ownership invariants are the two that are actually derivable** — each distinct owner occupies exactly one contiguous block of Property RIDs, and `blockCount <= PropertyMapRowCount` — and `DuplicatePropertyName` is scoped to `(owner, name, signature)`, not `(owner, name)`.

**The cut none of them made.** P1/P2/P3 all argued MethodSemantics is mandatory because a Property row has no access mask. That is true, and their conclusion — derive an accessibility value — is what forced three unbuildable contracts. The correct move under this project's own rule ("an unprovable answer is a typed stop, never an inference") is the opposite: **do not derive it.** A same-name property blocks unconditionally, and the residual is a declared boundary. This is byte-for-byte the shape of the landed `AccessibilityBypassApplied = 4` (`StaticFieldV2MemberLookup.cs:186-187`), which already names "we admitted a declaration without proving accessibility." The only behavioural cost is over-refusal on an *inaccessible* same-name property — a conservative typed stop, never a wrong value.

### Objections against the survivor, answered
- *"Over-refusal regresses a correct answer."* Today's Exact-field answer in that case is accidentally correct (the property is invisible to the model), not proven. §5 declares the boundary, §7 tests it.
- *"Retiring `PropertyAndEventTablesNotModeled` while MethodSemantics is unmodeled is an over-claim."* The value is **deleted, not narrowed in place**; it is replaced by `EventTablesNotModeled` plus a new `PropertyAccessorSemanticsNotModeled`. Nothing survives that is false.
- *"A and B are entangled through the Constant Parent range check."* Verified false: `HasConstant` needs only `PropertyRowCount`, which `StaticFieldModuleSearchFact.PropertyDefinitionRowCount` already carries (`StaticFieldSymbolContracts.cs:5934`). The Constant catalog takes **no** Property-catalog prerequisite (§3), so a Property-side refusal can never delete a literal answer.

---

# 2. SCOPE HONESTY

**The Property blocker does *not* require MethodSemantics or a PropertyMap catalog — provided it refuses to decide accessibility.** Concretely:

| Table | Landed? | Why |
|---|---|---|
| Constant 0x0B | **Yes — full table catalog** | RID-enumerable; every column verbatim from SRM. |
| Property 0x17 | **Yes — full table catalog** | RID-enumerable; owner per-row from `GetDeclaringType()` (NestedClass precedent, `MetadataAuthorityProducer.cs:1328-1352`). |
| PropertyMap 0x15 | **No catalog. Row count only, as an inequality cross-check.** | No read API. Its only load-bearing use — ownership — is available per-row without it. |
| PropertyPtr 0x16 | **No catalog. Row count only, to *refuse* indirection.** | Non-zero count ⇒ typed `NonExact/PropertyPointerIndirectionNotModeled`. Same posture as the landed `MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default)` at `MetadataAuthorityProducer.cs:971`. |
| MethodSemantics 0x18 | **Deferred.** | No read API at all. Accessor grouping would rest on SRM's Association resolution; the only available proof (a global count identity) is permutation-blind. Deferred behind a declared boundary. |
| Event 0x14 / EventMap 0x12 | **Deferred.** | Out of scope; the narrowed boundary names them. |

**This slice lands TWO catalogs** (Constant, Property) **plus one source-end extension and one row-count bundle.** Both halves land together — not because they are coupled (they are not), but because both boundary retirements move the same member-lookup/provenance digest chain, so splitting them would re-freeze the same goldens twice.

**Deferred deliberately, with its follow-up named:** accessor-semantics modelling. When MethodSemantics evidence lands (as a count-identity association catalog over `PropertyDefinition.GetAccessors()` / `EventDefinition.GetAccessors()` — both verified present at `:6373` and `:3643`, with `MetadataTokens.EventDefinitionHandle(Int32)` at `:2942`), `PropertyAccessorSemanticsNotModeled` is retired and the property pass gains a most-permissive-accessor join. **Do not attempt it in this slice.**

---

# 3. NEW TYPES

### 3.1 `src/PhoenixInspect.Product.DumpQuery/StaticFieldSymbolContracts.cs` (append)

**`StaticFieldModuleDeclaredMemberRowCounts`** — sealed, `IEquatable<>`, domain `"static-field-module-declared-member-row-counts"`, **schema 1**.
Public `static Create(int constantRowCount, int propertyMapRowCount, int propertyPointerRowCount)`; each rejected when negative or above `0x00FF_FFFF`. Three non-nullable `int` getters. Writer: three `WriteInt32` in that order.
*XML intent:* "Freezes the three physical declaration-side table row counts observed alongside one exact module search. The bundle is all-or-nothing: its presence asserts that every count in it was independently observed, and its absence asserts that none was."

### 3.2 New file `src/PhoenixInspect.Product.DumpQuery/MetadataDeclaredMemberSourceEndContracts.cs`

**`MetadataDeclaredMemberSourceEndIdentity`** — sealed, `IEquatable<>`, domain `"metadata-v2-declared-member-source-end"`, **schema 1**. Modelled line-for-line on `MetadataReferenceSourceEndIdentity` (`MetadataReferenceSourceAndPhysicalContracts.cs:11-101`).
Writer: `WriteSha256(definitionSourceEnds.Sha256)`, then `WriteInt32` of `FieldDefinitionRowCount`, `ParameterDefinitionRowCount`, `PropertyRowCount`, `ConstantRowCount`, `PropertyMapRowCount`, `PropertyPointerRowCount`.
`public static Create(MetadataSourceEndIdentity definitionSourceEnds)` projects from `definitionSourceEnds.SourceModuleFact` and throws `ArgumentException` unless `Status == Exact`, `PropertyDefinitionRowCount is not null`, and `DeclaredMemberRowCounts is not null` — exactly the precedent's guard at `MetadataReferenceSourceAndPhysicalContracts.cs:86-97`.
Getters: `DefinitionSourceEnds`, `SourceModule`, `FieldDefinitionRowCount`, `ParameterDefinitionRowCount`, `PropertyRowCount`, `ConstantRowCount`, `PropertyMapRowCount`, `PropertyPointerRowCount`, `CanonicalBytes`, `Sha256`.
Internal helpers: `ContainsHasConstantParentToken(int token, out MetadataConstantParentKind kind)`, `ContainsPropertyToken(int)`.
*XML intent:* "Extends one exact metadata source end with the declaration-side tables. The existing source-end digest remains authoritative for FieldDef and Param; Property, Constant, PropertyMap, and PropertyPtr counts are projected from the same retained exhaustive module-search fact."

### 3.3 New file `src/PhoenixInspect.Product.DumpQuery/MetadataConstantTableContracts.cs`

**`MetadataConstantTableResultKind`** — `Exact = 1, NonExact = 2, Invalid = 3`. Remark: every non-exact or invalid result exposes no row prefix.

**`MetadataConstantParentKind`** — `FieldDefinition = 1, ParameterDefinition = 2, PropertyDefinition = 3`. Remark: the member is the decoded HasConstant target table; the numbering is contract-local, the table kind is physical.

**`MetadataConstantParentOrderProfile`** — `Unavailable = 0, EcmaParentSorted = 1, Unsorted = 2`. Follows `MetadataGenericParameterPhysicalOrderProfile`. Remark: ECMA-335 II.24.2.6 requires Constant to be sorted by Parent, so `Unsorted` records a spec-violating image without rejecting it; no proof here depends on the order.

**`MetadataConstantDisposition`** — `Present = 1, AbsentByDeclaredAttributes = 2, OwnerNotIssuedByThisCatalog = 3, CatalogNonExact = 4`. Remark: after an exact catalog, `AbsentByDeclaredAttributes` is a proven negative, not a missing lookup.

**`MetadataConstantTableIssue`** — `None = 0`; `FieldDefinitionCatalogNonExact = 1`; `FieldDefinitionCatalogInvalid = 2`; `DeclaredMemberSourceEndMismatch = 3`; `TableRowBoundReached = 4`; `TableIncomplete = 5`; `TableRowCountConflict = 6`; `PhysicalOrderInvalid = 7`; `SourceModuleMismatch = 8`; `ParentTokenKindInvalid = 9`; `ParentTokenOutOfRange = 10`; `DuplicateParentConstant = 11`; `ConstantTypeCodeNotAdmitted = 12`; `ConstantValueBlobUninitialized = 13`; `ConstantValueBlobBoundReached = 14`; `ConstantValueWidthInvalid = 15`; `NullReferenceValueNonZero = 16`; `FieldParentWithoutDefaultFlag = 17`; `FieldDefaultFlagWithoutConstantRow = 18`; `FieldLiteralWithoutDefaultFlag = 19`.

**`MetadataConstantRowObservationIdentity`** — sealed, domain `"metadata-v2-constant-row-observation"`, **schema 1**, **public** `Create`.
Columns: `MetadataModule`, `ConstantToken` (validated `CanonicalReplayEncoding.ValidateMetadataToken(token, 0x0B, …)`), `ConstantTypeCode` (raw int), `ParentMetadataToken` (**retained raw** — the foreign column is validated only by the catalog, per `MetadataGenericParameterConstraintTableContracts.cs:126-152`), `ConstantValueBlob`.
Public const pair: `MaximumConstantValueByteCount = 65_536`, `MaximumConstantValueByteCountBoundName = "metadata-v2.constant-value.bytes"`.
*Required remark (explicit non-claim):* "The one reserved padding byte at ECMA-335 II.22.9 offset 1 is not exposed by `MetadataReader.GetConstant` and is therefore neither observed nor asserted. This observation carries no caller-authored parent."

**`MetadataConstantTableRowIdentity`** — sealed, domain `"metadata-v2-constant-table-row"`, **schema 1**, **no public factory**; `internal static Create(object mintCapability, …)` throwing `ArgumentException` on capability mismatch.
Adds `ParentKind`, `ParentMetadataToken`, and for a Field parent the joined `MetadataFieldDefinitionTableRowIdentity`. Writer uses fixed-size references (`MetadataGenericParameterConstraintTableContracts.cs:308-316`): `WriteSha256(observation.Sha256)`, `WriteInt32((int)parentKind)`, `WriteInt32(parentMetadataToken)`, `WriteOptionalDigest(declaringFieldRow?.Sha256)`. Also exposes `ConstantTypeCode`, `ConstantValueBlob`, `ConstantValueByteCount`.

**`MetadataConstantTableCatalogIdentity`** — sealed, domain `"metadata-v2-constant-table-catalog"`, **schema 1**. Sole issuer:
```
public static MetadataConstantTableCatalogIdentity Create(
    MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
    MetadataFieldDefinitionTableCatalogIdentity fieldDefinitions,
    ImmutableArray<MetadataConstantRowObservationIdentity> observations)
```
Guarded issuance: `private static readonly object RowMintCapability = new();` + `internal static bool OwnsRowMintCapability(object?)`.
Validation order (mirrors `MetadataFieldDefinitionTableContracts.cs:462-594`):
1. `fieldDefinitions` NonExact→NonExact/1 propagating its bound; Invalid→Invalid/2.
2. `!declaredMemberSourceEnds.DefinitionSourceEnds.Equals(fieldDefinitions.SourceEnds)` → Invalid/3.
3. `ConstantRowCount > 65_536` → NonExact/4 with `new EvaluationDeterministicBound("expression-v2.metadata.constant-rows", 65_536)`, observed `= 65_537`.
4. default array + zero end → `Empty`; default/short → NonExact/5 with supplied length; longer → Invalid/6.
5. defensive re-copy; `token != (0x0B00_0000 | checked(index + 1))` → Invalid/7; module inequality → Invalid/8.
6. Parent: table kind ∉ {0x04, 0x08, 0x17} → Invalid/9; rowId 0 or above the matching end (`FieldDefinitionRowCount` / `ParameterDefinitionRowCount` / `PropertyRowCount`) → Invalid/10; `HashSet<int>` on the parent token → Invalid/11.
7. Value: type code ∉ {0x02…0x0E} ∪ {0x12} → Invalid/12 (matches `IsAdmittedConstantTypeCode`, `StaticFieldV2StorageStrategyBinder.cs:1787-1788`); `IsDefault` → Invalid/13; length > 65 536 → NonExact/14 with the contract-local bound; width ≠ `ConstantWidth(typeCode)` (1/2/4/8, STRING = any even count) → Invalid/15; CLASS with any non-zero byte → Invalid/16.
8. Bidirectional Field pairing over the exhaustive FieldDef catalog — **this is the absence proof**: a Field-parented row whose FieldDef lacks `HasDefault` → Invalid/17; a FieldDef row with `HasDefault` and no Field-parented row → Invalid/18; a FieldDef row with `IsLiteral` and not `HasDefault` (ECMA II.22.15) → Invalid/19. `HasDefault`/`IsLiteral` are already decoded at `MetadataFieldDefinitionTableContracts.cs:293-299` — **no edit to that row's writer**, so golden `acbf1a2e…` is untouched.
9. `ParentOrderProfile` recorded, never gated.
10. Rows minted only after all of the above.

Accessors: `FindRow(int constantToken)`; `MetadataConstantTableRowIdentity? FindRowForFieldDefinition(MetadataFieldDefinitionTableRowIdentity)` and `MetadataConstantDisposition DispositionForField(MetadataFieldDefinitionTableRowIdentity, out MetadataConstantTableRowIdentity?)` — both doubly guarded on `ResultKind == Exact` **and** `FieldDefinitions.FindRow(row.FieldDefinitionToken)?.Equals(row) == true` (identity equality, per `MetadataFieldDefinitionTableContracts.cs:626-651`). This closes the "the fact may not belong to this row" hole the seam cannot close.

*Required remark (stated non-claim):* "Param- and Property-parented rows are validated physically — kind, source range, parent uniqueness, type code, and value width — but are not paired against `ParamAttributes.HasDefault` or `PropertyAttributes.HasDefault`, because neither the Param table nor property default values are consumed here."

### 3.4 New file `src/PhoenixInspect.Product.DumpQuery/MetadataPropertyTableContracts.cs`

**`MetadataPropertyTableResultKind`** — `Exact = 1, NonExact = 2, Invalid = 3`.

**`MetadataPropertyTableIssue`** — `None = 0`; `DefinitionAuthorityNonExact = 1`; `DefinitionAuthorityInvalid = 2`; `DeclaredMemberSourceEndMismatch = 3`; `TableRowBoundReached = 4`; `TableIncomplete = 5`; `TableRowCountConflict = 6`; `PhysicalOrderInvalid = 7`; `SourceModuleMismatch = 8`; `PropertyPointerIndirectionNotModeled = 9`; `NameEmpty = 10`; `NameBoundReached = 11`; `SignatureUninitialized = 12`; `SignatureBoundReached = 13`; `PropertyAttributesNotAdmitted = 14`; `DeclaringTypeDefinitionOutOfRange = 15`; `DeclaringTypeDefinitionNotIssued = 16`; `OwnershipBlockNotContiguous = 17`; `OwnershipBlockCountConflict = 18`; `DuplicatePropertySignature = 19`.

**`MetadataPropertyRowObservationIdentity`** — sealed, domain `"metadata-v2-property-row-observation"`, **schema 1**, public `Create`. Columns: `MetadataModule`, `PropertyToken` (validated 0x17), `Attributes`, `Name`, `SignatureBytes`, `DeclaringTypeDefinitionToken` (**retained raw**).
Public const pairs: `MaximumNameCharacterCount = 1_024` / `"metadata-v2.property-name.characters"`; `MaximumSignatureByteCount = 2_048` / `"metadata-v2.property-signature.bytes"` (mirrors `MetadataMethodDefinitionTableContracts.cs:94-98`).
*Required remarks:* (a) the owner is the PropertyMap Parent recovered through the reader's association, validated only by the catalog; (b) the PropertySig blob is retained **undecoded** — `BoundedEcmaSignatureForm` has no Property member, so the property's type is not decoded and nothing here asserts it; (c) the row carries **no accessibility**, because the physical table has none.

**`MetadataPropertyTableRowIdentity`** — sealed, domain `"metadata-v2-property-table-row"`, **schema 1**, internal `Create(object mintCapability, …)`. Adds the authority-issued `DeclaringTypeDefinition`; exposes `Name`, `Attributes`, `IsSpecialName`, `IsRuntimeSpecialName`, `HasDefault` as pure decodings. Writer: `WriteSha256(observation.Sha256)`, `WriteSha256(declaringType.Sha256)`, `WriteInt32(declaringTypeToken)`.

**`MetadataPropertyTableCatalogIdentity`** — sealed, domain `"metadata-v2-property-table-catalog"`, **schema 1**.
```
public static MetadataPropertyTableCatalogIdentity Create(
    MetadataDeclaredMemberSourceEndIdentity declaredMemberSourceEnds,
    MetadataDefinitionAuthorityCatalogIdentity definitionAuthority,
    ImmutableArray<MetadataPropertyRowObservationIdentity> observations)
```
Same guarded-issuance token. Validation: prerequisite propagation (1/2); source-end identity equality against `definitionAuthority.SourceEnds` (3); bound (4, `"expression-v2.metadata.property-rows"`, 65 536); incomplete/conflict (5/6); contiguity `0x1700_0000 | index+1` (7); module (8); **`PropertyPointerRowCount != 0` → NonExact/9 with a null bound** (an unmodeled image shape is an unavailable acquisition, not a contradiction); name empty (10) / over cap (11, NonExact); signature default (12) / over cap (13, NonExact); attribute bits outside `SpecialName 0x0200 | RTSpecialName 0x0400 | HasDefault 0x1000` (14, ECMA II.23.1.14); owner not a 0x02 token in `TypeDefinitionRowCount` (15) or not issued by `definitionAuthority.ExactTypeDefinitionOrDefault` (16).

**Ownership proofs — exactly the two that are derivable:**
- (17) `OwnershipBlockNotContiguous`: walking Property RIDs ascending, each distinct owner must occupy exactly **one** contiguous block; re-encountering an owner after leaving its block is the contradiction. *Deliberately not asserted: that owner RIDs are monotonic.* PropertyMap is **not** in the ECMA-335 II.24.2.6 sorted-table set, so a legal image may order its rows by `PropertyList` alone.
- (18) `OwnershipBlockCountConflict`: `blockCount > declaredMemberSourceEnds.PropertyMapRowCount`, **or** `PropertyMapRowCount == 0 && PropertyRowCount > 0`. *Deliberately an inequality, not equality*: a PropertyMap row owning a zero-length run is legal and invisible.
- (19) `DuplicatePropertySignature`: duplicate `(owner, name, signature-bytes)`. **Not** `(owner, name)` — overloaded indexers legally share the name `Item`.

Accessor: `public ImmutableArray<MetadataPropertyTableRowIdentity> RowsForDeclaringTypeOrEmpty(MetadataTypeDefinitionAuthorityIdentity)`, doubly guarded exactly like `MetadataFieldDefinitionTableContracts.cs:626-651`, returning rows in ascending Property RID order.

*Required remark (stated non-claim):* "This catalog proves the complete Property table and its complete, exactly-once ownership by authority-issued TypeDefs. It asserts no PropertyMap RID sequence, decodes no PropertySig, and derives no accessibility — MethodSemantics (0x18) is not modeled by this composition."

---

# 4. INTEGRATION EDITS

### 4.1 `StaticFieldSymbolContracts.cs`
- `:5789` (private ctor param list) and `Create`: append trailing `StaticFieldModuleDeclaredMemberRowCounts? declaredMemberRowCounts = null`.
- `:6039` `Exact(...)`: append the same trailing optional parameter, after `parameterPointerRowCount`, and thread it through the `Create` call.
- **`:5873`** — immediately after `WriteOptionalInt32(writer, parameterPointerRowCount)` and before `canonicalBytes = writer.ToImmutableArray()` (`:5874`), insert the tagged trailer:
```csharp
private const int DeclaredMemberRowCountsFieldTag = 1;
// The declaration-side row counts are appended only when the caller independently observed all three, so every
// fact created without them keeps its exact version-4 byte content and its frozen digest unchanged.
if (declaredMemberRowCounts is not null)
{
    writer.WriteInt32(DeclaredMemberRowCountsFieldTag);
    writer.WriteLengthPrefixedBytes(declaredMemberRowCounts.CanonicalBytes.AsSpan());
}
```
**No schema bump.** Verified byte-preserving: the writer's last statement before `ToImmutableArray()` is the `parameterPointerRowCount` write (`:5873-5874`).
- Add `public StaticFieldModuleDeclaredMemberRowCounts? DeclaredMemberRowCounts { get; }`.
- `:9319-9341` table-to-count switch: add `0x0B`, `0x15`, `0x16` cases projecting from the bundle.

### 4.2 `ExpressionV2ContractPrimitives.cs`
- `:84-108` block: `MaximumConstantRowCount = 65_536`, `MaximumPropertyRowCount = 65_536`.
- `:215-224` block: `ConstantRowCountBoundName = "expression-v2.metadata.constant-rows"`, `PropertyRowCountBoundName = "expression-v2.metadata.property-rows"`.
- `:252-313` `DeclaredBounds`: append both entries. Do **not** reuse `DeclaredPropertyCountBoundName` (`:234`) — it is the V1 declared-member profile.
- The contract-local caps (`metadata-v2.constant-value.bytes`, `metadata-v2.property-name.characters`, `metadata-v2.property-signature.bytes`) stay as public const pairs on their observations, following `MetadataMethodDefinitionTableContracts.cs:94-98`.

### 4.3 `MetadataAuthorityProducer.cs`
1. **`:99`** stage enum — append, never renumber (the int reaches canonical bytes at `:281-283`): `DeclaredMemberSourceEnds = 23`, `PropertyTable = 24`, `ConstantTable = 25`.
2. **`:923-937`** — add three reads: `reader.GetTableRowCount(TableIndex.Constant)`, `…PropertyMap`, `…PropertyPtr`.
3. **`:939-967`** — pass `declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(constantRowCount, propertyMapRowCount, propertyPointerRowCount)` to `StaticFieldModuleSearchFact.Exact`. (`propertyDefinitionRowCount` is already passed at `:952`.)
4. **`:426-454`** `ModuleDraft` — three nullable slots; **`:310-383`** three public nullable properties; **`:305`** three `WriteOptionalDigest` calls appended at the **end** of the writer's run.
5. Two new readers modelled on `ReadFieldDefinitionRows` (`:1430-1449`), placed near it:
   - `ReadConstantRows`: `MetadataTokens.ConstantHandle(rowId)` → `reader.GetConstant(handle)` → `(int)c.TypeCode`, `MetadataTokens.GetToken(c.Parent)`, `ReadBlob(reader, c.Value)` (`:1580-1581`).
   - `ReadPropertyRows`: `MetadataTokens.PropertyDefinitionHandle(rowId)` → `reader.GetPropertyDefinition(handle)` → `(int)p.Attributes`, `reader.GetString(p.Name)`, `ReadBlob(reader, p.Signature)`, `MetadataTokens.GetToken(p.GetDeclaringType())`.
6. **After `:1227`** (`ModuleReferenceTableSet`), append three stage blocks in dependency order, each copying the `FieldDefinitionTable` template at `:1085-1097`: `DeclaredMemberSourceEnds` → `PropertyTable(declaredEnds, definitionAuthority, ReadPropertyRows(...))` → `ConstantTable(declaredEnds, fieldDefinitions, ReadConstantRows(...))`.
7. **`:614-621`** remark block — extend the "three physical columns are reconstructed" sentence to name the Property owner alongside the NestedClass rows, with the same justification.
8. `AcquirePortfolio` (`:698-857`) and `MetadataAuthorityPortfolioStage` (`:141-163`) — **untouched**; neither catalog is multi-module.

### 4.4 `StaticFieldV2ExpressionPipeline.cs` — retire the seam by deleting it
- **`:39-41`** delete `StaticFieldV2PipelineEvidenceKind.MetadataConstantRow = 3` (numeric hole at 3); `:200-210` `CallCount` drops the case.
- **`:107`** `StaticFieldV2PipelineEvidenceLedger` **CanonicalSchemaVersion 2 → 3**; `:113`/`:132` delete the `metadataConstantRow` parameter and its `WriteInt32`; `:122` delete `MetadataConstantRowCallCount`; `:180` drop the term from `TotalCallCount`.
- **`:545-612`** delete the public type `StaticFieldV2LiteralConstantFact` entirely.
- **`:1032`** `StaticFieldV2ExpressionRequest` **CanonicalSchemaVersion 2 → 3**; delete the field at `:1052`, the parameter at `:1165-1168`/`:1244`/`:1296`, and the positional `writer.WriteBoolean(literalConstantSource is not null)` at **`:1100`**. Add `ImmutableArray<MetadataConstantTableCatalogIdentity> constantCatalogs` written **positionally** in its place (it replaces a deleted positional field in the same bumped schema).
- **`:1340-1356`** delete the `StaticFieldV2LiteralConstantFact? literalConstant` parameter; `:1490` delete the `LiteralConstant` property; **`:1415`** delete the `WriteOptionalDigest(writer, literalConstant?.Sha256)`; add `MetadataConstantTableRowIdentity? literalConstantRow` written at the same position; **bump provenance CanonicalSchemaVersion 2 → 3**.
- **`:2329-2338`** rewrite `AcquireConstantRow` to a catalog projection:
```csharp
private MetadataConstantTableRowIdentity? AcquireConstantRow(
    MetadataFieldDefinitionTableRowIdentity fieldRow, out DumpExpressionValueOutcome? stop)
```
resolving the module's catalog from `request.ConstantCatalogs`, calling `DispositionForField`, and mapping: `Present` → row, `stop = null`; `AbsentByDeclaredAttributes` → `stop = Invalid` (a contradiction, since `:1814` already required `IsLiteral && HasDefault`); `OwnerNotIssuedByThisCatalog` or no catalog for the module → `stop = Unavailable`; `CatalogNonExact` → `Unavailable` for NonExact, `Invalid` for Invalid. **No boundary is added.**
- **`:2340-2353`** `ProjectLiteral`: the silent `return DumpExpressionValueOutcome.Unavailable` is replaced by the typed stop above; `StaticFieldV2LiteralProjectionRequest.Create(fieldRow, constantRow, FieldCatalogFor(fieldRow), request.CapabilityProbes)`.

### 4.5 `StaticFieldV2StorageStrategyBinder.cs`
- **`:104-105`** delete `StaticFieldV2StorageCoverageBoundary.ConstantTableSuppliedByCaller = 4` (hole at 4; values 1,2,3,5 unchanged — `W8V2ContractFoundationTests.cs:1397-1425` forbids aliasing, not holes). It never appears in a `ClassifyStrategy` outcome (`:1754-1772` adds only 1/2/3), so no strategy digest moves.
- **`:857-858`** `StaticFieldV2LiteralProjectionRequest` **CanonicalSchemaVersion 1 → 2**; `:875-880` replace `WriteInt32(constantTypeCode)` + `WriteLengthPrefixedBytes(constantValueBlob)` with `writer.WriteSha256(constantRow.Sha256, nameof(constantRow))`.
- **`:929-941`** `Create(fieldRow, MetadataConstantTableRowIdentity constantRow, namedLiteralTypeCatalog, capabilityProbes)`. Both throw paths are deleted; `ConstantTypeCode` and `ConstantValueBlob` become projections of the row. Add a **typed** parent check (not a throw) consumed by `ProjectLiteral`.
- **`:1284-1288`** the seed becomes `ImmutableArray.Create(CustomAttributeTableNotModeled)`.
- New issue values on `StaticFieldV2LiteralValueIssue` (appended): `ConstantRowParentMismatch` (Invalid — the row's parent is not this FieldDef token; the wrong-field hole the seam cannot catch) and `ConstantValueBoundReached` (NonExact — `constantRow.ConstantValueByteCount > MaximumConstantValueByteCount` (2050, `:856`), carrying `"expression-v2.static.string-characters"`). **This is deliberately a per-field stop, not a catalog stop**, so one over-long `const string` cannot make every other literal in the module unanswerable.
- **`:1310-1376`** signature-agreement and width-exact decoding **unchanged**. `:1720-1725` requirement vector **unchanged**.

### 4.6 `StaticFieldV2MemberLookup.cs`
- **`:558`** request **CanonicalSchemaVersion 2 → 3**; add `ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs` written **positionally** immediately after the field-catalog block (`:600-604`). The vector is **required**, matching `FieldCatalogVectorUninitialized` (`:1493`).
- **Issue enum `:153`** — append: `HiddenByDeclaredProperty = 32`; then the eleven vector stops mirroring `ValidateCatalogVector` (`:1476-1631`): `PropertyCatalogVectorUninitialized = 33`, `PropertyCatalogModuleCountBoundReached = 34`, `PropertyCatalogSlotsIncomplete = 35`, `PropertyCatalogSlotCountConflict = 36`, `PropertyCatalogMissing = 37`, `PropertyCatalogNonExact = 38`, `PropertyCatalogInvalid = 39`, `DuplicatePropertyCatalogModule = 40`, `PropertyCatalogModuleNotInPortfolio = 41`, `PropertyCatalogSourceEndsMismatch = 42`, `PropertyCatalogAuthorityMismatch = 43`.
- **`:175-188`** boundary enum: **delete** `PropertyAndEventTablesNotModeled = 1` (hole at 1, never reused); append `EventTablesNotModeled = 5` and `PropertyAccessorSemanticsNotModeled = 6`. Update `BaseBoundaries` (`:1665-1675`) — both arms emit `EventTablesNotModeled` and `PropertyAccessorSemanticsNotModeled` in place of value 1.
- **`:1266-1270`** add `var propertyRows = propertyCatalogsByModule[walkModule].RowsForDeclaringTypeOrEmpty(walkType);` and `var levelPropertyToken = 0;`.
- **After the method pass (`:1340`), before `levels.Add` (`:1345`)** insert the third pass:
```csharp
foreach (var propertyRow in propertyRows)
{
    if (!string.Equals(propertyRow.Name, name, StringComparison.Ordinal)) { continue; }
    observedCount++;
    accessibleCandidateCount++;
    if (levelPropertyToken == 0) { levelPropertyToken = propertyRow.PropertyToken; }
}
```
It increments the same `accessibleCandidateCount` that gates the break at `:1352-1359`, and it **does not charge `decisionCount`** — it makes no accessibility decision, so charging `AccessibilityCheckCountBoundName` would falsify that bound. The pass is bounded by the Property table row bound. A property's accessors are named `get_P`/`set_P` and can never match `P` in the method pass at `:1318-1323`, so this is genuinely new blocking.
- **`:1345`** `IssueLevel(levelIndex, walkType, fieldRows.Length, methodTokens.Length, propertyRows.Length, accessibleCandidateCount)`; `StaticFieldV2MemberLookupLevelIdentity` **CanonicalSchemaVersion 1 → 2** with `examinedPropertyCount` written positionally after `examinedMethodCount` (`:466`).
- **Precedence (`:1411-1451`)** — insert the property branch **last, immediately before `var selected = winningStaticFields[0]` at `:1451`**:
```csharp
if (winningPropertyToken != 0)
{
    return StaticFieldV2MemberLookupOutcome.IssueComplete(
        StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember,
        StaticFieldV2MemberLookupIssue.HiddenByDeclaredProperty,
        request, null, consultedLevels, [.. examined],
        currentChain.TerminalKind, boundaries, observedCount, winningPropertyToken);
}
```
This position is chosen deliberately: `HiddenByDeclaredMethod`, `HiddenByInstanceField`, `AmbiguousStaticDeclarations`, `Absent` and `Partial` all keep their exact issue values, and the **only** answer that changes kind is a level where a property owns the name. A level declaring both an accessible static field and a same-name property (illegal in C#, legal in IL) reports the property — the conservative answer, stated as a rule and tested.
- **`:1476-1631`** add `ValidatePropertyCatalogVector`, keyed identically to `:1486-1489`, raising the eleven stops above.

### 4.7 `StaticFieldV2LexicalCompleteness.cs`
- **`:420`** `StaticFieldV2LexicalCertificateRequest` **CanonicalSchemaVersion 1 → 2**; add `ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs` written positionally after the field-catalog block (`:447-451`).
- **`:262-281`** boundary enum: **delete** `PropertyAndEventTablesNotModeled = 1` and `ImportedMemberGroupBlockingNotModeled = 6` (holes at 1 and 6); append `EventTablesNotModeled = 7`, `ImportedEventGroupBlockingNotModeled = 8`, `PropertyAccessorSemanticsNotModeled = 9`. Rebuild `CertificateBoundaries` and `BareRootBoundaries` (`:1239-1255`).
- **`:2093-2141`** `CertifyTypeMemberNames` gains a property pass over `propertyCatalog.RowsForDeclaringTypeOrEmpty(owner)` returning `Owned(TypeMemberName, examined, property.Name, propertyToken)`, and a `propertyCatalog is null` → `Incomplete(TypeMemberName, MemberCatalogMissing)` guard mirroring `:2098-2105`.
  **Verified safe for incident 31:** the selected type is `PropertyNameProbe` (`tests/PhoenixInspect.W8CoordinatorShapeTarget/Program.cs:113`), while `RegionTotal` is declared on `CoordinatorTotals` (`CoordinatorGraph.cs:93`) and reached through the file-scoped `using static` at `Program.cs:2`. `CoordinatorTotals` is **not** in the declaration chain, so the certificate still returns `Absent`, does **not** short-circuit to `Shadowed` (`:1488-1503`), and the predeclared `lexicalCompleteness: Complete` / `memberLookup: HiddenByUnsupportedMember` pair is preserved.
- **`:29-30`** update the `TypeMemberName` doc text to "field, method, or property".
- **`:1858-1866`** the internal member-lookup re-issue threads the property vector through.

### 4.8 Corpus runner — `tests/PhoenixInspect.IntegrationTests/W8MeaningfulSyntheticCorpusTests.cs`
- **`:1196-1230`** `W8CorpusEvaluationWorld` ctor + properties: add `ConstantCatalogs` and `PropertyCatalogs` mirroring `FieldCatalogs`.
- **`:2113-2250`, `:2276-2282`** `BuildSyntheticCoreModule` / `SyntheticCoreModule`: supply `declaredMemberRowCounts: Create(0, 0, 0)` on the core's search fact and mint empty-Exact Constant and Property catalogs.
- **`:1301-1305`** prepend both vectors as `[core.X, .. produced.Select(m => m.Outcome.Y!)]`.
- **`:1580-1589`, `:1596-1603`, `:1655-1662`** thread both vectors through all three entry points.
- **`:693-739`** `ApplyCounterfactual`: add `"request-declared-field-instead-of-property"` → `world.Evaluate(incident.CounterfactualExpression!, incident.ReadWidth, …)` and `"withhold-literal-constant-source"` → evaluate with an empty constant-catalog vector.
- **`:960-963`** generalize `CounterfactualExpression` from the hardcoded `substitute-closed-type-argument` case to a manifest-supplied per-action expression; add `counterfactualExpression` fields to incidents 12 (`global::PhoenixInspect.W8WorkflowShapeTarget.BaseStage.Sentinel`) and 31 (`DeclaredRegionTotal`) in `tests/corpus/w8-static-field-incidents-v1.json`.
- **`:637-663`** rewrite the incident-31 block (see §6).

**Incident disposition, stated honestly:** incidents **12** (`HidingStage : BaseStage` with `public static new int Sentinel => …`, verified at `tests/PhoenixInspect.W8WorkflowShapeTarget/WorkflowGraph.cs:54-57`) and **31** flip `manifest-only → executed`. Incident **32** stays `manifest-only`: its recorded blocker is the undecoded ground TypeSpec alias, a second independent gap this slice does not close. **Do not cite incident 32 as evidence the Constant table landed.**

---

# 5. BOUNDARIES RETIRED

**Retired outright (member deleted, numeric hole left):**
1. `StaticFieldV2PipelineCoverageBoundary.MetadataConstantRowSuppliedByCallerSeam = 3` (`StaticFieldV2ExpressionPipeline.cs:73`). Hole at 3; values 4-9 keep their numbers because the ints are written raw into provenance at `:1427-1431`. Honest **only** because the `Func` seam itself is deleted — keeping it beside the catalog would be a fallback.
2. `StaticFieldV2StorageCoverageBoundary.ConstantTableSuppliedByCaller = 4` (`StaticFieldV2StorageStrategyBinder.cs:104-105`). Retirable because `Create` now takes a catalog-minted row whose parent is verified against the requested FieldDef token.

**Deleted and replaced by a narrower value (never re-numbered in place — reusing a value would keep frozen digests byte-identical while changing what they mean):**
3. `StaticFieldV2MemberLookupCoverageBoundary.PropertyAndEventTablesNotModeled = 1` → `EventTablesNotModeled = 5`. Event 0x14 / EventMap 0x12 remain genuinely unmodeled.
4. `StaticFieldV2LexicalCoverageBoundary.PropertyAndEventTablesNotModeled = 1` → `EventTablesNotModeled = 7`.
5. `StaticFieldV2LexicalCoverageBoundary.ImportedMemberGroupBlockingNotModeled = 6` → `ImportedEventGroupBlockingNotModeled = 8`, with corrected text. The old text was **already partly false**: a same-name method declared directly on an imported owner does block today, via the level-zero fall-through at `:1666-1692` into `:1814-1826`.

**Added as the honest price of the scope cut:**
6. `StaticFieldV2MemberLookupCoverageBoundary.PropertyAccessorSemanticsNotModeled = 6` and `StaticFieldV2LexicalCoverageBoundary.PropertyAccessorSemanticsNotModeled = 9` — "MethodSemantics (0x18) is not modeled, so a same-name property blocks without any test of whether its accessors are reachable from the use site." Declared unconditionally in both accessibility modes.

**Explicitly retained, unchanged:** `InterfaceAncestryNotModeled = 2`, `FriendAssemblyAttributesNotModeled = 3`, `AccessibilityBypassApplied = 4`; `RangeAndPatternVariableNamesNotModeled = 2`, `LocalFunctionParentRelationNotPhysical = 3`, `DirectlyDeclaredMemberSelectionDeferred = 4`, `UsingStaticInheritedMembersNotImported = 5`; `CustomAttributeTableNotModeled = 1`, `EnumUnderlyingDerivedFromInstanceValueField = 5`. `decimal` remains attribute-encoded and out of scope (`StaticFieldV2StorageStrategyBinder.cs:1185-1192`), notwithstanding `docs/plans/post-w7-path-forward.md:422-424`, which should be corrected in the same commit.

---

# 6. FROZEN ARTIFACTS THAT MOVE — exhaustive

| # | Golden | file:line | Why moving is legitimate |
|---|---|---|---|
| 1 | `9452ab47de754999f808a7d14b51c89485ee681a117cf0e3230a3d2f2ffcc4a8` | `W8V2StorageStrategyTests.cs:592` | `ConstantTableSuppliedByCaller = 4` deleted from `ProjectLiteral`'s unconditional seed (`StaticFieldV2StorageStrategyBinder.cs:1284-1286`), and the projection request now embeds a catalog-minted row digest instead of `(typeCode, blob)` at schema 2. Both are the boundary genuinely ceasing to be true. |
| 2 | `c8cc916736efd2d3ba4cb324b336ee3d7cf706f5a5b0cf9776068dd46f59d020` | `W8V2MemberLookupTests.cs:785` | `declaredCoverageBoundaries` are written unconditionally at `StaticFieldV2MemberLookup.cs:886-890`; boundary value 1 is gone, two new values are present, and the request carries a required property vector at schema 3. |
| 3 | `8006dd4dcb035c0585ba9da0d70ea37937c4071152a1b57e253ef33519b1a104` | `W8V2LexicalCompletenessTests.cs:637` | Both boundary arrays (`StaticFieldV2LexicalCompleteness.cs:1239-1255`) change, and the certificate request gains the property vector at schema 2. |
| 4 | `0fca3a0b3d380df96ab1b2ab1635916f088fd92199a732637c4bd4cedb52562e` | `W8V2ExpressionPipelineTests.cs:42` | Request schema 2→3 (deleted literal-seam boolean at `:1100`), provenance 2→3 (deleted `literalConstant` slot at `:1415`), ledger 2→3 (deleted third `Int32` at `:132`), plus the member-lookup digest embedded at `:1405`. |
| 5 | `2d8a82713bb85603df039f40715af46b36e8fa0ade6578391b2d0e0eaf396aff` | `W8V2GenericVarFieldTests.cs:19` | Same four causes. |
| 6 | `1f4bc39fc29db0e8c5881f32c5b01118ecd4d46c30c1247cb8890a526870a7d4` | `W8V2SuffixEvaluationTests.cs:15` | Same four causes. |
| 7 | `fd50f4ded62b10c8531275f8bd040656a324f964777ba473aabb818b14babe90` | `W8FrameValueV1PipelineTests.cs:19` | Request/provenance/ledger schema bumps only — `RunFrameValue` never calls member lookup (`StaticFieldV2ExpressionPipeline.cs:1901-1944`), but it does embed `evidenceLedger.Sha256` (`:1425`). |
| 8 | `edadd88f3b2879bbb1671aa618fc198f39761e16bfe6200b35f2c58bef41c390` | `W8CorpusPortfolioReportTests.cs:45` | Manifest-text digest; the comment at `:41-43` says the pair is re-frozen whenever a runner-execution status legitimately changes. |
| 9 | `1b5190b1408d15d34ab72ca659f736ddc32904a6f48e1acf78964e16cd928981` | `W8CorpusPortfolioReportTests.cs:48` | Same. |
| 10 | Counts `13`/`22` | `W8CorpusPortfolioReportTests.cs:50-51` | → `15`/`20` as incidents 12 and 31 flip. |

**Non-digest assertions that must be rewritten in the same commits:**
- `W8V2MemberLookupTests.cs:811-814` and `:83-85` (positional `DeclaredCoverageBoundaries[0]`) → retarget to `EventTablesNotModeled`.
- `W8V2LexicalCompletenessTests.cs:653-654`, `:663-665`, `:666-668` (mutation value + positional boundaries) → retarget.
- `W8MeaningfulSyntheticCorpusTests.cs:642` `"manifest-only"` → `"executed"`; `:651-654` axis string `…/Absent/…` → `…/HiddenByUnsupportedMember/…`; `:661` `Absent` → `HiddenByUnsupportedMember`; **`:662` `Assert.NotEqual(PredeclaredAxes, produced.Axes)` → `Assert.Equal`** (verified: incident 31's `expectedAxes` differ from the produced axes **only** in `memberLookup`, so the two become equal the moment that axis flips).
- `tests/corpus/w8-static-field-incidents-v1.json` — incidents 12 and 31: `runnerExecution.status` → `executed`, reasons cleared, `counterfactualExpression` added. The file carries **no** 64-hex digests, so this is a manifest edit only.

**Explicitly NOT moved, and this is the design's payoff:**
- The full source-end-derived set — `W8FieldDefinitionTableCatalogContractTests.cs:414` `acbf1a2e…`, `W8CompilerNameMappingContractTests.cs:217/:218/:272`, `W8MetadataGenericParameterProofContractTests.cs:288-292`, `W8MetadataGenericParameterConstraintAuthorityContractTests.cs:111/:112/:178/:179/:242/:532/:534/:537`, `W8W7TypeDefinitionCompatibilityContractTests.cs:325`, `W8MetadataDefinitionCompatibilityPortfolioContractTests.cs:255/:256`, `W8MetadataNamedTypeDefinitionChainContractTests.cs:104/:442`, `W8MetadataAncestryAuthorityContractTests.cs:134`, `W8MetadataConstraintTargetResolutionContractTests.cs:83`, `W8MetadataTypeReferenceResolutionContractTests.cs:131`, `W8MetadataReferencePhysicalTableContractTests.cs:168/:171`, `W8InterfaceImplementationAuthorityTests.cs:218`, `W8MetadataConstructionContractTests.cs:859-862`, `W8V2TypeNameBindingTests.cs:393`, `W8V2ScopedContextBindingTests.cs:569`, `W8V2ClosedConstructionBindingTests.cs:685`, `W8V2AssignabilityTests.cs:809`, `W8V2RuntimeConstructionTests.cs:34` — because `MetadataSourceEndIdentity` is untouched and the three new counts enter `StaticFieldModuleSearchFact` behind a field tag written only when the bundle is supplied. **If a reviewer sees three unconditional `WriteOptionalInt32` calls appended at `StaticFieldSymbolContracts.cs:5873` instead, reject the change** — that re-freezes all of the above.
- `W8V2SyntaxProjectionTests.cs:19-22`, `W8FrameValueV1ProjectionTests.cs:13-15`, `W7LegacyCompatibilityGoldenTests.cs:431-444`.
- `MetadataModuleAcquisitionOutcome` / `MetadataAuthorityPortfolioOutcome` — no test freezes either; `W8MetadataAuthorityProducerTests.cs:308-335` compares two live acquisitions and its only 64-hex literal is the synthetic snapshot fixture at `:26-27`.

---

# 7. TESTS

### Fast — new files
**`W8DeclaredMemberSourceEndContractTests.cs`**
- *Physical fact:* a fact built **without** the bundle is canonically byte-identical to the pre-change build. Pin the pre-change digest of one representative synthetic fact as a one-shot regression literal, then delete it once green. **This is the single test that protects the entire 20-golden non-moving set.**
- `Create` throws when the fact is non-exact, when `PropertyDefinitionRowCount` is null, and when the bundle is absent.
- `StaticFieldModuleDeclaredMemberRowCounts.Create` rejects negatives and values above `0x00FF_FFFF`.
- `ContainsHasConstantParentToken` classifies 0x04/0x08/0x17 and rejects out-of-range rowIds and foreign tables.

**`W8ConstantTableCatalogContractTests.cs`** — full family template (`W8FieldDefinitionTableCatalogContractTests.cs:11-24`, `:420-447`, `:494-551`): sealed class, XML doc on class and every test, `[Trait("Category","Fast")]`, `DeclaredOnly` public statics pinned to exactly `["Create"]` on observation and catalog and none on the row, all three types sealed with no public constructors, `OwnsRowMintCapability(new object())` false plus a direct `Create(new object(), …)` throw, `AssertPublicDraftXml` over all four enums and all three identities, one case per issue value asserting `Rows.IsEmpty` and (for Invalid) a null `ReachedBound`, and a canonical-replay/defensive-copy test pinning a new frozen catalog digest. Plus the physical facts:
- Each of the three `HasConstant` kinds is range-checked against **its own** end; a Property-tagged parent above `PropertyRowCount` → `ParentTokenOutOfRange` (proves the whole table is covered, not a field-filtered subset).
- Width table over every admitted type code (1/2/4/8, STRING even) → `ConstantValueWidthInvalid` off-by-one both directions; CLASS with a non-zero byte → `NullReferenceValueNonZero`; 0x0F and 0x18 → `ConstantTypeCodeNotAdmitted`.
- **The bidirectional pairing, both directions**: `HasDefault` field with no row → `FieldDefaultFlagWithoutConstantRow`; Field-parented row on a non-`HasDefault` field → `FieldParentWithoutDefaultFlag`; `Literal` without `HasDefault` → `FieldLiteralWithoutDefaultFlag`; two rows sharing a parent → `DuplicateParentConstant`.
- An unsorted Parent order is **recorded** in the profile without becoming an issue.
- `DispositionForField` returns `AbsentByDeclaredAttributes` (not null) for a non-`HasDefault` field, and `OwnerNotIssuedByThisCatalog` for a byte-different FieldDef row issued by a foreign module's catalog.

**`W8PropertyTableCatalogContractTests.cs`** — same template, plus:
- `PropertyPointerRowCount != 0` → `NonExact/PropertyPointerIndirectionNotModeled` with `Rows.IsEmpty` and a **null** bound.
- **Two same-named indexer properties with different signatures on one owner are ACCEPTED** (this is the test that would have caught P3's `(owner, name)` rule); identical `(owner, name, signature)` → `DuplicatePropertySignature`.
- Owners in **decreasing** RID order with contiguous blocks are **accepted** (PropertyMap is not an ECMA-sorted table); an owner re-appearing after its block closed → `OwnershipBlockNotContiguous`.
- `blockCount == PropertyMapRowCount - 1` (an empty PropertyMap run) is **accepted**; `blockCount > PropertyMapRowCount` → `OwnershipBlockCountConflict`; `PropertyMapRowCount == 0` with a non-empty Property table → same.
- Reflection assertion that the row type exposes **no** accessibility and **no** accessor property (mirrors `W8FieldDefinitionTableCatalogContractTests.cs:426-427`).

**`W8V2PropertyNameHidingTests.cs`**
- A same-name property on the nearest level → `HiddenByUnsupportedMember` / `HiddenByDeclaredProperty` / `RelatedMetadataToken` = the Property token.
- A level declaring **both** a same-name method and a same-name property still reports `HiddenByDeclaredMethod = 20` (pins replay of every existing answer).
- A level declaring both an accessible static field and a same-name property reports `HiddenByDeclaredProperty` — the stated conservative rule.
- A **private** property still blocks, and `PropertyAccessorSemanticsNotModeled` is present in `DeclaredCoverageBoundaries` — the executable proof that the residual is declared rather than guessed.
- All eleven `ValidatePropertyCatalogVector` stops.
- Two levels that examined zero properties versus one that examined three have distinct level digests.

### Fast — extended files
- **`W8V2MemberLookupTests.cs`**: re-freeze `c8cc9167…`; retarget `:83-85`, `:811-814`; extend the `publicTypes` guard at `:888-921` and `AssertPublicDraftXml` at `:922`.
- **`W8V2LexicalCompletenessTests.cs`**: re-freeze `8006dd4d…`; retarget `:653-654`, `:663-668`; add a test that a property declared on an **enclosing** type of the selected type yields `Owned(TypeMemberName)`; extend the guard at `:711`.
- **`W8V2StorageStrategyTests.cs`**: rebuild the ~35 per-encoding cases at `:231-497` to source rows from a synthetic Constant catalog; new `ConstantRowParentMismatch` case (a catalog-minted row whose parent names a **different** FieldDef); new `ConstantValueBoundReached` case (a 2051-byte blob is a **per-field** NonExact stop while the catalog stays Exact); re-freeze `9452ab47…`; extend the surface guard at `:665-712`. **Note for the implementer: this file contains an embedded NUL near offset 15277, so ripgrep classifies it as binary and silently skips it — use `grep -a` or strip NULs when auditing coverage.**
- **`W8V2ExpressionPipelineTests.cs`**: delete `LiteralSource(int)` (`:829-838`) and its seven call sites (`:186, :191, :254, :295, :470, :530, :547`), replacing them with a Constant-catalog vector; new tests — a literal answered end-to-end with **no seam anywhere in the request** and an all-zero capability ledger; a foreign-module catalog answers `Unavailable` **without throwing** (closing the no-fallback defect at `:2347-2353`); a NonExact catalog → `Unavailable`, an Invalid catalog → `Invalid`; re-freeze `0fca3a0b…`; extend the public-surface array (which currently pins `typeof(StaticFieldV2ExpressionProvenance)` and `StaticFieldV2LiteralConstantFact`).
- **`W8MetadataAuthorityProducerTests.cs`**: three new row-count equality assertions (`TableIndex.Constant/PropertyMap/PropertyPtr`) at `:50-75`; three new "slot non-null and Exact" assertions at `:76-136` (otherwise "every catalog exact" silently under-covers); three new null assertions in the truncated-image prefix-free stop at `:357-366`; extend `publicTypes` at `:388-400`.
- **`W8V2ContractFoundationTests.cs`**: assert the deliberate numbering holes — `StaticFieldV2PipelineCoverageBoundary` has no member 3, `StaticFieldV2StorageCoverageBoundary` no member 4, both coverage-boundary enums no member 1, `StaticFieldV2LexicalCoverageBoundary` no member 6, `StaticFieldV2PipelineEvidenceKind` no member 3 — so a later slice cannot quietly reuse them; assert the two new bound names appear in `ExpressionV2ContractLimits.AllDeclaredBounds`; re-verify `AllDeclaredBounds[0] == "expression-v2.access.checks"` still holds (it does — both new names sort under `expression-v2.metadata.`).

### Dump
- **`W8CompilerPhysicalTruthTests.cs`** — **the oracle, and the only test that proves the catalog against real module bytes.** For each of the ~35 named literals in the expectations table at `:445-495`, assert the landed catalog's `FindRowForFieldDefinition` reproduces the exact `TypeCode` and value blob that `reader.GetConstant(field.GetDefaultValue())` yields via `AssertMetadataLiteral` (`:2566-2579`), including both `NullReference` cases. Without this, the catalog's only proof is synthetic self-consistency.
- **`W8PropertyPhysicalTruthTests.cs` (new)** — against the real compiled target: catalog row count == `reader.GetTableRowCount(TableIndex.Property)`; every row's owner == `PropertyDefinition.GetDeclaringType()`; every row's `Attributes`/`Name` match; distinct-owner block count `<=` `GetTableRowCount(TableIndex.PropertyMap)`; `GetTableRowCount(TableIndex.PropertyPtr) == 0` on Roslyn output.
- **`W8MeaningfulSyntheticCorpusTests.cs`** — incidents 12 and 31 driven live to `HiddenByUnsupportedMember`, with `request-declared-field-instead-of-property` implemented and proven to differ (both are `decisionChanging=true`, so `:310-338` drives them). Per the recorded local-lane note this needs `CI=true` builds and, because rebuilding `W8CoordinatorShapeTarget` invalidates the pinned PDB identity, a **fresh dump capture** for incident 31 rather than a rerun.

### Gating without edits
`W8MetadataConstructionContractTests.cs:1497-1513` (no public issuer returns a guarded type), `:1580-1614` (every exported `Metadata*` type sealed and fully documented), and `W8MetadataAuthorityIssuerGuardTests.cs:18-43` (no `Metadata*Identity` factory takes a W7 `StaticFieldTypeDefinitionIdentity`) all pull the six new types in automatically. If any is shaped wrongly these fail without a new test being written.

---

# 8. ORDER OF WORK — green at every step boundary

1. **Row-count bundle + tagged trailer.** Add `StaticFieldModuleDeclaredMemberRowCounts`, thread the optional parameter through `StaticFieldModuleSearchFact`, add the trailer at `:5873`, extend the token switch at `:9319-9341`. Add `W8DeclaredMemberSourceEndContractTests` byte-stability test **first** and run the full matrix. *Nothing else changes; every golden must still pass. If any moves here, stop — the trailer condition is wrong.*
2. **Source-end extension.** Add `MetadataDeclaredMemberSourceEndContracts.cs` + its tests. Producer reads the three counts and supplies the bundle. Full matrix green (no consumer uses the extension yet).
3. **Constant catalog contract, unwired.** Add `MetadataConstantTableContracts.cs` + `W8ConstantTableCatalogContractTests`. Register `"expression-v2.metadata.constant-rows"`. Full matrix green.
4. **Property catalog contract, unwired.** Add `MetadataPropertyTableContracts.cs` + `W8PropertyTableCatalogContractTests`. Register `"expression-v2.metadata.property-rows"`. Full matrix green.
5. **Producer wiring.** Stages 23-25, drafts, outcome properties, `WriteOptionalDigest` appends, both readers, both stage blocks; extend `W8MetadataAuthorityProducerTests`; add `W8PropertyPhysicalTruthTests`. Full matrix green — no contract digest exists for the producer outcome.
6. **Constant consumer, one commit.** Delete the seam, `StaticFieldV2LiteralConstantFact`, the ledger slot + evidence-kind member, the provenance slot, and boundary values 3 and 4; bump the four schemas; rewrite `AcquireConstantRow`/`ProjectLiteral`/`StaticFieldV2LiteralProjectionRequest`; migrate `W8V2ExpressionPipelineTests` and `W8V2StorageStrategyTests` off `LiteralSource`; extend `W8CompilerPhysicalTruthTests`. **Re-freeze goldens 1, 4, 5, 6, 7 in this commit.** Full matrix green.
7. **Property consumer, one commit.** Member-lookup request schema 3 + required vector + third pass + precedence + level schema 2 + issue values 32-43 + boundary retirement; lexical request schema 2 + property pass + boundary retirement; add `W8V2PropertyNameHidingTests`; extend the two lookup/lexical test files. **Re-freeze goldens 2 and 3** (and 4, 5, 6 again — they embed the member-lookup digest). Full matrix green.
8. **Corpus, one commit.** World vectors, synthetic core, three entry points, two counterfactual actions, manifest edits for incidents 12 and 31, incident-31 block rewrite. **Re-freeze goldens 8, 9 and counts 10.** Fast lane green; Dump lane green after a fresh `CI=true` capture.
9. **Docs.** Correct `docs/plans/post-w7-path-forward.md:422-424` (decimal remains attribute-encoded) and record the deferred MethodSemantics follow-up.

Steps 6 and 7 each move goldens *within* the step, so the matrix is green at every step **boundary** but not mid-step. Do not split either.

---

# 9. RISKS AND UNKNOWNS — verify these first

1. **`PropertyDefinition.GetDeclaringType()` behaviour when no PropertyMap row covers the property.** SRM may return a nil handle or throw `BadImageFormatException`. Confirm which, and confirm the producer's existing `IsRowFault` band (`MetadataAuthorityProducer.cs:622`) classifies the throw as `PhysicalRowRejected`. A nil handle must reach the catalog as token `0x02000000` → `DeclaringTypeDefinitionNotIssued`. **Verify before writing `ReadPropertyRows`.**
2. **`GetDeclaringType()` on an image with a non-empty PropertyPtr.** The catalog refuses such images (`PropertyPointerIndirectionNotModeled`), but the *producer* still calls `GetDeclaringType()` on every RID before the catalog sees the count. Reorder so the producer checks `GetTableRowCount(TableIndex.PropertyPtr) != 0` and passes `default` observations in that case, letting the catalog produce the typed NonExact — same shape as `MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default)` at `:971`.
3. **`reader.GetConstant` on a malformed `HasConstant` parent.** SRM decodes `Parent` to an `EntityHandle` and may throw before the observation is built, which makes `ParentTokenKindInvalid` unreachable from real modules. That is acceptable (it is reachable from synthetic observations, and the throw is a typed producer stop), but **confirm the `IsRowFault` band catches it** so it does not escape `AcquireModuleCore`.
4. **`FrameValueV1Limits.AllDeclaredBounds` is a hard-coded inclusion list.** Confirm that `W8V2ContractFoundationTests.cs` (~`:485-525`) filters `FrameValueV1Limits` by an explicit whitelist and `StaticFieldV2Limits` by *exclusion* of `expression-v2.frame.*`. The two new `expression-v2.metadata.*` names are then automatically in the latter and automatically out of the former. **If either filter is the other way round, the names must change.**
5. **Whether `metadata-v2.methoddef-signature.bytes` is registered in `DeclaredBounds`.** The survey claims registration is asserted at `W8MemberPointerTableCatalogContractTests.cs:295-309`. Check the actual assertion: if contract-local `metadata-v2.*` caps **are** registered, register the three new ones too; if only `expression-v2.*` names are, leave them as public const pairs only.
6. **Whether `RunFrameValue` can ever reach the literal route.** It should not (`StaticFieldV2ExpressionPipeline.cs:1901-1944`), which is why the frame provenance never carries a literal-constant-row digest. Confirm before relying on it in the re-freeze rationale for golden 7.
7. **Incident 12's counterfactual expression.** `BaseStage.Sentinel` must be a real static **field** on `BaseStage` for `request-declared-field-instead-of-property` to reach `ExactValue` on the same snapshot. Read `tests/PhoenixInspect.W8WorkflowShapeTarget/WorkflowGraph.cs` around `:40-57` and confirm before adding the manifest field.
8. **Roslyn's Property attribute bits.** `PropertyAttributesNotAdmitted` rejects anything outside `0x0200 | 0x0400 | 0x1000`. Verify against the real compiled targets that no fixture property carries another bit before making this Invalid rather than a recorded profile.
9. **Public-surface guard drift.** `W8V2ExpressionPipelineTests` pins an exact array of public types including `StaticFieldV2LiteralConstantFact`; `W8V2StorageStrategyTests.cs:665-712` pins seventeen public types. Both must be edited in steps 6 and 7 respectively or the build fails on an unrelated-looking assertion.
10. **The Dump lane needs `CI=true` builds, and rebuilding `W8CoordinatorShapeTarget` invalidates the PDB identity of previously captured demo dumps.** Incident 31's pinned snapshot therefore needs a **fresh capture**, not a rerun, in step 8.