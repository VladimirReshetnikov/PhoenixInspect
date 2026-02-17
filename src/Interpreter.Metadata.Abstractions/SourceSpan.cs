using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Represents a source span used for highlighting and stepping.
/// </summary>
/// <param name="Document">Source document identifier.</param>
/// <param name="StartLine">Start line (1-based).</param>
/// <param name="StartCol">Start column (1-based).</param>
/// <param name="EndLine">End line (1-based).</param>
/// <param name="EndCol">End column (1-based).</param>
/// <param name="IsHidden">Whether the span should be treated as hidden/non-user code.</param>
public sealed record SourceSpan(
    DocumentId Document,
    int StartLine,
    int StartCol,
    int EndLine,
    int EndCol,
    bool IsHidden);
