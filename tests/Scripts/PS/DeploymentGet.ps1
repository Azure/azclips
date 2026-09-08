# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Resource group scope deployment read - PowerShell (Get-AzResourceGroupDeployment)
# Equivalent of: Get-AzResourceGroupDeployment
#
# Get/list are READ-ONLY. The one write below reuses a STABLE deployment name, so repeated
# runs overwrite the same deployment record instead of accumulating new ones -- nothing to
# clean up. The storage account it provisions is likewise idempotent (same name every run).
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: azclips-test-group-1 (region: australiaeast)

$SubscriptionId = '282bb33f-067d-4179-a7a4-b833c13af735'
$ResourceGroupName = 'azclips-test-group-1'
$DeploymentName = 'azclips-test-deployment-get'

$BicepDir = Join-Path $PSScriptRoot '..' 'Bicep'

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

# Arrange: ensure there is a deployment to read back. Stable name -> idempotent.
New-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName `
    -TemplateFile (Join-Path $BicepDir 'sample-storage.bicep') `
    -Parameter "storageAccountName=azclipsdeploymentget" `
    -Mode Incremental

# Happy path (Get parameter set): retrieve the single deployment by name.
$deployment = Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $DeploymentName

if ($deployment.Name -ne $DeploymentName) {
    Write-Host "UNEXPECTED: got deployment '$($deployment.Name)', expected '$DeploymentName'" -ForegroundColor Red
} elseif ($deployment.Properties.ProvisioningState -ne 'Succeeded') {
    Write-Host "UNEXPECTED: provisioningState is '$($deployment.Properties.ProvisioningState)', expected 'Succeeded'" -ForegroundColor Red
} else {
    Write-Host "PASS (Get): $($deployment.Name) $($deployment.Properties.ProvisioningState) mode=$($deployment.Properties.Mode)"
}

# Happy path (List parameter set, the default): omit -Name to enumerate the resource group.
# The cmdlet enumerates output, so this is a flat array of DeploymentExtended, not an envelope.
$deployments = @(Get-AzResourceGroupDeployment `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName)

if ($deployments.Count -lt 1) {
    Write-Host 'UNEXPECTED: list returned no deployments' -ForegroundColor Red
} elseif ($DeploymentName -notin $deployments.Name) {
    Write-Host "UNEXPECTED: list of $($deployments.Count) did not contain '$DeploymentName'" -ForegroundColor Red
} else {
    Write-Host "PASS (List): $($deployments.Count) deployment(s), including '$DeploymentName'"
}

# SubscriptionId is optional: it defaults to the active profile's subscription.
Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -Name $DeploymentName | Out-Null
Write-Host 'PASS (default subscription): resolved from the active profile'

# Negative case: a deployment name that does not exist must surface an ARM 404
# (DeploymentNotFound), not an empty success.
try {
    Get-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName $ResourceGroupName `
        -Name azclips-test-deployment-does-not-exist `
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
# (ResourceGroupNotFound), not an empty list.
try {
    Get-AzResourceGroupDeployment `
        -SubscriptionId $SubscriptionId `
        -ResourceGroupName azclips-test-group-does-not-exist `
        -ErrorAction Stop
    Write-Host 'UNEXPECTED: missing resource group did not throw' -ForegroundColor Red
} catch {
    if ($_.Exception.Message -match 'ResourceGroupNotFound|not found|404') {
        Write-Host "EXPECTED FAILURE (missing resource group): $($_.Exception.Message)"
    } else {
        Write-Host "UNEXPECTED: $($_.Exception.Message)" -ForegroundColor Red
    }
}
