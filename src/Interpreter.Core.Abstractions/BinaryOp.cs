using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Supported binary operations for value-domain interpretation.
/// </summary>
public enum BinaryOp
{
    Add,
    Sub,
    Mul,
    Div,
    Rem,
    And,
    Or,
    Xor,
    Shl,
    Shr,
    ShrUn,
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}
