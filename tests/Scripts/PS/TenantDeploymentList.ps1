# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment list - PowerShell (Get-AzTenantDeployment_List)
# Equivalent of: Get-AzTenantDeployment (list)
#
# Optional:
#   AZCLIPS_TENANT_ID       - tenant to sign in with before running commands; if unset, assumes
#                              the session already has azclips's own modules imported and signed in
#   AZCLIPS_SUBSCRIPTION_ID - subscription to sign in to (used only with AZCLIPS_TENANT_ID)
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentList.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

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

Write-Host '== 1. list deployments at tenant scope and find the one just created =='
$name = "azclips-tenant-list-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null

$list = Get-AzTenantDeployment
$match = $list | Where-Object { $_.DeploymentName -eq $name }
Assert-Equal 'list contains the created deployment' $true ($null -ne $match)
Remove-AzTenantDeployment -Name $name -Confirm:$false
Remove-Item $templateFile -ErrorAction SilentlyContinue

# Note: unlike management-group scope, Get-AzTenantDeployment (list) has no scope-id parameter at
# all, so there is no "list against a scope that does not exist" negative case to exercise here.

if ($failures.Count -gt 0) {
    throw "Get-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Get-AzTenantDeployment smoke test finished =='
