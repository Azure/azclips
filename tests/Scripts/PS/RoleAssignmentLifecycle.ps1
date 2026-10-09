# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Role assignment lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, list (by scope, by assignee, and by role), idempotent re-create (a generated
# name returns the existing assignment; an explicit different name surfaces the conflict), and
# delete (including ARM's dual 200 OK / 204 No Content response for deleting an already-deleted
# assignment, which must not be treated as an error). A fresh user-assigned identity is created to
# act as the principal, so the test is self-contained and needs no pre-existing fixture.
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
#   AZCLIPS_KEEP_ROLE_ASSIGNMENT   - set to 1 to skip delete verification and leave the role
#                                    assignment in place (e.g. to inspect it manually)
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
$roleAssignmentName = $null
$roleAssignmentDeleted = $false

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
    # Reuse a single stable name (GUID) across every attempt so a retry after a transient client-side
    # failure re-affirms the same PUT (idempotent for the same scope+name) and leaves the cleanup step a
    # known name to delete. An omitted name would instead generate a new one each attempt; the idempotent
    # conflict path would still recover the existing grant, but under a name this script never recorded.
    $roleAssignmentName = [Guid]::NewGuid().ToString()
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

    Section 'Get-AzRoleAssignment including inherited (ancestor-scope) assignments'
    Get-AzRoleAssignment -Scope $scope -IncludeInherited | Out-Host

    Section 'Get-AzRoleAssignment expanding the principal''s group memberships'
    Get-AzRoleAssignment -Scope $scope -ObjectId $principalId -ExpandPrincipalGroups | Out-Host

    Section 'Get-AzRoleAssignment across the whole subscription (every nested scope)'
    Get-AzRoleAssignment -IncludeNestedScopes | Out-Host

    Section 'New-AzRoleAssignment idempotent re-create without -Name returns the existing assignment'
    # The same principal+role+scope already has an assignment (created above, under $roleAssignmentName).
    # Re-running create WITHOUT -Name generates a fresh name, so ARM returns a RoleAssignmentExists
    # conflict; because the name was generated the idempotent path must look up and return the existing
    # assignment instead of failing, and must not create a second grant.
    $idempotent = New-AzRoleAssignment `
        -Scope $scope `
        -ObjectId $principalId `
        -ObjectType ServicePrincipal `
        -RoleDefinitionId $readerRoleDefinitionId `
        -Confirm:$false
    $idempotent | Out-Host
    if ($idempotent.Name -ne $roleAssignmentName) {
        throw "Idempotent create returned '$($idempotent.Name)', expected the existing assignment '$roleAssignmentName'."
    }
    Write-Host "Confirmed: generated-name re-create returned the existing assignment '$roleAssignmentName'." -ForegroundColor Green

    Section 'New-AzRoleAssignment explicit different -Name for an existing grant surfaces the conflict'
    # With an explicit -Name the caller asked for a specific assignment, so a RoleAssignmentExists
    # conflict must surface as an error rather than silently returning the differently-named existing one.
    $conflictName = [Guid]::NewGuid().ToString()
    $conflictSucceeded = $false
    try {
        New-AzRoleAssignment `
            -Scope $scope `
            -ObjectId $principalId `
            -ObjectType ServicePrincipal `
            -RoleDefinitionId $readerRoleDefinitionId `
            -Name $conflictName `
            -Confirm:$false | Out-Host
        $conflictSucceeded = $true
    } catch {
        Write-Host 'Confirmed: explicit-name create surfaced the existing-assignment conflict.' -ForegroundColor Green
    }
    if ($conflictSucceeded) {
        # Clean up the unexpected extra assignment so it does not leak, then fail.
        Remove-AzRoleAssignment -Scope $scope -Name $conflictName -Confirm:$false -ErrorAction SilentlyContinue | Out-Host
        throw "Expected create with explicit -Name '$conflictName' to fail with a conflict, but it succeeded."
    }

    if ($env:AZCLIPS_KEEP_ROLE_ASSIGNMENT -eq '1') {
        Write-Host ''
        Write-Host 'AZCLIPS_KEEP_ROLE_ASSIGNMENT=1: skipping delete verification; role assignment left in place.'
    } else {
        Section 'Remove-AzRoleAssignment'
        Remove-AzRoleAssignment -Scope $scope -Name $roleAssignmentName -Confirm:$false | Out-Host
        $roleAssignmentDeleted = $true

        Section 'Get-AzRoleAssignment after delete (must not include the deleted assignment)'
        $remaining = @(Get-AzRoleAssignment -Scope $scope -ObjectId $principalId)
        if ($remaining | Where-Object { $_.Name -eq $roleAssignmentName }) {
            throw "Deleted role assignment '$roleAssignmentName' still appears in the list after delete."
        }
        Write-Host "Confirmed: '$roleAssignmentName' no longer appears in the list." -ForegroundColor Green

        Section 'Remove-AzRoleAssignment again (ARM returns 204 No Content for an already-deleted assignment; must not be treated as an error)'
        Remove-AzRoleAssignment -Scope $scope -Name $roleAssignmentName -Confirm:$false | Out-Host
        Write-Host 'Confirmed: deleting an already-deleted assignment did not throw.' -ForegroundColor Green
    }

    Write-Host ''
    Write-Host 'All role assignment lifecycle smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($env:AZCLIPS_KEEP_ROLE_ASSIGNMENT -ne '1' -and -not $roleAssignmentDeleted -and $roleAssignmentName) {
        # Safety net only: the main flow above already deletes (and verifies) the role assignment
        # when AZCLIPS_KEEP_ROLE_ASSIGNMENT isn't set. This only runs if something earlier in the
        # script threw before reaching that point. Remove-AzRoleAssignment is safe to call even if
        # the assignment was never created (204 No Content, not an error), so no existence check
        # is needed here.
        Section 'Remove-AzRoleAssignment cleanup (safety net)'
        try {
            Remove-AzRoleAssignment -Scope $scope -Name $roleAssignmentName -Confirm:$false -ErrorAction Stop | Out-Host
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
