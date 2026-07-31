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
public sealed record ClrmdFrameMethodName(
    string DeclaringTypeFullName,
    string MethodName,
    string ModuleName)
{
    /// <summary>Gets the one-line spelling a call stack displays.</summary>
    public string DisplayName => $"{DeclaringTypeFullName}.{MethodName}";
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
                new ClrmdFrameMethodName(declaringTypeFullName, methodName, module.Name),
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
}
