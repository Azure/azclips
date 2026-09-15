<#
.SYNOPSIS
Exercises dependency validation with real local NuGet packages and PowerShellGet.
.DESCRIPTION
No public feeds, user-module installations, Azure credentials, or Pester are used.
Repository registrations have unique names and are removed by the runner.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$runner = Join-Path $repoRoot 'devops/scripts/packaging/powershell/test_ps_dependencies.ps1'
. (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/dependency_packages.ps1')
$scratch = Join-Path ([IO.Path]::GetTempPath()) "ps-dependency-fixtures-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$source = Join-Path $scratch 'source'
$gallery = Join-Path $scratch 'gallery'
$reports = Join-Path $scratch 'reports'
$null = New-Item -ItemType Directory -Path $source, $gallery
$count = 0

function Assert-DependencyFixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Dependency fixture failed: $Message" }
}

function New-FixturePackage
{
    param([string]$Name, [string[]]$Dependencies)
    $package = if ($Name -eq 'Az') { 'Az' } else { "Az.$Name" }
    $sourceDirectory = Join-Path $source $Name
    $moduleDirectory = Join-Path $scratch "package-$Name"
    $null = New-Item -ItemType Directory -Path $sourceDirectory, $moduleDirectory -Force
    $required = @($Dependencies | ForEach-Object { "@{ ModuleName = '../$_/$_.psd1'; ModuleVersion = '1.0.0' }" }) -join ','
    Set-Content -LiteralPath (Join-Path $sourceDirectory "$Name.psd1") -Value "@{ ModuleVersion = '1.0.0'; RequiredModules = @($required); CmdletsToExport = @() }"
    $moduleText = "`$ErrorActionPreference = 'Stop'`n"
    foreach ($dependency in $Dependencies)
    {
        if ($Name -ne 'Az')
        {
            $moduleText += @"
`$loaded = Get-Module Az.$dependency
if (`$loaded -and `$loaded.Version -ne [version]'1.0.0') {
    throw 'This module requires Az.$dependency version 1.0.0. A different version is imported. Please open a new session.'
}
"@
        }
        $moduleText += "`nImport-Module Az.$dependency -RequiredVersion '1.0.0' -Global -ErrorAction Stop`n"
    }
    Set-Content -LiteralPath (Join-Path $moduleDirectory "$package.psm1") -Value $moduleText
    New-ModuleManifest -Path (Join-Path $moduleDirectory "$package.psd1") -RootModule "$package.psm1" `
        -ModuleVersion '1.0.0' -Author 'Fixture' -Description 'Offline dependency fixture' `
        -FunctionsToExport @() -CmdletsToExport @() -PowerShellVersion '7.0'
    $dependenciesXml = @($Dependencies | ForEach-Object { "<dependency id='Az.$_' version='1.0.0' />" }) -join ''
    Set-Content -LiteralPath (Join-Path $moduleDirectory "$package.nuspec") -Value @"
<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$package</id><version>1.0.0</version><authors>Fixture</authors><owners>Fixture</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance><description>Offline dependency fixture</description>
    <tags>PSModule</tags><dependencies>$dependenciesXml</dependencies>
  </metadata>
</package>
"@
    [IO.Compression.ZipFile]::CreateFromDirectory($moduleDirectory, (Join-Path $gallery "$package.1.0.0.nupkg"))
}

try
{
    New-FixturePackage Login @()
    New-FixturePackage Compute @('Login')
    New-FixturePackage Az @('Login', 'Compute')
    $sentinel = Join-Path $scratch 'NuGet.config'
    Set-Content -LiteralPath $sentinel -Value 'untouched caller configuration'
    $before = @(Get-PSRepository | Select-Object Name, SourceLocation | ConvertTo-Json -Compress)
    $hashes = @(Get-ChildItem -LiteralPath $gallery -File | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress)
    $output = & $pwsh -NoProfile -File $runner -RepositoryRoot $gallery -SourcePsRoot $source -PwshPath $pwsh -ReportRoot $reports 2>&1
    Assert-DependencyFixture ($LASTEXITCODE -eq 0) "Real resolver suite failed:`n$($output -join "`n")"
    $report = Get-Content -LiteralPath (Join-Path $reports 'dependency-tests.json') -Raw | ConvertFrom-Json
    Assert-DependencyFixture ($report.status -eq 'Succeeded') 'Successful suite has a successful report.'
    Assert-DependencyFixture ($report.cases.Count -eq 11) 'Metadata, three resolutions, six missing/incompatible edges, and conflict are all executed.'
    Assert-DependencyFixture (@($report.cases | Where-Object status -ne 'Succeeded').Count -eq 0) 'Expected dependency rejections are asserted passes.'
    $rollup = $report.cases | Where-Object name -eq 'resolve-Az'
    Assert-DependencyFixture ($rollup.observations[0].result.inventory.Count -eq 3) 'The rollup alone saves all three modules.'
    Assert-DependencyFixture ($rollup.observations[1].result.imported.Count -eq 3) 'Rollup import loads dependencies without preloading.'
    Assert-DependencyFixture (($before -join '') -ceq (@(Get-PSRepository | Select-Object Name, SourceLocation | ConvertTo-Json -Compress) -join '')) 'Existing repositories are unchanged.'
    Assert-DependencyFixture (($hashes -join '') -ceq (@(Get-ChildItem -LiteralPath $gallery -File | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress) -join '')) 'The source gallery is unchanged.'
    Assert-DependencyFixture ((Get-Content -LiteralPath $sentinel -Raw).Trim() -eq 'untouched caller configuration') 'Caller configuration is unchanged.'
    Write-Host 'PASS: real package resolution, missing/incompatible packages, conflicts, isolation and reporting'
    $count++

    $contracts = @(Get-PSDependencyContracts -SourcePsRoot $source -RepositoryRoot $gallery)
    $contracts[0].Dependencies = @(@{ Name = 'Az'; Version = '1.0.0' })
    $caught = $null
    try { Get-PSDependencyClosure -Name Az -Contracts $contracts | Out-Null }
    catch { $caught = $_ }
    Assert-DependencyFixture ($caught.Exception.Message -match 'Circular module dependency') 'Cycles fail rather than recurse indefinitely.'
    $count++

    $computePackage = Join-Path $gallery 'Az.Compute.1.0.0.nupkg'
    $archive = [IO.Compression.ZipFile]::Open($computePackage, [IO.Compression.ZipArchiveMode]::Update)
    try
    {
        $entry = $archive.GetEntry('Az.Compute.nuspec')
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $text = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $entry.Delete()
        $writer = [IO.StreamWriter]::new($archive.CreateEntry('Az.Compute.nuspec').Open())
        try { $writer.Write($text.Replace("version='1.0.0'", "version='2.0.0'")) }
        finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
    $output = & $pwsh -NoProfile -File $runner -RepositoryRoot $gallery -SourcePsRoot $source -PwshPath $pwsh -ReportRoot $reports 2>&1
    Assert-DependencyFixture ($LASTEXITCODE -ne 0) 'Incorrect NuGet dependency metadata fails the runner.'
    $report = Get-Content -LiteralPath (Join-Path $reports 'dependency-tests.json') -Raw | ConvertFrom-Json
    Assert-DependencyFixture ($report.status -eq 'Failed' -and $report.cases[0].message -match 'Az.Login') 'Metadata failures identify the dependency in the report.'
    Write-Host 'PASS: incorrect metadata and cycle rejection'
    $count++

    $worker = Join-Path $scratch 'slow-worker.ps1'
    Set-Content -LiteralPath $worker -Value "Start-Sleep -Seconds 30"
    $process = Invoke-PSDependencyProcess -PwshPath $pwsh -WorkerPath $worker -RequestPath unused -ResultPath unused -WorkingDirectory $scratch -TimeoutSeconds 1
    Assert-DependencyFixture $process.TimedOut 'A hung worker is timed out and terminated.'
    Set-Content -LiteralPath $worker -Value '[Console]::Out.WriteLine("STDOUT"); [Console]::Error.WriteLine("STDERR"); exit 42'
    $process = Invoke-PSDependencyProcess -PwshPath $pwsh -WorkerPath $worker -RequestPath unused -ResultPath unused -WorkingDirectory $scratch
    Assert-DependencyFixture ($process.ExitCode -eq 42 -and $process.Output -match 'STDOUT' -and $process.Output -match 'STDERR') 'Native exit and both streams are retained.'
    Write-Host 'PASS: bounded child execution and native diagnostics'
    $count++

    foreach ($result in @(
        @{ status = 'Succeeded'; errorId = $null; message = $null; diagnostics = @() },
        @{ status = 'Failed'; errorId = 'SourceNotFound,SavePackage'; message = 'Source not found'; diagnostics = @() },
        @{ status = 'Failed'; errorId = 'ProviderFailToDownloadFile,SavePackage'; message = 'Permission denied reading Az.Login'; diagnostics = @() },
        @{ status = 'Failed'; errorId = 'ProviderFailToDownloadFile,SavePackage'; message = "Unable to find dependent module 'Az.Other'"; diagnostics = @() }
    ))
    {
        $caught = $null
        try { Assert-PSDependencyFailure $result @{ Name = 'Az.Login'; Version = '1.0.0' } }
        catch { $caught = $_ }
        Assert-DependencyFixture ($null -ne $caught) 'Unexpected success and unrelated failures are not expected dependency rejection.'
    }
    Write-Host 'PASS: strict expected-rejection classification'
    $count++

    $ambientRoot = Join-Path $scratch 'ambient'
    $isolatedRoot = Join-Path $scratch 'isolated'
    $ambientModule = Join-Path $ambientRoot 'Az.Login/1.0.0'
    $null = New-Item -ItemType Directory -Path $ambientModule, $isolatedRoot -Force
    New-ModuleManifest -Path (Join-Path $ambientModule 'Az.Login.psd1') -ModuleVersion '1.0.0'
    $requestPath = Join-Path $scratch 'isolation-request.json'
    $resultPath = Join-Path $scratch 'isolation-result.json'
    . (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/phase_artifacts.ps1')
    Write-PSValidationJson -Path $requestPath -Value @{
        Operation = 'Import'; ModuleRoot = $isolatedRoot
        Module = @{ Name = 'Az.Login'; Version = '1.0.0'; Commands = @() }
        Closure = @(@{ Name = 'Az.Login'; Version = '1.0.0' }); Conflict = $false
    }
    $originalModulePath = $env:PSModulePath
    try
    {
        $env:PSModulePath = $ambientRoot + [IO.Path]::PathSeparator + $env:PSModulePath
        $process = Invoke-PSDependencyProcess -PwshPath $pwsh `
            -WorkerPath (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/dependency_worker.ps1') `
            -RequestPath $requestPath -ResultPath $resultPath -WorkingDirectory $scratch
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        Assert-DependencyFixture ($process.ExitCode -eq 1 -and $result.status -eq 'Failed') 'An ambient matching module cannot satisfy an isolated import.'
    }
    finally { $env:PSModulePath = $originalModulePath }
    $output = & $pwsh -NoProfile -File $runner -RepositoryRoot $gallery -SourcePsRoot $source -ReportRoot $gallery 2>&1
    Assert-DependencyFixture ($LASTEXITCODE -ne 0 -and ($output -join "`n") -match 'reports must be outside') 'Report paths cannot overwrite input packages.'
    Write-Host 'PASS: ambient modules and input report paths are rejected'
    $count++
}
finally { Remove-Item -LiteralPath $scratch -Recurse -Force }
Write-Host "PASS: $count PowerShell dependency fixture groups."
