# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Role assignment lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create and list (by scope, by assignee, and by role). A fresh user-assigned identity
# is created to act as the principal, so the test is self-contained and needs no pre-existing
# fixture. Delete is not yet available on this branch (feature/853-role-assignment-create); cleanup
# falls back to a raw REST DELETE via Invoke-AzRestMethod, the same pattern this script family
# already uses for resource group cleanup. Swap to Remove-AzRoleAssignment once it merges.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID    - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID              - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP         - resource group name; defaults to timestamped test group
#   AZCLIPS_IDENTITY_NAME          - principal identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION               - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY          - set to 1 to skip identity cleanup
#   AZCLIPS_KEEP_ROLE_ASSIGNMENT   - set to 1 to skip role assignment cleanup
#   AZCLIPS_CLEANUP_RG             - set to 1 to delete the resource group at the end
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\RoleAssignmentLifecycle.ps1

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
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { "azclips-roleassign-it-$stamp" } else { $env:AZCLIPS_RESOURCE_GROUP }
$identityName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_IDENTITY_NAME)) { "azclips-ra-id-$stamp" } else { $env:AZCLIPS_IDENTITY_NAME }

# Built-in "Reader" role definition GUID (stable across all tenants); least-privilege choice for a
# smoke test that only needs to prove the grant works, not actually use it.
$readerRoleDefinitionId = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

$scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup"
$roleAssignmentId = $null

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

    Section 'New-AzUserAssignedIdentity (principal for the role assignment)'
    $identity = New-AzUserAssignedIdentity `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $identityName `
        -Location $location
    $identity | Out-Host

    $principalId = $identity.Properties.PrincipalId
    if ([string]::IsNullOrWhiteSpace($principalId)) {
        throw 'New-AzUserAssignedIdentity did not return a principal ID.'
    }

    Section 'New-AzRoleAssignment Reader for the identity (retrying for principal propagation)'
    # Reuse a single stable name (GUID) across every attempt. Create is a PUT, idempotent for the
    # same scope+name: if a prior attempt actually succeeded server-side but the client reported a
    # transient failure, retrying with the SAME name safely re-affirms the existing assignment.
    # Retrying with a fresh (omitted) name instead would hit ARM's RoleAssignmentExists conflict
    # (same principal+role+scope, a different assignment ID), exhausting retries while silently
    # leaving the first assignment behind with no recorded name to clean it up.
    $roleAssignmentName = [Guid]::NewGuid().ToString()
    $roleAssignmentId = "$scope/providers/Microsoft.Authorization/roleAssignments/$roleAssignmentName"
    $roleAssignment = $null
    $maxAttempts = 6
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            $roleAssignment = New-AzRoleAssignment `
                -Scope $scope `
                -ObjectId $principalId `
                -ObjectType ServicePrincipal `
                -RoleDefinitionId $readerRoleDefinitionId `
                -Name $roleAssignmentName `
                -Description 'azclips role assignment lifecycle smoke test' `
                -Confirm:$false
            break
        } catch {
            if ($attempt -eq $maxAttempts) {
                throw
            }
            # A freshly-created principal can take a few seconds to propagate to ARM's
            # authorization backend; retry rather than fail outright on the first attempt.
            Write-Warning "Attempt $attempt failed (principal may not have propagated yet): $($_.Exception.Message). Retrying in 10s..."
            Start-Sleep -Seconds 10
        }
    }
    $roleAssignment | Out-Host

    Section 'Get-AzRoleAssignment by scope'
    Get-AzRoleAssignment -Scope $scope | Out-Host

    Section 'Get-AzRoleAssignment filtered by assignee object ID'
    Get-AzRoleAssignment -Scope $scope -ObjectId $principalId | Out-Host

    Section 'Get-AzRoleAssignment filtered by role definition ID'
    Get-AzRoleAssignment -Scope $scope -RoleDefinitionId $readerRoleDefinitionId | Out-Host

    Write-Host ''
    Write-Host 'All role assignment lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_ROLE_ASSIGNMENT -ne '1' -and $roleAssignmentId) {
        Section 'Invoke-AzRestMethod cleanup role assignment'
        try {
            Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "$($roleAssignmentId)?api-version=2022-04-01" `
                -ErrorAction Stop | Out-Host
        } catch {
            Write-Warning "Role assignment cleanup failed: $($_.Exception.Message)"
        }
    }

    if ($env:AZCLIPS_KEEP_IDENTITY -ne '1') {
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
