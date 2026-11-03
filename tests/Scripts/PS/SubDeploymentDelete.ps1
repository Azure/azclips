# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Subscription deployment delete smoke test for the Azclips PowerShell surface
# (Remove-AzDeployment / Remove-AzSubscriptionDeployment). Deleting removes the history record only,
# never the resources it created. Deleting a name that does not exist succeeds silently because ARM
# answers 204 either way. Arranges a deployment of an empty template, so nothing else needs cleaning up.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\SubDeploymentDelete.ps1

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
$deploymentName = "azclips-test-sub-delete-$stamp"
$templateFile = Join-Path $repoRoot 'tests\Scripts\Bicep\subscription-empty-template.json'

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

    Section "New-AzDeployment $deploymentName (arrange, empty template)"
    $deployment = New-AzDeployment `
        -SubscriptionId $subscriptionId `
        -Name $deploymentName `
        -Location $location `
        -TemplateFile $templateFile
    Assert-Equal 'Succeeded' $deployment.Properties.ProvisioningState 'arrange: deployment created'

    Section 'Remove-AzDeployment (blocks until the delete LRO finishes; no response body)'
    $removed = Remove-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    Assert-Equal $null $removed 'deletes silently'

    Section 'Get-AzDeployment after delete (must surface DeploymentNotFound)'
    $message = Get-ErrorText {
        Get-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName
    }
    Assert-Match 'DeploymentNotFound' $message 'record is gone'

    Section 'Remove-AzSubscriptionDeployment on the already-deleted name (legacy alias; must not be treated as an error)'
    Remove-AzSubscriptionDeployment -SubscriptionId $subscriptionId -DeploymentName $deploymentName -Confirm:$false
    Write-Host 'PASS  already deleted: silent success via legacy alias' -ForegroundColor Green

    Write-Host ''
    Write-Host 'All subscription deployment delete smoke checks completed.' -ForegroundColor Green
}
finally {
    Section 'Remove-AzDeployment cleanup'
    try {
        Remove-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    } catch {
        Write-Warning "Deployment cleanup failed: $($_.Exception.Message)"
    }
}
