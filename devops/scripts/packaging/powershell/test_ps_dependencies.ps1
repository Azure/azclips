<#
.SYNOPSIS
Validates local package dependency resolution and import contracts in isolated directories.

.DESCRIPTION
Save-Module exercises PowerShellGet dependency resolution without installing into user
module paths. Consumers are requested alone and imported in fresh, restricted workers.
Negative cases require a dependency-specific error; unrelated failures and timeouts fail.
The input gallery and existing repositories/modules are never changed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$SourcePsRoot = (Join-Path $PSScriptRoot '../../../../src/PS'),
    [string]$PwshPath = 'pwsh',
    [string]$ReportRoot,
    [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared/dependency_packages.ps1')
. (Join-Path $PSScriptRoot 'shared/phase_artifacts.ps1')
. (Join-Path $PSScriptRoot 'shared/module_artifacts.ps1')
$repository = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).ProviderPath
$source = (Resolve-Path -LiteralPath $SourcePsRoot -ErrorAction Stop).ProviderPath
if ($ReportRoot)
{
    $ReportRoot = [IO.Path]::GetFullPath($ReportRoot)
    Assert-AzArtifactNoLinks -Path $ReportRoot
    foreach ($inputRoot in $repository, $source)
    {
        if (Test-AzArtifactPathWithin -Path $ReportRoot -Root $inputRoot)
        {
            throw 'Dependency reports must be outside the source modules and package gallery.'
        }
    }
}
$scratch = Join-Path ([IO.Path]::GetTempPath()) "azclips-dependencies-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch
$report = [ordered]@{
    schemaVersion = 1
    status = 'Running'
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    finishedAtUtc = $null
    errorId = $null
    message = $null
    client = 'PowerShellGet/Save-Module'
    cases = [Collections.Generic.List[object]]::new()
}
$failure = $null
$registeredRepositories = [Collections.Generic.List[string]]::new()
$originalDirectory = [Environment]::CurrentDirectory
$originalLocation = Get-Location

function Save-DependencyReport
{
    if ($ReportRoot)
    {
        New-AzArtifactDirectory -Path $ReportRoot
        $reportPath = Join-Path $ReportRoot 'dependency-tests.json'
        Assert-AzArtifactNoLinks -Path $reportPath
        Write-PSValidationJson -Path $reportPath -Value $report
        Set-AzArtifactReadable -Path $reportPath
    }
}

function Invoke-DependencyCase
{
    param([string]$Name, [scriptblock]$Body)
    $case = [ordered]@{
        name = $Name; status = 'Running'
        startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        finishedAtUtc = $null; message = $null
        observations = [Collections.Generic.List[object]]::new()
    }
    $report.cases.Add($case)
    Save-DependencyReport
    try
    {
        & $Body $case
        $case.status = 'Succeeded'
        Write-Host "PASS: dependency $Name"
    }
    catch
    {
        $case.status = 'Failed'
        $case.message = $_.Exception.Message
        Write-Host "FAIL: dependency $Name`: $($case.message)"
    }
    finally
    {
        $case.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Save-DependencyReport
    }
}

function Invoke-DependencyWorker
{
    param([System.Collections.IDictionary]$Case, [hashtable]$Request, [string]$CaseRoot)
    $id = [guid]::NewGuid().ToString('N')
    $requestPath = Join-Path $CaseRoot "$id-request.json"
    $resultPath = Join-Path $CaseRoot "$id-result.json"
    Write-PSValidationJson -Path $requestPath -Value $Request
    $process = Invoke-PSDependencyProcess -PwshPath $PwshPath `
        -WorkerPath (Join-Path $PSScriptRoot 'shared/dependency_worker.ps1') `
        -RequestPath $requestPath -ResultPath $resultPath -WorkingDirectory $CaseRoot -TimeoutSeconds $TimeoutSeconds
    $observation = [ordered]@{
        operation = $Request.Operation; exitCode = $process.ExitCode
        timedOut = $process.TimedOut; output = $process.Output; result = $null
    }
    $Case.observations.Add($observation)
    if ($process.TimedOut) { throw "Dependency $($Request.Operation) timed out after $TimeoutSeconds seconds." }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Dependency worker exited $($process.ExitCode) without a result: $($process.Output)" }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $observation.result = $result
    if ($result.status -notin 'Succeeded', 'Failed' -or
        ($result.status -eq 'Succeeded' -and $process.ExitCode -ne 0) -or
        ($result.status -eq 'Failed' -and $process.ExitCode -ne 1))
    {
        throw "Invalid dependency worker result or exit code: $($process.ExitCode)"
    }
    return $result
}

function New-DependencyRepository
{
    param([string]$Path)
    $name = "AzclipsDependency-$([guid]::NewGuid().ToString('N'))"
    Register-PSRepository -Name $name -SourceLocation $Path -PublishLocation $Path `
        -InstallationPolicy Trusted -PackageManagementProvider NuGet -ErrorAction Stop | Out-Null
    $registeredRepositories.Add($name)
    return $name
}

try
{
    Set-Location -LiteralPath $scratch
    [Environment]::CurrentDirectory = $scratch
    Save-DependencyReport
    $script:contracts = @()
    Invoke-DependencyCase 'package-metadata' {
        param($case)
        $script:contracts = @(Get-PSDependencyContracts -SourcePsRoot $source -RepositoryRoot $repository)
        if ($contracts.Count -eq 0) { throw 'No source module dependency contracts were found.' }
        foreach ($contract in $contracts) { $null = @(Get-PSDependencyClosure -Name $contract.Name -Contracts $contracts) }
        $case.observations.Add(@{ packages = $contracts })
    }
    if ($report.cases[0].status -ne 'Succeeded') { throw 'Package dependency metadata validation failed.' }
    Import-Module PowerShellGet -ErrorAction Stop
    $completeRepository = New-DependencyRepository $repository
    foreach ($contract in $contracts)
    {
        $closure = @(Get-PSDependencyClosure -Name $contract.Name -Contracts $contracts)
        Invoke-DependencyCase "resolve-$($contract.Name)" {
            param($case)
            $caseRoot = Join-Path $scratch $case.name
            $moduleRoot = Join-Path $caseRoot 'modules'
            $null = New-Item -ItemType Directory -Path $moduleRoot -Force
            $request = @{
                Operation = 'Resolve'; Module = $contract; Closure = $closure
                RepositoryName = $completeRepository; ModuleRoot = $moduleRoot; Conflict = $false
            }
            $resolved = Invoke-DependencyWorker $case $request $caseRoot
            if ($resolved.status -ne 'Succeeded') { throw "Automatic resolution failed: [$($resolved.errorId)] $($resolved.message)" }
            foreach ($expected in $closure)
            {
                if (@($resolved.inventory | Where-Object { $_.name -eq $expected.Name -and $_.version -eq $expected.Version }).Count -ne 1)
                {
                    throw "Automatic resolution did not save $($expected.Name) $($expected.Version)."
                }
            }
            $request.Operation = 'Import'
            $imported = Invoke-DependencyWorker $case $request $caseRoot
            if ($imported.status -ne 'Succeeded') { throw "Import without preloading failed: [$($imported.errorId)] $($imported.message)" }
        }

        foreach ($dependency in $contract.Dependencies)
        {
            foreach ($scenario in 'missing', 'incompatible')
            {
                Invoke-DependencyCase "$scenario-$($contract.Name)-$($dependency.Name)" {
                    param($case)
                    $caseRoot = Join-Path $scratch $case.name
                    $galleryRoot = Join-Path $caseRoot 'gallery'
                    $moduleRoot = Join-Path $caseRoot 'modules'
                    $null = New-Item -ItemType Directory -Path $galleryRoot, $moduleRoot -Force
                    foreach ($package in $contracts | Where-Object Name -ne $dependency.Name)
                    {
                        Copy-Item -LiteralPath $package.PackagePath -Destination $galleryRoot -ErrorAction Stop
                    }
                    if ($scenario -eq 'incompatible')
                    {
                        if ([version]$dependency.Version -le [version]'0.0.0') { throw "Cannot create an older version than $($dependency.Name) $($dependency.Version)." }
                        $dependencyPackage = $contracts | Where-Object Name -eq $dependency.Name
                        New-PSIncompatibleDependencyPackage -SourcePath $dependencyPackage.PackagePath `
                            -DestinationPath (Join-Path $galleryRoot "$($dependency.Name).0.0.0.nupkg")
                    }
                    $name = New-DependencyRepository $galleryRoot
                    $request = @{
                        Operation = 'Resolve'; Module = $contract; Closure = $closure
                        RepositoryName = $name; ModuleRoot = $moduleRoot; Conflict = $false
                    }
                    $resolved = Invoke-DependencyWorker $case $request $caseRoot
                    Assert-PSDependencyFailure $resolved $dependency
                }
            }
        }
        if ($contract.Name -ne 'Az' -and $contract.Dependencies.Count -gt 0)
        {
            $dependency = $contract.Dependencies[0]
            Invoke-DependencyCase "loaded-conflict-$($contract.Name)-$($dependency.Name)" {
                param($case)
                $caseRoot = Join-Path $scratch $case.name
                $moduleRoot = Join-Path $caseRoot 'modules'
                $null = New-Item -ItemType Directory -Path $moduleRoot -Force
                $request = @{
                    Operation = 'Resolve'; Module = $contract; Closure = $closure
                    RepositoryName = $completeRepository; ModuleRoot = $moduleRoot; Conflict = $false
                }
                $resolved = Invoke-DependencyWorker $case $request $caseRoot
                if ($resolved.status -ne 'Succeeded') { throw "Conflict setup failed: $($resolved.message)" }
                $conflictPath = Join-Path $caseRoot "$($dependency.Name).psd1"
                $conflictVersion = [version]::new(([version]$dependency.Version).Major + 1, 0, 0)
                New-ModuleManifest -Path $conflictPath -ModuleVersion $conflictVersion -FunctionsToExport @() -CmdletsToExport @()
                $request.Operation = 'Import'
                $request.Conflict = $true
                $request.ConflictPath = $conflictPath
                $conflict = Invoke-DependencyWorker $case $request $caseRoot
                if ($conflict.status -ne 'Failed' -or
                    $conflict.message -notmatch [regex]::Escape("$($dependency.Name) version $($dependency.Version)") -or
                    $conflict.message -notmatch 'different version|new session')
                {
                    throw "Expected loaded-dependency version conflict, got [$($conflict.errorId)] $($conflict.message)"
                }
            }
        }
    }
}
catch
{
    $failure = $_
    $report.errorId = $_.FullyQualifiedErrorId
    $report.message = $_.Exception.Message
}
finally
{
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    foreach ($name in $registeredRepositories)
    {
        try { Unregister-PSRepository -Name $name -ErrorAction Stop | Out-Null }
        catch { $cleanupErrors.Add($_.Exception.Message) }
    }
    Set-Location -LiteralPath $originalLocation.Path
    [Environment]::CurrentDirectory = $originalDirectory
    try { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction Stop }
    catch { $cleanupErrors.Add($_.Exception.Message) }
    if ($cleanupErrors.Count -gt 0)
    {
        $report.cases.Add(@{ name = 'cleanup'; status = 'Failed'; message = $cleanupErrors -join '; ' })
    }
    $report.status = if ($failure -or @($report.cases | Where-Object status -ne 'Succeeded').Count -gt 0) { 'Failed' } else { 'Succeeded' }
    $report.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    try { Save-DependencyReport }
    catch
    {
        if ($failure) { Write-Warning "Unable to finalize dependency report: $($_.Exception.Message)" }
        else { $failure = $_ }
    }
}
if ($failure) { throw $failure }
if ($report.status -ne 'Succeeded') { throw "PowerShell dependency validation failed: $(($report.cases | Where-Object status -ne 'Succeeded').name -join ', ')" }
