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
            Console.WriteLine("NOT_READY secret-readiness-marker-canary");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
            return 70;
        }

        if (args is ["--harness-exit-before-ready"])
        {
            Console.Error.WriteLine("secret-readiness-stderr-canary");
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
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_SECRET_CANARY") is not null)
        {
            Console.WriteLine("UNSAFE_ENVIRONMENT");
            Console.Out.Flush();
            return 72;
        }

        RetOnly();
        if (FatBodyWithLocalsAndExceptionRegions(41) != 42)
        {
            return 74;
        }

        var dumpProbeRoot = GCHandle.Alloc(
            new DumpProbe(
                marker: 0x13579BDF,
                message: "dump-memory-evidence:\uD83D\uDE80 exact rooted string"),
            GCHandleType.Normal);

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
        Message = message;
        OptionalMessage = null;
        LongMessage = new string('x', 5000);
        PresentCount = 73;
        OptionalCount = null;
        Enabled = true;
    }

    internal readonly int Marker;

    internal readonly string Message;

    internal readonly string? OptionalMessage;

    internal readonly string LongMessage;

    internal readonly int? PresentCount;

    internal readonly int? OptionalCount;

    internal readonly bool Enabled;
}
