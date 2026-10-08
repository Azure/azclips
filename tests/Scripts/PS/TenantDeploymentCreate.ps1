# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment create - PowerShell (New-AzTenantDeployment)
# Equivalent of: New-AzTenantDeployment
# Get/Remove/Stop are covered by TenantDeploymentShow.ps1 / Delete.ps1 / Stop.ps1.
# What-if is covered separately by TenantDeploymentWhatIf.ps1.
#
# Unlike management-group scope, tenant deployments have no scope-id parameter at all (the
# tenant is implicit from the signed-in context), so there is no AZCLIPS_*_ID equivalent required
# here.
#
# Optional:
#   AZCLIPS_TEMPLATE_SPEC_ID - existing template spec version resource ID (skips that case if unset)
#   AZCLIPS_TENANT_ID        - tenant to sign in with before running commands; if unset, assumes
#                               the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentCreate.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

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

# Tenant-scope templates have no public quickstart equivalent, so generate a minimal empty
# template inline.
$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
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
$name = "azclips-tenant-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$created = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile
Assert-Equal 'create provisioning state' 'Succeeded' $created.Properties.ProvisioningState
Remove-AzTenantDeployment -Name $name -Confirm:$false

Write-Host '== 2. -Tag (deployment tags round-trip on create) =='
$name = "azclips-tenant-tags-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$result = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile -Tag @{ key1 = 'value1'; key2 = 'value2' }
Assert-Equal 'tags key1' 'value1' $result.Tags['key1']
Assert-Equal 'tags key2' 'value2' $result.Tags['key2']

Write-Host '== 3. -TemplateSpecId (optional; requires AZCLIPS_TEMPLATE_SPEC_ID) =='
$templateSpecId = $env:AZCLIPS_TEMPLATE_SPEC_ID
if ([string]::IsNullOrWhiteSpace($templateSpecId)) {
    Write-Host 'Skipping -TemplateSpecId case: AZCLIPS_TEMPLATE_SPEC_ID not set.'
} else {
    $name = "azclips-tenant-templatespec-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
    $result = New-AzTenantDeployment -Location 'West US 2' -Name $name -TemplateSpecId $templateSpecId
    Assert-Equal 'template-spec-id provisioning state' 'Succeeded' $result.Properties.ProvisioningState
}

Write-Host '== 4. negative: invalid -ValidationLevel value (Foo) must be rejected =='
# Expected result: the command fails fast with a clear validation message listing the allowed
# values (Template, Provider, ProviderNoRbac) and does NOT create a deployment.
# Note: New-AzTenantDeployment has no -Mode parameter at all (confirmed against
# TenantDeploymentOrchestratorBase and ExtensionIndex.cs); tenant deployments always run
# Incremental, so there is no -Mode case to test here (CLI tenant create has no --mode either).
try {
    New-AzTenantDeployment -Location eastus -Name azclips-tenant-validationlevel-negative -TemplateFile $templateFile -ValidationLevel Foo -ErrorAction Stop
    Add-Failure 'invalid -ValidationLevel Foo did not throw'
} catch {
    Assert-Equal 'invalid -ValidationLevel Foo error mentions ValidationLevel' $true ($_.Exception.Message -match 'ValidationLevel')
}

if ($failures.Count -gt 0) {
    throw "New-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== New-AzTenantDeployment smoke test finished =='
