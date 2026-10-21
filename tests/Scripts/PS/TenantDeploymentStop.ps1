# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment stop - PowerShell (Stop-AzTenantDeployment)
# Equivalent of: Stop-AzTenantDeployment
#
# A deployment always finishes in seconds for an empty template, so a mid-flight cancel is too
# racy to assert. Stop sends the cancel request directly and Azure Resource Manager checks the
# deployment state, so the cases below only assert that cancelling a finished or missing
# deployment fails; the error text is ARM's own.
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

$name = "azclips-tenant-stop-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
try {
    Write-Host '== 1. negative: Stop-AzTenantDeployment on an already-succeeded deployment must be rejected =='
    New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null
    $threw = $false
    try {
        Stop-AzTenantDeployment -Name $name -ErrorAction Stop
    } catch {
        $threw = $true
        Write-Host "stop of an already-succeeded deployment failed as expected: $($_.Exception.Message)"
    }
    Assert-Equal 'stop of an already-succeeded deployment was rejected' $true $threw

    Write-Host '== 2. negative: Stop-AzTenantDeployment on a deployment name that does not exist must be rejected =='
    $threw = $false
    try {
        Stop-AzTenantDeployment -Name azclips-tenant-stop-does-not-exist -ErrorAction Stop
    } catch {
        $threw = $true
        Write-Host "stop of a nonexistent deployment failed as expected: $($_.Exception.Message)"
    }
    Assert-Equal 'stop of a nonexistent deployment was rejected' $true $threw
} finally {
    try {
        Remove-AzTenantDeployment -Name $name -Confirm:$false
    } finally {
        Remove-Item -LiteralPath $templateFile -Force -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    throw "Stop-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Stop-AzTenantDeployment smoke test finished =='
