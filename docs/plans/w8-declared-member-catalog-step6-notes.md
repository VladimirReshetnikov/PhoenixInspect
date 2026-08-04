# Step 6 preparation notes: retiring the Constant caller seam

Steps 1–5 of [the plan](w8-declared-member-catalog-plan.md) are landed and green. This note records what a first pass
through step 6 established, so the next attempt starts from measured facts rather than from the plan's estimate.

## The product side is settled and compiles

A complete pass over the product edits builds clean with zero warnings. Nothing about the design needed revision:

- `StaticFieldV2LiteralProjectionRequest` takes a `MetadataConstantTableRowIdentity` instead of a loose type code and
  blob, at schema 2. Both of its throw paths disappear, because a row a complete Constant table already validated
  cannot be malformed. What remains worth checking is the one thing a caller can still get wrong — pairing a valid row
  with the wrong field — and that becomes the typed `ConstantRowParentMismatch` stop rather than a throw.
- The value cap becomes `ConstantValueBoundReached`, deliberately a per-field stop: one over-long `const string` must
  not make every other literal in the same module unanswerable.
- `StaticFieldV2StorageCoverageBoundary.ConstantTableSuppliedByCaller` is deleted with a numbering hole at 4, and the
  literal seed becomes `CustomAttributeTableNotModeled`. The retired value is not renumbered and not reused: the same
  boundary int must never come to mean a different fact across replays.
- On the pipeline: `StaticFieldV2LiteralConstantFact` is deleted outright; the evidence kind, its ledger counter, and
  `MetadataConstantRowSuppliedByCallerSeam` go with it, each leaving a numbering hole at 3. The ledger bumps to
  schema 3 and the provenance to schema 3, because the retained slot changes from a caller-supplied fact to a proven
  catalog row at the same position — identical bytes must never describe a different kind of evidence.
- `AcquireConstantRow` becomes a catalog projection returning typed stops. The silent `Unavailable` that a null seam
  produced is gone: over an exact catalog a proven absence on a field that declares `Literal | HasDefault` is a
  contradiction and reports `Invalid`, while a non-exact catalog reports `Unavailable` or `Invalid` on its own
  disposition.

## What the plan under-estimated: the test migration

The plan's step 6 lists the test work as "migrate `W8V2ExpressionPipelineTests` and `W8V2StorageStrategyTests` off
`LiteralSource`". Measured, that is the larger half of the step:

- `W8V2StorageStrategyTests` has **seven** `StaticFieldV2LiteralProjectionRequest.Create` call sites, and it is the
  primary evidence for literal decoding — every admitted type code, every stop, the string cap, the null encoding,
  the enum-underlying path. Each call site currently passes a loose type code and blob. Each now needs a real
  `MetadataConstantTableRowIdentity`, which means a synthetic Constant-catalog world per case: field row, field
  catalog, source ends carrying the declared-member bundle, declared-member source ends, and matching observations.
  **A shared fixture builder is needed first**; migrating the call sites individually would duplicate that world seven
  times.
- `W8V2ExpressionPipelineTests` builds its own source ends at one local call site, which is tractable, but the literal
  value is currently per-test (7, 11, 99) while a catalog is per-world. Either the world takes a per-field constant
  map, or those tests get their own worlds. The per-field map is preferable: the catalog must be exact, so every
  `HasDefault` field needs a row regardless, and a map lets one field's value vary without disturbing the others.
- Note for anyone auditing coverage with a text search: `W8V2StorageStrategyTests.cs` contains an embedded NUL byte,
  so ripgrep classifies it as binary and silently skips it. Use `grep -a`.

## Recommended sequencing change

Split the *test* work ahead of the product work, which the plan's ordering does not allow for:

1. Land a shared synthetic Constant-catalog fixture builder in the test project, unused, with the matrix green.
2. Then apply the product edits and migrate both test files against that builder in one commit, re-freezing the five
   goldens the plan enumerates.

Step 6 still cannot be split across the product edits themselves — deleting the fact type breaks both test files at
once — but the fixture builder is independent of that and can land first, which shortens the window in which the tree
does not build.
