# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$resourceGroup = 'azclips-mg-test-rg'
$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$templateFile = Join-Path $PSScriptRoot '..\Bicep\management-group-nested-empty.json'

Test-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Location eastus `
    -TemplateFile $templateFile `
    -subscriptionId $subscriptionId `
    -resourceGroupName $resourceGroup
