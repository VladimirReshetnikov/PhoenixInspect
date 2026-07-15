using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises W4.6a structural pure-model selection, opaque-leaf freezing, and fail-closed graph preparation.
/// </summary>
public sealed class MethodGraphPlannerModelTests
{
    private static readonly ModuleHandle Module = new(
        0xC401020304050607,
        0xD408091011121314);
    private static readonly ModuleHandle OtherModule = new(
        0xE401020304050607,
        0xF408091011121314);
    private static readonly TypeSig RootType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.W46Root");
    private static readonly TypeSig HelperType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000002,
        "Interpreter.Tests.W46Helper");
    private static readonly TypeSig MiddleType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000003,
        "Interpreter.Tests.W46Middle");
    private static readonly TypeSig AlternateHelperType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000004,
        "Interpreter.Tests.W46AlternateHelper");

    /// <summary>
    /// Proves the compiler-shaped root freezes one opaque model leaf without reading its body and charges exactly five
    /// traversal units across the root, leaf, two fields, and edge.
    /// </summary>
    [Fact]
    public void RequiredModelFreezesOpaqueLeafBeforeTargetBodyAcquisition()
    {
        var fixture = CreateExactFixture(includeHelperDefinition: false, forbidHelperDefinition: true);
        var model = Model(fixture.Target);
        var registry = new RecordingRegistry(
            target => PureCallModelSelectionResult.Selected(model),
            fixture.Resolver.Operations);

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
        Assert.Equal(fixture.Root, plan.Root);
        var rootNode = Assert.Single(plan.Nodes);
        Assert.Equal(fixture.Root, rootNode.Method);
        Assert.True(plan.Fields.SequenceEqual([fixture.FirstField, fixture.SecondField]));
        Assert.Equal(2, plan.RequiredLogicalDepth);
        Assert.Equal(5, plan.TraversalUnitCount);

        var call = Assert.Single(plan.CallSites);
        Assert.Equal((fixture.Root, 12, fixture.Helper.MetadataToken, fixture.Helper),
            (call.Caller, call.IlOffset, call.MetadataToken, call.Target.Method));
        Assert.Equal(FrozenMethodCallDisposition.PureModel, call.Disposition);
        Assert.Equal(EvaluationEffectStatus.None, call.Effects);
        Assert.Equal(model.Descriptor, call.ModelDescriptor);

        var leaf = Assert.Single(plan.ModeledLeaves);
        Assert.Equal(fixture.Target, leaf.Target);
        Assert.Equal(fixture.Helper, leaf.Method);
        Assert.Equal(model.Descriptor, leaf.Descriptor);
        Assert.Equal(EvaluationEffectStatus.None, leaf.Effects);
        Assert.True(plan.TryGetModeledLeaf(fixture.Helper, out var lookedUpLeaf));
        Assert.Same(leaf, lookedUpLeaf);
        Assert.False(plan.TryGetNode(fixture.Helper, out _));

        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(fixture.Target, Assert.Single(registry.Targets));
        Assert.Equal(1, fixture.Resolver.DefinitionCount(fixture.Root));
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
        Assert.Equal(1, fixture.Resolver.FieldCount(fixture.Root, fixture.FirstField.Handle.MetadataToken));
        Assert.Equal(1, fixture.Resolver.FieldCount(fixture.Root, fixture.SecondField.Handle.MetadataToken));
        Assert.Equal(1, fixture.Resolver.CallCount(fixture.Root, fixture.Helper.MetadataToken));
        Assert.DoesNotContain(
            $"definition:{fixture.Helper.MetadataToken:X8}",
            fixture.Resolver.Operations);
        Assert.True(
            fixture.Resolver.Operations.IndexOf(
                $"call:{fixture.Root.MetadataToken:X8}:{fixture.Helper.MetadataToken:X8}") <
            fixture.Resolver.Operations.IndexOf($"select:{fixture.Helper.MetadataToken:X8}"));
    }

    /// <summary>
    /// Proves W4.6a plans cannot execute a caller prefix before modeled transfer support exists: activation rejects the
    /// graph before creating root state, re-resolving metadata, or invoking the selected capability.
    /// </summary>
    [Fact]
    public void CurrentMachineRejectsModeledPlanAtomicallyBeforeActivation()
    {
        var fixture = CreateExactFixture(includeHelperDefinition: false);
        var model = Model(fixture.Target);
        var registry = new RecordingRegistry(
            _ => PureCallModelSelectionResult.Selected(model),
            selectedModel: model);
        var preparation = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(preparation.Plan);
        var operationCount = fixture.Resolver.Operations.Count;
        var selectionCount = registry.SelectionCount;
        var domain = new ConcreteDomain();
        var memoryModel = new ConcreteMemoryModel(domain);
        var (receiver, memory) = memoryModel.NewObject(ConcreteMemory.Empty, RootType);
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            fixture.Resolver,
            memoryModel,
            new InstructionBudgetPolicy());

        var activation = machine.ActivatePreparedGraph(
            plan,
            maximumLogicalCallDepth: 2,
            ImmutableArray.Create(receiver),
            memory);

        Assert.False(activation.IsSuccess);
        Assert.Null(activation.State);
        Assert.Equal(MachineRunStatus.Blocked, activation.Status);
        var failure = Assert.IsType<ExecutionFailure>(activation.Failure);
        Assert.Equal(ExecutionFailureKind.UnsupportedInstruction, failure.Kind);
        Assert.Equal("EXEC_MODEL_EXECUTION_UNAVAILABLE", failure.Code);
        Assert.Equal(fixture.Root, failure.Method);
        Assert.Equal(0, failure.IlOffset);
        Assert.Equal(operationCount, fixture.Resolver.Operations.Count);
        Assert.Equal(selectionCount, registry.SelectionCount);
        Assert.Equal(0, model.InvocationCount);
    }

    /// <summary>Proves the legacy preparation entry point remains interpreted and never consults a model registry.</summary>
    [Fact]
    public void DefaultPreparationRetainsInterpretedDispositionAndFrozenEquality()
    {
        var firstFixture = CreateExactFixture(includeHelperDefinition: true);
        var secondFixture = CreateExactFixture(includeHelperDefinition: true);

        var first = new MethodGraphPlanner(firstFixture.Resolver).Prepare(firstFixture.Root);
        var second = new MethodGraphPlanner(secondFixture.Resolver).Prepare(secondFixture.Root);

        Assert.True(first.IsSuccess, first.Failure?.Code);
        Assert.True(second.IsSuccess, second.Failure?.Code);
        var firstPlan = Assert.IsType<FrozenMethodGraphPlan>(first.Plan);
        var secondPlan = Assert.IsType<FrozenMethodGraphPlan>(second.Plan);
        Assert.Equal(firstPlan, secondPlan);
        Assert.Equal(firstPlan.GetHashCode(), secondPlan.GetHashCode());
        Assert.Equal(2, firstPlan.Nodes.Length);
        Assert.Empty(firstPlan.ModeledLeaves);
        var call = Assert.Single(firstPlan.CallSites);
        Assert.Equal(FrozenMethodCallDisposition.Interpreted, call.Disposition);
        Assert.Equal(EvaluationEffectStatus.None, call.Effects);
        Assert.Null(call.ModelDescriptor);
        Assert.Equal(LegacyCallSiteHash(call), call.GetHashCode());
        Assert.True(firstPlan.TryGetNode(firstFixture.Helper, out _));
        Assert.False(firstPlan.TryGetModeledLeaf(firstFixture.Helper, out _));
        Assert.Equal(1, firstFixture.Resolver.DefinitionCount(firstFixture.Helper));
        Assert.Equal(5, firstPlan.TraversalUnitCount);
        Assert.Equal(2, firstPlan.RequiredLogicalDepth);
    }

    /// <summary>
    /// Proves repeated edges retain distinct call sites while structurally equal targets select and charge one leaf.
    /// </summary>
    [Fact]
    public void RepeatedModeledEdgesSelectAndChargeStructuralTargetOnce()
    {
        var root = Method(1);
        var helper = Method(2);
        var target = Target(helper, HelperType);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(root, RootCallsBody(helper, helper), maxStack: 3);
        resolver.Calls[(root, helper.MetadataToken)] = target;
        resolver.ForbiddenDefinitions.Add(helper);
        var registry = SelectedRegistry(target);

        var result = new MethodGraphPlanner(resolver).RequirePureModel(root, helper, registry);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
        Assert.Single(plan.Nodes);
        Assert.Single(plan.ModeledLeaves);
        Assert.Equal(2, plan.CallSites.Length);
        Assert.Equal(new[] { 2, 9 }, plan.CallSites.Select(call => call.IlOffset));
        Assert.All(
            plan.CallSites,
            call =>
            {
                Assert.Equal(FrozenMethodCallDisposition.PureModel, call.Disposition);
                Assert.Equal(plan.ModeledLeaves[0].Descriptor, call.ModelDescriptor);
            });
        Assert.Equal(4, plan.TraversalUnitCount);
        Assert.Equal(2, plan.RequiredLogicalDepth);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(1, resolver.CallCount(root, helper.MetadataToken));
        Assert.Equal(0, resolver.DefinitionCount(helper));
    }

    /// <summary>
    /// Proves a modeled leaf terminates only its edge while an interpreted parent remains a real graph node and adds
    /// one logical depth level without acquiring the modeled body.
    /// </summary>
    [Fact]
    public void NestedInterpretedCallToModeledLeafHasDepthThree()
    {
        var root = Method(1);
        var middle = Method(2);
        var helper = Method(3);
        var middleTarget = Target(middle, MiddleType);
        var helperTarget = Target(helper, HelperType);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(root, RootCallsBody(middle), maxStack: 2);
        resolver.Definitions[middle] = CallableDefinition(
            middle,
            MiddleType,
            CallsBody(helper),
            maxStack: 2);
        resolver.Calls[(root, middle.MetadataToken)] = middleTarget;
        resolver.Calls[(middle, helper.MetadataToken)] = helperTarget;
        resolver.ForbiddenDefinitions.Add(helper);
        var registry = SelectedRegistry(helperTarget);

        var result = new MethodGraphPlanner(resolver).RequirePureModel(root, helper, registry);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
        Assert.Equal(new[] { root, middle }, plan.Nodes.Select(node => node.Method));
        Assert.Equal(helper, Assert.Single(plan.ModeledLeaves).Method);
        Assert.Equal(2, plan.CallSites.Length);
        Assert.Equal(
            FrozenMethodCallDisposition.Interpreted,
            plan.CallSites.Single(call => call.Caller == root).Disposition);
        Assert.Equal(
            FrozenMethodCallDisposition.PureModel,
            plan.CallSites.Single(call => call.Caller == middle).Disposition);
        Assert.Equal(3, plan.RequiredLogicalDepth);
        Assert.Equal(5, plan.TraversalUnitCount);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(1, resolver.DefinitionCount(middle));
        Assert.Equal(0, resolver.DefinitionCount(helper));
    }

    /// <summary>Proves structurally equal fresh registries and capabilities reproduce equal frozen graph content.</summary>
    [Fact]
    public void FreshModelCapabilitiesProduceStructurallyEqualPlans()
    {
        var firstFixture = CreateExactFixture(includeHelperDefinition: false);
        var secondFixture = CreateExactFixture(includeHelperDefinition: false);
        var firstRegistry = SelectedRegistry(firstFixture.Target);
        var secondRegistry = SelectedRegistry(secondFixture.Target);

        var first = new MethodGraphPlanner(firstFixture.Resolver)
            .RequirePureModel(firstFixture.Root, firstFixture.Helper, firstRegistry);
        var second = new MethodGraphPlanner(secondFixture.Resolver)
            .RequirePureModel(secondFixture.Root, secondFixture.Helper, secondRegistry);

        Assert.True(first.IsSuccess, first.Failure?.Code);
        Assert.True(second.IsSuccess, second.Failure?.Code);
        var firstPlan = Assert.IsType<FrozenMethodGraphPlan>(first.Plan);
        var secondPlan = Assert.IsType<FrozenMethodGraphPlan>(second.Plan);
        Assert.Equal(firstPlan, secondPlan);
        Assert.Equal(firstPlan.GetHashCode(), secondPlan.GetHashCode());
        Assert.NotSame(firstRegistry.SelectedModel, secondRegistry.SelectedModel);
        Assert.True(firstPlan.ModeledLeaves.SequenceEqual(secondPlan.ModeledLeaves));
        Assert.True(firstPlan.CallSites.SequenceEqual(secondPlan.CallSites));
    }

    /// <summary>Proves model identity and semantic version independently participate in frozen graph equality.</summary>
    [Fact]
    public void ModelIdentityAndVersionAreFrozenSelectionAxes()
    {
        var fixture = CreateExactFixture(includeHelperDefinition: false);
        var baseline = PrepareWithIdentity(fixture, "w4.combine-markers", new PureCallModelVersion(1, 0, 0));
        var changedId = PrepareWithIdentity(fixture, "w4.combine-markers-alt", new PureCallModelVersion(1, 0, 0));
        var changedVersion = PrepareWithIdentity(fixture, "w4.combine-markers", new PureCallModelVersion(1, 0, 1));

        Assert.NotEqual(baseline, changedId);
        Assert.NotEqual(baseline, changedVersion);
        Assert.NotEqual(
            Assert.Single(baseline.ModeledLeaves).Descriptor.Identity,
            Assert.Single(changedId.ModeledLeaves).Descriptor.Identity);
        Assert.NotEqual(
            Assert.Single(baseline.ModeledLeaves).Descriptor.Identity,
            Assert.Single(changedVersion.ModeledLeaves).Descriptor.Identity);
    }

    /// <summary>Proves every typed non-selected registry outcome returns no partial plan and never falls back.</summary>
    /// <param name="kind">The structural selection outcome to return.</param>
    /// <param name="status">The expected preparation status.</param>
    /// <param name="code">The stable code preserved from selection.</param>
    [Theory]
    [InlineData(PureCallModelSelectionKind.NotApplicable, MachineRunStatus.Blocked, "W4.Model.Missing")]
    [InlineData(PureCallModelSelectionKind.Blocked, MachineRunStatus.Blocked, "W4.Model.Conflict")]
    [InlineData(PureCallModelSelectionKind.Invalid, MachineRunStatus.InvalidProgram, "W4.Model.InvalidRegistry")]
    public void NonSelectedResultNeverFallsBackToAvailableTargetBody(
        PureCallModelSelectionKind kind,
        MachineRunStatus status,
        string code)
    {
        var fixture = CreateExactFixture(includeHelperDefinition: true);
        var registry = new RecordingRegistry(_ => kind switch
        {
            PureCallModelSelectionKind.NotApplicable => PureCallModelSelectionResult.NotApplicable(code),
            PureCallModelSelectionKind.Blocked => PureCallModelSelectionResult.Blocked(code),
            PureCallModelSelectionKind.Invalid => PureCallModelSelectionResult.Invalid(code),
            _ => throw new InvalidOperationException("The theory supplies only non-selected kinds."),
        });

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        AssertFailure(result, status, code, fixture.Root, 12);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
    }

    /// <summary>Proves registry and descriptor capability exceptions normalize without target-body fallback.</summary>
    /// <param name="descriptorThrows">Whether selection succeeds with a capability whose descriptor getter throws.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionCapabilityFailureIsPayloadSafeAndBodyFree(bool descriptorThrows)
    {
        var fixture = CreateExactFixture(includeHelperDefinition: true);
        var registry = descriptorThrows
            ? new RecordingRegistry(_ => PureCallModelSelectionResult.Selected(new ThrowingDescriptorModel()))
            : new RecordingRegistry(_ => throw new InvalidOperationException("host-controlled sensitive payload"));

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        AssertFailure(result, MachineRunStatus.Blocked, "W4.Model.Capability", fixture.Root, 12);
        Assert.DoesNotContain("sensitive", result.Failure!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
    }

    /// <summary>Proves a selected descriptor must match both the exact MethodDef and complete structural signature.</summary>
    /// <param name="changeMethod">Whether the mismatched descriptor changes the MethodDef rather than its signature.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedDescriptorMismatchIsInvalidAndBodyFree(bool changeMethod)
    {
        var fixture = CreateExactFixture(includeHelperDefinition: true);
        var mismatchedTarget = changeMethod
            ? Target(Method(20), HelperType)
            : Target(fixture.Helper, AlternateHelperType);
        var registry = new RecordingRegistry(
            _ => PureCallModelSelectionResult.Selected(Model(mismatchedTarget)));

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        AssertFailure(
            result,
            MachineRunStatus.InvalidProgram,
            "W4.Model.DescriptorMismatch",
            fixture.Root,
            12);
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
    }

    /// <summary>
    /// Proves equal MethodDef identities with conflicting body-free signatures cannot alias one modeled leaf or cause
    /// a second registry selection.
    /// </summary>
    [Fact]
    public void ConflictingStructuralTargetsRejectBeforeSecondSelectionOrBodyRead()
    {
        var root = Method(1);
        var firstMiddle = Method(2);
        var secondMiddle = Method(3);
        var helper = Method(4);
        var firstTarget = Target(helper, HelperType);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(
            root,
            RootCallsBody(firstMiddle, secondMiddle),
            maxStack: 3);
        resolver.Definitions[firstMiddle] = CallableDefinition(
            firstMiddle,
            MiddleType,
            CallsBody(helper),
            maxStack: 2);
        resolver.Definitions[secondMiddle] = CallableDefinition(
            secondMiddle,
            MiddleType,
            CallsBody(helper),
            maxStack: 2);
        resolver.Calls[(root, firstMiddle.MetadataToken)] = Target(firstMiddle, MiddleType);
        resolver.Calls[(root, secondMiddle.MetadataToken)] = Target(secondMiddle, MiddleType);
        resolver.Calls[(firstMiddle, helper.MetadataToken)] = firstTarget;
        resolver.Calls[(secondMiddle, helper.MetadataToken)] = Target(helper, AlternateHelperType);
        resolver.ForbiddenDefinitions.Add(helper);
        var registry = SelectedRegistry(firstTarget);

        var result = new MethodGraphPlanner(resolver).RequirePureModel(root, helper, registry);

        AssertFailure(
            result,
            MachineRunStatus.Blocked,
            "W4.Model.TargetConflict",
            secondMiddle,
            2);
        Assert.Equal(
            ResolutionFailureKind.Conflict,
            Assert.IsType<ExecutionFailure>(result.Failure).ResolutionFailure?.Kind);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(0, resolver.DefinitionCount(helper));
    }

    /// <summary>Proves a normalized unsupported effect blocks before a target body can be requested.</summary>
    [Fact]
    public void UnsupportedModelEffectBlocksWithoutPlanOrFallback()
    {
        var fixture = CreateExactFixture(includeHelperDefinition: true);
        var registry = SelectedRegistry(fixture.Target, effects: EvaluationEffectStatus.Unsupported);

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        AssertFailure(
            result,
            MachineRunStatus.Blocked,
            "W4.Model.EffectUnsupported",
            fixture.Root,
            12);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
    }

    /// <summary>Proves every defined non-exact confidence classification blocks without body fallback.</summary>
    /// <param name="confidence">The defined confidence value outside the closed exact-only planning profile.</param>
    [Theory]
    [InlineData(PureCallModelConfidence.BestEffort)]
    [InlineData(PureCallModelConfidence.Partial)]
    [InlineData(PureCallModelConfidence.UnsupportedLayout)]
    public void NonExactModelConfidenceBlocksBeforeTargetBodyAccess(PureCallModelConfidence confidence)
    {
        var fixture = CreateExactFixture(includeHelperDefinition: true);
        var model = new TestModel(new PureCallModelDescriptor(
            Identity("w4.combine-markers", new PureCallModelVersion(1, 0, 0)),
            fixture.Target,
            confidence,
            EvaluationEffectStatus.None));
        var registry = new RecordingRegistry(
            _ => PureCallModelSelectionResult.Selected(model),
            selectedModel: model);

        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);

        AssertFailure(
            result,
            MachineRunStatus.Blocked,
            "W4.Model.ConfidenceUnsupported",
            fixture.Root,
            12);
        Assert.Equal(1, registry.SelectionCount);
        Assert.Equal(0, fixture.Resolver.DefinitionCount(fixture.Helper));
        Assert.Equal(0, model.InvocationCount);
    }

    /// <summary>Proves non-normalized virtual and modeled effect declarations are invalid before selection.</summary>
    /// <param name="effects">The prohibited effect declaration.</param>
    [Theory]
    [InlineData(EvaluationEffectStatus.VirtualOnly)]
    [InlineData(EvaluationEffectStatus.Modeled)]
    public void NonNormalizedEffectDeclarationsCannotReachPlanning(EvaluationEffectStatus effects)
    {
        var target = Target(Method(2), HelperType);

        Assert.Throws<ArgumentException>(() => new PureCallModelDescriptor(
            Identity("w4.combine-markers", new PureCallModelVersion(1, 0, 0)),
            target,
            PureCallModelConfidence.Exact,
            effects));
    }

    /// <summary>Proves a required structural target must actually occur and does not trigger name-based selection.</summary>
    [Fact]
    public void UnreachedRequiredTargetBlocksWithoutRegistryOrTargetBodyAccess()
    {
        var root = Method(1);
        var middle = Method(2);
        var helper = Method(3);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(root, RootCallsBody(middle), maxStack: 2);
        resolver.Definitions[middle] = CallableDefinition(middle, MiddleType, LeafBody(), maxStack: 2);
        resolver.Calls[(root, middle.MetadataToken)] = Target(middle, MiddleType);
        resolver.ForbiddenDefinitions.Add(helper);
        var registry = new RecordingRegistry(_ => throw new InvalidOperationException("must not select"));

        var result = new MethodGraphPlanner(resolver).RequirePureModel(root, helper, registry);

        AssertFailure(
            result,
            MachineRunStatus.Blocked,
            "W4.Model.TargetNotReached",
            root,
            0);
        Assert.Equal(0, registry.SelectionCount);
        Assert.Equal(1, resolver.DefinitionCount(root));
        Assert.Equal(1, resolver.DefinitionCount(middle));
        Assert.Equal(0, resolver.DefinitionCount(helper));
    }

    /// <summary>Proves invalid required-target axes reject before any metadata capability call.</summary>
    /// <param name="targetKind">Selects a default, root-equal, or cross-module target.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InvalidRequiredTargetRejectsBeforeResolution(int targetKind)
    {
        var root = Method(1);
        var target = targetKind switch
        {
            0 => default,
            1 => root,
            2 => new MethodHandle(OtherModule, 0x06000002),
            _ => throw new InvalidOperationException(),
        };
        var resolver = new GraphResolver();
        var registry = new RecordingRegistry(_ => throw new InvalidOperationException("must not select"));

        var result = new MethodGraphPlanner(resolver).RequirePureModel(root, target, registry);

        AssertFailure(
            result,
            MachineRunStatus.InvalidProgram,
            "W4.Model.TargetInvalid",
            root,
            0);
        Assert.Empty(resolver.Operations);
        Assert.Equal(0, registry.SelectionCount);
    }

    private static FrozenMethodGraphPlan PrepareWithIdentity(
        ExactFixture fixture,
        string stableId,
        PureCallModelVersion version)
    {
        var registry = SelectedRegistry(fixture.Target, stableId, version);
        var result = new MethodGraphPlanner(fixture.Resolver)
            .RequirePureModel(fixture.Root, fixture.Helper, registry);
        Assert.True(result.IsSuccess, result.Failure?.Code);
        return Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
    }

    private static ExactFixture CreateExactFixture(
        bool includeHelperDefinition,
        bool forbidHelperDefinition = false)
    {
        var root = Method(1);
        var helper = Method(2);
        var firstField = Field(1, RootType);
        var secondField = Field(2, RootType);
        var target = Target(helper, HelperType);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(
            root,
            ExactRootBody(firstField.Handle.MetadataToken, secondField.Handle.MetadataToken, helper.MetadataToken),
            maxStack: 2);
        if (includeHelperDefinition)
        {
            resolver.Definitions[helper] = CallableDefinition(helper, HelperType, LeafBody(), maxStack: 2);
        }

        if (forbidHelperDefinition)
        {
            resolver.ForbiddenDefinitions.Add(helper);
        }

        resolver.Fields[(root, firstField.Handle.MetadataToken)] = firstField;
        resolver.Fields[(root, secondField.Handle.MetadataToken)] = secondField;
        resolver.Calls[(root, helper.MetadataToken)] = target;
        return new ExactFixture(root, helper, target, firstField, secondField, resolver);
    }

    private static RecordingRegistry SelectedRegistry(
        ResolvedMethodCallTarget target,
        string stableId = "w4.combine-markers",
        PureCallModelVersion? version = null,
        EvaluationEffectStatus effects = EvaluationEffectStatus.None)
    {
        var model = Model(
            target,
            stableId,
            version ?? new PureCallModelVersion(1, 0, 0),
            effects);
        return new RecordingRegistry(
            _ => PureCallModelSelectionResult.Selected(model),
            selectedModel: model);
    }

    private static TestModel Model(
        ResolvedMethodCallTarget target,
        string stableId = "w4.combine-markers",
        PureCallModelVersion? version = null,
        EvaluationEffectStatus effects = EvaluationEffectStatus.None) =>
        new(new PureCallModelDescriptor(
            Identity(stableId, version ?? new PureCallModelVersion(1, 0, 0)),
            target,
            PureCallModelConfidence.Exact,
            effects));

    private static PureCallModelIdentity Identity(string stableId, PureCallModelVersion version) =>
        new(stableId, version);

    private static MethodHandle Method(int row) => new(Module, 0x06000000 | row);

    private static ResolvedField Field(int row, TypeSig declaringType) => new(
        new FieldHandle(Module, 0x04000000 | row),
        declaringType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    private static ResolvedMethodCallTarget Target(MethodHandle method, TypeSig declaringType) =>
        new(method, CallableSignature(declaringType));

    private static MethodCallSignatureShape RootSignature() => new(
        RootType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: true,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray<TypeSig>.Empty,
        TypeSig.Int32);

    private static MethodCallSignatureShape CallableSignature(TypeSig declaringType) => new(
        declaringType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: false,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
        TypeSig.Int32);

    private static ResolvedMethodDefinition RootDefinition(
        MethodHandle method,
        byte[] code,
        int maxStack) =>
        Definition(method, RootSignature(), code, maxStack);

    private static ResolvedMethodDefinition CallableDefinition(
        MethodHandle method,
        TypeSig declaringType,
        byte[] code,
        int maxStack) =>
        Definition(method, CallableSignature(declaringType), code, maxStack);

    private static ResolvedMethodDefinition Definition(
        MethodHandle method,
        MethodCallSignatureShape signature,
        byte[] code,
        int maxStack) =>
        new(
            method,
            IlBody.Create(maxStack, code),
            new MethodSignatureShape(signature, ImmutableArray<TypeSig>.Empty));

    private static byte[] ExactRootBody(int firstFieldToken, int secondFieldToken, int helperToken)
    {
        var code = new List<byte> { 0x02 };
        EmitToken(code, 0x7B, firstFieldToken);
        code.Add(0x02);
        EmitToken(code, 0x7B, secondFieldToken);
        EmitToken(code, 0x28, helperToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] RootCallsBody(params MethodHandle[] targets) =>
        BuildCallsBody(targets.Select(target => target.MetadataToken), useArguments: false);

    private static byte[] CallsBody(params MethodHandle[] targets) =>
        BuildCallsBody(targets.Select(target => target.MetadataToken), useArguments: true);

    private static byte[] BuildCallsBody(IEnumerable<int> targetTokens, bool useArguments)
    {
        var tokens = targetTokens.ToArray();
        var code = new List<byte>();
        for (var index = 0; index < tokens.Length; index++)
        {
            code.Add(useArguments ? (byte)0x02 : (byte)0x16);
            code.Add(useArguments ? (byte)0x03 : (byte)0x16);
            EmitToken(code, 0x28, tokens[index]);
            if (index != 0)
            {
                code.Add(0x58);
            }
        }

        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] LeafBody() => [0x02, 0x03, 0x58, 0x2A];

    private static void EmitToken(List<byte> code, byte opcode, int token)
    {
        code.Add(opcode);
        code.Add((byte)token);
        code.Add((byte)(token >> 8));
        code.Add((byte)(token >> 16));
        code.Add((byte)(token >> 24));
    }

    private static void AssertFailure(
        MethodGraphPreparationResult result,
        MachineRunStatus status,
        string code,
        MethodHandle method,
        int ilOffset)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Equal(status, result.Status);
        var failure = Assert.IsType<ExecutionFailure>(result.Failure);
        Assert.Equal(code, failure.Code);
        Assert.Equal(method, failure.Method);
        Assert.Equal(ilOffset, failure.IlOffset);
    }

    private static int LegacyCallSiteHash(FrozenMethodCallSite callSite)
    {
        var hash = 17;
        hash = unchecked((hash * 397) ^ (int)callSite.Caller.Module.High);
        hash = unchecked((hash * 397) ^ (int)(callSite.Caller.Module.High >> 32));
        hash = unchecked((hash * 397) ^ (int)callSite.Caller.Module.Low);
        hash = unchecked((hash * 397) ^ (int)(callSite.Caller.Module.Low >> 32));
        hash = unchecked((hash * 397) ^ callSite.Caller.MetadataToken);
        hash = unchecked((hash * 397) ^ callSite.IlOffset);
        hash = unchecked((hash * 397) ^ callSite.MetadataToken);
        hash = unchecked((hash * 397) ^ (int)callSite.Target.Method.Module.High);
        hash = unchecked((hash * 397) ^ (int)(callSite.Target.Method.Module.High >> 32));
        hash = unchecked((hash * 397) ^ (int)callSite.Target.Method.Module.Low);
        hash = unchecked((hash * 397) ^ (int)(callSite.Target.Method.Module.Low >> 32));
        hash = unchecked((hash * 397) ^ callSite.Target.Method.MetadataToken);
        return unchecked((hash * 397) ^ callSite.Target.Signature.GetHashCode());
    }

    private sealed record ExactFixture(
        MethodHandle Root,
        MethodHandle Helper,
        ResolvedMethodCallTarget Target,
        ResolvedField FirstField,
        ResolvedField SecondField,
        GraphResolver Resolver);

    private sealed class TestModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = descriptor;

        internal int InvocationCount { get; private set; }

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            InvocationCount++;
            return PureCallModelOutcome.ExactReturn(0);
        }
    }

    private sealed class ThrowingDescriptorModel : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor =>
            throw new InvalidOperationException("host-controlled descriptor payload");

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation) =>
            throw new InvalidOperationException("The planning test must not invoke a model.");
    }

    private sealed class RecordingRegistry : IPureCallModelRegistry
    {
        private readonly Func<ResolvedMethodCallTarget, PureCallModelSelectionResult> _select;
        private readonly List<string>? _operations;

        internal RecordingRegistry(
            Func<ResolvedMethodCallTarget, PureCallModelSelectionResult> select,
            List<string>? operations = null,
            IPureCallModel? selectedModel = null)
        {
            _select = select;
            _operations = operations;
            SelectedModel = selectedModel;
        }

        internal int SelectionCount { get; private set; }

        internal List<ResolvedMethodCallTarget> Targets { get; } = [];

        internal IPureCallModel? SelectedModel { get; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            Targets.Add(target);
            _operations?.Add($"select:{target.Method.MetadataToken:X8}");
            return _select(target);
        }
    }

    private sealed class GraphResolver : IResolutionServices
    {
        internal Dictionary<MethodHandle, ResolvedMethodDefinition> Definitions { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedField> Fields { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedMethodCallTarget> Calls { get; } = [];

        internal HashSet<MethodHandle> ForbiddenDefinitions { get; } = [];

        internal Dictionary<MethodHandle, int> DefinitionCounts { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), int> FieldCounts { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), int> CallCounts { get; } = [];

        internal List<string> Operations { get; } = [];

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            Operations.Add($"definition:{method.MetadataToken:X8}");
            Increment(DefinitionCounts, method);
            if (ForbiddenDefinitions.Contains(method))
            {
                throw new InvalidOperationException("A modeled target body must remain opaque.");
            }

            return Definitions.TryGetValue(method, out var definition)
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_METHOD_UNAVAILABLE",
                    "Synthetic method definition was not configured.");
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            Operations.Add($"field:{contextMethod.MetadataToken:X8}:{metadataToken:X8}");
            var key = (contextMethod, metadataToken);
            Increment(FieldCounts, key);
            return Fields.TryGetValue(key, out var field)
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_FIELD_UNAVAILABLE",
                    "Synthetic field descriptor was not configured.");
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Operations.Add($"call:{contextMethod.MetadataToken:X8}:{metadataToken:X8}");
            var key = (contextMethod, metadataToken);
            Increment(CallCounts, key);
            return Calls.TryGetValue(key, out var target)
                ? ResolutionResult<ResolvedMethodCallTarget>.Success(target)
                : ResolutionResult<ResolvedMethodCallTarget>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_CALL_UNAVAILABLE",
                    "Synthetic direct-call target was not configured.");
        }

        internal int DefinitionCount(MethodHandle method) => DefinitionCounts.GetValueOrDefault(method);

        internal int FieldCount(MethodHandle method, int token) => FieldCounts.GetValueOrDefault((method, token));

        internal int CallCount(MethodHandle method, int token) => CallCounts.GetValueOrDefault((method, token));

        private static void Increment<TKey>(Dictionary<TKey, int> counts, TKey key)
            where TKey : notnull =>
            counts[key] = counts.GetValueOrDefault(key) + 1;
    }
}
