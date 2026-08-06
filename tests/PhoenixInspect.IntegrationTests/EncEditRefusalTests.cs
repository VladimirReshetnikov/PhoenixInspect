using System.Collections.Immutable;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the E2 refusal wiring: an explicit-route owner whose module is declared with applied edit generations
/// stops at the construction stage with its typed issue, an unedited declaration changes nothing, and an absent
/// declaration vector keeps the previous behavior exactly.
/// </summary>
/// <remarks>
/// The edit states are constructed at the pinned measured shapes — the enablement bit set with the counter at its
/// floor for the unedited arm and one above it for the edited arm — so the test exercises exactly the contract the
/// E1 disposition froze rather than synthetic flag values the runtime never produces.
/// </remarks>
public sealed class EncEditRefusalTests
{
    private const string Expression = "global::Pipe.App.GenericSlot<int>.Text";

    /// <summary>An edited module refuses construction; unedited and absent declarations do not.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Edited_owner_module_refuses_construction_with_its_typed_stop()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var refused = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            Expression,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, refused.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Partial, refused.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            refused.Provenance.OwnerConstruction!.ResultKind);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
            refused.Provenance.OwnerConstruction.Issue);
        Assert.Equal(DumpExpressionMemberLookupOutcome.NotReached, refused.Axes.MemberLookup);
        Assert.Equal(DumpExpressionValueOutcome.NotReached, refused.Axes.Value);

        // An unedited declaration for the same module changes nothing about the construction.
        var declaredUnedited = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            Expression,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 1)),
            ]));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, declaredUnedited.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.None,
            declaredUnedited.Provenance.OwnerConstruction!.Issue);

        // An absent vector keeps the previous behavior exactly, construction included.
        var absent = StaticFieldV2ExpressionPipeline.Evaluate(
            W8V2ExpressionPipelineTests.Request(world, Expression));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, absent.Axes.TypeConstruction);
        Assert.Equal(declaredUnedited.Axes, absent.Axes);
        Assert.NotEqual(absent.Sha256, refused.Sha256);
    }

    /// <summary>The contextual route refuses an edited owner module through the same typed stop.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contextual_route_refuses_an_edited_owner_module()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var refused = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "GenericSlot<Holder>.Current",
            scopedContext: W8V2ExpressionPipelineTests.NamespaceImportContext(world, "Pipe.App"),
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, refused.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Partial, refused.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
            refused.Provenance.OwnerConstruction!.Issue);
        Assert.Equal(DumpExpressionMemberLookupOutcome.NotReached, refused.Axes.MemberLookup);
    }

    /// <summary>
    /// Member lookup refuses a walked level in a declared edited module through its own typed stop — the
    /// defense-in-depth guard below the construction-stage refusal, whose cross-module positive arm only an
    /// edited multi-module corpus can exercise end to end.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_lookup_refuses_a_walked_level_in_an_edited_module()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var lookup = StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            world.App,
            world.HostType.TypeDefinitionToken,
            DumpExpressionIdentifier.Create("Text"),
            world.Ancestry,
            world.FieldCatalogs,
            world.PropertyCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(StaticFieldV2MemberLookupResultKind.NonExact, lookup.ResultKind);
        Assert.Equal(
            StaticFieldV2MemberLookupIssue.BaseModuleEditedGenerationsNotComposed,
            lookup.Issue);
    }

    private static StaticFieldV2ModuleEditStateOutcome EditState(ulong generationCounter) =>
        StaticFieldV2ModuleEditStateOutcome.IssueExact(
            runtimeModuleAddress: 0x7000_0000,
            moduleFlags: 0x9019,
            generationCounter: generationCounter);
}
