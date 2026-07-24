namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Freezes the exact MethodDef identity and body-independent signature of one admitted managed-IL call target.
/// </summary>
/// <remarks>
/// This value deliberately carries no method body, RVA, local signature, or local types. A preparation pipeline can
/// therefore select an interpreted, modeled, or blocked disposition from exact metadata before acquiring a possible
/// callee body. Construction has no non-managed alternative: every instance represents an exact managed-IL admission
/// fact established by its resolver.
/// </remarks>
public sealed record ResolvedMethodCallTarget
{
    /// <summary>Creates an exact managed-IL method-call target.</summary>
    /// <param name="method">The exact module-and-MethodDef identity.</param>
    /// <param name="signature">The exact body-independent call signature.</param>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The target MethodDef and its declaring TypeDef belong to different modules.
    /// </exception>
    public ResolvedMethodCallTarget(MethodHandle method, MethodCallSignatureShape signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        if (method.Module != signature.DeclaringType.Module)
        {
            throw new ArgumentException(
                "A resolved call target and its declaring TypeDef must belong to the same module.",
                nameof(method));
        }

        Method = method;
        Signature = signature;
    }

    /// <summary>Gets the exact module-and-MethodDef identity.</summary>
    public MethodHandle Method { get; }

    /// <summary>Gets the exact body-independent call signature.</summary>
    public MethodCallSignatureShape Signature { get; }

    /// <summary>
    /// Gets the exact admission fact that the target is managed IL rather than native, runtime-supplied, or P/Invoke.
    /// </summary>
    /// <remarks>
    /// The value is always <see langword="true"/> because the type exposes no constructor for a non-managed target.
    /// </remarks>
    public bool IsManagedIl => true;
}
