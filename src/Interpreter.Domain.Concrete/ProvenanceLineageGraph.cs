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

        Nodes = nodes
            .OrderBy(static node => node.Id.Sha256, StringComparer.Ordinal)
            .ToImmutableArray();
        nodesById = new Dictionary<LineageNodeId, LineageNode>(Nodes.Length);
        foreach (var node in Nodes)
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

        ValidateReachabilityAndAcyclicity(root);
        Root = root;
        CanonicalBytes = ProvenanceLineageCodec.EncodeGraph(root, Nodes);
        Sha256 = Convert.ToHexString(SHA256.HashData(CanonicalBytes.AsSpan())).ToLowerInvariant();
    }

    /// <summary>Gets the content-addressed node that explains the returned unknown.</summary>
    public LineageNodeId Root { get; }

    /// <summary>Gets every reachable node exactly once, sorted by lowercase SHA-256 identity.</summary>
    public ImmutableArray<LineageNode> Nodes { get; }

    /// <summary>Gets the versioned canonical bytes of the complete reachable graph.</summary>
    public ImmutableArray<byte> CanonicalBytes { get; }

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

    private void ValidateReachabilityAndAcyclicity(LineageNodeId root)
    {
        var colors = new Dictionary<LineageNodeId, byte>(nodesById.Count);
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

            if (!nodesById.TryGetValue(id, out var node))
            {
                throw new ArgumentException("A lineage dependency is absent from the graph.");
            }

            colors[id] = 1;
            stack.Push((id, true));
            for (var index = node.Dependencies.Length - 1; index >= 0; index--)
            {
                var dependency = node.Dependencies[index];
                if (!nodesById.TryGetValue(dependency, out var dependencyNode))
                {
                    throw new ArgumentException("A lineage dependency is absent from the graph.");
                }

                if (node is BinaryTransformLineageNode && dependencyNode.StaticType != TypeSig.Int32)
                {
                    throw new ArgumentException("A W4.2 binary predecessor must have the structural Int32 type.");
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

        if (colors.Count(static pair => pair.Value == 2) != nodesById.Count)
        {
            throw new ArgumentException("A lineage graph cannot contain nodes unreachable from its root.");
        }
    }
}
