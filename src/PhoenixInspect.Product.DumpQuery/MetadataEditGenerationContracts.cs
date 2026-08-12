using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies one edit-generation delta acquisition answer.</summary>
public enum MetadataEditGenerationResultKind
{
    /// <summary>The delta blob decoded completely and its physical facts are coherent.</summary>
    Exact = 1,

    /// <summary>The delta blob or its declared shape is invalid; no fact is claimed.</summary>
    Invalid = 2,

    /// <summary>A declared bound was reached; the blob is retained but not decoded past it.</summary>
    Partial = 3,
}

/// <summary>Names the typed issue of one non-exact edit-generation acquisition.</summary>
public enum MetadataEditGenerationIssue
{
    /// <summary>No issue applies to an exact outcome.</summary>
    None = 0,

    /// <summary>The delta bytes could not be opened as a metadata image.</summary>
    BlobUnreadable = 1,

    /// <summary>The blob's Module row generation disagrees with the declared ordinal.</summary>
    GenerationNumberMismatch = 2,

    /// <summary>The blob's Module row carries an empty edit identifier.</summary>
    EmptyEditIdentifier = 3,

    /// <summary>The delta byte count crossed the declared cap.</summary>
    DeltaByteCountBoundReached = 4,
}

/// <summary>One retained edit-log row: the referenced token and its declared operation.</summary>
public readonly struct MetadataEditLogRow : IEquatable<MetadataEditLogRow>
{
    /// <summary>Initializes one retained edit-log row.</summary>
    /// <param name="token">The metadata token the log row references.</param>
    /// <param name="operation">The declared edit-and-continue operation code.</param>
    public MetadataEditLogRow(int token, int operation)
    {
        Token = token;
        Operation = operation;
    }

    /// <summary>Gets the metadata token the log row references.</summary>
    public int Token { get; }

    /// <summary>Gets the declared edit-and-continue operation code.</summary>
    public int Operation { get; }

    /// <summary>Tests value equality between two edit-log rows.</summary>
    /// <param name="other">The other row.</param>
    /// <returns><see langword="true"/> only for identical token and operation.</returns>
    public bool Equals(MetadataEditLogRow other) => Token == other.Token && Operation == other.Operation;

    /// <summary>Tests edit-log-row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical content.</returns>
    public override bool Equals(object? obj) => obj is MetadataEditLogRow other && Equals(other);

    /// <summary>Computes a deterministic hash code from the row content.</summary>
    /// <returns>A hash code for this row.</returns>
    public override int GetHashCode() => HashCode.Combine(Token, Operation);
}

/// <summary>
/// Freezes one generation's metadata delta as physically read facts: the Module row's lineage identifiers and the
/// complete retained edit-log and edit-map rows.
/// </summary>
/// <remarks>
/// The Edit-and-Continue E1 disposition proves the runtime retains no delta blob in a dump, so delta bytes reach
/// this contract only through a caller-owned artifact seam, exactly as Portable-PDB bytes do; what this contract
/// adds is the physical validation the caller cannot assert — every fact below is decoded from the supplied bytes
/// with prefix-free typed stops, never copied from a manifest. The retained log rows are the vocabulary a later
/// generation-aware composition consumes, including the measured reference-table extension of body-only edits.
/// </remarks>
public sealed class MetadataEditGenerationOutcome : IEquatable<MetadataEditGenerationOutcome>
{
    /// <summary>Gets the maximum admitted delta blob byte count.</summary>
    public const int MaximumDeltaByteCount = 1 * 1_024 * 1_024;

    private const string CanonicalDomain = "metadata-edit-generation";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataEditLogRow> editLogRows;
    private readonly ImmutableArray<int> editMapTokens;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataEditGenerationOutcome(
        MetadataEditGenerationResultKind resultKind,
        MetadataEditGenerationIssue issue,
        int generationNumber,
        Guid moduleVersionId,
        Guid editId,
        Guid editBaseId,
        int deltaByteCount,
        string deltaSha256,
        ImmutableArray<MetadataEditLogRow> editLogRows,
        ImmutableArray<int> editMapTokens)
    {
        ResultKind = resultKind;
        Issue = issue;
        GenerationNumber = generationNumber;
        ModuleVersionId = moduleVersionId;
        EditId = editId;
        EditBaseId = editBaseId;
        DeltaByteCount = deltaByteCount;
        DeltaSha256 = deltaSha256;
        this.editLogRows = editLogRows;
        this.editMapTokens = editMapTokens;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteInt32(generationNumber);
        writer.WriteString(moduleVersionId.ToString("D"));
        writer.WriteString(editId.ToString("D"));
        writer.WriteString(editBaseId.ToString("D"));
        writer.WriteInt32(deltaByteCount);
        writer.WriteString(deltaSha256);
        writer.WriteInt32(editLogRows.Length);
        foreach (var row in editLogRows)
        {
            writer.WriteInt32(row.Token);
            writer.WriteInt32(row.Operation);
        }
        writer.WriteInt32(editMapTokens.Length);
        foreach (var token in editMapTokens)
        {
            writer.WriteInt32(token);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this acquisition is exact, invalid, or partial.</summary>
    public MetadataEditGenerationResultKind ResultKind { get; }

    /// <summary>Gets the typed issue of a non-exact acquisition.</summary>
    public MetadataEditGenerationIssue Issue { get; }

    /// <summary>Gets the caller-declared one-based generation ordinal this blob was supplied as.</summary>
    public int GenerationNumber { get; }

    /// <summary>Gets the blob's own module version identifier; empty for every stop before the Module row.</summary>
    public Guid ModuleVersionId { get; }

    /// <summary>Gets the blob's own edit identifier; empty for every stop before the Module row.</summary>
    public Guid EditId { get; }

    /// <summary>Gets the blob's own base edit identifier joining it to its predecessor.</summary>
    public Guid EditBaseId { get; }

    /// <summary>Gets the supplied delta blob byte count.</summary>
    public int DeltaByteCount { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the supplied delta blob bytes.</summary>
    public string DeltaSha256 { get; }

    /// <summary>Gets a defensive copy of the complete retained edit-log rows in physical order.</summary>
    public ImmutableArray<MetadataEditLogRow> EditLogRows => ExpressionV2ContractEncoding.Copy(editLogRows);

    /// <summary>Gets a defensive copy of the complete retained edit-map tokens in physical order.</summary>
    public ImmutableArray<int> EditMapTokens => ExpressionV2ContractEncoding.Copy(editMapTokens);

    /// <summary>Gets a defensive copy of the fixed-reference canonical outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Acquires one generation's physical facts from its supplied metadata delta bytes.</summary>
    /// <param name="generationNumber">The one-based generation ordinal the caller supplies this blob as.</param>
    /// <param name="metadataDeltaBytes">The complete metadata delta blob bytes.</param>
    /// <returns>A sealed immutable outcome that is one exact acquisition or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The generation ordinal is not positive.</exception>
    /// <exception cref="ArgumentException">The delta bytes are default.</exception>
    public static MetadataEditGenerationOutcome Acquire(
        int generationNumber,
        ImmutableArray<byte> metadataDeltaBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generationNumber);
        if (metadataDeltaBytes.IsDefault)
        {
            throw new ArgumentException("Initialized delta bytes are required.", nameof(metadataDeltaBytes));
        }

        var deltaSha256 = CanonicalReplayEncoding.ComputeSha256(metadataDeltaBytes.AsSpan());
        if (metadataDeltaBytes.Length > MaximumDeltaByteCount)
        {
            return new MetadataEditGenerationOutcome(
                MetadataEditGenerationResultKind.Partial,
                MetadataEditGenerationIssue.DeltaByteCountBoundReached,
                generationNumber,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                metadataDeltaBytes.Length,
                deltaSha256,
                [],
                []);
        }

        Guid moduleVersionId;
        Guid editId;
        Guid editBaseId;
        int declaredGeneration;
        ImmutableArray<MetadataEditLogRow> logRows;
        ImmutableArray<int> mapTokens;
        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataDeltaBytes);
            var reader = provider.GetMetadataReader();
            var moduleRow = reader.GetModuleDefinition();
            declaredGeneration = moduleRow.Generation;
            moduleVersionId = reader.GetGuid(moduleRow.Mvid);
            editId = reader.GetGuid(moduleRow.GenerationId);
            editBaseId = reader.GetGuid(moduleRow.BaseGenerationId);
            logRows =
            [
                .. reader.GetEditAndContinueLogEntries().Select(static entry =>
                    new MetadataEditLogRow(MetadataTokens.GetToken(entry.Handle), (int)entry.Operation)),
            ];
            mapTokens = [.. reader.GetEditAndContinueMapEntries().Select(MetadataTokens.GetToken)];
        }
        catch (BadImageFormatException)
        {
            return new MetadataEditGenerationOutcome(
                MetadataEditGenerationResultKind.Invalid,
                MetadataEditGenerationIssue.BlobUnreadable,
                generationNumber,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                metadataDeltaBytes.Length,
                deltaSha256,
                [],
                []);
        }

        if (declaredGeneration != generationNumber)
        {
            return new MetadataEditGenerationOutcome(
                MetadataEditGenerationResultKind.Invalid,
                MetadataEditGenerationIssue.GenerationNumberMismatch,
                generationNumber,
                moduleVersionId,
                editId,
                editBaseId,
                metadataDeltaBytes.Length,
                deltaSha256,
                [],
                []);
        }

        if (editId == Guid.Empty)
        {
            return new MetadataEditGenerationOutcome(
                MetadataEditGenerationResultKind.Invalid,
                MetadataEditGenerationIssue.EmptyEditIdentifier,
                generationNumber,
                moduleVersionId,
                editId,
                editBaseId,
                metadataDeltaBytes.Length,
                deltaSha256,
                [],
                []);
        }

        return new MetadataEditGenerationOutcome(
            MetadataEditGenerationResultKind.Exact,
            MetadataEditGenerationIssue.None,
            generationNumber,
            moduleVersionId,
            editId,
            editBaseId,
            metadataDeltaBytes.Length,
            deltaSha256,
            logRows,
            mapTokens);
    }

    /// <summary>Tests canonical equality between two edit-generation outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataEditGenerationOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests edit-generation outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataEditGenerationOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical outcome content.</summary>
    /// <returns>A hash code for this canonical outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Classifies one edit lineage-chain composition answer.</summary>
public enum MetadataEditLineageChainResultKind
{
    /// <summary>Every generation is exact and the chain joins pair by pair.</summary>
    Exact = 1,

    /// <summary>A generation or a join contradicts the frozen lineage relation.</summary>
    Invalid = 2,

    /// <summary>A declared bound was reached before the chain completed.</summary>
    Partial = 3,

    /// <summary>The chain disagrees with the physically acquired edit state it was declared against.</summary>
    Conflict = 4,
}

/// <summary>Names the typed issue of one non-exact lineage-chain composition.</summary>
public enum MetadataEditLineageChainIssue
{
    /// <summary>No issue applies to an exact chain.</summary>
    None = 0,

    /// <summary>A supplied generation was itself non-exact.</summary>
    GenerationNotExact = 1,

    /// <summary>A generation's module version identifier differs from the baseline's.</summary>
    ForeignModuleVersionId = 2,

    /// <summary>A generation's base edit identifier does not equal its predecessor's edit identifier.</summary>
    ChainPairMismatch = 3,

    /// <summary>The generation count crossed the declared cap.</summary>
    GenerationCountBoundReached = 4,

    /// <summary>The chain length disagrees with the declared physically acquired applied-generation count.</summary>
    GenerationCountDisagreesWithEditState = 5,

    /// <summary>The declared edit state is itself non-exact, so no chain can be validated against it.</summary>
    EditStateNotExact = 6,
}

/// <summary>
/// Freezes one module's edit lineage chain: the baseline identity and every supplied generation joined by the
/// measured pairing rule, optionally validated against the module's physically acquired edit state.
/// </summary>
/// <remarks>
/// The pairing rule is the E1-measured one, not a hypothesis: every generation shares the baseline's module
/// version identifier and carries its own nonempty edit identifier; the first generation's base identifier is
/// empty because generation zero has no edit identifier at all; and each later generation's base identifier
/// equals its predecessor's edit identifier exactly. A declared edit state joins the caller-supplied artifacts to
/// the dump's own truth: a chain whose length disagrees with the physically read applied-generation count is a
/// typed conflict, never a preference for either side.
/// </remarks>
public sealed class MetadataEditLineageChainOutcome : IEquatable<MetadataEditLineageChainOutcome>
{
    /// <summary>Gets the maximum admitted generation count of one lineage chain.</summary>
    public const int MaximumGenerationCount = 64;

    private const string CanonicalDomain = "metadata-edit-lineage-chain";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataEditGenerationOutcome> generations;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataEditLineageChainOutcome(
        MetadataEditLineageChainResultKind resultKind,
        MetadataEditLineageChainIssue issue,
        Guid baselineModuleVersionId,
        ImmutableArray<MetadataEditGenerationOutcome> generations)
    {
        ResultKind = resultKind;
        Issue = issue;
        BaselineModuleVersionId = baselineModuleVersionId;
        this.generations = generations;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteString(baselineModuleVersionId.ToString("D"));
        writer.WriteInt32(generations.Length);
        foreach (var generation in generations)
        {
            writer.WriteSha256(generation.Sha256, nameof(generations));
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this chain is exact, invalid, partial, or in conflict with the edit state.</summary>
    public MetadataEditLineageChainResultKind ResultKind { get; }

    /// <summary>Gets the typed issue of a non-exact chain.</summary>
    public MetadataEditLineageChainIssue Issue { get; }

    /// <summary>Gets the baseline module version identifier every generation must share.</summary>
    public Guid BaselineModuleVersionId { get; }

    /// <summary>Gets a defensive copy of the retained generation outcomes in chain order.</summary>
    public ImmutableArray<MetadataEditGenerationOutcome> Generations =>
        ExpressionV2ContractEncoding.Copy(generations);

    /// <summary>Gets the composed generation count; zero for every stop.</summary>
    public int GenerationCount =>
        ResultKind == MetadataEditLineageChainResultKind.Exact ? generations.Length : 0;

    /// <summary>Gets a defensive copy of the fixed-reference canonical chain bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical chain.</summary>
    public string Sha256 { get; }

    /// <summary>Composes one lineage chain from the baseline identity and the ordered supplied generations.</summary>
    /// <param name="baselineModuleVersionId">The baseline module's version identifier.</param>
    /// <param name="generations">The supplied generation outcomes in one-based chain order.</param>
    /// <param name="declaredEditState">
    /// The optional physically acquired edit state of the module's runtime instance; a chain whose length
    /// disagrees with its applied-generation count is a typed conflict.
    /// </param>
    /// <returns>A sealed immutable outcome that is one exact chain or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentException">The generations vector is default or the baseline identifier empty.</exception>
    public static MetadataEditLineageChainOutcome Compose(
        Guid baselineModuleVersionId,
        ImmutableArray<MetadataEditGenerationOutcome> generations,
        StaticFieldV2ModuleEditStateOutcome? declaredEditState = null)
    {
        if (baselineModuleVersionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A nonempty baseline module version identifier is required.",
                nameof(baselineModuleVersionId));
        }
        if (generations.IsDefault)
        {
            throw new ArgumentException("An initialized generations vector is required.", nameof(generations));
        }

        var copied = ExpressionV2ContractEncoding.Copy(generations);
        if (copied.Length > MaximumGenerationCount)
        {
            return new MetadataEditLineageChainOutcome(
                MetadataEditLineageChainResultKind.Partial,
                MetadataEditLineageChainIssue.GenerationCountBoundReached,
                baselineModuleVersionId,
                []);
        }

        var expectedBaseId = Guid.Empty;
        for (var index = 0; index < copied.Length; index++)
        {
            var generation = copied[index];
            if (generation.ResultKind != MetadataEditGenerationResultKind.Exact ||
                generation.GenerationNumber != index + 1)
            {
                return new MetadataEditLineageChainOutcome(
                    MetadataEditLineageChainResultKind.Invalid,
                    MetadataEditLineageChainIssue.GenerationNotExact,
                    baselineModuleVersionId,
                    []);
            }
            if (generation.ModuleVersionId != baselineModuleVersionId)
            {
                return new MetadataEditLineageChainOutcome(
                    MetadataEditLineageChainResultKind.Invalid,
                    MetadataEditLineageChainIssue.ForeignModuleVersionId,
                    baselineModuleVersionId,
                    []);
            }
            if (generation.EditBaseId != expectedBaseId)
            {
                return new MetadataEditLineageChainOutcome(
                    MetadataEditLineageChainResultKind.Invalid,
                    MetadataEditLineageChainIssue.ChainPairMismatch,
                    baselineModuleVersionId,
                    []);
            }

            expectedBaseId = generation.EditId;
        }

        // A non-exact declared edit state is refused rather than counted as zero applied generations: the caller
        // asked for validation against the dump's own truth, and an unavailable or invalid truth validates
        // nothing — a filtered capture therefore stops here, before any catalog is issued.
        if (declaredEditState is not null &&
            declaredEditState.ResultKind != StaticFieldV2ModuleEditStateResultKind.Exact)
        {
            return new MetadataEditLineageChainOutcome(
                MetadataEditLineageChainResultKind.Invalid,
                MetadataEditLineageChainIssue.EditStateNotExact,
                baselineModuleVersionId,
                []);
        }

        if (declaredEditState is not null && declaredEditState.AppliedGenerationCount != copied.Length)
        {
            return new MetadataEditLineageChainOutcome(
                MetadataEditLineageChainResultKind.Conflict,
                MetadataEditLineageChainIssue.GenerationCountDisagreesWithEditState,
                baselineModuleVersionId,
                []);
        }

        return new MetadataEditLineageChainOutcome(
            MetadataEditLineageChainResultKind.Exact,
            MetadataEditLineageChainIssue.None,
            baselineModuleVersionId,
            copied);
    }

    /// <summary>Tests canonical equality between two lineage-chain outcomes.</summary>
    /// <param name="other">The other outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataEditLineageChainOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests lineage-chain outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataEditLineageChainOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical chain content.</summary>
    /// <returns>A hash code for this canonical chain.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Classifies one effective-row projection answer.</summary>
public enum MetadataEditEffectiveProjectionResultKind
{
    /// <summary>Every generation's rows projected onto coherent effective table states.</summary>
    Exact = 1,

    /// <summary>A row contradicted the effective state; no projection is claimed.</summary>
    Invalid = 2,
}

/// <summary>Names the typed issue of one non-exact effective-row projection.</summary>
public enum MetadataEditEffectiveProjectionIssue
{
    /// <summary>No issue applies to an exact projection.</summary>
    None = 0,

    /// <summary>The supplied lineage chain was itself non-exact.</summary>
    ChainNotExact = 1,

    /// <summary>A log row touched a table whose base row end was not declared.</summary>
    TableEndUndeclared = 2,

    /// <summary>An added row's identifier skipped past the table's effective end.</summary>
    AddedRowGap = 3,

    /// <summary>A directive row was the last row of its generation, with no following added row.</summary>
    DirectiveWithoutFollowingRow = 4,
}

/// <summary>One caller-declared base table end: the table index and its baseline row count.</summary>
public readonly struct MetadataEditDeclaredTableEnd : IEquatable<MetadataEditDeclaredTableEnd>
{
    /// <summary>Initializes one declared base table end.</summary>
    /// <param name="tableIndex">The metadata table index.</param>
    /// <param name="rowCount">The baseline image's row count for that table.</param>
    public MetadataEditDeclaredTableEnd(int tableIndex, int rowCount)
    {
        TableIndex = tableIndex;
        RowCount = rowCount;
    }

    /// <summary>Gets the metadata table index.</summary>
    public int TableIndex { get; }

    /// <summary>Gets the baseline image's row count for the table.</summary>
    public int RowCount { get; }

    /// <summary>Tests value equality between two declared table ends.</summary>
    /// <param name="other">The other declared end.</param>
    /// <returns><see langword="true"/> only for identical table index and row count.</returns>
    public bool Equals(MetadataEditDeclaredTableEnd other) =>
        TableIndex == other.TableIndex && RowCount == other.RowCount;

    /// <summary>Tests declared-table-end equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an end with identical content.</returns>
    public override bool Equals(object? obj) => obj is MetadataEditDeclaredTableEnd other && Equals(other);

    /// <summary>Computes a deterministic hash code from the declared content.</summary>
    /// <returns>A hash code for this declared end.</returns>
    public override int GetHashCode() => HashCode.Combine(TableIndex, RowCount);
}

/// <summary>One projected effective row: the token, whether the chain added it, and its generation span.</summary>
public readonly struct MetadataEditEffectiveRow : IEquatable<MetadataEditEffectiveRow>
{
    /// <summary>Initializes one projected effective row.</summary>
    /// <param name="token">The metadata token of the row.</param>
    /// <param name="isAdded">Whether the chain added the row past the baseline table end.</param>
    /// <param name="firstGeneration">The first generation that added or updated the row.</param>
    /// <param name="latestGeneration">The latest generation that added or updated the row.</param>
    public MetadataEditEffectiveRow(int token, bool isAdded, int firstGeneration, int latestGeneration)
    {
        Token = token;
        IsAdded = isAdded;
        FirstGeneration = firstGeneration;
        LatestGeneration = latestGeneration;
    }

    /// <summary>Gets the metadata token of the row.</summary>
    public int Token { get; }

    /// <summary>Gets whether the chain added the row past the baseline table end.</summary>
    public bool IsAdded { get; }

    /// <summary>Gets the first generation that added or updated the row.</summary>
    public int FirstGeneration { get; }

    /// <summary>Gets the latest generation that added or updated the row.</summary>
    public int LatestGeneration { get; }

    /// <summary>Tests value equality between two effective rows.</summary>
    /// <param name="other">The other row.</param>
    /// <returns><see langword="true"/> only for identical content.</returns>
    public bool Equals(MetadataEditEffectiveRow other) =>
        Token == other.Token &&
        IsAdded == other.IsAdded &&
        FirstGeneration == other.FirstGeneration &&
        LatestGeneration == other.LatestGeneration;

    /// <summary>Tests effective-row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical content.</returns>
    public override bool Equals(object? obj) => obj is MetadataEditEffectiveRow other && Equals(other);

    /// <summary>Computes a deterministic hash code from the row content.</summary>
    /// <returns>A hash code for this row.</returns>
    public override int GetHashCode() => HashCode.Combine(Token, IsAdded, FirstGeneration, LatestGeneration);
}

/// <summary>
/// Projects one exact lineage chain onto the logical table state its generations produce: per-table effective row
/// ends and a per-token classification of every touched row as chain-added or chain-updated.
/// </summary>
/// <remarks>
/// This is the E4 core model over the retained E3 rows. Updated rows supersede without erasure — the retained
/// per-generation log rows in the chain remain the physical history — and added rows extend each table's row
/// domain contiguously past the caller-declared baseline end, which the measured delta shapes require: a
/// generation's added reference and definition rows continue exactly at the effective end. A directive row (a
/// nonzero operation such as the measured add-field, add-method, and add-parameter codes) orders the addition
/// that follows it and never classifies its parent as updated; the parent row's content is not superseded by a
/// membership change.
/// </remarks>
public sealed class MetadataEditEffectiveProjectionOutcome : IEquatable<MetadataEditEffectiveProjectionOutcome>
{
    private const string CanonicalDomain = "metadata-edit-effective-projection";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<MetadataEditDeclaredTableEnd> effectiveTableEnds;
    private readonly ImmutableArray<MetadataEditEffectiveRow> effectiveRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private MetadataEditEffectiveProjectionOutcome(
        MetadataEditEffectiveProjectionResultKind resultKind,
        MetadataEditEffectiveProjectionIssue issue,
        string chainSha256,
        ImmutableArray<MetadataEditDeclaredTableEnd> effectiveTableEnds,
        ImmutableArray<MetadataEditEffectiveRow> effectiveRows)
    {
        ResultKind = resultKind;
        Issue = issue;
        ChainSha256 = chainSha256;
        this.effectiveTableEnds = effectiveTableEnds;
        this.effectiveRows = effectiveRows;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(chainSha256, nameof(chainSha256));
        writer.WriteInt32(effectiveTableEnds.Length);
        foreach (var end in effectiveTableEnds)
        {
            writer.WriteInt32(end.TableIndex);
            writer.WriteInt32(end.RowCount);
        }
        writer.WriteInt32(effectiveRows.Length);
        foreach (var row in effectiveRows)
        {
            writer.WriteInt32(row.Token);
            writer.WriteInt32(row.IsAdded ? 1 : 0);
            writer.WriteInt32(row.FirstGeneration);
            writer.WriteInt32(row.LatestGeneration);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this projection is exact or invalid.</summary>
    public MetadataEditEffectiveProjectionResultKind ResultKind { get; }

    /// <summary>Gets the typed issue of a non-exact projection.</summary>
    public MetadataEditEffectiveProjectionIssue Issue { get; }

    /// <summary>Gets the digest of the projected lineage chain.</summary>
    public string ChainSha256 { get; }

    /// <summary>Gets a defensive copy of the per-table effective row ends after every generation.</summary>
    public ImmutableArray<MetadataEditDeclaredTableEnd> EffectiveTableEnds =>
        ExpressionV2ContractEncoding.Copy(effectiveTableEnds);

    /// <summary>Gets a defensive copy of every touched row's classification, in ascending token order.</summary>
    public ImmutableArray<MetadataEditEffectiveRow> EffectiveRows =>
        ExpressionV2ContractEncoding.Copy(effectiveRows);

    /// <summary>Gets a defensive copy of the fixed-reference canonical projection bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical projection.</summary>
    public string Sha256 { get; }

    /// <summary>Projects one exact chain over the caller-declared baseline table ends.</summary>
    /// <param name="chain">The exact lineage chain whose generations are projected in order.</param>
    /// <param name="declaredTableEnds">The baseline row count of every table the chain's rows may touch.</param>
    /// <returns>A sealed immutable outcome that is one exact projection or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chain"/> is null.</exception>
    /// <exception cref="ArgumentException">The declared ends are default or name one table twice.</exception>
    public static MetadataEditEffectiveProjectionOutcome Project(
        MetadataEditLineageChainOutcome chain,
        ImmutableArray<MetadataEditDeclaredTableEnd> declaredTableEnds)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (declaredTableEnds.IsDefault)
        {
            throw new ArgumentException("Initialized declared table ends are required.", nameof(declaredTableEnds));
        }

        var ends = new Dictionary<int, int>();
        foreach (var declared in declaredTableEnds)
        {
            if (!ends.TryAdd(declared.TableIndex, declared.RowCount))
            {
                throw new ArgumentException(
                    "Each table's baseline end may be declared exactly once.",
                    nameof(declaredTableEnds));
            }
        }

        if (chain.ResultKind != MetadataEditLineageChainResultKind.Exact)
        {
            return new MetadataEditEffectiveProjectionOutcome(
                MetadataEditEffectiveProjectionResultKind.Invalid,
                MetadataEditEffectiveProjectionIssue.ChainNotExact,
                chain.Sha256,
                [],
                []);
        }

        var rows = new Dictionary<int, MetadataEditEffectiveRow>();
        foreach (var generation in chain.Generations)
        {
            var logRows = generation.EditLogRows;
            for (var index = 0; index < logRows.Length; index++)
            {
                var logRow = logRows[index];
                if (logRow.Operation != 0)
                {
                    if (index == logRows.Length - 1)
                    {
                        return new MetadataEditEffectiveProjectionOutcome(
                            MetadataEditEffectiveProjectionResultKind.Invalid,
                            MetadataEditEffectiveProjectionIssue.DirectiveWithoutFollowingRow,
                            chain.Sha256,
                            [],
                            []);
                    }

                    continue;
                }

                var tableIndex = logRow.Token >>> 24;
                var rowId = logRow.Token & 0x00_FF_FF_FF;
                if (!ends.TryGetValue(tableIndex, out var effectiveEnd))
                {
                    return new MetadataEditEffectiveProjectionOutcome(
                        MetadataEditEffectiveProjectionResultKind.Invalid,
                        MetadataEditEffectiveProjectionIssue.TableEndUndeclared,
                        chain.Sha256,
                        [],
                        []);
                }

                if (rowId <= effectiveEnd)
                {
                    rows[logRow.Token] = rows.TryGetValue(logRow.Token, out var existing)
                        ? new MetadataEditEffectiveRow(
                            logRow.Token,
                            existing.IsAdded,
                            existing.FirstGeneration,
                            generation.GenerationNumber)
                        : new MetadataEditEffectiveRow(
                            logRow.Token,
                            isAdded: false,
                            generation.GenerationNumber,
                            generation.GenerationNumber);
                }
                else if (rowId == effectiveEnd + 1)
                {
                    ends[tableIndex] = effectiveEnd + 1;
                    rows[logRow.Token] = new MetadataEditEffectiveRow(
                        logRow.Token,
                        isAdded: true,
                        generation.GenerationNumber,
                        generation.GenerationNumber);
                }
                else
                {
                    return new MetadataEditEffectiveProjectionOutcome(
                        MetadataEditEffectiveProjectionResultKind.Invalid,
                        MetadataEditEffectiveProjectionIssue.AddedRowGap,
                        chain.Sha256,
                        [],
                        []);
                }
            }
        }

        return new MetadataEditEffectiveProjectionOutcome(
            MetadataEditEffectiveProjectionResultKind.Exact,
            MetadataEditEffectiveProjectionIssue.None,
            chain.Sha256,
            [.. ends.OrderBy(static pair => pair.Key)
                .Select(static pair => new MetadataEditDeclaredTableEnd(pair.Key, pair.Value))],
            [.. rows.Values.OrderBy(static row => row.Token)]);
    }

    /// <summary>Tests canonical equality between two effective projections.</summary>
    /// <param name="other">The other projection.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(MetadataEditEffectiveProjectionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests effective-projection equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a projection with identical canonical content.</returns>
    public override bool Equals(object? obj) => Equals(obj as MetadataEditEffectiveProjectionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical projection content.</summary>
    /// <returns>A hash code for this canonical projection.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}
