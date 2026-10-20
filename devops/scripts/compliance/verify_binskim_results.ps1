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
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($report in $reports)
{
    $sarif = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
    if ($sarif.version -ne '2.1.0' -or -not $sarif.runs)
    {
        throw "Missing runs or unsupported SARIF version in $($report.FullName)."
    }
    foreach ($run in $sarif.runs)
    {
        if ($run.tool.driver.name -notmatch 'BinSkim' -or -not $run.invocations)
        {
            throw "Report lacks a BinSkim invocation: $($report.FullName)."
        }
        foreach ($invocation in $run.invocations)
        {
            $processFailed = ($null -ne $invocation.exitCode -and $invocation.exitCode -ne 0) -or
                ($null -ne $invocation.exitSignalNumber -and $invocation.exitSignalNumber -ne 0) -or
                -not [string]::IsNullOrEmpty([string]$invocation.exitSignalName)
            $executionSucceeded = $invocation.executionSuccessful -is [bool] -and $invocation.executionSuccessful
            if ($processFailed -or -not $executionSucceeded)
            {
                $status = if ($null -eq $invocation.executionSuccessful) { '<missing>' } else { [string]$invocation.executionSuccessful }
                $exitCode = if ($null -eq $invocation.exitCode) { '<missing>' } else { [string]$invocation.exitCode }
                $failures.Add("BinSkim execution did not succeed in $($report.FullName) (executionSuccessful=$status; exitCode=$exitCode; exitSignalName=$($invocation.exitSignalName); exitSignalNumber=$($invocation.exitSignalNumber)).")
            }
        }
    }
}

if ($failures.Count -gt 0)
{
    foreach ($failure in $failures)
    {
        Write-Host "##vso[task.logissue type=error]$failure"
    }
    throw "BinSkim report-health verification failed with $($failures.Count) unsuccessful invocation(s)."
}
Write-Host "Verified execution health for $($reports.Count) BinSkim SARIF report(s). Finding enforcement is delegated to PostAnalysis."
