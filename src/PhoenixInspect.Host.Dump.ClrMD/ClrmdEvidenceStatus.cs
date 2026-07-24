namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Classifies whether a ClrMD adapter operation selected and validated one requested piece of runtime evidence.
/// </summary>
public enum ClrmdEvidenceStatus
{
    /// <summary>
    /// One unambiguous result was selected and all bytes required for that result were available.
    /// </summary>
    Exact,

    /// <summary>
    /// A non-empty, evidence-backed prefix or subset was recovered, but required evidence was incomplete.
    /// </summary>
    Partial,

    /// <summary>
    /// Required runtime structure or memory was not present in the captured snapshot.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Competing candidates or incompatible provenance prevented a unique result.
    /// </summary>
    Conflict,

    /// <summary>
    /// Captured evidence violated an invariant of the supported runtime layout.
    /// </summary>
    Invalid,
}
