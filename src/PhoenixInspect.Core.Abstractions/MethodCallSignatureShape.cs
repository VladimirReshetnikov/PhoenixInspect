using System.Collections.Immutable;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Freezes the body-independent metadata signature required to type one direct method call.
/// </summary>
/// <remarks>
/// Explicit parameters exclude an implicit receiver. Local-variable types deliberately do not belong to this value:
/// they are selected by a method body's StandAloneSig and must not be acquired while resolving an opaque call target.
/// Equality and hashing compare the ordered parameter contents so independently projected metadata images reproduce
/// the same signature identity even when their immutable arrays have different backing storage.
/// </remarks>
public sealed class MethodCallSignatureShape : IEquatable<MethodCallSignatureShape>
{
    /// <summary>Creates an immutable body-independent method-call signature.</summary>
    /// <param name="declaringType">The exact metadata TypeDef that declares the method.</param>
    /// <param name="callingConvention">The decoded managed calling-convention family.</param>
    /// <param name="hasImplicitThis">Whether invocation supplies an implicit receiver before explicit parameters.</param>
    /// <param name="hasExplicitThis">Whether metadata uses the explicit-this calling convention.</param>
    /// <param name="genericParameterCount">The decoded generic method arity.</param>
    /// <param name="parameterTypes">Ordered explicit parameter types, excluding any receiver.</param>
    /// <param name="returnType">The exact return type, including explicit <see cref="TypeSig.Void"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="declaringType"/> or <paramref name="returnType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The declaring type is not an exact TypeDef, the parameter vector is default or contains a null or void type,
    /// or both implicit-this and explicit-this are set.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="callingConvention"/> is undefined or <paramref name="genericParameterCount"/> is negative.
    /// </exception>
    public MethodCallSignatureShape(
        TypeSig declaringType,
        MethodCallingConventionKind callingConvention,
        bool hasImplicitThis,
        bool hasExplicitThis,
        int genericParameterCount,
        ImmutableArray<TypeSig> parameterTypes,
        TypeSig returnType)
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

        ValidateTypes(parameterTypes, nameof(parameterTypes));

        DeclaringType = declaringType;
        CallingConvention = callingConvention;
        HasImplicitThis = hasImplicitThis;
        HasExplicitThis = hasExplicitThis;
        GenericParameterCount = genericParameterCount;
        ParameterTypes = ImmutableArray.CreateRange(parameterTypes.AsSpan().ToArray());
        ReturnType = returnType;
    }

    /// <summary>Gets the exact TypeDef that declares the method.</summary>
    public TypeSig DeclaringType { get; }

    /// <summary>Gets the decoded managed calling-convention family.</summary>
    public MethodCallingConventionKind CallingConvention { get; }

    /// <summary>Gets a value indicating whether invocation supplies an implicit receiver.</summary>
    public bool HasImplicitThis { get; }

    /// <summary>Gets a value indicating whether metadata uses the explicit-this calling convention.</summary>
    public bool HasExplicitThis { get; }

    /// <summary>Gets the decoded generic method arity.</summary>
    public int GenericParameterCount { get; }

    /// <summary>Gets ordered explicit parameter types, excluding any implicit receiver.</summary>
    public ImmutableArray<TypeSig> ParameterTypes { get; }

    /// <summary>Gets the exact return type, including explicit <see cref="TypeSig.Void"/>.</summary>
    public TypeSig ReturnType { get; }

    /// <inheritdoc />
    public bool Equals(MethodCallSignatureShape? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        DeclaringType == other.DeclaringType &&
        CallingConvention == other.CallingConvention &&
        HasImplicitThis == other.HasImplicitThis &&
        HasExplicitThis == other.HasExplicitThis &&
        GenericParameterCount == other.GenericParameterCount &&
        ParameterTypes.SequenceEqual(other.ParameterTypes) &&
        ReturnType == other.ReturnType;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MethodCallSignatureShape);

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

        return unchecked((hash * 397) ^ ReturnType.GetHashCode());
    }

    /// <summary>Compares two body-independent call signatures for structural equality.</summary>
    /// <param name="left">The first signature, or <see langword="null"/>.</param>
    /// <param name="right">The second signature, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both values carry identical structural signature facts.</returns>
    public static bool operator ==(MethodCallSignatureShape? left, MethodCallSignatureShape? right) =>
        Equals(left, right);

    /// <summary>Compares two body-independent call signatures for structural inequality.</summary>
    /// <param name="left">The first signature, or <see langword="null"/>.</param>
    /// <param name="right">The second signature, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the values carry different structural signature facts.</returns>
    public static bool operator !=(MethodCallSignatureShape? left, MethodCallSignatureShape? right) =>
        !Equals(left, right);

    internal static void ValidateTypes(ImmutableArray<TypeSig> types, string parameterName)
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

            if (type.Kind == TypeSigKind.Void)
            {
                throw new ArgumentException("Parameters and locals cannot have the void type.", parameterName);
            }
        }
    }
}
