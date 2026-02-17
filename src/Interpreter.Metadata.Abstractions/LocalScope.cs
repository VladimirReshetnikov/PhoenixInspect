using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Represents a lexical local scope.
/// </summary>
/// <param name="StartOffset">Inclusive starting IL offset.</param>
/// <param name="EndOffset">Exclusive ending IL offset.</param>
/// <param name="Locals">Locals active for the scope interval.</param>
public sealed record LocalScope(int StartOffset, int EndOffset, IReadOnlyList<LocalInfo> Locals);
