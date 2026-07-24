using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Implements the W7 runtime declaration and ordinary-static storage boundary.</summary>
public sealed partial class ClrmdDumpSession
{
    /// <summary>
    /// Exhaustively maps one Product-selected TypeDef and FieldDef to the corresponding ClrMD ordinary-static field.
    /// </summary>
    /// <param name="module">The exact loaded runtime module instance selected by Product metadata binding.</param>
    /// <param name="declaringTypeDefinitionToken">The non-nil declaring TypeDef token from counted metadata.</param>
    /// <param name="declaringTypeName">The exact ClrMD-format full runtime name expected for the declaring type.</param>
    /// <param name="fieldDefinitionToken">The non-nil directly owned FieldDef token from counted metadata.</param>
    /// <param name="fieldName">The exact ordinal FieldDef name from counted metadata.</param>
    /// <param name="expectedDecoderKind">
    /// Product's closed decoder expectation, derived from the exact field signature rather than inferred by Host.
    /// </param>
    /// <returns>
    /// An exact detached mapping after both runtime catalogs are exhausted under their fixed caps; otherwise a typed
    /// partial, unavailable, conflict, invalid, or unsupported result retaining every counted metadata read reached.
    /// </returns>
    /// <remarks>
    /// This operation deliberately performs a cap-plus-one scan of
    /// <see cref="ClrModule.EnumerateTypesWithStaticFields"/> and the selected <see cref="ClrType.StaticFields"/>
    /// catalog. It never treats a prefix as uniqueness and never exposes live ClrMD objects to Product.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is null.</exception>
    /// <exception cref="ArgumentException">A token, name, or decoder tag is not a valid closed W7 request.</exception>
    public ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity> MapStaticFieldDeclaration(
        ClrmdModuleInfo module,
        int declaringTypeDefinitionToken,
        string declaringTypeName,
        int fieldDefinitionToken,
        string fieldName,
        ClrmdStaticExpectedDecoderKind expectedDecoderKind)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(module);
        CanonicalReplayEncoding.ValidateMetadataToken(
            declaringTypeDefinitionToken,
            0x02,
            nameof(declaringTypeDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            fieldDefinitionToken,
            0x04,
            nameof(fieldDefinitionToken));
        ClrmdStaticRuntimeMappingCanonical.ValidateDecodedName(declaringTypeName, nameof(declaringTypeName));
        ClrmdStaticRuntimeMappingCanonical.ValidateDecodedName(fieldName, nameof(fieldName));
        _ = expectedDecoderKind switch
        {
            ClrmdStaticExpectedDecoderKind.Int32 or
            ClrmdStaticExpectedDecoderKind.NullableInt32 or
            ClrmdStaticExpectedDecoderKind.String or
            ClrmdStaticExpectedDecoderKind.ManagedReference => expectedDecoderKind,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedDecoderKind)),
        };

        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_runtimeModules.TryGetValue(module.Identity, out var runtimeModule))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        var ownerMetadata = ReadStaticMetadata(module);
        if (ownerMetadata.Status != ClrmdEvidenceStatus.Exact || ownerMetadata.Value is null)
        {
            return CopyFailure<ClrmdStaticRuntimeDeclarationMappingIdentity, StaticMetadataImage>(ownerMetadata);
        }
        using var ownerMetadataImage = ownerMetadata.Value;

        if (!TryValidateSelectedMetadataRows(
                ownerMetadataImage.Reader,
                declaringTypeDefinitionToken,
                declaringTypeName,
                fieldDefinitionToken,
                fieldName,
                out var metadataFieldAttributes))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: ownerMetadata.Evidence);
        }

        var typeBound = ImmutableArray.Create(
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeDeclarationMappingCounters.RuntimeTypesExaminedBoundName,
                ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeTypesExamined),
            new EvaluationDeterministicBound(
                ClrmdStaticRuntimeDeclarationMappingCounters.DeclaringTypeMatchesRetainedBoundName,
                ClrmdStaticRuntimeDeclarationMappingCounters.MaximumDeclaringTypeMatchesRetained));
        ImmutableArray<ClrType> runtimeTypes;
        try
        {
            runtimeTypes = runtimeModule.EnumerateTypesWithStaticFields()
                .Take(checked(ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeTypesExamined + 1))
                .ToImmutableArray();
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.RuntimeUnsupported,
                evidence: ownerMetadata.Evidence,
                appliedBounds: typeBound);
        }

        if (runtimeTypes.Length > ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeTypesExamined)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                evidence: ownerMetadata.Evidence,
                appliedBounds: typeBound);
        }

        var tokenMatches = runtimeTypes
            .Where(candidate => candidate.MetadataToken == declaringTypeDefinitionToken)
            .Take(ClrmdStaticRuntimeDeclarationMappingCounters.MaximumDeclaringTypeMatchesRetained)
            .ToImmutableArray();
        if (tokenMatches.IsEmpty)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.TypeUnavailable,
                evidence: ownerMetadata.Evidence,
                appliedBounds: typeBound);
        }

        if (tokenMatches.Length != 1)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch,
                evidence: ownerMetadata.Evidence,
                appliedBounds: typeBound);
        }

        var runtimeType = tokenMatches[0];
        if (!ReferenceEquals(runtimeType.Module, runtimeModule) ||
            !string.Equals(runtimeType.Name, declaringTypeName, StringComparison.Ordinal))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: ownerMetadata.Evidence,
                appliedBounds: typeBound);
        }

        var fieldBounds = typeBound.AddRange(
            ImmutableArray.Create(
                new EvaluationDeterministicBound(
                    ClrmdStaticRuntimeDeclarationMappingCounters.RuntimeStaticFieldsExaminedBoundName,
                    ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined),
                new EvaluationDeterministicBound(
                    ClrmdStaticRuntimeDeclarationMappingCounters.StaticFieldMatchesRetainedBoundName,
                    ClrmdStaticRuntimeDeclarationMappingCounters.MaximumStaticFieldMatchesRetained)));
        ImmutableArray<ClrStaticField> runtimeFields;
        try
        {
            runtimeFields = runtimeType.StaticFields
                .Take(checked(ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined + 1))
                .ToImmutableArray();
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.RuntimeUnsupported,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        if (runtimeFields.Length > ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        var fieldTokenMatches = runtimeFields
            .Where(candidate => candidate.Token == fieldDefinitionToken)
            .Take(ClrmdStaticRuntimeDeclarationMappingCounters.MaximumStaticFieldMatchesRetained)
            .ToImmutableArray();
        if (fieldTokenMatches.IsEmpty)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        if (fieldTokenMatches.Length != 1)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        var runtimeField = fieldTokenMatches[0];
        if (!ReferenceEquals(runtimeField.ContainingType, runtimeType) ||
            !string.Equals(runtimeField.Name, fieldName, StringComparison.Ordinal) ||
            runtimeField.Attributes != metadataFieldAttributes)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        if (runtimeField.Type is not { } observedFieldType)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.TypeUnavailable,
                evidence: ownerMetadata.Evidence,
                appliedBounds: fieldBounds);
        }

        using var projectionContext = new StaticRuntimeTypeProjectionContext(this, ownerMetadataImage);
        var declaringProjection = projectionContext.Project(runtimeType);
        if (declaringProjection.Status != ClrmdEvidenceStatus.Exact || declaringProjection.Value is null)
        {
            return projectionContext.CopyProjectionFailure<ClrmdStaticRuntimeDeclarationMappingIdentity>(
                declaringProjection,
                fieldBounds);
        }

        var fieldTypeProjection = projectionContext.Project(observedFieldType);
        if (fieldTypeProjection.Status != ClrmdEvidenceStatus.Exact || fieldTypeProjection.Value is null)
        {
            return projectionContext.CopyProjectionFailure<ClrmdStaticRuntimeDeclarationMappingIdentity>(
                fieldTypeProjection,
                fieldBounds);
        }

        try
        {
            var runtimeFieldIdentity = ClrmdStaticRuntimeFieldIdentity.Create(
                declaringProjection.Value,
                runtimeField.Token,
                runtimeField.Name!,
                runtimeField.Attributes,
                runtimeReportsThreadStatic: false,
                runtimeReportsContextStatic: false,
                expectedDecoderKind,
                fieldTypeProjection.Value);
            var counters = ClrmdStaticRuntimeDeclarationMappingCounters.Create(
                runtimeTypes.Length,
                runtimeFields.Length,
                declaringTypeMatchesRetained: 1,
                staticFieldMatchesRetained: 1,
                runtimeTypeSearchExhaustive: true,
                runtimeStaticFieldSearchExhaustive: true);
            var mapping = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
                declaringProjection.Value,
                runtimeFieldIdentity,
                counters);
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                mapping,
                projectionContext.Evidence,
                CanonicalReplayEncoding.NormalizeBounds(
                    mapping.Counters.CanonicalBounds.AddRange(
                        ImmutableArray.Create(
                            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound,
                            ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound,
                            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphDepthBound,
                            ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphNodeCountBound)),
                    "runtimeMappingBounds"));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeDeclarationMappingIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: projectionContext.Evidence,
                appliedBounds: fieldBounds);
        }
    }

    /// <summary>Acquires and decodes one exact Product-authorized ordinary-static slot through bounded raw reads.</summary>
    /// <param name="request">
    /// The detached physical request created only after Product proved the declaration, decoder, and optional nullable
    /// child layout against the exact runtime mapping.
    /// </param>
    /// <returns>
    /// An immutable observation containing exhaustive domain topology, the returned slot when any, every ordered raw
    /// read, raw-header-first target evidence, and either one exact terminal or the first typed stop.
    /// </returns>
    /// <remarks>
    /// This operation uses ClrMD only to rebind the already proved declaration, enumerate application domains,
    /// obtain the ordinary-static slot, and map a raw method table back to a runtime type. Scalar, nullable, reference,
    /// object-header, string-length, and string-character values are decoded exclusively from <see cref="Memory"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">The request belongs to a different immutable dump snapshot.</exception>
    public ClrmdStaticFieldValueObservation ReadStaticField(ClrmdStaticFieldEvaluationRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        if (request.Snapshot != Snapshot)
        {
            throw new ArgumentException(
                "The physical static-field request belongs to a different immutable dump snapshot.",
                nameof(request));
        }

        var rebound = RebindStaticField(request.RuntimeMapping);
        if (rebound.Field is null)
        {
            return ClrmdStaticFieldValueObservation.PreStorageRebindFailure(
                Snapshot,
                rebound.Status,
                rebound.Issue);
        }

        var domainBound = ImmutableArray.Create(
            ClrmdStaticStorageAcquisitionEvidence.DeclaredApplicationDomainCountBound);
        ImmutableArray<ClrAppDomain> domainPrefix;
        try
        {
            domainPrefix = _runtime.AppDomains
                .OrderBy(static domain => domain.Address)
                .ThenBy(static domain => domain.Id)
                .Take(checked(ClrmdStaticStorageAcquisitionEvidence.MaximumApplicationDomains + 1))
                .ToImmutableArray();
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            return ClrmdStaticFieldValueObservation.PreStorageRebindFailure(
                Snapshot,
                ClrmdStaticFieldObservationStatus.Unsupported,
                ClrmdValueIssue.RuntimeUnsupported);
        }

        var prefixLength = Math.Min(
            domainPrefix.Length,
            ClrmdStaticStorageAcquisitionEvidence.MaximumApplicationDomains);
        var matchingOrdinals = Enumerable.Range(0, prefixLength)
            .Where(index => domainPrefix[index].Address == request.ApplicationDomainAddress)
            .ToImmutableArray();
        if (domainPrefix.Length > ClrmdStaticStorageAcquisitionEvidence.MaximumApplicationDomains)
        {
            var acquisition = ClrmdStaticStorageAcquisitionEvidence.CatalogLimitReached(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                matchingOrdinals);
            return ClrmdStaticFieldValueObservation.Partial(
                Snapshot,
                ClrmdValueIssue.LimitExceeded,
                request,
                slotAddress: null,
                ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
                domainBound,
                storageAcquisitionEvidence: acquisition);
        }

        if (matchingOrdinals.IsEmpty)
        {
            var acquisition = ClrmdStaticStorageAcquisitionEvidence.DomainUnavailable(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                domainPrefix.Length);
            return ClrmdStaticFieldValueObservation.Unavailable(
                Snapshot,
                ClrmdValueIssue.FieldUnavailable,
                request,
                slotAddress: null,
                ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
                domainBound,
                storageAcquisitionEvidence: acquisition);
        }

        if (matchingOrdinals.Length != 1)
        {
            var acquisition = ClrmdStaticStorageAcquisitionEvidence.DomainAmbiguous(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                domainPrefix.Length,
                matchingOrdinals);
            return ClrmdStaticFieldValueObservation.Conflict(
                Snapshot,
                request,
                domainBound,
                acquisition);
        }

        var domainOrdinal = matchingOrdinals[0];
        var domain = domainPrefix[domainOrdinal];
        ulong slotAddress;
        try
        {
            if (!rebound.Field.IsInitialized(domain))
            {
                var acquisition = ClrmdStaticStorageAcquisitionEvidence.SlotUnavailable(
                    request.PointerWidth,
                    request.ApplicationDomainAddress,
                    domainPrefix.Length,
                    domainOrdinal);
                return ClrmdStaticFieldValueObservation.Unavailable(
                    Snapshot,
                    ClrmdValueIssue.FieldUnavailable,
                    request,
                    slotAddress: null,
                    ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
                    domainBound,
                    storageAcquisitionEvidence: acquisition);
            }

            slotAddress = rebound.Field.GetAddress(domain);
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            var acquisition = ClrmdStaticStorageAcquisitionEvidence.RuntimeUnsupported(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                domainPrefix.Length,
                domainOrdinal);
            return ClrmdStaticFieldValueObservation.Unsupported(
                Snapshot,
                request,
                domainBound,
                acquisition);
        }

        if (slotAddress == 0)
        {
            var acquisition = ClrmdStaticStorageAcquisitionEvidence.SlotUnavailable(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                domainPrefix.Length,
                domainOrdinal);
            return ClrmdStaticFieldValueObservation.Unavailable(
                Snapshot,
                ClrmdValueIssue.FieldUnavailable,
                request,
                slotAddress: null,
                ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
                domainBound,
                storageAcquisitionEvidence: acquisition);
        }

        var maximumAddress = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if (slotAddress > maximumAddress ||
            (ulong)(request.StorageSize - 1) > maximumAddress - slotAddress)
        {
            if (slotAddress > maximumAddress)
            {
                return ClrmdStaticFieldValueObservation.PreStorageRebindFailure(
                    Snapshot,
                    ClrmdStaticFieldObservationStatus.Invalid,
                    ClrmdValueIssue.InvalidData);
            }

            var acquisition = ClrmdStaticStorageAcquisitionEvidence.InvalidSlot(
                request.PointerWidth,
                request.ApplicationDomainAddress,
                domainPrefix.Length,
                domainOrdinal,
                slotAddress,
                request.StorageSize);
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress: null,
                ImmutableArray<ClrmdRawMemoryEvidence>.Empty,
                domainBound,
                targetEvidence: null,
                acquisition);
        }

        var acquired = ClrmdStaticStorageAcquisitionEvidence.Acquired(
            request.PointerWidth,
            request.ApplicationDomainAddress,
            domainPrefix.Length,
            domainOrdinal,
            slotAddress,
            request.StorageSize);
        return request.ValueShape switch
        {
            ClrmdStaticFieldValueShape.Int32 => ReadStaticInt32(request, slotAddress, acquired),
            ClrmdStaticFieldValueShape.NullableInt32 => ReadStaticNullableInt32(request, slotAddress, acquired),
            ClrmdStaticFieldValueShape.String or ClrmdStaticFieldValueShape.ObjectReference =>
                ReadStaticReference(request, slotAddress, acquired),
            _ => throw new InvalidOperationException("The physical request contains an unknown decoder shape."),
        };
    }

    /// <summary>
    /// Projects an already exact raw-header-first object into the compatibility shape consumed by existing W2/W6
    /// instance-member engines without inventing a CLR handle source.
    /// </summary>
    /// <param name="value">The exact non-null TypeDef-backed object selected by a typed Product source.</param>
    /// <returns>
    /// An exact direct-address projection after runtime object, method table, TypeDef, name, module, and extent agree;
    /// otherwise a typed unavailable, conflict, invalid, or unsupported result. The projection uses zero for the
    /// legacy handle slot and the non-handle marker <c>TypedObjectBinding</c>.
    /// </returns>
    /// <remarks>
    /// The operation performs one direct heap lookup at the already proved address and no heap enumeration or raw
    /// memory read. It reuses the exact header bytes carried by <paramref name="value"/> as detached evidence. The
    /// caller must retain the authoritative typed source provenance separately.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ClrmdEvidenceResult<ClrmdHeapObjectInfo> ProjectExactObjectForInstanceEvaluation(
        ClrmdExactObjectReference value)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(value);
        if (value.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }
        var identity = value.HeaderRuntimeType;
        if (identity.Kind != ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition ||
            identity.RuntimeModule is not { } runtimeModuleIdentity ||
            identity.TypeDefinitionToken is not { } typeDefinitionToken)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemberShapeUnsupported);
        }

        var modules = Modules.Where(module => module.Identity == runtimeModuleIdentity).ToArray();
        if (modules.Length != 1)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                modules.Length == 0 ? ClrmdEvidenceStatus.Unavailable : ClrmdEvidenceStatus.Conflict,
                modules.Length == 0 ? ClrmdValueIssue.ModuleUnavailable : ClrmdValueIssue.AmbiguousMatch);
        }

        ClrObject runtimeObject;
        ClrType? runtimeType;
        ulong objectSize;
        try
        {
            runtimeObject = _runtime.Heap.GetObject(value.Address);
            runtimeType = runtimeObject.Type;
            objectSize = runtimeObject.Size;
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception) || exception is OverflowException)
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }
        if (!runtimeObject.IsValid || runtimeType is null || objectSize < (ulong)Memory.PointerSize ||
            value.Address > ulong.MaxValue - (objectSize - 1))
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.ObjectUnavailable);
        }
        if (runtimeType.MethodTable != value.MethodTable ||
            runtimeType.MetadataToken != typeDefinitionToken ||
            !string.Equals(runtimeType.Name, identity.FullName, StringComparison.Ordinal) ||
            !RuntimeTypeMatchesIdentity(runtimeType, identity))
        {
            return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var header = value.HeaderEvidence;
        var evidence = ImmutableArray.Create(MemoryReadResult.Create(
            Memory.SourceId,
            header.Address,
            header.RequestedLength,
            header.Bytes.AsSpan()));
        var result = new ClrmdHeapObjectInfo(
            Snapshot,
            value.Address,
            identity.FullName,
            typeDefinitionToken,
            value.MethodTable,
            rootAddress: 0,
            rootKind: nameof(ClrmdHeapObjectSelectionKind.TypedObjectBinding),
            modules[0],
            evidence,
            ClrmdHeapObjectSelectionKind.TypedObjectBinding);
        return ClrmdEvidenceResult<ClrmdHeapObjectInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            result,
            evidence);
    }

    /// <summary>
    /// Projects the complete raw specialized-value field catalog needed for Product's nullable layout proof.
    /// </summary>
    /// <param name="runtimeMapping">
    /// The exact outer declaration mapping whose Product-supplied decoder tag is Nullable&lt;Int32&gt;.
    /// </param>
    /// <returns>
    /// An exact detached payload extent and complete raw child catalog, or a typed failure before Product assigns
    /// semantic HasValue/value roles. Counted metadata reads used to project child runtime types are retained.
    /// </returns>
    /// <remarks>
    /// This operation is deliberately metadata-semantic-blind: it does not select children by name, recognize
    /// Boolean or Int32, or assume offsets. Product must correlate every role with its independently resolved metadata.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="runtimeMapping"/> is null.</exception>
    /// <exception cref="ArgumentException">The mapping belongs to a different immutable dump snapshot.</exception>
    public ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity> MapStaticNullableRuntimeLayout(
        ClrmdStaticRuntimeDeclarationMappingIdentity runtimeMapping)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(runtimeMapping);
        if (runtimeMapping.DeclaringType.Snapshot != Snapshot)
        {
            throw new ArgumentException(
                "The nullable runtime mapping belongs to a different immutable dump snapshot.",
                nameof(runtimeMapping));
        }
        if (runtimeMapping.Field.ExpectedDecoderKind != ClrmdStaticExpectedDecoderKind.NullableInt32)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var rebound = RebindStaticField(runtimeMapping);
        if (rebound.Field is null)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ToEvidenceStatus(rebound.Status),
                rebound.Issue);
        }
        if (rebound.Field.Type is not { } runtimeNullableType ||
            rebound.Field.Size <= 0 ||
            !RuntimeTypeMatchesIdentity(runtimeNullableType, runtimeMapping.Field.ObservedFieldType))
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var fieldBound = ImmutableArray.Create(
            ClrmdStaticNullableRuntimeLayoutIdentity.DeclaredRuntimeFieldCountBound);
        var fields = runtimeNullableType.Fields;
        if (fields.IsDefault)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                appliedBounds: fieldBound);
        }
        if (fields.Length == 0)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable,
                appliedBounds: fieldBound);
        }
        if (fields.Length > ClrmdStaticNullableRuntimeLayoutIdentity.MaximumRuntimeFields)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                appliedBounds: fieldBound);
        }

        var moduleKey = (runtimeNullableType.Module.AppDomain.Address, runtimeNullableType.Module.Address);
        if (!_moduleInfos.TryGetValue(moduleKey, out var module))
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable,
                appliedBounds: fieldBound);
        }
        var metadata = ReadStaticMetadata(module);
        if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                metadata.Status,
                metadata.Issue,
                evidence: metadata.Evidence,
                appliedBounds: fieldBound.AddRange(metadata.AppliedBounds));
        }

        using var metadataImage = metadata.Value;
        using var projectionContext = new StaticRuntimeTypeProjectionContext(this, metadataImage);
        var projectedFields = ImmutableArray.CreateBuilder<ClrmdStaticNullableRuntimeFieldIdentity>(fields.Length);
        foreach (var field in fields)
        {
            if (field.Type is not { } observedType ||
                string.IsNullOrEmpty(field.Name) ||
                field.Offset < 0 || field.Size <= 0)
            {
                return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: projectionContext.Evidence,
                    appliedBounds: fieldBound);
            }

            var observedProjection = projectionContext.Project(observedType);
            if (observedProjection.Status != ClrmdEvidenceStatus.Exact || observedProjection.Value is null)
            {
                return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                    observedProjection.Status,
                    observedProjection.Issue,
                    evidence: projectionContext.Evidence,
                    appliedBounds: fieldBound.AddRange(observedProjection.AppliedBounds));
            }

            try
            {
                projectedFields.Add(ClrmdStaticNullableRuntimeFieldIdentity.Create(
                    runtimeMapping.Field.ObservedFieldType,
                    field.Token,
                    field.Name,
                    field.Offset,
                    field.Size,
                    observedProjection.Value));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence: projectionContext.Evidence,
                    appliedBounds: fieldBound);
            }
        }

        try
        {
            var layout = ClrmdStaticNullableRuntimeLayoutIdentity.Create(
                runtimeMapping,
                rebound.Field.Size,
                projectedFields.MoveToImmutable());
            var bounds = CanonicalReplayEncoding.NormalizeBounds(
                fieldBound.AddRange(
                    ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphDepthBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeTypeGraphNodeCountBound)),
                "nullableRuntimeLayoutBounds");
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                layout,
                projectionContext.Evidence,
                bounds);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ClrmdEvidenceResult<ClrmdStaticNullableRuntimeLayoutIdentity>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                evidence: projectionContext.Evidence,
                appliedBounds: fieldBound);
        }
    }

    private static ClrmdEvidenceStatus ToEvidenceStatus(ClrmdStaticFieldObservationStatus status) => status switch
    {
        ClrmdStaticFieldObservationStatus.Partial => ClrmdEvidenceStatus.Partial,
        ClrmdStaticFieldObservationStatus.Unavailable or ClrmdStaticFieldObservationStatus.Unsupported =>
            ClrmdEvidenceStatus.Unavailable,
        ClrmdStaticFieldObservationStatus.Conflict => ClrmdEvidenceStatus.Conflict,
        ClrmdStaticFieldObservationStatus.Invalid => ClrmdEvidenceStatus.Invalid,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private StaticFieldRebindResult RebindStaticField(ClrmdStaticRuntimeDeclarationMappingIdentity mapping)
    {
        var declaringIdentity = mapping.DeclaringType;
        if (declaringIdentity.RuntimeModule is not { } runtimeModuleIdentity ||
            !_runtimeModules.TryGetValue(runtimeModuleIdentity, out var runtimeModule))
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        ClrType? runtimeType;
        try
        {
            runtimeType = runtimeModule.GetTypeByName(declaringIdentity.FullName);
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Unsupported,
                ClrmdValueIssue.RuntimeUnsupported);
        }

        if (runtimeType is null)
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Unavailable,
                ClrmdValueIssue.TypeUnavailable);
        }

        if (!RuntimeTypeMatchesIdentity(runtimeType, declaringIdentity))
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        ImmutableArray<ClrStaticField> fields;
        try
        {
            fields = runtimeType.StaticFields
                .Take(checked(ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined + 1))
                .ToImmutableArray();
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Unsupported,
                ClrmdValueIssue.RuntimeUnsupported);
        }

        if (fields.Length > ClrmdStaticRuntimeDeclarationMappingCounters.MaximumRuntimeStaticFieldsExamined)
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Partial,
                ClrmdValueIssue.LimitExceeded);
        }

        if (fields.Length != mapping.Counters.RuntimeStaticFieldsExamined)
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        var matches = fields
            .Where(field => field.Token == mapping.Field.FieldDefinitionToken)
            .Take(2)
            .ToImmutableArray();
        if (matches.IsEmpty)
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable);
        }

        if (matches.Length != 1)
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch);
        }

        var field = matches[0];
        if (!ReferenceEquals(field.ContainingType, runtimeType) ||
            !string.Equals(field.Name, mapping.Field.Name, StringComparison.Ordinal) ||
            field.Attributes != mapping.Field.Attributes ||
            field.Type is not { } fieldType ||
            !RuntimeTypeMatchesIdentity(fieldType, mapping.Field.ObservedFieldType))
        {
            return StaticFieldRebindResult.Failure(
                ClrmdStaticFieldObservationStatus.Conflict,
                ClrmdValueIssue.TypeMismatch);
        }

        return StaticFieldRebindResult.Success(field);
    }

    private static bool RuntimeTypeMatchesIdentity(
        ClrType runtimeType,
        ClrmdStaticRuntimeTypeIdentity identity)
    {
        if (identity.Kind != ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition ||
            identity.RuntimeModule is not { } module ||
            runtimeType.IsArray ||
            runtimeType.MetadataToken != identity.TypeDefinitionToken ||
            !string.Equals(runtimeType.Name, identity.FullName, StringComparison.Ordinal) ||
            (runtimeType.MethodTable == 0 ? null : runtimeType.MethodTable) != identity.MethodTable ||
            runtimeType.IsValueType != identity.IsValueType ||
            runtimeType.IsPrimitive != identity.IsPrimitive ||
            ((runtimeType.TypeAttributes & TypeAttributes.Interface) != 0) != identity.IsInterface)
        {
            return false;
        }

        return runtimeType.Module.AppDomain.Address == module.AppDomainAddress &&
            runtimeType.Module.Address == module.ModuleAddress &&
            runtimeType.Module.ImageBase == module.ImageBase &&
            runtimeType.Module.Size == module.ImageSize;
    }

    private ClrmdStaticFieldValueObservation ReadStaticInt32(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ClrmdStaticStorageAcquisitionEvidence acquisition)
    {
        var read = ReadStaticRaw(slotAddress, sizeof(int));
        var reads = ImmutableArray.Create(read);
        var bounds = DecoderBounds(includeStringCap: false);
        if (!read.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        return ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            reads,
            ClrmdStaticFieldValue.ExactInt32(BinaryPrimitives.ReadInt32LittleEndian(read.Bytes.AsSpan())),
            bounds);
    }

    private ClrmdStaticFieldValueObservation ReadStaticNullableInt32(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ClrmdStaticStorageAcquisitionEvidence acquisition)
    {
        var layout = request.NullableInt32Layout!;
        var bounds = DecoderBounds(includeStringCap: false);
        var slot = ReadStaticRaw(slotAddress, request.PointerWidth);
        var reads = ImmutableArray.Create(slot);
        if (!slot.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(slot.Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence: null,
                acquisition);
        }

        var maximumAddress = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(request.PointerWidth - 1) > maximumAddress - targetAddress)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence: null,
                acquisition);
        }

        var header = ReadStaticRaw(targetAddress, request.PointerWidth);
        reads = reads.Add(header);
        if (!header.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        var methodTable = ClrmdStaticPhysicalCanonical.DecodePointer(header.Bytes.AsSpan(), request.PointerWidth);
        if (request.ObservedFieldType.MethodTable is not { } expectedMethodTable ||
            methodTable != expectedMethodTable)
        {
            return ClrmdStaticFieldValueObservation.Conflict(
                Snapshot,
                ClrmdValueIssue.TypeMismatch,
                request,
                slotAddress,
                reads,
                bounds,
                acquisition);
        }

        ulong valueStorageAddress;
        try
        {
            valueStorageAddress = checked(targetAddress + (ulong)request.PointerWidth);
            if ((ulong)(layout.StorageSize - 1) > maximumAddress - valueStorageAddress)
            {
                throw new OverflowException();
            }
        }
        catch (OverflowException)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence: null,
                acquisition);
        }

        var flagAddress = checked(valueStorageAddress + (ulong)layout.HasValueOffset);
        var flag = ReadStaticRaw(flagAddress, sizeof(byte));
        reads = reads.Add(flag);
        if (!flag.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        if (flag.Bytes[0] > 1)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence: null,
                acquisition);
        }

        if (flag.Bytes[0] == 0)
        {
            return ClrmdStaticFieldValueObservation.Exact(
                request,
                acquisition,
                reads,
                ClrmdStaticFieldValue.NullableInt32NoValue(),
                bounds);
        }

        var valueAddress = checked(valueStorageAddress + (ulong)layout.ValueOffset);
        var value = ReadStaticRaw(valueAddress, sizeof(int));
        reads = reads.Add(value);
        if (!value.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        return ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            reads,
            ClrmdStaticFieldValue.NullableInt32Value(
                BinaryPrimitives.ReadInt32LittleEndian(value.Bytes.AsSpan())),
            bounds);
    }

    private ClrmdStaticFieldValueObservation ReadStaticReference(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ClrmdStaticStorageAcquisitionEvidence acquisition)
    {
        var slot = ReadStaticRaw(slotAddress, request.PointerWidth);
        var reads = ImmutableArray.Create(slot);
        var bounds = DecoderBounds(includeStringCap: false);
        if (!slot.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition);
        }

        var targetAddress = ClrmdStaticPhysicalCanonical.DecodePointer(slot.Bytes.AsSpan(), request.PointerWidth);
        if (targetAddress == 0)
        {
            return ClrmdStaticFieldValueObservation.Exact(
                request,
                acquisition,
                reads,
                ClrmdStaticFieldValue.NullReference(),
                bounds);
        }

        var maximumAddress = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if ((ulong)(request.PointerWidth - 1) > maximumAddress - targetAddress)
        {
            var invalidTarget = ClrmdStaticTargetEvidence.InvalidHeaderAddress(
                Snapshot,
                request.PointerWidth,
                targetAddress);
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                invalidTarget,
                acquisition);
        }

        var header = ReadStaticRaw(targetAddress, request.PointerWidth);
        reads = reads.Add(header);
        if (!header.IsExact)
        {
            var unavailableTarget = ClrmdStaticTargetEvidence.HeaderUnavailable(
                Snapshot,
                request.PointerWidth,
                targetAddress,
                header);
            return IncompleteRead(
                request,
                slotAddress,
                reads,
                bounds,
                acquisition,
                unavailableTarget);
        }

        var methodTable = ClrmdStaticPhysicalCanonical.DecodePointer(header.Bytes.AsSpan(), request.PointerWidth);
        if (methodTable == 0)
        {
            var invalidTarget = ClrmdStaticTargetEvidence.InvalidMethodTable(
                Snapshot,
                request.PointerWidth,
                targetAddress,
                header);
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                invalidTarget,
                acquisition);
        }

        ClrType? runtimeType;
        try
        {
            runtimeType = _runtime.GetTypeByMethodTable(methodTable);
        }
        catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
        {
            runtimeType = null;
        }

        if (runtimeType is null)
        {
            try
            {
                var objectType = _runtime.Heap.GetObjectType(targetAddress);
                if (objectType?.MethodTable == methodTable)
                {
                    runtimeType = objectType;
                }
            }
            catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
            {
                runtimeType = null;
            }
        }

        if (runtimeType is null)
        {
            var unavailableTarget = ClrmdStaticTargetEvidence.RuntimeTypeUnavailable(
                Snapshot,
                request.PointerWidth,
                targetAddress,
                header);
            return ClrmdStaticFieldValueObservation.Unavailable(
                Snapshot,
                ClrmdValueIssue.TypeUnavailable,
                request,
                slotAddress,
                reads,
                bounds,
                unavailableTarget,
                acquisition);
        }

        var projectedTarget = ProjectStaticTargetType(runtimeType);
        var targetType = projectedTarget.Status == ClrmdEvidenceStatus.Exact
            ? projectedTarget.Value
            : null;

        if (targetType is null)
        {
            var unavailableTarget = ClrmdStaticTargetEvidence.RuntimeTypeUnavailable(
                Snapshot,
                request.PointerWidth,
                targetAddress,
                header);
            return ClrmdStaticFieldValueObservation.Unavailable(
                Snapshot,
                ClrmdValueIssue.TypeUnavailable,
                request,
                slotAddress,
                reads,
                bounds,
                unavailableTarget,
                acquisition);
        }

        ClrmdStaticTargetEvidence targetEvidence;
        if (targetType.MethodTable != methodTable)
        {
            targetEvidence = ClrmdStaticTargetEvidence.RuntimeTypeConflict(
                Snapshot,
                request.PointerWidth,
                targetAddress,
                header,
                targetType);
            return ClrmdStaticFieldValueObservation.Conflict(
                Snapshot,
                ClrmdValueIssue.TypeMismatch,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition);
        }

        targetEvidence = ClrmdStaticTargetEvidence.Matched(
            Snapshot,
            request.PointerWidth,
            targetAddress,
            header,
            targetType);
        var objectReference = ClrmdExactObjectReference.Create(targetEvidence);
        if (request.ValueShape == ClrmdStaticFieldValueShape.ObjectReference)
        {
            return ClrmdStaticFieldValueObservation.Exact(
                request,
                acquisition,
                reads,
                ClrmdStaticFieldValue.ExactObjectReference(objectReference),
                bounds);
        }

        return ReadStaticString(request, slotAddress, acquisition, reads, targetEvidence, objectReference);
    }

    private ClrmdStaticFieldValueObservation ReadStaticString(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ClrmdStaticStorageAcquisitionEvidence acquisition,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ClrmdStaticTargetEvidence targetEvidence,
        ClrmdExactObjectReference objectReference)
    {
        ulong lengthAddress;
        try
        {
            lengthAddress = checked(objectReference.Address + (ulong)request.PointerWidth);
        }
        catch (OverflowException)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                DecoderBounds(includeStringCap: false),
                targetEvidence,
                acquisition);
        }

        var length = ReadStaticRaw(lengthAddress, sizeof(int));
        reads = reads.Add(length);
        var bounds = DecoderBounds(includeStringCap: length.IsExact &&
            BinaryPrimitives.ReadInt32LittleEndian(length.Bytes.AsSpan()) >= 0);
        if (!length.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition, targetEvidence);
        }

        var characterCount = BinaryPrimitives.ReadInt32LittleEndian(length.Bytes.AsSpan());
        if (characterCount < 0)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition);
        }

        if (characterCount > ClrmdExactStringValue.MaximumCharacters)
        {
            return ClrmdStaticFieldValueObservation.Partial(
                Snapshot,
                ClrmdValueIssue.LimitExceeded,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition);
        }

        if (characterCount == 0)
        {
            var exactString = ClrmdExactStringValue.Create(
                objectReference,
                string.Empty,
                length,
                characterEvidence: null);
            return ClrmdStaticFieldValueObservation.Exact(
                request,
                acquisition,
                reads,
                ClrmdStaticFieldValue.ExactString(exactString),
                bounds);
        }

        var characterByteCount = checked(characterCount * sizeof(char));
        ulong characterAddress;
        try
        {
            characterAddress = checked(lengthAddress + sizeof(int));
            var maximumAddress = request.PointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
            if ((ulong)(characterByteCount - 1) > maximumAddress - characterAddress)
            {
                throw new OverflowException();
            }
        }
        catch (OverflowException)
        {
            return ClrmdStaticFieldValueObservation.Invalid(
                Snapshot,
                ClrmdValueIssue.InvalidData,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition);
        }

        var characters = ReadStaticRaw(characterAddress, characterByteCount);
        reads = reads.Add(characters);
        if (!characters.IsExact)
        {
            return IncompleteRead(request, slotAddress, reads, bounds, acquisition, targetEvidence);
        }

        var value = Encoding.Unicode.GetString(characters.Bytes.AsSpan());
        var exactValue = ClrmdExactStringValue.Create(objectReference, value, length, characters);
        return ClrmdStaticFieldValueObservation.Exact(
            request,
            acquisition,
            reads,
            ClrmdStaticFieldValue.ExactString(exactValue),
            bounds);
    }

    private ClrmdStaticFieldValueObservation IncompleteRead(
        ClrmdStaticFieldEvaluationRequest request,
        ulong slotAddress,
        ImmutableArray<ClrmdRawMemoryEvidence> reads,
        ImmutableArray<EvaluationDeterministicBound> bounds,
        ClrmdStaticStorageAcquisitionEvidence acquisition,
        ClrmdStaticTargetEvidence? targetEvidence = null)
    {
        var finalRead = reads[^1];
        return finalRead.Status == ClrmdRawMemoryStatus.Partial
            ? ClrmdStaticFieldValueObservation.Partial(
                Snapshot,
                ClrmdValueIssue.MemoryUnavailable,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition)
            : ClrmdStaticFieldValueObservation.Unavailable(
                Snapshot,
                ClrmdValueIssue.MemoryUnavailable,
                request,
                slotAddress,
                reads,
                bounds,
                targetEvidence,
                acquisition);
    }

    private ClrmdRawMemoryEvidence ReadStaticRaw(ulong address, int length)
    {
        var read = Memory.Read(address, length);
        return read.Status switch
        {
            MemoryReadStatus.Exact => ClrmdRawMemoryEvidence.Exact(Snapshot, address, read.Bytes),
            MemoryReadStatus.Partial => ClrmdRawMemoryEvidence.Partial(
                Snapshot,
                address,
                length,
                read.Bytes),
            MemoryReadStatus.Unavailable => ClrmdRawMemoryEvidence.Unavailable(Snapshot, address, length),
            _ => throw new InvalidOperationException("The memory reader returned an unknown completeness status."),
        };
    }

    private static ImmutableArray<EvaluationDeterministicBound> DecoderBounds(bool includeStringCap)
    {
        var bounds = ImmutableArray.Create(
            ClrmdStaticStorageAcquisitionEvidence.DeclaredApplicationDomainCountBound,
            ClrmdStaticFieldValueObservation.DeclaredRawReadCountBound);
        return includeStringCap
            ? bounds.Add(ClrmdExactStringValue.DeclaredCharacterLimitBound)
            : bounds;
    }

    private ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity> ProjectStaticTargetType(ClrType runtimeType)
    {
        var moduleKey = (runtimeType.Module.AppDomain.Address, runtimeType.Module.Address);
        if (!_moduleInfos.TryGetValue(moduleKey, out var module))
        {
            return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        var metadata = ReadStaticMetadata(module);
        if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
        {
            return CopyFailure<ClrmdStaticRuntimeTypeIdentity, StaticMetadataImage>(metadata);
        }

        using var metadataImage = metadata.Value;
        using var context = new StaticRuntimeTypeProjectionContext(this, metadataImage);
        return context.Project(runtimeType);
    }

    private ClrmdEvidenceResult<StaticMetadataImage> ReadStaticMetadata(ClrmdModuleInfo module)
    {
        var metadata = ReadCompleteMetadata(module);
        if (metadata.Status != ClrmdEvidenceStatus.Exact || metadata.Value is null)
        {
            return CopyFailure<StaticMetadataImage, CompleteMetadataImage>(metadata);
        }

        try
        {
            var provider = MetadataReaderProvider.FromMetadataImage(metadata.Value.Bytes);
            var reader = provider.GetMetadataReader();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            return ClrmdEvidenceResult<StaticMetadataImage>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                new StaticMetadataImage(
                    module,
                    ModuleContentIdentity.FromMetadata(mvid, metadata.Value.Bytes.AsSpan()),
                    provider,
                    reader,
                    metadata.Evidence),
                metadata.Evidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or ArgumentException or InvalidOperationException)
        {
            return ClrmdEvidenceResult<StaticMetadataImage>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: metadata.Evidence);
        }
    }

    private static bool TryValidateSelectedMetadataRows(
        MetadataReader reader,
        int declaringTypeDefinitionToken,
        string declaringRuntimeName,
        int fieldDefinitionToken,
        string fieldName,
        out FieldAttributes fieldAttributes)
    {
        fieldAttributes = default;
        var typeHandle = MetadataTokens.TypeDefinitionHandle(declaringTypeDefinitionToken & 0x00ff_ffff);
        var fieldHandle = MetadataTokens.FieldDefinitionHandle(fieldDefinitionToken & 0x00ff_ffff);
        if (typeHandle.IsNil || fieldHandle.IsNil ||
            MetadataTokens.GetRowNumber(typeHandle) > reader.GetTableRowCount(TableIndex.TypeDef) ||
            MetadataTokens.GetRowNumber(fieldHandle) > reader.GetTableRowCount(TableIndex.Field))
        {
            return false;
        }

        var type = reader.GetTypeDefinition(typeHandle);
        var metadataFullName = RuntimeMetadataFullName(reader, typeHandle);
        if (!RuntimeNameMatchesMetadataDefinition(declaringRuntimeName, metadataFullName) ||
            !type.GetFields().Contains(fieldHandle))
        {
            return false;
        }

        var field = reader.GetFieldDefinition(fieldHandle);
        if (!string.Equals(reader.GetString(field.Name), fieldName, StringComparison.Ordinal))
        {
            return false;
        }

        fieldAttributes = field.Attributes;
        return true;
    }

    private static bool RuntimeNameMatchesMetadataDefinition(string runtimeName, string metadataName)
    {
        if (string.Equals(runtimeName, metadataName, StringComparison.Ordinal))
        {
            return true;
        }

        var arityDelimiter = metadataName.LastIndexOf('`');
        var genericDelimiter = runtimeName.IndexOf('<');
        return arityDelimiter >= 0 && genericDelimiter >= 0 &&
            runtimeName.AsSpan(0, genericDelimiter)
                .Equals(metadataName.AsSpan(0, arityDelimiter), StringComparison.Ordinal);
    }

    private static string RuntimeMetadataFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);
        var declaringType = definition.GetDeclaringType();
        if (!declaringType.IsNil)
        {
            return $"{RuntimeMetadataFullName(reader, declaringType)}+{name}";
        }

        var namespaceName = reader.GetString(definition.Namespace);
        return namespaceName.Length == 0 ? name : $"{namespaceName}.{name}";
    }

    private static ClrmdEvidenceResult<TTarget> CopyFailure<TTarget, TSource>(
        ClrmdEvidenceResult<TSource> source)
        where TTarget : class
        where TSource : class =>
        ClrmdEvidenceResult<TTarget>.Create(
            source.Status,
            source.Issue,
            evidence: source.Evidence,
            appliedBounds: source.AppliedBounds);

    private static bool IsClrmdRuntimeFailure(Exception exception) =>
        exception is ClrDiagnosticsException or InvalidDataException or InvalidOperationException or
            ArgumentOutOfRangeException or NotSupportedException or OverflowException;

    private sealed record StaticFieldRebindResult(
        ClrStaticField? Field,
        ClrmdStaticFieldObservationStatus Status,
        ClrmdValueIssue Issue)
    {
        internal static StaticFieldRebindResult Success(ClrStaticField field) =>
            new(field, ClrmdStaticFieldObservationStatus.Exact, ClrmdValueIssue.None);

        internal static StaticFieldRebindResult Failure(
            ClrmdStaticFieldObservationStatus status,
            ClrmdValueIssue issue) =>
            new(null, status, issue);
    }

    private sealed class StaticRuntimeTypeProjectionContext : IDisposable
    {
        private readonly ClrmdDumpSession session;
        private readonly Dictionary<(ulong AppDomain, ulong Module), StaticMetadataImage> metadata = [];
        private readonly Dictionary<ulong, ClrmdStaticRuntimeTypeIdentity> projectedMethodTables = [];
        private readonly List<MemoryReadResult> evidence = [];
        private readonly List<StaticMetadataImage> ownedMetadata = [];

        internal StaticRuntimeTypeProjectionContext(ClrmdDumpSession session, StaticMetadataImage ownerMetadata)
        {
            this.session = session;
            metadata.Add(
                (ownerMetadata.Module.Identity.AppDomainAddress, ownerMetadata.Module.Identity.ModuleAddress),
                ownerMetadata);
            AddEvidence(ownerMetadata.Evidence);
        }

        internal ImmutableArray<MemoryReadResult> Evidence => evidence.ToImmutableArray();

        internal ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity> Project(ClrType runtimeType)
        {
            ArgumentNullException.ThrowIfNull(runtimeType);
            if (runtimeType.IsArray)
            {
                return ProjectArray(runtimeType);
            }

            if (runtimeType.MethodTable != 0 &&
                projectedMethodTables.TryGetValue(runtimeType.MethodTable, out var cached))
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Exact,
                    ClrmdValueIssue.None,
                    cached,
                    Evidence);
            }

            var moduleKey = (runtimeType.Module.AppDomain.Address, runtimeType.Module.Address);
            if (!session._moduleInfos.TryGetValue(moduleKey, out var moduleInfo))
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.ModuleUnavailable,
                    evidence: Evidence);
            }

            if (!metadata.TryGetValue(moduleKey, out var moduleMetadata))
            {
                var metadataResult = session.ReadStaticMetadata(moduleInfo);
                AddEvidence(metadataResult.Evidence);
                if (metadataResult.Status != ClrmdEvidenceStatus.Exact || metadataResult.Value is null)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        metadataResult.Status,
                        metadataResult.Issue,
                        evidence: Evidence,
                        appliedBounds: metadataResult.AppliedBounds);
                }

                moduleMetadata = metadataResult.Value;
                metadata.Add(moduleKey, moduleMetadata);
                ownedMetadata.Add(moduleMetadata);
            }

            var name = runtimeType.Name;
            if (string.IsNullOrEmpty(name) ||
                name.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Partial,
                    ClrmdValueIssue.LimitExceeded,
                    evidence: Evidence,
                    appliedBounds: ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound));
            }

            if (!TryValidateRuntimeTypeMetadata(moduleMetadata.Reader, runtimeType, name))
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence: Evidence);
            }

            var genericArgumentNames = ParseConstructedGenericArguments(name);
            if (genericArgumentNames.Length > ClrmdStaticRuntimeTypeIdentity.MaximumGenericArgumentCount)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Partial,
                    ClrmdValueIssue.LimitExceeded,
                    evidence: Evidence,
                    appliedBounds: ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredGenericArgumentCountBound));
            }

            var genericArguments = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(genericArgumentNames.Length);
            foreach (var genericArgumentName in genericArgumentNames)
            {
                var candidates = session._runtimeModules
                    .OrderBy(static pair => pair.Key.AppDomainAddress)
                    .ThenBy(static pair => pair.Key.ModuleAddress)
                    .Select(pair => pair.Value.GetTypeByName(genericArgumentName))
                    .Where(static candidate => candidate is not null)
                    .Cast<ClrType>()
                    .GroupBy(static candidate => (candidate.Module.Address, candidate.MetadataToken, candidate.MethodTable))
                    .Select(static group => group.First())
                    .Take(2)
                    .ToImmutableArray();
                if (candidates.Length == 0)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.TypeUnavailable,
                        evidence: Evidence);
                }

                if (candidates.Length != 1)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Conflict,
                        ClrmdValueIssue.AmbiguousMatch,
                        evidence: Evidence);
                }

                var argumentProjection = Project(candidates[0]);
                if (argumentProjection.Status != ClrmdEvidenceStatus.Exact || argumentProjection.Value is null)
                {
                    return argumentProjection;
                }

                genericArguments.Add(argumentProjection.Value);
            }

            try
            {
                var projection = ClrmdStaticRuntimeTypeIdentity.Create(
                    session.Snapshot,
                    session.Memory.PointerSize,
                    moduleInfo.Identity,
                    moduleMetadata.Content,
                    runtimeType.MetadataToken,
                    name,
                    runtimeType.MethodTable == 0 ? null : runtimeType.MethodTable,
                    runtimeType.IsValueType,
                    runtimeType.IsPrimitive,
                    runtimeType.IsArray,
                    (runtimeType.TypeAttributes & TypeAttributes.Interface) != 0,
                    genericArguments.MoveToImmutable());
                if (runtimeType.MethodTable != 0)
                {
                    projectedMethodTables.TryAdd(runtimeType.MethodTable, projection);
                }

                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Exact,
                    ClrmdValueIssue.None,
                    projection,
                    Evidence);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence: Evidence);
            }
        }

        private ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity> ProjectArray(ClrType runtimeType)
        {
            if (runtimeType.MethodTable != 0 &&
                projectedMethodTables.TryGetValue(runtimeType.MethodTable, out var cached))
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Exact,
                    ClrmdValueIssue.None,
                    cached,
                    Evidence);
            }

            var name = runtimeType.Name;
            if (runtimeType.MethodTable == 0 ||
                string.IsNullOrEmpty(name) ||
                name.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters ||
                !TryParseArrayShape(name, out var rank, out var isSzArray, out var componentName) ||
                runtimeType.BaseType is not { } baseType)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.TypeUnavailable,
                    evidence: Evidence,
                    appliedBounds: ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredArrayRankBound));
            }

            var componentType = runtimeType.ComponentType;
            if (componentType is null)
            {
                var candidates = FindRuntimeTypesByName(componentName);
                if (candidates.Length == 0)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.TypeUnavailable,
                        evidence: Evidence);
                }
                if (candidates.Length != 1)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Conflict,
                        ClrmdValueIssue.AmbiguousMatch,
                        evidence: Evidence);
                }
                componentType = candidates[0];
            }

            ImmutableArray<ClrInterface> interfaceTypes;
            try
            {
                interfaceTypes = runtimeType.EnumerateInterfaces()
                    .Take(ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeInterfaceTypeCount + 1)
                    .ToImmutableArray();
            }
            catch (Exception exception) when (IsClrmdRuntimeFailure(exception))
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.RuntimeUnsupported,
                    evidence: Evidence);
            }
            if (interfaceTypes.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeInterfaceTypeCount)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Partial,
                    ClrmdValueIssue.LimitExceeded,
                    evidence: Evidence,
                    appliedBounds: ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeInterfaceTypeCountBound));
            }

            var component = Project(componentType);
            if (component.Status != ClrmdEvidenceStatus.Exact || component.Value is null)
            {
                return component;
            }
            var projectedBase = Project(baseType);
            if (projectedBase.Status != ClrmdEvidenceStatus.Exact || projectedBase.Value is null)
            {
                return projectedBase;
            }
            var interfaces = ImmutableArray.CreateBuilder<ClrmdStaticRuntimeTypeIdentity>(interfaceTypes.Length);
            foreach (var interfaceType in interfaceTypes)
            {
                if (string.IsNullOrEmpty(interfaceType.Name))
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.TypeUnavailable,
                        evidence: Evidence);
                }
                var candidates = FindRuntimeTypesByName(interfaceType.Name);
                if (candidates.Length == 0)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.TypeUnavailable,
                        evidence: Evidence);
                }
                if (candidates.Length != 1)
                {
                    return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                        ClrmdEvidenceStatus.Conflict,
                        ClrmdValueIssue.AmbiguousMatch,
                        evidence: Evidence);
                }

                var projectedInterface = Project(candidates[0]);
                if (projectedInterface.Status != ClrmdEvidenceStatus.Exact || projectedInterface.Value is null)
                {
                    return projectedInterface;
                }
                interfaces.Add(projectedInterface.Value);
            }

            try
            {
                var projection = ClrmdStaticRuntimeTypeIdentity.CreateArray(
                    session.Snapshot,
                    session.Memory.PointerSize,
                    name,
                    runtimeType.MethodTable,
                    rank,
                    isSzArray,
                    component.Value,
                    projectedBase.Value,
                    interfaces.MoveToImmutable());
                projectedMethodTables.TryAdd(runtimeType.MethodTable, projection);
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Exact,
                    ClrmdValueIssue.None,
                    projection,
                    Evidence,
                    ImmutableArray.Create(
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeNameCharacterBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredArrayRankBound,
                        ClrmdStaticRuntimeTypeIdentity.DeclaredRuntimeInterfaceTypeCountBound));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Conflict,
                    ClrmdValueIssue.TypeMismatch,
                    evidence: Evidence);
            }
        }

        private ImmutableArray<ClrType> FindRuntimeTypesByName(string name) =>
            session._runtimeModules
                .OrderBy(static pair => pair.Key.AppDomainAddress)
                .ThenBy(static pair => pair.Key.ModuleAddress)
                .Select(pair => pair.Value.GetTypeByName(name))
                .Where(static candidate => candidate is not null)
                .Cast<ClrType>()
                .GroupBy(static candidate => (candidate.Module.Address, candidate.MetadataToken, candidate.MethodTable))
                .Select(static group => group.First())
                .Take(2)
                .ToImmutableArray();

        private static bool TryParseArrayShape(
            string name,
            out int rank,
            out bool isSzArray,
            out string componentName)
        {
            rank = 0;
            isSzArray = false;
            componentName = string.Empty;
            var opening = name.LastIndexOf('[');
            if (opening <= 0 || name[^1] != ']')
            {
                return false;
            }

            componentName = name[..opening];
            var shape = name.AsSpan(opening + 1, name.Length - opening - 2);
            if (shape.IsEmpty)
            {
                rank = 1;
                isSzArray = true;
                return true;
            }
            if (shape.Length == 1 && shape[0] == '*')
            {
                rank = 1;
                return true;
            }
            if (shape.IndexOfAnyExcept(',') < 0)
            {
                rank = shape.Length + 1;
                return rank <= ClrmdStaticRuntimeTypeIdentity.MaximumArrayRank;
            }
            return false;
        }

        internal ClrmdEvidenceResult<T> CopyProjectionFailure<T>(
            ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity> failure,
            ImmutableArray<EvaluationDeterministicBound> mappingBounds)
            where T : class =>
            ClrmdEvidenceResult<T>.Create(
                failure.Status,
                failure.Issue,
                evidence: Evidence,
                appliedBounds: mappingBounds.AddRange(failure.AppliedBounds));

        public void Dispose()
        {
            foreach (var image in ownedMetadata)
            {
                image.Dispose();
            }
        }

        private static bool TryValidateRuntimeTypeMetadata(
            MetadataReader reader,
            ClrType runtimeType,
            string runtimeName)
        {
            if ((runtimeType.MetadataToken & unchecked((int)0xff00_0000)) != 0x0200_0000)
            {
                return false;
            }

            var handle = MetadataTokens.TypeDefinitionHandle(runtimeType.MetadataToken & 0x00ff_ffff);
            return !handle.IsNil &&
                MetadataTokens.GetRowNumber(handle) <= reader.GetTableRowCount(TableIndex.TypeDef) &&
                RuntimeNameMatchesMetadataDefinition(runtimeName, RuntimeMetadataFullName(reader, handle));
        }

        private static ImmutableArray<string> ParseConstructedGenericArguments(string runtimeName)
        {
            var opening = runtimeName.IndexOf('<');
            if (opening < 0)
            {
                return ImmutableArray<string>.Empty;
            }

            if (runtimeName[^1] != '>')
            {
                return ImmutableArray<string>.Empty;
            }

            var arguments = ImmutableArray.CreateBuilder<string>();
            var depth = 0;
            var start = opening + 1;
            for (var index = start; index < runtimeName.Length - 1; index++)
            {
                switch (runtimeName[index])
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        if (depth == 0)
                        {
                            return ImmutableArray<string>.Empty;
                        }

                        depth--;
                        break;
                    case ',' when depth == 0:
                        if (!AddArgument(runtimeName.AsSpan(start, index - start), arguments))
                        {
                            return ImmutableArray<string>.Empty;
                        }

                        start = index + 1;
                        break;
                }
            }

            return depth == 0 && AddArgument(
                    runtimeName.AsSpan(start, runtimeName.Length - 1 - start),
                    arguments)
                ? arguments.ToImmutable()
                : ImmutableArray<string>.Empty;
        }

        private static bool AddArgument(
            ReadOnlySpan<char> argument,
            ImmutableArray<string>.Builder arguments)
        {
            argument = argument.Trim();
            if (argument.IsEmpty)
            {
                return false;
            }

            arguments.Add(argument.ToString());
            return true;
        }

        private void AddEvidence(ImmutableArray<MemoryReadResult> reads)
        {
            foreach (var read in reads)
            {
                if (!evidence.Any(candidate =>
                        candidate.Address == read.Address &&
                        candidate.RequestedLength == read.RequestedLength &&
                        candidate.Status == read.Status &&
                        candidate.Bytes.AsSpan().SequenceEqual(read.Bytes.AsSpan())))
                {
                    evidence.Add(read);
                }
            }
        }
    }

    private sealed class StaticMetadataImage : IDisposable
    {
        internal StaticMetadataImage(
            ClrmdModuleInfo module,
            ModuleContentIdentity content,
            MetadataReaderProvider provider,
            MetadataReader reader,
            ImmutableArray<MemoryReadResult> evidence)
        {
            Module = module;
            Content = content;
            Provider = provider;
            Reader = reader;
            Evidence = evidence;
        }

        internal ClrmdModuleInfo Module { get; }

        internal ModuleContentIdentity Content { get; }

        internal MetadataReaderProvider Provider { get; }

        internal MetadataReader Reader { get; }

        internal ImmutableArray<MemoryReadResult> Evidence { get; }

        public void Dispose() => Provider.Dispose();
    }
}
