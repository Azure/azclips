# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$resourceGroup = 'azclips-mg-test-rg'
$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$nameDeployment = 'azclips-test-deployment-mg-delete'
$idDeployment = 'azclips-test-deployment-mg-delete-id'
$templateFile = Join-Path $PSScriptRoot '..\Bicep\management-group-nested-empty.json'

foreach ($name in @($nameDeployment, $idDeployment)) {
    New-AzManagementGroupDeployment `
        -ManagementGroupId $managementGroupId `
        -Name $name `
        -Location eastus `
        -TemplateFile $templateFile `
        -subscriptionId $subscriptionId `
        -resourceGroupName $resourceGroup `
        -Confirm:$false |
        Out-Null
}

$removed = Remove-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Name $nameDeployment `
    -PassThru `
    -Confirm:$false
if ($removed -ne $true) {
    throw 'Delete by name did not return $true with -PassThru.'
}

$id = "/providers/Microsoft.Management/managementGroups/$managementGroupId/providers/Microsoft.Resources/deployments/$idDeployment"
$removed = Remove-AzManagementGroupDeployment -Id $id -PassThru -Confirm:$false
if ($removed -ne $true) {
    throw 'Delete by ID did not return $true with -PassThru.'
}

$remainingNames = @(Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId).Name
foreach ($name in @($nameDeployment, $idDeployment)) {
    if ($name -in $remainingNames) {
        throw "Deployment '$name' still exists."
    }
}

try {
    Remove-AzManagementGroupDeployment `
        -Id 'not-a-management-group-deployment-id' `
        -Confirm:$false `
        -ErrorAction Stop
    throw 'Delete unexpectedly accepted an invalid deployment ID.'
}
catch {
    if ($_.Exception.Message -eq 'Delete unexpectedly accepted an invalid deployment ID.') {
        throw
    }
    Write-Host 'OK: delete rejected an invalid deployment ID'
}
