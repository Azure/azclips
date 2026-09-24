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

# Every expectation is tallied rather than thrown on, so cleanup at the bottom always runs and the
# negative cases can continue past their expected errors. The script exits non-zero at the very end
# if anything did not hold -- without that a failed cancel would still exit 0 and look like a pass.
$script:failures = 0
function Fail($message) {
    Write-Host "UNEXPECTED: $message" -ForegroundColor Red
    $script:failures++
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

# Everything from the arrange onwards is inside try/finally so teardown runs even when a statement
# terminates the script. Invoke-AzRestMethod surfaces a non-success response as a terminating error
# (RestProcessor_Orchestrator throws on status >= 400), so without this a failed arrange PUT -- or a
# transport/auth failure -- would exit before cleanup and strand the deployment's storage account.
try {
    # Arrange: start the deployment without waiting for it. ARM answers 201/200 the moment it
    # accepts the request and the deployment begins provisioning the storage account, leaving it
    # Running and cancellable.
    $put = Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $templatePayload

    # The deployment name is stable and reused across runs, so a failed arrange would leave the
    # checks below asserting against STALE state from an earlier run. Skip straight to cleanup.
    # ARM answers 201 the first time this name is created and 200 when it overwrites the existing
    # record, so both mean the deployment was accepted and is now running.
    $arrangeOk = $put.StatusCode -in @(200, 201)
    if (-not $arrangeOk) {
        Fail "arrange PUT returned $($put.StatusCode), expected 200 or 201 - skipping the cancel checks"
    } else {
        Write-Host "ARRANGE: deployment '$DeploymentName' accepted (HTTP $($put.StatusCode)), now cancellable"
    }

    # Synchronize on a cancellable state instead of assuming the PUT left one. A deployment is only
    # cancellable while it is Accepted or Running, and assuming that window is open is exactly what
    # made this test flake with DeploymentCannotBeCancelled. The loop exits on the first cancellable
    # reading, so in practice it costs a single GET.
    $cancellable = $false
    if ($arrangeOk) {
        $preCancelState = $null
        foreach ($attempt in 1..15) {
            $preCancelState = (Get-AzResourceGroupDeployment `
                -SubscriptionId $SubscriptionId `
                -ResourceGroupName $ResourceGroupName `
                -Name $DeploymentName).Properties.ProvisioningState
            if ($preCancelState -in @('Accepted', 'Running')) { break }
            Start-Sleep -Seconds 2
        }

        $cancellable = $preCancelState -in @('Accepted', 'Running')
        if ($cancellable) {
            Write-Host "ARRANGE: provisioningState is '$preCancelState', deployment is cancellable"
        } else {
            Fail "deployment settled at '$preCancelState' before it could be cancelled - skipping the cancel checks"
        }
    }

    if ($cancellable) {
        # Happy path: cancel the in-flight deployment. ARM answers 204 No Content, so the cmdlet
        # emits NOTHING on success -- a silent return and no error is the pass condition.
        # ConfirmImpact is Medium and $ConfirmPreference defaults to High, so this does not prompt;
        # the explicit -Confirm:$false keeps it unattended even if the caller raised
        # $ConfirmPreference.
        # Wrapped in try/catch so a failure here still lets the negative cases below run; the catch
        # records the failure rather than swallowing it.
        try {
            $cancelOutput = Stop-AzResourceGroupDeployment `
                -SubscriptionId $SubscriptionId `
                -ResourceGroupName $ResourceGroupName `
                -Name $DeploymentName `
                -Confirm:$false `
                -ErrorAction Stop

            if ($null -ne $cancelOutput) {
                Fail "cancel emitted output '$cancelOutput', expected nothing"
            } else {
                Write-Host 'PASS (cancel): completed silently'
            }
        } catch {
            Fail "cancel threw: $($_.Exception.Message)"
        }

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

        if ($state -in @('Canceled', 'Cancelled')) {
            Write-Host "PASS (state): provisioningState is '$state'"
        } else {
            Fail "provisioningState is '$state', expected 'Canceled'"
        }

        # SubscriptionId is optional: it defaults to the active profile's subscription. Cancelling
        # an already-cancelled deployment is expected to fail below, so this only proves the flag is
        # optional at bind time, which is why it shares the negative case that follows.

        # Negative case: the deployment is now in a terminal state, so a second cancel must surface
        # DeploymentCannotBeCancelled and not report a silent success.
        try {
            Stop-AzResourceGroupDeployment `
                -ResourceGroupName $ResourceGroupName `
                -Name $DeploymentName `
                -Confirm:$false `
                -ErrorAction Stop
            Fail 'cancelling a terminal deployment did not throw'
        } catch {
            if ($_.Exception.Message -match 'DeploymentCannotBeCancelled|not in a cancellable|409') {
                Write-Host "EXPECTED FAILURE (terminal deployment): $($_.Exception.Message)"
            } else {
                Fail "terminal deployment threw without DeploymentCannotBeCancelled: $($_.Exception.Message)"
            }
        }
    }

    # Negative case: a deployment name that does not exist must surface an ARM 404
    # (DeploymentNotFound), not a silent success.
    try {
        Stop-AzResourceGroupDeployment `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $MissingDeploymentName `
            -Confirm:$false `
            -ErrorAction Stop
        Fail 'missing deployment did not throw'
    } catch {
        if ($_.Exception.Message -match 'DeploymentNotFound|not found|404') {
            Write-Host "EXPECTED FAILURE (missing deployment): $($_.Exception.Message)"
        } else {
            Fail "missing deployment threw without DeploymentNotFound: $($_.Exception.Message)"
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
        Fail 'missing resource group did not throw'
    } catch {
        if ($_.Exception.Message -match 'ResourceGroupNotFound|not found|404') {
            Write-Host "EXPECTED FAILURE (missing resource group): $($_.Exception.Message)"
        } else {
            Fail "missing resource group threw without ResourceGroupNotFound: $($_.Exception.Message)"
        }
    }

    # -WhatIf must short-circuit in ShouldProcess before any HTTP call is issued
    # (StopResourceGroupDeploymentCmdlet.cs returns from ProcessRecord before OpenExecutionSession).
    # Asserting "no HTTP happened" needs an observable difference, so this reuses the deployment name
    # from the negative case directly above: without -WhatIf that exact call just threw
    # DeploymentNotFound, so with -WhatIf a silent return is only possible if ARM was never reached.
    try {
        $whatIfOutput = Stop-AzResourceGroupDeployment `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $MissingDeploymentName `
            -WhatIf `
            -ErrorAction Stop

        if ($null -ne $whatIfOutput) {
            Fail "-WhatIf emitted output '$whatIfOutput', expected nothing"
        } else {
            Write-Host 'PASS (whatif): short-circuited without reaching ARM'
        }
    } catch {
        Fail "-WhatIf reached ARM instead of short-circuiting: $($_.Exception.Message)"
    }

    # Required-argument binding: actually invoking the cmdlet without -Name would make PowerShell
    # PROMPT for the mandatory value and hang an unattended run, so assert the binding metadata
    # instead. The CLI script exercises az's own argument parser, which is a different binding layer
    # and says nothing about this cmdlet's parameters.
    $nameMandatory = (Get-Command Stop-AzResourceGroupDeployment).Parameters['Name'].Attributes |
        Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } |
        ForEach-Object { $_.Mandatory }
    if ($nameMandatory -contains $true) {
        Write-Host 'PASS (binding): -Name is declared mandatory'
    } else {
        Fail '-Name is not declared mandatory, so omitting it would silently bind an empty name'
    }
}
catch {
    # Without this the script would swallow its own failure: a terminating error inside the try
    # unwinds to the finally, cleanup runs, execution then resumes after the try/finally and the
    # tally at the bottom sees zero failures -- printing PASS and exiting 0 on a broken run.
    Fail "aborted before finishing the assertions: $($_.Exception.Message)"
}
finally {
    # Cleanup: the cancelled deployment may have created the storage account before it was stopped.
    # Cancelling does not roll anything back, so delete it explicitly. ARM answers 200 when the
    # account existed and 204 when it did not, so both are a clean teardown. An account whose create
    # was already in flight when the cancel landed keeps provisioning in the Storage RP and answers
    # 409 until it settles, so retry rather than stranding it -- but a delete that never succeeds is
    # still tallied, because a leaked account must make the run non-zero.
    $removed = $false
    $lastResult = 'no attempt made'
    foreach ($attempt in 1..12) {
        try {
            $delete = Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName`?api-version=2023-01-01" `
                -ErrorAction Stop

            if ($delete.StatusCode -in @(200, 204)) {
                Write-Host "CLEANUP: storage account '$StorageAccountName' removed (HTTP $($delete.StatusCode))"
                $removed = $true
                break
            }
            $lastResult = "HTTP $($delete.StatusCode)"
        } catch {
            $lastResult = $_.Exception.Message
        }
        Start-Sleep -Seconds 5
    }

    if (-not $removed) {
        Fail "cleanup of '$StorageAccountName' never succeeded, last result: $lastResult"
    }
}

# The deployment record itself is left behind on purpose: the name is stable, so the next run
# overwrites it rather than accumulating a new one.

if ($script:failures -gt 0) {
    Write-Host "FAILED: $($script:failures) check(s) did not meet expectations" -ForegroundColor Red
    exit 1
}
Write-Host 'PASS: all deployment cancel checks met expectations' -ForegroundColor Green
exit 0
