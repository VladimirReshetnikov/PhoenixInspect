using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Preserves one counted outer-field pointer read without fabricating missing reference bytes.
/// </summary>
/// <remarks>
/// This W6 observation is distinct from target-object validation. Exact zero is a truthful null reference;
/// partial and unavailable reads retain only their observed prefix and expose no target address.
/// </remarks>
public sealed class ClrmdObjectReferenceObservation
{
    private const string CanonicalVersion = "clrmd-object-reference-v1";

    internal ClrmdObjectReferenceObservation(
        ClrmdInstanceFieldInfo field,
        MemoryReadResult memory,
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        ulong? targetAddress)
    {
        Field = field;
        Memory = memory;
        Status = status;
        Issue = issue;
        TargetAddress = targetAddress;
    }

    /// <summary>Gets the already-certified outer reference field whose pointer bytes were read.</summary>
    public ClrmdInstanceFieldInfo Field { get; }

    /// <summary>Gets the sole counted pointer-width memory read.</summary>
    public MemoryReadResult Memory { get; }

    /// <summary>Gets the exact, partial, unavailable, conflicting, or invalid observation status.</summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>Gets the stable reason associated with <see cref="Status"/>.</summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets the exact decoded pointer, including zero, or <see langword="null"/> when all pointer bytes were not read.
    /// </summary>
    public ulong? TargetAddress { get; }

    /// <summary>Gets whether complete pointer bytes prove an exact null reference.</summary>
    public bool IsExactNull => Status == ClrmdEvidenceStatus.Exact && TargetAddress == 0;

    /// <summary>Gets whether complete pointer bytes prove an exact non-null reference.</summary>
    public bool IsExactNonNull => Status == ClrmdEvidenceStatus.Exact && TargetAddress is > 0;

    /// <summary>Produces the deterministic replay projection of field identity and exact observed pointer evidence.</summary>
    /// <returns>A length-delimited representation containing no inferred target facts.</returns>
    public string ToCanonicalReplayProjection()
    {
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, Field.ToCanonicalReplayProjection());
        Append(builder, Status.ToString());
        Append(builder, Issue.ToString());
        Append(builder, Memory.SourceId);
        Append(builder, Memory.Address.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Memory.RequestedLength.ToString(CultureInfo.InvariantCulture));
        Append(builder, Convert.ToHexString(Memory.Bytes.AsSpan()));
        Append(builder, TargetAddress?.ToString("x16", CultureInfo.InvariantCulture) ?? "none");
        return builder.ToString();
    }

    internal static ClrmdEvidenceResult<ClrmdObjectReferenceObservation> Project(
        ClrmdInstanceFieldInfo field,
        int pointerSize,
        MemoryReadResult memory)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(memory);
        if (pointerSize is not (sizeof(uint) or sizeof(ulong)) ||
            field.Size != pointerSize ||
            !field.IsObjectReference ||
            memory.Address != field.Address ||
            memory.RequestedLength != pointerSize)
        {
            var invalid = new ClrmdObjectReferenceObservation(
                field,
                memory,
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                targetAddress: null);
            return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData,
                invalid,
                [memory]);
        }

        if (memory.Status != MemoryReadStatus.Exact)
        {
            var status = memory.Status == MemoryReadStatus.Partial
                ? ClrmdEvidenceStatus.Partial
                : ClrmdEvidenceStatus.Unavailable;
            var incomplete = new ClrmdObjectReferenceObservation(
                field,
                memory,
                status,
                ClrmdValueIssue.MemoryUnavailable,
                targetAddress: null);
            return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
                status,
                ClrmdValueIssue.MemoryUnavailable,
                incomplete,
                [memory]);
        }

        var address = pointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(memory.Bytes.AsSpan())
            : BinaryPrimitives.ReadUInt64LittleEndian(memory.Bytes.AsSpan());
        var exact = new ClrmdObjectReferenceObservation(
            field,
            memory,
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            address);
        return ClrmdEvidenceResult<ClrmdObjectReferenceObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            exact,
            [memory]);
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}
