using Interpreter.Core.Abstractions;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Defines the narrow metadata-module capability exercised by current executable prototype slices.
/// </summary>
/// <remarks>
/// The contract intentionally stops at deterministic MethodDef identity and body acquisition. Type, field,
/// signature, and dispatch services belong in later contract-just-ahead increments backed by executable fixtures.
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

    /// <summary>Retrieves a method body suitable for draft interpreter execution.</summary>
    /// <param name="method">The deterministic method-definition handle.</param>
    /// <returns>The method body or a structured unavailable/invalid result.</returns>
    ResolutionResult<MethodBody> GetMethodBody(MethodHandle method);
}
