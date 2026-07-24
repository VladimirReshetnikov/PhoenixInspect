using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;
using Xunit;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>
/// Verifies W4.6 modeled-return lineage identity, atomic same-batch dependencies, validation, and fresh replay.
/// </summary>
public sealed class ProvenanceModeledCallLineageTests
{
    private const string LegacyCallArgumentFixtureId =
        "b2340a16e54f17b90f354993b8aa64d0f150092e023e31d389d28cf2159afc2b";
    private const string LegacyCallArgumentFixtureCanonicalHex =
        "0000002550686F656E6978496E73706563742E50726F76656E616E63654C696E656167652E4E" +
        "6F64650000000100000004000000020000000201020304050607081112131415161718060000" +
        "010000000C010203040506070811121314151617180600000200000000C73AC6CEFD2ED651C0" +
        "F2FDA053BC8149B33E51F6BADFA68A288D41C23069E526";
    private const string ModeledReturnFixtureId =
        "db8ef30ffd3f59dd1ca37db650b4569c3c6f8e9dc2bb04d190c04f270eab7191";
    private const string ModeledReturnFixtureCanonicalHex =
        "0000002550686F656E6978496E73706563742E50726F76656E616E63654C696E656167652E4E" +
        "6F64650000000100000006000000020000000201020304050607081112131415161718060000" +
        "010000000C01020304050607081112131415161718060000020000001277342E636F6D62696E" +
        "652D6D61726B6572730000000100000002000000030000000200000002B2340A16E54F17B90F" +
        "354993B8AA64D0F150092E023E31D389D28CF2159AFC2B0000000100000007";
    private const string ModeledGraphFixtureSha256 =
        "898045c1524ad3066be2710353a65f6eeda58408db38ef5e5f087d52f962f94b";

    private static readonly ModuleHandle Module = new(
        0x0102030405060708,
        0x1112131415161718);

    private static readonly MethodHandle Caller = new(Module, 0x06000001);
    private static readonly MethodHandle Callee = new(Module, 0x06000002);
    private static readonly DirectCallSiteIdentity CallSite = new(Caller, 12, Callee);
    private static readonly PureCallModelIdentity ModelIdentity = new(
        "w4.combine-markers",
        new PureCallModelVersion(1, 2, 3));

    /// <summary>
    /// Checks kind 6's frozen bytes and complete mixed-precision shape while proving kind 4 retains its W4.5 identity.
    /// </summary>
    [Fact]
    public void MixedModeledReturnHasFrozenCanonicalIdentityAndPreservesKindFourBytes()
    {
        var domain = new ProvenanceConcreteDomain();
        var capability = Assert.IsAssignableFrom<IPureCallModelLineageDomain<ProvenanceConcreteValue>>(domain);
        var unknown = Unknown(domain, "canonical", 0);
        var exact = domain.ConstInt32(7);

        var first = capability.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(unknown, exact));
        var repeated = capability.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(unknown, exact));
        var graph = domain.CaptureLineage(first);

        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(first));
        Assert.Equal(first, repeated);
        Assert.Equal(Root(first), Root(repeated));
        Assert.Equal(3, domain.InternedNodeCount);
        Assert.Equal(3, graph.Nodes.Length);

        var callArgument = Assert.Single(graph.Nodes.OfType<CallArgumentTransformLineageNode>());
        var modeledReturn = Assert.Single(graph.Nodes.OfType<ModeledReturnTransformLineageNode>());
        var origin = Assert.Single(graph.Nodes.OfType<InputOriginLineageNode>());

        Assert.Equal(LegacyCallArgumentFixtureId, callArgument.Id.Sha256);
        Assert.Equal(
            LegacyCallArgumentFixtureCanonicalHex,
            Convert.ToHexString(callArgument.CanonicalBytes.AsSpan()));
        Assert.Equal(CallSite, callArgument.CallSite);
        Assert.Equal(0, callArgument.ParameterIndex);
        Assert.Equal(origin.Id, callArgument.Predecessor);

        Assert.Equal(ModeledReturnFixtureId, modeledReturn.Id.Sha256);
        Assert.Equal(
            ModeledReturnFixtureCanonicalHex,
            Convert.ToHexString(modeledReturn.CanonicalBytes.AsSpan()));
        Assert.Equal(LineageNodeKind.ModeledReturnTransform, modeledReturn.Kind);
        Assert.Equal(TypeSig.Int32, modeledReturn.StaticType);
        Assert.Equal(CallSite, modeledReturn.CallSite);
        Assert.Equal(ModelIdentity, modeledReturn.ModelIdentity);
        Assert.Equal(2, modeledReturn.Arguments.Length);
        Assert.Equal(LineageOperandKind.Unknown, modeledReturn.Arguments[0].Kind);
        Assert.Equal(callArgument.Id, modeledReturn.Arguments[0].Predecessor);
        Assert.Equal(LineageOperandKind.ExactInt32, modeledReturn.Arguments[1].Kind);
        Assert.Equal(7, modeledReturn.Arguments[1].ExactInt32);
        Assert.Equal(new[] { callArgument.Id }, modeledReturn.Dependencies);
        Assert.Equal(modeledReturn.Id, graph.Root);
        Assert.Equal(ModeledGraphFixtureSha256, graph.Sha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(modeledReturn.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            modeledReturn.Id.Sha256);
    }

    /// <summary>
    /// Proves mutation of a public modeled-argument array cannot alter retained operands, node identity, graph
    /// identity, or fresh-domain replay.
    /// </summary>
    [Fact]
    public void ModeledReturnArgumentProjectionCannotMutateLineageOrReplay()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "defensive-arguments", 0);
        var result = domain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(unknown, domain.ConstInt32(7)));
        var graph = domain.CaptureLineage(result);
        var modeledReturn = Assert.Single(graph.Nodes.OfType<ModeledReturnTransformLineageNode>());
        var expectedArguments = modeledReturn.Arguments.ToArray();
        var expectedNodeId = modeledReturn.Id;
        var expectedNodeCanonical = modeledReturn.CanonicalBytes.ToArray();
        var expectedGraphSha256 = graph.Sha256;

        var visibleArguments = modeledReturn.Arguments;
        ImmutableCollectionsMarshal.AsArray(visibleArguments)![0] = LineageOperand.FromExactInt32(99);

        Assert.Equal(expectedArguments, modeledReturn.Arguments);
        Assert.Equal(expectedNodeId, modeledReturn.Id);
        Assert.Equal(expectedNodeCanonical, modeledReturn.CanonicalBytes);
        Assert.Equal(expectedGraphSha256, graph.Sha256);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayed = replayDomain.ReplayLineage(graph);
        Assert.Equal(expectedGraphSha256, replayDomain.CaptureLineage(replayed).Sha256);
    }

    /// <summary>Checks both unknown arguments receive ordered parameter transforms in the same atomic batch.</summary>
    [Fact]
    public void BothUnknownArgumentsProduceOrderedSameBatchDependencies()
    {
        var domain = new ProvenanceConcreteDomain();
        var left = Unknown(domain, "left", 0);
        var right = Unknown(domain, "right", 1, EvaluationEvidenceStatus.Unavailable);
        var nodesBefore = domain.InternedNodeCount;

        var result = domain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(left, right));
        var graph = domain.CaptureLineage(result);

        Assert.Equal(nodesBefore + 3, domain.InternedNodeCount);
        Assert.Equal(5, graph.Nodes.Length);
        var callArguments = graph.Nodes
            .OfType<CallArgumentTransformLineageNode>()
            .OrderBy(static node => node.ParameterIndex)
            .ToArray();
        var modeledReturn = Assert.Single(graph.Nodes.OfType<ModeledReturnTransformLineageNode>());

        Assert.Equal(new[] { 0, 1 }, callArguments.Select(static node => node.ParameterIndex));
        Assert.Equal(CallSite, callArguments[0].CallSite);
        Assert.Equal(CallSite, callArguments[1].CallSite);
        Assert.Equal(Root(left), callArguments[0].Predecessor);
        Assert.Equal(Root(right), callArguments[1].Predecessor);
        Assert.Equal(
            callArguments.Select(static node => node.Id),
            modeledReturn.Arguments.Select(static operand => operand.Predecessor!.Value));
        Assert.Equal(
            callArguments.Select(static node => node.Id),
            modeledReturn.Dependencies);
        Assert.All(modeledReturn.Arguments, static operand =>
            Assert.Equal(LineageOperandKind.Unknown, operand.Kind));
    }

    /// <summary>Checks every model, call-site, argument-position, and exact-payload axis participates in kind-6 identity.</summary>
    [Fact]
    public void ModeledReturnIdentityIncludesEveryStructuralAxisButNotSemanticEquality()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "axes", 0);
        var otherModule = new ModuleHandle(0x2122232425262728, 0x3132333435363738);
        var sites = new[]
        {
            CallSite,
            new DirectCallSiteIdentity(
                new MethodHandle(otherModule, Caller.MetadataToken),
                CallSite.CallIlOffset,
                new MethodHandle(otherModule, Callee.MetadataToken)),
            new DirectCallSiteIdentity(new MethodHandle(Module, 0x06000003), 12, Callee),
            new DirectCallSiteIdentity(Caller, 13, Callee),
            new DirectCallSiteIdentity(Caller, 12, new MethodHandle(Module, 0x06000004)),
        };

        var values = sites
            .Select(site => domain.CreateModeledReturnUnknown(
                site,
                ModelIdentity,
                ImmutableArray.Create(unknown, domain.ConstInt32(7))))
            .ToList();
        values.Add(domain.CreateModeledReturnUnknown(
            CallSite,
            new PureCallModelIdentity("w4.combine-markers-v2", ModelIdentity.Version),
            ImmutableArray.Create(unknown, domain.ConstInt32(7))));
        values.Add(domain.CreateModeledReturnUnknown(
            CallSite,
            new PureCallModelIdentity("w4.combine-markers", new PureCallModelVersion(1, 2, 4)),
            ImmutableArray.Create(unknown, domain.ConstInt32(7))));
        values.Add(domain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(unknown, domain.ConstInt32(8))));
        values.Add(domain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(domain.ConstInt32(7), unknown)));

        var roots = values.Select(Root).ToArray();
        Assert.Equal(roots.Length, roots.Distinct().Count());
        Assert.All(values, value => Assert.Equal(values[0], value));
        Assert.All(values, value => Assert.Equal(values[0].GetHashCode(), value.GetHashCode()));
        Assert.All(values, value => Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(value)));
    }

    /// <summary>Checks shape, grounding, and value failures leave the domain's interning store unchanged.</summary>
    [Fact]
    public void InvalidModeledReturnInputsAreRejectedBeforeAnyPartialInterning()
    {
        var shapeDomain = new ProvenanceConcreteDomain();
        var shapeUnknown = Unknown(shapeDomain, "shape", 0);
        var nodesBeforeShapeFailures = shapeDomain.InternedNodeCount;
        foreach (var invalid in new[]
        {
            default(ImmutableArray<ProvenanceConcreteValue>),
            ImmutableArray<ProvenanceConcreteValue>.Empty,
            ImmutableArray.Create(shapeUnknown),
            ImmutableArray.Create(shapeUnknown, shapeDomain.ConstInt32(1), shapeDomain.ConstInt32(2)),
        })
        {
            Assert.ThrowsAny<ArgumentException>(() => shapeDomain.CreateModeledReturnUnknown(
                CallSite,
                ModelIdentity,
                invalid));
            Assert.Equal(nodesBeforeShapeFailures, shapeDomain.InternedNodeCount);
        }

        Assert.Throws<ArgumentException>(() => shapeDomain.CreateModeledReturnUnknown(
            default,
            ModelIdentity,
            ImmutableArray.Create(shapeUnknown, shapeDomain.ConstInt32(1))));
        Assert.Throws<ArgumentException>(() => shapeDomain.CreateModeledReturnUnknown(
            CallSite,
            default,
            ImmutableArray.Create(shapeUnknown, shapeDomain.ConstInt32(1))));
        Assert.Throws<ArgumentException>(() => shapeDomain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(shapeDomain.ConstInt32(1), shapeDomain.ConstInt32(2))));
        Assert.Equal(nodesBeforeShapeFailures, shapeDomain.InternedNodeCount);

        var foreignDomain = new ProvenanceConcreteDomain();
        var foreign = Unknown(foreignDomain, "foreign", 0);
        var invalidFactories = new Func<ProvenanceConcreteDomain, ProvenanceConcreteValue>[]
        {
            static domain => domain.Top(TypeSig.Int32),
            static domain => domain.Bottom(TypeSig.Int32),
            static domain => domain.ConstInt64(1),
            static _ => null!,
            _ => foreign,
        };

        foreach (var invalidFactory in invalidFactories)
        {
            var domain = new ProvenanceConcreteDomain();
            var local = Unknown(domain, "local", 0);
            var nodesBefore = domain.InternedNodeCount;

            Assert.ThrowsAny<ArgumentException>(() => domain.CreateModeledReturnUnknown(
                CallSite,
                ModelIdentity,
                ImmutableArray.Create(local, invalidFactory(domain))));

            Assert.Equal(nodesBefore, domain.InternedNodeCount);
            Assert.Single(domain.CaptureLineage(local).Nodes);
        }
    }

    /// <summary>Checks a same-batch cycle is rejected after complete preflight and before dictionary publication.</summary>
    [Fact]
    public void SameBatchCycleIsRejectedAtomically()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "cycle", 0);
        var callArgument = CreateCallArgumentNode(CallSite, 0, Root(unknown));
        var modeledReturn = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(
                LineageOperand.FromUnknown(callArgument.Id),
                LineageOperand.FromExactInt32(1)));
        typeof(LineageNode)
            .GetField("dependencies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(modeledReturn, ImmutableArray.Create(modeledReturn.Id));
        var nodesBefore = domain.InternedNodeCount;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokeInternBatch(domain, ImmutableArray.Create<LineageNode>(callArgument, modeledReturn)));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Equal(nodesBefore, domain.InternedNodeCount);
        Assert.Single(domain.CaptureLineage(unknown).Nodes);
    }

    /// <summary>Checks relational validation rejects every unsupported modeled-return predecessor relationship.</summary>
    [Fact]
    public void ModeledGraphValidationRejectsWrongKindTypeSiteParameterOrderAndMissingDependency()
    {
        var intOrigin0 = StandaloneOrigin(TypeSig.Int32, "int-0", 0);
        var intOrigin1 = StandaloneOrigin(TypeSig.Int32, "int-1", 1);
        var booleanOrigin = StandaloneOrigin(TypeSig.Boolean, "bool", 0);
        var call0 = CreateCallArgumentNode(CallSite, 0, intOrigin0.Id);
        var call1 = CreateCallArgumentNode(CallSite, 1, intOrigin1.Id);
        var otherSite = new DirectCallSiteIdentity(Caller, 13, Callee);

        var wrongKind = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            Operands(intOrigin0.Id, 1));
        var wrongTypeCall = CreateCallArgumentNode(CallSite, 0, booleanOrigin.Id);
        var wrongType = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            Operands(wrongTypeCall.Id, 1));
        var wrongSite = CreateModeledReturnNode(
            otherSite,
            ModelIdentity,
            Operands(call0.Id, 1));
        var wrongParameter = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            Operands(call1.Id, 1));
        var swapped = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(
                LineageOperand.FromUnknown(call1.Id),
                LineageOperand.FromUnknown(call0.Id)));

        var invalidGraphs = new[]
        {
            (wrongKind.Id, ImmutableArray.Create<LineageNode>(intOrigin0, wrongKind)),
            (wrongType.Id, ImmutableArray.Create<LineageNode>(booleanOrigin, wrongTypeCall, wrongType)),
            (wrongSite.Id, ImmutableArray.Create<LineageNode>(intOrigin0, call0, wrongSite)),
            (wrongParameter.Id, ImmutableArray.Create<LineageNode>(intOrigin1, call1, wrongParameter)),
            (swapped.Id, ImmutableArray.Create<LineageNode>(intOrigin0, intOrigin1, call0, call1, swapped)),
            (wrongParameter.Id, ImmutableArray.Create<LineageNode>(wrongParameter)),
        };

        foreach (var (root, nodes) in invalidGraphs)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => CreateGraph(root, nodes));
            Assert.IsType<ArgumentException>(exception.InnerException);
        }

        var exactOnly = Assert.Throws<TargetInvocationException>(() => CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(
                LineageOperand.FromExactInt32(1),
                LineageOperand.FromExactInt32(2))));
        Assert.IsType<ArgumentException>(exactOnly.InnerException);
        var defaultModel = Assert.Throws<TargetInvocationException>(() => CreateModeledReturnNode(
            CallSite,
            default,
            Operands(call0.Id, 1)));
        Assert.IsType<ArgumentException>(defaultModel.InnerException);
    }

    /// <summary>Checks replay revalidates modeled relationships before importing any node into a fresh domain.</summary>
    [Fact]
    public void ReplayRejectsForgedModeledRelationshipBeforeMutation()
    {
        var origin = StandaloneOrigin(TypeSig.Int32, "forged", 0);
        var modeledReturn = CreateModeledReturnNode(
            CallSite,
            ModelIdentity,
            Operands(origin.Id, 1));
        var forged = ForgeGraphWithoutValidation(
            modeledReturn.Id,
            ImmutableArray.Create<LineageNode>(origin, modeledReturn));
        var replayDomain = new ProvenanceConcreteDomain();
        var retained = Unknown(replayDomain, "retained", 0);
        var nodesBefore = replayDomain.InternedNodeCount;

        Assert.Throws<ArgumentException>(() => replayDomain.ReplayLineage(forged));

        Assert.Equal(nodesBefore, replayDomain.InternedNodeCount);
        Assert.Single(replayDomain.CaptureLineage(retained).Nodes);
    }

    /// <summary>Checks same- and fresh-domain replay preserve kind 6 byte-for-byte and support later arithmetic.</summary>
    [Fact]
    public void FreshReplayPreservesModeledGraphAndSupportsContinuation()
    {
        var sourceDomain = new ProvenanceConcreteDomain();
        var source = sourceDomain.CreateModeledReturnUnknown(
            CallSite,
            ModelIdentity,
            ImmutableArray.Create(
                Unknown(sourceDomain, "replay-left", 0),
                Unknown(sourceDomain, "replay-right", 1, EvaluationEvidenceStatus.Unavailable)));
        var sourceGraph = sourceDomain.CaptureLineage(source);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayed = replayDomain.ReplayLineage(sourceGraph);
        var replayedAgain = replayDomain.ReplayLineage(sourceGraph);
        var replayGraph = replayDomain.CaptureLineage(replayed);

        Assert.Equal(source, replayed);
        Assert.Equal(replayed, replayedAgain);
        Assert.Equal(5, replayDomain.InternedNodeCount);
        Assert.Equal(sourceGraph.Root, replayGraph.Root);
        Assert.Equal(sourceGraph.Sha256, replayGraph.Sha256);
        Assert.True(sourceGraph.CanonicalBytes.AsSpan().SequenceEqual(replayGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(
            sourceGraph.Nodes.Select(static node => node.Id),
            replayGraph.Nodes.Select(static node => node.Id));

        var continued = replayDomain.ApplyBinary(BinaryOp.Mul, replayed, replayDomain.ConstInt32(2));
        var continuedGraph = replayDomain.CaptureLineage(continued);
        Assert.Equal(6, continuedGraph.Nodes.Length);
        var continuation = Assert.IsType<BinaryTransformLineageNode>(
            continuedGraph.Nodes.Single(node => node.Id == continuedGraph.Root));
        Assert.Equal(sourceGraph.Root, continuation.Left.Predecessor);
        Assert.Equal(2, continuation.Right.ExactInt32);
    }

    private static ProvenanceConcreteValue Unknown(
        ProvenanceConcreteDomain domain,
        string source,
        int index,
        EvaluationEvidenceStatus evidence = EvaluationEvidenceStatus.Partial) =>
        domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            index,
            evidence,
            ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(source)),
            evidence == EvaluationEvidenceStatus.Partial
                ? "W4.Call.Partial"
                : "W4.Call.Unavailable",
            TypeSig.Int32));

    private static LineageNodeId Root(ProvenanceConcreteValue value)
    {
        Assert.True(value.TryGetLineageRoot(out var root));
        return root;
    }

    private static InputOriginLineageNode StandaloneOrigin(TypeSig type, string source, int index)
    {
        var domain = new ProvenanceConcreteDomain();
        var value = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            index,
            EvaluationEvidenceStatus.Partial,
            ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(source)),
            "W4.Model.Partial",
            type));
        return Assert.IsType<InputOriginLineageNode>(Assert.Single(domain.CaptureLineage(value).Nodes));
    }

    private static ImmutableArray<LineageOperand> Operands(LineageNodeId unknown, int exact) =>
        ImmutableArray.Create(
            LineageOperand.FromUnknown(unknown),
            LineageOperand.FromExactInt32(exact));

    private static CallArgumentTransformLineageNode CreateCallArgumentNode(
        DirectCallSiteIdentity callSite,
        int parameterIndex,
        LineageNodeId predecessor) =>
        (CallArgumentTransformLineageNode)InvokeCodec(
            "CreateCallArgumentTransform",
            callSite,
            parameterIndex,
            predecessor);

    private static ModeledReturnTransformLineageNode CreateModeledReturnNode(
        DirectCallSiteIdentity callSite,
        PureCallModelIdentity modelIdentity,
        ImmutableArray<LineageOperand> arguments) =>
        (ModeledReturnTransformLineageNode)InvokeCodec(
            "CreateModeledReturnTransform",
            callSite,
            modelIdentity,
            arguments);

    private static object InvokeCodec(string methodName, params object[] arguments)
    {
        var codec = typeof(ProvenanceConcreteDomain).Assembly.GetType(
            "PhoenixInspect.Domain.Concrete.ProvenanceLineageCodec",
            throwOnError: true)!;
        var method = codec.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, arguments)!;
    }

    private static void InvokeInternBatch(
        ProvenanceConcreteDomain domain,
        ImmutableArray<LineageNode> candidates)
    {
        var method = typeof(ProvenanceConcreteDomain).GetMethod(
            "InternBatch",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        _ = method.Invoke(domain, [candidates]);
    }

    private static ProvenanceLineageGraph CreateGraph(
        LineageNodeId root,
        ImmutableArray<LineageNode> nodes)
    {
        var constructor = typeof(ProvenanceLineageGraph).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(LineageNodeId), typeof(ImmutableArray<LineageNode>)],
            modifiers: null)!;
        return (ProvenanceLineageGraph)constructor.Invoke([root, nodes]);
    }

    private static ProvenanceLineageGraph ForgeGraphWithoutValidation(
        LineageNodeId root,
        ImmutableArray<LineageNode> nodes)
    {
        var canonicalNodes = nodes
            .OrderBy(static node => node.Id.Sha256, StringComparer.Ordinal)
            .ToImmutableArray();
        var codec = typeof(ProvenanceConcreteDomain).Assembly.GetType(
            "PhoenixInspect.Domain.Concrete.ProvenanceLineageCodec",
            throwOnError: true)!;
        var encode = codec.GetMethod("EncodeGraph", BindingFlags.Static | BindingFlags.NonPublic)!;
        var canonicalBytes = (ImmutableArray<byte>)encode.Invoke(null, [root, canonicalNodes])!;
        var graph = (ProvenanceLineageGraph)RuntimeHelpers.GetUninitializedObject(typeof(ProvenanceLineageGraph));
        var fields = typeof(ProvenanceLineageGraph).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        fields.Single(static field => field.Name == "nodesById").SetValue(
            graph,
            canonicalNodes.ToDictionary(static node => node.Id));
        fields.Single(static field => field.Name == "<Root>k__BackingField").SetValue(graph, root);
        fields.Single(static field => field.Name == "nodes").SetValue(graph, canonicalNodes);
        fields.Single(static field => field.Name == "canonicalBytes").SetValue(graph, canonicalBytes);
        fields.Single(static field => field.Name == "<Sha256>k__BackingField").SetValue(
            graph,
            Convert.ToHexString(SHA256.HashData(canonicalBytes.AsSpan())).ToLowerInvariant());
        return graph;
    }
}
