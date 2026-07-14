namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries immutable runtime evidence and non-authoritative display hints for a managed module instance.
/// </summary>
public sealed class ClrmdModuleInfo
{
    internal ClrmdModuleInfo(
        ClrmdRuntimeModuleIdentity identity,
        string name,
        string? targetPathHint,
        int appDomainId,
        ulong metadataAddress,
        ulong metadataLength,
        string layout)
    {
        Identity = identity;
        Name = name;
        TargetPathHint = targetPathHint;
        AppDomainId = appDomainId;
        MetadataAddress = metadataAddress;
        MetadataLength = metadataLength;
        Layout = layout;
    }

    /// <summary>
    /// Gets the snapshot-scoped identity of this particular loaded module instance.
    /// </summary>
    public ClrmdRuntimeModuleIdentity Identity { get; }

    /// <summary>
    /// Gets the module display file name reported by the target runtime.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the unmodified path reported by the target runtime, when present.
    /// </summary>
    /// <remarks>
    /// This value is a target-side hint only. It must not be normalized as an analysis-machine path, used as identity,
    /// or opened without independent artifact discovery and identity validation.
    /// </remarks>
    public string? TargetPathHint { get; }

    /// <summary>
    /// Gets the runtime-reported numeric application-domain identifier.
    /// </summary>
    public int AppDomainId { get; }

    /// <summary>
    /// Gets the target virtual address of the module metadata root, or zero when unavailable.
    /// </summary>
    public ulong MetadataAddress { get; }

    /// <summary>
    /// Gets the runtime-reported metadata length in bytes.
    /// </summary>
    public ulong MetadataLength { get; }

    /// <summary>
    /// Gets the ClrMD-reported mapped-image layout name for diagnostics.
    /// </summary>
    public string Layout { get; }
}
