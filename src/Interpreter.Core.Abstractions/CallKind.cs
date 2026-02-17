namespace Interpreter.Core.Abstractions;

/// <summary>
/// Categorizes IL call-site instruction forms.
/// </summary>
public enum CallKind
{
    Call,
    CallVirt,
    NewObj,
}
