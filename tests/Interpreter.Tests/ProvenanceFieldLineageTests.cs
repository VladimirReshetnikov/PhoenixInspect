using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Verifies W4.3 imported-field lineage identity, relational graph validation, atomic interning, and replay.
/// </summary>
public sealed class ProvenanceFieldLineageTests
{
    private const string FieldOriginId =
        "f5c682cece206c3e580baeb0d46d8313ed7f015252f1eb93f4e6848d28b10d0e";
    private const string FieldOriginCanonicalHex =
        "00000022496E7465727072657465722E50726F76656E616E63654C696E656167652E4E6F6465" +
        "0000000100000001000000020000000200000003000000070000000169F04AC5779F4479EA84" +
        "5DF67D34BD3AF6397778C6CFB0E8E42E30705E5AF1510000001057342E4669656C642E506172" +
        "7469616C";
    private const string FieldTransformId =
        "778f4e6cb484d110bd6b5dce30c8cb20b3af90c427cf95b49da1315052b43101";
    private const string FieldTransformCanonicalHex =
        "00000022496E7465727072657465722E50726F76656E616E63654C696E656167652E4E6F6465" +
        "000000010000000300000002000000024A64B622042ECA95E3A5D375898ACD17E445D1B25003" +
        "74DF3892E8D689982209E5B31046F1FF4B2440EDAE7E12F0E3610400000100000003E5B31046" +
        "F1FF4B2440EDAE7E12F0E36102000001000000020000000200000001F5C682CECE206C3E580B" +
        "AEB0D46D8313ED7F015252F1EB93F4E6848D28B10D0E";
    private const string FieldGraphSha256 =
        "36094b3c9df1daf6d7e6c210d51662600de05cd3cc1940bc7fd1a8b7aa54ddb0";

    private const string W42InputOriginId =
        "42e1b053ecba0d7ccc329bb157cbbd6eeadd27ac9a5959913f70e7649bf1771e";
    private const string W42BinaryTransformId =
        "d37ca8ccb4f26afccddf05cdc8e5142b14e900dca7d9312b34438b931f4fe9d5";

    private static readonly ModuleHandle Module = ModuleHandle.FromContentIdentity(
        ModuleContentIdentity.FromMetadata(
            new Guid("00000000-0000-0000-0000-000000000433"),
            "ProvenanceFieldLineageTests-W4.3"u8),
        43,
        86);

    private static readonly TypeSig OwnerType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.W43.Owner");

    private static readonly ResolvedField Int32Field = new(
        new FieldHandle(Module, 0x04000001),
        OwnerType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    /// <summary>Checks the exact v1 bytes, hard-coded identities, node shape, and two-node reachable graph.</summary>
    [Fact]
    public void FieldLoadTransformHasFrozenCanonicalIdentityAndCompleteShape()
    {
        var domain = new ProvenanceConcreteDomain();
        var evidence = FixtureEvidence();
        var result = domain.CreateFieldLoadUnknown(domain.ObjectReference(41, OwnerType), evidence);
        var graph = domain.CaptureLineage(result);

        Assert.Equal(2, domain.InternedNodeCount);
        Assert.Equal(2, graph.Nodes.Length);
        Assert.True(result.TryGetLineageRoot(out var root));
        Assert.Equal(graph.Root, root);

        var fieldLoad = Assert.IsType<FieldLoadTransformLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.FieldLoadTransform));
        var origin = Assert.IsType<InputOriginLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.InputOrigin));

        Assert.Equal(FieldTransformId, fieldLoad.Id.Sha256);
        Assert.Equal(FieldTransformCanonicalHex, Convert.ToHexString(fieldLoad.CanonicalBytes.AsSpan()));
        Assert.Equal(FieldOriginId, origin.Id.Sha256);
        Assert.Equal(FieldOriginCanonicalHex, Convert.ToHexString(origin.CanonicalBytes.AsSpan()));
        Assert.Equal(FieldGraphSha256, graph.Sha256);
        Assert.Equal(LineageNodeKind.FieldLoadTransform, fieldLoad.Kind);
        Assert.Equal(TypeSig.Int32, fieldLoad.StaticType);
        Assert.Equal(new ImportedReceiverKey(evidence.ImportedObjectSha256), fieldLoad.Receiver);
        Assert.Equal(Int32Field, fieldLoad.Field);
        Assert.Equal(origin.Id, fieldLoad.InputOrigin);
        Assert.Equal(new[] { origin.Id }, fieldLoad.Dependencies);
        Assert.Equal(ProvenanceInputKind.ImportedField, origin.Origin.Kind);
        Assert.Equal(evidence.DependencyOrdinal, origin.Origin.OriginIndex);
        Assert.Equal(evidence.EvidenceStatus, origin.Origin.Evidence);
        Assert.Equal(evidence.ReasonCode, origin.Origin.ReasonCode);
        Assert.Equal(new ProvenanceSourceKey(evidence.Sha256), origin.Origin.SourceKey);
        Assert.Equal(TypeSig.Int32, origin.StaticType);
        Assert.Empty(origin.Dependencies);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(fieldLoad.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            fieldLoad.Id.Sha256);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(graph.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            graph.Sha256);
    }

    /// <summary>Checks appending the field kind did not alter either hard-coded W4.2 node identity.</summary>
    [Fact]
    public void W42InputAndBinaryCanonicalIdentitiesRemainUnchanged()
    {
        var domain = new ProvenanceConcreteDomain();
        var input = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            3,
            EvaluationEvidenceStatus.Partial,
            Source("request-alpha"),
            "W4.Input.Partial",
            TypeSig.Int32));
        Assert.True(input.TryGetLineageRoot(out var inputRoot));
        Assert.Equal(W42InputOriginId, inputRoot.Sha256);

        var binaryDomain = new ProvenanceConcreteDomain();
        var unknown = binaryDomain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Partial,
            Source("left"),
            "W4.Input.Partial",
            TypeSig.Int32));
        var binary = binaryDomain.ApplyBinary(BinaryOp.Add, unknown, binaryDomain.ConstInt32(7));
        Assert.True(binary.TryGetLineageRoot(out var binaryRoot));
        Assert.Equal(W42BinaryTransformId, binaryRoot.Sha256);
    }

    /// <summary>Checks numeric reference allocation, display text, and hypothetical load site cannot enter identity.</summary>
    [Fact]
    public void StableImportedEvidenceExcludesLocalReferenceAndDisplayIdentity()
    {
        var evidence = FixtureEvidence();
        var firstDomain = new ProvenanceConcreteDomain();
        var first = firstDomain.CreateFieldLoadUnknown(firstDomain.ObjectReference(1, OwnerType), evidence);
        var firstGraph = firstDomain.CaptureLineage(first);

        var renamedOwner = TypeSig.CreateTypeDefinition(Module, 0x02000001, "Renamed.Display.Only");
        var renamedField = Field(renamedOwner);
        var renamedEvidence = Evidence(field: renamedField);
        var secondDomain = new ProvenanceConcreteDomain();
        var second = secondDomain.CreateFieldLoadUnknown(secondDomain.ObjectReference(999_999, renamedOwner), renamedEvidence);
        var secondGraph = secondDomain.CaptureLineage(second);

        Assert.Equal(firstGraph.Root, secondGraph.Root);
        Assert.Equal(firstGraph.Sha256, secondGraph.Sha256);
        Assert.True(firstGraph.CanonicalBytes.AsSpan().SequenceEqual(secondGraph.CanonicalBytes.AsSpan()));
        Assert.Equal(-1, firstGraph.CanonicalBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(OwnerType.DisplayName)));
        Assert.Equal(-1, firstGraph.CanonicalBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Renamed.Display.Only")));
        Assert.DoesNotContain(
            typeof(FieldLoadTransformLineageNode).GetProperties(),
            static property => property.Name.Contains("Site", StringComparison.Ordinal));
    }

    /// <summary>Checks every receiver, field, and observation identity axis affects the resulting root.</summary>
    [Fact]
    public void FieldLoadIdentityIncludesEveryStructuralAndEvidenceAxis()
    {
        var baseline = Root(FixtureEvidence());
        var alternateModule = ModuleHandle.FromContentIdentity(
            ModuleContentIdentity.FromMetadata(
                new Guid("00000000-0000-0000-0000-000000000434"),
                "ProvenanceFieldLineageTests-W4.3-alternate"u8),
            44,
            88);
        var alternateOwner = TypeSig.CreateTypeDefinition(alternateModule, 0x02000001, "Alternate.Owner");

        var variations = new[]
        {
            Evidence(importedObjectSha256: Hash("other-owner")),
            Evidence(sourceSha256: Hash("other-source")),
            Evidence(dependencyOrdinal: 8),
            Evidence(status: EvaluationEvidenceStatus.Unavailable, observedBytes: []),
            Evidence(reasonCode: "W4.Field.Other"),
            Evidence(address: 0x0102030405060710UL),
            Evidence(observedBytes: [0x11, 0x23]),
            Evidence(field: Field(OwnerType, 0x04000002)),
            Evidence(field: Field(alternateOwner, module: alternateModule)),
            Evidence(field: new ResolvedField(
                new FieldHandle(Module, 0x04000001),
                TypeSig.CreateTypeDefinition(Module, 0x02000002, "Other.Declaring.Type"),
                TypeSig.Int32,
                false,
                false,
                false)),
        };

        Assert.All(variations, evidence => Assert.NotEqual(baseline, Root(evidence)));

        var displayOnly = TypeSig.CreateTypeDefinition(Module, 0x02000001, "Display.Changed");
        Assert.Equal(baseline, Root(Evidence(field: Field(displayOnly))));
    }

    /// <summary>Checks equal field imports intern atomically and remain equal across local receiver identities.</summary>
    [Fact]
    public void EqualFieldLoadsAtomicallyInternExactlyOneOriginAndTransform()
    {
        var domain = new ProvenanceConcreteDomain();
        var evidence = FixtureEvidence();
        var roots = new LineageNodeId[128];

        Parallel.For(0, roots.Length, index =>
        {
            var result = domain.CreateFieldLoadUnknown(
                domain.ObjectReference(index + 1, OwnerType),
                evidence);
            Assert.True(result.TryGetLineageRoot(out roots[index]));
        });

        Assert.All(roots, root => Assert.Equal(roots[0], root));
        Assert.Equal(2, domain.InternedNodeCount);
    }

    /// <summary>Checks explanation identity remains separate from lifted-flat value equality and hashing.</summary>
    [Fact]
    public void DifferentFieldRootsRemainOneSemanticTop()
    {
        var domain = new ProvenanceConcreteDomain();
        var first = domain.CreateFieldLoadUnknown(domain.ObjectReference(1, OwnerType), FixtureEvidence());
        var second = domain.CreateFieldLoadUnknown(
            domain.ObjectReference(1, OwnerType),
            Evidence(reasonCode: "W4.Field.Alternate"));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(first));
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(second));
        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(second.TryGetLineageRoot(out var secondRoot));
        Assert.NotEqual(firstRoot, secondRoot);
    }

    /// <summary>Checks arithmetic consumes the field root as an ordered predecessor without copying field facts.</summary>
    [Fact]
    public void ArithmeticOverFieldUnknownCreatesOneBinarySuccessor()
    {
        var domain = new ProvenanceConcreteDomain();
        var fieldUnknown = domain.CreateFieldLoadUnknown(domain.ObjectReference(1, OwnerType), FixtureEvidence());
        Assert.True(fieldUnknown.TryGetLineageRoot(out var fieldRoot));

        var result = domain.ApplyBinary(BinaryOp.Sub, fieldUnknown, domain.ConstInt32(9));
        var graph = domain.CaptureLineage(result);

        Assert.Equal(3, graph.Nodes.Length);
        var binary = Assert.IsType<BinaryTransformLineageNode>(
            graph.Nodes.Single(static node => node.Kind == LineageNodeKind.BinaryTransform));
        Assert.Equal(fieldRoot, binary.Left.Predecessor);
        Assert.Equal(9, binary.Right.ExactInt32);
        Assert.Equal(new[] { fieldRoot }, binary.Dependencies);
        Assert.Single(graph.Nodes, static node => node.Kind == LineageNodeKind.FieldLoadTransform);
        Assert.Single(graph.Nodes, static node => node.Kind == LineageNodeKind.InputOrigin);
    }

    /// <summary>Checks null, non-reference, non-exact, wrong-owner, and lineage-bearing receivers mutate nothing.</summary>
    [Fact]
    public void InvalidReceiversAreRejectedBeforeLineageMutation()
    {
        var evidence = FixtureEvidence();
        var otherType = TypeSig.CreateTypeDefinition(Module, 0x02000002, "Other.Owner");
        var invalidFactories = new Func<ProvenanceConcreteDomain, ProvenanceConcreteValue>[]
        {
            static domain => domain.ConstInt32(1),
            static domain => domain.ConstNull(OwnerType),
            static domain => domain.Top(OwnerType),
            static domain => domain.Bottom(OwnerType),
            domain => domain.ObjectReference(1, otherType),
        };

        foreach (var factory in invalidFactories)
        {
            var domain = new ProvenanceConcreteDomain();
            Assert.Throws<ArgumentException>(() => domain.CreateFieldLoadUnknown(factory(domain), evidence));
            Assert.Equal(0, domain.InternedNodeCount);
        }

        var foreignDomain = new ProvenanceConcreteDomain();
        var foreignUnknown = foreignDomain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.Receiver,
            0,
            EvaluationEvidenceStatus.Unavailable,
            Source("foreign-receiver"),
            "W4.Receiver.Unavailable",
            OwnerType));
        Assert.True(foreignUnknown.TryGetLineageRoot(out var foreignRoot));
        var target = new ProvenanceConcreteDomain();
        var forged = Assert.Throws<TargetInvocationException>(
            () => ForgeValueWithLineage(target.ObjectReference(1, OwnerType).SemanticValue, foreignRoot));
        Assert.IsType<ArgumentException>(forged.InnerException);
        Assert.Equal(0, target.InternedNodeCount);
        Assert.Throws<ArgumentNullException>(
            () => target.CreateFieldLoadUnknown(target.ObjectReference(1, OwnerType), null!));
        Assert.Equal(0, target.InternedNodeCount);
    }

    /// <summary>Checks unsupported field/evidence shapes cannot leave a partially interned origin behind.</summary>
    [Fact]
    public void InvalidFieldAndEvidenceShapesAreAtomic()
    {
        AssertRejectedWithoutMutation(() => Evidence(field: new ResolvedField(
            Int32Field.Handle,
            OwnerType,
            TypeSig.Int64,
            false,
            false,
            false)));
        AssertRejectedWithoutMutation(() => Evidence(field: new ResolvedField(
            Int32Field.Handle,
            OwnerType,
            TypeSig.Int32,
            true,
            false,
            false)));
        AssertRejectedWithoutMutation(() => Evidence(field: new ResolvedField(
            Int32Field.Handle,
            OwnerType,
            TypeSig.Int32,
            false,
            true,
            false)));
        AssertRejectedWithoutMutation(() => Evidence(field: new ResolvedField(
            Int32Field.Handle,
            OwnerType,
            TypeSig.Int32,
            false,
            false,
            true)));
        AssertRejectedWithoutMutation(() => Evidence(status: EvaluationEvidenceStatus.Exact));
        AssertRejectedWithoutMutation(() => Evidence(status: EvaluationEvidenceStatus.Conflict));
        AssertRejectedWithoutMutation(() => Evidence(status: EvaluationEvidenceStatus.Invalid));
    }

    /// <summary>Checks relational graph validation rejects missing, request, and wrong-type origin predecessors.</summary>
    [Fact]
    public void GraphRejectsForeignOrWrongFieldPredecessorsBeforeReplayMutation()
    {
        var receiver = new ImportedReceiverKey(Hash("imported-owner"));
        foreach (var origin in new[]
        {
            CreateStandaloneOrigin(ProvenanceInputKind.RequestArgument, TypeSig.Int32),
            CreateStandaloneOrigin(ProvenanceInputKind.ImportedField, TypeSig.Int64),
        })
        {
            var fieldNode = CreateFieldNode(receiver, Int32Field, origin.Id);
            var nodes = ImmutableArray.Create<LineageNode>(origin, fieldNode);
            var exception = Assert.Throws<TargetInvocationException>(
                () => CreateGraph(fieldNode.Id, nodes));
            Assert.IsType<ArgumentException>(exception.InnerException);
        }

        var wrongKindOrigin = CreateStandaloneOrigin(ProvenanceInputKind.RequestArgument, TypeSig.Int32);
        var wrongKindField = CreateFieldNode(receiver, Int32Field, wrongKindOrigin.Id);
        var forgedGraph = ForgeGraphWithoutValidation(
            wrongKindField.Id,
            ImmutableArray.Create<LineageNode>(wrongKindOrigin, wrongKindField));
        var replayDomain = new ProvenanceConcreteDomain();
        Assert.Throws<ArgumentException>(() => replayDomain.ReplayLineage(forgedGraph));
        Assert.Equal(0, replayDomain.InternedNodeCount);

        var validOrigin = CreateStandaloneOrigin(ProvenanceInputKind.ImportedField, TypeSig.Int32);
        var missingPredecessorField = CreateFieldNode(receiver, Int32Field, validOrigin.Id);
        var missing = Assert.Throws<TargetInvocationException>(
            () => CreateGraph(
                missingPredecessorField.Id,
                ImmutableArray.Create<LineageNode>(missingPredecessorField)));
        Assert.IsType<ArgumentException>(missing.InnerException);
    }

    /// <summary>
    /// Checks replay derives root membership from the public node sequence rather than a divergent hidden lookup map.
    /// </summary>
    [Fact]
    public void ReplayRejectsDivergentHiddenNodeIndexBeforeInterning()
    {
        var indexedRoot = CreateStandaloneOrigin(ProvenanceInputKind.RequestArgument, TypeSig.Int32);
        var listedNode = CreateStandaloneOrigin(ProvenanceInputKind.ImportedField, TypeSig.Int32);
        var forged = ForgeGraphWithoutValidation(
            indexedRoot.Id,
            ImmutableArray.Create<LineageNode>(listedNode),
            indexedNodes: ImmutableArray.Create<LineageNode>(indexedRoot));
        var replayDomain = new ProvenanceConcreteDomain();
        _ = replayDomain.CreateInputUnknown(indexedRoot.Origin);
        var nodesBefore = replayDomain.InternedNodeCount;

        Assert.Throws<ArgumentException>(() => replayDomain.ReplayLineage(forged));

        Assert.Equal(nodesBefore, replayDomain.InternedNodeCount);
        Assert.Equal(1, replayDomain.InternedNodeCount);
    }

    /// <summary>Checks replay rejects noncanonical node order and duplicate node entries before interning.</summary>
    [Fact]
    public void ReplayRejectsNoncanonicalOrderAndDuplicatesBeforeInterning()
    {
        var sourceDomain = new ProvenanceConcreteDomain();
        var value = sourceDomain.CreateFieldLoadUnknown(
            sourceDomain.ObjectReference(1, OwnerType),
            FixtureEvidence());
        var graph = sourceDomain.CaptureLineage(value);
        var reversed = graph.Nodes.Reverse().ToImmutableArray();
        var unsorted = ForgeGraphWithoutValidation(
            graph.Root,
            reversed,
            indexedNodes: reversed,
            preserveNodeOrder: true);

        var standaloneRoot = CreateStandaloneOrigin(ProvenanceInputKind.RequestArgument, TypeSig.Int32);
        var duplicateNodes = ImmutableArray.Create<LineageNode>(standaloneRoot, standaloneRoot);
        var duplicated = ForgeGraphWithoutValidation(
            standaloneRoot.Id,
            duplicateNodes,
            preserveNodeOrder: true);

        foreach (var forged in new[] { unsorted, duplicated })
        {
            var replayDomain = new ProvenanceConcreteDomain();
            Assert.Throws<ArgumentException>(() => replayDomain.ReplayLineage(forged));
            Assert.Equal(0, replayDomain.InternedNodeCount);
        }
    }

    /// <summary>Checks capture excludes unrelated nodes and remains immutable after copies and later interning.</summary>
    [Fact]
    public void FieldCaptureIsReachableOnlyAndDefensivelyImmutable()
    {
        var domain = new ProvenanceConcreteDomain();
        var selected = domain.CreateFieldLoadUnknown(domain.ObjectReference(1, OwnerType), FixtureEvidence());
        var unrelated = domain.CreateFieldLoadUnknown(
            domain.ObjectReference(2, OwnerType),
            Evidence(importedObjectSha256: Hash("unrelated-owner")));
        Assert.True(unrelated.TryGetLineageRoot(out var unrelatedRoot));
        var result = domain.ApplyBinary(BinaryOp.Add, selected, domain.ConstInt32(3));
        var graph = domain.CaptureLineage(result);
        var bytes = graph.CanonicalBytes.ToArray();
        var nodeBytes = graph.Nodes.ToDictionary(static node => node.Id, static node => node.CanonicalBytes.ToArray());

        Assert.Equal(3, graph.Nodes.Length);
        Assert.DoesNotContain(graph.Nodes, node => node.Id == unrelatedRoot);
        Assert.Equal(
            graph.Nodes.Select(static node => node.Id.Sha256).Order(StringComparer.Ordinal),
            graph.Nodes.Select(static node => node.Id.Sha256));

        var mutableGraph = graph.CanonicalBytes.ToArray();
        mutableGraph[0] ^= 0xff;
        var mutableNode = graph.Nodes[0].CanonicalBytes.ToArray();
        mutableNode[0] ^= 0xff;
        _ = domain.CreateFieldLoadUnknown(
            domain.ObjectReference(3, OwnerType),
            Evidence(importedObjectSha256: Hash("created-later")));

        Assert.True(bytes.AsSpan().SequenceEqual(graph.CanonicalBytes.AsSpan()));
        Assert.NotEqual(mutableGraph, graph.CanonicalBytes.ToArray());
        Assert.NotEqual(mutableNode, graph.Nodes[0].CanonicalBytes.ToArray());
        Assert.All(
            graph.Nodes,
            node => Assert.True(nodeBytes[node.Id].AsSpan().SequenceEqual(node.CanonicalBytes.AsSpan())));
    }

    /// <summary>Checks fresh replay preserves the field graph and remains executable by later arithmetic.</summary>
    [Fact]
    public void FreshReplayPreservesFieldGraphAndSupportsBinaryContinuation()
    {
        var sourceDomain = new ProvenanceConcreteDomain();
        var source = sourceDomain.CreateFieldLoadUnknown(
            sourceDomain.ObjectReference(77, OwnerType),
            FixtureEvidence());
        var sourceGraph = sourceDomain.CaptureLineage(source);

        var replayDomain = new ProvenanceConcreteDomain();
        var replayed = replayDomain.ReplayLineage(sourceGraph);
        var replayedAgain = replayDomain.ReplayLineage(sourceGraph);
        var replayGraph = replayDomain.CaptureLineage(replayed);

        Assert.Equal(source, replayed);
        Assert.Equal(replayed, replayedAgain);
        Assert.Equal(2, replayDomain.InternedNodeCount);
        Assert.Equal(sourceGraph.Root, replayGraph.Root);
        Assert.Equal(sourceGraph.Sha256, replayGraph.Sha256);
        Assert.True(sourceGraph.CanonicalBytes.AsSpan().SequenceEqual(replayGraph.CanonicalBytes.AsSpan()));

        var continued = replayDomain.ApplyBinary(BinaryOp.Mul, replayed, replayDomain.ConstInt32(2));
        var continuedGraph = replayDomain.CaptureLineage(continued);
        Assert.Equal(3, continuedGraph.Nodes.Length);
        var binary = Assert.IsType<BinaryTransformLineageNode>(
            continuedGraph.Nodes.Single(static node => node.Kind == LineageNodeKind.BinaryTransform));
        Assert.Equal(sourceGraph.Root, binary.Left.Predecessor);
        Assert.Equal(2, binary.Right.ExactInt32);
    }

    /// <summary>Checks the strong imported-receiver key normalizes only complete SHA-256 identities.</summary>
    [Fact]
    public void ImportedReceiverKeyRequiresCompleteCanonicalSha256()
    {
        Assert.Equal(new string('a', 64), new ImportedReceiverKey(new string('A', 64)).Sha256);
        Assert.Equal(string.Empty, default(ImportedReceiverKey).ToString());
        Assert.Throws<ArgumentException>(() => new ImportedReceiverKey(string.Empty));
        Assert.Throws<ArgumentException>(() => new ImportedReceiverKey(new string('a', 63)));
        Assert.Throws<ArgumentException>(() => new ImportedReceiverKey(new string('z', 64)));
    }

    private static FieldLoadEvidence FixtureEvidence() => Evidence();

    private static FieldLoadEvidence Evidence(
        int dependencyOrdinal = 7,
        ResolvedField? field = null,
        EvaluationEvidenceStatus status = EvaluationEvidenceStatus.Partial,
        string reasonCode = "W4.Field.Partial",
        string? sourceSha256 = null,
        string? importedObjectSha256 = null,
        ulong address = 0x0102030405060708,
        int requestedLength = 4,
        byte[]? observedBytes = null) =>
        new(
            dependencyOrdinal,
            field ?? Int32Field,
            status,
            reasonCode,
            sourceSha256 ?? Hash("field-source"),
            importedObjectSha256 ?? Hash("imported-owner"),
            address,
            requestedLength,
            observedBytes ?? [0x11, 0x22]);

    private static ResolvedField Field(
        TypeSig owner,
        int token = 0x04000001,
        ModuleHandle? module = null) =>
        new(
            new FieldHandle(module ?? Module, token),
            owner,
            TypeSig.Int32,
            false,
            false,
            false);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static ProvenanceSourceKey Source(string value) =>
        ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(value));

    private static LineageNodeId Root(FieldLoadEvidence evidence)
    {
        var domain = new ProvenanceConcreteDomain();
        var value = domain.CreateFieldLoadUnknown(
            domain.ObjectReference(1, evidence.Field.DeclaringType),
            evidence);
        Assert.True(value.TryGetLineageRoot(out var root));
        return root;
    }

    private static void AssertRejectedWithoutMutation(Func<FieldLoadEvidence> evidenceFactory)
    {
        var domain = new ProvenanceConcreteDomain();
        try
        {
            var evidence = evidenceFactory();
            Assert.Throws<ArgumentException>(
                () => domain.CreateFieldLoadUnknown(domain.ObjectReference(1, OwnerType), evidence));
        }
        catch (ArgumentException)
        {
            // Core evidence construction may reject a shape before it reaches the domain capability.
        }

        Assert.Equal(0, domain.InternedNodeCount);
    }

    private static ProvenanceConcreteValue ForgeValueWithLineage(
        ConcreteValue semantic,
        LineageNodeId root)
    {
        var constructor = typeof(ProvenanceConcreteValue).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ConcreteValue), typeof(LineageNodeId?)],
            modifiers: null)!;
        return (ProvenanceConcreteValue)constructor.Invoke([semantic, (LineageNodeId?)root]);
    }

    private static InputOriginLineageNode CreateStandaloneOrigin(
        ProvenanceInputKind kind,
        TypeSig type)
    {
        var domain = new ProvenanceConcreteDomain();
        var value = domain.CreateInputUnknown(new ProvenanceInputOrigin(
            kind,
            kind == ProvenanceInputKind.Receiver ? 0 : 1,
            EvaluationEvidenceStatus.Partial,
            Source($"standalone-{kind}-{type}"),
            "W4.Field.Partial",
            type));
        return Assert.IsType<InputOriginLineageNode>(Assert.Single(domain.CaptureLineage(value).Nodes));
    }

    private static FieldLoadTransformLineageNode CreateFieldNode(
        ImportedReceiverKey receiver,
        ResolvedField field,
        LineageNodeId inputOrigin)
    {
        var codec = typeof(ProvenanceConcreteDomain).Assembly.GetType(
            "Interpreter.Domain.Concrete.ProvenanceLineageCodec",
            throwOnError: true)!;
        var method = codec.GetMethod(
            "CreateFieldLoadTransform",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (FieldLoadTransformLineageNode)method.Invoke(null, [receiver, field, inputOrigin])!;
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
        ImmutableArray<LineageNode> nodes,
        ImmutableArray<LineageNode>? indexedNodes = null,
        bool preserveNodeOrder = false)
    {
        var canonicalNodes = preserveNodeOrder
            ? nodes
            : nodes.OrderBy(static node => node.Id.Sha256, StringComparer.Ordinal).ToImmutableArray();
        var indexed = indexedNodes ?? canonicalNodes;
        var codec = typeof(ProvenanceConcreteDomain).Assembly.GetType(
            "Interpreter.Domain.Concrete.ProvenanceLineageCodec",
            throwOnError: true)!;
        var encode = codec.GetMethod("EncodeGraph", BindingFlags.Static | BindingFlags.NonPublic)!;
        var canonicalBytes = (ImmutableArray<byte>)encode.Invoke(null, [root, canonicalNodes])!;
        var graph = (ProvenanceLineageGraph)RuntimeHelpers.GetUninitializedObject(typeof(ProvenanceLineageGraph));
        var fields = typeof(ProvenanceLineageGraph).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        fields.Single(static field => field.Name == "nodesById").SetValue(
            graph,
            indexed
                .GroupBy(static node => node.Id)
                .ToDictionary(static group => group.Key, static group => group.First()));
        fields.Single(static field => field.Name == "<Root>k__BackingField").SetValue(graph, root);
        fields.Single(static field => field.Name == "nodes").SetValue(graph, canonicalNodes);
        fields.Single(static field => field.Name == "canonicalBytes").SetValue(graph, canonicalBytes);
        fields.Single(static field => field.Name == "<Sha256>k__BackingField").SetValue(
            graph,
            Convert.ToHexString(SHA256.HashData(canonicalBytes.AsSpan())).ToLowerInvariant());
        return graph;
    }
}
