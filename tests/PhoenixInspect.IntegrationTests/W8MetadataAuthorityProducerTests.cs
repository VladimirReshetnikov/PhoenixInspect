using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Exercises the host-owned metadata producer against the real W8 fixture assemblies on disk.
/// </summary>
/// <remarks>
/// Every catalog under test is issued from physical PE metadata rows rather than synthetic rows. The asserted shapes
/// are physical evidence and are not commitments for the eventual public API.
/// </remarks>
public sealed class W8MetadataAuthorityProducerTests
{
    /// <remarks>
    /// The produced modules share the snapshot digest used by the synthetic W8 world builders so a produced
    /// catalog and a synthetic one can be composed into the same portfolio.
    /// </remarks>
    private const string SnapshotDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string AliasNamespace = "PhoenixInspect.W8AliasTarget";
    private const string AliasExternalTypeName = "ExternalRequestContext";
    private const string TargetNamespace = "PhoenixInspect.W8TestTarget";

    /// <summary>
    /// Proves acquiring the real W8 target module produces an exact per-module outcome whose every catalog is exact
    /// and whose derived source ends equal the physical <see cref="MetadataReader"/> table row counts.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Acquiring_the_real_target_module_matches_every_physical_table_row_count()
    {
        using var target = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
        var outcome = MetadataAuthorityProducer.AcquireModule(target.Request);

        Assert.Equal(MetadataModuleAcquisitionResultKind.Exact, outcome.ResultKind);
        Assert.Equal(MetadataModuleAcquisitionStage.None, outcome.Stage);
        Assert.Equal(MetadataModuleAcquisitionIssue.None, outcome.Issue);
        Assert.True(outcome.IsExact);
        Assert.Equal(target.Module, outcome.MetadataModule);

        var reader = target.Reader;
        var sourceEnds = Assert.IsType<MetadataSourceEndIdentity>(outcome.SourceEnds);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeDef), sourceEnds.TypeDefinitionRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeRef), sourceEnds.TypeReferenceRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeSpec), sourceEnds.TypeSpecificationRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.Field), sourceEnds.FieldDefinitionRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.FieldPtr), sourceEnds.FieldPointerRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodDef), sourceEnds.MethodDefinitionRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodPtr), sourceEnds.MethodPointerRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.Param), sourceEnds.ParameterDefinitionRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ParamPtr), sourceEnds.ParameterPointerRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.NestedClass), sourceEnds.NestedClassRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.GenericParam), sourceEnds.GenericParameterRowCount);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.GenericParamConstraint),
            sourceEnds.GenericParameterConstraintRowCount);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.InterfaceImpl),
            sourceEnds.InterfaceImplementationRowCount);

        var referenceSourceEnds = Assert.IsType<MetadataReferenceSourceEndIdentity>(outcome.ReferenceSourceEnds);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ModuleRef), referenceSourceEnds.ModuleReferenceRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.AssemblyRef), referenceSourceEnds.AssemblyReferenceRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.File), referenceSourceEnds.FileRowCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ExportedType), referenceSourceEnds.ExportedTypeRowCount);

        Assert.Equal(
            MetadataMemberPointerTableResultKind.Exact,
            Assert.IsType<MetadataMemberPointerTableCatalogIdentity>(outcome.MemberPointers).ResultKind);
        var typeDefinitions =
            Assert.IsType<MetadataTypeDefinitionTableCatalogIdentity>(outcome.TypeDefinitions);
        Assert.Equal(MetadataTypeDefinitionTableResultKind.Exact, typeDefinitions.ResultKind);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeDef), typeDefinitions.Rows.Length);
        Assert.Equal(
            MetadataNestedClassTableResultKind.Exact,
            Assert.IsType<MetadataNestedClassTableCatalogIdentity>(outcome.NestedClasses).ResultKind);
        var genericParameters =
            Assert.IsType<MetadataGenericParameterPhysicalTableCatalogIdentity>(outcome.GenericParameters);
        Assert.Equal(MetadataGenericParameterPhysicalTableResultKind.Exact, genericParameters.ResultKind);
        Assert.Equal(reader.GetTableRowCount(TableIndex.GenericParam), genericParameters.Rows.Length);
        var methodDefinitions =
            Assert.IsType<MetadataMethodDefinitionTableCatalogIdentity>(outcome.MethodDefinitions);
        Assert.Equal(MetadataMethodDefinitionTableResultKind.Exact, methodDefinitions.ResultKind);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodDef), methodDefinitions.Rows.Length);
        Assert.Equal(
            MetadataGenericParameterProofResultKind.Exact,
            Assert.IsType<MetadataGenericParameterAuthorityCatalogIdentity>(
                outcome.GenericParameterAuthority).ResultKind);
        var constraints = Assert.IsType<MetadataGenericParameterConstraintPhysicalTableCatalogIdentity>(
            outcome.GenericParameterConstraints);
        Assert.Equal(MetadataGenericParameterConstraintPhysicalTableResultKind.Exact, constraints.ResultKind);
        Assert.Equal(reader.GetTableRowCount(TableIndex.GenericParamConstraint), constraints.Rows.Length);
        var fieldDefinitions =
            Assert.IsType<MetadataFieldDefinitionTableCatalogIdentity>(outcome.FieldDefinitions);
        Assert.Equal(MetadataFieldDefinitionTableResultKind.Exact, fieldDefinitions.ResultKind);
        Assert.Equal(reader.GetTableRowCount(TableIndex.Field), fieldDefinitions.Rows.Length);
        Assert.Equal(
            MetadataCompilerNameMappingCatalogResultKind.Exact,
            Assert.IsType<MetadataCompilerNameMappingCatalogIdentity>(outcome.CompilerNameMappings).ResultKind);
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact,
            Assert.IsType<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>(outcome.Compatibility).ResultKind);
        Assert.Equal(
            MetadataNamedTypeDefinitionChainCatalogResultKind.Exact,
            Assert.IsType<MetadataNamedTypeDefinitionChainCatalogIdentity>(outcome.ChainCatalog).ResultKind);

        var typeReferences =
            Assert.IsType<MetadataTypeReferencePhysicalTableCatalogIdentity>(outcome.TypeReferences);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeRef), typeReferences.Rows.Length);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.ModuleRef),
            Assert.IsType<MetadataModuleReferencePhysicalTableCatalogIdentity>(outcome.ModuleReferences).Rows.Length);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.TypeSpec),
            Assert.IsType<MetadataTypeSpecificationPhysicalTableCatalogIdentity>(
                outcome.TypeSpecifications).Rows.Length);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.AssemblyRef),
            Assert.IsType<MetadataAssemblyReferencePhysicalTableCatalogIdentity>(
                outcome.AssemblyReferences).Rows.Length);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.File),
            Assert.IsType<MetadataAssemblyFilePhysicalTableCatalogIdentity>(outcome.AssemblyFiles).Rows.Length);
        Assert.Equal(
            reader.GetTableRowCount(TableIndex.ExportedType),
            Assert.IsType<MetadataExportedTypePhysicalTableCatalogIdentity>(outcome.ExportedTypes).Rows.Length);
        Assert.True(Assert.IsType<MetadataModuleReferenceTableSetIdentity>(outcome.ReferenceTables).AllTablesExact);
    }

    /// <summary>
    /// Proves the produced definition authority carries the real physical module pseudo-type at RID one and the real
    /// named TypeDefs the compiled W8 target declares, each with its physical namespace and metadata token.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Produced_definition_authority_carries_the_real_module_pseudo_type_and_named_types()
    {
        using var target = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
        var outcome = MetadataAuthorityProducer.AcquireModule(target.Request);
        Assert.Equal(MetadataModuleAcquisitionResultKind.Exact, outcome.ResultKind);

        var authority = Assert.IsType<MetadataDefinitionAuthorityCatalogIdentity>(outcome.DefinitionAuthority);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);
        Assert.Equal(target.Reader.GetTableRowCount(TableIndex.TypeDef), authority.TypeDefinitions.Length);

        var modulePseudoType = authority.TypeDefinitions[0];
        Assert.Equal(0x0200_0001, modulePseudoType.TypeDefinitionToken);
        Assert.Equal("<Module>", modulePseudoType.TypeName);
        Assert.Equal(string.Empty, modulePseudoType.NamespaceName);
        Assert.Null(modulePseudoType.EnclosingTypeDefinitionToken);

        foreach (var expectedTypeName in new[] { "Program", "W8TruthGate", "ConstructibleContext" })
        {
            var declared = Assert.Single(
                authority.TypeDefinitions,
                candidate => candidate.NamespaceName == TargetNamespace && candidate.TypeName == expectedTypeName);
            Assert.Equal(
                expectedTypeName,
                target.Reader.GetString(
                    target.Reader.GetTypeDefinition(
                        (TypeDefinitionHandle)MetadataTokens.Handle(declared.TypeDefinitionToken)).Name));
        }

        Assert.Contains(
            authority.TypeDefinitions,
            candidate => candidate.EnclosingTypeDefinitionToken is not null);
    }

    /// <summary>
    /// Proves acquiring the target with its real alias and forwarder companions composes an exact compatibility,
    /// chain, resolution, and constraint-target portfolio, and that a real cross-assembly TypeRef resolves to an
    /// authority TypeDef issued by another produced module.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Produced_portfolio_resolves_a_real_cross_assembly_typeref_to_another_produced_module()
    {
        using var world = ProducedWorld.OpenTargetAliasAndForwarder();
        var outcome = MetadataAuthorityProducer.AcquirePortfolio(world.Requests);

        Assert.Equal(3, outcome.ModuleOutcomes.Length);
        Assert.All(outcome.ModuleOutcomes, static moduleOutcome => Assert.True(moduleOutcome.IsExact));
        Assert.Equal(
            MetadataDefinitionCompatibilityPortfolioResultKind.Exact,
            Assert.IsType<MetadataDefinitionCompatibilityPortfolioIdentity>(
                outcome.CompatibilityPortfolio).ResultKind);
        Assert.Equal(
            MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact,
            Assert.IsType<MetadataNamedTypeDefinitionChainPortfolioIdentity>(outcome.ChainPortfolio).ResultKind);
        var resolution =
            Assert.IsType<MetadataTypeReferenceResolutionPortfolioIdentity>(outcome.ResolutionPortfolio);
        Assert.Equal(MetadataTypeReferenceResolutionPortfolioResultKind.Exact, resolution.ResultKind);
        Assert.Equal(
            MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
            Assert.IsType<MetadataConstraintTargetResolutionPortfolioIdentity>(
                outcome.ConstraintTargetPortfolio).ResultKind);

        var targetOutcome = outcome.ModuleOutcomes[0];
        var aliasOutcome = outcome.ModuleOutcomes[1];
        var externalReference = Assert.Single(
            targetOutcome.TypeReferences!.Rows,
            row => row.Observation.NamespaceName == AliasNamespace &&
                row.Observation.TypeName == AliasExternalTypeName);
        var resolved = Assert.IsType<MetadataTypeReferenceResolutionIdentity>(
            resolution.ExactResolutionOrDefault(world.Target.Module, externalReference.TypeReferenceToken));

        Assert.Equal(MetadataTypeReferenceResolutionDispositionKind.Resolved, resolved.Disposition);
        Assert.Equal(world.Alias.Module, resolved.TargetModule);
        Assert.NotEqual(world.Target.Module, resolved.TargetModule);
        var targetTypeDefinition =
            Assert.IsType<MetadataTypeDefinitionAuthorityIdentity>(resolved.TargetTypeDefinition);
        Assert.Equal(AliasNamespace, targetTypeDefinition.NamespaceName);
        Assert.Equal(AliasExternalTypeName, targetTypeDefinition.TypeName);
        Assert.Same(
            aliasOutcome.DefinitionAuthority!.ExactTypeDefinitionOrDefault(
                targetTypeDefinition.TypeDefinitionToken),
            targetTypeDefinition);
    }

    /// <summary>
    /// Proves the ancestry portfolio over the produced module set stops with the exact declared core-module issue.
    /// The runtime core library is deliberately not among the produced modules, so no module defines the nil-extends
    /// top-level System.Object the selection requires. This documents why a later host must add the core library to
    /// the produced set before ancestry can select roles.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Produced_portfolio_ancestry_stops_with_core_module_absent_without_the_core_library()
    {
        using var world = ProducedWorld.OpenTargetAliasAndForwarder();
        var outcome = MetadataAuthorityProducer.AcquirePortfolio(world.Requests);

        Assert.Equal(MetadataAuthorityPortfolioResultKind.NonExact, outcome.ResultKind);
        Assert.Equal(MetadataAuthorityPortfolioStage.AncestryAuthorityPortfolio, outcome.Stage);
        Assert.Equal(MetadataAuthorityPortfolioIssue.PortfolioNonExact, outcome.Issue);
        Assert.False(outcome.IsExact);

        var ancestry = Assert.IsType<MetadataAncestryAuthorityPortfolioIdentity>(outcome.AncestryPortfolio);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.NonExact, ancestry.ResultKind);
        Assert.Equal(MetadataAncestryAuthorityPortfolioIssue.CoreModuleAbsent, ancestry.Issue);
        Assert.Empty(ancestry.Entries);
        Assert.Null(ancestry.CoreRoles);

        Assert.DoesNotContain(
            outcome.ModuleOutcomes,
            static moduleOutcome => moduleOutcome.DefinitionAuthority!.TypeDefinitions.Any(
                static typeDefinition => typeDefinition.NamespaceName == "System" &&
                    typeDefinition.TypeName == "Object"));
    }

    /// <summary>
    /// Proves a produced chain catalog is interchangeable with a synthetic one by composing both into the same
    /// downstream chain portfolio and observing an exact result covering both modules.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Produced_chain_catalogs_compose_with_synthetic_chain_catalogs()
    {
        using var target = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
        var produced = MetadataAuthorityProducer.AcquireModule(target.Request);
        Assert.Equal(MetadataModuleAcquisitionResultKind.Exact, produced.ResultKind);

        var synthetic = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.ProducerPeer",
            0xB700,
            'c',
            [new W8MetadataAncestryAuthorityContractTests.TypeRow("Synthetic.ProducerPeer", "Peer", 0x0000_0001, null)]);

        var compatibilityPortfolio = MetadataDefinitionCompatibilityPortfolioIdentity.Create(
            [produced.Compatibility!, synthetic.Compatibility]);
        Assert.Equal(
            MetadataDefinitionCompatibilityPortfolioResultKind.Exact,
            compatibilityPortfolio.ResultKind);

        var chainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            compatibilityPortfolio,
            [produced.ChainCatalog!, synthetic.ChainCatalog]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, chainPortfolio.ResultKind);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioIssue.None, chainPortfolio.Issue);
        Assert.Equal(2, chainPortfolio.Entries.Length);
        Assert.Contains(chainPortfolio.Entries, entry => entry.SourceModule.Equals(target.Module));
        Assert.Contains(chainPortfolio.Entries, entry => entry.SourceModule.Equals(synthetic.Module));

        var producedOnly = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create([produced.Compatibility!]),
            [produced.ChainCatalog!]);
        Assert.Equal(MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact, producedOnly.ResultKind);
        Assert.Equal(
            produced.ChainCatalog,
            Assert.Single(producedOnly.Entries).ChainCatalog);
    }

    /// <summary>
    /// Proves acquiring the same real module twice yields canonically byte-identical outcomes with equal digests, and
    /// that acquiring the whole produced module set twice replays identically as well.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Acquiring_the_same_module_twice_replays_byte_identically()
    {
        using var first = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
        using var second = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);

        var firstOutcome = MetadataAuthorityProducer.AcquireModule(first.Request);
        var secondOutcome = MetadataAuthorityProducer.AcquireModule(second.Request);

        Assert.Equal(MetadataModuleAcquisitionResultKind.Exact, firstOutcome.ResultKind);
        Assert.Equal(firstOutcome, secondOutcome);
        Assert.Equal(firstOutcome.GetHashCode(), secondOutcome.GetHashCode());
        Assert.Equal(firstOutcome.Sha256, secondOutcome.Sha256);
        Assert.True(firstOutcome.CanonicalBytes.AsSpan().SequenceEqual(secondOutcome.CanonicalBytes.AsSpan()));

        var mutated = firstOutcome.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(mutated)![0] ^= 0x5A;
        Assert.Equal(secondOutcome.Sha256, firstOutcome.Sha256);
        Assert.True(firstOutcome.CanonicalBytes.AsSpan().SequenceEqual(secondOutcome.CanonicalBytes.AsSpan()));

        using var firstWorld = ProducedWorld.OpenTargetAliasAndForwarder();
        using var secondWorld = ProducedWorld.OpenTargetAliasAndForwarder();
        var firstPortfolio = MetadataAuthorityProducer.AcquirePortfolio(firstWorld.Requests);
        var secondPortfolio = MetadataAuthorityProducer.AcquirePortfolio(secondWorld.Requests);
        Assert.Equal(firstPortfolio, secondPortfolio);
        Assert.Equal(firstPortfolio.Sha256, secondPortfolio.Sha256);
        Assert.True(
            firstPortfolio.CanonicalBytes.AsSpan().SequenceEqual(secondPortfolio.CanonicalBytes.AsSpan()));
    }

    /// <summary>
    /// Proves a truncated physical image surfaces a typed image-unreadable stop that fabricates no row and retains no
    /// catalog, rather than throwing out of the producer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Truncated_physical_image_surfaces_a_typed_stop_instead_of_an_exception()
    {
        using var target = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
        using var scratch = new ScratchDirectory();
        var truncatedPath = scratch.Combine("PhoenixInspect.W8TestTarget.truncated.dll");
        var originalBytes = File.ReadAllBytes(target.Path);
        File.WriteAllBytes(truncatedPath, originalBytes.AsSpan(0, originalBytes.Length / 4).ToArray());

        using var truncatedStream = File.OpenRead(truncatedPath);
        var request = MetadataModuleAcquisitionRequest.Create(
            target.Module,
            () => new PEReader(truncatedStream).GetMetadataReader());
        var outcome = MetadataAuthorityProducer.AcquireModule(request);

        Assert.Equal(MetadataModuleAcquisitionResultKind.NonExact, outcome.ResultKind);
        Assert.Equal(MetadataModuleAcquisitionStage.MetadataImage, outcome.Stage);
        Assert.Equal(MetadataModuleAcquisitionIssue.MetadataImageUnreadable, outcome.Issue);
        Assert.False(outcome.IsExact);
        Assert.Null(outcome.SourceEnds);
        Assert.Null(outcome.TypeDefinitions);
        Assert.Null(outcome.DefinitionAuthority);
        Assert.Null(outcome.ChainCatalog);
        Assert.Null(outcome.ReferenceTables);
        Assert.Equal(target.Module, outcome.MetadataModule);

        var portfolioOutcome = MetadataAuthorityProducer.AcquirePortfolio([request]);
        Assert.Equal(MetadataAuthorityPortfolioResultKind.NonExact, portfolioOutcome.ResultKind);
        Assert.Equal(MetadataAuthorityPortfolioStage.ModuleAcquisition, portfolioOutcome.Stage);
        Assert.Equal(MetadataAuthorityPortfolioIssue.ModuleAcquisitionStopped, portfolioOutcome.Issue);
        Assert.Equal(target.Module.Sha256, portfolioOutcome.RelatedModuleSha256);
        Assert.Null(portfolioOutcome.CompatibilityPortfolio);
        Assert.Null(portfolioOutcome.ChainPortfolio);
        Assert.Null(portfolioOutcome.ResolutionPortfolio);
        Assert.Null(portfolioOutcome.AncestryPortfolio);
        Assert.Null(portfolioOutcome.ConstraintTargetPortfolio);
    }

    /// <summary>
    /// Proves every public producer type and public non-property member emits XML documentation, matching
    /// the implementation documentation convention the surrounding W8 contracts already observe.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Producer_contracts_are_documented_as_draft()
    {
        var publicTypes = new[]
        {
            typeof(MetadataModuleAcquisitionResultKind),
            typeof(MetadataModuleAcquisitionStage),
            typeof(MetadataModuleAcquisitionIssue),
            typeof(MetadataAuthorityPortfolioResultKind),
            typeof(MetadataAuthorityPortfolioStage),
            typeof(MetadataAuthorityPortfolioIssue),
            typeof(MetadataModuleAcquisitionRequest),
            typeof(MetadataModuleAcquisitionOutcome),
            typeof(MetadataAuthorityPortfolioOutcome),
            typeof(MetadataAuthorityProducer),
        };
        var assembly = typeof(MetadataAuthorityProducer).Assembly;
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

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"The required W8 fixture artifact is missing: {path}");
        return path;
    }

    private sealed class ProducedWorld : IDisposable
    {
        private ProducedWorld(ProducedModule target, ProducedModule alias, ProducedModule forwarder)
        {
            Target = target;
            Alias = alias;
            Forwarder = forwarder;
            Requests = [target.Request, alias.Request, forwarder.Request];
        }

        internal ProducedModule Target { get; }

        internal ProducedModule Alias { get; }

        internal ProducedModule Forwarder { get; }

        internal ImmutableArray<MetadataModuleAcquisitionRequest> Requests { get; }

        internal static ProducedWorld OpenTargetAliasAndForwarder()
        {
            var target = ProducedModule.Open(RequireArtifact(W8TestTargetPaths.ResolveAssembly()), ordinal: 1);
            try
            {
                var alias = ProducedModule.Open(
                    RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly()),
                    ordinal: 2);
                try
                {
                    var forwarder = ProducedModule.Open(
                        RequireArtifact(W8TestTargetPaths.ResolveForwarderAssembly()),
                        ordinal: 3);
                    return new ProducedWorld(target, alias, forwarder);
                }
                catch
                {
                    alias.Dispose();
                    throw;
                }
            }
            catch
            {
                target.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Forwarder.Dispose();
            Alias.Dispose();
            Target.Dispose();
        }
    }

    private sealed class ProducedModule : IDisposable
    {
        private readonly FileStream stream;
        private readonly PEReader peReader;

        private ProducedModule(
            string path,
            FileStream stream,
            PEReader peReader,
            StaticFieldMetadataModuleIdentity module)
        {
            Path = path;
            this.stream = stream;
            this.peReader = peReader;
            Reader = peReader.GetMetadataReader();
            Module = module;
            Request = MetadataModuleAcquisitionRequest.Create(module, () => peReader.GetMetadataReader());
        }

        internal string Path { get; }

        internal MetadataReader Reader { get; }

        internal StaticFieldMetadataModuleIdentity Module { get; }

        internal MetadataModuleAcquisitionRequest Request { get; }

        internal static ProducedModule Open(string path, int ordinal)
        {
            var stream = File.OpenRead(path);
            try
            {
                var peReader = new PEReader(stream);
                try
                {
                    var reader = peReader.GetMetadataReader();
                    var moduleInstance = StaticFieldModuleInstanceIdentity.Create(
                        SnapshotDigest,
                        sizeof(ulong),
                        applicationDomainAddress: 0x1000,
                        moduleAddress: 0x0001_0000UL + ((ulong)ordinal << 12),
                        imageBase: 0,
                        imageSize: 0);
                    var module = MetadataAuthorityProducer.AcquireManifestModuleIdentity(
                        moduleInstance,
                        reader,
                        peReader.GetMetadata().GetContent());
                    return new ProducedModule(path, stream, peReader, module);
                }
                catch
                {
                    peReader.Dispose();
                    throw;
                }
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            peReader.Dispose();
            stream.Dispose();
        }
    }

    private sealed class ScratchDirectory : IDisposable
    {
        internal ScratchDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"w8-producer-{Guid.NewGuid():n}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string Combine(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
