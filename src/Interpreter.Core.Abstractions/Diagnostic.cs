namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a stable, structured diagnostic emitted by abstraction-layer components.
/// </summary>
/// <param name="Severity">Diagnostic severity.</param>
/// <param name="Code">Stable, machine-readable code such as INTP0012.</param>
/// <param name="Message">Human-readable message describing the condition.</param>
public readonly record struct Diagnostic(DiagnosticSeverity Severity, string Code, string Message);
