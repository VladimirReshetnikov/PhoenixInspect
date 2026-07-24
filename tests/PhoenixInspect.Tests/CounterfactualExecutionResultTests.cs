using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;
using PhoenixInspect.Domain.Concrete;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;
using IlBody = PhoenixInspect.Core.Abstractions.MethodBody;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>Exercises W4.8's common inspect-only result schema and unchanged W4.7 projection bridge.</summary>
public sealed class CounterfactualExecutionResultTests
{
    private const string DigestA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RootedGoldenSha256 =
        "d6928f51300a49bb3f8448644eba6361f07dd4c611fef9b0c4cd90508cfa88b0";
    private const string StandaloneGoldenSha256 =
        "e466de385d03f78acfb0b768a4aa716b92402de4fa41b31e35432f380620afbd";
    private const string RejectionGoldenSha256 =
        "00c844a43289c1b585148e05b7b24e56c9a15c7e715dd887763747d6d638912e";
    private const string ModelId = "w4.result.model";
    private static readonly PureCallModelVersion Version = new(1, 0, 0);
    private static readonly ModuleHandle Module = MethodGraphPlannerTests.Module;
    private static readonly MethodHandle Root = new(Module, 0x06000001);
    private static readonly MethodHandle Helper = new(Module, 0x06000002);
    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField Field = new(
        new FieldHandle(Module, 0x04000001), Owner, TypeSig.Int32, false, false, false);
    private static readonly ResolvedField OtherField = new(
        new FieldHandle(Module, 0x04000002), Owner, TypeSig.Int32, false, false, false);

    /// <summary>Projects a real issuer-certified W4.7 transition sequence without inventing rooted identity.</summary>
    [Fact]
    public void CertifiedStandaloneProjectionIsFragmentDerivedAndHasLiteralSchemaV1Identity()
    {
        var fragment = CreateCertifiedTargetFragment();
        var result = CounterfactualExecutionProjector.FromTargetOutcome(fragment);

        Assert.Equal(CounterfactualExecutionOriginKind.StandaloneTargetOutcome, result.Context.Origin);
        Assert.Equal((EvaluationCompletionStatus.Completed, EvaluationCompleteness.Complete,
                EvaluationEvidenceStatus.Exact, EvaluationEffectStatus.None),
            (result.Completion, result.Completeness, result.Evidence, result.Effects));
        Assert.Equal(CounterfactualExecutionValueKind.TargetException, result.Value!.Kind);
        Assert.Same(fragment, result.Value.TargetOutcome);
        Assert.Null(result.Context.Request);
        Assert.Null(result.Context.PlanSchemaVersion);
        Assert.Null(result.Context.PlanSha256);
        Assert.Null(result.Context.RootMethod);
        Assert.Same(EvaluationEvidenceContext.Neutral, result.Context.EvidenceContext);
        Assert.Empty(result.Context.InterpretedMethods);
        Assert.Empty(result.Context.ModeledMethods);
        Assert.Empty(result.Context.PlannedFields);
        Assert.Empty(result.Context.CallDispositions);
        Assert.Empty(result.Context.ReachedFieldObservations);
        Assert.Empty(result.Context.ReachedFieldLoadOrdinals);
        Assert.Empty(result.Context.ModelAttempts);
        Assert.True(result.Context.CallTrace.SequenceEqual(fragment.CallTrace));
        Assert.True(result.Context.Events.SequenceEqual(fragment.Events));
        Assert.True(result.Diagnostics.SequenceEqual(fragment.Diagnostics));
        Assert.Empty(result.Provenance);
        Assert.Equal(CounterfactualBoundStatus.Applied, result.Context.Accounting.InstructionStatus);
        Assert.Equal((fragment.InitialInstructionUnits, fragment.UsedInstructionUnits, fragment.RemainingInstructionUnits),
            (result.Context.Accounting.InstructionLimit, result.Context.Accounting.InstructionUsed,
                result.Context.Accounting.InstructionRemaining));
        AssertAllNonInstructionBoundsNotApplicable(result.Context.Accounting);
        Assert.True(Contains(result.CanonicalBytes, fragment.CanonicalBytes.AsSpan()));
        Assert.Equal(StandaloneGoldenSha256, result.Sha256);
        AssertCanonicalIntegrity(result);
    }

    /// <summary>Issues a real plan and freezes the literal rooted schema-v1 identity and every fixed projection.</summary>
    [Fact]
    public void IssuedRootedExactResultDerivesPlanFactsAndHasLiteralSchemaV1Identity()
    {
        var fixture = CreatePlan();
        Assert.True(fixture.Plan.IsIssuedBy(fixture.Issuer));

        var result = CreateCompletedExact(fixture.Plan, 0x26AF37BD);
        var context = result.Context;
        Assert.Equal(CounterfactualExecutionOriginKind.RootedFacade, context.Origin);
        Assert.Same(fixture.Plan.Request, context.Request);
        Assert.Equal(fixture.Plan.SchemaVersion, context.PlanSchemaVersion);
        Assert.Equal(fixture.Plan.Sha256, context.PlanSha256);
        Assert.Equal(fixture.Plan.RootMethod, context.RootMethod);
        Assert.True(context.InterpretedMethods.SequenceEqual(fixture.Plan.InterpretedMethods));
        Assert.True(context.ModeledMethods.SequenceEqual(fixture.Plan.ModeledMethods));
        Assert.True(context.PlannedFields.SequenceEqual(fixture.Plan.Fields));
        Assert.True(context.TraversalAndPlanCallsEqual(fixture.Plan));
        Assert.Equal((CounterfactualExecutionValueKind.ExactReturn, TypeSig.Int32, 0x26AF37BD),
            (result.Value!.Kind, result.Value.StaticType, result.Value.ExactInt32));
        Assert.Equal(RootedGoldenSha256, result.Sha256);
        AssertCanonicalIntegrity(result);

        var replay = CreateCompletedExact(CreatePlan().Plan, 0x26AF37BD);
        AssertCanonicalReplay(result, replay);
    }

    /// <summary>Constricts pre-authority facade rejection to the fixed truthful invalid row and all-NA context.</summary>
    [Fact]
    public void FacadeRejectionIsOnlyInvalidInvalidNoneAndIdentityFree()
    {
        var diagnostic = new EvaluationDiagnostic("W4.Request.Invalid", "The request was rejected before authority.");
        var result = CounterfactualExecutionResult.CreateFacadeRejection([diagnostic]);

        Assert.Equal(CounterfactualExecutionOriginKind.FacadeRejection, result.Context.Origin);
        Assert.Equal((EvaluationCompletionStatus.Invalid, EvaluationCompleteness.None,
                EvaluationEvidenceStatus.Invalid, EvaluationEffectStatus.None),
            (result.Completion, result.Completeness, result.Evidence, result.Effects));
        Assert.Null(result.Value);
        Assert.Empty(result.Provenance);
        Assert.Equal(diagnostic, Assert.Single(result.Diagnostics));
        AssertIdentityFree(result.Context);
        AssertAllBoundsNotApplicable(result.Context.Accounting);
        Assert.Equal(RejectionGoldenSha256, result.Sha256);
        AssertCanonicalIntegrity(result);
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionResult.CreateFacadeRejection([]));

        var factory = typeof(CounterfactualExecutionResult).GetMethod(
            "CreateFacadeRejection",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Single(factory.GetParameters());
    }

    /// <summary>Shows independent canonical field families reidentify the result and fresh reconstruction replays.</summary>
    [Fact]
    public void EveryCanonicalFieldFamilyReidentifiesIndependently()
    {
        var plan = CreatePlan().Plan;
        var baseline = CreateCompletedExact(plan, 42);
        var value = CreateCompletedExact(plan, 43);
        var provenance = CreateCompletedExact(
            plan,
            42,
            provenance: [new EvaluationProvenance(EvaluationProvenanceKind.Policy, "policy.result")]);
        var diagnostic = CreateCompletedExact(
            plan,
            42,
            diagnostics: [new EvaluationDiagnostic("W4.Result.Note", "Canonical note.")]);
        var changedRequestPlan = CreateCompletedExact(CreatePlan(policyId: "policy.result.changed").Plan, 42);
        var modeled = CreateCompletedExact(CreatePlan(modeled: true).Plan, 42);
        var cancelled = CreateCancellationPrefix(plan);
        var standalone = CounterfactualExecutionProjector.FromTargetOutcome(CreateCertifiedTargetFragment());
        var rejection = CounterfactualExecutionResult.CreateFacadeRejection(
            [new EvaluationDiagnostic("W4.Request.Invalid", "Rejected.")]);

        var variants = new[]
        {
            baseline, value, provenance, diagnostic, changedRequestPlan, modeled, cancelled, standalone, rejection
        };
        Assert.Equal(variants.Length, variants.Select(static item => item.Sha256).Distinct().Count());
        Assert.All(variants, AssertCanonicalIntegrity);
    }

    /// <summary>Defends traversal, call, model, transcript, result, and canonical storage across both array boundaries.</summary>
    [Fact]
    public void EveryArrayProjectionIsDefensivelyCopied()
    {
        var fixture = CreatePlan(modeled: true);
        var plan = fixture.Plan;
        var observationsSource = plan.FieldObservations.ToArray();
        var ordinalsSource = new[] { 0, 1 };
        var attemptsSource = new[] { CreateSuccessfulModelAttempt() };
        var traceSource = new[] { Root, Helper };
        var eventsSource = CreateFullEvents(plan).ToArray();
        var provenanceSource = new[] { new EvaluationProvenance(EvaluationProvenanceKind.Policy, "policy.result") };
        var diagnosticSource = new[] { new EvaluationDiagnostic("W4.Result.Exact", "Exact result.") };
        var result = CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExactInt32(42),
            CounterfactualBoundStatus.Applied,
            6,
            plan.Request.InstructionLimit - 6,
            2,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            ImmutableCollectionsMarshal.AsImmutableArray(observationsSource),
            ImmutableCollectionsMarshal.AsImmutableArray(ordinalsSource),
            ImmutableCollectionsMarshal.AsImmutableArray(attemptsSource),
            1,
            1,
            ImmutableCollectionsMarshal.AsImmutableArray(traceSource),
            ImmutableCollectionsMarshal.AsImmutableArray(eventsSource),
            ImmutableCollectionsMarshal.AsImmutableArray(provenanceSource),
            ImmutableCollectionsMarshal.AsImmutableArray(diagnosticSource));
        var expectedSha = result.Sha256;
        var expectedBytes = result.CanonicalBytes.ToArray();

        observationsSource[0] = null!;
        ordinalsSource[0] = 9;
        attemptsSource[0] = null!;
        traceSource[0] = Helper;
        eventsSource[0] = new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 9, "LoadInt32");
        provenanceSource[0] = new EvaluationProvenance(EvaluationProvenanceKind.Policy, "policy.mutated");
        diagnosticSource[0] = new EvaluationDiagnostic("W4.Result.Mutated", "Mutated.");

        Mutate(result.Context.InterpretedMethods, Helper);
        Mutate(result.Context.ModeledMethods, Root);
        Mutate(result.Context.PlannedFields, OtherField);
        Mutate(result.Context.CallDispositions, null!);
        Mutate(result.Context.ReachedFieldObservations, null!);
        Mutate(result.Context.ReachedFieldLoadOrdinals, 9);
        Mutate(result.Context.ModelAttempts, null!);
        Mutate(result.Context.CallTrace, Helper);
        Mutate(result.Context.Events, new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 9, "LoadInt32"));
        Mutate(result.Context.Accounting.TraversalCharges, null!);
        Mutate(result.Provenance, new EvaluationProvenance(EvaluationProvenanceKind.Policy, "policy.consumer"));
        Mutate(result.Diagnostics, new EvaluationDiagnostic("W4.Result.Consumer", "Consumer mutation."));
        Mutate(result.CanonicalBytes, (byte)0xff);

        Assert.Equal(Root, Assert.Single(result.Context.InterpretedMethods));
        Assert.Equal(Helper, Assert.Single(result.Context.ModeledMethods));
        Assert.Equal(2, result.Context.PlannedFields.Length);
        Assert.Single(result.Context.CallDispositions);
        Assert.Equal(2, result.Context.ReachedFieldObservations.Length);
        Assert.True(result.Context.ReachedFieldLoadOrdinals.SequenceEqual([0, 1]));
        Assert.Single(result.Context.ModelAttempts);
        Assert.True(result.Context.CallTrace.SequenceEqual([Root, Helper]));
        Assert.Equal(7, result.Context.Events.Length);
        Assert.Equal(plan.TraversalUsed, result.Context.Accounting.TraversalCharges.Length);
        Assert.Equal("policy.result", Assert.Single(result.Provenance).SourceId);
        Assert.Equal("W4.Result.Exact", Assert.Single(result.Diagnostics).Code);
        Assert.True(expectedBytes.AsSpan().SequenceEqual(result.CanonicalBytes.AsSpan()));
        Assert.Equal(expectedSha, result.Sha256);
    }

    /// <summary>Derives every configured/prepared accounting fact and exactly the bounds applied on the path.</summary>
    [Fact]
    public void RootedFactoriesCorrelateRequestPlanAccountingAndEvidenceBounds()
    {
        var plan = CreatePlan().Plan;
        var cancelledBeforeFirst = CreateCancelledBeforeFirstInstruction(plan);
        Assert.Equal(CounterfactualBoundStatus.NotReached, cancelledBeforeFirst.Context.Accounting.InstructionStatus);
        Assert.Equal(CounterfactualBoundStatus.Applied, cancelledBeforeFirst.Context.Accounting.TraversalStatus);
        Assert.Equal(CounterfactualBoundStatus.Applied, cancelledBeforeFirst.Context.Accounting.DepthStatus);
        Assert.Equal(CounterfactualBoundStatus.NotReached, cancelledBeforeFirst.Context.Accounting.LineageStatus);
        Assert.Equal(
            [CounterfactualExecutionContext.LogicalDepthBoundName, CounterfactualExecutionContext.TraversalBoundName],
            cancelledBeforeFirst.Context.EvidenceContext.Bounds.Select(static item => item.Name));

        var exact = CreateCompletedExact(plan, 42);
        var accounting = exact.Context.Accounting;
        Assert.Equal(plan.Request.InstructionLimit, accounting.InstructionLimit);
        Assert.Equal((plan.TraversalLimit, plan.TraversalUsed, plan.TraversalRemaining),
            (accounting.TraversalLimit, accounting.TraversalUsed, accounting.TraversalRemaining));
        Assert.True(accounting.TraversalCharges.SequenceEqual(plan.TraversalCharges));
        Assert.Equal((plan.Request.LogicalDepthLimit, plan.RequiredLogicalDepth),
            (accounting.LogicalDepthLimit, accounting.RequiredLogicalDepth));
        Assert.Equal(plan.Request.LineageNodeCeiling, accounting.LineageNodeCeiling);
        Assert.Equal(
            [CounterfactualExecutionContext.InstructionBoundName,
                CounterfactualExecutionContext.LogicalDepthBoundName,
                CounterfactualExecutionContext.TraversalBoundName],
            exact.Context.EvidenceContext.Bounds.Select(static item => item.Name));

        Assert.Throws<ArgumentException>(() => CounterfactualExecutionAccounting.CreateRooted(
            plan, CounterfactualBoundStatus.Applied, 1, plan.Request.InstructionLimit - 1,
            plan.RequiredLogicalDepth + 1, 1, CounterfactualBoundStatus.NotReached, null));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionAccounting.CreateRooted(
            plan, CounterfactualBoundStatus.Applied, 1, plan.Request.InstructionLimit - 1,
            1, plan.RequiredLogicalDepth + 1, CounterfactualBoundStatus.NotReached, null));
    }

    /// <summary>Admits exact and unknown completed values only with their normative axes and lineage accounting.</summary>
    [Fact]
    public void CompletedRowsRequireAppliedExecutionAndExactOrCanonicalUnknownValue()
    {
        var exactPlan = CreatePlan().Plan;
        var exact = CreateCompletedExact(exactPlan, 42);
        Assert.Equal(EvaluationCompleteness.Complete, exact.Completeness);
        Assert.Equal(CounterfactualBoundStatus.Applied, exact.Context.Accounting.InstructionStatus);
        Assert.Equal(CounterfactualBoundStatus.NotReached, exact.Context.Accounting.LineageStatus);

        foreach (var evidence in new[] { EvaluationEvidenceStatus.Partial, EvaluationEvidenceStatus.Unavailable })
        {
            var plan = CreatePlan(firstFieldEvidence: evidence).Plan;
            var lineage = CreateLineage(evidence);
            var unknown = CreateCompletedUnknown(plan, lineage, evidence);
            Assert.Equal(EvaluationCompleteness.Partial, unknown.Completeness);
            Assert.Equal(evidence, unknown.Evidence);
            Assert.Equal(CounterfactualBoundStatus.Applied, unknown.Context.Accounting.LineageStatus);
            Assert.Equal(lineage.Nodes.Length, unknown.Context.Accounting.LineageNodeCount);
        }

        Assert.Throws<ArgumentException>(() => CounterfactualExecutionResult.CreateRooted(
            exactPlan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExactInt32(42),
            CounterfactualBoundStatus.Applied,
            10,
            exactPlan.Request.InstructionLimit - 10,
            2,
            2,
            CounterfactualBoundStatus.NotReached,
            null,
            exactPlan.FieldObservations,
            [0, 1],
            [],
            0,
            0,
            [Root, Helper],
            CreateFullEvents(exactPlan)));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionResult.CreateRooted(
            exactPlan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.Unsupported,
            CounterfactualExecutionValue.CreateExactInt32(42),
            CounterfactualBoundStatus.Applied,
            10,
            exactPlan.Request.InstructionLimit - 10,
            2,
            2,
            CounterfactualBoundStatus.NotReached,
            null,
            exactPlan.FieldObservations,
            [0, 1],
            [],
            0,
            0,
            [Root, Helper],
            CreateFullEvents(exactPlan)));
    }

    /// <summary>Distinguishes zero-unit budget/cancellation stops from truthful positive execution prefixes.</summary>
    [Fact]
    public void BudgetAndCancellationRowsUseExhaustedVersusNotReachedAppliedAccounting()
    {
        var zeroPlan = CreatePlan(instructionLimit: 0).Plan;
        var zeroBudget = CounterfactualExecutionResult.CreateRooted(
            zeroPlan,
            EvaluationCompletionStatus.BudgetExhausted,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Exhausted,
            0,
            0,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.BudgetExhausted));
        Assert.Null(zeroBudget.Value);
        Assert.Equal(CounterfactualBoundStatus.Exhausted, zeroBudget.Context.Accounting.InstructionStatus);

        var prefix = CreateBudgetPrefix(CreatePlan(instructionLimit: 1).Plan);
        Assert.Equal(CounterfactualExecutionValueKind.ExecutionPrefix, prefix.Value!.Kind);
        Assert.Equal(EvaluationCompleteness.Partial, prefix.Completeness);
        Assert.Equal(CounterfactualBoundStatus.Exhausted, prefix.Context.Accounting.InstructionStatus);

        var plan = CreatePlan().Plan;
        var before = CreateCancelledBeforeFirstInstruction(plan);
        var after = CreateCancellationPrefix(plan);
        Assert.Equal(CounterfactualBoundStatus.NotReached, before.Context.Accounting.InstructionStatus);
        Assert.Null(before.Value);
        Assert.False(before.IsDeterministicReplay);
        Assert.Equal(CounterfactualBoundStatus.Applied, after.Context.Accounting.InstructionStatus);
        Assert.Equal(CounterfactualExecutionValueKind.ExecutionPrefix, after.Value!.Kind);
        Assert.False(after.IsDeterministicReplay);

        Assert.Throws<ArgumentException>(() => CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Cancelled,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExecutionPrefix(),
            CounterfactualBoundStatus.NotReached,
            null,
            null,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Cancelled)));
    }

    /// <summary>Exercises every allowed blocked/invalid evidence/effect row, including atomic zero-transfer failures.</summary>
    [Fact]
    public void BlockedAndInvalidRowsPreserveReachedEvidenceAndPermitAtomicAppliedZero()
    {
        var exactPlan = CreatePlan().Plan;
        var blockedExact = CreateStoppedBeforeFirst(
            exactPlan, EvaluationCompletionStatus.Blocked, EvaluationEvidenceStatus.Exact, EvaluationEffectStatus.None);
        var blockedUnsupported = CreateStoppedBeforeFirst(
            exactPlan, EvaluationCompletionStatus.Blocked, EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.Unsupported);
        Assert.Null(blockedExact.Value);
        Assert.Equal(EvaluationEffectStatus.Unsupported, blockedUnsupported.Effects);

        foreach (var evidence in new[] { EvaluationEvidenceStatus.Partial, EvaluationEvidenceStatus.Unavailable })
        {
            var plan = CreatePlan(firstFieldEvidence: evidence).Plan;
            var stopped = CreateStoppedAfterFirstField(plan, EvaluationCompletionStatus.Blocked, evidence);
            Assert.Equal(evidence, stopped.Evidence);
            Assert.Equal(2, stopped.Context.Accounting.InstructionUsed);
        }

        var conflictPlan = CreatePlan(firstFieldEvidence: EvaluationEvidenceStatus.Conflict).Plan;
        var conflict = CreateStoppedAtTerminalField(
            conflictPlan, EvaluationCompletionStatus.Blocked, EvaluationEvidenceStatus.Conflict);
        Assert.Equal(EvaluationEvidenceStatus.Conflict, conflict.Evidence);

        var atomicBlocked = CounterfactualExecutionResult.CreateRooted(
            exactPlan,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Applied,
            0,
            exactPlan.Request.InstructionLimit,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Blocked));
        Assert.Equal(0, atomicBlocked.Context.Accounting.InstructionUsed);
        Assert.Throws<ArgumentException>(() => CreateStoppedBeforeFirst(
            exactPlan,
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            includeDiagnostic: false));

        var invalidExact = CreateStoppedBeforeFirst(
            exactPlan, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Exact, EvaluationEffectStatus.None);
        var invalidOverride = CreateStoppedBeforeFirst(
            exactPlan, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid, EvaluationEffectStatus.None);
        Assert.Equal(EvaluationEvidenceStatus.Exact, invalidExact.Evidence);
        Assert.Equal(EvaluationEvidenceStatus.Invalid, invalidOverride.Evidence);
        foreach (var evidence in new[] { EvaluationEvidenceStatus.Partial, EvaluationEvidenceStatus.Unavailable })
        {
            var plan = CreatePlan(firstFieldEvidence: evidence).Plan;
            Assert.Equal(evidence,
                CreateStoppedAfterFirstField(plan, EvaluationCompletionStatus.Invalid, evidence).Evidence);
            Assert.Equal(EvaluationEvidenceStatus.Invalid,
                CreateStoppedAfterFirstField(plan, EvaluationCompletionStatus.Invalid,
                    EvaluationEvidenceStatus.Invalid).Evidence);
        }

        var invalidPlan = CreatePlan(firstFieldEvidence: EvaluationEvidenceStatus.Invalid).Plan;
        Assert.Equal(EvaluationEvidenceStatus.Invalid,
            CreateStoppedAtTerminalField(invalidPlan, EvaluationCompletionStatus.Invalid,
                EvaluationEvidenceStatus.Invalid).Evidence);
        Assert.Throws<ArgumentException>(() => CreateStoppedAtTerminalField(
            conflictPlan, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid));
        Assert.Throws<ArgumentException>(() => CreateStoppedBeforeFirst(
            exactPlan, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.Unsupported));
    }

    /// <summary>Rejects observation/load transcript substitutions, temporal gaps, and evidence/event disagreement.</summary>
    [Fact]
    public void ReachedObservationAndLoadTranscriptMustBePlanExactAndTemporal()
    {
        var plan = CreatePlan().Plan;
        var accounting = CreateAccounting(plan, 10, 2, 2);
        var events = CreateFullEvents(plan);

        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, [plan.FieldObservations[1], plan.FieldObservations[0]], [0, 1], [], 0, 0,
            [Root, Helper], events));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, plan.FieldObservations, [0], [], 0, 0, [Root, Helper], events));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, plan.FieldObservations, [0, 1, 9], [], 0, 0, [Root, Helper], events));

        var other = CreatePlan(firstFieldEvidence: EvaluationEvidenceStatus.Partial).Plan.FieldObservations[0];
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, [other, plan.FieldObservations[1]], [0, 1], [], 0, 0,
            [Root, Helper], events));

        var atomicAccounting = CreateAccounting(plan, used: 0, logicalHighWater: 1, frameHighWater: 1);
        var blockedAtomic = CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Applied,
            0,
            plan.Request.InstructionLimit,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [plan.FieldObservations[0]],
            [0],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Blocked));
        Assert.Equal(0, blockedAtomic.Context.Accounting.InstructionUsed);
        Assert.Single(blockedAtomic.Context.ReachedFieldLoadOrdinals);
        Assert.Empty(blockedAtomic.Context.Events);
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan,
            atomicAccounting,
            [plan.FieldObservations[0]],
            [0],
            [],
            0,
            0,
            [Root],
            [],
            EvaluationCompletionStatus.Completed));

        var partialPlan = CreatePlan(firstFieldEvidence: EvaluationEvidenceStatus.Partial).Plan;
        var invalidAtomic = CounterfactualExecutionResult.CreateRooted(
            partialPlan,
            EvaluationCompletionStatus.Invalid,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Invalid,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Applied,
            0,
            partialPlan.Request.InstructionLimit,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [partialPlan.FieldObservations[0]],
            [0],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Invalid));
        Assert.Equal(EvaluationEvidenceStatus.Invalid, invalidAtomic.Evidence);
        Assert.Empty(invalidAtomic.Context.Events);
    }

    /// <summary>Rejects malformed model/call/frame/event structure and derives exact depth high-water witnesses.</summary>
    [Fact]
    public void ModelCallTraceAndEventTranscriptAreStructurallyJoined()
    {
        var plan = CreatePlan(modeled: true).Plan;
        var valid = CreateCompletedExact(plan, 42);
        Assert.Equal((2, 1),
            (valid.Context.Accounting.ObservedLogicalDepthHighWater,
                valid.Context.Accounting.ActiveFrameDepthHighWater));
        Assert.Single(valid.Context.ModelAttempts);

        var events = CreateFullEvents(plan);
        var accounting = CreateAccounting(plan, 6, 2, 1);
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, plan.FieldObservations, [0, 1], [CreateSuccessfulModelAttempt()], 2, 1,
            [Root, Helper], events));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            plan, accounting, plan.FieldObservations, [0, 1], [CreateSuccessfulModelAttempt()], 1, 1,
            [Root], events));

        var interpretedPlan = CreatePlan().Plan;
        var malformedEvents = CreateFullEvents(interpretedPlan).SetItem(
            6,
            new DebugEvent(DebugEventKind.FramePushed, Root, 0, "Entry"));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionContext.CreateRooted(
            interpretedPlan,
            CreateAccounting(interpretedPlan, 10, 2, 2),
            interpretedPlan.FieldObservations,
            [0, 1],
            [],
            0,
            0,
            [Root, Helper],
            malformedEvents));
    }

    /// <summary>Rejects non-Int32 lineage roots and canonical-storage corruption before result issuance.</summary>
    [Fact]
    public void UnknownReturnRequiresFreshCanonicalInt32LineageReplay()
    {
        var int32 = CreateLineage(EvaluationEvidenceStatus.Partial);
        var accepted = CounterfactualExecutionValue.CreateUnknownInt32(int32);
        Assert.Same(int32, accepted.Lineage);

        var domain = new ProvenanceConcreteDomain();
        var wrongType = domain.CaptureLineage(domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Partial,
            new ProvenanceSourceKey(DigestA),
            "W4.Input.Partial",
            Owner)));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionValue.CreateUnknownInt32(wrongType));

        var corrupted = CreateLineage(EvaluationEvidenceStatus.Partial);
        CorruptPrivateCanonicalBytes(corrupted, "canonicalBytes");
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionValue.CreateUnknownInt32(corrupted));
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionValue.CreateUnknownInt32(null!));
    }

    /// <summary>Rejects corrupted standalone fragments and proves the bridge has no alternate fact inputs.</summary>
    [Fact]
    public void StandaloneBridgeVerifiesCanonicalFragmentAndHasNoContradictoryFactory()
    {
        Assert.Throws<ArgumentNullException>(() => CounterfactualExecutionProjector.FromTargetOutcome(null!));
        var corrupted = CreateCertifiedTargetFragment();
        CorruptPrivateCanonicalBytes(corrupted, "_canonicalBytes");
        Assert.Throws<ArgumentException>(() => CounterfactualExecutionProjector.FromTargetOutcome(corrupted));

        Assert.Single(
            typeof(CounterfactualExecutionResult).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            static method => method.Name == "CreateRooted");
        Assert.Single(
            typeof(CounterfactualExecutionContext).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            static method => method.Name == "CreateRooted");
        Assert.Single(
            typeof(CounterfactualExecutionAccounting).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            static method => method.Name == "CreateRooted");
    }

    /// <summary>Guards the non-generic, non-constructible, capability-free public result surface.</summary>
    [Fact]
    public void PublicSurfaceIsObservationOnlyAndNotCallerConstructible()
    {
        var types = new[]
        {
            typeof(CounterfactualExecutionValue),
            typeof(CounterfactualExecutionAccounting),
            typeof(CounterfactualExecutionContext),
            typeof(CounterfactualExecutionResult),
        };
        Assert.All(types, type =>
        {
            Assert.False(type.IsGenericType);
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        });
        var surface = types.SelectMany(static type => type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
            .Select(static member => $"{member.Name}:{(member as PropertyInfo)?.PropertyType.FullName}")
            .ToArray();
        foreach (var forbidden in new[]
        {
            "Memory", "MachineState", "Resolver", "Registry", "ValueDomain", "Capability", "Issuer",
            "CancellationToken", "RuntimeBundle"
        })
        {
            Assert.DoesNotContain(surface, item => item.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static CounterfactualExecutionResult CreateCompletedExact(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        int value,
        ImmutableArray<EvaluationProvenance> provenance = default,
        ImmutableArray<EvaluationDiagnostic> diagnostics = default)
    {
        var modeled = !plan.ModeledMethods.IsEmpty;
        var used = modeled ? 6 : 10;
        var attempts = modeled ? ImmutableArray.Create(CreateSuccessfulModelAttempt()) : [];
        return CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExactInt32(value),
            CounterfactualBoundStatus.Applied,
            used,
            plan.Request.InstructionLimit - used,
            2,
            modeled ? 1 : 2,
            CounterfactualBoundStatus.NotReached,
            null,
            plan.FieldObservations,
            [0, 1],
            attempts,
            attempts.Length,
            attempts.Length,
            [Root, Helper],
            CreateFullEvents(plan),
            provenance,
            diagnostics);
    }

    private static CounterfactualExecutionResult CreateCompletedUnknown(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        ProvenanceLineageGraph lineage,
        EvaluationEvidenceStatus evidence) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Partial,
            evidence,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateUnknownInt32(lineage),
            CounterfactualBoundStatus.Applied,
            10,
            plan.Request.InstructionLimit - 10,
            2,
            2,
            CounterfactualBoundStatus.Applied,
            lineage.Nodes.Length,
            plan.FieldObservations,
            [0, 1],
            [],
            0,
            0,
            [Root, Helper],
            CreateFullEvents(plan));

    private static CounterfactualExecutionResult CreateBudgetPrefix(CounterfactualMethodPlan<ConcreteMemory> plan) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.BudgetExhausted,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExecutionPrefix(),
            CounterfactualBoundStatus.Exhausted,
            1,
            0,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [new DebugEvent(DebugEventKind.InstructionExecuted, Root, 0, "LoadArgument")],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.BudgetExhausted));

    private static CounterfactualExecutionResult CreateCancelledBeforeFirstInstruction(
        CounterfactualMethodPlan<ConcreteMemory> plan) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Cancelled,
            EvaluationCompleteness.None,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.NotReached,
            null,
            null,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Cancelled));

    private static CounterfactualExecutionResult CreateCancellationPrefix(
        CounterfactualMethodPlan<ConcreteMemory> plan) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            EvaluationCompletionStatus.Cancelled,
            EvaluationCompleteness.Partial,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            CounterfactualExecutionValue.CreateExecutionPrefix(),
            CounterfactualBoundStatus.Applied,
            1,
            plan.Request.InstructionLimit - 1,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [Root],
            [new DebugEvent(DebugEventKind.InstructionExecuted, Root, 0, "LoadArgument")],
            diagnostics: FailureDiagnostics(EvaluationCompletionStatus.Cancelled));

    private static CounterfactualExecutionResult CreateStoppedBeforeFirst(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        EvaluationEffectStatus effects,
        bool includeDiagnostic = true) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            completion,
            EvaluationCompleteness.None,
            evidence,
            effects,
            null,
            CounterfactualBoundStatus.NotReached,
            null,
            null,
            null,
            null,
            CounterfactualBoundStatus.NotReached,
            null,
            [],
            [],
            [],
            0,
            0,
            [],
            [],
            diagnostics: includeDiagnostic ? FailureDiagnostics(completion) : []);

    private static CounterfactualExecutionResult CreateStoppedAfterFirstField(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus resultEvidence)
    {
        var observation = plan.FieldObservations[0];
        return CounterfactualExecutionResult.CreateRooted(
            plan,
            completion,
            EvaluationCompleteness.None,
            resultEvidence,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Applied,
            2,
            plan.Request.InstructionLimit - 2,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [observation],
            [0],
            [],
            0,
            0,
            [Root],
            [
                new DebugEvent(DebugEventKind.InstructionExecuted, Root, 0, "LoadArgument"),
                new DebugEvent(DebugEventKind.InstructionExecuted, Root, 1, "LoadField"),
                new DebugEvent(DebugEventKind.ValuePrecisionLost, Root, 1, "LoadField", observation.RuntimeFieldEvidence)
            ],
            diagnostics: FailureDiagnostics(completion));
    }

    private static CounterfactualExecutionResult CreateStoppedAtTerminalField(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence) =>
        CounterfactualExecutionResult.CreateRooted(
            plan,
            completion,
            EvaluationCompleteness.None,
            evidence,
            EvaluationEffectStatus.None,
            null,
            CounterfactualBoundStatus.Applied,
            1,
            plan.Request.InstructionLimit - 1,
            1,
            1,
            CounterfactualBoundStatus.NotReached,
            null,
            [plan.FieldObservations[0]],
            [0],
            [],
            0,
            0,
            [Root],
            [new DebugEvent(DebugEventKind.InstructionExecuted, Root, 0, "LoadArgument")],
            diagnostics: FailureDiagnostics(completion));

    private static CounterfactualExecutionAccounting CreateAccounting(
        CounterfactualMethodPlan<ConcreteMemory> plan,
        long used,
        int logicalHighWater,
        int frameHighWater) =>
        CounterfactualExecutionAccounting.CreateRooted(
            plan,
            CounterfactualBoundStatus.Applied,
            used,
            plan.Request.InstructionLimit - used,
            logicalHighWater,
            frameHighWater,
            CounterfactualBoundStatus.NotReached,
            null);

    private static ImmutableArray<EvaluationDiagnostic> FailureDiagnostics(
        EvaluationCompletionStatus completion) =>
        [new EvaluationDiagnostic($"W4.Result.{completion}", "The result stopped before normal completion.")];

    private static ImmutableArray<DebugEvent> CreateFullEvents(CounterfactualMethodPlan<ConcreteMemory> plan)
    {
        var events = ImmutableArray.CreateBuilder<DebugEvent>();
        events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Root, 0, "LoadArgument"));
        AddFieldEvent(events, Root, 1, plan.FieldObservations[0]);
        events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Root, 6, "LoadArgument"));
        AddFieldEvent(events, Root, 7, plan.FieldObservations[1]);
        events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Root, 12, "Call"));
        if (plan.ModeledMethods.IsEmpty)
        {
            events.Add(new DebugEvent(DebugEventKind.FramePushed, Helper, 0, "Entry"));
            events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 0, "LoadArgument"));
            events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 1, "LoadArgument"));
            events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 2, "Add"));
            events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Helper, 3, "Return"));
            events.Add(new DebugEvent(DebugEventKind.FramePopped, Helper, 3, "Return"));
        }

        events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, Root, 17, "Return"));
        events.Add(new DebugEvent(DebugEventKind.FramePopped, Root, 17, "Return"));
        return events.ToImmutable();
    }

    private static void AddFieldEvent(
        ImmutableArray<DebugEvent>.Builder events,
        MethodHandle method,
        int ilOffset,
        CounterfactualFieldObservation observation)
    {
        events.Add(new DebugEvent(DebugEventKind.InstructionExecuted, method, ilOffset, "LoadField"));
        if (observation.RuntimeFieldEvidence is { } fieldEvidence)
        {
            events.Add(new DebugEvent(DebugEventKind.ValuePrecisionLost, method, ilOffset, "LoadField", fieldEvidence));
        }
    }

    private static PureModelAttempt CreateSuccessfulModelAttempt() => new(
        new DirectCallSiteIdentity(Root, 12, Helper),
        new PureCallModelIdentity(ModelId, Version),
        2,
        PureModelAttemptOutcomeKind.ExactReturn,
        true,
        null);

    private static PlanFixture CreatePlan(
        long instructionLimit = 20,
        EvaluationEvidenceStatus firstFieldEvidence = EvaluationEvidenceStatus.Exact,
        bool modeled = false,
        string policyId = "policy.result")
    {
        var resolver = CreateResolver();
        var request = CreateRequest(instructionLimit, modeled, policyId);
        MethodGraphPreparationResult preparation;
        IPureCallModelRegistry? registry = null;
        if (modeled)
        {
            var model = new FixtureModel(MethodGraphPlannerTests.Target(Helper));
            registry = new FixtureRegistry(model);
            preparation = new MethodGraphPlanner(resolver).RequirePureModel(Root, Helper, registry, 10);
        }
        else
        {
            preparation = new MethodGraphPlanner(resolver).Prepare(Root, 10);
        }

        Assert.True(preparation.IsSuccess, preparation.Failure?.Code);
        var observations = preparation.Plan!.Fields
            .Select((field, ordinal) => CreateObservation(
                request,
                ordinal,
                field,
                ordinal == 0 ? firstFieldEvidence : EvaluationEvidenceStatus.Exact))
            .ToImmutableArray();
        var domain = new ProvenanceConcreteDomain();
        var bundle = CounterfactualMethodExecutionInput<ConcreteMemory>.CreateSynthetic(
                request,
                resolver,
                domain,
                new UnreachableMemoryModel(),
                ConcreteMemory.Empty,
                domain.ObjectReference(1, Owner),
                observations,
                registry)
            .RuntimeBundle
            .MaterializeRootArguments();
        var issuer = new object();
        var plan = CounterfactualMethodPlan<ConcreteMemory>.Issue(
            issuer,
            request,
            preparation.Plan,
            preparation.TraversalAccounting!,
            bundle);
        return new PlanFixture(plan, issuer);
    }

    private static CounterfactualMethodRequest CreateRequest(long instructionLimit, bool modeled, string policyId) =>
        CounterfactualMethodRequest.CreateSynthetic(
            "fixture.counterfactual-result",
            "root.counterfactual-result",
            DigestA,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.exact", DigestA),
            [],
            policyId,
            Version,
            instructionLimit,
            2,
            10,
            "models.counterfactual-result",
            Version,
            modeled ? Helper : null,
            ["assume.read-only"]);

    private static CounterfactualFieldObservation CreateObservation(
        CounterfactualMethodRequest request,
        int ordinal,
        ResolvedField field,
        EvaluationEvidenceStatus evidence)
    {
        var sourceSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.SyntheticEvidenceId!)))
            .ToLowerInvariant();
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, ordinal == 0 ? 0x13579BDF : 0x13579BDE);
        return evidence switch
        {
            EvaluationEvidenceStatus.Exact => CounterfactualFieldObservation.CreateExactInt32(
                ordinal, field, sourceSha, request.Receiver.EvidenceSha256,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))), sizeof(int), bytes),
            EvaluationEvidenceStatus.Partial => CounterfactualFieldObservation.CreateNonExactInt32(
                ordinal, field, evidence, "W4.Field.Partial", sourceSha, request.Receiver.EvidenceSha256,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))), sizeof(int), bytes[..2]),
            EvaluationEvidenceStatus.Unavailable => CounterfactualFieldObservation.CreateNonExactInt32(
                ordinal, field, evidence, "W4.Field.Unavailable", sourceSha, request.Receiver.EvidenceSha256,
                checked(0x1000UL + ((ulong)ordinal * sizeof(int))), sizeof(int), []),
            EvaluationEvidenceStatus.Conflict or EvaluationEvidenceStatus.Invalid =>
                CounterfactualFieldObservation.CreateNonExactInt32(
                    ordinal, field, evidence, $"W4.Field.{evidence}", sourceSha, request.Receiver.EvidenceSha256,
                    checked(0x1000UL + ((ulong)ordinal * sizeof(int))), sizeof(int), bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
        };
    }

    private static MethodGraphPlannerTests.GraphResolver CreateResolver()
    {
        var rootDefinition = MethodGraphPlannerTests.RootDefinition(
            Root,
            MethodGraphPlannerTests.ExactRootBody(
                Field.Handle.MetadataToken,
                OtherField.Handle.MetadataToken,
                Helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = MethodGraphPlannerTests.HelperDefinition(
            Helper,
            [0x02, 0x03, 0x58, 0x2A],
            maxStack: 2);
        var resolver = MethodGraphPlannerTests.Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(Root, Field.Handle.MetadataToken)] = Field;
        resolver.Fields[(Root, OtherField.Handle.MetadataToken)] = OtherField;
        resolver.Calls[(Root, Helper.MetadataToken)] = MethodGraphPlannerTests.Target(Helper);
        return resolver;
    }

    private static ProvenanceLineageGraph CreateLineage(EvaluationEvidenceStatus evidence)
    {
        var domain = new ProvenanceConcreteDomain();
        return domain.CaptureLineage(domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.ImportedField,
            0,
            evidence,
            new ProvenanceSourceKey(DigestA),
            $"W4.Field.{evidence}",
            TypeSig.Int32)));
    }

    private static CounterfactualTargetOutcomeFragment CreateCertifiedTargetFragment()
    {
        var definition = new ResolvedMethodDefinition(
            Root,
            IlBody.Create(
                1,
                [0x02, 0x7B, 0x01, 0x00, 0x00, 0x04, 0x2A]),
            new MethodSignatureShape(
                Owner,
                MethodCallingConventionKind.Default,
                hasImplicitThis: true,
                hasExplicitThis: false,
                genericParameterCount: 0,
                ImmutableArray<TypeSig>.Empty,
                TypeSig.Int32,
                ImmutableArray<TypeSig>.Empty));
        var domain = new ConcreteDomain();
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            new FixedResolver(definition, Field),
            new ConcreteMemoryModel(domain),
            new InstructionBudgetPolicy());
        var activation = machine.ActivateRoot(
            Root,
            ImmutableArray.Create(domain.ConstNull(Owner)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var initialState = activation.State!;
        var initialOperations = new MachineOperationalState(new BudgetState(10));
        var first = machine.StepOne(initialState, initialOperations);
        var terminal = machine.StepOne(first.State, first.OperationalState);
        Assert.True(first.IsMachineIssuedTransitionFrom(machine, initialState, initialOperations));
        Assert.True(terminal.IsMachineIssuedTransitionFrom(machine, first.State, first.OperationalState));
        var projection = CounterfactualTargetOutcomeProjector.Project(
            machine,
            initialState,
            initialOperations,
            ImmutableArray.Create(first, terminal));
        Assert.True(projection.IsSuccess, projection.Failure?.Code);
        return Assert.IsType<CounterfactualTargetOutcomeFragment>(projection.Fragment);
    }

    private static void AssertCanonicalIntegrity(CounterfactualExecutionResult result)
    {
        Assert.Equal(result.Sha256, CounterfactualExecutionCanonicalCodec.ComputeSha256(result));
        Assert.True(result.CanonicalBytes.AsSpan().SequenceEqual(
            CounterfactualExecutionCanonicalCodec.SerializeCanonical(result).AsSpan()));
    }

    private static void AssertCanonicalReplay(
        CounterfactualExecutionResult expected,
        CounterfactualExecutionResult actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Sha256, actual.Sha256);
        Assert.True(expected.CanonicalBytes.AsSpan().SequenceEqual(actual.CanonicalBytes.AsSpan()));
    }

    private static void AssertIdentityFree(CounterfactualExecutionContext context)
    {
        Assert.Null(context.Request);
        Assert.Null(context.PlanSchemaVersion);
        Assert.Null(context.PlanSha256);
        Assert.Null(context.RootMethod);
        Assert.Same(EvaluationEvidenceContext.Neutral, context.EvidenceContext);
        Assert.Empty(context.InterpretedMethods);
        Assert.Empty(context.ModeledMethods);
        Assert.Empty(context.PlannedFields);
        Assert.Empty(context.CallDispositions);
        Assert.Empty(context.ReachedFieldObservations);
        Assert.Empty(context.ReachedFieldLoadOrdinals);
        Assert.Empty(context.ModelAttempts);
        Assert.Empty(context.CallTrace);
        Assert.Empty(context.Events);
    }

    private static void AssertAllNonInstructionBoundsNotApplicable(CounterfactualExecutionAccounting accounting)
    {
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, accounting.TraversalStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, accounting.DepthStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, accounting.LineageStatus);
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, accounting.AllocationStatus);
    }

    private static void AssertAllBoundsNotApplicable(CounterfactualExecutionAccounting accounting)
    {
        Assert.Equal(CounterfactualBoundStatus.NotApplicable, accounting.InstructionStatus);
        AssertAllNonInstructionBoundsNotApplicable(accounting);
    }

    private static void Mutate<T>(ImmutableArray<T> values, T replacement)
    {
        if (!values.IsEmpty)
        {
            ImmutableCollectionsMarshal.AsArray(values)![0] = replacement;
        }
    }

    private static void CorruptPrivateCanonicalBytes(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var retained = (ImmutableArray<byte>)field.GetValue(instance)!;
        ImmutableCollectionsMarshal.AsArray(retained)![0] ^= 0xff;
    }

    private static bool Contains(ImmutableArray<byte> haystack, ReadOnlySpan<byte> needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    private sealed record PlanFixture(CounterfactualMethodPlan<ConcreteMemory> Plan, object Issuer);

    private sealed class FixtureModel(ResolvedMethodCallTarget target) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = new(
            new PureCallModelIdentity(ModelId, Version),
            target,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation) =>
            throw new InvalidOperationException("Result-schema tests never invoke a model capability.");
    }

    private sealed class FixtureRegistry(IPureCallModel model) : IPureCallModelRegistry
    {
        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target) =>
            PureCallModelSelectionResult.Selected(model);
    }

    private sealed class UnreachableMemoryModel : IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>
    {
        public bool CanAllocate => false;

        public (ProvenanceConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");

        public (ProvenanceConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");

        public ProvenanceConcreteValue LoadElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) =>
            throw new InvalidOperationException("Result-schema tests do not execute memory operations.");
    }

    private sealed class FixedResolver(
        ResolvedMethodDefinition definition,
        ResolvedField field) : IResolutionServices
    {
        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            method == definition.Method
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Invalid,
                    "TEST_METHOD_MISMATCH",
                    "The certified fixture received another method.");

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken) =>
            ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Unsupported,
                "TEST_CALL_UNSUPPORTED",
                "The certified fixture has no direct call.");

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            contextMethod == definition.Method && metadataToken == field.Handle.MetadataToken
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Invalid,
                    "TEST_FIELD_MISMATCH",
                    "The certified fixture received another field.");
    }
}

internal static class CounterfactualExecutionResultTestExtensions
{
    internal static bool TraversalAndPlanCallsEqual(
        this CounterfactualExecutionContext context,
        CounterfactualMethodPlan<ConcreteMemory> plan) =>
        context.Accounting.TraversalCharges.SequenceEqual(plan.TraversalCharges) &&
        context.CallDispositions.Select(static item =>
                (item.Caller, item.IlOffset, item.MetadataToken, item.TargetMethod, item.Disposition,
                    item.Effects, item.ModelId, item.ModelVersion, item.ModelConfidence))
            .SequenceEqual(plan.CallSites.Select(static item =>
                (item.Caller, item.IlOffset, item.MetadataToken, item.TargetMethod, item.Disposition,
                    item.Effects, item.ModelId, item.ModelVersion, item.ModelConfidence)));
}
