using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Retains one typed stopping boundary when W6 preparation cannot issue a complete member-chain plan.</summary>
/// <remarks>
/// This draft value carries no partial plan and no target-derived value. Later product projection can preserve its
/// independent evidence status, issue, reads, bounds, stable code, and explanation.
/// </remarks>
public sealed class DumpMemberChainPreparationFailure
{
    private readonly ImmutableArray<MemoryReadResult> evidence;
    private readonly ImmutableArray<EvaluationDeterministicBound> appliedBounds;

    internal DumpMemberChainPreparationFailure(
        string code,
        string message,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence = default,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
        {
            throw new ArgumentException("A bounded stable preparation code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 2_048)
        {
            throw new ArgumentException("A bounded preparation explanation is required.", nameof(message));
        }

        if (status == ClrmdEvidenceStatus.Exact || issue == ClrmdValueIssue.None)
        {
            throw new ArgumentException("A preparation failure requires a non-exact evidence status and issue.");
        }

        Code = code;
        Message = message;
        Status = status;
        Issue = issue;
        this.evidence = evidence.IsDefault
            ? ImmutableArray<MemoryReadResult>.Empty
            : ImmutableArray.CreateRange(evidence.AsSpan().ToArray());
        this.appliedBounds = appliedBounds.IsDefault
            ? ImmutableArray<EvaluationDeterministicBound>.Empty
            : ImmutableArray.CreateRange(appliedBounds.AsSpan().ToArray());
    }

    /// <summary>Gets the bounded stable machine-readable stopping code.</summary>
    public string Code { get; }

    /// <summary>Gets the artifact-independent explanation that callers must not parse for behavior.</summary>
    public string Message { get; }

    /// <summary>Gets the adapter evidence status at the stopping boundary.</summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>Gets the stable adapter issue at the stopping boundary.</summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>Gets a defensive copy of every counted read completed before preparation stopped.</summary>
    public ImmutableArray<MemoryReadResult> Evidence =>
        ImmutableArray.CreateRange(evidence.AsSpan().ToArray());

    /// <summary>Gets a defensive copy of every deterministic adapter bound actually reached before failure.</summary>
    public ImmutableArray<EvaluationDeterministicBound> AppliedBounds =>
        ImmutableArray.CreateRange(appliedBounds.AsSpan().ToArray());
}

/// <summary>Contains either one complete immutable W6 member-chain plan or one typed value-free failure.</summary>
/// <remarks>This draft result never exposes a partial plan.</remarks>
public sealed class DumpMemberChainPreparationResult
{
    private DumpMemberChainPreparationResult(
        DumpMemberChainPlan? plan,
        DumpMemberChainPreparationFailure? failure)
    {
        if ((plan is null) == (failure is null))
        {
            throw new ArgumentException("Exactly one preparation payload is required.");
        }

        Plan = plan;
        Failure = failure;
    }

    /// <summary>Gets whether preparation issued one complete plan and no failure.</summary>
    public bool IsSuccess => Plan is not null;

    /// <summary>Gets the complete immutable plan on success; otherwise, gets <see langword="null"/>.</summary>
    public DumpMemberChainPlan? Plan { get; }

    /// <summary>Gets the typed value-free failure when no plan was issued; otherwise, gets <see langword="null"/>.</summary>
    public DumpMemberChainPreparationFailure? Failure { get; }

    internal static DumpMemberChainPreparationResult Succeeded(DumpMemberChainPlan plan) =>
        new(plan ?? throw new ArgumentNullException(nameof(plan)), failure: null);

    internal static DumpMemberChainPreparationResult Failed(DumpMemberChainPreparationFailure failure) =>
        new(plan: null, failure ?? throw new ArgumentNullException(nameof(failure)));
}

internal static class DumpMemberChainPlanBinder
{
    internal static DumpMemberChainPreparationResult Bind(
        DumpQueryRootBinding rootBinding,
        ClrmdDeclaredDataMemberCertificate certificate,
        DumpMemberChainAccessKind accessKind,
        DumpMemberChainFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback,
        ImmutableArray<byte> requestCanonicalBytes,
        string requestSha256,
        ImmutableArray<byte> expressionIdentityCanonicalBytes,
        string expressionIdentitySha256,
        ImmutableArray<EvaluationDeterministicBound> requestBounds,
        ImmutableArray<MemoryReadResult> preparationEvidence,
        ImmutableArray<EvaluationDeterministicBound> preparationBounds)
    {
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(certificate);
        var root = rootBinding.Root;
        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || root is null)
        {
            return Failed(
                "QUERY_CHAIN_ROOT_NOT_EXACT",
                "Member-chain preparation requires one exact root object.",
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ObjectUnavailable,
                preparationEvidence,
                preparationBounds);
        }

        if (certificate.OuterField.Snapshot != root.Snapshot ||
            certificate.DeclaredTarget.RuntimeModule.Snapshot != root.Snapshot)
        {
            return Failed(
                "QUERY_CHAIN_SNAPSHOT_CONFLICT",
                "The root and declared-member certificate belong to different snapshots.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch,
                preparationEvidence,
                preparationBounds);
        }

        if (certificate.RootTypeToken != root.TypeMetadataToken ||
            !string.Equals(certificate.RootTypeName, root.TypeName, StringComparison.Ordinal) ||
            certificate.OuterField.OwnerAddress != root.Address ||
            certificate.OuterField.OwnerMethodTable != root.MethodTable ||
            !string.Equals(certificate.OuterField.OwnerTypeName, root.TypeName, StringComparison.Ordinal))
        {
            return Failed(
                "QUERY_CHAIN_ROOT_CERTIFICATE_CONFLICT",
                "The exact root and declared-member certificate describe different owners.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                preparationEvidence,
                preparationBounds);
        }

        if (!IsFallbackCompatible(certificate.Decoder, accessKind, fallbackKind))
        {
            return Failed(
                "QUERY_CHAIN_COALESCE_TYPE_UNSUPPORTED",
                "The coalescing literal is incompatible with the certified terminal value shape.",
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemberShapeUnsupported,
                preparationEvidence,
                preparationBounds);
        }

        try
        {
            var plan = new DumpMemberChainPlan(
                rootBinding,
                certificate,
                accessKind,
                fallbackKind,
                int32Fallback,
                stringFallback,
                requestCanonicalBytes,
                requestSha256,
                expressionIdentityCanonicalBytes,
                expressionIdentitySha256,
                requestBounds,
                preparationEvidence,
                preparationBounds);
            return DumpMemberChainPreparationResult.Succeeded(plan);
        }
        catch (ArgumentException)
        {
            return Failed(
                "QUERY_CHAIN_PLAN_INVALID",
                "The accepted request or certificate violated the complete-plan identity contract.",
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                preparationEvidence,
                preparationBounds);
        }
    }

    private static bool IsFallbackCompatible(
        ClrmdTerminalDecoderKind decoder,
        DumpMemberChainAccessKind accessKind,
        DumpMemberChainFallbackKind fallbackKind)
    {
        if (!Enum.IsDefined(accessKind) || !Enum.IsDefined(fallbackKind))
        {
            return false;
        }

        return fallbackKind switch
        {
            DumpMemberChainFallbackKind.None => true,
            DumpMemberChainFallbackKind.Null =>
                decoder is ClrmdTerminalDecoderKind.String or ClrmdTerminalDecoderKind.NullableInt32 ||
                decoder == ClrmdTerminalDecoderKind.Int32 && accessKind == DumpMemberChainAccessKind.Conditional,
            DumpMemberChainFallbackKind.Int32 =>
                decoder == ClrmdTerminalDecoderKind.NullableInt32 ||
                decoder == ClrmdTerminalDecoderKind.Int32 && accessKind == DumpMemberChainAccessKind.Conditional,
            DumpMemberChainFallbackKind.String => decoder == ClrmdTerminalDecoderKind.String,
            _ => false,
        };
    }

    private static DumpMemberChainPreparationResult Failed(
        string code,
        string message,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence,
        ImmutableArray<EvaluationDeterministicBound> bounds) =>
        DumpMemberChainPreparationResult.Failed(new DumpMemberChainPreparationFailure(
            code,
            message,
            status,
            issue,
            evidence,
            bounds));
}
