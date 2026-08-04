using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the frozen static-field storage discriminator and exact metadata literal projection.</summary>
public sealed class W8V2StorageStrategyTests
{
    private const int PublicClass = 0x0000_0001;

    private const int FieldPublic = (int)FieldAttributes.Public;
    private const int FieldStatic = (int)FieldAttributes.Static;
    private const int FieldLiteral = (int)(FieldAttributes.Literal | FieldAttributes.HasDefault);
    private const int FieldRva = (int)FieldAttributes.HasFieldRVA;
    private const int FieldEnumValue =
        (int)(FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName);

    private const int StaticLiteral = FieldPublic | FieldStatic | FieldLiteral;

    private const int ColorsToken = 0x0200_0002;
    private const int MoneyToken = 0x0200_0003;
    private const int HolderToken = 0x0200_0004;

    private const int ConstantBoolean = 0x02;
    private const int ConstantChar = 0x03;
    private const int ConstantInt8 = 0x04;
    private const int ConstantUInt8 = 0x05;
    private const int ConstantInt16 = 0x06;
    private const int ConstantUInt16 = 0x07;
    private const int ConstantInt32 = 0x08;
    private const int ConstantUInt32 = 0x09;
    private const int ConstantInt64 = 0x0A;
    private const int ConstantUInt64 = 0x0B;
    private const int ConstantSingle = 0x0C;
    private const int ConstantDouble = 0x0D;
    private const int ConstantString = 0x0E;
    private const int ConstantNativeInt = 0x18;
    private const int ConstantClass = 0x12;

    /// <summary>
    /// Proves each of the four admitted strategies is classified from real physical FieldAttributes plus the
    /// caller-supplied thread marker alone, and that a literal always wins over a field RVA.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_admitted_strategy_is_classified_from_physical_flags()
    {
        var world = BuildWorld();

        var constructed = Classify(world, "PlainStatic");
        Assert.Equal(StaticFieldV2StorageStrategyResultKind.Exact, constructed.ResultKind);
        Assert.Equal(StaticFieldV2StorageStrategyIssue.None, constructed.Issue);
        Assert.Equal(StaticFieldV2StorageStrategy.ConstructedSlot, constructed.Strategy);
        Assert.Null(constructed.DiagnosticCode);
        Assert.Equal(Row(world, "PlainStatic").FieldDefinitionToken, constructed.FieldDefinitionToken);

        var threadRelative = Classify(world, "PlainStatic", threadStatic: true);
        Assert.Equal(StaticFieldV2StorageStrategy.ThreadRelativeSlot, threadRelative.Strategy);
        Assert.Contains(
            StaticFieldV2StorageCoverageBoundary.ThreadStaticAttributeSuppliedByCaller,
            threadRelative.DeclaredCoverageBoundaries);

        var moduleRva = Classify(world, "RvaBlob");
        Assert.Equal(StaticFieldV2StorageStrategy.ModuleRva, moduleRva.Strategy);
        Assert.True(Row(world, "RvaBlob").HasFieldRva);

        var literal = Classify(world, "LiteralAnswer");
        Assert.Equal(StaticFieldV2StorageStrategy.MetadataLiteral, literal.Strategy);
        Assert.True(Row(world, "LiteralAnswer").IsLiteral);
        Assert.True(Row(world, "LiteralAnswer").HasDefault);

        var literalWithRva = Classify(world, "LiteralAndRva");
        Assert.Equal(StaticFieldV2StorageStrategy.MetadataLiteral, literalWithRva.Strategy);

        foreach (var outcome in new[] { constructed, threadRelative, moduleRva, literal })
        {
            Assert.Contains(
                StaticFieldV2StorageCoverageBoundary.CustomAttributeTableNotModeled,
                outcome.DeclaredCoverageBoundaries);
        }
    }

    /// <summary>
    /// Proves the frozen per-capability requirement vector of every admitted strategy and of the two typed
    /// non-admissions, which the later runtime slice must obey exactly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Capability_requirement_vectors_are_frozen_per_strategy()
    {
        var world = BuildWorld();

        AssertVector(
            Classify(world, "LiteralAnswer"),
            runtimeConstruction: StaticFieldV2CapabilityRequirement.NotRequired,
            threadIdentity: StaticFieldV2CapabilityRequirement.NotRequired,
            moduleContent: StaticFieldV2CapabilityRequirement.Required,
            staticSlotAcquisition: StaticFieldV2CapabilityRequirement.NotRequired,
            memoryRead: StaticFieldV2CapabilityRequirement.NotRequired);
        AssertVector(
            Classify(world, "RvaBlob"),
            runtimeConstruction: StaticFieldV2CapabilityRequirement.NotRequired,
            threadIdentity: StaticFieldV2CapabilityRequirement.NotRequired,
            moduleContent: StaticFieldV2CapabilityRequirement.Required,
            staticSlotAcquisition: StaticFieldV2CapabilityRequirement.NotRequired,
            memoryRead: StaticFieldV2CapabilityRequirement.Required);
        AssertVector(
            Classify(world, "PlainStatic"),
            runtimeConstruction: StaticFieldV2CapabilityRequirement.Required,
            threadIdentity: StaticFieldV2CapabilityRequirement.NotRequired,
            moduleContent: StaticFieldV2CapabilityRequirement.NotRequired,
            staticSlotAcquisition: StaticFieldV2CapabilityRequirement.Required,
            memoryRead: StaticFieldV2CapabilityRequirement.Required);
        AssertVector(
            Classify(world, "PlainStatic", threadStatic: true),
            runtimeConstruction: StaticFieldV2CapabilityRequirement.Required,
            threadIdentity: StaticFieldV2CapabilityRequirement.Required,
            moduleContent: StaticFieldV2CapabilityRequirement.NotRequired,
            staticSlotAcquisition: StaticFieldV2CapabilityRequirement.Required,
            memoryRead: StaticFieldV2CapabilityRequirement.Required);

        Assert.Equal(
            [
                StaticFieldV2StorageCapability.RuntimeConstruction,
                StaticFieldV2StorageCapability.ThreadIdentity,
                StaticFieldV2StorageCapability.StaticSlotAcquisition,
                StaticFieldV2StorageCapability.MemoryRead,
            ],
            Classify(world, "PlainStatic", threadStatic: true).CapabilityRequirements.RequiredCapabilities.ToArray());
        Assert.Equal(
            [StaticFieldV2StorageCapability.ModuleContent],
            Classify(world, "LiteralAnswer").CapabilityRequirements.RequiredCapabilities.ToArray());

        foreach (var stop in new[]
                 {
                     Classify(world, "InstanceField"),
                     Classify(world, "PlainStatic", contextStatic: true),
                 })
        {
            AssertVector(
                stop,
                runtimeConstruction: StaticFieldV2CapabilityRequirement.NotRequired,
                threadIdentity: StaticFieldV2CapabilityRequirement.NotRequired,
                moduleContent: StaticFieldV2CapabilityRequirement.NotRequired,
                staticSlotAcquisition: StaticFieldV2CapabilityRequirement.NotRequired,
                memoryRead: StaticFieldV2CapabilityRequirement.NotRequired);
            Assert.Empty(stop.CapabilityRequirements.RequiredCapabilities);
        }
    }

    /// <summary>Proves an instance declaration is a typed non-admission that names no storage strategy.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Instance_declaration_is_a_typed_non_admission()
    {
        var world = BuildWorld();

        var outcome = Classify(world, "InstanceField");
        Assert.Equal(StaticFieldV2StorageStrategyResultKind.Unsupported, outcome.ResultKind);
        Assert.Equal(StaticFieldV2StorageStrategyIssue.InstanceFieldNotStatic, outcome.Issue);
        Assert.Null(outcome.Strategy);
        Assert.Equal("W8_STATIC_FIELD_INSTANCE_DECLARATION", outcome.DiagnosticCode);
        Assert.Equal(
            StaticFieldV2StorageStrategyOutcome.InstanceFieldNotStaticCode,
            outcome.DiagnosticCode);
    }

    /// <summary>
    /// Proves a caller-supplied context-relative marker produces the frozen W8.1 non-admission and that no
    /// context-relative storage value exists anywhere in the public surface.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Context_relative_storage_is_never_admitted()
    {
        var world = BuildWorld();

        var outcome = Classify(world, "PlainStatic", contextStatic: true);
        Assert.Equal(StaticFieldV2StorageStrategyResultKind.Unsupported, outcome.ResultKind);
        Assert.Equal(StaticFieldV2StorageStrategyIssue.ContextRelativeStorageNotAdmitted, outcome.Issue);
        Assert.Null(outcome.Strategy);
        Assert.Equal("W8_CONTEXT_IDENTITY_NOT_ATTRIBUTABLE", outcome.DiagnosticCode);
        Assert.Equal(
            StaticFieldV2StorageStrategyOutcome.ContextIdentityNotAttributableCode,
            outcome.DiagnosticCode);
        Assert.Contains(
            StaticFieldV2StorageCoverageBoundary.ContextStaticAttributeSuppliedByCaller,
            outcome.DeclaredCoverageBoundaries);

        // A context marker refuses even a literal declaration: the identity, not the storage shape, is unattributable.
        Assert.Equal(
            StaticFieldV2StorageStrategyIssue.ContextRelativeStorageNotAdmitted,
            Classify(world, "LiteralAnswer", contextStatic: true).Issue);

        var strategyNames = Enum.GetNames<StaticFieldV2StorageStrategy>();
        Assert.Equal(4, strategyNames.Length);
        Assert.Equal(
            ["ConstructedSlot", "MetadataLiteral", "ModuleRva", "ThreadRelativeSlot"],
            strategyNames.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(
            strategyNames,
            static name => name.Contains("Context", StringComparison.Ordinal));
        Assert.Equal(
            [1, 2, 3, 4],
            Enum.GetValues<StaticFieldV2StorageStrategy>().Select(static value => (int)value).Order().ToArray());

        var assembly = typeof(StaticFieldV2StorageStrategyBinder).Assembly;
        foreach (var type in assembly.GetExportedTypes())
        {
            Assert.DoesNotContain("ContextRelativeSlot", type.FullName ?? string.Empty, StringComparison.Ordinal);
            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                Assert.NotEqual("ContextRelativeSlot", member.Name);
            }
        }
    }

    /// <summary>Proves every admitted fixed-width integer encoding decodes with exact width and signedness.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_fixed_width_integer_literal_encoding_decodes_exactly()
    {
        var world = BuildWorld();

        AssertSigned(Project(world, "LitI1", ConstantInt8, [0x80]), StaticFieldV2LiteralValueKind.Int8, sbyte.MinValue);
        AssertSigned(Project(world, "LitI1", ConstantInt8, [0xFF]), StaticFieldV2LiteralValueKind.Int8, -1);
        AssertUnsigned(Project(world, "LitU1", ConstantUInt8, [0xFF]), StaticFieldV2LiteralValueKind.UInt8, byte.MaxValue);
        AssertSigned(
            Project(world, "LitI2", ConstantInt16, [0x00, 0x80]),
            StaticFieldV2LiteralValueKind.Int16,
            short.MinValue);
        AssertUnsigned(
            Project(world, "LitU2", ConstantUInt16, [0xFF, 0xFF]),
            StaticFieldV2LiteralValueKind.UInt16,
            ushort.MaxValue);
        AssertSigned(
            Project(world, "LitI4", ConstantInt32, [0x00, 0x00, 0x00, 0x80]),
            StaticFieldV2LiteralValueKind.Int32,
            int.MinValue);
        AssertSigned(
            Project(world, "LitI4", ConstantInt32, [0x2A, 0x00, 0x00, 0x00]),
            StaticFieldV2LiteralValueKind.Int32,
            42);
        AssertUnsigned(
            Project(world, "LitU4", ConstantUInt32, [0xFF, 0xFF, 0xFF, 0xFF]),
            StaticFieldV2LiteralValueKind.UInt32,
            uint.MaxValue);
        AssertSigned(
            Project(world, "LitI8", ConstantInt64, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80]),
            StaticFieldV2LiteralValueKind.Int64,
            long.MinValue);
        AssertUnsigned(
            Project(world, "LitU8", ConstantUInt64, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]),
            StaticFieldV2LiteralValueKind.UInt64,
            ulong.MaxValue);

        var exact = Project(world, "LitI4", ConstantInt32, [0x2A, 0x00, 0x00, 0x00]);
        Assert.Null(exact.UnsignedValue);
        Assert.Null(exact.FloatingBitPattern);
        Assert.Null(exact.StringValue);
        Assert.Null(exact.EnumDefinition);
        Assert.False(exact.IsEnumUnderlyingValue);
        Assert.Equal(4, exact.ObservedCount);
    }

    /// <summary>Proves char, boolean, and both floating encodings decode from their exact bit patterns.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Char_boolean_and_floating_literal_encodings_decode_from_exact_bits()
    {
        var world = BuildWorld();

        AssertUnsigned(
            Project(world, "LitChar", ConstantChar, [0x2C, 0x67]),
            StaticFieldV2LiteralValueKind.Char,
            0x672C);
        AssertUnsigned(Project(world, "LitBool", ConstantBoolean, [0x01]), StaticFieldV2LiteralValueKind.Boolean, 1);
        AssertUnsigned(Project(world, "LitBool", ConstantBoolean, [0x00]), StaticFieldV2LiteralValueKind.Boolean, 0);

        var negativeZero = Project(world, "LitR4", ConstantSingle, [0x00, 0x00, 0x00, 0x80]);
        Assert.Equal(StaticFieldV2LiteralValueKind.Single, negativeZero.ValueKind);
        Assert.Equal(0x8000_0000UL, negativeZero.FloatingBitPattern);
        Assert.Equal(-0.0f, negativeZero.SingleValue);
        Assert.True(float.IsNegative(negativeZero.SingleValue!.Value));
        Assert.Null(negativeZero.DoubleValue);

        var quietNaN = Project(world, "LitR8", ConstantDouble, [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0xFF]);
        Assert.Equal(StaticFieldV2LiteralValueKind.Double, quietNaN.ValueKind);
        Assert.Equal(0xFFF8_0000_0000_0000UL, quietNaN.FloatingBitPattern);
        Assert.True(double.IsNaN(quietNaN.DoubleValue!.Value));
        Assert.Equal(
            0xFFF8_0000_0000_0000UL,
            BitConverter.DoubleToUInt64Bits(quietNaN.DoubleValue.Value));
        Assert.Null(quietNaN.SingleValue);

        var pi = Project(
            world,
            "LitR8",
            ConstantDouble,
            [.. BitConverter.GetBytes(double.Pi)]);
        Assert.Equal(double.Pi, pi.DoubleValue);
        Assert.Equal(BitConverter.DoubleToUInt64Bits(double.Pi), pi.FloatingBitPattern);
    }

    /// <summary>Proves the UTF-16 string payload and the exact ECMA null encoding both decode from metadata.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void String_and_exact_null_literal_encodings_decode_from_metadata()
    {
        var world = BuildWorld();

        var text = Project(world, "LitString", ConstantString, Utf16("W8 😀"));
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, text.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueKind.String, text.ValueKind);
        Assert.Equal("W8 😀", text.StringValue);
        Assert.Equal(5, text.ObservedCount);

        var empty = Project(world, "LitString", ConstantString, []);
        Assert.Equal(string.Empty, empty.StringValue);
        Assert.Equal(0, empty.ObservedCount);

        // ECMA-335 II.22.9: the only admitted reference constant is ELEMENT_TYPE_CLASS with a four-byte zero value.
        foreach (var fieldName in new[] { "LitString", "LitObject", "LitClass", "LitArray" })
        {
            var nullOutcome = Project(world, fieldName, ConstantClass, [0x00, 0x00, 0x00, 0x00]);
            Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, nullOutcome.ResultKind);
            Assert.Equal(StaticFieldV2LiteralValueKind.Null, nullOutcome.ValueKind);
            Assert.Null(nullOutcome.SignedValue);
            Assert.Null(nullOutcome.UnsignedValue);
            Assert.Null(nullOutcome.FloatingBitPattern);
            Assert.Null(nullOutcome.StringValue);
            Assert.Equal(4, nullOutcome.ObservedCount);
        }

        // A null-reference encoding whose four bytes are not all zero is not a reference value, and the complete
        // table says so before any projection is reached.
        AssertTableRejects(
            world,
            "LitObject",
            ConstantClass,
            [0x00, 0x00, 0x00, 0x01],
            MetadataConstantTableIssue.NullReferenceValueNonZero);
        Assert.Equal(
            StaticFieldV2LiteralValueIssue.LiteralTypeDisagreement,
            Project(world, "LitI4", ConstantClass, [0x00, 0x00, 0x00, 0x00]).Issue);
    }

    /// <summary>
    /// Proves an enum-typed literal decodes its underlying primitive value and retains both the enum TypeDef and the
    /// decoded value, and that the underlying primitive must agree with the Constant type code.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Enum_underlying_literal_retains_the_enum_and_its_decoded_value()
    {
        var world = BuildWorld();

        var outcome = Project(world, "LitEnum", ConstantInt32, [0x07, 0x00, 0x00, 0x00], withCatalog: true);
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueKind.Int32, outcome.ValueKind);
        Assert.Equal(7, outcome.SignedValue);
        Assert.True(outcome.IsEnumUnderlyingValue);
        Assert.Equal(ColorsToken, outcome.EnumDefinition!.TypeDefinitionToken);
        Assert.Equal("Colors", outcome.EnumDefinition.TypeName);
        Assert.Contains(
            StaticFieldV2StorageCoverageBoundary.EnumUnderlyingDerivedFromInstanceValueField,
            outcome.DeclaredCoverageBoundaries);

        var declaredMember = Project(world, "ColorRed", ConstantInt32, [0x01, 0x00, 0x00, 0x00], withCatalog: true);
        Assert.Equal(1, declaredMember.SignedValue);
        Assert.Equal(ColorsToken, declaredMember.EnumDefinition!.TypeDefinitionToken);

        var disagreement = Project(
            world,
            "LitEnum",
            ConstantInt64,
            [0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            withCatalog: true);
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Invalid, disagreement.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.LiteralTypeDisagreement, disagreement.Issue);

        var withoutEvidence = Project(world, "LitEnum", ConstantInt32, [0x07, 0x00, 0x00, 0x00]);
        Assert.Equal(StaticFieldV2LiteralValueResultKind.NonExact, withoutEvidence.ResultKind);
        Assert.Equal(
            StaticFieldV2LiteralValueIssue.NamedLiteralTypeEvidenceUnavailable,
            withoutEvidence.Issue);
        Assert.Null(withoutEvidence.EnumDefinition);
    }

    /// <summary>Proves the static-string cap is admitted exactly and cap plus one stops with its bound.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void String_projection_admits_the_cap_and_stops_at_cap_plus_one()
    {
        var world = BuildWorld();
        const int cap = StaticFieldV2LiteralValueOutcome.MaximumStringCharacterCount;
        Assert.Equal(StaticFieldV2Limits.MaximumStringCharacterCount, cap);

        var atCap = Project(world, "LitString", ConstantString, Utf16(new string('x', cap)));
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, atCap.ResultKind);
        Assert.Equal(cap, atCap.StringValue!.Length);
        Assert.Equal(cap, atCap.ObservedCount);
        Assert.Null(atCap.ReachedBound);

        var overCap = Project(world, "LitString", ConstantString, Utf16(new string('x', cap + 1)));
        Assert.Equal(StaticFieldV2LiteralValueResultKind.NonExact, overCap.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.StringCharacterCountBoundReached, overCap.Issue);
        Assert.Null(overCap.StringValue);
        Assert.Null(overCap.ValueKind);
        Assert.Equal(cap + 1, overCap.ObservedCount);
        Assert.Equal("expression-v2.static.string-characters", overCap.ReachedBound!.Name);
        Assert.Equal(cap, overCap.ReachedBound.Value);
    }

    /// <summary>Proves signature-versus-Constant disagreement and malformed blobs are typed invalid stops.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Signature_disagreement_and_malformed_blobs_are_invalid()
    {
        var world = BuildWorld();

        var disagreement = Project(
            world,
            "LitI4",
            ConstantInt64,
            [0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Invalid, disagreement.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.LiteralTypeDisagreement, disagreement.Issue);
        Assert.Null(disagreement.ValueKind);
        Assert.Equal(ConstantInt64, disagreement.ObservedCount);

        Assert.Equal(
            StaticFieldV2LiteralValueIssue.LiteralTypeDisagreement,
            Project(world, "LitString", ConstantInt32, [0x00, 0x00, 0x00, 0x00]).Issue);

        // A blob whose width its type code fixes can no longer reach a projection at all: the complete Constant
        // table rejects it, so the stop now belongs one layer earlier. Each is still proven, against the table.
        AssertTableRejects(world, "LitI4", ConstantInt32, [0x2A, 0x00, 0x00]);
        AssertTableRejects(world, "LitI4", ConstantInt32, [0x2A, 0x00, 0x00, 0x00, 0x00]);
        AssertTableRejects(world, "LitI8", ConstantInt64, [0x2A]);
        AssertTableRejects(world, "LitR4", ConstantSingle, [0x00, 0x00]);
        AssertTableRejects(world, "LitChar", ConstantChar, [0x41]);
        AssertTableRejects(world, "LitString", ConstantString, [0x41, 0x00, 0x42]);
        AssertTableRejects(world, "LitClass", ConstantClass, [0x00, 0x00, 0x00]);

        // A Boolean wider than one is a width the table admits, so this one is still the projection's to reject.
        AssertInvalidBlob(Project(world, "LitBool", ConstantBoolean, [0x02]));
    }

    /// <summary>Proves unknown Constant codes and non-admitted signature forms are typed non-admissions.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Unadmitted_constant_codes_and_signature_forms_are_unsupported()
    {
        var world = BuildWorld();

        // A type code outside the admitted ECMA encoding set is rejected by the complete table, so no projection
        // ever observes one. The non-admission is proven there instead.
        AssertTableRejects(
            world,
            "LitI4",
            ConstantNativeInt,
            [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            MetadataConstantTableIssue.ConstantTypeCodeNotAdmitted);
        AssertTableRejects(
            world,
            "LitI4",
            0x00,
            [0x00, 0x00, 0x00, 0x00],
            MetadataConstantTableIssue.ConstantTypeCodeNotAdmitted);

        Assert.Equal(
            StaticFieldV2LiteralValueIssue.LiteralEncodingUnsupported,
            Project(world, "LitNative", ConstantInt32, [0x00, 0x00, 0x00, 0x00]).Issue);
        Assert.Equal(
            StaticFieldV2LiteralValueIssue.LiteralEncodingUnsupported,
            Project(world, "LitPointer", ConstantInt32, [0x00, 0x00, 0x00, 0x00]).Issue);

        // W8.1 froze decimal as a compiler-emitted attribute encoding rather than a Constant-table type: a named value
        // type that declares no instance value__ field therefore has no admitted Constant encoding at all.
        var attributeEncoded = Project(
            world,
            "LitMoney",
            ConstantInt32,
            [0x00, 0x00, 0x00, 0x00],
            withCatalog: true);
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Unsupported, attributeEncoded.ResultKind);
        Assert.Equal(
            StaticFieldV2LiteralValueIssue.AttributeEncodedLiteralNotModeled,
            attributeEncoded.Issue);
        Assert.Contains(
            StaticFieldV2StorageCoverageBoundary.CustomAttributeTableNotModeled,
            attributeEncoded.DeclaredCoverageBoundaries);
        Assert.DoesNotContain(
            Enum.GetNames<StaticFieldV2LiteralValueKind>(),
            static name => name.Contains("Decimal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            Enum.GetNames<MetadataPrimitiveTypeKind>(),
            static name => name.Contains("Decimal", StringComparison.OrdinalIgnoreCase));

        // A field that declares no default value cannot own a Constant row at all: the complete table's pairing
        // rejects it, so the projection's own FieldNotLiteral branch is now unreachable from valid evidence and the
        // refusal is proven one layer earlier.
        var plainStatic = Row(world, "PlainStatic");
        Assert.False(plainStatic.HasDefault);
        // The table is otherwise complete - the declared end is met - so the pairing is what rejects it, not a count.
        var defaults = world.FieldCatalog.Rows.Where(static row => row.HasDefault).ToArray();
        var rePaired = ImmutableArray.CreateBuilder<MetadataConstantRowObservationIdentity>(defaults.Length);
        for (var index = 0; index < defaults.Length; index++)
        {
            rePaired.Add(MetadataConstantRowObservationIdentity.Create(
                metadataModule: world.Module,
                constantToken: 0x0B00_0000 | (index + 1),
                constantTypeCode: ConstantInt32,
                parentMetadataToken: index == 0
                    ? plainStatic.Observation.FieldDefinitionToken
                    : defaults[index].Observation.FieldDefinitionToken,
                constantValueBlob: [0x00, 0x00, 0x00, 0x00]));
        }

        var pairedToPlain = MetadataConstantTableCatalogIdentity.Create(
            world.DeclaredMemberSourceEnds,
            world.FieldCatalog,
            rePaired.MoveToImmutable());
        Assert.Equal(MetadataConstantTableResultKind.Invalid, pairedToPlain.ResultKind);
        Assert.Equal(MetadataConstantTableIssue.FieldParentWithoutDefaultFlag, pairedToPlain.Issue);
    }

    /// <summary>
    /// Proves literal projection performs no runtime, thread, module-content, slot, or memory call by decoding while
    /// holding capability probes that throw whenever they are invoked.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Literal_projection_invokes_no_capability_probe()
    {
        var world = BuildWorld();
        var poisoned = ExpressionV2CapabilityProbeSet.Create(
            runtimeConstruction: static () => throw new InvalidOperationException("runtime construction"),
            threadIdentity: static () => throw new InvalidOperationException("thread identity"),
            moduleContent: static () => throw new InvalidOperationException("module content"),
            staticSlotAcquisition: static () => throw new InvalidOperationException("static slot"),
            memoryRead: static () => throw new InvalidOperationException("memory read"));

        var outcome = StaticFieldV2StorageStrategyBinder.ProjectLiteral(
            StaticFieldV2LiteralProjectionRequest.Create(
                Row(world, "LitI4"),
                ConstantRowFor(world, "LitI4", ConstantInt32, [0x2A, 0x00, 0x00, 0x00]),
                world.FieldCatalog,
                poisoned));
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(42, outcome.SignedValue);
        Assert.True(outcome.CapabilityCallLedger.IsZero);
        Assert.Equal(0, outcome.CapabilityCallLedger.TotalCallCount);
        Assert.Equal(0, poisoned.TotalCallCount);
        foreach (var capability in Enum.GetValues<StaticFieldV2StorageCapability>())
        {
            Assert.Equal(0, outcome.CapabilityCallLedger.CallCount(capability));
            Assert.Equal(0, poisoned.CallCount(capability));
        }

        // Every typed stop retains the same zero ledger, so no non-admitted branch acquires a capability either.
        var stop = StaticFieldV2StorageStrategyBinder.ProjectLiteral(
            StaticFieldV2LiteralProjectionRequest.Create(
                Row(world, "LitPointer"),
                ConstantRowFor(world, "LitPointer", ConstantInt32, [0x00, 0x00, 0x00, 0x00]),
                world.FieldCatalog,
                poisoned));
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Unsupported, stop.ResultKind);
        Assert.True(stop.CapabilityCallLedger.IsZero);
        Assert.Equal(0, poisoned.TotalCallCount);

        // The meter is real: a directly invoked probe both counts and runs its caller-owned delegate.
        var invoked = 0;
        var counting = ExpressionV2CapabilityProbeSet.Create(memoryRead: () => invoked++);
        counting.Invoke(StaticFieldV2StorageCapability.MemoryRead);
        counting.Invoke(StaticFieldV2StorageCapability.RuntimeConstruction);
        Assert.Equal(1, invoked);
        Assert.Equal(1, counting.MemoryReadCallCount);
        Assert.Equal(1, counting.RuntimeConstructionCallCount);
        Assert.Equal(2, counting.TotalCallCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            counting.Invoke((StaticFieldV2StorageCapability)9));
        Assert.Throws<InvalidOperationException>(() =>
            poisoned.Invoke(StaticFieldV2StorageCapability.ThreadIdentity));
        Assert.Equal(1, poisoned.ThreadIdentityCallCount);
    }

    /// <summary>
    /// Proves canonical replay, defensive copies, guarded private issuance, the closed public surface, and
    /// emitted XML documentation for the whole storage-strategy and literal-projection surface.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Storage_contracts_replay_are_immutable_guarded_and_documented()
    {
        var world = BuildWorld();
        var replayWorld = BuildWorld();

        var strategy = Classify(world, "RvaBlob");
        var replayStrategy = Classify(replayWorld, "RvaBlob");
        Assert.Equal(strategy, replayStrategy);
        Assert.Equal(strategy.GetHashCode(), replayStrategy.GetHashCode());
        Assert.Equal(strategy.Sha256, replayStrategy.Sha256);
        Assert.Equal(strategy.Request, replayStrategy.Request);
        Assert.Equal(strategy.CapabilityRequirements, replayStrategy.CapabilityRequirements);
        Assert.True(strategy.CanonicalBytes.AsSpan().SequenceEqual(replayStrategy.CanonicalBytes.AsSpan()));
        Assert.NotEqual(strategy, Classify(world, "PlainStatic"));
        Assert.NotEqual(strategy.CapabilityRequirements, Classify(world, "PlainStatic").CapabilityRequirements);

        var literal = Project(world, "LitEnum", ConstantInt32, [0x07, 0x00, 0x00, 0x00], withCatalog: true);
        var replayLiteral = Project(replayWorld, "LitEnum", ConstantInt32, [0x07, 0x00, 0x00, 0x00], withCatalog: true);
        Assert.Equal(literal, replayLiteral);
        Assert.Equal(literal.GetHashCode(), replayLiteral.GetHashCode());
        Assert.Equal(literal.Request, replayLiteral.Request);
        Assert.Equal(literal.CapabilityCallLedger, replayLiteral.CapabilityCallLedger);
        Assert.NotEqual(literal, Project(world, "LitEnum", ConstantInt32, [0x08, 0x00, 0x00, 0x00], withCatalog: true));
        // Re-frozen when this world's source ends began recording the Constant, PropertyMap, and PropertyPtr table
        // ends. Those ends are embedded in every FieldDef row identity and so reach this outcome; the literal
        // decoding itself is unchanged, and the value assertions above pin that independently of the digest.
        Assert.Equal(
            "e8d7ff99105fd963802a9dfb8cb5667e9223dc2ffa694577828428d482a61da2",
            literal.Sha256);

        var originalBoundaries = literal.DeclaredCoverageBoundaries;
        var originalSha = literal.Sha256;
        var originalBytes = literal.CanonicalBytes;
        var mutatedBoundaries = literal.DeclaredCoverageBoundaries;
        ImmutableCollectionsMarshal.AsArray(mutatedBoundaries)![0] =
            StaticFieldV2StorageCoverageBoundary.ThreadStaticAttributeSuppliedByCaller;
        var mutatedBytes = literal.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(mutatedBytes)![0] ^= 0x5A;
        var mutatedBlob = literal.Request.ConstantValueBlob;
        ImmutableCollectionsMarshal.AsArray(mutatedBlob)![0] ^= 0xFF;
        var mutatedRequired = strategy.CapabilityRequirements.RequiredCapabilities;
        ImmutableCollectionsMarshal.AsArray(mutatedRequired)![0] =
            StaticFieldV2StorageCapability.RuntimeConstruction;
        var mutatedVectorBytes = strategy.CapabilityRequirements.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(mutatedVectorBytes)![0] ^= 0x33;

        Assert.Equal(originalBoundaries[0], literal.DeclaredCoverageBoundaries[0]);
        Assert.Equal(originalSha, literal.Sha256);
        Assert.True(originalBytes.AsSpan().SequenceEqual(literal.CanonicalBytes.AsSpan()));
        Assert.Equal(0x07, literal.Request.ConstantValueBlob[0]);
        Assert.Equal(
            [StaticFieldV2StorageCapability.ModuleContent, StaticFieldV2StorageCapability.MemoryRead],
            strategy.CapabilityRequirements.RequiredCapabilities.ToArray());
        Assert.Equal(strategy.CapabilityRequirements.Sha256, replayStrategy.CapabilityRequirements.Sha256);

        Assert.False(StaticFieldV2StorageStrategyOutcome.OwnsRowMintCapability(new object()));
        Assert.False(StaticFieldV2LiteralValueOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2CapabilityRequirementVector.Create(
            new object(),
            StaticFieldV2CapabilityRequirement.Required,
            StaticFieldV2CapabilityRequirement.Required,
            StaticFieldV2CapabilityRequirement.Required,
            StaticFieldV2CapabilityRequirement.Required,
            StaticFieldV2CapabilityRequirement.Required));
        Assert.Throws<ArgumentException>(() => StaticFieldV2CapabilityCallLedger.Create(
            new object(),
            0,
            0,
            0,
            0,
            0));

        Assert.Throws<ArgumentNullException>(() => StaticFieldV2StorageStrategyBinder.ClassifyStrategy(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2StorageStrategyBinder.ProjectLiteral(null!));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2StorageStrategyRequest.Create(
            null!,
            Row(world, "LitI4").DeclaringTypeDefinition));
        Assert.Throws<ArgumentException>(() => StaticFieldV2StorageStrategyRequest.Create(
            Row(world, "LitI4"),
            Row(world, "PlainStatic").DeclaringTypeDefinition));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2LiteralProjectionRequest.Create(
            null!,
            ConstantRowFor(world, "LitI4", ConstantInt32, [0x00, 0x00, 0x00, 0x00])));
        Assert.Throws<ArgumentNullException>(() => StaticFieldV2LiteralProjectionRequest.Create(
            Row(world, "LitI4"),
            null!));

        // A malformed type code or blob is no longer representable here: the request carries a row a complete
        // Constant table already validated. The one thing a caller can still get wrong is pairing a valid row with
        // the wrong field, and that is a typed stop rather than a throw.
        Assert.Equal(
            StaticFieldV2LiteralValueIssue.ConstantRowParentMismatch,
            StaticFieldV2StorageStrategyBinder.ProjectLiteral(
                StaticFieldV2LiteralProjectionRequest.Create(
                    Row(world, "LitI4"),
                    ConstantRowFor(world, "LitI8", ConstantInt64, [0, 0, 0, 0, 0, 0, 0, 0]),
                    world.FieldCatalog)).Issue);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            strategy.CapabilityRequirements.For((StaticFieldV2StorageCapability)9));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            literal.CapabilityCallLedger.CallCount((StaticFieldV2StorageCapability)9));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2StorageStrategy),
            typeof(StaticFieldV2StorageCapability),
            typeof(StaticFieldV2CapabilityRequirement),
            typeof(StaticFieldV2StorageStrategyResultKind),
            typeof(StaticFieldV2StorageStrategyIssue),
            typeof(StaticFieldV2StorageCoverageBoundary),
            typeof(StaticFieldV2LiteralValueKind),
            typeof(StaticFieldV2LiteralValueResultKind),
            typeof(StaticFieldV2LiteralValueIssue),
            typeof(StaticFieldV2CapabilityRequirementVector),
            typeof(StaticFieldV2CapabilityCallLedger),
            typeof(ExpressionV2CapabilityProbeSet),
            typeof(StaticFieldV2StorageStrategyRequest),
            typeof(StaticFieldV2StorageStrategyOutcome),
            typeof(StaticFieldV2LiteralProjectionRequest),
            typeof(StaticFieldV2LiteralValueOutcome),
            typeof(StaticFieldV2StorageStrategyBinder),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (type == typeof(StaticFieldV2StorageStrategyBinder))
            {
                Assert.Equal(["ClassifyStrategy", "ProjectLiteral"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2StorageStrategyRequest) ||
                     type == typeof(StaticFieldV2LiteralProjectionRequest) ||
                     type == typeof(ExpressionV2CapabilityProbeSet))
            {
                Assert.Equal(["Create"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static void AssertVector(
        StaticFieldV2StorageStrategyOutcome outcome,
        StaticFieldV2CapabilityRequirement runtimeConstruction,
        StaticFieldV2CapabilityRequirement threadIdentity,
        StaticFieldV2CapabilityRequirement moduleContent,
        StaticFieldV2CapabilityRequirement staticSlotAcquisition,
        StaticFieldV2CapabilityRequirement memoryRead)
    {
        var vector = outcome.CapabilityRequirements;
        Assert.Equal(runtimeConstruction, vector.RuntimeConstruction);
        Assert.Equal(threadIdentity, vector.ThreadIdentity);
        Assert.Equal(moduleContent, vector.ModuleContent);
        Assert.Equal(staticSlotAcquisition, vector.StaticSlotAcquisition);
        Assert.Equal(memoryRead, vector.MemoryRead);
        Assert.Equal(runtimeConstruction, vector.For(StaticFieldV2StorageCapability.RuntimeConstruction));
        Assert.Equal(threadIdentity, vector.For(StaticFieldV2StorageCapability.ThreadIdentity));
        Assert.Equal(moduleContent, vector.For(StaticFieldV2StorageCapability.ModuleContent));
        Assert.Equal(staticSlotAcquisition, vector.For(StaticFieldV2StorageCapability.StaticSlotAcquisition));
        Assert.Equal(memoryRead, vector.For(StaticFieldV2StorageCapability.MemoryRead));
    }

    private static void AssertSigned(
        StaticFieldV2LiteralValueOutcome outcome,
        StaticFieldV2LiteralValueKind kind,
        long expected)
    {
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.None, outcome.Issue);
        Assert.Equal(kind, outcome.ValueKind);
        Assert.Equal(expected, outcome.SignedValue);
        Assert.Null(outcome.UnsignedValue);
    }

    private static void AssertUnsigned(
        StaticFieldV2LiteralValueOutcome outcome,
        StaticFieldV2LiteralValueKind kind,
        ulong expected)
    {
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.None, outcome.Issue);
        Assert.Equal(kind, outcome.ValueKind);
        Assert.Equal(expected, outcome.UnsignedValue);
        Assert.Null(outcome.SignedValue);
    }

    private static void AssertInvalidBlob(StaticFieldV2LiteralValueOutcome outcome)
    {
        Assert.Equal(StaticFieldV2LiteralValueResultKind.Invalid, outcome.ResultKind);
        Assert.Equal(StaticFieldV2LiteralValueIssue.LiteralBlobInvalid, outcome.Issue);
        Assert.Null(outcome.ValueKind);
        Assert.Null(outcome.SignedValue);
        Assert.Null(outcome.UnsignedValue);
        Assert.Null(outcome.StringValue);
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2StorageStrategyBinder).Assembly;
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

    private static ImmutableArray<byte> Utf16(string value)
    {
        var bytes = ImmutableArray.CreateBuilder<byte>(value.Length * 2);
        foreach (var codeUnit in value)
        {
            bytes.Add((byte)(codeUnit & 0xFF));
            bytes.Add((byte)(codeUnit >> 8));
        }
        return bytes.MoveToImmutable();
    }

    private static StaticFieldV2StorageStrategyOutcome Classify(
        StorageWorld world,
        string fieldName,
        bool threadStatic = false,
        bool contextStatic = false)
    {
        var row = Row(world, fieldName);
        return StaticFieldV2StorageStrategyBinder.ClassifyStrategy(
            StaticFieldV2StorageStrategyRequest.Create(
                row,
                row.DeclaringTypeDefinition,
                threadStatic,
                contextStatic));
    }

    private static StaticFieldV2LiteralValueOutcome Project(
        StorageWorld world,
        string fieldName,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob,
        bool withCatalog = false) =>
        StaticFieldV2StorageStrategyBinder.ProjectLiteral(
            StaticFieldV2LiteralProjectionRequest.Create(
                Row(world, fieldName),
                ConstantRowFor(world, fieldName, constantTypeCode, constantValueBlob),
                withCatalog ? world.FieldCatalog : null));

    /// <summary>Asserts the complete Constant table refuses one physical encoding before any projection sees it.</summary>
    private static void AssertTableRejects(
        StorageWorld world,
        string fieldName,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob,
        MetadataConstantTableIssue expected = MetadataConstantTableIssue.ConstantValueWidthInvalid)
    {
        var catalog = ConstantCatalogFor(world, fieldName, constantTypeCode, constantValueBlob);
        Assert.Equal(MetadataConstantTableResultKind.Invalid, catalog.ResultKind);
        Assert.Equal(expected, catalog.Issue);
        Assert.Empty(catalog.Rows);
    }

    private static MetadataFieldDefinitionTableRowIdentity Row(StorageWorld world, string fieldName) =>
        Assert.Single(world.FieldCatalog.Rows, row => string.Equals(row.Name, fieldName, StringComparison.Ordinal));

    private static StorageWorld BuildWorld(ulong addressBase = 0x9000) =>
        BuildModule(
            W8CompilerNameMappingContractTests.CreateMetadataModule(addressBase, '7', "Synthetic.StorageApp"),
            [
                new StorageTypeRow(
                    "Synthetic.Storage",
                    "Colors",
                    [
                        new StorageFieldRow("value__", FieldEnumValue, [0x06, 0x08]),
                        new StorageFieldRow("ColorRed", StaticLiteral, ValueTypeSignature(ColorsToken)),
                    ]),
                new StorageTypeRow(
                    "Synthetic.Storage",
                    "Money",
                    [new StorageFieldRow("MoneyUnits", FieldPublic, [0x06, 0x0A])]),
                new StorageTypeRow(
                    "Synthetic.Storage",
                    "Holder",
                    [
                        new StorageFieldRow("LitBool", StaticLiteral, [0x06, 0x02]),
                        new StorageFieldRow("LitChar", StaticLiteral, [0x06, 0x03]),
                        new StorageFieldRow("LitI1", StaticLiteral, [0x06, 0x04]),
                        new StorageFieldRow("LitU1", StaticLiteral, [0x06, 0x05]),
                        new StorageFieldRow("LitI2", StaticLiteral, [0x06, 0x06]),
                        new StorageFieldRow("LitU2", StaticLiteral, [0x06, 0x07]),
                        new StorageFieldRow("LitI4", StaticLiteral, [0x06, 0x08]),
                        new StorageFieldRow("LitU4", StaticLiteral, [0x06, 0x09]),
                        new StorageFieldRow("LitI8", StaticLiteral, [0x06, 0x0A]),
                        new StorageFieldRow("LitU8", StaticLiteral, [0x06, 0x0B]),
                        new StorageFieldRow("LitR4", StaticLiteral, [0x06, 0x0C]),
                        new StorageFieldRow("LitR8", StaticLiteral, [0x06, 0x0D]),
                        new StorageFieldRow("LitString", StaticLiteral, [0x06, 0x0E]),
                        new StorageFieldRow("LitObject", StaticLiteral, [0x06, 0x1C]),
                        new StorageFieldRow("LitClass", StaticLiteral, ClassSignature(HolderToken)),
                        new StorageFieldRow("LitArray", StaticLiteral, [0x06, 0x1D, 0x08]),
                        new StorageFieldRow("LitEnum", StaticLiteral, ValueTypeSignature(ColorsToken)),
                        new StorageFieldRow("LitMoney", StaticLiteral, ValueTypeSignature(MoneyToken)),
                        new StorageFieldRow("LitNative", StaticLiteral, [0x06, 0x18]),
                        new StorageFieldRow("LitPointer", StaticLiteral, [0x06, 0x0F, 0x08]),
                    ]),
                new StorageTypeRow(
                    "Synthetic.Storage",
                    "StorageZoo",
                    [
                        new StorageFieldRow("PlainStatic", FieldPublic | FieldStatic, [0x06, 0x08]),
                        new StorageFieldRow("RvaBlob", FieldPublic | FieldStatic | FieldRva, [0x06, 0x08]),
                        new StorageFieldRow("LiteralAnswer", StaticLiteral, [0x06, 0x08]),
                        new StorageFieldRow("LiteralAndRva", StaticLiteral | FieldRva, [0x06, 0x08]),
                        new StorageFieldRow("InstanceField", FieldPublic, [0x06, 0x08]),
                    ]),
            ]);

    private static ImmutableArray<byte> ClassSignature(int typeDefinitionToken) =>
        [0x06, 0x12, (byte)((typeDefinitionToken & 0x00FF_FFFF) << 2)];

    private static ImmutableArray<byte> ValueTypeSignature(int typeDefinitionToken) =>
        [0x06, 0x11, (byte)((typeDefinitionToken & 0x00FF_FFFF) << 2)];

    private static StorageWorld BuildModule(
        StaticFieldMetadataModuleIdentity module,
        ImmutableArray<StorageTypeRow> namedTypes)
    {
        var totalTypeCount = namedTypes.Length + 1;
        var fieldObservations = ImmutableArray.CreateBuilder<MetadataFieldDefinitionRowObservationIdentity>();
        var fieldStarts = new int[totalTypeCount];
        fieldStarts[0] = 1;
        for (var index = 0; index < namedTypes.Length; index++)
        {
            fieldStarts[index + 1] = fieldObservations.Count + 1;
            foreach (var field in namedTypes[index].Fields)
            {
                fieldObservations.Add(MetadataFieldDefinitionRowObservationIdentity.Create(
                    module,
                    0x0400_0000 | checked(fieldObservations.Count + 1),
                    field.Attributes,
                    field.Name,
                    field.Signature));
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

                // One Constant row per field that declares a default value: the count the catalog must agree with.
                declaredMemberRowCounts: StaticFieldModuleDeclaredMemberRowCounts.Create(
                    constantRowCount: fieldObservations.Count(
                        static field => ((FieldAttributes)field.Attributes & FieldAttributes.HasDefault) != 0),
                    propertyMapRowCount: 0,
                    propertyPointerRowCount: 0)));

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
                typeAttributes: PublicClass,
                extendsMetadataToken: null));
        }

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows.MoveToImmutable(),
            pointers);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, typeDefinitions.ResultKind);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(sourceEnds, typeDefinitions, []);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(sourceEnds, default);
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var fieldCatalog = MetadataFieldDefinitionTableCatalogIdentity.Create(
            authority,
            fieldObservations.ToImmutable());
        Assert.True(
            fieldCatalog.ResultKind == MetadataFieldDefinitionTableResultKind.Exact,
            $"FieldCatalog={fieldCatalog.ResultKind}/{fieldCatalog.Issue} at {fieldCatalog.RelatedMetadataToken:X8}.");
        return new StorageWorld(
            module,
            authority,
            fieldCatalog,
            MetadataDeclaredMemberSourceEndIdentity.Create(sourceEnds));
    }

    private sealed record StorageFieldRow(string Name, int Attributes, ImmutableArray<byte> Signature);

    private sealed record StorageTypeRow(
        string NamespaceName,
        string TypeName,
        ImmutableArray<StorageFieldRow> Fields);

    private sealed record StorageWorld(
        StaticFieldMetadataModuleIdentity Module,
        MetadataDefinitionAuthorityCatalogIdentity Authority,
        MetadataFieldDefinitionTableCatalogIdentity FieldCatalog,
        MetadataDeclaredMemberSourceEndIdentity DeclaredMemberSourceEnds);

    /// <summary>
    /// Builds one exact Constant table over this world, giving the named field the requested physical encoding.
    /// </summary>
    /// <remarks>
    /// A complete Constant table proves the pairing in both directions, so every field declaring a default value must
    /// own a row - not only the field under test. The other rows carry an admissible four-byte Int32 encoding, which
    /// is all the catalog checks: whether a row's encoding agrees with its field's signature is the projection's
    /// question, not the table's, and keeping it that way is what lets one fixture serve every literal case.
    /// </remarks>
    private static MetadataConstantTableRowIdentity ConstantRowFor(
        StorageWorld world,
        string fieldName,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob)
    {
        var defaultFields = world.FieldCatalog.Rows.Where(static row => row.HasDefault).ToArray();
        var observations = ImmutableArray.CreateBuilder<MetadataConstantRowObservationIdentity>(defaultFields.Length);
        var requestedIndex = -1;
        for (var index = 0; index < defaultFields.Length; index++)
        {
            var fieldRow = defaultFields[index];
            var isRequested = string.Equals(fieldRow.Name, fieldName, StringComparison.Ordinal);
            if (isRequested)
            {
                requestedIndex = index;
            }

            observations.Add(MetadataConstantRowObservationIdentity.Create(
                metadataModule: world.Module,
                constantToken: 0x0B00_0000 | (index + 1),
                constantTypeCode: isRequested ? constantTypeCode : ConstantInt32,
                parentMetadataToken: fieldRow.Observation.FieldDefinitionToken,
                constantValueBlob: isRequested ? constantValueBlob : [0x00, 0x00, 0x00, 0x00]));
        }

        Assert.True(requestedIndex >= 0, $"The world declares no default-valued field named {fieldName}.");
        var catalog = MetadataConstantTableCatalogIdentity.Create(
            world.DeclaredMemberSourceEnds,
            world.FieldCatalog,
            observations.MoveToImmutable());
        Assert.Equal(MetadataConstantTableResultKind.Exact, catalog.ResultKind);
        return catalog.Rows[requestedIndex];
    }

    private static MetadataConstantTableCatalogIdentity ConstantCatalogFor(
        StorageWorld world,
        string fieldName,
        int constantTypeCode,
        ImmutableArray<byte> constantValueBlob)
    {
        var defaultFields = world.FieldCatalog.Rows.Where(static row => row.HasDefault).ToArray();
        var observations = ImmutableArray.CreateBuilder<MetadataConstantRowObservationIdentity>(defaultFields.Length);
        for (var index = 0; index < defaultFields.Length; index++)
        {
            var fieldRow = defaultFields[index];
            var isRequested = string.Equals(fieldRow.Name, fieldName, StringComparison.Ordinal);
            observations.Add(MetadataConstantRowObservationIdentity.Create(
                metadataModule: world.Module,
                constantToken: 0x0B00_0000 | (index + 1),
                constantTypeCode: isRequested ? constantTypeCode : ConstantInt32,
                parentMetadataToken: fieldRow.Observation.FieldDefinitionToken,
                constantValueBlob: isRequested ? constantValueBlob : [0x00, 0x00, 0x00, 0x00]));
        }

        return MetadataConstantTableCatalogIdentity.Create(
            world.DeclaredMemberSourceEnds,
            world.FieldCatalog,
            observations.MoveToImmutable());
    }
}
