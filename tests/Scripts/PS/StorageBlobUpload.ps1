# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: Set-AzStorageBlobContent
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'artifacts/Debug/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts/Debug/PS/Storage/Storage.psd1') -Force

$subscription = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$azureCli = $env:AZURE_CLI ?? 'az'
$blob = "azclips-ps-upload-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).bin"
$source = Join-Path (Join-Path $repoRoot 'artifacts') $blob
$uri = "https://$account.blob.core.windows.net/$container/$blob"
$testError = $null
$cleanupError = $null

try {
    [IO.File]::WriteAllBytes($source, [byte[]](0, 1, 2, 127, 128, 255, 13, 10))
    Connect-AzAccount -SubscriptionId $subscription | Out-Null

    $upload = Set-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $source `
        -ErrorAction Stop

    if ($upload.Operation -ne 'upload' -or
        $upload.Destination -ne $uri -or
        $upload.ContentLength -ne (Get-Item $source).Length) {
        throw "Unexpected upload result: $($upload | ConvertTo-Json -Compress)"
    }
    Write-Host 'Upload result:' -ForegroundColor Cyan
    $upload | Format-List | Out-Host

    $duplicateFailed = $false
    try {
        Set-AzStorageBlobContent `
            -AccountName $account `
            -ContainerName $container `
            -BlobName $blob `
            -FilePath $source `
            -ErrorAction Stop | Out-Null
    }
    catch {
        if ($_ -notmatch 'BlobAlreadyExists|already exists') {
            throw
        }
        $duplicateFailed = $true
    }
    if (!$duplicateFailed) {
        throw 'Duplicate upload without -Force unexpectedly succeeded.'
    }

    [IO.File]::WriteAllBytes($source, [byte[]](0, 1, 2, 127, 128, 255, 13, 10, 3))
    $overwrite = Set-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $source `
        -Force `
        -ErrorAction Stop
    if ($overwrite.ContentLength -ne (Get-Item $source).Length) {
        throw "Unexpected overwrite result: $($overwrite | ConvertTo-Json -Compress)"
    }
    Write-Host 'Overwrite result:' -ForegroundColor Cyan
    $overwrite | Format-List | Out-Host

    Write-Host 'Storage Blob PowerShell upload assertions passed.' -ForegroundColor Green
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
    Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue
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
