using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Metadata.SRM;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Projects one exactly counted dump metadata image and method body into immutable execution descriptors.
/// </summary>
/// <remarks>
/// This resolver never opens a target-reported path or substitutes bytes from a local PE. The complete metadata
/// image, physical method header, IL code, and declared extra sections must all be exact reads from the same dump
/// snapshot. Metadata is reparsed from the retained immutable image for contextual field resolution, and each SRM
/// result is a deep immutable Core projection that outlives the temporary metadata reader.
/// </remarks>
public sealed class ClrmdDumpExecutionResolver : IResolutionServices
{
    private readonly ImmutableArray<byte> metadataImage;
    private readonly ResolvedMethodDefinition methodDefinition;
    private readonly ImmutableArray<int> fieldOperandTokens;
    private readonly bool fieldOperandScanComplete;

    private ClrmdDumpExecutionResolver(
        ClrmdModuleInfo module,
        ModuleContentIdentity contentIdentity,
        ModuleHandle moduleHandle,
        ImmutableArray<byte> metadataImage,
        ResolvedMethodDefinition methodDefinition,
        ImmutableArray<int> fieldOperandTokens,
        bool fieldOperandScanComplete)
    {
        Module = module;
        ContentIdentity = contentIdentity;
        ModuleHandle = moduleHandle;
        this.metadataImage = metadataImage;
        this.methodDefinition = methodDefinition;
        this.fieldOperandTokens = fieldOperandTokens;
        this.fieldOperandScanComplete = fieldOperandScanComplete;
    }

    /// <summary>Gets the exact runtime-module instance whose dump evidence backs this resolver.</summary>
    public ClrmdModuleInfo Module { get; }

    /// <summary>Gets the MVID, byte count, and digest recomputed from the complete counted metadata image.</summary>
    public ModuleContentIdentity ContentIdentity { get; }

    /// <summary>
    /// Gets the execution module handle derived from both metadata content and snapshot-scoped runtime module identity.
    /// </summary>
    public ModuleHandle ModuleHandle { get; }

    /// <summary>Gets the sole exact MethodDef identity admitted by this resolver instance.</summary>
    public MethodHandle Method => methodDefinition.Method;

    /// <summary>Gets the immutable counted-body and metadata-signature projection returned for <see cref="Method"/>.</summary>
    public ResolvedMethodDefinition MethodDefinition => methodDefinition;

    /// <summary>
    /// Creates a dump-grounded resolver only when separately counted metadata-identity and method-body operations
    /// agree on one complete metadata image and every method-memory read is exact for the same snapshot.
    /// </summary>
    /// <param name="module">The selected runtime module instance.</param>
    /// <param name="metadataIdentity">
    /// The exact result of <c>ClrmdDumpSession.ReadModuleContentIdentity(module)</c>, including its sole complete
    /// metadata-root read.
    /// </param>
    /// <param name="methodBody">
    /// The exact counted method-body result whose first evidence item is an independent read of the same metadata
    /// image and whose remaining items comprise its complete physical body.
    /// </param>
    /// <returns>
    /// An immutable resolver on success; otherwise a structured unavailable, invalid, or conflicting result. Partial
    /// evidence is never promoted to a resolver.
    /// </returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static ResolutionResult<ClrmdDumpExecutionResolver> Create(
        ClrmdModuleInfo module,
        ClrmdEvidenceResult<ModuleContentIdentity> metadataIdentity,
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> methodBody)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(metadataIdentity);
        ArgumentNullException.ThrowIfNull(methodBody);

        if (metadataIdentity.Status != ClrmdEvidenceStatus.Exact || metadataIdentity.Value is null)
        {
            return EvidenceFailure<ClrmdDumpExecutionResolver>(
                metadataIdentity.Status,
                "DUMP_EXEC_METADATA_NOT_EXACT",
                "Execution projection requires one complete exact dump metadata image.");
        }

        if (methodBody.Status != ClrmdEvidenceStatus.Exact || methodBody.Value is null)
        {
            return EvidenceFailure<ClrmdDumpExecutionResolver>(
                methodBody.Status,
                "DUMP_EXEC_METHOD_NOT_EXACT",
                "Execution projection requires a completely counted exact dump method body.");
        }

        var metadataValidation = ValidateMetadataEvidence(module, metadataIdentity);
        if (metadataValidation is { } metadataFailure)
        {
            return Failed<ClrmdDumpExecutionResolver>(metadataFailure);
        }

        var countedBodyValidation = ValidateCountedMethodEvidence(
            module,
            metadataIdentity.Evidence[0],
            methodBody);
        if (countedBodyValidation is { } bodyFailure)
        {
            return Failed<ClrmdDumpExecutionResolver>(bodyFailure);
        }

        try
        {
            var metadataRead = metadataIdentity.Evidence[0];
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
            var reader = provider.GetMetadataReader();
            var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);
            var recomputedIdentity = ModuleContentIdentity.FromMetadata(mvid, metadataRead.Bytes.AsSpan());
            var identityMatch = metadataIdentity.Value.VerifyMatches(recomputedIdentity);
            if (!identityMatch.IsSuccess)
            {
                return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                    identityMatch.Failure!.Kind,
                    identityMatch.Failure.Code,
                    identityMatch.Failure.Message);
            }

            var bodyInfo = methodBody.Value;
            var rowId = bodyInfo.MetadataToken & 0x00FF_FFFF;
            if (!MethodHandle.IsValidMetadataToken(bodyInfo.MetadataToken) ||
                rowId > reader.MethodDefinitions.Count)
            {
                return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                    ResolutionFailureKind.Invalid,
                    "DUMP_EXEC_METHOD_TOKEN_INVALID",
                    "The counted method token does not identify a MethodDef in the counted metadata image.");
            }

            var metadataMethod = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rowId));
            if (metadataMethod.RelativeVirtualAddress != bodyInfo.RelativeVirtualAddress)
            {
                return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                    ResolutionFailureKind.Conflict,
                    "DUMP_EXEC_METHOD_RVA_CONFLICT",
                    "The counted method body and metadata image disagree on the MethodDef RVA.");
            }

            var physicalReplayFailure = ValidatePhysicalMethodReplay(
                module,
                methodBody,
                reader.GetTableRowCount(TableIndex.StandAloneSig));
            if (physicalReplayFailure is { } replayFailure)
            {
                return Failed<ClrmdDumpExecutionResolver>(replayFailure);
            }

            var moduleHandle = ModuleHandle.FromRuntimeEvidenceIdentity(
                recomputedIdentity,
                module.Identity.SourceId);
            var methodHandle = new MethodHandle(moduleHandle, bodyInfo.MetadataToken);
            var projection = SrmMetadataProjection.ProjectMethodDefinition(
                reader,
                moduleHandle,
                methodHandle,
                bodyInfo.Body);
            if (!projection.IsSuccess)
            {
                return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                    projection.Failure!.Kind,
                    projection.Failure.Code,
                    projection.Failure.Message);
            }

            var scanComplete = TryCollectFieldOperandTokens(
                bodyInfo.Body.CodeBytes,
                out var fieldOperandTokens);
            return ResolutionResult<ClrmdDumpExecutionResolver>.Success(
                new ClrmdDumpExecutionResolver(
                    module,
                    recomputedIdentity,
                    moduleHandle,
                    metadataRead.Bytes,
                    projection.Value,
                    fieldOperandTokens,
                    scanComplete));
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
            InvalidOperationException or
            ArgumentException or
            ArgumentOutOfRangeException or
            OverflowException)
        {
            return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METADATA_INVALID",
                "The counted dump metadata could not be projected into an execution descriptor.");
        }
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
    {
        if (method != Method)
        {
            return ResolutionResult<ResolvedMethodDefinition>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METHOD_MISMATCH",
                "This dump resolver contains a different counted MethodDef.");
        }

        return ResolutionResult<ResolvedMethodDefinition>.Success(methodDefinition);
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
    {
        if (contextMethod != Method)
        {
            return ResolutionResult<ResolvedField>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_CONTEXT_MISMATCH",
                "The field request belongs to a different method context than this dump resolver.");
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage);
            return SrmMetadataProjection.ProjectField(
                provider.GetMetadataReader(),
                ModuleHandle,
                contextMethod,
                metadataToken);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
            InvalidOperationException or
            ArgumentException or
            ArgumentOutOfRangeException)
        {
            return ResolutionResult<ResolvedField>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_METADATA_INVALID",
                "The counted dump metadata could not project the requested field descriptor.");
        }
    }

    /// <summary>
    /// Correlates one unique exact strong-root object and exact counted Int32 observation with this method's
    /// metadata field descriptor without importing either into semantic memory.
    /// </summary>
    /// <param name="ownerSearch">
    /// An exact bounded handle traversal with exactly one retained match from this resolver's snapshot and module.
    /// </param>
    /// <param name="fieldObservation">
    /// An exact runtime-selected Int32 field observation for that unique owner, including one complete four-byte read.
    /// </param>
    /// <returns>
    /// A frozen execution-evidence descriptor only when snapshot, module, owner, token, declaring type, field type,
    /// storage kind, address, byte count, and decoded value all agree; otherwise a structured failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public ResolutionResult<ClrmdExactInt32FieldExecutionEvidence> CorrelateExactInt32Field(
        ClrmdHeapObjectSearchResult ownerSearch,
        ClrmdEvidenceResult<ClrmdInt32FieldObservation> fieldObservation)
    {
        ArgumentNullException.ThrowIfNull(ownerSearch);
        ArgumentNullException.ThrowIfNull(fieldObservation);

        if (ownerSearch.Status != ClrmdEvidenceStatus.Exact || ownerSearch.Issue != ClrmdValueIssue.None)
        {
            return EvidenceFailure<ClrmdExactInt32FieldExecutionEvidence>(
                ownerSearch.Status,
                "DUMP_EXEC_OWNER_NOT_EXACT",
                "Concrete-memory import requires an exact, exhaustive owner selection.");
        }

        if (ownerSearch.Snapshot != Module.Identity.Snapshot)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_SNAPSHOT_CONFLICT",
                "The selected owner belongs to a different dump snapshot.");
        }

        if (ownerSearch.Matches.Length == 0)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Unavailable,
                "DUMP_EXEC_OWNER_UNAVAILABLE",
                "The exact owner search found no matching object.");
        }

        if (ownerSearch.Matches.Length != 1)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_AMBIGUOUS",
                "Execution import requires one uniquely selected dump object.");
        }

        var owner = ownerSearch.Matches[0];
        if (owner.Snapshot != Module.Identity.Snapshot || owner.Module.Identity != Module.Identity)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_MODULE_CONFLICT",
                "The selected dump object does not belong to the resolver's runtime module instance.");
        }

        if (!string.Equals(ownerSearch.TypeNameSelector, owner.TypeName, StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_TYPE_CONFLICT",
                "The exact owner selection predicate disagrees with the retained runtime type.");
        }

        if (owner.Address == 0 ||
            owner.MethodTable == 0 ||
            owner.RootAddress == 0 ||
            string.IsNullOrWhiteSpace(owner.RootKind) ||
            owner.Evidence.Length != 2 ||
            owner.Evidence[0].Address != owner.RootAddress ||
            owner.Evidence[1].Address != owner.Address ||
            owner.Evidence[0].RequestedLength != owner.Evidence[1].RequestedLength ||
            !TryDecodePointer(owner.Evidence[0].Bytes.AsSpan(), out var rootedObjectAddress) ||
            rootedObjectAddress != owner.Address ||
            !TryDecodePointer(owner.Evidence[1].Bytes.AsSpan(), out var observedMethodTable) ||
            observedMethodTable != owner.MethodTable ||
            !AllExactFromSnapshot(ownerSearch.Evidence, Module.Identity.Snapshot) ||
            !AllExactFromSnapshot(owner.Evidence, Module.Identity.Snapshot) ||
            !ContainsReadSubsequence(ownerSearch.Evidence, owner.Evidence))
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_OWNER_EVIDENCE_INVALID",
                "The selected owner is not backed by complete counted reads from the resolver snapshot.");
        }

        if (fieldObservation.Status != ClrmdEvidenceStatus.Exact ||
            fieldObservation.Issue != ClrmdValueIssue.None ||
            fieldObservation.Value is null)
        {
            return EvidenceFailure<ClrmdExactInt32FieldExecutionEvidence>(
                fieldObservation.Status,
                "DUMP_EXEC_FIELD_NOT_EXACT",
                "Concrete-memory import requires one exact counted Int32 field observation.");
        }

        var observation = fieldObservation.Value;
        var runtimeField = observation.Field;
        if (runtimeField.Snapshot != owner.Snapshot ||
            runtimeField.OwnerAddress != owner.Address ||
            runtimeField.OwnerMethodTable != owner.MethodTable ||
            !string.Equals(runtimeField.OwnerTypeName, owner.TypeName, StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_OWNER_CONFLICT",
                "The runtime field descriptor does not belong to the uniquely selected owner.");
        }

        if (!fieldOperandScanComplete)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Unsupported,
                "DUMP_EXEC_FIELD_OPERAND_SCAN_UNSUPPORTED",
                "The counted method body is outside the closed instruction profile used for field correlation.");
        }

        if (fieldOperandTokens.Length != 1)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_OPERAND_COUNT_CONFLICT",
                "The closed dump-grounded getter profile requires exactly one ldfld occurrence.");
        }

        if (fieldOperandTokens[0] != runtimeField.MetadataToken)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_OPERAND_CONFLICT",
                "The runtime field token is not an ldfld operand in the counted method body.");
        }

        var projectedField = ResolveField(Method, runtimeField.MetadataToken);
        if (!projectedField.IsSuccess)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                projectedField.Failure!.Kind,
                projectedField.Failure.Code,
                projectedField.Failure.Message);
        }

        var field = projectedField.Value;
        if (field.Handle.Module != ModuleHandle ||
            field.Handle.MetadataToken != runtimeField.MetadataToken ||
            !TypeSig.IsValidTypeDefinitionToken(owner.TypeMetadataToken) ||
            owner.TypeMetadataToken != methodDefinition.Signature.DeclaringType.MetadataToken ||
            !string.Equals(
                owner.TypeName,
                methodDefinition.Signature.DeclaringType.DisplayName,
                StringComparison.Ordinal) ||
            field.DeclaringType != methodDefinition.Signature.DeclaringType ||
            field.FieldType != TypeSig.Int32 ||
            field.IsStatic ||
            field.IsLiteral ||
            field.HasRva)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_METADATA_TYPE_CONFLICT",
                "The projected FieldDef is not the method owner's ordinary instance Int32 field.");
        }

        if (runtimeField.Size != sizeof(int) ||
            runtimeField.Address == 0 ||
            runtimeField.IsObjectReference ||
            !string.Equals(runtimeField.ElementType, "Int32", StringComparison.Ordinal) ||
            !string.Equals(runtimeField.FieldTypeName, "System.Int32", StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_RUNTIME_TYPE_CONFLICT",
                "The runtime field descriptor is not an exact Int32 storage description.");
        }

        var memory = observation.Memory;
        if (fieldObservation.Evidence.Length != 1 ||
            !SameRead(fieldObservation.Evidence[0], memory) ||
            memory.Status != MemoryReadStatus.Exact ||
            !string.Equals(memory.SourceId, Module.Identity.Snapshot.MemorySourceId, StringComparison.Ordinal) ||
            memory.Address != runtimeField.Address ||
            memory.RequestedLength != sizeof(int) ||
            memory.Bytes.Length != sizeof(int) ||
            observation.Value is not { } value ||
            BinaryPrimitives.ReadInt32LittleEndian(memory.Bytes.AsSpan()) != value)
        {
            return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_VALUE_EVIDENCE_INVALID",
                "The Int32 observation is not backed by exactly one complete matching four-byte dump read.");
        }

        return ResolutionResult<ClrmdExactInt32FieldExecutionEvidence>.Success(
            new ClrmdExactInt32FieldExecutionEvidence(
                Module.Identity,
                methodDefinition,
                field,
                ownerSearch,
                owner,
                observation,
                value));
    }

    private static ProjectionFailure? ValidateMetadataEvidence(
        ClrmdModuleInfo module,
        ClrmdEvidenceResult<ModuleContentIdentity> metadataIdentity)
    {
        if (metadataIdentity.Issue != ClrmdValueIssue.None || metadataIdentity.Evidence.Length != 1)
        {
            return Invalid(
                "DUMP_EXEC_METADATA_EVIDENCE_INVALID",
                "An exact metadata identity must carry exactly one complete metadata-root read.");
        }

        var read = metadataIdentity.Evidence[0];
        if (read is null ||
            module.MetadataAddress == 0 ||
            module.MetadataLength == 0 ||
            module.MetadataLength > int.MaxValue ||
            read.Status != MemoryReadStatus.Exact ||
            !string.Equals(read.SourceId, module.Identity.Snapshot.MemorySourceId, StringComparison.Ordinal) ||
            read.Address != module.MetadataAddress ||
            read.RequestedLength != checked((int)module.MetadataLength) ||
            read.Bytes.Length != read.RequestedLength ||
            metadataIdentity.Value!.MetadataLength != read.RequestedLength)
        {
            return Invalid(
                "DUMP_EXEC_METADATA_PROVENANCE_INVALID",
                "The metadata identity is not backed by the selected module's complete counted metadata range.");
        }

        return null;
    }

    private static ProjectionFailure? ValidateCountedMethodEvidence(
        ClrmdModuleInfo module,
        MemoryReadResult identityMetadataRead,
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> methodBody)
    {
        if (methodBody.Issue != ClrmdValueIssue.None || methodBody.Evidence.IsDefaultOrEmpty)
        {
            return Invalid(
                "DUMP_EXEC_METHOD_EVIDENCE_INVALID",
                "An exact method body must retain its counted metadata and physical body reads.");
        }

        var info = methodBody.Value!;
        if (!SameRead(identityMetadataRead, methodBody.Evidence[0]))
        {
            return Conflict(
                "DUMP_EXEC_METADATA_READ_CONFLICT",
                "The identity and method acquisitions disagree on the counted metadata image.");
        }

        if (!AllExactFromSnapshot(methodBody.Evidence, module.Identity.Snapshot) ||
            info.HeaderEvidence.IsDefaultOrEmpty ||
            info.ExtraSectionEvidence.IsDefault ||
            info.Body.CodeBytes.IsDefault ||
            info.Code is null ||
            info.Code.Status != MemoryReadStatus.Exact ||
            info.Code.Address != info.CodeAddress ||
            info.Code.RequestedLength != info.Body.CodeBytes.Length ||
            !info.Code.Bytes.SequenceEqual(info.Body.CodeBytes))
        {
            return Invalid(
                "DUMP_EXEC_METHOD_READS_INVALID",
                "The normalized method body is not backed by complete matching physical dump reads.");
        }

        var expectedReadCount = 1 + info.HeaderEvidence.Length + 1 + info.ExtraSectionEvidence.Length;
        if (methodBody.Evidence.Length != expectedReadCount)
        {
            return Invalid(
                "DUMP_EXEC_METHOD_READ_SET_INVALID",
                "The counted method evidence contains missing or unclassified physical reads.");
        }

        var outerIndex = 1;
        foreach (var headerRead in info.HeaderEvidence)
        {
            if (!SameRead(methodBody.Evidence[outerIndex++], headerRead))
            {
                return Invalid(
                    "DUMP_EXEC_METHOD_HEADER_EVIDENCE_INVALID",
                    "The physical header read set disagrees with the retained method evidence.");
            }
        }

        if (!SameRead(methodBody.Evidence[outerIndex++], info.Code))
        {
            return Invalid(
                "DUMP_EXEC_METHOD_CODE_EVIDENCE_INVALID",
                "The physical IL read disagrees with the retained method evidence.");
        }

        foreach (var extraRead in info.ExtraSectionEvidence)
        {
            if (!SameRead(methodBody.Evidence[outerIndex++], extraRead))
            {
                return Invalid(
                    "DUMP_EXEC_METHOD_SECTION_EVIDENCE_INVALID",
                    "The physical extra-section read set disagrees with the retained method evidence.");
            }
        }

        ulong nextHeaderAddress = info.HeaderAddress;
        foreach (var headerRead in info.HeaderEvidence)
        {
            if (headerRead is null ||
                headerRead.RequestedLength <= 0 ||
                headerRead.Address != nextHeaderAddress ||
                nextHeaderAddress > ulong.MaxValue - (ulong)headerRead.RequestedLength)
            {
                return Invalid(
                    "DUMP_EXEC_METHOD_HEADER_RANGE_INVALID",
                    "The physical method header reads are not complete, ordered, and contiguous.");
            }

            nextHeaderAddress += (ulong)headerRead.RequestedLength;
        }

        if (nextHeaderAddress != info.CodeAddress ||
            info.RelativeVirtualAddress <= 0 ||
            module.Identity.ImageBase == 0 ||
            module.Identity.ImageBase > ulong.MaxValue - (uint)info.RelativeVirtualAddress ||
            module.Identity.ImageBase + (uint)info.RelativeVirtualAddress != info.HeaderAddress)
        {
            return Conflict(
                "DUMP_EXEC_METHOD_ADDRESS_CONFLICT",
                "The counted method RVA, header range, and code address do not describe one physical body.");
        }

        return null;
    }

    private static ProjectionFailure? ValidatePhysicalMethodReplay(
        ClrmdModuleInfo module,
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> methodBody,
        int standaloneSignatureRowCount)
    {
        var info = methodBody.Value!;
        var physicalReads = ImmutableArray.CreateBuilder<MemoryReadResult>(
            info.HeaderEvidence.Length + 1 + info.ExtraSectionEvidence.Length);
        physicalReads.AddRange(info.HeaderEvidence);
        physicalReads.Add(info.Code);
        physicalReads.AddRange(info.ExtraSectionEvidence);
        var replayMemory = new CountedMethodEvidenceReader(
            module.Identity.Snapshot.MemorySourceId,
            physicalReads.ToImmutable());

        ClrmdEvidenceResult<ClrmdMethodBodyInfo> replay;
        try
        {
            replay = ClrmdMethodBodyParser.Read(
                replayMemory,
                info.MetadataToken,
                info.RelativeVirtualAddress,
                module.Identity.ImageBase,
                module.Identity.ImageSize,
                standaloneSignatureRowCount,
                ImmutableArray.Create(methodBody.Evidence[0]));
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            ArgumentException or
            ArgumentOutOfRangeException or
            OverflowException)
        {
            return Invalid(
                "DUMP_EXEC_METHOD_PHYSICAL_REPLAY_INVALID",
                "The counted physical method reads cannot reproduce the normalized method body.");
        }

        if (replay.Status != ClrmdEvidenceStatus.Exact ||
            replay.Issue != ClrmdValueIssue.None ||
            replay.Value is not { } replayInfo ||
            !replayMemory.ConsumedAll ||
            replayInfo.MetadataToken != info.MetadataToken ||
            replayInfo.RelativeVirtualAddress != info.RelativeVirtualAddress ||
            replayInfo.HeaderAddress != info.HeaderAddress ||
            replayInfo.HeaderKind != info.HeaderKind ||
            replayInfo.CodeAddress != info.CodeAddress ||
            replayInfo.Body.MaxStack != info.Body.MaxStack ||
            replayInfo.Body.LocalVariablesInitialized != info.Body.LocalVariablesInitialized ||
            replayInfo.Body.LocalSignatureToken != info.Body.LocalSignatureToken ||
            replayInfo.Body.ExceptionRegionCount != info.Body.ExceptionRegionCount ||
            !replayInfo.Body.CodeBytes.SequenceEqual(info.Body.CodeBytes) ||
            !SameReadSequence(replay.Evidence, methodBody.Evidence) ||
            !SameReadSequence(replayInfo.HeaderEvidence, info.HeaderEvidence) ||
            !SameRead(replayInfo.Code, info.Code) ||
            !SameReadSequence(replayInfo.ExtraSectionEvidence, info.ExtraSectionEvidence))
        {
            return Conflict(
                "DUMP_EXEC_METHOD_PHYSICAL_REPLAY_CONFLICT",
                "The counted physical method reads disagree with the retained normalized body or evidence layout.");
        }

        return null;
    }

    private static bool AllExactFromSnapshot(
        ImmutableArray<MemoryReadResult> reads,
        ClrmdSnapshotIdentity snapshot)
    {
        if (reads.IsDefault)
        {
            return false;
        }

        foreach (var read in reads)
        {
            if (read is null ||
                read.Status != MemoryReadStatus.Exact ||
                read.Bytes.Length != read.RequestedLength ||
                !string.Equals(read.SourceId, snapshot.MemorySourceId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsReadSubsequence(
        ImmutableArray<MemoryReadResult> containing,
        ImmutableArray<MemoryReadResult> candidate)
    {
        if (candidate.IsDefault || candidate.Length == 0 || containing.IsDefault || candidate.Length > containing.Length)
        {
            return false;
        }

        for (var start = 0; start <= containing.Length - candidate.Length; start++)
        {
            var matches = true;
            for (var index = 0; index < candidate.Length; index++)
            {
                if (!SameRead(containing[start + index], candidate[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameRead(MemoryReadResult? left, MemoryReadResult? right) =>
        left is not null &&
        right is not null &&
        string.Equals(left.SourceId, right.SourceId, StringComparison.Ordinal) &&
        left.Address == right.Address &&
        left.RequestedLength == right.RequestedLength &&
        left.Status == right.Status &&
        left.Bytes.SequenceEqual(right.Bytes);

    private static bool SameReadSequence(
        ImmutableArray<MemoryReadResult> left,
        ImmutableArray<MemoryReadResult> right)
    {
        if (left.IsDefault || right.IsDefault || left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!SameRead(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectFieldOperandTokens(
        ImmutableArray<byte> code,
        out ImmutableArray<int> tokens)
    {
        var builder = ImmutableArray.CreateBuilder<int>();
        if (code.IsDefault)
        {
            tokens = ImmutableArray<int>.Empty;
            return false;
        }

        for (var offset = 0; offset < code.Length;)
        {
            var opcode = code[offset];
            int size;
            switch (opcode)
            {
                case 0x00:
                case >= 0x02 and <= 0x0D:
                case >= 0x15 and <= 0x1E:
                case 0x2A:
                case >= 0x58 and <= 0x5A:
                    size = 1;
                    break;
                case 0x0E:
                case 0x11:
                case 0x13:
                case 0x1F:
                    size = 2;
                    break;
                case 0x20:
                    size = 5;
                    break;
                case 0x7B:
                    size = 5;
                    if (offset > code.Length - size)
                    {
                        tokens = ImmutableArray<int>.Empty;
                        return false;
                    }

                    builder.Add(BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset + 1, sizeof(int))));
                    break;
                default:
                    tokens = ImmutableArray<int>.Empty;
                    return false;
            }

            if (offset > code.Length - size)
            {
                tokens = ImmutableArray<int>.Empty;
                return false;
            }

            offset += size;
        }

        tokens = builder.ToImmutable();
        return true;
    }

    private static bool TryDecodePointer(ReadOnlySpan<byte> bytes, out ulong value)
    {
        switch (bytes.Length)
        {
            case sizeof(uint):
                value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                return true;
            case sizeof(ulong):
                value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static ResolutionResult<T> EvidenceFailure<T>(
        ClrmdEvidenceStatus status,
        string code,
        string message) =>
        ResolutionResult<T>.Failed(
            status switch
            {
                ClrmdEvidenceStatus.Conflict => ResolutionFailureKind.Conflict,
                ClrmdEvidenceStatus.Invalid => ResolutionFailureKind.Invalid,
                _ => ResolutionFailureKind.Unavailable,
            },
            code,
            message);

    private static ResolutionResult<T> Failed<T>(ProjectionFailure failure) =>
        ResolutionResult<T>.Failed(failure.Kind, failure.Code, failure.Message);

    private static ProjectionFailure Invalid(string code, string message) =>
        new(ResolutionFailureKind.Invalid, code, message);

    private static ProjectionFailure Conflict(string code, string message) =>
        new(ResolutionFailureKind.Conflict, code, message);

    private readonly record struct ProjectionFailure(
        ResolutionFailureKind Kind,
        string Code,
        string Message);

    private sealed class CountedMethodEvidenceReader : IProcessMemoryReader
    {
        private readonly ImmutableArray<MemoryReadResult> reads;
        private int index;

        internal CountedMethodEvidenceReader(
            string sourceId,
            ImmutableArray<MemoryReadResult> reads)
        {
            SourceId = sourceId;
            this.reads = reads;
            MaximumReadLength = Math.Max(1, reads.Max(static read => read.RequestedLength));
        }

        public int PointerSize => sizeof(ulong);

        public int MaximumReadLength { get; }

        public string SourceId { get; }

        internal bool ConsumedAll => index == reads.Length;

        public MemoryReadResult Read(ulong address, int length)
        {
            if ((uint)index >= (uint)reads.Length)
            {
                throw new InvalidDataException("Physical method replay requested an uncounted range.");
            }

            var read = reads[index];
            if (read.Address != address ||
                read.RequestedLength != length ||
                read.Status != MemoryReadStatus.Exact ||
                !string.Equals(read.SourceId, SourceId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Physical method replay requested a different counted range.");
            }

            index++;
            return read;
        }
    }
}
