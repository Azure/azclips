# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment delete - PowerShell (Remove-AzResourceGroupDeployment)
# Equivalent of: Remove-AzResourceGroupDeployment
#
# Deleting a deployment removes the HISTORY RECORD and its deployment operations. It does NOT
# delete the resources the deployment created and it does NOT touch the resource group. Two
# behaviours are worth pinning here because they surprise people:
#   1. Deleting a name that does not exist SUCCEEDS SILENTLY -- ARM answers 204 No Content whether
#      or not the deployment was there. This is a deliberate divergence from legacy Azure
#      PowerShell, which pre-checked existence with CheckExistence and threw.
#   2. A deployment that is still RUNNING cannot be deleted; ARM rejects it. Cancel it first.
#
# The happy path needs a deployment that has already reached a terminal state, which is exactly what
# New-AzResourceGroupDeployment leaves behind (it blocks until terminal). An EMPTY template is used
# so the arrange step creates no resources and there is nothing to clean up afterwards.
#
# The "cannot delete a running deployment" case needs the opposite -- a deployment stuck in Running.
# That is arranged through the REST passthrough, which by design does NO LRO polling (see
# RestProcessor_Orchestrator): it returns the moment ARM accepts the PUT. Same trick
# DeploymentCancel.ps1 uses.
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
$DeploymentName = 'azclips-test-deployment-delete'
$RunningDeploymentName = 'azclips-test-deployment-delete-running'
$StorageAccountName = 'azclipsdeploydelete'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Nothing is fatal on its own: the negative cases below must all run. Every unexpected outcome is
# counted here instead, and the script exits non-zero at the end if any were recorded.
$script:failures = 0

function Add-Failure([string]$message) {
    $script:failures++
    Write-Host "UNEXPECTED: $message" -ForegroundColor Red
}

# Empty template: no resources, so the deployment reaches Succeeded almost immediately and creates
# nothing that needs cleaning up. Single-quoted here-string so "$schema" is NOT interpolated.
$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) 'azclips-empty-template.json'
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": []
}
'@ | Set-Content -Path $templateFile -Encoding utf8

# Arrange: create a deployment and wait for it to finish, leaving a terminal history record.
try {
    New-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -TemplateFile $templateFile `
        -ErrorAction Stop | Out-Null
    Write-Host "ARRANGE: deployment '$DeploymentName' created and terminal"
} catch {
    Add-Failure "arrange failed to create '$DeploymentName': $($_.Exception.Message)"
}

# Happy path: delete the terminal deployment. ARM answers 202 and the cmdlet polls the Location
# header until it returns 204, so this BLOCKS until the delete has actually finished. There is no
# response body, so the cmdlet emits NOTHING -- a silent return and no error is the pass condition.
# ConfirmImpact is Medium and $ConfirmPreference defaults to High, so this does not prompt; the
# explicit -Confirm:$false keeps it unattended even if the caller raised $ConfirmPreference.
try {
    $deleteOutput = Remove-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop

    if ($null -ne $deleteOutput) {
        Add-Failure "delete emitted output '$deleteOutput', expected nothing"
    } else {
        Write-Host 'PASS (delete): completed silently'
    }
} catch {
    Add-Failure "delete threw: $($_.Exception.Message)"
}

# Assert the record is gone. Because delete waits for the LRO to complete, no polling is needed
# here -- Get must ALREADY fail with DeploymentNotFound.
try {
    Get-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -ErrorAction Stop | Out-Null
    Add-Failure 'the deployment record still exists after delete'
} catch {
    if ($_.Exception.Message -match 'DeploymentNotFound|not found|404') {
        Write-Host "PASS (gone): $($_.Exception.Message)"
    } else {
        Add-Failure "get after delete failed for the wrong reason: $($_.Exception.Message)"
    }
}

# SubscriptionId is optional: it defaults to the active profile's subscription. Deleting the same
# name a second time is the "does not exist" case, which SUCCEEDS SILENTLY (see header note 1), so
# this call doubles as proof that -SubscriptionId binds optionally.
try {
    Remove-AzResourceGroupDeployment `
        -ResourceGroupName $ResourceGroupName `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'PASS (already deleted): silent success, as ARM returns 204 either way'
} catch {
    Add-Failure "deleting an absent deployment threw: $($_.Exception.Message)"
}

# Legacy binding parity, part 1: -ResourceGroupName is position 0 and -Name is position 1, so the
# parameter names can be omitted entirely. Still the "does not exist" path, so still silent.
try {
    Remove-AzResourceGroupDeployment $ResourceGroupName azclips-test-deployment-does-not-exist -Confirm:$false -ErrorAction Stop
    Write-Host 'PASS (positional): bound -ResourceGroupName/-Name positionally'
} catch {
    Add-Failure "positional binding threw: $($_.Exception.Message)"
}

# Legacy binding parity, part 2: -Name carries the legacy alias -DeploymentName.
try {
    Remove-AzResourceGroupDeployment `
        -ResourceGroupName $ResourceGroupName `
        -DeploymentName azclips-test-deployment-does-not-exist `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'PASS (alias): bound -DeploymentName'
} catch {
    Add-Failure "-DeploymentName alias threw: $($_.Exception.Message)"
}

# Legacy binding parity, part 3: the -Id parameter set. The subscription is read out of the id, so
# -SubscriptionId is not needed. -DeploymentId and -ResourceId are aliases of -Id.
try {
    Remove-AzResourceGroupDeployment `
        -Id "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Resources/deployments/azclips-test-deployment-does-not-exist" `
        -Confirm:$false `
        -ErrorAction Stop
    Write-Host 'PASS (-Id): resolved resource group, name and subscription from the id'
} catch {
    Add-Failure "-Id parameter set threw: $($_.Exception.Message)"
}

# Negative case: a malformed -Id must fail argument validation in the cmdlet BEFORE any HTTP call is
# made, rather than building a nonsense request URI.
try {
    Remove-AzResourceGroupDeployment -Id 'not-a-resource-id' -Confirm:$false -ErrorAction Stop
    Add-Failure 'a malformed -Id did not throw'
} catch {
    if ($_.FullyQualifiedErrorId -match 'InvalidDeploymentId') {
        Write-Host "EXPECTED FAILURE (malformed -Id): $($_.Exception.Message)"
    } else {
        Add-Failure "a malformed -Id failed for the wrong reason: $($_.Exception.Message)"
    }
}

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound). The resource group is a real path segment, so unlike the deployment name
# it is NOT silently tolerated.
try {
    Remove-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName azclips-test-group-does-not-exist `
        -Name $DeploymentName `
        -Confirm:$false `
        -ErrorAction Stop
    Add-Failure 'missing resource group did not throw'
} catch {
    if ($_.Exception.Message -match 'ResourceGroupNotFound|not found|404') {
        Write-Host "EXPECTED FAILURE (missing resource group): $($_.Exception.Message)"
    } else {
        Add-Failure "missing resource group failed for the wrong reason: $($_.Exception.Message)"
    }
}

# No missing-mandatory-parameter case here: omitting -Name on a Mandatory parameter makes
# PowerShell PROMPT for it, which would hang an unattended run. The CLI script
# (tests/Scripts/CLI/DeploymentDelete.sh) covers required-argument binding instead.

# Negative case: a deployment that is still running cannot be deleted. Arrange one via the REST
# passthrough, which returns as soon as ARM accepts the PUT and leaves the deployment Running.
$runningUri = "https://management.azure.com/subscriptions/$SubscriptionId/resourcegroups/$ResourceGroupName/providers/Microsoft.Resources/deployments/$RunningDeploymentName`?api-version=2025-04-01"

$runningPayload = @'
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
          "name": "azclipsdeploydelete",
          "location": "australiaeast",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
'@

$put = Invoke-AzRestMethod -Method PUT -Uri $runningUri -Payload $runningPayload

$runningIsArranged = $false
if ($put.StatusCode -ne 201) {
    Add-Failure "arrange PUT returned $($put.StatusCode), expected 201 - the running-delete case below cannot run"
} else {
    $runningIsArranged = $true
    Write-Host "ARRANGE: deployment '$RunningDeploymentName' accepted (201), now running"
}

# Only a 409 DeploymentCannotBeDeleted proves the running deployment was rejected for the right
# reason; a malformed request, a missing group or an auth failure would throw too and must not be
# mistaken for a pass.
if ($runningIsArranged) {
    try {
        Remove-AzResourceGroupDeployment `
            -SubscriptionId $SubscriptionId `
            -ResourceGroupName $ResourceGroupName `
            -Name $RunningDeploymentName `
            -Confirm:$false `
            -ErrorAction Stop
        Add-Failure 'deleting a running deployment did not throw'
    } catch {
        if ($_.Exception.Message -match 'DeploymentCannotBeDeleted|409|Conflict') {
            Write-Host "EXPECTED FAILURE (running deployment): $($_.Exception.Message)"
        } else {
            Add-Failure "deleting a running deployment failed for the wrong reason: $($_.Exception.Message)"
        }
    }
}

# Cleanup: cancel the in-flight deployment so it reaches a terminal state, poll until it gets there,
# then delete its record -- which also demonstrates that a cancelled deployment IS deletable.
Stop-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $RunningDeploymentName `
    -Confirm:$false

$state = $null
foreach ($attempt in 1..15) {
    $state = (Get-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $RunningDeploymentName).Properties.ProvisioningState
    if ($state -in @('Canceled', 'Cancelled', 'Succeeded', 'Failed')) { break }
    Start-Sleep -Seconds 2
}
Write-Host "CLEANUP: running deployment provisioningState is '$state'"
if ($state -notin @('Canceled', 'Cancelled', 'Succeeded', 'Failed')) {
    Add-Failure "running deployment never reached a terminal state (last state '$state')"
}

Remove-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $RunningDeploymentName `
    -Confirm:$false
Write-Host "CLEANUP: deployment record '$RunningDeploymentName' removed"

# The cancelled deployment may have created the storage account before it was stopped, and deleting
# the deployment record does not remove it. ARM answers 200 when the account existed and 204 when it
# did not, so both are a clean teardown.
$delete = Invoke-AzRestMethod `
    -Method DELETE `
    -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName`?api-version=2023-01-01"

if ($delete.StatusCode -in @(200, 204)) {
    Write-Host "CLEANUP: storage account '$StorageAccountName' removed (HTTP $($delete.StatusCode))"
} else {
    Add-Failure "cleanup returned $($delete.StatusCode); '$StorageAccountName' may still exist"
}

Remove-Item -Path $templateFile -ErrorAction SilentlyContinue

if ($script:failures -eq 0) {
    Write-Host 'RESULT: all assertions passed'
} else {
    Write-Host "RESULT: $($script:failures) unexpected outcome(s)" -ForegroundColor Red
}
exit $script:failures
