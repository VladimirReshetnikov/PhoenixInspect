using Interpreter.Core.Abstractions;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Defines the bounded metadata-module capability exercised by the W1 body-acquisition and W3 execution slices.
/// </summary>
/// <remarks>
/// Method activation uses one atomic definition projection so a body cannot be combined with a signature observed
/// at a different point in time. Field resolution is contextual to the containing method and deliberately accepts
/// only the closed same-module FieldDef profile required by W3. The body-only member remains available to W1 dump
/// evidence callers that compare independently acquired method bytes and do not activate the interpreter.
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
    /// The atomic method definition, or a structured unavailable, unsupported, invalid, or conflict result.
    /// </returns>
    ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method);

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
