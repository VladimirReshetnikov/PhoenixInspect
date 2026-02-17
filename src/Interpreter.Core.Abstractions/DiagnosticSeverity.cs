using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines severity levels for interpreter diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}
