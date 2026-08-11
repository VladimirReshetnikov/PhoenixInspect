#Requires -Version 7.0
#Requires -PSEdition Core

<#
.SYNOPSIS
    Builds the bounded PhoenixInspect Windows prerelease artifact set.

.DESCRIPTION
    Locked-restores and publishes exactly the CLI and desktop applications as win-x64, self-contained Release
    directory layouts. A mechanically verified third-party dependency and notice-evidence bundle is embedded in
    both payloads. Each payload then receives a complete per-file SHA-256 manifest and is archived with sorted entries
    and normalized timestamps. The archives are re-extracted before the CLI help and non-UI Desktop load smokes run
    with 30-second process bounds. The output directory contains exactly two ZIPs and SHA256SUMS.txt.

    These are unsigned local-validation artifacts and must not be redistributed. This script does not create NuGet
    packages, a GitHub release, an SBOM, provenance, a signature, or evidence of W8.10 release closure.

.PARAMETER OutputDirectory
    Destination for the two ZIP files and SHA256SUMS.txt. A successful run replaces this directory's contents so
    stale files cannot be mistaken for current output.

.EXAMPLE
    ./eng/Publish-PrereleaseArtifacts.ps1

.EXAMPLE
    ./eng/Publish-PrereleaseArtifacts.ps1 -OutputDirectory ./artifacts/prerelease
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts/prerelease'
}

$outputFullPath = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($OutputDirectory))
$repositoryFullPath = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($repositoryRoot))
$outputRoot = [System.IO.Path]::GetPathRoot($outputFullPath)
$artifactsRoot = [System.IO.Path]::TrimEndingDirectorySeparator(
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')))
if ($outputFullPath -eq $repositoryFullPath -or $outputFullPath -eq $outputRoot -or $outputFullPath -eq $artifactsRoot) {
    throw "OutputDirectory must identify a dedicated artifact directory, not '$outputFullPath'."
}

$runtimeIdentifier = 'win-x64'
$configuration = 'Release'
$sourcePropsPath = Join-Path $repositoryRoot 'src/Directory.Build.props'
[xml] $sourceProps = [System.IO.File]::ReadAllText($sourcePropsPath)
$versionPrefix = [string] $sourceProps.Project.PropertyGroup.VersionPrefix
$versionSuffix = [string] $sourceProps.Project.PropertyGroup.VersionSuffix
if ([string]::IsNullOrWhiteSpace($versionPrefix) -or [string]::IsNullOrWhiteSpace($versionSuffix)) {
    throw "VersionPrefix and VersionSuffix must both be explicit in '$sourcePropsPath'."
}

$expectedVersion = "$versionPrefix-$versionSuffix"
$headlessProcess = Join-Path $PSScriptRoot 'Invoke-HeadlessProcess.ps1'
$noticeGenerator = Join-Path $PSScriptRoot 'Generate-ThirdPartyNotices.ps1'
. (Join-Path $PSScriptRoot 'Enable-HeadlessTestMode.ps1')
$null = Enable-HeadlessTestMode
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("phoenixinspect-publish-" + [guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $workRoot 'publish'
$archiveRoot = Join-Path $workRoot 'archives'
$noticeEvidenceRoot = Join-Path $workRoot 'third-party-notices-evidence'

$outputLockHashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
try {
    $outputLockBytes = [System.Text.Encoding]::UTF8.GetBytes($outputFullPath.ToUpperInvariant())
    $outputLockHash = ([System.BitConverter]::ToString(
        $outputLockHashAlgorithm.ComputeHash($outputLockBytes))).Replace('-', '').ToLowerInvariant()
}
finally {
    $outputLockHashAlgorithm.Dispose()
}
$outputMutexName = "Local\PhoenixInspect.Prerelease.$outputLockHash"

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string[]] $CommandArguments,

        [Parameter(Mandatory)]
        [string] $Description
    )

    & $headlessProcess 'dotnet' @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Write-Utf8Lines {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string[]] $Lines
    )

    $text = ($Lines -join "`n") + "`n"
    [System.IO.File]::WriteAllText($Path, $text, [System.Text.UTF8Encoding]::new($false))
}

function Read-CanonicalUtf8Lines {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Context
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $strictUtf8.GetString($bytes)
    }
    catch {
        throw "$Context '$Path' is not valid UTF-8."
    }
    if ($text.StartsWith([char] 0xfeff) -or $text.Contains("`r") -or -not $text.EndsWith("`n")) {
        throw "$Context '$Path' must be BOM-free UTF-8 with LF line endings and a final newline."
    }

    return [string[]] $text.Substring(0, $text.Length - 1).Split("`n")
}

function Get-PayloadFiles {
    param([Parameter(Mandatory)][string] $PayloadDirectory)

    [string[]] $paths = @(
        Get-ChildItem -LiteralPath $PayloadDirectory -File -Recurse |
            ForEach-Object FullName
    )
    [System.Array]::Sort($paths, [System.StringComparer]::Ordinal)
    return @($paths | ForEach-Object { Get-Item -LiteralPath $_ })
}

function Assert-CanonicalRelativePath {
    param(
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][string] $Context,
        [switch] $LeafOnly
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.Contains('\') -or
        $RelativePath.StartsWith('/') -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Context path '$RelativePath' is not a canonical forward-slash relative path."
    }

    [string[]] $segments = $RelativePath.Split('/')
    if ($LeafOnly -and $segments.Count -ne 1) {
        throw "$Context path '$RelativePath' must be a leaf name."
    }
    foreach ($segment in $segments) {
        if ([string]::IsNullOrEmpty($segment) -or
            $segment -eq '.' -or
            $segment -eq '..' -or
            $segment.EndsWith('.') -or
            $segment.EndsWith(' ') -or
            $segment.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
            throw "$Context path '$RelativePath' contains a non-canonical segment '$segment'."
        }
    }
}

function Resolve-ContainedRelativePath {
    param(
        [Parameter(Mandatory)][string] $RootDirectory,
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][string] $Context
    )

    Assert-CanonicalRelativePath -RelativePath $RelativePath -Context $Context
    $rootFullPath = [System.IO.Path]::TrimEndingDirectorySeparator(
        [System.IO.Path]::GetFullPath($RootDirectory))
    $nativeRelativePath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $candidateFullPath = [System.IO.Path]::GetFullPath((Join-Path $rootFullPath $nativeRelativePath))
    $rootPrefix = $rootFullPath + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidateFullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Context path '$RelativePath' escapes '$rootFullPath'."
    }

    $roundTrip = [System.IO.Path]::GetRelativePath($rootFullPath, $candidateFullPath).Replace('\', '/')
    if (-not [string]::Equals($roundTrip, $RelativePath, [System.StringComparison]::Ordinal)) {
        throw "$Context path '$RelativePath' is not in canonical filesystem form (round-trip '$roundTrip')."
    }

    return $candidateFullPath
}

function Get-CanonicalPayloadRelativePaths {
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [switch] $ExcludeManifest
    )

    [string[]] $relativePaths = @(
        foreach ($file in Get-PayloadFiles $PayloadDirectory) {
            $relativePath = [System.IO.Path]::GetRelativePath($PayloadDirectory, $file.FullName).Replace('\', '/')
            Assert-CanonicalRelativePath -RelativePath $relativePath -Context 'Payload'
            if (-not $ExcludeManifest -or $relativePath -ne 'ARTIFACT-MANIFEST.txt') {
                $relativePath
            }
        }
    )
    [System.Array]::Sort($relativePaths, [System.StringComparer]::Ordinal)
    return $relativePaths
}

function Assert-PayloadFileAllowed {
    param(
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][System.IO.FileInfo] $File
    )

    $deniedExtensions = @('.dmp', '.dump', '.pfx', '.p12', '.snk', '.key', '.pem')
    $deniedNames = @('.env', 'auth.json')
    if (($File.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reparse-point payload '$RelativePath' is forbidden."
    }
    if ($deniedExtensions -contains $File.Extension.ToLowerInvariant() -or
        $deniedNames -contains $File.Name.ToLowerInvariant() -or
        $RelativePath.Split('/') -contains '.git') {
        throw "Sensitive or dump artifact '$RelativePath' is forbidden in a local-validation payload."
    }
}

function Write-PayloadManifest {
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][string] $Product,
        [Parameter(Mandatory)][string] $Version
    )

    $manifestPath = Join-Path $PayloadDirectory 'ARTIFACT-MANIFEST.txt'
    if (Test-Path -LiteralPath $manifestPath) {
        throw "The publish output unexpectedly supplied its own '$manifestPath'."
    }
    $reparseItems = @(
        Get-ChildItem -LiteralPath $PayloadDirectory -Force -Recurse |
            Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 }
    )
    if ($reparseItems.Count -ne 0) {
        throw "The publish output contains a forbidden reparse point '$($reparseItems[0].FullName)'."
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# PhoenixInspect prerelease payload manifest')
    $lines.Add("# Product: $Product")
    $lines.Add("# Version: $Version")
    $lines.Add("# Runtime: $runtimeIdentifier")
    $lines.Add('# Payload: unsigned, self-contained preview application for local validation')
    $lines.Add('# Third-party evidence: generated inventory and hash-pinned license/notice materials included')
    $lines.Add('# Redistribution blocked: human legal review, SBOM, provenance, and signatures are incomplete')
    $lines.Add('# Not supplied: NuGet package, legal clearance, SBOM, provenance, signature, W8.10 closure')
    foreach ($relativePath in Get-CanonicalPayloadRelativePaths $PayloadDirectory) {
        $filePath = Resolve-ContainedRelativePath `
            -RootDirectory $PayloadDirectory `
            -RelativePath $relativePath `
            -Context 'Payload'
        $file = Get-Item -LiteralPath $filePath
        Assert-PayloadFileAllowed -RelativePath $relativePath -File $file

        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines.Add("$hash *$relativePath")
    }

    Write-Utf8Lines -Path $manifestPath -Lines $lines.ToArray()
    return $manifestPath
}

function Test-PayloadManifest {
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][string] $ManifestPath
    )

    [string[]] $lines = @(Read-CanonicalUtf8Lines -Path $ManifestPath -Context 'Payload manifest')
    [string[]] $fixedHeaders = @(
        '# PhoenixInspect prerelease payload manifest',
        $null,
        $null,
        "# Runtime: $runtimeIdentifier",
        '# Payload: unsigned, self-contained preview application for local validation',
        '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
        '# Redistribution blocked: human legal review, SBOM, provenance, and signatures are incomplete',
        '# Not supplied: NuGet package, legal clearance, SBOM, provenance, signature, W8.10 closure'
    )
    if ($lines.Count -lt $fixedHeaders.Count) {
        throw "Payload manifest '$ManifestPath' is missing its required header."
    }
    foreach ($headerIndex in @(0, 3, 4, 5, 6, 7)) {
        if ($lines[$headerIndex] -ne $fixedHeaders[$headerIndex]) {
            throw "Payload manifest '$ManifestPath' has an invalid header at line $($headerIndex + 1)."
        }
    }
    if ($lines[1] -cnotmatch '^# Product: PhoenixInspect (?:CLI|Desktop)$' -or
        $lines[2] -cnotmatch '^# Version: [0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$') {
        throw "Payload manifest '$ManifestPath' has an invalid product or version header."
    }

    [string[]] $expectedPaths = @(Get-CanonicalPayloadRelativePaths -PayloadDirectory $PayloadDirectory -ExcludeManifest)
    [string[]] $recordLines = @($lines | Select-Object -Skip $fixedHeaders.Count)
    if ($recordLines.Count -ne $expectedPaths.Count) {
        throw "Payload manifest has $($recordLines.Count) records for $($expectedPaths.Count) non-manifest files."
    }

    $seenPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    for ($index = 0; $index -lt $recordLines.Count; $index++) {
        $line = $recordLines[$index]
        if ($line -cnotmatch '^(?<hash>[0-9a-f]{64}) \*(?<path>.+)$') {
            throw "Payload manifest '$ManifestPath' has a malformed record at line $($fixedHeaders.Count + $index + 1)."
        }
        $recordHash = $Matches.hash
        $recordPath = $Matches.path
        Assert-CanonicalRelativePath -RelativePath $recordPath -Context 'Manifest'
        if (-not $seenPaths.Add($recordPath)) {
            throw "Payload manifest '$ManifestPath' repeats path '$recordPath'."
        }
        if (-not [string]::Equals($recordPath, $expectedPaths[$index], [System.StringComparison]::Ordinal)) {
            throw "Payload manifest record '$recordPath' is out of contract; expected '$($expectedPaths[$index])'."
        }

        $filePath = Resolve-ContainedRelativePath `
            -RootDirectory $PayloadDirectory `
            -RelativePath $recordPath `
            -Context 'Manifest'
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Manifest file '$recordPath' is missing."
        }
        Assert-PayloadFileAllowed -RelativePath $recordPath -File (Get-Item -LiteralPath $filePath)

        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $recordHash) {
            throw "Manifest hash mismatch for '$recordPath'."
        }
    }
}

function New-NormalizedZip {
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][string] $ArchivePath,
        [Parameter(Mandatory)][string] $ContentRoot
    )

    Add-Type -AssemblyName System.IO.Compression
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ArchivePath) -Force
    $stream = [System.IO.File]::Open($ArchivePath, [System.IO.FileMode]::CreateNew)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            foreach ($file in Get-PayloadFiles $PayloadDirectory) {
                $relativePath = [System.IO.Path]::GetRelativePath($PayloadDirectory, $file.FullName) -replace '\\', '/'
                $entry = $archive.CreateEntry(
                    "$ContentRoot/$relativePath",
                    [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [datetimeoffset]::new(1980, 1, 1, 0, 0, 0, [timespan]::Zero)
                $source = $file.OpenRead()
                try {
                    $destination = $entry.Open()
                    try {
                        $source.CopyTo($destination)
                    }
                    finally {
                        $destination.Dispose()
                    }
                }
                finally {
                    $source.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    $readStream = [System.IO.File]::OpenRead($ArchivePath)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $readStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            $entries = @($archive.Entries)
            $expectedEntries = @(
                Get-PayloadFiles $PayloadDirectory |
                    ForEach-Object {
                        $relativePath = [System.IO.Path]::GetRelativePath($PayloadDirectory, $_.FullName) -replace '\\', '/'
                        "$ContentRoot/$relativePath"
                    }
            )
            if ($entries.Count -ne $expectedEntries.Count) {
                throw "Archive '$ArchivePath' has $($entries.Count) entries; expected $($expectedEntries.Count)."
            }

            $expectedPrefix = "$ContentRoot/"
            for ($index = 0; $index -lt $entries.Count; $index++) {
                $entry = $entries[$index]
                if (-not $entry.FullName.StartsWith($expectedPrefix, [System.StringComparison]::Ordinal) -or
                    $entry.FullName.Split('/') -contains '..') {
                    throw "Archive '$ArchivePath' contains unexpected entry '$($entry.FullName)'."
                }
                if ($entry.FullName -ne $expectedEntries[$index]) {
                    throw "Archive '$ArchivePath' entry '$($entry.FullName)' is out of contract; expected '$($expectedEntries[$index])'."
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $readStream.Dispose()
    }
}

function Get-PublishedProductVersion {
    param([Parameter(Mandatory)][string] $ExecutablePath)

    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath).ProductVersion
    if ([string]::IsNullOrWhiteSpace($productVersion)) {
        throw "Published executable '$ExecutablePath' has no product version."
    }

    return $productVersion.Split('+')[0]
}

function Test-ChecksumManifest {
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][string] $ChecksumPath,
        [Parameter(Mandatory)][string[]] $ExpectedArchiveNames
    )

    [string[]] $expectedNames = @($ExpectedArchiveNames)
    [System.Array]::Sort($expectedNames, [System.StringComparer]::Ordinal)
    [string[]] $lines = @(Read-CanonicalUtf8Lines -Path $ChecksumPath -Context 'Checksum manifest')
    if ($lines.Count -ne $expectedNames.Count) {
        throw "Checksum manifest '$ChecksumPath' has $($lines.Count) records; expected $($expectedNames.Count)."
    }

    $seenNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -cnotmatch '^(?<hash>[0-9a-f]{64}) \*(?<name>.+)$') {
            throw "Checksum manifest '$ChecksumPath' has a malformed record at line $($index + 1)."
        }
        $recordHash = $Matches.hash
        $recordName = $Matches.name
        Assert-CanonicalRelativePath -RelativePath $recordName -Context 'Checksum' -LeafOnly
        if ($recordName -cnotmatch '^phoenixinspect-(?:cli|desktop)-[0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*-win-x64\.zip$') {
            throw "Checksum manifest '$ChecksumPath' names an invalid archive '$recordName'."
        }
        if (-not $seenNames.Add($recordName)) {
            throw "Checksum manifest '$ChecksumPath' repeats archive '$recordName'."
        }
        if (-not [string]::Equals($recordName, $expectedNames[$index], [System.StringComparison]::Ordinal)) {
            throw "Checksum record '$recordName' is out of contract; expected '$($expectedNames[$index])'."
        }

        $archivePath = Resolve-ContainedRelativePath `
            -RootDirectory $Directory `
            -RelativePath $recordName `
            -Context 'Checksum'
        if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
            throw "Checksum archive '$recordName' is missing."
        }
        $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $recordHash) {
            throw "Checksum verification failed for '$recordName'."
        }
    }
}

function Test-PublisherOwnedArchiveIdentity {
    param(
        [Parameter(Mandatory)][string] $ArchivePath,
        [Parameter(Mandatory)][string] $ContentRoot,
        [Parameter(Mandatory)][string] $Product,
        [Parameter(Mandatory)][string] $Version
    )

    Add-Type -AssemblyName System.IO.Compression
    $readStream = [System.IO.File]::OpenRead($ArchivePath)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $readStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            $entries = @($archive.Entries)
            if ($entries.Count -eq 0) {
                throw "Archive '$ArchivePath' is empty and is not a publisher-owned artifact."
            }

            $seenEntries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            $previousEntryName = $null
            foreach ($entry in $entries) {
                Assert-CanonicalRelativePath -RelativePath $entry.FullName -Context 'Archive entry'
                if (-not $entry.FullName.StartsWith("$ContentRoot/", [System.StringComparison]::Ordinal) -or
                    -not $seenEntries.Add($entry.FullName)) {
                    throw "Archive '$ArchivePath' has an unexpected or duplicate entry '$($entry.FullName)'."
                }
                if ($null -ne $previousEntryName -and
                    [System.StringComparer]::Ordinal.Compare($previousEntryName, $entry.FullName) -ge 0) {
                    throw "Archive '$ArchivePath' entries are not in canonical ordinal order."
                }
                $previousEntryName = $entry.FullName
            }

            $manifestEntryName = "$ContentRoot/ARTIFACT-MANIFEST.txt"
            $manifestEntry = $archive.GetEntry($manifestEntryName)
            if ($null -eq $manifestEntry -or $manifestEntry.Length -gt 1MB) {
                throw "Archive '$ArchivePath' has no bounded publisher manifest '$manifestEntryName'."
            }

            $manifestStream = $manifestEntry.Open()
            try {
                $reader = [System.IO.StreamReader]::new(
                    $manifestStream,
                    [System.Text.UTF8Encoding]::new($false, $true),
                    $false,
                    1024,
                    $true)
                try {
                    $manifestText = $reader.ReadToEnd()
                }
                finally {
                    $reader.Dispose()
                }
            }
            finally {
                $manifestStream.Dispose()
            }

            [string[]] $identityHeader = @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $Version",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Redistribution blocked: human legal review, SBOM, provenance, and signatures are incomplete',
                '# Not supplied: NuGet package, legal clearance, SBOM, provenance, signature, W8.10 closure'
            )
            $identityPrefix = ($identityHeader -join "`n") + "`n"
            if (-not $manifestText.StartsWith($identityPrefix, [System.StringComparison]::Ordinal)) {
                throw "Archive '$ArchivePath' does not carry the expected PhoenixInspect publisher identity."
            }
            if ($manifestText.StartsWith([char] 0xfeff) -or
                $manifestText.Contains("`r") -or
                -not $manifestText.EndsWith("`n")) {
                throw "Archive '$ArchivePath' has a non-canonical embedded publisher manifest encoding."
            }

            [string[]] $manifestLines = $manifestText.Substring(0, $manifestText.Length - 1).Split("`n")
            [object[]] $payloadEntries = @($entries | Where-Object { $_.FullName -ne $manifestEntryName })
            [string[]] $recordLines = @($manifestLines | Select-Object -Skip $identityHeader.Count)
            if ($recordLines.Count -ne $payloadEntries.Count) {
                throw "Archive '$ArchivePath' manifest has $($recordLines.Count) records for $($payloadEntries.Count) payload entries."
            }

            $seenManifestPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            $contentPrefix = "$ContentRoot/"
            for ($index = 0; $index -lt $recordLines.Count; $index++) {
                $line = $recordLines[$index]
                if ($line -cnotmatch '^(?<hash>[0-9a-f]{64}) \*(?<path>.+)$') {
                    throw "Archive '$ArchivePath' has a malformed embedded manifest record at line $($identityHeader.Count + $index + 1)."
                }
                $recordHash = $Matches.hash
                $recordPath = $Matches.path
                Assert-CanonicalRelativePath -RelativePath $recordPath -Context 'Embedded manifest'
                if (-not $seenManifestPaths.Add($recordPath)) {
                    throw "Archive '$ArchivePath' repeats embedded manifest path '$recordPath'."
                }

                $expectedPath = $payloadEntries[$index].FullName.Substring($contentPrefix.Length)
                if (-not [string]::Equals($recordPath, $expectedPath, [System.StringComparison]::Ordinal)) {
                    throw "Archive '$ArchivePath' manifest record '$recordPath' is out of contract; expected '$expectedPath'."
                }

                $entryStream = $payloadEntries[$index].Open()
                try {
                    $sha256 = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $actualHash = ([System.BitConverter]::ToString(
                            $sha256.ComputeHash($entryStream))).Replace('-', '').ToLowerInvariant()
                    }
                    finally {
                        $sha256.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
                if ($actualHash -ne $recordHash) {
                    throw "Archive '$ArchivePath' manifest hash mismatch for '$recordPath'."
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $readStream.Dispose()
    }
}

function Test-ArtifactOutputDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $ExpectedNames,
        [switch] $AllowPriorVersion
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        throw "OutputDirectory '$Path' exists but is not a directory."
    }
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "OutputDirectory '$Path' is a reparse point and will not be replaced."
    }

    $children = @(Get-ChildItem -LiteralPath $Path -Force)
    if (@($children | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
        @($children | Where-Object {
            ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        }).Count -ne 0) {
        throw "Refusing to replace '$Path': artifact output must contain files only and no reparse points."
    }

    [string[]] $actualNames = @($children | ForEach-Object Name)
    [System.Array]::Sort($actualNames, [System.StringComparer]::Ordinal)
    [string[]] $sortedExpectedNames = @($ExpectedNames)
    [System.Array]::Sort($sortedExpectedNames, [System.StringComparer]::Ordinal)
    $isExpectedVersion = [string]::Equals(
        ($actualNames -join "`n"),
        ($sortedExpectedNames -join "`n"),
        [System.StringComparison]::Ordinal)
    if (-not $isExpectedVersion -and -not $AllowPriorVersion) {
        throw "Artifact output '$Path' is not the exact current-version allowlist."
    }
    $checksumNameCount = @($actualNames | Where-Object {
        [string]::Equals($_, 'SHA256SUMS.txt', [System.StringComparison]::Ordinal)
    }).Count
    if ($actualNames.Count -ne 3 -or $checksumNameCount -ne 1) {
        throw "Refusing to replace '$Path': a publisher-owned artifact directory must contain exactly two ZIPs and SHA256SUMS.txt."
    }

    $archiveIdentityPattern = '^phoenixinspect-(?<slug>cli|desktop)-(?<version>[0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)-win-x64\.zip$'
    $archiveIdentities = @(
        foreach ($name in $actualNames | Where-Object {
            -not [string]::Equals($_, 'SHA256SUMS.txt', [System.StringComparison]::Ordinal)
        }) {
            if ($name -cnotmatch $archiveIdentityPattern) {
                throw "Refusing to replace '$Path': archive '$name' is outside the publisher-owned naming contract."
            }
            [pscustomobject]@{ Name = $name; Slug = $Matches.slug; Version = $Matches.version }
        }
    )
    if ($archiveIdentities.Count -ne 2 -or
        @($archiveIdentities | Select-Object -ExpandProperty Slug -Unique).Count -ne 2 -or
        @($archiveIdentities | Select-Object -ExpandProperty Version -Unique).Count -ne 1) {
        throw "Refusing to replace '$Path': CLI and Desktop archives do not share one publisher-owned version."
    }

    Test-ChecksumManifest `
        -Directory $Path `
        -ChecksumPath (Join-Path $Path 'SHA256SUMS.txt') `
        -ExpectedArchiveNames @($archiveIdentities | Select-Object -ExpandProperty Name)
    foreach ($identity in $archiveIdentities) {
        $product = if ($identity.Slug -eq 'cli') { 'PhoenixInspect CLI' } else { 'PhoenixInspect Desktop' }
        $contentRoot = [System.IO.Path]::GetFileNameWithoutExtension($identity.Name)
        Test-PublisherOwnedArchiveIdentity `
            -ArchivePath (Join-Path $Path $identity.Name) `
            -ContentRoot $contentRoot `
            -Product $product `
            -Version $identity.Version
    }
}

function Assert-SwapDirectoryIdentity {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][ValidateSet('stage', 'previous')][string] $Kind
    )

    $pathFull = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Path))
    $expectedParent = [System.IO.Path]::GetDirectoryName($outputFullPath)
    $expectedLeafPrefix = [System.IO.Path]::GetFileName($outputFullPath) + ".$Kind-"
    $actualParent = [System.IO.Path]::GetDirectoryName($pathFull)
    $actualLeaf = [System.IO.Path]::GetFileName($pathFull)
    if (-not [string]::Equals($actualParent, $expectedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
        $actualLeaf -notmatch ('^' + [regex]::Escape($expectedLeafPrefix) + '[0-9a-f]{32}$')) {
        throw "Refusing artifact transaction operation on unexpected $Kind path '$pathFull'."
    }
}

function Remove-GuardedSwapDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][ValidateSet('stage', 'previous')][string] $Kind
    )

    Assert-SwapDirectoryIdentity -Path $Path -Kind $Kind
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing cleanup of unexpected $Kind item '$Path'."
    }
    $children = @(Get-ChildItem -LiteralPath $Path -Force)
    if (@($children | Where-Object {
        $_.PSIsContainer -or ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
    }).Count -ne 0) {
        throw "Refusing cleanup of $Kind directory '$Path' because it contains a directory or reparse point."
    }
    foreach ($file in $children) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
    Remove-Item -LiteralPath $Path -Force
}

function Test-ExtractedArchive {
    param(
        [Parameter(Mandatory)][string] $ArchivePath,
        [Parameter(Mandatory)][string] $ContentRoot,
        [Parameter(Mandatory)][string] $ExtractionParent
    )

    $extractionDirectory = Join-Path $ExtractionParent $ContentRoot
    $null = New-Item -ItemType Directory -Path $extractionDirectory
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $extractionDirectory)
    $payloadDirectory = Join-Path $extractionDirectory $ContentRoot
    $children = @(Get-ChildItem -LiteralPath $extractionDirectory -Force)
    if ($children.Count -ne 1 -or -not $children[0].PSIsContainer -or $children[0].FullName -ne $payloadDirectory) {
        throw "Archive '$ArchivePath' did not extract to exactly one expected content root '$ContentRoot'."
    }

    $manifestPath = Join-Path $payloadDirectory 'ARTIFACT-MANIFEST.txt'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Archive '$ArchivePath' has no embedded ARTIFACT-MANIFEST.txt."
    }
    Test-PayloadManifest -PayloadDirectory $payloadDirectory -ManifestPath $manifestPath
    return $payloadDirectory
}

function Invoke-BoundedCliSmoke {
    param(
        [Parameter(Mandatory)][string] $ExecutablePath,
        [Parameter(Mandatory)][string] $ExpectedVersion,
        [Parameter(Mandatory)][string] $ScratchDirectory
    )

    if ((Get-PublishedProductVersion $ExecutablePath) -ne $ExpectedVersion) {
        throw "Extracted CLI '$ExecutablePath' does not report expected version '$ExpectedVersion'."
    }

    $stdoutPath = Join-Path $ScratchDirectory 'cli-help.stdout.txt'
    $stderrPath = Join-Path $ScratchDirectory 'cli-help.stderr.txt'
    $process = Start-Process `
        -FilePath $ExecutablePath `
        -ArgumentList '--help' `
        -WorkingDirectory (Split-Path -Parent $ExecutablePath) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru
    try {
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'The extracted CLI --help smoke test exceeded its 30-second wall-clock bound.'
        }
        if ($process.ExitCode -ne 0) {
            throw "The extracted CLI --help smoke test failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }

    $helpOutput = [System.IO.File]::ReadAllText($stdoutPath)
    if ($helpOutput -notmatch 'usage:\s+phoenixinspect') {
        throw 'The extracted CLI --help smoke test did not print the expected usage contract.'
    }
    $helpError = [System.IO.File]::ReadAllText($stderrPath)
    if (-not [string]::IsNullOrWhiteSpace($helpError)) {
        throw 'The extracted CLI --help smoke test wrote an unexpected diagnostic to standard error.'
    }
}

function Invoke-BoundedDesktopSmoke {
    param(
        [Parameter(Mandatory)][string] $ExecutablePath,
        [Parameter(Mandatory)][string] $ExpectedVersion,
        [Parameter(Mandatory)][string] $ScratchDirectory
    )

    if ((Get-PublishedProductVersion $ExecutablePath) -ne $ExpectedVersion) {
        throw "Extracted Desktop '$ExecutablePath' does not report expected version '$ExpectedVersion'."
    }

    $stdoutPath = Join-Path $ScratchDirectory 'desktop-smoke.stdout.txt'
    $stderrPath = Join-Path $ScratchDirectory 'desktop-smoke.stderr.txt'
    $process = Start-Process `
        -FilePath $ExecutablePath `
        -ArgumentList '--smoke-test' `
        -WorkingDirectory (Split-Path -Parent $ExecutablePath) `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru
    try {
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'The extracted Desktop --smoke-test exceeded its 30-second wall-clock bound.'
        }
        if ($process.ExitCode -ne 0) {
            $failureDiagnostic = [System.IO.File]::ReadAllText($stderrPath).Trim()
            throw "The extracted Desktop --smoke-test failed with exit code $($process.ExitCode): $failureDiagnostic"
        }
    }
    finally {
        $process.Dispose()
    }

    $expectedOutput = "PHOENIXINSPECT_DESKTOP_SMOKE_OK version=$ExpectedVersion mode=non-ui"
    $actualOutput = [System.IO.File]::ReadAllText($stdoutPath).Trim()
    if ($actualOutput -ne $expectedOutput) {
        throw "The extracted Desktop --smoke-test did not print its exact success contract."
    }

    $errorOutput = [System.IO.File]::ReadAllText($stderrPath)
    if (-not [string]::IsNullOrWhiteSpace($errorOutput)) {
        throw 'The extracted Desktop --smoke-test wrote an unexpected diagnostic to standard error.'
    }
}

$products = @(
    [pscustomobject]@{
        Name = 'PhoenixInspect CLI'
        Slug = 'phoenixinspect-cli'
        Project = Join-Path $repositoryRoot 'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj'
        Executable = 'phoenixinspect.exe'
    },
    [pscustomobject]@{
        Name = 'PhoenixInspect Desktop'
        Slug = 'phoenixinspect-desktop'
        Project = Join-Path $repositoryRoot 'src/PhoenixInspect.Desktop/PhoenixInspect.Desktop.csproj'
        Executable = 'PhoenixInspect.exe'
    }
)

[string[]] $expectedOutputNames = @(
    @($products | ForEach-Object { "$($_.Slug)-$expectedVersion-$runtimeIdentifier.zip" }) + 'SHA256SUMS.txt'
)
[System.Array]::Sort($expectedOutputNames, [System.StringComparer]::Ordinal)
$totalStopwatch = $null
$stagedOutput = $null
$previousOutput = $null
$previousOutputMoved = $false
$newOutputInstalled = $false
$swapCommitted = $false
$outputMutex = [System.Threading.Mutex]::new($false, $outputMutexName)
$outputMutexHeld = $false

try {
    try {
        $outputMutexHeld = $outputMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        # An abandoned mutex still transfers ownership to this process.
        $outputMutexHeld = $true
    }
    if (-not $outputMutexHeld) {
        throw "Another PhoenixInspect publisher is already targeting '$outputFullPath'."
    }

    Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion
    $null = New-Item -ItemType Directory -Path $publishRoot -Force
    $null = New-Item -ItemType Directory -Path $archiveRoot -Force
    $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    foreach ($product in $products) {
        $productStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Write-Host "Locked-restoring $($product.Name)…" -ForegroundColor Cyan
        Invoke-Checked -Description "Restoring $($product.Project)" -CommandArguments @(
            'restore', $product.Project, '--locked-mode', '--runtime', $runtimeIdentifier, '--verbosity', 'minimal')

        $productPublishDirectory = Join-Path $publishRoot $product.Slug
        Write-Host "Publishing $($product.Name) for $runtimeIdentifier…" -ForegroundColor Cyan
        Invoke-Checked -Description "Publishing $($product.Project)" -CommandArguments @(
            'publish', $product.Project,
            '--configuration', $configuration,
            '--runtime', $runtimeIdentifier,
            '--self-contained', 'true',
            '--no-restore',
            '--output', $productPublishDirectory,
            '--verbosity', 'minimal',
            '--nologo',
            '--disable-build-servers',
            '/p:UseSharedCompilation=false')

        $publishedExecutable = Join-Path $productPublishDirectory $product.Executable
        $publishedRuntime = Join-Path $productPublishDirectory 'coreclr.dll'
        foreach ($requiredFile in @($publishedExecutable, $publishedRuntime)) {
            if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
                throw "$($product.Name) is not a complete self-contained publish: '$requiredFile' is missing."
            }
        }

        $actualVersion = Get-PublishedProductVersion $publishedExecutable
        if ($actualVersion -ne $expectedVersion) {
            throw "$($product.Name) reports version '$actualVersion'; expected '$expectedVersion'."
        }

        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $productPublishDirectory 'LICENSE')

        $productStopwatch.Stop()
        Write-Host "$($product.Name) publish completed in $([math]::Round($productStopwatch.Elapsed.TotalSeconds, 1)) s."
    }

    if (-not (Test-Path -LiteralPath $noticeGenerator -PathType Leaf)) {
        throw "Third-party notice generator '$noticeGenerator' is missing."
    }
    Write-Host 'Generating exact third-party dependency and notice evidence…' -ForegroundColor Cyan
    & $noticeGenerator `
        -CliPayloadDirectory (Join-Path $publishRoot 'phoenixinspect-cli') `
        -DesktopPayloadDirectory (Join-Path $publishRoot 'phoenixinspect-desktop') `
        -OutputDirectory $noticeEvidenceRoot
    if (-not (Test-Path -LiteralPath (Join-Path $noticeEvidenceRoot 'MANIFEST.sha256') -PathType Leaf)) {
        throw "Third-party notice generator did not produce its required evidence manifest."
    }

    foreach ($product in $products) {
        $productPublishDirectory = Join-Path $publishRoot $product.Slug
        $noticeDestination = Join-Path $productPublishDirectory 'THIRD-PARTY-NOTICES'
        if (Test-Path -LiteralPath $noticeDestination) {
            throw "$($product.Name) publish unexpectedly already contains '$noticeDestination'."
        }
        Copy-Item -LiteralPath $noticeEvidenceRoot -Destination $noticeDestination -Recurse

        $manifest = Write-PayloadManifest `
            -PayloadDirectory $productPublishDirectory `
            -Product $product.Name `
            -Version $expectedVersion
        Test-PayloadManifest -PayloadDirectory $productPublishDirectory -ManifestPath $manifest

        $archiveBaseName = "$($product.Slug)-$expectedVersion-$runtimeIdentifier"
        $archivePath = Join-Path $archiveRoot "$archiveBaseName.zip"
        Write-Host "Archiving $archiveBaseName…" -ForegroundColor Cyan
        New-NormalizedZip `
            -PayloadDirectory $productPublishDirectory `
            -ArchivePath $archivePath `
            -ContentRoot $archiveBaseName
    }

    $archives = @(Get-ChildItem -LiteralPath $archiveRoot -File -Filter '*.zip' | Sort-Object Name)
    if ($archives.Count -ne $products.Count) {
        throw "Expected $($products.Count) archives, found $($archives.Count)."
    }

    $checksumLines = foreach ($archive in $archives) {
        $hash = (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$($archive.Name)"
    }
    $checksumPath = Join-Path $archiveRoot 'SHA256SUMS.txt'
    Write-Utf8Lines -Path $checksumPath -Lines @($checksumLines)

    Test-ArtifactOutputDirectory -Path $archiveRoot -ExpectedNames $expectedOutputNames

    $extractionRoot = Join-Path $workRoot 'extracted'
    $null = New-Item -ItemType Directory -Path $extractionRoot
    foreach ($product in $products) {
        $archiveBaseName = "$($product.Slug)-$expectedVersion-$runtimeIdentifier"
        $archivePath = Join-Path $archiveRoot "$archiveBaseName.zip"
        Write-Host "Extracting and verifying $archiveBaseName…" -ForegroundColor Cyan
        $extractedPayload = Test-ExtractedArchive `
            -ArchivePath $archivePath `
            -ContentRoot $archiveBaseName `
            -ExtractionParent $extractionRoot
        if ($product.Slug -eq 'phoenixinspect-cli') {
            Write-Host 'Smoke-launching the extracted CLI with a 30-second bound…' -ForegroundColor Cyan
            Invoke-BoundedCliSmoke `
                -ExecutablePath (Join-Path $extractedPayload $product.Executable) `
                -ExpectedVersion $expectedVersion `
                -ScratchDirectory $extractionRoot
        }
        elseif ($product.Slug -eq 'phoenixinspect-desktop') {
            Write-Host 'Smoke-loading the extracted Desktop payload without UI, with a 30-second bound…' -ForegroundColor Cyan
            Invoke-BoundedDesktopSmoke `
                -ExecutablePath (Join-Path $extractedPayload $product.Executable) `
                -ExpectedVersion $expectedVersion `
                -ScratchDirectory $extractionRoot
        }
    }

    Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion

    $outputParent = Split-Path -Parent $outputFullPath
    $null = New-Item -ItemType Directory -Path $outputParent -Force
    $swapIdentity = [guid]::NewGuid().ToString('N')
    $stagedOutput = "$outputFullPath.stage-$swapIdentity"
    $previousOutput = "$outputFullPath.previous-$swapIdentity"
    Assert-SwapDirectoryIdentity -Path $stagedOutput -Kind stage
    Assert-SwapDirectoryIdentity -Path $previousOutput -Kind previous
    if ((Test-Path -LiteralPath $stagedOutput) -or (Test-Path -LiteralPath $previousOutput)) {
        throw 'Fresh artifact transaction paths unexpectedly already exist.'
    }
    $null = New-Item -ItemType Directory -Path $stagedOutput
    foreach ($file in Get-ChildItem -LiteralPath $archiveRoot -File | Sort-Object Name) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $stagedOutput $file.Name)
    }

    Test-ArtifactOutputDirectory -Path $stagedOutput -ExpectedNames $expectedOutputNames

    $hadPreviousOutput = Test-Path -LiteralPath $outputFullPath
    try {
        if ($hadPreviousOutput) {
            # Revalidate at the destructive boundary, not only before the long publish.
            Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion
            [System.IO.Directory]::Move($outputFullPath, $previousOutput)
            $previousOutputMoved = $true
            Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion
        }
        [System.IO.Directory]::Move($stagedOutput, $outputFullPath)
        $newOutputInstalled = $true

        # Re-read the installed files and hashes at their final path before the previous output is removed.
        Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames
        $swapCommitted = $true
    }
    catch {
        $swapFailure = $_
        $rollbackFailures = [System.Collections.Generic.List[string]]::new()
        if ($newOutputInstalled -and (Test-Path -LiteralPath $outputFullPath)) {
            try {
                if (Test-Path -LiteralPath $stagedOutput) {
                    throw "Rollback stage '$stagedOutput' unexpectedly exists."
                }
                Assert-SwapDirectoryIdentity -Path $stagedOutput -Kind stage
                Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames
                [System.IO.Directory]::Move($outputFullPath, $stagedOutput)
                $newOutputInstalled = $false
            }
            catch {
                $rollbackFailures.Add($_.Exception.Message)
            }
        }
        if ($previousOutputMoved -and (Test-Path -LiteralPath $previousOutput)) {
            try {
                if (Test-Path -LiteralPath $outputFullPath) {
                    throw "Cannot restore previous artifact output because '$outputFullPath' is occupied."
                }
                Assert-SwapDirectoryIdentity -Path $previousOutput -Kind previous
                Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion
                [System.IO.Directory]::Move($previousOutput, $outputFullPath)
                $previousOutputMoved = $false
                Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion
            }
            catch {
                $rollbackFailures.Add($_.Exception.Message)
            }
        }
        if ($rollbackFailures.Count -ne 0) {
            throw "Artifact swap failed: $($swapFailure.Exception.Message) Rollback also failed: $($rollbackFailures -join ' | ')"
        }
        throw
    }
    if ($previousOutputMoved) {
        Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion
        Remove-GuardedSwapDirectory -Path $previousOutput -Kind previous
        $previousOutputMoved = $false
    }

    # Exact output shape and SHA-256 content are postconditions, not merely staging checks.
    Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames

    Write-Host ''
    Write-Host "Unsigned local-validation artifacts (DO NOT REDISTRIBUTE): $outputFullPath" -ForegroundColor Green
    foreach ($file in Get-ChildItem -LiteralPath $outputFullPath -File | Sort-Object Name) {
        Write-Host "  $($file.Name)"
    }
    Write-Host 'No signature, SBOM, provenance, package, tag, release, or W8.10 closure is claimed.'
    Write-Host 'Generated third-party inventory and notice evidence is embedded; human legal review remains a release-closure blocker.'
    Write-Host 'The Desktop smoke covers non-UI dependency presence, selected assembly loads, and compiled XAML registration; it does not claim visible UI startup.'
    $totalStopwatch.Stop()
    Write-Host "Completed in $([math]::Round($totalStopwatch.Elapsed.TotalSeconds, 1)) s."
}
finally {
    try {
        if ($null -ne $stagedOutput -and (Test-Path -LiteralPath $stagedOutput)) {
            Remove-GuardedSwapDirectory -Path $stagedOutput -Kind stage
        }
        if ($swapCommitted -and $previousOutputMoved -and
            $null -ne $previousOutput -and (Test-Path -LiteralPath $previousOutput)) {
            Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion
            Remove-GuardedSwapDirectory -Path $previousOutput -Kind previous
            $previousOutputMoved = $false
        }
        if (Test-Path -LiteralPath $workRoot) {
            $workRootFullPath = [System.IO.Path]::TrimEndingDirectorySeparator(
                [System.IO.Path]::GetFullPath($workRoot))
            $systemTempFullPath = [System.IO.Path]::TrimEndingDirectorySeparator(
                [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()))
            $workRootParent = [System.IO.Path]::GetDirectoryName($workRootFullPath)
            $workRootLeaf = [System.IO.Path]::GetFileName($workRootFullPath)
            $workRootItem = Get-Item -LiteralPath $workRootFullPath -Force
            if (-not [string]::Equals($workRootParent, $systemTempFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
                $workRootLeaf -notmatch '^phoenixinspect-publish-[0-9a-f]{32}$' -or
                ($workRootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing recursive cleanup of unexpected work root '$workRootFullPath'."
            }

            Remove-Item -LiteralPath $workRoot -Recurse -Force
        }
    }
    finally {
        if ($outputMutexHeld) {
            $outputMutex.ReleaseMutex()
            $outputMutexHeld = $false
        }
        $outputMutex.Dispose()
    }
}
