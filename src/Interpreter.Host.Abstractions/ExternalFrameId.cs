namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-specific external frame identity.
/// </summary>
/// <param name="Index">Frame index in host-defined stack order.</param>
public readonly record struct ExternalFrameId(int Index);
