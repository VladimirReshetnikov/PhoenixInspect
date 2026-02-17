using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Categorizes outcomes returned by call-model handlers.
/// </summary>
public enum CallOutcomeKind
{
    NotHandled,
    Returned,
    Threw,
    Forked,
    StopForDecision,
}
