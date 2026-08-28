# Set an Azure Storage blob access tier - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobSetTier.sh'
$blob = "azclips-tier-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"

storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source | Out-Null

storage blob set-tier `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --tier Cool

$shown = storage blob show `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob | ConvertFrom-Json
if ($shown.name -ne $blob -or $shown.accessTier -ne 'Cool') {
    throw "Unexpected blob after setting tier: $($shown | ConvertTo-Json -Compress)"
}

storage blob delete `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob
