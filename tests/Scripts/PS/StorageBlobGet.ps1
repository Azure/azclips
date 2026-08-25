# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Get one or more Azure Storage blobs - PowerShell
# Prereq: build Debug, import Login and Storage, sign in, and create the test container.

Import-Module .\src\PS\Login\Login.psd1 -Force
Import-Module .\src\PS\Storage\Storage.psd1 -Force

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$source = $PSCommandPath
$blob = "azclips-get-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"

Connect-AzAccount -SubscriptionId $sub | Out-Null
Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $source | Out-Null

$listed = @(Get-AzStorageBlob `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -Prefix $blob)
if ($listed.Count -ne 1 -or $listed[0].Name -ne $blob) {
    throw "Blob was not returned by list: $($listed | ConvertTo-Json -Compress)"
}

$shown = Get-AzStorageBlob `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob
if ($shown.Name -ne $blob -or $shown.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected get result: $($shown | ConvertTo-Json -Compress)"
}
