# Upload and download an Azure Storage blob - CLI
# Equivalent of: az storage blob upload / az storage blob download
#
# Prereq: build the solution in Debug, sign in, and create the test container.
#   dotnet build azclips.slnx --tl:off
#
# Shared test context:
#   storage account: mytestsa52379
#   container: azclips-manual-test

$source = 'tests/Scripts/CLI/StorageBlobUploadDownload.sh'
$destination = 'artifacts/StorageBlobUploadDownload.downloaded.sh'
$blob = "azclips-cli-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"
$uri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$blob"

# Happy path: upload this script. Assert the returned operation, destination, and byte count.
$upload = storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source | ConvertFrom-Json

if ($upload.operation -ne 'upload' -or
    $upload.destination -ne $uri -or
    $upload.contentLength -ne (Get-Item $source).Length) {
    throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
}
$upload

# Negative case: uploading the same blob without --overwrite must fail.
storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source

# Happy path: download the blob. Assert returned metadata and the downloaded bytes.
$download = storage blob download `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $destination `
    --overwrite | ConvertFrom-Json

if ($download.operation -ne 'download' -or
    $download.source -ne $uri -or
    $download.contentLength -ne (Get-Item $source).Length) {
    throw "Unexpected download result: $($download | ConvertTo-Json -Compress)"
}
$download

if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
    throw 'Downloaded bytes do not match uploaded bytes.'
}

# Negative case: downloading onto an existing file without --overwrite must fail.
storage blob download `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $destination
