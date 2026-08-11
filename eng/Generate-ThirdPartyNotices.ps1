#Requires -Version 7.0
#Requires -PSEdition Core

<#
.SYNOPSIS
    Generates deterministic third-party dependency and notice evidence for the PhoenixInspect prerelease payloads.

.DESCRIPTION
    Reads the CLI and Desktop self-contained win-x64 .deps.json files, attributes every external runtime/native
    asset to a package or runtime pack, verifies those files exist in the payload, verifies the matching NuGet
    package archives and policy-pinned license/notice material, and writes a deterministic evidence bundle.

    The output is an inventory of evidence. It deliberately does not assert that legal review or redistribution
    clearance is complete.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CliPayloadDirectory,

    [Parameter(Mandatory)]
    [string] $DesktopPayloadDirectory,

    [Parameter(Mandatory)]
    [string] $OutputDirectory,

    [string] $PolicyPath,

    [string] $NuGetPackageRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $PolicyPath) {
    $PolicyPath = Join-Path $PSScriptRoot 'third-party-notices.policy.json'
}
if (-not $NuGetPackageRoot) {
    $NuGetPackageRoot = if ($env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES
    }
    else {
        Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.nuget/packages'
    }
}

$utf8NoBom = [Text.UTF8Encoding]::new($false)
$sha256Cache = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)

function Get-FullPath {
    param([Parameter(Mandatory)][string] $Path)
    return [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string] $Path)
    $fullPath = Get-FullPath $Path
    if (-not $sha256Cache.ContainsKey($fullPath)) {
        $sha256Cache.Add($fullPath, (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant())
    }
    return $sha256Cache[$fullPath]
}

function Get-Sha512Base64 {
    param([Parameter(Mandatory)][string] $Path)
    $algorithm = [Security.Cryptography.SHA512]::Create()
    $stream = [IO.File]::OpenRead((Get-FullPath $Path))
    try {
        return [Convert]::ToBase64String($algorithm.ComputeHash($stream))
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Get-OrdinalStrings {
    param([Collections.IEnumerable] $Values)
    [string[]] $result = @(foreach ($value in $Values) { [string] $value })
    [Array]::Sort($result, [StringComparer]::Ordinal)
    return $result
}

function Get-OrdinalSortedRecords {
    param(
        [Parameter(Mandatory)][Collections.IEnumerable] $Values,
        [Parameter(Mandatory)][string[]] $PropertyNames
    )

    if ($PropertyNames.Count -eq 0) {
        throw 'At least one record property is required for ordinal sorting.'
    }

    $recordsByKey = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($value in $Values) {
        $components = [Collections.Generic.List[string]]::new()
        foreach ($propertyName in $PropertyNames) {
            if ($value -isnot [Collections.IDictionary] -or -not $value.Contains($propertyName)) {
                throw "Record is missing ordinal sort property '$propertyName'."
            }
            $component = [string] $value[$propertyName]
            if ($component.IndexOf([char] 0) -ge 0) {
                throw "Record property '$propertyName' contains an invalid null character."
            }
            $components.Add($component)
        }
        $key = [string]::Join([char] 0, $components)
        if ($recordsByKey.ContainsKey($key)) {
            throw ('Records contain a duplicate ordinal sort key for properties: {0}.' -f
                ($PropertyNames -join ', '))
        }
        $recordsByKey.Add($key, $value)
    }

    $result = [Collections.Generic.List[object]]::new()
    foreach ($key in Get-OrdinalStrings $recordsByKey.Keys) {
        $result.Add($recordsByKey[$key])
    }
    return $result.ToArray()
}

function Test-PathsOverlap {
    param(
        [Parameter(Mandatory)][string] $FirstPath,
        [Parameter(Mandatory)][string] $SecondPath
    )

    $first = Get-FullPath $FirstPath
    $second = Get-FullPath $SecondPath
    if ($first.Equals($second, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $separator = [IO.Path]::DirectorySeparatorChar
    $firstPrefix = if ($first.EndsWith($separator)) { $first } else { $first + $separator }
    $secondPrefix = if ($second.EndsWith($separator)) { $second } else { $second + $separator }
    return $first.StartsWith($secondPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $second.StartsWith($firstPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Remove-GuardedStageDirectory {
    param(
        [Parameter(Mandatory)][string] $StagePath,
        [Parameter(Mandatory)][string] $ExpectedParent,
        [Parameter(Mandatory)][string] $ExpectedLeafPrefix
    )

    $fullStage = Get-FullPath $StagePath
    $fullExpectedParent = Get-FullPath $ExpectedParent
    $actualParent = Get-FullPath ([IO.Path]::GetDirectoryName($fullStage))
    if (-not $actualParent.Equals($fullExpectedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing recursive cleanup outside expected staging parent '$fullExpectedParent': '$fullStage'."
    }

    $stageLeaf = [IO.Path]::GetFileName($fullStage)
    $expectedLeafPattern = '^' + [regex]::Escape($ExpectedLeafPrefix) + '[0-9a-f]{32}$'
    if (-not [regex]::IsMatch($stageLeaf, $expectedLeafPattern,
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "Refusing recursive cleanup of malformed staging directory '$fullStage'."
    }

    $stageItem = Get-Item -LiteralPath $fullStage -Force
    if ($stageItem -isnot [IO.DirectoryInfo]) {
        throw "Refusing recursive cleanup because staging target '$fullStage' is not a directory."
    }
    if (($stageItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing recursive cleanup of reparse-point staging directory '$fullStage'."
    }
    if (-not (Get-FullPath $stageItem.FullName).Equals($fullStage, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing recursive cleanup because staging target '$($stageItem.FullName)' is not '$fullStage'."
    }

    [IO.Directory]::Delete($fullStage, $true)
}

function Assert-SafeRelativePath {
    param([Parameter(Mandatory)][string] $Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) {
        throw "Unsafe relative path '$Path'."
    }
    $parts = $Path -split '[\\/]'
    if ($parts -contains '..' -or $parts -contains '') {
        throw "Unsafe relative path '$Path'."
    }
}

function Get-ForwardRelativePath {
    param(
        [Parameter(Mandatory)][string] $BasePath,
        [Parameter(Mandatory)][string] $Path
    )
    $relative = [IO.Path]::GetRelativePath($BasePath, $Path) -replace '\\', '/'
    Assert-SafeRelativePath $relative
    return $relative
}

function Write-Utf8Lf {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Text
    )
    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    if (-not $normalized.EndsWith("`n", [StringComparison]::Ordinal)) {
        $normalized += "`n"
    }
    $parent = Split-Path -Parent $Path
    $null = [IO.Directory]::CreateDirectory($parent)
    [IO.File]::WriteAllText($Path, $normalized, $utf8NoBom)
}

function Write-DeterministicJson {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)] $Value
    )
    Write-Utf8Lf -Path $Path -Text ($Value | ConvertTo-Json -Depth 100)
}

function Split-LibraryName {
    param([Parameter(Mandatory)][string] $Library)
    $separator = $Library.LastIndexOf('/')
    if ($separator -le 0 -or $separator -eq ($Library.Length - 1)) {
        throw "Dependency library '$Library' is not in package/version form."
    }
    return [pscustomobject]@{
        Id = $Library.Substring(0, $separator)
        Version = $Library.Substring($separator + 1)
    }
}

function Get-NuspecMetadata {
    param([Parameter(Mandatory)][string] $Path)
    [xml] $document = [IO.File]::ReadAllText($Path)
    $metadata = $document.package.metadata
    if ($null -eq $metadata) {
        throw "NuGet metadata is missing from '$Path'."
    }
    $children = @($metadata.ChildNodes)
    $license = @($children | Where-Object LocalName -eq 'license' | Select-Object -First 1)
    $repositoryNodes = @($children | Where-Object LocalName -eq 'repository' | Select-Object -First 1)
    $repository = if ($repositoryNodes.Count -eq 1) { $repositoryNodes[0] } else { $null }
    $licenseType = if ($license.Count -eq 1) { [string] $license[0].GetAttribute('type') } else { '' }
    $licenseValue = if ($license.Count -eq 1) { [string] $license[0].InnerText } else { '' }
    $childText = @{}
    foreach ($name in @('authors', 'copyright', 'licenseUrl', 'projectUrl')) {
        $nodes = @($children | Where-Object LocalName -eq $name | Select-Object -First 1)
        $childText[$name] = if ($nodes.Count -eq 1) { [string] $nodes[0].InnerText } else { '' }
    }
    return [ordered]@{
        authors = $childText.authors
        copyright = $childText.copyright
        licenseType = $licenseType
        licenseValue = $licenseValue
        licenseUrl = $childText.licenseUrl
        projectUrl = $childText.projectUrl
        repositoryType = if ($null -ne $repository) { [string] $repository.GetAttribute('type') } else { '' }
        repositoryUrl = if ($null -ne $repository) { [string] $repository.GetAttribute('url') } else { '' }
        repositoryCommit = if ($null -ne $repository) { [string] $repository.GetAttribute('commit') } else { '' }
    }
}

$policyFullPath = Get-FullPath $PolicyPath
$nugetFullPath = Get-FullPath $NuGetPackageRoot
$outputFullPath = Get-FullPath $OutputDirectory
$cliFullPath = Get-FullPath $CliPayloadDirectory
$desktopFullPath = Get-FullPath $DesktopPayloadDirectory

foreach ($requiredDirectory in @($cliFullPath, $desktopFullPath, $nugetFullPath)) {
    if (-not (Test-Path -LiteralPath $requiredDirectory -PathType Container)) {
        throw "Required directory '$requiredDirectory' does not exist."
    }
}
if (-not (Test-Path -LiteralPath $policyFullPath -PathType Leaf)) {
    throw "Notice policy '$policyFullPath' does not exist."
}
if ($outputFullPath -eq [IO.Path]::GetPathRoot($outputFullPath)) {
    throw "OutputDirectory '$outputFullPath' is not a safe dedicated output directory."
}
foreach ($protectedDirectory in ([ordered]@{
        'CLI payload' = $cliFullPath
        'Desktop payload' = $desktopFullPath
        'NuGet package root' = $nugetFullPath
    }).GetEnumerator()) {
    if (Test-PathsOverlap -FirstPath $outputFullPath -SecondPath $protectedDirectory.Value) {
        throw "OutputDirectory '$outputFullPath' overlaps the $($protectedDirectory.Key) '$($protectedDirectory.Value)'."
    }
}
if (Test-Path -LiteralPath $outputFullPath) {
    throw "OutputDirectory '$outputFullPath' already exists; refusing to merge or overwrite notice evidence."
}

$policy = [IO.File]::ReadAllText($policyFullPath) | ConvertFrom-Json -AsHashtable
if ([int] $policy.schemaVersion -ne 1) {
    throw "Unsupported third-party notice policy schema '$($policy.schemaVersion)'."
}
if ([string] $policy.runtimeIdentifier -ne 'win-x64') {
    throw "The notice policy must target win-x64."
}

$mappingByLibrary = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$mappingIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($mapping in @($policy.mappings)) {
    $mappingId = [string] $mapping.id
    if ([string]::IsNullOrWhiteSpace($mappingId) -or -not $mappingIds.Add($mappingId)) {
        throw "Notice policy contains a missing or duplicate mapping id '$mappingId'."
    }
    foreach ($library in @($mapping.libraries)) {
        $libraryName = [string] $library
        if ($mappingByLibrary.ContainsKey($libraryName)) {
            throw "Library '$libraryName' has more than one notice mapping."
        }
        $mappingByLibrary.Add($libraryName, $mapping)
    }
}

$runtimePattern = [regex]::new([string] $policy.runtimeMapping.libraryPattern,
    [Text.RegularExpressions.RegexOptions]::CultureInvariant)

$payloadDirectories = [ordered]@{
    cli = $cliFullPath
    desktop = $desktopFullPath
}
$productPolicies = @($policy.products)
if ($productPolicies.Count -ne 2) {
    throw 'The policy must describe exactly the cli and desktop payloads.'
}

$actualLibraries = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$productLibrarySets = @{}
$productRecords = [Collections.Generic.List[object]]::new()
$allRequirementAssets = [Collections.Generic.List[object]]::new()

foreach ($productPolicy in $productPolicies) {
    $productName = [string] $productPolicy.name
    if (-not $payloadDirectories.Contains($productName)) {
        throw "Unknown product '$productName' in notice policy."
    }
    $payloadDirectory = [string] $payloadDirectories[$productName]
    $depsFileName = [string] $productPolicy.depsFile
    Assert-SafeRelativePath $depsFileName
    $depsPath = Join-Path $payloadDirectory $depsFileName
    if (-not (Test-Path -LiteralPath $depsPath -PathType Leaf)) {
        throw "$productName payload is missing '$depsFileName'."
    }

    $deps = [IO.File]::ReadAllText($depsPath) | ConvertFrom-Json -AsHashtable
    $runtimeTarget = [string] $deps.runtimeTarget.name
    if (-not $runtimeTarget.EndsWith('/win-x64', [StringComparison]::Ordinal)) {
        throw "$productName dependency target '$runtimeTarget' is not win-x64."
    }
    if (-not $deps.targets.ContainsKey($runtimeTarget)) {
        throw "$productName dependencies do not contain runtime target '$runtimeTarget'."
    }
    $target = $deps.targets[$runtimeTarget]

    $filesByLeaf = [Collections.Generic.Dictionary[string, Collections.Generic.List[IO.FileInfo]]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    [IO.FileInfo[]] $payloadFiles = @(Get-ChildItem -LiteralPath $payloadDirectory -File -Recurse)
    foreach ($file in $payloadFiles) {
        if (-not $filesByLeaf.ContainsKey($file.Name)) {
            $filesByLeaf.Add($file.Name, [Collections.Generic.List[IO.FileInfo]]::new())
        }
        $filesByLeaf[$file.Name].Add($file)
    }

    $productLibrarySet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $assetRecords = [Collections.Generic.List[object]]::new()
    foreach ($libraryEntry in $deps.libraries.GetEnumerator()) {
        if ([string] $libraryEntry.Value.type -eq 'project') {
            continue
        }
        $libraryName = [string] $libraryEntry.Key
        if (-not $target.ContainsKey($libraryName)) {
            throw "$productName external library '$libraryName' has no selected runtime target entry."
        }
        $null = $productLibrarySet.Add($libraryName)
        if ($actualLibraries.ContainsKey($libraryName)) {
            if ($actualLibraries[$libraryName] -ne [string] $libraryEntry.Value.type) {
                throw "Library '$libraryName' has inconsistent dependency types across payloads."
            }
        }
        else {
            $actualLibraries.Add($libraryName, [string] $libraryEntry.Value.type)
        }

        $libraryAssetCount = 0
        foreach ($kind in @('runtime', 'native', 'resources', 'runtimeTargets')) {
            $selectedLibrary = $target[$libraryName]
            if (-not $selectedLibrary.ContainsKey($kind)) {
                continue
            }
            foreach ($assetPathObject in $selectedLibrary[$kind].Keys) {
                $assetPath = [string] $assetPathObject
                if ($assetPath -eq '_._' -or $assetPath.EndsWith('/_._', [StringComparison]::Ordinal)) {
                    continue
                }
                $leaf = [IO.Path]::GetFileName($assetPath)
                if (-not $filesByLeaf.ContainsKey($leaf)) {
                    throw "$productName dependency asset '$assetPath' from '$libraryName' is missing from the payload."
                }
                $matches = $filesByLeaf[$leaf]
                if ($matches.Count -ne 1) {
                    throw "$productName dependency asset '$assetPath' from '$libraryName' maps ambiguously to $($matches.Count) payload files named '$leaf'."
                }
                $payloadPath = Get-ForwardRelativePath -BasePath $payloadDirectory -Path $matches[0].FullName
                $record = [ordered]@{
                    library = $libraryName
                    kind = $kind
                    dependencyPath = $assetPath
                    payloadPath = $payloadPath
                    sha256 = Get-Sha256 $matches[0].FullName
                }
                $assetRecords.Add($record)
                $allRequirementAssets.Add([pscustomobject]@{
                    Product = $productName
                    Library = $libraryName
                    Kind = $kind
                    FileName = $leaf
                })
                $libraryAssetCount++
            }
        }
        if ($libraryAssetCount -eq 0) {
            throw "$productName external library '$libraryName' contributes no selected payload assets."
        }
    }

    [string[]] $productRuntimeLibraries = @($productLibrarySet | Where-Object { $runtimePattern.IsMatch($_) })
    if ($productRuntimeLibraries.Count -ne 1) {
        throw "$productName payload must contain exactly one .NET win-x64 runtime pack."
    }
    $appHostName = [string] $productPolicy.appHost
    Assert-SafeRelativePath $appHostName
    if (-not $depsFileName.EndsWith('.deps.json', [StringComparison]::Ordinal) -or
        [IO.Path]::GetFileNameWithoutExtension($appHostName) -cne
        $depsFileName.Substring(0, $depsFileName.Length - '.deps.json'.Length)) {
        throw "$productName app host '$appHostName' does not correspond to dependency file '$depsFileName'."
    }
    $appHostPath = Join-Path $payloadDirectory $appHostName
    if (-not (Test-Path -LiteralPath $appHostPath -PathType Leaf)) {
        throw "$productName payload is missing SDK-generated .NET app host '$appHostName'."
    }
    $appHostRecord = [ordered]@{
        library = $productRuntimeLibraries[0]
        kind = 'apphost'
        dependencyPath = 'SDK-patched .NET apphost'
        payloadPath = $appHostName
        sha256 = Get-Sha256 $appHostPath
        provenance = 'SDK-generated host executable attributed to the self-contained .NET runtime; not a literal .deps.json asset.'
    }
    $assetRecords.Add($appHostRecord)
    $allRequirementAssets.Add([pscustomobject]@{
        Product = $productName
        Library = $productRuntimeLibraries[0]
        Kind = 'apphost'
        FileName = $appHostName
    })

    $productLibrarySets[$productName] = $productLibrarySet
    $sortedAssets = @(Get-OrdinalSortedRecords -Values $assetRecords `
        -PropertyNames @('library', 'kind', 'dependencyPath', 'payloadPath'))
    $productRecords.Add([ordered]@{
        schemaVersion = 1
        product = $productName
        runtimeTarget = $runtimeTarget
        depsFile = $depsFileName
        depsSha256 = Get-Sha256 $depsPath
        payloadFileCount = $payloadFiles.Count
        externalLibraries = @(Get-OrdinalStrings $productLibrarySet)
        assets = $sortedAssets
    })
}

[string[]] $actualLibraryNames = @(Get-OrdinalStrings $actualLibraries.Keys)
[string[]] $runtimeLibraries = @($actualLibraryNames | Where-Object { $runtimePattern.IsMatch($_) })
if ($runtimeLibraries.Count -ne 1) {
    throw "Expected exactly one version-matched .NET win-x64 runtime pack, found $($runtimeLibraries.Count)."
}
$runtimeLibrary = $runtimeLibraries[0]
foreach ($productName in $payloadDirectories.Keys) {
    if (-not $productLibrarySets[$productName].Contains($runtimeLibrary)) {
        throw "$productName does not contain the common self-contained runtime pack '$runtimeLibrary'."
    }
}

$missingMappings = @($actualLibraryNames | Where-Object {
        $_ -ne $runtimeLibrary -and -not $mappingByLibrary.ContainsKey($_)
    })
if ($missingMappings.Count -ne 0) {
    throw "Notice policy is missing dependency mappings: $($missingMappings -join ', ')."
}
$staleMappings = @($mappingByLibrary.Keys | Where-Object { -not $actualLibraries.ContainsKey($_) })
if ($staleMappings.Count -ne 0) {
    throw "Notice policy contains stale dependency mappings: $((Get-OrdinalStrings $staleMappings) -join ', ')."
}

foreach ($requirement in @($policy.assetRequirements)) {
    [string[]] $matchingLibraries = if ($requirement.ContainsKey('library')) {
        @([string] $requirement.library)
    }
    else {
        $requirementPattern = [regex]::new([string] $requirement.libraryPattern,
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        @($actualLibraryNames | Where-Object { $requirementPattern.IsMatch($_) })
    }
    if ($matchingLibraries.Count -ne 1 -or -not $actualLibraries.ContainsKey($matchingLibraries[0])) {
        throw "Asset requirement must select exactly one present library; selected '$($matchingLibraries -join ', ')'."
    }
    $requiredLibrary = $matchingLibraries[0]
    foreach ($productName in $payloadDirectories.Keys) {
        if (-not $productLibrarySets[$productName].Contains($requiredLibrary)) {
            continue
        }
        foreach ($fileName in @($requirement.fileNames)) {
            $match = @($allRequirementAssets | Where-Object {
                $_.Product -eq $productName -and $_.Library -eq $requiredLibrary -and
                $_.Kind -eq [string] $requirement.kind -and $_.FileName -ceq [string] $fileName
            })
            if ($match.Count -ne 1) {
                throw "$productName must contain exactly one required $($requirement.kind) asset '$fileName' from '$requiredLibrary'; found $($match.Count)."
            }
        }
    }
}

$policyDirectory = Split-Path -Parent $policyFullPath
$copyPlan = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
function Add-CopyPlan {
    param(
        [Parameter(Mandatory)][string] $Source,
        [Parameter(Mandatory)][string] $Destination,
        [Parameter(Mandatory)][string] $ExpectedSha256
    )
    Assert-SafeRelativePath $Destination
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required notice evidence '$Source' does not exist."
    }
    $actualHash = Get-Sha256 $Source
    if ($actualHash -cne $ExpectedSha256.ToLowerInvariant()) {
        throw "Notice evidence hash mismatch for '$Source': expected $ExpectedSha256, found $actualHash."
    }
    if ($copyPlan.ContainsKey($Destination)) {
        if ([string] $copyPlan[$Destination].Sha256 -cne $actualHash) {
            throw "Two different evidence files map to '$Destination'."
        }
        return
    }
    $copyPlan.Add($Destination, [pscustomobject]@{ Source = $Source; Sha256 = $actualHash })
}

$licenseTextRecords = @{}
foreach ($licenseEntry in $policy.licenseTexts.GetEnumerator()) {
    $licenseName = [string] $licenseEntry.Key
    $relativeSource = [string] $licenseEntry.Value.path
    Assert-SafeRelativePath $relativeSource
    $source = Join-Path $policyDirectory ($relativeSource -replace '/', [IO.Path]::DirectorySeparatorChar)
    $destination = "licenses/$licenseName.txt"
    Add-CopyPlan -Source $source -Destination $destination -ExpectedSha256 ([string] $licenseEntry.Value.sha256)
    $licenseTextRecords[$licenseName] = [ordered]@{
        path = $destination
        sha256 = [string] $licenseEntry.Value.sha256
    }
}

$policyHash = Get-Sha256 $policyFullPath
Add-CopyPlan -Source $policyFullPath -Destination 'evidence/policy.json' -ExpectedSha256 $policyHash

$libraryRecords = [Collections.Generic.List[object]]::new()
foreach ($libraryName in $actualLibraryNames) {
    $parts = Split-LibraryName $libraryName
    $mapping = if ($libraryName -eq $runtimeLibrary) { $policy.runtimeMapping } else { $mappingByLibrary[$libraryName] }
    $sourcePackageId = if ($mapping.ContainsKey('sourcePackageId')) {
        [string] $mapping.sourcePackageId
    }
    else {
        $parts.Id
    }
    $packageDirectory = Join-Path (Join-Path $nugetFullPath $sourcePackageId.ToLowerInvariant()) $parts.Version.ToLowerInvariant()
    if (-not (Test-Path -LiteralPath $packageDirectory -PathType Container)) {
        throw "NuGet package evidence directory '$packageDirectory' is missing for '$libraryName'."
    }

    $packageFilesByLeaf = $null
    foreach ($productRecord in $productRecords) {
        foreach ($asset in @($productRecord.assets | Where-Object { $_.library -eq $libraryName })) {
            if ($asset.kind -eq 'apphost') {
                continue
            }
            $sourceAsset = Join-Path $packageDirectory (
                ([string] $asset.dependencyPath) -replace '/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $sourceAsset -PathType Leaf)) {
                if ($null -eq $packageFilesByLeaf) {
                    $packageFilesByLeaf = [Collections.Generic.Dictionary[string, Collections.Generic.List[IO.FileInfo]]]::new(
                        [StringComparer]::OrdinalIgnoreCase)
                    foreach ($packageFile in Get-ChildItem -LiteralPath $packageDirectory -File -Recurse) {
                        if (-not $packageFilesByLeaf.ContainsKey($packageFile.Name)) {
                            $packageFilesByLeaf.Add($packageFile.Name, [Collections.Generic.List[IO.FileInfo]]::new())
                        }
                        $packageFilesByLeaf[$packageFile.Name].Add($packageFile)
                    }
                }
                $leaf = [IO.Path]::GetFileName([string] $asset.dependencyPath)
                if (-not $packageFilesByLeaf.ContainsKey($leaf) -or $packageFilesByLeaf[$leaf].Count -ne 1) {
                    $count = if ($packageFilesByLeaf.ContainsKey($leaf)) { $packageFilesByLeaf[$leaf].Count } else { 0 }
                    throw "Package '$sourcePackageId/$($parts.Version)' provides $count source files for payload asset '$($asset.dependencyPath)' from '$libraryName'."
                }
                $sourceAsset = $packageFilesByLeaf[$leaf][0].FullName
            }
            $sourceAssetHash = Get-Sha256 $sourceAsset
            if ($sourceAssetHash -cne [string] $asset.sha256) {
                throw "$($productRecord.product) payload asset '$($asset.payloadPath)' does not match package source '$sourceAsset' for '$libraryName'."
            }
            $asset['sourcePackagePath'] = Get-ForwardRelativePath -BasePath $packageDirectory -Path $sourceAsset
        }
    }

    $nuspecFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nuspec')
    $archives = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nupkg')
    $archiveHashes = @(Get-ChildItem -LiteralPath $packageDirectory -File -Filter '*.nupkg.sha512')
    if ($nuspecFiles.Count -ne 1 -or $archives.Count -ne 1 -or $archiveHashes.Count -ne 1) {
        throw "Package '$sourcePackageId/$($parts.Version)' must contain exactly one nuspec, nupkg, and nupkg.sha512 file."
    }
    $declaredArchiveHash = [IO.File]::ReadAllText($archiveHashes[0].FullName).Trim()
    $actualArchiveHash = Get-Sha512Base64 $archives[0].FullName
    if ($declaredArchiveHash -cne $actualArchiveHash) {
        throw "NuGet archive SHA-512 verification failed for '$sourcePackageId/$($parts.Version)'."
    }

    $nuspecMetadata = Get-NuspecMetadata $nuspecFiles[0].FullName
    $expectedLicense = $mapping.license
    switch ([string] $expectedLicense.type) {
        'expression' {
            if ($nuspecMetadata.licenseType -cne 'expression' -or
                $nuspecMetadata.licenseValue -cne [string] $expectedLicense.value) {
                throw "Package '$libraryName' no longer declares expected license expression '$($expectedLicense.value)'."
            }
        }
        'file' {
            if ($nuspecMetadata.licenseType -cne 'file' -or
                $nuspecMetadata.licenseValue -cne [string] $expectedLicense.value) {
                throw "Package '$libraryName' no longer declares expected license file '$($expectedLicense.value)'."
            }
        }
        'legacyUrl' {
            if (-not [string]::IsNullOrEmpty($nuspecMetadata.licenseType) -or
                $nuspecMetadata.licenseUrl -cne [string] $expectedLicense.value) {
                throw "Package '$libraryName' no longer declares expected legacy license URL '$($expectedLicense.value)'."
            }
        }
        default { throw "Unknown license evidence type '$($expectedLicense.type)' for '$libraryName'." }
    }

    $safePackagePath = "$($sourcePackageId.ToLowerInvariant())/$($parts.Version.ToLowerInvariant())"
    $nuspecDestination = "evidence/$safePackagePath/$($nuspecFiles[0].Name)"
    $nuspecHash = Get-Sha256 $nuspecFiles[0].FullName
    Add-CopyPlan -Source $nuspecFiles[0].FullName -Destination $nuspecDestination -ExpectedSha256 $nuspecHash

    $materials = [Collections.Generic.List[object]]::new()
    if ($mapping.ContainsKey('spdxLicense')) {
        $spdxLicense = [string] $mapping.spdxLicense
        if (-not $licenseTextRecords.ContainsKey($spdxLicense)) {
            throw "Mapping '$($mapping.id)' references unknown checked-in license text '$spdxLicense'."
        }
        $materials.Add([ordered]@{
            kind = 'license'
            path = $licenseTextRecords[$spdxLicense].path
            sha256 = $licenseTextRecords[$spdxLicense].sha256
            source = 'checked-in SPDX license text selected by verified NuGet license expression'
        })
    }
    foreach ($material in @($mapping.materials)) {
        $materialPath = [string] $material.path
        Assert-SafeRelativePath $materialPath
        $source = Join-Path $packageDirectory ($materialPath -replace '/', [IO.Path]::DirectorySeparatorChar)
        $destination = "materials/$safePackagePath/$($materialPath -replace '\\', '/')"
        Add-CopyPlan -Source $source -Destination $destination -ExpectedSha256 ([string] $material.sha256)
        $materials.Add([ordered]@{
            kind = [string] $material.kind
            path = $destination
            sha256 = [string] $material.sha256
            source = "$sourcePackageId/$($parts.Version)/$($materialPath -replace '\\', '/')"
        })
    }
    if ($materials.Count -eq 0) {
        throw "Library '$libraryName' has no distributable license or notice material."
    }

    [string[]] $products = @(Get-OrdinalStrings @($payloadDirectories.Keys | Where-Object {
        $productLibrarySets[$_].Contains($libraryName)
    }))
    $libraryRecords.Add([ordered]@{
        library = $libraryName
        packageId = $parts.Id
        version = $parts.Version
        dependencyType = $actualLibraries[$libraryName]
        products = $products
        mapping = [string] $mapping.id
        licenseDeclaration = [ordered]@{
            type = if ($nuspecMetadata.licenseType) { $nuspecMetadata.licenseType } else { 'legacyUrl' }
            value = if ($nuspecMetadata.licenseValue) { $nuspecMetadata.licenseValue } else { $nuspecMetadata.licenseUrl }
        }
        authors = $nuspecMetadata.authors
        copyright = $nuspecMetadata.copyright
        projectUrl = $nuspecMetadata.projectUrl
        repository = [ordered]@{
            type = $nuspecMetadata.repositoryType
            url = $nuspecMetadata.repositoryUrl
            commit = $nuspecMetadata.repositoryCommit
        }
        sourcePackage = [ordered]@{
            id = $sourcePackageId
            version = $parts.Version
            archiveSha512 = $actualArchiveHash
            nuspec = [ordered]@{
                path = $nuspecDestination
                sha256 = $nuspecHash
            }
        }
        materials = @($materials)
    })
}

$sortedLibraryRecords = @(Get-OrdinalSortedRecords -Values $libraryRecords -PropertyNames @('library'))
$index = [ordered]@{
    schemaVersion = 1
    purpose = 'Mechanically derived dependency and notice evidence; not a legal-clearance assertion.'
    runtimeIdentifier = 'win-x64'
    policy = [ordered]@{
        path = 'evidence/policy.json'
        sha256 = $policyHash
    }
    products = @($productRecords | ForEach-Object {
        [ordered]@{
            name = $_.product
            inventory = "inventory/$($_.product).json"
            depsFile = $_.depsFile
            depsSha256 = $_.depsSha256
            externalLibraryCount = $_.externalLibraries.Count
            externalAssetCount = $_.assets.Count
        }
    })
    libraries = $sortedLibraryRecords
}

$catalogLines = [Collections.Generic.List[string]]::new()
$catalogLines.Add('PhoenixInspect third-party notice evidence')
$catalogLines.Add('')
$catalogLines.Add('This generated catalog inventories package declarations and package-provided notice material.')
$catalogLines.Add('It does not assert legal clearance, license compatibility, or permission to redistribute.')
$catalogLines.Add('See index.json for machine-readable attribution and inventory/*.json for exact external dependency assets plus the apphost.')
$catalogLines.Add('')
foreach ($library in $sortedLibraryRecords) {
    $catalogLines.Add(('=' * 80))
    $catalogLines.Add("Library: $($library.library)")
    $catalogLines.Add("Products: $($library.products -join ', ')")
    $catalogLines.Add("Dependency type: $($library.dependencyType)")
    $catalogLines.Add("NuGet license declaration: $($library.licenseDeclaration.type) $($library.licenseDeclaration.value)")
    if ($library.copyright) { $catalogLines.Add("Copyright metadata: $($library.copyright)") }
    if ($library.repository.url) { $catalogLines.Add("Repository: $($library.repository.url)") }
    $catalogLines.Add("Nuspec evidence: $($library.sourcePackage.nuspec.path)")
    $catalogLines.Add('License/notice material:')
    foreach ($material in $library.materials) {
        $catalogLines.Add("  - $($material.kind): $($material.path) [sha256 $($material.sha256)]")
    }
    $catalogLines.Add('')
}

$outputParent = Split-Path -Parent $outputFullPath
$outputLeaf = Split-Path -Leaf $outputFullPath
$null = [IO.Directory]::CreateDirectory($outputParent)
$stageLeafPrefix = ".$outputLeaf.stage-"
$stage = Join-Path $outputParent ($stageLeafPrefix + [guid]::NewGuid().ToString('N'))
try {
    $null = [IO.Directory]::CreateDirectory($stage)
    foreach ($copyDestination in Get-OrdinalStrings $copyPlan.Keys) {
        $destination = Join-Path $stage ($copyDestination -replace '/', [IO.Path]::DirectorySeparatorChar)
        $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $destination))
        [IO.File]::Copy([string] $copyPlan[$copyDestination].Source, $destination, $false)
    }
    foreach ($product in $productRecords) {
        Write-DeterministicJson -Path (Join-Path $stage "inventory/$($product.product).json") -Value $product
    }
    Write-DeterministicJson -Path (Join-Path $stage 'index.json') -Value $index
    Write-Utf8Lf -Path (Join-Path $stage 'THIRD-PARTY-NOTICES.txt') -Text ($catalogLines -join "`n")

    $manifestLines = [Collections.Generic.List[string]]::new()
    [string[]] $manifestFiles = @(Get-OrdinalStrings @(
        Get-ChildItem -LiteralPath $stage -File -Recurse |
            ForEach-Object { Get-ForwardRelativePath -BasePath $stage -Path $_.FullName }
    ))
    foreach ($relativePath in $manifestFiles) {
        $manifestLines.Add("$(Get-Sha256 (Join-Path $stage ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar))) *$relativePath")
    }
    Write-Utf8Lf -Path (Join-Path $stage 'MANIFEST.sha256') -Text ($manifestLines -join "`n")
    [IO.Directory]::Move($stage, $outputFullPath)
}
catch {
    $originalError = $_
    if (Test-Path -LiteralPath $stage) {
        try {
            Remove-GuardedStageDirectory -StagePath $stage -ExpectedParent $outputParent `
                -ExpectedLeafPrefix $stageLeafPrefix
        }
        catch {
            throw "Notice generation failed and guarded staging cleanup refused '$stage': $($_.Exception.Message) Original failure: $($originalError.Exception.Message)"
        }
    }
    throw $originalError
}

Write-Host "Third-party notice evidence written to '$outputFullPath'."
Write-Host "Inventoried $($actualLibraryNames.Count) external libraries across $($productRecords.Count) payloads."
