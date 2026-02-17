namespace Interpreter.Core.Abstractions;

/// <summary>
/// Categorizes side effects observed or inferred while interpreting a method.
/// </summary>
[Flags]
public enum EffectKind
{
    None = 0,
    Alloc = 1 << 0,
    ReadEnv = 1 << 1,
    WriteEnv = 1 << 2,
    Time = 1 << 3,
    Random = 1 << 4,
    Threading = 1 << 5,
    Native = 1 << 6,
    Reflection = 1 << 7,
    Throw = 1 << 8,
    Unsupported = 1 << 9,
}
