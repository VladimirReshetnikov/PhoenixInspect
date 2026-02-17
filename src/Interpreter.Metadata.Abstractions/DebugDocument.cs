using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Describes a debug/source document.
/// </summary>
/// <param name="Id">Document identity.</param>
/// <param name="UrlOrPath">Path or URI describing where content originates.</param>
/// <param name="Kind">Document origin kind.</param>
public sealed record DebugDocument(DocumentId Id, string UrlOrPath, DebugDocumentKind Kind);
