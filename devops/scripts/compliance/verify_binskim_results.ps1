[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportRoot,

    [Parameter(Mandatory = $true)]
    [string]$ScanRoot,

    [switch]$AllowReviewedRuntimePdbGaps
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

function Test-ReviewedRuntimePdbGap
{
    param($Notification, $Run, $Invocation, $Policy, [string]$Message)

    if (-not $AllowReviewedRuntimePdbGaps -or $Notification.descriptor.id -ne $Policy.ruleId -or $Message -notmatch [regex]::Escape($Policy.errorCode))
    {
        return $false
    }

    # Require a structured target location; a filename in prose alone is not enough to exempt a diagnostic.
    if (@($Notification.locations).Count -ne 1)
    {
        return $false
    }
    $location = $Notification.locations[0].physicalLocation.artifactLocation
    if (-not $location.uri -and $null -ne $location.index -and $location.index -ge 0 -and $location.index -lt @($Run.artifacts).Count)
    {
        $location = $Run.artifacts[$location.index].location
    }
    if (-not $location.uri)
    {
        return $false
    }

    try
    {
        $uri = [uri]::new([string]$location.uri, [System.UriKind]::RelativeOrAbsolute)
        if (-not $uri.IsAbsoluteUri)
        {
            $baseUri = if ($location.uriBaseId) { $Run.originalUriBaseIds.($location.uriBaseId).uri } else { $Invocation.workingDirectory.uri }
            if (-not $baseUri)
            {
                return $false
            }
            $uri = [uri]::new([uri]$baseUri, $uri)
        }
        if (-not $uri.IsFile)
        {
            return $false
        }
        $relativePath = [System.IO.Path]::GetRelativePath([System.IO.Path]::GetFullPath($ScanRoot), $uri.LocalPath)
        return $relativePath -notmatch '[\\/]' -and $relativePath -in $Policy.files
    }
    catch
    {
        return $false
    }
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
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'binskim-runtime-pdb-exceptions.json') -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()
$reviewedPdbGaps = 0

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
            if ($invocation.executionSuccessful -ne $true -or ($null -ne $invocation.exitCode -and $invocation.exitCode -ne 0))
            {
                $failures.Add("BinSkim execution did not succeed in $($report.FullName).")
            }
            foreach ($notification in @($invocation.toolExecutionNotifications) + @($invocation.toolConfigurationNotifications))
            {
                if ($null -eq $notification)
                {
                    continue
                }
                $message = Get-NotificationMessage $notification
                if (Test-ReviewedRuntimePdbGap $notification $run $invocation $policy $message)
                {
                    $reviewedPdbGaps++
                    Write-Host "##vso[task.logissue type=warning]Reviewed BinSkim runtime PDB coverage gap: $message"
                }
                elseif (-not $notification.level -or $notification.level -in @('warning', 'error') -or $notification.descriptor.id -like 'ERR*')
                {
                    $failures.Add("BinSkim diagnostic $($notification.descriptor.id): $message")
                }
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
Write-Host "Verified $($reports.Count) BinSkim SARIF report(s); reviewed runtime PDB warnings: $reviewedPdbGaps."
