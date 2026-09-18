# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$resourceGroup = 'azclips-mg-test-rg'
$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$deploymentName = 'azclips-test-deployment-mg-cancel'
$templateFile = Join-Path $PSScriptRoot '..\Bicep\management-group-nested-empty.json'

New-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Name $deploymentName `
    -Location eastus `
    -TemplateFile $templateFile `
    -subscriptionId $subscriptionId `
    -resourceGroupName $resourceGroup `
    -NoWait `
    -Confirm:$false |
    Out-Null

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
    if ($_.Exception.Message -eq 'Cancel unexpectedly accepted a nonexistent deployment.') {
        throw
    }
    Write-Host 'OK: cancel rejected a nonexistent deployment'
}
