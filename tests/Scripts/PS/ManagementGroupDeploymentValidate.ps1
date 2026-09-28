# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ErrorActionPreference = 'Stop'
$managementGroupId = $env:AZCLIPS_MANAGEMENT_GROUP_ID
$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($managementGroupId)) {
    throw 'Set AZCLIPS_MANAGEMENT_GROUP_ID to a child management group.'
}
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'Set AZCLIPS_SUBSCRIPTION_ID to the signed-in subscription.'
}
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
Write-Host "Target child management group: $managementGroupId"
Write-Host "Test resource group: $resourceGroupName ($resourceGroupAction)"

# Validate a template object with a parameter object.
$templateObject = Get-Content (Join-Path $bicepDir 'management-group-empty.json') -Raw | ConvertFrom-Json -AsHashtable
$parameterObject = @{
    subscriptionId = @{ value = $subscriptionId }
    resourceGroupName = @{ value = $resourceGroupName }
    marker = @{ value = 'from-object' }
}

$objectValidation = Test-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Location $location `
    -Name azclips-mg-validate-object `
    -TemplateObject $templateObject `
    -TemplateParameterObject $parameterObject `
    -ValidationLevel Provider
$objectValidation

# Validate Bicep with bicepparam.
$bicepValidation = Test-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Location $location `
    -Name azclips-mg-validate-bicep `
    -TemplateFile (Join-Path $bicepDir 'management-group-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'management-group-empty.bicepparam') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName" `
    -ValidationLevel Template
$bicepValidation

# Invalid templates return validation errors, matching Test-AzManagementGroupDeployment.
$invalid = Test-AzManagementGroupDeployment `
    -ManagementGroupId $managementGroupId `
    -Location $location `
    -Name azclips-mg-validate-invalid `
    -TemplateFile (Join-Path $bicepDir 'management-group-invalid-template.json') `
    -TemplateParameterObject @{
        subscriptionId = @{ value = $subscriptionId }
        resourceGroupName = @{ value = $resourceGroupName }
    }
if (-not $invalid.Error) {
    throw 'Invalid template validation did not return an error.'
}
$invalid

Write-Host 'Management-group deployment validation checks completed.'
