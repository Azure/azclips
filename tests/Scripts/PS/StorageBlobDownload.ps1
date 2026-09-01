# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: Get-AzStorageBlobContent
# Prereqs: build, sign in, and create the test container.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'src/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'src/PS/Storage/Storage.psd1') -Force

$sub = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$source = Join-Path $repoRoot 'tests/Scripts/PS/StorageBlobDownload.ps1'
$destination = Join-Path $repoRoot 'artifacts/StorageBlobDownload.downloaded.ps1'
$blob = "azclips-ps-download-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).ps1"
$uri = "https://$account.blob.core.windows.net/$container/$blob"

Remove-Item $destination -Force -ErrorAction SilentlyContinue
Connect-AzAccount -SubscriptionId $sub | Out-Null

# Arrange an independently downloadable blob.
Set-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop | Out-Null

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

# Answer No to the overwrite prompt; the destination must remain unchanged.
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
Write-Host 'Storage Blob PowerShell download assertions passed.' -ForegroundColor Green
