[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WorkerPath,
    [Parameter(Mandatory)][string]$RequestPath,
    [Parameter(Mandatory)][string]$ResultPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $env:AZCLIPS_UPGRADE_FIXTURE_ROOT -or
    -not (Test-Path -LiteralPath (Join-Path $env:AZCLIPS_UPGRADE_FIXTURE_ROOT '.upgrade-fixture')))
{
    throw 'The worker driver requires a marked private fixture directory.'
}
$root = [IO.Path]::GetFullPath($env:AZCLIPS_UPGRADE_FIXTURE_ROOT)
$env:PSModulePath = (Join-Path $root 'installed') + [IO.Path]::PathSeparator + (Join-Path $PSHOME 'Modules')
Import-Module Microsoft.PowerShell.Management, Microsoft.PowerShell.Utility -ErrorAction Stop
$global:PSModuleAutoLoadingPreference = 'None'
$managerDirectory = Join-Path $root 'installed\PowerShellGet\99.0.0'
Import-Module -Name (Join-Path $managerDirectory 'PowerShellGet.psd1') -Force -ErrorAction Stop
foreach ($command in 'Get-PSRepository', 'Register-PSRepository', 'Unregister-PSRepository', 'Get-InstalledModule', 'Install-Module', 'Uninstall-Module')
{
    $resolved = Get-Command $command -ErrorAction Stop
    if ($resolved.Module.ModuleBase -ne $managerDirectory -or $resolved.Module.Version -ne [version]'99.0.0')
    {
        throw "Host safety violation: $command did not resolve to the private fake PowerShellGet."
    }
}
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
$env:AZCLIPS_UPGRADE_FIXTURE_OPERATION = $request.operation
@{
    operation = 'Worker'; workerOperation = $request.operation; pid = $PID
    manager = (Get-Command Install-Module).Module.ModuleBase
    powerShellVersion = $PSVersionTable.PSVersion.ToString()
} | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $root 'events.jsonl')
if ($request.operation -eq 'Inspect' -and @($request.packages | Where-Object { $_.Name -eq 'Az' -and $_.Version -eq '0.1.3' }).Count -eq 1)
{
    $compute = Join-Path $root 'installed\Az.Compute\0.1.0'
    switch ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO)
    {
        { $_ -in 'stale-payload', 'cleanup-primary' } {
            Set-Content -LiteralPath (Join-Path $compute 'payload.txt') -Value 'released baseline bytes'
        }
        'unexpected-file' { Set-Content -LiteralPath (Join-Path $compute 'removed-feature.dll') -Value 'stale content' }
        'wrong-repository' {
            $state = Get-Content -LiteralPath (Join-Path $root 'state.json') -Raw | ConvertFrom-Json -AsHashtable
            $state.installed['Az.Compute|0.1.0'].Repository = 'ExistingGallery'
            $state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $root 'state.json')
        }
    }
}
if ($request.operation -eq 'Cleanup' -and $env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -eq 'success')
{
    $statePath = Join-Path $root 'state.json'
    $stateHash = (Get-FileHash -LiteralPath $statePath).Hash
    $payloadPath = Join-Path $root 'installed\Az.Compute\0.1.0\payload.txt'
    $payloadHash = (Get-FileHash -LiteralPath $payloadPath).Hash
    $caught = $null
    try { Uninstall-Module -Name Az.Compute -RequiredVersion '0.1.0' -Force -ErrorAction Stop }
    catch { $caught = $_ }
    if ($caught.Exception.Message -notmatch 'while required by' -or
        (Get-FileHash -LiteralPath $statePath).Hash -ne $stateHash -or
        (Get-FileHash -LiteralPath $payloadPath).Hash -ne $payloadHash)
    {
        throw 'Fixture manager must reject out-of-order dependency removal without changing installed state.'
    }
}

# Dot-source Install so restoration of its process-local defaults is observable,
# including when the helper fails. Preserve the worker's exit before assertions.
if ($request.operation -eq 'Install')
{
    $PSDefaultParameterValues = @{ 'Get-Content:Encoding' = 'utf8' }
    if ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -ne 'target-installer-failure')
    {
        $PSDefaultParameterValues['Install-Module:SkipPublisherCheck'] = $false
    }
    $fixtureDefaults = $PSDefaultParameterValues
    $fixtureDefaultsSnapshot = $PSDefaultParameterValues | ConvertTo-Json -Compress
    . $WorkerPath -RequestPath $RequestPath -ResultPath $ResultPath
}
else
{
    & $WorkerPath -RequestPath $RequestPath -ResultPath $ResultPath
}
$workerExitCode = [int]$LASTEXITCODE
if ($request.operation -eq 'Install')
{
    if (-not [object]::ReferenceEquals($PSDefaultParameterValues, $fixtureDefaults) -or
        ($PSDefaultParameterValues | ConvertTo-Json -Compress) -cne $fixtureDefaultsSnapshot)
    {
        throw 'The real worker did not restore preexisting parameter defaults after its installer call.'
    }
    @{
        operation = 'DefaultsRestored'; name = $request.moduleName; version = $request.version
        skipKeyPresent = $PSDefaultParameterValues.ContainsKey('Install-Module:SkipPublisherCheck')
        skipPublisherCheck = [bool]$PSDefaultParameterValues['Install-Module:SkipPublisherCheck']
        otherDefault = $PSDefaultParameterValues['Get-Content:Encoding']
    } | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $root 'events.jsonl')
}
if ($request.operation -eq 'Install' -and $request.moduleName -eq 'Az' -and $request.version -eq '0.1.2')
{
    if ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -eq 'no-result')
    {
        Remove-Item -LiteralPath $ResultPath -Force
    }
}
exit $workerExitCode
