#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding(DefaultParameterSetName = 'SelfTest')]
param(
    [Parameter(Mandatory, ParameterSetName = 'SelfTest')]
    [switch] $SelfTest,

    [Parameter(Mandatory, ParameterSetName = 'Payload')]
    [string] $PayloadDirectory,

    [Parameter(ParameterSetName = 'Payload')]
    [string] $ExpectedEvidencePath,

    [Parameter(ParameterSetName = 'Payload')]
    [string] $PolicyPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PrereleaseSbom.psm1') -Force

if ($SelfTest) {
    Invoke-PrereleaseSbomSelfTest
    return
}

$expectedEvidence = if ($ExpectedEvidencePath) {
    $fullEvidencePath = [IO.Path]::GetFullPath($ExpectedEvidencePath)
    if (-not (Test-Path -LiteralPath $fullEvidencePath -PathType Leaf)) {
        throw "ExpectedEvidencePath '$fullEvidencePath' does not exist."
    }
    [IO.File]::ReadAllBytes($fullEvidencePath)
}
else {
    $null
}

$validationArguments = @{
    PayloadDirectory = $PayloadDirectory
}
if ($null -ne $expectedEvidence) {
    $validationArguments.ExpectedEvidenceBytes = $expectedEvidence
}
if (-not [string]::IsNullOrWhiteSpace($PolicyPath)) {
    $validationArguments.PolicyPath = $PolicyPath
}
$result = Test-PrereleaseSbomPayload @validationArguments

Write-Output ("Verified product-specific local SPDX 2.2 SBOM: " +
    "product=$($result.Product) files=$($result.FileCount) dependencies=$($result.DependencyCount) " +
    "evidenceSha256=$($result.EvidenceSha256) manifestSha256=$($result.ManifestSha256)")
