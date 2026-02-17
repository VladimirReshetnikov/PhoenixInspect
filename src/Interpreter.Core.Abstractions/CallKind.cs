using Interpreter.IL;
using Interpreter.Types;

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
