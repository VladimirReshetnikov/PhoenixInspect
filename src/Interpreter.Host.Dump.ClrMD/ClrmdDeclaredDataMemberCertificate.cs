using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Interpreter.Core.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Identifies the existing W2 decoder that can interpret one certified terminal storage field.
/// </summary>
/// <remarks>
/// This is a draft W6 admission value. It deliberately contains only decoder kinds already implemented by W2 and
/// does not imply support for arbitrary CLI signatures or conversions.
/// </remarks>
public enum ClrmdTerminalDecoderKind
{
    /// <summary>Decode one managed string reference, including an exact null reference.</summary>
    String,

    /// <summary>Decode one four-byte CLI <see cref="int"/> value.</summary>
    Int32,

    /// <summary>Decode the admitted CLI <see cref="Nullable{T}"/> layout specialized with <see cref="int"/>.</summary>
    NullableInt32,
}

/// <summary>
/// Identifies whether a terminal name denotes storage directly or a property certified as an exact storage projection.
/// </summary>
/// <remarks>This draft W6 discriminator does not admit behavior-bearing properties.</remarks>
public enum ClrmdTerminalMemberKind
{
    /// <summary>The public terminal identity and physical storage are the same FieldDef.</summary>
    Field,

    /// <summary>The public terminal identity is a PropertyDef whose getter projects one backing FieldDef.</summary>
    CertifiedProperty,
}

/// <summary>
/// Freezes one declared intermediate TypeDef projected from complete counted dump metadata.
/// </summary>
/// <remarks>
/// This draft value is meaningful only together with its snapshot-scoped runtime module and complete metadata-content
/// identity. It represents exact declaration identity, not assignability or a future runtime object's exact type.
/// </remarks>
public sealed record ClrmdDeclaredTypeInfo
{
    internal ClrmdDeclaredTypeInfo(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int metadataToken,
        string name)
    {
        RuntimeModule = runtimeModule;
        ModuleContent = moduleContent;
        MetadataToken = metadataToken;
        Name = name;
    }

    /// <summary>Gets the snapshot-scoped module instance containing the declaration.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the identity of the complete counted metadata image containing the declaration.</summary>
    public ModuleContentIdentity ModuleContent { get; }

    /// <summary>Gets the non-nil TypeDef token of the exact declared intermediate type.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the ordinal full metadata name correlated with the runtime type catalog.</summary>
    public string Name { get; }
}

/// <summary>
/// Freezes target-object-relative storage for one directly declared terminal FieldDef.
/// </summary>
/// <remarks>
/// This draft descriptor contains no target object address and cannot read a value. A later evaluator must validate an
/// exact target object and combine its address with <see cref="OffsetFromObject"/> without repeating member lookup.
/// </remarks>
public sealed record ClrmdRelativeFieldInfo
{
    internal ClrmdRelativeFieldInfo(
        int metadataToken,
        string name,
        int offsetFromObject,
        int size,
        bool isObjectReference,
        string elementType,
        string? fieldTypeName,
        ImmutableArray<byte> signature,
        ClrmdRelativeNullableInt32Layout? nullableInt32Layout)
    {
        MetadataToken = metadataToken;
        Name = name;
        OffsetFromObject = offsetFromObject;
        Size = size;
        IsObjectReference = isObjectReference;
        ElementType = elementType;
        FieldTypeName = fieldTypeName;
        Signature = signature;
        NullableInt32Layout = nullableInt32Layout;
    }

    /// <summary>Gets the directly declared terminal or property-backing FieldDef token.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the exact ordinal metadata field name.</summary>
    public string Name { get; }

    /// <summary>Gets the non-negative byte offset from the managed object address to the first storage byte.</summary>
    public int OffsetFromObject { get; }

    /// <summary>Gets the runtime-reported storage size in bytes.</summary>
    public int Size { get; }

    /// <summary>Gets whether ClrMD classifies the storage as a managed object reference.</summary>
    public bool IsObjectReference { get; }

    /// <summary>Gets the frozen runtime element-type display value.</summary>
    public string ElementType { get; }

    /// <summary>Gets the frozen runtime field-type display name, when available.</summary>
    public string? FieldTypeName { get; }

    /// <summary>Gets an immutable copy of the exact FieldDef signature blob.</summary>
    public ImmutableArray<byte> Signature { get; }

    /// <summary>
    /// Gets the nested nullable discriminant/payload layout, or <see langword="null"/> for another decoder kind.
    /// </summary>
    /// <remarks>
    /// The draft layout is frozen during preparation so later evaluation does not repeat nested runtime-field lookup.
    /// </remarks>
    public ClrmdRelativeNullableInt32Layout? NullableInt32Layout { get; }
}

/// <summary>
/// Freezes the two target-field-relative storage ranges needed by the existing nullable Int32 decoder.
/// </summary>
/// <remarks>
/// This draft W6 value contains no absolute address. Offsets are relative to the containing terminal FieldDef storage
/// and are validated as distinct complete ranges before a certificate is issued.
/// </remarks>
public sealed record ClrmdRelativeNullableInt32Layout
{
    internal ClrmdRelativeNullableInt32Layout(
        int hasValueMetadataToken,
        int hasValueOffset,
        int hasValueSize,
        int valueMetadataToken,
        int valueOffset,
        int valueSize)
    {
        HasValueMetadataToken = hasValueMetadataToken;
        HasValueOffset = hasValueOffset;
        HasValueSize = hasValueSize;
        ValueMetadataToken = valueMetadataToken;
        ValueOffset = valueOffset;
        ValueSize = valueSize;
    }

    /// <summary>Gets the runtime-correlated FieldDef token for the Boolean discriminant.</summary>
    public int HasValueMetadataToken { get; }

    /// <summary>Gets the byte offset of the Boolean discriminant relative to terminal storage.</summary>
    public int HasValueOffset { get; }

    /// <summary>Gets the runtime-reported size of the Boolean discriminant.</summary>
    public int HasValueSize { get; }

    /// <summary>Gets the runtime-correlated FieldDef token for the Int32 payload.</summary>
    public int ValueMetadataToken { get; }

    /// <summary>Gets the byte offset of the Int32 payload relative to terminal storage.</summary>
    public int ValueOffset { get; }

    /// <summary>Gets the runtime-reported size of the Int32 payload.</summary>
    public int ValueSize { get; }
}

/// <summary>
/// Proves that one directly declared PropertyDef is exactly a projection of one directly declared backing FieldDef.
/// </summary>
/// <remarks>
/// The draft certificate retains the physical getter evidence but never invokes the getter. Admission requires the
/// complete body <c>ldarg.0; ldfld &lt;backing FieldDef&gt;; ret</c> and rejects every other body shape.
/// </remarks>
public sealed record ClrmdDataPropertyCertificate
{
    internal ClrmdDataPropertyCertificate(
        int propertyToken,
        ImmutableArray<byte> propertySignature,
        int getterToken,
        ImmutableArray<byte> getterSignature,
        ClrmdMethodBodyInfo getterBody)
    {
        PropertyToken = propertyToken;
        PropertySignature = propertySignature;
        GetterToken = getterToken;
        GetterSignature = getterSignature;
        GetterBody = getterBody;
    }

    /// <summary>Gets the directly declared PropertyDef token used as the terminal public identity.</summary>
    public int PropertyToken { get; }

    /// <summary>Gets an immutable copy of the exact PropertyDef signature blob.</summary>
    public ImmutableArray<byte> PropertySignature { get; }

    /// <summary>Gets the directly declared getter MethodDef token selected by method semantics.</summary>
    public int GetterToken { get; }

    /// <summary>Gets an immutable copy of the exact getter MethodDef signature blob.</summary>
    public ImmutableArray<byte> GetterSignature { get; }

    /// <summary>Gets the complete counted physical getter body whose exact instruction sequence was certified.</summary>
    public ClrmdMethodBodyInfo GetterBody { get; }
}

/// <summary>
/// Freezes all W6.3 declaration and storage facts for one root reference field and one terminal data member.
/// </summary>
/// <remarks>
/// This immutable draft certificate is preparation evidence only. Construction reads complete module metadata and,
/// for a property, its physical getter body; it never reads the outer reference, a referenced object, or terminal
/// storage. A later W6 checkpoint will consume the frozen identities during counted evaluation.
/// </remarks>
public sealed class ClrmdDeclaredDataMemberCertificate
{
    private const string CanonicalVersion = "clrmd-declared-data-member-v1";

    internal ClrmdDeclaredDataMemberCertificate(
        string rootTypeName,
        int rootTypeToken,
        ClrmdInstanceFieldInfo outerField,
        ImmutableArray<byte> outerFieldSignature,
        ClrmdDeclaredTypeInfo declaredTarget,
        string terminalMemberName,
        ClrmdTerminalMemberKind terminalMemberKind,
        int terminalMemberToken,
        ClrmdTerminalDecoderKind decoder,
        ClrmdRelativeFieldInfo storage,
        ClrmdDataPropertyCertificate? property)
    {
        RootTypeName = rootTypeName;
        RootTypeToken = rootTypeToken;
        OuterField = outerField;
        OuterFieldSignature = outerFieldSignature;
        DeclaredTarget = declaredTarget;
        TerminalMemberName = terminalMemberName;
        TerminalMemberKind = terminalMemberKind;
        TerminalMemberToken = terminalMemberToken;
        Decoder = decoder;
        Storage = storage;
        Property = property;
    }

    /// <summary>Gets the exact ordinal root runtime and metadata type name.</summary>
    public string RootTypeName { get; }

    /// <summary>Gets the root exact-runtime-type TypeDef token.</summary>
    public int RootTypeToken { get; }

    /// <summary>Gets the object-specific outer reference FieldDef and absolute storage identity.</summary>
    public ClrmdInstanceFieldInfo OuterField { get; }

    /// <summary>Gets an immutable copy of the outer reference FieldDef signature blob.</summary>
    public ImmutableArray<byte> OuterFieldSignature { get; }

    /// <summary>Gets the exact same-module TypeDef declared by the outer field signature.</summary>
    public ClrmdDeclaredTypeInfo DeclaredTarget { get; }

    /// <summary>Gets the exact ordinal public terminal field or property name.</summary>
    public string TerminalMemberName { get; }

    /// <summary>Gets whether the terminal identity is a direct field or a certified property.</summary>
    public ClrmdTerminalMemberKind TerminalMemberKind { get; }

    /// <summary>Gets the terminal public FieldDef or PropertyDef token.</summary>
    public int TerminalMemberToken { get; }

    /// <summary>Gets the existing W2 decoder admitted for the terminal signature.</summary>
    public ClrmdTerminalDecoderKind Decoder { get; }

    /// <summary>Gets the terminal or backing FieldDef storage frozen relative to a future target object.</summary>
    public ClrmdRelativeFieldInfo Storage { get; }

    /// <summary>Gets the property proof, or <see langword="null"/> when the terminal identity is a direct field.</summary>
    public ClrmdDataPropertyCertificate? Property { get; }

    /// <summary>
    /// Produces an injective versioned representation of every identity that can affect later member-chain evaluation.
    /// </summary>
    /// <returns>A deterministic length-delimited replay projection containing no display-only path.</returns>
    /// <remarks>
    /// The draft projection contains target addresses and metadata bytes. It is replay material, not diagnostic text.
    /// </remarks>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, RootTypeName);
        Append(builder, RootTypeToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, OuterField.ToCanonicalReplayProjection());
        Append(builder, Convert.ToHexString(OuterFieldSignature.AsSpan()));
        Append(builder, DeclaredTarget.RuntimeModule.SourceId);
        Append(builder, DeclaredTarget.ModuleContent.Mvid.ToString("D", CultureInfo.InvariantCulture));
        Append(builder, DeclaredTarget.ModuleContent.MetadataLength.ToString(CultureInfo.InvariantCulture));
        Append(builder, DeclaredTarget.ModuleContent.MetadataSha256);
        Append(builder, DeclaredTarget.MetadataToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, DeclaredTarget.Name);
        Append(builder, TerminalMemberName);
        Append(builder, TerminalMemberKind.ToString());
        Append(builder, TerminalMemberToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Decoder.ToString());
        Append(builder, Storage.MetadataToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Storage.Name);
        Append(builder, Storage.OffsetFromObject.ToString(CultureInfo.InvariantCulture));
        Append(builder, Storage.Size.ToString(CultureInfo.InvariantCulture));
        Append(builder, Storage.IsObjectReference ? "1" : "0");
        Append(builder, Storage.ElementType);
        Append(builder, Storage.FieldTypeName is null ? "none" : "value");
        Append(builder, Storage.FieldTypeName ?? string.Empty);
        Append(builder, Convert.ToHexString(Storage.Signature.AsSpan()));
        if (Storage.NullableInt32Layout is not { } nullableLayout)
        {
            Append(builder, "none");
        }
        else
        {
            Append(builder, "nullable-int32-relative-v1");
            Append(builder, nullableLayout.HasValueMetadataToken.ToString(CultureInfo.InvariantCulture));
            Append(builder, nullableLayout.HasValueOffset.ToString(CultureInfo.InvariantCulture));
            Append(builder, nullableLayout.HasValueSize.ToString(CultureInfo.InvariantCulture));
            Append(builder, nullableLayout.ValueMetadataToken.ToString(CultureInfo.InvariantCulture));
            Append(builder, nullableLayout.ValueOffset.ToString(CultureInfo.InvariantCulture));
            Append(builder, nullableLayout.ValueSize.ToString(CultureInfo.InvariantCulture));
        }

        if (Property is null)
        {
            Append(builder, "none");
            return builder.ToString();
        }

        Append(builder, "certified-property-v1");
        Append(builder, Property.PropertyToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Convert.ToHexString(Property.PropertySignature.AsSpan()));
        Append(builder, Property.GetterToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Convert.ToHexString(Property.GetterSignature.AsSpan()));
        Append(builder, Property.GetterBody.RelativeVirtualAddress.ToString(CultureInfo.InvariantCulture));
        Append(builder, Property.GetterBody.HeaderAddress.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Property.GetterBody.HeaderKind.ToString());
        Append(builder, Property.GetterBody.CodeAddress.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Convert.ToHexString(Property.GetterBody.Code.Bytes.AsSpan()));
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}
