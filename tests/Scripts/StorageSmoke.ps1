# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
Runs small live Storage smoke tests in resources owned exclusively by this run.
.DESCRIPTION
Build first. Authenticate BOTH the provisioning Azure CLI and the supplied Azclips
profile to the requested tenant/subscription before invoking with -AllowLive.
Requires permission to create resources and account-scoped data-role assignments.
Uses Standard LRS, a 1 GiB Files share and at most a few MiB of payload.
Always deletes its uniquely named/tagged resource group and verifies deletion.
Does not exercise the separately authorized 5 GiB performance acceptance.
.EXAMPLE
.\tests\Scripts\CLI\Storage.ps1 -TenantId <tenant> -SubscriptionId <subscription> -Profile smoke -AllowLive
.EXAMPLE
.\tests\Scripts\PS\Storage.ps1 -TenantId <tenant> -SubscriptionId <subscription> -Profile smoke -AllowLive
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('CLI', 'PowerShell')][string]$Surface,
    [Parameter(Mandatory)][guid]$TenantId,
    [Parameter(Mandatory)][guid]$SubscriptionId,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Profile,
    [ValidateSet('australiaeast', 'australiasoutheast')][string]$Location = 'australiaeast',
    [ValidateNotNullOrEmpty()][string]$ProvisioningCli = 'az',
    [switch]$AllowLive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $AllowLive) { throw 'Live resource creation requires explicit -AllowLive.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$cli = Join-Path $repo 'artifacts\Debug\Core.Cli\az.dll'
if (-not (Test-Path -LiteralPath $cli)) { throw 'Build azclips.slnx before running Storage acceptance.' }
$run = [guid]::NewGuid().ToString('N')
$group = "rg-azclips-storage-$($run.Substring(0, 12))"
$account = "clips$($run.Substring(0, 18))"
$sourceAccount = "${account}s"
$work = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "azclips-storage-$run"))
$attemptedCreation = $false
$ownsLocalDirectory = $false

function Invoke-Provisioning([string[]]$Arguments) {
    $scope = if ($Arguments[0] -eq 'ad') { @() } else { @('--subscription', $SubscriptionId.ToString()) }
    $text = & $ProvisioningCli @Arguments @scope --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) { throw "Provisioning command failed: $($Arguments[0..([Math]::Min(1, $Arguments.Length - 1))] -join ' ')." }
    if ($text) { $text -join "`n" | ConvertFrom-Json }
}

function Invoke-Storage([string[]]$Arguments) {
    $operation = $Arguments[0..([Math]::Min(1, $Arguments.Length - 1))] -join ' '
    Write-Host "Azclips smoke: $operation"
    $text = & dotnet $cli storage @Arguments --profile $Profile --output json
    if ($LASTEXITCODE -ne 0) { throw "Storage command failed: $operation." }
    if ($text) { $text -join "`n" | ConvertFrom-Json }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw $Message }
}

function Assert-File([string]$Path) {
    Assert-Equal (Get-FileHash -LiteralPath (Join-Path $work 'source.bin') -Algorithm SHA256).Hash `
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash 'Storage round-trip SHA-256 mismatch.'
}

function Wait-Copy([string]$Name) {
    for ($index = 0; $index -lt 60; $index++) {
        $blob = Invoke-Storage @('blob', 'show', '--account-name', $account, '-c', 'smoke', '-n', $Name)
        $status = $blob.properties.copy.status
        if ($status -eq 'success') { return }
        if ($status -ne 'pending') { throw "Copy acknowledgment became '$status'." }
        Start-Sleep -Seconds 2
    }
    throw 'The smoke-test copy did not complete within two minutes.'
}

function New-SmokeFileSas([string]$Key) {
    Invoke-Provisioning @('storage', 'file', 'generate-sas', '--account-name', $sourceAccount,
        '--account-key', $Key, '--share-name', 'smoke', '--path', 'dir/source.bin', '--permissions', 'r',
        '--start', [DateTime]::UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
        '--expiry', [DateTime]::UtcNow.AddHours(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), '--https-only')
}

try {
    $identity = Invoke-Provisioning @('account', 'show')
    Assert-Equal $TenantId.ToString() $identity.tenantId 'Provisioning identity belongs to a different tenant.'
    Assert-Equal $SubscriptionId.ToString() $identity.id 'Provisioning identity selected a different subscription.'
    $selected = & dotnet $cli profile show --name $Profile --output json
    if ($LASTEXITCODE -ne 0) { throw 'Authenticate the requested Azclips profile before running live acceptance.' }
    $selected = $selected -join "`n" | ConvertFrom-Json
    Assert-Equal $TenantId.ToString() $selected.tenantId 'The Azclips profile belongs to a different tenant.'
    Assert-Equal $SubscriptionId.ToString() $selected.subscriptionId 'The Azclips profile belongs to a different subscription.'
    $provider = Invoke-Provisioning @('provider', 'show', '--namespace', 'Microsoft.Storage')
    if ($provider.registrationState -ne 'Registered') {
        throw 'Register Microsoft.Storage explicitly before running smoke tests. Provider registration is a persistent subscription-level change.'
    }
    $principal = Invoke-Provisioning @('ad', 'signed-in-user', 'show')
    if (-not $principal.id) { throw 'Cannot resolve the signed-in user for least-privilege test role assignment.' }

    if (Invoke-Provisioning @('group', 'exists', '--name', $group)) { throw 'Refusing to reuse an existing resource group.' }
    Write-Host "Owned smoke resource group: $group; destination: $account; Files source: $sourceAccount; location: $Location"
    $attemptedCreation = $true
    $null = Invoke-Provisioning @('group', 'create', '--name', $group, '--location', $Location, '--tags', "azclips-storage-smoke=$run")
    $storage = Invoke-Provisioning @('storage', 'account', 'create', '--name', $account, '--resource-group', $group,
        '--location', $Location, '--kind', 'StorageV2', '--sku', 'Standard_LRS', '--min-tls-version', 'TLS1_2',
        '--allow-blob-public-access', 'false', '--allow-shared-key-access', 'false')
    $fileStorage = Invoke-Provisioning @('storage', 'account', 'create', '--name', $sourceAccount, '--resource-group', $group,
        '--location', $Location, '--kind', 'StorageV2', '--sku', 'Standard_LRS', '--min-tls-version', 'TLS1_2',
        '--allow-blob-public-access', 'false', '--allow-shared-key-access', 'true')
    foreach ($assignment in @(
        @{ Role = 'Storage Blob Data Owner'; Scope = $storage.id },
        @{ Role = 'Storage File Data Privileged Contributor'; Scope = $fileStorage.id }
    )) {
        $null = Invoke-Provisioning @('role', 'assignment', 'create', '--assignee-object-id', $principal.id,
            '--assignee-principal-type', 'User', '--role', $assignment.Role, '--scope', $assignment.Scope)
    }
    $null = Invoke-Provisioning @('storage', 'container-rm', 'create', '--storage-account', $account, '--resource-group', $group, '--name', 'smoke')
    $null = Invoke-Provisioning @('storage', 'share-rm', 'create', '--storage-account', $sourceAccount, '--resource-group', $group,
        '--name', 'smoke', '--quota', '1', '--enabled-protocols', 'SMB')
    Start-Sleep -Seconds 30
    $null = New-Item -ItemType Directory -Path $work
    $ownsLocalDirectory = $true
    $source = Join-Path $work 'source.bin'
    $bytes = [byte[]]::new(1024 * 1024)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    [IO.File]::WriteAllBytes($source, $bytes)
    $page = Join-Path $work 'page.vhd'
    [IO.File]::WriteAllBytes($page, [byte[]]::new(512))
    if ($Surface -eq 'CLI') {
        $null = Invoke-Storage @('blob', 'upload', '--account-name', $account, '-c', 'smoke', '-n', 'source.bin', '-f', $source, '--validate-content')
        $null = Invoke-Storage @('blob', 'upload', '--account-name', $account, '-c', 'smoke', '-n', 'page', '-f', $page, '--type', 'page')
        $null = Invoke-Storage @('blob', 'upload', '--account-name', $account, '-c', 'smoke', '-n', 'append', '-f', $source, '--type', 'append')
        $exists = Invoke-Storage @('blob', 'exists', '--account-name', $account, '-c', 'smoke', '-n', 'source.bin')
        Assert-Equal $true $exists.exists 'Uploaded blob was not found.'
        $listing = @(Invoke-Storage @('blob', 'list', '--account-name', $account, '-c', 'smoke', '--num-results', '*'))
        if ('source.bin' -notin $listing.name) { throw 'Blob listing omitted the uploaded resource.' }
        $null = Invoke-Storage @('blob', 'download', '--account-name', $account, '-c', 'smoke', '-n', 'source.bin', '-f', (Join-Path $work 'download.bin'), '--check-md5')
        Assert-File (Join-Path $work 'download.bin')
        $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
        foreach ($argument in @($cli, 'storage', 'blob', 'download', '--account-name', $account, '-c', 'smoke', '-n', 'source.bin', '--profile', $Profile)) { $start.ArgumentList.Add($argument) }
        $start.UseShellExecute = $false
        $start.RedirectStandardOutput = $true
        $process = [Diagnostics.Process]::Start($start)
        try {
            $output = [IO.File]::Create((Join-Path $work 'stdout.bin'))
            try { $process.StandardOutput.BaseStream.CopyTo($output) } finally { $output.Dispose() }
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) { throw 'Binary stdout download failed.' }
        } finally { $process.Dispose() }
        Assert-File (Join-Path $work 'stdout.bin')
        $batch = Join-Path $work 'batch'
        $null = New-Item -ItemType Directory -Path $batch
        $null = Invoke-Storage @('blob', 'download-batch', '--account-name', $account, '-s', 'smoke', '-d', $batch, '--pattern', 'source.*', '--dryrun')
        if (Get-ChildItem -LiteralPath $batch) { throw 'Dry run unexpectedly wrote files.' }
        $null = Invoke-Storage @('blob', 'download-batch', '--account-name', $account, '-s', 'smoke', '-d', $batch, '--pattern', 'source.*')
        Assert-File (Join-Path $batch 'source.bin')
        $null = Invoke-Storage @('blob', 'set-tier', '--account-name', $account, '-c', 'smoke', '-n', 'source.bin', '--tier', 'Cool')
        $copy = Invoke-Storage @('blob', 'copy', 'start', '--account-name', $account, '--destination-container', 'smoke',
            '--destination-blob', 'copy.bin', '--source-container', 'smoke', '--source-blob', 'source.bin')
        Assert-Equal 'copyId,copyStatus,etag,lastModified' ($copy.PSObject.Properties.Name -join ',') 'Copy JSON acknowledgment shape changed.'
        Wait-Copy 'copy.bin'
        $null = Invoke-Storage @('blob', 'download', '--account-name', $account, '-c', 'smoke', '-n', 'copy.bin', '-f', (Join-Path $work 'copy.bin'))
        Assert-File (Join-Path $work 'copy.bin')
        $null = Invoke-Storage @('directory', 'create', '--account-name', $sourceAccount, '--share-name', 'smoke', '--name', 'dir', '--enable-file-backup-request-intent')
        $null = Invoke-Storage @('file', 'upload', '--account-name', $sourceAccount, '--share-name', 'smoke', '--source', $source, '--path', 'dir/source.bin', '--enable-file-backup-request-intent')
        $null = Invoke-Storage @('file', 'download', '--account-name', $sourceAccount, '--share-name', 'smoke', '--path', 'dir/source.bin',
            '--dest', (Join-Path $work 'file.bin'), '--enable-file-backup-request-intent')
        Assert-File (Join-Path $work 'file.bin')
        $null = Invoke-Storage @('file', 'list', '--account-name', $sourceAccount, '--share-name', 'smoke', '--path', 'dir', '--enable-file-backup-request-intent')
        $null = Invoke-Storage @('directory', 'list', '--account-name', $sourceAccount, '--share-name', 'smoke', '--enable-file-backup-request-intent')
        $keys = @(Invoke-Storage @('account', 'keys', 'list', '--resource-group', $group, '--account-name', $account, '--subscription-id', $SubscriptionId.ToString()))
        if ($keys.Count -lt 1) { throw 'ARM key listing returned no key records.' }
        $null = Invoke-Storage @('blob', 'delete', '--account-name', $account, '-c', 'smoke', '-n', 'copy.bin')
        $sourceKeys = @(Invoke-Storage @('account', 'keys', 'list', '--resource-group', $group,
            '--account-name', $sourceAccount, '--subscription-id', $SubscriptionId.ToString()))
        $sourceKey = ($sourceKeys | Where-Object keyName -eq 'key1' | Select-Object -First 1).value
        $sas = New-SmokeFileSas $sourceKey
        foreach ($mode in @('lookup', 'key', 'sas')) {
            $authorization = switch ($mode) {
                'lookup' { @('--use-source-account-key-lookup', '--source-key-resource-group', $group) }
                'key' { @('--source-account-key', $sourceKey) }
                'sas' { @('--source-sas', $sas, '--source-account-key', 'ignored-invalid-key',
                    '--use-source-account-key-lookup', '--source-key-resource-group', "$group-unused") }
            }
            $name = "file-copy-$mode.bin"
            Write-Host "Azclips CLI smoke: Files-source copy using $mode"
            $arguments = @('blob', 'copy', 'start', '--account-name', $account, '--destination-container', 'smoke',
                '--destination-blob', $name, '--source-account-name', $sourceAccount,
                '--source-share', 'smoke', '--source-path', 'dir/source.bin') + $authorization
            $null = Invoke-Storage $arguments
            Wait-Copy $name
            $null = Invoke-Storage @('blob', 'download', '--account-name', $account, '-c', 'smoke', '-n', $name, '-f', (Join-Path $work $name))
            Assert-File (Join-Path $work $name)
        }
    }
    else {
        Import-Module (Join-Path $repo 'src\PS\Accounts\Accounts.psd1')
        Import-Module (Join-Path $repo 'src\PS\Storage\Storage.psd1')
        $context = New-AzStorageContext -StorageAccountName $account -Profile $Profile -EnableFileBackupRequestIntent
        $fileContext = New-AzStorageContext -StorageAccountName $sourceAccount -Profile $Profile -EnableFileBackupRequestIntent
        Write-Host 'Azclips PS smoke: Blob content and listing'
        $blob = Set-AzStorageBlobContent -File $source -Container smoke -Blob source.bin -Context $context -Force -Confirm:$false
        $null = $blob | Get-AzStorageBlobContent -Destination (Join-Path $work 'ps-blob.bin') -Force -Confirm:$false
        Assert-File (Join-Path $work 'ps-blob.bin')
        $null = Get-AzStorageBlob -Context $context -Container smoke -Blob 'source*'
        Write-Host 'Azclips PS smoke: Blob-source copy'
        $null = Start-AzStorageBlobCopy -SrcContainer smoke -SrcBlob source.bin -Context $context -DestContainer smoke -DestBlob copy.bin -DestContext $context -Force -Confirm:$false
        Wait-Copy 'copy.bin'
        $null = Get-AzStorageBlobContent -Context $context -Container smoke -Blob copy.bin -Destination (Join-Path $work 'ps-copy.bin') -Force -Confirm:$false
        Assert-File (Join-Path $work 'ps-copy.bin')
        Write-Host 'Azclips PS smoke: Files directory, content and listing'
        $null = New-AzStorageDirectory -ShareName smoke -Path dir -Context $fileContext -Confirm:$false
        $file = Set-AzStorageFileContent -Source $source -ShareName smoke -Path dir/source.bin -Context $fileContext -Force -PassThru -Confirm:$false
        $null = $file | Get-AzStorageFileContent -Destination (Join-Path $work 'ps-file.bin') -Force -PassThru -Confirm:$false
        Assert-File (Join-Path $work 'ps-file.bin')
        $null = Get-AzStorageFile -ShareName smoke -Context $fileContext
        $null = Get-AzStorageFile -ShareName smoke -Path dir/source.bin -Context $fileContext
        Write-Host 'Azclips PS smoke: ARM keys and Blob deletion'
        $keys = @(Get-AzStorageAccountKey -ResourceGroupName $group -Name $account -SubscriptionId $SubscriptionId.ToString() -Profile $Profile)
        if ($keys.Count -lt 1) { throw 'ARM key listing returned no key records.' }
        $removed = Remove-AzStorageBlob -Context $context -Container smoke -Blob copy.bin -Force -PassThru -Confirm:$false
        Assert-Equal $true $removed 'PS Blob removal did not acknowledge success.'
        $sourceKeys = @(Get-AzStorageAccountKey -ResourceGroupName $group -Name $sourceAccount -SubscriptionId $SubscriptionId.ToString() -Profile $Profile)
        $sourceKey = ($sourceKeys | Where-Object KeyName -eq 'key1' | Select-Object -First 1).Value
        $sas = New-SmokeFileSas $sourceKey
        foreach ($mode in @('lookup', 'key', 'sas')) {
            $authorization = switch ($mode) {
                'lookup' { @{ UseSourceAccountKeyLookup = $true; SourceKeyResourceGroup = $group } }
                'key' { @{ SourceAccountKey = $sourceKey } }
                'sas' { @{ SourceSas = $sas; SourceAccountKey = 'ignored-invalid-key';
                    UseSourceAccountKeyLookup = $true; SourceKeyResourceGroup = "$group-unused" } }
            }
            $name = "file-copy-$mode.bin"
            Write-Host "Azclips PS smoke: Files-source copy using $mode"
            $null = $file | Start-AzStorageBlobCopy -DestContext $context -DestContainer smoke -DestBlob $name @authorization -Force -Confirm:$false
            Wait-Copy $name
            $null = Get-AzStorageBlobContent -Context $context -Container smoke -Blob $name -Destination (Join-Path $work $name) -Force -Confirm:$false
            Assert-File (Join-Path $work $name)
        }
    }
    Write-Host "$Surface Storage smoke passed; removing all resources created by this run."
}
finally {
    try {
        if ($attemptedCreation -and (Invoke-Provisioning @('group', 'exists', '--name', $group))) {
            $created = Invoke-Provisioning @('group', 'show', '--name', $group)
            if ($created.tags.'azclips-storage-smoke' -ne $run) { throw "Refusing to delete resource group without this run's ownership tag: $group" }
            $null = Invoke-Provisioning @('group', 'delete', '--name', $group, '--yes')
            if (Invoke-Provisioning @('group', 'exists', '--name', $group)) { throw "Cleanup incomplete: resource group $group still exists." }
            Write-Host "Verified Azure cleanup: $group"
        }
    }
    finally {
        if ($ownsLocalDirectory) { Remove-Item -LiteralPath (Resolve-Path -LiteralPath $work).Path -Recurse -Force }
    }
}
