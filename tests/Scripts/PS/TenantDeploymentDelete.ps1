# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment delete - PowerShell (Remove-AzTenantDeployment)
# Equivalent of: Remove-AzTenantDeployment
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentDelete.ps1

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

Write-Host '== 1. Remove-AzTenantDeployment -Name =='
$name = "azclips-tenant-delete-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null
Remove-AzTenantDeployment -Name $name -Confirm:$false
try {
    $null = Get-AzTenantDeployment -Name $name -ErrorAction Stop
    Add-Failure "get still finds '$name' after remove"
} catch {
    Assert-Equal 'get after remove error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
}

Write-Host '== 2. Remove-AzTenantDeployment -Id (resource-id addressing) =='
$name = "azclips-tenant-delete-id-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$created = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile
Remove-AzTenantDeployment -Id $created.Id -Confirm:$false
try {
    $null = Get-AzTenantDeployment -Name $name -ErrorAction Stop
    Add-Failure "get still finds '$name' after remove by id"
} catch {
    Assert-Equal 'get after remove by id error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
}

Write-Host '== 3. removing a deployment name that does not exist succeeds (ARM DELETE is idempotent) =='
# Expected result: no error. Remove-AzTenantDeployment maps directly to an ARM DELETE, which
# returns success whether or not the deployment currently exists.
$threw = $false
try {
    Remove-AzTenantDeployment -Name azclips-tenant-remove-does-not-exist -Confirm:$false -ErrorAction Stop
} catch {
    $threw = $true
    Write-Host "remove of a nonexistent deployment threw: $($_.Exception.Message)"
}
Assert-Equal 'remove of a nonexistent deployment completed without error' $false $threw

if ($failures.Count -gt 0) {
    throw "Remove-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Remove-AzTenantDeployment smoke test finished =='
