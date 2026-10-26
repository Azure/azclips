[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportRoot
)

$ErrorActionPreference = 'Stop'

function Report-Warning([string]$Message)
{
    Write-Host "##vso[task.logissue type=warning]$Message"
}

if (-not (Test-Path -LiteralPath $ReportRoot -PathType Container))
{
    Report-Warning "BinSkim report directory is missing: $ReportRoot. A successful task without scan results is not sufficient."
    return
}
$reports = @(Get-ChildItem -LiteralPath $ReportRoot -Recurse -File -Filter '*.sarif')
if ($reports.Count -eq 0)
{
    Report-Warning "No BinSkim SARIF reports found under $ReportRoot."
    return
}
Write-Host "Found $($reports.Count) BinSkim SARIF report file(s). Analysis and enforcement are handled by Guardian/PostAnalysis."
