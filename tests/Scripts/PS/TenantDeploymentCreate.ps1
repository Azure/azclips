# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment create - PowerShell (New-AzTenantDeployment)
# Equivalent of: New-AzTenantDeployment
# Created deployments are removed in the finally block.
# Get/Remove/Stop are covered by TenantDeploymentShow.ps1 / Delete.ps1 / Stop.ps1.
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

$created = [System.Collections.Generic.List[string]]::new()
function New-Name([string]$Prefix) {
    $n = "$Prefix-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))-$(Get-Random -Maximum 1000)"
    $created.Add($n)
    $n
}

$paramTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-param-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": { "greeting": { "type": "string" } },
  "resources": [],
  "outputs": { "echo": { "type": "string", "value": "[parameters('greeting')]" } }
}
'@ | Set-Content -Path $paramTemplateFile -Encoding utf8

try {
    Write-Host '== 1. create: returns DeploymentExtended =='
    $name = New-Name 'azclips-tenant'
    $result = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile
    $result | Out-Host
    Assert-Equal 'create output type' 'DeploymentExtended' $result.GetType().Name
    Assert-Equal 'create deployment name' $name $result.Name
    Assert-Equal 'create provisioning state' 'Succeeded' $result.ProvisioningState

    Write-Host '== 2. -Parameter Name=Value flows to the outputs =='
    $result = New-AzTenantDeployment -Location eastus -Name (New-Name 'azclips-tenant-param') -TemplateFile $paramTemplateFile -Parameter 'greeting=hello-param'
    $result | Out-Host
    Assert-Equal 'parameter output' 'hello-param' $result.Outputs.GetProperty('echo').GetProperty('value').GetString()

    Write-Host '== 3. -TemplateSpecId (optional; requires AZCLIPS_TEMPLATE_SPEC_ID) =='
    $templateSpecId = $env:AZCLIPS_TEMPLATE_SPEC_ID
    if ([string]::IsNullOrWhiteSpace($templateSpecId)) {
        Write-Host 'Skipping -TemplateSpecId case: AZCLIPS_TEMPLATE_SPEC_ID not set.'
    } else {
        $result = New-AzTenantDeployment -Location 'West US 2' -Name (New-Name 'azclips-tenant-templatespec') -TemplateSpecId $templateSpecId
        $result | Out-Host
        Assert-Equal 'template-spec-id provisioning state' 'Succeeded' $result.ProvisioningState
    }

    Write-Host '== 4. negative: invalid -ValidationLevel value (Foo) must be rejected =='
    # The value is rejected before any request is sent, so no deployment is created.
    try {
        New-AzTenantDeployment -Location eastus -Name azclips-tenant-validationlevel-negative -TemplateFile $templateFile -ValidationLevel Foo -ErrorAction Stop
        Add-Failure 'invalid -ValidationLevel Foo did not throw'
    } catch {
        Assert-Equal 'invalid -ValidationLevel Foo error mentions ValidationLevel' $true ($_.Exception.Message -match 'ValidationLevel')
    }
} finally {
    foreach ($n in $created) {
        try { Remove-AzTenantDeployment -Name $n -Confirm:$false -ErrorAction Stop } catch { Write-Host "WARN: could not delete '$n': $($_.Exception.Message)" }
    }
    Remove-Item $templateFile, $paramTemplateFile -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "New-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== New-AzTenantDeployment smoke test finished =='
