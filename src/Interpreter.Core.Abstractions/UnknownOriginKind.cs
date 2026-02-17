namespace Interpreter.Core.Abstractions;

/// <summary>
/// Describes the provenance of an unknown or unconstrained value produced by interpretation.
/// </summary>
public enum UnknownOriginKind
{
    MissingData,
    UnsupportedIL,
    ExternalCall,
    NativeCall,
    EnvRead,
    BudgetExceeded,
    AmbiguousDispatch,
}
