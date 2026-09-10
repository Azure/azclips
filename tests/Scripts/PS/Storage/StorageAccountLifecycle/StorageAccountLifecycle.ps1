# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Storage account lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, get/list, and remove.
#
# Upstream evidence from remote main:
#   Azure PowerShell: azure-powershell/src/Storage/Storage.Management.Test/ScenarioTests/StorageAccountTests.ps1
#     - Test-NewAzureStorageAccount validates New-AzStorageAccount then Get-AzStorageAccount with name/SKU/location/kind assertions.
#     - Test-GetAzureStorageAccount validates named get and resource-group list.
#     - Test-RemoveAzureStorageAccount validates Remove-AzStorageAccount cleanup.
#   Azure CLI: azure-cli/src/azure-cli/azure/cli/command_modules/storage/tests/latest/test_storage_account_scenarios.py
#     - test_create_storage_account covers create/show/list/delete lifecycle for storage accounts.
#
# Prereqs:
#   1. Build and stage modules: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID            - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP       - resource group name; defaults to timestamped test group
#   AZCLIPS_STORAGE_ACCOUNT_NAME - storage account name; defaults to timestamped lowercase name
#   AZCLIPS_LOCATION             - Azure region; defaults to westus2
#   AZCLIPS_KEEP_STORAGE         - set to 1 to skip storage account cleanup
#   AZCLIPS_CLEANUP_RG           - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\Storage\StorageAccountLifecycle\StorageAccountLifecycle.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Storage\Storage.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyMMddHHmmss'
$suffix = Get-Random -Minimum 10 -Maximum 99
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-storage-it-$stamp-$suffix" } else { $env:AZCLIPS_RESOURCE_GROUP }
$storageAccountName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_STORAGE_ACCOUNT_NAME)) { "azclipssa$stamp$suffix" } else { $env:AZCLIPS_STORAGE_ACCOUNT_NAME }
$storageAccountId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$storageAccountName"
$skuName = 'Standard_LRS'
$kind = 'StorageV2'

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Normalize-Location($value) {
    if ($null -eq $value) {
        return ''
    }

    return $value.ToString().ToLowerInvariant().Replace(' ', '')
}

function Assert-Equal($expected, $actual, $message) {
    if ($expected -ne $actual) {
        throw "ASSERTION FAILED: $message. Expected '$expected', got '$actual'."
    }
}

function Assert-NotNull($value, $message) {
    if ($null -eq $value) {
        throw "ASSERTION FAILED: $message."
    }
}

function Assert-StorageAccountMatches($account, $message) {
    Assert-NotNull $account "$message returned no storage account"
    Assert-Equal $storageAccountName $account.Name "$message name"
    Assert-Equal $storageAccountId $account.Id "$message id"
    Assert-Equal (Normalize-Location $location) (Normalize-Location $account.Location) "$message location"
    Assert-Equal $skuName $account.Sku.Name "$message SKU"
    Assert-Equal $kind $account.Kind "$message kind"
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
    $createdAccount = New-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName `
        -Location $location `
        -SkuName $skuName `
        -Kind $kind
    Assert-StorageAccountMatches $createdAccount 'New-AzStorageAccount'

    Section "Get-AzStorageAccount named $storageAccountName"
    $shownAccount = Get-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName
    Assert-StorageAccountMatches $shownAccount 'Get-AzStorageAccount -Name'

    Section 'Get-AzStorageAccount list by resource group'
    $resourceGroupAccounts = @(Get-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup)
    $listedResourceGroupAccount = $resourceGroupAccounts | Where-Object { $_.Name -eq $storageAccountName } | Select-Object -First 1
    Assert-StorageAccountMatches $listedResourceGroupAccount 'Get-AzStorageAccount -ResourceGroupName list'

    Section 'Get-AzStorageAccount subscription list'
    $subscriptionAccounts = @(Get-AzStorageAccount -SubscriptionId $subscriptionId)
    $listedSubscriptionAccount = $subscriptionAccounts | Where-Object { $_.Name -eq $storageAccountName } | Select-Object -First 1
    Assert-StorageAccountMatches $listedSubscriptionAccount 'Get-AzStorageAccount subscription list'

    Section "Remove-AzStorageAccount $storageAccountName"
    Remove-AzStorageAccount `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $storageAccountName `
        -Confirm:$false

    $accountAfterDelete = $null
    try {
        $accountAfterDelete = Get-AzStorageAccount `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroup `
            -Name $storageAccountName `
            -ErrorAction Stop
    } catch {
        $accountAfterDelete = $null
    }

    if ($null -ne $accountAfterDelete) {
        throw "ASSERTION FAILED: storage account '$storageAccountName' was still readable after Remove-AzStorageAccount."
    }

    Write-Host ''
    Write-Host 'All storage account lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_STORAGE -ne '1') {
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