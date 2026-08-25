# List, show, and test existence of an Azure Storage blob - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobGet.sh'
$blob = "azclips-get-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"

storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source | Out-Null

$listed = storage blob list `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --prefix $blob | ConvertFrom-Json
if ($listed.Count -ne 1 -or $listed[0].name -ne $blob) {
    throw "Blob was not returned by list: $($listed | ConvertTo-Json -Compress)"
}

$shown = storage blob show `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob | ConvertFrom-Json
if ($shown.name -ne $blob -or $shown.contentLength -ne (Get-Item $source).Length) {
    throw "Unexpected show result: $($shown | ConvertTo-Json -Compress)"
}

$exists = storage blob exists `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob | ConvertFrom-Json
if (-not $exists) {
    throw 'Expected blob to exist.'
}
