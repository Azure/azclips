# Delete an Azure Storage blob - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobDelete.sh'
$blob = "azclips-delete-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"

storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source | Out-Null

storage blob delete `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob

$exists = storage blob exists `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob | ConvertFrom-Json
if ($exists) {
    throw 'Expected blob to be deleted.'
}
