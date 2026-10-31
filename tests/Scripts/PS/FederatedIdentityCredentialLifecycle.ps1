# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Federated identity credential lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, list, get, update, and delete of federated identity credentials
# under a user assigned identity.
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
#   AZCLIPS_IDENTITY_NAME   - identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION        - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY   - set to 1 to skip identity/resource group cleanup
#   AZCLIPS_CLEANUP_RG      - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\FederatedIdentityCredentialLifecycle.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\ManagedServiceIdentity\ManagedServiceIdentity.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-fic-it-$stamp" } else { $env:AZCLIPS_RESOURCE_GROUP }
$identityName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_IDENTITY_NAME)) { "azclips-id-$stamp" } else { $env:AZCLIPS_IDENTITY_NAME }
$subjectFicName = "azclips-fic-subject-$stamp"
$expressionFicName = "azclips-fic-expression-$stamp"

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

    Section 'New-AzUserAssignedIdentity'
    New-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -Location $location | Out-Host

    Section 'New-AzFederatedIdentityCredential using subject'
    New-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $subjectFicName `
        -Issuer 'https://kubernetes-oauth.azure.com' `
        -Subject 'system:serviceaccount:ns:svcaccount' | Out-Host

    Section 'New-AzFederatedIdentityCredential using claims matching expression'
    New-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $expressionFicName `
        -Issuer 'https://token.actions.githubusercontent.com' `
        -ClaimsMatchingExpressionVersion 1 `
        -ClaimsMatchingExpressionValue "claims['sub'] matches 'repo:contoso/example:ref:refs/heads/*' and claims['repository_id'] eq '456789'" | Out-Host

    Section 'Get-AzFederatedIdentityCredential list under identity'
    Get-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName | Out-Host

    Section 'Get-AzFederatedIdentityCredential subject credential'
    Get-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $subjectFicName | Out-Host

    Section 'Get-AzFederatedIdentityCredential expression credential'
    Get-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $expressionFicName | Out-Host

    Section 'Update-AzFederatedIdentityCredential subject audience override'
    Update-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $subjectFicName `
        -Audience @('api://AzureADTokenExchange-Modified') | Out-Host

    Section 'Update-AzFederatedIdentityCredential expression audience override'
    Update-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $expressionFicName `
        -Audience @('api://AzureADTokenExchange-Modified') | Out-Host

    Section 'Get-AzFederatedIdentityCredential subject after update'
    Get-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $subjectFicName | Out-Host

    Section 'Get-AzFederatedIdentityCredential expression after update'
    Get-AzFederatedIdentityCredential `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -IdentityName $identityName `
        -Name $expressionFicName | Out-Host

    Write-Host ''
    Write-Host 'All federated identity credential lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_IDENTITY -ne '1') {
        Section 'Remove-AzFederatedIdentityCredential cleanup'
        foreach ($ficName in @($subjectFicName, $expressionFicName)) {
            try {
                Remove-AzFederatedIdentityCredential `
                    -SubscriptionId $subscriptionId `
                    -ResourceGroupName $resourceGroup `
                    -IdentityName $identityName `
                    -Name $ficName `
                    -Confirm:$false `
                    -ErrorAction Stop
            } catch {
                Write-Warning "Federated identity credential '$ficName' cleanup failed: $($_.Exception.Message)"
            }
        }

        Section 'Remove-AzUserAssignedIdentity cleanup'
        try {
            Remove-AzUserAssignedIdentity `
                -SubscriptionId $subscriptionId `
                -ResourceGroupName $resourceGroup `
                -Name $identityName `
                -Confirm:$false `
                -ErrorAction Stop
        } catch {
            Write-Warning "Identity cleanup failed: $($_.Exception.Message)"
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
