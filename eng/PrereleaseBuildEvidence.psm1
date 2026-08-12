#Requires -Version 7.0
#Requires -PSEdition Core

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:EvidenceSchema = 'phoenixinspect.local-prerelease-build-evidence/v1'
$script:CanonicalRepositoryUrl = 'https://github.com/VladimirReshetnikov/PhoenixInspect'
$script:ExpectedTargetFramework = 'net10.0'
$script:ExpectedConfiguration = 'Release'
$script:ExpectedRuntimeIdentifier = 'win-x64'
$script:ExpectedRuntimeTarget = '.NETCoreApp,Version=v10.0/win-x64'
$script:ExpectedRuntimePackId = 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64'

[string[]] $script:PublishGraphProjects = @(
    'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj'
    'src/PhoenixInspect.Core.Abstractions/PhoenixInspect.Core.Abstractions.csproj'
    'src/PhoenixInspect.Core.Execution/PhoenixInspect.Core.Execution.csproj'
    'src/PhoenixInspect.Desktop/PhoenixInspect.Desktop.csproj'
    'src/PhoenixInspect.Domain.Concrete/PhoenixInspect.Domain.Concrete.csproj'
    'src/PhoenixInspect.Host.Abstractions/PhoenixInspect.Host.Abstractions.csproj'
    'src/PhoenixInspect.Host.Dump.ClrMD/PhoenixInspect.Host.Dump.ClrMD.csproj'
    'src/PhoenixInspect.Inspection/PhoenixInspect.Inspection.csproj'
    'src/PhoenixInspect.Metadata.Abstractions/PhoenixInspect.Metadata.Abstractions.csproj'
    'src/PhoenixInspect.Metadata.SRM/PhoenixInspect.Metadata.SRM.csproj'
    'src/PhoenixInspect.Product.DumpDebugging/PhoenixInspect.Product.DumpDebugging.csproj'
    'src/PhoenixInspect.Product.DumpQuery/PhoenixInspect.Product.DumpQuery.csproj'
)
[System.Array]::Sort($script:PublishGraphProjects, [System.StringComparer]::Ordinal)
[string[]] $script:SourceProjectDirectories = @(
    $script:PublishGraphProjects | ForEach-Object { [System.IO.Path]::GetDirectoryName($_).Replace('\', '/') }
) + 'src/PhoenixInspect.Headless.ReferenceConsumer'
[System.Array]::Sort($script:SourceProjectDirectories, [System.StringComparer]::Ordinal)

function Get-PrereleaseSelectedInputPaths {
    [CmdletBinding()]
    param()

    [string[]] $paths = @(
        'Directory.Build.props'
        'Directory.Packages.props'
        'LICENSE'
        'eng/Generate-ThirdPartyNotices.ps1'
        'eng/PrereleaseBuildEvidence.psm1'
        'eng/Publish-PrereleaseArtifacts.ps1'
        'eng/third-party-notices.policy.json'
        'eng/third-party-notices/licenses/MIT.txt'
        'global.json'
        'src/Directory.Build.props'
        foreach ($project in $script:PublishGraphProjects) {
            $project
            ([System.IO.Path]::GetDirectoryName($project).Replace('\', '/') + '/packages.lock.json')
        }
    )
    [System.Array]::Sort($paths, [System.StringComparer]::Ordinal)
    return $paths
}

function Test-ByteArraysEqual {
    param(
        [Parameter(Mandatory)][byte[]] $First,
        [Parameter(Mandatory)][byte[]] $Second
    )

    if ($First.Length -ne $Second.Length) {
        return $false
    }
    for ($index = 0; $index -lt $First.Length; $index++) {
        if ($First[$index] -ne $Second[$index]) {
            return $false
        }
    }
    return $true
}

function Assert-CanonicalRepositoryRelativePath {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Context
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        $Path.Contains('\') -or
        $Path.StartsWith('/') -or
        [System.IO.Path]::IsPathRooted($Path)) {
        throw "$Context path '$Path' is not a canonical forward-slash repository-relative path."
    }
    foreach ($segment in $Path.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -in @('.', '..') -or
            $segment.EndsWith('.') -or $segment.EndsWith(' ') -or $segment.Contains(':')) {
            throw "$Context path '$Path' contains a non-canonical segment '$segment'."
        }
    }
}

function Resolve-RepositoryFile {
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][string] $Context
    )

    Assert-CanonicalRepositoryRelativePath -Path $RelativePath -Context $Context
    $root = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($RepositoryRoot))
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $root $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
    if (-not $candidate.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Context path '$RelativePath' escapes '$root'."
    }
    $roundTrip = [System.IO.Path]::GetRelativePath($root, $candidate).Replace('\', '/')
    if (-not [string]::Equals($roundTrip, $RelativePath, [System.StringComparison]::Ordinal)) {
        throw "$Context path '$RelativePath' is not in canonical filesystem form (round-trip '$roundTrip')."
    }
    return $candidate
}

function Invoke-RawProcess {
    param(
        [Parameter(Mandatory)][string] $FileName,
        [Parameter(Mandatory)][string[]] $ArgumentList,
        [Parameter(Mandatory)][string] $WorkingDirectory,
        [Parameter(Mandatory)][string] $Description,
        [ValidateRange(1, 300)][int] $TimeoutSeconds = 30
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Could not start $Description."
    }
    try {
        $output = [System.IO.MemoryStream]::new()
        try {
            # Drain both redirected pipes concurrently. Reading either one to EOF first can
            # deadlock when a child fills the other OS pipe buffer.
            $outputTask = $process.StandardOutput.BaseStream.CopyToAsync($output)
            $errorTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                try {
                    $process.Kill($true)
                }
                catch {
                    # Preserve the timeout as the primary failure; the final wait still
                    # determines whether the redirected streams can be drained safely.
                }
                if (-not $process.WaitForExit(5000)) {
                    throw "$Description timed out after $TimeoutSeconds seconds and did not terminate within the five-second kill grace period."
                }
                $null = $outputTask.GetAwaiter().GetResult()
                $errorText = $errorTask.GetAwaiter().GetResult()
                throw "$Description timed out after $TimeoutSeconds seconds. $($errorText.Trim())"
            }
            # WaitForExit() without a timeout completes native stream bookkeeping after the
            # bounded wait has observed process termination.
            $process.WaitForExit()
            $null = $outputTask.GetAwaiter().GetResult()
            $errorText = $errorTask.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0) {
                throw "$Description failed with exit code $($process.ExitCode): $($errorText.Trim())"
            }
            return ,$output.ToArray()
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $process.Dispose()
    }
}

function ConvertFrom-NulTerminatedUtf8 {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]] $Bytes,
        [Parameter(Mandatory)][string] $Context
    )

    if ($Bytes.Length -eq 0) {
        return @()
    }
    if ($Bytes[$Bytes.Length - 1] -ne 0) {
        throw "$Context is not NUL-terminated."
    }
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $records = [System.Collections.Generic.List[string]]::new()
    $start = 0
    for ($index = 0; $index -lt $Bytes.Length; $index++) {
        if ($Bytes[$index] -ne 0) {
            continue
        }
        try {
            $records.Add($strictUtf8.GetString($Bytes, $start, $index - $start))
        }
        catch {
            throw "$Context contains a path that is not valid UTF-8."
        }
        $start = $index + 1
    }
    return $records.ToArray()
}

function Get-GitBlobSha1 {
    param([Parameter(Mandatory)][string] $Path)

    $file = Get-Item -LiteralPath $Path -Force
    if (-not $file.PSIsContainer -and ($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) {
        $sha1 = [System.Security.Cryptography.SHA1]::Create()
        try {
            $header = [System.Text.Encoding]::ASCII.GetBytes("blob $($file.Length)`0")
            $null = $sha1.TransformBlock($header, 0, $header.Length, $null, 0)
            $stream = $file.OpenRead()
            try {
                $buffer = [byte[]]::new(1MB)
                while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $null = $sha1.TransformBlock($buffer, 0, $read, $null, 0)
                }
                $null = $sha1.TransformFinalBlock([byte[]]::new(0), 0, 0)
                return ([System.BitConverter]::ToString($sha1.Hash)).Replace('-', '').ToLowerInvariant()
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $sha1.Dispose()
        }
    }
    throw "Tracked source '$Path' is missing, a directory, or a reparse point."
}

function Get-GitScalar {
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Description
    )

    $bytes = Invoke-RawProcess -FileName 'git' -ArgumentList $Arguments -WorkingDirectory $RepositoryRoot -Description $Description
    $text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes).Trim()
    if ($text.Contains("`n") -or [string]::IsNullOrWhiteSpace($text)) {
        throw "$Description did not produce exactly one value."
    }
    return $text
}

function Get-ProcessScalar {
    param(
        [Parameter(Mandatory)][string] $FileName,
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Description
    )

    $bytes = Invoke-RawProcess $FileName $Arguments $RepositoryRoot $Description
    $text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes).Trim()
    if ($text.Contains("`n") -or [string]::IsNullOrWhiteSpace($text)) {
        throw "$Description did not produce exactly one value."
    }
    return $text
}

function Get-PrereleaseSourceState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RepositoryRoot)

    $root = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($RepositoryRoot))
    $reported = Get-GitScalar $root @('rev-parse', '--show-toplevel') 'Resolving the Git repository root'
    if ($reported -ne $root.Replace('\', '/')) {
        # Git prints forward slashes on Windows; normalize both sides without accepting a parent checkout.
        if (-not [string]::Equals([System.IO.Path]::GetFullPath($reported), $root,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Publisher root '$root' is not the exact Git repository root '$reported'."
        }
    }
    $objectFormat = Get-GitScalar $root @('rev-parse', '--show-object-format') 'Resolving the Git object format'
    if ($objectFormat -cne 'sha1') {
        throw "Build-evidence schema v1 requires Git object format sha1; found '$objectFormat'."
    }

    $initialCommit = Get-GitScalar $root @('rev-parse', '--verify', 'HEAD^{commit}') 'Resolving Git HEAD'
    $initialTree = Get-GitScalar $root @('rev-parse', '--verify', 'HEAD^{tree}') 'Resolving the Git tree'
    if ($initialCommit -cnotmatch '^[0-9a-f]{40}$' -or $initialTree -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Git HEAD or tree identity is not a canonical SHA-1 object name.'
    }

    $statusBytes = Invoke-RawProcess 'git' @('status', '--porcelain=v1', '-z', '--untracked-files=all') $root 'Reading Git status'
    if ($statusBytes.Length -ne 0) {
        throw 'The prerelease publisher requires zero tracked or untracked Git status entries.'
    }

    foreach ($record in ConvertFrom-NulTerminatedUtf8 `
            (Invoke-RawProcess 'git' @('ls-files', '-v', '-z') $root 'Reading Git index flags') `
            'Git index flags') {
        if ($record.Length -lt 3 -or $record[0] -cne 'H' -or $record[1] -ne ' ') {
            $path = if ($record.Length -gt 2) { $record.Substring(2) } else { $record }
            throw "Tracked path '$path' has a non-normal skip-worktree, assume-unchanged, or index flag."
        }
    }

    foreach ($ignoredPath in ConvertFrom-NulTerminatedUtf8 `
            (Invoke-RawProcess 'git' @('ls-files', '--others', '--ignored', '--exclude-standard', '-z', '--', 'src') $root 'Reading ignored source files') `
            'Ignored source paths') {
        Assert-CanonicalRepositoryRelativePath -Path $ignoredPath -Context 'Ignored source'
        $allowed = $false
        foreach ($projectDirectory in $script:SourceProjectDirectories) {
            if ($ignoredPath.StartsWith("$projectDirectory/bin/", [System.StringComparison]::Ordinal) -or
                $ignoredPath.StartsWith("$projectDirectory/obj/", [System.StringComparison]::Ordinal)) {
                $allowed = $true
                break
            }
        }
        if (-not $allowed) {
            throw "Ignored source file '$ignoredPath' is outside the exact participating-project bin/obj allowance."
        }
    }

    $treeRecords = ConvertFrom-NulTerminatedUtf8 `
        (Invoke-RawProcess 'git' @('ls-tree', '-r', '-z', '--full-tree', 'HEAD') $root 'Reading the raw Git tree') `
        'Git tree'
    if ($treeRecords.Count -eq 0) {
        throw 'The Git HEAD tree is empty.'
    }
    foreach ($record in $treeRecords) {
        if ($record -cnotmatch '^(?<mode>[0-9]{6}) (?<type>[a-z]+) (?<hash>[0-9a-f]{40})\t(?<path>.+)$') {
            throw "Malformed Git tree record '$record'."
        }
        if ($Matches.type -cne 'blob' -or $Matches.mode -notin @('100644', '100755')) {
            throw "Tracked tree path '$($Matches.path)' is not a regular blob (mode $($Matches.mode), type $($Matches.type))."
        }
        $relativePath = $Matches.path
        Assert-CanonicalRepositoryRelativePath -Path $relativePath -Context 'Tracked source'
        $workingPath = Resolve-RepositoryFile $root $relativePath 'Tracked source'
        if (-not (Test-Path -LiteralPath $workingPath -PathType Leaf)) {
            throw "Tracked source '$relativePath' is missing from the working tree."
        }
        $rawHash = Get-GitBlobSha1 $workingPath
        if ($rawHash -cne $Matches.hash) {
            throw "Raw working bytes for '$relativePath' do not reproduce the HEAD Git blob '$($Matches.hash)'."
        }
    }

    $finalCommit = Get-GitScalar $root @('rev-parse', '--verify', 'HEAD^{commit}') 'Rechecking Git HEAD'
    $finalTree = Get-GitScalar $root @('rev-parse', '--verify', 'HEAD^{tree}') 'Rechecking the Git tree'
    $finalStatus = Invoke-RawProcess 'git' @('status', '--porcelain=v1', '-z', '--untracked-files=all') $root 'Rechecking Git status'
    if ($finalStatus.Length -ne 0 -or $finalCommit -cne $initialCommit -or $finalTree -cne $initialTree) {
        throw 'Git source state changed while its raw tracked bytes were being verified.'
    }

    return [pscustomobject]@{
        ObjectFormat = $objectFormat
        Commit = $initialCommit
        Tree = $initialTree
    }
}

function Assert-PrereleaseSourceStateEqual {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Initial,
        [Parameter(Mandatory)] $Current
    )

    foreach ($property in @('ObjectFormat', 'Commit', 'Tree')) {
        if ([string] $Initial.$property -cne [string] $Current.$property) {
            throw "Git source $property changed from '$($Initial.$property)' to '$($Current.$property)'."
        }
    }
}

function Get-PrereleaseRepositoryContract {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RepositoryRoot)

    $globalPath = Resolve-RepositoryFile $RepositoryRoot 'global.json' 'SDK policy'
    $global = [System.IO.File]::ReadAllText($globalPath) | ConvertFrom-Json -AsHashtable -Depth 10
    if (@($global.Keys).Count -ne 1 -or -not $global.Contains('sdk')) {
        throw 'global.json must contain exactly the sdk object for prerelease evidence schema v1.'
    }
    $sdk = $global.sdk
    if (@($sdk.Keys).Count -ne 3 -or
        [string] $sdk.version -cnotmatch '^10\.0\.4[0-9]{2}$' -or
        [string] $sdk.rollForward -cne 'latestPatch' -or
        $sdk.allowPrerelease -isnot [bool] -or $sdk.allowPrerelease) {
        throw 'global.json must pin a stable 10.0.4xx minimum with latestPatch and allowPrerelease false.'
    }

    [xml] $rootProps = [System.IO.File]::ReadAllText(
        (Resolve-RepositoryFile $RepositoryRoot 'Directory.Build.props' 'Build properties'))
    $targetFramework = [string] $rootProps.Project.PropertyGroup.TargetFramework
    $repositoryUrl = [string] $rootProps.Project.PropertyGroup.RepositoryUrl
    $repositoryType = [string] $rootProps.Project.PropertyGroup.RepositoryType
    if ($targetFramework -cne $script:ExpectedTargetFramework) {
        throw "Directory.Build.props must target '$($script:ExpectedTargetFramework)'."
    }
    if ($repositoryUrl -cne $script:CanonicalRepositoryUrl -or $repositoryType -cne 'git') {
        throw 'Directory.Build.props must carry the canonical tracked HTTPS RepositoryUrl and RepositoryType git.'
    }

    return [pscustomobject]@{
        ConfiguredSdkMinimum = [string] $sdk.version
        RollForward = [string] $sdk.rollForward
        AllowPrerelease = [bool] $sdk.allowPrerelease
        TargetFramework = $targetFramework
        RepositoryUrl = $repositoryUrl
    }
}

function Test-PrereleaseSdkVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $SelectedVersion,
        [Parameter(Mandatory)][string] $ConfiguredMinimum
    )

    if ($SelectedVersion -cnotmatch '^10\.0\.(?<patch>[0-9]{3})$' -or
        $ConfiguredMinimum -cnotmatch '^10\.0\.(?<floor>[0-9]{3})$') {
        throw "SDK '$SelectedVersion' and configured minimum '$ConfiguredMinimum' must be stable three-part 10.0 SDK versions."
    }
    $selectedPatch = [int] ([regex]::Match($SelectedVersion, '^10\.0\.([0-9]{3})$').Groups[1].Value)
    $floorPatch = [int] ([regex]::Match($ConfiguredMinimum, '^10\.0\.([0-9]{3})$').Groups[1].Value)
    if ($floorPatch -lt 400 -or $floorPatch -ge 500 -or
        $selectedPatch -lt $floorPatch -or $selectedPatch -ge 500) {
        throw "Selected SDK '$SelectedVersion' is outside the configured stable 10.0.4xx range [$ConfiguredMinimum, 10.0.500)."
    }
}

function Get-SelectedDotNetSdkVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $ConfiguredMinimum
    )

    $selected = Get-ProcessScalar 'dotnet' $RepositoryRoot @('--version') 'Reading the selected .NET SDK'
    Test-PrereleaseSdkVersion -SelectedVersion $selected -ConfiguredMinimum $ConfiguredMinimum
    return $selected
}

function Get-PrereleaseSelectedInputRecords {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RepositoryRoot)

    $records = [System.Collections.Generic.List[object]]::new()
    foreach ($relativePath in Get-PrereleaseSelectedInputPaths) {
        $fullPath = Resolve-RepositoryFile $RepositoryRoot $relativePath 'Selected input'
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Selected input '$relativePath' is missing."
        }
        $records.Add([ordered]@{
            path = $relativePath
            sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }
    return $records.ToArray()
}

function Test-PrereleaseRestoreGraph {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $RepositoryRoot,
        [Parameter(Mandatory)][string] $ArtifactsRoot,
        [Parameter(Mandatory)][string[]] $RootProjects
    )

    $root = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($RepositoryRoot))
    $graphProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $dgspecFiles = @(Get-ChildItem -LiteralPath $ArtifactsRoot -Recurse -File -Filter '*.nuget.dgspec.json')
    if ($dgspecFiles.Count -eq 0) {
        throw "No isolated NuGet dependency graph was produced under '$ArtifactsRoot'."
    }
    foreach ($file in $dgspecFiles) {
        $document = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 100
        foreach ($property in $document.projects.psobject.Properties) {
            $projectPath = [System.IO.Path]::GetFullPath([string] $property.Name)
            if (-not $projectPath.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Restore graph contains project outside the repository: '$projectPath'."
            }
            $relative = [System.IO.Path]::GetRelativePath($root, $projectPath).Replace('\', '/')
            Assert-CanonicalRepositoryRelativePath $relative 'Restore graph'
            $null = $graphProjects.Add($relative)
        }
    }
    [string[]] $actualProjects = @($graphProjects)
    [System.Array]::Sort($actualProjects, [System.StringComparer]::Ordinal)
    if (($actualProjects -join "`n") -cne ($script:PublishGraphProjects -join "`n")) {
        throw "Isolated restore graph differs from the exact 12-project publisher allowlist.`nActual:`n$($actualProjects -join "`n")"
    }

    $assetsByProject = @{}
    foreach ($file in Get-ChildItem -LiteralPath $ArtifactsRoot -Recurse -File -Filter 'project.assets.json') {
        $assets = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 100
        $projectPath = [System.IO.Path]::GetFullPath([string] $assets.project.restore.projectPath)
        if ($assetsByProject.ContainsKey($projectPath)) {
            throw "Isolated restore produced duplicate project.assets.json files for '$projectPath'."
        }
        $assetsByProject.Add($projectPath, [pscustomobject]@{ Path = $file.FullName; Json = $assets })
    }

    $rootRuntimeSets = [System.Collections.Generic.List[string]]::new()
    foreach ($relativeRoot in $RootProjects) {
        $rootPath = Resolve-RepositoryFile $root $relativeRoot 'Restore root'
        if (-not $assetsByProject.ContainsKey($rootPath)) {
            throw "No isolated project.assets.json was found for restore root '$relativeRoot'."
        }
        $assets = $assetsByProject[$rootPath].Json
        $artifactsFullPath = [System.IO.Path]::TrimEndingDirectorySeparator(
            [System.IO.Path]::GetFullPath($ArtifactsRoot))
        $recordedOutputPath = [System.IO.Path]::TrimEndingDirectorySeparator(
            [System.IO.Path]::GetFullPath([string] $assets.project.restore.outputPath))
        if (-not $recordedOutputPath.StartsWith(
                $artifactsFullPath + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Restore root '$relativeRoot' did not use the isolated artifacts root."
        }
        if (-not ($assets.targets.psobject.Properties.Name -ccontains "$($script:ExpectedTargetFramework)/$($script:ExpectedRuntimeIdentifier)")) {
            throw "Restore root '$relativeRoot' has no exact $($script:ExpectedTargetFramework)/$($script:ExpectedRuntimeIdentifier) assets target."
        }
        if ($assets.project.restore.restoreLockProperties.restoreLockedMode -ne $true -or
            [string] $assets.project.restore.restoreLockProperties.restorePackagesWithLockFile -cne 'true') {
            throw "Restore root '$relativeRoot' does not record locked package restore."
        }
        $framework = $assets.project.frameworks.psobject.Properties[$script:ExpectedTargetFramework].Value
        if ($null -eq $framework) {
            throw "Restore root '$relativeRoot' has no $($script:ExpectedTargetFramework) framework."
        }
        [string[]] $downloads = @(
            foreach ($dependency in @($framework.downloadDependencies)) {
                $name = [string] $dependency.name
                $version = [string] $dependency.version
                if ($name -cnotmatch '^Microsoft\.(?:NETCore|WindowsDesktop|AspNetCore)\.App\.Runtime\.win-x64$' -or
                    $version -cnotmatch '^\[(?<value>[0-9]+\.[0-9]+\.[0-9]+), \k<value>\]$') {
                    throw "Restore root '$relativeRoot' has a malformed runtime-pack download '$name $version'."
                }
                "$name/$($Matches.value)"
            }
        )
        [System.Array]::Sort($downloads, [System.StringComparer]::Ordinal)
        if ($downloads.Count -eq 0 -or @($downloads | Select-Object -Unique).Count -ne $downloads.Count) {
            throw "Restore root '$relativeRoot' has a missing or duplicate runtime-pack download set."
        }
        $rootRuntimeSets.Add($downloads -join "`n")
    }
    if (@($rootRuntimeSets | Select-Object -Unique).Count -ne 1) {
        throw 'CLI and Desktop post-restore project.assets.json runtime-pack download sets disagree.'
    }
    return [pscustomobject]@{
        RuntimePackDownloads = @($rootRuntimeSets[0].Split("`n"))
    }
}

function Get-PrereleasePublishedRuntimeIdentity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $PayloadDirectory)

    $depsFiles = @(Get-ChildItem -LiteralPath $PayloadDirectory -File -Filter '*.deps.json')
    if ($depsFiles.Count -ne 1) {
        throw "Payload '$PayloadDirectory' must contain exactly one root .deps.json file."
    }
    $deps = [System.IO.File]::ReadAllText($depsFiles[0].FullName) | ConvertFrom-Json -Depth 100
    $runtimeTarget = [string] $deps.runtimeTarget.name
    if ($runtimeTarget -cne $script:ExpectedRuntimeTarget) {
        throw "Payload '$PayloadDirectory' has runtime target '$runtimeTarget'; expected '$($script:ExpectedRuntimeTarget)'."
    }
    $target = $deps.targets.psobject.Properties[$runtimeTarget].Value
    if ($null -eq $target) {
        throw "Payload '$PayloadDirectory' has no dependency target '$runtimeTarget'."
    }
    [string[]] $runtimeLibraries = @(
        $target.psobject.Properties.Name | Where-Object { $_.StartsWith('runtimepack.', [System.StringComparison]::Ordinal) }
    )
    [System.Array]::Sort($runtimeLibraries, [System.StringComparer]::Ordinal)
    if ($runtimeLibraries.Count -ne 1 -or $runtimeLibraries[0] -cnotmatch '^(?<id>runtimepack\.[^/]+)/(?<version>[^/]+)$') {
        throw "Payload '$PayloadDirectory' must contain exactly one canonical runtime-pack library."
    }
    $runtimePackId = $Matches.id
    $runtimePackVersion = $Matches.version
    if ($runtimePackId -cne $script:ExpectedRuntimePackId -or $runtimePackVersion -cnotmatch '^10\.0\.[0-9]+$') {
        throw "Payload '$PayloadDirectory' carries unexpected runtime pack '$($runtimeLibraries[0])'."
    }
    return [pscustomobject]@{
        RuntimeTarget = $runtimeTarget
        RuntimePacks = @([ordered]@{ id = $runtimePackId; version = $runtimePackVersion })
    }
}

function ConvertTo-CanonicalBuildEvidenceBytes {
    param([Parameter(Mandatory)] $Evidence)

    $json = ConvertTo-Json -InputObject $Evidence -Depth 20
    $json = $json.Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
    return ,[System.Text.UTF8Encoding]::new($false).GetBytes($json)
}

function New-PrereleaseBuildEvidenceBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $InitialSource,
        [Parameter(Mandatory)] $FinalSource,
        [Parameter(Mandatory)] $RepositoryContract,
        [Parameter(Mandatory)][string] $SelectedSdk,
        [Parameter(Mandatory)][object[]] $RuntimePacks,
        [Parameter(Mandatory)][string] $ThirdPartyEvidenceManifestSha256,
        [Parameter(Mandatory)][object[]] $SelectedInputs
    )

    Assert-PrereleaseSourceStateEqual $InitialSource $FinalSource
    Test-PrereleaseSdkVersion $SelectedSdk $RepositoryContract.ConfiguredSdkMinimum
    $evidence = [ordered]@{
        schema = $script:EvidenceSchema
        scope = [ordered]@{
            kind = 'UnsignedLocalValidation'
            localOnly = $true
            redistributionApproved = $false
            slsaProvenanceClaimed = $false
            reproducibleBuildClaimed = $false
            releaseClosureClaimed = $false
        }
        source = [ordered]@{
            repositoryUrl = $RepositoryContract.RepositoryUrl
            objectFormat = $InitialSource.ObjectFormat
            initialCommit = $InitialSource.Commit
            initialTree = $InitialSource.Tree
            finalCommit = $FinalSource.Commit
            finalTree = $FinalSource.Tree
            workingTreePolicy = 'raw-head-bytes-clean-at-start-after-build-and-before-output-staging'
        }
        build = [ordered]@{
            configuration = $script:ExpectedConfiguration
            targetFramework = $RepositoryContract.TargetFramework
            runtimeIdentifier = $script:ExpectedRuntimeIdentifier
            selfContained = $true
            sdk = [ordered]@{
                configuredMinimum = $RepositoryContract.ConfiguredSdkMinimum
                rollForward = $RepositoryContract.RollForward
                allowPrerelease = $RepositoryContract.AllowPrerelease
                selected = $SelectedSdk
            }
            runtimeTarget = $script:ExpectedRuntimeTarget
            runtimePacks = @($RuntimePacks)
            thirdPartyEvidenceManifestSha256 = $ThirdPartyEvidenceManifestSha256
        }
        selectedInputs = @($SelectedInputs)
    }
    return ConvertTo-CanonicalBuildEvidenceBytes $evidence
}

function Assert-JsonNoDuplicateProperties {
    param(
        [Parameter(Mandatory)][System.Text.Json.JsonElement] $Element,
        [Parameter(Mandatory)][string] $Context
    )

    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) {
                throw "Build evidence repeats JSON property '$($property.Name)' at $Context."
            }
            Assert-JsonNoDuplicateProperties $property.Value "$Context.$($property.Name)"
        }
    }
    elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-JsonNoDuplicateProperties $item "$Context[$index]"
            $index++
        }
    }
}

function Assert-JsonObjectKeys {
    param(
        [Parameter(Mandatory)][System.Text.Json.JsonElement] $Element,
        [Parameter(Mandatory)][string[]] $ExpectedKeys,
        [Parameter(Mandatory)][string] $Context
    )

    if ($Element.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
        throw "Build evidence $Context must be a JSON object."
    }
    [string[]] $actual = @($Element.EnumerateObject() | ForEach-Object Name)
    if (($actual -join "`n") -cne ($ExpectedKeys -join "`n")) {
        throw "Build evidence $Context keys/order are invalid; expected $($ExpectedKeys -join ', ')."
    }
}

function Get-RequiredJsonString {
    param([System.Text.Json.JsonElement] $Object, [string] $Name, [string] $Context)
    $value = $Object.GetProperty($Name)
    if ($value.ValueKind -ne [System.Text.Json.JsonValueKind]::String) {
        throw "Build evidence $Context.$Name must be a string."
    }
    return $value.GetString()
}

function Get-RequiredJsonBoolean {
    param([System.Text.Json.JsonElement] $Object, [string] $Name, [string] $Context)
    $value = $Object.GetProperty($Name)
    if ($value.ValueKind -notin @([System.Text.Json.JsonValueKind]::True, [System.Text.Json.JsonValueKind]::False)) {
        throw "Build evidence $Context.$Name must be a Boolean."
    }
    return $value.GetBoolean()
}

function Test-PrereleaseBuildEvidenceBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][byte[]] $Bytes,
        [string] $RepositoryRoot,
        [switch] $VerifySelectedInputHashes
    )

    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 1MB -or
        ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xef -and $Bytes[1] -eq 0xbb -and $Bytes[2] -eq 0xbf)) {
        throw 'BUILD-EVIDENCE.json must be nonempty, bounded to 1 MiB, and BOM-free.'
    }
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $strictUtf8.GetString($Bytes)
    }
    catch {
        throw 'BUILD-EVIDENCE.json is not valid UTF-8.'
    }
    if ($text.Contains("`r") -or -not $text.EndsWith("`n") -or $text.EndsWith("`n`n")) {
        throw 'BUILD-EVIDENCE.json must use LF only and exactly one final newline.'
    }

    $stream = [System.IO.MemoryStream]::new($Bytes, $false)
    try {
        try {
            $document = [System.Text.Json.JsonDocument]::Parse($stream, [System.Text.Json.JsonDocumentOptions]@{
                AllowTrailingCommas = $false
                CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
                MaxDepth = 32
            })
        }
        catch {
            throw "BUILD-EVIDENCE.json is malformed JSON: $($_.Exception.Message)"
        }
        try {
            $root = $document.RootElement
            Assert-JsonNoDuplicateProperties $root '$'
            Assert-JsonObjectKeys $root @('schema', 'scope', 'source', 'build', 'selectedInputs') '$'
            $schema = Get-RequiredJsonString $root 'schema' '$'
            if ($schema -cne $script:EvidenceSchema) { throw "Unsupported build-evidence schema '$schema'." }

            $scope = $root.GetProperty('scope')
            Assert-JsonObjectKeys $scope @('kind', 'localOnly', 'redistributionApproved', 'slsaProvenanceClaimed', 'reproducibleBuildClaimed', 'releaseClosureClaimed') '$.scope'
            $kind = Get-RequiredJsonString $scope 'kind' '$.scope'
            $localOnly = Get-RequiredJsonBoolean $scope 'localOnly' '$.scope'
            $redistribution = Get-RequiredJsonBoolean $scope 'redistributionApproved' '$.scope'
            $slsa = Get-RequiredJsonBoolean $scope 'slsaProvenanceClaimed' '$.scope'
            $reproducible = Get-RequiredJsonBoolean $scope 'reproducibleBuildClaimed' '$.scope'
            $closure = Get-RequiredJsonBoolean $scope 'releaseClosureClaimed' '$.scope'
            if ($kind -cne 'UnsignedLocalValidation' -or -not $localOnly -or $redistribution -or $slsa -or $reproducible -or $closure) {
                throw 'Build-evidence scope overclaims its unsigned, local-only, non-redistributed status.'
            }

            $source = $root.GetProperty('source')
            Assert-JsonObjectKeys $source @('repositoryUrl', 'objectFormat', 'initialCommit', 'initialTree', 'finalCommit', 'finalTree', 'workingTreePolicy') '$.source'
            $repositoryUrl = Get-RequiredJsonString $source 'repositoryUrl' '$.source'
            $objectFormat = Get-RequiredJsonString $source 'objectFormat' '$.source'
            $initialCommit = Get-RequiredJsonString $source 'initialCommit' '$.source'
            $initialTree = Get-RequiredJsonString $source 'initialTree' '$.source'
            $finalCommit = Get-RequiredJsonString $source 'finalCommit' '$.source'
            $finalTree = Get-RequiredJsonString $source 'finalTree' '$.source'
            $workingTreePolicy = Get-RequiredJsonString $source 'workingTreePolicy' '$.source'
            if ($repositoryUrl -cne $script:CanonicalRepositoryUrl -or $objectFormat -cne 'sha1' -or
                $initialCommit -cnotmatch '^[0-9a-f]{40}$' -or $initialTree -cnotmatch '^[0-9a-f]{40}$' -or
                $finalCommit -cne $initialCommit -or $finalTree -cne $initialTree -or
                $workingTreePolicy -cne 'raw-head-bytes-clean-at-start-after-build-and-before-output-staging') {
                throw 'Build-evidence source identity or clean-working-tree policy is invalid.'
            }

            $build = $root.GetProperty('build')
            Assert-JsonObjectKeys $build @('configuration', 'targetFramework', 'runtimeIdentifier', 'selfContained', 'sdk', 'runtimeTarget', 'runtimePacks', 'thirdPartyEvidenceManifestSha256') '$.build'
            $configuration = Get-RequiredJsonString $build 'configuration' '$.build'
            $targetFramework = Get-RequiredJsonString $build 'targetFramework' '$.build'
            $runtimeIdentifier = Get-RequiredJsonString $build 'runtimeIdentifier' '$.build'
            $selfContained = Get-RequiredJsonBoolean $build 'selfContained' '$.build'
            $runtimeTarget = Get-RequiredJsonString $build 'runtimeTarget' '$.build'
            $noticeHash = Get-RequiredJsonString $build 'thirdPartyEvidenceManifestSha256' '$.build'
            if ($configuration -cne $script:ExpectedConfiguration -or $targetFramework -cne $script:ExpectedTargetFramework -or
                $runtimeIdentifier -cne $script:ExpectedRuntimeIdentifier -or -not $selfContained -or
                $runtimeTarget -cne $script:ExpectedRuntimeTarget -or $noticeHash -cnotmatch '^[0-9a-f]{64}$') {
                throw 'Build-evidence target, configuration, runtime, self-contained flag, or notice hash is invalid.'
            }

            $sdk = $build.GetProperty('sdk')
            Assert-JsonObjectKeys $sdk @('configuredMinimum', 'rollForward', 'allowPrerelease', 'selected') '$.build.sdk'
            $configuredMinimum = Get-RequiredJsonString $sdk 'configuredMinimum' '$.build.sdk'
            $rollForward = Get-RequiredJsonString $sdk 'rollForward' '$.build.sdk'
            $allowPrerelease = Get-RequiredJsonBoolean $sdk 'allowPrerelease' '$.build.sdk'
            $selectedSdk = Get-RequiredJsonString $sdk 'selected' '$.build.sdk'
            if ($rollForward -cne 'latestPatch' -or $allowPrerelease) {
                throw 'Build-evidence SDK roll-forward or prerelease policy is invalid.'
            }
            Test-PrereleaseSdkVersion $selectedSdk $configuredMinimum

            $runtimePacksElement = $build.GetProperty('runtimePacks')
            if ($runtimePacksElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Array -or $runtimePacksElement.GetArrayLength() -ne 1) {
                throw 'Build evidence must record exactly one actual common published runtime pack.'
            }
            $runtimePacks = [System.Collections.Generic.List[object]]::new()
            foreach ($pack in $runtimePacksElement.EnumerateArray()) {
                Assert-JsonObjectKeys $pack @('id', 'version') '$.build.runtimePacks[]'
                $id = Get-RequiredJsonString $pack 'id' '$.build.runtimePacks[]'
                $version = Get-RequiredJsonString $pack 'version' '$.build.runtimePacks[]'
                if ($id -cne $script:ExpectedRuntimePackId -or $version -cnotmatch '^10\.0\.[0-9]+$') {
                    throw "Build evidence records invalid runtime pack '$id/$version'."
                }
                $runtimePacks.Add([ordered]@{ id = $id; version = $version })
            }

            $inputsElement = $root.GetProperty('selectedInputs')
            if ($inputsElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Array) {
                throw 'Build evidence selectedInputs must be an array.'
            }
            [string[]] $expectedPaths = @(Get-PrereleaseSelectedInputPaths)
            if ($inputsElement.GetArrayLength() -ne $expectedPaths.Count) {
                throw "Build evidence has $($inputsElement.GetArrayLength()) selected inputs; expected $($expectedPaths.Count)."
            }
            $inputs = [System.Collections.Generic.List[object]]::new()
            $index = 0
            foreach ($input in $inputsElement.EnumerateArray()) {
                Assert-JsonObjectKeys $input @('path', 'sha256') '$.selectedInputs[]'
                $path = Get-RequiredJsonString $input 'path' '$.selectedInputs[]'
                $sha256 = Get-RequiredJsonString $input 'sha256' '$.selectedInputs[]'
                Assert-CanonicalRepositoryRelativePath $path 'Selected input'
                if ($path -cne $expectedPaths[$index] -or $sha256 -cnotmatch '^[0-9a-f]{64}$') {
                    throw "Build-evidence selected input '$path' is missing, out of order, or has an invalid SHA-256."
                }
                if ($VerifySelectedInputHashes) {
                    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
                        throw 'RepositoryRoot is required when verifying selected-input hashes.'
                    }
                    $actualHash = (Get-FileHash -LiteralPath (Resolve-RepositoryFile $RepositoryRoot $path 'Selected input') -Algorithm SHA256).Hash.ToLowerInvariant()
                    if ($actualHash -cne $sha256) {
                        throw "Build-evidence selected-input hash mismatch for '$path'."
                    }
                }
                $inputs.Add([ordered]@{ path = $path; sha256 = $sha256 })
                $index++
            }

            $canonical = ConvertTo-CanonicalBuildEvidenceBytes ([ordered]@{
                schema = $schema
                scope = [ordered]@{
                    kind = $kind
                    localOnly = $localOnly
                    redistributionApproved = $redistribution
                    slsaProvenanceClaimed = $slsa
                    reproducibleBuildClaimed = $reproducible
                    releaseClosureClaimed = $closure
                }
                source = [ordered]@{
                    repositoryUrl = $repositoryUrl
                    objectFormat = $objectFormat
                    initialCommit = $initialCommit
                    initialTree = $initialTree
                    finalCommit = $finalCommit
                    finalTree = $finalTree
                    workingTreePolicy = $workingTreePolicy
                }
                build = [ordered]@{
                    configuration = $configuration
                    targetFramework = $targetFramework
                    runtimeIdentifier = $runtimeIdentifier
                    selfContained = $selfContained
                    sdk = [ordered]@{
                        configuredMinimum = $configuredMinimum
                        rollForward = $rollForward
                        allowPrerelease = $allowPrerelease
                        selected = $selectedSdk
                    }
                    runtimeTarget = $runtimeTarget
                    runtimePacks = $runtimePacks.ToArray()
                    thirdPartyEvidenceManifestSha256 = $noticeHash
                }
                selectedInputs = $inputs.ToArray()
            })
            if (-not (Test-ByteArraysEqual $Bytes $canonical)) {
                throw 'BUILD-EVIDENCE.json is valid data but not in the exact canonical serialization.'
            }
            return [pscustomobject]@{
                Sha256 = Get-ByteArraySha256 $Bytes
                InitialCommit = $initialCommit
                InitialTree = $initialTree
                SelectedSdk = $selectedSdk
                RuntimePack = "$($runtimePacks[0].id)/$($runtimePacks[0].version)"
                ThirdPartyEvidenceManifestSha256 = $noticeHash
            }
        }
        finally {
            $document.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Write-PrereleaseBuildEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][byte[]] $Bytes
    )

    if (Test-Path -LiteralPath $Path) {
        throw "Refusing to overwrite existing build evidence '$Path'."
    }
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-PrereleaseBuildEvidenceIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][byte[]] $First,
        [Parameter(Mandatory)][byte[]] $Second
    )
    $null = Test-PrereleaseBuildEvidenceBytes $First
    $null = Test-PrereleaseBuildEvidenceBytes $Second
    if (-not (Test-ByteArraysEqual $First $Second)) {
        throw 'CLI and Desktop BUILD-EVIDENCE.json bytes are not identical.'
    }
}

function Assert-Throws {
    param([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $Name)
    try {
        & $Action
    }
    catch {
        return
    }
    throw "Self-test '$Name' unexpectedly succeeded."
}

function Get-ByteArraySha256 {
    param([Parameter(Mandatory)][byte[]] $Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Invoke-PrereleaseBuildEvidenceSelfTest {
    [CmdletBinding()]
    param()

    function New-SelfTestRestoreGraphFixture {
        param(
            [Parameter(Mandatory)][string] $RepositoryRoot,
            [Parameter(Mandatory)][string] $ArtifactsRoot,
            [switch] $Disagree,
            [switch] $OutsideRepository
        )

        $null = New-Item -ItemType Directory -Path $ArtifactsRoot -Force
        $projects = [ordered]@{}
        foreach ($relativeProject in $script:PublishGraphProjects) {
            $absoluteProject = [System.IO.Path]::GetFullPath(
                (Join-Path $RepositoryRoot $relativeProject.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
            $projects[$absoluteProject] = [ordered]@{}
        }
        if ($OutsideRepository) {
            $outsideProject = [System.IO.Path]::GetFullPath(
                (Join-Path ([System.IO.Path]::GetDirectoryName($RepositoryRoot)) 'outside-project.csproj'))
            $projects[$outsideProject] = [ordered]@{}
        }
        $dgspec = [ordered]@{ projects = $projects }
        [System.IO.File]::WriteAllText(
            (Join-Path $ArtifactsRoot 'fixture.nuget.dgspec.json'),
            ((ConvertTo-Json $dgspec -Depth 20) + "`n"),
            [System.Text.UTF8Encoding]::new($false))

        $rootProjects = @(
            'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj'
            'src/PhoenixInspect.Desktop/PhoenixInspect.Desktop.csproj'
        )
        for ($rootIndex = 0; $rootIndex -lt $rootProjects.Count; $rootIndex++) {
            $relativeProject = $rootProjects[$rootIndex]
            $absoluteProject = [System.IO.Path]::GetFullPath(
                (Join-Path $RepositoryRoot $relativeProject.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
            $assetDirectory = Join-Path $ArtifactsRoot "obj/root-$rootIndex"
            $null = New-Item -ItemType Directory -Path $assetDirectory -Force
            $downloads = @(
                [ordered]@{
                    name = 'Microsoft.WindowsDesktop.App.Runtime.win-x64'
                    version = if ($Disagree -and $rootIndex -eq 1) { '[10.0.12, 10.0.12]' } else { '[10.0.11, 10.0.11]' }
                }
                [ordered]@{
                    name = 'Microsoft.NETCore.App.Runtime.win-x64'
                    version = '[10.0.11, 10.0.11]'
                }
            )
            $assets = [ordered]@{
                version = 3
                targets = [ordered]@{
                    "$($script:ExpectedTargetFramework)/$($script:ExpectedRuntimeIdentifier)" = [ordered]@{}
                }
                project = [ordered]@{
                    restore = [ordered]@{
                        projectPath = $absoluteProject
                        outputPath = $assetDirectory + [System.IO.Path]::DirectorySeparatorChar
                        restoreLockProperties = [ordered]@{
                            restorePackagesWithLockFile = 'true'
                            restoreLockedMode = $true
                        }
                    }
                    frameworks = [ordered]@{
                        $script:ExpectedTargetFramework = [ordered]@{
                            downloadDependencies = $downloads
                        }
                    }
                }
            }
            [System.IO.File]::WriteAllText(
                (Join-Path $assetDirectory 'project.assets.json'),
                ((ConvertTo-Json $assets -Depth 30) + "`n"),
                [System.Text.UTF8Encoding]::new($false))
        }
        return $rootProjects
    }

    foreach ($valid in @('10.0.400', '10.0.401', '10.0.499')) {
        Test-PrereleaseSdkVersion $valid '10.0.400'
    }
    foreach ($invalid in @('10.0.399', '10.0.500', '11.0.400', '10.0.401-preview.1')) {
        Assert-Throws { Test-PrereleaseSdkVersion $invalid '10.0.400' } "SDK $invalid"
    }

    $noisyChildCode = @'
[Console]::Error.Write(('x' * 1048576))
[Console]::Out.Write('bounded-output')
'@
    $currentProcess = [System.Diagnostics.Process]::GetCurrentProcess()
    try {
        $pwshPath = $currentProcess.MainModule.FileName
    }
    finally {
        $currentProcess.Dispose()
    }
    $noisyOutput = Invoke-RawProcess `
        -FileName $pwshPath `
        -ArgumentList @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', $noisyChildCode) `
        -WorkingDirectory ([System.IO.Path]::GetTempPath()) `
        -Description 'Noisy redirected-child self-test' `
        -TimeoutSeconds 15
    if ([System.Text.Encoding]::UTF8.GetString($noisyOutput) -cne 'bounded-output') {
        throw 'Noisy redirected-child self-test returned unexpected standard output.'
    }

    $inputs = @(
        foreach ($path in Get-PrereleaseSelectedInputPaths) {
            [ordered]@{ path = $path; sha256 = '0' * 64 }
        }
    )
    $source = [pscustomobject]@{ ObjectFormat = 'sha1'; Commit = '1' * 40; Tree = '2' * 40 }
    $contract = [pscustomobject]@{
        ConfiguredSdkMinimum = '10.0.400'; RollForward = 'latestPatch'; AllowPrerelease = $false
        TargetFramework = 'net10.0'; RepositoryUrl = $script:CanonicalRepositoryUrl
    }
    $bytes = New-PrereleaseBuildEvidenceBytes $source $source $contract '10.0.401' `
        @([ordered]@{ id = $script:ExpectedRuntimePackId; version = '10.0.11' }) ('3' * 64) $inputs
    $null = Test-PrereleaseBuildEvidenceBytes $bytes
    Assert-PrereleaseBuildEvidenceIdentity $bytes ([byte[]] $bytes.Clone())

    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    Assert-Throws {
        $duplicate = $text.Replace('  "scope": {', "  `"schema`": `"$script:EvidenceSchema`",`n  `"scope`": {")
        $null = Test-PrereleaseBuildEvidenceBytes ([System.Text.Encoding]::UTF8.GetBytes($duplicate))
    } 'duplicate top-level JSON key'
    Assert-Throws {
        $duplicate = $text.Replace('    "localOnly": true,', "    `"kind`": `"UnsignedLocalValidation`",`n    `"localOnly`": true,")
        $null = Test-PrereleaseBuildEvidenceBytes ([System.Text.Encoding]::UTF8.GetBytes($duplicate))
    } 'duplicate nested JSON key'
    Assert-Throws {
        $extra = $text.Replace('  "scope": {', "  `"unexpected`": false,`n  `"scope`": {")
        $null = Test-PrereleaseBuildEvidenceBytes ([System.Text.Encoding]::UTF8.GetBytes($extra))
    } 'extra JSON key'
    Assert-Throws {
        $wrongType = $text.Replace('"localOnly": true', '"localOnly": "true"')
        $null = Test-PrereleaseBuildEvidenceBytes ([System.Text.Encoding]::UTF8.GetBytes($wrongType))
    } 'wrong JSON type'
    Assert-Throws {
        $uppercaseHash = $text.Replace(('"sha256": "' + ('0' * 64) + '"'), ('"sha256": "' + ('A' * 64) + '"'))
        $null = Test-PrereleaseBuildEvidenceBytes ([System.Text.Encoding]::UTF8.GetBytes($uppercaseHash))
    } 'uppercase SHA-256'
    Assert-Throws {
        $different = [byte[]] $bytes.Clone()
        $different[$different.Length - 2] = [byte] 0x20
        Assert-PrereleaseBuildEvidenceIdentity $bytes $different
    } 'payload byte disagreement'

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('phoenixinspect-evidence-selftest-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $tempRoot
    try {
        function Invoke-SelfTestGit([string[]] $Arguments) {
            $null = Invoke-RawProcess 'git' $Arguments $tempRoot "Self-test git $($Arguments -join ' ')"
        }
        Invoke-SelfTestGit @('init', '--quiet')
        [System.IO.File]::WriteAllText((Join-Path $tempRoot '.gitattributes'), "* text=auto eol=lf`n", [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText((Join-Path $tempRoot '.gitignore'), "Generated/`n*.g.cs`nbin/`nobj/`n", [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText((Join-Path $tempRoot 'tracked.txt'), "alpha`n", [System.Text.UTF8Encoding]::new($false))
        Invoke-SelfTestGit @('add', '--', '.gitattributes', '.gitignore', 'tracked.txt')
        Invoke-SelfTestGit @('-c', 'user.name=PhoenixInspect SelfTest', '-c', 'user.email=selftest@example.invalid', 'commit', '--quiet', '-m', 'fixture')
        $null = Get-PrereleaseSourceState $tempRoot

        [System.IO.File]::WriteAllText((Join-Path $tempRoot 'tracked.txt'), "alpha`r`n", [System.Text.UTF8Encoding]::new($false))
        Assert-Throws { $null = Get-PrereleaseSourceState $tempRoot } 'raw EOL mismatch hidden by Git clean filters'
        [System.IO.File]::WriteAllText((Join-Path $tempRoot 'tracked.txt'), "alpha`n", [System.Text.UTF8Encoding]::new($false))

        Invoke-SelfTestGit @('update-index', '--assume-unchanged', '--', 'tracked.txt')
        Assert-Throws { $null = Get-PrereleaseSourceState $tempRoot } 'assume-unchanged index flag'
        Invoke-SelfTestGit @('update-index', '--no-assume-unchanged', '--', 'tracked.txt')

        $generated = Join-Path $tempRoot 'src/PhoenixInspect.Cli/Generated'
        $null = New-Item -ItemType Directory -Path $generated -Force
        [System.IO.File]::WriteAllText((Join-Path $generated 'Injected.cs'), 'class Injected {}', [System.Text.UTF8Encoding]::new($false))
        Assert-Throws { $null = Get-PrereleaseSourceState $tempRoot } 'ignored generated source'
        Remove-Item -LiteralPath (Join-Path $tempRoot 'src') -Recurse -Force

        $bin = Join-Path $tempRoot 'src/PhoenixInspect.Cli/bin'
        $null = New-Item -ItemType Directory -Path $bin -Force
        [System.IO.File]::WriteAllText((Join-Path $bin 'stale.dll'), 'ignored', [System.Text.UTF8Encoding]::new($false))
        $null = Get-PrereleaseSourceState $tempRoot

        $headlessBin = Join-Path $tempRoot 'src/PhoenixInspect.Headless.ReferenceConsumer/bin'
        $null = New-Item -ItemType Directory -Path $headlessBin -Force
        [System.IO.File]::WriteAllText((Join-Path $headlessBin 'stale.dll'), 'ignored', [System.Text.UTF8Encoding]::new($false))
        $null = Get-PrereleaseSourceState $tempRoot

        [System.IO.File]::WriteAllText((Join-Path $tempRoot 'untracked.txt'), 'untracked', [System.Text.UTF8Encoding]::new($false))
        Assert-Throws { $null = Get-PrereleaseSourceState $tempRoot } 'ordinary untracked source state'

        $validArtifacts = Join-Path $tempRoot 'restore-fixture-valid'
        $rootProjects = New-SelfTestRestoreGraphFixture `
            -RepositoryRoot $tempRoot `
            -ArtifactsRoot $validArtifacts
        $restoreResult = Test-PrereleaseRestoreGraph `
            -RepositoryRoot $tempRoot `
            -ArtifactsRoot $validArtifacts `
            -RootProjects $rootProjects
        [string[]] $expectedDownloads = @(
            'Microsoft.NETCore.App.Runtime.win-x64/10.0.11'
            'Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.11'
        )
        if (($restoreResult.RuntimePackDownloads -join "`n") -cne ($expectedDownloads -join "`n")) {
            throw 'Restore-graph self-test did not return the exact agreed ordinal runtime-pack set.'
        }

        $disagreeArtifacts = Join-Path $tempRoot 'restore-fixture-disagree'
        $disagreeRoots = New-SelfTestRestoreGraphFixture `
            -RepositoryRoot $tempRoot `
            -ArtifactsRoot $disagreeArtifacts `
            -Disagree
        Assert-Throws {
            $null = Test-PrereleaseRestoreGraph $tempRoot $disagreeArtifacts $disagreeRoots
        } 'restore runtime-pack disagreement'

        $missingArtifacts = Join-Path $tempRoot 'restore-fixture-missing-root'
        $missingRoots = New-SelfTestRestoreGraphFixture `
            -RepositoryRoot $tempRoot `
            -ArtifactsRoot $missingArtifacts
        Remove-Item -LiteralPath (Join-Path $missingArtifacts 'obj/root-1/project.assets.json') -Force
        Assert-Throws {
            $null = Test-PrereleaseRestoreGraph $tempRoot $missingArtifacts $missingRoots
        } 'restore missing root assets'

        $outsideArtifacts = Join-Path $tempRoot 'restore-fixture-outside'
        $outsideRoots = New-SelfTestRestoreGraphFixture `
            -RepositoryRoot $tempRoot `
            -ArtifactsRoot $outsideArtifacts `
            -OutsideRepository
        Assert-Throws {
            $null = Test-PrereleaseRestoreGraph $tempRoot $outsideArtifacts $outsideRoots
        } 'restore graph outside repository'
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }

    Write-Output 'Prerelease build-evidence self-test passed: canonical contract, adversarial JSON/SDK/identity cases, and raw Git source-state boundaries.'
}

Export-ModuleMember -Function @(
    'Assert-PrereleaseBuildEvidenceIdentity'
    'Assert-PrereleaseSourceStateEqual'
    'Get-PrereleasePublishedRuntimeIdentity'
    'Get-PrereleaseRepositoryContract'
    'Get-PrereleaseSelectedInputRecords'
    'Get-PrereleaseSelectedInputPaths'
    'Get-PrereleaseSourceState'
    'Get-SelectedDotNetSdkVersion'
    'Invoke-PrereleaseBuildEvidenceSelfTest'
    'New-PrereleaseBuildEvidenceBytes'
    'Test-PrereleaseBuildEvidenceBytes'
    'Test-PrereleaseRestoreGraph'
    'Test-PrereleaseSdkVersion'
    'Write-PrereleaseBuildEvidence'
)
