using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Provides module lookup across all metadata modules participating in a session.
/// </summary>
public interface IMetadataUniverse
{
    /// <summary>
    /// Tries to retrieve a metadata module by identity.
    /// </summary>
    /// <param name="id">Requested module identity.</param>
    /// <param name="module">Resolved module when available.</param>
    /// <returns><see langword="true"/> when module lookup succeeds; otherwise <see langword="false"/>.</returns>
    bool TryGetModule(ModuleId id, out IMetadataModule module);
}
