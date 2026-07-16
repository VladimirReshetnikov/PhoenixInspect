using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Host.Dump.ClrMD;

public sealed partial class ClrmdDumpSession
{
    private const int TypeDefinitionTokenType = 0x02000000;
    private const int FieldDefinitionTokenType = 0x04000000;
    private const int MethodDefinitionTokenType = 0x06000000;
    private const int PropertyDefinitionTokenType = 0x17000000;
    private const int RowIdMask = 0x00FFFFFF;
    private const byte LoadArgumentZero = 0x02;
    private const byte LoadInstanceField = 0x7B;
    private const byte Return = 0x2A;
    private const int MaximumTerminalMethodSemanticsCount = 16;
    private const int MaximumDeclaredTypeNestingDepth = 128;
    private static readonly ImmutableArray<EvaluationDeterministicBound> TerminalFieldTraversalBounds =
        ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.terminal-fields.traversed",
            MaximumRuntimeInstanceFieldCount));
    private static readonly ImmutableArray<EvaluationDeterministicBound> TerminalPropertyTraversalBounds =
        ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.terminal-properties.traversed",
            MaximumRuntimeInstanceFieldCount));
    /// <summary>
    /// Certifies one directly declared terminal field or trivial field-backed property reached through an outer
    /// directly declared reference field.
    /// </summary>
    /// <param name="root">The exact host-selected root object from this session.</param>
    /// <param name="referenceFieldName">The ordinal name of the directly declared outer reference FieldDef.</param>
    /// <param name="terminalMemberName">The ordinal name of the directly declared terminal field or property.</param>
    /// <returns>
    /// An exact immutable declaration/storage certificate, or a typed partial, unavailable, conflicting, or invalid
    /// result retaining every counted read completed before failure.
    /// </returns>
    /// <remarks>
    /// This draft W6.3 operation never reads the value of the outer reference, validates a referenced object, reads
    /// terminal storage, or invokes a getter. It reads the complete module metadata image and, only for a property,
    /// the complete physical getter body. Member and catalog limits are deterministic and reported only when reached.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Either member name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either member name exceeds the deterministic name bound.</exception>
    public ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> CertifyDeclaredDataMember(
        ClrmdHeapObjectInfo root,
        string referenceFieldName,
        string terminalMemberName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(root);
        ValidateDeclaredMemberName(referenceFieldName, nameof(referenceFieldName));
        ValidateDeclaredMemberName(terminalMemberName, nameof(terminalMemberName));

        if (root.Snapshot != Snapshot)
        {
            return Failure(ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.SnapshotMismatch);
        }

        var outerResult = GetInstanceField(root, referenceFieldName);
        if (outerResult.Status != ClrmdEvidenceStatus.Exact || outerResult.Value is null)
        {
            return Failure(
                outerResult.Status,
                outerResult.Issue,
                appliedBounds: outerResult.AppliedBounds);
        }

        var outerField = outerResult.Value;
        if (!outerField.IsObjectReference)
        {
            return Failure(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.TypeMismatch,
                appliedBounds: outerResult.AppliedBounds);
        }

        if (!_runtimeModules.TryGetValue(root.Module.Identity, out var runtimeModule))
        {
            return Failure(ClrmdEvidenceStatus.Unavailable, ClrmdValueIssue.ModuleUnavailable);
        }

        var metadataResult = ReadCompleteMetadata(root.Module);
        if (metadataResult.Status != ClrmdEvidenceStatus.Exact || metadataResult.Value is null)
        {
            return Failure(
                metadataResult.Status,
                metadataResult.Issue,
                evidence: metadataResult.Evidence,
                appliedBounds: outerResult.AppliedBounds);
        }

        var metadataImage = metadataResult.Value;
        var evidence = metadataResult.Evidence;
        var appliedBounds = outerResult.AppliedBounds;
        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage.Bytes);
            var reader = provider.GetMetadataReader();
            var rootTypeHandle = RequireTypeDefinition(reader, root.TypeMetadataToken);
            if (rootTypeHandle.IsNil)
            {
                return Failure(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, evidence, appliedBounds);
            }

            var rootDefinition = reader.GetTypeDefinition(rootTypeHandle);
            var metadataRootName = GetFullTypeName(reader, rootTypeHandle);
            if (!string.Equals(metadataRootName, root.TypeName, StringComparison.Ordinal))
            {
                return Failure(ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.TypeMismatch, evidence, appliedBounds);
            }

            var outerHandle = RequireFieldDefinition(reader, outerField.MetadataToken);
            if (outerHandle.IsNil)
            {
                return Failure(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, evidence, appliedBounds);
            }

            var outerDefinition = reader.GetFieldDefinition(outerHandle);
            if (outerDefinition.GetDeclaringType() != rootTypeHandle ||
                !string.Equals(reader.GetString(outerDefinition.Name), referenceFieldName, StringComparison.Ordinal))
            {
                return Failure(ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.TypeMismatch, evidence, appliedBounds);
            }

            var outerNameCount = rootDefinition.GetFields().Count(handle =>
                string.Equals(
                    reader.GetString(reader.GetFieldDefinition(handle).Name),
                    referenceFieldName,
                    StringComparison.Ordinal));
            if (outerNameCount != 1)
            {
                return Failure(
                    outerNameCount == 0 ? ClrmdEvidenceStatus.Unavailable : ClrmdEvidenceStatus.Conflict,
                    outerNameCount == 0 ? ClrmdValueIssue.FieldUnavailable : ClrmdValueIssue.AmbiguousMatch,
                    evidence,
                    appliedBounds);
            }

            if (!IsOrdinaryInstanceField(outerDefinition.Attributes))
            {
                return Failure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var signatureProvider = new DeclaredMemberSignatureProvider(reader);
            var outerType = outerDefinition.DecodeSignature(signatureProvider, genericContext: null);
            if (outerType.Kind != DeclaredSignatureKind.TypeDefinition || outerType.Handle.Kind != HandleKind.TypeDefinition)
            {
                return Failure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var targetTypeHandle = (TypeDefinitionHandle)outerType.Handle;
            var targetDefinition = reader.GetTypeDefinition(targetTypeHandle);
            if (targetDefinition.GetGenericParameters().Count != 0)
            {
                return Failure(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MemberShapeUnsupported,
                    evidence,
                    appliedBounds);
            }

            var targetTypeToken = MetadataTokens.GetToken(targetTypeHandle);
            var targetTypeName = GetFullTypeName(reader, targetTypeHandle);
            var runtimeTarget = runtimeModule.GetTypeByName(targetTypeName);
            if (runtimeTarget is null)
            {
                return Failure(ClrmdEvidenceStatus.Unavailable, ClrmdValueIssue.TypeUnavailable, evidence, appliedBounds);
            }

            if (runtimeTarget.MetadataToken != targetTypeToken ||
                !string.Equals(runtimeTarget.Name, targetTypeName, StringComparison.Ordinal))
            {
                return Failure(ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.TypeMismatch, evidence, appliedBounds);
            }

            var terminalProjection = SelectTerminalMetadata(
                reader,
                signatureProvider,
                targetTypeHandle,
                terminalMemberName,
                MaximumRuntimeInstanceFieldCount,
                MaximumRuntimeInstanceFieldCount,
                MaximumTerminalMethodSemanticsCount);
            appliedBounds = MergeAppliedBounds(appliedBounds, terminalProjection.AppliedBounds);
            if (terminalProjection.Status != ClrmdEvidenceStatus.Exact || terminalProjection.Value is null)
            {
                return Failure(
                    terminalProjection.Status,
                    terminalProjection.Issue,
                    evidence,
                    appliedBounds);
            }

            var terminal = terminalProjection.Value;
            ClrmdMethodBodyInfo? getterBody = null;
            if (terminal.GetterToken is not null)
            {
                var bodyResult = ReadMethodBody(root.Module, terminal.GetterToken.Value);
                evidence = evidence.AddRange(bodyResult.Evidence);
                appliedBounds = MergeAppliedBounds(appliedBounds, bodyResult.AppliedBounds);
                if (bodyResult.Status != ClrmdEvidenceStatus.Exact || bodyResult.Value is null)
                {
                    return Failure(bodyResult.Status, bodyResult.Issue, evidence, appliedBounds);
                }

                getterBody = bodyResult.Value;
                if (getterBody.MetadataToken != terminal.GetterToken.Value ||
                    !TryReadCertifiedBackingFieldToken(getterBody, out var backingFieldToken))
                {
                    return Failure(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.MemberShapeUnsupported,
                        evidence,
                        appliedBounds);
                }

                var backingHandle = RequireFieldDefinition(reader, backingFieldToken);
                if (backingHandle.IsNil)
                {
                    return Failure(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, evidence, appliedBounds);
                }

                var backingField = reader.GetFieldDefinition(backingHandle);
                var backingSignature = backingField.DecodeSignature(signatureProvider, genericContext: null);
                if (backingField.GetDeclaringType() != targetTypeHandle ||
                    !IsOrdinaryInstanceField(backingField.Attributes) ||
                    backingSignature != terminal.TerminalTypeSignature ||
                    !TryGetDecoder(backingSignature, out var backingDecoder) ||
                    backingDecoder != terminal.Decoder)
                {
                    return Failure(
                        ClrmdEvidenceStatus.Unavailable,
                        ClrmdValueIssue.MemberShapeUnsupported,
                        evidence,
                        appliedBounds);
                }

                terminal = terminal with
                {
                    StorageFieldToken = backingFieldToken,
                    StorageSignature = ImmutableArray.Create(reader.GetBlobBytes(backingField.Signature)),
                };
            }

            var storageResult = SelectRelativeStorage(runtimeTarget, terminal);
            appliedBounds = MergeAppliedBounds(appliedBounds, storageResult.AppliedBounds);
            if (storageResult.Status != ClrmdEvidenceStatus.Exact || storageResult.Value is null)
            {
                return Failure(storageResult.Status, storageResult.Issue, evidence, appliedBounds);
            }

            var moduleContent = ModuleContentIdentity.FromMetadata(
                reader.GetGuid(reader.GetModuleDefinition().Mvid),
                metadataImage.Bytes.AsSpan());
            var declaredType = new ClrmdDeclaredTypeInfo(
                root.Module.Identity,
                moduleContent,
                targetTypeToken,
                targetTypeName);
            var property = terminal.PropertyToken is null
                ? null
                : new ClrmdDataPropertyCertificate(
                    terminal.PropertyToken.Value,
                    terminal.PublicSignature,
                    terminal.GetterToken!.Value,
                    terminal.GetterSignature,
                    terminal.SetterToken,
                    terminal.OtherAccessorTokens,
                    getterBody!);
            var certificate = new ClrmdDeclaredDataMemberCertificate(
                root.TypeName,
                root.TypeMetadataToken,
                outerField,
                ImmutableArray.Create(reader.GetBlobBytes(outerDefinition.Signature)),
                declaredType,
                terminalMemberName,
                terminal.PropertyToken is null
                    ? ClrmdTerminalMemberKind.Field
                    : ClrmdTerminalMemberKind.CertifiedProperty,
                terminal.PublicMemberToken,
                terminal.Decoder,
                storageResult.Value,
                property);
            return ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                certificate,
                evidence,
                appliedBounds);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return Failure(ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, evidence, appliedBounds);
        }
    }

    /// <summary>
    /// Reads one same-module MethodDef body by exact metadata token without requiring a materialized runtime method.
    /// </summary>
    /// <param name="module">The snapshot-scoped loaded module containing the MethodDef.</param>
    /// <param name="methodMetadataToken">A non-nil MethodDef token from the module's complete metadata image.</param>
    /// <returns>
    /// An exact counted physical body, or a typed unavailable, partial, conflicting, or invalid evidence result.
    /// </returns>
    /// <remarks>
    /// This draft overload supports W6 property certification for getters that have never executed and therefore may
    /// have no ClrMD runtime-method projection. It performs no name lookup or invocation; identity comes solely from
    /// the selected module, complete counted metadata, MethodDef token, RVA, and mapped target bytes.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
    public ClrmdEvidenceResult<ClrmdMethodBodyInfo> ReadMethodBody(
        ClrmdModuleInfo module,
        int methodMetadataToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(module);
        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_runtimeModules.TryGetValue(module.Identity, out var runtimeModule))
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        var metadataResult = ReadCompleteMetadata(module);
        if (metadataResult.Status != ClrmdEvidenceStatus.Exact || metadataResult.Value is null)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                metadataResult.Status,
                metadataResult.Issue,
                evidence: metadataResult.Evidence);
        }

        var metadataEvidence = metadataResult.Evidence;
        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataResult.Value.Bytes);
            var reader = provider.GetMetadataReader();
            var rowId = methodMetadataToken & RowIdMask;
            if ((methodMetadataToken & unchecked((int)0xFF000000)) != MethodDefinitionTokenType ||
                rowId == 0 || rowId > reader.MethodDefinitions.Count)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: metadataEvidence);
            }

            var definition = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(rowId));
            var implementationAttributes = definition.ImplAttributes;
            if ((implementationAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL ||
                (implementationAttributes & MethodImplAttributes.ManagedMask) != MethodImplAttributes.Managed)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyUnavailable,
                    evidence: metadataEvidence);
            }

            var relativeVirtualAddress = definition.RelativeVirtualAddress;
            if (relativeVirtualAddress == 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyUnavailable,
                    evidence: metadataEvidence);
            }

            if (relativeVirtualAddress < 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: metadataEvidence);
            }

            if (runtimeModule.IsDynamic || !runtimeModule.IsPEFile ||
                runtimeModule.Layout is not (ModuleLayout.Mapped or ModuleLayout.Loaded) ||
                module.Identity.ImageBase == 0 || module.Identity.ImageSize == 0)
            {
                return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.MethodBodyLayoutUnsupported,
                    evidence: metadataEvidence);
            }

            return ClrmdMethodBodyParser.Read(
                Memory,
                methodMetadataToken,
                relativeVirtualAddress,
                module.Identity.ImageBase,
                module.Identity.ImageSize,
                reader.GetTableRowCount(TableIndex.StandAloneSig),
                metadataEvidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return ClrmdEvidenceResult<ClrmdMethodBodyInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: metadataEvidence);
        }
    }

    private ClrmdEvidenceResult<CompleteMetadataImage> ReadCompleteMetadata(ClrmdModuleInfo module)
    {
        if (module.Identity.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (module.MetadataAddress == 0 || module.MetadataLength == 0)
        {
            return ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MetadataUnavailable);
        }

        if (module.MetadataLength > (ulong)Memory.MaximumReadLength)
        {
            return ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.LimitExceeded);
        }

        if (module.MetadataAddress > ulong.MaxValue - (module.MetadataLength - 1))
        {
            return ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var read = Memory.Read(module.MetadataAddress, checked((int)module.MetadataLength));
        var evidence = ImmutableArray.Create(read);
        return read.Status == MemoryReadStatus.Exact
            ? ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                new CompleteMetadataImage(read.Bytes),
                evidence)
            : ClrmdEvidenceResult<CompleteMetadataImage>.Create(
                ToEvidenceStatus(read.Status),
                ClrmdValueIssue.MemoryUnavailable,
                evidence: evidence);
    }

    private static ClrmdEvidenceResult<TerminalMetadataProjection> SelectTerminalMetadata(
        MetadataReader reader,
        DeclaredMemberSignatureProvider signatureProvider,
        TypeDefinitionHandle targetTypeHandle,
        string terminalMemberName,
        int maximumFieldCount,
        int maximumPropertyCount,
        int maximumMethodSemanticsCount)
    {
        var type = reader.GetTypeDefinition(targetTypeHandle);
        var fields = type.GetFields().ToImmutableArray();
        var properties = type.GetProperties().ToImmutableArray();
        var fieldBounds = ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.terminal-fields.traversed",
            maximumFieldCount));
        var propertyBounds = ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.terminal-properties.traversed",
            maximumPropertyCount));
        var bounds = MergeAppliedBounds(fieldBounds, propertyBounds);
        if (fields.Length > maximumFieldCount || properties.Length > maximumPropertyCount)
        {
            return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                appliedBounds: bounds);
        }

        var matchingFields = fields.Where(handle => string.Equals(
            reader.GetString(reader.GetFieldDefinition(handle).Name),
            terminalMemberName,
            StringComparison.Ordinal)).ToArray();
        var matchingProperties = properties.Where(handle => string.Equals(
            reader.GetString(reader.GetPropertyDefinition(handle).Name),
            terminalMemberName,
            StringComparison.Ordinal)).ToArray();
        if (matchingFields.Length + matchingProperties.Length == 0)
        {
            return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable,
                appliedBounds: bounds);
        }

        if (matchingFields.Length + matchingProperties.Length != 1)
        {
            return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch,
                appliedBounds: bounds);
        }

        if (matchingFields.Length == 1)
        {
            var fieldHandle = matchingFields[0];
            var field = reader.GetFieldDefinition(fieldHandle);
            if (!IsOrdinaryInstanceField(field.Attributes))
            {
                return UnsupportedTerminal(bounds);
            }

            var signature = field.DecodeSignature(signatureProvider, genericContext: null);
            if (!TryGetDecoder(signature, out var decoder))
            {
                return UnsupportedTerminal(bounds);
            }

            var token = MetadataTokens.GetToken(fieldHandle);
            return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                new TerminalMetadataProjection(
                    token,
                    PropertyToken: null,
                    GetterToken: null,
                    GetterName: null,
                    token,
                    decoder,
                    signature,
                    ImmutableArray.Create(reader.GetBlobBytes(field.Signature)),
                    ImmutableArray<byte>.Empty,
                    ImmutableArray.Create(reader.GetBlobBytes(field.Signature))),
                appliedBounds: bounds);
        }

        var propertyHandle = matchingProperties[0];
        var property = reader.GetPropertyDefinition(propertyHandle);
        var propertySignature = property.DecodeSignature(signatureProvider, genericContext: null);
        if (!propertySignature.Header.IsInstance ||
            propertySignature.Header.IsGeneric ||
            propertySignature.GenericParameterCount != 0 ||
            propertySignature.ParameterTypes.Length != 0 ||
            !TryGetDecoder(propertySignature.ReturnType, out var propertyDecoder))
        {
            return UnsupportedTerminal(bounds);
        }

        var accessors = property.GetAccessors();
        if (accessors.Getter.IsNil)
        {
            return UnsupportedTerminal(bounds);
        }

        var semanticsBounds = ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.terminal-method-semantics.traversed",
            maximumMethodSemanticsCount));
        bounds = MergeAppliedBounds(bounds, semanticsBounds);
        var semanticsCount = 1 + (accessors.Setter.IsNil ? 0 : 1) + accessors.Others.Count();
        if (semanticsCount > maximumMethodSemanticsCount)
        {
            return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                appliedBounds: bounds);
        }

        var getter = reader.GetMethodDefinition(accessors.Getter);
        if (getter.GetDeclaringType() != targetTypeHandle ||
            (getter.Attributes & MethodAttributes.Static) != 0 ||
            (getter.Attributes & MethodAttributes.Virtual) != 0 ||
            (getter.Attributes & MethodAttributes.Abstract) != 0 ||
            getter.GetGenericParameters().Count != 0 ||
            (getter.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL ||
            (getter.ImplAttributes & MethodImplAttributes.ManagedMask) != MethodImplAttributes.Managed)
        {
            return UnsupportedTerminal(bounds);
        }

        var getterSignature = getter.DecodeSignature(signatureProvider, genericContext: null);
        if (!getterSignature.Header.IsInstance ||
            getterSignature.Header.IsGeneric ||
            getterSignature.GenericParameterCount != 0 ||
            getterSignature.ParameterTypes.Length != 0 ||
            getterSignature.ReturnType != propertySignature.ReturnType)
        {
            return UnsupportedTerminal(bounds);
        }

        var getterToken = MetadataTokens.GetToken(accessors.Getter);
        var propertyToken = MetadataTokens.GetToken(propertyHandle);
        return ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            new TerminalMetadataProjection(
                propertyToken,
                propertyToken,
                getterToken,
                reader.GetString(getter.Name),
                StorageFieldToken: 0,
                propertyDecoder,
                propertySignature.ReturnType,
                ImmutableArray.Create(reader.GetBlobBytes(property.Signature)),
                ImmutableArray.Create(reader.GetBlobBytes(getter.Signature)),
                ImmutableArray<byte>.Empty,
                accessors.Setter.IsNil ? null : MetadataTokens.GetToken(accessors.Setter),
                accessors.Others.Select(static handle => MetadataTokens.GetToken((EntityHandle)handle)).ToImmutableArray()),
            appliedBounds: bounds);
    }

    internal static ClrmdTerminalCatalogInspection InspectTerminalCatalog(
        MetadataReader reader,
        int targetTypeToken,
        string terminalMemberName,
        int maximumFieldCount,
        int maximumPropertyCount,
        int maximumMethodSemanticsCount)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (string.IsNullOrWhiteSpace(terminalMemberName))
        {
            throw new ArgumentException("A terminal member name is required.", nameof(terminalMemberName));
        }

        if (maximumFieldCount < 0 || maximumPropertyCount < 0 || maximumMethodSemanticsCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFieldCount),
                "Terminal catalog and semantics bounds cannot be negative.");
        }

        try
        {
            var typeHandle = RequireTypeDefinition(reader, targetTypeToken);
            if (typeHandle.IsNil)
            {
                return new ClrmdTerminalCatalogInspection(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    ImmutableArray<EvaluationDeterministicBound>.Empty,
                    PublicMemberToken: null);
            }

            var result = SelectTerminalMetadata(
                reader,
                new DeclaredMemberSignatureProvider(reader),
                typeHandle,
                terminalMemberName,
                maximumFieldCount,
                maximumPropertyCount,
                maximumMethodSemanticsCount);
            return new ClrmdTerminalCatalogInspection(
                result.Status,
                result.Issue,
                result.AppliedBounds,
                result.Value?.PublicMemberToken);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return new ClrmdTerminalCatalogInspection(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                ImmutableArray<EvaluationDeterministicBound>.Empty,
                PublicMemberToken: null);
        }
    }

    private static ClrmdEvidenceResult<ClrmdRelativeFieldInfo> SelectRelativeStorage(
        ClrType runtimeTarget,
        TerminalMetadataProjection terminal)
    {
        var storageToken = terminal.StorageFieldToken;
        if (storageToken == 0 && terminal.GetterToken is not null)
        {
            return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        var fields = runtimeTarget.Fields;
        if (fields.IsDefault)
        {
            return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        if (fields.Length > MaximumRuntimeInstanceFieldCount)
        {
            return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        var matches = fields.Where(field =>
            field.Token == storageToken &&
            field.ContainingType.MetadataToken == runtimeTarget.MetadataToken).ToArray();
        if (matches.Length != 1)
        {
            return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                matches.Length == 0 ? ClrmdEvidenceStatus.Unavailable : ClrmdEvidenceStatus.Conflict,
                matches.Length == 0 ? ClrmdValueIssue.FieldUnavailable : ClrmdValueIssue.AmbiguousMatch,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        var field = matches[0];
        if (!RuntimeFieldMatchesDecoder(field, terminal.Decoder))
        {
            return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.TypeMismatch,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        ClrmdRelativeNullableInt32Layout? nullableLayout = null;
        if (terminal.Decoder == ClrmdTerminalDecoderKind.NullableInt32)
        {
            var nullableResult = SelectRelativeNullableInt32Layout(field);
            if (nullableResult.Status != ClrmdEvidenceStatus.Exact || nullableResult.Value is null)
            {
                return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
                    nullableResult.Status,
                    nullableResult.Issue,
                    appliedBounds: nullableResult.AppliedBounds);
            }

            nullableLayout = nullableResult.Value;
        }

        var offset = checked(field.Offset + IntPtr.Size);
        return ClrmdEvidenceResult<ClrmdRelativeFieldInfo>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            new ClrmdRelativeFieldInfo(
                field.Token,
                field.Name ?? string.Empty,
                offset,
                field.Size,
                field.IsObjectReference,
                field.ElementType.ToString(),
                field.Type?.Name,
                terminal.StorageSignature,
                nullableLayout),
            appliedBounds: TerminalFieldTraversalBounds);
    }

    private static ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout> SelectRelativeNullableInt32Layout(
        ClrInstanceField outerField)
    {
        var fields = outerField.Type?.Fields ?? default;
        if (fields.IsDefault)
        {
            return ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        if (fields.Length > MaximumRuntimeInstanceFieldCount)
        {
            return ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        var hasValueMatches = fields.Where(field =>
            string.Equals(field.Name, "hasValue", StringComparison.Ordinal)).ToArray();
        var valueMatches = fields.Where(field =>
            string.Equals(field.Name, "value", StringComparison.Ordinal)).ToArray();
        if (hasValueMatches.Length != 1 || valueMatches.Length != 1)
        {
            var conflict = hasValueMatches.Length > 1 || valueMatches.Length > 1;
            return ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout>.Create(
                conflict ? ClrmdEvidenceStatus.Conflict : ClrmdEvidenceStatus.Unavailable,
                conflict ? ClrmdValueIssue.AmbiguousMatch : ClrmdValueIssue.FieldUnavailable,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        var hasValue = hasValueMatches[0];
        var value = valueMatches[0];
        if (hasValue.IsObjectReference ||
            hasValue.ElementType != ClrElementType.Boolean ||
            value.IsObjectReference ||
            value.ElementType != ClrElementType.Int32 ||
            value.Size != sizeof(int) ||
            hasValue.Offset < 0 ||
            value.Offset < 0 ||
            !ClrmdNullableInt32FieldLayout.HasValidDistinctStorage(
                outerAddress: 0,
                outerField.Size,
                hasValue.Token,
                (ulong)hasValue.Offset,
                hasValue.Size,
                value.Token,
                (ulong)value.Offset,
                value.Size))
        {
            return ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                appliedBounds: TerminalFieldTraversalBounds);
        }

        return ClrmdEvidenceResult<ClrmdRelativeNullableInt32Layout>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            new ClrmdRelativeNullableInt32Layout(
                hasValue.Token,
                hasValue.Offset,
                hasValue.Size,
                value.Token,
                value.Offset,
                value.Size),
            appliedBounds: TerminalFieldTraversalBounds);
    }

    private static bool TryReadCertifiedBackingFieldToken(
        ClrmdMethodBodyInfo body,
        out int storageFieldToken)
    {
        storageFieldToken = 0;
        var code = body.Body.CodeBytes.AsSpan();
        if (body.HeaderKind != ClrmdMethodHeaderKind.Tiny ||
            body.MaxStack != 8 ||
            body.LocalVariablesInitialized ||
            body.LocalSignatureToken != 0 ||
            body.ExceptionRegionCount != 0 ||
            !body.ExtraSectionEvidence.IsEmpty ||
            code.Length != 7 ||
            code[0] != LoadArgumentZero ||
            code[1] != LoadInstanceField ||
            code[6] != Return)
        {
            return false;
        }

        var handle = MetadataTokens.EntityHandle(
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(code.Slice(2, sizeof(int))));
        if (handle.Kind != HandleKind.FieldDefinition)
        {
            return false;
        }

        storageFieldToken = MetadataTokens.GetToken(handle);
        return true;
    }

    private static bool RuntimeFieldMatchesDecoder(ClrInstanceField field, ClrmdTerminalDecoderKind decoder) =>
        decoder switch
        {
            ClrmdTerminalDecoderKind.String =>
                field.ElementType == ClrElementType.String && field.IsObjectReference,
            ClrmdTerminalDecoderKind.Int32 =>
                field.ElementType == ClrElementType.Int32 && !field.IsObjectReference && field.Size == sizeof(int),
            ClrmdTerminalDecoderKind.NullableInt32 =>
                field.ElementType == ClrElementType.Struct &&
                field.Type?.IsValueType == true &&
                string.Equals(field.Type.Name, "System.Nullable<System.Int32>", StringComparison.Ordinal),
            _ => false,
        };

    private static bool IsOrdinaryInstanceField(FieldAttributes attributes) =>
        (attributes & (FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasFieldRVA)) == 0;

    private static bool TryGetDecoder(DeclaredSignature signature, out ClrmdTerminalDecoderKind decoder)
    {
        decoder = signature.Kind switch
        {
            DeclaredSignatureKind.String => ClrmdTerminalDecoderKind.String,
            DeclaredSignatureKind.Int32 => ClrmdTerminalDecoderKind.Int32,
            DeclaredSignatureKind.NullableInt32 => ClrmdTerminalDecoderKind.NullableInt32,
            _ => default,
        };
        return signature.Kind is DeclaredSignatureKind.String or
            DeclaredSignatureKind.Int32 or
            DeclaredSignatureKind.NullableInt32;
    }

    private static TypeDefinitionHandle RequireTypeDefinition(MetadataReader reader, int token)
    {
        var rowId = token & RowIdMask;
        return (token & unchecked((int)0xFF000000)) == TypeDefinitionTokenType &&
            rowId > 0 && rowId <= reader.TypeDefinitions.Count
            ? MetadataTokens.TypeDefinitionHandle(rowId)
            : default;
    }

    private static FieldDefinitionHandle RequireFieldDefinition(MetadataReader reader, int token)
    {
        var rowId = token & RowIdMask;
        return (token & unchecked((int)0xFF000000)) == FieldDefinitionTokenType &&
            rowId > 0 && rowId <= reader.FieldDefinitions.Count
            ? MetadataTokens.FieldDefinitionHandle(rowId)
            : default;
    }

    private static string GetFullTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var names = new Stack<string>();
        var visited = new HashSet<TypeDefinitionHandle>();
        var current = handle;
        string? @namespace = null;
        for (var depth = 0; depth < MaximumDeclaredTypeNestingDepth; depth++)
        {
            if (current.IsNil || !visited.Add(current))
            {
                throw new BadImageFormatException("The declared type nesting chain is cyclic or nil.");
            }

            var definition = reader.GetTypeDefinition(current);
            names.Push(reader.GetString(definition.Name));
            var declaring = definition.GetDeclaringType();
            if (declaring.IsNil)
            {
                @namespace = reader.GetString(definition.Namespace);
                var nestedName = string.Join('+', names);
                return string.IsNullOrEmpty(@namespace) ? nestedName : $"{@namespace}.{nestedName}";
            }

            current = declaring;
        }

        throw new BadImageFormatException("The declared type nesting chain exceeds the deterministic depth bound.");
    }

    private static void ValidateDeclaredMemberName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A member name is required.", parameterName);
        }

        if (value.Length > MaximumRuntimeFieldNameCharacters)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Member names are limited to {MaximumRuntimeFieldNameCharacters} characters.");
        }
    }

    private static ClrmdEvidenceResult<TerminalMetadataProjection> UnsupportedTerminal(
        ImmutableArray<EvaluationDeterministicBound> bounds) =>
        ClrmdEvidenceResult<TerminalMetadataProjection>.Create(
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.MemberShapeUnsupported,
            appliedBounds: bounds);

    private static ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate> Failure(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ImmutableArray<MemoryReadResult> evidence = default,
        ImmutableArray<EvaluationDeterministicBound> appliedBounds = default) =>
        ClrmdEvidenceResult<ClrmdDeclaredDataMemberCertificate>.Create(
            status,
            issue,
            evidence: evidence,
            appliedBounds: appliedBounds);

    private sealed record CompleteMetadataImage(ImmutableArray<byte> Bytes);

    internal sealed record ClrmdTerminalCatalogInspection(
        ClrmdEvidenceStatus Status,
        ClrmdValueIssue Issue,
        ImmutableArray<EvaluationDeterministicBound> AppliedBounds,
        int? PublicMemberToken);

    private sealed record TerminalMetadataProjection(
        int PublicMemberToken,
        int? PropertyToken,
        int? GetterToken,
        string? GetterName,
        int StorageFieldToken,
        ClrmdTerminalDecoderKind Decoder,
        DeclaredSignature TerminalTypeSignature,
        ImmutableArray<byte> PublicSignature,
        ImmutableArray<byte> GetterSignature,
        ImmutableArray<byte> StorageSignature,
        int? SetterToken = null,
        ImmutableArray<int> OtherAccessorTokens = default);

    private enum DeclaredSignatureKind
    {
        Unsupported,
        Int32,
        String,
        TypeDefinition,
        TypeReference,
        NullableInt32,
    }

    private readonly record struct DeclaredSignature(DeclaredSignatureKind Kind, EntityHandle Handle)
    {
        internal static DeclaredSignature Unsupported => new(DeclaredSignatureKind.Unsupported, default);
    }

    private sealed class DeclaredMemberSignatureProvider : ISignatureTypeProvider<DeclaredSignature, object?>
    {
        private readonly MetadataReader _reader;

        internal DeclaredMemberSignatureProvider(MetadataReader reader)
        {
            _reader = reader;
        }

        public DeclaredSignature GetArrayType(DeclaredSignature elementType, ArrayShape shape) =>
            DeclaredSignature.Unsupported;

        public DeclaredSignature GetByReferenceType(DeclaredSignature elementType) => DeclaredSignature.Unsupported;

        public DeclaredSignature GetFunctionPointerType(MethodSignature<DeclaredSignature> signature) =>
            DeclaredSignature.Unsupported;

        public DeclaredSignature GetGenericInstantiation(
            DeclaredSignature genericType,
            ImmutableArray<DeclaredSignature> typeArguments) =>
            IsNullableDefinition(genericType) &&
            typeArguments.Length == 1 &&
            typeArguments[0].Kind == DeclaredSignatureKind.Int32
                ? new DeclaredSignature(DeclaredSignatureKind.NullableInt32, genericType.Handle)
                : DeclaredSignature.Unsupported;

        public DeclaredSignature GetGenericMethodParameter(object? genericContext, int index) =>
            DeclaredSignature.Unsupported;

        public DeclaredSignature GetGenericTypeParameter(object? genericContext, int index) =>
            DeclaredSignature.Unsupported;

        public DeclaredSignature GetModifiedType(
            DeclaredSignature modifier,
            DeclaredSignature unmodifiedType,
            bool isRequired) => DeclaredSignature.Unsupported;

        public DeclaredSignature GetPinnedType(DeclaredSignature elementType) => DeclaredSignature.Unsupported;

        public DeclaredSignature GetPointerType(DeclaredSignature elementType) => DeclaredSignature.Unsupported;

        public DeclaredSignature GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Int32 => new DeclaredSignature(DeclaredSignatureKind.Int32, default),
            PrimitiveTypeCode.String => new DeclaredSignature(DeclaredSignatureKind.String, default),
            _ => DeclaredSignature.Unsupported,
        };

        public DeclaredSignature GetSZArrayType(DeclaredSignature elementType) => DeclaredSignature.Unsupported;

        public DeclaredSignature GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) =>
            rawTypeKind == (byte)SignatureTypeKind.Class
                ? new DeclaredSignature(DeclaredSignatureKind.TypeDefinition, handle)
                : DeclaredSignature.Unsupported;

        public DeclaredSignature GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind) =>
            new(DeclaredSignatureKind.TypeReference, handle);

        public DeclaredSignature GetTypeFromSpecification(
            MetadataReader reader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        private bool IsNullableDefinition(DeclaredSignature signature)
        {
            if (signature.Kind != DeclaredSignatureKind.TypeReference ||
                signature.Handle.Kind != HandleKind.TypeReference)
            {
                return false;
            }

            var reference = _reader.GetTypeReference((TypeReferenceHandle)signature.Handle);
            return string.Equals(_reader.GetString(reference.Namespace), "System", StringComparison.Ordinal) &&
                string.Equals(_reader.GetString(reference.Name), "Nullable`1", StringComparison.Ordinal);
        }
    }
}
