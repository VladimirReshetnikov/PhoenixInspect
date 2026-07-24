namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Classifies the structural type identities understood by the W3 execution contract.
/// </summary>
public enum TypeSigKind
{
    /// <summary>The CLI <c>void</c> return type, which is not a value or stack type.</summary>
    Void,

    /// <summary>A runtime-defined intrinsic type identified by <see cref="IntrinsicTypeKind"/>.</summary>
    Intrinsic,

    /// <summary>An exact metadata-defined reference type identified by module and TypeDef token.</summary>
    TypeDefinition,

    /// <summary>A diagnostic-name identity admitted only by isolated fixtures.</summary>
    Synthetic,

    /// <summary>A zero-based, one-dimensional array whose element type is structural.</summary>
    SzArray,
}

/// <summary>
/// Identifies the small intrinsic-type vocabulary retained by the concrete validation domain.
/// </summary>
public enum IntrinsicTypeKind
{
    /// <summary>The CLI Boolean type represented in the I4 stack category.</summary>
    Boolean,

    /// <summary>The signed CLI 32-bit integer type.</summary>
    Int32,

    /// <summary>The signed CLI 64-bit integer type.</summary>
    Int64,

    /// <summary>The runtime intrinsic string reference type.</summary>
    String,

    /// <summary>The runtime intrinsic root object reference type.</summary>
    Object,
}

/// <summary>
/// Represents a structural CLI type identity for execution admission and semantic values.
/// </summary>
/// <remarks>
/// Metadata-defined types compare by content-derived module identity and non-nil TypeDef token. Their display
/// names are diagnostic evidence only and deliberately do not participate in equality or hashing. The public
/// string constructor creates a synthetic identity for isolated laws; such identities are not admissible
/// metadata identities and must never be used to join independently observed target types.
/// </remarks>
public sealed class TypeSig : IEquatable<TypeSig>
{
    private const int TokenTypeMask = unchecked((int)0xFF000000);
    private const int TypeDefinitionTokenType = 0x02000000;
    private const int RowIdMask = 0x00FFFFFF;

    /// <summary>Gets the maximum admitted diagnostic type-name length.</summary>
    public const int MaximumDisplayNameLength = 1024;

    /// <summary>Gets the canonical structural <c>void</c> return type.</summary>
    public static TypeSig Void { get; } = new(TypeSigKind.Void, null, null, 0, null, "System.Void");

    /// <summary>Gets the canonical structural CLI Boolean type.</summary>
    public static TypeSig Boolean { get; } = CreateIntrinsic(IntrinsicTypeKind.Boolean, "System.Boolean");

    /// <summary>Gets the canonical structural CLI signed 32-bit integer type.</summary>
    public static TypeSig Int32 { get; } = CreateIntrinsic(IntrinsicTypeKind.Int32, "System.Int32");

    /// <summary>Gets the canonical structural CLI signed 64-bit integer type.</summary>
    public static TypeSig Int64 { get; } = CreateIntrinsic(IntrinsicTypeKind.Int64, "System.Int64");

    /// <summary>Gets the canonical structural runtime string reference type.</summary>
    public static TypeSig String { get; } = CreateIntrinsic(IntrinsicTypeKind.String, "System.String");

    /// <summary>Gets the canonical structural runtime root object reference type.</summary>
    public static TypeSig Object { get; } = CreateIntrinsic(IntrinsicTypeKind.Object, "System.Object");

    /// <summary>
    /// Creates a bounded synthetic type identity for isolated fixtures.
    /// </summary>
    /// <param name="displayName">
    /// A deterministic fixture identity and diagnostic name. Unlike metadata type display names, it participates in
    /// equality because no stronger identity exists for a synthetic type.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="displayName"/> exceeds <see cref="MaximumDisplayNameLength"/> characters.
    /// </exception>
    /// <remarks>
    /// This constructor is intentionally retained for dump-free memory laws. Production metadata projection must
    /// use <see cref="CreateTypeDefinition"/> instead.
    /// </remarks>
    public TypeSig(string displayName)
        : this(TypeSigKind.Synthetic, null, null, 0, null, ValidateDisplayName(displayName))
    {
    }

    private TypeSig(
        TypeSigKind kind,
        IntrinsicTypeKind? intrinsicKind,
        ModuleHandle? module,
        int metadataToken,
        TypeSig? elementType,
        string displayName)
    {
        Kind = kind;
        IntrinsicKind = intrinsicKind;
        Module = module;
        MetadataToken = metadataToken;
        ElementType = elementType;
        DisplayName = displayName;
    }

    /// <summary>Gets the structural identity category.</summary>
    public TypeSigKind Kind { get; }

    /// <summary>Gets the intrinsic identity when <see cref="Kind"/> is <see cref="TypeSigKind.Intrinsic"/>.</summary>
    public IntrinsicTypeKind? IntrinsicKind { get; }

    /// <summary>Gets the defining module for a <see cref="TypeSigKind.TypeDefinition"/> identity.</summary>
    public ModuleHandle? Module { get; }

    /// <summary>Gets the non-nil TypeDef token for a <see cref="TypeSigKind.TypeDefinition"/> identity.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the structural element type for a <see cref="TypeSigKind.SzArray"/> identity.</summary>
    public TypeSig? ElementType { get; }

    /// <summary>
    /// Gets a bounded deterministic diagnostic name that is not the identity of metadata-defined types.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether this type carries an exact module-and-TypeDef metadata identity.
    /// </summary>
    public bool IsMetadataTypeDefinition => Kind == TypeSigKind.TypeDefinition;

    /// <summary>
    /// Creates an exact metadata-defined object-reference type.
    /// </summary>
    /// <param name="module">The content- or snapshot-derived defining module identity.</param>
    /// <param name="metadataToken">A non-nil TypeDef metadata token.</param>
    /// <param name="displayName">A bounded diagnostic name that does not participate in identity.</param>
    /// <returns>A structural type identified exclusively by <paramref name="module"/> and token.</returns>
    /// <exception cref="ArgumentException"><paramref name="module"/> is the default handle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metadataToken"/> is not a non-nil TypeDef token, or the display name is too long.
    /// </exception>
    public static TypeSig CreateTypeDefinition(ModuleHandle module, int metadataToken, string displayName)
    {
        if (module == default)
        {
            throw new ArgumentException("A metadata type requires a non-default module identity.", nameof(module));
        }

        if (!IsValidTypeDefinitionToken(metadataToken))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadataToken),
                "A metadata type requires a non-nil TypeDef metadata token.");
        }

        return new TypeSig(
            TypeSigKind.TypeDefinition,
            null,
            module,
            metadataToken,
            null,
            ValidateDisplayName(displayName));
    }

    /// <summary>
    /// Creates a structural zero-based, one-dimensional array type.
    /// </summary>
    /// <param name="elementType">The exact structural element type.</param>
    /// <returns>An array identity whose equality includes <paramref name="elementType"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="elementType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="elementType"/> is <see cref="Void"/>.</exception>
    public static TypeSig CreateSzArray(TypeSig elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType.Kind == TypeSigKind.Void)
        {
            throw new ArgumentException("An array element type cannot be void.", nameof(elementType));
        }

        var displayName = $"{elementType.DisplayName}[]";
        if (displayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elementType),
                $"Array type display names are limited to {MaximumDisplayNameLength} characters.");
        }

        return new TypeSig(TypeSigKind.SzArray, null, null, 0, elementType, displayName);
    }

    /// <summary>Determines whether a raw metadata token can identify a TypeDef.</summary>
    /// <param name="metadataToken">The raw ECMA-335 metadata token.</param>
    /// <returns><see langword="true"/> exactly for a TypeDef token with a nonzero row identifier.</returns>
    public static bool IsValidTypeDefinitionToken(int metadataToken) =>
        (metadataToken & TokenTypeMask) == TypeDefinitionTokenType &&
        (metadataToken & RowIdMask) != 0;

    /// <inheritdoc />
    public bool Equals(TypeSig? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            TypeSigKind.Void => true,
            TypeSigKind.Intrinsic => IntrinsicKind == other.IntrinsicKind,
            TypeSigKind.TypeDefinition => Module == other.Module && MetadataToken == other.MetadataToken,
            TypeSigKind.Synthetic => string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal),
            TypeSigKind.SzArray => ElementType == other.ElementType,
            _ => false,
        };
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as TypeSig);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = unchecked(((int)Kind + 1) * 486187739);
        return Kind switch
        {
            TypeSigKind.Void => hash,
            TypeSigKind.Intrinsic => unchecked((hash * 397) ^ (int)IntrinsicKind!),
            TypeSigKind.TypeDefinition => AddModuleAndToken(hash, Module!.Value, MetadataToken),
            TypeSigKind.Synthetic => AddOrdinalString(hash, DisplayName),
            TypeSigKind.SzArray => unchecked((hash * 397) ^ ElementType!.GetHashCode()),
            _ => hash,
        };
    }

    /// <summary>Compares two structural type identities.</summary>
    /// <param name="left">The first type, or <see langword="null"/>.</param>
    /// <param name="right">The second type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both operands carry equal structural identities.</returns>
    public static bool operator ==(TypeSig? left, TypeSig? right) => Equals(left, right);

    /// <summary>Compares two structural type identities for inequality.</summary>
    /// <param name="left">The first type, or <see langword="null"/>.</param>
    /// <param name="right">The second type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the operands carry different structural identities.</returns>
    public static bool operator !=(TypeSig? left, TypeSig? right) => !Equals(left, right);

    /// <summary>Returns the bounded diagnostic display name.</summary>
    /// <returns><see cref="DisplayName"/>; callers must not treat it as metadata identity.</returns>
    public override string ToString() => DisplayName;

    private static TypeSig CreateIntrinsic(IntrinsicTypeKind kind, string displayName) =>
        new(TypeSigKind.Intrinsic, kind, null, 0, null, displayName);

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A non-empty deterministic type display name is required.", nameof(displayName));
        }

        if (displayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayName),
                $"Type display names are limited to {MaximumDisplayNameLength} characters.");
        }

        return displayName;
    }

    private static int AddModuleAndToken(int hash, ModuleHandle module, int metadataToken)
    {
        hash = unchecked((hash * 397) ^ (int)module.High);
        hash = unchecked((hash * 397) ^ (int)(module.High >> 32));
        hash = unchecked((hash * 397) ^ (int)module.Low);
        hash = unchecked((hash * 397) ^ (int)(module.Low >> 32));
        return unchecked((hash * 397) ^ metadataToken);
    }

    private static int AddOrdinalString(int hash, string value)
    {
        foreach (var character in value)
        {
            hash = unchecked((hash * 397) ^ character);
        }

        return hash;
    }
}
