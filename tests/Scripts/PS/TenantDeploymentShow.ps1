# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment show - PowerShell (Get-AzTenantDeployment_Get)
# Equivalent of: Get-AzTenantDeployment
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentShow.ps1

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

$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $templateFile -Encoding utf8

$DebugPreference = 'Continue'

$name = "azclips-tenant-show-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null

Write-Host '== 1. Get-AzTenantDeployment -Name =='
$shown = Get-AzTenantDeployment -Name $name
Assert-Equal 'get name' $name $shown.Name
Assert-Equal 'get provisioning state' 'Succeeded' $shown.Properties.ProvisioningState

Write-Host '== 2. Get-AzTenantDeployment -Id (resource-id addressing) =='
$shownById = Get-AzTenantDeployment -Id $shown.Id
Assert-Equal 'get by id name' $name $shownById.Name

Write-Host '== 3. negative: show a deployment name that does not exist must be rejected =='
try {
    $null = Get-AzTenantDeployment -Name azclips-tenant-show-does-not-exist -ErrorAction Stop
    Add-Failure 'get of a nonexistent deployment did not throw'
} catch {
    Assert-Equal 'get of a nonexistent deployment error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
}

Remove-AzTenantDeployment -Name $name -Confirm:$false

if ($failures.Count -gt 0) {
    throw "Get-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Get-AzTenantDeployment smoke test finished =='
