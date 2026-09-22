# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$resourceGroup = 'azclips-mg-test-rg'
$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$runId = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
$deploymentName = "azclips-test-deployment-mg-cancel-$runId"
$bicepDir = Join-Path $PSScriptRoot '..' 'Bicep'
$templateFile = Join-Path $bicepDir 'management-group-cancel.json'

$payload = @{
    location = 'eastus'
    properties = @{
        mode = 'Incremental'
        template = Get-Content $templateFile -Raw | ConvertFrom-Json -AsHashtable
        parameters = @{
            subscriptionId = @{ value = $subscriptionId }
            resourceGroupName = @{ value = $resourceGroup }
            runId = @{ value = $runId }
        }
    }
} | ConvertTo-Json -Depth 100
$deploymentUri = "https://management.azure.com/providers/Microsoft.Management/managementGroups/$managementGroupId/providers/Microsoft.Resources/deployments/$deploymentName`?api-version=2025-04-01"
Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $payload |
    Out-Null

$deadline = (Get-Date).AddSeconds(120)
do {
    $deployment = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $deploymentName
    $state = $deployment.Properties.ProvisioningState
    if ($state -in @('Accepted', 'Running')) {
        break
    }
    if ($state -in @('Failed', 'Succeeded', 'Canceled')) {
        throw "Deployment reached terminal state '$state' before cancellation could start."
    }
    Start-Sleep -Seconds 1
} while ((Get-Date) -lt $deadline)

if ($state -notin @('Accepted', 'Running')) {
    throw "Deployment did not enter an active state before cancellation. Last state: '$state'."
}

$result = Stop-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Name $deploymentName `
    -PassThru `
    -Confirm:$false

if ($result -ne $true) {
    throw 'Cancel did not return $true with -PassThru.'
}

$deadline = (Get-Date).AddSeconds(120)
do {
    $deployment = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $deploymentName
    $state = $deployment.Properties.ProvisioningState
    if ($state -eq 'Canceled') {
        Write-Host "cancel provisioningState -> $state"
        break
    }
    if ($state -in @('Failed', 'Succeeded')) {
        throw "Deployment reached terminal state '$state' instead of 'Canceled'."
    }
    Start-Sleep -Seconds 5
} while ((Get-Date) -lt $deadline)

if ($state -ne 'Canceled') {
    throw "Deployment did not reach 'Canceled' within 120 seconds. Last state: '$state'."
}

try {
    Stop-AzManagementGroupDeployment `
        -ManagementGroupId $managementGroupId `
        -Name 'azclips-test-deployment-mg-does-not-exist' `
        -Confirm:$false `
        -ErrorAction Stop
    throw 'Cancel unexpectedly accepted a nonexistent deployment.'
}
catch {
    if ($_.Exception.Message -notmatch 'DeploymentNotFound') {
        throw
    }
    Write-Host 'OK: cancel rejected a nonexistent deployment'
}
