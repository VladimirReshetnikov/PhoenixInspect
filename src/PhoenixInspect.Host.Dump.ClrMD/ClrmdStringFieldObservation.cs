using System.Collections.Immutable;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Reports a dump-backed string-field observation together with every raw memory read used to derive it.
/// </summary>
public sealed class ClrmdStringFieldObservation
{
    internal ClrmdStringFieldObservation(
        ClrmdEvidenceStatus status,
        ClrmdValueIssue issue,
        bool isNull,
        string? value,
        int? targetLength,
        ulong objectAddress,
        string fieldName,
        int? fieldMetadataToken,
        ulong? fieldAddress,
        ulong? stringAddress,
        ImmutableArray<MemoryReadResult> evidence)
    {
        Status = status;
        Issue = issue;
        IsNull = isNull;
        Value = value;
        TargetLength = targetLength;
        ObjectAddress = objectAddress;
        FieldName = fieldName;
        FieldMetadataToken = fieldMetadataToken;
        FieldAddress = fieldAddress;
        StringAddress = stringAddress;
        Evidence = evidence;
    }

    /// <summary>
    /// Gets whether the supported string value is exact, partial, unavailable, conflicting, or invalid.
    /// </summary>
    public ClrmdEvidenceStatus Status { get; }

    /// <summary>
    /// Gets the stable reason why the observation is not exact.
    /// </summary>
    public ClrmdValueIssue Issue { get; }

    /// <summary>
    /// Gets whether an exact field-reference read observed a null reference.
    /// </summary>
    public bool IsNull { get; }

    /// <summary>
    /// Gets the exact string, a known prefix for a partial observation, or <see langword="null"/> when unavailable or null.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets the target string length when its length field was read exactly.
    /// </summary>
    public int? TargetLength { get; }

    /// <summary>
    /// Gets the target address of the containing object.
    /// </summary>
    public ulong ObjectAddress { get; }

    /// <summary>
    /// Gets the requested field display name.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the FieldDef token when runtime field lookup succeeded.
    /// </summary>
    public int? FieldMetadataToken { get; }

    /// <summary>
    /// Gets the target address of the field storage when runtime layout lookup succeeded.
    /// </summary>
    public ulong? FieldAddress { get; }

    /// <summary>
    /// Gets the observed target string-object address when the field reference was read exactly and was non-null.
    /// </summary>
    public ulong? StringAddress { get; }

    /// <summary>
    /// Gets the ordered immutable raw reads used to derive the observation.
    /// </summary>
    public ImmutableArray<MemoryReadResult> Evidence { get; }
}
