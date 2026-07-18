using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises W7's metadata-blind physical storage decoder with complex detached synthetic evidence.</summary>
[Trait("Category", "Fast")]
public sealed class W7StaticStorageObjectContractTests
{
    private const int PointerWidth = sizeof(ulong);
    private const ulong AppDomain = 0x1000;
    private const ulong Slot = 0x9000;
    private const ulong Target = 0x2000;
    private const ulong MethodTable = 0x7000;
    private static readonly ClrmdSnapshotIdentity Snapshot = new(new string('1', 64));
    private static readonly ModuleContentIdentity ProgramContent = ModuleContentIdentity.FromDigest(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        2048,
        new string('2', 64));
    private static readonly ModuleContentIdentity CoreContent = ModuleContentIdentity.FromDigest(
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        4096,
        new string('3', 64));
    private static readonly ClrmdRuntimeModuleIdentity ProgramModule = new(
        Snapshot,
        AppDomain,
        0x3000,
        0x0040_0000,
        0x0002_0000);
    private static readonly ClrmdRuntimeModuleIdentity CoreModule = new(
        Snapshot,
        AppDomain,
        0x4000,
        0x0060_0000,
        0x0004_0000);

    /// <summary>Proves empty exhaustive absence and truthful capped-prefix match topology.</summary>
    [Fact]
    public void Domain_topology_retains_empty_absence_and_every_capped_prefix_match()
    {
        var empty = ClrmdStaticStorageAcquisitionEvidence.DomainUnavailable(PointerWidth, AppDomain, 0);
        Assert.True(empty.CatalogExhaustive);
        Assert.Equal(0, empty.ApplicationDomainCatalogCardinality);
        Assert.Empty(empty.MatchingApplicationDomainOrdinals);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClrmdStaticStorageAcquisitionEvidence.Acquired(PointerWidth, AppDomain, 0, 0, Slot, sizeof(int)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClrmdStaticStorageAcquisitionEvidence.DomainAmbiguous(
                PointerWidth,
                AppDomain,
                0,
                ImmutableArray.Create(0, 1)));

        var noMatches = ClrmdStaticStorageAcquisitionEvidence.CatalogLimitReached(PointerWidth, AppDomain);
        var oneMatch = ClrmdStaticStorageAcquisitionEvidence.CatalogLimitReached(
            PointerWidth,
            AppDomain,
            ImmutableArray.Create(7));
        var twoMatches = ClrmdStaticStorageAcquisitionEvidence.CatalogLimitReached(
            PointerWidth,
            AppDomain,
            ImmutableArray.Create(12, 2));
        Assert.False(noMatches.CatalogExhaustive);
        Assert.Null(oneMatch.MatchingApplicationDomainOrdinal);
        Assert.Equal(new[] { 2, 12 }, twoMatches.MatchingApplicationDomainOrdinals);
        Assert.NotEqual(noMatches.Sha256, oneMatch.Sha256);
        Assert.NotEqual(oneMatch.Sha256, twoMatches.Sha256);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClrmdStaticStorageAcquisitionEvidence.CatalogLimitReached(
                PointerWidth,
                AppDomain,
                ImmutableArray.Create(16)));
    }

    /// <summary>Proves invalid-slot overflow is correlated with the exact request storage size.</summary>
    [Fact]
    public void Invalid_slot_cannot_be_replayed_under_a_smaller_request_width()
    {
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.Int32, Int32Type());
        var rawSlot = ulong.MaxValue - 6;
        var evidence = ClrmdStaticStorageAcquisitionEvidence.InvalidSlot(
            PointerWidth,
            AppDomain,
            1,
            0,
            rawSlot,
            storageSize: 8);

        Assert.Equal(8, evidence.AttemptedStorageSize);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            slotAddress: null,
            ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
            AcquisitionBounds,
            targetEvidence: null,
            evidence));

        var wrongAcquiredWidth = ClrmdStaticStorageAcquisitionEvidence.Acquired(
            PointerWidth,
            AppDomain,
            1,
            0,
            Slot,
            storageSize: 8);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Exact(
            request,
            wrongAcquiredWidth,
            ImmutableArray.Create(Int32Read(Slot, 42)),
            ClrmdStaticFieldValue.ExactInt32(42),
            ReadBounds));
    }

    /// <summary>Proves a raw read cannot allocate or retain more than the largest fixed string decoder range.</summary>
    [Fact]
    public void Raw_memory_evidence_has_a_fixed_per_read_byte_bound()
    {
        var atCap = ImmutableArray.Create(new byte[ClrmdRawMemoryEvidence.MaximumReadBytes]);
        Assert.Equal(atCap.Length, ClrmdRawMemoryEvidence.Exact(Snapshot, 0x5000, atCap).RequestedLength);
        Assert.Throws<ArgumentOutOfRangeException>(() => ClrmdRawMemoryEvidence.Exact(
            Snapshot,
            0x5000,
            ImmutableArray.Create(new byte[ClrmdRawMemoryEvidence.MaximumReadBytes + 1])));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            0x5000,
            ClrmdRawMemoryEvidence.MaximumReadBytes + 1,
            ImmutableArray.Create((byte)1)));
    }

    /// <summary>Proves exact and failed scalar decoding retain execution order and stop at the first incomplete read.</summary>
    [Fact]
    public void Int32_decoder_rejects_appended_work_after_its_first_failure()
    {
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.Int32, Int32Type());
        var acquisition = Acquired(request);
        var exactRead = Int32Read(Slot, -1234567);
        var exact = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(exactRead),
            ClrmdStaticFieldValue.ExactInt32(-1234567),
            ReadBounds);
        Assert.Equal(-1234567, exact.Value!.Int32Value);

        var partial = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            Slot,
            sizeof(int),
            ImmutableArray.Create((byte)0x79));
        var stopped = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(partial),
            ReadBounds,
            storageAcquisitionEvidence: acquisition);
        Assert.Single(stopped.Reads);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(partial, Int32Read(Slot + 16, 1)),
            ReadBounds,
            storageAcquisitionEvidence: acquisition));
    }

    /// <summary>Proves Nullable decoding follows semantic stage order even when child addresses run in reverse.</summary>
    [Fact]
    public void Nullable_decoder_represents_partial_invalid_and_reversed_offset_prefixes()
    {
        var layout = ClrmdStaticNullableInt32Layout.Create(storageSize: 8, hasValueOffset: 4, valueOffset: 0);
        var nullableType = NullableType(Int32Type());
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.NullableInt32, nullableType, layout);
        var acquisition = Acquired(request);
        var flagAddress = Slot + 4;

        var flagPartial = ClrmdRawMemoryEvidence.Unavailable(Snapshot, flagAddress, sizeof(byte));
        var flagStop = ClrmdStaticFieldValueObservation.Unavailable(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(flagPartial),
            ReadBounds,
            storageAcquisitionEvidence: acquisition);
        Assert.Equal(flagAddress, Assert.Single(flagStop.Reads).Address);

        var invalidBoolean = ByteRead(flagAddress, 2);
        var invalid = ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(invalidBoolean),
            ReadBounds,
            targetEvidence: null,
            acquisition);
        Assert.Equal(ClrmdStaticFieldObservationStatus.Invalid, invalid.Status);

        var trueFlag = ByteRead(flagAddress, 1);
        var valuePartial = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            Slot,
            sizeof(int),
            ImmutableArray.Create((byte)0x34, (byte)0x12));
        var valueStop = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(trueFlag, valuePartial),
            ReadBounds,
            storageAcquisitionEvidence: acquisition);
        Assert.Equal(new[] { flagAddress, Slot }, valueStop.Reads.Select(static read => read.Address));

        var valueRead = Int32Read(Slot, 0x12345678);
        var exact = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(trueFlag, valueRead),
            ClrmdStaticFieldValue.NullableInt32Value(0x12345678),
            ReadBounds);
        Assert.Equal(new[] { flagAddress, Slot }, exact.Reads.Select(static read => read.Address));
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(valueRead, trueFlag),
            ClrmdStaticFieldValue.NullableInt32Value(0x12345678),
            ReadBounds));
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(flagPartial, valueRead),
            ReadBounds,
            storageAcquisitionEvidence: acquisition));
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(invalidBoolean, valueRead),
            ReadBounds,
            targetEvidence: null,
            acquisition));
    }

    /// <summary>Proves raw-header-first reference execution never performs later work after a typed stop.</summary>
    [Fact]
    public void Reference_decoder_retains_strict_slot_header_and_lookup_prefixes()
    {
        var declaredType = ReferenceType("Demo.Base", 0x02000003, methodTable: 0x7100);
        var actualType = ReferenceType("Demo.Derived", 0x02000004, methodTable: MethodTable);
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.ManagedReference, declaredType);
        var acquisition = Acquired(request);
        var slotRead = PointerRead(Slot, Target);
        var headerRead = PointerRead(Target, MethodTable);
        var matched = ClrmdStaticTargetEvidence.Matched(
            Snapshot,
            PointerWidth,
            Target,
            headerRead,
            actualType);
        var exactObject = ClrmdExactObjectReference.Create(matched);
        var exact = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(slotRead, headerRead),
            ClrmdStaticFieldValue.ExactObjectReference(exactObject),
            ReadBounds);
        Assert.Equal(new[] { Slot, Target }, exact.Reads.Select(static read => read.Address));
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(headerRead, slotRead),
            ClrmdStaticFieldValue.ExactObjectReference(exactObject),
            ReadBounds));

        var slotPartial = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            Slot,
            PointerWidth,
            ImmutableArray.Create((byte)0x00));
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotPartial, headerRead),
            ReadBounds,
            storageAcquisitionEvidence: acquisition));

        var headerPartial = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            Target,
            PointerWidth,
            ImmutableArray.Create((byte)0x00));
        var headerUnavailable = ClrmdStaticTargetEvidence.HeaderUnavailable(
            Snapshot,
            PointerWidth,
            Target,
            headerPartial);
        _ = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerPartial),
            ReadBounds,
            headerUnavailable,
            acquisition);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerPartial, Int32Read(Target + 8, 0)),
            ReadBounds,
            headerUnavailable,
            acquisition));

        var typeUnavailable = ClrmdStaticTargetEvidence.RuntimeTypeUnavailable(
            Snapshot,
            PointerWidth,
            Target,
            headerRead);
        _ = ClrmdStaticFieldValueObservation.Unavailable(
            Snapshot,
            ClrmdValueIssue.TypeUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead),
            ReadBounds,
            typeUnavailable,
            acquisition);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Unavailable(
            Snapshot,
            ClrmdValueIssue.TypeUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, Int32Read(Target + 8, 0)),
            ReadBounds,
            typeUnavailable,
            acquisition));

        var conflictingType = ReferenceType("Demo.Derived", 0x02000004, methodTable: MethodTable + 8);
        var conflict = ClrmdStaticTargetEvidence.RuntimeTypeConflict(
            Snapshot,
            PointerWidth,
            Target,
            headerRead,
            conflictingType);
        _ = ClrmdStaticFieldValueObservation.Conflict(
            Snapshot,
            ClrmdValueIssue.TypeMismatch,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead),
            ReadBounds,
            conflict,
            acquisition);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Conflict(
            Snapshot,
            ClrmdValueIssue.TypeMismatch,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, Int32Read(Target + 8, 0)),
            ReadBounds,
            conflict,
            acquisition));
    }

    /// <summary>Proves string first-failure, cap, and address-overflow prefixes are exact and nonextendable.</summary>
    [Fact]
    public void String_decoder_proves_length_character_cap_and_address_prefixes()
    {
        var stringType = StringType(MethodTable);
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.String, stringType);
        var acquisition = Acquired(request);
        var slotRead = PointerRead(Slot, Target);
        var headerRead = PointerRead(Target, MethodTable);
        var target = ClrmdStaticTargetEvidence.Matched(Snapshot, PointerWidth, Target, headerRead, stringType);
        var lengthAddress = Target + PointerWidth;

        var partialLength = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            lengthAddress,
            sizeof(int),
            ImmutableArray.Create((byte)1));
        _ = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, partialLength),
            ReadBounds,
            target,
            acquisition);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, partialLength, ByteRead(Target + 12, 0)),
            ReadBounds,
            target,
            acquisition));

        var negativeLength = Int32Read(lengthAddress, -1);
        _ = ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, negativeLength),
            ReadBounds,
            target,
            acquisition);
        Assert.Throws<ArgumentException>(() => ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, negativeLength, ByteRead(Target + 12, 0)),
            ReadBounds,
            target,
            acquisition));

        var overCap = Int32Read(lengthAddress, ClrmdExactStringValue.MaximumCharacters + 1);
        _ = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.LimitExceeded,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, overCap),
            ReadAndStringBounds,
            target,
            acquisition);

        var length = Int32Read(lengthAddress, 2);
        var characters = ClrmdRawMemoryEvidence.Partial(
            Snapshot,
            Target + PointerWidth + sizeof(int),
            4,
            ImmutableArray.Create((byte)'A', (byte)0));
        _ = ClrmdStaticFieldValueObservation.Partial(
            Snapshot,
            ClrmdValueIssue.MemoryUnavailable,
            request,
            Slot,
            ImmutableArray.Create(slotRead, headerRead, length, characters),
            ReadAndStringBounds,
            target,
            acquisition);

        var exactCharacters = ClrmdRawMemoryEvidence.Exact(
            Snapshot,
            Target + PointerWidth + sizeof(int),
            ImmutableArray.Create(Encoding.Unicode.GetBytes("AB")));
        var exactString = ClrmdExactStringValue.Create(
            ClrmdExactObjectReference.Create(target),
            "AB",
            length,
            exactCharacters);
        var exact = ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            ImmutableArray.Create(slotRead, headerRead, length, exactCharacters),
            ClrmdStaticFieldValue.ExactString(exactString),
            ReadAndStringBounds);
        Assert.Equal("AB", exact.Value!.StringValue!.Value);

        AssertStringLengthAddressOverflow(request);
        AssertStringCharacterAddressOverflow(request);
    }

    /// <summary>Proves reached-bound payloads are an exact set rather than an extensible caller bag.</summary>
    [Fact]
    public void Reached_bounds_reject_unknown_wrong_missing_duplicate_and_unused_entries()
    {
        var request = CreateRequest(ClrmdStaticExpectedDecoderKind.Int32, Int32Type());
        var acquisition = Acquired(request);
        var read = Int32Read(Slot, 7);
        var value = ClrmdStaticFieldValue.ExactInt32(7);
        var unknown = ReadBounds.Add(new EvaluationDeterministicBound("caller.extra", 1));
        var wrong = ImmutableArray.Create(
            ClrmdStaticStorageAcquisitionEvidence.DeclaredApplicationDomainCountBound,
            new EvaluationDeterministicBound(ClrmdStaticFieldValueObservation.MaximumRawReadCountBoundName, 3));
        var missing = AcquisitionBounds;
        var duplicate = ReadBounds.Add(ClrmdStaticFieldValueObservation.DeclaredRawReadCountBound);
        var unusedString = ReadBounds.Add(ClrmdExactStringValue.DeclaredCharacterLimitBound);

        Assert.Throws<ArgumentException>(() =>
            ClrmdStaticFieldValueObservation.Exact(request, acquisition, ImmutableArray.Create(read), value, unknown));
        Assert.Throws<ArgumentException>(() =>
            ClrmdStaticFieldValueObservation.Exact(request, acquisition, ImmutableArray.Create(read), value, wrong));
        Assert.Throws<ArgumentException>(() =>
            ClrmdStaticFieldValueObservation.Exact(request, acquisition, ImmutableArray.Create(read), value, missing));
        Assert.Throws<ArgumentException>(() =>
            ClrmdStaticFieldValueObservation.Exact(request, acquisition, ImmutableArray.Create(read), value, duplicate));
        Assert.Throws<ArgumentException>(() =>
            ClrmdStaticFieldValueObservation.Exact(request, acquisition, ImmutableArray.Create(read), value, unusedString));
    }

    /// <summary>Proves the public W7 physical surface carries no live or high-level ClrMD object API.</summary>
    [Fact]
    public void Physical_contract_surface_is_detached_and_contains_no_legacy_semantic_graph()
    {
        var assembly = typeof(ClrmdStaticFieldEvaluationRequest).Assembly;
        var physicalTypes = assembly.GetExportedTypes()
            .Where(type => type.Name.StartsWith("ClrmdStatic", StringComparison.Ordinal) ||
                type == typeof(ClrmdRawMemoryEvidence) ||
                type == typeof(ClrmdExactObjectReference) ||
                type == typeof(ClrmdExactStringValue))
            .ToArray();
        var surfaceTypes = physicalTypes
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(MemberTypes)
            .Select(type => type.FullName ?? type.Name)
            .ToArray();
        Assert.DoesNotContain(surfaceTypes, name => name.Contains("ClrHeap", StringComparison.Ordinal));
        Assert.DoesNotContain(surfaceTypes, name => name.Contains("ClrObject", StringComparison.Ordinal));
        Assert.DoesNotContain(surfaceTypes, name => name.Contains("ClrStaticField", StringComparison.Ordinal));
        Assert.Null(assembly.GetType("Interpreter.Host.Dump.ClrMD.ClrmdStaticFieldDeclaration"));
        Assert.Null(assembly.GetType("Interpreter.Host.Dump.ClrMD.ClrmdStaticDeclaredTargetIdentity"));
        Assert.Null(assembly.GetType("Interpreter.Host.Dump.ClrMD.ClrmdStaticTypeAncestryIdentity"));
    }

    private static void AssertStringLengthAddressOverflow(ClrmdStaticFieldEvaluationRequest request)
    {
        var targetAddress = ulong.MaxValue - 7;
        var slotRead = PointerRead(Slot, targetAddress);
        var header = PointerRead(targetAddress, MethodTable);
        var target = ClrmdStaticTargetEvidence.Matched(
            Snapshot,
            PointerWidth,
            targetAddress,
            header,
            StringType(MethodTable));
        _ = ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(slotRead, header),
            ReadBounds,
            target,
            Acquired(request));
    }

    private static void AssertStringCharacterAddressOverflow(ClrmdStaticFieldEvaluationRequest request)
    {
        var targetAddress = ulong.MaxValue - 11;
        var slotRead = PointerRead(Slot, targetAddress);
        var header = PointerRead(targetAddress, MethodTable);
        var target = ClrmdStaticTargetEvidence.Matched(
            Snapshot,
            PointerWidth,
            targetAddress,
            header,
            StringType(MethodTable));
        var length = Int32Read(targetAddress + PointerWidth, 1);
        _ = ClrmdStaticFieldValueObservation.Invalid(
            Snapshot,
            ClrmdValueIssue.InvalidData,
            request,
            Slot,
            ImmutableArray.Create(slotRead, header, length),
            ReadAndStringBounds,
            target,
            Acquired(request));
    }

    private static ClrmdStaticFieldEvaluationRequest CreateRequest(
        ClrmdStaticExpectedDecoderKind decoder,
        ClrmdStaticRuntimeTypeIdentity observedFieldType,
        ClrmdStaticNullableInt32Layout? nullableLayout = null)
    {
        var owner = ReferenceType("Demo.Owner", 0x02000001, methodTable: 0x6000);
        var field = ClrmdStaticRuntimeFieldIdentity.Create(
            owner,
            0x04000001,
            "Value",
            FieldAttributes.Public | FieldAttributes.Static,
            runtimeReportsThreadStatic: false,
            runtimeReportsContextStatic: false,
            decoder,
            observedFieldType);
        var counters = ClrmdStaticRuntimeDeclarationMappingCounters.Create(3, 2, 1, 1, true, true);
        var mapping = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(owner, field, counters);
        return ClrmdStaticFieldEvaluationRequest.Create(mapping, nullableLayout);
    }

    private static ClrmdStaticStorageAcquisitionEvidence Acquired(
        ClrmdStaticFieldEvaluationRequest request) =>
        ClrmdStaticStorageAcquisitionEvidence.Acquired(
            request.PointerWidth,
            request.ApplicationDomainAddress,
            applicationDomainCatalogCardinality: 2,
            matchingApplicationDomainOrdinal: 1,
            Slot,
            request.StorageSize);

    private static ClrmdStaticRuntimeTypeIdentity ReferenceType(
        string name,
        int token,
        ulong? methodTable) =>
        RuntimeType(
            ProgramModule,
            ProgramContent,
            token,
            name,
            methodTable,
            isValueType: false,
            isPrimitive: false,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);

    private static ClrmdStaticRuntimeTypeIdentity Int32Type() =>
        RuntimeType(
            CoreModule,
            CoreContent,
            0x02000010,
            "System.Int32",
            methodTable: 0x7200,
            isValueType: true,
            isPrimitive: true,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);

    private static ClrmdStaticRuntimeTypeIdentity StringType(ulong methodTable) =>
        RuntimeType(
            CoreModule,
            CoreContent,
            0x02000011,
            "System.String",
            methodTable,
            isValueType: false,
            isPrimitive: false,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);

    private static ClrmdStaticRuntimeTypeIdentity NullableType(ClrmdStaticRuntimeTypeIdentity argument) =>
        RuntimeType(
            CoreModule,
            CoreContent,
            0x02000012,
            "System.Nullable<System.Int32>",
            methodTable: 0x7300,
            isValueType: true,
            isPrimitive: false,
            ImmutableArray.Create(argument));

    private static ClrmdStaticRuntimeTypeIdentity RuntimeType(
        ClrmdRuntimeModuleIdentity module,
        ModuleContentIdentity content,
        int token,
        string name,
        ulong? methodTable,
        bool isValueType,
        bool isPrimitive,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity> genericArguments) =>
        ClrmdStaticRuntimeTypeIdentity.Create(
            Snapshot,
            PointerWidth,
            module,
            content,
            token,
            name,
            methodTable,
            isValueType,
            isPrimitive,
            isArray: false,
            isInterface: false,
            genericArguments);

    private static ClrmdRawMemoryEvidence Int32Read(ulong address, int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return ClrmdRawMemoryEvidence.Exact(Snapshot, address, ImmutableArray.Create(bytes));
    }

    private static ClrmdRawMemoryEvidence ByteRead(ulong address, byte value) =>
        ClrmdRawMemoryEvidence.Exact(Snapshot, address, ImmutableArray.Create(value));

    private static ClrmdRawMemoryEvidence PointerRead(ulong address, ulong value)
    {
        var bytes = new byte[PointerWidth];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return ClrmdRawMemoryEvidence.Exact(Snapshot, address, ImmutableArray.Create(bytes));
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(static parameter => parameter.ParameterType)
            .Append(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(static parameter => parameter.ParameterType),
        PropertyInfo property => new[] { property.PropertyType },
        FieldInfo field => new[] { field.FieldType },
        _ => Array.Empty<Type>(),
    };

    private static ImmutableArray<EvaluationDeterministicBound> AcquisitionBounds =>
        ImmutableArray.Create(ClrmdStaticStorageAcquisitionEvidence.DeclaredApplicationDomainCountBound);

    private static ImmutableArray<EvaluationDeterministicBound> ReadBounds =>
        AcquisitionBounds.Add(ClrmdStaticFieldValueObservation.DeclaredRawReadCountBound);

    private static ImmutableArray<EvaluationDeterministicBound> ReadAndStringBounds =>
        ReadBounds.Add(ClrmdExactStringValue.DeclaredCharacterLimitBound);
}
