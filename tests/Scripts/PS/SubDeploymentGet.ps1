# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Subscription deployment get/list smoke test for the Azclips PowerShell surface
# (Get-AzDeployment / Get-AzSubscriptionDeployment). Arranges a deployment of an empty template
# (no resources), reads it back by name, by id, via list and via the legacy alias, checks the
# rejection paths, then deletes the record in the finally block.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\SubDeploymentGet.ps1

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
$deploymentName = "azclips-test-sub-get-$stamp"
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

    Section 'Get-AzDeployment -Name'
    $byName = Get-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName
    Assert-Equal $deploymentName $byName.Name 'gets by -Name'

    Section 'Get-AzDeployment -Id'
    $byId = Get-AzDeployment -Id $deployment.Id
    Assert-Equal $deploymentName $byId.Name 'gets by -Id'

    Section 'Get-AzDeployment (list the subscription)'
    $deployments = @(Get-AzDeployment -SubscriptionId $subscriptionId)
    Assert-Equal $true ($deploymentName -in $deployments.Name) "lists the deployment among $($deployments.Count)"

    Section 'Get-AzSubscriptionDeployment -DeploymentName (legacy alias)'
    $legacy = Get-AzSubscriptionDeployment -SubscriptionId $subscriptionId -DeploymentName $deploymentName
    Assert-Equal $deploymentName $legacy.Name 'legacy Get-AzSubscriptionDeployment -DeploymentName'

    Section 'Get-AzDeployment with a resource-group-scoped -Id (rejected locally, before any request)'
    $message = Get-ErrorText {
        Get-AzDeployment -Id "/subscriptions/$subscriptionId/resourceGroups/rg/providers/Microsoft.Resources/deployments/$deploymentName"
    }
    Assert-Match 'Invalid deployment id' $message 'rejects a resource-group-scoped -Id'

    Section 'Get-AzDeployment missing deployment (must surface DeploymentNotFound)'
    $message = Get-ErrorText {
        Get-AzDeployment -SubscriptionId $subscriptionId -Name 'azclips-test-sub-does-not-exist'
    }
    Assert-Match 'DeploymentNotFound' $message 'missing deployment surfaces DeploymentNotFound'

    Write-Host ''
    Write-Host 'All subscription deployment get smoke checks completed.' -ForegroundColor Green
}
finally {
    Section 'Remove-AzDeployment cleanup'
    try {
        Remove-AzDeployment -SubscriptionId $subscriptionId -Name $deploymentName -Confirm:$false
    } catch {
        Write-Warning "Deployment cleanup failed: $($_.Exception.Message)"
    }
}
