namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Represents minimal managed module information discovered from a dump runtime.
/// </summary>
/// <param name="Name">Module display name as reported by ClrMD.</param>
/// <param name="FilePath">On-disk module path used for metadata reading when available.</param>
public sealed record ClrmdModuleInfo(string Name, string? FilePath);
