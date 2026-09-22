# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$resourceGroup = 'azclips-mg-test-rg'
$managementGroups = Invoke-AzRestMethod -Method GET -Uri 'https://management.azure.com/providers/Microsoft.Management/managementGroups?api-version=2021-04-01'
$managementGroupId = ($managementGroups.Content | ConvertFrom-Json).value[0].name
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$nameDeployment = "azclips-test-deployment-mg-delete-$runId"
$idDeployment = "azclips-test-deployment-mg-delete-id-$runId"
$nameNestedDeployment = "azclips-delete-name-$runId"
$idNestedDeployment = "azclips-delete-id-$runId"
$bicepDir = Join-Path $PSScriptRoot '..' 'Bicep'
$templateFile = Join-Path $bicepDir 'management-group-nested-empty.json'

try {
    $deployments = @(
        @{ Name = $nameDeployment; NestedName = $nameNestedDeployment },
        @{ Name = $idDeployment; NestedName = $idNestedDeployment }
    )
    foreach ($deployment in $deployments) {
        New-AzManagementGroupDeployment `
            -ManagementGroupId $managementGroupId `
            -Name $deployment.Name `
            -Location eastus `
            -TemplateFile $templateFile `
            -TemplateParameterObject @{
                subscriptionId = $subscriptionId
                resourceGroupName = $resourceGroup
                nestedDeploymentName = $deployment.NestedName
            } `
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
        if ($_.Exception.Message -notlike '*Id must be a management-group deployment resource ID.*') {
            throw
        }
        Write-Host 'OK: delete rejected an invalid deployment ID'
    }
}
finally {
    $cleanupUris = @(
        "https://management.azure.com/providers/Microsoft.Management/managementGroups/$managementGroupId/providers/Microsoft.Resources/deployments/$nameDeployment`?api-version=2025-04-01",
        "https://management.azure.com/providers/Microsoft.Management/managementGroups/$managementGroupId/providers/Microsoft.Resources/deployments/$idDeployment`?api-version=2025-04-01",
        "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Resources/deployments/$nameNestedDeployment`?api-version=2025-04-01",
        "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Resources/deployments/$idNestedDeployment`?api-version=2025-04-01"
    )
    foreach ($cleanupUri in $cleanupUris) {
        try {
            Invoke-AzRestMethod -Method DELETE -Uri $cleanupUri -ErrorAction Stop | Out-Null
        }
        catch {
            Write-Verbose "Cleanup skipped for $cleanupUri`: $($_.Exception.Message)"
        }
    }
}
