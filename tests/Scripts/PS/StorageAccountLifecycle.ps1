# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Storage account lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, get, list, and delete.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID             - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP        - resource group name; defaults to timestamped test group
#   AZCLIPS_STORAGE_ACCOUNT_NAME  - storage account name; defaults to timestamped lowercase name
#   AZCLIPS_LOCATION              - Azure region; defaults to eastus
#   AZCLIPS_KEEP_STORAGE_ACCOUNT  - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG            - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\StorageAccountLifecycle.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Storage\Storage.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'eastus' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyMMddHHmmss'
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-storage-it-$stamp" } else { $env:AZCLIPS_RESOURCE_GROUP }
$storageAccountName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_ACCOUNT_NAME)) { "azclipssa$stamp" } else { $env:AZCLIPS_STORAGE_ACCOUNT_NAME }

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "New-AzResourceGroup $resourceGroup"
    New-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $resourceGroup `
        -Location $location | Out-Host

    Section "New-AzStorageAccount $storageAccountName"
    New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName `
        -Location $location `
        -SkuName Standard_LRS `
        -Kind StorageV2 | Out-Host

    Section 'Get-AzStorageAccount by name'
    Get-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName | Out-Host

    Section 'Get-AzStorageAccount list by resource group'
    Get-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup | Out-Host

    Section 'Get-AzStorageAccount list by subscription'
    Get-AzStorageAccount -SubscriptionId $subscriptionId | Out-Host

    Section 'Remove-AzStorageAccount'
    Remove-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName `
        -Confirm:$false

    Write-Host ''
    Write-Host 'All storage account lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_STORAGE_ACCOUNT -ne '1') {
        Section 'Remove-AzStorageAccount cleanup'
        try {
            Remove-AzStorageAccount `
                -SubscriptionId $subscriptionId `
                -ResourceGroupName $resourceGroup `
                -Name $storageAccountName `
                -Confirm:$false `
                -ErrorAction Stop
        } catch {
            Write-Warning "Storage account cleanup failed: $($_.Exception.Message)"
        }
    }

    if ($env:AZCLIPS_CLEANUP_RG -eq '1') {
        Section 'Invoke-AzRestMethod cleanup resource group'
        try {
            Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "/subscriptions/$subscriptionId/resourcegroups/$resourceGroup?api-version=2025-04-01" `
                -ErrorAction Stop | Out-Host
        } catch {
            Write-Warning "Resource group cleanup failed: $($_.Exception.Message)"
        }
    }
}