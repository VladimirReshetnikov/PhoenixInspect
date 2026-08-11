using Avalonia;

namespace PhoenixInspect.Desktop;

/// <summary>The desktop shell entry point.</summary>
public static class Program
{
    /// <summary>Starts the Avalonia application, or runs the exact non-UI packaged-payload smoke command.</summary>
    /// <param name="args">Optionally, one dump path to open at startup, or exactly <c>--smoke-test</c>.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is [DesktopSmokeTest.Argument])
        {
            return DesktopSmokeTest.Run();
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Configures the Avalonia application builder.</summary>
    /// <returns>The configured builder, also used by the designer.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
