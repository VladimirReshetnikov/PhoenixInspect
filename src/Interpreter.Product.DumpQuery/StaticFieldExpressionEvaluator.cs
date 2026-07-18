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

    private static bool IsTypedCompositionStop(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or BadImageFormatException;
}
