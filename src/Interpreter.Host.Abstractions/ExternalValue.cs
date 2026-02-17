using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Represents a host-provided raw value before adaptation into the interpreter value domain.
/// </summary>
/// <param name="Kind">Payload kind discriminator.</param>
/// <param name="I64">Integral payload storage.</param>
/// <param name="F64">Floating-point payload storage.</param>
/// <param name="Obj">Object-reference payload storage.</param>
/// <param name="Bytes">Optional raw byte payload for value types and blobs.</param>
public sealed record ExternalValue(
    ExternalValueKind Kind,
    long I64 = 0,
    double F64 = 0,
    ExternalObjectRef Obj = default,
    ReadOnlyMemory<byte>? Bytes = null);
