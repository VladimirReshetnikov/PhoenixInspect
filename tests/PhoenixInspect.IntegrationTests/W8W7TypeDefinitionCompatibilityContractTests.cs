using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises guarded authority-to-W7 TypeDef compatibility with synthetic metadata catalogs.</summary>
public sealed class W8W7TypeDefinitionCompatibilityContractTests
{
    private const int OuterTypeRid = 2;
    private const int EqualTypeRid = 3;
    private const int DeltaTypeRid = 4;
    private const int WideTypeRid = 6;
    private const int WiderTypeRid = 7;
    private const int PlainGenericTypeRid = 12;

    /// <summary>
    /// Proves a direct-layout catalog certifies a meaningful candidate chain while explicit null slots remain complete
    /// absent-candidate row outcomes.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_layout_certifies_complete_nested_candidates_and_explicit_absence()
    {
        var authority = W8DefinitionAuthorityCatalogContractTests.BuildCompatibilityAuthority(usePointers: false);
        var outer = Candidate(AuthorityType(authority, OuterTypeRid));
        var inner = Candidate(AuthorityType(authority, EqualTypeRid), outer);
        var leaf = Candidate(AuthorityType(authority, DeltaTypeRid), inner);
        var candidates = CandidateSlots(
            authority,
            (OuterTypeRid, outer),
            (EqualTypeRid, inner),
            (DeltaTypeRid, leaf));

        var catalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(authority, candidates);

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact, catalog.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.None, catalog.Issue);
        Assert.Same(authority, catalog.DefinitionAuthority);
        Assert.Equal(authority.TypeDefinitions.Length, catalog.Certificates.Length);
        Assert.Equal(
            Enumerable.Range(1, authority.TypeDefinitions.Length).Select(TypeToken),
            catalog.Certificates.Select(static certificate =>
                certificate.AuthorityTypeDefinition.TypeDefinitionToken));

        var module = Certificate(catalog, typeDefinitionRid: 1);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityResultKind.CandidateAbsent, module.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.CandidateAbsent, module.Issue);
        Assert.Null(module.Candidate);
        Assert.False(module.IsCompatible);

        var outerCertificate = Certificate(catalog, OuterTypeRid);
        Assert.True(outerCertificate.IsCompatible);
        Assert.Same(outer, outerCertificate.Candidate);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable,
            outerCertificate.FieldOwnershipDomain);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable,
            outerCertificate.MethodOwnershipDomain);
        Assert.Equal([FieldToken(2)], outerCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(2)], outerCertificate.MethodDefinitionTokens.ToArray());

        var innerCertificate = Certificate(catalog, EqualTypeRid);
        var leafCertificate = Certificate(catalog, DeltaTypeRid);
        Assert.True(innerCertificate.IsCompatible);
        Assert.True(leafCertificate.IsCompatible);
        Assert.Equal([FieldToken(3)], innerCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(3)], innerCertificate.MethodDefinitionTokens.ToArray());
        Assert.Equal([FieldToken(4)], leafCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(4)], leafCertificate.MethodDefinitionTokens.ToArray());
        Assert.True(leaf.CanonicalBytes.Length > outer.CanonicalBytes.Length);
        Assert.Equal(outerCertificate.CanonicalBytes.Length, leafCertificate.CanonicalBytes.Length);
        Assert.Equal(-1, leafCertificate.CanonicalBytes.AsSpan().IndexOf(leaf.CanonicalBytes.AsSpan()));

        Assert.Same(outerCertificate, catalog.CompleteCertificateOrDefault(TypeToken(OuterTypeRid)));
        Assert.Null(catalog.CompleteCertificateOrDefault(MethodToken(OuterTypeRid)));
        Assert.Null(catalog.CompleteCertificateOrDefault(TypeToken(authority.TypeDefinitions.Length + 1)));
    }

    /// <summary>
    /// Proves direct, FieldPtr-only, MethodPtr-only, and combined reordered domains compare resolved definition-token
    /// ownership instead of trusting numerically matching W7 intervals.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Reordered_pointer_domains_expose_field_and_method_ownership_mismatches()
    {
        var direct = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var fieldPointers = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            useFieldPointers: true,
            useMethodPointers: false).DefinitionAuthority;
        var methodPointers = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            useFieldPointers: false,
            useMethodPointers: true).DefinitionAuthority;
        var bothPointers = W8CompilerNameMappingContractTests.BuildScenario(usePointers: true).DefinitionAuthority;

        var directCertificate = CompareOuter(direct);
        var fieldPointerCertificate = CompareOuter(fieldPointers);
        var methodPointerCertificate = CompareOuter(methodPointers);
        var bothPointerCertificate = CompareOuter(bothPointers);

        Assert.True(directCertificate.IsCompatible);
        Assert.Equal([FieldToken(2)], directCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(2)], directCertificate.MethodDefinitionTokens.ToArray());

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.FieldOwnershipMismatch,
            fieldPointerCertificate.Issue);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.PointerTable,
            fieldPointerCertificate.FieldOwnershipDomain);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable,
            fieldPointerCertificate.MethodOwnershipDomain);
        Assert.Equal([FieldToken(1)], fieldPointerCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(2)], fieldPointerCertificate.MethodDefinitionTokens.ToArray());

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.MethodOwnershipMismatch,
            methodPointerCertificate.Issue);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.DirectDefinitionTable,
            methodPointerCertificate.FieldOwnershipDomain);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.PointerTable,
            methodPointerCertificate.MethodOwnershipDomain);
        Assert.Equal([FieldToken(2)], methodPointerCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(1)], methodPointerCertificate.MethodDefinitionTokens.ToArray());

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.FieldOwnershipMismatch,
            bothPointerCertificate.Issue);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.PointerTable,
            bothPointerCertificate.FieldOwnershipDomain);
        Assert.Equal(MetadataW7MemberOwnershipDomainKind.PointerTable,
            bothPointerCertificate.MethodOwnershipDomain);
        Assert.Equal([FieldToken(1)], bothPointerCertificate.FieldDefinitionTokens.ToArray());
        Assert.Equal([MethodToken(1)], bothPointerCertificate.MethodDefinitionTokens.ToArray());

        Assert.Equal(directCertificate.Candidate!.FieldListRowId,
            fieldPointerCertificate.Candidate!.FieldListRowId);
        Assert.Equal(directCertificate.Candidate.FieldListEndExclusiveRowId,
            fieldPointerCertificate.Candidate.FieldListEndExclusiveRowId);
        Assert.Equal(directCertificate.Candidate.MethodListRowId,
            methodPointerCertificate.Candidate!.MethodListRowId);
        Assert.Equal(directCertificate.Candidate.MethodListEndExclusiveRowId,
            methodPointerCertificate.Candidate.MethodListEndExclusiveRowId);
    }

    /// <summary>Proves each scalar authority field reports a stable first mismatch.</summary>
    /// <param name="mutation">The one candidate scalar changed from the exact authority row.</param>
    /// <param name="expectedIssue">The expected first typed mismatch.</param>
    [Theory]
    [InlineData(CandidateMutation.Source, MetadataW7TypeDefinitionCompatibilityIssue.SourceMismatch)]
    [InlineData(CandidateMutation.Token, MetadataW7TypeDefinitionCompatibilityIssue.TypeDefinitionTokenMismatch)]
    [InlineData(CandidateMutation.Namespace, MetadataW7TypeDefinitionCompatibilityIssue.NamespaceNameMismatch)]
    [InlineData(CandidateMutation.Name, MetadataW7TypeDefinitionCompatibilityIssue.TypeNameMismatch)]
    [InlineData(CandidateMutation.Attributes, MetadataW7TypeDefinitionCompatibilityIssue.TypeAttributesMismatch)]
    [InlineData(CandidateMutation.Extends, MetadataW7TypeDefinitionCompatibilityIssue.ExtendsMetadataTokenMismatch)]
    [InlineData(CandidateMutation.FieldStart, MetadataW7TypeDefinitionCompatibilityIssue.FieldListStartMismatch)]
    [InlineData(CandidateMutation.FieldEnd, MetadataW7TypeDefinitionCompatibilityIssue.FieldListEndMismatch)]
    [InlineData(CandidateMutation.MethodStart, MetadataW7TypeDefinitionCompatibilityIssue.MethodListStartMismatch)]
    [InlineData(CandidateMutation.MethodEnd, MetadataW7TypeDefinitionCompatibilityIssue.MethodListEndMismatch)]
    [Trait("Category", "Fast")]
    public void Scalar_candidate_mutations_report_the_first_exact_issue(
        CandidateMutation mutation,
        MetadataW7TypeDefinitionCompatibilityIssue expectedIssue)
    {
        var authority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var authorityOuter = AuthorityType(authority, OuterTypeRid);
        var candidate = MutatedOuter(authorityOuter, mutation);
        var catalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            CandidateSlots(authority, (OuterTypeRid, candidate)));
        var certificate = Certificate(catalog, OuterTypeRid);

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityResultKind.Mismatch, certificate.ResultKind);
        Assert.Equal(expectedIssue, certificate.Issue);
        Assert.False(certificate.IsCompatible);
        Assert.Same(candidate, certificate.Candidate);
        if (mutation == CandidateMutation.Name)
        {
            Assert.Equal(authorityOuter.SourceEnds.SourceModule, candidate.MetadataModule);
            Assert.Equal(authorityOuter.TypeDefinitionToken, candidate.TypeDefinitionToken);
        }
    }

    /// <summary>
    /// Proves total GenericParam count and a mismatch in the second enclosing ancestor are classified independently.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generic_count_and_complete_enclosing_chain_mismatches_are_explicit()
    {
        var wideAuthority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var plainAuthority = AuthorityType(wideAuthority, PlainGenericTypeRid);
        var zeroArityCandidate = Candidate(
            plainAuthority,
            genericParameterCount: 0,
            introducedGenericArity: 0);
        var genericCatalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            wideAuthority,
            CandidateSlots(wideAuthority, (PlainGenericTypeRid, zeroArityCandidate)));
        Assert.Equal(
            MetadataW7TypeDefinitionCompatibilityIssue.GenericParameterCountMismatch,
            Certificate(genericCatalog, PlainGenericTypeRid).Issue);

        var chainAuthority = W8DefinitionAuthorityCatalogContractTests.BuildCompatibilityAuthority(usePointers: false);
        var authorityOuter = AuthorityType(chainAuthority, OuterTypeRid);
        var authorityInner = AuthorityType(chainAuthority, EqualTypeRid);
        var authorityLeaf = AuthorityType(chainAuthority, DeltaTypeRid);
        var changedOuter = Candidate(authorityOuter, typeName: "ChangedOuter`2");
        var innerWithChangedOuter = Candidate(authorityInner, changedOuter);
        var leafWithChangedChain = Candidate(authorityLeaf, innerWithChangedOuter);
        var chainCatalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            chainAuthority,
            CandidateSlots(
                chainAuthority,
                (OuterTypeRid, changedOuter),
                (EqualTypeRid, innerWithChangedOuter),
                (DeltaTypeRid, leafWithChangedChain)));

        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.TypeNameMismatch,
            Certificate(chainCatalog, OuterTypeRid).Issue);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.EnclosingTypeMismatch,
            Certificate(chainCatalog, EqualTypeRid).Issue);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityIssue.EnclosingTypeMismatch,
            Certificate(chainCatalog, DeltaTypeRid).Issue);
    }

    /// <summary>
    /// Proves incomplete and surplus candidate vectors and non-exact or invalid authorities stop without a certificate
    /// prefix, while a complete null vector remains exact.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Catalog_vector_and_authority_stops_are_prefix_free()
    {
        var authority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var typeCount = authority.TypeDefinitions.Length;
        var absent = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            CandidateSlots(authority));
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Exact, absent.ResultKind);
        Assert.All(absent.Certificates, static certificate =>
            Assert.Equal(MetadataW7TypeDefinitionCompatibilityResultKind.CandidateAbsent,
                certificate.ResultKind));

        var defaultVector = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(authority, default);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact, defaultVector.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.CandidateSlotsIncomplete,
            defaultVector.Issue);
        Assert.Empty(defaultVector.Certificates);
        Assert.Equal(0, defaultVector.ObservedCount);
        Assert.Equal(TypeToken(1), defaultVector.RelatedMetadataToken);
        Assert.Null(defaultVector.CompleteCertificateOrDefault(TypeToken(1)));

        var shortVector = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            ImmutableArray.CreateRange(new StaticFieldTypeDefinitionIdentity?[typeCount - 1]));
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact, shortVector.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.CandidateSlotsIncomplete,
            shortVector.Issue);
        Assert.Empty(shortVector.Certificates);
        Assert.Equal(typeCount - 1, shortVector.ObservedCount);
        Assert.Equal(TypeToken(typeCount), shortVector.RelatedMetadataToken);

        var surplusVector = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            ImmutableArray.CreateRange(new StaticFieldTypeDefinitionIdentity?[typeCount + 1]));
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid, surplusVector.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.CandidateSlotCountConflict,
            surplusVector.Issue);
        Assert.Empty(surplusVector.Certificates);
        Assert.Equal(typeCount + 1, surplusVector.ObservedCount);
        Assert.Null(surplusVector.RelatedMetadataToken);

        var nonExactAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: false,
            omitGenericParameterRows: true).DefinitionAuthority;
        var nonExact = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(nonExactAuthority, default);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.NonExact, nonExact.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.DefinitionAuthorityNonExact,
            nonExact.Issue);
        Assert.Equal(nonExactAuthority.ReachedBound, nonExact.ReachedBound);
        Assert.Equal(nonExactAuthority.ObservedCount, nonExact.ObservedCount);
        Assert.Empty(nonExact.Certificates);

        var invalidAuthority = W8CompilerNameMappingContractTests.BuildScenario(
            usePointers: true,
            invalidModuleName: true).DefinitionAuthority;
        var invalid = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(invalidAuthority, default);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogResultKind.Invalid, invalid.ResultKind);
        Assert.Equal(MetadataW7TypeDefinitionCompatibilityCatalogIssue.DefinitionAuthorityInvalid,
            invalid.Issue);
        Assert.Equal(invalidAuthority.Issue, invalid.DefinitionAuthorityIssue);
        Assert.Equal(invalidAuthority.RelatedMetadataToken, invalid.RelatedMetadataToken);
        Assert.Empty(invalid.Certificates);
    }

    /// <summary>
    /// Proves canonical replay, defensive copies, fixed-size row references, and private certificate issuance for the
    /// catalog family.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Catalog_is_replayable_bounded_immutable_and_the_only_certificate_issuer()
    {
        var firstAuthority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var replayAuthority = W8CompilerNameMappingContractTests.BuildScenario(usePointers: false).DefinitionAuthority;
        var firstCandidates = WideCandidates(firstAuthority);
        var replayCandidates = WideCandidates(replayAuthority);
        var first = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(firstAuthority, firstCandidates);
        var replay = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(replayAuthority, replayCandidates);
        var originalBytes = first.CanonicalBytes;
        var originalSha = first.Sha256;

        var returnedCertificates = first.Certificates;
        ImmutableCollectionsMarshal.AsArray(returnedCertificates)![0] = returnedCertificates[^1];
        var returnedBytes = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x5A;
        var outerCertificate = Certificate(first, OuterTypeRid);
        var returnedFieldTokens = outerCertificate.FieldDefinitionTokens;
        ImmutableCollectionsMarshal.AsArray(returnedFieldTokens)![0] = FieldToken(1);

        AssertCanonicalReplay(first, replay);
        Assert.Equal(originalSha, first.Sha256);
        Assert.Equal("023a7f33cd2f4294f2a40d1854b44f5e8638952d9777b92cf5fc629d67bb7701", originalSha);
        Assert.True(originalBytes.AsSpan().SequenceEqual(first.CanonicalBytes.AsSpan()));
        Assert.Equal(TypeToken(1), first.Certificates[0].AuthorityTypeDefinition.TypeDefinitionToken);
        Assert.Equal([FieldToken(2)], outerCertificate.FieldDefinitionTokens.ToArray());

        var wide = Certificate(first, WideTypeRid);
        var wider = Certificate(first, WiderTypeRid);
        Assert.True(wide.IsCompatible);
        Assert.True(wider.IsCompatible);
        Assert.Equal(wide.CanonicalBytes.Length, wider.CanonicalBytes.Length);
        Assert.True(wider.AuthorityTypeDefinition.CanonicalBytes.Length >
                    wide.AuthorityTypeDefinition.CanonicalBytes.Length);
        Assert.Equal(-1, wide.CanonicalBytes.AsSpan().IndexOf(
            wide.AuthorityTypeDefinition.CanonicalBytes.AsSpan()));
        Assert.Equal(-1, wider.CanonicalBytes.AsSpan().IndexOf(
            wider.AuthorityTypeDefinition.CanonicalBytes.AsSpan()));
        Assert.Equal(-1, wide.CanonicalBytes.AsSpan().IndexOf(wide.Candidate!.CanonicalBytes.AsSpan()));
        Assert.Equal(-1, wider.CanonicalBytes.AsSpan().IndexOf(wider.Candidate!.CanonicalBytes.AsSpan()));

        Assert.Throws<ArgumentException>(() =>
            MetadataW7TypeDefinitionCompatibilityCertificateIdentity.Create(
                new object(),
                outerCertificate.AuthorityTypeDefinition,
                outerCertificate.Candidate,
                MetadataW7TypeDefinitionCompatibilityIssue.None));
        Assert.False(MetadataW7TypeDefinitionCompatibilityCatalogIdentity.OwnsCertificateMintCapability(
            new object()));
        Assert.Empty(typeof(MetadataW7TypeDefinitionCompatibilityCertificateIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(MetadataW7TypeDefinitionCompatibilityCertificateIdentity).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal(
            ["Create"],
            typeof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(static method => method.Name)
                .ToArray());
        var createParameters = typeof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity)
            .GetMethod(nameof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create))!
            .GetParameters();
        Assert.Equal(typeof(MetadataDefinitionAuthorityCatalogIdentity), createParameters[0].ParameterType);
        Assert.Equal(typeof(ImmutableArray<StaticFieldTypeDefinitionIdentity>),
            createParameters[1].ParameterType);
    }

    /// <summary>Proves every public compatibility-catalog type and method has emitted XML documentation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Compatibility_public_surface_has_draft_XML()
    {
        var assembly = typeof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataW7TypeDefinitionCompatibilityResultKind),
            typeof(MetadataW7TypeDefinitionCompatibilityIssue),
            typeof(MetadataW7MemberOwnershipDomainKind),
            typeof(MetadataW7TypeDefinitionCompatibilityCatalogResultKind),
            typeof(MetadataW7TypeDefinitionCompatibilityCatalogIssue),
            typeof(MetadataW7TypeDefinitionCompatibilityCertificateIdentity),
            typeof(MetadataW7TypeDefinitionCompatibilityCatalogIdentity),
        };

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

            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static MetadataW7TypeDefinitionCompatibilityCertificateIdentity CompareOuter(
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var candidate = Candidate(AuthorityType(authority, OuterTypeRid));
        var catalog = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            CandidateSlots(authority, (OuterTypeRid, candidate)));
        return Certificate(catalog, OuterTypeRid);
    }

    private static StaticFieldTypeDefinitionIdentity MutatedOuter(
        MetadataTypeDefinitionAuthorityIdentity authority,
        CandidateMutation mutation) =>
        mutation switch
        {
            CandidateMutation.Source => Candidate(
                authority,
                metadataModule: W8CompilerNameMappingContractTests.CreateMetadataModule(
                    moduleAddress: 0xB000,
                    digestCharacter: 'b')),
            CandidateMutation.Token => Candidate(authority, typeDefinitionToken: TypeToken(13)),
            CandidateMutation.Namespace => Candidate(authority, namespaceName: "Synthetic.Changed"),
            CandidateMutation.Name => Candidate(authority, typeName: "Changed`2"),
            CandidateMutation.Attributes => Candidate(
                authority,
                typeAttributes: (int)(TypeAttributes.Public | TypeAttributes.Abstract)),
            CandidateMutation.Extends => Candidate(authority, extendsMetadataToken: 0x01000001),
            CandidateMutation.FieldStart => Candidate(authority, fieldListRowId: 1),
            CandidateMutation.FieldEnd => Candidate(authority, fieldListEndExclusiveRowId: 4),
            CandidateMutation.MethodStart => Candidate(authority, methodListRowId: 1),
            CandidateMutation.MethodEnd => Candidate(authority, methodListEndExclusiveRowId: 4),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> WideCandidates(
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var outer = Candidate(AuthorityType(authority, OuterTypeRid));
        var wide = Candidate(AuthorityType(authority, WideTypeRid));
        var wider = Candidate(AuthorityType(authority, WiderTypeRid));
        return CandidateSlots(
            authority,
            (OuterTypeRid, outer),
            (WideTypeRid, wide),
            (WiderTypeRid, wider));
    }

    private static StaticFieldTypeDefinitionIdentity Candidate(
        MetadataTypeDefinitionAuthorityIdentity authority,
        StaticFieldTypeDefinitionIdentity? enclosingType = null,
        StaticFieldMetadataModuleIdentity? metadataModule = null,
        int? typeDefinitionToken = null,
        int? fieldListRowId = null,
        int? fieldListEndExclusiveRowId = null,
        int? methodListRowId = null,
        int? methodListEndExclusiveRowId = null,
        string? namespaceName = null,
        string? typeName = null,
        int? typeAttributes = null,
        int? genericParameterCount = null,
        int? introducedGenericArity = null,
        int? extendsMetadataToken = null)
    {
        var row = authority.TableRow;
        var totalArity = genericParameterCount ?? authority.TotalGenericArity;
        var introducedArity = introducedGenericArity ??
                              totalArity - (enclosingType?.GenericParameterCount ?? 0);
        return StaticFieldTypeDefinitionIdentity.Create(
            metadataModule ?? authority.SourceEnds.SourceModule,
            typeDefinitionToken ?? authority.TypeDefinitionToken,
            fieldListRowId ?? row.Observation.FieldListRowId,
            fieldListEndExclusiveRowId ?? row.FieldListEndExclusiveRowId,
            methodListRowId ?? row.Observation.MethodListRowId,
            methodListEndExclusiveRowId ?? row.MethodListEndExclusiveRowId,
            namespaceName ?? authority.NamespaceName,
            typeName ?? authority.TypeName,
            typeAttributes ?? row.Observation.TypeAttributes,
            totalArity,
            introducedArity,
            extendsMetadataToken ?? row.Observation.ExtendsMetadataToken,
            enclosingType);
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> CandidateSlots(
        MetadataDefinitionAuthorityCatalogIdentity authority,
        params (int TypeDefinitionRid, StaticFieldTypeDefinitionIdentity Candidate)[] supplied)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            builder.Add(null);
        }
        foreach (var (typeDefinitionRid, candidate) in supplied)
        {
            builder[typeDefinitionRid - 1] = candidate;
        }
        return builder.MoveToImmutable();
    }

    private static MetadataTypeDefinitionAuthorityIdentity AuthorityType(
        MetadataDefinitionAuthorityCatalogIdentity authority,
        int typeDefinitionRid) =>
        authority.TypeDefinitions[typeDefinitionRid - 1];

    private static MetadataW7TypeDefinitionCompatibilityCertificateIdentity Certificate(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity catalog,
        int typeDefinitionRid) =>
        Assert.IsType<MetadataW7TypeDefinitionCompatibilityCertificateIdentity>(
            catalog.CompleteCertificateOrDefault(TypeToken(typeDefinitionRid)));

    private static void AssertCanonicalReplay(
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity first,
        MetadataW7TypeDefinitionCompatibilityCatalogIdentity replay)
    {
        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
    }

    private static int TypeToken(int rowId) => 0x02000000 | rowId;

    private static int FieldToken(int rowId) => 0x04000000 | rowId;

    private static int MethodToken(int rowId) => 0x06000000 | rowId;

    /// <summary>Identifies the one W7 candidate scalar changed by a synthetic comparison case.</summary>
    public enum CandidateMutation
    {
        /// <summary>Changes the exact metadata module.</summary>
        Source,

        /// <summary>Changes the TypeDef token.</summary>
        Token,

        /// <summary>Changes the decoded namespace.</summary>
        Namespace,

        /// <summary>Changes the uninterpreted metadata name.</summary>
        Name,

        /// <summary>Changes raw TypeAttributes bits.</summary>
        Attributes,

        /// <summary>Changes the decoded Extends token.</summary>
        Extends,

        /// <summary>Changes the raw FieldList start.</summary>
        FieldStart,

        /// <summary>Changes the derived FieldList end.</summary>
        FieldEnd,

        /// <summary>Changes the raw MethodList start.</summary>
        MethodStart,

        /// <summary>Changes the derived MethodList end.</summary>
        MethodEnd,
    }
}
