# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment stop - PowerShell (Stop-AzManagementGroupDeployment)
# Equivalent of: Stop-AzManagementGroupDeployment
#
# AzClips has no -AsJob/-NoWait on New-AzManagementGroupDeployment, so a deployment always
# finishes synchronously and can never be caught mid-flight; the only cases reachable here are
# the already-succeeded and nonexistent-name rejection guards.
#
# Test target:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentStop.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$managementGroupId = $env:AZCLIPS_MANAGEMENT_GROUP_ID
if ([string]::IsNullOrWhiteSpace($managementGroupId)) {
    $managementGroupId = 'azclips-test-mg'
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

$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $templateFile -Encoding utf8

$DebugPreference = 'Continue'

$name = "azclips-mg-stop-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
try {
    Write-Host '== 1. negative: Stop-AzManagementGroupDeployment on an already-succeeded deployment must be rejected =='
    New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location westeurope -Name $name -TemplateFile $templateFile | Out-Null
    $threw = $false
    try {
        Stop-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -ErrorAction Stop
    } catch {
        $threw = $true
        Write-Host "stop of an already-succeeded deployment failed as expected: $($_.Exception.Message)"
    }
    Assert-Equal 'stop of an already-succeeded deployment was rejected' $true $threw

    Write-Host '== 2. negative: Stop-AzManagementGroupDeployment on a deployment name that does not exist must be rejected =='
    $threw = $false
    try {
        Stop-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name azclips-mg-stop-does-not-exist -ErrorAction Stop
    } catch {
        $threw = $true
        Write-Host "stop of a nonexistent deployment failed as expected: $($_.Exception.Message)"
    }
    Assert-Equal 'stop of a nonexistent deployment was rejected' $true $threw
} finally {
    try {
        Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false
    } finally {
        Remove-Item -LiteralPath $templateFile -Force
    }
}

if ($failures.Count -gt 0) {
    throw "Stop-AzManagementGroupDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Stop-AzManagementGroupDeployment smoke test finished =='
