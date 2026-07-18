using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Proves the W7 static-field syntax and symbol contracts as one relational, compiler-detached pipeline over
/// meaningful synthetic expression, context, expansion, module, declaration, and ambiguity evidence.
/// </summary>
public sealed class W7StaticFieldSyntaxAndSymbolContractTests
{
    private const string SnapshotDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ForeignSnapshotDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly ImmutableArray<EvaluationDeterministicBound> SyntaxBounds =
    [
        new("query.expression.characters", 512),
        new("query.syntax.nodes-and-tokens", 256),
        new("query.syntax.depth", 64),
    ];

    private static readonly ImmutableArray<EvaluationDeterministicBound> NonAncestryBindingBounds =
    [
        new("binding.modules.count", 64),
        new("binding.typedef-rows.count", 4096),
        new("binding.fielddef-rows.count", 16384),
    ];

    private static readonly ImmutableArray<EvaluationDeterministicBound> BindingBounds =
        NonAncestryBindingBounds.Add(StaticFieldTypeAncestryIdentity.DeclaredEdgeCountBound);

    /// <summary>
    /// Proves exact raw identifier spelling remains distinct from decoded values, candidate shape order is canonical,
    /// every mutable array boundary is copied, and canonical equality reacts to replay-significant perturbations.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Syntax_descriptor_is_content_equal_bounded_and_defensively_immutable()
    {
        var segmentBacking = CreateComplexSegments().ToArray();
        var shapeBacking = CreateComplexShapes().ToArray();
        var descriptor = StaticFieldExpressionDescriptor.Create(
            "global::Synthetic.Incident.@Probe.Root.Status.Code ?? \"missing\"",
            hasGlobalQualifier: true,
            ImmutableCollectionsMarshal.AsImmutableArray(segmentBacking),
            ImmutableCollectionsMarshal.AsImmutableArray(shapeBacking),
            StaticFieldParserCounts.Create(20, 18, 7, segmentBacking.Length, shapeBacking.Length),
            SyntaxBounds.Reverse().ToImmutableArray());

        Assert.Equal(StaticFieldExpressionProfile.StaticFieldExpressionV1, descriptor.Profile);
        Assert.Equal("StaticFieldExpressionV1", StaticFieldExpressionDescriptor.ProfileId);
        Assert.Equal("@Probe", descriptor.Segments[2].RawText);
        Assert.Equal("Probe", descriptor.Segments[2].DecodedIdentifier);
        Assert.Equal(StaticFieldSegmentSeparatorKind.Dot, descriptor.Segments[^1].SeparatorKind);
        Assert.Equal(StaticFieldSegmentAccessKind.DirectMember, descriptor.Segments[^1].AccessKind);
        Assert.Equal([3, 4], descriptor.CandidateShapes.Select(static shape => shape.StaticFieldSegmentIndex));
        Assert.Equal(
            SyntaxBounds.OrderBy(static bound => bound.Name).Select(static bound => bound.Name),
            descriptor.ReachedBounds.Select(static bound => bound.Name));

        var replay = StaticFieldExpressionDescriptor.Create(
            descriptor.RawExpression,
            descriptor.HasGlobalQualifier,
            CreateComplexSegments(),
            CreateComplexShapes().Reverse().ToImmutableArray(),
            StaticFieldParserCounts.Create(20, 18, 7, 6, 2),
            SyntaxBounds);
        Assert.Equal(descriptor, replay);
        Assert.Equal(descriptor.GetHashCode(), replay.GetHashCode());
        Assert.Equal(descriptor.Sha256, replay.Sha256);
        Assert.Equal(descriptor.CanonicalBytes.AsSpan().ToArray(), replay.CanonicalBytes.AsSpan().ToArray());

        var duplicateProjection = StaticFieldExpressionDescriptor.Create(
            descriptor.RawExpression,
            descriptor.HasGlobalQualifier,
            CreateComplexSegments(),
            [CreateComplexShapes()[0], CreateComplexShapes()[0], CreateComplexShapes()[1]],
            StaticFieldParserCounts.Create(20, 18, 7, 6, 2),
            SyntaxBounds);
        Assert.Equal(descriptor, duplicateProjection);
        Assert.Equal(2, duplicateProjection.CandidateShapes.Length);
        Assert.All(duplicateProjection.CandidateShapes, static candidate =>
        {
            Assert.Equal(StaticFieldFallbackKind.String, candidate.FallbackKind);
            Assert.Equal("missing", candidate.StringFallback);
        });

        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            descriptor.RawExpression,
            true,
            CreateComplexSegments(),
            [
                CreateComplexShapes()[0],
                StaticFieldCandidateShape.Create(
                    3,
                    StaticFieldSuffixShape.FixedDepthMemberChain,
                    StaticFieldFallbackKind.String,
                    stringFallback: "other"),
            ],
            StaticFieldParserCounts.Create(20, 18, 7, 6, 2),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            descriptor.RawExpression,
            true,
            CreateComplexSegments(),
            [
                CreateComplexShapes()[0],
                StaticFieldCandidateShape.Create(
                    4,
                    StaticFieldSuffixShape.DirectMember,
                    StaticFieldFallbackKind.Int32,
                    int32Fallback: 42),
            ],
            StaticFieldParserCounts.Create(20, 18, 7, 6, 2),
            SyntaxBounds));

        var changedRawSpelling = CreateComplexSegments().ToArray();
        changedRawSpelling[2] = StaticFieldAccessSegment.Create(
            "Probe",
            "Probe",
            StaticFieldSegmentSeparatorKind.Dot,
            StaticFieldSegmentAccessKind.DirectMember);
        var perturbed = StaticFieldExpressionDescriptor.Create(
            descriptor.RawExpression.Replace("@Probe", "Probe", StringComparison.Ordinal),
            true,
            ImmutableArray.CreateRange(changedRawSpelling),
            CreateComplexShapes(),
            StaticFieldParserCounts.Create(20, 18, 7, 6, 2),
            SyntaxBounds);
        Assert.NotEqual(descriptor, perturbed);
        Assert.NotEqual(descriptor.Sha256, perturbed.Sha256);

        segmentBacking[0] = StaticFieldAccessSegment.Create(
            "Poison",
            "Poison",
            StaticFieldSegmentSeparatorKind.GlobalAliasQualifier,
            StaticFieldSegmentAccessKind.Root);
        shapeBacking[0] = StaticFieldCandidateShape.Create(2, StaticFieldSuffixShape.FixedDepthMemberChain);
        Assert.Equal("Synthetic", descriptor.Segments[0].DecodedIdentifier);
        Assert.Equal([3, 4], descriptor.CandidateShapes.Select(static shape => shape.StaticFieldSegmentIndex));

        var returnedSegments = descriptor.Segments;
        ImmutableCollectionsMarshal.AsArray(returnedSegments)![0] = segmentBacking[0];
        var returnedBytes = descriptor.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x7F;
        Assert.Equal("Synthetic", descriptor.Segments[0].DecodedIdentifier);
        Assert.Equal(descriptor.Sha256, replay.Sha256);
    }

    /// <summary>
    /// Exercises accepted, invalid, and unsupported strict union cases plus every pinned front-end boundary and a
    /// malformed neighboring topology without constructing a compiler syntax object.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Syntax_outcomes_and_front_end_caps_reject_contradictory_payloads()
    {
        var descriptor = CreateSimpleDescriptor();
        var accepted = StaticFieldSyntaxOutcome.Accepted(descriptor);
        Assert.Equal(StaticFieldSyntaxStatus.Accepted, accepted.Status);
        Assert.Equal(StaticFieldSyntaxIssue.None, accepted.Issue);
        Assert.Same(descriptor, accepted.Descriptor);
        Assert.Null(accepted.DiagnosticCode);
        Assert.Null(accepted.DiagnosticMessage);

        var invalid = StaticFieldSyntaxOutcome.Invalid(
            string.Empty,
            StaticFieldSyntaxIssue.ParseError,
            "STATIC_PARSE_INVALID",
            "The synthetic expression is empty.",
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var unsupported = StaticFieldSyntaxOutcome.Unsupported(
            "new Synthetic.Incident.Probe()",
            StaticFieldSyntaxIssue.TreeShapeUnsupported,
            "STATIC_TREE_UNSUPPORTED",
            "Object creation is outside the static-field profile.",
            StaticFieldParserCounts.Create(5, 6, 3, 0, 0),
            SyntaxBounds);
        Assert.Null(invalid.Descriptor);
        Assert.Null(unsupported.Descriptor);
        Assert.Equal(StaticFieldSyntaxStatus.Invalid, invalid.Status);
        Assert.Equal(StaticFieldSyntaxStatus.Unsupported, unsupported.Status);
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Invalid(
            "x",
            StaticFieldSyntaxIssue.TreeShapeUnsupported,
            "X",
            "wrong status issue",
            StaticFieldParserCounts.Create(1, 1, 1, 0, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Unsupported(
            "x",
            StaticFieldSyntaxIssue.ParseError,
            "X",
            "wrong status issue",
            StaticFieldParserCounts.Create(1, 1, 1, 0, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Unsupported(
            "new Probe()",
            StaticFieldSyntaxIssue.TreeShapeUnsupported,
            "EMPTY_COUNTS",
            "A complete valid parse cannot have zero traversal counts.",
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Unsupported(
            "new Probe()",
            StaticFieldSyntaxIssue.TreeShapeUnsupported,
            "OVER_CAP_COUNTS",
            "An unsupported complete tree cannot bypass traversal caps.",
            StaticFieldParserCounts.Create(200, 100, 7, 0, 0),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Invalid(
            string.Empty,
            StaticFieldSyntaxIssue.SyntaxBoundReached,
            "MISSING_BOUND",
            "A bound disposition must identify its reached bound.",
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            ImmutableArray<EvaluationDeterministicBound>.Empty));

        var overLength = new string('x', 513);
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Invalid(
            overLength,
            StaticFieldSyntaxIssue.SyntaxBoundReached,
            "STATIC_LENGTH",
            "The expression exceeded the source bound.",
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            SyntaxBounds));
        var boundedFailure = StaticFieldSyntaxOutcome.Invalid(
            string.Empty,
            StaticFieldSyntaxIssue.SyntaxBoundReached,
            "STATIC_LENGTH",
            "The over-length raw expression is intentionally not retained.",
            StaticFieldParserCounts.Create(0, 0, 0, 0, 0),
            SyntaxBounds);
        Assert.Empty(boundedFailure.RawExpression);
        Assert.Throws<ArgumentException>(() => StaticFieldSyntaxOutcome.Invalid(
            "x",
            StaticFieldSyntaxIssue.SyntaxBoundReached,
            "UNREACHED_BOUND",
            "Applied bounds cannot be relabeled as reached without a matching counter.",
            StaticFieldParserCounts.Create(1, 1, 1, 0, 0),
            SyntaxBounds));

        var boundaryIdentifier = new string('i', 64);
        _ = StaticFieldAccessSegment.Create(
            boundaryIdentifier,
            boundaryIdentifier,
            StaticFieldSegmentSeparatorKind.None,
            StaticFieldSegmentAccessKind.Root);
        Assert.Throws<ArgumentException>(() => StaticFieldAccessSegment.Create(
            new string('i', 65),
            new string('i', 65),
            StaticFieldSegmentSeparatorKind.None,
            StaticFieldSegmentAccessKind.Root));
        Assert.Throws<ArgumentException>(() => StaticFieldAccessSegment.Create(
            new string('i', 513),
            "i",
            StaticFieldSegmentSeparatorKind.None,
            StaticFieldSegmentAccessKind.Root));
        Assert.Throws<ArgumentException>(() => StaticFieldAccessSegment.Create(
            "Member",
            "Member",
            StaticFieldSegmentSeparatorKind.ConditionalDot,
            StaticFieldSegmentAccessKind.DirectMember));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field?.Member",
            false,
            [
                StaticFieldAccessSegment.Create("Type", "Type", StaticFieldSegmentSeparatorKind.None, StaticFieldSegmentAccessKind.Root),
                StaticFieldAccessSegment.Create("Field", "Field", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
                StaticFieldAccessSegment.Create("Member", "Member", StaticFieldSegmentSeparatorKind.ConditionalDot, StaticFieldSegmentAccessKind.ConditionalMember),
            ],
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.DirectMember)],
            StaticFieldParserCounts.Create(5, 5, 4, 3, 1),
            SyntaxBounds));

        _ = StaticFieldCandidateShape.Create(
            1,
            StaticFieldSuffixShape.DirectMember,
            StaticFieldFallbackKind.String,
            stringFallback: new string('s', 256));
        Assert.Throws<ArgumentException>(() => StaticFieldCandidateShape.Create(
            1,
            StaticFieldSuffixShape.DirectMember,
            StaticFieldFallbackKind.String,
            stringFallback: new string('s', 257)));

        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            new string('x', 513),
            false,
            CreateTwoSegments(),
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(2, 2, 2, 2, 1),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            CreateTwoSegments(),
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(129, 128, 2, 2, 1),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            CreateTwoSegments(),
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(2, 2, 65, 2, 1),
            SyntaxBounds));
    }

    /// <summary>
    /// Runs the complete relational exact path and proves module/expansion input order cannot influence the selected
    /// declaration or canonical result.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Exact_binding_groups_distinct_origins_and_is_order_independent()
    {
        var descriptor = CreateSimpleDescriptor();
        var context = FullyQualifiedContext(SnapshotDigest);
        var module = CreateModule(SnapshotDigest, appDomainAddress: 0x1000, moduleAddress: 0x2000);
        var searchedModule = CreateModule(SnapshotDigest, appDomainAddress: 0x1000, moduleAddress: 0x3000);
        var content = CreateModuleContent('c');
        var searchedContent = CreateModuleContent('d');
        var declaration = CreateInt32Declaration(module, content);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var global = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var fact = StaticFieldModuleSearchFact.Exact(module, content, 37, 91);
        var searchedFact = StaticFieldModuleSearchFact.Exact(searchedModule, searchedContent, 12, 20);
        var globalCandidate = StaticFieldSymbolCandidate.Create(declaration, shape, [global]);

        var first = StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [global],
            [fact, searchedFact],
            [globalCandidate],
            BindingBounds);
        var reordered = StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [global],
            [searchedFact, fact],
            [globalCandidate],
            BindingBounds.Reverse().ToImmutableArray());

        Assert.Equal(StaticFieldBindingStatus.Exact, first.Status);
        Assert.Equal(StaticFieldBindingIssue.None, first.Issue);
        Assert.Same(descriptor, first.Descriptor);
        Assert.Equal(context, first.ConsultedContext);
        Assert.Equal(declaration, first.SelectedDeclaration);
        Assert.Equal(shape, first.SelectedShape);
        Assert.Equal(1, first.DistinctCandidateCount);
        Assert.Equal(1, first.CandidateOriginCount);
        Assert.Single(Assert.Single(first.Candidates).Origins);
        Assert.True(first.SearchExhaustive);
        Assert.Equal(49, first.TypeDefinitionsExamined);
        Assert.Equal(111, first.FieldDefinitionsExamined);
        Assert.Equal(first, reordered);
        Assert.Equal(first.Sha256, reordered.Sha256);
        Assert.Equal(first.GetHashCode(), reordered.GetHashCode());
        Assert.Equal(first.CanonicalBytes.AsSpan().ToArray(), reordered.CanonicalBytes.AsSpan().ToArray());

        var candidatesReturned = first.Candidates;
        ImmutableCollectionsMarshal.AsArray(candidatesReturned)![0] = StaticFieldSymbolCandidate.Create(
            CreateInt32Declaration(searchedModule, searchedContent),
            shape,
            [global]);
        var bytesReturned = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(bytesReturned)![0] ^= 0xFF;
        Assert.Single(Assert.Single(first.Candidates).Origins);
        Assert.Equal(first.Sha256, reordered.Sha256);
    }

    /// <summary>
    /// Proves physically distinct module/domain instances and genuinely different syntax interpretations are true
    /// ambiguity, while an exact exhaustive zero-candidate search is the separate Absent case.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Ambiguity_is_over_complete_interpretations_and_absence_is_exact_zero()
    {
        var descriptor = CreateSimpleDescriptor();
        var context = FullyQualifiedContext(SnapshotDigest);
        var content = CreateModuleContent('d');
        var firstModule = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var secondModule = CreateModule(SnapshotDigest, 0x3000, 0x4000);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var expansion = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var firstCandidate = StaticFieldSymbolCandidate.Create(
            CreateInt32Declaration(firstModule, content),
            shape,
            [expansion]);
        var secondCandidate = StaticFieldSymbolCandidate.Create(
            CreateInt32Declaration(secondModule, content),
            shape,
            [expansion]);
        var ambiguous = StaticFieldSymbolBindingOutcome.Ambiguous(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [
                StaticFieldModuleSearchFact.Exact(firstModule, content, 10, 20),
                StaticFieldModuleSearchFact.Exact(secondModule, content, 10, 20),
            ],
            [firstCandidate, secondCandidate],
            "STATIC_BIND_AMBIGUOUS",
            "Two loaded module instances contain the complete declaration.",
            BindingBounds);
        Assert.Equal(StaticFieldBindingStatus.Ambiguous, ambiguous.Status);
        Assert.Equal(StaticFieldBindingIssue.MultipleCandidates, ambiguous.Issue);
        Assert.Equal(2, ambiguous.DistinctCandidateCount);
        Assert.Null(ambiguous.SelectedDeclaration);
        Assert.Null(ambiguous.SelectedShape);

        var sameDeclarationDescriptor = CreateSameFieldDifferentShapeDescriptor();
        var suffixShape = sameDeclarationDescriptor.CandidateShapes.Single(static candidate =>
            candidate.StaticFieldSegmentIndex == 3);
        var terminalShape = sameDeclarationDescriptor.CandidateShapes.Single(static candidate =>
            candidate.StaticFieldSegmentIndex == 4);
        var suffixExpansion = CreateExpansion(suffixShape, StaticFieldNameExpansionKind.GlobalQualified);
        var terminalExpansion = StaticFieldNameExpansion.Create(
            terminalShape,
            StaticFieldNameExpansionKind.GlobalQualified,
            "Synthetic.Incident.Probe",
            "Root",
            "Root");
        var suffixDeclaration = CreateConcreteDeclaration(firstModule, content);
        var terminalDeclaration = CreateInt32Declaration(
            firstModule,
            content,
            typeDefinitionToken: 0x02000004,
            fieldDefinitionToken: 0x04000005,
            namespaceName: "Synthetic.Incident.Probe",
            typeName: "Root",
            fieldName: "Root");
        var shapeCandidates = ImmutableArray.Create(
            StaticFieldSymbolCandidate.Create(suffixDeclaration, suffixShape, [suffixExpansion]),
            StaticFieldSymbolCandidate.Create(terminalDeclaration, terminalShape, [terminalExpansion]));
        var shapeAmbiguity = StaticFieldSymbolBindingOutcome.Ambiguous(
            sameDeclarationDescriptor,
            SnapshotDigest,
            context,
            [suffixExpansion, terminalExpansion],
            [StaticFieldModuleSearchFact.Exact(firstModule, content, 10, 20)],
            shapeCandidates,
            "STATIC_SHAPE_AMBIGUOUS",
            "One FieldDef supports two complete static/suffix interpretations.",
            BindingBounds);
        Assert.Equal(2, shapeAmbiguity.DistinctCandidateCount);
        Assert.Equal(2, shapeAmbiguity.Candidates.Select(static candidate => candidate.Declaration).Distinct().Count());
        Assert.Equal(2, shapeAmbiguity.Candidates.Select(static candidate => candidate.Shape).Distinct().Count());

        var absent = StaticFieldSymbolBindingOutcome.Absent(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [StaticFieldModuleSearchFact.Exact(firstModule, content, 10, 20)],
            "STATIC_BIND_ABSENT",
            "The exhaustive counted search found no matching FieldDef.",
            NonAncestryBindingBounds);
        Assert.Equal(StaticFieldBindingStatus.Absent, absent.Status);
        Assert.Equal(StaticFieldBindingIssue.DeclarationAbsent, absent.Issue);
        Assert.True(absent.SearchExhaustive);
        Assert.Empty(absent.Candidates);
        Assert.Null(absent.SelectedDeclaration);
        Assert.NotEqual(absent.Sha256, ambiguous.Sha256);
    }

    /// <summary>
    /// Constructs every non-exact binding disposition with its own typed source evidence and verifies none can expose
    /// a selected declaration, while unsupported and conflict replay retains the exact facts that caused the stop.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Every_binding_status_has_source_specific_complete_payload_invariants()
    {
        var descriptor = CreateSimpleDescriptor();
        var context = FullyQualifiedContext(SnapshotDigest);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var expansion = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('e');
        var otherContent = CreateModuleContent('f');

        var partial = StaticFieldSymbolBindingOutcome.Partial(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.ModuleSearchPartial,
            "STATIC_METADATA_PARTIAL",
            "Only a counted metadata prefix was available.",
            [expansion],
            [StaticFieldModuleSearchFact.Partial(module, StaticFieldModuleSearchIssue.MetadataPartial, 4, 7)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            NonAncestryBindingBounds);
        var unavailable = StaticFieldSymbolBindingOutcome.Unavailable(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.ModuleUnavailable,
            "STATIC_METADATA_UNAVAILABLE",
            "The counted module metadata was unavailable.",
            [expansion],
            [StaticFieldModuleSearchFact.Unavailable(module)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            NonAncestryBindingBounds);
        var conflictFact = StaticFieldModuleSearchFact.Conflict(module, [content, otherContent], 1, 0);
        var conflict = StaticFieldSymbolBindingOutcome.Conflict(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.ModuleConflict,
            "STATIC_MODULE_CONFLICT",
            "Two complete metadata content identities disagree.",
            [expansion],
            [conflictFact],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            NonAncestryBindingBounds);
        Assert.Equal(2, Assert.Single(conflict.ModuleSearchFacts).ModuleContents.Length);

        var invalidEvidence = CreateRejectedEvidence(
            shape,
            expansion,
            module,
            content,
            StaticFieldBindingIssue.MetadataInvalid,
            "FIELD_SIGNATURE_MALFORMED",
            fieldAttributes: (int)(FieldAttributes.Public | FieldAttributes.Static));
        var invalid = StaticFieldSymbolBindingOutcome.Invalid(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.MetadataInvalid,
            "STATIC_METADATA_INVALID",
            "The exact FieldDef signature is malformed.",
            [expansion],
            [StaticFieldModuleSearchFact.Invalid(module, 2, 1, content)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            [invalidEvidence],
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            BindingBounds);
        var unsupportedEvidence = StaticFieldRejectedDeclarationEvidence.NonField(
            shape,
            expansion,
            module,
            content,
            "Synthetic.Incident",
            "Probe",
            0x02000002,
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            0,
            StaticFieldRejectedMemberKind.PropertyDefinition,
            "Root",
            0x17000001,
            memberAttributes: 0,
            [0x08, 0x00, 0x08],
            StaticFieldBindingIssue.DeclarationShapeUnsupported,
            "STATIC_PROPERTY");
        var unsupported = StaticFieldSymbolBindingOutcome.Unsupported(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.DeclarationShapeUnsupported,
            "STATIC_DECLARATION_UNSUPPORTED",
            "The exact declaration is a literal rather than ordinary static storage.",
            [expansion],
            [StaticFieldModuleSearchFact.Exact(
                module,
                content,
                2,
                0,
                propertyDefinitionRowCount: 1)],
            [unsupportedEvidence],
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            BindingBounds);

        var outcomes = new[] { partial, unavailable, conflict, invalid, unsupported };
        Assert.Equal(
            [
                StaticFieldBindingStatus.Partial,
                StaticFieldBindingStatus.Unavailable,
                StaticFieldBindingStatus.Conflict,
                StaticFieldBindingStatus.Invalid,
                StaticFieldBindingStatus.Unsupported,
            ],
            outcomes.Select(static outcome => outcome.Status));
        Assert.All(outcomes, static outcome =>
        {
            Assert.Null(outcome.SelectedDeclaration);
            Assert.Null(outcome.SelectedShape);
            Assert.NotNull(outcome.DiagnosticCode);
            Assert.NotNull(outcome.DiagnosticMessage);
        });
        Assert.Single(invalid.RejectedDeclarations);
        Assert.Single(unsupported.RejectedDeclarations);
        Assert.NotEqual(invalid.Sha256, unsupported.Sha256);

        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Partial(
            descriptor,
            SnapshotDigest,
            context,
            StaticFieldBindingIssue.ModuleUnavailable,
            "WRONG",
            "The issue belongs to another status.",
            [expansion],
            [StaticFieldModuleSearchFact.Partial(module, StaticFieldModuleSearchIssue.MetadataPartial, 1, 1)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            true,
            true,
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Absent(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [StaticFieldModuleSearchFact.Partial(module, StaticFieldModuleSearchIssue.MetadataPartial, 1, 1)],
            "FALSE_ABSENCE",
            "A partial search cannot prove absence.",
            BindingBounds));
    }

    /// <summary>
    /// Proves declaration conflict is exactly one metadata projection plus one independently observable runtime
    /// projection for the same syntax/module subject, with an explicit disagreement in overlapping facts.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Declaration_conflict_requires_two_independent_disagreeing_projections_of_one_subject()
    {
        var descriptor = CreateSimpleDescriptor();
        var shape = Assert.Single(descriptor.CandidateShapes);
        var expansion = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('8');
        var declaringType = CreateReferenceClassAncestry(module, content).SubjectType;
        var metadataField = CreateFieldDefinition(
            declaringType,
            "Root",
            0x04000003,
            (int)(FieldAttributes.Public | FieldAttributes.Static),
            [0x06, 0x08]);
        var metadata = StaticFieldRejectedDeclarationEvidence.Field(
            shape,
            expansion,
            metadataField,
            StaticFieldDeclaredValueKind.Int32,
            StaticFieldBindingIssue.DeclarationConflict,
            "COUNTED_FIELD");
        Assert.Throws<ArgumentException>(() => StaticFieldRejectedDeclarationEvidence.Field(
            shape,
            expansion,
            metadataField,
            StaticFieldDeclaredValueKind.String,
            StaticFieldBindingIssue.DeclarationConflict,
            "FORGED_VALUE_KIND"));
        var runtime = StaticFieldRejectedDeclarationEvidence.RuntimeFieldConflict(
            shape,
            expansion,
            module,
            content,
            "Synthetic.Incident",
            "Probe",
            0x02000002,
            "Root",
            0x04000004,
            isStatic: true,
            StaticFieldDeclaredValueKind.Int32,
            "RUNTIME_FIELD_TOKEN");
        var conflict = StaticFieldSymbolBindingOutcome.Conflict(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            StaticFieldBindingIssue.DeclarationConflict,
            "STATIC_DECLARATION_CONFLICT",
            "Counted metadata and runtime projection disagree on FieldDef token.",
            [expansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 4, 4)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            [metadata, runtime],
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            BindingBounds);
        Assert.Equal(StaticFieldBindingIssue.DeclarationConflict, conflict.Issue);
        Assert.Equal(2, conflict.RejectedDeclarations.Length);
        Assert.Empty(runtime.MemberSignature);
        Assert.Null(runtime.MemberAttributes);

        var agreeingRuntime = StaticFieldRejectedDeclarationEvidence.RuntimeFieldConflict(
            shape,
            expansion,
            module,
            content,
            "Synthetic.Incident",
            "Probe",
            0x02000002,
            "Root",
            0x04000003,
            isStatic: true,
            StaticFieldDeclaredValueKind.Int32,
            "RUNTIME_AGREES");
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Conflict(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            StaticFieldBindingIssue.DeclarationConflict,
            "FALSE_CONFLICT",
            "Equal overlapping facts cannot be relabeled conflict.",
            [expansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 4, 4)],
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            [metadata, agreeingRuntime],
            true,
            true,
            BindingBounds));
    }

    /// <summary>
    /// Proves a typed selected-frame current namespace supplies an exact expansion fact, mismatched facts are rejected,
    /// and a context failure before expansion can retain zero expansion candidates without fabricating a symbol.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Contextual_expansions_are_validated_against_typed_consulted_facts()
    {
        var descriptor = CreateSimpleDescriptor(globalQualifier: false);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var contextual = CreateCurrentNamespaceContext(exactFrame: true);
        var exactFrame = Assert.IsType<DumpSelectedFrameIdentity>(contextual.ConsultedFrameEvidence!.Frame);
        var expansion = StaticFieldNameExpansion.Create(
            shape,
            StaticFieldNameExpansionKind.CurrentNamespace,
            "Synthetic.Incident",
            "Probe",
            "Root",
            contextFactSha256: exactFrame.Sha256);
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('c');
        var declaration = CreateInt32Declaration(module, content);
        var candidate = StaticFieldSymbolCandidate.Create(
            declaration,
            Assert.Single(descriptor.CandidateShapes),
            [expansion]);
        var exact = StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            contextual,
            [expansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 4, 9)],
            [candidate],
            BindingBounds);
        Assert.Equal(declaration, exact.SelectedDeclaration);

        var forgedExpansion = StaticFieldNameExpansion.Create(
            shape,
            StaticFieldNameExpansionKind.CurrentNamespace,
            "Synthetic.Incident",
            "Probe",
            "Root",
            contextFactSha256: new string('9', 64));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            contextual,
            [forgedExpansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 4, 9)],
            [StaticFieldSymbolCandidate.Create(declaration, Assert.Single(descriptor.CandidateShapes), [forgedExpansion])],
            BindingBounds));

        var unavailableContext = CreateCurrentNamespaceContext(exactFrame: false);
        var blocked = StaticFieldSymbolBindingOutcome.Unavailable(
            descriptor,
            SnapshotDigest,
            unavailableContext,
            StaticFieldBindingIssue.ContextUnavailable,
            "STATIC_CONTEXT_UNAVAILABLE",
            "The selected frame was unavailable before current-namespace expansion.",
            ImmutableArray<StaticFieldNameExpansion>.Empty,
            ImmutableArray<StaticFieldModuleSearchFact>.Empty,
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            moduleCatalogExhaustive: false,
            expansionSearchExhaustive: false,
            NonAncestryBindingBounds);
        Assert.Empty(blocked.Expansions);
        Assert.Empty(blocked.Candidates);
        Assert.Null(blocked.SelectedDeclaration);
    }

    /// <summary>
    /// Proves assembly-qualified import resolution is counted from its exact selected-frame module to one target
    /// TypeDef, rejects a same-name declaration in another module, and preserves ambiguous/unsupported context stops.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Import_resolution_and_nonexact_context_dispositions_are_typed_and_module_exact()
    {
        var descriptor = CreateSimpleDescriptor(globalQualifier: false);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var (context, import, sourceModule, sourceContent) = CreateAssemblyNamespaceImportContext();
        var targetModule = CreateModule(SnapshotDigest, 0x1000, 0x3000);
        var targetContent = CreateModuleContent('5');
        var resolution = StaticFieldReferenceResolutionFact.Create(
            import.Sha256,
            sourceModule,
            sourceContent,
            0x23000001,
            sourceTypeToken: null,
            targetModule,
            targetContent,
            0x02000002);
        var expansion = StaticFieldNameExpansion.Create(
            shape,
            StaticFieldNameExpansionKind.NamespaceImport,
            "Synthetic.Incident",
            "Probe",
            "Root",
            contextFactSha256: import.Sha256,
            referenceResolution: resolution);
        var declaration = CreateInt32Declaration(targetModule, targetContent);
        var candidate = StaticFieldSymbolCandidate.Create(declaration, shape, [expansion]);
        var exact = StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [
                StaticFieldModuleSearchFact.Exact(
                    sourceModule,
                    sourceContent,
                    3,
                    3,
                    assemblyReferenceRowCount: 1),
                StaticFieldModuleSearchFact.Exact(targetModule, targetContent, 4, 3),
            ],
            [candidate],
            BindingBounds);
        Assert.Equal(declaration, exact.SelectedDeclaration);

        var wrongModule = CreateModule(SnapshotDigest, 0x1000, 0x4000);
        var wrongContent = CreateModuleContent('6');
        var wrongCandidate = StaticFieldSymbolCandidate.Create(
            CreateInt32Declaration(wrongModule, wrongContent),
            shape,
            [expansion]);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [
                StaticFieldModuleSearchFact.Exact(
                    sourceModule,
                    sourceContent,
                    3,
                    3,
                    assemblyReferenceRowCount: 1),
                StaticFieldModuleSearchFact.Exact(targetModule, targetContent, 4, 3),
                StaticFieldModuleSearchFact.Exact(wrongModule, wrongContent, 4, 3),
            ],
            [wrongCandidate],
            BindingBounds));

        var ambiguousContext = CreateNonExactCurrentNamespaceContext(DumpContextEvidenceStatus.Ambiguous);
        var contextAmbiguous = StaticFieldSymbolBindingOutcome.ContextAmbiguous(
            descriptor,
            SnapshotDigest,
            ambiguousContext,
            "STATIC_CONTEXT_AMBIGUOUS",
            "Two selected-frame candidates remained.",
            ImmutableArray<StaticFieldNameExpansion>.Empty,
            ImmutableArray<StaticFieldModuleSearchFact>.Empty,
            ImmutableArray<StaticFieldSymbolCandidate>.Empty,
            moduleCatalogExhaustive: false,
            expansionSearchExhaustive: false,
            NonAncestryBindingBounds);
        Assert.Equal(StaticFieldBindingIssue.ContextAmbiguous, contextAmbiguous.Issue);

        var unsupportedContext = CreateNonExactCurrentNamespaceContext(DumpContextEvidenceStatus.Unsupported);
        var contextUnsupported = StaticFieldSymbolBindingOutcome.Unsupported(
            descriptor,
            SnapshotDigest,
            unsupportedContext,
            StaticFieldBindingIssue.ContextUnsupported,
            "STATIC_CONTEXT_UNSUPPORTED",
            "The selected frame representation is outside the admitted context profile.",
            ImmutableArray<StaticFieldNameExpansion>.Empty,
            ImmutableArray<StaticFieldModuleSearchFact>.Empty,
            ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
            moduleCatalogExhaustive: false,
            expansionSearchExhaustive: false,
            NonAncestryBindingBounds);
        Assert.Equal(StaticFieldBindingIssue.ContextUnsupported, contextUnsupported.Issue);
    }

    /// <summary>
    /// Exercises default-array, duplicate-evidence, wrong-token, foreign-snapshot, module-content, shape, attribute,
    /// and reference-target contradictions at the exact factory where each defect first becomes knowable.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Invalid_inputs_cannot_be_normalized_into_false_exactness()
    {
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            default,
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(2, 2, 2, 2, 1),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            CreateTwoSegments(),
            default,
            StaticFieldParserCounts.Create(2, 2, 2, 2, 1),
            SyntaxBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            CreateTwoSegments(),
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(2, 2, 2, 2, 1),
            default));
        Assert.Throws<ArgumentException>(() => StaticFieldExpressionDescriptor.Create(
            "Type.Field",
            false,
            CreateTwoSegments(),
            [StaticFieldCandidateShape.Create(1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(2, 2, 2, 2, 1),
            [new("duplicate.bound.count", 1), new("duplicate.bound.count", 1)]));

        var descriptor = CreateSimpleDescriptor();
        var context = FullyQualifiedContext(SnapshotDigest);
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('1');
        var declaration = CreateInt32Declaration(module, content);
        var shape = Assert.Single(descriptor.CandidateShapes);
        var expansion = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var fact = StaticFieldModuleSearchFact.Exact(module, content, 3, 3);
        var candidate = StaticFieldSymbolCandidate.Create(declaration, shape, [expansion]);

        Assert.Throws<ArgumentException>(() => StaticFieldSymbolCandidate.Create(
            declaration,
            shape,
            [expansion, expansion]));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion, expansion],
            [fact],
            [candidate],
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [fact, fact],
            [candidate],
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            default,
            [fact],
            [candidate],
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            default,
            [candidate],
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [fact],
            default,
            BindingBounds));

        var foreignModule = CreateModule(ForeignSnapshotDigest, 0x1000, 0x2000);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Absent(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [StaticFieldModuleSearchFact.Exact(foreignModule, content, 3, 3)],
            "FOREIGN",
            "Foreign snapshots cannot enter one binding.",
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(ForeignSnapshotDigest),
            [expansion],
            [fact],
            [candidate],
            BindingBounds));
        var otherContent = CreateModuleContent('2');
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            context,
            [expansion],
            [StaticFieldModuleSearchFact.Exact(module, otherContent, 3, 3)],
            [candidate],
            BindingBounds));
        Assert.Throws<ArgumentException>(() => StaticFieldModuleSearchFact.Conflict(
            module,
            [content, content]));

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInt32Declaration(
            module,
            content,
            typeDefinitionToken: 0x04000001));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInt32Declaration(
            module,
            content,
            fieldDefinitionToken: 0x02000001));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldModuleInstanceIdentity.Create(
            SnapshotDigest,
            sizeof(ulong),
            0,
            0x2000,
            0x400000,
            0x1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldModuleInstanceIdentity.Create(
            SnapshotDigest,
            sizeof(ulong),
            0x1000,
            0,
            0x400000,
            0x1000));

        var nestedAttributes = (int)(TypeAttributes.NestedPublic | TypeAttributes.Class);
        var metadataModule = CreateMetadataModule(module, content);
        var outer = CreateTypeDefinition(
            metadataModule,
            0x02000005,
            "Synthetic.Incident",
            "Outer",
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            genericArity: 0,
            extendsMetadataToken: 0x02000001);
        var nested = CreateTypeDefinition(
            metadataModule,
            0x02000004,
            string.Empty,
            "NestedTarget",
            nestedAttributes,
            genericArity: 0,
            extendsMetadataToken: 0x02000001,
            enclosingType: outer);
        var coreLibrary = CreateCoreLibrary(metadataModule);
        var nestedAncestry = StaticFieldTypeAncestryIdentity.Create(
            nested,
            [StaticFieldTypeAncestryEdge.Create(nested, coreLibrary.SystemObjectType)],
            coreLibrary);
        Assert.False(nestedAncestry.SubjectType.IsTopLevel);
        Assert.Equal(outer, nestedAncestry.SubjectType.EnclosingType);
        var genericTarget = CreateReferenceClassAncestry(
            module,
            content,
            0x02000004,
            "Synthetic.Incident",
            "GenericTarget`1",
            genericArity: 1);
        Assert.Throws<ArgumentException>(() => StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeDefinition(
            genericTarget.SubjectType.MetadataModule,
            genericTarget));
        var declaring = CreateReferenceClassAncestry(module, content);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolDeclarationIdentity.Create(
            declaring,
            CreateFieldDefinition(
                declaring.SubjectType,
                "Root",
                0x04000003,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x0E]),
            StaticFieldDeclaredValueKind.String,
            referenceTarget: null));
    }

    /// <summary>
    /// Proves descriptor spellings, mandatory shape coverage, context independence, receiver shape, and counted row
    /// prefixes are relational invariants rather than caller assertions.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Descriptor_expansion_context_and_counted_row_relations_reject_forged_exactness()
    {
        var descriptor = CreateSimpleDescriptor();
        var shape = Assert.Single(descriptor.CandidateShapes);
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('7');
        var declaration = CreateInt32Declaration(module, content);
        var expansion = CreateExpansion(shape, StaticFieldNameExpansionKind.GlobalQualified);
        var candidate = StaticFieldSymbolCandidate.Create(declaration, shape, [expansion]);

        var wrongTypeExpansion = StaticFieldNameExpansion.Create(
            shape,
            StaticFieldNameExpansionKind.GlobalQualified,
            "Other.Namespace",
            "OtherType",
            "Root");
        var wrongTypeDeclaration = CreateInt32Declaration(
            module,
            content,
            namespaceName: "Other.Namespace",
            typeName: "OtherType");
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            [wrongTypeExpansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 3, 3)],
            [StaticFieldSymbolCandidate.Create(wrongTypeDeclaration, shape, [wrongTypeExpansion])],
            BindingBounds));

        var dotAgainstGlobal = StaticFieldNameExpansion.Create(
            shape,
            StaticFieldNameExpansionKind.DotQualified,
            "Synthetic.Incident",
            "Probe",
            "Root");
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Absent(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            [dotAgainstGlobal],
            [StaticFieldModuleSearchFact.Exact(module, content, 3, 3)],
            "DOT_GLOBAL_MISMATCH",
            "Literal global qualification cannot be relabeled dot-qualified.",
            BindingBounds));

        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            CreateCurrentNamespaceContext(exactFrame: true),
            [expansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 3, 3)],
            [candidate],
            BindingBounds));

        var simpleDescriptor = CreateSimpleDescriptor(globalQualifier: false);
        var simpleShape = Assert.Single(simpleDescriptor.CandidateShapes);
        var illegalDot = StaticFieldNameExpansion.Create(
            simpleShape,
            StaticFieldNameExpansionKind.DotQualified,
            "Synthetic.Incident",
            "Probe",
            "Root");
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Absent(
            simpleDescriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            [illegalDot],
            [StaticFieldModuleSearchFact.Exact(module, content, 3, 3)],
            "BARE_TYPE_WITHOUT_CONTEXT",
            "A bare type cannot acquire a context-independent namespace.",
            BindingBounds));

        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            descriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            [expansion],
            [StaticFieldModuleSearchFact.Exact(
                module,
                content,
                typeDefinitionsExamined: 1,
                fieldDefinitionsExamined: 1,
                typeDefinitionRowCount: 10,
                fieldDefinitionRowCount: 10)],
            [candidate],
            BindingBounds));

        var coreModule = CreateModule(SnapshotDigest, 0x1000, 0x3000);
        var coreContent = CreateModuleContent('a');
        var stringAncestry = CreateReferenceClassAncestry(
                coreModule,
                coreContent,
                0x02000007,
                "System",
                "String",
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed));
        var stringTarget = StaticFieldDeclaredReferenceIdentity.PrimitiveSystemString(
            CreateMetadataModule(module, content),
            stringAncestry);
        var stringOwner = CreateReferenceClassAncestry(module, content);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolDeclarationIdentity.Create(
            stringOwner,
            CreateFieldDefinition(
                stringOwner.SubjectType,
                "Root",
                0x04000003,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x12, 0x15]),
            StaticFieldDeclaredValueKind.String,
            stringTarget));

        var multiShapeDescriptor = CreateSameFieldDifferentShapeDescriptor();
        var oneShape = multiShapeDescriptor.CandidateShapes.Single(static candidateShape =>
            candidateShape.StaticFieldSegmentIndex == 3);
        var oneExpansion = CreateExpansion(oneShape, StaticFieldNameExpansionKind.GlobalQualified);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Absent(
            multiShapeDescriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            [oneExpansion],
            [StaticFieldModuleSearchFact.Exact(module, content, 10, 10)],
            "INCOMPLETE_SHAPE_SEARCH",
            "One retained descriptor shape was never attempted.",
            BindingBounds));
        var oneShapeRejection = StaticFieldRejectedDeclarationEvidence.NonField(
            oneShape,
            oneExpansion,
            module,
            content,
            "Synthetic.Incident",
            "Probe",
            0x02000002,
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            0,
            StaticFieldRejectedMemberKind.PropertyDefinition,
            "Root",
            0x17000001,
            memberAttributes: 0,
            [0x08, 0x00, 0x08],
            StaticFieldBindingIssue.DeclarationShapeUnsupported,
            "UNSUPPORTED_PROPERTY");
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Unsupported(
            multiShapeDescriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            StaticFieldBindingIssue.DeclarationShapeUnsupported,
            "INCOMPLETE_UNSUPPORTED_SHAPE_SEARCH",
            "One candidate split was not searched before declaring the expression shape unsupported.",
            [oneExpansion],
            [StaticFieldModuleSearchFact.Exact(
                module,
                content,
                10,
                0,
                propertyDefinitionRowCount: 1)],
            [oneShapeRejection],
            moduleCatalogExhaustive: true,
            expansionSearchExhaustive: true,
            BindingBounds));

        var suffixShape = multiShapeDescriptor.CandidateShapes.Single(static candidateShape =>
            candidateShape.StaticFieldSegmentIndex == 3);
        var suffixExpansion = CreateExpansion(suffixShape, StaticFieldNameExpansionKind.GlobalQualified);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolBindingOutcome.Exact(
            multiShapeDescriptor,
            SnapshotDigest,
            FullyQualifiedContext(SnapshotDigest),
            multiShapeDescriptor.CandidateShapes.Select(candidateShape =>
                candidateShape.StaticFieldSegmentIndex == 3
                    ? suffixExpansion
                    : StaticFieldNameExpansion.Create(
                        candidateShape,
                        StaticFieldNameExpansionKind.GlobalQualified,
                        "Synthetic.Incident.Probe",
                        "Root",
                        "Root")).ToImmutableArray(),
            [StaticFieldModuleSearchFact.Exact(module, content, 10, 10)],
            [StaticFieldSymbolCandidate.Create(declaration, suffixShape, [suffixExpansion])],
            BindingBounds));
    }

    /// <summary>
    /// Proves exact string and concrete-reference targets retain sufficient identity/shape, and signature arrays are
    /// defensively copied on input and output rather than exposing mutable storage through ImmutableArray wrappers.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Declaration_reference_targets_and_signature_bytes_are_complete_and_immutable()
    {
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('3');
        var coreLibraryModule = CreateModule(SnapshotDigest, 0x1000, 0x3000);
        var coreLibraryContent = CreateModuleContent('4');
        var stringAncestry = CreateReferenceClassAncestry(
            coreLibraryModule,
            coreLibraryContent,
            0x0200000A,
            "System",
            "String",
            (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed));
        var stringTarget = StaticFieldDeclaredReferenceIdentity.PrimitiveSystemString(
            CreateMetadataModule(module, content),
            stringAncestry);
        var signatureBacking = new byte[] { 0x06, 0x0E };
        var stringOwner = CreateReferenceClassAncestry(
            module,
            content,
            typeAttributes: (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit),
            fieldListRowId: 4,
            fieldListEndExclusiveRowId: 5);
        var stringDeclaration = StaticFieldSymbolDeclarationIdentity.Create(
            stringOwner,
            CreateFieldDefinition(
                stringOwner.SubjectType,
                "Message",
                0x04000004,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                ImmutableCollectionsMarshal.AsImmutableArray(signatureBacking)),
            StaticFieldDeclaredValueKind.String,
            stringTarget);
        Assert.Equal(StaticFieldDeclaredReferenceKind.SystemString, stringDeclaration.ReferenceTarget!.Kind);
        Assert.Equal("System", stringDeclaration.ReferenceTarget.NamespaceName);
        Assert.Equal("String", stringDeclaration.ReferenceTarget.TypeName);
        Assert.Null(stringDeclaration.ReferenceTarget.TypeMetadataToken);
        Assert.Null(stringDeclaration.ReferenceTarget.AssemblyReferenceToken);
        Assert.Equal(coreLibraryModule, stringDeclaration.ReferenceTarget.ResolvedTargetModule);
        Assert.Equal(0x0200000A, stringDeclaration.ReferenceTarget.ResolvedTargetTypeDefinitionToken);

        signatureBacking[0] = 0xFF;
        var returnedSignature = stringDeclaration.FieldSignature;
        ImmutableCollectionsMarshal.AsArray(returnedSignature)![0] = 0xFE;
        Assert.Equal(0x06, stringDeclaration.FieldSignature[0]);

        var managedTargetAncestry = CreateReferenceClassAncestry(
                module,
                content,
                0x02000005,
                "Synthetic.Incident",
                "RequestEnvelope");
        var concreteTarget = StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeDefinition(
            managedTargetAncestry.SubjectType.MetadataModule,
            managedTargetAncestry);
        var concreteOwner = CreateReferenceClassAncestry(
            module,
            content,
            fieldListRowId: 5,
            fieldListEndExclusiveRowId: 6);
        var concreteDeclaration = StaticFieldSymbolDeclarationIdentity.Create(
            concreteOwner,
            CreateFieldDefinition(
                concreteOwner.SubjectType,
                "Envelope",
                0x04000005,
                (int)(FieldAttributes.Private | FieldAttributes.Static),
                [0x06, 0x12, 0x14]),
            StaticFieldDeclaredValueKind.ManagedReference,
            concreteTarget);
        Assert.True(concreteDeclaration.IsDeclaringTypeClass);
        Assert.True(concreteDeclaration.IsDeclaringTypeTopLevel);
        Assert.Equal(0, concreteDeclaration.GenericArity);
        Assert.True(concreteTarget.IsClass);
        Assert.True(concreteTarget.IsTopLevel);
        Assert.False(concreteTarget.IsCrossModuleTarget);
        Assert.NotEqual(stringDeclaration, concreteDeclaration);
        Assert.NotEqual(stringDeclaration.Sha256, concreteDeclaration.Sha256);

        var ownerMetadata = CreateMetadataModule(module, content);
        var coreLibrary = CreateCoreLibrary(ownerMetadata);
        var objectAncestry = StaticFieldTypeAncestryIdentity.Create(
            coreLibrary.SystemObjectType,
            ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
            coreLibrary);
        var primitiveTarget = StaticFieldDeclaredReferenceIdentity.PrimitiveSystemObject(
            ownerMetadata,
            objectAncestry);
        var objectOwner = CreateReferenceClassAncestry(
            module,
            content,
            fieldListRowId: 6,
            fieldListEndExclusiveRowId: 7);
        var primitiveDeclaration = StaticFieldSymbolDeclarationIdentity.Create(
            objectOwner,
            CreateFieldDefinition(
                objectOwner.SubjectType,
                "PrimitiveObject",
                0x04000006,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x1C]),
            StaticFieldDeclaredValueKind.Object,
            primitiveTarget);
        Assert.Null(primitiveDeclaration.ReferenceTarget!.TypeMetadataToken);
        Assert.Null(primitiveDeclaration.ReferenceTarget.AssemblyReferenceToken);
        Assert.Equal(StaticFieldDeclaredReferenceKind.SystemObject, primitiveDeclaration.ReferenceTarget.Kind);

        var nullableAncestry = CreateValueTypeAncestry(
                coreLibraryModule,
                coreLibraryContent,
                0x02000007,
                "System",
                "Nullable`1",
                genericArity: 1,
                fieldListRowId: 9,
                fieldListEndExclusiveRowId: 11);
        var assemblyReference = StaticFieldAssemblyReferenceIdentity.Create(
            0x23000002,
            "System.Private.CoreLib",
            8,
            0,
            0,
            0,
            string.Empty,
            flags: 0,
            ImmutableArray<byte>.Empty,
            ImmutableArray<byte>.Empty);
        var nullableTypeReference = StaticFieldTypeReferenceRowIdentity.Create(
            0x01000006,
            "System",
            "Nullable`1",
            assemblyReference.AssemblyReferenceToken);
        var nullableResolution = StaticFieldTypeReferenceResolutionIdentity.ForDirectAssemblyReference(
            ownerMetadata,
            [nullableTypeReference],
            assemblyReference,
            nullableAncestry.SubjectType);
        var nullableTarget = StaticFieldNullableTypeIdentity.Create(
            ownerMetadata,
            nullableTypeReference.TypeReferenceToken,
            nullableResolution,
            nullableAncestry);
        var nullableOwner = CreateReferenceClassAncestry(
            module,
            content,
            fieldListRowId: 7,
            fieldListEndExclusiveRowId: 8);
        var int32Ancestry = CreateValueTypeAncestry(
            coreLibraryModule,
            coreLibraryContent,
            0x02000008,
            "System",
            "Int32");
        var nullableDeclaration = StaticFieldSymbolDeclarationIdentity.Create(
            nullableOwner,
            CreateFieldDefinition(
                nullableOwner.SubjectType,
                "OptionalCount",
                0x04000007,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x15, 0x11, 0x19, 0x01, 0x08]),
            StaticFieldDeclaredValueKind.NullableInt32,
            nullableType: nullableTarget,
            systemInt32TypeAncestry: int32Ancestry);
        Assert.Equal("Nullable`1", nullableDeclaration.NullableType!.TypeName);

        var booleanAncestry = CreateValueTypeAncestry(
            coreLibraryModule,
            coreLibraryContent,
            0x02000009,
            "System",
            "Boolean");
        var hasValueDefinition = CreateFieldDefinition(
            nullableAncestry.SubjectType,
            "hasValue",
            0x04000009,
            (int)FieldAttributes.Private,
            [0x06, 0x02]);
        var valueDefinition = CreateFieldDefinition(
            nullableAncestry.SubjectType,
            "value",
            0x0400000A,
            (int)FieldAttributes.Private,
            [0x06, 0x13, 0x00]);
        var snapshot = new ClrmdSnapshotIdentity(SnapshotDigest);
        var runtimeProgramModule = RuntimeModule(snapshot, module);
        var runtimeCoreLibraryModule = RuntimeModule(snapshot, coreLibraryModule);
        var runtimeInt32 = RuntimeType(
            snapshot,
            runtimeCoreLibraryModule,
            coreLibraryContent,
            int32Ancestry.SubjectType,
            "System.Int32",
            methodTable: 0x7100,
            isValueType: true,
            isPrimitive: true);
        var runtimeBoolean = RuntimeType(
            snapshot,
            runtimeCoreLibraryModule,
            coreLibraryContent,
            booleanAncestry.SubjectType,
            "System.Boolean",
            methodTable: 0x7200,
            isValueType: true,
            isPrimitive: true);
        var runtimeNullable = RuntimeType(
            snapshot,
            runtimeCoreLibraryModule,
            coreLibraryContent,
            nullableAncestry.SubjectType,
            "System.Nullable<System.Int32>",
            methodTable: 0x7300,
            isValueType: true,
            isPrimitive: false,
            [runtimeInt32]);
        var runtimeOwner = RuntimeType(
            snapshot,
            runtimeProgramModule,
            content,
            nullableOwner.SubjectType,
            "Synthetic.Incident.Probe",
            methodTable: 0x7400,
            isValueType: false,
            isPrimitive: false);
        var runtimeField = ClrmdStaticRuntimeFieldIdentity.Create(
            runtimeOwner,
            nullableDeclaration.FieldDefinitionToken,
            nullableDeclaration.FieldName,
            (FieldAttributes)nullableDeclaration.FieldAttributes,
            runtimeReportsThreadStatic: false,
            runtimeReportsContextStatic: false,
            ClrmdStaticExpectedDecoderKind.NullableInt32,
            runtimeNullable);
        var runtimeMapping = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
            runtimeOwner,
            runtimeField,
            ClrmdStaticRuntimeDeclarationMappingCounters.Create(17, 6, 1, 1, true, true));
        var rawLayout = ClrmdStaticNullableRuntimeLayoutIdentity.Create(
            runtimeMapping,
            storageSize: 8,
            [
                ClrmdStaticNullableRuntimeFieldIdentity.Create(
                    runtimeNullable,
                    valueDefinition.FieldDefinitionToken,
                    valueDefinition.Name,
                    offset: 4,
                    size: sizeof(int),
                    runtimeInt32),
                ClrmdStaticNullableRuntimeFieldIdentity.Create(
                    runtimeNullable,
                    hasValueDefinition.FieldDefinitionToken,
                    hasValueDefinition.Name,
                    offset: 0,
                    size: sizeof(byte),
                    runtimeBoolean),
            ]);

        var semanticLayout = StaticFieldNullableInt32RuntimeLayoutIdentity.Create(
            nullableDeclaration,
            booleanAncestry,
            hasValueDefinition,
            valueDefinition,
            rawLayout);

        Assert.Equal(rawLayout.StorageSize, semanticLayout.StorageSize);
        Assert.Equal(0, semanticLayout.HasValueRuntimeField.Offset);
        Assert.Equal(4, semanticLayout.ValueRuntimeField.Offset);
        Assert.Equal(rawLayout.RuntimeMapping.Field.ObservedFieldType, semanticLayout.RuntimeNullableType);
        Assert.Throws<ArgumentException>(() => StaticFieldNullableInt32RuntimeLayoutIdentity.Create(
            nullableDeclaration,
            booleanAncestry,
            valueDefinition,
            hasValueDefinition,
            rawLayout));

        var forgedOwner = CreateReferenceClassAncestry(
            module,
            content,
            fieldListRowId: 8,
            fieldListEndExclusiveRowId: 10);
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolDeclarationIdentity.Create(
            forgedOwner,
            CreateFieldDefinition(
                forgedOwner.SubjectType,
                "ForgedMessage",
                0x04000008,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x0E]),
            StaticFieldDeclaredValueKind.String,
            concreteTarget));
        Assert.Throws<ArgumentException>(() => StaticFieldSymbolDeclarationIdentity.Create(
            forgedOwner,
            CreateFieldDefinition(
                forgedOwner.SubjectType,
                "ForgedNullable",
                0x04000009,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x15, 0x11, 0x1D, 0x01, 0x08]),
            StaticFieldDeclaredValueKind.NullableInt32,
            nullableType: nullableTarget,
            systemInt32TypeAncestry: int32Ancestry));
    }

    /// <summary>
    /// Builds a transitive constructed-interface graph with TypeDef and cross-module TypeRef heads, then proves the
    /// exact InterfaceImpl/TypeSpec rows remain composable while malformed signatures and incomplete table facts fail.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Constructed_generic_interface_rows_retain_transitive_exact_resolution()
    {
        var module = CreateModule(SnapshotDigest, 0x1000, 0x2000);
        var content = CreateModuleContent('8');
        var metadataModule = CreateMetadataModule(module, content);
        var rootInterface = CreateTypeDefinition(
            metadataModule,
            0x02000004,
            "Synthetic.Contracts",
            "IRoot`1",
            (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
            genericArity: 1,
            extendsMetadataToken: null);
        var leafInterface = CreateTypeDefinition(
            metadataModule,
            0x02000005,
            "Synthetic.Contracts",
            "ILeaf`1",
            (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
            genericArity: 1,
            extendsMetadataToken: null);
        var implementingClass = CreateReferenceClassAncestry(
            module,
            content,
            0x02000006,
            "Synthetic.Model",
            "Envelope",
            fieldListRowId: 1,
            fieldListEndExclusiveRowId: 1).SubjectType;
        var boxType = CreateReferenceClassAncestry(
            module,
            content,
            0x02000007,
            "Synthetic.Model",
            "Box`1",
            genericArity: 1,
            fieldListRowId: 1,
            fieldListEndExclusiveRowId: 1).SubjectType;
        var rootAncestry = StaticFieldTypeAncestryIdentity.Create(
            rootInterface,
            ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
            coreLibrary: null);
        var leafAncestry = StaticFieldTypeAncestryIdentity.Create(
            leafInterface,
            ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
            coreLibrary: null);

        // GENERICINST CLASS IRoot`1<SZARRAY VAR 0>.
        var leafToRootSignature = ImmutableArray.Create<byte>(0x15, 0x12, 0x10, 0x01, 0x1D, 0x13, 0x00);
        var leafToRoot = StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000001,
            leafInterface,
            0x1B000001,
            leafToRootSignature,
            genericHeadTypeReferenceResolution: null,
            rootAncestry);

        // GENERICINST CLASS ILeaf`1<GENERICINST CLASS Box`1<STRING>>.
        var classToLeafSignature = ImmutableArray.Create<byte>(
            0x15, 0x12, 0x14, 0x01, 0x15, 0x12, 0x1C, 0x01, 0x0E);
        var classToLeaf = StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000002,
            implementingClass,
            0x1B000002,
            classToLeafSignature,
            genericHeadTypeReferenceResolution: null,
            leafAncestry);

        var contractsModule = CreateModule(SnapshotDigest, 0x1000, 0x3000);
        var contractsContent = CreateModuleContent('9');
        var contractsMetadataModule = CreateMetadataModule(
            contractsModule,
            contractsContent,
            "Synthetic.Remote.Contracts");
        var remoteInterface = CreateTypeDefinition(
            contractsMetadataModule,
            0x02000004,
            "Synthetic.Remote",
            "IRemote`1",
            (int)(TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract),
            genericArity: 1,
            extendsMetadataToken: null);
        var remoteAncestry = StaticFieldTypeAncestryIdentity.Create(
            remoteInterface,
            ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
            coreLibrary: null);
        var contractsAssemblyReference = StaticFieldAssemblyReferenceIdentity.Create(
            0x23000001,
            "Synthetic.Remote.Contracts",
            8,
            0,
            0,
            0,
            string.Empty,
            flags: 0,
            ImmutableArray<byte>.Empty,
            ImmutableArray<byte>.Empty);
        var remoteTypeReference = StaticFieldTypeReferenceRowIdentity.Create(
            0x01000001,
            "Synthetic.Remote",
            "IRemote`1",
            contractsAssemblyReference.AssemblyReferenceToken);
        var remoteResolution = StaticFieldTypeReferenceResolutionIdentity.ForDirectAssemblyReference(
            metadataModule,
            [remoteTypeReference],
            contractsAssemblyReference,
            remoteInterface);

        // GENERICINST CLASS TypeRef(IRemote`1)<SZARRAY STRING>.
        var classToRemoteSignature = ImmutableArray.Create<byte>(0x15, 0x12, 0x05, 0x01, 0x1D, 0x0E);
        var classToRemote = StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000003,
            implementingClass,
            0x1B000003,
            classToRemoteSignature,
            remoteResolution,
            remoteAncestry);

        var sourceFact = StaticFieldModuleSearchFact.Exact(
            module,
            content,
            typeDefinitionsExamined: 7,
            fieldDefinitionsExamined: 0,
            typeReferenceRowCount: 1,
            typeSpecificationRowCount: 3,
            assemblyReferenceRowCount: 1,
            interfaceImplementationRowCount: 3,
            genericParameterRowCount: 3);
        var contractsFact = StaticFieldModuleSearchFact.Exact(
            contractsModule,
            contractsContent,
            typeDefinitionsExamined: 4,
            fieldDefinitionsExamined: 0,
            genericParameterRowCount: 1);
        var facts = ImmutableArray.Create(sourceFact, contractsFact);
        StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
            leafToRoot,
            facts,
            nameof(leafToRoot));
        StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
            classToLeaf,
            facts,
            nameof(classToLeaf));
        StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
            classToRemote,
            facts,
            nameof(classToRemote));

        Assert.Equal(leafInterface, classToLeaf.ResolvedInterfaceType);
        Assert.Equal(leafInterface, leafToRoot.ImplementingType);
        Assert.Equal(StaticFieldTypeClassification.Interface, leafToRoot.ResolvedInterfaceTypeAncestry.Classification);
        Assert.Equal(0x1B000003, classToRemote.InterfaceTypeMetadataToken);
        Assert.Equal(remoteResolution, classToRemote.InterfaceTypeReferenceResolution);
        Assert.Equal(remoteInterface, classToRemote.ResolvedInterfaceType);
        Assert.True(
            classToRemoteSignature.AsSpan().SequenceEqual(classToRemote.InterfaceTypeSpecificationSignature.AsSpan()),
            $"Expected {Convert.ToHexString(classToRemoteSignature.AsSpan())}; " +
            $"actual {Convert.ToHexString(classToRemote.InterfaceTypeSpecificationSignature.AsSpan())}.");
        var returnedSignature = classToRemote.InterfaceTypeSpecificationSignature;
        ImmutableCollectionsMarshal.AsArray(returnedSignature)![0] = 0xFF;
        Assert.Equal(0x15, classToRemote.InterfaceTypeSpecificationSignature[0]);
        Assert.NotEqual(leafToRoot.Sha256, classToLeaf.Sha256);
        Assert.NotEqual(classToLeaf.Sha256, classToRemote.Sha256);
        Assert.Equal(0x02000007, boxType.TypeDefinitionToken);
        Assert.Equal(1, boxType.GenericArity);

        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000004,
            implementingClass,
            0x1B000004,
            [0x15, 0x11, 0x14, 0x01, 0x08],
            genericHeadTypeReferenceResolution: null,
            leafAncestry));
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000004,
            implementingClass,
            0x1B000004,
            [0x15, 0x12, 0x05, 0x01, 0x08],
            genericHeadTypeReferenceResolution: null,
            remoteAncestry));
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000004,
            implementingClass,
            0x1B000004,
            [0x15, 0x12, 0x06, 0x01, 0x08],
            genericHeadTypeReferenceResolution: null,
            leafAncestry));
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000004,
            implementingClass,
            0x1B000004,
            [0x15, 0x12, 0x80, 0x14, 0x01, 0x08],
            genericHeadTypeReferenceResolution: null,
            leafAncestry));
        Assert.Throws<ArgumentException>(() => StaticFieldInterfaceImplementationRowIdentity.ForTypeSpecification(
            metadataModule,
            0x09000004,
            implementingClass,
            0x1B000004,
            classToLeafSignature.Add(0x00),
            genericHeadTypeReferenceResolution: null,
            leafAncestry));

        var incompleteTypeSpecFact = StaticFieldModuleSearchFact.Exact(
            module,
            content,
            typeDefinitionsExamined: 7,
            fieldDefinitionsExamined: 0,
            typeReferenceRowCount: 1,
            typeSpecificationRowCount: 2,
            assemblyReferenceRowCount: 1,
            interfaceImplementationRowCount: 3,
            genericParameterRowCount: 3);
        Assert.Throws<ArgumentException>(() =>
            StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
                classToRemote,
                [incompleteTypeSpecFact, contractsFact],
                nameof(classToRemote)));
        var incompleteReferencedTypeFact = StaticFieldModuleSearchFact.Exact(
            module,
            content,
            typeDefinitionsExamined: 6,
            fieldDefinitionsExamined: 0,
            typeDefinitionRowCount: 6,
            typeReferenceRowCount: 1,
            typeSpecificationRowCount: 3,
            assemblyReferenceRowCount: 1,
            interfaceImplementationRowCount: 3,
            genericParameterRowCount: 3);
        Assert.Throws<ArgumentException>(() =>
            StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
                classToLeaf,
                [incompleteReferencedTypeFact, contractsFact],
                nameof(classToLeaf)));
    }

    /// <summary>
    /// Audits the entire public W7 syntax/symbol surface and proves no compiler tree/symbol, live ClrMD reader,
    /// metadata reader, stream, lazy value, or lazy enumerable crosses the immutable contract boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Corpus", "StaticFieldExpressionV1")]
    public void Public_contract_surface_contains_only_detached_eager_values()
    {
        var assembly = typeof(StaticFieldExpressionDescriptor).Assembly;
        var contractTypes = assembly.GetExportedTypes()
            .Where(static type => type.Namespace == "Interpreter.Product.DumpQuery" &&
                (type.Name.StartsWith("StaticField", StringComparison.Ordinal) ||
                 type.Name.StartsWith("DumpObject", StringComparison.Ordinal) ||
                 type.Name.StartsWith("DumpStrongHandle", StringComparison.Ordinal) ||
                 type.Name.StartsWith("DumpHostSuppliedObject", StringComparison.Ordinal) ||
                 type.Name.StartsWith("DumpStaticFieldExpression", StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(contractTypes);

        var surfacedTypes = new HashSet<Type>();
        foreach (var contractType in contractTypes)
        {
            foreach (var constructor in contractType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    CollectType(parameter.ParameterType, surfacedTypes);
                }
            }

            foreach (var property in contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                CollectType(property.PropertyType, surfacedTypes);
            }

            foreach (var method in contractType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                CollectType(method.ReturnType, surfacedTypes);
                foreach (var parameter in method.GetParameters())
                {
                    CollectType(parameter.ParameterType, surfacedTypes);
                }
            }
        }

        Assert.DoesNotContain(surfacedTypes, static type =>
            type.Namespace?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true ||
            type == typeof(System.Reflection.Metadata.MetadataReader) ||
            type == typeof(System.Reflection.PortableExecutable.PEReader) ||
            typeof(Stream).IsAssignableFrom(type) ||
            type.FullName is "Microsoft.Diagnostics.Runtime.DataTarget" or
                "Microsoft.Diagnostics.Runtime.ClrRuntime" or
                "Microsoft.Diagnostics.Runtime.ClrType" or
                "Microsoft.Diagnostics.Runtime.ClrStaticField" ||
            type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
                (definition == typeof(IEnumerable<>) ||
                 definition == typeof(IAsyncEnumerable<>) ||
                 definition == typeof(Lazy<>)));
    }

    private static StaticFieldExpressionDescriptor CreateSimpleDescriptor(bool globalQualifier = true)
    {
        var segments = globalQualifier
            ? ImmutableArray.Create(
                StaticFieldAccessSegment.Create("Synthetic", "Synthetic", StaticFieldSegmentSeparatorKind.GlobalAliasQualifier, StaticFieldSegmentAccessKind.Root),
                StaticFieldAccessSegment.Create("Incident", "Incident", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
                StaticFieldAccessSegment.Create("Probe", "Probe", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
                StaticFieldAccessSegment.Create("Root", "Root", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember))
            : ImmutableArray.Create(
                StaticFieldAccessSegment.Create("Probe", "Probe", StaticFieldSegmentSeparatorKind.None, StaticFieldSegmentAccessKind.Root),
                StaticFieldAccessSegment.Create("Root", "Root", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember));
        return StaticFieldExpressionDescriptor.Create(
            globalQualifier ? "global::Synthetic.Incident.Probe.Root" : "Probe.Root",
            globalQualifier,
            segments,
            [StaticFieldCandidateShape.Create(segments.Length - 1, StaticFieldSuffixShape.None)],
            StaticFieldParserCounts.Create(8, 7, 4, segments.Length, 1),
            SyntaxBounds);
    }

    private static ImmutableArray<StaticFieldAccessSegment> CreateComplexSegments() =>
    [
        StaticFieldAccessSegment.Create("Synthetic", "Synthetic", StaticFieldSegmentSeparatorKind.GlobalAliasQualifier, StaticFieldSegmentAccessKind.Root),
        StaticFieldAccessSegment.Create("Incident", "Incident", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
        StaticFieldAccessSegment.Create("@Probe", "Probe", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
        StaticFieldAccessSegment.Create("Root", "Root", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
        StaticFieldAccessSegment.Create("Status", "Status", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
        StaticFieldAccessSegment.Create("Code", "Code", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
    ];

    private static ImmutableArray<StaticFieldCandidateShape> CreateComplexShapes() =>
    [
        StaticFieldCandidateShape.Create(
            4,
            StaticFieldSuffixShape.DirectMember,
            StaticFieldFallbackKind.String,
            stringFallback: "missing"),
        StaticFieldCandidateShape.Create(
            3,
            StaticFieldSuffixShape.FixedDepthMemberChain,
            StaticFieldFallbackKind.String,
            stringFallback: "missing"),
    ];

    private static StaticFieldExpressionDescriptor CreateSameFieldDifferentShapeDescriptor()
    {
        var segments = ImmutableArray.Create(
            StaticFieldAccessSegment.Create("Synthetic", "Synthetic", StaticFieldSegmentSeparatorKind.GlobalAliasQualifier, StaticFieldSegmentAccessKind.Root),
            StaticFieldAccessSegment.Create("Incident", "Incident", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
            StaticFieldAccessSegment.Create("Probe", "Probe", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
            StaticFieldAccessSegment.Create("Root", "Root", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
            StaticFieldAccessSegment.Create("Root", "Root", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember));
        return StaticFieldExpressionDescriptor.Create(
            "global::Synthetic.Incident.Probe.Root.Root",
            true,
            segments,
            [
                StaticFieldCandidateShape.Create(3, StaticFieldSuffixShape.DirectMember),
                StaticFieldCandidateShape.Create(4, StaticFieldSuffixShape.None),
            ],
            StaticFieldParserCounts.Create(10, 9, 5, 5, 2),
            SyntaxBounds);
    }

    private static ImmutableArray<StaticFieldAccessSegment> CreateTwoSegments() =>
    [
        StaticFieldAccessSegment.Create("Type", "Type", StaticFieldSegmentSeparatorKind.None, StaticFieldSegmentAccessKind.Root),
        StaticFieldAccessSegment.Create("Field", "Field", StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember),
    ];

    private static StaticFieldNameExpansion CreateExpansion(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansionKind kind) =>
        StaticFieldNameExpansion.Create(shape, kind, "Synthetic.Incident", "Probe", "Root");

    private static StaticFieldModuleInstanceIdentity CreateModule(
        string snapshotSha256,
        ulong appDomainAddress,
        ulong moduleAddress) =>
        StaticFieldModuleInstanceIdentity.Create(
            snapshotSha256,
            sizeof(ulong),
            appDomainAddress,
            moduleAddress,
            imageBase: 0x400000 + moduleAddress,
            imageSize: 0x18000);

    private static ClrmdRuntimeModuleIdentity RuntimeModule(
        ClrmdSnapshotIdentity snapshot,
        StaticFieldModuleInstanceIdentity module) =>
        new(
            snapshot,
            module.ApplicationDomainAddress,
            module.ModuleAddress,
            module.ImageBase,
            module.ImageSize);

    private static ClrmdStaticRuntimeTypeIdentity RuntimeType(
        ClrmdSnapshotIdentity snapshot,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity content,
        StaticFieldTypeDefinitionIdentity type,
        string fullName,
        ulong methodTable,
        bool isValueType,
        bool isPrimitive,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments = default) =>
        ClrmdStaticRuntimeTypeIdentity.Create(
            snapshot,
            sizeof(ulong),
            runtimeModule,
            content,
            type.TypeDefinitionToken,
            fullName,
            methodTable,
            isValueType,
            isPrimitive,
            isArray: false,
            isInterface: false,
            genericArguments.IsDefault
                ? ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty
                : genericArguments);

    private static ModuleContentIdentity CreateModuleContent(char digestCharacter) =>
        ModuleContentIdentity.FromDigest(
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            metadataLength: 24_576,
            new string(digestCharacter, 64));

    private static StaticFieldSymbolDeclarationIdentity CreateInt32Declaration(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        int typeDefinitionToken = 0x02000002,
        int fieldDefinitionToken = 0x04000003,
        string namespaceName = "Synthetic.Incident",
        string typeName = "Probe",
        string fieldName = "Root")
    {
        var declaringAncestry = CreateReferenceClassAncestry(
                module,
                content,
                typeDefinitionToken,
                namespaceName,
                typeName,
                (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit),
                fieldListRowId: fieldDefinitionToken & 0x00FF_FFFF,
                fieldListEndExclusiveRowId: (fieldDefinitionToken & 0x00FF_FFFF) + 1);
        var field = CreateFieldDefinition(
            declaringAncestry.SubjectType,
            fieldName,
            fieldDefinitionToken,
            (int)(FieldAttributes.Public | FieldAttributes.Static),
            [0x06, 0x08]);
        return StaticFieldSymbolDeclarationIdentity.Create(
            declaringAncestry,
            field,
            StaticFieldDeclaredValueKind.Int32,
            systemInt32TypeAncestry: CreateValueTypeAncestry(
                module,
                content,
                0x02000004,
                "System",
                "Int32"));
    }

    private static StaticFieldSymbolDeclarationIdentity CreateConcreteDeclaration(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content)
    {
        var targetAncestry = CreateReferenceClassAncestry(
                module,
                content,
                0x02000006,
                "Synthetic.Incident",
                "RequestEnvelope",
                (int)(TypeAttributes.Public | TypeAttributes.Class));
        var target = StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeDefinition(
            targetAncestry.SubjectType.MetadataModule,
            targetAncestry);
        var declaringAncestry = CreateReferenceClassAncestry(module, content);
        return StaticFieldSymbolDeclarationIdentity.Create(
            declaringAncestry,
            CreateFieldDefinition(
                declaringAncestry.SubjectType,
                "Root",
                0x04000003,
                (int)(FieldAttributes.Public | FieldAttributes.Static),
                [0x06, 0x12, 0x18]),
            StaticFieldDeclaredValueKind.ManagedReference,
            target);
    }

    private static StaticFieldTypeAncestryIdentity CreateReferenceClassAncestry(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        int typeDefinitionToken = 0x02000002,
        string namespaceName = "Synthetic.Incident",
        string typeName = "Probe",
        int typeAttributes = (int)(TypeAttributes.Public | TypeAttributes.Class),
        int genericArity = 0,
        int objectTypeDefinitionToken = 0x02000001,
        int fieldListRowId = 3,
        int fieldListEndExclusiveRowId = 4)
    {
        var metadataModule = CreateMetadataModule(module, content);
        var coreLibrary = CreateCoreLibrary(metadataModule, objectTypeDefinitionToken);
        var objectType = coreLibrary.SystemObjectType;
        if (typeDefinitionToken == objectTypeDefinitionToken &&
            string.Equals(namespaceName, "System", StringComparison.Ordinal) &&
            string.Equals(typeName, "Object", StringComparison.Ordinal))
        {
            return StaticFieldTypeAncestryIdentity.Create(
                objectType,
                ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                coreLibrary);
        }

        var subject = CreateTypeDefinition(
            metadataModule,
            typeDefinitionToken,
            namespaceName,
            typeName,
            typeAttributes,
            genericArity,
            extendsMetadataToken: objectTypeDefinitionToken,
            fieldListRowId: fieldListRowId,
            fieldListEndExclusiveRowId: fieldListEndExclusiveRowId);
        return StaticFieldTypeAncestryIdentity.Create(
            subject,
            [StaticFieldTypeAncestryEdge.Create(subject, objectType)],
            coreLibrary);
    }

    private static StaticFieldTypeAncestryIdentity CreateValueTypeAncestry(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        int typeDefinitionToken,
        string namespaceName,
        string typeName,
        int genericArity = 0,
        int valueTypeDefinitionToken = 0x02000002,
        int fieldListRowId = 1,
        int fieldListEndExclusiveRowId = 1)
    {
        var metadataModule = CreateMetadataModule(module, content);
        var coreLibrary = CreateCoreLibrary(metadataModule, objectTypeDefinitionToken: 0x02000001);
        var valueType = coreLibrary.SystemValueType;
        if (valueType.TypeDefinitionToken != valueTypeDefinitionToken)
        {
            throw new ArgumentException("The synthetic ValueType token must match the core-library fixture.", nameof(valueTypeDefinitionToken));
        }
        var subject = CreateTypeDefinition(
            metadataModule,
            typeDefinitionToken,
            namespaceName,
            typeName,
            (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed),
            genericArity,
            extendsMetadataToken: valueTypeDefinitionToken,
            fieldListRowId: fieldListRowId,
            fieldListEndExclusiveRowId: fieldListEndExclusiveRowId);
        return StaticFieldTypeAncestryIdentity.Create(
            subject,
            [StaticFieldTypeAncestryEdge.Create(subject, valueType)],
            coreLibrary);
    }

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        string assemblyName = "System.Private.CoreLib")
    {
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            $"synthetic-{module.ModuleAddress:x}.dll",
            content.Mvid,
            Guid.Empty,
            Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            assemblyName,
            8,
            0,
            0,
            0,
            string.Empty,
            flags: 0,
            hashAlgorithm: 0x8004,
            ImmutableArray<byte>.Empty);
        var assembly = StaticFieldContainingAssemblyIdentity.Create(
            module,
            content,
            moduleDefinition,
            assemblyDefinition);
        return StaticFieldMetadataModuleIdentity.ForManifestModule(
            module,
            content,
            moduleDefinition,
            assembly);
    }

    private static StaticFieldCoreLibraryIdentity CreateCoreLibrary(
        StaticFieldMetadataModuleIdentity metadataModule,
        int objectTypeDefinitionToken = 0x02000001)
    {
        var objectType = CreateTypeDefinition(
            metadataModule,
            objectTypeDefinitionToken,
            "System",
            "Object",
            (int)(TypeAttributes.Public | TypeAttributes.Class),
            genericArity: 0,
            extendsMetadataToken: null);
        var valueType = CreateTypeDefinition(
            metadataModule,
            0x02000002,
            "System",
            "ValueType",
            (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
            genericArity: 0,
            extendsMetadataToken: objectTypeDefinitionToken);
        var enumType = CreateTypeDefinition(
            metadataModule,
            0x02000003,
            "System",
            "Enum",
            (int)(TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract),
            genericArity: 0,
            extendsMetadataToken: valueType.TypeDefinitionToken);
        var valueTypeEdge = StaticFieldTypeAncestryEdge.Create(valueType, objectType);
        var enumEdge = StaticFieldTypeAncestryEdge.Create(enumType, valueType);
        var selection = StaticFieldCoreLibrarySelectionIdentity.Create(
            StaticFieldCoreLibrarySelectionProvenance.ClrMdRuntimeBaseClassLibrary,
            runtimeOrdinal: 0,
            metadataModule);
        return StaticFieldCoreLibraryIdentity.Create(
            selection,
            metadataModule,
            objectType,
            valueType,
            enumType,
            valueTypeEdge,
            enumEdge);
    }

    private static StaticFieldTypeDefinitionIdentity CreateTypeDefinition(
        StaticFieldMetadataModuleIdentity metadataModule,
        int typeDefinitionToken,
        string namespaceName,
        string typeName,
        int typeAttributes,
        int genericArity,
        int? extendsMetadataToken,
        StaticFieldTypeDefinitionIdentity? enclosingType = null,
        int fieldListRowId = 1,
        int fieldListEndExclusiveRowId = 1,
        int methodListRowId = 1,
        int methodListEndExclusiveRowId = 1) =>
        StaticFieldTypeDefinitionIdentity.Create(
            metadataModule,
            typeDefinitionToken,
            fieldListRowId,
            fieldListEndExclusiveRowId,
            methodListRowId,
            methodListEndExclusiveRowId,
            namespaceName,
            typeName,
            typeAttributes,
            genericParameterCount: genericArity,
            introducedGenericArity: genericArity - (enclosingType?.GenericParameterCount ?? 0),
            extendsMetadataToken,
            enclosingType);

    private static StaticFieldDefinitionIdentity CreateFieldDefinition(
        StaticFieldTypeDefinitionIdentity declaringType,
        string name,
        int fieldDefinitionToken,
        int attributes,
        ImmutableArray<byte> signature,
        ImmutableArray<StaticFieldCustomAttributeRowIdentity> customAttributes = default)
    {
        var rows = customAttributes.IsDefault
            ? ImmutableArray<StaticFieldCustomAttributeRowIdentity>.Empty
            : customAttributes;
        var projection = StaticFieldFieldCustomAttributeProjection.Create(
            fieldDefinitionToken,
            rows.Length,
            rows.Length,
            rows);
        return StaticFieldDefinitionIdentity.Create(
            declaringType,
            fieldDefinitionToken,
            name,
            attributes,
            signature,
            projection);
    }

    private static StaticFieldRejectedDeclarationEvidence CreateRejectedEvidence(
        StaticFieldCandidateShape shape,
        StaticFieldNameExpansion expansion,
        StaticFieldModuleInstanceIdentity module,
        ModuleContentIdentity content,
        StaticFieldBindingIssue issue,
        string code,
        int fieldAttributes)
    {
        var declaringType = CreateReferenceClassAncestry(module, content).SubjectType;
        var field = CreateFieldDefinition(
            declaringType,
            "Root",
            0x04000003,
            fieldAttributes,
            issue == StaticFieldBindingIssue.MetadataInvalid ? [0xFF] : [0x06, 0x08]);
        return StaticFieldRejectedDeclarationEvidence.Field(
            shape,
            expansion,
            field,
            projectedValueKind: issue == StaticFieldBindingIssue.MetadataInvalid
                ? null
                : StaticFieldDeclaredValueKind.Int32,
            issue,
            code);
    }

    private static DumpConsultedBindingContextIdentity FullyQualifiedContext(string snapshotSha256) =>
        DumpConsultedBindingContextIdentity.ForFullyQualified(new ClrmdSnapshotIdentity(snapshotSha256));

    private static DumpConsultedBindingContextIdentity CreateCurrentNamespaceContext(bool exactFrame)
    {
        var snapshot = new ClrmdSnapshotIdentity(SnapshotDigest);
        var selector = DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 2, frameOrdinal: 4);
        DumpSelectedFrameObservation frameObservation;
        if (exactFrame)
        {
            var runtimeModule = new ClrmdRuntimeModuleIdentity(
                snapshot,
                AppDomainAddress: 0x1000,
                ModuleAddress: 0x2000,
                ImageBase: 0x402000,
                ImageSize: 0x18000);
            var frame = DumpSelectedFrameIdentity.Create(
                selector,
                managedThreadId: 37,
                runtimeThreadAddress: 0x7000,
                stackPointer: 0x7FFF0000,
                runtimeModule,
                CreateModuleContent('c'),
                methodDefinitionToken: 0x06000003,
                declaringTypeDefinitionToken: 0x02000002,
                declaringNamespace: "Synthetic.Incident",
                DumpInstructionLocation.Create(0x401234, ilOffset: 10));
            frameObservation = DumpSelectedFrameObservation.Exact(frame, BindingBounds);
        }
        else
        {
            frameObservation = DumpSelectedFrameObservation.Unavailable(
                selector,
                DumpContextEvidenceIssue.FrameUnavailable,
                BindingBounds);
        }

        var pdb = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(snapshot),
            exactFrame
                ? DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable
                : DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var acquired = DumpExpressionBindingContext.Acquire(snapshot, frameObservation, pdb);
        return DumpConsultedBindingContextIdentity.FromAcquiredContext(
            acquired,
            currentNamespaceConsulted: true,
            importsConsulted: false,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
    }

    private static DumpConsultedBindingContextIdentity CreateNonExactCurrentNamespaceContext(
        DumpContextEvidenceStatus status)
    {
        var snapshot = new ClrmdSnapshotIdentity(SnapshotDigest);
        var selector = DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 2, frameOrdinal: 4);
        var frame = status switch
        {
            DumpContextEvidenceStatus.Ambiguous => DumpSelectedFrameObservation.Ambiguous(
                selector,
                DumpContextEvidenceIssue.FrameAmbiguous,
                BindingBounds),
            DumpContextEvidenceStatus.Unsupported => DumpSelectedFrameObservation.Unsupported(
                selector,
                DumpContextEvidenceIssue.UnsupportedFrame,
                BindingBounds),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var pdb = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(snapshot),
            DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var acquired = DumpExpressionBindingContext.Acquire(snapshot, frame, pdb);
        return DumpConsultedBindingContextIdentity.FromAcquiredContext(
            acquired,
            currentNamespaceConsulted: true,
            importsConsulted: false,
            ImmutableArray<DumpPortablePdbImportFact>.Empty);
    }

    private static (
        DumpConsultedBindingContextIdentity Context,
        DumpPortablePdbImportFact Import,
        StaticFieldModuleInstanceIdentity SourceModule,
        ModuleContentIdentity SourceContent) CreateAssemblyNamespaceImportContext()
    {
        var snapshot = new ClrmdSnapshotIdentity(SnapshotDigest);
        var sourceContent = CreateModuleContent('c');
        var runtimeModule = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x1000,
            ModuleAddress: 0x2000,
            ImageBase: 0x402000,
            ImageSize: 0x18000);
        var selector = DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 2, frameOrdinal: 4);
        var frame = DumpSelectedFrameIdentity.Create(
            selector,
            managedThreadId: 37,
            runtimeThreadAddress: 0x7000,
            stackPointer: 0x7FFF0000,
            runtimeModule,
            sourceContent,
            methodDefinitionToken: 0x06000003,
            declaringTypeDefinitionToken: 0x02000002,
            declaringNamespace: "Synthetic.Client",
            DumpInstructionLocation.Create(0x401234, ilOffset: 10));
        var frameObservation = DumpSelectedFrameObservation.Exact(frame, BindingBounds);
        var debugIdentity = DumpPortablePdbDebugIdentity.Create(
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            stamp: 0x5A17C0DE);
        var moduleDebugIdentity = DumpModulePortablePdbDebugIdentity.Create(
            runtimeModule,
            sourceContent,
            debugIdentity);
        var artifact = DumpPortablePdbArtifactIdentity.Create(
            DumpPortablePdbContentIdentity.Create(31_744, new string('9', 64)),
            debugIdentity);
        var document = DumpPortablePdbDocumentIdentity.Create(
            0x30000001,
            Guid.Parse("3f5162f8-07c6-11d3-9053-00c04fa302a1"),
            Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16"),
            [0x01, 0x23, 0x45, 0x67]);
        var import = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001,
            ordinal: 0,
            rawKind: 2,
            "Synthetic.Incident",
            [0x02, 0x10],
            assemblyReferenceToken: 0x23000001);
        var importScope = DumpPortablePdbImportScopeIdentity.Create(
            0x35000001,
            parentImportScopeToken: null,
            nestingDepth: 0,
            [import]);
        var localScope = DumpPortablePdbLocalScopeIdentity.Create(
            0x32000001,
            0x06000003,
            0x35000001,
            startOffset: 0,
            length: 100,
            nestingDepth: 0);
        var facts = DumpPortablePdbContextFacts.Acquire(
            frame,
            moduleDebugIdentity,
            artifact,
            methodDebugInformationToken: 0x31000003,
            document,
            [localScope],
            [importScope]);
        var acquired = DumpExpressionBindingContext.Acquire(
            snapshot,
            frameObservation,
            DumpPortablePdbObservation.Exact(facts, BindingBounds));
        var context = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            acquired,
            currentNamespaceConsulted: false,
            importsConsulted: true,
            [import]);
        return (
            context,
            import,
            CreateModule(SnapshotDigest, 0x1000, 0x2000),
            sourceContent);
    }

    private static void CollectType(Type type, ISet<Type> result)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            CollectType(type.GetElementType()!, result);
            return;
        }

        if (!result.Add(type) || !type.IsGenericType)
        {
            return;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            CollectType(argument, result);
        }
    }
}
