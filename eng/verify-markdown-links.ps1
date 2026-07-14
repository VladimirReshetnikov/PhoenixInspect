#Requires -Version 7.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$inlineCodeSpanPattern = [regex]::new('`+[^`]*`+', [System.Text.RegularExpressions.RegexOptions]::Compiled)

function Test-IsEscaped {
    param(
        [Parameter(Mandatory)]
        [string] $Text,

        [Parameter(Mandatory)]
        [int] $Index
    )

    $backslashCount = 0
    for ($position = $Index - 1; $position -ge 0 -and $Text[$position] -eq '\'; $position--) {
        $backslashCount++
    }

    return ($backslashCount % 2) -eq 1
}

function ConvertFrom-MarkdownEscapes {
    param(
        [Parameter(Mandatory)]
        [string] $Text
    )

    $result = [System.Text.StringBuilder]::new($Text.Length)
    for ($index = 0; $index -lt $Text.Length; $index++) {
        $current = $Text[$index]
        if ($current -eq '\' -and $index + 1 -lt $Text.Length) {
            $next = $Text[$index + 1]
            $codePoint = [int] $next
            $isAsciiPunctuation =
                ($codePoint -ge 0x21 -and $codePoint -le 0x2f) -or
                ($codePoint -ge 0x3a -and $codePoint -le 0x40) -or
                ($codePoint -ge 0x5b -and $codePoint -le 0x60) -or
                ($codePoint -ge 0x7b -and $codePoint -le 0x7e)

            if ($isAsciiPunctuation) {
                [void] $result.Append($next)
                $index++
                continue
            }
        }

        [void] $result.Append($current)
    }

    return $result.ToString()
}

function Hide-InlineCodeSpans {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Text
    )

    return $inlineCodeSpanPattern.Replace(
        $Text,
        { param($match) return ' ' * $match.Length })
}

function Get-MarkdownContentLines {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $contentLines = [System.Collections.Generic.List[object]]::new()
    $fenceCharacter = [char] 0
    $fenceLength = 0
    $inFence = $false
    $lineNumber = 0

    foreach ($line in @(Get-Content -LiteralPath $Path)) {
        $lineNumber++
        $fenceMatch = [regex]::Match($line, '^ {0,3}(?<fence>`{3,}|~{3,})')

        if ($inFence) {
            if ($fenceMatch.Success) {
                $fence = $fenceMatch.Groups['fence'].Value
                $remainder = $line.Substring($fenceMatch.Index + $fenceMatch.Length)
                if ($fence[0] -eq $fenceCharacter -and $fence.Length -ge $fenceLength -and [string]::IsNullOrWhiteSpace($remainder)) {
                    $inFence = $false
                    $fenceCharacter = [char] 0
                    $fenceLength = 0
                }
            }

            continue
        }

        if ($fenceMatch.Success) {
            $fence = $fenceMatch.Groups['fence'].Value
            $inFence = $true
            $fenceCharacter = $fence[0]
            $fenceLength = $fence.Length
            continue
        }

        $scannableText = $line
        if ($line.Contains('[')) {
            $scannableText = Hide-InlineCodeSpans -Text $line
        }

        $contentLines.Add([pscustomobject]@{
            Number = $lineNumber
            Text = $scannableText
        })
    }

    return $contentLines
}

function Get-InlineLinkDestinations {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Text
    )

    $destinations = [System.Collections.Generic.List[object]]::new()
    $index = 0
    while ($index -lt $Text.Length) {
        $index = $Text.IndexOf('[', $index)
        if ($index -lt 0) {
            break
        }

        if (Test-IsEscaped -Text $Text -Index $index) {
            $index++
            continue
        }

        $labelDepth = 1
        $labelEnd = -1
        for ($cursor = $index + 1; $cursor -lt $Text.Length; $cursor++) {
            if (Test-IsEscaped -Text $Text -Index $cursor) {
                continue
            }

            if ($Text[$cursor] -eq '[') {
                $labelDepth++
            }
            elseif ($Text[$cursor] -eq ']') {
                $labelDepth--
                if ($labelDepth -eq 0) {
                    $labelEnd = $cursor
                    break
                }
            }
        }

        if ($labelEnd -lt 0 -or $labelEnd + 1 -ge $Text.Length -or $Text[$labelEnd + 1] -ne '(') {
            $index++
            continue
        }

        $cursor = $labelEnd + 2
        while ($cursor -lt $Text.Length -and [char]::IsWhiteSpace($Text[$cursor])) {
            $cursor++
        }

        $destinationStart = $cursor
        if ($cursor -lt $Text.Length -and $Text[$cursor] -eq '<') {
            $destinationStart++
            $cursor++
            while ($cursor -lt $Text.Length -and ($Text[$cursor] -ne '>' -or (Test-IsEscaped -Text $Text -Index $cursor))) {
                $cursor++
            }

            if ($cursor -ge $Text.Length) {
                $index = $labelEnd + 1
                continue
            }

            $destination = $Text.Substring($destinationStart, $cursor - $destinationStart)
        }
        else {
            $parenthesisDepth = 0
            while ($cursor -lt $Text.Length) {
                if (Test-IsEscaped -Text $Text -Index $cursor) {
                    $cursor++
                    continue
                }

                $current = $Text[$cursor]
                if ([char]::IsWhiteSpace($current) -and $parenthesisDepth -eq 0) {
                    break
                }

                if ($current -eq '(') {
                    $parenthesisDepth++
                }
                elseif ($current -eq ')') {
                    if ($parenthesisDepth -eq 0) {
                        break
                    }

                    $parenthesisDepth--
                }

                $cursor++
            }

            $destination = $Text.Substring($destinationStart, $cursor - $destinationStart)
        }

        $destinations.Add([pscustomobject]@{
            Destination = $destination
            Column = $destinationStart + 1
        })

        $index = [Math]::Max($labelEnd + 1, $cursor)
    }

    return $destinations
}

function Get-ReferenceDefinition {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Text
    )

    $match = [regex]::Match($Text, '^ {0,3}\[(?<label>(?:\\.|[^\]])+)\]:[ \t]*(?<remainder>.*)$')
    if (-not $match.Success) {
        return $null
    }

    $remainder = $match.Groups['remainder'].Value
    if ([string]::IsNullOrWhiteSpace($remainder)) {
        return $null
    }

    $cursor = 0
    while ($cursor -lt $remainder.Length -and [char]::IsWhiteSpace($remainder[$cursor])) {
        $cursor++
    }

    if ($cursor -ge $remainder.Length) {
        return $null
    }

    if ($remainder[$cursor] -eq '<') {
        $destinationStart = ++$cursor
        while ($cursor -lt $remainder.Length -and ($remainder[$cursor] -ne '>' -or (Test-IsEscaped -Text $remainder -Index $cursor))) {
            $cursor++
        }

        if ($cursor -ge $remainder.Length) {
            return $null
        }
    }
    else {
        $destinationStart = $cursor
        while ($cursor -lt $remainder.Length -and -not [char]::IsWhiteSpace($remainder[$cursor])) {
            $cursor++
        }
    }

    return [pscustomobject]@{
        Label = ConvertFrom-MarkdownEscapes -Text $match.Groups['label'].Value
        Destination = $remainder.Substring($destinationStart, $cursor - $destinationStart)
    }
}

function Normalize-ReferenceLabel {
    param(
        [Parameter(Mandatory)]
        [string] $Label
    )

    return [regex]::Replace((ConvertFrom-MarkdownEscapes -Text $Label).Trim(), '\s+', ' ')
}

function Test-LocalDestination {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Destination,

        [Parameter(Mandatory)]
        [string] $SourceDirectory
    )

    $trimmed = $Destination.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#') -or $trimmed.StartsWith('?')) {
        return [pscustomobject]@{ IsLocal = $false; IsValid = $true; Reason = '' }
    }

    $isWindowsAbsolutePath = $trimmed -match '^[A-Za-z]:[\\/]'
    $hasUriScheme = $trimmed -match '^[A-Za-z][A-Za-z0-9+.-]*:'
    if ($trimmed.StartsWith('//') -or ($hasUriScheme -and -not $isWindowsAbsolutePath)) {
        return [pscustomobject]@{ IsLocal = $false; IsValid = $true; Reason = '' }
    }

    $pathPart = $trimmed -replace '[?#].*$', ''
    $pathPart = ConvertFrom-MarkdownEscapes -Text $pathPart
    try {
        $pathPart = [System.Uri]::UnescapeDataString($pathPart)
    }
    catch {
        return [pscustomobject]@{
            IsLocal = $true
            IsValid = $false
            Reason = 'has invalid percent-encoding'
        }
    }

    if ([System.IO.Path]::IsPathRooted($pathPart)) {
        return [pscustomobject]@{
            IsLocal = $true
            IsValid = $false
            Reason = 'is an absolute path; repository links must be relative'
        }
    }

    try {
        $resolvedPath = [System.IO.Path]::GetFullPath($pathPart, $SourceDirectory)
    }
    catch {
        return [pscustomobject]@{
            IsLocal = $true
            IsValid = $false
            Reason = "cannot be resolved as a filesystem path: $($_.Exception.Message)"
        }
    }

    $repositoryRelativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $resolvedPath)
    $escapesRepository =
        $repositoryRelativePath -eq '..' -or
        $repositoryRelativePath.StartsWith('../', [System.StringComparison]::Ordinal) -or
        $repositoryRelativePath.StartsWith('..\', [System.StringComparison]::Ordinal) -or
        [System.IO.Path]::IsPathRooted($repositoryRelativePath)

    if ($escapesRepository) {
        return [pscustomobject]@{
            IsLocal = $true
            IsValid = $false
            Reason = 'resolves outside the repository'
        }
    }

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        $displayPath = $repositoryRelativePath.Replace('\', '/')
        return [pscustomobject]@{
            IsLocal = $true
            IsValid = $false
            Reason = "resolves to missing repository path '$displayPath'"
        }
    }

    return [pscustomobject]@{ IsLocal = $true; IsValid = $true; Reason = '' }
}

$markdownFiles = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $repositoryRoot -File -Filter '*.md') {
    $markdownFiles.Add($file.FullName)
}

$documentationRoot = Join-Path $repositoryRoot 'docs'
if (Test-Path -LiteralPath $documentationRoot -PathType Container) {
    foreach ($file in Get-ChildItem -LiteralPath $documentationRoot -File -Filter '*.md' -Recurse) {
        $markdownFiles.Add($file.FullName)
    }
}

$markdownFiles.Sort([System.StringComparer]::Ordinal)
$failures = [System.Collections.Generic.List[string]]::new()
$localDestinationCount = 0
$referenceUsePattern = [regex]::new('!?(?<open>\[)(?<text>(?:\\.|[^\]])+)\]\[(?<label>(?:\\.|[^\]])*)\]')

foreach ($markdownFile in $markdownFiles) {
    $displayFile = [System.IO.Path]::GetRelativePath($repositoryRoot, $markdownFile).Replace('\', '/')
    $sourceDirectory = Split-Path -Parent $markdownFile
    $contentLines = @(Get-MarkdownContentLines -Path $markdownFile)
    $definitions = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $definitionsByLine = [System.Collections.Generic.Dictionary[int, object]]::new()

    foreach ($contentLine in $contentLines) {
        $definition = Get-ReferenceDefinition -Text $contentLine.Text
        if ($null -eq $definition) {
            continue
        }

        $normalizedLabel = Normalize-ReferenceLabel -Label $definition.Label
        if (-not $definitions.ContainsKey($normalizedLabel)) {
            $definitions.Add($normalizedLabel, $definition)
        }

        $definitionsByLine[$contentLine.Number] = $definition
    }

    foreach ($contentLine in $contentLines) {
        $definition = $null
        $isDefinitionLine = $definitionsByLine.TryGetValue($contentLine.Number, [ref] $definition)
        if ($isDefinitionLine) {
            $result = Test-LocalDestination -Destination $definition.Destination -SourceDirectory $sourceDirectory
            if ($result.IsLocal) {
                $localDestinationCount++
                if (-not $result.IsValid) {
                    $failures.Add("${displayFile}:$($contentLine.Number): error: reference destination '$($definition.Destination)' $($result.Reason)")
                }
            }
        }

        foreach ($link in Get-InlineLinkDestinations -Text $contentLine.Text) {
            $result = Test-LocalDestination -Destination $link.Destination -SourceDirectory $sourceDirectory
            if ($result.IsLocal) {
                $localDestinationCount++
                if (-not $result.IsValid) {
                    $failures.Add("${displayFile}:$($contentLine.Number):$($link.Column): error: inline destination '$($link.Destination)' $($result.Reason)")
                }
            }
        }

        if ($isDefinitionLine) {
            continue
        }

        foreach ($referenceUse in $referenceUsePattern.Matches($contentLine.Text)) {
            $openBracket = $referenceUse.Groups['open'].Index
            if (Test-IsEscaped -Text $contentLine.Text -Index $openBracket) {
                continue
            }

            $label = $referenceUse.Groups['label'].Value
            if ($label.Length -eq 0) {
                $label = $referenceUse.Groups['text'].Value
            }

            $normalizedLabel = Normalize-ReferenceLabel -Label $label
            if (-not $definitions.ContainsKey($normalizedLabel)) {
                $column = $openBracket + 1
                $failures.Add("${displayFile}:$($contentLine.Number):${column}: error: Markdown reference '$label' has no definition in this file")
            }
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Output "Markdown link verification failed with $($failures.Count) error(s):"
    foreach ($failure in $failures) {
        Write-Output $failure
    }

    exit 1
}

Write-Output "Markdown link verification passed: $($markdownFiles.Count) file(s), $localDestinationCount local destination(s)."
