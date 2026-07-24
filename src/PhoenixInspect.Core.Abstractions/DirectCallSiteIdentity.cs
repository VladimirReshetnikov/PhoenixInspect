namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Identifies one structurally resolved direct-call instruction and its exact MethodDef target.
/// </summary>
/// <remarks>
/// The identity is content-equal across independently materialized plans and sessions: it combines the exact caller
/// MethodDef, the call opcode's byte offset, and the exact callee MethodDef. The current draft W4.5 slice admits only
/// same-module direct <c>call</c> edges; virtual, interface, generic, cross-module, and name-resolved calls remain
/// outside this contract. Signature and body facts stay in the frozen method graph and are deliberately not copied
/// into this boundary identity.
///
/// As with other record-struct handles, the CLR can still produce <see langword="default"/> without invoking the
/// constructor. Consumers must reject that sentinel at executable boundaries rather than treating it as a call site.
/// </remarks>
public readonly record struct DirectCallSiteIdentity
{
    /// <summary>Creates a validated identity for one admitted same-module direct call.</summary>
    /// <param name="caller">The exact MethodDef containing the call instruction.</param>
    /// <param name="callIlOffset">The nonnegative byte offset of the <c>call</c> opcode in <paramref name="caller"/>.</param>
    /// <param name="callee">The exact same-module MethodDef selected by the call operand.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="caller"/> or <paramref name="callee"/> is the default method handle, or the handles identify
    /// different modules.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="callIlOffset"/> is negative.</exception>
    public DirectCallSiteIdentity(MethodHandle caller, int callIlOffset, MethodHandle callee)
    {
        if (caller == default)
        {
            throw new ArgumentException("A direct call site requires a non-default caller.", nameof(caller));
        }

        if (callIlOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callIlOffset),
                "A direct call site requires a nonnegative IL offset.");
        }

        if (callee == default)
        {
            throw new ArgumentException("A direct call site requires a non-default callee.", nameof(callee));
        }

        if (caller.Module != callee.Module)
        {
            throw new ArgumentException(
                "The current direct-call identity requires caller and callee MethodDefs from the same module.",
                nameof(callee));
        }

        Caller = caller;
        CallIlOffset = callIlOffset;
        Callee = callee;
    }

    /// <summary>Gets the exact MethodDef containing the direct-call instruction.</summary>
    public MethodHandle Caller { get; }

    /// <summary>Gets the nonnegative byte offset of the <c>call</c> opcode in <see cref="Caller"/>.</summary>
    public int CallIlOffset { get; }

    /// <summary>Gets the exact same-module MethodDef selected by the direct-call operand.</summary>
    public MethodHandle Callee { get; }

    /// <summary>Deconstructs every structural call-site identity axis.</summary>
    /// <param name="caller">Receives the MethodDef containing the call.</param>
    /// <param name="callIlOffset">Receives the call opcode's byte offset.</param>
    /// <param name="callee">Receives the exact MethodDef selected by the operand.</param>
    public void Deconstruct(out MethodHandle caller, out int callIlOffset, out MethodHandle callee)
    {
        caller = Caller;
        callIlOffset = CallIlOffset;
        callee = Callee;
    }

    /// <summary>Formats the exact caller, call offset, and callee for deterministic diagnostics.</summary>
    /// <returns>A stable structural call-site representation containing no display names.</returns>
    public override string ToString() => $"{Caller}@IL_{CallIlOffset:X4}->{Callee}";
}
