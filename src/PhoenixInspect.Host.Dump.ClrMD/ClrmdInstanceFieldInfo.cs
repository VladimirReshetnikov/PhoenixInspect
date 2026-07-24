using System.Globalization;
using System.Text;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Describes the runtime-selected storage location of one instance field in one dump object.
/// </summary>
public sealed class ClrmdInstanceFieldInfo
{
    private const string CanonicalVersion = "clrmd-instance-field-v1";

    internal ClrmdInstanceFieldInfo(
        ClrmdSnapshotIdentity snapshot,
        ulong ownerAddress,
        ulong ownerMethodTable,
        string ownerTypeName,
        string name,
        int metadataToken,
        ulong address,
        int size,
        bool isObjectReference,
        string elementType,
        string? fieldTypeName,
        ClrmdNullableInt32FieldLayout? nullableInt32Layout)
    {
        Snapshot = snapshot;
        OwnerAddress = ownerAddress;
        OwnerMethodTable = ownerMethodTable;
        OwnerTypeName = ownerTypeName;
        Name = name;
        MetadataToken = metadataToken;
        Address = address;
        Size = size;
        IsObjectReference = isObjectReference;
        ElementType = elementType;
        FieldTypeName = fieldTypeName;
        NullableInt32Layout = nullableInt32Layout;
    }

    /// <summary>
    /// Gets the immutable dump identity from which this field descriptor was selected.
    /// </summary>
    /// <remarks>
    /// A descriptor-consuming read rejects a descriptor whose snapshot differs from either the supplied owner or the
    /// active session. This prevents an address that happens to repeat in another dump from being reused as evidence.
    /// </remarks>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>
    /// Gets the target address of the exact heap object for which this storage location was selected.
    /// </summary>
    public ulong OwnerAddress { get; }

    /// <summary>
    /// Gets the method-table identity of the owning object at selection time.
    /// </summary>
    public ulong OwnerMethodTable { get; }

    /// <summary>
    /// Gets the ordinal runtime type name of the owning object at selection time.
    /// </summary>
    public string OwnerTypeName { get; }

    /// <summary>
    /// Gets the metadata display name of the field.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the FieldDef metadata token supplied by the runtime type description.
    /// </summary>
    public int MetadataToken { get; }

    /// <summary>
    /// Gets the target virtual address of the field storage in the selected object.
    /// </summary>
    public ulong Address { get; }

    /// <summary>
    /// Gets the runtime-reported field size in bytes.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets whether the runtime describes the field as a managed object reference.
    /// </summary>
    public bool IsObjectReference { get; }

    /// <summary>
    /// Gets the runtime element-type classification used to interpret the field's storage.
    /// </summary>
    public string ElementType { get; }

    /// <summary>
    /// Gets the runtime field-type display name when ClrMD can resolve it.
    /// </summary>
    public string? FieldTypeName { get; }

    /// <summary>
    /// Gets whether the selected field has the supported <see cref="Nullable{T}"/> layout specialized with
    /// <see cref="int"/>.
    /// </summary>
    /// <remarks>
    /// This is an admission discriminator, not a decoded-value claim. Binding sets it only after ordinal,
    /// duplicate-free <c>hasValue</c>/<c>value</c> selection and freezes that layout; decoding still validates owner
    /// identity and all counted bytes before returning an answer.
    /// </remarks>
    public bool IsNullableInt32 => NullableInt32Layout is not null;

    /// <summary>
    /// Produces an injective, versioned projection of the complete immutable field descriptor for canonical query-plan
    /// identity and deterministic replay.
    /// </summary>
    /// <returns>
    /// A length-delimited representation of the snapshot and owner identity, outer field identity and type facts, and,
    /// for a supported nullable Int32 field, every frozen discriminator and payload token, address, and size.
    /// </returns>
    /// <remarks>
    /// The projection contains target addresses and target-derived metadata. It is replay material, not suitable for diagnostic display
    /// display text. Nullable child storage participates because changing either child descriptor changes which bytes
    /// the decoder observes even when every outer-field property remains identical.
    /// </remarks>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        AppendCanonicalValue(builder, CanonicalVersion);
        AppendCanonicalValue(builder, Snapshot.Sha256);
        AppendCanonicalValue(builder, OwnerAddress.ToString("x16", CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, OwnerMethodTable.ToString("x16", CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, OwnerTypeName);
        AppendCanonicalValue(builder, Name);
        AppendCanonicalValue(builder, MetadataToken.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, Address.ToString("x16", CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, Size.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, IsObjectReference ? "1" : "0");
        AppendCanonicalValue(builder, ElementType);
        AppendCanonicalValue(builder, FieldTypeName is null ? "none" : "value");
        AppendCanonicalValue(builder, FieldTypeName ?? string.Empty);

        if (NullableInt32Layout is not { } nullableLayout)
        {
            AppendCanonicalValue(builder, "none");
            return builder.ToString();
        }

        AppendCanonicalValue(builder, "nullable-int32-v1");
        AppendCanonicalValue(
            builder,
            nullableLayout.HasValueMetadataToken.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalValue(
            builder,
            nullableLayout.HasValueAddress.ToString("x16", CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, nullableLayout.HasValueSize.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, nullableLayout.ValueMetadataToken.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, nullableLayout.ValueAddress.ToString("x16", CultureInfo.InvariantCulture));
        AppendCanonicalValue(builder, nullableLayout.ValueSize.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    /// <summary>
    /// Gets the immutable, backend-neutral nested layout frozen while the outer field was selected.
    /// </summary>
    internal ClrmdNullableInt32FieldLayout? NullableInt32Layout { get; }

    private static void AppendCanonicalValue(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>
/// Freezes the validated nested storage locations of one supported nullable Int32 field without retaining ClrMD
/// runtime objects beyond the binding operation.
/// </summary>
internal sealed record ClrmdNullableInt32FieldLayout(
    int HasValueMetadataToken,
    ulong HasValueAddress,
    int HasValueSize,
    int ValueMetadataToken,
    ulong ValueAddress,
    int ValueSize)
{
    /// <summary>
    /// Checks that two nested field descriptors are distinct and occupy complete, non-overlapping ranges inside their
    /// outer nullable field.
    /// </summary>
    internal static bool HasValidDistinctStorage(
        ulong outerAddress,
        int outerSize,
        int hasValueMetadataToken,
        ulong hasValueAddress,
        int hasValueSize,
        int valueMetadataToken,
        ulong valueAddress,
        int valueSize)
    {
        if (outerSize < 0 ||
            (ulong)outerSize > ulong.MaxValue - outerAddress ||
            hasValueSize <= 0 ||
            valueSize <= 0 ||
            hasValueMetadataToken == valueMetadataToken ||
            !TryGetRelativeRange(
                outerAddress,
                (ulong)outerSize,
                hasValueAddress,
                hasValueSize,
                out var hasValueStart,
                out var hasValueEnd) ||
            !TryGetRelativeRange(
                outerAddress,
                (ulong)outerSize,
                valueAddress,
                valueSize,
                out var valueStart,
                out var valueEnd))
        {
            return false;
        }

        return hasValueEnd <= valueStart || valueEnd <= hasValueStart;
    }

    private static bool TryGetRelativeRange(
        ulong outerAddress,
        ulong outerSize,
        ulong rangeAddress,
        int rangeSize,
        out ulong rangeStart,
        out ulong rangeEnd)
    {
        rangeStart = 0;
        rangeEnd = 0;
        if (rangeSize < 0 || rangeAddress < outerAddress)
        {
            return false;
        }

        rangeStart = rangeAddress - outerAddress;
        var unsignedRangeSize = (ulong)rangeSize;
        if (rangeStart > outerSize || unsignedRangeSize > outerSize - rangeStart)
        {
            return false;
        }

        rangeEnd = rangeStart + unsignedRangeSize;
        return true;
    }
}
