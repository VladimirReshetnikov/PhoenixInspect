using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Interpreter.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Locks down the adapter's no-acquisition behavior at ClrMD's file-locator seam.</summary>
public sealed class OfflineFileResolutionTests
{
    /// <summary>Checks that every request routed through the replacement locator is refused.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Offline_locator_refuses_every_binary_acquisition_shape()
    {
        var locator = ClrmdOfflineFileLocator.Instance;
        var identity = ImmutableArray<byte>.Empty;

        Assert.Null(locator.FindPEImage("runtime", 1, 2, checkProperties: true));
        Assert.Null(locator.FindPEImage("runtime", default, identity, OSPlatform.Windows, checkProperties: true));
        Assert.Null(locator.FindElfImage("runtime", default, identity, checkProperties: true));
        Assert.Null(locator.FindMachOImage("runtime", default, identity, checkProperties: true));
    }
}
