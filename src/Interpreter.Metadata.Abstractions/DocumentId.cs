using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable document identity for debug/source artifacts.
/// </summary>
/// <param name="Value">Document identifier value.</param>
public readonly record struct DocumentId(Guid Value);
