#Requires -Version 7.0
#Requires -PSEdition Core

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:PolicySchema = 'phoenixinspect.prerelease-sbom-policy/v1'
$script:EvidenceSchema = 'phoenixinspect.local-prerelease-sbom-evidence/v1'
$script:RepositoryUrl = 'https://github.com/VladimirReshetnikov/PhoenixInspect'
$script:EvidencePath = 'SBOM-EVIDENCE.json'
$script:ArtifactManifestPath = 'ARTIFACT-MANIFEST.txt'
$script:DefaultPolicyPath = Join-Path $PSScriptRoot 'prerelease-sbom.policy.json'
$script:Utf8NoBom = [Text.UTF8Encoding]::new($false)
$script:StrictUtf8 = [Text.UTF8Encoding]::new($false, $true)

function Get-Sha256Bytes {
    param([Parameter(Mandatory)][byte[]] $Bytes)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-Sha256Stream {
    param([Parameter(Mandatory)][IO.Stream] $Stream)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($hash.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
    }
}

function Get-OrdinalStrings {
    param([Collections.IEnumerable] $Values)
    [string[]] $result = @(foreach ($value in $Values) { [string] $value })
    [Array]::Sort($result, [StringComparer]::Ordinal)
    return $result
}

function Test-ByteArraysEqual {
    param([byte[]] $First, [byte[]] $Second)
    if ($null -eq $First -or $null -eq $Second -or $First.Length -ne $Second.Length) { return $false }
    return [Security.Cryptography.CryptographicOperations]::FixedTimeEquals($First, $Second)
}

function Assert-SafeRelativePath {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string] $Context)
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Contains('\') -or $Path.StartsWith('/') -or
        [IO.Path]::IsPathRooted($Path) -or $Path.IndexOf([char]0) -ge 0) {
        throw "$Context path '$Path' is not a canonical forward-slash relative path."
    }
    foreach ($segment in $Path.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -in @('.', '..') -or
            $segment.EndsWith('.') -or $segment.EndsWith(' ') -or $segment.Contains(':')) {
            throw "$Context path '$Path' contains unsafe segment '$segment'."
        }
    }
}

function Resolve-ChildPath {
    param([string] $Root, [string] $RelativePath, [string] $Context)
    Assert-SafeRelativePath $RelativePath $Context
    $rootPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $candidate.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Context path '$RelativePath' escapes '$rootPath'."
    }
    return $candidate
}

function Assert-NoReparsePoints {
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][string] $Context)
    $item = Get-Item -LiteralPath $Root -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Context root '$Root' is a reparse point."
    }
    foreach ($child in Get-ChildItem -LiteralPath $Root -Force -Recurse) {
        if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Context contains reparse point '$($child.FullName)'."
        }
    }
}

function Read-BoundedFileBytes {
    param([string] $Path, [long] $MaximumBytes, [string] $Context)
    $file = Get-Item -LiteralPath $Path -Force
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $file.Length -le 0 -or $file.Length -gt $MaximumBytes) {
        throw "$Context '$Path' is missing, unsafe, empty, or exceeds $MaximumBytes bytes."
    }
    return ,[IO.File]::ReadAllBytes($file.FullName)
}

function Assert-JsonNoDuplicateProperties {
    param([Text.Json.JsonElement] $Element, [string] $Context)
    if ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) { throw "JSON repeats property '$($property.Name)' at $Context." }
            Assert-JsonNoDuplicateProperties $property.Value "$Context.$($property.Name)"
        }
    }
    elseif ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-JsonNoDuplicateProperties $item "$Context[$index]"
            $index++
        }
    }
}

function ConvertFrom-StrictJsonBytes {
    param([byte[]] $Bytes, [string] $Context, [int] $MaximumDepth = 64)
    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 16MB -or
        ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xef -and $Bytes[1] -eq 0xbb -and $Bytes[2] -eq 0xbf)) {
        throw "$Context must be nonempty, BOM-free, and bounded to 16 MiB."
    }
    try { $null = $script:StrictUtf8.GetString($Bytes) }
    catch { throw "$Context is not valid UTF-8." }
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    try {
        try {
            $document = [Text.Json.JsonDocument]::Parse($stream, [Text.Json.JsonDocumentOptions]@{
                AllowTrailingCommas = $false
                CommentHandling = [Text.Json.JsonCommentHandling]::Disallow
                MaxDepth = $MaximumDepth
            })
        }
        catch { throw "$Context is malformed JSON: $($_.Exception.Message)" }
        try {
            Assert-JsonNoDuplicateProperties $document.RootElement '$'
            return ConvertFrom-JsonElementPreservingOrder $document.RootElement '$'
        }
        finally { $document.Dispose() }
    }
    finally { $stream.Dispose() }
}

function ConvertFrom-JsonElementPreservingOrder {
    param([Parameter(Mandatory)][Text.Json.JsonElement] $Element, [Parameter(Mandatory)][string] $Context)
    switch ($Element.ValueKind) {
        ([Text.Json.JsonValueKind]::Object) {
            $result = [ordered]@{}
            foreach ($property in $Element.EnumerateObject()) {
                $result.Add($property.Name, (ConvertFrom-JsonElementPreservingOrder $property.Value "$Context.$($property.Name)"))
            }
            return $result
        }
        ([Text.Json.JsonValueKind]::Array) {
            $items = [Collections.Generic.List[object]]::new()
            $index = 0
            foreach ($item in $Element.EnumerateArray()) {
                $items.Add((ConvertFrom-JsonElementPreservingOrder $item "$Context[$index]"))
                $index++
            }
            return ,$items.ToArray()
        }
        ([Text.Json.JsonValueKind]::String) { return $Element.GetString() }
        ([Text.Json.JsonValueKind]::Number) {
            [long] $integer = 0
            if ($Element.TryGetInt64([ref]$integer)) { return $integer }
            [decimal] $decimal = 0
            if ($Element.TryGetDecimal([ref]$decimal)) { return $decimal }
            return $Element.GetDouble()
        }
        ([Text.Json.JsonValueKind]::True) { return $true }
        ([Text.Json.JsonValueKind]::False) { return $false }
        ([Text.Json.JsonValueKind]::Null) { return $null }
        default { throw "JSON $Context uses unsupported value kind '$($Element.ValueKind)'." }
    }
}

function Assert-ExactKeys {
    param([Collections.IDictionary] $Value, [string[]] $Keys, [string] $Context)
    if ($null -eq $Value -or (@($Value.Keys) -join "`n") -cne ($Keys -join "`n")) {
        throw "$Context keys/order are invalid; expected $($Keys -join ', ')."
    }
}

function ConvertTo-CanonicalJsonBytes {
    param([Parameter(Mandatory)] $Value)
    $json = (ConvertTo-Json -InputObject $Value -Depth 64).Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
    return ,$script:Utf8NoBom.GetBytes($json)
}

function Get-PrereleaseSbomPolicy {
    [CmdletBinding()]
    param([string] $PolicyPath = $script:DefaultPolicyPath)

    $fullPath = [IO.Path]::GetFullPath($PolicyPath)
    [byte[]] $bytes = Read-BoundedFileBytes $fullPath 64KB 'SBOM policy'
    $policy = ConvertFrom-StrictJsonBytes $bytes 'SBOM policy'
    Assert-ExactKeys $policy @('schema','format','namespaceBase','supplier','runtimeIdentifier','tool','generation') 'SBOM policy'
    Assert-ExactKeys $policy.format @('name','version','spdxVersion','manifestPath') 'SBOM policy format'
    Assert-ExactKeys $policy.tool @('name','version','platform','asset','authenticode','versionInfo') 'SBOM policy tool'
    Assert-ExactKeys $policy.tool.asset @('url','size','sha256') 'SBOM policy tool asset'
    Assert-ExactKeys $policy.tool.authenticode @('status','signerSubject','signerThumbprint') 'SBOM policy Authenticode'
    Assert-ExactKeys $policy.tool.versionInfo @('fileVersion','productVersion','commandVersion') 'SBOM policy version info'
    Assert-ExactKeys $policy.generation @('parallelism','fetchLicenseInformation','parsePackageMetadata','followSymlinks','deleteExistingManifest','rawManifestSha256Path','requiredPreGenerationFiles') 'SBOM generation policy'
    if ($policy.schema -cne $script:PolicySchema -or $policy.format.name -cne 'SPDX' -or
        $policy.format.version -cne '2.2' -or $policy.format.spdxVersion -cne 'SPDX-2.2' -or
        $policy.format.manifestPath -cne '_manifest/spdx_2.2/manifest.spdx.json' -or
        $policy.namespaceBase -cne "$script:RepositoryUrl/sbom" -or
        $policy.supplier -cne 'Vladimir Reshetnikov' -or $policy.runtimeIdentifier -cne 'win-x64' -or
        $policy.tool.name -cne 'Microsoft.SBOMTool' -or $policy.tool.version -cne '4.1.5' -or
        $policy.tool.platform -cne 'win-x64' -or
        $policy.tool.asset.url -cne 'https://github.com/microsoft/sbom-tool/releases/download/v4.1.5/sbom-tool-win-x64.exe' -or
        $policy.tool.asset.size -isnot [long] -or [long]$policy.tool.asset.size -ne 81033848 -or
        $policy.tool.asset.sha256 -cne '625767b371b7fdd58f40f618b8a86da0247a33c89e419039c86b4edba1dad4b5' -or
        $policy.tool.authenticode.status -cne 'Valid' -or
        $policy.tool.authenticode.signerSubject -cne 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US' -or
        $policy.tool.authenticode.signerThumbprint -cne '3F56A45111684D454E231CFDC4DA5C8D370F9816' -or
        $policy.tool.versionInfo.fileVersion -cne '4.1.5.0' -or
        $policy.tool.versionInfo.productVersion -cne '4.1.5' -or
        $policy.tool.versionInfo.commandVersion -cne '4.1.5' -or
        $policy.generation.parallelism -isnot [long] -or [long]$policy.generation.parallelism -ne 2 -or
        $policy.generation.fetchLicenseInformation -isnot [bool] -or $policy.generation.fetchLicenseInformation -or
        $policy.generation.parsePackageMetadata -isnot [bool] -or -not $policy.generation.parsePackageMetadata -or
        $policy.generation.followSymlinks -isnot [bool] -or $policy.generation.followSymlinks -or
        $policy.generation.deleteExistingManifest -isnot [bool] -or $policy.generation.deleteExistingManifest -or
        $policy.generation.rawManifestSha256Path -cne '_manifest/spdx_2.2/manifest.spdx.json.sha256' -or
        @($policy.generation.requiredPreGenerationFiles).Count -ne 1 -or
        [string]$policy.generation.requiredPreGenerationFiles[0] -cne $script:ArtifactManifestPath) {
        throw 'SBOM policy does not match the exact reviewed SPDX 2.2 / Microsoft SBOM Tool 4.1.5 contract.'
    }
    return [pscustomobject]@{ Path = $fullPath; Sha256 = Get-Sha256Bytes $bytes; Data = $policy }
}

function Get-PrereleaseSbomBuildEvidenceDescriptor {
    [CmdletBinding()]
    param([string] $PolicyPath = $script:DefaultPolicyPath)
    $policy = Get-PrereleaseSbomPolicy $PolicyPath
    return [ordered]@{
        format = $policy.Data.format.spdxVersion
        toolName = $policy.Data.tool.name
        version = $policy.Data.tool.version
        assetSha256 = $policy.Data.tool.asset.sha256
        productSpecific = $true
    }
}

function Get-PrereleaseSbomSourceTimestamp {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RepositoryRoot, [Parameter(Mandatory)][string] $SourceCommit)
    if ($SourceCommit -cnotmatch '^[0-9a-f]{40}$') { throw "Source commit '$SourceCommit' is not a canonical SHA-1 name." }
    $result = Invoke-BoundedProcess -FileName 'git' -Arguments @('show','-s','--format=%cI',$SourceCommit) `
        -WorkingDirectory $RepositoryRoot -Description 'Reading source commit timestamp' -TimeoutSeconds 30
    $text = $result.StandardOutput.Trim()
    if ($text.Contains("`n") -or [string]::IsNullOrWhiteSpace($text)) { throw 'Git did not return exactly one commit timestamp.' }
    $parsed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParseExact($text, 'yyyy-MM-ddTHH:mm:ssK', [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::None, [ref]$parsed)) {
        throw "Git returned non-canonical commit timestamp '$text'."
    }
    return $parsed.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)
}

function Invoke-BoundedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $FileName,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $WorkingDirectory,
        [Parameter(Mandatory)][string] $Description,
        [ValidateRange(1, 900)][int] $TimeoutSeconds = 120
    )
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $primaryError = $null
    $result = $null
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    try {
        if (-not $process.Start()) { throw "Could not start $Description." }
        $started = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "$Description timed out after $TimeoutSeconds seconds."
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($stdout.Length -gt 16MB -or $stderr.Length -gt 16MB) {
            throw "$Description produced more than the 16 MiB transcript bound."
        }
        if ($process.ExitCode -ne 0) {
            throw "$Description failed with exit code $($process.ExitCode). stdout: $($stdout.Trim()) stderr: $($stderr.Trim())"
        }
        $result = [pscustomobject]@{ ExitCode = $process.ExitCode; StandardOutput = $stdout; StandardError = $stderr }
    }
    catch {
        $primaryError = $_
    }
    finally {
        if ($started) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill($true)
                    if (-not $process.WaitForExit(5000)) {
                        throw 'process tree did not terminate within the five-second cleanup bound'
                    }
                }
            }
            catch { $cleanupErrors.Add("kill/wait: $($_.Exception.Message)") }
        }
        try { $process.Dispose() }
        catch { $cleanupErrors.Add("dispose: $($_.Exception.Message)") }
    }
    if ($null -ne $primaryError) {
        if ($cleanupErrors.Count -ne 0) {
            $primaryError.Exception.Data['PhoenixInspectCleanupErrors'] = $cleanupErrors -join '; '
        }
        [Runtime.ExceptionServices.ExceptionDispatchInfo]::Capture($primaryError.Exception).Throw()
        throw 'Unreachable primary process failure.'
    }
    if ($cleanupErrors.Count -ne 0) {
        throw "$Description completed but cleanup failed: $($cleanupErrors -join '; ')"
    }
    return $result
}

function Test-PrereleaseSbomTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ToolPath,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    if (-not $IsWindows) { throw 'The pinned prerelease SBOM executable is the win-x64 asset and can only run on Windows.' }
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $fullPath = [IO.Path]::GetFullPath($ToolPath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "SBOM tool '$fullPath' does not exist." }
    $item = Get-Item -LiteralPath $fullPath -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.Length -ne [long]$policy.tool.asset.size) {
        throw "SBOM tool '$fullPath' is a reparse point or has unexpected size $($item.Length)."
    }
    $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne [string]$policy.tool.asset.sha256) { throw "SBOM tool '$fullPath' SHA-256 is '$actualHash', not the policy pin." }
    $signature = Get-AuthenticodeSignature -LiteralPath $fullPath
    if ([string]$signature.Status -cne [string]$policy.tool.authenticode.status -or
        $null -eq $signature.SignerCertificate -or
        [string]$signature.SignerCertificate.Subject -cne [string]$policy.tool.authenticode.signerSubject -or
        [string]$signature.SignerCertificate.Thumbprint -cne [string]$policy.tool.authenticode.signerThumbprint) {
        throw "SBOM tool '$fullPath' does not have the exact pinned valid Microsoft Authenticode identity."
    }
    if ([string]$item.VersionInfo.FileVersion -cne [string]$policy.tool.versionInfo.fileVersion -or
        [string]$item.VersionInfo.ProductVersion -cne [string]$policy.tool.versionInfo.productVersion) {
        throw "SBOM tool '$fullPath' version resources do not match policy."
    }
    $versionResult = Invoke-BoundedProcess $fullPath @('--version') (Split-Path -Parent $fullPath) `
        'Checking Microsoft SBOM Tool command version' 30
    if (-not [string]::IsNullOrWhiteSpace($versionResult.StandardError) -or
        $versionResult.StandardOutput.Trim() -cne [string]$policy.tool.versionInfo.commandVersion) {
        throw "SBOM tool '$fullPath' command version transcript is not exactly '$($policy.tool.versionInfo.commandVersion)'."
    }
    return [pscustomobject]@{
        Path = $fullPath
        Name = $policy.tool.name
        Version = $policy.tool.version
        Size = $item.Length
        Sha256 = $actualHash
        SignerSubject = $signature.SignerCertificate.Subject
        SignerThumbprint = $signature.SignerCertificate.Thumbprint
    }
}

function Resolve-PrereleaseSbomTool {
    [CmdletBinding()]
    param(
        [string] $ToolPath,
        [string] $DownloadDirectory,
        [string] $PolicyPath = $script:DefaultPolicyPath,
        [ValidateRange(10, 600)][int] $TimeoutSeconds = 180
    )
    if ($ToolPath) { return Test-PrereleaseSbomTool $ToolPath $PolicyPath }
    if ([string]::IsNullOrWhiteSpace($DownloadDirectory)) { throw 'DownloadDirectory is required when ToolPath is not supplied.' }
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $directory = [IO.Path]::GetFullPath($DownloadDirectory)
    $null = [IO.Directory]::CreateDirectory($directory)
    $directoryItem = Get-Item -LiteralPath $directory -Force
    if (($directoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "SBOM tool download directory '$directory' is a reparse point." }
    $destination = Join-Path $directory 'sbom-tool-win-x64-v4.1.5.exe'
    if (Test-Path -LiteralPath $destination) { return Test-PrereleaseSbomTool $destination $PolicyPath }
    $temporary = Join-Path $directory ('.sbom-tool-win-x64-v4.1.5.' + [guid]::NewGuid().ToString('N') + '.download')
    try {
        $handler = [Net.Http.HttpClientHandler]::new()
        $handler.AllowAutoRedirect = $true
        $handler.MaxAutomaticRedirections = 5
        $client = [Net.Http.HttpClient]::new($handler)
        $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
        try {
            # ResponseHeadersRead completes when headers arrive, so HttpClient.Timeout alone would not bound a
            # stalled content stream. One token covers headers, every content read/write, and the durable flush.
            $client.Timeout = [Threading.Timeout]::InfiniteTimeSpan
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, [string]$policy.tool.asset.url)
            try {
                $response = $client.SendAsync(
                    $request,
                    [Net.Http.HttpCompletionOption]::ResponseHeadersRead,
                    $timeout.Token).GetAwaiter().GetResult()
                try {
                    $response.EnsureSuccessStatusCode()
                    $contentLength = $response.Content.Headers.ContentLength
                    if ($null -ne $contentLength -and
                        [long]$contentLength -ne [long]$policy.tool.asset.size) {
                        throw "SBOM tool download Content-Length is $contentLength, not $($policy.tool.asset.size)."
                    }
                    $source = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                    try {
                        $target = [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write,
                            [IO.FileShare]::None, 1MB, [IO.FileOptions]::SequentialScan)
                        try {
                            $buffer = [byte[]]::new(1MB)
                            [long] $total = 0
                            while (($read = $source.ReadAsync(
                                    $buffer,
                                    0,
                                    $buffer.Length,
                                    $timeout.Token).GetAwaiter().GetResult()) -gt 0) {
                                $total += $read
                                if ($total -gt [long]$policy.tool.asset.size) { throw 'SBOM tool download exceeded its exact policy size.' }
                                $target.WriteAsync($buffer, 0, $read, $timeout.Token).GetAwaiter().GetResult()
                            }
                            if ($total -ne [long]$policy.tool.asset.size) {
                                throw "SBOM tool download produced $total bytes, not the exact policy size $($policy.tool.asset.size)."
                            }
                            $target.FlushAsync($timeout.Token).GetAwaiter().GetResult()
                            $target.Flush($true)
                        }
                        finally { $target.Dispose() }
                    }
                    finally { $source.Dispose() }
                }
                finally { $response.Dispose() }
            }
            finally { $request.Dispose() }
        }
        catch [OperationCanceledException] {
            if ($timeout.IsCancellationRequested) {
                throw "SBOM tool download exceeded its $TimeoutSeconds-second wall-clock bound."
            }
            throw
        }
        finally { $timeout.Dispose(); $client.Dispose(); $handler.Dispose() }
        $null = Test-PrereleaseSbomTool $temporary $PolicyPath
        [IO.File]::Move($temporary, $destination)
        return Test-PrereleaseSbomTool $destination $PolicyPath
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { [IO.File]::Delete($temporary) }
    }
}

function ConvertTo-SbomNamespaceUniquePart {
    param([string] $SourceCommit, [string] $InventoryName, [string] $Version)
    if ($SourceCommit -cnotmatch '^[0-9a-f]{40}$' -or $InventoryName -cnotmatch '^[a-z][a-z0-9-]*$' -or
        $Version -cnotmatch '^[0-9A-Za-z][0-9A-Za-z.-]*$') { throw 'Source commit, inventory name, or version cannot form a canonical SBOM namespace.' }
    return "$SourceCommit-$InventoryName-$Version-win-x64"
}

function Get-PrereleaseSbomExpectedNamespace {
    param([Collections.IDictionary] $Policy, [string] $ProductName, [string] $Version, [string] $UniquePart)
    $escapedProduct = [Uri]::EscapeDataString($ProductName).Replace('%20', '%20')
    $escapedVersion = [Uri]::EscapeDataString($Version)
    return "$($Policy.namespaceBase)/$escapedProduct/$escapedVersion/$UniquePart"
}

function Assert-PrereleaseSbomProductInventoryIdentity {
    param(
        [Parameter(Mandatory)][string] $ProductName,
        [Parameter(Mandatory)][string] $InventoryName
    )

    $expectedInventory = switch -CaseSensitive ($ProductName) {
        'PhoenixInspect CLI' { 'cli'; break }
        'PhoenixInspect Desktop' { 'desktop'; break }
        default { throw "Unsupported prerelease SBOM product '$ProductName'." }
    }
    if ($InventoryName -cne $expectedInventory) {
        throw "Prerelease SBOM product '$ProductName' requires inventory '$expectedInventory', not '$InventoryName'."
    }
}

function New-PrereleaseSbomEvidenceBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ProductName,
        [Parameter(Mandatory)][string] $InventoryName,
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][string] $SourceCommit,
        [Parameter(Mandatory)][string] $SourceTree,
        [Parameter(Mandatory)][string] $SourceTimestamp,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    if ([string]::IsNullOrWhiteSpace($ProductName) -or $ProductName.Length -gt 100 -or
        $InventoryName -cnotmatch '^[a-z][a-z0-9-]*$' -or $Version -cnotmatch '^[0-9A-Za-z][0-9A-Za-z.-]*$' -or
        $SourceCommit -cnotmatch '^[0-9a-f]{40}$' -or $SourceTree -cnotmatch '^[0-9a-f]{40}$' -or
        $SourceTimestamp -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$') {
        throw 'Cannot create SBOM evidence from non-canonical product, source, or timestamp inputs.'
    }
    Assert-PrereleaseSbomProductInventoryIdentity $ProductName $InventoryName
    $parsedTimestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParseExact($SourceTimestamp, 'yyyy-MM-ddTHH:mm:ssZ',
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal,
            [ref]$parsedTimestamp)) { throw "Source timestamp '$SourceTimestamp' is invalid." }
    $policyRecord = Get-PrereleaseSbomPolicy $PolicyPath
    $policy = $policyRecord.Data
    $uniquePart = ConvertTo-SbomNamespaceUniquePart $SourceCommit $InventoryName $Version
    $namespace = Get-PrereleaseSbomExpectedNamespace $policy $ProductName $Version $uniquePart
    $evidence = [ordered]@{
        schema = $script:EvidenceSchema
        scope = [ordered]@{
            kind = 'UnsignedLocalValidation'
            localOnly = $true
            redistributionApproved = $false
            legalClearanceClaimed = $false
            slsaProvenanceClaimed = $false
            reproducibleBuildClaimed = $false
            w8_10ClosureClaimed = $false
            releaseClosureClaimed = $false
        }
        source = [ordered]@{
            repositoryUrl = $script:RepositoryUrl
            objectFormat = 'sha1'
            commit = $SourceCommit
            tree = $SourceTree
            commitTimestamp = $SourceTimestamp
        }
        product = [ordered]@{
            name = $ProductName
            inventory = $InventoryName
            version = $Version
            runtimeIdentifier = $policy.runtimeIdentifier
            selfContained = $true
        }
        format = [ordered]@{
            name = $policy.format.name
            version = $policy.format.version
            spdxVersion = $policy.format.spdxVersion
            documentNamespace = $namespace
            manifestPath = $policy.format.manifestPath
        }
        tool = [ordered]@{
            name = $policy.tool.name
            version = $policy.tool.version
            platform = $policy.tool.platform
            assetSize = [long]$policy.tool.asset.size
            assetSha256 = $policy.tool.asset.sha256
            authenticodeSignerSubject = $policy.tool.authenticode.signerSubject
            authenticodeSignerThumbprint = $policy.tool.authenticode.signerThumbprint
        }
        generation = [ordered]@{
            productSpecificComponents = $true
            exactFileHashes = $true
            exactDependencySet = $true
            licenseNetworkEnabled = $false
            packageMetadataParsing = $true
            followSymlinks = $false
            parallelism = 2
            capturedBeforeGeneration = $true
            excludedManifestDirectory = '_manifest'
            rawToolSidecarVerifiedAndRemoved = $true
            requiredPreGenerationFiles = @($script:ArtifactManifestPath)
        }
        policy = [ordered]@{
            path = 'eng/prerelease-sbom.policy.json'
            sha256 = $policyRecord.Sha256
        }
    }
    return ConvertTo-CanonicalJsonBytes $evidence
}

function Write-PrereleaseSbomEvidence {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][byte[]] $Bytes,
        [string] $PolicyPath = $script:DefaultPolicyPath)
    $null = Test-PrereleaseSbomEvidenceBytes $Bytes $PolicyPath
    $fullPath = [IO.Path]::GetFullPath($Path)
    $parent = Split-Path -Parent $fullPath
    $null = [IO.Directory]::CreateDirectory($parent)
    if (Test-Path -LiteralPath $fullPath) { throw "SBOM evidence destination '$fullPath' already exists." }
    [IO.File]::WriteAllBytes($fullPath, $Bytes)
}

function Test-PrereleaseSbomEvidenceBytes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][byte[]] $Bytes, [string] $PolicyPath = $script:DefaultPolicyPath)
    if ($Bytes.Length -gt 1MB) { throw 'SBOM-EVIDENCE.json exceeds its 1 MiB bound.' }
    $text = try { $script:StrictUtf8.GetString($Bytes) } catch { throw 'SBOM-EVIDENCE.json is not valid UTF-8.' }
    if ($text.Contains("`r") -or -not $text.EndsWith("`n") -or $text.EndsWith("`n`n")) {
        throw 'SBOM-EVIDENCE.json must use LF only and exactly one final newline.'
    }
    $evidence = ConvertFrom-StrictJsonBytes $Bytes 'SBOM-EVIDENCE.json'
    Assert-ExactKeys $evidence @('schema','scope','source','product','format','tool','generation','policy') 'SBOM evidence'
    Assert-ExactKeys $evidence.scope @('kind','localOnly','redistributionApproved','legalClearanceClaimed','slsaProvenanceClaimed','reproducibleBuildClaimed','w8_10ClosureClaimed','releaseClosureClaimed') 'SBOM evidence scope'
    Assert-ExactKeys $evidence.source @('repositoryUrl','objectFormat','commit','tree','commitTimestamp') 'SBOM evidence source'
    Assert-ExactKeys $evidence.product @('name','inventory','version','runtimeIdentifier','selfContained') 'SBOM evidence product'
    Assert-ExactKeys $evidence.format @('name','version','spdxVersion','documentNamespace','manifestPath') 'SBOM evidence format'
    Assert-ExactKeys $evidence.tool @('name','version','platform','assetSize','assetSha256','authenticodeSignerSubject','authenticodeSignerThumbprint') 'SBOM evidence tool'
    Assert-ExactKeys $evidence.generation @('productSpecificComponents','exactFileHashes','exactDependencySet','licenseNetworkEnabled','packageMetadataParsing','followSymlinks','parallelism','capturedBeforeGeneration','excludedManifestDirectory','rawToolSidecarVerifiedAndRemoved','requiredPreGenerationFiles') 'SBOM evidence generation'
    Assert-ExactKeys $evidence.policy @('path','sha256') 'SBOM evidence policy'
    $policyRecord = Get-PrereleaseSbomPolicy $PolicyPath
    $policy = $policyRecord.Data
    $falseFlags = @('redistributionApproved','legalClearanceClaimed','slsaProvenanceClaimed','reproducibleBuildClaimed','w8_10ClosureClaimed','releaseClosureClaimed')
    if ($evidence.schema -cne $script:EvidenceSchema -or $evidence.scope.kind -cne 'UnsignedLocalValidation' -or
        $evidence.scope.localOnly -isnot [bool] -or -not $evidence.scope.localOnly) {
        throw 'SBOM evidence has an invalid local-only scope.'
    }
    foreach ($flag in $falseFlags) {
        if ($evidence.scope[$flag] -isnot [bool] -or $evidence.scope[$flag]) { throw "SBOM evidence overclaims scope flag '$flag'." }
    }
    Assert-PrereleaseSbomProductInventoryIdentity ([string]$evidence.product.name) ([string]$evidence.product.inventory)
    if ($evidence.source.repositoryUrl -cne $script:RepositoryUrl -or $evidence.source.objectFormat -cne 'sha1' -or
        [string]$evidence.source.commit -cnotmatch '^[0-9a-f]{40}$' -or
        [string]$evidence.source.tree -cnotmatch '^[0-9a-f]{40}$' -or
        [string]$evidence.source.commitTimestamp -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$') {
        throw 'SBOM evidence source identity is invalid.'
    }
    $timestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParseExact([string]$evidence.source.commitTimestamp, 'yyyy-MM-ddTHH:mm:ssZ',
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal,
            [ref]$timestamp)) { throw 'SBOM evidence source timestamp is invalid.' }
    if ([string]::IsNullOrWhiteSpace([string]$evidence.product.name) -or ([string]$evidence.product.name).Length -gt 100 -or
        [string]$evidence.product.inventory -cnotmatch '^[a-z][a-z0-9-]*$' -or
        [string]$evidence.product.version -cnotmatch '^[0-9A-Za-z][0-9A-Za-z.-]*$' -or
        $evidence.product.runtimeIdentifier -cne $policy.runtimeIdentifier -or
        $evidence.product.selfContained -isnot [bool] -or -not $evidence.product.selfContained) {
        throw 'SBOM evidence product identity is invalid.'
    }
    $unique = ConvertTo-SbomNamespaceUniquePart $evidence.source.commit $evidence.product.inventory $evidence.product.version
    $expectedNamespace = Get-PrereleaseSbomExpectedNamespace $policy $evidence.product.name $evidence.product.version $unique
    if ($evidence.format.name -cne $policy.format.name -or $evidence.format.version -cne $policy.format.version -or
        $evidence.format.spdxVersion -cne $policy.format.spdxVersion -or
        $evidence.format.documentNamespace -cne $expectedNamespace -or
        $evidence.format.manifestPath -cne $policy.format.manifestPath) {
        throw 'SBOM evidence format or namespace is invalid.'
    }
    if ($evidence.tool.name -cne $policy.tool.name -or $evidence.tool.version -cne $policy.tool.version -or
        $evidence.tool.platform -cne $policy.tool.platform -or $evidence.tool.assetSize -isnot [long] -or
        [long]$evidence.tool.assetSize -ne [long]$policy.tool.asset.size -or
        $evidence.tool.assetSha256 -cne $policy.tool.asset.sha256 -or
        $evidence.tool.authenticodeSignerSubject -cne $policy.tool.authenticode.signerSubject -or
        $evidence.tool.authenticodeSignerThumbprint -cne $policy.tool.authenticode.signerThumbprint) {
        throw 'SBOM evidence tool identity is invalid.'
    }
    foreach ($flag in @('productSpecificComponents','exactFileHashes','exactDependencySet','packageMetadataParsing','capturedBeforeGeneration','rawToolSidecarVerifiedAndRemoved')) {
        if ($evidence.generation[$flag] -isnot [bool] -or -not $evidence.generation[$flag]) { throw "SBOM generation flag '$flag' must be true." }
    }
    foreach ($flag in @('licenseNetworkEnabled','followSymlinks')) {
        if ($evidence.generation[$flag] -isnot [bool] -or $evidence.generation[$flag]) { throw "SBOM generation flag '$flag' must be false." }
    }
    if ($evidence.generation.parallelism -isnot [long] -or [long]$evidence.generation.parallelism -ne 2 -or
        $evidence.generation.excludedManifestDirectory -cne '_manifest' -or
        @($evidence.generation.requiredPreGenerationFiles).Count -ne 1 -or
        [string]$evidence.generation.requiredPreGenerationFiles[0] -cne $script:ArtifactManifestPath -or
        $evidence.policy.path -cne 'eng/prerelease-sbom.policy.json' -or
        $evidence.policy.sha256 -cne $policyRecord.Sha256) {
        throw 'SBOM evidence generation or policy binding is invalid.'
    }
    [byte[]] $canonical = New-PrereleaseSbomEvidenceBytes `
        -ProductName $evidence.product.name -InventoryName $evidence.product.inventory -Version $evidence.product.version `
        -SourceCommit $evidence.source.commit -SourceTree $evidence.source.tree `
        -SourceTimestamp $evidence.source.commitTimestamp -PolicyPath $PolicyPath
    if (-not (Test-ByteArraysEqual $Bytes $canonical)) { throw 'SBOM-EVIDENCE.json is valid data but not in its exact canonical serialization.' }
    return [pscustomobject]@{
        Sha256 = Get-Sha256Bytes $Bytes
        ProductName = [string]$evidence.product.name
        InventoryName = [string]$evidence.product.inventory
        Version = [string]$evidence.product.version
        SourceCommit = [string]$evidence.source.commit
        SourceTree = [string]$evidence.source.tree
        SourceTimestamp = [string]$evidence.source.commitTimestamp
        DocumentNamespace = [string]$evidence.format.documentNamespace
        ManifestPath = [string]$evidence.format.manifestPath
        Data = $evidence
    }
}

function Assert-PrereleaseSbomEvidenceIdentity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][byte[]] $First, [Parameter(Mandatory)][byte[]] $Second,
        [string] $PolicyPath = $script:DefaultPolicyPath)
    $firstRecord = Test-PrereleaseSbomEvidenceBytes $First $PolicyPath
    $null = Test-PrereleaseSbomEvidenceBytes $Second $PolicyPath
    if (-not (Test-ByteArraysEqual $First $Second)) { throw 'Prerelease product SBOM evidence records are not byte-identical.' }
    return $firstRecord
}

function Get-NoticeDependencyContract {
    param([byte[]] $IndexBytes, [byte[]] $InventoryBytes, [string] $InventoryName)
    $index = ConvertFrom-StrictJsonBytes $IndexBytes 'THIRD-PARTY-NOTICES/index.json' 100
    $inventory = ConvertFrom-StrictJsonBytes $InventoryBytes "THIRD-PARTY-NOTICES/inventory/$InventoryName.json" 100
    if ($index.schemaVersion -isnot [long] -or [long]$index.schemaVersion -ne 1 -or
        $inventory.schemaVersion -isnot [long] -or [long]$inventory.schemaVersion -ne 1 -or
        $inventory.product -cne $InventoryName -or $inventory.runtimeTarget -cne '.NETCoreApp,Version=v10.0/win-x64') {
        throw 'Third-party notice index or product inventory identity is invalid.'
    }
    $libraryMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($library in @($index.libraries)) {
        if ($library -isnot [Collections.IDictionary] -or [string]$library.library -cnotmatch '^[^/]+/[^/]+$' -or
            [string]::IsNullOrWhiteSpace([string]$library.packageId) -or [string]::IsNullOrWhiteSpace([string]$library.version) -or
            $library.sourcePackage -isnot [Collections.IDictionary] -or $library.sourcePackage.nuspec -isnot [Collections.IDictionary]) {
            throw 'Third-party notice index contains a malformed library record.'
        }
        if (-not $libraryMap.TryAdd([string]$library.library, $library)) { throw "Third-party notice index duplicates library '$($library.library)'." }
    }
    [string[]] $names = @($inventory.externalLibraries | ForEach-Object { [string]$_ })
    [string[]] $sortedNames = @(Get-OrdinalStrings $names)
    if ($names.Count -eq 0 -or ($names -join "`n") -cne ($sortedNames -join "`n") -or
        ([Collections.Generic.HashSet[string]]::new($names, [StringComparer]::Ordinal)).Count -ne $names.Count) {
        throw 'Third-party product inventory dependency names must be nonempty, unique, and ordinal-sorted.'
    }
    $packages = [Collections.Generic.List[object]]::new()
    $packageKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $names) {
        if (-not $libraryMap.ContainsKey($name)) { throw "Product inventory dependency '$name' is absent from the notice index." }
        $library = $libraryMap[$name]
        if (@($library.products) -cnotcontains $InventoryName) { throw "Notice library '$name' does not declare product '$InventoryName'." }
        $separator = $name.LastIndexOf('/')
        $libraryId = $name.Substring(0, $separator)
        $libraryVersion = $name.Substring($separator + 1)
        $sourcePackageId = [string]$library.sourcePackage.id
        if ([string]$library.packageId -cne $libraryId -or [string]$library.version -cne $libraryVersion -or
            [string]$library.sourcePackage.version -cne $libraryVersion -or
            $sourcePackageId -cnotmatch '^[A-Za-z0-9_.-]+$' -or
            [string]$library.sourcePackage.nuspec.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "Notice library '$name' has inconsistent package or nuspec identity."
        }
        $nuspecPath = [string]$library.sourcePackage.nuspec.path
        Assert-SafeRelativePath $nuspecPath "Notice library '$name' nuspec"
        $key = "$sourcePackageId`0$libraryVersion"
        if (-not $packageKeys.Add($key)) { throw "Product inventory maps multiple libraries to source package '$sourcePackageId/$libraryVersion'." }
        $packages.Add([pscustomobject]@{
            Library = $name
            Name = $sourcePackageId
            Version = $libraryVersion
            NuspecPath = $nuspecPath
            NuspecSha256 = [string]$library.sourcePackage.nuspec.sha256
            LicenseDeclared = if ([string]$library.licenseDeclaration.type -ceq 'expression') {
                [string]$library.licenseDeclaration.value
            }
            else {
                'NOASSERTION'
            }
            Supplier = "Organization: $([string]$library.authors)"
            Purl = "pkg:nuget/$sourcePackageId@$libraryVersion"
        })
    }
    return [pscustomobject]@{ Index = $index; Inventory = $inventory; Packages = $packages.ToArray() }
}

function Get-FileRecordMap {
    param([Parameter(Mandatory)][object[]] $PayloadFiles)
    $map = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($file in $PayloadFiles) {
        $path = [string]$file.Path
        Assert-SafeRelativePath $path 'Payload file'
        if ($path -ceq '_manifest/spdx_2.2/manifest.spdx.json') { continue }
        if ($path.StartsWith('_manifest/', [StringComparison]::Ordinal)) {
            throw "Payload contains unexpected validator-owned manifest file '$path'."
        }
        $sha256 = [string]$file.Sha256
        if ($sha256 -cnotmatch '^[0-9a-f]{64}$') { throw "Payload file '$path' has invalid SHA-256 '$sha256'." }
        if (-not $map.TryAdd($path, $sha256)) { throw "Payload contains duplicate file path '$path'." }
    }
    if ($map.Count -eq 0 -or -not $map.ContainsKey($script:EvidencePath) -or
        -not $map.ContainsKey($script:ArtifactManifestPath)) {
        throw 'Payload SBOM file set is empty or omits SBOM-EVIDENCE.json / ARTIFACT-MANIFEST.txt.'
    }
    return $map
}

function ConvertTo-CanonicalPrereleaseSbomBytes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][byte[]] $Bytes)

    $sbom = ConvertFrom-StrictJsonBytes $Bytes 'SPDX manifest' 100
    if ($sbom -isnot [Collections.IDictionary]) {
        throw 'SPDX manifest root must be an object.'
    }

    $files = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($file in @($sbom.files)) {
        if ($file -isnot [Collections.IDictionary] -or [string]::IsNullOrWhiteSpace([string]$file.fileName) -or
            $files.ContainsKey([string]$file.fileName)) {
            throw "SPDX canonicalization found a missing or duplicate file name '$($file.fileName)'."
        }
        $files.Add([string]$file.fileName, $file)
        $checksums = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
        foreach ($checksum in @($file.checksums)) {
            if ($checksum -isnot [Collections.IDictionary] -or
                [string]::IsNullOrWhiteSpace([string]$checksum.algorithm) -or
                $checksums.ContainsKey([string]$checksum.algorithm)) {
                throw "SPDX canonicalization found a missing or duplicate checksum for '$($file.fileName)'."
            }
            $checksums.Add([string]$checksum.algorithm, $checksum)
        }
        $file.checksums = @($checksums.Values)
        if ($file.Contains('licenseInfoInFiles')) {
            $file.licenseInfoInFiles = @(Get-OrdinalStrings @($file.licenseInfoInFiles))
        }
    }
    $sbom.files = @($files.Values)

    $packages = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($package in @($sbom.packages)) {
        if ($package -isnot [Collections.IDictionary]) { throw 'SPDX canonicalization found a non-object package.' }
        $id = [string]$package.SPDXID
        $key = if ($id -ceq 'SPDXRef-RootPackage') {
            '0'
        }
        else {
            "1`0$([string]$package.name)`0$([string]$package.versionInfo)`0$id"
        }
        if ($packages.ContainsKey($key)) {
            throw "SPDX canonicalization found duplicate package identity '$key'."
        }
        $packages.Add($key, $package)
        if ($id -ceq 'SPDXRef-RootPackage') {
            # Microsoft SBOM Tool 4.1.5 emits a fresh pkg:swid tag GUID on every run. It is optional,
            # carries no dependency identity, and is deliberately removed from the canonical contract.
            $package.externalRefs = @()
            $package.hasFiles = @(Get-OrdinalStrings @($package.hasFiles))
            if ($package.Contains('licenseInfoFromFiles')) {
                $package.licenseInfoFromFiles = @(Get-OrdinalStrings @($package.licenseInfoFromFiles))
            }
        }
        elseif ($package.Contains('externalRefs')) {
            $references = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
            foreach ($reference in @($package.externalRefs)) {
                $referenceKey = "$([string]$reference.referenceCategory)`0$([string]$reference.referenceType)`0$([string]$reference.referenceLocator)"
                if ($references.ContainsKey($referenceKey)) {
                    throw "SPDX canonicalization found duplicate package external reference '$referenceKey'."
                }
                $references.Add($referenceKey, $reference)
            }
            $package.externalRefs = @($references.Values)
        }
    }
    $sbom.packages = @($packages.Values)

    $relationships = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($relationship in @($sbom.relationships)) {
        if ($relationship -isnot [Collections.IDictionary]) { throw 'SPDX canonicalization found a non-object relationship.' }
        $relationshipKey = "$([string]$relationship.spdxElementId)`0$([string]$relationship.relationshipType)`0$([string]$relationship.relatedSpdxElement)"
        if ($relationships.ContainsKey($relationshipKey)) {
            throw "SPDX canonicalization found duplicate relationship '$relationshipKey'."
        }
        $relationships.Add($relationshipKey, $relationship)
    }
    $sbom.relationships = @($relationships.Values)
    $sbom.documentDescribes = @(Get-OrdinalStrings @($sbom.documentDescribes))
    $sbom.externalDocumentRefs = @($sbom.externalDocumentRefs)
    if ($sbom.creationInfo -is [Collections.IDictionary]) {
        $sbom.creationInfo.creators = @(Get-OrdinalStrings @($sbom.creationInfo.creators))
    }

    return ConvertTo-CanonicalJsonBytes $sbom
}

function Assert-StringSetEqual {
    param([string[]] $Expected, [string[]] $Actual, [string] $Context)
    [string[]] $left = @(Get-OrdinalStrings $Expected)
    [string[]] $right = @(Get-OrdinalStrings $Actual)
    if (($left -join "`n") -cne ($right -join "`n")) {
        $missing = @($left | Where-Object { $right -cnotcontains $_ })
        $extra = @($right | Where-Object { $left -cnotcontains $_ })
        throw "$Context differs. Missing: $($missing -join ', '). Extra: $($extra -join ', ')."
    }
}

function Test-PrereleaseSbomRecords {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][byte[]] $SbomBytes,
        [Parameter(Mandatory)][byte[]] $EvidenceBytes,
        [Parameter(Mandatory)][object[]] $PayloadFiles,
        [Parameter(Mandatory)][byte[]] $NoticeIndexBytes,
        [Parameter(Mandatory)][byte[]] $InventoryBytes,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    [byte[]] $canonicalSbomBytes = ConvertTo-CanonicalPrereleaseSbomBytes $SbomBytes
    if (-not (Test-ByteArraysEqual $SbomBytes $canonicalSbomBytes)) {
        throw 'SPDX manifest is semantically valid input but not in the canonical PhoenixInspect serialization.'
    }
    $evidenceRecord = Test-PrereleaseSbomEvidenceBytes $EvidenceBytes $PolicyPath
    $files = Get-FileRecordMap $PayloadFiles
    if ($files[$script:EvidencePath] -cne (Get-Sha256Bytes $EvidenceBytes)) {
        throw 'Payload file record for SBOM-EVIDENCE.json does not match the validated evidence bytes.'
    }
    $contract = Get-NoticeDependencyContract $NoticeIndexBytes $InventoryBytes $evidenceRecord.InventoryName
    $sbom = ConvertFrom-StrictJsonBytes $SbomBytes 'SPDX manifest' 100
    [byte[]] $canonicalBytes = ConvertTo-CanonicalPrereleaseSbomBytes $SbomBytes
    if (-not (Test-ByteArraysEqual $SbomBytes $canonicalBytes)) {
        throw 'SPDX manifest is semantically parseable but not in the exact PhoenixInspect canonical serialization.'
    }
    [string[]] $requiredRootKeys = @('files','packages','externalDocumentRefs','relationships','spdxVersion','dataLicense','SPDXID','name','documentNamespace','creationInfo','documentDescribes')
    Assert-ExactKeys $sbom $requiredRootKeys 'SPDX document'
    if ($sbom.spdxVersion -cne 'SPDX-2.2' -or $sbom.dataLicense -cne 'CC0-1.0' -or
        $sbom.SPDXID -cne 'SPDXRef-DOCUMENT' -or
        $sbom.name -cne "$($evidenceRecord.ProductName) $($evidenceRecord.Version)" -or
        $sbom.documentNamespace -cne $evidenceRecord.DocumentNamespace -or
        @($sbom.externalDocumentRefs).Count -ne 0 -or @($sbom.documentDescribes).Count -ne 1 -or
        [string]$sbom.documentDescribes[0] -cne 'SPDXRef-RootPackage') {
        throw 'SPDX document root, name, namespace, or describes declaration is invalid.'
    }
    Assert-ExactKeys $sbom.creationInfo @('created','creators') 'SPDX creationInfo'
    if ($sbom.creationInfo.created -cne $evidenceRecord.SourceTimestamp -or
        (@($sbom.creationInfo.creators) -join "`n") -cne
            ("Organization: Vladimir Reshetnikov`nTool: Microsoft.SBOMTool-4.1.5")) {
        throw 'SPDX creation timestamp or creators do not match pinned source/tool evidence.'
    }

    $allIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if (-not $allIds.Add('SPDXRef-DOCUMENT')) { throw 'Internal SPDX ID initialization failed.' }
    $fileById = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $sbomFileHashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($file in @($sbom.files)) {
        Assert-ExactKeys $file @('fileName','SPDXID','checksums','licenseConcluded','licenseInfoInFiles','copyrightText') 'SPDX file'
        $path = [string]$file.fileName
        if (-not $path.StartsWith('./', [StringComparison]::Ordinal)) { throw "SPDX file name '$path' is not root-relative." }
        $path = $path.Substring(2)
        Assert-SafeRelativePath $path 'SPDX file'
        if ($path.StartsWith('_manifest/', [StringComparison]::Ordinal)) {
            throw "SPDX improperly inventories excluded file '$path'."
        }
        $id = [string]$file.SPDXID
        if ($id -cnotmatch '^SPDXRef-File-[A-Za-z0-9.-]+$' -or -not $allIds.Add($id) -or
            -not $fileById.TryAdd($id, $path) -or -not $sbomFileHashes.TryAdd($path, '')) {
            throw "SPDX contains duplicate or malformed file path/ID '$path' / '$id'."
        }
        if ($file.licenseConcluded -cne 'NOASSERTION' -or @($file.licenseInfoInFiles).Count -ne 1 -or
            [string]$file.licenseInfoInFiles[0] -cne 'NOASSERTION' -or $file.copyrightText -cne 'NOASSERTION') {
            throw "SPDX file '$path' makes an unexpected license or copyright assertion."
        }
        $checksumAlgorithms = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $sha256 = $null
        foreach ($checksum in @($file.checksums)) {
            Assert-ExactKeys $checksum @('algorithm','checksumValue') "SPDX file '$path' checksum"
            $algorithm = [string]$checksum.algorithm
            if ($algorithm -notin @('SHA256','SHA1') -or -not $checksumAlgorithms.Add($algorithm)) {
                throw "SPDX file '$path' has duplicate or unexpected checksum algorithm '$algorithm'."
            }
            if ($algorithm -ceq 'SHA256') { $sha256 = [string]$checksum.checksumValue }
            elseif ([string]$checksum.checksumValue -cnotmatch '^[0-9a-f]{40}$') { throw "SPDX file '$path' has invalid SHA-1 syntax." }
        }
        if (($checksumAlgorithms.Count -ne 2) -or $sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "SPDX file '$path' does not contain exactly one SHA-256 and SHA-1 checksum."
        }
        $sbomFileHashes[$path] = $sha256
    }
    Assert-StringSetEqual @($files.Keys) @($sbomFileHashes.Keys) 'SPDX file set'
    foreach ($path in $files.Keys) {
        if ($files[$path] -cne $sbomFileHashes[$path]) { throw "SPDX SHA-256 mismatch for payload file '$path'." }
    }

    $rootPackage = $null
    $actualDependencies = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $dependencyIdByKey = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($package in @($sbom.packages)) {
        $id = [string]$package.SPDXID
        if ([string]::IsNullOrWhiteSpace($id) -or -not $allIds.Add($id)) { throw "SPDX contains duplicate or empty package ID '$id'." }
        if ($id -ceq 'SPDXRef-RootPackage') {
            if ($null -ne $rootPackage) { throw 'SPDX contains more than one root package.' }
            $rootPackage = $package
            continue
        }
        Assert-ExactKeys $package @('name','SPDXID','downloadLocation','filesAnalyzed','licenseConcluded','licenseDeclared','copyrightText','versionInfo','externalRefs','supplier') "SPDX dependency '$id'"
        if ($id -cnotmatch '^SPDXRef-Package-[A-F0-9]{64}$' -or [string]::IsNullOrWhiteSpace([string]$package.name) -or
            [string]::IsNullOrWhiteSpace([string]$package.versionInfo)) { throw "SPDX dependency package '$id' is malformed." }
        $key = "$($package.name)`0$($package.versionInfo)"
        if (-not $actualDependencies.TryAdd($key, "$($package.name)/$($package.versionInfo)") -or
            -not $dependencyIdByKey.TryAdd($key, $id)) { throw "SPDX duplicates dependency '$($package.name)/$($package.versionInfo)'." }
        $expectedPackage = @($contract.Packages | Where-Object {
            $_.Name -ceq [string]$package.name -and $_.Version -ceq [string]$package.versionInfo
        })
        if ($expectedPackage.Count -ne 1) {
            throw "SPDX dependency '$($package.name)/$($package.versionInfo)' is absent from the exact notice contract."
        }
        $expectedPackage = $expectedPackage[0]
        if ($package.filesAnalyzed -isnot [bool] -or $package.filesAnalyzed -or
            $package.downloadLocation -cne 'NOASSERTION' -or $package.licenseConcluded -cne 'NOASSERTION' -or
            $package.copyrightText -cne 'NOASSERTION' -or
            $package.licenseDeclared -cne $expectedPackage.LicenseDeclared -or
            $package.supplier -cne $expectedPackage.Supplier -or @($package.externalRefs).Count -ne 1) {
            throw "SPDX dependency '$($package.name)' has unexpected analysis, license, supplier, or reference fields."
        }
        $reference = $package.externalRefs[0]
        Assert-ExactKeys $reference @('referenceCategory','referenceType','referenceLocator') "SPDX dependency '$($package.name)' external reference"
        if ($reference.referenceCategory -cne 'PACKAGE-MANAGER' -or $reference.referenceType -cne 'purl' -or
            $reference.referenceLocator -cne $expectedPackage.Purl) {
            throw "SPDX dependency '$($package.name)' has an unexpected package URL."
        }
    }
    if ($null -eq $rootPackage) { throw 'SPDX root package is missing.' }
    Assert-ExactKeys $rootPackage @('name','SPDXID','downloadLocation','packageVerificationCode','filesAnalyzed','licenseConcluded','licenseInfoFromFiles','licenseDeclared','copyrightText','versionInfo','externalRefs','supplier','hasFiles') 'SPDX root package'
    if ($rootPackage.name -cne $evidenceRecord.ProductName -or $rootPackage.versionInfo -cne $evidenceRecord.Version -or
        $rootPackage.supplier -cne 'Organization: Vladimir Reshetnikov' -or
        $rootPackage.filesAnalyzed -isnot [bool] -or -not $rootPackage.filesAnalyzed -or
        $rootPackage.downloadLocation -cne 'NOASSERTION' -or $rootPackage.licenseConcluded -cne 'NOASSERTION' -or
        $rootPackage.licenseDeclared -cne 'NOASSERTION' -or $rootPackage.copyrightText -cne 'NOASSERTION' -or
        @($rootPackage.licenseInfoFromFiles).Count -ne 1 -or [string]$rootPackage.licenseInfoFromFiles[0] -cne 'NOASSERTION' -or
        @($rootPackage.externalRefs).Count -ne 0 -or
        $rootPackage.packageVerificationCode -isnot [Collections.IDictionary] -or
        [string]$rootPackage.packageVerificationCode.packageVerificationCodeValue -cnotmatch '^[0-9a-f]{40}$') {
        throw 'SPDX root package identity or assertion scope is invalid.'
    }
    [string[]] $fileSha1Values = @(
        foreach ($file in @($sbom.files)) {
            [string](($file.checksums | Where-Object { $_.algorithm -ceq 'SHA1' } | Select-Object -First 1).checksumValue)
        }
    )
    [Array]::Sort($fileSha1Values, [StringComparer]::Ordinal)
    $verificationInput = $script:Utf8NoBom.GetBytes($fileSha1Values -join '')
    $verificationAlgorithm = [Security.Cryptography.SHA1]::Create()
    try {
        $expectedVerificationCode = ([BitConverter]::ToString(
            $verificationAlgorithm.ComputeHash($verificationInput))).Replace('-', '').ToLowerInvariant()
    }
    finally { $verificationAlgorithm.Dispose() }
    if ([string]$rootPackage.packageVerificationCode.packageVerificationCodeValue -cne $expectedVerificationCode) {
        throw 'SPDX root package verification code does not match the exact sorted payload-file SHA-1 set.'
    }
    Assert-StringSetEqual @($fileById.Keys) @($rootPackage.hasFiles | ForEach-Object { [string]$_ }) 'SPDX root package hasFiles set'
    [string[]] $expectedDependencyKeys = @($contract.Packages | ForEach-Object { "$($_.Name)`0$($_.Version)" })
    Assert-StringSetEqual $expectedDependencyKeys @($actualDependencies.Keys) 'SPDX product dependency set'

    $relationshipKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($relationship in @($sbom.relationships)) {
        Assert-ExactKeys $relationship @('relationshipType','relatedSpdxElement','spdxElementId') 'SPDX relationship'
        $key = "$($relationship.spdxElementId)`0$($relationship.relationshipType)`0$($relationship.relatedSpdxElement)"
        if (-not $relationshipKeys.Add($key)) { throw "SPDX duplicates relationship '$key'." }
    }
    [string[]] $expectedRelationships = @('SPDXRef-DOCUMENT' + [char]0 + 'DESCRIBES' + [char]0 + 'SPDXRef-RootPackage')
    $expectedRelationships += @($dependencyIdByKey.Values | ForEach-Object {
        'SPDXRef-RootPackage' + [char]0 + 'DEPENDS_ON' + [char]0 + $_
    })
    Assert-StringSetEqual $expectedRelationships @($relationshipKeys) 'SPDX relationships'
    if (@($sbom.packages).Count -ne ($contract.Packages.Count + 1) -or
        @($sbom.relationships).Count -ne ($contract.Packages.Count + 1) -or
        @($sbom.files).Count -ne $files.Count) { throw 'SPDX manifest counts do not match exact product inventory and payload.' }
    return [pscustomobject]@{
        Product = $evidenceRecord.ProductName
        Version = $evidenceRecord.Version
        FileCount = $files.Count
        DependencyCount = $contract.Packages.Count
        EvidenceSha256 = $evidenceRecord.Sha256
        ManifestSha256 = Get-Sha256Bytes $SbomBytes
        DocumentNamespace = $evidenceRecord.DocumentNamespace
        InventoryName = $evidenceRecord.InventoryName
        SourceCommit = $evidenceRecord.SourceCommit
        SourceTree = $evidenceRecord.SourceTree
    }
}

function Get-FilesystemPayloadRecords {
    param([string] $PayloadDirectory)
    $root = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PayloadDirectory))
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Payload directory '$root' does not exist." }
    Assert-NoReparsePoints $root 'SBOM payload'
    $records = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Force -Recurse) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\','/')
        Assert-SafeRelativePath $relative 'Payload file'
        $records.Add([pscustomobject]@{ Path = $relative; Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() })
    }
    return [pscustomobject]@{ Root = $root; Records = $records.ToArray() }
}

function Test-PrereleaseSbomPayload {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [byte[]] $ExpectedEvidenceBytes,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $payload = Get-FilesystemPayloadRecords $PayloadDirectory
    $manifestPath = Resolve-ChildPath $payload.Root $policy.format.manifestPath 'SPDX manifest'
    $manifestFiles = @(Get-ChildItem -LiteralPath (Join-Path $payload.Root '_manifest') -File -Force -Recurse -ErrorAction Stop)
    if ($manifestFiles.Count -ne 1 -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        $manifestFiles[0].FullName -cne $manifestPath) {
        throw 'Payload must contain exactly one canonical SPDX 2.2 JSON file under _manifest.'
    }
    [byte[]] $sbomBytes = Read-BoundedFileBytes $manifestPath 16MB 'SPDX manifest'
    [byte[]] $evidenceBytes = Read-BoundedFileBytes (Join-Path $payload.Root $script:EvidencePath) 1MB 'SBOM evidence'
    if ($null -ne $ExpectedEvidenceBytes -and -not (Test-ByteArraysEqual $ExpectedEvidenceBytes $evidenceBytes)) {
        throw 'Payload SBOM evidence does not match the expected canonical bytes.'
    }
    $evidenceRecord = Test-PrereleaseSbomEvidenceBytes $evidenceBytes $PolicyPath
    $noticeRoot = Join-Path $payload.Root 'THIRD-PARTY-NOTICES'
    [byte[]] $indexBytes = Read-BoundedFileBytes (Join-Path $noticeRoot 'index.json') 8MB 'Notice index'
    [byte[]] $inventoryBytes = Read-BoundedFileBytes (Join-Path $noticeRoot "inventory/$($evidenceRecord.InventoryName).json") 8MB 'Notice inventory'
    return Test-PrereleaseSbomRecords $sbomBytes $evidenceBytes $payload.Records $indexBytes $inventoryBytes $PolicyPath
}

function Finalize-PrereleaseSbomToolOutput {
    param([Parameter(Mandatory)][string] $PayloadDirectory,
        [string] $PolicyPath = $script:DefaultPolicyPath)
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $payloadRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PayloadDirectory))
    $manifestRoot = Join-Path $payloadRoot '_manifest'
    $manifestPath = Resolve-ChildPath $payloadRoot $policy.format.manifestPath 'Generated SPDX manifest'
    $sidecarPath = Resolve-ChildPath $payloadRoot $policy.generation.rawManifestSha256Path 'Generated SPDX hash sidecar'
    $manifestFiles = @(Get-ChildItem -LiteralPath $manifestRoot -File -Force -Recurse -ErrorAction Stop)
    if ($manifestFiles.Count -ne 2 -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $sidecarPath -PathType Leaf)) {
        throw 'Microsoft SBOM Tool must initially produce exactly one SPDX 2.2 JSON file and its raw SHA-256 sidecar.'
    }
    [byte[]] $rawSbomBytes = Read-BoundedFileBytes $manifestPath 16MB 'Generated SPDX manifest'
    [byte[]] $sidecarBytes = Read-BoundedFileBytes $sidecarPath 1KB 'Generated SPDX hash sidecar'
    $sidecarText = try { $script:StrictUtf8.GetString($sidecarBytes) }
        catch { throw 'Microsoft SBOM Tool raw-manifest SHA-256 sidecar is not valid UTF-8.' }
    if ($sidecarText -cnotmatch '^[0-9a-f]{64}$' -or $sidecarText -cne (Get-Sha256Bytes $rawSbomBytes)) {
        throw 'Microsoft SBOM Tool generated an invalid raw-manifest SHA-256 sidecar.'
    }
    [byte[]] $canonicalSbomBytes = ConvertTo-CanonicalPrereleaseSbomBytes $rawSbomBytes
    $temporaryPath = Join-Path (Split-Path -Parent $manifestPath) ('.manifest.' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = [IO.FileStream]::new($temporaryPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write,
            [IO.FileShare]::None, 64KB, [IO.FileOptions]::WriteThrough)
        try { $stream.Write($canonicalSbomBytes, 0, $canonicalSbomBytes.Length); $stream.Flush($true) }
        finally { $stream.Dispose() }
        [IO.File]::Delete($manifestPath)
        [IO.File]::Move($temporaryPath, $manifestPath)
        [IO.File]::Delete($sidecarPath)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) { [IO.File]::Delete($temporaryPath) }
    }
    $finalManifestFiles = @(Get-ChildItem -LiteralPath $manifestRoot -File -Force -Recurse)
    if ($finalManifestFiles.Count -ne 1 -or $finalManifestFiles[0].FullName -cne $manifestPath) {
        throw 'Final SBOM payload does not contain exactly one canonical SPDX JSON file.'
    }
    return ,$canonicalSbomBytes
}

function Invoke-PrereleaseSbomGeneration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ToolPath,
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][string] $WorkDirectory,
        [Parameter(Mandatory)][byte[]] $ExpectedEvidenceBytes,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )

    $tool = Test-PrereleaseSbomTool $ToolPath $PolicyPath
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $payloadRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PayloadDirectory))
    if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
        throw "SBOM payload '$payloadRoot' does not exist."
    }
    Assert-NoReparsePoints $payloadRoot 'Pre-generation SBOM payload'
    $manifestRoot = Join-Path $payloadRoot '_manifest'
    if (Test-Path -LiteralPath $manifestRoot) {
        throw "SBOM generation requires reserved manifest root '$manifestRoot' to be absent."
    }
    foreach ($required in @($script:EvidencePath, $script:ArtifactManifestPath,
            'THIRD-PARTY-NOTICES/index.json')) {
        $requiredPath = Resolve-ChildPath $payloadRoot $required 'Pre-generation payload'
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Pre-generation payload is missing '$required'."
        }
    }

    [byte[]] $actualEvidenceBytes = Read-BoundedFileBytes (Join-Path $payloadRoot $script:EvidencePath) 1MB 'SBOM evidence'
    if (-not (Test-ByteArraysEqual $ExpectedEvidenceBytes $actualEvidenceBytes)) {
        throw 'Pre-generation SBOM evidence differs from the expected canonical bytes.'
    }
    $evidence = Test-PrereleaseSbomEvidenceBytes $actualEvidenceBytes $PolicyPath
    $noticeRoot = Join-Path $payloadRoot 'THIRD-PARTY-NOTICES'
    [byte[]] $indexBytes = Read-BoundedFileBytes (Join-Path $noticeRoot 'index.json') 8MB 'Notice index'
    [byte[]] $inventoryBytes = Read-BoundedFileBytes `
        (Join-Path $noticeRoot "inventory/$($evidence.InventoryName).json") 8MB 'Notice inventory'
    $dependencyContract = Get-NoticeDependencyContract $indexBytes $inventoryBytes $evidence.InventoryName

    $workRoot = [IO.Path]::GetFullPath($WorkDirectory)
    if (Test-Path -LiteralPath $workRoot) { throw "SBOM work directory '$workRoot' must be absent." }
    $null = [IO.Directory]::CreateDirectory($workRoot)
    Assert-NoReparsePoints $workRoot 'SBOM work directory'
    $componentRoot = Join-Path $workRoot 'components'
    $null = [IO.Directory]::CreateDirectory($componentRoot)
    $ordinal = 0
    foreach ($package in $dependencyContract.Packages) {
        if ($package.Name -cnotmatch '^[A-Za-z0-9_.-]+$' -or $package.Version -cnotmatch '^[A-Za-z0-9_.+-]+$') {
            throw "Notice package '$($package.Name)/$($package.Version)' cannot form a safe component path."
        }
        $source = Resolve-ChildPath $noticeRoot $package.NuspecPath "Notice package '$($package.Library)' nuspec"
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Notice package '$($package.Library)' nuspec is missing."
        }
        $actualNuspecHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualNuspecHash -cne $package.NuspecSha256) {
            throw "Notice package '$($package.Library)' nuspec hash changed before SBOM generation."
        }
        $destinationDirectory = Join-Path $componentRoot ('{0:D4}-{1}' -f $ordinal, $package.Name.ToLowerInvariant())
        $null = [IO.Directory]::CreateDirectory($destinationDirectory)
        [IO.File]::Copy($source, (Join-Path $destinationDirectory ([IO.Path]::GetFileName($source))), $false)
        $ordinal++
    }
    if ($ordinal -ne $dependencyContract.Packages.Count) {
        throw 'Product-specific SBOM component staging count changed unexpectedly.'
    }

    $generation = Invoke-BoundedProcess $tool.Path @(
        'generate', '-b', $payloadRoot, '-bc', $componentRoot, '-m', $payloadRoot,
        '-pn', $evidence.ProductName, '-pv', $evidence.Version, '-ps', $policy.supplier,
        '-nsb', $policy.namespaceBase,
        '-nsu', (ConvertTo-SbomNamespaceUniquePart $evidence.SourceCommit $evidence.InventoryName $evidence.Version),
        '-gt', $evidence.SourceTimestamp,
        '-li', 'false', '-pm', 'true', '-P', '2', '-F', 'false', '-mi', 'SPDX:2.2', '-V', 'Error'
    ) $payloadRoot "Generating $($evidence.ProductName) SPDX 2.2 SBOM" 300
    $generationTranscript = $generation.StandardOutput + "`n" + $generation.StandardError
    if ($generationTranscript -match '(?im)##\[(?:warning|error)\]|unknown error|\bexception:|there were no packages') {
        throw "Microsoft SBOM Tool reported a warning or internal failure during generation: $($generationTranscript.Trim())"
    }

    $null = Finalize-PrereleaseSbomToolOutput $payloadRoot $PolicyPath

    $semanticResult = Test-PrereleaseSbomPayload $payloadRoot $ExpectedEvidenceBytes $PolicyPath
    $validationPath = Join-Path $workRoot 'microsoft-validation.json'
    $validation = Invoke-BoundedProcess $tool.Path @(
        'validate', '-b', $payloadRoot, '-m', $manifestRoot, '-o', $validationPath,
        '-n', '-P', '2', '-F', 'false', '-mi', 'SPDX:2.2', '-V', 'Error'
    ) $payloadRoot "Validating $($evidence.ProductName) SPDX 2.2 SBOM" 180
    $validationTranscript = $validation.StandardOutput + "`n" + $validation.StandardError
    if ($validationTranscript -match '(?im)##\[(?:warning|error)\]|unknown error|\bexception:') {
        throw "Microsoft SBOM Tool reported a warning or internal failure during validation: $($validationTranscript.Trim())"
    }
    [byte[]] $validationBytes = Read-BoundedFileBytes $validationPath 1MB 'Microsoft SBOM validation result'
    $validationResult = ConvertFrom-StrictJsonBytes $validationBytes 'Microsoft SBOM validation result'
    if ($validationResult.Result -cne 'Success' -or
        $validationResult.ValidationErrors -isnot [Collections.IDictionary] -or
        [long]$validationResult.ValidationErrors.Count -ne 0 -or
        $validationResult.Summary -isnot [Collections.IDictionary] -or
        $validationResult.Summary.ValidationTelemetery -isnot [Collections.IDictionary]) {
        throw 'Microsoft SBOM validation result did not report exact success with zero errors.'
    }
    $telemetry = $validationResult.Summary.ValidationTelemetery
    if ([long]$telemetry.FilesSuccessfulCount -ne $semanticResult.FileCount -or
        [long]$telemetry.TotalFilesInManifest -ne $semanticResult.FileCount -or
        [long]$telemetry.FilesValidatedCount -ne $semanticResult.FileCount -or
        [long]$telemetry.FilesSkippedCount -ne 1 -or
        [long]$telemetry.FilesFailedCount -ne 0 -or
        [long]$telemetry.TotalPackagesInManifest -ne ($semanticResult.DependencyCount + 1)) {
        throw 'Microsoft SBOM validation counters do not match the exact PhoenixInspect payload/dependency contract.'
    }
    return $semanticResult
}

function Read-ZipEntryBounded {
    param([IO.Compression.ZipArchiveEntry] $Entry, [long] $MaximumBytes, [string] $Context)
    if ($Entry.Length -le 0 -or $Entry.Length -gt $MaximumBytes) { throw "$Context is empty or exceeds $MaximumBytes bytes." }
    $stream = $Entry.Open()
    try {
        $memory = [IO.MemoryStream]::new([int]$Entry.Length)
        try { $stream.CopyTo($memory); return ,$memory.ToArray() }
        finally { $memory.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Test-PrereleaseSbomArchive {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][IO.Compression.ZipArchive] $Archive,
        [Parameter(Mandatory)][string] $ContentRoot,
        [byte[]] $ExpectedEvidenceBytes,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    Assert-SafeRelativePath $ContentRoot 'Archive content root'
    if ($ContentRoot.Contains('/')) { throw 'Archive content root must be exactly one path segment.' }
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $entryMap = [Collections.Generic.Dictionary[string, IO.Compression.ZipArchiveEntry]]::new([StringComparer]::Ordinal)
    $records = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Archive.Entries) {
        $name = $entry.FullName
        if ($name.Contains('\') -or -not $name.StartsWith("$ContentRoot/", [StringComparison]::Ordinal)) {
            throw "Archive entry '$name' is outside canonical content root '$ContentRoot'."
        }
        $relative = $name.Substring($ContentRoot.Length + 1)
        if ($relative.Length -eq 0) { continue }
        if ($relative.EndsWith('/')) { Assert-SafeRelativePath $relative.TrimEnd('/') 'Archive directory'; continue }
        Assert-SafeRelativePath $relative 'Archive file'
        if (-not $entryMap.TryAdd($relative, $entry)) { throw "Archive duplicates entry '$relative'." }
        $stream = $entry.Open()
        try { $hash = Get-Sha256Stream $stream }
        finally { $stream.Dispose() }
        $records.Add([pscustomobject]@{ Path = $relative; Sha256 = $hash })
    }
    foreach ($required in @($policy.format.manifestPath,$script:EvidencePath,$script:ArtifactManifestPath,'THIRD-PARTY-NOTICES/index.json')) {
        if (-not $entryMap.ContainsKey($required)) { throw "Archive is missing required SBOM entry '$required'." }
    }
    $manifestEntryCount = @($entryMap.Keys | Where-Object { $_.StartsWith('_manifest/', [StringComparison]::Ordinal) }).Count
    if ($manifestEntryCount -ne 1) { throw 'Archive must contain exactly one canonical SPDX JSON file beneath _manifest.' }
    [byte[]] $sbomBytes = Read-ZipEntryBounded $entryMap[$policy.format.manifestPath] 16MB 'Archive SPDX manifest'
    [byte[]] $evidenceBytes = Read-ZipEntryBounded $entryMap[$script:EvidencePath] 1MB 'Archive SBOM evidence'
    if ($null -ne $ExpectedEvidenceBytes -and -not (Test-ByteArraysEqual $ExpectedEvidenceBytes $evidenceBytes)) {
        throw 'Archive SBOM evidence differs from expected canonical bytes.'
    }
    $evidenceRecord = Test-PrereleaseSbomEvidenceBytes $evidenceBytes $PolicyPath
    $inventoryPath = "THIRD-PARTY-NOTICES/inventory/$($evidenceRecord.InventoryName).json"
    if (-not $entryMap.ContainsKey($inventoryPath)) { throw "Archive is missing product notice inventory '$inventoryPath'." }
    [byte[]] $indexBytes = Read-ZipEntryBounded $entryMap['THIRD-PARTY-NOTICES/index.json'] 8MB 'Archive notice index'
    [byte[]] $inventoryBytes = Read-ZipEntryBounded $entryMap[$inventoryPath] 8MB 'Archive notice inventory'
    return Test-PrereleaseSbomRecords $sbomBytes $evidenceBytes $records.ToArray() $indexBytes $inventoryBytes $PolicyPath
}

function Initialize-PrereleaseSbomSyntheticFixture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][ValidateSet('PhoenixInspect CLI','PhoenixInspect Desktop')][string] $ProductName,
        [Parameter(Mandatory)][ValidateSet('cli','desktop')][string] $InventoryName,
        [Parameter(Mandatory)][string] $Version,
        [string] $SourceCommit = ('1' * 40),
        [string] $SourceTree = ('2' * 40),
        [string] $SourceTimestamp = '2026-01-01T00:00:00Z',
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    if (($ProductName -ceq 'PhoenixInspect CLI') -ne ($InventoryName -ceq 'cli')) {
        throw "Synthetic fixture product '$ProductName' and inventory '$InventoryName' disagree."
    }
    $root = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($PayloadDirectory))
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Synthetic fixture payload '$root' does not exist." }
    Assert-NoReparsePoints $root 'Synthetic SBOM payload'
    if (Test-Path -LiteralPath (Join-Path $root '_manifest')) { throw 'Synthetic fixture phase one requires _manifest to be absent.' }
    $noticeRoot = Join-Path $root 'THIRD-PARTY-NOTICES'
    $null = [IO.Directory]::CreateDirectory($noticeRoot)
    $inventoryDirectory = Join-Path $noticeRoot 'inventory'
    $nuspecDirectory = Join-Path $noticeRoot 'evidence/phoenixinspect.syntheticdependency/1.0.0'
    $null = [IO.Directory]::CreateDirectory($inventoryDirectory)
    $null = [IO.Directory]::CreateDirectory($nuspecDirectory)
    $nuspecRelative = 'evidence/phoenixinspect.syntheticdependency/1.0.0/phoenixinspect.syntheticdependency.nuspec'
    $nuspecPath = Join-Path $noticeRoot $nuspecRelative.Replace('/', [IO.Path]::DirectorySeparatorChar)
    [byte[]] $nuspecBytes = $script:Utf8NoBom.GetBytes(
        "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n" +
        "<package><metadata><id>PhoenixInspect.SyntheticDependency</id><version>1.0.0</version>" +
        "<authors>PhoenixInspect SelfTest</authors><license type=`"expression`">MIT</license>" +
        "<description>Pure prerelease SBOM validation fixture.</description></metadata></package>`n")
    foreach ($path in @((Join-Path $noticeRoot 'index.json'),
            (Join-Path $inventoryDirectory "$InventoryName.json"),
            (Join-Path $root $script:EvidencePath), $nuspecPath)) {
        if (Test-Path -LiteralPath $path) { throw "Synthetic fixture phase one refuses to overwrite '$path'." }
    }
    [IO.File]::WriteAllBytes($nuspecPath, $nuspecBytes)
    $libraryId = 'runtimepack.PhoenixInspect.SyntheticDependency'
    $sourcePackageId = 'PhoenixInspect.SyntheticDependency'
    $libraryName = "$libraryId/1.0.0"
    [byte[]] $indexBytes = ConvertTo-CanonicalJsonBytes ([ordered]@{
        schemaVersion = 1
        purpose = 'Pure PhoenixInspect prerelease SBOM self-test fixture; not legal evidence.'
        runtimeIdentifier = 'win-x64'
        libraries = @([ordered]@{
            library = $libraryName
            packageId = $libraryId
            version = '1.0.0'
            dependencyType = 'package'
            products = @($InventoryName)
            licenseDeclaration = [ordered]@{ type = 'expression'; value = 'MIT' }
            authors = 'PhoenixInspect SelfTest'
            sourcePackage = [ordered]@{
                id = $sourcePackageId
                version = '1.0.0'
                nuspec = [ordered]@{ path = $nuspecRelative; sha256 = Get-Sha256Bytes $nuspecBytes }
            }
        })
    })
    [byte[]] $inventoryBytes = ConvertTo-CanonicalJsonBytes ([ordered]@{
        schemaVersion = 1
        product = $InventoryName
        runtimeTarget = '.NETCoreApp,Version=v10.0/win-x64'
        externalLibraries = @($libraryName)
    })
    [IO.File]::WriteAllBytes((Join-Path $noticeRoot 'index.json'), $indexBytes)
    [IO.File]::WriteAllBytes((Join-Path $inventoryDirectory "$InventoryName.json"), $inventoryBytes)
    [byte[]] $evidenceBytes = New-PrereleaseSbomEvidenceBytes `
        -ProductName $ProductName -InventoryName $InventoryName -Version $Version `
        -SourceCommit $SourceCommit -SourceTree $SourceTree -SourceTimestamp $SourceTimestamp -PolicyPath $PolicyPath
    Write-PrereleaseSbomEvidence (Join-Path $root $script:EvidencePath) $evidenceBytes $PolicyPath
    return [pscustomobject]@{
        EvidenceBytes = $evidenceBytes
        EvidenceSha256 = Get-Sha256Bytes $evidenceBytes
        ProductName = $ProductName
        InventoryName = $InventoryName
        Version = $Version
    }
}

function Complete-PrereleaseSbomSyntheticFixture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $PayloadDirectory,
        [Parameter(Mandatory)][byte[]] $ExpectedEvidenceBytes,
        [string] $PolicyPath = $script:DefaultPolicyPath
    )
    $policy = (Get-PrereleaseSbomPolicy $PolicyPath).Data
    $payload = Get-FilesystemPayloadRecords $PayloadDirectory
    if (@($payload.Records | Where-Object { $_.Path.StartsWith('_manifest/', [StringComparison]::Ordinal) }).Count -ne 0 -or
        (Test-Path -LiteralPath (Join-Path $payload.Root '_manifest'))) {
        throw 'Synthetic fixture phase two requires _manifest to be absent.'
    }
    $evidencePath = Join-Path $payload.Root $script:EvidencePath
    [byte[]] $evidenceBytes = Read-BoundedFileBytes $evidencePath 1MB 'Synthetic SBOM evidence'
    if (-not (Test-ByteArraysEqual $ExpectedEvidenceBytes $evidenceBytes)) { throw 'Synthetic fixture evidence bytes changed between phases.' }
    $evidence = Test-PrereleaseSbomEvidenceBytes $evidenceBytes $PolicyPath
    if (-not (Test-Path -LiteralPath (Join-Path $payload.Root $script:ArtifactManifestPath) -PathType Leaf)) {
        throw 'Synthetic fixture phase two requires publisher-created ARTIFACT-MANIFEST.txt.'
    }
    $noticeRoot = Join-Path $payload.Root 'THIRD-PARTY-NOTICES'
    [byte[]] $indexBytes = Read-BoundedFileBytes (Join-Path $noticeRoot 'index.json') 8MB 'Synthetic notice index'
    [byte[]] $inventoryBytes = Read-BoundedFileBytes `
        (Join-Path $noticeRoot "inventory/$($evidence.InventoryName).json") 8MB 'Synthetic notice inventory'
    $dependencies = (Get-NoticeDependencyContract $indexBytes $inventoryBytes $evidence.InventoryName).Packages
    $fileRecords = [Collections.Generic.List[object]]::new()
    $fileIds = [Collections.Generic.List[string]]::new()
    $fileSha1Values = [Collections.Generic.List[string]]::new()
    $recordByPath = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($record in $payload.Records) {
        if ($recordByPath.ContainsKey([string]$record.Path)) { throw "Synthetic payload duplicates '$($record.Path)'." }
        $recordByPath.Add([string]$record.Path, $record)
    }
    foreach ($record in $recordByPath.Values) {
        $path = [string]$record.Path
        if ($path.StartsWith('_manifest/', [StringComparison]::Ordinal)) { throw "Synthetic payload unexpectedly contains '$path'." }
        $fullPath = Resolve-ChildPath $payload.Root $path 'Synthetic payload file'
        $sha1Algorithm = [Security.Cryptography.SHA1]::Create()
        $stream = [IO.File]::OpenRead($fullPath)
        try { $sha1 = ([BitConverter]::ToString($sha1Algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose(); $sha1Algorithm.Dispose() }
        $safeIdPath = [regex]::Replace($path, '[^A-Za-z0-9.-]', '-')
        $id = "SPDXRef-File-$safeIdPath-$($sha1.ToUpperInvariant())"
        $fileIds.Add($id)
        $fileSha1Values.Add($sha1)
        $fileRecords.Add([ordered]@{
            fileName = "./$path"
            SPDXID = $id
            checksums = @(
                [ordered]@{ algorithm = 'SHA1'; checksumValue = $sha1 }
                [ordered]@{ algorithm = 'SHA256'; checksumValue = [string]$record.Sha256 }
            )
            licenseConcluded = 'NOASSERTION'
            licenseInfoInFiles = @('NOASSERTION')
            copyrightText = 'NOASSERTION'
        })
    }
    [string[]] $sortedSha1 = @(Get-OrdinalStrings $fileSha1Values)
    $verificationAlgorithm = [Security.Cryptography.SHA1]::Create()
    try {
        $verificationCode = ([BitConverter]::ToString($verificationAlgorithm.ComputeHash(
            $script:Utf8NoBom.GetBytes($sortedSha1 -join '')))).Replace('-', '').ToLowerInvariant()
    }
    finally { $verificationAlgorithm.Dispose() }
    $packages = [Collections.Generic.List[object]]::new()
    $packages.Add([ordered]@{
        name = $evidence.ProductName
        SPDXID = 'SPDXRef-RootPackage'
        downloadLocation = 'NOASSERTION'
        packageVerificationCode = [ordered]@{ packageVerificationCodeValue = $verificationCode }
        filesAnalyzed = $true
        licenseConcluded = 'NOASSERTION'
        licenseInfoFromFiles = @('NOASSERTION')
        licenseDeclared = 'NOASSERTION'
        copyrightText = 'NOASSERTION'
        versionInfo = $evidence.Version
        externalRefs = @()
        supplier = 'Organization: Vladimir Reshetnikov'
        hasFiles = @(Get-OrdinalStrings $fileIds)
    })
    $relationships = [Collections.Generic.List[object]]::new()
    $relationships.Add([ordered]@{
        relationshipType = 'DESCRIBES'
        relatedSpdxElement = 'SPDXRef-RootPackage'
        spdxElementId = 'SPDXRef-DOCUMENT'
    })
    foreach ($dependency in $dependencies) {
        $idDigest = Get-Sha256Bytes ($script:Utf8NoBom.GetBytes("$($dependency.Name)`0$($dependency.Version)"))
        $id = 'SPDXRef-Package-' + $idDigest.ToUpperInvariant()
        $packages.Add([ordered]@{
            name = $dependency.Name
            SPDXID = $id
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $false
            licenseConcluded = 'NOASSERTION'
            licenseDeclared = $dependency.LicenseDeclared
            copyrightText = 'NOASSERTION'
            versionInfo = $dependency.Version
            externalRefs = @([ordered]@{
                referenceCategory = 'PACKAGE-MANAGER'
                referenceType = 'purl'
                referenceLocator = $dependency.Purl
            })
            supplier = $dependency.Supplier
        })
        $relationships.Add([ordered]@{
            relationshipType = 'DEPENDS_ON'
            relatedSpdxElement = $id
            spdxElementId = 'SPDXRef-RootPackage'
        })
    }
    [byte[]] $sbomBytes = ConvertTo-CanonicalPrereleaseSbomBytes (ConvertTo-CanonicalJsonBytes ([ordered]@{
        files = $fileRecords.ToArray()
        packages = $packages.ToArray()
        externalDocumentRefs = @()
        relationships = $relationships.ToArray()
        spdxVersion = 'SPDX-2.2'
        dataLicense = 'CC0-1.0'
        SPDXID = 'SPDXRef-DOCUMENT'
        name = "$($evidence.ProductName) $($evidence.Version)"
        documentNamespace = $evidence.DocumentNamespace
        creationInfo = [ordered]@{
            created = $evidence.SourceTimestamp
            creators = @('Organization: Vladimir Reshetnikov','Tool: Microsoft.SBOMTool-4.1.5')
        }
        documentDescribes = @('SPDXRef-RootPackage')
    }))
    $manifestPath = Resolve-ChildPath $payload.Root $policy.format.manifestPath 'Synthetic SPDX manifest'
    $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $manifestPath))
    [IO.File]::WriteAllBytes($manifestPath, $sbomBytes)
    return Test-PrereleaseSbomPayload $payload.Root $ExpectedEvidenceBytes $PolicyPath
}

function Invoke-PrereleaseSbomSelfTest {
    [CmdletBinding()]
    param()

    function Assert-SelfTestThrows {
        param([Parameter(Mandatory)][scriptblock] $Action, [Parameter(Mandatory)][string] $Name)
        try { & $Action; throw "Self-test case '$Name' unexpectedly succeeded." }
        catch {
            if ($_.Exception.Message -ceq "Self-test case '$Name' unexpectedly succeeded.") { throw }
        }
    }

    function Copy-DirectoryFixture {
        param([string] $Source, [string] $Destination)
        $null = [IO.Directory]::CreateDirectory($Destination)
        foreach ($directory in Get-ChildItem -LiteralPath $Source -Directory -Force -Recurse) {
            $relative = [IO.Path]::GetRelativePath($Source, $directory.FullName)
            $null = [IO.Directory]::CreateDirectory((Join-Path $Destination $relative))
        }
        foreach ($file in Get-ChildItem -LiteralPath $Source -File -Force -Recurse) {
            $relative = [IO.Path]::GetRelativePath($Source, $file.FullName)
            $destinationFile = Join-Path $Destination $relative
            $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $destinationFile))
            [IO.File]::Copy($file.FullName, $destinationFile, $false)
        }
    }

    function New-ZipArchiveFromDirectory {
        param([string] $Source, [string] $ContentRoot)
        $memory = [IO.MemoryStream]::new()
        $archive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Create, $true)
        foreach ($file in Get-ChildItem -LiteralPath $Source -File -Force -Recurse) {
            $relative = [IO.Path]::GetRelativePath($Source, $file.FullName).Replace('\','/')
            $entry = $archive.CreateEntry("$ContentRoot/$relative", [IO.Compression.CompressionLevel]::NoCompression)
            $input = [IO.File]::OpenRead($file.FullName)
            $output = $entry.Open()
            try { $input.CopyTo($output) }
            finally { $output.Dispose(); $input.Dispose() }
        }
        $archive.Dispose()
        $memory.Position = 0
        $readArchive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Read, $false)
        return [pscustomobject]@{ Archive = $readArchive; Stream = $memory }
    }

    $null = Get-PrereleaseSbomPolicy
    $descriptor = Get-PrereleaseSbomBuildEvidenceDescriptor
    if (($descriptor.Keys -join "`n") -cne (@('format','toolName','version','assetSha256','productSpecific') -join "`n") -or
        $descriptor.format -cne 'SPDX-2.2' -or $descriptor.toolName -cne 'Microsoft.SBOMTool' -or
        $descriptor.version -cne '4.1.5' -or
        $descriptor.assetSha256 -cne '625767b371b7fdd58f40f618b8a86da0247a33c89e419039c86b4edba1dad4b5' -or
        $descriptor.productSpecific -isnot [bool] -or -not $descriptor.productSpecific) {
        throw 'SBOM build-evidence descriptor self-test failed.'
    }

    $root = Join-Path ([IO.Path]::GetTempPath()) ('phoenixinspect-sbom-selftest-' + [guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($root)
    try {
        $validPayloads = [Collections.Generic.List[object]]::new()
        foreach ($identity in @(
            [pscustomobject]@{ Product = 'PhoenixInspect CLI'; Inventory = 'cli' },
            [pscustomobject]@{ Product = 'PhoenixInspect Desktop'; Inventory = 'desktop' })) {
            $payload = Join-Path $root $identity.Inventory
            $null = [IO.Directory]::CreateDirectory($payload)
            [IO.File]::WriteAllText((Join-Path $payload 'application.bin'), "fixture-$($identity.Inventory)", $script:Utf8NoBom)
            $phaseOne = Initialize-PrereleaseSbomSyntheticFixture $payload $identity.Product $identity.Inventory '0.1.0-preview.1'
            [byte[]] $fixtureIndexBytes = [IO.File]::ReadAllBytes((Join-Path $payload 'THIRD-PARTY-NOTICES/index.json'))
            [byte[]] $fixtureInventoryBytes = [IO.File]::ReadAllBytes(
                (Join-Path $payload "THIRD-PARTY-NOTICES/inventory/$($identity.Inventory).json"))
            $fixtureContract = Get-NoticeDependencyContract $fixtureIndexBytes $fixtureInventoryBytes $identity.Inventory
            if ($fixtureContract.Packages.Count -ne 1 -or
                $fixtureContract.Packages[0].Library -cne 'runtimepack.PhoenixInspect.SyntheticDependency/1.0.0' -or
                $fixtureContract.Packages[0].Name -cne 'PhoenixInspect.SyntheticDependency' -or
                $fixtureContract.Packages[0].Purl -cne 'pkg:nuget/PhoenixInspect.SyntheticDependency@1.0.0') {
                throw 'Synthetic runtime-alias notice contract did not preserve distinct library and source-package identities.'
            }
            $fixtureIndexText = $script:StrictUtf8.GetString($fixtureIndexBytes)
            $mismatchedLibraryIndexText = $fixtureIndexText.Replace(
                '"packageId": "runtimepack.PhoenixInspect.SyntheticDependency"',
                '"packageId": "PhoenixInspect.SyntheticDependency"')
            if ($mismatchedLibraryIndexText -ceq $fixtureIndexText) {
                throw 'Synthetic runtime-alias packageId mutation did not change the notice index.'
            }
            Assert-SelfTestThrows {
                $null = Get-NoticeDependencyContract `
                    ($script:Utf8NoBom.GetBytes($mismatchedLibraryIndexText)) $fixtureInventoryBytes $identity.Inventory
            } 'library identity does not match the inventory library name'
            [IO.File]::WriteAllText((Join-Path $payload $script:ArtifactManifestPath), "# fixture`n", $script:Utf8NoBom)
            $result = Complete-PrereleaseSbomSyntheticFixture $payload $phaseOne.EvidenceBytes
            if ($result.Product -cne $identity.Product -or $result.InventoryName -cne $identity.Inventory -or
                $result.DependencyCount -ne 1 -or $result.FileCount -ne 6 -or
                (Test-Path -LiteralPath (Join-Path $payload '_manifest/spdx_2.2/manifest.spdx.json.sha256'))) {
                throw "Synthetic $($identity.Inventory) fixture did not satisfy the exact semantic/sidecar contract."
            }
            $roundTrip = Test-PrereleaseSbomPayload $payload $phaseOne.EvidenceBytes
            if ($roundTrip.ManifestSha256 -cne $result.ManifestSha256) { throw 'Filesystem SBOM round-trip changed manifest identity.' }
            $zipFixture = New-ZipArchiveFromDirectory $payload "fixture-$($identity.Inventory)"
            try {
                $archiveResult = Test-PrereleaseSbomArchive $zipFixture.Archive "fixture-$($identity.Inventory)" $phaseOne.EvidenceBytes
                if ($archiveResult.ManifestSha256 -cne $result.ManifestSha256) { throw 'Archive SBOM validation changed manifest identity.' }
            }
            finally { $zipFixture.Archive.Dispose(); $zipFixture.Stream.Dispose() }
            $validPayloads.Add([pscustomobject]@{ Path = $payload; Evidence = $phaseOne.EvidenceBytes; Result = $result })
        }
        Assert-SelfTestThrows {
            $null = Assert-PrereleaseSbomEvidenceIdentity $validPayloads[0].Evidence $validPayloads[1].Evidence
        } 'product evidence identity mismatch'
        Assert-SelfTestThrows {
            $null = New-PrereleaseSbomEvidenceBytes `
                -ProductName 'PhoenixInspect CLI' -InventoryName 'desktop' -Version '0.1.0-preview.1' `
                -SourceCommit ('1' * 40) -SourceTree ('2' * 40) -SourceTimestamp '2026-08-11T00:00:00Z'
        } 'CLI with Desktop inventory'
        Assert-SelfTestThrows {
            $null = New-PrereleaseSbomEvidenceBytes `
                -ProductName 'PhoenixInspect Desktop' -InventoryName 'cli' -Version '0.1.0-preview.1' `
                -SourceCommit ('1' * 40) -SourceTree ('2' * 40) -SourceTimestamp '2026-08-11T00:00:00Z'
        } 'Desktop with CLI inventory'

        $base = $validPayloads[0]
        foreach ($case in @(
            [pscustomobject]@{ Name = 'mutated payload hash'; Mutate = {
                param($path) [IO.File]::AppendAllText((Join-Path $path 'application.bin'), 'mutation', $script:Utf8NoBom) } },
            [pscustomobject]@{ Name = 'missing payload file'; Mutate = {
                param($path) [IO.File]::Delete((Join-Path $path 'application.bin')) } },
            [pscustomobject]@{ Name = 'extra payload file'; Mutate = {
                param($path) [IO.File]::WriteAllText((Join-Path $path 'extra.bin'), 'extra', $script:Utf8NoBom) } },
            [pscustomobject]@{ Name = 'extra manifest file'; Mutate = {
                param($path) [IO.File]::WriteAllText((Join-Path $path '_manifest/unexpected.txt'), 'extra', $script:Utf8NoBom) } },
            [pscustomobject]@{ Name = 'shipped sidecar'; Mutate = {
                param($path) [IO.File]::WriteAllText((Join-Path $path '_manifest/spdx_2.2/manifest.spdx.json.sha256'), ('0' * 64), $script:Utf8NoBom) } })) {
            $copy = Join-Path $root ('invalid-' + [guid]::NewGuid().ToString('N'))
            Copy-DirectoryFixture $base.Path $copy
            & $case.Mutate $copy
            Assert-SelfTestThrows { $null = Test-PrereleaseSbomPayload $copy $base.Evidence } $case.Name
        }

        foreach ($mutation in @(
            [pscustomobject]@{ Name = 'overclaimed redistribution'; Old = '"redistributionApproved": false'; New = '"redistributionApproved": true' },
            [pscustomobject]@{ Name = 'wrong tool hash'; Old = '625767b371b7fdd58f40f618b8a86da0247a33c89e419039c86b4edba1dad4b5'; New = ('f' * 64) },
            [pscustomobject]@{ Name = 'noncanonical evidence'; Old = '  "scope"'; New = '   "scope"' })) {
            $text = $script:StrictUtf8.GetString($base.Evidence).Replace($mutation.Old, $mutation.New)
            Assert-SelfTestThrows { $null = Test-PrereleaseSbomEvidenceBytes ($script:Utf8NoBom.GetBytes($text)) } $mutation.Name
        }
        $duplicateEvidenceText = $script:StrictUtf8.GetString($base.Evidence).Replace(
            '  "schema":', "  `"schema`": `"$script:EvidenceSchema`",`n  `"schema`":")
        Assert-SelfTestThrows {
            $null = Test-PrereleaseSbomEvidenceBytes ($script:Utf8NoBom.GetBytes($duplicateEvidenceText))
        } 'duplicate evidence property'

        $manifestPath = Join-Path $base.Path '_manifest/spdx_2.2/manifest.spdx.json'
        $manifestText = [IO.File]::ReadAllText($manifestPath)
        foreach ($mutation in @(
            [pscustomobject]@{ Name = 'mutated dependency version'; Old = '"versionInfo": "1.0.0"'; New = '"versionInfo": "1.0.1"' },
            [pscustomobject]@{ Name = 'mutated dependency relationship'; Old = '"relationshipType": "DEPENDS_ON"'; New = '"relationshipType": "CONTAINS"' },
            [pscustomobject]@{ Name = 'mutated file hash'; Old = '"algorithm": "SHA256"'; New = '"algorithm": "SHA512"' },
            [pscustomobject]@{ Name = 'noncanonical manifest whitespace'; Old = '  "files"'; New = '   "files"' })) {
            $copy = Join-Path $root ('invalid-manifest-' + [guid]::NewGuid().ToString('N'))
            Copy-DirectoryFixture $base.Path $copy
            $copyManifest = Join-Path $copy '_manifest/spdx_2.2/manifest.spdx.json'
            [IO.File]::WriteAllText($copyManifest, $manifestText.Replace($mutation.Old, $mutation.New), $script:Utf8NoBom)
            Assert-SelfTestThrows { $null = Test-PrereleaseSbomPayload $copy $base.Evidence } $mutation.Name
        }

        $toolOutput = Join-Path $root 'tool-output'
        Copy-DirectoryFixture $base.Path $toolOutput
        [IO.Directory]::Delete((Join-Path $toolOutput '_manifest'), $true)
        $manifestDirectory = Join-Path $toolOutput '_manifest/spdx_2.2'
        $null = [IO.Directory]::CreateDirectory($manifestDirectory)
        [byte[]] $canonicalManifest = [IO.File]::ReadAllBytes($manifestPath)
        $uncanonical = $script:StrictUtf8.GetString($canonicalManifest).Replace('  "files"', '   "files"')
        [byte[]] $rawBytes = $script:Utf8NoBom.GetBytes($uncanonical)
        [IO.File]::WriteAllBytes((Join-Path $manifestDirectory 'manifest.spdx.json'), $rawBytes)
        [IO.File]::WriteAllText((Join-Path $manifestDirectory 'manifest.spdx.json.sha256'), (Get-Sha256Bytes $rawBytes), $script:Utf8NoBom)
        $finalBytes = Finalize-PrereleaseSbomToolOutput $toolOutput
        if (-not (Test-ByteArraysEqual $finalBytes $canonicalManifest) -or
            (Test-Path -LiteralPath (Join-Path $manifestDirectory 'manifest.spdx.json.sha256'))) {
            throw 'Raw tool sidecar finalization did not canonicalize and remove the sidecar.'
        }
        $null = Test-PrereleaseSbomPayload $toolOutput $base.Evidence
        $badToolOutput = Join-Path $root 'bad-tool-output'
        Copy-DirectoryFixture $base.Path $badToolOutput
        [IO.File]::WriteAllText((Join-Path $badToolOutput '_manifest/spdx_2.2/manifest.spdx.json.sha256'), ('0' * 64), $script:Utf8NoBom)
        Assert-SelfTestThrows { $null = Finalize-PrereleaseSbomToolOutput $badToolOutput } 'invalid raw tool sidecar'
    }
    finally {
        if (Test-Path -LiteralPath $root) { [IO.Directory]::Delete($root, $true) }
    }
    Write-Output 'Prerelease SBOM self-test passed: pinned policy, canonical evidence/SPDX, exact product dependency/file/relationship semantics, final sidecar absence, filesystem/archive parity, and adversarial mutations.'
}

Export-ModuleMember -Function @(
    'Assert-PrereleaseSbomEvidenceIdentity'
    'Complete-PrereleaseSbomSyntheticFixture'
    'ConvertTo-CanonicalPrereleaseSbomBytes'
    'Get-PrereleaseSbomBuildEvidenceDescriptor'
    'Get-PrereleaseSbomPolicy'
    'Get-PrereleaseSbomSourceTimestamp'
    'Initialize-PrereleaseSbomSyntheticFixture'
    'Invoke-PrereleaseSbomGeneration'
    'Invoke-PrereleaseSbomSelfTest'
    'New-PrereleaseSbomEvidenceBytes'
    'Resolve-PrereleaseSbomTool'
    'Test-PrereleaseSbomArchive'
    'Test-PrereleaseSbomEvidenceBytes'
    'Test-PrereleaseSbomPayload'
    'Test-PrereleaseSbomRecords'
    'Test-PrereleaseSbomTool'
    'Write-PrereleaseSbomEvidence'
)
