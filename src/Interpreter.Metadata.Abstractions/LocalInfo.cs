using Interpreter.Types;

namespace Interpreter.Metadata.Abstractions;

/// <summary>
/// Represents local-variable metadata for debugging experiences.
/// </summary>
/// <param name="Slot">Local slot index.</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Optional local type signature when available.</param>
public sealed record LocalInfo(int Slot, string Name, TypeSig? Type = null);
