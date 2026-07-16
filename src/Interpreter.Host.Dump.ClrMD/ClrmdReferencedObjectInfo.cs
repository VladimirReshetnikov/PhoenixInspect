using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

internal interface IClrmdObjectIdentity
{
    ClrmdSnapshotIdentity Snapshot { get; }

    ulong Address { get; }

    ulong MethodTable { get; }

    string TypeName { get; }
}

/// <summary>
/// Carries exact non-root object identity and extent derived from one outer reference observation.
/// </summary>
/// <remarks>
/// This draft W6 descriptor deliberately has no root address, root kind, or claim that the object was independently
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
        Snapshot = snapshot;
        Address = address;
        TypeName = typeName;
        TypeMetadataToken = typeMetadataToken;
        MethodTable = methodTable;
        Size = size;
        Module = module;
        Selection = selection;
        Evidence = evidence;
    }

    /// <summary>Gets the immutable dump snapshot containing the referenced object.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the exact nonzero target object address decoded from the outer field.</summary>
    public ulong Address { get; }

    /// <summary>Gets the ordinal exact runtime type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the exact runtime TypeDef token.</summary>
    public int TypeMetadataToken { get; }

    /// <summary>Gets the method-table pointer validated from counted object-header bytes.</summary>
    public ulong MethodTable { get; }

    /// <summary>Gets the runtime-reported object extent in bytes.</summary>
    public ulong Size { get; }

    /// <summary>Gets the snapshot-scoped module defining the exact runtime type.</summary>
    public ClrmdModuleInfo Module { get; }

    /// <summary>Gets the outer-field pointer observation that selected this non-root object.</summary>
    public ClrmdObjectReferenceObservation Selection { get; }

    /// <summary>Gets ordered counted pointer and object-header reads establishing selection and identity.</summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }

    /// <summary>Produces the canonical non-root object and selection-provenance replay projection.</summary>
    /// <returns>A deterministic length-delimited draft representation.</returns>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Snapshot.Sha256);
        Append(builder, Address.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, MethodTable.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Size.ToString(CultureInfo.InvariantCulture));
        Append(builder, TypeName);
        Append(builder, TypeMetadataToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Module.Identity.SourceId);
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
