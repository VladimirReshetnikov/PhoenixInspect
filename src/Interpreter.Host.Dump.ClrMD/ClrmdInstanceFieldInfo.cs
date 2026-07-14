namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Describes the runtime-selected storage location of one instance field in one dump object.
/// </summary>
public sealed class ClrmdInstanceFieldInfo
{
    internal ClrmdInstanceFieldInfo(
        string name,
        int metadataToken,
        ulong address,
        int size,
        bool isObjectReference,
        string elementType,
        string? fieldTypeName)
    {
        Name = name;
        MetadataToken = metadataToken;
        Address = address;
        Size = size;
        IsObjectReference = isObjectReference;
        ElementType = elementType;
        FieldTypeName = fieldTypeName;
    }

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
}
