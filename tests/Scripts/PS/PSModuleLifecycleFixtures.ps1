<#
.SYNOPSIS
Exercises the actual Az installation and uninstall scripts without touching user modules or repositories.

.DESCRIPTION
Runs each scenario in a fresh pwsh process with local dummy manifests/packages and
fake module, repository, help, and native-child commands. No PowerShellGet, network,
Pester, or package installation is needed. Scratch files use the OS temporary
directory and are removed after the run; the checkout and gallery may be read-only.

.EXAMPLE
pwsh -NoProfile -File tests\Scripts\PS\PSModuleLifecycleFixtures.ps1

.EXAMPLE
pwsh -NoProfile -File tests\Scripts\PS\PSModuleLifecycleFixtures.ps1 -Scenario artifacts-newer-versions -FixtureRoot C:\fixture

Integration smoke scenario: installs and uninstalls exact fake versions while preserving unrelated newer versions.
Reports remain under FixtureRoot\reports after the child exits. For a disposable Linux container, mount a writable
host report directory at /fixture/reports and pass -FixtureRoot /fixture; keep the repository mount read-only.
#>
[CmdletBinding()]
param(
    [string]$Scenario,
    [string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).ProviderPath
$installationScript = Join-Path $repoRoot 'devops\scripts\packaging\powershell\test_ps_installation.ps1'
$uninstallScript = Join-Path $repoRoot 'devops\scripts\packaging\powershell\test_ps_uninstall.ps1'
$definitionsScript = Join-Path $repoRoot 'devops\scripts\packaging\powershell\shared\module_definitions.ps1'

function Assert-Fixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Fixture assertion failed: $Message" }
}

function Add-LifecycleEvent
{
    param([string]$Operation, [string]$Name, [string]$Version)
    $global:Lifecycle.Events.Add([pscustomobject]@{
        Operation = $Operation
        Name = $Name
        Version = $Version
        Directory = (Get-Location).ProviderPath
        ProcessDirectory = [Environment]::CurrentDirectory
    })
}

function New-FakePackage
{
    param([string]$Name, [string]$Version, [bool]$Registered = $true)
    $moduleRoot = Join-Path $FixtureRoot "installed\$Name\$Version"
    New-Item -ItemType Directory -Path (Join-Path $moduleRoot 'en-US') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $moduleRoot "$Name.psd1") -Value "@{ ModuleVersion = '$Version'; RootModule = '$Name.dll' }"
    Set-Content -LiteralPath (Join-Path $moduleRoot "$Name.dll") -Value "Fake binary for $Name $Version"
    Set-Content -LiteralPath (Join-Path $moduleRoot "en-US\$Name.dll-Help.xml") -Value '<help>fixture external help</help>'
    if ($Version -eq '9.0.0')
    {
        Set-Content -LiteralPath (Join-Path $moduleRoot 'unrelated-sentinel.txt') -Value 'never capture a preexisting version'
    }
    $exports = @{}
    foreach ($cmdlet in $global:Lifecycle.Definitions[$Name].CmdletsToExport)
    {
        $exports[$cmdlet] = [pscustomobject]@{ Name = $cmdlet }
    }
    [pscustomobject]@{
        Name = $Name
        Version = [version]$Version
        Registered = $Registered
        InstalledLocation = $moduleRoot
        ModuleBase = $moduleRoot
        Path = Join-Path $moduleRoot "$Name.psd1"
        ExportedCommands = $exports
    }
}

function Enable-LifecycleFakes
{
    # Only stock shell modules are loaded; disabling autoload prevents fallback to PowerShellGet.
    Import-Module Microsoft.PowerShell.Management, Microsoft.PowerShell.Utility -ErrorAction Stop
    $global:PSModuleAutoLoadingPreference = 'None'

    function global:Get-PSRepository
    {
        [CmdletBinding()]
        param([string]$Name)
        $global:Lifecycle.Repositories.Values | Where-Object { -not $Name -or $_.Name -eq $Name }
    }

    function global:Register-PSRepository
    {
        [CmdletBinding()]
        param($Name, $SourceLocation, $PublishLocation, $InstallationPolicy, $PackageManagementProvider)
        Add-LifecycleEvent Register $Name
        Assert-Fixture ([IO.Path]::IsPathRooted($SourceLocation)) 'Repository source must be absolute.'
        Assert-Fixture ($SourceLocation -eq $global:Lifecycle.Gallery) 'Repository source must resolve before changing directories.'
        Assert-Fixture ($PublishLocation -eq $SourceLocation) 'Publish location must resolve to the same gallery.'
        Assert-Fixture ((Get-Location).ProviderPath -ne $FixtureRoot) 'Registration must not run in the caller directory.'
        Assert-Fixture ((Get-Location).ProviderPath -ne $SourceLocation) 'Registration must not run in the gallery root.'
        Assert-Fixture (-not (Get-Location).ProviderPath.StartsWith($SourceLocation + [IO.Path]::DirectorySeparatorChar)) 'Read-only galleries must not contain registration scratch directories.'
        Assert-Fixture (-not (Get-Location).ProviderPath.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar)) 'A read-only checkout must not contain registration scratch directories.'
        Assert-Fixture ([Environment]::CurrentDirectory -eq (Get-Location).ProviderPath) 'Registration must also isolate the .NET process current directory.'
        Set-Content -LiteralPath (Join-Path (Get-Location) 'NuGet.config') -Value 'fake registration config'
        [IO.File]::WriteAllText('NuGet.config', 'fake .NET provider config')
        if ($Scenario -eq 'registration-failure') { throw 'Injected repository registration failure.' }
        $global:Lifecycle.Repositories[$Name] = [pscustomobject]@{ Name = $Name; SourceLocation = $SourceLocation }
    }

    function global:Unregister-PSRepository
    {
        [CmdletBinding()]
        param($Name)
        Add-LifecycleEvent Unregister $Name
        if ($Scenario -in 'unregistration-failure', 'cleanup-primary')
        {
            throw 'Injected repository unregistration failure.'
        }
        $global:Lifecycle.Repositories.Remove($Name)
    }

    function global:Get-InstalledModule
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Installed-module queries must pin RequiredVersion.'
        Add-LifecycleEvent Query $Name $RequiredVersion
        if ($Name -eq 'Az.Compute' -and
            ($Scenario -eq 'preflight-query-failure' -or
             ($Scenario -eq 'cleanup-query-failure' -and $global:Lifecycle.ChildFailed)))
        {
            $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new(
                [InvalidOperationException]::new('Injected module query failure.'), 'InjectedQueryFailure',
                [System.Management.Automation.ErrorCategory]::PermissionDenied, $Name))
        }
        $package = $global:Lifecycle.Packages["$Name|$RequiredVersion"]
        if ($package -and $package.Registered) { return $package }
        Write-Error -Message "No exact installed version: $Name $RequiredVersion" -ErrorId NoMatchFound -Category ObjectNotFound
    }

    function global:Get-Module
    {
        [CmdletBinding()]
        param([switch]$ListAvailable, [string]$Name)
        Assert-Fixture $ListAvailable.IsPresent 'Lifecycle discovery must query available modules.'
        if ($ArtifactScenario -eq 'artifacts-available-query-failure' -and $Name -eq 'Az.Compute' -and $global:Lifecycle.ChildFailed)
        {
            throw 'Injected available-module lookup failure.'
        }
        $global:Lifecycle.Packages.Values | Where-Object { $_.Name -eq $Name }
    }

    function global:Find-Module
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion, [string]$Repository)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Find-Module must pin RequiredVersion.'
        Add-LifecycleEvent Find $Name $RequiredVersion
        [pscustomobject]@{ Name = $Name; Version = $RequiredVersion }
    }

    function global:Install-Module
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion, $Repository, $Scope,
            [switch]$Force, [switch]$AllowClobber, [switch]$SkipPublisherCheck)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Install-Module must pin RequiredVersion.'
        Assert-Fixture ($Scope -eq 'CurrentUser') 'Installation must retain the CurrentUser default.'
        Add-LifecycleEvent Install $Name $RequiredVersion
        if ($Name -eq 'Az.Compute' -and $Scenario -eq 'install-absent-failure')
        {
            throw 'Injected installation failure before adding Az.Compute.'
        }
        $global:Lifecycle.Packages["$Name|$RequiredVersion"] = New-FakePackage $Name $RequiredVersion
        $package = $global:Lifecycle.Packages["$Name|$RequiredVersion"]
        if ($Name -eq 'Az.Compute')
        {
            if ($ArtifactScenario -eq 'artifacts-partial-install')
            {
                $package.Registered = $false
                throw 'Injected unregistered partial installation failure.'
            }
            if ($ArtifactScenario -in 'artifacts-capture-failure', 'artifacts-capture-primary')
            {
                Set-Content -LiteralPath $package.Path -Value "@{ ModuleVersion = '9.0.0' }"
            }
            if ($ArtifactScenario -eq 'artifacts-scope-mismatch')
            {
                $package.InstalledLocation = Join-Path $FixtureRoot 'unrelated-home'
            }
            if ($ArtifactScenario -eq 'artifacts-link-capture')
            {
                $linkType = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'Junction' } else { 'SymbolicLink' }
                New-Item -ItemType $linkType -Path (Join-Path $package.ModuleBase 'outside') -Value (Join-Path $FixtureRoot 'unrelated-home') | Out-Null
            }
        }
        if ($Name -eq 'Az.Compute' -and $Scenario -eq 'install-failure')
        {
            throw 'Injected installation failure after adding Az.Compute.'
        }
    }

    function global:Uninstall-Module
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion, [switch]$Force)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Uninstall-Module must pin RequiredVersion.'
        Add-LifecycleEvent Uninstall $Name $RequiredVersion
        if ($Scenario -eq 'removal-failures' -and $Name -in 'Az.Resources', 'Az.Compute')
        {
            throw "Injected removal failure for $Name."
        }
        if ($ArtifactScenario -eq 'artifacts-rollback-removal-failure')
        {
            throw "Injected rollback removal failure for $Name."
        }
        Assert-Fixture ($global:Lifecycle.Packages.ContainsKey("$Name|$RequiredVersion")) 'Absent versions must not be uninstalled during rollback.'
        $package = $global:Lifecycle.Packages["$Name|$RequiredVersion"]
        Microsoft.PowerShell.Management\Remove-Item -LiteralPath $package.ModuleBase -Recurse -Force
        $global:Lifecycle.Packages.Remove("$Name|$RequiredVersion")
    }

    function global:Import-Module
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion, [switch]$Force, [switch]$PassThru)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Import-Module must pin RequiredVersion.'
        Add-LifecycleEvent Import $Name $RequiredVersion
        $failureModule = if ($Scenario -eq 'removal-failures') { 'Az.Resources' } elseif ($Scenario -eq 'first-import-failure') { 'Az.Login' } else { 'Az.Compute' }
        if ($Name -eq $failureModule -and $Scenario -in 'first-import-failure', 'import-failure', 'removal-failures', 'cleanup-primary', 'cleanup-query-failure', 'failure-newer-versions')
        {
            throw "Injected import failure for $Name."
        }
        $package = $global:Lifecycle.Packages["$Name|$RequiredVersion"]
        if ($package) { return $package }
        Write-Error -Message "The exact version is not importable: $Name $RequiredVersion" -ErrorId Modules_ModuleNotFound
    }

    function global:Remove-Module
    {
        [CmdletBinding()]
        param([string]$Name, [switch]$Force)
        Add-LifecycleEvent RemoveLoaded $Name
    }

    function global:Remove-Item
    {
        [CmdletBinding()]
        param([string]$LiteralPath, [switch]$Recurse, [switch]$Force)
        if ($Scenario -eq 'directory-cleanup-failure')
        {
            throw 'Injected working directory removal failure.'
        }
        Microsoft.PowerShell.Management\Remove-Item @PSBoundParameters
    }

    function global:Get-Command
    {
        [CmdletBinding()]
        param([string]$Name, [string]$Module, [string]$CommandType)
        if ($Module)
        {
            $global:Lifecycle.Definitions[$Module].CmdletsToExport |
                ForEach-Object { [pscustomobject]@{ Name = $_ } }
        }
        else
        {
            Microsoft.PowerShell.Core\Get-Command -Name $Name -ErrorAction Stop
        }
    }

    function global:Get-Help
    {
        [CmdletBinding()]
        param([string]$Name)
        Add-LifecycleEvent Help $Name
        $code = if ($Scenario -eq 'help-failure' -and $Name -eq 'Get-ComputeFixture') { '' } else { "$Name -Example" }
        [pscustomobject]@{ Examples = @{ Example = @(@{ code = $code }) } }
    }

    function global:Invoke-FakePwsh
    {
        param([switch]$NoProfile, [scriptblock]$Command, [Alias('Args')][object[]]$ArgumentList)
        # Execute the real child validation body against fakes, including version/help assertions.
        try
        {
            & $Command @ArgumentList
            $global:LASTEXITCODE = 0
        }
        catch
        {
            $global:Lifecycle.ChildFailed = $true
            Write-Host "Fake subprocess failure: $($_.Exception.Message)"
            $global:LASTEXITCODE = 51
        }
    }
}

function Invoke-LifecycleScenario
{
    $sourceRoot = Join-Path $FixtureRoot 'source'
    $galleryRoot = Join-Path $FixtureRoot 'gallery'
    $processRoot = Join-Path $FixtureRoot 'process-cwd'
    $homeRoot = Join-Path $FixtureRoot 'unrelated-home'
    $reportRoot = Join-Path $FixtureRoot 'reports'
    New-Item -ItemType Directory -Path $sourceRoot, $galleryRoot, $processRoot, $homeRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $homeRoot 'unrelated-sentinel.txt') -Value 'fake unrelated home state'
    Set-Content -LiteralPath (Join-Path $homeRoot 'credentials.json') -Value '{"fixture":"never copy"}'
    Set-Content -LiteralPath (Join-Path $FixtureRoot 'NuGet.config') -Value 'caller config'
    Set-Content -LiteralPath (Join-Path $galleryRoot 'NuGet.config') -Value 'gallery config'
    Set-Content -LiteralPath (Join-Path $processRoot 'NuGet.config') -Value 'process caller config'
    $authenticationModule = if ($Scenario -eq 'accounts-source') { 'Accounts' } else { 'Login' }
    $moduleNames = @('Az', 'Resources', 'Compute', $authenticationModule)
    if ($Scenario -eq 'accounts-source') { $moduleNames += 'Aardvark' }
    foreach ($name in $moduleNames)
    {
        $moduleDirectory = New-Item -ItemType Directory -Path (Join-Path $sourceRoot $name)
        $cmdlets = if ($name -eq 'Az') { '@()' } else { "@('Get-${name}Fixture')" }
        Set-Content -LiteralPath (Join-Path $moduleDirectory.FullName "$name.psd1") `
            -Value "@{ ModuleVersion = '1.2.3'; CmdletsToExport = $cmdlets }"
    }

    . $definitionsScript
    if ($Scenario -eq 'default-source')
    {
        $sourceRoot = Join-Path $repoRoot 'src\PS'
        $authenticationModule = if (Test-Path -LiteralPath (Join-Path $sourceRoot 'Accounts\Accounts.psd1') -PathType Leaf) { 'Accounts' } else { 'Login' }
    }
    $definitions = @(Get-AzModuleDefinitions -SourcePsRoot $sourceRoot)
    Assert-Fixture ($definitions[0].PackageName -eq "Az.$authenticationModule" -and $definitions[-1].PackageName -eq 'Az') "Canonical discovery must place $authenticationModule first and Az last."
    if ($Scenario -ne 'default-source')
    {
        $expectedOrder = if ($Scenario -eq 'accounts-source') { 'Az.Accounts,Az.Aardvark,Az.Compute,Az.Resources,Az' } else { 'Az.Login,Az.Compute,Az.Resources,Az' }
        Assert-Fixture (($definitions.PackageName -join ',') -eq $expectedOrder) 'Canonical middle-module order must use manifest paths.'
        $compute = $definitions | Where-Object SourceModule -eq 'Compute'
        Assert-Fixture ($compute.Version -eq '1.2.3' -and $compute.CmdletsToExport[0] -eq 'Get-ComputeFixture') 'Discovery must preserve versions and exported cmdlets.'
    }
    $global:Lifecycle = @{
        Definitions = @{}
        Packages = @{}
        Repositories = @{ ExistingGallery = [pscustomobject]@{ Name = 'ExistingGallery'; SourceLocation = 'unchanged' } }
        Events = [System.Collections.Generic.List[object]]::new()
        ChildFailed = $false
        Gallery = $galleryRoot
    }
    foreach ($definition in $definitions)
    {
        $global:Lifecycle.Definitions[$definition.PackageName] = $definition
        Set-Content -LiteralPath (Join-Path $galleryRoot "$($definition.PackageName).$($definition.Version).nupkg") -Value 'dummy local package'
    }
    if ($Scenario -in 'preexisting-exact', 'preexisting-available')
    {
        $global:Lifecycle.Packages['Az|1.2.3'] = New-FakePackage Az '1.2.3' ($Scenario -eq 'preexisting-exact')
    }
    if ($Scenario -eq 'repository-conflict')
    {
        $global:Lifecycle.Repositories.LocalAzclips = [pscustomobject]@{ Name = 'LocalAzclips'; SourceLocation = 'original repository' }
    }
    if ($Scenario -in 'newer-versions', 'failure-newer-versions')
    {
        foreach ($definition in $definitions)
        {
            $name = $definition.PackageName
            $global:Lifecycle.Packages["$name|9.0.0"] = New-FakePackage $name '9.0.0'
        }
    }
    if ($Scenario -eq 'strict-uninstall-missing')
    {
        foreach ($definition in $definitions | Where-Object PackageName -ne Az)
        {
            $name = $definition.PackageName
            $global:Lifecycle.Packages["$name|1.2.3"] = New-FakePackage $name '1.2.3'
        }
    }
    if ($Scenario -eq 'strict-uninstall-partial')
    {
        foreach ($definition in $definitions | Where-Object PackageName -ne 'Az.Compute')
        {
            $name = $definition.PackageName
            $global:Lifecycle.Packages["$name|1.2.3"] = New-FakePackage $name '1.2.3'
        }
    }
    if ($ArtifactScenario -in 'artifacts-uninstall-capture-primary', 'artifacts-uninstall-capture-only')
    {
        New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $reportRoot 'inventories') -Value 'fixture blocks inventory directory'
    }
    if ($ArtifactScenario -eq 'artifacts-uninstall-capture-only')
    {
        foreach ($definition in $definitions)
        {
            $name = $definition.PackageName
            $global:Lifecycle.Packages["$name|1.2.3"] = New-FakePackage $name '1.2.3'
        }
    }
    if ($Scenario -in 'empty-install', 'empty-uninstall')
    {
        Get-ChildItem -LiteralPath $sourceRoot -Filter '*.psd1' -Recurse | Remove-Item
    }
    if ($Scenario -eq 'invalid-definition')
    {
        Set-Content -LiteralPath (Join-Path $sourceRoot 'Az\Az.psd1') -Value '@{ CmdletsToExport = @() }'
    }

    Enable-LifecycleFakes
    $env:PSModulePath = (Join-Path $FixtureRoot 'installed') + [IO.Path]::PathSeparator + $env:PSModulePath
    $caught = $null
    $output = [System.Collections.Generic.List[string]]::new()
    Push-Location -LiteralPath $FixtureRoot
    $originalProcessDirectory = [Environment]::CurrentDirectory
    try
    {
        # Deliberately start with different PowerShell and .NET working directories.
        [Environment]::CurrentDirectory = $processRoot
        $parameters = @{ SourcePsRoot = '.\source'; PwshPath = 'Invoke-FakePwsh' }
        if ($ArtifactScenario)
        {
            if ($ArtifactScenario -eq 'artifacts-report-in-source') { $reportRoot = Join-Path $sourceRoot 'reports' }
            if ($ArtifactScenario -eq 'artifacts-report-in-gallery') { $reportRoot = Join-Path $galleryRoot 'reports' }
            if ($ArtifactScenario -eq 'artifacts-report-in-module') { $reportRoot = Join-Path $FixtureRoot 'installed\Az.Compute\1.2.3\reports' }
            $parameters.ReportRoot = $reportRoot
        }
        try
        {
            if ($Scenario -in 'strict-uninstall-missing', 'strict-uninstall-partial', 'uninstall-capture-only', 'empty-uninstall')
            {
                & $uninstallScript @parameters *>&1 | ForEach-Object { $output.Add($_.ToString()) }
            }
            else
            {
                if ($Scenario -eq 'default-source') { $parameters.Remove('SourcePsRoot') }
                & $installationScript -RepositoryRoot '.\gallery' @parameters *>&1 |
                    ForEach-Object { $output.Add($_.ToString()) }

                Assert-Fixture (@($global:Lifecycle.Events | Where-Object Operation -eq Uninstall).Count -eq 0) 'Successful installation must leave packages for a separate uninstall phase.'
                foreach ($definition in $definitions)
                {
                    Assert-Fixture ($global:Lifecycle.Packages.ContainsKey("$($definition.PackageName)|$($definition.Version)")) 'Successful installation must retain every exact target version.'
                }
                if ($ArtifactScenario)
                {
                    foreach ($definition in $definitions)
                    {
                        $snapshot = Join-Path $reportRoot "installed-modules\$($definition.PackageName)\$($definition.Version)"
                        Assert-Fixture (Test-Path -LiteralPath (Join-Path $snapshot "$($definition.PackageName).dll")) 'Every installed binary must be captured before a separate uninstall.'
                    }
                }
                if ($Scenario -in 'newer-versions', 'default-source', 'accounts-source')
                {
                    & $uninstallScript @parameters *>&1 | ForEach-Object { $output.Add($_.ToString()) }
                }
            }
        }
        catch { $caught = $_ }

        Assert-Fixture ((Get-Location).ProviderPath -eq $FixtureRoot) 'The actual script must restore the caller directory on every path.'
        Assert-Fixture ([Environment]::CurrentDirectory -eq $processRoot) 'The actual script must independently restore the process current directory on every path.'
        Assert-Fixture ((Get-Content -LiteralPath (Join-Path $FixtureRoot 'NuGet.config') -Raw).Trim() -eq 'caller config') 'Caller NuGet.config must remain unchanged.'
        Assert-Fixture ((Get-Content -LiteralPath (Join-Path $galleryRoot 'NuGet.config') -Raw).Trim() -eq 'gallery config') 'Gallery NuGet.config must remain unchanged.'
        Assert-Fixture ((Get-Content -LiteralPath (Join-Path $processRoot 'NuGet.config') -Raw).Trim() -eq 'process caller config') 'The .NET caller NuGet.config must remain unchanged.'
        Assert-Fixture ($global:Lifecycle.Repositories.ExistingGallery.SourceLocation -eq 'unchanged') 'Unrelated repository registrations must be preserved.'
        $events = @($global:Lifecycle.Events.ToArray())
        $installed = @($events | Where-Object Operation -eq Install)
        $removed = @($events | Where-Object Operation -eq Uninstall)
        $registered = @($events | Where-Object Operation -eq Register)
        foreach ($registration in $registered)
        {
            Assert-Fixture ((Test-Path -LiteralPath $registration.Directory) -eq ($Scenario -eq 'directory-cleanup-failure')) 'Only this run''s scratch directory must be removed unless its deletion fails.'
        }
        $unregistered = @($events | Where-Object Operation -eq Unregister)
        $mutations = @($events | Where-Object { $_.Operation -in 'Register', 'Unregister', 'Install', 'Uninstall' })
        $joined = $output -join "`n"
        foreach ($event in $events | Where-Object { $_.Operation -in 'Find', 'Install', 'Unregister' })
        {
            Assert-Fixture ($event.Directory -eq $registered[0].Directory) 'Repository operations must stay in the isolated working directory.'
            Assert-Fixture ($event.ProcessDirectory -eq $registered[0].Directory) 'Repository operations must keep the .NET process directory isolated too.'
        }
        if ($registered.Count -gt 0 -and $Scenario -ne 'registration-failure')
        {
            Assert-Fixture ($unregistered.Count -eq 1) 'The script must unregister its owned registration exactly once.'
        }
        if ($Scenario -notin 'repository-conflict', 'unregistration-failure', 'cleanup-primary')
        {
            Assert-Fixture (-not $global:Lifecycle.Repositories.ContainsKey('LocalAzclips')) 'No owned repository registration may remain after successful teardown.'
        }

        switch ($Scenario)
        {
            { $_ -in 'success', 'newer-versions', 'default-source', 'accounts-source' } {
                Assert-Fixture ($null -eq $caught) "Success path must not throw: $caught`n$joined"
                Assert-Fixture (($installed.Name -join ',') -eq ($definitions.PackageName -join ',')) 'Installation must use canonical discovery order.'
                $firstMutation = @($events | Where-Object Operation -eq Register)[0]
                $queriesBeforeMutation = @($events | Select-Object -First ([array]::IndexOf($events, $firstMutation)) | Where-Object Operation -eq Query)
                $expectedQueriesPerModule = if ($ArtifactScenario) { 2 } else { 1 }
                Assert-Fixture ($queriesBeforeMutation.Count -eq ($expectedQueriesPerModule * $definitions.Count)) 'Inventory and preflight must query all exact versions before repository registration.'
                foreach ($definition in $definitions)
                {
                    Assert-Fixture (@($queriesBeforeMutation | Where-Object { $_.Name -eq $definition.PackageName -and $_.Version -eq $definition.Version }).Count -eq $expectedQueriesPerModule) 'Each target must receive its exact preflight query, independently of artifact queries.'
                }
                if ($_ -ne 'success')
                {
                    $reverse = @($definitions.PackageName)
                    [array]::Reverse($reverse)
                    Assert-Fixture (($removed.Name -join ',') -eq ($reverse -join ',')) 'Strict uninstall must reverse the exact canonical list.'
                    Assert-Fixture (@($events | Where-Object { $_.Operation -eq 'Import' }).Count -eq (2 * $definitions.Count)) 'Both real child validation bodies must execute.'
                    $expectedRemaining = if ($_ -eq 'newer-versions') { $definitions.Count } else { 0 }
                    Assert-Fixture ($global:Lifecycle.Packages.Count -eq $expectedRemaining) 'Strict uninstall must remove only the target versions.'
                    foreach ($package in $global:Lifecycle.Packages.Values)
                    {
                        Assert-Fixture ($package.Version -eq [version]'9.0.0') 'Unrelated newer versions must survive installation and uninstall.'
                    }
                }
            }
            { $_ -in 'import-failure', 'help-failure', 'install-failure', 'cleanup-primary' } {
                Assert-Fixture (($installed.Name -join ',') -eq 'Az.Login,Az.Compute') 'Only attempted modules may be installed.'
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Compute,Az.Login') 'Failure must reverse only attempted modules.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'Failure must remove attempted package versions.'
            }
            'first-import-failure' {
                Assert-Fixture (($installed.Name -join ',') -eq 'Az.Login') 'A first-module import failure must stop subsequent installations.'
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Login') 'A first-module import failure must roll back Login directly rather than invoke strict uninstall.'
                if ($ArtifactScenario -eq 'artifacts-rollback-removal-failure')
                {
                    Assert-Fixture ($global:Lifecycle.Packages.Count -eq 1) 'A failed removal must retain the real package state.'
                    Assert-Fixture ($joined -match 'Failed to clean up Az.Login') 'Rollback failure must be reported without replacing the import error.'
                }
                else
                {
                    Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'The first failing module must not remain installed.'
                }
            }
            'install-absent-failure' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Login') 'Rollback must tolerate an attempted installation that left no package.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'Rollback must remove prior installations.'
            }
            'failure-newer-versions' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Compute,Az.Login') 'Failure must reverse only attempted exact versions.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq $definitions.Count) 'Failure must preserve every preexisting newer version.'
                foreach ($package in $global:Lifecycle.Packages.Values)
                {
                    Assert-Fixture ($package.Version -eq [version]'9.0.0') 'Failure cleanup must not remove unrelated newer versions.'
                }
            }
            'removal-failures' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Resources,Az.Compute,Az.Login') 'Rollback must keep removing after multiple failures.'
                Assert-Fixture ($joined -match 'Failed to clean up Az.Resources' -and $joined -match 'Failed to clean up Az.Compute') 'Every removal failure must be logged.'
                Assert-Fixture (-not $global:Lifecycle.Packages.ContainsKey('Az.Login|1.2.3')) 'The last rollback must still run after earlier failures.'
            }
            'cleanup-query-failure' {
                Assert-Fixture ($joined -match 'Failed to clean up Az.Compute.*Injected module query failure') 'Unexpected cleanup lookup errors must be surfaced, not treated as absence.'
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Login') 'A cleanup query failure must not prevent later removals.'
            }
            { $_ -in 'preexisting-exact', 'preexisting-available' } {
                Assert-Fixture ($mutations.Count -eq 0) 'Every target must be preflighted before ANY mutation.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 1 -and $global:Lifecycle.Packages.ContainsKey('Az|1.2.3')) 'Preexisting exact versions must remain untouched.'
            }
            'repository-conflict' {
                Assert-Fixture ($mutations.Count -eq 0) 'A repository conflict must not cause any mutation.'
                Assert-Fixture ($global:Lifecycle.Repositories.LocalAzclips.SourceLocation -eq 'original repository') 'Conflicting user repository must remain unchanged.'
            }
            'registration-failure' {
                Assert-Fixture ($registered.Count -eq 1 -and $unregistered.Count -eq 0) 'Failed registration must never be unregistered as owned.'
                Assert-Fixture ($installed.Count -eq 0 -and $removed.Count -eq 0) 'Registration failure must not alter modules.'
            }
            { $_ -in 'unregistration-failure', 'directory-cleanup-failure' } {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az,Az.Resources,Az.Compute,Az.Login') 'Repository teardown failure must roll back an otherwise successful install.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'Repository teardown failure must not leave installed packages.'
            }
            'strict-uninstall-missing' {
                Assert-Fixture ($removed.Count -eq 0 -and $global:Lifecycle.Packages.Count -eq 3) 'Normal uninstall must fail strictly when the first required module is missing.'
            }
            'strict-uninstall-partial' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az,Az.Resources') 'Strict uninstall must stop on the first missing version after earlier removals.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 1 -and $global:Lifecycle.Packages.ContainsKey('Az.Login|1.2.3')) 'Strict partial uninstall must leave later definitions untouched.'
            }
            'partial-install' {
                Assert-Fixture (($installed.Name -join ',') -eq 'Az.Login,Az.Compute') 'Only attempted versions may be partially installed.'
                Assert-Fixture (($removed.Name -join ',') -eq 'Az.Login') 'PowerShellGet rollback must still remove registered prior installs.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 1 -and -not $global:Lifecycle.Packages['Az.Compute|1.2.3'].Registered) 'Inventory must not claim the unregistered partial package was removed.'
            }
            'capture-failure' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az,Az.Resources,Az.Compute,Az.Login') 'Capture-only failure must still roll back every attempted package in reverse order.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'Capture-only failure must clean attempted packages.'
            }
            'uninstall-capture-only' {
                Assert-Fixture (($removed.Name -join ',') -eq 'Az,Az.Resources,Az.Compute,Az.Login') 'Capture-only failure must not interrupt the strict uninstall loop.'
                Assert-Fixture ($global:Lifecycle.Packages.Count -eq 0) 'Uninstall must finish cleanup despite capture errors.'
            }
            'capture-preflight-failure' {
                Assert-Fixture ($mutations.Count -eq 0) 'Unsafe report paths must fail before installation mutation.'
                Assert-Fixture (-not (Test-Path -LiteralPath $reportRoot)) 'Capture must not create reports in either read-only input root.'
            }
            { $_ -in 'empty-install', 'empty-uninstall', 'invalid-definition', 'preflight-query-failure' } {
                Assert-Fixture ($mutations.Count -eq 0) 'Invalid discovery or query failures must fail before any mutation.'
            }
        }

        if ($Scenario -in 'cleanup-primary', 'unregistration-failure')
        {
            Assert-Fixture ($joined -match 'Failed to unregister this run') 'Repository cleanup failures must be logged.'
        }
        $expectedError = Get-ExpectedFailure $Scenario
        if ($expectedError)
        {
            Assert-Fixture ($null -ne $caught -and $caught.Exception.Message -match $expectedError) "Original failure must remain primary, matching '$expectedError'; got '$caught'."
        }
        if ($ArtifactScenario) { Assert-LifecycleArtifacts -ReportRoot $reportRoot -Definitions $definitions -Output $joined }
        $output | ForEach-Object { Write-Host $_ }
        $displayScenario = if ($ArtifactScenario) { $ArtifactScenario } else { $Scenario }
        Write-Host "FIXTURE ASSERTIONS PASSED: $displayScenario"
        if ($caught) { throw $caught }
    }
    finally
    {
        [Environment]::CurrentDirectory = $originalProcessDirectory
        Pop-Location
        foreach ($registration in $global:Lifecycle.Events | Where-Object Operation -eq Register)
        {
            # Remove the deliberately retained directory from the teardown-failure fixture.
            if (Test-Path -LiteralPath $registration.Directory)
            {
                Microsoft.PowerShell.Management\Remove-Item -LiteralPath $registration.Directory -Recurse -Force
            }
        }
    }
}

function Get-ExpectedFailure
{
    param([string]$Name)
    $Name = Get-LifecycleScenarioName $Name
    switch ($Name)
    {
        { $_ -in 'first-import-failure', 'import-failure', 'help-failure', 'removal-failures', 'cleanup-primary', 'cleanup-query-failure', 'failure-newer-versions' } { 'local gallery import validation failed \(exit 51\)' }
        'install-failure' { 'Injected installation failure after adding' }
        'install-absent-failure' { 'Injected installation failure before adding' }
        { $_ -in 'preexisting-exact', 'preexisting-available' } { 'already exists.*disposable environment' }
        'repository-conflict' { 'already registered.*disposable environment' }
        'registration-failure' { 'Injected repository registration failure' }
        'unregistration-failure' { 'Injected repository unregistration failure' }
        'directory-cleanup-failure' { 'Injected working directory removal failure' }
        'preflight-query-failure' { 'Injected module query failure' }
        { $_ -in 'empty-install', 'empty-uninstall' } { 'No Az module definitions' }
        'invalid-definition' { 'Invalid or missing ModuleVersion' }
        'strict-uninstall-missing' { 'Expected installed module was not found: Az 1.2.3' }
        'strict-uninstall-partial' { 'Expected installed module was not found: Az.Compute 1.2.3' }
        'partial-install' { 'Injected unregistered partial installation failure' }
        { $_ -in 'capture-failure', 'capture-preflight-failure', 'uninstall-capture-only' } { 'Module artifact capture failed' }
    }
}

function Get-LifecycleScenarioName
{
    param([string]$Name)
    switch ($Name)
    {
        'artifacts-rollback-removal-failure' { return 'first-import-failure' }
        { $_ -in 'artifacts-capture-primary', 'artifacts-available-query-failure' } { return 'import-failure' }
        { $_ -in 'artifacts-link-capture', 'artifacts-scope-mismatch' } { return 'capture-failure' }
        { $_ -in 'artifacts-report-in-source', 'artifacts-report-in-gallery', 'artifacts-report-in-module' } { return 'capture-preflight-failure' }
        'artifacts-uninstall-capture-primary' { return 'strict-uninstall-missing' }
        'artifacts-uninstall-partial' { return 'strict-uninstall-partial' }
        default { return ($Name -replace '^artifacts-', '') }
    }
}

function Assert-LifecycleArtifacts
{
    param([string]$ReportRoot, [array]$Definitions, [string]$Output)
    if ($Scenario -eq 'capture-preflight-failure')
    {
        Assert-Fixture ($Output -match 'Module artifact capture.*(source or package repository|module-search directory)') 'Rejected report roots must surface diagnostic warnings without writing there.'
        return
    }
    $captureErrorsPath = Join-Path $ReportRoot 'capture-errors.json'
    if ($ArtifactScenario -in 'artifacts-uninstall-capture-primary', 'artifacts-uninstall-capture-only')
    {
        Assert-Fixture (Test-Path -LiteralPath $captureErrorsPath) 'Unwritable inventory paths must produce capture-errors.json.'
        Assert-Fixture ($Output -match 'Module artifact capture') 'Unwritable inventory paths must also emit a warning.'
        return
    }
    $environment = Get-Content -LiteralPath (Join-Path $ReportRoot 'environment.json') -Raw | ConvertFrom-Json
    $environmentFields = @('schemaVersion', 'capturedAtUtc', 'powerShellVersion', 'powerShellEdition', 'dotNetRuntimeDescription', 'osDescription', 'processArchitecture', 'osArchitecture', 'psHome', 'moduleSearchPaths')
    Assert-Fixture ((($environment.PSObject.Properties.Name | Sort-Object) -join ',') -eq (($environmentFields | Sort-Object) -join ',')) 'Environment output must use only the allowlisted fields.'
    Assert-Fixture ($environment.powerShellVersion -eq $PSVersionTable.PSVersion.ToString()) 'Environment must record the actual test PowerShell version.'
    Assert-Fixture ($environment.powerShellEdition -eq $PSVersionTable.PSEdition -and $environment.psHome -eq $PSHOME) 'Environment must describe the tested process, not the host runner.'
    Assert-Fixture (-not [string]::IsNullOrWhiteSpace($environment.dotNetRuntimeDescription)) 'Environment must include the .NET runtime description.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath $ReportRoot -Recurse -Force -File | Where-Object { $_.Name -in 'unrelated-sentinel.txt', 'credentials.json', 'NuGet.config' }).Count -eq 0) 'Snapshots must never copy unrelated home, NuGet.config, or preexisting version files.'

    $inventoryRoot = Join-Path $ReportRoot 'inventories'
    $isUninstallOnly = $Scenario -in 'strict-uninstall-missing', 'strict-uninstall-partial'
    $beforeStage = if ($isUninstallOnly) { 'before-uninstall' } else { 'before-install' }
    $before = Get-Content -LiteralPath (Join-Path $inventoryRoot "$beforeStage.json") -Raw | ConvertFrom-Json
    Assert-Fixture (@($before.modules).Count -eq $Definitions.Count -and $before.stage -eq $beforeStage) 'Before inventories must enumerate exactly the requested targets.'
    foreach ($module in $before.modules)
    {
        Assert-Fixture ($module.version -eq '1.2.3') 'Inventories must not substitute preexisting newer versions.'
        Assert-Fixture ($module.snapshot.status -eq 'not-requested') 'Preflight must never snapshot preexisting modules.'
    }
    if ($isUninstallOnly)
    {
        $after = Get-Content -LiteralPath (Join-Path $inventoryRoot 'after-uninstall.json') -Raw | ConvertFrom-Json
        Assert-Fixture (@($after.modules | Where-Object { $_.packageName -eq 'Az.Login' -and $_.installed.status -eq 'found' }).Count -eq 1) 'Strict uninstall failure must retain final evidence of untouched modules.'
        if ($Scenario -eq 'strict-uninstall-partial')
        {
            Assert-Fixture (@($after.modules | Where-Object { $_.packageName -in 'Az', 'Az.Resources', 'Az.Compute' -and $_.installed.status -eq 'missing' }).Count -eq 3) 'Partial uninstall must record both removed and initially missing versions.'
        }
        Assert-Fixture (-not (Test-Path -LiteralPath (Join-Path $ReportRoot 'installed-modules'))) 'Standalone uninstall cannot claim ownership of installed snapshot versions.'
        return
    }
    $failed = [bool](Get-ExpectedFailure $Scenario)
    $afterStage = if ($failed) { 'install-failure' } else { 'after-install' }
    $after = Get-Content -LiteralPath (Join-Path $inventoryRoot "$afterStage.json") -Raw | ConvertFrom-Json
    $attempted = @($global:Lifecycle.Events | Where-Object Operation -eq Install)
    foreach ($module in $after.modules)
    {
        $wasAttempted = @($attempted | Where-Object { $_.Name -eq $module.packageName -and $_.Version -eq $module.version }).Count -gt 0
        if (-not $wasAttempted)
        {
            Assert-Fixture ($module.snapshot.status -eq 'not-attempted') 'Unattempted/preexisting targets must never be snapshotted.'
            Assert-Fixture (-not (Test-Path -LiteralPath (Join-Path $ReportRoot "installed-modules\$($module.packageName)\$($module.version)"))) 'There must be no snapshot directory for an unattempted version.'
        }
        elseif ($module.snapshot.status -eq 'copied')
        {
            $snapshot = $module.snapshot.destinationPath
            $manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $snapshot "$($module.packageName).psd1")
            Assert-Fixture ($manifest.ModuleVersion -eq $module.version) 'Snapshots must retain the exact versioned manifest.'
            Assert-Fixture ((Get-Content -LiteralPath (Join-Path $snapshot "$($module.packageName).dll") -Raw).Trim() -eq "Fake binary for $($module.packageName) $($module.version)") 'Snapshots must preserve binary file contents.'
            Assert-Fixture (Test-Path -LiteralPath (Join-Path $snapshot "en-US\$($module.packageName).dll-Help.xml")) 'Snapshots must preserve external help.'
            if ([Environment]::OSVersion.Platform -eq [PlatformID]::Unix)
            {
                Assert-Fixture (([int][IO.File]::GetUnixFileMode($snapshot) -band 5) -eq 5) 'Container snapshot directories must be readable/traversable by the host publisher.'
                Assert-Fixture (([int][IO.File]::GetUnixFileMode((Join-Path $snapshot "$($module.packageName).dll")) -band 4) -eq 4) 'Container snapshot files must be readable by the host publisher.'
            }
        }
        elseif ($ArtifactScenario -eq 'artifacts-install-absent-failure' -and $module.packageName -eq 'Az.Compute')
        {
            Assert-Fixture ($module.snapshot.status -eq 'missing' -and $module.installed.status -eq 'missing') 'An absent attempted package must be explicit rather than manufactured.'
        }
        else
        {
            Assert-Fixture ($module.packageName -eq 'Az.Compute' -and $module.snapshot.status -eq 'error' -and $module.snapshot.error) 'Capture failures must be explicit for the affected module.'
        }
    }
    if ($failed -and $attempted.Count -gt 0)
    {
        $rollback = Get-Content -LiteralPath (Join-Path $inventoryRoot 'after-rollback.json') -Raw | ConvertFrom-Json
        if ($Scenario -eq 'partial-install')
        {
            $partial = @($after.modules | Where-Object packageName -eq 'Az.Compute')[0]
            Assert-Fixture ($partial.installed.status -eq 'missing' -and $partial.available.status -eq 'found' -and $partial.snapshot.status -eq 'copied') 'Partial unregistered installs must be snapshotted from exact available-module metadata before rollback.'
            $partialAfter = @($rollback.modules | Where-Object packageName -eq 'Az.Compute')[0]
            Assert-Fixture ($partialAfter.installed.status -eq 'missing' -and $partialAfter.available.status -eq 'found') 'Rollback inventories must truthfully report unregistered leftovers.'
        }
        elseif ($ArtifactScenario -eq 'artifacts-rollback-removal-failure')
        {
            Assert-Fixture (@($rollback.modules | Where-Object { $_.packageName -eq 'Az.Login' -and $_.installed.status -eq 'found' }).Count -eq 1) 'Final rollback inventory must exist even when every removal fails.'
        }
        elseif ($Scenario -ne 'cleanup-query-failure')
        {
            Assert-Fixture (@($rollback.modules | Where-Object { $_.installed.status -ne 'missing' }).Count -eq 0) 'After-rollback inventories must prove exact registered targets are absent.'
        }
    }
    if ($Scenario -eq 'newer-versions')
    {
        $uninstalled = Get-Content -LiteralPath (Join-Path $inventoryRoot 'after-uninstall.json') -Raw | ConvertFrom-Json
        Assert-Fixture (@($uninstalled.modules | Where-Object { $_.installed.status -ne 'missing' -or $_.available.status -ne 'missing' }).Count -eq 0) 'Normal uninstall must record final exact-version absence even with newer versions remaining.'
        Assert-Fixture (@(Get-ChildItem -LiteralPath (Join-Path $ReportRoot 'installed-modules') -Recurse -File -Filter '*.dll').Count -eq $Definitions.Count) 'Snapshots must survive subsequent uninstall of their originals.'
    }
    if ($ArtifactScenario -in 'artifacts-capture-failure', 'artifacts-capture-primary', 'artifacts-link-capture', 'artifacts-scope-mismatch', 'artifacts-available-query-failure', 'artifacts-cleanup-query-failure')
    {
        $captureErrors = Get-Content -LiteralPath $captureErrorsPath -Raw | ConvertFrom-Json
        Assert-Fixture (@($captureErrors.errors).Count -gt 0 -and $Output -match 'Module artifact capture') 'Every capture problem must produce structured errors and a warning.'
        if ($ArtifactScenario -eq 'artifacts-available-query-failure')
        {
            Assert-Fixture (@($after.modules | Where-Object { $_.packageName -eq 'Az.Compute' -and $_.available.status -eq 'error' }).Count -eq 1) 'Unexpected available lookup failures must not be reported as missing.'
        }
    }
    else
    {
        Assert-Fixture (-not (Test-Path -LiteralPath $captureErrorsPath)) 'Normal absence and valid snapshots must not be mislabeled as capture failures.'
    }
}

if ($Scenario)
{
    $script:ArtifactScenario = if ($Scenario -like 'artifacts-*') { $Scenario } else { $null }
    $script:Scenario = Get-LifecycleScenarioName $Scenario
    Invoke-LifecycleScenario
    exit 0
}

$scenarios = @(
    'success', 'first-import-failure', 'import-failure', 'help-failure', 'install-failure', 'install-absent-failure',
    'removal-failures', 'cleanup-query-failure', 'preexisting-exact', 'preexisting-available',
    'newer-versions', 'repository-conflict', 'registration-failure', 'unregistration-failure',
    'cleanup-primary', 'preflight-query-failure', 'empty-install', 'empty-uninstall',
    'invalid-definition', 'strict-uninstall-missing', 'default-source', 'accounts-source',
    'failure-newer-versions', 'directory-cleanup-failure',
    'artifacts-success', 'artifacts-first-import-failure', 'artifacts-rollback-removal-failure', 'artifacts-newer-versions', 'artifacts-install-failure', 'artifacts-install-absent-failure',
    'artifacts-partial-install', 'artifacts-preexisting-exact', 'artifacts-preexisting-available',
    'artifacts-cleanup-primary', 'artifacts-cleanup-query-failure', 'artifacts-unregistration-failure',
    'artifacts-directory-cleanup-failure', 'artifacts-strict-uninstall-missing', 'artifacts-uninstall-partial',
    'artifacts-capture-failure', 'artifacts-capture-primary', 'artifacts-link-capture', 'artifacts-scope-mismatch',
    'artifacts-report-in-source', 'artifacts-report-in-gallery', 'artifacts-report-in-module', 'artifacts-available-query-failure',
    'artifacts-uninstall-capture-primary', 'artifacts-uninstall-capture-only'
)
$scratchRoot = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) "ps-lifecycle-$([guid]::NewGuid().ToString('N'))")
$pwsh = (Get-Process -Id $PID).Path
try
{
    foreach ($name in $scenarios)
    {
        $scenarioRoot = New-Item -ItemType Directory -Path (Join-Path $scratchRoot.FullName $name)
        $output = & $pwsh -NoProfile -File $PSCommandPath -Scenario $name -FixtureRoot $scenarioRoot.FullName 2>&1 |
            ForEach-Object { $_.ToString() }
        $exitCode = $LASTEXITCODE
        $joined = $output -join "`n"
        Assert-Fixture ($joined -match "FIXTURE ASSERTIONS PASSED: $name") "[$name] Child assertions did not complete:`n$joined"
        $expectedError = Get-ExpectedFailure $name
        if ($expectedError)
        {
            # The child asserts the original ErrorRecord message. Console formatting
            # can wrap that message differently on Windows and Linux.
            Assert-Fixture ($exitCode -ne 0) "[$name] Expected original script failure/nonzero exit:`n$joined"
        }
        else
        {
            Assert-Fixture ($exitCode -eq 0) "[$name] Expected success; got exit ${exitCode}:`n$joined"
        }
        Write-Host "[$name] PASS"
    }
}
finally { Remove-Item -LiteralPath $scratchRoot.FullName -Recurse -Force }

Write-Host "All $($scenarios.Count) PS module lifecycle fixtures passed."
exit 0
