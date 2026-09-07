# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: Get-AzStorageBlobContent
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'artifacts/Debug/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts/Debug/PS/Storage/Storage.psd1') -Force

$subscription = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$azureCli = $env:AZURE_CLI ?? 'az'
$blob = "azclips-ps-download-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).bin"
$source = Join-Path (Join-Path $repoRoot 'artifacts') $blob
$destination = "$source.downloaded"
$uri = "https://$account.blob.core.windows.net/$container/$blob"
$testError = $null
$cleanupError = $null

try {
    [IO.File]::WriteAllBytes($source, [byte[]](0, 1, 2, 127, 128, 255, 13, 10))
    Connect-AzAccount -SubscriptionId $subscription | Out-Null

    Set-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $source `
        -ErrorAction Stop | Out-Null

    $download = Get-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $destination `
        -ErrorAction Stop

    if ($download.Operation -ne 'download' -or
        $download.Source -ne $uri -or
        $download.ContentLength -ne (Get-Item $source).Length) {
        throw "Unexpected download result: $($download | ConvertTo-Json -Compress)"
    }
    if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
        throw 'Downloaded bytes do not match uploaded bytes.'
    }
    Write-Host 'Download result:' -ForegroundColor Cyan
    $download | Format-List | Out-Host

    [IO.File]::WriteAllText($destination, 'wrong content')
    $overwrite = Get-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $destination `
        -Force `
        -ErrorAction Stop
    if ((Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
        throw 'Download with -Force did not replace the destination.'
    }
    Write-Host 'Overwrite result:' -ForegroundColor Cyan
    $overwrite | Format-List | Out-Host

    Write-Host 'Storage Blob PowerShell download assertions passed.' -ForegroundColor Green
}
catch {
    $testError = $_
}
finally {
    try {
        & $azureCli storage blob delete `
            --account-name $account `
            --container-name $container `
            --name $blob `
            --auth-mode login `
            --only-show-errors | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to delete test blob '$blob'."
        }
        [string]$remaining = & $azureCli storage blob exists `
            --account-name $account `
            --container-name $container `
            --name $blob `
            --auth-mode login `
            --query exists `
            --output tsv `
            --only-show-errors
        if ($LASTEXITCODE -ne 0 -or $remaining.Trim() -ne 'false') {
            throw "Test blob '$blob' still exists after cleanup."
        }
    }
    catch {
        $cleanupError = $_
    }
    Remove-Item -LiteralPath $source, $destination -Force -ErrorAction SilentlyContinue
}

if ($null -ne $testError) {
    if ($null -ne $cleanupError) {
        Write-Warning $cleanupError
    }
    throw $testError
}
if ($null -ne $cleanupError) {
    throw $cleanupError
}
