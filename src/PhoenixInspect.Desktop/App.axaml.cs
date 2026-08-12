using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PhoenixInspect.Desktop.Views;

namespace PhoenixInspect.Desktop;

/// <summary>The desktop shell application.</summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args is [DesktopNativeWindowSmokeTest.Argument])
            {
                var mainWindow = new MainWindow(null);
                DesktopNativeWindowSmokeTest.Configure(desktop, mainWindow);
                desktop.MainWindow = mainWindow;
            }
            else
            {
                var startupDump = desktop.Args is [{ Length: > 0 } path, ..] ? path : null;
                desktop.MainWindow = new MainWindow(startupDump);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
