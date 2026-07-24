using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;
using PhoenixInspect.Metadata.SRM;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves W4.6a pure-model graph selection over the compiler-emitted GetMarkerSummary/CombineMarkers dependency.
/// </summary>
public sealed class W4PureModelPlanningIntegrationTests
{
    private const string CallerName = "GetMarkerSummary";
    private const string HelperName = "CombineMarkers";
    private const int CallOffset = 12;

    /// <summary>
    /// Proves the real SRM call target becomes a body-free modeled leaf with stable structural replay across fresh
    /// modules, resolvers, registries, and capability objects.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CompilerEmittedPureModelLeafIsBodyFreeAndReplaysFresh()
    {
        using var first = Fixture.Create();
        using var fresh = Fixture.Create();

        Assert.Equal(first.Caller, fresh.Caller);
        Assert.Equal(first.Helper, fresh.Helper);
        Assert.Equal(first.Plan, fresh.Plan);
        Assert.Equal(first.Plan.GetHashCode(), fresh.Plan.GetHashCode());
        Assert.True(first.Plan.Nodes.SequenceEqual(fresh.Plan.Nodes));
        Assert.True(first.Plan.ModeledLeaves.SequenceEqual(fresh.Plan.ModeledLeaves));
        Assert.True(first.Plan.CallSites.SequenceEqual(fresh.Plan.CallSites));

        Assert.Equal(first.Caller, first.Plan.Root);
        var callerNode = Assert.Single(first.Plan.Nodes);
        Assert.Equal(first.Caller, callerNode.Method);
        Assert.Equal(2, first.Plan.Fields.Length);
        Assert.Equal(5, first.Plan.TraversalUnitCount);
        Assert.Equal(2, first.Plan.RequiredLogicalDepth);

        var call = Assert.Single(first.Plan.CallSites);
        Assert.Equal((first.Caller, CallOffset, first.Helper.MetadataToken, first.Helper),
            (call.Caller, call.IlOffset, call.MetadataToken, call.Target.Method));
        Assert.Equal(FrozenMethodCallDisposition.PureModel, call.Disposition);
        Assert.Equal(EvaluationEffectStatus.None, call.Effects);
        Assert.NotNull(call.ModelDescriptor);

        var leaf = Assert.Single(first.Plan.ModeledLeaves);
        Assert.Equal(first.Helper, leaf.Method);
        Assert.Equal(call.Target, leaf.Target);
        Assert.Equal(call.ModelDescriptor, leaf.Descriptor);
        Assert.Equal(
            new PureCallModelIdentity(
                "w4.combine-markers",
                new PureCallModelVersion(1, 0, 0)),
            leaf.Descriptor.Identity);
        Assert.Equal(PureCallModelConfidence.Exact, leaf.Descriptor.Confidence);
        Assert.Equal(EvaluationEffectStatus.None, leaf.Effects);
        Assert.True(first.Plan.TryGetModeledLeaf(first.Helper, out var lookedUpLeaf));
        Assert.Same(leaf, lookedUpLeaf);
        Assert.False(first.Plan.TryGetNode(first.Helper, out _));

        Assert.Equal(new ResolutionCounts(1, 2, 1), first.Resolution.Counts);
        Assert.Equal(1, first.Registry.SelectionCount);
        Assert.Equal(call.Target, Assert.Single(first.Registry.Targets));
        Assert.Equal(0, Assert.IsType<CompilerModel>(first.Registry.SelectedModel).InvocationCount);
    }

    private static string ResolveTargetAssemblyPath() =>
        TestTargetPaths.ResolveAssembly(TestTargetPaths.ResolveExecutable());

    private static MethodHandle ResolveMethodHandle(SrmMetadataModule module, string methodName)
    {
        var token = module.FindMethodDefinition("DumpProbe", methodName);
        Assert.True(token.IsSuccess, token.Failure?.Code);
        var handle = module.GetMethodHandle(token.Value);
        Assert.True(handle.IsSuccess, handle.Failure?.Code);
        return handle.Value;
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            SrmMetadataModule module,
            MethodHandle caller,
            MethodHandle helper,
            CountingResolutionServices resolution,
            CompilerRegistry registry,
            FrozenMethodGraphPlan plan)
        {
            Module = module;
            Caller = caller;
            Helper = helper;
            Resolution = resolution;
            Registry = registry;
            Plan = plan;
        }

        private SrmMetadataModule Module { get; }

        internal MethodHandle Caller { get; }

        internal MethodHandle Helper { get; }

        internal CountingResolutionServices Resolution { get; }

        internal CompilerRegistry Registry { get; }

        internal FrozenMethodGraphPlan Plan { get; }

        internal static Fixture Create()
        {
            var module = SrmMetadataModule.LoadFromFile(ResolveTargetAssemblyPath());
            try
            {
                var caller = ResolveMethodHandle(module, CallerName);
                var helper = ResolveMethodHandle(module, HelperName);
                var resolution = new CountingResolutionServices(
                    new MetadataResolutionServices(module),
                    helper);
                var registry = new CompilerRegistry();
                var result = new MethodGraphPlanner(resolution)
                    .RequirePureModel(caller, helper, registry);
                Assert.True(result.IsSuccess, result.Failure?.Code);
                return new Fixture(
                    module,
                    caller,
                    helper,
                    resolution,
                    registry,
                    Assert.IsType<FrozenMethodGraphPlan>(result.Plan));
            }
            catch
            {
                module.Dispose();
                throw;
            }
        }

        public void Dispose() => Module.Dispose();
    }

    private sealed class CompilerRegistry : IPureCallModelRegistry
    {
        internal int SelectionCount { get; private set; }

        internal List<ResolvedMethodCallTarget> Targets { get; } = [];

        internal IPureCallModel? SelectedModel { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            SelectionCount++;
            Targets.Add(target);
            var model = new CompilerModel(new PureCallModelDescriptor(
                new PureCallModelIdentity(
                    "w4.combine-markers",
                    new PureCallModelVersion(1, 0, 0)),
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None));
            SelectedModel = model;
            return PureCallModelSelectionResult.Selected(model);
        }
    }

    private sealed class CompilerModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = descriptor;

        internal int InvocationCount { get; private set; }

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            InvocationCount++;
            return PureCallModelOutcome.ExactReturn(0);
        }
    }

    private sealed class CountingResolutionServices(
        IResolutionServices inner,
        MethodHandle forbiddenDefinition) : IResolutionServices
    {
        internal ResolutionCounts Counts { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            if (method == forbiddenDefinition)
            {
                throw new InvalidOperationException(
                    "The selected compiler-emitted pure-model target body must remain opaque.");
            }

            Counts = Counts with { MethodDefinitions = Counts.MethodDefinitions + 1 };
            return inner.GetMethodDefinition(method);
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Methods = Counts.Methods + 1 };
            return inner.ResolveMethod(contextMethod, metadataToken);
        }

        public ResolutionResult<ResolvedField> ResolveField(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Counts = Counts with { Fields = Counts.Fields + 1 };
            return inner.ResolveField(contextMethod, metadataToken);
        }
    }

    private readonly record struct ResolutionCounts(int MethodDefinitions, int Fields, int Methods);
}
