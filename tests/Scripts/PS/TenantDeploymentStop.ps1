# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment stop - PowerShell (Stop-AzTenantDeployment)
# Equivalent of: Stop-AzTenantDeployment
#
# AzClips has no -AsJob/-NoWait on New-AzTenantDeployment, so a deployment always finishes
# synchronously and can never be caught mid-flight; the only cases reachable here are the
# already-succeeded and nonexistent-name rejection guards.
#
# Unlike management-group scope, StopTenantDeploymentProcessor_Orchestrator has no local
# provisioning-state precheck: it forwards directly to ARM's cancel operation. So the exact
# error text below is ARM's own and is not asserted; only that the call fails is checked.
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentStop.ps1

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

Write-Host '== 1. negative: Stop-AzTenantDeployment on an already-succeeded deployment must be rejected =='
$name = "azclips-tenant-stop-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null
$threw = $false
try {
    Stop-AzTenantDeployment -Name $name -ErrorAction Stop
} catch {
    $threw = $true
    Write-Host "stop of an already-succeeded deployment failed as expected: $($_.Exception.Message)"
}
Assert-Equal 'stop of an already-succeeded deployment was rejected' $true $threw
Remove-AzTenantDeployment -Name $name -Confirm:$false

Write-Host '== 2. negative: Stop-AzTenantDeployment on a deployment name that does not exist must be rejected =='
$threw = $false
try {
    Stop-AzTenantDeployment -Name azclips-tenant-stop-does-not-exist -ErrorAction Stop
} catch {
    $threw = $true
    Write-Host "stop of a nonexistent deployment failed as expected: $($_.Exception.Message)"
}
Assert-Equal 'stop of a nonexistent deployment was rejected' $true $threw

if ($failures.Count -gt 0) {
    throw "Stop-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Stop-AzTenantDeployment smoke test finished =='
