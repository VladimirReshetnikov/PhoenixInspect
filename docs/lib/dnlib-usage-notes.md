# dnlib usage notes for this project

## Why it matters

dnlib remains an important reference point for this project even if it is not our default backend.

It is historically strong for:

- broad .NET assembly metadata handling,
- IL read/write scenarios,
- practical support patterns across reverse-engineering/debugging tooling,
- both portable and Windows PDB integration paths.

## Snapshot review highlights

The `lib/dnlib/src` snapshot is centered around a single `dnlib.csproj` with major namespaces grouped by concern:

- `DotNet` (metadata object model and high-level definitions),
- `DotNet/Emit` (IL body representation and readers),
- `DotNet/MD` (metadata stream/table internals),
- `DotNet/Pdb` (portable + managed/windows symbol readers),
- `PE`, `IO`, and `W32Resources` (binary substrate support).

This layout reinforces dnlib's "one stack" ergonomics: metadata, CIL, and symbol features can be consumed from one library surface.

## Source-level API surfaces relevant to our adapters

### 1) Module loading and metadata stream access

`ModuleDefMD` is the principal entry for loading modules from file/bytes/streams and exposes direct access to metadata streams (`TablesStream`, `StringsStream`, `BlobStream`, etc.).

Practical implication: dnlib can satisfy both high-level and low-level metadata needs, but our adapter should avoid exposing stream-specific details outside ingestion.

### 2) Method and body model richness

`MethodDef` and `CilBody` together provide object-model access to:

- signatures and generic context,
- instruction collections,
- locals and exception handlers,
- method-level metadata attributes.

Practical implication: dnlib can produce the normalized method-body contract we want with relatively low projection overhead.

### 3) Explicit method-body reader pipeline

`MethodBodyReader` APIs expose factory methods that accept operand resolvers, EH readers, and generic parameter context.

Practical implication: this is useful for controlled decoding experiments (including malformed/partial bodies), which helps our miss-reason and diagnostics taxonomy work.

### 4) Dual symbol path strategy

Under `DotNet/Pdb`, the snapshot contains distinct implementations for:

- managed/Windows PDB reader paths,
- portable PDB reader paths,
- symbol reader factories and options.

Practical implication: dnlib can be a realistic fallback for mixed symbol ecosystems, but symbol projection must be normalized so we do not fork host behavior by backend.

## Potential project applications

1. **Alternative metadata backend**
   - used behind the same normalized contracts as AsmResolver/SRM-based implementations.
2. **Compatibility fallback option**
   - useful where another backend struggles with specific edge-case binaries.
3. **Cross-checking backend during design**
   - can validate assumptions in metadata normalization and method-body parsing.
4. **Failure taxonomy training backend**
   - controlled reader APIs make it practical to probe malformed input behavior.

## Boundary and architecture guidance

- Do not bake dnlib object models into interpreter or public project contracts.
- Keep dnlib support as a pluggable adapter path until explicit MVP commitments are made.
- Prefer decision records documenting when dnlib is required vs optional.
- Keep symbol quality/provenance mapping backend-neutral even if dnlib is used for both metadata and symbols.

## Risks and design pressure

1. **Third-stack overhead**
   - running SRM + AsmResolver + dnlib in parallel can fragment design effort.
2. **Contract drift**
   - if backend-specific quirks leak upward, normalized semantics become unstable.
3. **Maintenance cost**
   - each additional backend multiplies regression coverage needs.
4. **Reader-mode complexity**
   - multiple symbol reader options (portable/managed/native) can create policy ambiguity unless explicitly governed.

## Recommended next experiments

1. Produce one dnlib-based adapter spike that only maps method body + generic context + symbol sequence points.
2. Run malformed IL body fixtures through `MethodBodyReader` and classify outcome patterns into miss reasons.
3. Compare dnlib and AsmResolver projections for the same generic-heavy methods and document semantic diffs.
4. Create a backend capability note for "single-stack ingestion" vs "multi-stack composition" trade-offs.
