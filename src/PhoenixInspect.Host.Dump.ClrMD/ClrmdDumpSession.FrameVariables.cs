using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Diagnostics.Runtime;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>One parameter of a selected frame's method, decoded from the frame module's own metadata.</summary>
/// <param name="Ordinal">The zero-based display position, with <c>this</c> first for an instance method.</param>
/// <param name="Name">The Param-row name, <c>this</c> for the instance receiver, or a positional placeholder.</param>
/// <param name="TypeDisplay">The C#-flavored type spelling decoded from the signature blob.</param>
/// <param name="IsThis">Whether this row is the instance receiver rather than a declared parameter.</param>
public sealed record ClrmdFrameParameterInfo(int Ordinal, string Name, string TypeDisplay, bool IsThis);

/// <summary>One local variable slot of a selected frame's method.</summary>
/// <param name="Slot">The zero-based IL local slot index.</param>
/// <param name="TypeDisplay">The C#-flavored type spelling decoded from the local signature blob.</param>
/// <param name="Name">
/// The source name recorded by the identity-matching Portable PDB, or null when no scope names the slot or no
/// matching PDB was available. A slot the compiler uses without naming stays honestly unnamed.
/// </param>
/// <param name="IsInScopeAtCurrentOffset">
/// Whether a PDB local scope that names the slot covers the frame's IL offset; a named slot outside every covering
/// scope is shown the way a debugger grays an out-of-scope local.
/// </param>
/// <param name="IsDebuggerHidden">Whether the PDB marks the slot debugger-hidden.</param>
public sealed record ClrmdFrameLocalSlotInfo(
    int Slot,
    string TypeDisplay,
    string? Name,
    bool IsInScopeAtCurrentOffset,
    bool IsDebuggerHidden);

/// <summary>
/// The declared variables of one selected frame: its parameters and its local variable slots, together with honest
/// statements of what could not be decoded.
/// </summary>
/// <remarks>
/// This is declaration evidence, not value evidence: the adapter publishes no register or stack-slot mapping for a
/// managed frame, so no variable value is read or fabricated. Parameters come from the frame module's counted
/// metadata, slot types from the method body's local signature in dump memory, and names from the
/// identity-validated Portable PDB's local scopes.
/// </remarks>
public sealed class ClrmdFrameVariables
{
    internal ClrmdFrameVariables(
        ImmutableArray<ClrmdFrameParameterInfo> parameters,
        ImmutableArray<ClrmdFrameLocalSlotInfo> localSlots,
        string? localSlotsNote,
        string? localNamesNote)
    {
        Parameters = parameters;
        LocalSlots = localSlots;
        LocalSlotsNote = localSlotsNote;
        LocalNamesNote = localNamesNote;
    }

    /// <summary>Gets the parameters in display order, with <c>this</c> first for an instance method.</summary>
    public ImmutableArray<ClrmdFrameParameterInfo> Parameters { get; }

    /// <summary>Gets the local variable slots in slot order; empty when the method declares none.</summary>
    public ImmutableArray<ClrmdFrameLocalSlotInfo> LocalSlots { get; }

    /// <summary>Gets why the local slots could not be decoded, or null when they were (or none exist).</summary>
    public string? LocalSlotsNote { get; }

    /// <summary>Gets why no Portable-PDB names could be attached to the slots, or null when they were.</summary>
    public string? LocalNamesNote { get; }
}

public sealed partial class ClrmdDumpSession
{
    /// <summary>
    /// Decodes the parameters and local variable slots of one exact selected frame, the way a debugger's Locals
    /// window lists them: names and types, never guessed values.
    /// </summary>
    /// <param name="frame">An exact frame identity produced by <see cref="SelectExpressionFrame"/>.</param>
    /// <param name="portablePdbCandidates">
    /// Local Portable-PDB candidate paths offered for slot names; each is identity-validated before use. An empty
    /// array skips names, and the result says so.
    /// </param>
    /// <returns>
    /// An exact result whose parameters decoded from the frame module's counted metadata; sections that could not
    /// be decoded carry their explanation on the payload instead of failing the whole answer. A frame whose module
    /// metadata is not completely present is a typed non-exact result.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
    /// <exception cref="ArgumentException">A candidate path is null or blank.</exception>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    public ClrmdEvidenceResult<ClrmdFrameVariables> DescribeFrameVariables(
        DumpSelectedFrameIdentity frame,
        ImmutableArray<string> portablePdbCandidates)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(frame);
        var candidates = portablePdbCandidates.IsDefault ? [] : portablePdbCandidates;
        if (candidates.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Portable-PDB candidate paths cannot be null or blank.",
                nameof(portablePdbCandidates));
        }

        if (frame.Selector.Snapshot != Snapshot)
        {
            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.SnapshotMismatch);
        }

        if (!_moduleInfos.TryGetValue(
                (frame.RuntimeModule.AppDomainAddress, frame.RuntimeModule.ModuleAddress),
                out var moduleInfo) ||
            !moduleInfo.Identity.Equals(frame.RuntimeModule) ||
            !_runtimeModules.TryGetValue(moduleInfo.Identity, out var runtimeModule))
        {
            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.ModuleUnavailable);
        }

        if (moduleInfo.MetadataAddress == 0 ||
            moduleInfo.MetadataLength == 0 ||
            moduleInfo.MetadataLength > (ulong)Memory.MaximumReadLength ||
            moduleInfo.MetadataAddress > ulong.MaxValue - (moduleInfo.MetadataLength - 1))
        {
            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MetadataUnavailable);
        }

        var metadataRead = Memory.Read(moduleInfo.MetadataAddress, checked((int)moduleInfo.MetadataLength));
        var evidence = ImmutableArray.Create(metadataRead);
        if (metadataRead.Status != MemoryReadStatus.Exact)
        {
            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
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
            if (methodHandle.IsNil ||
                MetadataTokens.GetRowNumber(methodHandle) > reader.MethodDefinitions.Count)
            {
                return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData,
                    evidence: evidence);
            }

            var method = reader.GetMethodDefinition(methodHandle);
            var genericContext = new FrameMethodGenericContext(
                CollectGenericParameterNames(reader, method.GetDeclaringType()),
                CollectGenericParameterNames(reader, methodHandle));

            var parameters = DecodeParameters(reader, method, genericContext);
            var (localSlots, localSlotsNote) = DecodeLocalSlots(
                reader,
                method,
                methodHandle,
                moduleInfo,
                runtimeModule,
                genericContext);
            var localNamesNote = localSlots.IsEmpty
                ? null
                : "The Portable-PDB name pass did not run because there are no slots to name.";
            if (!localSlots.IsEmpty)
            {
                (localSlots, localNamesNote) = AttachLocalNames(
                    frame,
                    moduleInfo,
                    runtimeModule,
                    localSlots,
                    candidates);
            }

            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                ClrmdEvidenceStatus.Exact,
                ClrmdValueIssue.None,
                new ClrmdFrameVariables(parameters, localSlots, localSlotsNote, localNamesNote),
                evidence);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or ArgumentOutOfRangeException or ArgumentException)
        {
            return ClrmdEvidenceResult<ClrmdFrameVariables>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                evidence: evidence);
        }
    }

    /// <summary>Decodes the parameter rows, pairing signature types with Param-row names.</summary>
    private static ImmutableArray<ClrmdFrameParameterInfo> DecodeParameters(
        MetadataReader reader,
        MethodDefinition method,
        FrameMethodGenericContext genericContext)
    {
        var signature = method.DecodeSignature(FrameMethodSignatureProvider.Instance, genericContext);
        var rows = ImmutableArray.CreateBuilder<ClrmdFrameParameterInfo>();
        var ordinal = 0;
        if (signature.Header.IsInstance)
        {
            var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
            var typeName = reader.GetString(declaringType.Name);
            var arity = typeName.IndexOf('`');
            rows.Add(new ClrmdFrameParameterInfo(
                ordinal++,
                "this",
                arity < 0 ? typeName : typeName[..arity],
                IsThis: true));
        }

        var names = new string?[signature.ParameterTypes.Length];
        foreach (var parameterHandle in method.GetParameters())
        {
            var parameter = reader.GetParameter(parameterHandle);
            if (parameter.SequenceNumber >= 1 &&
                parameter.SequenceNumber <= signature.ParameterTypes.Length &&
                !parameter.Name.IsNil)
            {
                names[parameter.SequenceNumber - 1] = reader.GetString(parameter.Name);
            }
        }

        for (var index = 0; index < signature.ParameterTypes.Length; index++)
        {
            rows.Add(new ClrmdFrameParameterInfo(
                ordinal++,
                names[index] is { Length: > 0 } name ? name : $"arg{index}",
                signature.ParameterTypes[index],
                IsThis: false));
        }

        return rows.ToImmutable();
    }

    /// <summary>
    /// Reads the method body from dump memory and decodes its local signature into typed slots. Every failure is a
    /// note rather than an error, because parameters remain valid evidence without the body.
    /// </summary>
    private (ImmutableArray<ClrmdFrameLocalSlotInfo> Slots, string? Note) DecodeLocalSlots(
        MetadataReader reader,
        MethodDefinition method,
        MethodDefinitionHandle methodHandle,
        ClrmdModuleInfo moduleInfo,
        ClrModule runtimeModule,
        FrameMethodGenericContext genericContext)
    {
        var implementation = method.ImplAttributes;
        if ((implementation & System.Reflection.MethodImplAttributes.CodeTypeMask) !=
                System.Reflection.MethodImplAttributes.IL ||
            (implementation & System.Reflection.MethodImplAttributes.ManagedMask) !=
                System.Reflection.MethodImplAttributes.Managed)
        {
            return ([], "The method is not managed IL, so it declares no IL local slots.");
        }

        if (method.RelativeVirtualAddress <= 0)
        {
            return ([], "The method has no IL body in the image, so it declares no local slots.");
        }

        if (runtimeModule.IsDynamic || !runtimeModule.IsPEFile ||
            runtimeModule.Layout is not (ModuleLayout.Mapped or ModuleLayout.Loaded) ||
            moduleInfo.Identity.ImageBase == 0 || moduleInfo.Identity.ImageSize == 0)
        {
            return ([], "The module's image layout does not support locating the method body in dump memory.");
        }

        var body = ClrmdMethodBodyParser.Read(
            Memory,
            MetadataTokens.GetToken(methodHandle),
            method.RelativeVirtualAddress,
            moduleInfo.Identity.ImageBase,
            moduleInfo.Identity.ImageSize,
            reader.GetTableRowCount(TableIndex.StandAloneSig),
            ImmutableArray<MemoryReadResult>.Empty);
        if (body.Status != ClrmdEvidenceStatus.Exact || body.Value is null)
        {
            return ([], $"The method body read from dump memory ended {body.Status} ({body.Issue}), "
                + "so the local slots cannot be decoded.");
        }

        var localSignatureToken = body.Value.LocalSignatureToken;
        if (localSignatureToken == 0)
        {
            return ([], null);
        }

        if ((localSignatureToken >>> 24) != 0x11 ||
            (localSignatureToken & 0x00FF_FFFF) is var signatureRow &&
            (signatureRow == 0 || signatureRow > reader.GetTableRowCount(TableIndex.StandAloneSig)))
        {
            return ([], "The method header's local-signature token does not name a StandAloneSig row.");
        }

        var standalone = reader.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(signatureRow));
        var types = standalone.DecodeLocalSignature(FrameMethodSignatureProvider.Instance, genericContext);
        var slots = ImmutableArray.CreateBuilder<ClrmdFrameLocalSlotInfo>(types.Length);
        for (var slot = 0; slot < types.Length; slot++)
        {
            slots.Add(new ClrmdFrameLocalSlotInfo(
                slot,
                types[slot],
                Name: null,
                IsInScopeAtCurrentOffset: false,
                IsDebuggerHidden: false));
        }

        return (slots.ToImmutable(), null);
    }

    /// <summary>
    /// Attaches Portable-PDB names to the decoded slots through the same identity validation the source and
    /// expression-context paths use: only a candidate whose CodeView identity matches the mapped module may name
    /// anything. The innermost scope covering the frame's IL offset wins; a slot named only by non-covering scopes
    /// keeps that name but is marked out of scope.
    /// </summary>
    private (ImmutableArray<ClrmdFrameLocalSlotInfo> Slots, string? Note) AttachLocalNames(
        DumpSelectedFrameIdentity frame,
        ClrmdModuleInfo moduleInfo,
        ClrModule runtimeModule,
        ImmutableArray<ClrmdFrameLocalSlotInfo> slots,
        ImmutableArray<string> candidates)
    {
        if (candidates.IsEmpty)
        {
            return (slots, "No Portable-PDB candidate was offered, so the slots keep their types without names.");
        }

        if (candidates.Length > MaximumPortablePdbCandidateCount)
        {
            return (slots, "The offered candidate list exceeds the declared candidate bound; no name was read.");
        }

        var moduleDebug = ReadModulePortablePdbDebugIdentity(runtimeModule, moduleInfo, frame.ModuleContent);
        if (moduleDebug.Status != DumpContextEvidenceStatus.Exact || moduleDebug.Identity is null)
        {
            return (slots, $"The module's CodeView identity could not be read ({moduleDebug.Issue}), "
                + "so no candidate can be validated and the slots keep their types without names.");
        }

        var bounds = FrameSourceBounds();
        var reads = ReadPortablePdbCandidates(
            new PathPortablePdbArtifactResolver(candidates),
            moduleDebug.Identity,
            bounds,
            out var candidateFailure);
        if (reads.Length == 0)
        {
            return (slots, candidateFailure is { } failure
                ? $"No Portable-PDB candidate could be read ({failure.Issue})."
                : "No offered Portable-PDB candidate exists on this machine.");
        }

        var matching = reads
            .Where(candidate => candidate.Artifact.DebugIdentity.Equals(moduleDebug.Identity.DebugIdentity))
            .GroupBy(candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static candidate => candidate.Artifact.Content.Sha256, StringComparer.Ordinal)
            .ToArray();
        if (matching.Length != 1)
        {
            return (slots, matching.Length == 0
                ? "No offered candidate matches this module's CodeView identity; a file with the right name is "
                    + "not evidence."
                : "Multiple distinct candidates match the module identity; no one artifact can name the slots.");
        }

        try
        {
            using var pdbProvider = MetadataReaderProvider.FromPortablePdbImage(matching[0].Bytes);
            var pdbReader = pdbProvider.GetMetadataReader();
            var methodRow = frame.MethodDefinitionToken & 0x00FF_FFFF;
            if ((frame.MethodDefinitionToken >>> 24) != 0x06 || methodRow == 0 ||
                methodRow > pdbReader.GetTableRowCount(TableIndex.MethodDebugInformation))
            {
                return (slots, "The identity-matching PDB has no debug row for this method.");
            }

            // Per slot: a covering scope's variable wins, the narrowest covering scope first; a variable from a
            // non-covering scope supplies the name but leaves the slot marked out of scope at this offset.
            var chosen = new (string Name, bool InScope, bool Hidden, int ScopeLength)?[slots.Length];
            var scopeCount = 0;
            foreach (var scopeHandle in pdbReader.GetLocalScopes(
                MetadataTokens.MethodDefinitionHandle(methodRow)))
            {
                if (++scopeCount > MaximumPortablePdbLocalScopeCount)
                {
                    return (slots, "The method's local-scope count exceeds the declared bound; names were not read.");
                }

                var scope = pdbReader.GetLocalScope(scopeHandle);
                var covers = scope.StartOffset <= frame.Instruction.IlOffset &&
                    frame.Instruction.IlOffset < scope.EndOffset;
                foreach (var variableHandle in scope.GetLocalVariables())
                {
                    var variable = pdbReader.GetLocalVariable(variableHandle);
                    if (variable.Index < 0 || variable.Index >= slots.Length || variable.Name.IsNil)
                    {
                        continue;
                    }

                    var name = pdbReader.GetString(variable.Name);
                    var hidden = (variable.Attributes & LocalVariableAttributes.DebuggerHidden) != 0;
                    var current = chosen[variable.Index];
                    var replace = current is null ||
                        (covers && (!current.Value.InScope || scope.Length < current.Value.ScopeLength));
                    if (replace)
                    {
                        chosen[variable.Index] = (name, covers, hidden, scope.Length);
                    }
                }
            }

            var named = ImmutableArray.CreateBuilder<ClrmdFrameLocalSlotInfo>(slots.Length);
            foreach (var slot in slots)
            {
                named.Add(chosen[slot.Slot] is { } row
                    ? slot with { Name = row.Name, IsInScopeAtCurrentOffset = row.InScope, IsDebuggerHidden = row.Hidden }
                    : slot);
            }

            return (named.ToImmutable(), null);
        }
        catch (Exception exception) when (exception is BadImageFormatException or InvalidDataException)
        {
            return (slots, "The identity-matching PDB could not be decoded, so the slots keep their types "
                + "without names.");
        }
    }
}
