# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$deploymentName = 'azclips-test-deployment-mg-create'

$deployment = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $deploymentName
if ($deployment.Name -ne $deploymentName) {
    throw 'Show did not return the deployment.'
}

if ((Get-AzManagementGroupDeployment -Id $deployment.Id).Name -ne $deploymentName) {
    throw 'Get by deployment ID did not return the deployment.'
}

if ($deploymentName -notin (Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId).Name) {
    throw 'List did not return the deployment.'
}

try {
    Get-AzManagementGroupDeployment `
        -ManagementGroupId $managementGroupId `
        -Name 'azclips-test-deployment-mg-does-not-exist' `
        -ErrorAction Stop
    throw 'Show unexpectedly found a nonexistent deployment.'
}
catch {
    if ($_.Exception.Message -eq 'Show unexpectedly found a nonexistent deployment.') {
        throw
    }
    if ($_.Exception.Message -notmatch 'DeploymentNotFound') {
        throw
    }
    Write-Host 'OK: show rejected a nonexistent deployment'
}
