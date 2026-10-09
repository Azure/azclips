# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Subscription scope deployment cancel - PowerShell (Stop-AzDeployment)
# Equivalent of: Stop-AzDeployment / Stop-AzSubscriptionDeployment
# Only an Accepted or Running deployment can be cancelled, and New-AzDeployment blocks until terminal,
# so the arrange step PUTs the deployment through Invoke-AzRestMethod, which returns as soon as ARM
# accepts it. The template creates a resource group named after the deployment plus a storage account
# in it, which keeps the deployment running long enough to cancel. The finally block deletes the
# deployment record and that resource group.
#
# Prereqs: build the solution and run Connect-AzAccount. Accounts must be imported before Resources.
# Optional: $env:AZCLIPS_SUBSCRIPTION_ID (defaults to the shared test subscription) and
#           $env:AZCLIPS_LOCATION (defaults to australiaeast).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { '282bb33f-067d-4179-a7a4-b833c13af735' }
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'australiaeast' }
$Name = "azclips-test-sub-cancel-$(Get-Date -Format 'yyMMddHHmmss')"
$Uri = "https://management.azure.com/subscriptions/$SubscriptionId/providers/Microsoft.Resources/deployments/$Name`?api-version=2025-04-01"

# Single-quoted here-string so "$schema" is not interpolated; the location is substituted afterwards.
$Payload = (@'
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
'@).Replace('__LOCATION__', $Location)

$failures = 0
function Check([bool]$Condition, [string]$Case, $Actual) {
    if ($Condition) { Write-Host "PASS  $Case" }
    else { Write-Host "FAIL  $Case => '$Actual'" -ForegroundColor Red; $script:failures++ }
}

function Wait-State([string[]]$States, [int]$Attempts) {
    $state = $null
    foreach ($attempt in 1..$Attempts) {
        $state = (Get-AzDeployment -SubscriptionId $SubscriptionId -Name $Name).Properties.ProvisioningState
        if ($state -in $States) { break }
        Start-Sleep -Seconds 2
    }
    $state
}

try {
    $put = Invoke-AzRestMethod -Method PUT -Uri $Uri -Payload $Payload
    Check ($put.StatusCode -eq 201) 'arrange: running deployment accepted' $put.StatusCode

    # Without a 201 from this run there is nothing of ours to cancel.
    if ($put.StatusCode -eq 201) {
        $state = Wait-State 'Accepted', 'Running' 15
        Check ($state -in 'Accepted', 'Running') 'arrange: deployment is running' $state

        # ARM answers 204 No Content, so the cmdlet emits nothing.
        $stopped = Stop-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false -ErrorVariable err
        Check ($err.Count -eq 0 -and $null -eq $stopped) 'cancels the running deployment silently' "$err"

        # ARM flips provisioningState only after the in-flight work settles.
        $state = Wait-State 'Canceled' 30
        Check ($state -eq 'Canceled') 'deployment reaches Canceled' $state

        Stop-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false -ErrorVariable err
        Check ("$err" -match 'DeploymentCannotBeCancelled') 'rejects cancelling a terminal deployment' "$err"
    }

    Stop-AzDeployment -SubscriptionId $SubscriptionId -Name 'azclips-test-sub-does-not-exist' -Confirm:$false -ErrorVariable err
    Check ("$err" -match 'DeploymentNotFound') 'missing deployment surfaces DeploymentNotFound' "$err"
}
finally {
    # Cancelling does not roll back what was created, so remove the resource group explicitly.
    Stop-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false -ErrorAction SilentlyContinue
    Remove-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false -ErrorAction SilentlyContinue
    Remove-AzResourceGroup -SubscriptionId $SubscriptionId -Name $Name -Force -ErrorAction SilentlyContinue
}

if ($failures -gt 0) {
    Write-Host "`n$failures check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll checks passed."
exit 0
