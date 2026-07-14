namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Identifies one managed module instance inside one immutable dump snapshot.
/// </summary>
/// <param name="Snapshot">Content identity of the containing dump.</param>
/// <param name="AppDomainAddress">Target address of the owning CLR application domain.</param>
/// <param name="ModuleAddress">Target address of the CLR module structure.</param>
/// <param name="ImageBase">Target base address of the loaded image, or zero for an image without a mapped base.</param>
/// <param name="ImageSize">Observed mapped-image size in bytes.</param>
/// <remarks>
/// The identity deliberately excludes module name and target path. Those values are lookup and display hints and can
/// collide across loader contexts or refer to stale files on the analysis machine.
/// </remarks>
public readonly record struct ClrmdRuntimeModuleIdentity(
    ClrmdSnapshotIdentity Snapshot,
    ulong AppDomainAddress,
    ulong ModuleAddress,
    ulong ImageBase,
    ulong ImageSize);
