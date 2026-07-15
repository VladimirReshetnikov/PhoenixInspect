using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Verifies W4.2 content-addressed lineage identity, canonicalization, graph capture, and replay.
/// </summary>
public sealed class ProvenanceLineageTests
{
    private const string NodeCanonicalDomain = "Interpreter.ProvenanceLineage.Node";
    private const string GraphCanonicalDomain = "Interpreter.ProvenanceLineage.Graph";
    private const string InputOriginFixtureId =
        "42e1b053ecba0d7ccc329bb157cbbd6eeadd27ac9a5959913f70e7649bf1771e";
    private const string InputOriginFixtureGraphSha256 =
        "1cf6434b483ba3f93299c4dc4fe404ae76e563e866f508915d812338766eb7af";
    private const string InputOriginFixtureCanonicalHex =
        "00000022496E7465727072657465722E50726F76656E616E63654C696E656167652E4E6F6465" +
        "000000010000000100000002000000020000000100000003000000011FC92498FB5974F7D5FE" +
        "86915F563E51D4B59EAC08F44D94B8B7CD063AF2D1200000001057342E496E7075742E506172" +
        "7469616C";
    private const string BinaryFixtureId =
        "d37ca8ccb4f26afccddf05cdc8e5142b14e900dca7d9312b34438b931f4fe9d5";
    private const string BinaryFixtureGraphSha256 =
        "9d7c93c47655d5215314dac3a9c19c6833446fd5dce370ae6154041dc6557459";
    private const string BinaryFixtureCanonicalHex =
        "00000022496E7465727072657465722E50726F76656E616E63654C696E656167652E4E6F6465" +
        "000000010000000200000002000000020000000100000002B7A0BC2C4E1A8471249DFC9C22A0" +
        "95B0C955876089042140933C4ABD712FCECC0000000100000007";

    /// <summary>Checks one input origin's versioned bytes, SHA identity, frozen facts, and canonical interning.</summary>
    [Fact]
    public void InputOriginHasVersionedCanonicalIdentityAndInternsEqualNodes()
    {
        var domain = new ProvenanceConcreteDomain();
        var origin = Origin(
            ProvenanceInputKind.RequestArgument,
            3,
            EvaluationEvidenceStatus.Partial,
            Source("request-alpha"),
            "W4.Input.Partial",
            TypeSig.Int32);

        var first = domain.CreateInputUnknown(origin);
        var second = domain.CreateInputUnknown(origin);
        var firstGraph = domain.CaptureLineage(first);
        var secondGraph = domain.CaptureLineage(second);

        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(second.TryGetLineageRoot(out var secondRoot));
        Assert.Equal(firstRoot, secondRoot);
        Assert.Equal(1, domain.InternedNodeCount);
        Assert.True(firstGraph.CanonicalBytes.AsSpan().SequenceEqual(secondGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(firstGraph.Sha256, secondGraph.Sha256);

        var node = Assert.IsType<InputOriginLineageNode>(Assert.Single(firstGraph.Nodes));
        Assert.Equal(firstRoot, node.Id);
        Assert.Equal(LineageNodeKind.InputOrigin, node.Kind);
        Assert.Equal(TypeSig.Int32, node.StaticType);
        Assert.Empty(node.Dependencies);
        Assert.Equal(origin, node.Origin);
        Assert.Equal(ProvenanceInputKind.RequestArgument, node.Origin.Kind);
        Assert.Equal(3, node.Origin.OriginIndex);
        Assert.Equal(EvaluationEvidenceStatus.Partial, node.Origin.Evidence);
        Assert.Equal("W4.Input.Partial", node.Origin.ReasonCode);
        Assert.Equal(Source("request-alpha"), node.Origin.SourceKey);
        Assert.Equal(InputOriginFixtureId, node.Id.Sha256);
        Assert.Equal(InputOriginFixtureCanonicalHex, Convert.ToHexString(node.CanonicalBytes.AsSpan()));
        Assert.Equal(InputOriginFixtureGraphSha256, firstGraph.Sha256);
        AssertCanonicalNode(node, LineageNodeKind.InputOrigin);
        AssertCanonicalGraph(firstGraph);
    }

    /// <summary>Checks that every identity-bearing input-origin fact changes identity and display-only names do not.</summary>
    [Fact]
    public void InputOriginIdentityIncludesEveryStructuralFactAndExcludesMetadataDisplayName()
    {
        var domain = new ProvenanceConcreteDomain();
        var baseRoot = Root(domain, Origin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Partial,
            Source("source-a"),
            "W4.Input.Partial",
            TypeSig.Int32));
        var changedRoots = new[]
        {
            Root(domain, Origin(ProvenanceInputKind.Receiver, 0, EvaluationEvidenceStatus.Partial,
                Source("source-a"), "W4.Input.Partial", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.ImportedField, 0, EvaluationEvidenceStatus.Partial,
                Source("source-a"), "W4.Input.Partial", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.RequestArgument, 1, EvaluationEvidenceStatus.Partial,
                Source("source-a"), "W4.Input.Partial", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Unavailable,
                Source("source-a"), "W4.Input.Partial", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
                Source("source-b"), "W4.Input.Partial", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
                Source("source-a"), "W4.Input.Other", TypeSig.Int32)),
            Root(domain, Origin(ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
                Source("source-a"), "W4.Input.Partial", TypeSig.Boolean)),
        };

        Assert.Equal(changedRoots.Length, changedRoots.Distinct().Count());
        Assert.DoesNotContain(baseRoot, changedRoots);

        var module = new ModuleHandle(0x0102030405060708, 0x1112131415161718);
        var renamedA = TypeSig.CreateTypeDefinition(module, 0x02000001, "Fixture.FirstName");
        var renamedB = TypeSig.CreateTypeDefinition(module, 0x02000001, "Fixture.Renamed");
        var otherRow = TypeSig.CreateTypeDefinition(module, 0x02000002, "Fixture.FirstName");
        var otherModule = TypeSig.CreateTypeDefinition(
            new ModuleHandle(module.High, module.Low + 1),
            0x02000001,
            "Fixture.FirstName");

        var renamedRootA = Root(domain, Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            Source("metadata"), "W4.Metadata.Partial", renamedA));
        var renamedRootB = Root(domain, Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            Source("metadata"), "W4.Metadata.Partial", renamedB));
        var otherRowRoot = Root(domain, Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            Source("metadata"), "W4.Metadata.Partial", otherRow));
        var otherModuleRoot = Root(domain, Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            Source("metadata"), "W4.Metadata.Partial", otherModule));

        Assert.Equal(renamedRootA, renamedRootB);
        Assert.NotEqual(renamedRootA, otherRowRoot);
        Assert.NotEqual(renamedRootA, otherModuleRoot);
    }

    /// <summary>Checks bounded canonical source, node-id, reason, ordinal, status, and type validation.</summary>
    [Fact]
    public void IdentityAndOriginInputsRejectNonCanonicalOrUnsupportedValues()
    {
        var upperDigest = new string('A', 64);
        Assert.Equal(new string('a', 64), new ProvenanceSourceKey(upperDigest).Sha256);
        Assert.Equal(new string('a', 64), new LineageNodeId(upperDigest).Sha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData("source"u8)).ToLowerInvariant(),
            ProvenanceSourceKey.Hash("source"u8).Sha256);

        foreach (var invalid in new string?[]
        {
            null,
            string.Empty,
            " ",
            new string('0', 63),
            new string('0', 65),
            new string('g', 64),
        })
        {
            Assert.Throws<ArgumentException>(() => new ProvenanceSourceKey(invalid!));
            Assert.Throws<ArgumentException>(() => new LineageNodeId(invalid!));
        }

        var source = Source("validation");
        Assert.Throws<ArgumentOutOfRangeException>(() => Origin(
            (ProvenanceInputKind)int.MaxValue, 0, EvaluationEvidenceStatus.Partial,
            source, "W4.Valid", TypeSig.Int32));
        Assert.Throws<ArgumentOutOfRangeException>(() => Origin(
            ProvenanceInputKind.RequestArgument, -1, EvaluationEvidenceStatus.Partial,
            source, "W4.Valid", TypeSig.Int32));
        Assert.Throws<ArgumentOutOfRangeException>(() => Origin(
            ProvenanceInputKind.Receiver, 1, EvaluationEvidenceStatus.Partial,
            source, "W4.Valid", TypeSig.Int32));

        foreach (var evidence in new[]
        {
            EvaluationEvidenceStatus.Exact,
            EvaluationEvidenceStatus.Conflict,
            EvaluationEvidenceStatus.Invalid,
            (EvaluationEvidenceStatus)int.MaxValue,
        })
        {
            Assert.Throws<ArgumentException>(() => Origin(
                ProvenanceInputKind.RequestArgument, 0, evidence,
                source, "W4.Valid", TypeSig.Int32));
        }

        Assert.Throws<ArgumentException>(() => Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            default, "W4.Valid", TypeSig.Int32));
        foreach (var invalidReason in new string?[]
        {
            null,
            string.Empty,
            " ",
            "W4/Invalid",
            "W4 Invalid",
            "W4\nInvalid",
            new string('R', ProvenanceInputOrigin.MaximumReasonCodeLength + 1),
        })
        {
            Assert.Throws<ArgumentException>(() => Origin(
                ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
                source, invalidReason!, TypeSig.Int32));
        }

        var maximum = Origin(
            ProvenanceInputKind.ImportedField,
            int.MaxValue,
            EvaluationEvidenceStatus.Unavailable,
            source,
            new string('R', ProvenanceInputOrigin.MaximumReasonCodeLength),
            TypeSig.CreateSzArray(TypeSig.Int32));
        Assert.Equal(int.MaxValue, maximum.OriginIndex);
        Assert.Equal(ProvenanceInputOrigin.MaximumReasonCodeLength, maximum.ReasonCode.Length);
        Assert.Throws<ArgumentNullException>(() => Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            source, "W4.Valid", null!));
        Assert.Throws<ArgumentException>(() => Origin(
            ProvenanceInputKind.RequestArgument, 0, EvaluationEvidenceStatus.Partial,
            source, "W4.Valid", TypeSig.Void));
        Assert.Throws<ArgumentException>(() => LineageOperand.FromUnknown(default));
    }

    /// <summary>Checks operand factories as a closed, immutable discriminated union.</summary>
    [Fact]
    public void LineageOperandsRetainExactlyOneCanonicalPayload()
    {
        var predecessor = new LineageNodeId(new string('1', 64));
        var exact = LineageOperand.FromExactInt32(int.MinValue);
        var unknown = LineageOperand.FromUnknown(predecessor);

        Assert.Equal(LineageOperandKind.ExactInt32, exact.Kind);
        Assert.Equal(int.MinValue, exact.ExactInt32);
        Assert.Null(exact.Predecessor);
        Assert.Equal(LineageOperandKind.Unknown, unknown.Kind);
        Assert.Null(unknown.ExactInt32);
        Assert.Equal(predecessor, unknown.Predecessor);
    }

    /// <summary>Checks binary node versioning, exact-operand embedding, dependencies, and canonical interning.</summary>
    [Fact]
    public void BinaryTransformEmbedsExactOperandAndInternsEqualNodes()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "left", 0, EvaluationEvidenceStatus.Partial);
        var first = domain.ApplyBinary(BinaryOp.Add, unknown, domain.ConstInt32(7));
        var second = domain.ApplyBinary(BinaryOp.Add, unknown, domain.ConstInt32(7));

        Assert.True(unknown.TryGetLineageRoot(out var originRoot));
        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(second.TryGetLineageRoot(out var secondRoot));
        Assert.Equal(firstRoot, secondRoot);
        Assert.Equal(2, domain.InternedNodeCount);

        var graph = domain.CaptureLineage(first);
        Assert.Equal(2, graph.Nodes.Length);
        Assert.True(graph.TryGetNode(firstRoot, out var rawNode));
        var node = Assert.IsType<BinaryTransformLineageNode>(rawNode);
        Assert.Equal(LineageNodeKind.BinaryTransform, node.Kind);
        Assert.Equal(BinaryOp.Add, node.Operation);
        Assert.Equal(TypeSig.Int32, node.StaticType);
        Assert.Equal(LineageOperandKind.Unknown, node.Left.Kind);
        Assert.Equal(originRoot, node.Left.Predecessor);
        Assert.Null(node.Left.ExactInt32);
        Assert.Equal(LineageOperandKind.ExactInt32, node.Right.Kind);
        Assert.Equal(7, node.Right.ExactInt32);
        Assert.Null(node.Right.Predecessor);
        Assert.Equal(new[] { originRoot }, node.Dependencies);
        Assert.Equal(BinaryFixtureId, node.Id.Sha256);
        Assert.Equal(BinaryFixtureCanonicalHex, Convert.ToHexString(node.CanonicalBytes.AsSpan()));
        Assert.Equal(BinaryFixtureGraphSha256, graph.Sha256);
        AssertCanonicalNode(node, LineageNodeKind.BinaryTransform);
        AssertCanonicalGraph(graph);
    }

    /// <summary>Checks that add and multiply preserve IL operand order despite commutative concrete arithmetic.</summary>
    [Theory]
    [InlineData(BinaryOp.Add)]
    [InlineData(BinaryOp.Mul)]
    public void CommutativeOperationsRetainOrderedOperandLineage(BinaryOp operation)
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "ordered", 0, EvaluationEvidenceStatus.Partial);

        var unknownLeft = domain.ApplyBinary(operation, unknown, domain.ConstInt32(9));
        var unknownRight = domain.ApplyBinary(operation, domain.ConstInt32(9), unknown);
        var leftNode = BinaryRoot(domain, unknownLeft);
        var rightNode = BinaryRoot(domain, unknownRight);

        Assert.Equal(unknownLeft, unknownRight);
        Assert.NotEqual(leftNode.Id, rightNode.Id);
        Assert.Equal(LineageOperandKind.Unknown, leftNode.Left.Kind);
        Assert.Equal(LineageOperandKind.ExactInt32, leftNode.Right.Kind);
        Assert.Equal(LineageOperandKind.ExactInt32, rightNode.Left.Kind);
        Assert.Equal(LineageOperandKind.Unknown, rightNode.Right.Kind);
    }

    /// <summary>Checks operation, exact payload, and unknown predecessor participation in transform identity.</summary>
    [Fact]
    public void BinaryTransformIdentityIncludesOperationOperandsAndTheirOrder()
    {
        var domain = new ProvenanceConcreteDomain();
        var firstUnknown = Unknown(domain, "first", 0, EvaluationEvidenceStatus.Partial);
        var secondUnknown = Unknown(domain, "second", 1, EvaluationEvidenceStatus.Unavailable);

        var identities = new[]
        {
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, firstUnknown, domain.ConstInt32(1))).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Sub, firstUnknown, domain.ConstInt32(1))).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Mul, firstUnknown, domain.ConstInt32(1))).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, firstUnknown, domain.ConstInt32(2))).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, domain.ConstInt32(1), firstUnknown)).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, secondUnknown, domain.ConstInt32(1))).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, firstUnknown, secondUnknown)).Id,
            BinaryRoot(domain, domain.ApplyBinary(BinaryOp.Add, secondUnknown, firstUnknown)).Id,
        };

        Assert.Equal(identities.Length, identities.Distinct().Count());

        var repeatedPredecessor = BinaryRoot(
            domain,
            domain.ApplyBinary(BinaryOp.Mul, firstUnknown, firstUnknown));
        Assert.Single(repeatedPredecessor.Dependencies);
        Assert.Equal(repeatedPredecessor.Left.Predecessor, repeatedPredecessor.Right.Predecessor);

        var distinctPredecessors = BinaryRoot(
            domain,
            domain.ApplyBinary(BinaryOp.Mul, firstUnknown, secondUnknown));
        Assert.Equal(2, distinctPredecessors.Dependencies.Length);
        Assert.Equal(distinctPredecessors.Left.Predecessor, distinctPredecessors.Dependencies[0]);
        Assert.Equal(distinctPredecessors.Right.Predecessor, distinctPredecessors.Dependencies[1]);
    }

    /// <summary>Checks that exact arithmetic creates neither origin nor transform nodes.</summary>
    [Fact]
    public void ExactArithmeticDoesNotAllocateLineageNodes()
    {
        var domain = new ProvenanceConcreteDomain();

        var result = domain.ApplyBinary(BinaryOp.Mul, domain.ConstInt32(6), domain.ConstInt32(7));

        Assert.True(domain.TryGetConstInt32(result, out var value));
        Assert.Equal(42, value);
        Assert.False(result.TryGetLineageRoot(out _));
        Assert.Equal(0, domain.InternedNodeCount);
    }

    /// <summary>Checks capture includes exactly the root-reachable DAG and excludes unrelated interned nodes.</summary>
    [Fact]
    public void CaptureContainsOnlyReachableNodesInIdentityOrder()
    {
        var domain = new ProvenanceConcreteDomain();
        var left = Unknown(domain, "left", 0, EvaluationEvidenceStatus.Partial);
        var right = Unknown(domain, "right", 1, EvaluationEvidenceStatus.Unavailable);
        var unrelated = Unknown(domain, "unrelated", 2, EvaluationEvidenceStatus.Partial);
        var sum = domain.ApplyBinary(BinaryOp.Add, left, right);
        var result = domain.ApplyBinary(BinaryOp.Mul, sum, domain.ConstInt32(3));

        Assert.Equal(5, domain.InternedNodeCount);
        Assert.True(unrelated.TryGetLineageRoot(out var unrelatedRoot));
        var graph = domain.CaptureLineage(result);

        Assert.Equal(4, graph.Nodes.Length);
        Assert.DoesNotContain(graph.Nodes, node => node.Id == unrelatedRoot);
        Assert.Equal(
            graph.Nodes.Select(static node => node.Id.Sha256).Order(StringComparer.Ordinal),
            graph.Nodes.Select(static node => node.Id.Sha256));
        Assert.All(graph.Nodes, node => Assert.True(graph.TryGetNode(node.Id, out var replay) && ReferenceEquals(node, replay)));
        Assert.False(graph.TryGetNode(default, out var missingDefault));
        Assert.Null(missingDefault);
        Assert.False(graph.TryGetNode(new LineageNodeId(new string('f', 64)), out var missing));
        Assert.Null(missing);
        AssertCanonicalGraph(graph);
    }

    /// <summary>Checks graph capture rejects values without a complete locally owned explanation.</summary>
    [Fact]
    public void CaptureRejectsExactBottomBareTopAndForeignLineage()
    {
        var domain = new ProvenanceConcreteDomain();
        var foreignDomain = new ProvenanceConcreteDomain();
        var foreign = Unknown(foreignDomain, "foreign", 0, EvaluationEvidenceStatus.Partial);

        Assert.Throws<ArgumentException>(() => domain.CaptureLineage(domain.ConstInt32(1)));
        Assert.Throws<ArgumentException>(() => domain.CaptureLineage(domain.Bottom(TypeSig.Int32)));
        Assert.Throws<ArgumentException>(() => domain.CaptureLineage(domain.Top(TypeSig.Int32)));
        Assert.Throws<ArgumentException>(() => domain.CaptureLineage(foreign));
        Assert.Throws<ArgumentException>(() => domain.GetPrecision(foreign));
        Assert.Throws<ArgumentException>(
            () => domain.ApplyBinary(BinaryOp.Add, foreign, domain.ConstInt32(1)));
        Assert.Throws<ArgumentNullException>(() => domain.CaptureLineage(null!));
        Assert.Throws<ArgumentNullException>(() => domain.ReplayLineage(null!));
    }

    /// <summary>Checks fresh-domain replay reproduces canonical graph bytes and idempotently interns every node.</summary>
    [Fact]
    public void FreshDomainReplayPreservesRootNodesBytesFingerprintAndSemanticValue()
    {
        var firstDomain = new ProvenanceConcreteDomain();
        var left = Unknown(firstDomain, "left", 0, EvaluationEvidenceStatus.Partial);
        var right = Unknown(firstDomain, "right", 1, EvaluationEvidenceStatus.Unavailable);
        var firstValue = firstDomain.ApplyBinary(
            BinaryOp.Sub,
            firstDomain.ApplyBinary(BinaryOp.Add, left, firstDomain.ConstInt32(17)),
            right);
        var firstGraph = firstDomain.CaptureLineage(firstValue);

        var freshDomain = new ProvenanceConcreteDomain();
        var replayed = freshDomain.ReplayLineage(firstGraph);
        var replayedAgain = freshDomain.ReplayLineage(firstGraph);
        var replayGraph = freshDomain.CaptureLineage(replayed);

        Assert.Equal(firstValue, replayed);
        Assert.Equal(replayed, replayedAgain);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, freshDomain.GetPrecision(replayed));
        Assert.True(replayed.TryGetLineageRoot(out var replayRoot));
        Assert.Equal(firstGraph.Root, replayRoot);
        Assert.Equal(firstGraph.Nodes.Length, freshDomain.InternedNodeCount);
        Assert.Equal(firstGraph.Root, replayGraph.Root);
        Assert.True(firstGraph.CanonicalBytes.AsSpan().SequenceEqual(replayGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(firstGraph.Sha256, replayGraph.Sha256);
        Assert.Equal(
            firstGraph.Nodes.Select(static node => node.Id),
            replayGraph.Nodes.Select(static node => node.Id));
    }

    /// <summary>Checks construction history and unrelated interning do not affect fresh-object graph replay.</summary>
    [Fact]
    public void GraphReplayIsIndependentOfDomainInsertionOrder()
    {
        var firstDomain = new ProvenanceConcreteDomain();
        _ = Unknown(firstDomain, "unrelated-first", 99, EvaluationEvidenceStatus.Partial);
        var firstLeft = Unknown(firstDomain, "left", 0, EvaluationEvidenceStatus.Partial);
        var firstRight = Unknown(firstDomain, "right", 1, EvaluationEvidenceStatus.Unavailable);
        var firstResult = firstDomain.ApplyBinary(BinaryOp.Add, firstLeft, firstRight);

        var secondDomain = new ProvenanceConcreteDomain();
        var secondRight = Unknown(secondDomain, "right", 1, EvaluationEvidenceStatus.Unavailable);
        _ = Unknown(secondDomain, "unrelated-second", 98, EvaluationEvidenceStatus.Unavailable);
        var secondLeft = Unknown(secondDomain, "left", 0, EvaluationEvidenceStatus.Partial);
        var secondResult = secondDomain.ApplyBinary(BinaryOp.Add, secondLeft, secondRight);

        var firstGraph = firstDomain.CaptureLineage(firstResult);
        var secondGraph = secondDomain.CaptureLineage(secondResult);

        Assert.Equal(firstGraph.Root, secondGraph.Root);
        Assert.True(firstGraph.CanonicalBytes.AsSpan().SequenceEqual(secondGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(firstGraph.Sha256, secondGraph.Sha256);
        Assert.Equal(
            firstGraph.Nodes.Select(static node => node.Id),
            secondGraph.Nodes.Select(static node => node.Id));
    }

    /// <summary>Checks captured graphs and nodes are frozen against mutable copies and later domain activity.</summary>
    [Fact]
    public void CapturedGraphIsDefensivelyImmutable()
    {
        var domain = new ProvenanceConcreteDomain();
        var unknown = Unknown(domain, "frozen", 0, EvaluationEvidenceStatus.Partial);
        var result = domain.ApplyBinary(BinaryOp.Add, unknown, domain.ConstInt32(5));
        var graph = domain.CaptureLineage(result);
        var graphBytesBefore = graph.CanonicalBytes.ToArray();
        var nodeBytesBefore = graph.Nodes.ToDictionary(
            static node => node.Id,
            static node => node.CanonicalBytes.ToArray());
        var nodesBefore = graph.Nodes;

        var mutableGraphCopy = graph.CanonicalBytes.ToArray();
        mutableGraphCopy[0] ^= 0xff;
        var mutableNodeCopy = graph.Nodes[0].CanonicalBytes.ToArray();
        mutableNodeCopy[0] ^= 0xff;
        var mutableNodesCopy = graph.Nodes.ToArray();
        Array.Reverse(mutableNodesCopy);
        _ = Unknown(domain, "created-later", 7, EvaluationEvidenceStatus.Unavailable);

        Assert.True(graphBytesBefore.AsSpan().SequenceEqual(graph.CanonicalBytes.AsSpan()));
        Assert.Equal(nodesBefore, graph.Nodes);
        Assert.NotEqual(mutableGraphCopy, graph.CanonicalBytes.ToArray());
        Assert.NotEqual(mutableNodeCopy, graph.Nodes[0].CanonicalBytes.ToArray());
        Assert.True(graphBytesBefore.AsSpan().SequenceEqual(graph.CanonicalBytes.AsSpan()));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(graph.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            graph.Sha256);
        foreach (var node in graph.Nodes)
        {
            Assert.True(nodeBytesBefore[node.Id].AsSpan().SequenceEqual(node.CanonicalBytes.AsSpan()));
        }
    }

    private static ProvenanceInputOrigin Origin(
        ProvenanceInputKind kind,
        int originIndex,
        EvaluationEvidenceStatus evidence,
        ProvenanceSourceKey source,
        string reason,
        TypeSig type) =>
        new(kind, originIndex, evidence, source, reason, type);

    private static ProvenanceSourceKey Source(string identity) =>
        ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(identity));

    private static ProvenanceConcreteValue Unknown(
        ProvenanceConcreteDomain domain,
        string source,
        int originIndex,
        EvaluationEvidenceStatus evidence) =>
        domain.CreateInputUnknown(Origin(
            ProvenanceInputKind.RequestArgument,
            originIndex,
            evidence,
            Source(source),
            evidence == EvaluationEvidenceStatus.Partial
                ? "W4.Input.Partial"
                : "W4.Input.Unavailable",
            TypeSig.Int32));

    private static LineageNodeId Root(
        ProvenanceConcreteDomain domain,
        ProvenanceInputOrigin origin)
    {
        var value = domain.CreateInputUnknown(origin);
        Assert.True(value.TryGetLineageRoot(out var root));
        return root;
    }

    private static BinaryTransformLineageNode BinaryRoot(
        ProvenanceConcreteDomain domain,
        ProvenanceConcreteValue value)
    {
        var graph = domain.CaptureLineage(value);
        Assert.True(graph.TryGetNode(graph.Root, out var node));
        return Assert.IsType<BinaryTransformLineageNode>(node);
    }

    private static void AssertCanonicalNode(LineageNode node, LineageNodeKind expectedKind)
    {
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(node.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            node.Id.Sha256);
        var bytes = node.CanonicalBytes.AsSpan();
        var offset = 0;
        Assert.Equal(NodeCanonicalDomain, ReadLengthPrefixedUtf8(bytes, ref offset));
        Assert.Equal(1, ReadInt32(bytes, ref offset));
        Assert.Equal((int)expectedKind, ReadInt32(bytes, ref offset));
        Assert.True(offset < bytes.Length);
    }

    private static void AssertCanonicalGraph(ProvenanceLineageGraph graph)
    {
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(graph.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            graph.Sha256);
        var bytes = graph.CanonicalBytes.AsSpan();
        var offset = 0;
        Assert.Equal(GraphCanonicalDomain, ReadLengthPrefixedUtf8(bytes, ref offset));
        Assert.Equal(1, ReadInt32(bytes, ref offset));
        Assert.True(offset < bytes.Length);
    }

    private static string ReadLengthPrefixedUtf8(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var length = ReadInt32(bytes, ref offset);
        Assert.InRange(length, 0, bytes.Length - offset);
        var value = Encoding.UTF8.GetString(bytes.Slice(offset, length));
        offset += length;
        return value;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset)
    {
        Assert.True(offset <= bytes.Length - sizeof(int));
        var value = BinaryPrimitives.ReadInt32BigEndian(bytes[offset..]);
        offset += sizeof(int);
        return value;
    }
}
