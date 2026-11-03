# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Subscription deployment validate smoke test for the Azclips PowerShell surface
# (Test-AzDeployment / Test-AzSubscriptionDeployment). Validation is READ-ONLY: it creates no
# deployment record and no resources, so there is nothing to clean up.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\SubDeploymentValidate.ps1

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
$deploymentName = "azclips-test-sub-validate-$stamp"

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

if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
    Section 'Connect-AzAccount'
    Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
}

Section 'Test-AzDeployment -TemplateUri with rgName'
$result = Test-AzDeployment `
    -SubscriptionId $subscriptionId `
    -Name $deploymentName `
    -Location $location `
    -TemplateUri $templateUri `
    -Parameter "rgName=$deploymentName"
Assert-Equal 'Succeeded' $result.Properties.ProvisioningState 'validates the template'

Section 'Test-AzDeployment without rgName (required template parameter with no default)'
$message = Get-ErrorText {
    Test-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Location $location -TemplateUri $templateUri
}
Assert-Match 'rgName' $message 'reports the missing rgName parameter'

Write-Host ''
Write-Host 'All subscription deployment validate smoke checks completed.' -ForegroundColor Green
