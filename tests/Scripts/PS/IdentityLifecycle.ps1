# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Managed identity lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, list, get, update, resource restrictions, and delete.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID       - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP  - resource group name; defaults to timestamped test group
#   AZCLIPS_IDENTITY_NAME   - primary identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION        - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY   - set to 1 to skip identity cleanup
#   AZCLIPS_CLEANUP_RG      - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\IdentityLifecycle.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Login\Login.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Identity\Identity.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-identity-it-$stamp" } else { $env:AZCLIPS_RESOURCE_GROUP }
$identityName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_IDENTITY_NAME)) { "azclips-id-$stamp" } else { $env:AZCLIPS_IDENTITY_NAME }
$secondaryIdentityName = "$identityName-2"

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

    Section 'New-AzUserAssignedIdentity primary'
    New-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -Location $location `
        -Tag @{ env = 'local-integration' } `
        -IsolationScope Regional `
        -ResourceRestriction Microsoft.Compute | Out-Host

    Section 'New-AzUserAssignedIdentity secondary'
    New-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $secondaryIdentityName `
        -Location $location `
        -Tag @{ env = 'local-integration' } `
        -IsolationScope Regional `
        -ResourceRestriction Microsoft.Compute | Out-Host

    Section 'Get-AzUserAssignedIdentity list by resource group'
    Get-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup | Out-Host

    Section 'Get-AzUserAssignedIdentity primary'
    Get-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName | Out-Host

    Section 'Get-AzUserAssignedIdentity secondary'
    Get-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $secondaryIdentityName | Out-Host

    Section 'Update-AzUserAssignedIdentity normal path'
    Update-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -Location $location `
        -IsolationScope None `
        -Tag @{ env = 'normal-update' } | Out-Host

    Section 'Update-AzUserAssignedIdentity secondary'
    Update-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $secondaryIdentityName `
        -IsolationScope None `
        -Tag @{ env = 'secondary-update' } | Out-Host

    Section 'Update-AzUserAssignedIdentity resource restriction provider list'
    Update-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -ResourceRestriction Microsoft.Storage/accounts | Out-Host

    Section 'Update-AzUserAssignedIdentity both identities'
    Update-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -IsolationScope None `
        -Tag @{ env = 'local-integration-updated' } | Out-Host

    Update-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $secondaryIdentityName `
        -IsolationScope None `
        -Tag @{ env = 'local-integration-updated' } | Out-Host

    Section 'Get-AzUserAssignedIdentity after update'
    Get-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup | Out-Host

    Write-Host ''
    Write-Host 'All identity lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_IDENTITY -ne '1') {
        Section 'Remove-AzUserAssignedIdentity cleanup both identities'
        try {
            Remove-AzUserAssignedIdentity `
                -SubscriptionId $subscriptionId `
                -ResourceGroupName $resourceGroup `
                -Name $identityName `
                -Confirm:$false `
                -ErrorAction Stop
        } catch {
            Write-Warning "Primary identity cleanup failed: $($_.Exception.Message)"
        }

        try {
            Remove-AzUserAssignedIdentity `
                -SubscriptionId $subscriptionId `
                -ResourceGroupName $resourceGroup `
                -Name $secondaryIdentityName `
                -Confirm:$false `
                -ErrorAction Stop
        } catch {
            Write-Warning "Secondary identity cleanup failed: $($_.Exception.Message)"
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
