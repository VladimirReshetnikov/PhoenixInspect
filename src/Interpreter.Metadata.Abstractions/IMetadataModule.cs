using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Defines metadata operations for a single module.
/// </summary>
public interface IMetadataModule
{
    /// <summary>Gets the stable module identity.</summary>
    ModuleId Id { get; }

    /// <summary>Gets the corresponding core-layer module handle.</summary>
    ModuleHandle ModuleHandle { get; }

    /// <summary>Builds or retrieves a stable type handle from a metadata token and context.</summary>
    TypeHandle GetTypeHandle(int metadataToken, GenericContext ctx);

    /// <summary>Builds or retrieves a stable method handle from a metadata token and context.</summary>
    MethodHandle GetMethodHandle(int metadataToken, GenericContext ctx);

    /// <summary>Builds or retrieves a stable field handle from a metadata token and context.</summary>
    FieldHandle GetFieldHandle(int metadataToken, GenericContext ctx);

    /// <summary>Resolves a type signature from a type handle.</summary>
    TypeSig GetTypeSignature(TypeHandle type);

    /// <summary>Resolves a method signature from a method handle.</summary>
    MethodSig GetMethodSignature(MethodHandle method);

    /// <summary>Resolves a field signature from a field handle.</summary>
    FieldSig GetFieldSignature(FieldHandle field);

    /// <summary>Resolves a type token to the normalized core representation.</summary>
    ResolvedType ResolveTypeToken(int token, GenericContext ctx);

    /// <summary>Resolves a field token to the normalized core representation.</summary>
    ResolvedField ResolveFieldToken(int token, GenericContext ctx);

    /// <summary>Resolves a method token to the normalized core representation.</summary>
    ResolvedMethod ResolveMethodToken(int token, GenericContext ctx);

    /// <summary>Tries to retrieve a method body for interpretation.</summary>
    bool TryGetMethodBody(MethodHandle method, out MethodBody body);

    /// <summary>Resolves the runtime target method for virtual/interface dispatch.</summary>
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}
