<#
.SYNOPSIS
Tests the real PowerShell upgrade orchestrator and workers without host installations.
.DESCRIPTION
Uses real NuGet archives, hashes and module imports in fresh bounded child processes.
Every child explicitly loads a private version-99 fake PowerShellGet before running
the production worker. No network, real package-manager operations, Pester, or runtime
version spoofing is used. A synthetic released installer retains the historical
Register/Install-Module workflow and is compatible with the actual fixture runtime.
Scratch data stays under this checkout's artifacts directory and is always removed.
.EXAMPLE
pwsh -NoProfile -File tests\Scripts\PS\PSUpgradeFixtures.ps1
#>
[CmdletBinding()]
param([string[]]$Scenario)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
. (Join-Path $repoRoot 'devops\scripts\packaging\powershell\shared\upgrade_modules.ps1')
$script:realRuntimeProcess = (Get-Command Invoke-PSRuntimeProcess).ScriptBlock
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$originalModulePath = $env:PSModulePath
$originalAgentTemp = $env:AGENT_TEMPDIRECTORY
$originalFixtureRoot = $env:AZCLIPS_UPGRADE_FIXTURE_ROOT
# Normalize checkout aliases such as macOS /var without placing files in system temp.
$env:AGENT_TEMPDIRECTORY = $repoRoot
try { $fixtureBase = Get-PSRuntimeScratchRoot }
finally { $env:AGENT_TEMPDIRECTORY = $originalAgentTemp }
$scratch = Join-Path $fixtureBase "artifacts\ps-upgrade-fixtures-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch -Force
$groups = 0

function Assert-UpgradeFixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Upgrade fixture failed: $Message" }
}

function Invoke-PSRuntimeProcess
{
    [CmdletBinding()]
    param([string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory,
        [hashtable]$Environment = @{}, [int]$TimeoutSeconds = 180)
    $workerIndex = [array]::IndexOf($Arguments, '-File') + 1
    Assert-UpgradeFixture ($workerIndex -gt 0 -and
        [IO.Path]::GetFileName($Arguments[$workerIndex]) -eq 'upgrade_worker.ps1') 'Only actual upgrade workers are wrapped.'
    $requestPath = $Arguments[[array]::IndexOf($Arguments, '-RequestPath') + 1]
    $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
    Assert-UpgradeFixture ($Environment.AZCLIPS_TELEMETRY_TARGET -eq 'local' -and
        $Environment.POWERSHELL_TELEMETRY_OPTOUT -eq '1' -and
        $Environment.DOTNET_CLI_TELEMETRY_OPTOUT -eq '1') 'Production workers explicitly receive telemetry isolation.'
    if ($request.operation -eq 'Cleanup')
    {
        Write-PSValidationJson -Path (Join-Path $script:fixtureRoot 'cleanup-request.json') -Value $request
    }
    $environmentCopy = @{}
    foreach ($key in $Environment.Keys) { $environmentCopy[$key] = $Environment[$key] }
    $environmentCopy.AZCLIPS_UPGRADE_FIXTURE_ROOT = $script:fixtureRoot
    $environmentCopy.AZCLIPS_UPGRADE_FIXTURE_SCENARIO = $script:currentScenario
    $environmentCopy.PSModulePath = (Join-Path $script:fixtureRoot 'installed') +
        [IO.Path]::PathSeparator + (Join-Path $PSHOME 'Modules')
    $wrapped = @('-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot 'PSUpgradeFixtureDriver.ps1'),
        '-WorkerPath', $Arguments[$workerIndex], '-RequestPath', $requestPath,
        '-ResultPath', $Arguments[[array]::IndexOf($Arguments, '-ResultPath') + 1])
    $process = & $script:realRuntimeProcess -FilePath $FilePath -Arguments $wrapped `
        -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -Environment $environmentCopy
    # Install real baseline bytes first, then inject bounded-process failure flags.
    # This exercises actual rollback without waiting for a three-minute timeout.
    if ($request.operation -eq 'Install' -and $request.moduleName -eq 'Az' -and $request.version -eq '0.1.2')
    {
        if ($script:currentScenario -eq 'timeout') { $process.TimedOut = $true }
        if ($script:currentScenario -eq 'output-limit') { $process.Truncated = $true }
    }
    return $process
}

function New-UpgradeFixturePackage
{
    param([string]$Name, [string]$Version, [string[]]$Dependencies, [string]$Gallery,
        [string]$Marker, [switch]$Source)
    $directory = Join-Path $script:fixtureRoot "packages\$Marker-$Name-$Version"
    $null = New-Item -ItemType Directory -Path $directory -Force
    $shortName = if ($Name -eq 'Az') { 'Az' } else { $Name.Substring(3) }
    $command = "Get-${shortName}UpgradeFixture"
    $required = @($Dependencies | ForEach-Object { @{ ModuleName = $_; RequiredVersion = '0.1.0' } })
    $moduleText = @"
[IO.File]::AppendAllText((Join-Path `$env:AZCLIPS_UPGRADE_FIXTURE_ROOT 'imports.jsonl'), ((@{ name = '$Name'; version = '$Version'; marker = '$Marker'; moduleBase = `$PSScriptRoot; pid = `$PID } | ConvertTo-Json -Compress) + [Environment]::NewLine))
function $command { '$Marker' }
Export-ModuleMember -Function $command
"@
    $exports = @($command)
    if ($Source -and $Name -eq 'Az.Compute')
    {
        if ($script:currentScenario -eq 'missing-command')
        {
            $moduleText = $moduleText.Replace("Export-ModuleMember -Function $command", 'Export-ModuleMember -Function @()')
            $exports = @()
        }
        if ($script:currentScenario -eq 'import-failure') { $moduleText = "throw 'Injected target module import failure.'`n$moduleText" }
    }
    Set-Content -LiteralPath (Join-Path $directory "$Name.psm1") -Value $moduleText
    New-ModuleManifest -Path (Join-Path $directory "$Name.psd1") -RootModule "$Name.psm1" `
        -ModuleVersion $Version -Author Fixture -Description 'Offline upgrade fixture' -PowerShellVersion '7.0' `
        -RequiredModules $required -FunctionsToExport $exports -CmdletsToExport @()
    Set-Content -LiteralPath (Join-Path $directory 'payload.txt') -Value "$Marker payload for $Name $Version"
    $dependenciesXml = @($Dependencies | ForEach-Object { "<dependency id='$_' version='0.1.0' />" }) -join ''
    Set-Content -LiteralPath (Join-Path $directory "$Name.nuspec") -Value @"
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
<metadata><id>$Name</id><version>$Version</version><authors>Fixture</authors>
<description>Offline upgrade fixture</description><dependencies>$dependenciesXml</dependencies></metadata>
</package>
"@
    Set-Content -LiteralPath (Join-Path $directory '[Content_Types].xml') -Value '<Types />'
    $null = New-Item -ItemType Directory -Path (Join-Path $directory '_rels') -Force
    Set-Content -LiteralPath (Join-Path $directory '_rels\.rels') -Value '<Relationships />'
    $path = Join-Path $Gallery "$Name.$Version.nupkg"
    [IO.Compression.ZipFile]::CreateFromDirectory($directory, $path)
    if ($Source)
    {
        $sourceDirectory = Join-Path $script:fixtureRoot "source\$shortName"
        $null = New-Item -ItemType Directory -Path $sourceDirectory -Force
        $sourceRequirements = @($Dependencies | ForEach-Object {
            "@{ ModuleName = '$_'; RequiredVersion = '0.1.0' }"
        }) -join ','
        Set-Content -LiteralPath (Join-Path $sourceDirectory "$shortName.psd1") -Value @"
@{ ModuleVersion = '$Version'; RequiredModules = @($sourceRequirements); CmdletsToExport = @('$command') }
"@
    }
}

function New-UpgradeFixture
{
    param([string]$Name)
    $script:currentScenario = $Name
    $script:fixtureRoot = Join-Path $scratch $Name
    $null = New-Item -ItemType Directory -Path $script:fixtureRoot
    Set-Content -LiteralPath (Join-Path $script:fixtureRoot '.upgrade-fixture') -Value 'private offline fixture'
    $baseline = Join-Path $script:fixtureRoot 'baseline\gallery'
    $target = Join-Path $script:fixtureRoot 'target'
    $manager = Join-Path $script:fixtureRoot 'installed\PowerShellGet\99.0.0'
    $null = New-Item -ItemType Directory -Path $baseline, $target, $manager,
        (Join-Path $script:fixtureRoot 'source'), (Join-Path $script:fixtureRoot 'work') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PSUpgradeFixturePowerShellGet.psm1') `
        -Destination (Join-Path $manager 'PowerShellGet.psm1')
    New-ModuleManifest -Path (Join-Path $manager 'PowerShellGet.psd1') -RootModule PowerShellGet.psm1 `
        -ModuleVersion '99.0.0' -FunctionsToExport @('Get-PSRepository', 'Register-PSRepository',
        'Unregister-PSRepository', 'Get-InstalledModule', 'Install-Module', 'Uninstall-Module') -CmdletsToExport @()
    New-UpgradeFixturePackage Az.Login '0.1.0' @() $baseline baseline
    New-UpgradeFixturePackage Az.Compute '0.1.0' @('Az.Login') $baseline baseline
    New-UpgradeFixturePackage Az '0.1.2' @('Az.Login', 'Az.Compute') $baseline baseline
    New-UpgradeFixturePackage Az.Accounts '0.1.0' @() $target target -Source
    New-UpgradeFixturePackage Az.Compute '0.1.0' @('Az.Accounts') $target target -Source
    New-UpgradeFixturePackage Az '0.1.3' @('Az.Accounts', 'Az.Compute') $target target -Source
    if ($Name -eq 'success')
    {
        New-UpgradeFixturePackage Az.Standalone '0.1.0' @('Az.Accounts') $target target -Source
    }
    Write-PSValidationJson -Path (Join-Path $script:fixtureRoot 'baseline\release.json') -Value @{
        schemaVersion = 1; version = '0.1.2'; targetVersion = '0.1.3'
    }
    # This is intentionally not the current helper's 7.6 runtime check: fixture
    # manifests support 7.0, and the suite tests lifecycle semantics on its real host.
    Set-Content -LiteralPath (Join-Path $baseline 'InstallModule.ps1') -Value @'
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RepositoryName,
    [Parameter(Mandatory)][string]$ModuleName,
    [Parameter(Mandatory)][string]$RequiredVersion,
    [Parameter(Mandatory)][ValidateSet('CurrentUser')][string]$Scope)
$ErrorActionPreference = 'Stop'
if (-not $env:AZCLIPS_UPGRADE_FIXTURE_ROOT) { throw 'Synthetic installer requires the private fixture manager.' }
$expectedManager = Join-Path $env:AZCLIPS_UPGRADE_FIXTURE_ROOT 'installed\PowerShellGet\99.0.0'
if ((Get-Command Install-Module).Module.ModuleBase -ne $expectedManager) { throw 'Unsafe synthetic installer manager.' }
@{ operation = 'Helper'; name = $ModuleName; version = $RequiredVersion; repository = $RepositoryName
    gallery = $PSScriptRoot; helperSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash } |
    ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $env:AZCLIPS_UPGRADE_FIXTURE_ROOT 'events.jsonl')
if (Get-PSRepository -Name $RepositoryName -ErrorAction Stop)
{
    Unregister-PSRepository -Name $RepositoryName -ErrorAction Stop
}
Register-PSRepository -Name $RepositoryName -SourceLocation $PSScriptRoot -PublishLocation $PSScriptRoot -InstallationPolicy Trusted
Install-Module -Name $ModuleName -RequiredVersion $RequiredVersion -Repository $RepositoryName -Scope $Scope -Force -AllowClobber -ErrorAction Stop
if ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -eq 'native-exit' -and $ModuleName -eq 'Az' -and $RequiredVersion -eq '0.1.2')
{
    [Console]::Error.WriteLine('Injected native installer process exit.')
    exit 43
}
'@
    $unrelated = Join-Path $script:fixtureRoot 'installed\Az.Compute\9.0.0'
    $null = New-Item -ItemType Directory -Path $unrelated -Force
    Set-Content -LiteralPath (Join-Path $unrelated 'sentinel.txt') -Value 'Preserve this unrelated installed version.'
    New-ModuleManifest -Path (Join-Path $unrelated 'Az.Compute.psd1') -ModuleVersion '9.0.0'
    $state = @{
        repositories = @{ ExistingGallery = @{ Name = 'ExistingGallery'; SourceLocation = (Join-Path $script:fixtureRoot 'existing-gallery') } }
        installed = @{ 'Az.Compute|9.0.0' = @{
            Name = 'Az.Compute'; Version = '9.0.0'; Repository = 'ExistingGallery'; InstalledLocation = $unrelated
        } }
    }
    if ($Name -in 'preexisting-exact', 'preexisting-directory')
    {
        $preexisting = Join-Path $script:fixtureRoot 'installed\Az.Accounts\0.1.0'
        $null = New-Item -ItemType Directory -Path $preexisting -Force
        Set-Content -LiteralPath (Join-Path $preexisting 'sentinel.txt') -Value 'Never mutate this exact version.'
        if ($Name -eq 'preexisting-exact')
        {
            $state.installed['Az.Accounts|0.1.0'] = @{
                Name = 'Az.Accounts'; Version = '0.1.0'; Repository = 'ExistingGallery'; InstalledLocation = $preexisting
            }
        }
    }
    Write-PSValidationJson -Path (Join-Path $script:fixtureRoot 'state.json') -Value $state
    $env:PSModulePath = (Join-Path $script:fixtureRoot 'installed') + [IO.Path]::PathSeparator + (Join-Path $PSHOME 'Modules')
    $env:AGENT_TEMPDIRECTORY = Join-Path $script:fixtureRoot 'work'
}

function Get-UpgradeFixtureHashes
{
    param([string[]]$Roots)
    @($Roots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -File | Sort-Object FullName |
            ForEach-Object { "$($_.FullName)=$((Get-FileHash -LiteralPath $_.FullName).Hash)" }
    }) -join "`n"
}

function Invoke-UpgradeFixture
{
    param([string]$Name, [string]$ExpectedError)
    New-UpgradeFixture $Name
    $baseline = Join-Path $script:fixtureRoot 'baseline'
    $target = Join-Path $script:fixtureRoot 'target'
    $source = Join-Path $script:fixtureRoot 'source'
    $reportRoot = Join-Path $script:fixtureRoot 'reports'
    $protectedRoots = @($baseline, $target, $source, (Join-Path $script:fixtureRoot 'installed\Az.Compute\9.0.0'))
    $before = Get-UpgradeFixtureHashes $protectedRoots
    $initialState = Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'state.json') -Raw | ConvertFrom-Json -AsHashtable
    $contracts = @(Get-PSDependencyContracts -SourcePsRoot $source -RepositoryRoot $target)
    $targetCount = if ($Name -eq 'success') { 4 } else { 3 }
    Assert-UpgradeFixture ($contracts.Count -eq $targetCount) 'Fixture target source/package dependency contracts must be real and valid.'
    $caught = $null
    $returned = $null
    $parameters = @{
        BaselineRoot = $baseline; RepositoryRoot = $target; SourcePsRoot = $source
        PwshPath = $pwsh; ReportRoot = $reportRoot; TimeoutSeconds = 30
    }
    if ($Name -eq 'success')
    {
        $parameters.BaselineRoot = '.\baseline'
        $parameters.RepositoryRoot = '.\target'
        $parameters.SourcePsRoot = '.\source'
        $parameters.ReportRoot = '.\reports'
    }
    Push-Location -LiteralPath $script:fixtureRoot
    try
    {
        if ($Name -eq 'success')
        {
            Assert-UpgradeFixture ((Get-Location).ProviderPath -ne [Environment]::CurrentDirectory) `
                'Relative input paths are resolved from PowerShell location, not the process working directory.'
        }
        $returned = Invoke-PSUpgradeValidation @parameters 6>$null
    }
    catch { $caught = $_ }
    finally
    {
        Pop-Location
    }
    Assert-UpgradeFixture (Test-Path -LiteralPath (Join-Path $reportRoot 'upgrade-tests.json')) "[$Name] Expected report was not retained: $caught"
    $report = Get-Content -LiteralPath (Join-Path $reportRoot 'upgrade-tests.json') -Raw | ConvertFrom-Json
    $state = Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'state.json') -Raw | ConvertFrom-Json -AsHashtable
    $events = @(Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'events.jsonl') |
        ForEach-Object { $_ | ConvertFrom-Json })
    Assert-UpgradeFixture ($before -ceq (Get-UpgradeFixtureHashes $protectedRoots)) "[$Name] Inputs and unrelated installed bytes remain unchanged."
    Assert-UpgradeFixture ($state.repositories.ExistingGallery.SourceLocation -eq $initialState.repositories.ExistingGallery.SourceLocation) 'Unrelated repository registration is preserved.'
    Assert-UpgradeFixture (($state.installed['Az.Compute|9.0.0'] | ConvertTo-Json -Compress) -ceq
        ($initialState.installed['Az.Compute|9.0.0'] | ConvertTo-Json -Compress)) 'Unrelated installed-module registry entry is unchanged.'
    Assert-UpgradeFixture (@(Get-ChildItem -LiteralPath (Join-Path $script:fixtureRoot 'work') -Force).Count -eq 0) 'The orchestrator removes its own scratch/gallery before fresh-install stages.'
    Assert-UpgradeFixture ($report.schemaVersion -eq 1 -and $report.finishedAtUtc -and
        $report.baseline.version -eq '0.1.2' -and $report.targetVersion -eq '0.1.3') 'Report records completed baseline/target provenance.'
    foreach ($case in $report.cases)
    {
        Assert-UpgradeFixture ($case.status -in 'Succeeded', 'Failed' -and $case.startedAtUtc -and $case.finishedAtUtc -and
            $null -ne $case.exitCode) 'Each executed operation retains terminal status, timestamps and exit code.'
    }
    $workers = @($events | Where-Object operation -eq Worker)
    Assert-UpgradeFixture (@($workers.pid | Sort-Object -Unique).Count -eq $workers.Count) 'Each real operation runs in a fresh worker process.'
    foreach ($worker in $workers)
    {
        Assert-UpgradeFixture ($worker.manager -eq (Join-Path $script:fixtureRoot 'installed\PowerShellGet\99.0.0') -and
            $worker.powerShellVersion -eq $PSVersionTable.PSVersion.ToString()) 'Every worker uses the private fake manager and the actual, unspoofed runtime.'
    }
    $installCases = @($report.cases | Where-Object operation -eq Install)
    foreach ($case in $installCases | Where-Object { $null -ne $_.data })
    {
        Assert-UpgradeFixture ($case.data.publisherCheckSkipped -is [bool] -and
            $case.data.publisherCheckSkipped -eq ($case.name -ne 'install-baseline')) 'Report distinguishes normal released publisher checks from unsigned pipeline artifact provenance validation.'
    }
    $managerInstalls = @($events | Where-Object operation -eq ManagerInstall)
    Assert-UpgradeFixture ($managerInstalls.Count -eq $installCases.Count) 'Every released-helper invocation reaches the isolated manager.'
    foreach ($install in $managerInstalls)
    {
        $expectedSkip = -not ($install.name -eq 'Az' -and $install.version -eq '0.1.2')
        Assert-UpgradeFixture ($install.skipPublisherCheck -eq $expectedSkip) 'Synthetic helper inherits SkipPublisherCheck only for target installs without an explicit helper argument.'
    }
    $restoredDefaults = @($events | Where-Object operation -eq DefaultsRestored)
    Assert-UpgradeFixture ($restoredDefaults.Count -eq $installCases.Count) 'Actual worker restores defaults after every successful or failing installer invocation.'
    foreach ($defaults in $restoredDefaults)
    {
        Assert-UpgradeFixture (-not $defaults.skipPublisherCheck -and $defaults.otherDefault -eq 'utf8' -and
            $defaults.skipKeyPresent -eq ($Name -ne 'target-installer-failure')) 'Worker preserves unrelated defaults and restores an absent or explicitly false publisher-check default.'
    }
    $mutationEvents = @($events | Where-Object operation -in 'Register', 'Install', 'Uninstall', 'Unregister')
    if ($Name -in 'preexisting-exact', 'preexisting-directory')
    {
        Assert-UpgradeFixture ($mutationEvents.Count -eq 0 -and $report.cases.Count -eq 1) 'Preflight rejects conflicts before any package or repository mutation.'
        Assert-UpgradeFixture ((Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'installed\Az.Accounts\0.1.0\sentinel.txt') -Raw).Trim() -eq 'Never mutate this exact version.') 'Preexisting exact or unregistered directory is untouched.'
    }
    else
    {
        Assert-UpgradeFixture (@($report.cases | Where-Object name -eq cleanup).Count -eq 1) 'Every attempted installation receives actual worker cleanup, including process failure.'
        Assert-UpgradeFixture ($state.installed.Count -eq 1) 'Cleanup removes all and only transaction-owned exact module registrations.'
        foreach ($identity in 'Az\0.1.2', 'Az\0.1.3', 'Az.Login\0.1.0', 'Az.Accounts\0.1.0', 'Az.Compute\0.1.0', 'Az.Standalone\0.1.0')
        {
            Assert-UpgradeFixture (-not (Test-Path -LiteralPath (Join-Path $script:fixtureRoot "installed\$identity"))) "Cleanup removes baseline-only/renamed and target directories: $identity."
        }
    }
    if ($Name -notin 'cleanup-error', 'cleanup-primary')
    {
        Assert-UpgradeFixture ($state.repositories.Count -eq 1 -and $report.cleanupErrors.Count -eq 0) 'Successful cleanup retains only unrelated registrations.'
    }
    else
    {
        Assert-UpgradeFixture ($report.cleanupErrors.Count -gt 0 -and
            ($report.cases | Where-Object name -eq cleanup).status -eq 'Failed') 'Cleanup errors fail the operation and remain in the report.'
    }
    if ($ExpectedError)
    {
        Assert-UpgradeFixture ($caught -and $caught.Exception.Message -match $ExpectedError -and
            $report.status -eq 'Failed' -and $report.message -match $ExpectedError) "[$Name] Preserve original failure '$ExpectedError'; got: $caught"
        Assert-UpgradeFixture (@($report.cases | Where-Object status -eq Failed).Count -ge 1) 'Failure is attached to an actual per-operation report case.'
        if ($Name -eq 'native-exit')
        {
            $nativeCase = $report.cases | Where-Object name -eq install-baseline
            Assert-UpgradeFixture ($nativeCase.exitCode -ne 0 -and
                $nativeCase.message -match 'installer.*43' -and
                $nativeCase.output -match 'Injected native installer process exit' -and
                $nativeCase.data.installerExitCode -eq 43) "Native installer exit code and stderr are retained: $($nativeCase | ConvertTo-Json -Depth 10 -Compress)"
        }
        if ($Name -eq 'cleanup-primary')
        {
            Assert-UpgradeFixture ($caught.Exception.Message -notmatch 'repository cleanup failure' -and
                ($report.cleanupErrors -join ' ') -match 'repository cleanup failure') 'Cleanup diagnostics do not replace the original payload failure.'
        }
    }
    else
    {
        Assert-UpgradeFixture (-not $caught -and $returned.status -eq 'Succeeded' -and $report.status -eq 'Succeeded') "Successful lifecycle returns and writes a passing report: $caught"
        $expectedCases = @('preflight', 'install-baseline', 'inspect-baseline', 'import-baseline',
            'install-target-Az.Accounts', 'install-target-Az.Compute', 'install-target-Az.Standalone',
            'install-target-Az', 'inspect-target', 'import-target', 'cleanup')
        Assert-UpgradeFixture (($report.cases.name -join ',') -eq ($expectedCases -join ',')) 'Report includes each real operation in dependency order.'
        $helpers = @($events | Where-Object operation -eq Helper)
        $registrations = @($events | Where-Object operation -eq Register)
        Assert-UpgradeFixture ($registrations.Count -eq 5 -and $helpers.Count -eq 5 -and
            @($helpers.repository | Sort-Object -Unique).Count -eq 1 -and
            @($helpers.gallery | Sort-Object -Unique).Count -eq 1 -and
            @($registrations.name | Sort-Object -Unique).Count -eq 1 -and
            @($registrations.gallery | Sort-Object -Unique).Count -eq 1) 'Historical helper re-registration retains the same repository name and already-used gallery for baseline and every target.'
        Assert-UpgradeFixture (@($events | Where-Object operation -eq Unregister).Count -eq 5) 'Target helpers replace the owned registration, and final cleanup unregisters the last registration.'
        $helperHash = (Get-FileHash -LiteralPath (Join-Path $baseline 'gallery\InstallModule.ps1')).Hash
        Assert-UpgradeFixture (@($helpers | Where-Object helperSha256 -ne $helperHash).Count -eq 0) 'Every install uses the bundled released helper bytes.'
        $explicit = @($events | Where-Object { $_.operation -eq 'Install' -and $_.explicit })
        Assert-UpgradeFixture (($explicit.name -join ',') -eq 'Az,Az.Accounts,Az.Compute,Az.Standalone,Az' -and
            ($explicit.version -join ',') -eq '0.1.2,0.1.0,0.1.0,0.1.0,0.1.3') 'Helper explicitly pins baseline, same-version children, out-of-rollup modules, and exact target rollup.'
        $compute = @($events | Where-Object { $_.operation -eq 'Install' -and $_.name -eq 'Az.Compute' })
        Assert-UpgradeFixture ($compute.Count -eq 2 -and $compute[1].force -and $compute[1].explicit -and
            $compute[1].previousPayloadSha256 -eq $compute[0].payloadSha256 -and
            $compute[1].payloadSha256 -ne $compute[0].payloadSha256) 'An equal-version Compute child is explicitly forced and changes real payload bytes.'
        Assert-UpgradeFixture ($compute[0].dependencies[0].Name -eq 'Az.Login' -and
            $compute[1].dependencies.Count -eq 1 -and $compute[1].dependencies[0].Name -eq 'Az.Accounts') 'Equal-version replacement retains only target dependency metadata.'
        Assert-UpgradeFixture ('Az.0.1.2.nupkg' -in $explicit[-1].galleryPackages -and
            'Az.0.1.3.nupkg' -in $explicit[-1].galleryPackages -and
            'Az.Accounts.0.1.0.nupkg' -notin $explicit[0].galleryPackages) 'Pipeline packages augment the already-used gallery; older Az remains when exact target is selected.'
        $targetInventory = ($report.cases | Where-Object name -eq inspect-target).data.modules
        foreach ($contract in $contracts)
        {
            $inventory = @($targetInventory | Where-Object name -eq $contract.Name)
            $payload = Get-PSUpgradePayload -PackagePath $contract.PackagePath
            Assert-UpgradeFixture ($inventory.Count -eq 1 -and $inventory[0].version -eq $contract.Version -and
                $inventory[0].packageSha256 -eq (Get-FileHash -LiteralPath $contract.PackagePath).Hash -and
                $inventory[0].verifiedFiles -eq $payload.Count -and $payload.Count -eq 3) 'Production payload comparison verifies every file, not merely installed versions.'
            Assert-UpgradeFixture ($inventory[0].installedLocation -eq (Join-Path $script:fixtureRoot "installed\$($contract.Name)\$($contract.Version)")) 'Installed payload provenance points to the private exact-version path.'
        }
        $imports = @(Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'imports.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
        Assert-UpgradeFixture (@($imports | Where-Object marker -eq baseline).Count -eq 3 -and
            @($imports | Where-Object marker -eq target).Count -eq $targetCount -and
            @($imports.pid | Sort-Object -Unique).Count -eq 2) 'Real baseline and target module bodies import in separate processes.'
        Assert-UpgradeFixture (($report.cases | Where-Object name -eq import-target).data.imported.Count -eq $targetCount -and
            'Az.Standalone' -notin @(Get-PSDependencyClosure -Name Az -Contracts $contracts).Name) 'Worker verifies actual target imports and exports, including packages outside the Az rollup closure.'
        $removed = @($events | Where-Object operation -eq Uninstall)
        Assert-UpgradeFixture ($removed.Count -eq 6 -and
            @($removed | Where-Object name -eq Az.Login).Count -eq 1 -and
            @($removed | Where-Object name -eq Az.Accounts).Count -eq 1) 'Cleanup removes old and new authentication names and deduplicates same-version identities.'
        $removedIdentities = @($removed | ForEach-Object { "$($_.name)|$($_.version)" })
        foreach ($edge in @(
            @('Az|0.1.2', 'Az.Login|0.1.0'), @('Az|0.1.2', 'Az.Compute|0.1.0'),
            @('Az|0.1.3', 'Az.Compute|0.1.0'), @('Az.Compute|0.1.0', 'Az.Accounts|0.1.0'),
            @('Az|0.1.3', 'Az.Accounts|0.1.0'), @('Az.Standalone|0.1.0', 'Az.Accounts|0.1.0')
        ))
        {
            Assert-UpgradeFixture ([array]::IndexOf($removedIdentities, $edge[0]) -lt
                [array]::IndexOf($removedIdentities, $edge[1])) 'Cleanup uninstalls dependents before their resolved dependencies across old/new closures.'
        }
        $cleanupRequest = Get-Content -LiteralPath (Join-Path $script:fixtureRoot 'cleanup-request.json') -Raw | ConvertFrom-Json
        $cleanupCompute = @($cleanupRequest.packages | Where-Object Name -eq 'Az.Compute')
        Assert-UpgradeFixture ($cleanupRequest.packages.Count -eq 6 -and $cleanupCompute.Count -eq 1 -and
            $cleanupCompute[0].Dependencies.Count -eq 1 -and $cleanupCompute[0].Dependencies[0].Name -eq 'Az.Accounts') 'Real cleanup request collapses repeated attempts using the latest same-version dependency contract.'
        $blocked = @($events | Where-Object operation -eq UninstallBlocked)
        Assert-UpgradeFixture ($blocked.Count -eq 1 -and $blocked[0].name -eq 'Az.Compute' -and
            (($blocked[0].dependents | Sort-Object) -join ',') -eq 'Az|0.1.2,Az|0.1.3') 'The private manager enforces both installed rollups as dependents before actual topological cleanup.'
    }
    Write-Host "PASS: upgrade fixture $Name"
    $script:groups++
}

try
{
    $cases = [ordered]@{
        success = $null
        'preexisting-exact' = 'disposable environment'
        'preexisting-directory' = 'Preexisting module version directory'
        'stale-payload' = 'Installed payload differs'
        'unexpected-file' = 'stale or unexpected file'
        'wrong-repository' = 'from the upgrade gallery'
        'missing-command' = 'does not export Get-ComputeUpgradeFixture'
        'import-failure' = 'Injected target module import failure'
        'installer-failure' = 'Injected released installer failure'
        'target-installer-failure' = 'Injected target installer failure'
        'native-exit' = "install-baseline.*failed"
        'timeout' = 'timed out'
        'no-result' = 'produced no result'
        'output-limit' = 'diagnostic output limit'
        'cleanup-error' = 'repository cleanup failure'
        'cleanup-primary' = 'Installed payload differs'
    }
    foreach ($name in $cases.Keys)
    {
        if (-not $Scenario -or $name -in $Scenario) { Invoke-UpgradeFixture $name $cases[$name] }
    }
    if (-not $Scenario -or 'report-protection' -in $Scenario)
    {
        New-UpgradeFixture report-protection
        $inputRoots = @('baseline', 'target', 'source', 'installed')
        foreach ($inputRoot in $inputRoots)
        {
            $existingReportRoot = Join-Path $script:fixtureRoot "$inputRoot\existing-reports"
            $null = New-Item -ItemType Directory -Path $existingReportRoot
            Set-Content -LiteralPath (Join-Path $existingReportRoot 'upgrade-tests.json') -Value 'Do not overwrite this input.'
        }
        $before = Get-UpgradeFixtureHashes @($inputRoots | ForEach-Object { Join-Path $script:fixtureRoot $_ })
        foreach ($inputRoot in $inputRoots)
        {
            foreach ($reportDirectory in 'reports', 'existing-reports')
            {
                $reportRoot = Join-Path $script:fixtureRoot "$inputRoot\$reportDirectory"
                $caught = $null
                try
                {
                    Invoke-PSUpgradeValidation -BaselineRoot (Join-Path $script:fixtureRoot 'baseline') `
                        -RepositoryRoot (Join-Path $script:fixtureRoot 'target') -SourcePsRoot (Join-Path $script:fixtureRoot 'source') `
                        -PwshPath $pwsh -ReportRoot $reportRoot | Out-Null
                }
                catch { $caught = $_ }
                Assert-UpgradeFixture ($caught.Exception.Message -match 'reports must be outside' -and
                    (Test-Path -LiteralPath $reportRoot) -eq ($reportDirectory -eq 'existing-reports')) "Report paths cannot mutate $inputRoot."
            }
        }
        Assert-UpgradeFixture ($before -ceq (Get-UpgradeFixtureHashes @($inputRoots | ForEach-Object { Join-Path $script:fixtureRoot $_ })) -and
            -not (Test-Path -LiteralPath (Join-Path $script:fixtureRoot 'events.jsonl'))) 'Protected reports fail before worker activity or input changes.'
        Write-Host 'PASS: report/input/module-path protection'
        $groups++
    }
    if (-not $Scenario -or 'payload-safety' -in $Scenario)
    {
        New-UpgradeFixture payload-safety
        $packagePath = Join-Path $script:fixtureRoot 'target\Az.Compute.0.1.0.nupkg'
        foreach ($entryName in '../outside.txt', 'payload.txt')
        {
            $modifiedPackage = Join-Path $script:fixtureRoot 'invalid.nupkg'
            Copy-Item -LiteralPath $packagePath -Destination $modifiedPackage -Force
            $archive = [IO.Compression.ZipFile]::Open($modifiedPackage, [IO.Compression.ZipArchiveMode]::Update)
            try
            {
                $writer = [IO.StreamWriter]::new($archive.CreateEntry($entryName).Open())
                try { $writer.Write('invalid payload') }
                finally { $writer.Dispose() }
            }
            finally { $archive.Dispose() }
            $caught = $null
            try { Get-PSUpgradePayload -PackagePath $modifiedPackage | Out-Null }
            catch { $caught = $_ }
            Assert-UpgradeFixture ($caught.Exception.Message -match 'Unsafe package entry|Duplicate package entry') 'Real archive inspection rejects traversal and duplicate payload entries.'
        }
        $baselineGallery = Join-Path $script:fixtureRoot 'baseline\gallery'
        Copy-Item -LiteralPath (Join-Path $script:fixtureRoot 'target\Az.0.1.3.nupkg') -Destination $baselineGallery
        $caught = $null
        try { Get-PSUpgradeGalleryPackages -GalleryRoot $baselineGallery | Out-Null }
        catch { $caught = $_ }
        Assert-UpgradeFixture ($caught.Exception.Message -match 'one version of each module') 'Released bundle inventory cannot contain ambiguous rollup versions.'
        Write-Host 'PASS: real archive payload and released-gallery identity safety'
        $groups++
    }
    if (-not $Scenario -or 'transport-metadata' -in $Scenario)
    {
        New-UpgradeFixture transport-metadata
        $package = Get-PSDependencyContracts -SourcePsRoot (Join-Path $script:fixtureRoot 'source') `
            -RepositoryRoot (Join-Path $script:fixtureRoot 'target') | Where-Object Name -eq 'Az.Compute'
        $realPayloads = @('package/real-payload.txt', '_rels/real-payload.txt')
        $transportEntries = @('[Content_Types].xml', '.signature.p7s', '_rels/.rels',
            'Az.Compute.nuspec', 'package/services/metadata/core-properties/fixture.psmdcp')
        $archive = [IO.Compression.ZipFile]::Open($package.PackagePath, [IO.Compression.ZipArchiveMode]::Update)
        try
        {
            foreach ($entryName in @('.signature.p7s', 'package/services/metadata/core-properties/fixture.psmdcp') + $realPayloads)
            {
                $writer = [IO.StreamWriter]::new($archive.CreateEntry($entryName).Open())
                try { $writer.Write("Fixture archive content: $entryName") }
                finally { $writer.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        $location = Join-Path $script:fixtureRoot 'installed\Az.Compute\0.1.0'
        [IO.Compression.ZipFile]::ExtractToDirectory($package.PackagePath, $location)
        foreach ($clientEntry in 'PSGetModuleInfo.xml', 'client.nuspec',
            'package/services/metadata/core-properties/client.psmdcp')
        {
            Set-Content -LiteralPath (Join-Path $location $clientEntry) -Value 'Retained client metadata'
        }
        $payload = Get-PSUpgradePayload -PackagePath $package.PackagePath
        foreach ($entryName in $transportEntries)
        {
            Assert-UpgradeFixture ((Test-PSUpgradeTransportEntry -Name $entryName) -and
                -not $payload.ContainsKey($entryName) -and
                (Test-Path -LiteralPath (Join-Path $location $entryName))) 'Legitimate retained transport files are excluded from archive payload hashes.'
        }
        $inventory = Assert-PSUpgradeInstalledPayload -Package $package -InstalledLocation $location
        Assert-UpgradeFixture ($payload.Count -eq 5 -and $inventory.verifiedFiles -eq 5) 'Installed transport and generated manager metadata are accepted without hiding the two real nested payloads.'
        foreach ($entryName in $realPayloads)
        {
            $path = Join-Path $location $entryName
            Assert-UpgradeFixture (-not (Test-PSUpgradeTransportEntry -Name $entryName) -and
                $payload.ContainsKey($entryName) -and
                $payload[$entryName] -ceq (Get-FileHash -LiteralPath $path).Hash) "Real nested payload is hashed: $entryName."
            $original = [IO.File]::ReadAllBytes($path)
            $caught = $null
            try
            {
                [IO.File]::WriteAllText($path, 'Stale nested payload')
                try { Assert-PSUpgradeInstalledPayload -Package $package -InstalledLocation $location | Out-Null }
                catch { $caught = $_ }
            }
            finally { [IO.File]::WriteAllBytes($path, $original) }
            Assert-UpgradeFixture ($caught.Exception.Message -match 'Installed payload differs' -and
                $caught.Exception.Message.Contains($entryName)) "Stale nested payload is rejected: $entryName."
        }
        foreach ($entryName in 'package/unexpected.txt', '_rels/unexpected.txt')
        {
            $path = Join-Path $location $entryName
            Set-Content -LiteralPath $path -Value 'Unexpected client leftover'
            $caught = $null
            try { Assert-PSUpgradeInstalledPayload -Package $package -InstalledLocation $location | Out-Null }
            catch { $caught = $_ }
            finally { Remove-Item -LiteralPath $path -Force }
            Assert-UpgradeFixture ($caught.Exception.Message -match 'stale or unexpected file' -and
                $caught.Exception.Message.Contains($entryName)) "The installed extra-file scan does not ignore arbitrary directory contents: $entryName."
        }
        Write-Host 'PASS: narrow symmetric transport exclusions and real nested-payload hashes'
        $groups++
    }
    if (-not $Scenario -or 'manager-safety' -in $Scenario)
    {
        New-UpgradeFixture manager-safety
        $managerPath = (Join-Path $PSScriptRoot 'PSUpgradeFixturePowerShellGet.psm1').Replace("'", "''")
        $command = @"
`$ErrorActionPreference = 'Stop'
try { Import-Module -Name '$managerPath' -Force -ErrorAction Stop }
catch { [Console]::Error.WriteLine(`$_.Exception.Message); exit 41 }
throw 'Fixture manager unexpectedly accepted an unset private root.'
"@
        $process = & $script:realRuntimeProcess -FilePath $pwsh -WorkingDirectory $script:fixtureRoot `
            -Arguments @('-NoProfile', '-NonInteractive', '-EncodedCommand', (ConvertTo-PSRuntimeEncodedCommand $command)) `
            -Environment @{ AZCLIPS_UPGRADE_FIXTURE_ROOT = ''; PSModulePath = (Join-Path $PSHOME 'Modules') } -TimeoutSeconds 30
        Assert-UpgradeFixture ($process.ExitCode -eq 41 -and -not $process.TimedOut -and
            $process.Output -match 'Private upgrade fixture root is required') 'Fake PowerShellGet fails closed before exporting manager commands without its private root.'
        Write-Host 'PASS: private-manager fail-closed safety'
        $groups++
    }
    Assert-UpgradeFixture ($groups -gt 0) 'At least one requested fixture group must run.'
}
finally
{
    $env:PSModulePath = $originalModulePath
    $env:AGENT_TEMPDIRECTORY = $originalAgentTemp
    $env:AZCLIPS_UPGRADE_FIXTURE_ROOT = $originalFixtureRoot
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction Stop
}
Write-Host "PASS: $groups offline PowerShell upgrade fixture groups."
