using System.Collections.Immutable;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries snapshot identity, root-selection provenance, and runtime type evidence for a dump object.
/// </summary>
public sealed class ClrmdHeapObjectInfo
{
    internal ClrmdHeapObjectInfo(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        string typeName,
        ulong methodTable,
        ulong rootAddress,
        string rootKind,
        ClrmdModuleInfo module,
        ImmutableArray<MemoryReadResult> evidence)
        : this(
            snapshot,
            address,
            typeName,
            typeMetadataToken: 0,
            methodTable,
            rootAddress,
            rootKind,
            module,
            evidence)
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
        ImmutableArray<MemoryReadResult> evidence)
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

    /// <summary>
    /// Gets the target address of the CLR handle slot through which the object was selected.
    /// </summary>
    public ulong RootAddress { get; }

    /// <summary>
    /// Gets the CLR handle kind retained as root-selection provenance.
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
