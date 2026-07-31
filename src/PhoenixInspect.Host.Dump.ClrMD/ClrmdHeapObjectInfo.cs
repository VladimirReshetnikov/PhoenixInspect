using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Identifies how a <see cref="ClrmdHeapObjectInfo"/> entered an instance-evaluation boundary.</summary>
public enum ClrmdHeapObjectSelectionKind
{
    /// <summary>The object was selected through one CLR strong-handle family slot.</summary>
    StrongHandle = 1,

    /// <summary>
    /// The object was already selected by a typed Product binding and is projected only for legacy instance engines.
    /// </summary>
    TypedObjectBinding = 2,

    /// <summary>
    /// The object was validated as an intermediate member-chain reference target and is projected so the next hop
    /// can evaluate against it.
    /// </summary>
    ChainReferencedObject = 3,
}

/// <summary>
/// Carries snapshot identity, root-selection provenance, and runtime type evidence for a dump object.
/// </summary>
public sealed class ClrmdHeapObjectInfo : IClrmdObjectIdentity
{
    internal ClrmdHeapObjectInfo(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        string typeName,
        ulong methodTable,
        ulong rootAddress,
        string rootKind,
        ClrmdModuleInfo module,
        ImmutableArray<MemoryReadResult> evidence,
        ClrmdHeapObjectSelectionKind selectionKind = ClrmdHeapObjectSelectionKind.StrongHandle)
        : this(
            snapshot,
            address,
            typeName,
            typeMetadataToken: 0,
            methodTable,
            rootAddress,
            rootKind,
            module,
            evidence,
            selectionKind)
    {
    }

    internal ClrmdHeapObjectInfo(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        string typeName,
        int typeMetadataToken,
        ulong methodTable,
        ulong rootAddress,
        string rootKind,
        ClrmdModuleInfo module,
        ImmutableArray<MemoryReadResult> evidence,
        ClrmdHeapObjectSelectionKind selectionKind = ClrmdHeapObjectSelectionKind.StrongHandle)
    {
        Snapshot = snapshot;
        Address = address;
        TypeName = typeName;
        TypeMetadataToken = typeMetadataToken;
        MethodTable = methodTable;
        RootAddress = rootAddress;
        RootKind = rootKind;
        Module = module;
        Evidence = evidence;
        SelectionKind = selectionKind;
    }

    /// <summary>
    /// Gets the content identity of the dump containing the object.
    /// </summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>
    /// Gets the target virtual address used as object identity within the immutable snapshot.
    /// </summary>
    public ulong Address { get; }

    /// <summary>
    /// Gets the runtime type name reported by ClrMD.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the TypeDef metadata token reported by ClrMD for the object's exact runtime type.
    /// </summary>
    /// <remarks>
    /// Execution preparation validates this runtime token against the declaring TypeDef projected from counted
    /// metadata. The token is meaningful only together with <see cref="Module"/> and <see cref="Snapshot"/>.
    /// </remarks>
    public int TypeMetadataToken { get; }

    /// <summary>
    /// Gets the target method-table address reported for the object's runtime type.
    /// </summary>
    public ulong MethodTable { get; }

    /// <summary>Gets whether selection came from a legacy strong handle or an independently typed Product binding.</summary>
    public ClrmdHeapObjectSelectionKind SelectionKind { get; }

    /// <summary>
    /// Gets the target address of the CLR handle slot through which the object was selected, or zero for a typed
    /// object-binding projection whose source remains outside this compatibility descriptor.
    /// </summary>
    public ulong RootAddress { get; }

    /// <summary>
    /// Gets the CLR handle kind retained as compatibility provenance, or the stable non-handle marker
    /// <c>TypedObjectBinding</c> when <see cref="SelectionKind"/> is
    /// <see cref="ClrmdHeapObjectSelectionKind.TypedObjectBinding"/>.
    /// </summary>
    public string RootKind { get; }

    /// <summary>
    /// Gets runtime evidence for the module defining the object's runtime type.
    /// </summary>
    public ClrmdModuleInfo Module { get; }

    /// <summary>
    /// Gets immutable raw reads used to select and validate this object, when the selection path required memory reads.
    /// </summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }
}
