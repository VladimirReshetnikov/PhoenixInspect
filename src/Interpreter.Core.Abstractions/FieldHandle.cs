namespace Interpreter.Core.Abstractions;

/// <summary>
/// Identifies a FieldDef within a content- or snapshot-identified managed module.
/// </summary>
/// <remarks>
/// A raw metadata row number is not globally meaningful. Pairing the validated FieldDef token with the exact
/// module prevents same-row fields from unrelated modules or snapshots from aliasing in admitted plans or memory.
/// </remarks>
public readonly record struct FieldHandle
{
    private const int TokenTypeMask = unchecked((int)0xFF000000);
    private const int FieldDefinitionTokenType = 0x04000000;
    private const int RowIdMask = 0x00FFFFFF;

    /// <summary>Creates a validated field-definition handle.</summary>
    /// <param name="module">The deterministic identity of the defining module.</param>
    /// <param name="metadataToken">A non-nil FieldDef metadata token.</param>
    /// <exception cref="ArgumentException"><paramref name="module"/> is the default handle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metadataToken"/> is not a non-nil FieldDef token.
    /// </exception>
    public FieldHandle(ModuleHandle module, int metadataToken)
    {
        if (module == default)
        {
            throw new ArgumentException("A field handle requires a non-default module identity.", nameof(module));
        }

        if (!IsValidMetadataToken(metadataToken))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadataToken),
                "A field handle requires a non-nil FieldDef metadata token.");
        }

        Module = module;
        MetadataToken = metadataToken;
    }

    /// <summary>Gets the deterministic identity of the defining module.</summary>
    public ModuleHandle Module { get; }

    /// <summary>Gets the non-nil FieldDef metadata token.</summary>
    public int MetadataToken { get; }

    /// <summary>Determines whether a raw metadata token can identify a FieldDef.</summary>
    /// <param name="metadataToken">The raw ECMA-335 metadata token.</param>
    /// <returns><see langword="true"/> exactly for a FieldDef token with a nonzero row identifier.</returns>
    public static bool IsValidMetadataToken(int metadataToken) =>
        (metadataToken & TokenTypeMask) == FieldDefinitionTokenType &&
        (metadataToken & RowIdMask) != 0;

    /// <summary>Deconstructs the handle into its defining module and FieldDef token.</summary>
    /// <param name="module">Receives the defining module identity.</param>
    /// <param name="metadataToken">Receives the FieldDef metadata token.</param>
    public void Deconstruct(out ModuleHandle module, out int metadataToken)
    {
        module = Module;
        metadataToken = MetadataToken;
    }

    /// <summary>Formats the module identity and metadata token for deterministic diagnostics.</summary>
    /// <returns>A stable representation suitable for traces and test diagnostics.</returns>
    public override string ToString() => $"{Module}:0x{MetadataToken:X8}";
}
