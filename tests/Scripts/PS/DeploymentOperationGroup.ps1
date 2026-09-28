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

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$resourceGroupName = "azclips-deployment-op-$stamp-$PID"
$deploymentName = "azclips-deployment-op-$stamp"
$templateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json'
$created = $false

try {
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -Location $location | Out-Null
    $created = $true

    # The deployment must reach a terminal state before it reports any operations; the create
    # cmdlet already blocks until then.
    New-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -Name $deploymentName `
        -TemplateUri $templateUri `
        -Mode Incremental | Out-Null

    $operations = @(Get-AzResourceGroupDeploymentOperation `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -DeploymentName $deploymentName)
    if ($operations.Count -eq 0) {
        throw 'Get-AzResourceGroupDeploymentOperation returned no operations for the deployment.'
    }

    $aliased = @(Get-AzResourceGroupDeploymentOperation `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -Name $deploymentName)
    if ($aliased.Count -ne $operations.Count) {
        throw 'The -Name alias for -DeploymentName did not return the same operations.'
    }

    $operationId = $operations[0].OperationId
    if ([string]::IsNullOrWhiteSpace($operationId)) {
        throw 'The listed deployment operation did not carry an OperationId.'
    }

    $single = Get-AzResourceGroupDeploymentOperation `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroupName `
        -DeploymentName $deploymentName `
        -OperationId $operationId
    if ($single.OperationId -ne $operationId) {
        throw "Get-AzResourceGroupDeploymentOperation -OperationId returned '$($single.OperationId)' instead of '$operationId'."
    }

    Write-Host 'Get-AzResourceGroupDeploymentOperation smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($created) {
        Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -Force -ErrorAction SilentlyContinue | Out-Null
    }
}
