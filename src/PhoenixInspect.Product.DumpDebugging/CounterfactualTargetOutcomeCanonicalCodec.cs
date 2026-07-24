using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>
/// Serializes validated target-outcome fragments into the domain-separated canonical W4 binary schema.
/// </summary>
/// <remarks>
/// Every numeric value is big-endian, every variable-length byte sequence has a signed 32-bit byte-length prefix,
/// and every enum is mapped through an explicit stable tag. Neither enum ordinals nor display strings define replay
/// identity. Schema changes require a new version and conformance corpus rather than silent reinterpretation.
/// </remarks>
public static class CounterfactualTargetOutcomeCanonicalCodec
{
    private static ReadOnlySpan<byte> FragmentDomain =>
        "PhoenixInspect.CounterfactualTargetOutcome.Fragment"u8;

    /// <summary>Serializes one validated immutable fragment using canonical schema version one.</summary>
    /// <param name="fragment">The projector-produced fragment to serialize.</param>
    /// <returns>A fresh immutable canonical byte vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fragment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The fragment contains a value outside the closed schema-v1 vocabulary.</exception>
    public static ImmutableArray<byte> SerializeCanonical(CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return Encode(fragment);
    }

    /// <summary>Computes the canonical lowercase SHA-256 fingerprint of one validated fragment.</summary>
    /// <param name="fragment">The projector-produced fragment to fingerprint.</param>
    /// <returns>A 64-character lowercase hexadecimal digest of the canonical bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fragment"/> is <see langword="null"/>.</exception>
    public static string ComputeSha256(CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        return Hash(Encode(fragment).AsSpan());
    }

    internal static ImmutableArray<byte> Encode(CounterfactualTargetOutcomeFragment fragment)
    {
        var writer = new CanonicalWriter();
        writer.WriteBytes(FragmentDomain);
        writer.WriteInt32(fragment.SchemaVersion == CounterfactualTargetOutcomeFragment.CanonicalSchemaVersion
            ? fragment.SchemaVersion
            : throw new ArgumentException("The fragment schema version is unsupported.", nameof(fragment)));
        writer.WriteInt32(EncodeSemanticMode(fragment.SemanticMode));
        writer.WriteInt32(EncodeCompletion(fragment.Completion));
        writer.WriteInt32(EncodeCompleteness(fragment.Completeness));
        writer.WriteInt32(EncodeEvidence(fragment.Evidence));
        writer.WriteInt32(EncodeEffects(fragment.Effects));
        writer.WriteInt32(EncodeTerminalStatus(fragment.TerminalStatus));

        var targetException = fragment.TargetException;
        writer.WriteInt32(EncodeTargetExceptionKind(targetException.Kind));
        writer.WriteString(targetException.Code);
        if (targetException.Method is not { } method || targetException.IlOffset is not { } ilOffset)
        {
            throw new ArgumentException("A canonical target exception requires one structural location.", nameof(fragment));
        }

        writer.WriteMethod(method);
        writer.WriteInt32(ilOffset);

        var callTrace = fragment.CallTrace;
        writer.WriteInt32(callTrace.Length);
        foreach (var traceMethod in callTrace)
        {
            writer.WriteMethod(traceMethod);
        }

        writer.WriteInt64(fragment.InitialInstructionUnits);
        writer.WriteInt64(fragment.UsedInstructionUnits);
        writer.WriteInt64(fragment.RemainingInstructionUnits);

        var events = fragment.Events;
        writer.WriteInt32(events.Length);
        foreach (var item in events)
        {
            ArgumentNullException.ThrowIfNull(item);
            writer.WriteInt32(EncodeEventKind(item.Kind));
            writer.WriteMethod(item.Method);
            writer.WriteInt32(item.IlOffset);
            writer.WriteString(item.Instruction);
        }

        var diagnostics = fragment.Diagnostics;
        writer.WriteInt32(diagnostics.Length);
        foreach (var diagnostic in diagnostics)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);
            writer.WriteString(diagnostic.Code);
            writer.WriteString(diagnostic.Message);
        }

        return writer.ToImmutableArray();
    }

    internal static string Hash(ReadOnlySpan<byte> canonicalBytes) =>
        Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();

    private static int EncodeSemanticMode(EvaluationSemanticMode value) => value switch
    {
        EvaluationSemanticMode.CounterfactualExecution => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeCompletion(EvaluationCompletionStatus value) => value switch
    {
        EvaluationCompletionStatus.Completed => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeCompleteness(EvaluationCompleteness value) => value switch
    {
        EvaluationCompleteness.Complete => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeEvidence(EvaluationEvidenceStatus value) => value switch
    {
        EvaluationEvidenceStatus.Exact => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeEffects(EvaluationEffectStatus value) => value switch
    {
        EvaluationEffectStatus.None => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeTerminalStatus(MachineRunStatus value) => value switch
    {
        MachineRunStatus.TargetException => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeTargetExceptionKind(TargetExceptionKind value) => value switch
    {
        TargetExceptionKind.NullReference => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static int EncodeEventKind(DebugEventKind value) => value switch
    {
        DebugEventKind.InstructionExecuted => 1,
        DebugEventKind.TargetExceptionRaised => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private sealed class CanonicalWriter
    {
        private readonly ArrayBufferWriter<byte> _buffer = new();

        internal void WriteInt32(int value)
        {
            var destination = _buffer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(destination, value);
            _buffer.Advance(sizeof(int));
        }

        internal void WriteInt64(long value)
        {
            var destination = _buffer.GetSpan(sizeof(long));
            BinaryPrimitives.WriteInt64BigEndian(destination, value);
            _buffer.Advance(sizeof(long));
        }

        internal void WriteUInt64(ulong value)
        {
            var destination = _buffer.GetSpan(sizeof(ulong));
            BinaryPrimitives.WriteUInt64BigEndian(destination, value);
            _buffer.Advance(sizeof(ulong));
        }

        internal void WriteMethod(MethodHandle method)
        {
            if (method.Module == default || !MethodHandle.IsValidMetadataToken(method.MetadataToken))
            {
                throw new ArgumentException(
                    "A canonical method handle requires one non-default module and non-nil MethodDef token.",
                    nameof(method));
            }

            WriteUInt64(method.Module.High);
            WriteUInt64(method.Module.Low);
            WriteInt32(method.MetadataToken);
        }

        internal void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            WriteBytes(Encoding.UTF8.GetBytes(value));
        }

        internal void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteInt32(value.Length);
            var destination = _buffer.GetSpan(value.Length);
            value.CopyTo(destination);
            _buffer.Advance(value.Length);
        }

        internal ImmutableArray<byte> ToImmutableArray() =>
            ImmutableArray.CreateRange(_buffer.WrittenSpan.ToArray());
    }
}
