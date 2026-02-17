namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Stable PDB identity for symbol correlation.
/// </summary>
/// <param name="Guid">PDB content GUID.</param>
/// <param name="Age">PDB age value.</param>
public readonly record struct PdbId(Guid Guid, int Age);
