using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Provides best-available debug maps across symbol and decompiler backends.
/// </summary>
public interface IDebugMapProvider
{
    /// <summary>
    /// Gets the best available debug map for a method in a module.
    /// </summary>
    /// <param name="module">Module providing metadata context.</param>
    /// <param name="method">Target method handle.</param>
    /// <returns>A debug map from PDB, decompiler, or synthetic IL fallback.</returns>
    IDebugMap GetBestMap(IMetadataModule module, MethodHandle method);
}
