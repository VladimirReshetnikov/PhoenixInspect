#Requires -Version 7.0
#Requires -PSEdition Core

<#
.SYNOPSIS
    Builds the bounded PhoenixInspect Windows prerelease artifact set.

.DESCRIPTION
    Locked-restores and publishes exactly the CLI and desktop applications as win-x64, self-contained Release
    directory layouts from a raw-byte-verified clean Git source state, fresh isolated SDK intermediates, and a fresh
    NuGet package root. Identical canonical local build-identity evidence and mechanically verified third-party
    dependency/notice evidence are embedded in both payloads. Each payload then receives product-specific SPDX 2.2
    inventory, a complete per-file SHA-256 manifest, and is archived with sorted entries and normalized timestamps.
    The archives are re-extracted before the
    CLI help smoke, a bounded CLI capture/open/static-field workflow against a disposable sample target, and the
    non-UI Desktop load smoke run. The output directory contains exactly two ZIPs and SHA256SUMS.txt.

    These are unsigned local-validation artifacts and must not be redistributed. This script does not create NuGet
    packages, a GitHub release, SLSA provenance, reproducibility evidence, a signature, redistribution authorization,
    legal clearance, or evidence of W8.10 release closure. The validated SBOM is inventory evidence only.

.PARAMETER OutputDirectory
    Destination for the two ZIP files and SHA256SUMS.txt. A successful run replaces this directory's contents so
    stale files cannot be mistaken for current output.

.PARAMETER SelfTest
    Exercises new/legacy archive-ownership, build-evidence, and CLI workflow-output rejection cases in disposable
    directories without restoring, publishing, staging, or replacing artifact output.

.PARAMETER SbomToolPath
    Optional path to an already-downloaded Microsoft SBOM Tool 4.1.5 win-x64 executable. The same exact size,
    SHA-256, Authenticode signer, version-resource, and command-version checks apply. When omitted, the publisher
    downloads the exact versioned policy asset into its disposable work root; it never falls back to a latest tool.

.EXAMPLE
    ./eng/Publish-PrereleaseArtifacts.ps1

.EXAMPLE
    ./eng/Publish-PrereleaseArtifacts.ps1 -OutputDirectory ./artifacts/prerelease
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $SelfTest,
    [string] $SbomToolPath
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
$sbomPolicyPath = Join-Path $PSScriptRoot 'prerelease-sbom.policy.json'
Import-Module (Join-Path $PSScriptRoot 'PrereleaseBuildEvidence.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PrereleaseSbom.psm1') -Force
. (Join-Path $PSScriptRoot 'Enable-HeadlessTestMode.ps1')
$null = Enable-HeadlessTestMode
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("phoenixinspect-publish-" + [guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $workRoot 'publish'
$archiveRoot = Join-Path $workRoot 'archives'
$noticeEvidenceRoot = Join-Path $workRoot 'third-party-notices-evidence'
$sbomToolRoot = Join-Path $workRoot 'sbom-tool'
$sbomWorkRoot = Join-Path $workRoot 'sbom-work'
$sdkArtifactsRoot = Join-Path $workRoot 'sdk-artifacts'
$nugetPackageRoot = Join-Path $workRoot 'packages'
$cliWorkflowRoot = Join-Path $workRoot 'cli-workflow-smoke'
$demoTargetArtifactsRoot = Join-Path $cliWorkflowRoot 'sdk-artifacts'
$demoTargetPublishRoot = Join-Path $cliWorkflowRoot 'target-publish'
$demoTargetScratchRoot = Join-Path $cliWorkflowRoot 'execution'
$demoTargetProject = Join-Path $repositoryRoot 'samples/Contoso.OrderService/Contoso.OrderService.csproj'
$demoTargetExecutable = Join-Path $demoTargetPublishRoot 'Contoso.OrderService.exe'

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
            if ($relativePath -cne '_manifest/spdx_2.2/manifest.spdx.json' -and
                (-not $ExcludeManifest -or $relativePath -cne 'ARTIFACT-MANIFEST.txt')) {
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
    $reservedSbomDirectory = Join-Path $PayloadDirectory '_manifest'
    if (Test-Path -LiteralPath $reservedSbomDirectory) {
        throw "The publish output unexpectedly supplied reserved SBOM directory '$reservedSbomDirectory'."
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
    $lines.Add('# Build identity: exact clean Git source and selected-input evidence included; unsigned and local only')
    $lines.Add('# SBOM: product-specific SPDX 2.2 dependency inventory and exact payload-file hashes included and validated')
    $lines.Add('# Redistribution blocked: human legal review, SLSA provenance, signatures, and W8.10 closure remain incomplete')
    $lines.Add('# Inventory only: the SBOM does not assert legal clearance or authorize redistribution')
    $lines.Add('# Not supplied: NuGet package, legal clearance, SLSA provenance, reproducibility, signature, W8.10 closure')
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
        '# Build identity: exact clean Git source and selected-input evidence included; unsigned and local only',
        '# SBOM: product-specific SPDX 2.2 dependency inventory and exact payload-file hashes included and validated',
        '# Redistribution blocked: human legal review, SLSA provenance, signatures, and W8.10 closure remain incomplete',
        '# Inventory only: the SBOM does not assert legal clearance or authorize redistribution',
        '# Not supplied: NuGet package, legal clearance, SLSA provenance, reproducibility, signature, W8.10 closure'
    )
    if ($lines.Count -lt $fixedHeaders.Count) {
        throw "Payload manifest '$ManifestPath' is missing its required header."
    }
    foreach ($headerIndex in @(0, 3, 4, 5, 6, 7, 8, 9, 10)) {
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
        [Parameter(Mandatory)][string] $Inventory,
        [Parameter(Mandatory)][string] $Version,
        [switch] $AllowLegacyPublisherFormat
    )

    Add-Type -AssemblyName System.IO.Compression
    $identityResult = $null
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

            [string[]] $sbomIdentityHeader = @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $Version",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Build identity: exact clean Git source and selected-input evidence included; unsigned and local only',
                '# SBOM: product-specific SPDX 2.2 dependency inventory and exact payload-file hashes included and validated',
                '# Redistribution blocked: human legal review, SLSA provenance, signatures, and W8.10 closure remain incomplete',
                '# Inventory only: the SBOM does not assert legal clearance or authorize redistribution',
                '# Not supplied: NuGet package, legal clearance, SLSA provenance, reproducibility, signature, W8.10 closure'
            )
            [string[]] $buildEvidenceV1IdentityHeader = @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $Version",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Build identity: exact clean Git source and selected-input evidence included; unsigned and local only',
                '# Redistribution blocked: human legal review, SBOM, SLSA provenance, and signatures are incomplete',
                '# Not supplied: NuGet package, legal clearance, SBOM, SLSA provenance, reproducibility, signature, W8.10 closure'
            )
            [string[]] $legacyIdentityHeader = @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $Version",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Redistribution blocked: human legal review, SBOM, provenance, and signatures are incomplete',
                '# Not supplied: NuGet package, legal clearance, SBOM, provenance, signature, W8.10 closure'
            )
            $isSbomFormat = $manifestText.StartsWith(
                (($sbomIdentityHeader -join "`n") + "`n"), [System.StringComparison]::Ordinal)
            $isBuildEvidenceV1Format = $manifestText.StartsWith(
                (($buildEvidenceV1IdentityHeader -join "`n") + "`n"), [System.StringComparison]::Ordinal)
            $isLegacyV0Format = $manifestText.StartsWith(
                (($legacyIdentityHeader -join "`n") + "`n"), [System.StringComparison]::Ordinal)
            if (-not $isSbomFormat -and
                (-not $AllowLegacyPublisherFormat -or
                    (-not $isBuildEvidenceV1Format -and -not $isLegacyV0Format))) {
                throw "Archive '$ArchivePath' does not carry the expected PhoenixInspect publisher identity."
            }
            [string[]] $identityHeader = if ($isSbomFormat) {
                $sbomIdentityHeader
            }
            elseif ($isBuildEvidenceV1Format) {
                $buildEvidenceV1IdentityHeader
            }
            else {
                $legacyIdentityHeader
            }
            if ($manifestText.StartsWith([char] 0xfeff) -or
                $manifestText.Contains("`r") -or
                -not $manifestText.EndsWith("`n")) {
                throw "Archive '$ArchivePath' has a non-canonical embedded publisher manifest encoding."
            }

            [string[]] $manifestLines = $manifestText.Substring(0, $manifestText.Length - 1).Split("`n")
            $sbomEntryName = "$ContentRoot/_manifest/spdx_2.2/manifest.spdx.json"
            [object[]] $payloadEntries = @($entries | Where-Object {
                $_.FullName -cne $manifestEntryName -and
                (-not $isSbomFormat -or $_.FullName -cne $sbomEntryName)
            })
            $evidenceEntryName = "$ContentRoot/BUILD-EVIDENCE.json"
            $evidenceEntry = $archive.GetEntry($evidenceEntryName)
            if ($isSbomFormat -or $isBuildEvidenceV1Format) {
                if ($null -eq $evidenceEntry -or $evidenceEntry.Length -gt 1MB) {
                    throw "Archive '$ArchivePath' has no bounded '$evidenceEntryName'."
                }
            }
            elseif ($null -ne $evidenceEntry) {
                throw "Legacy archive '$ArchivePath' unexpectedly contains BUILD-EVIDENCE.json."
            }
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

            if ($isSbomFormat -or $isBuildEvidenceV1Format) {
                $evidenceStream = $evidenceEntry.Open()
                try {
                    $memory = [System.IO.MemoryStream]::new()
                    try {
                        $evidenceStream.CopyTo($memory)
                        [byte[]] $evidenceBytes = $memory.ToArray()
                    }
                    finally {
                        $memory.Dispose()
                    }
                }
                finally {
                    $evidenceStream.Dispose()
                }
                $validatedEvidence = Test-PrereleaseBuildEvidenceBytes -Bytes $evidenceBytes
                $expectedEvidenceFormat = if ($isSbomFormat) {
                    'BuildEvidenceV2Sbom'
                }
                else {
                    'BuildEvidenceV1NoSbom'
                }
                if ($validatedEvidence.Format -cne $expectedEvidenceFormat) {
                    throw "Archive '$ArchivePath' build-evidence format '$($validatedEvidence.Format)' does not match its publisher header."
                }
                $noticeManifestEntryName = "$ContentRoot/THIRD-PARTY-NOTICES/MANIFEST.sha256"
                $noticeManifestEntry = $archive.GetEntry($noticeManifestEntryName)
                if ($null -eq $noticeManifestEntry -or $noticeManifestEntry.Length -le 0 -or
                    $noticeManifestEntry.Length -gt 1MB) {
                    throw "Archive '$ArchivePath' has no bounded '$noticeManifestEntryName'."
                }
                $noticeManifestStream = $noticeManifestEntry.Open()
                try {
                    $noticeHashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $actualNoticeManifestHash = ([System.BitConverter]::ToString(
                            $noticeHashAlgorithm.ComputeHash($noticeManifestStream))).Replace('-', '').ToLowerInvariant()
                    }
                    finally {
                        $noticeHashAlgorithm.Dispose()
                    }
                }
                finally {
                    $noticeManifestStream.Dispose()
                }
                if ($actualNoticeManifestHash -cne $validatedEvidence.ThirdPartyEvidenceManifestSha256) {
                    throw "Archive '$ArchivePath' third-party evidence manifest does not match BUILD-EVIDENCE.json."
                }
                $sbomResult = if ($isSbomFormat) {
                    Test-PrereleaseSbomArchive `
                        -Archive $archive `
                        -ContentRoot $ContentRoot `
                        -PolicyPath $sbomPolicyPath
                }
                else {
                    if (@($entries | Where-Object {
                        $_.FullName.StartsWith("$ContentRoot/_manifest/", [System.StringComparison]::Ordinal)
                    }).Count -ne 0) {
                        throw "Schema-v1 archive '$ArchivePath' unexpectedly contains a validator-owned manifest."
                    }
                    $null
                }
                if ($isSbomFormat -and
                    ($sbomResult.Product -cne $Product -or
                        $sbomResult.InventoryName -cne $Inventory -or
                        $sbomResult.Version -cne $Version -or
                        $sbomResult.SourceCommit -cne $validatedEvidence.InitialCommit -or
                        $sbomResult.SourceTree -cne $validatedEvidence.InitialTree)) {
                    throw "Archive '$ArchivePath' SBOM product/version/source does not match its publisher and build-evidence identities."
                }
                $identityResult = [pscustomobject]@{
                    Format = $expectedEvidenceFormat
                    EvidenceBytes = $evidenceBytes
                    EvidenceSha256 = $validatedEvidence.Sha256
                    SbomResult = $sbomResult
                }
            }
            else {
                if (@($entries | Where-Object {
                    $_.FullName.StartsWith("$ContentRoot/_manifest/", [System.StringComparison]::Ordinal)
                }).Count -ne 0) {
                    throw "Legacy archive '$ArchivePath' unexpectedly contains a validator-owned manifest."
                }
                $identityResult = [pscustomobject]@{
                    Format = 'LegacyV0'
                    EvidenceBytes = $null
                    EvidenceSha256 = $null
                    SbomResult = $null
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
    return $identityResult
}

function Test-ArtifactOutputDirectory {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $ExpectedNames,
        [switch] $AllowPriorVersion,
        [switch] $AllowLegacyPublisherFormat
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
    $archiveResults = [System.Collections.Generic.List[object]]::new()
    foreach ($identity in $archiveIdentities) {
        $product = if ($identity.Slug -eq 'cli') { 'PhoenixInspect CLI' } else { 'PhoenixInspect Desktop' }
        $contentRoot = [System.IO.Path]::GetFileNameWithoutExtension($identity.Name)
        $archiveResults.Add((Test-PublisherOwnedArchiveIdentity `
            -ArchivePath (Join-Path $Path $identity.Name) `
            -ContentRoot $contentRoot `
            -Product $product `
            -Inventory $identity.Slug `
            -Version $identity.Version `
            -AllowLegacyPublisherFormat:$AllowLegacyPublisherFormat))
    }
    if (@($archiveResults | Select-Object -ExpandProperty Format -Unique).Count -ne 1) {
        throw "Artifact output '$Path' mixes publisher archive formats."
    }
    if ($archiveResults[0].Format -in @('BuildEvidenceV1NoSbom', 'BuildEvidenceV2Sbom')) {
        Assert-PrereleaseBuildEvidenceIdentity `
            -First $archiveResults[0].EvidenceBytes `
            -Second $archiveResults[1].EvidenceBytes
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
        [Parameter(Mandatory)][string] $ExtractionParent,
        [Parameter(Mandatory)][byte[]] $ExpectedBuildEvidenceBytes,
        [Parameter(Mandatory)][string] $ExpectedProduct,
        [Parameter(Mandatory)][string] $ExpectedInventory,
        [Parameter(Mandatory)][string] $ExpectedVersion
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
    $evidencePath = Join-Path $payloadDirectory 'BUILD-EVIDENCE.json'
    if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
        throw "Archive '$ArchivePath' has no embedded BUILD-EVIDENCE.json."
    }
    [byte[]] $actualEvidenceBytes = [System.IO.File]::ReadAllBytes($evidencePath)
    Assert-PrereleaseBuildEvidenceIdentity `
        -First $ExpectedBuildEvidenceBytes `
        -Second $actualEvidenceBytes
    $validatedEvidence = Test-PrereleaseBuildEvidenceBytes -Bytes $actualEvidenceBytes
    if ($validatedEvidence.Format -cne 'BuildEvidenceV2Sbom') {
        throw "Archive '$ArchivePath' extracted build evidence is not the required SBOM-bound schema-v2 format."
    }
    $noticeManifestPath = Join-Path $payloadDirectory 'THIRD-PARTY-NOTICES/MANIFEST.sha256'
    if (-not (Test-Path -LiteralPath $noticeManifestPath -PathType Leaf)) {
        throw "Archive '$ArchivePath' has no embedded third-party evidence manifest."
    }
    $actualNoticeManifestHash = (Get-FileHash -LiteralPath $noticeManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualNoticeManifestHash -cne $validatedEvidence.ThirdPartyEvidenceManifestSha256) {
        throw "Archive '$ArchivePath' extracted third-party evidence manifest does not match BUILD-EVIDENCE.json."
    }
    $sbomResult = Test-PrereleaseSbomPayload `
        -PayloadDirectory $payloadDirectory `
        -PolicyPath $sbomPolicyPath
    if ($sbomResult.Product -cne $ExpectedProduct -or
        $sbomResult.InventoryName -cne $ExpectedInventory -or
        $sbomResult.Version -cne $ExpectedVersion -or
        $sbomResult.SourceCommit -cne $validatedEvidence.InitialCommit -or
        $sbomResult.SourceTree -cne $validatedEvidence.InitialTree) {
        throw "Archive '$ArchivePath' extracted SBOM product/version/source does not match its publisher and build-evidence identities."
    }
    return $payloadDirectory
}

function New-BoundedProcessStartInfo {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Arguments,
        [Parameter(Mandatory)][string] $WorkingDirectory
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.ErrorDialog = $false
    $startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $startInfo.StandardOutputEncoding = $strictUtf8
    $startInfo.StandardErrorEncoding = $strictUtf8
    $startInfo.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }
    return $startInfo
}

function New-PublisherOperationFailure {
    param(
        [AllowNull()][System.Exception] $PrimaryFailure,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]] $CleanupFailures,
        [Parameter(Mandatory)][string] $Description
    )

    if ($null -eq $PrimaryFailure -and $CleanupFailures.Count -eq 0) {
        return $null
    }
    if ($null -ne $PrimaryFailure -and $CleanupFailures.Count -eq 0) {
        return $PrimaryFailure
    }
    if ($null -eq $PrimaryFailure -and $CleanupFailures.Count -eq 1) {
        return $CleanupFailures[0].Failure
    }

    $failures = [System.Collections.Generic.List[System.Exception]]::new()
    if ($null -ne $PrimaryFailure) {
        $failures.Add($PrimaryFailure)
    }
    foreach ($cleanupFailure in $CleanupFailures) {
        $failures.Add([System.InvalidOperationException]::new(
            "Cleanup failure ($($cleanupFailure.Name)): $($cleanupFailure.Failure.Message)",
            $cleanupFailure.Failure))
    }
    return [System.AggregateException]::new("$Description failed.", $failures.ToArray())
}

function Complete-PublisherOperation {
    param(
        [AllowNull()][System.Exception] $PrimaryFailure,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]] $CleanupFailures,
        [Parameter(Mandatory)][string] $Description
    )

    $failure = New-PublisherOperationFailure `
        -PrimaryFailure $PrimaryFailure `
        -CleanupFailures $CleanupFailures `
        -Description $Description
    if ($null -ne $failure) {
        throw $failure
    }
}

function Stop-PublisherOwnedProcess {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process] $Process,
        [Parameter(Mandatory)][string] $Description
    )

    if (-not $Process.HasExited) {
        try {
            $Process.Kill($true)
        }
        catch {
            if (-not $Process.HasExited) {
                throw "Failed to kill the complete process tree for ${Description}: $($_.Exception.Message)"
            }
        }
    }
    if (-not $Process.WaitForExit(5000)) {
        throw "The complete process tree for $Description did not terminate within the 5-second cleanup bound."
    }
}

function Wait-PublisherTextTasks {
    param(
        [Parameter(Mandatory)][System.Threading.Tasks.Task[]] $Tasks,
        [Parameter(Mandatory)][ValidateRange(0, 60000)][int] $TimeoutMilliseconds,
        [Parameter(Mandatory)][string] $Description
    )

    if (-not [System.Threading.Tasks.Task]::WaitAll($Tasks, $TimeoutMilliseconds)) {
        throw "$Description did not close its redirected output streams within the wall-clock bound."
    }
}

function Invoke-BoundedCapturedProcess {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $WorkingDirectory,
        [Parameter(Mandatory)][ValidateRange(1, 60)][int] $TimeoutSeconds,
        [Parameter(Mandatory)][string] $Description
    )

    $startInfo = New-BoundedProcessStartInfo `
        -FilePath $FilePath `
        -Arguments $Arguments `
        -WorkingDirectory $WorkingDirectory
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $stdoutTask = $null
    $stderrTask = $null
    $result = $null
    $primaryFailure = $null
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) {
            throw "Failed to start $Description."
        }
        $started = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timeoutMilliseconds = $TimeoutSeconds * 1000
        if (-not $process.WaitForExit($timeoutMilliseconds)) {
            throw "$Description exceeded its $TimeoutSeconds-second wall-clock bound."
        }

        $remainingMilliseconds = $timeoutMilliseconds - [int][math]::Ceiling($stopwatch.Elapsed.TotalMilliseconds)
        if ($remainingMilliseconds -lt 0) {
            $remainingMilliseconds = 0
        }
        Wait-PublisherTextTasks `
            -Tasks ([System.Threading.Tasks.Task[]]@($stdoutTask, $stderrTask)) `
            -TimeoutMilliseconds $remainingMilliseconds `
            -Description $Description
        $result = [pscustomobject]@{
            ExitCode = $process.ExitCode
            StandardOutput = $stdoutTask.GetAwaiter().GetResult()
            StandardError = $stderrTask.GetAwaiter().GetResult()
        }
    }
    catch {
        $primaryFailure = $_.Exception
    }
    finally {
        $cleanupFailures = [System.Collections.Generic.List[object]]::new()
        if ($started) {
            try {
                if (-not $process.HasExited) {
                    Stop-PublisherOwnedProcess -Process $process -Description $Description
                }
            }
            catch {
                $cleanupFailures.Add([pscustomobject]@{
                    Name = 'process-tree termination'
                    Failure = $_.Exception
                })
            }
            if ($null -ne $stdoutTask -and $null -ne $stderrTask) {
                try {
                    Wait-PublisherTextTasks `
                        -Tasks ([System.Threading.Tasks.Task[]]@($stdoutTask, $stderrTask)) `
                        -TimeoutMilliseconds 5000 `
                        -Description "$Description cleanup"
                }
                catch {
                    $cleanupFailures.Add([pscustomobject]@{
                        Name = 'redirected-stream drain'
                        Failure = $_.Exception
                    })
                }
            }
        }
        try {
            $process.Dispose()
        }
        catch {
            $cleanupFailures.Add([pscustomobject]@{
                Name = 'process disposal'
                Failure = $_.Exception
            })
        }
        Complete-PublisherOperation `
            -PrimaryFailure $primaryFailure `
            -CleanupFailures $cleanupFailures.ToArray() `
            -Description $Description
    }
    return $result
}

function Start-BoundedDemoTarget {
    param([Parameter(Mandatory)][string] $ExecutablePath)

    $startInfo = New-BoundedProcessStartInfo `
        -FilePath $ExecutablePath `
        -Arguments @() `
        -WorkingDirectory (Split-Path -Parent $ExecutablePath)
    $startInfo.Environment['DOTNET_EnableDiagnostics'] = '1'
    $startInfo.Environment['COMPlus_EnableDiagnostics'] = '1'
    $startInfo.Environment['DOTNET_EnableDiagnostics_IPC'] = '1'
    $null = $startInfo.Environment.Remove('DOTNET_DiagnosticPorts')
    $null = $startInfo.Environment.Remove('DOTNET_DefaultDiagnosticPortSuspend')

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $ownershipTransferred = $false
    $result = $null
    $primaryFailure = $null
    try {
        if (-not $process.Start()) {
            throw 'Failed to start the disposable Contoso.OrderService smoke target.'
        }
        $started = $true
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $readyTask = $process.StandardOutput.ReadLineAsync()
        if (-not $readyTask.Wait(30000)) {
            throw 'The disposable Contoso.OrderService smoke target did not report READY within 30 seconds.'
        }
        $readyLine = $readyTask.GetAwaiter().GetResult()
        if ($readyLine -cne 'READY') {
            throw "The disposable Contoso.OrderService smoke target reported '$readyLine' instead of exact READY."
        }

        $remainingStdoutTask = $process.StandardOutput.ReadToEndAsync()
        $result = [pscustomobject]@{
            Process = $process
            RemainingStandardOutputTask = $remainingStdoutTask
            StandardErrorTask = $stderrTask
        }
        $ownershipTransferred = $true
    }
    catch {
        $primaryFailure = $_.Exception
    }
    finally {
        if (-not $ownershipTransferred) {
            $cleanupFailures = [System.Collections.Generic.List[object]]::new()
            if ($started) {
                try {
                    Stop-PublisherOwnedProcess `
                        -Process $process `
                        -Description 'the disposable Contoso.OrderService smoke target'
                }
                catch {
                    $cleanupFailures.Add([pscustomobject]@{
                        Name = 'process-tree termination'
                        Failure = $_.Exception
                    })
                }
            }
            try {
                $process.Dispose()
            }
            catch {
                $cleanupFailures.Add([pscustomobject]@{
                    Name = 'process disposal'
                    Failure = $_.Exception
                })
            }
            Complete-PublisherOperation `
                -PrimaryFailure $primaryFailure `
                -CleanupFailures $cleanupFailures.ToArray() `
                -Description 'The disposable Contoso.OrderService smoke target startup'
        }
    }
    return $result
}

function Stop-BoundedDemoTarget {
    param([Parameter(Mandatory)][pscustomobject] $Target)

    $process = $Target.Process
    $processId = $process.Id
    $result = $null
    $primaryFailure = $null
    try {
        Stop-PublisherOwnedProcess `
            -Process $process `
            -Description 'the disposable Contoso.OrderService smoke target'
        Wait-PublisherTextTasks `
            -Tasks ([System.Threading.Tasks.Task[]]@(
                $Target.RemainingStandardOutputTask,
                $Target.StandardErrorTask)) `
            -TimeoutMilliseconds 5000 `
            -Description 'The disposable Contoso.OrderService smoke target cleanup'
        $remainingOutput = $Target.RemainingStandardOutputTask.GetAwaiter().GetResult()
        $errorOutput = $Target.StandardErrorTask.GetAwaiter().GetResult()
        $result = [pscustomobject]@{
            ProcessId = $processId
            StandardOutput = $remainingOutput
            StandardError = $errorOutput
        }
    }
    catch {
        $primaryFailure = $_.Exception
    }
    finally {
        $cleanupFailures = [System.Collections.Generic.List[object]]::new()
        try {
            $process.Dispose()
        }
        catch {
            $cleanupFailures.Add([pscustomobject]@{
                Name = 'target process disposal'
                Failure = $_.Exception
            })
        }
        Complete-PublisherOperation `
            -PrimaryFailure $primaryFailure `
            -CleanupFailures $cleanupFailures.ToArray() `
            -Description 'The disposable Contoso.OrderService smoke target cleanup'
    }
    return $result
}

function Assert-BoundedDemoTargetPostReadyOutput {
    param(
        [Parameter(Mandatory)][pscustomobject] $StoppedTarget,
        [Parameter(Mandatory)][string] $ExpectedDumpPath
    )

    if (-not [string]::IsNullOrEmpty($StoppedTarget.StandardError)) {
        throw 'The disposable Contoso.OrderService smoke target wrote an unexpected diagnostic to standard error.'
    }

    $remainingOutput = [string] $StoppedTarget.StandardOutput
    if ($remainingOutput.Length -gt 4096) {
        throw 'The disposable Contoso.OrderService smoke target wrote more than 4 KiB after exact READY.'
    }

    $expectedStart = '[createdump] Writing full dump for process {0} to file {1}' -f `
        $StoppedTarget.ProcessId,
        [System.IO.Path]::GetFullPath($ExpectedDumpPath)
    $expectedPattern = '\A{0}\r?\n\[createdump\] Dump successfully written in [0-9]+ms(?:\r?\n)?\z' -f `
        [System.Text.RegularExpressions.Regex]::Escape($expectedStart)
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch(
            $remainingOutput,
            $expectedPattern,
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw 'The disposable Contoso.OrderService smoke target did not write the exact PID/path-bound CoreCLR createdump start/success diagnostics after READY.'
    }
}

function Remove-BoundedSmokeFile {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $ExpectedParent
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $expectedParentFullPath = [System.IO.Path]::TrimEndingDirectorySeparator(
        [System.IO.Path]::GetFullPath($ExpectedParent))
    if (-not [string]::Equals(
            [System.IO.Path]::GetDirectoryName($fullPath),
            $expectedParentFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup of smoke file outside its exact scratch directory: '$fullPath'."
    }

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        if (-not (Test-Path -LiteralPath $fullPath)) {
            return
        }
        $item = Get-Item -LiteralPath $fullPath -Force
        if ($item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing cleanup of unexpected smoke item '$fullPath'."
        }
        try {
            Remove-Item -LiteralPath $fullPath -Force
        }
        catch {
            if ($attempt -eq 20) {
                throw "Could not delete the temporary CLI workflow dump within the 5-second lock-cleanup bound: $($_.Exception.Message)"
            }
        }
        if (-not (Test-Path -LiteralPath $fullPath)) {
            return
        }
        if ($attempt -lt 20) {
            Start-Sleep -Milliseconds 250
        }
    }
    throw "Could not delete the temporary CLI workflow dump within the 5-second lock-cleanup bound: '$fullPath' remains."
}

function Assert-CliWorkflowEvaluationOutput {
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Output)

    $contracts = @(
        [pscustomobject]@{
            Name = 'the exact fully-qualified expression headline'
            Pattern = '^[ \t]*\[exact\][ \t]+Contoso\.OrderService\.Diagnostics\.ServiceState\.BuildLabel[ \t]*\r?$'
        },
        [pscustomobject]@{
            Name = 'the exact string value'
            Pattern = '^[ \t]*=[ \t]+"2026\.07\.30-preview"[ \t]+\[String \(length 18\)\][ \t]*\r?$'
        },
        [pscustomobject]@{
            Name = 'the Exact Complete status'
            Pattern = '^[ \t]*status[ \t]+Exact[ \t]+·[ \t]+Complete[ \t]*\r?$'
        },
        [pscustomobject]@{
            Name = 'the one-expression summary'
            Pattern = '^[ \t]*Expressions evaluated[ \t]+1[ \t]*\r?$'
        },
        [pscustomobject]@{
            Name = 'the zero-nonexact summary'
            Pattern = '^[ \t]*Answers that were not exact or exhaustively absent[ \t]+0[ \t]*\r?$'
        })
    foreach ($contract in $contracts) {
        $matches = [System.Text.RegularExpressions.Regex]::Matches(
            $Output,
            $contract.Pattern,
            [System.Text.RegularExpressions.RegexOptions]::Multiline)
        if ($matches.Count -ne 1) {
            throw "The extracted CLI workflow did not report exactly one line for $($contract.Name)."
        }
    }
}

function Invoke-BoundedCliWorkflowSmoke {
    param(
        [Parameter(Mandatory)][string] $ExecutablePath,
        [Parameter(Mandatory)][string] $TargetExecutablePath,
        [Parameter(Mandatory)][string] $ScratchDirectory
    )

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "The extracted CLI workflow executable '$ExecutablePath' is missing."
    }
    if (-not (Test-Path -LiteralPath $TargetExecutablePath -PathType Leaf)) {
        throw "The disposable CLI workflow target '$TargetExecutablePath' is missing."
    }
    if (Test-Path -LiteralPath $ScratchDirectory) {
        throw "The CLI workflow scratch directory '$ScratchDirectory' unexpectedly already exists."
    }
    $null = New-Item -ItemType Directory -Path $ScratchDirectory
    $dumpPath = Join-Path $ScratchDirectory 'Contoso.OrderService.dmp'
    $target = $null
    $primaryFailure = $null
    try {
        $target = Start-BoundedDemoTarget -ExecutablePath $TargetExecutablePath
        $captureFailure = $null
        try {
            $capture = Invoke-BoundedCapturedProcess `
                -FilePath $ExecutablePath `
                -Arguments @('capture', '--pid', $target.Process.Id.ToString(), '--output', $dumpPath) `
                -WorkingDirectory (Split-Path -Parent $ExecutablePath) `
                -TimeoutSeconds 60 `
                -Description 'The extracted CLI capture smoke'
            if ($capture.ExitCode -ne 0) {
                throw "The extracted CLI capture smoke failed with exit code $($capture.ExitCode)."
            }
            if (-not [string]::IsNullOrEmpty($capture.StandardError)) {
                throw 'The extracted CLI capture smoke wrote an unexpected diagnostic to standard error.'
            }
            if (-not (Test-Path -LiteralPath $dumpPath -PathType Leaf)) {
                throw 'The extracted CLI capture smoke did not create its requested dump.'
            }
            $dumpItem = Get-Item -LiteralPath $dumpPath -Force
            if (($dumpItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                $dumpItem.Length -le 0) {
                throw 'The extracted CLI capture smoke did not create a nonempty regular dump file.'
            }
        }
        catch {
            $captureFailure = $_.Exception
        }

        $targetCleanupFailures = [System.Collections.Generic.List[object]]::new()
        $stoppedTarget = $null
        try {
            $ownedTarget = $target
            $target = $null
            $stoppedTarget = Stop-BoundedDemoTarget -Target $ownedTarget
        }
        catch {
            $targetCleanupFailures.Add([pscustomobject]@{
                Name = 'target termination and stream drain'
                Failure = $_.Exception
            })
        }
        Complete-PublisherOperation `
            -PrimaryFailure $captureFailure `
            -CleanupFailures $targetCleanupFailures.ToArray() `
            -Description 'The extracted CLI capture smoke'
        Assert-BoundedDemoTargetPostReadyOutput `
            -StoppedTarget $stoppedTarget `
            -ExpectedDumpPath $dumpPath

        $evaluation = Invoke-BoundedCapturedProcess `
            -FilePath $ExecutablePath `
            -Arguments @(
                $dumpPath,
                '--eval', 'Contoso.OrderService.Diagnostics.ServiceState.BuildLabel',
                '--no-color') `
            -WorkingDirectory (Split-Path -Parent $ExecutablePath) `
            -TimeoutSeconds 60 `
            -Description 'The extracted CLI dump evaluation smoke'
        if ($evaluation.ExitCode -ne 0) {
            throw "The extracted CLI dump evaluation smoke failed with exit code $($evaluation.ExitCode)."
        }
        if (-not [string]::IsNullOrEmpty($evaluation.StandardError)) {
            throw 'The extracted CLI dump evaluation smoke wrote an unexpected diagnostic to standard error.'
        }
        Assert-CliWorkflowEvaluationOutput -Output $evaluation.StandardOutput
    }
    catch {
        $primaryFailure = $_.Exception
    }
    finally {
        $cleanupFailures = [System.Collections.Generic.List[object]]::new()
        if ($null -ne $target) {
            try {
                $ownedTarget = $target
                $target = $null
                $null = Stop-BoundedDemoTarget -Target $ownedTarget
            }
            catch {
                $cleanupFailures.Add([pscustomobject]@{
                    Name = 'fallback target termination and stream drain'
                    Failure = $_.Exception
                })
            }
        }
        try {
            Remove-BoundedSmokeFile -Path $dumpPath -ExpectedParent $ScratchDirectory
        }
        catch {
            $cleanupFailures.Add([pscustomobject]@{
                Name = 'temporary dump removal'
                Failure = $_.Exception
            })
        }
        Complete-PublisherOperation `
            -PrimaryFailure $primaryFailure `
            -CleanupFailures $cleanupFailures.ToArray() `
            -Description 'The extracted CLI workflow smoke'
    }
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

function Invoke-PublisherArchiveContractSelfTest {
    function Assert-SelfTestThrows {
        param([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $Name)
        try {
            & $Action
        }
        catch {
            return
        }
        throw "Publisher archive self-test '$Name' unexpectedly succeeded."
    }

    function New-SelfTestEvidenceBytes {
        param([Parameter(Mandatory)][string] $ThirdPartyEvidenceManifestSha256)

        $source = [pscustomobject]@{ ObjectFormat = 'sha1'; Commit = '1' * 40; Tree = '2' * 40 }
        $contract = [pscustomobject]@{
            ConfiguredSdkMinimum = '10.0.400'
            RollForward = 'latestPatch'
            AllowPrerelease = $false
            TargetFramework = 'net10.0'
            RepositoryUrl = 'https://github.com/VladimirReshetnikov/PhoenixInspect'
        }
        $inputs = @(
            foreach ($path in Get-PrereleaseSelectedInputPaths) {
                [ordered]@{ path = $path; sha256 = '0' * 64 }
            }
        )
        return ,(New-PrereleaseBuildEvidenceBytes `
            -InitialSource $source `
            -FinalSource $source `
            -RepositoryContract $contract `
            -SelectedSdk '10.0.401' `
            -RuntimePacks @([ordered]@{
                id = 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64'
                version = '10.0.11'
            }) `
            -ThirdPartyEvidenceManifestSha256 $ThirdPartyEvidenceManifestSha256 `
            -Sbom (Get-PrereleaseSbomBuildEvidenceDescriptor -PolicyPath $sbomPolicyPath) `
            -SelectedInputs $inputs)
    }

    function ConvertTo-SelfTestBuildEvidenceV1Bytes {
        param([Parameter(Mandatory)][byte[]] $BuildEvidenceV2Bytes)

        $evidence = [System.Text.Encoding]::UTF8.GetString($BuildEvidenceV2Bytes) |
            ConvertFrom-Json -AsHashtable -Depth 32
        $evidence.schema = 'phoenixinspect.local-prerelease-build-evidence/v1'
        $null = $evidence.build.Remove('sbom')
        $v1Paths = [System.Collections.Generic.HashSet[string]]::new(
            [string[]] (Get-PrereleaseSelectedInputPaths -Schema $evidence.schema),
            [System.StringComparer]::Ordinal)
        $evidence.selectedInputs = @($evidence.selectedInputs | Where-Object {
            $v1Paths.Contains([string] $_.path)
        })
        $text = (ConvertTo-Json -InputObject $evidence -Depth 32).Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
        [byte[]] $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($text)
        $validated = Test-PrereleaseBuildEvidenceBytes -Bytes $bytes
        if ($validated.Format -cne 'BuildEvidenceV1NoSbom') {
            throw 'Publisher self-test could not synthesize schema-v1 migration evidence.'
        }
        return ,$bytes
    }

    function Write-SelfTestPayloadManifest {
        param(
            [Parameter(Mandatory)][string] $PayloadDirectory,
            [Parameter(Mandatory)][string] $Product,
            [Parameter(Mandatory)][ValidateSet('BuildEvidenceV1NoSbom','LegacyV0')][string] $Format
        )

        [string[]] $headers = if ($Format -ceq 'BuildEvidenceV1NoSbom') {
            @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $expectedVersion",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Build identity: exact clean Git source and selected-input evidence included; unsigned and local only',
                '# Redistribution blocked: human legal review, SBOM, SLSA provenance, and signatures are incomplete',
                '# Not supplied: NuGet package, legal clearance, SBOM, SLSA provenance, reproducibility, signature, W8.10 closure'
            )
        }
        else {
            @(
                '# PhoenixInspect prerelease payload manifest',
                "# Product: $Product",
                "# Version: $expectedVersion",
                "# Runtime: $runtimeIdentifier",
                '# Payload: unsigned, self-contained preview application for local validation',
                '# Third-party evidence: generated inventory and hash-pinned license/notice materials included',
                '# Redistribution blocked: human legal review, SBOM, provenance, and signatures are incomplete',
                '# Not supplied: NuGet package, legal clearance, SBOM, provenance, signature, W8.10 closure'
            )
        }
        $lines = [System.Collections.Generic.List[string]]::new()
        $lines.AddRange($headers)
        foreach ($relativePath in Get-CanonicalPayloadRelativePaths $PayloadDirectory) {
            $hash = (Get-FileHash -LiteralPath (Resolve-ContainedRelativePath `
                -RootDirectory $PayloadDirectory `
                -RelativePath $relativePath `
                -Context 'Self-test payload') -Algorithm SHA256).Hash.ToLowerInvariant()
            $lines.Add("$hash *$relativePath")
        }
        $manifestPath = Join-Path $PayloadDirectory 'ARTIFACT-MANIFEST.txt'
        Write-Utf8Lines -Path $manifestPath -Lines $lines.ToArray()
        return $manifestPath
    }

    function New-SelfTestArtifactOutput {
        param(
            [Parameter(Mandatory)][string] $Path,
            [Parameter(Mandatory)][ValidateSet(
                'New', 'BuildEvidenceV1', 'LegacyV0', 'MissingEvidence', 'InvalidEvidence',
                'MismatchedNoticeEvidence', 'MissingSbom', 'InvalidSbom', 'UnexpectedSidecar')][string] $Format,
            [Parameter(Mandatory)][byte[]] $EvidenceBytes,
            [Parameter(Mandatory)][byte[]] $V1EvidenceBytes,
            [Parameter(Mandatory)][byte[]] $NoticeManifestBytes
        )

        $payloadRoot = Join-Path $Path 'payloads'
        $archiveDirectory = Join-Path $Path 'output'
        $null = New-Item -ItemType Directory -Path $payloadRoot -Force
        $null = New-Item -ItemType Directory -Path $archiveDirectory -Force
        foreach ($fixture in @(
            [pscustomobject]@{ Slug = 'phoenixinspect-cli'; Product = 'PhoenixInspect CLI' },
            [pscustomobject]@{ Slug = 'phoenixinspect-desktop'; Product = 'PhoenixInspect Desktop' })) {
            $payload = Join-Path $payloadRoot $fixture.Slug
            $null = New-Item -ItemType Directory -Path $payload
            Write-Utf8Lines -Path (Join-Path $payload 'fixture.txt') -Lines @('publisher self-test')
            if ($Format -eq 'BuildEvidenceV1') {
                $noticeDirectory = Join-Path $payload 'THIRD-PARTY-NOTICES'
                $null = New-Item -ItemType Directory -Path $noticeDirectory
                [System.IO.File]::WriteAllBytes(
                    (Join-Path $noticeDirectory 'MANIFEST.sha256'),
                    $NoticeManifestBytes)
                Write-PrereleaseBuildEvidence `
                    -Path (Join-Path $payload 'BUILD-EVIDENCE.json') `
                    -Bytes $V1EvidenceBytes
                $null = Write-SelfTestPayloadManifest `
                    -PayloadDirectory $payload `
                    -Product $fixture.Product `
                    -Format BuildEvidenceV1NoSbom
            }
            elseif ($Format -eq 'LegacyV0') {
                $null = Write-SelfTestPayloadManifest `
                    -PayloadDirectory $payload `
                    -Product $fixture.Product `
                    -Format LegacyV0
            }
            else {
                $inventoryName = if ($fixture.Slug -ceq 'phoenixinspect-cli') { 'cli' } else { 'desktop' }
                $synthetic = Initialize-PrereleaseSbomSyntheticFixture `
                    -PayloadDirectory $payload `
                    -ProductName $fixture.Product `
                    -InventoryName $inventoryName `
                    -Version $expectedVersion `
                    -PolicyPath $sbomPolicyPath
                [byte[]] $embeddedNoticeBytes = if ($Format -eq 'MismatchedNoticeEvidence') {
                    [System.Text.UTF8Encoding]::new($false).GetBytes("mismatched publisher self-test notice manifest`n")
                }
                else {
                    $NoticeManifestBytes
                }
                [System.IO.File]::WriteAllBytes(
                    (Join-Path $payload 'THIRD-PARTY-NOTICES/MANIFEST.sha256'),
                    $embeddedNoticeBytes)
                if ($Format -ne 'MissingEvidence') {
                    [byte[]] $embeddedEvidenceBytes = if ($Format -eq 'InvalidEvidence') {
                        [System.Text.Encoding]::UTF8.GetBytes(
                            [System.Text.Encoding]::UTF8.GetString($EvidenceBytes).Replace(
                                '"localOnly": true', '"localOnly": false'))
                    }
                    else {
                        $EvidenceBytes
                    }
                    Write-PrereleaseBuildEvidence `
                        -Path (Join-Path $payload 'BUILD-EVIDENCE.json') `
                        -Bytes $embeddedEvidenceBytes
                }
                $null = Write-PayloadManifest `
                    -PayloadDirectory $payload `
                    -Product $fixture.Product `
                    -Version $expectedVersion
                if ($Format -ne 'MissingSbom') {
                    $null = Complete-PrereleaseSbomSyntheticFixture `
                        -PayloadDirectory $payload `
                        -ExpectedEvidenceBytes $synthetic.EvidenceBytes `
                        -PolicyPath $sbomPolicyPath
                }
                if ($Format -eq 'InvalidSbom') {
                    $sbomPath = Join-Path $payload '_manifest/spdx_2.2/manifest.spdx.json'
                    [byte[]] $sbomBytes = [System.IO.File]::ReadAllBytes($sbomPath)
                    $sbomBytes[$sbomBytes.Length - 2] = [byte] 0x20
                    [System.IO.File]::WriteAllBytes($sbomPath, $sbomBytes)
                }
                elseif ($Format -eq 'UnexpectedSidecar') {
                    Write-Utf8Lines `
                        -Path (Join-Path $payload '_manifest/spdx_2.2/manifest.spdx.json.sha256') `
                        -Lines @('0' * 64)
                }
            }

            $archiveBaseName = "$($fixture.Slug)-$expectedVersion-$runtimeIdentifier"
            New-NormalizedZip `
                -PayloadDirectory $payload `
                -ArchivePath (Join-Path $archiveDirectory "$archiveBaseName.zip") `
                -ContentRoot $archiveBaseName
        }
        $checksumLines = @(
            Get-ChildItem -LiteralPath $archiveDirectory -File -Filter '*.zip' |
                Sort-Object Name |
                ForEach-Object {
                    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    "$hash *$($_.Name)"
                }
        )
        Write-Utf8Lines -Path (Join-Path $archiveDirectory 'SHA256SUMS.txt') -Lines $checksumLines
        return $archiveDirectory
    }

    $selfTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
        'phoenixinspect-publisher-selftest-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $selfTestRoot
    try {
        [byte[]] $noticeManifestBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(
            "publisher self-test notice manifest`n")
        $noticeHashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $noticeManifestHash = ([System.BitConverter]::ToString(
                $noticeHashAlgorithm.ComputeHash($noticeManifestBytes))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $noticeHashAlgorithm.Dispose()
        }
        [byte[]] $evidenceBytes = New-SelfTestEvidenceBytes `
            -ThirdPartyEvidenceManifestSha256 $noticeManifestHash
        [byte[]] $v1EvidenceBytes = ConvertTo-SelfTestBuildEvidenceV1Bytes `
            -BuildEvidenceV2Bytes $evidenceBytes

        $newOutput = New-SelfTestArtifactOutput `
            -Path (Join-Path $selfTestRoot 'new') `
            -Format New `
            -EvidenceBytes $evidenceBytes `
            -V1EvidenceBytes $v1EvidenceBytes `
            -NoticeManifestBytes $noticeManifestBytes
        Test-ArtifactOutputDirectory -Path $newOutput -ExpectedNames $expectedOutputNames

        $v1Output = New-SelfTestArtifactOutput `
            -Path (Join-Path $selfTestRoot 'build-evidence-v1') `
            -Format BuildEvidenceV1 `
            -EvidenceBytes $evidenceBytes `
            -V1EvidenceBytes $v1EvidenceBytes `
            -NoticeManifestBytes $noticeManifestBytes
        Assert-SelfTestThrows {
            Test-ArtifactOutputDirectory `
                -Path $v1Output `
                -ExpectedNames $expectedOutputNames `
                -AllowPriorVersion
        } 'schema-v1 pre-SBOM output rejected as new output'
        Test-ArtifactOutputDirectory `
            -Path $v1Output `
            -ExpectedNames $expectedOutputNames `
            -AllowPriorVersion `
            -AllowLegacyPublisherFormat

        $legacyOutput = New-SelfTestArtifactOutput `
            -Path (Join-Path $selfTestRoot 'legacy') `
            -Format LegacyV0 `
            -EvidenceBytes $evidenceBytes `
            -V1EvidenceBytes $v1EvidenceBytes `
            -NoticeManifestBytes $noticeManifestBytes
        Assert-SelfTestThrows {
            Test-ArtifactOutputDirectory `
                -Path $legacyOutput `
                -ExpectedNames $expectedOutputNames `
                -AllowPriorVersion
        } 'legacy rejected as new output'
        Test-ArtifactOutputDirectory `
            -Path $legacyOutput `
            -ExpectedNames $expectedOutputNames `
            -AllowPriorVersion `
            -AllowLegacyPublisherFormat

        foreach ($invalidFormat in @(
            'MissingEvidence', 'InvalidEvidence', 'MismatchedNoticeEvidence',
            'MissingSbom', 'InvalidSbom', 'UnexpectedSidecar')) {
            $invalidOutput = New-SelfTestArtifactOutput `
                -Path (Join-Path $selfTestRoot $invalidFormat) `
                -Format $invalidFormat `
                -EvidenceBytes $evidenceBytes `
                -V1EvidenceBytes $v1EvidenceBytes `
                -NoticeManifestBytes $noticeManifestBytes
            Assert-SelfTestThrows {
                Test-ArtifactOutputDirectory -Path $invalidOutput -ExpectedNames $expectedOutputNames
            } "$invalidFormat rejected"
        }

        $validEvaluationOutput = @'
PhoenixInspect nondeterministic overview is deliberately ignored by this contract.

  [exact]    Contoso.OrderService.Diagnostics.ServiceState.BuildLabel
    =   "2026.07.30-preview"   [String (length 18)]
    status  Exact  ·  Complete
    via     nondeterministic address and duration

Session summary
  Expressions evaluated     1
  Answers that were not exact or exhaustively absent  0
'@
        Assert-CliWorkflowEvaluationOutput -Output $validEvaluationOutput
        foreach ($invalidEvaluation in @(
            [pscustomobject]@{
                Name = 'wrong workflow value rejected'
                Output = $validEvaluationOutput.Replace('"2026.07.30-preview"', '"wrong"')
            },
            [pscustomobject]@{
                Name = 'non-complete workflow result rejected'
                Output = $validEvaluationOutput.Replace('Exact  ·  Complete', 'Exact  ·  Storage')
            },
            [pscustomobject]@{
                Name = 'wrong workflow expression count rejected'
                Output = $validEvaluationOutput.Replace('Expressions evaluated     1', 'Expressions evaluated     2')
            },
            [pscustomobject]@{
                Name = 'nonexact workflow answer rejected'
                Output = $validEvaluationOutput.Replace('exhaustively absent  0', 'exhaustively absent  1')
            },
            [pscustomobject]@{
                Name = 'duplicate workflow result rejected'
                Output = $validEvaluationOutput + "`n  [exact]    Contoso.OrderService.Diagnostics.ServiceState.BuildLabel"
            })) {
            Assert-SelfTestThrows {
                Assert-CliWorkflowEvaluationOutput -Output $invalidEvaluation.Output
            } $invalidEvaluation.Name
        }

        $selfTestDumpPath = Join-Path $selfTestRoot 'workflow-smoke/Contoso.OrderService.dmp'
        $selfTestProcessId = 12345
        $validTargetOutput = @(
            '[createdump] Writing full dump for process {0} to file {1}' -f `
                $selfTestProcessId,
                [System.IO.Path]::GetFullPath($selfTestDumpPath)
            '[createdump] Dump successfully written in 435ms'
        ) -join "`n"
        $validStoppedTarget = [pscustomobject]@{
            ProcessId = $selfTestProcessId
            StandardOutput = $validTargetOutput + "`n"
            StandardError = ''
        }
        foreach ($validTranscript in @(
            $validTargetOutput,
            ($validTargetOutput + "`n"),
            $validTargetOutput.Replace("`n", "`r`n"),
            ($validTargetOutput.Replace("`n", "`r`n") + "`r`n"))) {
            Assert-BoundedDemoTargetPostReadyOutput `
                -StoppedTarget ([pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTranscript
                    StandardError = ''
                }) `
                -ExpectedDumpPath $selfTestDumpPath
        }

        foreach ($invalidTargetOutput in @(
            [pscustomobject]@{
                Name = 'wrong createdump PID rejected'
                Target = [pscustomobject]@{
                    ProcessId = 54321
                    StandardOutput = $validStoppedTarget.StandardOutput
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'wrong createdump path rejected'
                Target = $validStoppedTarget
                DumpPath = Join-Path $selfTestRoot 'workflow-smoke/other.dmp'
            },
            [pscustomobject]@{
                Name = 'missing createdump completion rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput.Split("`n")[0]
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'empty target output rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = ''
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'createdump success without start rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = "[createdump] Dump successfully written in 435ms`n"
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'reversed createdump diagnostics rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput.Split("`n")[1] + "`n" + $validTargetOutput.Split("`n")[0]
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'duplicate createdump diagnostic rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput + "`n" + $validTargetOutput.Split("`n")[1]
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'blank line before createdump diagnostics rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = "`n" + $validTargetOutput
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'blank line between createdump diagnostics rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput.Replace("`n", "`n`n")
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'extra blank line after createdump diagnostics rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput + "`n`n"
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'malformed createdump duration rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validTargetOutput.Replace('435ms', '+435ms')
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'unrelated target output rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validStoppedTarget.StandardOutput + "application output`n"
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'oversized target output rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = 'x' * 4097
                    StandardError = ''
                }
                DumpPath = $selfTestDumpPath
            },
            [pscustomobject]@{
                Name = 'target standard error rejected'
                Target = [pscustomobject]@{
                    ProcessId = $selfTestProcessId
                    StandardOutput = $validStoppedTarget.StandardOutput
                    StandardError = 'unexpected diagnostic'
                }
                DumpPath = $selfTestDumpPath
            })) {
            Assert-SelfTestThrows {
                Assert-BoundedDemoTargetPostReadyOutput `
                    -StoppedTarget $invalidTargetOutput.Target `
                    -ExpectedDumpPath $invalidTargetOutput.DumpPath
            } $invalidTargetOutput.Name
        }

        $primaryException = [System.InvalidOperationException]::new('primary operation failed')
        $targetCleanupException = [System.InvalidOperationException]::new('target cleanup failed')
        $dumpCleanupException = [System.InvalidOperationException]::new('dump cleanup failed')
        $targetCleanupRecord = [pscustomobject]@{
            Name = 'target cleanup'
            Failure = $targetCleanupException
        }
        $dumpCleanupRecord = [pscustomobject]@{
            Name = 'dump cleanup'
            Failure = $dumpCleanupException
        }
        $primaryOnlyFailure = New-PublisherOperationFailure `
            -PrimaryFailure $primaryException `
            -CleanupFailures @() `
            -Description 'self-test operation'
        if (-not [object]::ReferenceEquals($primaryException, $primaryOnlyFailure)) {
            throw 'Publisher operation failure composition did not preserve a primary-only exception.'
        }
        $cleanupOnlyFailure = New-PublisherOperationFailure `
            -PrimaryFailure $null `
            -CleanupFailures @($targetCleanupRecord) `
            -Description 'self-test operation'
        if (-not [object]::ReferenceEquals($targetCleanupException, $cleanupOnlyFailure)) {
            throw 'Publisher operation failure composition did not preserve a cleanup-only exception.'
        }
        $combinedFailure = New-PublisherOperationFailure `
            -PrimaryFailure $primaryException `
            -CleanupFailures @($targetCleanupRecord, $dumpCleanupRecord) `
            -Description 'self-test operation'
        if ($combinedFailure -isnot [System.AggregateException] -or
            $combinedFailure.InnerExceptions.Count -ne 3 -or
            -not [object]::ReferenceEquals($primaryException, $combinedFailure.InnerExceptions[0]) -or
            -not [object]::ReferenceEquals($targetCleanupException, $combinedFailure.InnerExceptions[1].InnerException) -or
            -not [object]::ReferenceEquals($dumpCleanupException, $combinedFailure.InnerExceptions[2].InnerException)) {
            throw 'Publisher operation failure composition did not retain primary, target-cleanup, and dump-cleanup failures in order.'
        }
        $multipleCleanupFailure = New-PublisherOperationFailure `
            -PrimaryFailure $null `
            -CleanupFailures @($targetCleanupRecord, $dumpCleanupRecord) `
            -Description 'self-test operation'
        if ($multipleCleanupFailure -isnot [System.AggregateException] -or
            $multipleCleanupFailure.InnerExceptions.Count -ne 2 -or
            -not [object]::ReferenceEquals($targetCleanupException, $multipleCleanupFailure.InnerExceptions[0].InnerException) -or
            -not [object]::ReferenceEquals($dumpCleanupException, $multipleCleanupFailure.InnerExceptions[1].InnerException)) {
            throw 'Publisher operation failure composition did not retain multiple cleanup failures in order.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $selfTestRoot) {
            $selfTestFullPath = [System.IO.Path]::GetFullPath($selfTestRoot)
            if ([System.IO.Path]::GetDirectoryName($selfTestFullPath) -cne
                    [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetTempPath()) -or
                [System.IO.Path]::GetFileName($selfTestFullPath) -notmatch '^phoenixinspect-publisher-selftest-[0-9a-f]{32}$') {
                throw "Refusing self-test cleanup of unexpected path '$selfTestFullPath'."
            }
            Remove-Item -LiteralPath $selfTestFullPath -Recurse -Force
        }
    }

    Write-Output 'Publisher self-test passed: SBOM-bound v2 output, v1/v0 migration-only ownership, adversarial archive rejection, exact CLI workflow semantics, and PID/path-bound createdump diagnostics.'
}

$products = @(
    [pscustomobject]@{
        Name = 'PhoenixInspect CLI'
        Slug = 'phoenixinspect-cli'
        Inventory = 'cli'
        Project = Join-Path $repositoryRoot 'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj'
        Executable = 'phoenixinspect.exe'
    },
    [pscustomobject]@{
        Name = 'PhoenixInspect Desktop'
        Slug = 'phoenixinspect-desktop'
        Inventory = 'desktop'
        Project = Join-Path $repositoryRoot 'src/PhoenixInspect.Desktop/PhoenixInspect.Desktop.csproj'
        Executable = 'PhoenixInspect.exe'
    }
)

[string[]] $expectedOutputNames = @(
    @($products | ForEach-Object { "$($_.Slug)-$expectedVersion-$runtimeIdentifier.zip" }) + 'SHA256SUMS.txt'
)
[System.Array]::Sort($expectedOutputNames, [System.StringComparer]::Ordinal)
if ($SelfTest) {
    Invoke-PublisherArchiveContractSelfTest
    return
}
$totalStopwatch = $null
$stagedOutput = $null
$previousOutput = $null
$previousOutputMoved = $false
$newOutputInstalled = $false
$swapCommitted = $false
$outputMutex = [System.Threading.Mutex]::new($false, $outputMutexName)
$outputMutexHeld = $false
$publisherFailure = $null

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

    Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
    $null = New-Item -ItemType Directory -Path $publishRoot -Force
    $null = New-Item -ItemType Directory -Path $archiveRoot -Force
    $null = New-Item -ItemType Directory -Path $sdkArtifactsRoot -Force
    $null = New-Item -ItemType Directory -Path $nugetPackageRoot -Force
    $totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Host 'Verifying clean raw Git source bytes and selected .NET SDK policy…' -ForegroundColor Cyan
    $initialSourceState = Get-PrereleaseSourceState -RepositoryRoot $repositoryRoot
    $repositoryContract = Get-PrereleaseRepositoryContract -RepositoryRoot $repositoryRoot
    $selectedSdk = Get-SelectedDotNetSdkVersion `
        -RepositoryRoot $repositoryRoot `
        -ConfiguredMinimum $repositoryContract.ConfiguredSdkMinimum
    $sourceTimestamp = Get-PrereleaseSbomSourceTimestamp `
        -RepositoryRoot $repositoryRoot `
        -SourceCommit $initialSourceState.Commit
    Write-Host 'Resolving and verifying pinned Microsoft SBOM Tool 4.1.5…' -ForegroundColor Cyan
    $sbomTool = Resolve-PrereleaseSbomTool `
        -ToolPath $SbomToolPath `
        -DownloadDirectory $sbomToolRoot `
        -PolicyPath $sbomPolicyPath

    foreach ($product in $products) {
        Write-Host "Locked-restoring $($product.Name)…" -ForegroundColor Cyan
        Invoke-Checked -Description "Restoring $($product.Project)" -CommandArguments @(
            'restore', $product.Project,
            '--locked-mode',
            '--runtime', $runtimeIdentifier,
            '--artifacts-path', $sdkArtifactsRoot,
            '--packages', $nugetPackageRoot,
            '--disable-build-servers',
            '--verbosity', 'minimal')
    }
    $restoreGraphEvidence = Test-PrereleaseRestoreGraph `
        -RepositoryRoot $repositoryRoot `
        -ArtifactsRoot $sdkArtifactsRoot `
        -RootProjects @(
            'src/PhoenixInspect.Cli/PhoenixInspect.Cli.csproj',
            'src/PhoenixInspect.Desktop/PhoenixInspect.Desktop.csproj')

    foreach ($product in $products) {
        $productStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $productPublishDirectory = Join-Path $publishRoot $product.Slug
        Write-Host "Publishing $($product.Name) for $runtimeIdentifier…" -ForegroundColor Cyan
        Invoke-Checked -Description "Publishing $($product.Project)" -CommandArguments @(
            'publish', $product.Project,
            '--configuration', $configuration,
            '--runtime', $runtimeIdentifier,
            '--self-contained', 'true',
            '--no-restore',
            '--artifacts-path', $sdkArtifactsRoot,
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

    if (-not (Test-Path -LiteralPath $demoTargetProject -PathType Leaf)) {
        throw "CLI workflow sample target '$demoTargetProject' is missing."
    }
    $null = New-Item -ItemType Directory -Path $demoTargetArtifactsRoot -Force
    $null = New-Item -ItemType Directory -Path $demoTargetPublishRoot -Force
    Write-Host 'Locked-restoring the disposable Contoso.OrderService CLI workflow target…' -ForegroundColor Cyan
    Invoke-Checked -Description "Restoring $demoTargetProject" -CommandArguments @(
        'restore', $demoTargetProject,
        '--locked-mode',
        '--artifacts-path', $demoTargetArtifactsRoot,
        '--packages', $nugetPackageRoot,
        '--disable-build-servers',
        '--verbosity', 'minimal')
    Write-Host 'Publishing the disposable Contoso.OrderService CLI workflow target…' -ForegroundColor Cyan
    Invoke-Checked -Description "Publishing $demoTargetProject" -CommandArguments @(
        'publish', $demoTargetProject,
        '--configuration', $configuration,
        '--framework', $repositoryContract.TargetFramework,
        '--self-contained', 'false',
        '--no-restore',
        '--artifacts-path', $demoTargetArtifactsRoot,
        '--output', $demoTargetPublishRoot,
        '--verbosity', 'minimal',
        '--nologo',
        '--disable-build-servers',
        '/p:UseSharedCompilation=false')
    foreach ($requiredTargetFile in @(
        $demoTargetExecutable,
        (Join-Path $demoTargetPublishRoot 'Contoso.OrderService.dll'),
        (Join-Path $demoTargetPublishRoot 'Contoso.OrderService.runtimeconfig.json'))) {
        if (-not (Test-Path -LiteralPath $requiredTargetFile -PathType Leaf)) {
            throw "The disposable framework-dependent CLI workflow target is incomplete: '$requiredTargetFile' is missing."
        }
    }
    if (Test-Path -LiteralPath (Join-Path $demoTargetPublishRoot 'coreclr.dll')) {
        throw 'The disposable CLI workflow target unexpectedly contains coreclr.dll; its smoke-only publish must remain framework-dependent.'
    }

    if (-not (Test-Path -LiteralPath $noticeGenerator -PathType Leaf)) {
        throw "Third-party notice generator '$noticeGenerator' is missing."
    }
    Write-Host 'Generating exact third-party dependency and notice evidence…' -ForegroundColor Cyan
    & $noticeGenerator `
        -CliPayloadDirectory (Join-Path $publishRoot 'phoenixinspect-cli') `
        -DesktopPayloadDirectory (Join-Path $publishRoot 'phoenixinspect-desktop') `
        -OutputDirectory $noticeEvidenceRoot `
        -NuGetPackageRoot $nugetPackageRoot
    $noticeManifestPath = Join-Path $noticeEvidenceRoot 'MANIFEST.sha256'
    if (-not (Test-Path -LiteralPath $noticeManifestPath -PathType Leaf)) {
        throw "Third-party notice generator did not produce its required evidence manifest."
    }

    $cliRuntime = Get-PrereleasePublishedRuntimeIdentity `
        -PayloadDirectory (Join-Path $publishRoot 'phoenixinspect-cli')
    $desktopRuntime = Get-PrereleasePublishedRuntimeIdentity `
        -PayloadDirectory (Join-Path $publishRoot 'phoenixinspect-desktop')
    $cliRuntimeText = ConvertTo-Json $cliRuntime -Depth 10 -Compress
    $desktopRuntimeText = ConvertTo-Json $desktopRuntime -Depth 10 -Compress
    if ($cliRuntimeText -cne $desktopRuntimeText) {
        throw "CLI and Desktop published runtime-pack identities disagree: '$cliRuntimeText' versus '$desktopRuntimeText'."
    }
    $actualRuntimeDownload = $cliRuntime.RuntimePacks[0].id.Substring('runtimepack.'.Length) + '/' +
        $cliRuntime.RuntimePacks[0].version
    if ($restoreGraphEvidence.RuntimePackDownloads -cnotcontains $actualRuntimeDownload) {
        throw "Published runtime pack '$actualRuntimeDownload' is absent from the agreed isolated restore runtime-pack set."
    }

    Write-Host 'Rechecking source identity and creating canonical unsigned local build evidence…' -ForegroundColor Cyan
    $postBuildSourceState = Get-PrereleaseSourceState -RepositoryRoot $repositoryRoot
    Assert-PrereleaseSourceStateEqual -Initial $initialSourceState -Current $postBuildSourceState
    $selectedInputs = Get-PrereleaseSelectedInputRecords -RepositoryRoot $repositoryRoot
    $sbomBuildDescriptor = Get-PrereleaseSbomBuildEvidenceDescriptor -PolicyPath $sbomPolicyPath
    [byte[]] $buildEvidenceBytes = New-PrereleaseBuildEvidenceBytes `
        -InitialSource $initialSourceState `
        -FinalSource $postBuildSourceState `
        -RepositoryContract $repositoryContract `
        -SelectedSdk $selectedSdk `
        -RuntimePacks $cliRuntime.RuntimePacks `
        -ThirdPartyEvidenceManifestSha256 ((Get-FileHash -LiteralPath $noticeManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()) `
        -Sbom $sbomBuildDescriptor `
        -SelectedInputs $selectedInputs
    $null = Test-PrereleaseBuildEvidenceBytes `
        -Bytes $buildEvidenceBytes `
        -RepositoryRoot $repositoryRoot `
        -VerifySelectedInputHashes

    foreach ($product in $products) {
        $productPublishDirectory = Join-Path $publishRoot $product.Slug
        Write-PrereleaseBuildEvidence `
            -Path (Join-Path $productPublishDirectory 'BUILD-EVIDENCE.json') `
            -Bytes $buildEvidenceBytes
        $noticeDestination = Join-Path $productPublishDirectory 'THIRD-PARTY-NOTICES'
        if (Test-Path -LiteralPath $noticeDestination) {
            throw "$($product.Name) publish unexpectedly already contains '$noticeDestination'."
        }
        Copy-Item -LiteralPath $noticeEvidenceRoot -Destination $noticeDestination -Recurse

        [byte[]] $sbomEvidenceBytes = New-PrereleaseSbomEvidenceBytes `
            -ProductName $product.Name `
            -InventoryName $product.Inventory `
            -Version $expectedVersion `
            -SourceCommit $initialSourceState.Commit `
            -SourceTree $initialSourceState.Tree `
            -SourceTimestamp $sourceTimestamp `
            -PolicyPath $sbomPolicyPath
        Write-PrereleaseSbomEvidence `
            -Path (Join-Path $productPublishDirectory 'SBOM-EVIDENCE.json') `
            -Bytes $sbomEvidenceBytes `
            -PolicyPath $sbomPolicyPath

        $manifest = Write-PayloadManifest `
            -PayloadDirectory $productPublishDirectory `
            -Product $product.Name `
            -Version $expectedVersion
        Test-PayloadManifest -PayloadDirectory $productPublishDirectory -ManifestPath $manifest

        Write-Host "Generating and validating the product-specific $($product.Name) SPDX 2.2 SBOM…" -ForegroundColor Cyan
        $sbomResult = Invoke-PrereleaseSbomGeneration `
            -ToolPath $sbomTool.Path `
            -PayloadDirectory $productPublishDirectory `
            -WorkDirectory (Join-Path $sbomWorkRoot $product.Inventory) `
            -ExpectedEvidenceBytes $sbomEvidenceBytes `
            -PolicyPath $sbomPolicyPath
        if ($sbomResult.Product -cne $product.Name -or
            $sbomResult.InventoryName -cne $product.Inventory -or
            $sbomResult.Version -cne $expectedVersion -or
            $sbomResult.DependencyCount -le 0 -or
            $sbomResult.FileCount -le 0) {
            throw "$($product.Name) SBOM validation returned an invalid product, version, dependency count, or file count."
        }
        Test-PayloadManifest -PayloadDirectory $productPublishDirectory -ManifestPath $manifest

        $archiveBaseName = "$($product.Slug)-$expectedVersion-$runtimeIdentifier"
        $archivePath = Join-Path $archiveRoot "$archiveBaseName.zip"
        Write-Host "Archiving $archiveBaseName…" -ForegroundColor Cyan
        New-NormalizedZip `
            -PayloadDirectory $productPublishDirectory `
            -ArchivePath $archivePath `
            -ContentRoot $archiveBaseName
    }

    Assert-PrereleaseBuildEvidenceIdentity `
        -First ([System.IO.File]::ReadAllBytes((Join-Path $publishRoot 'phoenixinspect-cli/BUILD-EVIDENCE.json'))) `
        -Second ([System.IO.File]::ReadAllBytes((Join-Path $publishRoot 'phoenixinspect-desktop/BUILD-EVIDENCE.json')))

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
            -ExtractionParent $extractionRoot `
            -ExpectedBuildEvidenceBytes $buildEvidenceBytes `
            -ExpectedProduct $product.Name `
            -ExpectedInventory $product.Inventory `
            -ExpectedVersion $expectedVersion
        if ($product.Slug -eq 'phoenixinspect-cli') {
            Write-Host 'Smoke-launching the extracted CLI with a 30-second bound…' -ForegroundColor Cyan
            Invoke-BoundedCliSmoke `
                -ExecutablePath (Join-Path $extractedPayload $product.Executable) `
                -ExpectedVersion $expectedVersion `
                -ScratchDirectory $extractionRoot
            Write-Host 'Capturing and inspecting a disposable Contoso.OrderService dump with the extracted CLI…' -ForegroundColor Cyan
            Invoke-BoundedCliWorkflowSmoke `
                -ExecutablePath (Join-Path $extractedPayload $product.Executable) `
                -TargetExecutablePath $demoTargetExecutable `
                -ScratchDirectory $demoTargetScratchRoot
        }
        elseif ($product.Slug -eq 'phoenixinspect-desktop') {
            Write-Host 'Smoke-loading the extracted Desktop payload without UI, with a 30-second bound…' -ForegroundColor Cyan
            Invoke-BoundedDesktopSmoke `
                -ExecutablePath (Join-Path $extractedPayload $product.Executable) `
                -ExpectedVersion $expectedVersion `
                -ScratchDirectory $extractionRoot
        }
    }

    Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
    $preStageSourceState = Get-PrereleaseSourceState -RepositoryRoot $repositoryRoot
    Assert-PrereleaseSourceStateEqual -Initial $initialSourceState -Current $preStageSourceState

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
    $preInstallSourceState = Get-PrereleaseSourceState -RepositoryRoot $repositoryRoot
    Assert-PrereleaseSourceStateEqual -Initial $initialSourceState -Current $preInstallSourceState
    try {
        if ($hadPreviousOutput) {
            # Revalidate at the destructive boundary, not only before the long publish.
            Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
            [System.IO.Directory]::Move($outputFullPath, $previousOutput)
            $previousOutputMoved = $true
            Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
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
                Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
                [System.IO.Directory]::Move($previousOutput, $outputFullPath)
                $previousOutputMoved = $false
                Test-ArtifactOutputDirectory -Path $outputFullPath -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
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
        Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
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
    Write-Host 'Canonical unsigned local build identity and validated product-specific SPDX 2.2 inventory are embedded; no reproducibility, SLSA provenance, signature, package, tag, release, redistribution authorization, or W8.10 closure is claimed.'
    Write-Host 'Generated third-party inventory, SBOM, and notice evidence are embedded; human legal review remains a release-closure blocker.'
    Write-Host 'The extracted CLI workflow smoke proves one local capture/open/static-field evaluation path against a disposable sample dump; it is not general compatibility or release closure.'
    Write-Host 'The Desktop smoke covers non-UI dependency presence, selected assembly loads, and compiled XAML registration; it does not claim visible UI startup.'
    $totalStopwatch.Stop()
    Write-Host "Completed in $([math]::Round($totalStopwatch.Elapsed.TotalSeconds, 1)) s."
}
catch {
    $publisherFailure = $_.Exception
}
finally {
$publisherCleanupFailures = [System.Collections.Generic.List[object]]::new()
try {
    if ($null -ne $stagedOutput -and (Test-Path -LiteralPath $stagedOutput)) {
        Remove-GuardedSwapDirectory -Path $stagedOutput -Kind stage
    }
}
catch {
    $publisherCleanupFailures.Add([pscustomobject]@{
        Name = 'staged artifact directory removal'
        Failure = $_.Exception
    })
}
try {
    if ($swapCommitted -and $previousOutputMoved -and
        $null -ne $previousOutput -and (Test-Path -LiteralPath $previousOutput)) {
        Test-ArtifactOutputDirectory -Path $previousOutput -ExpectedNames $expectedOutputNames -AllowPriorVersion -AllowLegacyPublisherFormat
        Remove-GuardedSwapDirectory -Path $previousOutput -Kind previous
        $previousOutputMoved = $false
    }
}
catch {
    $publisherCleanupFailures.Add([pscustomobject]@{
        Name = 'previous artifact directory removal'
        Failure = $_.Exception
    })
}
try {
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
catch {
    $publisherCleanupFailures.Add([pscustomobject]@{
        Name = 'temporary publisher work-root removal'
        Failure = $_.Exception
    })
}
try {
    if ($outputMutexHeld) {
        $outputMutex.ReleaseMutex()
        $outputMutexHeld = $false
    }
}
catch {
    $publisherCleanupFailures.Add([pscustomobject]@{
        Name = 'artifact-output mutex release'
        Failure = $_.Exception
    })
}
try {
    $outputMutex.Dispose()
}
catch {
    $publisherCleanupFailures.Add([pscustomobject]@{
        Name = 'artifact-output mutex disposal'
        Failure = $_.Exception
    })
}
Complete-PublisherOperation `
    -PrimaryFailure $publisherFailure `
    -CleanupFailures $publisherCleanupFailures.ToArray() `
    -Description 'The prerelease artifact publisher'
}
