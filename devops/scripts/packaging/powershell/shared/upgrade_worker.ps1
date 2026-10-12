[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RequestPath,
    [Parameter(Mandatory)][string]$ResultPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'upgrade_modules.ps1')
$request = Get-Content -LiteralPath $RequestPath -Raw -ErrorAction Stop | ConvertFrom-Json
$result = [ordered]@{ status = 'Failed'; message = $null; data = @{} }
try
{
    if ($request.operation -in 'Preflight', 'Inspect', 'Cleanup')
    {
        foreach ($package in $request.packages)
        {
            if ($package.Name -notmatch '^Az(?:\.[A-Za-z0-9_-]+)*$' -or
                $package.Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$')
            {
                throw 'Unsafe upgrade module identity.'
            }
        }
    }
    if ($request.operation -ne 'Import') { Import-Module PowerShellGet -ErrorAction Stop }
    switch ($request.operation)
    {
        'Preflight' {
            if (@(Get-PSRepository -ErrorAction Stop | Where-Object Name -eq $request.repositoryName).Count -gt 0)
            {
                throw "Upgrade repository already exists: $($request.repositoryName)"
            }
            $roots = @($env:PSModulePath -split [regex]::Escape([string][IO.Path]::PathSeparator) |
                Where-Object { $_ } | ForEach-Object {
                    if (-not [IO.Path]::IsPathRooted($_)) { throw "Module search path is not absolute: $_" }
                    [IO.Path]::GetFullPath($_)
                } | Sort-Object -Unique)
            if ($roots.Count -eq 0) { throw 'No module search paths are available for upgrade preflight.' }
            foreach ($package in $request.packages)
            {
                if ((Get-PSUpgradeInstalledModule $package) -or
                    @(Get-Module -ListAvailable -Name $package.Name | Where-Object Version -eq ([version]$package.Version)).Count -gt 0)
                {
                    throw "Upgrade requires a disposable environment without $($package.Name) $($package.Version)."
                }
                foreach ($root in $roots)
                {
                    if (Test-Path -LiteralPath (Join-Path $root "$($package.Name)/$($package.Version)"))
                    {
                        throw "Preexisting module version directory will not be changed: $($package.Name) $($package.Version)"
                    }
                }
            }
            $result.data = @{ moduleRoots = $roots }
        }
        'Install' {
            $unsignedProperty = $request.PSObject.Properties['allowUnsignedArtifact']
            $allowUnsignedArtifact = $null -ne $unsignedProperty -and $unsignedProperty.Value -eq $true
            $defaults = $PSDefaultParameterValues
            $result.data = @{ installerExitCode = $null; publisherCheckSkipped = $allowUnsignedArtifact }
            try
            {
                if ($allowUnsignedArtifact)
                {
                    # Match fresh-install CI policy without modifying the released helper.
                    $PSDefaultParameterValues = $defaults.Clone()
                    $PSDefaultParameterValues['Install-Module:SkipPublisherCheck'] = $true
                }
                $global:LASTEXITCODE = 0
                & (Join-Path $request.galleryRoot 'InstallModule.ps1') `
                    -RepositoryName $request.repositoryName -ModuleName $request.moduleName `
                    -RequiredVersion $request.version -Scope CurrentUser
                $result.data.installerExitCode = $LASTEXITCODE
                if ($LASTEXITCODE -ne 0) { throw "The released installer exited with code $LASTEXITCODE." }
            }
            finally { $PSDefaultParameterValues = $defaults }
        }
        'Inspect' {
            $modules = @(foreach ($package in $request.packages)
            {
                $installed = @(Get-PSUpgradeInstalledModule $package)
                if ($installed.Count -ne 1 -or $installed[0].Repository -ne $request.repositoryName)
                {
                    throw "Expected one installed $($package.Name) $($package.Version) from the upgrade gallery."
                }
                Assert-PSUpgradeInstalledPayload -Package $package -InstalledLocation $installed[0].InstalledLocation
            })
            $result.data = @{ modules = $modules }
        }
        'Import' {
            foreach ($expected in $request.modules)
            {
                $imported = @(Import-Module -Name $expected.modulePath -RequiredVersion $expected.version -PassThru -ErrorAction Stop)
                if ($imported.Count -ne 1 -or $imported[0].Name -ne $expected.name -or
                    $imported[0].Version -ne [version]$expected.version -or
                    [IO.Path]::GetFullPath($imported[0].ModuleBase) -ne [IO.Path]::GetFullPath($expected.installedLocation))
                {
                    throw "Wrong upgraded module was imported: $($expected.name)"
                }
                foreach ($command in $expected.commands)
                {
                    if ($command -notin $imported[0].ExportedCommands.Keys)
                    {
                        throw "Upgraded $($expected.name) does not export $command."
                    }
                }
            }
            $result.data = @{ imported = @($request.modules | ForEach-Object { @{ name = $_.name; version = $_.version } }) }
        }
        'Cleanup' {
            $errors = [Collections.Generic.List[string]]::new()
            foreach ($package in $request.packages)
            {
                try
                {
                    $installed = @(Get-PSUpgradeInstalledModule $package)
                    if ($installed.Count -gt 0)
                    {
                        if ($installed.Count -ne 1) { throw "Ambiguous cleanup identity: $($package.Name) $($package.Version)" }
                        $location = [IO.Path]::GetFullPath($installed[0].InstalledLocation)
                        $expectedLocations = @($request.moduleRoots | ForEach-Object {
                            [IO.Path]::GetFullPath((Join-Path $_ "$($package.Name)/$($package.Version)"))
                        })
                        if ($location -notin $expectedLocations) { throw "Cleanup location is outside preflight module roots: $location" }
                        Assert-AzArtifactNoLinks -Path $location
                        Uninstall-Module -Name $package.Name -RequiredVersion $package.Version -Force -ErrorAction Stop
                    }
                }
                catch { $errors.Add($_.Exception.Message) }
                foreach ($root in $request.moduleRoots)
                {
                    try
                    {
                        $path = Join-Path $root "$($package.Name)/$($package.Version)"
                        if (Test-Path -LiteralPath $path)
                        {
                            Assert-AzArtifactNoLinks -Path $path
                            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
                        }
                    }
                    catch { $errors.Add($_.Exception.Message) }
                }
                try
                {
                    if ((Get-PSUpgradeInstalledModule $package) -or
                        @(Get-Module -ListAvailable -Name $package.Name | Where-Object Version -eq ([version]$package.Version)).Count -gt 0)
                    {
                        throw "Upgrade cleanup left $($package.Name) $($package.Version) installed."
                    }
                }
                catch { $errors.Add($_.Exception.Message) }
            }
            try
            {
                $repository = @(Get-PSRepository -ErrorAction Stop | Where-Object Name -eq $request.repositoryName)
                if ($repository.Count -gt 0)
                {
                    if ($repository.Count -ne 1 -or
                        [IO.Path]::GetFullPath($repository[0].SourceLocation) -ne [IO.Path]::GetFullPath($request.galleryRoot))
                    {
                        throw 'Upgrade cleanup refused to change a repository pointing outside its gallery.'
                    }
                    Unregister-PSRepository -Name $request.repositoryName -ErrorAction Stop
                }
            }
            catch { $errors.Add($_.Exception.Message) }
            $result.data = @{ errors = @($errors.ToArray()) }
            if ($errors.Count -gt 0) { throw "Upgrade cleanup failed: $($errors -join '; ')" }
        }
        default { throw "Unknown upgrade operation: $($request.operation)" }
    }
    $result.status = 'Succeeded'
}
catch { $result.message = $_.Exception.Message }
finally { Write-PSValidationJson -Path $ResultPath -Value $result }
if ($result.status -ne 'Succeeded') { exit 1 }
