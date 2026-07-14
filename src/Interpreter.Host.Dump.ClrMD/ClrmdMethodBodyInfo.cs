using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Carries a fully counted managed method body whose metadata, header, code, and declared extra sections were read
/// from one immutable dump snapshot.
/// </summary>
/// <remarks>
/// Instances are created only for an exact <see cref="ClrmdEvidenceResult{T}"/>. Partial or unavailable reads retain
/// their observed byte prefixes in the result evidence but never expose this type or an executable
/// <see cref="MethodBody"/>. The normalized body remains a draft prototype shape and does not imply support for
/// executing locals or exception handlers.
/// </remarks>
public sealed class ClrmdMethodBodyInfo
{
    internal ClrmdMethodBodyInfo(
        int metadataToken,
        int relativeVirtualAddress,
        ulong headerAddress,
        ClrmdMethodHeaderKind headerKind,
        ImmutableArray<MemoryReadResult> headerEvidence,
        ulong codeAddress,
        MemoryReadResult code,
        ImmutableArray<MemoryReadResult> extraSectionEvidence,
        MethodBody body)
    {
        MetadataToken = metadataToken;
        RelativeVirtualAddress = relativeVirtualAddress;
        HeaderAddress = headerAddress;
        HeaderKind = headerKind;
        HeaderEvidence = headerEvidence;
        CodeAddress = codeAddress;
        Code = code;
        ExtraSectionEvidence = extraSectionEvidence;
        Body = body;
    }

    /// <summary>Gets the MethodDef token validated against the counted dump metadata image.</summary>
    public int MetadataToken { get; }

    /// <summary>Gets the MethodDef relative virtual address decoded from the counted dump metadata image.</summary>
    public int RelativeVirtualAddress { get; }

    /// <summary>Gets the target virtual address of the first physical method-header byte.</summary>
    public ulong HeaderAddress { get; }

    /// <summary>Gets the physical tiny or fat header encoding observed in dump memory.</summary>
    public ClrmdMethodHeaderKind HeaderKind { get; }

    /// <summary>
    /// Gets the ordered, non-overlapping counted reads that comprise the complete physical method header.
    /// </summary>
    public ImmutableArray<MemoryReadResult> HeaderEvidence { get; }

    /// <summary>Gets the target virtual address of the first IL instruction byte.</summary>
    public ulong CodeAddress { get; }

    /// <summary>Gets the exact counted IL instruction-byte read from dump memory.</summary>
    public MemoryReadResult Code { get; }

    /// <summary>
    /// Gets ordered counted reads for alignment padding and every declared extra-section header and payload.
    /// </summary>
    /// <remarks>A method without extra sections exposes an empty array.</remarks>
    public ImmutableArray<MemoryReadResult> ExtraSectionEvidence { get; }

    /// <summary>
    /// Gets the normalized immutable body built solely from exact dump metadata and memory evidence.
    /// </summary>
    /// <remarks>
    /// Availability of this property proves physical evidence completeness, not interpreter admission. The execution
    /// layer still rejects unsupported locals, exception regions, opcodes, and resource requirements.
    /// </remarks>
    public MethodBody Body { get; }

    /// <summary>Gets the declared maximum evaluation-stack depth decoded from the dump method header.</summary>
    public int MaxStack => Body.MaxStack;

    /// <summary>Gets whether the dump method header requests zero-initialized locals.</summary>
    public bool LocalVariablesInitialized => Body.LocalVariablesInitialized;

    /// <summary>Gets the normalized StandAloneSig token, or zero when the dump method header declares none.</summary>
    public int LocalSignatureToken => Body.LocalSignatureToken;

    /// <summary>Gets the number of exception clauses counted across all declared dump method data sections.</summary>
    public int ExceptionRegionCount => Body.ExceptionRegionCount;
}
