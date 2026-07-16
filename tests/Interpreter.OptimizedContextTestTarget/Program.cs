using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Interpreter.OptimizedContextTestTarget;

internal static class Program
{
    private static int Main()
    {
#if !INTERPRETER_OPTIMIZED_RELEASE
        Console.WriteLine("INVALID_BUILD_CONFIGURATION");
        Console.Out.Flush();
        return 80;
#else
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null ||
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("GH_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_ARTIFACT_CANARY") is not null)
        {
            Console.WriteLine("UNEXPECTED_ENVIRONMENT");
            Console.Out.Flush();
            return 81;
        }

        ModeledIncidentContext.Run();
        return 0;
#endif
    }
}

internal static class ModeledIncidentContext
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        StaticContextProbe.Root = new StaticContextProbe(0x4A44C004);
        var strongRoot = GCHandle.Alloc(new StrongRootContextProbe(0x5A55C005), GCHandleType.Normal);

        try
        {
            var frameReceiver = new ThisContextProbe(0x1A11C001);
            var argument = new ArgumentContextProbe(0x2A22C002);
            frameReceiver.Capture(argument);
        }
        finally
        {
            strongRoot.Free();
            StaticContextProbe.Root = null;
        }
    }
}

internal sealed class ThisContextProbe
{
    internal ThisContextProbe(int marker)
    {
        Marker = marker;
    }

    internal readonly int Marker;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Capture(ArgumentContextProbe argument)
    {
        var local = new LocalContextProbe(0x3A33C003);

        // A compacting collection makes mere allocation insufficient for discovery. The three active-frame
        // references remain live because each is used after the non-returning-in-practice pause boundary.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        IncidentPause.WaitForDump();

        GC.KeepAlive(this);
        GC.KeepAlive(argument);
        GC.KeepAlive(local);
    }
}

internal sealed class ArgumentContextProbe
{
    internal ArgumentContextProbe(int marker)
    {
        Marker = marker;
    }

    internal readonly int Marker;
}

internal sealed class LocalContextProbe
{
    internal LocalContextProbe(int marker)
    {
        Marker = marker;
    }

    internal readonly int Marker;
}

internal sealed class StaticContextProbe
{
    internal static StaticContextProbe? Root;

    internal StaticContextProbe(int marker)
    {
        Marker = marker;
    }

    internal readonly int Marker;
}

internal sealed class StrongRootContextProbe
{
    internal StrongRootContextProbe(int marker)
    {
        Marker = marker;
    }

    internal readonly int Marker;
}

internal static class IncidentPause
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WaitForDump()
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
    }
}
