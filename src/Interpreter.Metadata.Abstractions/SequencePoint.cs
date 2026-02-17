using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Represents a normalized sequence point for debug stepping and source highlighting.
/// </summary>
/// <param name="IlOffset">Starting IL offset.</param>
/// <param name="IlEndOffset">Ending IL offset (inclusive or inferred boundary depending on backend).</param>
/// <param name="Document">Source document identifier.</param>
/// <param name="StartLine">Start line (1-based).</param>
/// <param name="StartColumn">Start column (1-based).</param>
/// <param name="EndLine">End line (1-based).</param>
/// <param name="EndColumn">End column (1-based).</param>
/// <param name="IsHidden">Whether the sequence point is hidden/non-user code.</param>
public readonly record struct SequencePoint(
    int IlOffset,
    int IlEndOffset,
    DocumentId Document,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    bool IsHidden);
