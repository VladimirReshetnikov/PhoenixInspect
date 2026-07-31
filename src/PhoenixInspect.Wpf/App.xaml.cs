using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PhoenixInspect.Wpf;

/// <summary>
/// The demonstration host application.
/// </summary>
/// <remarks>
/// This executable is an internal exploration tool for the implemented dump-inspection and expression-evaluation
/// contracts. It is not a released product, and it deliberately adds no analysis capability of its own: everything it
/// shows is produced by an existing public contract and rendered verbatim.
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// Gets the dump path supplied on the command line, or <see langword="null"/> when the app was started without one.
    /// </summary>
    /// <remarks>
    /// Accepting a path lets an investigation be reproduced by launching the demo directly at a dump, which matters
    /// because the snapshot identity, not the path, is what the product treats as evidence identity.
    /// </remarks>
    public string? StartupDumpPath { get; private set; }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        StartupDumpPath = e.Args.FirstOrDefault(static argument =>
            !argument.StartsWith('-') && File.Exists(argument));
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A demo host is more useful when an unexpected adapter failure is reported instead of terminating the
        // process mid-investigation. The message is shown verbatim so the underlying contract stays visible.
        MessageBox.Show(
            e.Exception.ToString(),
            "PhoenixInspect — unhandled error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
