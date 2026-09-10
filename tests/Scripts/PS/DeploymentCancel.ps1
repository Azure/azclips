# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment cancel - PowerShell (Stop-AzResourceGroupDeployment)
# Equivalent of: Stop-AzResourceGroupDeployment
#
# A deployment can only be cancelled while its provisioningState is Accepted or Running.
# New-AzResourceGroupDeployment BLOCKS until the deployment reaches a terminal state, so it cannot
# arrange the happy path -- by the time it returns there is nothing left to cancel. The arrange step
# below therefore issues the deployment PUT through the REST passthrough, which by design does NO
# LRO polling (see RestProcessor_Orchestrator): it returns the moment ARM accepts the request,
# leaving the deployment Running and cancellable. No background job and no sleep-and-hope required.
#
# All names are STABLE, so repeated runs overwrite the same deployment record and reuse the same
# storage account instead of accumulating new ones. The cleanup step at the bottom deletes the
# storage account the cancelled deployment may have partially created.
#
# Prereq: Connect-AzAccount has run. Accounts must be imported before Resources -- its module
# initializer is what calls AzclipsServiceProvider.Initialize(), and without it every cmdlet in
# this script fails with "AzclipsServiceProvider has not been initialized."
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = '282bb33f-067d-4179-a7a4-b833c13af735'
$ResourceGroupName = 'azclips-test-group-1'
$DeploymentName = 'azclips-test-deployment-cancel'
$StorageAccountName = 'azclipsdeploycancel'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

$deploymentUri = "https://management.azure.com/subscriptions/$SubscriptionId/resourcegroups/$ResourceGroupName/providers/Microsoft.Resources/deployments/$DeploymentName`?api-version=2025-04-01"

# Single-quoted here-string: the ARM template's "$schema" key must NOT be interpolated.
$templatePayload = @'
{
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Storage/storageAccounts",
          "apiVersion": "2023-01-01",
          "name": "azclipsdeploycancel",
          "location": "australiaeast",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
'@

# Arrange: start the deployment without waiting for it. ARM answers 201 Created and the deployment
# begins provisioning the storage account, which takes long enough to leave a wide cancel window.
$put = Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $templatePayload

if ($put.StatusCode -ne 201) {
    Write-Host "UNEXPECTED: arrange PUT returned $($put.StatusCode), expected 201 - the happy path below cannot run" -ForegroundColor Red
} else {
    Write-Host "ARRANGE: deployment '$DeploymentName' accepted (201), now cancellable"
}

# Happy path: cancel the in-flight deployment. ARM answers 204 No Content, so the cmdlet emits
# NOTHING on success -- a silent return and no error is the pass condition.
# ConfirmImpact is Medium and $ConfirmPreference defaults to High, so this does not prompt; the
# explicit -Confirm:$false keeps it unattended even if the caller raised $ConfirmPreference.
# Wrapped in try/catch so a failure here still lets the negative cases below run.
try {
    $cancelOutput = Stop-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop

    if ($null -ne $cancelOutput) {
        Write-Host "UNEXPECTED: cancel emitted output '$cancelOutput', expected nothing" -ForegroundColor Red
    } else {
        Write-Host 'PASS (cancel): completed silently'
    }
} catch {
    Write-Host "UNEXPECTED: cancel threw: $($_.Exception.Message)" -ForegroundColor Red
}

# Assert the cancel actually took effect. ARM flips provisioningState to Canceled asynchronously,
# so poll rather than sleeping a fixed amount and hoping.
$state = $null
foreach ($attempt in 1..15) {
    $state = (Get-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName).Properties.ProvisioningState
    if ($state -in @('Canceled', 'Cancelled')) { break }
    Start-Sleep -Seconds 2
}

if ($state -in @('Canceled', 'Cancelled')) {
    Write-Host "PASS (state): provisioningState is '$state'"
} else {
    Write-Host "UNEXPECTED: provisioningState is '$state', expected 'Canceled'" -ForegroundColor Red
}

# SubscriptionId is optional: it defaults to the active profile's subscription. Cancelling an
# already-cancelled deployment is expected to fail below, so this only proves the flag is optional
# at bind time, which is why it shares the negative case that follows.

# Negative case: the deployment is now in a terminal state, so a second cancel must surface
# DeploymentCannotBeCancelled and not report a silent success.
try {
    Stop-AzResourceGroupDeployment `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: cancelling a terminal deployment did not throw' -ForegroundColor Red
} catch {
    if ($_.Exception.Message -match 'DeploymentCannotBeCancelled|not in a cancellable|409') {
        Write-Host "EXPECTED FAILURE (terminal deployment): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound), not a silent success.
try {
    Stop-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name azclips-test-deployment-does-not-exist `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: missing deployment did not throw' -ForegroundColor Red
} catch {
    if ($_.Exception.Message -match 'DeploymentNotFound|not found|404') {
        Write-Host "EXPECTED FAILURE (missing deployment): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound).
try {
    Stop-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName azclips-test-group-does-not-exist `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: missing resource group did not throw' -ForegroundColor Red
} catch {
    if ($_.Exception.Message -match 'ResourceGroupNotFound|not found|404') {
        Write-Host "EXPECTED FAILURE (missing resource group): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# No missing-mandatory-parameter case here: omitting -Name on a Mandatory parameter makes
# PowerShell PROMPT for it, which would hang an unattended run. The CLI script
# (tests/Scripts/CLI/DeploymentCancel.sh) covers required-argument binding instead.

# Cleanup: the cancelled deployment may have created the storage account before it was stopped.
# Cancelling does not roll anything back, so delete it explicitly. ARM answers 200 when the account
# existed and 204 when it did not, so both are a clean teardown.
$delete = Invoke-AzRestMethod `
    -Method DELETE `
    -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName`?api-version=2023-01-01"

if ($delete.StatusCode -in @(200, 204)) {
    Write-Host "CLEANUP: storage account '$StorageAccountName' removed (HTTP $($delete.StatusCode))"
} else {
    Write-Host "UNEXPECTED: cleanup returned $($delete.StatusCode); '$StorageAccountName' may still exist" -ForegroundColor Red
}

# The deployment record itself is left behind on purpose: the name is stable, so the next run
# overwrites it rather than accumulating a new one.
