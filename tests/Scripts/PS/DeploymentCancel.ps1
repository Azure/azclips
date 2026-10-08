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

# A deployment name that must never exist. Used twice on purpose: once without -WhatIf, where it has
# to 404, and once with -WhatIf, where the identical call has to stay silent. Sharing one constant
# keeps that pair a controlled A/B in which -WhatIf is the only difference.
$MissingDeploymentName = 'azclips-test-deployment-does-not-exist'

# Every check is tallied rather than thrown, so all cases and the cleanup at the bottom always run;
# the script exits non-zero at the end if any failed (same pattern as
# DeploymentParametersQueryString.ps1).
$failures = 0
function Assert-Equal {
    param($Expected, $Actual, [string]$Case)
    if ($Expected -eq $Actual) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected '$Expected' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}

function Assert-Match {
    param([string]$Pattern, $Actual, [string]$Case)
    if ($Actual -match $Pattern) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected a message matching '$Pattern' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}

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

# Arrange: start the deployment without waiting for it. ARM answers 201 the first time this name is
# created and 200 when it overwrites the existing record; either way the deployment begins
# provisioning the storage account, leaving it Running and cancellable.
$put = Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $templatePayload
Assert-Match '^20[01]$' $put.StatusCode 'arrange: deployment PUT accepted'

# Synchronize on a cancellable state instead of assuming the PUT left one. A deployment is only
# cancellable while it is Accepted or Running, and assuming that window is open is exactly what
# made this test flake with DeploymentCannotBeCancelled. The loop exits on the first cancellable
# reading, so in practice it costs a single GET.
$preCancelState = $null
if ($put.StatusCode -in @(200, 201)) {
    foreach ($attempt in 1..15) {
        $preCancelState = (Get-AzResourceGroupDeployment `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $DeploymentName).Properties.ProvisioningState
        if ($preCancelState -in @('Accepted', 'Running')) { break }
        Start-Sleep -Seconds 2
    }
}
Assert-Match '^(Accepted|Running)$' $preCancelState 'arrange: deployment is cancellable'

# The deployment name is stable and reused across runs, so without a cancellable deployment from
# THIS run the checks below would assert against stale state from an earlier run. Skip them.
if ($preCancelState -in @('Accepted', 'Running')) {
    # Happy path: cancel the in-flight deployment. ARM answers 204 No Content, so the cmdlet
    # emits NOTHING on success -- a silent return and no error is the pass condition.
    # ConfirmImpact is Medium and $ConfirmPreference defaults to High, so this does not prompt;
    # the explicit -Confirm:$false keeps it unattended even if the caller raised
    # $ConfirmPreference.
    $cancelOutput = Stop-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorVariable err
    Assert-Equal 0 $err.Count 'cancel: no error'
    Assert-Equal $null $cancelOutput 'cancel: emits nothing'

    # Assert the cancel actually took effect. ARM acknowledges the cancel with 204 immediately
    # but only flips provisioningState to Canceled after the in-flight resource operation
    # settles, so the budget has to outlast one storage account create rather than just the
    # POST. The loop exits as soon as the state flips.
    $state = $null
    foreach ($attempt in 1..30) {
        $state = (Get-AzResourceGroupDeployment `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $DeploymentName).Properties.ProvisioningState
        if ($state -in @('Canceled', 'Cancelled')) { break }
        Start-Sleep -Seconds 2
    }
    Assert-Match '^(Canceled|Cancelled)$' $state 'cancel: provisioningState'

    # Negative case: the deployment is now in a terminal state, so a second cancel must surface
    # DeploymentCannotBeCancelled and not report a silent success. -SubscriptionId is omitted on
    # purpose: it defaults to the active profile's subscription, so this also proves the flag is
    # optional at bind time.
    Stop-AzResourceGroupDeployment `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorVariable err
    Assert-Match 'DeploymentCannotBeCancelled|not in a cancellable|409' $err.Message 'terminal deployment cannot be cancelled'
}

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound), not a silent success.
Stop-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $MissingDeploymentName `
    -Confirm:$false `
    -ErrorVariable err
Assert-Match 'DeploymentNotFound|not found|404' $err.Message 'missing deployment surfaces 404'

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound).
Stop-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName azclips-test-group-does-not-exist `
    -Name $DeploymentName `
    -Confirm:$false `
    -ErrorVariable err
Assert-Match 'ResourceGroupNotFound|not found|404' $err.Message 'missing resource group surfaces 404'

# -WhatIf must short-circuit in ShouldProcess before any HTTP call is issued
# (StopResourceGroupDeploymentCmdlet.cs returns from ProcessRecord before OpenExecutionSession).
# Asserting "no HTTP happened" needs an observable difference, so this reuses the deployment name
# from the negative case above: without -WhatIf that exact call surfaced DeploymentNotFound, so with
# -WhatIf a silent return is only possible if ARM was never reached.
$whatIfOutput = Stop-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $MissingDeploymentName `
    -WhatIf `
    -ErrorVariable err
Assert-Equal 0 $err.Count '-WhatIf: did not reach ARM'
Assert-Equal $null $whatIfOutput '-WhatIf: emits nothing'

# Required-argument binding: actually invoking the cmdlet without -Name would make PowerShell
# PROMPT for the mandatory value and hang an unattended run, so assert the binding metadata
# instead. The CLI script exercises az's own argument parser, which is a different binding layer
# and says nothing about this cmdlet's parameters.
$nameMandatory = (Get-Command Stop-AzResourceGroupDeployment).Parameters['Name'].Attributes |
    Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } |
    ForEach-Object { $_.Mandatory }
Assert-Equal $true ($nameMandatory -contains $true) 'binding: -Name is mandatory'

# Cleanup: the cancelled deployment may have created the storage account before it was stopped.
# Cancelling does not roll anything back, so delete it explicitly. ARM answers 200 when the
# account existed and 204 when it did not, so both are a clean teardown. An account whose create
# was already in flight when the cancel landed keeps provisioning in the Storage RP and answers
# 409 until it settles, so retry rather than stranding it -- but a delete that never succeeds is
# still a failure, because a leaked account must make the run non-zero.
$delete = $null
foreach ($attempt in 1..12) {
    $delete = Invoke-AzRestMethod `
        -Method DELETE `
        -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName`?api-version=2023-01-01"
    if ($delete.StatusCode -in @(200, 204)) { break }
    Start-Sleep -Seconds 5
}
Assert-Match '^20[04]$' $delete.StatusCode "cleanup: storage account '$StorageAccountName' removed"

# The deployment record itself is left behind on purpose: the name is stable, so the next run
# overwrites it rather than accumulating a new one.

if ($failures -gt 0) {
    Write-Host "`n$failures assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
