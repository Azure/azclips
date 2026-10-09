# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment create - PowerShell (New-AzManagementGroupDeployment)
# Equivalent of: New-AzManagementGroupDeployment
# Get/Remove/Stop are covered by ManagementGroupDeploymentShow.ps1 / Delete.ps1 / Stop.ps1.
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to
#   AZCLIPS_TEMPLATE_SPEC_ID    - existing template spec version resource ID (optional; skips the
#                                 template-spec case when unset)
# Unlike the resource-group scope (fixed azclips-test-group-1), no fixed test management group
# exists across environments, so the management group ID must come from the environment.
#
# Required:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to
#
# Optional:
#   AZCLIPS_TEMPLATE_SPEC_ID - existing template spec version resource ID (skips that case if unset)
#   AZCLIPS_TENANT_ID        - tenant to sign in with before running commands; if unset, assumes
#                               the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentCreate.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$managementGroupId = $env:AZCLIPS_MANAGEMENT_GROUP_ID
if ([string]::IsNullOrWhiteSpace($managementGroupId)) {
    throw 'AZCLIPS_MANAGEMENT_GROUP_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
    Connect-AzAccount -TenantId $tenantId | Out-Host
}

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string]$Message) {
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
}
function Assert-Equal([string]$Label, $Expected, $Actual) {
    if ($Actual -eq $Expected) {
        Write-Host "OK: ${Label} => '$Actual'"
    } else {
        Add-Failure "${Label} expected '$Expected' but got '$Actual'"
    }
}

# Management-group-scope templates have no public quickstart equivalent, so generate a minimal
# empty template inline.
$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $templateFile -Encoding utf8

# Surface the cmdlet debug stream without prompting. Passing -Debug to each call sets
# $DebugPreference = Inquire, which raises an interactive confirmation prompt and hangs an
# unattended run. Setting Continue once here shows the same debug output non-interactively.
$DebugPreference = 'Continue'

Write-Host '== 1. create: provisioning state =='
$name = "azclips-mg-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$created = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name $name -TemplateFile $templateFile
Assert-Equal 'create provisioning state' 'Succeeded' $created.ProvisioningState
Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false

Write-Host '== 2. -Tag (deployment tags round-trip on create) =='
$name = "azclips-mg-tags-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name $name -TemplateFile $templateFile -Tag @{ key1 = 'value1'; key2 = 'value2' }
Assert-Equal 'tags key1' 'value1' $result.Tags['key1']
Assert-Equal 'tags key2' 'value2' $result.Tags['key2']
Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false

Write-Host '== 3. -TemplateSpecId (optional; requires AZCLIPS_TEMPLATE_SPEC_ID) =='
$templateSpecId = $env:AZCLIPS_TEMPLATE_SPEC_ID
if ([string]::IsNullOrWhiteSpace($templateSpecId)) {
    Write-Host 'Skipping -TemplateSpecId case: AZCLIPS_TEMPLATE_SPEC_ID not set.'
} else {
    $name = "azclips-mg-templatespec-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
    $result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location 'West US 2' -Name $name -TemplateSpecId $templateSpecId
    Assert-Equal 'template-spec-id provisioning state' 'Succeeded' $result.ProvisioningState
    Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false
}

Write-Host '== 4. negative: invalid -ValidationLevel value (Foo) must be rejected =='
# Expected result: the command fails fast with a clear validation message listing the allowed
# values (Template, Provider, ProviderNoRbac) and does NOT create a deployment.
# Note: unlike Azure CLI's `deployment mg create`, New-AzManagementGroupDeployment has no -Mode
# parameter at all (confirmed against azure-powershell source and ExtensionIndex.cs); PS always
# deploys Incremental, so there is no -Mode case to test here.
try {
    New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name azclips-mg-validationlevel-negative -TemplateFile $templateFile -ValidationLevel Foo -ErrorAction Stop
    Add-Failure 'invalid -ValidationLevel Foo did not throw'
} catch {
    Assert-Equal 'invalid -ValidationLevel Foo error mentions ValidationLevel' $true ($_.Exception.Message -match 'ValidationLevel')
}

if ($failures.Count -gt 0) {
    throw "New-AzManagementGroupDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== New-AzManagementGroupDeployment smoke test finished =='
