param(
    [Parameter(Mandatory)]
    [string] $GeneratedPath,
    [string] $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')),
    [switch] $UpdateRootNotice
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Normalize-Notice {
    param([string] $Content)

    (($Content -replace "`r`n", "`n" -replace '(?m)[ \t]+$', '').TrimEnd("`n")) + "`n"
}

if (-not (Test-Path -LiteralPath $GeneratedPath -PathType Leaf)) {
    throw "Official CG NOTICE was not generated: $GeneratedPath"
}

$generated = [System.IO.File]::ReadAllText($GeneratedPath)
if ([string]::IsNullOrWhiteSpace($generated)) {
    throw "Official CG NOTICE is empty: $GeneratedPath"
}

$rootNotice = Join-Path $RepoRoot 'NOTICE'
if (-not (Test-Path -LiteralPath $rootNotice -PathType Leaf)) {
    throw "Committed root NOTICE was not found: $rootNotice"
}

$committed = [System.IO.File]::ReadAllText($rootNotice)
if ([string]::IsNullOrWhiteSpace($committed)) {
    throw "Committed root NOTICE is empty: $rootNotice"
}

$normalized = Normalize-Notice $generated
if ($normalized -ceq (Normalize-Notice $committed)) {
    Write-Host 'Official CG NOTICE matches the committed root NOTICE.'
}
elseif ($UpdateRootNotice) {
    [System.IO.File]::WriteAllText($rootNotice, $normalized, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Updated root NOTICE from the official CG output for the stable-release PR.'
}
else {
    Write-Host '##vso[task.logissue type=warning]Official CG NOTICE differs from the committed root NOTICE. Review NOTICE.generated; the stable-release PR refreshes NOTICE. Packaging continues to use the committed file.'
}
