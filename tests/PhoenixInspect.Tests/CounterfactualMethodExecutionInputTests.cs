using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;
using PhoenixInspect.Domain.Concrete;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>Exercises W4.8's private typed runtime binding before the rooted runner consumes it.</summary>
public sealed class CounterfactualMethodExecutionInputTests
{
    private const string DigestA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string DigestB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string SyntheticId = "fixture.synthetic.binding.v1";
    private static readonly MethodHandle Root = MethodGraphPlannerTests.Method(1);
    private static readonly MethodHandle Helper = MethodGraphPlannerTests.Method(2);
    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField FirstField = MethodGraphPlannerTests.Field(1, Owner);
    private static readonly ResolvedField SecondField = MethodGraphPlannerTests.Field(2, Owner);

    /// <summary>
    /// Proves public construction only validates and copies structure: no operational capability is invoked or
    /// exposed, and neither caller nor consumer mutation can alter the retained observation catalog.
    /// </summary>
    [Fact]
    public void SyntheticBindingIsCapabilityFreePrivateAndDefensive()
    {
        var request = CreateRequest();
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(7, Owner);
        var source = CreateObservations(request).ToArray();
        var input = CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request,
            new PoisonResolver(),
            domain,
            new PoisonMemoryModel(),
            ConcreteMemory.Empty,
            receiver,
            ImmutableCollectionsMarshal.AsImmutableArray(source),
            new PoisonRegistry());
        var first = source[0];
        source[0] = null!;

        Assert.Same(request, input.Request);
        Assert.Same(first, input.FieldObservations[0]);
        Assert.Equal(0, domain.InternedNodeCount);
        Assert.False(input.RuntimeBundle.HasMaterializedRootArguments);

        var publicCopy = input.FieldObservations;
        ImmutableCollectionsMarshal.AsArray(publicCopy)![0] = null!;
        var privateCopy = input.RuntimeBundle.FieldObservations;
        ImmutableCollectionsMarshal.AsArray(privateCopy)![0] = null!;
        Assert.Same(first, input.FieldObservations[0]);
        Assert.Same(first, input.RuntimeBundle.FieldObservations[0]);

        var inputType = typeof(CounterfactualMethodExecutionInput<ConcreteMemory>);
        Assert.True(inputType.IsSealed);
        Assert.Empty(inputType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            inputType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            method => method.Name.Contains("Clone", StringComparison.Ordinal));
        Assert.DoesNotContain(
            inputType.GetMembers(BindingFlags.Public | BindingFlags.Instance),
            member => member.Name.Contains("Resolver", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("Receiver", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("Registry", StringComparison.OrdinalIgnoreCase) ||
                member.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            inputType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SelectMany(static method => method.GetParameters()),
            parameter => parameter.ParameterType == typeof(ImmutableArray<ProvenanceConcreteValue>));
        Assert.False(typeof(CounterfactualRuntimeBundle<ConcreteMemory>).IsPublic);
    }

    /// <summary>
    /// Rejects null, non-synthetic, non-exact receiver, uncorrelated evidence, and missing-required-registry bindings
    /// without consulting a resolver, registry, memory model, or machine.
    /// </summary>
    [Fact]
    public void InvalidBindingMatrixRejectsBeforeCapabilitiesWhileInternalDumpSourceIsReserved()
    {
        var request = CreateRequest();
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(1, Owner);
        var observations = CreateObservations(request);
        var resolver = new PoisonResolver();
        var memory = new PoisonMemoryModel();

        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            null!, resolver, domain, memory, ConcreteMemory.Empty, receiver, observations));
        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request, null!, domain, memory, ConcreteMemory.Empty, receiver, observations));
        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request, resolver, null!, memory, ConcreteMemory.Empty, receiver, observations));
        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request, resolver, domain, null!, ConcreteMemory.Empty, receiver, observations));
        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request, resolver, domain, memory, null!, receiver, observations));
        Assert.Throws<ArgumentNullException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request, resolver, domain, memory, ConcreteMemory.Empty, null!, observations));
        Assert.Throws<ArgumentException>(() => CreateInput(request, receiver: domain.ConstInt32(1)));
        Assert.Throws<ArgumentException>(() => CreateInput(
            request,
            receiver: domain.ObjectReference(
                1,
                TypeSig.CreateTypeDefinition(MethodGraphPlannerTests.Module, 0x02000003, "Ignored.Other"))));
        Assert.Throws<ArgumentException>(() => CreateInput(
            request,
            observations: default(ImmutableArray<CounterfactualFieldObservation>)));
        Assert.Throws<ArgumentException>(() => CreateInput(request, observations: [null!]));
        Assert.Throws<ArgumentException>(() => CreateInput(request, observations: [observations[1], observations[0]]));
        Assert.Throws<ArgumentException>(() => CreateInput(request, observations: [observations[0], observations[0]]));
        Assert.Throws<ArgumentException>(() => CreateInput(request, observations: observations.SetItem(
            0,
            CreateObservation(request, 0, FirstField, sourceSha256: DigestA))));
        Assert.Throws<ArgumentException>(() => CreateInput(request, observations: observations.SetItem(
            0,
            CreateObservation(request, 0, FirstField, importedObjectSha256: DigestA))));

        var modeledRequest = CreateRequest(requiredModelTarget: Helper);
        Assert.Throws<ArgumentException>(() => CreateInput(modeledRequest));
        _ = CreateInput(modeledRequest, registry: new PoisonRegistry());
        _ = CreateInput(request, registry: new PoisonRegistry());

        var dumpRequest = CreateDumpRequest();
        Assert.Throws<ArgumentException>(() => CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            dumpRequest,
            null!,
            null!,
            null!,
            null!,
            null!,
            default));
        var dumpInput = CounterfactualMethodExecutionInput<ConcreteMemory>.CreateValidated(
            dumpRequest,
            resolver,
            domain,
            memory,
            ConcreteMemory.Empty,
            receiver,
            CreateObservations(dumpRequest),
            null);
        Assert.Same(dumpRequest, dumpInput.Request);
        Assert.Equal(EvaluationEvidenceSourceKind.DumpSnapshot, dumpInput.Request.EvidenceSource);
        Assert.DoesNotContain(
            typeof(CounterfactualMethodExecutionInput<ConcreteMemory>)
                .GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Dump", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Materializes the receiver and canonical request arguments exactly once, preserving metadata parameter
    /// ordinals and evidence-specific lineage without accepting any caller-supplied argument domain vector.
    /// </summary>
    [Fact]
    public void RuntimeBundleMaterializesExactAndExplainedArgumentsDeterministicallyAndDefensively()
    {
        var request = CreateRequest(arguments:
        [
            CounterfactualInputEvidence.CreateExactInt32("argument.exact", DigestA, 42),
            CounterfactualInputEvidence.CreateUnknownInt32(
                "argument.partial",
                DigestB,
                EvaluationEvidenceStatus.Partial),
            CounterfactualInputEvidence.CreateUnknownInt32(
                "argument.unavailable",
                DigestA,
                EvaluationEvidenceStatus.Unavailable),
        ]);
        var domain = new ProvenanceConcreteDomain();
        var receiver = domain.ObjectReference(11, Owner);
        var input = CreateInput(request, domain: domain, receiver: receiver, observations: []);
        var prepared = input.RuntimeBundle.MaterializeRootArguments();

        Assert.False(input.RuntimeBundle.HasMaterializedRootArguments);
        Assert.True(prepared.HasMaterializedRootArguments);
        Assert.Throws<InvalidOperationException>(() => prepared.MaterializeRootArguments());
        var rootArguments = prepared.RootArguments;
        Assert.Equal(4, rootArguments.Length);
        Assert.Same(receiver, rootArguments[0]);
        Assert.True(domain.TryGetConstInt32(rootArguments[1], out var exact));
        Assert.Equal(42, exact);
        AssertOrigin(
            domain,
            rootArguments[2],
            1,
            EvaluationEvidenceStatus.Partial,
            DigestB,
            "W4.Unknown.RequestArgument.Partial");
        AssertOrigin(
            domain,
            rootArguments[3],
            2,
            EvaluationEvidenceStatus.Unavailable,
            DigestA,
            "W4.Unknown.RequestArgument.Unavailable");
        Assert.Equal(2, domain.InternedNodeCount);

        ImmutableCollectionsMarshal.AsArray(rootArguments)![0] = domain.ObjectReference(99, Owner);
        Assert.Same(receiver, prepared.RootArguments[0]);
    }

    /// <summary>
    /// Proves issuance requires the exact request-bound, argument-materialized bundle while operational capability
    /// and local receiver identities remain excluded from canonical plan bytes.
    /// </summary>
    [Fact]
    public void PlanRequiresExactPreparedBundleAndPreservesCanonicalIdentityAcrossCapabilities()
    {
        var firstRequest = CreateRequest();
        var freshEqualRequest = CreateRequest();
        var preparation = CreateGraph();
        var firstInput = CreateInput(firstRequest, receiverId: 1);
        var secondInput = CreateInput(freshEqualRequest, receiverId: 999);
        var firstBundle = firstInput.RuntimeBundle.MaterializeRootArguments();
        var secondBundle = secondInput.RuntimeBundle.MaterializeRootArguments();
        var first = CounterfactualMethodPlan<ConcreteMemory>.Issue(
            new object(),
            firstRequest,
            preparation.Plan!,
            preparation.TraversalAccounting!,
            firstBundle);
        var second = CounterfactualMethodPlan<ConcreteMemory>.Issue(
            new object(),
            freshEqualRequest,
            preparation.Plan!,
            preparation.TraversalAccounting!,
            secondBundle);

        Assert.Equal(firstRequest.Sha256, freshEqualRequest.Sha256);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));
        Assert.Throws<ArgumentException>(() => CounterfactualMethodPlan<ConcreteMemory>.Issue(
            new object(),
            freshEqualRequest,
            preparation.Plan!,
            preparation.TraversalAccounting!,
            firstBundle));
        Assert.Throws<ArgumentException>(() => CounterfactualMethodPlan<ConcreteMemory>.Issue(
            new object(),
            firstRequest,
            preparation.Plan!,
            preparation.TraversalAccounting!,
            firstInput.RuntimeBundle));

        var runtimeArguments = first.RuntimeBundle.RootArguments;
        ImmutableCollectionsMarshal.AsArray(runtimeArguments)![0] = null!;
        Assert.NotNull(first.RuntimeBundle.RootArguments[0]);
        Assert.Equal(first.Sha256, ComputeSha(first.CanonicalBytes));

        var planType = typeof(CounterfactualMethodPlan<ConcreteMemory>);
        Assert.DoesNotContain(
            planType.GetMembers(BindingFlags.Public | BindingFlags.Instance),
            member => member.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase));
        Assert.Null(planType.GetProperty("RuntimeSeed", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.Null(planType.GetProperty("RuntimeCapabilities", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.Equal(
            typeof(CounterfactualRuntimeBundle<ConcreteMemory>),
            planType.GetProperty("RuntimeBundle", BindingFlags.NonPublic | BindingFlags.Instance)!.PropertyType);
    }

    private static CounterfactualMethodExecutionInput<ConcreteMemory> CreateInput(
        CounterfactualMethodRequest request,
        IResolutionServices? resolver = null,
        ProvenanceConcreteDomain? domain = null,
        IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>? memoryModel = null,
        ConcreteMemory? initialMemory = null,
        ProvenanceConcreteValue? receiver = null,
        ImmutableArray<CounterfactualFieldObservation>? observations = null,
        IPureCallModelRegistry? registry = null,
        long receiverId = 1)
    {
        domain ??= new ProvenanceConcreteDomain();
        return CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
            request,
            resolver ?? new PoisonResolver(),
            domain,
            memoryModel ?? new PoisonMemoryModel(),
            initialMemory ?? ConcreteMemory.Empty,
            receiver ?? domain.ObjectReference(receiverId, Owner),
            observations ?? CreateObservations(request),
            registry);
    }

    private static CounterfactualMethodRequest CreateRequest(
        ImmutableArray<CounterfactualInputEvidence>? arguments = null,
        MethodHandle? requiredModelTarget = null) =>
        CounterfactualMethodRequest.CreateSynthetic(
            SyntheticId,
            "root.selection.binding.v1",
            DigestA,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.binding", DigestB),
            arguments ?? ImmutableArray<CounterfactualInputEvidence>.Empty,
            "policy.counterfactual",
            new PureCallModelVersion(1, 0, 0),
            32,
            2,
            10,
            "catalog.binding",
            new PureCallModelVersion(1, 0, 0),
            requiredModelTarget,
            ImmutableArray.Create("assume.read-only"));

    private static CounterfactualMethodRequest CreateDumpRequest()
    {
        var snapshot = EvaluationEvidenceIdentity.CreateAvailable("snapshot://binding/dump/1");
        var module = EvaluationEvidenceIdentity.CreateAvailable("module://binding/mvid/1");
        return CounterfactualMethodRequest.CreateValidated(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            null,
            snapshot,
            module,
            "root.selection.binding.v1",
            DigestA,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.binding", DigestB),
            ImmutableArray<CounterfactualInputEvidence>.Empty,
            "policy.counterfactual",
            new PureCallModelVersion(1, 0, 0),
            32,
            2,
            10,
            "catalog.binding",
            new PureCallModelVersion(1, 0, 0),
            null,
            ImmutableArray.Create("assume.read-only"));
    }

    private static MethodGraphPreparationResult CreateGraph()
    {
        var rootDefinition = MethodGraphPlannerTests.RootDefinition(
            Root,
            MethodGraphPlannerTests.ExactRootBody(
                FirstField.Handle.MetadataToken,
                SecondField.Handle.MetadataToken,
                Helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = MethodGraphPlannerTests.HelperDefinition(
            Helper,
            [0x02, 0x03, 0x58, 0x2A],
            maxStack: 2);
        var resolver = MethodGraphPlannerTests.Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = MethodGraphPlannerTests.Target(Helper);
        var preparation = new MethodGraphPlanner(resolver).Prepare(Root, 10);
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        return preparation;
    }

    private static ImmutableArray<CounterfactualFieldObservation> CreateObservations(
        CounterfactualMethodRequest request) =>
        ImmutableArray.Create(
            CreateObservation(request, 0, FirstField),
            CreateObservation(request, 1, SecondField));

    private static CounterfactualFieldObservation CreateObservation(
        CounterfactualMethodRequest request,
        int ordinal,
        ResolvedField field,
        string? sourceSha256 = null,
        string? importedObjectSha256 = null)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 100 + ordinal);
        return CounterfactualFieldObservation.CreateExactInt32(
            ordinal,
            field,
            sourceSha256 ?? SourceSha256(request),
            importedObjectSha256 ?? request.Receiver.EvidenceSha256,
            checked(0x2000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            bytes);
    }

    private static string SourceSha256(CounterfactualMethodRequest request)
    {
        var source = request.EvidenceSource switch
        {
            EvaluationEvidenceSourceKind.Synthetic => request.SyntheticEvidenceId,
            EvaluationEvidenceSourceKind.DumpSnapshot => request.SnapshotIdentity.SourceId,
            _ => null,
        };
        Assert.NotNull(source);
        return ComputeSha(ImmutableArray.CreateRange(Encoding.UTF8.GetBytes(source)));
    }

    private static string ComputeSha(ImmutableArray<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes.AsSpan())).ToLowerInvariant();

    private static void AssertOrigin(
        ProvenanceConcreteDomain domain,
        ProvenanceConcreteValue value,
        int expectedIndex,
        EvaluationEvidenceStatus expectedStatus,
        string expectedSource,
        string expectedReason)
    {
        var graph = domain.CaptureLineage(value);
        var node = Assert.IsType<InputOriginLineageNode>(Assert.Single(graph.Nodes));
        Assert.Equal(ProvenanceInputKind.RequestArgument, node.Origin.Kind);
        Assert.Equal(expectedIndex, node.Origin.OriginIndex);
        Assert.Equal(expectedStatus, node.Origin.Evidence);
        Assert.Equal(expectedSource, node.Origin.SourceKey.Sha256);
        Assert.Equal(expectedReason, node.Origin.ReasonCode);
        Assert.Equal(TypeSig.Int32, node.Origin.StaticType);
    }

    private sealed class PoisonResolver : IResolutionServices
    {
        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            throw new InvalidOperationException("Binding validation must not invoke the resolver.");

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(MethodHandle contextMethod, int metadataToken) =>
            throw new InvalidOperationException("Binding validation must not invoke the resolver.");

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            throw new InvalidOperationException("Binding validation must not invoke the resolver.");
    }

    private sealed class PoisonRegistry : IPureCallModelRegistry
    {
        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target) =>
            throw new InvalidOperationException("Binding validation must not invoke the registry.");
    }

    private sealed class PoisonMemoryModel : IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>
    {
        public bool CanAllocate => throw new InvalidOperationException("Binding validation must not query memory.");

        public (ProvenanceConcreteValue objRef, ConcreteMemory mem) NewObject(
            ConcreteMemory mem,
            TypeSig type) => throw new InvalidOperationException("Binding validation must not invoke memory.");

        public (ProvenanceConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) =>
            throw new InvalidOperationException("Binding validation must not invoke memory.");

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field) => throw new InvalidOperationException("Binding validation must not invoke memory.");

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Binding validation must not invoke memory.");

        public ProvenanceConcreteValue LoadElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) =>
            throw new InvalidOperationException("Binding validation must not invoke memory.");

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Binding validation must not invoke memory.");
    }
}
