using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Core.Abstractions;
using MethodBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Metadata.SRM;

/// <summary>
/// Projects the closed W3 method, local, and field metadata profile from an already acquired SRM reader.
/// </summary>
/// <remarks>
/// This class has no PE, stream, or file-system dependency. A dump host can construct a
/// <see cref="MetadataReader"/> over exact counted metadata bytes, acquire the corresponding method body through
/// independently counted target-memory reads, and then call these operations with its snapshot-derived
/// <see cref="ModuleHandle"/>. Successful results contain immutable Core values and do not retain the reader or its
/// provider, so they remain usable after the metadata provider is disposed. Callers remain responsible for proving
/// that the reader, module handle, and supplied body describe the same evidence source.
/// </remarks>
public static class SrmMetadataProjection
{
    private const int TokenTypeMask = unchecked((int)0xFF000000);
    private const int RowIdMask = 0x00FFFFFF;
    private const int MethodDefinitionTokenType = 0x06000000;
    private const int MemberReferenceTokenType = 0x0A000000;
    private const int FieldDefinitionTokenType = 0x04000000;
    private const int StandaloneSignatureTokenType = 0x11000000;
    private const int MethodSpecificationTokenType = 0x2B000000;
    private const int MaximumExplicitParameterCount = 256;
    private const int MaximumLocalCount = 1_024;
    private const int MaximumTypeAncestryDepth = 128;

    /// <summary>
    /// Projects one complete MethodDef activation shape and combines it with an already acquired immutable body.
    /// </summary>
    /// <param name="metadataReader">The reader over the exact metadata image that defines the method.</param>
    /// <param name="module">The content- or snapshot-derived identity assigned to that metadata image.</param>
    /// <param name="method">The same-module MethodDef to project.</param>
    /// <param name="body">
    /// The immutable body acquired for <paramref name="method"/> from the same evidence source. The local-signature
    /// token preserved by this body selects the local vector decoded by this operation.
    /// </param>
    /// <returns>
    /// An atomic resolved definition, or a stable structured failure for conflicting identities, invalid metadata,
    /// non-ordinary managed-IL implementations, unsupported signatures, or bounded-profile limits.
    /// </returns>
    public static ResolutionResult<ResolvedMethodDefinition> ProjectMethodDefinition(
        MetadataReader metadataReader,
        ModuleHandle module,
        MethodHandle method,
        MethodBody body)
    {
        ArgumentNullException.ThrowIfNull(metadataReader);
        ArgumentNullException.ThrowIfNull(body);

        if (module == default)
        {
            return Failure<ResolvedMethodDefinition>(
                ResolutionFailureKind.Invalid,
                "META_MODULE_HANDLE_INVALID",
                "Metadata projection requires a non-default module identity.");
        }

        if (method.Module != module)
        {
            return Failure<ResolvedMethodDefinition>(
                ResolutionFailureKind.Conflict,
                "META_METHOD_MODULE_CONFLICT",
                "The requested method identity does not match the projected metadata module.");
        }

        if (!IsValidToken(method.MetadataToken, MethodDefinitionTokenType, metadataReader.MethodDefinitions.Count))
        {
            return Failure<ResolvedMethodDefinition>(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METHOD_TOKEN",
                "The supplied metadata token is not a valid MethodDef in this module.");
        }

        try
        {
            var callMetadataResult = ProjectMethodCallMetadata(metadataReader, module, method);
            if (!callMetadataResult.IsSuccess)
            {
                return Propagate<ResolvedMethodDefinition>(callMetadataResult.Failure!);
            }

            var callMetadata = callMetadataResult.Value;
            if (!IsOrdinaryManagedIlImplementation(
                    callMetadata.Attributes,
                    callMetadata.ImplementationAttributes))
            {
                return Failure<ResolvedMethodDefinition>(
                    ResolutionFailureKind.Unsupported,
                    "META_METHOD_IMPLEMENTATION_UNSUPPORTED",
                    "The selected method is not an ordinary managed-IL MethodDef.");
            }

            var localsResult = ProjectLocals(metadataReader, body.LocalSignatureToken);
            if (!localsResult.IsSuccess)
            {
                return Propagate<ResolvedMethodDefinition>(localsResult.Failure!);
            }

            var shape = new MethodSignatureShape(callMetadata.Signature, localsResult.Value);
            return ResolutionResult<ResolvedMethodDefinition>.Success(
                new ResolvedMethodDefinition(method, body, shape));
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<ResolvedMethodDefinition>(
                ResolutionFailureKind.Invalid,
                "META_METHOD_DEFINITION_INVALID",
                "The managed metadata contains an invalid method definition or signature.");
        }
    }

    /// <summary>
    /// Resolves one body-free InlineMethod operand to the closed direct-call MethodDef profile.
    /// </summary>
    /// <param name="metadataReader">The reader over the exact metadata image that defines both methods.</param>
    /// <param name="module">The content- or snapshot-derived identity assigned to that metadata image.</param>
    /// <param name="contextMethod">The same-module MethodDef whose IL contains the operand.</param>
    /// <param name="metadataToken">The raw four-byte InlineMethod operand.</param>
    /// <returns>
    /// An exact static managed-IL <c>Int32 (Int32, Int32)</c> MethodDef target, or a stable structured failure.
    /// In-range MemberRef and MethodSpec operands are reported as unsupported without being resolved; malformed,
    /// nil, out-of-range, and unrelated token kinds are invalid. Generic substitution, cross-module binding, and
    /// name lookup are never attempted.
    /// </returns>
    /// <remarks>
    /// This operation reads only metadata tables and signature blobs. It never inspects an RVA, PE method body,
    /// local signature, or local types, so an opaque model disposition can be selected before body acquisition.
    /// </remarks>
    public static ResolutionResult<ResolvedMethodCallTarget> ProjectMethodCallTarget(
        MetadataReader metadataReader,
        ModuleHandle module,
        MethodHandle contextMethod,
        int metadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataReader);

        if (module == default)
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Invalid,
                "META_MODULE_HANDLE_INVALID",
                "Metadata projection requires a non-default module identity.");
        }

        if (contextMethod.Module != module)
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Conflict,
                "META_METHOD_CONTEXT_MODULE_CONFLICT",
                "The containing method identity does not match the projected metadata module.");
        }

        if (!IsValidToken(
                contextMethod.MetadataToken,
                MethodDefinitionTokenType,
                metadataReader.MethodDefinitions.Count))
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Invalid,
                "META_INVALID_CONTEXT_METHOD_TOKEN",
                "The call context is not a valid MethodDef in this module.");
        }

        if (IsValidToken(metadataToken, MemberReferenceTokenType, metadataReader.MemberReferences.Count) ||
            IsValidToken(
                metadataToken,
                MethodSpecificationTokenType,
                metadataReader.GetTableRowCount(TableIndex.MethodSpec)))
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Unsupported,
                "META_CALL_TOKEN_KIND_UNSUPPORTED",
                "The direct-call profile does not resolve MemberRef or MethodSpec operands.");
        }

        if (!IsValidToken(metadataToken, MethodDefinitionTokenType, metadataReader.MethodDefinitions.Count))
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Invalid,
                "META_INVALID_CALL_METHOD_TOKEN",
                "The InlineMethod operand is not a valid same-module MethodDef token.");
        }

        try
        {
            var contextHandle = MetadataTokens.MethodDefinitionHandle(contextMethod.MetadataToken & RowIdMask);
            var contextDefinition = metadataReader.GetMethodDefinition(contextHandle);
            if (contextDefinition.GetGenericParameters().Count != 0 ||
                DeclaringTypeIsGeneric(metadataReader, contextDefinition.GetDeclaringType()))
            {
                return Failure<ResolvedMethodCallTarget>(
                    ResolutionFailureKind.Unsupported,
                    "META_GENERIC_METHOD_CONTEXT_UNSUPPORTED",
                    "Generic method or declaring-type contexts are outside the direct-call profile.");
            }

            var target = new MethodHandle(module, metadataToken);
            var callMetadataResult = ProjectMethodCallMetadata(metadataReader, module, target);
            if (!callMetadataResult.IsSuccess)
            {
                return Propagate<ResolvedMethodCallTarget>(callMetadataResult.Failure!);
            }

            var callMetadata = callMetadataResult.Value;
            var attributes = callMetadata.Attributes;
            var implementationAttributes = callMetadata.ImplementationAttributes;
            if ((attributes & MethodAttributes.Static) == 0)
            {
                return Failure<ResolvedMethodCallTarget>(
                    ResolutionFailureKind.Unsupported,
                    "META_CALL_TARGET_INSTANCE_UNSUPPORTED",
                    "The direct-call profile requires a static MethodDef target.");
            }

            if (!IsOrdinaryManagedIlImplementation(attributes, implementationAttributes))
            {
                return Failure<ResolvedMethodCallTarget>(
                    ResolutionFailureKind.Unsupported,
                    "META_CALL_TARGET_IMPLEMENTATION_UNSUPPORTED",
                    "The direct-call target is not an ordinary managed-IL MethodDef.");
            }

            var targetDefinitionHandle = MetadataTokens.MethodDefinitionHandle(metadataToken & RowIdMask);
            if (HasOptionalExplicitParameter(
                    metadataReader,
                    metadataReader.GetMethodDefinition(targetDefinitionHandle)))
            {
                return Failure<ResolvedMethodCallTarget>(
                    ResolutionFailureKind.Unsupported,
                    "META_OPTIONAL_PARAMETERS_UNSUPPORTED",
                    "Optional direct-call parameters are outside the closed MethodDef profile.");
            }

            var signature = callMetadata.Signature;
            if (signature.HasImplicitThis ||
                signature.HasExplicitThis ||
                signature.GenericParameterCount != 0 ||
                signature.CallingConvention != MethodCallingConventionKind.Default ||
                signature.ParameterTypes.Length != 2 ||
                signature.ParameterTypes.Any(static type => type != TypeSig.Int32) ||
                signature.ReturnType != TypeSig.Int32)
            {
                return Failure<ResolvedMethodCallTarget>(
                    ResolutionFailureKind.Unsupported,
                    "META_CALL_TARGET_SIGNATURE_UNSUPPORTED",
                    "The direct-call target must have the exact static Int32 (Int32, Int32) signature.");
            }

            return ResolutionResult<ResolvedMethodCallTarget>.Success(
                new ResolvedMethodCallTarget(target, signature));
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<ResolvedMethodCallTarget>(
                ResolutionFailureKind.Invalid,
                "META_CALL_TARGET_INVALID",
                "The managed metadata contains an invalid direct-call context, target, or signature.");
        }
    }

    /// <summary>
    /// Projects one same-module FieldDef operand in the context of the MethodDef that contains it.
    /// </summary>
    /// <param name="metadataReader">The reader over the exact metadata image that defines both members.</param>
    /// <param name="module">The content- or snapshot-derived identity assigned to that metadata image.</param>
    /// <param name="contextMethod">The same-module MethodDef whose IL contains the field operand.</param>
    /// <param name="metadataToken">The raw four-byte InlineField token to resolve.</param>
    /// <returns>
    /// An immutable exact Int32 instance-field descriptor, or a stable failure. W3 rejects MemberRef operands,
    /// fields declared by another TypeDef, generic owners, and static, literal, RVA-backed, or non-Int32 fields.
    /// </returns>
    public static ResolutionResult<ResolvedField> ProjectField(
        MetadataReader metadataReader,
        ModuleHandle module,
        MethodHandle contextMethod,
        int metadataToken)
    {
        ArgumentNullException.ThrowIfNull(metadataReader);

        if (module == default)
        {
            return Failure<ResolvedField>(
                ResolutionFailureKind.Invalid,
                "META_MODULE_HANDLE_INVALID",
                "Metadata projection requires a non-default module identity.");
        }

        if (contextMethod.Module != module)
        {
            return Failure<ResolvedField>(
                ResolutionFailureKind.Conflict,
                "META_FIELD_CONTEXT_MODULE_CONFLICT",
                "The containing method identity does not match the projected metadata module.");
        }

        if (!IsValidToken(
                contextMethod.MetadataToken,
                MethodDefinitionTokenType,
                metadataReader.MethodDefinitions.Count))
        {
            return Failure<ResolvedField>(
                ResolutionFailureKind.Invalid,
                "META_INVALID_CONTEXT_METHOD_TOKEN",
                "The field context is not a valid MethodDef in this module.");
        }

        if (!IsValidToken(metadataToken, FieldDefinitionTokenType, metadataReader.FieldDefinitions.Count))
        {
            return Failure<ResolvedField>(
                ResolutionFailureKind.Invalid,
                "META_INVALID_FIELD_TOKEN",
                "The InlineField operand is not a valid same-module FieldDef token.");
        }

        try
        {
            var contextHandle = MetadataTokens.MethodDefinitionHandle(contextMethod.MetadataToken & RowIdMask);
            var contextDefinition = metadataReader.GetMethodDefinition(contextHandle);
            if (contextDefinition.GetGenericParameters().Count != 0)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_GENERIC_FIELD_CONTEXT_UNSUPPORTED",
                    "Generic method contexts are outside the W3 field-resolution profile.");
            }

            var fieldHandle = MetadataTokens.FieldDefinitionHandle(metadataToken & RowIdMask);
            var fieldDefinition = metadataReader.GetFieldDefinition(fieldHandle);
            var declaringTypeHandle = fieldDefinition.GetDeclaringType();
            if (contextDefinition.GetDeclaringType() != declaringTypeHandle)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_FIELD_OWNER_UNSUPPORTED",
                    "W3 field operands must name a field declared directly by the containing method's type.");
            }

            var declaringTypeResult = ProjectDeclaringType(
                metadataReader,
                module,
                declaringTypeHandle,
                rejectGenericType: true);
            if (!declaringTypeResult.IsSuccess)
            {
                return Propagate<ResolvedField>(declaringTypeResult.Failure!);
            }

            var attributes = fieldDefinition.Attributes;
            var isStatic = (attributes & FieldAttributes.Static) != 0;
            var isLiteral = (attributes & FieldAttributes.Literal) != 0;
            var hasRva = (attributes & FieldAttributes.HasFieldRVA) != 0;
            if (isLiteral)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_LITERAL_FIELD_UNSUPPORTED",
                    "Literal fields are outside the W3 memory-opcode profile.");
            }

            if (hasRva)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_RVA_FIELD_UNSUPPORTED",
                    "RVA-backed fields are outside the W3 memory-opcode profile.");
            }

            if (isStatic)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_STATIC_FIELD_UNSUPPORTED",
                    "Static fields are outside the W3 memory-opcode profile.");
            }

            TypeSig fieldType;
            try
            {
                PreflightFieldSignature(metadataReader, fieldDefinition.Signature);
                fieldType = fieldDefinition.DecodeSignature(ClosedTypeProvider.Instance, GenericContext.Empty);
            }
            catch (UnsupportedSignatureShapeException exception)
            {
                return UnsupportedSignature<ResolvedField>(exception);
            }

            if (fieldType != TypeSig.Int32)
            {
                return Failure<ResolvedField>(
                    ResolutionFailureKind.Unsupported,
                    "META_FIELD_TYPE_UNSUPPORTED",
                    "W3 field operands must name a field with the exact CLI Int32 type.");
            }

            return ResolutionResult<ResolvedField>.Success(
                new ResolvedField(
                    new FieldHandle(module, metadataToken),
                    declaringTypeResult.Value,
                    fieldType,
                    isStatic,
                    isLiteral,
                    hasRva));
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<ResolvedField>(
                ResolutionFailureKind.Invalid,
                "META_FIELD_DEFINITION_INVALID",
                "The managed metadata contains an invalid field definition or signature.");
        }
    }

    private static ResolutionResult<ProjectedMethodCallMetadata> ProjectMethodCallMetadata(
        MetadataReader metadataReader,
        ModuleHandle module,
        MethodHandle method)
    {
        var methodHandle = MetadataTokens.MethodDefinitionHandle(method.MetadataToken & RowIdMask);
        var methodDefinition = metadataReader.GetMethodDefinition(methodHandle);
        var isStatic = (methodDefinition.Attributes & MethodAttributes.Static) != 0;
        var declaringTypeResult = ProjectDeclaringType(
            metadataReader,
            module,
            methodDefinition.GetDeclaringType(),
            rejectGenericType: true);
        if (!declaringTypeResult.IsSuccess)
        {
            return Propagate<ProjectedMethodCallMetadata>(declaringTypeResult.Failure!);
        }

        if (methodDefinition.GetGenericParameters().Count != 0)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_GENERIC_METHOD_UNSUPPORTED",
                "Generic method definitions are outside the W3 execution profile.");
        }

        var preflightResult = PreflightMethodSignature(metadataReader, methodDefinition.Signature);
        if (!preflightResult.IsSuccess)
        {
            return Propagate<ProjectedMethodCallMetadata>(preflightResult.Failure!);
        }

        MethodSignature<TypeSig> signature;
        try
        {
            signature = methodDefinition.DecodeSignature(ClosedTypeProvider.Instance, GenericContext.Empty);
        }
        catch (UnsupportedSignatureShapeException exception)
        {
            return UnsupportedSignature<ProjectedMethodCallMetadata>(exception);
        }

        if (signature.Header.Kind != SignatureKind.Method)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Invalid,
                "META_METHOD_SIGNATURE_KIND_INVALID",
                "The MethodDef signature does not use the method signature kind.");
        }

        if (signature.Header.CallingConvention == SignatureCallingConvention.VarArgs)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_VARARGS_METHOD_UNSUPPORTED",
                "Variable-argument method signatures are outside the W3 execution profile.");
        }

        if (signature.Header.CallingConvention != SignatureCallingConvention.Default)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_CALLING_CONVENTION_UNSUPPORTED",
                "The method calling convention is outside the W3 execution profile.");
        }

        if (signature.Header.HasExplicitThis)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_EXPLICIT_THIS_UNSUPPORTED",
                "Explicit-this method signatures are outside the W3 execution profile.");
        }

        if (signature.Header.IsGeneric || signature.GenericParameterCount != 0)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_GENERIC_METHOD_UNSUPPORTED",
                "Generic method signatures are outside the W3 execution profile.");
        }

        if (signature.Header.IsInstance == isStatic)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Invalid,
                "META_METHOD_THIS_MISMATCH",
                "Method attributes and signature disagree about the implicit receiver.");
        }

        if (signature.ParameterTypes.Length > MaximumExplicitParameterCount)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_PARAMETER_COUNT_LIMIT",
                "The method signature exceeds the deterministic explicit-parameter limit.");
        }

        if (signature.RequiredParameterCount != signature.ParameterTypes.Length)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_OPTIONAL_PARAMETERS_UNSUPPORTED",
                "Optional or sentinel-delimited parameters are outside the W3 execution profile.");
        }

        if (signature.ParameterTypes.Any(static type => type != TypeSig.Int32))
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_PARAMETER_TYPE_UNSUPPORTED",
                "W3 method parameters must have the exact CLI Int32 type.");
        }

        if (signature.ReturnType != TypeSig.Void && signature.ReturnType != TypeSig.Int32)
        {
            return Failure<ProjectedMethodCallMetadata>(
                ResolutionFailureKind.Unsupported,
                "META_RETURN_TYPE_UNSUPPORTED",
                "W3 methods must return void or the exact CLI Int32 type.");
        }

        var callSignature = new MethodCallSignatureShape(
            declaringTypeResult.Value,
            MethodCallingConventionKind.Default,
            signature.Header.IsInstance,
            hasExplicitThis: false,
            genericParameterCount: 0,
            signature.ParameterTypes,
            signature.ReturnType);
        return ResolutionResult<ProjectedMethodCallMetadata>.Success(
            new ProjectedMethodCallMetadata(
                callSignature,
                methodDefinition.Attributes,
                methodDefinition.ImplAttributes));
    }

    private static bool HasOptionalExplicitParameter(
        MetadataReader metadataReader,
        MethodDefinition methodDefinition)
    {
        foreach (var parameterHandle in methodDefinition.GetParameters())
        {
            var parameter = metadataReader.GetParameter(parameterHandle);
            if (parameter.SequenceNumber != 0 &&
                (parameter.Attributes & (ParameterAttributes.Optional | ParameterAttributes.HasDefault)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaringTypeIsGeneric(
        MetadataReader metadataReader,
        TypeDefinitionHandle declaringTypeHandle) =>
        metadataReader.GetTypeDefinition(declaringTypeHandle).GetGenericParameters().Count != 0;

    private static ResolutionResult<TypeSig> ProjectDeclaringType(
        MetadataReader metadataReader,
        ModuleHandle module,
        TypeDefinitionHandle declaringTypeHandle,
        bool rejectGenericType)
    {
        if (declaringTypeHandle.IsNil)
        {
            return Failure<TypeSig>(
                ResolutionFailureKind.Invalid,
                "META_DECLARING_TYPE_INVALID",
                "The metadata member has no declaring TypeDef.");
        }

        var rowId = MetadataTokens.GetRowNumber(declaringTypeHandle);
        if (rowId <= 0 || rowId > metadataReader.TypeDefinitions.Count)
        {
            return Failure<TypeSig>(
                ResolutionFailureKind.Invalid,
                "META_DECLARING_TYPE_INVALID",
                "The metadata member has an invalid declaring TypeDef.");
        }

        var typeDefinition = metadataReader.GetTypeDefinition(declaringTypeHandle);
        if (rejectGenericType && typeDefinition.GetGenericParameters().Count != 0)
        {
            return Failure<TypeSig>(
                ResolutionFailureKind.Unsupported,
                "META_GENERIC_DECLARING_TYPE_UNSUPPORTED",
                "Members declared by generic types are outside the W3 execution profile.");
        }

        var referenceTypeResult = VerifyReferenceTypeAncestry(metadataReader, declaringTypeHandle);
        if (!referenceTypeResult.IsSuccess)
        {
            return Propagate<TypeSig>(referenceTypeResult.Failure!);
        }

        var name = metadataReader.GetString(typeDefinition.Name);
        var typeNamespace = metadataReader.GetString(typeDefinition.Namespace);
        var displayName = string.IsNullOrEmpty(typeNamespace) ? name : $"{typeNamespace}.{name}";
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Failure<TypeSig>(
                ResolutionFailureKind.Invalid,
                "META_DECLARING_TYPE_NAME_INVALID",
                "The declaring TypeDef has no valid diagnostic name.");
        }

        if (displayName.Length > TypeSig.MaximumDisplayNameLength)
        {
            return Failure<TypeSig>(
                ResolutionFailureKind.Unsupported,
                "META_DECLARING_TYPE_NAME_LIMIT",
                "The declaring type name exceeds the deterministic diagnostic limit.");
        }

        return ResolutionResult<TypeSig>.Success(
            TypeSig.CreateTypeDefinition(module, MetadataTokens.GetToken(declaringTypeHandle), displayName));
    }

    private static ResolutionResult<bool> VerifyReferenceTypeAncestry(
        MetadataReader metadataReader,
        TypeDefinitionHandle declaringTypeHandle)
    {
        var visitedRows = new HashSet<int>();
        var currentHandle = declaringTypeHandle;
        for (var depth = 0; depth < MaximumTypeAncestryDepth; depth++)
        {
            var rowId = MetadataTokens.GetRowNumber(currentHandle);
            if (rowId <= 0 || rowId > metadataReader.TypeDefinitions.Count)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_RECEIVER_ANCESTRY_INVALID",
                    "The receiver TypeDef ancestry contains an invalid metadata row.");
            }

            if (!visitedRows.Add(rowId))
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_RECEIVER_ANCESTRY_CYCLE",
                    "The receiver TypeDef ancestry contains a cycle.");
            }

            var currentDefinition = metadataReader.GetTypeDefinition(currentHandle);
            if ((currentDefinition.Attributes & TypeAttributes.Interface) != 0 ||
                IsValueTypeMarker(
                    metadataReader.GetString(currentDefinition.Namespace),
                    metadataReader.GetString(currentDefinition.Name)))
            {
                return Failure<bool>(
                    ResolutionFailureKind.Unsupported,
                    "META_RECEIVER_TYPE_UNSUPPORTED",
                    "Value-type, enum, and interface receivers are outside the W3 execution profile.");
            }

            var baseTypeHandle = currentDefinition.BaseType;
            if (baseTypeHandle.IsNil)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_RECEIVER_ANCESTRY_INVALID",
                    "The receiver ancestry terminates without a validated external System.Object base.");
            }

            switch (baseTypeHandle.Kind)
            {
                case HandleKind.TypeDefinition:
                    currentHandle = (TypeDefinitionHandle)baseTypeHandle;
                    continue;
                case HandleKind.TypeReference:
                    return ClassifyTerminalTypeReference(
                        metadataReader,
                        (TypeReferenceHandle)baseTypeHandle);
                case HandleKind.TypeSpecification:
                    return Failure<bool>(
                        ResolutionFailureKind.Unsupported,
                        "META_RECEIVER_ANCESTRY_UNSUPPORTED",
                        "TypeSpec receiver ancestry is outside the W3 execution profile.");
                default:
                    return Failure<bool>(
                        ResolutionFailureKind.Invalid,
                        "META_RECEIVER_ANCESTRY_INVALID",
                        "The receiver TypeDef has an invalid base-type handle.");
            }
        }

        return Failure<bool>(
            ResolutionFailureKind.Unsupported,
            "META_RECEIVER_ANCESTRY_LIMIT",
            "The receiver TypeDef ancestry exceeds the deterministic traversal limit.");
    }

    private static ResolutionResult<bool> ClassifyTerminalTypeReference(
        MetadataReader metadataReader,
        TypeReferenceHandle typeReferenceHandle)
    {
        var typeReference = metadataReader.GetTypeReference(typeReferenceHandle);
        var name = metadataReader.GetString(typeReference.Name);
        var typeNamespace = metadataReader.GetString(typeReference.Namespace);
        if (IsValueTypeMarker(typeNamespace, name))
        {
            return Failure<bool>(
                ResolutionFailureKind.Unsupported,
                "META_RECEIVER_TYPE_UNSUPPORTED",
                "Value-type, enum, and interface receivers are outside the W3 execution profile.");
        }

        if (!string.Equals(typeNamespace, "System", StringComparison.Ordinal) ||
            !string.Equals(name, "Object", StringComparison.Ordinal) ||
            !IsKnownCoreLibraryScope(metadataReader, typeReference.ResolutionScope))
        {
            return Failure<bool>(
                ResolutionFailureKind.Unsupported,
                "META_RECEIVER_ANCESTRY_UNRESOLVED",
                "The receiver base type cannot be proven to be a reference type without external resolution.");
        }

        return ResolutionResult<bool>.Success(true);
    }

    private static bool IsKnownCoreLibraryScope(MetadataReader metadataReader, EntityHandle resolutionScope)
    {
        if (resolutionScope.Kind != HandleKind.AssemblyReference)
        {
            return false;
        }

        var assemblyReference = metadataReader.GetAssemblyReference((AssemblyReferenceHandle)resolutionScope);
        return IsKnownCoreLibraryAssemblyName(metadataReader.GetString(assemblyReference.Name));
    }

    private static bool IsKnownCoreLibraryAssemblyName(string name) =>
        string.Equals(name, "System.Runtime", StringComparison.Ordinal) ||
        string.Equals(name, "System.Private.CoreLib", StringComparison.Ordinal) ||
        string.Equals(name, "mscorlib", StringComparison.Ordinal) ||
        string.Equals(name, "netstandard", StringComparison.Ordinal);

    private static bool IsValueTypeMarker(string typeNamespace, string name) =>
        string.Equals(typeNamespace, "System", StringComparison.Ordinal) &&
        (string.Equals(name, "ValueType", StringComparison.Ordinal) ||
         string.Equals(name, "Enum", StringComparison.Ordinal));

    private static ResolutionResult<ImmutableArray<TypeSig>> ProjectLocals(
        MetadataReader metadataReader,
        int localSignatureToken)
    {
        if (localSignatureToken == 0)
        {
            return ResolutionResult<ImmutableArray<TypeSig>>.Success(ImmutableArray<TypeSig>.Empty);
        }

        var standaloneSignatureCount = metadataReader.GetTableRowCount(TableIndex.StandAloneSig);
        if (!IsValidToken(localSignatureToken, StandaloneSignatureTokenType, standaloneSignatureCount))
        {
            return Failure<ImmutableArray<TypeSig>>(
                ResolutionFailureKind.Invalid,
                "META_LOCAL_SIGNATURE_TOKEN_INVALID",
                "The method body local-signature token is not a valid StandAloneSig in this module.");
        }

        try
        {
            var handle = MetadataTokens.StandaloneSignatureHandle(localSignatureToken & RowIdMask);
            var standaloneSignature = metadataReader.GetStandaloneSignature(handle);
            var preflightResult = PreflightLocalSignature(metadataReader, standaloneSignature.Signature);
            if (!preflightResult.IsSuccess)
            {
                return Propagate<ImmutableArray<TypeSig>>(preflightResult.Failure!);
            }

            ImmutableArray<TypeSig> localTypes;
            try
            {
                localTypes = standaloneSignature.DecodeLocalSignature(
                    ClosedTypeProvider.Instance,
                    GenericContext.Empty);
            }
            catch (UnsupportedSignatureShapeException exception)
            {
                return UnsupportedSignature<ImmutableArray<TypeSig>>(exception);
            }

            if (localTypes.Length > MaximumLocalCount)
            {
                return Failure<ImmutableArray<TypeSig>>(
                    ResolutionFailureKind.Unsupported,
                    "META_LOCAL_COUNT_LIMIT",
                    "The local signature exceeds the deterministic local-slot limit.");
            }

            if (localTypes.Any(type => type != TypeSig.Int32))
            {
                return Failure<ImmutableArray<TypeSig>>(
                    ResolutionFailureKind.Unsupported,
                    "META_LOCAL_TYPE_UNSUPPORTED",
                    "W3 method locals must have the exact CLI Int32 type.");
            }

            return ResolutionResult<ImmutableArray<TypeSig>>.Success(localTypes);
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<ImmutableArray<TypeSig>>(
                ResolutionFailureKind.Invalid,
                "META_LOCAL_SIGNATURE_INVALID",
                "The method body references an invalid local-variable signature.");
        }
    }

    private static ResolutionResult<bool> PreflightMethodSignature(
        MetadataReader metadataReader,
        BlobHandle signatureHandle)
    {
        try
        {
            var reader = metadataReader.GetBlobReader(signatureHandle);
            var header = reader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_METHOD_SIGNATURE_KIND_INVALID",
                    "The MethodDef signature does not use the method signature kind.");
            }

            if (header.IsGeneric)
            {
                var genericCount = reader.ReadCompressedInteger();
                if (genericCount < 0)
                {
                    return Failure<bool>(
                        ResolutionFailureKind.Invalid,
                        "META_METHOD_SIGNATURE_INVALID",
                        "The generic method arity is structurally invalid.");
                }
            }

            var parameterCount = reader.ReadCompressedInteger();
            if (parameterCount < 0)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_METHOD_SIGNATURE_INVALID",
                    "The method parameter count is structurally invalid.");
            }

            if (parameterCount > MaximumExplicitParameterCount)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Unsupported,
                    "META_PARAMETER_COUNT_LIMIT",
                    "The method signature exceeds the deterministic explicit-parameter limit.");
            }

            return ResolutionResult<bool>.Success(true);
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<bool>(
                ResolutionFailureKind.Invalid,
                "META_METHOD_SIGNATURE_INVALID",
                "The MethodDef signature is structurally invalid.");
        }
    }

    private static ResolutionResult<bool> PreflightLocalSignature(
        MetadataReader metadataReader,
        BlobHandle signatureHandle)
    {
        try
        {
            var reader = metadataReader.GetBlobReader(signatureHandle);
            var header = reader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.LocalVariables)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_LOCAL_SIGNATURE_KIND_INVALID",
                    "The method body StandAloneSig is not a local-variable signature.");
            }

            var localCount = reader.ReadCompressedInteger();
            if (localCount < 0)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Invalid,
                    "META_LOCAL_SIGNATURE_INVALID",
                    "The local-variable count is structurally invalid.");
            }

            if (localCount > MaximumLocalCount)
            {
                return Failure<bool>(
                    ResolutionFailureKind.Unsupported,
                    "META_LOCAL_COUNT_LIMIT",
                    "The local signature exceeds the deterministic local-slot limit.");
            }

            return ResolutionResult<bool>.Success(true);
        }
        catch (Exception exception) when (IsInvalidMetadataException(exception))
        {
            return Failure<bool>(
                ResolutionFailureKind.Invalid,
                "META_LOCAL_SIGNATURE_INVALID",
                "The method body references an invalid local-variable signature.");
        }
    }

    private static void PreflightFieldSignature(MetadataReader metadataReader, BlobHandle signatureHandle)
    {
        var reader = metadataReader.GetBlobReader(signatureHandle);
        if (reader.ReadSignatureHeader().Kind != SignatureKind.Field)
        {
            throw new BadImageFormatException("The FieldDef signature does not use the field signature kind.");
        }
    }

    private static bool IsValidToken(int token, int tokenType, int tableRowCount)
    {
        if ((token & TokenTypeMask) != tokenType)
        {
            return false;
        }

        var rowId = token & RowIdMask;
        return rowId > 0 && rowId <= tableRowCount;
    }

    internal static bool IsOrdinaryManagedIlImplementation(
        MethodAttributes attributes,
        MethodImplAttributes implementationAttributes) =>
        (attributes & (MethodAttributes.PinvokeImpl | MethodAttributes.Abstract)) == 0 &&
        (implementationAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.IL &&
        (implementationAttributes & MethodImplAttributes.ManagedMask) == MethodImplAttributes.Managed &&
        (implementationAttributes &
            (MethodImplAttributes.InternalCall |
             MethodImplAttributes.ForwardRef |
             MethodImplAttributes.Synchronized)) == 0;

    private static bool IsInvalidMetadataException(Exception exception) =>
        exception is BadImageFormatException or ArgumentOutOfRangeException or InvalidOperationException;

    private static ResolutionResult<T> UnsupportedSignature<T>(UnsupportedSignatureShapeException exception) =>
        Failure<T>(ResolutionFailureKind.Unsupported, exception.Code, exception.SafeMessage);

    private static ResolutionResult<T> Propagate<T>(ResolutionFailure failure) =>
        Failure<T>(failure.Kind, failure.Code, failure.Message);

    private static ResolutionResult<T> Failure<T>(
        ResolutionFailureKind kind,
        string code,
        string message) =>
        ResolutionResult<T>.Failed(kind, code, message);

    private readonly record struct GenericContext
    {
        internal static GenericContext Empty => default;
    }

    private readonly record struct ProjectedMethodCallMetadata(
        MethodCallSignatureShape Signature,
        MethodAttributes Attributes,
        MethodImplAttributes ImplementationAttributes);

    private sealed class ClosedTypeProvider : ISignatureTypeProvider<TypeSig, GenericContext>
    {
        internal static ClosedTypeProvider Instance { get; } = new();

        private ClosedTypeProvider()
        {
        }

        public TypeSig GetArrayType(TypeSig elementType, ArrayShape shape) =>
            throw Unsupported("META_ARRAY_TYPE_UNSUPPORTED", "Array signatures are outside the W3 execution profile.");

        public TypeSig GetByReferenceType(TypeSig elementType) =>
            throw Unsupported("META_BYREF_TYPE_UNSUPPORTED", "By-reference signatures are outside the W3 execution profile.");

        public TypeSig GetFunctionPointerType(MethodSignature<TypeSig> signature) =>
            throw Unsupported("META_FUNCTION_POINTER_UNSUPPORTED", "Function-pointer signatures are outside the W3 execution profile.");

        public TypeSig GetGenericInstantiation(TypeSig genericType, ImmutableArray<TypeSig> typeArguments) =>
            throw Unsupported("META_GENERIC_INSTANTIATION_UNSUPPORTED", "Generic type signatures are outside the W3 execution profile.");

        public TypeSig GetGenericMethodParameter(GenericContext genericContext, int index) =>
            throw Unsupported("META_GENERIC_METHOD_PARAMETER_UNSUPPORTED", "Generic method parameters are outside the W3 execution profile.");

        public TypeSig GetGenericTypeParameter(GenericContext genericContext, int index) =>
            throw Unsupported("META_GENERIC_TYPE_PARAMETER_UNSUPPORTED", "Generic type parameters are outside the W3 execution profile.");

        public TypeSig GetModifiedType(TypeSig modifier, TypeSig unmodifiedType, bool isRequired) =>
            throw Unsupported("META_CUSTOM_MODIFIER_UNSUPPORTED", "Custom-modified signatures are outside the W3 execution profile.");

        public TypeSig GetPinnedType(TypeSig elementType) =>
            throw Unsupported("META_PINNED_LOCAL_UNSUPPORTED", "Pinned local signatures are outside the W3 execution profile.");

        public TypeSig GetPointerType(TypeSig elementType) =>
            throw Unsupported("META_POINTER_TYPE_UNSUPPORTED", "Pointer signatures are outside the W3 execution profile.");

        public TypeSig GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => TypeSig.Void,
            PrimitiveTypeCode.Boolean => TypeSig.Boolean,
            PrimitiveTypeCode.Int32 => TypeSig.Int32,
            PrimitiveTypeCode.Int64 => TypeSig.Int64,
            PrimitiveTypeCode.String => TypeSig.String,
            PrimitiveTypeCode.Object => TypeSig.Object,
            _ => throw Unsupported(
                "META_PRIMITIVE_TYPE_UNSUPPORTED",
                "The primitive signature type is outside the W3 execution profile."),
        };

        public TypeSig GetSZArrayType(TypeSig elementType) =>
            throw Unsupported("META_SZARRAY_TYPE_UNSUPPORTED", "Array signatures are outside the W3 execution profile.");

        public TypeSig GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) =>
            throw Unsupported("META_NAMED_TYPE_UNSUPPORTED", "Named signature types are outside the W3 execution profile.");

        public TypeSig GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind) =>
            throw Unsupported("META_NAMED_TYPE_UNSUPPORTED", "Named signature types are outside the W3 execution profile.");

        public TypeSig GetTypeFromSpecification(
            MetadataReader reader,
            GenericContext genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            throw Unsupported("META_TYPE_SPECIFICATION_UNSUPPORTED", "TypeSpec signatures are outside the W3 execution profile.");

        private static UnsupportedSignatureShapeException Unsupported(string code, string safeMessage) =>
            new(code, safeMessage);
    }

    private sealed class UnsupportedSignatureShapeException : Exception
    {
        internal UnsupportedSignatureShapeException(string code, string safeMessage)
            : base(safeMessage)
        {
            Code = code;
            SafeMessage = safeMessage;
        }

        internal string Code { get; }

        internal string SafeMessage { get; }
    }
}
