using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Validates detached, bounded runtime declaration mapping with complex synthetic evidence.</summary>
[Trait("Category", "Fast")]
public sealed class W7StaticRuntimeDeclarationMappingContractTests
{
    private const int DeclaringTypeToken = 0x02000011;
    private const int PayloadTypeToken = 0x02000012;
    private const int GenericPayloadTypeToken = 0x02000013;
    private const int PairTypeToken = 0x02000014;
    private const int NestedTypeToken = 0x02000015;
    private const int TooManyArgumentsTypeToken = 0x02000016;
    private const int Int32TypeToken = 0x02000020;
    private const int StringTypeToken = 0x02000021;
    private const int NullableTypeToken = 0x02000022;
    private const int ArrayTypeToken = 0x02000023;
    private const int ListInterfaceTypeToken = 0x02000024;
    private const int ReadOnlyListInterfaceTypeToken = 0x02000025;
    private const int CloneableInterfaceTypeToken = 0x02000026;
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
        Assert.Equal(ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition, exact.Kind);
        Assert.Equal(
            "ff3d87a85f475fb1554fbc804f0d4fe04f46785d069b239138c08ffd26890886",
            exact.Sha256);
        Assert.Equal(Module, exact.RuntimeModule);
        Assert.Equal(ModuleContent, exact.ModuleContent);
        Assert.Equal(DeclaringTypeToken, exact.TypeDefinitionToken);
        Assert.Equal("Incident.State", exact.FullName);
        Assert.False(exact.IsValueType);
        Assert.False(exact.IsPrimitive);
        Assert.False(exact.IsArray);
        Assert.False(exact.IsInterface);
        Assert.Null(exact.ArrayRank);
        Assert.Null(exact.IsSzArray);
        Assert.Null(exact.ComponentType);
        Assert.Null(exact.BaseType);
        Assert.Empty(exact.InterfaceTypes);
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
        var arrayPayload = RuntimeArray(
            PayloadType(),
            "Incident.Payload[]",
            0x7500);
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
            GenericPayloadTypeToken,
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
        var payload = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, payloadType);
        var expectedInt32ButPayload = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, payloadType);
        var expectedStringButPayload = Field(owner, ClrmdStaticExpectedDecoderKind.String, payloadType);
        var nullableLookalike = Field(
            owner,
            ClrmdStaticExpectedDecoderKind.NullableInt32,
            mislabeledNullableOfString);
        var nameOnlyInt32Field = Field(owner, ClrmdStaticExpectedDecoderKind.Int32, nameOnlyInt32);
        var structAsManagedReference = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, structPayload);
        var primitiveAsManagedReference = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, primitivePayload);
        var arrayAsManagedReference = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, arrayPayload);
        var interfaceAsManagedReference = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, interfacePayload);
        var genericAsManagedReference = Field(owner, ClrmdStaticExpectedDecoderKind.ManagedReference, genericPayload);

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
        Assert.Same(structPayload, structAsManagedReference.ObservedFieldType);
        Assert.Same(primitivePayload, primitiveAsManagedReference.ObservedFieldType);
        Assert.Same(arrayPayload, arrayAsManagedReference.ObservedFieldType);
        Assert.Same(interfacePayload, interfaceAsManagedReference.ObservedFieldType);
        Assert.Same(genericPayload, genericAsManagedReference.ObservedFieldType);
        AssertCanonicalDifference(int32.CanonicalBytes, expectedInt32ButPayload.CanonicalBytes);
        AssertCanonicalDifference(payload.CanonicalBytes, structAsManagedReference.CanonicalBytes);
        Assert.Single(nullableType.GenericArguments);
        Assert.Equal(int32Type, nullableType.GenericArguments[0]);
        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.GenericArgumentCountBoundName,
                64),
            ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound);

        Assert.Throws<ArgumentNullException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.Int32,
            observedType: null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Field(
            owner,
            (ClrmdStaticExpectedDecoderKind)99,
            payloadType));
        var pair = RuntimeType(
            Module,
            ModuleContent,
            PairTypeToken,
            "Incident.Pair<System.Int32,System.String>",
            0x7400,
            genericArguments: ImmutableArray.Create(int32Type, stringType));
        var nestedGeneric = RuntimeType(
            Module,
            ModuleContent,
            NestedTypeToken,
            "Incident.NestedBox<Incident.Pair<System.Int32,System.String>,Incident.Payload<System.Int32>>",
            0x7410,
            genericArguments: ImmutableArray.Create(pair, genericPayload));
        Assert.Equal(new[] { int32Type, stringType }, pair.GenericArguments);
        Assert.Equal(new[] { pair, genericPayload }, nestedGeneric.GenericArguments);
        Assert.Equal(int32Type, nestedGeneric.GenericArguments[0].GenericArguments[0]);
        Assert.Throws<ArgumentException>(() => RuntimeType(
            Module,
            ModuleContent,
            TooManyArgumentsTypeToken,
            "Incident.TooManyArguments",
            0x7420,
            genericArguments: Enumerable.Repeat(
                int32Type,
                ClrmdStaticRuntimeTypeIdentity.MaximumGenericArgumentCount + 1).ToImmutableArray()));

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
        Assert.Throws<ArgumentException>(() => Field(
            owner,
            ClrmdStaticExpectedDecoderKind.ManagedReference,
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
            ClrmdStaticExpectedDecoderKind.ManagedReference,
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
            ClrmdStaticExpectedDecoderKind.ManagedReference,
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
            ClrmdStaticExpectedDecoderKind.ManagedReference,
            PayloadType());
        var first = Mapping(firstOwner, firstField);
        var secondOwner = DeclaringType();
        var secondField = Field(
            secondOwner,
            ClrmdStaticExpectedDecoderKind.ManagedReference,
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

    /// <summary>
    /// Proves a TypeDef-less jagged array freezes exact recursive topology and a canonical interface set.
    /// </summary>
    [Fact]
    public void Runtime_array_identity_freezes_typedefless_jagged_topology_and_canonical_set()
    {
        var systemArray = SystemArrayType();
        var listOfInt32 = RuntimeInterface(
            ListInterfaceTypeToken,
            "System.Collections.Generic.IList<System.Int32>",
            ImmutableArray.Create(Int32Type()));
        var innerArray = RuntimeArray(
            Int32Type(),
            "System.Int32[]",
            0x7600,
            systemArray,
            ImmutableArray.Create(listOfInt32));
        var listOfInnerArray = RuntimeInterface(
            ListInterfaceTypeToken,
            "System.Collections.Generic.IList<System.Int32[]>",
            ImmutableArray.Create(innerArray));
        var readOnlyListOfInnerArray = RuntimeInterface(
            ReadOnlyListInterfaceTypeToken,
            "System.Collections.Generic.IReadOnlyList<System.Int32[]>",
            ImmutableArray.Create(innerArray));
        var cloneable = RuntimeInterface(CloneableInterfaceTypeToken, "System.ICloneable");
        var interfaces = ImmutableArray.Create(
            readOnlyListOfInnerArray,
            cloneable,
            listOfInnerArray);

        var outerArray = RuntimeArray(
            innerArray,
            "System.Int32[][]",
            0x7700,
            systemArray,
            interfaces);
        var replay = RuntimeArray(
            innerArray,
            "System.Int32[][]",
            0x7700,
            systemArray,
            ImmutableArray.Create(listOfInnerArray, readOnlyListOfInnerArray, cloneable));

        Assert.Equal(ClrmdStaticRuntimeTypeIdentityKind.Array, outerArray.Kind);
        Assert.True(outerArray.IsArray);
        Assert.False(outerArray.IsValueType);
        Assert.False(outerArray.IsPrimitive);
        Assert.False(outerArray.IsInterface);
        Assert.Null(outerArray.RuntimeModule);
        Assert.Null(outerArray.ModuleContent);
        Assert.Null(outerArray.TypeDefinitionToken);
        Assert.Equal((ulong)0x7700, outerArray.MethodTable);
        Assert.Equal(1, outerArray.ArrayRank);
        Assert.True(outerArray.IsSzArray);
        Assert.Same(innerArray, outerArray.ComponentType);
        Assert.Same(systemArray, outerArray.BaseType);
        Assert.Empty(outerArray.GenericArguments);
        Assert.Equal(3, outerArray.InterfaceTypes.Length);
        Assert.Contains(listOfInnerArray, outerArray.InterfaceTypes);
        Assert.Contains(readOnlyListOfInnerArray, outerArray.InterfaceTypes);
        Assert.Contains(cloneable, outerArray.InterfaceTypes);
        Assert.Equal(outerArray, replay);
        Assert.True(outerArray.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));

        var canonicalInterfaces = outerArray.InterfaceTypes;
        for (var index = 1; index < canonicalInterfaces.Length; index++)
        {
            Assert.True(
                canonicalInterfaces[index - 1].CanonicalBytes.AsSpan().SequenceCompareTo(
                    canonicalInterfaces[index].CanonicalBytes.AsSpan()) < 0);
        }

        var originalInterfaces = outerArray.InterfaceTypes;
        var exposedInterfaces = outerArray.InterfaceTypes;
        ImmutableCollectionsMarshal.AsArray(exposedInterfaces)![0] = RuntimeInterface(
            0x02000027,
            "Synthetic.IPoison");
        Assert.True(originalInterfaces.AsSpan().SequenceEqual(outerArray.InterfaceTypes.AsSpan()));

        var structuralCoreLibraryLookalike = RuntimeType(
            Module,
            DifferentModuleContent,
            ArrayTypeToken,
            "System.Array",
            0x78F0);
        var lookalikeArray = RuntimeArray(
            innerArray,
            "System.Int32[][]",
            0x7700,
            structuralCoreLibraryLookalike,
            interfaces);
        Assert.Same(structuralCoreLibraryLookalike, lookalikeArray.BaseType);
        AssertCanonicalDifference(outerArray.CanonicalBytes, lookalikeArray.CanonicalBytes);

        AssertCanonicalDifference(
            outerArray.CanonicalBytes,
            RuntimeArray(innerArray, "System.Int32[][]", 0x7708, systemArray, interfaces).CanonicalBytes);
        AssertCanonicalDifference(
            outerArray.CanonicalBytes,
            RuntimeArray(innerArray, "System.Int32[][]Alias", 0x7700, systemArray, interfaces).CanonicalBytes);
        AssertCanonicalDifference(
            outerArray.CanonicalBytes,
            RuntimeArray(
                innerArray,
                "System.Int32[][]",
                0x7700,
                systemArray,
                interfaces,
                rank: 1,
                isSzArray: false).CanonicalBytes);
        AssertCanonicalDifference(
            outerArray.CanonicalBytes,
            RuntimeArray(StringType(), "System.String[]", 0x7700, systemArray, interfaces).CanonicalBytes);
        AssertCanonicalDifference(
            outerArray.CanonicalBytes,
            RuntimeArray(
                innerArray,
                "System.Int32[][]",
                0x7700,
                systemArray,
                ImmutableArray.Create(listOfInnerArray)).CanonicalBytes);

        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.RuntimeTypeGraphDepthBoundName,
                8),
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphDepthBound);
        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.RuntimeTypeGraphNodeCountBoundName,
                256),
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphNodeCountBound);
        Assert.Equal(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeTypeIdentity.RuntimeInterfaceTypeCountBoundName,
                64),
            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeInterfaceTypeCountBound);
        Assert.Equal(
            new EvaluationDeterministicBound(ClrmdStaticRuntimeTypeIdentity.ArrayRankBoundName, 32),
            ClrmdStaticRuntimeTypeIdentity.DeclaredArrayRankBound);
    }

    /// <summary>Proves array construction rejects malformed coordinates, variant lies, and topology poison.</summary>
    [Fact]
    public void Runtime_array_identity_rejects_malformed_physical_and_variant_facts()
    {
        var systemArray = SystemArrayType();
        var component = Int32Type();
        var interfaceType = RuntimeInterface(CloneableInterfaceTypeToken, "System.ICloneable");
        var array = RuntimeArray(component, "System.Int32[]", 0x7600, systemArray);

        Assert.Throws<ArgumentException>(() => RuntimeType(
            Module,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload[]",
            0x7500,
            isArray: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(ulong),
            "System.Int32[]",
            0,
            1,
            true,
            component,
            systemArray,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            rank: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            component,
            "System.Int32[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]",
            0x7600,
            systemArray,
            rank: ClrmdStaticRuntimeTypeIdentity.MaximumArrayRank + 1,
            isSzArray: false));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[,]",
            0x7600,
            systemArray,
            rank: 2,
            isSzArray: true));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32\0[]",
            0x7600,
            systemArray));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            component,
            new string('A', ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters + 1),
            0x7600,
            systemArray));

        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(ulong),
            "System.Int32[]",
            0x7600,
            1,
            true,
            component,
            systemArray,
            default));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            ImmutableArray.Create(interfaceType, RuntimeInterface(CloneableInterfaceTypeToken, "System.ICloneable"))));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            ImmutableArray.Create(PayloadType())));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            ImmutableArray.Create(component)));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            PayloadType()));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            array));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            RuntimeType(
                CoreLibraryModule,
                CoreLibraryContent,
                ArrayTypeToken,
                "System.Array",
                0x78F0,
                isInterface: true)));

        var foreignComponent = RuntimeType(
            ForeignModule,
            ModuleContent,
            PayloadTypeToken,
            "Incident.Payload",
            0x9000,
            snapshot: ForeignSnapshot);
        var foreignInterface = CreateRuntimeType(
            ForeignSnapshot,
            sizeof(ulong),
            ForeignModule,
            ModuleContent,
            CloneableInterfaceTypeToken,
            "System.ICloneable",
            0x9100,
            isInterface: true);
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            foreignComponent,
            "Incident.Payload[]",
            0x7600,
            systemArray));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            ImmutableArray.Create(foreignInterface)));
        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeTypeIdentity.CreateArray(
            ForeignSnapshot,
            sizeof(ulong),
            "System.Int32[]",
            0x7600,
            1,
            true,
            component,
            systemArray,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty));

        var x86Module = new ClrmdRuntimeModuleIdentity(
            Snapshot,
            AppDomainAddress: 0x1000,
            ModuleAddress: 0x3200,
            ImageBase: 0x0100_0000,
            ImageSize: 0x4000);
        var x86Component = CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            CoreLibraryContent,
            Int32TypeToken,
            "System.Int32",
            0x7200,
            isValueType: true,
            isPrimitive: true);
        var x86SystemArray = CreateRuntimeType(
            Snapshot,
            sizeof(uint),
            x86Module,
            CoreLibraryContent,
            ArrayTypeToken,
            "System.Array",
            0x7300);
        var maximumX86Array = ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(uint),
            "System.Int32[]",
            uint.MaxValue,
            1,
            true,
            x86Component,
            x86SystemArray,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);
        Assert.Equal((ulong)uint.MaxValue, maximumX86Array.MethodTable);
        Assert.Throws<ArgumentOutOfRangeException>(() => ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(uint),
            "System.Int32[]",
            0x1_0000_0000,
            1,
            true,
            x86Component,
            x86SystemArray,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty));
        Assert.Throws<ArgumentException>(() => ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(uint),
            "System.Int32[]",
            0x7600,
            1,
            true,
            component,
            x86SystemArray,
            ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty));

        var tooManyInterfaces = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(
            ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeInterfaceTypeCount + 1);
        for (var index = 0; index <= ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeInterfaceTypeCount; index++)
        {
            tooManyInterfaces.Add(RuntimeInterface(
                0x02000100 + index,
                $"Synthetic.IArrayContract{index:D2}"));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            component,
            "System.Int32[]",
            0x7600,
            systemArray,
            tooManyInterfaces.MoveToImmutable()));
        Assert.Throws<ArgumentException>(() => Field(
            array,
            ClrmdStaticExpectedDecoderKind.Int32,
            component));
    }

    /// <summary>Proves recursive array graphs enforce their depth/node caps and reject reflected cycles.</summary>
    [Fact]
    public void Runtime_array_identity_enforces_recursive_graph_bounds_and_cycles()
    {
        var systemArray = SystemArrayType();
        ClrmdStaticRuntimeTypeIdentity deepestAccepted = Int32Type();
        for (var depth = 1; depth < ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeTypeGraphDepth; depth++)
        {
            deepestAccepted = RuntimeArray(
                deepestAccepted,
                $"Synthetic.Depth{depth}[]",
                (ulong)(0x8000 + depth * 0x10),
                systemArray);
        }

        Assert.Equal(ClrmdStaticRuntimeTypeIdentityKind.Array, deepestAccepted.Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            deepestAccepted,
            "Synthetic.TooDeep[]",
            0x9000,
            systemArray));

        var branchInterfaces = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(64);
        for (var index = 0; index < 64; index++)
        {
            branchInterfaces.Add(RuntimeInterface(
                0x02000200 + index,
                $"Synthetic.IBranch{index:D2}"));
        }

        var branchArray = RuntimeArray(
            Int32Type(),
            "Synthetic.Branch[]",
            0xA000,
            systemArray,
            branchInterfaces.MoveToImmutable());
        var rootInterfaces = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(4);
        for (var index = 0; index < 4; index++)
        {
            rootInterfaces.Add(RuntimeInterface(
                0x02000300 + index,
                $"Synthetic.IRoot{index}<Synthetic.Branch[]>",
                ImmutableArray.Create(branchArray)));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeArray(
            Int32Type(),
            "Synthetic.NodeOverflow[]",
            0xB000,
            systemArray,
            rootInterfaces.MoveToImmutable()));

        var reflectedCycle = RuntimeArray(Int32Type(), "Synthetic.Cycle[]", 0xC000, systemArray);
        var componentField = typeof(ClrmdStaticRuntimeTypeIdentity).GetField(
            "componentType",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentField);
        componentField.SetValue(reflectedCycle, reflectedCycle);
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            reflectedCycle,
            "Synthetic.Outer[]",
            0xC100,
            systemArray));

        var typeDefinitionWithDefaultVariantSet = Int32Type();
        var interfaceTypesField = typeof(ClrmdStaticRuntimeTypeIdentity).GetField(
            "interfaceTypes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(interfaceTypesField);
        interfaceTypesField.SetValue(
            typeDefinitionWithDefaultVariantSet,
            default(ImmutableArray<ClrmdStaticRuntimeTypeIdentity>));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            typeDefinitionWithDefaultVariantSet,
            "Synthetic.DefaultTypeDefVariant[]",
            0xC200,
            systemArray));

        var arrayWithDefaultVariantSet = RuntimeArray(
            Int32Type(),
            "Synthetic.DefaultArrayVariant[]",
            0xC300,
            systemArray);
        var genericArgumentsField = typeof(ClrmdStaticRuntimeTypeIdentity).GetField(
            "genericArguments",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(genericArgumentsField);
        genericArgumentsField.SetValue(
            arrayWithDefaultVariantSet,
            default(ImmutableArray<ClrmdStaticRuntimeTypeIdentity>));
        Assert.Throws<ArgumentException>(() => RuntimeArray(
            arrayWithDefaultVariantSet,
            "Synthetic.DefaultArrayVariantOuter[]",
            0xC400,
            systemArray));
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

    private static ClrmdStaticRuntimeTypeIdentity SystemArrayType() =>
        RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            ArrayTypeToken,
            "System.Array",
            0x7500);

    private static ClrmdStaticRuntimeTypeIdentity RuntimeInterface(
        int typeToken,
        string fullName,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity>? genericArguments = null) =>
        RuntimeType(
            CoreLibraryModule,
            CoreLibraryContent,
            typeToken,
            fullName,
            methodTable: (ulong)(0xD000 + (typeToken & 0x0FFF) * 0x10),
            isInterface: true,
            genericArguments: genericArguments);

    private static ClrmdStaticRuntimeTypeIdentity RuntimeArray(
        ClrmdStaticRuntimeTypeIdentity componentType,
        string fullName,
        ulong methodTable,
        ClrmdStaticRuntimeTypeIdentity baseType,
        ImmutableArray<ClrmdStaticRuntimeTypeIdentity>? interfaceTypes = null,
        int rank = 1,
        bool isSzArray = true) =>
        ClrmdStaticRuntimeTypeIdentity.CreateArray(
            Snapshot,
            sizeof(ulong),
            fullName,
            methodTable,
            rank,
            isSzArray,
            componentType,
            baseType,
            interfaceTypes ?? ImmutableArray<ClrmdStaticRuntimeTypeIdentity>.Empty);

    private static ClrmdStaticRuntimeTypeIdentity RuntimeArray(
        ClrmdStaticRuntimeTypeIdentity componentType,
        string fullName,
        ulong methodTable) =>
        RuntimeArray(componentType, fullName, methodTable, SystemArrayType());

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
