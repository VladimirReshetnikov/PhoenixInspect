namespace Interpreter.Core.Abstractions;

/// <summary>
/// Resolves the atomic method and contextual field descriptors required by the closed W3 execution profiles.
/// </summary>
/// <remarks>
/// A machine snapshots the first result observed for each method and field request. Implementations must therefore
/// preserve unavailable, unsupported, invalid, and conflicting evidence as structured results rather than throwing
/// target-derived exception text across the VM boundary.
/// </remarks>
public interface IResolutionServices
{
    /// <summary>Resolves an immutable method body and metadata-derived activation shape atomically.</summary>
    /// <param name="method">The deterministic method-definition handle.</param>
    /// <returns>
    /// The complete method definition or a structured unavailable, unsupported, invalid, or conflict result.
    /// Implementations should be stable for one analysis snapshot; the machine additionally caches the first
    /// observed result defensively.
    /// </returns>
    ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method);

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
