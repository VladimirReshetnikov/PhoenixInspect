using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Freezes the compiler-emitted property and object-graph facts required before W6 admits member-chain syntax.
/// </summary>
/// <remarks>
/// This is an independent PE/SRM fixture oracle. It deliberately does not call, implement, or share the reusable
/// terminal-property certificate planned for W6.3, and it performs no dump-memory value read.
/// </remarks>
public sealed class W6EmittedShapeFixtureTests
{
    private const int ExpectedArtifactLength = 30720;
    private const string ExpectedArtifactSha256 =
        "1eee4384cc891aa7908b7b425b0626e66cd1ddd08bac11a4ad26d917d378e05a";
    private const string ExpectedMvid = "6039baaa-605a-480b-a989-53f6e68acabc";
    private const int ExpectedMetadataLength = 18680;
    private const string ExpectedMetadataSha256 =
        "c8e0207d7b7c1553a862c6fe1383c446637f36d5025be5136fea4e45095311e5";
    private const byte TinyMethodHeaderForSevenCodeBytes = 0x1E;
    private const byte LoadArgumentZero = 0x02;
    private const byte LoadInstanceField = 0x7B;
    private const byte Return = 0x2A;

    /// <summary>
    /// Freezes the single intentional W6.1 TestTarget content transition as complete-artifact and metadata-root
    /// identities after all four graph families and their emitted property relations have been finalized.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CurrentTargetArtifactIdentityIsExplicitlyFrozen()
    {
        var artifact = File.ReadAllBytes(ResolveTargetAssemblyPath());
        using var stream = new MemoryStream(artifact, writable: false);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadata().GetContent();
        var reader = peReader.GetMetadataReader();
        var module = reader.GetModuleDefinition();

        Assert.Equal(ExpectedArtifactLength, artifact.Length);
        Assert.Equal(ExpectedArtifactSha256, Hash(artifact));
        Assert.Equal(ExpectedMvid, reader.GetGuid(module.Mvid).ToString("D"));
        Assert.Equal(ExpectedMetadataLength, metadata.Length);
        Assert.Equal(ExpectedMetadataSha256, Hash(metadata.AsSpan()));
    }

    /// <summary>
    /// Proves that every terminal record property selected for the W6 corpus has one exact PropertyDef/getter/backing-
    /// FieldDef projection with matching signatures and the frozen tiny <c>ldarg.0; ldfld; ret</c> body.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void SelectedTerminalPropertiesHaveExactCompilerEmittedProjection()
    {
        using var stream = File.OpenRead(ResolveTargetAssemblyPath());
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var expected = new[]
        {
            new ExpectedProperty("SyntheticFailureRecord", "Code", SignatureType.String),
            new ExpectedProperty("SyntheticRequestState", "Status", SignatureType.String),
            new ExpectedProperty("SyntheticBatchProgress", "State", SignatureType.String),
            new ExpectedProperty("SyntheticBatchProgress", "CompletedPartitions", SignatureType.Int32),
            new ExpectedProperty("SyntheticCoordinatorOwner", "Name", SignatureType.String),
            new ExpectedProperty("SyntheticCoordinatorJob", "RetryCount", SignatureType.Int32),
            new ExpectedProperty("SyntheticShardHealth", "State", SignatureType.String),
            new ExpectedProperty("SyntheticCoordinatorWorker", "State", SignatureType.String),
            new ExpectedProperty("SyntheticWorkflowAttempt", "Status", SignatureType.String),
            new ExpectedProperty("SyntheticAssignedWorker", "State", SignatureType.String),
        };

        var seenGetterTokens = new HashSet<int>();
        var seenBackingFieldTokens = new HashSet<int>();
        foreach (var item in expected)
        {
            var projection = ReadPropertyProjection(peReader, reader, item.TypeName, item.PropertyName);
            var elementType = item.Type == SignatureType.String ? (byte)0x0E : (byte)0x08;

            Assert.Equal(PropertyAttributes.None, projection.Property.Attributes);
            Assert.Equal(new byte[] { 0x28, 0x00, elementType }, projection.PropertySignature);
            Assert.Equal($"get_{item.PropertyName}", projection.GetterName);
            Assert.Equal($"set_{item.PropertyName}", projection.SetterName);
            Assert.True((projection.Getter.Attributes & MethodAttributes.Public) != 0);
            Assert.True((projection.Getter.Attributes & MethodAttributes.HideBySig) != 0);
            Assert.True((projection.Getter.Attributes & MethodAttributes.SpecialName) != 0);
            Assert.False((projection.Getter.Attributes & MethodAttributes.Static) != 0);
            Assert.False((projection.Getter.Attributes & MethodAttributes.Virtual) != 0);
            Assert.Equal(MethodImplAttributes.IL, projection.Getter.ImplAttributes & MethodImplAttributes.CodeTypeMask);
            Assert.Equal(new byte[] { 0x20, 0x00, elementType }, projection.GetterSignature);
            Assert.Empty(projection.OtherAccessors);

            Assert.Equal(TinyMethodHeaderForSevenCodeBytes, projection.HeaderByte);
            Assert.Equal(8, projection.Body.MaxStack);
            Assert.False(projection.Body.LocalVariablesInitialized);
            Assert.True(projection.Body.LocalSignature.IsNil);
            Assert.Empty(projection.Body.ExceptionRegions);
            Assert.Equal(7, projection.Code.Length);
            Assert.Equal(LoadArgumentZero, projection.Code[0]);
            Assert.Equal(LoadInstanceField, projection.Code[1]);
            Assert.Equal(Return, projection.Code[6]);

            Assert.Equal($"<{item.PropertyName}>k__BackingField", projection.BackingFieldName);
            Assert.True((projection.BackingField.Attributes & FieldAttributes.Private) != 0);
            Assert.True((projection.BackingField.Attributes & FieldAttributes.InitOnly) != 0);
            Assert.False((projection.BackingField.Attributes & FieldAttributes.Static) != 0);
            Assert.Equal(new byte[] { 0x06, elementType }, projection.BackingFieldSignature);
            Assert.Contains(projection.BackingFieldHandle, projection.DeclaringType.GetFields());
            Assert.Equal(
                MetadataTokens.GetToken(projection.BackingFieldHandle),
                BinaryPrimitives.ReadInt32LittleEndian(projection.Code.AsSpan(2, sizeof(int))));
            Assert.True(seenGetterTokens.Add(MetadataTokens.GetToken(projection.GetterHandle)));
            Assert.True(seenBackingFieldTokens.Add(MetadataTokens.GetToken(projection.BackingFieldHandle)));
        }

        Assert.Equal(expected.Length, seenGetterTokens.Count);
        Assert.Equal(expected.Length, seenBackingFieldTokens.Count);
    }

    /// <summary>
    /// Proves that all four W6 graph families are non-inheriting, structurally distinct, and already contain every
    /// reference, collection, method, and terminal record needed by the predeclared twenty-four-row corpus.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void FourGraphFamiliesFreezeDistinctRelationalShapes()
    {
        var assembly = Assembly.LoadFile(Path.GetFullPath(ResolveTargetAssemblyPath()));
        var request = RequireType(assembly, "SyntheticRequestPipelineProbe");
        var batch = RequireType(assembly, "SyntheticBatchPipelineProbe");
        var coordinator = RequireType(assembly, "SyntheticCoordinatorPipelineProbe");
        var workflow = RequireType(assembly, "SyntheticWorkflowDispatchProbe");
        var roots = new[] { request, batch, coordinator, workflow };

        Assert.Equal(4, roots.Distinct().Count());
        Assert.All(roots, type =>
        {
            Assert.True(type.IsSealed);
            Assert.Equal(typeof(object), type.BaseType);
            Assert.NotNull(type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(int), typeof(int), typeof(string)],
                modifiers: null));
            Assert.NotNull(type.GetMethod("GetMarkerSummary", BindingFlags.Instance | BindingFlags.NonPublic));
        });
        foreach (var left in roots)
        {
            foreach (var right in roots)
            {
                if (left != right)
                {
                    Assert.False(left.IsAssignableFrom(right));
                }
            }
        }

        AssertDeclaredFields(request,
            ("Marker", typeof(int)),
            ("AlternateMarker", typeof(int)),
            ("Failure", RequireType(assembly, "SyntheticFailureRecord")),
            ("CurrentRequest", RequireType(assembly, "SyntheticRequestState")),
            ("RetryMarkers", typeof(int[])));
        AssertDeclaredFields(batch,
            ("Marker", typeof(int)),
            ("AlternateMarker", typeof(int)),
            ("LastFailure", RequireType(assembly, "SyntheticFailureRecord")),
            ("Progress", RequireType(assembly, "SyntheticBatchProgress")),
            ("PartitionMarkers", typeof(Dictionary<string, int>)));
        AssertDeclaredFields(coordinator,
            ("Marker", typeof(int)),
            ("AlternateMarker", typeof(int)),
            ("Owner", RequireType(assembly, "SyntheticCoordinatorOwner")),
            ("ActiveJob", RequireType(assembly, "SyntheticCoordinatorJob")),
            ("ActiveShard", RequireType(assembly, "SyntheticCoordinatorShard")),
            ("Workers", RequireType(assembly, "SyntheticCoordinatorWorker").MakeArrayType()),
            ("CurrentTask", RequireType(assembly, "SyntheticCoordinatorTask")));
        AssertDeclaredFields(workflow,
            ("Marker", typeof(int)),
            ("AlternateMarker", typeof(int)),
            ("CurrentAttempt", RequireType(assembly, "SyntheticWorkflowAttempt")),
            ("OptionalError", RequireType(assembly, "SyntheticWorkflowError")),
            ("AssignedWorker", RequireType(assembly, "SyntheticAssignedWorker")),
            ("Attempts", RequireType(assembly, "SyntheticWorkflowAttempt").MakeArrayType()));

        AssertDeclaredProperty(assembly, "SyntheticCoordinatorOwner", "Name", typeof(string));
        AssertDeclaredProperty(assembly, "SyntheticCoordinatorJob", "RetryCount", typeof(int));
        AssertDeclaredProperty(assembly, "SyntheticCoordinatorShard", "Health", RequireType(assembly, "SyntheticShardHealth"));
        AssertDeclaredProperty(assembly, "SyntheticShardHealth", "State", typeof(string));
        AssertDeclaredProperty(assembly, "SyntheticWorkflowAttempt", "Status", typeof(string));
        AssertDeclaredProperty(assembly, "SyntheticWorkflowAttempt", "Worker", RequireType(assembly, "SyntheticAssignedWorker"));
        AssertDeclaredProperty(assembly, "SyntheticAssignedWorker", "State", typeof(string));
        Assert.NotNull(RequireType(assembly, "SyntheticCoordinatorTask").GetMethod(
            "GetState",
            BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(RequireType(assembly, "SyntheticWorkflowAttempt").GetMethod(
            "GetDisplayStatus",
            BindingFlags.Instance | BindingFlags.NonPublic));
    }

    /// <summary>
    /// Demonstrates that materially different compiler-emitted property profiles violate at least one frozen W6.1
    /// predicate without invoking the future product certificate recognizer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void NegativePropertyProfilesRemainStructurallyDistinct()
    {
        using var stream = File.OpenRead(typeof(W6EmittedShapeFixtureTests).Assembly.Location);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var computed = ReadPropertyFacts(peReader, reader, nameof(W6ComputedPropertyProfile), "Value");
        Assert.True(computed.Code.Length > 7);

        var indexed = ReadPropertyFacts(peReader, reader, nameof(W6IndexedPropertyProfile), "Item");
        Assert.Equal(1, indexed.PropertyParameterCount);

        var @static = ReadPropertyFacts(peReader, reader, nameof(W6StaticPropertyProfile), "Value");
        Assert.True((@static.GetterAttributes & MethodAttributes.Static) != 0);

        var inheritedType = FindType(reader, nameof(W6InheritedPropertyProfile));
        Assert.Empty(reader.GetTypeDefinition(inheritedType).GetProperties());
        Assert.False(reader.GetTypeDefinition(inheritedType).BaseType.IsNil);

        var mismatched = ReadPropertyFacts(peReader, reader, nameof(W6MismatchedFieldPropertyProfile), "Value");
        Assert.Equal(7, mismatched.Code.Length);
        Assert.Equal((byte)0x1C, mismatched.PropertySignature[^1]);
        Assert.Equal((byte)0x0E, mismatched.LoadedFieldSignature[^1]);

        var extra = ReadPropertyFacts(peReader, reader, nameof(W6ExtraInstructionPropertyProfile), "Value");
        Assert.True(extra.Code.Length > 7);

        var call = ReadPropertyFacts(peReader, reader, nameof(W6CallPropertyProfile), "Value");
        Assert.Contains((byte)0x28, call.Code);

        var branch = ReadPropertyFacts(peReader, reader, nameof(W6BranchPropertyProfile), "Value");
        Assert.Contains(branch.Code, IsBranchOpcode);

        var local = ReadPropertyFacts(peReader, reader, nameof(W6LocalPropertyProfile), "Value");
        Assert.False(local.Body.LocalSignature.IsNil);

        var eh = ReadPropertyFacts(peReader, reader, nameof(W6ExceptionRegionPropertyProfile), "Value");
        Assert.NotEmpty(eh.Body.ExceptionRegions);
    }

    /// <summary>
    /// Verifies that the W6.1 scenario draft freezes twenty-four unique target invocations across four materially
    /// different graph families before any W6 evaluator or report schema exists.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void ScenarioDraftFreezesTwentyFourTargetInputsAndExpressions()
    {
        var path = Path.Combine(
            ResolveRepositoryRoot(),
            "tests",
            "corpus",
            "w6-member-chain-target-inputs-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("w6-member-chain-target-inputs-v1", root.GetProperty("scenarioSetId").GetString());
        Assert.Equal("Synthetic", root.GetProperty("corpusKind").GetString());
        Assert.Equal("W6.1TargetShapeFreeze", root.GetProperty("lifecycle").GetString());

        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();
        Assert.Equal(24, scenarios.Length);
        Assert.Equal(24, scenarios.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            24,
            scenarios
                .Select(item => string.Join('\u001F', item.GetProperty("targetArguments").EnumerateArray().Select(argument => argument.GetString())))
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            new[]
            {
                new KeyValuePair<string, int>("BatchPipeline", 6),
                new KeyValuePair<string, int>("CoordinatorPipeline", 6),
                new KeyValuePair<string, int>("RequestPipeline", 7),
                new KeyValuePair<string, int>("WorkflowDispatch", 5),
            },
            scenarios
                .GroupBy(item => item.GetProperty("shape").GetString()!, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .ToArray());

        Assert.All(scenarios, scenario =>
        {
            Assert.StartsWith("w6-", scenario.GetProperty("id").GetString(), StringComparison.Ordinal);
            Assert.StartsWith("root.", scenario.GetProperty("expression").GetString(), StringComparison.Ordinal);
            var arguments = scenario.GetProperty("targetArguments").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Equal(4, arguments.Length);
            Assert.Equal(ExpectedCommand(scenario.GetProperty("shape").GetString()!), arguments[0]);
            Assert.Equal(ExpectedRootType(scenario.GetProperty("shape").GetString()!), scenario.GetProperty("rootType").GetString());
            Assert.True(int.TryParse(arguments[1], out _));
            Assert.True(int.TryParse(arguments[2], out _));
            Assert.Contains(arguments[3], new[] { "clear", "degraded", "failed", "running" });
        });
    }

    /// <summary>
    /// Launches both newly frozen target entry points through the repository's hidden-window harness and proves they
    /// retain their strongly rooted graphs until deterministic cleanup.
    /// </summary>
    /// <param name="command">The versioned target command naming one new graph family.</param>
    /// <param name="state">The state value used to exercise that family's readiness oracle.</param>
    [Theory]
    [InlineData("--synthetic-coordinator-pipeline", "running")]
    [InlineData("--synthetic-workflow-dispatch", "degraded")]
    [Trait("Category", "Fast")]
    public void NewGraphTargetsReachReadinessHeadlessly(string command, string state)
    {
        using var target = TestTargetRunner.StartAndWaitReady(
            TestTargetPaths.ResolveExecutable(),
            [command, "413", "-907", state],
            isolatedDirectory: null);

        Assert.True(target.Pid > 0);
    }

    private static PropertyProjection ReadPropertyProjection(
        PEReader peReader,
        MetadataReader reader,
        string typeName,
        string propertyName)
    {
        var typeHandle = FindType(reader, typeName);
        var declaringType = reader.GetTypeDefinition(typeHandle);
        var propertyHandle = declaringType.GetProperties().Single(handle =>
            reader.GetString(reader.GetPropertyDefinition(handle).Name) == propertyName);
        var property = reader.GetPropertyDefinition(propertyHandle);
        var accessors = property.GetAccessors();
        Assert.False(accessors.Getter.IsNil);
        Assert.False(accessors.Setter.IsNil);
        var getter = reader.GetMethodDefinition(accessors.Getter);
        var setter = reader.GetMethodDefinition(accessors.Setter);
        Assert.Equal(typeHandle, getter.GetDeclaringType());
        Assert.Equal(typeHandle, setter.GetDeclaringType());
        var body = peReader.GetMethodBody(getter.RelativeVirtualAddress);
        var code = body.GetILBytes() ?? throw new InvalidOperationException("The getter body has no IL bytes.");
        Assert.Equal(7, code.Length);
        var fieldEntity = MetadataTokens.EntityHandle(BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(2, sizeof(int))));
        Assert.Equal(HandleKind.FieldDefinition, fieldEntity.Kind);
        var fieldHandle = (FieldDefinitionHandle)fieldEntity;
        var field = reader.GetFieldDefinition(fieldHandle);
        var headerByte = peReader.GetSectionData(getter.RelativeVirtualAddress).GetContent(0, 1)[0];
        return new PropertyProjection(
            declaringType,
            property,
            reader.GetBlobBytes(property.Signature),
            accessors.Getter,
            getter,
            reader.GetString(getter.Name),
            reader.GetBlobBytes(getter.Signature),
            reader.GetString(setter.Name),
            accessors.Others,
            body,
            code,
            headerByte,
            fieldHandle,
            field,
            reader.GetString(field.Name),
            reader.GetBlobBytes(field.Signature));
    }

    private static PropertyFacts ReadPropertyFacts(
        PEReader peReader,
        MetadataReader reader,
        string typeName,
        string propertyName)
    {
        var type = reader.GetTypeDefinition(FindType(reader, typeName));
        var propertyHandle = type.GetProperties().Single(handle =>
            reader.GetString(reader.GetPropertyDefinition(handle).Name) == propertyName);
        var property = reader.GetPropertyDefinition(propertyHandle);
        var propertySignature = reader.GetBlobBytes(property.Signature);
        var getter = reader.GetMethodDefinition(property.GetAccessors().Getter);
        var body = peReader.GetMethodBody(getter.RelativeVirtualAddress);
        var code = body.GetILBytes() ?? throw new InvalidOperationException("The negative getter body has no IL bytes.");
        var loadedFieldSignature = Array.Empty<byte>();
        if (code.Length == 7 && code[0] == LoadArgumentZero && code[1] == LoadInstanceField && code[6] == Return)
        {
            var handle = MetadataTokens.EntityHandle(BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(2, sizeof(int))));
            if (handle.Kind == HandleKind.FieldDefinition)
            {
                loadedFieldSignature = reader.GetBlobBytes(reader.GetFieldDefinition((FieldDefinitionHandle)handle).Signature);
            }
        }

        return new PropertyFacts(
            propertySignature,
            propertySignature[1],
            getter.Attributes,
            body,
            code,
            loadedFieldSignature);
    }

    private static TypeDefinitionHandle FindType(MetadataReader reader, string typeName) =>
        reader.TypeDefinitions.Single(handle =>
        {
            var definition = reader.GetTypeDefinition(handle);
            return reader.GetString(definition.Name) == typeName;
        });

    private static Type RequireType(Assembly assembly, string name) =>
        assembly.GetType(name, throwOnError: true)!;

    private static void AssertDeclaredFields(Type type, params (string Name, Type Type)[] expected)
    {
        var actual = type
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => (field.Name, field.FieldType))
            .ToArray();
        Assert.Equal(expected.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray(), actual);
        Assert.All(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
        {
            Assert.True(field.IsInitOnly);
            Assert.False(field.IsStatic);
        });
    }

    private static void AssertDeclaredProperty(Assembly assembly, string typeName, string propertyName, Type propertyType)
    {
        var property = RequireType(assembly, typeName).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(property);
        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.NotNull(property.SetMethod);
        Assert.Empty(property.GetIndexParameters());
    }

    private static bool IsBranchOpcode(byte opcode) =>
        opcode is >= 0x2B and <= 0x45;

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string ExpectedCommand(string shape) => shape switch
    {
        "RequestPipeline" => "--synthetic-request-pipeline",
        "BatchPipeline" => "--synthetic-batch-pipeline",
        "CoordinatorPipeline" => "--synthetic-coordinator-pipeline",
        "WorkflowDispatch" => "--synthetic-workflow-dispatch",
        _ => throw new InvalidOperationException("The scenario draft contains an unknown graph family."),
    };

    private static string ExpectedRootType(string shape) => shape switch
    {
        "RequestPipeline" => "SyntheticRequestPipelineProbe",
        "BatchPipeline" => "SyntheticBatchPipelineProbe",
        "CoordinatorPipeline" => "SyntheticCoordinatorPipelineProbe",
        "WorkflowDispatch" => "SyntheticWorkflowDispatchProbe",
        _ => throw new InvalidOperationException("The scenario draft contains an unknown graph family."),
    };

    private static string ResolveTargetAssemblyPath() =>
        TestTargetPaths.ResolveAssembly(TestTargetPaths.ResolveExecutable());

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Interpreter.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }

    private enum SignatureType
    {
        String,
        Int32,
    }

    private sealed record ExpectedProperty(string TypeName, string PropertyName, SignatureType Type);

    private sealed record PropertyProjection(
        TypeDefinition DeclaringType,
        PropertyDefinition Property,
        byte[] PropertySignature,
        MethodDefinitionHandle GetterHandle,
        MethodDefinition Getter,
        string GetterName,
        byte[] GetterSignature,
        string SetterName,
        IReadOnlyList<MethodDefinitionHandle> OtherAccessors,
        MethodBodyBlock Body,
        byte[] Code,
        byte HeaderByte,
        FieldDefinitionHandle BackingFieldHandle,
        FieldDefinition BackingField,
        string BackingFieldName,
        byte[] BackingFieldSignature);

    private sealed record PropertyFacts(
        byte[] PropertySignature,
        int PropertyParameterCount,
        MethodAttributes GetterAttributes,
        MethodBodyBlock Body,
        byte[] Code,
        byte[] LoadedFieldSignature);
}

internal sealed class W6ComputedPropertyProfile
{
    private readonly int value = 17;

    internal int Value => checked((value * 3) + 1);
}

internal sealed class W6IndexedPropertyProfile
{
    private readonly string value = "indexed";

    internal string this[int index] => index == 0 ? value : string.Empty;
}

internal static class W6StaticPropertyProfile
{
    internal static int Value => 17;
}

internal class W6InheritedPropertyBaseProfile
{
    internal int Value { get; } = 17;
}

internal sealed class W6InheritedPropertyProfile : W6InheritedPropertyBaseProfile
{
}

internal sealed class W6MismatchedFieldPropertyProfile
{
    private readonly string value = "mismatched";

    internal object Value => value;
}

internal sealed class W6ExtraInstructionPropertyProfile
{
    private readonly int value = 17;

    internal int Value => value ^ 1;
}

internal sealed class W6CallPropertyProfile
{
    private readonly int value = 17;

    internal int Value => Normalize(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Normalize(int input) => input;
}

internal sealed class W6BranchPropertyProfile
{
    private readonly int value = 17;

    internal int Value => value >= 0 ? value : -value;
}

internal sealed class W6LocalPropertyProfile
{
    private readonly int value = 17;

    internal int Value
    {
        get
        {
            Span<int> local = stackalloc int[1];
            local[0] = value;
            return local[0];
        }
    }
}

internal sealed class W6ExceptionRegionPropertyProfile
{
    private readonly int value = 17;

    internal int Value
    {
        get
        {
            try
            {
                return checked(value + 1);
            }
            catch (OverflowException)
            {
                return int.MaxValue;
            }
        }
    }
}
