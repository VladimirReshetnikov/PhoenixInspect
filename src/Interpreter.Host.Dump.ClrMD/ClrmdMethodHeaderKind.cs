namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Identifies the ECMA-335 physical header encoding observed for a dump-backed managed method body.
/// </summary>
/// <remarks>
/// This draft evidence value describes bytes captured from the target address space. It does not imply that the
/// interpreter admits every semantic feature represented by the body.
/// </remarks>
public enum ClrmdMethodHeaderKind
{
    /// <summary>The one-byte tiny header used by methods with no locals or extra data sections.</summary>
    Tiny,

    /// <summary>The current twelve-byte fat header carrying explicit stack, locals, and section facts.</summary>
    Fat,
}
