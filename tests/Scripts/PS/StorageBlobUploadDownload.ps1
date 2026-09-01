# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Live assertion checks for: Set-AzStorageBlobContent / Get-AzStorageBlobContent
#
# Prereq: build the solution in Debug (the committed manifests reference
# artifacts/Debug), import Login FIRST, then Storage, then Connect-AzAccount.
#   dotnet build azclips.slnx --tl:off
#
# Optional:
#   AZCLIPS_SUBSCRIPTION_ID     - defaults to the shared test subscription
#   AZCLIPS_STORAGE_ACCOUNT     - defaults to the shared test account
#   AZCLIPS_STORAGE_CONTAINER   - defaults to the shared test container

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'src/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'src/PS/Storage/Storage.psd1') -Force

$sub = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$source = Join-Path $repoRoot 'tests/Scripts/PS/StorageBlobUploadDownload.ps1'
$destination = Join-Path $repoRoot 'artifacts/StorageBlobUploadDownload.downloaded.ps1'
$blob = "azclips-ps-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).ps1"
$uri = "https://$account.blob.core.windows.net/$container/$blob"

Remove-Item $destination -Force -ErrorAction SilentlyContinue

Connect-AzAccount -SubscriptionId $sub | Out-Null

$upload = Set-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop

if ($upload.Operation -ne 'upload' -or
    $upload.Destination -ne $uri -or
    $upload.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
}

# The cmdlet prompts on a duplicate upload. Answer No; the assertion verifies no transfer result was emitted.
$duplicate = Set-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop
if ($null -ne $duplicate) {
    throw 'Duplicate upload without -Force unexpectedly produced a transfer result.'
}

$download = Get-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $destination `
    -ErrorAction Stop

if ($download.Operation -ne 'download' -or
    $download.Source -ne $uri -or
    $download.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected download result: $($download | ConvertTo-Json -Compress)"
}

if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
    throw 'Downloaded bytes do not match uploaded bytes.'
}

# The cmdlet prompts when the destination exists. Answer No; assert the file and result remain unchanged.
$hashBefore = (Get-FileHash $destination).Hash
$existing = Get-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $destination `
    -ErrorAction Stop
if ($null -ne $existing -or (Get-FileHash $destination).Hash -ne $hashBefore) {
    throw 'Download without -Force unexpectedly replaced the existing file.'
}

Remove-Item $destination -Force
Write-Host 'Storage Blob PowerShell assertions passed.' -ForegroundColor Green
