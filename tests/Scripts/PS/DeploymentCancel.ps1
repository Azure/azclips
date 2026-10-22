# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Resource group deployment cancel smoke test for the Azclips PowerShell surface
# (Stop-AzResourceGroupDeployment). A deployment is only cancellable while Accepted or Running, and
# New-AzResourceGroupDeployment blocks until terminal, so the arrange step PUTs the deployment through
# Invoke-AzRestMethod, which does NO LRO polling: it returns as soon as ARM accepts the request,
# leaving the deployment Running. Names are STABLE so reruns overwrite the same deployment record;
# the storage account the cancelled deployment may have partially created is deleted in finally.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\DeploymentCancel.ps1

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
$deploymentName = 'azclips-test-deployment-cancel'
$storageAccountName = 'azclipsdeploycancel'
# Used twice on purpose: without -WhatIf it must 404, with -WhatIf the identical call must stay
# silent, which proves -WhatIf never reached ARM.
$missingDeploymentName = 'azclips-test-deployment-does-not-exist'
if ($env:AZCLIPS_DEBUG -eq '1') { $DebugPreference = 'Continue' }

$deploymentUri = "/subscriptions/$subscriptionId/resourcegroups/$resourceGroup/providers/Microsoft.Resources/deployments/$deploymentName`?api-version=2025-04-01"
$storageUri = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$storageAccountName`?api-version=2023-01-01"

# Single-quoted here-string: the ARM template's "$schema" key must NOT be interpolated.
$templatePayload = @'
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
          "name": "azclipsdeploycancel",
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

function Get-DeploymentState {
    (Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $deploymentName).Properties.ProvisioningState
}

$deploymentStarted = $false

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    Section "Invoke-AzRestMethod PUT $deploymentName (arrange a Running deployment)"
    # 201 the first time the name is created, 200 when it overwrites the existing record.
    $put = Invoke-AzRestMethod -Method PUT -Uri $deploymentUri -Payload $templatePayload
    Assert-Match '^20[01]$' $put.StatusCode 'arrange: deployment PUT accepted'
    $deploymentStarted = $true

    # Synchronize on a cancellable state instead of assuming the PUT left one; usually one GET.
    $state = $null
    foreach ($attempt in 1..15) {
        $state = Get-DeploymentState
        if ($state -in @('Accepted', 'Running')) { break }
        Start-Sleep -Seconds 2
    }
    Assert-Match '^(Accepted|Running)$' $state 'arrange: deployment is cancellable'

    Section 'Stop-AzResourceGroupDeployment (ARM answers 204; the cmdlet emits nothing)'
    $cancelOutput = Stop-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Confirm:$false
    Assert-Equal $null $cancelOutput 'cancel: emits nothing'

    # ARM acknowledges with 204 immediately but only flips to Canceled once the in-flight resource
    # operation settles, so the budget must outlast one storage account create.
    foreach ($attempt in 1..30) {
        $state = Get-DeploymentState
        if ($state -in @('Canceled', 'Cancelled')) { break }
        Start-Sleep -Seconds 2
    }
    Assert-Match '^(Canceled|Cancelled)$' $state 'cancel: provisioningState'

    Section 'Stop-AzResourceGroupDeployment on a terminal deployment, without -SubscriptionId (must fail)'
    $message = Get-ErrorText {
        Stop-AzResourceGroupDeployment -ResourceGroupName $resourceGroup -Name $deploymentName -Confirm:$false
    }
    Assert-Match 'DeploymentCannotBeCancelled|not in a cancellable|409' $message 'terminal deployment cannot be cancelled'

    Section 'Stop-AzResourceGroupDeployment missing deployment (must surface 404)'
    $message = Get-ErrorText {
        Stop-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name $missingDeploymentName -Confirm:$false
    }
    Assert-Match 'DeploymentNotFound|not found|404' $message 'missing deployment surfaces 404'

    Section 'Stop-AzResourceGroupDeployment missing resource group (must surface 404)'
    $message = Get-ErrorText {
        Stop-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName 'azclips-test-group-does-not-exist' -Name $deploymentName -Confirm:$false
    }
    Assert-Match 'ResourceGroupNotFound|not found|404' $message 'missing resource group surfaces 404'

    Section 'Stop-AzResourceGroupDeployment -WhatIf (must not reach ARM)'
    $whatIfOutput = Stop-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $missingDeploymentName `
        -WhatIf
    Assert-Equal $null $whatIfOutput '-WhatIf: silent, no 404'

    Section 'Stop-AzResourceGroupDeployment -Name is mandatory (binding metadata)'
    # Invoking without -Name would PROMPT and hang an unattended run, so assert the metadata instead.
    $nameMandatory = (Get-Command Stop-AzResourceGroupDeployment).Parameters['Name'].Attributes |
        Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } |
        ForEach-Object { $_.Mandatory }
    Assert-Equal $true ($nameMandatory -contains $true) 'binding: -Name is mandatory'

    Write-Host ''
    Write-Host 'All deployment cancel smoke checks completed.' -ForegroundColor Green
}
finally {
    if ($deploymentStarted) {
        # Cancelling does not roll anything back, so the storage account may exist. 200 = deleted,
        # 204 = never existed. An account still provisioning answers 409 until it settles, so retry.
        # The deployment record is left behind on purpose: the stable name is overwritten next run.
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
}
