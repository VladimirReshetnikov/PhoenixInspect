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
