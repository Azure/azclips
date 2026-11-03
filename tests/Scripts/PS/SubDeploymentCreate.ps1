# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Subscription deployment create smoke test for the Azclips PowerShell surface
# (New-AzDeployment / New-AzSubscriptionDeployment). The template creates a resource group named
# after the deployment; the finally block deletes it and the deployment record, so a run leaves
# nothing behind.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID    - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID          - tenant to sign in with before running commands
#   AZCLIPS_LOCATION           - deployment location; defaults to australiaeast
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\SubDeploymentCreate.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$location = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_LOCATION)) { 'australiaeast' } else { $env:AZCLIPS_LOCATION }
$stamp = Get-Date -Format 'yyMMddHHmmss'
$deploymentName = "azclips-test-sub-create-$stamp"

# Public quickstart: creates resource group rgName in rgLocation (defaults to the deployment location).
$templateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/subscription-deployments/create-rg/azuredeploy.json'

function Section($title) {
    Write-Host ''
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Assert-Equal($Expected, $Actual, [string]$Case) {
    if (-not ($Expected -eq $Actual)) { throw "${Case}: expected '$Expected' but got '$Actual'." }
    Write-Host "PASS  $Case" -ForegroundColor Green
}

function Assert-Match([string]$Pattern, $Actual, [string]$Case) {
    if ($Actual -notmatch $Pattern) { throw "${Case}: expected a message matching '$Pattern' but got '$Actual'." }
    Write-Host "PASS  $Case" -ForegroundColor Green
}

# Runs a call that must fail and returns its error text; throws if it unexpectedly succeeds.
function Get-ErrorText([scriptblock]$Action) {
    try { & $Action | Out-Null } catch { return "$_" }
    throw 'Expected the command to fail, but it succeeded.'
}

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "New-AzDeployment $deploymentName (blocks until terminal)"
    $deployment = New-AzDeployment `
        -SubscriptionId $subscriptionId `
        -Name $deploymentName `
        -Location $location `
        -TemplateUri $templateUri `
        -Parameter "rgName=$deploymentName"
    Assert-Equal 'Succeeded' $deployment.Properties.ProvisioningState 'creates the deployment'

    Section 'New-AzDeployment without a template source (rejected locally, before any request)'
    $message = Get-ErrorText {
        New-AzDeployment -SubscriptionId $subscriptionId -Name "$deploymentName-none" -Location $location
    }
    Assert-Match 'Exactly one of TemplateUri' $message 'rejects a missing template source'

    Write-Host ''
    Write-Host 'All subscription deployment create smoke checks completed.' -ForegroundColor Green
}
finally {
    Section 'Remove-AzDeployment cleanup'
    try {
        Remove-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    } catch {
        Write-Warning "Deployment cleanup failed: $($_.Exception.Message)"
    }

    # Deleting a deployment does not delete what it created, so remove the resource group explicitly.
    Section "Remove-AzResourceGroup $deploymentName cleanup"
    try {
        $null = Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $deploymentName -Force
    } catch {
        Write-Warning "Could not delete resource group $deploymentName; delete it manually if it exists."
    }
}
