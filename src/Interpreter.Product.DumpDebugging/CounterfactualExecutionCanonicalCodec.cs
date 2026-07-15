using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;

namespace Interpreter.Product.DumpDebugging;

/// <summary>Serializes validated counterfactual execution results into the closed canonical W4 schema.</summary>
/// <remarks>
/// Schema version one is domain-separated, uses big-endian fixed-width numbers, length-prefixed variable bytes,
/// raw 32-byte digests, presence tags for nullable facts, and explicit closed tags for every enum. Runtime object
/// identity, dictionary order, display-only method names, capabilities, memory, and cancellation tokens are absent.
/// This codec is a draft W4 prototype contract; schema evolution must use a new version rather than silently changing
/// version-one bytes.
/// </remarks>
public static class CounterfactualExecutionCanonicalCodec
{
    private const string ResultDomain = "Interpreter.CounterfactualExecution.Result";

    /// <summary>Serializes one validated immutable result using canonical schema version one.</summary>
    /// <param name="result">The product-issued result to serialize.</param>
    /// <returns>A fresh immutable canonical byte vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<byte> SerializeCanonical(CounterfactualExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Encode(result);
    }

    /// <summary>Computes the lowercase SHA-256 fingerprint of one result's canonical bytes.</summary>
    /// <param name="result">The product-issued result to fingerprint.</param>
    /// <returns>A 64-character lowercase hexadecimal digest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static string ComputeSha256(CounterfactualExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CounterfactualCanonical.Hash(Encode(result).AsSpan());
    }

    internal static ImmutableArray<byte> Encode(CounterfactualExecutionResult result)
    {
        if (result.SchemaVersion != CounterfactualExecutionResult.CanonicalSchemaVersion ||
            result.SemanticMode != EvaluationSemanticMode.CounterfactualExecution)
        {
            throw new ArgumentException("The counterfactual result schema or semantic mode is unsupported.", nameof(result));
        }

        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString(ResultDomain);
        writer.WriteInt32(result.SchemaVersion);
        var context = result.Context;
        writer.WriteInt32(Tag(context.Origin));
        writer.WriteInt32(Tag(result.SemanticMode));
        writer.WriteInt32(Tag(result.Completion));
        writer.WriteInt32(Tag(result.Completeness));
        writer.WriteInt32(Tag(result.Evidence));
        writer.WriteInt32(Tag(result.Effects));
        writer.WriteBoolean(result.IsDeterministicReplay);

        WriteRequest(writer, context.Request);
        WritePlanIdentity(writer, context.PlanSchemaVersion, context.PlanSha256);
        WriteEvidenceContext(writer, context.EvidenceContext);
        WriteAccounting(writer, context.Accounting);
        WritePlanProjections(writer, context);
        WriteExecutionTranscript(writer, context);
        WriteValue(writer, result.Value);
        WriteProvenance(writer, result.Provenance);
        WriteDiagnostics(writer, result.Diagnostics);
        return writer.ToImmutableArray();
    }

    private static void WriteRequest(CounterfactualCanonicalWriter writer, CounterfactualMethodRequest? request)
    {
        writer.WriteBoolean(request is not null);
        if (request is not null)
        {
            writer.WriteInt32(request.SchemaVersion);
            writer.WriteDigest(request.Sha256);
        }
    }

    private static void WritePlanIdentity(
        CounterfactualCanonicalWriter writer,
        int? schemaVersion,
        string? sha256)
    {
        writer.WriteBoolean(schemaVersion.HasValue);
        if (schemaVersion is { } schema)
        {
            writer.WriteInt32(schema);
            writer.WriteDigest(sha256!);
        }
    }

    private static void WriteEvidenceContext(
        CounterfactualCanonicalWriter writer,
        EvaluationEvidenceContext context)
    {
        writer.WriteInt32(Tag(context.SourceKind));
        writer.WriteEvidenceIdentity(context.Snapshot);
        writer.WriteEvidenceIdentity(context.Module);
        writer.WriteInt32(Tag(context.Fallback.Status));
        writer.WriteString(context.Fallback.Name);
        var bounds = context.Bounds;
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    private static void WriteAccounting(
        CounterfactualCanonicalWriter writer,
        CounterfactualExecutionAccounting accounting)
    {
        writer.WriteInt32(Tag(accounting.InstructionStatus));
        WriteNullableInt64(writer, accounting.InstructionLimit);
        WriteNullableInt64(writer, accounting.InstructionUsed);
        WriteNullableInt64(writer, accounting.InstructionRemaining);

        writer.WriteInt32(Tag(accounting.TraversalStatus));
        WriteNullableInt32(writer, accounting.TraversalLimit);
        WriteNullableInt32(writer, accounting.TraversalUsed);
        WriteNullableInt32(writer, accounting.TraversalRemaining);
        var charges = accounting.TraversalCharges;
        writer.WriteInt32(charges.Length);
        foreach (var charge in charges)
        {
            WriteTraversalCharge(writer, charge);
        }

        writer.WriteBoolean(accounting.RejectedTraversalCharge is not null);
        if (accounting.RejectedTraversalCharge is { } rejected)
        {
            WriteTraversalCharge(writer, rejected);
        }

        writer.WriteInt32(Tag(accounting.DepthStatus));
        WriteNullableInt32(writer, accounting.LogicalDepthLimit);
        WriteNullableInt32(writer, accounting.RequiredLogicalDepth);
        WriteNullableInt32(writer, accounting.ObservedLogicalDepthHighWater);
        WriteNullableInt32(writer, accounting.ActiveFrameDepthHighWater);

        writer.WriteInt32(Tag(accounting.LineageStatus));
        WriteNullableInt64(writer, accounting.LineageNodeCeiling);
        WriteNullableInt32(writer, accounting.LineageNodeCount);
        writer.WriteInt32(Tag(accounting.AllocationStatus));
    }

    private static void WritePlanProjections(
        CounterfactualCanonicalWriter writer,
        CounterfactualExecutionContext context)
    {
        writer.WriteBoolean(context.RootMethod.HasValue);
        if (context.RootMethod is { } root)
        {
            writer.WriteMethod(root);
        }

        WriteMethods(writer, context.InterpretedMethods);
        WriteMethods(writer, context.ModeledMethods);

        var fields = context.PlannedFields;
        writer.WriteInt32(fields.Length);
        foreach (var field in fields)
        {
            WriteField(writer, field);
        }

        var calls = context.CallDispositions;
        writer.WriteInt32(calls.Length);
        foreach (var call in calls)
        {
            writer.WriteMethod(call.Caller);
            writer.WriteInt32(call.IlOffset);
            writer.WriteInt32(call.MetadataToken);
            writer.WriteMethod(call.TargetMethod);
            writer.WriteInt32(Tag(call.Disposition));
            writer.WriteInt32(Tag(call.Effects));
            writer.WriteBoolean(call.ModelId is not null);
            if (call.ModelId is { } modelId)
            {
                writer.WriteString(modelId);
                writer.WriteVersion(call.ModelVersion!.Value);
                writer.WriteInt32(Tag(call.ModelConfidence!.Value));
            }
        }
    }

    private static void WriteExecutionTranscript(
        CounterfactualCanonicalWriter writer,
        CounterfactualExecutionContext context)
    {
        var observations = context.ReachedFieldObservations;
        writer.WriteInt32(observations.Length);
        foreach (var observation in observations)
        {
            writer.WriteBytes(observation.CanonicalBytes.AsSpan());
        }

        var ordinals = context.ReachedFieldLoadOrdinals;
        writer.WriteInt32(ordinals.Length);
        foreach (var ordinal in ordinals)
        {
            writer.WriteInt32(ordinal);
        }

        var attempts = context.ModelAttempts;
        writer.WriteInt32(attempts.Length);
        foreach (var attempt in attempts)
        {
            writer.WriteMethod(attempt.CallSite.Caller);
            writer.WriteInt32(attempt.CallSite.CallIlOffset);
            writer.WriteMethod(attempt.CallSite.Callee);
            writer.WriteString(attempt.ModelIdentity.StableId);
            writer.WriteVersion(attempt.ModelIdentity.Version);
            writer.WriteInt32(attempt.EnteredLogicalDepth);
            writer.WriteInt32(Tag(attempt.OutcomeKind));
            writer.WriteBoolean(attempt.TransferCompleted);
            writer.WriteBoolean(attempt.StableCode is not null);
            if (attempt.StableCode is { } stableCode)
            {
                writer.WriteString(stableCode);
            }
        }

        writer.WriteInt32(context.ModelInvocationCount);
        writer.WriteInt32(context.CompletedModeledCallCount);
        WriteMethods(writer, context.CallTrace);

        var events = context.Events;
        writer.WriteInt32(events.Length);
        foreach (var item in events)
        {
            writer.WriteInt32(Tag(item.Kind));
            writer.WriteMethod(item.Method);
            writer.WriteInt32(item.IlOffset);
            writer.WriteString(item.Instruction);
            writer.WriteBoolean(item.FieldEvidence is not null);
            if (item.FieldEvidence is { } evidence)
            {
                writer.WriteBytes(evidence.CanonicalBytes.AsSpan());
            }
        }
    }

    private static void WriteValue(CounterfactualCanonicalWriter writer, CounterfactualExecutionValue? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is null)
        {
            return;
        }

        writer.WriteInt32(Tag(value.Kind));
        switch (value.Kind)
        {
            case CounterfactualExecutionValueKind.ExactReturn:
                writer.WriteType(value.StaticType!);
                writer.WriteInt32(value.ExactInt32!.Value);
                break;
            case CounterfactualExecutionValueKind.UnknownReturn:
                writer.WriteType(value.StaticType!);
                writer.WriteDigest(value.Lineage!.Root.Sha256);
                writer.WriteDigest(value.Lineage.Sha256);
                writer.WriteBytes(value.Lineage.CanonicalBytes.AsSpan());
                break;
            case CounterfactualExecutionValueKind.TargetException:
                writer.WriteBytes(value.TargetOutcome!.CanonicalBytes.AsSpan());
                break;
            case CounterfactualExecutionValueKind.ExecutionPrefix:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static void WriteProvenance(
        CounterfactualCanonicalWriter writer,
        ImmutableArray<EvaluationProvenance> provenance)
    {
        writer.WriteInt32(provenance.Length);
        foreach (var item in provenance)
        {
            writer.WriteInt32(Tag(item.Kind));
            writer.WriteString(item.SourceId);
            WriteNullableUInt64(writer, item.Address);
            WriteNullableInt32(writer, item.RequestedLength);
            WriteNullableInt32(writer, item.ObservedLength);
        }
    }

    private static void WriteDiagnostics(
        CounterfactualCanonicalWriter writer,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        writer.WriteInt32(diagnostics.Length);
        foreach (var diagnostic in diagnostics)
        {
            writer.WriteString(diagnostic.Code);
            writer.WriteString(diagnostic.Message);
        }
    }

    private static void WriteTraversalCharge(
        CounterfactualCanonicalWriter writer,
        MethodGraphTraversalCharge charge)
    {
        writer.WriteInt32(charge.Ordinal);
        writer.WriteInt32(Tag(charge.Kind));
        writer.WriteMethod(charge.Method);
        writer.WriteBoolean(charge.Field.HasValue);
        if (charge.Field is { } field)
        {
            writer.WriteField(field);
        }

        writer.WriteInt32(charge.IlOffset);
        writer.WriteInt32(charge.RawMetadataToken);
    }

    private static void WriteField(CounterfactualCanonicalWriter writer, ResolvedField field)
    {
        writer.WriteField(field.Handle);
        writer.WriteType(field.DeclaringType);
        writer.WriteType(field.FieldType);
        writer.WriteBoolean(field.IsStatic);
        writer.WriteBoolean(field.IsLiteral);
        writer.WriteBoolean(field.HasRva);
    }

    private static void WriteMethods(CounterfactualCanonicalWriter writer, ImmutableArray<MethodHandle> methods)
    {
        writer.WriteInt32(methods.Length);
        foreach (var method in methods)
        {
            writer.WriteMethod(method);
        }
    }

    private static void WriteNullableInt32(CounterfactualCanonicalWriter writer, int? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt32(value.Value);
        }
    }

    private static void WriteNullableInt64(CounterfactualCanonicalWriter writer, long? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteInt64(value.Value);
        }
    }

    private static void WriteNullableUInt64(CounterfactualCanonicalWriter writer, ulong? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value.HasValue)
        {
            writer.WriteUInt64(value.Value);
        }
    }

    private static int Tag(CounterfactualExecutionOriginKind value) => value switch
    {
        CounterfactualExecutionOriginKind.RootedFacade => 1,
        CounterfactualExecutionOriginKind.StandaloneTargetOutcome => 2,
        CounterfactualExecutionOriginKind.FacadeRejection => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(CounterfactualExecutionValueKind value) => value switch
    {
        CounterfactualExecutionValueKind.ExactReturn => 1,
        CounterfactualExecutionValueKind.UnknownReturn => 2,
        CounterfactualExecutionValueKind.TargetException => 3,
        CounterfactualExecutionValueKind.ExecutionPrefix => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(CounterfactualBoundStatus value) => value switch
    {
        CounterfactualBoundStatus.NotApplicable => 1,
        CounterfactualBoundStatus.NotReached => 2,
        CounterfactualBoundStatus.Applied => 3,
        CounterfactualBoundStatus.Exhausted => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationSemanticMode value) => value switch
    {
        EvaluationSemanticMode.CounterfactualExecution => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationCompletionStatus value) => value switch
    {
        EvaluationCompletionStatus.Completed => 1,
        EvaluationCompletionStatus.Blocked => 2,
        EvaluationCompletionStatus.BudgetExhausted => 3,
        EvaluationCompletionStatus.Cancelled => 4,
        EvaluationCompletionStatus.Invalid => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationCompleteness value) => value switch
    {
        EvaluationCompleteness.Complete => 1,
        EvaluationCompleteness.Partial => 2,
        EvaluationCompleteness.None => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationEvidenceStatus value) => CounterfactualCanonicalTags.Tag(value);

    private static int Tag(EvaluationEffectStatus value) => CounterfactualCanonicalTags.Tag(value);

    private static int Tag(EvaluationEvidenceSourceKind value) => value switch
    {
        EvaluationEvidenceSourceKind.None => 1,
        EvaluationEvidenceSourceKind.DumpSnapshot => 2,
        EvaluationEvidenceSourceKind.Artifact => 3,
        EvaluationEvidenceSourceKind.Synthetic => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationFallbackStatus value) => value switch
    {
        EvaluationFallbackStatus.None => 1,
        EvaluationFallbackStatus.Applied => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(FrozenMethodCallDisposition value) => CounterfactualCanonicalTags.Tag(value);

    private static int Tag(PureCallModelConfidence value) => CounterfactualCanonicalTags.Tag(value);

    private static int Tag(MethodGraphTraversalChargeKind value) => CounterfactualCanonicalTags.Tag(value);

    private static int Tag(PureModelAttemptOutcomeKind value) => value switch
    {
        PureModelAttemptOutcomeKind.ExactReturn => 1,
        PureModelAttemptOutcomeKind.UnknownReturn => 2,
        PureModelAttemptOutcomeKind.Blocked => 3,
        PureModelAttemptOutcomeKind.Invalid => 4,
        PureModelAttemptOutcomeKind.CapabilityFailure => 5,
        PureModelAttemptOutcomeKind.MalformedOutcome => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(DebugEventKind value) => value switch
    {
        DebugEventKind.InstructionExecuted => 1,
        DebugEventKind.FramePopped => 2,
        DebugEventKind.TargetExceptionRaised => 3,
        DebugEventKind.ValuePrecisionLost => 4,
        DebugEventKind.FramePushed => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int Tag(EvaluationProvenanceKind value) => value switch
    {
        EvaluationProvenanceKind.DumpMemory => 1,
        EvaluationProvenanceKind.RuntimeStructure => 2,
        EvaluationProvenanceKind.Artifact => 3,
        EvaluationProvenanceKind.Policy => 4,
        EvaluationProvenanceKind.Transformation => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
