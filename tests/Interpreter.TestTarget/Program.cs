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
