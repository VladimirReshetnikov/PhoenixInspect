using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Metadata.Abstractions;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Metadata.SRM;

/// <summary>
/// Provides the bounded SRM metadata backend used by the W1 body-evidence and W3 execution slices.
/// </summary>
/// <remarks>
/// The backend resolves deterministic MethodDef handles and bodies, and projects W3 method signatures, local
/// signatures, and same-module FieldDefs through <see cref="SrmMetadataProjection"/>. Projection is deliberately
/// separated from disk acquisition so dump-counted metadata can reuse the exact same closed semantic mapping.
/// </remarks>
public sealed class SrmMetadataModule : IMetadataModule, IDisposable
{
    private const int MethodDefinitionTokenType = 0x06000000;
    private const long MaximumExternalArtifactLength = 512L * 1024 * 1024;
    private const int MaximumLookupNameLength = 1024;
    private const int MaximumTypeDefinitionScanCount = 100_000;
    private const int MaximumMethodDefinitionScanCount = 1_000_000;
    private const int MaximumMethodBodyCodeBytes = 4_096;
    private readonly FileStream _stream;
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadataReader;
    private bool _disposed;

    /// <summary>
    /// Opens a managed PE module from disk and derives path-independent identity from its metadata and PE headers.
    /// </summary>
    /// <param name="modulePath">The local artifact path to open for metadata operations.</param>
    /// <remarks>
    /// The path is retained only as descriptor evidence. It does not participate in <see cref="Id"/> or
    /// <see cref="ModuleHandle"/> equality, so byte-identical copies produce stable execution identities.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="modulePath"/> is empty or whitespace.</exception>
    /// <exception cref="NotSupportedException">
    /// The opened artifact exceeds the deterministic external-input size limit.
    /// </exception>
    public SrmMetadataModule(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
        {
            throw new ArgumentException("Module path is required.", nameof(modulePath));
        }

        FileStream? stream = null;
        PEReader? peReader = null;
        try
        {
            stream = File.OpenRead(modulePath);
            if (stream.Length > MaximumExternalArtifactLength)
            {
                throw new ArtifactSizeLimitExceededException(MaximumExternalArtifactLength);
            }

            var artifactIdentity = ArtifactContentIdentity.FromStream(stream);
            peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var metadataReader = peReader.GetMetadataReader();

            var moduleDefinition = metadataReader.GetModuleDefinition();
            var mvid = metadataReader.GetGuid(moduleDefinition.Mvid);
            var metadataContent = peReader.GetMetadata().GetContent();
            var contentIdentity = ModuleContentIdentity.FromMetadata(mvid, metadataContent.AsSpan());
            var peHeader = peReader.PEHeaders.PEHeader
                ?? throw new BadImageFormatException("The managed module has no PE optional header.");
            var peStamp = (
                TimeDateStamp: unchecked((uint)peReader.PEHeaders.CoffHeader.TimeDateStamp),
                ImageSize: unchecked((uint)peHeader.SizeOfImage));

            Id = new ModuleId(contentIdentity, peStamp, artifactIdentity);
            Descriptor = new ModuleDescriptor(Id, Path.GetFileName(modulePath), Path.GetFullPath(modulePath));
            ModuleHandle = ModuleHandle.FromContentIdentity(
                contentIdentity,
                peStamp.TimeDateStamp,
                peStamp.ImageSize,
                artifactIdentity);
            _stream = stream;
            _peReader = peReader;
            _metadataReader = metadataReader;
        }
        catch
        {
            peReader?.Dispose();
            stream?.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ModuleId Id { get; }

    /// <inheritdoc />
    public ModuleDescriptor Descriptor { get; }

    /// <inheritdoc />
    public ModuleHandle ModuleHandle { get; }

    /// <summary>
    /// Opens an SRM module from a local managed PE artifact.
    /// </summary>
    /// <param name="modulePath">The named local artifact path to open.</param>
    /// <returns>A disposable SRM-backed metadata module.</returns>
    /// <remarks>
    /// This throwing convenience is intended for named fixtures and caller-programming errors. External artifact
    /// admission should use <see cref="Open"/> so absence and malformed PE content remain typed outcomes.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The opened artifact exceeds the deterministic external-input size limit.
    /// </exception>
    public static SrmMetadataModule LoadFromFile(string modulePath) => new(modulePath);

    /// <summary>
    /// Attempts to open an external managed PE artifact without exposing incidental parser or file-system exceptions.
    /// </summary>
    /// <param name="modulePath">The local artifact path supplied by the host.</param>
    /// <returns>
    /// A disposable module on success; otherwise a stable unavailable or invalid result whose message does not echo
    /// the path or parser payload.
    /// </returns>
    public static ResolutionResult<SrmMetadataModule> Open(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
        {
            return ResolutionResult<SrmMetadataModule>.Failed(
                ResolutionFailureKind.Invalid,
                "META_ARTIFACT_PATH_INVALID",
                "A non-empty local artifact path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(modulePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ResolutionResult<SrmMetadataModule>.Failed(
                ResolutionFailureKind.Invalid,
                "META_ARTIFACT_PATH_INVALID",
                "The supplied local artifact path is structurally invalid.");
        }

        try
        {
            return ResolutionResult<SrmMetadataModule>.Success(new SrmMetadataModule(fullPath));
        }
        catch (ArtifactSizeLimitExceededException)
        {
            return ResolutionResult<SrmMetadataModule>.Failed(
                ResolutionFailureKind.Unsupported,
                "META_ARTIFACT_SIZE_LIMIT",
                "The managed artifact exceeds the deterministic external-input size limit.");
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            DirectoryNotFoundException or
            UnauthorizedAccessException or
            IOException)
        {
            return ResolutionResult<SrmMetadataModule>.Failed(
                ResolutionFailureKind.Unavailable,
                "META_ARTIFACT_UNAVAILABLE",
                "The managed artifact could not be opened from the supplied location.");
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException)
        {
            return ResolutionResult<SrmMetadataModule>.Failed(
                ResolutionFailureKind.Invalid,
                "META_ARTIFACT_INVALID",
                "The managed artifact is structurally invalid for the active metadata reader.");
        }
    }

    /// <summary>
    /// Searches a simple metadata type for a uniquely named method definition.
    /// </summary>
    /// <param name="typeName">The simple metadata type name.</param>
    /// <param name="methodName">The method name to find.</param>
    /// <returns>
    /// The unique MethodDef token, or a structured unavailable, invalid, or conflict result that preserves why the
    /// fixture lookup could not produce an identity.
    /// </returns>
    /// <remarks>
    /// This helper exists for bounded fixtures. Production binding must include namespace, arity, and signature and
    /// must surface ambiguous matches rather than selecting the first overload.
    /// </remarks>
    public ResolutionResult<int> FindMethodDefinition(string typeName, string methodName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
        {
            return ResolutionResult<int>.Failed(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METHOD_LOOKUP",
                "A non-empty simple type name and method name are required.");
        }

        if (typeName.Length > MaximumLookupNameLength || methodName.Length > MaximumLookupNameLength)
        {
            return ResolutionResult<int>.Failed(
                ResolutionFailureKind.Invalid,
                "META_METHOD_LOOKUP_TOO_LONG",
                "Type and method names are bounded for deterministic metadata lookup.");
        }

        if (_metadataReader.TypeDefinitions.Count > MaximumTypeDefinitionScanCount ||
            _metadataReader.MethodDefinitions.Count > MaximumMethodDefinitionScanCount)
        {
            return ResolutionResult<int>.Failed(
                ResolutionFailureKind.Unsupported,
                "META_METHOD_LOOKUP_LIMIT",
                "The metadata tables exceed the deterministic lookup profile.");
        }

        try
        {
            var methodToken = default(int);
            var matchingTypeCount = 0;
            var matchCount = 0;

            foreach (var typeHandle in _metadataReader.TypeDefinitions)
            {
                var typeDefinition = _metadataReader.GetTypeDefinition(typeHandle);
                if (!string.Equals(_metadataReader.GetString(typeDefinition.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                matchingTypeCount++;

                foreach (var candidateHandle in typeDefinition.GetMethods())
                {
                    var methodDefinition = _metadataReader.GetMethodDefinition(candidateHandle);
                    if (!string.Equals(_metadataReader.GetString(methodDefinition.Name), methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    methodToken = MetadataTokens.GetToken(candidateHandle);
                    matchCount++;
                }
            }

            if (matchingTypeCount == 0)
            {
                return ResolutionResult<int>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "META_TYPE_NOT_FOUND",
                    "No matching metadata type was found.");
            }

            if (matchCount == 0)
            {
                return ResolutionResult<int>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "META_METHOD_NOT_FOUND",
                    "No matching method definition was found.");
            }

            if (matchCount > 1)
            {
                return ResolutionResult<int>.Failed(
                    ResolutionFailureKind.Conflict,
                    "META_AMBIGUOUS_METHOD",
                    "Multiple matching method definitions were found.");
            }

            return ResolutionResult<int>.Success(methodToken);
        }
        catch (Exception exception) when (exception is BadImageFormatException or ArgumentOutOfRangeException)
        {
            return ResolutionResult<int>.Failed(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METADATA",
                "The managed artifact contains invalid method-definition metadata.");
        }
    }

    /// <inheritdoc />
    public ResolutionResult<MethodHandle> GetMethodHandle(int metadataToken)
    {
        ThrowIfDisposed();
        try
        {
            if (!IsValidMethodDefinitionToken(metadataToken))
            {
                return ResolutionResult<MethodHandle>.Failed(
                    ResolutionFailureKind.Invalid,
                    "META_INVALID_METHOD_TOKEN",
                    "The supplied metadata token is not a valid MethodDef in this module.");
            }

            return ResolutionResult<MethodHandle>.Success(new MethodHandle(ModuleHandle, metadataToken));
        }
        catch (Exception exception) when (exception is BadImageFormatException or ArgumentOutOfRangeException)
        {
            return ResolutionResult<MethodHandle>.Failed(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METADATA",
                "The managed artifact contains invalid MethodDef table metadata.");
        }
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
    {
        ThrowIfDisposed();
        if (method.Module != ModuleHandle)
        {
            return ResolutionResult<ResolvedMethodDefinition>.Failed(
                ResolutionFailureKind.Conflict,
                "META_METHOD_MODULE_CONFLICT",
                "The requested method identity does not match this metadata module.");
        }

        var bodyResult = GetMethodBody(method);
        if (!bodyResult.IsSuccess)
        {
            var failure = bodyResult.Failure!;
            return ResolutionResult<ResolvedMethodDefinition>.Failed(
                failure.Kind,
                failure.Code,
                failure.Message);
        }

        return SrmMetadataProjection.ProjectMethodDefinition(
            _metadataReader,
            ModuleHandle,
            method,
            bodyResult.Value);
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
        MethodHandle contextMethod,
        int metadataToken)
    {
        ThrowIfDisposed();
        return SrmMetadataProjection.ProjectMethodCallTarget(
            _metadataReader,
            ModuleHandle,
            contextMethod,
            metadataToken);
    }

    /// <inheritdoc />
    public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
    {
        ThrowIfDisposed();
        return SrmMetadataProjection.ProjectField(
            _metadataReader,
            ModuleHandle,
            contextMethod,
            metadataToken);
    }

    /// <inheritdoc />
    public ResolutionResult<MethodBody> GetMethodBody(MethodHandle method)
    {
        ThrowIfDisposed();
        if (method.Module != ModuleHandle)
        {
            return ResolutionResult<MethodBody>.Failed(
                ResolutionFailureKind.Invalid,
                "META_MODULE_MISMATCH",
                "The supplied method handle belongs to a different module.");
        }

        try
        {
            if (!IsValidMethodDefinitionToken(method.MetadataToken))
            {
                return ResolutionResult<MethodBody>.Failed(
                    ResolutionFailureKind.Invalid,
                    "META_INVALID_METHOD_TOKEN",
                    "The supplied metadata token is not a valid MethodDef in this module.");
            }

            var methodDefinitionHandle = MetadataTokens.MethodDefinitionHandle(method.MetadataToken & 0x00FFFFFF);
            var methodDefinition = _metadataReader.GetMethodDefinition(methodDefinitionHandle);
            if (!SrmMetadataProjection.IsOrdinaryManagedIlImplementation(
                    methodDefinition.Attributes,
                    methodDefinition.ImplAttributes))
            {
                return ResolutionResult<MethodBody>.Failed(
                    ResolutionFailureKind.Unsupported,
                    "META_METHOD_IMPLEMENTATION_UNSUPPORTED",
                    "The selected method is not an ordinary managed-IL MethodDef.");
            }

            if (methodDefinition.RelativeVirtualAddress == 0)
            {
                return ResolutionResult<MethodBody>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "META_METHOD_BODY_UNAVAILABLE",
                    "The selected MethodDef has no managed IL body.");
            }

            var methodBody = _peReader.GetMethodBody(methodDefinition.RelativeVirtualAddress);
            var ilBytes = methodBody.GetILBytes();
            if (ilBytes is null)
            {
                return ResolutionResult<MethodBody>.Failed(
                    ResolutionFailureKind.Invalid,
                    "META_METHOD_BODY_INVALID",
                    "The managed method body did not expose an IL code span.");
            }

            if (ilBytes.Length > MaximumMethodBodyCodeBytes)
            {
                return ResolutionResult<MethodBody>.Failed(
                    ResolutionFailureKind.Unsupported,
                    "META_METHOD_BODY_SIZE_LIMIT",
                    "The method body exceeds the active deterministic execution profile.");
            }

            return ResolutionResult<MethodBody>.Success(
                MethodBody.Create(
                    methodBody.MaxStack,
                    ilBytes,
                    methodBody.LocalVariablesInitialized,
                    methodBody.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(methodBody.LocalSignature),
                    methodBody.ExceptionRegions.Length));
        }
        catch (BadImageFormatException)
        {
            return ResolutionResult<MethodBody>.Failed(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METHOD_BODY",
                "The managed artifact contains an invalid method body.");
        }
        catch (ArgumentOutOfRangeException)
        {
            return ResolutionResult<MethodBody>.Failed(
                ResolutionFailureKind.Invalid,
                "META_INVALID_METHOD_BODY",
                "The managed artifact contains an invalid method-body reference.");
        }
    }

    /// <summary>
    /// Releases the PE reader and underlying artifact stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _peReader.Dispose();
        _stream.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private bool IsValidMethodDefinitionToken(int token)
    {
        if ((token & unchecked((int)0xFF000000)) != MethodDefinitionTokenType)
        {
            return false;
        }

        var rowNumber = token & 0x00FFFFFF;
        return rowNumber > 0 && rowNumber <= _metadataReader.MethodDefinitions.Count;
    }

    private sealed class ArtifactSizeLimitExceededException : NotSupportedException
    {
        internal ArtifactSizeLimitExceededException(long maximumBytes)
            : base($"Managed PE artifacts are limited to {maximumBytes} bytes before hashing or parsing.")
        {
        }
    }
}
