using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Metadata.SRM;

/// <summary>
/// Adapts an <see cref="IMetadataModule"/> to the VM-facing <see cref="IResolutionServices"/> contract.
/// </summary>
public sealed class MetadataResolutionServices : IResolutionServices
{
    private readonly IMetadataModule _module;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataResolutionServices"/> class.
    /// </summary>
    /// <param name="module">Metadata module used to satisfy resolution requests.</param>
    public MetadataResolutionServices(IMetadataModule module)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
    }

    /// <inheritdoc />
    public ResolvedType ResolveType(ModuleHandle module, int metadataToken, GenericContext ctx) =>
        _module.ResolveTypeToken(metadataToken, ctx);

    /// <inheritdoc />
    public ResolvedField ResolveField(ModuleHandle module, int metadataToken, GenericContext ctx) =>
        _module.ResolveFieldToken(metadataToken, ctx);

    /// <inheritdoc />
    public ResolvedMethod ResolveMethod(ModuleHandle module, int metadataToken, GenericContext ctx) =>
        _module.ResolveMethodToken(metadataToken, ctx);

    /// <inheritdoc />
    public bool TryGetMethodBody(MethodHandle method, out MethodBody body) => _module.TryGetMethodBody(method, out body);

    /// <inheritdoc />
    public MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType) =>
        _module.ResolveVirtualOverride(declared, runtimeType);
}
