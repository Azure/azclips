# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live smoke test for Get-AzResourceGroupDeploymentOperation.
#
# Prereqs:
#   1. Build and stage modules:
#      pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in to the target subscription.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource group
#
# Optional:
#   AZCLIPS_LOCATION - region for the test resource group (default: westus2)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$resourceGroupName = "azclips-deployment-operation-$stamp-$PID"
$deploymentName = "azclips-deployment-operation-$stamp"
$templateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json'
$created = $false

try {
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -Location $location | Out-Null
    $created = $true

    New-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -Name $deploymentName `
        -TemplateUri $templateUri `
        -Mode Incremental | Out-Null

    # List parameter set: no -OperationId.
    $operations = @(Get-AzResourceGroupDeploymentOperation `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroupName `
            -DeploymentName $deploymentName)
    if ($operations.Count -eq 0) {
        throw 'Get-AzResourceGroupDeploymentOperation returned no operations.'
    }
    Write-Host "List returned $($operations.Count) operation(s)."

    # -Name is the legacy alias for -DeploymentName.
    $aliased = @(Get-AzResourceGroupDeploymentOperation `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroupName `
            -Name $deploymentName)
    if ($aliased.Count -ne $operations.Count) {
        throw 'The -Name alias did not return the same operations as -DeploymentName.'
    }

    # Get parameter set: -OperationId selects a single operation.
    $operationId = $operations[0].OperationId
    $single = Get-AzResourceGroupDeploymentOperation `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -DeploymentName $deploymentName `
        -OperationId $operationId
    if ($single.OperationId -ne $operationId) {
        throw "Get-AzResourceGroupDeploymentOperation -OperationId returned '$($single.OperationId)' instead of '$operationId'."
    }
    if ($null -eq $single.Properties -or [string]::IsNullOrWhiteSpace($single.Properties.ProvisioningState)) {
        throw 'The returned deployment operation did not carry a provisioning state.'
    }

    # Negative case: an unknown operation id must fail rather than return an empty result.
    try {
        Get-AzResourceGroupDeploymentOperation `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroupName `
            -DeploymentName $deploymentName `
            -OperationId '00000000000000000000' `
            -ErrorAction Stop | Out-Null
        Write-Host 'UNEXPECTED: unknown operation id did not throw' -ForegroundColor Red
    }
    catch {
        Write-Host "EXPECTED FAILURE (unknown operation id): $($_.Exception.Message)"
    }

    Write-Host 'Get-AzResourceGroupDeploymentOperation smoke checks completed.' -ForegroundColor Green
}
finally {
    # Deleting the resource group removes the deployment, its operations, and every resource the
    # template provisioned, so a single delete is the complete cleanup for this test.
    if ($created) {
        try {
            Invoke-AzRestMethod `
                -Method DELETE `
                -Uri "/subscriptions/$subscriptionId/resourcegroups/$resourceGroupName?api-version=2025-04-01" `
                -ErrorAction Stop | Out-Null
        }
        catch {
            Write-Warning "Resource group cleanup failed for '$resourceGroupName': $($_.Exception.Message)"
        }
    }
}
