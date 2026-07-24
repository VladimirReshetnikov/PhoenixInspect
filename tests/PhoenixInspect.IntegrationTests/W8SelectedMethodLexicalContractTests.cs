using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Exercises the selected-method lexical evidence contracts with complex synthetic physical rows.</summary>
/// <remarks>
/// These headless tests freeze invariants, canonical replay behavior, and bounded source-end semantics; they
/// do not present the W8 contracts as a final compatibility surface.
/// </remarks>
public sealed class W8SelectedMethodLexicalContractTests
{
    /// <summary>
    /// Proves complete MethodDef/Param/GenericParam/signature/PDB rows derive one exact active chain while retaining
    /// disjoint rows, slot reuse, unnamed slots, constants, and generated local-function MethodDefs.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complex_complete_catalog_derives_exact_active_and_inactive_lexical_facts()
    {
        var fixture = LexicalFixture.Create();
        var observation = fixture.Acquire();
        var facts = Assert.IsType<DumpSelectedMethodLexicalFacts>(observation.Facts);

        Assert.Equal(DumpContextEvidenceStatus.Exact, observation.Status);
        Assert.Equal(DumpContextEvidenceIssue.None, observation.Issue);
        Assert.True(observation.HasExactFacts);
        Assert.Empty(observation.ReachedBounds);
        Assert.Equal(6, facts.MethodDefinitionTableRowCount);
        Assert.Equal(6, facts.ParameterTableRowCount);
        Assert.Equal(7, facts.SourceEnds.LocalScopeRowCount);
        Assert.Equal(7, facts.SourceEnds.LocalVariableRowCount);
        Assert.Equal(5, facts.SourceEnds.LocalConstantRowCount);
        Assert.Equal(
            Enumerable.Range(1, 6).Select(static row => 0x06000000 | row),
            facts.AllMethods.Select(static method => method.MethodDefinitionToken));
        Assert.Equal(0x06000002, facts.SelectedMethod.MethodDefinitionToken);
        Assert.Equal(0x02000002, facts.SelectedMethod.DeclaringTypeDefinitionToken);
        Assert.Equal(
            new[] { "", "profile", "request", "number" },
            facts.SelectedMethod.Parameters.Select(static parameter => parameter.Name));
        Assert.Equal(new[] { "TOuter", "TType" },
            facts.DeclaringTypeGenericParameters.Select(static parameter => parameter.Name));
        Assert.Equal("TMethod", Assert.Single(facts.MethodGenericParameters).Name);

        Assert.Equal(4, Assert.IsType<DumpLexicalStandaloneSignatureFact>(facts.LocalSignature).LocalSlotCount);
        Assert.Equal(
            new[] { 0x32000001, 0x32000003, 0x32000004 },
            facts.ActiveLocalScopes.Select(static scope => scope.LocalScopeToken));
        Assert.Equal(
            new[] { 0x32000002, 0x32000005 },
            facts.InactiveLocalScopes.Select(static scope => scope.LocalScopeToken));
        Assert.All(facts.ActiveLocalScopes, scope => Assert.True(scope.Contains(fixture.PdbFacts.SelectedFrame.Instruction.IlOffset)));
        Assert.All(facts.InactiveLocalScopes, scope => Assert.False(scope.Contains(fixture.PdbFacts.SelectedFrame.Instruction.IlOffset)));
        Assert.DoesNotContain(
            typeof(DumpLexicalLocalScopeRowFact).GetProperties(),
            static property => property.Name.Contains("Parent", StringComparison.Ordinal));
        Assert.Equal(1, facts.AllLocalScopes[0].VariableListStartRowId);
        Assert.Equal(2, facts.GetLocalVariableListEndRowIdExclusive(0x32000001));
        Assert.Equal(1, facts.AllLocalScopes[0].ConstantListStartRowId);
        Assert.Equal(2, facts.GetLocalConstantListEndRowIdExclusive(0x32000001));
        Assert.Equal(7, facts.LocalScopeTable.Length);
        Assert.Equal(7, facts.LocalVariableTable.Length);
        Assert.Equal(5, facts.LocalConstantTable.Length);
        Assert.Equal(6, facts.GetLocalVariableListEndRowIdExclusive(0x32000005));
        Assert.Equal(4, facts.GetLocalConstantListEndRowIdExclusive(0x32000005));
        Assert.Equal(8, facts.GetLocalVariableListEndRowIdExclusive(0x32000007));
        Assert.Equal(6, facts.GetLocalConstantListEndRowIdExclusive(0x32000007));
        Assert.Throws<ArgumentException>(() => facts.GetLocalVariableListEndRowIdExclusive(0x32000020));

        Assert.Equal(
            new[] { "outer", "shadowed", "leaf" },
            facts.ActiveLocalVariables.Select(static variable => variable.Name));
        var inactiveReuse = facts.InactiveLocalVariables.Single(static variable => variable.Name == "inactiveReuse");
        Assert.Equal("inactiveReuse", inactiveReuse.Name);
        Assert.Equal(1, inactiveReuse.SlotIndex);
        Assert.Equal(1, facts.ActiveLocalVariables.Single(variable => variable.Name == "shadowed").SlotIndex);
        Assert.Equal([3], facts.InactiveOnlyLocalSlotIndices.ToArray());
        Assert.Empty(facts.UnaccountedLocalSlotIndices);
        Assert.Equal(
            new[] { "RootConstant", "LeafConstant" },
            facts.ActiveLocalConstants.Select(static constant => constant.Name));
        Assert.Equal("InactiveConstant", Assert.Single(facts.InactiveLocalConstants).Name);

        Assert.Equal(2, facts.GeneratedLocalFunctions.Length);
        var ordinary = facts.GeneratedLocalFunctions.Single(static generated => generated.LocalFunctionName == "LocalProbe");
        Assert.Equal("Run", ordinary.ContainingMethodName);
        Assert.Equal<int?>(0, ordinary.MethodOrdinal);
        Assert.Equal<int?>(0, ordinary.MethodGeneration);
        Assert.Equal(0, ordinary.LocalFunctionOrdinal);
        Assert.Equal(0, ordinary.LocalFunctionGeneration);
        Assert.Equal(0x06000003, ordinary.Method.MethodDefinitionToken);
        var generated = facts.GeneratedLocalFunctions.Single(static item => item.LocalFunctionName == "Second");
        Assert.Equal<int?>(12, generated.MethodOrdinal);
        Assert.Equal<int?>(3, generated.MethodGeneration);
        Assert.Equal(4, generated.LocalFunctionOrdinal);
        Assert.Equal(5, generated.LocalFunctionGeneration);
        Assert.DoesNotContain(
            typeof(DumpGeneratedLocalFunctionRowFact).GetProperties(),
            static property => property.Name.Contains("Parent", StringComparison.Ordinal) ||
                property.Name.Contains("Scope", StringComparison.Ordinal));
    }

    /// <summary>
    /// Proves a complete selected MethodDef with parameters, generic rows, and a local signature remains exact when
    /// the complete Portable-PDB scope, variable, and constant tables contain no row for that method.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Exact_selected_method_without_local_scope_rows_remains_exact()
    {
        var fixture = LexicalFixture.Create();
        var noScopePdbFacts = DumpPortablePdbContextFacts.Acquire(
            fixture.PdbFacts.SelectedFrame,
            fixture.PdbFacts.ModuleDebugIdentity,
            fixture.PdbFacts.Artifact,
            fixture.PdbFacts.MethodDebugInformationToken,
            fixture.PdbFacts.Document,
            ImmutableArray<DumpPortablePdbLocalScopeIdentity>.Empty,
            fixture.PdbFacts.ImportScopes);
        var sourceEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            fixture.AllMethods.Length,
            fixture.SourceEnds.ParameterRowCount,
            fixture.DeclaringTypeGenericParameters.Length,
            fixture.MethodGenericParameters.Length,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            fixture.SourceEnds.MaximumMethodSignatureByteCount,
            fixture.SourceEnds.AggregateMethodSignatureByteCount,
            fixture.SourceEnds.MaximumSignatureTypeDepth,
            fixture.SourceEnds.MaximumSignatureTypeNodeCount,
            fixture.LocalSignature.Signature.Length,
            fixture.LocalSignature.LocalSlotCount,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            fixture.SourceEnds.GeneratedLocalFunctionRowCount);

        var observation = DumpSelectedMethodLexicalObservation.Acquire(
            noScopePdbFacts,
            sourceEnds,
            fixture.AllMethods,
            fixture.DeclaringTypeGenericParameters,
            fixture.MethodGenericParameters,
            fixture.LocalSignature,
            ImmutableArray<DumpLexicalLocalScopeRowFact>.Empty,
            ImmutableArray<DumpLexicalLocalVariableRowFact>.Empty,
            ImmutableArray<DumpLexicalLocalConstantRowFact>.Empty);

        var facts = Assert.IsType<DumpSelectedMethodLexicalFacts>(observation.Facts);
        Assert.Equal(DumpContextEvidenceStatus.Exact, observation.Status);
        Assert.Empty(facts.LocalScopeTable);
        Assert.Empty(facts.ActiveLocalScopes);
        Assert.Empty(facts.InactiveLocalScopes);
        Assert.Empty(facts.LocalVariableTable);
        Assert.Empty(facts.LocalConstantTable);
        Assert.Equal([0, 1, 2, 3], facts.UnaccountedLocalSlotIndices.ToArray());
    }

    /// <summary>
    /// Proves physical-set caller order is normalized, declaration order remains RID-based, canonical equality is
    /// content-based, and every returned array is a defensive copy.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Canonical_equality_normalizes_sets_and_defensively_copies_every_array()
    {
        var fixture = LexicalFixture.Create();
        var first = fixture.Acquire();
        var reorderedMethods = fixture.AllMethods
            .Select(method => RecreateMethod(
                method,
                method.Name,
                method.ParameterListStartRowId,
                method.Parameters.Reverse().ToImmutableArray()))
            .Reverse()
            .ToImmutableArray();
        var replay = DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            RecreateSourceEnds(fixture.SourceEnds),
            reorderedMethods,
            fixture.DeclaringTypeGenericParameters.Reverse().ToImmutableArray(),
            fixture.MethodGenericParameters.Reverse().ToImmutableArray(),
            fixture.LocalSignature,
            fixture.AllLocalScopes.Reverse().ToImmutableArray(),
            fixture.AllLocalVariables.Reverse().ToImmutableArray(),
            fixture.AllLocalConstants.Reverse().ToImmutableArray());

        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.Equal(first.CanonicalBytes.AsSpan().ToArray(), replay.CanonicalBytes.AsSpan().ToArray());

        var facts = Assert.IsType<DumpSelectedMethodLexicalFacts>(first.Facts);
        var returnedMethods = facts.AllMethods;
        ImmutableCollectionsMarshal.AsArray(returnedMethods)![0] = returnedMethods[1];
        Assert.Equal(0x06000001, facts.AllMethods[0].MethodDefinitionToken);

        var returnedSignature = Assert.IsType<DumpLexicalStandaloneSignatureFact>(facts.LocalSignature).Signature;
        ImmutableCollectionsMarshal.AsArray(returnedSignature)![0] = 0xFF;
        Assert.Equal(0x07, facts.LocalSignature!.Signature[0]);

        var returnedMethodSignature = facts.SelectedMethod.Signature;
        ImmutableCollectionsMarshal.AsArray(returnedMethodSignature)![0] = 0xFF;
        Assert.Equal(0x10, facts.SelectedMethod.Signature[0]);

        var returnedTypeParameters = facts.DeclaringTypeGenericParameters;
        ImmutableCollectionsMarshal.AsArray(returnedTypeParameters)![0] = returnedTypeParameters[^1];
        Assert.Equal("TOuter", facts.DeclaringTypeGenericParameters[0].Name);

        var returnedScopes = facts.ActiveLocalScopes;
        ImmutableCollectionsMarshal.AsArray(returnedScopes)![0] = returnedScopes[^1];
        Assert.Equal(0x32000001, facts.ActiveLocalScopes[0].LocalScopeToken);

        var returnedVariables = facts.AllLocalVariables;
        ImmutableCollectionsMarshal.AsArray(returnedVariables)![0] = returnedVariables[^1];
        Assert.Equal(0x33000001, facts.AllLocalVariables[0].LocalVariableToken);

        var returnedConstantSignature = facts.AllLocalConstants[0].Signature;
        ImmutableCollectionsMarshal.AsArray(returnedConstantSignature)![0] = 0xFF;
        Assert.Equal(0x08, facts.AllLocalConstants[0].Signature[0]);

        var returnedInactiveSlots = facts.InactiveOnlyLocalSlotIndices;
        ImmutableCollectionsMarshal.AsArray(returnedInactiveSlots)![0] = 0;
        Assert.Equal(3, facts.InactiveOnlyLocalSlotIndices[0]);

        var returnedSourceBytes = facts.SourceEnds.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedSourceBytes)![0] ^= 0x7F;
        Assert.Equal(fixture.SourceEnds.Sha256, facts.SourceEnds.Sha256);

        var returnedBytes = first.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(returnedBytes)![0] ^= 0x7F;
        Assert.Equal(replay.CanonicalBytes.AsSpan().ToArray(), first.CanonicalBytes.AsSpan().ToArray());

        var bootstrap = fixture.AllMethods.Single(static method => method.MethodDefinitionToken == 0x06000001);
        var changedMethod = RecreateMethod(
            bootstrap,
            name: "BootstrapChanged",
            parameterListStartRowId: bootstrap.ParameterListStartRowId);
        var changedMethods = fixture.AllMethods
            .Select(method => method.MethodDefinitionToken == changedMethod.MethodDefinitionToken ? changedMethod : method)
            .ToImmutableArray();
        var changed = DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            fixture.SourceEnds,
            changedMethods,
            fixture.DeclaringTypeGenericParameters,
            fixture.MethodGenericParameters,
            fixture.LocalSignature,
            fixture.AllLocalScopes,
            fixture.AllLocalVariables,
            fixture.AllLocalConstants);
        Assert.NotEqual(first, changed);
        Assert.NotEqual(first.Sha256, changed.Sha256);
    }

    /// <summary>
    /// Proves exact source end at a cap retains the complete sequence while cap-plus-one drops the entire prefix, and
    /// over-cap MethodDef or Param table counts produce identical factless Partial/BoundReached observations.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Source_end_and_module_table_caps_never_expose_a_prefix()
    {
        Assert.True(DumpSelectedMethodLexicalEncoding.TryMaterializeComplete(
            Enumerable.Range(0, 4),
            maximumCount: 4,
            out var exactAtCap));
        Assert.Equal([0, 1, 2, 3], exactAtCap.ToArray());

        Assert.False(DumpSelectedMethodLexicalEncoding.TryMaterializeComplete(
            Enumerable.Range(0, 5),
            maximumCount: 4,
            out var overCap));
        Assert.Empty(overCap);

        var exactCountAtCap = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 3,
            aggregateMethodSignatureByteCount:
                3L * DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount,
            maximumSignatureTypeDepth: 1,
            maximumSignatureTypeNodeCount: 1,
            standaloneSignatureByteCount: 0,
            localSlotCount: 0,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0);
        var exactCountBounds = exactCountAtCap.GetReachedBounds(out var exactCountExceeded);
        Assert.False(exactCountExceeded);
        Assert.Equal(
            [DumpSelectedMethodLexicalLimits.MethodDefinitionRowTraversalBound],
            exactCountBounds.ToArray());

        var exactLocalSignature = CreateFlatLocalSignature(
            DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount,
            usePointerElement: true);
        var exactLocalFact = DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount,
            exactLocalSignature);
        Assert.Equal(DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount, exactLocalFact.LocalSlotCount);
        Assert.Equal(DumpSelectedMethodLexicalLimits.MaximumSignatureTypeNodeCount, exactLocalFact.SignatureTypeNodeCount);

        var exactLocalEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 1,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 3,
            aggregateMethodSignatureByteCount: 3,
            maximumSignatureTypeDepth: exactLocalFact.SignatureTypeDepth,
            maximumSignatureTypeNodeCount: exactLocalFact.SignatureTypeNodeCount,
            standaloneSignatureByteCount: exactLocalFact.Signature.Length,
            localSlotCount: exactLocalFact.LocalSlotCount,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0);
        var exactLocalBounds = exactLocalEnds.GetReachedBounds(out var exactLocalExceeded);
        Assert.False(exactLocalExceeded);
        Assert.Equal(
            new[]
            {
                DumpSelectedMethodLexicalLimits.LocalSlotTraversalBound,
                DumpSelectedMethodLexicalLimits.SignatureTypeNodeTraversalBound,
            }.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToArray(),
            exactLocalBounds.ToArray());

        var overLocalSignature = CreateFlatLocalSignature(
            DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount + 1,
            usePointerElement: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount + 1,
            overLocalSignature));
        var overLocalEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 1,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 3,
            aggregateMethodSignatureByteCount: 3,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: overLocalSignature.Length,
            localSlotCount: DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount + 1,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0);
        var overLocalObservation = DumpSelectedMethodLexicalObservation.Acquire(
            LexicalFixture.Create().PdbFacts,
            overLocalEnds,
            allMethods: default,
            declaringTypeGenericParameters: default,
            methodGenericParameters: default,
            localSignature: null,
            allLocalScopes: default,
            allLocalVariables: default,
            allLocalConstants: default);
        Assert.Equal(DumpContextEvidenceStatus.Partial, overLocalObservation.Status);
        Assert.Equal(
            [DumpSelectedMethodLexicalLimits.LocalSlotTraversalBound],
            overLocalObservation.ReachedBounds.ToArray());

        var fixture = LexicalFixture.Create();
        var noPrefix = DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            EmptySourceEnds(
                methodDefinitionRowCount: DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount + 1,
                parameterRowCount: 0),
            allMethods: default,
            declaringTypeGenericParameters: default,
            methodGenericParameters: default,
            localSignature: null,
            allLocalScopes: default,
            allLocalVariables: default,
            allLocalConstants: default);
        var ignoredPrefix = DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            EmptySourceEnds(
                methodDefinitionRowCount: DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount + 1,
                parameterRowCount: 0),
            allMethods: [fixture.AllMethods[0]],
            declaringTypeGenericParameters: default,
            methodGenericParameters: default,
            localSignature: null,
            allLocalScopes: default,
            allLocalVariables: default,
            allLocalConstants: default);
        Assert.Equal(noPrefix, ignoredPrefix);
        Assert.Equal(DumpContextEvidenceStatus.Partial, noPrefix.Status);
        Assert.Equal(DumpContextEvidenceIssue.BoundReached, noPrefix.Issue);
        Assert.Null(noPrefix.Facts);
        Assert.False(noPrefix.HasExactFacts);
        Assert.Equal(
            [DumpSelectedMethodLexicalLimits.MethodDefinitionRowTraversalBound],
            noPrefix.ReachedBounds.ToArray());

        var parameterOverflow = DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            EmptySourceEnds(
                methodDefinitionRowCount: 1,
                parameterRowCount: DumpSelectedMethodLexicalLimits.MaximumParameterRowCount + 1),
            allMethods: default,
            declaringTypeGenericParameters: default,
            methodGenericParameters: default,
            localSignature: null,
            allLocalScopes: default,
            allLocalVariables: default,
            allLocalConstants: default);
        Assert.Equal(DumpContextEvidenceStatus.Partial, parameterOverflow.Status);
        Assert.Null(parameterOverflow.Facts);
        Assert.Equal(
            [DumpSelectedMethodLexicalLimits.ParameterRowTraversalBound],
            parameterOverflow.ReachedBounds.ToArray());

        Assert.Same(
            ClrmdDumpSession.PortablePdbLocalScopeTraversalBound,
            DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound);
        Assert.All(
            DumpSelectedMethodLexicalLimits.AllDeclaredBounds,
            static bound => Assert.True(
                bound.Name.StartsWith("dump.context.selected-method.", StringComparison.Ordinal) ||
                bound == ClrmdDumpSession.PortablePdbLocalScopeTraversalBound));

        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            fixture.SourceEnds,
            allMethods: default,
            declaringTypeGenericParameters: default,
            methodGenericParameters: default,
            localSignature: null,
            allLocalScopes: default,
            allLocalVariables: default,
            allLocalConstants: default));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 0,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 2,
            aggregateMethodSignatureByteCount: 1,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: 0,
            localSlotCount: 1,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 1,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: checked((int)DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound.Value + 1),
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 2,
            aggregateMethodSignatureByteCount: 1,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: 0,
            localSlotCount: 0,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 1,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 3,
            aggregateMethodSignatureByteCount: 3,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: 0,
            localSlotCount: DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount + 1,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: 1,
            parameterRowCount: 0,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 3,
            aggregateMethodSignatureByteCount: 3,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: 0,
            localSlotCount: 0,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount:
                DumpSelectedMethodLexicalLimits.MaximumGeneratedLocalFunctionRowCount + 1));
    }

    /// <summary>
    /// Proves all sixteen ordered lexical measurement identities admit their exact edge and convert their saturated
    /// cap-plus-one witness into a factless first-boundary observation without reading any supplied prefix.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Every_ordered_source_measurement_has_exact_edge_and_factless_cap_plus_one_behavior()
    {
        var fixture = LexicalFixture.Create();
        (string Identity, EvaluationDeterministicBound Bound)[] measurements =
        [
            ("MethodDefinitionRowCount", DumpSelectedMethodLexicalLimits.MethodDefinitionRowTraversalBound),
            ("ParameterRowCount", DumpSelectedMethodLexicalLimits.ParameterRowTraversalBound),
            (
                "DeclaringTypeGenericParameterRowCount",
                DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound),
            ("MethodGenericParameterRowCount", DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound),
            (
                "MaximumMethodSignatureByteCount",
                DumpSelectedMethodLexicalLimits.MethodSignatureBlobByteTraversalBound),
            (
                "AggregateMethodSignatureByteCount",
                DumpSelectedMethodLexicalLimits.MethodSignatureByteTraversalBound),
            (
                "StandaloneSignatureByteCount",
                DumpSelectedMethodLexicalLimits.StandAloneSignatureByteTraversalBound),
            ("LocalSlotCount", DumpSelectedMethodLexicalLimits.LocalSlotTraversalBound),
            ("MaximumSignatureTypeDepth", DumpSelectedMethodLexicalLimits.SignatureTypeDepthTraversalBound),
            ("MaximumSignatureTypeNodeCount", DumpSelectedMethodLexicalLimits.SignatureTypeNodeTraversalBound),
            ("LocalScopeRowCount", DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound),
            ("LocalVariableRowCount", DumpSelectedMethodLexicalLimits.LocalVariableRowTraversalBound),
            ("LocalConstantRowCount", DumpSelectedMethodLexicalLimits.LocalConstantRowTraversalBound),
            (
                "MaximumLocalConstantSignatureByteCount",
                DumpSelectedMethodLexicalLimits.LocalConstantSignatureBlobByteTraversalBound),
            (
                "AggregateLocalConstantSignatureByteCount",
                DumpSelectedMethodLexicalLimits.LocalConstantSignatureByteTraversalBound),
            (
                "GeneratedLocalFunctionRowCount",
                DumpSelectedMethodLexicalLimits.GeneratedLocalFunctionRowTraversalBound),
        ];
        Assert.Equal(16, measurements.Length);
        for (var measurementPosition = 0; measurementPosition < measurements.Length; measurementPosition++)
        {
            var measurement = measurements[measurementPosition];
            var exactEnds = CreateSourceEndBoundary(measurementPosition, measurement.Bound, exceedsBound: false);
            var exactReached = exactEnds.GetReachedBounds(out var exactExceeded);
            Assert.False(exactExceeded);
            Assert.Equal(
                measurement.Bound.Value,
                GetSourceEndMeasurementValue(exactEnds, measurementPosition));
            Assert.Contains(measurement.Bound, exactReached);

            var overEnds = CreateSourceEndBoundary(measurementPosition, measurement.Bound, exceedsBound: true);
            Assert.Equal(
                measurement.Bound.Value + 1,
                GetSourceEndMeasurementValue(overEnds, measurementPosition));
            var observation = DumpSelectedMethodLexicalObservation.Acquire(
                fixture.PdbFacts,
                overEnds,
                allMethods: default,
                declaringTypeGenericParameters: default,
                methodGenericParameters: default,
                localSignature: null,
                allLocalScopes: default,
                allLocalVariables: default,
                allLocalConstants: default);
            Assert.Equal(DumpContextEvidenceStatus.Partial, observation.Status);
            Assert.Equal(DumpContextEvidenceIssue.BoundReached, observation.Issue);
            Assert.Contains(measurement.Bound, observation.ReachedBounds);
            Assert.Null(observation.Facts);
        }
    }

    /// <summary>
    /// Proves the current generated local-function grammar handles balanced containing names, canonical generations,
    /// and the method-ordinal-omitted form while rejecting noncanonical or trailing ordinal text.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Generated_local_function_name_derivation_matches_current_ordinal_grammar()
    {
        AssertGeneratedName("<Run>g__Local|0_0", "Run", "Local", 0, 0, 0, 0);
        AssertGeneratedName("<Run>g__Local|12#3_4#5", "Run", "Local", 12, 3, 4, 5);
        AssertGeneratedName("<<Main>$>g__Local|7_8", "<Main>$", "Local", 7, 0, 8, 0);
        AssertGeneratedName("<Run>g__Single|4#2", "Run", "Single", null, null, 4, 2);
        AssertGeneratedName("<Run>g__class|2_2", "Run", "class", 2, 0, 2, 0);

        foreach (var invalid in new[]
                 {
                     "Run>g__Local|0_0",
                     "<Run>g__|0_0",
                     "<Run>b__Local|0_0",
                     "<Run>g__Local|-1_0",
                     "<Run>g__Local|01_0",
                     "<Run>g__Local|0#0_0",
                     "<Run>g__Local|0_0#0",
                     "<Run>g__Local|0#2_0#1",
                     "<Run>g__not-valid|0_0",
                     "<Run>g__9invalid|0_0",
                     "<Run>g__Local|2147483648_0",
                     "<Run>g__Local|0_0_tail",
                     "<Run>g__Local|0_0x",
                 })
        {
            Assert.False(DumpSelectedMethodLexicalEncoding.TryParseGeneratedLocalFunctionName(
                invalid,
                out _, out _, out _, out _, out _, out _));
        }
    }

    /// <summary>
    /// Proves every established observation status remains available, only Exact carries facts, and status/issue
    /// mismatches or default bound sets are rejected.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Observation_status_matrix_carries_facts_only_for_exact()
    {
        var fixture = LexicalFixture.Create();
        var exact = fixture.Acquire();
        var bounds = DumpSelectedMethodLexicalLimits.AllDeclaredBounds.Reverse().ToImmutableArray();
        var nonExact = new[]
        {
            DumpSelectedMethodLexicalObservation.Partial(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.SourceIncomplete, bounds),
            DumpSelectedMethodLexicalObservation.Unavailable(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.ScopeUnavailable, bounds),
            DumpSelectedMethodLexicalObservation.Ambiguous(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.ScopeAmbiguous, bounds),
            DumpSelectedMethodLexicalObservation.Conflict(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.ModuleMismatch, bounds),
            DumpSelectedMethodLexicalObservation.Invalid(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.InvalidScope, bounds),
            DumpSelectedMethodLexicalObservation.Unsupported(
                fixture.PdbFacts.SelectedFrame, DumpContextEvidenceIssue.UnsupportedScope, bounds),
        };

        Assert.NotNull(exact.Facts);
        Assert.All(nonExact, static observation =>
        {
            Assert.Null(observation.Facts);
            Assert.False(observation.HasExactFacts);
            Assert.Equal(
                observation.ReachedBounds.OrderBy(static bound => bound.Name, StringComparer.Ordinal),
                observation.ReachedBounds);
        });
        Assert.Equal(
            new[]
            {
                DumpContextEvidenceStatus.Partial,
                DumpContextEvidenceStatus.Unavailable,
                DumpContextEvidenceStatus.Ambiguous,
                DumpContextEvidenceStatus.Conflict,
                DumpContextEvidenceStatus.Invalid,
                DumpContextEvidenceStatus.Unsupported,
            },
            nonExact.Select(static observation => observation.Status));

        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalObservation.Partial(
            fixture.PdbFacts.SelectedFrame,
            DumpContextEvidenceIssue.ScopeUnavailable,
            bounds));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalObservation.Partial(
            fixture.PdbFacts.SelectedFrame,
            DumpContextEvidenceIssue.BoundReached,
            default));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalObservation.Partial(
            fixture.PdbFacts.SelectedFrame,
            DumpContextEvidenceIssue.BoundReached,
            ImmutableArray<EvaluationDeterministicBound>.Empty));
        Assert.Throws<ArgumentException>(() => DumpSelectedMethodLexicalObservation.Partial(
            fixture.PdbFacts.SelectedFrame,
            DumpContextEvidenceIssue.SourceIncomplete,
            [new EvaluationDeterministicBound("dump.context.selected-method.unknown-rows", 1)]));
    }

    /// <summary>
    /// Proves malformed within-cap row ownership, generic owners, local signatures, active-scope correlation, scope
    /// ownership, and slot ranges are rejected instead of being normalized into different physical evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Within_cap_physical_invariant_violations_are_rejected()
    {
        var fixture = LexicalFixture.Create();
        var missingRid = fixture.AllMethods
            .Where(static method => method.MethodDefinitionToken != 0x06000006)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allMethods: missingRid));

        var selected = fixture.AllMethods.Single(static method => method.MethodDefinitionToken == 0x06000002);
        Assert.Throws<ArgumentException>(() => RecreateMethodWithSignature(
            selected,
            [0x10, 0x01, 0x03, 0x01, 0x08]));
        Assert.Throws<ArgumentException>(() => RecreateMethodWithSignature(
            selected,
            [0x10, 0x01, 0x03, 0x01, 0x08, 0x08, 0x08, 0x08]));
        Assert.Throws<ArgumentException>(() => RecreateMethodWithSignature(
            selected,
            [0x30, 0x01, 0x03, 0x01, 0x08, 0x08, 0x08]));

        var wrongGenericArity = RecreateMethodWithSignature(
            selected,
            [0x10, 0x02, 0x03, 0x01, 0x08, 0x08, 0x08]);
        var wrongGenericArityCatalog = fixture.AllMethods
            .Select(method => method.MethodDefinitionToken == selected.MethodDefinitionToken
                ? wrongGenericArity
                : method)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allMethods: wrongGenericArityCatalog));

        var wrongParamStart = RecreateMethod(selected, selected.Name, parameterListStartRowId: 2);
        var wrongParamCatalog = fixture.AllMethods
            .Select(method => method.MethodDefinitionToken == selected.MethodDefinitionToken ? wrongParamStart : method)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allMethods: wrongParamCatalog));

        var wrongOwnerGeneric = DumpLexicalGenericParameterRowFact.Create(
            0x2A000004,
            ownerToken: 0x06000003,
            number: 0,
            GenericParameterAttributes.None,
            "TWrong");
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            methodGenericParameters: [wrongOwnerGeneric]));

        var duplicateAcrossOwners = DumpLexicalGenericParameterRowFact.Create(
            fixture.DeclaringTypeGenericParameters[0].GenericParameterToken,
            fixture.PdbFacts.SelectedFrame.MethodDefinitionToken,
            number: 0,
            GenericParameterAttributes.None,
            "TMethod");
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            methodGenericParameters: [duplicateAcrossOwners]));

        Assert.Throws<ArgumentException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            localSlotCount: 4,
            [0x07, 0x80, 0x04, 0x08]));
        Assert.Throws<ArgumentException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            localSlotCount: 3,
            [0x07, 0x04, 0x08, 0x0E, 0x1C, 0x08]));
        Assert.Throws<ArgumentException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            localSlotCount: 2,
            [0x07, 0x02, 0x08]));
        Assert.Throws<ArgumentException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            localSlotCount: 1,
            [0x07, 0x01, 0xFF]));
        Assert.Throws<ArgumentException>(() => DumpLexicalStandaloneSignatureFact.Create(
            0x11000001,
            localSlotCount: 1,
            [0x07, 0x01, 0x08, 0x08]));

        var changedRoot = DumpLexicalLocalScopeRowFact.Create(
            0x32000001,
            0x06000002,
            0x35000001,
            startOffset: 0,
            length: 99,
            variableListStartRowId: 1,
            constantListStartRowId: 1);
        var mismatchedScopes = fixture.AllLocalScopes
            .Select(scope => scope.LocalScopeToken == changedRoot.LocalScopeToken ? changedRoot : scope)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allLocalScopes: mismatchedScopes));

        var fabricatedInternalEnd = fixture.AllLocalVariables
            .Select(variable => variable.LocalVariableToken == 0x33000003
                ? DumpLexicalLocalVariableRowFact.Create(
                    variable.LocalVariableToken,
                    localScopeToken: 0x32000002,
                    variable.SlotIndex,
                    variable.Attributes,
                    variable.Name)
                : variable)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allLocalVariables: fabricatedInternalEnd));

        var fabricatedSelectedMethodTailEnd = fixture.AllLocalVariables
            .Select(variable => variable.LocalVariableToken == 0x33000006
                ? DumpLexicalLocalVariableRowFact.Create(
                    variable.LocalVariableToken,
                    localScopeToken: 0x32000005,
                    variable.SlotIndex,
                    variable.Attributes,
                    variable.Name)
                : variable)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            allLocalVariables: fabricatedSelectedMethodTailEnd));

        var fabricatedConstantEnd = fixture.AllLocalConstants
            .Select(constant => constant.LocalConstantToken == 0x34000002
                ? DumpLexicalLocalConstantRowFact.Create(
                    constant.LocalConstantToken,
                    localScopeToken: 0x32000003,
                    constant.Name,
                    constant.Signature)
                : constant)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(fixture, allLocalConstants: fabricatedConstantEnd));

        var equalRangeScope = DumpLexicalLocalScopeRowFact.Create(
            0x32000004,
            0x06000002,
            0x35000001,
            startOffset: 20,
            length: 60,
            variableListStartRowId: 4,
            constantListStartRowId: 2);
        var ambiguousScopes = fixture.AllLocalScopes
            .Select(scope => scope.LocalScopeToken == equalRangeScope.LocalScopeToken ? equalRangeScope : scope)
            .ToImmutableArray();
        var ambiguity = Acquire(fixture, allLocalScopes: ambiguousScopes);
        Assert.Equal(DumpContextEvidenceStatus.Ambiguous, ambiguity.Status);
        Assert.Equal(DumpContextEvidenceIssue.ScopeAmbiguous, ambiguity.Issue);
        Assert.Null(ambiguity.Facts);

        var crossedActiveScope = DumpLexicalLocalScopeRowFact.Create(
            0x32000004,
            0x06000002,
            0x35000001,
            startOffset: 40,
            length: 50,
            variableListStartRowId: 4,
            constantListStartRowId: 2);
        var crossedActiveScopes = fixture.AllLocalScopes
            .Select(scope => scope.LocalScopeToken == crossedActiveScope.LocalScopeToken ? crossedActiveScope : scope)
            .ToImmutableArray();
        var crossedActive = Acquire(fixture, allLocalScopes: crossedActiveScopes);
        Assert.Equal(DumpContextEvidenceStatus.Ambiguous, crossedActive.Status);
        Assert.Equal(DumpContextEvidenceIssue.ScopeAmbiguous, crossedActive.Issue);
        Assert.Null(crossedActive.Facts);

        var physicallyOutOfOrderScope = DumpLexicalLocalScopeRowFact.Create(
            0x32000002,
            0x06000002,
            0x35000001,
            startOffset: 30,
            length: 10,
            variableListStartRowId: 2,
            constantListStartRowId: 2);
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            allLocalScopes: fixture.AllLocalScopes
                .Select(scope => scope.LocalScopeToken == physicallyOutOfOrderScope.LocalScopeToken
                    ? physicallyOutOfOrderScope
                    : scope)
                .ToImmutableArray()));

        var wrongDescendingLength = DumpLexicalLocalScopeRowFact.Create(
            0x32000002,
            0x06000002,
            0x35000001,
            startOffset: 0,
            length: 101,
            variableListStartRowId: 2,
            constantListStartRowId: 2);
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            allLocalScopes: fixture.AllLocalScopes
                .Select(scope => scope.LocalScopeToken == wrongDescendingLength.LocalScopeToken
                    ? wrongDescendingLength
                    : scope)
                .ToImmutableArray()));

        var foreignScopeVariable = fixture.AllLocalVariables
            .Select(variable => variable.LocalVariableToken == 0x33000006
                ? DumpLexicalLocalVariableRowFact.Create(
                    variable.LocalVariableToken,
                    0x32000020,
                    variable.SlotIndex,
                    variable.Attributes,
                    variable.Name)
                : variable)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            allLocalVariables: foreignScopeVariable));

        var outOfRangeVariable = fixture.AllLocalVariables
            .Select(variable => variable.LocalVariableToken == 0x33000001
                ? DumpLexicalLocalVariableRowFact.Create(
                    variable.LocalVariableToken,
                    variable.LocalScopeToken,
                    slotIndex: 4,
                    variable.Attributes,
                    "outOfRange")
                : variable)
            .ToImmutableArray();
        Assert.Throws<ArgumentException>(() => Acquire(
            fixture,
            allLocalVariables: outOfRangeVariable));
    }

    private static void AssertGeneratedName(
        string rawName,
        string expectedContainingMethod,
        string expectedLocalFunction,
        int? expectedMethodOrdinal,
        int? expectedMethodGeneration,
        int expectedLocalFunctionOrdinal,
        int expectedLocalFunctionGeneration)
    {
        Assert.True(DumpSelectedMethodLexicalEncoding.TryParseGeneratedLocalFunctionName(
            rawName,
            out var containingMethod,
            out var localFunction,
            out var methodOrdinal,
            out var methodGeneration,
            out var localFunctionOrdinal,
            out var localFunctionGeneration));
        Assert.Equal(expectedContainingMethod, containingMethod);
        Assert.Equal(expectedLocalFunction, localFunction);
        Assert.Equal(expectedMethodOrdinal, methodOrdinal);
        Assert.Equal(expectedMethodGeneration, methodGeneration);
        Assert.Equal(expectedLocalFunctionOrdinal, localFunctionOrdinal);
        Assert.Equal(expectedLocalFunctionGeneration, localFunctionGeneration);
    }

    private static DumpSelectedMethodLexicalObservation Acquire(
        LexicalFixture fixture,
        DumpSelectedMethodLexicalSourceEnds? sourceEnds = null,
        ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods = default,
        ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters = default,
        ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters = default,
        ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes = default,
        ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables = default,
        ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants = default) =>
        DumpSelectedMethodLexicalObservation.Acquire(
            fixture.PdbFacts,
            sourceEnds ?? fixture.SourceEnds,
            allMethods.IsDefault ? fixture.AllMethods : allMethods,
            declaringTypeGenericParameters.IsDefault
                ? fixture.DeclaringTypeGenericParameters
                : declaringTypeGenericParameters,
            methodGenericParameters.IsDefault ? fixture.MethodGenericParameters : methodGenericParameters,
            fixture.LocalSignature,
            allLocalScopes.IsDefault ? fixture.AllLocalScopes : allLocalScopes,
            allLocalVariables.IsDefault ? fixture.AllLocalVariables : allLocalVariables,
            allLocalConstants.IsDefault ? fixture.AllLocalConstants : allLocalConstants);

    private static DumpSelectedMethodLexicalSourceEnds EmptySourceEnds(
        int methodDefinitionRowCount,
        int parameterRowCount) =>
        DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount,
            parameterRowCount,
            declaringTypeGenericParameterRowCount: 0,
            methodGenericParameterRowCount: 0,
            localScopeRowCount: 0,
            localVariableRowCount: 0,
            localConstantRowCount: 0,
            maximumMethodSignatureByteCount: 0,
            aggregateMethodSignatureByteCount: 0,
            maximumSignatureTypeDepth: 0,
            maximumSignatureTypeNodeCount: 0,
            standaloneSignatureByteCount: 0,
            localSlotCount: 0,
            maximumLocalConstantSignatureByteCount: 0,
            aggregateLocalConstantSignatureByteCount: 0,
            generatedLocalFunctionRowCount: 0);

    private static ImmutableArray<byte> CreateFlatLocalSignature(int slotCount, bool usePointerElement)
    {
        var bytesPerSlot = usePointerElement ? 2 : 1;
        var builder = ImmutableArray.CreateBuilder<byte>(checked(5 + slotCount * bytesPerSlot));
        builder.Add(0x07);
        if (slotCount <= 0x7F)
        {
            builder.Add(checked((byte)slotCount));
        }
        else if (slotCount <= 0x3FFF)
        {
            builder.Add(checked((byte)(0x80 | slotCount >> 8)));
            builder.Add(checked((byte)(slotCount & 0xFF)));
        }
        else
        {
            builder.Add(checked((byte)(0xC0 | slotCount >> 24)));
            builder.Add(checked((byte)(slotCount >> 16 & 0xFF)));
            builder.Add(checked((byte)(slotCount >> 8 & 0xFF)));
            builder.Add(checked((byte)(slotCount & 0xFF)));
        }

        for (var slot = 0; slot < slotCount; slot++)
        {
            if (usePointerElement)
            {
                builder.Add(0x0F);
            }

            builder.Add(0x08);
        }

        return builder.ToImmutable();
    }

    private static DumpSelectedMethodLexicalSourceEnds CreateSourceEndBoundary(
        int targetOrder,
        EvaluationDeterministicBound target,
        bool exceedsBound)
    {
        var methodRows = 1;
        var parameterRows = 0;
        var typeGenericRows = 0;
        var methodGenericRows = 0;
        var maximumMethodSignatureBytes = 3;
        long aggregateMethodSignatureBytes = 3;
        var standaloneSignatureBytes = 2;
        var localSlots = 0;
        var signatureDepth = 1;
        var signatureNodes = 1;
        var localScopes = 0;
        var localVariables = 0;
        var localConstants = 0;
        var maximumConstantSignatureBytes = 0;
        long aggregateConstantSignatureBytes = 0;
        var generatedRows = 0;
        var edge = checked(target.Value + (exceedsBound ? 1 : 0));
        switch (targetOrder)
        {
            case 0:
                methodRows = checked((int)edge);
                aggregateMethodSignatureBytes = 3L * methodRows;
                break;
            case 1:
                parameterRows = checked((int)edge);
                if (!exceedsBound)
                {
                    methodRows = DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount;
                    maximumMethodSignatureBytes = 7;
                    aggregateMethodSignatureBytes = 7L * methodRows;
                }
                break;
            case 2:
                typeGenericRows = checked((int)edge);
                break;
            case 3:
                methodGenericRows = checked((int)edge);
                break;
            case 4:
                maximumMethodSignatureBytes = checked((int)edge);
                aggregateMethodSignatureBytes = edge;
                break;
            case 5:
                methodRows = 1_025;
                maximumMethodSignatureBytes = DumpSelectedMethodLexicalLimits.MaximumMethodSignatureByteCount;
                aggregateMethodSignatureBytes = edge;
                break;
            case 6:
                standaloneSignatureBytes = checked((int)edge);
                localSlots = 1;
                break;
            case 7:
                localSlots = checked((int)edge);
                standaloneSignatureBytes = checked(localSlots + 5);
                signatureNodes = localSlots;
                break;
            case 8:
                signatureDepth = checked((int)edge);
                signatureNodes = signatureDepth;
                standaloneSignatureBytes = checked(signatureDepth + 3);
                localSlots = 1;
                break;
            case 9:
                signatureNodes = checked((int)edge);
                signatureDepth = 2;
                localSlots = DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount;
                standaloneSignatureBytes = checked(signatureNodes + 5);
                break;
            case 10:
                localScopes = checked((int)edge);
                break;
            case 11:
                localScopes = DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound.Value > int.MaxValue
                    ? int.MaxValue
                    : checked((int)DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound.Value);
                localVariables = checked((int)edge);
                standaloneSignatureBytes = 3;
                localSlots = 1;
                break;
            case 12:
                localScopes = 1;
                localConstants = checked((int)edge);
                maximumConstantSignatureBytes = 1;
                aggregateConstantSignatureBytes = localConstants;
                break;
            case 13:
                localScopes = 1;
                localConstants = 1;
                maximumConstantSignatureBytes = checked((int)edge);
                aggregateConstantSignatureBytes = edge;
                break;
            case 14:
                localScopes = 1;
                maximumConstantSignatureBytes = DumpSelectedMethodLexicalLimits.MaximumLocalConstantSignatureByteCount;
                localConstants = checked((int)((edge + maximumConstantSignatureBytes - 1) /
                    maximumConstantSignatureBytes));
                aggregateConstantSignatureBytes = edge;
                break;
            case 15:
                generatedRows = checked((int)edge);
                methodRows = generatedRows;
                aggregateMethodSignatureBytes = 3L * methodRows;
                break;
        }

        if (exceedsBound)
        {
            if (targetOrder < 1) parameterRows = 0;
            if (targetOrder < 2) typeGenericRows = 0;
            if (targetOrder < 3) methodGenericRows = 0;
            if (targetOrder < 4) maximumMethodSignatureBytes = 0;
            if (targetOrder < 5) aggregateMethodSignatureBytes = 0;
            if (targetOrder < 6) standaloneSignatureBytes = 0;
            if (targetOrder < 7) localSlots = 0;
            if (targetOrder < 8) signatureDepth = 0;
            if (targetOrder < 9) signatureNodes = 0;
            if (targetOrder < 10) localScopes = 0;
            if (targetOrder < 11) localVariables = 0;
            if (targetOrder < 12) localConstants = 0;
            if (targetOrder < 13) maximumConstantSignatureBytes = 0;
            if (targetOrder < 14) aggregateConstantSignatureBytes = 0;
            if (targetOrder < 15) generatedRows = 0;
        }

        return DumpSelectedMethodLexicalSourceEnds.Create(
            methodRows,
            parameterRows,
            typeGenericRows,
            methodGenericRows,
            localScopes,
            localVariables,
            localConstants,
            maximumMethodSignatureBytes,
            aggregateMethodSignatureBytes,
            signatureDepth,
            signatureNodes,
            standaloneSignatureBytes,
            localSlots,
            maximumConstantSignatureBytes,
            aggregateConstantSignatureBytes,
            generatedRows);
    }

    private static long GetSourceEndMeasurementValue(
        DumpSelectedMethodLexicalSourceEnds sourceEnds,
        int measurementPosition) =>
        measurementPosition switch
        {
            0 => sourceEnds.MethodDefinitionRowCount,
            1 => sourceEnds.ParameterRowCount,
            2 => sourceEnds.DeclaringTypeGenericParameterRowCount,
            3 => sourceEnds.MethodGenericParameterRowCount,
            4 => sourceEnds.MaximumMethodSignatureByteCount,
            5 => sourceEnds.AggregateMethodSignatureByteCount,
            6 => sourceEnds.StandaloneSignatureByteCount,
            7 => sourceEnds.LocalSlotCount,
            8 => sourceEnds.MaximumSignatureTypeDepth,
            9 => sourceEnds.MaximumSignatureTypeNodeCount,
            10 => sourceEnds.LocalScopeRowCount,
            11 => sourceEnds.LocalVariableRowCount,
            12 => sourceEnds.LocalConstantRowCount,
            13 => sourceEnds.MaximumLocalConstantSignatureByteCount,
            14 => sourceEnds.AggregateLocalConstantSignatureByteCount,
            15 => sourceEnds.GeneratedLocalFunctionRowCount,
            _ => throw new ArgumentOutOfRangeException(nameof(measurementPosition)),
        };

    private static DumpSelectedMethodLexicalSourceEnds RecreateSourceEnds(
        DumpSelectedMethodLexicalSourceEnds source) =>
        DumpSelectedMethodLexicalSourceEnds.Create(
            source.MethodDefinitionRowCount,
            source.ParameterRowCount,
            source.DeclaringTypeGenericParameterRowCount,
            source.MethodGenericParameterRowCount,
            source.LocalScopeRowCount,
            source.LocalVariableRowCount,
            source.LocalConstantRowCount,
            source.MaximumMethodSignatureByteCount,
            source.AggregateMethodSignatureByteCount,
            source.MaximumSignatureTypeDepth,
            source.MaximumSignatureTypeNodeCount,
            source.StandaloneSignatureByteCount,
            source.LocalSlotCount,
            source.MaximumLocalConstantSignatureByteCount,
            source.AggregateLocalConstantSignatureByteCount,
            source.GeneratedLocalFunctionRowCount);

    private static DumpLexicalMethodDefinitionRowFact RecreateMethod(
        DumpLexicalMethodDefinitionRowFact method,
        string name,
        int parameterListStartRowId,
        ImmutableArray<DumpLexicalParameterRowFact> parameters = default) =>
        DumpLexicalMethodDefinitionRowFact.Create(
            method.MethodDefinitionToken,
            method.DeclaringTypeDefinitionToken,
            method.RelativeVirtualAddress,
            method.Attributes,
            method.ImplementationAttributes,
            name,
            method.Signature,
            method.SignatureParameterCount,
            parameterListStartRowId,
            parameters.IsDefault ? method.Parameters : parameters);

    private static DumpLexicalMethodDefinitionRowFact RecreateMethodWithSignature(
        DumpLexicalMethodDefinitionRowFact method,
        ImmutableArray<byte> signature) =>
        DumpLexicalMethodDefinitionRowFact.Create(
            method.MethodDefinitionToken,
            method.DeclaringTypeDefinitionToken,
            method.RelativeVirtualAddress,
            method.Attributes,
            method.ImplementationAttributes,
            method.Name,
            signature,
            method.SignatureParameterCount,
            method.ParameterListStartRowId,
            method.Parameters);

    private sealed class LexicalFixture
    {
        private LexicalFixture(
            DumpPortablePdbContextFacts pdbFacts,
            DumpSelectedMethodLexicalSourceEnds sourceEnds,
            ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods,
            ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters,
            ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters,
            DumpLexicalStandaloneSignatureFact localSignature,
            ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes,
            ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables,
            ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants)
        {
            PdbFacts = pdbFacts;
            SourceEnds = sourceEnds;
            AllMethods = allMethods;
            DeclaringTypeGenericParameters = declaringTypeGenericParameters;
            MethodGenericParameters = methodGenericParameters;
            LocalSignature = localSignature;
            AllLocalScopes = allLocalScopes;
            AllLocalVariables = allLocalVariables;
            AllLocalConstants = allLocalConstants;
        }

        internal DumpPortablePdbContextFacts PdbFacts { get; }

        internal DumpSelectedMethodLexicalSourceEnds SourceEnds { get; }

        internal int MethodDefinitionTableRowCount => SourceEnds.MethodDefinitionRowCount;

        internal int ParameterTableRowCount => SourceEnds.ParameterRowCount;

        internal ImmutableArray<DumpLexicalMethodDefinitionRowFact> AllMethods { get; }

        internal ImmutableArray<DumpLexicalGenericParameterRowFact> DeclaringTypeGenericParameters { get; }

        internal ImmutableArray<DumpLexicalGenericParameterRowFact> MethodGenericParameters { get; }

        internal DumpLexicalStandaloneSignatureFact LocalSignature { get; }

        internal ImmutableArray<DumpLexicalLocalScopeRowFact> AllLocalScopes { get; }

        internal ImmutableArray<DumpLexicalLocalVariableRowFact> AllLocalVariables { get; }

        internal ImmutableArray<DumpLexicalLocalConstantRowFact> AllLocalConstants { get; }

        internal DumpSelectedMethodLexicalObservation Acquire() =>
            W8SelectedMethodLexicalContractTests.Acquire(this);

        internal static LexicalFixture Create()
        {
            var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
            var runtimeModule = new ClrmdRuntimeModuleIdentity(
                snapshot,
                AppDomainAddress: 0x1000,
                ModuleAddress: 0x2000,
                ImageBase: 0x00400000,
                ImageSize: 0x00018000);
            var moduleContent = ModuleContentIdentity.FromDigest(
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
                metadataLength: 24_576,
                new string('b', 64));
            var selector = DumpSelectedFrameSelector.Create(snapshot, threadOrdinal: 1, frameOrdinal: 2);
            var frame = DumpSelectedFrameIdentity.Create(
                selector,
                managedThreadId: 37,
                runtimeThreadAddress: 0x7000,
                stackPointer: 0x7FFF0000,
                runtimeModule,
                moduleContent,
                methodDefinitionToken: 0x06000002,
                declaringTypeDefinitionToken: 0x02000002,
                declaringNamespace: "Synthetic.Lexical",
                DumpInstructionLocation.Create(0x401234, ilOffset: 50));
            var debugIdentity = DumpPortablePdbDebugIdentity.Create(
                Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
                stamp: 0x5A17C0DE);
            var moduleDebug = DumpModulePortablePdbDebugIdentity.Create(
                runtimeModule,
                moduleContent,
                debugIdentity);
            var pdbContent = DumpPortablePdbContentIdentity.Create(31_744, new string('c', 64));
            var artifact = DumpPortablePdbArtifactIdentity.Create(pdbContent, debugIdentity);
            var importScope = DumpPortablePdbImportScopeIdentity.Create(
                0x35000001,
                parentImportScopeToken: null,
                nestingDepth: 0,
                ImmutableArray<DumpPortablePdbImportFact>.Empty);
            var activePdbScopes = ImmutableArray.Create(
                DumpPortablePdbLocalScopeIdentity.Create(
                    0x32000001, 0x06000002, 0x35000001, startOffset: 0, length: 100, nestingDepth: 0),
                DumpPortablePdbLocalScopeIdentity.Create(
                    0x32000003, 0x06000002, 0x35000001, startOffset: 20, length: 60, nestingDepth: 1),
                DumpPortablePdbLocalScopeIdentity.Create(
                    0x32000004, 0x06000002, 0x35000001, startOffset: 40, length: 20, nestingDepth: 2));
            var pdbFacts = DumpPortablePdbContextFacts.Acquire(
                frame,
                moduleDebug,
                artifact,
                methodDebugInformationToken: 0x31000002,
                document: null,
                activePdbScopes,
                [importScope]);

            var parameters = ImmutableArray.Create(
                Parameter(1, sequence: 0, ""),
                Parameter(2, sequence: 1, "profile"),
                Parameter(3, sequence: 2, "request"),
                Parameter(4, sequence: 3, "number"),
                Parameter(5, sequence: 1, "left"),
                Parameter(6, sequence: 2, "element"));
            var methods = ImmutableArray.Create(
                Method(6, 2, "<Other>g__Ignored|0_0", parameterListStart: 7, signatureParameterCount: 0, []),
                Method(
                    2,
                    2,
                    "Run",
                    parameterListStart: 1,
                    signatureParameterCount: 3,
                    parameters.Take(4).ToImmutableArray()),
                Method(4, 3, "<Run>g__Second|12#3_4#5", parameterListStart: 7, signatureParameterCount: 0, []),
                Method(1, 1, "Bootstrap", parameterListStart: 1, signatureParameterCount: 0, []),
                Method(5, 3, "<Run>b__0_0", parameterListStart: 7, signatureParameterCount: 0, []),
                Method(
                    3,
                    3,
                    "<Run>g__LocalProbe|0_0",
                    parameterListStart: 5,
                    signatureParameterCount: 2,
                    parameters.Skip(4).ToImmutableArray()));
            var typeGenericParameters = ImmutableArray.Create(
                DumpLexicalGenericParameterRowFact.Create(
                    0x2A000002, 0x02000002, number: 1, GenericParameterAttributes.None, "TType"),
                DumpLexicalGenericParameterRowFact.Create(
                    0x2A000001, 0x02000002, number: 0, GenericParameterAttributes.None, "TOuter"));
            var methodGenericParameters = ImmutableArray.Create(
                DumpLexicalGenericParameterRowFact.Create(
                    0x2A000003, 0x06000002, number: 0, GenericParameterAttributes.None, "TMethod"));
            var localSignature = DumpLexicalStandaloneSignatureFact.Create(
                0x11000001,
                localSlotCount: 4,
                [
                    0x07, 0x04,
                    0x08,
                    0x45, 0x1D, 0x0E,
                    0x10, 0x08,
                    0x15, 0x12, 0x05, 0x01, 0x1C,
                ]);
            var scopes = ImmutableArray.Create(
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000005, 0x06000002, 0x35000001, startOffset: 80, length: 20,
                    variableListStartRowId: 5, constantListStartRowId: 3),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000004, 0x06000002, 0x35000001, startOffset: 40, length: 20,
                    variableListStartRowId: 4, constantListStartRowId: 2),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000001, 0x06000002, 0x35000001, startOffset: 0, length: 100,
                    variableListStartRowId: 1, constantListStartRowId: 1),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000002, 0x06000002, 0x35000001, startOffset: 0, length: 20,
                    variableListStartRowId: 2, constantListStartRowId: 2),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000003, 0x06000002, 0x35000001, startOffset: 20, length: 60,
                    variableListStartRowId: 3, constantListStartRowId: 2),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000007, 0x06000003, 0x35000001, startOffset: 20, length: 20,
                    variableListStartRowId: 7, constantListStartRowId: 5),
                DumpLexicalLocalScopeRowFact.Create(
                    0x32000006, 0x06000003, 0x35000001, startOffset: 0, length: 20,
                    variableListStartRowId: 6, constantListStartRowId: 4));
            var variables = ImmutableArray.Create(
                Variable(2, 0x32000002, slot: 1, "inactiveReuse"),
                Variable(5, 0x32000005, slot: 3, "inactiveOnly"),
                Variable(3, 0x32000003, slot: 1, "shadowed"),
                Variable(1, 0x32000001, slot: 0, "outer"),
                Variable(4, 0x32000004, slot: 2, "leaf"),
                Variable(7, 0x32000007, slot: 1, "foreignTail"),
                Variable(6, 0x32000006, slot: 0, "foreignHead"));
            var constants = ImmutableArray.Create(
                Constant(3, 0x32000005, "InactiveConstant", [0x08, 0x02]),
                Constant(1, 0x32000001, "RootConstant", [0x08, 0x01]),
                Constant(2, 0x32000004, "LeafConstant", [0x0E, 0x00]),
                Constant(5, 0x32000007, "ForeignTailConstant", [0x08, 0x04]),
                Constant(4, 0x32000006, "ForeignHeadConstant", [0x08, 0x03]));
            var sourceEnds = DumpSelectedMethodLexicalSourceEnds.Create(
                methodDefinitionRowCount: methods.Length,
                parameterRowCount: parameters.Length,
                declaringTypeGenericParameterRowCount: typeGenericParameters.Length,
                methodGenericParameterRowCount: methodGenericParameters.Length,
                localScopeRowCount: scopes.Length,
                localVariableRowCount: variables.Length,
                localConstantRowCount: constants.Length,
                maximumMethodSignatureByteCount: methods.Max(static method => method.Signature.Length),
                aggregateMethodSignatureByteCount: methods.Sum(static method => (long)method.Signature.Length),
                maximumSignatureTypeDepth: Math.Max(
                    methods.Max(static method => method.SignatureTypeDepth),
                    localSignature.SignatureTypeDepth),
                maximumSignatureTypeNodeCount: Math.Max(
                    methods.Max(static method => method.SignatureTypeNodeCount),
                    localSignature.SignatureTypeNodeCount),
                standaloneSignatureByteCount: localSignature.Signature.Length,
                localSlotCount: localSignature.LocalSlotCount,
                maximumLocalConstantSignatureByteCount: constants.Max(static constant => constant.Signature.Length),
                aggregateLocalConstantSignatureByteCount: constants.Sum(static constant => (long)constant.Signature.Length),
                generatedLocalFunctionRowCount: 2);
            return new LexicalFixture(
                pdbFacts,
                sourceEnds,
                methods,
                typeGenericParameters,
                methodGenericParameters,
                localSignature,
                scopes,
                variables,
                constants);
        }

        private static DumpLexicalParameterRowFact Parameter(int row, int sequence, string name) =>
            DumpLexicalParameterRowFact.Create(
                0x08000000 | row,
                sequence,
                ParameterAttributes.None,
                name);

        private static DumpLexicalMethodDefinitionRowFact Method(
            int row,
            int declaringTypeRow,
            string name,
            int parameterListStart,
            int signatureParameterCount,
            ImmutableArray<DumpLexicalParameterRowFact> parameters)
        {
            var signature = ImmutableArray.CreateBuilder<byte>();
            var isSelectedGenericMethod = row == 2;
            signature.Add(isSelectedGenericMethod ? (byte)0x10 : (byte)0x00);
            if (isSelectedGenericMethod)
            {
                signature.Add(0x01);
            }

            signature.Add(checked((byte)signatureParameterCount));
            signature.Add(0x01);
            signature.AddRange(Enumerable.Repeat((byte)0x08, signatureParameterCount));
            return DumpLexicalMethodDefinitionRowFact.Create(
                0x06000000 | row,
                0x02000000 | declaringTypeRow,
                relativeVirtualAddress: row * 32,
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                name,
                signature.ToImmutable(),
                signatureParameterCount,
                parameterListStart,
                parameters);
        }

        private static DumpLexicalLocalVariableRowFact Variable(
            int row,
            int localScopeToken,
            int slot,
            string name) =>
            DumpLexicalLocalVariableRowFact.Create(
                0x33000000 | row,
                localScopeToken,
                slot,
                LocalVariableAttributes.None,
                name);

        private static DumpLexicalLocalConstantRowFact Constant(
            int row,
            int localScopeToken,
            string name,
            ImmutableArray<byte> signature) =>
            DumpLexicalLocalConstantRowFact.Create(
                0x34000000 | row,
                localScopeToken,
                name,
                signature);
    }
}
