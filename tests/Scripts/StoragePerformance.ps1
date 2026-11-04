# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
Measures actual 5 GiB Blob round trips on both hosts and Files round trips on CLI.
.DESCRIPTION
Requires explicit -AllowLive and prior sign-ins for Azure CLI, Azclips CLI and
PowerShell. Owns one temporary Australian resource group and Standard LRS account
with Shared Key disabled. Uses four workers, 8 MiB Blob / 4 MiB Files chunks.
Warms each host with a 1 MiB round trip before taking its idle memory baseline.
CLI measurements invoke the real CLI entrypoint in a warm dotnet host using the
CLI runtime configuration. Each measured operation runs in a fresh child process.
No forced GC, heap limits or working-set trimming are used. Memory is sampled at
25 ms and also checked against the OS lifetime peak (a conservative upper bound).
Throughput is reported, not asserted. The incremental memory limit is 256 MiB.
Always cleans up owned Azure resources and local payloads, including on failure.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][guid]$TenantId,
    [Parameter(Mandatory)][guid]$SubscriptionId,
    [Parameter(Mandatory)][string]$CliProfile,
    [Parameter(Mandatory)][string]$PowerShellProfile,
    [Parameter(Mandatory)][string]$ResultPath,
    [ValidateSet('australiaeast', 'australiasoutheast')][string]$Location = 'australiaeast',
    [string]$ProvisioningCli = 'az',
    [ValidateNotNullOrEmpty()][ValidateSet('Blob', 'Files')][string[]]$Service = @('Blob', 'Files'),
    [switch]$AllowLive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $AllowLive) { throw 'The 5 GiB live test requires explicit -AllowLive.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$cliDir = Join-Path $repo 'artifacts\Debug\Core.Cli'
$cli = Join-Path $cliDir 'az.dll'
if (-not (Test-Path -LiteralPath $cli)) { throw 'Build azclips.slnx before performance acceptance.' }
$resultFile = [IO.Path]::GetFullPath($ResultPath)
if (Test-Path -LiteralPath $resultFile) { throw 'Choose a new result file; existing results are never overwritten.' }
$run = [guid]::NewGuid().ToString('N')
$group = "rg-azclips-perf-$($run.Substring(0, 12))"
$account = "clipsperf$($run.Substring(0, 15))"
$work = Join-Path ([IO.Path]::GetTempPath()) "azclips-storage-perf-$run"
$hostAssembly = Join-Path $cliDir "StoragePerformanceHost.$run.dll"
$attemptedCreation = $false
$ownsLocalDirectory = $false
$ownsHostAssembly = $false
$rows = [Collections.Generic.List[object]]::new()
$report = [ordered]@{
    Run = $run; ResourceGroup = $group; Account = $account; Location = $Location
    PayloadBytes = 5L * 1024 * 1024 * 1024; MemoryLimitBytes = 256L * 1024 * 1024
    Workers = 4; BlobChunkBytes = 8388608; FilesChunkBytes = 4194304
    Baseline = 'Median of 20 idle working-set samples after a 1 MiB warmup round trip; no GC/heap overrides.'
    CliHost = 'Actual CLI entrypoint in warm dotnet process with original CLI runtimeconfig/deps.'
    Peak = 'Maximum of 25 ms samples and OS lifetime peak, including warmup.'
    Sha256 = $null; Results = @(); TransfersPassed = $false
    AzureCleanupVerified = $false; LocalCleanupVerified = $false
}

function Invoke-Provisioning([string[]]$Arguments) {
    $scope = if ($Arguments[0] -eq 'ad') { @() } else { @('--subscription', $SubscriptionId.ToString()) }
    $text = & $ProvisioningCli @Arguments @scope --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) { throw "Provisioning command failed: $($Arguments[0..1] -join ' ')." }
    if ($text) { $text -join "`n" | ConvertFrom-Json }
}

function Save-Report {
    $report.Results = $rows.ToArray()
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultFile -Encoding utf8
}

function Cli-Transfer([string]$Kind, [string]$Direction, [string]$Name, [string]$Source, [string]$Destination) {
    $arguments = if ($Kind -eq 'Blob') {
        if ($Direction -eq 'Upload') {
            @('storage','blob','upload','--account-name',$account,'-c','perf','-n',$Name,'-f',$Source,
                '--overwrite','--max-connections','4','--chunk-size','8388608','--no-progress')
        } else {
            @('storage','blob','download-batch','--account-name',$account,'-s','perf','--pattern',$Name,
                '-d',(Split-Path -Parent $Destination),
                '--max-connections','4','--chunk-size','8388608','--no-progress')
        }
    } else {
        if ($Direction -eq 'Upload') {
            @('storage','file','upload','--account-name',$account,'--share-name','perf','--path',$Name,
                '--source',$Source,'--max-connections','4','--no-progress','--enable-file-backup-request-intent')
        } else {
            @('storage','file','download','--account-name',$account,'--share-name','perf','--path',$Name,
                '--dest',$Destination,'--max-connections','4','--no-progress','--enable-file-backup-request-intent')
        }
    }
    ,($arguments + @('--profile',$CliProfile,'--output','json'))
}

try {
    $identity = Invoke-Provisioning @('account','show')
    if ($identity.tenantId -ne $TenantId.ToString() -or $identity.id -ne $SubscriptionId.ToString()) {
        throw 'Provisioning tenant/subscription does not match the requested scope.'
    }
    foreach ($profile in @($CliProfile, $PowerShellProfile)) {
        $text = & dotnet $cli profile show --name $profile --output json
        if ($LASTEXITCODE -ne 0) { throw "Authenticate the requested profile: $profile" }
        $selected = $text -join "`n" | ConvertFrom-Json
        if ($selected.tenantId -ne $TenantId.ToString() -or $selected.subscriptionId -ne $SubscriptionId.ToString()) {
            throw 'A benchmark profile does not match the requested tenant/subscription.'
        }
    }
    $provider = Invoke-Provisioning @('provider','show','--namespace','Microsoft.Storage')
    if ($provider.registrationState -ne 'Registered') { throw 'Register Microsoft.Storage explicitly before this test.' }
    if ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($work).TrimEnd('\',':'))).Free -lt 16GB) {
        throw 'At least 16 GiB free local disk space is required.'
    }
    if (Test-Path -LiteralPath $work) { throw 'Refusing to reuse a local benchmark directory.' }
    $null = New-Item -ItemType Directory -Path $work
    $ownsLocalDirectory = $true
    dotnet build (Join-Path $PSScriptRoot 'StoragePerformanceHost\StoragePerformanceHost.csproj') `
        --nologo --disable-build-servers --verbosity minimal --output (Join-Path $work 'host')
    if ($LASTEXITCODE -ne 0) { throw 'Performance measurement host build failed.' }
    if (Test-Path -LiteralPath $hostAssembly) { throw 'Refusing to replace an existing measurement host.' }
    Copy-Item -LiteralPath (Join-Path $work 'host\Azclips.Storage.PerformanceHost.dll') -Destination $hostAssembly
    $ownsHostAssembly = $true

    $principal = Invoke-Provisioning @('ad','signed-in-user','show')
    if (-not $principal.id) { throw 'Cannot resolve the signed-in user for scoped role assignments.' }
    if (Invoke-Provisioning @('group','exists','--name',$group)) { throw 'Refusing to reuse an Azure resource group.' }
    Write-Host "Owned performance group: $group; account: $account; location: $Location"
    $attemptedCreation = $true
    $null = Invoke-Provisioning @('group','create','--name',$group,'--location',$Location,'--tags',"azclips-storage-perf=$run")
    $storage = Invoke-Provisioning @('storage','account','create','--name',$account,'--resource-group',$group,
        '--location',$Location,'--kind','StorageV2','--sku','Standard_LRS','--min-tls-version','TLS1_2',
        '--allow-blob-public-access','false','--allow-shared-key-access','false')
    foreach ($role in @('Storage Blob Data Contributor','Storage File Data Privileged Contributor')) {
        $null = Invoke-Provisioning @('role','assignment','create','--assignee-object-id',$principal.id,
            '--assignee-principal-type','User','--role',$role,'--scope',$storage.id)
    }
    $null = Invoke-Provisioning @('storage','container-rm','create','--storage-account',$account,
        '--resource-group',$group,'--name','perf')
    if ('Files' -in $Service) {
        $null = Invoke-Provisioning @('storage','share-rm','create','--storage-account',$account,
            '--resource-group',$group,'--name','perf','--quota','16','--enabled-protocols','SMB')
    }
    Start-Sleep -Seconds 30
    $source = Join-Path $work 'source.bin'
    $warmSource = Join-Path $work 'warm.bin'
    $buffer = [byte[]]::new(1MB)
    $stream = [IO.File]::Open($source, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        for ($offset = 0L; $offset -lt $report.PayloadBytes; $offset += $buffer.Length) {
            [Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
            $stream.Write($buffer, 0, $buffer.Length)
        }
    } finally { $stream.Dispose() }
    [IO.File]::WriteAllBytes($warmSource, $buffer)
    if ((Get-Item -LiteralPath $source).Length -ne $report.PayloadBytes) { throw 'Payload is not exactly 5 GiB.' }
    $report.Sha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Save-Report
    foreach ($kind in $Service) {
        $surfaces = if ($kind -eq 'Blob') { @('CLI','PowerShell') } else { @('CLI') }
        foreach ($surface in $surfaces) {
            foreach ($direction in @('Upload','Download')) {
                $id = "$kind-$surface-$direction"
                $destination = Join-Path $work "$id.bin"
                $warmDestination = Join-Path $work "$id-warm.bin"
                $warmName = "$($id.ToLowerInvariant())-warm.bin"
                if ($kind -eq 'Blob' -and $surface -eq 'CLI') {
                    $batchDirectory = Join-Path $work "$id-batch"
                    $warmDirectory = Join-Path $work "$id-warm"
                    $null = New-Item -ItemType Directory -Path $batchDirectory, $warmDirectory
                    $destination = Join-Path $batchDirectory 'large.bin'
                    $warmDestination = Join-Path $warmDirectory $warmName
                }
                $metricsPath = Join-Path $work "$id-metrics.json"
                $planPath = Join-Path $work "$id-plan.json"
                Write-Host "Starting 5 GiB $id"
                if ($surface -eq 'CLI') {
                    $plan = @{
                        Warmup = @((Cli-Transfer $kind Upload $warmName $warmSource $warmDestination),
                            (Cli-Transfer $kind Download $warmName $warmSource $warmDestination))
                        Command = Cli-Transfer $kind $direction 'large.bin' $source $destination
                        MetricsPath = $metricsPath
                    }
                    $plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding utf8
                    & dotnet exec --runtimeconfig (Join-Path $cliDir 'az.runtimeconfig.json') `
                        --depsfile (Join-Path $cliDir 'az.deps.json') $hostAssembly $planPath
                } else {
                    @{
                        Repo=$repo; HostAssembly=$hostAssembly; Profile=$PowerShellProfile; Account=$account
                        Service=$kind; Direction=$direction; Name='large.bin'; Source=$source; Destination=$destination
                        WarmName=$warmName; WarmSource=$warmSource; WarmDestination=$warmDestination; MetricsPath=$metricsPath
                    } | ConvertTo-Json | Set-Content -LiteralPath $planPath -Encoding utf8
                    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'StoragePerformanceWorker.ps1') -PlanPath $planPath
                }
                if ($LASTEXITCODE -ne 0) { throw "5 GiB transfer failed: $id" }
                $metrics = Get-Content -LiteralPath $metricsPath -Raw | ConvertFrom-Json
                $hash = $null
                if ($direction -eq 'Download') {
                    if ((Get-Item -LiteralPath $destination).Length -ne $report.PayloadBytes) {
                        throw "Downloaded length is not exactly 5 GiB: $id"
                    }
                    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
                    if ($hash -cne $report.Sha256) { throw "5 GiB SHA256 mismatch: $id" }
                    Remove-Item -LiteralPath $destination -Force
                }
                $row = [ordered]@{
                    Surface=$surface; Service=$kind; Direction=$direction; Bytes=$report.PayloadBytes
                    BaselineBytes=$metrics.BaselineWorkingSetBytes; PeakBytes=$metrics.PeakWorkingSetBytes
                    IncrementalBytes=$metrics.IncrementalWorkingSetBytes; Samples=$metrics.SampleCount
                    Seconds=$metrics.ElapsedSeconds; MiBPerSecond=5120 / $metrics.ElapsedSeconds; Sha256=$hash
                    MemoryPassed=$metrics.IncrementalWorkingSetBytes -le $report.MemoryLimitBytes
                }
                $rows.Add($row)
                Save-Report
                Write-Host ("Completed {0}: {1:N2} MiB/s; incremental memory {2:N2} MiB; memory passed: {3}" -f `
                    $id,$row.MiBPerSecond,($row.IncrementalBytes / 1MB),$row.MemoryPassed)
            }
        }
    }
    $expectedTransfers = 0
    if ('Blob' -in $Service) { $expectedTransfers += 4 }
    if ('Files' -in $Service) { $expectedTransfers += 2 }
    $report.TransfersPassed = $rows.Count -eq $expectedTransfers -and `
        @($rows | Where-Object { -not $_.MemoryPassed }).Count -eq 0
    if (-not $report.TransfersPassed) { throw 'SHA256 round trips completed, but a transfer exceeded the 256 MiB incremental working-set limit.' }
}
finally {
    try {
        if ($attemptedCreation -and (Invoke-Provisioning @('group','exists','--name',$group))) {
            $created = Invoke-Provisioning @('group','show','--name',$group)
            if ($created.tags.'azclips-storage-perf' -ne $run) { throw 'Refusing to delete resources without this run ownership tag.' }
            $null = Invoke-Provisioning @('group','delete','--name',$group,'--yes')
            if (Invoke-Provisioning @('group','exists','--name',$group)) { throw "Azure cleanup incomplete: $group" }
            $report.AzureCleanupVerified = $true
            Write-Host "Verified performance Azure cleanup: $group"
        }
    }
    finally {
        if ($ownsHostAssembly) { Remove-Item -LiteralPath $hostAssembly -Force }
        if ($ownsLocalDirectory) { Remove-Item -LiteralPath $work -Recurse -Force }
        $report.LocalCleanupVerified = -not (Test-Path -LiteralPath $work) -and -not (Test-Path -LiteralPath $hostAssembly)
        Save-Report
    }
}
