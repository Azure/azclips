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
$arranged = New-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -TemplateFile $templateFile
Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

# Happy path: delete the terminal deployment. ARM answers 202 and the cmdlet polls the Location
# header until it returns 204, so this BLOCKS until the delete has actually finished. There is no
# response body, so the cmdlet emits NOTHING -- a silent return and no error is the pass condition.
# ConfirmImpact is Medium and $ConfirmPreference defaults to High, so this does not prompt; the
# explicit -Confirm:$false keeps it unattended even if the caller raised $ConfirmPreference.
$deleteOutput = Remove-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Confirm:$false `
    -ErrorVariable err
Assert-Equal 0 $err.Count 'delete: no error'
Assert-Equal $null $deleteOutput 'delete: emits nothing'

# Assert the record is gone. Because delete waits for the LRO to complete, no polling is needed
# here -- Get must ALREADY fail with DeploymentNotFound.
Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -ErrorVariable err | Out-Null
Assert-Match 'DeploymentNotFound|not found|404' $err.Message 'delete: record is gone'

# SubscriptionId is optional: it defaults to the active profile's subscription. Deleting the same
# name a second time is the "does not exist" case, which SUCCEEDS SILENTLY (see header note 1), so
# this call doubles as proof that -SubscriptionId binds optionally.
Remove-AzResourceGroupDeployment `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -Confirm:$false `
    -ErrorVariable err
Assert-Equal 0 $err.Count 'already deleted: silent success, as ARM returns 204 either way'

# Legacy binding parity, part 1: -ResourceGroupName is position 0 and -Name is position 1, so the
# parameter names can be omitted entirely. Still the "does not exist" path, so still silent.
Remove-AzResourceGroupDeployment $ResourceGroupName azclips-test-deployment-does-not-exist -Confirm:$false -ErrorVariable err
Assert-Equal 0 $err.Count 'positional: bound -ResourceGroupName/-Name'

# Legacy binding parity, part 2: -Name carries the legacy alias -DeploymentName.
Remove-AzResourceGroupDeployment `
    -ResourceGroupName $ResourceGroupName `
    -DeploymentName azclips-test-deployment-does-not-exist `
    -Confirm:$false `
    -ErrorVariable err
Assert-Equal 0 $err.Count 'alias: bound -DeploymentName'

# Legacy binding parity, part 3: the -Id parameter set. The subscription is read out of the id, so
# -SubscriptionId is not needed. -DeploymentId and -ResourceId are aliases of -Id.
Remove-AzResourceGroupDeployment `
    -Id "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Resources/deployments/azclips-test-deployment-does-not-exist" `
    -Confirm:$false `
    -ErrorVariable err
Assert-Equal 0 $err.Count '-Id: resolved resource group, name and subscription from the id'

# Negative case: a malformed -Id must be rejected by the orchestrator's id resolver BEFORE any HTTP
# call is made, rather than building a nonsense request URI.
Remove-AzResourceGroupDeployment -Id 'not-a-resource-id' -Confirm:$false -ErrorVariable err
Assert-Match 'Invalid deployment id' $err.Message 'malformed -Id is rejected locally'

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound). The resource group is a real path segment, so unlike the deployment name
# it is NOT silently tolerated.
Remove-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName azclips-test-group-does-not-exist `
    -Name $DeploymentName `
    -Confirm:$false `
    -ErrorVariable err
Assert-Match 'ResourceGroupNotFound|not found|404' $err.Message 'missing resource group surfaces 404'

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
Assert-Equal 201 $put.StatusCode 'arrange: running deployment accepted'

# Without a 201 from THIS run, the delete, cancel and poll below would act on whatever record an
# earlier run left behind and report its state as if it were this run's result, so they are skipped.
if ($put.StatusCode -eq 201) {
    # Only a 409 DeploymentCannotBeDeleted proves the running deployment was rejected for the right
    # reason; a malformed request, a missing group or an auth failure would fail too and must not be
    # mistaken for a pass.
    Remove-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $RunningDeploymentName `
        -Confirm:$false `
        -ErrorVariable err
    Assert-Match 'DeploymentCannotBeDeleted|409|Conflict' $err.Message 'running deployment cannot be deleted'

    # Cleanup: cancel the in-flight deployment so it reaches a terminal state, poll until it gets
    # there, then delete its record -- which also demonstrates that a cancelled deployment IS
    # deletable.
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
    Assert-Match '^(Canceled|Cancelled|Succeeded|Failed)$' $state 'cleanup: running deployment reached a terminal state'

    Remove-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name $RunningDeploymentName `
        -Confirm:$false `
        -ErrorVariable err
    Assert-Equal 0 $err.Count "cleanup: deployment record '$RunningDeploymentName' removed"
}

# The cancelled deployment may have created the storage account before it was stopped, and deleting
# the deployment record does not remove it. ARM answers 200 when the account existed and 204 when it
# did not, so both are a clean teardown.
$delete = Invoke-AzRestMethod `
    -Method DELETE `
    -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName`?api-version=2023-01-01"
Assert-Match '^20[04]$' $delete.StatusCode "cleanup: storage account '$StorageAccountName' removed"

Remove-Item -Path $templateFile -ErrorAction SilentlyContinue

if ($failures -gt 0) {
    Write-Host "`n$failures assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
