# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Live smoke test for Set-AzResourceGroup.
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
Import-Module (Join-Path $repoRoot 'artifacts\Debug\Microsoft.Azure.Azclips.Extensions.Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'westus2' } else { $env:AZCLIPS_LOCATION }
$name = "azclips-group-update-$(Get-Date -Format 'yyyyMMddHHmmss')-$PID"
$created = $false

try {
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $name -Location $location | Out-Null
    $created = $true

    $updated = Set-AzResourceGroup `
        -SubscriptionId $subscriptionId `
        -Name $name `
        -Tag @{ Environment = 'test'; Owner = 'team-a' }
    if ($updated.Tags.Environment -ne 'test' -or $updated.Tags.Owner -ne 'team-a') {
        throw 'Set-AzResourceGroup by name did not replace the resource group tags.'
    }

    $resourceGroupId = "/subscriptions/$subscriptionId/resourceGroups/$name"
    $updated = Set-AzResourceGroup -Id $resourceGroupId -Tag @{ Owner = 'team-b' }
    if ($updated.Tags.Count -ne 1 -or $updated.Tags.Owner -ne 'team-b') {
        throw 'Set-AzResourceGroup by ID did not replace the resource group tags.'
    }

    Write-Host 'Set-AzResourceGroup smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($created) {
        Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $name -Force -ErrorAction SilentlyContinue | Out-Null
    }
}
