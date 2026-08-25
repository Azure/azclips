# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Delete an Azure Storage blob - PowerShell
# Prereq: build Debug, import Login and Storage, sign in, and create the test container.

Import-Module .\src\PS\Login\Login.psd1 -Force
Import-Module .\src\PS\Storage\Storage.psd1 -Force

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$source = $PSCommandPath
$blob = "azclips-delete-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"

Connect-AzAccount -SubscriptionId $sub | Out-Null
Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $source | Out-Null

$deleted = Remove-AzStorageBlob `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -Force `
    -PassThru `
    -Confirm:$false
if (-not $deleted) {
    throw 'Expected Remove-AzStorageBlob to return true.'
}

$remaining = @(Get-AzStorageBlob `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -Prefix $blob)
if ($remaining.Count -ne 0) {
    throw 'Expected blob to be deleted.'
}
