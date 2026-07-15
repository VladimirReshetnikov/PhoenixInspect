using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Freezes the caller boundary at which execution resumes after one interpreted callee frame returns.
/// </summary>
/// <remarks>
/// This content-equal draft W4.5 value belongs only to non-root interpreted frames. It retains both the complete
/// structural call-site identity and the already-decoded caller resume offset, so a return transfer does not decode,
/// resolve, or infer either fact again. Model-covered calls do not push frames and therefore do not create instances.
/// Broader stepping statement identities and exception-continuation state remain outside this prototype contract.
/// </remarks>
public sealed record FrameReturnSite
{
    /// <summary>Creates a validated interpreted-frame return boundary.</summary>
    /// <param name="callSite">The non-default structural direct-call identity that pushed the callee frame.</param>
    /// <param name="callerResumeIlOffset">
    /// The nonnegative caller instruction boundary immediately after the call instruction.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="callSite"/> is the default identity, or <paramref name="callerResumeIlOffset"/> does not
    /// follow its call opcode.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="callerResumeIlOffset"/> is negative.</exception>
    public FrameReturnSite(DirectCallSiteIdentity callSite, int callerResumeIlOffset)
    {
        if (callSite == default)
        {
            throw new ArgumentException("A frame return site requires a non-default direct call site.", nameof(callSite));
        }

        if (callerResumeIlOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callerResumeIlOffset),
                "A frame return site requires a nonnegative caller resume offset.");
        }

        if (callerResumeIlOffset <= callSite.CallIlOffset)
        {
            throw new ArgumentException(
                "A caller resume offset must follow the direct-call opcode.",
                nameof(callerResumeIlOffset));
        }

        CallSite = callSite;
        CallerResumeIlOffset = callerResumeIlOffset;
    }

    /// <summary>Gets the complete structural identity of the direct call that pushed the frame.</summary>
    public DirectCallSiteIdentity CallSite { get; }

    /// <summary>Gets the caller instruction boundary at which execution resumes after the callee returns.</summary>
    public int CallerResumeIlOffset { get; }
}
