namespace Interpreter.Core.Abstractions;

/// <summary>
/// Supported primitive conversion operations for value-domain interpretation.
/// </summary>
public enum ConvOp
{
    I1,
    U1,
    I2,
    U2,
    I4,
    U4,
    I8,
    U8,
    R4,
    R8,
    NativeInt,
    NativeUInt,
}
