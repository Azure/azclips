<#
.SYNOPSIS
Imports every packaged Az module and executes its configured smoke commands.

.DESCRIPTION
Uses ps_smoke_tests.psd1 for named, credential-free command cases. Every source
module must have coverage. Each module runs in a fresh, noninteractive instance
of the tested PowerShell, with exact-version imports for its Az dependencies.
Commands are invoked through their owning module, not unqualified name lookup.
Failures are aggregated, including import failures and bounded child timeouts.
An optional ReportRoot receives smoke-tests.json; command output and credentials
are not serialized. This validates local command execution, not live Azure APIs.
#>
[CmdletBinding()]
param(
    [string]$SourcePsRoot = (Join-Path $PSScriptRoot '..\..\..\..\src\PS'),
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'ps_smoke_tests.psd1'),
    [string]$PwshPath = 'pwsh',
    [string]$ReportRoot,
    [ValidateRange(1, 1800)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared\module_definitions.ps1')
. (Join-Path $PSScriptRoot 'shared\phase_artifacts.ps1')
. (Join-Path $PSScriptRoot 'shared\module_artifacts.ps1')

function Assert-SmokeKeys
{
    param($Value, [string[]]$Allowed, [string[]]$Required, [string]$Context)
    if ($Value -isnot [System.Collections.IDictionary]) { throw "$Context must be a hashtable." }
    foreach ($key in $Value.Keys)
    {
        if ($key -notin $Allowed) { throw "Unknown $Context property: $key" }
    }
    foreach ($key in $Required)
    {
        if (-not $Value.Contains($key)) { throw "Missing $Context property: $key" }
    }
}

function Read-SmokeConfiguration
{
    $config = Import-PowerShellDataFile -LiteralPath $ConfigPath -ErrorAction Stop
    Assert-SmokeKeys $config @('SchemaVersion', 'Modules') @('SchemaVersion', 'Modules') 'configuration'
    if ($config.SchemaVersion -isnot [int] -or $config.SchemaVersion -ne 1) { throw 'Unsupported smoke-test SchemaVersion; expected 1.' }
    if ($config.Modules -isnot [array] -or $config.Modules.Count -eq 0) { throw 'Modules must be a nonempty array.' }

    $coverage = @{}
    foreach ($entry in $config.Modules)
    {
        Assert-SmokeKeys $entry @('Names', 'Tests') @('Names', 'Tests') 'module'
        if ($entry.Names -isnot [array] -or $entry.Names.Count -eq 0) { throw 'Module Names must be a nonempty array.' }
        if ($entry.Tests -isnot [array] -or $entry.Tests.Count -eq 0) { throw 'Module Tests must be a nonempty array.' }
        $testNames = @{}
        foreach ($test in $entry.Tests)
        {
            Assert-SmokeKeys $test @('Name', 'Command', 'CommandModule', 'Mode', 'Parameters', 'MinimumOutputCount', 'ExpectedError') @('Name', 'Command', 'Mode', 'Parameters') 'test'
            if ($test.Name -isnot [string] -or $test.Name -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]*$') { throw 'Test Name must be a nonempty identifier.' }
            if ($testNames.ContainsKey($test.Name)) { throw "Duplicate test Name: $($test.Name)" }
            $testNames[$test.Name] = $true
            if ($test.Command -isnot [string] -or $test.Command -notmatch '^[a-zA-Z]+-[a-zA-Z0-9]+$') { throw "Invalid Command for test $($test.Name)." }
            if ($test.Mode -isnot [string] -or $test.Mode -notin 'ReadOnly', 'WhatIf') { throw "Test $($test.Name) Mode must be ReadOnly or WhatIf." }
            if ($test.Mode -eq 'ReadOnly' -and $test.Command -notmatch '^(Get|Test)-') { throw "ReadOnly test $($test.Name) must use a Get- or Test- command." }
            if ($test.ContainsKey('CommandModule') -and ($test.CommandModule -isnot [string] -or $test.CommandModule -notmatch '^Az(\.[a-zA-Z0-9]+)*$')) { throw "Invalid CommandModule for test $($test.Name)." }
            if ($test.Parameters -isnot [System.Collections.IDictionary]) { throw "Parameters for test $($test.Name) must be a hashtable." }
            foreach ($key in $test.Parameters.Keys)
            {
                if ($key -notmatch '^[a-zA-Z][a-zA-Z0-9]*$' -or $key -in 'WhatIf', 'Confirm', 'ErrorAction', 'ErrorVariable', 'OutVariable')
                {
                    throw "Reserved or invalid parameter '$key' for test $($test.Name)."
                }
            }
            if ($test.ContainsKey('MinimumOutputCount') -and ($test.MinimumOutputCount -isnot [int] -or $test.MinimumOutputCount -lt 0)) { throw "MinimumOutputCount for test $($test.Name) must be a nonnegative integer." }
            if ($test.ContainsKey('ExpectedError'))
            {
                Assert-SmokeKeys $test.ExpectedError @('Id', 'Message') @('Id', 'Message') 'ExpectedError'
                foreach ($key in 'Id', 'Message')
                {
                    if ($test.ExpectedError[$key] -isnot [string] -or [string]::IsNullOrWhiteSpace($test.ExpectedError[$key])) { throw "ExpectedError.$key must be a nonempty string." }
                }
                if ($test.MinimumOutputCount -gt 0) { throw "Test $($test.Name) cannot require output and an error." }
            }
        }
        foreach ($name in $entry.Names)
        {
            if ($name -isnot [string] -or $name -notmatch '^Az(\.[a-zA-Z0-9]+)*$') { throw "Invalid module name: $name" }
            if ($coverage.ContainsKey($name)) { throw "Duplicate smoke coverage for $name." }
            $coverage[$name] = $entry.Tests
        }
    }
    return $coverage
}

function Invoke-SmokeChild
{
    param([string]$Executable, [string]$RequestPath, [string]$ResultPath, [string]$WorkingDirectory)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.Environment['POWERSHELL_TELEMETRY_OPTOUT'] = '1'
    $startInfo.Environment['AZCLIPS_TELEMETRY_TARGET'] = 'local'
    foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot 'shared\smoke_worker.ps1'), '-RequestPath', $RequestPath, '-ResultPath', $ResultPath))
    {
        $startInfo.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    try
    {
        if (-not $process.Start()) { throw 'Failed to start smoke-test PowerShell.' }
        $started = $true
        # Drain both pipes concurrently so a verbose command cannot deadlock the timeout.
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut)
        {
            $process.Kill($true)
            $process.WaitForExit()
        }
        foreach ($text in @($stdout.GetAwaiter().GetResult(), $stderr.GetAwaiter().GetResult()))
        {
            if ($text) { Write-Host $text.TrimEnd() }
        }
        if ($timedOut) { throw "Module smoke tests exceeded the $TimeoutSeconds-second timeout." }
        if ($process.ExitCode -ne 0) { throw "Smoke-test PowerShell exited with code $($process.ExitCode)." }
        if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) { throw 'Smoke-test PowerShell did not produce its result.' }
        Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json -AsHashtable
    }
    finally
    {
        if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

$report = [ordered]@{
    schemaVersion = 1
    status = 'Failed'
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    completedAtUtc = $null
    modules = @()
    errors = @()
}
$scratchRoot = $null
try
{
    $definitions = @(Get-AzModuleDefinitions -SourcePsRoot (Resolve-Path -LiteralPath $SourcePsRoot).ProviderPath)
    $coverage = Read-SmokeConfiguration
    $missing = @($definitions | Where-Object { -not $coverage.ContainsKey($_.PackageName) } | ForEach-Object PackageName)
    if ($missing.Count -gt 0) { throw "Missing smoke-test configuration for: $($missing -join ', '). Add entries to $ConfigPath." }
    $executable = (Get-Command -Name $PwshPath -CommandType Application -ErrorAction Stop).Source
    $scratchRoot = Join-Path ([IO.Path]::GetTempPath()) "azclips-ps-smoke-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $scratchRoot -ErrorAction Stop | Out-Null

    foreach ($definition in $definitions)
    {
        $tests = @($coverage[$definition.PackageName])
        $result = [ordered]@{
            module = $definition.PackageName
            version = $definition.Version
            status = 'Failed'
            importStatus = 'NotRun'
            durationMilliseconds = 0
            error = $null
            tests = @($tests | ForEach-Object { @{ name = $_.Name; command = $_.Command; status = 'NotRun' } })
        }
        $timer = [Diagnostics.Stopwatch]::StartNew()
        try
        {
            $requestPath = Join-Path $scratchRoot "$($definition.PackageName)-request.json"
            $resultPath = Join-Path $scratchRoot "$($definition.PackageName)-result.json"
            Write-PSValidationJson -Path $requestPath -Value @{
                module = $definition.PackageName
                definitions = $definitions
                tests = $tests
            }
            $result = Invoke-SmokeChild -Executable $executable -RequestPath $requestPath -ResultPath $resultPath -WorkingDirectory $scratchRoot
        }
        catch
        {
            $failureMessage = $_.Exception.Message
            if (Test-Path -LiteralPath $resultPath -PathType Leaf)
            {
                try
                {
                    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json -AsHashtable
                    if ($result.importStatus -eq 'Running') { $result.importStatus = 'Interrupted' }
                    foreach ($testResult in $result.tests)
                    {
                        if ($testResult.status -eq 'Running') { $testResult.status = 'Interrupted' }
                    }
                }
                catch
                {
                    $failureMessage += " Could not read partial smoke results: $($_.Exception.Message)"
                }
            }
            $result.status = 'Failed'
            $result.error = $failureMessage
        }
        finally
        {
            $timer.Stop()
            $result.durationMilliseconds = $timer.ElapsedMilliseconds
        }
        $report.modules += $result
        Write-Host "$($result.status): $($definition.PackageName) $($definition.Version)"
        if ($result.error) { Write-Host $result.error }
    }
    if (@($report.modules | Where-Object status -ne 'Succeeded').Count -eq 0) { $report.status = 'Succeeded' }
}
catch
{
    $report.errors += $_.Exception.Message
    throw
}
finally
{
    $report.completedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    try
    {
        if ($ReportRoot)
        {
            Write-AzArtifactJson -Path (Join-Path $ReportRoot 'smoke-tests.json') -Value $report
        }
    }
    catch
    {
        if ($report.status -eq 'Succeeded') { throw }
        Write-Warning "Could not save smoke results; the smoke-test failure remains primary: $($_.Exception.Message)" -WarningAction Continue
    }
    finally
    {
        if ($scratchRoot -and (Test-Path -LiteralPath $scratchRoot)) { Remove-Item -LiteralPath $scratchRoot -Recurse -Force -ErrorAction Stop }
    }
}

if ($report.status -ne 'Succeeded')
{
    throw "PowerShell smoke tests failed for $(@($report.modules | Where-Object status -ne 'Succeeded').Count) module(s). See Validate output and smoke-tests.json when reporting is enabled."
}
