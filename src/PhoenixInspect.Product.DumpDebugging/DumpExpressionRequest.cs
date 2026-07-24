using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>Chooses the closed W4 execution realization for the one admitted method expression.</summary>
public enum DumpMethodEvaluationMode
{
    /// <summary>Requires the complete admitted caller/helper graph and interprets both method bodies.</summary>
    Interpreted = 1,

    /// <summary>Interprets the caller while replacing the admitted helper with its exact versioned pure model.</summary>
    Modeled = 2,
}

/// <summary>Identifies which closed W5 expression grammar admitted a bounded request.</summary>
public enum DumpExpressionKind
{
    /// <summary>The unchanged W2 root-field grammar admitted the expression.</summary>
    DerivedQuery = 1,

    /// <summary>The exact versioned W5 method-expression grammar admitted the expression.</summary>
    CounterfactualMethod = 2,

    /// <summary>The opt-in W6 fixed-depth direct or conditional member-chain grammar admitted the expression.</summary>
    FixedDepthMemberChain = 3,
}

/// <summary>Chooses the versioned expression-admission profile applied after one complete C# expression parse.</summary>
/// <remarks>
/// This draft prototype choice is explicit so the frozen W5 language remains unchanged while W6 callers opt into
/// fixed-depth member chains. It does not imply that binding or evaluation supports arbitrary C# parse trees.
/// </remarks>
public enum DumpExpressionLanguageProfile
{
    /// <summary>Retains the exact W2 field and W5 method spelling and classification behavior.</summary>
    FrozenW5 = 1,

    /// <summary>Adds the version-one direct and conditional two-member chain shapes after the legacy recognizers.</summary>
    FixedDepthMemberChainV1 = 2,
}

/// <summary>Publishes the frozen, project-owned identity of the complete C# expression front-end profile.</summary>
/// <remarks>
/// These draft constants describe parsing and structural work bounds only. They do not widen the product's admitted
/// binding or evaluation semantics, and no compiler object is exposed through this surface.
/// </remarks>
public static class DumpCSharpExpressionProfile
{
    /// <summary>Gets the canonical front-end profile identity.</summary>
    public const string Id = "RoslynCSharpExpressionV1";

    /// <summary>Gets the sole direct compiler-front-end package.</summary>
    public const string PackageId = "Microsoft.CodeAnalysis.CSharp";

    /// <summary>Gets the exactly pinned package version.</summary>
    public const string PackageVersion = "5.3.0";

    /// <summary>Gets the exactly pinned C# language-version name.</summary>
    public const string LanguageVersion = "CSharp14";

    /// <summary>Gets the pre-parse UTF-16 input limit.</summary>
    public const int MaximumExpressionCharacters = 512;

    /// <summary>Gets the post-parse combined syntax-node and token limit.</summary>
    public const int MaximumNodesAndTokens = 256;

    /// <summary>Gets the post-parse syntax-node depth limit.</summary>
    public const int MaximumSyntaxDepth = 64;

    /// <summary>Gets the decoded identifier-value limit.</summary>
    public const int MaximumIdentifierCharacters = 64;

    /// <summary>Gets the decoded string-literal-value limit.</summary>
    public const int MaximumStringCharacters = 256;
}

/// <summary>Classifies the final syntax-only disposition of one W5 expression request.</summary>
public enum DumpExpressionClassificationStatus
{
    /// <summary>One closed grammar admitted the complete expression.</summary>
    Accepted = 1,

    /// <summary>The input was missing, malformed, mismatched with the selected root, or exceeded a bound.</summary>
    Invalid = 2,

    /// <summary>The complete input was well-bounded but requested syntax outside both closed grammars.</summary>
    Unsupported = 3,
}

/// <summary>
/// Freezes the bounded execution policy supplied with every W5 expression request, including requests that route to
/// the unchanged W2 query path and therefore do not consume the method-specific limits.
/// </summary>
/// <remarks>
/// This is a draft closed product policy, not an extensible policy bag. Its stable W4 policy/catalog identities,
/// versions, and assumptions are fixed here so a host supplies only the bounded choice that the implementation can
/// honestly honor. Canonical identity distinguishes interpreted and modeled execution even before either is reached.
/// </remarks>
public sealed class DumpExpressionPolicy
{
    /// <summary>Gets the canonical policy schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    /// <summary>Gets the W4 policy identity reused by the W5 product facade.</summary>
    public const string CounterfactualPolicyId = "policy.counterfactual.dump";

    /// <summary>Gets the W4 pure-model catalog identity reused by the W5 product facade.</summary>
    public const string ModelCatalogId = "catalog.counterfactual.dump";

    private static readonly PureCallModelVersion VersionOne = new(1, 0, 0);
    private static readonly ImmutableArray<string> FixedAssumptions =
        ImmutableArray.Create("assume.read-only", "assume.counterfactual-not-historical");

    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionPolicy(
        DumpMethodEvaluationMode methodMode,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit)
    {
        SchemaVersion = CanonicalSchemaVersion;
        MethodMode = methodMode;
        InstructionLimit = instructionLimit;
        LogicalDepthLimit = logicalDepthLimit;
        TraversalLimit = traversalLimit;
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the canonical schema version used by this policy.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets whether an admitted method expression uses the interpreted or pure-modeled W4 realization.</summary>
    public DumpMethodEvaluationMode MethodMode { get; }

    /// <summary>Gets the maximum W4 machine instruction units, including zero.</summary>
    public long InstructionLimit { get; }

    /// <summary>Gets the maximum W4 logical call depth, including zero.</summary>
    public int LogicalDepthLimit { get; }

    /// <summary>Gets the maximum W4 graph-preparation traversal units, including zero.</summary>
    public int TraversalLimit { get; }

    /// <summary>Gets the exact fixed W4 policy semantic version.</summary>
    public PureCallModelVersion PolicyVersion => VersionOne;

    /// <summary>Gets the exact fixed W4 pure-model catalog semantic version.</summary>
    public PureCallModelVersion ModelCatalogVersion => VersionOne;

    /// <summary>Gets a defensive copy of the fixed assumptions that qualify every W4 result.</summary>
    public ImmutableArray<string> Assumptions =>
        ImmutableArray.CreateRange(FixedAssumptions.AsSpan().ToArray());

    /// <summary>Gets a defensive copy of the canonical schema-v1 policy bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes =>
        ImmutableArray.CreateRange(canonicalBytes.AsSpan().ToArray());

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated closed W5 expression policy.</summary>
    /// <param name="methodMode">The required realization if the expression routes to the W4 method path.</param>
    /// <param name="instructionLimit">The nonnegative W4 instruction-unit limit.</param>
    /// <param name="logicalDepthLimit">
    /// The W4 logical-depth limit from zero through <see cref="CounterfactualMethodRequest.MaximumLogicalCallDepth"/>.
    /// </param>
    /// <param name="traversalLimit">
    /// The W4 preparation limit from zero through <see cref="CounterfactualMethodRequest.MaximumTraversalUnits"/>.
    /// </param>
    /// <returns>An immutable policy with a path-independent canonical identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A mode or limit is outside the closed W4 envelope.</exception>
    public static DumpExpressionPolicy Create(
        DumpMethodEvaluationMode methodMode,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit)
    {
        if (methodMode is not (DumpMethodEvaluationMode.Interpreted or DumpMethodEvaluationMode.Modeled))
        {
            throw new ArgumentOutOfRangeException(nameof(methodMode));
        }

        CounterfactualCanonical.ValidateNonNegative(instructionLimit, nameof(instructionLimit));
        CounterfactualCanonical.ValidateBound(
            logicalDepthLimit,
            CounterfactualMethodRequest.MaximumLogicalCallDepth,
            nameof(logicalDepthLimit));
        CounterfactualCanonical.ValidateBound(
            traversalLimit,
            CounterfactualMethodRequest.MaximumTraversalUnits,
            nameof(traversalLimit));
        return new DumpExpressionPolicy(methodMode, instructionLimit, logicalDepthLimit, traversalLimit);
    }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("w5-dump-expression-policy");
        writer.WriteInt32(CanonicalSchemaVersion);
        writer.WriteInt32((int)MethodMode);
        writer.WriteInt64(InstructionLimit);
        writer.WriteInt32(LogicalDepthLimit);
        writer.WriteInt32(TraversalLimit);
        writer.WriteString(CounterfactualPolicyId);
        writer.WriteVersion(VersionOne);
        writer.WriteString(ModelCatalogId);
        writer.WriteVersion(VersionOne);
        writer.WriteInt32(FixedAssumptions.Length);
        foreach (var assumption in FixedAssumptions)
        {
            writer.WriteString(assumption);
        }

        return writer.ToImmutableArray();
    }
}

/// <summary>
/// Gives the one admitted W5 method expression a separate versioned identity without changing any W2 request or plan
/// identity.
/// </summary>
public sealed class DumpMethodExpressionIdentity
{
    /// <summary>Gets the canonical method-expression grammar version.</summary>
    public const int CanonicalSchemaVersion = 1;

    private readonly ImmutableArray<byte> canonicalBytes;

    internal DumpMethodExpressionIdentity(string rootName, string expression)
    {
        RootName = rootName;
        Expression = expression;
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("w5-dump-method-expression");
        writer.WriteInt32(CanonicalSchemaVersion);
        writer.WriteString(rootName);
        writer.WriteString(DumpExpressionClassifier.SupportedMethodName);
        writer.WriteString(expression);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact case-sensitive host root identifier.</summary>
    public string RootName { get; }

    /// <summary>Gets the exact admitted expression text, without normalization.</summary>
    public string Expression { get; }

    /// <summary>Gets a defensive copy of the canonical schema-v1 method-expression bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes =>
        ImmutableArray.CreateRange(canonicalBytes.AsSpan().ToArray());

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }
}

/// <summary>
/// Freezes one opt-in W6 fixed-depth member-chain descriptor independently of metadata binding and value reads.
/// </summary>
/// <remarks>
/// This draft identity contains only bounded raw text and project-owned decoded scalars. It deliberately contains no
/// compiler syntax object, symbol, diagnostic, metadata member, runtime address, or evaluation claim.
/// </remarks>
public sealed class DumpMemberChainExpressionIdentity
{
    /// <summary>Gets the canonical member-chain identity schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    /// <summary>Gets the exact opt-in admission-profile identity.</summary>
    public const string AdmissionProfileId = "FixedDepthMemberChainV1";

    private readonly ImmutableArray<byte> canonicalBytes;

    internal DumpMemberChainExpressionIdentity(string expression, ParsedExpressionDescriptor descriptor)
    {
        if (descriptor.Operation is not (
            ParsedExpressionOperationKind.DirectMemberChain or
            ParsedExpressionOperationKind.ConditionalMemberChain))
        {
            throw new ArgumentException("A member-chain identity requires one admitted chain descriptor.", nameof(descriptor));
        }

        Expression = expression;
        RootName = descriptor.RootName;
        ReferenceMemberName = descriptor.FirstMemberName;
        TerminalMemberName = descriptor.SecondMemberName!;
        AccessKind = descriptor.Operation == ParsedExpressionOperationKind.ConditionalMemberChain
            ? DumpMemberChainAccessKind.Conditional
            : DumpMemberChainAccessKind.Direct;
        FallbackKind = descriptor.CoalesceLiteral switch
        {
            null => DumpMemberChainFallbackKind.None,
            { Kind: DumpQueryLiteralKind.Null } => DumpMemberChainFallbackKind.Null,
            { Kind: DumpQueryLiteralKind.Int32 } => DumpMemberChainFallbackKind.Int32,
            { Kind: DumpQueryLiteralKind.String } => DumpMemberChainFallbackKind.String,
            _ => throw new InvalidOperationException("The admitted chain contains an invalid coalescing literal."),
        };
        Int32Fallback = descriptor.CoalesceLiteral is { Kind: DumpQueryLiteralKind.Int32 } int32
            ? int32.Int32Value
            : null;
        StringFallback = descriptor.CoalesceLiteral is { Kind: DumpQueryLiteralKind.String } text
            ? text.StringValue
            : null;
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact bounded raw expression, including trivia and literal spelling.</summary>
    public string Expression { get; }

    /// <summary>Gets the decoded, case-sensitive root identifier value.</summary>
    public string RootName { get; }

    /// <summary>Gets the decoded declared reference-member identifier value.</summary>
    public string ReferenceMemberName { get; }

    /// <summary>Gets the decoded terminal-member identifier value.</summary>
    public string TerminalMemberName { get; }

    /// <summary>Gets whether the terminal hop is direct or conditional.</summary>
    public DumpMemberChainAccessKind AccessKind { get; }

    /// <summary>Gets the typed coalescing-literal discriminator.</summary>
    public DumpMemberChainFallbackKind FallbackKind { get; }

    /// <summary>Gets the exact integer fallback only when <see cref="FallbackKind"/> is <see cref="DumpMemberChainFallbackKind.Int32"/>.</summary>
    public int? Int32Fallback { get; }

    /// <summary>Gets the decoded string fallback only when <see cref="FallbackKind"/> is <see cref="DumpMemberChainFallbackKind.String"/>.</summary>
    public string? StringFallback { get; }

    /// <summary>Gets a defensive copy of the canonical schema-v1 chain bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes =>
        ImmutableArray.CreateRange(canonicalBytes.AsSpan().ToArray());

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("w6-dump-member-chain-expression");
        writer.WriteInt32(CanonicalSchemaVersion);
        writer.WriteString(DumpCSharpExpressionProfile.Id);
        writer.WriteString(DumpCSharpExpressionProfile.PackageId);
        writer.WriteString(DumpCSharpExpressionProfile.PackageVersion);
        writer.WriteString(DumpCSharpExpressionProfile.LanguageVersion);
        writer.WriteInt32(DumpCSharpExpressionProfile.MaximumExpressionCharacters);
        writer.WriteInt32(DumpCSharpExpressionProfile.MaximumNodesAndTokens);
        writer.WriteInt32(DumpCSharpExpressionProfile.MaximumSyntaxDepth);
        writer.WriteInt32(DumpCSharpExpressionProfile.MaximumIdentifierCharacters);
        writer.WriteInt32(DumpCSharpExpressionProfile.MaximumStringCharacters);
        writer.WriteString(AdmissionProfileId);
        writer.WriteString(Expression);
        writer.WriteString(RootName);
        writer.WriteString(ReferenceMemberName);
        writer.WriteString(TerminalMemberName);
        writer.WriteInt32((int)AccessKind);
        writer.WriteInt32((int)FallbackKind);
        if (Int32Fallback is { } int32)
        {
            writer.WriteInt32(int32);
        }

        if (StringFallback is not null)
        {
            writer.WriteString(StringFallback);
        }

        return writer.ToImmutableArray();
    }
}

/// <summary>
/// Freezes one bounded W5 product request before evidence acquisition or execution and binds it to the exact
/// host-selected dump object.
/// </summary>
/// <remarks>
/// A request preserves the raw expression, root-selection outcome, policy, and syntax bounds reached. Its identity is
/// distinct from the unchanged W2 canonical query-plan identity. Rejected but bounded syntax still receives a request
/// identity; oversized input and non-exact roots do not, because they cannot satisfy this bounded exact-root contract.
/// </remarks>
public sealed class DumpExpressionRequest
{
    /// <summary>Gets the canonical request schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    /// <summary>Gets the greatest raw expression length retained by a canonical request.</summary>
    public const int MaximumExpressionCharacters = 512;

    /// <summary>Gets the greatest host root-identifier length retained by a canonical request.</summary>
    public const int MaximumRootNameCharacters = 64;

    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    internal DumpExpressionRequest(
        string? expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionPolicy policy,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds,
        DumpExpressionLanguageProfile languageProfile,
        DumpExpressionKind? admittedKind,
        ParsedExpressionDescriptor? parsedExpression,
        DumpQueryParserBounds parserBounds,
        DumpMethodExpressionIdentity? methodExpressionIdentity,
        DumpMemberChainExpressionIdentity? memberChainExpressionIdentity)
    {
        if (expression?.Length > MaximumExpressionCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(expression));
        }

        if (rootBinding.Name?.Length > MaximumRootNameCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(rootBinding));
        }

        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
        {
            throw new ArgumentException("A canonical W5 request requires one exact host-selected root.", nameof(rootBinding));
        }

        SchemaVersion = CanonicalSchemaVersion;
        Expression = expression;
        RootBinding = rootBinding;
        Policy = policy;
        this.reachedBounds = NormalizeBounds(reachedBounds);
        LanguageProfile = languageProfile;
        AdmittedKind = admittedKind;
        ParsedExpression = parsedExpression;
        ParserBounds = parserBounds;
        MethodExpressionIdentity = methodExpressionIdentity;
        MemberChainExpressionIdentity = memberChainExpressionIdentity;
        ValidateAdmissionState();
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the canonical request schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the exact raw expression, including <see langword="null"/> for a bounded invalid request.</summary>
    public string? Expression { get; }

    /// <summary>Gets the exact host-selected root binding frozen into this request.</summary>
    public DumpQueryRootBinding RootBinding { get; }

    /// <summary>Gets the immutable bounded policy supplied for this request.</summary>
    public DumpExpressionPolicy Policy { get; }

    /// <summary>Gets the explicit versioned language profile selected before classification.</summary>
    public DumpExpressionLanguageProfile LanguageProfile { get; }

    /// <summary>Gets the admitted closed grammar, or <see langword="null"/> when bounded syntax was rejected.</summary>
    public DumpExpressionKind? AdmittedKind { get; }

    /// <summary>
    /// Gets the separate versioned method-expression identity for an admitted method request, or
    /// <see langword="null"/> for W2 and rejected syntax.
    /// </summary>
    public DumpMethodExpressionIdentity? MethodExpressionIdentity { get; }

    /// <summary>
    /// Gets the separate versioned member-chain identity for an admitted opt-in W6 request, or
    /// <see langword="null"/> for legacy operations and rejected syntax.
    /// </summary>
    public DumpMemberChainExpressionIdentity? MemberChainExpressionIdentity { get; }

    /// <summary>Gets a defensive, canonical-name-ordered copy of syntax bounds reached during classification.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        ImmutableArray.CreateRange(reachedBounds.AsSpan().ToArray());

    /// <summary>Gets a defensive copy of the canonical schema-v1 request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes =>
        ImmutableArray.CreateRange(canonicalBytes.AsSpan().ToArray());

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    internal ParsedExpressionDescriptor? ParsedExpression { get; }

    internal DumpQueryParserBounds ParserBounds { get; }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString(LanguageProfile == DumpExpressionLanguageProfile.FrozenW5
            ? "w5-dump-expression-request"
            : "w6-dump-expression-request");
        writer.WriteInt32(CanonicalSchemaVersion);
        if (LanguageProfile != DumpExpressionLanguageProfile.FrozenW5)
        {
            writer.WriteInt32((int)LanguageProfile);
            writer.WriteString(DumpCSharpExpressionProfile.Id);
        }

        WriteNullableString(writer, Expression);
        writer.WriteBoolean(AdmittedKind.HasValue);
        if (AdmittedKind is { } kind)
        {
            writer.WriteInt32((int)kind);
        }

        WriteRoot(writer, RootBinding);
        WriteBounds(writer, reachedBounds);
        writer.WriteBytes(Policy.CanonicalBytes.AsSpan());
        writer.WriteBoolean(MethodExpressionIdentity is not null);
        if (MethodExpressionIdentity is { } methodIdentity)
        {
            writer.WriteBytes(methodIdentity.CanonicalBytes.AsSpan());
        }

        if (LanguageProfile != DumpExpressionLanguageProfile.FrozenW5)
        {
            writer.WriteBoolean(MemberChainExpressionIdentity is not null);
            if (MemberChainExpressionIdentity is { } chainIdentity)
            {
                writer.WriteBytes(chainIdentity.CanonicalBytes.AsSpan());
            }
        }

        return writer.ToImmutableArray();
    }

    private void ValidateAdmissionState()
    {
        if (LanguageProfile is not (
            DumpExpressionLanguageProfile.FrozenW5 or
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1))
        {
            throw new ArgumentOutOfRangeException(nameof(LanguageProfile));
        }

        if ((AdmittedKind is null) != (ParsedExpression is null))
        {
            throw new ArgumentException("An admitted request requires exactly one parsed project descriptor.");
        }

        if ((AdmittedKind == DumpExpressionKind.CounterfactualMethod) != (MethodExpressionIdentity is not null))
        {
            throw new ArgumentException("The method identity must appear only on an admitted method request.");
        }

        if ((AdmittedKind == DumpExpressionKind.FixedDepthMemberChain) !=
            (MemberChainExpressionIdentity is not null))
        {
            throw new ArgumentException("The member-chain identity must appear only on an admitted chain request.");
        }

        if (AdmittedKind == DumpExpressionKind.FixedDepthMemberChain &&
            LanguageProfile != DumpExpressionLanguageProfile.FixedDepthMemberChainV1)
        {
            throw new ArgumentException("A fixed-depth member chain requires the opt-in W6 language profile.");
        }
    }

    private static void WriteRoot(CounterfactualCanonicalWriter writer, DumpQueryRootBinding binding)
    {
        var root = binding.Root!;
        WriteNullableString(writer, binding.Name);
        writer.WriteDigest(binding.Snapshot.Sha256);
        writer.WriteInt32((int)binding.Status);
        writer.WriteInt32((int)binding.Issue);
        writer.WriteUInt64(root.Address);
        writer.WriteString(root.TypeName);
        writer.WriteInt32(root.TypeMetadataToken);
        writer.WriteUInt64(root.MethodTable);
        writer.WriteUInt64(root.RootAddress);
        writer.WriteString(root.RootKind);
        writer.WriteString(root.Module.Identity.SourceId);
        WriteNullableString(writer, binding.TypeNameSelector);
        WriteNullableInt32(writer, binding.SearchStatus is { } searchStatus ? (int)searchStatus : null);
        WriteNullableInt32(writer, binding.HandlesScanned);
        WriteNullableInt32(writer, binding.MaximumHandlesScanned);
        WriteNullableInt32(writer, binding.MaximumMatches);
        WriteNullableInt32(writer, binding.MatchesRetained);
        writer.WriteBoolean(binding.MatchLimitReached.HasValue);
        if (binding.MatchLimitReached is { } limitReached)
        {
            writer.WriteBoolean(limitReached);
        }

        WriteBounds(writer, binding.AppliedBounds);
        writer.WriteInt32(binding.Evidence.Length);
        foreach (var read in binding.Evidence)
        {
            WriteRead(writer, read);
        }
    }

    private static void WriteRead(CounterfactualCanonicalWriter writer, MemoryReadResult read)
    {
        writer.WriteString(read.SourceId);
        writer.WriteUInt64(read.Address);
        writer.WriteInt32(read.RequestedLength);
        writer.WriteInt32((int)read.Status);
        writer.WriteBytes(read.Bytes.AsSpan());
    }

    private static void WriteBounds(
        CounterfactualCanonicalWriter writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    private static void WriteNullableString(CounterfactualCanonicalWriter writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    private static void WriteNullableInt32(CounterfactualCanonicalWriter writer, int? value)
    {
        writer.WriteBoolean(value.HasValue);
        if (value is { } present)
        {
            writer.WriteInt32(present);
        }
    }

    private static ImmutableArray<EvaluationDeterministicBound> NormalizeBounds(
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        if (bounds.IsDefault)
        {
            throw new ArgumentException("Reached bounds must be initialized.", nameof(bounds));
        }

        if (bounds.Any(static bound => bound is null))
        {
            throw new ArgumentException("Reached bounds cannot contain null entries.", nameof(bounds));
        }

        var normalized = bounds.OrderBy(static bound => bound.Name, StringComparer.Ordinal).ToImmutableArray();

        for (var index = 1; index < normalized.Length; index++)
        {
            if (string.Equals(normalized[index - 1].Name, normalized[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException("Reached bound names must be unique.", nameof(bounds));
            }
        }

        return normalized;
    }
}

/// <summary>Returns one complete syntax-only W5 classification without acquiring or executing dump evidence.</summary>
public sealed class DumpExpressionClassification
{
    internal DumpExpressionClassification(
        DumpExpressionClassificationStatus status,
        DumpExpressionKind? kind,
        DumpExpressionRequest? request,
        string? diagnosticCode,
        string? diagnosticMessage)
    {
        Status = status;
        Kind = kind;
        Request = request;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
    }

    /// <summary>Gets whether a closed grammar accepted the expression or why classification rejected it.</summary>
    public DumpExpressionClassificationStatus Status { get; }

    /// <summary>Gets the accepted grammar, or <see langword="null"/> after rejection.</summary>
    public DumpExpressionKind? Kind { get; }

    /// <summary>
    /// Gets the canonical bounded request, or <see langword="null"/> when oversized input or a non-exact root made
    /// canonical issuance impossible.
    /// </summary>
    public DumpExpressionRequest? Request { get; }

    /// <summary>Gets the stable diagnostic code after rejection, or <see langword="null"/> after admission.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the artifact-independent rejection explanation, or <see langword="null"/> after admission.</summary>
    public string? DiagnosticMessage { get; }
}

/// <summary>
/// Classifies the complete W5 expression before any field, method, body, graph, or memory evidence is acquired.
/// </summary>
public static class DumpExpressionClassifier
{
    /// <summary>Gets the only method name admitted by W5's version-one method-expression grammar.</summary>
    public const string SupportedMethodName = "GetMarkerSummary";

    /// <summary>Classifies one raw expression against the unchanged W2 grammar and the one exact W5 method form.</summary>
    /// <param name="expression">Raw expression text; no whitespace, casing, or punctuation normalization is applied.</param>
    /// <param name="rootBinding">
    /// The host's exact selected root. Non-exact evidence is rejected before a canonical W5 request is issued.
    /// </param>
    /// <param name="policy">The already validated closed W5 execution policy.</param>
    /// <returns>
    /// A deterministic accepted, invalid, or unsupported classification. Every bounded exact-root input carries a
    /// canonical request, including rejected syntax. The operation performs no evidence acquisition or execution.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootBinding"/> or <paramref name="policy"/> is null.</exception>
    public static DumpExpressionClassification Classify(
        string? expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionPolicy policy) =>
        Classify(expression, rootBinding, policy, DumpExpressionLanguageProfile.FrozenW5);

    /// <summary>
    /// Parses one complete bounded C# expression and runs only the recognizers enabled by an explicit language
    /// profile, without binding a member or reading runtime evidence.
    /// </summary>
    /// <param name="expression">Exact raw expression text retained by every bounded canonical request.</param>
    /// <param name="rootBinding">The exact host-selected root whose decoded name must match the expression root.</param>
    /// <param name="policy">The already validated execution policy retained by the issued request.</param>
    /// <param name="languageProfile">The frozen legacy profile or the explicit W6 member-chain opt-in.</param>
    /// <returns>
    /// A deterministic accepted, invalid, or unsupported classification containing only project-owned descriptors.
    /// A valid C# tree outside the enabled subset is unsupported rather than malformed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootBinding"/> or <paramref name="policy"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="languageProfile"/> is not defined.</exception>
    /// <remarks>
    /// This draft W6 overload performs exactly one complete Roslyn expression parse. It creates no compilation or
    /// semantic model, performs no metadata lookup or memory read, and exposes no compiler object.
    /// </remarks>
    public static DumpExpressionClassification Classify(
        string? expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionPolicy policy,
        DumpExpressionLanguageProfile languageProfile)
    {
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(policy);
        if (languageProfile is not (
            DumpExpressionLanguageProfile.FrozenW5 or
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1))
        {
            throw new ArgumentOutOfRangeException(nameof(languageProfile));
        }

        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
        {
            return Rejected(
                DumpExpressionClassificationStatus.Invalid,
                request: null,
                "W5_ROOT_SELECTION_NOT_EXACT",
                "The W5 expression facade requires one exact host-selected root object.");
        }

        var syntax = CSharpExpressionFrontEnd.Classify(
            expression,
            rootBinding.Name,
            languageProfile == DumpExpressionLanguageProfile.FrozenW5
                ? CSharpExpressionAdmissionProfile.FrozenW5
                : CSharpExpressionAdmissionProfile.FixedDepthMemberChainV1);
        if (expression?.Length > DumpExpressionRequest.MaximumExpressionCharacters ||
            rootBinding.Name?.Length > DumpExpressionRequest.MaximumRootNameCharacters)
        {
            return Rejected(
                DumpExpressionClassificationStatus.Invalid,
                request: null,
                syntax.DiagnosticCode!,
                syntax.DiagnosticMessage!);
        }

        var reachedBounds = DumpQueryEngine.ProjectParserBounds(syntax.AppliedBounds);
        if (syntax is { IsAccepted: true, Expression: { } descriptor })
        {
            var kind = descriptor.Operation switch
            {
                ParsedExpressionOperationKind.DirectMember => DumpExpressionKind.DerivedQuery,
                ParsedExpressionOperationKind.EmptyInstanceInvocation => DumpExpressionKind.CounterfactualMethod,
                ParsedExpressionOperationKind.DirectMemberChain or
                    ParsedExpressionOperationKind.ConditionalMemberChain =>
                    DumpExpressionKind.FixedDepthMemberChain,
                _ => throw new InvalidOperationException("The parsed expression operation is invalid."),
            };
            var methodIdentity = kind == DumpExpressionKind.CounterfactualMethod
                ? new DumpMethodExpressionIdentity(rootBinding.Name!, expression!)
                : null;
            var chainIdentity = kind == DumpExpressionKind.FixedDepthMemberChain
                ? new DumpMemberChainExpressionIdentity(expression!, descriptor)
                : null;
            var request = new DumpExpressionRequest(
                expression,
                rootBinding,
                policy,
                reachedBounds,
                languageProfile,
                kind,
                descriptor,
                syntax.AppliedBounds,
                methodIdentity,
                chainIdentity);
            return Accepted(kind, request);
        }

        var boundedRequest = new DumpExpressionRequest(
            expression,
            rootBinding,
            policy,
            reachedBounds,
            languageProfile,
            admittedKind: null,
            parsedExpression: null,
            syntax.AppliedBounds,
            methodExpressionIdentity: null,
            memberChainExpressionIdentity: null);
        var rejectionStatus = syntax.Status == CSharpExpressionAdmissionStatus.Unsupported
            ? DumpExpressionClassificationStatus.Unsupported
            : DumpExpressionClassificationStatus.Invalid;
        return Rejected(
            rejectionStatus,
            boundedRequest,
            syntax.DiagnosticCode!,
            syntax.DiagnosticMessage!);
    }

    private static DumpExpressionClassification Accepted(
        DumpExpressionKind kind,
        DumpExpressionRequest request) => new(
        DumpExpressionClassificationStatus.Accepted,
        kind,
        request,
        diagnosticCode: null,
        diagnosticMessage: null);

    private static DumpExpressionClassification Rejected(
        DumpExpressionClassificationStatus status,
        DumpExpressionRequest? request,
        string diagnosticCode,
        string diagnosticMessage) => new(
        status,
        kind: null,
        request,
        diagnosticCode,
        diagnosticMessage);

}
