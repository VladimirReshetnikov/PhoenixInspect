using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises complete cross-catalog TypeDef and MethodDef authority with synthetic metadata modules.</summary>
public sealed class W8DefinitionAuthorityCatalogContractTests
{
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Proves direct and pointer ownership compose with unsorted interleaved GenericParam owners and nested total
    /// arities, including a physical child arity smaller than its parent's total arity.
    /// </summary>
    /// <param name="usePointers">Whether FieldList and MethodList use complete reordered pointer tables.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Fast")]
    public void Exact_authority_composes_complete_direct_and_pointer_catalogs(bool usePointers)
    {
        var scenario = BuildScenario(usePointers: usePointers);
        var result = scenario.Authority;

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, scenario.TypeDefinitions.ResultKind);
        Assert.Equal(MetadataNestedClassTableResultKind.Exact, scenario.NestedClasses.ResultKind);
        Assert.Equal(
            MetadataGenericParameterPhysicalTableResultKind.Exact,
            scenario.GenericParameters.ResultKind);
        Assert.Equal(MetadataGenericParameterPhysicalOrderProfile.Unsorted,
            scenario.GenericParameters.OrderProfile);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, scenario.MethodDefinitions.ResultKind);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, result.ResultKind);
        Assert.Equal(MetadataDefinitionAuthorityIssue.None, result.Issue);
        Assert.Null(result.ReachedBound);
        Assert.Null(result.RelatedMetadataToken);
        Assert.Equal(0, result.ObservedCount);
        Assert.Equal(scenario.SourceEnds, result.SourceEnds);
        Assert.Equal(scenario.TypeDefinitions, result.TypeDefinitionCatalog);
        Assert.Equal(scenario.NestedClasses, result.NestedClassCatalog);
        Assert.Equal(scenario.GenericParameters, result.GenericParameterCatalog);
        Assert.Equal(scenario.MethodDefinitions, result.MethodDefinitionCatalog);
        Assert.Same(result.TypeDefinitions[0], result.EssentialModuleTypeDefinition);
        Assert.Same(result.TypeDefinitions[3], result.ExactTypeDefinitionOrDefault(TypeToken(4)));
        Assert.Same(result.MethodDefinitions[2], result.ExactMethodDefinitionOrDefault(MethodToken(3)));
        Assert.Null(result.ExactTypeDefinitionOrDefault(MethodToken(1)));
        Assert.Null(result.ExactTypeDefinitionOrDefault(TypeToken(6)));
        Assert.Null(result.ExactMethodDefinitionOrDefault(TypeToken(1)));
        Assert.Null(result.ExactMethodDefinitionOrDefault(MethodToken(6)));

        Assert.Equal([0, 2, 2, 3, 1],
            result.TypeDefinitions.Select(static type => type.TotalGenericArity).ToArray());
        Assert.Equal([null, null, TypeToken(2), TypeToken(3), TypeToken(2)],
            result.TypeDefinitions.Select(static type => type.EnclosingTypeDefinitionToken).ToArray());
        Assert.Equal([0, 0, 1, 2, 1],
            result.TypeDefinitions.Select(static type => type.NestingDepth).ToArray());
        Assert.Equal([false, false, true, true, true],
            result.TypeDefinitions.Select(static type => type.IsNested).ToArray());
        Assert.Equal(["O0", "O1"],
            result.TypeDefinitions[1].GenericParameters.Select(static row => row.Name).ToArray());
        Assert.Equal(["RenamedI0", "RenamedI1"],
            result.TypeDefinitions[2].GenericParameters.Select(static row => row.Name).ToArray());
        Assert.Equal([0, 1, 2],
            result.TypeDefinitions[3].GenericParameters.Select(static row => row.Number).ToArray());

        Assert.True(result.TypeDefinitions[4].TotalGenericArity < result.TypeDefinitions[1].TotalGenericArity);
        Assert.Equal("UnderflowShape", result.TypeDefinitions[4].TypeName);
        Assert.Null(typeof(MetadataTypeDefinitionAuthorityIdentity).GetProperty("IntroducedGenericArity"));
        Assert.Null(typeof(MetadataTypeDefinitionAuthorityIdentity).GetProperty("RoslynName"));

        var expectedFieldTokens = usePointers
            ? new[] { FieldToken(4), FieldToken(1), FieldToken(3), FieldToken(2) }
            : new[] { FieldToken(1), FieldToken(2), FieldToken(3), FieldToken(4) };
        Assert.Equal(expectedFieldTokens,
            result.TypeDefinitions.Take(4).SelectMany(static type => type.FieldDefinitionTokens).ToArray());
        Assert.Empty(result.TypeDefinitions[4].FieldDefinitionTokens);

        var expectedTypeOwnedMethods = usePointers
            ? new[] { MethodToken(5), MethodToken(2), MethodToken(1), MethodToken(4), MethodToken(3) }
            : new[] { MethodToken(1), MethodToken(2), MethodToken(3), MethodToken(4), MethodToken(5) };
        Assert.Equal(expectedTypeOwnedMethods,
            result.TypeDefinitions.SelectMany(static type => type.MethodDefinitionTokens).ToArray());
        Assert.NotEmpty(result.TypeDefinitions[0].FieldDefinitionTokens);
        Assert.NotEmpty(result.TypeDefinitions[0].MethodDefinitionTokens);

        Assert.Equal([1, 2, 1, 0, 0],
            result.MethodDefinitions.Select(static method => method.DeclaredGenericArity).ToArray());
        Assert.Equal(
            usePointers
                ? [TypeToken(3), TypeToken(2), TypeToken(5), TypeToken(4), TypeToken(1)]
                : [TypeToken(1), TypeToken(2), TypeToken(3), TypeToken(4), TypeToken(5)],
            result.MethodDefinitions.Select(static method => method.DeclaringTypeDefinitionToken).ToArray());
        Assert.All(result.MethodDefinitions, method =>
        {
            Assert.Equal(method.TableRow.DeclaringTypeDefinitionToken, method.DeclaringTypeDefinitionToken);
            Assert.Equal(method.SourceEnds, method.DeclaringTypeDefinition.SourceEnds);
            Assert.Equal(method.DeclaredGenericArity, method.GenericParameters.Length);
            Assert.Equal(Enumerable.Range(0, method.DeclaredGenericArity),
                method.GenericParameters.Select(static row => row.Number));
            var nestedGenericParameterBytes = method.GenericParameters.Sum(
                static row => row.CanonicalBytes.Length);
            Assert.True(
                method.CanonicalBytes.Length <=
                method.TableRow.CanonicalBytes.Length + nestedGenericParameterBytes + 256,
                "A MethodDef authority row must retain its declaring TypeDef through a fixed-size content reference.");
        });
    }

    /// <summary>Proves every essential module pseudo-type profile contradiction remains typed and prefix-free.</summary>
    /// <param name="mutation">The one synthetic module-row contradiction to introduce.</param>
    /// <param name="expectedIssue">The exact authority issue expected for that contradiction.</param>
    [Theory]
    [InlineData(ModuleMutation.Name, MetadataDefinitionAuthorityIssue.ModuleTypeNameMismatch)]
    [InlineData(ModuleMutation.Namespace, MetadataDefinitionAuthorityIssue.ModuleTypeNamespaceMismatch)]
    [InlineData(ModuleMutation.Extends, MetadataDefinitionAuthorityIssue.ModuleTypeExtendsMismatch)]
    [InlineData(ModuleMutation.Visibility, MetadataDefinitionAuthorityIssue.ModuleTypeVisibilityMismatch)]
    [InlineData(ModuleMutation.ClassSemantics, MetadataDefinitionAuthorityIssue.ModuleTypeClassSemanticsMismatch)]
    [InlineData(ModuleMutation.Nesting, MetadataDefinitionAuthorityIssue.ModuleTypeNestingMismatch)]
    [InlineData(ModuleMutation.GenericParameter, MetadataDefinitionAuthorityIssue.ModuleTypeGenericParameterMismatch)]
    [Trait("Category", "Fast")]
    public void Module_profile_contradictions_are_typed_and_prefix_free(
        ModuleMutation mutation,
        MetadataDefinitionAuthorityIssue expectedIssue)
    {
        var scenario = BuildScenario(usePointers: true, moduleMutation: mutation);

        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, scenario.TypeDefinitions.ResultKind);
        Assert.Equal(MetadataNestedClassTableResultKind.Exact, scenario.NestedClasses.ResultKind);
        Assert.Equal(
            MetadataGenericParameterPhysicalTableResultKind.Exact,
            scenario.GenericParameters.ResultKind);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, scenario.MethodDefinitions.ResultKind);
        AssertInvalid(scenario.Authority, expectedIssue, TypeToken(1));
    }

    /// <summary>Proves a signature/GenericParam arity disagreement invalidates the whole authority join.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Method_signature_arity_requires_the_complete_physical_owner_group()
    {
        var scenario = BuildScenario(usePointers: true, secondMethodSignatureArity: 3);

        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, scenario.MethodDefinitions.ResultKind);
        Assert.Equal(3, scenario.MethodDefinitions.Rows[1].DeclaredGenericArity);
        Assert.Equal(2, scenario.GenericParameters.RowsForOwnerOrEmpty(
            MetadataGenericParameterOwnerKind.MethodDefinition,
            MethodToken(2)).Length);
        AssertInvalid(
            scenario.Authority,
            MetadataDefinitionAuthorityIssue.MethodDefinitionGenericParameterArityMismatch,
            MethodToken(2));
        Assert.Equal(2, scenario.Authority.ObservedCount);
    }

    /// <summary>Proves different source ends and a non-exact prerequisite never yield an authority-row prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Source_and_prerequisite_stops_are_typed_and_prefix_free()
    {
        var first = BuildScenario(usePointers: false);
        var second = BuildScenario(
            usePointers: false,
            module: CreateMetadataModule(moduleAddress: 0xB000, digestCharacter: 'b'));

        var sourceMismatch = MetadataDefinitionAuthorityCatalogIdentity.Create(
            first.TypeDefinitions,
            first.NestedClasses,
            second.GenericParameters,
            first.MethodDefinitions);
        AssertInvalid(
            sourceMismatch,
            MetadataDefinitionAuthorityIssue.GenericParameterSourceMismatch,
            relatedMetadataToken: null);

        var incomplete = BuildScenario(usePointers: false, omitGenericParameterObservations: true);
        Assert.Equal(
            MetadataGenericParameterPhysicalTableResultKind.NonExact,
            incomplete.GenericParameters.ResultKind);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.NonExact, incomplete.Authority.ResultKind);
        Assert.Equal(
            MetadataDefinitionAuthorityIssue.GenericParameterCatalogNonExact,
            incomplete.Authority.Issue);
        Assert.Empty(incomplete.Authority.TypeDefinitions);
        Assert.Empty(incomplete.Authority.MethodDefinitions);
        Assert.Null(incomplete.Authority.EssentialModuleTypeDefinition);
        Assert.Null(incomplete.Authority.ExactTypeDefinitionOrDefault(TypeToken(1)));
        Assert.Null(incomplete.Authority.ExactMethodDefinitionOrDefault(MethodToken(1)));
        Assert.Null(incomplete.Authority.ReachedBound);
        Assert.Null(incomplete.Authority.RelatedMetadataToken);
        Assert.Equal(0, incomplete.Authority.ObservedCount);
    }

    /// <summary>Proves canonical replay, defensive copies, and catalog-only authority issuance.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Authority_rows_are_guarded_immutable_and_canonically_replayable()
    {
        var first = BuildScenario(usePointers: true).Authority;
        var replay = BuildScenario(usePointers: true).Authority;
        var originalSha = first.Sha256;
        var originalBytes = first.CanonicalBytes;

        var returnedTypes = first.TypeDefinitions;
        ImmutableCollectionsMarshal.AsArray(returnedTypes)![0] = returnedTypes[^1];
        var returnedMethods = first.MethodDefinitions;
        ImmutableCollectionsMarshal.AsArray(returnedMethods)![0] = returnedMethods[^1];
        var returnedGenericParameters = first.TypeDefinitions[1].GenericParameters;
        ImmutableCollectionsMarshal.AsArray(returnedGenericParameters)![0] = returnedGenericParameters[^1];
        var returnedFields = first.TypeDefinitions[0].FieldDefinitionTokens;
        ImmutableCollectionsMarshal.AsArray(returnedFields)![0] = FieldToken(2);
        var returnedBytes = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;

        Assert.Equal(replay, first);
        Assert.Equal(replay.GetHashCode(), first.GetHashCode());
        Assert.Equal(originalSha, first.Sha256);
        Assert.True(originalBytes.AsSpan().SequenceEqual(first.CanonicalBytes.AsSpan()));
        Assert.Equal(TypeToken(1), first.TypeDefinitions[0].TypeDefinitionToken);
        Assert.Equal(MethodToken(1), first.MethodDefinitions[0].MethodDefinitionToken);
        Assert.Equal("O0", first.TypeDefinitions[1].GenericParameters[0].Name);
        Assert.Equal(FieldToken(4), first.TypeDefinitions[0].FieldDefinitionTokens[0]);

        var typeIdentity = typeof(MetadataTypeDefinitionAuthorityIdentity);
        var methodIdentity = typeof(MetadataMethodDefinitionAuthorityIdentity);
        var catalogIdentity = typeof(MetadataDefinitionAuthorityCatalogIdentity);
        Assert.Empty(typeIdentity.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(methodIdentity.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(catalogIdentity.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeIdentity.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Empty(methodIdentity.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Create"],
            catalogIdentity.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name).ToArray());
        Assert.Throws<ArgumentException>(() => MetadataTypeDefinitionAuthorityIdentity.Create(
            new object(),
            null!,
            enclosingTypeDefinitionToken: null,
            nestingDepth: 0,
            genericParameters: default));
        Assert.Throws<ArgumentException>(() => MetadataMethodDefinitionAuthorityIdentity.Create(
            new object(),
            null!,
            null!,
            genericParameters: default));
        Assert.False(MetadataDefinitionAuthorityCatalogIdentity.OwnsTypeRowMintCapability(new object()));
        Assert.False(MetadataDefinitionAuthorityCatalogIdentity.OwnsMethodRowMintCapability(new object()));
        Assert.All(new[] { typeIdentity, methodIdentity, catalogIdentity }, static type => Assert.True(type.IsSealed));
    }

    /// <summary>Proves every new public authority draft type and method has emitted XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Definition_authority_public_surface_has_draft_XML()
    {
        var assembly = typeof(MetadataDefinitionAuthorityCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataDefinitionAuthorityResultKind),
            typeof(MetadataDefinitionAuthorityIssue),
            typeof(MetadataTypeDefinitionAuthorityIdentity),
            typeof(MetadataMethodDefinitionAuthorityIdentity),
            typeof(MetadataDefinitionAuthorityCatalogIdentity),
        };

        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.Contains("draft", typeDocumentation.Value, StringComparison.OrdinalIgnoreCase);

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
                    Assert.Contains("draft", member.Value, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static Scenario BuildScenario(
        bool usePointers,
        ModuleMutation moduleMutation = ModuleMutation.None,
        int secondMethodSignatureArity = 2,
        bool omitGenericParameterObservations = false,
        StaticFieldMetadataModuleIdentity? module = null)
    {
        module ??= CreateMetadataModule();
        var genericObservations = GenericParameterRows(module, moduleMutation);
        var nestedObservations = NestedRows(module, moduleMutation);
        var sourceEnds = CreateSourceEnds(
            module,
            typeDefinitionRows: 5,
            fieldDefinitionRows: 4,
            fieldPointerRows: usePointers ? 4 : 0,
            methodDefinitionRows: 5,
            methodPointerRows: usePointers ? 5 : 0,
            parameterDefinitionRows: 5,
            parameterPointerRows: 0,
            nestedClassRows: nestedObservations.Length,
            genericParameterRows: genericObservations.Length);
        var memberPointers = MetadataMemberPointerTableCatalogIdentity.Create(
            sourceEnds,
            usePointers ? PointerRows(module, MetadataMemberPointerTableKind.Field, [4, 1, 3, 2]) : default,
            usePointers ? PointerRows(module, MetadataMemberPointerTableKind.Method, [5, 2, 1, 4, 3]) : default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            TypeDefinitionRows(module, moduleMutation),
            memberPointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            nestedObservations);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            omitGenericParameterObservations ? default : genericObservations);
        var methodDefinitions = MetadataMethodDefinitionTableCatalogIdentity.Create(
            typeDefinitions,
            MethodDefinitionRows(module, secondMethodSignatureArity));
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions);
        return new Scenario(
            sourceEnds,
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methodDefinitions,
            authority);
    }

    internal static MetadataDefinitionAuthorityCatalogIdentity BuildCompatibilityAuthority(bool usePointers) =>
        BuildScenario(usePointers).Authority;

    private static ImmutableArray<MetadataTypeDefinitionRowObservationIdentity> TypeDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        ModuleMutation mutation)
    {
        var moduleName = mutation == ModuleMutation.Name ? "NotTheModuleType" : "<Module>";
        var moduleNamespace = mutation == ModuleMutation.Namespace ? "Synthetic.BadNamespace" : string.Empty;
        var moduleAttributes = mutation switch
        {
            ModuleMutation.Visibility => TypeAttributes.Public,
            ModuleMutation.ClassSemantics => TypeAttributes.NotPublic | TypeAttributes.Interface | TypeAttributes.Abstract,
            ModuleMutation.Nesting => TypeAttributes.NestedPublic,
            _ => TypeAttributes.NotPublic,
        };
        var moduleExtends = mutation == ModuleMutation.Extends ? TypeToken(2) : (int?)null;

        return
        [
            TypeRow(module, 1, 1, 1, moduleNamespace, moduleName, moduleAttributes, moduleExtends),
            TypeRow(module, 2, 2, 2, "Synthetic.Authority", "Outer`2", TypeAttributes.Public, null),
            TypeRow(module, 3, 3, 3, string.Empty, "Inner", TypeAttributes.NestedPublic, null),
            TypeRow(module, 4, 4, 4, string.Empty, "Leaf`1", TypeAttributes.NestedPublic, null),
            TypeRow(module, 5, 5, 5, string.Empty, "UnderflowShape", TypeAttributes.NestedPublic, null),
        ];
    }

    private static MetadataTypeDefinitionRowObservationIdentity TypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int fieldStart,
        int methodStart,
        string namespaceName,
        string typeName,
        TypeAttributes attributes,
        int? extendsToken) =>
        MetadataTypeDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            typeDefinitionToken: TypeToken(rowId),
            fieldListRowId: fieldStart,
            methodListRowId: methodStart,
            namespaceName: namespaceName,
            typeName: typeName,
            typeAttributes: (int)attributes,
            extendsMetadataToken: extendsToken);

    private static ImmutableArray<MetadataNestedClassRowObservationIdentity> NestedRows(
        StaticFieldMetadataModuleIdentity module,
        ModuleMutation mutation)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataNestedClassRowObservationIdentity>();
        if (mutation == ModuleMutation.Nesting)
        {
            builder.Add(NestedRow(module, physicalRowId: 1, nestedTypeRowId: 1, enclosingTypeRowId: 2));
        }
        builder.Add(NestedRow(module, builder.Count + 1, nestedTypeRowId: 3, enclosingTypeRowId: 2));
        builder.Add(NestedRow(module, builder.Count + 1, nestedTypeRowId: 4, enclosingTypeRowId: 3));
        builder.Add(NestedRow(module, builder.Count + 1, nestedTypeRowId: 5, enclosingTypeRowId: 2));
        return builder.ToImmutable();
    }

    private static MetadataNestedClassRowObservationIdentity NestedRow(
        StaticFieldMetadataModuleIdentity module,
        int physicalRowId,
        int nestedTypeRowId,
        int enclosingTypeRowId) =>
        MetadataNestedClassRowObservationIdentity.Create(
            metadataModule: module,
            nestedClassRowToken: 0x29000000 | physicalRowId,
            nestedTypeDefinitionToken: TypeToken(nestedTypeRowId),
            enclosingTypeDefinitionToken: TypeToken(enclosingTypeRowId));

    private static ImmutableArray<MetadataGenericParameterRowObservationIdentity> GenericParameterRows(
        StaticFieldMetadataModuleIdentity module,
        ModuleMutation mutation)
    {
        var rows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        void Add(int ownerToken, int number, string name, int flags = 0) =>
            rows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                metadataModule: module,
                genericParameterToken: 0x2A000000 | checked(rows.Count + 1),
                number: number,
                flags: flags,
                ownerMetadataToken: ownerToken,
                name: name));

        Add(TypeToken(4), 2, "L2", flags: 0x20);
        Add(MethodToken(2), 1, "M2_1");
        Add(TypeToken(2), 1, "O1");
        Add(MethodToken(3), 0, "M3_0");
        Add(TypeToken(3), 0, "RenamedI0");
        Add(TypeToken(5), 0, "U0");
        Add(MethodToken(1), 0, "M1_0");
        Add(TypeToken(4), 0, "L0");
        Add(MethodToken(2), 0, "M2_0");
        Add(TypeToken(2), 0, "O0");
        Add(TypeToken(3), 1, "RenamedI1");
        Add(TypeToken(4), 1, "L1");
        if (mutation == ModuleMutation.GenericParameter)
        {
            Add(TypeToken(1), 0, "GlobalT");
        }
        return rows.ToImmutable();
    }

    private static ImmutableArray<MetadataMethodDefinitionRowObservationIdentity> MethodDefinitionRows(
        StaticFieldMetadataModuleIdentity module,
        int secondMethodSignatureArity) =>
        [
            MethodRow(module, 1, isStatic: true, [0x10, 0x01, 0x00, 0x01]),
            MethodRow(
                module,
                2,
                isStatic: true,
                [0x10, checked((byte)secondMethodSignatureArity), 0x01, 0x01, 0x1E, 0x00]),
            MethodRow(module, 3, isStatic: false, [0x30, 0x01, 0x00, 0x01]),
            MethodRow(module, 4, isStatic: false, [0x20, 0x00, 0x01]),
            MethodRow(module, 5, isStatic: true, [0x00, 0x00, 0x01]),
        ];

    private static MetadataMethodDefinitionRowObservationIdentity MethodRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        bool isStatic,
        ImmutableArray<byte> signature) =>
        MetadataMethodDefinitionRowObservationIdentity.Create(
            metadataModule: module,
            methodDefinitionToken: MethodToken(rowId),
            relativeVirtualAddress: 0x2000 + rowId * 0x30,
            implementationAttributes: (int)(MethodImplAttributes.NoInlining | MethodImplAttributes.NoOptimization),
            attributes: (int)(MethodAttributes.Public | MethodAttributes.HideBySig |
                              (isStatic ? MethodAttributes.Static : default)),
            name: $"AuthorityMethod{rowId}",
            signaturePrefixBytes: signature,
            signatureByteCount: signature.Length,
            parameterListRowId: rowId);

    private static ImmutableArray<MetadataMemberPointerRowObservationIdentity> PointerRows(
        StaticFieldMetadataModuleIdentity module,
        MetadataMemberPointerTableKind tableKind,
        ImmutableArray<int> targetRowIds)
    {
        var pointerTable = tableKind == MetadataMemberPointerTableKind.Field ? 0x03 : 0x05;
        var definitionTable = tableKind == MetadataMemberPointerTableKind.Field ? 0x04 : 0x06;
        var rows = ImmutableArray.CreateBuilder<MetadataMemberPointerRowObservationIdentity>(targetRowIds.Length);
        for (var index = 0; index < targetRowIds.Length; index++)
        {
            rows.Add(MetadataMemberPointerRowObservationIdentity.Create(
                metadataModule: module,
                pointerMetadataToken: pointerTable << 24 | checked(index + 1),
                targetDefinitionMetadataToken: definitionTable << 24 | targetRowIds[index]));
        }
        return rows.ToImmutable();
    }

    private static MetadataSourceEndIdentity CreateSourceEnds(
        StaticFieldMetadataModuleIdentity module,
        int typeDefinitionRows,
        int fieldDefinitionRows,
        int fieldPointerRows,
        int methodDefinitionRows,
        int methodPointerRows,
        int parameterDefinitionRows,
        int parameterPointerRows,
        int nestedClassRows,
        int genericParameterRows) =>
        MetadataSourceEndIdentity.Create(
            sourceModule: module,
            sourceModuleFact: StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: typeDefinitionRows,
                fieldDefinitionsExamined: fieldDefinitionRows,
                typeDefinitionRowCount: typeDefinitionRows,
                fieldDefinitionRowCount: fieldDefinitionRows,
                typeReferenceRowCount: 0,
                typeSpecificationRowCount: 0,
                assemblyReferenceRowCount: 0,
                methodDefinitionRowCount: methodDefinitionRows,
                parameterDefinitionRowCount: parameterDefinitionRows,
                propertyDefinitionRowCount: 0,
                eventDefinitionRowCount: 0,
                moduleDefinitionRowCount: 1,
                assemblyDefinitionRowCount: 1,
                interfaceImplementationRowCount: 0,
                memberReferenceRowCount: 0,
                customAttributeRowCount: 0,
                moduleReferenceRowCount: 0,
                fileRowCount: 0,
                exportedTypeRowCount: 0,
                nestedClassRowCount: nestedClassRows,
                genericParameterRowCount: genericParameterRows,
                genericParameterConstraintRowCount: 0,
                fieldPointerRowCount: fieldPointerRows,
                methodPointerRowCount: methodPointerRows,
                parameterPointerRowCount: parameterPointerRows));

    private static StaticFieldMetadataModuleIdentity CreateMetadataModule(
        ulong moduleAddress = 0xA000,
        char digestCharacter = 'a')
    {
        var module = StaticFieldModuleInstanceIdentity.Create(
            SnapshotDigest,
            sizeof(ulong),
            applicationDomainAddress: 0x1000,
            moduleAddress: moduleAddress,
            imageBase: 0x400000 + moduleAddress,
            imageSize: 0x20000);
        var content = ModuleContentIdentity.FromDigest(
            mvid: Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
            metadataLength: 32_768,
            metadataSha256: new string(digestCharacter, 64));
        var moduleDefinition = StaticFieldModuleDefinitionIdentity.Create(
            generation: 0,
            name: $"definition-authority-{moduleAddress:x}.dll",
            mvid: content.Mvid,
            encId: Guid.Empty,
            encBaseId: Guid.Empty);
        var assemblyDefinition = StaticFieldAssemblyDefinitionIdentity.Create(
            name: "Synthetic.DefinitionAuthority",
            majorVersion: 1,
            minorVersion: 0,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            hashAlgorithm: 0x8004,
            publicKey: ImmutableArray<byte>.Empty);
        var assembly = StaticFieldContainingAssemblyIdentity.Create(
            module,
            content,
            moduleDefinition,
            assemblyDefinition);
        return StaticFieldMetadataModuleIdentity.ForManifestModule(
            module,
            content,
            moduleDefinition,
            assembly);
    }

    private static void AssertInvalid(
        MetadataDefinitionAuthorityCatalogIdentity result,
        MetadataDefinitionAuthorityIssue issue,
        int? relatedMetadataToken)
    {
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Invalid, result.ResultKind);
        Assert.Equal(issue, result.Issue);
        Assert.Empty(result.TypeDefinitions);
        Assert.Empty(result.MethodDefinitions);
        Assert.Null(result.EssentialModuleTypeDefinition);
        Assert.Null(result.ExactTypeDefinitionOrDefault(TypeToken(1)));
        Assert.Null(result.ExactMethodDefinitionOrDefault(MethodToken(1)));
        Assert.Null(result.ReachedBound);
        Assert.Equal(relatedMetadataToken, result.RelatedMetadataToken);
    }

    private static int TypeToken(int rowId) => 0x02000000 | rowId;

    private static int FieldToken(int rowId) => 0x04000000 | rowId;

    private static int MethodToken(int rowId) => 0x06000000 | rowId;

    /// <summary>Identifies one synthetic essential module-row profile mutation used by the draft contract tests.</summary>
    public enum ModuleMutation
    {
        /// <summary>Leaves the essential module-row profile unchanged.</summary>
        None,

        /// <summary>Changes the required module pseudo-type name.</summary>
        Name,

        /// <summary>Changes the required empty namespace.</summary>
        Namespace,

        /// <summary>Adds a non-nil Extends token.</summary>
        Extends,

        /// <summary>Changes top-level NotPublic visibility.</summary>
        Visibility,

        /// <summary>Changes class semantics to interface semantics.</summary>
        ClassSemantics,

        /// <summary>Adds an exact NestedClass parent relation for TypeDef RID one.</summary>
        Nesting,

        /// <summary>Adds a TypeDef-owned GenericParam row to TypeDef RID one.</summary>
        GenericParameter,
    }

    private sealed record Scenario(
        MetadataSourceEndIdentity SourceEnds,
        MetadataTypeDefinitionTableCatalogIdentity TypeDefinitions,
        MetadataNestedClassTableCatalogIdentity NestedClasses,
        MetadataGenericParameterPhysicalTableCatalogIdentity GenericParameters,
        MetadataMethodDefinitionTableCatalogIdentity MethodDefinitions,
        MetadataDefinitionAuthorityCatalogIdentity Authority);
}
