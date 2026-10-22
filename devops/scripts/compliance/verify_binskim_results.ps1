[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportRoot
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ReportRoot -PathType Container))
{
    throw "BinSkim report directory is missing: $ReportRoot. A successful task without scan results is not sufficient."
}
$reports = @(Get-ChildItem -LiteralPath $ReportRoot -Recurse -File -Filter '*.sarif')
if ($reports.Count -eq 0)
{
    throw "No BinSkim SARIF reports found under $ReportRoot."
}
Write-Host "Found $($reports.Count) BinSkim SARIF report file(s). Analysis and enforcement are handled by Guardian/PostAnalysis."
