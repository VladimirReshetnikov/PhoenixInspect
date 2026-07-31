<#
.SYNOPSIS
    Runs the PhoenixInspect preview demo end to end.

.DESCRIPTION
    Builds the console host and the demo target, starts the target and waits for it to reach its stalled state,
    captures a full dump of it, then replays a scripted inspection session against that dump and prints the
    transcript.

    Nothing in the session is special-cased for the demo. The same commands answer the same way against any dump of
    any .NET application, within the surface the product currently supports.

.PARAMETER OutputDirectory
    Where the dump and transcript are written. Defaults to a directory under the system temporary path, because a
    full dump is large and is not repository content.

.PARAMETER KeepDump
    Keep the captured dump after the demo instead of deleting it. The dump is what makes the session reproducible,
    so keep it when investigating a difference between two runs.

.PARAMETER SessionScript
    The command script to replay. Defaults to eng/demo-session.pi.

.PARAMETER SkipBuild
    Reuse the existing Release output instead of building first.

.EXAMPLE
    ./eng/Invoke-PreviewDemo.ps1

.EXAMPLE
    ./eng/Invoke-PreviewDemo.ps1 -OutputDirectory ./artifacts/demo -KeepDump
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $KeepDump,
    [string] $SessionScript,
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$configuration = 'Release'
$framework = 'net10.0'

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'phoenixinspect-preview-demo'
}

if (-not $SessionScript) {
    $SessionScript = Join-Path $PSScriptRoot 'demo-session.pi'
}

if (-not (Test-Path -LiteralPath $SessionScript)) {
    throw "The session script '$SessionScript' does not exist."
}

$consoleHostProject = Join-Path $repositoryRoot 'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj'
$demoTargetProject = Join-Path $repositoryRoot 'samples/Contoso.OrderService/Contoso.OrderService.csproj'
$consoleHost = Join-Path $repositoryRoot "src/PhoenixInspect.Cli/bin/$configuration/$framework/phoenixinspect.exe"
$demoTarget = Join-Path $repositoryRoot "samples/Contoso.OrderService/bin/$configuration/$framework/Contoso.OrderService.exe"

if (-not $SkipBuild) {
    Write-Host 'Building the console host and the demo target…' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'Invoke-HeadlessProcess.ps1') `
        'dotnet' 'build' $consoleHostProject '--configuration' $configuration '--verbosity' 'quiet' '--nologo'
    if ($LASTEXITCODE -ne 0) { throw "Building $consoleHostProject failed with exit code $LASTEXITCODE." }

    & (Join-Path $PSScriptRoot 'Invoke-HeadlessProcess.ps1') `
        'dotnet' 'build' $demoTargetProject '--configuration' $configuration '--verbosity' 'quiet' '--nologo'
    if ($LASTEXITCODE -ne 0) { throw "Building $demoTargetProject failed with exit code $LASTEXITCODE." }
}

foreach ($required in @($consoleHost, $demoTarget)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "'$required' was not found. Run without -SkipBuild, or build the solution first."
    }
}

$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$dumpPath = Join-Path $OutputDirectory 'Contoso.OrderService.dmp'
$transcriptPath = Join-Path $OutputDirectory 'demo-session.txt'
$readyPath = Join-Path $OutputDirectory 'demo-target.out'

if (Test-Path -LiteralPath $readyPath) { Remove-Item -LiteralPath $readyPath -Force }

Write-Host 'Starting the demo target and waiting for it to stall…' -ForegroundColor Cyan
$target = Start-Process -FilePath $demoTarget -PassThru -NoNewWindow -RedirectStandardOutput $readyPath

try {
    $readyDeadline = [datetime]::UtcNow.AddSeconds(30)
    $ready = $false
    while ([datetime]::UtcNow -lt $readyDeadline) {
        if ($target.HasExited) {
            throw "The demo target exited with code $($target.ExitCode) before it reached its stalled state."
        }

        if ((Test-Path -LiteralPath $readyPath) -and
            ((Get-Content -LiteralPath $readyPath -Raw -ErrorAction SilentlyContinue) -match 'READY')) {
            $ready = $true
            break
        }

        Start-Sleep -Milliseconds 200
    }

    if (-not $ready) {
        throw 'The demo target did not reach its stalled state within 30 seconds.'
    }

    Write-Host "Capturing a full dump of process $($target.Id)…" -ForegroundColor Cyan
    & $consoleHost 'capture' '--pid' $target.Id '--output' $dumpPath
    if ($LASTEXITCODE -ne 0) { throw "Capturing the dump failed with exit code $LASTEXITCODE." }
}
finally {
    if (-not $target.HasExited) {
        Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Host 'Replaying the inspection session against the dump…' -ForegroundColor Cyan
Write-Host ''

# The transcript is written without styling so it stays readable in a file, and mirrored to the console so the run is
# watchable. The host's exit code decides the demo's exit code.
& $consoleHost $dumpPath '--script' $SessionScript '--no-color' 2>&1 |
    Tee-Object -FilePath $transcriptPath |
    Write-Host
$sessionExitCode = $LASTEXITCODE

Write-Host ''
Write-Host "Transcript: $transcriptPath" -ForegroundColor Cyan
if ($KeepDump) {
    Write-Host "Dump:       $dumpPath" -ForegroundColor Cyan
}
else {
    Remove-Item -LiteralPath $dumpPath -Force -ErrorAction SilentlyContinue
    Write-Host 'Dump:       removed (pass -KeepDump to retain it)' -ForegroundColor Cyan
}

if ($sessionExitCode -ne 0) {
    throw "The inspection session ended with exit code $sessionExitCode."
}

Write-Host ''
Write-Host 'Preview demo completed.' -ForegroundColor Green
