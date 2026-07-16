using System.Collections.Immutable;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;

namespace Interpreter.Product.DumpDebugging;

/// <summary>
/// Binds one already-admitted W6 member-chain request to a complete immutable declaration plan.
/// </summary>
/// <remarks>
/// This draft facade is capability-separated from evaluation: its source can certify declarations but cannot read an
/// outer reference or terminal value. The request was already parsed once by the pinned complete C# front end and only
/// its project-owned admitted identity reaches this stage.
/// </remarks>
public static class DumpMemberChainPreparationFacade
{
    /// <summary>Prepares one admitted W6 request against an already-open immutable dump session.</summary>
    /// <param name="session">The session that issued the request's exact root and all declaration evidence.</param>
    /// <param name="request">The accepted fixed-depth member-chain request produced by the product classifier.</param>
    /// <returns>
    /// One complete canonical plan, or a typed value-free failure retaining evidence only through its stopping boundary.
    /// No pointer or terminal-value read occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public static DumpMemberChainPreparationResult Prepare(
        ClrmdDumpSession session,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Prepare(new ClrmdDumpMemberChainEvidenceSource(session), request);
    }

    internal static DumpMemberChainPreparationResult Prepare(
        IDumpMemberChainEvidenceSource source,
        DumpExpressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        var identity = request.MemberChainExpressionIdentity;
        if (request.AdmittedKind != DumpExpressionKind.FixedDepthMemberChain ||
            request.LanguageProfile != DumpExpressionLanguageProfile.FixedDepthMemberChainV1 ||
            identity is null)
        {
            return Failed(
                "QUERY_CHAIN_REQUEST_INVALID",
                "Preparation requires one accepted FixedDepthMemberChainV1 request.",
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var rootBinding = request.RootBinding;
        var root = rootBinding.Root;
        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || root is null)
        {
            return Failed(
                "QUERY_CHAIN_ROOT_NOT_EXACT",
                "Member-chain preparation requires one exact root object.",
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ObjectUnavailable);
        }

        if (source.Snapshot != rootBinding.Snapshot)
        {
            return Failed(
                "QUERY_CHAIN_SNAPSHOT_CONFLICT",
                "The preparation source and exact root belong to different snapshots.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        var certificateResult = source.CertifyDeclaredDataMember(
            root,
            identity.ReferenceMemberName,
            identity.TerminalMemberName);
        if (certificateResult.Status != ClrmdEvidenceStatus.Exact || certificateResult.Value is null)
        {
            return FromCertificateFailure(certificateResult);
        }

        var certificate = certificateResult.Value;
        if (!string.Equals(certificate.OuterField.Name, identity.ReferenceMemberName, StringComparison.Ordinal) ||
            !string.Equals(certificate.TerminalMemberName, identity.TerminalMemberName, StringComparison.Ordinal))
        {
            return Failed(
                "QUERY_CHAIN_CERTIFICATE_CONFLICT",
                "The certificate does not describe the admitted ordinal member path.",
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                certificateResult.Evidence,
                certificateResult.AppliedBounds);
        }

        return DumpMemberChainPlanBinder.Bind(
            rootBinding,
            certificate,
            identity.AccessKind,
            identity.FallbackKind,
            identity.Int32Fallback,
            identity.StringFallback,
            request.CanonicalBytes,
            request.Sha256,
            identity.CanonicalBytes,
            identity.Sha256,
            request.ReachedBounds,
            certificateResult.Evidence,
            certificateResult.AppliedBounds);
    }

    private static DumpMemberChainPreparationResult FromCertificateFailure(
        ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> result)
    {
        var (code, message) = result.Status switch
        {
            ClrmdEvidenceStatus.Partial => (
                "QUERY_CHAIN_BIND_PARTIAL",
                "Declaration binding stopped after retaining incomplete evidence."),
            ClrmdEvidenceStatus.Unavailable => (
                "QUERY_CHAIN_BIND_UNAVAILABLE",
                "The required declared member or complete declaration evidence is unavailable."),
            ClrmdEvidenceStatus.Conflict => (
                "QUERY_CHAIN_BIND_CONFLICT",
                "Available declaration evidence disagrees with the accepted member path."),
            ClrmdEvidenceStatus.Invalid => (
                "QUERY_CHAIN_BIND_INVALID",
                "Captured declaration evidence violates a supported structural invariant."),
            _ => (
                "QUERY_CHAIN_BIND_INVALID",
                "Declaration binding returned an incoherent result."),
        };
        var status = result.Status == ClrmdEvidenceStatus.Exact
            ? ClrmdEvidenceStatus.Invalid
            : result.Status;
        var issue = result.Status == ClrmdEvidenceStatus.Exact || result.Issue == ClrmdValueIssue.None
            ? ClrmdValueIssue.InvalidData
            : result.Issue;
        return Failed(code, message, status, issue, result.Evidence, result.AppliedBounds);
    }

    private static DumpMemberChainPreparationResult Failed(
        string code,
        string message,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence = default,
        ImmutableArray<Interpreter.Core.Abstractions.EvaluationDeterministicBound> bounds = default) =>
        DumpMemberChainPreparationResult.Failed(new DumpMemberChainPreparationFailure(
            code,
            message,
            status,
            issue,
            evidence,
            bounds));
}
