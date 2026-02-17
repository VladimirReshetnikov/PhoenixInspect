using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Provides normalized symbol information independent of concrete PDB technologies.
/// </summary>
public interface ISymbolInfo
{
    /// <summary>Tries to retrieve sequence points for a method.</summary>
    bool TryGetSequencePoints(MethodHandle method, out IReadOnlyList<SequencePoint> points);

    /// <summary>Tries to retrieve local scopes for a method.</summary>
    bool TryGetLocalScopes(MethodHandle method, out IReadOnlyList<LocalScope> scopes);

    /// <summary>Tries to resolve a document descriptor from its identity.</summary>
    bool TryGetDocument(DocumentId id, out DebugDocument doc);

    /// <summary>Tries to map an async move-next method back to its kickoff method.</summary>
    bool TryGetStateMachineKickoff(MethodHandle moveNext, out MethodHandle kickoff);
}
