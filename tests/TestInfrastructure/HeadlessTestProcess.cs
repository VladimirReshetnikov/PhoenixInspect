using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PhoenixInspect.Tests.Infrastructure;

internal static class HeadlessTestProcess
{
    internal const uint SuppressedErrorModeMask = 0x0000_8003;
    internal const uint WerAlwaysShowUi = 0x0000_0010;
    internal const uint WerNoUi = 0x0000_0020;
    private static readonly object PolicyGate = new();

#pragma warning disable CA2255 // Test assemblies deliberately configure failure handling before any test runs.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => EnsureCurrentPolicy();

    internal static void EnsureCurrentPolicy()
    {
        lock (PolicyGate)
        {
            // Native/test-host code can replace process-global error-mode bits, so each configured lifecycle boundary
            // reapplies and attests the complete policy.
            Environment.SetEnvironmentVariable("DOTNET_DISABLE_GUI_ERRORS", "1");
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var previousErrorMode = GetErrorMode();
            SetErrorMode(previousErrorMode | SuppressedErrorModeMask);

            var effectiveErrorMode = GetErrorMode();
            if ((effectiveErrorMode & SuppressedErrorModeMask) != SuppressedErrorModeMask)
            {
                throw new InvalidOperationException(
                    $"Could not enable headless Windows process error handling. Observed error mode 0x{effectiveErrorMode:X8}.");
            }

            var previousWerFlags = GetWerFlags();
            var requestedWerFlags = (previousWerFlags & ~WerAlwaysShowUi) | WerNoUi;
            ThrowIfFailed(WerSetFlags(requestedWerFlags), nameof(WerSetFlags));

            var effectiveWerFlags = GetWerFlags();
            if ((effectiveWerFlags & WerNoUi) == 0 || (effectiveWerFlags & WerAlwaysShowUi) != 0)
            {
                throw new InvalidOperationException(
                    $"Could not disable Windows Error Reporting UI. Observed WER flags 0x{effectiveWerFlags:X8}.");
            }
        }
    }

    internal static uint GetCurrentErrorMode() => GetErrorMode();

    internal static uint GetCurrentWerFlags() => GetWerFlags();

    private static uint GetWerFlags()
    {
        ThrowIfFailed(WerGetFlags(GetCurrentProcess(), out var flags), nameof(WerGetFlags));
        return flags;
    }

    private static void ThrowIfFailed(int hresult, string operation)
    {
        if (hresult != 0)
        {
            throw new InvalidOperationException(
                $"{operation} failed with HRESULT 0x{unchecked((uint)hresult):X8}.");
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint SetErrorMode(uint mode);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int WerGetFlags(nint process, out uint flags);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int WerSetFlags(uint flags);
}
