# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: Set-AzStorageBlobContent
# Prereqs: build, sign in, and create the test container.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'src/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'src/PS/Storage/Storage.psd1') -Force

$sub = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$source = Join-Path $repoRoot 'tests/Scripts/PS/StorageBlobUpload.ps1'
$blob = "azclips-ps-upload-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).ps1"
$uri = "https://$account.blob.core.windows.net/$container/$blob"

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

# Answer No to the overwrite prompt; no transfer result should be emitted.
$duplicate = Set-AzStorageBlobContent `
    -AccountName $account `
    -ContainerName $container `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop
if ($null -ne $duplicate) {
    throw 'Duplicate upload without -Force unexpectedly produced a transfer result.'
}

Write-Host 'Storage Blob PowerShell upload assertions passed.' -ForegroundColor Green
