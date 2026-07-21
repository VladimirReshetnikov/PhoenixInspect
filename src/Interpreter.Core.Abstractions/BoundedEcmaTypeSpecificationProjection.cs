using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

internal readonly record struct BoundedEcmaTypeSpecification(
    int GenericHeadMetadataToken,
    int GenericArgumentCount,
    int AggregateGenericArgumentCount,
    int MaximumObservedDepth,
    ImmutableArray<int> ReferencedTypeMetadataTokens);

/// <summary>
/// Performs bounded structural validation of one canonical ECMA-335 TypeSpec GENERICINST CLASS signature.
/// </summary>
/// <remarks>
/// The caller owns the public operation caps and checks the byte length before any copy. This shared reader covers the
/// complete encoded type grammar needed inside generic arguments, including arrays, custom modifiers, pointers,
/// function pointers, byrefs in method-signature positions, nested generic instances, VAR/MVAR, and primitive types.
/// Named CLASS/VALUETYPE nodes and GENERICINST heads admit only TypeDef/TypeRef coded indices. TypeSpec coded indices
/// are retained only in custom-modifier slots, where the runtime grammar permits them. The higher W8 metadata resolver
/// owns TypeSpec indirection resolution and cycle detection; this byte-only reader deliberately performs neither.
/// </remarks>
internal static class BoundedEcmaTypeSpecificationProjection
{
    internal static bool TryDecodeGenericClass(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateGenericArgumentCount,
        out BoundedEcmaTypeSpecification projection)
        => BoundedEcmaSignatureProjection.TryDecodeGenericClassTypeSpecification(
            signature,
            maximumSignatureLength,
            maximumDepth,
            maximumAggregateGenericArgumentCount,
            out projection);
}
