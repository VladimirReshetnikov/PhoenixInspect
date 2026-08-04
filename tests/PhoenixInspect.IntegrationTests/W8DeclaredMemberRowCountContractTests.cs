using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the declaration-side row-count bundle and the absence-preserving trailer that carries it on one module
/// search fact.
/// </summary>
/// <remarks>
/// The Constant (0x0B), PropertyMap (0x15), and PropertyPtr (0x16) tables are counted but never enumerated, because
/// the shared metadata reader projects no rows for any of them. Their counts therefore arrive as a separate bundle
/// rather than as columns of the frozen search-fact schema, and the trailer that carries the bundle must be written
/// only when the bundle exists — otherwise every already-frozen search-fact, source-end, and catalog digest in the
/// metadata family would move for observations that never counted those tables at all.
/// </remarks>
public sealed class W8DeclaredMemberRowCountContractTests
{
    private const string ModuleDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves the bundle is all-or-nothing, bounded, and canonical by content.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Declared_member_row_counts_are_bounded_and_canonical_by_content()
    {
        var counts = StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 0);
        Assert.Equal(39, counts.ConstantRowCount);
        Assert.Equal(13, counts.PropertyMapRowCount);
        Assert.Equal(0, counts.PropertyPointerRowCount);

        Assert.Equal(counts, StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 0));
        Assert.Equal(
            counts.Sha256,
            StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 0).Sha256);
        Assert.NotEqual(counts, StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 1));
        Assert.NotEqual(counts, StaticFieldModuleDeclaredMemberRowCounts.Create(39, 12, 0));
        Assert.NotEqual(counts, StaticFieldModuleDeclaredMemberRowCounts.Create(40, 13, 0));

        // Each count is independently bounded; there is no shared or inferred cap.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StaticFieldModuleDeclaredMemberRowCounts.Create(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StaticFieldModuleDeclaredMemberRowCounts.Create(0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StaticFieldModuleDeclaredMemberRowCounts.Create(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldModuleDeclaredMemberRowCounts.Create(
            StaticFieldModuleDeclaredMemberRowCounts.MaximumRowCount + 1,
            0,
            0));
        Assert.Equal(
            StaticFieldModuleDeclaredMemberRowCounts.MaximumRowCount,
            StaticFieldModuleDeclaredMemberRowCounts
                .Create(StaticFieldModuleDeclaredMemberRowCounts.MaximumRowCount, 0, 0)
                .ConstantRowCount);
    }

    /// <summary>
    /// Proves the trailer is absence-preserving: a search fact that observed no declaration-side counts is
    /// byte-identical to the same fact built before the trailer existed, while a fact that observed them is not.
    /// </summary>
    /// <remarks>
    /// The pinned digest is the load-bearing assertion. It was produced by the unchanged schema-4 encoding, so a
    /// future edit that writes anything unconditional at the trailer position — a presence boolean, a zero tag, a
    /// default bundle — fails here rather than silently re-freezing the whole metadata digest family.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Search_fact_trailer_is_written_only_when_the_bundle_is_observed()
    {
        var withoutCounts = ExactFact(declaredMemberRowCounts: null);
        var withCounts = ExactFact(StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 0));

        Assert.Null(withoutCounts.DeclaredMemberRowCounts);
        Assert.Equal(
            "8d40556276f1843cfc740defb5593e1b5f8f31b147ebf12ba0224e360fcb2833",
            withoutCounts.Sha256);

        Assert.NotNull(withCounts.DeclaredMemberRowCounts);
        Assert.Equal(39, withCounts.DeclaredMemberRowCounts!.ConstantRowCount);
        Assert.NotEqual(withoutCounts.Sha256, withCounts.Sha256);
        Assert.NotEqual(withoutCounts, withCounts);

        // The trailer carries content, not merely presence: two observations differing only in a counted end differ.
        Assert.NotEqual(
            withCounts.Sha256,
            ExactFact(StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 1)).Sha256);

        // Replay is exact in both shapes.
        Assert.Equal(withoutCounts.Sha256, ExactFact(declaredMemberRowCounts: null).Sha256);
        Assert.Equal(
            withCounts.Sha256,
            ExactFact(StaticFieldModuleDeclaredMemberRowCounts.Create(39, 13, 0)).Sha256);
    }

    private static StaticFieldModuleSearchFact ExactFact(
        StaticFieldModuleDeclaredMemberRowCounts? declaredMemberRowCounts) =>
        StaticFieldModuleSearchFact.Exact(
            module: StaticFieldModuleInstanceIdentity.Create(
                new string('a', 64),
                sizeof(ulong),
                applicationDomainAddress: 0x1000,
                moduleAddress: 0x2000,
                imageBase: 0x0040_0000,
                imageSize: 0x0001_8000),
            moduleContent: ModuleContentIdentity.FromDigest(
                Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
                metadataLength: 4096,
                metadataSha256: ModuleDigest),
            typeDefinitionsExamined: 13,
            fieldDefinitionsExamined: 41,
            declaredMemberRowCounts: declaredMemberRowCounts);
}
