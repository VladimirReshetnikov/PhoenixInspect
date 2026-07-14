Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($args.Count -eq 0) {
    throw 'A headless process executable is required.'
}

$ExecutablePath = [string]$args[0]
$ArgumentList = if ($args.Count -gt 1) { @($args[1..($args.Count - 1)]) } else { @() }

. (Join-Path $PSScriptRoot 'Enable-HeadlessTestMode.ps1')
$null = Enable-HeadlessTestMode

& $ExecutablePath @ArgumentList
$processExitCode = $LASTEXITCODE
if ($null -eq $processExitCode) {
    throw "The headless process '$ExecutablePath' did not report an exit code."
}

exit $processExitCode
