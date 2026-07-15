using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;

namespace Interpreter.Metadata.SRM;

/// <summary>
/// Adapts one <see cref="IMetadataModule"/> to the VM-facing atomic definition and contextual operand contract.
/// </summary>
public sealed class MetadataResolutionServices : IResolutionServices
{
    private readonly IMetadataModule _module;

    /// <summary>
    /// Creates a resolver for one content-identified metadata module.
    /// </summary>
    /// <param name="module">The metadata module that owns all accepted method handles.</param>
    public MetadataResolutionServices(IMetadataModule module)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
        _module.GetMethodDefinition(method);

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
        MethodHandle contextMethod,
        int metadataToken) =>
        _module.ResolveMethod(contextMethod, metadataToken);

    /// <inheritdoc />
    public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
        _module.ResolveField(contextMethod, metadataToken);
}
