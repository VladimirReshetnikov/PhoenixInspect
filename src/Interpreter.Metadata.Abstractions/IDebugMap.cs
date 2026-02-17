using Interpreter.Core.Abstractions;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Provides a normalized stepping map for a single method.
/// </summary>
public interface IDebugMap
{
    /// <summary>Gets the method represented by the map.</summary>
    MethodHandle Method { get; }

    /// <summary>Gets the statement identity at a given IL offset.</summary>
    StatementId GetStatementAtIlOffset(int ilOffset);

    /// <summary>Tries to retrieve source-span information for an IL offset.</summary>
    bool TryGetSourceSpan(int ilOffset, out SourceSpan span);

    /// <summary>Tries to compute the next statement offset for stepping.</summary>
    int? TryGetNextStatementOffset(int ilOffset);

    /// <summary>Tries to compute the previous statement offset for stepping.</summary>
    int? TryGetPreviousStatementOffset(int ilOffset);
}
