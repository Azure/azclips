$source = [IO.Path]::GetTempFileName()
$destination = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllBytes($source, [byte[]](0, 1, 2, 255))
    Remove-Item $destination
    $blob = "azclips-ps-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()).bin"

    Set-AzStorageBlobContent `
        -AccountName $env:AZCLIPS_STORAGE_ACCOUNT `
        -Container $env:AZCLIPS_STORAGE_CONTAINER `
        -Blob $blob `
        -File $source

    Get-AzStorageBlobContent `
        -AccountName $env:AZCLIPS_STORAGE_ACCOUNT `
        -Container $env:AZCLIPS_STORAGE_CONTAINER `
        -Blob $blob `
        -Destination $destination

    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($source)) -ne
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($destination))) {
        throw 'Downloaded bytes do not match uploaded bytes.'
    }
}
finally {
    Remove-Item $source, $destination -Force -ErrorAction SilentlyContinue
}
