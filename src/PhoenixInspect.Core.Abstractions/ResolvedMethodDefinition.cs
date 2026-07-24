using System.Collections.Immutable;

namespace PhoenixInspect.Core.Abstractions;

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
        : this(
            new MethodCallSignatureShape(
                declaringType,
                callingConvention,
                hasImplicitThis,
                hasExplicitThis,
                genericParameterCount,
                parameterTypes,
                returnType),
            localTypes)
    {
    }

    /// <summary>Creates an immutable activation shape from a body-independent call signature and body locals.</summary>
    /// <param name="callSignature">The exact metadata-derived signature that does not depend on a method body.</param>
    /// <param name="localTypes">Ordered local types decoded from the body's StandAloneSig.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callSignature"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="localTypes"/> is default or contains a null or void type.
    /// </exception>
    public MethodSignatureShape(
        MethodCallSignatureShape callSignature,
        ImmutableArray<TypeSig> localTypes)
    {
        ArgumentNullException.ThrowIfNull(callSignature);
        MethodCallSignatureShape.ValidateTypes(localTypes, nameof(localTypes));

        CallSignature = callSignature;
        LocalTypes = localTypes;
    }

    /// <summary>Gets the body-independent declaring-type, calling-convention, parameter, and return signature.</summary>
    public MethodCallSignatureShape CallSignature { get; }

    /// <summary>Gets the exact TypeDef that declares the method.</summary>
    public TypeSig DeclaringType => CallSignature.DeclaringType;

    /// <summary>Gets the decoded managed calling-convention family.</summary>
    public MethodCallingConventionKind CallingConvention => CallSignature.CallingConvention;

    /// <summary>Gets a value indicating whether activation requires an implicit receiver at argument slot zero.</summary>
    public bool HasImplicitThis => CallSignature.HasImplicitThis;

    /// <summary>Gets a value indicating whether the unsupported explicit-this convention was decoded.</summary>
    public bool HasExplicitThis => CallSignature.HasExplicitThis;

    /// <summary>Gets the decoded generic arity retained for admission.</summary>
    public int GenericParameterCount => CallSignature.GenericParameterCount;

    /// <summary>Gets ordered explicit parameter types, excluding any implicit receiver.</summary>
    public ImmutableArray<TypeSig> ParameterTypes => CallSignature.ParameterTypes;

    /// <summary>Gets the exact return type, including explicit <see cref="TypeSig.Void"/>.</summary>
    public TypeSig ReturnType => CallSignature.ReturnType;

    /// <summary>Gets ordered local types decoded from the body's StandAloneSig.</summary>
    public ImmutableArray<TypeSig> LocalTypes { get; }

    /// <inheritdoc />
    public bool Equals(MethodSignatureShape? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        CallSignature == other.CallSignature &&
        LocalTypes.SequenceEqual(other.LocalTypes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MethodSignatureShape);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = CallSignature.GetHashCode();
        foreach (var local in LocalTypes)
        {
            hash = unchecked((hash * 397) ^ local.GetHashCode());
        }

        return hash;
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
