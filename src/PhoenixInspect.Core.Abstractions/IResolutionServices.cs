namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Resolves atomic method definitions and contextual method/field operands required by the active execution profiles.
/// </summary>
/// <remarks>
/// A machine snapshots the first result observed for each definition, method operand, and field request. Implementations must therefore
/// preserve unavailable, unsupported, invalid, and conflicting evidence as structured results rather than throwing
/// target-derived exception text across the VM boundary.
/// </remarks>
public interface IResolutionServices
{
    /// <summary>Resolves an immutable method body and metadata-derived activation shape atomically.</summary>
    /// <param name="method">The deterministic method-definition handle.</param>
    /// <returns>
    /// The complete ordinary managed-IL method definition or a structured unavailable, unsupported, invalid, or
    /// conflict result. Supplying body bytes does not make P/Invoke, abstract, runtime-provided, forwarded,
    /// synchronized, native, or unmanaged implementations executable by this contract.
    /// Implementations should be stable for one analysis snapshot; the machine additionally caches the first
    /// observed result defensively.
    /// </returns>
    ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method);

    /// <summary>Resolves an InlineMethod operand without acquiring the target method body.</summary>
    /// <param name="contextMethod">
    /// The MethodDef whose module and deliberately closed generic context govern token interpretation.
    /// </param>
    /// <param name="metadataToken">The raw four-byte InlineMethod metadata token.</param>
    /// <returns>
    /// An exact same-module managed-IL MethodDef target with its body-independent call signature, or a structured
    /// unavailable, unsupported, invalid, or conflict result. The active direct-call profile classifies in-range
    /// MemberRef and MethodSpec tokens as unsupported, malformed or out-of-range tokens as invalid, and rejects
    /// cross-module, generic, instance, and name-based resolution.
    /// </returns>
    /// <remarks>
    /// Implementations must not acquire an RVA, method body, local signature, or local types in this operation. The
    /// default result preserves source compatibility for W3-only resolvers while requiring production W4 resolvers to
    /// implement the capability explicitly.
    /// </remarks>
    ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(MethodHandle contextMethod, int metadataToken) =>
        ResolutionResult<ResolvedMethodCallTarget>.Failed(
            ResolutionFailureKind.Unsupported,
            "RESOLUTION_DIRECT_METHOD_UNSUPPORTED",
            "This resolver does not provide body-independent direct MethodDef resolution.");

    /// <summary>Resolves an InlineField operand in the context of its containing method definition.</summary>
    /// <param name="contextMethod">
    /// The method whose module and, in later profiles, generic context govern token interpretation.
    /// </param>
    /// <param name="metadataToken">The raw four-byte InlineField metadata token.</param>
    /// <returns>
    /// A frozen structural field descriptor or a structured unavailable, unsupported, invalid, or conflict result.
    /// W3 accepts only same-module FieldDef tokens; implementations must reject MemberRef and other token kinds.
    /// </returns>
    ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken);
}
