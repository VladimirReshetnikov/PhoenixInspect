using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;
using Xunit;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>
/// Verifies W4.5 interpreted-call lineage identity, atomic batch transformation, replay, and semantic-axis isolation.
/// </summary>
public sealed class ProvenanceCallLineageTests
{
    private const string CallArgumentFixtureId =
        "b2340a16e54f17b90f354993b8aa64d0f150092e023e31d389d28cf2159afc2b";
    private const string CallArgumentFixtureCanonicalHex =
        "0000002550686F656E6978496E73706563742E50726F76656E616E63654C696E656167652E4E" +
        "6F64650000000100000004000000020000000201020304050607081112131415161718060000" +
        "010000000C010203040506070811121314151617180600000200000000C73AC6CEFD2ED651C0" +
        "F2FDA053BC8149B33E51F6BADFA68A288D41C23069E526";
    private const string ReturnFixtureId =
        "3c0bf04c2f8fbedea51808af44ee7225b209746f9edaca6e275cd51f75dc8552";
    private const string ReturnFixtureCanonicalHex =
        "0000002550686F656E6978496E73706563742E50726F76656E616E63654C696E656167652E4E" +
        "6F64650000000100000005000000020000000201020304050607081112131415161718060000" +
        "010000000C01020304050607081112131415161718060000020BCBE549FABA07B76A133FD15F" +
        "2A9C9434FC9FE6493D815E02C929E752901EAD";
    private const string CallGraphFixtureSha256 =
        "ce12ba04fa64bbbca01bdd6f38564efd4ec2fd171d6ae3b02efeefcc0eb564fc";

    private static readonly ModuleHandle Module = new(
        0x0102030405060708,
        0x1112131415161718);

    private static readonly MethodHandle Caller = new(Module, 0x06000001);
    private static readonly MethodHandle Callee = new(Module, 0x06000002);
    private static readonly DirectCallSiteIdentity CallSite = new(Caller, 12, Callee);

    /// <summary>Checks the append-only call/return kinds, frozen bytes, complete shape, and canonical interning.</summary>
    [Fact]
    public void CallAndReturnTransformsHaveFrozenCanonicalIdentityAndCompleteShape()
    {
        var domain = new ProvenanceConcreteDomain();
        var capability = Assert.IsAssignableFrom<IInterpretedCallLineageDomain<ProvenanceConcreteValue>>(domain);
        var input = Unknown(domain, "canonical", 0);
        var exact = domain.ConstInt32(7);
        Assert.True(input.TryGetLineageRoot(out var inputRoot));

        var firstArguments = capability.TransformInterpretedCallArguments(
            CallSite,
            ImmutableArray.Create(input, exact));
        var secondArguments = capability.TransformInterpretedCallArguments(
            CallSite,
            ImmutableArray.Create(input, exact));
        Assert.True(firstArguments[0].TryGetLineageRoot(out var argumentRoot));
        Assert.True(secondArguments[0].TryGetLineageRoot(out var repeatedArgumentRoot));
        Assert.Equal(argumentRoot, repeatedArgumentRoot);
        Assert.Same(firstArguments[1].SemanticValue, secondArguments[1].SemanticValue);

        var sum = domain.ApplyBinary(BinaryOp.Add, firstArguments[0], firstArguments[1]);
        Assert.True(sum.TryGetLineageRoot(out var sumRoot));
        var returned = capability.TransformInterpretedReturn(CallSite, sum);
        var returnedAgain = capability.TransformInterpretedReturn(CallSite, sum);
        Assert.True(returned.TryGetLineageRoot(out var returnRoot));
        Assert.True(returnedAgain.TryGetLineageRoot(out var repeatedReturnRoot));
        Assert.Equal(returnRoot, repeatedReturnRoot);

        var graph = domain.CaptureLineage(returned);
        Assert.Equal(4, graph.Nodes.Length);
        Assert.Equal(4, domain.InternedNodeCount);
        var argument = Assert.IsType<CallArgumentTransformLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.CallArgumentTransform));
        var interpretedReturn = Assert.IsType<InterpretedReturnTransformLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.InterpretedReturnTransform));

        Assert.Equal(CallArgumentFixtureId, argument.Id.Sha256);
        Assert.Equal(CallArgumentFixtureCanonicalHex, Convert.ToHexString(argument.CanonicalBytes.AsSpan()));
        Assert.Equal(LineageNodeKind.CallArgumentTransform, argument.Kind);
        Assert.Equal(TypeSig.Int32, argument.StaticType);
        Assert.Equal(CallSite, argument.CallSite);
        Assert.Equal(0, argument.ParameterIndex);
        Assert.Equal(inputRoot, argument.Predecessor);
        Assert.Equal(new[] { inputRoot }, argument.Dependencies);

        Assert.Equal(ReturnFixtureId, interpretedReturn.Id.Sha256);
        Assert.Equal(ReturnFixtureCanonicalHex, Convert.ToHexString(interpretedReturn.CanonicalBytes.AsSpan()));
        Assert.Equal(LineageNodeKind.InterpretedReturnTransform, interpretedReturn.Kind);
        Assert.Equal(TypeSig.Int32, interpretedReturn.StaticType);
        Assert.Equal(CallSite, interpretedReturn.CallSite);
        Assert.Equal(Callee, interpretedReturn.Callee);
        Assert.Equal(sumRoot, interpretedReturn.Predecessor);
        Assert.Equal(new[] { sumRoot }, interpretedReturn.Dependencies);
        Assert.Equal(returnRoot, graph.Root);
        Assert.Equal(CallGraphFixtureSha256, graph.Sha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(argument.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            argument.Id.Sha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(interpretedReturn.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            interpretedReturn.Id.Sha256);
    }

    /// <summary>Checks module, caller, offset, callee, parameter, and boundary kind remain separate identity axes.</summary>
    [Fact]
    public void CallBoundaryIdentityIncludesEveryStructuralAxisButNotSemanticEquality()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "axis", 0);
        var otherCaller = new MethodHandle(Module, 0x06000003);
        var otherCallee = new MethodHandle(Module, 0x06000004);
        var otherModule = new ModuleHandle(0x2122232425262728, 0x3132333435363738);
        var sites = new[]
        {
            CallSite,
            new DirectCallSiteIdentity(
                new MethodHandle(otherModule, Caller.MetadataToken),
                12,
                new MethodHandle(otherModule, Callee.MetadataToken)),
            new DirectCallSiteIdentity(otherCaller, 12, Callee),
            new DirectCallSiteIdentity(Caller, 13, Callee),
            new DirectCallSiteIdentity(Caller, 12, otherCallee),
        };

        var values = sites
            .Select(site => domain.TransformInterpretedCallArguments(
                site,
                ImmutableArray.Create(unknown, domain.ConstInt32(1)))[0])
            .ToList();
        values.Add(domain.TransformInterpretedCallArguments(
            CallSite,
            ImmutableArray.Create(domain.ConstInt32(1), unknown))[1]);
        values.Add(domain.TransformInterpretedReturn(CallSite, unknown));

        var roots = values.Select(Root).ToArray();
        Assert.Equal(roots.Length, roots.Distinct().Count());
        Assert.All(values, value => Assert.Equal(values[0], value));
        Assert.All(values, value => Assert.Equal(values[0].GetHashCode(), value.GetHashCode()));
        Assert.All(values, value => Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(value)));
    }

    /// <summary>Checks exact arguments and returns retain their objects and allocate no explanatory node.</summary>
    [Fact]
    public void ExactCallArgumentsAndReturnPassThroughWithoutLineage()
    {
        var domain = new ProvenanceConcreteDomain();
        var first = domain.ConstInt32(10);
        var second = domain.ConstInt32(20);
        var arguments = ImmutableArray.Create(first, second);

        var transformed = domain.TransformInterpretedCallArguments(CallSite, arguments);
        var returned = domain.TransformInterpretedReturn(CallSite, first);

        Assert.Same(first, transformed[0]);
        Assert.Same(second, transformed[1]);
        Assert.Same(first, returned);
        Assert.Equal(0, domain.InternedNodeCount);
        Assert.False(first.TryGetLineageRoot(out _));
        Assert.False(second.TryGetLineageRoot(out _));
    }

    /// <summary>Checks vector shape and every malformed/foreign value reject before any partial batch interning.</summary>
    [Fact]
    public void InvalidCallArgumentBatchesAreRejectedAtomically()
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
            Assert.ThrowsAny<ArgumentException>(
                () => shapeDomain.TransformInterpretedCallArguments(CallSite, invalid));
            Assert.Equal(nodesBeforeShapeFailures, shapeDomain.InternedNodeCount);
        }

        Assert.Throws<ArgumentException>(() => shapeDomain.TransformInterpretedCallArguments(
            default,
            ImmutableArray.Create(shapeUnknown, shapeDomain.ConstInt32(1))));
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

            Assert.ThrowsAny<ArgumentException>(() => domain.TransformInterpretedCallArguments(
                CallSite,
                ImmutableArray.Create(local, invalidFactory(domain))));

            Assert.Equal(nodesBefore, domain.InternedNodeCount);
            Assert.Single(domain.CaptureLineage(local).Nodes);
        }
    }

    /// <summary>Checks malformed and foreign return values reject without adding a return-boundary node.</summary>
    [Fact]
    public void InvalidInterpretedReturnsAreRejectedAtomically()
    {
        var shapeDomain = new ProvenanceConcreteDomain();
        var shapeUnknown = Unknown(shapeDomain, "return-shape", 0);
        var nodesBeforeShapeFailure = shapeDomain.InternedNodeCount;
        Assert.Throws<ArgumentException>(
            () => shapeDomain.TransformInterpretedReturn(default, shapeUnknown));
        Assert.Equal(nodesBeforeShapeFailure, shapeDomain.InternedNodeCount);

        var foreignDomain = new ProvenanceConcreteDomain();
        var foreign = Unknown(foreignDomain, "foreign-return", 0);
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
            var retained = Unknown(domain, "retained", 0);
            var nodesBefore = domain.InternedNodeCount;

            Assert.ThrowsAny<ArgumentException>(
                () => domain.TransformInterpretedReturn(CallSite, invalidFactory(domain)));

            Assert.Equal(nodesBefore, domain.InternedNodeCount);
            Assert.Single(domain.CaptureLineage(retained).Nodes);
        }
    }

    /// <summary>Checks fresh replay reproduces the complete call graph and remains valid for later arithmetic.</summary>
    [Fact]
    public void FreshReplayPreservesCallGraphAndSupportsContinuation()
    {
        var sourceDomain = new ProvenanceConcreteDomain();
        var sourceArguments = sourceDomain.TransformInterpretedCallArguments(
            CallSite,
            ImmutableArray.Create(
                Unknown(sourceDomain, "left", 0),
                Unknown(sourceDomain, "right", 1, EvaluationEvidenceStatus.Unavailable)));
        var sourceSum = sourceDomain.ApplyBinary(BinaryOp.Add, sourceArguments[0], sourceArguments[1]);
        var sourceReturn = sourceDomain.TransformInterpretedReturn(CallSite, sourceSum);
        var sourceGraph = sourceDomain.CaptureLineage(sourceReturn);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayed = replayDomain.ReplayLineage(sourceGraph);
        var replayedAgain = replayDomain.ReplayLineage(sourceGraph);
        var replayGraph = replayDomain.CaptureLineage(replayed);

        Assert.Equal(sourceReturn, replayed);
        Assert.Equal(replayed, replayedAgain);
        Assert.Equal(6, replayDomain.InternedNodeCount);
        Assert.Equal(sourceGraph.Root, replayGraph.Root);
        Assert.Equal(sourceGraph.Sha256, replayGraph.Sha256);
        Assert.True(sourceGraph.CanonicalBytes.AsSpan().SequenceEqual(replayGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(
            sourceGraph.Nodes.Select(static node => node.Id),
            replayGraph.Nodes.Select(static node => node.Id));

        var continued = replayDomain.ApplyBinary(BinaryOp.Mul, replayed, replayDomain.ConstInt32(2));
        var continuedGraph = replayDomain.CaptureLineage(continued);
        Assert.Equal(7, continuedGraph.Nodes.Length);
        var continuation = Assert.IsType<BinaryTransformLineageNode>(
            continuedGraph.Nodes.Single(node => node.Id == continuedGraph.Root));
        Assert.Equal(sourceGraph.Root, continuation.Left.Predecessor);
        Assert.Equal(2, continuation.Right.ExactInt32);
    }

    /// <summary>Checks graph validation rejects wrong-type, missing, and out-of-profile call predecessors.</summary>
    [Fact]
    public void CallGraphValidationRejectsMalformedRelationshipsBeforeReplay()
    {
        var booleanOrigin = StandaloneOrigin(TypeSig.Boolean);
        var wrongTypeCall = CreateCallArgumentNode(CallSite, 0, booleanOrigin.Id);
        var wrongTypeReturn = CreateReturnNode(CallSite, booleanOrigin.Id);
        foreach (var node in new LineageNode[] { wrongTypeCall, wrongTypeReturn })
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => CreateGraph(node.Id, ImmutableArray.Create<LineageNode>(booleanOrigin, node)));
            Assert.IsType<ArgumentException>(exception.InnerException);
        }

        var int32Origin = StandaloneOrigin(TypeSig.Int32);
        var missingPredecessor = CreateCallArgumentNode(CallSite, 0, int32Origin.Id);
        var missing = Assert.Throws<TargetInvocationException>(
            () => CreateGraph(
                missingPredecessor.Id,
                ImmutableArray.Create<LineageNode>(missingPredecessor)));
        Assert.IsType<ArgumentException>(missing.InnerException);

        var invalidIndex = Assert.Throws<TargetInvocationException>(
            () => CreateCallArgumentNode(CallSite, 2, int32Origin.Id));
        Assert.IsType<ArgumentOutOfRangeException>(invalidIndex.InnerException);
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

    private static InputOriginLineageNode StandaloneOrigin(TypeSig type)
    {
        var domain = new ProvenanceConcreteDomain();
        var value = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Partial,
            ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes($"standalone-{type}")),
            "W4.Call.Partial",
            type));
        return Assert.IsType<InputOriginLineageNode>(Assert.Single(domain.CaptureLineage(value).Nodes));
    }

    private static CallArgumentTransformLineageNode CreateCallArgumentNode(
        DirectCallSiteIdentity callSite,
        int parameterIndex,
        LineageNodeId predecessor) =>
        (CallArgumentTransformLineageNode)InvokeCodec(
            "CreateCallArgumentTransform",
            callSite,
            parameterIndex,
            predecessor);

    private static InterpretedReturnTransformLineageNode CreateReturnNode(
        DirectCallSiteIdentity callSite,
        LineageNodeId predecessor) =>
        (InterpretedReturnTransformLineageNode)InvokeCodec(
            "CreateInterpretedReturnTransform",
            callSite,
            predecessor);

    private static object InvokeCodec(string methodName, params object[] arguments)
    {
        var codec = typeof(ProvenanceConcreteDomain).Assembly.GetType(
            "PhoenixInspect.Domain.Concrete.ProvenanceLineageCodec",
            throwOnError: true)!;
        var method = codec.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, arguments)!;
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
}
