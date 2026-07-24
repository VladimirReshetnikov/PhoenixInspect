using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises explicit-route namespace/type name binding over authority-issued named-TypeDef chains.</summary>
public sealed class W8V2TypeNameBindingTests
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const int NestedPublicClassAttributes = 0x0000_0002;
    private const string TargetNamespace = "Synthetic.Target";
    private const string NonAddressableTypeName = "A\u200C";

    private const int GenericSlotToken = 0x0200_0002;
    private const int OuterToken = 0x0200_0003;
    private const int InnerToken = 0x0200_0004;
    private const int NamespacedNodeToken = 0x0200_0007;
    private const int SplitToken = 0x0200_0008;
    private const int NestedNodeToken = 0x0200_0009;
    private const int AlphaToken = 0x0200_000A;
    private const int ChainToken = 0x0200_000B;
    private const int WrapToken = 0x0200_0002;

    /// <summary>
    /// Proves one fully qualified top-level generic owner binds exactly, that every structural partition and every
    /// namespace-versus-nested split is examined, and that partitions naming one physical TypeDef converge.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Fully_qualified_top_level_generic_owner_binds_exactly_and_converges()
    {
        var portfolio = SingleTargetPortfolio();
        var outcome = Bind("global::Synthetic.Target.GenericSlot<int>.Current", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.None, outcome.Issue);
        Assert.Null(outcome.ReachedBound);
        Assert.Equal(3, outcome.PartitionResults.Length);
        Assert.All(
            outcome.PartitionResults,
            static result =>
            {
                Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, result.ResultKind);
                Assert.Equal(StaticFieldV2CandidateKind.QualifiedOwner, result.CandidateKind);
                Assert.Equal(3, result.OwnerSegmentCount);
                Assert.Equal(33, result.ExaminedOccurrenceCount);
                Assert.Equal(StaticFieldV2TypeNameRejectionReason.SimpleNameMismatch, result.FirstRejectionReason);
                var occurrence = Assert.Single(result.Groups.Single().Occurrences);
                Assert.Equal(2, occurrence.NamespaceSegmentCount);
            });
        Assert.Equal(99, outcome.ObservedCount);

        var selected = Assert.IsType<StaticFieldV2TypeNameCandidateGroup>(outcome.SelectedCandidate);
        Assert.Equal(GenericSlotToken, selected.FinalTypeDefinitionToken);
        Assert.Equal(TargetNamespace, selected.FinalTypeDefinition.NamespaceName);
        Assert.Equal("GenericSlot`1", selected.FinalTypeDefinition.TypeName);
        Assert.Equal(1, selected.FinalTypeDefinition.TotalGenericArity);
    }

    /// <summary>
    /// Proves one nested owner binds exactly through per-segment introduced arity and per-segment projected simple
    /// names while its outermost namespace text alone is compared against the namespace split.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nested_owner_binds_exactly_through_per_segment_arity_and_projection()
    {
        var portfolio = SingleTargetPortfolio();
        var outcome = Bind("global::Synthetic.Target.Outer<int>.Inner<string>.Field", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, outcome.ResultKind);
        Assert.Equal(3, outcome.PartitionResults.Length);
        Assert.All(outcome.PartitionResults, static result => Assert.Equal(4, result.OwnerSegmentCount));

        var selected = Assert.IsType<StaticFieldV2TypeNameCandidateGroup>(outcome.SelectedCandidate);
        Assert.Equal(InnerToken, selected.FinalTypeDefinitionToken);
        var occurrence = selected.Occurrences[0];
        Assert.Equal(2, occurrence.NamespaceSegmentCount);
        Assert.Equal(2, occurrence.Chain.Segments.Length);
        Assert.Equal(OuterToken, occurrence.Chain.Segments[0].TypeDefinitionToken);
        Assert.Equal(TargetNamespace, occurrence.Chain.Segments[0].RawNamespaceName);
        Assert.Equal(string.Empty, occurrence.Chain.Segments[1].RawNamespaceName);
        Assert.Equal("Outer", occurrence.Chain.Segments[0].RoslynProjection!.ProjectedSimpleName);
        Assert.Equal("Inner", occurrence.Chain.Segments[1].RoslynProjection!.ProjectedSimpleName);
        Assert.Equal(1, occurrence.Chain.Segments[0].IntroducedGenericArity);
        Assert.Equal(1, occurrence.Chain.Segments[1].IntroducedGenericArity);
        Assert.Equal(2, occurrence.Chain.Segments[1].TotalGenericArity);
    }

    /// <summary>
    /// Proves one owner name that splits both as namespace plus type and as an enclosing plus nested type retains
    /// both physical candidates in the same partition rather than stopping at the first successful split.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Namespace_and_nested_splits_are_both_examined_within_one_partition()
    {
        var portfolio = SingleTargetPortfolio();
        var outcome = Bind("global::Split.Node.Field", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Ambiguous, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.AmbiguousWithinPartition, outcome.Issue);
        Assert.Null(outcome.SelectedCandidate);
        Assert.Equal(3, outcome.PartitionResults.Length);
        Assert.Equal(55, outcome.ObservedCount);

        var ownerOne = outcome.PartitionResults.Single(static result => result.OwnerSegmentCount == 1);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, ownerOne.ResultKind);
        Assert.Equal(SplitToken, ownerOne.Groups.Single().FinalTypeDefinitionToken);

        var ownerTwo = outcome.PartitionResults.Where(static result => result.OwnerSegmentCount == 2).ToArray();
        Assert.Equal(2, ownerTwo.Length);
        Assert.All(
            ownerTwo,
            static result =>
            {
                Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Ambiguous, result.ResultKind);
                Assert.Equal(22, result.ExaminedOccurrenceCount);
                Assert.Equal(
                    [NestedNodeToken, NamespacedNodeToken],
                    result.Groups.Select(static group => group.FinalTypeDefinitionToken).ToArray());
                Assert.Equal(
                    [0, 1],
                    result.Groups.Select(static group => group.Occurrences.Single().NamespaceSegmentCount).ToArray());
            });
    }

    /// <summary>
    /// Proves two partitions whose exact answers name different physical TypeDefs stay ambiguous instead of being
    /// resolved by a longest-prefix or first-partition preference.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Distinct_exact_partitions_stay_ambiguous_across_partitions()
    {
        var portfolio = SingleTargetPortfolio();
        var outcome = Bind("global::Chain.Alpha.Beta", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Ambiguous, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.AmbiguousAcrossPartitions, outcome.Issue);
        Assert.Null(outcome.SelectedCandidate);
        Assert.Equal(
            [ChainToken],
            outcome.PartitionResults
                .Where(static result => result.OwnerSegmentCount == 1)
                .Select(static result => result.Groups.Single().FinalTypeDefinitionToken)
                .ToArray());
        Assert.Equal(
            [AlphaToken, AlphaToken],
            outcome.PartitionResults
                .Where(static result => result.OwnerSegmentCount == 2)
                .Select(static result => result.Groups.Single().FinalTypeDefinitionToken)
                .ToArray());
    }

    /// <summary>
    /// Proves a source arity that disagrees with the introduced physical arity produces a complete absent
    /// answer with its typed rejection evidence retained rather than a stop.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Arity_disagreement_produces_a_complete_absent_answer()
    {
        var portfolio = SingleTargetPortfolio();
        var outcome = Bind("global::Synthetic.Target.GenericSlot<int, string>.Current", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.CandidateAbsent, outcome.Issue);
        Assert.Null(outcome.SelectedCandidate);
        Assert.Equal(3, outcome.PartitionResults.Length);
        Assert.All(
            outcome.PartitionResults,
            static result =>
            {
                Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, result.ResultKind);
                Assert.Empty(result.Groups);
                Assert.Equal(StaticFieldV2TypeNameRejectionReason.ArityMismatch, result.FirstRejectionReason);
            });
    }

    /// <summary>
    /// Proves two modules that spell the same fully qualified name produce two distinct physical groups in
    /// every partition instead of converging.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Cross_module_same_name_produces_two_physical_groups()
    {
        var target = BuildTargetModule();
        var other = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.Other",
            0xB200,
            '2',
            [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                TargetNamespace,
                "GenericSlot`1",
                PublicClassAttributes,
                null,
                GenericArity: 1)]);
        var portfolio = ChainPortfolio(target, other);
        var outcome = Bind("global::Synthetic.Target.GenericSlot<int>.Current", portfolio);

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Ambiguous, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.AmbiguousWithinPartition, outcome.Issue);
        Assert.Null(outcome.SelectedCandidate);
        Assert.All(
            outcome.PartitionResults,
            static result =>
            {
                Assert.Equal(2, result.Groups.Length);
                Assert.Equal(39, result.ExaminedOccurrenceCount);
                Assert.All(
                    result.Groups,
                    static group => Assert.Equal(GenericSlotToken, group.FinalTypeDefinitionToken));
                Assert.Equal(2, result.Groups.Select(static group => group.SourceModule).Distinct().Count());
            });
        Assert.Contains(outcome.PartitionResults[0].Groups, group => group.SourceModule.Equals(target.Module));
        Assert.Contains(outcome.PartitionResults[0].Groups, group => group.SourceModule.Equals(other.Module));
    }

    /// <summary>
    /// Proves a segment whose compiler-name mapping is non-exact can never match, that a chain whose length
    /// disagrees with the examined split is counted but carries no name evidence, and that the module pseudo-type
    /// is examined and typed rather than ever bound.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Non_exact_mapping_shape_and_module_pseudo_type_are_rejected_with_typed_reasons()
    {
        var portfolio = ChainPortfolio(BuildUnderflowModule());

        var underflow = Bind("global::Wrap<int>.Underflow.Field", portfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, underflow.ResultKind);
        Assert.Equal(WrapToken, underflow.SelectedCandidate!.FinalTypeDefinitionToken);
        var nested = underflow.PartitionResults.Single(static result => result.OwnerSegmentCount == 2);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, nested.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameRejectionReason.NonExactCompilerMapping, nested.FirstRejectionReason);
        Assert.Equal(3, nested.ExaminedOccurrenceCount);

        var shape = Bind("global::Wrap<int>.Deep.Deeper.Field", portfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, shape.ResultKind);
        var unmatchable = shape.PartitionResults.Single(static result => result.OwnerSegmentCount == 3);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, unmatchable.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameRejectionReason.SegmentCountMismatch, unmatchable.FirstRejectionReason);
        Assert.Equal(3, unmatchable.ExaminedOccurrenceCount);

        var soloPortfolio = ChainPortfolio(W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.Solo",
            0xC300,
            '3',
            []));
        var solo = Bind("global::Solo.Field", soloPortfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, solo.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.CandidateAbsent, solo.Issue);
        var pseudo = Assert.Single(solo.PartitionResults);
        Assert.Equal(StaticFieldV2TypeNameRejectionReason.ModulePseudoType, pseudo.FirstRejectionReason);
        Assert.Equal(1, pseudo.ExaminedOccurrenceCount);
        Assert.Equal(1, solo.ObservedCount);
    }

    /// <summary>
    /// Proves a metadata name with no ordinary C# simple-name spelling is never reachable from source, because the
    /// complete parser drops the formatting character that made the physical name non-addressable.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Non_addressable_physical_name_is_never_bound_from_source()
    {
        var portfolio = SingleTargetPortfolio();
        var parsed = StaticFieldV2ExpressionParser.Parse(
            $"global::Synthetic.Target.{NonAddressableTypeName}.Field");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        Assert.Equal("A", parsed.Descriptor!.Segments[2].Identifier.DecodedText);

        var outcome = StaticFieldV2TypeNameBinder.BindExplicitRoute(parsed.Descriptor, portfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, outcome.ResultKind);
        Assert.All(outcome.PartitionResults, static result => Assert.Empty(result.Groups));

        var nonAddressable = portfolio.Entries.Single().ChainCatalog.Chains
            .Single(static chain => chain.FinalTypeDefinition.TypeName == NonAddressableTypeName);
        Assert.False(nonAddressable.CanAppearInCSharpNamedType);
        Assert.False(nonAddressable.FinalSegment.CSharpAddressability!.IsAddressable);
    }

    /// <summary>
    /// Proves the named-alias route, the bare-member-only descriptor, and both chain-portfolio prerequisites stop
    /// with prefix-free typed dispositions that retain no partition evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Route_and_prerequisite_stops_are_typed_and_prefix_free()
    {
        var target = BuildTargetModule();
        var portfolio = ChainPortfolio(target);

        AssertStop(
            Bind("alias::Synthetic.Target.GenericSlot<int>.Current", portfolio),
            StaticFieldV2TypeNameBindingResultKind.Unsupported,
            StaticFieldV2TypeNameBindingIssue.ContextRouteNotSupported);
        AssertStop(
            Bind("Current", portfolio),
            StaticFieldV2TypeNameBindingResultKind.Unsupported,
            StaticFieldV2TypeNameBindingIssue.NoQualifiedOwnerPartition);

        var nonExactPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
            [target.ChainCatalog]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.NonExact, nonExactPortfolio.ResultKind);
        AssertStop(
            Bind("global::Synthetic.Target.GenericSlot<int>.Current", nonExactPortfolio),
            StaticFieldV2TypeNameBindingResultKind.NonExact,
            StaticFieldV2TypeNameBindingIssue.ChainPortfolioNonExact);

        var invalidPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create([target.Compatibility]),
            [target.ChainCatalog, target.ChainCatalog]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Invalid, invalidPortfolio.ResultKind);
        AssertStop(
            Bind("global::Synthetic.Target.GenericSlot<int>.Current", invalidPortfolio),
            StaticFieldV2TypeNameBindingResultKind.Invalid,
            StaticFieldV2TypeNameBindingIssue.ChainPortfolioInvalid);

        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2TypeNameBinder.BindExplicitRoute(null!, portfolio));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2TypeNameBinder.BindExplicitRoute(
            StaticFieldV2ExpressionParser.Parse("global::A.B.C").Descriptor!,
            null!));
    }

    /// <summary>
    /// Proves the shared candidate-occurrence cap stops binding at cap plus one without retaining any partial
    /// partition evidence, and records why the grouped-candidate cap can never be reached before it.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Candidate_occurrence_bound_stops_at_cap_plus_one_without_a_prefix()
    {
        const int occurrenceCap = StaticFieldV2TypeNameBindingOutcome.MaximumCandidateOccurrenceCount;
        const int groupCap = StaticFieldV2TypeNameBindingOutcome.MaximumGroupedPhysicalCandidateCount;
        Assert.Equal(4_096, occurrenceCap);
        Assert.Equal(occurrenceCap, groupCap);

        var bulkRows = ImmutableArray.CreateBuilder<W8MetadataAncestryAuthorityContractTests.TypeRow>(300);
        for (var index = 0; index < 300; index++)
        {
            bulkRows.Add(new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Bulk",
                $"Bulk{index}",
                PublicClassAttributes,
                null));
        }
        var bulk = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.Bulk",
            0xD400,
            '4',
            bulkRows.MoveToImmutable());
        var outcome = Bind("global::Zed.Yew.Xis.Field", ChainPortfolio(bulk));

        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.NonExact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2TypeNameBindingIssue.CandidateOccurrenceBoundReached, outcome.Issue);
        Assert.Empty(outcome.PartitionResults);
        Assert.Null(outcome.SelectedCandidate);
        Assert.Equal(occurrenceCap + 1, outcome.ObservedCount);
        var bound = Assert.IsType<EvaluationDeterministicBound>(outcome.ReachedBound);
        Assert.Equal("expression-v2.binding.candidate-occurrences", bound.Name);
        Assert.Equal(occurrenceCap, bound.Value);
        Assert.Contains(
            StaticFieldV2Limits.AllDeclaredBounds,
            declared => declared.Name == "expression-v2.binding.candidate-groups" && declared.Value == groupCap);
    }

    /// <summary>
    /// Proves canonical replay equality, defensive copies, guarded private issuance for every retained row,
    /// the closed public issuer surface, and emitted XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Binding_contracts_are_replayable_immutable_guarded_and_documented()
    {
        var outcome = Bind("global::Synthetic.Target.GenericSlot<int>.Current", SingleTargetPortfolio());
        var replay = Bind("global::Synthetic.Target.GenericSlot<int>.Current", SingleTargetPortfolio());

        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(
            "9e3eabe055cca1df1300ecafbb36500001b374c3463406809df2886bc2c0a457",
            outcome.Sha256);

        var originalBytes = outcome.CanonicalBytes;
        var partition = outcome.PartitionResults[0];
        var group = partition.Groups[0];
        var occurrence = group.Occurrences[0];
        var originalGroups = partition.Groups;
        var originalOccurrences = group.Occurrences;

        var returnedResults = outcome.PartitionResults;
        ImmutableCollectionsMarshal.AsArray(returnedResults)![0] = returnedResults[^1];
        var returnedBytes = outcome.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(partition.Groups)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(group.Occurrences)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(occurrence.CanonicalBytes)![0] ^= 0x33;

        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(originalGroups[0], partition.Groups[0]);
        Assert.Equal(originalOccurrences[0], group.Occurrences[0]);
        Assert.Equal(outcome.PartitionResults[0], partition);

        Assert.False(StaticFieldV2TypeNameBindingOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2TypeNameCandidateOccurrence.Create(
            new object(),
            occurrence.SourceModule,
            occurrence.Chain,
            occurrence.NamespaceSegmentCount,
            occurrence.PartitionIndex));
        Assert.Throws<ArgumentException>(() => StaticFieldV2TypeNameCandidateGroup.Create(
            new object(),
            group.SourceModule,
            group.FinalTypeDefinition,
            group.Occurrences));
        Assert.Throws<ArgumentException>(() => StaticFieldV2TypeNamePartitionResult.Create(
            new object(),
            partition.PartitionIndex,
            partition.CandidateKind,
            partition.OwnerSegmentCount,
            partition.ResultKind,
            partition.Groups,
            partition.ExaminedOccurrenceCount,
            partition.FirstRejectionReason));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2TypeNameRejectionReason),
            typeof(StaticFieldV2TypeNameBindingResultKind),
            typeof(StaticFieldV2TypeNameBindingIssue),
            typeof(StaticFieldV2TypeNameCandidateOccurrence),
            typeof(StaticFieldV2TypeNameCandidateGroup),
            typeof(StaticFieldV2TypeNamePartitionResult),
            typeof(StaticFieldV2TypeNameBindingOutcome),
            typeof(StaticFieldV2TypeNameBinder),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .ToArray();
            if (type == typeof(StaticFieldV2TypeNameBinder))
            {
                Assert.Equal(["BindExplicitRoute"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static StaticFieldV2TypeNameBindingOutcome Bind(
        string expressionText,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse(expressionText);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        return StaticFieldV2TypeNameBinder.BindExplicitRoute(parsed.Descriptor!, chainPortfolio);
    }

    private static MetadataNamedTypeDefinitionChainPortfolioIdentity SingleTargetPortfolio() =>
        ChainPortfolio(BuildTargetModule());

    private static MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio(
        params W8MetadataAncestryAuthorityContractTests.AncestryModule[] modules)
    {
        var compatibility = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [.. modules.Select(static module => module.Compatibility)]);
        Assert.Equal(MetadataDefinitionCompatibilityPortfolioResultKind.Exact, compatibility.ResultKind);
        var portfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibility,
            [.. modules.Select(static module => module.ChainCatalog)]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, portfolio.ResultKind);
        return portfolio;
    }

    private static W8MetadataAncestryAuthorityContractTests.AncestryModule BuildTargetModule() =>
        W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            TargetNamespace,
            0xA100,
            '1',
            [
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    TargetNamespace, "GenericSlot`1", PublicClassAttributes, null, GenericArity: 1),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    TargetNamespace, "Outer`1", PublicClassAttributes, null, GenericArity: 1),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty,
                    "Inner`1",
                    NestedPublicClassAttributes,
                    null,
                    GenericArity: 2,
                    EnclosingTypeRowId: 3),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    TargetNamespace, "Plain", PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    TargetNamespace, NonAddressableTypeName, PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "Split", "Node", PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty, "Split", PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty, "Node", NestedPublicClassAttributes, null, EnclosingTypeRowId: 8),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "Chain", "Alpha", PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty, "Chain", PublicClassAttributes, null),
            ]);

    private static W8MetadataAncestryAuthorityContractTests.AncestryModule BuildUnderflowModule() =>
        W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.Underflow",
            0xE500,
            '5',
            [
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty, "Wrap`1", PublicClassAttributes, null, GenericArity: 1),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    string.Empty, "Underflow", NestedPublicClassAttributes, null, EnclosingTypeRowId: 2),
            ]);

    private static void AssertStop(
        StaticFieldV2TypeNameBindingOutcome outcome,
        StaticFieldV2TypeNameBindingResultKind resultKind,
        StaticFieldV2TypeNameBindingIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Empty(outcome.PartitionResults);
        Assert.Null(outcome.SelectedCandidate);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2TypeNameBinder).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(typeDocumentation.Value));
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, static member =>
                    Assert.False(string.IsNullOrWhiteSpace(member.Value)));
            }
        }
    }
}
