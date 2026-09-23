# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ErrorActionPreference = 'Stop'
$managementGroupId = 'azclips-test-mg'
$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$location = 'eastus'
# This dedicated test group is reused and intentionally not deleted by individual scripts.
$resourceGroupName = 'azclips-mg-test-rg'
$accountsManifest = Join-Path $PSScriptRoot '..' '..' '..' 'src' 'PS' 'Accounts' 'Accounts.psd1'
$resourcesManifest = Join-Path $PSScriptRoot '..' '..' '..' 'src' 'PS' 'Resources' 'Resources.psd1'
Import-Module $accountsManifest -Force
Import-Module $resourcesManifest -Force

$managementGroup = Invoke-AzRestMethod -Method GET -Uri "https://management.azure.com/providers/Microsoft.Management/managementGroups/$managementGroupId`?api-version=2021-04-01"
$managementGroupDetails = $managementGroup.Content | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($managementGroupDetails.properties.details.parent.id)) {
    throw "$managementGroupId must identify a child management group, not the tenant root management group."
}
$resourceGroupAction = 'reused'
try {
    Get-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -ErrorAction Stop | Out-Null
} catch {
    if ($_.Exception.Message -notmatch '404|ResourceGroupNotFound|could not be found') {
        throw
    }
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -Location $location | Out-Null
    $resourceGroupAction = 'created'
}
$bicepDir = Join-Path $PSScriptRoot '..' 'Bicep'
$name = "azclips-mg-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
Write-Host "Target child management group: $managementGroupId"
Write-Host "Test resource group: $resourceGroupName ($resourceGroupAction)"

# Create with -WhatIf:$false to verify the normal create path.
$deployment = New-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Location $location `
    -Name $name `
    -TemplateFile (Join-Path $bicepDir 'management-group-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'management-group-empty.bicepparam') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName", 'marker=from-inline' `
    -ValidationLevel Provider `
    -WhatIf:$false `
    -Confirm:$false
$deployment

# Exercise list, name, ID, input-object, and PassThru surfaces.
$deployments = @(Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId)
if ($name -notin $deployments.Name) {
    throw 'Created deployment was not returned by list.'
}
$deployments
$byName = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name
$byName
$byId = Get-AzManagementGroupDeployment -Id $byName.Id
if ($byId.Name -ne $name) {
    throw 'Get by deployment ID returned the wrong deployment.'
}
$byId

$nestedDeployment = Invoke-AzRestMethod `
    -Method GET `
    -Uri "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.Resources/deployments/azclips-management-group-nested?api-version=2022-09-01"
$nestedState = ($nestedDeployment.Content | ConvertFrom-Json).properties.provisioningState
if ($nestedState -ne 'Succeeded') {
    throw "Nested resource-group deployment did not succeed: $nestedState"
}
$nestedDeployment

try {
    $byName | Stop-AzManagementGroupDeployment -PassThru -Confirm:$false -ErrorAction Stop
} catch {
    if ($_.Exception.Message -notmatch 'not running|cannot be canceled') {
        throw
    }
    Write-Warning $_.Exception.Message
}

$removed = $byName | Remove-AzManagementGroupDeployment -PassThru -Confirm:$false
if (-not $removed) {
    throw 'Remove-AzManagementGroupDeployment -PassThru did not return true.'
}
$removed

Write-Host "Management-group deployment lifecycle completed for $name."
