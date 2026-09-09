<#
.SYNOPSIS
Exercises smoke configuration, real script-module imports, commands, and reports offline.

.DESCRIPTION
Creates versioned dummy modules under an OS temporary directory. Child processes
see only those modules and PowerShell's built-in modules. Does not install Az,
call Azure, or change user module/repository registrations.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).ProviderPath
$smokeScript = Join-Path $repoRoot 'devops\scripts\packaging\powershell\test_ps_smoke.ps1'
$defaultConfig = Join-Path $repoRoot 'devops\scripts\packaging\powershell\ps_smoke_tests.psd1'
$pwshPath = (Get-Process -Id $PID).Path
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) "azclips-smoke-fixtures-$([guid]::NewGuid().ToString('N'))"
$originalModulePath = $env:PSModulePath
$script:passed = 0

function Assert-Fixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Fixture assertion failed: $Message" }
}

function New-FixtureModule
{
    param([string]$Name, [string]$Body, [string]$Version = '1.2.3', [string]$Requires = '@()')
    $moduleRoot = Join-Path $fixtureRoot "modules\$Name\$Version"
    New-Item -ItemType Directory -Path $moduleRoot -Force | Out-Null
    $rootModule = if ($Body) { "'$Name.psm1'" } else { "''" }
    $exports = if ($Body) { "'*'" } else { '@()' }
    $manifest = "@{ ModuleVersion = '$Version'; RootModule = $rootModule; RequiredModules = $Requires; FunctionsToExport = $exports; CmdletsToExport = @(); AliasesToExport = @(); VariablesToExport = @() }"
    Set-Content -LiteralPath (Join-Path $moduleRoot "$Name.psd1") -Value $manifest
    if ($Body) { Set-Content -LiteralPath (Join-Path $moduleRoot "$Name.psm1") -Value $Body }
    if ($Version -eq '1.2.3')
    {
        $sourceName = if ($Name -eq 'Az') { 'Az' } else { $Name.Substring(3) }
        Set-Content -LiteralPath (Join-Path $fixtureRoot "source\$sourceName.psd1") -Value $manifest
    }
}

$probeBody = @'
function Get-SmokeValue
{
    [CmdletBinding()]
    param([switch]$KeysOnly, [string]$Profile)
    'fixture-output'
}
function Get-SmokeMissingProfile
{
    [CmdletBinding()]
    param([string]$Profile)
    Write-Error -Message "Profile '$Profile' does not exist." -ErrorId ProfileMissing
}
function Remove-SmokeItem
{
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()
    if ($PSCmdlet.ShouldProcess('fixture', 'Remove')) { throw 'WhatIf was not supplied.' }
}
function Get-SmokeFailure { [CmdletBinding()]param([switch]$KeysOnly) Write-Error 'Deliberate command failure.' }
function Get-SmokeEmpty { [CmdletBinding()]param([switch]$KeysOnly) }
function Get-SmokeSleep { [CmdletBinding()]param() Start-Sleep -Seconds 30 }
function Remove-SmokeUnsafe { [CmdletBinding()]param() throw 'Unsafe command must not be invoked.' }
'@

$baselineConfig = @'
@{
    SchemaVersion = 1
    Modules = @(
        @{
            Names = @('Az.Probe')
            Tests = @(
                @{ Name = 'output'; Command = 'Get-SmokeValue'; Mode = 'ReadOnly'; Parameters = @{ KeysOnly = $true }; MinimumOutputCount = 1 }
                @{ Name = 'whatif'; Command = 'Remove-SmokeItem'; Mode = 'WhatIf'; Parameters = @{} }
                @{ Name = 'expected-error'; Command = 'Get-SmokeMissingProfile'; Mode = 'ReadOnly'; Parameters = @{ Profile = '{MissingProfile}' }; ExpectedError = @{ Id = 'ProfileMissing'; Message = 'Profile ''{MissingProfile}'' does not exist.' } }
            )
        }
        @{
            Names = @('Az.Other')
            Tests = @(
                @{ Name = 'other'; Command = 'Get-SmokeValue'; Mode = 'ReadOnly'; Parameters = @{}; MinimumOutputCount = 1 }
            )
        }
        @{
            Names = @('Az')
            Tests = @(
                @{ Name = 'rollup'; Command = 'Get-SmokeValue'; CommandModule = 'Az.Probe'; Mode = 'ReadOnly'; Parameters = @{}; MinimumOutputCount = 1 }
            )
        }
    )
}
'@
$baselineConfig = $baselineConfig.Replace("`r`n", "`n")

function Reset-Fixture
{
    foreach ($directory in 'modules', 'source', 'reports')
    {
        $path = Join-Path $fixtureRoot $directory
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
    New-FixtureModule Az.Probe $probeBody
    New-FixtureModule Az.Other 'function Get-SmokeValue { [CmdletBinding()]param() "other-output" }'
    New-FixtureModule Az '' -Requires "@(@{ ModuleName = 'Az.Probe'; ModuleVersion = '1.2.3' })"
    # Minimum-version dependency declarations must not select this newer module.
    New-FixtureModule Az.Probe "throw 'Loaded the wrong module version.'" -Version '9.0.0'
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'smoke.psd1') -Value $baselineConfig
}

function Invoke-FixtureSmoke
{
    param([bool]$ExpectSuccess, [string]$Config = (Join-Path $fixtureRoot 'smoke.psd1'), [int]$Timeout = 30)
    $failed = $false
    try
    {
        & $smokeScript -SourcePsRoot (Join-Path $fixtureRoot 'source') -ConfigPath $Config `
            -PwshPath $pwshPath -ReportRoot (Join-Path $fixtureRoot 'reports') -TimeoutSeconds $Timeout 6>$null
    }
    catch
    {
        $failed = $true
    }
    Assert-Fixture ($failed -ne $ExpectSuccess) "Expected success=$ExpectSuccess, observed failure=$failed."
    $report = Get-Content -LiteralPath (Join-Path $fixtureRoot 'reports\smoke-tests.json') -Raw | ConvertFrom-Json -AsHashtable
    Assert-Fixture ($report.schemaVersion -eq 1) 'Smoke report must be schema 1.'
    Assert-Fixture ($report.status -eq $(if ($ExpectSuccess) { 'Succeeded' } else { 'Failed' })) 'Report must agree with script outcome.'
    Assert-Fixture ($report.startedAtUtc -and $report.completedAtUtc) 'Report must include timestamps.'
    return $report
}

function Complete-Fixture
{
    param([string]$Name)
    $script:passed++
    Write-Host "PASS: $Name"
}

try
{
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    $env:PSModulePath = (Join-Path $fixtureRoot 'modules') + [IO.Path]::PathSeparator + (Join-Path $PSHOME 'Modules')
    Reset-Fixture
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'reports') -Recurse -Force
    $report = Invoke-FixtureSmoke $true
    Assert-Fixture ($report.modules.Count -eq 3) 'Every source module, including the rollup, must run.'
    $probe = @($report.modules | Where-Object module -eq 'Az.Probe')[0]
    Assert-Fixture ($probe.version -eq '1.2.3' -and $probe.importStatus -eq 'Succeeded') 'Import must use exact source version.'
    Assert-Fixture ($probe.tests.Count -eq 3 -and $probe.tests[2].expectedErrorObserved) 'All configured tests and exact expected errors must run.'
    Assert-Fixture ($probe.tests[0].outputCount -eq 1 -and $probe.tests[0].durationMilliseconds -ge 0) 'Record output counts and timings.'
    Assert-Fixture (-not ((Get-Content (Join-Path $fixtureRoot 'reports\smoke-tests.json') -Raw).Contains('fixture-output'))) 'Do not serialize command output.'
    if (-not $IsWindows)
    {
        $mode = [IO.File]::GetUnixFileMode((Join-Path $fixtureRoot 'reports\smoke-tests.json'))
        Assert-Fixture (($mode -band [IO.UnixFileMode]::OtherRead) -ne 0) 'The host artifact publisher must be able to read container-owned reports.'
    }
    Complete-Fixture 'Exact imports, dependency pinning, command execution, WhatIf, expected errors, and rollup'

    Reset-Fixture
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'source\Az.psd1') -Value "@{ ModuleVersion = '1.2.3'; RequiredModules = @(@{ ModuleName = '../Probe/Probe.psd1'; ModuleVersion = '1.2.3' }); CmdletsToExport = @() }"
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'modules\Az\1.2.3\Az.psd1') -Value "@{ ModuleVersion = '1.2.3'; RequiredModules = @(); FunctionsToExport = @(); CmdletsToExport = @() }"
    $report = Invoke-FixtureSmoke $true
    Assert-Fixture (($report.modules | Where-Object module -eq 'Az').status -eq 'Succeeded') 'Pin and delegate through source dependencies even when packaging clears RequiredModules.'
    Complete-Fixture 'Source dependency paths and NuGet-only rollup requirements'

    Reset-Fixture
    New-FixtureModule Az.New 'function Get-SmokeValue { [CmdletBinding()]param() "new-output" }'
    $report = Invoke-FixtureSmoke $false
    Assert-Fixture ($report.errors[0] -match 'Missing smoke-test configuration.*Az.New') 'New modules must fail with actionable missing-coverage diagnostics.'
    $extended = $baselineConfig.Replace("Names = @('Az.Other')", "Names = @('Az.Other', 'Az.New')")
    Set-Content (Join-Path $fixtureRoot 'smoke.psd1') $extended
    $report = Invoke-FixtureSmoke $true
    Assert-Fixture ($report.modules.Count -eq 4) 'A configuration-only addition must exercise a new module.'
    Complete-Fixture 'Missing coverage and configuration-only extension'

    foreach ($case in @(
        @{ Name = 'Unknown schema property'; From = 'SchemaVersion = 1'; To = 'SchemaVersion = 1; Typo = 1' }
        @{ Name = 'Unsupported schema'; From = 'SchemaVersion = 1'; To = 'SchemaVersion = 2' }
        @{ Name = 'Duplicate module coverage'; From = "Names = @('Az.Other')"; To = "Names = @('Az.Other', 'Az.Probe')" }
        @{ Name = 'Duplicate test names'; From = "Name = 'whatif'"; To = "Name = 'output'" }
        @{ Name = 'Unknown test property'; From = "Name = 'whatif'"; To = "Name = 'whatif'; Retires = 3" }
        @{ Name = 'Empty test list'; From = "Tests = @(`n                @{ Name = 'other'; Command = 'Get-SmokeValue'; Mode = 'ReadOnly'; Parameters = @{}; MinimumOutputCount = 1 }`n            )"; To = 'Tests = @()' }
        @{ Name = 'WhatIf override'; From = "Name = 'whatif'; Command = 'Remove-SmokeItem'; Mode = 'WhatIf'; Parameters = @{}"; To = "Name = 'whatif'; Command = 'Remove-SmokeItem'; Mode = 'WhatIf'; Parameters = @{ WhatIf = `$false }" }
        @{ Name = 'Unsafe read-only verb'; From = "Command = 'Remove-SmokeItem'; Mode = 'WhatIf'"; To = "Command = 'Remove-SmokeItem'; Mode = 'ReadOnly'" }
        @{ Name = 'Negative output count'; From = 'MinimumOutputCount = 1'; To = 'MinimumOutputCount = -1' }
        @{ Name = 'Wildcard expected error'; From = "Id = 'ProfileMissing'; Message = 'Profile ''{MissingProfile}'' does not exist.'"; To = "Id = 'ProfileMissing'" }
    ))
    {
        Reset-Fixture
        Set-Content (Join-Path $fixtureRoot 'smoke.psd1') $baselineConfig.Replace($case.From, $case.To)
        $report = Invoke-FixtureSmoke $false
        Assert-Fixture ($report.modules.Count -eq 0 -and $report.errors.Count -eq 1) 'Invalid configuration must fail before any module runs.'
        Complete-Fixture $case.Name
    }

    foreach ($case in @(
        @{ Name = 'Unexpected command error'; From = "Command = 'Get-SmokeValue'; Mode"; To = "Command = 'Get-SmokeFailure'; Mode"; Error = 'Deliberate command failure' }
        @{ Name = 'Empty command output'; From = "Command = 'Get-SmokeValue'; Mode"; To = "Command = 'Get-SmokeEmpty'; Mode"; Error = 'Expected at least' }
        @{ Name = 'Missing exported command'; From = "Command = 'Get-SmokeValue'; Mode"; To = "Command = 'Get-DoesNotExist'; Mode"; Error = 'not exported' }
        @{ Name = 'WhatIf command without ShouldProcess'; From = "Command = 'Remove-SmokeItem'"; To = "Command = 'Remove-SmokeUnsafe'"; Error = 'supporting ShouldProcess' }
        @{ Name = 'Wrong expected error id'; From = "Id = 'ProfileMissing'"; To = "Id = 'DifferentError'"; Error = 'Unexpected command error' }
        @{ Name = 'Wrong expected error message'; From = "Message = 'Profile ''{MissingProfile}'' does not exist.'"; To = "Message = 'A different error.'"; Error = 'Unexpected command error' }
        @{ Name = 'Missing expected error'; From = "Command = 'Get-SmokeMissingProfile'"; To = "Command = 'Get-SmokeValue'"; Error = 'was not raised' }
        @{ Name = 'Wrong command owner'; From = "Name = 'output'; Command"; To = "Name = 'output'; CommandModule = 'Az.Other'; Command"; Error = 'not the tested module' }
        @{ Name = 'Undeclared rollup command owner'; From = "CommandModule = 'Az.Probe'"; To = "CommandModule = 'Az.Other'"; Error = 'declared dependency' }
    ))
    {
        Reset-Fixture
        Set-Content (Join-Path $fixtureRoot 'smoke.psd1') $baselineConfig.Replace($case.From, $case.To)
        $report = Invoke-FixtureSmoke $false
        $failures = @($report.modules.tests | Where-Object status -eq 'Failed')
        Assert-Fixture (@($failures | Where-Object error -match $case.Error).Count -gt 0) "Expected diagnostic: $($case.Error)"
        Assert-Fixture ($report.modules.Count -eq 3) 'Failed commands must not prevent later module tests.'
        Complete-Fixture $case.Name
    }

    Reset-Fixture
    New-FixtureModule Az.Probe "throw 'Deliberate import failure.'"
    $report = Invoke-FixtureSmoke $false
    $probe = @($report.modules | Where-Object module -eq 'Az.Probe')[0]
    Assert-Fixture ($probe.importStatus -eq 'Failed' -and $probe.error -match 'Deliberate import failure') 'Import errors must fail explicitly.'
    Assert-Fixture (@($probe.tests | Where-Object status -ne 'NotRun').Count -eq 0) 'Do not claim commands ran after a failed import.'
    Assert-Fixture (@($report.modules | Where-Object { $_.module -eq 'Az.Other' -and $_.status -eq 'Succeeded' }).Count -eq 1) 'Continue independent modules after an import failure.'
    Complete-Fixture 'Import failure aggregation'

    Reset-Fixture
    Remove-Item -LiteralPath (Join-Path $fixtureRoot 'modules\Az.Probe\1.2.3') -Recurse -Force
    $report = Invoke-FixtureSmoke $false
    Assert-Fixture (($report.modules | Where-Object module -eq 'Az.Probe').error -match 'found 0') 'A newer version must not substitute for a missing required version.'
    Complete-Fixture 'Missing target version with a newer version available'

    Reset-Fixture
    New-FixtureModule Az.Probe @'
function Get-SmokeValue { [CmdletBinding()]param([switch]$KeysOnly) Start-Sleep -Seconds 30 }
'@
    $report = Invoke-FixtureSmoke $false -Timeout 3
    $probe = $report.modules | Where-Object module -eq 'Az.Probe'
    Assert-Fixture ($probe.error -match 'timeout') 'A hung child must fail with a bounded timeout.'
    Assert-Fixture ($probe.importStatus -eq 'Succeeded' -and $probe.tests[0].status -eq 'Interrupted') 'Retain completed imports and mark the active command interrupted.'
    Assert-Fixture ($probe.tests[1].status -eq 'NotRun') 'Do not claim later commands ran after a timeout.'
    Assert-Fixture (($report.modules | Where-Object module -eq 'Az.Other').status -eq 'Succeeded') 'Continue after terminating a timed-out child.'
    Complete-Fixture 'Bounded module timeout'

    foreach ($accountModule in 'Az.Login', 'Az.Accounts')
    {
        Reset-Fixture
        foreach ($manifest in @(Get-ChildItem (Join-Path $fixtureRoot 'source') -File)) { Remove-Item -LiteralPath $manifest.FullName }
        $defaultBody = @'
function Disconnect-AzAccount { [CmdletBinding(SupportsShouldProcess = $true)]param() if ($PSCmdlet.ShouldProcess('fixture', 'Disconnect')) { throw 'Mutation is forbidden.' } }
function Remove-AzVM { [CmdletBinding(SupportsShouldProcess = $true)]param([string]$SubscriptionId, [string]$ResourceGroupName, [string]$Name) if ($PSCmdlet.ShouldProcess($Name, 'Remove')) { throw 'Mutation is forbidden.' } }
function Get-AzConfigInfo { [CmdletBinding()]param([switch]$KeysOnly) if (-not $KeysOnly) { throw 'Values must not be read.' }; 'config.key' }
function Get-AzResourceGroup { [CmdletBinding()]param([string]$SubscriptionId, [string]$Profile) Write-Error -Message "Profile '$Profile' does not exist." -ErrorId System.ArgumentException }
function Remove-AzUserAssignedIdentity { [CmdletBinding(SupportsShouldProcess = $true)]param([string]$SubscriptionId, [string]$ResourceGroupName, [string]$Name) if ($PSCmdlet.ShouldProcess($Name, 'Remove')) { throw 'Mutation is forbidden.' } }
function Remove-AzMsGraphApplication { [CmdletBinding(SupportsShouldProcess = $true)]param([string]$Id) if ($PSCmdlet.ShouldProcess($Id, 'Remove')) { throw 'Mutation is forbidden.' } }
'@
        foreach ($name in @($accountModule, 'Az.Compute', 'Az.Config', 'Az.Resources', 'Az.Identity', 'Az.MsGraph')) { New-FixtureModule $name $defaultBody }
        New-FixtureModule Az '' -Requires "@(@{ ModuleName = 'Az.Config'; ModuleVersion = '1.2.3' })"
        $report = Invoke-FixtureSmoke $true -Config $defaultConfig
        Assert-Fixture ($report.modules.Count -eq 7) 'Default config must cover both authentication names and current main modules.'
        Complete-Fixture "Default configuration with $accountModule, Identity, and MsGraph"
    }

    Write-Host "All $script:passed PowerShell smoke fixture groups passed."
}
finally
{
    $env:PSModulePath = $originalModulePath
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
