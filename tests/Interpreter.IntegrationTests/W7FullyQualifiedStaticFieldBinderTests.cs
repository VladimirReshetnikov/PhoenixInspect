using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises context-independent W7 binding over complete synthetic multi-module metadata evidence.</summary>
public sealed class W7FullyQualifiedStaticFieldBinderTests
{
    private const string NamespaceName = "Interpreter.IntegrationTests";
    private const string TypeName = nameof(W7FullyQualifiedSyntheticTarget);

    /// <summary>Proves all admitted declaration value shapes bind through the same counted metadata path.</summary>
    [Theory]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.Int32Value), StaticFieldDeclaredValueKind.Int32)]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.StringValue), StaticFieldDeclaredValueKind.String)]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.ObjectValue), StaticFieldDeclaredValueKind.Object)]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.ReferenceValue), StaticFieldDeclaredValueKind.ManagedReference)]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.NullableValue), StaticFieldDeclaredValueKind.NullableInt32)]
    public void GlobalQualifiedBindingProjectsEveryAdmittedValueShape(
        string fieldName,
        StaticFieldDeclaredValueKind expectedKind)
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"global::{NamespaceName}.{TypeName}.{fieldName}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.Equal(expectedKind, result.SelectedDeclaration!.DeclaredValueKind);
        Assert.Equal(NamespaceName, result.SelectedDeclaration.NamespaceName);
        Assert.Equal(TypeName, result.SelectedDeclaration.TypeName);
        Assert.Equal(fieldName, result.SelectedDeclaration.FieldName);
        Assert.False(result.ConsultedContext.CurrentNamespaceConsulted);
        Assert.False(result.ConsultedContext.ImportsConsulted);
        Assert.Equal(source.Modules.Length, result.ModulesConsidered);
        Assert.Equal(source.Modules.Length, result.ExactModulesSearched);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves ordinary dot qualification and a competing suffix split remain context independent.</summary>
    [Fact]
    public void DotQualifiedBindingAndSuffixSplitRemainContextIndependent()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.ReferenceValue)}.Value");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.Equal(StaticFieldNameExpansionKind.DotQualified, Assert.Single(result.Candidates).Origins[0].Kind);
        Assert.Equal(StaticFieldSuffixShape.DirectMember, result.SelectedShape!.SuffixShape);
        Assert.Equal(nameof(W7FullyQualifiedSyntheticTarget.ReferenceValue), result.SelectedDeclaration!.FieldName);
        Assert.True(result.ExpansionCount >= 2);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves a bare type name is deferred without reading any metadata image.</summary>
    [Fact]
    public void BareTypeRequiresContextAndDoesNotReadMetadata()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Unsupported, result.Status);
        Assert.Equal(StaticFieldBindingIssue.ExpansionUnsupported, result.Issue);
        Assert.Empty(result.ModuleSearchFacts);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(0, pair.Value));
    }

    /// <summary>Proves an exact selected-frame namespace binds a bare type while unavailable PDB remains additive.</summary>
    [Fact]
    public void CurrentNamespaceBindsBareTypeWithoutPortablePdb()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");
        var context = CreateContext(
            source,
            exactFrame: true,
            declaringNamespace: NamespaceName);

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.True(result.ConsultedContext.CurrentNamespaceConsulted);
        Assert.False(result.ConsultedContext.ImportsConsulted);
        Assert.Equal(
            StaticFieldNameExpansionKind.CurrentNamespace,
            Assert.Single(result.Candidates).Origins[0].Kind);
        Assert.Equal(nameof(W7FullyQualifiedSyntheticTarget.Int32Value), result.SelectedDeclaration!.FieldName);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves a current-namespace miss cannot manufacture exact absence when imports were unavailable.</summary>
    [Fact]
    public void UnavailablePortablePdbStopsAfterExactCurrentNamespaceMiss()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");
        var context = CreateContext(
            source,
            exactFrame: true,
            declaringNamespace: "Synthetic.Client");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Unavailable, result.Status);
        Assert.Equal(StaticFieldBindingIssue.ContextUnavailable, result.Issue);
        Assert.True(result.ConsultedContext.CurrentNamespaceConsulted);
        Assert.True(result.ConsultedContext.ImportsConsulted);
        Assert.Equal(DumpContextEvidenceStatus.Unavailable, result.ConsultedContext.ImportEvidenceStatus);
        Assert.True(result.ModuleCatalogExhaustive);
        Assert.NotEmpty(result.ModuleSearchFacts);
        Assert.Null(result.SelectedDeclaration);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves unavailable selected-frame evidence stops a bare lookup before any metadata image is read.</summary>
    [Fact]
    public void UnavailableFrameStopsBareBindingBeforeMetadataSearch()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");
        var context = CreateContext(source, exactFrame: false, declaringNamespace: NamespaceName);

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Unavailable, result.Status);
        Assert.Equal(StaticFieldBindingIssue.ContextUnavailable, result.Issue);
        Assert.Empty(result.ModuleSearchFacts);
        Assert.Null(result.SelectedDeclaration);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(0, pair.Value));
    }

    /// <summary>Proves literal global qualification bypasses even unavailable selected-frame/PDB evidence.</summary>
    [Fact]
    public void GlobalQualificationBypassesUnavailableContextCanonically()
    {
        var descriptor = Parse(
            $"global::{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");
        using var contextualSource = SyntheticMetadataSource.Create();
        using var independentSource = SyntheticMetadataSource.Create();
        var unavailable = CreateContext(
            contextualSource,
            exactFrame: false,
            declaringNamespace: "Ignored.Namespace");

        var contextual = StaticFieldContextualBinder.Bind(contextualSource, descriptor, unavailable);
        var independent = StaticFieldFullyQualifiedBinder.Bind(independentSource, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Exact, contextual.Status);
        Assert.Equal(independent.Sha256, contextual.Sha256);
        Assert.Null(contextual.ConsultedContext.ConsultedFrameEvidence);
    }

    /// <summary>Proves one exact active namespace import contributes a source-attributed bare-type candidate.</summary>
    [Fact]
    public void NamespaceImportBindsBareTypeFromExactPortablePdbContext()
    {
        using var source = SyntheticMetadataSource.Create();
        var import = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001,
            ordinal: 0,
            rawKind: 1,
            NamespaceName,
            [0x01]);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [import]);
        var descriptor = Parse($"{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.True(result.ConsultedContext.CurrentNamespaceConsulted);
        Assert.True(result.ConsultedContext.ImportsConsulted);
        Assert.Equal(import, Assert.Single(result.ConsultedContext.ConsultedImports));
        Assert.Equal(
            StaticFieldNameExpansionKind.NamespaceImport,
            Assert.Single(result.Candidates).Origins[0].Kind);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves a TypeDef-bearing type alias is resolved from the selected-frame module to its exact owner.</summary>
    [Fact]
    public void TypeAliasCarriesCountedSameModuleResolution()
    {
        using var source = SyntheticMetadataSource.Create();
        const string alias = "IncidentStatics";
        var targetToken = source.FindTypeDefinitionToken(NamespaceName, TypeName);
        var import = DumpPortablePdbImportFact.TypeAlias(
            0x35000001,
            ordinal: 0,
            rawKind: 9,
            alias,
            $"{NamespaceName}.{TypeName}",
            targetToken,
            [0x09]);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [import]);
        var descriptor = Parse($"{alias}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        var origin = Assert.Single(result.Candidates).Origins.Single(static item =>
            item.Kind == StaticFieldNameExpansionKind.TypeAlias);
        Assert.Equal(import.Sha256, origin.ContextFactSha256);
        Assert.Equal(targetToken, origin.ReferenceResolution!.SourceTypeToken);
        Assert.Equal(targetToken, origin.ReferenceResolution.TargetTypeDefinitionToken);
        Assert.Equal(source.TargetContent, origin.ReferenceResolution.TargetModuleContent);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves a TypeRef-bearing alias retains its complete counted reference/forwarder relation.</summary>
    [Fact]
    public void TypeAliasCarriesClosedTypeReferenceResolution()
    {
        using var source = SyntheticMetadataSource.Create();
        const string alias = "TextType";
        var targetToken = source.FindTypeReferenceToken("System", "String");
        var import = DumpPortablePdbImportFact.TypeAlias(
            0x35000001,
            ordinal: 0,
            rawKind: 9,
            alias,
            "System.String",
            targetToken,
            [0x09, 0x01]);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [import]);
        var descriptor = Parse($"{alias}.Empty");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.Equal("System", result.SelectedDeclaration!.NamespaceName);
        Assert.Equal("String", result.SelectedDeclaration.TypeName);
        Assert.Equal("Empty", result.SelectedDeclaration.FieldName);
        var origin = Assert.Single(result.Candidates).Origins.Single(static item =>
            item.Kind == StaticFieldNameExpansionKind.TypeAlias);
        Assert.Equal(targetToken, origin.ReferenceResolution!.SourceTypeToken);
        Assert.NotNull(origin.ReferenceResolution.TypeReferenceResolution);
        Assert.Equal(
            origin.ReferenceResolution.TargetTypeDefinitionToken,
            result.SelectedDeclaration.TypeDefinitionToken);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves an assembly-qualified namespace import resolves through loaded assembly identity and forwarding.</summary>
    [Fact]
    public void AssemblyNamespaceImportNarrowsToResolvedAssemblyType()
    {
        using var source = SyntheticMetadataSource.Create();
        var assemblyReferenceToken = source.FindAssemblyReferenceToken("System.Runtime");
        var import = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001,
            ordinal: 0,
            rawKind: 2,
            "System",
            [0x02, 0x23],
            assemblyReferenceToken);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [import]);
        var descriptor = Parse("String.Empty");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        var origin = Assert.Single(result.Candidates).Origins.Single(static item =>
            item.Kind == StaticFieldNameExpansionKind.NamespaceImport);
        Assert.Equal(assemblyReferenceToken, origin.ReferenceResolution!.AssemblyReferenceToken);
        Assert.Null(origin.ReferenceResolution.SourceTypeToken);
        Assert.Equal(result.SelectedDeclaration!.Module, origin.ReferenceResolution.TargetModule);
        Assert.Equal(result.SelectedDeclaration.TypeDefinitionToken, origin.ReferenceResolution.TargetTypeDefinitionToken);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves a namespace alias contributes a qualified interpretation without replacing dot qualification.</summary>
    [Fact]
    public void NamespaceAliasAddsQualifiedInterpretation()
    {
        using var source = SyntheticMetadataSource.Create();
        const string alias = "Incident";
        var import = DumpPortablePdbImportFact.NamespaceAlias(
            0x35000001,
            ordinal: 0,
            rawKind: 7,
            alias,
            NamespaceName,
            [0x07]);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [import]);
        var descriptor = Parse(
            $"{alias}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.True(result.ConsultedContext.CurrentNamespaceConsulted);
        Assert.True(result.ConsultedContext.ImportsConsulted);
        Assert.Contains(result.Expansions, static expansion =>
            expansion.Kind == StaticFieldNameExpansionKind.DotQualified);
        Assert.Equal(
            StaticFieldNameExpansionKind.NamespaceAlias,
            Assert.Single(result.Candidates).Origins[0].Kind);
    }

    /// <summary>Proves two imported namespaces producing distinct declarations remain ambiguity, never import order.</summary>
    [Fact]
    public void CompetingNamespaceImportsRemainAmbiguous()
    {
        using var source = SyntheticMetadataSource.Create();
        var first = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001, 0, 1, "Synthetic.Context.One", [0x01]);
        var second = DumpPortablePdbImportFact.NamespaceImport(
            0x35000001, 1, 1, "Synthetic.Context.Two", [0x01]);
        var context = CreateExactPortablePdbContext(source, "Synthetic.Client", [first, second]);
        var descriptor = Parse($"SharedStatics.{nameof(Synthetic.Context.One.SharedStatics.Value)}");

        var result = StaticFieldContextualBinder.Bind(source, descriptor, context);

        Assert.Equal(StaticFieldBindingStatus.Ambiguous, result.Status);
        Assert.Equal(StaticFieldBindingIssue.MultipleCandidates, result.Issue);
        Assert.Equal(2, result.DistinctCandidateCount);
        Assert.Null(result.SelectedDeclaration);
        Assert.Equal(2, result.ConsultedContext.ConsultedImports.Length);
    }

    /// <summary>Proves exact absence requires exhaustive search of every physical module.</summary>
    [Fact]
    public void ExactAbsenceSearchesEveryPhysicalModuleAndRetainsNoCandidate()
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"global::{NamespaceName}.{TypeName}.MissingValue");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Absent, result.Status);
        Assert.Equal(StaticFieldBindingIssue.DeclarationAbsent, result.Issue);
        Assert.True(result.SearchExhaustive);
        Assert.Empty(result.Candidates);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves located literal and thread-local storage shapes become explicit rejected evidence.</summary>
    [Theory]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.ConstantValue))]
    [InlineData(nameof(W7FullyQualifiedSyntheticTarget.ThreadValue))]
    public void LocatedUnsupportedStorageShapeIsNeverSelected(string fieldName)
    {
        using var source = SyntheticMetadataSource.Create();
        var descriptor = Parse($"global::{NamespaceName}.{TypeName}.{fieldName}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Unsupported, result.Status);
        Assert.Equal(StaticFieldBindingIssue.DeclarationShapeUnsupported, result.Issue);
        var rejected = Assert.Single(result.RejectedDeclarations);
        Assert.Equal(fieldName, rejected.MemberName);
        Assert.Equal(StaticFieldRejectedMemberKind.FieldDefinition, rejected.MemberKind);
        Assert.Null(result.SelectedDeclaration);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves byte-identical metadata in distinct physical module instances remains ambiguous.</summary>
    [Fact]
    public void DuplicatePhysicalAssemblyLoadsRemainAmbiguous()
    {
        using var source = SyntheticMetadataSource.Create(duplicateTargetModule: true);
        var descriptor = Parse(
            $"global::{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Ambiguous, result.Status);
        Assert.Equal(StaticFieldBindingIssue.MultipleCandidates, result.Issue);
        Assert.Equal(2, result.DistinctCandidateCount);
        Assert.Equal(2, result.Candidates.Select(static candidate => candidate.Declaration.Module.Sha256).Distinct().Count());
        Assert.Null(result.SelectedDeclaration);
    }

    /// <summary>Proves a runtime assembly-address relation admits declarations physically stored in a netmodule.</summary>
    [Fact]
    public void NetmoduleDeclarationBindsThroughExactManifestContainment()
    {
        using var source = SyntheticMetadataSource.CreateNetmoduleFixture();
        var descriptor = Parse("global::Synthetic.Net.Holder.Value");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Exact, result.Status);
        Assert.Equal("Synthetic.Net", result.SelectedDeclaration!.NamespaceName);
        Assert.Equal("Holder", result.SelectedDeclaration.TypeName);
        Assert.Equal("Value", result.SelectedDeclaration.FieldName);
        var metadataModule = result.SelectedDeclaration.FieldDefinition.DeclaringType.MetadataModule;
        Assert.False(metadataModule.IsManifestModule);
        Assert.Equal(
            StaticFieldMetadataModuleContainmentKind.ClrMdAssemblyModule,
            metadataModule.ContainmentKind);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves partial, unavailable, and invalid metadata can never produce a selected declaration.</summary>
    [Theory]
    [InlineData((int)StaticFieldMetadataImageStatus.Partial, StaticFieldBindingStatus.Partial, StaticFieldBindingIssue.ModuleSearchPartial)]
    [InlineData((int)StaticFieldMetadataImageStatus.Unavailable, StaticFieldBindingStatus.Unavailable, StaticFieldBindingIssue.ModuleUnavailable)]
    [InlineData((int)StaticFieldMetadataImageStatus.Invalid, StaticFieldBindingStatus.Invalid, StaticFieldBindingIssue.MetadataInvalid)]
    public void NonExactModuleEvidenceCannotProduceASelection(
        int rawStatus,
        StaticFieldBindingStatus expectedStatus,
        StaticFieldBindingIssue expectedIssue)
    {
        using var source = SyntheticMetadataSource.Create(targetStatus: (StaticFieldMetadataImageStatus)rawStatus);
        var descriptor = Parse(
            $"global::{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedIssue, result.Issue);
        Assert.Null(result.SelectedDeclaration);
        Assert.Contains(result.ModuleSearchFacts, fact => fact.Status != StaticFieldModuleSearchStatus.Exact);
        Assert.All(source.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    /// <summary>Proves conflicting complete identities are retained without choosing either image.</summary>
    [Fact]
    public void ConflictingModuleContentsAreRetainedWithoutChoosingEither()
    {
        using var source = SyntheticMetadataSource.Create(targetStatus: StaticFieldMetadataImageStatus.Conflict);
        var descriptor = Parse(
            $"global::{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.Int32Value)}");

        var result = StaticFieldFullyQualifiedBinder.Bind(source, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Conflict, result.Status);
        Assert.Equal(StaticFieldBindingIssue.ModuleConflict, result.Issue);
        var conflict = Assert.Single(result.ModuleSearchFacts.Where(
            static fact => fact.Status == StaticFieldModuleSearchStatus.Conflict));
        Assert.Equal(2, conflict.ModuleContents.Length);
        Assert.Null(result.SelectedDeclaration);
    }

    /// <summary>Proves repeat acquisition is canonical and reads every module exactly once per operation.</summary>
    [Fact]
    public void ReplayIsCanonicalAndMetadataImagesAreReadOncePerOperation()
    {
        var expression = $"global::{NamespaceName}.{TypeName}.{nameof(W7FullyQualifiedSyntheticTarget.NullableValue)}";
        var descriptor = Parse(expression);
        using var firstSource = SyntheticMetadataSource.Create();
        using var secondSource = SyntheticMetadataSource.Create();

        var first = StaticFieldFullyQualifiedBinder.Bind(firstSource, descriptor);
        var second = StaticFieldFullyQualifiedBinder.Bind(secondSource, descriptor);

        Assert.Equal(StaticFieldBindingStatus.Exact, first.Status);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));
        Assert.All(firstSource.ReadCounts, static pair => Assert.Equal(1, pair.Value));
        Assert.All(secondSource.ReadCounts, static pair => Assert.Equal(1, pair.Value));
    }

    private static StaticFieldExpressionDescriptor Parse(string expression)
    {
        var syntax = StaticFieldExpressionParser.Parse(expression);
        Assert.Equal(StaticFieldSyntaxStatus.Accepted, syntax.Status);
        return Assert.IsType<StaticFieldExpressionDescriptor>(syntax.Descriptor);
    }

    private static DumpExpressionBindingContext CreateContext(
        SyntheticMetadataSource source,
        bool exactFrame,
        string declaringNamespace)
    {
        var selector = DumpSelectedFrameSelector.Create(source.Snapshot, threadOrdinal: 0, frameOrdinal: 0);
        var frame = exactFrame
            ? DumpSelectedFrameObservation.Exact(
                DumpSelectedFrameIdentity.Create(
                    selector,
                    managedThreadId: 17,
                    runtimeThreadAddress: 0x7100,
                    stackPointer: 0x7FFF_1000,
                    source.TargetRuntimeModule,
                    source.TargetContent,
                    methodDefinitionToken: 0x06000001,
                    declaringTypeDefinitionToken: 0x02000001,
                    declaringNamespace,
                    DumpInstructionLocation.Create(0x0040_1234, ilOffset: 0)),
                ImmutableArray<EvaluationDeterministicBound>.Empty)
            : DumpSelectedFrameObservation.Unavailable(
                selector,
                DumpContextEvidenceIssue.FrameUnavailable,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
        var pdb = DumpPortablePdbObservation.Unavailable(
            DumpPortablePdbEvidenceSource.ForSnapshot(source.Snapshot),
            exactFrame
                ? DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable
                : DumpContextEvidenceIssue.PrerequisiteUnavailable,
            ImmutableArray<EvaluationDeterministicBound>.Empty);
        return DumpExpressionBindingContext.Acquire(source.Snapshot, frame, pdb);
    }

    private static DumpExpressionBindingContext CreateExactPortablePdbContext(
        SyntheticMetadataSource source,
        string declaringNamespace,
        ImmutableArray<DumpPortablePdbImportFact> imports)
    {
        var selector = DumpSelectedFrameSelector.Create(source.Snapshot, threadOrdinal: 0, frameOrdinal: 0);
        var frame = DumpSelectedFrameIdentity.Create(
            selector,
            managedThreadId: 17,
            runtimeThreadAddress: 0x7100,
            stackPointer: 0x7FFF_1000,
            source.TargetRuntimeModule,
            source.TargetContent,
            methodDefinitionToken: 0x06000001,
            declaringTypeDefinitionToken: 0x02000001,
            declaringNamespace,
            DumpInstructionLocation.Create(0x0040_1234, ilOffset: 0));
        var debugIdentity = DumpPortablePdbDebugIdentity.Create(
            Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            stamp: 0x1020_3040);
        var moduleDebugIdentity = DumpModulePortablePdbDebugIdentity.Create(
            source.TargetRuntimeModule,
            source.TargetContent,
            debugIdentity);
        var artifact = DumpPortablePdbArtifactIdentity.Create(
            DumpPortablePdbContentIdentity.Create(4_096, new string('d', 64)),
            debugIdentity);
        var importScope = DumpPortablePdbImportScopeIdentity.Create(
            0x35000001,
            parentImportScopeToken: null,
            nestingDepth: 0,
            imports);
        var localScope = DumpPortablePdbLocalScopeIdentity.Create(
            0x32000001,
            0x06000001,
            0x35000001,
            startOffset: 0,
            length: 16,
            nestingDepth: 0);
        var facts = DumpPortablePdbContextFacts.Acquire(
            frame,
            moduleDebugIdentity,
            artifact,
            methodDebugInformationToken: 0x31000001,
            document: null,
            [localScope],
            [importScope]);
        return DumpExpressionBindingContext.Acquire(
            source.Snapshot,
            DumpSelectedFrameObservation.Exact(
                frame,
                ImmutableArray<EvaluationDeterministicBound>.Empty),
            DumpPortablePdbObservation.Exact(
                facts,
                ImmutableArray<EvaluationDeterministicBound>.Empty));
    }

    private sealed class SyntheticMetadataSource : IStaticFieldMetadataBindingSource, IDisposable
    {
        private static readonly ClrmdSnapshotIdentity SharedSnapshot = new(new string('a', 64));
        private readonly ImmutableDictionary<string, StaticFieldMetadataImageObservation> observations;
        private readonly Dictionary<string, int> readCounts;

        private SyntheticMetadataSource(
            ImmutableArray<StaticFieldMetadataModuleInput> modules,
            StaticFieldMetadataModuleInput coreLibraryModule,
            ImmutableDictionary<string, StaticFieldMetadataImageObservation> observations)
        {
            Modules = modules;
            CoreLibraryModule = coreLibraryModule;
            this.observations = observations;
            readCounts = modules.ToDictionary(static module => module.Module.Sha256, static _ => 0, StringComparer.Ordinal);
        }

        public ClrmdSnapshotIdentity Snapshot => SharedSnapshot;

        public int PointerWidth => sizeof(ulong);

        public ImmutableArray<StaticFieldMetadataModuleInput> Modules { get; }

        public StaticFieldMetadataModuleInput CoreLibraryModule { get; }

        internal IReadOnlyDictionary<string, int> ReadCounts => readCounts;

        internal ClrmdRuntimeModuleIdentity TargetRuntimeModule
        {
            get
            {
                var module = Modules[2].Module;
                return new ClrmdRuntimeModuleIdentity(
                    Snapshot,
                    module.ApplicationDomainAddress,
                    module.ModuleAddress,
                    module.ImageBase,
                    module.ImageSize);
            }
        }

        internal ModuleContentIdentity TargetContent => observations[Modules[2].Module.Sha256].ExactContent!;

        internal int FindTypeDefinitionToken(string namespaceName, string typeName)
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(
                observations[Modules[2].Module.Sha256].Bytes);
            var reader = provider.GetMetadataReader();
            return MetadataTokens.GetToken(reader.TypeDefinitions.Single(handle =>
            {
                var definition = reader.GetTypeDefinition(handle);
                return definition.GetDeclaringType().IsNil &&
                    string.Equals(reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal) &&
                    string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal);
            }));
        }

        internal int FindTypeReferenceToken(string namespaceName, string typeName)
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(
                observations[Modules[2].Module.Sha256].Bytes);
            var reader = provider.GetMetadataReader();
            return MetadataTokens.GetToken(reader.TypeReferences.First(handle =>
            {
                var reference = reader.GetTypeReference(handle);
                return string.Equals(reader.GetString(reference.Namespace), namespaceName, StringComparison.Ordinal) &&
                    string.Equals(reader.GetString(reference.Name), typeName, StringComparison.Ordinal);
            }));
        }

        internal int FindAssemblyReferenceToken(string assemblyName)
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(
                observations[Modules[2].Module.Sha256].Bytes);
            var reader = provider.GetMetadataReader();
            return MetadataTokens.GetToken(reader.AssemblyReferences.Single(handle =>
                string.Equals(
                    reader.GetString(reader.GetAssemblyReference(handle).Name),
                    assemblyName,
                    StringComparison.Ordinal)));
        }

        internal static SyntheticMetadataSource Create(
            bool duplicateTargetModule = false,
            StaticFieldMetadataImageStatus targetStatus = StaticFieldMetadataImageStatus.Exact)
        {
            var coreLibrary = typeof(object).Assembly;
            var facade = Assembly.Load("System.Runtime");
            var target = typeof(W7FullyQualifiedSyntheticTarget).Assembly;
            var assemblies = new List<Assembly> { coreLibrary, facade, target };
            if (duplicateTargetModule)
            {
                assemblies.Add(target);
            }

            var modules = ImmutableArray.CreateBuilder<StaticFieldMetadataModuleInput>(assemblies.Count);
            var observations = ImmutableDictionary.CreateBuilder<string, StaticFieldMetadataImageObservation>(StringComparer.Ordinal);
            StaticFieldMetadataModuleInput? coreInput = null;
            for (var ordinal = 0; ordinal < assemblies.Count; ordinal++)
            {
                var input = new StaticFieldMetadataModuleInput(
                    StaticFieldModuleInstanceIdentity.Create(
                        SharedSnapshot.Sha256,
                        sizeof(ulong),
                        applicationDomainAddress: 0x1000,
                        moduleAddress: (ulong)(0x2000 + ordinal * 0x1000),
                        imageBase: (ulong)(0x0040_0000 + ordinal * 0x0010_0000),
                        imageSize: 0x0008_0000),
                    ordinal,
                    assemblyAddress: (ulong)(0x9000 + ordinal * 0x1000));
                modules.Add(input);
                var image = ReadMetadataImage(assemblies[ordinal]);
                var exact = StaticFieldMetadataImageObservation.Exact(image.Bytes, image.Content);
                observations.Add(input.Module.Sha256, exact);
                if (ordinal == 0)
                {
                    coreInput = input;
                }
                if (assemblies[ordinal] == target && targetStatus != StaticFieldMetadataImageStatus.Exact)
                {
                    observations[input.Module.Sha256] = targetStatus switch
                    {
                        StaticFieldMetadataImageStatus.Partial => StaticFieldMetadataImageObservation.Partial(),
                        StaticFieldMetadataImageStatus.Unavailable => StaticFieldMetadataImageObservation.Unavailable(),
                        StaticFieldMetadataImageStatus.Invalid => StaticFieldMetadataImageObservation.Invalid(image.Content),
                        StaticFieldMetadataImageStatus.Conflict => StaticFieldMetadataImageObservation.Conflict(
                            ImmutableArray.Create(
                                image.Content,
                                ModuleContentIdentity.FromDigest(
                                    image.Content.Mvid,
                                    image.Content.MetadataLength,
                                    new string('b', 64)))),
                        _ => exact,
                    };
                }
            }

            return new SyntheticMetadataSource(modules.ToImmutable(), coreInput!, observations.ToImmutable());
        }

        internal static SyntheticMetadataSource CreateNetmoduleFixture()
        {
            var core = ReadMetadataImage(typeof(object).Assembly);
            var facade = ReadMetadataImage(Assembly.Load("System.Runtime"));
            var emitted = EmitManifestAndNetmodule();
            var images = new[]
            {
                (core, AssemblyAddress: 0x9000UL),
                (facade, AssemblyAddress: 0xA000UL),
                (ReadMetadataImage(emitted.ManifestImage), AssemblyAddress: 0xB000UL),
                (ReadMetadataImage(emitted.NetmoduleImage), AssemblyAddress: 0xB000UL),
            };
            var modules = ImmutableArray.CreateBuilder<StaticFieldMetadataModuleInput>(images.Length);
            var observations = ImmutableDictionary.CreateBuilder<string, StaticFieldMetadataImageObservation>(StringComparer.Ordinal);
            StaticFieldMetadataModuleInput? coreInput = null;
            for (var ordinal = 0; ordinal < images.Length; ordinal++)
            {
                var input = new StaticFieldMetadataModuleInput(
                    StaticFieldModuleInstanceIdentity.Create(
                        SharedSnapshot.Sha256,
                        sizeof(ulong),
                        applicationDomainAddress: 0x1000,
                        moduleAddress: (ulong)(0x5000 + ordinal * 0x1000),
                        imageBase: (ulong)(0x0080_0000 + ordinal * 0x0010_0000),
                        imageSize: 0x0008_0000),
                    ordinal,
                    images[ordinal].AssemblyAddress);
                modules.Add(input);
                observations.Add(
                    input.Module.Sha256,
                    StaticFieldMetadataImageObservation.Exact(
                        images[ordinal].Item1.Bytes,
                        images[ordinal].Item1.Content));
                coreInput ??= input;
            }

            return new SyntheticMetadataSource(modules.ToImmutable(), coreInput!, observations.ToImmutable());
        }

        public StaticFieldMetadataImageObservation ReadMetadata(StaticFieldMetadataModuleInput module)
        {
            readCounts[module.Module.Sha256]++;
            return observations[module.Module.Sha256];
        }

        public void Dispose()
        {
        }

        private static (ImmutableArray<byte> Bytes, ModuleContentIdentity Content) ReadMetadataImage(Assembly assembly)
        {
            using var stream = File.OpenRead(assembly.Location);
            return ReadMetadataImage(stream);
        }

        private static (ImmutableArray<byte> Bytes, ModuleContentIdentity Content) ReadMetadataImage(
            ImmutableArray<byte> portableExecutable)
        {
            using var stream = new MemoryStream(portableExecutable.ToArray(), writable: false);
            return ReadMetadataImage(stream);
        }

        private static (ImmutableArray<byte> Bytes, ModuleContentIdentity Content) ReadMetadataImage(Stream stream)
        {
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var bytes = ImmutableArray.CreateRange(peReader.GetMetadata().GetContent());
            using var provider = MetadataReaderProvider.FromMetadataImage(bytes);
            var reader = provider.GetMetadataReader();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            return (bytes, ModuleContentIdentity.FromMetadata(mvid, bytes.AsSpan()));
        }

        private static (ImmutableArray<byte> ManifestImage, ImmutableArray<byte> NetmoduleImage)
            EmitManifestAndNetmodule()
        {
            var coreReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var netmodule = CSharpCompilation.Create(
                "Synthetic.Net.Part",
                [CSharpSyntaxTree.ParseText(
                    "namespace Synthetic.Net { public sealed class Holder { public static int Value = 73; } }")],
                [coreReference],
                new CSharpCompilationOptions(
                    OutputKind.NetModule,
                    moduleName: "Synthetic.Net.Part.netmodule",
                    optimizationLevel: OptimizationLevel.Release,
                    deterministic: true));
            using var netmoduleStream = new MemoryStream();
            var netmoduleResult = netmodule.Emit(netmoduleStream);
            if (!netmoduleResult.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, netmoduleResult.Diagnostics));
            }
            var netmoduleImage = ImmutableArray.CreateRange(netmoduleStream.ToArray());

            var moduleReference = MetadataReference.CreateFromImage(
                netmoduleImage,
                new MetadataReferenceProperties(MetadataImageKind.Module));
            var manifest = CSharpCompilation.Create(
                "Synthetic.Net.Container",
                [CSharpSyntaxTree.ParseText(
                    "namespace Synthetic.Net { internal sealed class ManifestAnchor { } }")],
                [coreReference, moduleReference],
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    moduleName: "Synthetic.Net.Container.dll",
                    optimizationLevel: OptimizationLevel.Release,
                    deterministic: true));
            using var manifestStream = new MemoryStream();
            var manifestResult = manifest.Emit(manifestStream);
            if (!manifestResult.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, manifestResult.Diagnostics));
            }

            return (ImmutableArray.CreateRange(manifestStream.ToArray()), netmoduleImage);
        }
    }
}

internal static class W7FullyQualifiedSyntheticTarget
{
    internal const int ConstantValue = 41;

    [ThreadStatic]
    internal static int ThreadValue = 0;

    internal static int Int32Value = 42;

    internal static string? StringValue = "synthetic";

    internal static object? ObjectValue = new();

    internal static W7FullyQualifiedSyntheticReference? ReferenceValue = new() { Value = 43 };

    internal static int? NullableValue = 44;
}

internal sealed class W7FullyQualifiedSyntheticReference
{
    internal int Value { get; init; }
}
