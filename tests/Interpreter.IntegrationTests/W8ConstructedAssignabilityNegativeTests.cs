using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// Proves negative direction, emitted class/interface and base edges, and array topology cases for W8
/// constructed-reference assignability from one target artifact, an isolated runtime load, and the pinned compiler.
/// </summary>
/// <remarks>
/// This draft W8.1 oracle is deliberately test-only. It freezes physical evidence for the later product contract;
/// it is not itself a public evaluator API or a substitute for dump-backed reference validation.
/// </remarks>
public sealed class W8ConstructedAssignabilityNegativeTests
{
    private const string TargetNamespace = "Interpreter.W8TestTarget";

    /// <summary>
    /// Proves invariant mismatch, carrier-to-interface and derived-to-base graph edges, reversed covariance and
    /// contravariance, array rank and SZ-topology mismatch, and value-element covariance rejection against exact
    /// runtime identities and independent runtime/compiler answers.
    /// </summary>
    /// <remarks>
    /// Positive mirrors are included so a direction reversal cannot make the negative matrix pass accidentally. The
    /// rank-one multidimensional form has no C# source spelling, so both cross-topology rows retain a typed compiler
    /// disposition. The runtime's asymmetric positive answer for a multidimensional target and SZ source is recorded
    /// separately from the stricter W8 equal-topology contract; every source-expressible row is compared with Roslyn.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_negative_matrix_records_runtime_and_compiler_oracles()
    {
        var targetPath = RequireArtifact(W8TestTargetPaths.ResolveAssembly());
        var aliasPath = RequireArtifact(W8TestTargetPaths.ResolveAliasAssembly());
        var metadata = W8AssignabilityMetadataCatalog.Read(targetPath);
        using var runtime = W8AssignabilityRuntimeUniverse.Load(targetPath);
        var compiler = W8AssignabilityCompilerUniverse.Create(targetPath, aliasPath);

        AssertArtifactIdentity(metadata, runtime, compiler);
        AssertEmittedFieldShapes(runtime);

        var exactTypes = new W8ExactRuntimeTypeCatalog();
        var oracle = new W8ExactConstructedAssignabilityOracle(exactTypes);
        var cases = CreateCases(runtime, compiler, exactTypes);
        Assert.Equal(21, cases.Length);
        Assert.Equal(13, cases.Count(static candidate => !candidate.ExpectedContractAssignable));
        Assert.Equal(8, cases.Count(static candidate => candidate.ExpectedContractAssignable));
        Assert.Equal(12, cases.Count(static candidate => !candidate.ExpectedRuntimeAssignable));
        Assert.Equal(9, cases.Count(static candidate => candidate.ExpectedRuntimeAssignable));
        Assert.Equal(20, cases.Count(static candidate =>
            candidate.ExpectedContractAssignable == candidate.ExpectedRuntimeAssignable));
        Assert.Single(cases, static candidate =>
            candidate.ExpectedContractAssignable != candidate.ExpectedRuntimeAssignable);
        Assert.Equal(8, cases.Count(static candidate => candidate.RequiresSourceEdgeTraversal));
        Assert.Equal(8, cases.Count(static candidate =>
            candidate.ExpectedCompilerDisposition == W8CompilerConversionDisposition.Implicit));
        Assert.Equal(11, cases.Count(static candidate =>
            candidate.ExpectedCompilerDisposition == W8CompilerConversionDisposition.NotImplicit));
        Assert.Equal(2, cases.Count(static candidate =>
            candidate.ExpectedCompilerDisposition == W8CompilerConversionDisposition.NotExpressible));

        var firstPass = EvaluateCases(cases, compiler.Compilation, oracle);
        var replay = EvaluateCases(
            cases,
            compiler.Compilation,
            new W8ExactConstructedAssignabilityOracle(exactTypes));
        Assert.True(firstPass.SequenceEqual(replay), "W8 assignability replay lines differ.");
    }

    private static ImmutableArray<W8AssignabilityCase> CreateCases(
        W8AssignabilityRuntimeUniverse runtime,
        W8AssignabilityCompilerUniverse compiler,
        W8ExactRuntimeTypeCatalog exactTypes)
    {
        var request = runtime.RequireType("RequestContext");
        var batch = runtime.RequireType("BatchContext");
        var value = runtime.RequireType("ValueContext");
        var invariant = runtime.RequireType("IInvariantNode`1");
        var covariant = runtime.RequireType("ICovariantNode`1");
        var contravariant = runtime.RequireType("IContravariantNode`1");
        var carrier = runtime.RequireType("AssignabilityCarrier`1");
        var baseNode = runtime.RequireType("AssignabilityBaseNode");
        var derivedNode = runtime.RequireType("AssignabilityDerivedNode");

        var invariantRequest = invariant.MakeGenericType(request);
        var invariantBatch = invariant.MakeGenericType(batch);
        var covariantRequest = covariant.MakeGenericType(request);
        var covariantObject = covariant.MakeGenericType(typeof(object));
        var covariantValue = covariant.MakeGenericType(value);
        var contravariantRequest = contravariant.MakeGenericType(request);
        var contravariantObject = contravariant.MakeGenericType(typeof(object));
        var carrierRequest = carrier.MakeGenericType(request);
        var carrierObject = carrier.MakeGenericType(typeof(object));
        var requestVector = request.MakeArrayType();
        var objectVector = typeof(object).MakeArrayType();
        var valueVector = value.MakeArrayType();
        var requestMatrix = request.MakeArrayType(2);
        var objectMatrix = typeof(object).MakeArrayType(2);
        var requestRankOneMatrix = request.MakeArrayType(1);
        var objectRankOneMatrix = typeof(object).MakeArrayType(1);

        Assert.True(requestVector.IsSZArray);
        Assert.False(requestRankOneMatrix.IsSZArray);
        Assert.Equal(1, requestVector.GetArrayRank());
        Assert.Equal(1, requestRankOneMatrix.GetArrayRank());
        Assert.NotEqual(requestVector.TypeHandle, requestRankOneMatrix.TypeHandle);
        Assert.False(objectRankOneMatrix.IsSZArray);
        Assert.Equal(1, objectRankOneMatrix.GetArrayRank());
        Assert.NotEqual(objectVector.TypeHandle, objectRankOneMatrix.TypeHandle);

        return
        [
            Case(
                "invariant-argument-mismatch",
                invariantRequest,
                invariantBatch,
                compiler.InvariantRequest,
                compiler.InvariantBatch,
                expectedAssignable: false,
                exactTypes),
            EdgeCase(
                "invariant-carrier-edge-forward",
                invariantRequest,
                carrierRequest,
                compiler.InvariantRequest,
                compiler.CarrierRequest,
                expectedAssignable: true,
                exactTypes),
            EdgeCase(
                "invariant-carrier-edge-mismatch",
                invariantBatch,
                carrierRequest,
                compiler.InvariantBatch,
                compiler.CarrierRequest,
                expectedAssignable: false,
                exactTypes),
            Case(
                "covariance-forward",
                covariantObject,
                covariantRequest,
                compiler.CovariantObject,
                compiler.CovariantRequest,
                expectedAssignable: true,
                exactTypes),
            Case(
                "covariance-reversed",
                covariantRequest,
                covariantObject,
                compiler.CovariantRequest,
                compiler.CovariantObject,
                expectedAssignable: false,
                exactTypes),
            EdgeCase(
                "covariance-carrier-edge-forward",
                covariantObject,
                carrierRequest,
                compiler.CovariantObject,
                compiler.CarrierRequest,
                expectedAssignable: true,
                exactTypes),
            EdgeCase(
                "covariance-carrier-edge-reversed",
                covariantRequest,
                carrierObject,
                compiler.CovariantRequest,
                compiler.CarrierObject,
                expectedAssignable: false,
                exactTypes),
            Case(
                "contravariance-forward",
                contravariantRequest,
                contravariantObject,
                compiler.ContravariantRequest,
                compiler.ContravariantObject,
                expectedAssignable: true,
                exactTypes),
            Case(
                "contravariance-reversed",
                contravariantObject,
                contravariantRequest,
                compiler.ContravariantObject,
                compiler.ContravariantRequest,
                expectedAssignable: false,
                exactTypes),
            EdgeCase(
                "contravariance-carrier-edge-forward",
                contravariantRequest,
                carrierObject,
                compiler.ContravariantRequest,
                compiler.CarrierObject,
                expectedAssignable: true,
                exactTypes),
            EdgeCase(
                "contravariance-carrier-edge-reversed",
                contravariantObject,
                carrierRequest,
                compiler.ContravariantObject,
                compiler.CarrierRequest,
                expectedAssignable: false,
                exactTypes),
            EdgeCase(
                "base-class-edge-forward",
                baseNode,
                derivedNode,
                compiler.BaseNode,
                compiler.DerivedNode,
                expectedAssignable: true,
                exactTypes),
            EdgeCase(
                "base-class-edge-reversed",
                derivedNode,
                baseNode,
                compiler.DerivedNode,
                compiler.BaseNode,
                expectedAssignable: false,
                exactTypes),
            Case(
                "variant-value-argument",
                covariantObject,
                covariantValue,
                compiler.CovariantObject,
                compiler.CovariantValue,
                expectedAssignable: false,
                exactTypes),
            Case(
                "array-vector-covariance",
                objectVector,
                requestVector,
                compiler.ObjectVector,
                compiler.RequestVector,
                expectedAssignable: true,
                exactTypes),
            Case(
                "array-matrix-covariance",
                objectMatrix,
                requestMatrix,
                compiler.ObjectMatrix,
                compiler.RequestMatrix,
                expectedAssignable: true,
                exactTypes),
            Case(
                "array-rank-mismatch",
                objectMatrix,
                requestVector,
                compiler.ObjectMatrix,
                compiler.RequestVector,
                expectedAssignable: false,
                exactTypes),
            Case(
                "array-rank-mismatch-reversed",
                objectVector,
                requestMatrix,
                compiler.ObjectVector,
                compiler.RequestMatrix,
                expectedAssignable: false,
                exactTypes),
            TopologyCase(
                "array-sz-topology-mismatch",
                objectVector,
                requestRankOneMatrix,
                expectedRuntimeAssignable: false,
                exactTypes),
            TopologyCase(
                "array-md1-target-runtime-divergence",
                objectRankOneMatrix,
                requestVector,
                expectedRuntimeAssignable: true,
                exactTypes),
            Case(
                "array-value-element-covariance",
                objectVector,
                valueVector,
                compiler.ObjectVector,
                compiler.ValueVector,
                expectedAssignable: false,
                exactTypes),
        ];
    }

    private static W8AssignabilityCase Case(
        string name,
        Type runtimeTarget,
        Type runtimeSource,
        ITypeSymbol? compilerTarget,
        ITypeSymbol? compilerSource,
        bool expectedAssignable,
        W8ExactRuntimeTypeCatalog exactTypes) =>
        new(
            name,
            exactTypes.Get(runtimeTarget),
            exactTypes.Get(runtimeSource),
            compilerTarget,
            compilerSource,
            expectedAssignable,
            expectedAssignable,
            compilerTarget is null
                ? W8CompilerConversionDisposition.NotExpressible
                : expectedAssignable
                    ? W8CompilerConversionDisposition.Implicit
                    : W8CompilerConversionDisposition.NotImplicit,
            RequiresSourceEdgeTraversal: false);

    private static W8AssignabilityCase EdgeCase(
        string name,
        Type runtimeTarget,
        Type runtimeSource,
        ITypeSymbol compilerTarget,
        ITypeSymbol compilerSource,
        bool expectedAssignable,
        W8ExactRuntimeTypeCatalog exactTypes)
    {
        var target = exactTypes.Get(runtimeTarget);
        var source = exactTypes.Get(runtimeSource);
        Assert.False(target.IsArray);
        Assert.False(source.IsArray);
        Assert.NotEqual(target.RuntimeHandle, source.RuntimeHandle);
        Assert.NotEqual(target.DefinitionRuntimeHandle, source.DefinitionRuntimeHandle);
        Assert.True(source.RuntimeType.BaseType is not null || source.RuntimeType.GetInterfaces().Length != 0);
        return new W8AssignabilityCase(
            name,
            target,
            source,
            compilerTarget,
            compilerSource,
            expectedAssignable,
            expectedAssignable,
            expectedAssignable
                ? W8CompilerConversionDisposition.Implicit
                : W8CompilerConversionDisposition.NotImplicit,
            RequiresSourceEdgeTraversal: true);
    }

    private static W8AssignabilityCase TopologyCase(
        string name,
        Type runtimeTarget,
        Type runtimeSource,
        bool expectedRuntimeAssignable,
        W8ExactRuntimeTypeCatalog exactTypes)
    {
        var target = exactTypes.Get(runtimeTarget);
        var source = exactTypes.Get(runtimeSource);
        Assert.True(target.IsArray);
        Assert.True(source.IsArray);
        Assert.Equal(1, target.ArrayRank);
        Assert.Equal(1, source.ArrayRank);
        Assert.NotEqual(target.IsSzArray, source.IsSzArray);
        Assert.NotEqual(target.RuntimeHandle, source.RuntimeHandle);
        Assert.NotEqual(target.CanonicalIdentity, source.CanonicalIdentity);
        return new W8AssignabilityCase(
            name,
            target,
            source,
            null,
            null,
            false,
            expectedRuntimeAssignable,
            W8CompilerConversionDisposition.NotExpressible,
            RequiresSourceEdgeTraversal: false);
    }

    private static ImmutableArray<string> EvaluateCases(
        ImmutableArray<W8AssignabilityCase> cases,
        CSharpCompilation compilation,
        W8ExactConstructedAssignabilityOracle oracle)
    {
        var lines = ImmutableArray.CreateBuilder<string>(cases.Length);
        foreach (var candidate in cases)
        {
            var runtimeResult = candidate.Target.RuntimeType.IsAssignableFrom(candidate.Source.RuntimeType);
            var exactObservation = oracle.Observe(candidate.Target, candidate.Source);
            var exactResult = exactObservation.IsAssignable;
            Assert.True(
                runtimeResult == candidate.ExpectedRuntimeAssignable,
                $"{candidate.Name}: expected runtime={candidate.ExpectedRuntimeAssignable}, observed runtime={runtimeResult}.");
            Assert.True(
                exactResult == candidate.ExpectedContractAssignable,
                $"{candidate.Name}: expected contract={candidate.ExpectedContractAssignable}, observed contract={exactResult}.");
            Assert.True(
                (runtimeResult == exactResult) ==
                (candidate.ExpectedRuntimeAssignable == candidate.ExpectedContractAssignable),
                $"{candidate.Name}: unexpected contract/runtime convergence; runtime={runtimeResult}, contract={exactResult}.");
            if (candidate.RequiresSourceEdgeTraversal)
            {
                Assert.True(
                    exactObservation.SourceEdgeEnumerationCount > 0,
                    $"{candidate.Name}: the exact oracle did not enumerate a source edge set.");
                Assert.True(
                    exactObservation.SourceEdgeCandidateCount > 0,
                    $"{candidate.Name}: the exact oracle observed no source edge candidates.");
            }

            string compilerDisposition;
            if (candidate.ExpectedCompilerDisposition == W8CompilerConversionDisposition.NotExpressible)
            {
                Assert.Null(candidate.CompilerTarget);
                Assert.Null(candidate.CompilerSource);
                compilerDisposition = "not-expressible";
            }
            else
            {
                Assert.NotNull(candidate.CompilerTarget);
                Assert.NotNull(candidate.CompilerSource);
                var conversion = compilation.ClassifyConversion(
                    candidate.CompilerSource!,
                    candidate.CompilerTarget!);
                var expectedImplicit =
                    candidate.ExpectedCompilerDisposition == W8CompilerConversionDisposition.Implicit;
                Assert.True(
                    conversion.IsImplicit == expectedImplicit,
                    $"{candidate.Name}: expected compiler={candidate.ExpectedCompilerDisposition}, " +
                    $"observed implicit={conversion.IsImplicit}.");
                compilerDisposition = conversion.IsImplicit ? "implicit" : "not-implicit";
            }

            Assert.NotEqual(candidate.Target.RuntimeHandle, candidate.Source.RuntimeHandle);
            lines.Add(
                $"{candidate.Name}|contract={ToDisposition(exactResult)}|runtime={ToDisposition(runtimeResult)}|" +
                $"compiler={compilerDisposition}|edge-enumerations={exactObservation.SourceEdgeEnumerationCount}|" +
                $"edge-candidates={exactObservation.SourceEdgeCandidateCount}|{candidate.Target.CanonicalIdentity}|" +
                candidate.Source.CanonicalIdentity);
        }

        return lines.ToImmutable();
    }

    private static string ToDisposition(bool value) => value ? "yes" : "no";

    private static void AssertArtifactIdentity(
        W8AssignabilityMetadataCatalog metadata,
        W8AssignabilityRuntimeUniverse runtime,
        W8AssignabilityCompilerUniverse compiler)
    {
        string[] names =
        [
            "AssignabilityBaseNode",
            "AssignabilityCarrier`1",
            "AssignabilityDerivedNode",
            "BatchContext",
            "IContravariantNode`1",
            "ICovariantNode`1",
            "IInvariantNode`1",
            "RequestContext",
            "ValueContext",
        ];

        foreach (var name in names)
        {
            var runtimeType = runtime.RequireType(name);
            var compilerType = compiler.RequireDefinition(name);
            Assert.Equal(metadata.ModuleVersionId, runtimeType.Module.ModuleVersionId);
            Assert.Equal(metadata.TypeTokens[name], runtimeType.MetadataToken);
            Assert.Equal(name, runtimeType.Name);
            Assert.Equal(name, compilerType.MetadataName);
            Assert.Equal(TargetNamespace, compilerType.ContainingNamespace.ToDisplayString());
            Assert.True(SymbolEqualityComparer.Default.Equals(
                compiler.TargetAssembly,
                compilerType.ContainingAssembly));
        }
    }

    private static void AssertEmittedFieldShapes(W8AssignabilityRuntimeUniverse runtime)
    {
        var storage = runtime.RequireType("ConstructedAssignabilityStorage");
        var request = runtime.RequireType("RequestContext");
        var batch = runtime.RequireType("BatchContext");
        var value = runtime.RequireType("ValueContext");
        var invariant = runtime.RequireType("IInvariantNode`1");
        var covariant = runtime.RequireType("ICovariantNode`1");
        var contravariant = runtime.RequireType("IContravariantNode`1");
        var carrier = runtime.RequireType("AssignabilityCarrier`1");
        var baseNode = runtime.RequireType("AssignabilityBaseNode");
        var derivedNode = runtime.RequireType("AssignabilityDerivedNode");

        Assert.Equal(derivedNode, RequirePublicStaticField(storage, "DirectDerived").FieldType);
        Assert.Equal(baseNode, RequirePublicStaticField(storage, "BaseView").FieldType);
        Assert.Equal(
            carrier.MakeGenericType(request),
            RequirePublicStaticField(storage, "DirectRequestCarrier").FieldType);
        Assert.Equal(
            invariant.MakeGenericType(request),
            RequirePublicStaticField(storage, "InvariantRequest").FieldType);
        Assert.Equal(
            invariant.MakeGenericType(batch),
            RequirePublicStaticField(storage, "InvariantBatch").FieldType);
        Assert.Equal(
            covariant.MakeGenericType(request),
            RequirePublicStaticField(storage, "CovariantRequest").FieldType);
        Assert.Equal(
            covariant.MakeGenericType(typeof(object)),
            RequirePublicStaticField(storage, "CovariantObject").FieldType);
        Assert.Equal(
            contravariant.MakeGenericType(typeof(object)),
            RequirePublicStaticField(storage, "ContravariantObject").FieldType);
        Assert.Equal(
            contravariant.MakeGenericType(request),
            RequirePublicStaticField(storage, "ContravariantRequest").FieldType);
        Assert.Equal(
            carrier.MakeGenericType(typeof(object)),
            RequirePublicStaticField(storage, "DirectObjectCarrier").FieldType);
        Assert.Equal(request.MakeArrayType(), RequirePublicStaticField(storage, "RequestVector").FieldType);
        Assert.Equal(typeof(object).MakeArrayType(), RequirePublicStaticField(storage, "ObjectVector").FieldType);
        Assert.Equal(request.MakeArrayType(2), RequirePublicStaticField(storage, "RequestMatrix").FieldType);
        Assert.Equal(typeof(object).MakeArrayType(2), RequirePublicStaticField(storage, "ObjectMatrix").FieldType);
        Assert.Equal(value.MakeArrayType(), RequirePublicStaticField(storage, "ValueVector").FieldType);
    }

    private static FieldInfo RequirePublicStaticField(Type owner, string fieldName)
    {
        var field = owner.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        return field!;
    }

    private static string RequireArtifact(string path)
    {
        Assert.True(File.Exists(path), $"Required W8 artifact was not found: {path}");
        return Path.GetFullPath(path);
    }

    private sealed record W8AssignabilityCase(
        string Name,
        W8ExactRuntimeType Target,
        W8ExactRuntimeType Source,
        ITypeSymbol? CompilerTarget,
        ITypeSymbol? CompilerSource,
        bool ExpectedContractAssignable,
        bool ExpectedRuntimeAssignable,
        W8CompilerConversionDisposition ExpectedCompilerDisposition,
        bool RequiresSourceEdgeTraversal);

    private enum W8CompilerConversionDisposition
    {
        Implicit,
        NotImplicit,
        NotExpressible,
    }
}

internal sealed class W8ExactConstructedAssignabilityOracle
{
    private const int MaximumTraversalDepth = 32;
    private const int MaximumTraversalPairs = 256;
    private const int MaximumRuntimeEdges = 256;
    private readonly W8ExactRuntimeTypeCatalog types;

    internal W8ExactConstructedAssignabilityOracle(W8ExactRuntimeTypeCatalog types)
    {
        this.types = types;
    }

    internal W8ExactAssignabilityObservation Observe(W8ExactRuntimeType target, W8ExactRuntimeType source)
    {
        var visited = new HashSet<(ulong Target, ulong Source)>();
        var accounting = new W8ExactAssignabilityTraversalAccounting();
        var result = IsAssignableFrom(target, source, depth: 0, visited, accounting);
        return new W8ExactAssignabilityObservation(
            result,
            accounting.SourceEdgeEnumerationCount,
            accounting.SourceEdgeCandidateCount);
    }

    private bool IsAssignableFrom(
        W8ExactRuntimeType target,
        W8ExactRuntimeType source,
        int depth,
        HashSet<(ulong Target, ulong Source)> visited,
        W8ExactAssignabilityTraversalAccounting accounting)
    {
        if (depth > MaximumTraversalDepth)
        {
            throw new InvalidOperationException($"W8 assignability depth exceeded {MaximumTraversalDepth}.");
        }

        if (SameIdentity(target, source))
        {
            return true;
        }

        if (!visited.Add((target.RuntimeHandle, source.RuntimeHandle)))
        {
            return false;
        }

        if (visited.Count > MaximumTraversalPairs)
        {
            throw new InvalidOperationException($"W8 assignability pair count exceeded {MaximumTraversalPairs}.");
        }

        if (target.IsArray)
        {
            if (!source.IsArray ||
                target.IsSzArray != source.IsSzArray ||
                target.ArrayRank != source.ArrayRank)
            {
                return false;
            }

            var targetElement = Assert.Single(target.TypeArguments);
            var sourceElement = Assert.Single(source.TypeArguments);
            if (SameIdentity(targetElement, sourceElement))
            {
                return true;
            }

            if (targetElement.IsValueType || sourceElement.IsValueType)
            {
                return false;
            }

            return IsAssignableFrom(targetElement, sourceElement, checked(depth + 1), visited, accounting);
        }

        if (SameGenericDefinition(target, source))
        {
            Assert.Equal(target.TypeArguments.Length, source.TypeArguments.Length);
            Assert.Equal(target.Variance.Length, target.TypeArguments.Length);
            var allArgumentsMatch = true;
            for (var index = 0; index < target.TypeArguments.Length; index++)
            {
                var targetArgument = target.TypeArguments[index];
                var sourceArgument = source.TypeArguments[index];
                var variance = target.Variance[index];
                bool argumentMatches;
                if (variance == GenericParameterAttributes.None)
                {
                    argumentMatches = SameIdentity(targetArgument, sourceArgument);
                }
                else if (targetArgument.IsValueType || sourceArgument.IsValueType)
                {
                    argumentMatches = SameIdentity(targetArgument, sourceArgument);
                }
                else if (variance == GenericParameterAttributes.Covariant)
                {
                    argumentMatches = IsAssignableFrom(
                        targetArgument,
                        sourceArgument,
                        checked(depth + 1),
                        visited,
                        accounting);
                }
                else if (variance == GenericParameterAttributes.Contravariant)
                {
                    argumentMatches = IsAssignableFrom(
                        sourceArgument,
                        targetArgument,
                        checked(depth + 1),
                        visited,
                        accounting);
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected variance value {variance}.");
                }

                allArgumentsMatch &= argumentMatches;
            }

            if (allArgumentsMatch)
            {
                return true;
            }
        }

        var runtimeEdges = EnumerateRuntimeEdges(source.RuntimeType);
        accounting.RecordSourceEdges(runtimeEdges.Length);
        foreach (var edge in runtimeEdges)
        {
            if (IsAssignableFrom(target, types.Get(edge), checked(depth + 1), visited, accounting))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<Type> EnumerateRuntimeEdges(Type source)
    {
        var edges = ImmutableArray.CreateBuilder<Type>();
        if (source.BaseType is not null)
        {
            edges.Add(source.BaseType);
        }

        var interfaces = source.GetInterfaces();
        if (interfaces.Length > MaximumRuntimeEdges)
        {
            throw new InvalidOperationException($"W8 runtime edge count exceeded {MaximumRuntimeEdges}.");
        }

        edges.AddRange(interfaces);
        if (edges.Count > MaximumRuntimeEdges)
        {
            throw new InvalidOperationException($"W8 runtime edge count exceeded {MaximumRuntimeEdges}.");
        }

        return edges
            .Distinct()
            .OrderBy(static type => type.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool SameIdentity(W8ExactRuntimeType left, W8ExactRuntimeType right) =>
        left.RuntimeHandle == right.RuntimeHandle &&
        ReferenceEquals(left.LoadContext, right.LoadContext) &&
        string.Equals(left.CanonicalIdentity, right.CanonicalIdentity, StringComparison.Ordinal);

    private static bool SameGenericDefinition(W8ExactRuntimeType left, W8ExactRuntimeType right) =>
        left.TypeArguments.Length != 0 &&
        right.TypeArguments.Length != 0 &&
        left.DefinitionRuntimeHandle == right.DefinitionRuntimeHandle &&
        ReferenceEquals(left.LoadContext, right.LoadContext) &&
        left.ModuleVersionId == right.ModuleVersionId &&
        left.TypeToken == right.TypeToken;
}

internal sealed record W8ExactAssignabilityObservation(
    bool IsAssignable,
    int SourceEdgeEnumerationCount,
    int SourceEdgeCandidateCount);

internal sealed class W8ExactAssignabilityTraversalAccounting
{
    internal int SourceEdgeEnumerationCount { get; private set; }

    internal int SourceEdgeCandidateCount { get; private set; }

    internal void RecordSourceEdges(int candidateCount)
    {
        SourceEdgeEnumerationCount = checked(SourceEdgeEnumerationCount + 1);
        SourceEdgeCandidateCount = checked(SourceEdgeCandidateCount + candidateCount);
    }
}

internal sealed class W8ExactRuntimeTypeCatalog
{
    private const int MaximumIdentityDepth = 32;
    private const int MaximumIdentityNodes = 256;
    private readonly Dictionary<Type, W8ExactRuntimeType> types = new();
    private int nodeCount;

    internal W8ExactRuntimeType Get(Type runtimeType) => Get(runtimeType, depth: 0);

    private W8ExactRuntimeType Get(Type runtimeType, int depth)
    {
        if (types.TryGetValue(runtimeType, out var existing))
        {
            return existing;
        }

        if (depth > MaximumIdentityDepth)
        {
            throw new InvalidOperationException($"W8 type-identity depth exceeded {MaximumIdentityDepth}.");
        }

        nodeCount = checked(nodeCount + 1);
        if (nodeCount > MaximumIdentityNodes)
        {
            throw new InvalidOperationException($"W8 type-identity count exceeded {MaximumIdentityNodes}.");
        }

        W8ExactRuntimeType result;
        if (runtimeType.IsArray)
        {
            var elementType = runtimeType.GetElementType();
            Assert.NotNull(elementType);
            var element = Get(elementType!, checked(depth + 1));
            var handle = ToUInt64(runtimeType.TypeHandle.Value);
            result = new W8ExactRuntimeType(
                runtimeType,
                handle,
                0,
                AssemblyLoadContext.GetLoadContext(runtimeType.Assembly),
                Guid.Empty,
                0,
                ImmutableArray.Create(element),
                ImmutableArray<GenericParameterAttributes>.Empty,
                true,
                runtimeType.IsSZArray,
                runtimeType.GetArrayRank(),
                false,
                $"array:{handle:x16}:{(runtimeType.IsSZArray ? "sz" : "md")}:{runtimeType.GetArrayRank()}:" +
                element.CanonicalIdentity);
        }
        else
        {
            var definition = runtimeType.IsGenericType
                ? runtimeType.GetGenericTypeDefinition()
                : runtimeType;
            var arguments = runtimeType.IsGenericType
                ? runtimeType.GetGenericArguments()
                    .Select(argument => Get(argument, checked(depth + 1)))
                    .ToImmutableArray()
                : ImmutableArray<W8ExactRuntimeType>.Empty;
            var variance = definition.IsGenericTypeDefinition
                ? definition.GetGenericArguments()
                    .Select(static parameter =>
                        parameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask)
                    .ToImmutableArray()
                : ImmutableArray<GenericParameterAttributes>.Empty;
            var handle = ToUInt64(runtimeType.TypeHandle.Value);
            var definitionHandle = ToUInt64(definition.TypeHandle.Value);
            var moduleVersionId = definition.Module.ModuleVersionId;
            var typeToken = definition.MetadataToken;
            var argumentText = arguments.IsEmpty
                ? string.Empty
                : $"<{string.Join(",", arguments.Select(static argument => argument.CanonicalIdentity))}>";
            result = new W8ExactRuntimeType(
                runtimeType,
                handle,
                definitionHandle,
                AssemblyLoadContext.GetLoadContext(definition.Assembly),
                moduleVersionId,
                typeToken,
                arguments,
                variance,
                false,
                false,
                0,
                runtimeType.IsValueType,
                $"named:{handle:x16}:{definitionHandle:x16}:{moduleVersionId:D}:{typeToken:x8}{argumentText}");
        }

        types.Add(runtimeType, result);
        return result;
    }

    private static ulong ToUInt64(nint value) => unchecked((ulong)value.ToInt64());
}

internal sealed record W8ExactRuntimeType(
    Type RuntimeType,
    ulong RuntimeHandle,
    ulong DefinitionRuntimeHandle,
    AssemblyLoadContext? LoadContext,
    Guid ModuleVersionId,
    int TypeToken,
    ImmutableArray<W8ExactRuntimeType> TypeArguments,
    ImmutableArray<GenericParameterAttributes> Variance,
    bool IsArray,
    bool IsSzArray,
    int ArrayRank,
    bool IsValueType,
    string CanonicalIdentity);

internal sealed class W8AssignabilityRuntimeUniverse : IDisposable
{
    private readonly AssemblyLoadContext loadContext;
    private readonly Assembly targetAssembly;

    private W8AssignabilityRuntimeUniverse(AssemblyLoadContext loadContext, Assembly targetAssembly)
    {
        this.loadContext = loadContext;
        this.targetAssembly = targetAssembly;
    }

    internal static W8AssignabilityRuntimeUniverse Load(string targetPath)
    {
        var loadContext = new W8AssignabilityLoadContext(targetPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(targetPath);
            return new W8AssignabilityRuntimeUniverse(loadContext, assembly);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    internal Type RequireType(string metadataName)
    {
        var type = targetAssembly.GetType($"Interpreter.W8TestTarget.{metadataName}", throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }

    public void Dispose() => loadContext.Unload();
}

internal sealed class W8AssignabilityLoadContext : AssemblyLoadContext
{
    private readonly string directory;

    internal W8AssignabilityLoadContext(string targetPath)
        : base($"W8-assignability-{Guid.NewGuid():N}", isCollectible: true)
    {
        directory = Assert.IsType<string>(Path.GetDirectoryName(targetPath));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
        {
            return null;
        }

        var candidatePath = Path.Combine(directory, $"{assemblyName.Name}.dll");
        return File.Exists(candidatePath) ? LoadFromAssemblyPath(candidatePath) : null;
    }
}

internal sealed class W8AssignabilityCompilerUniverse
{
    private readonly ImmutableDictionary<string, INamedTypeSymbol> definitions;

    private W8AssignabilityCompilerUniverse(
        CSharpCompilation compilation,
        IAssemblySymbol targetAssembly,
        ImmutableDictionary<string, INamedTypeSymbol> definitions)
    {
        Compilation = compilation;
        TargetAssembly = targetAssembly;
        this.definitions = definitions;

        var request = RequireDefinition("RequestContext");
        var batch = RequireDefinition("BatchContext");
        var value = RequireDefinition("ValueContext");
        var invariant = RequireDefinition("IInvariantNode`1");
        var covariant = RequireDefinition("ICovariantNode`1");
        var contravariant = RequireDefinition("IContravariantNode`1");
        var carrier = RequireDefinition("AssignabilityCarrier`1");
        var objectType = compilation.GetSpecialType(SpecialType.System_Object);

        InvariantRequest = invariant.Construct(request);
        InvariantBatch = invariant.Construct(batch);
        CovariantRequest = covariant.Construct(request);
        CovariantObject = covariant.Construct(objectType);
        CovariantValue = covariant.Construct(value);
        ContravariantRequest = contravariant.Construct(request);
        ContravariantObject = contravariant.Construct(objectType);
        CarrierRequest = carrier.Construct(request);
        CarrierObject = carrier.Construct(objectType);
        BaseNode = RequireDefinition("AssignabilityBaseNode");
        DerivedNode = RequireDefinition("AssignabilityDerivedNode");
        RequestVector = compilation.CreateArrayTypeSymbol(request);
        ObjectVector = compilation.CreateArrayTypeSymbol(objectType);
        ValueVector = compilation.CreateArrayTypeSymbol(value);
        RequestMatrix = compilation.CreateArrayTypeSymbol(request, rank: 2);
        ObjectMatrix = compilation.CreateArrayTypeSymbol(objectType, rank: 2);
    }

    internal CSharpCompilation Compilation { get; }

    internal IAssemblySymbol TargetAssembly { get; }

    internal INamedTypeSymbol InvariantRequest { get; }

    internal INamedTypeSymbol InvariantBatch { get; }

    internal INamedTypeSymbol CovariantRequest { get; }

    internal INamedTypeSymbol CovariantObject { get; }

    internal INamedTypeSymbol CovariantValue { get; }

    internal INamedTypeSymbol ContravariantRequest { get; }

    internal INamedTypeSymbol ContravariantObject { get; }

    internal INamedTypeSymbol CarrierRequest { get; }

    internal INamedTypeSymbol CarrierObject { get; }

    internal INamedTypeSymbol BaseNode { get; }

    internal INamedTypeSymbol DerivedNode { get; }

    internal IArrayTypeSymbol RequestVector { get; }

    internal IArrayTypeSymbol ObjectVector { get; }

    internal IArrayTypeSymbol ValueVector { get; }

    internal IArrayTypeSymbol RequestMatrix { get; }

    internal IArrayTypeSymbol ObjectMatrix { get; }

    internal static W8AssignabilityCompilerUniverse Create(string targetPath, string aliasPath)
    {
        var targetReference = MetadataReference.CreateFromFile(targetPath);
        var runtimeDirectory = Assert.IsType<string>(Path.GetDirectoryName(typeof(object).Assembly.Location));
        MetadataReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(aliasPath),
            targetReference,
        ];
        var compilation = CSharpCompilation.Create(
            "W8.Assignability.CompilerOracle",
            syntaxTrees: null,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));
        var targetAssembly = Assert.IsAssignableFrom<IAssemblySymbol>(
            compilation.GetAssemblyOrModuleSymbol(targetReference));
        var names = new[]
        {
            "AssignabilityBaseNode",
            "AssignabilityCarrier`1",
            "AssignabilityDerivedNode",
            "BatchContext",
            "IContravariantNode`1",
            "ICovariantNode`1",
            "IInvariantNode`1",
            "RequestContext",
            "ValueContext",
        };
        var definitions = names.ToImmutableDictionary(
            static name => name,
            name => Assert.IsAssignableFrom<INamedTypeSymbol>(
                targetAssembly.GetTypeByMetadataName($"Interpreter.W8TestTarget.{name}")),
            StringComparer.Ordinal);
        return new W8AssignabilityCompilerUniverse(compilation, targetAssembly, definitions);
    }

    internal INamedTypeSymbol RequireDefinition(string metadataName) => definitions[metadataName];
}

internal sealed record W8AssignabilityMetadataCatalog(
    Guid ModuleVersionId,
    ImmutableDictionary<string, int> TypeTokens)
{
    private const int MaximumTypeDefinitions = 4_096;

    internal static W8AssignabilityMetadataCatalog Read(string targetPath)
    {
        using var stream = File.OpenRead(targetPath);
        using var peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
        var reader = peReader.GetMetadataReader();
        var typeHandles = reader.TypeDefinitions.Take(MaximumTypeDefinitions + 1).ToArray();
        if (typeHandles.Length > MaximumTypeDefinitions)
        {
            throw new InvalidOperationException($"W8 TypeDef count exceeded {MaximumTypeDefinitions}.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            "AssignabilityBaseNode",
            "AssignabilityCarrier`1",
            "AssignabilityDerivedNode",
            "BatchContext",
            "IContravariantNode`1",
            "ICovariantNode`1",
            "IInvariantNode`1",
            "RequestContext",
            "ValueContext",
        };
        var tokens = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var handle in typeHandles)
        {
            var definition = reader.GetTypeDefinition(handle);
            if (!string.Equals(reader.GetString(definition.Namespace), "Interpreter.W8TestTarget", StringComparison.Ordinal))
            {
                continue;
            }

            var name = reader.GetString(definition.Name);
            if (names.Contains(name))
            {
                Assert.True(tokens.TryAdd(name, MetadataTokens.GetToken(handle)));
            }
        }

        Assert.Equal(names.Order(StringComparer.Ordinal), tokens.Keys.Order(StringComparer.Ordinal));
        var module = reader.GetModuleDefinition();
        return new W8AssignabilityMetadataCatalog(reader.GetGuid(module.Mvid), tokens.ToImmutable());
    }
}
