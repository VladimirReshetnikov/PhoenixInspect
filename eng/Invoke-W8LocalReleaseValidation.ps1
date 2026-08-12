#requires -Version 7.2

<#
.SYNOPSIS
Runs the complete local W8.10 release-validation matrix headlessly.

.DESCRIPTION
Restores and builds the pinned solution, runs complete and focused test lanes into isolated TRX files, validates every
TRX independently, runs the preview demo, builds and smoke-validates the non-distributed prerelease payloads, runs the
repository guards, and writes deterministic local-only evidence to evidence.json. Focused lanes intentionally
re-execute tests covered by complete lanes. The script never supplies hosted evidence and never claims milestone or
release closure.

.PARAMETER OutputDirectory
The direct artifacts child named w8-local-release-validation, or the same name with a bounded suffix. The default is
artifacts/w8-local-release-validation.

.PARAMETER Force
Replaces only the validated OutputDirectory. Reparse-point paths and paths outside artifacts are rejected.

.PARAMETER List
Prints the deterministic command plan as JSON without changing the workspace or running any command.

.PARAMETER SelfTest
Exercises the TRX validator and W8.9 decision-candidate/owner-authority gate against passing and fail-closed synthetic
inputs without running the matrix.

.PARAMETER ValidateTrxPath
Validates one existing TRX file with the same fail-closed parser used by the release matrix.

.EXAMPLE
./eng/Invoke-W8LocalReleaseValidation.ps1 -List

.EXAMPLE
./eng/Invoke-W8LocalReleaseValidation.ps1 -SelfTest

.EXAMPLE
./eng/Invoke-W8LocalReleaseValidation.ps1 -OutputDirectory artifacts/w8-local-release-validation -Force
#>

[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory = 'artifacts/w8-local-release-validation',

    [Parameter(ParameterSetName = 'Run')]
    [switch] $Force,

    [Parameter(Mandatory, ParameterSetName = 'List')]
    [switch] $List,

    [Parameter(Mandatory, ParameterSetName = 'SelfTest')]
    [switch] $SelfTest,

    [Parameter(Mandatory, ParameterSetName = 'ValidateTrx')]
    [ValidateNotNullOrEmpty()]
    [string] $ValidateTrxPath,

    [Parameter(Mandatory, ParameterSetName = 'InternalGuard')]
    [ValidateSet('AuthoredVocabulary', 'CleanTree', 'OneParserSite')]
    [string] $InternalGuard
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$headlessWrapperPath = Join-Path $PSScriptRoot 'Invoke-HeadlessProcess.ps1'
$scriptPath = $PSCommandPath
$integrationProject = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'
$unitProject = 'tests/PhoenixInspect.Tests/PhoenixInspect.Tests.csproj'
$w8FrozenV1RelativePath = 'tests/corpus/w8-static-field-incidents-v1.json'
$w8FrozenV1Sha256 = '64bd03319c774b16a4e49dca0c85c43059f8f7d220873ecc8f31c6774842ff37'
$w8ReconciledV2RelativePath = 'tests/corpus/w8-static-field-incidents-v2.json'
$w8ReconciledV2Sha256 = '468c78076bf3e149b395647fc1557c234d25399d48d42b425824b70bd413d35a'
$w8DecisionCandidateRelativePath = 'tests/corpus/w8-static-field-portfolio-decision-candidate-v1.json'
$w8DecisionCandidateId = 'interpreter-w8-static-field-portfolio-decision-candidate-v1'
$w8DecisionAuthorityRelativePath = 'tests/corpus/w8-static-field-portfolio-decision-authority-v1.json'
$w8DecisionAuthorityId = 'interpreter-w8-static-field-portfolio-decision-authority-v1'
[int[]] $w8OwnerDispositionOrdinals = @(20, 21, 22, 26, 27, 29, 34)
[string[]] $w8OwnerDispositionActions = @(
    'retired-disproved-premise',
    'retired-disproved-premise',
    'retired-disproved-premise',
    'retired-disproved-premise',
    'retired-disproved-premise',
    'deferred-unrealized-physical-counterfactual',
    'deferred-unrealized-physical-counterfactual')
$w8ProposedCategory = 'observed-boundary-hardening'
$w8ProposedAction = 'Harden the observed first-boundary reporting surface without extending binding reach.'

function ConvertTo-RepositoryRelativePath {
    param([Parameter(Mandatory)][string] $Path)

    [System.IO.Path]::GetRelativePath($repositoryRoot, $Path).Replace('\', '/')
}

function Assert-PathIsInsideRepository {
    param([Parameter(Mandatory)][string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $relativePath = ConvertTo-RepositoryRelativePath $fullPath
    if ($relativePath -eq '.' -or
        $relativePath -eq '..' -or
        $relativePath.StartsWith('../', [System.StringComparison]::Ordinal) -or
        [System.IO.Path]::IsPathRooted($relativePath) -or
        $relativePath.Equals('.git', [System.StringComparison]::OrdinalIgnoreCase) -or
        $relativePath.StartsWith('.git/', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The output directory must be a non-.git descendant of the repository root: '$fullPath'."
    }

    if (-not [regex]::IsMatch(
            $relativePath,
            '^artifacts/w8-local-release-validation(?:-[a-z0-9][a-z0-9._-]{0,63})?$',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
                [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        throw "The output directory must be a direct artifacts/w8-local-release-validation[-suffix] path: '$fullPath'."
    }

    $currentPath = $fullPath
    while (-not $currentPath.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if ($currentPath.Equals($fullPath, [System.StringComparison]::OrdinalIgnoreCase) -and
                $item -isnot [System.IO.DirectoryInfo]) {
                throw "The output path exists but is not a directory: '$($item.FullName)'."
            }
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "The output path must not traverse a reparse point: '$($item.FullName)'."
            }
        }

        $parent = [System.IO.Directory]::GetParent($currentPath)
        if ($null -eq $parent) {
            throw "Could not validate the output path ancestry for '$fullPath'."
        }

        $currentPath = $parent.FullName
    }

    $fullPath
}

function Assert-ValidatorOwnedOutput {
    param([Parameter(Mandatory)][string] $Path)

    $item = Get-Item -LiteralPath $Path -Force
    if ($item -isnot [System.IO.DirectoryInfo] -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to replace non-directory or reparse-point validation output '$Path'."
    }

    $children = @(Get-ChildItem -LiteralPath $Path -Force)
    $evidenceItems = @($children | Where-Object {
        -not $_.PSIsContainer -and $_.Name -ceq 'evidence.json'
    })
    $resultItems = @($children | Where-Object {
        $_.PSIsContainer -and $_.Name -ceq 'test-results'
    })
    if ($children.Count -ne 2 -or $evidenceItems.Count -ne 1 -or $resultItems.Count -ne 1) {
        throw "Refusing to replace '$Path': prior validator output must contain exactly evidence.json and test-results."
    }

    $reparsePoints = @(Get-ChildItem -LiteralPath $Path -Recurse -Force | Where-Object {
        ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
    })
    if ($reparsePoints.Count -ne 0) {
        throw "Refusing to replace output containing a reparse point: '$($reparsePoints[0].FullName)'."
    }

    try {
        $evidence = [System.IO.File]::ReadAllText($evidenceItems[0].FullName) |
            ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "Refusing to replace '$Path': evidence.json is not valid validator evidence. $($_.Exception.Message)"
    }
    if ($evidence -isnot [System.Collections.IDictionary] -or
        -not $evidence.Contains('schema') -or
        -not $evidence.Contains('scope') -or
        -not $evidence.Contains('commands') -or
        -not $evidence.Contains('repository') -or
        [string]$evidence['schema'] -cne 'phoenixinspect.w8-local-release-validation/evidence-v1' -or
        $evidence['scope'] -isnot [System.Collections.IDictionary]) {
        throw "Refusing to replace '$Path': evidence.json does not carry the validator-owned local-only identity."
    }
    $scope = [System.Collections.IDictionary]$evidence['scope']
    if (-not $scope.Contains('kind') -or
        -not $scope.Contains('hostedEvidence') -or
        -not $scope.Contains('closureClaim') -or
        [string]$scope['kind'] -cne 'LocalOnly' -or
        [string]$scope['hostedEvidence'] -cne 'NotRun' -or
        $scope['closureClaim'] -isnot [bool] -or
        [bool]$scope['closureClaim'] -ne $false -or
        $null -eq $evidence['commands'] -or
        $null -eq $evidence['repository']) {
        throw "Refusing to replace '$Path': evidence.json does not carry the validator-owned local-only identity."
    }
}

function Get-GitOutput {
    param([Parameter(Mandatory)][string[]] $ArgumentList)

    $output = @(& git -C $repositoryRoot @ArgumentList)
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode -or $exitCode -ne 0) {
        throw "git -C $repositoryRoot $($ArgumentList -join ' ') failed with exit code '$exitCode'."
    }

    @($output | ForEach-Object { [string]$_ })
}

function Get-RepositoryState {
    $commitLines = @(Get-GitOutput @('rev-parse', '--verify', 'HEAD'))
    if ($commitLines.Count -ne 1 -or $commitLines[0] -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'git rev-parse did not return exactly one full commit identity.'
    }

    $statusLines = @(
        Get-GitOutput @(
            '-c',
            'core.quotepath=false',
            'status',
            '--porcelain=v1',
            '--untracked-files=all'))

    [ordered]@{
        commit = $commitLines[0].ToLowerInvariant()
        isDirty = $statusLines.Count -ne 0
        entryCount = $statusLines.Count
        entries = [string[]]$statusLines
    }
}

function Assert-TrackedHeadFile {
    param([Parameter(Mandatory)][string] $RelativePath)

    $trackedPaths = @(Get-GitOutput @(
        'ls-tree',
        '--full-tree',
        '-r',
        '--name-only',
        'HEAD',
        '--',
        $RelativePath))
    if ($trackedPaths.Count -ne 1 -or $trackedPaths[0] -cne $RelativePath) {
        throw "Required W8.9 release input is not a tracked file in HEAD: '$RelativePath'."
    }

    $headBlobLines = @(Get-GitOutput @('rev-parse', '--verify', "HEAD:$RelativePath"))
    if ($headBlobLines.Count -ne 1 -or
        $headBlobLines[0] -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Required W8.9 release input does not resolve to exactly one tracked HEAD blob: '$RelativePath'."
    }

    $workingBlobLines = @(Get-GitOutput @('hash-object', '--no-filters', '--', $RelativePath))
    if ($workingBlobLines.Count -ne 1 -or
        $workingBlobLines[0] -notmatch '^[0-9a-fA-F]{40}$' -or
        $workingBlobLines[0] -cne $headBlobLines[0]) {
        throw "Required W8.9 release input bytes differ from the tracked HEAD blob: '$RelativePath'."
    }

    $headBlobLines[0].ToLowerInvariant()
}

function Get-RequiredReleaseInputSpecs {
    @(
        [pscustomobject][ordered]@{
            Role = 'FrozenPredeclaration'
            Path = $w8FrozenV1RelativePath
            Requirement = 'ExactSha256'
            Sha256 = $w8FrozenV1Sha256
        }
        [pscustomobject][ordered]@{
            Role = 'ProducedOutcomeReconciliation'
            Path = $w8ReconciledV2RelativePath
            Requirement = 'ExactSha256'
            Sha256 = $w8ReconciledV2Sha256
        }
        [pscustomobject][ordered]@{
            Role = 'DecisionCandidate'
            Path = $w8DecisionCandidateRelativePath
            Requirement = 'ProposedDecisionCandidate'
            Sha256 = $null
        }
        [pscustomobject][ordered]@{
            Role = 'OwnerAuthorityEnvelope'
            Path = $w8DecisionAuthorityRelativePath
            Requirement = 'CandidateBoundOwnerApproval'
            Sha256 = $null
        }
    )
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string] $Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        [System.Convert]::ToHexString($algorithm.ComputeHash($stream)).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Assert-NoDuplicateJsonProperties {
    param(
        [Parameter(Mandatory)][string] $Json,
        [Parameter(Mandatory)][string] $DisplayPath
    )

    try {
        $root = [System.Text.Json.Nodes.JsonNode]::Parse($Json)
    }
    catch [System.Text.Json.JsonException] {
        throw "W8.9 JSON input is malformed: '$DisplayPath'. $($_.Exception.Message)"
    }

    $visit = $null
    $visit = {
        param([System.Text.Json.Nodes.JsonNode] $Node)

        if ($null -eq $Node) {
            return
        }
        if ($Node -is [System.Text.Json.Nodes.JsonObject]) {
            try {
                foreach ($property in $Node) {
                    & $visit $property.Value
                }
            }
            catch {
                throw "Duplicate JSON object properties are forbidden in '$DisplayPath'. $($_.Exception.Message)"
            }
        }
        elseif ($Node -is [System.Text.Json.Nodes.JsonArray]) {
            foreach ($element in $Node) {
                & $visit $element
            }
        }
    }
    & $visit $root
}

function ConvertFrom-StrictJsonObject {
    param(
        [Parameter(Mandatory)][string] $Json,
        [Parameter(Mandatory)][string] $DisplayPath,
        [Parameter(Mandatory)][string] $Description
    )

    Assert-NoDuplicateJsonProperties $Json $DisplayPath
    try {
        $result = $Json | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "W8.9 $Description is not valid JSON: '$DisplayPath'. $($_.Exception.Message)"
    }
    if ($result -isnot [System.Collections.IDictionary]) {
        throw "W8.9 $Description must be a JSON object: '$DisplayPath'."
    }
    $result
}

function Assert-ExactObjectKeys {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Object,
        [Parameter(Mandatory)][string[]] $ExpectedKeys,
        [Parameter(Mandatory)][string] $Description,
        [Parameter(Mandatory)][string] $DisplayPath
    )

    if ($Object.Count -ne $ExpectedKeys.Count) {
        throw "W8.9 $Description must contain exactly $($ExpectedKeys.Count) fields: '$DisplayPath'."
    }
    foreach ($key in $ExpectedKeys) {
        if (-not $Object.Contains($key)) {
            throw "W8.9 $Description is missing required field '$key': '$DisplayPath'."
        }
    }
}

function Assert-DecisionCandidateRecord {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Candidate,
        [Parameter(Mandatory)][string] $DisplayPath,
        [Parameter(Mandatory)][string] $Sha256
    )

    Assert-ExactObjectKeys $Candidate @(
        'schemaVersion',
        'candidateId',
        'evidenceKind',
        'declaredPurpose',
        'evidenceInputs',
        'metricRules',
        'proposedCounterfactualDispositions',
        'verifiedCorrectedCounterfactuals',
        'confirmedNonDecisionChangingCorrections',
        'candidatePortfolioMetrics',
        'firstBoundaryCounts',
        'candidateCategoryMetrics',
        'qualification',
        'candidateSelection',
        'scopeLimits') 'decision-candidate root' $DisplayPath
    if (-not $Candidate.Contains('schemaVersion') -or
        $Candidate['schemaVersion'] -isnot [long] -or
        [long]$Candidate['schemaVersion'] -ne 1 -or
        -not $Candidate.Contains('candidateId') -or
        $Candidate['candidateId'] -isnot [string] -or
        [string]$Candidate['candidateId'] -cne $w8DecisionCandidateId -or
        -not $Candidate.Contains('evidenceKind') -or
        $Candidate['evidenceKind'] -isnot [string] -or
        [string]$Candidate['evidenceKind'] -cne 'derived-designed-synthetic-decision-candidate') {
        throw "W8.9 decision candidate has the wrong frozen identity: '$DisplayPath'."
    }
    if ($Sha256 -notmatch '^[0-9a-f]{64}$') {
        throw "W8.9 decision candidate does not have a canonical computed SHA-256: '$DisplayPath'."
    }
    foreach ($prohibitedField in @(
            'decisionAuthority',
            'finalDecision',
            'counterfactualOwnerDispositions',
            'ownerDisposition',
            'ownerDispositions')) {
        if ($Candidate.Contains($prohibitedField)) {
            throw "W8.9 decision candidate must not contain final or owner-authority field '$prohibitedField': '$DisplayPath'."
        }
    }

    if (-not $Candidate.Contains('evidenceInputs')) {
        throw "W8.9 decision candidate has no frozen evidence inputs: '$DisplayPath'."
    }
    $evidenceInputs = @($Candidate['evidenceInputs'])
    $expectedEvidenceInputs = @(
        [pscustomobject]@{
            Role = 'frozen-predeclaration'
            Path = $w8FrozenV1RelativePath
            CorpusId = 'interpreter-w8-static-field-incidents-v1'
            Sha256 = $w8FrozenV1Sha256
        },
        [pscustomobject]@{
            Role = 'produced-outcome-reconciliation'
            Path = $w8ReconciledV2RelativePath
            CorpusId = 'interpreter-w8-static-field-incidents-v2'
            Sha256 = $w8ReconciledV2Sha256
        })
    if ($evidenceInputs.Count -ne $expectedEvidenceInputs.Count) {
        throw "W8.9 decision candidate must bind exactly the frozen v1 and reconciled v2 inputs: '$DisplayPath'."
    }
    for ($index = 0; $index -lt $expectedEvidenceInputs.Count; $index++) {
        if ($evidenceInputs[$index] -isnot [System.Collections.IDictionary]) {
            throw "W8.9 decision candidate evidence input $index is malformed: '$DisplayPath'."
        }
        $input = [System.Collections.IDictionary]$evidenceInputs[$index]
        $expected = $expectedEvidenceInputs[$index]
        Assert-ExactObjectKeys $input @('role', 'path', 'corpusId', 'sha256') "decision-candidate evidence input $index" $DisplayPath
        if ($input['role'] -isnot [string] -or [string]$input['role'] -cne $expected.Role -or
            $input['path'] -isnot [string] -or [string]$input['path'] -cne $expected.Path -or
            $input['corpusId'] -isnot [string] -or [string]$input['corpusId'] -cne $expected.CorpusId -or
            $input['sha256'] -isnot [string] -or [string]$input['sha256'] -cne $expected.Sha256) {
            throw "W8.9 decision candidate evidence input $index does not match its immutable source: '$DisplayPath'."
        }
    }

    if (-not $Candidate.Contains('proposedCounterfactualDispositions')) {
        throw "W8.9 decision candidate has no proposed counterfactual dispositions: '$DisplayPath'."
    }
    $dispositions = @($Candidate['proposedCounterfactualDispositions'])
    if ($dispositions.Count -ne $w8OwnerDispositionOrdinals.Count) {
        throw "W8.9 decision candidate must contain exactly seven proposed dispositions: '$DisplayPath'."
    }
    $retiredCount = 0
    $deferredCount = 0
    for ($index = 0; $index -lt $dispositions.Count; $index++) {
        if ($dispositions[$index] -isnot [System.Collections.IDictionary]) {
            throw "W8.9 decision candidate disposition $index is malformed: '$DisplayPath'."
        }
        $disposition = [System.Collections.IDictionary]$dispositions[$index]
        Assert-ExactObjectKeys $disposition @(
            'id',
            'ordinal',
            'disposition',
            'useful',
            'decisionChanging',
            'reason') "decision-candidate disposition $index" $DisplayPath
        if (-not $disposition.Contains('ordinal') -or
            $disposition['ordinal'] -isnot [long] -or
            [long]$disposition['ordinal'] -ne $w8OwnerDispositionOrdinals[$index] -or
            -not $disposition.Contains('disposition') -or
            $disposition['disposition'] -isnot [string] -or
            [string]$disposition['disposition'] -cne $w8OwnerDispositionActions[$index] -or
            -not $disposition.Contains('decisionChanging') -or
            $disposition['decisionChanging'] -isnot [bool] -or
            [bool]$disposition['decisionChanging']) {
            throw "W8.9 decision candidate dispositions must match the proposed ordinal-to-action map and carry no decision-changing credit: '$DisplayPath'."
        }
        switch ($w8OwnerDispositionActions[$index]) {
            'retired-disproved-premise' { $retiredCount++ }
            'deferred-unrealized-physical-counterfactual' { $deferredCount++ }
            default { throw 'The validator contains an unsupported proposed owner action.' }
        }
    }
    if ($retiredCount -ne 5 -or $deferredCount -ne 2) {
        throw "W8.9 decision candidate must propose five retired and two deferred dispositions: '$DisplayPath'."
    }

    if (-not $Candidate.Contains('candidateSelection') -or
        $Candidate['candidateSelection'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 decision candidate has no proposed successor selection: '$DisplayPath'."
    }
    $selection = [System.Collections.IDictionary]$Candidate['candidateSelection']
    Assert-ExactObjectKeys $selection @(
        'status',
        'tieDefers',
        'proposedCategory',
        'proposedAction',
        'winningSubstantiveKey',
        'runnerUpCategory',
        'runnerUpSubstantiveKey',
        'decisiveKey',
        'implementationDisposition',
        'rationale') 'decision-candidate selection' $DisplayPath
    if ($selection['status'] -isnot [string] -or
        [string]$selection['status'] -cne 'computed-under-proposals-pending-owner-approval' -or
        $selection['tieDefers'] -isnot [bool] -or
        [bool]$selection['tieDefers'] -or
        $selection['proposedCategory'] -isnot [string] -or
        [string]$selection['proposedCategory'] -cne $w8ProposedCategory -or
        $selection['proposedAction'] -isnot [string] -or
        [string]$selection['proposedAction'] -cne $w8ProposedAction -or
        $selection['winningSubstantiveKey'] -isnot [string] -or
        [string]$selection['winningSubstantiveKey'] -cne '14:4:7:9' -or
        $selection['runnerUpCategory'] -isnot [string] -or
        [string]$selection['runnerUpCategory'] -cne 'additional-static-storage-family' -or
        $selection['runnerUpSubstantiveKey'] -isnot [string] -or
        [string]$selection['runnerUpSubstantiveKey'] -cne '12:4:6:11' -or
        $selection['decisiveKey'] -isnot [string] -or
        [string]$selection['decisiveKey'] -cne 'incidentCount' -or
        $selection['implementationDisposition'] -isnot [string] -or
        [string]$selection['implementationDisposition'] -cne 'proposed-for-post-w8-planning-not-implemented-by-w8' -or
        $selection['rationale'] -isnot [string] -or
        [string]::IsNullOrWhiteSpace([string]$selection['rationale'])) {
        throw "W8.9 decision candidate does not carry the exact pending successor selection: '$DisplayPath'."
    }

    if (-not $Candidate.Contains('candidatePortfolioMetrics') -or
        $Candidate['candidatePortfolioMetrics'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 decision candidate has no portfolio metrics: '$DisplayPath'."
    }
    $portfolioMetrics = [System.Collections.IDictionary]$Candidate['candidatePortfolioMetrics']
    $expectedMetrics = [ordered]@{
        calculationBasis = 'conditional-on-unapproved-proposed-dispositions'
        incidentCount = 35L
        applicationShapeCount = 4L
        usefulCount = 33L
        decisionChangingCount = 19L
        attributableEvidenceCount = 25L
        exactOrNoBoundaryCount = 24L
        firstBoundaryCount = 11L
        representativeObservationCount = 0L
        executedBaselineCount = 35L
        manifestOnlyBaselineCount = 0L
        retiredCounterfactualCount = 5L
        deferredCounterfactualCount = 2L
    }
    Assert-ExactObjectKeys $portfolioMetrics ([string[]]$expectedMetrics.Keys) 'decision-candidate portfolio metrics' $DisplayPath
    foreach ($metricName in $expectedMetrics.Keys) {
        if ($metricName -ceq 'calculationBasis') {
            if ($portfolioMetrics[$metricName] -isnot [string] -or
                [string]$portfolioMetrics[$metricName] -cne [string]$expectedMetrics[$metricName]) {
                throw "W8.9 decision-candidate metric basis must remain conditional on unapproved proposed dispositions: '$DisplayPath'."
            }
        }
        elseif ($portfolioMetrics[$metricName] -isnot [long] -or
            [long]$portfolioMetrics[$metricName] -ne [long]$expectedMetrics[$metricName]) {
            throw "W8.9 decision-candidate metric '$metricName' must be exactly $($expectedMetrics[$metricName]): '$DisplayPath'."
        }
    }

    if (-not $Candidate.Contains('scopeLimits') -or
        $Candidate['scopeLimits'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 decision candidate has no scope limits: '$DisplayPath'."
    }
    $scopeLimits = [System.Collections.IDictionary]$Candidate['scopeLimits']
    Assert-ExactObjectKeys $scopeLimits @(
        'ownerAuthorityClaimed',
        'w8_9ClosureClaimed',
        'representativeEvidenceClaimed',
        'proposedSuccessorImplementedByW8',
        'w8_10ClosureClaimed',
        'promotionPolicy') 'decision-candidate scope limits' $DisplayPath
    foreach ($falseClaim in @(
            'ownerAuthorityClaimed',
            'w8_9ClosureClaimed',
            'representativeEvidenceClaimed',
            'proposedSuccessorImplementedByW8',
            'w8_10ClosureClaimed')) {
        if ($scopeLimits[$falseClaim] -isnot [bool] -or [bool]$scopeLimits[$falseClaim]) {
            throw "W8.9 decision-candidate scope limit '$falseClaim' must be exactly false: '$DisplayPath'."
        }
    }
    if ($scopeLimits['promotionPolicy'] -isnot [string] -or
        [string]$scopeLimits['promotionPolicy'] -cne 'no-generated-row-may-be-promoted-to-representative-observation') {
        throw "W8.9 decision candidate has the wrong representative-evidence promotion policy: '$DisplayPath'."
    }

    [ordered]@{
        candidateId = $w8DecisionCandidateId
        path = $DisplayPath
        sha256 = $Sha256
        status = [string]$selection['status']
        proposedDispositionOrdinals = [int[]]$w8OwnerDispositionOrdinals
        proposedDispositionActions = [string[]]$w8OwnerDispositionActions
        retiredDispositionCount = $retiredCount
        deferredDispositionCount = $deferredCount
        proposedCategory = $w8ProposedCategory
        proposedAction = $w8ProposedAction
    }
}

function Assert-OwnerAuthorityEnvelope {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Authority,
        [Parameter(Mandatory)][System.Collections.IDictionary] $CandidateEvidence,
        [Parameter(Mandatory)][string] $DisplayPath
    )

    Assert-ExactObjectKeys $Authority @(
        'schemaVersion',
        'authorityId',
        'evidenceKind',
        'status',
        'approvedBy',
        'approvedOn',
        'candidateBinding',
        'approvedCounterfactualDispositions',
        'approvedSelection',
        'scopeLimits') 'owner-authority envelope' $DisplayPath
    if ($Authority['schemaVersion'] -isnot [long] -or
        [long]$Authority['schemaVersion'] -ne 1 -or
        $Authority['authorityId'] -isnot [string] -or
        [string]$Authority['authorityId'] -cne $w8DecisionAuthorityId -or
        $Authority['evidenceKind'] -isnot [string] -or
        [string]$Authority['evidenceKind'] -cne 'owner-authority-envelope' -or
        [string]$Authority['authorityId'] -ceq [string]$CandidateEvidence['candidateId'] -or
        [string]$Authority['evidenceKind'] -ceq 'derived-designed-synthetic-decision-candidate' -or
        $Authority['status'] -isnot [string] -or
        [string]$Authority['status'] -cne 'owner-approved' -or
        $Authority['approvedBy'] -isnot [string] -or
        [string]$Authority['approvedBy'] -cne 'Vladimir Reshetnikov' -or
        $Authority['approvedOn'] -isnot [string]) {
        throw "W8.9 owner-authority envelope has the wrong identity, kind, status, approver, or approval-date type: '$DisplayPath'."
    }

    [datetime] $approvalDate = [datetime]::MinValue
    $approvalDateText = [string]$Authority['approvedOn']
    if (-not [datetime]::TryParseExact(
            $approvalDateText,
            'yyyy-MM-dd',
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$approvalDate) -or
        $approvalDate.ToString('yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture) -cne $approvalDateText) {
        throw "W8.9 owner-authority approval date must be a real calendar date in exact yyyy-MM-dd form: '$DisplayPath'."
    }

    if ($Authority['candidateBinding'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 owner-authority envelope has no candidate binding: '$DisplayPath'."
    }
    $binding = [System.Collections.IDictionary]$Authority['candidateBinding']
    Assert-ExactObjectKeys $binding @('path', 'candidateId', 'sha256') 'owner-authority candidate binding' $DisplayPath
    if ($binding['path'] -isnot [string] -or
        [string]$binding['path'] -cne [string]$CandidateEvidence['path'] -or
        $binding['candidateId'] -isnot [string] -or
        [string]$binding['candidateId'] -cne [string]$CandidateEvidence['candidateId'] -or
        $binding['sha256'] -isnot [string] -or
        [string]$binding['sha256'] -notmatch '^[0-9a-f]{64}$' -or
        [string]$binding['sha256'] -cne [string]$CandidateEvidence['sha256']) {
        throw "W8.9 owner-authority envelope does not bind the exact tracked candidate path, id, and computed SHA-256: '$DisplayPath'."
    }

    $approvedDispositions = @($Authority['approvedCounterfactualDispositions'])
    if ($approvedDispositions.Count -ne $w8OwnerDispositionOrdinals.Count) {
        throw "W8.9 owner-authority envelope must approve exactly seven ordinal-to-action pairs: '$DisplayPath'."
    }
    for ($index = 0; $index -lt $approvedDispositions.Count; $index++) {
        if ($approvedDispositions[$index] -isnot [System.Collections.IDictionary]) {
            throw "W8.9 owner-authority disposition approval $index is malformed: '$DisplayPath'."
        }
        $approvedDisposition = [System.Collections.IDictionary]$approvedDispositions[$index]
        Assert-ExactObjectKeys $approvedDisposition @('ordinal', 'action') "owner-authority disposition approval $index" $DisplayPath
        if ($approvedDisposition['ordinal'] -isnot [long] -or
            [long]$approvedDisposition['ordinal'] -ne $w8OwnerDispositionOrdinals[$index] -or
            $approvedDisposition['action'] -isnot [string] -or
            [string]$approvedDisposition['action'] -cne $w8OwnerDispositionActions[$index] -or
            [long]$approvedDisposition['ordinal'] -ne [long]$CandidateEvidence['proposedDispositionOrdinals'][$index] -or
            [string]$approvedDisposition['action'] -cne [string]$CandidateEvidence['proposedDispositionActions'][$index]) {
            throw "W8.9 owner-authority envelope does not approve the exact candidate ordinal-to-action map: '$DisplayPath'."
        }
    }

    if ($Authority['approvedSelection'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 owner-authority envelope has no approved candidate selection: '$DisplayPath'."
    }
    $approvedSelection = [System.Collections.IDictionary]$Authority['approvedSelection']
    Assert-ExactObjectKeys $approvedSelection @('category', 'action') 'owner-authority approved selection' $DisplayPath
    if ($approvedSelection['category'] -isnot [string] -or
        [string]$approvedSelection['category'] -cne [string]$CandidateEvidence['proposedCategory'] -or
        $approvedSelection['action'] -isnot [string] -or
        [string]$approvedSelection['action'] -cne [string]$CandidateEvidence['proposedAction']) {
        throw "W8.9 owner-authority envelope does not approve the exact candidate category and action: '$DisplayPath'."
    }

    if ($Authority['scopeLimits'] -isnot [System.Collections.IDictionary]) {
        throw "W8.9 owner-authority envelope has no scope limits: '$DisplayPath'."
    }
    $scopeLimits = [System.Collections.IDictionary]$Authority['scopeLimits']
    Assert-ExactObjectKeys $scopeLimits @('w8_10ClosureClaimed') 'owner-authority scope limits' $DisplayPath
    if ($scopeLimits['w8_10ClosureClaimed'] -isnot [bool] -or
        [bool]$scopeLimits['w8_10ClosureClaimed']) {
        throw "W8.9 owner-authority envelope must not claim W8.10 closure: '$DisplayPath'."
    }

    [ordered]@{
        authorityId = $w8DecisionAuthorityId
        status = [string]$Authority['status']
        approvedBy = [string]$Authority['approvedBy']
        approvedOn = $approvalDateText
        candidatePath = [string]$binding['path']
        candidateId = [string]$binding['candidateId']
        candidateSha256 = [string]$binding['sha256']
        approvedDispositionOrdinals = [int[]]$w8OwnerDispositionOrdinals
        retiredDispositionCount = [int]$CandidateEvidence['retiredDispositionCount']
        deferredDispositionCount = [int]$CandidateEvidence['deferredDispositionCount']
        approvedCategory = [string]$approvedSelection['category']
        approvedAction = [string]$approvedSelection['action']
        w8_10ClosureClaimed = $false
    }
}

function Assert-RequiredReleaseInputs {
    $validatedInputs = [System.Collections.Generic.List[object]]::new()
    $candidateEvidence = $null
    foreach ($spec in Get-RequiredReleaseInputSpecs) {
        $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $spec.Path))
        if (-not [System.IO.File]::Exists($fullPath)) {
            if ($spec.Role -ceq 'OwnerAuthorityEnvelope') {
                throw "W8.9 owner-authority envelope is missing: '$($spec.Path)'. Owner approval remains absent; no W8.10 validation output was changed."
            }
            throw "Required immutable W8.9 release input is missing: '$($spec.Path)'."
        }

        $item = Get-Item -LiteralPath $fullPath -Force
        if ($item -isnot [System.IO.FileInfo] -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Required W8.9 release input must be a regular non-reparse file: '$($spec.Path)'."
        }

        $headBlob = Assert-TrackedHeadFile $spec.Path
        $sha256 = Get-Sha256Hex $fullPath
        $semanticEvidence = $null
        if ($spec.Requirement -ceq 'ExactSha256') {
            if ($sha256 -cne $spec.Sha256) {
                throw "Immutable W8.9 release input hash mismatch for '$($spec.Path)': expected $($spec.Sha256), observed $sha256."
            }
        }
        elseif ($spec.Requirement -ceq 'ProposedDecisionCandidate') {
            $candidateJson = [System.IO.File]::ReadAllText($fullPath)
            $candidate = ConvertFrom-StrictJsonObject $candidateJson $spec.Path 'decision-candidate input'
            $candidateEvidence = Assert-DecisionCandidateRecord $candidate $spec.Path $sha256
            $semanticEvidence = $candidateEvidence
        }
        elseif ($spec.Requirement -ceq 'CandidateBoundOwnerApproval') {
            if ($null -eq $candidateEvidence) {
                throw 'The validator release-input plan must validate the decision candidate before its owner-authority envelope.'
            }
            $authorityJson = [System.IO.File]::ReadAllText($fullPath)
            $authority = ConvertFrom-StrictJsonObject $authorityJson $spec.Path 'owner-authority envelope'
            $semanticEvidence = Assert-OwnerAuthorityEnvelope $authority $candidateEvidence $spec.Path
        }
        else {
            throw "Unknown W8.9 release-input requirement '$($spec.Requirement)'."
        }

        $validatedInputs.Add([ordered]@{
            role = $spec.Role
            path = $spec.Path
            headBlob = $headBlob
            sha256 = $sha256
            semanticValidation = $semanticEvidence
        })
    }

    @($validatedInputs)
}

function Read-RequiredCounter {
    param(
        [Parameter(Mandatory)][System.Xml.XmlElement] $Counters,
        [Parameter(Mandatory)][string] $Name
    )

    $text = $Counters.GetAttribute($Name)
    [int] $value = 0
    if ([string]::IsNullOrWhiteSpace($text) -or
        -not [int]::TryParse(
            $text,
            [System.Globalization.NumberStyles]::None,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$value) -or
        $value -lt 0) {
        throw "The TRX Counters element has no valid non-negative '$Name' count."
    }

    $value
}

function Get-TrxEvidence {
    param(
        [Parameter(Mandatory)][xml] $Document,
        [Parameter(Mandatory)][string] $SourceName
    )

    $summaries = @($Document.SelectNodes("//*[local-name()='ResultSummary']"))
    if ($summaries.Count -ne 1) {
        throw "TRX '$SourceName' contains $($summaries.Count) ResultSummary elements; exactly one is required."
    }

    [System.Xml.XmlElement] $summary = $summaries[0]
    if ($summary.GetAttribute('outcome') -cne 'Completed') {
        throw "TRX '$SourceName' did not complete: outcome '$($summary.GetAttribute('outcome'))'."
    }

    $summaryErrorInfos = @($summary.SelectNodes(".//*[local-name()='ErrorInfo']"))
    if ($summaryErrorInfos.Count -ne 0) {
        throw "TRX '$SourceName' contains $($summaryErrorInfos.Count) run-level ErrorInfo elements."
    }
    $runInfos = @($summary.SelectNodes("./*[local-name()='RunInfos']/*[local-name()='RunInfo']"))
    foreach ($runInfoNode in $runInfos) {
        [System.Xml.XmlElement] $runInfo = $runInfoNode
        $runInfoOutcome = $runInfo.GetAttribute('outcome')
        if ($runInfoOutcome -cnotin @('Completed', 'Passed')) {
            throw "TRX '$SourceName' contains an adverse RunInfo outcome '$runInfoOutcome'."
        }
    }

    $counterNodes = @($summary.SelectNodes("./*[local-name()='Counters']"))
    if ($counterNodes.Count -ne 1) {
        throw "TRX '$SourceName' contains $($counterNodes.Count) summary Counters elements; exactly one is required."
    }

    [System.Xml.XmlElement] $counters = $counterNodes[0]
    $total = Read-RequiredCounter $counters 'total'
    $executed = Read-RequiredCounter $counters 'executed'
    $passed = Read-RequiredCounter $counters 'passed'
    $failed = Read-RequiredCounter $counters 'failed'
    $skipped = Read-RequiredCounter $counters 'notExecuted'
    $errorCount = Read-RequiredCounter $counters 'error'
    $timeoutCount = Read-RequiredCounter $counters 'timeout'
    $abortedCount = Read-RequiredCounter $counters 'aborted'
    $inconclusiveCount = Read-RequiredCounter $counters 'inconclusive'
    $passedButRunAbortedCount = Read-RequiredCounter $counters 'passedButRunAborted'
    $notRunnableCount = Read-RequiredCounter $counters 'notRunnable'
    $disconnectedCount = Read-RequiredCounter $counters 'disconnected'
    $warningCount = Read-RequiredCounter $counters 'warning'
    $completedCount = Read-RequiredCounter $counters 'completed'
    $inProgressCount = Read-RequiredCounter $counters 'inProgress'
    $pendingCount = Read-RequiredCounter $counters 'pending'

    $results = @($Document.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($total -eq 0 -or $results.Count -eq 0) {
        throw "TRX '$SourceName' selected zero tests."
    }

    if ($results.Count -ne $total) {
        throw "TRX '$SourceName' reports total=$total but contains $($results.Count) UnitTestResult elements."
    }

    $executionIds = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $resultTestIds = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    foreach ($resultNode in $results) {
        [System.Xml.XmlElement] $result = $resultNode
        $executionId = $result.GetAttribute('executionId')
        $testId = $result.GetAttribute('testId')
        $testName = $result.GetAttribute('testName')
        $outcome = $result.GetAttribute('outcome')

        if ([string]::IsNullOrWhiteSpace($executionId) -or
            [string]::IsNullOrWhiteSpace($testId) -or
            [string]::IsNullOrWhiteSpace($testName) -or
            [string]::IsNullOrWhiteSpace($outcome)) {
            throw "TRX '$SourceName' contains a result without executionId, testId, testName, or outcome."
        }

        if (-not $executionIds.Add($executionId)) {
            throw "TRX '$SourceName' contains duplicate executionId '$executionId'."
        }

        $null = $resultTestIds.Add($testId)
        if ($outcome -cne 'Passed') {
            throw "TRX '$SourceName' contains non-passed test '$testName' with outcome '$outcome'."
        }
    }

    $definitions = @($Document.SelectNodes("//*[local-name()='TestDefinitions']/*[local-name()='UnitTest']"))
    if ($definitions.Count -eq 0) {
        throw "TRX '$SourceName' contains no UnitTest definitions."
    }

    $definitionIds = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($definitionNode in $definitions) {
        [System.Xml.XmlElement] $definition = $definitionNode
        $definitionId = $definition.GetAttribute('id')
        if ([string]::IsNullOrWhiteSpace($definitionId)) {
            throw "TRX '$SourceName' contains a UnitTest definition without an id."
        }

        if (-not $definitionIds.Add($definitionId)) {
            throw "TRX '$SourceName' contains duplicate UnitTest definition id '$definitionId'."
        }
    }

    foreach ($testId in $resultTestIds) {
        if (-not $definitionIds.Contains($testId)) {
            throw "TRX '$SourceName' has a result whose UnitTest definition is missing: '$testId'."
        }
    }

    foreach ($definitionId in $definitionIds) {
        if (-not $resultTestIds.Contains($definitionId)) {
            throw "TRX '$SourceName' has a UnitTest definition with no result: '$definitionId'."
        }
    }

    if ($executed -ne $total -or
        $passed -ne $total -or
        $failed -ne 0 -or
        $skipped -ne 0 -or
        $errorCount -ne 0 -or
        $timeoutCount -ne 0 -or
        $abortedCount -ne 0 -or
        $inconclusiveCount -ne 0 -or
        $passedButRunAbortedCount -ne 0 -or
        $notRunnableCount -ne 0 -or
        $disconnectedCount -ne 0 -or
        $warningCount -ne 0 -or
        $completedCount -ne 0 -or
        $inProgressCount -ne 0 -or
        $pendingCount -ne 0) {
        throw (
            "TRX '$SourceName' is not an all-passed zero-skip run: " +
            "total=$total executed=$executed passed=$passed failed=$failed skipped=$skipped " +
            "error=$errorCount timeout=$timeoutCount aborted=$abortedCount inconclusive=$inconclusiveCount " +
            "passedButRunAborted=$passedButRunAbortedCount notRunnable=$notRunnableCount " +
            "disconnected=$disconnectedCount warning=$warningCount completed=$completedCount " +
            "inProgress=$inProgressCount pending=$pendingCount.")
    }

    [ordered]@{
        total = $total
        executed = $executed
        passed = $passed
        failed = $failed
        skipped = $skipped
    }
}

function Read-TrxEvidenceFile {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The expected TRX result is missing: '$Path'."
    }

    try {
        [xml] $document = [System.IO.File]::ReadAllText($Path)
    }
    catch {
        throw "The expected TRX result is not well-formed XML: '$Path'. $($_.Exception.Message)"
    }

    Get-TrxEvidence $document (ConvertTo-RepositoryRelativePath $Path)
}

function Assert-RejectedTrx {
    param(
        [Parameter(Mandatory)][xml] $Document,
        [Parameter(Mandatory)][string] $CaseName
    )

    try {
        $null = Get-TrxEvidence $Document $CaseName
    }
    catch {
        return
    }

    throw "The TRX parser self-test '$CaseName' was expected to be rejected."
}

function Invoke-TrxParserSelfTest {
    [xml] $valid = @'
<TestRun>
  <Results>
    <UnitTestResult executionId="e1" testId="t1" testName="One(1)" outcome="Passed" />
    <UnitTestResult executionId="e2" testId="t1" testName="One(2)" outcome="Passed" />
  </Results>
  <TestDefinitions>
    <UnitTest id="t1" name="One" />
  </TestDefinitions>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
  </ResultSummary>
</TestRun>
'@
    $validEvidence = Get-TrxEvidence $valid 'valid'
    if ($validEvidence.total -ne 2 -or $validEvidence.passed -ne 2) {
        throw 'The valid TRX parser self-test returned incorrect counts.'
    }

    [xml] $zero = @'
<TestRun><Results /><TestDefinitions /><ResultSummary outcome="Completed"><Counters total="0" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $zero 'zero-tests'

    [xml] $skipped = @'
<TestRun><Results><UnitTestResult executionId="e1" testId="t1" testName="One" outcome="NotExecuted" /></Results><TestDefinitions><UnitTest id="t1" name="One" /></TestDefinitions><ResultSummary outcome="Completed"><Counters total="1" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="1" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $skipped 'skipped-test'

    [xml] $duplicate = @'
<TestRun><Results><UnitTestResult executionId="e1" testId="t1" testName="One" outcome="Passed" /><UnitTestResult executionId="e1" testId="t2" testName="Two" outcome="Passed" /></Results><TestDefinitions><UnitTest id="t1" name="One" /><UnitTest id="t2" name="Two" /></TestDefinitions><ResultSummary outcome="Completed"><Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $duplicate 'duplicate-result'

    [xml] $missingDefinition = @'
<TestRun><Results><UnitTestResult executionId="e1" testId="t1" testName="One" outcome="Passed" /></Results><TestDefinitions><UnitTest id="different" name="Different" /></TestDefinitions><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $missingDefinition 'missing-definition'

    [xml] $contradictoryErrorCounter = @'
<TestRun><Results><UnitTestResult executionId="e1" testId="t1" testName="One" outcome="Passed" /></Results><TestDefinitions><UnitTest id="t1" name="One" /></TestDefinitions><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" error="1" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $contradictoryErrorCounter 'contradictory-error-counter'

    [xml] $adverseRunInfo = @'
<TestRun><Results><UnitTestResult executionId="e1" testId="t1" testName="One" outcome="Passed" /></Results><TestDefinitions><UnitTest id="t1" name="One" /></TestDefinitions><ResultSummary outcome="Completed"><RunInfos><RunInfo outcome="Warning"><Text>Discovery warning</Text></RunInfo></RunInfos><Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" /></ResultSummary></TestRun>
'@
    Assert-RejectedTrx $adverseRunInfo 'adverse-run-info'

    [ordered]@{
        parser = 'TRX-v1'
        validCases = 1
        rejectedCases = 6
        status = 'Passed'
    }
}

function New-DecisionCandidateSelfTestRecord {
    $dispositions = @(
        for ($index = 0; $index -lt $w8OwnerDispositionOrdinals.Count; $index++) {
            [ordered]@{
                id = "self-test-$index"
                ordinal = $w8OwnerDispositionOrdinals[$index]
                disposition = $w8OwnerDispositionActions[$index]
                useful = $true
                decisionChanging = $false
                reason = 'Synthetic validator self-test proposal.'
            }
        }
    )

    $record = [ordered]@{
        schemaVersion = 1
        candidateId = $w8DecisionCandidateId
        evidenceKind = 'derived-designed-synthetic-decision-candidate'
        declaredPurpose = 'Synthetic validator self-test candidate; no owner authority or closure is claimed.'
        evidenceInputs = @(
            [ordered]@{
                role = 'frozen-predeclaration'
                path = $w8FrozenV1RelativePath
                corpusId = 'interpreter-w8-static-field-incidents-v1'
                sha256 = $w8FrozenV1Sha256
            },
            [ordered]@{
                role = 'produced-outcome-reconciliation'
                path = $w8ReconciledV2RelativePath
                corpusId = 'interpreter-w8-static-field-incidents-v2'
                sha256 = $w8ReconciledV2Sha256
            })
        metricRules = @('synthetic-self-test-rule')
        proposedCounterfactualDispositions = $dispositions
        verifiedCorrectedCounterfactuals = @()
        confirmedNonDecisionChangingCorrections = @()
        candidateSelection = [ordered]@{
            status = 'computed-under-proposals-pending-owner-approval'
            tieDefers = $false
            proposedCategory = $w8ProposedCategory
            proposedAction = $w8ProposedAction
            winningSubstantiveKey = '14:4:7:9'
            runnerUpCategory = 'additional-static-storage-family'
            runnerUpSubstantiveKey = '12:4:6:11'
            decisiveKey = 'incidentCount'
            implementationDisposition = 'proposed-for-post-w8-planning-not-implemented-by-w8'
            rationale = 'Synthetic self-test candidate remains pending owner approval.'
        }
        scopeLimits = [ordered]@{
            ownerAuthorityClaimed = $false
            w8_9ClosureClaimed = $false
            representativeEvidenceClaimed = $false
            proposedSuccessorImplementedByW8 = $false
            w8_10ClosureClaimed = $false
            promotionPolicy = 'no-generated-row-may-be-promoted-to-representative-observation'
        }
        candidatePortfolioMetrics = [ordered]@{
            calculationBasis = 'conditional-on-unapproved-proposed-dispositions'
            incidentCount = 35
            applicationShapeCount = 4
            usefulCount = 33
            decisionChangingCount = 19
            attributableEvidenceCount = 25
            exactOrNoBoundaryCount = 24
            firstBoundaryCount = 11
            representativeObservationCount = 0
            executedBaselineCount = 35
            manifestOnlyBaselineCount = 0
            retiredCounterfactualCount = 5
            deferredCounterfactualCount = 2
        }
        firstBoundaryCounts = [ordered]@{ none = 35 }
        candidateCategoryMetrics = @()
        qualification = [ordered]@{ qualifiedCategories = @($w8ProposedCategory) }
    }

    $record | ConvertTo-Json -Depth 8 | ConvertFrom-Json -AsHashtable
}

function New-OwnerAuthoritySelfTestEnvelope {
    param([Parameter(Mandatory)][string] $CandidateSha256)

    $approvedDispositions = @(
        for ($index = 0; $index -lt $w8OwnerDispositionOrdinals.Count; $index++) {
            [ordered]@{
                ordinal = $w8OwnerDispositionOrdinals[$index]
                action = $w8OwnerDispositionActions[$index]
            }
        }
    )
    $record = [ordered]@{
        schemaVersion = 1
        authorityId = $w8DecisionAuthorityId
        evidenceKind = 'owner-authority-envelope'
        status = 'owner-approved'
        approvedBy = 'Vladimir Reshetnikov'
        approvedOn = '2024-02-29'
        candidateBinding = [ordered]@{
            path = $w8DecisionCandidateRelativePath
            candidateId = $w8DecisionCandidateId
            sha256 = $CandidateSha256
        }
        approvedCounterfactualDispositions = $approvedDispositions
        approvedSelection = [ordered]@{
            category = $w8ProposedCategory
            action = $w8ProposedAction
        }
        scopeLimits = [ordered]@{
            w8_10ClosureClaimed = $false
        }
    }

    $record | ConvertTo-Json -Depth 8 | ConvertFrom-Json -AsHashtable
}

function Assert-RejectedDecisionCandidateRecord {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Candidate,
        [Parameter(Mandatory)][string] $Sha256,
        [Parameter(Mandatory)][string] $CaseName
    )

    try {
        $null = Assert-DecisionCandidateRecord $Candidate $CaseName $Sha256
    }
    catch {
        return
    }

    throw "The decision-candidate self-test '$CaseName' was expected to be rejected."
}

function Assert-RejectedOwnerAuthorityEnvelope {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Authority,
        [Parameter(Mandatory)][System.Collections.IDictionary] $CandidateEvidence,
        [Parameter(Mandatory)][string] $CaseName
    )

    try {
        $null = Assert-OwnerAuthorityEnvelope $Authority $CandidateEvidence $CaseName
    }
    catch {
        return
    }

    throw "The owner-authority-envelope self-test '$CaseName' was expected to be rejected."
}

function Invoke-OwnerAuthoritySelfTest {
    $candidateSha256 = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'
    $candidate = New-DecisionCandidateSelfTestRecord
    $candidateEvidence = Assert-DecisionCandidateRecord $candidate $w8DecisionCandidateRelativePath $candidateSha256
    if ($candidateEvidence.status -cne 'computed-under-proposals-pending-owner-approval' -or
        $candidateEvidence.retiredDispositionCount -ne 5 -or
        $candidateEvidence.deferredDispositionCount -ne 2) {
        throw 'The valid decision-candidate self-test returned incorrect evidence.'
    }

    $valid = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $evidence = Assert-OwnerAuthorityEnvelope $valid $candidateEvidence 'valid-owner-authority-envelope'
    if ($evidence.status -cne 'owner-approved' -or
        $evidence.candidateSha256 -cne $candidateSha256 -or
        $evidence.retiredDispositionCount -ne 5 -or
        $evidence.deferredDispositionCount -ne 2 -or
        $evidence.w8_10ClosureClaimed) {
        throw 'The valid owner-authority-envelope self-test returned incorrect evidence.'
    }

    $candidateWithAuthority = New-DecisionCandidateSelfTestRecord
    $candidateWithAuthority['decisionAuthority'] = [ordered]@{ status = 'owner-approved' }
    Assert-RejectedDecisionCandidateRecord $candidateWithAuthority $candidateSha256 'candidate-contains-decision-authority'

    $candidateWithFinal = New-DecisionCandidateSelfTestRecord
    $candidateWithFinal['finalDecision'] = [ordered]@{ status = 'selected' }
    Assert-RejectedDecisionCandidateRecord $candidateWithFinal $candidateSha256 'candidate-contains-final-decision'

    $wrongCandidateStatus = New-DecisionCandidateSelfTestRecord
    $wrongCandidateStatus['candidateSelection']['status'] = 'selected'
    Assert-RejectedDecisionCandidateRecord $wrongCandidateStatus $candidateSha256 'candidate-selected-status'

    $wrongCandidateAction = New-DecisionCandidateSelfTestRecord
    $wrongCandidateAction['proposedCounterfactualDispositions'][0]['disposition'] = 'deferred-unrealized-physical-counterfactual'
    Assert-RejectedDecisionCandidateRecord $wrongCandidateAction $candidateSha256 'candidate-wrong-proposed-action'

    $stringCandidateOrdinal = New-DecisionCandidateSelfTestRecord
    $stringCandidateOrdinal['proposedCounterfactualDispositions'][0]['ordinal'] = '20'
    Assert-RejectedDecisionCandidateRecord $stringCandidateOrdinal $candidateSha256 'candidate-string-ordinal'

    $creditedCandidateDisposition = New-DecisionCandidateSelfTestRecord
    $creditedCandidateDisposition['proposedCounterfactualDispositions'][0]['decisionChanging'] = $true
    Assert-RejectedDecisionCandidateRecord $creditedCandidateDisposition $candidateSha256 'candidate-credited-disposition'

    $wrongCandidateSelection = New-DecisionCandidateSelfTestRecord
    $wrongCandidateSelection['candidateSelection']['proposedCategory'] = 'additional-static-storage-family'
    Assert-RejectedDecisionCandidateRecord $wrongCandidateSelection $candidateSha256 'candidate-wrong-selection'

    $wrongCandidateMetric = New-DecisionCandidateSelfTestRecord
    $wrongCandidateMetric['candidatePortfolioMetrics']['executedBaselineCount'] = 34
    Assert-RejectedDecisionCandidateRecord $wrongCandidateMetric $candidateSha256 'candidate-wrong-metric'

    $claimedCandidateScope = New-DecisionCandidateSelfTestRecord
    $claimedCandidateScope['scopeLimits']['w8_10ClosureClaimed'] = $true
    Assert-RejectedDecisionCandidateRecord $claimedCandidateScope $candidateSha256 'candidate-claimed-w8-10-closure'

    $duplicateCandidateRejected = $false
    try {
        $null = ConvertFrom-StrictJsonObject '{"candidateId":"first","candidateId":"second"}' 'duplicate-candidate-property' 'decision-candidate input'
    }
    catch {
        $duplicateCandidateRejected = $true
    }
    if (-not $duplicateCandidateRejected) {
        throw 'The duplicate candidate-property self-test was expected to be rejected.'
    }

    $pending = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $pending['status'] = 'pending-owner-approval'
    Assert-RejectedOwnerAuthorityEnvelope $pending $candidateEvidence 'pending-owner-authority'

    $wrongBindingPath = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongBindingPath['candidateBinding']['path'] = 'tests/corpus/other.json'
    Assert-RejectedOwnerAuthorityEnvelope $wrongBindingPath $candidateEvidence 'wrong-candidate-path-binding'

    $wrongBindingId = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongBindingId['candidateBinding']['candidateId'] = 'other-candidate'
    Assert-RejectedOwnerAuthorityEnvelope $wrongBindingId $candidateEvidence 'wrong-candidate-id-binding'

    $wrongBindingSha = New-OwnerAuthoritySelfTestEnvelope ('f' * 64)
    Assert-RejectedOwnerAuthorityEnvelope $wrongBindingSha $candidateEvidence 'wrong-candidate-sha-binding'

    $swappedActions = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $swappedActions['approvedCounterfactualDispositions'][4]['action'] = 'deferred-unrealized-physical-counterfactual'
    $swappedActions['approvedCounterfactualDispositions'][5]['action'] = 'retired-disproved-premise'
    Assert-RejectedOwnerAuthorityEnvelope $swappedActions $candidateEvidence 'swapped-owner-actions'

    $wrongOrdinal = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongOrdinal['approvedCounterfactualDispositions'][6]['ordinal'] = 35
    Assert-RejectedOwnerAuthorityEnvelope $wrongOrdinal $candidateEvidence 'wrong-owner-ordinal'

    $wrongCategory = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongCategory['approvedSelection']['category'] = 'additional-static-storage-family'
    Assert-RejectedOwnerAuthorityEnvelope $wrongCategory $candidateEvidence 'wrong-approved-category'

    $wrongAction = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongAction['approvedSelection']['action'] = 'Extend binding reach.'
    Assert-RejectedOwnerAuthorityEnvelope $wrongAction $candidateEvidence 'wrong-approved-action'

    $invalidDate = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $invalidDate['approvedOn'] = '2025-02-29'
    Assert-RejectedOwnerAuthorityEnvelope $invalidDate $candidateEvidence 'invalid-calendar-date'

    $wrongDateShape = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongDateShape['approvedOn'] = '2024-2-29'
    Assert-RejectedOwnerAuthorityEnvelope $wrongDateShape $candidateEvidence 'non-canonical-date'

    $wrongDateType = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongDateType['approvedOn'] = 20240229
    Assert-RejectedOwnerAuthorityEnvelope $wrongDateType $candidateEvidence 'non-string-date'

    $stringOrdinal = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $stringOrdinal['approvedCounterfactualDispositions'][0]['ordinal'] = '20'
    Assert-RejectedOwnerAuthorityEnvelope $stringOrdinal $candidateEvidence 'string-approved-ordinal'

    $wrongKind = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $wrongKind['evidenceKind'] = 'derived-designed-synthetic-decision-candidate'
    Assert-RejectedOwnerAuthorityEnvelope $wrongKind $candidateEvidence 'candidate-kind-masquerade'

    $extraField = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $extraField['finalDecision'] = [ordered]@{ status = 'selected' }
    Assert-RejectedOwnerAuthorityEnvelope $extraField $candidateEvidence 'prohibited-final-field'

    $claimedClosure = New-OwnerAuthoritySelfTestEnvelope $candidateSha256
    $claimedClosure['scopeLimits']['w8_10ClosureClaimed'] = $true
    Assert-RejectedOwnerAuthorityEnvelope $claimedClosure $candidateEvidence 'claimed-w8-10-closure'

    $duplicateAuthorityRejected = $false
    try {
        $null = ConvertFrom-StrictJsonObject '{"status":"owner-approved","status":"pending-owner-approval"}' 'duplicate-authority-property' 'owner-authority envelope'
    }
    catch {
        $duplicateAuthorityRejected = $true
    }
    if (-not $duplicateAuthorityRejected) {
        throw 'The duplicate owner-authority-property self-test was expected to be rejected.'
    }

    [ordered]@{
        gate = 'W8.9-candidate-bound-owner-authority-v1'
        validCandidateCases = 1
        rejectedCandidateCases = 10
        validAuthorityCases = 1
        rejectedAuthorityCases = 15
        status = 'Passed'
    }
}

function Invoke-HeadProvenanceSelfTest {
    $headBlob = Assert-TrackedHeadFile $w8FrozenV1RelativePath
    if ($headBlob -notmatch '^[0-9a-f]{40}$') {
        throw 'The valid tracked-HEAD provenance self-test returned an invalid blob identity.'
    }

    try {
        $null = Assert-TrackedHeadFile 'tests/corpus/__w8-release-validator-missing-probe__.json'
    }
    catch {
        return [ordered]@{
            gate = 'tracked-HEAD-input-v1'
            validCases = 1
            rejectedCases = 1
            status = 'Passed'
        }
    }

    throw 'The missing tracked-HEAD provenance self-test was expected to be rejected.'
}

function Invoke-ValidatorSelfTest {
    [ordered]@{
        schema = 'phoenixinspect.w8-local-release-validation/self-test-v1'
        trx = Invoke-TrxParserSelfTest
        ownerAuthority = Invoke-OwnerAuthoritySelfTest
        headProvenance = Invoke-HeadProvenanceSelfTest
        commandPlan = Invoke-CommandPlanSelfTest
        status = 'Passed'
    } | ConvertTo-Json -Depth 8
}

function Invoke-AuthoredVocabularyGuard {
    $prohibited = [regex]::new(
        '(?i)(?://|/\*|\*)[^\r\n]*\b(?:draft|prototype|conceptual\s+design)\b',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $findings = [System.Collections.Generic.List[string]]::new()

    foreach ($rootName in @('src', 'tests')) {
        $rootPath = Join-Path $repositoryRoot $rootName
        $files = @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Filter '*.cs' |
            Where-Object {
                $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]'
            } |
            Sort-Object FullName)
        foreach ($file in $files) {
            $lineNumber = 0
            foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
                $lineNumber++
                if ($prohibited.IsMatch($line)) {
                    $relativePath = ConvertTo-RepositoryRelativePath $file.FullName
                    $findings.Add("$relativePath`:$lineNumber`:$line")
                }
            }
        }
    }

    if ($findings.Count -ne 0) {
        throw "Authored-scope stage vocabulary remains in C# comments:`n$($findings -join "`n")"
    }

    Write-Output 'Authored-scope vocabulary guard passed.'
}

function Invoke-OneParserSiteGuard {
    $parseSitePattern = [regex]::new(
        'SyntaxFactory\s*\.\s*ParseExpression\s*\(',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $sites = [System.Collections.Generic.List[string]]::new()

    $files = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File -Filter '*.cs' |
        Where-Object {
            $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]'
        } |
        Sort-Object FullName)
    foreach ($file in $files) {
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
            $lineNumber++
            foreach ($match in $parseSitePattern.Matches($line)) {
                $null = $match
                $sites.Add("$(ConvertTo-RepositoryRelativePath $file.FullName):$lineNumber")
            }
        }
    }

    $expectedFilePrefix = 'src/PhoenixInspect.Product.DumpQuery/DumpQueryParser.cs:'
    if ($sites.Count -ne 1 -or
        -not $sites[0].StartsWith($expectedFilePrefix, [System.StringComparison]::Ordinal)) {
        throw "Expected one production ParseExpression site in '$expectedFilePrefix'; observed: $($sites -join ', ')."
    }

    Write-Output "One-parser-site guard passed: $($sites[0])."
}

function Invoke-CleanTreeGuard {
    $null = Get-GitOutput @('diff', '--check', '--')
    $state = Get-RepositoryState
    if ($state.isDirty) {
        throw "The repository is not clean ($($state.entryCount) status entries)."
    }

    Write-Output "Clean-tree guard passed at $($state.commit)."
}

function Invoke-SelectedInternalGuard {
    switch ($InternalGuard) {
        'AuthoredVocabulary' { Invoke-AuthoredVocabularyGuard; break }
        'CleanTree' { Invoke-CleanTreeGuard; break }
        'OneParserSite' { Invoke-OneParserSiteGuard; break }
        default { throw "Unknown internal guard '$InternalGuard'." }
    }
}

function New-CommandSpec {
    param(
        [Parameter(Mandatory)][string] $Id,
        [Parameter(Mandatory)][ValidateSet('Build', 'RepositoryGuard', 'Test')][string] $Kind,
        [Parameter(Mandatory)][string] $Executable,
        [Parameter(Mandatory)][string[]] $ArgumentList,
        [string] $TrxPath
    )

    [pscustomobject][ordered]@{
        Id = $Id
        Kind = $Kind
        Executable = $Executable
        ArgumentList = [string[]]$ArgumentList
        TrxPath = if ([string]::IsNullOrWhiteSpace($TrxPath)) { $null } else { $TrxPath }
    }
}

function New-TestCommandSpec {
    param(
        [Parameter(Mandatory)][string] $Id,
        [Parameter(Mandatory)][string] $Project,
        [string] $Filter,
        [Parameter(Mandatory)][ValidateSet('minimal', 'normal')][string] $Verbosity,
        [Parameter(Mandatory)][string] $ResultRoot
    )

    $laneResultDirectory = "$ResultRoot/$Id"
    $trxRelativePath = "$laneResultDirectory/$Id.trx"
    [string[]] $arguments = @(
        'test',
        $Project,
        '--configuration',
        'Release',
        '--no-build',
        '--no-restore')
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('--filter', $Filter)
    }

    $arguments += @(
        '--verbosity',
        $Verbosity,
        '--logger',
        "trx;LogFileName=$Id.trx",
        '--results-directory',
        $laneResultDirectory)

    New-CommandSpec $Id 'Test' 'dotnet' $arguments $trxRelativePath
}

function Get-CommandSpecs {
    param([Parameter(Mandatory)][string] $ResultRoot)

    $selfRelativePath = ConvertTo-RepositoryRelativePath $scriptPath
    $markdownGuard = 'eng/verify-markdown-links.ps1'
    $headlessGuard = 'eng/verify-headless-workflows.ps1'
    $solutionProjectsGuard = 'eng/verify-solution-projects.ps1'
    $prereleaseGuard = 'eng/verify-prerelease-nondistribution.ps1'
    $prereleasePublisher = 'eng/Publish-PrereleaseArtifacts.ps1'
    $previewDemo = 'eng/Invoke-PreviewDemo.ps1'

    $commands = [System.Collections.Generic.List[object]]::new()
    $commands.Add((New-CommandSpec 'locked-restore' 'Build' 'dotnet' @(
        'restore', 'PhoenixInspect.sln', '--locked-mode', '--verbosity', 'minimal')))
    $commands.Add((New-CommandSpec 'strict-release-build' 'Build' 'dotnet' @(
        'build',
        'PhoenixInspect.sln',
        '--configuration',
        'Release',
        '--no-restore',
        '--verbosity',
        'minimal',
        '--maxcpucount:1',
        '--disable-build-servers',
        '--property:UseSharedCompilation=false',
        '--property:ContinuousIntegrationBuild=true',
        '--property:TreatWarningsAsErrors=true')))

    $testDefinitions = @(
        [ordered]@{ Id = 'unit-complete'; Project = $unitProject; Filter = ''; Verbosity = 'minimal' }
        [ordered]@{ Id = 'integration-non-dump'; Project = $integrationProject; Filter = 'Category!=Dump'; Verbosity = 'minimal' }
        [ordered]@{ Id = 'integration-fast'; Project = $integrationProject; Filter = 'Category=Fast'; Verbosity = 'minimal' }
        [ordered]@{ Id = 'ordinary-dump'; Project = $integrationProject; Filter = 'Category=Dump&Corpus!=ModeledIncidentContextV1'; Verbosity = 'normal' }
        [ordered]@{ Id = 'optimized-context'; Project = $integrationProject; Filter = 'Category=Dump&Corpus=ModeledIncidentContextV1'; Verbosity = 'normal' }
        [ordered]@{ Id = 'focused-v2'; Project = $integrationProject; Filter = 'FullyQualifiedName~PhoenixInspect.IntegrationTests.W8V2'; Verbosity = 'normal' }
        [ordered]@{ Id = 'generated-conformance'; Project = $integrationProject; Filter = 'FullyQualifiedName~PhoenixInspect.IntegrationTests.W8&Corpus!=W8MeaningfulSyntheticV1&Corpus!=W8MeaningfulSyntheticV2&Corpus!=W8MeaningfulSyntheticDecisionCandidate'; Verbosity = 'normal' }
        [ordered]@{ Id = 'w8-portfolio-v1'; Project = $integrationProject; Filter = 'Corpus=W8MeaningfulSyntheticV1'; Verbosity = 'normal' }
        [ordered]@{ Id = 'w8-portfolio-v2'; Project = $integrationProject; Filter = 'Corpus=W8MeaningfulSyntheticV2'; Verbosity = 'normal' }
        [ordered]@{ Id = 'w8-decision-candidate'; Project = $integrationProject; Filter = 'Corpus=W8MeaningfulSyntheticDecisionCandidate'; Verbosity = 'normal' }
        [ordered]@{ Id = 'public-surface'; Project = $integrationProject; Filter = 'FullyQualifiedName~PublicSurface|FullyQualifiedName~public_surface'; Verbosity = 'minimal' }
    )
    $testSelections = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($testDefinition in $testDefinitions) {
        $selectionKey = "$($testDefinition.Project)`n$($testDefinition.Filter)"
        if (-not $testSelections.Add($selectionKey)) {
            throw "Duplicate release-validation test selection for '$($testDefinition.Id)'."
        }

        $commands.Add((New-TestCommandSpec -Id $testDefinition.Id -Project $testDefinition.Project -Filter $testDefinition.Filter -Verbosity $testDefinition.Verbosity -ResultRoot $ResultRoot))
    }

    $commands.Add((New-CommandSpec 'preview-demo' 'Build' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $previewDemo,
        '-OutputDirectory', 'artifacts/preview-demo', '-SkipBuild')))
    $commands.Add((New-CommandSpec 'prerelease-payloads' 'Build' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $prereleasePublisher,
        '-OutputDirectory', 'artifacts/prerelease')))
    $commands.Add((New-CommandSpec 'markdown-links' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $markdownGuard)))
    $commands.Add((New-CommandSpec 'headless-workflows' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $headlessGuard)))
    $commands.Add((New-CommandSpec 'solution-projects' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $solutionProjectsGuard)))
    $commands.Add((New-CommandSpec 'prerelease-nondistribution' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $prereleaseGuard)))
    $commands.Add((New-CommandSpec 'authored-vocabulary' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $selfRelativePath,
        '-InternalGuard', 'AuthoredVocabulary')))
    $commands.Add((New-CommandSpec 'one-parser-site' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $selfRelativePath,
        '-InternalGuard', 'OneParserSite')))
    $commands.Add((New-CommandSpec 'clean-tree' 'RepositoryGuard' 'pwsh' @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $selfRelativePath,
        '-InternalGuard', 'CleanTree')))

    $ids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $trxPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($command in $commands) {
        if (-not $ids.Add($command.Id)) {
            throw "Duplicate release-validation command id '$($command.Id)'."
        }

        if ($command.Kind -eq 'Test') {
            if ([string]::IsNullOrWhiteSpace($command.TrxPath)) {
                throw "Test command '$($command.Id)' has no TRX result path."
            }

            if (-not $trxPaths.Add($command.TrxPath)) {
                throw "Duplicate release-validation TRX result path '$($command.TrxPath)'."
            }
        }
    }

    @($commands)
}

function Assert-ExactStringArray {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Actual,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Expected
    )

    if ($Actual.Count -ne $Expected.Count) {
        throw "Command-plan '$Name' has $($Actual.Count) values instead of $($Expected.Count)."
    }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($Actual[$index] -cne $Expected[$index]) {
            throw "Command-plan '$Name' differs at index ${index}: expected '$($Expected[$index])', observed '$($Actual[$index])'."
        }
    }
}

function Invoke-CommandPlanSelfTest {
    $resultRoot = 'artifacts/w8-local-release-validation/test-results'
    $commands = @(Get-CommandSpecs $resultRoot)
    [string[]] $expectedIds = @(
        'locked-restore',
        'strict-release-build',
        'unit-complete',
        'integration-non-dump',
        'integration-fast',
        'ordinary-dump',
        'optimized-context',
        'focused-v2',
        'generated-conformance',
        'w8-portfolio-v1',
        'w8-portfolio-v2',
        'w8-decision-candidate',
        'public-surface',
        'preview-demo',
        'prerelease-payloads',
        'markdown-links',
        'headless-workflows',
        'solution-projects',
        'prerelease-nondistribution',
        'authored-vocabulary',
        'one-parser-site',
        'clean-tree')
    Assert-ExactStringArray 'ordered command ids' @($commands | ForEach-Object { [string]$_.Id }) $expectedIds

    $byId = @{}
    foreach ($command in $commands) {
        $byId.Add([string]$command.Id, $command)
    }

    $lockedRestore = $byId['locked-restore']
    if ($lockedRestore.Kind -cne 'Build' -or $lockedRestore.Executable -cne 'dotnet') {
        throw 'The locked-restore command must remain a dotnet Build command.'
    }
    Assert-ExactStringArray 'locked restore arguments' $lockedRestore.ArgumentList @(
        'restore', 'PhoenixInspect.sln', '--locked-mode', '--verbosity', 'minimal')

    $strictBuild = $byId['strict-release-build']
    if ($strictBuild.Kind -cne 'Build' -or $strictBuild.Executable -cne 'dotnet') {
        throw 'The strict-release-build command must remain a dotnet Build command.'
    }
    Assert-ExactStringArray 'strict Release build arguments' $strictBuild.ArgumentList @(
        'build',
        'PhoenixInspect.sln',
        '--configuration',
        'Release',
        '--no-restore',
        '--verbosity',
        'minimal',
        '--maxcpucount:1',
        '--disable-build-servers',
        '--property:UseSharedCompilation=false',
        '--property:ContinuousIntegrationBuild=true',
        '--property:TreatWarningsAsErrors=true')

    $testContracts = @(
        [pscustomobject]@{ Id = 'unit-complete'; Project = 'tests/PhoenixInspect.Tests/PhoenixInspect.Tests.csproj'; Filter = ''; Verbosity = 'minimal' },
        [pscustomobject]@{ Id = 'integration-non-dump'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Category!=Dump'; Verbosity = 'minimal' },
        [pscustomobject]@{ Id = 'integration-fast'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Category=Fast'; Verbosity = 'minimal' },
        [pscustomobject]@{ Id = 'ordinary-dump'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Category=Dump&Corpus!=ModeledIncidentContextV1'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'optimized-context'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Category=Dump&Corpus=ModeledIncidentContextV1'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'focused-v2'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'FullyQualifiedName~PhoenixInspect.IntegrationTests.W8V2'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'generated-conformance'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'FullyQualifiedName~PhoenixInspect.IntegrationTests.W8&Corpus!=W8MeaningfulSyntheticV1&Corpus!=W8MeaningfulSyntheticV2&Corpus!=W8MeaningfulSyntheticDecisionCandidate'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'w8-portfolio-v1'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Corpus=W8MeaningfulSyntheticV1'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'w8-portfolio-v2'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Corpus=W8MeaningfulSyntheticV2'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'w8-decision-candidate'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'Corpus=W8MeaningfulSyntheticDecisionCandidate'; Verbosity = 'normal' },
        [pscustomobject]@{ Id = 'public-surface'; Project = 'tests/PhoenixInspect.IntegrationTests/PhoenixInspect.IntegrationTests.csproj'; Filter = 'FullyQualifiedName~PublicSurface|FullyQualifiedName~public_surface'; Verbosity = 'minimal' })
    foreach ($contract in $testContracts) {
        $command = $byId[$contract.Id]
        if ($command.Kind -cne 'Test' -or $command.Executable -cne 'dotnet') {
            throw "The '$($contract.Id)' command must remain a dotnet Test command."
        }

        [string[]] $expectedArguments = @(
            'test',
            $contract.Project,
            '--configuration',
            'Release',
            '--no-build',
            '--no-restore')
        if (-not [string]::IsNullOrWhiteSpace($contract.Filter)) {
            $expectedArguments += @('--filter', $contract.Filter)
        }
        $laneDirectory = "$resultRoot/$($contract.Id)"
        $expectedArguments += @(
            '--verbosity',
            $contract.Verbosity,
            '--logger',
            "trx;LogFileName=$($contract.Id).trx",
            '--results-directory',
            $laneDirectory)
        Assert-ExactStringArray "$($contract.Id) arguments" $command.ArgumentList $expectedArguments
        if ($command.TrxPath -cne "$laneDirectory/$($contract.Id).trx") {
            throw "The '$($contract.Id)' command has the wrong isolated TRX path."
        }
    }

    $preview = $byId['preview-demo']
    if ($preview.Kind -cne 'Build' -or $preview.Executable -cne 'pwsh') {
        throw 'The preview-demo command must remain a pwsh Build command.'
    }
    Assert-ExactStringArray 'preview demo arguments' $preview.ArgumentList @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', 'eng/Invoke-PreviewDemo.ps1',
        '-OutputDirectory', 'artifacts/preview-demo', '-SkipBuild')

    $prerelease = $byId['prerelease-payloads']
    if ($prerelease.Kind -cne 'Build' -or $prerelease.Executable -cne 'pwsh') {
        throw 'The prerelease-payloads command must remain a pwsh Build command.'
    }
    Assert-ExactStringArray 'prerelease payload arguments' $prerelease.ArgumentList @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', 'eng/Publish-PrereleaseArtifacts.ps1',
        '-OutputDirectory', 'artifacts/prerelease')

    $guardScripts = [ordered]@{
        'markdown-links' = 'eng/verify-markdown-links.ps1'
        'headless-workflows' = 'eng/verify-headless-workflows.ps1'
        'solution-projects' = 'eng/verify-solution-projects.ps1'
        'prerelease-nondistribution' = 'eng/verify-prerelease-nondistribution.ps1'
    }
    foreach ($guardId in $guardScripts.Keys) {
        $guard = $byId[$guardId]
        if ($guard.Kind -cne 'RepositoryGuard' -or $guard.Executable -cne 'pwsh') {
            throw "The '$guardId' command must remain a pwsh RepositoryGuard command."
        }
        Assert-ExactStringArray "$guardId arguments" $guard.ArgumentList @(
            '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $guardScripts[$guardId])
    }

    $internalGuards = [ordered]@{
        'authored-vocabulary' = 'AuthoredVocabulary'
        'one-parser-site' = 'OneParserSite'
        'clean-tree' = 'CleanTree'
    }
    foreach ($guardId in $internalGuards.Keys) {
        $guard = $byId[$guardId]
        if ($guard.Kind -cne 'RepositoryGuard' -or $guard.Executable -cne 'pwsh') {
            throw "The '$guardId' command must remain a pwsh RepositoryGuard command."
        }
        Assert-ExactStringArray "$guardId arguments" $guard.ArgumentList @(
            '-NoLogo', '-NoProfile', '-NonInteractive', '-File',
            'eng/Invoke-W8LocalReleaseValidation.ps1', '-InternalGuard', $internalGuards[$guardId])
    }

    foreach ($command in @($commands | Where-Object { $_.Kind -cne 'Test' })) {
        if ($null -ne $command.TrxPath) {
            throw "The non-test command '$($command.Id)' must not claim a TRX result path."
        }
    }

    [ordered]@{
        plan = 'W8.10-local-v1'
        commandCount = $commands.Count
        testLaneCount = $testContracts.Count
        localOnly = $true
        hostedEvidence = 'NotRun'
        closureClaim = $false
        status = 'Passed'
    }
}

function Format-Command {
    param([Parameter(Mandatory)] $Command)

    $tokens = @('eng/Invoke-HeadlessProcess.ps1', $Command.Executable) + $Command.ArgumentList
    ($tokens | ForEach-Object {
        $token = [string]$_
        if ($token -notmatch '^[A-Za-z0-9_./:=~!+\-]+$') {
            '"' + $token.Replace('"', '\"') + '"'
        }
        else {
            $token
        }
    }) -join ' '
}

function Invoke-CommandSpec {
    param(
        [Parameter(Mandatory)] $Command,
        [Parameter(Mandatory)][string] $PowerShellPath
    )

    Write-Output ''
    Write-Output "[$($Command.Id)] $(Format-Command $Command)"
    [string[]] $hostArgumentList = @(
        '-NoLogo',
        '-NoProfile',
        '-NonInteractive',
        '-File',
        $headlessWrapperPath,
        $Command.Executable) + @($Command.ArgumentList)
    & $PowerShellPath @hostArgumentList
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) {
        throw "Command '$($Command.Id)' did not report an exit code."
    }

    [int]$exitCode
}

function New-CommandEvidence {
    param([Parameter(Mandatory)] $Command)

    [ordered]@{
        id = $Command.Id
        kind = $Command.Kind
        command = [ordered]@{
            wrapper = 'eng/Invoke-HeadlessProcess.ps1'
            executable = $Command.Executable
            arguments = [string[]]$Command.ArgumentList
            display = Format-Command $Command
        }
        status = 'NotRun'
        exitCode = $null
        testCounts = $null
        trx = if ($null -eq $Command.TrxPath) { $null } else { $Command.TrxPath }
        validationError = $null
    }
}

if ($PSCmdlet.ParameterSetName -eq 'InternalGuard') {
    Push-Location $repositoryRoot
    try {
        Invoke-SelectedInternalGuard
    }
    finally {
        Pop-Location
    }

    exit 0
}

if ($PSCmdlet.ParameterSetName -eq 'ValidateTrx') {
    $trxPath = if ([System.IO.Path]::IsPathRooted($ValidateTrxPath)) {
        [System.IO.Path]::GetFullPath($ValidateTrxPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ValidateTrxPath))
    }

    Read-TrxEvidenceFile $trxPath | ConvertTo-Json -Depth 4
    exit 0
}

if ($SelfTest) {
    Invoke-ValidatorSelfTest
    exit 0
}

$outputCandidate = if ([System.IO.Path]::IsPathFullyQualified($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repositoryRoot $OutputDirectory
}
$defaultOutputPath = Assert-PathIsInsideRepository $outputCandidate
$defaultResultRoot = "$(ConvertTo-RepositoryRelativePath $defaultOutputPath)/test-results"
$commandSpecs = @(Get-CommandSpecs $defaultResultRoot)

if ($List) {
    [ordered]@{
        schema = 'phoenixinspect.w8-local-release-validation/plan-v1'
        mode = 'ListOnly'
        localOnly = $true
        hostedEvidence = 'NotRun'
        closureClaim = $false
        focusedLaneOverlap = 'Intentional'
        requiredInputs = @(Get-RequiredReleaseInputSpecs | ForEach-Object {
            [ordered]@{
                role = $_.Role
                path = $_.Path
                requirement = $_.Requirement
                sha256 = $_.Sha256
            }
        })
        commandCount = $commandSpecs.Count
        commands = @($commandSpecs | ForEach-Object {
            [ordered]@{
                id = $_.Id
                kind = $_.Kind
                command = Format-Command $_
                trx = $_.TrxPath
            }
        })
    } | ConvertTo-Json -Depth 8
    exit 0
}

$outputPath = $defaultOutputPath
$lockAlgorithm = [System.Security.Cryptography.SHA256]::Create()
try {
    # All suffix variants share bin/obj plus the preview-demo and prerelease output roots, so serialize by checkout.
    $lockBytes = [System.Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToUpperInvariant())
    $lockHash = ([System.BitConverter]::ToString($lockAlgorithm.ComputeHash($lockBytes))).Replace('-', '')
}
finally {
    $lockAlgorithm.Dispose()
}
$outputMutex = [System.Threading.Mutex]::new($false, "Local\PhoenixInspect.W8Validation.$lockHash")
$outputMutexHeld = $false

try {
    try {
        $outputMutexHeld = $outputMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $outputMutexHeld = $true
    }
    if (-not $outputMutexHeld) {
        throw "Another W8 local release validator is already targeting '$outputPath'."
    }

    $initialState = Get-RepositoryState
    if ($initialState.isDirty) {
        throw "The repository must be clean before validation starts ($($initialState.entryCount) status entries)."
    }

    $requiredInputEvidence = @(Assert-RequiredReleaseInputs)

    if (Test-Path -LiteralPath $outputPath) {
        $existingEntries = @(Get-ChildItem -LiteralPath $outputPath -Force)
        if ($existingEntries.Count -ne 0 -and -not $Force) {
            throw "Output directory '$outputPath' is not empty. Pass -Force to replace prior validator-owned output."
        }

        if ($existingEntries.Count -ne 0) {
            Assert-ValidatorOwnedOutput $outputPath
            [System.IO.Directory]::Delete($outputPath, $true)
        }
    }

    $null = New-Item -ItemType Directory -Path $outputPath -Force
    $resultRootPath = Join-Path $outputPath 'test-results'
    $null = New-Item -ItemType Directory -Path $resultRootPath -Force

    $finalState = $null
    $overallStatus = 'Failed'
    $failureMessage = $null
    $commandEvidence = [System.Collections.Generic.List[object]]::new()
    foreach ($command in $commandSpecs) {
        $commandEvidence.Add((New-CommandEvidence $command))
    }
    $powerShellPath = (Get-Process -Id $PID).Path
    $previousCi = $env:CI
    $env:CI = 'true'

    Push-Location $repositoryRoot
    try {

    for ($commandIndex = 0; $commandIndex -lt $commandSpecs.Count; $commandIndex++) {
        $command = $commandSpecs[$commandIndex]
        $evidence = $commandEvidence[$commandIndex]

        try {
            if ($command.Kind -eq 'Test') {
                $trxFullPath = Join-Path $repositoryRoot $command.TrxPath
                $laneDirectory = Split-Path -Parent $trxFullPath
                if (Test-Path -LiteralPath $laneDirectory) {
                    $existingLaneEntries = @(Get-ChildItem -LiteralPath $laneDirectory -Force)
                    if ($existingLaneEntries.Count -ne 0) {
                        throw "Test result directory is unexpectedly non-empty: '$laneDirectory'."
                    }
                }
                else {
                    $null = New-Item -ItemType Directory -Path $laneDirectory -Force
                }
            }

            $evidence.exitCode = Invoke-CommandSpec $command $powerShellPath
            if ($evidence.exitCode -ne 0) {
                throw "Command '$($command.Id)' failed with exit code $($evidence.exitCode)."
            }

            if ($command.Kind -eq 'Test') {
                $trxFullPath = Join-Path $repositoryRoot $command.TrxPath
                $trxFiles = @(Get-ChildItem -LiteralPath (Split-Path -Parent $trxFullPath) -File -Filter '*.trx')
                if ($trxFiles.Count -ne 1 -or
                    -not $trxFiles[0].FullName.Equals($trxFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Test lane '$($command.Id)' produced $($trxFiles.Count) TRX files instead of its one expected result."
                }

                $evidence.testCounts = Read-TrxEvidenceFile $trxFullPath
            }

            $evidence.status = 'Passed'
        }
        catch {
            $evidence.status = 'Failed'
            $evidence.validationError = $_.Exception.Message
            throw
        }
    }

    $finalState = Get-RepositoryState
    if ($initialState.commit -cne $finalState.commit) {
        throw "HEAD changed during validation: $($initialState.commit) -> $($finalState.commit)."
    }

    if ($finalState.isDirty) {
        throw 'The final repository state is dirty despite the clean-tree command passing.'
    }

    $overallStatus = 'Passed'
    }
    catch {
        $failureMessage = $_.Exception.Message
    }
    finally {
    try {
        if ($null -eq $finalState) {
            $finalState = Get-RepositoryState
        }
    }
    catch {
        $finalState = [ordered]@{
            commit = $null
            isDirty = $null
            entryCount = $null
            entries = @()
            readError = $_.Exception.Message
        }
        if ($null -eq $failureMessage) {
            $failureMessage = $_.Exception.Message
        }
    }

    if ($null -eq $previousCi) {
        Remove-Item Env:CI -ErrorAction SilentlyContinue
    }
    else {
        $env:CI = $previousCi
    }

    Pop-Location

    $testCommands = @($commandEvidence | Where-Object { $_.kind -eq 'Test' })
    $passedTestCommands = @($testCommands | Where-Object { $_.status -eq 'Passed' })
    $testExecutionCount = 0
    foreach ($testCommand in $passedTestCommands) {
        $testExecutionCount += $testCommand.testCounts.total
    }

    $summary = [ordered]@{
        schema = 'phoenixinspect.w8-local-release-validation/evidence-v1'
        scope = [ordered]@{
            kind = 'LocalOnly'
            hostedEvidence = 'NotRun'
            closureClaim = $false
            focusedLaneOverlap = 'Intentional'
        }
        status = $overallStatus
        failure = $failureMessage
        requiredInputs = @($requiredInputEvidence)
        repository = [ordered]@{
            commit = if ($null -eq $initialState) { $null } else { $initialState.commit }
            initialDirtyState = $initialState
            finalDirtyState = $finalState
        }
        counts = [ordered]@{
            plannedCommands = $commandSpecs.Count
            executedCommands = @($commandEvidence | Where-Object { $_.status -ne 'NotRun' }).Count
            passedCommands = @($commandEvidence | Where-Object { $_.status -eq 'Passed' }).Count
            failedCommands = @($commandEvidence | Where-Object { $_.status -eq 'Failed' }).Count
            notRunCommands = @($commandEvidence | Where-Object { $_.status -eq 'NotRun' }).Count
            passedTestLanes = $passedTestCommands.Count
            testExecutions = $testExecutionCount
            skippedTests = if ($passedTestCommands.Count -eq $testCommands.Count) { 0 } else { $null }
        }
        commands = @($commandEvidence)
    }

    $summaryPath = Join-Path $outputPath 'evidence.json'
    $json = $summary | ConvertTo-Json -Depth 16
    [System.IO.File]::WriteAllText(
        $summaryPath,
        $json + "`n",
        [System.Text.UTF8Encoding]::new($false))
    Write-Output "Local-only evidence: $(ConvertTo-RepositoryRelativePath $summaryPath)"
    }

    if ($overallStatus -ne 'Passed') {
        throw "W8 local release validation failed: $failureMessage"
    }

    Write-Output "W8 local release validation passed at $($initialState.commit). No hosted evidence or closure is claimed."
}
finally {
    if ($outputMutexHeld) {
        $outputMutex.ReleaseMutex()
        $outputMutexHeld = $false
    }
    $outputMutex.Dispose()
}
