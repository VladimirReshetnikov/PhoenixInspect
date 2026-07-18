using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Resolves context-independent <c>global::Namespace.Type.Field</c> and unambiguous dot-qualified static-field
/// expressions against complete metadata images read from every managed module in one dump snapshot.
/// </summary>
/// <remarks>
/// This draft W7 binder consumes the immutable output of <see cref="StaticFieldExpressionParser"/> and never
/// reparses expression text. It retains every admitted syntax split, searches the bounded module catalog
/// exhaustively, and selects a declaration only when one complete declaration-and-shape interpretation remains.
/// Bare <c>Type.Field</c> spellings require selected-frame/import context and are intentionally left to the
/// contextual binder. Manifest modules and netmodules joined by the exact runtime assembly-address relation are
/// admitted. Caveat: this prototype admits the field signature shapes represented by
/// <see cref="StaticFieldDeclaredValueKind"/>; a located competing shape outside that set produces a typed
/// unsupported result rather than a speculative selection.
/// </remarks>
public static class StaticFieldFullyQualifiedBinder
{
    /// <summary>Gets the maximum number of physical modules searched in one binding operation.</summary>
    public const int MaximumModuleCount = 4_096;

    /// <summary>Gets the maximum TypeDef-table row count searched in one metadata module.</summary>
    public const int MaximumTypeDefinitionCountPerModule = 65_536;

    /// <summary>Gets the maximum FieldDef-table row count searched in one metadata module.</summary>
    public const int MaximumFieldDefinitionCountPerModule = 65_536;

    /// <summary>Gets the maximum expansion-to-declaration occurrences retained before grouping.</summary>
    public const int MaximumCandidateOccurrenceCount = 256;

    private const string ModuleCountBoundName = "static-field.binding-modules";
    private const string TypeDefinitionCountBoundName = "static-field.binding-typedef-rows-per-module";
    private const string FieldDefinitionCountBoundName = "static-field.binding-fielddef-rows-per-module";
    private const string CandidateOccurrenceCountBoundName = "static-field.binding-candidate-occurrences";

    /// <summary>Gets the declared physical-module catalog bound.</summary>
    public static EvaluationDeterministicBound DeclaredModuleCountBound =>
        new(ModuleCountBoundName, MaximumModuleCount);

    /// <summary>Gets the declared per-module TypeDef-table bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeDefinitionCountPerModuleBound =>
        new(TypeDefinitionCountBoundName, MaximumTypeDefinitionCountPerModule);

    /// <summary>Gets the declared per-module FieldDef-table bound.</summary>
    public static EvaluationDeterministicBound DeclaredFieldDefinitionCountPerModuleBound =>
        new(FieldDefinitionCountBoundName, MaximumFieldDefinitionCountPerModule);

    /// <summary>Gets the declared expansion-to-declaration occurrence bound.</summary>
    public static EvaluationDeterministicBound DeclaredCandidateOccurrenceCountBound =>
        new(CandidateOccurrenceCountBoundName, MaximumCandidateOccurrenceCount);

    /// <summary>
    /// Resolves every context-independent fully qualified interpretation retained by an accepted static-field
    /// expression descriptor against one immutable dump session.
    /// </summary>
    /// <param name="session">The open ClrMD session supplying one immutable module catalog and counted metadata.</param>
    /// <param name="descriptor">The complete accepted Roslyn projection; it is not reparsed or rewritten.</param>
    /// <returns>
    /// A canonical exact, absent, ambiguous, partial, unavailable, conflicting, invalid, or unsupported binding
    /// outcome. Exact status alone carries a selected declaration.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <remarks>
    /// The operation does not consult a selected stack frame or Portable PDB. Each catalog module's metadata is read
    /// at most once through the session's counted memory-evidence boundary. The caller retains ownership of
    /// <paramref name="session"/>.
    /// </remarks>
    public static StaticFieldSymbolBindingOutcome Bind(
        ClrmdDumpSession session,
        StaticFieldExpressionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Bind(new ClrmdStaticFieldMetadataBindingSource(session), descriptor);
    }

    internal static StaticFieldSymbolBindingOutcome Bind(
        IStaticFieldMetadataBindingSource source,
        StaticFieldExpressionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(descriptor);
        var context = DumpConsultedBindingContextIdentity.ForFullyQualified(source.Snapshot);
        var expansions = CreateExpansions(descriptor);
        if (expansions.IsEmpty)
        {
            return StaticFieldSymbolBindingOutcome.Unsupported(
                descriptor,
                source.Snapshot.Sha256,
                context,
                StaticFieldBindingIssue.ExpansionUnsupported,
                "W7_FQ_CONTEXT_REQUIRED",
                "No retained syntax split contains a context-independent qualified type name.",
                expansions,
                ImmutableArray<StaticFieldModuleSearchFact>.Empty,
                ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                moduleCatalogExhaustive: false,
                expansionSearchExhaustive: false,
                ImmutableArray<EvaluationDeterministicBound>.Empty);
        }

        return BindExpanded(source, descriptor, context, expansions);
    }

    internal static StaticFieldSymbolBindingOutcome BindContextual(
        IStaticFieldMetadataBindingSource source,
        StaticFieldExpressionDescriptor descriptor,
        DumpExpressionBindingContext acquiredContext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(acquiredContext);
        if (acquiredContext.Snapshot != source.Snapshot)
        {
            throw new ArgumentException(
                "The acquired binding context belongs to another dump snapshot.",
                nameof(acquiredContext));
        }

        if (descriptor.HasGlobalQualifier)
        {
            return Bind(source, descriptor);
        }

        var bareShapes = descriptor.CandidateShapes
            .Where(static shape => shape.StaticFieldSegmentIndex == 1)
            .ToImmutableArray();
        var exactImports = acquiredContext.PortablePdb.Facts?.Imports ??
            ImmutableArray<DumpPortablePdbImportFact>.Empty;
        var relevantImports = exactImports
            .Where(import => IsRelevantImport(descriptor, import))
            .ToImmutableArray();
        var hasQualifiedAlias = relevantImports.Any(static import =>
            import.Kind == DumpPortablePdbImportKind.NamespaceAlias);
        if (bareShapes.IsEmpty && !hasQualifiedAlias)
        {
            return Bind(source, descriptor);
        }

        var currentNamespaceConsulted = !bareShapes.IsEmpty;
        var importsConsulted = acquiredContext.PortablePdb.Status == DumpContextEvidenceStatus.Exact &&
            (currentNamespaceConsulted || hasQualifiedAlias);
        if (acquiredContext.SelectedFrame.Status != DumpContextEvidenceStatus.Exact)
        {
            var blockedContext = DumpConsultedBindingContextIdentity.FromAcquiredContext(
                acquiredContext,
                currentNamespaceConsulted,
                importsConsulted: !currentNamespaceConsulted,
                ImmutableArray<DumpPortablePdbImportFact>.Empty);
            return CreateContextStop(
                source,
                descriptor,
                blockedContext,
                acquiredContext.SelectedFrame.Status);
        }

        if (!currentNamespaceConsulted &&
            acquiredContext.PortablePdb.Status != DumpContextEvidenceStatus.Exact)
        {
            var blockedContext = DumpConsultedBindingContextIdentity.FromAcquiredContext(
                acquiredContext,
                currentNamespaceConsulted: false,
                importsConsulted: true,
                ImmutableArray<DumpPortablePdbImportFact>.Empty);
            return CreateContextStop(
                source,
                descriptor,
                blockedContext,
                acquiredContext.PortablePdb.Status);
        }

        var consultedImports = importsConsulted
            ? relevantImports
            : ImmutableArray<DumpPortablePdbImportFact>.Empty;
        var context = DumpConsultedBindingContextIdentity.FromAcquiredContext(
            acquiredContext,
            currentNamespaceConsulted,
            importsConsulted,
            consultedImports);
        var expansions = CreateContextualExpansions(
            descriptor,
            acquiredContext.SelectedFrame.Frame!,
            consultedImports);
        return BindExpanded(source, descriptor, context, expansions);
    }

    internal static StaticFieldSymbolBindingOutcome BindExpanded(
        IStaticFieldMetadataBindingSource source,
        StaticFieldExpressionDescriptor descriptor,
        DumpConsultedBindingContextIdentity context,
        ImmutableArray<StaticFieldNameExpansion> expansions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        if (expansions.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Expanded metadata binding requires an initialized non-empty expansion set.",
                nameof(expansions));
        }

        var reachedBounds = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>();
        reachedBounds.Add(DeclaredModuleCountBound);
        if (source.Modules.IsDefault || source.Modules.Length > MaximumModuleCount ||
            source.Modules.Any(static module => module is null))
        {
            reachedBounds.Add(DeclaredTypeDefinitionCountPerModuleBound);
            reachedBounds.Add(DeclaredFieldDefinitionCountPerModuleBound);
            reachedBounds.Add(DeclaredCandidateOccurrenceCountBound);
            return StaticFieldSymbolBindingOutcome.Partial(
                descriptor,
                source.Snapshot.Sha256,
                context,
                StaticFieldBindingIssue.SearchBoundReached,
                "W7_FQ_MODULE_BOUND",
                "The physical managed-module catalog exceeded the declared binding bound.",
                expansions,
                ImmutableArray<StaticFieldModuleSearchFact>.Empty,
                ImmutableArray<StaticFieldSymbolCandidate>.Empty,
                moduleCatalogExhaustive: false,
                expansionSearchExhaustive: true,
                reachedBounds.ToImmutable());
        }

        reachedBounds.Add(DeclaredTypeDefinitionCountPerModuleBound);
        reachedBounds.Add(DeclaredFieldDefinitionCountPerModuleBound);
        reachedBounds.Add(DeclaredCandidateOccurrenceCountBound);

        using var universe = MetadataUniverse.Acquire(source);
        var facts = universe.ModuleFacts;
        var sourceDisposition = FirstNonExactDisposition(facts);
        if (sourceDisposition is not null)
        {
            return CreateNonExactOutcome(
                descriptor,
                source,
                context,
                expansions,
                facts,
                ImmutableArray<StaticFieldSymbolCandidate>.Empty,
                reachedBounds.ToImmutable(),
                sourceDisposition.Value);
        }

        if (!universe.TryCreateCoreLibrary(out var coreLibrary, out var coreFailureModule))
        {
            facts = ReplaceWithInvalidFact(facts, coreFailureModule);
            return StaticFieldSymbolBindingOutcome.Invalid(
                descriptor,
                source.Snapshot.Sha256,
                context,
                StaticFieldBindingIssue.MetadataInvalid,
                "W7_FQ_CORE_LIBRARY_INVALID",
                "The runtime-selected base-class-library module did not expose the required exact core type roles.",
                expansions,
                facts,
                ImmutableArray<StaticFieldSymbolCandidate>.Empty,
                ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                moduleCatalogExhaustive: true,
                expansionSearchExhaustive: true,
                reachedBounds.ToImmutable());
        }

        var candidates = ImmutableArray.CreateBuilder<StaticFieldSymbolCandidate>();
        var rejected = ImmutableArray.CreateBuilder<StaticFieldRejectedDeclarationEvidence>();
        var retainedCandidateFields = new List<(StaticFieldSymbolCandidate Candidate, StaticFieldDefinitionIdentity Field, StaticFieldDeclaredValueKind Kind)>();
        var ancestryReached = false;
        var candidateBoundReached = false;
        (StaticFieldModuleInstanceIdentity Module, string Code, FatalProjectionDisposition Disposition)? fatalProjection = null;

        foreach (var expansion in expansions)
        {
            foreach (var module in universe.ExactModules)
            {
                foreach (var match in module.FindFields(expansion.NamespaceName, expansion.TypeName, expansion.FieldName))
                {
                    if (candidates.Count + rejected.Count == MaximumCandidateOccurrenceCount)
                    {
                        candidateBoundReached = true;
                        break;
                    }

                    var projection = universe.ProjectDeclaration(module, match.TypeHandle, match.FieldHandle, coreLibrary);
                    if (projection.FieldDefinition is null)
                    {
                        fatalProjection = (
                            module.Input.Module,
                            projection.Code,
                            projection.Code.EndsWith("_BOUND", StringComparison.Ordinal)
                                ? FatalProjectionDisposition.Bound
                                : projection.Issue == StaticFieldBindingIssue.MetadataInvalid
                                    ? FatalProjectionDisposition.Invalid
                                    : FatalProjectionDisposition.Unsupported);
                        break;
                    }

                    ancestryReached = true;
                    if (projection.Issue == StaticFieldBindingIssue.MetadataInvalid)
                    {
                        fatalProjection = (
                            module.Input.Module,
                            projection.Code,
                            FatalProjectionDisposition.Invalid);
                        break;
                    }

                    if (projection.Declaration is not null)
                    {
                        var candidate = StaticFieldSymbolCandidate.Create(
                            projection.Declaration,
                            expansion.CandidateShape,
                            ImmutableArray.Create(expansion));
                        candidates.Add(candidate);
                        retainedCandidateFields.Add((candidate, projection.FieldDefinition!, projection.ValueKind!.Value));
                    }
                    else if (projection.FieldDefinition is not null)
                    {
                        rejected.Add(StaticFieldRejectedDeclarationEvidence.Field(
                            expansion.CandidateShape,
                            expansion,
                            projection.FieldDefinition,
                            projection.ValueKind,
                            projection.Issue,
                            projection.Code));
                    }
                }

                if (candidateBoundReached || fatalProjection.HasValue)
                {
                    break;
                }
            }

            if (candidateBoundReached || fatalProjection.HasValue)
            {
                break;
            }
        }

        if (ancestryReached)
        {
            reachedBounds.Add(StaticFieldTypeAncestryIdentity.DeclaredEdgeCountBound);
        }

        if (fatalProjection is { } fatal)
        {
            if (fatal.Disposition == FatalProjectionDisposition.Unsupported)
            {
                return StaticFieldSymbolBindingOutcome.Unsupported(
                    descriptor,
                    source.Snapshot.Sha256,
                    context,
                    StaticFieldBindingIssue.ExpansionUnsupported,
                    fatal.Code,
                    "A located qualified interpretation requires a metadata relation outside the admitted bounded projection.",
                    expansions,
                    facts,
                    ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                    moduleCatalogExhaustive: true,
                    expansionSearchExhaustive: false,
                    reachedBounds
                        .Where(static bound => !string.Equals(
                            bound.Name,
                            StaticFieldTypeAncestryIdentity.MaximumAncestryEdgeCountBoundName,
                            StringComparison.Ordinal))
                        .ToImmutableArray());
            }

            facts = fatal.Disposition == FatalProjectionDisposition.Bound
                ? ReplaceWithPartialBoundFact(facts, fatal.Module)
                : ReplaceWithInvalidFact(facts, fatal.Module);
            return CreateNonExactOutcome(
                descriptor,
                source,
                context,
                expansions,
                facts,
                ImmutableArray<StaticFieldSymbolCandidate>.Empty,
                reachedBounds
                    .Where(static bound => !string.Equals(
                        bound.Name,
                        StaticFieldTypeAncestryIdentity.MaximumAncestryEdgeCountBoundName,
                        StringComparison.Ordinal))
                    .ToImmutableArray(),
                fatal.Disposition == FatalProjectionDisposition.Bound
                    ? NonExactDisposition.Bound
                    : NonExactDisposition.Invalid);
        }

        if (candidateBoundReached)
        {
            return StaticFieldSymbolBindingOutcome.Partial(
                descriptor,
                source.Snapshot.Sha256,
                context,
                StaticFieldBindingIssue.SearchBoundReached,
                "W7_FQ_CANDIDATE_BOUND",
                "The expansion-to-declaration occurrence search reached its declared bound.",
                expansions,
                facts,
                candidates.ToImmutable(),
                moduleCatalogExhaustive: true,
                expansionSearchExhaustive: false,
                reachedBounds.ToImmutable());
        }

        if (rejected.Count > 0)
        {
            foreach (var retained in retainedCandidateFields)
            {
                var expansion = retained.Candidate.Origins[0];
                rejected.Add(StaticFieldRejectedDeclarationEvidence.Field(
                    retained.Candidate.Shape,
                    expansion,
                    retained.Field,
                    retained.Kind,
                    StaticFieldBindingIssue.DeclarationShapeUnsupported,
                    "W7_FQ_COMPETING_DECLARATION"));
            }

            return StaticFieldSymbolBindingOutcome.Unsupported(
                descriptor,
                source.Snapshot.Sha256,
                context,
                StaticFieldBindingIssue.DeclarationShapeUnsupported,
                "W7_FQ_DECLARATION_UNSUPPORTED",
                "At least one located declaration shape could not participate in an exact version-one selection.",
                expansions,
                facts,
                rejected.ToImmutable(),
                moduleCatalogExhaustive: true,
                expansionSearchExhaustive: true,
                reachedBounds.ToImmutable());
        }

        var occurrences = candidates.ToImmutable();
        var grouped = StaticFieldSymbolContractEncoding.GroupCandidates(
            occurrences,
            descriptor,
            source.Snapshot.Sha256,
            expansions,
            facts,
            nameof(candidates));
        if (grouped.Length == 0)
        {
            return StaticFieldSymbolBindingOutcome.Absent(
                descriptor,
                source.Snapshot.Sha256,
                context,
                expansions,
                facts,
                "W7_FQ_DECLARATION_ABSENT",
                "No exact directly declared ordinary static field matched any retained qualified interpretation.",
                reachedBounds.ToImmutable());
        }

        if (grouped.Length == 1)
        {
            return StaticFieldSymbolBindingOutcome.Exact(
                descriptor,
                source.Snapshot.Sha256,
                context,
                expansions,
                facts,
                occurrences,
                reachedBounds.ToImmutable());
        }

        return StaticFieldSymbolBindingOutcome.Ambiguous(
            descriptor,
            source.Snapshot.Sha256,
            context,
            expansions,
            facts,
            occurrences,
            "W7_FQ_MULTIPLE_DECLARATIONS",
            "More than one complete physical declaration or syntax interpretation matched the qualified expression.",
            reachedBounds.ToImmutable());
    }

    private static ImmutableArray<StaticFieldNameExpansion> CreateExpansions(
        StaticFieldExpressionDescriptor descriptor)
    {
        var segments = descriptor.Segments;
        var builder = ImmutableArray.CreateBuilder<StaticFieldNameExpansion>();
        foreach (var shape in descriptor.CandidateShapes)
        {
            var prefixLength = shape.StaticFieldSegmentIndex;
            if (prefixLength == 0)
            {
                continue;
            }

            var contextIndependent = descriptor.HasGlobalQualifier || prefixLength >= 2;
            if (!contextIndependent)
            {
                continue;
            }

            var typeName = segments[prefixLength - 1].DecodedIdentifier;
            var namespaceName = prefixLength == 1
                ? string.Empty
                : string.Join('.', segments.Take(prefixLength - 1).Select(static segment => segment.DecodedIdentifier));
            builder.Add(StaticFieldNameExpansion.Create(
                shape,
                descriptor.HasGlobalQualifier
                    ? StaticFieldNameExpansionKind.GlobalQualified
                    : StaticFieldNameExpansionKind.DotQualified,
                namespaceName,
                typeName,
                segments[shape.StaticFieldSegmentIndex].DecodedIdentifier));
        }

        return builder.ToImmutable();
    }

    private static bool IsRelevantImport(
        StaticFieldExpressionDescriptor descriptor,
        DumpPortablePdbImportFact import)
    {
        var segments = descriptor.Segments;
        return import.Kind switch
        {
            DumpPortablePdbImportKind.Namespace =>
                import.AssemblyReferenceToken is null &&
                descriptor.CandidateShapes.Any(static shape => shape.StaticFieldSegmentIndex == 1),
            DumpPortablePdbImportKind.TypeAlias => false,
            DumpPortablePdbImportKind.NamespaceAlias =>
                import.AssemblyReferenceToken is null &&
                descriptor.CandidateShapes.Any(shape =>
                    shape.StaticFieldSegmentIndex >= 2 &&
                    string.Equals(
                        segments[0].DecodedIdentifier,
                        import.Alias,
                        StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static ImmutableArray<StaticFieldNameExpansion> CreateContextualExpansions(
        StaticFieldExpressionDescriptor descriptor,
        DumpSelectedFrameIdentity frame,
        ImmutableArray<DumpPortablePdbImportFact> imports)
    {
        var segments = descriptor.Segments;
        var builder = ImmutableArray.CreateBuilder<StaticFieldNameExpansion>();
        builder.AddRange(CreateExpansions(descriptor));
        foreach (var shape in descriptor.CandidateShapes)
        {
            var prefixLength = shape.StaticFieldSegmentIndex;
            var fieldName = segments[prefixLength].DecodedIdentifier;
            if (prefixLength == 1)
            {
                var typeName = segments[0].DecodedIdentifier;
                builder.Add(StaticFieldNameExpansion.Create(
                    shape,
                    StaticFieldNameExpansionKind.CurrentNamespace,
                    frame.DeclaringNamespace,
                    typeName,
                    fieldName,
                    contextFactSha256: frame.Sha256));
                foreach (var import in imports.Where(static import =>
                             import.Kind == DumpPortablePdbImportKind.Namespace &&
                             import.AssemblyReferenceToken is null))
                {
                    builder.Add(StaticFieldNameExpansion.Create(
                        shape,
                        StaticFieldNameExpansionKind.NamespaceImport,
                        import.Target!,
                        typeName,
                        fieldName,
                        contextFactSha256: import.Sha256));
                }
            }

            foreach (var import in imports.Where(import =>
                         import.Kind == DumpPortablePdbImportKind.NamespaceAlias &&
                         import.AssemblyReferenceToken is null &&
                         prefixLength >= 2 &&
                         string.Equals(segments[0].DecodedIdentifier, import.Alias, StringComparison.Ordinal)))
            {
                var relativeNamespace = prefixLength == 2
                    ? string.Empty
                    : string.Join('.', segments
                        .Skip(1)
                        .Take(prefixLength - 2)
                        .Select(static segment => segment.DecodedIdentifier));
                var namespaceName = relativeNamespace.Length == 0
                    ? import.Target!
                    : $"{import.Target}.{relativeNamespace}";
                builder.Add(StaticFieldNameExpansion.Create(
                    shape,
                    StaticFieldNameExpansionKind.NamespaceAlias,
                    namespaceName,
                    segments[prefixLength - 1].DecodedIdentifier,
                    fieldName,
                    alias: import.Alias,
                    contextFactSha256: import.Sha256));
            }
        }

        return builder.ToImmutable();
    }

    private static StaticFieldSymbolBindingOutcome CreateContextStop(
        IStaticFieldMetadataBindingSource source,
        StaticFieldExpressionDescriptor descriptor,
        DumpConsultedBindingContextIdentity context,
        DumpContextEvidenceStatus status)
    {
        var expansions = ImmutableArray<StaticFieldNameExpansion>.Empty;
        var modules = ImmutableArray<StaticFieldModuleSearchFact>.Empty;
        var candidates = ImmutableArray<StaticFieldSymbolCandidate>.Empty;
        var bounds = ImmutableArray<EvaluationDeterministicBound>.Empty;
        return status switch
        {
            DumpContextEvidenceStatus.Partial => StaticFieldSymbolBindingOutcome.Partial(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ContextPartial,
                "W7_CONTEXT_PARTIAL", "Required selected-frame or import context was only partially observed.",
                expansions, modules, candidates, false, false, bounds),
            DumpContextEvidenceStatus.Unavailable => StaticFieldSymbolBindingOutcome.Unavailable(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ContextUnavailable,
                "W7_CONTEXT_UNAVAILABLE", "Required selected-frame or import context was unavailable.",
                expansions, modules, candidates, false, false, bounds),
            DumpContextEvidenceStatus.Ambiguous => StaticFieldSymbolBindingOutcome.ContextAmbiguous(
                descriptor, source.Snapshot.Sha256, context,
                "W7_CONTEXT_AMBIGUOUS", "Required selected-frame or import context retained multiple candidates.",
                expansions, modules, candidates, false, false, bounds),
            DumpContextEvidenceStatus.Conflict => StaticFieldSymbolBindingOutcome.Conflict(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ContextConflict,
                "W7_CONTEXT_CONFLICT", "Required selected-frame or import context facts disagreed.",
                expansions, modules, candidates, ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                false, false, bounds),
            DumpContextEvidenceStatus.Invalid => StaticFieldSymbolBindingOutcome.Invalid(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ContextInvalid,
                "W7_CONTEXT_INVALID", "Required selected-frame or import context was structurally invalid.",
                expansions, modules, candidates, ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                false, false, bounds),
            DumpContextEvidenceStatus.Unsupported => StaticFieldSymbolBindingOutcome.Unsupported(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ContextUnsupported,
                "W7_CONTEXT_UNSUPPORTED", "Required selected-frame or import context used an unsupported representation.",
                expansions, modules, ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                false, false, bounds),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    private static NonExactDisposition? FirstNonExactDisposition(
        ImmutableArray<StaticFieldModuleSearchFact> facts)
    {
        if (facts.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Invalid))
        {
            return NonExactDisposition.Invalid;
        }
        if (facts.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Conflict))
        {
            return NonExactDisposition.Conflict;
        }
        if (facts.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Partial &&
                                     fact.Issue == StaticFieldModuleSearchIssue.SearchBoundReached))
        {
            return NonExactDisposition.Bound;
        }
        if (facts.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Partial))
        {
            return NonExactDisposition.Partial;
        }
        if (facts.Any(static fact => fact.Status == StaticFieldModuleSearchStatus.Unavailable))
        {
            return NonExactDisposition.Unavailable;
        }
        return null;
    }

    private static StaticFieldSymbolBindingOutcome CreateNonExactOutcome(
        StaticFieldExpressionDescriptor descriptor,
        IStaticFieldMetadataBindingSource source,
        DumpConsultedBindingContextIdentity context,
        ImmutableArray<StaticFieldNameExpansion> expansions,
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        ImmutableArray<StaticFieldSymbolCandidate> candidates,
        ImmutableArray<EvaluationDeterministicBound> bounds,
        NonExactDisposition disposition) =>
        disposition switch
        {
            NonExactDisposition.Invalid => StaticFieldSymbolBindingOutcome.Invalid(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.MetadataInvalid,
                "W7_FQ_METADATA_INVALID", "At least one managed module exposed malformed metadata.",
                expansions, facts, candidates, ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                true, true, bounds),
            NonExactDisposition.Conflict => StaticFieldSymbolBindingOutcome.Conflict(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ModuleConflict,
                "W7_FQ_MODULE_CONFLICT", "At least one physical module retained conflicting complete metadata identities.",
                expansions, facts, candidates, ImmutableArray<StaticFieldRejectedDeclarationEvidence>.Empty,
                true, true, bounds),
            NonExactDisposition.Bound => StaticFieldSymbolBindingOutcome.Partial(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.SearchBoundReached,
                "W7_FQ_METADATA_BOUND", "A per-module metadata-table search reached its declared bound.",
                expansions, facts, candidates, true, true, bounds),
            NonExactDisposition.Partial => StaticFieldSymbolBindingOutcome.Partial(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ModuleSearchPartial,
                "W7_FQ_METADATA_PARTIAL", "At least one module metadata image was only partially observed.",
                expansions, facts, candidates, true, true, bounds),
            NonExactDisposition.Unavailable => StaticFieldSymbolBindingOutcome.Unavailable(
                descriptor, source.Snapshot.Sha256, context, StaticFieldBindingIssue.ModuleUnavailable,
                "W7_FQ_METADATA_UNAVAILABLE", "At least one managed module's complete metadata image was unavailable.",
                expansions, facts, candidates, true, true, bounds),
            _ => throw new ArgumentOutOfRangeException(nameof(disposition)),
        };

    private static ImmutableArray<StaticFieldModuleSearchFact> ReplaceWithInvalidFact(
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        StaticFieldModuleInstanceIdentity? module)
    {
        if (module is null)
        {
            return facts;
        }

        return facts.Select(fact => fact.Module.Equals(module)
                ? StaticFieldModuleSearchFact.Invalid(fact.Module, observedModuleContent: fact.ModuleContent)
                : fact)
            .ToImmutableArray();
    }

    private static ImmutableArray<StaticFieldModuleSearchFact> ReplaceWithPartialBoundFact(
        ImmutableArray<StaticFieldModuleSearchFact> facts,
        StaticFieldModuleInstanceIdentity module) =>
        facts.Select(fact => fact.Module.Equals(module)
                ? StaticFieldModuleSearchFact.Partial(
                    fact.Module,
                    StaticFieldModuleSearchIssue.SearchBoundReached,
                    fact.TypeDefinitionsExamined,
                    fact.FieldDefinitionsExamined)
                : fact)
            .ToImmutableArray();

    private enum NonExactDisposition
    {
        Partial,
        Unavailable,
        Conflict,
        Invalid,
        Bound,
    }

    private enum FatalProjectionDisposition
    {
        Bound,
        Invalid,
        Unsupported,
    }

    private sealed class MetadataUniverse : IDisposable
    {
        private readonly IStaticFieldMetadataBindingSource source;
        private readonly ImmutableArray<ModuleModel> exactModules;

        private MetadataUniverse(
            IStaticFieldMetadataBindingSource source,
            ImmutableArray<ModuleModel> exactModules,
            ImmutableArray<StaticFieldModuleSearchFact> moduleFacts)
        {
            this.source = source;
            this.exactModules = exactModules;
            ModuleFacts = moduleFacts;
        }

        internal ImmutableArray<StaticFieldModuleSearchFact> ModuleFacts { get; }

        internal IEnumerable<ModuleModel> ExactModules => exactModules;

        internal ImmutableArray<ModuleModel> AllExactModules => exactModules;

        internal static MetadataUniverse Acquire(IStaticFieldMetadataBindingSource source)
        {
            var models = ImmutableArray.CreateBuilder<ModuleModel>();
            var facts = ImmutableArray.CreateBuilder<StaticFieldModuleSearchFact>(source.Modules.Length);
            foreach (var module in source.Modules)
            {
                var observation = source.ReadMetadata(module);
                if (observation.Status != StaticFieldMetadataImageStatus.Exact)
                {
                    facts.Add(observation.Status switch
                    {
                        StaticFieldMetadataImageStatus.Partial => StaticFieldModuleSearchFact.Partial(
                            module.Module, StaticFieldModuleSearchIssue.MetadataPartial, 0, 0),
                        StaticFieldMetadataImageStatus.Unavailable => StaticFieldModuleSearchFact.Unavailable(module.Module),
                        StaticFieldMetadataImageStatus.Conflict => StaticFieldModuleSearchFact.Conflict(
                            module.Module, observation.Contents),
                        StaticFieldMetadataImageStatus.Invalid => StaticFieldModuleSearchFact.Invalid(
                            module.Module,
                            observedModuleContent: observation.Contents.IsEmpty ? null : observation.Contents[0]),
                        _ => throw new ArgumentOutOfRangeException(),
                    });
                    continue;
                }

                try
                {
                    var model = ModuleModel.Create(module, observation.Bytes, observation.ExactContent!);
                    if (model.TypeDefinitionCount > MaximumTypeDefinitionCountPerModule ||
                        model.FieldDefinitionCount > MaximumFieldDefinitionCountPerModule)
                    {
                        facts.Add(StaticFieldModuleSearchFact.Partial(
                            module.Module,
                            StaticFieldModuleSearchIssue.SearchBoundReached,
                            0,
                            0));
                        model.Dispose();
                        continue;
                    }

                    models.Add(model);
                    facts.Add(model.CreateSearchFact());
                }
                catch (Exception exception) when (exception is BadImageFormatException or ArgumentException or OverflowException)
                {
                    facts.Add(StaticFieldModuleSearchFact.Invalid(
                        module.Module,
                        observedModuleContent: observation.ExactContent));
                }
            }

            var universe = new MetadataUniverse(source, models.ToImmutable(), facts.ToImmutable());
            foreach (var model in universe.exactModules)
            {
                model.Universe = universe;
            }
            universe.AttachNetmodules();
            return universe;
        }

        private void AttachNetmodules()
        {
            foreach (var model in exactModules.Where(static candidate => candidate.MetadataModule is null))
            {
                if (model.Input.AssemblyAddress == 0)
                {
                    continue;
                }

                var manifests = exactModules.Where(candidate =>
                        candidate.MetadataModule?.IsManifestModule == true &&
                        candidate.Input.AssemblyAddress == model.Input.AssemblyAddress &&
                        candidate.Input.Module.ApplicationDomainAddress == model.Input.Module.ApplicationDomainAddress)
                    .ToArray();
                if (manifests.Length != 1)
                {
                    continue;
                }

                model.MetadataModule = StaticFieldMetadataModuleIdentity.ForNetmodule(
                    model.Input.Module,
                    model.Content,
                    model.ModuleDefinition,
                    manifests[0].MetadataModule!.ContainingAssembly,
                    model.Input.RuntimeOrdinal);
            }
        }

        internal bool TryCreateCoreLibrary(
            out StaticFieldCoreLibraryIdentity coreLibrary,
            out StaticFieldModuleInstanceIdentity? failureModule)
        {
            coreLibrary = null!;
            failureModule = source.CoreLibraryModule?.Module;
            if (source.CoreLibraryModule is null)
            {
                return false;
            }

            var model = exactModules.SingleOrDefault(candidate =>
                candidate.Input.Module.Equals(source.CoreLibraryModule.Module));
            if (model?.MetadataModule is null)
            {
                return false;
            }

            try
            {
                var objectType = model.GetUniqueTopLevelType("System", "Object");
                var valueType = model.GetUniqueTopLevelType("System", "ValueType");
                var enumType = model.GetUniqueTopLevelType("System", "Enum");
                if (objectType is null || valueType is null || enumType is null)
                {
                    return false;
                }

                var objectIdentity = model.GetTypeIdentity(objectType.Value);
                var valueIdentity = model.GetTypeIdentity(valueType.Value);
                var enumIdentity = model.GetTypeIdentity(enumType.Value);
                var valueEdge = CreateEdge(model, valueIdentity);
                var enumEdge = CreateEdge(model, enumIdentity);
                var selection = StaticFieldCoreLibrarySelectionIdentity.Create(
                    StaticFieldCoreLibrarySelectionProvenance.ClrMdRuntimeBaseClassLibrary,
                    source.CoreLibraryModule.RuntimeOrdinal,
                    model.MetadataModule);
                coreLibrary = StaticFieldCoreLibraryIdentity.Create(
                    selection,
                    model.MetadataModule,
                    objectIdentity,
                    valueIdentity,
                    enumIdentity,
                    valueEdge,
                    enumEdge);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return false;
            }
        }

        internal DeclarationProjection ProjectDeclaration(
            ModuleModel module,
            TypeDefinitionHandle typeHandle,
            FieldDefinitionHandle fieldHandle,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            StaticFieldDefinitionIdentity? fieldIdentity = null;
            StaticFieldDeclaredValueKind? projectedKind = null;
            try
            {
                var typeIdentity = module.GetTypeIdentity(typeHandle);
                var field = module.Reader.GetFieldDefinition(fieldHandle);
                var signature = ImmutableArray.CreateRange(module.Reader.GetBlobBytes(field.Signature));
                BoundedEcmaFieldSignature? signatureProjection = null;
                if (BoundedEcmaFieldSignatureProjection.TryDecode(signature.AsSpan(), out var decoded))
                {
                    signatureProjection = decoded;
                    projectedKind = decoded.Kind switch
                    {
                        BoundedEcmaFieldSignatureKind.Int32 => StaticFieldDeclaredValueKind.Int32,
                        BoundedEcmaFieldSignatureKind.PrimitiveString => StaticFieldDeclaredValueKind.String,
                        BoundedEcmaFieldSignatureKind.PrimitiveObject => StaticFieldDeclaredValueKind.Object,
                        BoundedEcmaFieldSignatureKind.ClassType => StaticFieldDeclaredValueKind.ManagedReference,
                        BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32 => StaticFieldDeclaredValueKind.NullableInt32,
                        _ => null,
                    };
                }

                var fieldToken = MetadataTokens.GetToken(fieldHandle);
                var attributeProjection = CreateCustomAttributeProjection(
                    module,
                    fieldHandle,
                    fieldToken,
                    coreLibrary);
                fieldIdentity = StaticFieldDefinitionIdentity.Create(
                    typeIdentity,
                    fieldToken,
                    module.Reader.GetString(field.Name),
                    (int)field.Attributes,
                    signature,
                    attributeProjection);

                var fieldFlags = field.Attributes;
                if ((fieldFlags & FieldAttributes.FieldAccessMask) == FieldAttributes.FieldAccessMask ||
                    (fieldFlags & FieldAttributes.Static) == 0 ||
                    (fieldFlags & FieldAttributes.Literal) != 0 ||
                    (fieldFlags & FieldAttributes.HasFieldRVA) != 0 ||
                    fieldIdentity.IsThreadStatic ||
                    fieldIdentity.IsContextStatic)
                {
                    return DeclarationProjection.Rejected(
                        fieldIdentity,
                        projectedKind,
                        "W7_FQ_STORAGE_SHAPE_UNSUPPORTED",
                        StaticFieldBindingIssue.DeclarationShapeUnsupported);
                }

                if (signatureProjection is null || projectedKind is null)
                {
                    return DeclarationProjection.Rejected(
                        fieldIdentity,
                        null,
                        "W7_FQ_FIELD_SIGNATURE_UNSUPPORTED",
                        StaticFieldBindingIssue.DeclarationShapeUnsupported);
                }

                var ancestry = GetAncestry(typeIdentity, coreLibrary);
                if (ancestry.Classification != StaticFieldTypeClassification.ReferenceClass ||
                    typeIdentity.GenericArity != 0)
                {
                    return DeclarationProjection.Rejected(
                        fieldIdentity,
                        projectedKind,
                        "W7_FQ_OWNER_SHAPE_UNSUPPORTED",
                        StaticFieldBindingIssue.DeclarationShapeUnsupported);
                }
                var declaration = signatureProjection.Value.Kind switch
                {
                    BoundedEcmaFieldSignatureKind.Int32 =>
                        StaticFieldSymbolDeclarationIdentity.Create(
                            ancestry,
                            fieldIdentity,
                            StaticFieldDeclaredValueKind.Int32,
                            systemInt32TypeAncestry: GetCoreAncestry(coreLibrary, "Int32")),
                    BoundedEcmaFieldSignatureKind.PrimitiveString =>
                        CreatePrimitiveReferenceDeclaration(
                            module,
                            ancestry,
                            fieldIdentity,
                            coreLibrary,
                            isString: true),
                    BoundedEcmaFieldSignatureKind.PrimitiveObject =>
                        CreatePrimitiveReferenceDeclaration(
                            module,
                            ancestry,
                            fieldIdentity,
                            coreLibrary,
                            isString: false),
                    BoundedEcmaFieldSignatureKind.ClassType =>
                        CreateManagedReferenceDeclaration(
                            module,
                            ancestry,
                            fieldIdentity,
                            signatureProjection.Value.NamedTypeMetadataToken!.Value,
                            coreLibrary),
                    BoundedEcmaFieldSignatureKind.GenericInstanceValueTypeInt32 =>
                        CreateNullableDeclaration(
                            module,
                            ancestry,
                            fieldIdentity,
                            signatureProjection.Value.NamedTypeMetadataToken!.Value,
                            coreLibrary),
                    _ => null,
                };

                return declaration is null
                    ? DeclarationProjection.Rejected(
                        fieldIdentity,
                        projectedKind,
                        "W7_FQ_DECLARATION_SHAPE_UNSUPPORTED",
                        StaticFieldBindingIssue.DeclarationShapeUnsupported)
                    : DeclarationProjection.Admitted(declaration, fieldIdentity, projectedKind.Value);
            }
            catch (UnsupportedMetadataShapeException exception)
            {
                return fieldIdentity is null
                    ? DeclarationProjection.Unprojectable(exception.Code, StaticFieldBindingIssue.DeclarationShapeUnsupported)
                    : DeclarationProjection.Rejected(
                        fieldIdentity,
                        projectedKind,
                        exception.Code,
                        StaticFieldBindingIssue.DeclarationShapeUnsupported);
            }
            catch (Exception exception) when (exception is ArgumentException or BadImageFormatException or InvalidOperationException)
            {
                return fieldIdentity is null
                    ? DeclarationProjection.Unprojectable("W7_FQ_DECLARATION_INVALID", StaticFieldBindingIssue.MetadataInvalid)
                    : DeclarationProjection.Rejected(
                        fieldIdentity,
                        projectedKind,
                        "W7_FQ_DECLARATION_INVALID",
                        StaticFieldBindingIssue.MetadataInvalid);
            }
        }

        private StaticFieldFieldCustomAttributeProjection CreateCustomAttributeProjection(
            ModuleModel module,
            FieldDefinitionHandle fieldHandle,
            int fieldToken,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            var handles = module.Reader.GetFieldDefinition(fieldHandle).GetCustomAttributes();
            if (handles.Count > StaticFieldFieldCustomAttributeProjection.MaximumOwnedCustomAttributeRowCount)
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_FIELD_CUSTOM_ATTRIBUTE_BOUND");
            }

            var rows = ImmutableArray.CreateBuilder<StaticFieldCustomAttributeRowIdentity>(handles.Count);
            foreach (var handle in handles)
            {
                var attribute = module.Reader.GetCustomAttribute(handle);
                var value = ImmutableArray.CreateRange(module.Reader.GetBlobBytes(attribute.Value));
                if (value.Length > StaticFieldCustomAttributeRowIdentity.MaximumAttributeValueLength)
                {
                    throw new UnsupportedMetadataShapeException("W7_FQ_CUSTOM_ATTRIBUTE_VALUE_BOUND");
                }

                rows.Add(attribute.Constructor.Kind switch
                {
                    HandleKind.MethodDefinition => CreateMethodDefinitionAttribute(
                        module,
                        fieldToken,
                        handle,
                        (MethodDefinitionHandle)attribute.Constructor,
                        value,
                        coreLibrary),
                    HandleKind.MemberReference => CreateMemberReferenceAttribute(
                        module,
                        fieldToken,
                        handle,
                        (MemberReferenceHandle)attribute.Constructor,
                        value,
                        coreLibrary),
                    _ => throw new UnsupportedMetadataShapeException("W7_FQ_CUSTOM_ATTRIBUTE_CONSTRUCTOR_SHAPE"),
                });
            }

            return StaticFieldFieldCustomAttributeProjection.Create(
                fieldToken,
                rows.Count,
                rows.Count,
                rows.ToImmutable());
        }

        private StaticFieldCustomAttributeRowIdentity CreateMethodDefinitionAttribute(
            ModuleModel module,
            int fieldToken,
            CustomAttributeHandle attributeHandle,
            MethodDefinitionHandle constructorHandle,
            ImmutableArray<byte> value,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            var constructor = module.GetMethodIdentity(constructorHandle);
            var declaringType = constructor.DeclaringType;
            if (declaringType.AssemblyDefinition.HasRuntimeCoreLibraryName &&
                string.Equals(declaringType.NamespaceName, "System", StringComparison.Ordinal) &&
                declaringType.TypeName is "ThreadStaticAttribute" or "ContextStaticAttribute")
            {
                return StaticFieldCustomAttributeRowIdentity.Create(
                    module.MetadataModule!,
                    fieldToken,
                    MetadataTokens.GetToken(attributeHandle),
                    constructor,
                    sourceMemberReference: null,
                    value,
                    GetAncestry(declaringType, coreLibrary));
            }

            return StaticFieldCustomAttributeRowIdentity.CreateProvenNonMarkerFromMethodDefinition(
                module.MetadataModule!,
                fieldToken,
                MetadataTokens.GetToken(attributeHandle),
                constructor,
                value);
        }

        private StaticFieldCustomAttributeRowIdentity CreateMemberReferenceAttribute(
            ModuleModel module,
            int fieldToken,
            CustomAttributeHandle attributeHandle,
            MemberReferenceHandle memberHandle,
            ImmutableArray<byte> value,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            var member = module.Reader.GetMemberReference(memberHandle);
            var memberName = module.Reader.GetString(member.Name);
            if (!string.Equals(memberName, ".ctor", StringComparison.Ordinal))
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_CUSTOM_ATTRIBUTE_NOT_CONSTRUCTOR");
            }

            var signature = ImmutableArray.CreateRange(module.Reader.GetBlobBytes(member.Signature));
            if (member.Parent.Kind == HandleKind.TypeReference)
            {
                var parentHandle = (TypeReferenceHandle)member.Parent;
                var parent = module.Reader.GetTypeReference(parentHandle);
                var parentNamespace = module.Reader.GetString(parent.Namespace);
                var parentName = module.Reader.GetString(parent.Name);
                if (string.Equals(parentNamespace, "System", StringComparison.Ordinal) &&
                    parentName is "ThreadStaticAttribute" or "ContextStaticAttribute")
                {
                    var resolution = module.ResolveTypeReference(parentHandle);
                    var targetModel = exactModules.Single(candidate =>
                        candidate.MetadataModule?.Equals(resolution.ResolvedTargetType.MetadataModule) == true);
                    var constructors = targetModel.Reader
                        .GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(
                            resolution.ResolvedTargetType.TypeDefinitionToken & 0x00FF_FFFF))
                        .GetMethods()
                        .Where(candidate =>
                        {
                            var method = targetModel.Reader.GetMethodDefinition(candidate);
                            return string.Equals(targetModel.Reader.GetString(method.Name), memberName, StringComparison.Ordinal) &&
                                   targetModel.Reader.GetBlobBytes(method.Signature).AsSpan().SequenceEqual(signature.AsSpan());
                        })
                        .ToArray();
                    if (constructors.Length != 1)
                    {
                        throw new UnsupportedMetadataShapeException("W7_FQ_STORAGE_MARKER_CONSTRUCTOR_AMBIGUOUS");
                    }

                    var constructor = targetModel.GetMethodIdentity(constructors[0]);
                    var sourceReference = StaticFieldMemberReferenceIdentity.Create(
                        module.MetadataModule!,
                        MetadataTokens.GetToken(memberHandle),
                        MetadataTokens.GetToken(parentHandle),
                        memberName,
                        signature,
                        resolution,
                        constructor);
                    return StaticFieldCustomAttributeRowIdentity.Create(
                        module.MetadataModule!,
                        fieldToken,
                        MetadataTokens.GetToken(attributeHandle),
                        constructor,
                        sourceReference,
                        value,
                        GetAncestry(resolution.ResolvedTargetType, coreLibrary));
                }
            }

            var unresolved = module.CreateUnresolvedMemberReference(memberHandle);
            return StaticFieldCustomAttributeRowIdentity.CreateProvenNonMarkerFromMemberReference(
                module.MetadataModule!,
                fieldToken,
                MetadataTokens.GetToken(attributeHandle),
                unresolved,
                value);
        }

        private StaticFieldSymbolDeclarationIdentity CreatePrimitiveReferenceDeclaration(
            ModuleModel module,
            StaticFieldTypeAncestryIdentity ownerAncestry,
            StaticFieldDefinitionIdentity field,
            StaticFieldCoreLibraryIdentity coreLibrary,
            bool isString)
        {
            var targetAncestry = isString
                ? GetCoreAncestry(coreLibrary, "String")
                : StaticFieldTypeAncestryIdentity.Create(
                    coreLibrary.SystemObjectType,
                    ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                    coreLibrary);
            var target = isString
                ? StaticFieldDeclaredReferenceIdentity.PrimitiveSystemString(module.MetadataModule!, targetAncestry)
                : StaticFieldDeclaredReferenceIdentity.PrimitiveSystemObject(module.MetadataModule!, targetAncestry);
            return StaticFieldSymbolDeclarationIdentity.Create(
                ownerAncestry,
                field,
                isString ? StaticFieldDeclaredValueKind.String : StaticFieldDeclaredValueKind.Object,
                target);
        }

        private StaticFieldSymbolDeclarationIdentity CreateManagedReferenceDeclaration(
            ModuleModel module,
            StaticFieldTypeAncestryIdentity ownerAncestry,
            StaticFieldDefinitionIdentity field,
            int typeToken,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            var handle = MetadataTokens.EntityHandle(typeToken);
            StaticFieldDeclaredReferenceIdentity target;
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                var targetType = module.GetTypeIdentity((TypeDefinitionHandle)handle);
                target = StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeDefinition(
                    module.MetadataModule!,
                    GetAncestry(targetType, coreLibrary));
            }
            else if (handle.Kind == HandleKind.TypeReference)
            {
                var resolution = module.ResolveTypeReference((TypeReferenceHandle)handle);
                target = StaticFieldDeclaredReferenceIdentity.ManagedReferenceTypeReference(
                    module.MetadataModule!,
                    resolution,
                    GetAncestry(resolution.ResolvedTargetType, coreLibrary));
            }
            else
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_CLASS_TYPESPEC_UNSUPPORTED");
            }

            return StaticFieldSymbolDeclarationIdentity.Create(
                ownerAncestry,
                field,
                StaticFieldDeclaredValueKind.ManagedReference,
                target);
        }

        private StaticFieldSymbolDeclarationIdentity CreateNullableDeclaration(
            ModuleModel module,
            StaticFieldTypeAncestryIdentity ownerAncestry,
            StaticFieldDefinitionIdentity field,
            int typeToken,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            var handle = MetadataTokens.EntityHandle(typeToken);
            StaticFieldTypeReferenceResolutionIdentity? resolution = null;
            StaticFieldTypeDefinitionIdentity targetType;
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                targetType = module.GetTypeIdentity((TypeDefinitionHandle)handle);
            }
            else if (handle.Kind == HandleKind.TypeReference)
            {
                resolution = module.ResolveTypeReference((TypeReferenceHandle)handle);
                targetType = resolution.ResolvedTargetType;
            }
            else
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_NULLABLE_TYPESPEC_UNSUPPORTED");
            }

            var nullable = StaticFieldNullableTypeIdentity.Create(
                module.MetadataModule!,
                typeToken,
                resolution,
                GetAncestry(targetType, coreLibrary));
            return StaticFieldSymbolDeclarationIdentity.Create(
                ownerAncestry,
                field,
                StaticFieldDeclaredValueKind.NullableInt32,
                nullableType: nullable,
                systemInt32TypeAncestry: GetCoreAncestry(coreLibrary, "Int32"));
        }

        private StaticFieldTypeAncestryIdentity GetCoreAncestry(
            StaticFieldCoreLibraryIdentity coreLibrary,
            string typeName)
        {
            var coreModel = exactModules.Single(module =>
                module.MetadataModule?.Equals(coreLibrary.MetadataModule) == true);
            var handle = coreModel.GetUniqueTopLevelType("System", typeName) ??
                throw new UnsupportedMetadataShapeException($"W7_FQ_CORE_{typeName.ToUpperInvariant()}_ABSENT");
            return GetAncestry(coreModel.GetTypeIdentity(handle), coreLibrary);
        }

        private StaticFieldTypeAncestryIdentity GetAncestry(
            StaticFieldTypeDefinitionIdentity subject,
            StaticFieldCoreLibraryIdentity coreLibrary)
        {
            if (subject.IsInterface)
            {
                return StaticFieldTypeAncestryIdentity.Create(
                    subject,
                    ImmutableArray<StaticFieldTypeAncestryEdge>.Empty,
                    coreLibrary: null);
            }

            var edges = ImmutableArray.CreateBuilder<StaticFieldTypeAncestryEdge>();
            var current = subject;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (!IsCoreTerminal(current, coreLibrary))
            {
                if (edges.Count == StaticFieldTypeAncestryIdentity.MaximumAncestryEdgeCount ||
                    !seen.Add($"{current.Module.Sha256}:{current.TypeDefinitionToken:x8}"))
                {
                    throw new UnsupportedMetadataShapeException("W7_FQ_ANCESTRY_BOUND_OR_CYCLE");
                }

                var model = exactModules.Single(candidate =>
                    candidate.MetadataModule?.Equals(current.MetadataModule) == true);
                var edge = CreateEdge(model, current);
                edges.Add(edge);
                current = edge.ResolvedBaseType;
            }

            return StaticFieldTypeAncestryIdentity.Create(subject, edges.ToImmutable(), coreLibrary);
        }

        private StaticFieldTypeAncestryEdge CreateEdge(
            ModuleModel model,
            StaticFieldTypeDefinitionIdentity sourceType)
        {
            if (!sourceType.ExtendsMetadataToken.HasValue)
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_NIL_ANCESTRY");
            }

            var handle = MetadataTokens.EntityHandle(sourceType.ExtendsMetadataToken.Value);
            return handle.Kind switch
            {
                HandleKind.TypeDefinition => StaticFieldTypeAncestryEdge.Create(
                    sourceType,
                    model.GetTypeIdentity((TypeDefinitionHandle)handle)),
                HandleKind.TypeReference => CreateTypeReferenceEdge(model, sourceType, (TypeReferenceHandle)handle),
                _ => throw new UnsupportedMetadataShapeException("W7_FQ_ANCESTRY_TYPESPEC_UNSUPPORTED"),
            };
        }

        private static StaticFieldTypeAncestryEdge CreateTypeReferenceEdge(
            ModuleModel model,
            StaticFieldTypeDefinitionIdentity sourceType,
            TypeReferenceHandle handle)
        {
            var resolution = model.ResolveTypeReference(handle);
            return StaticFieldTypeAncestryEdge.Create(
                sourceType,
                resolution.ResolvedTargetType,
                resolution);
        }

        private static bool IsCoreTerminal(
            StaticFieldTypeDefinitionIdentity type,
            StaticFieldCoreLibraryIdentity coreLibrary) =>
            type.Equals(coreLibrary.SystemObjectType) ||
            type.Equals(coreLibrary.SystemValueType) ||
            type.Equals(coreLibrary.SystemEnumType);

        internal ImmutableArray<ModuleModel> FindAssemblies(StaticFieldAssemblyReferenceIdentity reference) =>
            exactModules
                .Where(module => module.MetadataModule is not null &&
                    StaticFieldExportedTypeForwarderIdentity.AssemblyReferenceMatchesDefinition(
                        reference,
                        module.MetadataModule.ContainingAssembly.AssemblyDefinition))
                .ToImmutableArray();

        public void Dispose()
        {
            foreach (var module in exactModules)
            {
                module.Dispose();
            }
        }
    }

    private sealed class ModuleModel : IDisposable
    {
        private readonly MetadataReaderProvider provider;
        private readonly Dictionary<int, StaticFieldTypeDefinitionIdentity> types = new();
        private readonly int[] fieldStarts;
        private readonly int[] fieldEnds;
        private readonly int[] methodStarts;
        private readonly int[] methodEnds;
        private readonly int[] parameterStarts;
        private readonly int[] parameterEnds;

        private ModuleModel(
            StaticFieldMetadataModuleInput input,
            MetadataReaderProvider provider,
            MetadataReader reader,
            ModuleContentIdentity content,
            StaticFieldModuleDefinitionIdentity moduleDefinition,
            StaticFieldMetadataModuleIdentity? metadataModule,
            int[] fieldStarts,
            int[] fieldEnds,
            int[] methodStarts,
            int[] methodEnds,
            int[] parameterStarts,
            int[] parameterEnds)
        {
            Input = input;
            this.provider = provider;
            Reader = reader;
            Content = content;
            ModuleDefinition = moduleDefinition;
            MetadataModule = metadataModule;
            this.fieldStarts = fieldStarts;
            this.fieldEnds = fieldEnds;
            this.methodStarts = methodStarts;
            this.methodEnds = methodEnds;
            this.parameterStarts = parameterStarts;
            this.parameterEnds = parameterEnds;
        }

        internal MetadataUniverse Universe { get; set; } = null!;

        internal StaticFieldMetadataModuleInput Input { get; }

        internal MetadataReader Reader { get; }

        internal ModuleContentIdentity Content { get; }

        internal StaticFieldModuleDefinitionIdentity ModuleDefinition { get; }

        internal StaticFieldMetadataModuleIdentity? MetadataModule { get; set; }

        internal int TypeDefinitionCount => Reader.GetTableRowCount(TableIndex.TypeDef);

        internal int FieldDefinitionCount => Reader.GetTableRowCount(TableIndex.Field);

        internal static ModuleModel Create(
            StaticFieldMetadataModuleInput input,
            ImmutableArray<byte> bytes,
            ModuleContentIdentity content)
        {
            var provider = MetadataReaderProvider.FromMetadataImage(bytes);
            try
            {
                var reader = provider.GetMetadataReader();
                if (reader.GetTableRowCount(TableIndex.Module) != 1)
                {
                    throw new BadImageFormatException("A metadata image must contain exactly one Module row.");
                }

                var moduleDefinition = CreateModuleDefinition(reader);
                StaticFieldMetadataModuleIdentity? metadataModule = null;
                if (reader.IsAssembly)
                {
                    var assemblyDefinition = CreateAssemblyDefinition(reader);
                    var containingAssembly = StaticFieldContainingAssemblyIdentity.Create(
                        input.Module,
                        content,
                        moduleDefinition,
                        assemblyDefinition);
                    metadataModule = StaticFieldMetadataModuleIdentity.ForManifestModule(
                        input.Module,
                        content,
                        moduleDefinition,
                        containingAssembly);
                }

                CreateOwnerRanges(
                    reader,
                    out var fieldStarts,
                    out var fieldEnds,
                    out var methodStarts,
                    out var methodEnds,
                    out var parameterStarts,
                    out var parameterEnds);
                return new ModuleModel(
                    input,
                    provider,
                    reader,
                    content,
                    moduleDefinition,
                    metadataModule,
                    fieldStarts,
                    fieldEnds,
                    methodStarts,
                    methodEnds,
                    parameterStarts,
                    parameterEnds);
            }
            catch
            {
                provider.Dispose();
                throw;
            }
        }

        internal StaticFieldModuleSearchFact CreateSearchFact() =>
            StaticFieldModuleSearchFact.Exact(
                Input.Module,
                Content,
                TypeDefinitionCount,
                FieldDefinitionCount,
                typeReferenceRowCount: Reader.GetTableRowCount(TableIndex.TypeRef),
                typeSpecificationRowCount: Reader.GetTableRowCount(TableIndex.TypeSpec),
                assemblyReferenceRowCount: Reader.GetTableRowCount(TableIndex.AssemblyRef),
                methodDefinitionRowCount: Reader.GetTableRowCount(TableIndex.MethodDef),
                parameterDefinitionRowCount: Reader.GetTableRowCount(TableIndex.Param),
                propertyDefinitionRowCount: Reader.GetTableRowCount(TableIndex.Property),
                eventDefinitionRowCount: Reader.GetTableRowCount(TableIndex.Event),
                moduleDefinitionRowCount: Reader.GetTableRowCount(TableIndex.Module),
                assemblyDefinitionRowCount: Reader.GetTableRowCount(TableIndex.Assembly),
                interfaceImplementationRowCount: Reader.GetTableRowCount(TableIndex.InterfaceImpl),
                memberReferenceRowCount: Reader.GetTableRowCount(TableIndex.MemberRef),
                customAttributeRowCount: Reader.GetTableRowCount(TableIndex.CustomAttribute),
                moduleReferenceRowCount: Reader.GetTableRowCount(TableIndex.ModuleRef),
                fileRowCount: Reader.GetTableRowCount(TableIndex.File),
                exportedTypeRowCount: Reader.GetTableRowCount(TableIndex.ExportedType),
                nestedClassRowCount: Reader.GetTableRowCount(TableIndex.NestedClass),
                genericParameterRowCount: Reader.GetTableRowCount(TableIndex.GenericParam),
                genericParameterConstraintRowCount: Reader.GetTableRowCount(TableIndex.GenericParamConstraint));

        internal IEnumerable<(TypeDefinitionHandle TypeHandle, FieldDefinitionHandle FieldHandle)> FindFields(
            string namespaceName,
            string typeName,
            string fieldName)
        {
            foreach (var typeHandle in Reader.TypeDefinitions)
            {
                var type = Reader.GetTypeDefinition(typeHandle);
                if (!type.GetDeclaringType().IsNil ||
                    !string.Equals(Reader.GetString(type.Namespace), namespaceName, StringComparison.Ordinal) ||
                    !string.Equals(Reader.GetString(type.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var fieldHandle in type.GetFields())
                {
                    var field = Reader.GetFieldDefinition(fieldHandle);
                    if (string.Equals(Reader.GetString(field.Name), fieldName, StringComparison.Ordinal))
                    {
                        yield return (typeHandle, fieldHandle);
                    }
                }
            }
        }

        internal TypeDefinitionHandle? GetUniqueTopLevelType(string namespaceName, string typeName)
        {
            TypeDefinitionHandle? result = null;
            foreach (var handle in Reader.TypeDefinitions)
            {
                var definition = Reader.GetTypeDefinition(handle);
                if (!definition.GetDeclaringType().IsNil ||
                    !string.Equals(Reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal) ||
                    !string.Equals(Reader.GetString(definition.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (result.HasValue)
                {
                    return null;
                }
                result = handle;
            }
            return result;
        }

        internal StaticFieldTypeDefinitionIdentity GetTypeIdentity(TypeDefinitionHandle handle)
        {
            if (MetadataModule is null)
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_NETMODULE_DECLARATION_UNSUPPORTED");
            }

            var rowId = MetadataTokens.GetRowNumber(handle);
            if (types.TryGetValue(rowId, out var cached))
            {
                return cached;
            }

            var definition = Reader.GetTypeDefinition(handle);
            var enclosingHandle = definition.GetDeclaringType();
            var enclosing = enclosingHandle.IsNil ? null : GetTypeIdentity(enclosingHandle);
            var genericCount = definition.GetGenericParameters().Count;
            var introducedArity = genericCount - (enclosing?.GenericParameterCount ?? 0);
            var baseToken = definition.BaseType.IsNil ? (int?)null : MetadataTokens.GetToken(definition.BaseType);
            var identity = StaticFieldTypeDefinitionIdentity.Create(
                MetadataModule,
                MetadataTokens.GetToken(handle),
                fieldStarts[rowId],
                fieldEnds[rowId],
                methodStarts[rowId],
                methodEnds[rowId],
                Reader.GetString(definition.Namespace),
                Reader.GetString(definition.Name),
                (int)definition.Attributes,
                genericCount,
                introducedArity,
                baseToken,
                enclosing);
            types.Add(rowId, identity);
            return identity;
        }

        internal StaticFieldMethodDefinitionIdentity GetMethodIdentity(MethodDefinitionHandle handle)
        {
            var definition = Reader.GetMethodDefinition(handle);
            var declaringType = GetTypeIdentity(definition.GetDeclaringType());
            var rowId = MetadataTokens.GetRowNumber(handle);
            return StaticFieldMethodDefinitionIdentity.Create(
                declaringType,
                MetadataTokens.GetToken(handle),
                definition.RelativeVirtualAddress,
                (int)definition.ImplAttributes,
                (int)definition.Attributes,
                Reader.GetString(definition.Name),
                ImmutableArray.CreateRange(Reader.GetBlobBytes(definition.Signature)),
                parameterStarts[rowId],
                parameterEnds[rowId],
                Reader.GetTableRowCount(TableIndex.Param));
        }

        internal StaticFieldMemberReferenceRowIdentity CreateUnresolvedMemberReference(
            MemberReferenceHandle handle)
        {
            var member = Reader.GetMemberReference(handle);
            var token = MetadataTokens.GetToken(handle);
            var name = Reader.GetString(member.Name);
            var signature = ImmutableArray.CreateRange(Reader.GetBlobBytes(member.Signature));
            return member.Parent.Kind switch
            {
                HandleKind.TypeDefinition => StaticFieldMemberReferenceRowIdentity.ForTypeDefinition(
                    MetadataModule!,
                    token,
                    GetTypeIdentity((TypeDefinitionHandle)member.Parent),
                    name,
                    signature),
                HandleKind.TypeReference => StaticFieldMemberReferenceRowIdentity.ForTypeReference(
                    MetadataModule!,
                    token,
                    CreateTypeReferenceRow((TypeReferenceHandle)member.Parent),
                    name,
                    signature),
                HandleKind.TypeSpecification => StaticFieldMemberReferenceRowIdentity.ForTypeSpecification(
                    MetadataModule!,
                    token,
                    MetadataTokens.GetToken(member.Parent),
                    ImmutableArray.CreateRange(Reader.GetBlobBytes(
                        Reader.GetTypeSpecification((TypeSpecificationHandle)member.Parent).Signature)),
                    name,
                    signature),
                _ => throw new UnsupportedMetadataShapeException("W7_FQ_MEMBERREF_PARENT_UNSUPPORTED"),
            };
        }

        private StaticFieldTypeReferenceRowIdentity CreateTypeReferenceRow(TypeReferenceHandle handle)
        {
            var reference = Reader.GetTypeReference(handle);
            return StaticFieldTypeReferenceRowIdentity.Create(
                MetadataTokens.GetToken(handle),
                Reader.GetString(reference.Namespace),
                Reader.GetString(reference.Name),
                MetadataTokens.GetToken(reference.ResolutionScope));
        }

        internal StaticFieldTypeReferenceResolutionIdentity ResolveTypeReference(TypeReferenceHandle handle)
        {
            var rows = ImmutableArray.CreateBuilder<StaticFieldTypeReferenceRowIdentity>();
            var current = handle;
            EntityHandle root;
            while (true)
            {
                if (rows.Count == StaticFieldTypeReferenceResolutionIdentity.MaximumTypeReferenceChainLength)
                {
                    throw new UnsupportedMetadataShapeException("W7_FQ_TYPEREF_CHAIN_BOUND");
                }

                var reference = Reader.GetTypeReference(current);
                rows.Add(StaticFieldTypeReferenceRowIdentity.Create(
                    MetadataTokens.GetToken(current),
                    Reader.GetString(reference.Namespace),
                    Reader.GetString(reference.Name),
                    MetadataTokens.GetToken(reference.ResolutionScope)));
                if (reference.ResolutionScope.Kind != HandleKind.TypeReference)
                {
                    root = reference.ResolutionScope;
                    break;
                }
                current = (TypeReferenceHandle)reference.ResolutionScope;
            }

            var retainedRows = rows.ToImmutable();
            return root.Kind switch
            {
                HandleKind.ModuleDefinition => StaticFieldTypeReferenceResolutionIdentity.ForModule(
                    MetadataModule!,
                    retainedRows,
                    ResolveTargetType(retainedRows, this)),
                HandleKind.AssemblyReference => ResolveAssemblyTypeReference(
                    retainedRows,
                    (AssemblyReferenceHandle)root),
                HandleKind.ModuleReference => ResolveModuleTypeReference(
                    retainedRows,
                    (ModuleReferenceHandle)root),
                _ => throw new UnsupportedMetadataShapeException("W7_FQ_TYPEREF_SCOPE_UNSUPPORTED"),
            };
        }

        private StaticFieldTypeReferenceResolutionIdentity ResolveModuleTypeReference(
            ImmutableArray<StaticFieldTypeReferenceRowIdentity> rows,
            ModuleReferenceHandle moduleReferenceHandle)
        {
            var moduleReference = Reader.GetModuleReference(moduleReferenceHandle);
            var moduleName = Reader.GetString(moduleReference.Name);
            var targets = Universe.AllExactModules.Where(candidate =>
                    MetadataModule is not null &&
                    candidate.MetadataModule is not null &&
                    candidate.MetadataModule.ContainingAssembly.Equals(MetadataModule.ContainingAssembly) &&
                    string.Equals(candidate.ModuleDefinition.Name, moduleName, StringComparison.Ordinal))
                .ToArray();
            if (targets.Length != 1)
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_MODULEREF_TARGET_AMBIGUOUS");
            }

            return StaticFieldTypeReferenceResolutionIdentity.ForModuleReference(
                MetadataModule!,
                rows,
                MetadataTokens.GetToken(moduleReferenceHandle),
                moduleName,
                ResolveTargetType(rows, targets[0]));
        }

        private StaticFieldTypeReferenceResolutionIdentity ResolveAssemblyTypeReference(
            ImmutableArray<StaticFieldTypeReferenceRowIdentity> rows,
            AssemblyReferenceHandle assemblyHandle)
        {
            var assemblyReference = CreateAssemblyReference(Reader, assemblyHandle);
            var targets = Universe.FindAssemblies(assemblyReference);
            if (targets.Length != 1)
            {
                throw new UnsupportedMetadataShapeException("W7_FQ_ASSEMBLY_REFERENCE_AMBIGUOUS");
            }

            var target = targets[0];
            var direct = ResolveTargetType(rows, target, required: false);
            if (direct is not null)
            {
                return StaticFieldTypeReferenceResolutionIdentity.ForDirectAssemblyReference(
                    MetadataModule!, rows, assemblyReference, direct);
            }

            var forwarders = ImmutableArray.CreateBuilder<StaticFieldExportedTypeForwarderIdentity>();
            var outer = rows[^1];
            var current = target;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (forwarders.Count < StaticFieldTypeReferenceResolutionIdentity.MaximumExportedTypeForwarderCount &&
                   seen.Add(current.MetadataModule!.Sha256))
            {
                var matches = current.FindForwarders(outer.NamespaceName, outer.TypeName).ToArray();
                if (matches.Length != 1)
                {
                    break;
                }

                var exported = current.Reader.GetExportedType(matches[0]);
                if (exported.Implementation.Kind != HandleKind.AssemblyReference)
                {
                    break;
                }

                var nextReference = CreateAssemblyReference(
                    current.Reader,
                    (AssemblyReferenceHandle)exported.Implementation);
                var nextTargets = Universe.FindAssemblies(nextReference);
                if (nextTargets.Length != 1)
                {
                    break;
                }

                var next = nextTargets[0];
                forwarders.Add(StaticFieldExportedTypeForwarderIdentity.Create(
                    current.MetadataModule.ContainingAssembly,
                    MetadataTokens.GetToken(matches[0]),
                    (int)exported.Attributes,
                    typeDefinitionId: 0,
                    current.Reader.GetString(exported.Namespace),
                    current.Reader.GetString(exported.Name),
                    nextReference,
                    next.MetadataModule!.ContainingAssembly));
                current = next;
                var resolved = ResolveTargetType(rows, current, required: false);
                if (resolved is not null)
                {
                    return StaticFieldTypeReferenceResolutionIdentity.ForExportedTypeForwarders(
                        MetadataModule!,
                        rows,
                        assemblyReference,
                        forwarders.ToImmutable(),
                        resolved);
                }
            }

            throw new UnsupportedMetadataShapeException("W7_FQ_TYPEREF_TARGET_UNRESOLVED");
        }

        private IEnumerable<ExportedTypeHandle> FindForwarders(string namespaceName, string typeName)
        {
            const int forwarderBit = 0x0020_0000;
            foreach (var handle in Reader.ExportedTypes)
            {
                var exported = Reader.GetExportedType(handle);
                if (((int)exported.Attributes & forwarderBit) != 0 &&
                    string.Equals(Reader.GetString(exported.Namespace), namespaceName, StringComparison.Ordinal) &&
                    string.Equals(Reader.GetString(exported.Name), typeName, StringComparison.Ordinal))
                {
                    yield return handle;
                }
            }
        }

        private static StaticFieldTypeDefinitionIdentity ResolveTargetType(
            ImmutableArray<StaticFieldTypeReferenceRowIdentity> rows,
            ModuleModel target,
            bool required = true)
        {
            var outer = rows[^1];
            var handle = target.GetUniqueTopLevelType(outer.NamespaceName, outer.TypeName);
            if (!handle.HasValue)
            {
                if (required)
                {
                    throw new UnsupportedMetadataShapeException("W7_FQ_TYPEREF_TARGET_ABSENT");
                }
                return null!;
            }

            var current = handle.Value;
            for (var index = rows.Length - 2; index >= 0; index--)
            {
                var matches = target.Reader.TypeDefinitions
                    .Where(candidate =>
                    {
                        var definition = target.Reader.GetTypeDefinition(candidate);
                        return definition.GetDeclaringType() == current &&
                               string.Equals(target.Reader.GetString(definition.Namespace), rows[index].NamespaceName, StringComparison.Ordinal) &&
                               string.Equals(target.Reader.GetString(definition.Name), rows[index].TypeName, StringComparison.Ordinal);
                    })
                    .ToArray();
                if (matches.Length != 1)
                {
                    if (required)
                    {
                        throw new UnsupportedMetadataShapeException("W7_FQ_NESTED_TYPEREF_TARGET_AMBIGUOUS");
                    }
                    return null!;
                }
                current = matches[0];
            }

            return target.GetTypeIdentity(current);
        }

        private static StaticFieldModuleDefinitionIdentity CreateModuleDefinition(MetadataReader reader)
        {
            var definition = reader.GetModuleDefinition();
            return StaticFieldModuleDefinitionIdentity.Create(
                definition.Generation,
                reader.GetString(definition.Name),
                reader.GetGuid(definition.Mvid),
                definition.GenerationId.IsNil ? Guid.Empty : reader.GetGuid(definition.GenerationId),
                definition.BaseGenerationId.IsNil ? Guid.Empty : reader.GetGuid(definition.BaseGenerationId));
        }

        private static StaticFieldAssemblyDefinitionIdentity CreateAssemblyDefinition(MetadataReader reader)
        {
            var definition = reader.GetAssemblyDefinition();
            return StaticFieldAssemblyDefinitionIdentity.Create(
                reader.GetString(definition.Name),
                definition.Version.Major,
                definition.Version.Minor,
                definition.Version.Build,
                definition.Version.Revision,
                definition.Culture.IsNil ? string.Empty : reader.GetString(definition.Culture),
                (int)definition.Flags,
                (int)definition.HashAlgorithm,
                ImmutableArray.CreateRange(reader.GetBlobBytes(definition.PublicKey)));
        }

        private static StaticFieldAssemblyReferenceIdentity CreateAssemblyReference(
            MetadataReader reader,
            AssemblyReferenceHandle handle)
        {
            var reference = reader.GetAssemblyReference(handle);
            return StaticFieldAssemblyReferenceIdentity.Create(
                MetadataTokens.GetToken(handle),
                reader.GetString(reference.Name),
                reference.Version.Major,
                reference.Version.Minor,
                reference.Version.Build,
                reference.Version.Revision,
                reference.Culture.IsNil ? string.Empty : reader.GetString(reference.Culture),
                (int)reference.Flags,
                ImmutableArray.CreateRange(reader.GetBlobBytes(reference.PublicKeyOrToken)),
                ImmutableArray.CreateRange(reader.GetBlobBytes(reference.HashValue)));
        }

        private static void CreateOwnerRanges(
            MetadataReader reader,
            out int[] fieldStarts,
            out int[] fieldEnds,
            out int[] methodStarts,
            out int[] methodEnds,
            out int[] parameterStarts,
            out int[] parameterEnds)
        {
            var typeCount = reader.GetTableRowCount(TableIndex.TypeDef);
            fieldStarts = new int[typeCount + 1];
            fieldEnds = new int[typeCount + 1];
            methodStarts = new int[typeCount + 1];
            methodEnds = new int[typeCount + 1];
            var nextField = reader.GetTableRowCount(TableIndex.Field) + 1;
            var nextMethod = reader.GetTableRowCount(TableIndex.MethodDef) + 1;
            for (var rowId = typeCount; rowId >= 1; rowId--)
            {
                var definition = reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(rowId));
                var fields = definition.GetFields();
                var methods = definition.GetMethods();
                fieldEnds[rowId] = nextField;
                methodEnds[rowId] = nextMethod;
                fieldStarts[rowId] = fields.Count == 0
                    ? nextField
                    : MetadataTokens.GetRowNumber(fields.First());
                methodStarts[rowId] = methods.Count == 0
                    ? nextMethod
                    : MetadataTokens.GetRowNumber(methods.First());
                nextField = fieldStarts[rowId];
                nextMethod = methodStarts[rowId];
            }

            var methodCount = reader.GetTableRowCount(TableIndex.MethodDef);
            parameterStarts = new int[methodCount + 1];
            parameterEnds = new int[methodCount + 1];
            var nextParameter = reader.GetTableRowCount(TableIndex.Param) + 1;
            for (var rowId = methodCount; rowId >= 1; rowId--)
            {
                var definition = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rowId));
                var parameters = definition.GetParameters();
                parameterEnds[rowId] = nextParameter;
                parameterStarts[rowId] = parameters.Count == 0
                    ? nextParameter
                    : MetadataTokens.GetRowNumber(parameters.First());
                nextParameter = parameterStarts[rowId];
            }
        }

        public void Dispose() => provider.Dispose();
    }

    private sealed class UnsupportedMetadataShapeException : Exception
    {
        internal UnsupportedMetadataShapeException(string code)
            : base(code)
        {
            Code = code;
        }

        internal string Code { get; }
    }

    private sealed class DeclarationProjection
    {
        private DeclarationProjection(
            StaticFieldSymbolDeclarationIdentity? declaration,
            StaticFieldDefinitionIdentity? fieldDefinition,
            StaticFieldDeclaredValueKind? valueKind,
            StaticFieldBindingIssue issue,
            string code)
        {
            Declaration = declaration;
            FieldDefinition = fieldDefinition;
            ValueKind = valueKind;
            Issue = issue;
            Code = code;
        }

        internal StaticFieldSymbolDeclarationIdentity? Declaration { get; }

        internal StaticFieldDefinitionIdentity? FieldDefinition { get; }

        internal StaticFieldDeclaredValueKind? ValueKind { get; }

        internal StaticFieldBindingIssue Issue { get; }

        internal string Code { get; }

        internal static DeclarationProjection Admitted(
            StaticFieldSymbolDeclarationIdentity declaration,
            StaticFieldDefinitionIdentity field,
            StaticFieldDeclaredValueKind kind) =>
            new(declaration, field, kind, StaticFieldBindingIssue.None, string.Empty);

        internal static DeclarationProjection Rejected(
            StaticFieldDefinitionIdentity field,
            StaticFieldDeclaredValueKind? kind,
            string code,
            StaticFieldBindingIssue issue) =>
            new(null, field, kind, issue, code);

        internal static DeclarationProjection Unprojectable(string code, StaticFieldBindingIssue issue) =>
            new(null, null, null, issue, code);
    }
}
