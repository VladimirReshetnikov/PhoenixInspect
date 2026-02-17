using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents the evaluation-stack category of a value.
/// </summary>
public enum StackKind
{
    I4,
    I8,
    R4,
    R8,
    NativeInt,
    Ref,
    ByRef,
    ValueType,
}
