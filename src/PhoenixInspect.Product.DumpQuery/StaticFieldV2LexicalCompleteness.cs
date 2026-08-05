using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Identifies one lexical name kind that can outrank a bare static-field root.</summary>
/// <remarks>
/// The kinds are listed in C# precedence order. Every kind is certified independently so a consumer can always
/// see which construct was proved absent, which one owns the spelling, and which one carried no usable evidence.
/// </remarks>
public enum StaticFieldV2LexicalBlockerKind
{
    /// <summary>A local variable declared by a LocalScope active at the selected instruction.</summary>
    LocalVariable = 1,

    /// <summary>A local constant declared by a LocalScope active at the selected instruction.</summary>
    LocalConstant = 2,

    /// <summary>A parameter of the selected MethodDef.</summary>
    Parameter = 3,

    /// <summary>A generic parameter of the selected MethodDef or its declaring type.</summary>
    TypeParameter = 4,

    /// <summary>A local function of the selected method, observed through its generated MethodDef name.</summary>
    LocalFunction = 5,

    /// <summary>A field, method, or property directly declared by the selected type or an enclosing type.</summary>
    /// <remarks>
    /// The owning symbol token names whichever table declared it, so a property blocker carries a Property (0x17)
    /// token where a field blocker carries a FieldDef (0x04) one.
    /// </remarks>
    TypeMemberName = 6,

    /// <summary>A type directly nested in the selected type or one of its enclosing types.</summary>
    NestedTypeName = 7,
}

/// <summary>Classifies what one blocker kind proved about a requested spelling.</summary>
/// <remarks>
/// The dispositions are deliberately three-valued. <see cref="Incomplete"/> is never a synonym for
/// <see cref="CompleteAndAbsent"/>, because missing evidence proves nothing about the construct.
/// </remarks>
public enum StaticFieldV2LexicalBlockerDisposition
{
    /// <summary>The kind's catalog is complete for the selected instruction and does not declare the spelling.</summary>
    CompleteAndAbsent = 1,

    /// <summary>The kind's catalog is complete and declares the spelling, so the bare root is shadowed.</summary>
    CompleteAndOwned = 2,

    /// <summary>The supplied evidence does not prove this kind's catalog complete at the selected instruction.</summary>
    Incomplete = 3,
}

/// <summary>Identifies why one blocker kind's catalog could not be proved complete.</summary>
/// <remarks>Each value names one physical evidence gap, so an incomplete certificate is always explainable.</remarks>
public enum StaticFieldV2LexicalIncompletenessSource
{
    /// <summary>No incompleteness applies to this blocker kind.</summary>
    None = 0,

    /// <summary>A LocalVariable row reachable at the selected instruction carries an empty metadata name.</summary>
    UnnamedLocalSlot = 1,

    /// <summary>At least one declared local slot has no LocalVariable row in any retained scope.</summary>
    UnaccountedLocalSlot = 2,

    /// <summary>A selected-method Param row carries an empty name or the ParamList omits a signature parameter.</summary>
    StrippedParameterName = 3,

    /// <summary>The selected instruction has no active LocalScope chain this slice can consult.</summary>
    ActiveScopeUnavailable = 4,

    /// <summary>No FieldDef catalog of the selected module was supplied, so member names cannot be enumerated.</summary>
    MemberCatalogMissing = 5,

    /// <summary>A generated local-function name omits the containing-method ordinal and stays unattributed.</summary>
    UnattributedCompilerGeneratedName = 6,

    /// <summary>A GenericParam row of the selected method or its declaring type carries an empty name.</summary>
    UnnamedTypeParameter = 7,
}

/// <summary>Classifies one lexical-blocker completeness certificate answer.</summary>
/// <remarks>
/// <see cref="Complete"/>, <see cref="Shadowed"/>, and <see cref="Partial"/> are complete derived answers that
/// retain one disposition row per blocker kind. <see cref="NonExact"/>, <see cref="Invalid"/>, and
/// <see cref="Unsupported"/> are prefix-free stops that retain no blocker row at all.
/// </remarks>
public enum StaticFieldV2LexicalCertificateResultKind
{
    /// <summary>Every blocker kind is complete and none declares the requested spelling.</summary>
    Complete = 1,

    /// <summary>One complete blocker kind declares the requested spelling.</summary>
    Shadowed = 2,

    /// <summary>At least one blocker kind carried no proof of completeness.</summary>
    Partial = 3,

    /// <summary>A prerequisite or a declared bound prevented any complete certificate.</summary>
    NonExact = 4,

    /// <summary>Complete supplied evidence contradicted the authority portfolio or itself.</summary>
    Invalid = 5,

    /// <summary>The requested owner selects a later route that this certificate does not own.</summary>
    Unsupported = 6,
}

/// <summary>Identifies the deterministic issue for one lexical-completeness certificate outcome.</summary>
/// <remarks>The catalog keeps prerequisite, bound, and derived answers distinct.</remarks>
public enum StaticFieldV2LexicalCertificateIssue
{
    /// <summary>No issue applies to a complete certificate.</summary>
    None = 0,

    /// <summary>The caller-supplied selected-method lexical evidence envelope was not exact.</summary>
    LexicalEvidenceNonExact = 1,

    /// <summary>The ancestry authority portfolio prerequisite was non-exact.</summary>
    AncestryPortfolioNonExact = 2,

    /// <summary>The ancestry authority portfolio prerequisite was invalid.</summary>
    AncestryPortfolioInvalid = 3,

    /// <summary>The selected metadata module is absent from the exact ancestry portfolio.</summary>
    SelectedModuleNotInPortfolio = 4,

    /// <summary>The selected TypeDef was not issued by the selected module's definition authority.</summary>
    SelectedTypeDefinitionNotIssued = 5,

    /// <summary>The selected method's physical declaring TypeDef differs from the selected declaring type.</summary>
    SelectedMethodDeclaringTypeMismatch = 6,

    /// <summary>The selected owner is the ECMA module pseudo-type, which declares no source-addressable name.</summary>
    ModulePseudoTypeOwner = 7,

    /// <summary>The examined lexical-name count reached the declared cap plus one.</summary>
    LexicalBlockerCountBoundReached = 8,

    /// <summary>One complete blocker kind declares the requested spelling.</summary>
    BlockerOwnsSpelling = 9,

    /// <summary>At least one blocker kind carried no proof of completeness.</summary>
    BlockerEvidenceIncomplete = 10,
}

/// <summary>Identifies which step selected or refused a bare static-field root.</summary>
/// <remarks>The source is retained so import order and ancestry can never be confused with one another.</remarks>
public enum StaticFieldV2BareRootSource
{
    /// <summary>The selected declaring type or one of its enclosing types, through its bounded base chain.</summary>
    DeclaringTypeChain = 1,

    /// <summary>An active non-shadowed <c>using static</c> import of the projected scoped context.</summary>
    UsingStaticImport = 2,
}

/// <summary>Classifies one bare static-field root binding answer.</summary>
/// <remarks>
/// <see cref="Exact"/>, <see cref="Absent"/>, <see cref="Ambiguous"/>, <see cref="Partial"/>,
/// <see cref="Shadowed"/>, and <see cref="HiddenByUnsupportedMember"/> are complete derived answers that retain
/// their certificate and their examined evidence. <see cref="NonExact"/>, <see cref="Invalid"/>, and
/// <see cref="Unsupported"/> are prefix-free stops that retain no lookup and no import candidate.
/// </remarks>
public enum StaticFieldV2BareRootResultKind
{
    /// <summary>Exactly one accessible static field was selected for the bare root.</summary>
    Exact = 1,

    /// <summary>Every consulted source was complete and declared no such name.</summary>
    Absent = 2,

    /// <summary>Two or more distinct physical static fields survived selection.</summary>
    Ambiguous = 3,

    /// <summary>A consulted complete source could not prove absence over its own evidence.</summary>
    Partial = 4,

    /// <summary>A complete higher-precedence lexical name owns the requested spelling.</summary>
    Shadowed = 5,

    /// <summary>The nearest declaring level declared the name as a member this profile does not own.</summary>
    HiddenByUnsupportedMember = 6,

    /// <summary>A prerequisite, a declared bound, or retained non-exact evidence prevented an answer.</summary>
    NonExact = 7,

    /// <summary>Complete supplied evidence contradicted itself.</summary>
    Invalid = 8,

    /// <summary>A consulted owner selects a later route that this binder does not own.</summary>
    Unsupported = 9,
}

/// <summary>Identifies the deterministic issue for one bare static-field root outcome.</summary>
/// <remarks>
/// Lookup-derived issues are shared by the declaring-type chain and the imported owners; the deciding step is
/// retained separately so one issue value never has to encode two different sources.
/// </remarks>
public enum StaticFieldV2BareRootIssue
{
    /// <summary>No issue applies to an exact outcome.</summary>
    None = 0,

    /// <summary>The lexical-completeness certificate prerequisite was non-exact.</summary>
    CertificateNonExact = 1,

    /// <summary>The lexical-completeness certificate prerequisite was invalid.</summary>
    CertificateInvalid = 2,

    /// <summary>The lexical-completeness certificate selects a later route.</summary>
    CertificateUnsupported = 3,

    /// <summary>A complete higher-precedence lexical name owns the requested spelling.</summary>
    CertificateShadowed = 4,

    /// <summary>The certificate could not prove every blocker kind complete.</summary>
    CertificateIncomplete = 5,

    /// <summary>The scoped-context projection prerequisite was non-exact.</summary>
    ScopedContextNonExact = 6,

    /// <summary>The scoped-context projection prerequisite was invalid.</summary>
    ScopedContextInvalid = 7,

    /// <summary>The scoped-context projection was made for another module, type, or ancestry portfolio.</summary>
    ScopedContextSubjectMismatch = 8,

    /// <summary>One consulted member-lookup answer stopped non-exactly.</summary>
    MemberLookupNonExact = 9,

    /// <summary>One consulted member-lookup answer stopped as invalid or contradicted the certificate.</summary>
    MemberLookupInvalid = 10,

    /// <summary>One consulted member-lookup answer could not prove absence over its base chain.</summary>
    MemberLookupIncomplete = 11,

    /// <summary>One consulted level declared two or more accessible static fields with the name.</summary>
    AmbiguousDeclarations = 12,

    /// <summary>The nearest declaring level declared the name as a member this profile does not own.</summary>
    HiddenByUnsupportedMember = 13,

    /// <summary>Two or more imported owners contributed different physical static fields.</summary>
    AmbiguousImportedDeclarations = 14,

    /// <summary>Every consulted complete source declared no such name.</summary>
    DeclarationAbsent = 15,

    /// <summary>A non-exhaustive scoped context forbids claiming a complete absent answer.</summary>
    AbsenceNotClaimableOverRetainedEvidence = 16,

    /// <summary>An active <c>using static</c> import retained a target that did not resolve exactly.</summary>
    UnresolvedUsingStaticTargetRetained = 17,
}

/// <summary>Identifies one declared coverage boundary retained by a lexical-completeness answer.</summary>
/// <remarks>
/// Every boundary is an informational fact rather than an error. A boundary states what this phase deliberately
/// does not model, so a consumer can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2LexicalCoverageBoundary
{
    // Value 1, PropertyAndEventTablesNotModeled, is retired: the Property table is modeled now. The number is left
    // as a hole rather than renumbered, so no already-recorded digest keeps its bytes while changing its meaning.

    /// <summary>Query range variables and pattern designations leave no physical row and are not modeled.</summary>
    RangeAndPatternVariableNamesNotModeled = 2,

    /// <summary>A generated local-function MethodDef carries no physical parent or source-scope relation.</summary>
    LocalFunctionParentRelationNotPhysical = 3,

    /// <summary>Selecting a directly declared current-type member is deferred to the separately owned route.</summary>
    DirectlyDeclaredMemberSelectionDeferred = 4,

    /// <summary>A <c>using static</c> import contributes only directly declared members, never inherited ones.</summary>
    UsingStaticInheritedMembersNotImported = 5,

    // Value 6, ImportedMemberGroupBlockingNotModeled, is retired. Its text was already partly false: a same-name
    // method declared directly on an imported owner does block today, through the level-zero fall-through into the
    // shared member lookup. What remains genuinely unmodeled is narrower, and is stated as value 8.

    /// <summary>The physical Event and EventMap tables are not modeled, so a same-name event cannot block.</summary>
    EventTablesNotModeled = 7,

    /// <summary>A same-name imported event cannot block, because the Event and EventMap tables are absent.</summary>
    ImportedEventGroupBlockingNotModeled = 8,

    /// <summary>
    /// MethodSemantics is not modeled, so a same-name property blocks without any test of whether its accessors are
    /// reachable from the use site.
    /// </summary>
    PropertyAccessorSemanticsNotModeled = 9,
}

/// <summary>Freezes one certified blocker-kind disposition for a single requested spelling.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2LexicalCertificateOutcome"/>. It retains the examined name count
/// of the kind, and, for an owned spelling, the physical token and metadata name of the owning symbol.
/// </remarks>
public sealed class StaticFieldV2LexicalBlockerRow : IEquatable<StaticFieldV2LexicalBlockerRow>
{
    private const string CanonicalDomain = "static-field-v2-lexical-blocker-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LexicalBlockerRow(
        StaticFieldV2LexicalBlockerKind kind,
        StaticFieldV2LexicalBlockerDisposition disposition,
        StaticFieldV2LexicalIncompletenessSource incompletenessSource,
        int examinedNameCount,
        string? owningSymbolName,
        int? owningSymbolMetadataToken)
    {
        Kind = kind;
        Disposition = disposition;
        IncompletenessSource = incompletenessSource;
        ExaminedNameCount = examinedNameCount;
        OwningSymbolName = owningSymbolName;
        OwningSymbolMetadataToken = owningSymbolMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)kind);
        writer.WriteInt32((int)disposition);
        writer.WriteInt32((int)incompletenessSource);
        writer.WriteInt32(examinedNameCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, owningSymbolName);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, owningSymbolMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the lexical name kind this row certifies.</summary>
    public StaticFieldV2LexicalBlockerKind Kind { get; }

    /// <summary>Gets whether this kind is complete-and-absent, complete-and-owned, or incomplete.</summary>
    public StaticFieldV2LexicalBlockerDisposition Disposition { get; }

    /// <summary>Gets the physical evidence gap for an incomplete kind, otherwise none.</summary>
    public StaticFieldV2LexicalIncompletenessSource IncompletenessSource { get; }

    /// <summary>Gets the count of names this kind contributed to the examined lexical catalog.</summary>
    public int ExaminedNameCount { get; }

    /// <summary>Gets the exact metadata name of the owning symbol, otherwise null.</summary>
    public string? OwningSymbolName { get; }

    /// <summary>Gets the physical metadata token of the owning symbol, otherwise null.</summary>
    public int? OwningSymbolMetadataToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical row.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two certified blocker rows.</summary>
    /// <param name="other">The other row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2LexicalBlockerRow? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests certified blocker row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LexicalBlockerRow);

    /// <summary>Computes a deterministic hash code from immutable canonical row content.</summary>
    /// <returns>A hash code for this canonical row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2LexicalBlockerRow Create(
        object mintCapability,
        StaticFieldV2LexicalBlockerKind kind,
        StaticFieldV2LexicalBlockerDisposition disposition,
        StaticFieldV2LexicalIncompletenessSource incompletenessSource,
        int examinedNameCount,
        string? owningSymbolName,
        int? owningSymbolMetadataToken)
    {
        if (!StaticFieldV2LexicalCertificateOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "A blocker row requires the certificate outcome's private mint capability.",
                nameof(mintCapability));
        }

        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        ExpressionV2ContractEncoding.RequireDefined(disposition, nameof(disposition));
        ExpressionV2ContractEncoding.RequireDefined(incompletenessSource, nameof(incompletenessSource));
        ArgumentOutOfRangeException.ThrowIfNegative(examinedNameCount);
        if ((disposition == StaticFieldV2LexicalBlockerDisposition.Incomplete) !=
            (incompletenessSource != StaticFieldV2LexicalIncompletenessSource.None))
        {
            throw new ArgumentException(
                "Exactly an incomplete blocker row names one physical evidence gap.",
                nameof(incompletenessSource));
        }
        if ((disposition == StaticFieldV2LexicalBlockerDisposition.CompleteAndOwned) != (owningSymbolName is not null))
        {
            throw new ArgumentException(
                "Exactly an owned blocker row retains the owning symbol's metadata name.",
                nameof(owningSymbolName));
        }
        if (owningSymbolName is not null)
        {
            ExpressionV2ContractEncoding.RequireText(
                owningSymbolName,
                nameof(owningSymbolName),
                StaticFieldV2Limits.MaximumIdentifierCharacterCount);
        }

        return new StaticFieldV2LexicalBlockerRow(
            kind,
            disposition,
            incompletenessSource,
            examinedNameCount,
            owningSymbolName,
            owningSymbolMetadataToken);
    }
}

/// <summary>Freezes one complete lexical-blocker completeness certificate request.</summary>
/// <remarks>
/// The request names the caller-supplied selected-method lexical evidence envelope, the requested bare identifier,
/// the selected metadata module and declaring TypeDef, the exact ancestry authority portfolio, and one FieldDef
/// catalog per ancestry-portfolio module. It carries no runtime, no storage, and no scoped import evidence.
/// </remarks>
public sealed class StaticFieldV2LexicalCertificateRequest : IEquatable<StaticFieldV2LexicalCertificateRequest>
{
    private const string CanonicalDomain = "static-field-v2-lexical-certificate-request";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs;
    private readonly ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LexicalCertificateRequest(
        DumpSelectedMethodLexicalObservation lexicalEvidence,
        DumpExpressionIdentifier identifier,
        StaticFieldMetadataModuleIdentity selectedModule,
        MetadataTypeDefinitionAuthorityIdentity selectedTypeDefinition,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs)
    {
        LexicalEvidence = lexicalEvidence;
        Identifier = identifier;
        SelectedModule = selectedModule;
        SelectedTypeDefinition = selectedTypeDefinition;
        AncestryPortfolio = ancestryPortfolio;
        this.fieldCatalogs = fieldCatalogs;
        this.propertyCatalogs = propertyCatalogs;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(lexicalEvidence.Sha256, nameof(lexicalEvidence));
        writer.WriteSha256(identifier.Sha256, nameof(identifier));
        writer.WriteSha256(selectedModule.Sha256, nameof(selectedModule));
        writer.WriteSha256(selectedTypeDefinition.Sha256, nameof(selectedTypeDefinition));
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        writer.WriteInt32(fieldCatalogs.IsDefault ? -1 : fieldCatalogs.Length);
        if (!fieldCatalogs.IsDefault)
        {
            foreach (var catalog in fieldCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        writer.WriteInt32(propertyCatalogs.IsDefault ? -1 : propertyCatalogs.Length);
        if (!propertyCatalogs.IsDefault)
        {
            foreach (var catalog in propertyCatalogs)
            {
                ExpressionV2ContractEncoding.WriteOptionalDigest(writer, catalog?.Sha256);
            }
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the caller-supplied selected-method lexical evidence envelope.</summary>
    public DumpSelectedMethodLexicalObservation LexicalEvidence { get; }

    /// <summary>Gets the decoded bare identifier whose lexical blockers are certified.</summary>
    public DumpExpressionIdentifier Identifier { get; }

    /// <summary>Gets the exact metadata module owning the selected declaring TypeDef.</summary>
    public StaticFieldMetadataModuleIdentity SelectedModule { get; }

    /// <summary>Gets the authority-issued declaring TypeDef of the selected method.</summary>
    public MetadataTypeDefinitionAuthorityIdentity SelectedTypeDefinition { get; }

    /// <summary>Gets the ancestry authority portfolio supplying classification and definition authorities.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets a defensive copy of the supplied per-module FieldDef catalog vector, default when absent.</summary>
    public ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogs =>
        fieldCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(fieldCatalogs);

    /// <summary>Gets whether the supplied FieldDef catalog vector was explicitly initialized.</summary>
    public bool IsFieldCatalogVectorInitialized => !fieldCatalogs.IsDefault;

    /// <summary>Gets a defensive copy of the supplied per-module Property catalog vector, default when absent.</summary>
    public ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogs =>
        propertyCatalogs.IsDefault ? default : ExpressionV2ContractEncoding.Copy(propertyCatalogs);

    /// <summary>Gets whether the supplied Property catalog vector was explicitly initialized.</summary>
    public bool IsPropertyCatalogVectorInitialized => !propertyCatalogs.IsDefault;

    /// <summary>Gets a defensive copy of the fixed-reference canonical request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete lexical-blocker completeness certificate request.</summary>
    /// <param name="lexicalEvidence">The caller-supplied selected-method lexical evidence envelope.</param>
    /// <param name="identifier">The decoded bare identifier whose lexical blockers are certified.</param>
    /// <param name="selectedModule">The exact metadata module owning the selected declaring TypeDef.</param>
    /// <param name="selectedTypeDefinition">The authority-issued declaring TypeDef of the selected method.</param>
    /// <param name="ancestryPortfolio">The ancestry authority portfolio prerequisite.</param>
    /// <param name="fieldCatalogs">
    /// One FieldDef catalog per ancestry-portfolio module in any order. A default or incomplete vector is admitted here
    /// and becomes a typed incomplete member-name disposition rather than an exception.
    /// </param>
    /// <param name="propertyCatalogs">
    /// One Property catalog per ancestry-portfolio module in any order, on the same terms as
    /// <paramref name="fieldCatalogs"/>.
    /// </param>
    /// <returns>A sealed immutable request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    public static StaticFieldV2LexicalCertificateRequest Create(
        DumpSelectedMethodLexicalObservation lexicalEvidence,
        DumpExpressionIdentifier identifier,
        StaticFieldMetadataModuleIdentity selectedModule,
        MetadataTypeDefinitionAuthorityIdentity selectedTypeDefinition,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> fieldCatalogs,
        ImmutableArray<MetadataPropertyTableCatalogIdentity> propertyCatalogs)
    {
        ArgumentNullException.ThrowIfNull(lexicalEvidence);
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(selectedModule);
        ArgumentNullException.ThrowIfNull(selectedTypeDefinition);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);

        var catalogs = fieldCatalogs.IsDefault ? default : ImmutableArray.CreateRange(fieldCatalogs);
        var properties = propertyCatalogs.IsDefault ? default : ImmutableArray.CreateRange(propertyCatalogs);
        return new StaticFieldV2LexicalCertificateRequest(
            lexicalEvidence,
            identifier,
            selectedModule,
            selectedTypeDefinition,
            ancestryPortfolio,
            catalogs,
            properties);
    }

    /// <summary>Tests canonical equality between two certificate requests.</summary>
    /// <param name="other">The other request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2LexicalCertificateRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests certificate request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LexicalCertificateRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical request content.</summary>
    /// <returns>A hash code for this canonical request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<MetadataFieldDefinitionTableCatalogIdentity> FieldCatalogsCore => fieldCatalogs;

    internal ImmutableArray<MetadataPropertyTableCatalogIdentity> PropertyCatalogsCore => propertyCatalogs;
}

/// <summary>Freezes the complete outcome of one lexical-blocker completeness certification.</summary>
/// <remarks>
/// This sealed outcome is the sole issuer of every blocker row it retains. A complete derived answer retains one
/// row per lexical blocker kind in ascending kind order, so no kind can silently vanish. A prefix-free stop retains no
/// row at all, because a partial derivation is not evidence about the requested spelling.
/// <para>
/// A single <see cref="StaticFieldV2LexicalBlockerDisposition.Incomplete"/> kind makes the whole certificate
/// <see cref="StaticFieldV2LexicalCertificateResultKind.Partial"/>; missing evidence never degrades to a proven
/// absence. A <see cref="StaticFieldV2LexicalBlockerDisposition.CompleteAndOwned"/> kind outranks an incomplete one,
/// because a name that was actually found is positive proof regardless of any other catalog gap.
/// </para>
/// </remarks>
public sealed class StaticFieldV2LexicalCertificateOutcome : IEquatable<StaticFieldV2LexicalCertificateOutcome>
{
    /// <summary>Gets the shared examined lexical-name cap applied by one complete certification.</summary>
    public const int MaximumLexicalBlockerCount = StaticFieldV2Limits.MaximumLexicalBlockerCount;

    private const string CanonicalDomain = "static-field-v2-lexical-certificate-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2LexicalBlockerRow> blockers;
    private readonly ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2LexicalCertificateOutcome(
        StaticFieldV2LexicalCertificateResultKind resultKind,
        StaticFieldV2LexicalCertificateIssue issue,
        StaticFieldV2LexicalCertificateRequest request,
        ImmutableArray<StaticFieldV2LexicalBlockerRow> blockers,
        StaticFieldV2LexicalBlockerKind? owningKind,
        StaticFieldV2LexicalBlockerKind? firstIncompleteKind,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        this.blockers = ExpressionV2ContractEncoding.Copy(blockers);
        OwningKind = owningKind;
        FirstIncompleteKind = firstIncompleteKind;
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteInt32(blockers.Length);
        foreach (var blocker in blockers)
        {
            writer.WriteSha256(blocker.Sha256, nameof(blockers));
        }
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, owningKind);
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, firstIncompleteKind);
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this certificate is complete, shadowed, partial, or a typed stop.</summary>
    public StaticFieldV2LexicalCertificateResultKind ResultKind { get; }

    /// <summary>Gets the typed certificate issue, or none for a complete certificate.</summary>
    public StaticFieldV2LexicalCertificateIssue Issue { get; }

    /// <summary>Gets the complete request that produced this certificate.</summary>
    public StaticFieldV2LexicalCertificateRequest Request { get; }

    /// <summary>Gets a defensive ascending-kind copy of every certified blocker row, empty for a stop.</summary>
    public ImmutableArray<StaticFieldV2LexicalBlockerRow> Blockers =>
        ExpressionV2ContractEncoding.Copy(blockers);

    /// <summary>Gets the blocker kind that owns the requested spelling, otherwise null.</summary>
    public StaticFieldV2LexicalBlockerKind? OwningKind { get; }

    /// <summary>Gets the first blocker kind whose evidence was incomplete, otherwise null.</summary>
    public StaticFieldV2LexicalBlockerKind? FirstIncompleteKind { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary.</summary>
    public ImmutableArray<StaticFieldV2LexicalCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the examined lexical-name count for a complete answer, or the propagated stop observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the metadata token related to this answer or stop, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the certified row for one blocker kind, or null for a prefix-free stop.</summary>
    /// <param name="kind">The lexical blocker kind whose disposition is requested.</param>
    /// <returns>The retained row, or null when this outcome retains no row at all.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined kind.</exception>
    public StaticFieldV2LexicalBlockerRow? BlockerFor(StaticFieldV2LexicalBlockerKind kind)
    {
        ExpressionV2ContractEncoding.RequireDefined(kind, nameof(kind));
        foreach (var blocker in blockers)
        {
            if (blocker.Kind == kind)
            {
                return blocker;
            }
        }
        return null;
    }

    /// <summary>Tests canonical equality between two lexical-certificate outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2LexicalCertificateOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests lexical-certificate outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2LexicalCertificateOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal ImmutableArray<StaticFieldV2LexicalBlockerRow> BlockersCore => blockers;

    internal static StaticFieldV2LexicalBlockerRow IssueBlocker(
        StaticFieldV2LexicalBlockerKind kind,
        StaticFieldV2LexicalBlockerDisposition disposition,
        StaticFieldV2LexicalIncompletenessSource incompletenessSource,
        int examinedNameCount,
        string? owningSymbolName,
        int? owningSymbolMetadataToken) =>
        StaticFieldV2LexicalBlockerRow.Create(
            RowMintCapability,
            kind,
            disposition,
            incompletenessSource,
            examinedNameCount,
            owningSymbolName,
            owningSymbolMetadataToken);

    internal static StaticFieldV2LexicalCertificateOutcome IssueComplete(
        StaticFieldV2LexicalCertificateResultKind resultKind,
        StaticFieldV2LexicalCertificateIssue issue,
        StaticFieldV2LexicalCertificateRequest request,
        ImmutableArray<StaticFieldV2LexicalBlockerRow> blockers,
        StaticFieldV2LexicalBlockerKind? owningKind,
        StaticFieldV2LexicalBlockerKind? firstIncompleteKind,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            blockers,
            owningKind,
            firstIncompleteKind,
            declaredCoverageBoundaries,
            null,
            observedCount,
            relatedMetadataToken);

    internal static StaticFieldV2LexicalCertificateOutcome IssueStop(
        StaticFieldV2LexicalCertificateResultKind resultKind,
        StaticFieldV2LexicalCertificateIssue issue,
        StaticFieldV2LexicalCertificateRequest request,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            [],
            null,
            null,
            declaredCoverageBoundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Freezes one examined <c>using static</c> imported owner candidate.</summary>
/// <remarks>
/// The row is minted only by <see cref="StaticFieldV2BareRootOutcome"/>. It retains the contributing import
/// projection, the resolved owner, the complete member-lookup answer on that owner, and whether the answer was
/// refused because the selected declaration is inherited rather than directly declared by the imported owner.
/// </remarks>
public sealed class StaticFieldV2BareRootImportCandidate : IEquatable<StaticFieldV2BareRootImportCandidate>
{
    private const string CanonicalDomain = "static-field-v2-bare-root-import-candidate";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2BareRootImportCandidate(
        StaticFieldV2ScopedImportProjection contributingImport,
        StaticFieldMetadataModuleIdentity ownerModule,
        MetadataTypeDefinitionAuthorityIdentity ownerTypeDefinition,
        StaticFieldV2MemberLookupOutcome lookup,
        bool isInheritedAndRefused,
        bool isAccepted)
    {
        ContributingImport = contributingImport;
        OwnerModule = ownerModule;
        OwnerTypeDefinition = ownerTypeDefinition;
        Lookup = lookup;
        IsInheritedAndRefused = isInheritedAndRefused;
        IsAccepted = isAccepted;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(contributingImport.Sha256, nameof(contributingImport));
        writer.WriteSha256(ownerModule.Sha256, nameof(ownerModule));
        writer.WriteSha256(ownerTypeDefinition.Sha256, nameof(ownerTypeDefinition));
        writer.WriteSha256(lookup.Sha256, nameof(lookup));
        writer.WriteBoolean(isInheritedAndRefused);
        writer.WriteBoolean(isAccepted);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the active non-shadowed <c>using static</c> projection that contributed this owner.</summary>
    public StaticFieldV2ScopedImportProjection ContributingImport { get; }

    /// <summary>Gets the metadata module owning the resolved imported owner.</summary>
    public StaticFieldMetadataModuleIdentity OwnerModule { get; }

    /// <summary>Gets the authority-issued TypeDef of the resolved imported owner.</summary>
    public MetadataTypeDefinitionAuthorityIdentity OwnerTypeDefinition { get; }

    /// <summary>Gets the complete member-lookup answer produced on this imported owner.</summary>
    public StaticFieldV2MemberLookupOutcome Lookup { get; }

    /// <summary>Gets whether an exact answer was refused because the declaration is inherited, not imported.</summary>
    public bool IsInheritedAndRefused { get; }

    /// <summary>Gets whether this imported owner contributed one directly declared candidate.</summary>
    public bool IsAccepted { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical candidate bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical candidate.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two examined imported-owner candidates.</summary>
    /// <param name="other">The other candidate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2BareRootImportCandidate? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests imported-owner candidate equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a candidate with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2BareRootImportCandidate);

    /// <summary>Computes a deterministic hash code from immutable canonical candidate content.</summary>
    /// <returns>A hash code for this canonical candidate.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2BareRootImportCandidate Create(
        object mintCapability,
        StaticFieldV2ScopedImportProjection contributingImport,
        StaticFieldMetadataModuleIdentity ownerModule,
        MetadataTypeDefinitionAuthorityIdentity ownerTypeDefinition,
        StaticFieldV2MemberLookupOutcome lookup,
        bool isInheritedAndRefused,
        bool isAccepted)
    {
        if (!StaticFieldV2BareRootOutcome.OwnsRowMintCapability(mintCapability))
        {
            throw new ArgumentException(
                "An imported-owner candidate requires the bare-root outcome's private mint capability.",
                nameof(mintCapability));
        }

        ArgumentNullException.ThrowIfNull(contributingImport);
        ArgumentNullException.ThrowIfNull(ownerModule);
        ArgumentNullException.ThrowIfNull(ownerTypeDefinition);
        ArgumentNullException.ThrowIfNull(lookup);
        if (isAccepted && isInheritedAndRefused)
        {
            throw new ArgumentException(
                "An accepted imported candidate cannot also be a refused inherited declaration.",
                nameof(isAccepted));
        }

        return new StaticFieldV2BareRootImportCandidate(
            contributingImport,
            ownerModule,
            ownerTypeDefinition,
            lookup,
            isInheritedAndRefused,
            isAccepted);
    }
}

/// <summary>Freezes one complete bare static-field root binding request.</summary>
/// <remarks>
/// The request reuses the certificate request for identity, evidence, and catalogs, and adds the projected
/// scoped context plus the caller-declared accessibility certificate governing every member admission.
/// </remarks>
public sealed class StaticFieldV2BareRootRequest : IEquatable<StaticFieldV2BareRootRequest>
{
    private const string CanonicalDomain = "static-field-v2-bare-root-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2BareRootRequest(
        StaticFieldV2LexicalCertificateRequest certificateRequest,
        StaticFieldV2ScopedContextOutcome scopedContext,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants)
    {
        CertificateRequest = certificateRequest;
        ScopedContext = scopedContext;
        AccessibilityMode = accessibilityMode;
        RequestingAssembly = requestingAssembly;
        this.friendAssemblyGrants = friendAssemblyGrants;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(certificateRequest.Sha256, nameof(certificateRequest));
        writer.WriteSha256(scopedContext.Sha256, nameof(scopedContext));
        writer.WriteInt32((int)accessibilityMode);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, requestingAssembly?.Sha256);
        writer.WriteInt32(friendAssemblyGrants.Length);
        foreach (var grant in friendAssemblyGrants)
        {
            writer.WriteSha256(grant.Sha256, nameof(friendAssemblyGrants));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete lexical-certificate request consumed by step one.</summary>
    public StaticFieldV2LexicalCertificateRequest CertificateRequest { get; }

    /// <summary>Gets the projected scoped-context outcome supplying <c>using static</c> imports.</summary>
    public StaticFieldV2ScopedContextOutcome ScopedContext { get; }

    /// <summary>Gets the caller-declared accessibility certificate governing every member admission.</summary>
    public StaticFieldV2AccessibilityMode AccessibilityMode { get; }

    /// <summary>Gets the requesting assembly for the use-site certificate, or null for qualified inspection.</summary>
    public StaticFieldContainingAssemblyIdentity? RequestingAssembly { get; }

    /// <summary>Gets a defensive declaration-order copy of the caller-supplied friend-assembly grants.</summary>
    public ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrants =>
        ExpressionV2ContractEncoding.Copy(friendAssemblyGrants);

    /// <summary>Gets the decoded bare identifier this request binds.</summary>
    public DumpExpressionIdentifier Identifier => CertificateRequest.Identifier;

    /// <summary>Gets a defensive copy of the fixed-reference canonical request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete bare static-field root binding request.</summary>
    /// <param name="certificateRequest">The complete lexical-certificate request consumed by step one.</param>
    /// <param name="scopedContext">The projected scoped-context outcome supplying imports.</param>
    /// <param name="accessibilityMode">The caller-declared accessibility certificate.</param>
    /// <param name="requestingAssembly">
    /// The requesting assembly, required exactly for <see cref="StaticFieldV2AccessibilityMode.UseSiteCertificate"/>.
    /// </param>
    /// <param name="friendAssemblyGrants">
    /// The caller-supplied friend grants, admitted only for the use-site certificate and bounded by the shared cap.
    /// </param>
    /// <returns>A sealed immutable request with defensively copied evidence.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The declared accessibility mode is undefined.</exception>
    /// <exception cref="ArgumentException">The requesting assembly or grants disagree with the declared mode.</exception>
    public static StaticFieldV2BareRootRequest Create(
        StaticFieldV2LexicalCertificateRequest certificateRequest,
        StaticFieldV2ScopedContextOutcome scopedContext,
        StaticFieldV2AccessibilityMode accessibilityMode,
        StaticFieldContainingAssemblyIdentity? requestingAssembly = null,
        ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> friendAssemblyGrants = default)
    {
        ArgumentNullException.ThrowIfNull(certificateRequest);
        ArgumentNullException.ThrowIfNull(scopedContext);
        ExpressionV2ContractEncoding.RequireDefined(accessibilityMode, nameof(accessibilityMode));

        var useSite = accessibilityMode == StaticFieldV2AccessibilityMode.UseSiteCertificate;
        if (useSite == (requestingAssembly is null))
        {
            throw new ArgumentException(
                "A requesting assembly is required exactly for the use-site accessibility certificate.",
                nameof(requestingAssembly));
        }

        var grants = ExpressionV2ContractEncoding.CopyRequired(
            friendAssemblyGrants.IsDefault
                ? ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity>.Empty
                : friendAssemblyGrants,
            nameof(friendAssemblyGrants),
            StaticFieldV2Limits.MaximumFriendAssemblyDeclarationCount);
        if (!useSite && !grants.IsEmpty)
        {
            throw new ArgumentException(
                "Qualified inspection bypasses accessibility and therefore admits no friend-assembly grant.",
                nameof(friendAssemblyGrants));
        }

        return new StaticFieldV2BareRootRequest(
            certificateRequest,
            scopedContext,
            accessibilityMode,
            requestingAssembly,
            grants);
    }

    /// <summary>Tests canonical equality between two bare-root requests.</summary>
    /// <param name="other">The other request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2BareRootRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests bare-root request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2BareRootRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical request content.</summary>
    /// <returns>A hash code for this canonical request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<StaticFieldV2FriendAssemblyGrantIdentity> FriendAssemblyGrantsCore =>
        friendAssemblyGrants;
}

/// <summary>Freezes the complete outcome of one bare static-field root binding.</summary>
/// <remarks>
/// This sealed outcome is the sole issuer of every imported-owner candidate it retains. It always retains the
/// lexical-completeness certificate that governed the answer, so a consumer can see exactly why an import was or was
/// not permitted to contribute.
/// <para>
/// A complete derived answer retains its consulted declaring-chain lookups and its examined imported owners. A
/// prefix-free stop retains neither, because a partial derivation is not evidence about the requested spelling.
/// </para>
/// </remarks>
public sealed class StaticFieldV2BareRootOutcome : IEquatable<StaticFieldV2BareRootOutcome>
{
    private const string CanonicalDomain = "static-field-v2-bare-root-outcome";
    private const int CanonicalSchemaVersion = 1;
    private static readonly object RowMintCapability = new();
    private readonly ImmutableArray<StaticFieldV2MemberLookupOutcome> declaringChainLookups;
    private readonly ImmutableArray<StaticFieldV2BareRootImportCandidate> importCandidates;
    private readonly ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2BareRootOutcome(
        StaticFieldV2BareRootResultKind resultKind,
        StaticFieldV2BareRootIssue issue,
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2BareRootSource? source,
        StaticFieldV2MemberCandidateIdentity? selectedField,
        StaticFieldMetadataModuleIdentity? selectedOwnerModule,
        MetadataTypeDefinitionAuthorityIdentity? selectedOwnerTypeDefinition,
        ImmutableArray<StaticFieldV2MemberLookupOutcome> declaringChainLookups,
        ImmutableArray<StaticFieldV2BareRootImportCandidate> importCandidates,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken)
    {
        ResultKind = resultKind;
        Issue = issue;
        Request = request;
        Certificate = certificate;
        Source = source;
        SelectedField = selectedField;
        SelectedOwnerModule = selectedOwnerModule;
        SelectedOwnerTypeDefinition = selectedOwnerTypeDefinition;
        this.declaringChainLookups = ExpressionV2ContractEncoding.Copy(declaringChainLookups);
        this.importCandidates = ExpressionV2ContractEncoding.Copy(importCandidates);
        this.declaredCoverageBoundaries = ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);
        ReachedBound = reachedBound;
        ObservedCount = observedCount;
        RelatedMetadataToken = relatedMetadataToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(certificate.Sha256, nameof(certificate));
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, source);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedField?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedOwnerModule?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedOwnerTypeDefinition?.Sha256);
        writer.WriteInt32(declaringChainLookups.Length);
        foreach (var lookup in declaringChainLookups)
        {
            writer.WriteSha256(lookup.Sha256, nameof(declaringChainLookups));
        }
        writer.WriteInt32(importCandidates.Length);
        foreach (var candidate in importCandidates)
        {
            writer.WriteSha256(candidate.Sha256, nameof(importCandidates));
        }
        writer.WriteInt32(declaredCoverageBoundaries.Length);
        foreach (var boundary in declaredCoverageBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(observedCount);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, relatedMetadataToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this binding is exact, absent, ambiguous, blocked, or a typed stop.</summary>
    public StaticFieldV2BareRootResultKind ResultKind { get; }

    /// <summary>Gets the typed binding issue, or none for an exact outcome.</summary>
    public StaticFieldV2BareRootIssue Issue { get; }

    /// <summary>Gets the complete request that produced this outcome.</summary>
    public StaticFieldV2BareRootRequest Request { get; }

    /// <summary>Gets the lexical-completeness certificate that governed this outcome.</summary>
    public StaticFieldV2LexicalCertificateOutcome Certificate { get; }

    /// <summary>Gets the step that decided this outcome, or null for a certificate or context stop.</summary>
    public StaticFieldV2BareRootSource? Source { get; }

    /// <summary>Gets the single selected static-field candidate, or null for anything but an exact answer.</summary>
    public StaticFieldV2MemberCandidateIdentity? SelectedField { get; }

    /// <summary>Gets the metadata module owning the selected declaration, otherwise null.</summary>
    public StaticFieldMetadataModuleIdentity? SelectedOwnerModule { get; }

    /// <summary>Gets the consulted owner TypeDef that produced the selected declaration, otherwise null.</summary>
    public MetadataTypeDefinitionAuthorityIdentity? SelectedOwnerTypeDefinition { get; }

    /// <summary>Gets a defensive innermost-first copy of every consulted declaring-chain lookup.</summary>
    public ImmutableArray<StaticFieldV2MemberLookupOutcome> DeclaringChainLookups =>
        ExpressionV2ContractEncoding.Copy(declaringChainLookups);

    /// <summary>Gets a defensive examination-order copy of every examined imported-owner candidate.</summary>
    public ImmutableArray<StaticFieldV2BareRootImportCandidate> ImportCandidates =>
        ExpressionV2ContractEncoding.Copy(importCandidates);

    internal ImmutableArray<StaticFieldV2BareRootImportCandidate> ImportCandidatesCore => importCandidates;

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary.</summary>
    public ImmutableArray<StaticFieldV2LexicalCoverageBoundary> DeclaredCoverageBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredCoverageBoundaries);

    /// <summary>Gets the declared bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the consulted owner count for a complete answer, or the propagated stop observation.</summary>
    public int ObservedCount { get; }

    /// <summary>Gets the metadata token related to this answer or stop, otherwise null.</summary>
    public int? RelatedMetadataToken { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two bare-root outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(StaticFieldV2BareRootOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests bare-root outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2BareRootOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static bool OwnsRowMintCapability(object? capability) =>
        ReferenceEquals(capability, RowMintCapability);

    internal static StaticFieldV2BareRootImportCandidate IssueImportCandidate(
        StaticFieldV2ScopedImportProjection contributingImport,
        StaticFieldMetadataModuleIdentity ownerModule,
        MetadataTypeDefinitionAuthorityIdentity ownerTypeDefinition,
        StaticFieldV2MemberLookupOutcome lookup,
        bool isInheritedAndRefused,
        bool isAccepted) =>
        StaticFieldV2BareRootImportCandidate.Create(
            RowMintCapability,
            contributingImport,
            ownerModule,
            ownerTypeDefinition,
            lookup,
            isInheritedAndRefused,
            isAccepted);

    internal static StaticFieldV2BareRootOutcome IssueComplete(
        StaticFieldV2BareRootResultKind resultKind,
        StaticFieldV2BareRootIssue issue,
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2BareRootSource? source,
        StaticFieldV2MemberCandidateIdentity? selectedField,
        StaticFieldMetadataModuleIdentity? selectedOwnerModule,
        MetadataTypeDefinitionAuthorityIdentity? selectedOwnerTypeDefinition,
        ImmutableArray<StaticFieldV2MemberLookupOutcome> declaringChainLookups,
        ImmutableArray<StaticFieldV2BareRootImportCandidate> importCandidates,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            certificate,
            source,
            selectedField,
            selectedOwnerModule,
            selectedOwnerTypeDefinition,
            declaringChainLookups,
            importCandidates,
            declaredCoverageBoundaries,
            null,
            observedCount,
            relatedMetadataToken);

    internal static StaticFieldV2BareRootOutcome IssueStop(
        StaticFieldV2BareRootResultKind resultKind,
        StaticFieldV2BareRootIssue issue,
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2BareRootSource? source,
        ImmutableArray<StaticFieldV2LexicalCoverageBoundary> declaredCoverageBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        new(
            resultKind,
            issue,
            request,
            certificate,
            source,
            null,
            null,
            null,
            [],
            [],
            declaredCoverageBoundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}

/// <summary>Certifies lexical-blocker completeness and binds bare <c>using static</c> static-field roots.</summary>
/// <remarks>
/// This binder answers exactly one question: may a bare identifier root reach an imported static field? It first
/// proves, kind by kind, that every higher-precedence lexical name is completely accounted for at the selected
/// instruction, and only then applies C# precedence over the declaring-type chain and the active
/// <c>using static</c> imports.
/// <para>
/// Declared coverage boundaries of this slice: a same-name property does block a bare root and does block an import,
/// from the module's complete Property table, but MethodSemantics is not modeled, so nothing tests whether its
/// accessors are reachable; the physical Event and EventMap tables are not modeled, so a same-name event can block
/// neither; query range variables and pattern
/// designations leave no physical row and are not certified; a generated local-function MethodDef carries no physical
/// parent relation, so attribution relies on the raw generated name alone; and selecting a directly declared member of
/// the selected type is deferred to the separately owned qualified route, because this slice only decides whether the
/// import may bind.
/// </para>
/// <para>
/// A <c>using static</c> import contributes only members directly declared by the imported owner. That rule is
/// enforced physically: an exact member-lookup answer on an imported owner is refused unless the selected declaration
/// was found at level zero of that owner's own bounded base chain.
/// </para>
/// </remarks>
public static class StaticFieldV2LexicalCompleteness
{
    private static readonly ImmutableArray<StaticFieldV2LexicalCoverageBoundary> CertificateBoundaries =
    [
        StaticFieldV2LexicalCoverageBoundary.RangeAndPatternVariableNamesNotModeled,
        StaticFieldV2LexicalCoverageBoundary.LocalFunctionParentRelationNotPhysical,
        StaticFieldV2LexicalCoverageBoundary.DirectlyDeclaredMemberSelectionDeferred,
        StaticFieldV2LexicalCoverageBoundary.EventTablesNotModeled,
        StaticFieldV2LexicalCoverageBoundary.PropertyAccessorSemanticsNotModeled,
    ];

    private static readonly ImmutableArray<StaticFieldV2LexicalCoverageBoundary> BareRootBoundaries =
    [
        StaticFieldV2LexicalCoverageBoundary.RangeAndPatternVariableNamesNotModeled,
        StaticFieldV2LexicalCoverageBoundary.LocalFunctionParentRelationNotPhysical,
        StaticFieldV2LexicalCoverageBoundary.DirectlyDeclaredMemberSelectionDeferred,
        StaticFieldV2LexicalCoverageBoundary.UsingStaticInheritedMembersNotImported,
        StaticFieldV2LexicalCoverageBoundary.EventTablesNotModeled,
        StaticFieldV2LexicalCoverageBoundary.ImportedEventGroupBlockingNotModeled,
        StaticFieldV2LexicalCoverageBoundary.PropertyAccessorSemanticsNotModeled,
    ];

    private static readonly ImmutableArray<StaticFieldV2LexicalBlockerKind> BlockerKinds =
    [
        StaticFieldV2LexicalBlockerKind.LocalVariable,
        StaticFieldV2LexicalBlockerKind.LocalConstant,
        StaticFieldV2LexicalBlockerKind.Parameter,
        StaticFieldV2LexicalBlockerKind.TypeParameter,
        StaticFieldV2LexicalBlockerKind.LocalFunction,
        StaticFieldV2LexicalBlockerKind.TypeMemberName,
        StaticFieldV2LexicalBlockerKind.NestedTypeName,
    ];

    /// <summary>Certifies every higher-precedence lexical blocker for one bare identifier at one instruction.</summary>
    /// <param name="request">The complete lexical-completeness certificate request.</param>
    /// <remarks>
    /// Each blocker kind is certified independently and its disposition is always retained. A kind that actually
    /// declares the spelling is complete-and-owned even when another kind carried incomplete evidence, because a name
    /// that was found is positive proof. A kind whose catalog cannot be proved complete is incomplete and never
    /// silently becomes an absence.
    /// </remarks>
    /// <returns>A sealed immutable certificate that is one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2LexicalCertificateOutcome CertifyBlockers(
        StaticFieldV2LexicalCertificateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evidence = request.LexicalEvidence;
        if (evidence.Facts is not { } facts)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.NonExact,
                StaticFieldV2LexicalCertificateIssue.LexicalEvidenceNonExact,
                null,
                0,
                null);
        }

        var portfolio = request.AncestryPortfolio;
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.NonExact)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.NonExact,
                StaticFieldV2LexicalCertificateIssue.AncestryPortfolioNonExact,
                null,
                portfolio.ObservedCount,
                null);
        }
        if (portfolio.ResultKind == MetadataAncestryAuthorityPortfolioResultKind.Invalid)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.Invalid,
                StaticFieldV2LexicalCertificateIssue.AncestryPortfolioInvalid,
                null,
                portfolio.ObservedCount,
                null);
        }

        MetadataDefinitionAuthorityCatalogIdentity? authority = null;
        foreach (var entry in portfolio.Entries)
        {
            if (entry.SourceModule.Equals(request.SelectedModule))
            {
                authority = entry.ResolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority;
                break;
            }
        }
        if (authority is null)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.Invalid,
                StaticFieldV2LexicalCertificateIssue.SelectedModuleNotInPortfolio,
                null,
                portfolio.Entries.Length,
                null);
        }

        var selectedToken = request.SelectedTypeDefinition.TypeDefinitionToken;
        var issued = authority.ExactTypeDefinitionOrDefault(selectedToken);
        var classification = portfolio.ExactClassificationOrDefault(request.SelectedModule, selectedToken);
        if (issued is null || !issued.Equals(request.SelectedTypeDefinition) || classification is null)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.Invalid,
                StaticFieldV2LexicalCertificateIssue.SelectedTypeDefinitionNotIssued,
                null,
                0,
                selectedToken);
        }
        if (classification.Role == MetadataTypeDefinitionSemanticRole.ModulePseudoType)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.Unsupported,
                StaticFieldV2LexicalCertificateIssue.ModulePseudoTypeOwner,
                null,
                0,
                selectedToken);
        }
        if (facts.SelectedMethod.DeclaringTypeDefinitionToken != selectedToken)
        {
            return CertificateStop(
                request,
                StaticFieldV2LexicalCertificateResultKind.Invalid,
                StaticFieldV2LexicalCertificateIssue.SelectedMethodDeclaringTypeMismatch,
                null,
                0,
                facts.SelectedMethod.DeclaringTypeDefinitionToken);
        }

        var declarationChain = EnclosingChain(issued, authority);
        var fieldCatalog = SelectedFieldCatalog(request);
        var propertyCatalog = SelectedPropertyCatalog(request);
        var name = request.Identifier.DecodedText;
        var rows = ImmutableArray.CreateBuilder<StaticFieldV2LexicalBlockerRow>(BlockerKinds.Length);
        var examined = 0;

        foreach (var kind in BlockerKinds)
        {
            var certified = CertifyKind(kind, name, facts, declarationChain, authority, fieldCatalog, propertyCatalog);
            examined += certified.ExaminedNameCount;
            if (examined > StaticFieldV2LexicalCertificateOutcome.MaximumLexicalBlockerCount)
            {
                return CertificateStop(
                    request,
                    StaticFieldV2LexicalCertificateResultKind.NonExact,
                    StaticFieldV2LexicalCertificateIssue.LexicalBlockerCountBoundReached,
                    new EvaluationDeterministicBound(
                        ExpressionV2ContractLimits.LexicalBlockerCountBoundName,
                        StaticFieldV2LexicalCertificateOutcome.MaximumLexicalBlockerCount),
                    StaticFieldV2LexicalCertificateOutcome.MaximumLexicalBlockerCount + 1,
                    null);
            }
            rows.Add(certified);
        }

        var blockers = rows.MoveToImmutable();
        StaticFieldV2LexicalBlockerKind? owningKind = null;
        StaticFieldV2LexicalBlockerKind? incompleteKind = null;
        int? owningToken = null;
        foreach (var row in blockers)
        {
            if (owningKind is null && row.Disposition == StaticFieldV2LexicalBlockerDisposition.CompleteAndOwned)
            {
                owningKind = row.Kind;
                owningToken = row.OwningSymbolMetadataToken;
            }
            if (incompleteKind is null && row.Disposition == StaticFieldV2LexicalBlockerDisposition.Incomplete)
            {
                incompleteKind = row.Kind;
            }
        }

        var resultKind = owningKind is not null
            ? StaticFieldV2LexicalCertificateResultKind.Shadowed
            : incompleteKind is not null
                ? StaticFieldV2LexicalCertificateResultKind.Partial
                : StaticFieldV2LexicalCertificateResultKind.Complete;
        var issue = owningKind is not null
            ? StaticFieldV2LexicalCertificateIssue.BlockerOwnsSpelling
            : incompleteKind is not null
                ? StaticFieldV2LexicalCertificateIssue.BlockerEvidenceIncomplete
                : StaticFieldV2LexicalCertificateIssue.None;

        return StaticFieldV2LexicalCertificateOutcome.IssueComplete(
            resultKind,
            issue,
            request,
            blockers,
            owningKind,
            incompleteKind,
            CertificateBoundaries,
            examined,
            owningToken);
    }

    /// <summary>Binds one bare identifier root over lexical blockers, the declaring chain, and imports.</summary>
    /// <param name="request">The complete bare static-field root binding request.</param>
    /// <remarks>
    /// The order is fixed and stops at the first decisive step. A certificate that is not complete propagates
    /// unchanged; the declaring type and each enclosing type outward are consulted next through the shared member
    /// lookup; and only then do active non-shadowed <c>using static</c> imports contribute directly declared members.
    /// Absence is claimable only over an exhaustive scoped context whose every active import target resolved.
    /// <para>
    /// The certificate is a strictly stronger owner precondition than member lookup's own owner check, and the scoped
    /// projection never resolves an import onto the ECMA module pseudo-type. A consulted member lookup that
    /// nevertheless answers unsupported therefore contradicts the certified evidence and is reported as invalid.
    /// </para>
    /// </remarks>
    /// <returns>A sealed immutable outcome that is one complete answer or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static StaticFieldV2BareRootOutcome BindBareStaticRoot(StaticFieldV2BareRootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var certificateRequest = request.CertificateRequest;
        var certificate = CertifyBlockers(certificateRequest);
        switch (certificate.ResultKind)
        {
            case StaticFieldV2LexicalCertificateResultKind.NonExact:
                return BareRootStop(
                    request,
                    certificate,
                    StaticFieldV2BareRootResultKind.NonExact,
                    StaticFieldV2BareRootIssue.CertificateNonExact,
                    null,
                    certificate.ReachedBound,
                    certificate.ObservedCount,
                    certificate.RelatedMetadataToken);
            case StaticFieldV2LexicalCertificateResultKind.Invalid:
                return BareRootStop(
                    request,
                    certificate,
                    StaticFieldV2BareRootResultKind.Invalid,
                    StaticFieldV2BareRootIssue.CertificateInvalid,
                    null,
                    null,
                    certificate.ObservedCount,
                    certificate.RelatedMetadataToken);
            case StaticFieldV2LexicalCertificateResultKind.Unsupported:
                return BareRootStop(
                    request,
                    certificate,
                    StaticFieldV2BareRootResultKind.Unsupported,
                    StaticFieldV2BareRootIssue.CertificateUnsupported,
                    null,
                    null,
                    certificate.ObservedCount,
                    certificate.RelatedMetadataToken);
            case StaticFieldV2LexicalCertificateResultKind.Shadowed:
                return StaticFieldV2BareRootOutcome.IssueComplete(
                    StaticFieldV2BareRootResultKind.Shadowed,
                    StaticFieldV2BareRootIssue.CertificateShadowed,
                    request,
                    certificate,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    BareRootBoundaries,
                    certificate.ObservedCount,
                    certificate.RelatedMetadataToken);
            case StaticFieldV2LexicalCertificateResultKind.Partial:
                return StaticFieldV2BareRootOutcome.IssueComplete(
                    StaticFieldV2BareRootResultKind.Partial,
                    StaticFieldV2BareRootIssue.CertificateIncomplete,
                    request,
                    certificate,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    BareRootBoundaries,
                    certificate.ObservedCount,
                    certificate.RelatedMetadataToken);
            default:
                break;
        }

        var context = request.ScopedContext;
        if (context.ResultKind == StaticFieldV2ScopedContextResultKind.NonExact)
        {
            return BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.NonExact,
                StaticFieldV2BareRootIssue.ScopedContextNonExact,
                null,
                context.ReachedBound,
                context.ObservedCount,
                null);
        }
        if (context.ResultKind == StaticFieldV2ScopedContextResultKind.Invalid)
        {
            return BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.Invalid,
                StaticFieldV2BareRootIssue.ScopedContextInvalid,
                null,
                null,
                context.ObservedCount,
                null);
        }
        if (!context.Request.SelectedModule.Equals(certificateRequest.SelectedModule) ||
            !context.Request.SelectedTypeDefinition.Equals(certificateRequest.SelectedTypeDefinition) ||
            !context.Request.AncestryPortfolio.Equals(certificateRequest.AncestryPortfolio))
        {
            return BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.Invalid,
                StaticFieldV2BareRootIssue.ScopedContextSubjectMismatch,
                null,
                null,
                0,
                certificateRequest.SelectedTypeDefinition.TypeDefinitionToken);
        }

        var authority = SelectedAuthority(certificateRequest)!;
        var declarationChain = EnclosingChain(
            authority.ExactTypeDefinitionOrDefault(certificateRequest.SelectedTypeDefinition.TypeDefinitionToken)!,
            authority);
        var lookups = ImmutableArray.CreateBuilder<StaticFieldV2MemberLookupOutcome>(declarationChain.Length);
        var consulted = 0;

        foreach (var owner in declarationChain)
        {
            var lookup = Lookup(request, certificateRequest.SelectedModule, owner);
            lookups.Add(lookup);
            consulted++;
            if (lookup.ResultKind == StaticFieldV2MemberLookupResultKind.Absent)
            {
                continue;
            }
            if (lookup.ResultKind == StaticFieldV2MemberLookupResultKind.Exact)
            {
                return StaticFieldV2BareRootOutcome.IssueComplete(
                    StaticFieldV2BareRootResultKind.Exact,
                    StaticFieldV2BareRootIssue.None,
                    request,
                    certificate,
                    StaticFieldV2BareRootSource.DeclaringTypeChain,
                    lookup.SelectedCandidate,
                    certificateRequest.SelectedModule,
                    owner,
                    lookups.ToImmutable(),
                    [],
                    BareRootBoundaries,
                    consulted,
                    lookup.SelectedCandidate!.FieldDefinitionToken);
            }
            return PropagateLookup(
                request,
                certificate,
                StaticFieldV2BareRootSource.DeclaringTypeChain,
                lookup,
                lookups.ToImmutable(),
                [],
                consulted);
        }

        return BindImports(request, certificate, certificateRequest, lookups.ToImmutable(), consulted);
    }

    private static StaticFieldV2BareRootOutcome BindImports(
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2LexicalCertificateRequest certificateRequest,
        ImmutableArray<StaticFieldV2MemberLookupOutcome> declaringChainLookups,
        int consulted)
    {
        var context = request.ScopedContext;
        var candidates = ImmutableArray.CreateBuilder<StaticFieldV2BareRootImportCandidate>();
        var accepted = new List<StaticFieldV2BareRootImportCandidate>();
        var unresolvedTargetRetained = false;

        foreach (var level in context.ScopeLevelsCore)
        {
            foreach (var import in level.ImportsCore)
            {
                if (import.Kind != StaticFieldV2ScopedImportKind.UsingStatic || import.IsShadowed)
                {
                    continue;
                }
                if (import.TargetTypeDefinition is not { } ownerType ||
                    import.TargetTypeModule is not { } ownerModule ||
                    !import.IsTargetExact)
                {
                    unresolvedTargetRetained = true;
                    continue;
                }

                var lookup = Lookup(request, ownerModule, ownerType, import.TargetClosedConstruction);
                consulted++;
                if (lookup.ResultKind == StaticFieldV2MemberLookupResultKind.Absent)
                {
                    candidates.Add(StaticFieldV2BareRootOutcome.IssueImportCandidate(
                        import,
                        ownerModule,
                        ownerType,
                        lookup,
                        false,
                        false));
                    continue;
                }
                if (lookup.ResultKind == StaticFieldV2MemberLookupResultKind.Exact)
                {
                    var inherited = lookup.SelectedCandidate!.LevelIndex != 0;
                    var candidate = StaticFieldV2BareRootOutcome.IssueImportCandidate(
                        import,
                        ownerModule,
                        ownerType,
                        lookup,
                        inherited,
                        !inherited);
                    candidates.Add(candidate);
                    if (!inherited)
                    {
                        accepted.Add(candidate);
                    }
                    continue;
                }
                if (lookup.ResultKind is StaticFieldV2MemberLookupResultKind.Ambiguous
                        or StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember &&
                    DecidingLevelIndex(lookup) != 0)
                {
                    candidates.Add(StaticFieldV2BareRootOutcome.IssueImportCandidate(
                        import,
                        ownerModule,
                        ownerType,
                        lookup,
                        true,
                        false));
                    continue;
                }

                candidates.Add(StaticFieldV2BareRootOutcome.IssueImportCandidate(
                    import,
                    ownerModule,
                    ownerType,
                    lookup,
                    false,
                    false));
                return PropagateLookup(
                    request,
                    certificate,
                    StaticFieldV2BareRootSource.UsingStaticImport,
                    lookup,
                    declaringChainLookups,
                    candidates.ToImmutable(),
                    consulted);
            }
        }

        if (accepted.Count > 0)
        {
            var first = accepted[0];
            foreach (var candidate in accepted)
            {
                if (!candidate.Lookup.SelectedCandidate!.FieldRow.Equals(first.Lookup.SelectedCandidate!.FieldRow))
                {
                    return StaticFieldV2BareRootOutcome.IssueComplete(
                        StaticFieldV2BareRootResultKind.Ambiguous,
                        StaticFieldV2BareRootIssue.AmbiguousImportedDeclarations,
                        request,
                        certificate,
                        StaticFieldV2BareRootSource.UsingStaticImport,
                        null,
                        null,
                        null,
                        declaringChainLookups,
                        candidates.ToImmutable(),
                        BareRootBoundaries,
                        consulted,
                        first.Lookup.SelectedCandidate.FieldDefinitionToken);
                }
            }

            return StaticFieldV2BareRootOutcome.IssueComplete(
                StaticFieldV2BareRootResultKind.Exact,
                StaticFieldV2BareRootIssue.None,
                request,
                certificate,
                StaticFieldV2BareRootSource.UsingStaticImport,
                first.Lookup.SelectedCandidate,
                first.OwnerModule,
                first.OwnerTypeDefinition,
                declaringChainLookups,
                candidates.ToImmutable(),
                BareRootBoundaries,
                consulted,
                first.Lookup.SelectedCandidate!.FieldDefinitionToken);
        }

        if (unresolvedTargetRetained)
        {
            return BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.NonExact,
                StaticFieldV2BareRootIssue.UnresolvedUsingStaticTargetRetained,
                StaticFieldV2BareRootSource.UsingStaticImport,
                null,
                consulted,
                null);
        }
        if (!context.IsExhaustive)
        {
            return BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.NonExact,
                StaticFieldV2BareRootIssue.AbsenceNotClaimableOverRetainedEvidence,
                StaticFieldV2BareRootSource.UsingStaticImport,
                null,
                consulted,
                null);
        }

        return StaticFieldV2BareRootOutcome.IssueComplete(
            StaticFieldV2BareRootResultKind.Absent,
            StaticFieldV2BareRootIssue.DeclarationAbsent,
            request,
            certificate,
            null,
            null,
            null,
            null,
            declaringChainLookups,
            candidates.ToImmutable(),
            BareRootBoundaries,
            consulted,
            null);
    }

    private static StaticFieldV2BareRootOutcome PropagateLookup(
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2BareRootSource source,
        StaticFieldV2MemberLookupOutcome lookup,
        ImmutableArray<StaticFieldV2MemberLookupOutcome> declaringChainLookups,
        ImmutableArray<StaticFieldV2BareRootImportCandidate> importCandidates,
        int consulted) =>
        lookup.ResultKind switch
        {
            StaticFieldV2MemberLookupResultKind.Ambiguous => StaticFieldV2BareRootOutcome.IssueComplete(
                StaticFieldV2BareRootResultKind.Ambiguous,
                StaticFieldV2BareRootIssue.AmbiguousDeclarations,
                request,
                certificate,
                source,
                null,
                null,
                null,
                declaringChainLookups,
                importCandidates,
                BareRootBoundaries,
                consulted,
                lookup.RelatedMetadataToken),
            StaticFieldV2MemberLookupResultKind.Partial => StaticFieldV2BareRootOutcome.IssueComplete(
                StaticFieldV2BareRootResultKind.Partial,
                StaticFieldV2BareRootIssue.MemberLookupIncomplete,
                request,
                certificate,
                source,
                null,
                null,
                null,
                declaringChainLookups,
                importCandidates,
                BareRootBoundaries,
                consulted,
                lookup.RelatedMetadataToken),
            StaticFieldV2MemberLookupResultKind.HiddenByUnsupportedMember =>
                StaticFieldV2BareRootOutcome.IssueComplete(
                    StaticFieldV2BareRootResultKind.HiddenByUnsupportedMember,
                    StaticFieldV2BareRootIssue.HiddenByUnsupportedMember,
                    request,
                    certificate,
                    source,
                    null,
                    null,
                    null,
                    declaringChainLookups,
                    importCandidates,
                    BareRootBoundaries,
                    consulted,
                    lookup.RelatedMetadataToken),
            StaticFieldV2MemberLookupResultKind.Invalid or StaticFieldV2MemberLookupResultKind.Unsupported =>
                BareRootStop(
                    request,
                    certificate,
                    StaticFieldV2BareRootResultKind.Invalid,
                    StaticFieldV2BareRootIssue.MemberLookupInvalid,
                    source,
                    null,
                    lookup.ObservedCount,
                    lookup.RelatedMetadataToken),
            _ => BareRootStop(
                request,
                certificate,
                StaticFieldV2BareRootResultKind.NonExact,
                StaticFieldV2BareRootIssue.MemberLookupNonExact,
                source,
                lookup.ReachedBound,
                lookup.ObservedCount,
                lookup.RelatedMetadataToken),
        };

    /// <summary>
    /// Looks the bare identifier up on one imported owner, supplying the import's own decoded construction when it
    /// has one so a field declared through the owner's type parameters is substituted rather than read as ground.
    /// </summary>
    /// <param name="request">The bare-root request.</param>
    /// <param name="ownerModule">The module owning the consulted definition.</param>
    /// <param name="ownerType">The consulted owner definition.</param>
    /// <param name="ownerConstruction">The import's decoded closed construction, or null when it names none.</param>
    /// <returns>The member-lookup outcome for that owner.</returns>
    private static StaticFieldV2MemberLookupOutcome Lookup(
        StaticFieldV2BareRootRequest request,
        StaticFieldMetadataModuleIdentity ownerModule,
        MetadataTypeDefinitionAuthorityIdentity ownerType,
        MetadataClosedTypeIdentity? ownerConstruction = null) =>
        StaticFieldV2MemberLookup.SelectStaticField(StaticFieldV2MemberLookupRequest.Create(
            ownerModule,
            ownerType.TypeDefinitionToken,
            request.CertificateRequest.Identifier,
            request.CertificateRequest.AncestryPortfolio,
            request.CertificateRequest.FieldCatalogsCore,
            request.CertificateRequest.PropertyCatalogsCore,
            request.AccessibilityMode,
            request.RequestingAssembly,
            request.FriendAssemblyGrantsCore,
            ownerClosedConstruction: ownerConstruction));

    private static int DecidingLevelIndex(StaticFieldV2MemberLookupOutcome lookup)
    {
        var levels = lookup.Levels;
        return levels.IsEmpty ? -1 : levels[^1].LevelIndex;
    }

    private static StaticFieldV2LexicalBlockerRow CertifyKind(
        StaticFieldV2LexicalBlockerKind kind,
        string name,
        DumpSelectedMethodLexicalFacts facts,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> declarationChain,
        MetadataDefinitionAuthorityCatalogIdentity authority,
        MetadataFieldDefinitionTableCatalogIdentity? fieldCatalog,
        MetadataPropertyTableCatalogIdentity? propertyCatalog) => kind switch
        {
            StaticFieldV2LexicalBlockerKind.LocalVariable => CertifyLocalVariables(name, facts),
            StaticFieldV2LexicalBlockerKind.LocalConstant => CertifyLocalConstants(name, facts),
            StaticFieldV2LexicalBlockerKind.Parameter => CertifyParameters(name, facts),
            StaticFieldV2LexicalBlockerKind.TypeParameter => CertifyTypeParameters(name, facts),
            StaticFieldV2LexicalBlockerKind.LocalFunction => CertifyLocalFunctions(name, facts),
            StaticFieldV2LexicalBlockerKind.TypeMemberName =>
                CertifyTypeMemberNames(name, declarationChain, authority, fieldCatalog, propertyCatalog),
            _ => CertifyNestedTypeNames(name, declarationChain, authority),
        };

    private static StaticFieldV2LexicalBlockerRow CertifyLocalVariables(
        string name,
        DumpSelectedMethodLexicalFacts facts)
    {
        var rows = facts.ActiveLocalVariables;
        var examined = 0;
        var unnamed = false;
        foreach (var row in rows)
        {
            examined++;
            if (row.Name.Length == 0)
            {
                unnamed = true;
                continue;
            }
            if (string.Equals(row.Name, name, StringComparison.Ordinal))
            {
                return Owned(
                    StaticFieldV2LexicalBlockerKind.LocalVariable,
                    examined,
                    row.Name,
                    row.LocalVariableToken);
            }
        }

        if (facts.ActiveLocalScopes.IsEmpty)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalVariable,
                StaticFieldV2LexicalIncompletenessSource.ActiveScopeUnavailable,
                examined);
        }
        if (unnamed)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalVariable,
                StaticFieldV2LexicalIncompletenessSource.UnnamedLocalSlot,
                examined);
        }
        if (!facts.UnaccountedLocalSlotIndices.IsEmpty)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalVariable,
                StaticFieldV2LexicalIncompletenessSource.UnaccountedLocalSlot,
                examined);
        }

        return Absent(StaticFieldV2LexicalBlockerKind.LocalVariable, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyLocalConstants(
        string name,
        DumpSelectedMethodLexicalFacts facts)
    {
        var rows = facts.ActiveLocalConstants;
        var examined = 0;
        var unnamed = false;
        foreach (var row in rows)
        {
            examined++;
            if (row.Name.Length == 0)
            {
                // A stripped or unreadable constant name is incomplete evidence: the unnamed constant
                // could actually carry the requested identifier and shadow a bare root, so absence is
                // unprovable. This mirrors the CertifyLocalVariables unnamed-slot guard above; constants
                // carry no StandAloneSig slot, so the unaccounted-slot check does not apply to them.
                unnamed = true;
                continue;
            }
            if (string.Equals(row.Name, name, StringComparison.Ordinal))
            {
                return Owned(
                    StaticFieldV2LexicalBlockerKind.LocalConstant,
                    examined,
                    row.Name,
                    row.LocalConstantToken);
            }
        }

        if (facts.ActiveLocalScopes.IsEmpty)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalConstant,
                StaticFieldV2LexicalIncompletenessSource.ActiveScopeUnavailable,
                examined);
        }
        if (unnamed)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalConstant,
                StaticFieldV2LexicalIncompletenessSource.UnnamedLocalSlot,
                examined);
        }

        return Absent(StaticFieldV2LexicalBlockerKind.LocalConstant, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyParameters(
        string name,
        DumpSelectedMethodLexicalFacts facts)
    {
        var method = facts.SelectedMethod;
        var examined = 0;
        var stripped = false;
        foreach (var parameter in method.Parameters)
        {
            if (parameter.SequenceNumber == 0)
            {
                continue;
            }
            examined++;
            if (parameter.Name.Length == 0)
            {
                stripped = true;
                continue;
            }
            if (string.Equals(parameter.Name, name, StringComparison.Ordinal))
            {
                return Owned(
                    StaticFieldV2LexicalBlockerKind.Parameter,
                    examined,
                    parameter.Name,
                    parameter.ParameterToken);
            }
        }

        if (stripped || examined < method.SignatureParameterCount)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.Parameter,
                StaticFieldV2LexicalIncompletenessSource.StrippedParameterName,
                examined);
        }

        return Absent(StaticFieldV2LexicalBlockerKind.Parameter, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyTypeParameters(
        string name,
        DumpSelectedMethodLexicalFacts facts)
    {
        var examined = 0;
        var unnamed = false;
        foreach (var row in facts.MethodGenericParameters.AddRange(facts.DeclaringTypeGenericParameters))
        {
            examined++;
            if (row.Name.Length == 0)
            {
                unnamed = true;
                continue;
            }
            if (string.Equals(row.Name, name, StringComparison.Ordinal))
            {
                return Owned(
                    StaticFieldV2LexicalBlockerKind.TypeParameter,
                    examined,
                    row.Name,
                    row.GenericParameterToken);
            }
        }

        return unnamed
            ? Incomplete(
                StaticFieldV2LexicalBlockerKind.TypeParameter,
                StaticFieldV2LexicalIncompletenessSource.UnnamedTypeParameter,
                examined)
            : Absent(StaticFieldV2LexicalBlockerKind.TypeParameter, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyLocalFunctions(
        string name,
        DumpSelectedMethodLexicalFacts facts)
    {
        var examined = 0;
        var unattributed = false;
        foreach (var row in facts.GeneratedLocalFunctions)
        {
            examined++;
            if (row.MethodOrdinal is null)
            {
                unattributed = true;
                continue;
            }
            if (string.Equals(row.LocalFunctionName, name, StringComparison.Ordinal))
            {
                return Owned(
                    StaticFieldV2LexicalBlockerKind.LocalFunction,
                    examined,
                    row.LocalFunctionName,
                    row.Method.MethodDefinitionToken);
            }
        }

        return unattributed
            ? Incomplete(
                StaticFieldV2LexicalBlockerKind.LocalFunction,
                StaticFieldV2LexicalIncompletenessSource.UnattributedCompilerGeneratedName,
                examined)
            : Absent(StaticFieldV2LexicalBlockerKind.LocalFunction, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyTypeMemberNames(
        string name,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> declarationChain,
        MetadataDefinitionAuthorityCatalogIdentity authority,
        MetadataFieldDefinitionTableCatalogIdentity? fieldCatalog,
        MetadataPropertyTableCatalogIdentity? propertyCatalog)
    {
        if (fieldCatalog is null)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.TypeMemberName,
                StaticFieldV2LexicalIncompletenessSource.MemberCatalogMissing,
                0);
        }
        if (propertyCatalog is null)
        {
            return Incomplete(
                StaticFieldV2LexicalBlockerKind.TypeMemberName,
                StaticFieldV2LexicalIncompletenessSource.MemberCatalogMissing,
                0);
        }

        var examined = 0;
        foreach (var owner in declarationChain)
        {
            foreach (var field in fieldCatalog.RowsForDeclaringTypeOrEmpty(owner))
            {
                examined++;
                if (string.Equals(field.Name, name, StringComparison.Ordinal))
                {
                    return Owned(
                        StaticFieldV2LexicalBlockerKind.TypeMemberName,
                        examined,
                        field.Name,
                        field.FieldDefinitionToken);
                }
            }
            foreach (var methodToken in owner.MethodDefinitionTokens)
            {
                var method = authority.ExactMethodDefinitionOrDefault(methodToken);
                if (method is null)
                {
                    continue;
                }
                examined++;
                if (string.Equals(method.TableRow.Observation.Name, name, StringComparison.Ordinal))
                {
                    return Owned(
                        StaticFieldV2LexicalBlockerKind.TypeMemberName,
                        examined,
                        method.TableRow.Observation.Name,
                        methodToken);
                }
            }
            foreach (var property in propertyCatalog.RowsForDeclaringTypeOrEmpty(owner))
            {
                examined++;
                if (string.Equals(property.Name, name, StringComparison.Ordinal))
                {
                    return Owned(
                        StaticFieldV2LexicalBlockerKind.TypeMemberName,
                        examined,
                        property.Name,
                        property.PropertyToken);
                }
            }
        }

        return Absent(StaticFieldV2LexicalBlockerKind.TypeMemberName, examined);
    }

    private static StaticFieldV2LexicalBlockerRow CertifyNestedTypeNames(
        string name,
        ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> declarationChain,
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var examined = 0;
        foreach (var owner in declarationChain)
        {
            foreach (var candidate in authority.TypeDefinitions)
            {
                if (candidate.EnclosingTypeDefinitionToken != owner.TypeDefinitionToken)
                {
                    continue;
                }
                examined++;
                if (string.Equals(SourceSimpleName(candidate.TypeName), name, StringComparison.Ordinal))
                {
                    return Owned(
                        StaticFieldV2LexicalBlockerKind.NestedTypeName,
                        examined,
                        candidate.TypeName,
                        candidate.TypeDefinitionToken);
                }
            }
        }

        return Absent(StaticFieldV2LexicalBlockerKind.NestedTypeName, examined);
    }

    private static string SourceSimpleName(string typeName)
    {
        var tick = typeName.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? typeName : typeName[..tick];
    }

    private static StaticFieldV2LexicalBlockerRow Absent(
        StaticFieldV2LexicalBlockerKind kind,
        int examinedNameCount) =>
        StaticFieldV2LexicalCertificateOutcome.IssueBlocker(
            kind,
            StaticFieldV2LexicalBlockerDisposition.CompleteAndAbsent,
            StaticFieldV2LexicalIncompletenessSource.None,
            examinedNameCount,
            null,
            null);

    private static StaticFieldV2LexicalBlockerRow Owned(
        StaticFieldV2LexicalBlockerKind kind,
        int examinedNameCount,
        string owningSymbolName,
        int owningSymbolMetadataToken) =>
        StaticFieldV2LexicalCertificateOutcome.IssueBlocker(
            kind,
            StaticFieldV2LexicalBlockerDisposition.CompleteAndOwned,
            StaticFieldV2LexicalIncompletenessSource.None,
            examinedNameCount,
            owningSymbolName,
            owningSymbolMetadataToken);

    private static StaticFieldV2LexicalBlockerRow Incomplete(
        StaticFieldV2LexicalBlockerKind kind,
        StaticFieldV2LexicalIncompletenessSource source,
        int examinedNameCount) =>
        StaticFieldV2LexicalCertificateOutcome.IssueBlocker(
            kind,
            StaticFieldV2LexicalBlockerDisposition.Incomplete,
            source,
            examinedNameCount,
            null,
            null);

    private static MetadataDefinitionAuthorityCatalogIdentity? SelectedAuthority(
        StaticFieldV2LexicalCertificateRequest request)
    {
        foreach (var entry in request.AncestryPortfolio.Entries)
        {
            if (entry.SourceModule.Equals(request.SelectedModule))
            {
                return entry.ResolutionEntry.ChainEntry.ChainCatalog.DefinitionAuthority;
            }
        }
        return null;
    }

    private static MetadataFieldDefinitionTableCatalogIdentity? SelectedFieldCatalog(
        StaticFieldV2LexicalCertificateRequest request)
    {
        var catalogs = request.FieldCatalogsCore;
        if (catalogs.IsDefault)
        {
            return null;
        }
        foreach (var catalog in catalogs)
        {
            if (catalog is not null &&
                catalog.ResultKind == MetadataFieldDefinitionTableResultKind.Exact &&
                catalog.SourceEnds.SourceModule.Equals(request.SelectedModule))
            {
                return catalog;
            }
        }
        return null;
    }

    private static MetadataPropertyTableCatalogIdentity? SelectedPropertyCatalog(
        StaticFieldV2LexicalCertificateRequest request)
    {
        var catalogs = request.PropertyCatalogsCore;
        if (catalogs.IsDefault)
        {
            return null;
        }
        foreach (var catalog in catalogs)
        {
            if (catalog is not null &&
                catalog.ResultKind == MetadataPropertyTableResultKind.Exact &&
                catalog.DeclaredMemberSourceEnds.SourceModule.Equals(request.SelectedModule))
            {
                return catalog;
            }
        }
        return null;
    }

    private static ImmutableArray<MetadataTypeDefinitionAuthorityIdentity> EnclosingChain(
        MetadataTypeDefinitionAuthorityIdentity selectedType,
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var chain = ImmutableArray.CreateBuilder<MetadataTypeDefinitionAuthorityIdentity>();
        var current = selectedType;
        for (var depth = 0; depth <= StaticFieldV2Limits.MaximumNestedTypeDefinitionDepth; depth++)
        {
            chain.Add(current);
            if (current.EnclosingTypeDefinitionToken is not { } enclosing ||
                authority.ExactTypeDefinitionOrDefault(enclosing) is not { } parent)
            {
                break;
            }
            current = parent;
        }
        return chain.ToImmutable();
    }

    private static StaticFieldV2LexicalCertificateOutcome CertificateStop(
        StaticFieldV2LexicalCertificateRequest request,
        StaticFieldV2LexicalCertificateResultKind resultKind,
        StaticFieldV2LexicalCertificateIssue issue,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        StaticFieldV2LexicalCertificateOutcome.IssueStop(
            resultKind,
            issue,
            request,
            CertificateBoundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);

    private static StaticFieldV2BareRootOutcome BareRootStop(
        StaticFieldV2BareRootRequest request,
        StaticFieldV2LexicalCertificateOutcome certificate,
        StaticFieldV2BareRootResultKind resultKind,
        StaticFieldV2BareRootIssue issue,
        StaticFieldV2BareRootSource? source,
        EvaluationDeterministicBound? reachedBound,
        int observedCount,
        int? relatedMetadataToken) =>
        StaticFieldV2BareRootOutcome.IssueStop(
            resultKind,
            issue,
            request,
            certificate,
            source,
            BareRootBoundaries,
            reachedBound,
            observedCount,
            relatedMetadataToken);
}
