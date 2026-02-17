using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Identifies source-document origin classes for debug UX.
/// </summary>
public enum DebugDocumentKind
{
    RealFile,
    Embedded,
    SourceLink,
    Decompiled,
    IL,
}
