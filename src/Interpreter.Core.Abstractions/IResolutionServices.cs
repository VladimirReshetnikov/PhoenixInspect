using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Provides VM-facing token resolution and method-body lookup services.
/// </summary>
public interface IResolutionServices
{
    /// <summary>Resolves a metadata type token in module context.</summary>
    ResolvedType ResolveType(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Resolves a metadata field token in module context.</summary>
    ResolvedField ResolveField(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Resolves a metadata method token in module context.</summary>
    ResolvedMethod ResolveMethod(ModuleHandle module, int metadataToken, GenericContext ctx);

    /// <summary>Tries to retrieve a method body for interpretation.</summary>
    bool TryGetMethodBody(MethodHandle method, out MethodBody body);

    /// <summary>Resolves virtual/interface dispatch for a runtime receiver type.</summary>
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}
