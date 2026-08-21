# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Set-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName StorageBlobUploadDownload.ps1 `
    -FilePath tests/Scripts/PS/StorageBlobUploadDownload.ps1 `
    -Force `
    -Debug

Get-AzStorageBlobContent `
    -AccountName mytestsa52379 `
    -ContainerName azclips-manual-test `
    -BlobName StorageBlobUploadDownload.ps1 `
    -FilePath artifacts/StorageBlobUploadDownload.downloaded.ps1 `
    -Force `
    -Debug
