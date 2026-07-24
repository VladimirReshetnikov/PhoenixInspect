using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;
using Xunit;
using static PhoenixInspect.Tests.MethodGraphPlannerTests;

namespace PhoenixInspect.Tests;

/// <summary>Proves configurable ordered method-graph traversal accounting and capability cutoffs.</summary>
public sealed class MethodGraphPlannerTraversalAccountingTests
{
    /// <summary>Freezes the exact discovery order and distinguishes exact fit from exhaustion.</summary>
    [Fact]
    public void ExactFitPublishesOrderedChargesAndFreshContentEquality()
    {
        var first = ExactFixture();
        var result = new MethodGraphPlanner(first.Resolver).Prepare(first.Root, 5);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var accounting = Assert.IsType<MethodGraphTraversalAccounting>(result.TraversalAccounting);
        Assert.Equal((5, 5, 0, false),
            (accounting.Limit, accounting.Used, accounting.Remaining, accounting.IsExhausted));
        Assert.Null(accounting.RejectedCharge);
        Assert.Equal(result.Plan!.TraversalUnitCount, accounting.Used);
        Assert.Collection(
            accounting.Charges,
            charge => AssertCharge(charge, 0, MethodGraphTraversalChargeKind.InterpretedMethod, first.Root, null, 0, first.Root.MetadataToken),
            charge => AssertCharge(charge, 1, MethodGraphTraversalChargeKind.FieldDependency, first.Root, first.First.Handle, 1, first.First.Handle.MetadataToken),
            charge => AssertCharge(charge, 2, MethodGraphTraversalChargeKind.FieldDependency, first.Root, first.Second.Handle, 7, first.Second.Handle.MetadataToken),
            charge => AssertCharge(charge, 3, MethodGraphTraversalChargeKind.DirectCallEdge, first.Root, null, 12, first.Helper.MetadataToken),
            charge => AssertCharge(charge, 4, MethodGraphTraversalChargeKind.InterpretedMethod, first.Helper, null, 12, first.Helper.MetadataToken));

        var copied = ImmutableCollectionsMarshal.AsArray(accounting.Charges)!;
        copied[0] = null!;
        Assert.NotNull(accounting.Charges[0]);

        var fresh = ExactFixture();
        var replay = new MethodGraphPlanner(fresh.Resolver).Prepare(fresh.Root, 5);
        Assert.Equal(accounting, replay.TraversalAccounting);
        Assert.Equal(accounting.GetHashCode(), replay.TraversalAccounting!.GetHashCode());

        var legacy = ExactFixture();
        var legacyResult = new MethodGraphPlanner(legacy.Resolver).Prepare(legacy.Root);
        Assert.True(legacyResult.IsSuccess, legacyResult.Failure?.Code);
        Assert.Equal(MethodGraphPlanner.MaximumConfigurableTraversalUnits, legacyResult.TraversalAccounting!.Limit);
        Assert.Equal(5, legacyResult.TraversalAccounting.Used);
    }

    /// <summary>Rejects invalid limits before capabilities and treats zero as a real exhausted budget.</summary>
    [Fact]
    public void InvalidAndZeroLimitsUseDistinctPreSessionAndSessionOutcomes()
    {
        foreach (var invalidLimit in new[] { -1, MethodGraphPlanner.MaximumConfigurableTraversalUnits + 1 })
        {
            var invalid = ExactFixture();
            var result = new MethodGraphPlanner(invalid.Resolver).Prepare(invalid.Root, invalidLimit);

            AssertFailure(result, MachineRunStatus.InvalidProgram, "W4.Budget.Traversal.Invalid");
            Assert.Null(result.TraversalAccounting);
            Assert.Empty(invalid.Resolver.Operations);
        }

        var modelRoot = Method(30);
        var modelTarget = Target(Method(31));
        var modelResolver = new GraphResolver();
        var modelRegistry = SelectedRegistry(modelTarget);
        var invalidModel = new MethodGraphPlanner(modelResolver).RequirePureModel(
            modelRoot,
            modelTarget.Method,
            modelRegistry,
            -1);
        AssertFailure(invalidModel, MachineRunStatus.InvalidProgram, "W4.Budget.Traversal.Invalid");
        Assert.Null(invalidModel.TraversalAccounting);
        Assert.Empty(modelResolver.Operations);
        Assert.Equal(0, modelRegistry.SelectionCount);

        var zero = ExactFixture();
        var exhausted = new MethodGraphPlanner(zero.Resolver).Prepare(zero.Root, 0);

        AssertFailure(exhausted, MachineRunStatus.BudgetExhausted, "W4.Budget.Traversal");
        var accounting = Assert.IsType<MethodGraphTraversalAccounting>(exhausted.TraversalAccounting);
        Assert.Equal((0, 0, 0, true),
            (accounting.Limit, accounting.Used, accounting.Remaining, accounting.IsExhausted));
        Assert.Empty(accounting.Charges);
        AssertCharge(
            Assert.IsType<MethodGraphTraversalCharge>(accounting.RejectedCharge),
            0,
            MethodGraphTraversalChargeKind.InterpretedMethod,
            zero.Root,
            null,
            0,
            zero.Root.MetadataToken);
        Assert.Empty(zero.Resolver.Operations);
    }

    /// <summary>Stops at field, edge, and callee-method boundaries without using later capabilities.</summary>
    [Theory]
    [InlineData(1, MethodGraphTraversalChargeKind.FieldDependency)]
    [InlineData(3, MethodGraphTraversalChargeKind.DirectCallEdge)]
    [InlineData(4, MethodGraphTraversalChargeKind.InterpretedMethod)]
    public void MidGraphExhaustionRetainsRejectedSubjectAndStopsCapabilities(
        int limit,
        MethodGraphTraversalChargeKind rejectedKind)
    {
        var fixture = ExactFixture();
        var result = new MethodGraphPlanner(fixture.Resolver).Prepare(fixture.Root, limit);

        AssertFailure(result, MachineRunStatus.BudgetExhausted, "W4.Budget.Traversal");
        var accounting = Assert.IsType<MethodGraphTraversalAccounting>(result.TraversalAccounting);
        Assert.Equal((limit, limit, 0, true),
            (accounting.Limit, accounting.Used, accounting.Remaining, accounting.IsExhausted));
        Assert.Equal(rejectedKind, accounting.RejectedCharge!.Kind);
        Assert.Equal(limit, accounting.RejectedCharge.Ordinal);
        Assert.Equal(1, fixture.Resolver.DefinitionCount(fixture.Root));
        Assert.Equal(limit >= 1 ? 1 : 0, fixture.Resolver.FieldCount(fixture.Root, fixture.First.Handle.MetadataToken));
        Assert.Equal(limit >= 2 ? 1 : 0, fixture.Resolver.FieldCount(fixture.Root, fixture.Second.Handle.MetadataToken));
        Assert.Equal(limit >= 4 ? 1 : 0, fixture.Resolver.CallCount(fixture.Root, fixture.Helper.MetadataToken));
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
    }

    /// <summary>Charges one structural field despite repeated instructions and retains accounting on resolution failure.</summary>
    [Fact]
    public void StructuralFieldDedupAndResolutionFailureRetainTruthfulAccounting()
    {
        var root = Method(10);
        var helper = Method(12);
        var field = Field(10, RootType);
        var resolver = Resolver(
            RootDefinition(
                root,
                ExactRootBody(field.Handle.MetadataToken, field.Handle.MetadataToken, helper.MetadataToken),
                maxStack: 2),
            HelperDefinition(helper, [0x02, 0x03, 0x58, 0x2A], maxStack: 2));
        resolver.Fields[(root, field.Handle.MetadataToken)] = field;
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var success = new MethodGraphPlanner(resolver).Prepare(root, 4);

        Assert.True(success.IsSuccess, success.Failure?.Code);
        Assert.Equal(
            [MethodGraphTraversalChargeKind.InterpretedMethod,
             MethodGraphTraversalChargeKind.FieldDependency,
             MethodGraphTraversalChargeKind.DirectCallEdge,
             MethodGraphTraversalChargeKind.InterpretedMethod],
            success.TraversalAccounting!.Charges.Select(charge => charge.Kind));
        Assert.Equal(1, resolver.FieldCount(root, field.Handle.MetadataToken));

        var missingRoot = Method(11);
        var unavailable = new GraphResolver();
        var failure = new MethodGraphPlanner(unavailable).Prepare(missingRoot, 7);

        AssertFailure(failure, MachineRunStatus.Blocked, "TEST_METHOD_UNAVAILABLE");
        Assert.Equal((7, 1, 6, false),
            (failure.TraversalAccounting!.Limit,
             failure.TraversalAccounting.Used,
             failure.TraversalAccounting.Remaining,
             failure.TraversalAccounting.IsExhausted));
        Assert.Equal(MethodGraphTraversalChargeKind.InterpretedMethod, failure.TraversalAccounting.Charges[0].Kind);
    }

    /// <summary>Charges each edge but one modeled leaf, and exhausts before registry selection.</summary>
    [Fact]
    public void ModeledLeafChargeIsDeduplicatedAndPrecedesRegistrySelection()
    {
        var root = Method(20);
        var targetMethod = Method(21);
        var target = Target(targetMethod);
        var exhaustedResolver = Resolver(RootDefinition(root, RootRepeatedCallsBody(targetMethod, 2), maxStack: 3));
        exhaustedResolver.Calls[(root, targetMethod.MetadataToken)] = target;
        var exhaustedRegistry = SelectedRegistry(target);

        var exhausted = new MethodGraphPlanner(exhaustedResolver)
            .RequirePureModel(root, targetMethod, exhaustedRegistry, 3);

        AssertFailure(exhausted, MachineRunStatus.BudgetExhausted, "W4.Budget.Traversal");
        Assert.Equal(
            [MethodGraphTraversalChargeKind.InterpretedMethod,
             MethodGraphTraversalChargeKind.DirectCallEdge,
             MethodGraphTraversalChargeKind.DirectCallEdge],
            exhausted.TraversalAccounting!.Charges.Select(charge => charge.Kind));
        Assert.Equal(MethodGraphTraversalChargeKind.ModeledLeaf, exhausted.TraversalAccounting.RejectedCharge!.Kind);
        Assert.Equal(1, exhaustedResolver.CallCount(root, targetMethod.MetadataToken));
        Assert.Equal(0, exhaustedResolver.DefinitionCount(targetMethod));
        Assert.Equal(0, exhaustedRegistry.SelectionCount);

        var successResolver = Resolver(RootDefinition(root, RootRepeatedCallsBody(targetMethod, 2), maxStack: 3));
        successResolver.Calls[(root, targetMethod.MetadataToken)] = target;
        var successRegistry = SelectedRegistry(target);
        var success = new MethodGraphPlanner(successResolver)
            .RequirePureModel(root, targetMethod, successRegistry, 4);

        Assert.True(success.IsSuccess, success.Failure?.Code);
        Assert.Equal(4, success.TraversalAccounting!.Used);
        Assert.False(success.TraversalAccounting.IsExhausted);
        Assert.Equal(MethodGraphTraversalChargeKind.ModeledLeaf, success.TraversalAccounting.Charges[3].Kind);
        Assert.Equal(1, successRegistry.SelectionCount);
        Assert.Equal(0, successResolver.DefinitionCount(targetMethod));

        var blockedResolver = Resolver(RootDefinition(root, RootRepeatedCallsBody(targetMethod, 2), maxStack: 3));
        blockedResolver.Calls[(root, targetMethod.MetadataToken)] = target;
        var blockedRegistry = new RecordingRegistry(
            _ => PureCallModelSelectionResult.Blocked("W4.Model.TestBlocked"));
        var blocked = new MethodGraphPlanner(blockedResolver)
            .RequirePureModel(root, targetMethod, blockedRegistry, 4);
        AssertFailure(blocked, MachineRunStatus.Blocked, "W4.Model.TestBlocked");
        Assert.Equal((4, 4, 0, false),
            (blocked.TraversalAccounting!.Limit,
             blocked.TraversalAccounting.Used,
             blocked.TraversalAccounting.Remaining,
             blocked.TraversalAccounting.IsExhausted));
        Assert.Null(blocked.TraversalAccounting.RejectedCharge);
        Assert.Equal(1, blockedRegistry.SelectionCount);
    }

    private static ExactGraphFixture ExactFixture()
    {
        var root = Method(1);
        var helper = Method(2);
        var first = Field(1, RootType);
        var second = Field(2, RootType);
        var resolver = Resolver(
            RootDefinition(root, ExactRootBody(first.Handle.MetadataToken, second.Handle.MetadataToken, helper.MetadataToken), 2),
            HelperDefinition(helper, [0x02, 0x03, 0x58, 0x2A], maxStack: 2));
        resolver.Fields[(root, first.Handle.MetadataToken)] = first;
        resolver.Fields[(root, second.Handle.MetadataToken)] = second;
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);
        return new ExactGraphFixture(root, helper, first, second, resolver);
    }

    private static RecordingRegistry SelectedRegistry(ResolvedMethodCallTarget target)
    {
        var model = new TestModel(new PureCallModelDescriptor(
            new PureCallModelIdentity("w4.traversal-test", new PureCallModelVersion(1, 0, 0)),
            target,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None));
        return new RecordingRegistry(_ => PureCallModelSelectionResult.Selected(model));
    }

    private static void AssertCharge(
        MethodGraphTraversalCharge charge,
        int ordinal,
        MethodGraphTraversalChargeKind kind,
        MethodHandle method,
        FieldHandle? field,
        int offset,
        int token) =>
        Assert.Equal((ordinal, kind, method, field, offset, token),
            (charge.Ordinal, charge.Kind, charge.Method, charge.Field, charge.IlOffset, charge.RawMetadataToken));

    private static void AssertFailure(MethodGraphPreparationResult result, MachineRunStatus status, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Equal(status, result.Status);
        Assert.Equal(code, Assert.IsType<ExecutionFailure>(result.Failure).Code);
    }

    private sealed record ExactGraphFixture(
        MethodHandle Root,
        MethodHandle Helper,
        ResolvedField First,
        ResolvedField Second,
        GraphResolver Resolver);

    private sealed class TestModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor => descriptor;

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation) =>
            PureCallModelOutcome.ExactReturn(0);
    }

    private sealed class RecordingRegistry(
        Func<ResolvedMethodCallTarget, PureCallModelSelectionResult> select) : IPureCallModelRegistry
    {
        internal int SelectionCount { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            return select(target);
        }
    }
}
