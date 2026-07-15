using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Domain.Concrete;

/// <summary>
/// Implements the W4.2/W4.3 lifted-flat concrete semantics with a separate content-addressed explanation channel.
/// </summary>
/// <remarks>
/// Semantic operations delegate to <see cref="ConcreteDomain"/>. Lineage never participates in value equality,
/// hashing, ordering, joins, meets, or widening. Runtime arithmetic creates a <see cref="BinaryTransformLineageNode"/>
/// only when a complete explanation can be derived from every unknown operand; bare lattice top remains deliberately
/// ungrounded and is rejected by execution through <see cref="IValuePrecisionDomain{TValue}"/>.
/// </remarks>
public sealed class ProvenanceConcreteDomain :
    IValuePrecisionDomain<ProvenanceConcreteValue>,
    IFieldLoadApproximationDomain<ProvenanceConcreteValue>
{
    private readonly ConcreteDomain semanticDomain = new();
    private readonly object lineageGate = new();
    private readonly Dictionary<LineageNodeId, LineageNode> lineageNodes = [];

    /// <summary>Gets the number of distinct canonical nodes interned by this domain instance.</summary>
    public int InternedNodeCount
    {
        get
        {
            lock (lineageGate)
            {
                return lineageNodes.Count;
            }
        }
    }

    /// <inheritdoc />
    public ProvenanceConcreteValue Bottom(TypeSig type) => Wrap(semanticDomain.Bottom(type));

    /// <inheritdoc />
    public bool IsBottom(ProvenanceConcreteValue value) => semanticDomain.IsBottom(RequireValue(value).SemanticValue);

    /// <inheritdoc />
    public ProvenanceConcreteValue Top(TypeSig type) => Wrap(semanticDomain.Top(type));

    /// <inheritdoc />
    public ProvenanceConcreteValue DefaultValue(TypeSig type) => Wrap(semanticDomain.DefaultValue(type));

    /// <inheritdoc />
    public ProvenanceConcreteValue ConstInt32(int value) => Wrap(semanticDomain.ConstInt32(value));

    /// <summary>Creates an exact signed 64-bit value without lineage.</summary>
    /// <param name="value">The exact payload.</param>
    /// <returns>An exact I8 semantic value.</returns>
    public ProvenanceConcreteValue ConstInt64(long value) => Wrap(semanticDomain.ConstInt64(value));

    /// <summary>Creates an exact typed null reference without lineage.</summary>
    /// <param name="refType">The admitted structural reference type.</param>
    /// <returns>An exact null semantic value.</returns>
    public ProvenanceConcreteValue ConstNull(TypeSig refType) => Wrap(semanticDomain.ConstNull(refType));

    /// <summary>Creates an exact immutable string value without exposing its payload in diagnostics.</summary>
    /// <param name="value">The exact string payload.</param>
    /// <returns>An exact string semantic value.</returns>
    public ProvenanceConcreteValue ConstString(string value) => Wrap(semanticDomain.ConstString(value));

    /// <summary>Creates a deterministic exact object reference for dump-free execution fixtures.</summary>
    /// <param name="id">A positive fixture-owned object identity.</param>
    /// <param name="type">The exact metadata-defined runtime type.</param>
    /// <returns>An exact reference value without lineage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a metadata TypeDef identity.</exception>
    public ProvenanceConcreteValue ObjectReference(long id, TypeSig type)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "An object reference identity must be positive.");
        }

        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsMetadataTypeDefinition)
        {
            throw new ArgumentException("An object reference requires an exact metadata TypeDef.", nameof(type));
        }

        return Wrap(semanticDomain.ObjectReference(id, type));
    }

    /// <summary>Creates one explained semantic top from a validated partial or unavailable input origin.</summary>
    /// <param name="origin">The complete bounded origin facts.</param>
    /// <returns>A same-typed unknown carrying the content-addressed input-origin root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="origin"/> is <see langword="null"/>.</exception>
    public ProvenanceConcreteValue CreateInputUnknown(ProvenanceInputOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var node = Intern(ProvenanceLineageCodec.CreateInputOrigin(origin));
        return new ProvenanceConcreteValue(semanticDomain.Top(origin.StaticType), node.Id);
    }

    /// <inheritdoc />
    public ProvenanceConcreteValue CreateFieldLoadUnknown(
        ProvenanceConcreteValue receiver,
        FieldLoadEvidence evidence)
    {
        receiver = RequireValue(receiver);
        ArgumentNullException.ThrowIfNull(evidence);

        if (receiver.SemanticValue.Kind != ConcreteValueKind.ObjectReference ||
            !receiver.SemanticValue.TryGetReferenceId(out var referenceId) ||
            referenceId <= 0 ||
            receiver.LineageRoot.HasValue)
        {
            throw new ArgumentException(
                "An approximate field load requires one exact object-reference receiver without lineage.",
                nameof(receiver));
        }

        var field = evidence.Field;
        if (field.FieldType != TypeSig.Int32 || field.IsStatic || field.IsLiteral || field.HasRva)
        {
            throw new ArgumentException(
                "W4.3 field approximation requires one ordinary instance Int32 field.",
                nameof(evidence));
        }

        if (receiver.SemanticValue.StaticType != field.DeclaringType)
        {
            throw new ArgumentException(
                "The approximate field receiver must exactly match the frozen declaring TypeDef.",
                nameof(receiver));
        }

        if (evidence.EvidenceStatus is not (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            throw new ArgumentException(
                "Approximate field evidence must be partial or unavailable.",
                nameof(evidence));
        }

        var receiverKey = new ImportedReceiverKey(evidence.ImportedObjectSha256);
        var origin = new ProvenanceInputOrigin(
            ProvenanceInputKind.ImportedField,
            evidence.DependencyOrdinal,
            evidence.EvidenceStatus,
            new ProvenanceSourceKey(evidence.Sha256),
            evidence.ReasonCode,
            TypeSig.Int32);
        var originCandidate = ProvenanceLineageCodec.CreateInputOrigin(origin);
        var fieldCandidate = ProvenanceLineageCodec.CreateFieldLoadTransform(
            receiverKey,
            field,
            originCandidate.Id);
        var fieldNode = InternFieldLoadPair(originCandidate, fieldCandidate);
        return new ProvenanceConcreteValue(semanticDomain.Top(TypeSig.Int32), fieldNode.Id);
    }

    /// <inheritdoc />
    public ProvenanceConcreteValue Join(ProvenanceConcreteValue a, ProvenanceConcreteValue b)
    {
        a = RequireValue(a);
        b = RequireValue(b);
        var semantic = semanticDomain.Join(a.SemanticValue, b.SemanticValue);
        return new ProvenanceConcreteValue(semantic, SelectLatticeRoot(semantic, a, b));
    }

    /// <inheritdoc />
    public bool IsLessThanOrEqual(ProvenanceConcreteValue a, ProvenanceConcreteValue b) =>
        semanticDomain.IsLessThanOrEqual(RequireValue(a).SemanticValue, RequireValue(b).SemanticValue);

    /// <inheritdoc />
    public ProvenanceConcreteValue Meet(ProvenanceConcreteValue a, ProvenanceConcreteValue b)
    {
        a = RequireValue(a);
        b = RequireValue(b);
        var semantic = semanticDomain.Meet(a.SemanticValue, b.SemanticValue);
        return new ProvenanceConcreteValue(semantic, SelectLatticeRoot(semantic, a, b));
    }

    /// <inheritdoc />
    public ProvenanceConcreteValue Widen(ProvenanceConcreteValue prev, ProvenanceConcreteValue next)
    {
        prev = RequireValue(prev);
        next = RequireValue(next);
        var semantic = semanticDomain.Widen(prev.SemanticValue, next.SemanticValue);
        return new ProvenanceConcreteValue(semantic, SelectLatticeRoot(semantic, prev, next));
    }

    /// <inheritdoc />
    public TypeSig GetStaticType(ProvenanceConcreteValue value) =>
        semanticDomain.GetStaticType(RequireValue(value).SemanticValue);

    /// <inheritdoc />
    public StackKind GetStackKind(ProvenanceConcreteValue value) =>
        semanticDomain.GetStackKind(RequireValue(value).SemanticValue);

    /// <inheritdoc />
    public bool TryGetConstInt32(ProvenanceConcreteValue value, out int c) =>
        semanticDomain.TryGetConstInt32(RequireValue(value).SemanticValue, out c);

    /// <inheritdoc />
    public ProvenanceConcreteValue ApplyBinary(
        BinaryOp op,
        ProvenanceConcreteValue a,
        ProvenanceConcreteValue b)
    {
        a = RequireValue(a);
        b = RequireValue(b);
        var semantic = semanticDomain.ApplyBinary(op, a.SemanticValue, b.SemanticValue);
        if (semantic.Kind != ConcreteValueKind.Unknown)
        {
            return Wrap(semantic);
        }

        if (semantic.StaticType != TypeSig.Int32)
        {
            return Wrap(semantic);
        }

        if (!TryCreateOperand(a, out var left) || !TryCreateOperand(b, out var right))
        {
            return Wrap(semantic);
        }

        var node = Intern(ProvenanceLineageCodec.CreateBinaryTransform(op, TypeSig.Int32, left!, right!));
        return new ProvenanceConcreteValue(semantic, node.Id);
    }

    /// <inheritdoc />
    public ValuePrecisionKind GetPrecision(ProvenanceConcreteValue value)
    {
        value = RequireValue(value);
        if (semanticDomain.IsBottom(value.SemanticValue))
        {
            throw new ArgumentException("Lattice bottom has no executable precision.", nameof(value));
        }

        if (value.SemanticValue.Kind != ConcreteValueKind.Unknown)
        {
            if (value.LineageRoot.HasValue)
            {
                throw new ArgumentException("An exact value cannot carry unknown lineage.", nameof(value));
            }

            return ValuePrecisionKind.Exact;
        }

        if (value.LineageRoot is not { } root)
        {
            return ValuePrecisionKind.UnexplainedUnknown;
        }

        lock (lineageGate)
        {
            if (!lineageNodes.TryGetValue(root, out var node) || node.StaticType != value.SemanticValue.StaticType)
            {
                throw new ArgumentException("The unknown lineage root is foreign or has a conflicting type.", nameof(value));
            }
        }

        return ValuePrecisionKind.ExplainedUnknown;
    }

    /// <summary>Captures only the immutable nodes reachable from one explained unknown.</summary>
    /// <param name="value">An explained unknown owned by this domain instance.</param>
    /// <returns>A canonical, insertion-order-independent reachable DAG.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is exact, bottom, ungrounded, foreign, or type-inconsistent.
    /// </exception>
    public ProvenanceLineageGraph CaptureLineage(ProvenanceConcreteValue value)
    {
        value = RequireValue(value);
        if (GetPrecision(value) != ValuePrecisionKind.ExplainedUnknown || value.LineageRoot is not { } root)
        {
            throw new ArgumentException("Lineage capture requires an explained unknown.", nameof(value));
        }

        lock (lineageGate)
        {
            var reachable = new Dictionary<LineageNodeId, LineageNode>();
            Collect(root, reachable);
            return new ProvenanceLineageGraph(root, reachable.Values.ToImmutableArray());
        }
    }

    /// <summary>Validates and imports one fresh-object canonical graph as a local explained unknown.</summary>
    /// <param name="graph">A canonical reachable graph produced by another domain instance.</param>
    /// <returns>A local semantic top carrying <paramref name="graph"/>'s root identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="graph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The graph conflicts with an already interned identity.</exception>
    public ProvenanceConcreteValue ReplayLineage(ProvenanceLineageGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.ValidateForReplay();
        lock (lineageGate)
        {
            foreach (var node in graph.Nodes)
            {
                if (!ProvenanceLineageCodec.IsCanonical(node))
                {
                    throw new ArgumentException("A replay graph contains a noncanonical node.", nameof(graph));
                }

                if (lineageNodes.TryGetValue(node.Id, out var existing) &&
                    !existing.CanonicalBytes.AsSpan().SequenceEqual(node.CanonicalBytes.AsSpan()))
                {
                    throw new ArgumentException(
                        "A replay graph identity conflicts with already interned canonical bytes.",
                        nameof(graph));
                }
            }

            foreach (var node in graph.Nodes)
            {
                if (!lineageNodes.ContainsKey(node.Id))
                {
                    lineageNodes.Add(node.Id, node);
                }
            }

            var root = lineageNodes[graph.Root];
            return new ProvenanceConcreteValue(semanticDomain.Top(root.StaticType), root.Id);
        }
    }

    private bool TryCreateOperand(ProvenanceConcreteValue value, out LineageOperand? operand)
    {
        if (value.SemanticValue.Kind == ConcreteValueKind.Unknown)
        {
            if (value.LineageRoot is not { } root)
            {
                operand = null;
                return false;
            }

            lock (lineageGate)
            {
                if (!lineageNodes.TryGetValue(root, out var node) || node.StaticType != TypeSig.Int32)
                {
                    throw new ArgumentException("An unknown arithmetic operand has foreign or conflicting lineage.", nameof(value));
                }
            }

            operand = LineageOperand.FromUnknown(root);
            return true;
        }

        if (!semanticDomain.TryGetConstInt32(value.SemanticValue, out var exact) ||
            value.SemanticValue.StaticType != TypeSig.Int32)
        {
            throw new ArgumentException("W4.2 binary lineage admits only exact or explained Int32 operands.", nameof(value));
        }

        operand = LineageOperand.FromExactInt32(exact);
        return true;
    }

    private LineageNodeId? SelectLatticeRoot(
        ConcreteValue semanticResult,
        ProvenanceConcreteValue left,
        ProvenanceConcreteValue right)
    {
        if (semanticResult.Kind != ConcreteValueKind.Unknown)
        {
            return null;
        }

        var leftUnknown = left.SemanticValue.Kind == ConcreteValueKind.Unknown;
        var rightUnknown = right.SemanticValue.Kind == ConcreteValueKind.Unknown;
        if (leftUnknown && rightUnknown)
        {
            var leftRoot = GetLocalRoot(left);
            var rightRoot = GetLocalRoot(right);
            return leftRoot == rightRoot ? leftRoot : null;
        }

        if (leftUnknown)
        {
            return GetLocalRoot(left);
        }

        return rightUnknown ? GetLocalRoot(right) : null;
    }

    private LineageNodeId? GetLocalRoot(ProvenanceConcreteValue value)
    {
        if (value.LineageRoot is not { } root)
        {
            return null;
        }

        lock (lineageGate)
        {
            return lineageNodes.TryGetValue(root, out var node) && node.StaticType == value.SemanticValue.StaticType
                ? root
                : null;
        }
    }

    private LineageNode Intern(LineageNode candidate)
    {
        lock (lineageGate)
        {
            return InternUnderLock(candidate);
        }
    }

    private FieldLoadTransformLineageNode InternFieldLoadPair(
        InputOriginLineageNode origin,
        FieldLoadTransformLineageNode fieldLoad)
    {
        lock (lineageGate)
        {
            if (!ProvenanceLineageCodec.IsCanonical(origin) || !ProvenanceLineageCodec.IsCanonical(fieldLoad))
            {
                throw new ArgumentException("A field-load lineage pair is not canonical.");
            }

            if (fieldLoad.Dependencies.Length != 1 ||
                fieldLoad.Dependencies[0] != origin.Id ||
                fieldLoad.InputOrigin != origin.Id)
            {
                throw new ArgumentException("A field-load transform must depend on exactly its imported-field origin.");
            }

            var storedOrigin = Preflight(origin);
            var storedField = Preflight(fieldLoad);
            if (storedOrigin is not InputOriginLineageNode canonicalOrigin ||
                storedField is not FieldLoadTransformLineageNode canonicalField)
            {
                throw new InvalidOperationException("A canonical lineage identity is bound to an incompatible node kind.");
            }

            if (canonicalOrigin.Origin.Kind != ProvenanceInputKind.ImportedField ||
                canonicalOrigin.StaticType != TypeSig.Int32 ||
                canonicalOrigin.Origin.Evidence is not (
                    EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
            {
                throw new ArgumentException("A field-load lineage pair requires a partial or unavailable imported-field origin.");
            }

            if (!lineageNodes.ContainsKey(origin.Id))
            {
                lineageNodes.Add(origin.Id, origin);
            }

            if (!lineageNodes.ContainsKey(fieldLoad.Id))
            {
                lineageNodes.Add(fieldLoad.Id, fieldLoad);
            }

            return canonicalField;

            LineageNode Preflight(LineageNode candidate)
            {
                if (!lineageNodes.TryGetValue(candidate.Id, out var existing))
                {
                    return candidate;
                }

                if (!existing.CanonicalBytes.AsSpan().SequenceEqual(candidate.CanonicalBytes.AsSpan()))
                {
                    throw new InvalidOperationException(
                        "A lineage SHA-256 identity collided with different canonical bytes.");
                }

                return existing;
            }
        }
    }

    private LineageNode InternUnderLock(LineageNode candidate)
    {
        if (!ProvenanceLineageCodec.IsCanonical(candidate))
        {
            throw new ArgumentException("A lineage node is not canonical.", nameof(candidate));
        }

        if (lineageNodes.TryGetValue(candidate.Id, out var existing))
        {
            if (!existing.CanonicalBytes.AsSpan().SequenceEqual(candidate.CanonicalBytes.AsSpan()))
            {
                throw new InvalidOperationException("A lineage SHA-256 identity collided with different canonical bytes.");
            }

            return existing;
        }

        foreach (var dependency in candidate.Dependencies)
        {
            if (!lineageNodes.ContainsKey(dependency))
            {
                throw new ArgumentException("A lineage node refers to a predecessor not interned in this domain.", nameof(candidate));
            }
        }

        lineageNodes.Add(candidate.Id, candidate);
        return candidate;
    }

    private void Collect(LineageNodeId id, Dictionary<LineageNodeId, LineageNode> reachable)
    {
        var pending = new Stack<LineageNodeId>();
        pending.Push(id);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (reachable.ContainsKey(current))
            {
                continue;
            }

            if (!lineageNodes.TryGetValue(current, out var node))
            {
                throw new ArgumentException("A captured lineage dependency is absent from the domain.", nameof(id));
            }

            reachable.Add(current, node);
            for (var index = node.Dependencies.Length - 1; index >= 0; index--)
            {
                pending.Push(node.Dependencies[index]);
            }
        }
    }

    private static ProvenanceConcreteValue Wrap(ConcreteValue value) => new(value);

    private static ProvenanceConcreteValue RequireValue(ProvenanceConcreteValue value) =>
        value ?? throw new ArgumentNullException(nameof(value));
}
