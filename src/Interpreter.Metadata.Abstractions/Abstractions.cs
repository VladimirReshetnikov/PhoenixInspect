using Interpreter.Core.Abstractions;
using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable module identity used by metadata backends and runtime bridges.
/// </summary>
/// <param name="Mvid">Module version identifier.</param>
/// <param name="Name">Optional module display name.</param>
/// <param name="PathHint">Optional module path hint.</param>
/// <param name="PeStamp">Optional PE timestamp/image-size tuple for disambiguation.</param>
public readonly record struct ModuleId(
    Guid Mvid,
    string? Name = null,
    string? PathHint = null,
    (uint TimeDateStamp, uint ImageSize)? PeStamp = null);

/// <summary>
/// Stable document identity for debug/source artifacts.
/// </summary>
/// <param name="Value">Document identifier value.</param>
public readonly record struct DocumentId(Guid Value);

/// <summary>
/// Stable PDB identity for symbol correlation.
/// </summary>
/// <param name="Guid">PDB content GUID.</param>
/// <param name="Age">PDB age value.</param>
public readonly record struct PdbId(Guid Guid, int Age);

/// <summary>
/// Defines metadata operations for a single module.
/// </summary>
public interface IMetadataModule
{
    /// <summary>Gets the stable module identity.</summary>
    ModuleId Id { get; }

    /// <summary>Gets the corresponding core-layer module handle.</summary>
    ModuleHandle ModuleHandle { get; }

    /// <summary>Builds or retrieves a stable type handle from a metadata token and context.</summary>
    TypeHandle GetTypeHandle(int metadataToken, GenericContext ctx);

    /// <summary>Builds or retrieves a stable method handle from a metadata token and context.</summary>
    MethodHandle GetMethodHandle(int metadataToken, GenericContext ctx);

    /// <summary>Builds or retrieves a stable field handle from a metadata token and context.</summary>
    FieldHandle GetFieldHandle(int metadataToken, GenericContext ctx);

    /// <summary>Resolves a type signature from a type handle.</summary>
    TypeSig GetTypeSignature(TypeHandle type);

    /// <summary>Resolves a method signature from a method handle.</summary>
    MethodSig GetMethodSignature(MethodHandle method);

    /// <summary>Resolves a field signature from a field handle.</summary>
    FieldSig GetFieldSignature(FieldHandle field);

    /// <summary>Resolves a type token to the normalized core representation.</summary>
    ResolvedType ResolveTypeToken(int token, GenericContext ctx);

    /// <summary>Resolves a field token to the normalized core representation.</summary>
    ResolvedField ResolveFieldToken(int token, GenericContext ctx);

    /// <summary>Resolves a method token to the normalized core representation.</summary>
    ResolvedMethod ResolveMethodToken(int token, GenericContext ctx);

    /// <summary>Tries to retrieve a method body for interpretation.</summary>
    bool TryGetMethodBody(MethodHandle method, out MethodBody body);

    /// <summary>Resolves the runtime target method for virtual/interface dispatch.</summary>
    MethodHandle ResolveVirtualOverride(MethodHandle declared, TypeHandle runtimeType);
}

/// <summary>
/// Provides module lookup across all metadata modules participating in a session.
/// </summary>
public interface IMetadataUniverse
{
    /// <summary>
    /// Tries to retrieve a metadata module by identity.
    /// </summary>
    /// <param name="id">Requested module identity.</param>
    /// <param name="module">Resolved module when available.</param>
    /// <returns><see langword="true"/> when module lookup succeeds; otherwise <see langword="false"/>.</returns>
    bool TryGetModule(ModuleId id, out IMetadataModule module);
}

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

/// <summary>
/// Represents local-variable metadata for debugging experiences.
/// </summary>
/// <param name="Slot">Local slot index.</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Optional local type signature when available.</param>
public sealed record LocalInfo(int Slot, string Name, TypeSig? Type = null);

/// <summary>
/// Represents a lexical local scope.
/// </summary>
/// <param name="StartOffset">Inclusive starting IL offset.</param>
/// <param name="EndOffset">Exclusive ending IL offset.</param>
/// <param name="Locals">Locals active for the scope interval.</param>
public sealed record LocalScope(int StartOffset, int EndOffset, IReadOnlyList<LocalInfo> Locals);

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

/// <summary>
/// Describes a debug/source document.
/// </summary>
/// <param name="Id">Document identity.</param>
/// <param name="UrlOrPath">Path or URI describing where content originates.</param>
/// <param name="Kind">Document origin kind.</param>
public sealed record DebugDocument(DocumentId Id, string UrlOrPath, DebugDocumentKind Kind);

/// <summary>
/// Stable identifier for debugger statements within a method.
/// </summary>
/// <param name="Value">Statement identifier value.</param>
public readonly record struct StatementId(int Value);

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

/// <summary>
/// Provides best-available debug maps across symbol and decompiler backends.
/// </summary>
public interface IDebugMapProvider
{
    /// <summary>
    /// Gets the best available debug map for a method in a module.
    /// </summary>
    /// <param name="module">Module providing metadata context.</param>
    /// <param name="method">Target method handle.</param>
    /// <returns>A debug map from PDB, decompiler, or synthetic IL fallback.</returns>
    IDebugMap GetBestMap(IMetadataModule module, MethodHandle method);
}

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
