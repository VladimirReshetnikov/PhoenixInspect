using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the terminal disposition of one W7 static-field syntax projection.</summary>
/// <remarks>
/// The explicit numeric values are part of the version-one canonical encoding. New values must therefore be appended
/// without renumbering existing tags.
/// </remarks>
public enum StaticFieldSyntaxStatus
{
    /// <summary>The complete parse was admitted and produced one detached project descriptor.</summary>
    Accepted = 1,

    /// <summary>The expression or complete parse violated a syntax-integrity requirement.</summary>
    Invalid = 2,

    /// <summary>The expression is valid C# but its tree or suffix is outside the enabled version-one profile.</summary>
    Unsupported = 3,
}

/// <summary>Identifies the first syntax boundary that prevented a W7 static-field descriptor.</summary>
/// <remarks>
/// Values are partitioned by <see cref="StaticFieldSyntaxStatus"/>. Factories reject an issue belonging to a
/// different disposition so an otherwise valid enum value cannot describe an impossible outcome.
/// </remarks>
public enum StaticFieldSyntaxIssue
{
    /// <summary>No syntax issue occurred; valid only for an accepted descriptor.</summary>
    None = 0,

    /// <summary>The expression text was empty or otherwise could not form a complete expression parse.</summary>
    ParseError = 1,

    /// <summary>The complete parser result contained diagnostics that make the expression invalid.</summary>
    ParserDiagnostic = 2,

    /// <summary>The parser tree, token stream, or source spans failed a project integrity check.</summary>
    ParserIntegrityFailure = 3,

    /// <summary>A syntax traversal reached a declared deterministic limit before classification completed.</summary>
    SyntaxBoundReached = 4,

    /// <summary>The complete parse tree is valid but is not one of the version-one projected access trees.</summary>
    TreeShapeUnsupported = 101,

    /// <summary>A separator or member-access topology is valid C# but outside the projected version-one topology.</summary>
    AccessTopologyUnsupported = 102,

    /// <summary>The instance suffix is valid C# but is not an admitted direct or fixed-depth member suffix.</summary>
    SuffixShapeUnsupported = 103,

    /// <summary>The fallback expression is valid C# but is not an admitted null, Int32, or string literal.</summary>
    FallbackShapeUnsupported = 104,
}

/// <summary>Identifies the sole expression profile represented by the W7 syntax contracts.</summary>
public enum StaticFieldExpressionProfile
{
    /// <summary>The opt-in, bounded static-field expression profile introduced by W7.</summary>
    StaticFieldExpressionV1 = 1,
}

/// <summary>Identifies the exact separator immediately preceding a projected identifier segment.</summary>
public enum StaticFieldSegmentSeparatorKind
{
    /// <summary>The first segment has no explicit qualifier or separator.</summary>
    None = 0,

    /// <summary>The first segment is preceded by the literal <c>global::</c> alias qualifier.</summary>
    GlobalAliasQualifier = 1,

    /// <summary>The segment is reached through ordinary dot member access.</summary>
    Dot = 2,

    /// <summary>The segment is reached through conditional member access.</summary>
    ConditionalDot = 3,
}

/// <summary>Describes the access-tree role encoded by a projected identifier segment.</summary>
public enum StaticFieldSegmentAccessKind
{
    /// <summary>The segment is the root of the projected identifier access tree.</summary>
    Root = 1,

    /// <summary>The segment follows an ordinary direct member-access edge.</summary>
    DirectMember = 2,

    /// <summary>The segment follows a conditional member-access edge.</summary>
    ConditionalMember = 3,
}

/// <summary>Identifies the admitted root-relative suffix associated with one possible static-field split.</summary>
public enum StaticFieldSuffixShape
{
    /// <summary>The static field is terminal and has no instance-member suffix.</summary>
    None = 0,

    /// <summary>Exactly one admitted instance member follows the static field.</summary>
    DirectMember = 1,

    /// <summary>Exactly two admitted member hops follow the static field.</summary>
    FixedDepthMemberChain = 2,
}

/// <summary>Identifies the optional literal fallback associated with one possible static-field split.</summary>
public enum StaticFieldFallbackKind
{
    /// <summary>No coalescing fallback is present.</summary>
    None = 0,

    /// <summary>The suffix coalesces to the null literal.</summary>
    Null = 1,

    /// <summary>The suffix coalesces to one exact <see cref="int"/> literal.</summary>
    Int32 = 2,

    /// <summary>The suffix coalesces to one decoded string literal.</summary>
    String = 3,
}

/// <summary>Freezes one raw and decoded identifier plus its exact access-tree edge.</summary>
/// <remarks>
/// This value deliberately keeps source spelling separate from decoded identifier value. It contains no
/// compiler syntax node, source file, or semantic symbol and can therefore survive disposal of the parser tree.
/// </remarks>
public sealed class StaticFieldAccessSegment : IEquatable<StaticFieldAccessSegment>
{
    private const string CanonicalDomain = "static-field-access-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldAccessSegment(
        string rawText,
        string decodedIdentifier,
        StaticFieldSegmentSeparatorKind separatorKind,
        StaticFieldSegmentAccessKind accessKind)
    {
        RawText = rawText;
        DecodedIdentifier = decodedIdentifier;
        SeparatorKind = separatorKind;
        AccessKind = accessKind;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(rawText);
        writer.WriteString(decodedIdentifier);
        writer.WriteInt32((int)separatorKind);
        writer.WriteInt32((int)accessKind);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact identifier spelling copied from the expression, excluding its separator.</summary>
    public string RawText { get; }

    /// <summary>Gets the parser-decoded identifier value, including decoding of escapes and verbatim identifiers.</summary>
    public string DecodedIdentifier { get; }

    /// <summary>Gets the exact qualifier or separator that immediately preceded this segment.</summary>
    public StaticFieldSegmentSeparatorKind SeparatorKind { get; }

    /// <summary>Gets whether this segment is the tree root, a direct member, or a conditional member.</summary>
    public StaticFieldSegmentAccessKind AccessKind { get; }

    /// <summary>Gets a defensive copy of the versioned canonical content bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated detached identifier segment.</summary>
    /// <param name="rawText">The non-empty exact identifier spelling, without the preceding separator.</param>
    /// <param name="decodedIdentifier">The non-empty identifier value decoded by the complete parser.</param>
    /// <param name="separatorKind">The exact separator or global qualifier preceding the identifier.</param>
    /// <param name="accessKind">The corresponding root, direct, or conditional access-tree role.</param>
    /// <returns>An immutable, content-equal segment suitable for a syntax descriptor.</returns>
    /// <exception cref="ArgumentException">Either text is empty or the separator/access pairing is contradictory.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is not defined by version one.</exception>
    public static StaticFieldAccessSegment Create(
        string rawText,
        string decodedIdentifier,
        StaticFieldSegmentSeparatorKind separatorKind,
        StaticFieldSegmentAccessKind accessKind)
    {
        StaticFieldContractEncoding.RequireNonEmptyText(rawText, nameof(rawText));
        StaticFieldContractEncoding.RequireIdentifierValue(decodedIdentifier, nameof(decodedIdentifier), allowDots: false);
        if (rawText.Length > CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            throw new ArgumentException("The raw identifier spelling exceeds the complete expression length limit.", nameof(rawText));
        }
        if (decodedIdentifier.Length > CSharpExpressionFrontEnd.MaximumIdentifierLength)
        {
            throw new ArgumentException("The decoded identifier exceeds the pinned front-end limit.", nameof(decodedIdentifier));
        }
        StaticFieldContractEncoding.RequireDefined(separatorKind, nameof(separatorKind));
        StaticFieldContractEncoding.RequireDefined(accessKind, nameof(accessKind));

        var validTopology = (separatorKind, accessKind) switch
        {
            (StaticFieldSegmentSeparatorKind.None or StaticFieldSegmentSeparatorKind.GlobalAliasQualifier,
                StaticFieldSegmentAccessKind.Root) => true,
            (StaticFieldSegmentSeparatorKind.Dot, StaticFieldSegmentAccessKind.DirectMember) => true,
            (StaticFieldSegmentSeparatorKind.ConditionalDot, StaticFieldSegmentAccessKind.ConditionalMember) => true,
            _ => false,
        };
        if (!validTopology)
        {
            throw new ArgumentException("The separator and access-tree role describe different topologies.");
        }

        return new StaticFieldAccessSegment(rawText, decodedIdentifier, separatorKind, accessKind);
    }

    /// <summary>Determines whether another segment has the same versioned canonical content.</summary>
    /// <param name="other">The segment to compare.</param>
    /// <returns><see langword="true"/> only when every canonical field is equal.</returns>
    public bool Equals(StaticFieldAccessSegment? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldAccessSegment);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Records exact parser traversal counters copied into a detached syntax outcome.</summary>
public sealed class StaticFieldParserCounts : IEquatable<StaticFieldParserCounts>
{
    private const string CanonicalDomain = "static-field-parser-counts";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldParserCounts(
        int nodeCount,
        int tokenCount,
        int maximumDepthReached,
        int projectedSegmentCount,
        int projectedCandidateShapeCount)
    {
        NodeCount = nodeCount;
        TokenCount = tokenCount;
        MaximumDepthReached = maximumDepthReached;
        ProjectedSegmentCount = projectedSegmentCount;
        ProjectedCandidateShapeCount = projectedCandidateShapeCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(nodeCount);
        writer.WriteInt32(tokenCount);
        writer.WriteInt32(maximumDepthReached);
        writer.WriteInt32(projectedSegmentCount);
        writer.WriteInt32(projectedCandidateShapeCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact number of syntax nodes observed by the bounded complete-tree walk.</summary>
    public int NodeCount { get; }

    /// <summary>Gets the exact number of syntax tokens observed by the bounded complete-tree walk.</summary>
    public int TokenCount { get; }

    /// <summary>Gets the greatest syntax-tree depth reached, with the expression root counted as depth one.</summary>
    public int MaximumDepthReached { get; }

    /// <summary>Gets the exact number of detached identifier segments projected from the tree.</summary>
    public int ProjectedSegmentCount { get; }

    /// <summary>Gets the exact number of possible static-field/suffix shapes retained by the descriptor.</summary>
    public int ProjectedCandidateShapeCount { get; }

    /// <summary>Gets a defensive copy of the versioned canonical content bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a complete non-negative parser counter set.</summary>
    /// <param name="nodeCount">The number of syntax nodes visited.</param>
    /// <param name="tokenCount">The number of syntax tokens visited.</param>
    /// <param name="maximumDepthReached">The greatest tree depth reached.</param>
    /// <param name="projectedSegmentCount">The number of detached identifier segments produced.</param>
    /// <param name="projectedCandidateShapeCount">The number of retained static-field split shapes.</param>
    /// <returns>An immutable parser counter value; all-zero counts are valid for an early invalid parse.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any count is negative.</exception>
    public static StaticFieldParserCounts Create(
        int nodeCount,
        int tokenCount,
        int maximumDepthReached,
        int projectedSegmentCount,
        int projectedCandidateShapeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nodeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepthReached);
        ArgumentOutOfRangeException.ThrowIfNegative(projectedSegmentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(projectedCandidateShapeCount);
        return new StaticFieldParserCounts(
            nodeCount,
            tokenCount,
            maximumDepthReached,
            projectedSegmentCount,
            projectedCandidateShapeCount);
    }

    /// <summary>Determines whether another counter set has the same versioned canonical content.</summary>
    /// <param name="other">The counter set to compare.</param>
    /// <returns><see langword="true"/> only when every exact count is equal.</returns>
    public bool Equals(StaticFieldParserCounts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldParserCounts);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Describes one bounded interpretation of where static binding ends and instance suffix evaluation begins.</summary>
public sealed class StaticFieldCandidateShape : IEquatable<StaticFieldCandidateShape>
{
    private const string CanonicalDomain = "static-field-candidate-shape";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldCandidateShape(
        int staticFieldSegmentIndex,
        StaticFieldSuffixShape suffixShape,
        StaticFieldFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback)
    {
        StaticFieldSegmentIndex = staticFieldSegmentIndex;
        SuffixShape = suffixShape;
        FallbackKind = fallbackKind;
        Int32Fallback = int32Fallback;
        StringFallback = stringFallback;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(staticFieldSegmentIndex);
        writer.WriteInt32((int)suffixShape);
        writer.WriteInt32((int)fallbackKind);
        writer.WriteBoolean(int32Fallback.HasValue);
        if (int32Fallback.HasValue)
        {
            writer.WriteInt32(int32Fallback.Value);
        }

        writer.WriteBoolean(stringFallback is not null);
        if (stringFallback is not null)
        {
            writer.WriteString(stringFallback);
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the zero-based segment index interpreted as the directly declared static field name.</summary>
    public int StaticFieldSegmentIndex { get; }

    /// <summary>Gets the admitted instance suffix following the static field for this possible split.</summary>
    public StaticFieldSuffixShape SuffixShape { get; }

    /// <summary>Gets the optional typed literal fallback associated with the suffix.</summary>
    public StaticFieldFallbackKind FallbackKind { get; }

    /// <summary>Gets the exact integer fallback only when <see cref="FallbackKind"/> is <see cref="StaticFieldFallbackKind.Int32"/>.</summary>
    public int? Int32Fallback { get; }

    /// <summary>Gets the decoded string fallback only when <see cref="FallbackKind"/> is <see cref="StaticFieldFallbackKind.String"/>.</summary>
    public string? StringFallback { get; }

    /// <summary>Gets a defensive copy of the versioned canonical content bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated possible static-field/suffix split.</summary>
    /// <param name="staticFieldSegmentIndex">The zero-based field segment index; at least one type segment must precede it.</param>
    /// <param name="suffixShape">The admitted zero-, one-, or two-segment instance suffix shape.</param>
    /// <param name="fallbackKind">The optional typed literal fallback discriminator.</param>
    /// <param name="int32Fallback">The exact integer payload required only by an Int32 fallback.</param>
    /// <param name="stringFallback">The decoded string payload required only by a string fallback.</param>
    /// <returns>An immutable candidate shape whose segment extent is validated by its containing descriptor.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is below one or an enum tag is undefined.</exception>
    /// <exception cref="ArgumentException">The suffix and fallback payloads contradict their discriminators.</exception>
    public static StaticFieldCandidateShape Create(
        int staticFieldSegmentIndex,
        StaticFieldSuffixShape suffixShape,
        StaticFieldFallbackKind fallbackKind = StaticFieldFallbackKind.None,
        int? int32Fallback = null,
        string? stringFallback = null)
    {
        if (staticFieldSegmentIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(staticFieldSegmentIndex),
                "At least one type-name segment must precede a static field.");
        }

        StaticFieldContractEncoding.RequireDefined(suffixShape, nameof(suffixShape));
        StaticFieldContractEncoding.RequireDefined(fallbackKind, nameof(fallbackKind));
        var payloadIsValid = fallbackKind switch
        {
            StaticFieldFallbackKind.None or StaticFieldFallbackKind.Null =>
                int32Fallback is null && stringFallback is null,
            StaticFieldFallbackKind.Int32 => int32Fallback.HasValue && stringFallback is null,
            StaticFieldFallbackKind.String => int32Fallback is null && stringFallback is not null,
            _ => false,
        };
        if (!payloadIsValid)
        {
            throw new ArgumentException("The fallback discriminator and payload disagree.");
        }

        if (suffixShape == StaticFieldSuffixShape.None && fallbackKind != StaticFieldFallbackKind.None)
        {
            throw new ArgumentException("A terminal static field cannot carry an instance-suffix fallback.");
        }

        if (stringFallback?.Length > CSharpExpressionFrontEnd.MaximumStringLiteralLength)
        {
            throw new ArgumentException("The decoded string fallback exceeds the pinned front-end limit.", nameof(stringFallback));
        }

        return new StaticFieldCandidateShape(
            staticFieldSegmentIndex,
            suffixShape,
            fallbackKind,
            int32Fallback,
            stringFallback);
    }

    /// <summary>Determines whether another shape has the same versioned canonical content.</summary>
    /// <param name="other">The candidate shape to compare.</param>
    /// <returns><see langword="true"/> only when split, suffix, and fallback are equal.</returns>
    public bool Equals(StaticFieldCandidateShape? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldCandidateShape);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete admitted W7 syntax identity without retaining a compiler object.</summary>
/// <remarks>
/// The descriptor records every bounded static-field/suffix split admitted by tree shape. It does not select a type,
/// field, module, or value; later binding must consider every retained shape and require one distinct declaration.
/// </remarks>
public sealed class StaticFieldExpressionDescriptor : IEquatable<StaticFieldExpressionDescriptor>
{
    /// <summary>Gets the stable textual identifier of the sole represented expression profile.</summary>
    public const string ProfileId = "StaticFieldExpressionV1";

    private const string CanonicalDomain = "static-field-expression-descriptor";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldAccessSegment> segments;
    private readonly ImmutableArray<StaticFieldCandidateShape> candidateShapes;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldExpressionDescriptor(
        string rawExpression,
        bool hasGlobalQualifier,
        ImmutableArray<StaticFieldAccessSegment> segments,
        ImmutableArray<StaticFieldCandidateShape> candidateShapes,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        RawExpression = rawExpression;
        HasGlobalQualifier = hasGlobalQualifier;
        this.segments = segments;
        this.candidateShapes = candidateShapes;
        ParserCounts = parserCounts;
        this.reachedBounds = reachedBounds;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(rawExpression);
        writer.WriteInt32((int)Profile);
        writer.WriteString(ProfileId);
        writer.WriteBoolean(hasGlobalQualifier);
        StaticFieldContractEncoding.WriteValues(writer, segments);
        StaticFieldContractEncoding.WriteValues(writer, candidateShapes);
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        StaticFieldContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact expression text, including trivia and escaped identifier spellings.</summary>
    public string RawExpression { get; }

    /// <summary>Gets the explicit opt-in profile identity; this descriptor can never represent a legacy profile.</summary>
    public StaticFieldExpressionProfile Profile => StaticFieldExpressionProfile.StaticFieldExpressionV1;

    /// <summary>Gets whether the access tree begins with the literal <c>global::</c> qualifier.</summary>
    public bool HasGlobalQualifier { get; }

    /// <summary>Gets a defensive array copy of the expression-order raw/decoded identifier segments.</summary>
    public ImmutableArray<StaticFieldAccessSegment> Segments => StaticFieldContractEncoding.Copy(segments);

    /// <summary>Gets a canonical-order defensive copy of every bounded static-field/suffix split.</summary>
    public ImmutableArray<StaticFieldCandidateShape> CandidateShapes => StaticFieldContractEncoding.Copy(candidateShapes);

    /// <summary>Gets the exact complete-parser and project-projection counters.</summary>
    public StaticFieldParserCounts ParserCounts { get; }

    /// <summary>Gets an ordinal-name-ordered defensive copy of only the deterministic bounds reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => StaticFieldContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned canonical content bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete, admitted, compiler-detached syntax descriptor.</summary>
    /// <param name="rawExpression">The exact non-empty expression text supplied to the complete parser.</param>
    /// <param name="hasGlobalQualifier">Whether the first segment is preceded by literal <c>global::</c>.</param>
    /// <param name="segments">The initialized, non-empty expression-order segment array.</param>
    /// <param name="candidateShapes">The initialized, non-empty set of bounded static-field/suffix splits.</param>
    /// <param name="parserCounts">Complete parser/projection counters agreeing with both arrays.</param>
    /// <param name="reachedBounds">An explicitly initialized array of reached deterministic bounds.</param>
    /// <returns>An immutable descriptor with deterministic shape order and canonical identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parserCounts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Text or arrays are missing, counts disagree, global/topology facts conflict, a shape exceeds the segment tree,
    /// or bounds/shapes are duplicated.
    /// </exception>
    public static StaticFieldExpressionDescriptor Create(
        string rawExpression,
        bool hasGlobalQualifier,
        ImmutableArray<StaticFieldAccessSegment> segments,
        ImmutableArray<StaticFieldCandidateShape> candidateShapes,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        StaticFieldContractEncoding.RequireNonEmptyText(rawExpression, nameof(rawExpression));
        if (rawExpression.Length > CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            throw new ArgumentException("The accepted expression exceeds the pinned front-end length limit.", nameof(rawExpression));
        }
        ArgumentNullException.ThrowIfNull(parserCounts);
        var copiedSegments = StaticFieldContractEncoding.RequireAndCopyNonEmpty(segments, nameof(segments));
        var normalizedShapes = StaticFieldContractEncoding.NormalizeAndDeduplicate(
            candidateShapes,
            nameof(candidateShapes),
            static shape => shape.CanonicalBytes);
        if (normalizedShapes.IsEmpty)
        {
            throw new ArgumentException("An explicitly initialized, non-empty candidate-shape array is required.", nameof(candidateShapes));
        }
        if (normalizedShapes.Select(static shape => shape.StaticFieldSegmentIndex).Distinct().Count() != normalizedShapes.Length)
        {
            throw new ArgumentException(
                "One parsed tree can retain at most one suffix interpretation for each static-field split index.",
                nameof(candidateShapes));
        }
        var sharedFallback = normalizedShapes[0];
        if (normalizedShapes.Any(shape =>
                shape.FallbackKind != sharedFallback.FallbackKind ||
                shape.Int32Fallback != sharedFallback.Int32Fallback ||
                !string.Equals(shape.StringFallback, sharedFallback.StringFallback, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every candidate split from one parsed tree must share one identical coalesce fallback, including None.",
                nameof(candidateShapes));
        }
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));

        var expectedFirstSeparator = hasGlobalQualifier
            ? StaticFieldSegmentSeparatorKind.GlobalAliasQualifier
            : StaticFieldSegmentSeparatorKind.None;
        if (copiedSegments[0].SeparatorKind != expectedFirstSeparator ||
            copiedSegments[0].AccessKind != StaticFieldSegmentAccessKind.Root ||
            copiedSegments.Skip(1).Any(static segment => segment.AccessKind == StaticFieldSegmentAccessKind.Root))
        {
            throw new ArgumentException("The global qualifier and root/member segment topology disagree.", nameof(segments));
        }

        if (parserCounts.NodeCount == 0 || parserCounts.TokenCount == 0 || parserCounts.MaximumDepthReached == 0 ||
            (long)parserCounts.NodeCount + parserCounts.TokenCount > CSharpExpressionFrontEnd.MaximumNodeTokenCount ||
            parserCounts.MaximumDepthReached > CSharpExpressionFrontEnd.MaximumSyntaxDepth ||
            parserCounts.ProjectedSegmentCount != copiedSegments.Length ||
            parserCounts.ProjectedCandidateShapeCount != normalizedShapes.Length)
        {
            throw new ArgumentException("Accepted parser counts must be positive and agree with projected arrays.", nameof(parserCounts));
        }

        foreach (var shape in normalizedShapes)
        {
            var suffixLength = shape.SuffixShape switch
            {
                StaticFieldSuffixShape.None => 0,
                StaticFieldSuffixShape.DirectMember => 1,
                StaticFieldSuffixShape.FixedDepthMemberChain => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(candidateShapes)),
            };
            if (shape.StaticFieldSegmentIndex + suffixLength != copiedSegments.Length - 1)
            {
                throw new ArgumentException(
                    "A candidate shape must consume the complete segment tree with its declared suffix extent.",
                    nameof(candidateShapes));
            }

            if (copiedSegments.Take(shape.StaticFieldSegmentIndex + 1)
                .Skip(1)
                .Any(static segment => segment.AccessKind != StaticFieldSegmentAccessKind.DirectMember))
            {
                throw new ArgumentException(
                    "Type qualification and static-field access must use ordinary dot edges.",
                    nameof(segments));
            }

            var suffix = copiedSegments.Skip(shape.StaticFieldSegmentIndex + 1).ToArray();
            var suffixTopologyIsAdmitted = shape.SuffixShape switch
            {
                StaticFieldSuffixShape.None => suffix.Length == 0,
                StaticFieldSuffixShape.DirectMember =>
                    suffix is [{ AccessKind: StaticFieldSegmentAccessKind.DirectMember }],
                StaticFieldSuffixShape.FixedDepthMemberChain =>
                    suffix is
                    [
                        { AccessKind: StaticFieldSegmentAccessKind.DirectMember },
                        {
                            AccessKind: StaticFieldSegmentAccessKind.DirectMember or
                                StaticFieldSegmentAccessKind.ConditionalMember,
                        },
                    ],
                _ => false,
            };
            if (!suffixTopologyIsAdmitted)
            {
                throw new ArgumentException(
                    "The suffix edges do not reproduce an admitted W2 direct member or W6 fixed-depth chain.",
                    nameof(segments));
            }
        }

        return new StaticFieldExpressionDescriptor(
            rawExpression,
            hasGlobalQualifier,
            copiedSegments,
            normalizedShapes,
            parserCounts,
            normalizedBounds);
    }

    /// <summary>Determines whether another descriptor has identical versioned syntax content.</summary>
    /// <param name="other">The descriptor to compare.</param>
    /// <returns><see langword="true"/> only when raw spelling, decoded tree, shapes, counts, and bounds match.</returns>
    public bool Equals(StaticFieldExpressionDescriptor? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldExpressionDescriptor);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Carries one accepted, invalid, or unsupported W7 syntax outcome as a strict immutable union.</summary>
/// <remarks>
/// Only <see cref="StaticFieldSyntaxStatus.Accepted"/> carries a descriptor. Rejections retain raw text, complete
/// counters, reached bounds, and a stable diagnostic without leaking a partial descriptor or compiler tree.
/// </remarks>
public sealed class StaticFieldSyntaxOutcome : IEquatable<StaticFieldSyntaxOutcome>
{
    private const string CanonicalDomain = "static-field-syntax-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldSyntaxOutcome(
        string rawExpression,
        StaticFieldSyntaxStatus status,
        StaticFieldSyntaxIssue issue,
        StaticFieldExpressionDescriptor? descriptor,
        string? diagnosticCode,
        string? diagnosticMessage,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        RawExpression = rawExpression;
        Status = status;
        Issue = issue;
        Descriptor = descriptor;
        DiagnosticCode = diagnosticCode;
        DiagnosticMessage = diagnosticMessage;
        ParserCounts = parserCounts;
        this.reachedBounds = reachedBounds;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(rawExpression);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteBoolean(descriptor is not null);
        if (descriptor is not null)
        {
            writer.WriteLengthPrefixedBytes(descriptor.CanonicalBytes.AsSpan());
        }

        StaticFieldContractEncoding.WriteOptionalString(writer, diagnosticCode);
        StaticFieldContractEncoding.WriteOptionalString(writer, diagnosticMessage);
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        StaticFieldContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact raw expression supplied for classification, including rejected text.</summary>
    public string RawExpression { get; }

    /// <summary>Gets whether classification accepted, invalidated, or declined the complete parse tree.</summary>
    public StaticFieldSyntaxStatus Status { get; }

    /// <summary>Gets the source-specific syntax issue, or <see cref="StaticFieldSyntaxIssue.None"/> on acceptance.</summary>
    public StaticFieldSyntaxIssue Issue { get; }

    /// <summary>Gets the complete descriptor only for an accepted outcome; otherwise gets <see langword="null"/>.</summary>
    public StaticFieldExpressionDescriptor? Descriptor { get; }

    /// <summary>Gets the stable artifact-independent diagnostic code only for a rejected outcome.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets the stable artifact-independent diagnostic explanation only for a rejected outcome.</summary>
    public string? DiagnosticMessage { get; }

    /// <summary>Gets the complete exact parser/projection counters reached before disposition.</summary>
    public StaticFieldParserCounts ParserCounts { get; }

    /// <summary>Gets an ordinal-name-ordered defensive copy of only deterministic bounds actually reached.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => StaticFieldContractEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the versioned canonical union bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the sole accepted union case from one complete descriptor.</summary>
    /// <param name="descriptor">The complete admitted static-field syntax descriptor.</param>
    /// <returns>An accepted outcome with no issue or diagnostic payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public static StaticFieldSyntaxOutcome Accepted(StaticFieldExpressionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new StaticFieldSyntaxOutcome(
            descriptor.RawExpression,
            StaticFieldSyntaxStatus.Accepted,
            StaticFieldSyntaxIssue.None,
            descriptor,
            diagnosticCode: null,
            diagnosticMessage: null,
            descriptor.ParserCounts,
            CanonicalReplayEncoding.NormalizeBounds(descriptor.ReachedBounds, nameof(descriptor)));
    }

    /// <summary>Creates an invalid complete-parse outcome without a partial descriptor.</summary>
    /// <param name="rawExpression">The exact raw input; an empty value is valid for an invalid parse.</param>
    /// <param name="issue">One issue reserved for invalid syntax outcomes.</param>
    /// <param name="diagnosticCode">A stable non-empty parser/integrity diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable non-empty, artifact-independent explanation.</param>
    /// <param name="parserCounts">Exact counters reached before invalidity was established.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An invalid outcome whose <see cref="Descriptor"/> is null.</returns>
    /// <exception cref="ArgumentException">The issue belongs to another status or a diagnostic/bound is invalid.</exception>
    public static StaticFieldSyntaxOutcome Invalid(
        string rawExpression,
        StaticFieldSyntaxIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateRejected(
            rawExpression,
            StaticFieldSyntaxStatus.Invalid,
            issue,
            diagnosticCode,
            diagnosticMessage,
            parserCounts,
            reachedBounds);

    /// <summary>Creates a valid-but-unadmitted complete-tree outcome without a partial descriptor.</summary>
    /// <param name="rawExpression">The exact non-empty raw expression text.</param>
    /// <param name="issue">One issue reserved for unsupported syntax outcomes.</param>
    /// <param name="diagnosticCode">A stable non-empty admission diagnostic code.</param>
    /// <param name="diagnosticMessage">A stable non-empty, artifact-independent explanation.</param>
    /// <param name="parserCounts">Exact counters reached during complete-tree classification.</param>
    /// <param name="reachedBounds">An explicitly initialized reached-bound array.</param>
    /// <returns>An unsupported outcome whose <see cref="Descriptor"/> is null.</returns>
    /// <exception cref="ArgumentException">The issue belongs to another status or a diagnostic/bound is invalid.</exception>
    public static StaticFieldSyntaxOutcome Unsupported(
        string rawExpression,
        StaticFieldSyntaxIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateRejected(
            rawExpression,
            StaticFieldSyntaxStatus.Unsupported,
            issue,
            diagnosticCode,
            diagnosticMessage,
            parserCounts,
            reachedBounds);

    /// <summary>Determines whether another outcome has identical versioned union content.</summary>
    /// <param name="other">The syntax outcome to compare.</param>
    /// <returns><see langword="true"/> only when status, payload, counters, diagnostics, and bounds match.</returns>
    public bool Equals(StaticFieldSyntaxOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldSyntaxOutcome);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldSyntaxOutcome CreateRejected(
        string rawExpression,
        StaticFieldSyntaxStatus status,
        StaticFieldSyntaxIssue issue,
        string diagnosticCode,
        string diagnosticMessage,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(rawExpression);
        ArgumentNullException.ThrowIfNull(parserCounts);
        if (rawExpression.Length > CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            throw new ArgumentException(
                "Rejected outcomes cannot retain raw text beyond the pinned front-end limit; use empty raw text for an over-length rejection.",
                nameof(rawExpression));
        }
        StaticFieldContractEncoding.RequireNonEmptyText(diagnosticCode, nameof(diagnosticCode));
        StaticFieldContractEncoding.RequireNonEmptyText(diagnosticMessage, nameof(diagnosticMessage));
        var validIssue = status switch
        {
            StaticFieldSyntaxStatus.Invalid => issue is
                StaticFieldSyntaxIssue.ParseError or
                StaticFieldSyntaxIssue.ParserDiagnostic or
                StaticFieldSyntaxIssue.ParserIntegrityFailure or
                StaticFieldSyntaxIssue.SyntaxBoundReached,
            StaticFieldSyntaxStatus.Unsupported => issue is
                StaticFieldSyntaxIssue.TreeShapeUnsupported or
                StaticFieldSyntaxIssue.AccessTopologyUnsupported or
                StaticFieldSyntaxIssue.SuffixShapeUnsupported or
                StaticFieldSyntaxIssue.FallbackShapeUnsupported,
            _ => false,
        };
        if (!validIssue)
        {
            throw new ArgumentException("The syntax issue is not valid for the requested rejection status.", nameof(issue));
        }

        if (status == StaticFieldSyntaxStatus.Unsupported && string.IsNullOrWhiteSpace(rawExpression))
        {
            throw new ArgumentException("An unsupported parse must retain non-empty valid C# text.", nameof(rawExpression));
        }

        if (status == StaticFieldSyntaxStatus.Unsupported &&
            (parserCounts.NodeCount == 0 || parserCounts.TokenCount == 0 || parserCounts.MaximumDepthReached == 0 ||
             (long)parserCounts.NodeCount + parserCounts.TokenCount > CSharpExpressionFrontEnd.MaximumNodeTokenCount ||
             parserCounts.MaximumDepthReached > CSharpExpressionFrontEnd.MaximumSyntaxDepth))
        {
            throw new ArgumentException(
                "A valid-but-unsupported complete parse must retain positive in-cap complete traversal counts.",
                nameof(parserCounts));
        }

        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        if (issue == StaticFieldSyntaxIssue.SyntaxBoundReached &&
            !HasCorrelatedSyntaxBound(rawExpression, parserCounts, normalizedBounds))
        {
            throw new ArgumentException(
                "A syntax-bound rejection must retain a fixed front-end bound whose raw-length or traversal counter reached it.",
                nameof(reachedBounds));
        }
        return new StaticFieldSyntaxOutcome(
            rawExpression,
            status,
            issue,
            descriptor: null,
            diagnosticCode,
            diagnosticMessage,
            parserCounts,
            normalizedBounds);
    }

    private static bool HasCorrelatedSyntaxBound(
        string rawExpression,
        StaticFieldParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        foreach (var bound in bounds)
        {
            if (string.Equals(bound.Name, "query.expression.characters", StringComparison.Ordinal) &&
                bound.Value == CSharpExpressionFrontEnd.MaximumExpressionLength &&
                (rawExpression.Length == 0 || rawExpression.Length >= CSharpExpressionFrontEnd.MaximumExpressionLength))
            {
                return true;
            }
            if (string.Equals(bound.Name, "query.syntax.nodes-and-tokens", StringComparison.Ordinal) &&
                bound.Value == CSharpExpressionFrontEnd.MaximumNodeTokenCount &&
                (long)parserCounts.NodeCount + parserCounts.TokenCount >= CSharpExpressionFrontEnd.MaximumNodeTokenCount)
            {
                return true;
            }
            if (string.Equals(bound.Name, "query.syntax.depth", StringComparison.Ordinal) &&
                bound.Value == CSharpExpressionFrontEnd.MaximumSyntaxDepth &&
                parserCounts.MaximumDepthReached >= CSharpExpressionFrontEnd.MaximumSyntaxDepth)
            {
                return true;
            }
            if (string.Equals(
                    bound.Name,
                    StaticFieldExpressionParser.IdentifierCharacterBoundName,
                    StringComparison.Ordinal) &&
                bound.Value == CSharpExpressionFrontEnd.MaximumIdentifierLength &&
                rawExpression.Length >= CSharpExpressionFrontEnd.MaximumIdentifierLength)
            {
                return true;
            }
            if (string.Equals(
                    bound.Name,
                    StaticFieldExpressionParser.StringLiteralCharacterBoundName,
                    StringComparison.Ordinal) &&
                bound.Value == CSharpExpressionFrontEnd.MaximumStringLiteralLength &&
                rawExpression.Length >= CSharpExpressionFrontEnd.MaximumStringLiteralLength)
            {
                return true;
            }
            if (string.Equals(bound.Name, StaticFieldExpressionParser.SegmentCountBoundName, StringComparison.Ordinal) &&
                bound.Value == StaticFieldExpressionParser.MaximumSegmentCount &&
                parserCounts.ProjectedSegmentCount >= StaticFieldExpressionParser.MaximumSegmentCount)
            {
                return true;
            }
            if (string.Equals(
                    bound.Name,
                    StaticFieldExpressionParser.CandidateShapeCountBoundName,
                    StringComparison.Ordinal) &&
                bound.Value == StaticFieldExpressionParser.MaximumCandidateShapeCount &&
                parserCounts.ProjectedCandidateShapeCount >= StaticFieldExpressionParser.MaximumCandidateShapeCount)
            {
                return true;
            }
        }
        return false;
    }
}

internal static class StaticFieldContractEncoding
{
    internal static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty ? ImmutableArray<T>.Empty : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    internal static ImmutableArray<T> RequireAndCopyNonEmpty<T>(ImmutableArray<T> values, string parameterName)
        where T : class
    {
        if (values.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An explicitly initialized, non-empty array is required.", parameterName);
        }

        if (values.Any(static value => value is null))
        {
            throw new ArgumentException("The array cannot contain null entries.", parameterName);
        }

        return Copy(values);
    }

    internal static ImmutableArray<T> RequireAndCopy<T>(ImmutableArray<T> values, string parameterName)
        where T : class
    {
        if (values.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized array is required.", parameterName);
        }

        if (values.Any(static value => value is null))
        {
            throw new ArgumentException("The array cannot contain null entries.", parameterName);
        }

        return Copy(values);
    }

    internal static ImmutableArray<T> NormalizeUnique<T>(
        ImmutableArray<T> values,
        string parameterName,
        Func<T, ImmutableArray<byte>> canonicalBytes)
        where T : class
    {
        var copied = RequireAndCopyNonEmpty(values, parameterName).ToArray();
        Array.Sort(copied, (left, right) => CompareBytes(canonicalBytes(left).AsSpan(), canonicalBytes(right).AsSpan()));
        for (var index = 1; index < copied.Length; index++)
        {
            if (canonicalBytes(copied[index - 1]).AsSpan().SequenceEqual(canonicalBytes(copied[index]).AsSpan()))
            {
                throw new ArgumentException("The array contains a duplicate canonical entry.", parameterName);
            }
        }

        return ImmutableArray.CreateRange(copied);
    }

    internal static ImmutableArray<T> NormalizeAndDeduplicate<T>(
        ImmutableArray<T> values,
        string parameterName,
        Func<T, ImmutableArray<byte>> canonicalBytes)
        where T : class
    {
        var copied = RequireAndCopy(values, parameterName).ToArray();
        Array.Sort(copied, (left, right) => CompareBytes(canonicalBytes(left).AsSpan(), canonicalBytes(right).AsSpan()));
        if (copied.Length < 2)
        {
            return ImmutableArray.CreateRange(copied);
        }

        var unique = ImmutableArray.CreateBuilder<T>(copied.Length);
        unique.Add(copied[0]);
        for (var index = 1; index < copied.Length; index++)
        {
            if (!canonicalBytes(copied[index - 1]).AsSpan().SequenceEqual(canonicalBytes(copied[index]).AsSpan()))
            {
                unique.Add(copied[index]);
            }
        }

        return unique.ToImmutable();
    }

    internal static ImmutableArray<T> NormalizeUniqueAllowEmpty<T>(
        ImmutableArray<T> values,
        string parameterName,
        Func<T, ImmutableArray<byte>> canonicalBytes)
        where T : class
    {
        var copied = RequireAndCopy(values, parameterName).ToArray();
        Array.Sort(copied, (left, right) => CompareBytes(canonicalBytes(left).AsSpan(), canonicalBytes(right).AsSpan()));
        for (var index = 1; index < copied.Length; index++)
        {
            if (canonicalBytes(copied[index - 1]).AsSpan().SequenceEqual(canonicalBytes(copied[index]).AsSpan()))
            {
                throw new ArgumentException("The array contains a duplicate canonical entry.", parameterName);
            }
        }

        return ImmutableArray.CreateRange(copied);
    }

    internal static void RequireNonEmptyText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

    internal static void RequireIdentifierValue(string value, string parameterName, bool allowDots)
    {
        RequireNonEmptyText(value, parameterName);
        if (value.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            (!allowDots && value.Contains('.', StringComparison.Ordinal)) ||
            (allowDots && (value.StartsWith(".", StringComparison.Ordinal) ||
                           value.EndsWith(".", StringComparison.Ordinal) ||
                           value.Contains("..", StringComparison.Ordinal))))
        {
            throw new ArgumentException("The decoded identifier value has an invalid detached shape.", parameterName);
        }
    }

    internal static void RequireDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "The numeric tag is not defined by this schema.");
        }
    }

    internal static void WriteValues<T>(CanonicalReplayEncoding.Writer writer, ImmutableArray<T> values)
        where T : class
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            var bytes = value switch
            {
                StaticFieldAccessSegment segment => segment.CanonicalBytes,
                StaticFieldCandidateShape shape => shape.CanonicalBytes,
                _ => throw new InvalidOperationException("The canonical value type is not supported."),
            };
            writer.WriteLengthPrefixedBytes(bytes.AsSpan());
        }
    }

    internal static void WriteBounds(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        writer.WriteInt32(bounds.Length);
        foreach (var bound in bounds)
        {
            writer.WriteString(bound.Name);
            writer.WriteInt64(bound.Value);
        }
    }

    internal static void WriteOptionalString(CanonicalReplayEncoding.Writer writer, string? value)
    {
        writer.WriteBoolean(value is not null);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    internal static int CompareBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var commonLength = Math.Min(left.Length, right.Length);
        for (var index = 0; index < commonLength; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
