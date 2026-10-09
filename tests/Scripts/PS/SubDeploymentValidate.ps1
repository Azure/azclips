# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Subscription scope deployment validate - PowerShell (Test-AzDeployment)
# Equivalent of: Test-AzDeployment / Test-AzSubscriptionDeployment
# Validation is read-only: it creates no deployment record and no resources, so there is no cleanup.
#
# Prereqs: build the solution and run Connect-AzAccount. Accounts must be imported before Resources.
# Optional: $env:AZCLIPS_SUBSCRIPTION_ID (defaults to the shared test subscription) and
#           $env:AZCLIPS_LOCATION (defaults to australiaeast).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$SubscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { '282bb33f-067d-4179-a7a4-b833c13af735' }
$Location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'australiaeast' }
$Name = "azclips-test-sub-validate-$(Get-Date -Format 'yyMMddHHmmss')"

# Public quickstart: creates resource group rgName in rgLocation (defaults to the deployment location).
$TemplateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/subscription-deployments/create-rg/azuredeploy.json'

$failures = 0
function Check([bool]$Condition, [string]$Case, $Actual) {
    if ($Condition) { Write-Host "PASS  $Case" }
    else { Write-Host "FAIL  $Case => '$Actual'" -ForegroundColor Red; $script:failures++ }
}

$result = Test-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Location $Location `
    -TemplateUri $TemplateUri -Parameter "rgName=$Name" -ErrorVariable err
Check ($err.Count -eq 0 -and $result.Properties.ProvisioningState -eq 'Succeeded') 'validates the template' "$err"

# rgName is a required template parameter with no default.
Test-AzDeployment -SubscriptionId $SubscriptionId -Name $Name -Location $Location -TemplateUri $TemplateUri -ErrorVariable err
Check ("$err" -match 'rgName') 'reports the missing rgName parameter' "$err"

if ($failures -gt 0) {
    Write-Host "`n$failures check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll checks passed."
exit 0
