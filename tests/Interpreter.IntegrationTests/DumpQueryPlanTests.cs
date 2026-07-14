using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises dump-free W2 root-binding and immutable-plan identity invariants.</summary>
public sealed class DumpQueryPlanTests
{
    /// <summary>
    /// Proves canonical plan fingerprints preserve distinct UTF-16 literal payloads even when ordinary UTF-8
    /// replacement fallback would encode both unpaired surrogates identically.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Plan_fingerprint_is_injective_for_distinct_unpaired_surrogate_literals()
    {
        var (binding, field) = CreateBoundStringField();
        var highSurrogateD800 = new DumpQueryPlan(
            binding,
            field,
            DumpQueryPlanFieldKind.String,
            new DumpQueryLiteral(DumpQueryLiteralKind.String, 0, "\uD800"),
            DumpQueryParserBounds.ExpressionLength |
                DumpQueryParserBounds.RootNameLength |
                DumpQueryParserBounds.FieldNameLength |
                DumpQueryParserBounds.StringLiteralLength,
            ImmutableArray.Create(ClrmdDumpSession.InstanceFieldTraversalBound));
        var highSurrogateD801 = new DumpQueryPlan(
            binding,
            field,
            DumpQueryPlanFieldKind.String,
            new DumpQueryLiteral(DumpQueryLiteralKind.String, 0, "\uD801"),
            DumpQueryParserBounds.ExpressionLength |
                DumpQueryParserBounds.RootNameLength |
                DumpQueryParserBounds.FieldNameLength |
                DumpQueryParserBounds.StringLiteralLength,
            ImmutableArray.Create(ClrmdDumpSession.InstanceFieldTraversalBound));

        Assert.NotEqual(
            highSurrogateD800.ToCanonicalReplayProjection(),
            highSurrogateD801.ToCanonicalReplayProjection());
        Assert.NotEqual(highSurrogateD800.ComputeSha256(), highSurrogateD801.ComputeSha256());
        Assert.Matches("^[0-9a-f]{64}$", highSurrogateD800.ComputeSha256());
        Assert.Equal(EvaluationSemanticMode.DerivedQuery, highSurrogateD800.SemanticMode);
    }

    /// <summary>
    /// Verifies exact absence and non-exact searches retain distinct typed root outcomes, snapshot identity, reads,
    /// and the two bounds enforced by strong-handle traversal.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Root_binding_preserves_search_outcomes_without_selecting_partial_or_ambiguous_matches()
    {
        var (binding, _) = CreateBoundStringField();
        var root = binding.Root!;
        var alternate = CreateRoot(root.Snapshot, address: 0x1100, rootAddress: 0x2100);
        var evidence = ImmutableArray.Create(MemoryReadResult.Create(
            root.Snapshot.MemorySourceId,
            0x2000,
            sizeof(ulong),
            BitConverter.GetBytes(root.Address)));

        var exact = DumpQueryRootBinding.FromSearchResult(
            "root",
            CreateSearch(
                root.Snapshot,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                [root],
                evidence));
        var absent = DumpQueryRootBinding.FromSearchResult(
            "root",
            CreateSearch(
                root.Snapshot,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                [],
                evidence));
        var ambiguous = DumpQueryRootBinding.FromSearchResult(
            "root",
            CreateSearch(
                root.Snapshot,
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                [root, alternate],
                evidence));
        var partial = DumpQueryRootBinding.FromSearchResult(
            "root",
            CreateSearch(
                root.Snapshot,
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                [root],
                evidence));
        var invalid = DumpQueryRootBinding.FromSearchResult(
            "root",
            CreateSearch(
                root.Snapshot,
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                [],
                evidence));

        Assert.Equal(DumpQueryRootBindingStatus.ExactObject, exact.Status);
        Assert.Same(root, exact.Root);
        Assert.Equal(DumpQueryRootBindingStatus.ExhaustiveAbsence, absent.Status);
        Assert.Null(absent.Root);
        Assert.Equal(DumpQueryRootBindingStatus.Conflict, ambiguous.Status);
        Assert.Equal(ClrmdValueIssue.AmbiguousMatch, ambiguous.Issue);
        Assert.Null(ambiguous.Root);
        Assert.Equal(DumpQueryRootBindingStatus.Partial, partial.Status);
        Assert.Equal(ClrmdValueIssue.LimitExceeded, partial.Issue);
        Assert.Null(partial.Root);
        Assert.Equal(DumpQueryRootBindingStatus.Invalid, invalid.Status);
        Assert.Null(invalid.Root);

        foreach (var result in new[] { exact, absent, ambiguous, partial, invalid })
        {
            Assert.Equal(root.Snapshot, result.Snapshot);
            Assert.Equal(evidence.ToArray(), result.Evidence.ToArray());
            Assert.Equal(
                new[] { "root-selection.maximum-handles", "root-selection.maximum-matches" },
                result.AppliedBounds.Select(static bound => bound.Name).ToArray());
            Assert.Equal(new long[] { 31, 2 }, result.AppliedBounds.Select(static bound => bound.Value).ToArray());
        }
    }

    private static (DumpQueryRootBinding Binding, ClrmdInstanceFieldInfo Field) CreateBoundStringField()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var root = CreateRoot(snapshot, address: 0x1000, rootAddress: 0x2000);
        var binding = DumpQueryRootBinding.FromExactObject(
            "root",
            root,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("root-selection.maximum-handles", 31),
                new EvaluationDeterministicBound("root-selection.maximum-matches", 2)));
        var field = new ClrmdInstanceFieldInfo(
            snapshot,
            root.Address,
            root.MethodTable,
            root.TypeName,
            "Message",
            metadataToken: 0x04000001,
            address: root.Address + 16,
            size: sizeof(ulong),
            isObjectReference: true,
            elementType: "String",
            fieldTypeName: "System.String",
            nullableInt32Layout: null);
        return (binding, field);
    }

    private static ClrmdHeapObjectInfo CreateRoot(
        ClrmdSnapshotIdentity snapshot,
        ulong address,
        ulong rootAddress)
    {
        var identity = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x3000,
            ModuleAddress: 0x4000,
            ImageBase: 0x5000,
            ImageSize: 0x6000);
        var module = new ClrmdModuleInfo(
            identity,
            "Fixture.dll",
            targetPathHint: null,
            appDomainId: 1,
            metadataAddress: 0x7000,
            metadataLength: 0x8000,
            layout: "Mapped");
        return new ClrmdHeapObjectInfo(
            snapshot,
            address,
            "Fixture",
            methodTable: 0x9000,
            rootAddress,
            "Strong",
            module,
            ImmutableArray<MemoryReadResult>.Empty);
    }

    private static ClrmdHeapObjectSearchResult CreateSearch(
        ClrmdSnapshotIdentity snapshot,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<ClrmdHeapObjectInfo> matches,
        ImmutableArray<MemoryReadResult> evidence) => new(
            snapshot,
            status,
            issue,
            handlesScanned: 17,
            maximumHandlesScanned: 31,
            maximumMatches: 2,
            matchLimitReached: false,
            matches,
            evidence);
}
