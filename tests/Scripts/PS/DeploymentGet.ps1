# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment read - PowerShell (Get-AzResourceGroupDeployment)
# Equivalent of: Get-AzResourceGroupDeployment
#
# Get/list are READ-ONLY. The one write below reuses a STABLE deployment name, so repeated
# runs overwrite the same deployment record instead of accumulating new ones -- nothing to
# clean up. The storage account it provisions is likewise idempotent (same name every run).
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription holding the test resource group
# Optional:
#   AZCLIPS_RESOURCE_GROUP  - existing resource group to read, defaults to azclips-test-group-1

$SubscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$ResourceGroupName = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { 'azclips-test-group-1' } else { $env:AZCLIPS_RESOURCE_GROUP }
$DeploymentName = 'azclips-test-deployment-get'

$BicepDir = Join-Path $PSScriptRoot '..' 'Bicep'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Every check is tallied rather than thrown, so all cases run; the script exits non-zero at the
# end if any failed (same pattern as DeploymentParametersQueryString.ps1).
$script:failures = 0
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

# Arrange: ensure there is a deployment to read back. Stable name -> idempotent.
$arranged = New-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsdeploymentget" `
    -Mode Incremental
Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

# Happy path (Get parameter set): retrieve the single deployment by name.
$deployment = Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName
Assert-Equal $DeploymentName $deployment.Name 'get: deployment name'
Assert-Equal 'Succeeded' $deployment.Properties.ProvisioningState 'get: provisioningState'

# Happy path (List parameter set, the default): omit -Name to enumerate the resource group.
# The cmdlet enumerates output, so this is a flat array of DeploymentExtended, not an envelope.
$deployments = @(Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName)
Assert-Equal $true ($DeploymentName -in $deployments.Name) "list: contains '$DeploymentName' among $($deployments.Count)"

# SubscriptionId is optional: it defaults to the active profile's subscription.
$deployment = Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -Name $DeploymentName
Assert-Equal $DeploymentName $deployment.Name 'default subscription: resolved from the active profile'

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound), not an empty success.
Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name azclips-test-deployment-does-not-exist `
    -ErrorVariable err | Out-Null
Assert-Match 'DeploymentNotFound|not found|404' $err.Message 'missing deployment surfaces 404'

# Negative case: a resource group that does not exist must surface an ARM 404
# (ResourceGroupNotFound), not an empty list.
Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName azclips-test-group-does-not-exist `
    -ErrorVariable err | Out-Null
Assert-Match 'ResourceGroupNotFound|not found|404' $err.Message 'missing resource group surfaces 404'

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
