# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Subscription scope deployment delete - PowerShell (Remove-AzDeployment)
# Equivalent of: Remove-AzDeployment / Remove-AzSubscriptionDeployment
# Deleting removes the history record only, never the resources it created. Deleting a name that does
# not exist succeeds silently because ARM answers 204 either way. Arranges a deployment of an empty
# template, so nothing else needs cleaning up.
#
# Prereqs: build the solution and run Connect-AzAccount. Accounts must be imported before Resources.
# Optional: $env:AZCLIPS_SUBSCRIPTION_ID (defaults to the shared test subscription) and
#           $env:AZCLIPS_LOCATION (defaults to australiaeast).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { '282bb33f-067d-4179-a7a4-b833c13af735' }
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'australiaeast' }
$Name = "azclips-test-sub-delete-$(Get-Date -Format 'yyMMddHHmmss')"
$TemplateFile = Join-Path $repoRoot 'tests\Scripts\Bicep\subscription-empty-template.json'

$failures = 0
function Check([bool]$Condition, [string]$Case, $Actual) {
    if ($Condition) { Write-Host "PASS  $Case" }
    else { Write-Host "FAIL  $Case => '$Actual'" -ForegroundColor Red; $script:failures++ }
}

try {
    $deployment = New-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Location $Location -TemplateFile $TemplateFile
    Check ($deployment.Properties.ProvisioningState -eq 'Succeeded') 'arrange: deployment created' $deployment.Properties.ProvisioningState

    # Blocks until the delete LRO finishes and has no response body.
    $removed = Remove-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Confirm:$false -ErrorVariable err
    Check ($err.Count -eq 0 -and $null -eq $removed) 'deletes silently' "$err"

    # Cmdlet failures are terminating and would skip the rest of this block, so expected ones are caught inline.
    $err = try { Get-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -ErrorAction Stop } catch { $_ }
    Check ("$err" -match 'DeploymentNotFound') 'record is gone' "$err"

    # Legacy alias, on a name that no longer exists.
    Remove-AzSubscriptionDeployment -SubscriptionId $SubscriptionId -DeploymentName $Name -Confirm:$false -ErrorVariable err
    Check ($err.Count -eq 0) 'already deleted: silent success via legacy alias' "$err"
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
