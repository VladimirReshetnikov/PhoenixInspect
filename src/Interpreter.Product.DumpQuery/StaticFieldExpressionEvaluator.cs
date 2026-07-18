using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Runs the complete W7 parse, bind, runtime-map, storage-read, and semantic-composition pipeline.</summary>
/// <remarks>
/// The evaluator parses exactly once and shares one metadata source across symbol binding, nullable child-role
/// composition, and reference assignability. It deliberately returns typed stops for ordinary evidence failures.
/// The caller owns the open dump session and any separately acquired selected-frame/PDB context.
/// </remarks>
public static class StaticFieldExpressionEvaluator
{
    /// <summary>Evaluates a context-independent fully qualified static-field expression in one immutable dump.</summary>
    /// <param name="session">The open dump session supplying metadata, runtime catalogs, and raw memory.</param>
    /// <param name="expression">The complete expression text to parse exactly once.</param>
    /// <returns>
    /// A replayable typed outcome retaining every exact prefix and the final semantic observation when available.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    /// <remarks>
    /// This overload does not consult selected-frame or Portable-PDB facts. Bare type names therefore produce the
    /// binder's typed context-required result; use the context overload when debugger name context is available.
    /// </remarks>
    public static StaticFieldExpressionEvaluationResult Evaluate(
        ClrmdDumpSession session,
        string? expression)
    {
        ArgumentNullException.ThrowIfNull(session);
        return EvaluateCore(session, expression, context: null);
    }

    /// <summary>Evaluates a static-field expression with additive selected-frame and Portable-PDB binding context.</summary>
    /// <param name="session">The open dump session supplying metadata, runtime catalogs, and raw memory.</param>
    /// <param name="expression">The complete expression text to parse exactly once.</param>
    /// <param name="context">Independently acquired context for the same immutable dump snapshot.</param>
    /// <returns>
    /// A replayable typed outcome retaining only context facts actually consulted by binding, every exact runtime
    /// prefix, and the final semantic observation or common object binding when available.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="context"/> belongs to another dump snapshot.</exception>
    public static StaticFieldExpressionEvaluationResult Evaluate(
        ClrmdDumpSession session,
        string? expression,
        DumpExpressionBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Snapshot != session.Snapshot)
        {
            throw new ArgumentException(
                "The expression binding context belongs to another immutable dump snapshot.",
                nameof(context));
        }
        return EvaluateCore(session, expression, context);
    }

    private static StaticFieldExpressionEvaluationResult EvaluateCore(
        ClrmdDumpSession session,
        string? expression,
        DumpExpressionBindingContext? context)
    {
        var syntax = StaticFieldExpressionParser.Parse(expression);
        if (syntax.Status != StaticFieldSyntaxStatus.Accepted)
        {
            return Result(
                syntax,
                symbolBinding: null,
                StaticFieldExpressionEvaluationStage.Syntax,
                syntax.Status == StaticFieldSyntaxStatus.Invalid
                    ? StaticFieldExpressionEvaluationStatus.Invalid
                    : StaticFieldExpressionEvaluationStatus.Unsupported,
                diagnosticCode: syntax.DiagnosticCode,
                diagnosticMessage: syntax.DiagnosticMessage);
        }

        var source = new ClrmdStaticFieldMetadataBindingSource(session);
        var descriptor = syntax.Descriptor!;
        var binding = context is null
            ? StaticFieldFullyQualifiedBinder.Bind(source, descriptor)
            : StaticFieldContextualBinder.Bind(source, descriptor, context);
        if (binding.Status != StaticFieldBindingStatus.Exact)
        {
            return Result(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.SymbolBinding,
                ToEvaluationStatus(binding.Status),
                observation: StaticFieldObservation.FromFailedSymbol(binding),
                diagnosticCode: binding.DiagnosticCode,
                diagnosticMessage: binding.DiagnosticMessage);
        }

        var declaration = binding.SelectedDeclaration!;
        var moduleMatches = session.Modules.Where(candidate => ModuleMatches(candidate, declaration.Module)).ToArray();
        if (moduleMatches.Length != 1)
        {
            var ambiguous = moduleMatches.Length > 1;
            return Result(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.RuntimeDeclaration,
                ambiguous
                    ? StaticFieldExpressionEvaluationStatus.Ambiguous
                    : StaticFieldExpressionEvaluationStatus.Unavailable,
                runtimeIssue: ambiguous ? ClrmdValueIssue.AmbiguousMatch : ClrmdValueIssue.ModuleUnavailable,
                diagnosticCode: ambiguous
                    ? "W7_RUNTIME_MODULE_AMBIGUOUS"
                    : "W7_RUNTIME_MODULE_UNAVAILABLE",
                diagnosticMessage: ambiguous
                    ? "The exact Product module identity matched more than one runtime module."
                    : "The exact Product module identity was absent from the active runtime module catalog.");
        }

        var mapped = session.MapStaticFieldDeclaration(
            moduleMatches[0],
            declaration.TypeDefinitionToken,
            StaticFieldRuntimeComposition.RuntimeFullName(declaration.DeclaringTypeAncestry.SubjectType),
            declaration.FieldDefinitionToken,
            declaration.FieldName,
            ExpectedDecoder(declaration.DeclaredValueKind));
        if (mapped.Status != ClrmdEvidenceStatus.Exact || mapped.Value is null)
        {
            return Result(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.RuntimeDeclaration,
                ToEvaluationStatus(mapped.Status, mapped.Issue),
                runtimeIssue: mapped.Issue,
                runtimeDeclarationEvidence: mapped.Evidence,
                runtimeDeclarationBounds: mapped.AppliedBounds);
        }

        var runtimeMapping = mapped.Value;
        var rawNullableLayout = (ClrmdStaticNullableRuntimeLayoutIdentity?)null;
        var nullableLayout = (StaticFieldNullableInt32RuntimeLayoutIdentity?)null;
        var nullableEvidence = ImmutableArray<MemoryReadResult>.Empty;
        var nullableBounds = ImmutableArray<EvaluationDeterministicBound>.Empty;
        if (declaration.DeclaredValueKind == StaticFieldDeclaredValueKind.NullableInt32)
        {
            var projected = session.MapStaticNullableRuntimeLayout(runtimeMapping);
            nullableEvidence = projected.Evidence;
            nullableBounds = projected.AppliedBounds;
            if (projected.Status != ClrmdEvidenceStatus.Exact || projected.Value is null)
            {
                return Result(
                    syntax,
                    binding,
                    StaticFieldExpressionEvaluationStage.NullableLayout,
                    ToEvaluationStatus(projected.Status, projected.Issue),
                    runtimeIssue: projected.Issue,
                    runtimeDeclaration: runtimeMapping,
                    runtimeDeclarationEvidence: mapped.Evidence,
                    runtimeDeclarationBounds: mapped.AppliedBounds,
                    nullableLayoutEvidence: nullableEvidence,
                    nullableLayoutBounds: nullableBounds);
            }
            rawNullableLayout = projected.Value;
            try
            {
                nullableLayout = StaticFieldRuntimeComposer.ComposeNullableInt32Layout(
                    source,
                    declaration,
                    rawNullableLayout);
            }
            catch (Exception exception) when (IsTypedCompositionStop(exception))
            {
                return CompositionFailure(
                    syntax,
                    binding,
                    StaticFieldExpressionEvaluationStage.NullableLayout,
                    exception,
                    runtimeMapping,
                    mapped.Evidence,
                    mapped.AppliedBounds,
                    rawNullableLayout,
                    nullableEvidence,
                    nullableBounds,
                    hostObservation: null);
            }
        }

        ClrmdStaticFieldEvaluationRequest request;
        try
        {
            request = StaticFieldObservation.CreatePhysicalRequest(binding, runtimeMapping, nullableLayout);
        }
        catch (Exception exception) when (IsTypedCompositionStop(exception))
        {
            return CompositionFailure(
                syntax,
                binding,
                declaration.DeclaredValueKind == StaticFieldDeclaredValueKind.NullableInt32
                    ? StaticFieldExpressionEvaluationStage.NullableLayout
                    : StaticFieldExpressionEvaluationStage.RuntimeDeclaration,
                exception,
                runtimeMapping,
                mapped.Evidence,
                mapped.AppliedBounds,
                rawNullableLayout,
                nullableEvidence,
                nullableBounds,
                hostObservation: null);
        }

        var hostObservation = session.ReadStaticField(request);
        StaticFieldRuntimeAssignabilityProof? assignability = null;
        var nonNullObject = GetMatchedNonNullObject(hostObservation);
        if (nonNullObject is not null)
        {
            try
            {
                assignability = StaticFieldRuntimeComposer.ProveReferenceAssignability(
                    source,
                    declaration,
                    nonNullObject);
            }
            catch (Exception exception) when (IsTypedCompositionStop(exception))
            {
                return CompositionFailure(
                    syntax,
                    binding,
                    StaticFieldExpressionEvaluationStage.Assignability,
                    exception,
                    runtimeMapping,
                    mapped.Evidence,
                    mapped.AppliedBounds,
                    rawNullableLayout,
                    nullableEvidence,
                    nullableBounds,
                    hostObservation);
            }
        }

        StaticFieldObservation observation;
        try
        {
            observation = StaticFieldObservation.FromExactSymbol(
                binding,
                hostObservation,
                nullableLayout,
                assignability);
        }
        catch (Exception exception) when (IsTypedCompositionStop(exception))
        {
            return CompositionFailure(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.Assignability,
                exception,
                runtimeMapping,
                mapped.Evidence,
                mapped.AppliedBounds,
                rawNullableLayout,
                nullableEvidence,
                nullableBounds,
                hostObservation);
        }

        DumpObjectBinding? objectBinding = null;
        if (hostObservation.Status == ClrmdStaticFieldObservationStatus.Exact &&
            hostObservation.Value?.Kind == ClrmdStaticFieldTerminalKind.ObjectReference)
        {
            var objectReference = hostObservation.Value.ObjectReference!;
            var identity = DumpObjectIdentity.FromExactObject(objectReference);
            var staticSource = DumpStaticFieldExpressionSourceIdentity.Create(observation);
            objectBinding = DumpObjectBinding.Create(
                identity,
                DumpObjectProvenance.FromStaticFieldExpression(staticSource));
        }

        var selectedShape = binding.SelectedShape!;
        if (hostObservation.Status == ClrmdStaticFieldObservationStatus.Exact &&
            selectedShape.SuffixShape != StaticFieldSuffixShape.None)
        {
            return EvaluateSuffix(
                session,
                syntax,
                binding,
                runtimeMapping,
                mapped.Evidence,
                mapped.AppliedBounds,
                rawNullableLayout,
                nullableEvidence,
                nullableBounds,
                nullableLayout,
                hostObservation,
                observation,
                objectBinding,
                selectedShape);
        }

        var exact = hostObservation.Status == ClrmdStaticFieldObservationStatus.Exact;
        return Result(
            syntax,
            binding,
            exact
                ? StaticFieldExpressionEvaluationStage.Complete
                : StaticFieldExpressionEvaluationStage.Storage,
            exact
                ? StaticFieldExpressionEvaluationStatus.Exact
                : ToEvaluationStatus(hostObservation.Status, hostObservation.Issue),
            runtimeIssue: exact ? null : hostObservation.Issue,
            runtimeDeclaration: runtimeMapping,
            runtimeDeclarationEvidence: mapped.Evidence,
            runtimeDeclarationBounds: mapped.AppliedBounds,
            rawNullableLayout,
            nullableLayoutEvidence: nullableEvidence,
            nullableLayoutBounds: nullableBounds,
            nullableLayout,
            hostObservation,
            observation,
            objectBinding);
    }

    private static StaticFieldExpressionEvaluationResult EvaluateSuffix(
        ClrmdDumpSession session,
        StaticFieldSyntaxOutcome syntax,
        StaticFieldSymbolBindingOutcome binding,
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        ImmutableArray<MemoryReadResult> mappingEvidence,
        ImmutableArray<EvaluationDeterministicBound> mappingBounds,
        ClrmdStaticNullableRuntimeLayoutIdentity? rawNullableLayout,
        ImmutableArray<MemoryReadResult> nullableEvidence,
        ImmutableArray<EvaluationDeterministicBound> nullableBounds,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableLayout,
        ClrmdStaticFieldValueObservation hostObservation,
        StaticFieldObservation observation,
        DumpObjectBinding? objectBinding,
        StaticFieldCandidateShape selectedShape)
    {
        var suffixKind = selectedShape.SuffixShape switch
        {
            StaticFieldSuffixShape.DirectMember => StaticFieldExpressionSuffixKind.DirectMember,
            StaticFieldSuffixShape.FixedDepthMemberChain =>
                StaticFieldExpressionSuffixKind.FixedDepthMemberChain,
            _ => throw new ArgumentOutOfRangeException(nameof(selectedShape)),
        };
        if (objectBinding is null)
        {
            var isNull = hostObservation.Value?.Kind == ClrmdStaticFieldTerminalKind.Null;
            var result = CreateSuffixFailure(
                session,
                binding,
                isNull ? EvaluationEvidenceStatus.Exact : EvaluationEvidenceStatus.Unavailable,
                isNull ? ClrmdValueIssue.ObjectUnavailable : ClrmdValueIssue.MemberShapeUnsupported,
                isNull ? "STATIC_SUFFIX_NULL_RECEIVER" : "STATIC_SUFFIX_RECEIVER_UNSUPPORTED",
                isNull
                    ? "Direct instance access cannot continue through an exact null static receiver."
                    : "The exact static terminal is not an admitted common object receiver.",
                ImmutableArray<MemoryReadResult>.Empty,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
            return Result(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.SuffixPreparation,
                isNull
                    ? StaticFieldExpressionEvaluationStatus.Unavailable
                    : StaticFieldExpressionEvaluationStatus.Unsupported,
                runtimeIssue: isNull ? ClrmdValueIssue.ObjectUnavailable : ClrmdValueIssue.MemberShapeUnsupported,
                runtimeDeclaration: runtimeMapping,
                runtimeDeclarationEvidence: mappingEvidence,
                runtimeDeclarationBounds: mappingBounds,
                rawNullableLayout,
                nullableLayoutEvidence: nullableEvidence,
                nullableLayoutBounds: nullableBounds,
                nullableLayout,
                hostObservation,
                observation,
                objectBinding: null,
                suffixKind,
                suffixResult: result,
                diagnosticCode: result.Diagnostics[0].Code,
                diagnosticMessage: result.Diagnostics[0].Message);
        }

        var projected = session.ProjectExactObjectForInstanceEvaluation(
            objectBinding.Provenance.StaticField!.Observation.HostObservation!.Value!.ObjectReference!);
        if (projected.Status != ClrmdEvidenceStatus.Exact || projected.Value is null)
        {
            var (code, message) = SuffixProjectionDiagnostic(projected.Status, projected.Issue);
            var result = CreateSuffixFailure(
                session,
                binding,
                ToEvaluationEvidenceStatus(projected.Status),
                projected.Issue,
                code,
                message,
                projected.Evidence,
                projected.AppliedBounds);
            return Result(
                syntax,
                binding,
                StaticFieldExpressionEvaluationStage.SuffixPreparation,
                ToEvaluationStatus(projected.Status, projected.Issue),
                runtimeIssue: projected.Issue,
                runtimeDeclaration: runtimeMapping,
                runtimeDeclarationEvidence: mappingEvidence,
                runtimeDeclarationBounds: mappingBounds,
                rawNullableLayout,
                nullableLayoutEvidence: nullableEvidence,
                nullableLayoutBounds: nullableBounds,
                nullableLayout,
                hostObservation,
                observation,
                objectBinding,
                suffixKind,
                suffixResult: result,
                diagnosticCode: code,
                diagnosticMessage: message);
        }

        var rootBinding = DumpQueryRootBinding.FromObjectBinding(
            "staticRoot",
            projected.Value,
            objectBinding);
        DumpQueryPlan? directPlan = null;
        DumpMemberChainPlan? memberChainPlan = null;
        EvaluationResult<DumpQueryValue> suffixResult;
        var preparationSucceeded = false;
        if (suffixKind == StaticFieldExpressionSuffixKind.DirectMember)
        {
            var preparation = DumpQueryEngine.PrepareStaticSuffix(
                session,
                binding.Descriptor,
                selectedShape,
                rootBinding);
            directPlan = preparation.Plan;
            preparationSucceeded = preparation.IsSuccess;
            suffixResult = preparation.IsSuccess
                ? DumpQueryEngine.Evaluate(session, preparation.Plan!)
                : preparation.Failure!;
        }
        else
        {
            (memberChainPlan, suffixResult, preparationSucceeded) = EvaluateMemberChainSuffix(
                session,
                binding,
                selectedShape,
                rootBinding);
        }

        var suffixExact = IsExactComplete(suffixResult);
        return Result(
            syntax,
            binding,
            suffixExact
                ? StaticFieldExpressionEvaluationStage.Complete
                : preparationSucceeded
                    ? StaticFieldExpressionEvaluationStage.SuffixEvaluation
                    : StaticFieldExpressionEvaluationStage.SuffixPreparation,
            suffixExact
                ? StaticFieldExpressionEvaluationStatus.Exact
                : ToEvaluationStatus(suffixResult),
            runtimeIssue: null,
            runtimeDeclaration: runtimeMapping,
            runtimeDeclarationEvidence: mappingEvidence,
            runtimeDeclarationBounds: mappingBounds,
            rawNullableLayout,
            nullableLayoutEvidence: nullableEvidence,
            nullableLayoutBounds: nullableBounds,
            nullableLayout,
            hostObservation,
            observation,
            objectBinding,
            suffixKind,
            directPlan,
            memberChainPlan,
            suffixResult,
            diagnosticCode: suffixExact || suffixResult.Diagnostics.IsEmpty
                ? null
                : suffixResult.Diagnostics[0].Code,
            diagnosticMessage: suffixExact || suffixResult.Diagnostics.IsEmpty
                ? null
                : suffixResult.Diagnostics[0].Message);
    }

    private static (
        DumpMemberChainPlan? Plan,
        EvaluationResult<DumpQueryValue> Result,
        bool PreparationSucceeded) EvaluateMemberChainSuffix(
        ClrmdDumpSession session,
        StaticFieldSymbolBindingOutcome binding,
        StaticFieldCandidateShape shape,
        DumpQueryRootBinding rootBinding)
    {
        var descriptor = binding.Descriptor;
        var referenceName = descriptor.Segments[shape.StaticFieldSegmentIndex + 1].DecodedIdentifier;
        var terminalSegment = descriptor.Segments[shape.StaticFieldSegmentIndex + 2];
        var terminalName = terminalSegment.DecodedIdentifier;
        var accessKind = terminalSegment.AccessKind == StaticFieldSegmentAccessKind.ConditionalMember
            ? DumpMemberChainAccessKind.Conditional
            : DumpMemberChainAccessKind.Direct;
        var fallbackKind = shape.FallbackKind switch
        {
            StaticFieldFallbackKind.None => DumpMemberChainFallbackKind.None,
            StaticFieldFallbackKind.Null => DumpMemberChainFallbackKind.Null,
            StaticFieldFallbackKind.Int32 => DumpMemberChainFallbackKind.Int32,
            StaticFieldFallbackKind.String => DumpMemberChainFallbackKind.String,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        var writer = new CanonicalReplayEncoding.Writer("static-field-member-chain-request", 1);
        writer.WriteLengthPrefixedBytes(binding.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(rootBinding.ObjectBinding!.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(shape.CanonicalBytes.AsSpan());
        var requestBytes = writer.ToImmutableArray();
        var requestSha256 = CanonicalReplayEncoding.ComputeSha256(requestBytes.AsSpan());
        var source = new ClrmdDumpMemberChainEvidenceSource(session);
        var certificateResult = source.CertifyDeclaredDataMember(
            rootBinding.Root!,
            referenceName,
            terminalName);
        if (certificateResult.Status != ClrmdEvidenceStatus.Exact || certificateResult.Value is null)
        {
            var failure = CreateMemberChainPreparationFailure(certificateResult);
            return (
                null,
                DumpMemberChainEngine.FromPreparationFailure(
                    source,
                    rootBinding,
                    descriptor.ReachedBounds,
                    requestSha256,
                    failure),
                false);
        }

        var preparation = DumpMemberChainPlanBinder.Bind(
            rootBinding,
            certificateResult.Value,
            accessKind,
            fallbackKind,
            shape.Int32Fallback,
            shape.StringFallback,
            requestBytes,
            requestSha256,
            descriptor.CanonicalBytes,
            descriptor.Sha256,
            descriptor.ReachedBounds,
            certificateResult.Evidence,
            certificateResult.AppliedBounds);
        if (!preparation.IsSuccess)
        {
            return (
                null,
                DumpMemberChainEngine.FromPreparationFailure(
                    source,
                    rootBinding,
                    descriptor.ReachedBounds,
                    requestSha256,
                    preparation.Failure!),
                false);
        }
        return (
            preparation.Plan,
            DumpMemberChainEngine.Evaluate(source, preparation.Plan!),
            true);
    }

    private static DumpMemberChainPreparationFailure CreateMemberChainPreparationFailure(
        ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> result)
    {
        var (code, message) = result.Status switch
        {
            ClrmdEvidenceStatus.Partial => (
                "STATIC_SUFFIX_BIND_PARTIAL",
                "Static suffix declaration binding stopped after incomplete evidence."),
            ClrmdEvidenceStatus.Unavailable => (
                "STATIC_SUFFIX_BIND_UNAVAILABLE",
                "The required static suffix declaration evidence is unavailable."),
            ClrmdEvidenceStatus.Conflict => (
                "STATIC_SUFFIX_BIND_CONFLICT",
                "Static suffix declaration evidence disagrees with the selected member path."),
            _ => (
                "STATIC_SUFFIX_BIND_INVALID",
                "Static suffix declaration evidence violates an admitted structural invariant."),
        };
        return new DumpMemberChainPreparationFailure(
            code,
            message,
            result.Status == ClrmdEvidenceStatus.Exact ? ClrmdEvidenceStatus.Invalid : result.Status,
            result.Issue == ClrmdValueIssue.None ? ClrmdValueIssue.InvalidData : result.Issue,
            result.Evidence,
            result.AppliedBounds);
    }

    private static EvaluationResult<DumpQueryValue> CreateSuffixFailure(
        ClrmdDumpSession session,
        StaticFieldSymbolBindingOutcome binding,
        EvaluationEvidenceStatus evidenceStatus,
        ClrmdValueIssue issue,
        string code,
        string message,
        ImmutableArray<MemoryReadResult> evidence,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        var normalizedBounds = binding.Descriptor.ReachedBounds
            .Concat(bounds.IsDefault ? ImmutableArray<EvaluationDeterministicBound>.Empty : bounds)
            .GroupBy(static bound => bound.Name, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToImmutableArray();
        var context = EvaluationEvidenceContext.Create(
            EvaluationEvidenceSourceKind.DumpSnapshot,
            EvaluationEvidenceIdentity.CreateAvailable(session.Snapshot.MemorySourceId),
            EvaluationEvidenceIdentity.Unavailable,
            EvaluationFallback.None,
            normalizedBounds);
        var provenance = ImmutableArray.CreateBuilder<EvaluationProvenance>();
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.Policy,
            $"static-field-binding:sha256:{binding.Sha256}"));
        foreach (var read in evidence.IsDefault ? ImmutableArray<MemoryReadResult>.Empty : evidence)
        {
            provenance.Add(new EvaluationProvenance(
                EvaluationProvenanceKind.DumpMemory,
                read.SourceId,
                read.Address,
                read.RequestedLength,
                read.BytesRead));
        }
        provenance.Add(new EvaluationProvenance(
            EvaluationProvenanceKind.RuntimeStructure,
            $"static-suffix-stop:{(int)issue}"));
        return EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            evidenceStatus == EvaluationEvidenceStatus.Invalid
                ? EvaluationCompletionStatus.Invalid
                : EvaluationCompletionStatus.Blocked,
            EvaluationCompleteness.None,
            evidenceStatus,
            EvaluationEffectStatus.None,
            value: null,
            context,
            provenance.ToImmutable(),
            ImmutableArray.Create(new EvaluationDiagnostic(code, message)));
    }

    private static StaticFieldExpressionEvaluationResult CompositionFailure(
        StaticFieldSyntaxOutcome syntax,
        StaticFieldSymbolBindingOutcome binding,
        StaticFieldExpressionEvaluationStage stage,
        Exception exception,
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping,
        ImmutableArray<MemoryReadResult> mappingEvidence,
        ImmutableArray<EvaluationDeterministicBound> mappingBounds,
        ClrmdStaticNullableRuntimeLayoutIdentity? rawNullableLayout,
        ImmutableArray<MemoryReadResult> nullableEvidence,
        ImmutableArray<EvaluationDeterministicBound> nullableBounds,
        ClrmdStaticFieldValueObservation? hostObservation)
    {
        var (status, issue, code, message) = exception switch
        {
            ArgumentOutOfRangeException => (
                StaticFieldExpressionEvaluationStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                "W7_RUNTIME_COMPOSITION_BOUND",
                "A declared deterministic bound prevented exact runtime semantic composition."),
            BadImageFormatException => (
                StaticFieldExpressionEvaluationStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                "W7_RUNTIME_COMPOSITION_METADATA_INVALID",
                "Counted metadata used for runtime semantic composition was structurally invalid."),
            InvalidOperationException => (
                StaticFieldExpressionEvaluationStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                "W7_RUNTIME_COMPOSITION_INVALID",
                "Exact runtime semantic composition reached an invalid structural state."),
            _ => (
                StaticFieldExpressionEvaluationStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                "W7_RUNTIME_COMPOSITION_CONFLICT",
                "Product metadata semantics and detached runtime evidence did not compose exactly."),
        };
        return Result(
            syntax,
            binding,
            stage,
            status,
            issue,
            runtimeMapping,
            mappingEvidence,
            mappingBounds,
            rawNullableLayout,
            nullableEvidence,
            nullableBounds,
            nullableLayout: null,
            hostObservation,
            diagnosticCode: code,
            diagnosticMessage: message);
    }

    private static StaticFieldExpressionEvaluationResult Result(
        StaticFieldSyntaxOutcome syntax,
        StaticFieldSymbolBindingOutcome? symbolBinding,
        StaticFieldExpressionEvaluationStage stage,
        StaticFieldExpressionEvaluationStatus status,
        ClrmdValueIssue? runtimeIssue = null,
        ClrmdStaticRuntimeDeclarationMappingIdentity? runtimeDeclaration = null,
        ImmutableArray<MemoryReadResult> runtimeDeclarationEvidence = default,
        ImmutableArray<EvaluationDeterministicBound> runtimeDeclarationBounds = default,
        ClrmdStaticNullableRuntimeLayoutIdentity? rawNullableLayout = null,
        ImmutableArray<MemoryReadResult> nullableLayoutEvidence = default,
        ImmutableArray<EvaluationDeterministicBound> nullableLayoutBounds = default,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableLayout = null,
        ClrmdStaticFieldValueObservation? hostObservation = null,
        StaticFieldObservation? observation = null,
        DumpObjectBinding? objectBinding = null,
        StaticFieldExpressionSuffixKind suffixKind = StaticFieldExpressionSuffixKind.None,
        DumpQueryPlan? directSuffixPlan = null,
        DumpMemberChainPlan? memberChainSuffixPlan = null,
        EvaluationResult<DumpQueryValue>? suffixResult = null,
        string? diagnosticCode = null,
        string? diagnosticMessage = null) =>
        new(
            syntax,
            symbolBinding,
            stage,
            status,
            runtimeIssue,
            runtimeDeclaration,
            runtimeDeclarationEvidence.IsDefault
                ? ImmutableArray<MemoryReadResult>.Empty
                : runtimeDeclarationEvidence,
            runtimeDeclarationBounds.IsDefault
                ? ImmutableArray<EvaluationDeterministicBound>.Empty
                : runtimeDeclarationBounds,
            rawNullableLayout,
            nullableLayoutEvidence.IsDefault
                ? ImmutableArray<MemoryReadResult>.Empty
                : nullableLayoutEvidence,
            nullableLayoutBounds.IsDefault
                ? ImmutableArray<EvaluationDeterministicBound>.Empty
                : nullableLayoutBounds,
            nullableLayout,
            hostObservation,
            observation,
            objectBinding,
            suffixKind,
            directSuffixPlan,
            memberChainSuffixPlan,
            suffixResult,
            diagnosticCode,
            diagnosticMessage);

    private static bool ModuleMatches(ClrmdModuleInfo candidate, StaticFieldModuleInstanceIdentity expected)
    {
        var identity = candidate.Identity;
        return string.Equals(identity.Snapshot.Sha256, expected.SnapshotSha256, StringComparison.Ordinal) &&
            identity.AppDomainAddress == expected.ApplicationDomainAddress &&
            identity.ModuleAddress == expected.ModuleAddress &&
            identity.ImageBase == expected.ImageBase &&
            identity.ImageSize == expected.ImageSize;
    }

    private static ClrmdStaticExpectedDecoderKind ExpectedDecoder(StaticFieldDeclaredValueKind valueKind) =>
        valueKind switch
        {
            StaticFieldDeclaredValueKind.Int32 => ClrmdStaticExpectedDecoderKind.Int32,
            StaticFieldDeclaredValueKind.NullableInt32 => ClrmdStaticExpectedDecoderKind.NullableInt32,
            StaticFieldDeclaredValueKind.String => ClrmdStaticExpectedDecoderKind.String,
            StaticFieldDeclaredValueKind.ManagedReference or StaticFieldDeclaredValueKind.Object =>
                ClrmdStaticExpectedDecoderKind.ManagedReference,
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind)),
        };

    private static ClrmdExactObjectReference? GetMatchedNonNullObject(
        ClrmdStaticFieldValueObservation observation)
    {
        if (observation.TargetEvidence is { Kind: ClrmdStaticTargetEvidenceKind.Matched } target)
        {
            return ClrmdExactObjectReference.Create(target);
        }
        return observation.Value?.Kind switch
        {
            ClrmdStaticFieldTerminalKind.String => observation.Value.StringValue!.ObjectReference,
            ClrmdStaticFieldTerminalKind.ObjectReference => observation.Value.ObjectReference,
            _ => null,
        };
    }

    private static StaticFieldExpressionEvaluationStatus ToEvaluationStatus(StaticFieldBindingStatus status) =>
        status switch
        {
            StaticFieldBindingStatus.Absent => StaticFieldExpressionEvaluationStatus.Absent,
            StaticFieldBindingStatus.Partial => StaticFieldExpressionEvaluationStatus.Partial,
            StaticFieldBindingStatus.Unavailable => StaticFieldExpressionEvaluationStatus.Unavailable,
            StaticFieldBindingStatus.Ambiguous => StaticFieldExpressionEvaluationStatus.Ambiguous,
            StaticFieldBindingStatus.Conflict => StaticFieldExpressionEvaluationStatus.Conflict,
            StaticFieldBindingStatus.Invalid => StaticFieldExpressionEvaluationStatus.Invalid,
            StaticFieldBindingStatus.Unsupported => StaticFieldExpressionEvaluationStatus.Unsupported,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static StaticFieldExpressionEvaluationStatus ToEvaluationStatus(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue) =>
        status switch
        {
            ClrmdEvidenceStatus.Partial => StaticFieldExpressionEvaluationStatus.Partial,
            ClrmdEvidenceStatus.Unavailable => StaticFieldExpressionEvaluationStatus.Unavailable,
            ClrmdEvidenceStatus.Conflict when issue == ClrmdValueIssue.AmbiguousMatch =>
                StaticFieldExpressionEvaluationStatus.Ambiguous,
            ClrmdEvidenceStatus.Conflict => StaticFieldExpressionEvaluationStatus.Conflict,
            ClrmdEvidenceStatus.Invalid => StaticFieldExpressionEvaluationStatus.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static StaticFieldExpressionEvaluationStatus ToEvaluationStatus(
        ClrmdStaticFieldObservationStatus status,
        ClrmdValueIssue issue) =>
        status switch
        {
            ClrmdStaticFieldObservationStatus.Partial => StaticFieldExpressionEvaluationStatus.Partial,
            ClrmdStaticFieldObservationStatus.Unavailable => StaticFieldExpressionEvaluationStatus.Unavailable,
            ClrmdStaticFieldObservationStatus.Conflict when issue == ClrmdValueIssue.AmbiguousMatch =>
                StaticFieldExpressionEvaluationStatus.Ambiguous,
            ClrmdStaticFieldObservationStatus.Conflict => StaticFieldExpressionEvaluationStatus.Conflict,
            ClrmdStaticFieldObservationStatus.Invalid => StaticFieldExpressionEvaluationStatus.Invalid,
            ClrmdStaticFieldObservationStatus.Unsupported => StaticFieldExpressionEvaluationStatus.Unsupported,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static StaticFieldExpressionEvaluationStatus ToEvaluationStatus(
        EvaluationResult<DumpQueryValue> result)
    {
        if (result.Completion == EvaluationCompletionStatus.Invalid ||
            result.Evidence == EvaluationEvidenceStatus.Invalid)
        {
            return StaticFieldExpressionEvaluationStatus.Invalid;
        }
        return result.Evidence switch
        {
            EvaluationEvidenceStatus.Partial => StaticFieldExpressionEvaluationStatus.Partial,
            EvaluationEvidenceStatus.Unavailable => StaticFieldExpressionEvaluationStatus.Unavailable,
            EvaluationEvidenceStatus.Conflict => StaticFieldExpressionEvaluationStatus.Conflict,
            EvaluationEvidenceStatus.Exact when result.Completion != EvaluationCompletionStatus.Completed ||
                                                result.Completeness != EvaluationCompleteness.Complete =>
                StaticFieldExpressionEvaluationStatus.Unavailable,
            _ => StaticFieldExpressionEvaluationStatus.Invalid,
        };
    }

    private static EvaluationEvidenceStatus ToEvaluationEvidenceStatus(ClrmdEvidenceStatus status) =>
        status switch
        {
            ClrmdEvidenceStatus.Exact => EvaluationEvidenceStatus.Exact,
            ClrmdEvidenceStatus.Partial => EvaluationEvidenceStatus.Partial,
            ClrmdEvidenceStatus.Unavailable => EvaluationEvidenceStatus.Unavailable,
            ClrmdEvidenceStatus.Conflict => EvaluationEvidenceStatus.Conflict,
            ClrmdEvidenceStatus.Invalid => EvaluationEvidenceStatus.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static bool IsExactComplete(EvaluationResult<DumpQueryValue> result) =>
        result.Completion == EvaluationCompletionStatus.Completed &&
        result.Completeness == EvaluationCompleteness.Complete &&
        result.Evidence == EvaluationEvidenceStatus.Exact &&
        result.Value is not null;

    private static (string Code, string Message) SuffixProjectionDiagnostic(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue) => status switch
    {
        ClrmdEvidenceStatus.Partial => (
            "STATIC_SUFFIX_ROOT_PARTIAL",
            "The exact static object could not be projected after a deterministic runtime bound."),
        ClrmdEvidenceStatus.Unavailable when issue == ClrmdValueIssue.MemberShapeUnsupported => (
            "STATIC_SUFFIX_ROOT_UNSUPPORTED",
            "The exact static object runtime shape is outside the existing instance-member engine."),
        ClrmdEvidenceStatus.Unavailable => (
            "STATIC_SUFFIX_ROOT_UNAVAILABLE",
            "Required runtime facts for the exact static object are unavailable."),
        ClrmdEvidenceStatus.Conflict => (
            "STATIC_SUFFIX_ROOT_CONFLICT",
            "The exact static object and direct runtime projection disagree."),
        _ => (
            "STATIC_SUFFIX_ROOT_INVALID",
            "The exact static object projection violates an admitted structural invariant."),
    };

    private static bool IsTypedCompositionStop(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or BadImageFormatException;
}
