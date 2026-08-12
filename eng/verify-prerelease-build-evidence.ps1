#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding(DefaultParameterSetName = 'SelfTest')]
param(
    [Parameter(Mandatory, ParameterSetName = 'SelfTest')]
    [switch] $SelfTest,

    [Parameter(Mandatory, ParameterSetName = 'Evidence')]
    [string] $EvidencePath,

    [Parameter(ParameterSetName = 'Evidence')]
    [string] $SecondEvidencePath,

    [Parameter(ParameterSetName = 'Evidence')]
    [switch] $VerifySelectedInputHashes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'PrereleaseBuildEvidence.psm1') -Force

if ($SelfTest) {
    Invoke-PrereleaseBuildEvidenceSelfTest
    return
}

$firstPath = [System.IO.Path]::GetFullPath($EvidencePath)
if (-not (Test-Path -LiteralPath $firstPath -PathType Leaf)) {
    throw "EvidencePath '$firstPath' does not exist."
}
$first = [System.IO.File]::ReadAllBytes($firstPath)
$result = Test-PrereleaseBuildEvidenceBytes -Bytes $first -RepositoryRoot $repositoryRoot -VerifySelectedInputHashes:$VerifySelectedInputHashes

if ($SecondEvidencePath) {
    $secondPath = [System.IO.Path]::GetFullPath($SecondEvidencePath)
    if (-not (Test-Path -LiteralPath $secondPath -PathType Leaf)) {
        throw "SecondEvidencePath '$secondPath' does not exist."
    }
    Assert-PrereleaseBuildEvidenceIdentity -First $first -Second ([System.IO.File]::ReadAllBytes($secondPath))
}

Write-Output "Verified canonical unsigned local build evidence: sha256=$($result.Sha256) commit=$($result.InitialCommit) sdk=$($result.SelectedSdk) runtimePack=$($result.RuntimePack)"
