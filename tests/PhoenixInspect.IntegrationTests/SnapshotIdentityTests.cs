using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises canonical dump-snapshot identity validation without loading a dump.</summary>
public sealed class SnapshotIdentityTests
{
    /// <summary>Checks canonical case normalization and rejection of malformed public identities.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Snapshot_identity_requires_one_complete_sha256_digest()
    {
        var uppercase = new ClrmdSnapshotIdentity(new string('A', 64));

        Assert.Equal(new string('a', 64), uppercase.Sha256);
        Assert.Equal($"dump-sha256:{new string('a', 64)}", uppercase.MemorySourceId);
        Assert.Throws<ArgumentException>(() => new ClrmdSnapshotIdentity("short"));
        Assert.Throws<ArgumentException>(() => new ClrmdSnapshotIdentity(new string('g', 64)));
    }

    /// <summary>Checks that runtime-module identity is path-independent, fixed-width, and snapshot-scoped.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Runtime_module_identity_has_stable_backend_neutral_projection()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('A', 64));
        var module = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x12,
            ModuleAddress: 0x345,
            ImageBase: 0x6789,
            ImageSize: 0xABCD);

        Assert.Equal(
            $"clrmd-module:v1:{new string('a', 64)}:0000000000000012:0000000000000345:0000000000006789:000000000000abcd",
            module.SourceId);
        Assert.DoesNotContain('\\', module.SourceId);
        Assert.DoesNotContain('/', module.SourceId);
    }
}
