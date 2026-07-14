using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Interpreter.Core.Abstractions;

namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Freezes the exact dump-object, runtime-field, metadata-field, and counted Int32 observation admitted for one
/// dump-grounded execution.
/// </summary>
/// <remarks>
/// Instances are created only by <see cref="ClrmdDumpExecutionResolver.CorrelateExactInt32Field"/> after every
/// participating result is exact and all snapshot, module, owner, token, type, address, and byte-count identities
/// agree. The descriptor does not itself mutate an execution memory model; callers must explicitly import it into
/// the chosen domain so non-exact ClrMD observations cannot become ordinary semantic values by accident.
/// </remarks>
public sealed class ClrmdExactInt32FieldExecutionEvidence
{
    private const string OwnerIdentityVersion = "clrmd-imported-object:v1";
    private static ReadOnlySpan<byte> OwnerIdentityDomain =>
        "Interpreter.ClrmdImportedObjectEvidence.v1"u8;

    internal ClrmdExactInt32FieldExecutionEvidence(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ResolvedMethodDefinition method,
        ResolvedField field,
        ClrmdHeapObjectSearchResult ownerSearch,
        ClrmdHeapObjectInfo owner,
        ClrmdInt32FieldObservation observation,
        int value)
    {
        RuntimeModule = runtimeModule;
        Method = method;
        Field = field;
        OwnerSearch = ownerSearch;
        Owner = owner;
        Observation = observation;
        Value = value;
        OwnerEvidenceIdentity = CreateOwnerEvidenceIdentity(
            runtimeModule,
            owner.Address,
            owner.MethodTable);
    }

    /// <summary>Gets the snapshot-scoped runtime module whose counted metadata projected the descriptors.</summary>
    public ClrmdRuntimeModuleIdentity RuntimeModule { get; }

    /// <summary>Gets the immutable method body and metadata-derived activation shape used by execution.</summary>
    public ResolvedMethodDefinition Method { get; }

    /// <summary>Gets the exact same-module FieldDef descriptor correlated with the runtime field token.</summary>
    public ResolvedField Field { get; }

    /// <summary>
    /// Gets the exact bounded strong-handle search that uniquely selected <see cref="Owner"/>.
    /// </summary>
    public ClrmdHeapObjectSearchResult OwnerSearch { get; }

    /// <summary>Gets the uniquely selected snapshot object whose instance field was observed.</summary>
    public ClrmdHeapObjectInfo Owner { get; }

    /// <summary>Gets the runtime field descriptor and its sole counted four-byte memory read.</summary>
    public ClrmdInt32FieldObservation Observation { get; }

    /// <summary>Gets the exact little-endian Int32 decoded from the complete counted field read.</summary>
    public int Value { get; }

    /// <summary>
    /// Gets a bounded, versioned identity suitable for <c>ImportedObjectEvidenceIdentity</c> in a test or host
    /// composition that explicitly imports this evidence into a concrete semantic memory.
    /// </summary>
    /// <remarks>
    /// The fixed-width digest binds dump, loader-instance, object-address, and method-table provenance without
    /// copying a potentially long source identifier into the concrete-memory key. It is replay material rather than
    /// display text and must not be used as a target-memory address by the interpreter.
    /// </remarks>
    public string OwnerEvidenceIdentity { get; }

    /// <summary>
    /// Creates the fixed-width imported-object identity used after exact owner correlation.
    /// </summary>
    /// <param name="runtimeModule">The complete snapshot-scoped runtime module identity.</param>
    /// <param name="ownerAddress">The nonzero target address of the selected object.</param>
    /// <param name="ownerMethodTable">The nonzero method-table identity validated for the object.</param>
    /// <returns>A versioned lowercase SHA-256 projection whose length is independent of source-identity length.</returns>
    internal static string CreateOwnerEvidenceIdentity(
        ClrmdRuntimeModuleIdentity runtimeModule,
        ulong ownerAddress,
        ulong ownerMethodTable)
    {
        var sourceBytes = Encoding.UTF8.GetBytes(runtimeModule.SourceId);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(OwnerIdentityDomain);

        Span<byte> sourceLength = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(sourceLength, sourceBytes.Length);
        hash.AppendData(sourceLength);
        hash.AppendData(sourceBytes);

        Span<byte> ownerIdentity = stackalloc byte[sizeof(ulong) * 2];
        BinaryPrimitives.WriteUInt64BigEndian(ownerIdentity, ownerAddress);
        BinaryPrimitives.WriteUInt64BigEndian(ownerIdentity[sizeof(ulong)..], ownerMethodTable);
        hash.AppendData(ownerIdentity);

        var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLower(CultureInfo.InvariantCulture);
        return $"{OwnerIdentityVersion}:sha256:{digest}";
    }
}
