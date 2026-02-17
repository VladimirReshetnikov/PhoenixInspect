using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Supported unary operations for value-domain interpretation.
/// </summary>
public enum UnaryOp
{
    Neg,
    Not,
}
