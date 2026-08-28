# Generate a user delegation SAS for an Azure Storage blob - CLI
# Prereq: build Debug, sign in, and create the test container.

$source = 'tests/Scripts/CLI/StorageBlobGenerateSas.sh'
$destination = 'artifacts/StorageBlobGenerateSas.downloaded.sh'
$blob = "azclips-sas-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).sh"
$blobUri = "https://mytestsa52379.blob.core.windows.net/azclips-manual-test/$blob"

$upload = storage blob upload `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --file $source | ConvertFrom-Json
if ($upload.operation -ne 'upload' -or
    $upload.destination -ne $blobUri -or
    $upload.contentLength -ne (Get-Item $source).Length) {
    throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
}

$uri = storage blob generate-sas `
    --account-name mytestsa52379 `
    --container-name azclips-manual-test `
    --name $blob `
    --permissions r `
    --expiry (Get-Date).ToUniversalTime().AddHours(1).ToString('yyyy-MM-ddTHH:mm:ssZ') `
    --https-only `
    --full-uri | ConvertFrom-Json

if ($uri -notlike "$blobUri?*" -or
    $uri -notmatch '[?&]sp=r(?:&|$)' -or
    $uri -notmatch '[?&]spr=https(?:&|$)' -or
    $uri -notmatch '[?&]sig=[^&]+') {
    throw "Unexpected SAS result: $uri"
}

Invoke-WebRequest -Uri $uri -OutFile $destination
if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
    throw 'Downloaded bytes do not match uploaded bytes.'
}
Remove-Item $destination -Force

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
