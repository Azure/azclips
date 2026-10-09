# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Subscription scope deployment get/list - PowerShell (Get-AzDeployment)
# Equivalent of: Get-AzDeployment / Get-AzSubscriptionDeployment
# Arranges a deployment of an empty template (no resources), then deletes its record in the finally block.
#
# Prereqs: build the solution and run Connect-AzAccount. Accounts must be imported before Resources.
# Optional: $env:AZCLIPS_SUBSCRIPTION_ID (defaults to the shared test subscription) and
#           $env:AZCLIPS_LOCATION (defaults to australiaeast).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { '282bb33f-067d-4179-a7a4-b833c13af735' }
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'australiaeast' }
$Name = "azclips-test-sub-get-$(Get-Date -Format 'yyMMddHHmmss')"
$TemplateFile = Join-Path $repoRoot 'tests\Scripts\Bicep\subscription-empty-template.json'

$failures = 0
function Check([bool]$Condition, [string]$Case, $Actual) {
    if ($Condition) { Write-Host "PASS  $Case" }
    else { Write-Host "FAIL  $Case => '$Actual'" -ForegroundColor Red; $script:failures++ }
}

try {
    $deployment = New-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Location $Location -TemplateFile $TemplateFile
    Check ($deployment.Properties.ProvisioningState -eq 'Succeeded') 'arrange: deployment created' $deployment.Properties.ProvisioningState

    $byName = Get-AzDeployment -SubscriptionId $SubscriptionId -Name $Name
    Check ($byName.Name -eq $Name) 'gets by -Name' $byName.Name

    $byId = Get-AzDeployment -Id $deployment.Id
    Check ($byId.Name -eq $Name) 'gets by -Id' $byId.Name

    $list = Get-AzDeployment -SubscriptionId $SubscriptionId
    Check ($list.Name -contains $Name) 'lists the deployment' $list.Count

    $legacy = Get-AzSubscriptionDeployment -SubscriptionId $SubscriptionId -DeploymentName $Name
    Check ($legacy.Name -eq $Name) 'legacy Get-AzSubscriptionDeployment -DeploymentName' $legacy.Name

    # A resource-group-scoped id is rejected locally, before any request. Cmdlet failures are terminating
    # and would skip the rest of this block, so expected ones are caught inline.
    $err = try { Get-AzDeployment -Id "/subscriptions/$SubscriptionId/resourceGroups/rg/providers/Microsoft.Resources/deployments/$Name" -ErrorAction Stop } catch { $_ }
    Check ("$err" -match 'Invalid deployment id') 'rejects a resource-group-scoped -Id' "$err"

    $err = try { Get-AzDeployment -SubscriptionId $SubscriptionId -Name 'azclips-test-sub-does-not-exist' -ErrorAction Stop } catch { $_ }
    Check ("$err" -match 'DeploymentNotFound') 'missing deployment surfaces DeploymentNotFound' "$err"
}
catch { Check $false 'no unexpected error' $_ }
finally {
    try { Remove-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false } catch { }
}

if ($failures -gt 0) {
    Write-Host "`n$failures check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll checks passed."
exit 0
