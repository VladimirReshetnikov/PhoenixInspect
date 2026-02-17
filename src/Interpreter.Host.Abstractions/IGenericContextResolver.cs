using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Optional host-specific generic context resolver.
/// </summary>
public interface IGenericContextResolver
{
    /// <summary>
    /// Tries to resolve generic context for a runtime method and optional receiver.
    /// </summary>
    /// <param name="runtimeMethod">Runtime method identity.</param>
    /// <param name="thisObj">Optional runtime receiver object.</param>
    /// <param name="ctx">Resolved generic context when available.</param>
    /// <returns><see langword="true"/> when context resolution succeeds; otherwise <see langword="false"/>.</returns>
    bool TryResolveGenericContext(RuntimeMethodId runtimeMethod, ExternalObjectRef? thisObj, out GenericContext ctx);
}
