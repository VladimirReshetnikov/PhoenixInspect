using PhoenixInspect.Core.Abstractions;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Metadata.Abstractions;

/// <summary>
/// Defines the bounded metadata-module capability exercised by the W1 body-acquisition and W3 execution slices.
/// </summary>
/// <remarks>
/// Method activation uses one atomic definition projection so a body cannot be combined with a signature observed
/// at a different point in time. Method-operand resolution is separately body-free so a caller can select an opaque
/// model disposition before acquiring a prospective callee body. Field resolution is contextual to the containing
/// method and deliberately accepts only the closed same-module FieldDef profile required by W3. The body-only member
/// remains available to W1 dump evidence callers that compare independently acquired method bytes.
/// </remarks>
public interface IMetadataModule
{
    /// <summary>Gets the canonical, path-independent module identity.</summary>
    ModuleId Id { get; }

    /// <summary>Gets non-identity display and artifact-location evidence for the module.</summary>
    ModuleDescriptor Descriptor { get; }

    /// <summary>Gets the corresponding deterministic execution-core handle.</summary>
    ModuleHandle ModuleHandle { get; }

    /// <summary>Resolves a MethodDef token into a deterministic definition handle.</summary>
    /// <param name="metadataToken">The MethodDef metadata token.</param>
    /// <returns>The method definition handle or a structured invalid result.</returns>
    ResolutionResult<MethodHandle> GetMethodHandle(int metadataToken);

    /// <summary>Resolves a complete MethodDef body and activation shape as one immutable observation.</summary>
    /// <param name="method">The deterministic same-module method-definition handle.</param>
    /// <returns>
    /// The atomic ordinary managed-IL method definition, or a structured unavailable, unsupported, invalid, or
    /// conflict result. Excluded CLR implementation kinds remain unsupported even when a caller can supply bytes.
    /// </returns>
    ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method);

    /// <summary>Resolves a same-module MethodDef call operand without acquiring its body or local signature.</summary>
    /// <param name="contextMethod">The MethodDef whose IL contains the InlineMethod operand.</param>
    /// <param name="metadataToken">The raw four-byte InlineMethod metadata token.</param>
    /// <returns>
    /// An exact managed-IL target and body-independent call signature, or a structured unsupported, invalid, or
    /// conflict result. In-range MemberRef and MethodSpec operands are unsupported without resolution; malformed,
    /// nil, out-of-range, and unrelated tokens are invalid. Cross-module, generic, instance, and name-based
    /// resolution are never promoted into a successful target.
    /// </returns>
    ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(MethodHandle contextMethod, int metadataToken);

    /// <summary>Resolves a same-module FieldDef operand in the context of a containing MethodDef.</summary>
    /// <param name="contextMethod">The method whose IL contains the field operand.</param>
    /// <param name="metadataToken">The raw four-byte InlineField metadata token.</param>
    /// <returns>
    /// A structural field descriptor, or a structured unsupported, invalid, or conflict result. The result never
    /// treats MemberRef or cross-module binding as an admitted W3 field.
    /// </returns>
    ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken);

    /// <summary>Retrieves a method body for W1 evidence comparison without projecting an activation shape.</summary>
    /// <param name="method">The deterministic method-definition handle.</param>
    /// <returns>The method body or a structured unavailable, unsupported, invalid, or conflict result.</returns>
    /// <remarks>
    /// Interpreter activation must use <see cref="GetMethodDefinition"/>. This compatibility operation exists for
    /// callers that independently validate dump-acquired body bytes and must not be used to seed a VM frame.
    /// </remarks>
    ResolutionResult<MethodBody> GetMethodBody(MethodHandle method);
}
