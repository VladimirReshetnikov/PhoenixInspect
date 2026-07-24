using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises exact constructed-runtime selection, static-slot geometry, and raw-byte value decoding.</summary>
public sealed class W8V2RuntimeConstructionTests
{
    private const int PublicClassAttributes = 0x0000_0001;

    private const int FieldPublic = (int)FieldAttributes.Public;
    private const int FieldStatic = (int)FieldAttributes.Static;
    private const int FieldLiteral = (int)(FieldAttributes.Literal | FieldAttributes.HasDefault);
    private const int FieldRva = (int)FieldAttributes.HasFieldRVA;

    private const string CoreAssemblyName = "Synthetic.RuntimeCore";
    private const string TargetAssemblyName = "Synthetic.RuntimeTarget";
    private const string TargetNamespace = "Synthetic.RtTarget";

    private const int SlotToken = 0x0200_0002;
    private const int PairToken = 0x0200_0003;

    private const int ObjectTypeReferenceToken = 0x0100_0001;
    private const int ValueTypeTypeReferenceToken = 0x0100_0002;
    private const int EnumTypeReferenceToken = 0x0100_0003;

    private const int FieldRvaRowToken = 0x1D00_0001;

    private const string ExactConstructedSlotOutcomeSha256 =
        "00f45ae4c417a88bd974794ce5461c7f98e034d348d0f1ac1a87d81219490955";

    /// <summary>
    /// Proves one exact runtime construction is selected among several simultaneously loaded same-TypeDef candidates
    /// using the complete construction identity alone.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_construction_is_selected_among_same_typedef_candidates()
    {
        var world = BuildWorld();
        var metadata = Bind(world, "Slot<int>");
        var strategy = Strategy(world, "PlainStatic");
        var probes = ExpressionV2CapabilityProbeSet.Create();

        var selection = Select(
            metadata,
            strategy,
            [
                Candidate(world, SlotToken, Arguments(world, "string"), 0x100),
                Candidate(world, SlotToken, Arguments(world, "int"), 0x200),
                Candidate(world, SlotToken, Arguments(world, "long"), 0x300),
                Candidate(world, PairToken, Arguments(world, "int", "string"), 0x400),
            ],
            probes);

        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, selection.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeConstructionIssue.None, selection.Issue);
        Assert.Null(selection.DiagnosticCode);
        Assert.Null(selection.ReachedBound);
        Assert.Equal(4, selection.DistinctConstructionCount);
        Assert.Equal(1, selection.MatchingConstructionCount);
        Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, selection.Strategy);

        var selected = Assert.IsType<StaticFieldV2RuntimeConstructionCandidate>(selection.SelectedCandidate);
        Assert.Equal(SlotToken, selected.TypeDefinitionToken);
        Assert.Equal(0x20200UL, selected.MethodTableAddress);
        Assert.Equal(world.Target, selected.DefinitionModule);
        Assert.Equal(world.Target.ContainingAssembly, selected.Assembly);
        Assert.Equal(1, selected.ClosedArgumentCount);
        Assert.Equal(8, selected.PointerWidth);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.RuntimeConstructionEvidenceSuppliedByCaller,
            selection.DeclaredCoverageBoundaries);
        Assert.Equal(1, selection.CapabilityCallLedger.RuntimeConstructionCallCount);
    }

    /// <summary>
    /// Proves four closed constructions of one generic TypeDef each select a distinct runtime construction, a distinct
    /// static slot address, and a distinct decoded value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Four_closed_constructions_select_distinct_slots_and_values()
    {
        var world = BuildWorld();
        var strategy = Strategy(world, "PlainStatic");
        var spellings = new[] { "int", "long", "string", "bool" };
        var candidates = ImmutableArray.CreateRange(
            spellings.Select((spelling, index) =>
                Candidate(world, SlotToken, Arguments(world, spelling), (ulong)(0x1000 * (index + 1)))));

        var slotAddresses = new HashSet<ulong>();
        var slotDigests = new HashSet<string>();
        var decodedDigests = new HashSet<string>();
        for (var index = 0; index < spellings.Length; index++)
        {
            var selection = Select(Bind(world, $"Slot<{spellings[index]}>"), strategy, candidates);
            Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, selection.ResultKind);
            Assert.Equal(candidates[index], selection.SelectedCandidate);

            var slotAddress = 0x5000_0000UL + (ulong)(0x40 * (index + 1));
            var slot = Acquire(strategy, readWidth: 8, selection: selection, slotAddress: slotAddress);
            Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, slot.ResultKind);
            Assert.Equal(slotAddress, slot.Slot!.SlotAddress);
            Assert.Equal(slotAddress, slot.Slot!.EffectiveAddress);
            Assert.True(slotAddresses.Add(slotAddress));
            Assert.True(slotDigests.Add(slot.Sha256));

            var value = Decode(
                MetadataClosedTypeIdentity.Primitive(MetadataPrimitiveTypeKind.Int64),
                Bytes((byte)(index + 1), 0, 0, 0, 0, 0, 0, 0));
            Assert.Equal(StaticFieldV2RuntimeValueResultKind.Exact, value.ResultKind);
            Assert.Equal(index + 1L, value.SignedValue);
            Assert.True(decodedDigests.Add(value.Sha256));
        }

        Assert.Equal(4, slotAddresses.Count);
        Assert.Equal(4, slotDigests.Count);
        Assert.Equal(4, decodedDigests.Count);
    }

    /// <summary>
    /// Proves two candidates differing only in ordered argument position are distinct construction identities and that
    /// the ordered vector alone selects between them.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Candidates_differing_only_in_argument_order_remain_distinct()
    {
        var world = BuildWorld();
        var strategy = Strategy(world, "PlainStatic");
        var forward = Candidate(world, PairToken, Arguments(world, "int", "string"), 0x900);
        var reversed = Candidate(world, PairToken, Arguments(world, "string", "int"), 0x900);

        Assert.NotEqual(forward, reversed);
        Assert.NotEqual(forward.Sha256, reversed.Sha256);
        Assert.Equal(forward.MethodTableAddress, reversed.MethodTableAddress);
        Assert.Equal(forward.RuntimeTypeHandleAddress, reversed.RuntimeTypeHandleAddress);

        var selection = Select(Bind(world, "Pair<int, string>"), strategy, [forward, reversed]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, selection.ResultKind);
        Assert.Equal(forward, selection.SelectedCandidate);
        Assert.Equal(2, selection.DistinctConstructionCount);

        var other = Select(Bind(world, "Pair<string, int>"), strategy, [forward, reversed]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, other.ResultKind);
        Assert.Equal(reversed, other.SelectedCandidate);
    }

    /// <summary>
    /// Proves candidate presentation order never selects: the same candidate set in reversed order yields a byte
    /// identical selection, and duplicate identical rows collapse into one group.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reversed_candidate_presentation_yields_identical_selection()
    {
        var world = BuildWorld();
        var metadata = Bind(world, "Slot<int>");
        var strategy = Strategy(world, "PlainStatic");
        var candidates = ImmutableArray.Create(
            Candidate(world, SlotToken, Arguments(world, "string"), 0x100),
            Candidate(world, SlotToken, Arguments(world, "int"), 0x200),
            Candidate(world, SlotToken, Arguments(world, "long"), 0x300));

        var forward = Select(metadata, strategy, candidates);
        var backward = Select(metadata, strategy, [.. candidates.Reverse()]);

        Assert.Equal(forward.ResultKind, backward.ResultKind);
        Assert.Equal(forward.SelectedCandidate, backward.SelectedCandidate);
        Assert.Equal(forward.DistinctConstructionCount, backward.DistinctConstructionCount);
        Assert.Equal(forward.MatchingConstructionCount, backward.MatchingConstructionCount);
        Assert.Equal(forward.Issue, backward.Issue);
        Assert.Equal(forward.DiagnosticCode, backward.DiagnosticCode);
        Assert.Equal(forward.ObservedCount, backward.ObservedCount);
        Assert.Equal(forward.SelectedCandidate!.Sha256, backward.SelectedCandidate!.Sha256);
        Assert.True(forward.SelectedCandidate!.CanonicalBytes.AsSpan()
            .SequenceEqual(backward.SelectedCandidate!.CanonicalBytes.AsSpan()));
        Assert.NotEqual(forward.Request.Sha256, backward.Request.Sha256);

        var duplicated = Select(
            metadata,
            strategy,
            [candidates[1], candidates[0], candidates[1], candidates[2], candidates[1]]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, duplicated.ResultKind);
        Assert.Equal(3, duplicated.DistinctConstructionCount);
        Assert.Equal(candidates[1], duplicated.SelectedCandidate);
    }

    /// <summary>
    /// Proves the two exactness failures W8.1 froze: zero matching complete identities is absent and two or more
    /// distinct simultaneously loaded identities sharing one TypeDef and argument vector are ambiguous.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Absent_and_ambiguous_constructions_stop_prefix_free()
    {
        var world = BuildWorld();
        var metadata = Bind(world, "Slot<int>");
        var strategy = Strategy(world, "PlainStatic");

        var absent = Select(
            metadata,
            strategy,
            [
                Candidate(world, SlotToken, Arguments(world, "string"), 0x100),
                Candidate(world, PairToken, Arguments(world, "int", "string"), 0x200),
            ]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Absent, absent.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeConstructionIssue.ConstructionAbsent, absent.Issue);
        Assert.Null(absent.SelectedCandidate);
        Assert.Equal(0, absent.MatchingConstructionCount);
        Assert.Equal(2, absent.ObservedCount);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelection.ConstructionAbsentCode,
            absent.DiagnosticCode);

        var ambiguous = Select(
            metadata,
            strategy,
            [
                Candidate(world, SlotToken, Arguments(world, "int"), 0x200, loaderAllocator: 0x7000),
                Candidate(world, SlotToken, Arguments(world, "int"), 0x200, loaderAllocator: 0x7100),
                Candidate(world, SlotToken, Arguments(world, "string"), 0x300),
            ]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Ambiguous, ambiguous.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeConstructionIssue.ConstructionAmbiguous, ambiguous.Issue);
        Assert.Null(ambiguous.SelectedCandidate);
        Assert.Equal(3, ambiguous.DistinctConstructionCount);
        Assert.Equal(2, ambiguous.MatchingConstructionCount);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelection.ConstructionAmbiguousCode,
            ambiguous.DiagnosticCode);
    }

    /// <summary>
    /// Proves the declared runtime-construction cap stops at cap plus one before any grouping and before the runtime
    /// construction capability is ever acquired.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Candidate_cap_plus_one_stops_at_the_declared_bound()
    {
        var world = BuildWorld();
        var arguments = Arguments(world, "int");
        var builder = ImmutableArray.CreateBuilder<StaticFieldV2RuntimeConstructionCandidate>(
            StaticFieldV2Limits.MaximumRuntimeConstructionCount + 1);
        for (var index = 0; index <= StaticFieldV2Limits.MaximumRuntimeConstructionCount; index++)
        {
            builder.Add(Candidate(world, SlotToken, arguments, 0x10_0000UL + (ulong)(index * 0x20)));
        }

        var probes = ExpressionV2CapabilityProbeSet.Create(
            runtimeConstruction: static () => throw new InvalidOperationException("bounded stop must not acquire"));
        var selection = Select(Bind(world, "Slot<int>"), Strategy(world, "PlainStatic"), builder.MoveToImmutable(), probes);

        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.NonExact, selection.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeConstructionIssue.CandidateCountBoundReached, selection.Issue);
        Assert.Null(selection.SelectedCandidate);
        Assert.Equal(StaticFieldV2Limits.MaximumRuntimeConstructionCount + 1, selection.ObservedCount);
        Assert.Equal(
            StaticFieldV2Limits.MaximumRuntimeConstructionCount,
            selection.ReachedBound!.Value);
        Assert.Equal("expression-v2.runtime.constructions", selection.ReachedBound!.Name);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionSelection.ConstructionCountBoundReachedCode,
            selection.DiagnosticCode);
        Assert.Equal(0, probes.TotalCallCount);
        Assert.True(selection.CapabilityCallLedger.IsZero);
    }

    /// <summary>
    /// Proves every frozen per-strategy slot requirement and every frozen per-strategy rejection, including the
    /// metadata literal that owns no slot at all.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_strategy_slot_validation_is_frozen()
    {
        var world = BuildWorld();
        var constructed = Strategy(world, "PlainStatic");
        var threadRelative = Strategy(world, "PlainStatic", threadStatic: true);
        var moduleRva = Strategy(world, "RvaBlob");
        var literal = Strategy(world, "LiteralAnswer");

        var constructedSelection = ExactSelection(world, constructed);
        var threadSelection = ExactSelection(world, threadRelative);
        var thread = StaticFieldV2SelectedThreadIdentity.Create(8, 0x4400, 4711, 9);

        var exactConstructed = Acquire(constructed, 4, constructedSelection, slotAddress: 0x5100);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, exactConstructed.ResultKind);
        Assert.Equal(StaticFieldV2StaticSlotIssue.None, exactConstructed.Issue);
        Assert.Null(exactConstructed.Slot!.SelectedThread);
        Assert.Null(exactConstructed.Slot!.ModuleContent);
        Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, exactConstructed.Slot!.Strategy);
        Assert.Equal(4, exactConstructed.Slot!.ReadWidth);
        Assert.Equal(
            constructed.FieldDefinitionToken,
            exactConstructed.Slot!.FieldDefinitionToken);

        var exactThread = Acquire(threadRelative, 4, threadSelection, slotAddress: 0x5200, selectedThread: thread);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, exactThread.ResultKind);
        Assert.Equal(thread, exactThread.Slot!.SelectedThread);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.SelectedThreadEvidenceSuppliedByCaller,
            exactThread.DeclaredCoverageBoundaries);

        var exactRva = AcquireRva(moduleRva, world);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, exactRva.ResultKind);
        Assert.Null(exactRva.Slot!.OwnerConstruction);
        Assert.Null(exactRva.Slot!.SlotAddress);
        Assert.Equal(FieldRvaRowToken, exactRva.Slot!.FieldRvaRowToken);
        Assert.Equal(0x2000U, exactRva.Slot!.MappedRelativeVirtualAddress);
        Assert.Equal(0x40_2000UL, exactRva.Slot!.MappedAddress);
        Assert.Equal(0x40_2000UL, exactRva.Slot!.EffectiveAddress);
        Assert.Equal(world.Target.ModuleContent, exactRva.Slot!.ModuleContent);

        AssertSlotStop(
            Acquire(constructed, 4, slotAddress: 0x5100),
            StaticFieldV2StaticSlotIssue.ConstructionSelectionRequired);
        AssertSlotStop(
            Acquire(constructed, 4, constructedSelection, slotAddress: 0x5100, selectedThread: thread),
            StaticFieldV2StaticSlotIssue.ThreadIdentityNotPermitted);
        AssertSlotStop(
            Acquire(constructed, 4, constructedSelection),
            StaticFieldV2StaticSlotIssue.SlotAddressRequired);
        AssertSlotStop(
            Acquire(constructed, 4, constructedSelection, slotAddress: 0x5100, moduleContent: world.Target.ModuleContent),
            StaticFieldV2StaticSlotIssue.ModuleGeometryNotPermitted);
        AssertSlotStop(
            Acquire(threadRelative, 4, threadSelection, slotAddress: 0x5200),
            StaticFieldV2StaticSlotIssue.ThreadIdentityRequired);
        AssertSlotStop(
            Acquire(threadRelative, 4, slotAddress: 0x5200, selectedThread: thread),
            StaticFieldV2StaticSlotIssue.ConstructionSelectionRequired);

        var rvaSelection = Select(Bind(world, "Slot<int>"), moduleRva, []);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.NotRequired, rvaSelection.ResultKind);
        AssertSlotStop(
            AcquireRva(moduleRva, world, selection: rvaSelection),
            StaticFieldV2StaticSlotIssue.ConstructionSelectionNotPermitted);
        AssertSlotStop(
            AcquireRva(moduleRva, world, selectedThread: thread),
            StaticFieldV2StaticSlotIssue.ThreadIdentityNotPermitted);
        AssertSlotStop(
            AcquireRva(moduleRva, world, slotAddress: 0x5300),
            StaticFieldV2StaticSlotIssue.SlotAddressNotPermitted);
        AssertSlotStop(
            Acquire(moduleRva, 4, moduleContent: world.Target.ModuleContent),
            StaticFieldV2StaticSlotIssue.ModuleGeometryRequired);

        var literalSlot = Acquire(literal, 4);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Unsupported, literalSlot.ResultKind);
        Assert.Equal(StaticFieldV2StaticSlotIssue.MetadataLiteralHasNoSlot, literalSlot.Issue);
        Assert.Null(literalSlot.Slot);
        Assert.Equal(
            StaticFieldV2StaticSlotOutcome.MetadataLiteralHasNoSlotCode,
            literalSlot.DiagnosticCode);
        Assert.Empty(literalSlot.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves each strategy obeys its frozen capability-requirement vector executably: every capability the vector
    /// marks as not required is proven non-invoked by a poisoned probe that would throw if it were called.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Frozen_capability_requirements_are_obeyed_with_poisoned_probes()
    {
        var world = BuildWorld();
        var thread = StaticFieldV2SelectedThreadIdentity.Create(8, 0x4400, 4711, 9);

        var constructedProbes = Poison(threadIdentity: true, moduleContent: true, memoryRead: true);
        var constructed = Strategy(world, "PlainStatic");
        var constructedSelection = Select(
            Bind(world, "Slot<int>"),
            constructed,
            [Candidate(world, SlotToken, Arguments(world, "int"), 0x200)],
            constructedProbes);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, constructedSelection.ResultKind);
        var constructedSlot = Acquire(
            constructed,
            4,
            constructedSelection,
            slotAddress: 0x5100,
            probes: constructedProbes);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, constructedSlot.ResultKind);
        AssertCalls(constructedProbes, runtimeConstruction: 1, threadIdentity: 0, moduleContent: 0, staticSlot: 1);
        Assert.Equal(constructedProbes.TotalCallCount, constructedSlot.CapabilityCallLedger.TotalCallCount);

        var threadProbes = Poison(moduleContent: true, memoryRead: true);
        var threadRelative = Strategy(world, "PlainStatic", threadStatic: true);
        var threadSelection = Select(
            Bind(world, "Slot<int>"),
            threadRelative,
            [Candidate(world, SlotToken, Arguments(world, "int"), 0x200)],
            threadProbes);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, threadSelection.ResultKind);
        var threadSlot = Acquire(
            threadRelative,
            4,
            threadSelection,
            slotAddress: 0x5200,
            selectedThread: thread,
            probes: threadProbes);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, threadSlot.ResultKind);
        AssertCalls(threadProbes, runtimeConstruction: 1, threadIdentity: 1, moduleContent: 0, staticSlot: 1);

        var rvaProbes = Poison(runtimeConstruction: true, threadIdentity: true, staticSlot: true, memoryRead: true);
        var moduleRva = Strategy(world, "RvaBlob");
        var rvaSelection = Select(
            Bind(world, "Slot<int>"),
            moduleRva,
            [Candidate(world, SlotToken, Arguments(world, "int"), 0x200)],
            rvaProbes);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.NotRequired, rvaSelection.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeConstructionIssue.ConstructionNotRequiredForStrategy,
            rvaSelection.Issue);
        var rvaSlot = AcquireRva(moduleRva, world, probes: rvaProbes);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Exact, rvaSlot.ResultKind);
        AssertCalls(rvaProbes, runtimeConstruction: 0, threadIdentity: 0, moduleContent: 1, staticSlot: 0);

        var literalProbes = Poison(
            runtimeConstruction: true,
            threadIdentity: true,
            moduleContent: true,
            staticSlot: true,
            memoryRead: true);
        var literal = Strategy(world, "LiteralAnswer");
        var literalSelection = Select(Bind(world, "Slot<int>"), literal, [], literalProbes);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.NotRequired, literalSelection.ResultKind);
        var literalSlot = Acquire(literal, 4, probes: literalProbes);
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Unsupported, literalSlot.ResultKind);
        Assert.Equal(0, literalProbes.TotalCallCount);
        Assert.True(literalSelection.CapabilityCallLedger.IsZero);
        Assert.True(literalSlot.CapabilityCallLedger.IsZero);

        foreach (var strategy in new[] { constructed, threadRelative, moduleRva, literal })
        {
            var vector = strategy.CapabilityRequirements;
            foreach (var capability in Enum.GetValues<StaticFieldV2StorageCapability>())
            {
                if (vector.For(capability) != StaticFieldV2CapabilityRequirement.NotRequired)
                {
                    continue;
                }
                var probes = strategy.Strategy switch
                {
                    StaticFieldV2StorageStrategy.ConstructedSlot => constructedProbes,
                    StaticFieldV2StorageStrategy.ThreadRelativeSlot => threadProbes,
                    StaticFieldV2StorageStrategy.ModuleRva => rvaProbes,
                    _ => literalProbes,
                };
                Assert.Equal(0, probes.CallCount(capability));
            }
        }
    }

    /// <summary>
    /// Proves every admitted value shape decodes exactly from copied raw bytes with correct signedness, width,
    /// and target-width native integers supplied as a fact rather than assumed.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_admitted_value_shape_decodes_exactly()
    {
        var world = BuildWorld();

        AssertUnsigned(Decode(Primitive(MetadataPrimitiveTypeKind.Boolean), Bytes(1)), StaticFieldV2RuntimeValueKind.Boolean, 1);
        AssertUnsigned(Decode(Primitive(MetadataPrimitiveTypeKind.Boolean), Bytes(0)), StaticFieldV2RuntimeValueKind.Boolean, 0);
        AssertUnsigned(Decode(Primitive(MetadataPrimitiveTypeKind.Char), Bytes(0x41, 0x00)), StaticFieldV2RuntimeValueKind.Char, 0x41);
        AssertSigned(Decode(Primitive(MetadataPrimitiveTypeKind.Int8), Bytes(0xFF)), StaticFieldV2RuntimeValueKind.Int8, -1);
        AssertUnsigned(Decode(Primitive(MetadataPrimitiveTypeKind.UInt8), Bytes(0xFF)), StaticFieldV2RuntimeValueKind.UInt8, 255);
        AssertSigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.Int16), Bytes(0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.Int16,
            -1);
        AssertUnsigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.UInt16), Bytes(0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.UInt16,
            ushort.MaxValue);
        AssertSigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.Int32), Bytes(0xFF, 0xFF, 0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.Int32,
            -1);
        AssertUnsigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.UInt32), Bytes(0xFF, 0xFF, 0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.UInt32,
            uint.MaxValue);
        AssertSigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.Int64), Bytes(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.Int64,
            -1);
        AssertUnsigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.UInt64), Bytes(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.UInt64,
            ulong.MaxValue);

        var single = Decode(Primitive(MetadataPrimitiveTypeKind.Single), Bytes(0x00, 0x00, 0xC0, 0x3F));
        Assert.Equal(StaticFieldV2RuntimeValueKind.Single, single.ValueKind);
        Assert.Equal(0x3FC0_0000UL, single.FloatingBitPattern);
        Assert.Equal(1.5f, single.SingleValue);
        Assert.Null(single.DoubleValue);

        var doubleValue = Decode(
            Primitive(MetadataPrimitiveTypeKind.Double),
            Bytes(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x40));
        Assert.Equal(StaticFieldV2RuntimeValueKind.Double, doubleValue.ValueKind);
        Assert.Equal(2.5d, doubleValue.DoubleValue);
        Assert.Null(doubleValue.SingleValue);

        AssertSigned(
            Decode(Primitive(MetadataPrimitiveTypeKind.NativeInt), Bytes(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF)),
            StaticFieldV2RuntimeValueKind.NativeInt,
            -1);
        var narrowNative = Decode(
            Primitive(MetadataPrimitiveTypeKind.NativeUInt),
            Bytes(0xFF, 0xFF, 0xFF, 0xFF),
            pointerWidth: 4);
        Assert.Equal(StaticFieldV2RuntimeValueKind.NativeUnsignedInt, narrowNative.ValueKind);
        Assert.Equal(uint.MaxValue, narrowNative.UnsignedValue);
        Assert.Equal(4, narrowNative.DeclaredWidth);

        var enumType = Argument(world, "Synthetic.RtTarget.Colors");
        var enumValue = Decode(
            enumType,
            Bytes(0x07, 0x00, 0x00, 0x00),
            enumUnderlyingKind: MetadataPrimitiveTypeKind.Int32);
        Assert.Equal(StaticFieldV2RuntimeValueKind.EnumUnderlying, enumValue.ValueKind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, enumValue.PayloadKind);
        Assert.Equal(7L, enumValue.SignedValue);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.EnumUnderlyingKindSuppliedByCaller,
            enumValue.DeclaredCoverageBoundaries);

        var nullableType = Argument(world, "int?");
        var layout = StaticFieldV2NullableLayoutFact.Create(8, hasValueOffset: 4, valueOffset: 0, valueByteCount: 4);
        var absent = Decode(
            nullableType,
            Bytes(0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00),
            nullableLayout: layout);
        Assert.Equal(StaticFieldV2RuntimeValueKind.NullableAbsent, absent.ValueKind);
        Assert.False(absent.HasNullableValue);
        Assert.Null(absent.SignedValue);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, absent.PayloadKind);

        var present = Decode(
            nullableType,
            Bytes(0x2A, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00),
            nullableLayout: layout);
        Assert.Equal(StaticFieldV2RuntimeValueKind.NullablePresent, present.ValueKind);
        Assert.True(present.HasNullableValue);
        Assert.Equal(42L, present.SignedValue);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.NullableLayoutSuppliedByCaller,
            present.DeclaredCoverageBoundaries);

        var nullReference = Decode(Primitive(MetadataPrimitiveTypeKind.Object), Bytes(0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Equal(StaticFieldV2RuntimeValueKind.NullReference, nullReference.ValueKind);
        Assert.Null(nullReference.ReferenceAddress);

        var objectReference = Decode(
            Argument(world, "Synthetic.RtTarget.Widget"),
            Bytes(0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueKind.ObjectReference, objectReference.ValueKind);
        Assert.Equal(0x1234_5678UL, objectReference.ReferenceAddress);

        var stringReference = Decode(
            Primitive(MetadataPrimitiveTypeKind.String),
            Bytes(0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueKind.StringReference, stringReference.ValueKind);
        Assert.Equal(0x10UL, stringReference.ReferenceAddress);

        var arrayReference = Decode(
            MetadataClosedTypeIdentity.SzArray(Primitive(MetadataPrimitiveTypeKind.Int32)),
            Bytes(0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueKind.ObjectReference, arrayReference.ValueKind);
        Assert.Equal(0x20UL, arrayReference.ReferenceAddress);

        var probes = Poison(
            runtimeConstruction: true,
            threadIdentity: true,
            moduleContent: true,
            staticSlot: true,
            memoryRead: true);
        var pure = StaticFieldV2ValueDecoder.DecodeValue(
            StaticFieldV2RuntimeValueRequest.Create(
                Primitive(MetadataPrimitiveTypeKind.Int32),
                Bytes(0x01, 0x00, 0x00, 0x00),
                8,
                capabilityProbes: probes));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Exact, pure.ResultKind);
        Assert.Equal(0, probes.TotalCallCount);
        Assert.True(pure.CapabilityCallLedger.IsZero);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.RawValueBytesCopiedByCaller,
            pure.DeclaredCoverageBoundaries);
        Assert.Contains(
            StaticFieldV2RuntimeCoverageBoundary.TargetPointerWidthSuppliedByCaller,
            pure.DeclaredCoverageBoundaries);
    }

    /// <summary>
    /// Proves a byte count disagreeing with the declared width is invalid and every unadmitted shape or missing
    /// caller-supplied fact is a typed non-admission rather than a silent guess.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Rejected_value_shapes_and_byte_counts_stop_prefix_free()
    {
        var world = BuildWorld();

        var shortBytes = Decode(Primitive(MetadataPrimitiveTypeKind.Int32), Bytes(0x01, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, shortBytes.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
            shortBytes.Issue);
        Assert.Null(shortBytes.ValueKind);
        Assert.Equal(4, shortBytes.DeclaredWidth);
        Assert.Equal(2, shortBytes.ObservedCount);

        var wideReference = Decode(Primitive(MetadataPrimitiveTypeKind.String), Bytes(0x01, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, wideReference.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
            wideReference.Issue);

        var badBoolean = Decode(Primitive(MetadataPrimitiveTypeKind.Boolean), Bytes(0x02));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, badBoolean.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.FlagEncodingInvalid, badBoolean.Issue);

        var valueTypeShape = Decode(
            Argument(world, "Synthetic.RtTarget.Point"),
            Bytes(0x01, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Unsupported, valueTypeShape.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.UnsupportedValueShape, valueTypeShape.Issue);
        Assert.Equal(0, valueTypeShape.DeclaredWidth);

        var enumWithoutFact = Decode(
            Argument(world, "Synthetic.RtTarget.Colors"),
            Bytes(0x01, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Unsupported, enumWithoutFact.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.EnumUnderlyingEvidenceUnavailable,
            enumWithoutFact.Issue);

        var nullableType = Argument(world, "int?");
        var nullableWithoutLayout = Decode(nullableType, Bytes(0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Unsupported, nullableWithoutLayout.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.NullableLayoutEvidenceUnavailable,
            nullableWithoutLayout.Issue);

        var mismatchedLayout = Decode(
            nullableType,
            Bytes(0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00),
            nullableLayout: StaticFieldV2NullableLayoutFact.Create(
                16,
                hasValueOffset: 8,
                valueOffset: 0,
                valueByteCount: 8));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, mismatchedLayout.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.NullableLayoutInvalid, mismatchedLayout.Issue);

        var wrongNullableCount = Decode(
            nullableType,
            Bytes(0x01, 0x00, 0x00, 0x00),
            nullableLayout: StaticFieldV2NullableLayoutFact.Create(
                8,
                hasValueOffset: 4,
                valueOffset: 0,
                valueByteCount: 4));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, wrongNullableCount.ResultKind);
        Assert.Equal(
            StaticFieldV2RuntimeValueIssue.RawByteCountDisagreesWithDeclaredWidth,
            wrongNullableCount.Issue);

        var badFlag = Decode(
            nullableType,
            Bytes(0x2A, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00),
            nullableLayout: StaticFieldV2NullableLayoutFact.Create(
                8,
                hasValueOffset: 4,
                valueOffset: 0,
                valueByteCount: 4));
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Invalid, badFlag.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.FlagEncodingInvalid, badFlag.Issue);
    }

    /// <summary>
    /// Proves replay equality, defensive copies, guarded mint capability, and the frozen golden digest of one exact
    /// constructed-slot outcome.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Replay_equality_defensive_copies_and_guarded_issuance_hold()
    {
        var world = BuildWorld();
        var constructed = Strategy(world, "PlainStatic");
        var first = Acquire(constructed, 4, ExactSelection(world, constructed), slotAddress: 0x5100);
        var second = Acquire(
            BuildWorldStrategy(),
            4,
            ExactSelection(BuildWorld(), BuildWorldStrategy()),
            slotAddress: 0x5100);

        Assert.Equal(first, second);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));
        Assert.Equal(ExactConstructedSlotOutcomeSha256, first.Sha256);

        var originalBytes = first.CanonicalBytes;
        var originalSha = first.Sha256;
        var candidate = first.Slot!.OwnerConstruction!.SelectedCandidate!;
        var originalArgument = candidate.ClosedArguments[0];
        var selectionRequest = first.Slot!.OwnerConstruction!.Request;
        var originalCandidate = selectionRequest.Candidates[0];

        ImmutableCollectionsMarshal.AsArray(first.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(first.Slot!.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(first.DeclaredCoverageBoundaries)![0] =
            StaticFieldV2RuntimeCoverageBoundary.RawValueBytesCopiedByCaller;
        ImmutableCollectionsMarshal.AsArray(candidate.ClosedArguments)![0] =
            Primitive(MetadataPrimitiveTypeKind.String);
        ImmutableCollectionsMarshal.AsArray(selectionRequest.Candidates)![0] =
            Candidate(world, PairToken, Arguments(world, "int", "string"), 0x900);

        Assert.Equal(originalSha, first.Sha256);
        Assert.True(originalBytes.AsSpan().SequenceEqual(first.CanonicalBytes.AsSpan()));
        Assert.Equal(
            StaticFieldV2RuntimeCoverageBoundary.RuntimeConstructionEvidenceSuppliedByCaller,
            first.DeclaredCoverageBoundaries[0]);
        Assert.Equal(originalArgument, candidate.ClosedArguments[0]);
        Assert.Equal(originalCandidate, selectionRequest.Candidates[0]);

        var value = Decode(Primitive(MetadataPrimitiveTypeKind.Int32), Bytes(0x2A, 0x00, 0x00, 0x00));
        var originalRaw = value.Request.RawBytes;
        ImmutableCollectionsMarshal.AsArray(value.Request.RawBytes)![0] ^= 0x5A;
        Assert.True(originalRaw.AsSpan().SequenceEqual(value.Request.RawBytes.AsSpan()));
        Assert.Equal(value, Decode(Primitive(MetadataPrimitiveTypeKind.Int32), Bytes(0x2A, 0x00, 0x00, 0x00)));
        Assert.NotEqual(value, Decode(Primitive(MetadataPrimitiveTypeKind.Int32), Bytes(0x2B, 0x00, 0x00, 0x00)));

        var guarded = Assert.Throws<ArgumentException>(() => StaticFieldV2StaticSlotIdentity.Create(
            new object(),
            StaticFieldV2StorageStrategy.ConstructedSlot,
            null,
            0x0400_0001,
            0x5100,
            4,
            null,
            null,
            null,
            null,
            null));
        Assert.Equal("mintCapability", guarded.ParamName);
        Assert.False(StaticFieldV2StaticSlotOutcome.OwnsRowMintCapability(new object()));

        Assert.Throws<ArgumentException>(() => StaticFieldV2RuntimeConstructionCandidate.Create(
            0x1000,
            0x2000,
            world.Target,
            SlotToken,
            world.Target,
            world.Target.ContainingAssembly,
            0x7000,
            0x8000,
            default));

        return;

        StaticFieldV2StorageStrategyOutcome BuildWorldStrategy() => Strategy(BuildWorld(), "PlainStatic");
    }

    /// <summary>Proves the public surface stays closed, issuer-guarded, and documented.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Public_surface_is_closed_and_documented_as_draft()
    {
        var publicTypes = new[]
        {
            typeof(StaticFieldV2RuntimeConstructionSelectionKind),
            typeof(StaticFieldV2RuntimeConstructionIssue),
            typeof(StaticFieldV2StaticSlotResultKind),
            typeof(StaticFieldV2StaticSlotIssue),
            typeof(StaticFieldV2RuntimeValueKind),
            typeof(StaticFieldV2RuntimeValueResultKind),
            typeof(StaticFieldV2RuntimeValueIssue),
            typeof(StaticFieldV2RuntimeCoverageBoundary),
            typeof(StaticFieldV2SelectedThreadIdentity),
            typeof(StaticFieldV2NullableLayoutFact),
            typeof(StaticFieldV2RuntimeConstructionCandidate),
            typeof(StaticFieldV2RuntimeConstructionRequest),
            typeof(StaticFieldV2RuntimeConstructionSelection),
            typeof(StaticFieldV2StaticSlotIdentity),
            typeof(StaticFieldV2StaticSlotRequest),
            typeof(StaticFieldV2StaticSlotOutcome),
            typeof(StaticFieldV2RuntimeValueRequest),
            typeof(StaticFieldV2RuntimeValueOutcome),
            typeof(StaticFieldV2RuntimeConstructionBinder),
            typeof(StaticFieldV2ValueDecoder),
        };

        foreach (var type in publicTypes)
        {
            Assert.True(type.IsPublic);
            Assert.True(type.IsEnum || type.IsSealed);
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            if (type == typeof(StaticFieldV2RuntimeConstructionBinder))
            {
                Assert.Equal(["AcquireStaticSlot", "SelectConstruction"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2ValueDecoder))
            {
                Assert.Equal(["DecodeValue"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2SelectedThreadIdentity) ||
                     type == typeof(StaticFieldV2NullableLayoutFact) ||
                     type == typeof(StaticFieldV2RuntimeConstructionCandidate) ||
                     type == typeof(StaticFieldV2RuntimeConstructionRequest) ||
                     type == typeof(StaticFieldV2StaticSlotRequest) ||
                     type == typeof(StaticFieldV2RuntimeValueRequest))
            {
                Assert.Equal(["Create"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }

        Assert.Empty(typeof(StaticFieldV2StaticSlotIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.NotNull(typeof(StaticFieldV2StaticSlotIdentity).GetMethod(
            "Create",
            BindingFlags.NonPublic | BindingFlags.Static));

        AssertPublicDraftXml(publicTypes);
    }

    private static void AssertCalls(
        ExpressionV2CapabilityProbeSet probes,
        int runtimeConstruction,
        int threadIdentity,
        int moduleContent,
        int staticSlot)
    {
        Assert.Equal(runtimeConstruction, probes.RuntimeConstructionCallCount);
        Assert.Equal(threadIdentity, probes.ThreadIdentityCallCount);
        Assert.Equal(moduleContent, probes.ModuleContentCallCount);
        Assert.Equal(staticSlot, probes.StaticSlotAcquisitionCallCount);
        Assert.Equal(0, probes.MemoryReadCallCount);
    }

    private static void AssertSlotStop(
        StaticFieldV2StaticSlotOutcome outcome,
        StaticFieldV2StaticSlotIssue issue)
    {
        Assert.Equal(StaticFieldV2StaticSlotResultKind.Invalid, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Null(outcome.Slot);
        Assert.Equal(
            StaticFieldV2StaticSlotOutcome.SlotGeometryContradictedCode,
            outcome.DiagnosticCode);
    }

    private static void AssertSigned(
        StaticFieldV2RuntimeValueOutcome outcome,
        StaticFieldV2RuntimeValueKind kind,
        long expected)
    {
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.None, outcome.Issue);
        Assert.Equal(kind, outcome.ValueKind);
        Assert.Equal(expected, outcome.SignedValue);
        Assert.Null(outcome.UnsignedValue);
        Assert.Null(outcome.ReferenceAddress);
    }

    private static void AssertUnsigned(
        StaticFieldV2RuntimeValueOutcome outcome,
        StaticFieldV2RuntimeValueKind kind,
        ulong expected)
    {
        Assert.Equal(StaticFieldV2RuntimeValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2RuntimeValueIssue.None, outcome.Issue);
        Assert.Equal(kind, outcome.ValueKind);
        Assert.Equal(expected, outcome.UnsignedValue);
        Assert.Null(outcome.SignedValue);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2RuntimeConstructionBinder).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(typeDocumentation.Value));
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, static member =>
                    Assert.False(string.IsNullOrWhiteSpace(member.Value)));
            }
        }
    }

    private static ImmutableArray<byte> Bytes(params byte[] values) => [.. values];

    private static MetadataClosedTypeIdentity Primitive(MetadataPrimitiveTypeKind kind) =>
        MetadataClosedTypeIdentity.Primitive(kind);

    private static StaticFieldV2RuntimeValueOutcome Decode(
        MetadataClosedTypeIdentity declaredType,
        ImmutableArray<byte> rawBytes,
        int pointerWidth = 8,
        MetadataPrimitiveTypeKind? enumUnderlyingKind = null,
        StaticFieldV2NullableLayoutFact? nullableLayout = null) =>
        StaticFieldV2ValueDecoder.DecodeValue(
            StaticFieldV2RuntimeValueRequest.Create(
                declaredType,
                rawBytes,
                pointerWidth,
                enumUnderlyingKind,
                nullableLayout));

    private static StaticFieldV2RuntimeConstructionSelection Select(
        StaticFieldV2ClosedConstructionOutcome metadata,
        StaticFieldV2StorageStrategyOutcome strategy,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates,
        ExpressionV2CapabilityProbeSet? probes = null) =>
        StaticFieldV2RuntimeConstructionBinder.SelectConstruction(
            StaticFieldV2RuntimeConstructionRequest.Create(metadata, strategy, candidates, probes));

    private static StaticFieldV2RuntimeConstructionSelection ExactSelection(
        RuntimeWorld world,
        StaticFieldV2StorageStrategyOutcome strategy)
    {
        var selection = Select(
            Bind(world, "Slot<int>"),
            strategy,
            [Candidate(world, SlotToken, Arguments(world, "int"), 0x200)]);
        Assert.Equal(StaticFieldV2RuntimeConstructionSelectionKind.Exact, selection.ResultKind);
        return selection;
    }

    private static StaticFieldV2StaticSlotOutcome Acquire(
        StaticFieldV2StorageStrategyOutcome strategy,
        int readWidth,
        StaticFieldV2RuntimeConstructionSelection? selection = null,
        ulong? slotAddress = null,
        StaticFieldV2SelectedThreadIdentity? selectedThread = null,
        ModuleContentIdentity? moduleContent = null,
        ExpressionV2CapabilityProbeSet? probes = null) =>
        StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
            StaticFieldV2StaticSlotRequest.Create(
                strategy,
                readWidth,
                selection,
                slotAddress,
                selectedThread,
                moduleContent,
                capabilityProbes: probes));

    private static StaticFieldV2StaticSlotOutcome AcquireRva(
        StaticFieldV2StorageStrategyOutcome strategy,
        RuntimeWorld world,
        StaticFieldV2RuntimeConstructionSelection? selection = null,
        ulong? slotAddress = null,
        StaticFieldV2SelectedThreadIdentity? selectedThread = null,
        ExpressionV2CapabilityProbeSet? probes = null) =>
        StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
            StaticFieldV2StaticSlotRequest.Create(
                strategy,
                readWidth: 4,
                selection,
                slotAddress,
                selectedThread,
                world.Target.ModuleContent,
                FieldRvaRowToken,
                0x2000U,
                0x40_2000UL,
                probes));

    private static ExpressionV2CapabilityProbeSet Poison(
        bool runtimeConstruction = false,
        bool threadIdentity = false,
        bool moduleContent = false,
        bool staticSlot = false,
        bool memoryRead = false) =>
        ExpressionV2CapabilityProbeSet.Create(
            runtimeConstruction ? Throw : null,
            threadIdentity ? Throw : null,
            moduleContent ? Throw : null,
            staticSlot ? Throw : null,
            memoryRead ? Throw : null);

    private static void Throw() =>
        throw new InvalidOperationException("A capability marked NotRequired must never be acquired.");

    private static StaticFieldV2RuntimeConstructionCandidate Candidate(
        RuntimeWorld world,
        int typeDefinitionToken,
        ImmutableArray<MetadataClosedTypeIdentity> arguments,
        ulong seed,
        ulong loaderAllocator = 0x7000,
        ulong loadContext = 0x8000,
        StaticFieldMetadataModuleIdentity? definitionModule = null) =>
        StaticFieldV2RuntimeConstructionCandidate.Create(
            0x1_0000UL + seed,
            0x2_0000UL + seed,
            definitionModule ?? world.Target,
            typeDefinitionToken,
            world.Target,
            world.Target.ContainingAssembly,
            loaderAllocator,
            loadContext,
            arguments);

    private static ImmutableArray<MetadataClosedTypeIdentity> Arguments(RuntimeWorld world, params string[] spellings) =>
        [.. spellings.Select(spelling => Argument(world, spelling))];

    private static MetadataClosedTypeIdentity Argument(RuntimeWorld world, string spelling) =>
        Bind(world, $"Slot<{spelling}>").FlattenedArguments[0];

    private static StaticFieldV2ClosedConstructionOutcome Bind(RuntimeWorld world, string ownerSpelling)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse($"global::{TargetNamespace}.{ownerSpelling}.Current");
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        var nameBinding = StaticFieldV2TypeNameBinder.BindExplicitRoute(parsed.Descriptor!, world.ChainPortfolio);
        var outcome = StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
            nameBinding,
            world.Ancestry,
            world.Constraints);
        Assert.True(
            outcome.ResultKind == StaticFieldV2ClosedConstructionResultKind.Exact,
            $"Owner '{ownerSpelling}' bound as {outcome.ResultKind}/{outcome.Issue}.");
        return outcome;
    }

    private static StaticFieldV2StorageStrategyOutcome Strategy(
        RuntimeWorld world,
        string fieldName,
        bool threadStatic = false)
    {
        var row = Assert.Single(
            world.FieldCatalog.Rows,
            candidate => string.Equals(candidate.Name, fieldName, StringComparison.Ordinal));
        var outcome = StaticFieldV2StorageStrategyBinder.ClassifyStrategy(
            StaticFieldV2StorageStrategyRequest.Create(row, row.DeclaringTypeDefinition, threadStatic));
        Assert.Equal(StaticFieldV2StorageStrategyResultKind.Exact, outcome.ResultKind);
        return outcome;
    }

    private static RuntimeWorld BuildWorld()
    {
        var core = BuildModule(
            CoreAssemblyName,
            0xD100,
            '1',
            [
                new RuntimeTypeRow("System", "Object", PublicClassAttributes, null),
                new RuntimeTypeRow("System", "ValueType", PublicClassAttributes, 0x0200_0002),
                new RuntimeTypeRow("System", "Enum", PublicClassAttributes, 0x0200_0003),
                new RuntimeTypeRow("System", "Delegate", PublicClassAttributes, 0x0200_0002),
                new RuntimeTypeRow("System", "MulticastDelegate", PublicClassAttributes, 0x0200_0005),
                new RuntimeTypeRow("System", "Nullable`1", PublicClassAttributes, 0x0200_0003, GenericArity: 1),
            ],
            []);
        var target = BuildModule(
            TargetAssemblyName,
            0xD200,
            '2',
            [
                new RuntimeTypeRow(
                    TargetNamespace, "Slot`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new RuntimeTypeRow(
                    TargetNamespace, "Pair`2", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 2),
                new RuntimeTypeRow(TargetNamespace, "Widget", PublicClassAttributes, ObjectTypeReferenceToken),
                new RuntimeTypeRow(TargetNamespace, "Colors", PublicClassAttributes, EnumTypeReferenceToken),
                new RuntimeTypeRow(TargetNamespace, "Point", PublicClassAttributes, ValueTypeTypeReferenceToken),
                new RuntimeTypeRow(TargetNamespace, "Holder", PublicClassAttributes, ObjectTypeReferenceToken),
            ],
            [
                new RuntimeFieldRow("Holder", "PlainStatic", FieldPublic | FieldStatic, [0x06, 0x08]),
                new RuntimeFieldRow("Holder", "RvaBlob", FieldPublic | FieldStatic | FieldRva, [0x06, 0x08]),
                new RuntimeFieldRow("Holder", "LiteralAnswer", FieldPublic | FieldStatic | FieldLiteral, [0x06, 0x08]),
            ],
            [("System", "Object"), ("System", "ValueType"), ("System", "Enum")]);

        var ancestryWorld = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            core.Ancestry,
            target.Ancestry);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestryWorld.Ancestry.ResultKind);
        var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            ancestryWorld.Resolution,
            [core.Constraints, target.Constraints]);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, constraints.ResultKind);

        return new RuntimeWorld(
            target.Ancestry.Module,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            target.FieldCatalog);
    }

    private static RuntimeModule BuildModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<RuntimeTypeRow> namedTypes,
        ImmutableArray<RuntimeFieldRow> fields,
        ImmutableArray<(string NamespaceName, string TypeName)> typeReferences = default)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var referenceSpecs = typeReferences.IsDefault ? [] : typeReferences;

        var typeReferenceRows =
            ImmutableArray.CreateBuilder<MetadataTypeReferenceRowObservationIdentity>(referenceSpecs.Length);
        for (var index = 0; index < referenceSpecs.Length; index++)
        {
            typeReferenceRows.Add(W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                module,
                index + 1,
                referenceSpecs[index].NamespaceName,
                referenceSpecs[index].TypeName,
                0x2300_0001));
        }
        var assemblyReferenceRows = referenceSpecs.IsEmpty
            ? ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>.Empty
            : [W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, CoreAssemblyName)];

        var totalTypeCount = namedTypes.Length + 1;
        var fieldObservations = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>();
        var fieldStarts = new int[totalTypeCount];
        fieldStarts[0] = 1;
        for (var index = 0; index < namedTypes.Length; index++)
        {
            fieldStarts[index + 1] = fieldObservations.Count + 1;
            foreach (var field in fields.Where(row =>
                         string.Equals(row.TypeName, namedTypes[index].TypeName, StringComparison.Ordinal)))
            {
                fieldObservations.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                    module,
                    0x0400_0000 | checked(fieldObservations.Count + 1),
                    field.Attributes,
                    field.Name,
                    field.Signature));
            }
        }

        var genericParameterRows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var ownerToken = 0x0200_0002 + index;
            for (var number = 0; number < namedTypes[index].GenericArity; number++)
            {
                genericParameterRows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                    module,
                    0x2A00_0000 | checked(genericParameterRows.Count + 1),
                    number,
                    flags: 0,
                    ownerMetadataToken: ownerToken,
                    name: $"T{ownerToken & 0x00FF_FFFF}_{number}"));
            }
        }

        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: totalTypeCount,
                fieldDefinitionsExamined: fieldObservations.Count,
                typeDefinitionRowCount: totalTypeCount,
                fieldDefinitionRowCount: fieldObservations.Count,
                typeReferenceRowCount: typeReferenceRows.Count,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                genericParameterRowCount: genericParameterRows.Count));

        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x0200_0001,
            fieldListRowId: fieldStarts[0],
            methodListRowId: 1,
            namespaceName: string.Empty,
            typeName: "<Module>",
            typeAttributes: 0,
            extendsMetadataToken: null));
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x0200_0002 + index,
                fieldListRowId: fieldStarts[index + 1],
                methodListRowId: 1,
                namespaceName: row.NamespaceName,
                typeName: row.TypeName,
                typeAttributes: row.TypeAttributes,
                extendsMetadataToken: row.ExtendsMetadataToken));
        }

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows.MoveToImmutable(),
            pointers);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, typeDefinitions.ResultKind);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, typeDefinitions, []);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            genericParameterRows.Count == 0 ? default : genericParameterRows.ToImmutable());
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows.ToImmutable()),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(referenceEnds, assemblyReferenceRows),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));
        Assert.True(tables.AllTablesExact);

        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            compatibility,
            MetadataCompilerNameMappingCatalogIdentity.Create(authority));
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);

        var constraintCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(authority),
            []);
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            constraintCatalog.ResultKind);

        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            fieldObservations.ToImmutable());
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, fieldCatalog.ResultKind);

        return new RuntimeModule(
            new W8MetadataAncestryAuthorityContractTests.AncestryModule(
                module,
                compatibility,
                chainCatalog,
                tables),
            constraintCatalog,
            fieldCatalog);
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> NullCandidateSlots(
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            builder.Add(null);
        }
        return builder.MoveToImmutable();
    }

    private sealed record RuntimeTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        int GenericArity = 0);

    private sealed record RuntimeFieldRow(
        string TypeName,
        string Name,
        int Attributes,
        ImmutableArray<byte> Signature);

    private sealed record RuntimeModule(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Ancestry,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog);

    private sealed record RuntimeWorld(
        StaticFieldMetadataModuleIdentity Target,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity Constraints,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog)
    {
        internal MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio => Resolution.ChainPortfolio;
    }
}
