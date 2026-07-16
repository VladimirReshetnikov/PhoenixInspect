using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RetOnly()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int FatBodyWithLocalsAndExceptionRegions(int value)
    {
        int result;
        try
        {
            result = checked(value + 1);
        }
        catch (OverflowException)
        {
            result = int.MaxValue;
        }
        finally
        {
            GC.KeepAlive(value);
        }

        return result;
    }

    private static int Main(string[] args)
    {
        if (args is ["--harness-invalid-readiness"])
        {
            Console.WriteLine("NOT_READY artifact-readiness-marker-canary");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
            return 70;
        }

        if (args is ["--harness-exit-before-ready"])
        {
            Console.Error.WriteLine("artifact-readiness-stderr-canary");
            Console.Error.Flush();
            return 71;
        }

        if (args is ["--harness-never-ready"])
        {
            Thread.Sleep(Timeout.Infinite);
            return 73;
        }

        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null ||
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("GH_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_ARTIFACT_CANARY") is not null)
        {
            Console.WriteLine("UNEXPECTED_ENVIRONMENT");
            Console.Out.Flush();
            return 72;
        }

        if (args is ["--synthetic-request-pipeline", var requestMarker, var requestAlternate, var requestState])
        {
            return RunSyntheticRequestPipeline(requestMarker, requestAlternate, requestState);
        }

        if (args is ["--synthetic-batch-pipeline", var batchMarker, var batchAlternate, var batchState])
        {
            return RunSyntheticBatchPipeline(batchMarker, batchAlternate, batchState);
        }

        if (args is ["--synthetic-coordinator-pipeline", var coordinatorMarker, var coordinatorAlternate, var coordinatorState])
        {
            return RunSyntheticCoordinatorPipeline(coordinatorMarker, coordinatorAlternate, coordinatorState);
        }

        if (args is ["--synthetic-workflow-dispatch", var workflowMarker, var workflowAlternate, var workflowState])
        {
            return RunSyntheticWorkflowDispatch(workflowMarker, workflowAlternate, workflowState);
        }

        if (args is ["--synthetic-certificate-profiles", var profileMarker, var profileAlternate, var profileState])
        {
            return RunSyntheticCertificateProfiles(profileMarker, profileAlternate, profileState);
        }

        RetOnly();
        if (FatBodyWithLocalsAndExceptionRegions(41) != 42)
        {
            return 74;
        }

        var dumpProbe = new DumpProbe(
            marker: 0x13579BDF,
            message: "dump-memory-evidence:\uD83D\uDE80 exact rooted string");
        if (dumpProbe.GetMarker() != 0x13579BDF ||
            dumpProbe.GetAdjustedMarker() != unchecked(0x13579BDF + 1) ||
            dumpProbe.GetDuplicatedMarker() != unchecked(0x13579BDF + 0x13579BDF) ||
            dumpProbe.GetMarkerSummary() != 0x26AF37BD)
        {
            return 75;
        }

        try
        {
            DumpProbe? nullProbe = null;
            _ = nullProbe!.GetMarker();
            return 76;
        }
        catch (NullReferenceException)
        {
            // Reaching READY below is the CoreCLR oracle for the typed-null getter boundary exercised by W3.
        }

        var dumpProbeRoot = GCHandle.Alloc(dumpProbe, GCHandleType.Normal);

        Console.WriteLine("READY");
        Console.Out.Flush();

        Thread.Sleep(Timeout.Infinite);
        dumpProbeRoot.Free();
        return 0;
    }

    private static int RunSyntheticRequestPipeline(
        string markerText,
        string alternateText,
        string state)
    {
        if (!TryParseSyntheticArguments(markerText, alternateText, state, out var marker, out var alternate))
        {
            return 77;
        }

        var root = new SyntheticRequestPipelineProbe(marker, alternate, state);
        if (root.GetMarkerSummary() != unchecked(marker + alternate))
        {
            return 78;
        }

        return PauseWithStrongRoot(root);
    }

    private static int RunSyntheticBatchPipeline(
        string markerText,
        string alternateText,
        string state)
    {
        if (!TryParseSyntheticArguments(markerText, alternateText, state, out var marker, out var alternate))
        {
            return 79;
        }

        var root = new SyntheticBatchPipelineProbe(marker, alternate, state);
        if (root.GetMarkerSummary() != unchecked(marker + alternate))
        {
            return 80;
        }

        return PauseWithStrongRoot(root);
    }

    private static int RunSyntheticCoordinatorPipeline(
        string markerText,
        string alternateText,
        string state)
    {
        if (!TryParseSyntheticArguments(markerText, alternateText, state, out var marker, out var alternate))
        {
            return 81;
        }

        var root = new SyntheticCoordinatorPipelineProbe(marker, alternate, state);
        if (root.GetMarkerSummary() != unchecked(marker + alternate) ||
            root.Owner is null ||
            root.CurrentTask.GetState() != state)
        {
            return 82;
        }

        return PauseWithStrongRoot(root);
    }

    private static int RunSyntheticWorkflowDispatch(
        string markerText,
        string alternateText,
        string state)
    {
        if (!TryParseSyntheticArguments(markerText, alternateText, state, out var marker, out var alternate))
        {
            return 83;
        }

        var root = new SyntheticWorkflowDispatchProbe(marker, alternate, state);
        if (root.GetMarkerSummary() != unchecked(marker + alternate) ||
            root.CurrentAttempt.GetDisplayStatus() != state)
        {
            return 84;
        }

        return PauseWithStrongRoot(root);
    }

    private static int RunSyntheticCertificateProfiles(
        string markerText,
        string alternateText,
        string state)
    {
        if (!TryParseSyntheticArguments(markerText, alternateText, state, out var marker, out var alternate))
        {
            return 85;
        }

        var root = new SyntheticCertificateProfileProbe(marker, alternate, state);
        if (root.Marker != marker || root.AlternateMarker != alternate || root.Direct.Count != marker)
        {
            return 86;
        }

        return PauseWithStrongRoot(root);
    }

    private static bool TryParseSyntheticArguments(
        string markerText,
        string alternateText,
        string state,
        out int marker,
        out int alternate)
    {
        var markerParsed = int.TryParse(
            markerText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out marker);
        var alternateParsed = int.TryParse(
            alternateText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out alternate);
        return markerParsed && alternateParsed &&
            state is "clear" or "degraded" or "failed" or "running";
    }

    private static int PauseWithStrongRoot<T>(T root)
        where T : class
    {
        var strongRoot = GCHandle.Alloc(root, GCHandleType.Normal);
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(root);
        strongRoot.Free();
        return 0;
    }
}

internal sealed class DumpProbe
{
    internal DumpProbe(int marker, string message)
    {
        Marker = marker;
        AlternateMarker = unchecked(marker - 1);
        Message = message;
        OptionalMessage = null;
        LongMessage = new string('x', 5000);
        PresentCount = 73;
        OptionalCount = null;
        Enabled = true;
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly string Message;

    internal readonly string? OptionalMessage;

    internal readonly string LongMessage;

    internal readonly int? PresentCount;

    internal readonly int? OptionalCount;

    internal readonly bool Enabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarker() => Marker;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetAdjustedMarker() => Marker + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetDuplicatedMarker() => Marker + Marker;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CombineMarkers(int marker, int alternateMarker) =>
        unchecked(marker + alternateMarker);
}

internal sealed class SyntheticRequestPipelineProbe
{
    internal SyntheticRequestPipelineProbe(int marker, int alternateMarker, string state)
    {
        Marker = marker;
        AlternateMarker = alternateMarker;
        Failure = state is "failed" or "degraded"
            ? new SyntheticFailureRecord(state == "failed" ? "request-failed" : "request-degraded")
            : null;
        CurrentRequest = new SyntheticRequestState(
            state,
            CorrelationId: $"request-{marker:X8}-{alternateMarker:X8}");
        RetryMarkers = [marker, alternateMarker, unchecked(marker + alternateMarker)];
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly SyntheticFailureRecord? Failure;

    internal readonly SyntheticRequestState CurrentRequest;

    internal readonly int[] RetryMarkers;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CombineMarkers(int marker, int alternateMarker) =>
        unchecked(marker + alternateMarker);
}

internal sealed class SyntheticBatchPipelineProbe
{
    internal SyntheticBatchPipelineProbe(int marker, int alternateMarker, string state)
    {
        Marker = marker;
        AlternateMarker = alternateMarker;
        LastFailure = state is "failed" or "degraded"
            ? new SyntheticFailureRecord(state == "failed" ? "batch-failed" : "batch-degraded")
            : null;
        Progress = new SyntheticBatchProgress(
            state,
            CompletedPartitions: Math.Abs(marker % 17),
            TotalPartitions: 17);
        PartitionMarkers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["primary"] = marker,
            ["alternate"] = alternateMarker,
        };
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly SyntheticFailureRecord? LastFailure;

    internal readonly SyntheticBatchProgress Progress;

    internal readonly Dictionary<string, int> PartitionMarkers;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CombineMarkers(int marker, int alternateMarker) =>
        unchecked(marker + alternateMarker);
}

internal sealed record SyntheticFailureRecord(string Code);

internal sealed record SyntheticRequestState(string Status, string CorrelationId);

internal sealed record SyntheticBatchProgress(string State, int CompletedPartitions, int TotalPartitions);

internal sealed class SyntheticCoordinatorPipelineProbe
{
    internal SyntheticCoordinatorPipelineProbe(int marker, int alternateMarker, string state)
    {
        Marker = marker;
        AlternateMarker = alternateMarker;
        Owner = new SyntheticCoordinatorOwner(
            Name: $"coordinator-{marker:X8}",
            Region: marker >= 0 ? "west" : "east");
        ActiveJob = state is "failed" or "degraded"
            ? new SyntheticCoordinatorJob(
                RetryCount: Math.Abs(alternateMarker % 7),
                JobId: $"job-{alternateMarker:X8}")
            : null;
        ActiveShard = new SyntheticCoordinatorShard(
            Id: $"shard-{Math.Abs(marker % 11)}",
            Health: new SyntheticShardHealth(
                State: state is "failed" ? "unhealthy" : state,
                FailedWorkers: state is "failed" ? 2 : state is "degraded" ? 1 : 0));
        Workers =
        [
            new SyntheticCoordinatorWorker(State: state, QueueDepth: Math.Abs(marker % 13)),
            new SyntheticCoordinatorWorker(State: "standby", QueueDepth: Math.Abs(alternateMarker % 13)),
        ];
        CurrentTask = new SyntheticCoordinatorTask(state);
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly SyntheticCoordinatorOwner? Owner;

    internal readonly SyntheticCoordinatorJob? ActiveJob;

    internal readonly SyntheticCoordinatorShard ActiveShard;

    internal readonly SyntheticCoordinatorWorker[] Workers;

    internal readonly SyntheticCoordinatorTask CurrentTask;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CombineMarkers(int marker, int alternateMarker) =>
        unchecked(marker + alternateMarker);
}

internal sealed record SyntheticCoordinatorOwner(string Name, string Region);

internal sealed record SyntheticCoordinatorJob(int RetryCount, string JobId);

internal sealed record SyntheticCoordinatorShard(string Id, SyntheticShardHealth Health);

internal sealed record SyntheticShardHealth(string State, int FailedWorkers);

internal sealed record SyntheticCoordinatorWorker(string State, int QueueDepth);

internal sealed class SyntheticCoordinatorTask
{
    private readonly string state;

    internal SyntheticCoordinatorTask(string state)
    {
        this.state = state;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal string GetState() => state;
}

internal sealed class SyntheticWorkflowDispatchProbe
{
    internal SyntheticWorkflowDispatchProbe(int marker, int alternateMarker, string state)
    {
        Marker = marker;
        AlternateMarker = alternateMarker;
        var primaryWorker = new SyntheticAssignedWorker(
            State: state is "failed" ? "draining" : "assigned",
            Node: $"worker-{Math.Abs(marker % 19)}");
        var standbyWorker = new SyntheticAssignedWorker(
            State: "standby",
            Node: $"worker-{Math.Abs(alternateMarker % 19)}");
        CurrentAttempt = new SyntheticWorkflowAttempt(
            Status: state,
            Worker: primaryWorker,
            Sequence: Math.Abs(marker % 31));
        OptionalError = state is "failed" or "degraded"
            ? new SyntheticWorkflowError(
                Code: state == "failed" ? "dispatch-failed" : "dispatch-degraded",
                Message: $"workflow-{marker:X8}-{alternateMarker:X8}")
            : null;
        AssignedWorker = primaryWorker;
        Attempts =
        [
            CurrentAttempt,
            new SyntheticWorkflowAttempt(
                Status: "queued",
                Worker: standbyWorker,
                Sequence: Math.Abs(alternateMarker % 31)),
        ];
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly SyntheticWorkflowAttempt CurrentAttempt;

    internal readonly SyntheticWorkflowError? OptionalError;

    internal readonly SyntheticAssignedWorker AssignedWorker;

    internal readonly SyntheticWorkflowAttempt[] Attempts;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int GetMarkerSummary() => CombineMarkers(Marker, AlternateMarker);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CombineMarkers(int marker, int alternateMarker) =>
        unchecked(marker + alternateMarker);
}

internal sealed record SyntheticWorkflowAttempt(string Status, SyntheticAssignedWorker Worker, int Sequence)
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal string GetDisplayStatus() => Status;
}

internal sealed record SyntheticAssignedWorker(string State, string Node);

internal sealed record SyntheticWorkflowError(string Code, string Message);

internal sealed class SyntheticCertificateProfileProbe
{
    internal SyntheticCertificateProfileProbe(int marker, int alternateMarker, string state)
    {
        Marker = marker;
        AlternateMarker = alternateMarker;
        Direct = new SyntheticDirectTerminalProfile(state, marker, alternateMarker < 0 ? null : alternateMarker);
        AutoNullable = new SyntheticAutoNullableProfile(alternateMarker < 0 ? null : alternateMarker);
        Computed = new SyntheticComputedPropertyProfile(marker);
        Indexed = new SyntheticIndexedPropertyProfile(state);
        Static = new SyntheticStaticPropertyProfile();
        Inherited = new SyntheticInheritedPropertyProfile(marker);
        Unsupported = new SyntheticUnsupportedPropertyProfile(state == "failed");
        Mismatched = new SyntheticMismatchedPropertyProfile(state);
        Call = new SyntheticCallPropertyProfile(marker);
        Virtual = new SyntheticVirtualPropertyProfile(alternateMarker);
    }

    internal readonly int Marker;

    internal readonly int AlternateMarker;

    internal readonly SyntheticDirectTerminalProfile Direct;

    internal readonly SyntheticAutoNullableProfile AutoNullable;

    internal readonly SyntheticComputedPropertyProfile Computed;

    internal readonly SyntheticIndexedPropertyProfile Indexed;

    internal readonly SyntheticStaticPropertyProfile Static;

    internal readonly SyntheticInheritedPropertyProfile Inherited;

    internal readonly SyntheticUnsupportedPropertyProfile Unsupported;

    internal readonly SyntheticMismatchedPropertyProfile Mismatched;

    internal readonly SyntheticCallPropertyProfile Call;

    internal readonly SyntheticVirtualPropertyProfile Virtual;
}

internal sealed class SyntheticDirectTerminalProfile
{
    internal SyntheticDirectTerminalProfile(string text, int count, int? optionalCount)
    {
        Text = text;
        Count = count;
        OptionalCount = optionalCount;
    }

    internal readonly string Text;

    internal readonly int Count;

    internal readonly int? OptionalCount;
}

internal sealed class SyntheticAutoNullableProfile
{
    internal SyntheticAutoNullableProfile(int? optionalValue)
    {
        OptionalValue = optionalValue;
    }

    internal int? OptionalValue { get; }
}

internal sealed class SyntheticComputedPropertyProfile
{
    private readonly int value;

    internal SyntheticComputedPropertyProfile(int value)
    {
        this.value = value;
    }

    internal int Value => checked((value * 3) + 1);
}

internal sealed class SyntheticIndexedPropertyProfile
{
    private readonly string value;

    internal SyntheticIndexedPropertyProfile(string value)
    {
        this.value = value;
    }

    internal string this[int index] => index == 0 ? value : string.Empty;
}

internal sealed class SyntheticStaticPropertyProfile
{
    internal static int Value => 17;
}

internal class SyntheticInheritedPropertyBaseProfile
{
    internal SyntheticInheritedPropertyBaseProfile(int value)
    {
        Value = value;
    }

    internal int Value { get; }
}

internal sealed class SyntheticInheritedPropertyProfile : SyntheticInheritedPropertyBaseProfile
{
    internal SyntheticInheritedPropertyProfile(int value)
        : base(value)
    {
    }
}

internal sealed class SyntheticUnsupportedPropertyProfile
{
    internal SyntheticUnsupportedPropertyProfile(bool value)
    {
        Value = value;
    }

    internal bool Value { get; }
}

internal sealed class SyntheticMismatchedPropertyProfile
{
    private readonly string value;

    internal SyntheticMismatchedPropertyProfile(string value)
    {
        this.value = value;
    }

    internal object Value => value;
}

internal sealed class SyntheticCallPropertyProfile
{
    private readonly int value;

    internal SyntheticCallPropertyProfile(int value)
    {
        this.value = value;
    }

    internal int Value => Echo(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Echo(int value) => value;
}

internal class SyntheticVirtualPropertyProfile
{
    private readonly int value;

    internal SyntheticVirtualPropertyProfile(int value)
    {
        this.value = value;
    }

    internal virtual int Value => value;
}
