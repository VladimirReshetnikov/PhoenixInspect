using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

internal interface IClrmdObjectIdentity
{
    ClrmdSnapshotIdentity Snapshot { get; }

    ulong Address { get; }

    ulong MethodTable { get; }

    string TypeName { get; }
}

/// <summary>
/// Carries only the intrinsic identity and validated extent of one exact non-root object within a dump snapshot.
/// </summary>
/// <remarks>
/// This W6 identity deliberately excludes the parent field and pointer read. Two alias paths to the same object
/// therefore have equal canonical intrinsic identities while their enclosing observations remain distinct. It has no
/// root slot, root kind, or independent-root claim.
/// </remarks>
public sealed class ClrmdReferencedObjectIdentity : IClrmdObjectIdentity
{
    private const string CanonicalVersion = "clrmd-referenced-object-identity-v1";

    internal ClrmdReferencedObjectIdentity(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        string typeName,
        int typeMetadataToken,
        ulong methodTable,
        ulong size,
        ClrmdModuleInfo module)
    {
        Snapshot = snapshot;
        Address = address;
        TypeName = typeName;
        TypeMetadataToken = typeMetadataToken;
        MethodTable = methodTable;
        Size = size;
        Module = module;
    }

    /// <summary>Gets the immutable snapshot containing this non-root object.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the exact nonzero object address within <see cref="Snapshot"/>.</summary>
    public ulong Address { get; }

    /// <summary>Gets the exact ordinal runtime type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact runtime TypeDef token.</summary>
    public int TypeMetadataToken { get; }

    /// <summary>Gets the method-table pointer validated from counted object-header bytes.</summary>
    public ulong MethodTable { get; }

    /// <summary>Gets the runtime-reported object extent in bytes.</summary>
    public ulong Size { get; }

    /// <summary>Gets the snapshot-scoped module defining the exact runtime type.</summary>
    public ClrmdModuleInfo Module { get; }

    /// <summary>Produces the path-independent canonical identity and extent projection.</summary>
    /// <returns>A deterministic length-delimited representation containing no selection provenance.</returns>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Snapshot.Sha256);
        Append(builder, Snapshot.MemorySourceId);
        Append(builder, Address.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, MethodTable.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Size.ToString(CultureInfo.InvariantCulture));
        Append(builder, TypeName);
        Append(builder, TypeMetadataToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Module.Identity.SourceId);
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

/// <summary>
/// Carries exact non-root object identity and extent derived from one outer reference observation.
/// </summary>
/// <remarks>
/// This W6 descriptor deliberately has no root address, root kind, or claim that the object was independently
/// rooted. Selection provenance remains the outer field and pointer read. Instances contain no live ClrMD object.
/// </remarks>
public sealed class ClrmdReferencedObjectInfo : IClrmdObjectIdentity
{
    private const string CanonicalVersion = "clrmd-referenced-object-v1";

    internal ClrmdReferencedObjectInfo(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        string typeName,
        int typeMetadataToken,
        ulong methodTable,
        ulong size,
        ClrmdModuleInfo module,
        ClrmdObjectReferenceObservation selection,
        ImmutableArray<MemoryReadResult> evidence)
    {
        Identity = new ClrmdReferencedObjectIdentity(
            snapshot,
            address,
            typeName,
            typeMetadataToken,
            methodTable,
            size,
            module);
        Selection = selection;
        Evidence = evidence;
    }

    /// <summary>Gets the path-independent exact object identity and validated extent.</summary>
    public ClrmdReferencedObjectIdentity Identity { get; }

    /// <summary>Gets the immutable dump snapshot containing the referenced object.</summary>
    public ClrmdSnapshotIdentity Snapshot => Identity.Snapshot;

    /// <summary>Gets the exact nonzero target object address decoded from the outer field.</summary>
    public ulong Address => Identity.Address;

    /// <summary>Gets the ordinal exact runtime type name.</summary>
    public string TypeName => Identity.TypeName;

    /// <summary>Gets the exact runtime TypeDef token.</summary>
    public int TypeMetadataToken => Identity.TypeMetadataToken;

    /// <summary>Gets the method-table pointer validated from counted object-header bytes.</summary>
    public ulong MethodTable => Identity.MethodTable;

    /// <summary>Gets the runtime-reported object extent in bytes.</summary>
    public ulong Size => Identity.Size;

    /// <summary>Gets the snapshot-scoped module defining the exact runtime type.</summary>
    public ClrmdModuleInfo Module => Identity.Module;

    /// <summary>Gets the outer-field pointer observation that selected this non-root object.</summary>
    public ClrmdObjectReferenceObservation Selection { get; }

    /// <summary>Gets ordered counted pointer and object-header reads establishing selection and identity.</summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }

    /// <summary>Produces the canonical non-root object and selection-provenance replay projection.</summary>
    /// <returns>A deterministic length-delimited representation.</returns>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Identity.ToCanonicalReplayProjection());
        Append(builder, Selection.ToCanonicalReplayProjection());
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
