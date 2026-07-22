using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

internal sealed class MetadataSignatureProjectionAdapterOutcome
{
    private MetadataSignatureProjectionAdapterOutcome(
        MetadataSignatureDecodeResultKind kind,
        MetadataTypeSignatureNode? root,
        BoundedEcmaSignatureCertificate? coreCertificate,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        string? code)
    {
        Kind = kind;
        Root = root;
        CoreCertificate = coreCertificate;
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        Code = code;
    }

    internal MetadataSignatureDecodeResultKind Kind { get; }

    internal MetadataTypeSignatureNode? Root { get; }

    internal BoundedEcmaSignatureCertificate? CoreCertificate { get; }

    internal EvaluationDeterministicBound? ReachedBound { get; }

    internal int ObservedCount { get; }

    internal string? Code { get; }

    internal static MetadataSignatureProjectionAdapterOutcome Exact(
        MetadataTypeSignatureNode root,
        BoundedEcmaSignatureCertificate certificate) =>
        new(MetadataSignatureDecodeResultKind.Exact, root, certificate, null, 0, null);

    internal static MetadataSignatureProjectionAdapterOutcome BoundReached(
        EvaluationDeterministicBound reachedBound,
        int observedCount) =>
        new(MetadataSignatureDecodeResultKind.NonExact, null, null, reachedBound, observedCount, null);

    internal static MetadataSignatureProjectionAdapterOutcome Incomplete(string code) =>
        new(MetadataSignatureDecodeResultKind.NonExact, null, null, null, 0, code);

    internal static MetadataSignatureProjectionAdapterOutcome Invalid(string code) =>
        new(MetadataSignatureDecodeResultKind.Invalid, null, null, null, 0, code);
}

/// <summary>
/// Projects the shared Core signature event stream into Product metadata type nodes without owning signature grammar.
/// </summary>
internal static class MetadataSignatureProjectionAdapter
{
    internal static MetadataSignatureProjectionAdapterOutcome Decode(
        ImmutableArray<byte> signatureBytes,
        BoundedEcmaSignatureForm form,
        MetadataSignatureTokenResolutionCatalog tokenCatalog)
    {
        ArgumentNullException.ThrowIfNull(tokenCatalog);
        if (signatureBytes.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized signature is required.", nameof(signatureBytes));
        }
        if (form is not (BoundedEcmaSignatureForm.TypeSpecification or BoundedEcmaSignatureForm.Field))
        {
            throw new ArgumentOutOfRangeException(nameof(form));
        }

        var limits = CreateLimits(form);
        var sink = new NodeSink();
        var coreOutcome = BoundedEcmaSignatureProjection.Decode(
            signatureBytes.AsSpan(),
            form,
            limits,
            sink);
        if (coreOutcome.Kind == BoundedEcmaSignatureDecodeKind.BoundReached)
        {
            return MapBound(coreOutcome.ReachedBound, coreOutcome.Counters, form);
        }
        if (coreOutcome.Kind == BoundedEcmaSignatureDecodeKind.Invalid)
        {
            return MetadataSignatureProjectionAdapterOutcome.Invalid(MapFailure(coreOutcome.Failure));
        }
        if (coreOutcome.Certificate is not { } certificate ||
            certificate.Form != form ||
            certificate.SignatureByteCount != signatureBytes.Length ||
            certificate.Counters.ConsumedByteCount != signatureBytes.Length)
        {
            return MetadataSignatureProjectionAdapterOutcome.Invalid("W8_SIGNATURE_CORE_CERTIFICATE_INVALID");
        }

        var events = sink.ToImmutable();
        if (TryFindProductTopologyBound(events) is { } topologyBound)
        {
            return topologyBound;
        }

        // A non-exact catalog cannot be used as a resolution-map prefix. Grammar is nevertheless established first,
        // so malformed bytes never acquire a misleading catalog-bound classification.
        if (tokenCatalog.ResultKind == MetadataSignatureTokenResolutionCatalogResultKind.NonExact)
        {
            return MetadataSignatureProjectionAdapterOutcome.BoundReached(
                tokenCatalog.ReachedBound!,
                tokenCatalog.ObservedCount);
        }

        try
        {
            var context = ProjectionContext.Create(
                signatureBytes,
                events,
                tokenCatalog,
                limits,
                certificate);
            return MetadataSignatureProjectionAdapterOutcome.Exact(context.ProjectRoot(), certificate);
        }
        catch (ProjectionStopException exception)
        {
            return exception.Kind == MetadataSignatureDecodeResultKind.NonExact
                ? MetadataSignatureProjectionAdapterOutcome.Incomplete(exception.Code)
                : MetadataSignatureProjectionAdapterOutcome.Invalid(exception.Code);
        }
        catch (ArgumentException)
        {
            return MetadataSignatureProjectionAdapterOutcome.Invalid("W8_SIGNATURE_SHAPE_INVALID");
        }
        catch (OverflowException)
        {
            return MetadataSignatureProjectionAdapterOutcome.Invalid("W8_SIGNATURE_COUNTER_OVERFLOW");
        }
    }

    private static BoundedEcmaSignatureLimits CreateLimits(BoundedEcmaSignatureForm form) =>
        new(
            form == BoundedEcmaSignatureForm.Field
                ? StaticFieldFieldSignatureLimits.MaximumFieldSignatureLength
                : StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
            StaticFieldV2Limits.MaximumTypeSpecificationDepth,
            StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
            StaticFieldV2Limits.MaximumGenericParameterCount,
            StaticFieldV2Limits.MaximumParameterCount,
            StaticFieldV2Limits.MaximumLocalCount,
            StaticFieldV2Limits.MaximumArrayRank);

    private static MetadataSignatureProjectionAdapterOutcome MapBound(
        BoundedEcmaSignatureBoundKind boundKind,
        BoundedEcmaSignatureCounters counters,
        BoundedEcmaSignatureForm form)
    {
        var (name, limit, observed) = boundKind switch
        {
            BoundedEcmaSignatureBoundKind.ByteCount => (
                form == BoundedEcmaSignatureForm.Field
                    ? StaticFieldFieldSignatureLimits.MaximumFieldSignatureLengthBoundName
                    : ExpressionV2ContractLimits.TypeSpecificationByteCountBoundName,
                form == BoundedEcmaSignatureForm.Field
                    ? StaticFieldFieldSignatureLimits.MaximumFieldSignatureLength
                    : StaticFieldV2Limits.MaximumTypeSpecificationByteCount,
                counters.InputByteCount),
            BoundedEcmaSignatureBoundKind.RecursiveDepth => (
                ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationDepth,
                counters.MaximumObservedDepth),
            BoundedEcmaSignatureBoundKind.AggregateTypeCount => (
                ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName,
                StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount,
                counters.AggregateTypeCount),
            BoundedEcmaSignatureBoundKind.AggregateGenericArgumentCount => (
                ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
                StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
                counters.AggregateGenericArgumentCount),
            BoundedEcmaSignatureBoundKind.GenericParameterCount => (
                ExpressionV2ContractLimits.GenericParameterCountBoundName,
                StaticFieldV2Limits.MaximumGenericParameterCount,
                counters.MaximumDeclaredGenericParameterCount),
            BoundedEcmaSignatureBoundKind.ParameterCount => (
                ExpressionV2ContractLimits.ParameterCountBoundName,
                StaticFieldV2Limits.MaximumParameterCount,
                counters.MaximumDeclaredParameterCount),
            BoundedEcmaSignatureBoundKind.ArrayRank => (
                ExpressionV2ContractLimits.ArrayRankBoundName,
                StaticFieldV2Limits.MaximumArrayRank,
                counters.MaximumDeclaredArrayRank),
            _ => throw new InvalidOperationException("The Core bound is not applicable to Product type projection."),
        };
        return MetadataSignatureProjectionAdapterOutcome.BoundReached(
            new EvaluationDeterministicBound(name, limit),
            SaturatedCapPlusOne(observed, limit));
    }

    private static int SaturatedCapPlusOne(int observed, int cap) =>
        Math.Min(Math.Max(observed, cap + 1), cap + 1);

    private static MetadataSignatureProjectionAdapterOutcome? TryFindProductTopologyBound(
        ImmutableArray<BoundedEcmaSignatureNodeEvent> events)
    {
        if (events.IsDefaultOrEmpty)
        {
            return null;
        }

        var retained = new bool[events.Length];
        var retainedDepth = new int[events.Length];
        var retainedCount = 0;
        var maximumDepth = 0;
        foreach (var node in events)
        {
            var isGenericHead = node.ParentNodeOrdinal >= 0 &&
                events[node.ParentNodeOrdinal].Kind == BoundedEcmaSignatureNodeKind.GenericInstantiation &&
                (node.Kind is BoundedEcmaSignatureNodeKind.Class or BoundedEcmaSignatureNodeKind.ValueType) &&
                node.Count > 0;
            retained[node.NodeOrdinal] = !isGenericHead && node.Kind is not (
                BoundedEcmaSignatureNodeKind.ArrayShape or
                BoundedEcmaSignatureNodeKind.ArraySize or
                BoundedEcmaSignatureNodeKind.ArrayLowerBound);
            if (!retained[node.NodeOrdinal])
            {
                continue;
            }

            var retainedParent = node.ParentNodeOrdinal;
            while (retainedParent >= 0 && !retained[retainedParent])
            {
                retainedParent = events[retainedParent].ParentNodeOrdinal;
            }
            var depth = retainedParent < 0 ? 1 : retainedDepth[retainedParent] + 1;
            retainedDepth[node.NodeOrdinal] = depth;
            retainedCount++;
            maximumDepth = Math.Max(maximumDepth, depth);
        }

        if (retainedCount > StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount)
        {
            return MetadataSignatureProjectionAdapterOutcome.BoundReached(
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.RawTypeSignatureNodeCountBoundName,
                    StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount),
                StaticFieldV2Limits.MaximumRawTypeSignatureNodeCount + 1);
        }
        if (maximumDepth > StaticFieldV2Limits.MaximumTypeSpecificationDepth)
        {
            return MetadataSignatureProjectionAdapterOutcome.BoundReached(
                new EvaluationDeterministicBound(
                    ExpressionV2ContractLimits.TypeSpecificationDepthBoundName,
                    StaticFieldV2Limits.MaximumTypeSpecificationDepth),
                StaticFieldV2Limits.MaximumTypeSpecificationDepth + 1);
        }
        return null;
    }

    private static string MapFailure(BoundedEcmaSignatureFailureKind failure) => failure switch
    {
        BoundedEcmaSignatureFailureKind.EmptyInput => "W8_SIGNATURE_EMPTY",
        BoundedEcmaSignatureFailureKind.UnexpectedEnd => "W8_SIGNATURE_TRUNCATED",
        BoundedEcmaSignatureFailureKind.InvalidHeader => "W8_SIGNATURE_HEADER_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidCallingConvention => "W8_SIGNATURE_FNPTR_HEADER_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidReceiverFlags => "W8_SIGNATURE_FNPTR_HEADER_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidGenericHeader => "W8_SIGNATURE_FNPTR_HEADER_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidCompressedInteger => "W8_SIGNATURE_COMPRESSED_INTEGER_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidCount => "W8_SIGNATURE_COUNT_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidElementType => "W8_SIGNATURE_ELEMENT_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidTypePlacement => "W8_SIGNATURE_TYPE_PLACEMENT_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidMetadataToken => "W8_SIGNATURE_TYPESPEC_HEAD_INVALID",
        BoundedEcmaSignatureFailureKind.InvalidArrayShape => "W8_SIGNATURE_ARRAY_SHAPE_INVALID",
        BoundedEcmaSignatureFailureKind.SentinelNotPermitted => "W8_SIGNATURE_FNPTR_SENTINEL_INVALID",
        BoundedEcmaSignatureFailureKind.TrailingData => "W8_SIGNATURE_TRAILING_BYTES",
        _ => "W8_SIGNATURE_CORE_INVALID",
    };

    private sealed class NodeSink : IBoundedEcmaSignatureNodeSink
    {
        private readonly ImmutableArray<BoundedEcmaSignatureNodeEvent>.Builder nodes =
            ImmutableArray.CreateBuilder<BoundedEcmaSignatureNodeEvent>();

        public void Add(in BoundedEcmaSignatureNodeEvent node) => nodes.Add(node);

        internal ImmutableArray<BoundedEcmaSignatureNodeEvent> ToImmutable() => nodes.ToImmutable();
    }

    private sealed class ProjectionContext
    {
        private readonly ImmutableArray<byte> signatureBytes;
        private readonly ImmutableArray<BoundedEcmaSignatureNodeEvent> events;
        private readonly ImmutableArray<ImmutableArray<int>> childOrdinals;
        private readonly MetadataSignatureTokenResolutionCatalog tokenCatalog;
        private readonly BoundedEcmaSignatureLimits limits;
        private readonly BoundedEcmaSignatureCertificate certificate;
        private readonly int rootOrdinal;

        private ProjectionContext(
            ImmutableArray<byte> signatureBytes,
            ImmutableArray<BoundedEcmaSignatureNodeEvent> events,
            ImmutableArray<ImmutableArray<int>> childOrdinals,
            MetadataSignatureTokenResolutionCatalog tokenCatalog,
            BoundedEcmaSignatureLimits limits,
            BoundedEcmaSignatureCertificate certificate,
            int rootOrdinal)
        {
            this.signatureBytes = signatureBytes;
            this.events = events;
            this.childOrdinals = childOrdinals;
            this.tokenCatalog = tokenCatalog;
            this.limits = limits;
            this.certificate = certificate;
            this.rootOrdinal = rootOrdinal;
        }

        internal static ProjectionContext Create(
            ImmutableArray<byte> signatureBytes,
            ImmutableArray<BoundedEcmaSignatureNodeEvent> events,
            MetadataSignatureTokenResolutionCatalog tokenCatalog,
            BoundedEcmaSignatureLimits limits,
            BoundedEcmaSignatureCertificate certificate)
        {
            if (events.IsDefaultOrEmpty || events.Length != certificate.Counters.ProjectedNodeCount)
            {
                throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
            }

            var children = new ImmutableArray<int>.Builder[events.Length];
            for (var ordinal = 0; ordinal < children.Length; ordinal++)
            {
                children[ordinal] = ImmutableArray.CreateBuilder<int>();
            }

            var rootOrdinal = -1;
            foreach (var node in events)
            {
                if (node.NodeOrdinal < 0 ||
                    node.NodeOrdinal >= events.Length ||
                    events[node.NodeOrdinal] != node ||
                    node.SignatureOffset < 0 ||
                    node.SignatureOffset >= signatureBytes.Length)
                {
                    throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
                }
                if (node.ParentNodeOrdinal == -1)
                {
                    if (rootOrdinal >= 0)
                    {
                        throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
                    }
                    rootOrdinal = node.NodeOrdinal;
                }
                else
                {
                    if (node.ParentNodeOrdinal < 0 || node.ParentNodeOrdinal >= node.NodeOrdinal)
                    {
                        throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
                    }
                    children[node.ParentNodeOrdinal].Add(node.NodeOrdinal);
                }
            }
            if (rootOrdinal < 0)
            {
                throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
            }

            return new ProjectionContext(
                signatureBytes,
                events,
                children.Select(static builder => builder.ToImmutable()).ToImmutableArray(),
                tokenCatalog,
                limits,
                certificate,
                rootOrdinal);
        }

        internal MetadataTypeSignatureNode ProjectRoot() => Project(rootOrdinal);

        private MetadataTypeSignatureNode Project(int ordinal)
        {
            var node = events[ordinal];
            var children = childOrdinals[ordinal];
            return node.Kind switch
            {
                BoundedEcmaSignatureNodeKind.Primitive => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.Primitive(MapPrimitive(node.ElementType))),
                BoundedEcmaSignatureNodeKind.String => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.String)),
                BoundedEcmaSignatureNodeKind.Object => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.Primitive(MetadataPrimitiveTypeKind.Object)),
                BoundedEcmaSignatureNodeKind.Class => ProjectNamed(node, children, MetadataNamedSignatureHeadKind.Class),
                BoundedEcmaSignatureNodeKind.ValueType => ProjectNamed(
                    node,
                    children,
                    MetadataNamedSignatureHeadKind.ValueType),
                BoundedEcmaSignatureNodeKind.OwnerTypeParameter => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.OwnerTypeParameter(node.Index)),
                BoundedEcmaSignatureNodeKind.MethodTypeParameter => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.MethodTypeParameter(node.Index)),
                BoundedEcmaSignatureNodeKind.Pointer => MetadataTypeSignatureNode.Pointer(ProjectOnlyChild(children)),
                BoundedEcmaSignatureNodeKind.ByReference => MetadataTypeSignatureNode.ByReference(ProjectOnlyChild(children)),
                BoundedEcmaSignatureNodeKind.SzArray => MetadataTypeSignatureNode.SzArray(ProjectOnlyChild(children)),
                BoundedEcmaSignatureNodeKind.MultidimensionalArray => ProjectArray(children),
                BoundedEcmaSignatureNodeKind.GenericInstantiation => ProjectGenericInstantiation(children),
                BoundedEcmaSignatureNodeKind.FunctionPointer => ProjectFunctionPointer(node, children),
                BoundedEcmaSignatureNodeKind.TypedByReference => RequireLeaf(
                    children,
                    MetadataTypeSignatureNode.TypedByReference()),
                BoundedEcmaSignatureNodeKind.Void => RequireLeaf(children, MetadataTypeSignatureNode.Void()),
                BoundedEcmaSignatureNodeKind.RequiredModifier => ProjectModifier(node, children, required: true),
                BoundedEcmaSignatureNodeKind.OptionalModifier => ProjectModifier(node, children, required: false),
                _ => throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID"),
            };
        }

        private MetadataTypeSignatureNode ProjectNamed(
            BoundedEcmaSignatureNodeEvent node,
            ImmutableArray<int> children,
            MetadataNamedSignatureHeadKind headKind)
        {
            if (!children.IsEmpty)
            {
                throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
            }
            var entry = Resolve(node.MetadataToken);
            if (entry.NamedTarget is null)
            {
                throw Invalid("W8_SIGNATURE_TYPESPEC_HEAD_INVALID");
            }
            return MetadataTypeSignatureNode.Named(headKind, entry.NamedTarget, entry.DefinitionChain);
        }

        private MetadataTypeSignatureNode ProjectGenericInstantiation(ImmutableArray<int> children)
        {
            if (children.Length < 2 ||
                events[children[0]].Kind is not (
                    BoundedEcmaSignatureNodeKind.Class or BoundedEcmaSignatureNodeKind.ValueType) ||
                events[children[0]].Count != children.Length - 1)
            {
                throw Invalid("W8_SIGNATURE_GENERIC_HEAD_INVALID");
            }
            var head = Project(children[0]);
            var arguments = children.Skip(1).Select(Project).ToImmutableArray();
            return MetadataTypeSignatureNode.GenericInstantiation(head, arguments);
        }

        private MetadataTypeSignatureNode ProjectArray(ImmutableArray<int> children)
        {
            if (children.Length != 2)
            {
                throw Invalid("W8_SIGNATURE_ARRAY_SHAPE_INVALID");
            }
            var shapeOrdinal = children.SingleOrDefault(
                child => events[child].Kind == BoundedEcmaSignatureNodeKind.ArrayShape,
                -1);
            if (shapeOrdinal < 0)
            {
                throw Invalid("W8_SIGNATURE_ARRAY_SHAPE_INVALID");
            }
            var elementOrdinal = children[0] == shapeOrdinal ? children[1] : children[0];
            var shape = events[shapeOrdinal];
            var sizes = ImmutableArray.CreateBuilder<int>();
            var lowerBounds = ImmutableArray.CreateBuilder<int>();
            foreach (var componentOrdinal in childOrdinals[shapeOrdinal])
            {
                var component = events[componentOrdinal];
                if (!childOrdinals[componentOrdinal].IsEmpty)
                {
                    throw Invalid("W8_SIGNATURE_ARRAY_SHAPE_INVALID");
                }
                if (component.Kind == BoundedEcmaSignatureNodeKind.ArraySize && component.Index == sizes.Count)
                {
                    sizes.Add(component.Value);
                }
                else if (component.Kind == BoundedEcmaSignatureNodeKind.ArrayLowerBound &&
                         component.Index == lowerBounds.Count)
                {
                    lowerBounds.Add(component.Value);
                }
                else
                {
                    throw Invalid("W8_SIGNATURE_ARRAY_SHAPE_INVALID");
                }
            }
            return MetadataTypeSignatureNode.MultidimensionalArray(
                Project(elementOrdinal),
                shape.Count,
                sizes.ToImmutable(),
                lowerBounds.ToImmutable());
        }

        private MetadataTypeSignatureNode ProjectFunctionPointer(
            BoundedEcmaSignatureNodeEvent node,
            ImmutableArray<int> children)
        {
            if (children.Length != node.Count + 1)
            {
                throw Invalid("W8_SIGNATURE_FNPTR_SHAPE_INVALID");
            }
            var projectedChildren = children.Select(Project).ToImmutableArray();
            return MetadataTypeSignatureNode.FunctionPointerDecoded(
                FindFunctionPointerBytes(node.NodeOrdinal),
                node.Header,
                node.Index,
                node.Count,
                projectedChildren);
        }

        private MetadataTypeSignatureNode ProjectModifier(
            BoundedEcmaSignatureNodeEvent node,
            ImmutableArray<int> children,
            bool required)
        {
            var underlying = ProjectOnlyChild(children);
            var entry = Resolve(node.MetadataToken);
            if (entry.TypeSpecificationReference is { } reference)
            {
                return required
                    ? MetadataTypeSignatureNode.RequiredModifier(reference, underlying)
                    : MetadataTypeSignatureNode.OptionalModifier(reference, underlying);
            }
            if (entry.NamedTarget is null)
            {
                throw Invalid("W8_SIGNATURE_MODIFIER_TOKEN_INVALID");
            }
            return required
                ? MetadataTypeSignatureNode.RequiredModifier(entry.NamedTarget, underlying)
                : MetadataTypeSignatureNode.OptionalModifier(entry.NamedTarget, underlying);
        }

        private MetadataSignatureTokenResolutionEntry Resolve(int token)
        {
            var entry = tokenCatalog.Resolve(token);
            if (entry is not null)
            {
                return entry;
            }
            throw tokenCatalog.ContainsSourceToken(token)
                ? Incomplete("W8_SIGNATURE_TOKEN_RESOLUTION_INCOMPLETE")
                : Invalid("W8_SIGNATURE_TOKEN_OUT_OF_RANGE");
        }

        private MetadataTypeSignatureNode ProjectOnlyChild(ImmutableArray<int> children)
        {
            if (children.Length != 1)
            {
                throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
            }
            return Project(children[0]);
        }

        private ImmutableArray<byte> FindFunctionPointerBytes(int functionPointerOrdinal)
        {
            var functionPointerOffset = events[functionPointerOrdinal].SignatureOffset;
            var subtreeEndOrdinal = functionPointerOrdinal;
            for (var ordinal = functionPointerOrdinal + 1; ordinal < events.Length; ordinal++)
            {
                if (IsDescendantOf(ordinal, functionPointerOrdinal))
                {
                    subtreeEndOrdinal = ordinal;
                }
            }
            var minimumEnd = events
                .Skip(functionPointerOrdinal)
                .Take(subtreeEndOrdinal - functionPointerOrdinal + 1)
                .Max(static node => node.SignatureOffset + 1);
            var maximumEnd = subtreeEndOrdinal + 1 < events.Length
                ? events[subtreeEndOrdinal + 1].SignatureOffset
                : signatureBytes.Length;
            maximumEnd = Math.Max(minimumEnd, maximumEnd);
            for (var candidateEnd = minimumEnd; candidateEnd <= maximumEnd; candidateEnd++)
            {
                var candidate = signatureBytes.AsSpan(functionPointerOffset, candidateEnd - functionPointerOffset);
                var outcome = BoundedEcmaSignatureProjection.Decode(
                    candidate,
                    BoundedEcmaSignatureForm.TypeSpecification,
                    limits);
                if (outcome.IsExact)
                {
                    return signatureBytes.Slice(functionPointerOffset, candidate.Length);
                }
            }
            throw Invalid("W8_SIGNATURE_FNPTR_SPAN_INVALID");
        }

        private bool IsDescendantOf(int ordinal, int ancestorOrdinal)
        {
            var parent = events[ordinal].ParentNodeOrdinal;
            while (parent >= 0)
            {
                if (parent == ancestorOrdinal)
                {
                    return true;
                }
                parent = events[parent].ParentNodeOrdinal;
            }
            return false;
        }

        private static MetadataPrimitiveTypeKind MapPrimitive(int elementType) => elementType switch
        {
            0x02 => MetadataPrimitiveTypeKind.Boolean,
            0x03 => MetadataPrimitiveTypeKind.Char,
            0x04 => MetadataPrimitiveTypeKind.Int8,
            0x05 => MetadataPrimitiveTypeKind.UInt8,
            0x06 => MetadataPrimitiveTypeKind.Int16,
            0x07 => MetadataPrimitiveTypeKind.UInt16,
            0x08 => MetadataPrimitiveTypeKind.Int32,
            0x09 => MetadataPrimitiveTypeKind.UInt32,
            0x0A => MetadataPrimitiveTypeKind.Int64,
            0x0B => MetadataPrimitiveTypeKind.UInt64,
            0x0C => MetadataPrimitiveTypeKind.Single,
            0x0D => MetadataPrimitiveTypeKind.Double,
            0x18 => MetadataPrimitiveTypeKind.NativeInt,
            0x19 => MetadataPrimitiveTypeKind.NativeUInt,
            _ => throw Invalid("W8_SIGNATURE_PRIMITIVE_INVALID"),
        };

        private static MetadataTypeSignatureNode RequireLeaf(
            ImmutableArray<int> children,
            MetadataTypeSignatureNode leaf)
        {
            if (!children.IsEmpty)
            {
                throw Invalid("W8_SIGNATURE_EVENT_STREAM_INVALID");
            }
            return leaf;
        }
    }

    private static ProjectionStopException Invalid(string code) =>
        new(MetadataSignatureDecodeResultKind.Invalid, code);

    private static ProjectionStopException Incomplete(string code) =>
        new(MetadataSignatureDecodeResultKind.NonExact, code);

    private sealed class ProjectionStopException : Exception
    {
        internal ProjectionStopException(MetadataSignatureDecodeResultKind kind, string code)
        {
            Kind = kind;
            Code = code;
        }

        internal MetadataSignatureDecodeResultKind Kind { get; }

        internal string Code { get; }
    }
}
