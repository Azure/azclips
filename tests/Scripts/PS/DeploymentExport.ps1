# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Resource group deployment export smoke test for the Azclips PowerShell surface
# (Save-AzResourceGroupDeploymentTemplate). The export is READ-ONLY: it returns the template the
# deployment was created from. The arrange step reuses a STABLE deployment name and storage account,
# so repeated runs overwrite instead of accumulating; nothing to clean up in Azure. The only thing
# created locally is a temp directory, removed in the finally block.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. Bicep CLI on PATH (or az-bundled at ~/.azure/bin/bicep).
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\DeploymentExport.ps1

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
$deploymentName = 'azclips-test-deployment-export'
$storageAccountName = 'azclipsdeploymentexport'
$templateFile = Join-Path $PSScriptRoot '..\Bicep\sample-storage.bicep'
$outputDir = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-export-$([guid]::NewGuid())"
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

try {
    if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
        Section 'Connect-AzAccount'
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }

    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

    Section "New-AzResourceGroupDeployment $deploymentName (arrange, idempotent)"
    $arranged = New-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -TemplateFile $templateFile `
        -Parameter "storageAccountName=$storageAccountName" `
        -Mode Incremental
    Assert-Equal 'Succeeded' $arranged.Properties.ProvisioningState 'arrange: deployment created'

    Section 'Save-AzResourceGroupDeploymentTemplate -Path <directory>'
    $saved = Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Path $outputDir
    Assert-Equal (Join-Path $outputDir "$deploymentName.json") $saved.Path 'directory path: file named after the deployment'
    Assert-Equal $true (Test-Path $saved.Path) 'directory path: file written'

    # The saved file must be the template ITSELF, not the ARM { "template": ... } envelope.
    $content = Get-Content $saved.Path -Raw | ConvertFrom-Json
    Assert-Equal $null $content.template 'saved file is not the ARM envelope'
    Assert-Match 'deploymentTemplate\.json' $content.'$schema' 'saved file is an ARM template'

    Section 'Test-AzResourceGroupDeployment with the saved template (round-trip)'
    $replay = Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name "$deploymentName-replay" `
        -TemplateFile $saved.Path `
        -Parameter "storageAccountName=$storageAccountName"
    Assert-Equal 'Succeeded' $replay.Properties.ProvisioningState 'saved template validates'

    Section 'Save-AzResourceGroupDeploymentTemplate -Path <nested file without extension>'
    $nested = Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Path (Join-Path $outputDir 'nested\prod-baseline')
    Assert-Equal (Join-Path $outputDir 'nested\prod-baseline.json') $nested.Path 'file path: .json appended'
    Assert-Equal $true (Test-Path $nested.Path) 'file path: parent directories created'

    Section 'Save-AzResourceGroupDeploymentTemplate -ResourceGroup -DeploymentName (legacy aliases)'
    $aliasPath = Join-Path $outputDir 'via-aliases.json'
    Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $subscriptionId `
        -ResourceGroup $resourceGroup `
        -DeploymentName $deploymentName `
        -Path $aliasPath | Out-Host
    Assert-Equal $true (Test-Path $aliasPath) 'aliases: file written'

    Section 'Save-AzResourceGroupDeploymentTemplate without -SubscriptionId (defaults to the active profile)'
    $defaultPath = Join-Path $outputDir 'default-subscription.json'
    Save-AzResourceGroupDeploymentTemplate `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Path $defaultPath | Out-Host
    Assert-Equal $true (Test-Path $defaultPath) 'default subscription: file written'

    Section 'Save-AzResourceGroupDeploymentTemplate over an existing file without -Force (rejected)'
    $before = Get-Content $saved.Path -Raw
    $message = Get-ErrorText {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroup `
            -Name $deploymentName `
            -Path $outputDir
    }
    Assert-Match 'already exists.*-Force' $message 'overwrite without -Force is rejected'
    Assert-Equal $before (Get-Content $saved.Path -Raw) 'overwrite without -Force leaves the file untouched'

    Section 'Save-AzResourceGroupDeploymentTemplate -Force'
    $forced = Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Path $outputDir `
        -Force
    Assert-Equal $saved.Path $forced.Path '-Force overwrites the existing file'

    Section 'Save-AzResourceGroupDeploymentTemplate -WhatIf (writes nothing)'
    $whatIfPath = Join-Path $outputDir 'whatif.json'
    Save-AzResourceGroupDeploymentTemplate `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name $deploymentName `
        -Path $whatIfPath `
        -WhatIf
    Assert-Equal $false (Test-Path $whatIfPath) '-WhatIf writes no file'

    Section 'Save-AzResourceGroupDeploymentTemplate missing deployment (must surface 404)'
    $message = Get-ErrorText {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName $resourceGroup `
            -Name 'azclips-test-deployment-does-not-exist' `
            -Path $outputDir
    }
    Assert-Match 'DeploymentNotFound|not found|404' $message 'missing deployment surfaces 404'

    Section 'Save-AzResourceGroupDeploymentTemplate missing resource group (must surface 404)'
    $message = Get-ErrorText {
        Save-AzResourceGroupDeploymentTemplate `
            -SubscriptionId $subscriptionId `
            -ResourceGroupName 'azclips-test-group-does-not-exist' `
            -Name $deploymentName `
            -Path $outputDir
    }
    Assert-Match 'ResourceGroupNotFound|not found|404' $message 'missing resource group surfaces 404'

    Write-Host ''
    Write-Host 'All deployment export smoke checks completed.' -ForegroundColor Green
}
finally {
    Section 'Remove local export directory'
    try {
        Remove-Item -Path $outputDir -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Warning "Export directory cleanup failed: $($_.Exception.Message)"
    }
}
