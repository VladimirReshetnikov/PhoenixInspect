using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
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
