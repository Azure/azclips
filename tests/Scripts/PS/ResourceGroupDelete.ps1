# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live smoke test for Remove-AzResourceGroup.
#
# Prereqs:
#   1. Build and stage modules:
#      pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in to the target subscription.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the test resource groups

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$stamp = "$(Get-Date -Format 'yyyyMMddHHmmss')-$PID"
$nameGroup = "azclips-group-delete-name-$stamp"
$idGroup = "azclips-group-delete-id-$stamp"
$nameCreated = $false
$idCreated = $false

function Remove-TestGroup($name) {
    try {
        Invoke-AzRestMethod `
            -Method DELETE `
            -Uri "/subscriptions/$subscriptionId/resourcegroups/$name?api-version=2025-04-01" `
            -ErrorAction Stop | Out-Null
    } catch {
        Write-Warning "Resource group cleanup failed for '$name': $($_.Exception.Message)"
    }
}

try {
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $nameGroup -Location $location | Out-Null
    $nameCreated = $true

    $removedByName = Remove-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $nameGroup `
        -Force
    if ($removedByName -ne $true) {
        throw 'Remove-AzResourceGroup by name did not return true.'
    }
    $nameCreated = $false

    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $idGroup -Location $location | Out-Null
    $idCreated = $true
    $resourceGroupId = "/subscriptions/$subscriptionId/resourceGroups/$idGroup"

    $removedById = Remove-AzResourceGroup -Id $resourceGroupId -Force
    if ($removedById -ne $true) {
        throw 'Remove-AzResourceGroup by ID did not return true.'
    }
    $idCreated = $false

    Write-Host 'Remove-AzResourceGroup smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($nameCreated) {
        Remove-TestGroup $nameGroup
    }
    if ($idCreated) {
        Remove-TestGroup $idGroup
    }
}
