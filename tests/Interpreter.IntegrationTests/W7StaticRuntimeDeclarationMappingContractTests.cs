using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Validates detached, bounded runtime declaration mapping with complex synthetic evidence.</summary>
[Trait("Category", "Fast")]
public sealed class W7StaticRuntimeDeclarationMappingContractTests
{
    private const int DeclaringTypeToken = 0x02000011;
    private const int PayloadTypeToken = 0x02000012;
    private const int Int32TypeToken = 0x02000020;
    private const int StringTypeToken = 0x02000021;
    private const int NullableTypeToken = 0x02000022;
    private const int FieldToken = 0x04000031;

    private static readonly ClrmdSnapshotIdentity Snapshot = new(new string('1', 64));
    private static readonly ClrmdSnapshotIdentity ForeignSnapshot = new(new string('2', 64));
    private static readonly ClrmdRuntimeModuleIdentity Module = new(
        Snapshot,
        AppDomainAddress: 0x0000_1000,
        ModuleAddress: 0x0000_2000,
        ImageBase: 0x0040_0000,
        ImageSize: 0x0002_0000);
    private static readonly ClrmdRuntimeModuleIdentity CoreLibraryModule = new(
        Snapshot,
        AppDomainAddress: 0x0000_1000,
        ModuleAddress: 0x0000_3000,
        ImageBase: 0x0060_0000,
        ImageSize: 0x0004_0000);
    private static readonly ClrmdRuntimeModuleIdentity ForeignModule = new(
        ForeignSnapshot,
        AppDomainAddress: 0x0000_1000,
        ModuleAddress: 0x0000_2000,
        ImageBase: 0x0040_0000,
        ImageSize: 0x0002_0000);
    private static readonly ModuleContentIdentity ModuleContent = ModuleContentIdentity.FromDigest(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        4096,
        new string('a', 64));
    private static readonly ModuleContentIdentity DifferentModuleContent = ModuleContentIdentity.FromDigest(
        Guid.Parse("11111111-2222-3333-4444-666666666666"),
        4096,
        new string('b', 64));
    private static readonly ModuleContentIdentity CoreLibraryContent = ModuleContentIdentity.FromDigest(
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        8192,
        new string('c', 64));

    /// <summary>Proves runtime-type identities validate all physical coordinates and retain optional method tables.</summary>
    [Fact]
    public void Runtime_type_identity_rejects_foreign_or_malformed_physical_facts()
    {
        var exact = DeclaringType();
        var withoutMethodTable = RuntimeType(
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            methodTable: null);
        var x86Module = new ClrmdRuntimeModuleIdentity(
            Snapshot,
            AppDomainAddress: 0x1000,
            ModuleAddress: 0x2000,
            ImageBase: 0x1000_0000,
            ImageSize: 0x2000);
        var maximumX86MethodTable = CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            uint.MaxValue);

        Assert.Equal((ulong)0x7000, exact.MethodTable);
        Assert.Null(withoutMethodTable.MethodTable);
        Assert.Equal((ulong)uint.MaxValue, maximumX86MethodTable.MethodTable);
        Assert.Equal(DeclaringTypeToken, exact.TypeDefinitionToken);
        Assert.Equal("Incident.State", exact.FullName);
        Assert.False(exact.IsValueType);
        Assert.False(exact.IsPrimitive);
        Assert.False(exact.IsArray);
        Assert.False(exact.IsInterface);
        Assert.Empty(exact.GenericArguments);

        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            ForeignSnapshot,
            sizeof(ulong),
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(ulong),
            ForeignModule,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            default,
            sizeof(ulong),
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRuntimeType(
            Snapshot,
            16,
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(ulong),
            Module,
            ModuleContent,
            0x01000011,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(ulong),
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.\0State",
            0x7000));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x1_0000_0000));

        var overflowingX86Module = x86Module with
        {
            ImageBase = uint.MaxValue - 3,
            ImageSize = 8,
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            overflowingX86Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000));
        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(ulong),
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000,
            isPrimitive: true));
        Assert.Throws<ArgumentException>(() => CreateRuntimeType(
            Snapshot,
            sizeof(ulong),
            Module,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000,
            isArray: true,
            isInterface: true));
    }

    /// <summary>Proves runtime type and field names are capped before any canonical name copy.</summary>
    [Fact]
    public void Runtime_mapping_names_apply_the_exact_2048_character_bound()
    {
        var maximumName = new string('T', ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters);
        var overlongName = maximumName + "X";
        var owner = RuntimeType(Module, ModuleContent, DeclaringTypeToken, maximumName, 0x7000);
        var field = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            Int32Type(),
            name: maximumName);

        Assert.Equal(2048, owner.FullName.Length);
        Assert.Equal(2048, field.Name.Length);
        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.RuntimeNameCharacterBoundName,
                2048),
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound);
        Assert.Equal(
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound,
            ClrmdStaticRuntimeFieldIdentity.DeclaredRuntimeNameCharacterBound);
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeType(
            Module,
            ModuleContent,
            DeclaringTypeToken,
            overlongName,
            0x7000));
        Assert.Throws<ArgumentOutOfRangeException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            Int32Type(),
            name: overlongName));
    }

    /// <summary>
    /// Proves Product expectation and raw ClrMD type evidence stay separate until semantic composition.
    /// </summary>
    [Fact]
    public void Runtime_field_keeps_expected_decoder_separate_from_raw_observed_type()
    {
        var owner = DeclaringType();
        var int32Type = Int32Type();
        var stringType = StringType();
        var nullableType = NullableType();
        var payloadType = PayloadType();
        var mislabeledNullableOfString = RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            NullableTypeToken,
            "System.Nullable<System.Int32>",
            0x7300,
            isValueType: true,
            genericArguments: ImmutableArray.Create(stringType));
        var structPayload = RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload",
            0x7100,
            isValueType: true);
        var primitivePayload = RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload",
            0x7100,
            isValueType: true,
            isPrimitive: true);
        var arrayPayload = RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload[]",
            0x7100,
            isArray: true);
        var interfacePayload = RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.IPayload",
            0x7100,
            isInterface: true);
        var genericPayload = RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload<System.Int32>",
            0x7100,
            genericArguments: ImmutableArray.Create(int32Type));
        var nameOnlyInt32 = RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            Int32TypeToken,
            "System.Int32",
            0x71F0);

        var int32 = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, int32Type);
        var text = Field(owner, ClrmdStaticExpectedDecoderKind.String, stringType);
        var nullable = Field(owner, ClrmdStaticExpectedDecoderKind.NullableInt32, nullableType);
        var payload = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, payloadType);
        var expectedInt32ButPayload = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, payloadType);
        var expectedStringButPayload = Field(owner, ClrmdStaticExpectedDecoderKind.String, payloadType);
        var nullableLookalike = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.NullableInt32,
            mislabeledNullableOfString);
        var nameOnlyInt32Field = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, nameOnlyInt32);
        var structAsConcrete = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, structPayload);
        var primitiveAsConcrete = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, primitivePayload);
        var arrayAsConcrete = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, arrayPayload);
        var interfaceAsConcrete = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, interfacePayload);
        var genericAsConcrete = Field(owner, ClrmdStaticExpectedDecoderKind.ConcreteReference, genericPayload);

        Assert.Equal(ClrmdStaticExpectedDecoderKind.Int32, int32.ExpectedDecoderKind);
        Assert.Same(int32Type, int32.ObservedFieldType);
        Assert.Equal(ClrmdStaticExpectedDecoderKind.String, text.ExpectedDecoderKind);
        Assert.Same(stringType, text.ObservedFieldType);
        Assert.Same(nullableType, nullable.ObservedFieldType);
        Assert.Same(payloadType, payload.ObservedFieldType);
        Assert.Same(payloadType, expectedInt32ButPayload.ObservedFieldType);
        Assert.Same(payloadType, expectedStringButPayload.ObservedFieldType);
        Assert.Same(mislabeledNullableOfString, nullableLookalike.ObservedFieldType);
        Assert.Same(nameOnlyInt32, nameOnlyInt32Field.ObservedFieldType);
        Assert.Same(structPayload, structAsConcrete.ObservedFieldType);
        Assert.Same(primitivePayload, primitiveAsConcrete.ObservedFieldType);
        Assert.Same(arrayPayload, arrayAsConcrete.ObservedFieldType);
        Assert.Same(interfacePayload, interfaceAsConcrete.ObservedFieldType);
        Assert.Same(genericPayload, genericAsConcrete.ObservedFieldType);
        AssertCanonicalDifference(int32.CanonicalBytes, expectedInt32ButPayload.CanonicalBytes);
        AssertCanonicalDifference(payload.CanonicalBytes, structAsConcrete.CanonicalBytes);
        Assert.Single(nullableType.GenericArguments);
        Assert.Equal(int32Type, nullableType.GenericArguments[0]);
        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.GenericArgumentCountBoundName,
                1),
            ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound);

        Assert.Throws<ArgumentNullException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            observedType: null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Field(
            owner,
            (ClrmdStaticExpectedDecoderKind)99,
            payloadType));
        Assert.Throws<ArgumentException>(() => RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Pair<System.Int32,System.String>",
            0x7400,
            genericArguments: ImmutableArray.Create(int32Type, stringType)));

        var foreignPayload = RuntimeType(
            ForeignModule,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload",
            0x9000,
            snapshot: ForeignSnapshot);
        Assert.Throws<ArgumentException>(() => RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.ForeignBox",
            0x7400,
            genericArguments: ImmutableArray.Create(foreignPayload)));
        Assert.Throws<ArgumentException>(() => RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.NestedBox",
            0x7400,
            genericArguments: ImmutableArray.Create(genericPayload)));
        Assert.Throws<ArgumentException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.ConcreteReference,
            foreignPayload));

        var x86Module = new ClrmdRuntimeModuleIdentity(
            Snapshot,
            AppDomainAddress: 0x1000,
            ModuleAddress: 0x2200,
            ImageBase: 0x0080_0000,
            ImageSize: 0x1000);
        var x86Payload = CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload",
            0x9000);
        Assert.Throws<ArgumentException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.ConcreteReference,
            x86Payload));
    }

    /// <summary>Proves runtime mapping retains raw storage observations without performing Product admission.</summary>
    [Fact]
    public void Runtime_field_identity_retains_raw_storage_observations_and_canonical_swaps()
    {
        var owner = DeclaringType();
        var int32Type = Int32Type();
        var accepted = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var literal = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Public | FieldAttributes.Static |
                FieldAttributes.Literal | FieldAttributes.HasDefault);
        var nonliteralDefault = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasDefault);
        var rva = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasFieldRVA);
        var thread = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            runtimeReportsThreadStatic: true);
        var context = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            runtimeReportsContextStatic: true);
        var conflictingMarkers = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            runtimeReportsThreadStatic: true,
            runtimeReportsContextStatic: true);

        Assert.Equal(FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly, accepted.Attributes);
        Assert.False(accepted.RuntimeReportsThreadStatic);
        Assert.False(accepted.RuntimeReportsContextStatic);
        Assert.Equal(FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasDefault, nonliteralDefault.Attributes);
        Assert.True(literal.Attributes.HasFlag(FieldAttributes.Literal));
        Assert.True(rva.Attributes.HasFlag(FieldAttributes.HasFieldRVA));
        Assert.True(thread.RuntimeReportsThreadStatic);
        Assert.False(thread.RuntimeReportsContextStatic);
        Assert.False(context.RuntimeReportsThreadStatic);
        Assert.True(context.RuntimeReportsContextStatic);
        Assert.True(conflictingMarkers.RuntimeReportsThreadStatic);
        Assert.True(conflictingMarkers.RuntimeReportsContextStatic);
        AssertCanonicalDifference(accepted.CanonicalBytes, literal.CanonicalBytes);
        AssertCanonicalDifference(accepted.CanonicalBytes, nonliteralDefault.CanonicalBytes);
        AssertCanonicalDifference(accepted.CanonicalBytes, rva.CanonicalBytes);
        AssertCanonicalDifference(accepted.CanonicalBytes, thread.CanonicalBytes);
        AssertCanonicalDifference(accepted.CanonicalBytes, context.CanonicalBytes);
        AssertCanonicalDifference(thread.CanonicalBytes, context.CanonicalBytes);
        AssertCanonicalDifference(thread.CanonicalBytes, conflictingMarkers.CanonicalBytes);
        AssertCanonicalDifference(context.CanonicalBytes, conflictingMarkers.CanonicalBytes);

        Assert.Throws<ArgumentException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Public));
        Assert.Throws<ArgumentException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: (FieldAttributes)0x0007 | FieldAttributes.Static));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClrmdStaticRuntimeFieldIdentity.Create(
            owner,
            0x02000031,
            "Count",
            FieldAttributes.Public | FieldAttributes.Static,
            runtimeReportsThreadStatic: false,
            runtimeReportsContextStatic: false,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type));
        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeFieldIdentity.Create(
            owner,
            FieldToken,
            "Count\nAlias",
            FieldAttributes.Public | FieldAttributes.Static,
            runtimeReportsThreadStatic: false,
            runtimeReportsContextStatic: false,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type));
    }

    /// <summary>Proves exact mapping counters enforce all caps, unique retention, and four canonical bounds.</summary>
    [Fact]
    public void Runtime_mapping_counters_are_positive_unique_and_fixed_bounded()
    {
        var counters = ClrmdStaticRuntimeDeclarationMappingCounters.Create(
            ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeTypesExamined,
            ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined,
            declaringTypeMatchesRetained: 1,
            staticFieldMatchesRetained: 1,
            runtimeTypeSearchExhaustive: true,
            runtimeStaticFieldSearchExhaustive: true);

        Assert.Equal(4, counters.CanonicalBounds.Length);
        Assert.True(counters.RuntimeTypeSearchExhaustive);
        Assert.True(counters.RuntimeStaticFieldSearchExhaustive);
        Assert.Equal(
            new[]
            {
                (ClrmdStaticRuntimeDeclarationMappingCounters.DeclaringTypeMatchesRetainedBoundName, 2L),
                (ClrmdStaticRuntimeDeclarationMappingCounters.RuntimeStaticFieldsExaminedBoundName, 256L),
                (ClrmdStaticRuntimeDeclarationMappingCounters.RuntimeTypesExaminedBoundName, 4096L),
                (ClrmdStaticRuntimeDeclarationMappingCounters.StaticFieldMatchesRetainedBoundName, 2L),
            },
            counters.CanonicalBounds.Select(bound => (bound.Name, bound.Value)).ToArray());

        Assert.Throws<ArgumentOutOfRangeException>(() => Counters(runtimeTypesExamined: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Counters(runtimeTypesExamined: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Counters(
            runtimeTypesExamined:
                ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeTypesExamined + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Counters(runtimeStaticFieldsExamined: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Counters(
            runtimeStaticFieldsExamined:
                ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined + 1));
        Assert.Throws<ArgumentException>(() => Counters(declaringTypeMatchesRetained: 0));
        Assert.Throws<ArgumentException>(() => Counters(declaringTypeMatchesRetained: 2));
        Assert.Throws<ArgumentException>(() => Counters(staticFieldMatchesRetained: 0));
        Assert.Throws<ArgumentException>(() => Counters(staticFieldMatchesRetained: 2));
        Assert.Throws<ArgumentException>(() => Counters(runtimeTypeSearchExhaustive: false));
        Assert.Throws<ArgumentException>(() => Counters(runtimeStaticFieldSearchExhaustive: false));
    }

    /// <summary>Proves owner, module, content, field, type, and counter substitutions cannot alias an exact mapping.</summary>
    [Fact]
    public void Runtime_mapping_correlates_owner_and_canonicalizes_every_swappable_fact()
    {
        var owner = DeclaringType();
        var int32Type = Int32Type();
        var field = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, int32Type);
        var counters = Counters();
        var mapping = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(owner, field, counters);

        var foreignModuleSameSnapshot = Module with { ModuleAddress = 0x2400 };
        var moduleSwappedOwner = RuntimeType(
            foreignModuleSameSnapshot,
            ModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000);
        var contentSwappedOwner = RuntimeType(
            Module,
            DifferentModuleContent,
            DeclaringTypeToken,
            "Incident.State",
            0x7000);
        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
            owner,
            Field(moduleSwappedOwner, ClrmdStaticExpectedDecoderKind.Int32, int32Type),
            counters));
        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
            owner,
            Field(contentSwappedOwner, ClrmdStaticExpectedDecoderKind.Int32, int32Type),
            counters));

        var tokenSwap = Mapping(owner, Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            fieldToken: FieldToken + 1));
        var nameSwap = Mapping(owner, Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            name: "CountAlias"));
        var attributeSwap = Mapping(owner, Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            int32Type,
            attributes: FieldAttributes.Private | FieldAttributes.Static));
        var typeSwap = Mapping(owner, Field(
            owner,
            ClrmdStaticExpectedDecoderKind.ConcreteReference,
            PayloadType()));
        var counterSwap = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
            owner,
            field,
            Counters(runtimeTypesExamined: counters.RuntimeTypesExamined + 1));

        AssertCanonicalDifference(mapping.CanonicalBytes, tokenSwap.CanonicalBytes);
        AssertCanonicalDifference(mapping.CanonicalBytes, nameSwap.CanonicalBytes);
        AssertCanonicalDifference(mapping.CanonicalBytes, attributeSwap.CanonicalBytes);
        AssertCanonicalDifference(mapping.CanonicalBytes, typeSwap.CanonicalBytes);
        AssertCanonicalDifference(mapping.CanonicalBytes, counterSwap.CanonicalBytes);
        Assert.NotEqual(mapping.Sha256, tokenSwap.Sha256);
        Assert.Equal(owner, mapping.DeclaringType);
        Assert.Equal(field, mapping.Field);
        Assert.Equal(counters, mapping.Counters);
    }

    /// <summary>Proves equal synthetic mappings replay byte-for-byte while returned arrays remain defensive.</summary>
    [Fact]
    public void Runtime_mapping_has_defensive_canonical_content_equality()
    {
        var firstOwner = DeclaringType();
        var firstField = Field(
            firstOwner,
            ClrmdStaticExpectedDecoderKind.ConcreteReference,
            PayloadType());
        var first = Mapping(firstOwner, firstField);
        var secondOwner = DeclaringType();
        var secondField = Field(
            secondOwner,
            ClrmdStaticExpectedDecoderKind.ConcreteReference,
            PayloadType());
        var second = Mapping(secondOwner, secondField);

        Assert.Equal(firstOwner, secondOwner);
        Assert.Equal(firstField, secondField);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));

        var originalBytes = first.CanonicalBytes.ToArray();
        var exposedBytes = first.CanonicalBytes;
        var exposedArray = ImmutableCollectionsMarshal.AsArray(exposedBytes)!;
        exposedArray[0] ^= 0xFF;
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(originalBytes));
        Assert.Equal(second, first);

        var originalBounds = first.Counters.CanonicalBounds;
        var exposedBounds = first.Counters.CanonicalBounds;
        var exposedBoundArray = ImmutableCollectionsMarshal.AsArray(exposedBounds)!;
        exposedBoundArray[0] = new EvaluationDeterministicBound("poison.bound", 1);
        Assert.True(originalBounds.AsSpan().SequenceEqual(first.Counters.CanonicalBounds.AsSpan()));

        var nullableType = NullableType();
        var originalArgument = nullableType.GenericArguments[0];
        var exposedArguments = nullableType.GenericArguments;
        var exposedArgumentArray = ImmutableCollectionsMarshal.AsArray(exposedArguments)!;
        exposedArgumentArray[0] = StringType();
        Assert.Equal(originalArgument, nullableType.GenericArguments[0]);
    }

    private static ClrmdStaticRuntimeTypeIdentity DeclaringType() =>
        RuntimeType(Module, ModuleContent, DeclaringTypeToken, "Incident.State", 0x7000);

    private static ClrmdStaticRuntimeTypeIdentity PayloadType() =>
        RuntimeType(Module, ModuleContent, PayloadTypeToken, "Incident.Payload", 0x7100);

    private static ClrmdStaticRuntimeTypeIdentity Int32Type() =>
        RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            Int32TypeToken,
            "System.Int32",
            0x71F0,
            isValueType: true,
            isPrimitive: true);

    private static ClrmdStaticRuntimeTypeIdentity StringType() =>
        RuntimeType(CoreLibraryModule, CoreLibraryContent, StringTypeToken, "System.String", 0x7200);

    private static ClrmdStaticRuntimeTypeIdentity NullableType() =>
        RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            NullableTypeToken,
            "System.Nullable<System.Int32>",
            0x7300,
            isValueType: true,
            genericArguments: ImmutableArray.Create(Int32Type()));

    private static ClrmdStaticRuntimeTypeIdentity RuntimeType(
        ClrmdRuntimeModuleIdentity module,
        ModuleContentIdentity content,
        int typeToken,
        string fullName,
        ulong? methodTable,
        ClrmdSnapshotIdentity? snapshot = null,
        bool isValueType = false,
        bool isPrimitive = false,
        bool isArray = false,
        bool isInterface = false,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity>? genericArguments = null) =>
        CreateRuntimeType(
            snapshot ?? Snapshot,
            sizeof(ulong),
            module,
            content,
            typeToken,
            fullName,
            methodTable,
            isValueType,
            isPrimitive,
            isArray,
            isInterface,
            genericArguments);

    private static ClrmdStaticRuntimeTypeIdentity CreateRuntimeType(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ClrmdRuntimeModuleIdentity module,
        ModuleContentIdentity content,
        int typeToken,
        string fullName,
        ulong? methodTable,
        bool isValueType = false,
        bool isPrimitive = false,
        bool isArray = false,
        bool isInterface = false,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity>? genericArguments = null) =>
        ClrmdStaticRuntimeTypeIdentity.Create(
            snapshot,
            pointerWidth,
            module,
            content,
            typeToken,
            fullName,
            methodTable,
            isValueType,
            isPrimitive,
            isArray,
            isInterface,
            genericArguments ?? ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);

    private static ClrmdStaticRuntimeFieldIdentity Field(
        ClrmdStaticRuntimeTypeIdentity owner,
        ClrmdStaticExpectedDecoderKind expectedDecoderKind,
        ClrmdStaticRuntimeTypeIdentity observedType,
        int fieldToken = FieldToken,
        string name = "Count",
        FieldAttributes attributes = FieldAttributes.Public | FieldAttributes.Static,
        bool runtimeReportsThreadStatic = false,
        bool runtimeReportsContextStatic = false) =>
        ClrmdStaticRuntimeFieldIdentity.Create(
            owner,
            fieldToken,
            name,
            attributes,
            runtimeReportsThreadStatic,
            runtimeReportsContextStatic,
            expectedDecoderKind,
            observedType);

    private static ClrmdStaticRuntimeDeclarationMappingCounters Counters(
        int runtimeTypesExamined = 17,
        int runtimeStaticFieldsExamined = 9,
        int declaringTypeMatchesRetained = 1,
        int staticFieldMatchesRetained = 1,
        bool runtimeTypeSearchExhaustive = true,
        bool runtimeStaticFieldSearchExhaustive = true) =>
        ClrmdStaticRuntimeDeclarationMappingCounters.Create(
            runtimeTypesExamined,
            runtimeStaticFieldsExamined,
            declaringTypeMatchesRetained,
            staticFieldMatchesRetained,
            runtimeTypeSearchExhaustive,
            runtimeStaticFieldSearchExhaustive);

    private static ClrmdStaticRuntimeDeclarationMappingIdentity Mapping(
        ClrmdStaticRuntimeTypeIdentity owner,
        ClrmdStaticRuntimeFieldIdentity field) =>
        ClrmdStaticRuntimeDeclarationMappingIdentity.Create(owner, field, Counters());

    private static void AssertCanonicalDifference(
        ImmutableArray<byte> left,
        ImmutableArray<byte> right) =>
        Assert.False(left.AsSpan().SequenceEqual(right.AsSpan()));
}
