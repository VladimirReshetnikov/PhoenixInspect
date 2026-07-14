[CmdletBinding()]
param(
    [string] $WorkflowDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) '.github/workflows')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workflowFiles = @(
    Get-ChildItem -LiteralPath $WorkflowDirectory -File |
        Where-Object { $_.Extension -in '.yml', '.yaml' } |
        Sort-Object FullName
)

if ($workflowFiles.Count -eq 0) {
    throw "No workflow files were found under '$WorkflowDirectory'."
}

$violations = [System.Collections.Generic.List[string]]::new()
$managedInvocationPattern = [regex]::new(
    '\bdotnet\s+(?:restore|build|test)\b',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$headlessPrefixPattern = [regex]::new(
    '(?:^|\s)\./eng/Invoke-HeadlessProcess\.ps1\s+$',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$headlessEnvironmentPattern = [regex]::new(
    '(?m)^\s*DOTNET_DISABLE_GUI_ERRORS:\s*[''"]?1[''"]?\s*(?:#.*)?$')

foreach ($workflowFile in $workflowFiles) {
    $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $workflowFile.FullName) -replace '\\', '/'
    $lines = @(Get-Content -LiteralPath $workflowFile.FullName)
    $containsManagedInvocation = $false

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        if ($line -match '^\s*#') {
            continue
        }

        foreach ($managedInvocation in $managedInvocationPattern.Matches($line)) {
            $containsManagedInvocation = $true
            $prefix = $line.Substring(0, $managedInvocation.Index)
            if (-not $headlessPrefixPattern.IsMatch($prefix)) {
                $violations.Add(
                    "${relativePath}:$($lineIndex + 1): managed restore/build/test must be invoked " +
                    "through ./eng/Invoke-HeadlessProcess.ps1")
            }
        }
    }

    if ($containsManagedInvocation) {
        $workflowText = [System.IO.File]::ReadAllText($workflowFile.FullName)
        if (-not $headlessEnvironmentPattern.IsMatch($workflowText)) {
            $violations.Add(
                "${relativePath}: workflow contains managed restore/build/test but does not set " +
                'DOTNET_DISABLE_GUI_ERRORS: 1')
        }
    }
}

if ($violations.Count -gt 0) {
    throw "Headless workflow verification failed:`n - $($violations -join "`n - ")"
}

Write-Output "Verified $($workflowFiles.Count) workflow file(s): managed restore/build/test invocations are headless."
