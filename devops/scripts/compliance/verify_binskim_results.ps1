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

# Advisory only: BinSkim runs with load/analysis errors ignored, so surface problems as warnings without failing the job.
$parsed = $false
foreach ($report in $reports)
{
    try
    {
        $sarif = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json -Depth 100
    }
    catch
    {
        Report-Warning "BinSkim report could not be parsed: $($report.FullName). $($_.Exception.Message)"
        continue
    }
    $parsed = $true

    foreach ($run in @($sarif.runs))
    {
        foreach ($invocation in @($run.invocations))
        {
            if ($invocation.executionSuccessful -eq $false)
            {
                Report-Warning "BinSkim invocation reported executionSuccessful=false in $($report.Name) (exit code $($invocation.exitCode))."
            }
            $notifications = @($invocation.toolExecutionNotifications) + @($invocation.toolConfigurationNotifications)
            foreach ($notification in @($notifications | Where-Object { $_ -and $_.level -eq 'error' }))
            {
                Report-Warning "BinSkim error notification in $($report.Name): $($notification.associatedRule.id) $($notification.message.text)"
            }
        }
    }
}
