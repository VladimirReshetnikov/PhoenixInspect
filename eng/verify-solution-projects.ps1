#requires -Version 7.2

<#
.SYNOPSIS
Verifies that every tracked C# project is listed exactly once in PhoenixInspect.sln.

.DESCRIPTION
Compares the Git-tracked *.csproj set with the C# project paths declared by PhoenixInspect.sln. Paths are compared
ordinally after normalizing only directory separators. Rooted, traversing, non-canonical, malformed, duplicate,
untracked, and omitted paths fail with deterministic diagnostics.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'PhoenixInspect.sln'

function Get-OrdinalSortedStrings {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Values)

    [string[]] $copy = @($Values)
    [System.Array]::Sort($copy, [System.StringComparer]::Ordinal)
    $copy
}

function ConvertTo-CanonicalProjectPath {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Source
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        $Path.StartsWith('/', [System.StringComparison]::Ordinal) -or
        $Path.StartsWith('\', [System.StringComparison]::Ordinal) -or
        [regex]::IsMatch($Path, '^[A-Za-z]:', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "$Source contains a rooted or empty project path: '$Path'."
    }

    $normalized = $Path.Replace('\', '/')
    $segments = $normalized.Split('/')
    if ($segments.Count -lt 1 -or
        @($segments | Where-Object { $_ -ceq '' -or $_ -ceq '.' -or $_ -ceq '..' }).Count -ne 0 -or
        -not $normalized.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Source contains a non-canonical C# project path: '$Path'."
    }

    $normalized
}

if (-not [System.IO.File]::Exists($solutionPath)) {
    throw "Solution file is missing: '$solutionPath'."
}

function Read-GitTrackedPathStream {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-C', $repositoryRoot, '-c', 'core.quotepath=false', 'ls-files', '-z')) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $output = [System.IO.MemoryStream]::new()
    try {
        if (-not $process.Start()) {
            throw 'Could not start git ls-files.'
        }
        $errorTask = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($output)
        $process.WaitForExit()
        $errorText = $errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "git ls-files failed with exit code '$($process.ExitCode)': $errorText"
        }

        [byte[]] $bytes = $output.ToArray()
        try {
            [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        }
        catch {
            throw "git ls-files returned a path stream that is not valid UTF-8. $($_.Exception.Message)"
        }
    }
    finally {
        $output.Dispose()
        $process.Dispose()
    }
}

$trackedOutputText = Read-GitTrackedPathStream
if ([string]::IsNullOrEmpty($trackedOutputText) -or $trackedOutputText[$trackedOutputText.Length - 1] -ne [char]0) {
    throw 'git ls-files -z returned an empty or non-NUL-terminated path stream.'
}
$trackedOutput = @($trackedOutputText.Split(
        [char[]]@([char]0),
        [System.StringSplitOptions]::RemoveEmptyEntries))

$trackedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($path in $trackedOutput) {
    if (-not ([string]$path).EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }
    $canonical = ConvertTo-CanonicalProjectPath ([string]$path) 'Git'
    if (-not $trackedSet.Add($canonical)) {
        throw "Git returned a duplicate tracked project path: '$canonical'."
    }
}
if ($trackedSet.Count -eq 0) {
    throw 'Git returned no tracked C# project paths.'
}

$projectLinePattern = [regex]::new(
    '^Project\("\{(?<type>[0-9A-Fa-f-]{36})\}"\)\s*=\s*"(?:[^"]|"")*",\s*"(?<path>(?:[^"]|"")*)",\s*"\{(?<guid>[0-9A-Fa-f-]{36})\}"\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$solutionFolderProjectType = '2150E333-8FDC-42A3-9474-1A3956D46DE8'
$solutionCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)
$solutionProjectGuids = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
$projectGuidCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
$solutionLines = [System.IO.File]::ReadAllLines($solutionPath)
$lineNumber = 0
foreach ($line in $solutionLines) {
    $lineNumber++
    if (-not $line.StartsWith('Project(', [System.StringComparison]::Ordinal)) {
        continue
    }

    $match = $projectLinePattern.Match($line)
    if (-not $match.Success) {
        if ($line.Contains('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Malformed C# project entry in PhoenixInspect.sln at line ${lineNumber}: '$line'."
        }
        continue
    }

    $declaredPath = $match.Groups['path'].Value.Replace('""', '"')
    $projectGuid = $match.Groups['guid'].Value.ToUpperInvariant()
    if ($projectGuidCounts.ContainsKey($projectGuid)) {
        $projectGuidCounts[$projectGuid]++
    }
    else {
        $projectGuidCounts.Add($projectGuid, 1)
    }
    if (-not $declaredPath.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }
    if ($match.Groups['type'].Value.Equals($solutionFolderProjectType, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "PhoenixInspect.sln declares a C# project path as a solution folder at line ${lineNumber}: '$declaredPath'."
    }
    $canonical = ConvertTo-CanonicalProjectPath $declaredPath 'PhoenixInspect.sln'
    if ($solutionCounts.ContainsKey($canonical)) {
        $solutionCounts[$canonical]++
    }
    else {
        $solutionCounts.Add($canonical, 1)
        $solutionProjectGuids.Add($canonical, $projectGuid)
    }
}

$globalSectionStartPattern = [regex]::new(
    '^\s*GlobalSection\((?<name>[^)]+)\)\s*=\s*(?<phase>preSolution|postSolution)\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$globalSectionMentionPattern = [regex]::new(
    'GlobalSection\(',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$endGlobalSectionPattern = [regex]::new(
    '^\s*EndGlobalSection\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$configurationPattern = [regex]::new(
    '^\s*\{(?<guid>[0-9A-Fa-f-]{36})\}\.Release\|Any CPU\.(?<kind>ActiveCfg|Build\.0)\s*=\s*Release\|Any CPU\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$configurationMentionPattern = [regex]::new(
    '\.Release\|Any CPU\.(?:ActiveCfg|Build\.0)',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$globalStartPattern = [regex]::new(
    '^\s*Global\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$globalEndPattern = [regex]::new(
    '^\s*EndGlobal\s*$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$releaseActiveCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
$releaseBuildCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
$configurationSectionCount = 0
$insideConfigurationSection = $false
$openGlobalSectionName = $null
$globalBlockCount = 0
$insideGlobalBlock = $false
$lineNumber = 0
foreach ($line in $solutionLines) {
    $lineNumber++
    if ($globalStartPattern.IsMatch($line)) {
        if ($insideGlobalBlock) {
            throw "PhoenixInspect.sln starts a nested Global block at line $lineNumber."
        }
        $globalBlockCount++
        $insideGlobalBlock = $true
        continue
    }
    if ($globalEndPattern.IsMatch($line)) {
        if (-not $insideGlobalBlock -or $null -ne $openGlobalSectionName) {
            throw "PhoenixInspect.sln has a misplaced EndGlobal at line $lineNumber."
        }
        $insideGlobalBlock = $false
        continue
    }
    $sectionMatch = $globalSectionStartPattern.Match($line)
    if ($sectionMatch.Success) {
        if (-not $insideGlobalBlock -or $null -ne $openGlobalSectionName) {
            throw "PhoenixInspect.sln starts a misplaced or nested GlobalSection at line $lineNumber."
        }
        $openGlobalSectionName = $sectionMatch.Groups['name'].Value
        if ($openGlobalSectionName -ceq 'ProjectConfigurationPlatforms') {
            if ($sectionMatch.Groups['phase'].Value -cne 'postSolution') {
                throw "PhoenixInspect.sln declares ProjectConfigurationPlatforms with the wrong phase at line $lineNumber."
            }
            $configurationSectionCount++
            $insideConfigurationSection = $true
        }
        continue
    }
    if ($globalSectionMentionPattern.IsMatch($line)) {
        throw "PhoenixInspect.sln has a malformed GlobalSection header at line ${lineNumber}: '$line'."
    }
    if ($endGlobalSectionPattern.IsMatch($line)) {
        if (-not $insideGlobalBlock -or $null -eq $openGlobalSectionName) {
            throw "PhoenixInspect.sln has a misplaced EndGlobalSection at line $lineNumber."
        }
        $insideConfigurationSection = $false
        $openGlobalSectionName = $null
        continue
    }

    $match = $configurationPattern.Match($line)
    if (-not $insideConfigurationSection) {
        if ($match.Success -or $configurationMentionPattern.IsMatch($line)) {
            throw "PhoenixInspect.sln has a Release project mapping outside ProjectConfigurationPlatforms at line ${lineNumber}: '$line'."
        }
        continue
    }
    if (-not $match.Success) {
        if ($configurationMentionPattern.IsMatch($line)) {
            throw "PhoenixInspect.sln has a malformed Release project mapping at line ${lineNumber}: '$line'."
        }
        continue
    }

    $projectGuid = $match.Groups['guid'].Value.ToUpperInvariant()
    $target = if ($match.Groups['kind'].Value -ceq 'ActiveCfg') { $releaseActiveCounts } else { $releaseBuildCounts }
    if ($target.ContainsKey($projectGuid)) {
        $target[$projectGuid]++
    }
    else {
        $target.Add($projectGuid, 1)
    }
}
if ($insideConfigurationSection -or $null -ne $openGlobalSectionName) {
    throw "PhoenixInspect.sln has an unterminated GlobalSection '$openGlobalSectionName'."
}
if ($insideGlobalBlock -or $globalBlockCount -ne 1) {
    throw "PhoenixInspect.sln must contain exactly one terminated Global block; observed $globalBlockCount."
}
if ($configurationSectionCount -ne 1) {
    throw "PhoenixInspect.sln must contain exactly one ProjectConfigurationPlatforms postSolution section; observed $configurationSectionCount."
}

[string[]] $missingFromSolution = @(Get-OrdinalSortedStrings @(
    $trackedSet | Where-Object { -not $solutionCounts.ContainsKey($_) }))
[string[]] $notTracked = @(Get-OrdinalSortedStrings @(
    $solutionCounts.Keys | Where-Object { -not $trackedSet.Contains($_) }))
[string[]] $duplicates = @(Get-OrdinalSortedStrings @(
    $solutionCounts.GetEnumerator() |
        Where-Object { $_.Value -ne 1 } |
        ForEach-Object { "$($_.Key) ($($_.Value) entries)" }))
[string[]] $duplicateGuids = @(Get-OrdinalSortedStrings @(
    $projectGuidCounts.GetEnumerator() |
        Where-Object { $_.Value -ne 1 } |
        ForEach-Object { "$($_.Key) ($($_.Value) project entries)" }))
[string[]] $missingReleaseActive = @(Get-OrdinalSortedStrings @(
    $solutionProjectGuids.GetEnumerator() |
        Where-Object {
            -not $releaseActiveCounts.ContainsKey($_.Value) -or $releaseActiveCounts[$_.Value] -ne 1
        } |
        ForEach-Object { "$($_.Key) {$($_.Value)}" }))
[string[]] $missingReleaseBuild = @(Get-OrdinalSortedStrings @(
    $solutionProjectGuids.GetEnumerator() |
        Where-Object {
            -not $releaseBuildCounts.ContainsKey($_.Value) -or $releaseBuildCounts[$_.Value] -ne 1
        } |
        ForEach-Object { "$($_.Key) {$($_.Value)}" }))

if ($missingFromSolution.Count -ne 0 -or
    $notTracked.Count -ne 0 -or
    $duplicates.Count -ne 0 -or
    $duplicateGuids.Count -ne 0 -or
    $missingReleaseActive.Count -ne 0 -or
    $missingReleaseBuild.Count -ne 0) {
    $diagnostic = [System.Collections.Generic.List[string]]::new()
    $diagnostic.Add('PhoenixInspect.sln project membership does not match tracked C# projects.')
    foreach ($section in @(
            [pscustomobject]@{ Title = 'Tracked but absent from solution'; Values = $missingFromSolution },
            [pscustomobject]@{ Title = 'In solution but not tracked'; Values = $notTracked },
            [pscustomobject]@{ Title = 'Duplicate solution entries'; Values = $duplicates },
            [pscustomobject]@{ Title = 'Duplicate C# project GUIDs'; Values = $duplicateGuids },
            [pscustomobject]@{ Title = 'Missing or duplicate Release ActiveCfg mappings'; Values = $missingReleaseActive },
            [pscustomobject]@{ Title = 'Missing or duplicate Release Build.0 mappings'; Values = $missingReleaseBuild })) {
        $diagnostic.Add("$($section.Title):")
        if ($section.Values.Count -eq 0) {
            $diagnostic.Add('  (none)')
        }
        else {
            foreach ($value in $section.Values) {
                $diagnostic.Add("  $value")
            }
        }
    }
    throw ($diagnostic -join "`n")
}

Write-Output "Verified $($trackedSet.Count) tracked C# projects are listed exactly once and enabled for Release|Any CPU in PhoenixInspect.sln."
