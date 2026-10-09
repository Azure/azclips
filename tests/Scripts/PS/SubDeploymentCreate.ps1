# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Subscription scope deployment create - PowerShell (New-AzDeployment)
# Equivalent of: New-AzDeployment / New-AzSubscriptionDeployment
# The template creates a resource group named after the deployment; the finally block deletes it and
# the deployment record, so a run leaves nothing behind.
#
# Prereqs: build the solution and run Connect-AzAccount. Accounts must be imported before Resources.
# Optional: $env:AZCLIPS_SUBSCRIPTION_ID (defaults to the shared test subscription) and
#           $env:AZCLIPS_LOCATION (defaults to australiaeast).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { '282bb33f-067d-4179-a7a4-b833c13af735' }
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'australiaeast' }
$Name = "azclips-test-sub-create-$(Get-Date -Format 'yyMMddHHmmss')"

# Public quickstart: creates resource group rgName in rgLocation (defaults to the deployment location).
$TemplateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/subscription-deployments/create-rg/azuredeploy.json'

# Checks are tallied rather than thrown so every case and the cleanup always run.
$failures = 0
function Check([bool]$Condition, [string]$Case, $Actual) {
    if ($Condition) { Write-Host "PASS  $Case" }
    else { Write-Host "FAIL  $Case => '$Actual'" -ForegroundColor Red; $script:failures++ }
}

try {
    # Blocks until the deployment is terminal.
    $deployment = New-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Location $Location `
        -TemplateUri $TemplateUri -Parameter "rgName=$Name"
    Check ($deployment.Properties.ProvisioningState -eq 'Succeeded') 'creates the deployment' $deployment.Properties.ProvisioningState

    # A missing template source is rejected locally, before any request. Cmdlet failures are terminating
    # and would skip the rest of this block, so expected ones are caught inline.
    $err = try { New-AzDeployment -SubscriptionId $SubscriptionId -Name "$Name-none" -Location $Location -ErrorAction Stop } catch { $_ }
    Check ("$err" -match 'Exactly one of TemplateUri') 'rejects a missing template source' "$err"
}
catch { Check $false 'no unexpected error' $_ }
finally {
    # Deleting a deployment does not delete what it created, so remove the resource group explicitly.
    # -ErrorAction can't silence terminating errors, so each cleanup call is guarded.
    try { Remove-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false } catch { }
    try { $null = Remove-AzResourceGroup -SubscriptionId $SubscriptionId -Name $Name -Force }
    catch { Write-Warning "Could not delete resource group $Name; delete it manually if it exists." }
}

if ($failures -gt 0) {
    Write-Host "`n$failures check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll checks passed."
exit 0
