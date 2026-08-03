using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>Identifies how an expression root was selected.</summary>
public enum RootSelectionKind
{
    /// <summary>The object was reached through one CLR strong-handle family slot.</summary>
    StrongHandle = 1,

    /// <summary>
    /// The object was reached as the exact value of a static-field expression and carries its own typed provenance.
    /// </summary>
    ObjectBinding = 2,
}

/// <summary>
/// Carries one selected expression root together with the provenance the product requires to bind it.
/// </summary>
/// <remarks>
/// The two admitted sources need different binding calls: a handle-selected object binds directly, while an object
/// reached through a static-field expression must first be projected into the compatibility shape consumed by the
/// instance-member engines, and its authoritative typed source must be preserved rather than restated as a handle.
/// Keeping both behind one selection lets the evaluation panel treat them uniformly without discarding that
/// distinction.
/// </remarks>
public sealed class RootSelection
{
    private readonly ClrmdHeapObjectInfo? handleObject;
    private readonly ClrmdExactObjectReference? objectReference;
    private readonly DumpObjectBinding? objectBinding;

    private RootSelection(
        RootSelectionKind kind,
        string description,
        ClrmdHeapObjectInfo? handleObject,
        ClrmdExactObjectReference? objectReference,
        DumpObjectBinding? objectBinding)
    {
        Kind = kind;
        Description = description;
        this.handleObject = handleObject;
        this.objectReference = objectReference;
        this.objectBinding = objectBinding;
    }

    /// <summary>Gets how this root was selected.</summary>
    public RootSelectionKind Kind { get; }

    /// <summary>Gets a one-line description of the selected object for the UI.</summary>
    public string Description { get; }

    /// <summary>Creates a root from an object reached through a bounded strong-handle search.</summary>
    /// <param name="row">The selected search match.</param>
    /// <returns>A strong-handle root selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public static RootSelection FromHandleObject(HeapObjectRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new RootSelection(
            RootSelectionKind.StrongHandle,
            $"{row.TypeName} @ {row.Address} (rooted by {row.RootKind})",
            row.Object,
            objectReference: null,
            objectBinding: null);
    }

    /// <summary>Creates a root from the exact object value of a static-field expression.</summary>
    /// <param name="reference">The exact raw-header-first object reference decoded from the field's storage.</param>
    /// <param name="binding">The authoritative typed object binding retaining the originating expression.</param>
    /// <param name="expression">The static-field expression whose value produced this object.</param>
    /// <returns>An object-binding root selection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static RootSelection FromObjectBinding(
        ClrmdExactObjectReference reference,
        DumpObjectBinding binding,
        string expression)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(binding);
        return new RootSelection(
            RootSelectionKind.ObjectBinding,
            $"{binding.Identity.TypeName} @ 0x{reference.Address:X16} (value of {expression})",
            handleObject: null,
            reference,
            binding);
    }

    /// <summary>
    /// Resolves this selection to its heap object for metadata-only projections such as member-name completion.
    /// </summary>
    /// <param name="session">The open dump session.</param>
    /// <returns>The heap object, or null when the typed object could not be projected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public ClrmdHeapObjectInfo? TryResolveHeapObject(ClrmdDumpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Kind == RootSelectionKind.StrongHandle
            ? handleObject
            : session.ProjectExactObjectForInstanceEvaluation(objectReference!).Value;
    }

    /// <summary>Resolves this selection into the product's root binding.</summary>
    /// <param name="session">The open dump session; a typed object binding needs one direct heap lookup.</param>
    /// <param name="identifier">The case-sensitive identifier the expression uses for the root.</param>
    /// <returns>
    /// The exact root binding, or a stop message when a typed object could not be projected into the compatibility
    /// shape required by the instance-member engines.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public (DumpQueryRootBinding? Binding, string? Stop) Resolve(ClrmdDumpSession session, string identifier)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Kind == RootSelectionKind.StrongHandle)
        {
            return (DumpQueryRootBinding.FromExactObject(identifier, handleObject!), null);
        }

        var projected = session.ProjectExactObjectForInstanceEvaluation(objectReference!);
        return projected.Value is { } info
            ? (DumpQueryRootBinding.FromObjectBinding(identifier, info, objectBinding!), null)
            : (null,
                $"The object could not be projected for instance evaluation ({projected.Status}, {projected.Issue}).");
    }
}
