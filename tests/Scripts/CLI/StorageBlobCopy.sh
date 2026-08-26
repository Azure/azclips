# Start an Azure Storage blob copy - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobCopy.sh'
$sourceBlob = "azclips-copy-source-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"
$destinationBlob = "azclips-copy-destination-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"

storage blob upload --account-name mytestsa52379 --container-name azclips-manual-test --name $sourceBlob --file $source | Out-Null

$sourceUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$sourceBlob"
$result = storage blob copy start --account-name mytestsa52379 --destination-container azclips-manual-test --destination-blob $destinationBlob --source-uri $sourceUri | ConvertFrom-Json
if (-not $result.copyId) {
    throw 'Expected a copy ID.'
}
