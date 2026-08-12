using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PhoenixInspect.Desktop.ViewModels;
using PhoenixInspect.Desktop.Views;

namespace PhoenixInspect.Desktop;

/// <summary>
/// Starts the production Avalonia application lifetime, shows the real shell without taking focus, and validates
/// the loaded native main-window boundary. The caller supplies the wall-clock bound around the packaged process.
/// </summary>
internal static class DesktopNativeWindowSmokeTest
{
    internal const string Argument = "--native-window-smoke-test";

    private const string FailurePrefix = "PHOENIXINSPECT_DESKTOP_NATIVE_WINDOW_SMOKE_FAILED";
    private const string SuccessPrefix = "PHOENIXINSPECT_DESKTOP_NATIVE_WINDOW_SMOKE_OK";
    private const int FailureExitCode = 70;
    private const int Pending = 0;
    private const int Succeeded = 1;
    private const int Failed = 2;

    private static int completionState;
    private static string? failureCode;

    /// <summary>Runs the native-window smoke through the production builder and classic desktop lifetime.</summary>
    /// <returns>Zero after successful validation and orderly shutdown; otherwise 70.</returns>
    internal static int Run()
    {
        Volatile.Write(ref completionState, Pending);
        Volatile.Write(ref failureCode, null);

        string productVersion;
        try
        {
            productVersion = DesktopSmokeTest.VerifyProductVersion();
        }
        catch
        {
            return ReportFailure("PRODUCT_VERSION_VERIFICATION");
        }

        int lifetimeExitCode;
        try
        {
            lifetimeExitCode = Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(
                [Argument],
                ShutdownMode.OnExplicitShutdown);
        }
        catch
        {
            return ReportFailure(Volatile.Read(ref failureCode) ?? "UNEXPECTED_ERROR");
        }

        if (Volatile.Read(ref completionState) != Succeeded)
        {
            return ReportFailure(Volatile.Read(ref failureCode) ?? "VALIDATION_NOT_COMPLETED");
        }

        if (lifetimeExitCode != 0)
        {
            return ReportFailure("LIFETIME_EXIT_CODE");
        }

        Console.Out.WriteLine($"{SuccessPrefix} version={productVersion} mode=win32-main-window");
        return 0;
    }

    /// <summary>Configures the smoke-only behavior before the lifetime shows its main window.</summary>
    /// <param name="desktop">The production classic desktop lifetime.</param>
    /// <param name="mainWindow">The real shell window created by <see cref="App"/>.</param>
    internal static void Configure(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow)
    {
        mainWindow.ShowActivated = false;
        mainWindow.Opened += OnOpened;

        void OnOpened(object? sender, EventArgs eventArgs)
        {
            mainWindow.Opened -= OnOpened;
            Dispatcher.UIThread.Post(
                () => ValidateAndShutdown(desktop, mainWindow),
                DispatcherPriority.Loaded);
        }
    }

    private static void ValidateAndShutdown(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow)
    {
        try
        {
            Require(Application.Current is App, "APP_IDENTITY");
            Require(desktop.Args is [Argument], "ARGUMENT_IDENTITY");
            Require(ReferenceEquals(desktop.MainWindow, mainWindow), "MAIN_WINDOW_IDENTITY");
            Require(
                desktop.Windows is [{ } onlyWindow] && ReferenceEquals(onlyWindow, mainWindow),
                "WINDOW_COUNT");
            Require(!mainWindow.ShowActivated, "WINDOW_ACTIVATION_POLICY");
            Require(mainWindow.IsLoaded, "WINDOW_NOT_LOADED");
            Require(mainWindow.IsVisible, "WINDOW_NOT_VISIBLE");
            Require(
                double.IsFinite(mainWindow.ClientSize.Width) && mainWindow.ClientSize.Width > 0 &&
                double.IsFinite(mainWindow.ClientSize.Height) && mainWindow.ClientSize.Height > 0,
                "WINDOW_SIZE_INVALID");

            var platformHandle = mainWindow.TryGetPlatformHandle();
            Require(platformHandle is not null, "HWND_UNAVAILABLE");
            Require(
                string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.Ordinal),
                "HWND_KIND");
            Require(platformHandle.Handle != IntPtr.Zero, "HWND_ZERO");
            Require(mainWindow.DataContext is MainWindowViewModel, "DATA_CONTEXT_IDENTITY");
            Require(
                mainWindow.FindControl<MenuItem>("RecentDumpsMenu") is not null,
                "MAIN_WINDOW_XAML_CONTROL_MISSING");

            Volatile.Write(ref completionState, Succeeded);
            desktop.Shutdown(0);
        }
        catch (NativeWindowSmokeTestFailureException failure)
        {
            FailAndShutdown(desktop, failure.Code);
        }
        catch
        {
            FailAndShutdown(desktop, "UNEXPECTED_ERROR");
        }
    }

    private static void FailAndShutdown(
        IClassicDesktopStyleApplicationLifetime desktop,
        string code)
    {
        Volatile.Write(ref failureCode, code);
        Volatile.Write(ref completionState, Failed);
        desktop.Shutdown(FailureExitCode);
    }

    private static int ReportFailure(string code)
    {
        Console.Error.WriteLine($"{FailurePrefix} {code}");
        return FailureExitCode;
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string failureCode)
    {
        if (!condition)
        {
            throw new NativeWindowSmokeTestFailureException(failureCode);
        }
    }

    private sealed class NativeWindowSmokeTestFailureException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}
