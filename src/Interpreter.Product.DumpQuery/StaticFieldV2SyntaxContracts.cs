using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies the first syntax boundary that prevented a V2 descriptor.</summary>
/// <remarks>This draft-phase enumeration may evolve with the projector while preserving typed first-stop semantics.</remarks>
public enum StaticFieldV2SyntaxIssue
{
    /// <summary>No issue occurred; valid only for an admitted descriptor.</summary>
    None = 0,
    /// <summary>The input exceeded the common front-end expression limit before parsing.</summary>
    ExpressionBoundReached = 1,
    /// <summary>The input could not form one complete expression parse.</summary>
    ParseError = 2,
    /// <summary>The complete parser reported an error diagnostic.</summary>
    ParserDiagnostic = 3,
    /// <summary>Parser spans, tokens, or tree ownership failed an integrity check.</summary>
    ParserIntegrityFailure = 4,
    /// <summary>The complete node/token walk exceeded its common limit.</summary>
    NodeTokenBoundReached = 5,
    /// <summary>The complete syntax walk exceeded its depth limit.</summary>
    SyntaxDepthBoundReached = 6,
    /// <summary>A decoded identifier reached the common identifier-character cap-plus-one.</summary>
    IdentifierBoundReached = 7,
    /// <summary>A decoded string literal reached the common fallback-character cap-plus-one.</summary>
    FallbackStringBoundReached = 8,
    /// <summary>The complete parse is valid but its root tree is outside the V2 grammar.</summary>
    TreeShapeUnsupported = 101,
    /// <summary>A valid type-argument tree is outside the closed V2 grammar.</summary>
    TypeArgumentShapeUnsupported = 102,
    /// <summary>A valid separator topology is outside the V2 grammar.</summary>
    SeparatorTopologyUnsupported = 103,
    /// <summary>The common expression-segment count reached cap-plus-one.</summary>
    SegmentBoundReached = 104,
    /// <summary>The recursive type topology reached the depth cap-plus-one.</summary>
    TypeTopologyDepthBoundReached = 105,
    /// <summary>The recursive type topology reached the cumulative-node cap-plus-one.</summary>
    TypeTopologyNodeBoundReached = 106,
    /// <summary>The recursive type topology reached the cumulative-argument cap-plus-one.</summary>
    TypeArgumentBoundReached = 107,
    /// <summary>Array syntax reached the rank cap-plus-one.</summary>
    ArrayRankBoundReached = 108,
    /// <summary>The operation-derived candidate-partition count reached cap-plus-one.</summary>
    PartitionBoundReached = 109,
    /// <summary>The detached suffix is outside the unchanged W2/W6 shapes.</summary>
    SuffixShapeUnsupported = 110,
}

/// <summary>Classifies the qualifier preceding the first V2 expression-name segment.</summary>
/// <remarks>This draft-phase enumeration describes detached syntax and does not resolve an alias.</remarks>
public enum StaticFieldV2AliasKind
{
    /// <summary>No alias qualifier precedes the first segment.</summary>
    None = 0,
    /// <summary>The exact <c>global::</c> qualifier precedes the first segment.</summary>
    Global = 1,
    /// <summary>An arbitrary named alias followed by <c>::</c> precedes the first segment.</summary>
    Named = 2,
}

/// <summary>Classifies whether a structural partition has an explicit owner or a bare field root.</summary>
/// <remarks>This draft-phase enumeration records syntax candidates before binding selects any route.</remarks>
public enum StaticFieldV2CandidateKind
{
    /// <summary>One or more namespace/type segments precede the candidate field.</summary>
    QualifiedOwner = 1,
    /// <summary>The candidate field is the first expression segment.</summary>
    BareMember = 2,
}

/// <summary>Classifies the source edge leading to one expression-name segment.</summary>
/// <remarks>This draft-phase enumeration retains source topology without evaluating the edge.</remarks>
public enum StaticFieldV2ExpressionSeparatorKind
{
    /// <summary>The first segment has no dot edge.</summary>
    None = 0,
    /// <summary>The segment follows ordinary dot access.</summary>
    Dot = 1,
    /// <summary>The segment follows conditional access.</summary>
    ConditionalDot = 2,
}

/// <summary>Classifies the recursively detached closed type-argument syntax.</summary>
/// <remarks>This draft-phase enumeration covers only the explicitly admitted closed type grammar.</remarks>
public enum StaticFieldV2TypeSyntaxKind
{
    /// <summary>One admitted predefined C# type keyword.</summary>
    Predefined = 1,
    /// <summary>A possibly alias-qualified sequence of named type segments.</summary>
    Named = 2,
    /// <summary>A nullable suffix over one value-type candidate.</summary>
    Nullable = 3,
    /// <summary>A single-dimensional zero-based array suffix.</summary>
    SzArray = 4,
    /// <summary>A bounded multidimensional array suffix.</summary>
    MultidimensionalArray = 5,
}

/// <summary>Identifies each predefined type admitted by the V2 source grammar.</summary>
/// <remarks>This draft-phase enumeration is a source-syntax catalog, not a bound runtime type catalog.</remarks>
public enum StaticFieldV2PredefinedTypeKind
{
    /// <summary><c>bool</c>.</summary>
    Boolean = 1,
    /// <summary><c>byte</c>.</summary>
    Byte = 2,
    /// <summary><c>sbyte</c>.</summary>
    SByte = 3,
    /// <summary><c>short</c>.</summary>
    Int16 = 4,
    /// <summary><c>ushort</c>.</summary>
    UInt16 = 5,
    /// <summary><c>int</c>.</summary>
    Int32 = 6,
    /// <summary><c>uint</c>.</summary>
    UInt32 = 7,
    /// <summary><c>long</c>.</summary>
    Int64 = 8,
    /// <summary><c>ulong</c>.</summary>
    UInt64 = 9,
    /// <summary><c>nint</c>.</summary>
    NativeInt = 10,
    /// <summary><c>nuint</c>.</summary>
    NativeUInt = 11,
    /// <summary><c>char</c>.</summary>
    Char = 12,
    /// <summary><c>float</c>.</summary>
    Single = 13,
    /// <summary><c>double</c>.</summary>
    Double = 14,
    /// <summary><c>decimal</c>.</summary>
    Decimal = 15,
    /// <summary><c>string</c>.</summary>
    String = 16,
    /// <summary><c>object</c>.</summary>
    Object = 17,
}

/// <summary>Freezes the exact optional alias qualifier preceding a named route.</summary>
/// <remarks>This sealed draft value distinguishes global, named, and absent qualifiers canonically.</remarks>
public sealed class StaticFieldV2AliasQualifier : IEquatable<StaticFieldV2AliasQualifier>
{
    private const string CanonicalDomain = "static-field-v2-alias-qualifier";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2AliasQualifier(StaticFieldV2AliasKind kind, DumpExpressionIdentifier? alias)
    {
        Kind = kind;
        Alias = alias;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteBoolean(alias is not null);
        if (alias is not null)
        {
            writer.WriteLengthPrefixedBytes(alias.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact qualifier kind.</summary>
    public StaticFieldV2AliasKind Kind { get; }
    /// <summary>Gets the named alias only when <see cref="Kind"/> is <see cref="StaticFieldV2AliasKind.Named"/>.</summary>
    public DumpExpressionIdentifier? Alias { get; }
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the canonical absence of an alias qualifier.</summary>
    /// <returns>A new content-equal immutable draft qualifier.</returns>
    public static StaticFieldV2AliasQualifier None() => new(StaticFieldV2AliasKind.None, null);

    /// <summary>Creates the canonical <c>global::</c> qualifier.</summary>
    /// <returns>A new content-equal immutable draft qualifier.</returns>
    public static StaticFieldV2AliasQualifier Global() => new(StaticFieldV2AliasKind.Global, null);

    /// <summary>Creates one arbitrary named alias qualifier.</summary>
    /// <param name="alias">The exact alias identifier preceding <c>::</c>.</param>
    /// <returns>A sealed immutable draft qualifier.</returns>
    public static StaticFieldV2AliasQualifier Named(DumpExpressionIdentifier alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        return new StaticFieldV2AliasQualifier(StaticFieldV2AliasKind.Named, alias);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other qualifier.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2AliasQualifier? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2AliasQualifier);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one named type segment and its segment-local ordered type arguments.</summary>
/// <remarks>This sealed draft segment is detached syntax only; its namespace-versus-type role remains undecided.</remarks>
public sealed class StaticFieldV2TypeNameSegment : IEquatable<StaticFieldV2TypeNameSegment>
{
    private const string CanonicalDomain = "static-field-v2-type-name-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2TypeSyntax> typeArguments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeNameSegment(
        DumpExpressionIdentifier identifier,
        ImmutableArray<StaticFieldV2TypeSyntax> typeArguments)
    {
        Identifier = identifier;
        this.typeArguments = typeArguments;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(identifier.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            typeArguments,
            static argument => argument.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact detached segment identifier.</summary>
    public DumpExpressionIdentifier Identifier { get; }
    /// <summary>Gets a defensive copy of segment-local type arguments.</summary>
    public ImmutableArray<StaticFieldV2TypeSyntax> TypeArguments => ExpressionV2ContractEncoding.Copy(typeArguments);
    /// <summary>Gets the segment-local generic arity.</summary>
    public int Arity => typeArguments.Length;
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated named type segment.</summary>
    /// <param name="identifier">The exact detached segment identifier.</param>
    /// <param name="typeArguments">Zero through sixty-four segment-local closed type-argument trees.</param>
    /// <returns>A sealed immutable draft segment.</returns>
    /// <exception cref="ArgumentException">The array is default, over bound, or contains null.</exception>
    public static StaticFieldV2TypeNameSegment Create(
        DumpExpressionIdentifier identifier,
        ImmutableArray<StaticFieldV2TypeSyntax> typeArguments)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            typeArguments,
            nameof(typeArguments),
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount);
        return new StaticFieldV2TypeNameSegment(identifier, copied);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other segment.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2TypeNameSegment? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeNameSegment);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one recursively closed source type-argument tree without binding names.</summary>
/// <remarks>
/// This sealed draft syntax value admits only the W8 closed grammar. Pointer, function-pointer, tuple, dynamic, open
/// type-parameter, and other valid C# trees receive a typed syntax outcome before this factory is called.
/// </remarks>
public sealed class StaticFieldV2TypeSyntax : IEquatable<StaticFieldV2TypeSyntax>
{
    private const string CanonicalDomain = "static-field-v2-type-syntax";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2TypeSyntax(
        StaticFieldV2TypeSyntaxKind kind,
        StaticFieldV2PredefinedTypeKind? predefinedKind,
        StaticFieldV2AliasQualifier? aliasQualifier,
        ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments,
        StaticFieldV2TypeSyntax? elementType,
        int? arrayRank,
        int topologyDepth,
        int topologyNodeCount,
        int cumulativeArgumentCount)
    {
        Kind = kind;
        PredefinedKind = predefinedKind;
        AliasQualifier = aliasQualifier;
        this.nameSegments = nameSegments;
        ElementType = elementType;
        ArrayRank = arrayRank;
        TopologyDepth = topologyDepth;
        TopologyNodeCount = topologyNodeCount;
        CumulativeArgumentCount = cumulativeArgumentCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, predefinedKind);
        writer.WriteBoolean(aliasQualifier is not null);
        if (aliasQualifier is not null)
        {
            writer.WriteLengthPrefixedBytes(aliasQualifier.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            nameSegments,
            static segment => segment.CanonicalBytes);
        writer.WriteBoolean(elementType is not null);
        if (elementType is not null)
        {
            writer.WriteLengthPrefixedBytes(elementType.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, arrayRank);
        writer.WriteInt32(topologyDepth);
        writer.WriteInt32(topologyNodeCount);
        writer.WriteInt32(cumulativeArgumentCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact recursive syntax kind.</summary>
    public StaticFieldV2TypeSyntaxKind Kind { get; }
    /// <summary>Gets the predefined keyword kind when applicable.</summary>
    public StaticFieldV2PredefinedTypeKind? PredefinedKind { get; }
    /// <summary>Gets the named-root alias qualifier when applicable.</summary>
    public StaticFieldV2AliasQualifier? AliasQualifier { get; }
    /// <summary>Gets a defensive copy of named segments when applicable.</summary>
    public ImmutableArray<StaticFieldV2TypeNameSegment> NameSegments => ExpressionV2ContractEncoding.Copy(nameSegments);
    /// <summary>Gets the element type for nullable or array syntax.</summary>
    public StaticFieldV2TypeSyntax? ElementType { get; }
    /// <summary>Gets the exact multidimensional rank, one for SZARRAY, or null for non-array syntax.</summary>
    public int? ArrayRank { get; }
    /// <summary>Gets the exact maximum recursive topology depth.</summary>
    public int TopologyDepth { get; }
    /// <summary>Gets the exact cumulative topology node count.</summary>
    public int TopologyNodeCount { get; }
    /// <summary>Gets the exact cumulative generic-argument count.</summary>
    public int CumulativeArgumentCount { get; }
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one predefined type-keyword syntax tree.</summary>
    /// <param name="kind">The admitted predefined keyword kind.</param>
    /// <returns>A sealed immutable draft tree.</returns>
    public static StaticFieldV2TypeSyntax Predefined(StaticFieldV2PredefinedTypeKind kind)
    {
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        return CreateCore(
            StaticFieldV2TypeSyntaxKind.Predefined,
            kind,
            null,
            ImmutableArray<StaticFieldV2TypeNameSegment>.Empty,
            null,
            null);
    }

    /// <summary>Creates one possibly alias-qualified named type syntax tree.</summary>
    /// <param name="aliasQualifier">The exact absent, global, or named qualifier.</param>
    /// <param name="segments">One through thirty-two undecided namespace/type segments.</param>
    /// <returns>A sealed immutable draft tree.</returns>
    public static StaticFieldV2TypeSyntax Named(
        StaticFieldV2AliasQualifier aliasQualifier,
        ImmutableArray<StaticFieldV2TypeNameSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(aliasQualifier);
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            segments,
            nameof(segments),
            StaticFieldV2Limits.MaximumExpressionSegmentCount);
        if (copied.IsEmpty)
        {
            throw new ArgumentException("A named type requires at least one segment.", nameof(segments));
        }
        return CreateCore(StaticFieldV2TypeSyntaxKind.Named, null, aliasQualifier, copied, null, null);
    }

    /// <summary>Creates nullable syntax over one admitted closed element tree.</summary>
    /// <param name="elementType">The detached value-type candidate syntax.</param>
    /// <returns>A sealed immutable draft tree.</returns>
    public static StaticFieldV2TypeSyntax Nullable(StaticFieldV2TypeSyntax elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType.Kind == StaticFieldV2TypeSyntaxKind.Nullable)
        {
            throw new ArgumentException("Nullable syntax cannot directly wrap another nullable suffix.", nameof(elementType));
        }
        return CreateCore(
            StaticFieldV2TypeSyntaxKind.Nullable,
            null,
            null,
            ImmutableArray<StaticFieldV2TypeNameSegment>.Empty,
            elementType,
            null);
    }

    /// <summary>Creates single-dimensional zero-based array syntax.</summary>
    /// <param name="elementType">The detached closed element syntax.</param>
    /// <returns>A sealed immutable draft tree.</returns>
    public static StaticFieldV2TypeSyntax SzArray(StaticFieldV2TypeSyntax elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return CreateCore(
            StaticFieldV2TypeSyntaxKind.SzArray,
            null,
            null,
            ImmutableArray<StaticFieldV2TypeNameSegment>.Empty,
            elementType,
            1);
    }

    /// <summary>Creates bounded multidimensional array syntax.</summary>
    /// <param name="elementType">The detached closed element syntax.</param>
    /// <param name="rank">The exact source rank from two through thirty-two.</param>
    /// <returns>A sealed immutable draft multidimensional-array tree.</returns>
    public static StaticFieldV2TypeSyntax MultidimensionalArray(StaticFieldV2TypeSyntax elementType, int rank)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (rank < 2 || rank > StaticFieldV2Limits.MaximumArrayRank)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                "Source multidimensional-array syntax requires rank two through the V2 bound.");
        }
        return CreateCore(
            StaticFieldV2TypeSyntaxKind.MultidimensionalArray,
            null,
            null,
            ImmutableArray<StaticFieldV2TypeNameSegment>.Empty,
            elementType,
            rank);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other syntax tree.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2TypeSyntax? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2TypeSyntax);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldV2TypeSyntax CreateCore(
        StaticFieldV2TypeSyntaxKind kind,
        StaticFieldV2PredefinedTypeKind? predefinedKind,
        StaticFieldV2AliasQualifier? aliasQualifier,
        ImmutableArray<StaticFieldV2TypeNameSegment> nameSegments,
        StaticFieldV2TypeSyntax? elementType,
        int? arrayRank)
    {
        var depth = 1;
        var nodes = 1;
        var arguments = 0;
        if (elementType is not null)
        {
            depth = checked(elementType.TopologyDepth + 1);
            nodes = checked(elementType.TopologyNodeCount + 1);
            arguments = elementType.CumulativeArgumentCount;
        }
        foreach (var segment in nameSegments)
        {
            foreach (var argument in segment.TypeArguments)
            {
                depth = Math.Max(depth, checked(argument.TopologyDepth + 1));
                nodes = checked(nodes + argument.TopologyNodeCount);
                arguments = checked(arguments + 1 + argument.CumulativeArgumentCount);
            }
        }
        if (depth > StaticFieldV2Limits.MaximumClosedTypeTopologyDepth ||
            nodes > StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount ||
            arguments > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
        {
            throw new ArgumentException("The recursive type syntax exceeds a declared V2 topology bound.");
        }
        return new StaticFieldV2TypeSyntax(
            kind,
            predefinedKind,
            aliasQualifier,
            nameSegments,
            elementType,
            arrayRank,
            depth,
            nodes,
            arguments);
    }
}

/// <summary>Freezes one expression-name segment and its source-local generic arguments.</summary>
/// <remarks>No namespace/type/member role is assigned by this sealed draft syntax value.</remarks>
public sealed class StaticFieldV2ExpressionNameSegment : IEquatable<StaticFieldV2ExpressionNameSegment>
{
    private const string CanonicalDomain = "static-field-v2-expression-name-segment";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2TypeSyntax> typeArguments;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ExpressionNameSegment(
        DumpExpressionIdentifier identifier,
        StaticFieldV2ExpressionSeparatorKind separator,
        ImmutableArray<StaticFieldV2TypeSyntax> typeArguments)
    {
        Identifier = identifier;
        Separator = separator;
        this.typeArguments = typeArguments;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(identifier.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)separator);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            typeArguments,
            static argument => argument.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact detached identifier.</summary>
    public DumpExpressionIdentifier Identifier { get; }
    /// <summary>Gets the source edge leading to this segment.</summary>
    public StaticFieldV2ExpressionSeparatorKind Separator { get; }
    /// <summary>Gets a defensive copy of segment-local generic arguments.</summary>
    public ImmutableArray<StaticFieldV2TypeSyntax> TypeArguments => ExpressionV2ContractEncoding.Copy(typeArguments);
    /// <summary>Gets the segment-local generic arity.</summary>
    public int Arity => typeArguments.Length;
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated expression-name segment.</summary>
    /// <param name="identifier">The exact detached identifier.</param>
    /// <param name="separator">The source edge leading to this segment.</param>
    /// <param name="typeArguments">Zero through sixty-four source-local generic arguments.</param>
    /// <returns>A sealed immutable draft segment.</returns>
    public static StaticFieldV2ExpressionNameSegment Create(
        DumpExpressionIdentifier identifier,
        StaticFieldV2ExpressionSeparatorKind separator,
        ImmutableArray<StaticFieldV2TypeSyntax> typeArguments)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ExpressionV2ContractEncoding.RequireDefined(separator, nameof(separator));
        var copied = ExpressionV2ContractEncoding.CopyRequired(
            typeArguments,
            nameof(typeArguments),
            StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount);
        return new StaticFieldV2ExpressionNameSegment(identifier, separator, copied);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other segment.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2ExpressionNameSegment? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ExpressionNameSegment);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete structural interpretation without assigning namespace or type roles.</summary>
/// <remarks>Every possible top-level type split is retained as a distinct sealed draft partition.</remarks>
public sealed class StaticFieldV2StructuralPartition : IEquatable<StaticFieldV2StructuralPartition>
{
    private const string CanonicalDomain = "static-field-v2-structural-partition";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StructuralPartition(
        StaticFieldV2CandidateKind candidateKind,
        int fieldSegmentIndex,
        int? possibleTopLevelTypeSegmentIndex,
        DumpExpressionSuffixDescriptor suffix)
    {
        CandidateKind = candidateKind;
        FieldSegmentIndex = fieldSegmentIndex;
        PossibleTopLevelTypeSegmentIndex = possibleTopLevelTypeSegmentIndex;
        Suffix = suffix;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)candidateKind);
        writer.WriteInt32(fieldSegmentIndex);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, possibleTopLevelTypeSegmentIndex);
        writer.WriteLengthPrefixedBytes(suffix.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this is a qualified-owner or bare-member candidate.</summary>
    public StaticFieldV2CandidateKind CandidateKind { get; }
    /// <summary>Gets the zero-based expression segment interpreted as the candidate field.</summary>
    public int FieldSegmentIndex { get; }
    /// <summary>Gets one possible top-level type segment, or null for a bare candidate.</summary>
    public int? PossibleTopLevelTypeSegmentIndex { get; }
    /// <summary>Gets the exact detached suffix following the candidate field.</summary>
    public DumpExpressionSuffixDescriptor Suffix { get; }
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one validated structural partition.</summary>
    /// <param name="candidateKind">Whether the candidate is qualified or bare.</param>
    /// <param name="fieldSegmentIndex">The zero-based candidate field index.</param>
    /// <param name="possibleTopLevelTypeSegmentIndex">One possible top-level type index for a qualified route.</param>
    /// <param name="suffix">The exact detached suffix.</param>
    /// <returns>A sealed immutable draft partition whose full extent is checked by its descriptor.</returns>
    public static StaticFieldV2StructuralPartition Create(
        StaticFieldV2CandidateKind candidateKind,
        int fieldSegmentIndex,
        int? possibleTopLevelTypeSegmentIndex,
        DumpExpressionSuffixDescriptor suffix)
    {
        ExpressionV2ContractEncoding.RequireDefined(candidateKind, nameof(candidateKind));
        ArgumentNullException.ThrowIfNull(suffix);
        if (fieldSegmentIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldSegmentIndex));
        }
        var valid = candidateKind switch
        {
            StaticFieldV2CandidateKind.BareMember =>
                fieldSegmentIndex == 0 && possibleTopLevelTypeSegmentIndex is null,
            StaticFieldV2CandidateKind.QualifiedOwner =>
                fieldSegmentIndex >= 1 && possibleTopLevelTypeSegmentIndex is >= 0 &&
                possibleTopLevelTypeSegmentIndex < fieldSegmentIndex,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Candidate kind, field index, and top-level type split disagree.");
        }
        return new StaticFieldV2StructuralPartition(
            candidateKind,
            fieldSegmentIndex,
            possibleTopLevelTypeSegmentIndex,
            suffix);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other partition.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2StructuralPartition? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StructuralPartition);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Records exact parser and projection counters for one V2 syntax outcome.</summary>
/// <remarks>Cap-plus-one observations are retained for non-admitted outcomes by this sealed draft counter set.</remarks>
public sealed class StaticFieldV2ParserCounts : IEquatable<StaticFieldV2ParserCounts>
{
    private const string CanonicalDomain = "static-field-v2-parser-counts";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ParserCounts(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int projectedSegmentCount,
        int projectedTypeArgumentCount,
        int maximumTypeTopologyDepth,
        int cumulativeTypeTopologyNodeCount,
        int maximumArrayRank,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength,
        int completePartitionCount)
    {
        NodeCount = nodeCount;
        TokenCount = tokenCount;
        MaximumDepth = maximumDepth;
        ProjectedSegmentCount = projectedSegmentCount;
        ProjectedTypeArgumentCount = projectedTypeArgumentCount;
        MaximumTypeTopologyDepth = maximumTypeTopologyDepth;
        CumulativeTypeTopologyNodeCount = cumulativeTypeTopologyNodeCount;
        MaximumArrayRank = maximumArrayRank;
        MaximumDecodedIdentifierLength = maximumDecodedIdentifierLength;
        MaximumDecodedFallbackStringLength = maximumDecodedFallbackStringLength;
        CompletePartitionCount = completePartitionCount;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(nodeCount);
        writer.WriteInt32(tokenCount);
        writer.WriteInt32(maximumDepth);
        writer.WriteInt32(projectedSegmentCount);
        writer.WriteInt32(projectedTypeArgumentCount);
        writer.WriteInt32(maximumTypeTopologyDepth);
        writer.WriteInt32(cumulativeTypeTopologyNodeCount);
        writer.WriteInt32(maximumArrayRank);
        writer.WriteInt32(maximumDecodedIdentifierLength);
        writer.WriteInt32(maximumDecodedFallbackStringLength);
        writer.WriteInt32(completePartitionCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact syntax-node count.</summary>
    public int NodeCount { get; }
    /// <summary>Gets the exact syntax-token count.</summary>
    public int TokenCount { get; }
    /// <summary>Gets the greatest syntax depth reached.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets the projected expression-segment count, including cap-plus-one.</summary>
    public int ProjectedSegmentCount { get; }
    /// <summary>Gets the cumulative projected type-argument count, including cap-plus-one.</summary>
    public int ProjectedTypeArgumentCount { get; }
    /// <summary>Gets the greatest projected type-topology depth, including cap-plus-one.</summary>
    public int MaximumTypeTopologyDepth { get; }
    /// <summary>Gets the cumulative projected type-topology node count, including cap-plus-one.</summary>
    public int CumulativeTypeTopologyNodeCount { get; }
    /// <summary>Gets the greatest projected source array rank, including cap-plus-one.</summary>
    public int MaximumArrayRank { get; }
    /// <summary>Gets the greatest decoded identifier length, including cap-plus-one.</summary>
    public int MaximumDecodedIdentifierLength { get; }
    /// <summary>Gets the greatest decoded fallback-string length, including cap-plus-one.</summary>
    public int MaximumDecodedFallbackStringLength { get; }
    /// <summary>Gets the complete count through the cap, or the saturated first over-cap observation.</summary>
    public int CompletePartitionCount { get; }
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one non-negative exact counter set.</summary>
    /// <param name="nodeCount">The exact syntax-node count.</param>
    /// <param name="tokenCount">The exact syntax-token count.</param>
    /// <param name="maximumDepth">The greatest syntax depth reached.</param>
    /// <param name="projectedSegmentCount">The projected segment count, at most cap-plus-one.</param>
    /// <param name="projectedTypeArgumentCount">The projected type-argument count, at most cap-plus-one.</param>
    /// <param name="maximumTypeTopologyDepth">The greatest type-topology depth, at most cap-plus-one.</param>
    /// <param name="cumulativeTypeTopologyNodeCount">The cumulative topology node count, at most cap-plus-one.</param>
    /// <param name="maximumArrayRank">The greatest source array rank, at most cap-plus-one.</param>
    /// <param name="maximumDecodedIdentifierLength">The greatest decoded identifier length, at most cap-plus-one.</param>
    /// <param name="maximumDecodedFallbackStringLength">The greatest decoded fallback-string length, at most cap-plus-one.</param>
    /// <param name="completePartitionCount">The complete partition count or saturated first over-cap observation.</param>
    /// <returns>A sealed immutable draft counter set.</returns>
    public static StaticFieldV2ParserCounts Create(
        int nodeCount,
        int tokenCount,
        int maximumDepth,
        int projectedSegmentCount,
        int projectedTypeArgumentCount,
        int maximumTypeTopologyDepth,
        int cumulativeTypeTopologyNodeCount,
        int maximumArrayRank,
        int maximumDecodedIdentifierLength,
        int maximumDecodedFallbackStringLength,
        int completePartitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nodeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDepth);
        if ((long)nodeCount + tokenCount > CSharpExpressionFrontEnd.MaximumNodeTokenCount + 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount), "The node/token observation cannot exceed cap-plus-one.");
        }
        if (maximumDepth > CSharpExpressionFrontEnd.MaximumSyntaxDepth + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth), "The syntax-depth observation cannot exceed cap-plus-one.");
        }
        if (projectedSegmentCount < 0 || projectedSegmentCount > StaticFieldV2Limits.MaximumExpressionSegmentCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(projectedSegmentCount));
        }
        if (projectedTypeArgumentCount < 0 ||
            projectedTypeArgumentCount > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(projectedTypeArgumentCount));
        }
        if (maximumTypeTopologyDepth < 0 ||
            maximumTypeTopologyDepth > StaticFieldV2Limits.MaximumClosedTypeTopologyDepth + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTypeTopologyDepth));
        }
        if (cumulativeTypeTopologyNodeCount < 0 ||
            cumulativeTypeTopologyNodeCount > StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cumulativeTypeTopologyNodeCount));
        }
        if (maximumArrayRank < 0 || maximumArrayRank > StaticFieldV2Limits.MaximumArrayRank + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumArrayRank));
        }
        if (maximumDecodedIdentifierLength < 0 ||
            maximumDecodedIdentifierLength > StaticFieldV2Limits.MaximumIdentifierCharacterCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDecodedIdentifierLength));
        }
        if (maximumDecodedFallbackStringLength < 0 ||
            maximumDecodedFallbackStringLength > StaticFieldV2Limits.MaximumFallbackStringCharacterCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDecodedFallbackStringLength));
        }
        if (completePartitionCount < 0 || completePartitionCount > StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(completePartitionCount));
        }
        return new StaticFieldV2ParserCounts(
            nodeCount,
            tokenCount,
            maximumDepth,
            projectedSegmentCount,
            projectedTypeArgumentCount,
            maximumTypeTopologyDepth,
            cumulativeTypeTopologyNodeCount,
            maximumArrayRank,
            maximumDecodedIdentifierLength,
            maximumDecodedFallbackStringLength,
            completePartitionCount);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other counter set.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2ParserCounts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ParserCounts);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one admitted V2 expression with all complete structural partitions.</summary>
/// <remarks>
/// This sealed draft descriptor keeps namespace/type roles unresolved. A cap-plus-one partition observation has no
/// descriptor and must instead be represented by a typed unsupported syntax outcome.
/// </remarks>
public sealed class StaticFieldV2ExpressionDescriptor : IEquatable<StaticFieldV2ExpressionDescriptor>
{
    private const string CanonicalDomain = "static-field-v2-expression-descriptor";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2ExpressionNameSegment> segments;
    private readonly ImmutableArray<StaticFieldV2StructuralPartition> partitions;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2ExpressionDescriptor(
        string rawExpression,
        StaticFieldV2AliasQualifier aliasQualifier,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        ImmutableArray<StaticFieldV2StructuralPartition> partitions,
        StaticFieldV2ParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        RawExpression = rawExpression;
        AliasQualifier = aliasQualifier;
        this.segments = segments;
        this.partitions = partitions;
        ParserCounts = parserCounts;
        this.reachedBounds = reachedBounds;
        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)DumpExpressionProfileKind.StaticFieldExpressionV2);
        writer.WriteString(rawExpression);
        writer.WriteLengthPrefixedBytes(aliasQualifier.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, segments, static segment => segment.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, partitions, static partition => partition.CanonicalBytes);
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the explicit profile represented by this descriptor.</summary>
    public DumpExpressionProfileKind Profile => DumpExpressionProfileKind.StaticFieldExpressionV2;
    /// <summary>Gets the exact complete expression text.</summary>
    public string RawExpression { get; }
    /// <summary>Gets the exact qualifier preceding the first segment.</summary>
    public StaticFieldV2AliasQualifier AliasQualifier { get; }
    /// <summary>Gets a defensive copy of all detached expression-name segments.</summary>
    public ImmutableArray<StaticFieldV2ExpressionNameSegment> Segments => ExpressionV2ContractEncoding.Copy(segments);
    /// <summary>Gets a defensive copy of every complete structural partition.</summary>
    public ImmutableArray<StaticFieldV2StructuralPartition> Partitions => ExpressionV2ContractEncoding.Copy(partitions);
    /// <summary>Gets exact parser and projection counters.</summary>
    public StaticFieldV2ParserCounts ParserCounts { get; }
    /// <summary>Gets a defensive copy of exact declared bounds reached without losing completeness.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => ExpressionV2ContractEncoding.Copy(reachedBounds);
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete admitted V2 syntax descriptor.</summary>
    /// <param name="rawExpression">The exact bounded non-empty expression text.</param>
    /// <param name="aliasQualifier">The exact first-segment alias qualifier.</param>
    /// <param name="segments">All detached expression-name segments.</param>
    /// <param name="partitions">Every complete structural partition.</param>
    /// <param name="parserCounts">Exact parser and projection counters.</param>
    /// <param name="reachedBounds">Exact declared bounds reached by the complete projection.</param>
    /// <returns>A sealed immutable draft descriptor.</returns>
    /// <exception cref="ArgumentException">Any topology, count, partition, bound, or suffix relation is inconsistent.</exception>
    public static StaticFieldV2ExpressionDescriptor Create(
        string rawExpression,
        StaticFieldV2AliasQualifier aliasQualifier,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> segments,
        ImmutableArray<StaticFieldV2StructuralPartition> partitions,
        StaticFieldV2ParserCounts parserCounts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ExpressionV2ContractEncoding.RequireText(
            rawExpression,
            nameof(rawExpression),
            CSharpExpressionFrontEnd.MaximumExpressionLength);
        ArgumentNullException.ThrowIfNull(aliasQualifier);
        ArgumentNullException.ThrowIfNull(parserCounts);
        var segmentCopy = ExpressionV2ContractEncoding.CopyRequired(
            segments,
            nameof(segments),
            StaticFieldV2Limits.MaximumExpressionSegmentCount);
        var partitionCopy = ExpressionV2ContractEncoding.CopyRequired(
            partitions,
            nameof(partitions),
            StaticFieldV2Limits.MaximumSyntaxPartitionCount);
        if (segmentCopy.IsEmpty || partitionCopy.IsEmpty)
        {
            throw new ArgumentException("An admitted descriptor requires at least one segment and one complete partition.");
        }
        if (segmentCopy[0].Separator != StaticFieldV2ExpressionSeparatorKind.None ||
            segmentCopy.Skip(1).Any(static segment => segment.Separator == StaticFieldV2ExpressionSeparatorKind.None))
        {
            throw new ArgumentException("Only the first expression-name segment can have no dot edge.", nameof(segments));
        }
        if (parserCounts.ProjectedSegmentCount != segmentCopy.Length ||
            parserCounts.CompletePartitionCount != partitionCopy.Length ||
            parserCounts.NodeCount <= 0 || parserCounts.TokenCount <= 0 || parserCounts.MaximumDepth <= 0 ||
            (long)parserCounts.NodeCount + parserCounts.TokenCount > CSharpExpressionFrontEnd.MaximumNodeTokenCount ||
            parserCounts.MaximumDepth > CSharpExpressionFrontEnd.MaximumSyntaxDepth)
        {
            throw new ArgumentException("Parser counters do not describe one admitted complete projection.", nameof(parserCounts));
        }
        var firstSuffix = partitionCopy[0].Suffix;
        var argumentCount = segmentCopy.Sum(static segment =>
            segment.TypeArguments.Sum(static argument => 1 + argument.CumulativeArgumentCount));
        var rootTypeArguments = segmentCopy.SelectMany(static segment => segment.TypeArguments).ToArray();
        var maximumTopologyDepth = rootTypeArguments.Length == 0
            ? 0
            : rootTypeArguments.Max(static argument => argument.TopologyDepth);
        var cumulativeTopologyNodes = rootTypeArguments.Sum(static argument => argument.TopologyNodeCount);
        var maximumArrayRank = rootTypeArguments.Length == 0
            ? 0
            : rootTypeArguments.Max(MaximumArrayRankInTree);
        var identifierLengths = segmentCopy.Select(static segment => segment.Identifier.DecodedText.Length)
            .Concat(rootTypeArguments.SelectMany(EnumerateIdentifierLengths));
        if (aliasQualifier.Alias is not null)
        {
            identifierLengths = identifierLengths.Append(aliasQualifier.Alias.DecodedText.Length);
        }
        var maximumIdentifierLength = identifierLengths.DefaultIfEmpty(0).Max();
        var maximumFallbackStringLength = firstSuffix.StringFallback?.Length ?? 0;
        if (parserCounts.ProjectedTypeArgumentCount != argumentCount ||
            parserCounts.MaximumTypeTopologyDepth != maximumTopologyDepth ||
            parserCounts.CumulativeTypeTopologyNodeCount != cumulativeTopologyNodes ||
            parserCounts.MaximumArrayRank != maximumArrayRank ||
            parserCounts.MaximumDecodedIdentifierLength != maximumIdentifierLength ||
            parserCounts.MaximumDecodedFallbackStringLength != maximumFallbackStringLength)
        {
            throw new ArgumentException(
                "The parser type-topology counters do not match detached segment-local trees.",
                nameof(parserCounts));
        }
        if (aliasQualifier.Kind != StaticFieldV2AliasKind.None &&
            partitionCopy.Any(static partition => partition.CandidateKind == StaticFieldV2CandidateKind.BareMember))
        {
            throw new ArgumentException("An alias-qualified expression cannot produce a bare-member partition.", nameof(partitions));
        }

        if (partitionCopy.Any(partition =>
                partition.Suffix.FallbackKind != firstSuffix.FallbackKind ||
                partition.Suffix.Int32Fallback != firstSuffix.Int32Fallback ||
                !string.Equals(partition.Suffix.StringFallback, firstSuffix.StringFallback, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every partition must retain the same expression-level fallback payload.", nameof(partitions));
        }

        foreach (var partition in partitionCopy)
        {
            ValidatePartition(segmentCopy, partition, nameof(partitions));
        }
        if (partitionCopy.Select(static partition => partition.Sha256).Distinct(StringComparer.Ordinal).Count() !=
            partitionCopy.Length)
        {
            throw new ArgumentException("Complete structural partitions cannot repeat.", nameof(partitions));
        }
        var expectedPartitions = DerivePartitionsThroughFirstBound(
            aliasQualifier,
            segmentCopy,
            firstSuffix.FallbackKind,
            firstSuffix.Int32Fallback,
            firstSuffix.StringFallback);
        var suppliedKeys = partitionCopy.Select(static partition => partition.Sha256)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        var expectedKeys = expectedPartitions.Select(static partition => partition.Sha256)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        if (!suppliedKeys.SequenceEqual(expectedKeys, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The descriptor must retain every structurally possible field/type partition and no invented partition.",
                nameof(partitions));
        }

        var bounds = ExpressionV2ContractEncoding.NormalizeReachedBounds(reachedBounds, nameof(reachedBounds));
        var expectedBounds = ExpectedSyntaxReachedBounds(rawExpression, parserCounts);
        if (!BoundsEqual(bounds, expectedBounds))
        {
            throw new ArgumentException(
                "Reached bounds must equal the complete set implied by exact syntax counters.",
                nameof(reachedBounds));
        }

        return new StaticFieldV2ExpressionDescriptor(
            rawExpression,
            aliasQualifier,
            segmentCopy,
            ImmutableArray.CreateRange(partitionCopy.OrderBy(static partition => partition.Sha256, StringComparer.Ordinal)),
            parserCounts,
            bounds);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other descriptor.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2ExpressionDescriptor? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2ExpressionDescriptor);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static void ValidatePartition(
        ImmutableArray<StaticFieldV2ExpressionNameSegment> allSegments,
        StaticFieldV2StructuralPartition partition,
        string parameterName)
    {
        if (partition.FieldSegmentIndex >= allSegments.Length ||
            allSegments[partition.FieldSegmentIndex].TypeArguments.Length != 0)
        {
            throw new ArgumentException("A candidate field must be an in-range non-generic segment.", parameterName);
        }
        var expectedSuffixCount = allSegments.Length - partition.FieldSegmentIndex - 1;
        if (partition.Suffix.Segments.Length != expectedSuffixCount)
        {
            throw new ArgumentException("A partition suffix must cover every segment after its candidate field.", parameterName);
        }
        for (var index = 0; index < expectedSuffixCount; index++)
        {
            var expressionSegment = allSegments[partition.FieldSegmentIndex + index + 1];
            var suffixSegment = partition.Suffix.Segments[index];
            if (expressionSegment.TypeArguments.Length != 0 ||
                !expressionSegment.Identifier.Equals(suffixSegment.Identifier) ||
                expressionSegment.Separator switch
                {
                    StaticFieldV2ExpressionSeparatorKind.Dot =>
                        suffixSegment.AccessKind != DumpExpressionSuffixAccessKind.Direct,
                    StaticFieldV2ExpressionSeparatorKind.ConditionalDot =>
                        suffixSegment.AccessKind != DumpExpressionSuffixAccessKind.Conditional,
                    _ => true,
                })
            {
                throw new ArgumentException("The detached suffix and expression access edges disagree.", parameterName);
            }
        }
        if (allSegments.Take(partition.FieldSegmentIndex + 1)
            .Any(static segment => segment.Separator == StaticFieldV2ExpressionSeparatorKind.ConditionalDot))
        {
            throw new ArgumentException("Conditional access cannot occur in the namespace/type/field prefix.", parameterName);
        }
    }

    internal static ImmutableArray<StaticFieldV2StructuralPartition> DerivePartitionsThroughFirstBound(
        StaticFieldV2AliasQualifier aliasQualifier,
        ImmutableArray<StaticFieldV2ExpressionNameSegment> allSegments,
        DumpExpressionFallbackKind fallbackKind,
        int? int32Fallback,
        string? stringFallback)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldV2StructuralPartition>();
        var firstPossibleFieldIndex = Math.Max(0, allSegments.Length - 3);
        for (var fieldIndex = firstPossibleFieldIndex; fieldIndex < allSegments.Length; fieldIndex++)
        {
            if (allSegments[fieldIndex].TypeArguments.Length != 0 ||
                allSegments.Skip(fieldIndex + 1).Any(static segment => segment.TypeArguments.Length != 0) ||
                allSegments.Take(fieldIndex + 1)
                    .Any(static segment => segment.Separator == StaticFieldV2ExpressionSeparatorKind.ConditionalDot))
            {
                continue;
            }
            var suffixCount = allSegments.Length - fieldIndex - 1;
            if (suffixCount == 0 && fallbackKind != DumpExpressionFallbackKind.None)
            {
                continue;
            }
            var suffixSegments = allSegments.Skip(fieldIndex + 1)
                .Select(static segment => DumpExpressionSuffixSegment.Create(
                    segment.Identifier,
                    segment.Separator switch
                    {
                        StaticFieldV2ExpressionSeparatorKind.Dot => DumpExpressionSuffixAccessKind.Direct,
                        StaticFieldV2ExpressionSeparatorKind.ConditionalDot => DumpExpressionSuffixAccessKind.Conditional,
                        _ => throw new InvalidOperationException("A suffix segment must have an access edge."),
                    }))
                .ToImmutableArray();
            var suffixKind = suffixCount switch
            {
                0 => DumpExpressionSuffixKind.NotRequested,
                1 => DumpExpressionSuffixKind.DirectMember,
                2 => DumpExpressionSuffixKind.FixedDepthMemberChain,
                _ => throw new InvalidOperationException("Only the unchanged zero-, one-, or two-member suffix is admitted."),
            };
            var suffix = DumpExpressionSuffixDescriptor.Create(
                suffixKind,
                suffixSegments,
                fallbackKind,
                int32Fallback,
                stringFallback);

            if (fieldIndex == 0)
            {
                if (aliasQualifier.Kind == StaticFieldV2AliasKind.None)
                {
                    builder.Add(StaticFieldV2StructuralPartition.Create(
                        StaticFieldV2CandidateKind.BareMember,
                        fieldIndex,
                        null,
                        suffix));
                    if (builder.Count == StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1)
                    {
                        return builder.ToImmutable();
                    }
                }
                continue;
            }

            for (var topLevelTypeIndex = 0; topLevelTypeIndex < fieldIndex; topLevelTypeIndex++)
            {
                if (allSegments.Take(topLevelTypeIndex)
                    .Any(static segment => segment.TypeArguments.Length != 0))
                {
                    continue;
                }
                builder.Add(StaticFieldV2StructuralPartition.Create(
                    StaticFieldV2CandidateKind.QualifiedOwner,
                    fieldIndex,
                    topLevelTypeIndex,
                    suffix));
                if (builder.Count == StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1)
                {
                    return builder.ToImmutable();
                }
            }
        }
        if (builder.Count == 0)
        {
            throw new ArgumentException("The detached expression does not contain any admitted structural partition.");
        }
        return builder.ToImmutable();
    }

    private static int MaximumArrayRankInTree(StaticFieldV2TypeSyntax syntax)
    {
        var ownRank = syntax.ArrayRank ?? 0;
        var elementRank = syntax.ElementType is null ? 0 : MaximumArrayRankInTree(syntax.ElementType);
        var argumentRank = syntax.NameSegments
            .SelectMany(static segment => segment.TypeArguments)
            .Select(MaximumArrayRankInTree)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(ownRank, Math.Max(elementRank, argumentRank));
    }

    private static IEnumerable<int> EnumerateIdentifierLengths(StaticFieldV2TypeSyntax syntax)
    {
        if (syntax.AliasQualifier?.Alias is not null)
        {
            yield return syntax.AliasQualifier.Alias.DecodedText.Length;
        }
        foreach (var segment in syntax.NameSegments)
        {
            yield return segment.Identifier.DecodedText.Length;
            foreach (var argument in segment.TypeArguments)
            {
                foreach (var length in EnumerateIdentifierLengths(argument))
                {
                    yield return length;
                }
            }
        }
        if (syntax.ElementType is not null)
        {
            foreach (var length in EnumerateIdentifierLengths(syntax.ElementType))
            {
                yield return length;
            }
        }
    }

    internal static ImmutableArray<EvaluationDeterministicBound> ExpectedSyntaxReachedBounds(
        string rawExpression,
        StaticFieldV2ParserCounts counts)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        AddIf(rawExpression.Length >= StaticFieldV2Limits.MaximumExpressionCharacterCount,
            ExpressionV2ContractLimits.ExpressionCharacterCountBoundName);
        AddIf((long)counts.NodeCount + counts.TokenCount >= StaticFieldV2Limits.MaximumSyntaxNodeTokenCount,
            ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName);
        AddIf(counts.MaximumDepth >= StaticFieldV2Limits.MaximumSyntaxDepth,
            ExpressionV2ContractLimits.SyntaxDepthBoundName);
        AddIf(counts.ProjectedSegmentCount >= StaticFieldV2Limits.MaximumExpressionSegmentCount,
            ExpressionV2ContractLimits.ExpressionSegmentCountBoundName);
        AddIf(counts.CompletePartitionCount >= StaticFieldV2Limits.MaximumSyntaxPartitionCount,
            ExpressionV2ContractLimits.SyntaxPartitionCountBoundName);
        AddIf(counts.ProjectedTypeArgumentCount >= StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount,
            ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName);
        AddIf(counts.MaximumTypeTopologyDepth >= StaticFieldV2Limits.MaximumClosedTypeTopologyDepth,
            ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName);
        AddIf(counts.CumulativeTypeTopologyNodeCount >= StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount,
            ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName);
        AddIf(counts.MaximumArrayRank >= StaticFieldV2Limits.MaximumArrayRank,
            ExpressionV2ContractLimits.ArrayRankBoundName);
        AddIf(counts.MaximumDecodedIdentifierLength >= StaticFieldV2Limits.MaximumIdentifierCharacterCount,
            ExpressionV2ContractLimits.IdentifierCharacterCountBoundName);
        AddIf(counts.MaximumDecodedFallbackStringLength >= StaticFieldV2Limits.MaximumFallbackStringCharacterCount,
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

/// <summary>Freezes an admitted descriptor or one typed invalid/unsupported V2 syntax stop.</summary>
/// <remarks>This sealed draft outcome never carries a prefix partition after cap-plus-one is observed.</remarks>
public sealed class StaticFieldV2SyntaxOutcome : IEquatable<StaticFieldV2SyntaxOutcome>
{
    private const string CanonicalDomain = "static-field-v2-syntax-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<DumpExpressionDiagnostic> diagnostics;
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2SyntaxOutcome(
        DumpExpressionSyntaxStatus status,
        StaticFieldV2SyntaxIssue issue,
        string rawExpression,
        StaticFieldV2ParserCounts parserCounts,
        StaticFieldV2ExpressionDescriptor? descriptor,
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
        writer.WriteInt32((int)DumpExpressionProfileKind.StaticFieldExpressionV2);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteString(rawExpression);
        writer.WriteLengthPrefixedBytes(parserCounts.CanonicalBytes.AsSpan());
        writer.WriteBoolean(descriptor is not null);
        if (descriptor is not null)
        {
            writer.WriteLengthPrefixedBytes(descriptor.CanonicalBytes.AsSpan());
        }
        ExpressionV2ContractEncoding.WriteCanonicalArray(writer, diagnostics, static diagnostic => diagnostic.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteBounds(writer, reachedBounds);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the explicit profile represented by this outcome.</summary>
    public DumpExpressionProfileKind Profile => DumpExpressionProfileKind.StaticFieldExpressionV2;
    /// <summary>Gets the admitted, invalid, or unsupported disposition.</summary>
    public DumpExpressionSyntaxStatus Status { get; }
    /// <summary>Gets the first typed syntax issue.</summary>
    public StaticFieldV2SyntaxIssue Issue { get; }
    /// <summary>Gets the exact retained input text.</summary>
    public string RawExpression { get; }
    /// <summary>Gets exact parser and projection counters.</summary>
    public StaticFieldV2ParserCounts ParserCounts { get; }
    /// <summary>Gets the complete descriptor only for an admitted outcome.</summary>
    public StaticFieldV2ExpressionDescriptor? Descriptor { get; }
    /// <summary>Gets a defensive copy of stable structured diagnostics.</summary>
    public ImmutableArray<DumpExpressionDiagnostic> Diagnostics => ExpressionV2ContractEncoding.Copy(diagnostics);
    /// <summary>Gets a defensive copy of exact reached bounds.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => ExpressionV2ContractEncoding.Copy(reachedBounds);
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one admitted syntax outcome.</summary>
    /// <param name="descriptor">The complete admitted V2 descriptor.</param>
    /// <param name="diagnostics">Optional stable informational diagnostics.</param>
    /// <returns>A sealed immutable draft admitted outcome.</returns>
    public static StaticFieldV2SyntaxOutcome Admitted(
        StaticFieldV2ExpressionDescriptor descriptor,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var copied = ExpressionV2ContractEncoding.CopyRequired(diagnostics, nameof(diagnostics), maximumCount: 256);
        if (copied.Any(static diagnostic =>
                diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error ||
                diagnostic.Stage is not DumpExpressionDiagnosticStage.Parse and
                    not DumpExpressionDiagnosticStage.Projection))
        {
            throw new ArgumentException(
                "An admitted syntax outcome permits only non-error Parse or Projection diagnostics.",
                nameof(diagnostics));
        }
        return new StaticFieldV2SyntaxOutcome(
            DumpExpressionSyntaxStatus.Admitted,
            StaticFieldV2SyntaxIssue.None,
            descriptor.RawExpression,
            descriptor.ParserCounts,
            descriptor,
            copied,
            descriptor.ReachedBounds);
    }

    /// <summary>Creates one typed invalid syntax outcome.</summary>
    /// <param name="rawExpression">The exact retained bounded input text.</param>
    /// <param name="issue">One invalid-category issue.</param>
    /// <param name="parserCounts">Exact counters reached before the stop.</param>
    /// <param name="diagnostics">Stable diagnostics including at least one error.</param>
    /// <param name="reachedBounds">Exact declared bounds reached.</param>
    /// <returns>A sealed immutable draft invalid outcome.</returns>
    public static StaticFieldV2SyntaxOutcome Invalid(
        string rawExpression,
        StaticFieldV2SyntaxIssue issue,
        StaticFieldV2ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateStopped(DumpExpressionSyntaxStatus.Invalid, rawExpression, issue, parserCounts, diagnostics, reachedBounds);

    /// <summary>Creates one typed unsupported syntax outcome.</summary>
    /// <param name="rawExpression">The exact retained bounded input text.</param>
    /// <param name="issue">One unsupported-category issue.</param>
    /// <param name="parserCounts">Exact complete-parse counters.</param>
    /// <param name="diagnostics">Stable diagnostics including at least one error.</param>
    /// <param name="reachedBounds">Exact declared bounds reached.</param>
    /// <returns>A sealed immutable draft unsupported outcome with no prefix descriptor.</returns>
    public static StaticFieldV2SyntaxOutcome Unsupported(
        string rawExpression,
        StaticFieldV2SyntaxIssue issue,
        StaticFieldV2ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        CreateStopped(DumpExpressionSyntaxStatus.Unsupported, rawExpression, issue, parserCounts, diagnostics, reachedBounds);

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2SyntaxOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2SyntaxOutcome);
    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static StaticFieldV2SyntaxOutcome CreateStopped(
        DumpExpressionSyntaxStatus status,
        string rawExpression,
        StaticFieldV2SyntaxIssue issue,
        StaticFieldV2ParserCounts parserCounts,
        ImmutableArray<DumpExpressionDiagnostic> diagnostics,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ExpressionV2ContractEncoding.RequireText(
            rawExpression,
            nameof(rawExpression),
            CSharpExpressionFrontEnd.MaximumExpressionLength + 1,
            allowEmpty: true);
        ArgumentNullException.ThrowIfNull(parserCounts);
        ExpressionV2ContractEncoding.RequireDefined(issue, nameof(issue));
        var issueMatches = status switch
        {
            DumpExpressionSyntaxStatus.Invalid => (int)issue is >= 1 and < 100,
            DumpExpressionSyntaxStatus.Unsupported => (int)issue >= 100,
            _ => false,
        };
        if (!issueMatches)
        {
            throw new ArgumentException("The syntax issue does not belong to the selected stop disposition.", nameof(issue));
        }
        var copied = ExpressionV2ContractEncoding.CopyRequired(diagnostics, nameof(diagnostics), maximumCount: 256);
        if (copied.Any(static diagnostic =>
                diagnostic.Stage is not DumpExpressionDiagnosticStage.Parse and
                    not DumpExpressionDiagnosticStage.Projection))
        {
            throw new ArgumentException(
                "A syntax outcome permits only Parse or Projection diagnostics.",
                nameof(diagnostics));
        }
        var expectedErrorStage = status == DumpExpressionSyntaxStatus.Invalid
            ? DumpExpressionDiagnosticStage.Parse
            : DumpExpressionDiagnosticStage.Projection;
        if (!copied.Any(diagnostic =>
                diagnostic.Severity == DumpExpressionDiagnosticSeverity.Error &&
                diagnostic.Stage == expectedErrorStage))
        {
            throw new ArgumentException(
                "A stopped syntax outcome requires one stable Error diagnostic at the issue's pipeline stage.",
                nameof(diagnostics));
        }
        var bounds = ExpressionV2ContractEncoding.NormalizeReachedBounds(reachedBounds, nameof(reachedBounds));
        var expectedBounds = StaticFieldV2ExpressionDescriptor.ExpectedSyntaxReachedBounds(rawExpression, parserCounts);
        if (!StaticFieldV2ExpressionDescriptor.BoundsEqual(bounds, expectedBounds))
        {
            throw new ArgumentException(
                "Reached bounds must equal the complete set implied by exact stopped-outcome counters.",
                nameof(reachedBounds));
        }
        var firstReachedBoundary = FirstReachedBoundary(rawExpression, parserCounts);
        if (firstReachedBoundary != StaticFieldV2SyntaxIssue.None &&
            GetIssuePrecedence(firstReachedBoundary) < GetIssuePrecedence(issue))
        {
            throw new ArgumentException(
                "The selected issue occurs after an earlier reached syntax boundary.",
                nameof(issue));
        }
        ValidateNoPostStopCounters(issue, parserCounts);
        ValidateBoundIssue(issue, parserCounts, rawExpression, bounds);
        return new StaticFieldV2SyntaxOutcome(status, issue, rawExpression, parserCounts, null, copied, bounds);
    }

    private static void ValidateBoundIssue(
        StaticFieldV2SyntaxIssue issue,
        StaticFieldV2ParserCounts counts,
        string rawExpression,
        ImmutableArray<EvaluationDeterministicBound> bounds)
    {
        string? requiredBound = issue switch
        {
            StaticFieldV2SyntaxIssue.ExpressionBoundReached => ExpressionV2ContractLimits.ExpressionCharacterCountBoundName,
            StaticFieldV2SyntaxIssue.NodeTokenBoundReached => ExpressionV2ContractLimits.SyntaxNodeTokenCountBoundName,
            StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached => ExpressionV2ContractLimits.SyntaxDepthBoundName,
            StaticFieldV2SyntaxIssue.SegmentBoundReached => ExpressionV2ContractLimits.ExpressionSegmentCountBoundName,
            StaticFieldV2SyntaxIssue.PartitionBoundReached => ExpressionV2ContractLimits.SyntaxPartitionCountBoundName,
            StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached =>
                ExpressionV2ContractLimits.ClosedTypeTopologyDepthBoundName,
            StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached =>
                ExpressionV2ContractLimits.ClosedTypeTopologyNodeCountBoundName,
            StaticFieldV2SyntaxIssue.TypeArgumentBoundReached =>
                ExpressionV2ContractLimits.TypeSpecificationArgumentCountBoundName,
            StaticFieldV2SyntaxIssue.ArrayRankBoundReached => ExpressionV2ContractLimits.ArrayRankBoundName,
            StaticFieldV2SyntaxIssue.IdentifierBoundReached =>
                ExpressionV2ContractLimits.IdentifierCharacterCountBoundName,
            StaticFieldV2SyntaxIssue.FallbackStringBoundReached =>
                ExpressionV2ContractLimits.FallbackStringCharacterCountBoundName,
            _ => null,
        };
        if (issue == StaticFieldV2SyntaxIssue.ExpressionBoundReached &&
            rawExpression.Length <= CSharpExpressionFrontEnd.MaximumExpressionLength)
        {
            throw new ArgumentException("The expression-bound issue requires a cap-plus-one text observation.", nameof(rawExpression));
        }
        if (issue == StaticFieldV2SyntaxIssue.NodeTokenBoundReached &&
            (long)counts.NodeCount + counts.TokenCount != CSharpExpressionFrontEnd.MaximumNodeTokenCount + 1L ||
            issue == StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached &&
            counts.MaximumDepth != CSharpExpressionFrontEnd.MaximumSyntaxDepth + 1 ||
            issue == StaticFieldV2SyntaxIssue.SegmentBoundReached &&
            counts.ProjectedSegmentCount != StaticFieldV2Limits.MaximumExpressionSegmentCount + 1 ||
            issue == StaticFieldV2SyntaxIssue.PartitionBoundReached &&
            counts.CompletePartitionCount != StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1 ||
            issue == StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached &&
            counts.MaximumTypeTopologyDepth != StaticFieldV2Limits.MaximumClosedTypeTopologyDepth + 1 ||
            issue == StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached &&
            counts.CumulativeTypeTopologyNodeCount != StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount + 1 ||
            issue == StaticFieldV2SyntaxIssue.TypeArgumentBoundReached &&
            counts.ProjectedTypeArgumentCount != StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1 ||
            issue == StaticFieldV2SyntaxIssue.ArrayRankBoundReached &&
            counts.MaximumArrayRank != StaticFieldV2Limits.MaximumArrayRank + 1 ||
            issue == StaticFieldV2SyntaxIssue.IdentifierBoundReached &&
            counts.MaximumDecodedIdentifierLength != StaticFieldV2Limits.MaximumIdentifierCharacterCount + 1 ||
            issue == StaticFieldV2SyntaxIssue.FallbackStringBoundReached &&
            counts.MaximumDecodedFallbackStringLength != StaticFieldV2Limits.MaximumFallbackStringCharacterCount + 1)
        {
            throw new ArgumentException("The cap-plus-one issue and exact counter disagree.", nameof(counts));
        }
        if (requiredBound is not null && !bounds.Any(bound => string.Equals(bound.Name, requiredBound, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The typed bound issue requires its exact declared reached bound.", nameof(bounds));
        }
    }

    private static StaticFieldV2SyntaxIssue FirstReachedBoundary(
        string rawExpression,
        StaticFieldV2ParserCounts counts)
    {
        if (rawExpression.Length == CSharpExpressionFrontEnd.MaximumExpressionLength + 1)
        {
            return StaticFieldV2SyntaxIssue.ExpressionBoundReached;
        }
        if ((long)counts.NodeCount + counts.TokenCount == CSharpExpressionFrontEnd.MaximumNodeTokenCount + 1L)
        {
            return StaticFieldV2SyntaxIssue.NodeTokenBoundReached;
        }
        if (counts.MaximumDepth == CSharpExpressionFrontEnd.MaximumSyntaxDepth + 1)
        {
            return StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached;
        }
        if (counts.MaximumDecodedIdentifierLength == StaticFieldV2Limits.MaximumIdentifierCharacterCount + 1)
        {
            return StaticFieldV2SyntaxIssue.IdentifierBoundReached;
        }
        if (counts.MaximumDecodedFallbackStringLength == StaticFieldV2Limits.MaximumFallbackStringCharacterCount + 1)
        {
            return StaticFieldV2SyntaxIssue.FallbackStringBoundReached;
        }
        if (counts.ProjectedSegmentCount == StaticFieldV2Limits.MaximumExpressionSegmentCount + 1)
        {
            return StaticFieldV2SyntaxIssue.SegmentBoundReached;
        }
        if (counts.MaximumTypeTopologyDepth == StaticFieldV2Limits.MaximumClosedTypeTopologyDepth + 1)
        {
            return StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached;
        }
        if (counts.CumulativeTypeTopologyNodeCount == StaticFieldV2Limits.MaximumClosedTypeTopologyNodeCount + 1)
        {
            return StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached;
        }
        if (counts.ProjectedTypeArgumentCount == StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount + 1)
        {
            return StaticFieldV2SyntaxIssue.TypeArgumentBoundReached;
        }
        if (counts.MaximumArrayRank == StaticFieldV2Limits.MaximumArrayRank + 1)
        {
            return StaticFieldV2SyntaxIssue.ArrayRankBoundReached;
        }
        if (counts.CompletePartitionCount == StaticFieldV2Limits.MaximumSyntaxPartitionCount + 1)
        {
            return StaticFieldV2SyntaxIssue.PartitionBoundReached;
        }
        return StaticFieldV2SyntaxIssue.None;
    }

    private static void ValidateNoPostStopCounters(
        StaticFieldV2SyntaxIssue issue,
        StaticFieldV2ParserCounts counts)
    {
        var allZero = counts.NodeCount == 0 && counts.TokenCount == 0 && counts.MaximumDepth == 0 &&
            counts.MaximumDecodedIdentifierLength == 0 && counts.MaximumDecodedFallbackStringLength == 0 &&
            ProjectionCountersAreZero(counts);
        var afterNodeZero = counts.MaximumDepth == 0 && counts.MaximumDecodedIdentifierLength == 0 &&
            counts.MaximumDecodedFallbackStringLength == 0 && ProjectionCountersAreZero(counts);
        var afterDepthZero = counts.MaximumDecodedIdentifierLength == 0 &&
            counts.MaximumDecodedFallbackStringLength == 0 && ProjectionCountersAreZero(counts);
        var valid = issue switch
        {
            StaticFieldV2SyntaxIssue.ExpressionBoundReached or
            StaticFieldV2SyntaxIssue.ParseError or
            StaticFieldV2SyntaxIssue.ParserDiagnostic or
            StaticFieldV2SyntaxIssue.ParserIntegrityFailure => allZero,
            StaticFieldV2SyntaxIssue.NodeTokenBoundReached => afterNodeZero,
            StaticFieldV2SyntaxIssue.SyntaxDepthBoundReached => afterDepthZero,
            StaticFieldV2SyntaxIssue.IdentifierBoundReached =>
                counts.MaximumDecodedFallbackStringLength == 0 && ProjectionCountersAreZero(counts),
            StaticFieldV2SyntaxIssue.FallbackStringBoundReached => ProjectionCountersAreZero(counts),
            StaticFieldV2SyntaxIssue.TreeShapeUnsupported => ProjectionCountersAreZero(counts),
            StaticFieldV2SyntaxIssue.SegmentBoundReached => TypeAndPartitionCountersAreZero(counts),
            StaticFieldV2SyntaxIssue.TypeTopologyDepthBoundReached =>
                counts.CumulativeTypeTopologyNodeCount == 0 &&
                counts.ProjectedTypeArgumentCount == 0 &&
                counts.MaximumArrayRank == 0 &&
                counts.CompletePartitionCount == 0,
            StaticFieldV2SyntaxIssue.TypeTopologyNodeBoundReached =>
                counts.ProjectedTypeArgumentCount == 0 &&
                counts.MaximumArrayRank == 0 &&
                counts.CompletePartitionCount == 0,
            StaticFieldV2SyntaxIssue.TypeArgumentBoundReached =>
                counts.MaximumArrayRank == 0 && counts.CompletePartitionCount == 0,
            StaticFieldV2SyntaxIssue.ArrayRankBoundReached => counts.CompletePartitionCount == 0,
            _ => true,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "Parser and projection counters after the selected first stop must remain zero.",
                nameof(counts));
        }

        static bool ProjectionCountersAreZero(StaticFieldV2ParserCounts value) =>
            value.ProjectedSegmentCount == 0 && TypeAndPartitionCountersAreZero(value);

        static bool TypeAndPartitionCountersAreZero(StaticFieldV2ParserCounts value) =>
            value.ProjectedTypeArgumentCount == 0 &&
            value.MaximumTypeTopologyDepth == 0 &&
            value.CumulativeTypeTopologyNodeCount == 0 &&
            value.MaximumArrayRank == 0 &&
            value.CompletePartitionCount == 0;
    }

    private static int GetIssuePrecedence(StaticFieldV2SyntaxIssue issue) => (int)issue;
}
