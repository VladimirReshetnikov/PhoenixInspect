using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Names the method a selected managed frame is executing, read from that frame's own module metadata.
/// </summary>
/// <param name="DeclaringTypeFullName">
/// The declaring type's metadata full name. Namespaces are separated by <c>.</c> and nested types by <c>+</c>, which
/// is the metadata spelling rather than a C# source spelling.
/// </param>
/// <param name="MethodName">The method's metadata name, which for a compiler-generated method is its emitted name.</param>
/// <param name="ModuleName">The display name of the module instance whose metadata supplied both names.</param>
/// <param name="ParameterList">
/// The parenthesized parameter list decoded from the method's signature blob, such as <c>(string[] args)</c>, or an
/// empty string when the signature could not be decoded. Types use C# keyword spellings where one exists and short
/// metadata names elsewhere; the list is a display aid, never an identity.
/// </param>
public sealed record ClrmdFrameMethodName(
    string DeclaringTypeFullName,
    string MethodName,
    string ModuleName,
    string ParameterList = "")
{
    /// <summary>Gets the one-line spelling a call stack displays.</summary>
    public string DisplayName => $"{DeclaringTypeFullName}.{MethodName}";

    /// <summary>
    /// Gets <see cref="DisplayName"/> followed by <see cref="ParameterList"/> when the signature decoded, which is
    /// the spelling a Visual Studio call-stack row uses.
    /// </summary>
    public string Signature => $"{DisplayName}{ParameterList}";
}

public sealed partial class ClrmdDumpSession
{
    /// <summary>Gets the deterministic bound on how deep a nested-type chain may be walked while building a name.</summary>
    public const int MaximumTypeNestingDepth = 32;

    private static readonly ImmutableArray<EvaluationDeterministicBound> FrameMethodNestingBounds =
        ImmutableArray.Create(new EvaluationDeterministicBound(
            "dump.frame-method.type-nesting-depth",
            MaximumTypeNestingDepth));

    /// <summary>
    /// Resolves the declaring type and method name of one exact selected frame from counted dump metadata.
    /// </summary>
    /// <param name="frame">An exact frame identity produced by <see cref="SelectExpressionFrame"/>.</param>
    /// <returns>
    /// An exact name only when the frame's module metadata was read completely and the frame's MethodDef row resolved
    /// within the nesting bound; otherwise a typed conflicting, unavailable, partial, or invalid result that names no
    /// method.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The names come from the same counted metadata bytes the snapshot already carries, never from a disk image and
    /// never from a symbol server, so a name is evidence from the dump on the same footing as a field value. A frame
    /// whose module metadata is not fully present in the snapshot is reported as unnamed rather than guessed at from
    /// the instruction pointer.
    /// </para>
    /// <para>
    /// This is a display aid. It does not widen expression binding: no evaluation path consumes a frame method name,
    /// and naming a frame neither admits nor rejects any expression.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    public ClrmdEvidenceResult<ClrmdFrameMethodName> DescribeFrameMethod(DumpSelectedFrameIdentity frame)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Selector.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        ClrmdModuleInfo? module = null;
        foreach (var candidate in Modules)
        {
            if (candidate.Identity == frame.RuntimeModule)
            {
                module = candidate;
                break;
            }
        }

        if (module is null)
        {
            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        if (module.MetadataAddress == 0 ||
            module.MetadataLength == 0 ||
            module.MetadataLength > (ulong)Memory.MaximumReadLength ||
            module.MetadataAddress > ulong.MaxValue - (module.MetadataLength - 1))
        {
            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MetadataUnavailable);
        }

        var metadataRead = Memory.Read(module.MetadataAddress, checked((int)module.MetadataLength));
        var evidence = ImmutableArray.Create(metadataRead);
        if (metadataRead.Status != MemoryReadStatus.Exact)
        {
            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                metadataRead.Status == MemoryReadStatus.Partial
                    ? ClrmdEvidenceStatus.Partial
                    : ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable,
                evidence: evidence);
        }

        try
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
            var reader = provider.GetMetadataReader();
            var methodHandle = MetadataTokens.MethodDefinitionHandle(frame.MethodDefinitionToken);
            if (methodHandle.IsNil)
            {
                return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: evidence);
            }

            var method = reader.GetMethodDefinition(methodHandle);
            var methodName = reader.GetString(method.Name);
            if (!TryBuildTypeFullName(reader, method.GetDeclaringType(), out var declaringTypeFullName))
            {
                return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                    ClrmdEvidenceStatus.Partial,
                    ClrmdValueIssue.LimitExceeded,
                    evidence: evidence,
                    appliedBounds: FrameMethodNestingBounds);
            }

            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                new ClrmdFrameMethodName(
                    declaringTypeFullName,
                    methodName,
                    module.Name,
                    DecodeParameterList(reader, methodHandle, method)),
                evidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or ArgumentOutOfRangeException or ArgumentException)
        {
            return ClrmdEvidenceResult<ClrmdFrameMethodName>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: evidence);
        }
    }

    /// <summary>
    /// Decodes the method's parenthesized parameter list from its signature blob, pairing each decoded type with the
    /// Param-row name that shares its sequence number.
    /// </summary>
    /// <param name="reader">The metadata reader over the frame module's counted metadata bytes.</param>
    /// <param name="methodHandle">The frame method's definition handle, used to name generic method parameters.</param>
    /// <param name="method">The frame method's definition row.</param>
    /// <returns>
    /// A list such as <c>(string[] args, int count)</c>, or an empty string when the blob does not decode; the
    /// surrounding name remains usable either way because the list is display-only.
    /// </returns>
    private static string DecodeParameterList(
        MetadataReader reader,
        MethodDefinitionHandle methodHandle,
        MethodDefinition method)
    {
        try
        {
            var genericContext = new FrameMethodGenericContext(
                CollectGenericParameterNames(reader, method.GetDeclaringType()),
                CollectGenericParameterNames(reader, methodHandle));
            var signature = method.DecodeSignature(FrameMethodSignatureProvider.Instance, genericContext);

            var names = new string?[signature.ParameterTypes.Length];
            foreach (var parameterHandle in method.GetParameters())
            {
                var parameter = reader.GetParameter(parameterHandle);
                // Sequence number 0 names the return value; 1..n name the parameters in signature order.
                if (parameter.SequenceNumber >= 1 &&
                    parameter.SequenceNumber <= signature.ParameterTypes.Length &&
                    !parameter.Name.IsNil)
                {
                    names[parameter.SequenceNumber - 1] = reader.GetString(parameter.Name);
                }
            }

            var rendered = new string[signature.ParameterTypes.Length];
            for (var index = 0; index < signature.ParameterTypes.Length; index++)
            {
                var name = names[index];
                rendered[index] = string.IsNullOrEmpty(name)
                    ? signature.ParameterTypes[index]
                    : $"{signature.ParameterTypes[index]} {name}";
            }

            return $"({string.Join(", ", rendered)})";
        }
        catch (BadImageFormatException)
        {
            // A malformed signature blob must not withhold the already-resolved names; the list simply stays absent.
            return string.Empty;
        }
    }

    /// <summary>Reads the declared generic-parameter names of a type or method definition, in declared order.</summary>
    /// <param name="reader">The metadata reader over the frame module's counted metadata bytes.</param>
    /// <param name="handle">A type-definition or method-definition handle.</param>
    /// <returns>The names, or an empty array when the definition declares none.</returns>
    private static ImmutableArray<string> CollectGenericParameterNames(MetadataReader reader, EntityHandle handle)
    {
        var handles = handle.Kind switch
        {
            HandleKind.TypeDefinition => reader.GetTypeDefinition((TypeDefinitionHandle)handle).GetGenericParameters(),
            HandleKind.MethodDefinition =>
                reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetGenericParameters(),
            _ => default,
        };
        if (handles.Count == 0)
        {
            return [];
        }

        var names = ImmutableArray.CreateBuilder<string>(handles.Count);
        foreach (var genericParameterHandle in handles)
        {
            var genericParameter = reader.GetGenericParameter(genericParameterHandle);
            names.Add(genericParameter.Name.IsNil ? $"T{names.Count}" : reader.GetString(genericParameter.Name));
        }

        return names.ToImmutable();
    }

    private static bool TryBuildTypeFullName(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        out string fullName)
    {
        fullName = string.Empty;
        if (handle.IsNil)
        {
            return false;
        }

        var segments = new List<string>(capacity: 4);
        var current = handle;
        for (var depth = 0; depth < MaximumTypeNestingDepth; depth++)
        {
            var definition = reader.GetTypeDefinition(current);
            segments.Add(reader.GetString(definition.Name));
            var declaring = definition.GetDeclaringType();
            if (declaring.IsNil)
            {
                var typeNamespace = reader.GetString(definition.Namespace);
                segments.Reverse();
                var nested = string.Join('+', segments);
                fullName = typeNamespace.Length == 0 ? nested : $"{typeNamespace}.{nested}";
                return true;
            }

            current = declaring;
        }

        return false;
    }

    /// <summary>Carries the declared generic-parameter names the signature decoder substitutes for indices.</summary>
    /// <param name="TypeParameters">The declaring type's generic-parameter names, in declared order.</param>
    /// <param name="MethodParameters">The method's generic-parameter names, in declared order.</param>
    private sealed record FrameMethodGenericContext(
        ImmutableArray<string> TypeParameters,
        ImmutableArray<string> MethodParameters);

    /// <summary>
    /// Renders signature types as short C#-flavored display strings: keyword spellings for primitives, short
    /// metadata names elsewhere, and declared names for generic parameters.
    /// </summary>
    /// <remarks>
    /// The provider deliberately resolves no cross-module references and follows no handles beyond the name of the
    /// row it is given, so decoding a parameter list never widens what metadata the frame naming path reads.
    /// </remarks>
    private sealed class FrameMethodSignatureProvider :
        ISignatureTypeProvider<string, FrameMethodGenericContext>
    {
        /// <summary>Gets the stateless singleton instance.</summary>
        public static FrameMethodSignatureProvider Instance { get; } = new();

        private FrameMethodSignatureProvider()
        {
        }

        /// <inheritdoc />
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.IntPtr => "nint",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.TypedReference => "typedref",
            _ => typeCode.ToString(),
        };

        /// <inheritdoc />
        public string GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) =>
            ShortTypeName(reader.GetString(reader.GetTypeDefinition(handle).Name));

        /// <inheritdoc />
        public string GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind) =>
            ShortTypeName(reader.GetString(reader.GetTypeReference(handle).Name));

        /// <inheritdoc />
        public string GetTypeFromSpecification(
            MetadataReader reader,
            FrameMethodGenericContext genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        /// <inheritdoc />
        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        /// <inheritdoc />
        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";

        /// <inheritdoc />
        public string GetByReferenceType(string elementType) => $"ref {elementType}";

        /// <inheritdoc />
        public string GetPointerType(string elementType) => $"{elementType}*";

        /// <inheritdoc />
        public string GetPinnedType(string elementType) => elementType;

        /// <inheritdoc />
        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"delegate*<{string.Join(", ", signature.ParameterTypes.Add(signature.ReturnType))}>";

        /// <inheritdoc />
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";

        /// <inheritdoc />
        public string GetGenericTypeParameter(FrameMethodGenericContext genericContext, int index) =>
            index >= 0 && index < genericContext.TypeParameters.Length
                ? genericContext.TypeParameters[index]
                : $"!{index}";

        /// <inheritdoc />
        public string GetGenericMethodParameter(FrameMethodGenericContext genericContext, int index) =>
            index >= 0 && index < genericContext.MethodParameters.Length
                ? genericContext.MethodParameters[index]
                : $"!!{index}";

        /// <inheritdoc />
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        /// <summary>Strips the namespace-free metadata name's generic arity suffix, such as <c>`2</c>.</summary>
        /// <param name="metadataName">The metadata type name.</param>
        /// <returns>The display spelling used inside a parameter list.</returns>
        private static string ShortTypeName(string metadataName)
        {
            var arity = metadataName.IndexOf('`');
            return arity < 0 ? metadataName : metadataName[..arity];
        }
    }
}
