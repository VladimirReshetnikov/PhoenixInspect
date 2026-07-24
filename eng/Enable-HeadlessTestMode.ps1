function Enable-HeadlessTestMode {
    [CmdletBinding()]
    param()

    $env:DOTNET_DISABLE_GUI_ERRORS = '1'
    $isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)
    if (-not $isWindowsHost) {
        return [pscustomobject]@{
            IsWindows = $false
            PreviousMode = [uint32]0
            EffectiveMode = [uint32]0
            SuppressedModeMask = [uint32]0
        }
    }

    $nativeMethods = 'PhoenixInspect.Testing.WindowsErrorMode' -as [type]
    if ($null -eq $nativeMethods) {
        Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

namespace PhoenixInspect.Testing
{
    public static class WindowsErrorMode
    {
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern uint GetErrorMode();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern uint SetErrorMode(uint mode);
    }
}
'@
        $nativeMethods = 'PhoenixInspect.Testing.WindowsErrorMode' -as [type]
    }

    # These flags are inherited by children. SEM_NOGPFAULTERRORBOX is essential for failures that occur before
    # a managed or native entry point can configure its own Windows Error Reporting behavior.
    [uint32]$suppressedModeMask = 0x00000001 -bor # SEM_FAILCRITICALERRORS
        0x00000002 -bor                          # SEM_NOGPFAULTERRORBOX
        0x00008000                               # SEM_NOOPENFILEERRORBOX
    [uint32]$previousMode = $nativeMethods::GetErrorMode()
    [uint32]$requestedMode = $previousMode -bor $suppressedModeMask

    if ($requestedMode -ne $previousMode) {
        $null = $nativeMethods::SetErrorMode($requestedMode)
    }

    [uint32]$effectiveMode = $nativeMethods::GetErrorMode()
    if (($effectiveMode -band $suppressedModeMask) -ne $suppressedModeMask) {
        throw ("Failed to enable non-interactive Windows error handling. " +
            "Requested error-mode mask 0x{0:X8}, observed 0x{1:X8}." -f
            $suppressedModeMask, $effectiveMode)
    }

    return [pscustomobject]@{
        IsWindows = $true
        PreviousMode = $previousMode
        EffectiveMode = $effectiveMode
        SuppressedModeMask = $suppressedModeMask
    }
}
