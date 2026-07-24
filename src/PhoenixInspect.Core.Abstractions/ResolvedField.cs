namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Freezes the structural identity and admission-relevant metadata of one resolved FieldDef.
/// </summary>
/// <remarks>
/// Execution plans carry this descriptor rather than a raw four-byte IL operand. The descriptor is immutable and
/// binds the owner, value type, and storage disposition observed during whole-body admission.
/// </remarks>
public sealed record ResolvedField
{
    /// <summary>Creates a structurally validated field descriptor.</summary>
    /// <param name="handle">The exact same-module FieldDef identity.</param>
    /// <param name="declaringType">The exact metadata TypeDef that directly declares the field.</param>
    /// <param name="fieldType">The field's structural value type.</param>
    /// <param name="isStatic">Whether the field uses static rather than instance storage.</param>
    /// <param name="isLiteral">Whether the field is a metadata literal.</param>
    /// <param name="hasRva">Whether the field uses RVA-backed storage.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="declaringType"/> or <paramref name="fieldType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The declaring type is not an exact TypeDef, its module differs from <paramref name="handle"/>, or the field
    /// type is <c>void</c>.
    /// </exception>
    public ResolvedField(
        FieldHandle handle,
        TypeSig declaringType,
        TypeSig fieldType,
        bool isStatic,
        bool isLiteral,
        bool hasRva)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(fieldType);
        if (!declaringType.IsMetadataTypeDefinition)
        {
            throw new ArgumentException(
                "A resolved field declaring type must carry an exact TypeDef identity.",
                nameof(declaringType));
        }

        if (declaringType.Module != handle.Module)
        {
            throw new ArgumentException(
                "A resolved field and its declaring TypeDef must belong to the same module.",
                nameof(handle));
        }

        if (fieldType.Kind == TypeSigKind.Void)
        {
            throw new ArgumentException("A field cannot have the void type.", nameof(fieldType));
        }

        Handle = handle;
        DeclaringType = declaringType;
        FieldType = fieldType;
        IsStatic = isStatic;
        IsLiteral = isLiteral;
        HasRva = hasRva;
    }

    /// <summary>Gets the exact module-and-FieldDef identity.</summary>
    public FieldHandle Handle { get; }

    /// <summary>Gets the exact TypeDef that directly declares the field.</summary>
    public TypeSig DeclaringType { get; }

    /// <summary>Gets the structural field value type.</summary>
    public TypeSig FieldType { get; }

    /// <summary>Gets a value indicating whether the field uses static storage.</summary>
    public bool IsStatic { get; }

    /// <summary>Gets a value indicating whether the field is a metadata literal.</summary>
    public bool IsLiteral { get; }

    /// <summary>Gets a value indicating whether the field uses RVA-backed storage.</summary>
    public bool HasRva { get; }
}
