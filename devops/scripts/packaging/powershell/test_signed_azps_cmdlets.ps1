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

    $bootstrapPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../install/azps-install.ps1'))
    $bootstrapValidationScript = Join-Path $testRoot 'ValidatePublicBootstrap.ps1'
    @'
param($BootstrapPath, $ArchivePath, $ModuleName, $ExpectedVersion)
$ErrorActionPreference = 'Stop'
Import-Module PowerShellGet -ErrorAction Stop
$fixture = @{ Scenario = 'valid'; Stage = $null; Installed = $null }

function Invoke-RestMethod {
    [CmdletBinding()]
    param([string]$Uri)
    if ($Uri -ne 'https://api.github.com/repos/Azure/azclips/releases/latest') { throw "Unexpected metadata URL: $Uri" }
    if ($fixture.Scenario -eq 'metadata-failure') { throw 'Fixture HTTP 403 rate limit' }
    $asset = @{ name = (Split-Path -Leaf $ArchivePath); browser_download_url = 'https://example.invalid/pipeline-archive' }
    $assets = @($asset)
    if ($fixture.Scenario -eq 'missing') { $assets = @() }
    if ($fixture.Scenario -eq 'duplicate') { $assets = @($asset, $asset) }
    if ($fixture.Scenario -eq 'invalid-url') { $asset.browser_download_url = 'http://example.invalid/pipeline-archive' }
    $tag = if ($fixture.Scenario -eq 'malformed') { '' } else { 'v-fixture.timestamp' }
    @{ tag_name = $tag; assets = $assets }
}
function Invoke-WebRequest {
    [CmdletBinding()]
    param([string]$Uri, [string]$OutFile)
    if ($Uri -ne 'https://example.invalid/pipeline-archive') { throw "Wrong asset selected: $Uri" }
    $fixture.Stage = Split-Path $OutFile
    if ($fixture.Scenario -eq 'download-failure') { throw 'Fixture HTTP 503 download failure' }
    Copy-Item -LiteralPath $ArchivePath -Destination $OutFile
}
function Install-Module {
    [CmdletBinding()]
    param($Name, $RequiredVersion, $Repository, $Scope, [switch]$Force, [switch]$AllowClobber)
    $fixture.Installed = @{ Name = $Name; Version = $RequiredVersion; Repository = $Repository; Scope = $Scope }
    if ($fixture.Scenario -eq 'install-failure') { throw 'Fixture installation failed' }
    PowerShellGet\Install-Module @PSBoundParameters
}
function Assert-Cleanup {
    if ($fixture.Stage -and (Test-Path -LiteralPath $fixture.Stage)) { throw 'Bootstrap left temporary files.' }
    if ($fixture.Installed -and (Get-PSRepository -Name $fixture.Installed.Repository -ErrorAction SilentlyContinue)) {
        throw 'Bootstrap left its temporary repository.'
    }
}

$repositoryName = "BootstrapFixture$PID"
& $BootstrapPath -ModuleName $ModuleName -RequiredVersion $ExpectedVersion -Scope CurrentUser -RepositoryName $repositoryName
if ($fixture.Installed.Name -ne $ModuleName -or $fixture.Installed.Version -ne $ExpectedVersion -or
    $fixture.Installed.Repository -ne $repositoryName -or $fixture.Installed.Scope -ne 'CurrentUser') {
    throw 'Bootstrap did not forward module/version/repository/scope options.'
}
Assert-Cleanup
Import-Module -Name $ModuleName -RequiredVersion $ExpectedVersion -ErrorAction Stop

$beforePath = $env:PATH
$ErrorActionPreference = 'Continue'
Get-Content -Raw $BootstrapPath | Invoke-Expression
if ($ErrorActionPreference -ne 'Continue' -or $env:PATH -ne $beforePath -or
    (Get-Variable options -Scope Local -ErrorAction SilentlyContinue)) {
    throw 'Piped bootstrap leaked state into the caller.'
}
$ErrorActionPreference = 'Stop'
if ($fixture.Installed.Name -ne 'Az' -or $fixture.Installed.Scope -ne 'CurrentUser') {
    throw 'Piped bootstrap did not install the default Az rollup.'
}
Assert-Cleanup
Import-Module Az -ErrorAction Stop

foreach ($scenario in 'missing', 'duplicate', 'malformed', 'invalid-url', 'metadata-failure', 'download-failure', 'install-failure') {
    $fixture.Scenario = $scenario
    $fixture.Stage = $null
    $fixture.Installed = $null
    $expected = switch ($scenario) {
        'missing' { 'Expected exactly one' }
        'duplicate' { 'Expected exactly one' }
        'malformed' { 'valid tag_name' }
        'invalid-url' { 'HTTPS download URL' }
        'metadata-failure' { 'Fixture HTTP 403' }
        'download-failure' { 'Fixture HTTP 503' }
        'install-failure' { 'Fixture installation failed' }
    }
    try {
        if ($scenario -eq 'install-failure') {
            # The proxy throws before writing to AllUsers; only forwarding is tested.
            & $BootstrapPath -ModuleName $ModuleName -Scope AllUsers
        }
        else {
            Get-Content -Raw $BootstrapPath | Invoke-Expression
        }
        throw "FAIL: $scenario unexpectedly succeeded."
    }
    catch {
        if ($_.Exception.Message -notlike "*$expected*") { throw }
    }
    Assert-Cleanup
    if ($scenario -eq 'install-failure' -and $fixture.Installed.Scope -ne 'AllUsers') {
        throw 'Bootstrap did not forward the non-default scope.'
    }
}

Register-PSRepository -Name $repositoryName -SourceLocation (Split-Path $ArchivePath) -InstallationPolicy Trusted
try {
    try {
        & $BootstrapPath -RepositoryName $repositoryName
        throw 'FAIL: Bootstrap replaced an existing repository.'
    }
    catch {
        if ($_.Exception.Message -notlike '*already exists*') { throw }
    }
    if (-not (Get-PSRepository -Name $repositoryName -ErrorAction SilentlyContinue)) {
        throw 'Bootstrap removed an existing repository.'
    }
}
finally {
    Unregister-PSRepository -Name $repositoryName
}
Write-Host 'Validated public PowerShell bootstrap: pipeline archive installation/import, options, piped scope, and failure cleanup.'
'@ | Set-Content -Path $bootstrapValidationScript
    Invoke-IsolatedPowerShell -PwshPath $PwshPath -HomeRoot $homeRoot -ModuleRoot $moduleRoot -Arguments @(
        '-NoLogo', '-NoProfile', '-File', $bootstrapValidationScript,
        '-BootstrapPath', $bootstrapPath,
        '-ArchivePath', $resolvedArchivePath,
        '-ModuleName', $candidate.Name,
        '-ExpectedVersion', $candidate.Version.ToString()
    )
    Write-Host "Validated signed PowerShell installer lifecycle: $($candidate.Name) $($candidate.Version) from $resolvedArchivePath"
}
finally
{
    Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}