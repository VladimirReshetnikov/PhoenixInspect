namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Describes the runtime-selected storage location of one instance field in one dump object.
/// </summary>
public sealed class ClrmdInstanceFieldInfo
{
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
    /// Gets the immutable, backend-neutral nested layout frozen while the outer field was selected.
    /// </summary>
    internal ClrmdNullableInt32FieldLayout? NullableInt32Layout { get; }
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
    int ValueSize);
