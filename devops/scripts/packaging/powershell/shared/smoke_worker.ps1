<#
.SYNOPSIS
Internal fresh-process worker for test_ps_smoke.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [Parameter(Mandatory = $true)][string]$ResultPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'phase_artifacts.ps1')
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json -AsHashtable
$definitions = @{}
foreach ($definition in $request.definitions) { $definitions[$definition.PackageName] = $definition }
$imported = @{}
$importing = @{}

function Import-PinnedSmokeModule
{
    param([string]$Name)
    if ($imported.ContainsKey($Name)) { return $imported[$Name] }
    if (-not $definitions.ContainsKey($Name)) { throw "Az dependency $Name has no source definition." }
    if ($importing.ContainsKey($Name)) { throw "Cyclic Az dependency involving $Name." }
    $importing[$Name] = $true
    $version = [version]$definitions[$Name].Version
    $loaded = @(Get-Module -Name $Name -All)
    if (@($loaded | Where-Object Version -ne $version).Count -gt 0) { throw "An unexpected version of $Name was loaded before its smoke test." }
    $available = @(Get-Module -ListAvailable -FullyQualifiedName @{ ModuleName = $Name; RequiredVersion = $version } |
        Sort-Object -Property Path -Unique)
    if ($available.Count -ne 1) { throw "Expected one available $Name $version module; found $($available.Count)." }
    # Packaging may move requirements to NuGet metadata and clear RequiredModules.
    # Retain the source dependency graph as well as the installed manifest's graph.
    $dependencies = @($definitions[$Name].RequiredModules) + @($available[0].RequiredModules.Name)
    foreach ($dependency in @($dependencies | Where-Object { $_ } | Sort-Object -Unique))
    {
        if ($dependency -eq 'Az' -or $dependency.StartsWith('Az.', [StringComparison]::OrdinalIgnoreCase))
        {
            Import-PinnedSmokeModule -Name $dependency | Out-Null
        }
    }
    $module = @(Import-Module -Name $available[0].Path -RequiredVersion $version -PassThru -ErrorAction Stop)
    if ($module.Count -ne 1 -or $module[0].Name -ne $Name -or $module[0].Version -ne $version)
    {
        throw "Import did not return the expected $Name $version module."
    }
    # A dependency's manifest can otherwise auto-load a newer installed version.
    foreach ($loadedModule in @(Get-Module -All))
    {
        if ($definitions.ContainsKey($loadedModule.Name) -and $loadedModule.Version -ne [version]$definitions[$loadedModule.Name].Version)
        {
            throw "Import loaded an unexpected $($loadedModule.Name) $($loadedModule.Version) dependency."
        }
    }
    $imported[$Name] = $module[0]
    $importing.Remove($Name)
    return $module[0]
}

$result = [ordered]@{
    module = $request.module
    version = $definitions[$request.module].Version
    status = 'Failed'
    importStatus = 'Running'
    error = $null
    tests = @($request.tests | ForEach-Object { @{ name = $_.Name; command = $_.Command; status = 'NotRun' } })
}
$missingProfile = "azclips-smoke-missing-$([guid]::NewGuid().ToString('N'))"
try
{
    Write-PSValidationJson -Path $ResultPath -Value $result
    $module = Import-PinnedSmokeModule -Name $request.module
    $result.importStatus = 'Succeeded'
    $testIndex = 0
    foreach ($test in $request.tests)
    {
        $testResult = [ordered]@{
            name = $test.Name
            command = $test.Command
            commandModule = if ($test.CommandModule) { $test.CommandModule } else { $request.module }
            status = 'Running'
            durationMilliseconds = 0
            outputCount = 0
            expectedErrorObserved = $false
            error = $null
        }
        $result.tests[$testIndex] = $testResult
        Write-PSValidationJson -Path $ResultPath -Value $result
        $timer = [Diagnostics.Stopwatch]::StartNew()
        try
        {
            $owner = $testResult.commandModule
            if ($owner -ne $request.module -and ($request.module -ne 'Az' -or $owner -notin $definitions[$request.module].RequiredModules))
            {
                throw "CommandModule $owner is not the tested module or a declared dependency of the Az rollup."
            }
            $commandModule = Import-PinnedSmokeModule -Name $owner
            $command = $commandModule.ExportedCommands[$test.Command]
            if (-not $command -or $command.Module.Name -ne $owner -or $command.Module.Version -ne [version]$definitions[$owner].Version)
            {
                throw "Command $($test.Command) is not exported by the pinned $owner module."
            }
            $parameters = @{}
            foreach ($key in $test.Parameters.Keys)
            {
                if (-not $command.Parameters.ContainsKey($key)) { throw "Command $($test.Command) has no parameter '$key'." }
                $value = $test.Parameters[$key]
                $parameters[$key] = if ($value -is [string]) { $value.Replace('{MissingProfile}', $missingProfile) } else { $value }
            }
            if ($test.Mode -eq 'WhatIf')
            {
                if (-not ([Management.Automation.CommandMetadata]::new($command)).SupportsShouldProcess)
                {
                    throw "WhatIf test $($test.Name) requires a command supporting ShouldProcess."
                }
                $parameters.WhatIf = $true
                $parameters.Confirm = $false
            }
            $parameters.ErrorAction = 'Stop'
            $commandError = $null
            try
            {
                # Count output without persisting potentially sensitive objects in the report.
                & $command @parameters | ForEach-Object { $testResult.outputCount++ }
            }
            catch
            {
                $commandError = $_
            }
            if ($test.ExpectedError)
            {
                $expectedMessage = $test.ExpectedError.Message.Replace('{MissingProfile}', $missingProfile)
                if (-not $commandError) { throw "Expected error '$($test.ExpectedError.Id)' was not raised." }
                if (($commandError.FullyQualifiedErrorId -split ',')[0] -cne $test.ExpectedError.Id -or $commandError.Exception.Message -cne $expectedMessage)
                {
                    throw "Unexpected command error: $($commandError.FullyQualifiedErrorId): $($commandError.Exception.Message)"
                }
                $testResult.expectedErrorObserved = $true
            }
            elseif ($commandError)
            {
                throw $commandError
            }
            if ($testResult.outputCount -lt $test.MinimumOutputCount) { throw "Expected at least $($test.MinimumOutputCount) output object(s); received $($testResult.outputCount)." }
            $testResult.status = 'Succeeded'
        }
        catch
        {
            $testResult.status = 'Failed'
            $testResult.error = $_.Exception.Message
        }
        finally
        {
            $timer.Stop()
            $testResult.durationMilliseconds = $timer.ElapsedMilliseconds
        }
        $testIndex++
        Write-PSValidationJson -Path $ResultPath -Value $result
        Write-Host "$($testResult.status): $($request.module)/$($test.Name) ($($test.Command))"
        if ($testResult.error) { Write-Host $testResult.error }
    }
    if (@($result.tests | Where-Object status -ne 'Succeeded').Count -eq 0) { $result.status = 'Succeeded' }
}
catch
{
    if ($result.importStatus -eq 'Running') { $result.importStatus = 'Failed' }
    $result.error = $_.Exception.Message
}
finally
{
    Write-PSValidationJson -Path $ResultPath -Value $result
}
