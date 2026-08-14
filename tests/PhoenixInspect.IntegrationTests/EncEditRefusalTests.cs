using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the E2 refusal wiring: an explicit-route owner whose module is declared with applied edit generations
/// stops at the construction stage with its typed issue, an unedited declaration changes nothing, and an absent
/// declaration vector keeps the previous behavior exactly.
/// </summary>
/// <remarks>
/// The edit states are constructed at the pinned measured shapes — the enablement bit set with the counter at its
/// floor for the unedited arm and one above it for the edited arm — so the test exercises exactly the contract the
/// E1 disposition froze rather than synthetic flag values the runtime never produces.
/// </remarks>
public sealed class EncEditRefusalTests
{
    private const string Expression = "global::Pipe.App.GenericSlot<int>.Text";

    /// <summary>An edited module refuses construction; unedited and absent declarations do not.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Edited_owner_module_refuses_construction_with_its_typed_stop()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();

        var refused = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            Expression,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, refused.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Partial, refused.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            refused.Provenance.OwnerConstruction!.ResultKind);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
            refused.Provenance.OwnerConstruction.Issue);
        Assert.Equal(DumpExpressionMemberLookupOutcome.NotReached, refused.Axes.MemberLookup);
        Assert.Equal(DumpExpressionValueOutcome.NotReached, refused.Axes.Value);

        // An unedited declaration for the same module changes nothing about the construction.
        var declaredUnedited = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            Expression,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 1)),
            ]));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, declaredUnedited.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.None,
            declaredUnedited.Provenance.OwnerConstruction!.Issue);

        // An absent vector keeps the previous behavior exactly, construction included.
        var absent = StaticFieldV2ExpressionPipeline.Evaluate(
            W8V2ExpressionPipelineTests.Request(world, Expression));
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Exact, absent.Axes.TypeConstruction);
        Assert.Equal(declaredUnedited.Axes, absent.Axes);
        Assert.NotEqual(absent.Sha256, refused.Sha256);
    }

    /// <summary>The contextual route refuses an edited owner module through the same typed stop.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Contextual_route_refuses_an_edited_owner_module()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var refused = StaticFieldV2ExpressionPipeline.Evaluate(W8V2ExpressionPipelineTests.Request(
            world,
            "GenericSlot<Holder>.Current",
            scopedContext: W8V2ExpressionPipelineTests.NamespaceImportContext(world, "Pipe.App"),
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, refused.Axes.TypeBinding);
        Assert.Equal(DumpExpressionTypeConstructionOutcome.Partial, refused.Axes.TypeConstruction);
        Assert.Equal(
            StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
            refused.Provenance.OwnerConstruction!.Issue);
        Assert.Equal(DumpExpressionMemberLookupOutcome.NotReached, refused.Axes.MemberLookup);
    }

    /// <summary>
    /// Member lookup refuses a walked level in a declared edited module through its own typed stop — the
    /// defense-in-depth guard below the construction-stage refusal, whose cross-module positive arm only an
    /// edited multi-module corpus can exercise end to end.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Member_lookup_refuses_a_walked_level_in_an_edited_module()
    {
        var world = W8V2ExpressionPipelineTests.BuildWorld();
        var lookup = StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            world.App,
            world.HostType.TypeDefinitionToken,
            DumpExpressionIdentifier.Create("Text"),
            world.Ancestry,
            world.FieldCatalogs,
            world.PropertyCatalogs,
            StaticFieldV2AccessibilityMode.QualifiedInspectionBypass,
            moduleEditDeclarations:
            [
                StaticFieldV2ModuleEditDeclaration.Create(world.App, EditState(generationCounter: 2)),
            ]));
        Assert.Equal(StaticFieldV2MemberLookupResultKind.NonExact, lookup.ResultKind);
        Assert.Equal(
            StaticFieldV2MemberLookupIssue.BaseModuleEditedGenerationsNotComposed,
            lookup.Issue);
    }

    /// <summary>
    /// Closes the silent-staleness hazard end to end over a really edited process: a fully qualified spelling
    /// whose owner lives in the edited module refuses at construction through the complete acquired chain —
    /// composition, physical edit-state acquisition, declaration, and typed stop — while the identical spelling
    /// over the edit-enabled-but-unedited comparator module proceeds past construction into member lookup.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EncFixtureV1")]
    public void Edited_process_evaluation_refuses_end_to_end_over_the_real_dump()
    {
        var payloadDirectory = Path.Combine(Path.GetTempPath(), $"enc-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(payloadDirectory);
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enc-e2e-{Guid.NewGuid():N}.dmp");
        try
        {
            EncDeltaCompiler.WriteSmokePayload(payloadDirectory);
            using (var target = TestTargetRunner.StartAndWaitReady(
                W8ShapeTargetPaths.RequireArtifact(
                    W8ShapeTargetPaths.ResolveExecutable("PhoenixInspect.EncTestTarget")),
                ["--truth-gate", "enc-smoke", "--payload", payloadDirectory],
                isolatedDirectory: null,
                additionalEnvironment: new Dictionary<string, string>
                {
                    ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "Debug",
                }))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            const string expression = "global::PhoenixInspect.EncFixtureBaseline.Probe.Sentinel";
            using (var edited = W8CorpusEvaluationWorld.Open(
                dumpPath,
                "Request",
                metadataModuleNames: ["PhoenixInspect.EncFixtureBaseline.dll"]))
            {
                var refused = edited.Evaluate(expression, sizeof(int));
                Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, refused.Result.Axes.TypeBinding);
                Assert.Equal(
                    DumpExpressionTypeConstructionOutcome.Partial,
                    refused.Result.Axes.TypeConstruction);
                Assert.Equal(
                    StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
                    refused.Result.Provenance.OwnerConstruction!.Issue);
                Assert.Equal(DumpExpressionMemberLookupOutcome.NotReached, refused.Result.Axes.MemberLookup);
            }

            // The identical spelling over the edit-enabled-but-unedited comparator does not produce the edit
            // refusal: through the same composition path it stops at the measured classification gap instead —
            // the fixture payload compiles against implementation assemblies, so its Object TypeRef targets the
            // corelib identity rather than the composed core's — which proves the declared edit states, not
            // composition or enablement, decide the refusal. The unedited proceed-through behavior itself is
            // proved by the corpus rows over their real dumps.
            using (var unedited = W8CorpusEvaluationWorld.Open(
                dumpPath,
                "Request",
                metadataModuleNames: ["PhoenixInspect.EncFixtureUnedited.dll"]))
            {
                var comparator = unedited.Evaluate(expression, sizeof(int));
                Assert.Equal(DumpExpressionTypeBindingOutcome.Exact, comparator.Result.Axes.TypeBinding);
                Assert.Equal(
                    StaticFieldV2ClosedConstructionIssue.DefinitionClassificationAbsent,
                    comparator.Result.Provenance.OwnerConstruction!.Issue);
                Assert.NotEqual(
                    StaticFieldV2ClosedConstructionIssue.OwnerModuleEditedGenerationsNotComposed,
                    comparator.Result.Provenance.OwnerConstruction.Issue);
            }

            // The released CLI and Desktop share ExpressionEvaluationService rather than the V2 corpus pipeline.
            // Prove that this real edited snapshot is refused at that public boundary too, while the closed
            // evidence-free constant subset remains usable.
            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using (var hostSession = opened.Value!)
            {
                var admission = hostSession.ReadModuleEditAdmission();
                Assert.Same(admission, hostSession.ReadModuleEditAdmission());
                Assert.Equal(
                    ClrmdModuleEditAdmissionDisposition.EditedModulesNotComposed,
                    admission.Disposition);
                Assert.Equal(hostSession.Modules.Length, admission.InspectedModuleCount);
                Assert.Equal(hostSession.Modules.Length, admission.TotalModuleCount);
                Assert.NotNull(admission.StoppedModule);
                Assert.NotEmpty(admission.Evidence);

                var pure = ExpressionEvaluationService.EvaluateStaticField(hostSession, "2 + 2", null, []);
                Assert.Equal(EvaluationSeverity.Exact, pure.Severity);
                Assert.Equal("4", pure.Value);
                Assert.Equal("Folded without dump evidence", pure.Stage);
                Assert.Empty(pure.MemoryReads);

                var pureDirect = ExpressionEvaluator.Evaluate(hostSession, "2 + 2");
                var pureWithoutSession = ExpressionEvaluator.Evaluate(session: null, "2 + 2");
                Assert.Equal(ExpressionEvaluationStatus.Exact, pureDirect.Status);
                Assert.Null(pureDirect.ModuleEditAdmission);
                Assert.Equal(pureWithoutSession.Sha256, pureDirect.Sha256);

                // A type that may be an enum declared in dump-module metadata is not a pure typed error. The
                // evidence-free probe defers that one resolution attempt, and this edited session must therefore
                // retain the same admission refusal without scanning metadata through the constant evaluator.
                var deferredType = ExpressionEvaluator.Evaluate(
                    hostSession,
                    "typeof(System.ConsoleColor).IsEnum");
                Assert.Equal(ExpressionEvaluationStatus.Unavailable, deferredType.Status);
                Assert.Same(admission, deferredType.ModuleEditAdmission);
                Assert.Equal("DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED", deferredType.DiagnosticCode);
                Assert.Equal(0, deferredType.ModulesScanned);
                Assert.Equal(0, deferredType.MetadataLiteralsConsumed);

                var directConstant = ExpressionEvaluator.Evaluate(hostSession, expression);
                Assert.Equal(ExpressionEvaluationStatus.Unavailable, directConstant.Status);
                Assert.Same(admission, directConstant.ModuleEditAdmission);
                Assert.Equal("DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED", directConstant.DiagnosticCode);

                var directStatic = StaticFieldExpressionEvaluator.Evaluate(hostSession, expression);
                Assert.Equal(StaticFieldExpressionEvaluationStage.EditStateAdmission, directStatic.Stage);
                Assert.Equal(StaticFieldExpressionEvaluationStatus.Unavailable, directStatic.Status);
                Assert.Same(admission, directStatic.ModuleEditAdmission);
                Assert.Equal("DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED", directStatic.DiagnosticCode);

                var contextualStatic = StaticFieldExpressionEvaluator.Evaluate(
                    hostSession,
                    expression,
                    DumpSelectedFrameSelector.Create(hostSession.Snapshot, threadOrdinal: 0, frameOrdinal: 0),
                    portablePdbCandidates: []);
                Assert.Equal(StaticFieldExpressionEvaluationStage.EditStateAdmission, contextualStatic.Stage);
                Assert.Same(admission, contextualStatic.ModuleEditAdmission);

                // Syntax rejection remains evidence-free and wins before edit-state admission.
                var rejectedStatic = StaticFieldExpressionEvaluator.Evaluate(hostSession, "global::Broken..Field");
                Assert.Equal(StaticFieldExpressionEvaluationStage.Syntax, rejectedStatic.Stage);
                Assert.Null(rejectedStatic.ModuleEditAdmission);

                var rootObject = new ClrmdHeapObjectInfo(
                    hostSession.Snapshot,
                    address: 0x1000,
                    typeName: "Synthetic.Root",
                    methodTable: 0x2000,
                    rootAddress: 0x3000,
                    rootKind: "AdmissionTest",
                    module: hostSession.Modules[0],
                    evidence: []);
                var rootBinding = DumpQueryRootBinding.FromExactObject("root", rootObject);
                var directQuery = DumpQueryEngine.Prepare(hostSession, "root.Marker", rootBinding);
                Assert.False(directQuery.IsSuccess);
                Assert.Contains(
                    directQuery.Failure!.Diagnostics,
                    diagnostic => diagnostic.Code == "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");

                var queryPlan = new DumpQueryPlan(
                    rootBinding,
                    new ClrmdInstanceFieldInfo(
                        hostSession.Snapshot,
                        rootObject.Address,
                        rootObject.MethodTable,
                        rootObject.TypeName,
                        name: "Marker",
                        metadataToken: 0x04000001,
                        address: 0x4000,
                        size: sizeof(int),
                        isObjectReference: false,
                        elementType: "Int32",
                        fieldTypeName: "System.Int32",
                        nullableInt32Layout: null),
                    DumpQueryPlanFieldKind.Int32,
                    coalesceLiteral: null,
                    parserBounds: DumpQueryParserBounds.None,
                    fieldSelectionBounds: []);
                var directPlanEvaluation = DumpQueryEngine.Evaluate(hostSession, queryPlan);
                Assert.Contains(
                    directPlanEvaluation.Diagnostics,
                    diagnostic => diagnostic.Code == "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");

                var rejectedQuery = DumpQueryEngine.Prepare(hostSession, "root..Marker", rootBinding);
                Assert.False(rejectedQuery.IsSuccess);
                Assert.DoesNotContain(
                    rejectedQuery.Failure!.Diagnostics,
                    diagnostic => diagnostic.Code == "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");

                var policy = DumpExpressionPolicy.Create(
                    DumpMethodEvaluationMode.Interpreted,
                    instructionLimit: 100,
                    logicalDepthLimit: 4,
                    traversalLimit: 10);
                var routed = DumpExpressionEvaluator.Evaluate(
                    hostSession,
                    "root.Marker",
                    rootBinding,
                    policy);
                Assert.Equal(DumpExpressionEvaluationOutcomeKind.AdmissionFailure, routed.Kind);
                Assert.Same(admission, routed.AdmissionFailure);

                var methodRequest = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
                    "root.GetMarkerSummary()",
                    rootBinding,
                    policy).Request);
                var method = DumpMethodAcquisitionFacade.Acquire(hostSession, methodRequest);
                Assert.False(method.IsSuccess);
                Assert.Equal(DumpMethodAcquisitionFailureKind.EditStateAdmission, method.Failure!.Kind);
                Assert.Same(admission, method.Failure.ModuleEditAdmission);

                var chainRequest = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
                    "root.Child.Marker",
                    rootBinding,
                    policy,
                    DumpExpressionLanguageProfile.FixedDepthMemberChainV1).Request);
                var chainPreparation = DumpMemberChainPreparationFacade.Prepare(hostSession, chainRequest);
                Assert.False(chainPreparation.IsSuccess);
                Assert.Same(admission, chainPreparation.Failure!.ModuleEditAdmission);

                var pathRequest = Assert.IsType<DumpExpressionRequest>(DumpExpressionClassifier.Classify(
                    "root.Child.Grandchild.Marker",
                    rootBinding,
                    policy,
                    DumpExpressionLanguageProfile.MemberChainV2).Request);
                var path = DumpMemberChainPathEvaluator.Evaluate(hostSession, pathRequest);
                Assert.Contains(
                    path.Diagnostics,
                    diagnostic => diagnostic.Code == "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");

                // An edited session cannot honestly prepare a plan. A minimal detached plan shell proves the public
                // evaluation overload checks admission before it dereferences any plan evidence or opens its source.
                var detachedPlan = CreateAdmissionSentinelPlan(rootBinding);
                var chainEvaluation = DumpMemberChainEngine.Evaluate(hostSession, detachedPlan);
                Assert.Contains(
                    chainEvaluation.Diagnostics,
                    diagnostic => diagnostic.Code == "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");

                var hostRefusal = ExpressionEvaluationService.EvaluateStaticField(
                    hostSession,
                    expression,
                    contextSelector: null,
                    portablePdbCandidates: []);
                Assert.Equal(EvaluationSeverity.Stopped, hostRefusal.Severity);
                Assert.Equal("Session edit-state admission", hostRefusal.Path);
                Assert.Equal(
                    "Evaluation refused before base-image authority was consulted",
                    hostRefusal.Stage);
                Assert.Contains(
                    hostRefusal.Diagnostics,
                    diagnostic => diagnostic.Code == "EXPLORER_MODULE_EDITED_GENERATIONS_NOT_COMPOSED");
                Assert.Contains(
                    hostRefusal.Facts,
                    fact => fact.Group == "Stopped module" &&
                        fact.Name == "Name" &&
                        fact.Value == "PhoenixInspect.EncFixtureBaseline.dll");
                Assert.NotEmpty(hostRefusal.MemoryReads);
                Assert.Null(hostRefusal.PromotableRoot);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }

            if (Directory.Exists(payloadDirectory))
            {
                Directory.Delete(payloadDirectory, recursive: true);
            }
        }
    }

    private static StaticFieldV2ModuleEditStateOutcome EditState(ulong generationCounter) =>
        StaticFieldV2ModuleEditStateOutcome.IssueExact(
            runtimeModuleAddress: 0x7000_0000,
            moduleFlags: 0x9019,
            generationCounter: generationCounter);

    private static DumpMemberChainPlan CreateAdmissionSentinelPlan(DumpQueryRootBinding rootBinding)
    {
        var plan = (DumpMemberChainPlan)RuntimeHelpers.GetUninitializedObject(typeof(DumpMemberChainPlan));
        SetField(plan, "<RootBinding>k__BackingField", rootBinding);
        SetField(plan, "<RequestSha256>k__BackingField", new string('a', 64));
        SetField(plan, "requestBounds", ImmutableArray<EvaluationDeterministicBound>.Empty);
        return plan;
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
}
