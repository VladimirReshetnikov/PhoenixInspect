namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Provides source text retrieval for debug documents.
/// </summary>
public interface ISourceTextProvider
{
    /// <summary>
    /// Tries to load source text for a document.
    /// </summary>
    /// <param name="doc">Document descriptor to load.</param>
    /// <param name="text">Resolved source text when available.</param>
    /// <returns><see langword="true"/> when source content was retrieved; otherwise <see langword="false"/>.</returns>
    bool TryGetSourceText(DebugDocument doc, out string text);
}
