using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the additive W8 primitive, outcome-axis, and structured syntax contract foundation.</summary>
public sealed class W8V2ContractFoundationTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Proves decoded identifiers cover Roslyn keyword/escape cases without a production token reparse.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Decoded_identifier_contract_matches_pinned_Roslyn_identifier_values()
    {
        Assert.Equal("class", DumpExpressionIdentifier.Create("class").DecodedText);
        Assert.Equal("var", DumpExpressionIdentifier.Create("var").DecodedText);
        Assert.Equal("global", DumpExpressionIdentifier.Create("global").DecodedText);
        Assert.Equal("λ", DumpExpressionIdentifier.Create("λ").DecodedText);

        Assert.Throws<ArgumentException>(() => DumpExpressionIdentifier.Create(string.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionIdentifier.Create("foo.bar"));
        Assert.Throws<ArgumentException>(() => DumpExpressionIdentifier.Create(" foo"));
        Assert.Throws<ArgumentException>(() => DumpExpressionIdentifier.Create(new string('x', 65)));

        var escapedKeyword = Assert.IsType<IdentifierNameSyntax>(SyntaxFactory.ParseName("@class"));
        var unicodeEscape = Assert.IsType<IdentifierNameSyntax>(SyntaxFactory.ParseName(@"\u0066oo"));
        Assert.Equal("class", escapedKeyword.Identifier.ValueText);
        Assert.Equal("foo", unicodeEscape.Identifier.ValueText);
        var global = Assert.IsType<AliasQualifiedNameSyntax>(
            SyntaxFactory.ParseName("global::Demo"));
        var escaped = Assert.IsType<AliasQualifiedNameSyntax>(
            SyntaxFactory.ParseName("@global::Demo"));
        Assert.Equal("global", global.Alias.Identifier.ValueText);
        Assert.Equal("global", escaped.Alias.Identifier.ValueText);
        Assert.Equal("global", global.Alias.Identifier.Text);
        Assert.Equal("@global", escaped.Alias.Identifier.Text);

        Assert.Equal(StaticFieldV2AliasKind.Global, StaticFieldV2AliasQualifier.Global().Kind);
        Assert.Equal(
            StaticFieldV2AliasKind.Named,
            StaticFieldV2AliasQualifier.Named(DumpExpressionIdentifier.Create(escaped.Alias.Identifier.ValueText)).Kind);
        Assert.Single(typeof(DumpExpressionIdentifier).GetMethods()
            .Where(static method => method.Name == nameof(DumpExpressionIdentifier.Create))
            .Single()
            .GetParameters());
    }

    /// <summary>Proves named diagnostic arguments are normalized, unique, staged, and independent of caller order.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Diagnostics_are_stage_addressed_named_sets_with_canonical_order()
    {
        var alpha = DumpExpressionDiagnosticArgument.Signed("alpha", -7);
        var omega = DumpExpressionDiagnosticArgument.Text("omega", "owner");
        var first = DumpExpressionDiagnostic.Create(
            "W8_BIND_STOP",
            DumpExpressionDiagnosticStage.TypeBinding,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray.Create(omega, alpha));
        var replay = DumpExpressionDiagnostic.Create(
            "W8_BIND_STOP",
            DumpExpressionDiagnosticStage.TypeBinding,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray.Create(alpha, omega));

        Assert.Equal(new[] { "alpha", "omega" }, first.Arguments.Select(static argument => argument.Name));
        Assert.Equal(first, replay);
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.NotEqual(
            first,
            DumpExpressionDiagnostic.Create(
                "W8_BIND_STOP",
                DumpExpressionDiagnosticStage.MemberLookup,
                DumpExpressionDiagnosticSeverity.Error,
                ImmutableArray.Create(alpha, omega)));
        Assert.Throws<ArgumentException>(() => DumpExpressionDiagnostic.Create(
            "W8_DUPLICATE",
            DumpExpressionDiagnosticStage.TypeBinding,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray.Create(alpha, DumpExpressionDiagnosticArgument.Text("alpha", "other"))));
        Assert.Throws<ArgumentException>(() => DumpExpressionDiagnosticArgument.Text("Alpha", "bad-name"));
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpExpressionDiagnostic.Create(
            "W8_STAGE",
            (DumpExpressionDiagnosticStage)999,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty));
    }

    /// <summary>Proves capability records retain calls, source identity, bounds, and strict omission invariants.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Capability_ledger_retains_physical_accounting_and_rejects_contradictions()
    {
        var reached = ImmutableArray.Create(StaticFieldV2Limits.AllDeclaredBounds[0]);
        var exact = DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.Exact,
            callCount: 3,
            Digest.ToUpperInvariant(),
            reached,
            ImmutableArray<DumpExpressionDiagnostic>.Empty);
        Assert.Equal(3, exact.CallCount);
        Assert.Equal(Digest, exact.ConsultedSourceSha256);
        Assert.Equal(reached[0], Assert.Single(exact.ReachedBounds));

        var nonExact = DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.NonExact,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray.Create(Error("W8_METADATA_PARTIAL", DumpExpressionDiagnosticStage.TypeBinding)));
        Assert.Equal(DumpExpressionCapabilityDisposition.NonExact, nonExact.Disposition);

        var information = DumpExpressionDiagnostic.Create(
            "W8_METADATA_NOTE",
            DumpExpressionDiagnosticStage.TypeBinding,
            DumpExpressionDiagnosticSeverity.Information,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);
        var warning = DumpExpressionDiagnostic.Create(
            "W8_METADATA_WARNING",
            DumpExpressionDiagnosticStage.TypeBinding,
            DumpExpressionDiagnosticSeverity.Warning,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);
        _ = DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.Exact,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray.Create(information, warning));

        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.NotRequired,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.NotReached,
            callCount: 0,
            null,
            reached,
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.NonExact,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.NonExact,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray.Create(information)));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.Exact,
            callCount: 1,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray.Create(Error("W8_EXACT_ERROR", DumpExpressionDiagnosticStage.TypeBinding))));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityDisposition.Exact,
            callCount: 2,
            Digest,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityDisposition.NotRequired,
            callCount: 0,
            null,
            ImmutableArray<EvaluationDeterministicBound>.Empty,
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionCapabilityUse.Create(
            DumpExpressionCapabilityKind.Metadata,
            DumpExpressionCapabilityDisposition.Exact,
            callCount: 1,
            Digest,
            ImmutableArray.Create(new EvaluationDeterministicBound("caller.extra", 1)),
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
    }

    /// <summary>Proves route/profile ledgers encode context-independent omissions and route-specific producers.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Provenance_profile_and_route_rows_have_exact_capability_minima()
    {
        foreach (var route in new[]
                 {
                     DumpExpressionRouteKind.FullyQualified,
                     DumpExpressionRouteKind.OrdinaryQualified,
                 })
        {
            var provenance = Provenance(
                DumpExpressionProfileKind.StaticFieldExpressionV2,
                route,
                DumpExpressionCapabilityKind.Parse,
                DumpExpressionCapabilityKind.Metadata);
            Assert.Equal(DumpExpressionCapabilityDisposition.NotRequired,
                Use(provenance, DumpExpressionCapabilityKind.SelectedFrame).Disposition);
            Assert.Equal(DumpExpressionCapabilityDisposition.NotRequired,
                Use(provenance, DumpExpressionCapabilityKind.PortablePdb).Disposition);
            Assert.Equal(DumpExpressionCapabilityDisposition.NotRequired,
                Use(provenance, DumpExpressionCapabilityKind.SelectedMethodLexical).Disposition);
        }

        _ = Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.SelectedMethodLexical,
            DumpExpressionCapabilityKind.Metadata);
        _ = Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.AliasQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.PortablePdb,
            DumpExpressionCapabilityKind.Metadata);
        _ = Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.BareStaticMember,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.PortablePdb,
            DumpExpressionCapabilityKind.SelectedMethodLexical,
            DumpExpressionCapabilityKind.Metadata);
        _ = Provenance(
            DumpExpressionProfileKind.FrameValueExpressionV1,
            DumpExpressionRouteKind.FrameValue,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.PortablePdb,
            DumpExpressionCapabilityKind.SelectedMethodLexical);

        Assert.Throws<ArgumentException>(() => Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.FullyQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.Metadata));
        _ = Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.Metadata);
        Assert.Throws<ArgumentException>(() => Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.AliasQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.Metadata));
        Assert.Throws<ArgumentException>(() => Provenance(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.FrameValue,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.SelectedFrame,
            DumpExpressionCapabilityKind.PortablePdb,
            DumpExpressionCapabilityKind.SelectedMethodLexical));
        Assert.Throws<ArgumentException>(() => Provenance(
            DumpExpressionProfileKind.FrameValueExpressionV1,
            DumpExpressionRouteKind.FullyQualified,
            DumpExpressionCapabilityKind.Parse,
            DumpExpressionCapabilityKind.Metadata));
    }

    /// <summary>Proves route-required context capabilities admit typed stops without fabricated later calls.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Provenance_context_routes_enforce_ordered_early_stop_ledgers()
    {
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.AliasQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotReached));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.AliasQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NonExact));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.BareStaticMember,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotReached),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotReached));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.FrameValueExpressionV1,
            DumpExpressionRouteKind.FrameValue,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotReached));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotReached),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotReached));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotRequired),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NonExact));
        _ = ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotReached));

        Assert.Throws<ArgumentException>(() => ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.AliasQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.NotRequired),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.Exact)));
        Assert.Throws<ArgumentException>(() => ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.BareStaticMember,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotRequired),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.Exact)));
        Assert.Throws<ArgumentException>(() => ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.BareStaticMember,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotRequired),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotRequired)));
        Assert.Throws<ArgumentException>(() => ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NotRequired),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotReached)));
        Assert.Throws<ArgumentException>(() => ProvenanceWithDispositions(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            DumpExpressionRouteKind.OrdinaryQualified,
            (DumpExpressionCapabilityKind.Parse, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.SelectedFrame, DumpExpressionCapabilityDisposition.Exact),
            (DumpExpressionCapabilityKind.PortablePdb, DumpExpressionCapabilityDisposition.NonExact),
            (DumpExpressionCapabilityKind.SelectedMethodLexical, DumpExpressionCapabilityDisposition.NotRequired)));
    }

    /// <summary>Proves syntax stops retain an unreached route and never fabricate context-free omissions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Syntax_stops_require_NotReached_route_and_post_parse_capabilities()
    {
        var invalidParse = UnreachedProvenance(DumpExpressionCapabilityDisposition.NonExact);
        var unsupportedTree = UnreachedProvenance(DumpExpressionCapabilityDisposition.Exact);
        var preparseExpressionCap = UnreachedProvenance(DumpExpressionCapabilityDisposition.NotReached);
        Assert.Equal(DumpExpressionRouteKind.NotReached, invalidParse.Route);
        Assert.Equal(DumpExpressionCapabilityDisposition.NonExact,
            Use(invalidParse, DumpExpressionCapabilityKind.Parse).Disposition);
        Assert.Equal(DumpExpressionCapabilityDisposition.Exact,
            Use(unsupportedTree, DumpExpressionCapabilityKind.Parse).Disposition);
        Assert.Equal(DumpExpressionCapabilityDisposition.NotReached,
            Use(preparseExpressionCap, DumpExpressionCapabilityKind.Parse).Disposition);
        Assert.All(invalidParse.CapabilityUses.Where(static use => use.Capability != DumpExpressionCapabilityKind.Parse),
            static use => Assert.Equal(DumpExpressionCapabilityDisposition.NotReached, use.Disposition));

        Assert.Throws<ArgumentException>(() => DumpExpressionV2ProvenanceIdentity.Create(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Digest,
            DumpExpressionRouteKind.NotReached,
            Enum.GetValues<DumpExpressionCapabilityKind>().Select(static kind =>
                DumpExpressionCapabilityUse.Create(
                    kind,
                    DumpExpressionCapabilityDisposition.NotRequired,
                    0,
                    null,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty)).ToImmutableArray(),
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
        Assert.Throws<ArgumentException>(() => DumpExpressionV2ProvenanceIdentity.Create(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Digest,
            DumpExpressionRouteKind.FullyQualified,
            UnreachedCapabilityLedger(DumpExpressionCapabilityDisposition.NonExact),
            ImmutableArray<DumpExpressionDiagnostic>.Empty));
    }

    /// <summary>Proves syntax is the initial independent axis and exact values may still yield blocked suffixes.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Outcome_axes_preserve_initial_syntax_stops_and_exact_value_suffix_blocks()
    {
        foreach (var syntax in new[] { DumpExpressionSyntaxStatus.Invalid, DumpExpressionSyntaxStatus.Unsupported })
        {
            var stopped = SyntaxStoppedAxes(syntax);
            Assert.Equal(DumpExpressionContextOutcome.NotReached, stopped.Context);
            Assert.Equal(DumpExpressionCompletenessOutcome.NoAnswer, stopped.Completeness);
        }

        var exactNullBlocked = CompletePrefixAxes(
            DumpExpressionValueOutcome.ExactNull,
            DumpExpressionSuffixOutcome.Blocked,
            DumpExpressionCompletenessOutcome.NoAnswer);
        var exactValueIntermediateNullBlocked = CompletePrefixAxes(
            DumpExpressionValueOutcome.ExactValue,
            DumpExpressionSuffixOutcome.Blocked,
            DumpExpressionCompletenessOutcome.NoAnswer);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, exactNullBlocked.Syntax);
        Assert.Equal(DumpExpressionSuffixOutcome.Blocked, exactValueIntermediateNullBlocked.Suffix);

        _ = CompletePrefixAxes(
            DumpExpressionValueOutcome.ExactValue,
            DumpExpressionSuffixOutcome.Completed,
            DumpExpressionCompletenessOutcome.Complete);
        Assert.Throws<ArgumentException>(() => DumpExpressionV2OutcomeAxes.Create(
            DumpExpressionSyntaxStatus.Invalid,
            DumpExpressionContextOutcome.NotRequired,
            DumpExpressionRootAttributionOutcome.NotReached,
            DumpExpressionLexicalCompletenessOutcome.NotReached,
            DumpExpressionTypeBindingOutcome.NotReached,
            DumpExpressionTypeConstructionOutcome.NotReached,
            DumpExpressionMemberLookupOutcome.NotReached,
            DumpExpressionRuntimeConstructionOutcome.NotReached,
            DumpExpressionStorageOutcome.NotReached,
            DumpExpressionValueOutcome.NotReached,
            DumpExpressionSuffixOutcome.NotReached,
            DumpExpressionCompletenessOutcome.NoAnswer));
        Assert.Throws<ArgumentException>(() => DumpExpressionV2OutcomeAxes.Create(
            DumpExpressionSyntaxStatus.Admitted,
            DumpExpressionContextOutcome.NotReached,
            DumpExpressionRootAttributionOutcome.NotRequired,
            DumpExpressionLexicalCompletenessOutcome.NotRequired,
            DumpExpressionTypeBindingOutcome.NotRequired,
            DumpExpressionTypeConstructionOutcome.NotRequired,
            DumpExpressionMemberLookupOutcome.NotRequired,
            DumpExpressionRuntimeConstructionOutcome.NotRequired,
            DumpExpressionStorageOutcome.NotRequired,
            DumpExpressionValueOutcome.ExactValue,
            DumpExpressionSuffixOutcome.NotRequested,
            DumpExpressionCompletenessOutcome.Complete));
    }

    /// <summary>Proves profile limit catalogs have exact canonical membership, ordering, and independent caps.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frozen_limit_catalogs_have_exact_profile_membership_and_no_duplicates()
    {
        var shared = ExpressionV2ContractLimits.AllDeclaredBounds;
        Assert.Equal(shared.OrderBy(static bound => bound.Name, StringComparer.Ordinal).Select(static bound => bound.Name),
            shared.Select(static bound => bound.Name));
        Assert.Equal(shared.Length, shared.Select(static bound => bound.Name).Distinct(StringComparer.Ordinal).Count());

        var expectedStatic = shared.Where(static bound => bound.Name is not
            (ExpressionV2ContractLimits.FrameCountPerThreadBoundName or
             ExpressionV2ContractLimits.FrameStringCharacterCountBoundName or
             ExpressionV2ContractLimits.FrameValueByteCountBoundName));
        Assert.Equal(expectedStatic.Select(static bound => bound.Name),
            StaticFieldV2Limits.AllDeclaredBounds.Select(static bound => bound.Name));

        var expectedFrameNames = new[]
        {
            ExpressionV2ContractLimits.ArrayRankBoundName,
            ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName,
            ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName,
            ExpressionV2ContractLimits.DeclaredEventCountBoundName,
            ExpressionV2ContractLimits.DeclaredMethodCountBoundName,
            ExpressionV2ContractLimits.DeclaredNestedTypeCountBoundName,
            ExpressionV2ContractLimits.DeclaredPropertyCountBoundName,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName,
            ExpressionV2ContractLimits.FieldDefinitionRowCountBoundName,
            ExpressionV2ContractLimits.FieldPointerRowCountBoundName,
            ExpressionV2ContractLimits.FrameCountPerThreadBoundName,
            ExpressionV2ContractLimits.FrameStringCharacterCountBoundName,
            ExpressionV2ContractLimits.FrameThreadCountBoundName,
            ExpressionV2ContractLimits.FrameValueByteCountBoundName,
            ExpressionV2ContractLimits.GenericParameterConstraintRowCountBoundName,
            ExpressionV2ContractLimits.GenericParameterRowCountBoundName,
            ExpressionV2ContractLimits.IdentifierCharacterCountBoundName,
            ExpressionV2ContractLimits.InterfaceImplementationRowCountBoundName,
            ExpressionV2ContractLimits.LexicalBlockerCountBoundName,
            ExpressionV2ContractLimits.LocalConstantCountBoundName,
            ExpressionV2ContractLimits.LocalCountBoundName,
            ExpressionV2ContractLimits.LocalFunctionAssociationCountBoundName,
            ExpressionV2ContractLimits.MethodDefinitionRowCountBoundName,
            ExpressionV2ContractLimits.MethodPointerRowCountBoundName,
            ExpressionV2ContractLimits.NestedClassRowCountBoundName,
            ExpressionV2ContractLimits.ParameterCountBoundName,
            ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName,
            ExpressionV2ContractLimits.SignatureTokenResolutionCountBoundName,
            ExpressionV2ContractLimits.ExpressionCharacterCountBoundName,
            ExpressionV2ContractLimits.ExpressionSegmentCountBoundName,
            ExpressionV2ContractLimits.SyntaxDepthBoundName,
            ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName,
            ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
            ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName,
            ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
            ExpressionV2ContractLimits.TypeSpecificationRowTraversalCountBoundName,
            ExpressionV2ContractLimits.TypeDefinitionRowCountBoundName,
        }.OrderBy(static name => name, StringComparer.Ordinal);
        Assert.Equal(expectedFrameNames, FrameValueV1Limits.AllDeclaredBounds.Select(static bound => bound.Name));
        Assert.DoesNotContain(FrameValueV1Limits.AllDeclaredBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.SyntaxPartitionCountBoundName);
        Assert.Equal(63, StaticFieldV2Limits.MaximumSyntaxPartitionCount);
        Assert.Equal(4_096, StaticFieldV2Limits.MaximumNameCandidateOccurrenceCount);
        Assert.Equal(1_024, StaticFieldV2Limits.MaximumDeclaredMethodCount);
        Assert.Equal(256, StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount);
        Assert.Equal(64, StaticFieldV2Limits.MaximumTypeSpecificationRowTraversalCount);
        Assert.Equal(256, StaticFieldV2Limits.MaximumSignatureTokenResolutionCount);
        Assert.Equal(4_096, StaticFieldV2Limits.MaximumInterfaceImplementationRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumTypeDefinitionRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumFieldDefinitionRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumMethodDefinitionRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumNestedClassRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumGenericParameterRowCount);
        Assert.Equal(65_536, StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumSignatureTokenResolutionCount,
            FrameValueV1Limits.MaximumSignatureTokenResolutionCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumInterfaceImplementationRowCount,
            FrameValueV1Limits.MaximumInterfaceImplementationRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumTypeDefinitionRowCount,
            FrameValueV1Limits.MaximumTypeDefinitionRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumFieldDefinitionRowCount,
            FrameValueV1Limits.MaximumFieldDefinitionRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumMethodDefinitionRowCount,
            FrameValueV1Limits.MaximumMethodDefinitionRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumNestedClassRowCount,
            FrameValueV1Limits.MaximumNestedClassRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumGenericParameterRowCount,
            FrameValueV1Limits.MaximumGenericParameterRowCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumGenericParameterConstraintRowCount,
            FrameValueV1Limits.MaximumGenericParameterConstraintRowCount);
        Assert.Equal(32, FrameValueV1Limits.MaximumArrayRank);
    }

    /// <summary>Proves nested generics and arrays preserve segment-local arguments and every structural partition.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complex_alias_qualified_syntax_retains_complete_partition_universe()
    {
        var fixture = ComplexSyntaxFixture();
        var descriptor = StaticFieldV2ExpressionDescriptor.Create(
            fixture.RawExpression,
            fixture.Alias,
            fixture.Segments,
            fixture.Partitions.Reverse().ToImmutableArray(),
            fixture.Counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var replay = StaticFieldV2ExpressionDescriptor.Create(
            fixture.RawExpression,
            StaticFieldV2AliasQualifier.Named(Id("requestlib")),
            fixture.Segments,
            fixture.Partitions,
            fixture.Counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty);

        Assert.Equal(4, descriptor.Partitions.Length);
        Assert.Equal(new[] { 0, 1 }, descriptor.Partitions
            .Select(static partition => partition.PossibleTopLevelTypeSegmentIndex!.Value)
            .Distinct()
            .Order());
        Assert.Equal(new[] { 3, 4 }, descriptor.Partitions
            .Select(static partition => partition.FieldSegmentIndex)
            .Distinct()
            .Order());
        Assert.Equal(1, descriptor.Segments[1].Arity);
        Assert.Equal(1, descriptor.Segments[2].Arity);
        Assert.Equal(2, descriptor.Segments[2].TypeArguments[0].ArrayRank);
        Assert.Equal(descriptor, replay);
        Assert.Equal(descriptor.Sha256, replay.Sha256);

        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionDescriptor.Create(
            fixture.RawExpression,
            fixture.Alias,
            fixture.Segments,
            fixture.Partitions.RemoveAt(0),
            fixture.Counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionDescriptor.Create(
            fixture.RawExpression,
            fixture.Alias,
            fixture.Segments,
            fixture.Partitions.Add(fixture.Partitions[0]),
            StaticFieldV2ParserCounts.Create(40, 30, 8, 6, 4, 4, 6, 2, 10, 0, 5),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
    }

    /// <summary>Proves bare roots are explicit partitions and source array rank-one is rejected.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Bare_member_partition_has_no_sentinel_and_source_array_rank_starts_at_two()
    {
        var current = Segment("Current", StaticFieldV2ExpressionSeparatorKind.None);
        var payload = Segment("Payload", StaticFieldV2ExpressionSeparatorKind.Dot);
        var suffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableArray.Create(DumpExpressionSuffixSegment.Create(Id("Payload"), DumpExpressionSuffixAccessKind.Direct)));
        var partitions = ImmutableArray.Create(
            StaticFieldV2StructuralPartition.Create(StaticFieldV2CandidateKind.BareMember, 0, null, suffix),
            StaticFieldV2StructuralPartition.Create(
                StaticFieldV2CandidateKind.QualifiedOwner,
                1,
                0,
                DumpExpressionSuffixDescriptor.NotRequested()));
        var descriptor = StaticFieldV2ExpressionDescriptor.Create(
            "Current.Payload",
            StaticFieldV2AliasQualifier.None(),
            ImmutableArray.Create(current, payload),
            partitions,
            StaticFieldV2ParserCounts.Create(4, 3, 3, 2, 0, 0, 0, 0, 7, 0, 2),
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var bare = Assert.Single(descriptor.Partitions,
            static partition => partition.CandidateKind == StaticFieldV2CandidateKind.BareMember);
        Assert.Equal(0, bare.FieldSegmentIndex);
        Assert.Null(bare.PossibleTopLevelTypeSegmentIndex);

        var element = StaticFieldV2TypeSyntax.Predefined(StaticFieldV2PredefinedTypeKind.Int32);
        Assert.Equal(2, StaticFieldV2TypeSyntax.MultidimensionalArray(element, 2).ArrayRank);
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticFieldV2TypeSyntax.MultidimensionalArray(element, 1));
    }

    /// <summary>Proves syntax reached-bound sets are exact at caps and all cap-plus-one stops retain no descriptor.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Syntax_bounds_are_exact_sets_and_cap_plus_one_has_no_prefix_descriptor()
    {
        var cappedIdentifier = new string('x', 64);
        var cappedFallback = new string('f', 256);
        var cappedSegments = ImmutableArray.Create(
            Segment(cappedIdentifier, StaticFieldV2ExpressionSeparatorKind.None),
            Segment("Field", StaticFieldV2ExpressionSeparatorKind.Dot),
            Segment("Member", StaticFieldV2ExpressionSeparatorKind.Dot));
        var bareSuffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.FixedDepthMemberChain,
            ImmutableArray.Create(
                DumpExpressionSuffixSegment.Create(Id("Field"), DumpExpressionSuffixAccessKind.Direct),
                DumpExpressionSuffixSegment.Create(Id("Member"), DumpExpressionSuffixAccessKind.Direct)),
            DumpExpressionFallbackKind.String,
            stringFallback: cappedFallback);
        var qualifiedSuffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableArray.Create(DumpExpressionSuffixSegment.Create(Id("Member"), DumpExpressionSuffixAccessKind.Direct)),
            DumpExpressionFallbackKind.String,
            stringFallback: cappedFallback);
        var cappedPartitions = ImmutableArray.Create(
            StaticFieldV2StructuralPartition.Create(StaticFieldV2CandidateKind.BareMember, 0, null, bareSuffix),
            StaticFieldV2StructuralPartition.Create(StaticFieldV2CandidateKind.QualifiedOwner, 1, 0, qualifiedSuffix));
        var cappedCounts = StaticFieldV2ParserCounts.Create(20, 20, 5, 3, 0, 0, 0, 0, 64, 256, 2);
        var cappedRaw = $"{cappedIdentifier}.Field.Member ?? \"{cappedFallback}\"";
        var exactCappedBounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(cappedRaw, cappedCounts);
        Assert.Equal(
            new[]
            {
                ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName,
                ExpressionV2ContractLimits.IdentifierCharacterCountBoundName,
            }.OrderBy(static name => name, StringComparer.Ordinal),
            exactCappedBounds.Select(static bound => bound.Name));
        _ = StaticFieldV2ExpressionDescriptor.Create(
            cappedRaw,
            StaticFieldV2AliasQualifier.None(),
            cappedSegments,
            cappedPartitions,
            cappedCounts,
            exactCappedBounds);
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionDescriptor.Create(
            cappedRaw,
            StaticFieldV2AliasQualifier.None(),
            cappedSegments,
            cappedPartitions,
            cappedCounts,
            exactCappedBounds.RemoveAt(0)));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionDescriptor.Create(
            "Owner.Field",
            StaticFieldV2AliasQualifier.None(),
            ImmutableArray.Create(
                Segment("Owner", StaticFieldV2ExpressionSeparatorKind.None),
                Segment("Field", StaticFieldV2ExpressionSeparatorKind.Dot)),
            ImmutableArray.Create(
                StaticFieldV2StructuralPartition.Create(
                    StaticFieldV2CandidateKind.QualifiedOwner,
                    1,
                    0,
                    DumpExpressionSuffixDescriptor.NotRequested()),
                StaticFieldV2StructuralPartition.Create(
                    StaticFieldV2CandidateKind.BareMember,
                    0,
                    null,
                    DumpExpressionSuffixDescriptor.Create(
                        DumpExpressionSuffixKind.DirectMember,
                        ImmutableArray.Create(DumpExpressionSuffixSegment.Create(
                            Id("Field"),
                            DumpExpressionSuffixAccessKind.Direct))))),
            StaticFieldV2ParserCounts.Create(4, 3, 3, 2, 0, 0, 0, 0, 5, 0, 2),
            ImmutableArray.Create(StaticFieldV2Limits.AllDeclaredBounds[0])));

        var atCapCounts = StaticFieldV2ParserCounts.Create(
            128,
            128,
            64,
            32,
            64,
            16,
            128,
            32,
            64,
            256,
            63);
        var expectedAtCap = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(
            new string('x', 512),
            atCapCounts);
        Assert.Equal(11, expectedAtCap.Length);
        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Unsupported(
            new string('x', 512),
            StaticFieldV2SyntaxIssue.SuffixShapeUnsupported,
            atCapCounts,
            ImmutableArray.Create(Error("W8_TREE", DumpExpressionDiagnosticStage.Projection)),
            expectedAtCap.RemoveAt(0)));
        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Unsupported(
            "x",
            StaticFieldV2SyntaxIssue.TreeShapeUnsupported,
            EmptyCounts(),
            ImmutableArray.Create(Error("W8_TREE", DumpExpressionDiagnosticStage.Projection)),
            ImmutableArray.Create(StaticFieldV2Limits.AllDeclaredBounds[0])));

        foreach (var row in CapPlusOneRows())
        {
            var outcome = row.Invalid
                ? StaticFieldV2SyntaxOutcome.Invalid(
                    row.RawExpression,
                    row.Issue,
                    row.Counts,
                    ImmutableArray.Create(Error("W8_CAP", DumpExpressionDiagnosticStage.Parse)),
                    row.Bounds)
                : StaticFieldV2SyntaxOutcome.Unsupported(
                    row.RawExpression,
                    row.Issue,
                    row.Counts,
                    ImmutableArray.Create(Error("W8_CAP", DumpExpressionDiagnosticStage.Projection)),
                    row.Bounds);
            Assert.Null(outcome.Descriptor);
            Assert.Equal(row.Issue, outcome.Issue);
        }
    }

    /// <summary>Proves the partition cap and first over-cap observation come from the production derivation itself.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Partition_bound_is_reached_by_real_23_and_24_segment_derivations()
    {
        var exactSegments = SimpleSegments(23);
        var exactRaw = string.Join(".", exactSegments.Select(static segment => segment.Identifier.DecodedText));
        var exactPartitions = StaticFieldV2ExpressionDescriptor.DerivePartitionsThroughFirstBound(
            StaticFieldV2AliasQualifier.None(),
            exactSegments,
            DumpExpressionFallbackKind.None,
            null,
            null);
        Assert.Equal(StaticFieldV2Limits.MaximumSyntaxPartitionCount, exactPartitions.Length);
        var exactCounts = CountsForSimpleSegments(exactRaw, exactSegments, exactPartitions.Length);
        var exactBounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(exactRaw, exactCounts);
        Assert.Contains(exactBounds,
            static bound => bound.Name == ExpressionV2ContractLimits.SyntaxPartitionCountBoundName);
        _ = StaticFieldV2ExpressionDescriptor.Create(
            exactRaw,
            StaticFieldV2AliasQualifier.None(),
            exactSegments,
            exactPartitions,
            exactCounts,
            exactBounds);

        var overSegments = SimpleSegments(24);
        var overRaw = string.Join(".", overSegments.Select(static segment => segment.Identifier.DecodedText));
        var overPartitions = StaticFieldV2ExpressionDescriptor.DerivePartitionsThroughFirstBound(
            StaticFieldV2AliasQualifier.None(),
            overSegments,
            DumpExpressionFallbackKind.None,
            null,
            null);
        Assert.Equal(StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1, overPartitions.Length);
        var overCounts = CountsForSimpleSegments(overRaw, overSegments, overPartitions.Length);
        var overBounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(overRaw, overCounts);
        var stopped = StaticFieldV2SyntaxOutcome.Unsupported(
            overRaw,
            StaticFieldV2SyntaxIssue.PartitionBoundReached,
            overCounts,
            ImmutableArray.Create(Error("W8_PARTITION_BOUND", DumpExpressionDiagnosticStage.Projection)),
            overBounds);
        Assert.Null(stopped.Descriptor);

        var prefixCounts = CountsForSimpleSegments(
            overRaw,
            overSegments,
            StaticFieldV2Limits.MaximumSyntaxPartitionCount);
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionDescriptor.Create(
            overRaw,
            StaticFieldV2AliasQualifier.None(),
            overSegments,
            overPartitions.Take(StaticFieldV2Limits.MaximumSyntaxPartitionCount).ToImmutableArray(),
            prefixCounts,
            StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(overRaw, prefixCounts)));
    }

    /// <summary>Proves every simultaneous pair of numeric stops is rejected instead of misreporting a later boundary.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Syntax_boundary_precedence_rejects_pairwise_poisoned_counters()
    {
        var rows = CapPlusOneRows().ToArray();
        for (var earlierIndex = 0; earlierIndex < rows.Length; earlierIndex++)
        {
            for (var laterIndex = earlierIndex + 1; laterIndex < rows.Length; laterIndex++)
            {
                var earlier = rows[earlierIndex];
                var later = rows[laterIndex];
                var raw = earlier.RawExpression.Length >= later.RawExpression.Length
                    ? earlier.RawExpression
                    : later.RawExpression;
                var counts = MergeCounts(earlier.Counts, later.Counts);
                var bounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(raw, counts);
                Assert.Throws<ArgumentException>(() => CreateStopped(earlier.Issue, raw, counts, bounds));
                Assert.Throws<ArgumentException>(() => CreateStopped(later.Issue, raw, counts, bounds));
            }
        }

        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Invalid(
            "x",
            StaticFieldV2SyntaxIssue.ParserDiagnostic,
            EmptyCounts(),
            ImmutableArray.Create(Error("W8_PARSER", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        _ = StaticFieldV2SyntaxOutcome.Invalid(
            "x",
            StaticFieldV2SyntaxIssue.ParserDiagnostic,
            ZeroCounts(),
            ImmutableArray.Create(Error("W8_PARSER", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty);

        static StaticFieldV2SyntaxOutcome CreateStopped(
            StaticFieldV2SyntaxIssue issue,
            string rawExpression,
            StaticFieldV2ParserCounts counts,
            ImmutableArray<EvaluationDeterministicBound> bounds) =>
            (int)issue < 100
                ? StaticFieldV2SyntaxOutcome.Invalid(
                    rawExpression,
                    issue,
                    counts,
                    ImmutableArray.Create(Error("W8_EARLY_BOUND", DumpExpressionDiagnosticStage.Parse)),
                    bounds)
                : StaticFieldV2SyntaxOutcome.Unsupported(
                    rawExpression,
                    issue,
                    counts,
                    ImmutableArray.Create(Error("W8_LATE_BOUND", DumpExpressionDiagnosticStage.Projection)),
                    bounds);
    }

    /// <summary>Proves syntax diagnostics stay in their two stages and terminal errors match the selected stop.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Syntax_outcome_diagnostics_have_exact_stage_and_severity_rules()
    {
        var fixture = ComplexSyntaxFixture();
        var descriptor = StaticFieldV2ExpressionDescriptor.Create(
            fixture.RawExpression,
            fixture.Alias,
            fixture.Segments,
            fixture.Partitions,
            fixture.Counts,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        var parseInformation = DumpExpressionDiagnostic.Create(
            "W8_PARSE_NOTE",
            DumpExpressionDiagnosticStage.Parse,
            DumpExpressionDiagnosticSeverity.Information,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);
        var storageWarning = DumpExpressionDiagnostic.Create(
            "W8_STORAGE_NOTE",
            DumpExpressionDiagnosticStage.Storage,
            DumpExpressionDiagnosticSeverity.Warning,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);
        _ = StaticFieldV2SyntaxOutcome.Admitted(descriptor, ImmutableArray.Create(parseInformation));
        Assert.Throws<ArgumentException>(() =>
            StaticFieldV2SyntaxOutcome.Admitted(descriptor, ImmutableArray.Create(storageWarning)));

        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Invalid(
            "x",
            StaticFieldV2SyntaxIssue.ParseError,
            ZeroCounts(),
            ImmutableArray.Create(Error("W8_WRONG_STAGE", DumpExpressionDiagnosticStage.Projection)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Unsupported(
            "x",
            StaticFieldV2SyntaxIssue.TreeShapeUnsupported,
            EmptyCounts(),
            ImmutableArray.Create(Error("W8_WRONG_STAGE", DumpExpressionDiagnosticStage.Parse)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => StaticFieldV2SyntaxOutcome.Unsupported(
            "x",
            StaticFieldV2SyntaxIssue.TreeShapeUnsupported,
            EmptyCounts(),
            ImmutableArray.Create(Error("W8_LATE_STAGE", DumpExpressionDiagnosticStage.Storage)),
            ImmutableArray<EvaluationDeterministicBound>.Empty));
    }

    /// <summary>Proves input arrays and canonical-byte properties are copied and canonical domains remain distinct.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contracts_defensively_copy_arrays_and_keep_domains_distinct()
    {
        var mutableArguments = new[] { SimpleNamedType("Request") };
        var segment = StaticFieldV2ExpressionNameSegment.Create(
            Id("Outer"),
            StaticFieldV2ExpressionSeparatorKind.None,
            ImmutableCollectionsMarshal.AsImmutableArray(mutableArguments));
        var suffixSegment = DumpExpressionSuffixSegment.Create(Id("Value"), DumpExpressionSuffixAccessKind.Direct);
        var suffixArray = new[] { suffixSegment };
        var suffix = DumpExpressionSuffixDescriptor.Create(
            DumpExpressionSuffixKind.DirectMember,
            ImmutableCollectionsMarshal.AsImmutableArray(suffixArray));
        mutableArguments[0] = SimpleNamedType("Changed");
        suffixArray[0] = DumpExpressionSuffixSegment.Create(Id("Changed"), DumpExpressionSuffixAccessKind.Direct);

        Assert.Equal("Request", segment.TypeArguments[0].NameSegments[0].Identifier.DecodedText);
        Assert.Equal("Value", suffix.Segments[0].Identifier.DecodedText);
        Assert.NotEqual(segment.Sha256, suffix.Sha256);
        var exposedArguments = segment.TypeArguments;
        ImmutableCollectionsMarshal.AsArray(exposedArguments)![0] = SimpleNamedType("GetterChanged");
        Assert.Equal("Request", segment.TypeArguments[0].NameSegments[0].Identifier.DecodedText);
        var originalSuffixDigest = suffix.Sha256;
        var exposedBytes = suffix.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(exposedBytes)![0] ^= 0xFF;
        Assert.NotEqual(exposedBytes.ToArray(), suffix.CanonicalBytes.ToArray());
        Assert.Equal(originalSuffixDigest, suffix.Sha256);
        Assert.Throws<ArgumentException>(() => StaticFieldV2ExpressionNameSegment.Create(
            Id("X"),
            StaticFieldV2ExpressionSeparatorKind.None,
            default));
    }

    /// <summary>Proves the public checkpoint exposes one shared profile discriminator and sealed reference contracts.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Public_checkpoint_surface_uses_one_profile_enum_and_sealed_reference_types()
    {
        var assembly = typeof(DumpExpressionProfileKind).Assembly;
        Assert.Null(assembly.GetType("PhoenixInspect.Product.DumpQuery.StaticFieldV2ExpressionProfile"));
        Assert.Equal(
            new[] { DumpExpressionProfileKind.StaticFieldExpressionV2, DumpExpressionProfileKind.FrameValueExpressionV1 },
            Enum.GetValues<DumpExpressionProfileKind>());
        var checkpointTypes = assembly.GetExportedTypes().Where(static type =>
            type.Namespace == "PhoenixInspect.Product.DumpQuery" &&
            (type.Name.StartsWith("DumpExpression", StringComparison.Ordinal) ||
             type.Name.StartsWith("StaticFieldV2", StringComparison.Ordinal) ||
             type.Name is nameof(ExpressionV2ContractLimits) or nameof(FrameValueV1Limits)))
            .ToArray();
        Assert.All(checkpointTypes.Where(static type => type.IsClass && !type.IsAbstract), static type => Assert.True(type.IsSealed));

        // The W8.6c physical-acquisition seams are a live dump session, its fault, and one enumerated module
        // observation. None of the three is a canonical replayable draft identity, so none declares a canonical domain.
        var domainFields = checkpointTypes
            .Where(static type => type.IsClass && !type.IsAbstract)
            .Where(static type => type.Name is not (
                nameof(StaticFieldV2RuntimeAcquisitionSession) or
                nameof(StaticFieldV2RuntimeAcquisitionException) or
                nameof(StaticFieldV2RuntimeModuleObservation)))
            .Select(type => type.GetField(
                "CanonicalDomain",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic))
            .ToArray();
        Assert.DoesNotContain(domainFields, static field => field is null);
        var domains = domainFields.Select(static field => Assert.IsType<string>(field!.GetRawConstantValue())).ToArray();
        Assert.Equal(domains.Length, domains.Distinct(StringComparer.Ordinal).Count());

        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in checkpointTypes)
        {
            var typeName = $"T:{type.FullName}";
            var typeDocumentation = Assert.Single(members,
                member => string.Equals((string?)member.Attribute("name"), typeName, StringComparison.Ordinal));
            Assert.True(typeDocumentation.Value.Contains("draft", StringComparison.OrdinalIgnoreCase));

            foreach (var factory in type.GetMethods(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.Static |
                         System.Reflection.BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var methodPrefix = $"M:{type.FullName}.{factory.Name}";
                var methodDocumentation = Assert.Single(members, member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, methodPrefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{methodPrefix}(", StringComparison.Ordinal)));
                Assert.True(methodDocumentation.Value.Contains("draft", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static DumpExpressionDiagnostic Error(string code, DumpExpressionDiagnosticStage stage) =>
        DumpExpressionDiagnostic.Create(
            code,
            stage,
            DumpExpressionDiagnosticSeverity.Error,
            ImmutableArray<DumpExpressionDiagnosticArgument>.Empty);

    private static DumpExpressionV2ProvenanceIdentity Provenance(
        DumpExpressionProfileKind profile,
        DumpExpressionRouteKind route,
        params DumpExpressionCapabilityKind[] called)
    {
        var calledSet = called.ToHashSet();
        var uses = Enum.GetValues<DumpExpressionCapabilityKind>().Select(kind =>
            calledSet.Contains(kind)
                ? DumpExpressionCapabilityUse.Create(
                    kind,
                    DumpExpressionCapabilityDisposition.Exact,
                    1,
                    Digest,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty)
                : DumpExpressionCapabilityUse.Create(
                    kind,
                    DumpExpressionCapabilityDisposition.NotRequired,
                    0,
                    null,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty)).ToImmutableArray();
        return DumpExpressionV2ProvenanceIdentity.Create(
            profile,
            Digest,
            route,
            uses,
            ImmutableArray<DumpExpressionDiagnostic>.Empty);
    }

    private static DumpExpressionV2ProvenanceIdentity ProvenanceWithDispositions(
        DumpExpressionProfileKind profile,
        DumpExpressionRouteKind route,
        params (DumpExpressionCapabilityKind Kind, DumpExpressionCapabilityDisposition Disposition)[] entries)
    {
        var dispositions = entries.ToDictionary(static entry => entry.Kind, static entry => entry.Disposition);
        var uses = Enum.GetValues<DumpExpressionCapabilityKind>().Select(kind =>
        {
            var disposition = dispositions.GetValueOrDefault(kind, DumpExpressionCapabilityDisposition.NotRequired);
            return disposition switch
            {
                DumpExpressionCapabilityDisposition.Exact => DumpExpressionCapabilityUse.Create(
                    kind,
                    disposition,
                    1,
                    Digest,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty),
                DumpExpressionCapabilityDisposition.NonExact => DumpExpressionCapabilityUse.Create(
                    kind,
                    disposition,
                    1,
                    Digest,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray.Create(Error(
                        "W8_CAPABILITY_STOP",
                        kind == DumpExpressionCapabilityKind.Parse
                            ? DumpExpressionDiagnosticStage.Parse
                            : DumpExpressionDiagnosticStage.ContextAcquisition))),
                DumpExpressionCapabilityDisposition.NotRequired or DumpExpressionCapabilityDisposition.NotReached =>
                    DumpExpressionCapabilityUse.Create(
                        kind,
                        disposition,
                        0,
                        null,
                        ImmutableArray<EvaluationDeterministicBound>.Empty,
                        ImmutableArray<DumpExpressionDiagnostic>.Empty),
                _ => throw new ArgumentOutOfRangeException(nameof(entries)),
            };
        }).ToImmutableArray();
        return DumpExpressionV2ProvenanceIdentity.Create(
            profile,
            Digest,
            route,
            uses,
            ImmutableArray<DumpExpressionDiagnostic>.Empty);
    }

    private static DumpExpressionCapabilityUse Use(
        DumpExpressionV2ProvenanceIdentity provenance,
        DumpExpressionCapabilityKind kind) => provenance.CapabilityUses.Single(use => use.Capability == kind);

    private static DumpExpressionV2ProvenanceIdentity UnreachedProvenance(
        DumpExpressionCapabilityDisposition parseDisposition) =>
        DumpExpressionV2ProvenanceIdentity.Create(
            DumpExpressionProfileKind.StaticFieldExpressionV2,
            Digest,
            DumpExpressionRouteKind.NotReached,
            UnreachedCapabilityLedger(parseDisposition),
            ImmutableArray<DumpExpressionDiagnostic>.Empty);

    private static ImmutableArray<DumpExpressionCapabilityUse> UnreachedCapabilityLedger(
        DumpExpressionCapabilityDisposition parseDisposition) =>
        Enum.GetValues<DumpExpressionCapabilityKind>().Select(kind =>
        {
            if (kind != DumpExpressionCapabilityKind.Parse)
            {
                return DumpExpressionCapabilityUse.Create(
                    kind,
                    DumpExpressionCapabilityDisposition.NotReached,
                    0,
                    null,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty);
            }
            return parseDisposition switch
            {
                DumpExpressionCapabilityDisposition.Exact => DumpExpressionCapabilityUse.Create(
                    kind,
                    parseDisposition,
                    1,
                    Digest,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty),
                DumpExpressionCapabilityDisposition.NonExact => DumpExpressionCapabilityUse.Create(
                    kind,
                    parseDisposition,
                    1,
                    Digest,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray.Create(Error("W8_PARSE", DumpExpressionDiagnosticStage.Parse))),
                DumpExpressionCapabilityDisposition.NotReached => DumpExpressionCapabilityUse.Create(
                    kind,
                    parseDisposition,
                    0,
                    null,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    ImmutableArray<DumpExpressionDiagnostic>.Empty),
                _ => throw new ArgumentOutOfRangeException(nameof(parseDisposition)),
            };
        }).ToImmutableArray();

    private static DumpExpressionV2OutcomeAxes SyntaxStoppedAxes(DumpExpressionSyntaxStatus syntax) =>
        DumpExpressionV2OutcomeAxes.Create(
            syntax,
            DumpExpressionContextOutcome.NotReached,
            DumpExpressionRootAttributionOutcome.NotReached,
            DumpExpressionLexicalCompletenessOutcome.NotReached,
            DumpExpressionTypeBindingOutcome.NotReached,
            DumpExpressionTypeConstructionOutcome.NotReached,
            DumpExpressionMemberLookupOutcome.NotReached,
            DumpExpressionRuntimeConstructionOutcome.NotReached,
            DumpExpressionStorageOutcome.NotReached,
            DumpExpressionValueOutcome.NotReached,
            DumpExpressionSuffixOutcome.NotReached,
            DumpExpressionCompletenessOutcome.NoAnswer);

    private static DumpExpressionV2OutcomeAxes CompletePrefixAxes(
        DumpExpressionValueOutcome value,
        DumpExpressionSuffixOutcome suffix,
        DumpExpressionCompletenessOutcome completeness) =>
        DumpExpressionV2OutcomeAxes.Create(
            DumpExpressionSyntaxStatus.Admitted,
            DumpExpressionContextOutcome.NotRequired,
            DumpExpressionRootAttributionOutcome.NotRequired,
            DumpExpressionLexicalCompletenessOutcome.NotRequired,
            DumpExpressionTypeBindingOutcome.Exact,
            DumpExpressionTypeConstructionOutcome.Exact,
            DumpExpressionMemberLookupOutcome.Exact,
            DumpExpressionRuntimeConstructionOutcome.Exact,
            DumpExpressionStorageOutcome.Exact,
            value,
            suffix,
            completeness);

    private static ComplexSyntaxData ComplexSyntaxFixture()
    {
        var request = SimpleNamedType("Request");
        var batchArray = StaticFieldV2TypeSyntax.SzArray(SimpleNamedType("Batch"));
        var dictionary = StaticFieldV2TypeSyntax.Named(
            StaticFieldV2AliasQualifier.None(),
            ImmutableArray.Create(StaticFieldV2TypeNameSegment.Create(
                Id("Dictionary"),
                ImmutableArray.Create(
                    StaticFieldV2TypeSyntax.Predefined(StaticFieldV2PredefinedTypeKind.String),
                    batchArray))));
        var matrix = StaticFieldV2TypeSyntax.MultidimensionalArray(dictionary, 2);
        var segments = ImmutableArray.Create(
            Segment("Demo", StaticFieldV2ExpressionSeparatorKind.None),
            Segment("Outer", StaticFieldV2ExpressionSeparatorKind.Dot, request),
            Segment("Inner", StaticFieldV2ExpressionSeparatorKind.Dot, matrix),
            Segment("Current", StaticFieldV2ExpressionSeparatorKind.Dot),
            Segment("Payload", StaticFieldV2ExpressionSeparatorKind.Dot),
            Segment("Code", StaticFieldV2ExpressionSeparatorKind.ConditionalDot));
        var partitions = ImmutableArray.CreateBuilder<StaticFieldV2StructuralPartition>();
        foreach (var fieldIndex in new[] { 3, 4 })
        {
            var suffixSegments = segments.Skip(fieldIndex + 1).Select(static segment =>
                DumpExpressionSuffixSegment.Create(
                    segment.Identifier,
                    segment.Separator == StaticFieldV2ExpressionSeparatorKind.ConditionalDot
                        ? DumpExpressionSuffixAccessKind.Conditional
                        : DumpExpressionSuffixAccessKind.Direct)).ToImmutableArray();
            var suffixKind = suffixSegments.Length switch
            {
                0 => DumpExpressionSuffixKind.NotRequested,
                1 => DumpExpressionSuffixKind.DirectMember,
                2 => DumpExpressionSuffixKind.FixedDepthMemberChain,
                _ => throw new InvalidOperationException(),
            };
            var suffix = DumpExpressionSuffixDescriptor.Create(suffixKind, suffixSegments);
            foreach (var topLevelIndex in new[] { 0, 1 })
            {
                partitions.Add(StaticFieldV2StructuralPartition.Create(
                    StaticFieldV2CandidateKind.QualifiedOwner,
                    fieldIndex,
                    topLevelIndex,
                    suffix));
            }
        }
        return new ComplexSyntaxData(
            "requestlib::Demo.Outer<Request>.Inner<Dictionary<string,Batch[]>[,]>.Current.Payload?.Code",
            StaticFieldV2AliasQualifier.Named(Id("requestlib")),
            segments,
            partitions.ToImmutable(),
            StaticFieldV2ParserCounts.Create(40, 30, 8, 6, 4, 4, 6, 2, 10, 0, 4));
    }

    private static StaticFieldV2TypeSyntax SimpleNamedType(string name) =>
        StaticFieldV2TypeSyntax.Named(
            StaticFieldV2AliasQualifier.None(),
            ImmutableArray.Create(StaticFieldV2TypeNameSegment.Create(
                Id(name),
                ImmutableArray<StaticFieldV2TypeSyntax>.Empty)));

    private static StaticFieldV2ExpressionNameSegment Segment(
        string name,
        StaticFieldV2ExpressionSeparatorKind separator,
        params StaticFieldV2TypeSyntax[] arguments) =>
        StaticFieldV2ExpressionNameSegment.Create(Id(name), separator, ImmutableArray.Create(arguments));

    private static ImmutableArray<StaticFieldV2ExpressionNameSegment> SimpleSegments(int count) =>
        Enumerable.Range(0, count)
            .Select(index => Segment(
                $"N{index}",
                index == 0
                    ? StaticFieldV2ExpressionSeparatorKind.None
                    : StaticFieldV2ExpressionSeparatorKind.Dot))
            .ToImmutableArray();

    private static StaticFieldV2ParserCounts CountsForSimpleSegments(
        string rawExpression,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        int partitionCount)
    {
        var syntax = SyntaxFactory.ParseExpression(
            rawExpression,
            options: new CSharpParseOptions(LanguageVersion.CSharp14));
        Assert.Empty(syntax.GetDiagnostics());
        return StaticFieldV2ParserCounts.Create(
            syntax.DescendantNodesAndSelf().Count(),
            syntax.DescendantTokens().Count(),
            SyntaxDepth(syntax),
            segments.Length,
            0,
            0,
            0,
            0,
            segments.Max(static segment => segment.Identifier.DecodedText.Length),
            0,
            partitionCount);

        static int SyntaxDepth(Microsoft.CodeAnalysis.SyntaxNode node) =>
            1 + node.ChildNodes().Select(SyntaxDepth).DefaultIfEmpty(0).Max();
    }

    private static DumpExpressionIdentifier Id(string value) => DumpExpressionIdentifier.Create(value);

    private static StaticFieldV2ParserCounts EmptyCounts() =>
        StaticFieldV2ParserCounts.Create(1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0);

    private static StaticFieldV2ParserCounts ZeroCounts() =>
        StaticFieldV2ParserCounts.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static StaticFieldV2ParserCounts MergeCounts(
        StaticFieldV2ParserCounts left,
        StaticFieldV2ParserCounts right) =>
        StaticFieldV2ParserCounts.Create(
            Math.Max(left.NodeCount, right.NodeCount),
            Math.Max(left.TokenCount, right.TokenCount),
            Math.Max(left.MaximumDepth, right.MaximumDepth),
            Math.Max(left.ProjectedSegmentCount, right.ProjectedSegmentCount),
            Math.Max(left.ProjectedTypeArgumentCount, right.ProjectedTypeArgumentCount),
            Math.Max(left.MaximumTypeTopologyDepth, right.MaximumTypeTopologyDepth),
            Math.Max(left.CumulativeTypeTopologyNodeCount, right.CumulativeTypeTopologyNodeCount),
            Math.Max(left.MaximumArrayRank, right.MaximumArrayRank),
            Math.Max(left.MaximumDecodedIdentifierLength, right.MaximumDecodedIdentifierLength),
            Math.Max(left.MaximumDecodedFallbackStringLength, right.MaximumDecodedFallbackStringLength),
            Math.Max(left.CompletePartitionCount, right.CompletePartitionCount));

    private static IEnumerable<CapRow> CapPlusOneRows()
    {
        yield return Row(
            StaticFieldV2SyntaxIssue.ExpressionBoundReached,
            StaticFieldV2ParserCounts.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new string('x', 513));
        yield return Row(
            StaticFieldV2SyntaxIssue.NodeTokenBoundReached,
            StaticFieldV2ParserCounts.Create(128, 129, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 65, 0, 0, 0, 0, 0, 0, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.IdentifierBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 0, 0, 0, 0, 0, 65, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.FallbackStringBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 0, 0, 0, 0, 0, 1, 257, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.SegmentBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 33, 0, 0, 0, 0, 1, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 1, 0, 17, 0, 0, 1, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 1, 0, 1, 129, 0, 1, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.TypeArgumentBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 1, 65, 1, 65, 0, 1, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.ArrayRankBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 1, 1, 1, 1, 33, 1, 0, 0),
            "x");
        yield return Row(
            StaticFieldV2SyntaxIssue.PartitionBoundReached,
            StaticFieldV2ParserCounts.Create(1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 64),
            "x");

        static CapRow Row(
            StaticFieldV2SyntaxIssue issue,
            StaticFieldV2ParserCounts counts,
            string rawExpression) =>
            new(
                issue,
                counts,
                rawExpression,
                StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(rawExpression, counts),
                (int)issue is >= 1 and < 100);
    }

    private sealed record ComplexSyntaxData(
        string RawExpression,
        StaticFieldV2AliasQualifier Alias,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> Segments,
        ImmutableArray<StaticFieldV2StructuralPartition> Partitions,
        StaticFieldV2ParserCounts Counts);

    private sealed record CapRow(
        StaticFieldV2SyntaxIssue Issue,
        StaticFieldV2ParserCounts Counts,
        string RawExpression,
        ImmutableArray<EvaluationDeterministicBound> Bounds,
        bool Invalid);
}
