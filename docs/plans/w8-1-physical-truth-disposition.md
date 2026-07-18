# W8.1 Physical-Truth Disposition

> **Lifecycle:** Complete · **Roadmap:** Active evidence record
>
> **Decision:** W8.1 is implemented and locally validated. Its compiler/PDB, constructed-runtime, selected-frame,
> storage, literal, and assignability evidence freezes the branches that W8.2 contracts must expose or omit.
>
> **Product boundary:** this is pre-contract physical evidence, not a V2 binder or evaluator.
> `StaticFieldExpressionV2`, `BindingContextV2`, and `FrameValueExpressionV1` product contracts begin in W8.2.

## 1) Authority and status

This document is the authoritative disposition produced by W8.1 of the
[Post-W7 Path Forward](post-w7-path-forward.md). It replaces every earlier conditional statement about whether the
pinned compiler, Portable PDB, .NET 10 runtime, or selected-frame surfaces can provide the physical facts required by
W8. No later contract may promote a non-admitted branch, omit an admitted branch, or weaken an exact identity rule
without a new executable evidence checkpoint and an explicit plan revision.

W8 remains the sole active delivery sequence. W8.1 is complete; W8.2 is the active checkpoint. W1–W7 public profiles,
canonical artifacts, and evidence remain unchanged.

## 2) Evidence boundary

W8.1 uses dedicated emitted artifacts, independently decoded PE/Portable-PDB metadata, full Windows dumps, counted
runtime structures, copied raw bytes, and late compiler/runtime comparisons. It introduces no product binder,
expression profile implementation, public storage service, report schema, portfolio result, or representative
observation.

The evidence proves only the named generated artifacts and the pinned .NET 10 runtime shapes. Exact results remain
conditional on the same content, token, module, loader, thread, frame, scope, address, width, and topology identities
proved by the fixtures. Display names, enumeration order, first matches, and caller assertions are not identity.

## 3) Checkpoint ledger

| Checkpoint | Evidence added | Focused result |
|---|---|---|
| `942e4e561` | Dedicated W8 target and alias target; initial generic owners, statics, literals, imports, and frame profiles | Strict target build passed |
| `9a8f1c9d1` | Expanded definition-kind, primitive, thread/context/RVA, lexical, forwarding, ambiguity, and collectible-construction fixture | All ten hidden readiness profiles passed |
| `fdc554628` | Independent compiler/PE/Portable-PDB oracle, Roslyn semantic comparison, bounded TypeSpec traversal, altered artifact matrix | `W8CompilerPhysicalTruthTests` 25/25; strict build 0 warnings/errors |
| `2584cd4d2` | Descriptor-driven available-type traversal, exact candidate-keyed closed arguments, nested/array topology, collectible duplicate, construction-specific slots | `W8ConstructedRuntimeIdentityTests` 1/1; strict build 0 warnings/errors |
| `391675fef` | Six selected-frame profiles, detached contexts, exact root homes/types/widths/values, generic-context non-admission | `W8FramePhysicalTruthTests` 1/1 over six full dumps; strict build 0 warnings/errors |
| `220be94b49665f8950775a9ac924963fc6de0ab3` | Primitive/reference geometry, thread-relative and RVA storage, literal no-call evidence, context non-admission, bounded assignability differential | Storage/literal/bounds/assignability 8/8; strict build 0 warnings/errors |

Every managed invocation used the repository headless wrapper. Targets and helpers were hidden and windowless.

## 4) Compiler and Portable-PDB disposition

The compiler/PDB gate is exact for the physical inputs required by the mandatory V2 binder:

- nested TypeDef names, segment-local and flattened generic arity, GenericParam ownership/order, recursive base and
  interface TypeSpecs, complete field signatures, class/value-type/interface definition flags, and exact constants;
- TypeDef-, TypeRef-, and fully ground TypeSpec-backed aliases, including generic, SZ-array, multidimensional-array,
  constructed `using static`, imported nested-type, extern-alias, AssemblyRef, and forwarding facts;
- nested ImportScope and LocalScope chains, inner alias shadowing, same-level convergence and ambiguity inputs,
  method debug identity, parameters, active locals/constants, and lexically inactive locals; and
- constraints, declared accessibility, friend-assembly identity, literal encodings, and named four/eight-byte FieldRVA
  rows with mapped PE geometry.

Raw TypeSpec traversal is cumulatively bounded by bytes, depth, nodes, token rows, and a visited set before downstream
materialization. Cap-plus-one, cycles, malformed signatures, truncated PDB bytes, changed assembly identity, changed
imports, and changed RVA payloads are independent altered artifacts; unrelated facts remain exact.

The pinned compiler does not emit the proposed named-local slot-reuse relation: its reuse path is limited to unnamed
storage and the named case remains explicitly unavailable. Active/inactive source scopes are nevertheless exact and
are sufficient for the admitted active-local frame roots below.

## 5) Constructed-runtime disposition

The .NET 10 contract descriptor and available-type tables provide an exact candidate-keyed source for ordered closed
type arguments. The gate reads the exported descriptor, bounded JSON and pointer-data layouts, module available-type
buckets/chains, MethodTable and TypeDesc shapes, PerInstInfo, and dictionary entries without parsing runtime display
names or performing global name lookup.

Exact construction identity retains definition module/token, loader module, assembly, loader allocator, load-context
address, enclosing construction, ordered recursive arguments, array element/rank/SZ topology, runtime type handle, and
selected static slot. Class, value-type, interface, nested, vector, multidimensional-array, cross-assembly, default-load,
and collectible duplicate constructions are exact. The default and collectible alias assemblies intentionally share
one MVID while remaining distinct module, assembly, allocator, load-context, construction, slot, and value identities.

`ConstructedSlot` is therefore an admitted, mandatory V2 strategy. TypeDef plus FieldDef alone is never a construction
or storage identity.

## 6) Storage and value dispositions

| Physical branch | Disposition | Frozen consequence |
|---|---|---|
| `ConstructedSlot` | Admitted | Require one exact runtime construction, domain, declaration, slot, read geometry, and raw value. |
| `MetadataLiteral` | Admitted | Decode exact primitive, enum-underlying, floating-bit, string, null, and pinned decimal encodings from metadata. Runtime construction, storage acquisition, and value-memory calls are all zero and independently intercepted. |
| `ThreadRelativeSlot` | Admitted | Require exact selected-thread identity in addition to exact owner construction. Two workers by two closed owners prove four distinct slots and raw values with close/reopen replay. |
| `ModuleRva` | Admitted | Require exact module-content identity, MVID, FieldRVA row, mapped RVA/address geometry, and counted raw read. Runtime construction and slot acquisition are `NotRequired`. |
| Context-relative storage | Non-admitted | The emitted marker resolves through the exact framework AssemblyRef/forwarder chain, but the runtime exposes one ordinary static slot and no attributable context identity. Retain `W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE`; expose no `ContextRelativeSlot`. |

Every address-backed admitted value is decoded from copied dump bytes. High-level runtime reads are late comparisons
only. The value-geometry fixture covers every admitted fixed-width primitive and signedness, target-width native
integer, floating representation, enum underlying value, nullable form, string, exact null, reference, and array or
constructed-reference topology needed by W8.2.

## 7) Selected-frame dispositions

`FrameValueExpressionV1` is admitted and mandatory. Across generic-type, generic-method, optimized, disjoint-local,
lexical, and query profiles, W8.1 proves exact selected thread/frame/module/MethodDef/instruction identity, native-to-IL
map, detached raw context bytes, active PDB scopes, declared root type, liveness, memory address, payload width, copied
bytes, and decoded value for:

- `this` where the selected method has a receiver;
- reference and value parameters; and
- every active named local, while inactive same-method locals remain explicitly lexically inactive.

The admitted homes are exact memory locations. Register homes are not proven and do not enter W8.2 contracts.
`FrameValueExpressionV1` remains a separate profile accepting `this` or one active identifier plus an unchanged W2/W6
suffix. A missing, duplicate, inactive, partial, or unsupported frame root never falls through to static binding.

Selected-frame declaring-type and method generic arguments are non-admitted. The legacy generic enumeration surface
returns `E_NOTIMPL`; exact hidden token locations do not themselves identify closed arguments; descriptor traversal
retains exact `System.__Canon` when shared code has canonicalized the declaring argument; and
`CLRDataCreateInstance` declines `IDacDbiInterface` with `E_NOINTERFACE`. W8.2 therefore exposes no frame `VAR`/`MVAR`
substitution service, profile member, or placeholder. Fully ground expression and alias TypeSpecs remain unaffected.

## 8) Constructed assignability and array topology

The exact assignability differential contains 21 rows covering invariant generic arguments, covariance,
contravariance, emitted interface/base edges, reversed directions, value-type arguments, vector and matrix covariance,
rank mismatch, SZ-versus-rank-one-multidimensional mismatch, and value-element covariance rejection. Twenty rows agree
between the exact W8 rule and the pinned runtime; every source-expressible row is also compared with Roslyn.

The one deliberate divergence is retained: the runtime reports a positive result when the target is a rank-one
multidimensional array and the source is an SZ array with a compatible reference element. The reverse direction is
negative, and the rank-one multidimensional form has no C# source spelling. W8 does not inherit that asymmetry.
Array assignability requires equal rank and equal SZ-versus-multidimensional topology before recursive reference-element
assignability; value elements never gain covariance.

## 9) Mandatory W8.2 API consequences

W8.2 must freeze additive immutable contracts for:

1. `StaticFieldExpressionV2` and `BindingContextV2`;
2. structured closed type syntax, exact metadata construction, TypeSpec bytes, substitution, constraints,
   accessibility, member lookup, and constructed assignability;
3. exact runtime construction identity and the public storage discriminator containing only `ConstructedSlot`,
   `ThreadRelativeSlot`, `ModuleRva`, and `MetadataLiteral` for static fields;
4. separate `FrameValueExpressionV1` root, exact memory `FrameLocation`, plan, result, and provenance contracts;
5. strategy-tagged operation requirements and `NotRequired` facts, including literal and RVA no-call behavior; and
6. independent context, root, construction, member, runtime, storage, value, suffix, and completeness axes.

W8.2 must not expose `ContextRelativeSlot`, a register-home descriptor, selected-frame generic substitution, or a
placeholder capability for any non-admitted branch. The general V2 constructed-owner path still uses exact ordered
runtime arguments because that source was proven independently of selected-frame generic recovery.

## 10) Meaningful synthetic portfolio consequence

The fixed core remains thirty-two independent full-dump incidents over request, batch, coordinator, and workflow
shapes. The predeclared one-success-incident-per-admitted-branch rule now adds exactly three required incidents:

1. one `ThreadRelativeSlot` incident with exact selected-thread identity;
2. one `ModuleRva` incident with construction and slot marked `NotRequired`; and
3. one `FrameValueExpressionV1` incident with an exact memory-homed root and no static fallback.

The minimum W8 portfolio is therefore thirty-five independent incidents. Generated conformance still covers `this`,
reference/value parameters, active locals, every admitted storage/value form, and all typed non-admissions. Context-
relative and frame-generic branches add no fictitious success incident.

## 11) Validation and replay obligations

W8.1 validates exact-cap and cap-plus-one behavior, independent altered artifacts, same-session reconstruction, full
dump close/reopen/rebind, copied raw-byte decoding, canonical line equality, and capability-call accounting. The
focused checkpoint results are 25/25 compiler/PDB cases, 1/1 constructed-runtime case, one six-profile frame case, and
8/8 storage/literal/bounds/assignability cases, with strict Release builds at zero warnings and errors.

W8.2 and later checkpoints consume these dispositions; they do not rerun physical attribution while binding or
evaluating a frozen plan. Every admitted product path must retain the exact evidence it consulted, and every
non-admitted branch must retain its executable stop with no later capability call.

## 12) Pinned scope and next checkpoint

This disposition is pinned to the repository's generated artifacts, compiler profile, Windows full-dump mechanism,
and .NET 10 runtime/DAC surface. It establishes architectural feasibility and exact branch boundaries, not general
artifact coverage, production recoverability, product readiness, or hosted closure. Representative observation counts
remain zero.

W8.2 is next. It must implement the immutable contract families dictated by section 9, preserve every W1–W7 canonical
artifact, and leave the two non-admitted families absent rather than postponing them behind dormant API shapes.
