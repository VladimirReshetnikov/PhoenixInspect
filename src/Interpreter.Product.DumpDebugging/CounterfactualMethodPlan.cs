using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>Projects one frozen direct-call disposition without exposing the runtime graph that owns it.</summary>
public sealed class CounterfactualPlanCallSite
{
    internal CounterfactualPlanCallSite(FrozenMethodCallSite source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Caller = source.Caller;
        IlOffset = source.IlOffset;
        MetadataToken = source.MetadataToken;
        TargetMethod = source.Target.Method;
        Disposition = source.Disposition;
        Effects = source.Effects;
        ModelId = source.ModelDescriptor?.Identity.StableId;
        ModelVersion = source.ModelDescriptor?.Identity.Version;
        ModelConfidence = source.ModelDescriptor?.Confidence;
    }

    /// <summary>Gets the exact MethodDef containing the call instruction.</summary>
    public MethodHandle Caller { get; }

    /// <summary>Gets the zero-based call-instruction offset in <see cref="Caller"/>.</summary>
    public int IlOffset { get; }

    /// <summary>Gets the exact raw InlineMethod token retained from the admitted instruction.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the structurally resolved target MethodDef.</summary>
    public MethodHandle TargetMethod { get; }

    /// <summary>Gets whether the target is interpreted or handled by one frozen pure model.</summary>
    public FrozenMethodCallDisposition Disposition { get; }

    /// <summary>Gets the normalized effect declaration frozen for the call.</summary>
    public EvaluationEffectStatus Effects { get; }

    /// <summary>Gets the stable model identity for a modeled call, or <see langword="null"/> otherwise.</summary>
    public string? ModelId { get; }

    /// <summary>Gets the exact model version for a modeled call, or <see langword="null"/> otherwise.</summary>
    public PureCallModelVersion? ModelVersion { get; }

    /// <summary>Gets the model confidence for a modeled call, or <see langword="null"/> otherwise.</summary>
    public PureCallModelConfidence? ModelConfidence { get; }
}

/// <summary>
/// Freezes one successfully prepared structural method graph, traversal transcript, and private typed runtime
/// binding for a bounded counterfactual request.
/// </summary>
/// <typeparam name="TMemory">The persistent memory snapshot type retained privately for the rooted runner.</typeparam>
/// <remarks>
/// Construction and runtime fields are internal. Public identity contains no resolver, registry, capability, memory
/// reference, issuer token, dictionary order, process-random hash, or display-only method name.
/// </remarks>
public sealed class CounterfactualMethodPlan<TMemory>
    where TMemory : IPersistentMemoryState<TMemory>
{
    /// <summary>Gets the canonical plan schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    private readonly object issuer;
    private readonly CounterfactualRuntimeBundle<TMemory> runtimeBundle;
    private readonly ImmutableArray<MethodHandle> interpretedMethods;
    private readonly ImmutableArray<MethodHandle> modeledMethods;
    private readonly ImmutableArray<ResolvedField> fields;
    private readonly ImmutableArray<CounterfactualPlanCallSite> callSites;
    private readonly ImmutableArray<MethodGraphTraversalCharge> traversalCharges;
    private readonly ImmutableArray<CounterfactualFieldObservation> fieldObservations;
    private readonly ImmutableArray<byte> canonicalBytes;

    private CounterfactualMethodPlan(
        object issuer,
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph,
        MethodGraphTraversalAccounting traversalAccounting,
        CounterfactualRuntimeBundle<TMemory> runtimeBundle)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(traversalAccounting);
        ArgumentNullException.ThrowIfNull(runtimeBundle);
        if (!ReferenceEquals(request, runtimeBundle.Request) ||
            !runtimeBundle.HasMaterializedRootArguments ||
            runtimeBundle.RootArguments.Length != request.Arguments.Length + 1 ||
            !ReferenceEquals(runtimeBundle.RootArguments[0], runtimeBundle.Receiver) ||
            request.RootMethod != graph.Root ||
            request.LogicalDepthLimit < graph.RequiredLogicalDepth ||
            request.TraversalLimit != traversalAccounting.Limit ||
            traversalAccounting.IsExhausted ||
            traversalAccounting.RejectedCharge is not null ||
            traversalAccounting.Used != graph.TraversalUnitCount ||
            traversalAccounting.Remaining != traversalAccounting.Limit - traversalAccounting.Used)
        {
            throw new ArgumentException("The request, successful graph, and traversal accounting disagree.");
        }

        if (!graph.TryGetNode(graph.Root, out var rootNode) || rootNode is null ||
            !rootNode.Definition.Signature.HasImplicitThis ||
            rootNode.Definition.Signature.DeclaringType != request.Receiver.StaticType ||
            !rootNode.Definition.Signature.ParameterTypes.SequenceEqual(
                request.Arguments.Select(argument => argument.StaticType)))
        {
            throw new ArgumentException("The rooted receiver and ordered arguments disagree with the frozen root signature.", nameof(request));
        }

        var modeledDispositionMatchesRequest = request.RequiredModelTarget is { } modeledTarget
            ? graph.ModeledLeaves.Length == 1 && graph.ModeledLeaves[0].Method == modeledTarget
            : graph.ModeledLeaves.IsEmpty;
        if (!modeledDispositionMatchesRequest)
        {
            throw new ArgumentException(
                "The request's optional modeled target must exactly match the frozen graph's modeled disposition.",
                nameof(graph));
        }

        var copiedFieldObservations = ValidateFieldObservations(
            request,
            graph,
            runtimeBundle.FieldObservations);

        SchemaVersion = CanonicalSchemaVersion;
        this.issuer = issuer;
        Request = request;
        RuntimeGraph = graph;
        RuntimeTraversalAccounting = traversalAccounting;
        RootMethod = graph.Root;
        interpretedMethods = graph.Nodes.Select(static node => node.Method).ToImmutableArray();
        modeledMethods = graph.ModeledLeaves.Select(static leaf => leaf.Method).ToImmutableArray();
        fields = CounterfactualCanonical.Copy(graph.Fields);
        callSites = graph.CallSites.Select(static call => new CounterfactualPlanCallSite(call)).ToImmutableArray();
        TraversalLimit = traversalAccounting.Limit;
        TraversalUsed = traversalAccounting.Used;
        TraversalRemaining = traversalAccounting.Remaining;
        traversalCharges = CounterfactualCanonical.Copy(traversalAccounting.Charges);
        this.fieldObservations = copiedFieldObservations;
        RequiredLogicalDepth = graph.RequiredLogicalDepth;
        this.runtimeBundle = runtimeBundle;
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the canonical plan schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the exact canonical request from which this plan was prepared.</summary>
    public CounterfactualMethodRequest Request { get; }

    /// <summary>Gets the exact root MethodDef frozen into this plan.</summary>
    public MethodHandle RootMethod { get; }

    /// <summary>Gets a defensive copy of interpreted MethodDefs in canonical structural order.</summary>
    public ImmutableArray<MethodHandle> InterpretedMethods => CounterfactualCanonical.Copy(interpretedMethods);

    /// <summary>Gets a defensive copy of opaque modeled MethodDefs in canonical structural order.</summary>
    public ImmutableArray<MethodHandle> ModeledMethods => CounterfactualCanonical.Copy(modeledMethods);

    /// <summary>Gets a defensive copy of exact structural field dependencies in canonical order.</summary>
    public ImmutableArray<ResolvedField> Fields => CounterfactualCanonical.Copy(fields);

    /// <summary>Gets a defensive copy of ordered interpreted or modeled direct-call projections.</summary>
    public ImmutableArray<CounterfactualPlanCallSite> CallSites => CounterfactualCanonical.Copy(callSites);

    /// <summary>Gets the configured preparation-traversal limit.</summary>
    public int TraversalLimit { get; }

    /// <summary>Gets the traversal units consumed by successful preparation.</summary>
    public int TraversalUsed { get; }

    /// <summary>Gets the traversal units remaining after successful preparation.</summary>
    public int TraversalRemaining { get; }

    /// <summary>Gets a defensive copy of successful traversal charges in discovery order.</summary>
    public ImmutableArray<MethodGraphTraversalCharge> TraversalCharges =>
        CounterfactualCanonical.Copy(traversalCharges);

    /// <summary>Gets a defensive copy of the complete ordinal-aligned plan-relative field observations.</summary>
    public ImmutableArray<CounterfactualFieldObservation> FieldObservations =>
        CounterfactualCanonical.Copy(fieldObservations);

    /// <summary>Gets the graph's required logical call depth.</summary>
    public int RequiredLogicalDepth { get; }

    /// <summary>Gets a defensive copy of the domain-separated canonical plan bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CounterfactualCanonical.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal static CounterfactualMethodPlan<TMemory> Issue(
        object issuer,
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph,
        MethodGraphTraversalAccounting traversalAccounting,
        CounterfactualRuntimeBundle<TMemory> runtimeBundle) =>
        new(issuer, request, graph, traversalAccounting, runtimeBundle);

    internal bool IsIssuedBy(object candidate) => ReferenceEquals(issuer, candidate);

    internal FrozenMethodGraphPlan RuntimeGraph { get; }

    internal MethodGraphTraversalAccounting RuntimeTraversalAccounting { get; }

    internal ImmutableArray<CounterfactualFieldObservation> RuntimeFieldObservations => fieldObservations;

    internal CounterfactualRuntimeBundle<TMemory> RuntimeBundle => runtimeBundle;

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("Interpreter.CounterfactualMethodPlan");
        writer.WriteInt32(SchemaVersion);
        writer.WriteDigest(Request.Sha256);
        WriteGraph(writer, RuntimeGraph);
        WriteAccounting(writer, RuntimeTraversalAccounting);
        writer.WriteInt32(fieldObservations.Length);
        foreach (var observation in fieldObservations)
        {
            writer.WriteBytes(observation.CanonicalBytes.AsSpan());
        }

        return writer.ToImmutableArray();
    }

    private static ImmutableArray<CounterfactualFieldObservation> ValidateFieldObservations(
        CounterfactualMethodRequest request,
        FrozenMethodGraphPlan graph,
        ImmutableArray<CounterfactualFieldObservation> observations)
    {
        var copied = CounterfactualMethodExecutionInput<TMemory>.ValidateObservationBinding(request, observations);
        if (copied.Length != graph.Fields.Length || copied.Any(static observation => observation is null))
        {
            throw new ArgumentException(
                "A plan requires exactly one non-null observation per frozen graph field.",
                nameof(observations));
        }

        for (var index = 0; index < copied.Length; index++)
        {
            var observation = copied[index];
            if (observation.DependencyOrdinal != index ||
                observation.Field != graph.Fields[index] ||
                observation.Field.DeclaringType != request.Receiver.StaticType)
            {
                throw new ArgumentException(
                    "Field observations must match graph order, source identity, and imported receiver evidence.",
                    nameof(observations));
            }
        }

        return copied;
    }

    private static void WriteGraph(CounterfactualCanonicalWriter writer, FrozenMethodGraphPlan graph)
    {
        writer.WriteMethod(graph.Root);
        writer.WriteInt32(graph.RequiredLogicalDepth);
        writer.WriteInt32(graph.TraversalUnitCount);
        writer.WriteInt32(graph.Nodes.Length);
        foreach (var node in graph.Nodes)
        {
            writer.WriteMethod(node.Method);
            WriteDefinition(writer, node.Definition);
            WriteAdmission(writer, node.Admission);
            writer.WriteInt32(node.Fields.Length);
            foreach (var field in node.Fields)
            {
                WriteField(writer, field);
            }

            writer.WriteInt32(node.CallSites.Length);
            foreach (var call in node.CallSites)
            {
                WriteCall(writer, call);
            }
        }

        writer.WriteInt32(graph.ModeledLeaves.Length);
        foreach (var leaf in graph.ModeledLeaves)
        {
            WriteTarget(writer, leaf.Target);
            WriteDescriptor(writer, leaf.Descriptor);
        }

        writer.WriteInt32(graph.Fields.Length);
        foreach (var field in graph.Fields)
        {
            WriteField(writer, field);
        }

        writer.WriteInt32(graph.CallSites.Length);
        foreach (var call in graph.CallSites)
        {
            WriteCall(writer, call);
        }
    }

    private static void WriteDefinition(CounterfactualCanonicalWriter writer, ResolvedMethodDefinition definition)
    {
        writer.WriteMethod(definition.Method);
        writer.WriteInt32(definition.Body.MaxStack);
        writer.WriteBoolean(definition.Body.LocalVariablesInitialized);
        writer.WriteInt32(definition.Body.LocalSignatureToken);
        writer.WriteInt32(definition.Body.ExceptionRegionCount);
        writer.WriteBytes(definition.Body.CodeBytes.AsSpan());
        WriteSignature(writer, definition.Signature.CallSignature);
        writer.WriteInt32(definition.Signature.LocalTypes.Length);
        foreach (var local in definition.Signature.LocalTypes)
        {
            writer.WriteType(local);
        }
    }

    private static void WriteSignature(CounterfactualCanonicalWriter writer, MethodCallSignatureShape signature)
    {
        writer.WriteType(signature.DeclaringType);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(signature.CallingConvention));
        writer.WriteBoolean(signature.HasImplicitThis);
        writer.WriteBoolean(signature.HasExplicitThis);
        writer.WriteInt32(signature.GenericParameterCount);
        writer.WriteInt32(signature.ParameterTypes.Length);
        foreach (var parameter in signature.ParameterTypes)
        {
            writer.WriteType(parameter);
        }

        writer.WriteType(signature.ReturnType);
    }

    private static void WriteAdmission(CounterfactualCanonicalWriter writer, MethodAdmissionResult admission)
    {
        writer.WriteBoolean(admission.IsAdmitted);
        writer.WriteInt32(admission.InstructionCount);
        writer.WriteInt32(admission.InstructionBoundaries.Length);
        foreach (var boundary in admission.InstructionBoundaries)
        {
            writer.WriteInt32(boundary.IlOffset);
            writer.WriteInt32(boundary.ExpectedStackTypes.Length);
            foreach (var type in boundary.ExpectedStackTypes)
            {
                writer.WriteType(type);
            }
        }

        writer.WriteInt32(CounterfactualCanonicalTags.Tag(admission.FailureStatus));
        writer.WriteBoolean(admission.Failure is not null);
        if (admission.Failure is { } failure)
        {
            writer.WriteInt32(CounterfactualCanonicalTags.Tag(failure.Kind));
            writer.WriteString(failure.Code);
        }
    }

    private static void WriteField(CounterfactualCanonicalWriter writer, ResolvedField field)
    {
        writer.WriteField(field.Handle);
        writer.WriteType(field.DeclaringType);
        writer.WriteType(field.FieldType);
        writer.WriteBoolean(field.IsStatic);
        writer.WriteBoolean(field.IsLiteral);
        writer.WriteBoolean(field.HasRva);
    }

    private static void WriteCall(CounterfactualCanonicalWriter writer, FrozenMethodCallSite call)
    {
        writer.WriteMethod(call.Caller);
        writer.WriteInt32(call.IlOffset);
        writer.WriteInt32(call.MetadataToken);
        WriteTarget(writer, call.Target);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(call.Disposition));
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(call.Effects));
        writer.WriteBoolean(call.ModelDescriptor is not null);
        if (call.ModelDescriptor is { } descriptor)
        {
            WriteDescriptor(writer, descriptor);
        }
    }

    private static void WriteTarget(CounterfactualCanonicalWriter writer, ResolvedMethodCallTarget target)
    {
        writer.WriteMethod(target.Method);
        WriteSignature(writer, target.Signature);
        writer.WriteBoolean(target.IsManagedIl);
    }

    private static void WriteDescriptor(CounterfactualCanonicalWriter writer, PureCallModelDescriptor descriptor)
    {
        writer.WriteString(descriptor.Identity.StableId);
        writer.WriteVersion(descriptor.Identity.Version);
        WriteTarget(writer, descriptor.Target);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(descriptor.Confidence));
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(descriptor.Effects));
    }

    private static void WriteAccounting(
        CounterfactualCanonicalWriter writer,
        MethodGraphTraversalAccounting accounting)
    {
        writer.WriteInt32(accounting.Limit);
        writer.WriteInt32(accounting.Used);
        writer.WriteInt32(accounting.Remaining);
        writer.WriteBoolean(accounting.IsExhausted);
        writer.WriteInt32(accounting.Charges.Length);
        foreach (var charge in accounting.Charges)
        {
            writer.WriteInt32(charge.Ordinal);
            writer.WriteInt32(CounterfactualCanonicalTags.Tag(charge.Kind));
            writer.WriteMethod(charge.Method);
            writer.WriteBoolean(charge.Field.HasValue);
            if (charge.Field is { } field)
            {
                writer.WriteField(field);
            }

            writer.WriteInt32(charge.IlOffset);
            writer.WriteInt32(charge.RawMetadataToken);
        }

        writer.WriteBoolean(accounting.RejectedCharge is not null);
    }
}
