namespace Interpreter.Types;

/// <summary>
/// Represents a draft field signature used by abstraction-layer contracts during the concept phase.
/// </summary>
/// <param name="FieldType">The field value type.</param>
public sealed record FieldSig(TypeSig FieldType);
