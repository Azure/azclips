# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Subscription deployment cancel smoke test for the Azclips PowerShell surface
# (Stop-AzDeployment / Stop-AzSubscriptionDeployment). Only an Accepted or Running deployment can be
# cancelled, and New-AzDeployment blocks until terminal, so the arrange step PUTs the deployment
# through Invoke-AzRestMethod, which returns as soon as ARM accepts it. The template creates a
# resource group named after the deployment plus a storage account in it, which keeps the deployment
# running long enough to cancel. The finally block deletes the deployment record and that resource group.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\SubDeploymentCancel.ps1

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
$deploymentName = "azclips-test-sub-cancel-$stamp"
$deploymentUri = "https://management.azure.com/subscriptions/$subscriptionId/providers/Microsoft.Resources/deployments/$deploymentName`?api-version=2025-04-01"

# Single-quoted here-string so "$schema" is not interpolated; the location is substituted afterwards.
$templatePayload = (@'
{
  "location": "__LOCATION__",
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Resources/resourceGroups",
          "apiVersion": "2022-09-01",
          "name": "[deployment().name]",
          "location": "[deployment().location]"
        },
        {
          "type": "Microsoft.Resources/deployments",
          "apiVersion": "2022-09-01",
          "name": "storage",
          "resourceGroup": "[deployment().name]",
          "dependsOn": [ "[subscriptionResourceId('Microsoft.Resources/resourceGroups', deployment().name)]" ],
          "properties": {
            "mode": "Incremental",
            "template": {
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "resources": [
                {
                  "type": "Microsoft.Storage/storageAccounts",
                  "apiVersion": "2023-01-01",
                  "name": "[concat('azclips', uniqueString(deployment().name))]",
                  "location": "[deployment().location]",
                  "sku": { "name": "Standard_LRS" },
                  "kind": "StorageV2"
                }
              ]
            }
          }
        }
      ]
    }
  }
}
'@).Replace('__LOCATION__', $location)

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

# Polls the deployment every 2s until it reaches one of $States or $Attempts run out; returns the last state.
function Wait-DeploymentState([string[]]$States, [int]$Attempts) {
    $state = $null
    foreach ($attempt in 1..$Attempts) {
        $state = (Get-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName).ProvisioningState
        if ($state -in $States) { break }
        Start-Sleep -Seconds 2
    }
    $state
}

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "Invoke-AzRestMethod PUT $deploymentName (arrange a Running deployment)"
    $put = Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $templatePayload
    Assert-Equal 201 $put.StatusCode 'arrange: running deployment accepted'

    $state = Wait-DeploymentState 'Accepted', 'Running' 15
    Assert-Match '^(Accepted|Running)$' $state 'arrange: deployment is running'

    Section 'Stop-AzDeployment (ARM answers 204 No Content; the cmdlet emits nothing)'
    $stopped = Stop-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    Assert-Equal $null $stopped 'cancels the running deployment silently'

    # ARM flips provisioningState only after the in-flight work settles.
    $state = Wait-DeploymentState 'Canceled' 30
    Assert-Equal 'Canceled' $state 'deployment reaches Canceled'

    Section 'Stop-AzDeployment on a terminal deployment (must fail)'
    $message = Get-ErrorText {
        Stop-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    }
    Assert-Match 'DeploymentCannotBeCancelled' $message 'rejects cancelling a terminal deployment'

    Section 'Stop-AzDeployment missing deployment (must surface DeploymentNotFound)'
    $message = Get-ErrorText {
        Stop-AzDeployment -SubscriptionId $subscriptionId -Name 'azclips-test-sub-does-not-exist' -Confirm:$false
    }
    Assert-Match 'DeploymentNotFound' $message 'missing deployment surfaces DeploymentNotFound'

    Write-Host ''
    Write-Host 'All subscription deployment cancel smoke checks completed.' -ForegroundColor Green
}
finally {
    Section 'Stop-AzDeployment / Remove-AzDeployment cleanup'
    # Stop only matters if a check failed while the deployment was still running; on a terminal
    # deployment it fails by design, so its error is ignored.
    try { Stop-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false } catch { }
    try {
        Remove-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    } catch {
        Write-Warning "Deployment cleanup failed: $($_.Exception.Message)"
    }

    # Cancelling does not roll back what was created, so remove the resource group explicitly.
    Section "Remove-AzResourceGroup $deploymentName cleanup"
    try {
        $null = Remove-AzResourceGroup -SubscriptionId $subscriptionId -Name $deploymentName -Force
    } catch {
        Write-Warning "Could not delete resource group $deploymentName; delete it manually if it exists."
    }
}
