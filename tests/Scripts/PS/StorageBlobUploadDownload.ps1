# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Upload and download an Azure Storage blob - PowerShell
# Equivalent of: Set-AzStorageBlobContent / Get-AzStorageBlobContent
#
# Prereq: build the solution in Debug (the committed manifests reference
# artifacts/Debug), import Login FIRST, then Storage, then Connect-AzAccount.
#   dotnet build azclips.slnx --tl:off
#
# Shared test context:
#   subscription: f758ac53-3e63-4317-a956-0997793808d7
#   storage account: mytestsa52379
#   container: azclips-manual-test

Import-Module .\src\PS\Login\Login.psd1 -Force
Import-Module .\src\PS\Storage\Storage.psd1 -Force

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source = Join-Path $repoRoot 'tests/Scripts/PS/StorageBlobUploadDownload.ps1'
$destination = Join-Path $repoRoot 'artifacts/StorageBlobUploadDownload.downloaded.ps1'
$blob = "azclips-ps-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"
$uri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$blob"

Connect-AzAccount -SubscriptionId $sub | Out-Null

# Happy path: upload this script. Assert the returned operation, destination, and byte count.
$upload = Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop

if ($upload.Operation -ne 'upload' -or
    $upload.Destination -ne $uri -or
    $upload.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
}
$upload

# Negative case: uploading the same blob without -Force must fail.
Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $source

# Happy path: download the blob. Assert returned metadata and the downloaded bytes.
$download = Get-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $destination `
    -Force `
    -ErrorAction Stop

if ($download.Operation -ne 'download' -or
    $download.Source -ne $uri -or
    $download.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected download result: $($download | ConvertTo-Json -Compress)"
}
$download

if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
    throw 'Downloaded bytes do not match uploaded bytes.'
}

# Negative case: downloading onto an existing file without -Force must fail.
Get-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $destination
