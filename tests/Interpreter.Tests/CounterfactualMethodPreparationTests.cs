using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Domain.Concrete;
using Interpreter.Product.DumpDebugging;
using Xunit;

namespace Interpreter.Tests;

/// <summary>Exercises W4.8's nonthrowing rooted-method preparation and private plan-issuance boundary.</summary>
public sealed class CounterfactualMethodPreparationTests
{
    private const string DigestA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string DigestB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string SyntheticId = "fixture.synthetic.preparation.v1";
    private static readonly MethodHandle Root = MethodGraphPlannerTests.Method(1);
    private static readonly MethodHandle Helper = MethodGraphPlannerTests.Method(2);
    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField FirstField = MethodGraphPlannerTests.Field(1, Owner);
    private static readonly ResolvedField SecondField = MethodGraphPlannerTests.Field(2, Owner);
    private static readonly ResolvedField ThirdField = MethodGraphPlannerTests.Field(3, Owner);

    /// <summary>
    /// Proves raw creation preserves malformed/default states and defensive snapshots without exposing operational
    /// capabilities as public instance state.
    /// </summary>
    [Fact]
    public void CandidateSnapshotsRawMalformedDataWithoutValidationOrCapabilityExposure()
    {
        var domain = new ProvenanceConcreteDomain();
        var argumentSource = new CounterfactualInputEvidence[] { null! };
        var observationSource = new CounterfactualFieldObservation[] { null! };
        var candidate = CounterfactualMethodPreparationCandidate<ConcreteMemory>.CreateSynthetic(
            syntheticEvidenceId: null,
            rootSelectionId: "INVALID VALUE",
            rootEvidenceSha256: "short",
            rootMethod: default,
            receiverEvidence: null,
            ImmutableCollectionsMarshal.AsImmutableArray(argumentSource),
            policyId: null,
            policyVersion: default,
            instructionLimit: -1,
            logicalDepthLimit: -1,
            traversalLimit: -1,
            modelCatalogId: null,
            modelCatalogVersion: default,
            requiredModelTarget: null,
            assumptions: default,
            resolver: null,
            domain,
            memoryModel: null,
            initialMemory: null,
            receiver: null,
            ImmutableCollectionsMarshal.AsImmutableArray(observationSource));
        argumentSource[0] = CounterfactualInputEvidence.CreateExactInt32("argument.later", DigestA, 1);
        observationSource[0] = CreateExactObservation(0, FirstField);

        Assert.Null(candidate.Arguments[0]);
        Assert.Null(candidate.FieldObservations[0]);
        Assert.True(candidate.Assumptions.IsDefault);
        var publicMembers = typeof(CounterfactualMethodPreparationCandidate<ConcreteMemory>)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(publicMembers, property =>
            property.Name.Contains("Resolver", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Registry", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Maps malformed request data and missing bindings before any resolver or registry capability call.</summary>
    [Fact]
    public void RawAndBindingFailuresAreStableNeutralAndNonthrowing()
    {
        var poisonResolver = new PoisonResolver();
        var malformed = CreateCandidate(
            resolver: poisonResolver,
            rootDigest: "not-a-digest");

        var raw = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(malformed);

        AssertFailure(raw, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid, "W4.Request.Invalid");
        Assert.Null(raw.Failure!.RequestSha256);
        Assert.Same(EvaluationEvidenceContext.Neutral, raw.Failure.Context);
        Assert.Null(raw.Failure.TraversalAccounting);
        Assert.Equal(0, poisonResolver.CallCount);

        var missing = CreateCandidate(omitResolver: true);
        var binding = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(missing);

        AssertFailure(binding, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid, "W4.Request.BindingMissing");
        Assert.NotNull(binding.Failure!.RequestSha256);
        Assert.Equal(EvaluationEvidenceSourceKind.Synthetic, binding.Failure.Context.SourceKind);
        Assert.Empty(binding.Failure.Context.Bounds);
        Assert.Null(binding.Failure.TraversalAccounting);
    }

    /// <summary>
    /// Enforces request-correlated observation binding and a fresh lineage domain before graph discovery starts.
    /// </summary>
    [Fact]
    public void ObservationBindingAndFreshDomainRejectBeforePlanner()
    {
        var resolver = CreateResolver();
        var malformed = CreateCandidate(resolver: resolver, observations: default(ImmutableArray<CounterfactualFieldObservation>));
        var malformedResult = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(malformed);

        AssertFailure(
            malformedResult,
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            "W4.Evidence.FieldObservationInvalid");
        Assert.Empty(resolver.Operations);

        var nonFreshDomain = new ProvenanceConcreteDomain();
        _ = nonFreshDomain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            0,
            EvaluationEvidenceStatus.Partial,
            new ProvenanceSourceKey(DigestA),
            "W4.Unknown.Test",
            TypeSig.Int32));
        var secondResolver = CreateResolver();
        var nonFresh = CreateCandidate(resolver: secondResolver, domain: nonFreshDomain);
        var nonFreshResult = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(nonFresh);

        AssertFailure(
            nonFreshResult,
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            "W4.Request.DomainNotFresh");
        Assert.Empty(secondResolver.Operations);
    }

    /// <summary>
    /// Proves traversal zero wins depth zero before resolver or registry calls for both interpreted and modeled
    /// requests, while the context reports only the reached traversal bound.
    /// </summary>
    [Fact]
    public void ZeroTraversalPrecedesDepthResolverAndRegistry()
    {
        var interpretedResolver = new PoisonResolver();
        var interpretedRegistry = new PoisonRegistry();
        var interpreted = CreateCandidate(
            resolver: interpretedResolver,
            traversalLimit: 0,
            logicalDepth: 0,
            arguments: ImmutableArray.Create(CounterfactualInputEvidence.CreateUnknownInt32(
                "argument.unreached",
                DigestA,
                EvaluationEvidenceStatus.Unavailable)),
            registry: interpretedRegistry);
        var interpretedResult = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(interpreted);

        AssertTraversalExhausted(interpretedResult);
        Assert.Equal(0, interpretedResolver.CallCount);
        Assert.Equal(0, interpretedRegistry.CallCount);

        var modeledResolver = new PoisonResolver();
        var modeledRegistry = new PoisonRegistry();
        var modeled = CreateCandidate(
            resolver: modeledResolver,
            traversalLimit: 0,
            logicalDepth: 0,
            requiredModelTarget: Helper,
            registry: modeledRegistry);
        var modeledResult = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(modeled);

        AssertTraversalExhausted(modeledResult);
        Assert.Equal(0, modeledResolver.CallCount);
        Assert.Equal(0, modeledRegistry.CallCount);
    }

    /// <summary>
    /// Issues an exact-last-unit interpreted plan, never queries the optional registry, retains materialized root
    /// arguments privately, and admits conflict/invalid observations as execution-time evidence dispositions.
    /// </summary>
    [Fact]
    public void ExactLastTraversalUnitIssuesPrivatePlanAndAllowsNonExactObservationStatuses()
    {
        var resolver = CreateResolver();
        var registry = new PoisonRegistry();
        var observations = ImmutableArray.Create(
            CreateNonExactObservation(0, FirstField, EvaluationEvidenceStatus.Conflict),
            CreateNonExactObservation(1, SecondField, EvaluationEvidenceStatus.Invalid));
        var candidate = CreateCandidate(
            resolver: resolver,
            traversalLimit: 5,
            observations: observations,
            registry: registry);
        var runner = new CounterfactualMethodRunner<ConcreteMemory>();

        var result = runner.Prepare(candidate);

        Assert.True(result.IsSuccess, result.Failure?.Diagnostics[0].Code);
        Assert.Null(result.Failure);
        var plan = Assert.IsType<CounterfactualMethodPlan<ConcreteMemory>>(result.Plan);
        Assert.Equal(5, plan.TraversalLimit);
        Assert.Equal(5, plan.TraversalUsed);
        Assert.Equal(0, plan.TraversalRemaining);
        Assert.Equal([EvaluationEvidenceStatus.Conflict, EvaluationEvidenceStatus.Invalid],
            plan.FieldObservations.Select(static observation => observation.EvidenceStatus));
        Assert.True(plan.IsIssuedBy(GetIssuer(runner)));
        Assert.True(plan.RuntimeBundle.HasMaterializedRootArguments);
        Assert.Single(plan.RuntimeBundle.RootArguments);
        Assert.Equal(0, registry.CallCount);
    }

    /// <summary>
    /// Applies call depth only after complete graph preparation and rejects explicit arguments at the current
    /// zero-argument W4 facade before provenance materialization.
    /// </summary>
    [Fact]
    public void DepthAndRootSignatureFailuresOccurAfterGraphBeforeMaterialization()
    {
        var depthResolver = CreateResolver();
        var depthDomain = new ProvenanceConcreteDomain();
        var depthCandidate = CreateCandidate(
            resolver: depthResolver,
            domain: depthDomain,
            logicalDepth: 1,
            arguments: ImmutableArray.Create(CounterfactualInputEvidence.CreateUnknownInt32(
                "argument.unreached",
                DigestA,
                EvaluationEvidenceStatus.Partial)));
        var depth = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(depthCandidate);

        AssertFailure(depth, EvaluationCompletionStatus.BudgetExhausted, EvaluationEvidenceStatus.Exact, "W4.Budget.CallDepth");
        Assert.NotEmpty(depthResolver.Operations);
        Assert.Equal(0, depthDomain.InternedNodeCount);
        Assert.Equal(
            ["counterfactual.execution.logical-call-depth", "counterfactual.preparation.traversal-units"],
            depth.Failure!.Context.Bounds.Select(static bound => bound.Name));

        var signatureResolver = CreateResolver();
        var signatureDomain = new ProvenanceConcreteDomain();
        var signatureCandidate = CreateCandidate(
            resolver: signatureResolver,
            domain: signatureDomain,
            arguments: ImmutableArray.Create(CounterfactualInputEvidence.CreateUnknownInt32(
                "argument.partial",
                DigestA,
                EvaluationEvidenceStatus.Partial)));
        var signature = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(signatureCandidate);

        AssertFailure(signature, EvaluationCompletionStatus.Invalid, EvaluationEvidenceStatus.Invalid, "W4.Request.RootSignature");
        Assert.NotEmpty(signatureResolver.Operations);
        Assert.Equal(0, signatureDomain.InternedNodeCount);
    }

    /// <summary>Rejects graph-relative field incompleteness only after successful traversal and depth validation.</summary>
    [Fact]
    public void GraphRelativeObservationMismatchRejectsAfterPlanning()
    {
        var resolver = CreateResolver();
        var observations = ImmutableArray.Create(
            CreateExactObservation(0, FirstField),
            CreateExactObservation(1, ThirdField));
        var result = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(
            CreateCandidate(resolver: resolver, observations: observations));

        AssertFailure(
            result,
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            "W4.Evidence.FieldObservationInvalid");
        Assert.NotEmpty(resolver.Operations);
        Assert.NotNull(result.Failure!.TraversalAccounting);
        Assert.Equal(2, result.Failure.Context.Bounds.Length);
    }

    /// <summary>
    /// Maps unavailable, conflicting, invalid, and exact-unsupported graph outcomes to closed product diagnostics
    /// while removing arbitrary resolver codes and messages from the public core failure.
    /// </summary>
    [Fact]
    public void PlannerFailureMatrixUsesClosedAxesAndNormalizedCoreFailures()
    {
        AssertPlannerFailure(
            new FailingResolver(ResolutionFailureKind.Unavailable),
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Unavailable,
            "W4.Evidence.ProgramUnavailable");
        AssertPlannerFailure(
            new FailingResolver(ResolutionFailureKind.Conflict),
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Conflict,
            "W4.Evidence.ProgramConflict");
        AssertPlannerFailure(
            new FailingResolver(ResolutionFailureKind.Invalid),
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            "W4.Evidence.ProgramInvalid");
        AssertPlannerFailure(
            new FailingResolver(ResolutionFailureKind.Unsupported),
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            "W4.Admission.Unsupported");

        var unsupportedResolver = MethodGraphPlannerTests.Resolver(
            MethodGraphPlannerTests.RootDefinition(Root, [0x16, 0x2A], maxStack: 1));
        AssertPlannerFailure(
            unsupportedResolver,
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            "W4.Admission.Unsupported");
    }

    /// <summary>Projects an unsupported selected model effect independently from exact program evidence.</summary>
    [Fact]
    public void UnsupportedModelEffectSetsOnlyTheEffectsAxis()
    {
        var resolver = CreateResolver();
        var registry = new SelectedRegistry(Helper, EvaluationEffectStatus.Unsupported);
        var result = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(
            resolver: resolver,
            requiredModelTarget: Helper,
            registry: registry));

        AssertFailure(
            result,
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            "W4.Model.EffectUnsupported");
        Assert.Equal(EvaluationEffectStatus.Unsupported, result.Failure!.Effects);
        Assert.Equal(1, registry.CallCount);
    }

    /// <summary>
    /// Selects one exact effect-free model once per preparation without loading its body and reproduces canonical
    /// request and plan identities through the same runner and a fresh runner.
    /// </summary>
    [Fact]
    public void EffectFreeModeledPreparationIsSingleSelectBodyFreeAndReplayStable()
    {
        var firstResolver = CreateResolver();
        var firstRegistry = new SelectedRegistry(Helper, EvaluationEffectStatus.None);
        var secondResolver = CreateResolver();
        var secondRegistry = new SelectedRegistry(Helper, EvaluationEffectStatus.None);
        var freshResolver = CreateResolver();
        var freshRegistry = new SelectedRegistry(Helper, EvaluationEffectStatus.None);
        var runner = new CounterfactualMethodRunner<ConcreteMemory>();

        var first = runner.Prepare(CreateCandidate(
            resolver: firstResolver,
            requiredModelTarget: Helper,
            registry: firstRegistry));
        var second = runner.Prepare(CreateCandidate(
            resolver: secondResolver,
            requiredModelTarget: Helper,
            registry: secondRegistry));
        var fresh = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(
            resolver: freshResolver,
            requiredModelTarget: Helper,
            registry: freshRegistry));

        Assert.True(first.IsSuccess, first.Failure?.Diagnostics[0].Code);
        Assert.True(second.IsSuccess, second.Failure?.Diagnostics[0].Code);
        Assert.True(fresh.IsSuccess, fresh.Failure?.Diagnostics[0].Code);
        Assert.Equal(1, firstRegistry.CallCount);
        Assert.Equal(1, secondRegistry.CallCount);
        Assert.Equal(1, freshRegistry.CallCount);
        Assert.Equal(0, firstResolver.DefinitionCount(Helper));
        Assert.Equal(0, secondResolver.DefinitionCount(Helper));
        Assert.Equal(0, freshResolver.DefinitionCount(Helper));
        Assert.Equal(Helper, Assert.Single(first.Plan!.ModeledMethods));
        Assert.Equal(first.Plan.Request.Sha256, second.Plan!.Request.Sha256);
        Assert.Equal(first.Plan.Request.Sha256, fresh.Plan!.Request.Sha256);
        Assert.Equal(first.Plan.Sha256, second.Plan.Sha256);
        Assert.Equal(first.Plan.Sha256, fresh.Plan.Sha256);
        Assert.True(first.Plan.CanonicalBytes.AsSpan().SequenceEqual(second.Plan.CanonicalBytes.AsSpan()));
        Assert.True(first.Plan.CanonicalBytes.AsSpan().SequenceEqual(fresh.Plan.CanonicalBytes.AsSpan()));
    }

    /// <summary>
    /// Rejects a missing required registry before resolver use and normalizes resolver or registry exceptions into
    /// closed exact-evidence admission failures without touching the bound poison memory capability.
    /// </summary>
    [Fact]
    public void MissingAndThrowingPreparationCapabilitiesHaveClosedPreExecutionFailures()
    {
        var missingResolver = CreateResolver();
        var missing = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(
            resolver: missingResolver,
            requiredModelTarget: Helper,
            registry: null));

        AssertFailure(
            missing,
            EvaluationCompletionStatus.Invalid,
            EvaluationEvidenceStatus.Invalid,
            "W4.Request.ModelRegistryMissing");
        Assert.Empty(missingResolver.Operations);
        Assert.Empty(missing.Failure!.Context.Bounds);

        var throwingResolver = new PoisonResolver();
        var resolverFailure = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(
            resolver: throwingResolver));
        AssertFailure(
            resolverFailure,
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            "W4.Admission.Unsupported");
        Assert.Equal(1, throwingResolver.CallCount);
        Assert.Equal("W4.Admission.Unsupported", resolverFailure.Failure!.CoreFailure!.Code);

        var modeledResolver = CreateResolver();
        var throwingRegistry = new PoisonRegistry();
        var registryFailure = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(
            resolver: modeledResolver,
            requiredModelTarget: Helper,
            registry: throwingRegistry));
        AssertFailure(
            registryFailure,
            EvaluationCompletionStatus.Blocked,
            EvaluationEvidenceStatus.Exact,
            "W4.Admission.Unsupported");
        Assert.Equal(1, throwingRegistry.CallCount);
        Assert.Equal("W4.Admission.Unsupported", registryFailure.Failure!.CoreFailure!.Code);
    }

    /// <summary>Defensively copies public failure diagnostics and provenance and preserves strict union invariants.</summary>
    [Fact]
    public void FailureProjectionIsStrictAndDefensive()
    {
        var result = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(null);
        var failure = Assert.IsType<CounterfactualMethodPreparationFailure>(result.Failure);
        var diagnostics = failure.Diagnostics;
        ImmutableCollectionsMarshal.AsArray(diagnostics)![0] = new EvaluationDiagnostic("W4.Mutated", "Mutation.");
        Assert.Equal("W4.Request.CandidateMissing", failure.Diagnostics[0].Code);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Equal(EvaluationSemanticMode.CounterfactualExecution, failure.SemanticMode);
        Assert.Equal(EvaluationCompleteness.None, failure.Completeness);

        var plannedFailure = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(
            CreateCandidate(resolver: new FailingResolver(ResolutionFailureKind.Unavailable))).Failure!;
        var provenance = plannedFailure.Provenance;
        ImmutableCollectionsMarshal.AsArray(provenance)![0] = new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            "mutated");
        Assert.StartsWith("counterfactual-request-sha256:", plannedFailure.Provenance[0].SourceId);
    }

    private static CounterfactualMethodPreparationCandidate<ConcreteMemory> CreateCandidate(
        IResolutionServices? resolver = null,
        bool omitResolver = false,
        ProvenanceConcreteDomain? domain = null,
        int traversalLimit = 10,
        int logicalDepth = 2,
        string? rootDigest = DigestA,
        ImmutableArray<CounterfactualInputEvidence>? arguments = null,
        ImmutableArray<CounterfactualFieldObservation>? observations = null,
        MethodHandle? requiredModelTarget = null,
        IPureCallModelRegistry? registry = null)
    {
        domain ??= new ProvenanceConcreteDomain();
        return CounterfactualMethodPreparationCandidate<ConcreteMemory>.CreateSynthetic(
            SyntheticId,
            "root.selection.preparation.v1",
            rootDigest,
            Root,
            CounterfactualInputEvidence.CreateExactNonNullReceiver(Owner, "receiver.preparation", DigestB),
            arguments ?? ImmutableArray<CounterfactualInputEvidence>.Empty,
            "policy.counterfactual",
            new PureCallModelVersion(1, 0, 0),
            instructionLimit: 32,
            logicalDepth,
            traversalLimit,
            "catalog.preparation",
            new PureCallModelVersion(1, 0, 0),
            requiredModelTarget,
            ImmutableArray.Create("assume.read-only"),
            omitResolver ? null : resolver ?? CreateResolver(),
            domain,
            new PoisonMemoryModel(),
            ConcreteMemory.Empty,
            domain.ObjectReference(7, Owner),
            observations ?? CreateExactObservations(),
            registry);
    }

    private static MethodGraphPlannerTests.GraphResolver CreateResolver()
    {
        var rootDefinition = MethodGraphPlannerTests.RootDefinition(
            Root,
            MethodGraphPlannerTests.ExactRootBody(
                FirstField.Handle.MetadataToken,
                SecondField.Handle.MetadataToken,
                Helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = MethodGraphPlannerTests.HelperDefinition(
            Helper,
            [0x02, 0x03, 0x58, 0x2A],
            maxStack: 2);
        var resolver = MethodGraphPlannerTests.Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(Root, FirstField.Handle.MetadataToken)] = FirstField;
        resolver.Fields[(Root, SecondField.Handle.MetadataToken)] = SecondField;
        resolver.Calls[(Root, Helper.MetadataToken)] = MethodGraphPlannerTests.Target(Helper);
        return resolver;
    }

    private static ImmutableArray<CounterfactualFieldObservation> CreateExactObservations() =>
        ImmutableArray.Create(CreateExactObservation(0, FirstField), CreateExactObservation(1, SecondField));

    private static CounterfactualFieldObservation CreateExactObservation(int ordinal, ResolvedField field)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, ordinal + 100);
        return CounterfactualFieldObservation.CreateExactInt32(
            ordinal,
            field,
            SourceSha256(),
            DigestB,
            checked(0x2000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            bytes);
    }

    private static CounterfactualFieldObservation CreateNonExactObservation(
        int ordinal,
        ResolvedField field,
        EvaluationEvidenceStatus status) =>
        CounterfactualFieldObservation.CreateNonExactInt32(
            ordinal,
            field,
            status,
            status == EvaluationEvidenceStatus.Conflict ? "W4.Field.Conflict" : "W4.Field.Invalid",
            SourceSha256(),
            DigestB,
            checked(0x2000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            []);

    private static string SourceSha256() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SyntheticId))).ToLowerInvariant();

    private static object GetIssuer(CounterfactualMethodRunner<ConcreteMemory> runner) =>
        typeof(CounterfactualMethodRunner<ConcreteMemory>)
            .GetField("issuer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runner)!;

    private static void AssertTraversalExhausted(CounterfactualMethodPreparationResult<ConcreteMemory> result)
    {
        AssertFailure(
            result,
            EvaluationCompletionStatus.BudgetExhausted,
            EvaluationEvidenceStatus.Exact,
            "W4.Budget.Traversal");
        var failure = result.Failure!;
        Assert.Equal(EvaluationEffectStatus.None, failure.Effects);
        var accounting = Assert.IsType<MethodGraphTraversalAccounting>(failure.TraversalAccounting);
        Assert.True(accounting.IsExhausted);
        Assert.Equal(0, accounting.Used);
        Assert.Equal(0, accounting.Remaining);
        Assert.Equal(
            ["counterfactual.preparation.traversal-units"],
            failure.Context.Bounds.Select(static bound => bound.Name));
    }

    private static void AssertPlannerFailure(
        IResolutionServices resolver,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        string code)
    {
        var result = new CounterfactualMethodRunner<ConcreteMemory>().Prepare(CreateCandidate(resolver: resolver));
        AssertFailure(result, completion, evidence, code);
        var failure = result.Failure!;
        var core = Assert.IsType<ExecutionFailure>(failure.CoreFailure);
        Assert.Equal(code, core.Code);
        Assert.Equal(failure.Diagnostics[0].Message, core.Message);
        Assert.DoesNotContain("ARTIFACT", core.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\fixture-source", core.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(failure.Context.Bounds);
        Assert.Equal("counterfactual.preparation.traversal-units", failure.Context.Bounds[0].Name);
    }

    private static void AssertFailure(
        CounterfactualMethodPreparationResult<ConcreteMemory> result,
        EvaluationCompletionStatus completion,
        EvaluationEvidenceStatus evidence,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        var failure = Assert.IsType<CounterfactualMethodPreparationFailure>(result.Failure);
        Assert.Equal(EvaluationSemanticMode.CounterfactualExecution, failure.SemanticMode);
        Assert.Equal(completion, failure.Completion);
        Assert.Equal(EvaluationCompleteness.None, failure.Completeness);
        Assert.Equal(evidence, failure.Evidence);
        Assert.Equal(code, Assert.Single(failure.Diagnostics).Code);
    }

    private sealed class FailingResolver(ResolutionFailureKind kind) : IResolutionServices
    {
        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            ResolutionResult<ResolvedMethodDefinition>.Failed(
                kind,
                "ARTIFACT_RESOLVER_CODE",
                "C:\\fixture-source\\target\\artifact-name.dll must never escape.");

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken) =>
            throw new InvalidOperationException("Root failure must precede field resolution.");

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(MethodHandle contextMethod, int metadataToken) =>
            throw new InvalidOperationException("Root failure must precede call resolution.");
    }

    private sealed class PoisonResolver : IResolutionServices
    {
        public int CallCount { get; private set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            CallCount++;
            throw new InvalidOperationException("Resolver must not be queried.");
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            CallCount++;
            throw new InvalidOperationException("Resolver must not be queried.");
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(MethodHandle contextMethod, int metadataToken)
        {
            CallCount++;
            throw new InvalidOperationException("Resolver must not be queried.");
        }
    }

    private sealed class PoisonRegistry : IPureCallModelRegistry
    {
        public int CallCount { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            CallCount++;
            throw new InvalidOperationException("Registry must not be queried.");
        }
    }

    private sealed class SelectedRegistry : IPureCallModelRegistry
    {
        private readonly IPureCallModel model;

        internal SelectedRegistry(MethodHandle target, EvaluationEffectStatus effects)
        {
            model = new SelectedModel(MethodGraphPlannerTests.Target(target), effects);
        }

        internal int CallCount { get; private set; }

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            CallCount++;
            return PureCallModelSelectionResult.Selected(model);
        }
    }

    private sealed class SelectedModel(ResolvedMethodCallTarget target, EvaluationEffectStatus effects) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = new(
            new PureCallModelIdentity("w4.preparation.model", new PureCallModelVersion(1, 0, 0)),
            target,
            PureCallModelConfidence.Exact,
            effects);

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation) =>
            throw new InvalidOperationException("Preparation must not invoke a selected model.");
    }

    private sealed class PoisonMemoryModel : IMemoryModel<ProvenanceConcreteValue, ConcreteMemory>
    {
        public bool CanAllocate => throw new InvalidOperationException("Preparation must not inspect memory capabilities.");

        public (ProvenanceConcreteValue objRef, ConcreteMemory mem) NewObject(ConcreteMemory mem, TypeSig type) =>
            throw new InvalidOperationException("Preparation must not allocate.");

        public (ProvenanceConcreteValue arrRef, ConcreteMemory mem) NewArray(
            ConcreteMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length) => throw new InvalidOperationException("Preparation must not allocate.");

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field) => throw new InvalidOperationException("Preparation must not read memory.");

        public ConcreteMemory StoreField(
            ConcreteMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value) => throw new InvalidOperationException("Preparation must not write memory.");

        public ProvenanceConcreteValue LoadElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index) => throw new InvalidOperationException("Preparation must not read memory.");

        public ConcreteMemory StoreElement(
            ConcreteMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value) => throw new InvalidOperationException("Preparation must not write memory.");
    }
}
