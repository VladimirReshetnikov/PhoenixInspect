using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Identifies the first syntax boundary that prevented a frame-value descriptor.</summary>
/// <remarks>
/// This additive enumeration belongs only to <see cref="DumpExpressionProfileKind.FrameValueExpressionV1"/>.
/// It does not permit a stopped frame request to be reinterpreted by the static-field profile.
/// </remarks>
public enum FrameValueV1SyntaxIssue
{
    /// <summary>No issue occurred; valid only for an admitted descriptor.</summary>
    None = 0,

    /// <summary>The input reached the common expression-character cap-plus-one before parsing.</summary>
    ExpressionBoundReached = 1,

    /// <summary>The input did not form one complete expression parse.</summary>
    ParseError = 2,

    /// <summary>The complete parser reported an error diagnostic.</summary>
    ParserDiagnostic = 3,

    /// <summary>Parser spans, tokens, or tree ownership failed an integrity check.</summary>
    ParserIntegrityFailure = 4,

    /// <summary>The complete node/token walk reached its common cap-plus-one.</summary>
    NodeTokenBoundReached = 5,

    /// <summary>The complete syntax walk reached its depth cap-plus-one.</summary>
    SyntaxDepthBoundReached = 6,

    /// <summary>A decoded identifier reached the common character cap-plus-one.</summary>
    IdentifierBoundReached = 7,

    /// <summary>A decoded fallback string reached the common character cap-plus-one.</summary>
    FallbackStringBoundReached = 8,

    /// <summary>The complete parse is valid but its outer tree is outside the frame-value grammar.</summary>
    TreeShapeUnsupported = 101,

    /// <summary>The root is neither <see langword="this"/> nor one identifier.</summary>
    RootShapeUnsupported = 102,

    /// <summary>The detached suffix is outside the unchanged W2/W6 suffix shapes.</summary>
    SuffixShapeUnsupported = 103,
}

/// <summary>Classifies the only two root shapes admitted by the frame-value profile.</summary>
/// <remarks>
/// This discriminator intentionally has no static-member, literal, register, or inferred-root branch.
/// </remarks>
public enum FrameValueV1RootKind
{
    /// <summary>The exact C# <see langword="this"/> expression.</summary>
    This = 1,

    /// <summary>One decoded identifier whose physical frame attribution remains later work.</summary>
    Identifier = 2,
}

/// <summary>Records exact parser and projection counters for one frame-value syntax outcome.</summary>
/// <remarks>
/// This sealed counter set permits only saturated cap-plus-one observations. It never represents an arbitrary
/// prefix count after a boundary is crossed.
/// </remarks>
public sealed class FrameValueV1ParserCounts : IEquatable<FrameValueV1ParserCounts>
{
    private const string CanonicalDomain = "frame-value-v1-parser-counts";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private FrameValueV1ParserCounts(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength)
    {
        NodeCount = nodeCount;
        TokenCount = tokenCount;
        MaximumDepth = maximumDepth;
        MaximumDecodedIdentifierLength = maximumDecodedIdentifierLength;
        MaximumDecodedFallbackStringLength = maximumDecodedFallbackStringLength;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(nodeCount);
        writer.WriteInt32(tokenCount);
        writer.WriteInt32(maximumDepth);
        writer.WriteInt32(maximumDecodedIdentifierLength);
        writer.WriteInt32(maximumDecodedFallbackStringLength);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact syntax-node count, or the saturated count at the common boundary.</summary>
    public int NodeCount { get; }

    /// <summary>Gets the exact syntax-token count, or the saturated count at the common boundary.</summary>
    public int TokenCount { get; }

    /// <summary>Gets the greatest syntax depth reached, including a cap-plus-one observation.</summary>
    public int MaximumDepth { get; }

    /// <summary>Gets the greatest decoded identifier length, including a cap-plus-one observation.</summary>
    public int MaximumDecodedIdentifierLength { get; }

    /// <summary>Gets the greatest decoded fallback-string length, including a cap-plus-one observation.</summary>
    public int MaximumDecodedFallbackStringLength { get; }

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated exact-or-saturated parser counter set.</summary>
    /// <param name="nodeCount">The exact syntax-node count.</param>
    /// <param name="tokenCount">The exact syntax-token count.</param>
    /// <param name="maximumDepth">The greatest syntax depth reached.</param>
    /// <param name="maximumDecodedIdentifierLength">The greatest decoded identifier length reached.</param>
    /// <param name="maximumDecodedFallbackStringLength">The greatest decoded fallback-string length reached.</param>
    /// <remarks>
    /// This factory validates counter geometry only. W8.3 remains responsible for producing the counters from
    /// the sole complete Roslyn parse.
    /// </remarks>
    /// <returns>A sealed immutable counter set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A count is negative or exceeds its declared cap-plus-one observation.
    /// </exception>
    public static FrameValueV1ParserCounts Create(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nodeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
        if ((long)nodeCount + tokenCount > ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount + 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount), "The node/token observation exceeds cap-plus-one.");
        }

        ValidateSaturatedCount(
            maximumDepth,
            ExpressionV2ContractLimits.MaximumSyntaxDepth,
            nameof(maximumDepth));
        ValidateSaturatedCount(
            maximumDecodedIdentifierLength,
            ExpressionV2ContractLimits.MaximumIdentifierCharacterCount,
            nameof(maximumDecodedIdentifierLength));
        ValidateSaturatedCount(
            maximumDecodedFallbackStringLength,
            ExpressionV2ContractLimits.MaximumFallbackStringCharacterCount,
            nameof(maximumDecodedFallbackStringLength));

        return new FrameValueV1ParserCounts(
            nodeCount,
            tokenCount,
            maximumDepth,
            maximumDecodedIdentifierLength,
            maximumDecodedFallbackStringLength);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other counter set.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(FrameValueV1ParserCounts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FrameValueV1ParserCounts);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidateSaturatedCount(int value, int maximum, string parameterName)
    {
        if (value < 0 || value > maximum + 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The observation must be between zero and cap-plus-one.");
        }
    }
}

/// <summary>Freezes one admitted frame-value root and its unchanged detached suffix.</summary>
/// <remarks>
/// This sealed additive descriptor selects <see cref="DumpExpressionProfileKind.FrameValueExpressionV1"/>
/// before attribution. It carries no static-owner candidate, no physical location, and no fallback route.
/// </remarks>
public sealed class FrameValueV1ExpressionDescriptor : IEquatable<FrameValueV1ExpressionDescriptor>
{
    private const string CanonicalDomain = "frame-value-v1-expression-descriptor";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private FrameValueV1ExpressionDescriptor(
        string rawExpression,
        FrameValueV1RootKind rootKind,
        DumpExpressionIdentifier? identifier,
        DumpExpressionSuffixDescriptor suffix,
        FrameValueV1ParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        RawExpression = rawExpression;
        RootKind = rootKind;
        Identifier = identifier;
        Suffix = suffix;
        ParserCounts = parserCounts;
        this.reachedBounds = reachedBounds;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)DumpExpressionProfileKind.FrameValueExpressionV1);
        writer.WriteString(rawExpression);
        writer.WriteInt32((int)rootKind);
        writer.WriteBoolean(identifier is not null);
        if (identifier is not null)
        {
            writer.WriteLengthPrefixedBytes(identifier.CanonicalBytes.AsSpan());
        }
        writer.WriteLengthPrefixedBytes(suffix.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the explicitly selected profile.</summary>
    public DumpExpressionProfileKind Profile => DumpExpressionProfileKind.FrameValueExpressionV1;

    /// <summary>Gets the exact complete expression text retained by the sole parse.</summary>
    public string RawExpression { get; }

    /// <summary>Gets whether the root is <see langword="this"/> or an identifier.</summary>
    public FrameValueV1RootKind RootKind { get; }

    /// <summary>Gets the decoded root identifier, or <see langword="null"/> for <see langword="this"/>.</summary>
    public DumpExpressionIdentifier? Identifier { get; }

    /// <summary>Gets the unchanged detached W2/W6 suffix.</summary>
    public DumpExpressionSuffixDescriptor Suffix { get; }

    /// <summary>Gets exact parser and projection counters.</summary>
    public FrameValueV1ParserCounts ParserCounts { get; }

    /// <summary>Gets a defensive copy of exact declared bounds reached without losing completeness.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        ExpressionV2ContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete admitted frame-value syntax descriptor.</summary>
    /// <param name="rawExpression">The exact bounded non-empty expression text.</param>
    /// <param name="rootKind">The selected <see langword="this"/> or identifier root kind.</param>
    /// <param name="identifier">The decoded identifier only for an identifier root.</param>
    /// <param name="suffix">The unchanged detached suffix.</param>
    /// <param name="parserCounts">Exact parser and projection counters.</param>
    /// <param name="reachedBounds">Every exact declared syntax bound reached by the complete projection.</param>
    /// <remarks>
    /// This factory freezes detached syntax only. It neither attributes a frame symbol nor permits another
    /// expression profile when attribution later fails.
    /// </remarks>
    /// <returns>A sealed immutable descriptor.</returns>
    /// <exception cref="ArgumentException">The root, counters, suffix, or reached-bound set is inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rootKind"/> is undefined.</exception>
    public static FrameValueV1ExpressionDescriptor Create(
        string rawExpression,
        FrameValueV1RootKind rootKind,
        DumpExpressionIdentifier? identifier,
        DumpExpressionSuffixDescriptor suffix,
        FrameValueV1ParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ExpressionV2ContractEncoding.RequireText(
            rawExpression,
            nameof(rawExpression),
            ExpressionV2ContractLimits.MaximumExpressionCharacterCount);
        ExpressionV2ContractEncoding.RequireDefined(rootKind, nameof(rootKind));
        ArgumentNullException.ThrowIfNull(suffix);
        ArgumentNullException.ThrowIfNull(parserCounts);

        var rootMatches = rootKind switch
        {
            FrameValueV1RootKind.This => identifier is null,
            FrameValueV1RootKind.Identifier => identifier is not null,
            _ => false,
        };
        if (!rootMatches)
        {
            throw new ArgumentException("The root kind and identifier payload disagree.", nameof(identifier));
        }

        if (parserCounts.NodeCount <= 0 || parserCounts.TokenCount <= 0 || parserCounts.MaximumDepth <= 0 ||
            (long)parserCounts.NodeCount + parserCounts.TokenCount > ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount ||
            parserCounts.MaximumDepth > ExpressionV2ContractLimits.MaximumSyntaxDepth)
        {
            throw new ArgumentException("Parser counters do not describe one complete admitted parse.", nameof(parserCounts));
        }

        var maximumIdentifierLength = suffix.Segments
            .Select(static segment => segment.Identifier.DecodedText.Length)
            .Append(identifier?.DecodedText.Length ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        var maximumFallbackLength = suffix.StringFallback?.Length ?? 0;
        if (parserCounts.MaximumDecodedIdentifierLength != maximumIdentifierLength ||
            parserCounts.MaximumDecodedFallbackStringLength != maximumFallbackLength)
        {
            throw new ArgumentException("Parser counters do not match the detached root and suffix.", nameof(parserCounts));
        }

        var bounds = ExpressionV2ContractEncoding.NormalizeReachedBounds(reachedBounds, nameof(reachedBounds));
        var expected = ExpectedSyntaxReachedBounds(rawExpression, parserCounts);
        if (!BoundsEqual(bounds, expected))
        {
            throw new ArgumentException("Reached bounds do not match the complete detached syntax.", nameof(reachedBounds));
        }

        return new FrameValueV1ExpressionDescriptor(
            rawExpression,
            rootKind,
            identifier,
            suffix,
            parserCounts,
            bounds);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other descriptor.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(FrameValueV1ExpressionDescriptor? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FrameValueV1ExpressionDescriptor);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static ImmutableArray<EvaluationDeterministicBound> ExpectedSyntaxReachedBounds(
        string rawExpression,
        FrameValueV1ParserCounts counts)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        AddIf(
            rawExpression.Length >= ExpressionV2ContractLimits.MaximumExpressionCharacterCount,
            ExpressionV2ContractLimits.ExpressionCharacterCountBoundName);
        AddIf(
            (long)counts.NodeCount + counts.TokenCount >= ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount,
            ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName);
        AddIf(
            counts.MaximumDepth >= ExpressionV2ContractLimits.MaximumSyntaxDepth,
            ExpressionV2ContractLimits.SyntaxDepthBoundName);
        AddIf(
            counts.MaximumDecodedIdentifierLength >= ExpressionV2ContractLimits.MaximumIdentifierCharacterCount,
            ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);
        AddIf(
            counts.MaximumDecodedFallbackStringLength >= ExpressionV2ContractLimits.MaximumFallbackStringCharacterCount,
            ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName);

        return ExpressionV2ContractLimits.AllDeclaredBounds
            .Where(bound => names.Contains(bound.Name))
            .ToImmutableArray();

        void AddIf(bool condition, string name)
        {
            if (condition)
            {
                names.Add(name);
            }
        }
    }

    internal static bool BoundsEqual(
        ImmutableArray<EvaluationDeterministicBound> left,
        ImmutableArray<EvaluationDeterministicBound> right) =>
        left.Length == right.Length && left.Zip(right).All(static pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal) &&
            pair.First.Value == pair.Second.Value);
}

/// <summary>Freezes an admitted frame descriptor or one typed invalid/unsupported syntax stop.</summary>
/// <remarks>
/// This sealed outcome never carries a prefix descriptor after cap-plus-one and never redirects a stopped
/// frame request to static binding.
/// </remarks>
public sealed class FrameValueV1SyntaxOutcome : IEquatable<FrameValueV1SyntaxOutcome>
{
    private const string CanonicalDomain = "frame-value-v1-syntax-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpExpressionDiagnostic> diagnostics;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private FrameValueV1SyntaxOutcome(
        DumpExpressionSyntaxStatus status,
        FrameValueV1SyntaxIssue issue,
        string rawExpression,
        FrameValueV1ParserCounts parserCounts,
        FrameValueV1ExpressionDescriptor? descriptor,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        Status = status;
        Issue = issue;
        RawExpression = rawExpression;
        ParserCounts = parserCounts;
        Descriptor = descriptor;
        this.diagnostics = diagnostics;
        this.reachedBounds = reachedBounds;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)DumpExpressionProfileKind.FrameValueExpressionV1);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteString(rawExpression);
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        writer.WriteBoolean(descriptor is not null);
        if (descriptor is not null)
        {
            writer.WriteLengthPrefixedBytes(descriptor.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            diagnostics,
            static diagnostic => diagnostic.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the explicitly selected profile.</summary>
    public DumpExpressionProfileKind Profile => DumpExpressionProfileKind.FrameValueExpressionV1;

    /// <summary>Gets the admitted, invalid, or unsupported disposition.</summary>
    public DumpExpressionSyntaxStatus Status { get; }

    /// <summary>Gets the first typed syntax issue.</summary>
    public FrameValueV1SyntaxIssue Issue { get; }

    /// <summary>Gets the exact retained input text.</summary>
    public string RawExpression { get; }

    /// <summary>Gets exact parser and projection counters.</summary>
    public FrameValueV1ParserCounts ParserCounts { get; }

    /// <summary>Gets the complete descriptor only for an admitted outcome.</summary>
    public FrameValueV1ExpressionDescriptor? Descriptor { get; }

    /// <summary>Gets a defensive copy of stable structured diagnostics.</summary>
    public ImmutableArray<DumpExpressionDiagnostic> Diagnostics =>
        ExpressionV2ContractEncoding.Copy(diagnostics);

    /// <summary>Gets a defensive copy of exact reached bounds.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds =>
        ExpressionV2ContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one admitted frame-value syntax outcome.</summary>
    /// <param name="descriptor">The complete admitted frame descriptor.</param>
    /// <param name="diagnostics">Optional non-error Parse or Projection diagnostics.</param>
    /// <remarks>This factory preserves the already-selected frame profile and creates no fallback route.</remarks>
    /// <returns>A sealed immutable admitted outcome.</returns>
    /// <exception cref="ArgumentException">A diagnostic is an error or belongs to another stage.</exception>
    public static FrameValueV1SyntaxOutcome Admitted(
        FrameValueV1ExpressionDescriptor descriptor,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var copied = CopySyntaxDiagnostics(diagnostics, nameof(diagnostics));
        if (copied.Any(static diagnostic => diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error))
        {
            throw new ArgumentException("An admitted syntax outcome cannot carry an error diagnostic.", nameof(diagnostics));
        }

        return new FrameValueV1SyntaxOutcome(
            DumpExpressionSyntaxStatus.Admitted,
            FrameValueV1SyntaxIssue.None,
            descriptor.RawExpression,
            descriptor.ParserCounts,
            descriptor,
            copied,
            descriptor.ReachedBounds);
    }

    /// <summary>Creates one typed invalid frame-value syntax outcome.</summary>
    /// <param name="rawExpression">The exact retained input, bounded at cap-plus-one.</param>
    /// <param name="issue">One invalid-category issue.</param>
    /// <param name="parserCounts">Exact-or-saturated counters reached before the stop.</param>
    /// <param name="diagnostics">Stable diagnostics including a Parse-stage error.</param>
    /// <param name="reachedBounds">Every exact declared bound reached before the stop.</param>
    /// <remarks>This stop cannot trigger the static profile.</remarks>
    /// <returns>A sealed immutable invalid outcome with no prefix descriptor.</returns>
    public static FrameValueV1SyntaxOutcome Invalid(
        string rawExpression,
        FrameValueV1SyntaxIssue issue,
        FrameValueV1ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateStopped(
            DumpExpressionSyntaxStatus.Invalid,
            rawExpression,
            issue,
            parserCounts,
            diagnostics,
            reachedBounds);

    /// <summary>Creates one typed unsupported frame-value syntax outcome.</summary>
    /// <param name="rawExpression">The exact retained input, bounded at cap-plus-one.</param>
    /// <param name="issue">One unsupported-category issue.</param>
    /// <param name="parserCounts">Exact-or-saturated counters reached before the stop.</param>
    /// <param name="diagnostics">Stable diagnostics including a Projection-stage error.</param>
    /// <param name="reachedBounds">Every exact declared bound reached before the stop.</param>
    /// <remarks>This stop cannot trigger the static profile.</remarks>
    /// <returns>A sealed immutable unsupported outcome with no prefix descriptor.</returns>
    public static FrameValueV1SyntaxOutcome Unsupported(
        string rawExpression,
        FrameValueV1SyntaxIssue issue,
        FrameValueV1ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateStopped(
            DumpExpressionSyntaxStatus.Unsupported,
            rawExpression,
            issue,
            parserCounts,
            diagnostics,
            reachedBounds);

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(FrameValueV1SyntaxOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FrameValueV1SyntaxOutcome);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static FrameValueV1SyntaxOutcome CreateStopped(
        DumpExpressionSyntaxStatus status,
        string rawExpression,
        FrameValueV1SyntaxIssue issue,
        FrameValueV1ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ExpressionV2ContractEncoding.RequireText(
            rawExpression,
            nameof(rawExpression),
            ExpressionV2ContractLimits.MaximumExpressionCharacterCount + 1,
            allowEmpty: true);
        ExpressionV2ContractEncoding.RequireDefined(issue, nameof(issue));
        ArgumentNullException.ThrowIfNull(parserCounts);

        var issueMatches = status switch
        {
            DumpExpressionSyntaxStatus.Invalid => (int)issue is >= 1 and < 100,
            DumpExpressionSyntaxStatus.Unsupported => (int)issue >= 100,
            _ => false,
        };
        if (!issueMatches)
        {
            throw new ArgumentException("The issue does not belong to the selected syntax disposition.", nameof(issue));
        }

        var copiedDiagnostics = CopySyntaxDiagnostics(diagnostics, nameof(diagnostics));
        var errorStage = status == DumpExpressionSyntaxStatus.Invalid
            ? DumpExpressionDiagnosticStage.Parse
            : DumpExpressionDiagnosticStage.Projection;
        if (!copiedDiagnostics.Any(diagnostic =>
                diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error &&
                diagnostic.Stage == errorStage))
        {
            throw new ArgumentException("A stopped outcome requires an error at its syntax stage.", nameof(diagnostics));
        }

        var bounds = ExpressionV2ContractEncoding.NormalizeReachedBounds(reachedBounds, nameof(reachedBounds));
        var expectedBounds = FrameValueV1ExpressionDescriptor.ExpectedSyntaxReachedBounds(rawExpression, parserCounts);
        if (!FrameValueV1ExpressionDescriptor.BoundsEqual(bounds, expectedBounds))
        {
            throw new ArgumentException("Reached bounds do not match the stopped syntax counters.", nameof(reachedBounds));
        }

        ValidateFirstBoundary(issue, rawExpression, parserCounts);
        return new FrameValueV1SyntaxOutcome(
            status,
            issue,
            rawExpression,
            parserCounts,
            descriptor: null,
            copiedDiagnostics,
            bounds);
    }

    private static ImmutableArray<DumpExpressionDiagnostic> CopySyntaxDiagnostics(
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        string parameterName)
    {
        var copied = ExpressionV2ContractEncoding.CopyRequired(diagnostics, parameterName, maximumCount: 256);
        if (copied.Any(static diagnostic =>
                diagnostic.Stage is not DumpExpressionDiagnosticStage.Parse and
                    not DumpExpressionDiagnosticStage.Projection))
        {
            throw new ArgumentException("Frame syntax diagnostics must belong to Parse or Projection.", parameterName);
        }
        return copied;
    }

    private static void ValidateFirstBoundary(
        FrameValueV1SyntaxIssue issue,
        string rawExpression,
        FrameValueV1ParserCounts counts)
    {
        var first = rawExpression.Length > ExpressionV2ContractLimits.MaximumExpressionCharacterCount
            ? FrameValueV1SyntaxIssue.ExpressionBoundReached
            : (long)counts.NodeCount + counts.TokenCount > ExpressionV2ContractLimits.MaximumSyntaxNodeTokenCount
                ? FrameValueV1SyntaxIssue.NodeTokenBoundReached
                : counts.MaximumDepth > ExpressionV2ContractLimits.MaximumSyntaxDepth
                    ? FrameValueV1SyntaxIssue.SyntaxDepthBoundReached
                    : counts.MaximumDecodedIdentifierLength > ExpressionV2ContractLimits.MaximumIdentifierCharacterCount
                        ? FrameValueV1SyntaxIssue.IdentifierBoundReached
                        : counts.MaximumDecodedFallbackStringLength > ExpressionV2ContractLimits.MaximumFallbackStringCharacterCount
                            ? FrameValueV1SyntaxIssue.FallbackStringBoundReached
                            : FrameValueV1SyntaxIssue.None;

        if (first != FrameValueV1SyntaxIssue.None && first != issue)
        {
            throw new ArgumentException("The selected issue occurs after an earlier cap-plus-one boundary.", nameof(issue));
        }
    }
}
