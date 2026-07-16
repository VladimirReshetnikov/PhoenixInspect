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
/// Projects one exactly counted dump metadata image and a bounded method-body graph into immutable execution
/// descriptors.
/// </summary>
/// <remarks>
/// This resolver never opens a target-reported path or substitutes bytes from a local PE. The complete metadata
/// image, physical method header, IL code, and declared extra sections must all be exact reads from the same dump
/// snapshot. Metadata is reparsed from the retained immutable image for contextual field resolution, and each SRM
/// result is a deep immutable Core projection that outlives the temporary metadata reader.
/// </remarks>
public sealed class ClrmdDumpExecutionResolver : IResolutionServices
{
    /// <summary>Gets the maximum number of exact interpreted MethodDefs retained by one resolver.</summary>
    public const int MaximumInterpretedMethodCount = 64;

    private readonly ImmutableArray<byte> metadataImage;
    private readonly ImmutableSortedDictionary<MethodHandle, ResolvedMethodDefinition> methodDefinitions;
    private readonly ImmutableArray<MethodHandle> interpretedMethods;
    private readonly ResolvedMethodDefinition rootMethodDefinition;
    private readonly int metadataMethodDefinitionCount;
    private readonly ImmutableArray<int> fieldOperandTokens;
    private readonly bool fieldOperandScanComplete;

    private ClrmdDumpExecutionResolver(
        ClrmdModuleInfo module,
        ModuleContentIdentity contentIdentity,
        ModuleHandle moduleHandle,
        ImmutableArray<byte> metadataImage,
        ImmutableSortedDictionary<MethodHandle, ResolvedMethodDefinition> methodDefinitions,
        ResolvedMethodDefinition rootMethodDefinition,
        int metadataMethodDefinitionCount,
        ImmutableArray<int> fieldOperandTokens,
        bool fieldOperandScanComplete)
    {
        Module = module;
        ContentIdentity = contentIdentity;
        ModuleHandle = moduleHandle;
        this.metadataImage = metadataImage;
        this.methodDefinitions = methodDefinitions;
        interpretedMethods = methodDefinitions.Keys.ToImmutableArray();
        this.rootMethodDefinition = rootMethodDefinition;
        this.metadataMethodDefinitionCount = metadataMethodDefinitionCount;
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

    /// <summary>Gets the exact root MethodDef identity admitted by this resolver instance.</summary>
    public MethodHandle RootMethod => rootMethodDefinition.Method;

    /// <summary>Gets the immutable counted-body and metadata-signature projection for <see cref="RootMethod"/>.</summary>
    public ResolvedMethodDefinition RootMethodDefinition => rootMethodDefinition;

    /// <summary>
    /// Gets every exact interpreted MethodDef retained by the resolver in canonical module/token order.
    /// </summary>
    /// <remarks>
    /// The returned immutable value contains the root and every additional body admitted atomically by
    /// <see cref="CreateMethodGraph"/>. A body-free pure-model target is deliberately absent.
    /// </remarks>
    public ImmutableArray<MethodHandle> InterpretedMethods => interpretedMethods;

    /// <summary>Gets the exact root MethodDef identity retained for compatibility with the W3 single-body API.</summary>
    public MethodHandle Method => RootMethod;

    /// <summary>
    /// Gets the immutable root body and metadata-signature projection retained for compatibility with the W3
    /// single-body API.
    /// </summary>
    public ResolvedMethodDefinition MethodDefinition => RootMethodDefinition;

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

        return CreateMethodGraph(
            module,
            metadataIdentity,
            methodBody,
            ImmutableArray<ClrmdEvidenceResult<ClrmdMethodBodyInfo>>.Empty);
    }

    /// <summary>
    /// Creates one atomic dump-grounded interpreted-method graph from a complete counted metadata image and a
    /// bounded set of independently counted physical method bodies.
    /// </summary>
    /// <param name="module">The selected runtime module instance.</param>
    /// <param name="metadataIdentity">
    /// The exact result of <c>ClrmdDumpSession.ReadModuleContentIdentity(module)</c>, including its sole complete
    /// metadata-root read.
    /// </param>
    /// <param name="rootMethodBody">
    /// The exact counted root body. Its first evidence item must independently reproduce the same metadata-root read.
    /// </param>
    /// <param name="additionalInterpretedMethodBodies">
    /// An initialized bounded vector of additional exact bodies needed for interpretation. Order is not identity;
    /// successful resolvers canonicalize every retained MethodDef by module and token.
    /// </param>
    /// <returns>
    /// An immutable resolver containing the complete body set on success; otherwise a structured failure and no
    /// partially usable graph. A modeled target whose body must remain unread is omitted from the additional vector.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="module"/>, <paramref name="metadataIdentity"/>, or <paramref name="rootMethodBody"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static ResolutionResult<ClrmdDumpExecutionResolver> CreateMethodGraph(
        ClrmdModuleInfo module,
        ClrmdEvidenceResult<ModuleContentIdentity> metadataIdentity,
        ClrmdEvidenceResult<ClrmdMethodBodyInfo> rootMethodBody,
        ImmutableArray<ClrmdEvidenceResult<ClrmdMethodBodyInfo>> additionalInterpretedMethodBodies)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(metadataIdentity);
        ArgumentNullException.ThrowIfNull(rootMethodBody);

        if (additionalInterpretedMethodBodies.IsDefault)
        {
            return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METHOD_GRAPH_UNINITIALIZED",
                "The additional interpreted-method body vector must be initialized, even when empty.");
        }

        if (additionalInterpretedMethodBodies.Length >= MaximumInterpretedMethodCount)
        {
            return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METHOD_GRAPH_LIMIT",
                $"A dump execution resolver retains at most {MaximumInterpretedMethodCount} interpreted methods.");
        }

        if (metadataIdentity.Status != ClrmdEvidenceStatus.Exact || metadataIdentity.Value is null)
        {
            return EvidenceFailure<ClrmdDumpExecutionResolver>(
                metadataIdentity.Status,
                "DUMP_EXEC_METADATA_NOT_EXACT",
                "Execution projection requires one complete exact dump metadata image.");
        }

        if (rootMethodBody.Status != ClrmdEvidenceStatus.Exact || rootMethodBody.Value is null)
        {
            return EvidenceFailure<ClrmdDumpExecutionResolver>(
                rootMethodBody.Status,
                "DUMP_EXEC_METHOD_NOT_EXACT",
                "Execution projection requires a completely counted exact dump method body.");
        }

        var metadataValidation = ValidateMetadataEvidence(module, metadataIdentity);
        if (metadataValidation is { } metadataFailure)
        {
            return Failed<ClrmdDumpExecutionResolver>(metadataFailure);
        }

        var methodBodies = ImmutableArray.CreateBuilder<ClrmdEvidenceResult<ClrmdMethodBodyInfo>>(
            additionalInterpretedMethodBodies.Length + 1);
        methodBodies.Add(rootMethodBody);
        foreach (var additionalBody in additionalInterpretedMethodBodies)
        {
            if (additionalBody is null)
            {
                return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                    ResolutionFailureKind.Invalid,
                    "DUMP_EXEC_METHOD_GRAPH_BODY_MISSING",
                    "The additional interpreted-method body vector cannot contain null entries.");
            }

            if (additionalBody.Status != ClrmdEvidenceStatus.Exact || additionalBody.Value is null)
            {
                return EvidenceFailure<ClrmdDumpExecutionResolver>(
                    additionalBody.Status,
                    "DUMP_EXEC_METHOD_NOT_EXACT",
                    "Execution projection requires every interpreted method body to be completely counted and exact.");
            }

            methodBodies.Add(additionalBody);
        }

        foreach (var methodBody in methodBodies)
        {
            var countedBodyValidation = ValidateCountedMethodEvidence(
                module,
                metadataIdentity.Evidence[0],
                methodBody);
            if (countedBodyValidation is { } bodyFailure)
            {
                return Failed<ClrmdDumpExecutionResolver>(bodyFailure);
            }
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

            var moduleHandle = ModuleHandle.FromRuntimeEvidenceIdentity(
                recomputedIdentity,
                module.Identity.SourceId);
            var definitions = ImmutableSortedDictionary.CreateBuilder<MethodHandle, ResolvedMethodDefinition>(
                MethodHandleComparer.Instance);
            ResolvedMethodDefinition? rootDefinition = null;
            var standaloneSignatureRowCount = reader.GetTableRowCount(TableIndex.StandAloneSig);

            for (var index = 0; index < methodBodies.Count; index++)
            {
                var methodBody = methodBodies[index];
                var bodyInfo = methodBody.Value!;
                var rowId = bodyInfo.MetadataToken & 0x00FF_FFFF;
                if (!MethodHandle.IsValidMetadataToken(bodyInfo.MetadataToken) ||
                    rowId > reader.MethodDefinitions.Count)
                {
                    return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                        ResolutionFailureKind.Invalid,
                        "DUMP_EXEC_METHOD_TOKEN_INVALID",
                        "A counted method token does not identify a MethodDef in the counted metadata image.");
                }

                var methodHandle = new MethodHandle(moduleHandle, bodyInfo.MetadataToken);
                if (definitions.ContainsKey(methodHandle))
                {
                    return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                        ResolutionFailureKind.Conflict,
                        "DUMP_EXEC_METHOD_GRAPH_DUPLICATE",
                        "The counted method graph contains the same MethodDef body more than once.");
                }

                var metadataMethod = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rowId));
                if (metadataMethod.RelativeVirtualAddress != bodyInfo.RelativeVirtualAddress)
                {
                    return ResolutionResult<ClrmdDumpExecutionResolver>.Failed(
                        ResolutionFailureKind.Conflict,
                        "DUMP_EXEC_METHOD_RVA_CONFLICT",
                        "A counted method body and metadata image disagree on the MethodDef RVA.");
                }

                var physicalReplayFailure = ValidatePhysicalMethodReplay(
                    module,
                    methodBody,
                    standaloneSignatureRowCount);
                if (physicalReplayFailure is { } replayFailure)
                {
                    return Failed<ClrmdDumpExecutionResolver>(replayFailure);
                }

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

                definitions.Add(methodHandle, projection.Value);
                if (index == 0)
                {
                    rootDefinition = projection.Value;
                }
            }

            var rootBodyInfo = rootMethodBody.Value!;
            var scanComplete = TryCollectFieldOperandTokens(
                rootBodyInfo.Body.CodeBytes,
                out var fieldOperandTokens);
            return ResolutionResult<ClrmdDumpExecutionResolver>.Success(
                new ClrmdDumpExecutionResolver(
                    module,
                    recomputedIdentity,
                    moduleHandle,
                    metadataRead.Bytes,
                    definitions.ToImmutable(),
                    rootDefinition!,
                    reader.MethodDefinitions.Count,
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
        if (method == default || method.Module != ModuleHandle)
        {
            return ResolutionResult<ResolvedMethodDefinition>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METHOD_MISMATCH",
                "The requested MethodDef belongs to a different execution module.");
        }

        if (methodDefinitions.TryGetValue(method, out var definition))
        {
            return ResolutionResult<ResolvedMethodDefinition>.Success(definition);
        }

        var rowId = method.MetadataToken & 0x00FF_FFFF;
        if (!MethodHandle.IsValidMetadataToken(method.MetadataToken) || rowId > metadataMethodDefinitionCount)
        {
            return ResolutionResult<ResolvedMethodDefinition>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_METHOD_TOKEN_INVALID",
                "The requested handle does not identify a MethodDef in the counted metadata image.");
        }

        return ResolutionResult<ResolvedMethodDefinition>.Failed(
            ResolutionFailureKind.Unavailable,
            "DUMP_EXEC_METHOD_BODY_UNAVAILABLE",
            "The MethodDef exists in the counted metadata image, but its physical body was not admitted.");
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
        MethodHandle contextMethod,
        int metadataToken)
    {
        var contextFailure = ValidateCallerContext(
            contextMethod,
            "DUMP_EXEC_CALL_CONTEXT_MISMATCH",
            "DUMP_EXEC_CALL_CONTEXT_BODY_UNAVAILABLE",
            "direct-call target");
        if (contextFailure is not null)
        {
            return ResolutionResult<ResolvedMethodCallTarget>.Failed(
                contextFailure.Kind,
                contextFailure.Code,
                contextFailure.Message);
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage);
            return SrmMetadataProjection.ProjectMethodCallTarget(
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
            return ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_CALL_TARGET_METADATA_INVALID",
                "The counted dump metadata could not project the requested direct-call target.");
        }
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
    {
        var contextFailure = ValidateCallerContext(
            contextMethod,
            "DUMP_EXEC_FIELD_CONTEXT_MISMATCH",
            "DUMP_EXEC_FIELD_CONTEXT_BODY_UNAVAILABLE",
            "field");
        if (contextFailure is not null)
        {
            return ResolutionResult<ResolvedField>.Failed(
                contextFailure.Kind,
                contextFailure.Code,
                contextFailure.Message);
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

    private ResolutionFailure? ValidateCallerContext(
        MethodHandle contextMethod,
        string mismatchCode,
        string unavailableCode,
        string requestedDependency)
    {
        if (contextMethod == default || contextMethod.Module != ModuleHandle)
        {
            return new ResolutionFailure(
                ResolutionFailureKind.Invalid,
                mismatchCode,
                $"The {requestedDependency} request belongs to a different execution module.");
        }

        var rowId = contextMethod.MetadataToken & 0x00FF_FFFF;
        if (!MethodHandle.IsValidMetadataToken(contextMethod.MetadataToken) ||
            rowId > metadataMethodDefinitionCount)
        {
            return new ResolutionFailure(
                ResolutionFailureKind.Invalid,
                mismatchCode,
                $"The {requestedDependency} request uses a caller that is not a MethodDef in counted metadata.");
        }

        return methodDefinitions.ContainsKey(contextMethod)
            ? null
            : new ResolutionFailure(
                ResolutionFailureKind.Unavailable,
                unavailableCode,
                $"The {requestedDependency} request uses a caller whose physical body was not admitted.");
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
            owner.TypeMetadataToken != rootMethodDefinition.Signature.DeclaringType.MetadataToken ||
            !string.Equals(
                owner.TypeName,
                rootMethodDefinition.Signature.DeclaringType.DisplayName,
                StringComparison.Ordinal) ||
            field.DeclaringType != rootMethodDefinition.Signature.DeclaringType ||
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
                rootMethodDefinition,
                field,
                ownerSearch,
                owner,
                observation,
                value));
    }

    /// <summary>
    /// Correlates one unique exact strong-root object and one exact, partial, or unavailable counted Int32 field read
    /// with the root method's metadata field descriptor without inventing a value for missing bytes.
    /// </summary>
    /// <param name="ownerSearch">
    /// An exact bounded handle traversal with exactly one retained match from this resolver's snapshot and module.
    /// </param>
    /// <param name="fieldObservation">
    /// A runtime-selected Int32 field observation retaining one exact four-byte read, one non-empty partial prefix, or
    /// one unavailable empty read of the same four-byte range.
    /// </param>
    /// <returns>
    /// A frozen descriptor preserving the truthful read disposition and exposing a scalar only for exact evidence;
    /// otherwise a structured unavailable, invalid, or conflicting result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public ResolutionResult<ClrmdInt32FieldExecutionEvidence> CorrelateInt32FieldObservation(
        ClrmdHeapObjectSearchResult ownerSearch,
        ClrmdEvidenceResult<ClrmdInt32FieldObservation> fieldObservation)
    {
        ArgumentNullException.ThrowIfNull(ownerSearch);
        ArgumentNullException.ThrowIfNull(fieldObservation);

        if (ownerSearch.Status != ClrmdEvidenceStatus.Exact || ownerSearch.Issue != ClrmdValueIssue.None)
        {
            return EvidenceFailure<ClrmdInt32FieldExecutionEvidence>(
                ownerSearch.Status,
                "DUMP_EXEC_OWNER_NOT_EXACT",
                "Field correlation requires an exact, exhaustive owner selection.");
        }

        if (ownerSearch.Snapshot != Module.Identity.Snapshot)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_SNAPSHOT_CONFLICT",
                "The selected owner belongs to a different dump snapshot.");
        }

        if (ownerSearch.Matches.Length == 0)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Unavailable,
                "DUMP_EXEC_OWNER_UNAVAILABLE",
                "The exact owner search found no matching object.");
        }

        if (ownerSearch.Matches.Length != 1)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_AMBIGUOUS",
                "Field correlation requires one uniquely selected dump object.");
        }

        var owner = ownerSearch.Matches[0];
        if (owner.Snapshot != Module.Identity.Snapshot || owner.Module.Identity != Module.Identity)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_MODULE_CONFLICT",
                "The selected dump object does not belong to the resolver's runtime module instance.");
        }

        if (!string.Equals(ownerSearch.TypeNameSelector, owner.TypeName, StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_OWNER_TYPE_CONFLICT",
                "The exact owner selection predicate disagrees with the retained runtime type.");
        }

        if (ownerSearch.HandlesScanned <= 0 ||
            ownerSearch.MaximumHandlesScanned <= 0 ||
            ownerSearch.HandlesScanned > ownerSearch.MaximumHandlesScanned ||
            ownerSearch.MaximumMatches <= 0 ||
            ownerSearch.MatchLimitReached ||
            owner.Address == 0 ||
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
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_OWNER_EVIDENCE_INVALID",
                "The selected owner is not backed by complete counted reads from the resolver snapshot.");
        }

        if (fieldObservation.Status is ClrmdEvidenceStatus.Conflict or ClrmdEvidenceStatus.Invalid)
        {
            return EvidenceFailure<ClrmdInt32FieldExecutionEvidence>(
                fieldObservation.Status,
                "DUMP_EXEC_FIELD_STATUS_REJECTED",
                "Conflicting or invalid field evidence cannot be admitted into execution memory.");
        }

        if (fieldObservation.Status is not (
                ClrmdEvidenceStatus.Exact or
                ClrmdEvidenceStatus.Partial or
                ClrmdEvidenceStatus.Unavailable))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_STATUS_INVALID",
                "The field observation uses an undefined evidence status.");
        }

        var expectedIssue = fieldObservation.Status == ClrmdEvidenceStatus.Exact
            ? ClrmdValueIssue.None
            : ClrmdValueIssue.MemoryUnavailable;
        if (fieldObservation.Issue != expectedIssue || fieldObservation.Value is null)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_STATUS_TUPLE_INVALID",
                "The field status, issue, and retained runtime observation are inconsistent.");
        }

        var observation = fieldObservation.Value;
        var runtimeField = observation.Field;
        if (runtimeField.Snapshot != owner.Snapshot ||
            runtimeField.OwnerAddress != owner.Address ||
            runtimeField.OwnerMethodTable != owner.MethodTable ||
            !string.Equals(runtimeField.OwnerTypeName, owner.TypeName, StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_OWNER_CONFLICT",
                "The runtime field descriptor does not belong to the uniquely selected owner.");
        }

        if (!fieldOperandScanComplete)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Unsupported,
                "DUMP_EXEC_FIELD_OPERAND_SCAN_UNSUPPORTED",
                "The counted root method body is outside the closed instruction profile used for field correlation.");
        }

        if (!fieldOperandTokens.Contains(runtimeField.MetadataToken))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_OPERAND_CONFLICT",
                "The runtime field token is not an ldfld operand in the counted root method body.");
        }

        var projectedField = ResolveField(RootMethod, runtimeField.MetadataToken);
        if (!projectedField.IsSuccess)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                projectedField.Failure!.Kind,
                projectedField.Failure.Code,
                projectedField.Failure.Message);
        }

        var field = projectedField.Value;
        if (field.Handle.Module != ModuleHandle ||
            field.Handle.MetadataToken != runtimeField.MetadataToken ||
            !TypeSig.IsValidTypeDefinitionToken(owner.TypeMetadataToken) ||
            owner.TypeMetadataToken != rootMethodDefinition.Signature.DeclaringType.MetadataToken ||
            !string.Equals(
                owner.TypeName,
                rootMethodDefinition.Signature.DeclaringType.DisplayName,
                StringComparison.Ordinal) ||
            field.DeclaringType != rootMethodDefinition.Signature.DeclaringType ||
            field.FieldType != TypeSig.Int32 ||
            field.IsStatic ||
            field.IsLiteral ||
            field.HasRva)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_METADATA_TYPE_CONFLICT",
                "The projected FieldDef is not the root receiver's ordinary instance Int32 field.");
        }

        if (runtimeField.Size != sizeof(int) ||
            runtimeField.Address == 0 ||
            runtimeField.Address > ulong.MaxValue - (sizeof(int) - 1UL) ||
            runtimeField.IsObjectReference ||
            runtimeField.IsNullableInt32 ||
            !string.Equals(runtimeField.ElementType, "Int32", StringComparison.Ordinal) ||
            !string.Equals(runtimeField.FieldTypeName, "System.Int32", StringComparison.Ordinal))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Conflict,
                "DUMP_EXEC_FIELD_RUNTIME_TYPE_CONFLICT",
                "The runtime field descriptor is not a valid ordinary Int32 storage description.");
        }

        var memory = observation.Memory;
        if (fieldObservation.Evidence.Length != 1 ||
            !SameRead(fieldObservation.Evidence[0], memory) ||
            !string.Equals(memory.SourceId, Module.Identity.Snapshot.MemorySourceId, StringComparison.Ordinal) ||
            memory.Address != runtimeField.Address ||
            memory.RequestedLength != sizeof(int))
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_READ_TUPLE_INVALID",
                "The field observation is not backed by one matching four-byte read from the resolver snapshot.");
        }

        var expectedReadStatus = fieldObservation.Status switch
        {
            ClrmdEvidenceStatus.Exact => MemoryReadStatus.Exact,
            ClrmdEvidenceStatus.Partial => MemoryReadStatus.Partial,
            ClrmdEvidenceStatus.Unavailable => MemoryReadStatus.Unavailable,
            _ => throw new InvalidOperationException("The evidence status was validated above."),
        };
        var validValueTuple = fieldObservation.Status == ClrmdEvidenceStatus.Exact
            ? observation.Value is { } exactValue &&
                memory.Bytes.Length == sizeof(int) &&
                BinaryPrimitives.ReadInt32LittleEndian(memory.Bytes.AsSpan()) == exactValue
            : observation.Value is null &&
                (fieldObservation.Status != ClrmdEvidenceStatus.Partial || memory.Bytes.Length is >= 1 and < sizeof(int)) &&
                (fieldObservation.Status != ClrmdEvidenceStatus.Unavailable || memory.Bytes.IsEmpty);
        if (memory.Status != expectedReadStatus || !validValueTuple)
        {
            return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Failed(
                ResolutionFailureKind.Invalid,
                "DUMP_EXEC_FIELD_VALUE_EVIDENCE_INVALID",
                "The field status, observed byte prefix, and optional Int32 value are inconsistent.");
        }

        return ResolutionResult<ClrmdInt32FieldExecutionEvidence>.Success(
            new ClrmdInt32FieldExecutionEvidence(
                Module.Identity,
                rootMethodDefinition,
                field,
                ownerSearch,
                owner,
                observation,
                fieldObservation.Status,
                fieldObservation.Issue,
                observation.Value));
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
                case 0x28:
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

    private sealed class MethodHandleComparer : IComparer<MethodHandle>
    {
        internal static MethodHandleComparer Instance { get; } = new();

        public int Compare(MethodHandle left, MethodHandle right)
        {
            var comparison = left.Module.High.CompareTo(right.Module.High);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Module.Low.CompareTo(right.Module.Low);
            return comparison != 0 ? comparison : left.MetadataToken.CompareTo(right.MetadataToken);
        }
    }

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
