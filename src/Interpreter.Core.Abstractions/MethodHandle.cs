namespace Interpreter.Core.Abstractions;

/// <summary>
/// Identifies a MethodDef within a content-identified managed module.
/// </summary>
/// <remarks>
/// Generic instantiation is deliberately not folded into this definition handle. A method body belongs to the
/// MethodDef; constructed-method identity will be introduced only with an executable generic scenario. The current
/// slice rejects such scenarios rather than hiding context in this definition key. This avoids request-order-
/// dependent allocation and makes equal definitions compare equal across sessions.
/// </remarks>
public readonly record struct MethodHandle
{
    private const int TokenTypeMask = unchecked((int)0xFF000000);
    private const int MethodDefinitionTokenType = 0x06000000;
    private const int RowIdMask = 0x00FFFFFF;

    /// <summary>Creates a validated method-definition handle.</summary>
    /// <param name="module">The deterministic identity of the defining module.</param>
    /// <param name="metadataToken">A non-nil MethodDef metadata token.</param>
    /// <exception cref="ArgumentException"><paramref name="module"/> is the default handle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metadataToken"/> is not a non-nil MethodDef token.
    /// </exception>
    public MethodHandle(ModuleHandle module, int metadataToken)
    {
        if (module == default)
        {
            throw new ArgumentException("A method handle requires a non-default module identity.", nameof(module));
        }

        if (!IsValidMetadataToken(metadataToken))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadataToken),
                "A method handle requires a non-nil MethodDef metadata token.");
        }

        Module = module;
        MetadataToken = metadataToken;
    }

    /// <summary>Gets the deterministic identity of the defining module.</summary>
    public ModuleHandle Module { get; }

    /// <summary>Gets the non-nil MethodDef metadata token.</summary>
    public int MetadataToken { get; }

    /// <summary>Determines whether a raw metadata token can identify a MethodDef.</summary>
    /// <param name="metadataToken">The raw ECMA-335 metadata token.</param>
    /// <returns><see langword="true"/> exactly for a MethodDef token with a nonzero row identifier.</returns>
    public static bool IsValidMetadataToken(int metadataToken) =>
        (metadataToken & TokenTypeMask) == MethodDefinitionTokenType &&
        (metadataToken & RowIdMask) != 0;

    /// <summary>
    /// Deconstructs the handle into its module and MethodDef token.
    /// </summary>
    /// <param name="module">Receives the defining module identity.</param>
    /// <param name="metadataToken">Receives the MethodDef metadata token.</param>
    public void Deconstruct(out ModuleHandle module, out int metadataToken)
    {
        module = Module;
        metadataToken = MetadataToken;
    }

    /// <summary>
    /// Formats the module identity and metadata token in a deterministic diagnostic representation.
    /// </summary>
    /// <returns>A stable representation suitable for traces and test diagnostics.</returns>
    public override string ToString() => $"{Module}:0x{MetadataToken:X8}";
}
