using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Refuses every DAC or binary acquisition request routed through ClrMD's <see cref="IFileLocator"/> seam.
/// </summary>
/// <remarks>
/// ClrMD otherwise uses a symbol-server locator derived from ambient/default policy. This replacement blocks later
/// locator-backed acquisition, but it is not a filesystem, network, or DAC-trust boundary: pinned ClrMD may probe
/// target-reported full paths before or outside this seam and may accept a full-path DAC without signature
/// verification. Arbitrary dumps therefore remain behind the documented no-network access-control worker and
/// trusted-DAC gate.
/// </remarks>
internal sealed class ClrmdOfflineFileLocator : IFileLocator
{
    internal static ClrmdOfflineFileLocator Instance { get; } = new();

    private ClrmdOfflineFileLocator()
    {
    }

    /// <inheritdoc />
    public string? FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties) => null;

    /// <inheritdoc />
    public string? FindPEImage(
        string fileName,
        SymbolProperties archivedUnder,
        ImmutableArray<byte> buildIdOrUuid,
        OSPlatform originalPlatform,
        bool checkProperties) => null;

    /// <inheritdoc />
    public string? FindElfImage(
        string fileName,
        SymbolProperties archivedUnder,
        ImmutableArray<byte> buildId,
        bool checkProperties) => null;

    /// <inheritdoc />
    public string? FindMachOImage(
        string fileName,
        SymbolProperties archivedUnder,
        ImmutableArray<byte> uuid,
        bool checkProperties) => null;
}
