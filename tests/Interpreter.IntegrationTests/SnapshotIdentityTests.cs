using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

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
}
