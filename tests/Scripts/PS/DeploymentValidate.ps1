# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Resource group deployment validate smoke test for the Azclips PowerShell surface
# (Test-AzResourceGroupDeployment). Validate is READ-ONLY: ARM only checks whether the template
# would be accepted, so nothing is created and there is nothing to clean up. Covers every template
# source (URI, Bicep, .bicepparam only, in-memory object), parameter overlay, validation levels,
# the 202 long-running validate, and the local and ARM-side rejection paths.
#
# Prereqs:
#   1. Build the solution: pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#   3. Bicep CLI on PATH (or az-bundled at ~/.azure/bin/bicep) for the .bicep cases.
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
#   pwsh -NoProfile -File .\tests\Scripts\PS\DeploymentValidate.ps1

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
$bicepDir = Join-Path $PSScriptRoot '..\Bicep'
$bicepFile = Join-Path $bicepDir 'sample-storage.bicep'
$bicepParamFile = Join-Path $bicepDir 'sample-storage.bicepparam'
$templateUri = 'https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/quickstarts/microsoft.storage/storage-account-create/azuredeploy.json'
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

# A successful validate emits a DeploymentValidateResult with no error and provisioningState Succeeded.
function Assert-Validated($Result, [string]$Case) {
    Assert-Equal $null $Result.Error "${Case}: no error"
    Assert-Equal 'Succeeded' $Result.Properties.ProvisioningState "${Case}: provisioningState"
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

Section 'Test-AzResourceGroupDeployment -TemplateUri'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-1 `
    -TemplateUri $templateUri `
    -Mode Incremental
Assert-Validated $result 'template uri'

Section 'Test-AzResourceGroupDeployment -TemplateFile .bicep -ParametersFile .bicepparam'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-bicep `
    -TemplateFile $bicepFile `
    -ParametersFile $bicepParamFile `
    -Mode Incremental
Assert-Validated $result 'bicep'

Section 'Test-AzResourceGroupDeployment inline -Parameter overrides the .bicepparam value'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-inline `
    -TemplateFile $bicepFile `
    -ParametersFile $bicepParamFile `
    -Parameter 'storageAccountName=azclipsvalidateinline' `
    -Mode Incremental
Assert-Validated $result 'inline parameter'

Section 'Test-AzResourceGroupDeployment -ValidationLevel Template'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-level `
    -TemplateFile $bicepFile `
    -Parameter 'storageAccountName=azclipsvalidatelevel' `
    -ValidationLevel Template `
    -Mode Incremental
Assert-Validated $result 'validation level Template'

Section 'Test-AzResourceGroupDeployment without -Name (GUID generated client-side)'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -TemplateFile $bicepFile `
    -Parameter 'storageAccountName=azclipsvalidatenoname' `
    -Mode Incremental
Assert-Validated $result 'no -Name'
Assert-Match '^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$' $result.Name 'no -Name: generated name is a GUID'

Section 'Test-AzResourceGroupDeployment -TemplateObject'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-template-object `
    -TemplateObject @{
        '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
        contentVersion = '1.0.0.0'
        resources      = @()
    } `
    -Mode Incremental
Assert-Validated $result 'template object'

Section 'Test-AzResourceGroupDeployment -ParametersFile .bicepparam only (using clause names the template)'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-bicepparam-only `
    -ParametersFile $bicepParamFile `
    -Mode Incremental
Assert-Validated $result 'bicepparam only'

Section 'Test-AzResourceGroupDeployment -RollbackOnError'
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-rollback `
    -TemplateFile $bicepFile `
    -Parameter 'storageAccountName=azclipsvalidaterollback' `
    -RollbackOnError `
    -Mode Incremental
Assert-Validated $result 'rollback on error'

Section 'Test-AzResourceGroupDeployment -ValidationLevel Provider (HTTP 202 long-running validate)'
# ARM answers the POST with 202 + Location and the processor polls it; the output must be the final
# validate result, not an empty value or an intermediate status document.
$result = Test-AzResourceGroupDeployment `
    -SubscriptionId $subscriptionId `
    -ResourceGroupName $resourceGroup `
    -Name azclips-test-validate-lro `
    -TemplateFile $bicepFile `
    -Parameter 'storageAccountName=azclipsvalidatelro' `
    -ValidationLevel Provider `
    -Mode Incremental
Assert-Validated $result 'long-running validate'

Section 'Test-AzResourceGroupDeployment malformed .bicep (rejected by Bicep compilation, no ARM call)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-bicep-negative `
        -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
        -Mode Incremental
}
Assert-Match 'Bicep compilation failed' $message 'malformed .bicep is rejected'

Section 'Test-AzResourceGroupDeployment -ValidationLevel Foo (passed through, rejected by ARM)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-vl-negative `
        -TemplateUri $templateUri `
        -ValidationLevel Foo `
        -Mode Incremental
}
Assert-Match 'ValidationLevel|InvalidRequestContent' $message 'unknown -ValidationLevel is rejected by ARM'

Section 'Test-AzResourceGroupDeployment -RollbackOnError with -RollbackToDeploymentName (mutually exclusive)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-rollback-negative `
        -TemplateFile $bicepFile `
        -RollbackOnError `
        -RollbackToDeploymentName azclips-test-validate-1 `
        -Mode Incremental
}
Assert-Match 'mutually exclusive' $message 'rollback options are mutually exclusive'

Section 'Test-AzResourceGroupDeployment invalid template (ARM HTTP 400 must surface as an error)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-apierror `
        -TemplateFile (Join-Path $bicepDir 'whatif-invalid-template.json') `
        -Mode Incremental
}
Assert-Match 'Deployment validation failed' $message 'HTTP 400 surfaces a readable validation error'
Assert-Match 'InvalidTemplate|undefinedNameParam' $message 'HTTP 400 error carries the ARM diagnostic'

Section 'Test-AzResourceGroupDeployment missing required template parameter (caught locally)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-missing-param `
        -TemplateFile $bicepFile `
        -Mode Incremental
}
Assert-Match 'storageAccountName' $message 'missing required parameter is named'

Section 'Test-AzResourceGroupDeployment -QueryString without -TemplateUri (rejected locally)'
$message = Get-ErrorText {
    Test-AzResourceGroupDeployment `
        -SubscriptionId $subscriptionId `
        -ResourceGroupName $resourceGroup `
        -Name azclips-test-validate-querystring-negative `
        -TemplateFile $bicepFile `
        -Parameter 'storageAccountName=azclipsvalidateqs' `
        -QueryString 'sv=2021&sig=ABC' `
        -Mode Incremental
}
Assert-Match 'TemplateUri' $message 'query string without template uri is rejected'

Write-Host ''
Write-Host 'All deployment validate smoke checks completed.' -ForegroundColor Green
