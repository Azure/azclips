# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Resource group deployment get/list smoke test for the Azclips PowerShell surface
# (Get-AzResourceGroupDeployment). Arranges one deployment under a STABLE name, so repeated runs
# overwrite the same record and the same storage account instead of accumulating new ones; nothing
# to clean up. Then reads it back by name, lists the group, and checks the 404 paths.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\DeploymentGet.ps1

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
$deploymentName = 'azclips-test-deployment-get'
$bicepDir = Join-Path $PSScriptRoot '..\Bicep'
if ($env:AZCLIPS_DEBUG -eq '1') { $DebugPreference = 'Continue' }

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

Section "New-AzResourceGroupDeployment $deploymentName (arrange, idempotent)"
$arranged = New-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name $deploymentName `
    -TemplateFile (Join-Path $bicepDir 'sample-storage.bicep') `
    -Parameter 'storageAccountName=azclipsdeploymentget' `
    -Mode Incremental
Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

Section 'Get-AzResourceGroupDeployment -Name'
$deployment = Get-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name $deploymentName
Assert-Equal $deploymentName $deployment.Name 'get: deployment name'
Assert-Equal 'Succeeded' $deployment.Properties.ProvisioningState 'get: provisioningState'

Section 'Get-AzResourceGroupDeployment -Id'
$byId = Get-AzResourceGroupDeployment -Id $deployment.Id
Assert-Equal $deploymentName $byId.Name 'get by id: deployment name'
Assert-Equal $deployment.Id $byId.Id 'get by id: same resource id'

Section 'Get-AzResourceGroupDeployment (list the resource group)'
# The cmdlet enumerates output, so this is a flat array of DeploymentExtended, not an envelope.
$deployments = @(Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup)
Assert-Equal $true ($deploymentName -in $deployments.Name) "list: contains '$deploymentName' among $($deployments.Count)"

Section 'Get-AzResourceGroupDeployment without -SubscriptionId (defaults to the active profile)'
$deployment = Get-AzResourceGroupDeployment -ResourceGroupName $resourceGroup -Name $deploymentName
Assert-Equal $deploymentName $deployment.Name 'default subscription'

Section 'Get-AzResourceGroupDeployment missing deployment (must surface 404)'
$message = Get-ErrorText {
    Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName $resourceGroup -Name 'azclips-test-deployment-does-not-exist'
}
Assert-Match 'DeploymentNotFound|not found|404' $message 'missing deployment surfaces 404'

Section 'Get-AzResourceGroupDeployment missing resource group (must surface 404)'
$message = Get-ErrorText {
    Get-AzResourceGroupDeployment -SubscriptionId $subscriptionId -ResourceGroupName 'azclips-test-group-does-not-exist'
}
Assert-Match 'ResourceGroupNotFound|not found|404' $message 'missing resource group surfaces 404'

Write-Host ''
Write-Host 'All deployment get smoke checks completed.' -ForegroundColor Green
