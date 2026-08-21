storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name StorageBlobUploadDownload.sh `
    --file tests/Scripts/CLI/StorageBlobUploadDownload.sh `
    --overwrite `
    --debug

storage blob download `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name StorageBlobUploadDownload.sh `
    --file artifacts/StorageBlobUploadDownload.downloaded.sh `
    --overwrite `
    --debug
