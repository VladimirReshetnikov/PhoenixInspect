using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RetOnly()
    {
    }

    private static void Main()
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null ||
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("GH_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_SECRET_CANARY") is not null)
        {
            Console.WriteLine("UNSAFE_ENVIRONMENT");
            Console.Out.Flush();
            return;
        }

        RetOnly();
        var dumpProbeRoot = GCHandle.Alloc(
            new DumpProbe(
                marker: 0x13579BDF,
                message: "dump-memory-evidence:\uD83D\uDE80 exact rooted string"),
            GCHandleType.Normal);

        Console.WriteLine("READY");
        Console.Out.Flush();

        Thread.Sleep(Timeout.Infinite);
        dumpProbeRoot.Free();
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
        Enabled = true;
    }

    internal readonly int Marker;

    internal readonly string Message;

    internal readonly string? OptionalMessage;

    internal readonly string LongMessage;

    internal readonly bool Enabled;
}
