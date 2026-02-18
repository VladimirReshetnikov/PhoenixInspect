using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Metadata.SRM;

/// <summary>
/// Provides a minimal SRM-backed metadata module implementation for draft integration seams.
/// </summary>
public sealed class SrmMetadataModule : IMetadataModule, IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadataReader;
    private readonly ConcurrentDictionary<(int Token, GenericContext Ctx), MethodHandle> _methodHandleByToken = new();
    private readonly ConcurrentDictionary<MethodHandle, int> _tokenByMethodHandle = new();
    private long _nextMethodHandle = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="SrmMetadataModule"/> class for the specified module file.
    /// </summary>
    /// <param name="modulePath">Path to a managed PE module.</param>
    public SrmMetadataModule(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
        {
            throw new ArgumentException("Module path is required.", nameof(modulePath));
        }

        _stream = File.OpenRead(modulePath);
        _peReader = new PEReader(_stream, PEStreamOptions.LeaveOpen);
        _metadataReader = _peReader.GetMetadataReader();

        var moduleDefinition = _metadataReader.GetModuleDefinition();
        var mvid = _metadataReader.GetGuid(moduleDefinition.Mvid);
        var peStamp = (_peReader.PEHeaders.CoffHeader.TimeDateStamp, (uint)_peReader.PEHeaders.PEHeader!.SizeOfImage);

        Id = new ModuleId(
            mvid,
            Name: Path.GetFileName(modulePath),
            PathHint: Path.GetFullPath(modulePath),
            PeStamp: peStamp);
        ModuleHandle = new ModuleHandle(ComputeStableHandleValue(modulePath));
    }

    /// <inheritdoc />
    public ModuleId Id { get; }

    /// <inheritdoc />
    public ModuleHandle ModuleHandle { get; }

    /// <summary>
    /// Creates an SRM module by opening the specified on-disk module path.
    /// </summary>
    /// <param name="modulePath">Path to the module that should be opened for metadata operations.</param>
    /// <returns>A new <see cref="SrmMetadataModule"/> instance.</returns>
    public static SrmMetadataModule LoadFromFile(string modulePath) => new(modulePath);

    /// <summary>
    /// Tries to locate a method-definition token by type and method name.
    /// </summary>
    /// <param name="typeName">Simple metadata type name.</param>
    /// <param name="methodName">Method name to resolve.</param>
    /// <param name="methodToken">Resolved method-definition token when the method is found.</param>
    /// <returns><see langword="true"/> when the method is found; otherwise <see langword="false"/>.</returns>
    public bool TryFindMethodDefToken(string typeName, string methodName, out int methodToken)
    {
        methodToken = default;

        foreach (var typeHandle in _metadataReader.TypeDefinitions)
        {
            var typeDefinition = _metadataReader.GetTypeDefinition(typeHandle);
            if (!string.Equals(_metadataReader.GetString(typeDefinition.Name), typeName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var candidateHandle in typeDefinition.GetMethods())
            {
                var methodDefinition = _metadataReader.GetMethodDefinition(candidateHandle);
                if (!string.Equals(_metadataReader.GetString(methodDefinition.Name), methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                methodToken = MetadataTokens.GetToken(candidateHandle);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public TypeHandle GetTypeHandle(int metadataToken, GenericContext ctx) =>
        throw new NotSupportedException("Type-handle resolution is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public MethodHandle GetMethodHandle(int metadataToken, GenericContext ctx)
    {
        var key = (metadataToken, ctx);
        return _methodHandleByToken.GetOrAdd(key, _ =>
        {
            var handle = new MethodHandle((ulong)Interlocked.Increment(ref _nextMethodHandle));
            _tokenByMethodHandle[handle] = metadataToken;
            return handle;
        });
    }

    /// <inheritdoc />
    public FieldHandle GetFieldHandle(int metadataToken, GenericContext ctx) =>
        throw new NotSupportedException("Field-handle resolution is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public TypeSig GetTypeSignature(TypeHandle type) =>
        throw new NotSupportedException("Type signature decoding is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public MethodSig GetMethodSignature(MethodHandle method) =>
        throw new NotSupportedException("Method signature decoding is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public FieldSig GetFieldSignature(FieldHandle field) =>
        throw new NotSupportedException("Field signature decoding is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public ResolvedType ResolveTypeToken(int token, GenericContext ctx) =>
        throw new NotSupportedException("Type token resolution is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public ResolvedField ResolveFieldToken(int token, GenericContext ctx) =>
        throw new NotSupportedException("Field token resolution is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public ResolvedMethod ResolveMethodToken(int token, GenericContext ctx) =>
        throw new NotSupportedException("Method token resolution is intentionally out of scope for this draft integration seam.");

    /// <inheritdoc />
    public bool TryGetMethodBody(MethodHandle method, out MethodBody body)
    {
        body = default!;
        if (!_tokenByMethodHandle.TryGetValue(method, out var token))
        {
            return false;
        }

        var methodDefinitionHandle = MetadataTokens.MethodDefinitionHandle(token);
        var methodDefinition = _metadataReader.GetMethodDefinition(methodDefinitionHandle);
        if (methodDefinition.RelativeVirtualAddress == 0)
        {
            return false;
        }

        var methodBody = _peReader.GetMethodBody(methodDefinition.RelativeVirtualAddress);
        body = new MethodBody(methodBody.MaxStack, methodBody.GetILBytes());
        return true;
    }

    /// <inheritdoc />
    public MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType) =>
        throw new NotSupportedException("Virtual override resolution is intentionally out of scope for this draft integration seam.");

    /// <summary>
    /// Releases open module streams and SRM readers.
    /// </summary>
    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
    }

    private static ulong ComputeStableHandleValue(string modulePath)
    {
        var normalized = Path.GetFullPath(modulePath);
        return (ulong)StringComparer.OrdinalIgnoreCase.GetHashCode(normalized);
    }
}
