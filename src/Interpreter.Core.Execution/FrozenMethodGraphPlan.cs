using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>Classifies the preparation-time disposition frozen for one direct-call edge.</summary>
public enum FrozenMethodCallDisposition
{
    /// <summary>The target has an admitted body and executes through an interpreter frame.</summary>
    Interpreted = 1,

    /// <summary>The target is an opaque leaf covered by one exact, side-effect-free pure model.</summary>
    PureModel = 2,
}

/// <summary>
/// Freezes one structurally resolved direct-call edge in an admitted method graph.
/// </summary>
/// <remarks>
/// The call target is the body-independent descriptor observed while decoding the caller. The target body is held by
/// the corresponding <see cref="FrozenMethodGraphNode"/> only for <see cref="FrozenMethodCallDisposition.Interpreted"/>.
/// A <see cref="FrozenMethodCallDisposition.PureModel"/> target instead correlates with one body-free
/// <see cref="FrozenPureModelLeaf"/>. Instances are constructed only by the execution preparation pipeline.
/// </remarks>
public sealed class FrozenMethodCallSite : IEquatable<FrozenMethodCallSite>
{
    internal FrozenMethodCallSite(
        MethodHandle caller,
        int ilOffset,
        int metadataToken,
        ResolvedMethodCallTarget target,
        FrozenMethodCallDisposition disposition,
        EvaluationEffectStatus effects,
        PureCallModelDescriptor? modelDescriptor)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (caller == default)
        {
            throw new ArgumentException("A frozen call site requires a non-default caller.", nameof(caller));
        }

        if (ilOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ilOffset));
        }

        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        if (effects != EvaluationEffectStatus.None)
        {
            throw new ArgumentException(
                "A successfully frozen W4 call edge must have the normalized no-effect disposition.",
                nameof(effects));
        }

        if (disposition == FrozenMethodCallDisposition.Interpreted && modelDescriptor is not null ||
            disposition == FrozenMethodCallDisposition.PureModel &&
            (modelDescriptor is null ||
             modelDescriptor.Confidence != PureCallModelConfidence.Exact ||
             modelDescriptor.Effects != EvaluationEffectStatus.None ||
             modelDescriptor.Target != target))
        {
            throw new ArgumentException(
                "The call disposition, structural target, effect status, and model descriptor disagree.",
                nameof(modelDescriptor));
        }

        Caller = caller;
        IlOffset = ilOffset;
        MetadataToken = metadataToken;
        Target = target;
        Disposition = disposition;
        Effects = effects;
        ModelDescriptor = modelDescriptor;
    }

    /// <summary>Gets the exact MethodDef containing the direct call.</summary>
    public MethodHandle Caller { get; }

    /// <summary>Gets the zero-based byte offset of the <c>call</c> opcode in the caller body.</summary>
    public int IlOffset { get; }

    /// <summary>Gets the raw four-byte InlineMethod token frozen from the instruction operand.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the exact managed-IL MethodDef identity and body-independent signature selected for the edge.</summary>
    public ResolvedMethodCallTarget Target { get; }

    /// <summary>Gets the immutable interpreted or pure-model disposition selected during preparation.</summary>
    public FrozenMethodCallDisposition Disposition { get; }

    /// <summary>Gets the normalized effect classification frozen for this edge.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>Gets the selected pure-model descriptor, or <see langword="null"/> for an interpreted edge.</summary>
    public PureCallModelDescriptor? ModelDescriptor { get; }

    /// <inheritdoc />
    public bool Equals(FrozenMethodCallSite? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Caller == other.Caller &&
        IlOffset == other.IlOffset &&
        MetadataToken == other.MetadataToken &&
        Target.Method == other.Target.Method &&
        Target.Signature == other.Target.Signature &&
        Target.IsManagedIl == other.Target.IsManagedIl &&
        Disposition == other.Disposition &&
        Effects == other.Effects &&
        Equals(ModelDescriptor, other.ModelDescriptor);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FrozenMethodCallSite);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = GraphContent.AddMethod(17, Caller);
        hash = unchecked((hash * 397) ^ IlOffset);
        hash = unchecked((hash * 397) ^ MetadataToken);
        hash = GraphContent.AddMethod(hash, Target.Method);
        var legacyHash = unchecked((hash * 397) ^ Target.Signature.GetHashCode());
        if (Disposition == FrozenMethodCallDisposition.Interpreted &&
            Effects == EvaluationEffectStatus.None &&
            ModelDescriptor is null)
        {
            return legacyHash;
        }

        hash = legacyHash;
        hash = unchecked((hash * 397) ^ (int)Disposition);
        hash = unchecked((hash * 397) ^ (int)Effects);
        return unchecked((hash * 397) ^ (ModelDescriptor?.GetHashCode() ?? 0));
    }
}

/// <summary>
/// Freezes one structurally resolved call target as an opaque leaf covered by an exact pure model.
/// </summary>
/// <remarks>
/// The leaf retains only body-independent target evidence and the selected descriptor publicly. The runtime model
/// capability is retained internally for the later modeled-transfer increment; it is deliberately excluded from
/// structural equality so fresh equivalent registries reproduce equal plans. No target body belongs to this value.
/// </remarks>
public sealed class FrozenPureModelLeaf : IEquatable<FrozenPureModelLeaf>
{
    internal FrozenPureModelLeaf(
        ResolvedMethodCallTarget target,
        PureCallModelDescriptor descriptor,
        IPureCallModel runtimeModel)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(runtimeModel);
        if (descriptor.Target != target ||
            descriptor.Confidence != PureCallModelConfidence.Exact ||
            descriptor.Effects != EvaluationEffectStatus.None)
        {
            throw new ArgumentException(
                "A modeled leaf requires an exact target-correlated no-effect descriptor.",
                nameof(descriptor));
        }

        Target = target;
        Descriptor = descriptor;
        RuntimeModel = runtimeModel;
    }

    /// <summary>Gets the exact body-independent managed-IL target covered by this opaque leaf.</summary>
    public ResolvedMethodCallTarget Target { get; }

    /// <summary>Gets the exact structural MethodDef identity of the modeled target.</summary>
    public MethodHandle Method => Target.Method;

    /// <summary>Gets the selected stable model identity, version, confidence, target, and normalized effects.</summary>
    public PureCallModelDescriptor Descriptor { get; }

    /// <summary>Gets the normalized effect classification for the selected pure model.</summary>
    public EvaluationEffectStatus Effects => Descriptor.Effects;

    internal IPureCallModel RuntimeModel { get; }

    /// <inheritdoc />
    public bool Equals(FrozenPureModelLeaf? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Target == other.Target &&
        Descriptor == other.Descriptor;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FrozenPureModelLeaf);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Target, Descriptor);
}

/// <summary>
/// Freezes one completely admitted method definition and its outgoing dependencies.
/// </summary>
/// <remarks>
/// Definitions, whole-body admission facts, field descriptors, and call sites are immutable snapshots. The node also
/// retains an internal admitted-instruction plan so later execution work can consume exactly the graph that was
/// validated instead of decoding or resolving it again.
/// </remarks>
public sealed class FrozenMethodGraphNode : IEquatable<FrozenMethodGraphNode>
{
    internal FrozenMethodGraphNode(
        AdmittedMethodPlan admittedPlan,
        ImmutableArray<ResolvedField> fields,
        ImmutableArray<FrozenMethodCallSite> callSites)
    {
        ArgumentNullException.ThrowIfNull(admittedPlan);
        if (fields.IsDefault)
        {
            throw new ArgumentException("A frozen node requires an initialized field vector.", nameof(fields));
        }

        if (callSites.IsDefault)
        {
            throw new ArgumentException("A frozen node requires an initialized call-site vector.", nameof(callSites));
        }

        RuntimePlan = admittedPlan;
        Definition = admittedPlan.Definition;
        Admission = admittedPlan.Admission;
        Fields = fields;
        CallSites = callSites;
    }

    /// <summary>Gets the exact MethodDef identity of this node.</summary>
    public MethodHandle Method => Definition.Method;

    /// <summary>Gets the atomically resolved body, signature, and local-layout evidence.</summary>
    public ResolvedMethodDefinition Definition { get; }

    /// <summary>Gets the successful whole-body typed-admission projection.</summary>
    public MethodAdmissionResult Admission { get; }

    /// <summary>Gets the canonically ordered distinct structural fields referenced by this method.</summary>
    public ImmutableArray<ResolvedField> Fields { get; }

    /// <summary>Gets every outgoing direct-call edge in increasing IL-offset order.</summary>
    public ImmutableArray<FrozenMethodCallSite> CallSites { get; }

    internal AdmittedMethodPlan RuntimePlan { get; }

    /// <inheritdoc />
    public bool Equals(FrozenMethodGraphNode? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        GraphContent.DefinitionEquals(Definition, other.Definition) &&
        GraphContent.AdmissionEquals(Admission, other.Admission) &&
        Fields.SequenceEqual(other.Fields) &&
        CallSites.SequenceEqual(other.CallSites);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FrozenMethodGraphNode);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = GraphContent.DefinitionHash(Definition);
        hash = unchecked((hash * 397) ^ GraphContent.AdmissionHash(Admission));
        foreach (var field in Fields)
        {
            hash = unchecked((hash * 397) ^ field.GetHashCode());
        }

        foreach (var callSite in CallSites)
        {
            hash = unchecked((hash * 397) ^ callSite.GetHashCode());
        }

        return hash;
    }
}

/// <summary>
/// Represents the complete immutable, acyclic dependency closure prepared for one W4 root method.
/// </summary>
/// <remarks>
/// Nodes, fields, and call sites use canonical structural ordering independent of resolver dictionaries or discovery
/// storage. Preparation nevertheless discovers methods by root-first call-site-ordered depth-first traversal so
/// failures and fixed-cap precedence are deterministic. Public construction is intentionally unavailable: every
/// instance has passed graph reachability, identity, signature, instruction, cycle, and depth validation.
/// </remarks>
public sealed class FrozenMethodGraphPlan : IEquatable<FrozenMethodGraphPlan>
{
    private readonly ImmutableDictionary<MethodHandle, FrozenMethodGraphNode> _nodeLookup;
    private readonly ImmutableDictionary<MethodHandle, FrozenPureModelLeaf> _modeledLeafLookup;
    private readonly ImmutableDictionary<MethodHandle, AdmittedMethodPlan> _runtimePlanLookup;

    internal FrozenMethodGraphPlan(
        MethodHandle root,
        ImmutableArray<FrozenMethodGraphNode> nodes,
        ImmutableArray<FrozenPureModelLeaf> modeledLeaves,
        ImmutableArray<ResolvedField> fields,
        ImmutableArray<FrozenMethodCallSite> callSites,
        int requiredLogicalDepth,
        int traversalUnitCount)
    {
        if (root == default)
        {
            throw new ArgumentException("A graph plan requires a non-default root.", nameof(root));
        }

        if (nodes.IsDefault || nodes.Length == 0 || modeledLeaves.IsDefault || fields.IsDefault || callSites.IsDefault)
        {
            throw new ArgumentException("A graph plan requires initialized, nonempty node and dependency vectors.");
        }

        if (requiredLogicalDepth < 1 || traversalUnitCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredLogicalDepth),
                "Graph depth and traversal usage must be positive.");
        }

        Root = root;
        Nodes = nodes;
        ModeledLeaves = modeledLeaves;
        Fields = fields;
        CallSites = callSites;
        RequiredLogicalDepth = requiredLogicalDepth;
        TraversalUnitCount = traversalUnitCount;
        _nodeLookup = nodes.ToImmutableDictionary(node => node.Method);
        _modeledLeafLookup = modeledLeaves.ToImmutableDictionary(leaf => leaf.Method);
        _runtimePlanLookup = nodes.ToImmutableDictionary(node => node.Method, node => node.RuntimePlan);
    }

    /// <summary>Gets the exact root MethodDef from which dependency discovery began.</summary>
    public MethodHandle Root { get; }

    /// <summary>Gets all admitted methods in canonical structural MethodDef order.</summary>
    public ImmutableArray<FrozenMethodGraphNode> Nodes { get; }

    /// <summary>
    /// Gets all opaque pure-model leaves in canonical structural MethodDef order; no leaf contains a target body.
    /// </summary>
    public ImmutableArray<FrozenPureModelLeaf> ModeledLeaves { get; }

    /// <summary>Gets all distinct resolved field dependencies in canonical structural FieldDef order.</summary>
    public ImmutableArray<ResolvedField> Fields { get; }

    /// <summary>Gets every retained direct-call edge in canonical caller-and-offset order.</summary>
    public ImmutableArray<FrozenMethodCallSite> CallSites { get; }

    /// <summary>
    /// Gets the longest root-to-interpreted-method-or-modeled-boundary path, counting the root at logical depth one.
    /// </summary>
    public int RequiredLogicalDepth { get; }

    /// <summary>
    /// Gets the preparation units charged for interpreted methods, modeled leaves, structural fields, and call edges.
    /// </summary>
    public int TraversalUnitCount { get; }

    /// <summary>Looks up an admitted node without exposing mutable preparation state.</summary>
    /// <param name="method">The exact MethodDef identity to find.</param>
    /// <param name="node">Receives the frozen node on success; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="method"/> belongs to this complete graph.</returns>
    public bool TryGetNode(MethodHandle method, out FrozenMethodGraphNode? node) =>
        _nodeLookup.TryGetValue(method, out node);

    /// <summary>Looks up an opaque modeled leaf without acquiring or exposing a target body.</summary>
    /// <param name="method">The exact modeled MethodDef identity to find.</param>
    /// <param name="leaf">Receives the frozen leaf on success; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="method"/> is a modeled leaf in this graph.</returns>
    public bool TryGetModeledLeaf(MethodHandle method, out FrozenPureModelLeaf? leaf) =>
        _modeledLeafLookup.TryGetValue(method, out leaf);

    internal bool TryGetAdmittedMethodPlan(MethodHandle method, out AdmittedMethodPlan? plan) =>
        _runtimePlanLookup.TryGetValue(method, out plan);

    /// <inheritdoc />
    public bool Equals(FrozenMethodGraphPlan? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Root == other.Root &&
        RequiredLogicalDepth == other.RequiredLogicalDepth &&
        TraversalUnitCount == other.TraversalUnitCount &&
        Nodes.SequenceEqual(other.Nodes) &&
        ModeledLeaves.SequenceEqual(other.ModeledLeaves) &&
        Fields.SequenceEqual(other.Fields) &&
        CallSites.SequenceEqual(other.CallSites);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FrozenMethodGraphPlan);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = GraphContent.AddMethod(17, Root);
        hash = unchecked((hash * 397) ^ RequiredLogicalDepth);
        hash = unchecked((hash * 397) ^ TraversalUnitCount);
        foreach (var node in Nodes)
        {
            hash = unchecked((hash * 397) ^ node.GetHashCode());
        }

        foreach (var leaf in ModeledLeaves)
        {
            hash = unchecked((hash * 397) ^ leaf.GetHashCode());
        }

        foreach (var field in Fields)
        {
            hash = unchecked((hash * 397) ^ field.GetHashCode());
        }

        foreach (var callSite in CallSites)
        {
            hash = unchecked((hash * 397) ^ callSite.GetHashCode());
        }

        return hash;
    }
}

/// <summary>Reports either one complete frozen method graph or a structured preparation failure.</summary>
/// <remarks>
/// Failed results never carry a partial graph. Successful results always use <see cref="MachineRunStatus.Ready"/>
/// and carry no failure, making the public facts a closed discriminated outcome for this prototype phase.
/// </remarks>
public sealed class MethodGraphPreparationResult
{
    private MethodGraphPreparationResult(
        FrozenMethodGraphPlan? plan,
        MachineRunStatus status,
        ExecutionFailure? failure,
        MethodGraphTraversalAccounting? traversalAccounting)
    {
        Plan = plan;
        Status = status;
        Failure = failure;
        TraversalAccounting = traversalAccounting;
    }

    /// <summary>Gets the complete graph on success, or <see langword="null"/> after any failure.</summary>
    public FrozenMethodGraphPlan? Plan { get; }

    /// <summary>Gets the machine-level preparation disposition.</summary>
    public MachineRunStatus Status { get; }

    /// <summary>Gets the structured failure on rejection, or <see langword="null"/> on success.</summary>
    public ExecutionFailure? Failure { get; }

    /// <summary>
    /// Gets ordered traversal accounting when graph preparation entered a discovery session, or
    /// <see langword="null"/> when request validation rejected the operation before discovery began.
    /// </summary>
    public MethodGraphTraversalAccounting? TraversalAccounting { get; }

    /// <summary>
    /// Gets a value indicating whether preparation produced one complete frozen graph. Planning success does not by
    /// itself imply that the current machine admits every frozen call disposition.
    /// </summary>
    public bool IsSuccess => Plan is not null && Status == MachineRunStatus.Ready && Failure is null;

    internal static MethodGraphPreparationResult Success(FrozenMethodGraphPlan plan) =>
        new(plan, MachineRunStatus.Ready, null, traversalAccounting: null);

    internal static MethodGraphPreparationResult Failed(MachineRunStatus status, ExecutionFailure failure) =>
        new(null, status, failure, traversalAccounting: null);

    internal MethodGraphPreparationResult WithTraversalAccounting(MethodGraphTraversalAccounting traversalAccounting)
    {
        ArgumentNullException.ThrowIfNull(traversalAccounting);
        if (TraversalAccounting is not null)
        {
            throw new InvalidOperationException("Traversal accounting was already attached to this preparation result.");
        }

        if (Plan is not null &&
            (traversalAccounting.IsExhausted || Plan.TraversalUnitCount != traversalAccounting.Used))
        {
            throw new ArgumentException(
                "A successful frozen graph must consume exactly its reported non-exhausted traversal units.",
                nameof(traversalAccounting));
        }

        return new MethodGraphPreparationResult(Plan, Status, Failure, traversalAccounting);
    }
}

internal static class GraphContent
{
    internal static bool DefinitionEquals(ResolvedMethodDefinition left, ResolvedMethodDefinition right) =>
        left.Method == right.Method &&
        left.Body.MaxStack == right.Body.MaxStack &&
        left.Body.LocalVariablesInitialized == right.Body.LocalVariablesInitialized &&
        left.Body.LocalSignatureToken == right.Body.LocalSignatureToken &&
        left.Body.ExceptionRegionCount == right.Body.ExceptionRegionCount &&
        left.Body.CodeBytes.SequenceEqual(right.Body.CodeBytes) &&
        left.Signature.CallSignature == right.Signature.CallSignature &&
        left.Signature.LocalTypes.SequenceEqual(right.Signature.LocalTypes);

    internal static bool AdmissionEquals(MethodAdmissionResult left, MethodAdmissionResult right) =>
        left.IsAdmitted == right.IsAdmitted &&
        left.InstructionCount == right.InstructionCount &&
        left.FailureStatus == right.FailureStatus &&
        Equals(left.Failure, right.Failure) &&
        left.InstructionBoundaries.Length == right.InstructionBoundaries.Length &&
        left.InstructionBoundaries.Zip(right.InstructionBoundaries).All(pair =>
            pair.First.IlOffset == pair.Second.IlOffset &&
            pair.First.ExpectedStackTypes.SequenceEqual(pair.Second.ExpectedStackTypes));

    internal static int DefinitionHash(ResolvedMethodDefinition definition)
    {
        var hash = AddMethod(17, definition.Method);
        hash = unchecked((hash * 397) ^ definition.Body.MaxStack);
        hash = unchecked((hash * 397) ^ definition.Body.LocalSignatureToken);
        hash = unchecked((hash * 397) ^ definition.Body.ExceptionRegionCount);
        hash = unchecked((hash * 397) ^ (definition.Body.LocalVariablesInitialized ? 1 : 0));
        foreach (var value in definition.Body.CodeBytes)
        {
            hash = unchecked((hash * 397) ^ value);
        }

        hash = unchecked((hash * 397) ^ definition.Signature.CallSignature.GetHashCode());
        foreach (var local in definition.Signature.LocalTypes)
        {
            hash = unchecked((hash * 397) ^ local.GetHashCode());
        }

        return hash;
    }

    internal static int AdmissionHash(MethodAdmissionResult admission)
    {
        var hash = admission.IsAdmitted ? 1 : 0;
        hash = unchecked((hash * 397) ^ admission.InstructionCount);
        hash = unchecked((hash * 397) ^ (int)admission.FailureStatus);
        hash = unchecked((hash * 397) ^ (admission.Failure?.GetHashCode() ?? 0));
        foreach (var boundary in admission.InstructionBoundaries)
        {
            hash = unchecked((hash * 397) ^ boundary.IlOffset);
            foreach (var type in boundary.ExpectedStackTypes)
            {
                hash = unchecked((hash * 397) ^ type.GetHashCode());
            }
        }

        return hash;
    }

    internal static int AddMethod(int hash, MethodHandle method)
    {
        hash = unchecked((hash * 397) ^ (int)method.Module.High);
        hash = unchecked((hash * 397) ^ (int)(method.Module.High >> 32));
        hash = unchecked((hash * 397) ^ (int)method.Module.Low);
        hash = unchecked((hash * 397) ^ (int)(method.Module.Low >> 32));
        return unchecked((hash * 397) ^ method.MetadataToken);
    }
}

internal sealed class MethodHandleCanonicalComparer : IComparer<MethodHandle>
{
    internal static MethodHandleCanonicalComparer Instance { get; } = new();

    public int Compare(MethodHandle left, MethodHandle right)
    {
        var comparison = left.Module.High.CompareTo(right.Module.High);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Module.Low.CompareTo(right.Module.Low);
        return comparison != 0 ? comparison : left.MetadataToken.CompareTo(right.MetadataToken);
    }
}

internal sealed class FieldHandleCanonicalComparer : IComparer<FieldHandle>
{
    internal static FieldHandleCanonicalComparer Instance { get; } = new();

    public int Compare(FieldHandle left, FieldHandle right)
    {
        var comparison = left.Module.High.CompareTo(right.Module.High);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Module.Low.CompareTo(right.Module.Low);
        return comparison != 0 ? comparison : left.MetadataToken.CompareTo(right.MetadataToken);
    }
}
