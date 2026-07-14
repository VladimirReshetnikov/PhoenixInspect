using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Classifies the calling-convention families that W3 must preserve for explicit admission decisions.
/// </summary>
public enum MethodCallingConventionKind
{
    /// <summary>The ordinary managed default calling convention.</summary>
    Default,

    /// <summary>The managed variable-argument calling convention, which W3 preserves and rejects.</summary>
    VarArgs,
}

/// <summary>
/// Freezes the metadata-derived activation shape of one method definition.
/// </summary>
/// <remarks>
/// Ordered explicit parameters exclude an implicit receiver. Locals come from the body's StandAloneSig and are
/// represented here atomically with the method body by <see cref="ResolvedMethodDefinition"/>. Unsupported calling
/// convention facts are retained rather than discarded so whole-body admission can reject them explicitly.
/// </remarks>
public sealed class MethodSignatureShape : IEquatable<MethodSignatureShape>
{
    /// <summary>Creates an immutable metadata-derived method and local signature shape.</summary>
    /// <param name="declaringType">The exact metadata TypeDef that declares the method.</param>
    /// <param name="callingConvention">The decoded managed calling-convention family.</param>
    /// <param name="hasImplicitThis">Whether activation prepends an implicit receiver to explicit parameters.</param>
    /// <param name="hasExplicitThis">Whether metadata uses the unsupported explicit-this convention.</param>
    /// <param name="genericParameterCount">The decoded generic arity, retained so W3 can reject nonzero values.</param>
    /// <param name="parameterTypes">Ordered explicit parameter types, excluding any receiver.</param>
    /// <param name="returnType">The exact return type, including explicit <see cref="TypeSig.Void"/>.</param>
    /// <param name="localTypes">Ordered local types decoded from the body's StandAloneSig.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="declaringType"/> or <paramref name="returnType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The declaring type is not an exact TypeDef, either immutable vector is default or contains a null element,
    /// or both implicit-this and explicit-this are set.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="callingConvention"/> is undefined or <paramref name="genericParameterCount"/> is negative.
    /// </exception>
    public MethodSignatureShape(
        TypeSig declaringType,
        MethodCallingConventionKind callingConvention,
        bool hasImplicitThis,
        bool hasExplicitThis,
        int genericParameterCount,
        ImmutableArray<TypeSig> parameterTypes,
        TypeSig returnType,
        ImmutableArray<TypeSig> localTypes)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(returnType);
        if (!declaringType.IsMetadataTypeDefinition)
        {
            throw new ArgumentException(
                "A resolved method declaring type must carry an exact TypeDef identity.",
                nameof(declaringType));
        }

        if (!Enum.IsDefined(callingConvention))
        {
            throw new ArgumentOutOfRangeException(nameof(callingConvention));
        }

        if (hasImplicitThis && hasExplicitThis)
        {
            throw new ArgumentException(
                "Implicit-this and explicit-this are mutually exclusive signature facts.",
                nameof(hasExplicitThis));
        }

        if (genericParameterCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(genericParameterCount),
                "Generic parameter count cannot be negative.");
        }

        ValidateTypes(parameterTypes, nameof(parameterTypes), allowVoid: false);
        ValidateTypes(localTypes, nameof(localTypes), allowVoid: false);

        DeclaringType = declaringType;
        CallingConvention = callingConvention;
        HasImplicitThis = hasImplicitThis;
        HasExplicitThis = hasExplicitThis;
        GenericParameterCount = genericParameterCount;
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
        LocalTypes = localTypes;
    }

    /// <summary>Gets the exact TypeDef that declares the method.</summary>
    public TypeSig DeclaringType { get; }

    /// <summary>Gets the decoded managed calling-convention family.</summary>
    public MethodCallingConventionKind CallingConvention { get; }

    /// <summary>Gets a value indicating whether activation requires an implicit receiver at argument slot zero.</summary>
    public bool HasImplicitThis { get; }

    /// <summary>Gets a value indicating whether the unsupported explicit-this convention was decoded.</summary>
    public bool HasExplicitThis { get; }

    /// <summary>Gets the decoded generic arity retained for admission.</summary>
    public int GenericParameterCount { get; }

    /// <summary>Gets ordered explicit parameter types, excluding any implicit receiver.</summary>
    public ImmutableArray<TypeSig> ParameterTypes { get; }

    /// <summary>Gets the exact return type, including explicit <see cref="TypeSig.Void"/>.</summary>
    public TypeSig ReturnType { get; }

    /// <summary>Gets ordered local types decoded from the body's StandAloneSig.</summary>
    public ImmutableArray<TypeSig> LocalTypes { get; }

    /// <inheritdoc />
    public bool Equals(MethodSignatureShape? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        DeclaringType == other.DeclaringType &&
        CallingConvention == other.CallingConvention &&
        HasImplicitThis == other.HasImplicitThis &&
        HasExplicitThis == other.HasExplicitThis &&
        GenericParameterCount == other.GenericParameterCount &&
        ParameterTypes.SequenceEqual(other.ParameterTypes) &&
        ReturnType == other.ReturnType &&
        LocalTypes.SequenceEqual(other.LocalTypes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MethodSignatureShape);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = DeclaringType.GetHashCode();
        hash = unchecked((hash * 397) ^ (int)CallingConvention);
        hash = unchecked((hash * 397) ^ (HasImplicitThis ? 1 : 0));
        hash = unchecked((hash * 397) ^ (HasExplicitThis ? 1 : 0));
        hash = unchecked((hash * 397) ^ GenericParameterCount);
        foreach (var parameter in ParameterTypes)
        {
            hash = unchecked((hash * 397) ^ parameter.GetHashCode());
        }

        hash = unchecked((hash * 397) ^ ReturnType.GetHashCode());
        foreach (var local in LocalTypes)
        {
            hash = unchecked((hash * 397) ^ local.GetHashCode());
        }

        return hash;
    }

    private static void ValidateTypes(ImmutableArray<TypeSig> types, string parameterName, bool allowVoid)
    {
        if (types.IsDefault)
        {
            throw new ArgumentException("A resolved signature type vector cannot be default.", parameterName);
        }

        foreach (var type in types)
        {
            if (type is null)
            {
                throw new ArgumentException("A resolved signature type vector cannot contain null.", parameterName);
            }

            if (!allowVoid && type.Kind == TypeSigKind.Void)
            {
                throw new ArgumentException("Parameters and locals cannot have the void type.", parameterName);
            }
        }
    }
}

/// <summary>
/// Atomically binds one resolved MethodDef to its immutable body and metadata-derived activation shape.
/// </summary>
/// <remarks>
/// A resolver returns this value in one operation so execution cannot combine body bytes from one observation with
/// signature or local-shape evidence from another observation.
/// </remarks>
public sealed record ResolvedMethodDefinition
{
    /// <summary>Creates an atomic resolved method definition.</summary>
    /// <param name="method">The exact module-and-MethodDef identity.</param>
    /// <param name="body">The immutable IL body and header facts.</param>
    /// <param name="signature">The declaring type, calling convention, parameter, return, and local shape.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="body"/> or <paramref name="signature"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The method and declaring type belong to different modules, or body bytes are a default immutable array.
    /// </exception>
    public ResolvedMethodDefinition(
        MethodHandle method,
        MethodBody body,
        MethodSignatureShape signature)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(signature);
        if (method.Module != signature.DeclaringType.Module)
        {
            throw new ArgumentException(
                "A resolved method and its declaring TypeDef must belong to the same module.",
                nameof(method));
        }

        if (body.CodeBytes.IsDefault)
        {
            throw new ArgumentException("Resolved method body bytes cannot be a default immutable array.", nameof(body));
        }

        Method = method;
        Body = body;
        Signature = signature;
    }

    /// <summary>Gets the exact module-and-MethodDef identity.</summary>
    public MethodHandle Method { get; }

    /// <summary>Gets the immutable body and preserved method-header facts.</summary>
    public MethodBody Body { get; }

    /// <summary>Gets the metadata-derived activation and local shape observed with the body.</summary>
    public MethodSignatureShape Signature { get; }
}
