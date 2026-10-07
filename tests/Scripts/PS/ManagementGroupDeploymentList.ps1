# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment list - PowerShell (Get-AzManagementGroupDeployment)
# Equivalent of: Get-AzManagementGroupDeployment (list)
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to deploy to
#
# Optional:
#   AZCLIPS_TENANT_ID           - tenant to sign in with before running commands; if unset,
#                                 assumes the session already has azclips's own modules imported
#                                 and signed in
#   AZCLIPS_SUBSCRIPTION_ID     - subscription to sign in to (used only with AZCLIPS_TENANT_ID)
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentList.ps1

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
    $subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
    Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
}

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string]$Message) {
    Write-Host "FAIL: $Message"
    $failures.Add($Message)
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

Write-Host '== 1. list deployments at the management group and find the one just created =='
$name = "azclips-mg-list-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name $name -TemplateFile $templateFile | Out-Null

$list = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId
$match = $list | Where-Object { $_.Name -eq $name }
if ($null -ne $match) {
    Write-Host "OK: list contains '$name'"
} else {
    Add-Failure "list did not contain '$name'"
}

Write-Host '== 2. negative: list against a management group that does not exist must be rejected =='
try {
    $null = Get-AzManagementGroupDeployment -ManagementGroupId 'azclips-does-not-exist-mg' -ErrorAction Stop
    Add-Failure 'list against a nonexistent management group did not throw'
} catch {
    # ARM checks RBAC before existence, so a nonexistent management group surfaces as
    # 403 AuthorizationFailed rather than 404 NotFound.
    if ($_.Exception.Message -match 'NotFound|not found|AuthorizationFailed|Forbidden') {
        Write-Host "OK: list against a nonexistent management group failed as expected: $($_.Exception.Message)"
    } else {
        Add-Failure "list against a nonexistent management group failed, but not with an expected error: $($_.Exception.Message)"
    }
}

if ($failures.Count -gt 0) {
    throw "Get-AzManagementGroupDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Get-AzManagementGroupDeployment smoke test finished =='
