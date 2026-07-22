using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

internal readonly record struct BoundedEcmaMethodSignature(
    byte CallingConvention,
    bool HasThis,
    bool HasExplicitThis,
    int GenericParameterCount,
    int ParameterCount,
    int AggregateTypeCount,
    int MaximumObservedDepth);

internal readonly record struct BoundedEcmaLocalSignature(
    int LocalSlotCount,
    int AggregateTypeCount,
    int MaximumObservedDepth);

internal enum BoundedEcmaSignatureForm
{
    TypeSpecification,
    Field,
    MethodDefinition,
    LocalVariables,
}

internal enum BoundedEcmaSignatureDecodeKind
{
    Exact,
    Invalid,
    BoundReached,
}

internal enum BoundedEcmaSignatureFailureKind
{
    None,
    InvalidLimits,
    EmptyInput,
    UnexpectedEnd,
    InvalidHeader,
    InvalidCallingConvention,
    InvalidReceiverFlags,
    InvalidGenericHeader,
    InvalidCompressedInteger,
    InvalidCount,
    InvalidElementType,
    InvalidTypePlacement,
    InvalidMetadataToken,
    InvalidArrayShape,
    SentinelNotPermitted,
    TrailingData,
}

internal enum BoundedEcmaSignatureBoundKind
{
    None,
    ByteCount,
    RecursiveDepth,
    AggregateTypeCount,
    AggregateGenericArgumentCount,
    GenericParameterCount,
    ParameterCount,
    LocalSlotCount,
    ArrayRank,
}

internal enum BoundedEcmaSignatureNodeKind
{
    Primitive,
    String,
    Object,
    Class,
    ValueType,
    OwnerTypeParameter,
    MethodTypeParameter,
    Pointer,
    ByReference,
    SzArray,
    MultidimensionalArray,
    GenericInstantiation,
    FunctionPointer,
    TypedByReference,
    Void,
    RequiredModifier,
    OptionalModifier,
    Pinned,
    ArrayShape,
    ArraySize,
    ArrayLowerBound,
}

internal readonly record struct BoundedEcmaSignatureLimits(
    int MaximumSignatureLength,
    int MaximumDepth,
    int MaximumAggregateTypeCount,
    int MaximumAggregateGenericArgumentCount,
    int MaximumGenericParameterCount,
    int MaximumParameterCount,
    int MaximumLocalSlotCount,
    int MaximumArrayRank)
{
    internal BoundedEcmaSignatureLimits(
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        int maximumAggregateGenericArgumentCount)
        : this(
            maximumSignatureLength,
            maximumDepth,
            maximumAggregateTypeCount,
            maximumAggregateGenericArgumentCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount)
    {
    }

    internal BoundedEcmaSignatureLimits(
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        int maximumAggregateGenericArgumentCount,
        int maximumGenericParameterCount,
        int maximumParameterCount,
        int maximumLocalSlotCount)
        : this(
            maximumSignatureLength,
            maximumDepth,
            maximumAggregateTypeCount,
            maximumAggregateGenericArgumentCount,
            maximumGenericParameterCount,
            maximumParameterCount,
            maximumLocalSlotCount,
            maximumAggregateTypeCount)
    {
    }

    internal static BoundedEcmaSignatureLimits Create(
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount) =>
        new(
            maximumSignatureLength,
            maximumDepth,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount,
            maximumAggregateTypeCount);

    internal bool ArePositive =>
        MaximumSignatureLength > 0 &&
        MaximumDepth > 0 &&
        MaximumAggregateTypeCount > 0 &&
        MaximumAggregateGenericArgumentCount > 0 &&
        MaximumGenericParameterCount > 0 &&
        MaximumParameterCount > 0 &&
        MaximumLocalSlotCount > 0 &&
        MaximumArrayRank > 0;
}

internal readonly record struct BoundedEcmaSignatureCounters(
    int InputByteCount,
    int ConsumedByteCount,
    int AggregateTypeCount,
    int AggregateGenericArgumentCount,
    int MaximumObservedDepth,
    int ProjectedNodeCount,
    int MaximumDeclaredGenericParameterCount,
    int MaximumDeclaredParameterCount,
    int MaximumDeclaredLocalSlotCount,
    int MaximumDeclaredArrayRank);

internal readonly record struct BoundedEcmaSignatureNodeEvent(
    int NodeOrdinal,
    int ParentNodeOrdinal,
    int SignatureOffset,
    int SignatureDepth,
    BoundedEcmaSignatureNodeKind Kind,
    int ElementType,
    int MetadataToken,
    int Header,
    int Index,
    int Count,
    int Value);

internal interface IBoundedEcmaSignatureNodeSink
{
    void Add(in BoundedEcmaSignatureNodeEvent node);
}

internal readonly record struct BoundedEcmaSignatureCertificate(
    BoundedEcmaSignatureForm Form,
    int SignatureByteCount,
    int Header,
    int CallingConvention,
    bool HasThis,
    bool HasExplicitThis,
    int GenericParameterCount,
    int ParameterCount,
    int LocalSlotCount,
    BoundedEcmaSignatureCounters Counters);

internal readonly record struct BoundedEcmaSignatureDecodeOutcome(
    BoundedEcmaSignatureDecodeKind Kind,
    BoundedEcmaSignatureFailureKind Failure,
    BoundedEcmaSignatureBoundKind ReachedBound,
    BoundedEcmaSignatureCounters Counters,
    BoundedEcmaSignatureCertificate? Certificate)
{
    internal bool IsExact => Kind == BoundedEcmaSignatureDecodeKind.Exact;
}

/// <summary>
/// Performs bounded, byte-only structural validation of ECMA-335 type, field, MethodDef, and local signatures.
/// </summary>
/// <remarks>
/// This draft reader deliberately validates signature structure without resolving referenced metadata rows. It accepts
/// the runtime's documented signature augmentations: TypeSpec tokens in custom modifiers, custom modifiers at recursive
/// type positions, and the modern unmanaged function-pointer calling convention. Named CLASS/VALUETYPE nodes still
/// require TypeDef or TypeRef coded indices. Every entry point requires complete input consumption and applies explicit
/// byte-length, recursive-depth, aggregate-type-node, and aggregate-generic-argument limits before returning a
/// projection. The typed entry point can stream parent-indexed nodes into a caller-owned sink; omitting the sink keeps
/// the common host path allocation-free.
/// The local reader admits the runtime-compatible empty <c>07 00</c> signature even though the ECMA-335 sixth-edition
/// prose describes a positive local count; current System.Reflection.Metadata encoders accept zero.
/// The MethodDefSig reader has no MethodAttributes input; its caller must additionally require <c>HasThis</c> exactly
/// when the owning MethodDef is non-static.
/// </remarks>
internal static class BoundedEcmaSignatureProjection
{
    private const byte CallingConventionMask = 0x0F;
    private const byte CallingConventionDefault = 0x00;
    private const byte CallingConventionCDecl = 0x01;
    private const byte CallingConventionStandardCall = 0x02;
    private const byte CallingConventionThisCall = 0x03;
    private const byte CallingConventionFastCall = 0x04;
    private const byte CallingConventionVariableArguments = 0x05;
    private const byte CallingConventionUnmanaged = 0x09;
    private const byte CallingConventionGeneric = 0x10;
    private const byte CallingConventionHasThis = 0x20;
    private const byte CallingConventionExplicitThis = 0x40;
    private const byte CallingConventionReserved = 0x80;

    private const byte ElementTypeVoid = 0x01;
    private const byte ElementTypeBoolean = 0x02;
    private const byte ElementTypeChar = 0x03;
    private const byte ElementTypeI1 = 0x04;
    private const byte ElementTypeU1 = 0x05;
    private const byte ElementTypeI2 = 0x06;
    private const byte ElementTypeU2 = 0x07;
    private const byte ElementTypeI4 = 0x08;
    private const byte ElementTypeU4 = 0x09;
    private const byte ElementTypeI8 = 0x0A;
    private const byte ElementTypeU8 = 0x0B;
    private const byte ElementTypeR4 = 0x0C;
    private const byte ElementTypeR8 = 0x0D;
    private const byte ElementTypeString = 0x0E;
    private const byte ElementTypePointer = 0x0F;
    private const byte ElementTypeByReference = 0x10;
    private const byte ElementTypeValueType = 0x11;
    private const byte ElementTypeClass = 0x12;
    private const byte ElementTypeVar = 0x13;
    private const byte ElementTypeArray = 0x14;
    private const byte ElementTypeGenericInstance = 0x15;
    private const byte ElementTypeTypedByReference = 0x16;
    private const byte ElementTypeI = 0x18;
    private const byte ElementTypeU = 0x19;
    private const byte ElementTypeFunctionPointer = 0x1B;
    private const byte ElementTypeObject = 0x1C;
    private const byte ElementTypeSzArray = 0x1D;
    private const byte ElementTypeMVar = 0x1E;
    private const byte ElementTypeRequiredModifier = 0x1F;
    private const byte ElementTypeOptionalModifier = 0x20;
    private const byte ElementTypeSentinel = 0x41;
    private const byte ElementTypePinned = 0x45;

    private const byte FieldSignatureHeader = 0x06;
    private const byte LocalSignatureHeader = 0x07;

    internal static BoundedEcmaSignatureDecodeOutcome Decode(
        ReadOnlySpan<byte> signature,
        BoundedEcmaSignatureForm form,
        BoundedEcmaSignatureLimits limits,
        IBoundedEcmaSignatureNodeSink? nodeSink = null)
    {
        if (!limits.ArePositive)
        {
            return CreateEarlyInvalid(signature.Length, BoundedEcmaSignatureFailureKind.InvalidLimits);
        }

        if (signature.IsEmpty)
        {
            return CreateEarlyInvalid(0, BoundedEcmaSignatureFailureKind.EmptyInput);
        }

        if (signature.Length > limits.MaximumSignatureLength)
        {
            return new BoundedEcmaSignatureDecodeOutcome(
                BoundedEcmaSignatureDecodeKind.BoundReached,
                BoundedEcmaSignatureFailureKind.None,
                BoundedEcmaSignatureBoundKind.ByteCount,
                new BoundedEcmaSignatureCounters(signature.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                null);
        }

        var reader = new Reader(
            signature,
            limits.MaximumDepth,
            limits.MaximumAggregateTypeCount,
            limits.MaximumAggregateGenericArgumentCount,
            limits.MaximumGenericParameterCount,
            limits.MaximumParameterCount,
            limits.MaximumLocalSlotCount,
            limits.MaximumArrayRank,
            retainReferencedTypeTokens: false,
            nodeSink);

        var header = -1;
        byte callingConvention = 0;
        var hasThis = false;
        var hasExplicitThis = false;
        var genericParameterCount = 0;
        var parameterCount = 0;
        var localSlotCount = 0;
        var decoded = form switch
        {
            BoundedEcmaSignatureForm.TypeSpecification => reader.TryReadTypeSpecification(),
            BoundedEcmaSignatureForm.Field => reader.TryReadFieldSignature(out header),
            BoundedEcmaSignatureForm.MethodDefinition => reader.TryReadMethodDefinitionSignature(
                out header,
                out callingConvention,
                out hasThis,
                out hasExplicitThis,
                out genericParameterCount,
                out parameterCount),
            BoundedEcmaSignatureForm.LocalVariables => reader.TryReadLocalSignature(out header, out localSlotCount),
            _ => false,
        };

        if (decoded && !reader.AtEnd)
        {
            reader.Fail(BoundedEcmaSignatureFailureKind.TrailingData);
            decoded = false;
        }

        var counters = reader.CreateCounters();
        if (!decoded)
        {
            return reader.ReachedBound == BoundedEcmaSignatureBoundKind.None
                ? new BoundedEcmaSignatureDecodeOutcome(
                    BoundedEcmaSignatureDecodeKind.Invalid,
                    reader.Failure == BoundedEcmaSignatureFailureKind.None
                        ? BoundedEcmaSignatureFailureKind.InvalidElementType
                        : reader.Failure,
                    BoundedEcmaSignatureBoundKind.None,
                    counters,
                    null)
                : new BoundedEcmaSignatureDecodeOutcome(
                    BoundedEcmaSignatureDecodeKind.BoundReached,
                    BoundedEcmaSignatureFailureKind.None,
                    reader.ReachedBound,
                    counters,
                    null);
        }

        var certificate = new BoundedEcmaSignatureCertificate(
            form,
            signature.Length,
            header,
            form == BoundedEcmaSignatureForm.MethodDefinition ? callingConvention : -1,
            hasThis,
            hasExplicitThis,
            genericParameterCount,
            parameterCount,
            localSlotCount,
            counters);
        return new BoundedEcmaSignatureDecodeOutcome(
            BoundedEcmaSignatureDecodeKind.Exact,
            BoundedEcmaSignatureFailureKind.None,
            BoundedEcmaSignatureBoundKind.None,
            counters,
            certificate);
    }

    private static BoundedEcmaSignatureDecodeOutcome CreateEarlyInvalid(
        int inputByteCount,
        BoundedEcmaSignatureFailureKind failure) =>
        new(
            BoundedEcmaSignatureDecodeKind.Invalid,
            failure,
            BoundedEcmaSignatureBoundKind.None,
            new BoundedEcmaSignatureCounters(inputByteCount, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            null);

    internal static bool TryDecodeMethodDefinition(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        out BoundedEcmaMethodSignature projection)
    {
        projection = default;
        var outcome = Decode(
            signature,
            BoundedEcmaSignatureForm.MethodDefinition,
            BoundedEcmaSignatureLimits.Create(
                maximumSignatureLength,
                maximumDepth,
                maximumAggregateTypeCount));
        if (!outcome.IsExact)
        {
            return false;
        }

        var certificate = outcome.Certificate!.Value;
        projection = new BoundedEcmaMethodSignature(
            checked((byte)certificate.CallingConvention),
            certificate.HasThis,
            certificate.HasExplicitThis,
            certificate.GenericParameterCount,
            certificate.ParameterCount,
            certificate.Counters.AggregateTypeCount,
            certificate.Counters.MaximumObservedDepth);
        return true;
    }

    internal static bool TryDecodeLocal(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateTypeCount,
        out BoundedEcmaLocalSignature projection)
    {
        projection = default;
        var outcome = Decode(
            signature,
            BoundedEcmaSignatureForm.LocalVariables,
            BoundedEcmaSignatureLimits.Create(
                maximumSignatureLength,
                maximumDepth,
                maximumAggregateTypeCount));
        if (!outcome.IsExact)
        {
            return false;
        }

        var certificate = outcome.Certificate!.Value;
        projection = new BoundedEcmaLocalSignature(
            certificate.LocalSlotCount,
            certificate.Counters.AggregateTypeCount,
            certificate.Counters.MaximumObservedDepth);
        return true;
    }

    internal static bool TryDecodeGenericClassTypeSpecification(
        ReadOnlySpan<byte> signature,
        int maximumSignatureLength,
        int maximumDepth,
        int maximumAggregateGenericArgumentCount,
        out BoundedEcmaTypeSpecification projection)
    {
        projection = default;
        if (signature.IsEmpty ||
            signature.Length > maximumSignatureLength ||
            maximumSignatureLength <= 0 ||
            maximumDepth <= 0 ||
            maximumAggregateGenericArgumentCount <= 0)
        {
            return false;
        }

        var reader = new Reader(
            signature,
            maximumDepth,
            maximumAggregateTypeCount: signature.Length,
            maximumAggregateGenericArgumentCount,
            maximumGenericParameterCount: signature.Length,
            maximumParameterCount: signature.Length,
            maximumLocalSlotCount: signature.Length,
            maximumArrayRank: signature.Length,
            retainReferencedTypeTokens: true);
        if (!reader.TryReadGenericClassTypeSpecification(out var genericHeadToken, out var genericArgumentCount) ||
            !reader.AtEnd)
        {
            return false;
        }

        projection = new BoundedEcmaTypeSpecification(
            genericHeadToken,
            genericArgumentCount,
            reader.AggregateGenericArgumentCount,
            reader.MaximumObservedDepth,
            reader.ReferencedTypeMetadataTokens);
        return true;
    }

    private enum EncodedTypeCategory
    {
        DefinitelyNonReference,
        DefinitelyReference,
        GenericParameter,
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> bytes;
        private readonly int maximumDepth;
        private readonly int maximumAggregateTypeCount;
        private readonly int maximumAggregateGenericArgumentCount;
        private readonly int maximumGenericParameterCount;
        private readonly int maximumParameterCount;
        private readonly int maximumLocalSlotCount;
        private readonly int maximumArrayRank;
        private readonly ImmutableArray<int>.Builder? referencedTypeMetadataTokens;
        private readonly IBoundedEcmaSignatureNodeSink? nodeSink;
        private int offset;
        private int nextNodeOrdinal;

        internal Reader(
            ReadOnlySpan<byte> bytes,
            int maximumDepth,
            int maximumAggregateTypeCount,
            int maximumAggregateGenericArgumentCount,
            int maximumGenericParameterCount,
            int maximumParameterCount,
            int maximumLocalSlotCount,
            int maximumArrayRank,
            bool retainReferencedTypeTokens,
            IBoundedEcmaSignatureNodeSink? nodeSink = null)
        {
            this.bytes = bytes;
            this.maximumDepth = maximumDepth;
            this.maximumAggregateTypeCount = maximumAggregateTypeCount;
            this.maximumAggregateGenericArgumentCount = maximumAggregateGenericArgumentCount;
            this.maximumGenericParameterCount = maximumGenericParameterCount;
            this.maximumParameterCount = maximumParameterCount;
            this.maximumLocalSlotCount = maximumLocalSlotCount;
            this.maximumArrayRank = maximumArrayRank;
            referencedTypeMetadataTokens = retainReferencedTypeTokens
                ? ImmutableArray.CreateBuilder<int>()
                : null;
            this.nodeSink = nodeSink;
            offset = 0;
            nextNodeOrdinal = 0;
            AggregateTypeCount = 0;
            AggregateGenericArgumentCount = 0;
            MaximumObservedDepth = 0;
            MaximumDeclaredGenericParameterCount = 0;
            MaximumDeclaredParameterCount = 0;
            MaximumDeclaredLocalSlotCount = 0;
            MaximumDeclaredArrayRank = 0;
            Failure = BoundedEcmaSignatureFailureKind.None;
            ReachedBound = BoundedEcmaSignatureBoundKind.None;
        }

        internal bool AtEnd => offset == bytes.Length;

        internal int AggregateTypeCount { get; private set; }

        internal int AggregateGenericArgumentCount { get; private set; }

        internal int MaximumObservedDepth { get; private set; }

        internal int MaximumDeclaredGenericParameterCount { get; private set; }

        internal int MaximumDeclaredParameterCount { get; private set; }

        internal int MaximumDeclaredLocalSlotCount { get; private set; }

        internal int MaximumDeclaredArrayRank { get; private set; }

        internal BoundedEcmaSignatureFailureKind Failure { get; private set; }

        internal BoundedEcmaSignatureBoundKind ReachedBound { get; private set; }

        internal ImmutableArray<int> ReferencedTypeMetadataTokens =>
            referencedTypeMetadataTokens?.ToImmutable() ?? ImmutableArray<int>.Empty;

        internal BoundedEcmaSignatureCounters CreateCounters() =>
            new(
                bytes.Length,
                offset,
                AggregateTypeCount,
                AggregateGenericArgumentCount,
                MaximumObservedDepth,
                nextNodeOrdinal,
                MaximumDeclaredGenericParameterCount,
                MaximumDeclaredParameterCount,
                MaximumDeclaredLocalSlotCount,
                MaximumDeclaredArrayRank);

        internal bool Fail(BoundedEcmaSignatureFailureKind failure)
        {
            if (Failure == BoundedEcmaSignatureFailureKind.None &&
                ReachedBound == BoundedEcmaSignatureBoundKind.None)
            {
                Failure = failure;
            }

            return false;
        }

        internal bool TryReadTypeSpecification() =>
            TryReadType(depth: 1, parentNodeOrdinal: -1, out _);

        internal bool TryReadFieldSignature(out int signatureHeader)
        {
            signatureHeader = -1;
            if (!TryReadByte(out var header))
            {
                return false;
            }

            signatureHeader = header;
            return header == FieldSignatureHeader
                ? TryReadType(depth: 1, parentNodeOrdinal: -1, out _)
                : Fail(BoundedEcmaSignatureFailureKind.InvalidHeader);
        }

        internal bool TryReadLocalSignature(out int signatureHeader, out int localSlotCount)
        {
            signatureHeader = -1;
            localSlotCount = 0;
            if (!TryReadByte(out var header))
            {
                return false;
            }

            signatureHeader = header;
            if (header != LocalSignatureHeader)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidHeader);
            }

            if (!TryReadCompressedUInt32(out var slotCount))
            {
                return false;
            }

            if (slotCount > int.MaxValue)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            MaximumDeclaredLocalSlotCount = Math.Max(
                MaximumDeclaredLocalSlotCount,
                checked((int)slotCount));
            if (slotCount > (uint)maximumLocalSlotCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.LocalSlotCount);
                return false;
            }

            if (slotCount > (uint)maximumAggregateTypeCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateTypeCount);
                AggregateTypeCount = SaturatedCapPlusOne(maximumAggregateTypeCount);
                return false;
            }

            localSlotCount = checked((int)slotCount);
            for (uint slot = 0; slot < slotCount; slot++)
            {
                if (!TryReadLocalVariableType(depth: 1, parentNodeOrdinal: -1))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryReadMethodDefinitionSignature(
            out int signatureHeader,
            out byte callingConvention,
            out bool hasThis,
            out bool hasExplicitThis,
            out int genericParameterCount,
            out int parameterCount)
        {
            signatureHeader = -1;
            callingConvention = 0;
            hasThis = false;
            hasExplicitThis = false;
            genericParameterCount = 0;
            parameterCount = 0;
            if (!TryReadByte(out var header))
            {
                return false;
            }

            signatureHeader = header;
            if ((header & CallingConventionReserved) != 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidHeader);
            }

            callingConvention = (byte)(header & CallingConventionMask);
            if (callingConvention is not (CallingConventionDefault or CallingConventionVariableArguments))
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCallingConvention);
            }

            hasThis = (header & CallingConventionHasThis) != 0;
            hasExplicitThis = (header & CallingConventionExplicitThis) != 0;
            if (hasExplicitThis)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidReceiverFlags);
            }

            if ((header & CallingConventionGeneric) != 0 &&
                callingConvention != CallingConventionDefault)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidGenericHeader);
            }

            if (!TryReadGenericParameterCount(header, callingConvention, out genericParameterCount) ||
                !TryReadCompressedUInt32(out var encodedParameterCount))
            {
                return false;
            }

            if (encodedParameterCount > int.MaxValue)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            MaximumDeclaredParameterCount = Math.Max(
                MaximumDeclaredParameterCount,
                checked((int)encodedParameterCount));
            if (encodedParameterCount > (uint)maximumParameterCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.ParameterCount);
                return false;
            }

            if (encodedParameterCount >= (uint)maximumAggregateTypeCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateTypeCount);
                AggregateTypeCount = SaturatedCapPlusOne(maximumAggregateTypeCount);
                return false;
            }

            parameterCount = checked((int)encodedParameterCount);
            if (!TryReadReturnType(depth: 1, parentNodeOrdinal: -1))
            {
                return false;
            }

            for (var parameter = 0; parameter < parameterCount; parameter++)
            {
                if (!TryReadParameterType(depth: 1, parentNodeOrdinal: -1))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryReadLocalVariableType(int depth, int parentNodeOrdinal = -1)
        {
            if (!TryReadCustomModifiers(depth, parentNodeOrdinal, out var effectiveParent))
            {
                return false;
            }

            var isPinned = TryPeekByte(out var marker) && marker == ElementTypePinned;
            if (isPinned)
            {
                var markerOffset = offset;
                offset++;
                effectiveParent = EmitNode(
                    effectiveParent,
                    markerOffset,
                    depth,
                    BoundedEcmaSignatureNodeKind.Pinned,
                    ElementTypePinned,
                    metadataToken: 0,
                    value: 0);
                if (!TryReadCustomModifiers(depth, effectiveParent, out effectiveParent))
                {
                    return false;
                }
            }

            if (TryPeekByte(out marker) && marker == ElementTypeTypedByReference)
            {
                if (isPinned)
                {
                    return Fail(BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
                }

                var typedByReferenceOffset = offset;
                offset++;
                return TryConsumeLeafTypeNode(
                    depth,
                    effectiveParent,
                    typedByReferenceOffset,
                    BoundedEcmaSignatureNodeKind.TypedByReference,
                    ElementTypeTypedByReference);
            }

            var isByReference = TryPeekByte(out marker) && marker == ElementTypeByReference;
            if (isByReference)
            {
                var byReferenceOffset = offset;
                offset++;
                effectiveParent = EmitNode(
                    effectiveParent,
                    byReferenceOffset,
                    depth,
                    BoundedEcmaSignatureNodeKind.ByReference,
                    ElementTypeByReference,
                    metadataToken: 0,
                    value: 0);
                if (!TryReadCustomModifiers(depth, effectiveParent, out effectiveParent))
                {
                    return false;
                }
            }

            return TryReadTypeCore(depth, effectiveParent, out _);
        }

        internal bool TryReadGenericClassTypeSpecification(
            out int genericHeadToken,
            out int genericArgumentCount)
        {
            genericHeadToken = 0;
            genericArgumentCount = 0;
            if (!TryReadByte(out var genericInstance))
            {
                return false;
            }

            if (genericInstance != ElementTypeGenericInstance ||
                !TryReadGenericInstanceBody(
                    argumentDepth: 1,
                    parentNodeOrdinal: -1,
                    requireClass: true,
                    out _,
                    out genericHeadToken,
                    out genericArgumentCount))
            {
                return false;
            }

            return true;
        }

        private bool TryReadType(int depth, int parentNodeOrdinal, out EncodedTypeCategory category)
        {
            category = default;
            return TryReadCustomModifiers(depth, parentNodeOrdinal, out var effectiveParent) &&
                   TryReadTypeCore(depth, effectiveParent, out category);
        }

        private bool TryReadTypeCore(int depth, int parentNodeOrdinal, out EncodedTypeCategory category)
        {
            category = EncodedTypeCategory.DefinitelyNonReference;
            var elementOffset = offset;
            if (!TryConsumeTypeNode(depth) || !TryReadByte(out var elementType))
            {
                return false;
            }

            switch (elementType)
            {
                case ElementTypeBoolean:
                case ElementTypeChar:
                case ElementTypeI1:
                case ElementTypeU1:
                case ElementTypeI2:
                case ElementTypeU2:
                case ElementTypeI4:
                case ElementTypeU4:
                case ElementTypeI8:
                case ElementTypeU8:
                case ElementTypeR4:
                case ElementTypeR8:
                case ElementTypeI:
                case ElementTypeU:
                    EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.Primitive,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return true;
                case ElementTypeString:
                    category = EncodedTypeCategory.DefinitelyReference;
                    EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.String,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return true;
                case ElementTypeObject:
                    category = EncodedTypeCategory.DefinitelyReference;
                    EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.Object,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return true;
                case ElementTypeClass:
                    category = EncodedTypeCategory.DefinitelyReference;
                    if (!TryReadTypeDefOrRefToken(allowTypeSpecification: false, out var classToken))
                    {
                        return false;
                    }

                    EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.Class,
                        elementType,
                        classToken,
                        value: 0);
                    return true;
                case ElementTypeValueType:
                    if (!TryReadTypeDefOrRefToken(allowTypeSpecification: false, out var valueTypeToken))
                    {
                        return false;
                    }

                    EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.ValueType,
                        elementType,
                        valueTypeToken,
                        value: 0);
                    return true;
                case ElementTypeVar:
                    category = EncodedTypeCategory.GenericParameter;
                    return TryReadVariable(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.OwnerTypeParameter,
                        elementType);
                case ElementTypeMVar:
                    category = EncodedTypeCategory.GenericParameter;
                    return TryReadVariable(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.MethodTypeParameter,
                        elementType);
                case ElementTypePointer:
                    var pointerOrdinal = EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.Pointer,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return TryReadPointerTarget(depth + 1, pointerOrdinal);
                case ElementTypeSzArray:
                    category = EncodedTypeCategory.DefinitelyReference;
                    var szArrayOrdinal = EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.SzArray,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return TryReadType(depth + 1, szArrayOrdinal, out _);
                case ElementTypeArray:
                    category = EncodedTypeCategory.DefinitelyReference;
                    var arrayOrdinal = EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.MultidimensionalArray,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return TryReadArray(depth + 1, arrayOrdinal);
                case ElementTypeGenericInstance:
                    var genericInstantiationOrdinal = EmitNode(
                        parentNodeOrdinal,
                        elementOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.GenericInstantiation,
                        elementType,
                        metadataToken: 0,
                        value: 0);
                    return TryReadGenericInstanceBody(
                        depth + 1,
                        genericInstantiationOrdinal,
                        requireClass: false,
                        out category,
                        out _,
                        out _);
                case ElementTypeFunctionPointer:
                    return TryReadFunctionPointerSignature(
                        childDepth: depth + 1,
                        parentNodeOrdinal,
                        elementOffset,
                        functionPointerDepth: depth);
                case ElementTypeVoid:
                case ElementTypeByReference:
                case ElementTypeTypedByReference:
                    return Fail(BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
                case ElementTypeSentinel:
                    return Fail(BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
                default:
                    return Fail(BoundedEcmaSignatureFailureKind.InvalidElementType);
            }
        }

        private bool TryReadReturnType(int depth, int parentNodeOrdinal)
        {
            if (!TryReadCustomModifiers(depth, parentNodeOrdinal, out var effectiveParent))
            {
                return false;
            }

            if (TryPeekByte(out var special))
            {
                if (special is ElementTypeVoid or ElementTypeTypedByReference)
                {
                    var specialOffset = offset;
                    offset++;
                    return TryConsumeLeafTypeNode(
                        depth,
                        effectiveParent,
                        specialOffset,
                        special == ElementTypeVoid
                            ? BoundedEcmaSignatureNodeKind.Void
                            : BoundedEcmaSignatureNodeKind.TypedByReference,
                        special);
                }

                if (special == ElementTypeByReference)
                {
                    var byReferenceOffset = offset;
                    offset++;
                    var byReferenceOrdinal = EmitNode(
                        effectiveParent,
                        byReferenceOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.ByReference,
                        special,
                        metadataToken: 0,
                        value: 0);
                    return TryReadType(depth, byReferenceOrdinal, out _);
                }

                if (special == ElementTypeSentinel)
                {
                    return Fail(BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
                }
            }

            return TryReadTypeCore(depth, effectiveParent, out _);
        }

        private bool TryReadParameterType(int depth, int parentNodeOrdinal)
        {
            if (!TryReadCustomModifiers(depth, parentNodeOrdinal, out var effectiveParent))
            {
                return false;
            }

            if (TryPeekByte(out var special))
            {
                if (special == ElementTypeTypedByReference)
                {
                    var typedByReferenceOffset = offset;
                    offset++;
                    return TryConsumeLeafTypeNode(
                        depth,
                        effectiveParent,
                        typedByReferenceOffset,
                        BoundedEcmaSignatureNodeKind.TypedByReference,
                        special);
                }

                if (special == ElementTypeByReference)
                {
                    var byReferenceOffset = offset;
                    offset++;
                    var byReferenceOrdinal = EmitNode(
                        effectiveParent,
                        byReferenceOffset,
                        depth,
                        BoundedEcmaSignatureNodeKind.ByReference,
                        special,
                        metadataToken: 0,
                        value: 0);
                    return TryReadType(depth, byReferenceOrdinal, out _);
                }

                if (special is ElementTypeVoid or ElementTypeSentinel)
                {
                    return Fail(special == ElementTypeSentinel
                        ? BoundedEcmaSignatureFailureKind.SentinelNotPermitted
                        : BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
                }
            }

            return TryReadTypeCore(depth, effectiveParent, out _);
        }

        private bool TryReadPointerTarget(int depth, int parentNodeOrdinal)
        {
            if (!TryReadCustomModifiers(depth, parentNodeOrdinal, out var effectiveParent))
            {
                return false;
            }

            if (TryPeekByte(out var elementType) && elementType == ElementTypeVoid)
            {
                var voidOffset = offset;
                offset++;
                return TryConsumeLeafTypeNode(
                    depth,
                    effectiveParent,
                    voidOffset,
                    BoundedEcmaSignatureNodeKind.Void,
                    elementType);
            }

            if (elementType is ElementTypeByReference or ElementTypeTypedByReference or ElementTypeSentinel)
            {
                return Fail(elementType == ElementTypeSentinel
                    ? BoundedEcmaSignatureFailureKind.SentinelNotPermitted
                    : BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
            }

            return TryReadTypeCore(depth, effectiveParent, out _);
        }

        private bool TryReadArray(int depth, int parentNodeOrdinal)
        {
            if (!TryReadType(depth, parentNodeOrdinal, out _) ||
                !TryReadCompressedUInt32(out var rank))
            {
                return false;
            }

            if (rank == 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidArrayShape);
            }

            MaximumDeclaredArrayRank = Math.Max(MaximumDeclaredArrayRank, checked((int)rank));
            if (rank > (uint)maximumArrayRank)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.ArrayRank);
                return false;
            }

            if (!TryReadCompressedUInt32(out var sizeCount))
            {
                return false;
            }

            if (sizeCount > rank)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidArrayShape);
            }

            var shapeOrdinal = EmitNode(
                parentNodeOrdinal,
                offset,
                depth - 1,
                BoundedEcmaSignatureNodeKind.ArrayShape,
                ElementTypeArray,
                metadataToken: 0,
                value: 0,
                count: checked((int)rank));
            for (uint size = 0; size < sizeCount; size++)
            {
                var sizeOffset = offset;
                if (!TryReadCompressedUInt32(out var encodedSize) || encodedSize > int.MaxValue)
                {
                    return Failure == BoundedEcmaSignatureFailureKind.None
                        ? Fail(BoundedEcmaSignatureFailureKind.InvalidArrayShape)
                        : false;
                }

                EmitNode(
                    shapeOrdinal,
                    sizeOffset,
                    depth - 1,
                    BoundedEcmaSignatureNodeKind.ArraySize,
                    ElementTypeArray,
                    metadataToken: 0,
                    value: checked((int)encodedSize),
                    index: checked((int)size));
            }

            if (!TryReadCompressedUInt32(out var lowerBoundCount))
            {
                return false;
            }

            if (lowerBoundCount > rank)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidArrayShape);
            }

            for (uint lowerBound = 0; lowerBound < lowerBoundCount; lowerBound++)
            {
                var lowerBoundOffset = offset;
                if (!TryReadCompressedSignedInteger(out var decodedLowerBound))
                {
                    return false;
                }

                EmitNode(
                    shapeOrdinal,
                    lowerBoundOffset,
                    depth - 1,
                    BoundedEcmaSignatureNodeKind.ArrayLowerBound,
                    ElementTypeArray,
                    metadataToken: 0,
                    value: decodedLowerBound,
                    index: checked((int)lowerBound));
            }

            return true;
        }

        private bool TryReadGenericInstanceBody(
            int argumentDepth,
            int parentNodeOrdinal,
            bool requireClass,
            out EncodedTypeCategory category,
            out int genericHeadToken,
            out int genericArgumentCount)
        {
            category = default;
            genericHeadToken = 0;
            genericArgumentCount = 0;
            var headOffset = offset;
            if (!TryReadByte(out var classKind))
            {
                return false;
            }

            if (classKind is not (ElementTypeClass or ElementTypeValueType) ||
                requireClass && classKind != ElementTypeClass)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
            }

            if (!TryReadTypeDefOrRefToken(allowTypeSpecification: false, out genericHeadToken) ||
                !TryReadCompressedUInt32(out var argumentCount))
            {
                return false;
            }

            if (argumentCount == 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            if (!TryAddGenericArguments(argumentCount))
            {
                return false;
            }

            genericArgumentCount = checked((int)argumentCount);
            category = classKind == ElementTypeClass
                ? EncodedTypeCategory.DefinitelyReference
                : EncodedTypeCategory.DefinitelyNonReference;
            EmitNode(
                parentNodeOrdinal,
                headOffset,
                argumentDepth - 1,
                classKind == ElementTypeClass
                    ? BoundedEcmaSignatureNodeKind.Class
                    : BoundedEcmaSignatureNodeKind.ValueType,
                classKind,
                genericHeadToken,
                value: 0,
                count: genericArgumentCount);
            for (uint argument = 0; argument < argumentCount; argument++)
            {
                if (!TryReadType(argumentDepth, parentNodeOrdinal, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadFunctionPointerSignature(
            int childDepth,
            int parentNodeOrdinal,
            int functionPointerOffset,
            int functionPointerDepth)
        {
            if (childDepth > maximumDepth)
            {
                MaximumObservedDepth = Math.Max(MaximumObservedDepth, SaturatedCapPlusOne(maximumDepth));
                ReachBound(BoundedEcmaSignatureBoundKind.RecursiveDepth);
                return false;
            }

            if (!TryReadByte(out var header))
            {
                return false;
            }

            if ((header & CallingConventionReserved) != 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidHeader);
            }

            var callingConvention = (byte)(header & CallingConventionMask);
            if (callingConvention is not (
                    CallingConventionDefault or
                    CallingConventionCDecl or
                    CallingConventionStandardCall or
                    CallingConventionThisCall or
                    CallingConventionFastCall or
                    CallingConventionVariableArguments or
                    CallingConventionUnmanaged))
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCallingConvention);
            }

            var hasThis = (header & CallingConventionHasThis) != 0;
            var hasExplicitThis = (header & CallingConventionExplicitThis) != 0;
            if (hasExplicitThis && !hasThis)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidReceiverFlags);
            }

            if ((header & CallingConventionGeneric) != 0 &&
                callingConvention != CallingConventionDefault)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidGenericHeader);
            }

            if (!TryReadGenericParameterCount(header, callingConvention, out var genericParameterCount) ||
                !TryReadCompressedUInt32(out var parameterCount))
            {
                return false;
            }

            if (parameterCount > int.MaxValue)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            MaximumDeclaredParameterCount = Math.Max(
                MaximumDeclaredParameterCount,
                checked((int)parameterCount));
            if (parameterCount > (uint)maximumParameterCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.ParameterCount);
                return false;
            }

            if (parameterCount >= (uint)(maximumAggregateTypeCount - AggregateTypeCount))
            {
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateTypeCount);
                AggregateTypeCount = SaturatedCapPlusOne(maximumAggregateTypeCount);
                return false;
            }

            var functionPointerOrdinal = EmitNode(
                parentNodeOrdinal,
                functionPointerOffset,
                functionPointerDepth,
                BoundedEcmaSignatureNodeKind.FunctionPointer,
                ElementTypeFunctionPointer,
                metadataToken: 0,
                value: 0,
                header,
                index: genericParameterCount,
                count: checked((int)parameterCount));

            if (!TryReadReturnType(childDepth, functionPointerOrdinal))
            {
                return false;
            }

            for (uint parameter = 0; parameter < parameterCount; parameter++)
            {
                if (TryPeekByte(out var parameterMarker) && parameterMarker == ElementTypeSentinel)
                {
                    return Fail(BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
                }

                if (!TryReadParameterType(childDepth, functionPointerOrdinal))
                {
                    return false;
                }
            }

            if (TryPeekByte(out var trailingMarker) && trailingMarker == ElementTypeSentinel)
            {
                return Fail(BoundedEcmaSignatureFailureKind.SentinelNotPermitted);
            }

            return true;
        }

        private bool TryReadGenericParameterCount(
            byte header,
            byte callingConvention,
            out int genericParameterCount)
        {
            genericParameterCount = 0;
            if ((header & CallingConventionGeneric) == 0)
            {
                return true;
            }

            if (callingConvention != CallingConventionDefault)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidGenericHeader);
            }

            if (!TryReadCompressedUInt32(out var encodedCount))
            {
                return false;
            }

            if (encodedCount is 0 or > int.MaxValue)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            MaximumDeclaredGenericParameterCount = Math.Max(
                MaximumDeclaredGenericParameterCount,
                checked((int)encodedCount));
            if (encodedCount > (uint)maximumGenericParameterCount)
            {
                ReachBound(BoundedEcmaSignatureBoundKind.GenericParameterCount);
                return false;
            }

            genericParameterCount = checked((int)encodedCount);
            return true;
        }

        private bool TryReadCustomModifiers(
            int depth,
            int parentNodeOrdinal,
            out int effectiveParentNodeOrdinal)
        {
            effectiveParentNodeOrdinal = parentNodeOrdinal;
            while (TryPeekByte(out var elementType) &&
                   elementType is ElementTypeRequiredModifier or ElementTypeOptionalModifier)
            {
                var modifierOffset = offset;
                offset++;
                if (!TryReadTypeDefOrRefToken(allowTypeSpecification: true, out var modifierToken))
                {
                    return false;
                }

                effectiveParentNodeOrdinal = EmitNode(
                    effectiveParentNodeOrdinal,
                    modifierOffset,
                    depth,
                    elementType == ElementTypeRequiredModifier
                        ? BoundedEcmaSignatureNodeKind.RequiredModifier
                        : BoundedEcmaSignatureNodeKind.OptionalModifier,
                    elementType,
                    modifierToken,
                    value: 0);
            }

            return true;
        }

        private bool TryAddGenericArguments(uint count)
        {
            if (count > int.MaxValue)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCount);
            }

            if (count > (uint)(maximumAggregateGenericArgumentCount - AggregateGenericArgumentCount))
            {
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateGenericArgumentCount);
                AggregateGenericArgumentCount = SaturatedCapPlusOne(maximumAggregateGenericArgumentCount);
                return false;
            }

            if (count > (uint)(maximumAggregateTypeCount - AggregateTypeCount))
            {
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateTypeCount);
                AggregateTypeCount = SaturatedCapPlusOne(maximumAggregateTypeCount);
                return false;
            }

            AggregateGenericArgumentCount += checked((int)count);
            return true;
        }

        private bool TryConsumeTypeNode(int depth)
        {
            if (depth <= 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidTypePlacement);
            }

            if (depth > maximumDepth)
            {
                MaximumObservedDepth = Math.Max(MaximumObservedDepth, SaturatedCapPlusOne(maximumDepth));
                ReachBound(BoundedEcmaSignatureBoundKind.RecursiveDepth);
                return false;
            }

            if (AggregateTypeCount >= maximumAggregateTypeCount)
            {
                AggregateTypeCount = SaturatedCapPlusOne(maximumAggregateTypeCount);
                ReachBound(BoundedEcmaSignatureBoundKind.AggregateTypeCount);
                return false;
            }

            AggregateTypeCount++;
            MaximumObservedDepth = Math.Max(MaximumObservedDepth, depth);
            return true;
        }

        private bool TryConsumeLeafTypeNode(
            int depth,
            int parentNodeOrdinal,
            int signatureOffset,
            BoundedEcmaSignatureNodeKind kind,
            int elementType)
        {
            if (!TryConsumeTypeNode(depth))
            {
                return false;
            }

            EmitNode(
                parentNodeOrdinal,
                signatureOffset,
                depth,
                kind,
                elementType,
                metadataToken: 0,
                value: 0);
            return true;
        }

        private bool TryReadVariable(
            int parentNodeOrdinal,
            int signatureOffset,
            int depth,
            BoundedEcmaSignatureNodeKind kind,
            int elementType)
        {
            if (!TryReadCompressedUInt32(out var index) || index > int.MaxValue)
            {
                return Failure == BoundedEcmaSignatureFailureKind.None
                    ? Fail(BoundedEcmaSignatureFailureKind.InvalidCount)
                    : false;
            }

            EmitNode(
                parentNodeOrdinal,
                signatureOffset,
                depth,
                kind,
                elementType,
                metadataToken: 0,
                value: 0,
                index: checked((int)index));
            return true;
        }

        private int EmitNode(
            int parentNodeOrdinal,
            int signatureOffset,
            int depth,
            BoundedEcmaSignatureNodeKind kind,
            int elementType,
            int metadataToken,
            int value,
            int header = 0,
            int index = 0,
            int count = 0)
        {
            var ordinal = nextNodeOrdinal++;
            if (nodeSink is not null)
            {
                var node = new BoundedEcmaSignatureNodeEvent(
                    ordinal,
                    parentNodeOrdinal,
                    signatureOffset,
                    depth,
                    kind,
                    elementType,
                    metadataToken,
                    header,
                    index,
                    count,
                    value);
                nodeSink.Add(in node);
            }

            return ordinal;
        }

        private void ReachBound(BoundedEcmaSignatureBoundKind bound)
        {
            if (ReachedBound == BoundedEcmaSignatureBoundKind.None)
            {
                ReachedBound = bound;
            }
        }

        private static int SaturatedCapPlusOne(int cap) =>
            cap == int.MaxValue ? int.MaxValue : cap + 1;

        private bool TryReadTypeDefOrRefToken(bool allowTypeSpecification, out int token)
        {
            token = 0;
            if (!TryReadCompressedUInt32(out var codedIndex))
            {
                return false;
            }

            var rowId = codedIndex >> 2;
            if (rowId is 0 or > 0x00FF_FFFF)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidMetadataToken);
            }

            var table = (codedIndex & 0x03) switch
            {
                0 => 0x02,
                1 => 0x01,
                2 when allowTypeSpecification => 0x1B,
                _ => -1,
            };
            if (table < 0)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidMetadataToken);
            }

            token = (table << 24) | checked((int)rowId);
            referencedTypeMetadataTokens?.Add(token);
            return true;
        }

        internal bool TryReadCompressedUInt32(out uint value)
        {
            value = 0;
            if (!TryReadByte(out var first))
            {
                return false;
            }

            if ((first & 0x80) == 0)
            {
                value = first;
                return true;
            }

            if ((first & 0xC0) == 0x80)
            {
                if (!TryReadByte(out var second))
                {
                    return false;
                }

                value = (uint)(((first & 0x3F) << 8) | second);
                return value >= 0x80 || Fail(BoundedEcmaSignatureFailureKind.InvalidCompressedInteger);
            }

            if ((first & 0xE0) == 0xC0)
            {
                if (bytes.Length - offset < 3)
                {
                    return Fail(BoundedEcmaSignatureFailureKind.UnexpectedEnd);
                }

                value = (uint)(((first & 0x1F) << 24) |
                               (bytes[offset] << 16) |
                               (bytes[offset + 1] << 8) |
                               bytes[offset + 2]);
                offset += 3;
                return value is >= 0x4000 and <= 0x1FFF_FFFF ||
                       Fail(BoundedEcmaSignatureFailureKind.InvalidCompressedInteger);
            }

            return Fail(BoundedEcmaSignatureFailureKind.InvalidCompressedInteger);
        }

        private bool TryReadCompressedSignedInteger(out int value)
        {
            value = 0;
            if (!TryReadByte(out var first))
            {
                return false;
            }

            uint encoded;
            int encodedBitCount;
            int byteCount;
            if ((first & 0x80) == 0)
            {
                encoded = first;
                encodedBitCount = 7;
                byteCount = 1;
            }
            else if ((first & 0xC0) == 0x80)
            {
                if (!TryReadByte(out var second))
                {
                    return false;
                }

                encoded = (uint)(((first & 0x3F) << 8) | second);
                encodedBitCount = 14;
                byteCount = 2;
            }
            else if ((first & 0xE0) == 0xC0)
            {
                if (bytes.Length - offset < 3)
                {
                    return Fail(BoundedEcmaSignatureFailureKind.UnexpectedEnd);
                }

                encoded = (uint)(((first & 0x1F) << 24) |
                                 (bytes[offset] << 16) |
                                 (bytes[offset + 1] << 8) |
                                 bytes[offset + 2]);
                offset += 3;
                encodedBitCount = 29;
                byteCount = 4;
            }
            else
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCompressedInteger);
            }

            var decoded = checked((int)(encoded >> 1));
            if ((encoded & 1) != 0)
            {
                decoded |= -1 << (encodedBitCount - 1);
            }

            if (byteCount == 2 && decoded is >= -64 and <= 63 ||
                byteCount == 4 && decoded is >= -8192 and <= 8191)
            {
                return Fail(BoundedEcmaSignatureFailureKind.InvalidCompressedInteger);
            }

            value = decoded;
            return true;
        }

        internal bool TryReadByte(out byte value)
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                value = 0;
                return Fail(BoundedEcmaSignatureFailureKind.UnexpectedEnd);
            }

            value = bytes[offset++];
            return true;
        }

        private bool TryPeekByte(out byte value)
        {
            if ((uint)offset >= (uint)bytes.Length)
            {
                value = 0;
                return false;
            }

            value = bytes[offset];
            return true;
        }
    }
}
