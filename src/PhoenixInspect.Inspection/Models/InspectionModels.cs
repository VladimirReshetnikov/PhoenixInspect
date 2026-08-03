using System.Collections.ObjectModel;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.Inspection;

/// <summary>One labelled fact rendered by a property list.</summary>
/// <param name="Group">The section heading under which the fact is grouped.</param>
/// <param name="Name">The short fact label.</param>
/// <param name="Value">The display value, already formatted for the invariant culture.</param>
/// <param name="Detail">Optional explanatory text shown beneath the value, or null.</param>
public sealed record PropertyRow(string Group, string Name, string Value, string? Detail = null);

/// <summary>One managed module instance projected for the module grid.</summary>
/// <param name="Module">The originating adapter evidence, retained so the panel can request content identity.</param>
/// <param name="Name">The runtime-reported display file name.</param>
/// <param name="AppDomainId">The runtime-reported application-domain identifier.</param>
/// <param name="MetadataAddress">The formatted metadata-root address.</param>
/// <param name="MetadataLength">The formatted metadata length.</param>
/// <param name="Layout">The ClrMD-reported mapped-image layout name.</param>
/// <param name="TargetPathHint">The unmodified target-side path hint, which is never an analysis-machine path.</param>
/// <param name="SymbolHint">
/// A short statement of whether a hint-derived <c>.pdb</c> candidate exists on this machine. It reports discovery
/// only — a present candidate is still identity-validated before anything binds through it.
/// </param>
public sealed record ModuleRow(
    ClrmdModuleInfo Module,
    string Name,
    int AppDomainId,
    string MetadataAddress,
    string MetadataLength,
    string Layout,
    string TargetPathHint,
    string SymbolHint = "")
{
    /// <summary>
    /// Applies the module-list filter contract: a blank filter admits every row; otherwise the filter must appear
    /// as a case-insensitive substring of the module name or the target path hint.
    /// </summary>
    /// <param name="filter">The raw filter text, possibly null or blank.</param>
    /// <returns>Whether this row satisfies the filter.</returns>
    public bool Matches(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || TargetPathHint.Contains(filter, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One strong-handle-rooted heap object projected for the object grid.</summary>
/// <param name="Object">The originating adapter evidence, retained so it can become an expression root.</param>
/// <param name="TypeName">The runtime-reported type name.</param>
/// <param name="Address">The formatted object address.</param>
/// <param name="MethodTable">The formatted method-table address.</param>
/// <param name="RootKind">The CLR handle kind through which the object was reached.</param>
/// <param name="RootAddress">The formatted handle-slot address.</param>
/// <param name="ModuleName">The display name of the module defining the runtime type.</param>
public sealed record HeapObjectRow(
    ClrmdHeapObjectInfo Object,
    string TypeName,
    string Address,
    string MethodTable,
    string RootKind,
    string RootAddress,
    string ModuleName);

/// <summary>One row of the Locals pane: a parameter or a local variable slot of the selected frame.</summary>
/// <param name="Kind">The row kind: <c>Parameter</c>, <c>this</c>, or <c>Local</c>.</param>
/// <param name="Name">The decoded name, or a slot placeholder when no name is recorded.</param>
/// <param name="TypeDisplay">The C#-flavored declared type.</param>
/// <param name="Slot">The IL local slot index, or the parameter ordinal, as display text.</param>
/// <param name="Note">Display notes: out-of-scope or debugger-hidden markers, or an empty string.</param>
public sealed record FrameVariableRow(
    string Kind,
    string Name,
    string TypeDisplay,
    string Slot,
    string Note);

/// <summary>Shared display state for a node in the call-stack tree.</summary>
/// <remarks>
/// Expansion state lives on the node rather than on the tree container so the panel can pre-expand a thread and so
/// on-demand frame loading is driven by the same state the user manipulates.
/// </remarks>
public abstract class CallStackNode : ObservableObject
{
    private bool isExpanded;

    /// <summary>Creates a node with its two display lines.</summary>
    /// <param name="header">The primary display line.</param>
    /// <param name="detail">The secondary display line.</param>
    protected CallStackNode(string header, string detail)
    {
        Header = header;
        Detail = detail;
    }

    /// <summary>Gets the primary display line.</summary>
    public string Header { get; }

    /// <summary>Gets the secondary display line.</summary>
    public string Detail { get; }

    /// <summary>Gets or sets whether this node is expanded in the tree.</summary>
    /// <remarks>A frame node has no children, so its value is display state only.</remarks>
    public bool IsExpanded
    {
        get => isExpanded;
        set => Set(ref isExpanded, value);
    }
}

/// <summary>One managed frame projected for the call-stack tree.</summary>
/// <remarks>
/// A frame node is also the selection that supplies name-binding context to the evaluation panel, so it retains the
/// exact snapshot-scoped selector rather than only its display text.
/// </remarks>
public sealed class CallStackFrameNode : CallStackNode
{
    /// <summary>Creates a frame node from one exact selected-frame observation.</summary>
    /// <param name="selector">The snapshot-scoped thread/frame ordinal request that produced the observation.</param>
    /// <param name="frame">The correlated frame identity, or null when the observation was not exact.</param>
    /// <param name="header">The primary display line.</param>
    /// <param name="detail">The secondary display line carrying tokens and offsets.</param>
    /// <param name="isExact">Whether the underlying observation was exact.</param>
    /// <param name="methodDisplay">The method signature, or the typed explanation of why no name resolved.</param>
    /// <param name="moduleName">The display name of the frame's module, or an empty string when unmatched.</param>
    /// <param name="location">The short source-location cell text, or an empty string when none was attempted.</param>
    /// <param name="locationDetail">The full source-location explanation shown as a tooltip, or null.</param>
    public CallStackFrameNode(
        DumpSelectedFrameSelector selector,
        DumpSelectedFrameIdentity? frame,
        string header,
        string detail,
        bool isExact,
        string methodDisplay = "",
        string moduleName = "",
        string location = "",
        string? locationDetail = null)
        : base(header, detail)
    {
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        Frame = frame;
        IsExact = isExact;
        MethodDisplay = methodDisplay.Length > 0 ? methodDisplay : header;
        ModuleName = moduleName;
        Location = location;
        LocationDetail = locationDetail;
    }

    /// <summary>Gets the snapshot-scoped selector that reproduces this frame.</summary>
    public DumpSelectedFrameSelector Selector { get; }

    /// <summary>Gets the correlated frame identity when the observation was exact; otherwise, null.</summary>
    public DumpSelectedFrameIdentity? Frame { get; }

    /// <summary>Gets whether this node came from an exact observation.</summary>
    public bool IsExact { get; }

    /// <summary>Gets the zero-based frame ordinal within its thread's stack.</summary>
    public int FrameOrdinal => Selector.FrameOrdinal;

    /// <summary>Gets the method signature shown in the call-stack grid's Name column.</summary>
    public string MethodDisplay { get; }

    /// <summary>Gets the display name of the frame's module, or an empty string when it could not be matched.</summary>
    public string ModuleName { get; }

    /// <summary>Gets the short source-location cell text, such as <c>Program.cs, line 12</c>.</summary>
    public string Location { get; }

    /// <summary>Gets the full source-location explanation shown as a tooltip, or null when none applies.</summary>
    public string? LocationDetail { get; }

    /// <summary>Gets the frame's IL offset as invariant text, or an empty string for a non-exact frame.</summary>
    public string IlOffset => Frame is { } frame
        ? frame.Instruction.IlOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>Gets the formatted native instruction pointer, or an empty string for a non-exact frame.</summary>
    public string InstructionPointer => Frame is { } frame
        ? DisplayFormatting.Address(frame.Instruction.NativeInstructionPointer)
        : string.Empty;
}

/// <summary>One managed thread projected for the thread grid.</summary>
public sealed class CallStackThreadNode : CallStackNode
{
    /// <summary>Creates a thread node.</summary>
    /// <param name="threadOrdinal">The zero-based producer ordinal of the thread.</param>
    /// <param name="header">The primary display line.</param>
    /// <param name="detail">The secondary display line.</param>
    /// <param name="managedThreadId">The runtime-reported managed thread id, or null when unknown.</param>
    /// <param name="runtimeThreadAddress">The formatted runtime thread address, or an empty string.</param>
    public CallStackThreadNode(
        int threadOrdinal,
        string header,
        string detail,
        uint? managedThreadId = null,
        string runtimeThreadAddress = "")
        : base(header, detail)
    {
        ThreadOrdinal = threadOrdinal;
        ManagedThreadId = managedThreadId;
        RuntimeThreadAddress = runtimeThreadAddress;
    }

    /// <summary>Gets the zero-based producer ordinal of the thread.</summary>
    public int ThreadOrdinal { get; }

    /// <summary>Gets the runtime-reported managed thread id, or null when the probe did not observe one.</summary>
    public uint? ManagedThreadId { get; }

    /// <summary>Gets the formatted runtime thread address, or an empty string.</summary>
    public string RuntimeThreadAddress { get; }

    /// <summary>Gets the top frame's method display, shown as the thread grid's Location column.</summary>
    public string Location => Frames.Count > 0 ? Frames[0].MethodDisplay : string.Empty;

    /// <summary>Gets the bounded managed frames enumerated for this thread, in stack order.</summary>
    public ObservableCollection<CallStackFrameNode> Frames { get; } = [];
}
