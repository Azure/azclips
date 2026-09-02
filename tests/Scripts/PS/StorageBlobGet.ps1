# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live assertion checks for: Get-AzStorageBlob
# Prereqs: build, sign in to azclips and Azure CLI, and create the test container.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoRoot 'src/PS/Login/Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'src/PS/Storage/Storage.psd1') -Force

$subscription = $env:AZCLIPS_SUBSCRIPTION_ID ?? 'f758ac53-3e63-4317-a956-0997793808d7'
$account = $env:AZCLIPS_STORAGE_ACCOUNT ?? 'mytestsa52379'
$container = $env:AZCLIPS_STORAGE_CONTAINER ?? 'azclips-manual-test'
$azureCli = $env:AZURE_CLI ?? 'az'
$source = $PSCommandPath
$blob = "azclips-ps-get-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).ps1"
$testError = $null
$cleanupError = $null

try {
    Connect-AzAccount -SubscriptionId $subscription | Out-Null
    Set-AzStorageBlobContent `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -FilePath $source `
        -Metadata @{ owner = 'get-test' } `
        -Tags 'kind=get-test' `
        -ErrorAction Stop | Out-Null

    $listed = @(Get-AzStorageBlob `
        -AccountName $account `
        -ContainerName $container `
        -Prefix $blob `
        -IncludeTag `
        -ErrorAction Stop)
    if ($listed.Count -ne 1 -or
        $listed[0].Name -ne $blob -or
        $listed[0].Metadata['owner'] -ne 'get-test' -or
        $listed[0].Tags['kind'] -ne 'get-test') {
        throw "Unexpected list result: $($listed | ConvertTo-Json -Compress)"
    }

    $shown = Get-AzStorageBlob `
        -AccountName $account `
        -ContainerName $container `
        -BlobName $blob `
        -IncludeTag `
        -ErrorAction Stop
    if ($shown.Name -ne $blob -or
        $shown.ContentLength -ne (Get-Item $source).Length -or
        $shown.Tags['kind'] -ne 'get-test') {
        throw "Unexpected get result: $($shown | ConvertTo-Json -Compress)"
    }

    Write-Host 'Storage Blob PowerShell get assertions passed.' -ForegroundColor Green
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
