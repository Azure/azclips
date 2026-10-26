# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment delete - PowerShell (Remove-AzManagementGroupDeployment)
# Equivalent of: Remove-AzManagementGroupDeployment
#
# Test target:
#   AZCLIPS_MANAGEMENT_GROUP_ID - optional override (default: azclips-test-mg)
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentDelete.ps1

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

$name = "azclips-mg-delete-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
try {
    Write-Host '== 1. Remove-AzManagementGroupDeployment -ManagementGroupId -Name =='
    New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location westeurope -Name $name -TemplateFile $templateFile | Out-Null
    Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false
    try {
        $null = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -ErrorAction Stop
        Add-Failure "get still finds '$name' after remove"
    } catch {
        Assert-Equal 'get after remove error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
    }

    Write-Host '== 2. Remove-AzManagementGroupDeployment -Id (resource-id addressing) =='
    $name = "azclips-mg-delete-id-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
    $created = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location westeurope -Name $name -TemplateFile $templateFile
    Remove-AzManagementGroupDeployment -Id $created.Id -Confirm:$false
    try {
        $null = Get-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -ErrorAction Stop
        Add-Failure "get still finds '$name' after remove by id"
    } catch {
        Assert-Equal 'get after remove by id error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
    }

    Write-Host '== 3. removing a deployment name that does not exist succeeds without output =='
    $result = Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name azclips-mg-remove-does-not-exist -Confirm:$false -ErrorAction Stop
    Assert-Equal 'remove of a nonexistent deployment returns no output' 0 @($result).Count
} finally {
    try {
        Remove-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Name $name -Confirm:$false
    } finally {
        Remove-Item -LiteralPath $templateFile -Force
    }
}

if ($failures.Count -gt 0) {
    throw "Remove-AzManagementGroupDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Remove-AzManagementGroupDeployment smoke test finished =='
