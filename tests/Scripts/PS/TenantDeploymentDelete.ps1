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

Write-Host '== 1. Remove-AzTenantDeployment -Name -PassThru returns $true =='
$name = "azclips-tenant-delete-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null
$removed = Remove-AzTenantDeployment -Name $name -PassThru -Confirm:$false
Assert-Equal 'remove -PassThru output' $true $removed
try {
    $null = Get-AzTenantDeployment -Name $name -ErrorAction Stop
    Add-Failure "get still finds '$name' after remove"
} catch {
    Assert-Equal 'get after remove error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
}

Write-Host '== 2. Remove-AzTenantDeployment -Id (resource-id addressing) writes nothing without -PassThru =='
$name = "azclips-tenant-delete-id-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$created = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile
$removed = Remove-AzTenantDeployment -Id $created.Id -Confirm:$false
Assert-Equal 'remove without -PassThru writes nothing' $true ($null -eq $removed)
try {
    $null = Get-AzTenantDeployment -Name $name -ErrorAction Stop
    Add-Failure "get still finds '$name' after remove by id"
} catch {
    Assert-Equal 'get after remove by id error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
}

Write-Host '== 3. Get-AzTenantDeployment | Remove-AzTenantDeployment -AsJob (pipeline + job) =='
$name = "azclips-tenant-delete-job-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null
$job = Get-AzTenantDeployment -Name $name | Remove-AzTenantDeployment -AsJob -PassThru -Confirm:$false
$removed = $job | Wait-Job | Receive-Job
Assert-Equal 'remove job state' 'Completed' $job.State
Assert-Equal 'remove job -PassThru output' $true $removed

Write-Host '== 4. negative: removing a deployment that does not exist is rejected (legacy precheck) =='
$threw = $false
try {
    Remove-AzTenantDeployment -Name azclips-tenant-remove-does-not-exist -Confirm:$false -ErrorAction Stop
} catch {
    $threw = $true
    Assert-Equal 'remove of a missing deployment has the legacy message' $true ($_.Exception.Message -match "doesn't exist in the current tenant")
}
Assert-Equal 'remove of a nonexistent deployment was rejected' $true $threw

Remove-Item $templateFile -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
    throw "Remove-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Remove-AzTenantDeployment smoke test finished =='
