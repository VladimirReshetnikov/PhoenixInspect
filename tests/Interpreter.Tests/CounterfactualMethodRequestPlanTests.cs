using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Product.DumpDebugging;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>Exercises W4.8's canonical synthetic request and internally issued frozen product plan.</summary>
public sealed class CounterfactualMethodRequestPlanTests
{
    private const string DigestA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string DigestB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private static readonly MethodHandle Root = MethodGraphPlannerTests.Method(1);
    private static readonly MethodHandle Helper = MethodGraphPlannerTests.Method(2);
    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField Field = MethodGraphPlannerTests.Field(1, Owner);
    private static readonly ResolvedField AlternateField = MethodGraphPlannerTests.Field(2, Owner);

    /// <summary>Proves equal fresh requests and independently prepared graphs reproduce exact canonical identities.</summary>
    [Fact]
    public void SameAndFreshPreparationReplayCanonicalRequestAndPlan()
    {
        var firstRequest = CreateRequest();
        var secondRequest = CreateRequest();
        var first = CreatePlan(firstRequest, CreateGraph(adjust: false), out var firstIssuer);
        var second = CreatePlan(secondRequest, CreateGraph(adjust: false), out _);

        Assert.Equal(firstRequest.Sha256, secondRequest.Sha256);
        Assert.True(firstRequest.CanonicalBytes.AsSpan().SequenceEqual(secondRequest.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));
        Assert.Same(firstRequest, first.Request);
        Assert.Equal(Root, first.RootMethod);
        Assert.True(first.InterpretedMethods.SequenceEqual([Root, Helper]));
        Assert.True(first.Fields.SequenceEqual([Field, AlternateField]));
        Assert.Empty(first.ModeledMethods);
        var call = Assert.Single(first.CallSites);
        Assert.Equal((Root, 12, Helper, FrozenMethodCallDisposition.Interpreted),
            (call.Caller, call.IlOffset, call.TargetMethod, call.Disposition));
        Assert.Equal(2, first.RequiredLogicalDepth);
        Assert.Equal((10, 5, 5), (first.TraversalLimit, first.TraversalUsed, first.TraversalRemaining));
        Assert.Equal([0, 1, 2, 3, 4], first.TraversalCharges.Select(charge => charge.Ordinal));
        Assert.Equal(2, first.FieldObservations.Length);
        Assert.Equal(0x13579BDF, first.FieldObservations[0].ExactInt32);
        Assert.Equal(0x13579BDE, first.FieldObservations[1].ExactInt32);
        Assert.True(first.IsIssuedBy(firstIssuer));
        Assert.False(first.IsIssuedBy(new object()));
        Assert.Equal(ComputeSha(first.CanonicalBytes), first.Sha256);
        Assert.True(Contains(first.CanonicalBytes, Convert.FromHexString(firstRequest.Sha256)));
        Assert.True(Contains(
            first.CanonicalBytes,
            MethodGraphPlannerTests.ExactRootBody(
                Field.Handle.MetadataToken,
                AlternateField.Handle.MetadataToken,
                Helper.MetadataToken)));
        Assert.True(Contains(first.CanonicalBytes, first.FieldObservations[0].CanonicalBytes.AsSpan()));
    }

    /// <summary>Checks every meaningful request or graph perturbation changes canonical request or plan identity.</summary>
    [Fact]
    public void CanonicalIdentityIncludesRequestBoundsAssumptionsAndCompleteGraphBody()
    {
        var baselineRequest = CreateRequest();
        var requestVariants = new[]
        {
            CreateRequest(policyId: "policy.other"),
            CreateRequest(instructionLimit: 18),
            CreateRequest(assumptions: ["assume.other"]),
            CreateRequest(rootDigest: DigestB),
            CreateRequest(requiredModelTarget: Helper),
            CreateRequest(rootSelectionId: "gc-root://ROOT/selection#2"),
        };

        Assert.All(requestVariants, item => Assert.NotEqual(baselineRequest.Sha256, item.Sha256));
        var baseline = CreatePlan(baselineRequest, CreateGraph(adjust: false), out _);
        var changedBody = CreatePlan(baselineRequest, CreateGraph(adjust: true), out _);
        Assert.NotEqual(baseline.Sha256, changedBody.Sha256);
        Assert.NotEqual(
            baseline.RuntimeGraph.Nodes.Single(node => node.Method == Helper).Definition.Body.CodeBytes,
            changedBody.RuntimeGraph.Nodes.Single(node => node.Method == Helper).Definition.Body.CodeBytes);
    }

    /// <summary>Checks source arrays and every exposed canonical or ordered vector are defensively copied.</summary>
    [Fact]
    public void RequestAndPlanDefensivelyCopyCallerAndConsumerVectors()
    {
        var sourceAssumptions = new[] { "assume.root" };
        var request = CreateRequest(
            assumptions: ImmutableCollectionsMarshal.AsImmutableArray(sourceAssumptions));
        var plan = CreatePlan(request, CreateGraph(adjust: false), out _);
        var requestSha = request.Sha256;
        var planSha = plan.Sha256;
        sourceAssumptions[0] = "assume.mutated";

        Assert.Equal("assume.root", Assert.Single(request.Assumptions));
        var assumptions = request.Assumptions;
        ImmutableCollectionsMarshal.AsArray(assumptions)![0] = "assume.consumer";
        var requestBytes = request.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(requestBytes)![0] ^= 0xff;
        var planBytes = plan.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(planBytes)![0] ^= 0xff;
        var methods = plan.InterpretedMethods;
        ImmutableCollectionsMarshal.AsArray(methods)![0] = default;
        var fields = plan.Fields;
        ImmutableCollectionsMarshal.AsArray(fields)![0] = AlternateField;
        var calls = plan.CallSites;
        ImmutableCollectionsMarshal.AsArray(calls)![0] = null!;
        var charges = plan.TraversalCharges;
        ImmutableCollectionsMarshal.AsArray(charges)![0] = null!;
        var observations = plan.FieldObservations;
        ImmutableCollectionsMarshal.AsArray(observations)![0] = null!;
        var observedBytes = plan.FieldObservations[0].ObservedBytes;
        ImmutableCollectionsMarshal.AsArray(observedBytes)![0] ^= 0xff;
        var observationBytes = plan.FieldObservations[0].CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(observationBytes)![0] ^= 0xff;
        Assert.Equal("assume.root", Assert.Single(request.Assumptions));
        Assert.Equal(Root, plan.InterpretedMethods[0]);
        Assert.Equal(Field, plan.Fields[0]);
        Assert.NotNull(plan.CallSites[0]);
        Assert.NotNull(plan.TraversalCharges[0]);
        Assert.NotNull(plan.FieldObservations[0]);
        Assert.Equal(0xDF, plan.FieldObservations[0].ObservedBytes[0]);
        Assert.Equal(plan.FieldObservations[0].Sha256, ComputeSha(plan.FieldObservations[0].CanonicalBytes));
        Assert.Equal(requestSha, request.Sha256);
        Assert.Equal(planSha, plan.Sha256);
        Assert.Equal(requestSha, ComputeSha(request.CanonicalBytes));
        Assert.Equal(planSha, ComputeSha(plan.CanonicalBytes));
    }

    /// <summary>Rejects invalid identity, receiver, vector, bound, module, and assumption combinations.</summary>
    [Fact]
    public void InvalidRequestMatrixFailsAtConstructionWhileZeroBoundsRemainRepresentable()
    {
        Assert.Throws<ArgumentException>(() => CreateRequest(policyId: "Policy Bad"));
        Assert.Throws<ArgumentException>(() => CreateRequest(rootDigest: "bad"));
        Assert.Throws<ArgumentException>(() => CreateRequest(root: default(MethodHandle)));
        Assert.Throws<ArgumentException>(() => CreateRequest(
            receiver: CounterfactualInputEvidence.CreateExactInt32("receiver", DigestA, 0)));
        Assert.Throws<ArgumentException>(() => CreateRequest(
            arguments: default(ImmutableArray<CounterfactualInputEvidence>)));
        Assert.Throws<ArgumentException>(() => CreateRequest(arguments: [null!]));
        Assert.Throws<ArgumentException>(() => CreateRequest(arguments: [
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "other", DigestA)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(instructionLimit: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(instructionLimit: long.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(logicalDepth: 65));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(traversalLimit: 1_025));
        Assert.Throws<ArgumentException>(() => CreateRequest(assumptions: default(ImmutableArray<string>)));
        Assert.Throws<ArgumentException>(() => CreateRequest(assumptions: ["assume.same", "assume.same"]));
        Assert.Throws<ArgumentException>(() => CreateRequest(
            requiredModelTarget: new MethodHandle(new ModuleHandle(1, 2), 0x06000001)));
        Assert.Throws<ArgumentException>(() => CreateRequest(requiredModelTarget: Root));

        var zero = CreateRequest(instructionLimit: 0, logicalDepth: 0, traversalLimit: 0);
        Assert.Equal(0, zero.InstructionLimit);
        Assert.Equal(0, zero.LogicalDepthLimit);
        Assert.Equal(0, zero.TraversalLimit);
        Assert.Equal(1, zero.LineageNodeCeiling);
        Assert.Equal(EvaluationEvidenceSourceKind.Synthetic, zero.EvidenceSource);
        Assert.Equal(EvaluationIdentityAvailability.NotApplicable, zero.SnapshotIdentity.Availability);
        Assert.Equal(EvaluationIdentityAvailability.NotApplicable, zero.ModuleIdentity.Availability);
        Assert.Equal(CounterfactualInputEvidenceKind.ExactNonNullReceiver, zero.Receiver.Kind);
        Assert.DoesNotContain("null", Enum.GetNames<CounterfactualInputEvidenceKind>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Guards both immutable product values against public construction, record cloning, and runtime leakage.</summary>
    [Fact]
    public void RequestAndPlanExposeNoPublicConstructionWithSurfaceOrRuntimeSeed()
    {
        Assert.True(typeof(CounterfactualMethodRequest).IsSealed);
        Assert.True(typeof(CounterfactualMethodPlan<ConcreteMemory>).IsSealed);
        Assert.Empty(typeof(CounterfactualMethodRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(CounterfactualMethodPlan<ConcreteMemory>).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(typeof(CounterfactualMethodRequest).GetMethods(), method => method.Name.Contains("Clone", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(CounterfactualMethodPlan<ConcreteMemory>).GetMethods(), method => method.Name.Contains("Clone", StringComparison.Ordinal));
        var publicNames = typeof(CounterfactualMethodPlan<ConcreteMemory>)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(member => member.Name).ToArray();
        Assert.DoesNotContain(publicNames, name => name.Contains("Runtime", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Issuer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(CounterfactualMethodPlan<ConcreteMemory>).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(FrozenMethodGraphPlan) ||
                property.PropertyType == typeof(MethodGraphTraversalAccounting));
        Assert.Empty(typeof(CounterfactualPlanCallSite).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    /// <summary>Reserves dump identities in schema v1 while keeping public request issuance synthetic-only.</summary>
    [Fact]
    public void InternalEvidenceEnvelopeDistinguishesDumpAndRejectsInvalidSourceCombinations()
    {
        var snapshot = EvaluationEvidenceIdentity.CreateAvailable("sha256:ABCDEF0123456789/dump");
        var module = EvaluationEvidenceIdentity.CreateAvailable("mvid:11223344-5566-7788-99AA-BBCCDDEEFF00");
        var dump = CreateValidatedRequest(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            null,
            snapshot,
            module);

        Assert.Equal(EvaluationEvidenceSourceKind.DumpSnapshot, dump.EvidenceSource);
        Assert.Null(dump.SyntheticEvidenceId);
        Assert.Same(snapshot, dump.SnapshotIdentity);
        Assert.Same(module, dump.ModuleIdentity);
        Assert.NotEqual(CreateRequest().Sha256, dump.Sha256);
        Assert.DoesNotContain(
            typeof(CounterfactualMethodRequest).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Dump", StringComparison.OrdinalIgnoreCase));

        Assert.Throws<ArgumentException>(() => CreateValidatedRequest(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            null,
            snapshot,
            EvaluationEvidenceIdentity.Unavailable));
        Assert.Throws<ArgumentException>(() => CreateValidatedRequest(
            EvaluationEvidenceSourceKind.Synthetic,
            "fixture://Synthetic/ABC",
            snapshot,
            EvaluationEvidenceIdentity.NotApplicable));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateValidatedRequest(
            EvaluationEvidenceSourceKind.Artifact,
            null,
            snapshot,
            module));
    }

    /// <summary>Proves canonical enum vocabularies reject unassigned values instead of inheriting runtime ordinals.</summary>
    [Fact]
    public void CanonicalTagMapsRejectUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CounterfactualCanonicalTags.Tag((EvaluationEvidenceSourceKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CounterfactualCanonicalTags.Tag((TypeSigKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CounterfactualCanonicalTags.Tag((FrozenMethodCallDisposition)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CounterfactualCanonicalTags.Tag((MethodGraphTraversalChargeKind)999));
    }

    /// <summary>Rejects a modeled graph when the request selected interpreted-only preparation.</summary>
    [Fact]
    public void PlanIssuanceRejectsUnrequestedModeledDisposition()
    {
        var preparation = CreateModeledGraph();

        Assert.Throws<ArgumentException>(() => CreatePlan(CreateRequest(), preparation, out _));
    }

    /// <summary>Proves exact and approximate observations are canonical, derived, defensive, and content-equal.</summary>
    [Fact]
    public void FieldObservationFreezesDerivedExactAndApproximationEvidenceWithoutRawCoreExposure()
    {
        var source = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var exact = CounterfactualFieldObservation.CreateExactInt32(
            0,
            Field,
            DigestA,
            DigestB,
            0x1000,
            sizeof(int),
            source);
        var fresh = CounterfactualFieldObservation.CreateExactInt32(
            0,
            Field,
            DigestA.ToUpperInvariant(),
            DigestB.ToUpperInvariant(),
            0x1000,
            sizeof(int),
            [0x12, 0x34, 0x56, 0x78]);
        source[0] = 0xff;

        Assert.Equal(0x78563412, exact.ExactInt32);
        Assert.Null(exact.ReasonCode);
        Assert.Null(exact.ApproximationEvidenceSha256);
        Assert.True(exact.ObservedBytes.SequenceEqual(new byte[] { 0x12, 0x34, 0x56, 0x78 }));
        Assert.Equal(exact, fresh);
        Assert.Equal(exact.GetHashCode(), fresh.GetHashCode());
        Assert.False(Contains(exact.CanonicalBytes, [0x78, 0x56, 0x34, 0x12]));

        var partial = CounterfactualFieldObservation.CreateNonExactInt32(
            0,
            Field,
            EvaluationEvidenceStatus.Partial,
            "W4.Field.Partial",
            DigestA,
            DigestB,
            0x1000,
            sizeof(int),
            [0x12, 0x34]);
        Assert.Null(partial.ExactInt32);
        Assert.NotNull(partial.ApproximationEvidenceSha256);
        Assert.Equal(partial.RuntimeFieldEvidence!.Sha256, partial.ApproximationEvidenceSha256);
        Assert.DoesNotContain(
            typeof(CounterfactualFieldObservation).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(FieldLoadEvidence));
    }

    /// <summary>Rejects missing, misordered, wrong-source, and wrong-receiver observation catalogs.</summary>
    [Fact]
    public void PlanIssuanceRequiresCompleteCorrelatedFieldObservationCatalog()
    {
        var request = CreateRequest();
        var preparation = CreateGraph(adjust: false);
        var valid = CreateExactObservations(request, preparation.Plan!);

        Assert.Throws<ArgumentException>(() =>
            CreatePlan(request, preparation, out _, valid.RemoveAt(valid.Length - 1)));
        Assert.Throws<ArgumentException>(() =>
            CreatePlan(request, preparation, out _, [valid[1], valid[0]]));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            request,
            preparation,
            out _,
            valid.SetItem(0, CreateExactObservation(request, 0, Field, sourceSha256: DigestA))));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            request,
            preparation,
            out _,
            valid.SetItem(0, CreateExactObservation(request, 0, Field, importedObjectSha256: DigestA))));
    }

    /// <summary>Rejects a graph-field observation whose declaring owner differs from the rooted receiver type.</summary>
    [Fact]
    public void PlanIssuanceRejectsObservationWithForeignDeclaringOwner()
    {
        var request = CreateRequest();
        var preparation = CreateGraph(adjust: false);
        var graph = preparation.Plan!;
        var foreignOwner = TypeSig.CreateTypeDefinition(
            MethodGraphPlannerTests.Module,
            0x02000003,
            "Ignored.ForeignOwner");
        var foreignField = new ResolvedField(
            Field.Handle,
            foreignOwner,
            TypeSig.Int32,
            false,
            false,
            false);
        var forgedGraph = (FrozenMethodGraphPlan)typeof(FrozenMethodGraphPlan)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([
                graph.Root,
                graph.Nodes,
                graph.ModeledLeaves,
                ImmutableArray.Create(foreignField, AlternateField),
                graph.CallSites,
                graph.RequiredLogicalDepth,
                graph.TraversalUnitCount,
            ]);
        var observations = ImmutableArray.Create(
            CreateExactObservation(request, 0, foreignField),
            CreateExactObservation(request, 1, AlternateField));

        Assert.Throws<ArgumentException>(() => CounterfactualMethodPlan<ConcreteMemory>.Issue(
            new object(),
            request,
            forgedGraph,
            preparation.TraversalAccounting!,
            CreateRuntimeBundle(request, observations)));
    }

    private static CounterfactualMethodRequest CreateRequest(
        string policyId = "policy.counterfactual",
        long instructionLimit = 17,
        int logicalDepth = 2,
        int traversalLimit = 10,
        string rootDigest = DigestA,
        MethodHandle? root = null,
        CounterfactualInputEvidence? receiver = null,
        ImmutableArray<CounterfactualInputEvidence>? arguments = null,
        ImmutableArray<string>? assumptions = null,
        MethodHandle? requiredModelTarget = null,
        string rootSelectionId = "root.selection.v1") =>
        CounterfactualMethodRequest.CreateSynthetic(
            "fixture.synthetic.v1",
            rootSelectionId,
            rootDigest,
            root ?? Root,
            receiver ?? CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.root", DigestB),
            arguments ?? ImmutableArray<CounterfactualInputEvidence>.Empty,
            policyId,
            new PureCallModelVersion(1, 0, 0),
            instructionLimit,
            logicalDepth,
            traversalLimit,
            "catalog.empty",
            new PureCallModelVersion(1, 0, 0),
            requiredModelTarget,
            assumptions ?? ImmutableArray.Create("assume.read-only"));

    private static CounterfactualMethodRequest CreateValidatedRequest(
        EvaluationEvidenceSourceKind source,
        string? syntheticId,
        EvaluationEvidenceIdentity snapshot,
        EvaluationEvidenceIdentity module) =>
        CounterfactualMethodRequest.CreateValidated(
            source,
            syntheticId,
            snapshot,
            module,
            "gc-root://ROOT/selection#1",
            DigestA,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(
                Owner,
                "dump-object://ROOT/receiver#1",
                DigestB),
            ImmutableArray<CounterfactualInputEvidence>.Empty,
            "policy.counterfactual",
            new PureCallModelVersion(1, 0, 0),
            17,
            2,
            10,
            "catalog.empty",
            new PureCallModelVersion(1, 0, 0),
            null,
            ImmutableArray.Create("assume.read-only"));

    private static CounterfactualMethodPlan<ConcreteMemory> CreatePlan(
        CounterfactualMethodRequest request,
        MethodGraphPreparationResult preparation,
        out object issuer,
        ImmutableArray<CounterfactualFieldObservation>? fieldObservations = null)
    {
        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        issuer = new object();
        return CounterfactualMethodPlan<ConcreteMemory>.Issue(
            issuer,
            request,
            preparation.Plan!,
            preparation.TraversalAccounting!,
            CreateRuntimeBundle(
                request,
                fieldObservations ?? CreateExactObservations(request, preparation.Plan!)));
    }

    private static CounterfactualRuntimeBundle<ConcreteMemory> CreateRuntimeBundle(
        CounterfactualMethodRequest request,
        ImmutableArray<CounterfactualFieldObservation> fieldObservations)
    {
        var domain = new ProvenanceConcreteDomain();
        return CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
                request,
                CreateResolver(adjust: false),
                domain,
                new UnreachableMemoryModel(),
                ConcreteMemory.Empty,
                domain.ObjectReference(1, Owner),
                fieldObservations)
            .RuntimeBundle
            .MaterializeRootArguments();
    }

    private static MethodGraphPreparationResult CreateGraph(bool adjust)
        => new MethodGraphPlanner(CreateResolver(adjust)).Prepare(Root, 10);

    private static MethodGraphPreparationResult CreateModeledGraph()
    {
        var target = MethodGraphPlannerTests.Target(Helper);
        var model = new FixtureModel(target);
        return new MethodGraphPlanner(CreateResolver(adjust: false)).RequirePureModel(
            Root,
            Helper,
            new FixtureRegistry(model),
            10);
    }

    private static MethodGraphPlannerTests.GraphResolver CreateResolver(bool adjust)
    {
        var rootDefinition = MethodGraphPlannerTests.RootDefinition(
            Root,
            MethodGraphPlannerTests.ExactRootBody(
                Field.Handle.MetadataToken,
                AlternateField.Handle.MetadataToken,
                Helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = MethodGraphPlannerTests.HelperDefinition(
            Helper,
            adjust ? [0x02, 0x03, 0x59, 0x2A] : [0x02, 0x03, 0x58, 0x2A],
            maxStack: 2);
        var resolver = MethodGraphPlannerTests.Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(Root, Field.Handle.MetadataToken)] = Field;
        resolver.Fields[(Root, AlternateField.Handle.MetadataToken)] = AlternateField;
        resolver.Calls[(Root, Helper.MetadataToken)] = MethodGraphPlannerTests.Target(Helper);
        return resolver;
    }

    private static string ComputeSha(ImmutableArray<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes.AsSpan())).ToLowerInvariant();

    private static ImmutableArray<CounterfactualFieldObservation> CreateExactObservations(
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph) =>
        graph.Fields
            .Select((field, ordinal) => CreateExactObservation(request, ordinal, field))
            .ToImmutableArray();

    private static CounterfactualFieldObservation CreateExactObservation(
        CounterfactualMethodRequest request,
        int ordinal,
        ResolvedField field,
        string? sourceSha256 = null,
        string? importedObjectSha256 = null)
    {
        var value = ordinal == 0 ? 0x13579BDF : 0x13579BDE;
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return CounterfactualFieldObservation.CreateExactInt32(
            ordinal,
            field,
            sourceSha256 ?? RequestSourceSha256(request),
            importedObjectSha256 ?? request.Receiver.EvidenceSha256,
            checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            bytes);
    }

    private static string RequestSourceSha256(CounterfactualMethodRequest request)
    {
        var identity = request.EvidenceSource switch
        {
            EvaluationEvidenceSourceKind.Synthetic => request.SyntheticEvidenceId,
            EvaluationEvidenceSourceKind.DumpSnapshot => request.SnapshotIdentity.SourceId,
            _ => null,
        };
        Assert.NotNull(identity);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static bool Contains(ImmutableArray<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return true;
        }

        return false;
    }

    private sealed class FixtureModel(ResolvedMethodCallTarget target) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = new(
            new PureCallModelIdentity("w4.counterfactual.fixture", new PureCallModelVersion(1, 0, 0)),
            target,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation) =>
            throw new InvalidOperationException("Plan issuance must not invoke the frozen model.");
    }

    private sealed class FixtureRegistry(IPureCallModel model) : IPureCallModelRegistry
    {
        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target) =>
            PureCallModelSelectionResult.Selected(model);
    }

    private sealed class UnreachableMemoryModel : IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>
    {
        public bool CanAllocate => false;

        public (ProvenanceConcreteValue objRef, ConcreteMemory mem) NewObject(
            ConcreteMemory mem,
            TypeSig type) => throw new InvalidOperationException("Plan tests do not execute memory operations.");

        public (ProvenanceConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) =>
            throw new InvalidOperationException("Plan tests do not execute memory operations.");

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field) => throw new InvalidOperationException("Plan tests do not execute memory operations.");

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Plan tests do not execute memory operations.");

        public ProvenanceConcreteValue LoadElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) =>
            throw new InvalidOperationException("Plan tests do not execute memory operations.");

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Plan tests do not execute memory operations.");
    }

}
