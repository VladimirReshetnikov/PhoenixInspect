using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Interpreter.Host.ExternalWorker;

internal sealed class NoDialogProcessLaunchScope : IDisposable
{
    internal const uint RequiredErrorMode =
        WindowsNative.SemFailCriticalErrors |
        WindowsNative.SemNoGpFaultErrorBox |
        WindowsNative.SemNoOpenFileErrorBox;

    private static readonly object ProcessPolicyGate = new();
    private uint _oldProcessErrorMode;
    private uint _appliedProcessErrorMode;
    private uint _oldThreadErrorMode;
    private uint _appliedThreadErrorMode;
    private uint _oldWerFlags;
    private uint _appliedWerFlags;
    private bool _processModeChanged;
    private bool _threadModeChanged;
    private bool _werFlagsChanged;
    private bool _gateHeld;

    private NoDialogProcessLaunchScope()
    {
    }

    internal static NoDialogProcessLaunchScope Enter()
    {
        var scope = new NoDialogProcessLaunchScope();
        Monitor.Enter(ProcessPolicyGate);
        scope._gateHeld = true;
        try
        {
            scope._oldProcessErrorMode = WindowsNative.GetErrorMode();
            scope._appliedProcessErrorMode = scope._oldProcessErrorMode | RequiredErrorMode;
            _ = WindowsNative.SetErrorMode(scope._appliedProcessErrorMode);
            scope._processModeChanged = true;

            var threadMode = WindowsNative.GetThreadErrorMode();
            scope._appliedThreadErrorMode = threadMode | RequiredErrorMode;
            if (!WindowsNative.SetThreadErrorMode(
                    scope._appliedThreadErrorMode,
                    out scope._oldThreadErrorMode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            scope._threadModeChanged = true;
            var getWerFlagsResult = WindowsNative.WerGetFlags(
                WindowsNative.GetCurrentProcess(),
                out scope._oldWerFlags);
            if (getWerFlagsResult < 0)
            {
                Marshal.ThrowExceptionForHR(getWerFlagsResult);
            }

            scope._appliedWerFlags =
                (scope._oldWerFlags & ~WindowsNative.WerFaultReportingAlwaysShowUi) |
                WindowsNative.WerFaultReportingNoUi;
            var setWerFlagsResult = WindowsNative.WerSetFlags(scope._appliedWerFlags);
            if (setWerFlagsResult < 0)
            {
                Marshal.ThrowExceptionForHR(setWerFlagsResult);
            }

            scope._werFlagsChanged = true;
            if (!IsCurrentPolicyApplied())
            {
                throw new InvalidOperationException("Windows did not retain the required no-dialog launch policy.");
            }

            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_werFlagsChanged)
        {
            if (WindowsNative.WerGetFlags(WindowsNative.GetCurrentProcess(), out var currentWerFlags) >= 0 &&
                currentWerFlags == _appliedWerFlags)
            {
                _ = WindowsNative.WerSetFlags(_oldWerFlags);
            }

            _werFlagsChanged = false;
        }

        if (_threadModeChanged)
        {
            if (WindowsNative.GetThreadErrorMode() == _appliedThreadErrorMode)
            {
                _ = WindowsNative.SetThreadErrorMode(_oldThreadErrorMode, out _);
            }

            _threadModeChanged = false;
        }

        if (_processModeChanged)
        {
            if (WindowsNative.GetErrorMode() == _appliedProcessErrorMode)
            {
                _ = WindowsNative.SetErrorMode(_oldProcessErrorMode);
            }

            _processModeChanged = false;
        }

        if (_gateHeld)
        {
            _gateHeld = false;
            Monitor.Exit(ProcessPolicyGate);
        }
    }

    internal static bool IsCurrentPolicyApplied()
    {
        if ((WindowsNative.GetErrorMode() & RequiredErrorMode) != RequiredErrorMode ||
            (WindowsNative.GetThreadErrorMode() & RequiredErrorMode) != RequiredErrorMode ||
            WindowsNative.WerGetFlags(WindowsNative.GetCurrentProcess(), out var werFlags) < 0)
        {
            return false;
        }

        return (werFlags & WindowsNative.WerFaultReportingNoUi) != 0 &&
               (werFlags & WindowsNative.WerFaultReportingAlwaysShowUi) == 0;
    }
}
