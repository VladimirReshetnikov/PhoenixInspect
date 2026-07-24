using System.Collections.Immutable;
using System.Globalization;
using PhoenixInspect.Core.Abstractions;
using Microsoft.CodeAnalysis.CSharp;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies the outcome of one compiler-name mapping derivation.</summary>
/// <remarks>
/// The discriminator keeps an exact name projection separate from a coherent physical input for which no complete
/// compiler mapping can be derived. This contract is additive and is not yet a compatibility commitment.
/// </remarks>
public enum MetadataCompilerNameMappingResultKind
{
    /// <summary>The physical arities compose and every separate mapping fact is exact.</summary>
    Exact = 1,

    /// <summary>The physical input is exact, but a complete compiler mapping cannot be derived.</summary>
    NonExact = 2,
}

/// <summary>Identifies the typed issue carried by one compiler-name mapping.</summary>
/// <remarks>The issue does not overload CLS, Roslyn, source-name, or evaluator dispositions.</remarks>
public enum MetadataCompilerNameMappingIssue
{
    /// <summary>No issue applies to an exact mapping.</summary>
    None = 0,

    /// <summary>A nested type's total physical arity was less than its enclosing type's total physical arity.</summary>
    NestedTotalArityUnderflow = 1,
}

/// <summary>Classifies strict CLS generic-arity spelling for one metadata name.</summary>
/// <remarks>
/// This status describes only the terminal arity convention. It does not classify the rest of the name as a CLS
/// identifier and does not imply C# source-name addressability.
/// </remarks>
public enum MetadataClsAritySpellingStatus
{
    /// <summary>The terminal suffix exactly represents the positive introduced arity, or no suffix is needed.</summary>
    Canonical = 1,

    /// <summary>The terminal suffix is absent, malformed, unexpected, or different from the introduced arity.</summary>
    NonCanonical = 2,
}

/// <summary>Classifies the pinned Roslyn-compatible projection of one metadata name.</summary>
/// <remarks>This status is independent of CLS spelling and evaluator admission.</remarks>
public enum MetadataRoslynNameProjectionStatus
{
    /// <summary>The final terminal arity suffix matched a positive introduced arity and was removed.</summary>
    TerminalAritySuffixRemoved = 1,

    /// <summary>The complete raw metadata name was retained as the projected simple name.</summary>
    RawMetadataNameRetained = 2,
}

/// <summary>Classifies whether one projected name can be written as an ordinary C# simple name.</summary>
/// <remarks>
/// Reserved keywords may remain addressable through a verbatim identifier. This status does not perform namespace or
/// member binding and is not a claim that a complete qualified type is unambiguous.
/// </remarks>
public enum MetadataCSharpSimpleNameAddressabilityStatus
{
    /// <summary>The projected name is an identifier value or a keyword value that has a source spelling.</summary>
    Addressable = 1,

    /// <summary>The projected name has no ordinary C# simple-name spelling.</summary>
    NotAddressable = 2,
}

/// <summary>Classifies the current evaluator generic-arity admission of one exact mapping.</summary>
/// <remarks>The disposition is an operation bound and is not a physical metadata validity rule.</remarks>
public enum MetadataEvaluatorGenericArityAdmissionStatus
{
    /// <summary>The total physical arity is within the evaluator's current admitted ceiling.</summary>
    Admitted = 1,

    /// <summary>The total physical arity exceeds the evaluator's current admitted ceiling.</summary>
    TotalArityLimitExceeded = 2,
}

/// <summary>Freezes the authority-issued physical input to one compiler-name mapping derivation.</summary>
/// <remarks>
/// The input retains the exact TypeDef authority row and its optional immediate enclosing authority row. Its decoded
/// name and physical arities are derived views of those rows rather than independently supplied claims. It accepts no
/// legacy W7 definition object and makes no semantic-name claim by itself.
/// </remarks>
public sealed class MetadataCompilerNameMappingInputIdentity :
    IEquatable<MetadataCompilerNameMappingInputIdentity>
{
    private const string CanonicalDomain = "metadata-v2-compiler-name-mapping-input";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataCompilerNameMappingInputIdentity(
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        MetadataTypeDefinitionAuthorityIdentity? enclosingTypeDefinition)
    {
        TypeDefinition = typeDefinition;
        EnclosingTypeDefinition = enclosingTypeDefinition;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32(typeDefinition.TypeDefinitionToken);
        writer.WriteString(typeDefinition.Sha256);
        writer.WriteBoolean(enclosingTypeDefinition is not null);
        if (enclosingTypeDefinition is not null)
        {
            writer.WriteInt32(enclosingTypeDefinition.TypeDefinitionToken);
            writer.WriteString(enclosingTypeDefinition.Sha256);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact authority-issued TypeDef row classified by this input.</summary>
    public MetadataTypeDefinitionAuthorityIdentity TypeDefinition { get; }

    /// <summary>Gets the exact immediate enclosing authority row, or null for a top-level TypeDef.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? EnclosingTypeDefinition { get; }

    /// <summary>Gets the exact decoded metadata name before any compiler-style interpretation.</summary>
    public string RawMetadataName => TypeDefinition.TypeName;

    /// <summary>Gets the complete physical GenericParam count owned by this TypeDef.</summary>
    public int TotalGenericArity => TypeDefinition.TotalGenericArity;

    /// <summary>Gets the enclosing TypeDef's complete physical arity, or null for a top-level TypeDef.</summary>
    public int? EnclosingTotalGenericArity => EnclosingTypeDefinition?.TotalGenericArity;

    /// <summary>Gets whether this input represents a nested TypeDef segment.</summary>
    public bool IsNested => TypeDefinition.IsNested;

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two physical compiler-name inputs.</summary>
    /// <param name="other">The other input.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataCompilerNameMappingInputIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests physical compiler-name input equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an input with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataCompilerNameMappingInputIdentity);

    /// <summary>Computes a hash code from the immutable physical compiler-name input.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataCompilerNameMappingInputIdentity Create(
        object mintCapability,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        MetadataTypeDefinitionAuthorityIdentity? enclosingTypeDefinition)
    {
        if (!MetadataCompilerNameMappingIdentity.OwnsInputMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A compiler-name mapping input requires the mapping derivation's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(typeDefinition);
        if (typeDefinition.IsNested != (enclosingTypeDefinition is not null))
        {
            throw new ArgumentException(
                "A mapping input must retain an enclosing authority row exactly when its TypeDef is nested.",
                nameof(enclosingTypeDefinition));
        }
        if (enclosingTypeDefinition is not null &&
            (!typeDefinition.SourceEnds.Equals(enclosingTypeDefinition.SourceEnds) ||
             typeDefinition.EnclosingTypeDefinitionToken != enclosingTypeDefinition.TypeDefinitionToken ||
             typeDefinition.NestingDepth != enclosingTypeDefinition.NestingDepth + 1))
        {
            throw new ArgumentException(
                "The enclosing authority row must be the immediate same-source parent of the mapped TypeDef.",
                nameof(enclosingTypeDefinition));
        }

        return new MetadataCompilerNameMappingInputIdentity(
            typeDefinition,
            enclosingTypeDefinition);
    }
}

/// <summary>Freezes strict CLS arity-spelling facts for one exact mapping.</summary>
/// <remarks>
/// The fact is minted only with the mapping aggregate's private capability. A valid terminal arity is retained for
/// explanation even when it differs from the introduced physical arity.
/// </remarks>
public sealed class MetadataClsAritySpellingIdentity : IEquatable<MetadataClsAritySpellingIdentity>
{
    private const string CanonicalDomain = "metadata-v2-cls-arity-spelling";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataClsAritySpellingIdentity(
        MetadataCompilerNameMappingInputIdentity input,
        int introducedGenericArity,
        MetadataClsAritySpellingStatus status,
        int? parsedTerminalArity)
    {
        Input = input;
        IntroducedGenericArity = introducedGenericArity;
        Status = status;
        ParsedTerminalArity = parsedTerminalArity;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(input.CanonicalBytes.AsSpan());
        writer.WriteInt32(introducedGenericArity);
        writer.WriteInt32((int)status);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, parsedTerminalArity);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged physical input retained by this spelling fact.</summary>
    public MetadataCompilerNameMappingInputIdentity Input { get; }

    /// <summary>Gets the exact parent-relative generic arity classified by this spelling fact.</summary>
    public int IntroducedGenericArity { get; }

    /// <summary>Gets the strict CLS arity-spelling disposition.</summary>
    public MetadataClsAritySpellingStatus Status { get; }

    /// <summary>Gets the valid positive terminal arity, or null when the terminal text has no valid Roslyn form.</summary>
    public int? ParsedTerminalArity { get; }

    /// <summary>Gets whether the raw name has canonical CLS arity spelling.</summary>
    public bool IsCanonical => Status == MetadataClsAritySpellingStatus.Canonical;

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two CLS arity-spelling facts.</summary>
    /// <param name="other">The other fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataClsAritySpellingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests CLS arity-spelling equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataClsAritySpellingIdentity);

    /// <summary>Computes a hash code from immutable CLS arity-spelling content.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataClsAritySpellingIdentity Create(
        object mintCapability,
        MetadataCompilerNameMappingInputIdentity input,
        int introducedGenericArity)
    {
        if (!MetadataCompilerNameMappingIdentity.OwnsFactMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A CLS arity-spelling fact requires the mapping aggregate's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(input);
        if (introducedGenericArity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(introducedGenericArity));
        }

        var hasTerminalArity = MetadataCompilerNameMappingRules.TryParseTerminalArity(
            input.RawMetadataName,
            out var terminalPrefix,
            out var terminalArity);
        var canonical = introducedGenericArity == 0
            ? input.RawMetadataName.IndexOf('`') < 0
            : hasTerminalArity &&
              terminalArity == introducedGenericArity &&
              terminalPrefix.IndexOf('`') < 0;
        return new MetadataClsAritySpellingIdentity(
            input,
            introducedGenericArity,
            canonical
                ? MetadataClsAritySpellingStatus.Canonical
                : MetadataClsAritySpellingStatus.NonCanonical,
            hasTerminalArity ? terminalArity : null);
    }
}

/// <summary>Freezes one pinned Roslyn-compatible metadata-name projection for an exact mapping.</summary>
/// <remarks>
/// The projection examines only the final backtick. It removes a valid positive suffix only when that suffix equals
/// the exact introduced arity; otherwise the complete raw name remains visible.
/// </remarks>
public sealed class MetadataRoslynNameProjectionIdentity : IEquatable<MetadataRoslynNameProjectionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-roslyn-name-projection";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataRoslynNameProjectionIdentity(
        MetadataCompilerNameMappingInputIdentity input,
        int introducedGenericArity,
        MetadataRoslynNameProjectionStatus status,
        string projectedSimpleName)
    {
        Input = input;
        IntroducedGenericArity = introducedGenericArity;
        Status = status;
        ProjectedSimpleName = projectedSimpleName;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(input.CanonicalBytes.AsSpan());
        writer.WriteInt32(introducedGenericArity);
        writer.WriteInt32((int)status);
        writer.WriteString(projectedSimpleName);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged physical input retained by this Roslyn projection.</summary>
    public MetadataCompilerNameMappingInputIdentity Input { get; }

    /// <summary>Gets the exact parent-relative generic arity used by the projection.</summary>
    public int IntroducedGenericArity { get; }

    /// <summary>Gets whether the final arity suffix was removed or the raw metadata name was retained.</summary>
    public MetadataRoslynNameProjectionStatus Status { get; }

    /// <summary>Gets the Roslyn-compatible projected simple name.</summary>
    public string ProjectedSimpleName { get; }

    /// <summary>Gets whether one matching terminal arity suffix was removed.</summary>
    public bool WasTerminalAritySuffixRemoved =>
        Status == MetadataRoslynNameProjectionStatus.TerminalAritySuffixRemoved;

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two Roslyn name-projection facts.</summary>
    /// <param name="other">The other fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataRoslynNameProjectionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests Roslyn name-projection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataRoslynNameProjectionIdentity);

    /// <summary>Computes a hash code from immutable Roslyn name-projection content.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataRoslynNameProjectionIdentity Create(
        object mintCapability,
        MetadataCompilerNameMappingInputIdentity input,
        int introducedGenericArity)
    {
        if (!MetadataCompilerNameMappingIdentity.OwnsFactMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A Roslyn name projection requires the mapping aggregate's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(input);
        if (introducedGenericArity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(introducedGenericArity));
        }

        var prefix = string.Empty;
        var terminalArity = 0;
        var removeSuffix = introducedGenericArity > 0 &&
                            MetadataCompilerNameMappingRules.TryParseTerminalArity(
                                input.RawMetadataName,
                                out prefix,
                                out terminalArity) &&
                            terminalArity == introducedGenericArity;
        return new MetadataRoslynNameProjectionIdentity(
            input,
            introducedGenericArity,
            removeSuffix
                ? MetadataRoslynNameProjectionStatus.TerminalAritySuffixRemoved
                : MetadataRoslynNameProjectionStatus.RawMetadataNameRetained,
            removeSuffix ? prefix : input.RawMetadataName);
    }
}

/// <summary>Freezes ordinary C# simple-name addressability for one exact Roslyn projection.</summary>
/// <remarks>
/// The fact uses the pinned Roslyn lexical rules. It records verbatim escaping separately so addressable keyword
/// values are not confused with names that have no ordinary source spelling.
/// </remarks>
public sealed class MetadataCSharpSimpleNameAddressabilityIdentity :
    IEquatable<MetadataCSharpSimpleNameAddressabilityIdentity>
{
    private const string CanonicalDomain = "metadata-v2-csharp-simple-name-addressability";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataCSharpSimpleNameAddressabilityIdentity(
        MetadataRoslynNameProjectionIdentity projection,
        MetadataCSharpSimpleNameAddressabilityStatus status,
        bool requiresVerbatimEscape,
        string? sourceSpelling)
    {
        Projection = projection;
        Status = status;
        RequiresVerbatimEscape = requiresVerbatimEscape;
        SourceSpelling = sourceSpelling;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(projection.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)status);
        writer.WriteBoolean(requiresVerbatimEscape);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, sourceSpelling);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact Roslyn projection classified by this addressability fact.</summary>
    public MetadataRoslynNameProjectionIdentity Projection { get; }

    /// <summary>Gets the ordinary C# simple-name addressability disposition.</summary>
    public MetadataCSharpSimpleNameAddressabilityStatus Status { get; }

    /// <summary>Gets whether the source spelling must begin with <c>@</c>.</summary>
    public bool RequiresVerbatimEscape { get; }

    /// <summary>Gets the ordinary C# source spelling, or null when the projected name is not addressable.</summary>
    public string? SourceSpelling { get; }

    /// <summary>Gets whether the projected simple name has an ordinary C# source spelling.</summary>
    public bool IsAddressable => Status == MetadataCSharpSimpleNameAddressabilityStatus.Addressable;

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two C# simple-name addressability facts.</summary>
    /// <param name="other">The other fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataCSharpSimpleNameAddressabilityIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests C# simple-name addressability equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataCSharpSimpleNameAddressabilityIdentity);

    /// <summary>Computes a hash code from immutable C# simple-name addressability content.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataCSharpSimpleNameAddressabilityIdentity Create(
        object mintCapability,
        MetadataRoslynNameProjectionIdentity projection)
    {
        if (!MetadataCompilerNameMappingIdentity.OwnsFactMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A C# simple-name addressability fact requires the mapping aggregate's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(projection);
        var name = projection.ProjectedSimpleName;
        var isIdentifier = SyntaxFacts.IsValidIdentifier(name);
        var isReservedKeyword = SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None;
        var isContextualKeyword = SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None;
        var containsDroppedCharacters = MetadataCompilerNameMappingRules.ContainsDroppedIdentifierCharacters(name);
        var isAddressable =
            (isIdentifier || isReservedKeyword || isContextualKeyword) && !containsDroppedCharacters;
        var requiresVerbatimEscape = isAddressable && isReservedKeyword;
        return new MetadataCSharpSimpleNameAddressabilityIdentity(
            projection,
            isAddressable
                ? MetadataCSharpSimpleNameAddressabilityStatus.Addressable
                : MetadataCSharpSimpleNameAddressabilityStatus.NotAddressable,
            requiresVerbatimEscape,
            !isAddressable
                ? null
                : requiresVerbatimEscape
                    ? $"@{name}"
                    : name);
    }
}

/// <summary>Freezes the evaluator generic-arity admission for one exact mapping.</summary>
/// <remarks>
/// The fact retains the physical total arity and the current operation ceiling. Exceeding the ceiling does not make
/// the physical input, CLS spelling, Roslyn projection, or C# addressability non-exact.
/// </remarks>
public sealed class MetadataEvaluatorGenericArityAdmissionIdentity :
    IEquatable<MetadataEvaluatorGenericArityAdmissionIdentity>
{
    private const string CanonicalDomain = "metadata-v2-evaluator-generic-arity-admission";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataEvaluatorGenericArityAdmissionIdentity(
        MetadataCompilerNameMappingInputIdentity input,
        MetadataEvaluatorGenericArityAdmissionStatus status)
    {
        Input = input;
        Status = status;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteLengthPrefixedBytes(input.CanonicalBytes.AsSpan());
        writer.WriteInt32((int)status);
        writer.WriteInt32(StaticFieldV2Limits.MaximumGenericParameterCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the unchanged physical input classified by this admission fact.</summary>
    public MetadataCompilerNameMappingInputIdentity Input { get; }

    /// <summary>Gets the evaluator generic-arity admission disposition.</summary>
    public MetadataEvaluatorGenericArityAdmissionStatus Status { get; }

    /// <summary>Gets the exact physical total arity tested by the evaluator admission rule.</summary>
    public int ObservedTotalGenericArity => Input.TotalGenericArity;

    /// <summary>Gets the current maximum total generic arity admitted by the evaluator.</summary>
    public int MaximumAdmittedTotalGenericArity => StaticFieldV2Limits.MaximumGenericParameterCount;

    /// <summary>Gets whether the exact mapping is admitted by the evaluator's current arity ceiling.</summary>
    public bool IsAdmitted => Status == MetadataEvaluatorGenericArityAdmissionStatus.Admitted;

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two evaluator generic-arity admission facts.</summary>
    /// <param name="other">The other fact.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataEvaluatorGenericArityAdmissionIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests evaluator generic-arity admission equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a fact with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataEvaluatorGenericArityAdmissionIdentity);

    /// <summary>Computes a hash code from immutable evaluator generic-arity admission content.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataEvaluatorGenericArityAdmissionIdentity Create(
        object mintCapability,
        MetadataCompilerNameMappingInputIdentity input)
    {
        if (!MetadataCompilerNameMappingIdentity.OwnsFactMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An evaluator arity-admission fact requires the mapping aggregate's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(input);
        return new MetadataEvaluatorGenericArityAdmissionIdentity(
            input,
            input.TotalGenericArity <= StaticFieldV2Limits.MaximumGenericParameterCount
                ? MetadataEvaluatorGenericArityAdmissionStatus.Admitted
                : MetadataEvaluatorGenericArityAdmissionStatus.TotalArityLimitExceeded);
    }
}

/// <summary>Freezes all non-conflated compiler-name facts derived from one authority-issued TypeDef.</summary>
/// <remarks>
/// Exact outcomes retain separate CLS, Roslyn, C# source-name, and evaluator-admission facts. Nested arity underflow
/// retains the authority-bound physical input alone and exposes no semantic projection prefix. Only a complete
/// <see cref="MetadataCompilerNameMappingCatalogIdentity"/> can mint this certificate.
/// </remarks>
public sealed class MetadataCompilerNameMappingIdentity : IEquatable<MetadataCompilerNameMappingIdentity>
{
    private const string CanonicalDomain = "metadata-v2-compiler-name-mapping";
    private const int CanonicalSchemaVersion = 2;
    private static readonly object InputMintCapability = new();
    private static readonly object FactMintCapability = new();
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataCompilerNameMappingIdentity(
        MetadataCompilerNameMappingResultKind resultKind,
        MetadataCompilerNameMappingIssue issue,
        MetadataCompilerNameMappingInputIdentity input,
        int? introducedGenericArity,
        MetadataClsAritySpellingIdentity? clsAritySpelling,
        MetadataRoslynNameProjectionIdentity? roslynProjection,
        MetadataCSharpSimpleNameAddressabilityIdentity? csharpAddressability,
        MetadataEvaluatorGenericArityAdmissionIdentity? evaluatorAdmission)
    {
        ResultKind = resultKind;
        Issue = issue;
        Input = input;
        IntroducedGenericArity = introducedGenericArity;
        ClsAritySpelling = clsAritySpelling;
        RoslynProjection = roslynProjection;
        CSharpAddressability = csharpAddressability;
        EvaluatorAdmission = evaluatorAdmission;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(input.CanonicalBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, introducedGenericArity);
        WriteOptionalFact(writer, clsAritySpelling?.CanonicalBytes);
        WriteOptionalFact(writer, roslynProjection?.CanonicalBytes);
        WriteOptionalFact(writer, csharpAddressability?.CanonicalBytes);
        WriteOptionalFact(writer, evaluatorAdmission?.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether the complete mapping is exact or stopped without a semantic projection.</summary>
    public MetadataCompilerNameMappingResultKind ResultKind { get; }

    /// <summary>Gets the typed mapping issue, or none for an exact mapping.</summary>
    public MetadataCompilerNameMappingIssue Issue { get; }

    /// <summary>Gets the unchanged physical input retained by every outcome.</summary>
    public MetadataCompilerNameMappingInputIdentity Input { get; }

    /// <summary>Gets the exact authority-issued TypeDef row classified by this mapping.</summary>
    public MetadataTypeDefinitionAuthorityIdentity TypeDefinition => Input.TypeDefinition;

    /// <summary>Gets the exact immediate enclosing authority row, or null for a top-level TypeDef.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? EnclosingTypeDefinition => Input.EnclosingTypeDefinition;

    /// <summary>Gets the derived parent-relative arity, or null when nested physical arities underflow.</summary>
    public int? IntroducedGenericArity { get; }

    /// <summary>Gets the strict CLS arity-spelling fact, or null for a non-exact mapping.</summary>
    public MetadataClsAritySpellingIdentity? ClsAritySpelling { get; }

    /// <summary>Gets the pinned Roslyn name projection, or null for a non-exact mapping.</summary>
    public MetadataRoslynNameProjectionIdentity? RoslynProjection { get; }

    /// <summary>Gets ordinary C# simple-name addressability, or null for a non-exact mapping.</summary>
    public MetadataCSharpSimpleNameAddressabilityIdentity? CSharpAddressability { get; }

    /// <summary>Gets evaluator generic-arity admission, or null for a non-exact mapping.</summary>
    public MetadataEvaluatorGenericArityAdmissionIdentity? EvaluatorAdmission { get; }

    /// <summary>Gets a defensive copy of the versioned canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two complete compiler-name mappings.</summary>
    /// <param name="other">The other mapping.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataCompilerNameMappingIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests complete compiler-name mapping equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a mapping with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataCompilerNameMappingIdentity);

    /// <summary>Computes a hash code from immutable compiler-name mapping content.</summary>
    /// <returns>A deterministic hash code for canonical content.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static MetadataCompilerNameMappingIdentity Create(
        object mintCapability,
        MetadataTypeDefinitionAuthorityIdentity typeDefinition,
        MetadataTypeDefinitionAuthorityIdentity? enclosingTypeDefinition)
    {
        if (!MetadataCompilerNameMappingCatalogIdentity.OwnsMappingMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A compiler-name mapping requires the complete mapping catalog's private mint capability.",
                nameof(mintCapability));
        }

        var input = MetadataCompilerNameMappingInputIdentity.Create(
            InputMintCapability,
            typeDefinition,
            enclosingTypeDefinition);
        if (input.EnclosingTotalGenericArity.HasValue &&
            input.TotalGenericArity < input.EnclosingTotalGenericArity.Value)
        {
            return new MetadataCompilerNameMappingIdentity(
                MetadataCompilerNameMappingResultKind.NonExact,
                MetadataCompilerNameMappingIssue.NestedTotalArityUnderflow,
                input,
                introducedGenericArity: null,
                clsAritySpelling: null,
                roslynProjection: null,
                csharpAddressability: null,
                evaluatorAdmission: null);
        }

        var introducedGenericArity = input.EnclosingTotalGenericArity.HasValue
            ? input.TotalGenericArity - input.EnclosingTotalGenericArity.Value
            : input.TotalGenericArity;
        var clsAritySpelling = MetadataClsAritySpellingIdentity.Create(
            FactMintCapability,
            input,
            introducedGenericArity);
        var roslynProjection = MetadataRoslynNameProjectionIdentity.Create(
            FactMintCapability,
            input,
            introducedGenericArity);
        var csharpAddressability = MetadataCSharpSimpleNameAddressabilityIdentity.Create(
            FactMintCapability,
            roslynProjection);
        var evaluatorAdmission = MetadataEvaluatorGenericArityAdmissionIdentity.Create(
            FactMintCapability,
            input);
        return new MetadataCompilerNameMappingIdentity(
            MetadataCompilerNameMappingResultKind.Exact,
            MetadataCompilerNameMappingIssue.None,
            input,
            introducedGenericArity,
            clsAritySpelling,
            roslynProjection,
            csharpAddressability,
            evaluatorAdmission);
    }

    internal static bool OwnsInputMintCapability(object? capability) =>
        ReferenceEquals(capability, InputMintCapability);

    internal static bool OwnsFactMintCapability(object? capability) =>
        ReferenceEquals(capability, FactMintCapability);

    private static void WriteOptionalFact(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<byte>? factBytes)
    {
        writer.WriteBoolean(factBytes.HasValue);
        if (factBytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(factBytes.Value.AsSpan());
        }
    }
}

internal static class MetadataCompilerNameMappingRules
{
    internal static bool ContainsDroppedIdentifierCharacters(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }
        if (name[0] == '@')
        {
            return true;
        }

        foreach (var character in name)
        {
            if (character > 127 && CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool TryParseTerminalArity(
        string rawMetadataName,
        out string prefix,
        out int terminalArity)
    {
        var backtickIndex = rawMetadataName.LastIndexOf('`');
        var digitCount = rawMetadataName.Length - backtickIndex - 1;
        if (backtickIndex <= 0 || digitCount is < 1 or > 5)
        {
            prefix = string.Empty;
            terminalArity = 0;
            return false;
        }

        var digits = rawMetadataName.AsSpan(backtickIndex + 1);
        if (digits[0] == '0')
        {
            prefix = string.Empty;
            terminalArity = 0;
            return false;
        }

        var value = 0;
        foreach (var digit in digits)
        {
            if (digit is < '0' or > '9')
            {
                prefix = string.Empty;
                terminalArity = 0;
                return false;
            }

            value = (value * 10) + (digit - '0');
        }

        if (value > short.MaxValue)
        {
            prefix = string.Empty;
            terminalArity = 0;
            return false;
        }

        prefix = rawMetadataName[..backtickIndex];
        terminalArity = value;
        return true;
    }
}
