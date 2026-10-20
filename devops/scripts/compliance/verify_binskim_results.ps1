[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportRoot
)

$ErrorActionPreference = 'Stop'

function Get-NotificationMessage
{
    param($Notification)

    $text = [string]$Notification.message.text
    if ($Notification.message.arguments)
    {
        $text = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, $text, [object[]]$Notification.message.arguments)
    }
    return ($text -replace '[\r\n]+', ' ')
}

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
            foreach ($notification in @($invocation.toolExecutionNotifications) + @($invocation.toolConfigurationNotifications))
            {
                if ($null -eq $notification)
                {
                    continue
                }
                $message = Get-NotificationMessage $notification
                if (-not $notification.level -or $notification.level -in @('warning', 'error') -or $notification.descriptor.id -like 'ERR*')
                {
                    $failures.Add("BinSkim diagnostic $($notification.descriptor.id): $message")
                }
            }

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
        foreach ($result in $run.results)
        {
            if ($result.kind -in @('pass', 'notApplicable'))
            {
                continue
            }
            if ($result.level -eq 'error' -or $result.ruleId -like 'ERR*')
            {
                $failures.Add("BinSkim result $($result.ruleId): $(Get-NotificationMessage $result)")
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
    throw "BinSkim verification failed with $($failures.Count) unexpected execution, analysis, or security errors."
}
Write-Host "Verified $($reports.Count) BinSkim SARIF report(s)."
