using System.Runtime.CompilerServices;
using Interpreter.Host.ExternalWorker;

namespace Interpreter.Host.ExternalWorker.Runner;

internal static class HeadlessProcessPolicy
{
    private const uint RequiredErrorMode =
        WindowsNative.SemFailCriticalErrors |
        WindowsNative.SemNoGpFaultErrorBox |
        WindowsNative.SemNoOpenFileErrorBox;

    [ModuleInitializer]
    internal static void Initialize()
    {
        var processMode = WindowsNative.GetErrorMode();
        _ = WindowsNative.SetErrorMode(processMode | RequiredErrorMode);
        var threadMode = WindowsNative.GetThreadErrorMode();
        _ = WindowsNative.SetThreadErrorMode(threadMode | RequiredErrorMode, out _);
        if (WindowsNative.WerGetFlags(WindowsNative.GetCurrentProcess(), out var werFlags) >= 0)
        {
            var noUiFlags =
                (werFlags & ~WindowsNative.WerFaultReportingAlwaysShowUi) |
                WindowsNative.WerFaultReportingNoUi;
            _ = WindowsNative.WerSetFlags(noUiFlags);
        }
    }

    internal static bool IsApplied() =>
        (WindowsNative.GetErrorMode() & RequiredErrorMode) == RequiredErrorMode &&
        (WindowsNative.GetThreadErrorMode() & RequiredErrorMode) == RequiredErrorMode &&
        WindowsNative.WerGetFlags(WindowsNative.GetCurrentProcess(), out var werFlags) >= 0 &&
        (werFlags & WindowsNative.WerFaultReportingNoUi) != 0 &&
        (werFlags & WindowsNative.WerFaultReportingAlwaysShowUi) == 0 &&
        Environment.GetEnvironmentVariable("DOTNET_DISABLE_GUI_ERRORS") == "1";
}
