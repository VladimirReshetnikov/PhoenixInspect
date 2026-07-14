namespace Interpreter.Core.Abstractions;

/// <summary>
/// Supported binary operations for value-domain interpretation.
/// </summary>
public enum BinaryOp
{
    /// <summary>Adds the two operands using the domain's numeric overflow policy.</summary>
    Add,

    /// <summary>Subtracts the right operand from the left operand.</summary>
    Sub,

    /// <summary>Multiplies the two operands using the domain's numeric overflow policy.</summary>
    Mul,

}
