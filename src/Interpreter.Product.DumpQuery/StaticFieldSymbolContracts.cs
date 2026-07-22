using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies how one decoded type/field spelling was expanded before metadata lookup.</summary>
public enum StaticFieldNameExpansionKind
{
    /// <summary>The expression used literal <c>global::</c> qualification.</summary>
    GlobalQualified = 1,

    /// <summary>The dot-qualified spelling was searched directly without selected-frame import context.</summary>
    DotQualified = 2,

    /// <summary>The declaring namespace from exact selected-frame context supplied the qualification.</summary>
    CurrentNamespace = 3,

    /// <summary>One exact active namespace import supplied the qualification.</summary>
    NamespaceImport = 4,

    /// <summary>One exact active type alias supplied the complete type name.</summary>
    TypeAlias = 5,

    /// <summary>One exact active namespace alias supplied the namespace prefix.</summary>
    NamespaceAlias = 6,
}

/// <summary>Classifies one bounded per-module metadata search fact.</summary>
public enum StaticFieldModuleSearchStatus
{
    /// <summary>The complete counted metadata image was searched exhaustively.</summary>
    Exact = 1,

    /// <summary>Some counted metadata was available, but traversal did not complete.</summary>
    Partial = 2,

    /// <summary>The required module metadata could not be observed.</summary>
    Unavailable = 3,

    /// <summary>Independently obtained module or metadata identities disagreed.</summary>
    Conflict = 4,

    /// <summary>The observed metadata violated a structural invariant.</summary>
    Invalid = 5,
}

/// <summary>Identifies the first source-specific failure in one per-module metadata search.</summary>
public enum StaticFieldModuleSearchIssue
{
    /// <summary>No issue occurred; valid only for an exact exhaustive module search.</summary>
    None = 0,

    /// <summary>A counted metadata read or table traversal ended with only a prefix.</summary>
    MetadataPartial = 1,

    /// <summary>A declared module-search bound stopped traversal before exhaustion.</summary>
    SearchBoundReached = 2,

    /// <summary>The module's counted metadata image was unavailable.</summary>
    MetadataUnavailable = 101,

    /// <summary>Runtime/module/content identity facts disagreed.</summary>
    MetadataIdentityConflict = 201,

    /// <summary>The metadata root, table layout, row, or signature was malformed.</summary>
    MalformedMetadata = 301,
}

/// <summary>Identifies the admitted declared storage shape of one complete static-field symbol.</summary>
public enum StaticFieldDeclaredValueKind
{
    /// <summary>The FieldDef stores one CLI <see cref="int"/> value.</summary>
    Int32 = 1,

    /// <summary>The FieldDef stores the admitted <see cref="Nullable{T}"/> specialization for <see cref="int"/>.</summary>
    NullableInt32 = 2,

    /// <summary>The FieldDef stores the explicit managed-string reference exception.</summary>
    String = 3,

    /// <summary>The FieldDef stores one exact resolved managed-reference type.</summary>
    ManagedReference = 4,

    /// <summary>The FieldDef stores the canonical intrinsic System.Object managed-reference type.</summary>
    Object = 5,
}

/// <summary>Identifies a storage-affecting field CustomAttribute marker excluded by W7.</summary>
public enum StaticFieldStorageMarkerKind
{
    /// <summary>The exact field carries System.ThreadStaticAttribute.</summary>
    ThreadStatic = 1,

    /// <summary>The exact field carries System.ContextStaticAttribute.</summary>
    ContextStatic = 2,
}

/// <summary>Identifies the metadata table encoding an exact marker-attribute constructor.</summary>
public enum StaticFieldAttributeConstructorKind
{
    /// <summary>The CustomAttribute.Type coded index names a same-module MethodDef.</summary>
    MethodDefinition = 1,

    /// <summary>The CustomAttribute.Type coded index names an exact MemberRef row.</summary>
    MemberReference = 2,
}

/// <summary>Identifies the type-shaped MemberRefParent form retained for a CustomAttribute constructor row.</summary>
public enum StaticFieldCustomAttributeMemberReferenceParentKind
{
    /// <summary>The MemberRef.Parent coded index names an exact same-module TypeDef.</summary>
    TypeDefinition = 1,

    /// <summary>The MemberRef.Parent coded index names one exact source-module TypeRef row.</summary>
    TypeReference = 2,

    /// <summary>The MemberRef.Parent coded index names one exact bounded generic-class TypeSpec row.</summary>
    TypeSpecification = 3,
}

/// <summary>Identifies the two admitted declared reference-target identity forms.</summary>
public enum StaticFieldDeclaredReferenceKind
{
    /// <summary>The signature names exact System.String intrinsically or through an AssemblyRef-scoped TypeRef.</summary>
    SystemString = 1,

    /// <summary>The signature names one exact resolved non-generic class or interface TypeDef.</summary>
    ManagedReference = 2,

    /// <summary>The signature uses intrinsic ELEMENT_TYPE_OBJECT and resolves to runtime-selected System.Object.</summary>
    SystemObject = 3,
}

/// <summary>Identifies the two truthful ECMA FieldSig encodings accepted for declared System.String.</summary>
public enum StaticFieldStringSignatureEncoding
{
    /// <summary>The FieldSig uses intrinsic ELEMENT_TYPE_STRING and therefore carries no source metadata token.</summary>
    PrimitiveString = 1,
}

/// <summary>Classifies one TypeDef from exact ancestry rather than from the insufficient TypeAttributes class bit.</summary>
/// <remarks>
/// ECMA-335 uses the same class-semantics flag for reference classes, value types, and enums. Consequently this tag is
/// never supplied by a caller: <see cref="StaticFieldTypeAncestryIdentity.Create"/> derives it from a contiguous
/// TypeDef <c>Extends</c> chain ending at an exact well-known terminal, or directly from interface semantics.
/// </remarks>
public enum StaticFieldTypeClassification
{
    /// <summary>The subject TypeDef has interface semantics and a nil Extends coded index.</summary>
    Interface = 1,

    /// <summary>The contiguous ancestry terminates at exact System.Object.</summary>
    ReferenceClass = 2,

    /// <summary>The contiguous ancestry terminates at exact System.ValueType.</summary>
    ValueType = 3,

    /// <summary>The contiguous ancestry terminates at exact System.Enum.</summary>
    Enum = 4,
}

/// <summary>Identifies the metadata table used by one exact TypeDef Extends relation.</summary>
public enum StaticFieldTypeBaseReferenceKind
{
    /// <summary>The Extends coded index directly names a TypeDef in the source metadata image.</summary>
    TypeDefinition = 1,

    /// <summary>The Extends coded index names an AssemblyRef-scoped TypeRef resolved to a TypeDef.</summary>
    TypeReference = 2,

    /// <summary>The Extends coded index names a TypeSpec whose bounded generic-instance signature resolves to a TypeDef.</summary>
    TypeSpecification = 3,
}

/// <summary>Identifies the terminal non-TypeRef ResolutionScope form of one exact TypeRef chain.</summary>
public enum StaticFieldTypeReferenceResolutionScopeKind
{
    /// <summary>The outermost TypeRef is scoped to the source metadata image's Module row.</summary>
    Module = 1,

    /// <summary>The outermost TypeRef is scoped through one exact ModuleRef row.</summary>
    ModuleReference = 2,

    /// <summary>The outermost TypeRef is scoped through one exact AssemblyRef row.</summary>
    AssemblyReference = 3,
}

/// <summary>Identifies the exact evidence closing a TypeRef scope to its resolved target TypeDef.</summary>
public enum StaticFieldTypeReferenceTargetResolutionKind
{
    /// <summary>A Module-scoped TypeRef resolves inside the exact source metadata module.</summary>
    SameModule = 1,

    /// <summary>A ModuleRef row names an exact target module in the same containing assembly.</summary>
    ModuleReference = 2,

    /// <summary>An AssemblyRef identity directly matches the target manifest AssemblyDef.</summary>
    DirectAssemblyReference = 3,

    /// <summary>An exact bounded ExportedType forwarder chain reaches the target containing assembly.</summary>
    ExportedTypeForwarder = 4,

    /// <summary>An exact runtime-loader observation correlates the source TypeRef with the resolved runtime TypeDef.</summary>
    RuntimeLoader = 5,
}

/// <summary>Identifies the runtime subsystem that supplied an exact source-TypeRef to target-TypeDef correlation.</summary>
public enum StaticFieldRuntimeTypeResolutionProvenance
{
    /// <summary>The correlation came from the immutable-dump ClrMD loader/type projection.</summary>
    ClrMdLoader = 1,
}

/// <summary>Identifies the exact runtime operation that independently correlated a metadata TypeRef and ClrType.</summary>
public enum StaticFieldRuntimeTypeResolutionOperation
{
    /// <summary>ClrType.BaseType on the source runtime type returned the target runtime type.</summary>
    AncestryBaseType = 1,

    /// <summary>ClrStaticField.Type on an exact declaring-runtime-type/FieldDef returned the target runtime type.</summary>
    FieldDeclaredType = 2,
}

/// <summary>Identifies the runtime operation that selected the base-class-library module.</summary>
public enum StaticFieldCoreLibrarySelectionProvenance
{
    /// <summary>The module is the exact ClrRuntime.BaseClassLibrary result projected by ClrMD.</summary>
    ClrMdRuntimeBaseClassLibrary = 1,
}

/// <summary>Identifies how a metadata module is proven to belong to its containing manifest assembly.</summary>
public enum StaticFieldMetadataModuleContainmentKind
{
    /// <summary>The metadata module is exactly the manifest module that owns the AssemblyDef row.</summary>
    ManifestModule = 1,

    /// <summary>ClrModule.Assembly selected the manifest assembly for a distinct netmodule.</summary>
    ClrMdAssemblyModule = 2,
}

/// <summary>Declares the shared pre-allocation text cap for detached W7 metadata-row strings.</summary>
public static class StaticFieldMetadataTextLimits
{
    /// <summary>Gets the maximum UTF-16 character count admitted for any retained metadata name or culture.</summary>
    public const int MaximumTextLength = 2048;

    /// <summary>Gets the canonical deterministic-bound name for retained metadata text.</summary>
    public const string MaximumTextLengthBoundName = "static-field.metadata-text-characters";

    /// <summary>Gets the declared metadata-text character bound.</summary>
    public static EvaluationDeterministicBound DeclaredTextLengthBound =>
        new(MaximumTextLengthBoundName, MaximumTextLength);

    internal static void Validate(string value, string parameterName, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value);
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            value.Length > MaximumTextLength ||
            value.IndexOf('\0', StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException(
                $"Metadata text must be initialized, NUL-free, at most {MaximumTextLength} characters, and satisfy its empty-value role.",
                parameterName);
        }
    }
}

/// <summary>Declares the per-candidate pre-allocation cap for exact ECMA FieldSig blobs.</summary>
public static class StaticFieldFieldSignatureLimits
{
    /// <summary>Gets the maximum exact FieldSig byte count admitted before copying or decoding.</summary>
    public const int MaximumFieldSignatureLength = 256;

    /// <summary>Gets the canonical deterministic-bound name for FieldSig bytes.</summary>
    public const string MaximumFieldSignatureLengthBoundName = "static-field.field-signature-bytes";

    /// <summary>Gets the maximum CustomMod count admitted before decoding the underlying field type.</summary>
    public const int MaximumCustomModifierCount = BoundedEcmaFieldSignatureProjection.MaximumCustomModifierCount;

    /// <summary>Gets the canonical deterministic-bound name for FieldSig CustomMod entries.</summary>
    public const string MaximumCustomModifierCountBoundName = "static-field.field-signature-custom-modifiers";

    /// <summary>Gets the declared FieldSig byte bound.</summary>
    public static EvaluationDeterministicBound DeclaredFieldSignatureLengthBound =>
        new(MaximumFieldSignatureLengthBoundName, MaximumFieldSignatureLength);

    /// <summary>Gets the declared FieldSig CustomMod-count bound.</summary>
    public static EvaluationDeterministicBound DeclaredCustomModifierCountBound =>
        new(MaximumCustomModifierCountBoundName, MaximumCustomModifierCount);
}

/// <summary>Freezes one complete AssemblyDef identity associated with a loaded metadata module.</summary>
/// <remarks>
/// The identity retains every Assembly table field that participates in metadata identity, including the complete
/// public-key blob rather than a display-name approximation. The containing module remains independently identified
/// by <see cref="ModuleContentIdentity"/> and <see cref="StaticFieldModuleInstanceIdentity"/>.
/// </remarks>
public sealed class StaticFieldAssemblyDefinitionIdentity : IEquatable<StaticFieldAssemblyDefinitionIdentity>
{
    /// <summary>Gets the maximum AssemblyDef name length admitted before canonical allocation.</summary>
    public const int MaximumNameLength = StaticFieldMetadataTextLimits.MaximumTextLength;

    /// <summary>Gets the maximum AssemblyDef culture length admitted before canonical allocation.</summary>
    public const int MaximumCultureLength = StaticFieldMetadataTextLimits.MaximumTextLength;

    /// <summary>Gets the maximum AssemblyDef public-key blob length admitted before copying.</summary>
    public const int MaximumPublicKeyLength = 16 * 1024;

    private const int PublicKeyFlag = 0x0001;
    private const string CanonicalDomain = "static-field-assembly-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> publicKey;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldAssemblyDefinitionIdentity(
        string name,
        int majorVersion,
        int minorVersion,
        int buildNumber,
        int revisionNumber,
        string culture,
        int flags,
        int hashAlgorithm,
        ImmutableArray<byte> publicKey)
    {
        Name = name;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        BuildNumber = buildNumber;
        RevisionNumber = revisionNumber;
        Culture = culture;
        Flags = flags;
        HashAlgorithm = hashAlgorithm;
        this.publicKey = publicKey;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(name);
        writer.WriteInt32(majorVersion);
        writer.WriteInt32(minorVersion);
        writer.WriteInt32(buildNumber);
        writer.WriteInt32(revisionNumber);
        writer.WriteString(culture);
        writer.WriteInt32(flags);
        writer.WriteInt32(hashAlgorithm);
        writer.WriteLengthPrefixedBytes(publicKey.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact non-empty AssemblyDef name.</summary>
    public string Name { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyDef major version as an integer.</summary>
    public int MajorVersion { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyDef minor version as an integer.</summary>
    public int MinorVersion { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyDef build number as an integer.</summary>
    public int BuildNumber { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyDef revision number as an integer.</summary>
    public int RevisionNumber { get; }

    /// <summary>Gets the exact AssemblyDef culture string, including empty for the neutral culture.</summary>
    public string Culture { get; }

    /// <summary>Gets the exact raw AssemblyFlags bits.</summary>
    public int Flags { get; }

    /// <summary>Gets the exact raw AssemblyHashAlgorithm value.</summary>
    public int HashAlgorithm { get; }

    /// <summary>Gets a defensive copy of the exact AssemblyDef public-key blob.</summary>
    public ImmutableArray<byte> PublicKey => CanonicalReplayEncoding.Copy(publicKey);

    /// <summary>Gets whether this exact definition has a recognized runtime base-class-library assembly name.</summary>
    public bool HasRuntimeCoreLibraryName =>
        string.Equals(Name, "System.Private.CoreLib", StringComparison.Ordinal) ||
        string.Equals(Name, "mscorlib", StringComparison.Ordinal);

    /// <summary>Gets a defensive copy of the versioned canonical AssemblyDef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete detached AssemblyDef identity.</summary>
    /// <param name="name">The exact non-empty AssemblyDef name.</param>
    /// <param name="majorVersion">The unsigned 16-bit major version.</param>
    /// <param name="minorVersion">The unsigned 16-bit minor version.</param>
    /// <param name="buildNumber">The unsigned 16-bit build number.</param>
    /// <param name="revisionNumber">The unsigned 16-bit revision number.</param>
    /// <param name="culture">The exact culture string, including empty for neutral culture.</param>
    /// <param name="flags">The exact raw AssemblyFlags bits.</param>
    /// <param name="hashAlgorithm">The exact raw AssemblyHashAlgorithm value.</param>
    /// <param name="publicKey">The initialized exact public-key blob; empty is retained exactly.</param>
    /// <returns>An immutable content-addressed AssemblyDef identity.</returns>
    /// <exception cref="ArgumentException">A required string/blob is absent or a version component is outside UInt16.</exception>
    public static StaticFieldAssemblyDefinitionIdentity Create(
        string name,
        int majorVersion,
        int minorVersion,
        int buildNumber,
        int revisionNumber,
        string culture,
        int flags,
        int hashAlgorithm,
        ImmutableArray<byte> publicKey)
    {
        ValidateAssemblyName(name, nameof(name));
        ValidateVersionComponent(majorVersion, nameof(majorVersion));
        ValidateVersionComponent(minorVersion, nameof(minorVersion));
        ValidateVersionComponent(buildNumber, nameof(buildNumber));
        ValidateVersionComponent(revisionNumber, nameof(revisionNumber));
        StaticFieldMetadataTextLimits.Validate(culture, nameof(culture), allowEmpty: true);
        if (publicKey.IsDefault || publicKey.Length > MaximumPublicKeyLength)
        {
            throw new ArgumentException(
                $"An initialized AssemblyDef public key of at most {MaximumPublicKeyLength} bytes is required.",
                nameof(publicKey));
        }
        if (((flags & PublicKeyFlag) != 0) != !publicKey.IsEmpty)
        {
            throw new ArgumentException(
                "AssemblyDef PublicKey flag and public-key blob presence must agree exactly.",
                nameof(flags));
        }

        return new StaticFieldAssemblyDefinitionIdentity(
            name,
            majorVersion,
            minorVersion,
            buildNumber,
            revisionNumber,
            culture,
            flags,
            hashAlgorithm,
            CanonicalReplayEncoding.Copy(publicKey));
    }

    /// <summary>Determines whether another value has the same complete AssemblyDef row content.</summary>
    /// <param name="other">The identity to compare.</param>
    /// <returns><see langword="true"/> only when every canonical AssemblyDef field matches.</returns>
    public bool Equals(StaticFieldAssemblyDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldAssemblyDefinitionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static void ValidateAssemblyName(string name, string parameterName)
    {
        StaticFieldMetadataTextLimits.Validate(name, parameterName, allowEmpty: false);
    }

    internal static void ValidateVersionComponent(int value, string parameterName)
    {
        if ((uint)value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Assembly version components must fit UInt16.");
        }
    }
}

/// <summary>Freezes one complete AssemblyRef row used as a TypeRef resolution-scope root.</summary>
public sealed class StaticFieldAssemblyReferenceIdentity : IEquatable<StaticFieldAssemblyReferenceIdentity>
{
    /// <summary>Gets the maximum AssemblyRef public-key/token blob length admitted before copying.</summary>
    public const int MaximumPublicKeyOrTokenLength = StaticFieldAssemblyDefinitionIdentity.MaximumPublicKeyLength;

    /// <summary>Gets the maximum AssemblyRef hash-value blob length admitted before copying.</summary>
    public const int MaximumHashValueLength = 1024;

    private const int PublicKeyFlag = 0x0001;
    private const string CanonicalDomain = "static-field-assembly-reference-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> publicKeyOrToken;
    private readonly ImmutableArray<byte> hashValue;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldAssemblyReferenceIdentity(
        int assemblyReferenceToken,
        string name,
        int majorVersion,
        int minorVersion,
        int buildNumber,
        int revisionNumber,
        string culture,
        int flags,
        ImmutableArray<byte> publicKeyOrToken,
        ImmutableArray<byte> hashValue)
    {
        AssemblyReferenceToken = assemblyReferenceToken;
        Name = name;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        BuildNumber = buildNumber;
        RevisionNumber = revisionNumber;
        Culture = culture;
        Flags = flags;
        this.publicKeyOrToken = publicKeyOrToken;
        this.hashValue = hashValue;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(assemblyReferenceToken);
        writer.WriteString(name);
        writer.WriteInt32(majorVersion);
        writer.WriteInt32(minorVersion);
        writer.WriteInt32(buildNumber);
        writer.WriteInt32(revisionNumber);
        writer.WriteString(culture);
        writer.WriteInt32(flags);
        writer.WriteLengthPrefixedBytes(publicKeyOrToken.AsSpan());
        writer.WriteLengthPrefixedBytes(hashValue.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the non-nil AssemblyRef token.</summary>
    public int AssemblyReferenceToken { get; }

    /// <summary>Gets the exact AssemblyRef name.</summary>
    public string Name { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyRef major version as an integer.</summary>
    public int MajorVersion { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyRef minor version as an integer.</summary>
    public int MinorVersion { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyRef build number as an integer.</summary>
    public int BuildNumber { get; }

    /// <summary>Gets the unsigned 16-bit AssemblyRef revision number as an integer.</summary>
    public int RevisionNumber { get; }

    /// <summary>Gets the exact AssemblyRef culture string.</summary>
    public string Culture { get; }

    /// <summary>Gets the exact raw AssemblyRef flags.</summary>
    public int Flags { get; }

    /// <summary>Gets a defensive copy of the exact public-key-or-token blob.</summary>
    public ImmutableArray<byte> PublicKeyOrToken => CanonicalReplayEncoding.Copy(publicKeyOrToken);

    /// <summary>Gets a defensive copy of the exact AssemblyRef hash-value blob.</summary>
    public ImmutableArray<byte> HashValue => CanonicalReplayEncoding.Copy(hashValue);

    /// <summary>Gets whether this row names a recognized core-library definition or facade.</summary>
    public bool HasKnownCoreLibraryScopeName =>
        string.Equals(Name, "System.Runtime", StringComparison.Ordinal) ||
        string.Equals(Name, "System.Private.CoreLib", StringComparison.Ordinal) ||
        string.Equals(Name, "mscorlib", StringComparison.Ordinal) ||
        string.Equals(Name, "netstandard", StringComparison.Ordinal);

    /// <summary>Gets a defensive copy of the versioned canonical AssemblyRef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact detached AssemblyRef row identity.</summary>
    /// <param name="assemblyReferenceToken">The non-nil AssemblyRef token.</param>
    /// <param name="name">The exact non-empty AssemblyRef name.</param>
    /// <param name="majorVersion">The unsigned 16-bit major version.</param>
    /// <param name="minorVersion">The unsigned 16-bit minor version.</param>
    /// <param name="buildNumber">The unsigned 16-bit build number.</param>
    /// <param name="revisionNumber">The unsigned 16-bit revision number.</param>
    /// <param name="culture">The exact culture string, including empty for neutral culture.</param>
    /// <param name="flags">The exact raw AssemblyRef flags.</param>
    /// <param name="publicKeyOrToken">The initialized exact public-key-or-token blob.</param>
    /// <param name="hashValue">The initialized exact hash-value blob.</param>
    /// <returns>An immutable content-addressed AssemblyRef row.</returns>
    public static StaticFieldAssemblyReferenceIdentity Create(
        int assemblyReferenceToken,
        string name,
        int majorVersion,
        int minorVersion,
        int buildNumber,
        int revisionNumber,
        string culture,
        int flags,
        ImmutableArray<byte> publicKeyOrToken,
        ImmutableArray<byte> hashValue)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            assemblyReferenceToken,
            0x23,
            nameof(assemblyReferenceToken));
        StaticFieldAssemblyDefinitionIdentity.ValidateAssemblyName(name, nameof(name));
        StaticFieldAssemblyDefinitionIdentity.ValidateVersionComponent(majorVersion, nameof(majorVersion));
        StaticFieldAssemblyDefinitionIdentity.ValidateVersionComponent(minorVersion, nameof(minorVersion));
        StaticFieldAssemblyDefinitionIdentity.ValidateVersionComponent(buildNumber, nameof(buildNumber));
        StaticFieldAssemblyDefinitionIdentity.ValidateVersionComponent(revisionNumber, nameof(revisionNumber));
        StaticFieldMetadataTextLimits.Validate(culture, nameof(culture), allowEmpty: true);
        if (publicKeyOrToken.IsDefault || hashValue.IsDefault ||
            publicKeyOrToken.Length > MaximumPublicKeyOrTokenLength ||
            hashValue.Length > MaximumHashValueLength)
        {
            throw new ArgumentException("Initialized, bounded AssemblyRef key/token and hash blobs are required.");
        }
        if ((flags & PublicKeyFlag) != 0)
        {
            if (publicKeyOrToken.IsEmpty)
            {
                throw new ArgumentException("An AssemblyRef with PublicKey set requires the complete non-empty key blob.", nameof(flags));
            }
        }
        else if (publicKeyOrToken.Length is not (0 or 8))
        {
            throw new ArgumentException("An AssemblyRef without PublicKey must retain either no key or an 8-byte token.", nameof(publicKeyOrToken));
        }

        return new StaticFieldAssemblyReferenceIdentity(
            assemblyReferenceToken,
            name,
            majorVersion,
            minorVersion,
            buildNumber,
            revisionNumber,
            culture,
            flags,
            CanonicalReplayEncoding.Copy(publicKeyOrToken),
            CanonicalReplayEncoding.Copy(hashValue));
    }

    /// <summary>Determines whether another value has the same complete AssemblyRef row content.</summary>
    /// <param name="other">The identity to compare.</param>
    /// <returns><see langword="true"/> only when every canonical AssemblyRef field matches.</returns>
    public bool Equals(StaticFieldAssemblyReferenceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldAssemblyReferenceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the exact single Module row associated with one counted metadata image.</summary>
public sealed class StaticFieldModuleDefinitionIdentity : IEquatable<StaticFieldModuleDefinitionIdentity>
{
    private const string CanonicalDomain = "static-field-module-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldModuleDefinitionIdentity(
        int generation,
        string name,
        Guid mvid,
        Guid encId,
        Guid encBaseId)
    {
        Generation = generation;
        Name = name;
        Mvid = mvid;
        EncId = encId;
        EncBaseId = encBaseId;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(generation);
        writer.WriteString(name);
        writer.WriteString(mvid.ToString("D"));
        writer.WriteString(encId.ToString("D"));
        writer.WriteString(encBaseId.ToString("D"));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact unsigned 16-bit Module.Generation value as an integer.</summary>
    public int Generation { get; }

    /// <summary>Gets the exact non-empty Module.Name value.</summary>
    public string Name { get; }

    /// <summary>Gets the exact non-nil Module.Mvid value.</summary>
    public Guid Mvid { get; }

    /// <summary>Gets the exact Module.EncId value, including empty when the heap index is nil.</summary>
    public Guid EncId { get; }

    /// <summary>Gets the exact Module.EncBaseId value, including empty when the heap index is nil.</summary>
    public Guid EncBaseId { get; }

    /// <summary>Gets a defensive copy of the versioned canonical Module-row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete exact Module row.</summary>
    /// <param name="generation">The unsigned 16-bit generation.</param>
    /// <param name="name">The exact non-empty module name.</param>
    /// <param name="mvid">The exact non-empty MVID.</param>
    /// <param name="encId">The exact EncId, or empty for a nil heap index.</param>
    /// <param name="encBaseId">The exact EncBaseId, or empty for a nil heap index.</param>
    /// <returns>A complete immutable Module-row identity.</returns>
    public static StaticFieldModuleDefinitionIdentity Create(
        int generation,
        string name,
        Guid mvid,
        Guid encId,
        Guid encBaseId)
    {
        StaticFieldAssemblyDefinitionIdentity.ValidateVersionComponent(generation, nameof(generation));
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (mvid == Guid.Empty)
        {
            throw new ArgumentException("A non-empty Module MVID is required.", nameof(mvid));
        }
        return new StaticFieldModuleDefinitionIdentity(generation, name, mvid, encId, encBaseId);
    }

    /// <summary>Determines whether another value has the same complete Module row.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when every canonical Module field matches.</returns>
    public bool Equals(StaticFieldModuleDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldModuleDefinitionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Identifies an exact containing assembly through its loaded manifest module and AssemblyDef row.</summary>
public sealed class StaticFieldContainingAssemblyIdentity : IEquatable<StaticFieldContainingAssemblyIdentity>
{
    private const string CanonicalDomain = "static-field-containing-assembly-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldContainingAssemblyIdentity(
        StaticFieldModuleInstanceIdentity manifestModule,
        ModuleContentIdentity manifestModuleContent,
        StaticFieldModuleDefinitionIdentity manifestModuleDefinition,
        StaticFieldAssemblyDefinitionIdentity assemblyDefinition)
    {
        ManifestModule = manifestModule;
        ManifestModuleContent = manifestModuleContent;
        ManifestModuleDefinition = manifestModuleDefinition;
        AssemblyDefinition = assemblyDefinition;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(manifestModule.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteModuleContent(writer, manifestModuleContent);
        writer.WriteLengthPrefixedBytes(manifestModuleDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(assemblyDefinition.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact loaded manifest module owning the Assembly row.</summary>
    public StaticFieldModuleInstanceIdentity ManifestModule { get; }

    /// <summary>Gets the manifest module's complete counted metadata identity.</summary>
    public ModuleContentIdentity ManifestModuleContent { get; }

    /// <summary>Gets the manifest module's exact Module row.</summary>
    public StaticFieldModuleDefinitionIdentity ManifestModuleDefinition { get; }

    /// <summary>Gets the manifest module's exact AssemblyDef row.</summary>
    public StaticFieldAssemblyDefinitionIdentity AssemblyDefinition { get; }

    /// <summary>Gets a defensive copy of the versioned canonical containing-assembly bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact manifest-module/AssemblyDef relation.</summary>
    /// <param name="manifestModule">The loaded manifest module.</param>
    /// <param name="manifestModuleContent">Its complete counted metadata content.</param>
    /// <param name="manifestModuleDefinition">Its exact Module row.</param>
    /// <param name="assemblyDefinition">Its exact single AssemblyDef row.</param>
    /// <returns>A canonical containing-assembly identity usable by manifest modules and netmodules.</returns>
    public static StaticFieldContainingAssemblyIdentity Create(
        StaticFieldModuleInstanceIdentity manifestModule,
        ModuleContentIdentity manifestModuleContent,
        StaticFieldModuleDefinitionIdentity manifestModuleDefinition,
        StaticFieldAssemblyDefinitionIdentity assemblyDefinition)
    {
        ArgumentNullException.ThrowIfNull(manifestModule);
        ArgumentNullException.ThrowIfNull(manifestModuleContent);
        ArgumentNullException.ThrowIfNull(manifestModuleDefinition);
        ArgumentNullException.ThrowIfNull(assemblyDefinition);
        if (manifestModuleContent.Mvid != manifestModuleDefinition.Mvid)
        {
            throw new ArgumentException("The manifest Module row MVID disagrees with its counted metadata content.");
        }
        return new StaticFieldContainingAssemblyIdentity(
            manifestModule,
            manifestModuleContent,
            manifestModuleDefinition,
            assemblyDefinition);
    }

    /// <summary>Determines whether another value has the same manifest module, content, and AssemblyDef.</summary>
    /// <param name="other">The relation to compare.</param>
    /// <returns><see langword="true"/> only when all canonical relation facts match.</returns>
    public bool Equals(StaticFieldContainingAssemblyIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldContainingAssemblyIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Bundles one loaded metadata module with its Module row and exact containing-manifest relation.</summary>
/// <summary>Freezes one exact manifest File row relating an assembly to a metadata-bearing netmodule.</summary>
public sealed class StaticFieldAssemblyFileIdentity : IEquatable<StaticFieldAssemblyFileIdentity>
{
    /// <summary>Gets the maximum File.HashValue length admitted before copying.</summary>
    public const int MaximumHashValueLength = 1024;

    private const int ContainsNoMetadataFlag = 0x0001;
    private const string CanonicalDomain = "static-field-assembly-file-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> hashValue;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldAssemblyFileIdentity(
        int fileToken,
        int flags,
        string name,
        ImmutableArray<byte> hashValue)
    {
        FileToken = fileToken;
        Flags = flags;
        Name = name;
        this.hashValue = hashValue;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(fileToken);
        writer.WriteInt32(flags);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(hashValue.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the non-nil File token.</summary>
    public int FileToken { get; }

    /// <summary>Gets the exact raw File flags.</summary>
    public int Flags { get; }

    /// <summary>Gets the exact File name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the File row declares that the target contains metadata; required true here.</summary>
    public bool ContainsMetadata => (Flags & ContainsNoMetadataFlag) == 0;

    /// <summary>Gets a defensive copy of the exact File hash-value blob.</summary>
    public ImmutableArray<byte> HashValue => CanonicalReplayEncoding.Copy(hashValue);

    /// <summary>Gets a defensive copy of the versioned canonical File-row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact metadata-bearing manifest File row.</summary>
    /// <param name="fileToken">The non-nil File token.</param>
    /// <param name="flags">The exact flags, required not to contain ContainsNoMetadata.</param>
    /// <param name="name">The exact bounded File name.</param>
    /// <param name="hashValue">The initialized bounded exact File hash blob.</param>
    /// <returns>A canonical metadata-bearing File-row identity.</returns>
    public static StaticFieldAssemblyFileIdentity Create(
        int fileToken,
        int flags,
        string name,
        ImmutableArray<byte> hashValue)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(fileToken, 0x26, nameof(fileToken));
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if ((flags & ContainsNoMetadataFlag) != 0)
        {
            throw new ArgumentException("A netmodule containment File row must declare metadata content.", nameof(flags));
        }
        if (hashValue.IsDefault || hashValue.Length > MaximumHashValueLength)
        {
            throw new ArgumentException(
                $"An initialized File hash of at most {MaximumHashValueLength} bytes is required.",
                nameof(hashValue));
        }
        return new StaticFieldAssemblyFileIdentity(
            fileToken,
            flags,
            name,
            CanonicalReplayEncoding.Copy(hashValue));
    }

    /// <summary>Determines whether another value has the same exact File row.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when token, flags, name, and hash match.</returns>
    public bool Equals(StaticFieldAssemblyFileIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldAssemblyFileIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Bundles one loaded metadata module with its Module row and exact containing-manifest relation.</summary>
public sealed class StaticFieldMetadataModuleIdentity : IEquatable<StaticFieldMetadataModuleIdentity>
{
    private const string CanonicalDomain = "static-field-metadata-module-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldMetadataModuleIdentity(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        StaticFieldModuleDefinitionIdentity moduleDefinition,
        StaticFieldContainingAssemblyIdentity containingAssembly,
        StaticFieldMetadataModuleContainmentKind containmentKind,
        int? runtimeOrdinal,
        StaticFieldAssemblyFileIdentity? manifestFile)
    {
        Module = module;
        ModuleContent = moduleContent;
        ModuleDefinition = moduleDefinition;
        ContainingAssembly = containingAssembly;
        ContainmentKind = containmentKind;
        RuntimeOrdinal = runtimeOrdinal;
        ManifestFile = manifestFile;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(module.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteModuleContent(writer, moduleContent);
        writer.WriteLengthPrefixedBytes(moduleDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(containingAssembly.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)containmentKind);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, runtimeOrdinal);
        writer.WriteBoolean(manifestFile is not null);
        if (manifestFile is not null)
        {
            writer.WriteLengthPrefixedBytes(manifestFile.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact loaded module instance containing metadata rows.</summary>
    public StaticFieldModuleInstanceIdentity Module { get; }

    /// <summary>Gets the complete counted module metadata identity.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the module's exact single Module row.</summary>
    public StaticFieldModuleDefinitionIdentity ModuleDefinition { get; }

    /// <summary>Gets the exact loaded manifest-module/AssemblyDef relation containing this module.</summary>
    public StaticFieldContainingAssemblyIdentity ContainingAssembly { get; }

    /// <summary>Gets the exact manifest-self or ClrModule.Assembly containment proof kind.</summary>
    public StaticFieldMetadataModuleContainmentKind ContainmentKind { get; }

    /// <summary>Gets the ClrRuntime ordinal for a netmodule relation, otherwise null.</summary>
    public int? RuntimeOrdinal { get; }

    /// <summary>Gets an exact manifest File row when available for a netmodule, otherwise null.</summary>
    public StaticFieldAssemblyFileIdentity? ManifestFile { get; }

    /// <summary>Gets whether this module is itself the containing assembly's manifest module.</summary>
    public bool IsManifestModule => ContainmentKind == StaticFieldMetadataModuleContainmentKind.ManifestModule;

    /// <summary>Gets a defensive copy of the versioned canonical metadata-module bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact self-contained manifest-module relation.</summary>
    /// <param name="module">The loaded metadata module.</param>
    /// <param name="moduleContent">Its complete counted metadata identity.</param>
    /// <param name="moduleDefinition">Its exact Module row.</param>
    /// <param name="containingAssembly">Its exact containing manifest module and AssemblyDef.</param>
    /// <returns>A canonical manifest metadata-module identity.</returns>
    public static StaticFieldMetadataModuleIdentity ForManifestModule(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        StaticFieldModuleDefinitionIdentity moduleDefinition,
        StaticFieldContainingAssemblyIdentity containingAssembly) =>
        Create(
            module,
            moduleContent,
            moduleDefinition,
            containingAssembly,
            StaticFieldMetadataModuleContainmentKind.ManifestModule,
            runtimeOrdinal: null,
            manifestFile: null);

    /// <summary>Creates a netmodule relation selected by ClrModule.Assembly with optional exact manifest File evidence.</summary>
    /// <param name="module">The loaded netmodule.</param>
    /// <param name="moduleContent">Its complete counted metadata identity.</param>
    /// <param name="moduleDefinition">Its exact Module row.</param>
    /// <param name="containingAssembly">The exact manifest assembly selected by the runtime.</param>
    /// <param name="runtimeOrdinal">The non-negative ClrRuntime ordinal used for ClrModule.Assembly.</param>
    /// <param name="manifestFile">The exact matching metadata-bearing manifest File row when available.</param>
    /// <returns>A canonical operation-specific netmodule containment relation.</returns>
    public static StaticFieldMetadataModuleIdentity ForNetmodule(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        StaticFieldModuleDefinitionIdentity moduleDefinition,
        StaticFieldContainingAssemblyIdentity containingAssembly,
        int runtimeOrdinal,
        StaticFieldAssemblyFileIdentity? manifestFile = null) =>
        Create(
            module,
            moduleContent,
            moduleDefinition,
            containingAssembly,
            StaticFieldMetadataModuleContainmentKind.ClrMdAssemblyModule,
            runtimeOrdinal,
            manifestFile);

    private static StaticFieldMetadataModuleIdentity Create(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        StaticFieldModuleDefinitionIdentity moduleDefinition,
        StaticFieldContainingAssemblyIdentity containingAssembly,
        StaticFieldMetadataModuleContainmentKind containmentKind,
        int? runtimeOrdinal,
        StaticFieldAssemblyFileIdentity? manifestFile)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moduleContent);
        ArgumentNullException.ThrowIfNull(moduleDefinition);
        ArgumentNullException.ThrowIfNull(containingAssembly);
        StaticFieldContractEncoding.RequireDefined(containmentKind, nameof(containmentKind));
        if (moduleContent.Mvid != moduleDefinition.Mvid ||
            !string.Equals(module.SnapshotSha256, containingAssembly.ManifestModule.SnapshotSha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A metadata module must match its Module-row MVID and containing manifest snapshot.");
        }
        var physicallyManifest =
            module.Equals(containingAssembly.ManifestModule) &&
            moduleContent.Equals(containingAssembly.ManifestModuleContent) &&
            moduleDefinition.Equals(containingAssembly.ManifestModuleDefinition);
        if (containmentKind == StaticFieldMetadataModuleContainmentKind.ManifestModule)
        {
            if (!physicallyManifest || runtimeOrdinal.HasValue || manifestFile is not null)
            {
                throw new ArgumentException("Manifest-module containment must be exact self-identity without netmodule evidence.");
            }
        }
        else
        {
            if (physicallyManifest || !runtimeOrdinal.HasValue || runtimeOrdinal.Value < 0 ||
                manifestFile is not null &&
                    !string.Equals(manifestFile.Name, moduleDefinition.Name, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A netmodule requires a distinct same-snapshot manifest selected by a runtime operation and matching File row when present.");
            }
        }
        return new StaticFieldMetadataModuleIdentity(
            module,
            moduleContent,
            moduleDefinition,
            containingAssembly,
            containmentKind,
            runtimeOrdinal,
            manifestFile);
    }

    /// <summary>Determines whether another value has the same loaded module, content, Module row, and containing assembly.</summary>
    /// <param name="other">The identity to compare.</param>
    /// <returns><see langword="true"/> only when every canonical module relation matches.</returns>
    public bool Equals(StaticFieldMetadataModuleIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldMetadataModuleIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact top-level ExportedType forwarder hop between two manifest assemblies.</summary>
public sealed class StaticFieldExportedTypeForwarderIdentity : IEquatable<StaticFieldExportedTypeForwarderIdentity>
{
    private const int TypeForwarderAttribute = 0x00200000;
    private const string CanonicalDomain = "static-field-exported-type-forwarder-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldExportedTypeForwarderIdentity(
        StaticFieldContainingAssemblyIdentity sourceAssembly,
        int exportedTypeToken,
        int typeAttributes,
        int typeDefinitionId,
        string namespaceName,
        string typeName,
        StaticFieldAssemblyReferenceIdentity implementationAssemblyReference,
        StaticFieldContainingAssemblyIdentity targetAssembly)
    {
        SourceAssembly = sourceAssembly;
        ExportedTypeToken = exportedTypeToken;
        TypeAttributes = typeAttributes;
        TypeDefinitionId = typeDefinitionId;
        NamespaceName = namespaceName;
        TypeName = typeName;
        ImplementationAssemblyReference = implementationAssemblyReference;
        TargetAssembly = targetAssembly;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceAssembly.CanonicalBytes.AsSpan());
        writer.WriteInt32(exportedTypeToken);
        writer.WriteInt32(typeAttributes);
        writer.WriteInt32(typeDefinitionId);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteLengthPrefixedBytes(implementationAssemblyReference.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(targetAssembly.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact manifest assembly containing the ExportedType row.</summary>
    public StaticFieldContainingAssemblyIdentity SourceAssembly { get; }

    /// <summary>Gets the non-nil ExportedType token.</summary>
    public int ExportedTypeToken { get; }

    /// <summary>Gets the exact raw TypeAttributes value, including the Forwarder bit.</summary>
    public int TypeAttributes { get; }

    /// <summary>Gets the exact raw ExportedType.TypeDefId value.</summary>
    public int TypeDefinitionId { get; }

    /// <summary>Gets the exact forwarded namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact forwarded metadata type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact AssemblyRef row stored in the Implementation coded index.</summary>
    public StaticFieldAssemblyReferenceIdentity ImplementationAssemblyReference { get; }

    /// <summary>Gets the exact manifest assembly selected by that AssemblyRef.</summary>
    public StaticFieldContainingAssemblyIdentity TargetAssembly { get; }

    /// <summary>Gets a defensive copy of the versioned canonical forwarder-hop bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact top-level AssemblyRef-backed ExportedType forwarder hop.</summary>
    /// <param name="sourceAssembly">The manifest assembly containing the row.</param>
    /// <param name="exportedTypeToken">The non-nil ExportedType token.</param>
    /// <param name="typeAttributes">The exact attributes, required to contain the Forwarder bit.</param>
    /// <param name="typeDefinitionId">The exact raw TypeDefId value.</param>
    /// <param name="namespaceName">The exact forwarded namespace.</param>
    /// <param name="typeName">The exact forwarded metadata name.</param>
    /// <param name="implementationAssemblyReference">The complete AssemblyRef implementation row.</param>
    /// <param name="targetAssembly">The exact manifest assembly identity selected by that row.</param>
    /// <returns>A canonical forwarder hop whose AssemblyRef matches its target AssemblyDef.</returns>
    public static StaticFieldExportedTypeForwarderIdentity Create(
        StaticFieldContainingAssemblyIdentity sourceAssembly,
        int exportedTypeToken,
        int typeAttributes,
        int typeDefinitionId,
        string namespaceName,
        string typeName,
        StaticFieldAssemblyReferenceIdentity implementationAssemblyReference,
        StaticFieldContainingAssemblyIdentity targetAssembly)
    {
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentNullException.ThrowIfNull(implementationAssemblyReference);
        ArgumentNullException.ThrowIfNull(targetAssembly);
        CanonicalReplayEncoding.ValidateMetadataToken(exportedTypeToken, 0x27, nameof(exportedTypeToken));
        ArgumentOutOfRangeException.ThrowIfNegative(typeDefinitionId);
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }
        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        if ((typeAttributes & TypeForwarderAttribute) == 0)
        {
            throw new ArgumentException("An ExportedType forwarder row must retain the Forwarder attribute.", nameof(typeAttributes));
        }
        if (!AssemblyReferenceMatchesDefinition(implementationAssemblyReference, targetAssembly.AssemblyDefinition))
        {
            throw new ArgumentException("The forwarder Implementation AssemblyRef does not identity-bind its target AssemblyDef.");
        }
        return new StaticFieldExportedTypeForwarderIdentity(
            sourceAssembly,
            exportedTypeToken,
            typeAttributes,
            typeDefinitionId,
            namespaceName,
            typeName,
            implementationAssemblyReference,
            targetAssembly);
    }

    /// <summary>Determines whether another value has the same complete forwarder row and source/target assemblies.</summary>
    /// <param name="other">The hop to compare.</param>
    /// <returns><see langword="true"/> only when every canonical forwarder fact matches.</returns>
    public bool Equals(StaticFieldExportedTypeForwarderIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldExportedTypeForwarderIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool AssemblyReferenceMatchesDefinition(
        StaticFieldAssemblyReferenceIdentity reference,
        StaticFieldAssemblyDefinitionIdentity definition)
    {
        const int publicKeyFlag = 0x0001;
        const int retargetableFlag = 0x0100;
        const int contentTypeMask = 0x0E00;
        if (!string.Equals(reference.Name, definition.Name, StringComparison.Ordinal) ||
            reference.MajorVersion != definition.MajorVersion ||
            reference.MinorVersion != definition.MinorVersion ||
            reference.BuildNumber != definition.BuildNumber ||
            reference.RevisionNumber != definition.RevisionNumber ||
            !string.Equals(reference.Culture, definition.Culture, StringComparison.Ordinal) ||
            (reference.Flags & retargetableFlag) != 0 ||
            (reference.Flags & contentTypeMask) != (definition.Flags & contentTypeMask))
        {
            return false;
        }

        var referenceKey = reference.PublicKeyOrToken;
        var definitionKey = definition.PublicKey;
        if ((reference.Flags & publicKeyFlag) != 0)
        {
            return !referenceKey.IsEmpty && referenceKey.AsSpan().SequenceEqual(definitionKey.AsSpan());
        }
        if (referenceKey.IsEmpty)
        {
            return definitionKey.IsEmpty;
        }
        if (!definitionKey.IsEmpty)
        {
            var hash = System.Security.Cryptography.SHA1.HashData(definitionKey.AsSpan());
            Span<byte> token = stackalloc byte[8];
            for (var index = 0; index < token.Length; index++)
            {
                token[index] = hash[hash.Length - 1 - index];
            }
            return referenceKey.AsSpan().SequenceEqual(token);
        }
        return false;
    }
}

/// <summary>Freezes one exact runtime ClrType identity independently anchored to a metadata TypeDef.</summary>
public sealed class StaticFieldRuntimeTypeIdentity : IEquatable<StaticFieldRuntimeTypeIdentity>
{
    private const string CanonicalDomain = "static-field-runtime-type-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldRuntimeTypeIdentity(
        ulong runtimeTypeAddress,
        ulong methodTable,
        StaticFieldTypeDefinitionIdentity typeDefinition)
    {
        RuntimeTypeAddress = runtimeTypeAddress;
        MethodTable = methodTable;
        TypeDefinition = typeDefinition;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(runtimeTypeAddress.ToString("x16"));
        writer.WriteString(methodTable.ToString("x16"));
        writer.WriteLengthPrefixedBytes(typeDefinition.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the non-zero immutable-dump address identifying the runtime ClrType projection.</summary>
    public ulong RuntimeTypeAddress { get; }

    /// <summary>Gets the non-zero method-table address reported for this non-array TypeDef-backed runtime type.</summary>
    public ulong MethodTable { get; }

    /// <summary>Gets the exact metadata TypeDef identity correlated with the runtime type.</summary>
    public StaticFieldTypeDefinitionIdentity TypeDefinition { get; }

    /// <summary>Gets a defensive copy of the versioned canonical runtime-type bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact runtime-type/metadata-TypeDef correlation.</summary>
    /// <param name="runtimeTypeAddress">The non-zero runtime type address or stable ClrMD type identity address.</param>
    /// <param name="methodTable">The non-zero method-table address.</param>
    /// <param name="typeDefinition">The exact correlated metadata TypeDef.</param>
    /// <returns>A canonical detached runtime type identity.</returns>
    public static StaticFieldRuntimeTypeIdentity Create(
        ulong runtimeTypeAddress,
        ulong methodTable,
        StaticFieldTypeDefinitionIdentity typeDefinition)
    {
        ArgumentNullException.ThrowIfNull(typeDefinition);
        if (runtimeTypeAddress == 0 || methodTable == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeTypeAddress),
                "A TypeDef-backed runtime type requires non-zero runtime and method-table addresses.");
        }
        return new StaticFieldRuntimeTypeIdentity(runtimeTypeAddress, methodTable, typeDefinition);
    }

    /// <summary>Determines whether another value identifies the same runtime type and exact TypeDef.</summary>
    /// <param name="other">The runtime identity to compare.</param>
    /// <returns><see langword="true"/> only when addresses and canonical TypeDef match.</returns>
    public bool Equals(StaticFieldRuntimeTypeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldRuntimeTypeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one operation-specific exact runtime-loader correlation from a source TypeRef to a ClrType.</summary>
public sealed class StaticFieldRuntimeTypeResolutionIdentity : IEquatable<StaticFieldRuntimeTypeResolutionIdentity>
{
    private const string CanonicalDomain = "static-field-runtime-type-resolution-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldRuntimeTypeResolutionIdentity(
        StaticFieldRuntimeTypeResolutionProvenance provenance,
        StaticFieldRuntimeTypeResolutionOperation operation,
        StaticFieldMetadataModuleIdentity sourceModule,
        int sourceTypeReferenceToken,
        StaticFieldRuntimeTypeIdentity sourceRuntimeType,
        int? sourceFieldDefinitionToken,
        StaticFieldRuntimeTypeIdentity resolvedTargetRuntimeType)
    {
        Provenance = provenance;
        Operation = operation;
        SourceModule = sourceModule;
        SourceTypeReferenceToken = sourceTypeReferenceToken;
        SourceRuntimeType = sourceRuntimeType;
        SourceFieldDefinitionToken = sourceFieldDefinitionToken;
        ResolvedTargetRuntimeType = resolvedTargetRuntimeType;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)provenance);
        writer.WriteInt32((int)operation);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(sourceTypeReferenceToken);
        writer.WriteLengthPrefixedBytes(sourceRuntimeType.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, sourceFieldDefinitionToken);
        writer.WriteLengthPrefixedBytes(resolvedTargetRuntimeType.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact runtime subsystem that supplied the correlation.</summary>
    public StaticFieldRuntimeTypeResolutionProvenance Provenance { get; }

    /// <summary>Gets the exact runtime operation whose result supplied the target type.</summary>
    public StaticFieldRuntimeTypeResolutionOperation Operation { get; }

    /// <summary>Gets the source metadata module containing the TypeRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the exact source TypeRef token correlated by the loader.</summary>
    public int SourceTypeReferenceToken { get; }

    /// <summary>Gets the exact runtime source/declaring type on which the operation was performed.</summary>
    public StaticFieldRuntimeTypeIdentity SourceRuntimeType { get; }

    /// <summary>Gets the exact declaring FieldDef only for a field-declared-type operation.</summary>
    public int? SourceFieldDefinitionToken { get; }

    /// <summary>Gets the exact runtime type returned by BaseType or ClrStaticField.Type.</summary>
    public StaticFieldRuntimeTypeIdentity ResolvedTargetRuntimeType { get; }

    /// <summary>Gets the exact resolved runtime TypeDef identity.</summary>
    public StaticFieldTypeDefinitionIdentity ResolvedTargetType => ResolvedTargetRuntimeType.TypeDefinition;

    /// <summary>Gets a defensive copy of the versioned canonical runtime-resolution bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact ClrType.BaseType operation projection for ancestry resolution.</summary>
    /// <param name="provenance">The defined runtime resolution source.</param>
    /// <param name="sourceModule">The exact source metadata module.</param>
    /// <param name="sourceTypeReferenceToken">The non-nil source TypeRef token.</param>
    /// <param name="sourceRuntimeType">The exact source runtime type whose BaseType was read.</param>
    /// <param name="resolvedTargetRuntimeType">The exact non-null runtime BaseType result.</param>
    /// <returns>A canonical same-snapshot runtime resolution relation; AppDomain equality is not required.</returns>
    public static StaticFieldRuntimeTypeResolutionIdentity ForAncestryBaseType(
        StaticFieldRuntimeTypeResolutionProvenance provenance,
        StaticFieldMetadataModuleIdentity sourceModule,
        int sourceTypeReferenceToken,
        StaticFieldRuntimeTypeIdentity sourceRuntimeType,
        StaticFieldRuntimeTypeIdentity resolvedTargetRuntimeType) =>
        Create(
            provenance,
            StaticFieldRuntimeTypeResolutionOperation.AncestryBaseType,
            sourceModule,
            sourceTypeReferenceToken,
            sourceRuntimeType,
            sourceFieldDefinitionToken: null,
            resolvedTargetRuntimeType);

    /// <summary>Creates an exact ClrStaticField.Type operation projection for a declared field type.</summary>
    /// <param name="provenance">The defined runtime resolution source.</param>
    /// <param name="sourceModule">The exact metadata module owning the TypeRef and FieldDef.</param>
    /// <param name="sourceTypeReferenceToken">The non-nil source TypeRef token from FieldSig.</param>
    /// <param name="declaringRuntimeType">The exact runtime declaring type used to locate the field.</param>
    /// <param name="sourceFieldDefinitionToken">The exact non-nil declaring FieldDef token.</param>
    /// <param name="resolvedTargetRuntimeType">The exact runtime type returned by ClrStaticField.Type.</param>
    /// <returns>A canonical field-declared-type operation identity.</returns>
    public static StaticFieldRuntimeTypeResolutionIdentity ForFieldDeclaredType(
        StaticFieldRuntimeTypeResolutionProvenance provenance,
        StaticFieldMetadataModuleIdentity sourceModule,
        int sourceTypeReferenceToken,
        StaticFieldRuntimeTypeIdentity declaringRuntimeType,
        int sourceFieldDefinitionToken,
        StaticFieldRuntimeTypeIdentity resolvedTargetRuntimeType) =>
        Create(
            provenance,
            StaticFieldRuntimeTypeResolutionOperation.FieldDeclaredType,
            sourceModule,
            sourceTypeReferenceToken,
            declaringRuntimeType,
            sourceFieldDefinitionToken,
            resolvedTargetRuntimeType);

    private static StaticFieldRuntimeTypeResolutionIdentity Create(
        StaticFieldRuntimeTypeResolutionProvenance provenance,
        StaticFieldRuntimeTypeResolutionOperation operation,
        StaticFieldMetadataModuleIdentity sourceModule,
        int sourceTypeReferenceToken,
        StaticFieldRuntimeTypeIdentity sourceRuntimeType,
        int? sourceFieldDefinitionToken,
        StaticFieldRuntimeTypeIdentity resolvedTargetRuntimeType)
    {
        StaticFieldContractEncoding.RequireDefined(provenance, nameof(provenance));
        StaticFieldContractEncoding.RequireDefined(operation, nameof(operation));
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(sourceRuntimeType);
        ArgumentNullException.ThrowIfNull(resolvedTargetRuntimeType);
        CanonicalReplayEncoding.ValidateMetadataToken(sourceTypeReferenceToken, 0x01, nameof(sourceTypeReferenceToken));
        if (sourceFieldDefinitionToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(
                sourceFieldDefinitionToken.Value,
                0x04,
                nameof(sourceFieldDefinitionToken));
        }
        if (!string.Equals(
                sourceModule.Module.SnapshotSha256,
                resolvedTargetRuntimeType.TypeDefinition.Module.SnapshotSha256,
                StringComparison.Ordinal) ||
            !sourceRuntimeType.TypeDefinition.MetadataModule.Equals(sourceModule) ||
            (operation == StaticFieldRuntimeTypeResolutionOperation.AncestryBaseType) !=
                !sourceFieldDefinitionToken.HasValue)
        {
            throw new ArgumentException(
                "Runtime operation, source module/type/field, and target must form one same-snapshot exact relation.");
        }
        return new StaticFieldRuntimeTypeResolutionIdentity(
            provenance,
            operation,
            sourceModule,
            sourceTypeReferenceToken,
            sourceRuntimeType,
            sourceFieldDefinitionToken,
            resolvedTargetRuntimeType);
    }

    /// <summary>Determines whether another value has the same exact runtime-loader correlation.</summary>
    /// <param name="other">The relation to compare.</param>
    /// <returns><see langword="true"/> only when every canonical source/target fact matches.</returns>
    public bool Equals(StaticFieldRuntimeTypeResolutionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldRuntimeTypeResolutionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the exact runtime operation that selected one base-class-library module.</summary>
public sealed class StaticFieldCoreLibrarySelectionIdentity : IEquatable<StaticFieldCoreLibrarySelectionIdentity>
{
    private const string CanonicalDomain = "static-field-core-library-selection-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldCoreLibrarySelectionIdentity(
        StaticFieldCoreLibrarySelectionProvenance provenance,
        int runtimeOrdinal,
        StaticFieldMetadataModuleIdentity selectedModule)
    {
        Provenance = provenance;
        RuntimeOrdinal = runtimeOrdinal;
        SelectedModule = selectedModule;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)provenance);
        writer.WriteInt32(runtimeOrdinal);
        writer.WriteLengthPrefixedBytes(selectedModule.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact runtime selection operation.</summary>
    public StaticFieldCoreLibrarySelectionProvenance Provenance { get; }

    /// <summary>Gets the zero-based ClrRuntime ordinal in the immutable snapshot.</summary>
    public int RuntimeOrdinal { get; }

    /// <summary>Gets the exact metadata module returned by the runtime base-class-library selection.</summary>
    public StaticFieldMetadataModuleIdentity SelectedModule { get; }

    /// <summary>Gets a defensive copy of the versioned canonical selection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact detached runtime base-class-library selection observation.</summary>
    /// <param name="provenance">The defined runtime operation that supplied the module.</param>
    /// <param name="runtimeOrdinal">The non-negative runtime ordinal in the snapshot.</param>
    /// <param name="selectedModule">The exact module returned by that operation.</param>
    /// <returns>A canonical operation-specific selection identity.</returns>
    public static StaticFieldCoreLibrarySelectionIdentity Create(
        StaticFieldCoreLibrarySelectionProvenance provenance,
        int runtimeOrdinal,
        StaticFieldMetadataModuleIdentity selectedModule)
    {
        StaticFieldContractEncoding.RequireDefined(provenance, nameof(provenance));
        ArgumentOutOfRangeException.ThrowIfNegative(runtimeOrdinal);
        ArgumentNullException.ThrowIfNull(selectedModule);
        return new StaticFieldCoreLibrarySelectionIdentity(provenance, runtimeOrdinal, selectedModule);
    }

    /// <summary>Determines whether another value has the same runtime operation and selected module.</summary>
    /// <param name="other">The selection to compare.</param>
    /// <returns><see langword="true"/> only when every canonical selection fact matches.</returns>
    public bool Equals(StaticFieldCoreLibrarySelectionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldCoreLibrarySelectionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Identifies the exact loaded runtime base-class-library observation used to validate terminal roles.</summary>
public sealed class StaticFieldCoreLibraryIdentity : IEquatable<StaticFieldCoreLibraryIdentity>
{
    private const string CanonicalDomain = "static-field-core-library-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldCoreLibraryIdentity(
        StaticFieldCoreLibrarySelectionIdentity selection,
        StaticFieldMetadataModuleIdentity metadataModule,
        StaticFieldTypeDefinitionIdentity systemObjectType,
        StaticFieldTypeDefinitionIdentity systemValueType,
        StaticFieldTypeDefinitionIdentity systemEnumType,
        StaticFieldTypeAncestryEdge valueTypeBaseEdge,
        StaticFieldTypeAncestryEdge enumBaseEdge)
    {
        Selection = selection;
        MetadataModule = metadataModule;
        SystemObjectType = systemObjectType;
        SystemValueType = systemValueType;
        SystemEnumType = systemEnumType;
        ValueTypeBaseEdge = valueTypeBaseEdge;
        EnumBaseEdge = enumBaseEdge;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(selection.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemObjectType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemValueType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemEnumType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(valueTypeBaseEdge.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(enumBaseEdge.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact runtime operation that selected this module as the base class library.</summary>
    public StaticFieldCoreLibrarySelectionIdentity Selection { get; }

    /// <summary>Gets the complete runtime-selected base-class-library metadata-module relation.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact runtime-selected base-class-library module.</summary>
    public StaticFieldModuleInstanceIdentity Module => MetadataModule.Module;

    /// <summary>Gets the complete counted metadata content of the selected module.</summary>
    public ModuleContentIdentity ModuleContent => MetadataModule.ModuleContent;

    /// <summary>Gets the selected module's complete AssemblyDef identity.</summary>
    public StaticFieldAssemblyDefinitionIdentity AssemblyDefinition =>
        MetadataModule.ContainingAssembly.AssemblyDefinition;

    /// <summary>Gets the exact runtime-selected System.Object role TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity SystemObjectType { get; }

    /// <summary>Gets the exact runtime-selected System.ValueType role TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity SystemValueType { get; }

    /// <summary>Gets the exact runtime-selected System.Enum role TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity SystemEnumType { get; }

    /// <summary>Gets the exact System.ValueType-to-System.Object Extends resolution.</summary>
    public StaticFieldTypeAncestryEdge ValueTypeBaseEdge { get; }

    /// <summary>Gets the exact System.Enum-to-System.ValueType Extends resolution.</summary>
    public StaticFieldTypeAncestryEdge EnumBaseEdge { get; }

    /// <summary>Gets a defensive copy of the versioned canonical core-library observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact runtime base-class-library observation.</summary>
    /// <param name="metadataModule">The complete runtime-selected base-class-library module/manifest relation.</param>
    /// <param name="selection">The exact runtime BaseClassLibrary selection, required to name the same module.</param>
    /// <param name="systemObjectType">The exact top-level public System.Object TypeDef with nil Extends.</param>
    /// <param name="systemValueType">The exact top-level public abstract System.ValueType TypeDef.</param>
    /// <param name="systemEnumType">The exact top-level public abstract System.Enum TypeDef.</param>
    /// <param name="valueTypeBaseEdge">The exact ValueType-to-Object Extends resolution.</param>
    /// <param name="enumBaseEdge">The exact Enum-to-ValueType Extends resolution.</param>
    /// <returns>A canonical identity that well-known terminal proofs must match exactly.</returns>
    /// <exception cref="ArgumentException">The AssemblyDef is not a recognized runtime base-class library.</exception>
    public static StaticFieldCoreLibraryIdentity Create(
        StaticFieldCoreLibrarySelectionIdentity selection,
        StaticFieldMetadataModuleIdentity metadataModule,
        StaticFieldTypeDefinitionIdentity systemObjectType,
        StaticFieldTypeDefinitionIdentity systemValueType,
        StaticFieldTypeDefinitionIdentity systemEnumType,
        StaticFieldTypeAncestryEdge valueTypeBaseEdge,
        StaticFieldTypeAncestryEdge enumBaseEdge)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(metadataModule);
        ArgumentNullException.ThrowIfNull(systemObjectType);
        ArgumentNullException.ThrowIfNull(systemValueType);
        ArgumentNullException.ThrowIfNull(systemEnumType);
        ArgumentNullException.ThrowIfNull(valueTypeBaseEdge);
        ArgumentNullException.ThrowIfNull(enumBaseEdge);
        var assemblyDefinition = metadataModule.ContainingAssembly.AssemblyDefinition;
        if (!selection.SelectedModule.Equals(metadataModule) ||
            !metadataModule.IsManifestModule || !assemblyDefinition.HasRuntimeCoreLibraryName)
        {
            throw new ArgumentException(
                "The runtime base-class-library observation must carry System.Private.CoreLib or mscorlib AssemblyDef identity.",
                nameof(assemblyDefinition));
        }
        ValidateCoreRole(systemObjectType, metadataModule, "Object", mustBeAbstract: false, mustHaveBase: false);
        ValidateCoreRole(systemValueType, metadataModule, "ValueType", mustBeAbstract: true, mustHaveBase: true);
        ValidateCoreRole(systemEnumType, metadataModule, "Enum", mustBeAbstract: true, mustHaveBase: true);
        if (!valueTypeBaseEdge.SourceType.Equals(systemValueType) ||
            !valueTypeBaseEdge.ResolvedBaseType.Equals(systemObjectType) ||
            !enumBaseEdge.SourceType.Equals(systemEnumType) ||
            !enumBaseEdge.ResolvedBaseType.Equals(systemValueType))
        {
            throw new ArgumentException(
                "Core-library role edges must prove ValueType→Object and Enum→ValueType exactly.");
        }
        return new StaticFieldCoreLibraryIdentity(
            selection,
            metadataModule,
            systemObjectType,
            systemValueType,
            systemEnumType,
            valueTypeBaseEdge,
            enumBaseEdge);
    }

    /// <summary>Determines whether another observation identifies the same runtime core-library module and content.</summary>
    /// <param name="other">The observation to compare.</param>
    /// <returns><see langword="true"/> only when module, content, and AssemblyDef identity match.</returns>
    public bool Equals(StaticFieldCoreLibraryIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldCoreLibraryIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateCoreRole(
        StaticFieldTypeDefinitionIdentity type,
        StaticFieldMetadataModuleIdentity metadataModule,
        string expectedName,
        bool mustBeAbstract,
        bool mustHaveBase)
    {
        var flags = (TypeAttributes)type.TypeAttributes;
        if (!type.MetadataModule.Equals(metadataModule) || !type.IsTopLevel || type.IsInterface ||
            !string.Equals(type.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(type.TypeName, expectedName, StringComparison.Ordinal) ||
            type.GenericParameterCount != 0 ||
            ((flags & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public) ||
            type.IsAbstract != mustBeAbstract ||
            (flags & System.Reflection.TypeAttributes.Sealed) != 0 ||
            type.ExtendsMetadataToken.HasValue != mustHaveBase)
        {
            throw new ArgumentException($"The runtime core-library {expectedName} role TypeDef is structurally inconsistent.");
        }
    }
}

/// <summary>Freezes one exact TypeRef row in an ordered ResolutionScope chain.</summary>
public sealed class StaticFieldTypeReferenceRowIdentity : IEquatable<StaticFieldTypeReferenceRowIdentity>
{
    private const string CanonicalDomain = "static-field-type-reference-row-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldTypeReferenceRowIdentity(
        int typeReferenceToken,
        string namespaceName,
        string typeName,
        int resolutionScopeMetadataToken)
    {
        TypeReferenceToken = typeReferenceToken;
        NamespaceName = namespaceName;
        TypeName = typeName;
        ResolutionScopeMetadataToken = resolutionScopeMetadataToken;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(typeReferenceToken);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(resolutionScopeMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the non-nil TypeRef token.</summary>
    public int TypeReferenceToken { get; }

    /// <summary>Gets the exact decoded TypeRef namespace, including empty for a nested row.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact decoded TypeRef metadata name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact decoded Module, ModuleRef, AssemblyRef, or enclosing-TypeRef scope token.</summary>
    public int ResolutionScopeMetadataToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical TypeRef-row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact TypeRef row without resolving its scope independently.</summary>
    /// <param name="typeReferenceToken">The non-nil TypeRef token.</param>
    /// <param name="namespaceName">The exact decoded namespace, including empty for nested TypeRefs.</param>
    /// <param name="typeName">The exact decoded metadata name.</param>
    /// <param name="resolutionScopeMetadataToken">The non-nil decoded ResolutionScope token.</param>
    /// <returns>An immutable row identity for use in a closed scope chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A token is nil or uses an inadmissible table.</exception>
    public static StaticFieldTypeReferenceRowIdentity Create(
        int typeReferenceToken,
        string namespaceName,
        string typeName,
        int resolutionScopeMetadataToken)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(typeReferenceToken, 0x01, nameof(typeReferenceToken));
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }
        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x00) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x1A) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(resolutionScopeMetadataToken, 0x23))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionScopeMetadataToken),
                "A TypeRef ResolutionScope must be a non-nil Module, ModuleRef, AssemblyRef, or TypeRef token.");
        }

        return new StaticFieldTypeReferenceRowIdentity(
            typeReferenceToken,
            namespaceName,
            typeName,
            resolutionScopeMetadataToken);
    }

    /// <summary>Determines whether another value has the same complete TypeRef row.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when token, spelling, and ResolutionScope match.</returns>
    public bool Equals(StaticFieldTypeReferenceRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldTypeReferenceRowIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one bounded closed TypeRef ResolutionScope chain and its exact root row.</summary>
/// <remarks>
/// TypeRefs are ordered from the referenced row outward through zero or more enclosing TypeRefs. The final row is
/// scoped by exactly one Module, ModuleRef, or AssemblyRef root. This represents nested external bases without
/// flattening their names or assuming every TypeRef is AssemblyRef-scoped.
/// </remarks>
public sealed class StaticFieldTypeReferenceResolutionIdentity : IEquatable<StaticFieldTypeReferenceResolutionIdentity>
{
    /// <summary>Gets the maximum number of TypeRef rows retained in one nested ResolutionScope chain.</summary>
    public const int MaximumTypeReferenceChainLength = 16;

    /// <summary>Gets the maximum number of exact metadata forwarder hops retained by one resolution.</summary>
    public const int MaximumExportedTypeForwarderCount = 16;

    /// <summary>Gets the canonical deterministic-bound name for TypeRef ResolutionScope traversal.</summary>
    public const string MaximumTypeReferenceChainLengthBoundName =
        "static-field.typeref-resolution-scope.rows";

    /// <summary>Gets the canonical deterministic-bound name for ExportedType forwarder traversal.</summary>
    public const string MaximumExportedTypeForwarderCountBoundName =
        "static-field.exported-type-forwarders.count";

    private const string CanonicalDomain = "static-field-type-reference-resolution-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences;
    private readonly ImmutableArray<StaticFieldExportedTypeForwarderIdentity> exportedTypeForwarders;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldTypeReferenceResolutionIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldTypeReferenceResolutionScopeKind rootKind,
        int rootMetadataToken,
        string? moduleReferenceName,
        StaticFieldAssemblyReferenceIdentity? assemblyReference,
        StaticFieldTypeDefinitionIdentity resolvedTargetType,
        StaticFieldTypeReferenceTargetResolutionKind targetResolutionKind,
        ImmutableArray<StaticFieldExportedTypeForwarderIdentity> exportedTypeForwarders,
        StaticFieldRuntimeTypeResolutionIdentity? runtimeResolution)
    {
        SourceModule = sourceModule;
        this.typeReferences = typeReferences;
        RootKind = rootKind;
        RootMetadataToken = rootMetadataToken;
        ModuleReferenceName = moduleReferenceName;
        AssemblyReference = assemblyReference;
        ResolvedTargetType = resolvedTargetType;
        TargetResolutionKind = targetResolutionKind;
        this.exportedTypeForwarders = exportedTypeForwarders;
        RuntimeResolution = runtimeResolution;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeReferences.Length);
        foreach (var row in typeReferences)
        {
            writer.WriteLengthPrefixedBytes(row.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32((int)rootKind);
        writer.WriteInt32(rootMetadataToken);
        StaticFieldContractEncoding.WriteOptionalString(writer, moduleReferenceName);
        writer.WriteBoolean(assemblyReference is not null);
        if (assemblyReference is not null)
        {
            writer.WriteLengthPrefixedBytes(assemblyReference.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(resolvedTargetType.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)targetResolutionKind);
        writer.WriteInt32(exportedTypeForwarders.Length);
        foreach (var forwarder in exportedTypeForwarders)
        {
            writer.WriteLengthPrefixedBytes(forwarder.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(runtimeResolution is not null);
        if (runtimeResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(runtimeResolution.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets a defensive copy of TypeRef rows ordered from the referenced row to the outermost row.</summary>
    public ImmutableArray<StaticFieldTypeReferenceRowIdentity> TypeReferences =>
        StaticFieldContractEncoding.Copy(typeReferences);

    /// <summary>Gets the exact source metadata module containing every retained TypeRef/scope row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the referenced innermost/head TypeRef row.</summary>
    public StaticFieldTypeReferenceRowIdentity ReferencedType => typeReferences[0];

    /// <summary>Gets the closed root ResolutionScope kind.</summary>
    public StaticFieldTypeReferenceResolutionScopeKind RootKind { get; }

    /// <summary>Gets the exact Module, ModuleRef, or AssemblyRef root token.</summary>
    public int RootMetadataToken { get; }

    /// <summary>Gets the exact ModuleRef name only for a ModuleRef root.</summary>
    public string? ModuleReferenceName { get; }

    /// <summary>Gets the complete AssemblyRef row only for an AssemblyRef root.</summary>
    public StaticFieldAssemblyReferenceIdentity? AssemblyReference { get; }

    /// <summary>Gets the AssemblyRef token for an AssemblyRef root, otherwise null.</summary>
    public int? AssemblyReferenceToken => AssemblyReference?.AssemblyReferenceToken;

    /// <summary>Gets the exact resolved target TypeDef identity.</summary>
    public StaticFieldTypeDefinitionIdentity ResolvedTargetType { get; }

    /// <summary>Gets the evidence form closing the source scope to <see cref="ResolvedTargetType"/>.</summary>
    public StaticFieldTypeReferenceTargetResolutionKind TargetResolutionKind { get; }

    /// <summary>Gets a defensive copy of the exact metadata forwarder chain, when retained.</summary>
    public ImmutableArray<StaticFieldExportedTypeForwarderIdentity> ExportedTypeForwarders =>
        StaticFieldContractEncoding.Copy(exportedTypeForwarders);

    /// <summary>Gets the exact runtime-loader correlation only for loader-backed resolution.</summary>
    public StaticFieldRuntimeTypeResolutionIdentity? RuntimeResolution { get; }

    /// <summary>Gets the declared TypeRef ResolutionScope row-count bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeReferenceChainLengthBound =>
        new(MaximumTypeReferenceChainLengthBoundName, MaximumTypeReferenceChainLength);

    /// <summary>Gets the declared ExportedType forwarder-count bound.</summary>
    public static EvaluationDeterministicBound DeclaredExportedTypeForwarderCountBound =>
        new(MaximumExportedTypeForwarderCountBoundName, MaximumExportedTypeForwarderCount);

    /// <summary>Gets a defensive copy of the versioned canonical resolution bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a chain whose outer TypeRef is scoped to the source metadata Module row.</summary>
    /// <param name="sourceModule">The exact source metadata module.</param>
    /// <param name="typeReferences">One to sixteen exact TypeRef rows ordered inner-to-outer.</param>
    /// <param name="resolvedTargetType">The exact same-module target TypeDef.</param>
    /// <returns>A closed Module-scoped TypeRef resolution.</returns>
    public static StaticFieldTypeReferenceResolutionIdentity ForModule(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldTypeDefinitionIdentity resolvedTargetType) =>
        Create(
            sourceModule,
            typeReferences,
            StaticFieldTypeReferenceResolutionScopeKind.Module,
            0x00000001,
            null,
            null,
            resolvedTargetType,
            StaticFieldTypeReferenceTargetResolutionKind.SameModule,
            ImmutableArray<StaticFieldExportedTypeForwarderIdentity>.Empty,
            null);

    /// <summary>Creates a chain whose outer TypeRef is scoped through one exact ModuleRef row.</summary>
    /// <param name="sourceModule">The exact source metadata module containing the TypeRef/ModuleRef rows.</param>
    /// <param name="typeReferences">One to sixteen exact TypeRef rows ordered inner-to-outer.</param>
    /// <param name="moduleReferenceToken">The non-nil ModuleRef token.</param>
    /// <param name="moduleReferenceName">The exact non-empty ModuleRef name.</param>
    /// <param name="resolvedTargetType">The exact same-assembly target TypeDef.</param>
    /// <returns>A closed ModuleRef-scoped TypeRef resolution.</returns>
    public static StaticFieldTypeReferenceResolutionIdentity ForModuleReference(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        int moduleReferenceToken,
        string moduleReferenceName,
        StaticFieldTypeDefinitionIdentity resolvedTargetType)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(moduleReferenceToken, 0x1A, nameof(moduleReferenceToken));
        StaticFieldMetadataTextLimits.Validate(moduleReferenceName, nameof(moduleReferenceName), allowEmpty: false);
        return Create(
            sourceModule,
            typeReferences,
            StaticFieldTypeReferenceResolutionScopeKind.ModuleReference,
            moduleReferenceToken,
            moduleReferenceName,
            null,
            resolvedTargetType,
            StaticFieldTypeReferenceTargetResolutionKind.ModuleReference,
            ImmutableArray<StaticFieldExportedTypeForwarderIdentity>.Empty,
            null);
    }

    /// <summary>Creates a direct AssemblyRef-to-manifest-AssemblyDef target resolution.</summary>
    /// <param name="sourceModule">The exact source metadata module containing the TypeRef/AssemblyRef rows.</param>
    /// <param name="typeReferences">One to sixteen exact TypeRef rows ordered inner-to-outer.</param>
    /// <param name="assemblyReference">The complete exact AssemblyRef root row.</param>
    /// <param name="resolvedTargetType">The exact resolved target TypeDef in the matched manifest module.</param>
    /// <returns>A closed direct identity-bound AssemblyRef resolution.</returns>
    public static StaticFieldTypeReferenceResolutionIdentity ForDirectAssemblyReference(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldAssemblyReferenceIdentity assemblyReference,
        StaticFieldTypeDefinitionIdentity resolvedTargetType)
    {
        ArgumentNullException.ThrowIfNull(assemblyReference);
        return Create(
            sourceModule,
            typeReferences,
            StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference,
            assemblyReference.AssemblyReferenceToken,
            null,
            assemblyReference,
            resolvedTargetType,
            StaticFieldTypeReferenceTargetResolutionKind.DirectAssemblyReference,
            ImmutableArray<StaticFieldExportedTypeForwarderIdentity>.Empty,
            null);
    }

    /// <summary>Creates an AssemblyRef resolution closed by an exact bounded ExportedType forwarder chain.</summary>
    /// <param name="sourceModule">The source metadata module containing the TypeRef/AssemblyRef rows.</param>
    /// <param name="typeReferences">One to sixteen exact TypeRef rows ordered inner-to-outer.</param>
    /// <param name="assemblyReference">The exact outer AssemblyRef scope row.</param>
    /// <param name="exportedTypeForwarders">One to sixteen exact forwarder hops from facade to target assembly.</param>
    /// <param name="resolvedTargetType">The exact final target TypeDef.</param>
    /// <returns>A closed metadata-forwarded AssemblyRef resolution.</returns>
    public static StaticFieldTypeReferenceResolutionIdentity ForExportedTypeForwarders(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldAssemblyReferenceIdentity assemblyReference,
        ImmutableArray<StaticFieldExportedTypeForwarderIdentity> exportedTypeForwarders,
        StaticFieldTypeDefinitionIdentity resolvedTargetType)
    {
        ArgumentNullException.ThrowIfNull(assemblyReference);
        return Create(
            sourceModule,
            typeReferences,
            StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference,
            assemblyReference.AssemblyReferenceToken,
            null,
            assemblyReference,
            resolvedTargetType,
            StaticFieldTypeReferenceTargetResolutionKind.ExportedTypeForwarder,
            exportedTypeForwarders,
            null);
    }

    /// <summary>Creates an AssemblyRef resolution closed by an exact runtime-loader correlation.</summary>
    /// <param name="sourceModule">The source metadata module containing the TypeRef/AssemblyRef rows.</param>
    /// <param name="typeReferences">One to sixteen exact TypeRef rows ordered inner-to-outer.</param>
    /// <param name="assemblyReference">The exact outer AssemblyRef scope row.</param>
    /// <param name="resolvedTargetType">The exact resolved target TypeDef.</param>
    /// <param name="runtimeResolution">The independent exact loader correlation.</param>
    /// <param name="availableForwarders">Any available exact metadata forwarder chain; empty when artifacts are absent.</param>
    /// <returns>A closed loader-backed resolution that cross-checks any supplied metadata chain.</returns>
    public static StaticFieldTypeReferenceResolutionIdentity ForRuntimeLoader(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldAssemblyReferenceIdentity assemblyReference,
        StaticFieldTypeDefinitionIdentity resolvedTargetType,
        StaticFieldRuntimeTypeResolutionIdentity runtimeResolution,
        ImmutableArray<StaticFieldExportedTypeForwarderIdentity> availableForwarders = default)
    {
        ArgumentNullException.ThrowIfNull(assemblyReference);
        ArgumentNullException.ThrowIfNull(runtimeResolution);
        return Create(
            sourceModule,
            typeReferences,
            StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference,
            assemblyReference.AssemblyReferenceToken,
            null,
            assemblyReference,
            resolvedTargetType,
            StaticFieldTypeReferenceTargetResolutionKind.RuntimeLoader,
            availableForwarders.IsDefault
                ? ImmutableArray<StaticFieldExportedTypeForwarderIdentity>.Empty
                : availableForwarders,
            runtimeResolution);
    }

    /// <summary>Determines whether another value has the same ordered TypeRef chain and complete root row.</summary>
    /// <param name="other">The resolution to compare.</param>
    /// <returns><see langword="true"/> only when every canonical row and scope fact matches.</returns>
    public bool Equals(StaticFieldTypeReferenceResolutionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldTypeReferenceResolutionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldTypeReferenceResolutionIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> typeReferences,
        StaticFieldTypeReferenceResolutionScopeKind rootKind,
        int rootMetadataToken,
        string? moduleReferenceName,
        StaticFieldAssemblyReferenceIdentity? assemblyReference,
        StaticFieldTypeDefinitionIdentity resolvedTargetType,
        StaticFieldTypeReferenceTargetResolutionKind targetResolutionKind,
        ImmutableArray<StaticFieldExportedTypeForwarderIdentity> exportedTypeForwarders,
        StaticFieldRuntimeTypeResolutionIdentity? runtimeResolution)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(resolvedTargetType);
        if (typeReferences.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A TypeRef resolution requires an initialized non-empty row chain.", nameof(typeReferences));
        }
        if (typeReferences.Length > MaximumTypeReferenceChainLength)
        {
            throw new ArgumentException(
                $"A TypeRef ResolutionScope chain cannot exceed {MaximumTypeReferenceChainLength} rows.",
                nameof(typeReferences));
        }
        if (exportedTypeForwarders.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized forwarder array is required.", nameof(exportedTypeForwarders));
        }
        if (exportedTypeForwarders.Length > MaximumExportedTypeForwarderCount)
        {
            throw new ArgumentException(
                $"A resolution cannot exceed {MaximumExportedTypeForwarderCount} ExportedType forwarder hops.",
                nameof(exportedTypeForwarders));
        }
        StaticFieldContractEncoding.RequireDefined(rootKind, nameof(rootKind));
        StaticFieldContractEncoding.RequireDefined(targetResolutionKind, nameof(targetResolutionKind));
        var rows = StaticFieldContractEncoding.RequireAndCopy(typeReferences, nameof(typeReferences));
        var forwarders = StaticFieldContractEncoding.RequireAndCopy(exportedTypeForwarders, nameof(exportedTypeForwarders));
        var seenTokens = new HashSet<int>();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!seenTokens.Add(row.TypeReferenceToken))
            {
                throw new ArgumentException("A TypeRef ResolutionScope chain cannot contain a cycle.", nameof(typeReferences));
            }
            var expectedScope = index + 1 < rows.Length
                ? rows[index + 1].TypeReferenceToken
                : rootMetadataToken;
            if (row.ResolutionScopeMetadataToken != expectedScope)
            {
                throw new ArgumentException("The TypeRef ResolutionScope tokens do not form the supplied closed chain.", nameof(typeReferences));
            }
            if (index + 1 < rows.Length && row.NamespaceName.Length != 0)
            {
                throw new ArgumentException("A nested TypeRef row must retain an empty namespace.", nameof(typeReferences));
            }
        }

        if (!string.Equals(
                sourceModule.Module.SnapshotSha256,
                resolvedTargetType.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("A TypeRef resolution must remain inside one immutable snapshot.");
        }
        ValidateTypeReferenceChainAgainstTarget(rows, resolvedTargetType);

        switch (targetResolutionKind)
        {
            case StaticFieldTypeReferenceTargetResolutionKind.SameModule:
                if (rootKind != StaticFieldTypeReferenceResolutionScopeKind.Module ||
                    !sourceModule.Equals(resolvedTargetType.MetadataModule) ||
                    assemblyReference is not null || moduleReferenceName is not null ||
                    !forwarders.IsEmpty || runtimeResolution is not null)
                {
                    throw new ArgumentException("Same-module TypeRef evidence contradicts its Module root or target.");
                }
                break;
            case StaticFieldTypeReferenceTargetResolutionKind.ModuleReference:
                if (rootKind != StaticFieldTypeReferenceResolutionScopeKind.ModuleReference ||
                    !sourceModule.ContainingAssembly.Equals(resolvedTargetType.MetadataModule.ContainingAssembly) ||
                    !string.Equals(moduleReferenceName, resolvedTargetType.MetadataModule.ModuleDefinition.Name, StringComparison.Ordinal) ||
                    assemblyReference is not null || !forwarders.IsEmpty || runtimeResolution is not null)
                {
                    throw new ArgumentException("ModuleRef evidence must name an exact target module in the same containing assembly.");
                }
                break;
            case StaticFieldTypeReferenceTargetResolutionKind.DirectAssemblyReference:
                if (rootKind != StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference ||
                    assemblyReference is null || !resolvedTargetType.MetadataModule.IsManifestModule ||
                    !StaticFieldExportedTypeForwarderIdentity.AssemblyReferenceMatchesDefinition(
                        assemblyReference,
                        resolvedTargetType.MetadataModule.ContainingAssembly.AssemblyDefinition) ||
                    !forwarders.IsEmpty || runtimeResolution is not null)
                {
                    throw new ArgumentException("Direct AssemblyRef evidence does not identity-bind the target manifest AssemblyDef.");
                }
                break;
            case StaticFieldTypeReferenceTargetResolutionKind.ExportedTypeForwarder:
                if (rootKind != StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference ||
                    assemblyReference is null || forwarders.IsEmpty || runtimeResolution is not null ||
                    !resolvedTargetType.MetadataModule.IsManifestModule)
                {
                    throw new ArgumentException("Forwarded AssemblyRef evidence requires a non-empty exact metadata chain to a manifest target.");
                }
                ValidateForwarders(rows[^1], assemblyReference, forwarders, resolvedTargetType);
                break;
            case StaticFieldTypeReferenceTargetResolutionKind.RuntimeLoader:
                if (rootKind != StaticFieldTypeReferenceResolutionScopeKind.AssemblyReference ||
                    assemblyReference is null || runtimeResolution is null ||
                    !runtimeResolution.SourceModule.Equals(sourceModule) ||
                    runtimeResolution.SourceTypeReferenceToken != rows[0].TypeReferenceToken ||
                    !runtimeResolution.ResolvedTargetType.Equals(resolvedTargetType))
                {
                    throw new ArgumentException("Runtime-loader evidence does not exactly correlate this source TypeRef and target TypeDef.");
                }
                if (!forwarders.IsEmpty)
                {
                    ValidateForwarders(rows[^1], assemblyReference, forwarders, resolvedTargetType);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetResolutionKind));
        }

        return new StaticFieldTypeReferenceResolutionIdentity(
            sourceModule,
            rows,
            rootKind,
            rootMetadataToken,
            moduleReferenceName,
            assemblyReference,
            resolvedTargetType,
            targetResolutionKind,
            forwarders,
            runtimeResolution);
    }

    private static void ValidateTypeReferenceChainAgainstTarget(
        ImmutableArray<StaticFieldTypeReferenceRowIdentity> rows,
        StaticFieldTypeDefinitionIdentity target)
    {
        StaticFieldTypeDefinitionIdentity? targetNode = target;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (targetNode is null ||
                !string.Equals(row.NamespaceName, targetNode.NamespaceName, StringComparison.Ordinal) ||
                !string.Equals(row.TypeName, targetNode.TypeName, StringComparison.Ordinal))
            {
                throw new ArgumentException("The ordered TypeRef name chain disagrees with the resolved enclosing TypeDef chain.");
            }
            targetNode = targetNode.EnclosingType;
            if ((index + 1 < rows.Length) != (targetNode is not null))
            {
                throw new ArgumentException("TypeRef and resolved TypeDef nesting depths disagree.");
            }
        }
    }

    private static void ValidateForwarders(
        StaticFieldTypeReferenceRowIdentity outerTypeReference,
        StaticFieldAssemblyReferenceIdentity sourceAssemblyReference,
        ImmutableArray<StaticFieldExportedTypeForwarderIdentity> forwarders,
        StaticFieldTypeDefinitionIdentity resolvedTargetType)
    {
        var expectedAssemblyReference = sourceAssemblyReference;
        var seenRows = new HashSet<string>(StringComparer.Ordinal);
        var seenAssemblies = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < forwarders.Length; index++)
        {
            var forwarder = forwarders[index];
            var rowKey = $"{forwarder.SourceAssembly.Sha256}:{forwarder.ExportedTypeToken:x8}";
            if (!seenRows.Add(rowKey) || !seenAssemblies.Add(forwarder.SourceAssembly.Sha256) ||
                !StaticFieldExportedTypeForwarderIdentity.AssemblyReferenceMatchesDefinition(
                    expectedAssemblyReference,
                    forwarder.SourceAssembly.AssemblyDefinition) ||
                !string.Equals(forwarder.NamespaceName, outerTypeReference.NamespaceName, StringComparison.Ordinal) ||
                !string.Equals(forwarder.TypeName, outerTypeReference.TypeName, StringComparison.Ordinal))
            {
                throw new ArgumentException("The ExportedType forwarder chain disagrees with its source AssemblyRef or outer type name.");
            }
            expectedAssemblyReference = forwarder.ImplementationAssemblyReference;
            if (index + 1 < forwarders.Length &&
                !forwarder.TargetAssembly.Equals(forwarders[index + 1].SourceAssembly))
            {
                throw new ArgumentException("The ExportedType forwarder target/source assemblies are discontinuous.");
            }
        }
        if (!seenAssemblies.Add(forwarders[^1].TargetAssembly.Sha256) ||
            !forwarders[^1].TargetAssembly.Equals(resolvedTargetType.MetadataModule.ContainingAssembly))
        {
            throw new ArgumentException("The ExportedType forwarder chain is cyclic or does not end at the resolved target assembly.");
        }
    }
}

/// <summary>Freezes one exact TypeDef row, containing-assembly identity, and raw Extends projection.</summary>
/// <remarks>
/// This value intentionally does not claim that a non-interface TypeDef is a reference class. TypeAttributes can prove
/// top-level/interface shape and abstractness, but only <see cref="StaticFieldTypeAncestryIdentity"/> can distinguish a
/// reference class from a value type or enum. The optional base token is the decoded non-nil TypeDef, TypeRef, or
/// TypeSpec metadata token from the TypeDef row's Extends column.
/// </remarks>
public sealed class StaticFieldTypeDefinitionIdentity : IEquatable<StaticFieldTypeDefinitionIdentity>
{
    /// <summary>Gets the maximum exact enclosing-TypeDef depth retained for one nested definition.</summary>
    public const int MaximumEnclosingTypeDefinitionDepth = 16;

    /// <summary>Gets the canonical deterministic-bound name for enclosing-TypeDef traversal.</summary>
    public const string MaximumEnclosingTypeDefinitionDepthBoundName =
        "static-field.typedef-enclosing-depth";

    private const string CanonicalDomain = "static-field-type-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldTypeDefinitionIdentity(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int genericParameterCount,
        int introducedGenericArity,
        int? extendsMetadataToken,
        StaticFieldTypeDefinitionIdentity? enclosingType)
    {
        MetadataModule = metadataModule;
        TypeDefinitionToken = typeDefinitionToken;
        FieldListRowId = fieldListRowId;
        FieldListEndExclusiveRowId = fieldListEndExclusiveRowId;
        MethodListRowId = methodListRowId;
        MethodListEndExclusiveRowId = methodListEndExclusiveRowId;
        NamespaceName = namespaceName;
        TypeName = typeName;
        TypeAttributes = typeAttributes;
        GenericParameterCount = genericParameterCount;
        IntroducedGenericArity = introducedGenericArity;
        ExtendsMetadataToken = extendsMetadataToken;
        EnclosingType = enclosingType;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(metadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteInt32(fieldListRowId);
        writer.WriteInt32(fieldListEndExclusiveRowId);
        writer.WriteInt32(methodListRowId);
        writer.WriteInt32(methodListEndExclusiveRowId);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteInt32(typeAttributes);
        writer.WriteInt32(genericParameterCount);
        writer.WriteInt32(introducedGenericArity);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, extendsMetadataToken);
        writer.WriteBoolean(enclosingType is not null);
        if (enclosingType is not null)
        {
            writer.WriteLengthPrefixedBytes(enclosingType.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete exact metadata-module and containing-assembly relation.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets the exact physical loaded module containing the TypeDef row.</summary>
    public StaticFieldModuleInstanceIdentity Module => MetadataModule.Module;

    /// <summary>Gets the complete counted metadata identity containing the TypeDef row.</summary>
    public ModuleContentIdentity ModuleContent => MetadataModule.ModuleContent;

    /// <summary>Gets the complete containing-assembly definition identity observed for the module.</summary>
    public StaticFieldAssemblyDefinitionIdentity AssemblyDefinition =>
        MetadataModule.ContainingAssembly.AssemblyDefinition;

    /// <summary>Gets the non-nil TypeDef token.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the exact raw TypeDef.FieldList starting row ID.</summary>
    public int FieldListRowId { get; }

    /// <summary>Gets the next TypeDef.FieldList or Field-table-end row ID, exclusive.</summary>
    public int FieldListEndExclusiveRowId { get; }

    /// <summary>Gets the exact raw TypeDef.MethodList starting row ID.</summary>
    public int MethodListRowId { get; }

    /// <summary>Gets the next TypeDef.MethodList or MethodDef-table-end row ID, exclusive.</summary>
    public int MethodListEndExclusiveRowId { get; }

    /// <summary>Gets the exact decoded namespace, or empty string for the global namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact metadata type name, including a generic-arity suffix when applicable.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact raw TypeAttributes bits.</summary>
    public int TypeAttributes { get; }

    /// <summary>Gets the exact total GenericParam-row count owned by this TypeDef, including captured outer parameters.</summary>
    public int GenericParameterCount { get; }

    /// <summary>Gets the generic arity introduced by this metadata name, excluding captured outer parameters.</summary>
    public int IntroducedGenericArity { get; }

    /// <summary>Gets the total generic arity used by a GENERICINST signature; retained as a compatibility alias.</summary>
    public int GenericArity => GenericParameterCount;

    /// <summary>Gets the decoded TypeDef, TypeRef, or TypeSpec Extends token, or null for a nil coded index.</summary>
    public int? ExtendsMetadataToken { get; }

    /// <summary>Gets the complete exact enclosing TypeDef identity from NestedClass, or null when top-level.</summary>
    public StaticFieldTypeDefinitionIdentity? EnclosingType { get; }

    /// <summary>Gets the exact enclosing TypeDef token from NestedClass, or null when top-level.</summary>
    public int? EnclosingTypeDefinitionToken => EnclosingType?.TypeDefinitionToken;

    /// <summary>Gets the exact number of enclosing TypeDefs retained by this identity.</summary>
    public int EnclosingTypeDepth => EnclosingType is null ? 0 : EnclosingType.EnclosingTypeDepth + 1;

    /// <summary>Gets whether the raw attributes have interface rather than class semantics.</summary>
    public bool IsInterface => ((TypeAttributes)TypeAttributes & System.Reflection.TypeAttributes.ClassSemanticsMask) ==
                               System.Reflection.TypeAttributes.Interface;

    /// <summary>Gets whether the exact TypeAttributes mark the definition abstract.</summary>
    public bool IsAbstract => ((TypeAttributes)TypeAttributes & System.Reflection.TypeAttributes.Abstract) != 0;

    /// <summary>Gets whether the visibility encoding identifies a top-level rather than nested TypeDef.</summary>
    public bool IsTopLevel =>
        ((TypeAttributes)TypeAttributes & System.Reflection.TypeAttributes.VisibilityMask) is
            System.Reflection.TypeAttributes.Public or System.Reflection.TypeAttributes.NotPublic;

    /// <summary>Gets the declared enclosing-TypeDef traversal bound.</summary>
    public static EvaluationDeterministicBound DeclaredEnclosingTypeDefinitionDepthBound =>
        new(MaximumEnclosingTypeDefinitionDepthBoundName, MaximumEnclosingTypeDefinitionDepth);

    /// <summary>Gets whether the exact TypeDef FieldList interval directly owns one non-nil FieldDef token.</summary>
    /// <param name="fieldDefinitionToken">The FieldDef token to test.</param>
    /// <returns><see langword="true"/> only when its row lies in this exact half-open owner interval.</returns>
    public bool DirectlyDeclaresField(int fieldDefinitionToken) =>
        CanonicalReplayEncoding.IsMetadataTokenForTable(fieldDefinitionToken, 0x04) &&
        (fieldDefinitionToken & 0x00FF_FFFF) is var rowId &&
        rowId >= FieldListRowId && rowId < FieldListEndExclusiveRowId;

    /// <summary>Gets whether the exact TypeDef MethodList interval directly owns one non-nil MethodDef token.</summary>
    /// <param name="methodDefinitionToken">The MethodDef token to test.</param>
    /// <returns><see langword="true"/> only when its row lies in this exact half-open owner interval.</returns>
    public bool DirectlyDeclaresMethod(int methodDefinitionToken) =>
        CanonicalReplayEncoding.IsMetadataTokenForTable(methodDefinitionToken, 0x06) &&
        (methodDefinitionToken & 0x00FF_FFFF) is var rowId &&
        rowId >= MethodListRowId && rowId < MethodListEndExclusiveRowId;

    /// <summary>Gets the ordinal metadata full name.</summary>
    public string FullName => NamespaceName.Length == 0 ? TypeName : $"{NamespaceName}.{TypeName}";

    /// <summary>Gets a defensive copy of the versioned canonical TypeDef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact detached TypeDef fact without asserting reference/value classification.</summary>
    /// <param name="metadataModule">The complete exact loaded metadata-module/containing-assembly relation.</param>
    /// <param name="typeDefinitionToken">The non-nil TypeDef token.</param>
    /// <param name="fieldListRowId">The exact raw FieldList starting row ID.</param>
    /// <param name="fieldListEndExclusiveRowId">The next owner FieldList or Field-table-end row ID.</param>
    /// <param name="methodListRowId">The exact raw MethodList starting row ID.</param>
    /// <param name="methodListEndExclusiveRowId">The next owner MethodList or MethodDef-table-end row ID.</param>
    /// <param name="namespaceName">The decoded namespace, or empty string for global namespace.</param>
    /// <param name="typeName">The exact metadata name, including any generic-arity suffix.</param>
    /// <param name="typeAttributes">The exact raw TypeAttributes bits, including top-level or nested visibility.</param>
    /// <param name="genericParameterCount">The exact total GenericParam-row count, including captured outer parameters.</param>
    /// <param name="introducedGenericArity">The arity introduced by this name, excluding captured outer parameters.</param>
    /// <param name="extendsMetadataToken">The decoded TypeDef/TypeRef/TypeSpec Extends token, or null when nil.</param>
    /// <param name="enclosingType">The complete exact enclosing TypeDef/NestedClass chain; null for top-level.</param>
    /// <returns>An immutable content-addressed TypeDef fact suitable for an ancestry proof.</returns>
    /// <exception cref="ArgumentException">Names, visibility, interface Extends, or generic arity are inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A token is nil, in the wrong table, or arity is negative.</exception>
    public static StaticFieldTypeDefinitionIdentity Create(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        int fieldListRowId,
        int fieldListEndExclusiveRowId,
        int methodListRowId,
        int methodListEndExclusiveRowId,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int genericParameterCount,
        int introducedGenericArity,
        int? extendsMetadataToken,
        StaticFieldTypeDefinitionIdentity? enclosingType = null)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }
        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));
        ValidateOwnerRange(fieldListRowId, fieldListEndExclusiveRowId, nameof(fieldListRowId));
        ValidateOwnerRange(methodListRowId, methodListEndExclusiveRowId, nameof(methodListRowId));
        ArgumentOutOfRangeException.ThrowIfNegative(genericParameterCount);
        ArgumentOutOfRangeException.ThrowIfNegative(introducedGenericArity);
        if (introducedGenericArity > genericParameterCount)
        {
            throw new ArgumentException("Introduced generic arity cannot exceed total GenericParam count.", nameof(introducedGenericArity));
        }
        if (ParseMetadataNameArity(typeName, nameof(typeName)) != introducedGenericArity)
        {
            throw new ArgumentException(
                "A TypeDef name must have exactly one canonical terminal backtick-decimal suffix matching introduced arity.",
                nameof(typeName));
        }

        var flags = (TypeAttributes)typeAttributes;
        const int allowedTypeAttributeBits = 0x00177DBF;
        var layout = flags & System.Reflection.TypeAttributes.LayoutMask;
        if ((typeAttributes & ~allowedTypeAttributeBits) != 0 ||
            layout == System.Reflection.TypeAttributes.LayoutMask ||
            (flags & System.Reflection.TypeAttributes.RTSpecialName) != 0 &&
                (flags & System.Reflection.TypeAttributes.SpecialName) == 0)
        {
            throw new ArgumentException(
                "The TypeDef contains reserved bits, the reserved layout encoding, or RTSpecialName without SpecialName.",
                nameof(typeAttributes));
        }
        var visibility = flags & System.Reflection.TypeAttributes.VisibilityMask;
        var isTopLevel = visibility is System.Reflection.TypeAttributes.Public or System.Reflection.TypeAttributes.NotPublic;
        if (isTopLevel && enclosingType is not null)
        {
            throw new ArgumentException("A top-level TypeDef cannot retain an enclosing NestedClass relation.", nameof(enclosingType));
        }
        if (!isTopLevel && enclosingType is null)
        {
            throw new ArgumentException("A nested TypeDef must retain its exact enclosing TypeDef identity.", nameof(enclosingType));
        }
        if (isTopLevel && (genericParameterCount != introducedGenericArity))
        {
            throw new ArgumentException("A top-level TypeDef introduces all of its generic parameters.", nameof(introducedGenericArity));
        }
        if (enclosingType is not null)
        {
            if (!enclosingType.MetadataModule.Equals(metadataModule) ||
                enclosingType.TypeDefinitionToken == typeDefinitionToken)
            {
                throw new ArgumentException("A nested TypeDef must have a distinct enclosing row in the same exact metadata module.", nameof(enclosingType));
            }
            if (enclosingType.EnclosingTypeDepth >= MaximumEnclosingTypeDefinitionDepth)
            {
                throw new ArgumentException(
                    $"An enclosing TypeDef chain cannot exceed {MaximumEnclosingTypeDefinitionDepth} rows.",
                    nameof(enclosingType));
            }
            if (genericParameterCount - enclosingType.GenericParameterCount != introducedGenericArity)
            {
                throw new ArgumentException(
                    "Nested total/introduced generic arity must equal captured outer parameters plus introduced parameters.",
                    nameof(genericParameterCount));
            }
            var seenEnclosingRows = new HashSet<string>(StringComparer.Ordinal);
            for (var current = enclosingType; current is not null; current = current.EnclosingType)
            {
                var physicalKey = PhysicalTypeDefinitionKey(current);
                if (!seenEnclosingRows.Add(physicalKey) ||
                    current.MetadataModule.Equals(metadataModule) &&
                    current.TypeDefinitionToken == typeDefinitionToken)
                {
                    throw new ArgumentException(
                        "An enclosing TypeDef chain cannot repeat one physical metadata row.",
                        nameof(enclosingType));
                }
            }
        }
        var semantics = flags & System.Reflection.TypeAttributes.ClassSemanticsMask;
        if (semantics is not (System.Reflection.TypeAttributes.Class or System.Reflection.TypeAttributes.Interface))
        {
            throw new ArgumentException("The TypeDef has invalid class-semantics bits.", nameof(typeAttributes));
        }

        if (extendsMetadataToken.HasValue &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x02) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(extendsMetadataToken.Value, 0x1B))
        {
            throw new ArgumentOutOfRangeException(
                nameof(extendsMetadataToken),
                "Extends must decode to a non-nil TypeDef, TypeRef, or TypeSpec token.");
        }
        if (semantics == System.Reflection.TypeAttributes.Interface && extendsMetadataToken.HasValue)
        {
            throw new ArgumentException("An interface TypeDef must retain a nil Extends coded index.", nameof(extendsMetadataToken));
        }
        if (semantics == System.Reflection.TypeAttributes.Interface &&
            (flags & System.Reflection.TypeAttributes.Abstract) == 0)
        {
            throw new ArgumentException("An interface TypeDef must retain its required Abstract attribute.", nameof(typeAttributes));
        }
        if (semantics == System.Reflection.TypeAttributes.Interface &&
            (flags & System.Reflection.TypeAttributes.Sealed) != 0)
        {
            throw new ArgumentException("An interface TypeDef cannot retain the Sealed attribute.", nameof(typeAttributes));
        }

        return new StaticFieldTypeDefinitionIdentity(
            metadataModule,
            typeDefinitionToken,
            fieldListRowId,
            fieldListEndExclusiveRowId,
            methodListRowId,
            methodListEndExclusiveRowId,
            namespaceName,
            typeName,
            typeAttributes,
            genericParameterCount,
            introducedGenericArity,
            extendsMetadataToken,
            enclosingType);
    }

    /// <summary>Determines whether another value represents the same exact TypeDef row and Extends projection.</summary>
    /// <param name="other">The TypeDef fact to compare.</param>
    /// <returns><see langword="true"/> only when all canonical fields match.</returns>
    public bool Equals(StaticFieldTypeDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldTypeDefinitionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static string PhysicalTypeDefinitionKey(StaticFieldTypeDefinitionIdentity type) =>
        $"{type.MetadataModule.Sha256}:{type.TypeDefinitionToken:x8}";

    private static int ParseMetadataNameArity(string typeName, string parameterName)
    {
        var separator = typeName.LastIndexOf('`');
        if (separator < 0)
        {
            return 0;
        }
        if (separator == 0 || separator == typeName.Length - 1 ||
            typeName.AsSpan(0, separator).Contains('`'))
        {
            throw new ArgumentException("A metadata arity suffix must be one terminal backtick followed by decimal digits.", parameterName);
        }

        var digits = typeName.AsSpan(separator + 1);
        if (digits[0] == '0')
        {
            throw new ArgumentException("A metadata arity suffix must be a positive canonical decimal integer.", parameterName);
        }
        var arity = 0;
        foreach (var character in digits)
        {
            if (character is < '0' or > '9' ||
                arity > (int.MaxValue - (character - '0')) / 10)
            {
                throw new ArgumentException("A metadata arity suffix is malformed or exceeds Int32.", parameterName);
            }
            arity = (arity * 10) + (character - '0');
        }
        return arity;
    }

    private static void ValidateOwnerRange(int startRowId, int endExclusiveRowId, string parameterName)
    {
        const int maximumEndExclusiveRowId = 0x01000000;
        if (startRowId <= 0 || startRowId > maximumEndExclusiveRowId ||
            endExclusiveRowId < startRowId || endExclusiveRowId > maximumEndExclusiveRowId)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "TypeDef FieldList/MethodList ranges must be non-empty row indices or the one-past-table sentinel.");
        }
    }
}

/// <summary>Freezes one resolved edge in an exact TypeDef Extends chain.</summary>
/// <remarks>
/// A direct TypeDef edge resolves to the same metadata image. A TypeRef edge retains its complete bounded
/// Module/ModuleRef/AssemblyRef/enclosing-TypeRef ResolutionScope chain. A TypeSpec edge retains a bounded canonical
/// GENERICINST CLASS signature, the TypeDef/TypeRef generic-definition token decoded from it, and the same complete
/// TypeRef resolution when that inner token is a TypeRef. No module-order or simple-name search is represented.
/// </remarks>
public sealed class StaticFieldTypeAncestryEdge : IEquatable<StaticFieldTypeAncestryEdge>
{
    /// <summary>Gets the maximum exact TypeSpec signature length admitted by the bounded ancestry profile.</summary>
    public const int MaximumTypeSpecificationSignatureLength = 256;

    /// <summary>Gets the maximum nested type depth admitted while validating one TypeSpec signature.</summary>
    public const int MaximumTypeSpecificationDepth = 32;

    /// <summary>Gets the maximum aggregate generic argument count admitted by one TypeSpec signature.</summary>
    public const int MaximumTypeSpecificationGenericArgumentCount = 64;

    /// <summary>Gets the stable deterministic-bound name for TypeSpec signature bytes.</summary>
    public const string MaximumTypeSpecificationSignatureLengthBoundName =
        "static-field.typespec-signature.bytes";

    /// <summary>Gets the stable deterministic-bound name for recursive TypeSpec parsing depth.</summary>
    public const string MaximumTypeSpecificationDepthBoundName =
        "static-field.typespec-signature.depth";

    /// <summary>Gets the stable deterministic-bound name for aggregate TypeSpec generic arguments.</summary>
    public const string MaximumTypeSpecificationGenericArgumentCountBoundName =
        "static-field.typespec-signature.generic-arguments";

    private const string CanonicalDomain = "static-field-type-ancestry-edge";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> typeSpecificationSignature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldTypeAncestryEdge(
        StaticFieldTypeDefinitionIdentity sourceType,
        StaticFieldTypeDefinitionIdentity resolvedBaseType,
        StaticFieldTypeBaseReferenceKind kind,
        int referencedTypeMetadataToken,
        StaticFieldTypeReferenceResolutionIdentity? typeReferenceResolution,
        ImmutableArray<byte> typeSpecificationSignature,
        int genericArgumentCount,
        int aggregateGenericArgumentCount,
        int maximumTypeSpecificationDepthObserved)
    {
        SourceType = sourceType;
        ResolvedBaseType = resolvedBaseType;
        Kind = kind;
        ReferencedTypeMetadataToken = referencedTypeMetadataToken;
        TypeReferenceResolution = typeReferenceResolution;
        this.typeSpecificationSignature = typeSpecificationSignature;
        GenericArgumentCount = genericArgumentCount;
        AggregateGenericArgumentCount = aggregateGenericArgumentCount;
        MaximumTypeSpecificationDepthObserved = maximumTypeSpecificationDepthObserved;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(resolvedBaseType.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)kind);
        writer.WriteInt32(referencedTypeMetadataToken);
        writer.WriteBoolean(typeReferenceResolution is not null);
        if (typeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(typeReferenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(typeSpecificationSignature.AsSpan());
        writer.WriteInt32(genericArgumentCount);
        writer.WriteInt32(aggregateGenericArgumentCount);
        writer.WriteInt32(maximumTypeSpecificationDepthObserved);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source TypeDef whose Extends column supplied this edge.</summary>
    public StaticFieldTypeDefinitionIdentity SourceType { get; }

    /// <summary>Gets the exact resolved target TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity ResolvedBaseType { get; }

    /// <summary>Gets the TypeDef, TypeRef, or TypeSpec source representation derived from the Extends token table.</summary>
    public StaticFieldTypeBaseReferenceKind Kind { get; }

    /// <summary>
    /// Gets the direct Extends TypeDef/TypeRef token, or the generic-definition TypeDef/TypeRef token decoded from a
    /// TypeSpec signature.
    /// </summary>
    public int ReferencedTypeMetadataToken { get; }

    /// <summary>Gets the complete TypeRef ResolutionScope chain when a TypeRef is referenced, otherwise null.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? TypeReferenceResolution { get; }

    /// <summary>Gets the exact AssemblyRef root token when the TypeRef chain is AssemblyRef-scoped, otherwise null.</summary>
    public int? AssemblyReferenceToken => TypeReferenceResolution?.AssemblyReferenceToken;

    /// <summary>Gets the decoded TypeRef namespace when <see cref="ReferencedTypeMetadataToken"/> is a TypeRef.</summary>
    public string? ReferencedNamespaceName => TypeReferenceResolution?.ReferencedType.NamespaceName;

    /// <summary>Gets the decoded TypeRef metadata name when <see cref="ReferencedTypeMetadataToken"/> is a TypeRef.</summary>
    public string? ReferencedTypeName => TypeReferenceResolution?.ReferencedType.TypeName;

    /// <summary>Gets a defensive copy of the exact bounded TypeSpec signature, or an initialized empty array otherwise.</summary>
    public ImmutableArray<byte> TypeSpecificationSignature => CanonicalReplayEncoding.Copy(typeSpecificationSignature);

    /// <summary>Gets the TypeSpec generic argument count, or zero for direct TypeDef/TypeRef edges.</summary>
    public int GenericArgumentCount { get; }

    /// <summary>Gets the aggregate generic-argument count across the complete TypeSpec grammar tree.</summary>
    public int AggregateGenericArgumentCount { get; }

    /// <summary>Gets the maximum recursive type depth observed by the shared TypeSpec reader.</summary>
    public int MaximumTypeSpecificationDepthObserved { get; }

    /// <summary>Gets the declared TypeSpec-signature byte bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeSpecificationSignatureLengthBound =>
        new(MaximumTypeSpecificationSignatureLengthBoundName, MaximumTypeSpecificationSignatureLength);

    /// <summary>Gets the declared TypeSpec recursive-depth bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeSpecificationDepthBound =>
        new(MaximumTypeSpecificationDepthBoundName, MaximumTypeSpecificationDepth);

    /// <summary>Gets the declared TypeSpec aggregate-generic-argument bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeSpecificationGenericArgumentCountBound =>
        new(MaximumTypeSpecificationGenericArgumentCountBoundName, MaximumTypeSpecificationGenericArgumentCount);

    /// <summary>Gets a defensive copy of the versioned canonical edge bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact resolved Extends edge, deriving its source representation from the source TypeDef.</summary>
    /// <param name="sourceType">The TypeDef whose non-nil Extends coded index is being resolved.</param>
    /// <param name="resolvedBaseType">The exact resolved base TypeDef.</param>
    /// <param name="typeReferenceResolution">Complete TypeRef scope chain when the edge directly or indirectly uses TypeRef.</param>
    /// <param name="typeSpecificationSignature">
    /// Exact canonical TypeSpec signature for a TypeSpec edge; omit for direct TypeDef/TypeRef edges.
    /// </param>
    /// <returns>An immutable edge retaining source representation and resolved target identity.</returns>
    /// <exception cref="ArgumentException">The edge is cross-domain, self-contradictory, non-canonical, or misresolved.</exception>
    public static StaticFieldTypeAncestryEdge Create(
        StaticFieldTypeDefinitionIdentity sourceType,
        StaticFieldTypeDefinitionIdentity resolvedBaseType,
        StaticFieldTypeReferenceResolutionIdentity? typeReferenceResolution = null,
        ImmutableArray<byte> typeSpecificationSignature = default)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(resolvedBaseType);
        if (sourceType.IsInterface || resolvedBaseType.IsInterface)
        {
            throw new ArgumentException("An Extends ancestry edge cannot use an interface TypeDef.");
        }
        if (!string.Equals(
                sourceType.Module.SnapshotSha256,
                resolvedBaseType.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("An ancestry edge must resolve inside one immutable snapshot.");
        }
        if (!sourceType.ExtendsMetadataToken.HasValue)
        {
            throw new ArgumentException("The source TypeDef has a nil Extends coded index.", nameof(sourceType));
        }

        var extendsToken = sourceType.ExtendsMetadataToken.Value;
        StaticFieldTypeBaseReferenceKind kind;
        int referencedToken;
        int genericArgumentCount = 0;
        int aggregateGenericArgumentCount = 0;
        int maximumTypeSpecificationDepthObserved = 0;
        if (!typeSpecificationSignature.IsDefault &&
            typeSpecificationSignature.Length > MaximumTypeSpecificationSignatureLength)
        {
            throw new ArgumentException(
                $"A TypeSpec signature cannot exceed {MaximumTypeSpecificationSignatureLength} bytes.",
                nameof(typeSpecificationSignature));
        }
        var normalizedSignature = typeSpecificationSignature.IsDefault
            ? ImmutableArray<byte>.Empty
            : CanonicalReplayEncoding.Copy(typeSpecificationSignature);
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(extendsToken, 0x02))
        {
            kind = StaticFieldTypeBaseReferenceKind.TypeDefinition;
            referencedToken = extendsToken;
            if (!normalizedSignature.IsEmpty || typeReferenceResolution is not null ||
                !sourceType.MetadataModule.Equals(resolvedBaseType.MetadataModule) ||
                referencedToken != resolvedBaseType.TypeDefinitionToken)
            {
                throw new ArgumentException("A direct TypeDef Extends edge must resolve to itself in the source metadata.");
            }
        }
        else if (CanonicalReplayEncoding.IsMetadataTokenForTable(extendsToken, 0x01))
        {
            kind = StaticFieldTypeBaseReferenceKind.TypeReference;
            referencedToken = extendsToken;
            ValidateTypeReferenceResolution(
                sourceType,
                referencedToken,
                typeReferenceResolution,
                resolvedBaseType,
                normalizedSignature,
                nameof(typeReferenceResolution));
        }
        else
        {
            kind = StaticFieldTypeBaseReferenceKind.TypeSpecification;
            if (!BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                    normalizedSignature.AsSpan(),
                    MaximumTypeSpecificationSignatureLength,
                    MaximumTypeSpecificationDepth,
                    MaximumTypeSpecificationGenericArgumentCount,
                    out var projection))
            {
                throw new ArgumentException(
                    "A TypeSpec Extends edge requires one complete canonical bounded GENERICINST CLASS signature.",
                    nameof(typeSpecificationSignature));
            }
            referencedToken = projection.GenericHeadMetadataToken;
            genericArgumentCount = projection.GenericArgumentCount;
            aggregateGenericArgumentCount = projection.AggregateGenericArgumentCount;
            maximumTypeSpecificationDepthObserved = projection.MaximumObservedDepth;
            if (resolvedBaseType.GenericArity != genericArgumentCount)
            {
                throw new ArgumentException(
                    "The TypeSpec argument count disagrees with the resolved generic TypeDef arity.",
                    nameof(typeSpecificationSignature));
            }

            if (CanonicalReplayEncoding.IsMetadataTokenForTable(referencedToken, 0x02))
            {
                if (typeReferenceResolution is not null ||
                    !sourceType.MetadataModule.Equals(resolvedBaseType.MetadataModule) ||
                    referencedToken != resolvedBaseType.TypeDefinitionToken)
                {
                    throw new ArgumentException(
                        "A TypeSpec whose generic head is a TypeDef must resolve inside the exact source metadata.");
                }
            }
            else if (CanonicalReplayEncoding.IsMetadataTokenForTable(referencedToken, 0x01))
            {
                ValidateTypeReferenceResolution(
                    sourceType,
                    referencedToken,
                    typeReferenceResolution,
                    resolvedBaseType,
                    ImmutableArray<byte>.Empty,
                    nameof(typeReferenceResolution));
            }
            else
            {
                throw new ArgumentException("The bounded TypeSpec generic head must be a TypeDef or TypeRef token.");
            }
        }

        return new StaticFieldTypeAncestryEdge(
            sourceType,
            resolvedBaseType,
            kind,
            referencedToken,
            typeReferenceResolution,
            normalizedSignature,
            genericArgumentCount,
            aggregateGenericArgumentCount,
            maximumTypeSpecificationDepthObserved);
    }

    /// <summary>Determines whether another edge carries the same source, resolution, and signature evidence.</summary>
    /// <param name="other">The edge to compare.</param>
    /// <returns><see langword="true"/> only when all canonical evidence matches.</returns>
    public bool Equals(StaticFieldTypeAncestryEdge? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldTypeAncestryEdge);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateTypeReferenceResolution(
        StaticFieldTypeDefinitionIdentity sourceType,
        int referencedTypeToken,
        StaticFieldTypeReferenceResolutionIdentity? typeReferenceResolution,
        StaticFieldTypeDefinitionIdentity resolvedBaseType,
        ImmutableArray<byte> forbiddenSignature,
        string parameterName)
    {
        if (!forbiddenSignature.IsEmpty || typeReferenceResolution is null)
        {
            throw new ArgumentException(
                "A TypeRef edge requires one complete closed ResolutionScope chain and no direct TypeSpec payload.",
                parameterName);
        }
        var referencedType = typeReferenceResolution.ReferencedType;
        if (referencedType.TypeReferenceToken != referencedTypeToken ||
            !typeReferenceResolution.SourceModule.Equals(sourceType.MetadataModule) ||
            !typeReferenceResolution.ResolvedTargetType.Equals(resolvedBaseType) ||
            typeReferenceResolution.TargetResolutionKind == StaticFieldTypeReferenceTargetResolutionKind.RuntimeLoader &&
                (typeReferenceResolution.RuntimeResolution?.Operation !=
                    StaticFieldRuntimeTypeResolutionOperation.AncestryBaseType ||
                 !typeReferenceResolution.RuntimeResolution.SourceRuntimeType.TypeDefinition.Equals(sourceType)))
        {
            throw new ArgumentException(
                "The complete TypeRef source/target resolution disagrees with the ancestry edge.",
                parameterName);
        }
    }
}

/// <summary>Proves exact reference-class, value-type, enum, or interface shape from contiguous metadata ancestry.</summary>
/// <remarks>
/// Non-interface chains stop at the first classification boundary: exact System.Object for reference classes,
/// System.ValueType for value types, or System.Enum for enums. A boundary may not appear as an intermediate node.
/// This preserves a small proof while preventing a value type from being relabeled as a reference class by continuing
/// through ValueType to Object. Interfaces are represented separately and require a nil Extends column.
/// </remarks>
public sealed class StaticFieldTypeAncestryIdentity : IEquatable<StaticFieldTypeAncestryIdentity>
{
    /// <summary>Gets the maximum number of contiguous Extends edges retained by one ancestry proof.</summary>
    public const int MaximumAncestryEdgeCount = 64;

    /// <summary>Gets the stable deterministic-bound name used when acquisition reaches the ancestry edge cap.</summary>
    public const string MaximumAncestryEdgeCountBoundName = "static-field.type-ancestry-edge-count";

    private const string CanonicalDomain = "static-field-type-ancestry-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldTypeAncestryEdge> edges;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldTypeAncestryIdentity(
        StaticFieldTypeDefinitionIdentity subjectType,
        ImmutableArray<StaticFieldTypeAncestryEdge> edges,
        StaticFieldTypeDefinitionIdentity terminalType,
        StaticFieldTypeClassification classification,
        StaticFieldCoreLibraryIdentity? coreLibrary)
    {
        SubjectType = subjectType;
        this.edges = edges;
        TerminalType = terminalType;
        Classification = classification;
        CoreLibrary = coreLibrary;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(subjectType.CanonicalBytes.AsSpan());
        writer.WriteInt32(edges.Length);
        foreach (var edge in edges)
        {
            writer.WriteLengthPrefixedBytes(edge.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(terminalType.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)classification);
        writer.WriteBoolean(coreLibrary is not null);
        if (coreLibrary is not null)
        {
            writer.WriteLengthPrefixedBytes(coreLibrary.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact TypeDef being classified.</summary>
    public StaticFieldTypeDefinitionIdentity SubjectType { get; }

    /// <summary>Gets a defensive copy of the exact ordered contiguous Extends edges.</summary>
    public ImmutableArray<StaticFieldTypeAncestryEdge> Edges => StaticFieldContractEncoding.Copy(edges);

    /// <summary>Gets the exact System.Object, System.ValueType, System.Enum, or interface terminal TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity TerminalType { get; }

    /// <summary>Gets the classification derived by <see cref="Create"/>; no caller supplies this value.</summary>
    public StaticFieldTypeClassification Classification { get; }

    /// <summary>Gets the exact runtime core-library observation validating a non-interface terminal.</summary>
    public StaticFieldCoreLibraryIdentity? CoreLibrary { get; }

    /// <summary>Gets whether the derived classification is a reference class.</summary>
    public bool IsReferenceClass => Classification == StaticFieldTypeClassification.ReferenceClass;

    /// <summary>Gets the declared deterministic edge-count bound that an adapter must report when reached.</summary>
    public static EvaluationDeterministicBound DeclaredEdgeCountBound =>
        new(MaximumAncestryEdgeCountBoundName, MaximumAncestryEdgeCount);

    /// <summary>Gets a defensive copy of the versioned canonical proof bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact ancestry proof and derives its classification from its terminal TypeDef.</summary>
    /// <param name="subjectType">The exact TypeDef being classified.</param>
    /// <param name="edges">The explicitly initialized ordered Extends edge chain, possibly empty at a terminal/interface.</param>
    /// <param name="coreLibrary">The runtime core-library identity for a non-interface proof; null only for an interface.</param>
    /// <returns>A canonical immutable proof with derived classification.</returns>
    /// <exception cref="ArgumentException">The chain is missing, discontinuous, cyclic, cross-domain, or has a false terminal.</exception>
    public static StaticFieldTypeAncestryIdentity Create(
        StaticFieldTypeDefinitionIdentity subjectType,
        ImmutableArray<StaticFieldTypeAncestryEdge> edges,
        StaticFieldCoreLibraryIdentity? coreLibrary)
    {
        ArgumentNullException.ThrowIfNull(subjectType);
        if (edges.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized ancestry edge array is required.", nameof(edges));
        }
        if (edges.Length > MaximumAncestryEdgeCount)
        {
            throw new ArgumentException(
                $"An ancestry proof cannot exceed {MaximumAncestryEdgeCount} Extends edges.",
                nameof(edges));
        }
        var normalizedEdges = StaticFieldContractEncoding.RequireAndCopy(edges, nameof(edges));
        if (subjectType.IsInterface)
        {
            if (!normalizedEdges.IsEmpty || subjectType.ExtendsMetadataToken.HasValue || coreLibrary is not null)
            {
                throw new ArgumentException(
                    "An interface ancestry proof must be an empty nil-Extends chain without a core-library terminal.",
                    nameof(edges));
            }
            return new StaticFieldTypeAncestryIdentity(
                subjectType,
                normalizedEdges,
                subjectType,
                StaticFieldTypeClassification.Interface,
                coreLibrary: null);
        }

        ArgumentNullException.ThrowIfNull(coreLibrary);

        var expectedSource = subjectType;
        var visited = new HashSet<string>(StringComparer.Ordinal)
        {
            StaticFieldTypeDefinitionIdentity.PhysicalTypeDefinitionKey(subjectType),
        };
        for (var index = 0; index < normalizedEdges.Length; index++)
        {
            var edge = normalizedEdges[index];
            if (!edge.SourceType.Equals(expectedSource))
            {
                throw new ArgumentException("The ancestry edges are not an exact contiguous ordered chain.", nameof(edges));
            }
            if (IsClassificationTerminal(expectedSource, coreLibrary, out _) ||
                !visited.Add(StaticFieldTypeDefinitionIdentity.PhysicalTypeDefinitionKey(edge.ResolvedBaseType)))
            {
                throw new ArgumentException("An ancestry chain cannot continue past a terminal or contain a cycle.", nameof(edges));
            }
            expectedSource = edge.ResolvedBaseType;
        }

        var terminal = expectedSource;
        if (!IsClassificationTerminal(terminal, coreLibrary, out var classification))
        {
            throw new ArgumentException(
                "A non-interface ancestry proof must terminate at exact System.Object, System.ValueType, or System.Enum.",
                nameof(edges));
        }
        if (classification == StaticFieldTypeClassification.ReferenceClass && terminal.ExtendsMetadataToken.HasValue)
        {
            throw new ArgumentException("The System.Object terminal must retain a nil Extends coded index.", nameof(edges));
        }
        var terminalFlags = (TypeAttributes)terminal.TypeAttributes;
        var terminalMustBeAbstract = classification is
            StaticFieldTypeClassification.ValueType or StaticFieldTypeClassification.Enum;
        if ((terminalFlags & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public ||
            ((terminalFlags & System.Reflection.TypeAttributes.Abstract) != 0) != terminalMustBeAbstract ||
            (terminalFlags & System.Reflection.TypeAttributes.Sealed) != 0)
        {
            throw new ArgumentException(
                "A well-known terminal must retain exact public Object/abstract ValueType/abstract Enum role attributes.",
                nameof(edges));
        }
        var validatedTerminal = classification switch
        {
            StaticFieldTypeClassification.ReferenceClass => coreLibrary.SystemObjectType,
            StaticFieldTypeClassification.ValueType => coreLibrary.SystemValueType,
            StaticFieldTypeClassification.Enum => coreLibrary.SystemEnumType,
            _ => throw new ArgumentOutOfRangeException(nameof(classification)),
        };
        if (!terminal.IsTopLevel || !terminal.Equals(validatedTerminal))
        {
            throw new ArgumentException(
                "A well-known terminal must equal the runtime-selected core-library role TypeDef.",
                nameof(coreLibrary));
        }
        if (!subjectType.Equals(terminal) &&
            classification is StaticFieldTypeClassification.ValueType or StaticFieldTypeClassification.Enum)
        {
            var subjectFlags = (TypeAttributes)subjectType.TypeAttributes;
            if ((subjectFlags & System.Reflection.TypeAttributes.Sealed) == 0 ||
                (subjectFlags & System.Reflection.TypeAttributes.Abstract) != 0)
            {
                throw new ArgumentException(
                    "A concrete value-type or enum subject must retain sealed, non-abstract role attributes.",
                    nameof(subjectType));
            }
        }
        if (normalizedEdges.IsEmpty && !subjectType.Equals(terminal))
        {
            throw new ArgumentException("An empty ancestry proof is valid only for its terminal subject.", nameof(edges));
        }
        if (!normalizedEdges.IsEmpty && !subjectType.ExtendsMetadataToken.HasValue)
        {
            throw new ArgumentException("A non-empty ancestry proof requires a non-nil subject Extends token.", nameof(subjectType));
        }

        return new StaticFieldTypeAncestryIdentity(subjectType, normalizedEdges, terminal, classification, coreLibrary);
    }

    /// <summary>Determines whether another ancestry proof has identical subject, edge order, terminal, and classification.</summary>
    /// <param name="other">The ancestry proof to compare.</param>
    /// <returns><see langword="true"/> only when all canonical proof bytes match.</returns>
    public bool Equals(StaticFieldTypeAncestryIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldTypeAncestryIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static bool IsClassificationTerminal(
        StaticFieldTypeDefinitionIdentity type,
        StaticFieldCoreLibraryIdentity coreLibrary,
        out StaticFieldTypeClassification classification)
    {
        classification = default;
        if (type.IsInterface || type.GenericArity != 0)
        {
            return false;
        }

        classification = type.Equals(coreLibrary.SystemObjectType)
            ? StaticFieldTypeClassification.ReferenceClass
            : type.Equals(coreLibrary.SystemValueType)
                ? StaticFieldTypeClassification.ValueType
                : type.Equals(coreLibrary.SystemEnumType)
                    ? StaticFieldTypeClassification.Enum
                    : default;
        return classification != default;
    }
}

/// <summary>Freezes one exact InterfaceImpl row without claiming whole-program assignability.</summary>
/// <remarks>
/// The row retains its implementing TypeDef and the complete resolved interface identity. Consumers may compose a
/// bounded closure from these rows and class ancestry, but this primitive by itself proves only the one metadata row.
/// </remarks>
public sealed class StaticFieldInterfaceImplementationRowIdentity :
    IEquatable<StaticFieldInterfaceImplementationRowIdentity>
{
    private const string CanonicalDomain = "static-field-interface-implementation-row-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> interfaceTypeSpecificationSignature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldInterfaceImplementationRowIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int interfaceImplementationToken,
        StaticFieldTypeDefinitionIdentity implementingType,
        int interfaceTypeMetadataToken,
        ImmutableArray<byte> interfaceTypeSpecificationSignature,
        StaticFieldTypeReferenceResolutionIdentity? interfaceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity resolvedInterfaceTypeAncestry)
    {
        SourceModule = sourceModule;
        InterfaceImplementationToken = interfaceImplementationToken;
        ImplementingType = implementingType;
        InterfaceTypeMetadataToken = interfaceTypeMetadataToken;
        this.interfaceTypeSpecificationSignature = interfaceTypeSpecificationSignature;
        InterfaceTypeReferenceResolution = interfaceTypeReferenceResolution;
        ResolvedInterfaceTypeAncestry = resolvedInterfaceTypeAncestry;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(interfaceImplementationToken);
        writer.WriteLengthPrefixedBytes(implementingType.CanonicalBytes.AsSpan());
        writer.WriteInt32(interfaceTypeMetadataToken);
        writer.WriteLengthPrefixedBytes(interfaceTypeSpecificationSignature.AsSpan());
        writer.WriteBoolean(interfaceTypeReferenceResolution is not null);
        if (interfaceTypeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(interfaceTypeReferenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(resolvedInterfaceTypeAncestry.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact metadata module containing the InterfaceImpl row and implementing TypeDef.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the non-nil InterfaceImpl metadata token.</summary>
    public int InterfaceImplementationToken { get; }

    /// <summary>Gets the exact TypeDef named by the InterfaceImpl.Class column.</summary>
    public StaticFieldTypeDefinitionIdentity ImplementingType { get; }

    /// <summary>Gets the exact non-nil TypeDef, TypeRef, or TypeSpec token named by the InterfaceImpl.Interface column.</summary>
    public int InterfaceTypeMetadataToken { get; }

    /// <summary>Gets the exact bounded GENERICINST CLASS signature only for a TypeSpec Interface token.</summary>
    public ImmutableArray<byte> InterfaceTypeSpecificationSignature =>
        CanonicalReplayEncoding.Copy(interfaceTypeSpecificationSignature);

    /// <summary>
    /// Gets the complete source TypeRef resolution when the Interface token is a TypeRef, or when a TypeSpec signature
    /// has a TypeRef generic head; otherwise <see langword="null"/> for a same-module TypeDef target or TypeDef head.
    /// </summary>
    public StaticFieldTypeReferenceResolutionIdentity? InterfaceTypeReferenceResolution { get; }

    /// <summary>Gets exact interface ancestry for the resolved Interface TypeDef.</summary>
    public StaticFieldTypeAncestryIdentity ResolvedInterfaceTypeAncestry { get; }

    /// <summary>Gets the exact resolved Interface TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity ResolvedInterfaceType => ResolvedInterfaceTypeAncestry.SubjectType;

    /// <summary>Gets a defensive copy of the versioned canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact directly owned InterfaceImpl row and closed interface-token resolution.</summary>
    /// <param name="sourceModule">The exact metadata module containing the InterfaceImpl and implementing TypeDef rows.</param>
    /// <param name="interfaceImplementationToken">The non-nil InterfaceImpl row token.</param>
    /// <param name="implementingType">The exact TypeDef named by InterfaceImpl.Class.</param>
    /// <param name="interfaceTypeMetadataToken">The exact TypeDef or TypeRef token named by InterfaceImpl.Interface.</param>
    /// <param name="interfaceTypeReferenceResolution">Complete resolution exactly for a TypeRef interface token.</param>
    /// <param name="resolvedInterfaceTypeAncestry">Exact ancestry proving that the resolved target is an interface.</param>
    /// <returns>A detached immutable row primitive suitable for later bounded closure composition.</returns>
    public static StaticFieldInterfaceImplementationRowIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int interfaceImplementationToken,
        StaticFieldTypeDefinitionIdentity implementingType,
        int interfaceTypeMetadataToken,
        StaticFieldTypeReferenceResolutionIdentity? interfaceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity resolvedInterfaceTypeAncestry)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(implementingType);
        ArgumentNullException.ThrowIfNull(resolvedInterfaceTypeAncestry);
        CanonicalReplayEncoding.ValidateMetadataToken(
            interfaceImplementationToken,
            0x09,
            nameof(interfaceImplementationToken));
        if (!implementingType.MetadataModule.Equals(sourceModule))
        {
            throw new ArgumentException(
                "InterfaceImpl.Class must be an exact TypeDef in the row's source module.",
                nameof(implementingType));
        }

        var resolvedInterface = resolvedInterfaceTypeAncestry.SubjectType;
        if (resolvedInterfaceTypeAncestry.Classification != StaticFieldTypeClassification.Interface ||
            !string.Equals(
                sourceModule.Module.SnapshotSha256,
                resolvedInterface.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "InterfaceImpl.Interface must resolve to one exact same-snapshot interface TypeDef.",
                nameof(resolvedInterfaceTypeAncestry));
        }

        var isTypeDefinition = CanonicalReplayEncoding.IsMetadataTokenForTable(interfaceTypeMetadataToken, 0x02);
        var isTypeReference = CanonicalReplayEncoding.IsMetadataTokenForTable(interfaceTypeMetadataToken, 0x01);
        if (!isTypeDefinition && !isTypeReference)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interfaceTypeMetadataToken),
                "This exact InterfaceImpl primitive admits a non-nil TypeDef or TypeRef Interface token.");
        }
        if (isTypeDefinition)
        {
            if (interfaceTypeReferenceResolution is not null ||
                !sourceModule.Equals(resolvedInterface.MetadataModule) ||
                interfaceTypeMetadataToken != resolvedInterface.TypeDefinitionToken)
            {
                throw new ArgumentException(
                    "A TypeDef InterfaceImpl token must resolve to itself in the exact source module.");
            }
        }
        else if (interfaceTypeReferenceResolution is null ||
                 !interfaceTypeReferenceResolution.SourceModule.Equals(sourceModule) ||
                 interfaceTypeReferenceResolution.ReferencedType.TypeReferenceToken != interfaceTypeMetadataToken ||
                 !interfaceTypeReferenceResolution.ResolvedTargetType.Equals(resolvedInterface))
        {
            throw new ArgumentException(
                "A TypeRef InterfaceImpl token requires one matching complete source-to-interface resolution.",
                nameof(interfaceTypeReferenceResolution));
        }

        return new StaticFieldInterfaceImplementationRowIdentity(
            sourceModule,
            interfaceImplementationToken,
            implementingType,
            interfaceTypeMetadataToken,
            ImmutableArray<byte>.Empty,
            interfaceTypeReferenceResolution,
            resolvedInterfaceTypeAncestry);
    }

    /// <summary>Creates an InterfaceImpl row whose Interface column names a constructed generic-interface TypeSpec.</summary>
    /// <param name="sourceModule">The exact metadata module containing the InterfaceImpl and TypeSpec rows.</param>
    /// <param name="interfaceImplementationToken">The non-nil InterfaceImpl row token.</param>
    /// <param name="implementingType">The exact TypeDef named by InterfaceImpl.Class.</param>
    /// <param name="interfaceTypeSpecificationToken">The non-nil TypeSpec token named by InterfaceImpl.Interface.</param>
    /// <param name="interfaceTypeSpecificationSignature">The exact bounded GENERICINST CLASS TypeSpec signature.</param>
    /// <param name="genericHeadTypeReferenceResolution">Complete resolution when the generic head is TypeRef; null for TypeDef.</param>
    /// <param name="resolvedGenericInterfaceTypeAncestry">Exact ancestry proving the resolved generic head is an interface.</param>
    /// <returns>A detached exact InterfaceImpl/TypeSpec primitive for bounded interface-closure composition.</returns>
    public static StaticFieldInterfaceImplementationRowIdentity ForTypeSpecification(
        StaticFieldMetadataModuleIdentity sourceModule,
        int interfaceImplementationToken,
        StaticFieldTypeDefinitionIdentity implementingType,
        int interfaceTypeSpecificationToken,
        ImmutableArray<byte> interfaceTypeSpecificationSignature,
        StaticFieldTypeReferenceResolutionIdentity? genericHeadTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity resolvedGenericInterfaceTypeAncestry)
    {
        ValidateCommon(
            sourceModule,
            interfaceImplementationToken,
            implementingType,
            resolvedGenericInterfaceTypeAncestry);
        CanonicalReplayEncoding.ValidateMetadataToken(
            interfaceTypeSpecificationToken,
            0x1B,
            nameof(interfaceTypeSpecificationToken));
        if (interfaceTypeSpecificationSignature.IsDefaultOrEmpty ||
            !BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                interfaceTypeSpecificationSignature.AsSpan(),
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength,
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationDepth,
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationGenericArgumentCount,
                out var projection))
        {
            throw new ArgumentException(
                "A TypeSpec InterfaceImpl requires one canonical bounded GENERICINST CLASS signature.",
                nameof(interfaceTypeSpecificationSignature));
        }
        var resolvedInterface = resolvedGenericInterfaceTypeAncestry.SubjectType;
        if (resolvedInterface.GenericArity != projection.GenericArgumentCount)
        {
            throw new ArgumentException(
                "The InterfaceImpl TypeSpec argument count disagrees with the resolved generic interface arity.",
                nameof(interfaceTypeSpecificationSignature));
        }
        if (CanonicalReplayEncoding.IsMetadataTokenForTable(projection.GenericHeadMetadataToken, 0x02))
        {
            if (genericHeadTypeReferenceResolution is not null ||
                !sourceModule.Equals(resolvedInterface.MetadataModule) ||
                projection.GenericHeadMetadataToken != resolvedInterface.TypeDefinitionToken)
            {
                throw new ArgumentException(
                    "A TypeDef-headed InterfaceImpl TypeSpec must resolve to itself in the exact source module.");
            }
        }
        else if (CanonicalReplayEncoding.IsMetadataTokenForTable(projection.GenericHeadMetadataToken, 0x01))
        {
            if (genericHeadTypeReferenceResolution is null ||
                !genericHeadTypeReferenceResolution.SourceModule.Equals(sourceModule) ||
                genericHeadTypeReferenceResolution.ReferencedType.TypeReferenceToken != projection.GenericHeadMetadataToken ||
                !genericHeadTypeReferenceResolution.ResolvedTargetType.Equals(resolvedInterface))
            {
                throw new ArgumentException(
                    "A TypeRef-headed InterfaceImpl TypeSpec requires one matching complete generic-head resolution.",
                    nameof(genericHeadTypeReferenceResolution));
            }
        }
        else
        {
            throw new ArgumentException("A generic InterfaceImpl TypeSpec head must be TypeDef or TypeRef.");
        }

        return new StaticFieldInterfaceImplementationRowIdentity(
            sourceModule,
            interfaceImplementationToken,
            implementingType,
            interfaceTypeSpecificationToken,
            CanonicalReplayEncoding.Copy(interfaceTypeSpecificationSignature),
            genericHeadTypeReferenceResolution,
            resolvedGenericInterfaceTypeAncestry);
    }

    private static void ValidateCommon(
        StaticFieldMetadataModuleIdentity sourceModule,
        int interfaceImplementationToken,
        StaticFieldTypeDefinitionIdentity implementingType,
        StaticFieldTypeAncestryIdentity resolvedInterfaceTypeAncestry)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(implementingType);
        ArgumentNullException.ThrowIfNull(resolvedInterfaceTypeAncestry);
        CanonicalReplayEncoding.ValidateMetadataToken(interfaceImplementationToken, 0x09, nameof(interfaceImplementationToken));
        if (!implementingType.MetadataModule.Equals(sourceModule))
        {
            throw new ArgumentException("InterfaceImpl.Class must be an exact TypeDef in the row's source module.", nameof(implementingType));
        }
        if (resolvedInterfaceTypeAncestry.Classification != StaticFieldTypeClassification.Interface ||
            !string.Equals(
                sourceModule.Module.SnapshotSha256,
                resolvedInterfaceTypeAncestry.SubjectType.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "InterfaceImpl.Interface must resolve to one exact same-snapshot interface TypeDef.",
                nameof(resolvedInterfaceTypeAncestry));
        }
    }

    /// <summary>Determines whether another value represents the same exact InterfaceImpl row and resolution.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when every canonical field matches.</returns>
    public bool Equals(StaticFieldInterfaceImplementationRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldInterfaceImplementationRowIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the exact declared target identity and ancestry-derived shape for a managed-reference field.</summary>
/// <remarks>
/// A string uses the canonical intrinsic ELEMENT_TYPE_STRING encoding and retains its counted resolved core-library
/// TypeDef. Other managed references may be source-encoded by TypeDef or a complete
/// TypeRef scope/forwarder/loader relation, and their exact resolved target can be a class or interface in any module
/// in the immutable snapshot. Constructed TypeSpec references are represented separately from this CLASS-token form.
/// </remarks>
public sealed class StaticFieldDeclaredReferenceIdentity : IEquatable<StaticFieldDeclaredReferenceIdentity>
{
    private const string CanonicalDomain = "static-field-declared-reference-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldDeclaredReferenceIdentity(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldDeclaredReferenceKind kind,
        StaticFieldStringSignatureEncoding? stringSignatureEncoding,
        int? typeMetadataToken,
        StaticFieldTypeReferenceResolutionIdentity? sourceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        OwnerMetadataModule = ownerMetadataModule;
        Kind = kind;
        StringSignatureEncoding = stringSignatureEncoding;
        TypeMetadataToken = typeMetadataToken;
        SourceTypeReferenceResolution = sourceTypeReferenceResolution;
        TargetTypeAncestry = targetTypeAncestry;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(ownerMetadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)kind);
        writer.WriteBoolean(stringSignatureEncoding.HasValue);
        if (stringSignatureEncoding.HasValue)
        {
            writer.WriteInt32((int)stringSignatureEncoding.Value);
        }
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, typeMetadataToken);
        writer.WriteBoolean(sourceTypeReferenceResolution is not null);
        if (sourceTypeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(sourceTypeReferenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(targetTypeAncestry.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the target is exact System.String or another exact managed-reference type.</summary>
    public StaticFieldDeclaredReferenceKind Kind { get; }

    /// <summary>Gets intrinsic string encoding only for System.String, otherwise null.</summary>
    public StaticFieldStringSignatureEncoding? StringSignatureEncoding { get; }

    /// <summary>Gets the exact physical module owning the TypeRef/TypeDef token.</summary>
    public StaticFieldModuleInstanceIdentity OwnerModule => OwnerMetadataModule.Module;

    /// <summary>Gets the complete counted metadata identity owning the TypeRef/TypeDef token.</summary>
    public ModuleContentIdentity OwnerModuleContent => OwnerMetadataModule.ModuleContent;

    /// <summary>Gets the complete exact owner metadata-module/containing-assembly relation.</summary>
    public StaticFieldMetadataModuleIdentity OwnerMetadataModule { get; }

    /// <summary>Gets the exact TypeDef/TypeRef signature token, or null for intrinsic string.</summary>
    public int? TypeMetadataToken { get; }

    /// <summary>Gets the complete source TypeRef resolution for CLASS TypeRef, otherwise null.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? SourceTypeReferenceResolution { get; }

    /// <summary>Gets the AssemblyRef root token when the source TypeRef is AssemblyRef-scoped, otherwise null.</summary>
    public int? AssemblyReferenceToken => SourceTypeReferenceResolution?.AssemblyReferenceToken;

    /// <summary>Gets the exact decoded source TypeRef namespace for a managed-reference target, otherwise null.</summary>
    public string? SourceTypeReferenceNamespaceName => SourceTypeReferenceResolution?.ReferencedType.NamespaceName;

    /// <summary>Gets the exact decoded source TypeRef metadata name for a managed-reference target, otherwise null.</summary>
    public string? SourceTypeReferenceName => SourceTypeReferenceResolution?.ReferencedType.TypeName;

    /// <summary>Gets the exact ancestry-derived identity of the resolved target TypeDef.</summary>
    public StaticFieldTypeAncestryIdentity TargetTypeAncestry { get; }

    /// <summary>Gets the exact decoded target namespace.</summary>
    public string NamespaceName => TargetTypeAncestry.SubjectType.NamespaceName;

    /// <summary>Gets the exact decoded target type name.</summary>
    public string TypeName => TargetTypeAncestry.SubjectType.TypeName;

    /// <summary>Gets raw TypeAttributes of the resolved String or concrete target TypeDef.</summary>
    public int TypeAttributes => TargetTypeAncestry.SubjectType.TypeAttributes;

    /// <summary>Gets the exact total generic arity; CLASS-token managed-reference targets require zero.</summary>
    public int GenericArity => TargetTypeAncestry.SubjectType.GenericArity;

    /// <summary>Gets the exact runtime module containing the resolved target TypeDef.</summary>
    public StaticFieldModuleInstanceIdentity ResolvedTargetModule => TargetTypeAncestry.SubjectType.Module;

    /// <summary>Gets the complete counted metadata identity containing the resolved target TypeDef.</summary>
    public ModuleContentIdentity ResolvedTargetModuleContent => TargetTypeAncestry.SubjectType.ModuleContent;

    /// <summary>Gets the non-nil resolved target TypeDef token.</summary>
    public int ResolvedTargetTypeDefinitionToken => TargetTypeAncestry.SubjectType.TypeDefinitionToken;

    /// <summary>Gets whether ancestry proves a reference class rather than an interface.</summary>
    public bool IsClass => TargetTypeAncestry.Classification == StaticFieldTypeClassification.ReferenceClass;

    /// <summary>Gets whether the exact resolved target is top-level rather than nested.</summary>
    public bool IsTopLevel => TargetTypeAncestry.SubjectType.IsTopLevel;

    /// <summary>Gets whether the resolved target belongs to a different exact metadata module than the signature token.</summary>
    public bool IsCrossModuleTarget => !OwnerMetadataModule.Equals(TargetTypeAncestry.SubjectType.MetadataModule);

    /// <summary>Gets a defensive copy of the versioned canonical target bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an intrinsic ELEMENT_TYPE_STRING identity with no fabricated source TypeRef/AssemblyRef.</summary>
    /// <param name="ownerMetadataModule">The complete exact declaration metadata-module relation owning the FieldSig.</param>
    /// <param name="targetTypeAncestry">Exact ancestry proving the resolved target is sealed, non-abstract System.String.</param>
    /// <returns>An intrinsic string signature plus complete resolved System.String target identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The target token or arity is invalid.</exception>
    public static StaticFieldDeclaredReferenceIdentity PrimitiveSystemString(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ValidateOwner(ownerMetadataModule);
        ValidateSystemStringTarget(ownerMetadataModule.Module, targetTypeAncestry);
        return new StaticFieldDeclaredReferenceIdentity(
            ownerMetadataModule,
            StaticFieldDeclaredReferenceKind.SystemString,
            StaticFieldStringSignatureEncoding.PrimitiveString,
            typeMetadataToken: null,
            sourceTypeReferenceResolution: null,
            targetTypeAncestry);
    }

    /// <summary>Creates a canonical ELEMENT_TYPE_OBJECT identity resolved to runtime-selected System.Object.</summary>
    /// <param name="ownerMetadataModule">The complete declaration metadata-module relation owning the FieldSig.</param>
    /// <param name="targetTypeAncestry">Exact ancestry proving the runtime-selected System.Object target.</param>
    /// <returns>An intrinsic object signature plus complete exact System.Object target identity.</returns>
    public static StaticFieldDeclaredReferenceIdentity PrimitiveSystemObject(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ValidateOwner(ownerMetadataModule);
        ArgumentNullException.ThrowIfNull(targetTypeAncestry);
        var target = targetTypeAncestry.SubjectType;
        var coreLibrary = targetTypeAncestry.CoreLibrary;
        if (!string.Equals(
                ownerMetadataModule.Module.SnapshotSha256,
                target.Module.SnapshotSha256,
                StringComparison.Ordinal) ||
            coreLibrary is null ||
            !target.Equals(coreLibrary.SystemObjectType) ||
            targetTypeAncestry.Classification != StaticFieldTypeClassification.ReferenceClass)
        {
            throw new ArgumentException(
                "ELEMENT_TYPE_OBJECT requires the exact System.Object role in the runtime-selected core library.",
                nameof(targetTypeAncestry));
        }
        return new StaticFieldDeclaredReferenceIdentity(
            ownerMetadataModule,
            StaticFieldDeclaredReferenceKind.SystemObject,
            stringSignatureEncoding: null,
            typeMetadataToken: null,
            sourceTypeReferenceResolution: null,
            targetTypeAncestry);
    }

    /// <summary>Creates one exact same-module CLASS TypeDef target with admitted managed-reference semantics.</summary>
    /// <param name="ownerMetadataModule">The complete exact declaration metadata module containing the target TypeDef row.</param>
    /// <param name="targetTypeAncestry">Exact ancestry proving class/interface semantics or an exact core-library ValueType/Enum role.</param>
    /// <returns>A same-module TypeDef-encoded managed-reference target identity.</returns>
    /// <exception cref="ArgumentException">The target is foreign, generic, or not a managed-reference type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The token is not a TypeDef or generic arity is not zero.</exception>
    public static StaticFieldDeclaredReferenceIdentity ManagedReferenceTypeDefinition(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ValidateOwner(ownerMetadataModule);
        ArgumentNullException.ThrowIfNull(targetTypeAncestry);
        var target = targetTypeAncestry.SubjectType;
        if (!target.MetadataModule.Equals(ownerMetadataModule) ||
            !IsAdmittedClassEncodedReferenceTarget(targetTypeAncestry) ||
            target.GenericArity != 0 || RequiresIntrinsicReferenceEncoding(targetTypeAncestry))
        {
            throw new ArgumentException(
                "A TypeDef managed-reference target must be exact, same-module, non-generic, and a class/interface or exact core-library ValueType/Enum role.",
                nameof(targetTypeAncestry));
        }
        return new StaticFieldDeclaredReferenceIdentity(
            ownerMetadataModule,
            StaticFieldDeclaredReferenceKind.ManagedReference,
            stringSignatureEncoding: null,
            target.TypeDefinitionToken,
            sourceTypeReferenceResolution: null,
            targetTypeAncestry);
    }

    /// <summary>Creates a CLASS TypeRef source resolving to one exact admitted managed-reference target TypeDef.</summary>
    /// <param name="ownerMetadataModule">The complete declaration metadata module owning the TypeRef and FieldSig.</param>
    /// <param name="sourceTypeReferenceResolution">The complete exact source TypeRef scope chain.</param>
    /// <param name="targetTypeAncestry">Exact ancestry for the resolved target in any same-snapshot module.</param>
    /// <returns>A TypeRef-encoded managed-reference target retaining its complete resolution relation.</returns>
    public static StaticFieldDeclaredReferenceIdentity ManagedReferenceTypeReference(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldTypeReferenceResolutionIdentity sourceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ValidateOwner(ownerMetadataModule);
        ArgumentNullException.ThrowIfNull(sourceTypeReferenceResolution);
        ArgumentNullException.ThrowIfNull(targetTypeAncestry);
        var target = targetTypeAncestry.SubjectType;
        var sourceType = sourceTypeReferenceResolution.ReferencedType;
        if (!sourceTypeReferenceResolution.SourceModule.Equals(ownerMetadataModule) ||
            !sourceTypeReferenceResolution.ResolvedTargetType.Equals(target) ||
            !IsAdmittedClassEncodedReferenceTarget(targetTypeAncestry) ||
            target.GenericArity != 0 ||
            RequiresIntrinsicReferenceEncoding(targetTypeAncestry) ||
            !string.Equals(ownerMetadataModule.Module.SnapshotSha256, target.Module.SnapshotSha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The source TypeRef must resolve exactly to one same-snapshot non-generic class/interface or exact core-library ValueType/Enum role.",
                nameof(sourceTypeReferenceResolution));
        }
        return new StaticFieldDeclaredReferenceIdentity(
            ownerMetadataModule,
            StaticFieldDeclaredReferenceKind.ManagedReference,
            stringSignatureEncoding: null,
            sourceType.TypeReferenceToken,
            sourceTypeReferenceResolution,
            targetTypeAncestry);
    }

    /// <summary>Determines whether another declared target has identical versioned metadata identity and shape.</summary>
    /// <param name="other">The declared reference identity to compare.</param>
    /// <returns><see langword="true"/> only when all canonical target facts match.</returns>
    public bool Equals(StaticFieldDeclaredReferenceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldDeclaredReferenceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateOwner(StaticFieldMetadataModuleIdentity ownerMetadataModule)
    {
        ArgumentNullException.ThrowIfNull(ownerMetadataModule);
    }

    private static void ValidateSystemStringTarget(
        StaticFieldModuleInstanceIdentity ownerModule,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ArgumentNullException.ThrowIfNull(targetTypeAncestry);
        var target = targetTypeAncestry.SubjectType;
        var coreLibrary = targetTypeAncestry.CoreLibrary;
        var flags = (TypeAttributes)target.TypeAttributes;
        if (!string.Equals(ownerModule.SnapshotSha256, target.Module.SnapshotSha256, StringComparison.Ordinal) ||
            targetTypeAncestry.Classification != StaticFieldTypeClassification.ReferenceClass ||
            !string.Equals(target.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(target.TypeName, "String", StringComparison.Ordinal) ||
            target.GenericArity != 0 || !target.IsTopLevel || target.IsAbstract ||
            (flags & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public ||
            (flags & System.Reflection.TypeAttributes.Sealed) == 0 ||
            coreLibrary is null ||
            !target.Module.Equals(coreLibrary.Module) ||
            !target.ModuleContent.Equals(coreLibrary.ModuleContent) ||
            !target.AssemblyDefinition.Equals(coreLibrary.AssemblyDefinition))
        {
            throw new ArgumentException(
                "The resolved target must be exact sealed, non-abstract System.String in the runtime-selected core library of the snapshot.",
                nameof(targetTypeAncestry));
        }
    }

    private static bool RequiresIntrinsicReferenceEncoding(StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        var target = targetTypeAncestry.SubjectType;
        var coreLibrary = targetTypeAncestry.CoreLibrary;
        return coreLibrary is not null && target.MetadataModule.Equals(coreLibrary.MetadataModule) &&
            string.Equals(target.NamespaceName, "System", StringComparison.Ordinal) &&
            target.TypeName is "Object" or "String";
    }

    private static bool IsAdmittedClassEncodedReferenceTarget(
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        if (targetTypeAncestry.Classification is
            StaticFieldTypeClassification.ReferenceClass or StaticFieldTypeClassification.Interface)
        {
            return true;
        }

        var target = targetTypeAncestry.SubjectType;
        var coreLibrary = targetTypeAncestry.CoreLibrary;
        return coreLibrary is not null &&
            (target.Equals(coreLibrary.SystemValueType) || target.Equals(coreLibrary.SystemEnumType));
    }
}

/// <summary>Freezes the named generic definition required to prove exact System.Nullable&lt;Int32&gt;.</summary>
/// <remarks>
/// A GENERICINST VALUETYPE signature is not sufficient by itself: this identity scopes its TypeDef/TypeRef coded
/// index to counted source metadata and retains the resolved System.Nullable`1 TypeDef in the same snapshot and
/// application domain. The Int32 argument remains proven by the intrinsic ECMA element code in the FieldSig.
/// </remarks>
public sealed class StaticFieldNullableTypeIdentity : IEquatable<StaticFieldNullableTypeIdentity>
{
    private const string CanonicalDomain = "static-field-nullable-type-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldNullableTypeIdentity(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        int signatureTypeMetadataToken,
        StaticFieldTypeReferenceResolutionIdentity? sourceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        OwnerMetadataModule = ownerMetadataModule;
        SignatureTypeMetadataToken = signatureTypeMetadataToken;
        SourceTypeReferenceResolution = sourceTypeReferenceResolution;
        TargetTypeAncestry = targetTypeAncestry;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(ownerMetadataModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(signatureTypeMetadataToken);
        writer.WriteBoolean(sourceTypeReferenceResolution is not null);
        if (sourceTypeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(sourceTypeReferenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(targetTypeAncestry.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the declaration module owning the FieldSig TypeDefOrRef coded index.</summary>
    public StaticFieldModuleInstanceIdentity OwnerModule => OwnerMetadataModule.Module;

    /// <summary>Gets the complete counted declaration-module metadata identity.</summary>
    public ModuleContentIdentity OwnerModuleContent => OwnerMetadataModule.ModuleContent;

    /// <summary>Gets the complete exact owner metadata-module/containing-assembly relation.</summary>
    public StaticFieldMetadataModuleIdentity OwnerMetadataModule { get; }

    /// <summary>Gets the exact TypeDef or TypeRef token encoded by the generic-instance signature.</summary>
    public int SignatureTypeMetadataToken { get; }

    /// <summary>Gets the complete source TypeRef resolution, or null for a same-owner TypeDef.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? SourceTypeReferenceResolution { get; }

    /// <summary>Gets the AssemblyRef root token for an AssemblyRef-scoped source TypeRef, otherwise null.</summary>
    public int? AssemblyReferenceToken => SourceTypeReferenceResolution?.AssemblyReferenceToken;

    /// <summary>Gets the exact decoded source TypeDef/TypeRef namespace; System is required.</summary>
    public string SourceTypeNamespaceName => SourceTypeReferenceResolution?.ReferencedType.NamespaceName ?? NamespaceName;

    /// <summary>Gets the exact decoded source TypeDef/TypeRef metadata name; Nullable`1 is required.</summary>
    public string SourceTypeName => SourceTypeReferenceResolution?.ReferencedType.TypeName ?? TypeName;

    /// <summary>Gets exact ancestry proving that the resolved target is a System.Nullable`1 value type.</summary>
    public StaticFieldTypeAncestryIdentity TargetTypeAncestry { get; }

    /// <summary>Gets the runtime module containing the resolved System.Nullable`1 TypeDef.</summary>
    public StaticFieldModuleInstanceIdentity ResolvedTargetModule => TargetTypeAncestry.SubjectType.Module;

    /// <summary>Gets the complete counted resolved-target metadata identity.</summary>
    public ModuleContentIdentity ResolvedTargetModuleContent => TargetTypeAncestry.SubjectType.ModuleContent;

    /// <summary>Gets the exact resolved System.Nullable`1 TypeDef token.</summary>
    public int ResolvedTargetTypeDefinitionToken => TargetTypeAncestry.SubjectType.TypeDefinitionToken;

    /// <summary>Gets raw TypeAttributes proving the resolved definition is a top-level type.</summary>
    public int ResolvedTargetTypeAttributes => TargetTypeAncestry.SubjectType.TypeAttributes;

    /// <summary>Gets the exact resolved namespace, always System.</summary>
    public string NamespaceName => TargetTypeAncestry.SubjectType.NamespaceName;

    /// <summary>Gets the exact metadata type name, always Nullable`1.</summary>
    public string TypeName => TargetTypeAncestry.SubjectType.TypeName;

    /// <summary>Gets the exact resolved generic arity, always one.</summary>
    public int GenericArity => TargetTypeAncestry.SubjectType.GenericArity;

    /// <summary>Gets a defensive copy of the versioned canonical identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a counted source-token and resolved-target identity for System.Nullable`1.</summary>
    /// <param name="ownerMetadataModule">The complete declaration metadata module owning the signature token.</param>
    /// <param name="signatureTypeMetadataToken">The exact TypeDef or TypeRef encoded by FieldSig.</param>
    /// <param name="sourceTypeReferenceResolution">Complete source TypeRef scope chain; null for a same-owner TypeDef.</param>
    /// <param name="targetTypeAncestry">Exact ancestry proving the resolved target is a value type named System.Nullable`1.</param>
    /// <returns>A complete immutable named-generic-definition identity.</returns>
    public static StaticFieldNullableTypeIdentity Create(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        int signatureTypeMetadataToken,
        StaticFieldTypeReferenceResolutionIdentity? sourceTypeReferenceResolution,
        StaticFieldTypeAncestryIdentity targetTypeAncestry)
    {
        ArgumentNullException.ThrowIfNull(ownerMetadataModule);
        ArgumentNullException.ThrowIfNull(targetTypeAncestry);
        var target = targetTypeAncestry.SubjectType;
        var isTypeDefinition = CanonicalReplayEncoding.IsMetadataTokenForTable(signatureTypeMetadataToken, 0x02);
        var isTypeReference = CanonicalReplayEncoding.IsMetadataTokenForTable(signatureTypeMetadataToken, 0x01);
        if (!isTypeDefinition && !isTypeReference)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signatureTypeMetadataToken),
                "Nullable signature identity requires a non-nil TypeDef or TypeRef token.");
        }
        if (isTypeReference && sourceTypeReferenceResolution is null)
        {
            throw new ArgumentException(
                "A Nullable TypeRef requires one complete exact ResolutionScope chain.",
                nameof(sourceTypeReferenceResolution));
        }
        if (isTypeDefinition &&
            (sourceTypeReferenceResolution is not null ||
             !ownerMetadataModule.Equals(target.MetadataModule) ||
             signatureTypeMetadataToken != target.TypeDefinitionToken))
        {
            throw new ArgumentException(
                "A Nullable TypeDef signature token must resolve to itself in the exact owner metadata.",
                nameof(signatureTypeMetadataToken));
        }
        if (isTypeReference &&
            (sourceTypeReferenceResolution!.ReferencedType.TypeReferenceToken != signatureTypeMetadataToken ||
             sourceTypeReferenceResolution.TypeReferences.Length != 1 ||
             !sourceTypeReferenceResolution.SourceModule.Equals(ownerMetadataModule) ||
             !sourceTypeReferenceResolution.ResolvedTargetType.Equals(target)))
        {
            throw new ArgumentException(
                "A top-level Nullable TypeRef token must be the head of its exact one-row resolution chain.",
                nameof(sourceTypeReferenceResolution));
        }
        if (!string.Equals(
                ownerMetadataModule.Module.SnapshotSha256,
                target.Module.SnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Nullable source and target modules must belong to the same immutable snapshot.",
                nameof(targetTypeAncestry));
        }
        var sourceNamespaceName = sourceTypeReferenceResolution?.ReferencedType.NamespaceName ?? target.NamespaceName;
        var sourceTypeName = sourceTypeReferenceResolution?.ReferencedType.TypeName ?? target.TypeName;
        if (!string.Equals(sourceNamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(sourceTypeName, "Nullable`1", StringComparison.Ordinal) ||
            !string.Equals(target.NamespaceName, sourceNamespaceName, StringComparison.Ordinal) ||
            !string.Equals(target.TypeName, sourceTypeName, StringComparison.Ordinal) ||
            target.GenericArity != 1 || !target.IsTopLevel || target.IsAbstract ||
            (((TypeAttributes)target.TypeAttributes & System.Reflection.TypeAttributes.VisibilityMask) !=
                System.Reflection.TypeAttributes.Public) ||
            targetTypeAncestry.Classification != StaticFieldTypeClassification.ValueType)
        {
            throw new ArgumentException(
                "Nullable identity requires exact source and resolved System.Nullable`1 names plus value-type ancestry.",
                nameof(targetTypeAncestry));
        }
        var coreLibrary = targetTypeAncestry.CoreLibrary;
        if (coreLibrary is null || !target.MetadataModule.Equals(coreLibrary.MetadataModule))
        {
            throw new ArgumentException(
                "Resolved System.Nullable`1 must belong to the exact runtime-selected core-library observation.",
                nameof(targetTypeAncestry));
        }

        return new StaticFieldNullableTypeIdentity(
            ownerMetadataModule,
            signatureTypeMetadataToken,
            sourceTypeReferenceResolution,
            targetTypeAncestry);
    }

    /// <summary>Determines whether another identity carries the same source token and resolved target.</summary>
    /// <param name="other">The nullable identity to compare.</param>
    /// <returns><see langword="true"/> only when every canonical fact matches.</returns>
    public bool Equals(StaticFieldNullableTypeIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldNullableTypeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete exact MethodDef row and its direct TypeDef/ParamList ownership ranges.</summary>
public sealed class StaticFieldMethodDefinitionIdentity : IEquatable<StaticFieldMethodDefinitionIdentity>
{
    /// <summary>Gets the maximum exact MethodDef signature length admitted before copying.</summary>
    public const int MaximumMethodSignatureLength = 256;

    private const string CanonicalDomain = "static-field-method-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldMethodDefinitionIdentity(
        StaticFieldTypeDefinitionIdentity declaringType,
        int methodDefinitionToken,
        int relativeVirtualAddress,
        int implementationAttributes,
        int attributes,
        string name,
        ImmutableArray<byte> signature,
        int parameterListRowId,
        int parameterListEndExclusiveRowId,
        int parameterDefinitionRowCount)
    {
        DeclaringType = declaringType;
        MethodDefinitionToken = methodDefinitionToken;
        RelativeVirtualAddress = relativeVirtualAddress;
        ImplementationAttributes = implementationAttributes;
        Attributes = attributes;
        Name = name;
        this.signature = signature;
        ParameterListRowId = parameterListRowId;
        ParameterListEndExclusiveRowId = parameterListEndExclusiveRowId;
        ParameterDefinitionRowCount = parameterDefinitionRowCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(declaringType.CanonicalBytes.AsSpan());
        writer.WriteInt32(methodDefinitionToken);
        writer.WriteInt32(relativeVirtualAddress);
        writer.WriteInt32(implementationAttributes);
        writer.WriteInt32(attributes);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        writer.WriteInt32(parameterListRowId);
        writer.WriteInt32(parameterListEndExclusiveRowId);
        writer.WriteInt32(parameterDefinitionRowCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact declaring TypeDef and MethodList owner interval.</summary>
    public StaticFieldTypeDefinitionIdentity DeclaringType { get; }

    /// <summary>Gets the non-nil MethodDef token.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the exact raw MethodDef RVA.</summary>
    public int RelativeVirtualAddress { get; }

    /// <summary>Gets the exact raw MethodImplAttributes bits.</summary>
    public int ImplementationAttributes { get; }

    /// <summary>Gets the exact raw MethodAttributes bits.</summary>
    public int Attributes { get; }

    /// <summary>Gets the exact bounded method name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the exact method signature blob.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets the exact raw MethodDef.ParamList starting row ID.</summary>
    public int ParameterListRowId { get; }

    /// <summary>Gets the next MethodDef.ParamList or Param-table-end row ID, exclusive.</summary>
    public int ParameterListEndExclusiveRowId { get; }

    /// <summary>Gets the exact complete Param table row count bounding the ownership interval.</summary>
    public int ParameterDefinitionRowCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical MethodDef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete exact directly owned MethodDef row.</summary>
    /// <param name="declaringType">The exact TypeDef whose MethodList interval owns the row.</param>
    /// <param name="methodDefinitionToken">The non-nil MethodDef token.</param>
    /// <param name="relativeVirtualAddress">The exact raw RVA.</param>
    /// <param name="implementationAttributes">The exact raw MethodImplAttributes bits.</param>
    /// <param name="attributes">The exact raw MethodAttributes bits.</param>
    /// <param name="name">The exact bounded method name.</param>
    /// <param name="signature">The initialized bounded exact MethodDef signature.</param>
    /// <param name="parameterListRowId">The exact raw ParamList starting row ID.</param>
    /// <param name="parameterListEndExclusiveRowId">The next ParamList or table-end row ID.</param>
    /// <param name="parameterDefinitionRowCount">The exact complete Param table row count.</param>
    /// <returns>A canonical complete MethodDef row and direct-owner relation.</returns>
    public static StaticFieldMethodDefinitionIdentity Create(
        StaticFieldTypeDefinitionIdentity declaringType,
        int methodDefinitionToken,
        int relativeVirtualAddress,
        int implementationAttributes,
        int attributes,
        string name,
        ImmutableArray<byte> signature,
        int parameterListRowId,
        int parameterListEndExclusiveRowId,
        int parameterDefinitionRowCount)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        if (!declaringType.DirectlyDeclaresMethod(methodDefinitionToken))
        {
            throw new ArgumentException("The MethodDef lies outside the declaring TypeDef's exact MethodList interval.");
        }
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (signature.IsDefaultOrEmpty || signature.Length > MaximumMethodSignatureLength)
        {
            throw new ArgumentException(
                $"An exact MethodDef signature of 1..{MaximumMethodSignatureLength} bytes is required.",
                nameof(signature));
        }
        if (parameterDefinitionRowCount is < 0 or > 0x00FF_FFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameterDefinitionRowCount),
                "The exact Param table row count must fit the metadata-token row-id domain.");
        }
        if (parameterListRowId <= 0 || parameterListEndExclusiveRowId < parameterListRowId ||
            parameterListEndExclusiveRowId > parameterDefinitionRowCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterListRowId), "MethodDef ParamList range is invalid.");
        }
        return new StaticFieldMethodDefinitionIdentity(
            declaringType,
            methodDefinitionToken,
            relativeVirtualAddress,
            implementationAttributes,
            attributes,
            name,
            CanonicalReplayEncoding.Copy(signature),
            parameterListRowId,
            parameterListEndExclusiveRowId,
            parameterDefinitionRowCount);
    }

    /// <summary>Determines whether another value has the same complete MethodDef row and owner.</summary>
    /// <param name="other">The method to compare.</param>
    /// <returns><see langword="true"/> only when every canonical MethodDef fact matches.</returns>
    public bool Equals(StaticFieldMethodDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldMethodDefinitionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact type-parented MemberRef row without claiming constructor resolution.</summary>
/// <remarks>
/// This row identity is intentionally weaker than <see cref="StaticFieldMemberReferenceIdentity"/>. It lets an
/// unrelated CustomAttribute remain exact when its constructor belongs to a generic TypeSpec or when resolving its
/// method would add no information needed for storage-marker classification. Candidate marker names still require
/// the stronger resolved identity and ancestry proof.
/// </remarks>
public sealed class StaticFieldMemberReferenceRowIdentity : IEquatable<StaticFieldMemberReferenceRowIdentity>
{
    private const string CanonicalDomain = "static-field-member-reference-row-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> parentTypeSpecificationSignature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldMemberReferenceRowIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        StaticFieldCustomAttributeMemberReferenceParentKind parentKind,
        int parentMetadataToken,
        string name,
        ImmutableArray<byte> signature,
        StaticFieldTypeDefinitionIdentity? parentTypeDefinition,
        StaticFieldTypeReferenceRowIdentity? parentTypeReference,
        ImmutableArray<byte> parentTypeSpecificationSignature)
    {
        SourceModule = sourceModule;
        MemberReferenceToken = memberReferenceToken;
        ParentKind = parentKind;
        ParentMetadataToken = parentMetadataToken;
        Name = name;
        this.signature = signature;
        ParentTypeDefinition = parentTypeDefinition;
        ParentTypeReference = parentTypeReference;
        this.parentTypeSpecificationSignature = parentTypeSpecificationSignature;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(memberReferenceToken);
        writer.WriteInt32((int)parentKind);
        writer.WriteInt32(parentMetadataToken);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        writer.WriteBoolean(parentTypeDefinition is not null);
        if (parentTypeDefinition is not null)
        {
            writer.WriteLengthPrefixedBytes(parentTypeDefinition.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(parentTypeReference is not null);
        if (parentTypeReference is not null)
        {
            writer.WriteLengthPrefixedBytes(parentTypeReference.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(parentTypeSpecificationSignature.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source metadata module containing the MemberRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the non-nil MemberRef token.</summary>
    public int MemberReferenceToken { get; }

    /// <summary>Gets the retained type-shaped MemberRefParent form.</summary>
    public StaticFieldCustomAttributeMemberReferenceParentKind ParentKind { get; }

    /// <summary>Gets the exact non-nil TypeDef, TypeRef, or TypeSpec Parent token.</summary>
    public int ParentMetadataToken { get; }

    /// <summary>Gets the exact bounded MemberRef name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the exact bounded MemberRef signature.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets the complete same-module Parent TypeDef only for <see cref="StaticFieldCustomAttributeMemberReferenceParentKind.TypeDefinition"/>.</summary>
    public StaticFieldTypeDefinitionIdentity? ParentTypeDefinition { get; }

    /// <summary>Gets the exact source Parent TypeRef row only for <see cref="StaticFieldCustomAttributeMemberReferenceParentKind.TypeReference"/>.</summary>
    public StaticFieldTypeReferenceRowIdentity? ParentTypeReference { get; }

    /// <summary>Gets the exact bounded Parent TypeSpec signature only for <see cref="StaticFieldCustomAttributeMemberReferenceParentKind.TypeSpecification"/>.</summary>
    public ImmutableArray<byte> ParentTypeSpecificationSignature =>
        CanonicalReplayEncoding.Copy(parentTypeSpecificationSignature);

    /// <summary>Gets a defensive copy of the versioned canonical MemberRef-row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact unresolved MemberRef whose Parent is a same-module TypeDef.</summary>
    /// <param name="sourceModule">The exact metadata module containing both rows.</param>
    /// <param name="memberReferenceToken">The non-nil MemberRef token.</param>
    /// <param name="parentTypeDefinition">The exact same-module Parent TypeDef.</param>
    /// <param name="name">The exact bounded MemberRef name.</param>
    /// <param name="signature">The initialized bounded exact MemberRef signature.</param>
    /// <returns>A canonical unresolved MemberRef row.</returns>
    public static StaticFieldMemberReferenceRowIdentity ForTypeDefinition(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        StaticFieldTypeDefinitionIdentity parentTypeDefinition,
        string name,
        ImmutableArray<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(parentTypeDefinition);
        if (!parentTypeDefinition.MetadataModule.Equals(sourceModule))
        {
            throw new ArgumentException("A TypeDef MemberRef parent must belong to the exact source module.", nameof(parentTypeDefinition));
        }
        return Create(
            sourceModule,
            memberReferenceToken,
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeDefinition,
            parentTypeDefinition.TypeDefinitionToken,
            name,
            signature,
            parentTypeDefinition,
            parentTypeReference: null,
            parentTypeSpecificationSignature: ImmutableArray<byte>.Empty);
    }

    /// <summary>Creates an exact unresolved MemberRef whose Parent is one source-module TypeRef row.</summary>
    /// <param name="sourceModule">The exact metadata module containing both rows.</param>
    /// <param name="memberReferenceToken">The non-nil MemberRef token.</param>
    /// <param name="parentTypeReference">The exact source TypeRef Parent row.</param>
    /// <param name="name">The exact bounded MemberRef name.</param>
    /// <param name="signature">The initialized bounded exact MemberRef signature.</param>
    /// <returns>A canonical unresolved MemberRef row.</returns>
    public static StaticFieldMemberReferenceRowIdentity ForTypeReference(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        StaticFieldTypeReferenceRowIdentity parentTypeReference,
        string name,
        ImmutableArray<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(parentTypeReference);
        return Create(
            sourceModule,
            memberReferenceToken,
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeReference,
            parentTypeReference.TypeReferenceToken,
            name,
            signature,
            parentTypeDefinition: null,
            parentTypeReference,
            parentTypeSpecificationSignature: ImmutableArray<byte>.Empty);
    }

    /// <summary>Creates an exact unresolved MemberRef whose Parent is a generic-class TypeSpec row.</summary>
    /// <param name="sourceModule">The exact metadata module containing both rows.</param>
    /// <param name="memberReferenceToken">The non-nil MemberRef token.</param>
    /// <param name="parentTypeSpecificationToken">The non-nil TypeSpec Parent token.</param>
    /// <param name="parentTypeSpecificationSignature">The exact bounded generic-class TypeSpec signature.</param>
    /// <param name="name">The exact bounded MemberRef name.</param>
    /// <param name="signature">The initialized bounded exact MemberRef signature.</param>
    /// <returns>A canonical unresolved generic-parent MemberRef row.</returns>
    public static StaticFieldMemberReferenceRowIdentity ForTypeSpecification(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        int parentTypeSpecificationToken,
        ImmutableArray<byte> parentTypeSpecificationSignature,
        string name,
        ImmutableArray<byte> signature)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            parentTypeSpecificationToken,
            0x1B,
            nameof(parentTypeSpecificationToken));
        if (parentTypeSpecificationSignature.IsDefaultOrEmpty ||
            parentTypeSpecificationSignature.Length > StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength ||
            !BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                parentTypeSpecificationSignature.AsSpan(),
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength,
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationDepth,
                StaticFieldTypeAncestryEdge.MaximumTypeSpecificationGenericArgumentCount,
                out _))
        {
            throw new ArgumentException(
                "A TypeSpec MemberRef parent requires one canonical bounded generic-class signature.",
                nameof(parentTypeSpecificationSignature));
        }
        return Create(
            sourceModule,
            memberReferenceToken,
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeSpecification,
            parentTypeSpecificationToken,
            name,
            signature,
            parentTypeDefinition: null,
            parentTypeReference: null,
            parentTypeSpecificationSignature);
    }

    private static StaticFieldMemberReferenceRowIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        StaticFieldCustomAttributeMemberReferenceParentKind parentKind,
        int parentMetadataToken,
        string name,
        ImmutableArray<byte> signature,
        StaticFieldTypeDefinitionIdentity? parentTypeDefinition,
        StaticFieldTypeReferenceRowIdentity? parentTypeReference,
        ImmutableArray<byte> parentTypeSpecificationSignature)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        CanonicalReplayEncoding.ValidateMetadataToken(memberReferenceToken, 0x0A, nameof(memberReferenceToken));
        StaticFieldContractEncoding.RequireDefined(parentKind, nameof(parentKind));
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (signature.IsDefaultOrEmpty || signature.Length > StaticFieldMethodDefinitionIdentity.MaximumMethodSignatureLength)
        {
            throw new ArgumentException(
                $"An exact MemberRef signature of 1..{StaticFieldMethodDefinitionIdentity.MaximumMethodSignatureLength} bytes is required.",
                nameof(signature));
        }
        return new StaticFieldMemberReferenceRowIdentity(
            sourceModule,
            memberReferenceToken,
            parentKind,
            parentMetadataToken,
            name,
            CanonicalReplayEncoding.Copy(signature),
            parentTypeDefinition,
            parentTypeReference,
            CanonicalReplayEncoding.Copy(parentTypeSpecificationSignature));
    }

    /// <summary>Determines whether another value has the same complete unresolved MemberRef row and Parent evidence.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when every canonical row fact matches.</returns>
    public bool Equals(StaticFieldMemberReferenceRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldMemberReferenceRowIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete exact MemberRef row and its resolved constructor MethodDef.</summary>
public sealed class StaticFieldMemberReferenceIdentity : IEquatable<StaticFieldMemberReferenceIdentity>
{
    private const string CanonicalDomain = "static-field-member-reference-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldMemberReferenceIdentity(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        int parentTypeMetadataToken,
        string name,
        ImmutableArray<byte> signature,
        StaticFieldTypeReferenceResolutionIdentity? parentTypeReferenceResolution,
        StaticFieldMethodDefinitionIdentity resolvedMethod)
    {
        SourceModule = sourceModule;
        MemberReferenceToken = memberReferenceToken;
        ParentTypeMetadataToken = parentTypeMetadataToken;
        Name = name;
        this.signature = signature;
        ParentTypeReferenceResolution = parentTypeReferenceResolution;
        ResolvedMethod = resolvedMethod;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(memberReferenceToken);
        writer.WriteInt32(parentTypeMetadataToken);
        writer.WriteString(name);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        writer.WriteBoolean(parentTypeReferenceResolution is not null);
        if (parentTypeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(parentTypeReferenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(resolvedMethod.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source metadata module containing the MemberRef row.</summary>
    public StaticFieldMetadataModuleIdentity SourceModule { get; }

    /// <summary>Gets the non-nil MemberRef token.</summary>
    public int MemberReferenceToken { get; }

    /// <summary>Gets the exact TypeDef or TypeRef Class coded-index token.</summary>
    public int ParentTypeMetadataToken { get; }

    /// <summary>Gets the exact bounded MemberRef name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the exact MemberRef signature.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets the complete TypeRef resolution when Class is TypeRef, otherwise null.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? ParentTypeReferenceResolution { get; }

    /// <summary>Gets the complete exact resolved constructor MethodDef.</summary>
    public StaticFieldMethodDefinitionIdentity ResolvedMethod { get; }

    /// <summary>Gets a defensive copy of the versioned canonical MemberRef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete exact TypeDef/TypeRef-parented MemberRef row and resolution.</summary>
    /// <param name="sourceModule">The source metadata module containing the MemberRef and parent token.</param>
    /// <param name="memberReferenceToken">The non-nil MemberRef token.</param>
    /// <param name="parentTypeMetadataToken">The exact TypeDef or TypeRef Class token.</param>
    /// <param name="name">The exact bounded member name.</param>
    /// <param name="signature">The initialized bounded exact MemberRef signature.</param>
    /// <param name="parentTypeReferenceResolution">Complete resolution only for a TypeRef parent.</param>
    /// <param name="resolvedMethod">The complete exact resolved MethodDef.</param>
    /// <returns>A canonical complete MemberRef row and method resolution.</returns>
    public static StaticFieldMemberReferenceIdentity Create(
        StaticFieldMetadataModuleIdentity sourceModule,
        int memberReferenceToken,
        int parentTypeMetadataToken,
        string name,
        ImmutableArray<byte> signature,
        StaticFieldTypeReferenceResolutionIdentity? parentTypeReferenceResolution,
        StaticFieldMethodDefinitionIdentity resolvedMethod)
    {
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(resolvedMethod);
        CanonicalReplayEncoding.ValidateMetadataToken(memberReferenceToken, 0x0A, nameof(memberReferenceToken));
        var parentIsTypeDefinition = CanonicalReplayEncoding.IsMetadataTokenForTable(parentTypeMetadataToken, 0x02);
        var parentIsTypeReference = CanonicalReplayEncoding.IsMetadataTokenForTable(parentTypeMetadataToken, 0x01);
        if (!parentIsTypeDefinition && !parentIsTypeReference)
        {
            throw new ArgumentOutOfRangeException(nameof(parentTypeMetadataToken), "MemberRef Class must be TypeDef or TypeRef here.");
        }
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        if (signature.IsDefaultOrEmpty || signature.Length > StaticFieldMethodDefinitionIdentity.MaximumMethodSignatureLength ||
            !signature.AsSpan().SequenceEqual(resolvedMethod.Signature.AsSpan()) ||
            !string.Equals(name, resolvedMethod.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("MemberRef name/signature must exactly match its complete resolved MethodDef.");
        }
        var resolvedType = resolvedMethod.DeclaringType;
        if (parentIsTypeDefinition)
        {
            if (parentTypeReferenceResolution is not null || !sourceModule.Equals(resolvedType.MetadataModule) ||
                parentTypeMetadataToken != resolvedType.TypeDefinitionToken)
            {
                throw new ArgumentException("A TypeDef-parented MemberRef must resolve to itself in the source module.");
            }
        }
        else if (parentTypeReferenceResolution is null ||
                 !parentTypeReferenceResolution.SourceModule.Equals(sourceModule) ||
                 parentTypeReferenceResolution.ReferencedType.TypeReferenceToken != parentTypeMetadataToken ||
                 !parentTypeReferenceResolution.ResolvedTargetType.Equals(resolvedType) ||
                 parentTypeReferenceResolution.TargetResolutionKind == StaticFieldTypeReferenceTargetResolutionKind.RuntimeLoader)
        {
            throw new ArgumentException("A TypeRef-parented MemberRef requires one exact non-loader metadata resolution.");
        }
        return new StaticFieldMemberReferenceIdentity(
            sourceModule,
            memberReferenceToken,
            parentTypeMetadataToken,
            name,
            CanonicalReplayEncoding.Copy(signature),
            parentTypeReferenceResolution,
            resolvedMethod);
    }

    /// <summary>Determines whether another value has the same complete MemberRef row and resolved method.</summary>
    /// <param name="other">The MemberRef to compare.</param>
    /// <returns><see langword="true"/> only when every canonical row/resolution fact matches.</returns>
    public bool Equals(StaticFieldMemberReferenceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldMemberReferenceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete FieldDef-owned CustomAttribute row and derives marker classification.</summary>
public sealed class StaticFieldCustomAttributeRowIdentity : IEquatable<StaticFieldCustomAttributeRowIdentity>
{
    /// <summary>Gets the maximum exact CustomAttribute value length admitted before copying.</summary>
    public const int MaximumAttributeValueLength = 4096;

    /// <summary>Gets the canonical deterministic-bound name for CustomAttribute value bytes.</summary>
    public const string MaximumAttributeValueLengthBoundName = "static-field.custom-attribute-value-bytes";

    private const string CanonicalDomain = "static-field-custom-attribute-row-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> value;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldCustomAttributeRowIdentity(
        StaticFieldMetadataModuleIdentity ownerModule,
        int fieldDefinitionToken,
        int customAttributeToken,
        StaticFieldMethodDefinitionIdentity? resolvedConstructor,
        StaticFieldMemberReferenceIdentity? sourceMemberReference,
        StaticFieldMemberReferenceRowIdentity? unresolvedMemberReference,
        ImmutableArray<byte> value,
        StaticFieldTypeAncestryIdentity? attributeTypeAncestry,
        StaticFieldStorageMarkerKind? storageMarkerKind)
    {
        OwnerModule = ownerModule;
        FieldDefinitionToken = fieldDefinitionToken;
        CustomAttributeToken = customAttributeToken;
        ResolvedConstructor = resolvedConstructor;
        SourceMemberReference = sourceMemberReference;
        UnresolvedMemberReference = unresolvedMemberReference;
        this.value = value;
        AttributeTypeAncestry = attributeTypeAncestry;
        StorageMarkerKind = storageMarkerKind;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(ownerModule.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteInt32(customAttributeToken);
        writer.WriteInt32(ConstructorMetadataToken);
        writer.WriteBoolean(resolvedConstructor is not null);
        if (resolvedConstructor is not null)
        {
            writer.WriteLengthPrefixedBytes(resolvedConstructor.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(sourceMemberReference is not null);
        if (sourceMemberReference is not null)
        {
            writer.WriteLengthPrefixedBytes(sourceMemberReference.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(unresolvedMemberReference is not null);
        if (unresolvedMemberReference is not null)
        {
            writer.WriteLengthPrefixedBytes(unresolvedMemberReference.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(value.AsSpan());
        writer.WriteBoolean(attributeTypeAncestry is not null);
        if (attributeTypeAncestry is not null)
        {
            writer.WriteLengthPrefixedBytes(attributeTypeAncestry.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(storageMarkerKind.HasValue);
        if (storageMarkerKind.HasValue)
        {
            writer.WriteInt32((int)storageMarkerKind.Value);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact source metadata module containing this row.</summary>
    public StaticFieldMetadataModuleIdentity OwnerModule { get; }

    /// <summary>Gets the exact FieldDef parent token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the non-nil CustomAttribute token.</summary>
    public int CustomAttributeToken { get; }

    /// <summary>Gets the complete exact resolved constructor MethodDef.</summary>
    public StaticFieldMethodDefinitionIdentity? ResolvedConstructor { get; }

    /// <summary>Gets the complete source MemberRef row, or null when CustomAttribute.Type is MethodDef.</summary>
    public StaticFieldMemberReferenceIdentity? SourceMemberReference { get; }

    /// <summary>Gets the exact unresolved source MemberRef row for a proven non-marker, otherwise null.</summary>
    public StaticFieldMemberReferenceRowIdentity? UnresolvedMemberReference { get; }

    /// <summary>Gets the exact MethodDef or MemberRef token encoded by CustomAttribute.Type.</summary>
    public int ConstructorMetadataToken =>
        SourceMemberReference?.MemberReferenceToken ??
        UnresolvedMemberReference?.MemberReferenceToken ??
        ResolvedConstructor!.MethodDefinitionToken;

    /// <summary>Gets a defensive copy of the exact bounded CustomAttribute value blob.</summary>
    public ImmutableArray<byte> Value => CanonicalReplayEncoding.Copy(value);

    /// <summary>Gets exact ancestry for the resolved attribute TypeDef.</summary>
    public StaticFieldTypeAncestryIdentity? AttributeTypeAncestry { get; }

    /// <summary>Gets the derived recognized storage marker, or null for a proven non-marker row.</summary>
    public StaticFieldStorageMarkerKind? StorageMarkerKind { get; }

    /// <summary>Gets a defensive copy of the versioned canonical CustomAttribute-row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the declared CustomAttribute value-byte bound.</summary>
    public static EvaluationDeterministicBound DeclaredAttributeValueLengthBound =>
        new(MaximumAttributeValueLengthBoundName, MaximumAttributeValueLength);

    /// <summary>Creates one complete exact row and derives recognized marker status from resolved core-library identity.</summary>
    /// <param name="ownerModule">The source metadata module containing the CustomAttribute row.</param>
    /// <param name="fieldDefinitionToken">The exact FieldDef parent token.</param>
    /// <param name="customAttributeToken">The non-nil CustomAttribute token.</param>
    /// <param name="resolvedConstructor">The complete exact resolved constructor MethodDef.</param>
    /// <param name="sourceMemberReference">The complete MemberRef row when CustomAttribute.Type uses MemberRef.</param>
    /// <param name="value">The initialized bounded exact CustomAttribute value blob.</param>
    /// <param name="attributeTypeAncestry">Exact reference-class ancestry for the constructor's declaring TypeDef.</param>
    /// <returns>A canonical row classified as exact marker or exact non-marker.</returns>
    public static StaticFieldCustomAttributeRowIdentity Create(
        StaticFieldMetadataModuleIdentity ownerModule,
        int fieldDefinitionToken,
        int customAttributeToken,
        StaticFieldMethodDefinitionIdentity resolvedConstructor,
        StaticFieldMemberReferenceIdentity? sourceMemberReference,
        ImmutableArray<byte> value,
        StaticFieldTypeAncestryIdentity attributeTypeAncestry)
    {
        ArgumentNullException.ThrowIfNull(ownerModule);
        ArgumentNullException.ThrowIfNull(resolvedConstructor);
        ArgumentNullException.ThrowIfNull(attributeTypeAncestry);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(customAttributeToken, 0x0C, nameof(customAttributeToken));
        if (value.IsDefault || value.Length > MaximumAttributeValueLength)
        {
            throw new ArgumentException(
                $"An initialized CustomAttribute value of at most {MaximumAttributeValueLength} bytes is required.",
                nameof(value));
        }
        if (sourceMemberReference is null)
        {
            if (!ownerModule.Equals(resolvedConstructor.DeclaringType.MetadataModule))
            {
                throw new ArgumentException("A MethodDef CustomAttribute constructor must be in the exact owner module.");
            }
        }
        else if (!sourceMemberReference.SourceModule.Equals(ownerModule) ||
                 !sourceMemberReference.ResolvedMethod.Equals(resolvedConstructor))
        {
            throw new ArgumentException("The source MemberRef does not resolve this exact constructor from the owner module.");
        }
        if (!attributeTypeAncestry.SubjectType.Equals(resolvedConstructor.DeclaringType) ||
            attributeTypeAncestry.Classification != StaticFieldTypeClassification.ReferenceClass)
        {
            throw new ArgumentException("CustomAttribute constructor and exact attribute-type ancestry disagree.");
        }
        var methodFlags = (MethodAttributes)resolvedConstructor.Attributes;
        if (!string.Equals(resolvedConstructor.Name, ".ctor", StringComparison.Ordinal) ||
            (methodFlags & System.Reflection.MethodAttributes.Static) != 0 ||
            (methodFlags & System.Reflection.MethodAttributes.SpecialName) == 0 ||
            (methodFlags & System.Reflection.MethodAttributes.RTSpecialName) == 0 ||
            (methodFlags & System.Reflection.MethodAttributes.MemberAccessMask) != System.Reflection.MethodAttributes.Public)
        {
            throw new ArgumentException("A CustomAttribute constructor must be exact public instance special-name .ctor MethodDef.");
        }

        StaticFieldStorageMarkerKind? marker = null;
        var target = attributeTypeAncestry.SubjectType;
        var coreLibrary = attributeTypeAncestry.CoreLibrary;
        if (coreLibrary is not null && target.MetadataModule.Equals(coreLibrary.MetadataModule) &&
            string.Equals(target.NamespaceName, "System", StringComparison.Ordinal))
        {
            marker = target.TypeName switch
            {
                "ThreadStaticAttribute" => StaticFieldStorageMarkerKind.ThreadStatic,
                "ContextStaticAttribute" => StaticFieldStorageMarkerKind.ContextStatic,
                _ => null,
            };
        }
        if (marker.HasValue)
        {
            var typeFlags = (TypeAttributes)target.TypeAttributes;
            if (!target.IsTopLevel || target.GenericParameterCount != 0 || target.IsAbstract ||
                (typeFlags & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public ||
                !resolvedConstructor.Signature.AsSpan().SequenceEqual(new byte[] { 0x20, 0x00, 0x01 }) ||
                !value.AsSpan().SequenceEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }))
            {
                throw new ArgumentException("A recognized storage marker has contradictory type/constructor/value evidence.");
            }
        }
        return new StaticFieldCustomAttributeRowIdentity(
            ownerModule,
            fieldDefinitionToken,
            customAttributeToken,
            resolvedConstructor,
            sourceMemberReference,
            unresolvedMemberReference: null,
            CanonicalReplayEncoding.Copy(value),
            attributeTypeAncestry,
            marker);
    }

    /// <summary>Creates a proven non-marker row whose CustomAttribute.Type directly names a MethodDef.</summary>
    /// <param name="ownerModule">The exact module containing the CustomAttribute and MethodDef rows.</param>
    /// <param name="fieldDefinitionToken">The exact FieldDef Parent token.</param>
    /// <param name="customAttributeToken">The non-nil CustomAttribute row token.</param>
    /// <param name="constructor">The complete exact directly named MethodDef.</param>
    /// <param name="value">The initialized bounded exact CustomAttribute value blob.</param>
    /// <returns>A canonical exact row proven not to be either recognized storage marker.</returns>
    public static StaticFieldCustomAttributeRowIdentity CreateProvenNonMarkerFromMethodDefinition(
        StaticFieldMetadataModuleIdentity ownerModule,
        int fieldDefinitionToken,
        int customAttributeToken,
        StaticFieldMethodDefinitionIdentity constructor,
        ImmutableArray<byte> value)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        ValidateBasicRow(ownerModule, fieldDefinitionToken, customAttributeToken, value);
        if (!constructor.DeclaringType.MetadataModule.Equals(ownerModule) ||
            !string.Equals(constructor.Name, ".ctor", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A directly named non-marker constructor must be an exact same-module .ctor MethodDef.",
                nameof(constructor));
        }
        if (CouldBeRuntimeStorageMarker(constructor.DeclaringType))
        {
            throw new ArgumentException(
                "A storage-marker name in a possible runtime core library requires resolved ancestry proof and cannot be downgraded.",
                nameof(constructor));
        }
        return new StaticFieldCustomAttributeRowIdentity(
            ownerModule,
            fieldDefinitionToken,
            customAttributeToken,
            constructor,
            sourceMemberReference: null,
            unresolvedMemberReference: null,
            CanonicalReplayEncoding.Copy(value),
            attributeTypeAncestry: null,
            storageMarkerKind: null);
    }

    /// <summary>Creates a proven non-marker row from an exact unresolved TypeDef, TypeRef, or TypeSpec-parented MemberRef.</summary>
    /// <param name="ownerModule">The exact module containing the CustomAttribute and MemberRef rows.</param>
    /// <param name="fieldDefinitionToken">The exact FieldDef Parent token.</param>
    /// <param name="customAttributeToken">The non-nil CustomAttribute row token.</param>
    /// <param name="constructorMemberReference">The exact unresolved source MemberRef row and Parent evidence.</param>
    /// <param name="value">The initialized bounded exact CustomAttribute value blob.</param>
    /// <returns>A canonical exact row whose Parent shape or exact source type name excludes both markers.</returns>
    public static StaticFieldCustomAttributeRowIdentity CreateProvenNonMarkerFromMemberReference(
        StaticFieldMetadataModuleIdentity ownerModule,
        int fieldDefinitionToken,
        int customAttributeToken,
        StaticFieldMemberReferenceRowIdentity constructorMemberReference,
        ImmutableArray<byte> value)
    {
        ArgumentNullException.ThrowIfNull(constructorMemberReference);
        ValidateBasicRow(ownerModule, fieldDefinitionToken, customAttributeToken, value);
        if (!constructorMemberReference.SourceModule.Equals(ownerModule) ||
            !string.Equals(constructorMemberReference.Name, ".ctor", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An unresolved non-marker constructor must be one exact owner-module .ctor MemberRef row.",
                nameof(constructorMemberReference));
        }

        var provenNonMarker = constructorMemberReference.ParentKind switch
        {
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeDefinition =>
                !CouldBeRuntimeStorageMarker(constructorMemberReference.ParentTypeDefinition!),
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeReference =>
                !HasStorageMarkerSourceName(constructorMemberReference.ParentTypeReference!),
            StaticFieldCustomAttributeMemberReferenceParentKind.TypeSpecification => true,
            _ => false,
        };
        if (!provenNonMarker)
        {
            throw new ArgumentException(
                "An unresolved ThreadStatic/ContextStatic source-name candidate cannot be classified as a non-marker.",
                nameof(constructorMemberReference));
        }
        return new StaticFieldCustomAttributeRowIdentity(
            ownerModule,
            fieldDefinitionToken,
            customAttributeToken,
            resolvedConstructor: null,
            sourceMemberReference: null,
            constructorMemberReference,
            CanonicalReplayEncoding.Copy(value),
            attributeTypeAncestry: null,
            storageMarkerKind: null);
    }

    private static void ValidateBasicRow(
        StaticFieldMetadataModuleIdentity ownerModule,
        int fieldDefinitionToken,
        int customAttributeToken,
        ImmutableArray<byte> value)
    {
        ArgumentNullException.ThrowIfNull(ownerModule);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(customAttributeToken, 0x0C, nameof(customAttributeToken));
        if (value.IsDefault || value.Length > MaximumAttributeValueLength)
        {
            throw new ArgumentException(
                $"An initialized CustomAttribute value of at most {MaximumAttributeValueLength} bytes is required.",
                nameof(value));
        }
    }

    private static bool CouldBeRuntimeStorageMarker(StaticFieldTypeDefinitionIdentity type) =>
        type.IsTopLevel &&
        type.AssemblyDefinition.HasRuntimeCoreLibraryName &&
        HasStorageMarkerSourceName(type.NamespaceName, type.TypeName);

    private static bool HasStorageMarkerSourceName(StaticFieldTypeReferenceRowIdentity type) =>
        HasStorageMarkerSourceName(type.NamespaceName, type.TypeName);

    private static bool HasStorageMarkerSourceName(string namespaceName, string typeName) =>
        string.Equals(namespaceName, "System", StringComparison.Ordinal) &&
        typeName is "ThreadStaticAttribute" or "ContextStaticAttribute";

    /// <summary>Determines whether another value has the same complete CustomAttribute row and resolution.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> only when every canonical row fact matches.</returns>
    public bool Equals(StaticFieldCustomAttributeRowIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldCustomAttributeRowIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Proves an exhaustive bounded CustomAttribute projection for one exact FieldDef parent.</summary>
public sealed class StaticFieldFieldCustomAttributeProjection : IEquatable<StaticFieldFieldCustomAttributeProjection>
{
    /// <summary>Gets the maximum FieldDef-owned CustomAttribute row count admitted before copying.</summary>
    public const int MaximumOwnedCustomAttributeRowCount = 256;

    /// <summary>Gets the canonical deterministic-bound name for per-field CustomAttribute traversal.</summary>
    public const string MaximumOwnedCustomAttributeRowCountBoundName =
        "static-field.field-custom-attribute-rows";

    private const string CanonicalDomain = "static-field-field-custom-attribute-projection";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldCustomAttributeRowIdentity> customAttributeRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldFieldCustomAttributeProjection(
        int fieldDefinitionToken,
        int ownedRowsExamined,
        int ownedRowCount,
        ImmutableArray<StaticFieldCustomAttributeRowIdentity> customAttributeRows)
    {
        FieldDefinitionToken = fieldDefinitionToken;
        OwnedRowsExamined = ownedRowsExamined;
        OwnedRowCount = ownedRowCount;
        this.customAttributeRows = customAttributeRows;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteInt32(ownedRowsExamined);
        writer.WriteInt32(ownedRowCount);
        writer.WriteInt32(customAttributeRows.Length);
        foreach (var row in customAttributeRows)
        {
            writer.WriteLengthPrefixedBytes(row.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact FieldDef parent token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact number of FieldDef-owned CustomAttribute rows examined.</summary>
    public int OwnedRowsExamined { get; }

    /// <summary>Gets the complete exact count of CustomAttribute rows owned by the FieldDef.</summary>
    public int OwnedRowCount { get; }

    /// <summary>Gets whether the retained projection exhaustively examined every owned row.</summary>
    public bool IsExhaustive => OwnedRowsExamined == OwnedRowCount;

    /// <summary>Gets a defensive metadata-token-order copy of every exact owned CustomAttribute row.</summary>
    public ImmutableArray<StaticFieldCustomAttributeRowIdentity> CustomAttributeRows =>
        StaticFieldContractEncoding.Copy(customAttributeRows);

    /// <summary>Gets a defensive metadata-token-order copy of recognized storage-marker rows.</summary>
    public ImmutableArray<StaticFieldCustomAttributeRowIdentity> StorageMarkerRows =>
        customAttributeRows.Where(static row => row.StorageMarkerKind.HasValue).ToImmutableArray();

    /// <summary>Gets whether an exact ThreadStatic marker row was found.</summary>
    public bool IsThreadStatic =>
        customAttributeRows.Any(static row => row.StorageMarkerKind == StaticFieldStorageMarkerKind.ThreadStatic);

    /// <summary>Gets whether an exact ContextStatic marker row was found.</summary>
    public bool IsContextStatic =>
        customAttributeRows.Any(static row => row.StorageMarkerKind == StaticFieldStorageMarkerKind.ContextStatic);

    /// <summary>Gets the declared per-field CustomAttribute row bound.</summary>
    public static EvaluationDeterministicBound DeclaredOwnedCustomAttributeRowCountBound =>
        new(MaximumOwnedCustomAttributeRowCountBoundName, MaximumOwnedCustomAttributeRowCount);

    /// <summary>Gets a defensive copy of the versioned canonical projection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact exhaustive bounded per-field CustomAttribute projection.</summary>
    /// <param name="fieldDefinitionToken">The exact non-nil FieldDef parent token.</param>
    /// <param name="ownedRowsExamined">The exact number of owned rows examined.</param>
    /// <param name="ownedRowCount">The complete exact owned-row count; equality proves exhaustion.</param>
    /// <param name="customAttributeRows">Every complete exact owned row, including proven non-marker rows.</param>
    /// <returns>A canonical projection from which both storage-marker flags are derived.</returns>
    public static StaticFieldFieldCustomAttributeProjection Create(
        int fieldDefinitionToken,
        int ownedRowsExamined,
        int ownedRowCount,
        ImmutableArray<StaticFieldCustomAttributeRowIdentity> customAttributeRows)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        if (ownedRowsExamined < 0 || ownedRowCount < 0 || ownedRowsExamined != ownedRowCount ||
            ownedRowCount > MaximumOwnedCustomAttributeRowCount)
        {
            throw new ArgumentException(
                $"An exact field projection must exhaust 0..{MaximumOwnedCustomAttributeRowCount} owned CustomAttribute rows.");
        }
        if (customAttributeRows.IsDefault ||
            customAttributeRows.Length > MaximumOwnedCustomAttributeRowCount ||
            customAttributeRows.Length != ownedRowCount)
        {
            throw new ArgumentException(
                "The initialized retained row set must contain every owned CustomAttribute row and stay within the bound.",
                nameof(customAttributeRows));
        }
        var copiedRows = StaticFieldContractEncoding.RequireAndCopy(customAttributeRows, nameof(customAttributeRows)).ToArray();
        Array.Sort(copiedRows, static (left, right) => left.CustomAttributeToken.CompareTo(right.CustomAttributeToken));
        var normalized = ImmutableArray.CreateRange(copiedRows);
        if (normalized.Any(row => row.FieldDefinitionToken != fieldDefinitionToken) ||
            normalized.Select(static row => row.CustomAttributeToken).Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException(
                "Every retained row must have this FieldDef parent and a unique CustomAttribute token.",
                nameof(customAttributeRows));
        }
        if (normalized
            .Where(static row => row.StorageMarkerKind.HasValue)
            .Select(static row => row.StorageMarkerKind!.Value)
            .Distinct()
            .Count() != normalized.Count(static row => row.StorageMarkerKind.HasValue))
        {
            throw new ArgumentException(
                "One FieldDef cannot carry duplicate recognized storage-marker roles.",
                nameof(customAttributeRows));
        }
        return new StaticFieldFieldCustomAttributeProjection(
            fieldDefinitionToken,
            ownedRowsExamined,
            ownedRowCount,
            normalized);
    }

    /// <summary>Determines whether another value has the same counts and exact marker rows.</summary>
    /// <param name="other">The projection to compare.</param>
    /// <returns><see langword="true"/> only when every canonical projection fact matches.</returns>
    public bool Equals(StaticFieldFieldCustomAttributeProjection? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldFieldCustomAttributeProjection);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete exact FieldDef row, direct owner relation, signature, and CustomAttribute rows.</summary>
public sealed class StaticFieldDefinitionIdentity : IEquatable<StaticFieldDefinitionIdentity>
{
    private const string CanonicalDomain = "static-field-definition-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldDefinitionIdentity(
        StaticFieldTypeDefinitionIdentity declaringType,
        int fieldDefinitionToken,
        string name,
        int attributes,
        ImmutableArray<byte> signature,
        StaticFieldFieldCustomAttributeProjection customAttributes)
    {
        DeclaringType = declaringType;
        FieldDefinitionToken = fieldDefinitionToken;
        Name = name;
        Attributes = attributes;
        this.signature = signature;
        CustomAttributes = customAttributes;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(declaringType.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteString(name);
        writer.WriteInt32(attributes);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        writer.WriteLengthPrefixedBytes(customAttributes.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact declaring TypeDef and FieldList owner range.</summary>
    public StaticFieldTypeDefinitionIdentity DeclaringType { get; }

    /// <summary>Gets the exact non-nil FieldDef token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact decoded FieldDef name.</summary>
    public string Name { get; }

    /// <summary>Gets the exact raw FieldAttributes bits.</summary>
    public int Attributes { get; }

    /// <summary>Gets a defensive copy of the exact Field signature blob.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets the exact exhaustive bounded CustomAttribute-row projection for this FieldDef.</summary>
    public StaticFieldFieldCustomAttributeProjection CustomAttributes { get; }

    /// <summary>Gets whether exact marker evidence proves ThreadStatic storage.</summary>
    public bool IsThreadStatic => CustomAttributes.IsThreadStatic;

    /// <summary>Gets whether exact marker evidence proves ContextStatic storage.</summary>
    public bool IsContextStatic => CustomAttributes.IsContextStatic;

    /// <summary>Gets a defensive copy of the versioned canonical FieldDef bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete exact directly owned FieldDef identity.</summary>
    /// <param name="declaringType">The exact TypeDef whose FieldList interval directly owns the row.</param>
    /// <param name="fieldDefinitionToken">The non-nil FieldDef token inside that interval.</param>
    /// <param name="name">The exact bounded decoded field name.</param>
    /// <param name="attributes">The exact raw FieldAttributes bits.</param>
    /// <param name="signature">The initialized exact bounded FieldSig blob.</param>
    /// <param name="customAttributes">The exact exhaustive CustomAttribute-row projection for this FieldDef.</param>
    /// <returns>A canonical exact FieldDef row and direct-owner relation.</returns>
    public static StaticFieldDefinitionIdentity Create(
        StaticFieldTypeDefinitionIdentity declaringType,
        int fieldDefinitionToken,
        string name,
        int attributes,
        ImmutableArray<byte> signature,
        StaticFieldFieldCustomAttributeProjection customAttributes)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(customAttributes);
        StaticFieldMetadataTextLimits.Validate(name, nameof(name), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(name, nameof(name), allowDots: false);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        if (!declaringType.DirectlyDeclaresField(fieldDefinitionToken))
        {
            throw new ArgumentException("The FieldDef row lies outside the declaring TypeDef's exact FieldList interval.");
        }
        if (signature.IsDefaultOrEmpty || signature.Length > StaticFieldFieldSignatureLimits.MaximumFieldSignatureLength)
        {
            throw new ArgumentException(
                $"An exact FieldSig of 1..{StaticFieldFieldSignatureLimits.MaximumFieldSignatureLength} bytes is required.",
                nameof(signature));
        }
        if (customAttributes.FieldDefinitionToken != fieldDefinitionToken || !customAttributes.IsExhaustive)
        {
            throw new ArgumentException("The CustomAttribute projection must exhaust this exact FieldDef parent.", nameof(customAttributes));
        }
        if (customAttributes.CustomAttributeRows.Any(
                row => !row.OwnerModule.Equals(declaringType.MetadataModule)))
        {
            throw new ArgumentException(
                "Every retained CustomAttribute row must belong to the exact FieldDef metadata module.",
                nameof(customAttributes));
        }
        return new StaticFieldDefinitionIdentity(
            declaringType,
            fieldDefinitionToken,
            name,
            attributes,
            CanonicalReplayEncoding.Copy(signature),
            customAttributes);
    }

    /// <summary>Determines whether another value has the same exact owner, row, signature, and marker projection.</summary>
    /// <param name="other">The field identity to compare.</param>
    /// <returns><see langword="true"/> only when every canonical FieldDef fact matches.</returns>
    public bool Equals(StaticFieldDefinitionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldDefinitionIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Classifies the terminal result of bounded static-field declaration binding.</summary>
public enum StaticFieldBindingStatus
{
    /// <summary>Exactly one complete declaration-and-syntax interpretation remains.</summary>
    Exact = 1,

    /// <summary>An exact exhaustive search proved that no complete declaration exists.</summary>
    Absent = 2,

    /// <summary>Some required context or module evidence was partial.</summary>
    Partial = 3,

    /// <summary>Required context or module evidence was unavailable.</summary>
    Unavailable = 4,

    /// <summary>Multiple complete symbol interpretations or incompatible exact context sources remain.</summary>
    Ambiguous = 5,

    /// <summary>Available context, module, or declaration facts disagreed.</summary>
    Conflict = 6,

    /// <summary>A descriptor, context, or metadata fact violated a structural invariant.</summary>
    Invalid = 7,

    /// <summary>The resolved expansion or declaration shape is outside the version-one binder.</summary>
    Unsupported = 8,
}

/// <summary>Identifies the first source-specific issue retained by a static-field binding outcome.</summary>
public enum StaticFieldBindingIssue
{
    /// <summary>No binding issue occurred; valid only for exact binding.</summary>
    None = 0,

    /// <summary>Exact exhaustive expansion and module searches found no declaration.</summary>
    DeclarationAbsent = 1,

    /// <summary>Selected-frame/import context was only partially observed.</summary>
    ContextPartial = 101,

    /// <summary>At least one relevant module search was partial.</summary>
    ModuleSearchPartial = 102,

    /// <summary>A declared binding or traversal bound prevented exhaustive search.</summary>
    SearchBoundReached = 103,

    /// <summary>Required selected-frame/import context was unavailable.</summary>
    ContextUnavailable = 201,

    /// <summary>At least one relevant module metadata source was unavailable.</summary>
    ModuleUnavailable = 202,

    /// <summary>Two or more complete declaration-and-syntax interpretations remain.</summary>
    MultipleCandidates = 301,

    /// <summary>Selected-frame or import context itself retained multiple incompatible exact sources.</summary>
    ContextAmbiguous = 302,

    /// <summary>Selected-frame/import context facts disagreed.</summary>
    ContextConflict = 401,

    /// <summary>At least one runtime-module or metadata identity fact disagreed.</summary>
    ModuleConflict = 402,

    /// <summary>Complete declaration facts disagreed on token, name, signature, or module identity.</summary>
    DeclarationConflict = 403,

    /// <summary>The detached syntax descriptor violated the binder's structural preconditions.</summary>
    DescriptorInvalid = 501,

    /// <summary>Selected-frame/import context was structurally invalid.</summary>
    ContextInvalid = 502,

    /// <summary>At least one counted module metadata source was malformed.</summary>
    MetadataInvalid = 503,

    /// <summary>The requested contextual expansion kind is retained but not admitted by version one.</summary>
    ExpansionUnsupported = 601,

    /// <summary>A located declaration uses a storage or signature shape outside version one.</summary>
    DeclarationShapeUnsupported = 602,

    /// <summary>The consulted selected-frame or Portable-PDB context uses an unsupported representation.</summary>
    ContextUnsupported = 603,
}

/// <summary>Freezes one path-independent runtime module instance within one immutable dump snapshot.</summary>
/// <remarks>
/// Physically distinct application-domain/module pairs remain distinct even when their counted metadata content is
/// byte-identical. Display names and target or analysis-machine paths are intentionally absent.
/// </remarks>
public sealed class StaticFieldModuleInstanceIdentity : IEquatable<StaticFieldModuleInstanceIdentity>
{
    private const string CanonicalDomain = "static-field-module-instance-identity";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldModuleInstanceIdentity(
        string snapshotSha256,
        int pointerWidth,
        ulong applicationDomainAddress,
        ulong moduleAddress,
        ulong imageBase,
        ulong imageSize)
    {
        SnapshotSha256 = snapshotSha256;
        PointerWidth = pointerWidth;
        ApplicationDomainAddress = applicationDomainAddress;
        ModuleAddress = moduleAddress;
        ImageBase = imageBase;
        ImageSize = imageSize;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(snapshotSha256, nameof(snapshotSha256));
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(applicationDomainAddress);
        writer.WriteUInt64(moduleAddress);
        writer.WriteUInt64(imageBase);
        writer.WriteUInt64(imageSize);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the lowercase SHA-256 digest of the complete containing dump snapshot.</summary>
    public string SnapshotSha256 { get; }

    /// <summary>Gets the exact target pointer width in bytes, either four or eight.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the nonzero target address of the uniquely mapped owning application-domain runtime structure.</summary>
    public ulong ApplicationDomainAddress { get; }

    /// <summary>Gets the nonzero target address of the loaded CLR module structure.</summary>
    public ulong ModuleAddress { get; }

    /// <summary>Gets the runtime-reported mapped-image base, or zero when no mapped base applies.</summary>
    public ulong ImageBase { get; }

    /// <summary>Gets the runtime-reported mapped-image extent, or zero for a module without a mapped image.</summary>
    public ulong ImageSize { get; }

    /// <summary>Gets a defensive copy of the versioned canonical identity bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a complete snapshot-scoped runtime module identity.</summary>
    /// <param name="snapshotSha256">The complete dump-file SHA-256 digest.</param>
    /// <param name="pointerWidth">The exact target pointer width in bytes, either four or eight.</param>
    /// <param name="applicationDomainAddress">The nonzero uniquely mapped owning application-domain address.</param>
    /// <param name="moduleAddress">The nonzero CLR module address.</param>
    /// <param name="imageBase">The mapped-image base, or zero when inapplicable.</param>
    /// <param name="imageSize">The mapped-image extent, or zero when inapplicable.</param>
    /// <returns>A path-independent immutable module-instance identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="snapshotSha256"/> is not a complete SHA-256 digest.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required runtime address is zero or the image range overflows.</exception>
    public static StaticFieldModuleInstanceIdentity Create(
        string snapshotSha256,
        int pointerWidth,
        ulong applicationDomainAddress,
        ulong moduleAddress,
        ulong imageBase,
        ulong imageSize)
    {
        var normalizedSnapshot = CanonicalReplayEncoding.NormalizeSha256(snapshotSha256, nameof(snapshotSha256));
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(
            applicationDomainAddress,
            pointerWidth,
            allowZero: false,
            nameof(applicationDomainAddress));
        CanonicalReplayEncoding.ValidatePointerValue(
            moduleAddress,
            pointerWidth,
            allowZero: false,
            nameof(moduleAddress));
        CanonicalReplayEncoding.ValidatePointerValue(imageBase, pointerWidth, allowZero: true, nameof(imageBase));
        var maximumAddress = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((imageBase == 0) != (imageSize == 0) || imageSize > maximumAddress ||
            imageSize > 0 && imageBase > maximumAddress - (imageSize - 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(imageSize),
                "Mapped-image base/extent presence must agree and the range must fit the target pointer width.");
        }

        return new StaticFieldModuleInstanceIdentity(
            normalizedSnapshot,
            pointerWidth,
            applicationDomainAddress,
            moduleAddress,
            imageBase,
            imageSize);
    }

    /// <summary>Determines whether another identity names the same physical module instance.</summary>
    /// <param name="other">The module identity to compare.</param>
    /// <returns><see langword="true"/> only when snapshot and all runtime coordinates match.</returns>
    public bool Equals(StaticFieldModuleInstanceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldModuleInstanceIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>
/// Freezes counted AssemblyRef/TypeRef narrowing from one selected source module to one candidate target declaration.
/// </summary>
/// <remarks>
/// This product-owned fact prevents a token-bearing Portable-PDB import from degenerating into an unqualified
/// same-name search. It contains no resolver or metadata reader. A TypeDef source token is legal only for a same-
/// module target; TypeRef relations retain the complete legal Module/ModuleRef/AssemblyRef ResolutionScope union.
/// </remarks>
public sealed class StaticFieldReferenceResolutionFact : IEquatable<StaticFieldReferenceResolutionFact>
{
    private const string CanonicalDomain = "static-field-reference-resolution-fact";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldReferenceResolutionFact(
        string contextFactSha256,
        StaticFieldModuleInstanceIdentity sourceModule,
        ModuleContentIdentity sourceModuleContent,
        int? assemblyReferenceToken,
        int? sourceTypeToken,
        StaticFieldTypeReferenceResolutionIdentity? typeReferenceResolution,
        StaticFieldModuleInstanceIdentity targetModule,
        ModuleContentIdentity targetModuleContent,
        int targetTypeDefinitionToken)
    {
        ContextFactSha256 = contextFactSha256;
        SourceModule = sourceModule;
        SourceModuleContent = sourceModuleContent;
        AssemblyReferenceToken = assemblyReferenceToken;
        SourceTypeToken = sourceTypeToken;
        TypeReferenceResolution = typeReferenceResolution;
        TargetModule = targetModule;
        TargetModuleContent = targetModuleContent;
        TargetTypeDefinitionToken = targetTypeDefinitionToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(contextFactSha256, nameof(contextFactSha256));
        writer.WriteLengthPrefixedBytes(sourceModule.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteModuleContent(writer, sourceModuleContent);
        writer.WriteBoolean(assemblyReferenceToken.HasValue);
        if (assemblyReferenceToken.HasValue)
        {
            writer.WriteInt32(assemblyReferenceToken.Value);
        }

        writer.WriteBoolean(sourceTypeToken.HasValue);
        if (sourceTypeToken.HasValue)
        {
            writer.WriteInt32(sourceTypeToken.Value);
        }

        writer.WriteBoolean(typeReferenceResolution is not null);
        if (typeReferenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(typeReferenceResolution.CanonicalBytes.AsSpan());
        }

        writer.WriteLengthPrefixedBytes(targetModule.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteModuleContent(writer, targetModuleContent);
        writer.WriteInt32(targetTypeDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the digest of the exact consulted Portable-PDB import fact being resolved.</summary>
    public string ContextFactSha256 { get; }

    /// <summary>Gets the selected-frame module whose metadata owns the reference token.</summary>
    public StaticFieldModuleInstanceIdentity SourceModule { get; }

    /// <summary>Gets the complete counted metadata identity in which the reference token was decoded.</summary>
    public ModuleContentIdentity SourceModuleContent { get; }

    /// <summary>
    /// Gets the non-nil AssemblyRef token carried by an assembly-qualified import or reached as the root of a
    /// type-bearing import's complete TypeRef resolution, when either relation applies.
    /// </summary>
    public int? AssemblyReferenceToken { get; }

    /// <summary>Gets the source TypeDef or TypeRef token for a type-bearing alias, when one applies.</summary>
    public int? SourceTypeToken { get; }

    /// <summary>Gets the complete closed Module/ModuleRef/AssemblyRef TypeRef relation for a TypeRef-bearing import.</summary>
    public StaticFieldTypeReferenceResolutionIdentity? TypeReferenceResolution { get; }

    /// <summary>Gets the exact physical loaded module instance selected by counted reference resolution.</summary>
    public StaticFieldModuleInstanceIdentity TargetModule { get; }

    /// <summary>Gets the complete counted metadata identity of the selected target module.</summary>
    public ModuleContentIdentity TargetModuleContent { get; }

    /// <summary>Gets the exact target TypeDef token to which the candidate declaration must belong.</summary>
    public int TargetTypeDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the versioned canonical reference-resolution bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact counted import-reference resolution fact.</summary>
    /// <param name="contextFactSha256">Digest of the exact consulted import fact.</param>
    /// <param name="sourceModule">Selected-frame module owning the AssemblyRef/TypeRef/TypeDef token.</param>
    /// <param name="sourceModuleContent">Complete counted source-module metadata identity.</param>
    /// <param name="assemblyReferenceToken">AssemblyRef token for an assembly-qualified namespace import.</param>
    /// <param name="sourceTypeToken">A same-module TypeDef token for a type-bearing import, otherwise null.</param>
    /// <param name="targetModule">Exact loaded target module selected by the counted relationship.</param>
    /// <param name="targetModuleContent">Complete counted target-module metadata identity.</param>
    /// <param name="targetTypeDefinitionToken">Exact target TypeDef token of the candidate declaring type.</param>
    /// <returns>A detached resolution fact that can narrow one candidate without using module order or names.</returns>
    /// <exception cref="ArgumentException">
    /// Snapshots differ, no source token is supplied, a same-module TypeDef relation disagrees, or content is missing.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A metadata token is nil or belongs to an unsupported table.</exception>
    public static StaticFieldReferenceResolutionFact Create(
        string contextFactSha256,
        StaticFieldModuleInstanceIdentity sourceModule,
        ModuleContentIdentity sourceModuleContent,
        int? assemblyReferenceToken,
        int? sourceTypeToken,
        StaticFieldModuleInstanceIdentity targetModule,
        ModuleContentIdentity targetModuleContent,
        int targetTypeDefinitionToken)
    {
        var normalizedContextFact = CanonicalReplayEncoding.NormalizeSha256(contextFactSha256, nameof(contextFactSha256));
        ArgumentNullException.ThrowIfNull(sourceModule);
        ArgumentNullException.ThrowIfNull(sourceModuleContent);
        ArgumentNullException.ThrowIfNull(targetModule);
        ArgumentNullException.ThrowIfNull(targetModuleContent);
        if (!string.Equals(sourceModule.SnapshotSha256, targetModule.SnapshotSha256, StringComparison.Ordinal) ||
            sourceModule.ApplicationDomainAddress != targetModule.ApplicationDomainAddress)
        {
            throw new ArgumentException(
                "Reference-resolution source and target must share one snapshot and application domain.");
        }

        if (!assemblyReferenceToken.HasValue && !sourceTypeToken.HasValue)
        {
            throw new ArgumentException("An AssemblyRef, TypeDef, or TypeRef source token is required.");
        }

        if (assemblyReferenceToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(
                assemblyReferenceToken.Value,
                0x23,
                nameof(assemblyReferenceToken));
        }

        if (sourceTypeToken.HasValue &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(sourceTypeToken.Value, 0x01) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(sourceTypeToken.Value, 0x02))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceTypeToken),
                "Only a non-nil TypeRef or TypeDef source token is admitted.");
        }
        if (sourceTypeToken.HasValue &&
            CanonicalReplayEncoding.IsMetadataTokenForTable(sourceTypeToken.Value, 0x01))
        {
            throw new ArgumentException(
                "A TypeRef source relation requires the complete closed TypeRef-resolution factory.",
                nameof(sourceTypeToken));
        }

        CanonicalReplayEncoding.ValidateMetadataToken(
            targetTypeDefinitionToken,
            0x02,
            nameof(targetTypeDefinitionToken));
        if (sourceTypeToken.HasValue &&
            CanonicalReplayEncoding.IsMetadataTokenForTable(sourceTypeToken.Value, 0x02) &&
            (!sourceModule.Equals(targetModule) ||
             !sourceModuleContent.Equals(targetModuleContent) ||
             sourceTypeToken.Value != targetTypeDefinitionToken ||
             assemblyReferenceToken.HasValue))
        {
            throw new ArgumentException("A TypeDef import token must resolve to itself in the same module without AssemblyRef.");
        }

        return new StaticFieldReferenceResolutionFact(
            normalizedContextFact,
            sourceModule,
            sourceModuleContent,
            assemblyReferenceToken,
            sourceTypeToken,
            typeReferenceResolution: null,
            targetModule,
            targetModuleContent,
            targetTypeDefinitionToken);
    }

    /// <summary>Creates one type-bearing import fact from a complete closed TypeRef resolution relation.</summary>
    /// <param name="contextFactSha256">Digest of the exact consulted type-bearing import fact.</param>
    /// <param name="typeReferenceResolution">Complete Module-, ModuleRef-, or AssemblyRef-rooted TypeRef resolution.</param>
    /// <returns>A detached import narrowing fact retaining the full legal ResolutionScope union.</returns>
    public static StaticFieldReferenceResolutionFact ForTypeReference(
        string contextFactSha256,
        StaticFieldTypeReferenceResolutionIdentity typeReferenceResolution)
    {
        var normalizedContextFact = CanonicalReplayEncoding.NormalizeSha256(
            contextFactSha256,
            nameof(contextFactSha256));
        ArgumentNullException.ThrowIfNull(typeReferenceResolution);
        var source = typeReferenceResolution.SourceModule;
        var target = typeReferenceResolution.ResolvedTargetType;
        if (source.Module.ApplicationDomainAddress != target.Module.ApplicationDomainAddress)
        {
            throw new ArgumentException(
                "An import TypeRef resolution must remain inside one exact application domain.",
                nameof(typeReferenceResolution));
        }
        return new StaticFieldReferenceResolutionFact(
            normalizedContextFact,
            source.Module,
            source.ModuleContent,
            typeReferenceResolution.AssemblyReferenceToken,
            typeReferenceResolution.ReferencedType.TypeReferenceToken,
            typeReferenceResolution,
            target.Module,
            target.ModuleContent,
            target.TypeDefinitionToken);
    }

    /// <summary>Determines whether another fact has the same source tokens and counted target declaration relation.</summary>
    /// <param name="other">The resolution fact to compare.</param>
    /// <returns><see langword="true"/> only when all canonical source/target facts match.</returns>
    public bool Equals(StaticFieldReferenceResolutionFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldReferenceResolutionFact);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one exact expanded namespace/type/field spelling and the fact that produced it.</summary>
public sealed class StaticFieldNameExpansion : IEquatable<StaticFieldNameExpansion>
{
    private const string CanonicalDomain = "static-field-name-expansion";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldNameExpansion(
        StaticFieldNameExpansionKind kind,
        string namespaceName,
        string typeName,
        string fieldName,
        string? alias,
        string? contextFactSha256,
        StaticFieldReferenceResolutionFact? referenceResolution,
        StaticFieldCandidateShape candidateShape)
    {
        Kind = kind;
        NamespaceName = namespaceName;
        TypeName = typeName;
        FieldName = fieldName;
        Alias = alias;
        ContextFactSha256 = contextFactSha256;
        ReferenceResolution = referenceResolution;
        CandidateShape = candidateShape;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteString(namespaceName);
        writer.WriteString(typeName);
        writer.WriteString(fieldName);
        StaticFieldContractEncoding.WriteOptionalString(writer, alias);
        StaticFieldContractEncoding.WriteOptionalString(writer, contextFactSha256);
        writer.WriteBoolean(referenceResolution is not null);
        if (referenceResolution is not null)
        {
            writer.WriteLengthPrefixedBytes(referenceResolution.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(candidateShape.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact version-one lookup rule that produced this expansion.</summary>
    public StaticFieldNameExpansionKind Kind { get; }

    /// <summary>Gets the decoded namespace, with an empty value representing the global namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the decoded top-level non-generic type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the decoded directly declared static field name.</summary>
    public string FieldName { get; }

    /// <summary>Gets the decoded alias only for type- or namespace-alias expansion.</summary>
    public string? Alias { get; }

    /// <summary>Gets the digest of the exact consulted context fact only for a context-derived expansion.</summary>
    public string? ContextFactSha256 { get; }

    /// <summary>
    /// Gets counted AssemblyRef/TypeRef narrowing for a token-bearing import, or null for context-independent,
    /// current-namespace, and unqualified namespace-import expansions.
    /// </summary>
    public StaticFieldReferenceResolutionFact? ReferenceResolution { get; }

    /// <summary>
    /// Gets the exact descriptor split searched by this expansion.
    /// </summary>
    public StaticFieldCandidateShape CandidateShape { get; }

    /// <summary>Gets whether this expansion depends on selected-frame/import context.</summary>
    public bool IsContextual => Kind is not (StaticFieldNameExpansionKind.GlobalQualified or StaticFieldNameExpansionKind.DotQualified);

    /// <summary>Gets a defensive copy of the versioned canonical expansion bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete, source-attributed name expansion.</summary>
    /// <param name="candidateShape">The exact syntax split searched by this expansion.</param>
    /// <param name="kind">The exact global, dot, namespace, import, or alias expansion rule.</param>
    /// <param name="namespaceName">The decoded namespace, or the empty string for the global namespace.</param>
    /// <param name="typeName">The decoded top-level type name.</param>
    /// <param name="fieldName">The decoded directly declared field name.</param>
    /// <param name="alias">The decoded alias for alias rules; null for all other rules.</param>
    /// <param name="contextFactSha256">The exact consulted context-fact digest for contextual rules; otherwise null.</param>
    /// <param name="referenceResolution">Counted token resolution for token-bearing import forms; otherwise null.</param>
    /// <returns>An immutable expansion origin suitable for search and declaration provenance.</returns>
    /// <exception cref="ArgumentException">A name, alias, digest, or kind-specific payload is inconsistent.</exception>
    public static StaticFieldNameExpansion Create(
        StaticFieldCandidateShape candidateShape,
        StaticFieldNameExpansionKind kind,
        string namespaceName,
        string typeName,
        string fieldName,
        string? alias = null,
        string? contextFactSha256 = null,
        StaticFieldReferenceResolutionFact? referenceResolution = null)
    {
        ArgumentNullException.ThrowIfNull(candidateShape);
        StaticFieldContractEncoding.RequireDefined(kind, nameof(kind));
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }

        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        StaticFieldContractEncoding.RequireIdentifierValue(fieldName, nameof(fieldName), allowDots: false);
        var contextual = kind is not (StaticFieldNameExpansionKind.GlobalQualified or StaticFieldNameExpansionKind.DotQualified);
        var aliasRequired = kind is StaticFieldNameExpansionKind.TypeAlias or StaticFieldNameExpansionKind.NamespaceAlias;
        if (aliasRequired)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(alias!, nameof(alias), allowDots: false);
        }
        else if (alias is not null)
        {
            throw new ArgumentException("Only an alias expansion may retain an alias name.", nameof(alias));
        }

        string? normalizedContextFact = null;
        if (contextual)
        {
            normalizedContextFact = CanonicalReplayEncoding.NormalizeSha256(contextFactSha256!, nameof(contextFactSha256));
        }
        else if (contextFactSha256 is not null)
        {
            throw new ArgumentException("A context-independent expansion cannot retain a context fact.", nameof(contextFactSha256));
        }

        if (referenceResolution is not null &&
            (!contextual ||
             kind == StaticFieldNameExpansionKind.CurrentNamespace ||
             !string.Equals(referenceResolution.ContextFactSha256, normalizedContextFact, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Reference-resolution evidence must identify this exact token-bearing import fact.",
                nameof(referenceResolution));
        }

        return new StaticFieldNameExpansion(
            kind,
            namespaceName,
            typeName,
            fieldName,
            alias,
            normalizedContextFact,
            referenceResolution,
            candidateShape);
    }

    /// <summary>Determines whether another expansion has identical decoded target and provenance.</summary>
    /// <param name="other">The expansion to compare.</param>
    /// <returns><see langword="true"/> only when every canonical expansion field matches.</returns>
    public bool Equals(StaticFieldNameExpansion? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldNameExpansion);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Records the complete counted result of searching one physical runtime module instance.</summary>
public sealed class StaticFieldModuleSearchFact : IEquatable<StaticFieldModuleSearchFact>
{
    private const string CanonicalDomain = "static-field-module-search-fact";
    private const int CanonicalSchemaVersion = 3;
    private readonly ImmutableArray<ModuleContentIdentity> moduleContents;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldModuleSearchFact(
        StaticFieldModuleInstanceIdentity module,
        StaticFieldModuleSearchStatus status,
        StaticFieldModuleSearchIssue issue,
        ImmutableArray<ModuleContentIdentity> moduleContents,
        int typeDefinitionsExamined,
        int fieldDefinitionsExamined,
        int? typeDefinitionRowCount,
        int? fieldDefinitionRowCount,
        int? typeReferenceRowCount,
        int? typeSpecificationRowCount,
        int? assemblyReferenceRowCount,
        int? methodDefinitionRowCount,
        int? parameterDefinitionRowCount,
        int? propertyDefinitionRowCount,
        int? eventDefinitionRowCount,
        int? moduleDefinitionRowCount,
        int? assemblyDefinitionRowCount,
        int? interfaceImplementationRowCount,
        int? memberReferenceRowCount,
        int? customAttributeRowCount,
        int? moduleReferenceRowCount,
        int? fileRowCount,
        int? exportedTypeRowCount,
        int? nestedClassRowCount,
        int? genericParameterRowCount,
        int? genericParameterConstraintRowCount,
        int? fieldPointerRowCount,
        int? methodPointerRowCount)
    {
        Module = module;
        Status = status;
        Issue = issue;
        this.moduleContents = moduleContents;
        TypeDefinitionsExamined = typeDefinitionsExamined;
        FieldDefinitionsExamined = fieldDefinitionsExamined;
        TypeDefinitionRowCount = typeDefinitionRowCount;
        FieldDefinitionRowCount = fieldDefinitionRowCount;
        TypeReferenceRowCount = typeReferenceRowCount;
        TypeSpecificationRowCount = typeSpecificationRowCount;
        AssemblyReferenceRowCount = assemblyReferenceRowCount;
        MethodDefinitionRowCount = methodDefinitionRowCount;
        ParameterDefinitionRowCount = parameterDefinitionRowCount;
        PropertyDefinitionRowCount = propertyDefinitionRowCount;
        EventDefinitionRowCount = eventDefinitionRowCount;
        ModuleDefinitionRowCount = moduleDefinitionRowCount;
        AssemblyDefinitionRowCount = assemblyDefinitionRowCount;
        InterfaceImplementationRowCount = interfaceImplementationRowCount;
        MemberReferenceRowCount = memberReferenceRowCount;
        CustomAttributeRowCount = customAttributeRowCount;
        ModuleReferenceRowCount = moduleReferenceRowCount;
        FileRowCount = fileRowCount;
        ExportedTypeRowCount = exportedTypeRowCount;
        NestedClassRowCount = nestedClassRowCount;
        GenericParameterRowCount = genericParameterRowCount;
        GenericParameterConstraintRowCount = genericParameterConstraintRowCount;
        FieldPointerRowCount = fieldPointerRowCount;
        MethodPointerRowCount = methodPointerRowCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(module.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteInt32(moduleContents.Length);
        foreach (var moduleContent in moduleContents)
        {
            StaticFieldSymbolContractEncoding.WriteModuleContent(writer, moduleContent);
        }

        writer.WriteInt32(typeDefinitionsExamined);
        writer.WriteInt32(fieldDefinitionsExamined);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, typeDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, fieldDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, typeReferenceRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, typeSpecificationRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, assemblyReferenceRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, methodDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, parameterDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, propertyDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, eventDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, moduleDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, assemblyDefinitionRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, interfaceImplementationRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, memberReferenceRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, customAttributeRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, moduleReferenceRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, fileRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, exportedTypeRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, nestedClassRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, genericParameterRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, genericParameterConstraintRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, fieldPointerRowCount);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, methodPointerRowCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact physical runtime module instance that was searched.</summary>
    public StaticFieldModuleInstanceIdentity Module { get; }

    /// <summary>Gets whether counted metadata search was exact, partial, unavailable, conflicting, or invalid.</summary>
    public StaticFieldModuleSearchStatus Status { get; }

    /// <summary>Gets the source-specific module-search issue, or <see cref="StaticFieldModuleSearchIssue.None"/>.</summary>
    public StaticFieldModuleSearchIssue Issue { get; }

    /// <summary>Gets complete counted metadata identity for exact search or row-level invalid evidence, otherwise null.</summary>
    public ModuleContentIdentity? ModuleContent =>
        moduleContents.Length == 1 ? moduleContents[0] : null;

    /// <summary>
    /// Gets a canonical-order defensive copy containing one content identity for exact search, at least two competing
    /// identities for conflict, one optional observed identity for invalid search, or none for partial/unavailable.
    /// </summary>
    public ImmutableArray<ModuleContentIdentity> ModuleContents => StaticFieldContractEncoding.Copy(moduleContents);

    /// <summary>Gets the exact number of TypeDef rows examined before this module disposition.</summary>
    public int TypeDefinitionsExamined { get; }

    /// <summary>Gets the exact number of directly declared FieldDef rows examined before disposition.</summary>
    public int FieldDefinitionsExamined { get; }

    /// <summary>Gets the complete TypeDef table row count only for exact metadata.</summary>
    public int? TypeDefinitionRowCount { get; }

    /// <summary>Gets the complete FieldDef table row count only for exact metadata.</summary>
    public int? FieldDefinitionRowCount { get; }

    /// <summary>Gets the complete FieldPtr table row count only for exact metadata.</summary>
    public int? FieldPointerRowCount { get; }

    /// <summary>Gets the complete TypeRef table row count only for exact metadata.</summary>
    public int? TypeReferenceRowCount { get; }

    /// <summary>Gets the complete TypeSpec table row count only for exact metadata.</summary>
    public int? TypeSpecificationRowCount { get; }

    /// <summary>Gets the complete AssemblyRef table row count only for exact metadata.</summary>
    public int? AssemblyReferenceRowCount { get; }

    /// <summary>Gets the complete MethodDef table row count only for exact metadata.</summary>
    public int? MethodDefinitionRowCount { get; }

    /// <summary>Gets the complete MethodPtr table row count only for exact metadata.</summary>
    public int? MethodPointerRowCount { get; }

    /// <summary>Gets the complete Param table row count only for exact metadata.</summary>
    public int? ParameterDefinitionRowCount { get; }

    /// <summary>Gets the complete Property table row count only for exact metadata.</summary>
    public int? PropertyDefinitionRowCount { get; }

    /// <summary>Gets the complete Event table row count only for exact metadata.</summary>
    public int? EventDefinitionRowCount { get; }

    /// <summary>Gets the complete Module table row count only for exact metadata; valid CLI metadata has exactly one.</summary>
    public int? ModuleDefinitionRowCount { get; }

    /// <summary>Gets the complete Assembly table row count only for exact metadata.</summary>
    public int? AssemblyDefinitionRowCount { get; }

    /// <summary>Gets the complete InterfaceImpl table row count only for exact metadata.</summary>
    public int? InterfaceImplementationRowCount { get; }

    /// <summary>Gets the complete MemberRef table row count only for exact metadata.</summary>
    public int? MemberReferenceRowCount { get; }

    /// <summary>Gets the complete CustomAttribute table row count only for exact metadata.</summary>
    public int? CustomAttributeRowCount { get; }

    /// <summary>Gets the complete ModuleRef table row count only for exact metadata.</summary>
    public int? ModuleReferenceRowCount { get; }

    /// <summary>Gets the complete File table row count only for exact metadata.</summary>
    public int? FileRowCount { get; }

    /// <summary>Gets the complete ExportedType table row count only for exact metadata.</summary>
    public int? ExportedTypeRowCount { get; }

    /// <summary>Gets the complete NestedClass table row count only for exact metadata.</summary>
    public int? NestedClassRowCount { get; }

    /// <summary>Gets the complete GenericParam table row count only for exact metadata.</summary>
    public int? GenericParameterRowCount { get; }

    /// <summary>Gets the complete GenericParamConstraint table row count only for exact metadata.</summary>
    public int? GenericParameterConstraintRowCount { get; }

    /// <summary>Gets whether this module's complete counted metadata search was exhaustive.</summary>
    public bool IsExhaustive => Status == StaticFieldModuleSearchStatus.Exact;

    /// <summary>Gets a defensive copy of the versioned canonical fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact exhaustive per-module search fact.</summary>
    /// <param name="module">The physical module instance searched.</param>
    /// <param name="moduleContent">The identity of the complete counted metadata image searched.</param>
    /// <param name="typeDefinitionsExamined">The exact TypeDef-row count examined.</param>
    /// <param name="fieldDefinitionsExamined">The exact FieldDef-row count examined.</param>
    /// <param name="typeDefinitionRowCount">The complete TypeDef table row count; defaults to the examined count.</param>
    /// <param name="fieldDefinitionRowCount">The complete FieldDef table row count; defaults to the examined count.</param>
    /// <param name="typeReferenceRowCount">The complete TypeRef table row count.</param>
    /// <param name="typeSpecificationRowCount">The complete TypeSpec table row count.</param>
    /// <param name="assemblyReferenceRowCount">The complete AssemblyRef table row count.</param>
    /// <param name="methodDefinitionRowCount">The complete MethodDef table row count.</param>
    /// <param name="parameterDefinitionRowCount">The complete Param table row count.</param>
    /// <param name="propertyDefinitionRowCount">The complete Property table row count.</param>
    /// <param name="eventDefinitionRowCount">The complete Event table row count.</param>
    /// <param name="moduleDefinitionRowCount">The complete Module table row count, required to be one.</param>
    /// <param name="assemblyDefinitionRowCount">The complete Assembly table row count.</param>
    /// <param name="interfaceImplementationRowCount">The complete InterfaceImpl table row count.</param>
    /// <param name="memberReferenceRowCount">The complete MemberRef table row count.</param>
    /// <param name="customAttributeRowCount">The complete CustomAttribute table row count.</param>
    /// <param name="moduleReferenceRowCount">The complete ModuleRef table row count.</param>
    /// <param name="fileRowCount">The complete File table row count.</param>
    /// <param name="exportedTypeRowCount">The complete ExportedType table row count.</param>
    /// <param name="nestedClassRowCount">The complete NestedClass table row count.</param>
    /// <param name="genericParameterRowCount">The complete GenericParam table row count.</param>
    /// <param name="genericParameterConstraintRowCount">The complete GenericParamConstraint table row count.</param>
    /// <param name="fieldPointerRowCount">The complete FieldPtr table row count.</param>
    /// <param name="methodPointerRowCount">The complete MethodPtr table row count.</param>
    /// <returns>An exact fact with no issue and complete content identity.</returns>
    public static StaticFieldModuleSearchFact Exact(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        int typeDefinitionsExamined,
        int fieldDefinitionsExamined,
        int? typeDefinitionRowCount = null,
        int? fieldDefinitionRowCount = null,
        int typeReferenceRowCount = 0,
        int typeSpecificationRowCount = 0,
        int assemblyReferenceRowCount = 0,
        int methodDefinitionRowCount = 0,
        int parameterDefinitionRowCount = 0,
        int propertyDefinitionRowCount = 0,
        int eventDefinitionRowCount = 0,
        int moduleDefinitionRowCount = 1,
        int assemblyDefinitionRowCount = 1,
        int interfaceImplementationRowCount = 0,
        int memberReferenceRowCount = 0,
        int customAttributeRowCount = 0,
        int moduleReferenceRowCount = 0,
        int fileRowCount = 0,
        int exportedTypeRowCount = 0,
        int nestedClassRowCount = 0,
        int genericParameterRowCount = 0,
        int genericParameterConstraintRowCount = 0,
        int fieldPointerRowCount = 0,
        int methodPointerRowCount = 0) =>
        Create(module, StaticFieldModuleSearchStatus.Exact, StaticFieldModuleSearchIssue.None,
            ImmutableArray.Create(moduleContent),
            typeDefinitionsExamined, fieldDefinitionsExamined,
            typeDefinitionRowCount ?? typeDefinitionsExamined,
            fieldDefinitionRowCount ?? fieldDefinitionsExamined,
            typeReferenceRowCount,
            typeSpecificationRowCount,
            assemblyReferenceRowCount,
            methodDefinitionRowCount,
            parameterDefinitionRowCount,
            propertyDefinitionRowCount,
            eventDefinitionRowCount,
            moduleDefinitionRowCount,
            assemblyDefinitionRowCount,
            interfaceImplementationRowCount,
            memberReferenceRowCount,
            customAttributeRowCount,
            moduleReferenceRowCount,
            fileRowCount,
            exportedTypeRowCount,
            nestedClassRowCount,
            genericParameterRowCount,
            genericParameterConstraintRowCount,
            fieldPointerRowCount,
            methodPointerRowCount);

    /// <summary>Creates a partial per-module search fact without a complete metadata-content payload.</summary>
    /// <param name="module">The physical module instance attempted.</param>
    /// <param name="issue">Either partial-read or bound-reached issue.</param>
    /// <param name="typeDefinitionsExamined">The exact TypeDef prefix examined.</param>
    /// <param name="fieldDefinitionsExamined">The exact FieldDef prefix examined.</param>
    /// <returns>A partial non-exhaustive fact.</returns>
    public static StaticFieldModuleSearchFact Partial(
        StaticFieldModuleInstanceIdentity module,
        StaticFieldModuleSearchIssue issue,
        int typeDefinitionsExamined,
        int fieldDefinitionsExamined) =>
        Create(module, StaticFieldModuleSearchStatus.Partial, issue, ImmutableArray<ModuleContentIdentity>.Empty,
            typeDefinitionsExamined, fieldDefinitionsExamined, null, null, null, null, null, null, null, null, null);

    /// <summary>Creates an unavailable per-module search fact with exact zero-or-greater attempted counts.</summary>
    /// <param name="module">The physical module instance attempted.</param>
    /// <param name="typeDefinitionsExamined">The exact TypeDef count reached before unavailability.</param>
    /// <param name="fieldDefinitionsExamined">The exact FieldDef count reached before unavailability.</param>
    /// <returns>An unavailable fact carrying only the module identity and counts.</returns>
    public static StaticFieldModuleSearchFact Unavailable(
        StaticFieldModuleInstanceIdentity module,
        int typeDefinitionsExamined = 0,
        int fieldDefinitionsExamined = 0) =>
        Create(module, StaticFieldModuleSearchStatus.Unavailable, StaticFieldModuleSearchIssue.MetadataUnavailable,
            ImmutableArray<ModuleContentIdentity>.Empty, typeDefinitionsExamined, fieldDefinitionsExamined,
            null, null, null, null, null, null, null, null, null);

    /// <summary>Creates a module/content identity-conflict fact without choosing either metadata image.</summary>
    /// <param name="module">The physical module instance whose metadata facts conflicted.</param>
    /// <param name="competingModuleContents">At least two distinct complete content identities that conflicted.</param>
    /// <param name="typeDefinitionsExamined">The exact TypeDef count reached before conflict.</param>
    /// <param name="fieldDefinitionsExamined">The exact FieldDef count reached before conflict.</param>
    /// <returns>A conflict fact retaining every distinct complete competing content identity without choosing one.</returns>
    public static StaticFieldModuleSearchFact Conflict(
        StaticFieldModuleInstanceIdentity module,
        ImmutableArray<ModuleContentIdentity> competingModuleContents,
        int typeDefinitionsExamined = 0,
        int fieldDefinitionsExamined = 0) =>
        Create(module, StaticFieldModuleSearchStatus.Conflict, StaticFieldModuleSearchIssue.MetadataIdentityConflict,
            competingModuleContents, typeDefinitionsExamined, fieldDefinitionsExamined,
            null, null, null, null, null, null, null, null, null);

    /// <summary>Creates a structurally invalid metadata-search fact without a declaration payload.</summary>
    /// <param name="module">The physical module instance whose metadata was malformed.</param>
    /// <param name="typeDefinitionsExamined">The exact TypeDef count reached before invalidity.</param>
    /// <param name="fieldDefinitionsExamined">The exact FieldDef count reached before invalidity.</param>
    /// <param name="observedModuleContent">
    /// Optional complete content identity when invalidity was located inside rows of an otherwise identified image.
    /// </param>
    /// <returns>An invalid fact with no complete metadata-content payload.</returns>
    public static StaticFieldModuleSearchFact Invalid(
        StaticFieldModuleInstanceIdentity module,
        int typeDefinitionsExamined = 0,
        int fieldDefinitionsExamined = 0,
        ModuleContentIdentity? observedModuleContent = null) =>
        Create(module, StaticFieldModuleSearchStatus.Invalid, StaticFieldModuleSearchIssue.MalformedMetadata,
            observedModuleContent is null
                ? ImmutableArray<ModuleContentIdentity>.Empty
                : ImmutableArray.Create(observedModuleContent),
            typeDefinitionsExamined, fieldDefinitionsExamined,
            null, null, null, null, null, null, null, null, null);

    /// <summary>Determines whether another module-search fact has identical versioned content.</summary>
    /// <param name="other">The fact to compare.</param>
    /// <returns><see langword="true"/> only when module, status, content identity, issue, and counts match.</returns>
    public bool Equals(StaticFieldModuleSearchFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldModuleSearchFact);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldModuleSearchFact Create(
        StaticFieldModuleInstanceIdentity module,
        StaticFieldModuleSearchStatus status,
        StaticFieldModuleSearchIssue issue,
        ImmutableArray<ModuleContentIdentity> moduleContents,
        int typeDefinitionsExamined,
        int fieldDefinitionsExamined,
        int? typeDefinitionRowCount,
        int? fieldDefinitionRowCount,
        int? typeReferenceRowCount,
        int? typeSpecificationRowCount,
        int? assemblyReferenceRowCount,
        int? methodDefinitionRowCount,
        int? parameterDefinitionRowCount,
        int? propertyDefinitionRowCount,
        int? eventDefinitionRowCount,
        int? moduleDefinitionRowCount = null,
        int? assemblyDefinitionRowCount = null,
        int? interfaceImplementationRowCount = null,
        int? memberReferenceRowCount = null,
        int? customAttributeRowCount = null,
        int? moduleReferenceRowCount = null,
        int? fileRowCount = null,
        int? exportedTypeRowCount = null,
        int? nestedClassRowCount = null,
        int? genericParameterRowCount = null,
        int? genericParameterConstraintRowCount = null,
        int? fieldPointerRowCount = null,
        int? methodPointerRowCount = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        ValidateRowCount(typeDefinitionsExamined, nameof(typeDefinitionsExamined));
        ValidateRowCount(fieldDefinitionsExamined, nameof(fieldDefinitionsExamined));
        ValidateOptionalCount(typeDefinitionRowCount, nameof(typeDefinitionRowCount));
        ValidateOptionalCount(fieldDefinitionRowCount, nameof(fieldDefinitionRowCount));
        ValidateOptionalCount(typeReferenceRowCount, nameof(typeReferenceRowCount));
        ValidateOptionalCount(typeSpecificationRowCount, nameof(typeSpecificationRowCount));
        ValidateOptionalCount(assemblyReferenceRowCount, nameof(assemblyReferenceRowCount));
        ValidateOptionalCount(methodDefinitionRowCount, nameof(methodDefinitionRowCount));
        ValidateOptionalCount(parameterDefinitionRowCount, nameof(parameterDefinitionRowCount));
        ValidateOptionalCount(propertyDefinitionRowCount, nameof(propertyDefinitionRowCount));
        ValidateOptionalCount(eventDefinitionRowCount, nameof(eventDefinitionRowCount));
        ValidateOptionalCount(moduleDefinitionRowCount, nameof(moduleDefinitionRowCount));
        ValidateOptionalCount(assemblyDefinitionRowCount, nameof(assemblyDefinitionRowCount));
        ValidateOptionalCount(interfaceImplementationRowCount, nameof(interfaceImplementationRowCount));
        ValidateOptionalCount(memberReferenceRowCount, nameof(memberReferenceRowCount));
        ValidateOptionalCount(customAttributeRowCount, nameof(customAttributeRowCount));
        ValidateOptionalCount(moduleReferenceRowCount, nameof(moduleReferenceRowCount));
        ValidateOptionalCount(fileRowCount, nameof(fileRowCount));
        ValidateOptionalCount(exportedTypeRowCount, nameof(exportedTypeRowCount));
        ValidateOptionalCount(nestedClassRowCount, nameof(nestedClassRowCount));
        ValidateOptionalCount(genericParameterRowCount, nameof(genericParameterRowCount));
        ValidateOptionalCount(genericParameterConstraintRowCount, nameof(genericParameterConstraintRowCount));
        ValidateOptionalCount(fieldPointerRowCount, nameof(fieldPointerRowCount));
        ValidateOptionalCount(methodPointerRowCount, nameof(methodPointerRowCount));
        var validIssue = status switch
        {
            StaticFieldModuleSearchStatus.Exact => issue == StaticFieldModuleSearchIssue.None,
            StaticFieldModuleSearchStatus.Partial => issue is StaticFieldModuleSearchIssue.MetadataPartial or StaticFieldModuleSearchIssue.SearchBoundReached,
            StaticFieldModuleSearchStatus.Unavailable => issue == StaticFieldModuleSearchIssue.MetadataUnavailable,
            StaticFieldModuleSearchStatus.Conflict => issue == StaticFieldModuleSearchIssue.MetadataIdentityConflict,
            StaticFieldModuleSearchStatus.Invalid => issue == StaticFieldModuleSearchIssue.MalformedMetadata,
            _ => false,
        };
        if (!validIssue)
        {
            throw new ArgumentException("The module-search issue does not belong to the requested status.", nameof(issue));
        }

        var normalizedContents = StaticFieldSymbolContractEncoding.NormalizeModuleContents(
            moduleContents,
            nameof(moduleContents));
        var validContentCount = status switch
        {
            StaticFieldModuleSearchStatus.Exact => normalizedContents.Length == 1,
            StaticFieldModuleSearchStatus.Conflict => normalizedContents.Length >= 2,
            StaticFieldModuleSearchStatus.Invalid => normalizedContents.Length <= 1,
            _ => normalizedContents.IsEmpty,
        };
        if (!validContentCount)
        {
            throw new ArgumentException(
                "Exact search requires one content identity, conflict at least two, invalid at most one, and other statuses none.",
                nameof(moduleContents));
        }

        var rowCountsAreValid = status == StaticFieldModuleSearchStatus.Exact
            ? typeDefinitionRowCount.HasValue &&
              fieldDefinitionRowCount.HasValue &&
              typeReferenceRowCount.HasValue &&
              typeSpecificationRowCount.HasValue &&
              assemblyReferenceRowCount.HasValue &&
              methodDefinitionRowCount.HasValue &&
              parameterDefinitionRowCount.HasValue &&
              propertyDefinitionRowCount.HasValue &&
              eventDefinitionRowCount.HasValue &&
              moduleDefinitionRowCount == 1 &&
              assemblyDefinitionRowCount.HasValue &&
              interfaceImplementationRowCount.HasValue &&
              memberReferenceRowCount.HasValue &&
              customAttributeRowCount.HasValue &&
              moduleReferenceRowCount.HasValue &&
              fileRowCount.HasValue &&
              exportedTypeRowCount.HasValue &&
              nestedClassRowCount.HasValue &&
              genericParameterRowCount.HasValue &&
              genericParameterConstraintRowCount.HasValue &&
              fieldPointerRowCount.HasValue &&
              methodPointerRowCount.HasValue &&
              typeDefinitionsExamined == typeDefinitionRowCount.Value &&
              fieldDefinitionsExamined == fieldDefinitionRowCount.Value
            : !typeDefinitionRowCount.HasValue &&
              !fieldDefinitionRowCount.HasValue &&
              !typeReferenceRowCount.HasValue &&
              !typeSpecificationRowCount.HasValue &&
              !assemblyReferenceRowCount.HasValue &&
              !methodDefinitionRowCount.HasValue &&
              !parameterDefinitionRowCount.HasValue &&
              !propertyDefinitionRowCount.HasValue &&
              !eventDefinitionRowCount.HasValue &&
              !moduleDefinitionRowCount.HasValue &&
              !assemblyDefinitionRowCount.HasValue &&
              !interfaceImplementationRowCount.HasValue &&
              !memberReferenceRowCount.HasValue &&
              !customAttributeRowCount.HasValue &&
              !moduleReferenceRowCount.HasValue &&
              !fileRowCount.HasValue &&
              !exportedTypeRowCount.HasValue &&
              !nestedClassRowCount.HasValue &&
              !genericParameterRowCount.HasValue &&
              !genericParameterConstraintRowCount.HasValue &&
              !fieldPointerRowCount.HasValue &&
              !methodPointerRowCount.HasValue;
        if (!rowCountsAreValid)
        {
            throw new ArgumentException(
                "Only exact metadata carries every consulted table row count, and an exact scan must examine every TypeDef and FieldDef row.");
        }

        return new StaticFieldModuleSearchFact(
            module,
            status,
            issue,
            normalizedContents,
            typeDefinitionsExamined,
            fieldDefinitionsExamined,
            typeDefinitionRowCount,
            fieldDefinitionRowCount,
            typeReferenceRowCount,
            typeSpecificationRowCount,
            assemblyReferenceRowCount,
            methodDefinitionRowCount,
            parameterDefinitionRowCount,
            propertyDefinitionRowCount,
            eventDefinitionRowCount,
            moduleDefinitionRowCount,
            assemblyDefinitionRowCount,
            interfaceImplementationRowCount,
            memberReferenceRowCount,
            customAttributeRowCount,
            moduleReferenceRowCount,
            fileRowCount,
            exportedTypeRowCount,
            nestedClassRowCount,
            genericParameterRowCount,
            genericParameterConstraintRowCount,
            fieldPointerRowCount,
            methodPointerRowCount);
    }

    private static void ValidateOptionalCount(int? count, string parameterName)
    {
        if (count.HasValue)
        {
            ValidateRowCount(count.Value, parameterName);
        }
    }

    private static void ValidateRowCount(int count, string parameterName)
    {
        if (count is < 0 or > 0x00FF_FFFF)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A metadata-table row count must fit the 24-bit metadata-token row-id domain.");
        }
    }
}

/// <summary>Identifies the exact metadata member table represented by rejected binding evidence.</summary>
public enum StaticFieldRejectedMemberKind
{
    /// <summary>The expression located a FieldDef that failed declaration admission.</summary>
    FieldDefinition = 1,

    /// <summary>The expression located a Property row; W7 never invokes its accessor.</summary>
    PropertyDefinition = 2,

    /// <summary>The expression located an Event row, which is outside static-field semantics.</summary>
    EventDefinition = 3,

    /// <summary>The expression located a MethodDef; W7 never converts static-member syntax into invocation.</summary>
    MethodDefinition = 4,
}

/// <summary>Identifies the independent source that supplied one declaration-conflict payload.</summary>
public enum StaticFieldDeclarationEvidenceSource
{
    /// <summary>The payload came from the complete counted metadata image.</summary>
    CountedMetadata = 1,

    /// <summary>The payload came from an independently projected runtime declaration.</summary>
    RuntimeProjection = 2,
}

/// <summary>Retains one exact attempted syntax expansion and located member that could not be selected.</summary>
/// <remarks>
/// The evidence is shape- and expansion-scoped, so a rejection for another spelling cannot enter an outcome.
/// FieldDef, Property, Event, and MethodDef rows remain distinct; a static property is therefore representable as an
/// exact unsupported member without fabricating a FieldDef. Declaration conflicts retain independent metadata and
/// runtime projections instead of asserting two contradictory rows in one content-addressed metadata image.
/// </remarks>
public sealed class StaticFieldRejectedDeclarationEvidence : IEquatable<StaticFieldRejectedDeclarationEvidence>
{
    private const string CanonicalDomain = "static-field-rejected-declaration-evidence";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> memberSignature;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldRejectedDeclarationEvidence(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion origin,
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        string namespaceName,
        string typeName,
        int typeDefinitionToken,
        int? typeAttributes,
        int? typeGenericParameterCount,
        StaticFieldDefinitionIdentity? fieldDefinition,
        StaticFieldRejectedMemberKind memberKind,
        string memberName,
        int memberDefinitionToken,
        int? memberAttributes,
        ImmutableArray<byte> memberSignature,
        bool? runtimeIsStatic,
        StaticFieldDeclaredValueKind? projectedValueKind,
        StaticFieldDeclarationEvidenceSource evidenceSource,
        StaticFieldBindingIssue rejectionIssue,
        string evidenceCode)
    {
        Shape = shape;
        Origin = origin;
        Module = module;
        ModuleContent = moduleContent;
        NamespaceName = namespaceName;
        TypeName = typeName;
        TypeDefinitionToken = typeDefinitionToken;
        TypeAttributes = typeAttributes;
        TypeGenericParameterCount = typeGenericParameterCount;
        FieldDefinition = fieldDefinition;
        MemberKind = memberKind;
        MemberName = memberName;
        MemberDefinitionToken = memberDefinitionToken;
        MemberAttributes = memberAttributes;
        this.memberSignature = memberSignature;
        RuntimeIsStatic = runtimeIsStatic;
        ProjectedValueKind = projectedValueKind;
        EvidenceSource = evidenceSource;
        RejectionIssue = rejectionIssue;
        EvidenceCode = evidenceCode;

        var payloadWriter = new CanonicalReplayEncoding.Writer("static-field-rejected-declaration-payload", 1);
        WritePayload(payloadWriter);
        DeclarationPayloadSha256 = CanonicalReplayEncoding.ComputeSha256(payloadWriter.ToImmutableArray().AsSpan());

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        WritePayload(writer);
        writer.WriteInt32((int)evidenceSource);
        writer.WriteInt32((int)rejectionIssue);
        writer.WriteString(evidenceCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact descriptor split attempted by this rejected lookup.</summary>
    public StaticFieldCandidateShape Shape { get; }

    /// <summary>Gets the exact source-attributed name expansion attempted by this rejected lookup.</summary>
    public StaticFieldNameExpansion Origin { get; }

    /// <summary>Gets the physical runtime module containing the rejected metadata row.</summary>
    public StaticFieldModuleInstanceIdentity Module { get; }

    /// <summary>Gets the complete counted metadata-content identity containing the rejected row.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the exact decoded declaring namespace, or empty string for the global namespace.</summary>
    public string NamespaceName { get; }

    /// <summary>Gets the exact decoded declaring type name without asserting admitted type shape.</summary>
    public string TypeName { get; }

    /// <summary>Gets the non-nil located TypeDef token.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the exact raw TypeAttributes bits that contributed to rejection.</summary>
    public int? TypeAttributes { get; }

    /// <summary>Gets the exact declaring-type generic-parameter count.</summary>
    public int? TypeGenericParameterCount { get; }

    /// <summary>Gets the complete exact FieldDef row and owner for counted-metadata field evidence.</summary>
    public StaticFieldDefinitionIdentity? FieldDefinition { get; }

    /// <summary>Gets the exact metadata table containing the located member.</summary>
    public StaticFieldRejectedMemberKind MemberKind { get; }

    /// <summary>Gets the exact decoded located member name.</summary>
    public string MemberName { get; }

    /// <summary>Gets the non-nil FieldDef, Property, Event, or MethodDef token selected by <see cref="MemberKind"/>.</summary>
    public int MemberDefinitionToken { get; }

    /// <summary>Gets the exact raw table-specific member attributes.</summary>
    public int? MemberAttributes { get; }

    /// <summary>Gets whether exact retained CustomAttribute rows classify a counted-metadata FieldDef as thread-static.</summary>
    public bool? IsThreadStatic => FieldDefinition?.IsThreadStatic;

    /// <summary>Gets whether exact retained CustomAttribute rows classify a counted-metadata FieldDef as context-static.</summary>
    public bool? IsContextStatic => FieldDefinition?.IsContextStatic;

    /// <summary>Gets a defensive copy of the exact member signature; Event evidence may carry an empty array.</summary>
    public ImmutableArray<byte> MemberSignature => CanonicalReplayEncoding.Copy(memberSignature);

    /// <summary>Gets the independently projected runtime static flag only for runtime conflict evidence.</summary>
    public bool? RuntimeIsStatic { get; }

    /// <summary>Gets the independently projected declared value shape when the source exposes it.</summary>
    public StaticFieldDeclaredValueKind? ProjectedValueKind { get; }

    /// <summary>Gets the independent counted-metadata or runtime-projection source.</summary>
    public StaticFieldDeclarationEvidenceSource EvidenceSource { get; }

    /// <summary>Gets the declaration-specific unsupported, conflict, or invalid issue.</summary>
    public StaticFieldBindingIssue RejectionIssue { get; }

    /// <summary>Gets a stable non-empty code naming the rejected shape or disagreement.</summary>
    public string EvidenceCode { get; }

    /// <summary>Gets a digest of declaration payload fields excluding source, disposition, and diagnostic code.</summary>
    public string DeclarationPayloadSha256 { get; }

    /// <summary>Gets a defensive copy of the versioned canonical rejection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates exact typed evidence for a located but unselected FieldDef.</summary>
    /// <param name="shape">The exact descriptor split attempted.</param>
    /// <param name="origin">The exact expansion that located the row.</param>
    /// <param name="fieldDefinition">The complete exact FieldDef, direct TypeDef owner, and all owned CustomAttribute rows.</param>
    /// <param name="projectedValueKind">Optional value shape independently decoded from the metadata signature.</param>
    /// <param name="rejectionIssue">Declaration-shape unsupported, declaration conflict, or metadata invalid.</param>
    /// <param name="evidenceCode">A stable non-empty rejection code.</param>
    /// <returns>Immutable typed field rejection evidence.</returns>
    public static StaticFieldRejectedDeclarationEvidence Field(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion origin,
        StaticFieldDefinitionIdentity fieldDefinition,
        StaticFieldDeclaredValueKind? projectedValueKind,
        StaticFieldBindingIssue rejectionIssue,
        string evidenceCode)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        var declaringType = fieldDefinition.DeclaringType;
        return Create(
            shape,
            origin,
            declaringType.Module,
            declaringType.ModuleContent,
            declaringType.NamespaceName,
            declaringType.TypeName,
            declaringType.TypeDefinitionToken,
            declaringType.TypeAttributes,
            declaringType.GenericParameterCount,
            fieldDefinition,
            StaticFieldRejectedMemberKind.FieldDefinition,
            fieldDefinition.Name,
            fieldDefinition.FieldDefinitionToken,
            fieldDefinition.Attributes,
            fieldDefinition.Signature,
            runtimeIsStatic: null,
            projectedValueKind,
            StaticFieldDeclarationEvidenceSource.CountedMetadata,
            rejectionIssue,
            evidenceCode);
    }

    /// <summary>Creates exact unsupported or conflicting evidence for a Property, Event, or MethodDef row.</summary>
    /// <param name="shape">The exact descriptor split attempted.</param>
    /// <param name="origin">The exact expansion that located the row.</param>
    /// <param name="module">The physical runtime module containing the rows.</param>
    /// <param name="moduleContent">The exact complete counted metadata identity.</param>
    /// <param name="namespaceName">The decoded namespace, or empty string for global namespace.</param>
    /// <param name="typeName">The decoded declaring type name.</param>
    /// <param name="typeDefinitionToken">The non-nil located TypeDef token.</param>
    /// <param name="typeAttributes">The exact raw TypeAttributes bits.</param>
    /// <param name="typeGenericParameterCount">The exact non-negative generic-parameter count.</param>
    /// <param name="memberKind">Property, Event, or MethodDef; FieldDef must use <see cref="Field"/>.</param>
    /// <param name="memberName">The exact decoded member name.</param>
    /// <param name="memberDefinitionToken">The non-nil token selected by <paramref name="memberKind"/>.</param>
    /// <param name="memberAttributes">The exact raw table-specific attributes.</param>
    /// <param name="memberSignature">The exact signature; an Event may use an initialized empty array.</param>
    /// <param name="rejectionIssue">Declaration-shape unsupported, declaration conflict, or metadata invalid.</param>
    /// <param name="evidenceCode">A stable non-empty rejection code.</param>
    /// <returns>Immutable typed non-field rejection evidence.</returns>
    public static StaticFieldRejectedDeclarationEvidence NonField(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion origin,
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        string namespaceName,
        string typeName,
        int typeDefinitionToken,
        int typeAttributes,
        int typeGenericParameterCount,
        StaticFieldRejectedMemberKind memberKind,
        string memberName,
        int memberDefinitionToken,
        int memberAttributes,
        ImmutableArray<byte> memberSignature,
        StaticFieldBindingIssue rejectionIssue,
        string evidenceCode)
    {
        if (memberKind == StaticFieldRejectedMemberKind.FieldDefinition)
        {
            throw new ArgumentException("FieldDef rejection must use the field-specific factory.", nameof(memberKind));
        }

        return Create(
            shape,
            origin,
            module,
            moduleContent,
            namespaceName,
            typeName,
            typeDefinitionToken,
            typeAttributes,
            typeGenericParameterCount,
            fieldDefinition: null,
            memberKind,
            memberName,
            memberDefinitionToken,
            memberAttributes,
            memberSignature,
            runtimeIsStatic: null,
            projectedValueKind: null,
            StaticFieldDeclarationEvidenceSource.CountedMetadata,
            rejectionIssue,
            evidenceCode);
    }

    /// <summary>Creates the independent runtime side of one FieldDef declaration conflict.</summary>
    /// <param name="shape">The exact descriptor split attempted.</param>
    /// <param name="origin">The exact expansion identifying the intended declaration subject.</param>
    /// <param name="module">The runtime module independently projected by the adapter.</param>
    /// <param name="moduleContent">The counted content identity used only to join the shared module subject.</param>
    /// <param name="namespaceName">The runtime-projected declaring namespace.</param>
    /// <param name="typeName">The runtime-projected declaring type name.</param>
    /// <param name="typeDefinitionToken">The runtime-projected TypeDef token.</param>
    /// <param name="fieldName">The runtime-projected field name.</param>
    /// <param name="fieldDefinitionToken">The runtime-projected FieldDef token.</param>
    /// <param name="isStatic">The independently projected runtime static flag.</param>
    /// <param name="projectedValueKind">Optional independently projected runtime value shape.</param>
    /// <param name="evidenceCode">A stable non-empty conflict evidence code.</param>
    /// <returns>Runtime-only conflict evidence without fabricated metadata attributes or signature bytes.</returns>
    public static StaticFieldRejectedDeclarationEvidence RuntimeFieldConflict(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion origin,
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        string namespaceName,
        string typeName,
        int typeDefinitionToken,
        string fieldName,
        int fieldDefinitionToken,
        bool isStatic,
        StaticFieldDeclaredValueKind projectedValueKind,
        string evidenceCode) =>
        Create(
            shape,
            origin,
            module,
            moduleContent,
            namespaceName,
            typeName,
            typeDefinitionToken,
            typeAttributes: null,
            typeGenericParameterCount: null,
            fieldDefinition: null,
            StaticFieldRejectedMemberKind.FieldDefinition,
            fieldName,
            fieldDefinitionToken,
            memberAttributes: null,
            ImmutableArray<byte>.Empty,
            runtimeIsStatic: isStatic,
            projectedValueKind,
            StaticFieldDeclarationEvidenceSource.RuntimeProjection,
            StaticFieldBindingIssue.DeclarationConflict,
            evidenceCode);

    internal bool HasExplicitOverlappingDisagreement(StaticFieldRejectedDeclarationEvidence other)
    {
        var metadata = EvidenceSource == StaticFieldDeclarationEvidenceSource.CountedMetadata ? this : other;
        var runtime = EvidenceSource == StaticFieldDeclarationEvidenceSource.RuntimeProjection ? this : other;
        if (metadata.EvidenceSource != StaticFieldDeclarationEvidenceSource.CountedMetadata ||
            runtime.EvidenceSource != StaticFieldDeclarationEvidenceSource.RuntimeProjection)
        {
            return false;
        }

        if (!string.Equals(metadata.NamespaceName, runtime.NamespaceName, StringComparison.Ordinal) ||
            !string.Equals(metadata.TypeName, runtime.TypeName, StringComparison.Ordinal) ||
            metadata.TypeDefinitionToken != runtime.TypeDefinitionToken ||
            metadata.MemberKind != runtime.MemberKind ||
            !string.Equals(metadata.MemberName, runtime.MemberName, StringComparison.Ordinal) ||
            metadata.MemberDefinitionToken != runtime.MemberDefinitionToken)
        {
            return true;
        }

        if (metadata.MemberKind == StaticFieldRejectedMemberKind.FieldDefinition &&
            metadata.MemberAttributes.HasValue &&
            runtime.RuntimeIsStatic.HasValue &&
            ((metadata.MemberAttributes.Value & (int)FieldAttributes.Static) != 0) != runtime.RuntimeIsStatic.Value)
        {
            return true;
        }

        return metadata.ProjectedValueKind.HasValue &&
               runtime.ProjectedValueKind.HasValue &&
               metadata.ProjectedValueKind.Value != runtime.ProjectedValueKind.Value;
    }

    /// <summary>Determines whether another rejection carries the same exact attempted syntax and source evidence.</summary>
    /// <param name="other">The rejection evidence to compare.</param>
    /// <returns><see langword="true"/> only when all canonical evidence fields match.</returns>
    public bool Equals(StaticFieldRejectedDeclarationEvidence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldRejectedDeclarationEvidence);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldRejectedDeclarationEvidence Create(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion origin,
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity moduleContent,
        string namespaceName,
        string typeName,
        int typeDefinitionToken,
        int? typeAttributes,
        int? typeGenericParameterCount,
        StaticFieldDefinitionIdentity? fieldDefinition,
        StaticFieldRejectedMemberKind memberKind,
        string memberName,
        int memberDefinitionToken,
        int? memberAttributes,
        ImmutableArray<byte> memberSignature,
        bool? runtimeIsStatic,
        StaticFieldDeclaredValueKind? projectedValueKind,
        StaticFieldDeclarationEvidenceSource evidenceSource,
        StaticFieldBindingIssue rejectionIssue,
        string evidenceCode)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moduleContent);
        StaticFieldMetadataTextLimits.Validate(namespaceName, nameof(namespaceName), allowEmpty: true);
        if (namespaceName.Length > 0)
        {
            StaticFieldContractEncoding.RequireIdentifierValue(namespaceName, nameof(namespaceName), allowDots: true);
        }

        StaticFieldMetadataTextLimits.Validate(typeName, nameof(typeName), allowEmpty: false);
        StaticFieldContractEncoding.RequireIdentifierValue(typeName, nameof(typeName), allowDots: false);
        StaticFieldContractEncoding.RequireIdentifierValue(memberName, nameof(memberName), allowDots: false);
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));
        StaticFieldContractEncoding.RequireDefined(memberKind, nameof(memberKind));
        CanonicalReplayEncoding.ValidateMetadataToken(
            memberDefinitionToken,
            memberKind switch
            {
                StaticFieldRejectedMemberKind.FieldDefinition => 0x04,
                StaticFieldRejectedMemberKind.PropertyDefinition => 0x17,
                StaticFieldRejectedMemberKind.EventDefinition => 0x14,
                StaticFieldRejectedMemberKind.MethodDefinition => 0x06,
                _ => throw new ArgumentOutOfRangeException(nameof(memberKind)),
            },
            nameof(memberDefinitionToken));
        if (typeGenericParameterCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(typeGenericParameterCount));
        }
        StaticFieldContractEncoding.RequireDefined(evidenceSource, nameof(evidenceSource));
        if (projectedValueKind.HasValue)
        {
            StaticFieldContractEncoding.RequireDefined(projectedValueKind.Value, nameof(projectedValueKind));
        }

        var sourcePayloadIsValid = evidenceSource switch
        {
            StaticFieldDeclarationEvidenceSource.CountedMetadata =>
                typeAttributes.HasValue &&
                typeGenericParameterCount.HasValue &&
                memberAttributes.HasValue &&
                runtimeIsStatic is null &&
                !memberSignature.IsDefault &&
                (memberKind == StaticFieldRejectedMemberKind.EventDefinition || !memberSignature.IsEmpty) &&
                (memberKind == StaticFieldRejectedMemberKind.FieldDefinition) == (fieldDefinition is not null),
            StaticFieldDeclarationEvidenceSource.RuntimeProjection =>
                rejectionIssue == StaticFieldBindingIssue.DeclarationConflict &&
                memberKind == StaticFieldRejectedMemberKind.FieldDefinition &&
                !typeAttributes.HasValue &&
                !typeGenericParameterCount.HasValue &&
                !memberAttributes.HasValue &&
                fieldDefinition is null &&
                runtimeIsStatic.HasValue &&
                projectedValueKind.HasValue &&
                !memberSignature.IsDefault &&
                memberSignature.IsEmpty,
            _ => false,
        };
        if (!sourcePayloadIsValid)
        {
            throw new ArgumentException(
                "Rejected declaration payload fields contradict their independent evidence source.",
                nameof(evidenceSource));
        }
        if (fieldDefinition is not null &&
            (!fieldDefinition.DeclaringType.Module.Equals(module) ||
             !fieldDefinition.DeclaringType.ModuleContent.Equals(moduleContent) ||
             !string.Equals(fieldDefinition.DeclaringType.NamespaceName, namespaceName, StringComparison.Ordinal) ||
             !string.Equals(fieldDefinition.DeclaringType.TypeName, typeName, StringComparison.Ordinal) ||
             fieldDefinition.DeclaringType.TypeDefinitionToken != typeDefinitionToken ||
             fieldDefinition.DeclaringType.TypeAttributes != typeAttributes ||
             fieldDefinition.DeclaringType.GenericParameterCount != typeGenericParameterCount ||
             !string.Equals(fieldDefinition.Name, memberName, StringComparison.Ordinal) ||
             fieldDefinition.FieldDefinitionToken != memberDefinitionToken ||
             fieldDefinition.Attributes != memberAttributes ||
             !fieldDefinition.Signature.AsSpan().SequenceEqual(memberSignature.AsSpan())))
        {
            throw new ArgumentException(
                "The counted-metadata field payload must be derived from one complete exact FieldDef identity.",
                nameof(fieldDefinition));
        }
        if (evidenceSource == StaticFieldDeclarationEvidenceSource.CountedMetadata &&
            projectedValueKind.HasValue &&
            (!BoundedEcmaFieldSignatureProjection.TryDecode(memberSignature.AsSpan(), out var projection) ||
             !ProjectedValueKindMatches(projection.Kind, projectedValueKind.Value)))
        {
            throw new ArgumentException(
                "The metadata-projected value kind disagrees with the shared bounded FieldSig projection.",
                nameof(projectedValueKind));
        }

        if (rejectionIssue is not (
                StaticFieldBindingIssue.DeclarationShapeUnsupported or
                StaticFieldBindingIssue.DeclarationConflict or
                StaticFieldBindingIssue.MetadataInvalid))
        {
            throw new ArgumentException("Rejected declaration evidence requires a declaration-specific issue.", nameof(rejectionIssue));
        }
        if (rejectionIssue != StaticFieldBindingIssue.DeclarationConflict &&
            evidenceSource != StaticFieldDeclarationEvidenceSource.CountedMetadata)
        {
            throw new ArgumentException(
                "Only declaration-conflict evidence can originate in an independent runtime projection.",
                nameof(evidenceSource));
        }
        if (!origin.CandidateShape.Equals(shape))
        {
            throw new ArgumentException("Rejected evidence shape disagrees with its expansion attempt.", nameof(shape));
        }

        StaticFieldContractEncoding.RequireNonEmptyText(evidenceCode, nameof(evidenceCode));
        return new StaticFieldRejectedDeclarationEvidence(
            shape,
            origin,
            module,
            moduleContent,
            namespaceName,
            typeName,
            typeDefinitionToken,
            typeAttributes,
            typeGenericParameterCount,
            fieldDefinition,
            memberKind,
            memberName,
            memberDefinitionToken,
            memberAttributes,
            CanonicalReplayEncoding.Copy(memberSignature),
            runtimeIsStatic,
            projectedValueKind,
            evidenceSource,
            rejectionIssue,
            evidenceCode);
    }

    private void WritePayload(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteLengthPrefixedBytes(Shape.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(Origin.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(Module.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteModuleContent(writer, ModuleContent);
        writer.WriteString(NamespaceName);
        writer.WriteString(TypeName);
        writer.WriteInt32(TypeDefinitionToken);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, TypeAttributes);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, TypeGenericParameterCount);
        writer.WriteBoolean(FieldDefinition is not null);
        if (FieldDefinition is not null)
        {
            writer.WriteLengthPrefixedBytes(FieldDefinition.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32((int)MemberKind);
        writer.WriteString(MemberName);
        writer.WriteInt32(MemberDefinitionToken);
        StaticFieldSymbolContractEncoding.WriteOptionalInt32(writer, MemberAttributes);
        writer.WriteLengthPrefixedBytes(memberSignature.AsSpan());
        writer.WriteBoolean(RuntimeIsStatic.HasValue);
        if (RuntimeIsStatic.HasValue)
        {
            writer.WriteBoolean(RuntimeIsStatic.Value);
        }
        writer.WriteBoolean(ProjectedValueKind.HasValue);
        if (ProjectedValueKind.HasValue)
        {
            writer.WriteInt32((int)ProjectedValueKind.Value);
        }
    }

    private static bool ProjectedValueKindMatches(
        BoundedEcmaFieldSignatureKind signatureKind,
        StaticFieldDeclaredValueKind valueKind) =>
        signatureKind switch
        {
            BoundedEcmaFieldSignatureKind.Int32 => valueKind == StaticFieldDeclaredValueKind.Int32,
            BoundedEcmaFieldSignatureKind.PrimitiveString => valueKind == StaticFieldDeclaredValueKind.String,
            BoundedEcmaFieldSignatureKind.PrimitiveObject => valueKind == StaticFieldDeclaredValueKind.Object,
            BoundedEcmaFieldSignatureKind.ClassType => valueKind == StaticFieldDeclaredValueKind.ManagedReference,
            BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32 =>
                valueKind == StaticFieldDeclaredValueKind.NullableInt32,
            _ => false,
        };
}

/// <summary>Freezes one complete product-owned static-field declaration identity from counted metadata.</summary>
/// <remarks>
/// This is deliberately distinct from any host storage declaration: it contains no live runtime field, reader,
/// addressable slot, or value. The owner may be top-level or nested but must be non-generic in total and proven to be
/// a reference class. Managed-reference fields retain exact source-token resolution to class or interface targets in
/// any module in the immutable snapshot; runtime value subtype handling belongs to later physical evaluation.
/// </remarks>
public sealed class StaticFieldSymbolDeclarationIdentity : IEquatable<StaticFieldSymbolDeclarationIdentity>
{
    private const string CanonicalDomain = "static-field-symbol-declaration-identity";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldSymbolDeclarationIdentity(
        StaticFieldTypeAncestryIdentity declaringTypeAncestry,
        StaticFieldDefinitionIdentity fieldDefinition,
        StaticFieldDeclaredValueKind declaredValueKind,
        StaticFieldDeclaredReferenceIdentity? referenceTarget,
        StaticFieldNullableTypeIdentity? nullableType,
        StaticFieldTypeAncestryIdentity? systemInt32TypeAncestry)
    {
        DeclaringTypeAncestry = declaringTypeAncestry;
        FieldDefinition = fieldDefinition;
        DeclaredValueKind = declaredValueKind;
        ReferenceTarget = referenceTarget;
        NullableType = nullableType;
        SystemInt32TypeAncestry = systemInt32TypeAncestry;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(declaringTypeAncestry.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(fieldDefinition.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)declaredValueKind);
        writer.WriteBoolean(referenceTarget is not null);
        if (referenceTarget is not null)
        {
            writer.WriteLengthPrefixedBytes(referenceTarget.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(nullableType is not null);
        if (nullableType is not null)
        {
            writer.WriteLengthPrefixedBytes(nullableType.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(systemInt32TypeAncestry is not null);
        if (systemInt32TypeAncestry is not null)
        {
            writer.WriteLengthPrefixedBytes(systemInt32TypeAncestry.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the physical runtime module instance containing the declaration.</summary>
    public StaticFieldModuleInstanceIdentity Module => FieldDefinition.DeclaringType.Module;

    /// <summary>Gets the complete counted metadata-content identity containing both declaration rows.</summary>
    public ModuleContentIdentity ModuleContent => FieldDefinition.DeclaringType.ModuleContent;

    /// <summary>Gets exact contiguous ancestry proving the declaring TypeDef is a reference class.</summary>
    public StaticFieldTypeAncestryIdentity DeclaringTypeAncestry { get; }

    /// <summary>Gets the complete exact directly owned FieldDef row and marker projection.</summary>
    public StaticFieldDefinitionIdentity FieldDefinition { get; }

    /// <summary>Gets the decoded declaring namespace, or the empty string for the global namespace.</summary>
    public string NamespaceName => DeclaringTypeAncestry.SubjectType.NamespaceName;

    /// <summary>Gets the decoded declaring class metadata name.</summary>
    public string TypeName => DeclaringTypeAncestry.SubjectType.TypeName;

    /// <summary>Gets the ordinal metadata full name of the declaring class.</summary>
    public string DeclaringTypeName => DeclaringTypeAncestry.SubjectType.FullName;

    /// <summary>Gets the non-nil declaring TypeDef token.</summary>
    public int TypeDefinitionToken => DeclaringTypeAncestry.SubjectType.TypeDefinitionToken;

    /// <summary>Gets the exact raw ECMA-335 TypeAttributes bits validated as a reference class.</summary>
    public int TypeAttributes => DeclaringTypeAncestry.SubjectType.TypeAttributes;

    /// <summary>Gets the exact generic-parameter count, necessarily zero for this version.</summary>
    public int TypeGenericParameterCount => DeclaringTypeAncestry.SubjectType.GenericArity;

    /// <summary>Gets the composition-ready proof that the declaring TypeDef is a class; always true after validation.</summary>
    public bool IsDeclaringTypeClass => DeclaringTypeAncestry.Classification == StaticFieldTypeClassification.ReferenceClass;

    /// <summary>Gets whether the exact declaring TypeDef is top-level rather than nested.</summary>
    public bool IsDeclaringTypeTopLevel => DeclaringTypeAncestry.SubjectType.IsTopLevel;

    /// <summary>Gets the stable semantic generic arity; version one requires and reports zero.</summary>
    public int GenericArity => TypeGenericParameterCount;

    /// <summary>Gets the decoded directly declared field name.</summary>
    public string FieldName => FieldDefinition.Name;

    /// <summary>Gets the non-nil directly declared FieldDef token.</summary>
    public int FieldDefinitionToken => FieldDefinition.FieldDefinitionToken;

    /// <summary>Gets the exact raw ECMA-335 FieldAttributes bits validated as ordinary static storage.</summary>
    public int FieldAttributes => FieldDefinition.Attributes;

    /// <summary>Gets whether ThreadStaticAttribute was present; accepted declarations always report false.</summary>
    public bool IsThreadStatic => FieldDefinition.IsThreadStatic;

    /// <summary>Gets whether exact marker evidence proves ContextStatic storage; accepted declarations report false.</summary>
    public bool IsContextStatic => FieldDefinition.IsContextStatic;

    /// <summary>Gets a defensive copy of the exact FieldDef signature blob.</summary>
    public ImmutableArray<byte> FieldSignature => FieldDefinition.Signature;

    /// <summary>Gets the admitted value/storage semantic shape decoded from the exact signature.</summary>
    public StaticFieldDeclaredValueKind DeclaredValueKind { get; }

    /// <summary>Gets exact declared target identity for string and managed-reference shapes; otherwise null.</summary>
    public StaticFieldDeclaredReferenceIdentity? ReferenceTarget { get; }

    /// <summary>Gets the exact named System.Nullable`1 identity only for Nullable&lt;Int32&gt; declarations.</summary>
    public StaticFieldNullableTypeIdentity? NullableType { get; }

    /// <summary>Gets exact runtime-core-library System.Int32 ancestry for Int32 and Nullable&lt;Int32&gt; shapes.</summary>
    public StaticFieldTypeAncestryIdentity? SystemInt32TypeAncestry { get; }

    /// <summary>Gets a defensive copy of the versioned canonical declaration bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete admitted product symbol identity from counted metadata facts.</summary>
    /// <param name="declaringTypeAncestry">Exact ancestry proving a top-level or nested non-generic reference-class owner.</param>
    /// <param name="fieldDefinition">The complete exact directly owned FieldDef row and exhaustive marker projection.</param>
    /// <param name="declaredValueKind">The supported semantic value shape decoded from that signature.</param>
    /// <param name="referenceTarget">Exact System.String or resolved managed-reference target identity.</param>
    /// <param name="nullableType">Exact named System.Nullable`1 identity for Nullable&lt;Int32&gt;; otherwise null.</param>
    /// <param name="systemInt32TypeAncestry">Exact runtime-selected System.Int32 ancestry for Int32-bearing shapes.</param>
    /// <returns>A complete immutable declaration identity; it contains no storage slot or value.</returns>
    /// <exception cref="ArgumentException">Names, attributes, signature, or value-shape payload are inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A metadata token has the wrong table or is nil.</exception>
    public static StaticFieldSymbolDeclarationIdentity Create(
        StaticFieldTypeAncestryIdentity declaringTypeAncestry,
        StaticFieldDefinitionIdentity fieldDefinition,
        StaticFieldDeclaredValueKind declaredValueKind,
        StaticFieldDeclaredReferenceIdentity? referenceTarget = null,
        StaticFieldNullableTypeIdentity? nullableType = null,
        StaticFieldTypeAncestryIdentity? systemInt32TypeAncestry = null)
    {
        ArgumentNullException.ThrowIfNull(declaringTypeAncestry);
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        var declaringType = declaringTypeAncestry.SubjectType;
        if (!fieldDefinition.DeclaringType.Equals(declaringType) ||
            declaringTypeAncestry.Classification != StaticFieldTypeClassification.ReferenceClass ||
            declaringType.GenericArity != 0 ||
            !declaringType.DirectlyDeclaresField(fieldDefinition.FieldDefinitionToken))
        {
            throw new ArgumentException(
                "The declaring owner must be an exact same-module non-generic reference class whose FieldList interval owns the FieldDef; abstract classes are admitted.",
                nameof(declaringTypeAncestry));
        }

        var fieldFlags = (FieldAttributes)fieldDefinition.Attributes;
        if ((fieldFlags & System.Reflection.FieldAttributes.FieldAccessMask) ==
                System.Reflection.FieldAttributes.FieldAccessMask ||
            (fieldFlags & System.Reflection.FieldAttributes.Static) == 0 ||
            (fieldFlags & System.Reflection.FieldAttributes.Literal) != 0 ||
            (fieldFlags & System.Reflection.FieldAttributes.HasFieldRVA) != 0 ||
            fieldDefinition.IsThreadStatic || fieldDefinition.IsContextStatic)
        {
            throw new ArgumentException(
                "The FieldDef must use a defined access mask and ordinary non-literal, non-RVA, non-thread-static, non-context-static storage.",
                nameof(fieldDefinition));
        }

        StaticFieldContractEncoding.RequireDefined(declaredValueKind, nameof(declaredValueKind));
        var referencePayloadValid = declaredValueKind switch
        {
            StaticFieldDeclaredValueKind.String =>
                referenceTarget?.Kind == StaticFieldDeclaredReferenceKind.SystemString && nullableType is null,
            StaticFieldDeclaredValueKind.ManagedReference =>
                referenceTarget?.Kind == StaticFieldDeclaredReferenceKind.ManagedReference && nullableType is null,
            StaticFieldDeclaredValueKind.Object =>
                referenceTarget?.Kind == StaticFieldDeclaredReferenceKind.SystemObject && nullableType is null,
            StaticFieldDeclaredValueKind.NullableInt32 => referenceTarget is null && nullableType is not null,
            StaticFieldDeclaredValueKind.Int32 => referenceTarget is null && nullableType is null,
            _ => false,
        };
        if (!referencePayloadValid)
        {
            throw new ArgumentException("The declared value kind and exact reference-target identity disagree.", nameof(referenceTarget));
        }
        ValidateSystemInt32Type(
            declaringType.MetadataModule,
            declaredValueKind,
            systemInt32TypeAncestry);
        if (nullableType is not null &&
            (systemInt32TypeAncestry?.CoreLibrary is null ||
             nullableType.TargetTypeAncestry.CoreLibrary is null ||
             !systemInt32TypeAncestry.CoreLibrary.Equals(nullableType.TargetTypeAncestry.CoreLibrary)))
        {
            throw new ArgumentException(
                "Nullable`1 and its Int32 argument must be anchored to the same runtime-selected core library.",
                nameof(systemInt32TypeAncestry));
        }
        if (referenceTarget is not null &&
            !referenceTarget.OwnerMetadataModule.Equals(declaringType.MetadataModule))
        {
            throw new ArgumentException(
                "A declared reference token must be scoped by the declaration's exact module and metadata content.",
                nameof(referenceTarget));
        }
        if (nullableType is not null &&
            !nullableType.OwnerMetadataModule.Equals(declaringType.MetadataModule))
        {
            throw new ArgumentException(
                "The Nullable definition token must be scoped by the declaration's exact module and metadata content.",
                nameof(nullableType));
        }

        ValidateFieldRuntimeTypeResolution(
            referenceTarget?.SourceTypeReferenceResolution ?? nullableType?.SourceTypeReferenceResolution,
            declaringType,
            fieldDefinition.FieldDefinitionToken);

        StaticFieldSymbolContractEncoding.ValidateDeclaredFieldSignature(
            fieldDefinition.Signature,
            declaredValueKind,
            referenceTarget,
            nullableType,
            nameof(fieldDefinition));

        return new StaticFieldSymbolDeclarationIdentity(
            declaringTypeAncestry,
            fieldDefinition,
            declaredValueKind,
            referenceTarget,
            nullableType,
            systemInt32TypeAncestry);
    }

    private static void ValidateSystemInt32Type(
        StaticFieldMetadataModuleIdentity ownerMetadataModule,
        StaticFieldDeclaredValueKind declaredValueKind,
        StaticFieldTypeAncestryIdentity? systemInt32TypeAncestry)
    {
        var required = declaredValueKind is
            StaticFieldDeclaredValueKind.Int32 or StaticFieldDeclaredValueKind.NullableInt32;
        if (required != (systemInt32TypeAncestry is not null))
        {
            throw new ArgumentException(
                "Exactly Int32-bearing declarations require an exact System.Int32 ancestry anchor.",
                nameof(systemInt32TypeAncestry));
        }
        if (systemInt32TypeAncestry is null)
        {
            return;
        }

        var target = systemInt32TypeAncestry.SubjectType;
        var coreLibrary = systemInt32TypeAncestry.CoreLibrary;
        var flags = (TypeAttributes)target.TypeAttributes;
        if (!string.Equals(
                ownerMetadataModule.Module.SnapshotSha256,
                target.Module.SnapshotSha256,
                StringComparison.Ordinal) ||
            systemInt32TypeAncestry.Classification != StaticFieldTypeClassification.ValueType ||
            coreLibrary is null || !target.MetadataModule.Equals(coreLibrary.MetadataModule) ||
            !target.IsTopLevel || target.GenericArity != 0 || target.IsAbstract ||
            !string.Equals(target.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(target.TypeName, "Int32", StringComparison.Ordinal) ||
            (flags & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public ||
            (flags & System.Reflection.TypeAttributes.Sealed) == 0)
        {
            throw new ArgumentException(
                "The Int32 anchor must be exact public sealed System.Int32 in the runtime-selected core library.",
                nameof(systemInt32TypeAncestry));
        }
    }

    private static void ValidateFieldRuntimeTypeResolution(
        StaticFieldTypeReferenceResolutionIdentity? sourceResolution,
        StaticFieldTypeDefinitionIdentity declaringType,
        int fieldDefinitionToken)
    {
        if (sourceResolution?.TargetResolutionKind != StaticFieldTypeReferenceTargetResolutionKind.RuntimeLoader)
        {
            return;
        }
        var runtime = sourceResolution.RuntimeResolution;
        if (runtime?.Operation != StaticFieldRuntimeTypeResolutionOperation.FieldDeclaredType ||
            runtime.SourceFieldDefinitionToken != fieldDefinitionToken ||
            !runtime.SourceRuntimeType.TypeDefinition.Equals(declaringType))
        {
            throw new ArgumentException(
                "Loader-backed declared-type resolution must be the exact declaring ClrType/FieldDef/ClrStaticField.Type operation.",
                nameof(sourceResolution));
        }
    }

    /// <summary>Determines whether another product symbol names the same complete declaration content.</summary>
    /// <param name="other">The declaration identity to compare.</param>
    /// <returns><see langword="true"/> only when physical module, counted metadata, rows, shape, and signature match.</returns>
    public bool Equals(StaticFieldSymbolDeclarationIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldSymbolDeclarationIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Associates one complete declaration with every distinct name-expansion origin that reached it.</summary>
public sealed class StaticFieldSymbolCandidate : IEquatable<StaticFieldSymbolCandidate>
{
    private const string CanonicalDomain = "static-field-symbol-candidate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldNameExpansion> origins;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldSymbolCandidate(
        StaticFieldSymbolDeclarationIdentity declaration,
        StaticFieldCandidateShape shape,
        ImmutableArray<StaticFieldNameExpansion> origins)
    {
        Declaration = declaration;
        Shape = shape;
        this.origins = origins;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(declaration.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(shape.CanonicalBytes.AsSpan());
        StaticFieldSymbolContractEncoding.WriteExpansions(writer, origins);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete product-owned declaration identity reached by all origins.</summary>
    public StaticFieldSymbolDeclarationIdentity Declaration { get; }

    /// <summary>Gets the exact syntax split, instance suffix, and fallback interpreted by this candidate.</summary>
    public StaticFieldCandidateShape Shape { get; }

    /// <summary>Gets a deterministic defensive copy of distinct global/dot/context expansion origins.</summary>
    public ImmutableArray<StaticFieldNameExpansion> Origins => StaticFieldContractEncoding.Copy(origins);

    /// <summary>Gets a defensive copy of the versioned canonical candidate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a complete declaration-and-syntax candidate while sorting distinct origins.</summary>
    /// <param name="declaration">The complete counted declaration identity.</param>
    /// <param name="shape">The exact static-field split and suffix/fallback interpretation.</param>
    /// <param name="origins">An initialized, non-empty set of expansions resolving to that declaration.</param>
    /// <returns>One candidate retaining the union of distinct canonical origins.</returns>
    /// <exception cref="ArgumentException">Origins are absent, duplicated, or do not match the declaration.</exception>
    public static StaticFieldSymbolCandidate Create(
        StaticFieldSymbolDeclarationIdentity declaration,
        StaticFieldCandidateShape shape,
        ImmutableArray<StaticFieldNameExpansion> origins)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(shape);
        var normalizedOrigins = StaticFieldSymbolContractEncoding.NormalizeExpansions(origins, nameof(origins), requireNonEmpty: true);
        if (normalizedOrigins.Any(origin =>
                !origin.CandidateShape.Equals(shape) ||
                !string.Equals(origin.NamespaceName, declaration.NamespaceName, StringComparison.Ordinal) ||
                !string.Equals(origin.TypeName, declaration.TypeName, StringComparison.Ordinal) ||
                !string.Equals(origin.FieldName, declaration.FieldName, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every expansion origin must name the candidate declaration exactly.", nameof(origins));
        }

        return new StaticFieldSymbolCandidate(declaration, shape, normalizedOrigins);
    }

    /// <summary>Determines whether another candidate has the same declaration and expansion-origin set.</summary>
    /// <param name="other">The candidate to compare.</param>
    /// <returns><see langword="true"/> only when canonical declaration and sorted provenance match.</returns>
    public bool Equals(StaticFieldSymbolCandidate? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldSymbolCandidate);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Carries one strict, counted, canonical static-field symbol-binding outcome.</summary>
/// <remarks>
/// A selected declaration exists only for <see cref="StaticFieldBindingStatus.Exact"/>. Candidate evidence remains
/// visible on non-exact outcomes, but is never promoted to a selected symbol. Candidate occurrences targeting the
/// same declaration and syntax shape are grouped and their origins unioned. Different syntax interpretations remain
/// distinct even when they reach one FieldDef; equal metadata loaded into different runtime module or application-
/// domain instances also remains physically distinct and therefore ambiguous.
/// </remarks>
public sealed class StaticFieldSymbolBindingOutcome : IEquatable<StaticFieldSymbolBindingOutcome>
{
    private const string CanonicalDomain = "static-field-symbol-binding-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldNameExpansion> expansions;
    private readonly ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts;
    private readonly ImmutableArray<StaticFieldSymbolCandidate> candidates;
    private readonly ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldSymbolBindingOutcome(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingStatus status,
        StaticFieldBindingIssue issue,
        string? diagnosticCode,
        string? diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidates,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        int candidateOriginCount,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        Descriptor = descriptor;
        SnapshotSha256 = snapshotSha256;
        ConsultedContext = consultedContext;
        Status = status;
        Issue = issue;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        this.expansions = expansions;
        this.moduleSearchFacts = moduleSearchFacts;
        this.candidates = candidates;
        this.rejectedDeclarations = rejectedDeclarations;
        ModuleCatalogExhaustive = moduleCatalogExhaustive;
        ExpansionSearchExhaustive = expansionSearchExhaustive;
        CandidateOriginCount = candidateOriginCount;
        this.reachedBounds = reachedBounds;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(descriptor.CanonicalBytes.AsSpan());
        writer.WriteSha256(snapshotSha256, nameof(snapshotSha256));
        writer.WriteLengthPrefixedBytes(consultedContext.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        StaticFieldContractEncoding.WriteOptionalString(writer, diagnosticCode);
        StaticFieldContractEncoding.WriteOptionalString(writer, diagnosticMessage);
        StaticFieldSymbolContractEncoding.WriteExpansions(writer, expansions);
        StaticFieldSymbolContractEncoding.WriteModuleFacts(writer, moduleSearchFacts);
        StaticFieldSymbolContractEncoding.WriteCandidates(writer, candidates);
        StaticFieldSymbolContractEncoding.WriteRejectedDeclarations(writer, rejectedDeclarations);
        writer.WriteBoolean(moduleCatalogExhaustive);
        writer.WriteBoolean(expansionSearchExhaustive);
        writer.WriteInt32(candidateOriginCount);
        StaticFieldContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact accepted syntax descriptor whose bounded shapes were searched.</summary>
    public StaticFieldExpressionDescriptor Descriptor { get; }

    /// <summary>Gets the lowercase complete dump-snapshot digest governing every module and declaration fact.</summary>
    public string SnapshotSha256 { get; }

    /// <summary>
    /// Gets the typed snapshot-scoped identity of exactly the frame/import facts consulted. Fully qualified lookup
    /// carries the explicit unconsulted identity returned by <see cref="DumpConsultedBindingContextIdentity.ForFullyQualified"/>.
    /// </summary>
    public DumpConsultedBindingContextIdentity ConsultedContext { get; }

    /// <summary>Gets the digest of the complete typed consulted-context identity.</summary>
    public string ConsultedContextSha256 => ConsultedContext.Sha256;

    /// <summary>Gets the exact terminal binding disposition.</summary>
    public StaticFieldBindingStatus Status { get; }

    /// <summary>Gets the source-specific issue explaining the disposition.</summary>
    public StaticFieldBindingIssue Issue { get; }

    /// <summary>Gets a stable diagnostic code only for a non-exact outcome.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a stable artifact-independent explanation only for a non-exact outcome.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets a canonical-order defensive copy of every distinct attempted name expansion.</summary>
    public ImmutableArray<StaticFieldNameExpansion> Expansions => StaticFieldContractEncoding.Copy(expansions);

    /// <summary>Gets a canonical-order defensive copy of every distinct physical module-search fact.</summary>
    public ImmutableArray<StaticFieldModuleSearchFact> ModuleSearchFacts => StaticFieldContractEncoding.Copy(moduleSearchFacts);

    /// <summary>Gets a canonical-order defensive copy of distinct declarations with unioned expansion provenance.</summary>
    public ImmutableArray<StaticFieldSymbolCandidate> Candidates => StaticFieldContractEncoding.Copy(candidates);

    /// <summary>
    /// Gets a canonical-order defensive copy of exact located declarations rejected as unsupported, conflicting, or
    /// invalid. These facts never become <see cref="SelectedDeclaration"/>.
    /// </summary>
    public ImmutableArray<StaticFieldRejectedDeclarationEvidence> RejectedDeclarations =>
        StaticFieldContractEncoding.Copy(rejectedDeclarations);

    /// <summary>Gets the selected declaration only for exact status; every non-exact outcome returns null.</summary>
    public StaticFieldSymbolDeclarationIdentity? SelectedDeclaration =>
        Status == StaticFieldBindingStatus.Exact ? candidates[0].Declaration : null;

    /// <summary>Gets the selected static-field/suffix shape only for exact status; otherwise gets null.</summary>
    public StaticFieldCandidateShape? SelectedShape =>
        Status == StaticFieldBindingStatus.Exact ? candidates[0].Shape : null;

    /// <summary>Gets whether enumeration of the relevant managed module catalog was exact and exhaustive.</summary>
    public bool ModuleCatalogExhaustive { get; }

    /// <summary>Gets whether every bounded static path and applicable contextual expansion was examined.</summary>
    public bool ExpansionSearchExhaustive { get; }

    /// <summary>Gets whether catalog, expansion, and every per-module metadata search were exact and exhaustive.</summary>
    public bool SearchExhaustive =>
        ModuleCatalogExhaustive && ExpansionSearchExhaustive &&
        moduleSearchFacts.All(static fact => fact.IsExhaustive) &&
        (!ConsultedContext.CurrentNamespaceConsulted ||
         ConsultedContext.ConsultedFrameEvidence?.Status == DumpContextEvidenceStatus.Exact) &&
        (!ConsultedContext.ImportsConsulted ||
         ConsultedContext.ImportEvidenceStatus == DumpContextEvidenceStatus.Exact);

    /// <summary>Gets the exact number of distinct physical module instances considered.</summary>
    public int ModulesConsidered => moduleSearchFacts.Length;

    /// <summary>Gets the exact number of modules whose complete metadata was searched exhaustively.</summary>
    public int ExactModulesSearched => moduleSearchFacts.Count(static fact => fact.Status == StaticFieldModuleSearchStatus.Exact);

    /// <summary>Gets the exact total TypeDef-row count examined across all normalized module facts.</summary>
    public long TypeDefinitionsExamined => moduleSearchFacts.Sum(static fact => (long)fact.TypeDefinitionsExamined);

    /// <summary>Gets the exact total FieldDef-row count examined across all normalized module facts.</summary>
    public long FieldDefinitionsExamined => moduleSearchFacts.Sum(static fact => (long)fact.FieldDefinitionsExamined);

    /// <summary>Gets the number of distinct expanded names searched.</summary>
    public int ExpansionCount => expansions.Length;

    /// <summary>Gets the number of distinct expansion-to-declaration matches before declarations were grouped.</summary>
    public int CandidateOriginCount { get; }

    /// <summary>Gets the number of distinct complete declaration-and-syntax interpretations after grouping lookup paths.</summary>
    public int DistinctCandidateCount => candidates.Length;

    /// <summary>Gets an ordinal-name-ordered defensive copy of all binding bounds actually reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => StaticFieldContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the complete versioned canonical binding bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates exact binding, requiring one distinct declaration and exact exhaustive search.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="expansions">The initialized, non-empty expanded-name set.</param>
    /// <param name="moduleSearchFacts">The initialized exact per-module search facts.</param>
    /// <param name="candidateOccurrences">One or more complete candidates; equal declarations are grouped.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An exact outcome selecting the sole distinct declaration.</returns>
    public static StaticFieldSymbolBindingOutcome Exact(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Exact, StaticFieldBindingIssue.None,
            null, null, expansions, moduleSearchFacts, candidateOccurrences,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive: true, expansionSearchExhaustive: true, reachedBounds);

    /// <summary>Creates exact exhaustive symbol absence with zero candidate declarations.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="expansions">The initialized, non-empty expanded-name set searched.</param>
    /// <param name="moduleSearchFacts">The initialized exact exhaustive per-module search facts.</param>
    /// <param name="diagnosticCode">A stable absence diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent absence explanation.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An absent outcome with no selected declaration or candidates.</returns>
    public static StaticFieldSymbolBindingOutcome Absent(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Absent,
            StaticFieldBindingIssue.DeclarationAbsent, diagnosticCode, diagnosticMessage,
            expansions, moduleSearchFacts, ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive: true, expansionSearchExhaustive: true, reachedBounds);

    /// <summary>Creates a partial binding outcome without selecting any complete candidate.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="issue">A context-, module-, or bound-specific partial issue.</param>
    /// <param name="diagnosticCode">A stable partial-evidence diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized expanded-name set reached.</param>
    /// <param name="moduleSearchFacts">The initialized per-module fact set reached.</param>
    /// <param name="candidateOccurrences">Initialized complete candidates observed before the partial boundary.</param>
    /// <param name="moduleCatalogExhaustive">Whether the module catalog itself was exhausted.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration itself was exhausted.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>A partial outcome whose selected declaration is null.</returns>
    public static StaticFieldSymbolBindingOutcome Partial(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Partial, issue,
            diagnosticCode, diagnosticMessage, expansions, moduleSearchFacts, candidateOccurrences,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Creates an unavailable binding outcome without selecting any complete candidate.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="issue">A context- or module-specific unavailable issue.</param>
    /// <param name="diagnosticCode">A stable unavailability diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized expanded-name set reached.</param>
    /// <param name="moduleSearchFacts">The initialized per-module fact set reached.</param>
    /// <param name="candidateOccurrences">Initialized complete candidates observed before unavailability.</param>
    /// <param name="moduleCatalogExhaustive">Whether module enumeration completed.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration completed.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An unavailable outcome whose selected declaration is null.</returns>
    public static StaticFieldSymbolBindingOutcome Unavailable(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Unavailable, issue,
            diagnosticCode, diagnosticMessage, expansions, moduleSearchFacts, candidateOccurrences,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Creates ambiguity from at least two complete declaration-and-syntax interpretations.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="expansions">The initialized expanded-name set searched.</param>
    /// <param name="moduleSearchFacts">The initialized exact exhaustive module facts.</param>
    /// <param name="candidateOccurrences">Candidate occurrences grouping to at least two interpretations.</param>
    /// <param name="diagnosticCode">A stable ambiguity diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An ambiguous outcome with no selected declaration.</returns>
    public static StaticFieldSymbolBindingOutcome Ambiguous(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Ambiguous,
            StaticFieldBindingIssue.MultipleCandidates, diagnosticCode, diagnosticMessage,
            expansions, moduleSearchFacts, candidateOccurrences,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive: true, expansionSearchExhaustive: true, reachedBounds);

    /// <summary>Creates ambiguity established by the typed consulted context before one symbol could be selected.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were attempted.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">Typed context carrying an ambiguous selected-frame or import disposition.</param>
    /// <param name="diagnosticCode">A stable context-ambiguity diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized exact expansions completed before ambiguity, possibly empty.</param>
    /// <param name="moduleSearchFacts">The initialized module facts completed before ambiguity.</param>
    /// <param name="candidateOccurrences">Complete candidates retained as evidence only and never selected.</param>
    /// <param name="moduleCatalogExhaustive">Whether module enumeration completed before the context stop.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration completed; normally false.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An ambiguous outcome with issue <see cref="StaticFieldBindingIssue.ContextAmbiguous"/>.</returns>
    public static StaticFieldSymbolBindingOutcome ContextAmbiguous(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Ambiguous,
            StaticFieldBindingIssue.ContextAmbiguous, diagnosticCode, diagnosticMessage,
            expansions, moduleSearchFacts, candidateOccurrences,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Creates a conflicting binding outcome without selecting any candidate.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="issue">A context-, module-, or declaration-specific conflict issue.</param>
    /// <param name="diagnosticCode">A stable conflict diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized expansion set reached.</param>
    /// <param name="moduleSearchFacts">The initialized per-module facts reached.</param>
    /// <param name="candidateOccurrences">Initialized complete candidates retained as evidence only.</param>
    /// <param name="rejectedDeclarations">Exact located declaration facts rejected by the conflict, when applicable.</param>
    /// <param name="moduleCatalogExhaustive">Whether module enumeration completed.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration completed.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>A conflict outcome whose selected declaration is null.</returns>
    public static StaticFieldSymbolBindingOutcome Conflict(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Conflict, issue,
            diagnosticCode, diagnosticMessage, expansions, moduleSearchFacts, candidateOccurrences,
            rejectedDeclarations,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Creates a structurally invalid binding outcome without selecting any candidate.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="issue">A descriptor-, context-, or metadata-specific invalid issue.</param>
    /// <param name="diagnosticCode">A stable invalidity diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized expansion set reached.</param>
    /// <param name="moduleSearchFacts">The initialized per-module facts reached.</param>
    /// <param name="candidateOccurrences">Initialized complete candidates retained as evidence only.</param>
    /// <param name="rejectedDeclarations">Exact located declaration facts proving metadata invalidity, when applicable.</param>
    /// <param name="moduleCatalogExhaustive">Whether module enumeration completed.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration completed.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An invalid outcome whose selected declaration is null.</returns>
    public static StaticFieldSymbolBindingOutcome Invalid(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Invalid, issue,
            diagnosticCode, diagnosticMessage, expansions, moduleSearchFacts, candidateOccurrences,
            rejectedDeclarations,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Creates a valid-but-unadmitted binding outcome without a selected or complete candidate.</summary>
    /// <param name="descriptor">The exact accepted syntax descriptor whose shapes were searched.</param>
    /// <param name="snapshotSha256">The complete dump-snapshot digest.</param>
    /// <param name="consultedContext">The typed identity of exactly consulted context facts.</param>
    /// <param name="issue">An expansion- or declaration-shape unsupported issue.</param>
    /// <param name="diagnosticCode">A stable unsupported diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable artifact-independent explanation.</param>
    /// <param name="expansions">The initialized expansion set reached.</param>
    /// <param name="moduleSearchFacts">The initialized per-module facts reached.</param>
    /// <param name="rejectedDeclarations">Exact located declaration facts proving unsupported shape, when applicable.</param>
    /// <param name="moduleCatalogExhaustive">Whether module enumeration completed.</param>
    /// <param name="expansionSearchExhaustive">Whether expansion enumeration completed.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An unsupported outcome with no candidate or selected declaration.</returns>
    public static StaticFieldSymbolBindingOutcome Unsupported(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(
            descriptor, snapshotSha256, consultedContext, StaticFieldBindingStatus.Unsupported, issue,
            diagnosticCode, diagnosticMessage, expansions, moduleSearchFacts,
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            rejectedDeclarations,
            moduleCatalogExhaustive, expansionSearchExhaustive, reachedBounds);

    /// <summary>Determines whether another outcome has identical normalized, versioned binding content.</summary>
    /// <param name="other">The binding outcome to compare.</param>
    /// <returns><see langword="true"/> only when all identity, search, candidate, count, status, and bound facts match.</returns>
    public bool Equals(StaticFieldSymbolBindingOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldSymbolBindingOutcome);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldSymbolBindingOutcome Create(
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldBindingStatus status,
        StaticFieldBindingIssue issue,
        string? diagnosticCode,
        string? diagnosticMessage,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleSearchFacts,
        ImmutableArray<StaticFieldSymbolCandidate> candidateOccurrences,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(consultedContext);
        var normalizedSnapshot = CanonicalReplayEncoding.NormalizeSha256(snapshotSha256, nameof(snapshotSha256));
        if (!string.Equals(consultedContext.Snapshot.Sha256, normalizedSnapshot, StringComparison.Ordinal))
        {
            throw new ArgumentException("The consulted binding context belongs to another snapshot.", nameof(consultedContext));
        }
        var normalizedExpansions = StaticFieldSymbolContractEncoding.NormalizeExpansions(
            expansions,
            nameof(expansions),
            requireNonEmpty: false);
        if (normalizedExpansions.Any(static expansion => expansion.IsContextual) &&
            !consultedContext.CurrentNamespaceConsulted &&
            !consultedContext.ImportsConsulted)
        {
            throw new ArgumentException("Context-derived expansions require a context-consulting identity.", nameof(consultedContext));
        }

        StaticFieldSymbolContractEncoding.ValidateContextualExpansions(
            normalizedExpansions,
            consultedContext,
            nameof(expansions));
        StaticFieldSymbolContractEncoding.ValidateExpansionAttempts(
            normalizedExpansions,
            descriptor,
            consultedContext,
            nameof(expansions));

        var normalizedModules = StaticFieldSymbolContractEncoding.NormalizeModuleFacts(
            moduleSearchFacts,
            nameof(moduleSearchFacts));
        if (normalizedModules.Any(fact => !string.Equals(
                fact.Module.SnapshotSha256,
                normalizedSnapshot,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every module-search fact must belong to the binding snapshot.", nameof(moduleSearchFacts));
        }
        StaticFieldSymbolContractEncoding.ValidateReferenceResolutionModuleFacts(
            normalizedExpansions,
            normalizedModules,
            nameof(moduleSearchFacts));

        var rawCandidates = StaticFieldContractEncoding.RequireAndCopy(candidateOccurrences, nameof(candidateOccurrences));
        var candidateOriginCount = rawCandidates.Sum(static candidate => candidate.Origins.Length);
        var normalizedCandidates = StaticFieldSymbolContractEncoding.GroupCandidates(
            rawCandidates,
            descriptor,
            normalizedSnapshot,
            normalizedExpansions,
            normalizedModules,
            nameof(candidateOccurrences));
        var normalizedRejectedDeclarations = StaticFieldSymbolContractEncoding.NormalizeRejectedDeclarations(
            rejectedDeclarations,
            normalizedSnapshot,
            descriptor,
            normalizedExpansions,
            normalizedModules,
            nameof(rejectedDeclarations));
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        StaticFieldSymbolContractEncoding.ValidateOutcomeStatus(
            status,
            issue,
            consultedContext,
            descriptor,
            normalizedExpansions,
            normalizedModules,
            normalizedCandidates,
            normalizedRejectedDeclarations,
            moduleCatalogExhaustive,
            expansionSearchExhaustive,
            normalizedBounds);

        if (status == StaticFieldBindingStatus.Exact)
        {
            if (diagnosticCode is not null || diagnosticMessage is not null)
            {
                throw new ArgumentException("Exact binding cannot carry a failure diagnostic.");
            }
        }
        else
        {
            StaticFieldContractEncoding.RequireNonEmptyText(diagnosticCode!, nameof(diagnosticCode));
            StaticFieldContractEncoding.RequireNonEmptyText(diagnosticMessage!, nameof(diagnosticMessage));
        }

        return new StaticFieldSymbolBindingOutcome(
            descriptor,
            normalizedSnapshot,
            consultedContext,
            status,
            issue,
            diagnosticCode,
            diagnosticMessage,
            normalizedExpansions,
            normalizedModules,
            normalizedCandidates,
            normalizedRejectedDeclarations,
            moduleCatalogExhaustive,
            expansionSearchExhaustive,
            candidateOriginCount,
            normalizedBounds);
    }
}

internal static class StaticFieldSymbolContractEncoding
{
    internal static void WriteOptionalInt32(CanonicalReplayEncoding.Writer writer, int? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt32(value.Value);
        }
    }

    internal static void ValidateDeclaredFieldSignature(
        ImmutableArray<byte> signature,
        StaticFieldDeclaredValueKind declaredValueKind,
        StaticFieldDeclaredReferenceIdentity? referenceTarget,
        StaticFieldNullableTypeIdentity? nullableType,
        string parameterName)
    {
        if (signature.IsDefaultOrEmpty ||
            !BoundedEcmaFieldSignatureProjection.TryDecode(signature.AsSpan(), out var projection))
        {
            throw new ArgumentException(
                "The blob is not one exact, canonical, bounded FIELD signature admitted by W7.",
                parameterName);
        }

        var valid = declaredValueKind switch
        {
            StaticFieldDeclaredValueKind.Int32 =>
                projection.Kind == BoundedEcmaFieldSignatureKind.Int32 &&
                referenceTarget is null && nullableType is null,
            StaticFieldDeclaredValueKind.String =>
                referenceTarget is { Kind: StaticFieldDeclaredReferenceKind.SystemString } &&
                nullableType is null &&
                projection.Kind == BoundedEcmaFieldSignatureKind.PrimitiveString &&
                referenceTarget.StringSignatureEncoding == StaticFieldStringSignatureEncoding.PrimitiveString &&
                referenceTarget.TypeMetadataToken is null &&
                referenceTarget.SourceTypeReferenceResolution is null,
            StaticFieldDeclaredValueKind.Object =>
                projection.Kind == BoundedEcmaFieldSignatureKind.PrimitiveObject &&
                referenceTarget is
                {
                    Kind: StaticFieldDeclaredReferenceKind.SystemObject,
                    TypeMetadataToken: null,
                    SourceTypeReferenceResolution: null,
                } &&
                nullableType is null,
            StaticFieldDeclaredValueKind.ManagedReference =>
                projection.Kind == BoundedEcmaFieldSignatureKind.ClassType &&
                referenceTarget is { Kind: StaticFieldDeclaredReferenceKind.ManagedReference } &&
                projection.NamedTypeMetadataToken == referenceTarget.TypeMetadataToken &&
                ((CanonicalReplayEncoding.IsMetadataTokenForTable(referenceTarget.TypeMetadataToken!.Value, 0x02) &&
                  referenceTarget.SourceTypeReferenceResolution is null) ||
                 (CanonicalReplayEncoding.IsMetadataTokenForTable(referenceTarget.TypeMetadataToken!.Value, 0x01) &&
                  referenceTarget.SourceTypeReferenceResolution is not null)) &&
                nullableType is null,
            StaticFieldDeclaredValueKind.NullableInt32 =>
                projection.Kind == BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32 &&
                referenceTarget is null &&
                nullableType is not null &&
                projection.NamedTypeMetadataToken == nullableType.SignatureTypeMetadataToken,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "The exact FieldSig bytes do not independently encode the declared value kind and target.",
                parameterName);
        }
    }

    internal static void WriteModuleContent(CanonicalReplayEncoding.Writer writer, ModuleContentIdentity content)
    {
        writer.WriteLengthPrefixedBytes(content.Mvid.ToByteArray());
        writer.WriteInt32(content.MetadataLength);
        writer.WriteSha256(content.MetadataSha256, nameof(content.MetadataSha256));
    }

    internal static ImmutableArray<ModuleContentIdentity> NormalizeModuleContents(
        ImmutableArray<ModuleContentIdentity> contents,
        string parameterName)
    {
        var copied = StaticFieldContractEncoding.RequireAndCopy(contents, parameterName).ToArray();
        Array.Sort(copied, static (left, right) => StringComparer.Ordinal.Compare(ModuleContentKey(left), ModuleContentKey(right)));
        var unique = ImmutableArray.CreateBuilder<ModuleContentIdentity>(copied.Length);
        foreach (var content in copied)
        {
            if (unique.Count > 0 && unique[^1].Equals(content))
            {
                throw new ArgumentException("Competing module-content identities cannot contain duplicates.", parameterName);
            }

            unique.Add(content);
        }

        return unique.MoveToImmutable();
    }

    internal static ImmutableArray<StaticFieldNameExpansion> NormalizeExpansions(
        ImmutableArray<StaticFieldNameExpansion> expansions,
        string parameterName,
        bool requireNonEmpty)
    {
        if (requireNonEmpty && expansions.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An explicitly initialized, non-empty expansion array is required.", parameterName);
        }

        return StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            expansions,
            parameterName,
            static expansion => expansion.CanonicalBytes);
    }

    internal static ImmutableArray<StaticFieldModuleSearchFact> NormalizeModuleFacts(
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var normalized = StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            facts,
            parameterName,
            static fact => fact.CanonicalBytes);
        var byModule = new Dictionary<string, StaticFieldModuleSearchFact>(StringComparer.Ordinal);
        foreach (var fact in normalized)
        {
            if (byModule.TryGetValue(fact.Module.Sha256, out var prior) && !prior.Equals(fact))
            {
                throw new ArgumentException(
                    "One physical module instance cannot carry two different normalized search facts.",
                    parameterName);
            }

            byModule[fact.Module.Sha256] = fact;
        }

        return normalized;
    }

    internal static ImmutableArray<StaticFieldRejectedDeclarationEvidence> NormalizeRejectedDeclarations(
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> declarations,
        string snapshotSha256,
        StaticFieldExpressionDescriptor descriptor,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts,
        string parameterName)
    {
        var normalized = StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            declarations,
            parameterName,
            static declaration => declaration.CanonicalBytes);
        foreach (var declaration in normalized)
        {
            if (!string.Equals(declaration.Module.SnapshotSha256, snapshotSha256, StringComparison.Ordinal))
            {
                throw new ArgumentException("Rejected declaration evidence belongs to a foreign snapshot.", parameterName);
            }

            if (!descriptor.CandidateShapes.Any(shape => shape.Equals(declaration.Shape)) ||
                !expansions.Any(expansion => expansion.Equals(declaration.Origin)) ||
                !GetExpansionShape(declaration.Origin, descriptor, parameterName).Equals(declaration.Shape) ||
                declaration.RejectionIssue != StaticFieldBindingIssue.DeclarationConflict &&
                (!string.Equals(declaration.Origin.NamespaceName, declaration.NamespaceName, StringComparison.Ordinal) ||
                 !string.Equals(declaration.Origin.TypeName, declaration.TypeName, StringComparison.Ordinal) ||
                 !string.Equals(declaration.Origin.FieldName, declaration.MemberName, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Rejected declaration evidence must match one exact descriptor shape and searched expansion.",
                    parameterName);
            }

            var fact = moduleFacts.SingleOrDefault(candidate => candidate.Module.Equals(declaration.Module));
            var validFact = declaration.RejectionIssue switch
            {
                StaticFieldBindingIssue.MetadataInvalid =>
                    fact?.Status == StaticFieldModuleSearchStatus.Invalid &&
                    fact.ModuleContent is not null &&
                    fact.ModuleContent.Equals(declaration.ModuleContent),
                _ => fact?.Status == StaticFieldModuleSearchStatus.Exact &&
                     fact.ModuleContent is not null &&
                     fact.ModuleContent.Equals(declaration.ModuleContent),
            };
            if (!validFact)
            {
                throw new ArgumentException(
                    "Rejected declaration evidence lacks the matching module/content search fact.",
                    parameterName);
            }

            if (fact?.Status == StaticFieldModuleSearchStatus.Exact)
            {
                ValidateMetadataRowWithinFact(
                    declaration.TypeDefinitionToken,
                    declaration.MemberDefinitionToken,
                    declaration.MemberKind,
                    fact,
                    parameterName);
            }
        }

        return normalized;
    }

    internal static void ValidateContextualExpansions(
        ImmutableArray<StaticFieldNameExpansion> expansions,
        DumpConsultedBindingContextIdentity context,
        string parameterName)
    {
        foreach (var expansion in expansions.Where(static expansion => expansion.IsContextual))
        {
            if (expansion.Kind == StaticFieldNameExpansionKind.CurrentNamespace)
            {
                var frame = context.ConsultedFrameEvidence?.Frame;
                if (!context.CurrentNamespaceConsulted ||
                    frame is null ||
                    !string.Equals(expansion.ContextFactSha256, frame.Sha256, StringComparison.Ordinal) ||
                    !IsNamespaceOrEnclosing(frame.DeclaringNamespace, expansion.NamespaceName))
                {
                    throw new ArgumentException(
                        "A current-namespace expansion must match the exact consulted selected-frame fact.",
                        parameterName);
                }

                continue;
            }

            if (!context.ImportsConsulted)
            {
                throw new ArgumentException("An import expansion requires consulted import evidence.", parameterName);
            }

            var import = context.ConsultedImports.SingleOrDefault(fact =>
                string.Equals(fact.Sha256, expansion.ContextFactSha256, StringComparison.Ordinal));
            var matches = import is not null && expansion.Kind switch
            {
                StaticFieldNameExpansionKind.NamespaceImport =>
                    import.Kind == DumpPortablePdbImportKind.Namespace &&
                    string.Equals(import.Target, expansion.NamespaceName, StringComparison.Ordinal),
                StaticFieldNameExpansionKind.TypeAlias =>
                    import.Kind == DumpPortablePdbImportKind.TypeAlias &&
                    string.Equals(import.Alias, expansion.Alias, StringComparison.Ordinal) &&
                    MatchesQualifiedType(import.Target!, expansion.NamespaceName, expansion.TypeName),
                StaticFieldNameExpansionKind.NamespaceAlias =>
                    import.Kind == DumpPortablePdbImportKind.NamespaceAlias &&
                    string.Equals(import.Alias, expansion.Alias, StringComparison.Ordinal) &&
                    (string.Equals(import.Target, expansion.NamespaceName, StringComparison.Ordinal) ||
                     expansion.NamespaceName.StartsWith(import.Target + ".", StringComparison.Ordinal)),
                _ => false,
            };
            if (!matches)
            {
                throw new ArgumentException(
                    "A contextual expansion must match one exact consulted Portable-PDB import fact.",
                    parameterName);
            }

            var tokenBearing = import!.AssemblyReferenceToken.HasValue || import.TargetTypeToken.HasValue;
            if (tokenBearing != (expansion.ReferenceResolution is not null))
            {
                throw new ArgumentException(
                    "Every token-bearing import requires one counted reference resolution and unqualified imports require none.",
                    parameterName);
            }

            if (expansion.ReferenceResolution is { } resolution)
            {
                var frame = context.ConsultedFrameEvidence?.Frame;
                if (frame is null ||
                    !MatchesRuntimeModule(resolution.SourceModule, frame.RuntimeModule) ||
                    !resolution.SourceModuleContent.Equals(frame.ModuleContent) ||
                    import.AssemblyReferenceToken.HasValue &&
                        resolution.AssemblyReferenceToken != import.AssemblyReferenceToken ||
                    resolution.SourceTypeToken != import.TargetTypeToken)
                {
                    throw new ArgumentException(
                        "The import resolution must begin at the exact consulted selected-frame module and tokens.",
                        parameterName);
                }
            }
        }
    }

    internal static void ValidateExpansionAttempts(
        ImmutableArray<StaticFieldNameExpansion> expansions,
        StaticFieldExpressionDescriptor descriptor,
        DumpConsultedBindingContextIdentity context,
        string parameterName)
    {
        var segments = descriptor.Segments;
        foreach (var expansion in expansions)
        {
            var shape = GetExpansionShape(expansion, descriptor, parameterName);
            var prefix = segments
                .Take(shape.StaticFieldSegmentIndex)
                .Select(static segment => segment.DecodedIdentifier)
                .ToArray();
            if (!string.Equals(
                    segments[shape.StaticFieldSegmentIndex].DecodedIdentifier,
                    expansion.FieldName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The expansion field name does not reproduce its descriptor split.",
                    parameterName);
            }

            string[] expectedPrefix;
            switch (expansion.Kind)
            {
                case StaticFieldNameExpansionKind.GlobalQualified:
                    if (!descriptor.HasGlobalQualifier)
                    {
                        throw new ArgumentException("A global expansion requires literal global:: syntax.", parameterName);
                    }
                    expectedPrefix = QualifiedTypeSegments(expansion.NamespaceName, expansion.TypeName);
                    break;

                case StaticFieldNameExpansionKind.DotQualified:
                    if (descriptor.HasGlobalQualifier || expansion.NamespaceName.Length == 0)
                    {
                        throw new ArgumentException(
                            "A context-independent dot expansion requires a non-global spelling with a real namespace segment.",
                            parameterName);
                    }
                    expectedPrefix = QualifiedTypeSegments(expansion.NamespaceName, expansion.TypeName);
                    if (expectedPrefix.Length < 2)
                    {
                        throw new ArgumentException("A bare Type.Field cannot use context-independent dot binding.", parameterName);
                    }
                    break;

                case StaticFieldNameExpansionKind.CurrentNamespace:
                case StaticFieldNameExpansionKind.NamespaceImport:
                    if (descriptor.HasGlobalQualifier)
                    {
                        throw new ArgumentException("global:: suppresses contextual name expansion.", parameterName);
                    }
                    expectedPrefix = [expansion.TypeName];
                    break;

                case StaticFieldNameExpansionKind.TypeAlias:
                    if (descriptor.HasGlobalQualifier)
                    {
                        throw new ArgumentException("global:: suppresses type-alias expansion.", parameterName);
                    }
                    expectedPrefix = [expansion.Alias!];
                    break;

                case StaticFieldNameExpansionKind.NamespaceAlias:
                    if (descriptor.HasGlobalQualifier)
                    {
                        throw new ArgumentException("global:: suppresses namespace-alias expansion.", parameterName);
                    }
                    var import = context.ConsultedImports.Single(fact =>
                        string.Equals(fact.Sha256, expansion.ContextFactSha256, StringComparison.Ordinal));
                    var relativeNamespace = string.Equals(
                        import.Target,
                        expansion.NamespaceName,
                        StringComparison.Ordinal)
                        ? Array.Empty<string>()
                        : expansion.NamespaceName[(import.Target!.Length + 1)..].Split('.');
                    expectedPrefix = [expansion.Alias!, .. relativeNamespace, expansion.TypeName];
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(expansion.Kind));
            }

            if (!prefix.SequenceEqual(expectedPrefix, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "The expansion kind and decoded namespace/type do not reproduce the descriptor prefix.",
                    parameterName);
            }
        }
    }

    internal static void ValidateReferenceResolutionModuleFacts(
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts,
        string parameterName)
    {
        foreach (var resolution in expansions
                     .Where(static expansion => expansion.ReferenceResolution is not null)
                     .Select(static expansion => expansion.ReferenceResolution!))
        {
            if (resolution.TypeReferenceResolution is not null)
            {
                ValidateTypeReferenceResolutionWithinFacts(
                    resolution.TypeReferenceResolution,
                    moduleFacts,
                    parameterName,
                    "import TypeRef");
                continue;
            }
            var sourceFact = ValidateExactModuleContentFact(
                resolution.SourceModule,
                resolution.SourceModuleContent,
                moduleFacts,
                parameterName,
                "source");
            if (resolution.AssemblyReferenceToken.HasValue)
            {
                ValidateTokenWithinFact(
                    resolution.AssemblyReferenceToken.Value,
                    sourceFact,
                    parameterName,
                    requireExaminedPrefix: false);
            }
            if (resolution.SourceTypeToken.HasValue)
            {
                ValidateTokenWithinFact(
                    resolution.SourceTypeToken.Value,
                    sourceFact,
                    parameterName,
                    requireExaminedPrefix: false);
            }
            var targetFact = ValidateExactModuleContentFact(
                resolution.TargetModule,
                resolution.TargetModuleContent,
                moduleFacts,
                parameterName,
                "target");
            ValidateTypeDefinitionTokenWithinFact(
                resolution.TargetTypeDefinitionToken,
                targetFact,
                parameterName);
        }
    }

    internal static ImmutableArray<StaticFieldSymbolCandidate> GroupCandidates(
        ImmutableArray<StaticFieldSymbolCandidate> occurrences,
        StaticFieldExpressionDescriptor descriptor,
        string snapshotSha256,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts,
        string parameterName)
    {
        var groups = new Dictionary<string, (StaticFieldSymbolDeclarationIdentity Declaration, StaticFieldCandidateShape Shape, List<StaticFieldNameExpansion> Origins)>(
            StringComparer.Ordinal);
        foreach (var candidate in occurrences)
        {
            var declaration = candidate.Declaration;
            if (!descriptor.CandidateShapes.Any(shape => shape.Equals(candidate.Shape)))
            {
                throw new ArgumentException("A candidate syntax shape is absent from the bound descriptor.", parameterName);
            }

            if (!string.Equals(
                    descriptor.Segments[candidate.Shape.StaticFieldSegmentIndex].DecodedIdentifier,
                    declaration.FieldName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The candidate shape's decoded static-field segment disagrees with the declaration FieldDef name.",
                    parameterName);
            }
            if (candidate.Shape.SuffixShape != StaticFieldSuffixShape.None &&
                declaration.DeclaredValueKind != StaticFieldDeclaredValueKind.ManagedReference)
            {
                throw new ArgumentException(
                    "Only an exact managed-reference static declaration can become an instance-suffix receiver.",
                    parameterName);
            }
            if (!string.Equals(declaration.Module.SnapshotSha256, snapshotSha256, StringComparison.Ordinal))
            {
                throw new ArgumentException("A declaration candidate belongs to a foreign snapshot.", parameterName);
            }

            var matchingFact = moduleFacts.SingleOrDefault(fact => fact.Module.Equals(declaration.Module));
            if (matchingFact is null ||
                matchingFact.Status != StaticFieldModuleSearchStatus.Exact ||
                matchingFact.ModuleContent is null ||
                !matchingFact.ModuleContent.Equals(declaration.ModuleContent))
            {
                throw new ArgumentException(
                    "Every complete candidate requires one matching exact module/content search fact.",
                    parameterName);
            }
            ValidateMetadataRowWithinFact(
                declaration.TypeDefinitionToken,
                declaration.FieldDefinitionToken,
                StaticFieldRejectedMemberKind.FieldDefinition,
                matchingFact,
                parameterName);
            ValidateFieldDefinitionWithinFacts(declaration.FieldDefinition, moduleFacts, parameterName);
            ValidateTypeAncestryWithinFacts(
                declaration.DeclaringTypeAncestry,
                moduleFacts,
                parameterName,
                "declaring type");
            var target = declaration.ReferenceTarget;
            if (target is not null)
            {
                if (target.TypeMetadataToken.HasValue)
                {
                    ValidateTokenWithinFact(
                        target.TypeMetadataToken.Value,
                        matchingFact,
                        parameterName,
                        requireExaminedPrefix: false);
                }
                if (target.AssemblyReferenceToken.HasValue)
                {
                    ValidateTokenWithinFact(
                        target.AssemblyReferenceToken.Value,
                        matchingFact,
                        parameterName,
                        requireExaminedPrefix: false);
                }
                if (target.SourceTypeReferenceResolution is not null)
                {
                    ValidateTypeReferenceResolutionWithinFacts(
                        target.SourceTypeReferenceResolution,
                        moduleFacts,
                        parameterName,
                        "declared reference source");
                }
                ValidateTypeAncestryWithinFacts(
                    target.TargetTypeAncestry,
                    moduleFacts,
                    parameterName,
                    "declared reference target");
            }
            var nullableType = declaration.NullableType;
            if (nullableType is not null)
            {
                ValidateTokenWithinFact(
                    nullableType.SignatureTypeMetadataToken,
                    matchingFact,
                    parameterName,
                    requireExaminedPrefix: false);
                if (nullableType.AssemblyReferenceToken.HasValue)
                {
                    ValidateTokenWithinFact(
                        nullableType.AssemblyReferenceToken.Value,
                        matchingFact,
                        parameterName,
                        requireExaminedPrefix: false);
                }
                if (nullableType.SourceTypeReferenceResolution is not null)
                {
                    ValidateTypeReferenceResolutionWithinFacts(
                        nullableType.SourceTypeReferenceResolution,
                        moduleFacts,
                        parameterName,
                        "Nullable source");
                }
                ValidateTypeAncestryWithinFacts(
                    nullableType.TargetTypeAncestry,
                    moduleFacts,
                    parameterName,
                    "Nullable target");
            }
            if (declaration.SystemInt32TypeAncestry is not null)
            {
                ValidateTypeAncestryWithinFacts(
                    declaration.SystemInt32TypeAncestry,
                    moduleFacts,
                    parameterName,
                    "System.Int32 anchor");
            }

            foreach (var origin in candidate.Origins)
            {
                if (!expansions.Any(expansion => expansion.Equals(origin)))
                {
                    throw new ArgumentException("Candidate provenance must be present in the searched expansion set.", parameterName);
                }

                if (!GetExpansionShape(origin, descriptor, parameterName).Equals(candidate.Shape) ||
                    !string.Equals(origin.NamespaceName, declaration.NamespaceName, StringComparison.Ordinal) ||
                    !string.Equals(origin.TypeName, declaration.TypeName, StringComparison.Ordinal) ||
                    !string.Equals(origin.FieldName, declaration.FieldName, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Candidate provenance must reproduce the exact syntax split and declaration name.",
                        parameterName);
                }

                if (origin.ReferenceResolution is { } resolution &&
                    (!resolution.TargetModule.Equals(declaration.Module) ||
                     !resolution.TargetModuleContent.Equals(declaration.ModuleContent) ||
                     resolution.TargetTypeDefinitionToken != declaration.TypeDefinitionToken))
                {
                    throw new ArgumentException(
                        "A token-bearing import resolved to a different module or TypeDef than the candidate declaration.",
                        parameterName);
                }
            }

            var interpretationKey = $"{declaration.Sha256}:{candidate.Shape.Sha256}";
            if (!groups.TryGetValue(interpretationKey, out var group))
            {
                group = (declaration, candidate.Shape, []);
                groups.Add(interpretationKey, group);
            }

            group.Origins.AddRange(candidate.Origins);
        }

        var grouped = groups.Values
            .Select(group => StaticFieldSymbolCandidate.Create(
                group.Declaration,
                group.Shape,
                ImmutableArray.CreateRange(group.Origins)))
            .ToArray();
        Array.Sort(grouped, static (left, right) =>
            StaticFieldContractEncoding.CompareBytes(left.CanonicalBytes.AsSpan(), right.CanonicalBytes.AsSpan()));
        return ImmutableArray.CreateRange(grouped);
    }

    internal static void ValidateOutcomeStatus(
        StaticFieldBindingStatus status,
        StaticFieldBindingIssue issue,
        DumpConsultedBindingContextIdentity consultedContext,
        StaticFieldExpressionDescriptor descriptor,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> modules,
        ImmutableArray<StaticFieldSymbolCandidate> candidates,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> rejectedDeclarations,
        bool moduleCatalogExhaustive,
        bool expansionSearchExhaustive,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        var allModulesExact = modules.All(static fact => fact.Status == StaticFieldModuleSearchStatus.Exact);
        var contextExact =
            (!consultedContext.CurrentNamespaceConsulted ||
             consultedContext.ConsultedFrameEvidence?.Status == DumpContextEvidenceStatus.Exact) &&
            (!consultedContext.ImportsConsulted ||
             consultedContext.ImportEvidenceStatus == DumpContextEvidenceStatus.Exact);
        var exactExhaustiveSearch =
            moduleCatalogExhaustive && expansionSearchExhaustive && allModulesExact && contextExact;
        var expansionCount = expansions.Length;
        var ancestryBounds = reachedBounds
            .Where(static bound => string.Equals(
                bound.Name,
                StaticFieldTypeAncestryIdentity.MaximumAncestryEdgeCountBoundName,
                StringComparison.Ordinal))
            .ToArray();
        if (ancestryBounds.Any(static bound =>
                bound.Value != StaticFieldTypeAncestryIdentity.MaximumAncestryEdgeCount))
        {
            throw new ArgumentException(
                "The retained ancestry edge-count bound has a non-canonical value.",
                nameof(reachedBounds));
        }
        var hasAncestryBound = ancestryBounds.Length == 1;
        var retainedAncestryTraversal = candidates.Length > 0 || !rejectedDeclarations.IsEmpty;
        var stoppedAtAncestryBound = status == StaticFieldBindingStatus.Partial &&
                                     issue == StaticFieldBindingIssue.SearchBoundReached;
        if (retainedAncestryTraversal && !hasAncestryBound ||
            hasAncestryBound && !retainedAncestryTraversal && !stoppedAtAncestryBound)
        {
            throw new ArgumentException(
                "The ancestry edge-count bound must be retained exactly for candidate/rejection traversal or its bound stop, and nowhere else.",
                nameof(reachedBounds));
        }
        var validIssue = status switch
        {
            StaticFieldBindingStatus.Exact => issue == StaticFieldBindingIssue.None,
            StaticFieldBindingStatus.Absent => issue == StaticFieldBindingIssue.DeclarationAbsent,
            StaticFieldBindingStatus.Partial => issue is
                StaticFieldBindingIssue.ContextPartial or
                StaticFieldBindingIssue.ModuleSearchPartial or
                StaticFieldBindingIssue.SearchBoundReached,
            StaticFieldBindingStatus.Unavailable => issue is
                StaticFieldBindingIssue.ContextUnavailable or
                StaticFieldBindingIssue.ModuleUnavailable,
            StaticFieldBindingStatus.Ambiguous => issue is
                StaticFieldBindingIssue.MultipleCandidates or StaticFieldBindingIssue.ContextAmbiguous,
            StaticFieldBindingStatus.Conflict => issue is
                StaticFieldBindingIssue.ContextConflict or
                StaticFieldBindingIssue.ModuleConflict or
                StaticFieldBindingIssue.DeclarationConflict,
            StaticFieldBindingStatus.Invalid => issue is
                StaticFieldBindingIssue.DescriptorInvalid or
                StaticFieldBindingIssue.ContextInvalid or
                StaticFieldBindingIssue.MetadataInvalid,
            StaticFieldBindingStatus.Unsupported => issue is
                StaticFieldBindingIssue.ExpansionUnsupported or
                StaticFieldBindingIssue.DeclarationShapeUnsupported or
                StaticFieldBindingIssue.ContextUnsupported,
            _ => false,
        };
        if (!validIssue)
        {
            throw new ArgumentException("The binding issue is not valid for the requested status.", nameof(issue));
        }

        if (status == StaticFieldBindingStatus.Exact && (!exactExhaustiveSearch || candidates.Length != 1 || expansionCount == 0) ||
            status == StaticFieldBindingStatus.Absent && (!exactExhaustiveSearch || candidates.Length != 0 || expansionCount == 0) ||
            status == StaticFieldBindingStatus.Ambiguous && issue == StaticFieldBindingIssue.MultipleCandidates &&
                (!exactExhaustiveSearch || candidates.Length < 2 || expansionCount == 0) ||
            status == StaticFieldBindingStatus.Unsupported &&
                issue == StaticFieldBindingIssue.DeclarationShapeUnsupported &&
                (!exactExhaustiveSearch || candidates.Length != 0 || expansionCount == 0) ||
            status == StaticFieldBindingStatus.Ambiguous && issue == StaticFieldBindingIssue.ContextAmbiguous &&
                expansionSearchExhaustive ||
            status == StaticFieldBindingStatus.Unsupported && candidates.Length != 0)
        {
            throw new ArgumentException("The binding status contradicts candidate count or search exhaustiveness.");
        }

        var needsExhaustiveShapeCoverage =
            status is StaticFieldBindingStatus.Exact or StaticFieldBindingStatus.Absent ||
            status == StaticFieldBindingStatus.Ambiguous && issue == StaticFieldBindingIssue.MultipleCandidates ||
            status == StaticFieldBindingStatus.Unsupported &&
                issue == StaticFieldBindingIssue.DeclarationShapeUnsupported;
        if (needsExhaustiveShapeCoverage)
        {
            var attemptedShapes = expansions
                .Select(expansion => GetExpansionShape(expansion, descriptor, nameof(expansions)).Sha256)
                .ToHashSet(StringComparer.Ordinal);
            if (descriptor.CandidateShapes.Any(shape => !attemptedShapes.Contains(shape.Sha256)))
            {
                throw new ArgumentException(
                    "An exhaustive terminal binding must retain at least one attempted expansion for every descriptor shape.",
                    nameof(expansions));
            }
        }

        if (needsExhaustiveShapeCoverage &&
            expansions.All(static expansion => !expansion.IsContextual) &&
            (consultedContext.CurrentNamespaceConsulted || consultedContext.ImportsConsulted))
        {
            throw new ArgumentException(
                "A context-independent exhaustive binding cannot retain unconsumed frame or import evidence.",
                nameof(consultedContext));
        }

        if (status is StaticFieldBindingStatus.Partial or StaticFieldBindingStatus.Unavailable && exactExhaustiveSearch)
        {
            throw new ArgumentException("Partial or unavailable binding cannot claim exact exhaustive search.");
        }

        var validRejectedPayload = status switch
        {
            StaticFieldBindingStatus.Exact or
            StaticFieldBindingStatus.Absent or
            StaticFieldBindingStatus.Partial or
            StaticFieldBindingStatus.Unavailable or
            StaticFieldBindingStatus.Ambiguous => rejectedDeclarations.IsEmpty,
            StaticFieldBindingStatus.Conflict when issue == StaticFieldBindingIssue.DeclarationConflict =>
                rejectedDeclarations.Length == 2 && rejectedDeclarations.All(item => item.RejectionIssue == issue),
            StaticFieldBindingStatus.Conflict => rejectedDeclarations.IsEmpty,
            StaticFieldBindingStatus.Invalid when issue == StaticFieldBindingIssue.MetadataInvalid =>
                rejectedDeclarations.All(item => item.RejectionIssue == issue),
            StaticFieldBindingStatus.Invalid => rejectedDeclarations.IsEmpty,
            StaticFieldBindingStatus.Unsupported when issue == StaticFieldBindingIssue.DeclarationShapeUnsupported =>
                !rejectedDeclarations.IsEmpty && rejectedDeclarations.All(item => item.RejectionIssue == issue),
            StaticFieldBindingStatus.Unsupported => rejectedDeclarations.IsEmpty,
            _ => false,
        };
        if (!validRejectedPayload)
        {
            throw new ArgumentException(
                "Rejected declaration evidence contradicts the binding status or issue.",
                nameof(rejectedDeclarations));
        }
        if (status == StaticFieldBindingStatus.Conflict && issue == StaticFieldBindingIssue.DeclarationConflict)
        {
            var sources = rejectedDeclarations
                .Select(static item => item.EvidenceSource)
                .ToHashSet();
            var subjects = rejectedDeclarations
                .Select(DeclarationConflictSubjectKey)
                .ToHashSet(StringComparer.Ordinal);
            if (!sources.SetEquals(
                    [
                        StaticFieldDeclarationEvidenceSource.CountedMetadata,
                        StaticFieldDeclarationEvidenceSource.RuntimeProjection,
                    ]) ||
                subjects.Count != 1 ||
                !rejectedDeclarations[0].HasExplicitOverlappingDisagreement(rejectedDeclarations[1]))
            {
                throw new ArgumentException(
                    "Declaration conflict requires one shared subject and disagreeing metadata/runtime payloads.",
                    nameof(rejectedDeclarations));
            }
        }

        var contextSpecificIssue = issue is
            StaticFieldBindingIssue.ContextPartial or
            StaticFieldBindingIssue.ContextUnavailable or
            StaticFieldBindingIssue.ContextConflict or
            StaticFieldBindingIssue.ContextInvalid or
            StaticFieldBindingIssue.ContextAmbiguous or
            StaticFieldBindingIssue.ContextUnsupported;
        if (contextSpecificIssue &&
            !consultedContext.CurrentNamespaceConsulted &&
            !consultedContext.ImportsConsulted)
        {
            throw new ArgumentException("A context-specific issue requires consulted context evidence.", nameof(consultedContext));
        }

        if (contextSpecificIssue && !ContextStatusMatchesIssue(consultedContext, issue))
        {
            throw new ArgumentException(
                "The context-specific binding issue does not match the typed consulted source disposition.",
                nameof(issue));
        }

        if (issue == StaticFieldBindingIssue.ModuleSearchPartial &&
            !modules.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Partial) ||
            issue == StaticFieldBindingIssue.ModuleUnavailable &&
            !modules.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Unavailable) ||
            issue == StaticFieldBindingIssue.ModuleConflict &&
            !modules.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Conflict) ||
            issue == StaticFieldBindingIssue.MetadataInvalid &&
            !modules.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Invalid) ||
            issue == StaticFieldBindingIssue.SearchBoundReached && reachedBounds.IsEmpty)
        {
            throw new ArgumentException("The binding issue lacks matching source evidence.", nameof(issue));
        }
    }

    internal static void WriteExpansions(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<StaticFieldNameExpansion> expansions)
    {
        writer.WriteInt32(expansions.Length);
        foreach (var expansion in expansions)
        {
            writer.WriteLengthPrefixedBytes(expansion.CanonicalBytes.AsSpan());
        }
    }

    internal static void WriteModuleFacts(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<StaticFieldModuleSearchFact> facts)
    {
        writer.WriteInt32(facts.Length);
        foreach (var fact in facts)
        {
            writer.WriteLengthPrefixedBytes(fact.CanonicalBytes.AsSpan());
        }
    }

    internal static void WriteCandidates(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<StaticFieldSymbolCandidate> candidates)
    {
        writer.WriteInt32(candidates.Length);
        foreach (var candidate in candidates)
        {
            writer.WriteLengthPrefixedBytes(candidate.CanonicalBytes.AsSpan());
        }
    }

    internal static void WriteRejectedDeclarations(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<StaticFieldRejectedDeclarationEvidence> declarations)
    {
        writer.WriteInt32(declarations.Length);
        foreach (var declaration in declarations)
        {
            writer.WriteLengthPrefixedBytes(declaration.CanonicalBytes.AsSpan());
        }
    }

    private static StaticFieldCandidateShape GetExpansionShape(
        StaticFieldNameExpansion expansion,
        StaticFieldExpressionDescriptor descriptor,
        string parameterName)
    {
        if (!descriptor.CandidateShapes.Any(shape => shape.Equals(expansion.CandidateShape)))
        {
            throw new ArgumentException(
                "An expansion's mandatory candidate shape is absent from the descriptor.",
                parameterName);
        }

        return expansion.CandidateShape;
    }

    private static string[] QualifiedTypeSegments(string namespaceName, string typeName) =>
        namespaceName.Length == 0
            ? [typeName]
            : [.. namespaceName.Split('.'), typeName];

    private static StaticFieldModuleSearchFact ValidateExactModuleContentFact(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName,
        string role)
    {
        var fact = facts.SingleOrDefault(candidate => candidate.Module.Equals(module));
        if (fact?.Status != StaticFieldModuleSearchStatus.Exact ||
            fact.ModuleContent is null ||
            !fact.ModuleContent.Equals(content))
        {
            throw new ArgumentException(
                $"The {role} token relation lacks one exact owner-module/content search fact.",
                parameterName);
        }

        return fact;
    }

    private static void ValidateTypeAncestryWithinFacts(
        StaticFieldTypeAncestryIdentity ancestry,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName,
        string role)
    {
        ValidateTypeDefinitionWithinFacts(ancestry.SubjectType, facts, parameterName, role);

        foreach (var edge in ancestry.Edges)
        {
            var sourceFact = ValidateTypeDefinitionWithinFacts(
                edge.SourceType,
                facts,
                parameterName,
                $"{role} ancestry source");
            ValidateTokenWithinFact(
                edge.SourceType.ExtendsMetadataToken!.Value,
                sourceFact,
                parameterName,
                requireExaminedPrefix: false);
            if (edge.Kind == StaticFieldTypeBaseReferenceKind.TypeSpecification)
            {
                if (!BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                        edge.TypeSpecificationSignature.AsSpan(),
                        StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength,
                        StaticFieldTypeAncestryEdge.MaximumTypeSpecificationDepth,
                        StaticFieldTypeAncestryEdge.MaximumTypeSpecificationGenericArgumentCount,
                        out var typeSpecification))
                {
                    throw new ArgumentException("A retained TypeSpec ancestry signature is no longer canonical.", parameterName);
                }
                foreach (var token in typeSpecification.ReferencedTypeMetadataTokens)
                {
                    ValidateTokenWithinFact(token, sourceFact, parameterName, requireExaminedPrefix: false);
                }
            }
            if (edge.TypeReferenceResolution is not null)
            {
                ValidateTypeReferenceResolutionWithinFacts(
                    edge.TypeReferenceResolution,
                    facts,
                    parameterName,
                    $"{role} ancestry TypeRef");
            }

            ValidateTypeDefinitionWithinFacts(
                edge.ResolvedBaseType,
                facts,
                parameterName,
                $"{role} ancestry target");
        }
    }

    internal static void ValidateTypeAncestryWithinFactsForComposition(
        StaticFieldTypeAncestryIdentity ancestry,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        var normalizedFacts = NormalizeModuleFacts(facts, parameterName);
        ValidateTypeAncestryWithinFacts(
            ancestry,
            normalizedFacts,
            parameterName,
            "composed runtime type ancestry");
    }

    private static void ValidateFieldDefinitionWithinFacts(
        StaticFieldDefinitionIdentity field,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var ownerFact = ValidateTypeDefinitionWithinFacts(
            field.DeclaringType,
            facts,
            parameterName,
            "FieldDef owner");
        ValidateTokenWithinFact(
            field.FieldDefinitionToken,
            ownerFact,
            parameterName,
            requireExaminedPrefix: true);
        if (!BoundedEcmaFieldSignatureProjection.TryDecode(field.Signature.AsSpan(), out var fieldSignature))
        {
            throw new ArgumentException("A retained admitted FieldSig is no longer canonical.", parameterName);
        }
        foreach (var modifierToken in fieldSignature.CustomModifierTypeMetadataTokens)
        {
            ValidateTokenWithinFact(
                modifierToken,
                ownerFact,
                parameterName,
                requireExaminedPrefix: false);
        }

        foreach (var customAttribute in field.CustomAttributes.CustomAttributeRows)
        {
            ValidateCustomAttributeWithinFacts(customAttribute, facts, parameterName);
        }
    }

    internal static void ValidateInterfaceImplementationWithinFacts(
        StaticFieldInterfaceImplementationRowIdentity implementation,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        var sourceFact = ValidateMetadataModuleWithinFacts(
            implementation.SourceModule,
            facts,
            parameterName,
            "InterfaceImpl source");
        ValidateTokenWithinFact(
            implementation.InterfaceImplementationToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        ValidateTypeDefinitionWithinFacts(
            implementation.ImplementingType,
            facts,
            parameterName,
            "InterfaceImpl implementing type");
        ValidateTokenWithinFact(
            implementation.InterfaceTypeMetadataToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        if (!implementation.InterfaceTypeSpecificationSignature.IsEmpty)
        {
            if (!BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                    implementation.InterfaceTypeSpecificationSignature.AsSpan(),
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength,
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationDepth,
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationGenericArgumentCount,
                    out var typeSpecification))
            {
                throw new ArgumentException("A retained InterfaceImpl TypeSpec is no longer canonical.", parameterName);
            }
            foreach (var token in typeSpecification.ReferencedTypeMetadataTokens)
            {
                ValidateTokenWithinFact(token, sourceFact, parameterName, requireExaminedPrefix: false);
            }
        }
        if (implementation.InterfaceTypeReferenceResolution is not null)
        {
            ValidateTypeReferenceResolutionWithinFacts(
                implementation.InterfaceTypeReferenceResolution,
                facts,
                parameterName,
                "InterfaceImpl interface target");
        }
        ValidateTypeAncestryWithinFacts(
            implementation.ResolvedInterfaceTypeAncestry,
            facts,
            parameterName,
            "InterfaceImpl resolved interface");
    }

    private static void ValidateCustomAttributeWithinFacts(
        StaticFieldCustomAttributeRowIdentity customAttribute,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var ownerFact = ValidateMetadataModuleWithinFacts(
            customAttribute.OwnerModule,
            facts,
            parameterName,
            "CustomAttribute owner");
        ValidateTokenWithinFact(
            customAttribute.FieldDefinitionToken,
            ownerFact,
            parameterName,
            requireExaminedPrefix: false);
        ValidateTokenWithinFact(
            customAttribute.CustomAttributeToken,
            ownerFact,
            parameterName,
            requireExaminedPrefix: false);
        if (customAttribute.SourceMemberReference is { } sourceMemberReference)
        {
            ValidateMemberReferenceWithinFacts(sourceMemberReference, facts, parameterName);
        }
        else if (customAttribute.UnresolvedMemberReference is { } unresolvedMemberReference)
        {
            ValidateMemberReferenceRowWithinFacts(unresolvedMemberReference, facts, parameterName);
        }
        else if (customAttribute.ResolvedConstructor is { } directConstructor)
        {
            ValidateMethodDefinitionWithinFacts(directConstructor, facts, parameterName);
        }
        else
        {
            throw new ArgumentException("A CustomAttribute row lacks constructor evidence.", parameterName);
        }

        if (customAttribute.AttributeTypeAncestry is not null)
        {
            ValidateTypeAncestryWithinFacts(
                customAttribute.AttributeTypeAncestry,
                facts,
                parameterName,
                "CustomAttribute type");
        }
    }

    private static void ValidateMethodDefinitionWithinFacts(
        StaticFieldMethodDefinitionIdentity method,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var ownerFact = ValidateTypeDefinitionWithinFacts(
            method.DeclaringType,
            facts,
            parameterName,
            "MethodDef owner");
        ValidateTokenWithinFact(
            method.MethodDefinitionToken,
            ownerFact,
            parameterName,
            requireExaminedPrefix: false);
        if (method.ParameterDefinitionRowCount != ownerFact.ParameterDefinitionRowCount)
        {
            throw new ArgumentException(
                "A retained MethodDef ParamList authority disagrees with the exact owner Param table count.",
                parameterName);
        }
    }

    private static void ValidateMemberReferenceWithinFacts(
        StaticFieldMemberReferenceIdentity memberReference,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var sourceFact = ValidateMetadataModuleWithinFacts(
            memberReference.SourceModule,
            facts,
            parameterName,
            "MemberRef source");
        ValidateTokenWithinFact(
            memberReference.MemberReferenceToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        ValidateTokenWithinFact(
            memberReference.ParentTypeMetadataToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        if (memberReference.ParentTypeReferenceResolution is not null)
        {
            ValidateTypeReferenceResolutionWithinFacts(
                memberReference.ParentTypeReferenceResolution,
                facts,
                parameterName,
                "MemberRef parent");
        }
        ValidateMethodDefinitionWithinFacts(memberReference.ResolvedMethod, facts, parameterName);
    }

    private static void ValidateMemberReferenceRowWithinFacts(
        StaticFieldMemberReferenceRowIdentity memberReference,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName)
    {
        var sourceFact = ValidateMetadataModuleWithinFacts(
            memberReference.SourceModule,
            facts,
            parameterName,
            "unresolved MemberRef source");
        ValidateTokenWithinFact(
            memberReference.MemberReferenceToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        ValidateTokenWithinFact(
            memberReference.ParentMetadataToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        if (memberReference.ParentTypeDefinition is not null)
        {
            ValidateTypeDefinitionWithinFacts(
                memberReference.ParentTypeDefinition,
                facts,
                parameterName,
                "unresolved MemberRef TypeDef parent");
        }
        if (memberReference.ParentTypeReference is not null)
        {
            ValidateTokenWithinFact(
                memberReference.ParentTypeReference.ResolutionScopeMetadataToken,
                sourceFact,
                parameterName,
                requireExaminedPrefix: false);
        }
        if (!memberReference.ParentTypeSpecificationSignature.IsEmpty)
        {
            if (!BoundedEcmaTypeSpecificationProjection.TryDecodeGenericClass(
                    memberReference.ParentTypeSpecificationSignature.AsSpan(),
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationSignatureLength,
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationDepth,
                    StaticFieldTypeAncestryEdge.MaximumTypeSpecificationGenericArgumentCount,
                    out var typeSpecification))
            {
                throw new ArgumentException("A retained MemberRef-parent TypeSpec is no longer canonical.", parameterName);
            }
            foreach (var token in typeSpecification.ReferencedTypeMetadataTokens)
            {
                ValidateTokenWithinFact(token, sourceFact, parameterName, requireExaminedPrefix: false);
            }
        }
    }

    private static StaticFieldModuleSearchFact ValidateTypeDefinitionWithinFacts(
        StaticFieldTypeDefinitionIdentity type,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName,
        string role)
    {
        var fact = ValidateMetadataModuleWithinFacts(type.MetadataModule, facts, parameterName, role);
        ValidateTypeDefinitionTokenWithinFact(type.TypeDefinitionToken, fact, parameterName);
        if (type.FieldListEndExclusiveRowId > fact.FieldDefinitionRowCount!.Value + 1 ||
            type.MethodListEndExclusiveRowId > fact.MethodDefinitionRowCount!.Value + 1 ||
            type.GenericParameterCount > fact.GenericParameterRowCount!.Value)
        {
            throw new ArgumentException(
                "A retained TypeDef owner range or generic-parameter count exceeds its exact module table count.",
                parameterName);
        }

        var nestedDepth = 0;
        for (var enclosing = type.EnclosingType; enclosing is not null; enclosing = enclosing.EnclosingType)
        {
            nestedDepth++;
            if (!enclosing.MetadataModule.Equals(type.MetadataModule))
            {
                throw new ArgumentException("A NestedClass relation escaped its exact metadata module.", parameterName);
            }
            ValidateTypeDefinitionTokenWithinFact(enclosing.TypeDefinitionToken, fact, parameterName);
        }
        if (nestedDepth > fact.NestedClassRowCount!.Value)
        {
            throw new ArgumentException(
                "A retained NestedClass chain exceeds the exact NestedClass table row count.",
                parameterName);
        }
        return fact;
    }

    private static StaticFieldModuleSearchFact ValidateMetadataModuleWithinFacts(
        StaticFieldMetadataModuleIdentity metadataModule,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName,
        string role)
    {
        var fact = ValidateExactModuleContentFact(
            metadataModule.Module,
            metadataModule.ModuleContent,
            facts,
            parameterName,
            role);
        ValidateTokenWithinFact(0x00000001, fact, parameterName, requireExaminedPrefix: false);
        if (metadataModule.IsManifestModule)
        {
            ValidateTokenWithinFact(0x20000001, fact, parameterName, requireExaminedPrefix: false);
        }
        else
        {
            if (fact.AssemblyDefinitionRowCount != 0)
            {
                throw new ArgumentException("A netmodule metadata image cannot claim an AssemblyDef row.", parameterName);
            }
            var manifest = metadataModule.ContainingAssembly;
            var manifestFact = ValidateExactModuleContentFact(
                manifest.ManifestModule,
                manifest.ManifestModuleContent,
                facts,
                parameterName,
                $"{role} containing manifest");
            ValidateTokenWithinFact(0x00000001, manifestFact, parameterName, requireExaminedPrefix: false);
            ValidateTokenWithinFact(0x20000001, manifestFact, parameterName, requireExaminedPrefix: false);
            if (metadataModule.ManifestFile is not null)
            {
                ValidateTokenWithinFact(
                    metadataModule.ManifestFile.FileToken,
                    manifestFact,
                    parameterName,
                    requireExaminedPrefix: false);
            }
        }
        return fact;
    }

    private static void ValidateTypeReferenceResolutionWithinFacts(
        StaticFieldTypeReferenceResolutionIdentity resolution,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName,
        string role)
    {
        var sourceFact = ValidateMetadataModuleWithinFacts(
            resolution.SourceModule,
            facts,
            parameterName,
            $"{role} source");
        foreach (var typeReference in resolution.TypeReferences)
        {
            ValidateTokenWithinFact(
                typeReference.TypeReferenceToken,
                sourceFact,
                parameterName,
                requireExaminedPrefix: false);
            ValidateTokenWithinFact(
                typeReference.ResolutionScopeMetadataToken,
                sourceFact,
                parameterName,
                requireExaminedPrefix: false);
        }
        ValidateTokenWithinFact(
            resolution.RootMetadataToken,
            sourceFact,
            parameterName,
            requireExaminedPrefix: false);
        if (resolution.AssemblyReference is not null)
        {
            ValidateTokenWithinFact(
                resolution.AssemblyReference.AssemblyReferenceToken,
                sourceFact,
                parameterName,
                requireExaminedPrefix: false);
        }

        ValidateTypeDefinitionWithinFacts(
            resolution.ResolvedTargetType,
            facts,
            parameterName,
            $"{role} target");
        foreach (var forwarder in resolution.ExportedTypeForwarders)
        {
            var sourceAssemblyFact = ValidateExactModuleContentFact(
                forwarder.SourceAssembly.ManifestModule,
                forwarder.SourceAssembly.ManifestModuleContent,
                facts,
                parameterName,
                $"{role} forwarder source");
            ValidateTokenWithinFact(0x00000001, sourceAssemblyFact, parameterName, requireExaminedPrefix: false);
            ValidateTokenWithinFact(0x20000001, sourceAssemblyFact, parameterName, requireExaminedPrefix: false);
            ValidateTokenWithinFact(
                forwarder.ExportedTypeToken,
                sourceAssemblyFact,
                parameterName,
                requireExaminedPrefix: false);
            ValidateTokenWithinFact(
                forwarder.ImplementationAssemblyReference.AssemblyReferenceToken,
                sourceAssemblyFact,
                parameterName,
                requireExaminedPrefix: false);

            var targetAssemblyFact = ValidateExactModuleContentFact(
                forwarder.TargetAssembly.ManifestModule,
                forwarder.TargetAssembly.ManifestModuleContent,
                facts,
                parameterName,
                $"{role} forwarder target");
            ValidateTokenWithinFact(0x00000001, targetAssemblyFact, parameterName, requireExaminedPrefix: false);
            ValidateTokenWithinFact(0x20000001, targetAssemblyFact, parameterName, requireExaminedPrefix: false);
        }

        if (resolution.RuntimeResolution is { } runtime)
        {
            var runtimeSourceFact = ValidateMetadataModuleWithinFacts(
                runtime.SourceModule,
                facts,
                parameterName,
                $"{role} runtime source");
            ValidateTokenWithinFact(
                runtime.SourceTypeReferenceToken,
                runtimeSourceFact,
                parameterName,
                requireExaminedPrefix: false);
            if (runtime.SourceFieldDefinitionToken.HasValue)
            {
                ValidateTokenWithinFact(
                    runtime.SourceFieldDefinitionToken.Value,
                    runtimeSourceFact,
                    parameterName,
                    requireExaminedPrefix: false);
            }
            ValidateTypeDefinitionWithinFacts(
                runtime.SourceRuntimeType.TypeDefinition,
                facts,
                parameterName,
                $"{role} runtime declaring type");
            ValidateTypeDefinitionWithinFacts(
                runtime.ResolvedTargetRuntimeType.TypeDefinition,
                facts,
                parameterName,
                $"{role} runtime target type");
        }
    }

    private static void ValidateTypeDefinitionTokenWithinFact(
        int token,
        StaticFieldModuleSearchFact fact,
        string parameterName) =>
        ValidateTokenWithinFact(token, fact, parameterName, requireExaminedPrefix: false);

    private static void ValidateMetadataRowWithinFact(
        int typeDefinitionToken,
        int memberDefinitionToken,
        StaticFieldRejectedMemberKind memberKind,
        StaticFieldModuleSearchFact fact,
        string parameterName)
    {
        ValidateTokenWithinFact(typeDefinitionToken, fact, parameterName, requireExaminedPrefix: true);
        byte expectedTable = memberKind switch
        {
            StaticFieldRejectedMemberKind.FieldDefinition => 0x04,
            StaticFieldRejectedMemberKind.PropertyDefinition => 0x17,
            StaticFieldRejectedMemberKind.EventDefinition => 0x14,
            StaticFieldRejectedMemberKind.MethodDefinition => 0x06,
            _ => throw new ArgumentOutOfRangeException(nameof(memberKind)),
        };
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(memberDefinitionToken, expectedTable))
        {
            throw new ArgumentException("The member token table disagrees with its retained kind.", parameterName);
        }
        ValidateTokenWithinFact(
            memberDefinitionToken,
            fact,
            parameterName,
            requireExaminedPrefix: memberKind == StaticFieldRejectedMemberKind.FieldDefinition);
    }

    private static void ValidateTokenWithinFact(
        int token,
        StaticFieldModuleSearchFact fact,
        string parameterName,
        bool requireExaminedPrefix)
    {
        var table = (token >>> 24) & 0xFF;
        var rowId = token & 0x00FF_FFFF;
        var rowCount = table switch
        {
            0x00 => fact.ModuleDefinitionRowCount,
            0x01 => fact.TypeReferenceRowCount,
            0x02 => fact.TypeDefinitionRowCount,
            0x03 => fact.FieldPointerRowCount,
            0x04 => fact.FieldDefinitionRowCount,
            0x05 => fact.MethodPointerRowCount,
            0x06 => fact.MethodDefinitionRowCount,
            0x08 => fact.ParameterDefinitionRowCount,
            0x09 => fact.InterfaceImplementationRowCount,
            0x0A => fact.MemberReferenceRowCount,
            0x0C => fact.CustomAttributeRowCount,
            0x14 => fact.EventDefinitionRowCount,
            0x17 => fact.PropertyDefinitionRowCount,
            0x1A => fact.ModuleReferenceRowCount,
            0x1B => fact.TypeSpecificationRowCount,
            0x20 => fact.AssemblyDefinitionRowCount,
            0x23 => fact.AssemblyReferenceRowCount,
            0x26 => fact.FileRowCount,
            0x27 => fact.ExportedTypeRowCount,
            0x29 => fact.NestedClassRowCount,
            0x2A => fact.GenericParameterRowCount,
            0x2C => fact.GenericParameterConstraintRowCount,
            _ => null,
        };
        if (!rowCount.HasValue || rowId == 0 || rowId > rowCount.Value)
        {
            throw new ArgumentException(
                "A retained metadata token row ID exceeds its exact owner table row count.",
                parameterName);
        }

        if (requireExaminedPrefix &&
            (table == 0x02 && rowId > fact.TypeDefinitionsExamined ||
             table == 0x04 && rowId > fact.FieldDefinitionsExamined))
        {
            throw new ArgumentException(
                "A retained TypeDef/FieldDef token row ID exceeds the counted examined prefix.",
                parameterName);
        }
    }

    private static string DeclarationConflictSubjectKey(StaticFieldRejectedDeclarationEvidence evidence) =>
        $"{evidence.Shape.Sha256}:{evidence.Origin.Sha256}:{evidence.Module.Sha256}:" +
        $"{ModuleContentKey(evidence.ModuleContent)}";

    private static bool IsNamespaceOrEnclosing(string declaringNamespace, string candidateNamespace) =>
        candidateNamespace.Length == 0 ||
        string.Equals(declaringNamespace, candidateNamespace, StringComparison.Ordinal) ||
        declaringNamespace.StartsWith(candidateNamespace + ".", StringComparison.Ordinal);

    private static bool MatchesQualifiedType(string qualifiedType, string namespaceName, string typeName)
    {
        var expected = namespaceName.Length == 0 ? typeName : $"{namespaceName}.{typeName}";
        return string.Equals(qualifiedType, expected, StringComparison.Ordinal);
    }

    private static bool MatchesRuntimeModule(
        StaticFieldModuleInstanceIdentity product,
        ClrmdRuntimeModuleIdentity host)
    {
        var maximumAddress = product.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        var hostCoordinatesFit =
            host.AppDomainAddress is > 0 && host.AppDomainAddress <= maximumAddress &&
            host.ModuleAddress is > 0 && host.ModuleAddress <= maximumAddress &&
            host.ImageBase <= maximumAddress && host.ImageSize <= maximumAddress &&
            (host.ImageBase == 0) == (host.ImageSize == 0) &&
            (host.ImageSize == 0 || host.ImageBase <= maximumAddress - (host.ImageSize - 1));
        return hostCoordinatesFit &&
            string.Equals(product.SnapshotSha256, host.Snapshot.Sha256, StringComparison.Ordinal) &&
            product.ApplicationDomainAddress == host.AppDomainAddress &&
            product.ModuleAddress == host.ModuleAddress &&
            product.ImageBase == host.ImageBase &&
            product.ImageSize == host.ImageSize;
    }

    private static string ModuleContentKey(ModuleContentIdentity content) =>
        $"{content.Mvid:N}:{content.MetadataLength:D10}:{content.MetadataSha256}";

    private static bool ContextStatusMatchesIssue(
        DumpConsultedBindingContextIdentity context,
        StaticFieldBindingIssue issue)
    {
        var statuses = new List<DumpContextEvidenceStatus>(2);
        if (context.CurrentNamespaceConsulted && context.ConsultedFrameEvidence is not null)
        {
            statuses.Add(context.ConsultedFrameEvidence.Status);
        }

        if (context.ImportsConsulted && context.ImportEvidenceStatus.HasValue)
        {
            statuses.Add(context.ImportEvidenceStatus.Value);
        }

        return issue switch
        {
            StaticFieldBindingIssue.ContextPartial => statuses.Contains(DumpContextEvidenceStatus.Partial),
            StaticFieldBindingIssue.ContextUnavailable => statuses.Contains(DumpContextEvidenceStatus.Unavailable),
            StaticFieldBindingIssue.ContextAmbiguous => statuses.Contains(DumpContextEvidenceStatus.Ambiguous),
            StaticFieldBindingIssue.ContextConflict => statuses.Contains(DumpContextEvidenceStatus.Conflict),
            StaticFieldBindingIssue.ContextInvalid => statuses.Contains(DumpContextEvidenceStatus.Invalid),
            StaticFieldBindingIssue.ContextUnsupported => statuses.Contains(DumpContextEvidenceStatus.Unsupported),
            _ => true,
        };
    }
}
