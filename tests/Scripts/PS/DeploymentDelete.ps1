# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Resource group deployment delete smoke test for the Azclips PowerShell surface
# (Remove-AzResourceGroupDeployment). Deleting a deployment removes the HISTORY RECORD only; it does
# not delete the resources it created or touch the resource group. Two behaviours are pinned here:
#   1. Deleting a name that does not exist SUCCEEDS SILENTLY (ARM answers 204 either way). This is a
#      deliberate divergence from legacy Azure PowerShell, which pre-checked existence and threw.
#   2. A deployment that is still RUNNING cannot be deleted (409); cancel it first.
# The happy path uses an EMPTY template, so it creates nothing. The running case is arranged with
# Invoke-AzRestMethod (no LRO polling), and it is cancelled, deleted and its storage account removed
# in the finally block.
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
#   AZCLIPS_RESOURCE_GROUP     - existing resource group; defaults to azclips-test-group-1
#   AZCLIPS_DEBUG              - set to 1 to show the cmdlet debug stream (HTTP traffic)
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\DeploymentDelete.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
$resourceGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_RESOURCE_GROUP)) { 'azclips-test-group-1' } else { $env:AZCLIPS_RESOURCE_GROUP }
$deploymentName = 'azclips-test-deployment-delete'
$runningDeploymentName = 'azclips-test-deployment-delete-running'
$missingDeploymentName = 'azclips-test-deployment-does-not-exist'
$storageAccountName = 'azclipsdeploydelete'
$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-empty-template-$([guid]::NewGuid()).json"
if ($env:AZCLIPS_DEBUG -eq '1') { $DebugPreference = 'Continue' }

$runningUri = "/subscriptions/$subscriptionId/resourcegroups/$resourceGroup/providers/Microsoft.Resources/deployments/$runningDeploymentName`?api-version=2025-04-01"
$storageUri = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$storageAccountName`?api-version=2023-01-01"

# Single-quoted here-strings: the ARM template's "$schema" key must NOT be interpolated.
$emptyTemplate = @'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": []
}
'@

$runningPayload = @'
{
  "properties": {
    "mode": "Incremental",
    "template": {
      "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "resources": [
        {
          "type": "Microsoft.Storage/storageAccounts",
          "apiVersion": "2023-01-01",
          "name": "azclipsdeploydelete",
          "location": "[resourceGroup().location]",
          "sku": { "name": "Standard_LRS" },
          "kind": "StorageV2"
        }
      ]
    }
  }
}
'@

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

$runningStarted = $false

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "New-AzResourceGroupDeployment $deploymentName (arrange, empty template)"
    Set-Content -Path $templateFile -Value $emptyTemplate -Encoding utf8
    $arranged = New-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -TemplateFile $templateFile
    Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

    Section 'Remove-AzResourceGroupDeployment (blocks until the delete LRO finishes; emits nothing)'
    $deleteOutput = Remove-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Confirm:$false
    Assert-Equal $null $deleteOutput 'delete: emits nothing'

    Section 'Get-AzResourceGroupDeployment after delete (must already be 404)'
    $message = Get-ErrorText {
        Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $deploymentName
    }
    Assert-Match 'DeploymentNotFound|not found|404' $message 'delete: record is gone'

    Section 'Remove-AzResourceGroupDeployment again, without -SubscriptionId (silent success)'
    Remove-AzResourceGroupDeployment -ResourceGroupName $resourceGroup -Name $deploymentName -Confirm:$false | Out-Host
    Write-Host 'PASS  already deleted: silent success, as ARM returns 204 either way' -ForegroundColor Green

    Section 'Remove-AzResourceGroupDeployment positional -ResourceGroupName/-Name'
    Remove-AzResourceGroupDeployment $resourceGroup $missingDeploymentName -Confirm:$false | Out-Host
    Write-Host 'PASS  positional: bound -ResourceGroupName/-Name' -ForegroundColor Green

    Section 'Remove-AzResourceGroupDeployment -DeploymentName (legacy alias)'
    Remove-AzResourceGroupDeployment -ResourceGroupName $resourceGroup -DeploymentName $missingDeploymentName -Confirm:$false | Out-Host
    Write-Host 'PASS  alias: bound -DeploymentName' -ForegroundColor Green

    Section 'Remove-AzResourceGroupDeployment -Id (subscription, group and name parsed from the id)'
    Remove-AzResourceGroupDeployment `
        -Id "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Resources/deployments/$missingDeploymentName" `
        -Confirm:$false | Out-Host
    Write-Host 'PASS  -Id: resolved resource group, name and subscription from the id' -ForegroundColor Green

    Section 'Remove-AzResourceGroupDeployment malformed -Id (rejected locally, no HTTP call)'
    $message = Get-ErrorText {
        Remove-AzResourceGroupDeployment -Id 'not-a-resource-id' -Confirm:$false
    }
    Assert-Match 'Invalid deployment id' $message 'malformed -Id is rejected locally'

    Section 'Remove-AzResourceGroupDeployment missing resource group (must surface 404)'
    $message = Get-ErrorText {
        Remove-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName 'azclips-test-group-does-not-exist' -Name $deploymentName -Confirm:$false
    }
    Assert-Match 'ResourceGroupNotFound|not found|404' $message 'missing resource group surfaces 404'

    Section "Invoke-AzRestMethod PUT $runningDeploymentName (arrange a Running deployment)"
    $put = Invoke-AzRestMethod -Method PUT -Uri $runningUri -Payload $runningPayload
    Assert-Match '^20[01]$' $put.StatusCode 'arrange: running deployment accepted'
    $runningStarted = $true

    Section 'Remove-AzResourceGroupDeployment on a running deployment (must be rejected with 409)'
    $message = Get-ErrorText {
        Remove-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $runningDeploymentName -Confirm:$false
    }
    Assert-Match 'DeploymentCannotBeDeleted|409|Conflict' $message 'running deployment cannot be deleted'

    Write-Host ''
    Write-Host 'All deployment delete smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($runningStarted) {
        # Cancel so the deployment reaches a terminal state, then delete its record -- which also
        # shows that a cancelled deployment IS deletable.
        Section "Stop + Remove-AzResourceGroupDeployment $runningDeploymentName (cleanup)"
        try {
            try {
                Stop-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $runningDeploymentName -Confirm:$false
            } catch {
                Write-Warning "Cancel skipped: $($_.Exception.Message)"
            }
            foreach ($attempt in 1..15) {
                $state = (Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $runningDeploymentName).Properties.ProvisioningState
                if ($state -in @('Canceled', 'Cancelled', 'Succeeded', 'Failed')) { break }
                Start-Sleep -Seconds 2
            }
            Remove-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $runningDeploymentName -Confirm:$false
            Write-Host "Removed deployment record '$runningDeploymentName' (final state $state)."
        } catch {
            Write-Warning "Running deployment cleanup failed: $($_.Exception.Message)"
        }

        # Deleting the record does not remove the storage account. 200 = deleted, 204 = never
        # existed; 409 while it is still provisioning, so retry.
        Section "Invoke-AzRestMethod DELETE storage account $storageAccountName (cleanup)"
        try {
            $status = $null
            foreach ($attempt in 1..12) {
                $status = (Invoke-AzRestMethod -Method DELETE -Uri $storageUri).StatusCode
                if ($status -in @(200, 204)) { break }
                Start-Sleep -Seconds 5
            }
            if ($status -notin @(200, 204)) { throw "last status $status" }
            Write-Host "Removed storage account '$storageAccountName' (HTTP $status)."
        } catch {
            Write-Warning "Storage account cleanup failed: $($_.Exception.Message)"
        }
    }

    Remove-Item -Path $templateFile -ErrorAction SilentlyContinue
}
