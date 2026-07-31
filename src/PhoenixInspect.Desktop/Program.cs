using Avalonia;

namespace PhoenixInspect.Desktop;

/// <summary>The desktop shell entry point.</summary>
public static class Program
{
    /// <summary>Starts the Avalonia application.</summary>
    /// <param name="args">Optionally, one dump path to open at startup.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Configures the Avalonia application builder.</summary>
    /// <returns>The configured builder, also used by the designer.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
