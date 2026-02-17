using Interpreter.Metadata.Abstractions;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Bridges host runtime identities to metadata abstraction identities.
/// </summary>
public interface IRuntimeMetadataBridge
{
    /// <summary>Tries to map a runtime method identity to metadata information.</summary>
    bool TryMapMethod(RuntimeMethodId runtimeMethod, out RuntimeMethodInfo info);

    /// <summary>Tries to map a runtime module identity to a metadata module identity.</summary>
    bool TryMapModule(RuntimeModuleId runtimeModule, out ModuleId module);
}
