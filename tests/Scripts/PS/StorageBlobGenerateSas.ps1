# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Generate a user delegation SAS for an Azure Storage blob - PowerShell
# Prereq: build Debug, import Login and Storage, sign in, and create the test container.

Import-Module .\src\PS\Login\Login.psd1 -Force
Import-Module .\src\PS\Storage\Storage.psd1 -Force

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$source = $PSCommandPath
$blob = "azclips-sas-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"
$blobUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$blob"

Connect-AzAccount -SubscriptionId $sub | Out-Null

$upload = Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -FilePath $source `
    -ErrorAction Stop
if ($upload.Operation -ne 'upload' -or
    $upload.Destination -ne $blobUri -or
    $upload.ContentLength -ne (Get-Item $source).Length) {
    throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
}

$uri = New-AzStorageBlobSASToken `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $blob `
    -Permission r `
    -ExpiryTime (Get-Date).ToUniversalTime().AddHours(1) `
    -HttpsOnly `
    -FullUri
if ($uri -notlike "$blobUri?*" -or
    $uri -notmatch '[?&]sp=r(?:&|$)' -or
    $uri -notmatch '[?&]spr=https(?:&|$)' -or
    $uri -notmatch '[?&]sig=[^&]+') {
    throw "Unexpected SAS result: $uri"
}

if ((Invoke-WebRequest -Uri $uri).Content -ne [System.IO.File]::ReadAllText($source)) {
    throw 'Expected the SAS URI to return the uploaded blob content.'
}

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
