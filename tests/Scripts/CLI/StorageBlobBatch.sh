# Upload and download a batch of Azure Storage blobs - CLI
# Prereq: build Debug, sign in, and create the test container.

$prefix = "azclips-batch-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$source = "artifacts/$prefix-source"
$destination = "artifacts/$prefix-destination"
New-Item -ItemType Directory -Path $source, $destination | Out-Null
Set-Content -Path "$source/one.txt" -Value 'one' -NoNewline
Set-Content -Path "$source/two.txt" -Value 'two' -NoNewline
Set-Content -Path "$source/ignored.log" -Value 'ignored' -NoNewline

$uploads = @(storage blob upload-batch `
    --account-name mytestsa52379 `
    --destination azclips-manual-test `
    --source $source `
    --destination-path $prefix `
    --pattern '*.txt' | ConvertFrom-Json)
if ($uploads.Count -ne 2 -or
    @($uploads | Where-Object Operation -ne 'upload').Count -ne 0) {
    throw "Unexpected upload-batch result: $($uploads | ConvertTo-Json -Compress)"
}

$downloads = @(storage blob download-batch `
    --account-name mytestsa52379 `
    --source azclips-manual-test `
    --destination $destination `
    --pattern "$prefix/*.txt" | ConvertFrom-Json)
if ($downloads.Count -ne 2 -or
    @($downloads | Where-Object Operation -ne 'download').Count -ne 0) {
    throw "Unexpected download-batch result: $($downloads | ConvertTo-Json -Compress)"
}

foreach ($name in 'one.txt', 'two.txt') {
    if ((Get-FileHash "$source/$name").Hash -ne
        (Get-FileHash "$destination/$prefix/$name").Hash) {
        throw "Downloaded bytes for $name do not match."
    }
    storage blob delete `
        --account-name mytestsa52379 `
        --container-name azclips-manual-test `
        --name "$prefix/$name"
}

Remove-Item $source, $destination -Recurse -Force
