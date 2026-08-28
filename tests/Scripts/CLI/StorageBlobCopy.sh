# Start an Azure Storage blob copy - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobCopy.sh'
$sourceBlob = "azclips-copy-source-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"
$destinationBlob = "azclips-copy-destination-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"
$sourceUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$sourceBlob"
$destinationUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$destinationBlob"

storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $sourceBlob `
    --file $source | Out-Null

$result = storage blob copy start `
    --account-name mytestsa52379 `
    --destination-container azclips-manual-test `
    --destination-blob $destinationBlob `
    --source-uri $sourceUri | ConvertFrom-Json
if ($result.destinationUri -ne $destinationUri -or
    -not $result.copyId -or
    $result.copyStatus -notin 'pending', 'success' -or
    -not $result.eTag) {
    throw "Unexpected copy result: $($result | ConvertTo-Json -Compress)"
}
$result
