using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies the exact semantic route from a non-null runtime target to its declared reference type.</summary>
public enum StaticFieldRuntimeAssignabilityKind
{
    /// <summary>The actual runtime TypeDef is exactly the declared target TypeDef.</summary>
    ExactRuntimeType = 1,

    /// <summary>The actual class, boxed value type, or boxed enum reaches the declared target through exact Extends rows.</summary>
    BaseType = 2,

    /// <summary>A complete bounded InterfaceImpl closure reaches the declared interface TypeDef.</summary>
    InterfaceClosure = 3,

    /// <summary>The declared target is exact runtime-selected System.Object, which admits this managed target.</summary>
    SystemObject = 4,

    /// <summary>The actual target is an array whose exact base identity is the declared runtime-selected System.Array.</summary>
    SystemArray = 5,
}

/// <summary>
/// Freezes one exhaustive InterfaceImpl-table scan filtered to a single implementing TypeDef.
/// </summary>
/// <remarks>
/// The exact module fact pins the complete InterfaceImpl row count. <see cref="MatchingRows"/> retains every row whose
/// Class column equals <see cref="ImplementingType"/> after that complete scan, including an initialized empty result.
/// This lets Product prove both positive edges and their absence without copying unrelated rows from a large module.
/// </remarks>
public sealed class StaticFieldInterfaceImplementationCatalogIdentity :
    IEquatable<StaticFieldInterfaceImplementationCatalogIdentity>
{
    /// <summary>Gets the maximum complete InterfaceImpl table size admitted for one closure catalog scan.</summary>
    public const int MaximumInterfaceImplementationRowsExamined = 4_096;

    /// <summary>Gets the canonical bound name for a complete InterfaceImpl table scan.</summary>
    public const string MaximumInterfaceImplementationRowsExaminedBoundName =
        "static-field.assignability.interfaceimpl-rows-examined";

    private readonly ImmutableArray<StaticFieldInterfaceImplementationRowIdentity> matchingRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldInterfaceImplementationCatalogIdentity(
        StaticFieldTypeDefinitionIdentity implementingType,
        StaticFieldModuleSearchFact sourceModuleFact,
        ImmutableArray<StaticFieldInterfaceImplementationRowIdentity> matchingRows)
    {
        ImplementingType = implementingType;
        SourceModuleFact = sourceModuleFact;
        this.matchingRows = matchingRows;

        var writer = new CanonicalReplayEncoding.Writer(
            "static-field-interface-implementation-catalog-identity",
            1);
        writer.WriteString(MaximumInterfaceImplementationRowsExaminedBoundName);
        writer.WriteInt32(MaximumInterfaceImplementationRowsExamined);
        writer.WriteLengthPrefixedBytes(implementingType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(sourceModuleFact.CanonicalBytes.AsSpan());
        writer.WriteInt32(matchingRows.Length);
        foreach (var row in matchingRows)
        {
            writer.WriteLengthPrefixedBytes(row.CanonicalBytes.AsSpan());
        }

        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact TypeDef whose complete InterfaceImpl owner match set was retained.</summary>
    public StaticFieldTypeDefinitionIdentity ImplementingType { get; }

    /// <summary>Gets the exact exhaustive module fact that pins the complete InterfaceImpl table size.</summary>
    public StaticFieldModuleSearchFact SourceModuleFact { get; }

    /// <summary>Gets the exact complete number of InterfaceImpl rows examined in the source table.</summary>
    public int RowsExamined => SourceModuleFact.InterfaceImplementationRowCount!.Value;

    /// <summary>Gets a canonical-order defensive copy of every exact row owned by <see cref="ImplementingType"/>.</summary>
    public ImmutableArray<StaticFieldInterfaceImplementationRowIdentity> MatchingRows =>
        StaticFieldContractEncoding.Copy(matchingRows);

    /// <summary>Gets the declared deterministic complete-table scan bound.</summary>
    public static EvaluationDeterministicBound DeclaredRowsExaminedBound =>
        new(MaximumInterfaceImplementationRowsExaminedBoundName, MaximumInterfaceImplementationRowsExamined);

    /// <summary>Gets a defensive copy of the complete canonical catalog bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact complete-table owner catalog.</summary>
    /// <param name="implementingType">The TypeDef used to filter InterfaceImpl.Class.</param>
    /// <param name="sourceModuleFact">An exact fact for the TypeDef's complete metadata module.</param>
    /// <param name="matchingRows">Every retained InterfaceImpl row whose Class equals the implementing TypeDef.</param>
    /// <returns>An immutable exhaustive owner catalog suitable for bounded closure composition.</returns>
    /// <exception cref="ArgumentException">
    /// The fact is not exact, belongs to another module/content, or a retained row has another source or owner.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The complete table or a token lies outside its exact bound.</exception>
    public static StaticFieldInterfaceImplementationCatalogIdentity Create(
        StaticFieldTypeDefinitionIdentity implementingType,
        StaticFieldModuleSearchFact sourceModuleFact,
        ImmutableArray<StaticFieldInterfaceImplementationRowIdentity> matchingRows)
    {
        ArgumentNullException.ThrowIfNull(implementingType);
        ArgumentNullException.ThrowIfNull(sourceModuleFact);
        if (sourceModuleFact.Status != StaticFieldModuleSearchStatus.Exact ||
            sourceModuleFact.ModuleContent is not { } moduleContent ||
            !sourceModuleFact.Module.Equals(implementingType.Module) ||
            !moduleContent.Equals(implementingType.ModuleContent) ||
            sourceModuleFact.InterfaceImplementationRowCount is not { } interfaceRowCount ||
            sourceModuleFact.TypeDefinitionRowCount is not { } typeDefinitionRowCount)
        {
            throw new ArgumentException(
                "An InterfaceImpl owner catalog requires the exact complete source module/content fact.",
                nameof(sourceModuleFact));
        }

        if (interfaceRowCount > MaximumInterfaceImplementationRowsExamined)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceModuleFact),
                $"A complete InterfaceImpl scan cannot exceed {MaximumInterfaceImplementationRowsExamined} rows.");
        }

        var typeRowId = implementingType.TypeDefinitionToken & 0x00FF_FFFF;
        if (typeRowId == 0 || typeRowId > typeDefinitionRowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(implementingType),
                "The implementing TypeDef token exceeds the exact source table.");
        }

        var normalizedRows = StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            matchingRows,
            nameof(matchingRows),
            static row => row.CanonicalBytes);
        if (normalizedRows.Length > interfaceRowCount)
        {
            throw new ArgumentException(
                "An owner match set cannot exceed the complete InterfaceImpl table size.",
                nameof(matchingRows));
        }

        var physicalTokens = new HashSet<int>();
        foreach (var row in normalizedRows)
        {
            if (!row.SourceModule.Equals(implementingType.MetadataModule) ||
                !row.ImplementingType.Equals(implementingType))
            {
                throw new ArgumentException(
                    "Every retained InterfaceImpl row must be sourced from and owned by the catalog TypeDef.",
                    nameof(matchingRows));
            }

            if (!physicalTokens.Add(row.InterfaceImplementationToken))
            {
                throw new ArgumentException(
                    "One physical InterfaceImpl row token cannot carry two catalog identities.",
                    nameof(matchingRows));
            }
        }

        return new StaticFieldInterfaceImplementationCatalogIdentity(
            implementingType,
            sourceModuleFact,
            normalizedRows);
    }

    /// <inheritdoc />
    public bool Equals(StaticFieldInterfaceImplementationCatalogIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldInterfaceImplementationCatalogIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>
/// Proves that one exact non-null raw-header-first target is assignable to one exact declared reference type.
/// </summary>
/// <remarks>
/// Product alone owns this semantic relation. Host supplies the raw slot, header, and runtime topology; Product
/// correlates those facts with counted metadata, exact ancestry, the runtime-selected core library, and—when needed—
/// an exhaustive bounded InterfaceImpl closure. A null reference needs no proof because it is assignable to every
/// admitted reference declaration. Missing catalogs, partial metadata, cycles, lookalike core-library types, and a
/// bound stop cannot be represented as exact proof.
/// </remarks>
public sealed class StaticFieldRuntimeAssignabilityProof :
    IEquatable<StaticFieldRuntimeAssignabilityProof>
{
    /// <summary>Gets the maximum exact metadata module facts retained by one proof.</summary>
    public const int MaximumModuleFactCount = 64;

    /// <summary>Gets the maximum TypeDefs traversed by one complete interface closure.</summary>
    public const int MaximumInterfaceClosureNodeCount = 256;

    /// <summary>Gets the maximum number of InterfaceImpl edges on any retained closure path.</summary>
    public const int MaximumInterfaceClosureDepth = 64;

    /// <summary>Gets the maximum complete owner catalogs retained by one proof.</summary>
    public const int MaximumInterfaceCatalogCount = MaximumInterfaceClosureNodeCount;

    /// <summary>Gets the maximum exact owner-matching InterfaceImpl rows retained across one proof.</summary>
    public const int MaximumRetainedInterfaceImplementationCount = 4_096;

    /// <summary>Gets the maximum runtime-reported array interface anchors correlated by Product.</summary>
    public const int MaximumArrayInterfaceAnchorCount =
        ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeInterfaceTypeCount;

    /// <summary>Gets the canonical module-fact count bound name.</summary>
    public const string MaximumModuleFactCountBoundName = "static-field.assignability.module-facts";

    /// <summary>Gets the canonical interface-closure node count bound name.</summary>
    public const string MaximumInterfaceClosureNodeCountBoundName =
        "static-field.assignability.interface-closure-nodes";

    /// <summary>Gets the canonical interface-closure depth bound name.</summary>
    public const string MaximumInterfaceClosureDepthBoundName =
        "static-field.assignability.interface-closure-depth";

    /// <summary>Gets the canonical owner-catalog count bound name.</summary>
    public const string MaximumInterfaceCatalogCountBoundName =
        "static-field.assignability.interface-catalogs";

    /// <summary>Gets the canonical retained InterfaceImpl match count bound name.</summary>
    public const string MaximumRetainedInterfaceImplementationCountBoundName =
        "static-field.assignability.interfaceimpl-matches";

    private readonly ImmutableArray<StaticFieldTypeAncestryIdentity> arrayInterfaceTypeAncestries;
    private readonly ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> interfaceCatalogs;
    private readonly ImmutableArray<StaticFieldModuleSearchFact> moduleFacts;
    private readonly ImmutableArray<StaticFieldTypeDefinitionIdentity> interfacePath;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldRuntimeAssignabilityProof(
        StaticFieldRuntimeAssignabilityKind kind,
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdExactObjectReference objectReference,
        StaticFieldTypeAncestryIdentity? actualTypeAncestry,
        StaticFieldTypeAncestryIdentity? arrayBaseTypeAncestry,
        ImmutableArray<StaticFieldTypeAncestryIdentity> arrayInterfaceTypeAncestries,
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> interfaceCatalogs,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts,
        ImmutableArray<StaticFieldTypeDefinitionIdentity> interfacePath)
    {
        Kind = kind;
        Declaration = declaration;
        ObjectReference = objectReference;
        ActualTypeAncestry = actualTypeAncestry;
        ArrayBaseTypeAncestry = arrayBaseTypeAncestry;
        this.arrayInterfaceTypeAncestries = arrayInterfaceTypeAncestries;
        this.interfaceCatalogs = interfaceCatalogs;
        this.moduleFacts = moduleFacts;
        this.interfacePath = interfacePath;

        var writer = new CanonicalReplayEncoding.Writer("static-field-runtime-assignability-proof", 1);
        writer.WriteInt32((int)kind);
        writer.WriteString(MaximumModuleFactCountBoundName);
        writer.WriteInt32(MaximumModuleFactCount);
        writer.WriteString(MaximumInterfaceClosureNodeCountBoundName);
        writer.WriteInt32(MaximumInterfaceClosureNodeCount);
        writer.WriteString(MaximumInterfaceClosureDepthBoundName);
        writer.WriteInt32(MaximumInterfaceClosureDepth);
        writer.WriteString(MaximumInterfaceCatalogCountBoundName);
        writer.WriteInt32(MaximumInterfaceCatalogCount);
        writer.WriteString(MaximumRetainedInterfaceImplementationCountBoundName);
        writer.WriteInt32(MaximumRetainedInterfaceImplementationCount);
        writer.WriteLengthPrefixedBytes(declaration.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(objectReference.CanonicalBytes.AsSpan());
        WriteOptionalCanonical(writer, actualTypeAncestry?.CanonicalBytes);
        WriteOptionalCanonical(writer, arrayBaseTypeAncestry?.CanonicalBytes);
        WriteCanonicalArray(writer, arrayInterfaceTypeAncestries, static item => item.CanonicalBytes);
        WriteCanonicalArray(writer, interfaceCatalogs, static item => item.CanonicalBytes);
        WriteCanonicalArray(writer, moduleFacts, static item => item.CanonicalBytes);
        WriteCanonicalArray(writer, interfacePath, static item => item.CanonicalBytes);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact semantic assignability route.</summary>
    public StaticFieldRuntimeAssignabilityKind Kind { get; }

    /// <summary>Gets the exact declared reference-valued static field.</summary>
    public StaticFieldSymbolDeclarationIdentity Declaration { get; }

    /// <summary>Gets the exact non-null raw-header-first target whose runtime type was proved.</summary>
    public ClrmdExactObjectReference ObjectReference { get; }

    /// <summary>Gets exact actual TypeDef ancestry for a TypeDef-backed target, otherwise null for an array.</summary>
    public StaticFieldTypeAncestryIdentity? ActualTypeAncestry { get; }

    /// <summary>Gets exact runtime-selected System.Array ancestry only for an array target.</summary>
    public StaticFieldTypeAncestryIdentity? ArrayBaseTypeAncestry { get; }

    /// <summary>Gets defensive copies of every correlated runtime-reported array interface definition.</summary>
    public ImmutableArray<StaticFieldTypeAncestryIdentity> ArrayInterfaceTypeAncestries =>
        StaticFieldContractEncoding.Copy(arrayInterfaceTypeAncestries);

    /// <summary>Gets defensive copies of every exhaustive owner catalog used by interface closure.</summary>
    public ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> InterfaceCatalogs =>
        StaticFieldContractEncoding.Copy(interfaceCatalogs);

    /// <summary>Gets defensive copies of exact counted metadata facts consulted by this proof.</summary>
    public ImmutableArray<StaticFieldModuleSearchFact> ModuleFacts =>
        StaticFieldContractEncoding.Copy(moduleFacts);

    /// <summary>
    /// Gets the deterministic first complete path from an actual class/interface seed to the declared interface;
    /// empty for non-interface routes.
    /// </summary>
    public ImmutableArray<StaticFieldTypeDefinitionIdentity> InterfacePath =>
        StaticFieldContractEncoding.Copy(interfacePath);

    /// <summary>Gets the exact declared reference target TypeDef.</summary>
    public StaticFieldTypeDefinitionIdentity DeclaredTarget => Declaration.ReferenceTarget!.TargetTypeAncestry.SubjectType;

    /// <summary>Gets the declared module-fact count bound.</summary>
    public static EvaluationDeterministicBound DeclaredModuleFactCountBound =>
        new(MaximumModuleFactCountBoundName, MaximumModuleFactCount);

    /// <summary>Gets the declared interface-closure node bound.</summary>
    public static EvaluationDeterministicBound DeclaredInterfaceClosureNodeCountBound =>
        new(MaximumInterfaceClosureNodeCountBoundName, MaximumInterfaceClosureNodeCount);

    /// <summary>Gets the declared interface-closure depth bound.</summary>
    public static EvaluationDeterministicBound DeclaredInterfaceClosureDepthBound =>
        new(MaximumInterfaceClosureDepthBoundName, MaximumInterfaceClosureDepth);

    /// <summary>Gets the declared owner-catalog count bound.</summary>
    public static EvaluationDeterministicBound DeclaredInterfaceCatalogCountBound =>
        new(MaximumInterfaceCatalogCountBoundName, MaximumInterfaceCatalogCount);

    /// <summary>Gets the declared retained InterfaceImpl match count bound.</summary>
    public static EvaluationDeterministicBound DeclaredRetainedInterfaceImplementationCountBound =>
        new(MaximumRetainedInterfaceImplementationCountBoundName, MaximumRetainedInterfaceImplementationCount);

    /// <summary>Gets a defensive copy of the complete canonical proof bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact proof for a TypeDef-backed runtime target.</summary>
    /// <param name="declaration">The exact String, Object, or CLASS-reference declaration.</param>
    /// <param name="objectReference">The exact non-null slot target and raw-header runtime type.</param>
    /// <param name="actualTypeAncestry">Exact ancestry for the runtime target's TypeDef.</param>
    /// <param name="interfaceCatalogs">Complete owner catalogs, required only for an interface target.</param>
    /// <param name="moduleFacts">Exact counted metadata facts sufficient for every retained relation.</param>
    /// <returns>An immutable proof of exact type, base, Object, or complete interface assignability.</returns>
    /// <exception cref="ArgumentException">The runtime target is not assignable or evidence is incomplete/inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A fixed module, catalog, row, node, or depth bound is exceeded.</exception>
    public static StaticFieldRuntimeAssignabilityProof ForTypeDefinition(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdExactObjectReference objectReference,
        StaticFieldTypeAncestryIdentity actualTypeAncestry,
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> interfaceCatalogs,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts)
    {
        ArgumentNullException.ThrowIfNull(actualTypeAncestry);
        var normalizedFacts = ValidateCommon(declaration, objectReference, moduleFacts);
        var runtimeType = objectReference.HeaderRuntimeType;
        if (runtimeType.Kind != ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition ||
            actualTypeAncestry.Classification == StaticFieldTypeClassification.Interface)
        {
            throw new ArgumentException(
                "A TypeDef assignability proof requires a non-interface TypeDef-backed runtime target.",
                nameof(actualTypeAncestry));
        }

        ValidateAncestryWithinFacts(actualTypeAncestry, normalizedFacts, nameof(actualTypeAncestry));
        ValidateRuntimeAnchor(runtimeType, actualTypeAncestry, nameof(actualTypeAncestry));
        var targetAncestry = declaration.ReferenceTarget!.TargetTypeAncestry;
        StaticFieldRuntimeAssignabilityKind kind;
        ImmutableArray<StaticFieldTypeDefinitionIdentity> path;
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> normalizedCatalogs;
        if (targetAncestry.Classification == StaticFieldTypeClassification.Interface)
        {
            normalizedCatalogs = ValidateCatalogs(interfaceCatalogs, normalizedFacts);
            path = CompleteInterfaceClosure(
                TypeDefinitionAncestryNodes(actualTypeAncestry),
                targetAncestry.SubjectType,
                normalizedCatalogs,
                normalizedFacts);
            kind = StaticFieldRuntimeAssignabilityKind.InterfaceClosure;
        }
        else
        {
            RequireEmptyCatalogs(interfaceCatalogs);
            normalizedCatalogs = ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty;
            path = ImmutableArray<StaticFieldTypeDefinitionIdentity>.Empty;
            kind = ClassifyNonInterfaceRoute(declaration.ReferenceTarget, actualTypeAncestry);
        }

        return new StaticFieldRuntimeAssignabilityProof(
            kind,
            declaration,
            objectReference,
            actualTypeAncestry,
            arrayBaseTypeAncestry: null,
            ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty,
            normalizedCatalogs,
            normalizedFacts,
            path);
    }

    /// <summary>Creates an exact proof for a TypeDef-less runtime array target.</summary>
    /// <param name="declaration">The exact Object, System.Array, or non-generic interface declaration.</param>
    /// <param name="objectReference">The exact non-null array slot target and raw-header runtime topology.</param>
    /// <param name="arrayBaseTypeAncestry">Exact runtime-selected System.Array ancestry matching the raw base identity.</param>
    /// <param name="arrayInterfaceTypeAncestries">
    /// Every runtime-reported interface definition, required in full only for an interface declaration.
    /// </param>
    /// <param name="interfaceCatalogs">Complete owner catalogs for all reachable array interface definitions.</param>
    /// <param name="moduleFacts">Exact counted metadata facts sufficient for every retained relation.</param>
    /// <returns>An immutable Object, System.Array, or complete interface-closure proof.</returns>
    /// <exception cref="ArgumentException">The runtime array is not assignable or evidence is incomplete/inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A fixed module, catalog, row, node, or depth bound is exceeded.</exception>
    public static StaticFieldRuntimeAssignabilityProof ForArray(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdExactObjectReference objectReference,
        StaticFieldTypeAncestryIdentity arrayBaseTypeAncestry,
        ImmutableArray<StaticFieldTypeAncestryIdentity> arrayInterfaceTypeAncestries,
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> interfaceCatalogs,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts)
    {
        ArgumentNullException.ThrowIfNull(arrayBaseTypeAncestry);
        var normalizedFacts = ValidateCommon(declaration, objectReference, moduleFacts);
        var runtimeArray = objectReference.HeaderRuntimeType;
        if (runtimeArray.Kind != ClrmdStaticRuntimeTypeIdentityKind.Array || runtimeArray.BaseType is null)
        {
            throw new ArgumentException("An array assignability proof requires an exact runtime array target.", nameof(objectReference));
        }

        ValidateAncestryWithinFacts(arrayBaseTypeAncestry, normalizedFacts, nameof(arrayBaseTypeAncestry));
        ValidateRuntimeAnchor(runtimeArray.BaseType, arrayBaseTypeAncestry, nameof(arrayBaseTypeAncestry));
        ValidateSystemArray(arrayBaseTypeAncestry);

        var declaredReference = declaration.ReferenceTarget!;
        var declaredTarget = declaredReference.TargetTypeAncestry.SubjectType;
        StaticFieldRuntimeAssignabilityKind kind;
        ImmutableArray<StaticFieldTypeAncestryIdentity> normalizedInterfaceAncestries;
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> normalizedCatalogs;
        ImmutableArray<StaticFieldTypeDefinitionIdentity> path;
        if (declaredReference.Kind == StaticFieldDeclaredReferenceKind.SystemObject)
        {
            RequireSameCoreLibrary(declaredReference.TargetTypeAncestry, arrayBaseTypeAncestry);
            RequireEmptyArrayInterfaceEvidence(arrayInterfaceTypeAncestries, interfaceCatalogs);
            normalizedInterfaceAncestries = ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty;
            normalizedCatalogs = ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty;
            path = ImmutableArray<StaticFieldTypeDefinitionIdentity>.Empty;
            kind = StaticFieldRuntimeAssignabilityKind.SystemObject;
        }
        else if (declaredReference.Kind == StaticFieldDeclaredReferenceKind.ManagedReference &&
                 declaredTarget.Equals(arrayBaseTypeAncestry.SubjectType))
        {
            RequireEmptyArrayInterfaceEvidence(arrayInterfaceTypeAncestries, interfaceCatalogs);
            normalizedInterfaceAncestries = ImmutableArray<StaticFieldTypeAncestryIdentity>.Empty;
            normalizedCatalogs = ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity>.Empty;
            path = ImmutableArray<StaticFieldTypeDefinitionIdentity>.Empty;
            kind = StaticFieldRuntimeAssignabilityKind.SystemArray;
        }
        else if (declaredReference.Kind == StaticFieldDeclaredReferenceKind.ManagedReference &&
                 declaredReference.TargetTypeAncestry.Classification == StaticFieldTypeClassification.Interface)
        {
            normalizedInterfaceAncestries = ValidateArrayInterfaceAnchors(
                runtimeArray,
                arrayInterfaceTypeAncestries,
                normalizedFacts);
            normalizedCatalogs = ValidateCatalogs(interfaceCatalogs, normalizedFacts);
            path = CompleteInterfaceClosure(
                normalizedInterfaceAncestries.Select(static ancestry => ancestry.SubjectType).ToImmutableArray(),
                declaredTarget,
                normalizedCatalogs,
                normalizedFacts);
            kind = StaticFieldRuntimeAssignabilityKind.InterfaceClosure;
        }
        else
        {
            throw new ArgumentException(
                "A TypeDef-less array is assignable only to exact System.Object, System.Array, or a proved interface.",
                nameof(declaration));
        }

        return new StaticFieldRuntimeAssignabilityProof(
            kind,
            declaration,
            objectReference,
            actualTypeAncestry: null,
            arrayBaseTypeAncestry,
            normalizedInterfaceAncestries,
            normalizedCatalogs,
            normalizedFacts,
            path);
    }

    /// <inheritdoc />
    public bool Equals(StaticFieldRuntimeAssignabilityProof? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldRuntimeAssignabilityProof);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ImmutableArray<StaticFieldModuleSearchFact> ValidateCommon(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdExactObjectReference objectReference,
        ImmutableArray<StaticFieldModuleSearchFact> moduleFacts)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(objectReference);
        if (declaration.DeclaredValueKind is not (
                StaticFieldDeclaredValueKind.String or
                StaticFieldDeclaredValueKind.ManagedReference or
                StaticFieldDeclaredValueKind.Object) ||
            declaration.ReferenceTarget is null)
        {
            throw new ArgumentException("Runtime assignability applies only to an admitted reference declaration.", nameof(declaration));
        }

        if (!string.Equals(
                declaration.Module.SnapshotSha256,
                objectReference.Snapshot.Sha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Declaration and runtime target belong to different snapshots.", nameof(objectReference));
        }

        if (moduleFacts.IsDefaultOrEmpty || moduleFacts.Length > MaximumModuleFactCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(moduleFacts),
                $"One through {MaximumModuleFactCount} exact module facts are required.");
        }

        var normalizedFacts = StaticFieldSymbolContractEncoding.NormalizeModuleFacts(moduleFacts, nameof(moduleFacts));
        if (normalizedFacts.Any(fact =>
                fact.Status != StaticFieldModuleSearchStatus.Exact ||
                !string.Equals(fact.Module.SnapshotSha256, objectReference.Snapshot.Sha256, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "An exact assignability proof can retain only exact counted module facts from the target snapshot.",
                nameof(moduleFacts));
        }

        ValidateAncestryWithinFacts(
            declaration.ReferenceTarget.TargetTypeAncestry,
            normalizedFacts,
            nameof(declaration));
        return normalizedFacts;
    }

    private static StaticFieldRuntimeAssignabilityKind ClassifyNonInterfaceRoute(
        StaticFieldDeclaredReferenceIdentity declaredReference,
        StaticFieldTypeAncestryIdentity actualTypeAncestry)
    {
        var actual = actualTypeAncestry.SubjectType;
        var target = declaredReference.TargetTypeAncestry.SubjectType;
        if (actual.Equals(target))
        {
            return StaticFieldRuntimeAssignabilityKind.ExactRuntimeType;
        }

        if (declaredReference.Kind == StaticFieldDeclaredReferenceKind.SystemString)
        {
            throw new ArgumentException("A string target must have exact runtime-selected System.String type.");
        }

        if (declaredReference.Kind == StaticFieldDeclaredReferenceKind.SystemObject)
        {
            RequireSameCoreLibrary(declaredReference.TargetTypeAncestry, actualTypeAncestry);
            return StaticFieldRuntimeAssignabilityKind.SystemObject;
        }

        if (actualTypeAncestry.Edges.Any(edge => edge.ResolvedBaseType.Equals(target)))
        {
            return StaticFieldRuntimeAssignabilityKind.BaseType;
        }

        throw new ArgumentException("The actual runtime TypeDef ancestry does not reach the declared reference target.");
    }

    private static void RequireSameCoreLibrary(
        StaticFieldTypeAncestryIdentity declaredObject,
        StaticFieldTypeAncestryIdentity actualType)
    {
        if (declaredObject.CoreLibrary is null ||
            actualType.CoreLibrary is null ||
            !declaredObject.SubjectType.Equals(declaredObject.CoreLibrary.SystemObjectType) ||
            !declaredObject.CoreLibrary.Equals(actualType.CoreLibrary))
        {
            throw new ArgumentException(
                "System.Object assignability requires the same exact runtime-selected core-library identity.");
        }
    }

    private static void ValidateSystemArray(StaticFieldTypeAncestryIdentity ancestry)
    {
        var type = ancestry.SubjectType;
        var flags = (TypeAttributes)type.TypeAttributes;
        if (ancestry.Classification != StaticFieldTypeClassification.ReferenceClass ||
            ancestry.CoreLibrary is null ||
            !type.MetadataModule.Equals(ancestry.CoreLibrary.MetadataModule) ||
            !type.IsTopLevel || type.GenericArity != 0 ||
            !string.Equals(type.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(type.TypeName, "Array", StringComparison.Ordinal) ||
            (flags & TypeAttributes.VisibilityMask) != TypeAttributes.Public ||
            (flags & TypeAttributes.Abstract) == 0 ||
            (flags & TypeAttributes.Sealed) != 0)
        {
            throw new ArgumentException(
                "An array base anchor must be exact public abstract System.Array in the runtime-selected core library.",
                nameof(ancestry));
        }
    }

    private static ImmutableArray<StaticFieldTypeAncestryIdentity> ValidateArrayInterfaceAnchors(
        ClrmdStaticRuntimeTypeIdentity runtimeArray,
        ImmutableArray<StaticFieldTypeAncestryIdentity> ancestries,
        ImmutableArray<StaticFieldModuleSearchFact> facts)
    {
        if (ancestries.IsDefault || ancestries.Length > MaximumArrayInterfaceAnchorCount ||
            ancestries.Length != runtimeArray.InterfaceTypes.Length)
        {
            throw new ArgumentException(
                "Interface assignability for an array requires one Product ancestry anchor per runtime-reported interface.",
                nameof(ancestries));
        }

        var normalized = StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            ancestries,
            nameof(ancestries),
            static ancestry => ancestry.CanonicalBytes);
        var byPhysicalType = new Dictionary<string, StaticFieldTypeAncestryIdentity>(StringComparer.Ordinal);
        foreach (var ancestry in normalized)
        {
            if (ancestry.Classification != StaticFieldTypeClassification.Interface ||
                !byPhysicalType.TryAdd(TypeKey(ancestry.SubjectType), ancestry))
            {
                throw new ArgumentException(
                    "Every array interface anchor must identify one distinct exact interface TypeDef.",
                    nameof(ancestries));
            }

            ValidateAncestryWithinFacts(ancestry, facts, nameof(ancestries));
        }

        foreach (var runtimeInterface in runtimeArray.InterfaceTypes)
        {
            var match = normalized.SingleOrDefault(ancestry => RuntimeCoordinatesMatch(runtimeInterface, ancestry.SubjectType));
            if (match is null)
            {
                throw new ArgumentException(
                    "Every raw array interface identity must correlate with one exact Product metadata interface.",
                    nameof(ancestries));
            }

            ValidateRuntimeAnchor(runtimeInterface, match, nameof(ancestries));
        }

        return normalized;
    }

    private static ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> ValidateCatalogs(
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> catalogs,
        ImmutableArray<StaticFieldModuleSearchFact> facts)
    {
        if (catalogs.IsDefault || catalogs.Length > MaximumInterfaceCatalogCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(catalogs),
                $"At most {MaximumInterfaceCatalogCount} initialized owner catalogs are admitted.");
        }

        var normalized = StaticFieldContractEncoding.NormalizeUniqueAllowEmpty(
            catalogs,
            nameof(catalogs),
            static catalog => catalog.CanonicalBytes);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        var retainedRows = 0;
        foreach (var catalog in normalized)
        {
            if (!owners.Add(TypeKey(catalog.ImplementingType)))
            {
                throw new ArgumentException("One implementing TypeDef cannot have two closure catalogs.", nameof(catalogs));
            }

            var exactFact = facts.SingleOrDefault(fact =>
                fact.Module.Equals(catalog.SourceModuleFact.Module) &&
                Equals(fact.ModuleContent, catalog.SourceModuleFact.ModuleContent));
            if (exactFact is null || !exactFact.Equals(catalog.SourceModuleFact))
            {
                throw new ArgumentException(
                    "Every owner catalog must reuse one exact retained module fact without relabeling it.",
                    nameof(catalogs));
            }

            retainedRows = checked(retainedRows + catalog.MatchingRows.Length);
            if (retainedRows > MaximumRetainedInterfaceImplementationCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(catalogs),
                    $"A proof cannot retain more than {MaximumRetainedInterfaceImplementationCount} owner-matching rows.");
            }

            foreach (var row in catalog.MatchingRows)
            {
                StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
                    row,
                    facts,
                    nameof(catalogs));
            }
        }

        return normalized;
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity> CompleteInterfaceClosure(
        ImmutableArray<StaticFieldTypeDefinitionIdentity> seeds,
        StaticFieldTypeDefinitionIdentity declaredInterface,
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> catalogs,
        ImmutableArray<StaticFieldModuleSearchFact> facts)
    {
        if (!declaredInterface.IsInterface || seeds.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A complete interface closure requires non-empty seeds and an interface target.");
        }

        var catalogsByOwner = catalogs.ToDictionary(
            static catalog => TypeKey(catalog.ImplementingType),
            StringComparer.Ordinal);
        var queue = new Queue<StaticFieldTypeDefinitionIdentity>();
        var typesByKey = new Dictionary<string, StaticFieldTypeDefinitionIdentity>(StringComparer.Ordinal);
        var parentByKey = new Dictionary<string, string?>(StringComparer.Ordinal);
        var depthByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var seed in seeds)
        {
            var key = TypeKey(seed);
            if (typesByKey.TryAdd(key, seed))
            {
                parentByKey[key] = null;
                depthByKey[key] = 0;
                queue.Enqueue(seed);
            }
        }

        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var usedCatalogKeys = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            var currentKey = TypeKey(current);
            if (!catalogsByOwner.TryGetValue(currentKey, out var catalog) ||
                !catalog.ImplementingType.Equals(current))
            {
                throw new ArgumentException(
                    "A complete interface closure requires one exhaustive owner catalog for every reachable TypeDef.",
                    nameof(catalogs));
            }

            usedCatalogKeys.Add(currentKey);
            var targets = new HashSet<string>(StringComparer.Ordinal);
            adjacency[currentKey] = targets;
            foreach (var row in catalog.MatchingRows)
            {
                StaticFieldSymbolContractEncoding.ValidateInterfaceImplementationWithinFacts(
                    row,
                    facts,
                    nameof(catalogs));
                var target = row.ResolvedInterfaceType;
                var targetKey = TypeKey(target);
                targets.Add(targetKey);
                if (typesByKey.TryGetValue(targetKey, out var prior))
                {
                    if (!prior.Equals(target))
                    {
                        throw new ArgumentException("One physical interface TypeDef has conflicting retained identities.");
                    }
                    continue;
                }

                var depth = checked(depthByKey[currentKey] + 1);
                if (depth > MaximumInterfaceClosureDepth)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(catalogs),
                        $"An interface closure path cannot exceed {MaximumInterfaceClosureDepth} edges.");
                }

                if (typesByKey.Count >= MaximumInterfaceClosureNodeCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(catalogs),
                        $"An interface closure cannot retain more than {MaximumInterfaceClosureNodeCount} TypeDefs.");
                }

                typesByKey[targetKey] = target;
                parentByKey[targetKey] = currentKey;
                depthByKey[targetKey] = depth;
                queue.Enqueue(target);
            }
        }

        if (usedCatalogKeys.Count != catalogs.Length)
        {
            throw new ArgumentException(
                "An exact interface proof cannot retain owner catalogs outside its complete reachable closure.",
                nameof(catalogs));
        }

        RejectInterfaceCycles(adjacency);
        var declaredKey = TypeKey(declaredInterface);
        if (!typesByKey.TryGetValue(declaredKey, out var reached) || !reached.Equals(declaredInterface))
        {
            throw new ArgumentException("The complete InterfaceImpl closure does not reach the declared interface.");
        }

        var reversedPath = new List<StaticFieldTypeDefinitionIdentity>();
        string? pathKey = declaredKey;
        while (pathKey is not null)
        {
            reversedPath.Add(typesByKey[pathKey]);
            pathKey = parentByKey[pathKey];
        }

        reversedPath.Reverse();
        return ImmutableArray.CreateRange(reversedPath);
    }

    private static void RejectInterfaceCycles(Dictionary<string, HashSet<string>> adjacency)
    {
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in adjacency.Keys)
        {
            Visit(node);
        }

        void Visit(string node)
        {
            if (states.TryGetValue(node, out var state))
            {
                if (state == 1)
                {
                    throw new ArgumentException("A complete InterfaceImpl closure cannot contain an interface cycle.");
                }
                return;
            }

            states[node] = 1;
            if (adjacency.TryGetValue(node, out var targets))
            {
                foreach (var target in targets)
                {
                    Visit(target);
                }
            }
            states[node] = 2;
        }
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity> TypeDefinitionAncestryNodes(
        StaticFieldTypeAncestryIdentity ancestry)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity>(ancestry.Edges.Length + 1);
        builder.Add(ancestry.SubjectType);
        foreach (var edge in ancestry.Edges)
        {
            builder.Add(edge.ResolvedBaseType);
        }
        return builder.MoveToImmutable();
    }

    private static void ValidateRuntimeAnchor(
        ClrmdStaticRuntimeTypeIdentity runtimeType,
        StaticFieldTypeAncestryIdentity ancestry,
        string parameterName)
    {
        var expectedName = StaticFieldRuntimeComposition.RuntimeFullName(ancestry.SubjectType);
        if (!runtimeType.GenericArguments.IsEmpty)
        {
            var delimiter = expectedName.LastIndexOf('`');
            if (delimiter < 0 ||
                !int.TryParse(expectedName.AsSpan(delimiter + 1), out var arity) ||
                arity != runtimeType.GenericArguments.Length ||
                arity != ancestry.SubjectType.GenericArity)
            {
                throw new ArgumentException(
                    "Constructed runtime generic arguments disagree with the metadata definition arity.",
                    parameterName);
            }

            expectedName = $"{expectedName[..delimiter]}<{string.Join(",", runtimeType.GenericArguments.Select(static argument => argument.FullName))}>";
        }

        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            runtimeType,
            ancestry,
            expectedPrimitive: runtimeType.IsPrimitive,
            expectedGenericArgumentCount: ancestry.SubjectType.GenericArity,
            expectedName,
            parameterName);
    }

    private static void ValidateAncestryWithinFacts(
        StaticFieldTypeAncestryIdentity ancestry,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        string parameterName) =>
        StaticFieldSymbolContractEncoding.ValidateTypeAncestryWithinFactsForComposition(
            ancestry,
            facts,
            parameterName);

    private static bool RuntimeCoordinatesMatch(
        ClrmdStaticRuntimeTypeIdentity runtimeType,
        StaticFieldTypeDefinitionIdentity metadataType) =>
        runtimeType.Kind == ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition &&
        runtimeType.RuntimeModule is { } runtimeModule &&
        runtimeType.ModuleContent is { } moduleContent &&
        runtimeType.TypeDefinitionToken == metadataType.TypeDefinitionToken &&
        StaticFieldRuntimeComposition.RuntimeModuleMatches(metadataType.Module, runtimeModule) &&
        metadataType.ModuleContent.Equals(moduleContent);

    private static string TypeKey(StaticFieldTypeDefinitionIdentity type) =>
        StaticFieldTypeDefinitionIdentity.PhysicalTypeDefinitionKey(type);

    private static void RequireEmptyCatalogs(
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> catalogs)
    {
        if (catalogs.IsDefault || !catalogs.IsEmpty)
        {
            throw new ArgumentException(
                "A non-interface assignability route requires an initialized empty owner-catalog array.",
                nameof(catalogs));
        }
    }

    private static void RequireEmptyArrayInterfaceEvidence(
        ImmutableArray<StaticFieldTypeAncestryIdentity> ancestries,
        ImmutableArray<StaticFieldInterfaceImplementationCatalogIdentity> catalogs)
    {
        if (ancestries.IsDefault || !ancestries.IsEmpty)
        {
            throw new ArgumentException(
                "A direct Object/System.Array route requires an initialized empty array-interface anchor array.",
                nameof(ancestries));
        }
        RequireEmptyCatalogs(catalogs);
    }

    private static void WriteOptionalCanonical(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<byte>? bytes)
    {
        writer.WriteBoolean(bytes.HasValue);
        if (bytes.HasValue)
        {
            writer.WriteLengthPrefixedBytes(bytes.Value.AsSpan());
        }
    }

    private static void WriteCanonicalArray<T>(
        CanonicalReplayEncoding.Writer writer,
        ImmutableArray<T> values,
        Func<T, ImmutableArray<byte>> bytes)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteLengthPrefixedBytes(bytes(value).AsSpan());
        }
    }
}
