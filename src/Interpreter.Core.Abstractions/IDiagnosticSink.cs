namespace Interpreter.Core.Abstractions;

/// <summary>
/// Receives diagnostics emitted by interpreter subsystems.
/// </summary>
public interface IDiagnosticSink
{
    /// <summary>
    /// Records a diagnostic raised by a core component.
    /// </summary>
    /// <param name="diagnostic">Diagnostic payload to report.</param>
    void Report(in Diagnostic diagnostic);
}
