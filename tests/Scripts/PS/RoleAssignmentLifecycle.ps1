# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Role assignment lifecycle smoke test for the Azclips PowerShell surface.
# Exercises create, list (by scope, by assignee, by role, inherited, nested-scopes, and group-expanded),
# idempotent re-create (a generated name returns the existing assignment; an explicit different name
# surfaces the conflict), the -ResourceGroupName scope form with a fully-qualified role ID, an ABAC
# condition, the exact-scope default list, and delete (including ARM's dual 200 OK / 204 No Content
# response for deleting an already-deleted assignment, which must not be treated as an error). A fresh
# user-assigned identity is created to act as the principal, so the test is self-contained and needs no
# pre-existing fixture.
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

# Two more stable built-in roles for the extended-coverage checks below: "Monitoring Reader" is used
# (as a fully-qualified role definition ID) to exercise the -ResourceGroupName scope form with a
# non-Reader role, and "Storage Blob Data Reader" is a data-plane role whose blob read action the ABAC
# condition references, so ARM accepts a condition on it.
$monitoringReaderRoleDefinitionId = '43d0d8ad-25c7-4714-9337-8ba259a9fe05'
$storageBlobDataReaderRoleDefinitionId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

$scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup"
$roleAssignmentName = $null
$roleAssignmentDeleted = $false
# Names of the extra grants created by the extended-coverage checks. Each is deleted inline right after
# it is tested; this list is a cleanup safety net in case the script throws in between (delete is 204-safe).
$extraAssignmentNames = [System.Collections.Generic.List[string]]::new()

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
        if ($_.Exception.Message -notmatch '409' -or $_.Exception.Message -notmatch 'RoleAssignmentExists') {
            throw
        }
        Write-Host 'Confirmed: explicit-name create surfaced the existing-assignment conflict.' -ForegroundColor Green
    }
    if ($conflictSucceeded) {
        # Clean up the unexpected extra assignment so it does not leak, then fail.
        Remove-AzRoleAssignment -Scope $scope -Name $conflictName -Confirm:$false -ErrorAction SilentlyContinue | Out-Host
        throw "Expected create with explicit -Name '$conflictName' to fail with a conflict, but it succeeded."
    }

    Section 'Get-AzRoleAssignment default scope is exact and also appears in -IncludeInherited'
    # The default list returns assignments at exactly this scope; -IncludeInherited additionally returns
    # ancestor-scope (inherited) grants. Assert the default list contains only at-scope assignments and
    # that every default assignment also appears in the inherited result set.
    $defaultList = @(Get-AzRoleAssignment -Scope $scope)
    $inheritedList = @(Get-AzRoleAssignment -Scope $scope -IncludeInherited)
    $wrongScope = @($defaultList | Where-Object { $_.Properties.Scope -ne $scope })
    if ($wrongScope.Count -gt 0) {
        throw "Default list included $($wrongScope.Count) assignment(s) whose Properties.Scope was not '$scope'."
    }
    if (-not ($defaultList | Where-Object { $_.Name -eq $roleAssignmentName })) {
        throw "Default exact-scope list did not contain the assignment '$roleAssignmentName' created at this scope."
    }
    $inheritedNames = @{}
    foreach ($assignment in $inheritedList) {
        $inheritedNames[$assignment.Name] = $true
    }
    $missingFromInherited = @($defaultList | Where-Object { -not $inheritedNames.ContainsKey($_.Name) })
    if ($missingFromInherited.Count -gt 0) {
        throw "-IncludeInherited omitted $($missingFromInherited.Count) default assignment(s): $($missingFromInherited.Name -join ', ')."
    }
    Write-Host 'Confirmed: default list is exact-scope and every default assignment appears in -IncludeInherited.' -ForegroundColor Green

    Section 'New-AzRoleAssignment -ResourceGroupName scope form with a fully-qualified role ID'
    # Exercise the -ResourceGroupName scope form (vs an explicit -Scope) together with a fully-qualified
    # role definition ID (vs a bare GUID). Uses a different built-in role (Monitoring Reader) so this is a
    # genuinely new grant rather than an idempotent conflict with the Reader grant above.
    $monitoringReaderFullId = "/subscriptions/$subscriptionId/providers/Microsoft.Authorization/roleDefinitions/$monitoringReaderRoleDefinitionId"
    $rgFormName = [Guid]::NewGuid().ToString()
    $extraAssignmentNames.Add($rgFormName)
    $rgFormAssignment = New-AzRoleAssignment `
        -ResourceGroupName $resourceGroup `
        -ObjectId $principalId `
        -ObjectType ServicePrincipal `
        -RoleDefinitionId $monitoringReaderFullId `
        -Name $rgFormName `
        -Confirm:$false
    $rgFormAssignment | Out-Host
    if ($rgFormAssignment.Name -ne $rgFormName) {
        throw "Resource-group-form create returned '$($rgFormAssignment.Name)', expected '$rgFormName'."
    }
    Write-Host "Created role assignment via -ResourceGroupName and a full role ID: $($rgFormAssignment.Name)" -ForegroundColor Green

    Section 'Remove-AzRoleAssignment -ResourceGroupName scope form'
    Remove-AzRoleAssignment -ResourceGroupName $resourceGroup -Name $rgFormName -Confirm:$false | Out-Host
    Write-Host 'Confirmed: created and deleted an assignment via the -ResourceGroupName scope form.' -ForegroundColor Green

    Section 'New-AzRoleAssignment with an ABAC condition'
    # Exercise the -Condition / -ConditionVersion path end-to-end: ARM validates the condition expression
    # against the role's data actions, which the offline tests cannot. Uses Storage Blob Data Reader (whose
    # blob read action the condition references) so ARM accepts the condition.
    $blobCondition = '((!(ActionMatches{''Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read''})) OR (@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals ''smoke-test-container''))'
    $conditionName = [Guid]::NewGuid().ToString()
    $extraAssignmentNames.Add($conditionName)
    $conditionAssignment = New-AzRoleAssignment `
        -Scope $scope `
        -ObjectId $principalId `
        -ObjectType ServicePrincipal `
        -RoleDefinitionId $storageBlobDataReaderRoleDefinitionId `
        -Name $conditionName `
        -Condition $blobCondition `
        -ConditionVersion '2.0' `
        -Confirm:$false
    $conditionAssignment | Out-Host
    if ($conditionAssignment.Name -ne $conditionName) {
        throw "Conditional create returned '$($conditionAssignment.Name)', expected '$conditionName'."
    }
    Write-Host "Created conditional role assignment: $($conditionAssignment.Name)" -ForegroundColor Green
    Remove-AzRoleAssignment -Scope $scope -Name $conditionName -Confirm:$false | Out-Host
    Write-Host 'Confirmed: created and deleted a conditional assignment.' -ForegroundColor Green

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

    if ($env:AZCLIPS_KEEP_ROLE_ASSIGNMENT -ne '1' -and $extraAssignmentNames.Count -gt 0) {
        # Safety net for the extended-coverage grants; each is already deleted inline on the happy path,
        # and Remove is idempotent (204 No Content), so re-deleting here is harmless.
        Section 'Remove-AzRoleAssignment cleanup extended-coverage grants (safety net)'
        foreach ($extraName in $extraAssignmentNames) {
            try {
                Remove-AzRoleAssignment -Scope $scope -Name $extraName -Confirm:$false -ErrorAction Stop | Out-Host
            } catch {
                Write-Warning "Extended-coverage role assignment cleanup failed for '$extraName': $($_.Exception.Message)"
            }
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
                -Uri "/subscriptions/$subscriptionId/resourcegroups/${resourceGroup}?api-version=2025-04-01" `
                -ErrorAction Stop | Out-Host
        } catch {
            Write-Warning "Resource group cleanup failed: $($_.Exception.Message)"
        }
    }
}
