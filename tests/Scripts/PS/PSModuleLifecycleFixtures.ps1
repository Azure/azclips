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
    $exports = @{}
    foreach ($cmdlet in $global:Lifecycle.Definitions[$Name].CmdletsToExport)
    {
        $exports[$cmdlet] = [pscustomobject]@{ Name = $cmdlet }
    }
    [pscustomobject]@{
        Name = $Name
        Version = [version]$Version
        Registered = $Registered
        InstalledLocation = Join-Path $FixtureRoot "$Name-$Version"
        Path = Join-Path $FixtureRoot "$Name-$Version\$Name.psd1"
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
        Assert-Fixture ($global:Lifecycle.Packages.ContainsKey("$Name|$RequiredVersion")) 'Absent versions must not be uninstalled during rollback.'
        $global:Lifecycle.Packages.Remove("$Name|$RequiredVersion")
    }

    function global:Import-Module
    {
        [CmdletBinding()]
        param([string]$Name, [string]$RequiredVersion, [switch]$Force, [switch]$PassThru)
        Assert-Fixture (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) 'Import-Module must pin RequiredVersion.'
        Add-LifecycleEvent Import $Name $RequiredVersion
        $failureModule = if ($Scenario -eq 'removal-failures') { 'Az.Resources' } else { 'Az.Compute' }
        if ($Name -eq $failureModule -and $Scenario -in 'import-failure', 'removal-failures', 'cleanup-primary', 'cleanup-query-failure', 'failure-newer-versions')
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
    New-Item -ItemType Directory -Path $sourceRoot, $galleryRoot, $processRoot -Force | Out-Null
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
    if ($Scenario -in 'empty-install', 'empty-uninstall')
    {
        Get-ChildItem -LiteralPath $sourceRoot -Filter '*.psd1' -Recurse | Remove-Item
    }
    if ($Scenario -eq 'invalid-definition')
    {
        Set-Content -LiteralPath (Join-Path $sourceRoot 'Az\Az.psd1') -Value '@{ CmdletsToExport = @() }'
    }

    Enable-LifecycleFakes
    $caught = $null
    $output = [System.Collections.Generic.List[string]]::new()
    Push-Location -LiteralPath $FixtureRoot
    $originalProcessDirectory = [Environment]::CurrentDirectory
    try
    {
        # Deliberately start with different PowerShell and .NET working directories.
        [Environment]::CurrentDirectory = $processRoot
        $parameters = @{ SourcePsRoot = '.\source'; PwshPath = 'Invoke-FakePwsh' }
        try
        {
            if ($Scenario -in 'strict-uninstall-missing', 'empty-uninstall')
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
                Assert-Fixture ($queriesBeforeMutation.Count -eq $definitions.Count) 'All exact versions must be checked before repository registration.'
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
        $output | ForEach-Object { Write-Host $_ }
        Write-Host "FIXTURE ASSERTIONS PASSED: $Scenario"
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
    switch ($Name)
    {
        { $_ -in 'import-failure', 'help-failure', 'removal-failures', 'cleanup-primary', 'cleanup-query-failure', 'failure-newer-versions' } { 'local gallery import validation failed \(exit 51\)' }
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
    }
}

if ($Scenario)
{
    Invoke-LifecycleScenario
    exit 0
}

$scenarios = @(
    'success', 'import-failure', 'help-failure', 'install-failure', 'install-absent-failure',
    'removal-failures', 'cleanup-query-failure', 'preexisting-exact', 'preexisting-available',
    'newer-versions', 'repository-conflict', 'registration-failure', 'unregistration-failure',
    'cleanup-primary', 'preflight-query-failure', 'empty-install', 'empty-uninstall',
    'invalid-definition', 'strict-uninstall-missing', 'default-source', 'accounts-source',
    'failure-newer-versions', 'directory-cleanup-failure'
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
