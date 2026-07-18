using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>
/// Resolves the W7 static-field expression profile with additive selected-frame and Portable-PDB name context.
/// </summary>
/// <remarks>
/// The complete Roslyn-derived descriptor is consumed without reparsing. Literal <c>global::</c> expressions bypass
/// all frame and PDB evidence. Bare type names may use an exact declaring namespace and exact active namespace/type
/// aliases; namespace-qualified spellings retain their independent dot interpretation while an exact matching
/// namespace alias contributes another explicit interpretation. Caveat: this draft binder implements only the W7
/// name-expansion rules and declaration shapes, not general C# semantic binding or overload resolution.
/// </remarks>
public static class StaticFieldContextualBinder
{
    /// <summary>
    /// Resolves one accepted W7 static-field descriptor against an open dump session and its independently acquired
    /// selected-frame/Portable-PDB context.
    /// </summary>
    /// <param name="session">The open immutable dump session supplying the counted managed-module metadata catalog.</param>
    /// <param name="descriptor">The accepted, detached Roslyn projection to bind without reparsing.</param>
    /// <param name="context">The additive frame/PDB context acquired for the same immutable snapshot.</param>
    /// <returns>
    /// A canonical exact, absent, partial, unavailable, ambiguous, conflicting, invalid, or unsupported outcome.
    /// Exact status alone exposes one selected declaration.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The context belongs to a different dump snapshot.</exception>
    /// <remarks>
    /// Every module metadata image is read at most once. The operation performs no static-storage or value read and
    /// does not take ownership of <paramref name="session"/>.
    /// </remarks>
    public static StaticFieldSymbolBindingOutcome Bind(
        ClrmdDumpSession session,
        StaticFieldExpressionDescriptor descriptor,
        DumpExpressionBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(session);
        return StaticFieldFullyQualifiedBinder.BindContextual(
            new ClrmdStaticFieldMetadataBindingSource(session),
            descriptor,
            context);
    }

    internal static StaticFieldSymbolBindingOutcome Bind(
        IStaticFieldMetadataBindingSource source,
        StaticFieldExpressionDescriptor descriptor,
        DumpExpressionBindingContext context) =>
        StaticFieldFullyQualifiedBinder.BindContextual(source, descriptor, context);
}
