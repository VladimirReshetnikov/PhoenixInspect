using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

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
    /// This draft operation deliberately performs a cap-plus-one scan of
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
        exception is ClrDiagnosticsException or InvalidOperationException or NotSupportedException or OverflowException;

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
                return ClrmdEvidenceResult<ClrmdStaticRuntimeTypeIdentity>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.RuntimeUnsupported,
                    evidence: Evidence);
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
