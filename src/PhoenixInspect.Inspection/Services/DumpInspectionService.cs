using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>Carries the complete outcome of one bounded strong-handle object search.</summary>
/// <param name="Rows">The retained matches, in adapter order.</param>
/// <param name="Facts">Traversal counters and caps describing how exhaustive the search actually was.</param>
/// <param name="Summary">A one-line description of the outcome for the panel header.</param>
/// <param name="IsExhaustive">Whether the traversal completed without reaching a cap or partial read.</param>
public sealed record HeapObjectSearchProjection(
    ImmutableArray<HeapObjectRow> Rows,
    ImmutableArray<PropertyRow> Facts,
    string Summary,
    bool IsExhaustive);

/// <summary>Carries everything the shell needs after a dump is opened, in one session-thread round trip.</summary>
/// <param name="Properties">The grouped snapshot facts for the overview panel.</param>
/// <param name="Modules">The projected managed module instances.</param>
/// <param name="SnapshotSha256">The complete dump content digest.</param>
/// <param name="TargetPlatform">The runtime-reported target platform.</param>
/// <param name="TargetArchitecture">The runtime-reported target architecture.</param>
public sealed record SnapshotProjection(
    ImmutableArray<PropertyRow> Properties,
    ImmutableArray<ModuleRow> Modules,
    string SnapshotSha256,
    string TargetPlatform,
    string TargetArchitecture);

/// <summary>Carries the decoded parameters and local slots of one selected frame for the Locals pane.</summary>
/// <param name="Rows">The parameter rows followed by the local-slot rows, in declaration order.</param>
/// <param name="Summary">A one-line description of what was decoded and what was not.</param>
public sealed record FrameVariablesProjection(
    ImmutableArray<FrameVariableRow> Rows,
    string Summary);

/// <summary>Carries the bounded call-stack probe result together with an explanation of its limits.</summary>
/// <param name="Threads">Thread nodes that carry at least one exact managed frame.</param>
/// <param name="Summary">A one-line description of what was probed and what was found.</param>
/// <param name="Note">An explanation of why some probed ordinals cannot be shown.</param>
public sealed record CallStackProjection(
    ImmutableArray<CallStackThreadNode> Threads,
    string Summary,
    string Note);

/// <summary>
/// Projects the currently implemented dump-inspection contracts into immutable display models.
/// </summary>
/// <remarks>
/// Every member of this service runs on the dump host's dedicated adapter thread and returns only value-like display
/// models plus the adapter evidence objects that later operations need as input. No member interprets or widens an
/// adapter result: statuses, issues, counters, and caps are carried through verbatim.
/// </remarks>
public static class DumpInspectionService
{
    /// <summary>Loads every fact the shell shows immediately after a dump is opened.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="dumpPath">The path the session was opened from; it is display provenance, never identity.</param>
    /// <param name="dumpLength">The dump file length in bytes.</param>
    /// <returns>The combined snapshot and module projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static SnapshotProjection LoadSnapshot(ClrmdDumpSession session, string dumpPath, long dumpLength)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SnapshotProjection(
            DescribeSnapshot(session, dumpPath, dumpLength),
            DescribeModules(session),
            session.Snapshot.Sha256,
            session.TargetPlatform,
            session.TargetArchitecture);
    }

    /// <summary>Projects a suspended live-attach session: process facts instead of file facts.</summary>
    /// <param name="session">The open live-attach session.</param>
    /// <returns>The complete display projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static SnapshotProjection LoadLiveSnapshot(ClrmdDumpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rows = ImmutableArray.CreateBuilder<PropertyRow>();
        rows.Add(new PropertyRow(
            "Live session",
            "Process",
            $"{session.TargetProcessName} (PID {session.TargetProcessId})",
            "Every thread of the target is suspended for the lifetime of this session; closing it resumes them."));
        if (session.AttachedAtUtc is { } attachedAt)
        {
            rows.Add(new PropertyRow(
                "Live session",
                "Attached at (UTC)",
                attachedAt.ToString("O", CultureInfo.InvariantCulture)));
        }

        rows.Add(new PropertyRow(
            "Live session",
            "Session identity",
            session.Snapshot.Sha256,
            "SHA-256 of the attach circumstances - machine, process, start time, attach time. A live process has "
            + "no immutable content identity: reads replay within this session because the target is suspended, "
            + "but a fresh attach is a different snapshot."));
        rows.Add(new PropertyRow("Live session", "Memory source", session.Snapshot.MemorySourceId));
        AddTargetAndBounds(rows, session);
        return new SnapshotProjection(
            rows.ToImmutable(),
            DescribeModules(session),
            session.Snapshot.Sha256,
            session.TargetPlatform,
            session.TargetArchitecture);
    }

    /// <summary>Describes the immutable snapshot, the adapter's declared caps, and the enabled evaluation profiles.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="dumpPath">The path the session was opened from; it is display provenance, never identity.</param>
    /// <param name="dumpLength">The dump file length in bytes.</param>
    /// <returns>Grouped property rows ready for the overview panel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static ImmutableArray<PropertyRow> DescribeSnapshot(
        ClrmdDumpSession session,
        string dumpPath,
        long dumpLength)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rows = ImmutableArray.CreateBuilder<PropertyRow>();

        rows.Add(new PropertyRow("Snapshot", "File", Path.GetFileName(dumpPath), dumpPath));
        rows.Add(new PropertyRow("Snapshot", "Size", DisplayFormatting.ByteSize(dumpLength)));
        rows.Add(new PropertyRow(
            "Snapshot",
            "Content identity",
            session.Snapshot.Sha256,
            "SHA-256 of the complete dump file. Identity excludes the local path, so reopening the same dump "
            + "elsewhere produces the same identity."));
        rows.Add(new PropertyRow("Snapshot", "Memory source", session.Snapshot.MemorySourceId));
        AddTargetAndBounds(rows, session);
        return rows.ToImmutable();
    }

    /// <summary>Adds the target, declared-bound, and profile facts shared by dump and live-attach sessions.</summary>
    private static void AddTargetAndBounds(
        ImmutableArray<PropertyRow>.Builder rows,
        ClrmdDumpSession session)
    {
        rows.Add(new PropertyRow("Target", "Platform", session.TargetPlatform));
        rows.Add(new PropertyRow("Target", "Architecture", session.TargetArchitecture));
        rows.Add(new PropertyRow(
            "Target",
            "Managed module instances",
            DisplayFormatting.Count(session.Modules.Length)));

        AddBound(rows, "Declared bounds", ClrmdDumpSession.InstanceFieldTraversalBound);
        AddBound(rows, "Declared bounds", ClrmdDumpSession.SelectedFrameThreadTraversalBound);
        AddBound(rows, "Declared bounds", ClrmdDumpSession.SelectedFrameTraversalBound);
        AddBound(rows, "Declared bounds", ClrmdDumpSession.PortablePdbCandidateTraversalBound);
        AddBound(rows, "Declared bounds", ClrmdDumpSession.PortablePdbArtifactByteBound);

        rows.Add(new PropertyRow(
            "Expression front end",
            "Profile",
            DumpCSharpExpressionProfile.Id,
            $"{DumpCSharpExpressionProfile.PackageId} {DumpCSharpExpressionProfile.PackageVersion}"));
        rows.Add(new PropertyRow(
            "Expression front end",
            "Static-field profile",
            StaticFieldExpressionDescriptor.ProfileId,
            "Context-independent fully qualified static fields bind without stack or PDB evidence; contextual names "
            + "additionally require selected-frame and Portable-PDB import facts."));
    }

    /// <summary>Projects every managed module instance reported by the runtime.</summary>
    /// <param name="session">The open dump session.</param>
    /// <returns>Module rows ordered by display name and then metadata address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static ImmutableArray<ModuleRow> DescribeModules(ClrmdDumpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Modules
            .Select(static module => new ModuleRow(
                module,
                module.Name,
                module.AppDomainId,
                DisplayFormatting.Address(module.MetadataAddress),
                module.MetadataLength == 0
                    ? DisplayFormatting.Absent
                    : DisplayFormatting.ByteSize((long)module.MetadataLength),
                module.Layout,
                module.TargetPathHint ?? DisplayFormatting.Absent,
                DescribeSymbolHint(module.TargetPathHint)))
            .OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static row => row.Module.MetadataAddress)
            .ToImmutableArray();
    }

    /// <summary>States whether a hint-derived Portable-PDB candidate exists on the analysis machine.</summary>
    /// <param name="targetPathHint">The unmodified target-side path hint, or null.</param>
    /// <returns>A short discovery statement; a present candidate is still identity-validated before use.</returns>
    private static string DescribeSymbolHint(string? targetPathHint)
    {
        if (string.IsNullOrEmpty(targetPathHint))
        {
            return "no path hint";
        }

        try
        {
            return File.Exists(Path.ChangeExtension(targetPathHint, ".pdb"))
                ? "candidate .pdb found"
                : "no .pdb beside hint";
        }
        catch (ArgumentException)
        {
            // A target-side hint may not be a well-formed path on this machine; that is a discovery miss, not an error.
            return "no .pdb beside hint";
        }
    }

    /// <summary>Reads and describes one module's counted metadata content identity from dump memory.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="module">The module instance whose metadata should be read.</param>
    /// <returns>
    /// Property rows carrying the adapter status and, when available, the MVID, counted length, and metadata digest.
    /// </returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ImmutableArray<PropertyRow> DescribeModuleContent(
        ClrmdDumpSession session,
        ClrmdModuleInfo module)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(module);
        var rows = ImmutableArray.CreateBuilder<PropertyRow>();
        rows.Add(new PropertyRow("Module instance", "Name", module.Name));
        rows.Add(new PropertyRow("Module instance", "Application domain", module.AppDomainId.ToString(CultureInfo.InvariantCulture)));
        rows.Add(new PropertyRow("Module instance", "Image layout", module.Layout));
        rows.Add(new PropertyRow("Module instance", "Metadata root", DisplayFormatting.Address(module.MetadataAddress)));
        rows.Add(new PropertyRow(
            "Module instance",
            "Reported metadata length",
            module.MetadataLength == 0
                ? DisplayFormatting.Absent
                : DisplayFormatting.Count((long)module.MetadataLength) + " B"));
        rows.Add(new PropertyRow(
            "Module instance",
            "Target path hint",
            module.TargetPathHint ?? DisplayFormatting.Absent,
            "Target-side hint only. It is not identity and is never opened without independent artifact discovery."));

        var content = session.ReadModuleContentIdentity(module);
        rows.Add(new PropertyRow("Metadata read", "Status", content.Status.ToString()));
        rows.Add(new PropertyRow("Metadata read", "Issue", content.Issue.ToString()));
        if (content.Value is { } identity)
        {
            rows.Add(new PropertyRow("Metadata read", "MVID", identity.Mvid.ToString("D", CultureInfo.InvariantCulture)));
            rows.Add(new PropertyRow(
                "Metadata read",
                "Counted length",
                DisplayFormatting.Count(identity.MetadataLength) + " B"));
            rows.Add(new PropertyRow("Metadata read", "Metadata SHA-256", identity.MetadataSha256));
        }
        else
        {
            rows.Add(new PropertyRow(
                "Metadata read",
                "Content identity",
                DisplayFormatting.Absent,
                "The complete metadata bytes required to compute a content identity were not available in this snapshot."));
        }

        foreach (var read in content.Evidence)
        {
            rows.Add(new PropertyRow(
                "Raw reads",
                DisplayFormatting.Address(read.Address),
                $"{read.Status}: {DisplayFormatting.Count(read.BytesRead)} of {DisplayFormatting.Count(read.RequestedLength)} B"));
        }

        foreach (var bound in content.AppliedBounds)
        {
            rows.Add(new PropertyRow("Applied bounds", bound.Name, DisplayFormatting.Count(bound.Value)));
        }

        return rows.ToImmutable();
    }

    /// <summary>Probes managed thread ordinals and returns the top frame of every thread that carries one.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="threadOrdinalsToProbe">The number of zero-based thread ordinals to probe.</param>
    /// <returns>The bounded projection together with an explicit statement of what the probe cannot distinguish.</returns>
    /// <remarks>
    /// The adapter selects one frame at a time by snapshot-scoped ordinal and does not publish a thread count. A probe
    /// therefore cannot distinguish a past-the-end thread ordinal from a live thread with no managed frames: both
    /// return the same typed unavailable observation. Complete stacks are loaded on demand by
    /// <see cref="LoadFrames(ClrmdDumpSession, CallStackThreadNode, int, System.Collections.Immutable.ImmutableArray{string})"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="threadOrdinalsToProbe"/> is not positive.</exception>
    public static CallStackProjection ProbeCallStacks(ClrmdDumpSession session, int threadOrdinalsToProbe)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThan(threadOrdinalsToProbe, 1);

        var threads = ImmutableArray.CreateBuilder<CallStackThreadNode>();
        var withoutFrame = 0;
        var boundReached = false;
        for (var ordinal = 0; ordinal < threadOrdinalsToProbe; ordinal++)
        {
            var selector = DumpSelectedFrameSelector.Create(session.Snapshot, ordinal, 0);
            var observation = session.SelectExpressionFrame(selector);
            if (observation.Status == DumpContextEvidenceStatus.Partial &&
                observation.Issue == DumpContextEvidenceIssue.BoundReached)
            {
                boundReached = true;
                break;
            }

            if (observation.Frame is not { } frame)
            {
                withoutFrame++;
                continue;
            }

            var node = new CallStackThreadNode(
                ordinal,
                $"Thread #{ordinal}  ·  managed id {frame.ManagedThreadId}",
                $"runtime thread {DisplayFormatting.Address(frame.RuntimeThreadAddress)}",
                frame.ManagedThreadId,
                DisplayFormatting.Address(frame.RuntimeThreadAddress));
            node.Frames.Add(CreateFrameNode(session, selector, observation));
            threads.Add(node);
        }

        var summary = boundReached
            ? $"The snapshot exceeds the adapter's declared managed-thread cap of "
              + $"{DisplayFormatting.Count(ClrmdDumpSession.SelectedFrameThreadTraversalBound.Value)}; no frame can be selected."
            : $"Probed {DisplayFormatting.Count(threadOrdinalsToProbe)} thread ordinals; "
              + $"{DisplayFormatting.Count(threads.Count)} carry an exact managed frame 0.";

        var note = boundReached
            ? "Selected-frame acquisition reports a reached bound rather than choosing among threads."
            : $"{DisplayFormatting.Count(withoutFrame)} probed ordinals returned no managed frame 0. The adapter "
              + "returns the same typed unavailable observation for a past-the-end ordinal and for a live thread with "
              + "no managed frames, so those ordinals are not listed.";

        return new CallStackProjection(threads.ToImmutable(), summary, note);
    }

    /// <summary>Loads the complete bounded managed call stack of one already-probed thread.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="node">The thread node whose stack should be walked.</param>
    /// <param name="maximumFrames">The maximum number of frames to retain for this thread.</param>
    /// <returns>The frames from the top of the stack downward, in stack order.</returns>
    /// <remarks>
    /// When <paramref name="portablePdbCandidates"/> is initialized — even empty — each exact frame is additionally
    /// resolved to its Portable-PDB source location so the call-stack grid can show a file and line per frame. The
    /// default value skips that resolution entirely, which keeps callers that only need frame identities fast.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumFrames"/> is not positive.</exception>
    /// <param name="portablePdbCandidates">
    /// Local Portable-PDB candidate paths offered to per-frame source resolution, or default to skip resolution.
    /// </param>
    public static ImmutableArray<CallStackFrameNode> LoadFrames(
        ClrmdDumpSession session,
        CallStackThreadNode node,
        int maximumFrames,
        ImmutableArray<string> portablePdbCandidates = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFrames, 1);

        var frames = ImmutableArray.CreateBuilder<CallStackFrameNode>();
        for (var ordinal = 0; ordinal < maximumFrames; ordinal++)
        {
            var selector = DumpSelectedFrameSelector.Create(session.Snapshot, node.ThreadOrdinal, ordinal);
            var observation = session.SelectExpressionFrame(selector);
            if (observation.Status == DumpContextEvidenceStatus.Unavailable)
            {
                break;
            }

            frames.Add(CreateFrameNode(session, selector, observation, portablePdbCandidates));
            if (observation.Frame is null)
            {
                break;
            }
        }

        return frames.ToImmutable();
    }

    /// <summary>Runs one bounded strong-handle search and projects its matches and traversal facts.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="typeName">The exact ordinal runtime type-name predicate; it is not case-folded.</param>
    /// <param name="maximumMatches">The retained-match cap.</param>
    /// <param name="maximumHandlesScanned">The handle-inspection cap.</param>
    /// <returns>The projected matches together with the counters that describe how exhaustive the search was.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static HeapObjectSearchProjection SearchObjects(
        ClrmdDumpSession session,
        string typeName,
        int maximumMatches,
        int maximumHandlesScanned)
    {
        ArgumentNullException.ThrowIfNull(session);
        var search = session.FindStrongHandleObjectsByTypeName(typeName, maximumMatches, maximumHandlesScanned);
        var rows = search.Matches
            .Select(static match => new HeapObjectRow(
                match,
                match.TypeName,
                DisplayFormatting.Address(match.Address),
                DisplayFormatting.Address(match.MethodTable),
                match.RootKind,
                DisplayFormatting.Address(match.RootAddress),
                match.Module.Name))
            .ToImmutableArray();

        var facts = ImmutableArray.CreateBuilder<PropertyRow>();
        facts.Add(new PropertyRow("Search", "Type-name predicate", search.TypeNameSelector));
        facts.Add(new PropertyRow("Search", "Status", search.Status.ToString()));
        facts.Add(new PropertyRow("Search", "Issue", search.Issue.ToString()));
        facts.Add(new PropertyRow("Traversal", "Handles scanned", DisplayFormatting.Count(search.HandlesScanned)));
        facts.Add(new PropertyRow("Traversal", "Handle cap", DisplayFormatting.Count(search.MaximumHandlesScanned)));
        facts.Add(new PropertyRow("Traversal", "Matches retained", DisplayFormatting.Count(search.MatchesRetained)));
        facts.Add(new PropertyRow("Traversal", "Match cap", DisplayFormatting.Count(search.MaximumMatches)));
        facts.Add(new PropertyRow(
            "Traversal",
            "Match cap reached",
            search.MatchLimitReached ? "Yes" : "No",
            search.MatchLimitReached
                ? "A further match existed beyond the retained-match cap, so this list is a prefix."
                : null));

        var isExhaustive = search.Status == ClrmdEvidenceStatus.Exact && !search.MatchLimitReached;
        var summary = search.Status switch
        {
            ClrmdEvidenceStatus.Exact when search.MatchesRetained == 0 =>
                "Exhaustive traversal proved that no strong handle roots an object of this exact type name.",
            ClrmdEvidenceStatus.Exact when search.MatchLimitReached =>
                $"Retained {DisplayFormatting.Count(search.MatchesRetained)} matches and stopped at the match cap; "
                + "further matches exist.",
            ClrmdEvidenceStatus.Exact =>
                $"Exhaustive traversal retained {DisplayFormatting.Count(search.MatchesRetained)} matches from "
                + $"{DisplayFormatting.Count(search.HandlesScanned)} inspected handles.",
            _ =>
                $"Traversal ended {search.Status} ({search.Issue}); this list is not exhaustive.",
        };

        return new HeapObjectSearchProjection(rows, facts.ToImmutable(), summary, isExhaustive);
    }

    /// <summary>Decodes one selected frame's parameters and local slots for the Locals pane.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="frame">The frame node whose identity should be decoded.</param>
    /// <param name="portablePdbCandidates">
    /// Local Portable-PDB candidate paths offered for slot names; each is identity-validated before use.
    /// </param>
    /// <returns>The projected rows and an honest summary; a non-exact frame projects an explanation only.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static FrameVariablesProjection DescribeFrameVariables(
        ClrmdDumpSession session,
        CallStackFrameNode frame,
        ImmutableArray<string> portablePdbCandidates)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Frame is not { } identity)
        {
            return new FrameVariablesProjection(
                [],
                "The selected frame is not exact, so its variables cannot be decoded.");
        }

        var decoded = session.DescribeFrameVariables(identity, portablePdbCandidates);
        if (decoded.Value is not { } variables)
        {
            return new FrameVariablesProjection(
                [],
                $"Variable decoding ended {decoded.Status} ({decoded.Issue}); the frame module's metadata could "
                + "not supply the declarations.");
        }

        var rows = ImmutableArray.CreateBuilder<FrameVariableRow>();
        foreach (var parameter in variables.Parameters)
        {
            rows.Add(new FrameVariableRow(
                parameter.IsThis ? "this" : "Parameter",
                parameter.Name,
                parameter.TypeDisplay,
                parameter.Ordinal.ToString(CultureInfo.InvariantCulture),
                string.Empty));
        }

        foreach (var slot in variables.LocalSlots)
        {
            var notes = new List<string>(capacity: 2);
            if (slot.Name is not null && !slot.IsInScopeAtCurrentOffset)
            {
                notes.Add("out of scope at the current IL offset");
            }

            if (slot.IsDebuggerHidden)
            {
                notes.Add("debugger-hidden");
            }

            rows.Add(new FrameVariableRow(
                "Local",
                slot.Name ?? $"<slot {slot.Slot.ToString(CultureInfo.InvariantCulture)}>",
                slot.TypeDisplay,
                slot.Slot.ToString(CultureInfo.InvariantCulture),
                string.Join(", ", notes)));
        }

        var parts = new List<string>(capacity: 4)
        {
            $"{DisplayFormatting.Count(variables.Parameters.Length)} parameters and "
            + $"{DisplayFormatting.Count(variables.LocalSlots.Length)} local slots decoded from the frame "
            + "module's metadata and method body.",
        };
        if (variables.LocalSlotsNote is { } slotsNote)
        {
            parts.Add(slotsNote);
        }

        if (variables.LocalNamesNote is { } namesNote)
        {
            parts.Add(namesNote);
        }

        parts.Add(
            "Values are deliberately not shown: the adapter publishes no register or stack-slot mapping for "
            + "managed frames, and a guessed value would be fabricated evidence.");
        return new FrameVariablesProjection(rows.ToImmutable(), string.Join(" ", parts));
    }

    /// <summary>Builds the display node for one selected frame, naming its method when the metadata supports it.</summary>
    /// <param name="session">The open dump session, used to resolve the frame's method name and source location.</param>
    /// <param name="selector">The snapshot-scoped selector that produced the observation.</param>
    /// <param name="observation">The selected-frame observation.</param>
    /// <param name="portablePdbCandidates">
    /// Local Portable-PDB candidate paths offered to source resolution, or default to skip resolution.
    /// </param>
    /// <returns>The projected frame node.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static CallStackFrameNode CreateFrameNode(
        ClrmdDumpSession session,
        DumpSelectedFrameSelector selector,
        DumpSelectedFrameObservation observation,
        ImmutableArray<string> portablePdbCandidates = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Frame is not { } frame)
        {
            return new CallStackFrameNode(
                selector,
                null,
                $"Frame #{selector.FrameOrdinal}  ·  {observation.Status}",
                observation.Issue.ToString(),
                isExact: false,
                methodDisplay: $"<{observation.Status}: {observation.Issue}>");
        }

        // A frame's method name comes from that frame's own module metadata inside the snapshot. When those bytes are
        // not completely present the frame stays unnamed and says so, rather than being guessed at from the
        // instruction pointer or from a disk image that was never proven to match.
        var named = session.DescribeFrameMethod(frame);
        var declaringNamespace = string.IsNullOrEmpty(frame.DeclaringNamespace)
            ? "<global namespace>"
            : frame.DeclaringNamespace;
        var title = named.Value is { } method
            ? method.DisplayName
            : $"{declaringNamespace}.<unnamed: {named.Status}/{named.Issue}>";
        var moduleName = named.Value?.ModuleName
            ?? session.Modules.FirstOrDefault(candidate => candidate.Identity == frame.RuntimeModule)?.Name
            ?? string.Empty;

        var (location, locationDetail) = portablePdbCandidates.IsDefault
            ? (string.Empty, null)
            : DescribeFrameSourceCell(session.ResolveFrameSourceLocation(observation, portablePdbCandidates));

        return new CallStackFrameNode(
            selector,
            frame,
            $"Frame #{selector.FrameOrdinal}  ·  {title}",
            $"MethodDef {DisplayFormatting.Token(frame.MethodDefinitionToken)}  ·  "
            + $"TypeDef {DisplayFormatting.Token(frame.DeclaringTypeDefinitionToken)}  ·  "
            + $"IL+{frame.Instruction.IlOffset}  ·  IP {DisplayFormatting.Address(frame.Instruction.NativeInstructionPointer)}",
            isExact: true,
            methodDisplay: named.Value?.Signature ?? title,
            moduleName: moduleName,
            location: location,
            locationDetail: locationDetail);
    }

    /// <summary>Projects one frame-to-source resolution into a short grid cell and its tooltip explanation.</summary>
    /// <param name="resolution">The typed resolution outcome.</param>
    /// <returns>The cell text and the fuller explanation, both honest about what the PDB did and did not prove.</returns>
    private static (string Location, string? Detail) DescribeFrameSourceCell(DumpFrameSourceObservation resolution)
    {
        if (resolution.Location is { } location)
        {
            var fileName = Path.GetFileName(location.DocumentPath);
            var cell = string.IsNullOrEmpty(fileName)
                ? $"line {location.StartLine}"
                : $"{fileName}, line {location.StartLine}";
            return (cell, $"{location.DocumentPath} — the path the build recorded in the identity-matching "
                + "Portable PDB; whether that file exists on this machine is verified only when it is opened.");
        }

        var cellText = resolution.Issue switch
        {
            DumpContextEvidenceIssue.PortablePdbUnavailable => "no PDB candidate",
            DumpContextEvidenceIssue.PortablePdbIdentityMismatch => "PDB identity mismatch",
            DumpContextEvidenceIssue.PortablePdbDebugIdentityUnavailable => "no CodeView identity",
            DumpContextEvidenceIssue.SequencePointsUnavailable => "no sequence point",
            DumpContextEvidenceIssue.DocumentUnavailable => "no document",
            _ => DisplayFormatting.Humanize(resolution.Issue.ToString()),
        };
        return (cellText, $"Source resolution stopped: {resolution.Status} — {resolution.Issue}.");
    }

    private static void AddBound(
        ImmutableArray<PropertyRow>.Builder rows,
        string group,
        Core.Abstractions.EvaluationDeterministicBound bound) =>
        rows.Add(new PropertyRow(group, bound.Name, DisplayFormatting.Count(bound.Value)));
}
