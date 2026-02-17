# dnlib usage notes for this project

## Why it matters

dnlib remains an important reference backend even if it is not selected as default.

It is historically strong for:

- broad .NET metadata handling,
- practical IL read/write workflows,
- reverse-engineering-friendly object models,
- both portable and Windows PDB pathways.

## Snapshot review highlights

The `lib/dnlib/src` snapshot is centered around a single `dnlib.csproj` with layered namespaces:

- `DotNet` (high-level metadata definitions and module/type/member models),
- `DotNet/Emit` (CIL body parsing/emission utilities),
- `DotNet/MD` (metadata stream and table infrastructure),
- `DotNet/Pdb` (symbol reader selection and implementations),
- `PE`, `IO`, and `W32Resources` (binary substrate support).

This one-stack layout favors integration speed but increases the risk of backend-specific leakage if boundaries are weak.

## Source-level API surfaces relevant to our adapters

### 1) ModuleDefMD as metadata root

`ModuleDefMD` provides broad `Load(...)` entry points (file, bytes, stream, reflection module) and direct access to metadata streams.

Design implication:

- good fit for deep metadata probing in adapter spikes,
- keep stream/table details internal and project only normalized identity/body contracts.

### 2) MethodDef + CilBody object model

`MethodDef` and `CilBody` expose signatures, generic context, instruction lists, locals, and EH metadata.

Design implication:

- dnlib can satisfy our method-body projection contract with low ceremony,
- normalization layer must prevent dnlib-specific semantics from shaping core contracts.

### 3) MethodBodyReader controlled decode pipeline

`MethodBodyReader.CreateCilBody(...)` overloads accept operand resolvers, explicit EH readers, and generic parameter context.

Design implication:

- strong for malformed/partial-body experiments,
- ideal for building deterministic failure-classification tests mapped to our miss taxonomy.

### 4) SymbolReaderFactory policy routing

`DotNet/Pdb/SymbolReaderFactory` routes between:

- managed Windows PDB path,
- portable PDB reader path,
- COM-based Microsoft reader path (platform/options dependent),
- embedded portable PDB extraction.

Design implication:

- useful coverage breadth for mixed-symbol environments,
- requires a strict project policy to avoid backend-implicit symbol behavior differences.

### 5) Portable PDB + managed readers as separate implementations

`DotNet/Pdb/Portable/SymbolReaderFactory` and `DotNet/Pdb/Managed/SymbolReaderFactory` use distinct matching/validation rules.

Design implication:

- symbol projection should include source quality/provenance markers,
- mismatch and fallback outcomes should be normalized and testable across paths.

## Potential project applications

1. **Fallback metadata/CIL backend**
   - interchangeable behind the same adapter interfaces as primary backend candidates.
2. **Cross-check backend for parity testing**
   - validate assumptions made by AsmResolver or SRM-oriented projections.
3. **Failure-taxonomy training source**
   - exercise malformed metadata/IL scenarios through explicit reader controls.
4. **Symbol policy validation backend**
   - probe portable/embedded/windows symbol behavior under one stack.

## Boundary and architecture guidance

- Do not expose dnlib model types outside adapter internals.
- Keep dnlib capability optional until explicit decision gates are met.
- Map all reader and symbol failures to project-owned miss reasons.
- Preserve backend-neutral symbol and method-body contracts even when dnlib powers both.

## Risks and design pressure

1. **Stack proliferation overhead**
   - supporting dnlib in addition to AsmResolver/SRM can fragment effort.
2. **Contract drift risk**
   - rich backend-specific conveniences can leak into public abstractions.
3. **Maintenance multiplier**
   - each backend adds conformance and regression burden.
4. **Symbol policy ambiguity**
   - multiple reader modes can produce inconsistent host behavior if policy is implicit.
5. **Single-stack overconfidence**
   - convenience of one library can hide long-term portability and lock-in risk.

## Recommended next experiments

1. Build a dnlib adapter spike for method-body + generic context + sequence points.
2. Execute malformed IL fixtures through `MethodBodyReader` and classify outcomes.
3. Compare dnlib vs AsmResolver normalized projection outputs on generic-heavy samples.
4. Define a deterministic symbol-reader policy profile and evaluate path selection behavior.
5. Capture explicit parity cases for embedded portable PDB and Windows PDB fallback conditions.
