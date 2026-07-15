using System.Collections.Immutable;
using System.Security.Cryptography;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>
/// Freezes the complete reachable explanation DAG for one unknown root in deterministic identity order.
/// </summary>
/// <remarks>
/// The graph contains no unrelated nodes from the domain's interning store. Its canonical bytes include the root,
/// every reachable node exactly once, and each node's already versioned canonical encoding.
/// </remarks>
public sealed class ProvenanceLineageGraph
{
    private readonly Dictionary<LineageNodeId, LineageNode> nodesById;
    private readonly ImmutableArray<LineageNode> nodes;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal ProvenanceLineageGraph(LineageNodeId root, ImmutableArray<LineageNode> nodes)
    {
        if (!root.IsValid)
        {
            throw new ArgumentException("A non-default lineage root is required.", nameof(root));
        }

        if (nodes.IsDefaultOrEmpty || nodes.Any(static node => node is null))
        {
            throw new ArgumentException("A lineage graph requires initialized, non-null nodes.", nameof(nodes));
        }

        this.nodes = Copy(nodes
            .OrderBy(static node => node.Id.Sha256, StringComparer.Ordinal)
            .ToImmutableArray());
        nodesById = new Dictionary<LineageNodeId, LineageNode>(this.nodes.Length);
        foreach (var node in this.nodes)
        {
            if (!ProvenanceLineageCodec.IsCanonical(node))
            {
                throw new ArgumentException("A lineage node does not match its canonical bytes and identity.", nameof(nodes));
            }

            if (!nodesById.TryAdd(node.Id, node))
            {
                throw new ArgumentException("A lineage graph cannot contain duplicate node identities.", nameof(nodes));
            }
        }

        if (!nodesById.ContainsKey(root))
        {
            throw new ArgumentException("The lineage graph does not contain its root node.", nameof(root));
        }

        ValidateReachabilityAndAcyclicity(root, nodesById);
        Root = root;
        canonicalBytes = Copy(ProvenanceLineageCodec.EncodeGraph(root, this.nodes));
        Sha256 = Convert.ToHexString(SHA256.HashData(canonicalBytes.AsSpan())).ToLowerInvariant();
    }

    /// <summary>Gets the content-addressed node that explains the returned unknown.</summary>
    public LineageNodeId Root { get; }

    /// <summary>
    /// Gets a defensive copy containing every reachable node exactly once, sorted by lowercase SHA-256 identity.
    /// </summary>
    public ImmutableArray<LineageNode> Nodes => Copy(nodes);

    /// <summary>Gets a defensive copy of the versioned canonical bytes of the complete reachable graph.</summary>
    public ImmutableArray<byte> CanonicalBytes => Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Looks up one node in the frozen reachable graph.</summary>
    /// <param name="id">The complete content-addressed node identity.</param>
    /// <param name="node">Receives the immutable node on success; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> exactly when the graph contains <paramref name="id"/>.</returns>
    public bool TryGetNode(LineageNodeId id, out LineageNode? node)
    {
        if (!id.IsValid)
        {
            node = null;
            return false;
        }

        return nodesById.TryGetValue(id, out node);
    }

    internal void ValidateForReplay()
    {
        if (!Root.IsValid)
        {
            throw new ArgumentException("A replay graph requires a non-default root identity.");
        }

        if (nodes.IsDefaultOrEmpty || nodes.Any(static node => node is null))
        {
            throw new ArgumentException("A replay graph requires initialized, non-null nodes.");
        }

        var replayNodesById = new Dictionary<LineageNodeId, LineageNode>(nodes.Length);
        string? previousIdentity = null;
        foreach (var node in nodes)
        {
            if (!ProvenanceLineageCodec.IsCanonical(node))
            {
                throw new ArgumentException("A replay graph contains a noncanonical node.");
            }

            if (previousIdentity is not null &&
                StringComparer.Ordinal.Compare(previousIdentity, node.Id.Sha256) >= 0)
            {
                throw new ArgumentException(
                    "A replay graph requires distinct nodes in canonical identity order.");
            }

            if (!replayNodesById.TryAdd(node.Id, node))
            {
                throw new ArgumentException("A replay graph cannot contain duplicate node identities.");
            }

            previousIdentity = node.Id.Sha256;
        }

        if (!replayNodesById.ContainsKey(Root))
        {
            throw new ArgumentException("A replay graph does not contain its root node.");
        }

        ValidateReachabilityAndAcyclicity(Root, replayNodesById);
        var replayCanonicalBytes = ProvenanceLineageCodec.EncodeGraph(Root, nodes);
        if (!replayCanonicalBytes.AsSpan().SequenceEqual(canonicalBytes.AsSpan()) ||
            !string.Equals(
                Convert.ToHexString(SHA256.HashData(replayCanonicalBytes.AsSpan())).ToLowerInvariant(),
                Sha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("A replay graph does not match its canonical bytes and fingerprint.");
        }
    }

    private static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty
            ? ImmutableArray<T>.Empty
            : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    private static void ValidateReachabilityAndAcyclicity(
        LineageNodeId root,
        IReadOnlyDictionary<LineageNodeId, LineageNode> validatedNodesById)
    {
        var colors = new Dictionary<LineageNodeId, byte>(validatedNodesById.Count);
        var stack = new Stack<(LineageNodeId Id, bool Exit)>();
        stack.Push((root, false));
        while (stack.Count > 0)
        {
            var (id, exit) = stack.Pop();
            colors.TryGetValue(id, out var color);
            if (exit)
            {
                colors[id] = 2;
                continue;
            }

            if (color == 2)
            {
                continue;
            }

            if (color == 1)
            {
                throw new ArgumentException("A lineage graph must be acyclic.");
            }

            if (!validatedNodesById.TryGetValue(id, out var node))
            {
                throw new ArgumentException("A lineage dependency is absent from the graph.");
            }

            ValidateNodeRelationships(node, validatedNodesById);

            colors[id] = 1;
            stack.Push((id, true));
            for (var index = node.Dependencies.Length - 1; index >= 0; index--)
            {
                var dependency = node.Dependencies[index];
                if (!validatedNodesById.TryGetValue(dependency, out var dependencyNode))
                {
                    throw new ArgumentException("A lineage dependency is absent from the graph.");
                }

                if ((node is BinaryTransformLineageNode or
                    CallArgumentTransformLineageNode or
                    InterpretedReturnTransformLineageNode or
                    ModeledReturnTransformLineageNode) &&
                    dependencyNode.StaticType != TypeSig.Int32)
                {
                    throw new ArgumentException(
                        "An arithmetic or direct-call predecessor must have the structural Int32 type.");
                }

                colors.TryGetValue(dependency, out var dependencyColor);
                if (dependencyColor == 1)
                {
                    throw new ArgumentException("A lineage graph must be acyclic.");
                }

                if (dependencyColor != 2)
                {
                    stack.Push((dependency, false));
                }
            }
        }

        if (colors.Count(static pair => pair.Value == 2) != validatedNodesById.Count)
        {
            throw new ArgumentException("A lineage graph cannot contain nodes unreachable from its root.");
        }
    }

    private static void ValidateNodeRelationships(
        LineageNode node,
        IReadOnlyDictionary<LineageNodeId, LineageNode> validatedNodesById)
    {
        switch (node)
        {
            case FieldLoadTransformLineageNode fieldLoad:
                ValidateFieldLoad(fieldLoad, validatedNodesById);
                break;
            case CallArgumentTransformLineageNode callArgument:
                ValidateCallBoundary(
                    callArgument,
                    callArgument.CallSite,
                    callArgument.Predecessor,
                    "call-argument",
                    validatedNodesById);
                if (callArgument.ParameterIndex is < 0 or > 1)
                {
                    throw new ArgumentException(
                        "The closed W4 call profile requires parameter index zero or one.");
                }

                break;
            case InterpretedReturnTransformLineageNode interpretedReturn:
                ValidateCallBoundary(
                    interpretedReturn,
                    interpretedReturn.CallSite,
                    interpretedReturn.Predecessor,
                    "interpreted-return",
                    validatedNodesById);
                if (interpretedReturn.Callee != interpretedReturn.CallSite.Callee)
                {
                    throw new ArgumentException(
                        "An interpreted-return transform's callee must agree with its complete call-site identity.");
                }

                break;
            case ModeledReturnTransformLineageNode modeledReturn:
                ValidateModeledReturn(modeledReturn, validatedNodesById);
                break;
        }
    }

    private static void ValidateModeledReturn(
        ModeledReturnTransformLineageNode modeledReturn,
        IReadOnlyDictionary<LineageNodeId, LineageNode> validatedNodesById)
    {
        if (modeledReturn.StaticType != TypeSig.Int32 ||
            modeledReturn.CallSite.Caller == default ||
            modeledReturn.CallSite.Callee == default ||
            modeledReturn.CallSite.CallIlOffset < 0 ||
            modeledReturn.CallSite.Caller.Module != modeledReturn.CallSite.Callee.Module ||
            modeledReturn.ModelIdentity.StableId is null)
        {
            throw new ArgumentException(
                "A W4.6 modeled-return transform requires structural Int32, one valid call site, and one model identity.");
        }

        if (modeledReturn.Arguments.IsDefault ||
            modeledReturn.Arguments.Length != 2 ||
            modeledReturn.Arguments.Any(static argument => argument is null || !argument.HasValidShape))
        {
            throw new ArgumentException(
                "A W4.6 modeled-return transform requires exactly two valid metadata-ordered argument operands.");
        }

        var expectedDependencies = ImmutableArray.CreateBuilder<LineageNodeId>(2);
        for (var parameterIndex = 0; parameterIndex < modeledReturn.Arguments.Length; parameterIndex++)
        {
            var argument = modeledReturn.Arguments[parameterIndex];
            if (argument.Kind == LineageOperandKind.ExactInt32)
            {
                continue;
            }

            if (argument.Predecessor is not { } predecessor ||
                !validatedNodesById.TryGetValue(predecessor, out var predecessorNode) ||
                predecessorNode is not CallArgumentTransformLineageNode callArgument ||
                callArgument.StaticType != TypeSig.Int32 ||
                callArgument.CallSite != modeledReturn.CallSite ||
                callArgument.ParameterIndex != parameterIndex)
            {
                throw new ArgumentException(
                    "Each modeled-return unknown operand must depend on its matching parameter-indexed direct-call transform.");
            }

            expectedDependencies.Add(predecessor);
        }

        if (expectedDependencies.Count == 0)
        {
            throw new ArgumentException(
                "A modeled unknown return must be grounded in at least one unknown argument.");
        }

        if (!modeledReturn.Dependencies.SequenceEqual(expectedDependencies))
        {
            throw new ArgumentException(
                "A modeled-return dependency vector must contain exactly its unknown arguments in parameter order.");
        }
    }

    private static void ValidateFieldLoad(
        FieldLoadTransformLineageNode fieldLoad,
        IReadOnlyDictionary<LineageNodeId, LineageNode> validatedNodesById)
    {
        if (fieldLoad.StaticType != TypeSig.Int32 ||
            fieldLoad.Field.FieldType != TypeSig.Int32 ||
            fieldLoad.Field.IsStatic ||
            fieldLoad.Field.IsLiteral ||
            fieldLoad.Field.HasRva)
        {
            throw new ArgumentException("A W4.3 field-load transform must describe one ordinary Int32 instance field.");
        }

        if (fieldLoad.Dependencies.Length != 1 ||
            fieldLoad.Dependencies[0] != fieldLoad.InputOrigin)
        {
            throw new ArgumentException("A W4.3 field-load transform requires exactly its input-origin predecessor.");
        }

        if (!validatedNodesById.TryGetValue(fieldLoad.InputOrigin, out var predecessor))
        {
            throw new ArgumentException("A field-load input-origin predecessor is absent from the graph.");
        }

        if (predecessor is not InputOriginLineageNode input ||
            input.Origin.Kind != ProvenanceInputKind.ImportedField ||
            input.StaticType != TypeSig.Int32 ||
            input.Origin.Evidence is not (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            throw new ArgumentException(
                "A W4.3 field-load transform must depend on one partial or unavailable imported-field Int32 origin.");
        }
    }

    private static void ValidateCallBoundary(
        LineageNode node,
        DirectCallSiteIdentity callSite,
        LineageNodeId predecessor,
        string boundaryName,
        IReadOnlyDictionary<LineageNodeId, LineageNode> validatedNodesById)
    {
        if (node.StaticType != TypeSig.Int32 ||
            callSite.Caller == default ||
            callSite.Callee == default ||
            callSite.CallIlOffset < 0 ||
            callSite.Caller.Module != callSite.Callee.Module)
        {
            throw new ArgumentException(
                $"A W4.5 {boundaryName} transform requires structural Int32 and one valid same-module call site.");
        }

        if (node.Dependencies.Length != 1 ||
            node.Dependencies[0] != predecessor ||
            !predecessor.IsValid)
        {
            throw new ArgumentException(
                $"A W4.5 {boundaryName} transform requires exactly its prior unknown predecessor.");
        }

        if (!validatedNodesById.TryGetValue(predecessor, out var predecessorNode) ||
            predecessorNode.StaticType != TypeSig.Int32)
        {
            throw new ArgumentException(
                $"A W4.5 {boundaryName} predecessor must be one reachable structural Int32 explanation.");
        }
    }
}
