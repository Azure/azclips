# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Cancels an existing running management-group deployment and waits for Canceled.
# Add -UseId to exercise the resource-ID parameter set.
param(
    [Parameter(Mandatory)]
    [string] $ManagementGroupId,
    [Parameter(Mandatory)]
    [string] $Name,
    [switch] $UseId
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
Import-Module (Join-Path $repoRoot 'src\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'src\PS\Resources\Resources.psd1') -Force

if ($UseId) {
    $id = "/providers/Microsoft.Management/managementGroups/$ManagementGroupId/providers/Microsoft.Resources/deployments/$Name"
    $result = Stop-AzManagementGroupDeployment -Id $id -PassThru -Confirm:$false
}
else {
    $result = Stop-AzManagementGroupDeployment `
        -ManagementGroupId $ManagementGroupId `
        -Name $Name `
        -PassThru `
        -Confirm:$false
}

if ($result -ne $true) {
    throw 'Cancel did not return $true with -PassThru.'
}

$deadline = (Get-Date).AddSeconds(120)
do {
    $deployment = Get-AzManagementGroupDeployment -ManagementGroupId $ManagementGroupId -Name $Name
    $state = $deployment.Properties.ProvisioningState
    if ($state -eq 'Canceled') {
        return
    }
    if ($state -in @('Failed', 'Succeeded')) {
        throw "Deployment reached terminal state '$state' instead of 'Canceled'."
    }
    Start-Sleep -Seconds 5
} while ((Get-Date) -lt $deadline)

throw "Deployment did not reach 'Canceled' within 120 seconds. Last state: '$state'."
