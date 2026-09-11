function Write-PSValidationJson
{
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value
    )

    $temporaryPath = "$Path.$([guid]::NewGuid().ToString('N')).tmp"
    try
    {
        [IO.File]::WriteAllText($temporaryPath, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $Path, $true)
    }
    finally
    {
        if ([IO.File]::Exists($temporaryPath)) { [IO.File]::Delete($temporaryPath) }
    }
}

function Initialize-PSValidationReport
{
    param(
        [Parameter(Mandatory)][string]$ReportRoot,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Metadata
    )

    New-Item -ItemType Directory -Path (Join-Path $ReportRoot 'logs') -Force -ErrorAction Stop | Out-Null
    $matrixPath = Join-Path $ReportRoot 'matrix.json'
    if (-not (Test-Path -LiteralPath $matrixPath))
    {
        Write-PSValidationJson -Path $matrixPath -Value $Metadata
    }
    $resultsPath = Join-Path $ReportRoot 'results.json'
    if (-not (Test-Path -LiteralPath $resultsPath))
    {
        $phases = @(foreach ($phase in 'Bootstrap', 'Install', 'Upgrade', 'SmokeCoverage', 'Validate', 'Uninstall')
        {
            [ordered]@{
                phase = $phase
                status = 'NotRun'
                startedAtUtc = $null
                finishedAtUtc = $null
                exitCode = $null
                message = $null
            }
        })
        Write-PSValidationJson -Path $resultsPath -Value ([ordered]@{
            schemaVersion = 1
            status = 'NotStarted'
            completedAtUtc = $null
            phases = $phases
        })
    }
}

function Set-PSValidationPhaseResult
{
    param(
        [Parameter(Mandatory)][string]$ReportRoot,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)]
        [ValidateSet('Running', 'Succeeded', 'SucceededWithIssues', 'Failed', 'Skipped')]
        [string]$Status,
        [int]$ExitCode = 0,
        [string]$Message
    )

    $path = Join-Path $ReportRoot 'results.json'
    $report = Read-PSValidationReport -Path $path
    $entry = @($report.phases | Where-Object { $_.phase -eq $Phase })
    if ($entry.Count -ne 1) { throw "Expected exactly one '$Phase' result in '$path'." }
    $entry[0].status = $Status
    if ($Status -eq 'Running')
    {
        $report.status = 'Running'
        $entry[0].startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    }
    else
    {
        $entry[0].finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        $entry[0].exitCode = $ExitCode
        $entry[0].message = $Message
    }
    Write-PSValidationJson -Path $path -Value $report
}

function Read-PSValidationReport
{
    param([Parameter(Mandatory)][string]$Path)

    $report = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable
    $expectedPhases = 'Bootstrap,Install,Upgrade,SmokeCoverage,Validate,Uninstall'
    $allowedStatuses = @('NotRun', 'Running', 'Succeeded', 'SucceededWithIssues', 'Failed', 'Skipped', 'Interrupted')
    if ($report.schemaVersion -ne 1 -or
        (($report.phases | ForEach-Object { $_.phase }) -join ',') -ne $expectedPhases -or
        @($report.phases | Where-Object { $_.status -notin $allowedStatuses }).Count -gt 0)
    {
        throw "Invalid PowerShell validation report: $Path"
    }
    return $report
}

function Complete-PSValidationReport
{
    param(
        [Parameter(Mandatory)][string]$ReportRoot,
        [string]$ValidationKey,
        [string]$JobStatus
    )

    Initialize-PSValidationReport -ReportRoot $ReportRoot -Metadata ([ordered]@{
        validationKey = $ValidationKey
        buildId = $env:BUILD_BUILDID
        jobId = $env:SYSTEM_JOBID
        note = 'Bootstrap did not initialize phase metadata.'
    })
    $path = Join-Path $ReportRoot 'results.json'
    $report = Read-PSValidationReport -Path $path
    foreach ($entry in $report.phases)
    {
        if ($entry.status -eq 'NotRun')
        {
            $entry.status = 'Skipped'
            $entry.message = 'Not executed; an earlier phase or job prerequisite did not complete.'
        }
        elseif ($entry.status -eq 'Running')
        {
            $entry.status = 'Interrupted'
            $entry.message = 'Execution started but did not record a completed result.'
        }
        $logPath = Join-Path $ReportRoot "logs/$($entry.phase.ToLowerInvariant()).log"
        if (-not (Test-Path -LiteralPath $logPath))
        {
            "$($entry.phase): $($entry.status). $($entry.message)" |
                Set-Content -LiteralPath $logPath -Encoding utf8 -ErrorAction Stop
        }
    }
    $requiredPhases = @($report.phases | Where-Object { $_.phase -in 'Bootstrap', 'Install', 'SmokeCoverage', 'Validate', 'Uninstall' })
    $report.status = if ($JobStatus -eq 'Canceled') { 'Canceled' }
        elseif ($JobStatus -eq 'Failed' -or @($report.phases | Where-Object { $_.status -eq 'Failed' }).Count -gt 0) { 'Failed' }
        elseif (@($report.phases | Where-Object { $_.phase -ne 'SmokeCoverage' -and $_.status -eq 'SucceededWithIssues' }).Count -gt 0) { 'SucceededWithIssues' }
        elseif (@($report.phases | Where-Object { $_.status -eq 'Interrupted' }).Count -gt 0 -or
            @($requiredPhases | Where-Object { $_.status -notin 'Succeeded', 'SucceededWithIssues' }).Count -gt 0) { 'Incomplete' }
        elseif (@($report.phases | Where-Object { $_.status -eq 'SucceededWithIssues' }).Count -gt 0) { 'SucceededWithIssues' }
        else { 'Succeeded' }
    $report.completedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $report.jobStatusAtCollection = $JobStatus
    $report.runtimeEnvironmentCaptured = Test-Path -LiteralPath (Join-Path $ReportRoot 'environment.json') -PathType Leaf
    Write-PSValidationJson -Path $path -Value $report
}
