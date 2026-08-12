#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding()]
param(
    [string] $WorkflowPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($WorkflowPath)) {
    $WorkflowPath = Join-Path $repositoryRoot '.github/workflows/ci.yml'
}
elseif (-not [System.IO.Path]::IsPathFullyQualified($WorkflowPath)) {
    $WorkflowPath = Join-Path $repositoryRoot $WorkflowPath
}

$WorkflowPath = [System.IO.Path]::GetFullPath($WorkflowPath)
if (-not [System.IO.File]::Exists($WorkflowPath)) {
    throw "The CI workflow was not found at '$WorkflowPath'."
}

$workflow = [System.IO.File]::ReadAllText($WorkflowPath)
$jobMatches = [regex]::Matches(
    $workflow,
    '(?ms)^  prerelease-artifacts:\r?\n(?<body>.*?)(?=^  [A-Za-z0-9_-]+:\r?\n|\z)')
if ($jobMatches.Count -ne 1) {
    throw "Expected exactly one prerelease-artifacts validation job, but found $($jobMatches.Count)."
}

$jobBody = $jobMatches[0].Groups['body'].Value
$needsMatches = [regex]::Matches($jobBody, '(?m)^    needs:[^\r\n]*\r?$')
if ($needsMatches.Count -ne 1 -or
    $needsMatches[0].Value -notmatch '^    needs: \[documentation, fast\]\r?$') {
    throw 'The prerelease-artifacts job must have exactly needs: [documentation, fast].'
}

[string[]] $forbiddenJobPatterns = @(
    '(?im)^\s*uses:\s*actions/upload-artifact@',
    '(?im)^\s*uses:\s*(?:actions/upload-release-asset|softprops/action-gh-release|ncipollo/release-action|svenstaro/upload-release-action)@',
    '(?im)^\s*(?:run:\s*)?gh\s+release(?:\s|$)',
    '(?im)^\s*(?:run:\s*)?gh\s+api\b[^\r\n]*/releases\b'
)
foreach ($pattern in $forbiddenJobPatterns) {
    if ($jobBody -match $pattern) {
        throw "The prerelease-artifacts job contains a forbidden distribution operation matching '$pattern'."
    }
}

# Until license/notice clearance is recorded, this workflow may reference the local output path only in
# the builder invocation. Any upload requires an intentional change to this guard and its documented gate.
$prereleasePath = 'artifacts/' + 'prerelease'
$pathPattern = '(?i)(?:\./)?' + [regex]::Escape($prereleasePath)
$builderPathPattern = '(?i)(?:\./)?eng/Publish-PrereleaseArtifacts\.ps1'
$builderPattern = '(?m)^\s*run:\s*\./eng/Publish-PrereleaseArtifacts\.ps1 -OutputDirectory \./' +
    [regex]::Escape($prereleasePath) + '\s*$'
$pathMatches = [regex]::Matches($workflow, $pathPattern)
$builderPathMatches = [regex]::Matches($workflow, $builderPathPattern)
$builderMatches = [regex]::Matches($jobBody, $builderPattern)
if ($pathMatches.Count -ne 1 -or
    $builderPathMatches.Count -ne 1 -or
    $builderMatches.Count -ne 1) {
    throw 'The prerelease builder and output paths may appear exactly once, in the local validation builder invocation.'
}

$uploadUses = [regex]::Matches($workflow, '(?im)^\s*uses:\s*actions/upload-artifact@')
$allowedDemoUpload = '(?ms)^      - name: Upload the demo transcript\r?\n        if: always\(\)\r?\n        uses: actions/upload-artifact@[^\r\n]+\r?\n        with:\r?\n          name: preview-demo-transcript\r?\n          path: \./artifacts/preview-demo/demo-session\.txt\r?\n          if-no-files-found: warn\r?$'
$allowedDemoUploads = [regex]::Matches($workflow, $allowedDemoUpload)
if ($uploadUses.Count -ne 1 -or $allowedDemoUploads.Count -ne 1) {
    throw 'Only the explicitly allowlisted preview-demo transcript upload is permitted before release clearance.'
}

[string[]] $forbiddenWorkflowPatterns = @(
    '(?im)^\s*uses:\s*(?:actions/upload-release-asset|softprops/action-gh-release|ncipollo/release-action|svenstaro/upload-release-action)@',
    '(?im)^\s*(?:run:\s*)?gh\s+release(?:\s|$)',
    '(?im)^\s*(?:run:\s*)?gh\s+api\b[^\r\n]*/releases\b'
)
foreach ($pattern in $forbiddenWorkflowPatterns) {
    if ($workflow -match $pattern) {
        throw "The workflow contains a release-capable operation matching '$pattern'."
    }
}

$writePermissionPattern = '(?im)^\s*(?:permissions\s*:\s*(?:[''"]?write-all[''"]?|\{[^\r\n]*\bwrite\b[^\r\n]*\})|[A-Za-z_][A-Za-z0-9_-]*\s*:\s*[''"]?write[''"]?)\s*(?:#.*)?$'
if ($workflow -match $writePermissionPattern) {
    throw "The workflow grants a write permission matching '$writePermissionPattern'."
}

$relativeWorkflowPath = [System.IO.Path]::GetRelativePath($repositoryRoot, $WorkflowPath) -replace '\\', '/'
Write-Output "Verified ${relativeWorkflowPath}: prerelease artifacts remain local and non-distributed."
