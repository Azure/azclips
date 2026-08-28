# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Start an Azure Storage blob copy - PowerShell
# Prereq: build Debug, import Login and Storage, sign in, and create the test container.

Import-Module .\src\PS\Login\Login.psd1 -Force
Import-Module .\src\PS\Storage\Storage.psd1 -Force

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$source = $PSCommandPath
$sourceBlob = "azclips-copy-source-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"
$destinationBlob = "azclips-copy-destination-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).ps1"
$sourceUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$sourceBlob"
$destinationUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$destinationBlob"

Connect-AzAccount -SubscriptionId $sub | Out-Null
Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName $sourceBlob `
    -FilePath $source | Out-Null

$result = Start-AzStorageBlobCopy `
    -AccountName mytestsa52379 `
    -DestContainer azclips-manual-test `
    -DestBlob $destinationBlob `
    -AbsoluteUri $sourceUri `
    -Confirm:$false
if ($result.DestinationUri -ne $destinationUri -or
    -not $result.CopyId -or
    $result.CopyStatus -notin 'pending', 'success' -or
    -not $result.ETag) {
    throw "Unexpected copy result: $($result | ConvertTo-Json -Compress)"
}
$result
