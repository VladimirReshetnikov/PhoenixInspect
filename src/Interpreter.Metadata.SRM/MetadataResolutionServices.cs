using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;

namespace Interpreter.Metadata.SRM;

/// <summary>
/// Adapts one <see cref="IMetadataModule"/> to the VM-facing method-body lookup contract.
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
    public ResolutionResult<MethodBody> GetMethodBody(MethodHandle method) => _module.GetMethodBody(method);
}
