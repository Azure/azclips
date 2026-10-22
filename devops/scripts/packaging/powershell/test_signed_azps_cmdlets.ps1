[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [string]$PwshPath = 'pwsh'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Get-PackageIdentity
{
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$Package
    )

    if ($Package.BaseName -match '^Az\.(?<Version>\d+(?:\.\d+){1,3})$')
    {
        return [pscustomobject]@{
            Name = 'Az'
            Version = [version]$Matches.Version
            Path = $Package.FullName
        }
    }

    if ($Package.BaseName -notmatch '^(?<Name>Az(?:\.[A-Za-z][A-Za-z0-9]*)+)\.(?<Version>\d+(?:\.\d+){1,3})$')
    {
        return $null
    }

    [pscustomobject]@{
        Name = $Matches.Name
        Version = [version]$Matches.Version
        Path = $Package.FullName
    }
}

function Invoke-IsolatedPowerShell
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PwshPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$HomeRoot,

        [Parameter(Mandatory = $true)]
        [string]$ModuleRoot
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $PwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['HOME'] = $HomeRoot
    $startInfo.Environment['PSModulePath'] = "$ModuleRoot$([System.IO.Path]::PathSeparator)$($env:PSModulePath)"
    foreach ($argument in $Arguments)
    {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try
    {
        if (-not $process.Start())
        {
            throw "Failed to start PowerShell executable: $PwshPath"
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()

        if ($output)
        {
            Write-Host $output.TrimEnd()
        }
        if ($errorOutput)
        {
            Write-Host $errorOutput.TrimEnd()
        }
        if ($process.ExitCode -ne 0)
        {
            throw "Isolated PowerShell exited with code $($process.ExitCode)."
        }
    }
    finally
    {
        $process.Dispose()
    }
}

function Assert-NumericVersionInference
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallerPath,

        [Parameter(Mandatory = $true)]
        [string]$FixtureRoot
    )

    $fixtureInstaller = Join-Path $FixtureRoot 'InstallModule.ps1'
    Copy-Item -Path $InstallerPath -Destination $fixtureInstaller
    $null = New-Item -ItemType File -Path @(
        (Join-Path $FixtureRoot 'Az.9.9.9.nupkg'),
        (Join-Path $FixtureRoot 'Az.Compute.0.1.9.nupkg'),
        (Join-Path $FixtureRoot 'Az.Compute.0.1.10.nupkg')
    )

    $capture = @{}
    & {
        function Get-PSRepository
        {
            param([string]$Name)
            return $null
        }

        function Register-PSRepository
        {
            param($Name, $SourceLocation, $PublishLocation, $InstallationPolicy, $PackageManagementProvider)
        }

        function Install-Module
        {
            param($Name, $RequiredVersion, $Repository, $Scope, [switch]$Force, [switch]$AllowClobber)
            $capture.Name = $Name
            $capture.Version = $RequiredVersion
        }

        & $fixtureInstaller -ModuleName 'Az.Compute'
    }

    if ($capture.Name -ne 'Az.Compute' -or $capture.Version -ne '0.1.10')
    {
        throw "Expected numeric inference to select Az.Compute 0.1.10; got $($capture.Name) $($capture.Version)."
    }

    Write-Host 'Validated numeric module version inference: Az.Compute 0.1.10 selected over 0.1.9 and Az 9.9.9.'
}

$resolvedArchivePath = (Resolve-Path -Path $ArchivePath).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-signed-ps-$([guid]::NewGuid())"
$extractRoot = Join-Path $testRoot 'repository'
$homeRoot = Join-Path $testRoot 'home'
$moduleRoot = Join-Path $homeRoot '.local/share/powershell/Modules'
$fixtureRoot = Join-Path $testRoot 'version-fixture'
$validationScript = Join-Path $testRoot 'ValidateInstalledModule.ps1'

try
{
    $null = New-Item -ItemType Directory -Path $extractRoot, $homeRoot, $moduleRoot, $fixtureRoot -Force
    & tar -xzf $resolvedArchivePath -C $extractRoot
    if ($LASTEXITCODE -ne 0)
    {
        throw "Failed to extract signed PowerShell archive: $resolvedArchivePath"
    }

    $installerPath = Join-Path $extractRoot 'InstallModule.ps1'
    if (-not (Test-Path -Path $installerPath -PathType Leaf))
    {
        throw "Signed PowerShell archive does not contain InstallModule.ps1: $resolvedArchivePath"
    }

    $packageIdentities = @(Get-ChildItem -Path $extractRoot -Filter '*.nupkg' -File | ForEach-Object { Get-PackageIdentity -Package $_ })
    $rollup = @($packageIdentities | Where-Object Name -eq 'Az')
    $childPackages = @($packageIdentities | Where-Object Name -ne 'Az' | Sort-Object Name)
    if ($rollup.Count -ne 1 -or $childPackages.Count -eq 0)
    {
        throw "Expected one Az rollup and at least one Az.* child package; found $($rollup.Count) rollup and $($childPackages.Count) child packages."
    }

    $candidate = $childPackages | Where-Object Version -ne $rollup[0].Version | Select-Object -First 1
    if (-not $candidate)
    {
        $candidate = $childPackages | Select-Object -First 1
    }

    $repositoryName = "LocalAzclipsSigned$PID"
    Invoke-IsolatedPowerShell -PwshPath $PwshPath -HomeRoot $homeRoot -ModuleRoot $moduleRoot -Arguments @(
        '-NoLogo',
        '-NoProfile',
        '-File', $installerPath,
        '-RepositoryName', $repositoryName,
        '-ModuleName', $candidate.Name,
        '-Scope', 'CurrentUser'
    )

    @'
param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleName,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$module = Get-Module -ListAvailable -Name $ModuleName |
    Where-Object Version -eq ([version]$ExpectedVersion) |
    Select-Object -First 1
if (-not $module)
{
    throw "Expected installed module was not found: $ModuleName $ExpectedVersion"
}
Import-Module -Name $module.Path -Force -ErrorAction Stop
Write-Host "Imported signed archive module: $ModuleName $ExpectedVersion"
'@ | Set-Content -Path $validationScript

    Invoke-IsolatedPowerShell -PwshPath $PwshPath -HomeRoot $homeRoot -ModuleRoot $moduleRoot -Arguments @(
        '-NoLogo',
        '-NoProfile',
        '-File', $validationScript,
        '-ModuleName', $candidate.Name,
        '-ExpectedVersion', $candidate.Version.ToString()
    )

    Assert-NumericVersionInference -InstallerPath $installerPath -FixtureRoot $fixtureRoot
    Write-Host "Validated signed PowerShell installer lifecycle: $($candidate.Name) $($candidate.Version) from $resolvedArchivePath"
}
finally
{
    Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}