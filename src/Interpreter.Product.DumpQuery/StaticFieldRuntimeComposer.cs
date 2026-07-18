using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Composes Product-owned static-field semantics with detached Host runtime facts.</summary>
/// <remarks>
/// This draft W7 boundary rereads no expression and performs no storage access. It uses counted metadata to assign
/// semantic roles that the ClrMD Host deliberately leaves raw, then returns immutable proof objects accepted by the
/// physical request factory.
/// </remarks>
public static class StaticFieldRuntimeComposer
{
    /// <summary>Correlates a complete Host nullable layout with the exact Product-selected metadata declaration.</summary>
    /// <param name="session">The open immutable dump session that supplied the binding and runtime layout.</param>
    /// <param name="symbolBinding">The exact exhaustive binding selecting a Nullable&lt;Int32&gt; declaration.</param>
    /// <param name="runtimeLayout">The complete metadata-blind Host child catalog and payload extent.</param>
    /// <returns>An immutable semantic layout proof suitable for physical request construction.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Binding is non-exact or non-nullable, metadata is incomplete, or counted metadata and runtime facts disagree.
    /// </exception>
    public static StaticFieldNullableInt32RuntimeLayoutIdentity ComposeNullableInt32Layout(
        ClrmdDumpSession session,
        StaticFieldSymbolBindingOutcome symbolBinding,
        ClrmdStaticNullableRuntimeLayoutIdentity runtimeLayout)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(symbolBinding);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        if (symbolBinding.Status != StaticFieldBindingStatus.Exact ||
            symbolBinding.SelectedDeclaration is not { } declaration ||
            declaration.DeclaredValueKind != StaticFieldDeclaredValueKind.NullableInt32)
        {
            throw new ArgumentException(
                "Nullable runtime composition requires one exact exhaustive Nullable<Int32> binding.",
                nameof(symbolBinding));
        }

        if (!string.Equals(symbolBinding.SnapshotSha256, session.Snapshot.Sha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The exact symbol binding belongs to a different immutable dump snapshot.",
                nameof(symbolBinding));
        }

        return ComposeNullableInt32Layout(
            new ClrmdStaticFieldMetadataBindingSource(session),
            declaration,
            runtimeLayout);
    }

    internal static StaticFieldNullableInt32RuntimeLayoutIdentity ComposeNullableInt32Layout(
        IStaticFieldMetadataBindingSource source,
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdStaticNullableRuntimeLayoutIdentity runtimeLayout) =>
        StaticFieldFullyQualifiedBinder.ComposeNullableInt32RuntimeLayout(source, declaration, runtimeLayout);
}
