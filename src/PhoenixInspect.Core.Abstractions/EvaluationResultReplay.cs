using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace PhoenixInspect.Core.Abstractions;

/// <summary>Produces a canonical replay artifact for the common multi-axis evaluation envelope.</summary>
/// <remarks>
/// The caller supplies a deterministic value projection because value types remain product-specific. The
/// projection must cover every semantically observable value field and must not use process-local identities,
/// ambient culture, clocks, or unordered iteration. Replay artifacts can contain target-derived data and are not
/// suitable for diagnostic display merely because they are deterministic.
/// </remarks>
public static class EvaluationResultReplay
{
    /// <summary>Serializes one evaluation envelope in a fixed UTF-8 JSON property order.</summary>
    /// <typeparam name="TValue">The immutable result value projection.</typeparam>
    /// <param name="result">The result to serialize.</param>
    /// <param name="projectValue">
    /// A deterministic, content-derived string projection for a present value. The delegate is not called when the
    /// result has no value.
    /// </param>
    /// <returns>Canonical UTF-8 JSON bytes suitable for replay comparison and hashing.</returns>
    public static byte[] SerializeCanonical<TValue>(
        EvaluationResult<TValue> result,
        Func<TValue, string> projectValue)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(projectValue);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("semanticMode", result.SemanticMode.ToString());
            writer.WriteString("completion", result.Completion.ToString());
            writer.WriteString("completeness", result.Completeness.ToString());
            writer.WriteString("evidence", result.Evidence.ToString());
            writer.WriteString("effects", result.Effects.ToString());
            writer.WriteStartObject("context");
            writer.WriteString("sourceKind", result.Context.SourceKind.ToString());
            WriteIdentity(writer, "snapshot", result.Context.Snapshot);
            WriteIdentity(writer, "module", result.Context.Module);
            writer.WriteStartObject("fallback");
            writer.WriteString("status", result.Context.Fallback.Status.ToString());
            writer.WriteString("name", result.Context.Fallback.Name);
            writer.WriteEndObject();
            writer.WriteStartArray("bounds");
            foreach (var bound in result.Context.Bounds)
            {
                writer.WriteStartObject();
                writer.WriteString("name", bound.Name);
                writer.WriteNumber("value", bound.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            if (result.Value is null)
            {
                writer.WriteNull("value");
            }
            else
            {
                var projection = projectValue(result.Value)
                    ?? throw new InvalidOperationException("The canonical value projection returned null.");
                writer.WriteString("value", projection);
            }

            writer.WriteStartArray("provenance");
            foreach (var item in result.Provenance)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", item.Kind.ToString());
                writer.WriteString("sourceId", item.SourceId);
                if (item.Address is ulong address)
                {
                    writer.WriteString("address", $"0x{address:X16}");
                }
                else
                {
                    writer.WriteNull("address");
                }

                if (item.RequestedLength is int requestedLength)
                {
                    writer.WriteNumber("requestedLength", requestedLength);
                }
                else
                {
                    writer.WriteNull("requestedLength");
                }

                if (item.ObservedLength is int observedLength)
                {
                    writer.WriteNumber("observedLength", observedLength);
                }
                else
                {
                    writer.WriteNull("observedLength");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            foreach (var item in result.Diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("code", item.Code);
                writer.WriteString("message", item.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Computes a lowercase SHA-256 replay fingerprint over canonical envelope bytes.</summary>
    /// <typeparam name="TValue">The immutable result value projection.</typeparam>
    /// <param name="result">The result to fingerprint.</param>
    /// <param name="projectValue">The deterministic value projection used by <see cref="SerializeCanonical{TValue}"/>.</param>
    /// <returns>A 64-character lowercase SHA-256 digest.</returns>
    public static string ComputeSha256<TValue>(
        EvaluationResult<TValue> result,
        Func<TValue, string> projectValue)
        where TValue : class =>
        Convert.ToHexString(SHA256.HashData(SerializeCanonical(result, projectValue))).ToLowerInvariant();

    private static void WriteIdentity(
        Utf8JsonWriter writer,
        string propertyName,
        EvaluationEvidenceIdentity identity)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("availability", identity.Availability.ToString());
        if (identity.SourceId is null)
        {
            writer.WriteNull("sourceId");
        }
        else
        {
            writer.WriteString("sourceId", identity.SourceId);
        }

        writer.WriteEndObject();
    }
}
