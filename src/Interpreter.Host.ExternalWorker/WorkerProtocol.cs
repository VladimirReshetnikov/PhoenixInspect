using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Interpreter.Host.ExternalWorker;

internal static class WorkerProtocol
{
    private const uint Magic = 0x31575049; // IWP1
    private const ushort Version = 1;
    private const ushort RequestKind = 1;
    private const ushort ResponseKind = 2;
    private const int HeaderLength = 12;
    internal const int MaximumRequestBytes = ExternalWorkerPolicy.MaximumRequestPayloadBytes;
    internal const int MaximumResponseBytes = ExternalWorkerPolicy.MaximumResponsePayloadBytes;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static void WriteRequest(Stream stream, ExternalDumpQueryRequest request, int loopbackProbePort) =>
        WriteFrame(
            stream,
            RequestKind,
            JsonSerializer.SerializeToUtf8Bytes(new WorkerRequestEnvelope(request, loopbackProbePort), SerializerOptions),
            MaximumRequestBytes);

    internal static WorkerRequestEnvelope ReadRequest(Stream stream) =>
        JsonSerializer.Deserialize<WorkerRequestEnvelope>(
            ReadFrame(stream, RequestKind, MaximumRequestBytes),
            SerializerOptions)
        ?? throw new InvalidDataException("The request payload is missing.");

    internal static void WriteResponse(Stream stream, ExternalDumpQueryResponse response) =>
        WriteFrame(stream, ResponseKind, JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions), MaximumResponseBytes);

    internal static ExternalDumpQueryResponse ReadResponse(Stream stream) =>
        JsonSerializer.Deserialize<ExternalDumpQueryResponse>(
            ReadFrame(stream, ResponseKind, MaximumResponseBytes),
            SerializerOptions)
        ?? throw new InvalidDataException("The response payload is missing.");

    private static void WriteFrame(Stream stream, ushort kind, byte[] payload, int maximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length > maximumPayloadBytes)
        {
            throw new InvalidDataException("The protocol payload exceeds its fixed bound.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], kind);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], payload.Length);
        stream.Write(header);
        stream.Write(payload);
        stream.Flush();
    }

    private static byte[] ReadFrame(Stream stream, ushort expectedKind, int maximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Span<byte> header = stackalloc byte[HeaderLength];
        ReadExactly(stream, header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[4..]) != Version ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[6..]) != expectedKind)
        {
            throw new InvalidDataException("The protocol frame header is invalid.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        if (length < 0 || length > maximumPayloadBytes)
        {
            throw new InvalidDataException("The protocol frame length is invalid.");
        }

        var payload = GC.AllocateUninitializedArray<byte>(length);
        ReadExactly(stream, payload);
        return payload;
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            var count = stream.Read(destination);
            if (count == 0)
            {
                throw new EndOfStreamException("The protocol frame ended before its declared length.");
            }

            destination = destination[count..];
        }
    }
}

internal sealed record WorkerRequestEnvelope(
    ExternalDumpQueryRequest Query,
    int LoopbackProbePort);
