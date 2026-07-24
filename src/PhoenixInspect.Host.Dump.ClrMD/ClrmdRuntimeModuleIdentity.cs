namespace PhoenixInspect.Host.Dump.ClrMD;

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
    ulong ImageSize)
{
    /// <summary>
    /// Gets a stable, path-independent identity projection suitable for backend-neutral result context and replay.
    /// </summary>
    /// <remarks>
    /// The versioned representation uses the dump content digest followed by fixed-width lowercase hexadecimal
    /// application-domain, CLR-module, image-base, and image-size fields. It identifies a runtime module instance;
    /// it is not an assertion that separately acquired file bytes match that loaded instance.
    /// </remarks>
    public string SourceId =>
        $"clrmd-module:v1:{Snapshot.Sha256}:{AppDomainAddress:x16}:{ModuleAddress:x16}:{ImageBase:x16}:{ImageSize:x16}";
}
