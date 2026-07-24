using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the six complete physical reference-table catalogs and their shared source ends.</summary>
public sealed class W8MetadataReferencePhysicalTableContractTests
{
    private const int TypeReferenceRowCount = 2;
    private const int ModuleReferenceRowCount = 1;
    private const int TypeSpecificationRowCount = 1;
    private const int AssemblyReferenceRowCount = 1;
    private const int FileRowCount = 1;
    private const int ExportedTypeRowCount = 2;
    private const int ForwarderTypeAttribute = 0x0020_0000;

    /// <summary>
    /// Proves the reference source ends extend one exact definition source end with all six reference tables,
    /// classify in-range and out-of-range tokens per table, and replay canonically.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reference_source_ends_extend_definition_ends_with_exact_reference_tables()
    {
        var sourceEnds = CreateReferenceSourceEnds();
        var replay = CreateReferenceSourceEnds();

        Assert.Equal(TypeReferenceRowCount, sourceEnds.TypeReferenceRowCount);
        Assert.Equal(ModuleReferenceRowCount, sourceEnds.ModuleReferenceRowCount);
        Assert.Equal(TypeSpecificationRowCount, sourceEnds.TypeSpecificationRowCount);
        Assert.Equal(AssemblyReferenceRowCount, sourceEnds.AssemblyReferenceRowCount);
        Assert.Equal(FileRowCount, sourceEnds.FileRowCount);
        Assert.Equal(ExportedTypeRowCount, sourceEnds.ExportedTypeRowCount);
        Assert.Equal(sourceEnds.DefinitionSourceEnds.SourceModule, sourceEnds.SourceModule);

        Assert.True(sourceEnds.ContainsReferenceToken(0x0100_0001));
        Assert.True(sourceEnds.ContainsReferenceToken(0x0100_0002));
        Assert.False(sourceEnds.ContainsReferenceToken(0x0100_0003));
        Assert.True(sourceEnds.ContainsReferenceToken(0x1A00_0001));
        Assert.False(sourceEnds.ContainsReferenceToken(0x1A00_0002));
        Assert.True(sourceEnds.ContainsReferenceToken(0x1B00_0001));
        Assert.False(sourceEnds.ContainsReferenceToken(0x1B00_0002));
        Assert.True(sourceEnds.ContainsReferenceToken(0x2300_0001));
        Assert.False(sourceEnds.ContainsReferenceToken(0x2300_0002));
        Assert.True(sourceEnds.ContainsReferenceToken(0x2600_0001));
        Assert.False(sourceEnds.ContainsReferenceToken(0x2600_0002));
        Assert.True(sourceEnds.ContainsReferenceToken(0x2700_0002));
        Assert.False(sourceEnds.ContainsReferenceToken(0x2700_0003));
        Assert.False(sourceEnds.ContainsReferenceToken(0x0100_0000));
        Assert.False(sourceEnds.ContainsReferenceToken(0x0200_0001));
        Assert.False(sourceEnds.ContainsReferenceToken(0x0000_0001));

        Assert.Equal(sourceEnds, replay);
        Assert.Equal(sourceEnds.GetHashCode(), replay.GetHashCode());
        Assert.Equal(sourceEnds.Sha256, replay.Sha256);
        Assert.True(sourceEnds.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));

        var wider = CreateReferenceSourceEnds(exportedTypeRowCount: ExportedTypeRowCount + 1);
        Assert.NotEqual(sourceEnds, wider);
        Assert.NotEqual(sourceEnds.Sha256, wider.Sha256);
        Assert.Throws<ArgumentNullException>(static () => MetadataReferenceSourceEndIdentity.Create(null!));
    }

    /// <summary>
    /// Proves all six complete reference tables issue guarded RID-ordered rows over one exact source end,
    /// including Module, TypeRef, ModuleRef, and AssemblyRef ResolutionScopes, File and AssemblyRef and enclosing
    /// ExportedType Implementation forms, exact token lookup, W7 candidate comparison, and canonical replay.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_reference_tables_issue_guarded_rows_with_cross_table_scope_checks()
    {
        var tables = BuildExactReferenceTables();
        var replay = BuildExactReferenceTables();

        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.TypeReferences.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.ModuleReferences.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.TypeSpecifications.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.AssemblyReferences.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.Files.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, tables.ExportedTypes.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.None, tables.ExportedTypes.Issue);

        Assert.Equal(TypeReferenceRowCount, tables.TypeReferences.Rows.Length);
        Assert.Single(tables.ModuleReferences.Rows);
        Assert.Single(tables.TypeSpecifications.Rows);
        Assert.Single(tables.AssemblyReferences.Rows);
        Assert.Single(tables.Files.Rows);
        Assert.Equal(ExportedTypeRowCount, tables.ExportedTypes.Rows.Length);

        var moduleScoped = Assert.IsType<MetadataTypeReferencePhysicalRowIdentity>(
            tables.TypeReferences.FindRow(0x0100_0001));
        Assert.Equal(0x0000_0001, moduleScoped.Observation.ResolutionScopeMetadataToken);
        Assert.Equal("System", moduleScoped.Observation.NamespaceName);
        Assert.Equal("Object", moduleScoped.Observation.TypeName);
        var assemblyScoped = Assert.IsType<MetadataTypeReferencePhysicalRowIdentity>(
            tables.TypeReferences.FindRow(0x0100_0002));
        Assert.Equal(0x2300_0001, assemblyScoped.Observation.ResolutionScopeMetadataToken);
        Assert.Equal(tables.SourceEnds, assemblyScoped.SourceEnds);
        Assert.Null(tables.TypeReferences.FindRow(0x0100_0003));
        Assert.Null(tables.TypeReferences.FindRow(0x0100_0000));
        Assert.Null(tables.TypeReferences.FindRow(0x0200_0001));

        var moduleReference = Assert.IsType<MetadataModuleReferencePhysicalRowIdentity>(
            tables.ModuleReferences.FindRow(0x1A00_0001));
        Assert.Equal("native-companion.dll", moduleReference.Observation.Name);
        Assert.Null(tables.ModuleReferences.FindRow(0x1A00_0002));

        var typeSpecification = Assert.IsType<MetadataTypeSpecificationPhysicalRowIdentity>(
            tables.TypeSpecifications.FindRow(0x1B00_0001));
        Assert.True(TypeSpecificationBlob.AsSpan().SequenceEqual(
            typeSpecification.Observation.SignatureBytes.AsSpan()));
        Assert.Null(tables.TypeSpecifications.FindRow(0x1B00_0002));

        var assemblyReference = Assert.IsType<MetadataAssemblyReferencePhysicalRowIdentity>(
            tables.AssemblyReferences.FindRow(0x2300_0001));
        Assert.Equal("Synthetic.CompilerNameMapping", assemblyReference.Observation.Name);
        var matchingCandidate = CreateAssemblyReferenceCandidate();
        Assert.True(assemblyReference.Observation.MatchesCandidate(matchingCandidate));
        Assert.False(assemblyReference.Observation.MatchesCandidate(
            CreateAssemblyReferenceCandidate(minorVersion: 9)));

        var file = Assert.IsType<MetadataAssemblyFilePhysicalRowIdentity>(tables.Files.FindRow(0x2600_0001));
        Assert.True(file.Observation.ContainsMetadata);
        var fileCandidate = StaticFieldAssemblyFileIdentity.Create(
            fileToken: 0x2600_0001,
            flags: 0,
            name: "companion.netmodule",
            hashValue: FileHashBlob);
        Assert.True(file.Observation.MatchesCandidate(fileCandidate));
        Assert.False(file.Observation.MatchesCandidate(StaticFieldAssemblyFileIdentity.Create(
            fileToken: 0x2600_0001,
            flags: 0,
            name: "renamed.netmodule",
            hashValue: FileHashBlob)));

        var forwarded = Assert.IsType<MetadataExportedTypePhysicalRowIdentity>(
            tables.ExportedTypes.FindRow(0x2700_0001));
        Assert.Equal(0x2300_0001, forwarded.Observation.ImplementationMetadataToken);
        var sourceAssembly = tables.SourceEnds.SourceModule.ContainingAssembly;
        var forwarderCandidate = StaticFieldExportedTypeForwarderIdentity.Create(
            sourceAssembly,
            exportedTypeToken: 0x2700_0001,
            typeAttributes: ForwarderTypeAttribute,
            typeDefinitionId: 0,
            namespaceName: "Synthetic.Forwarded",
            typeName: "Relocated",
            implementationAssemblyReference: matchingCandidate,
            targetAssembly: sourceAssembly);
        Assert.True(forwarded.Observation.MatchesCandidate(forwarderCandidate));
        var nested = Assert.IsType<MetadataExportedTypePhysicalRowIdentity>(
            tables.ExportedTypes.FindRow(0x2700_0002));
        Assert.Equal(0x2700_0001, nested.Observation.ImplementationMetadataToken);
        Assert.False(nested.Observation.MatchesCandidate(forwarderCandidate));

        AssertCatalogReplay(tables.TypeReferences, replay.TypeReferences);
        AssertCatalogReplay(tables.ModuleReferences, replay.ModuleReferences);
        AssertCatalogReplay(tables.TypeSpecifications, replay.TypeSpecifications);
        AssertCatalogReplay(tables.AssemblyReferences, replay.AssemblyReferences);
        AssertCatalogReplay(tables.Files, replay.Files);
        AssertCatalogReplay(tables.ExportedTypes, replay.ExportedTypes);
        Assert.Equal(
            "ff94a8cb37be26bf9be867957dbe4e2b3d608cdcd48a2d317c269c304548d498",
            tables.TypeReferences.Sha256);
        Assert.Equal(
            "ba37b5a4626d723e2ce16bacff212b46eb2776b53597a451f3dc954df8a4e6cb",
            tables.ExportedTypes.Sha256);
    }

    /// <summary>
    /// Proves incomplete, conflicting, misordered, foreign-source, out-of-range, and cap-crossing inputs produce
    /// deterministic prefix-free typed stops while default and explicit-empty vectors remain distinct.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reference_table_stops_are_typed_prefix_free_and_boundary_exact()
    {
        var sourceEnds = CreateReferenceSourceEnds();
        var module = sourceEnds.SourceModule;

        var missing = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(sourceEnds, default);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.NonExact, missing.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.TableIncomplete, missing.Issue);
        Assert.Empty(missing.Rows);
        Assert.Null(missing.FindRow(0x1A00_0001));
        Assert.Equal(0, missing.ObservedCount);

        var emptyEnds = CreateReferenceSourceEnds(
            typeReferenceRowCount: 0,
            moduleReferenceRowCount: 0,
            typeSpecificationRowCount: 0,
            assemblyReferenceRowCount: 0,
            fileRowCount: 0,
            exportedTypeRowCount: 0);
        var defaultAtZero = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(emptyEnds, default);
        Assert.Equal(MetadataReferencePhysicalTableIssue.TableIncomplete, defaultAtZero.Issue);
        var explicitEmpty = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
            emptyEnds,
            ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, explicitEmpty.ResultKind);
        Assert.Empty(explicitEmpty.Rows);
        Assert.NotEqual(defaultAtZero.Sha256, explicitEmpty.Sha256);

        var shortVector = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 1)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.NonExact, shortVector.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.TableIncomplete, shortVector.Issue);
        Assert.Equal(1, shortVector.ObservedCount);

        var longVector = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 1), TypeReferenceRow(module, 2), TypeReferenceRow(module, 3)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, longVector.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.TableRowCountConflict, longVector.Issue);
        Assert.Empty(longVector.Rows);

        var misordered = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 2), TypeReferenceRow(module, 1)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, misordered.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.PhysicalOrderInvalid, misordered.Issue);

        var foreignModule = W8CompilerNameMappingContractTests.CreateMetadataModule(0xF000, 'f');
        var foreign = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 1), TypeReferenceRow(foreignModule, 2)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, foreign.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.SourceMismatch, foreign.Issue);
        Assert.Empty(foreign.Rows);

        var moduleScopeBeyondOne = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 1), TypeReferenceRow(module, 2, resolutionScope: 0x0000_0002)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, moduleScopeBeyondOne.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.CodedIndexOutOfRange, moduleScopeBeyondOne.Issue);
        var scopeBeyondEnd = MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [TypeReferenceRow(module, 1), TypeReferenceRow(module, 2, resolutionScope: 0x2300_0002)]);
        Assert.Equal(MetadataReferencePhysicalTableIssue.CodedIndexOutOfRange, scopeBeyondEnd.Issue);

        var implementationBeyondEnd = MetadataExportedTypePhysicalTableCatalogIdentity.Create(
            sourceEnds,
            [ExportedTypeRow(module, 1, implementation: 0x2600_0002),
             ExportedTypeRow(module, 2, implementation: 0x2700_0001)]);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Invalid, implementationBeyondEnd.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.CodedIndexOutOfRange, implementationBeyondEnd.Issue);
        Assert.Empty(implementationBeyondEnd.Rows);

        var atCapEnds = CreateReferenceSourceEnds(
            moduleReferenceRowCount: MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount);
        var atCapObservations = ImmutableArray.CreateBuilder<MetadataModuleReferenceRowObservationIdentity>(
            MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount);
        for (var rowId = 1; rowId <= MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount; rowId++)
        {
            atCapObservations.Add(MetadataModuleReferenceRowObservationIdentity.Create(
                module,
                0x1A00_0000 | rowId,
                $"m{rowId}.dll"));
        }
        var atCap = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
            atCapEnds,
            atCapObservations.MoveToImmutable());
        Assert.Equal(MetadataReferencePhysicalTableResultKind.Exact, atCap.ResultKind);
        Assert.Equal(MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount, atCap.Rows.Length);
        Assert.Null(atCap.ReachedBound);

        var overCapEnds = CreateReferenceSourceEnds(
            moduleReferenceRowCount: MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount + 1);
        var overCap = MetadataModuleReferencePhysicalTableCatalogIdentity.Create(overCapEnds, default);
        Assert.Equal(MetadataReferencePhysicalTableResultKind.NonExact, overCap.ResultKind);
        Assert.Equal(MetadataReferencePhysicalTableIssue.TableRowBoundReached, overCap.Issue);
        Assert.Empty(overCap.Rows);
        var bound = Assert.IsType<EvaluationDeterministicBound>(overCap.ReachedBound);
        Assert.Equal(MetadataModuleReferencePhysicalTableCatalogIdentity.TableRowCountBoundName, bound.Name);
        Assert.Equal(MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount, bound.Value);
        Assert.Equal(MetadataReferenceSourceEndIdentity.MaximumReferenceTableRowCount + 1, overCap.ObservedCount);
    }

    /// <summary>
    /// Proves defensive copies, private guarded row issuance, the closed public issuer surface, and emitted XML
    /// documentation across all six physical reference-table families.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reference_tables_are_immutable_guarded_and_documented()
    {
        var tables = BuildExactReferenceTables();
        var catalog = tables.TypeReferences;
        var originalBytes = catalog.CanonicalBytes;
        var originalSha = catalog.Sha256;
        var originalFirstRow = catalog.Rows[0];

        var returnedRows = catalog.Rows;
        ImmutableCollectionsMarshal.AsArray(returnedRows)![0] = returnedRows[^1];
        var returnedBytes = catalog.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var returnedBlob = tables.TypeSpecifications.Rows[0].Observation.SignatureBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBlob)![0] ^= 0x3C;

        Assert.Equal(originalFirstRow, catalog.Rows[0]);
        Assert.True(originalBytes.AsSpan().SequenceEqual(catalog.CanonicalBytes.AsSpan()));
        Assert.Equal(originalSha, catalog.Sha256);
        Assert.True(TypeSpecificationBlob.AsSpan().SequenceEqual(
            tables.TypeSpecifications.Rows[0].Observation.SignatureBytes.AsSpan()));

        var sourceEnds = tables.SourceEnds;
        var module = sourceEnds.SourceModule;
        Assert.Throws<ArgumentException>(() => MetadataTypeReferencePhysicalRowIdentity.Create(
            new object(), sourceEnds, TypeReferenceRow(module, 1)));
        Assert.Throws<ArgumentException>(() => MetadataModuleReferencePhysicalRowIdentity.Create(
            new object(), sourceEnds, ModuleReferenceRow(module, 1)));
        Assert.Throws<ArgumentException>(() => MetadataTypeSpecificationPhysicalRowIdentity.Create(
            new object(), sourceEnds, TypeSpecificationRow(module, 1)));
        Assert.Throws<ArgumentException>(() => MetadataAssemblyReferencePhysicalRowIdentity.Create(
            new object(), sourceEnds, AssemblyReferenceRow(module, 1)));
        Assert.Throws<ArgumentException>(() => MetadataAssemblyFilePhysicalRowIdentity.Create(
            new object(), sourceEnds, FileRow(module, 1)));
        Assert.Throws<ArgumentException>(() => MetadataExportedTypePhysicalRowIdentity.Create(
            new object(), sourceEnds, ExportedTypeRow(module, 1, implementation: 0x2300_0001)));
        Assert.False(MetadataTypeReferencePhysicalTableCatalogIdentity.OwnsRowMintCapability(new object()));
        Assert.False(MetadataExportedTypePhysicalTableCatalogIdentity.OwnsRowMintCapability(new object()));

        var publicTypes = new[]
        {
            typeof(MetadataReferenceSourceEndIdentity),
            typeof(MetadataReferencePhysicalTableResultKind),
            typeof(MetadataReferencePhysicalTableIssue),
            typeof(MetadataTypeReferenceRowObservationIdentity),
            typeof(MetadataTypeReferencePhysicalRowIdentity),
            typeof(MetadataTypeReferencePhysicalTableCatalogIdentity),
            typeof(MetadataModuleReferenceRowObservationIdentity),
            typeof(MetadataModuleReferencePhysicalRowIdentity),
            typeof(MetadataModuleReferencePhysicalTableCatalogIdentity),
            typeof(MetadataAssemblyReferenceRowObservationIdentity),
            typeof(MetadataAssemblyReferencePhysicalRowIdentity),
            typeof(MetadataAssemblyReferencePhysicalTableCatalogIdentity),
            typeof(MetadataTypeSpecificationRowObservationIdentity),
            typeof(MetadataTypeSpecificationPhysicalRowIdentity),
            typeof(MetadataTypeSpecificationPhysicalTableCatalogIdentity),
            typeof(MetadataAssemblyFileRowObservationIdentity),
            typeof(MetadataAssemblyFilePhysicalRowIdentity),
            typeof(MetadataAssemblyFilePhysicalTableCatalogIdentity),
            typeof(MetadataExportedTypeRowObservationIdentity),
            typeof(MetadataExportedTypePhysicalRowIdentity),
            typeof(MetadataExportedTypePhysicalTableCatalogIdentity),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .ToArray();
            if (type.Name.EndsWith("PhysicalRowIdentity", StringComparison.Ordinal))
            {
                Assert.Empty(publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Equal(["Create"], publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static readonly ImmutableArray<byte> TypeSpecificationBlob = [0x15, 0x12, 0x08, 0x01, 0x0E];

    private static readonly ImmutableArray<byte> FileHashBlob = [0xAA, 0xBB, 0xCC, 0xDD];

    internal static MetadataReferenceSourceEndIdentity CreateReferenceSourceEnds(
        StaticFieldMetadataModuleIdentity? module = null,
        int typeReferenceRowCount = TypeReferenceRowCount,
        int moduleReferenceRowCount = ModuleReferenceRowCount,
        int typeSpecificationRowCount = TypeSpecificationRowCount,
        int assemblyReferenceRowCount = AssemblyReferenceRowCount,
        int fileRowCount = FileRowCount,
        int exportedTypeRowCount = ExportedTypeRowCount)
    {
        module ??= W8CompilerNameMappingContractTests.CreateMetadataModule();
        var definitionEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: 1,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: 1,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: typeReferenceRowCount,
                typeSpecificationRowCount: typeSpecificationRowCount,
                assemblyReferenceRowCount: assemblyReferenceRowCount,
                methodDefinitionRowCount: 0,
                parameterDefinitionRowCount: 0,
                propertyDefinitionRowCount: 0,
                eventDefinitionRowCount: 0,
                moduleDefinitionRowCount: 1,
                assemblyDefinitionRowCount: 1,
                interfaceImplementationRowCount: 0,
                memberReferenceRowCount: 0,
                customAttributeRowCount: 0,
                moduleReferenceRowCount: moduleReferenceRowCount,
                fileRowCount: fileRowCount,
                exportedTypeRowCount: exportedTypeRowCount,
                nestedClassRowCount: 0,
                genericParameterRowCount: 0,
                genericParameterConstraintRowCount: 0,
                fieldPointerRowCount: 0,
                methodPointerRowCount: 0,
                parameterPointerRowCount: 0));
        return MetadataReferenceSourceEndIdentity.Create(definitionEnds);
    }

    private static ExactReferenceTables BuildExactReferenceTables()
    {
        var sourceEnds = CreateReferenceSourceEnds();
        var module = sourceEnds.SourceModule;
        return new ExactReferenceTables(
            sourceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [TypeReferenceRow(module, 1), TypeReferenceRow(module, 2, resolutionScope: 0x2300_0001)]),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [ModuleReferenceRow(module, 1)]),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [TypeSpecificationRow(module, 1)]),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [AssemblyReferenceRow(module, 1)]),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [FileRow(module, 1)]),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                sourceEnds,
                [ExportedTypeRow(module, 1, implementation: 0x2300_0001),
                 ExportedTypeRow(module, 2, implementation: 0x2700_0001)]));
    }

    private static MetadataTypeReferenceRowObservationIdentity TypeReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int resolutionScope = 0x0000_0001) =>
        MetadataTypeReferenceRowObservationIdentity.Create(
            module,
            0x0100_0000 | rowId,
            namespaceName: "System",
            typeName: rowId == 1 ? "Object" : "ValueType",
            resolutionScopeMetadataToken: resolutionScope);

    private static MetadataModuleReferenceRowObservationIdentity ModuleReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataModuleReferenceRowObservationIdentity.Create(
            module,
            0x1A00_0000 | rowId,
            name: "native-companion.dll");

    private static MetadataTypeSpecificationRowObservationIdentity TypeSpecificationRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataTypeSpecificationRowObservationIdentity.Create(
            module,
            0x1B00_0000 | rowId,
            TypeSpecificationBlob);

    private static MetadataAssemblyReferenceRowObservationIdentity AssemblyReferenceRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataAssemblyReferenceRowObservationIdentity.Create(
            module,
            0x2300_0000 | rowId,
            name: "Synthetic.CompilerNameMapping",
            majorVersion: 1,
            minorVersion: 0,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            publicKeyOrToken: ImmutableArray<byte>.Empty,
            hashValue: ImmutableArray<byte>.Empty);

    private static StaticFieldAssemblyReferenceIdentity CreateAssemblyReferenceCandidate(int minorVersion = 0) =>
        StaticFieldAssemblyReferenceIdentity.Create(
            assemblyReferenceToken: 0x2300_0001,
            name: "Synthetic.CompilerNameMapping",
            majorVersion: 1,
            minorVersion: minorVersion,
            buildNumber: 0,
            revisionNumber: 0,
            culture: string.Empty,
            flags: 0,
            publicKeyOrToken: ImmutableArray<byte>.Empty,
            hashValue: ImmutableArray<byte>.Empty);

    private static MetadataAssemblyFileRowObservationIdentity FileRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId) =>
        MetadataAssemblyFileRowObservationIdentity.Create(
            module,
            0x2600_0000 | rowId,
            flags: 0,
            name: "companion.netmodule",
            hashValue: FileHashBlob);

    private static MetadataExportedTypeRowObservationIdentity ExportedTypeRow(
        StaticFieldMetadataModuleIdentity module,
        int rowId,
        int implementation) =>
        MetadataExportedTypeRowObservationIdentity.Create(
            module,
            0x2700_0000 | rowId,
            typeAttributes: rowId == 1 ? ForwarderTypeAttribute : 0,
            typeDefinitionId: 0,
            namespaceName: rowId == 1 ? "Synthetic.Forwarded" : string.Empty,
            typeName: rowId == 1 ? "Relocated" : "Nested",
            implementationMetadataToken: implementation);

    private static void AssertCatalogReplay<T>(T first, T replay)
        where T : class, IEquatable<T>
    {
        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(MetadataReferenceSourceEndIdentity).Assembly;
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

    private sealed record ExactReferenceTables(
        MetadataReferenceSourceEndIdentity SourceEnds,
        MetadataTypeReferencePhysicalTableCatalogIdentity TypeReferences,
        MetadataModuleReferencePhysicalTableCatalogIdentity ModuleReferences,
        MetadataTypeSpecificationPhysicalTableCatalogIdentity TypeSpecifications,
        MetadataAssemblyReferencePhysicalTableCatalogIdentity AssemblyReferences,
        MetadataAssemblyFilePhysicalTableCatalogIdentity Files,
        MetadataExportedTypePhysicalTableCatalogIdentity ExportedTypes);
}
