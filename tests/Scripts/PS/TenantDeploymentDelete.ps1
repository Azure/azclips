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

$createdNames = [System.Collections.Generic.List[string]]::new()
try {
    Write-Host '== 1. Remove-AzTenantDeployment -Name -PassThru returns $true =='
    $name = "azclips-tenant-delete-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
    $createdNames.Add($name)
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
    $createdNames.Add($name)
    $created = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile
    $removed = Remove-AzTenantDeployment -Id $created.Id -Confirm:$false
    Assert-Equal 'remove without -PassThru writes nothing' $true ($null -eq $removed)
    try {
        $null = Get-AzTenantDeployment -Name $name -ErrorAction Stop
        Add-Failure "get still finds '$name' after remove by id"
    } catch {
        Assert-Equal 'get after remove by id error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
    }

    Write-Host '== 3. New-AzTenantDeployment -AsJob, then Get-AzTenantDeployment | Remove-AzTenantDeployment -AsJob -PassThru (pipeline) =='
    $name = "azclips-tenant-delete-pipe-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
    $createdNames.Add($name)
    $newJob = New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile -AsJob
    $created = $newJob | Wait-Job | Receive-Job
    Assert-Equal 'new -AsJob job state' 'Completed' $newJob.State
    Assert-Equal 'new -AsJob output provisioning state' 'Succeeded' $created.ProvisioningState
    $removeJob = Get-AzTenantDeployment -Name $name | Remove-AzTenantDeployment -AsJob -PassThru -Confirm:$false
    $removed = $removeJob | Wait-Job | Receive-Job
    Assert-Equal 'remove -AsJob job state' 'Completed' $removeJob.State
    Assert-Equal 'remove -AsJob -PassThru output' $true $removed

    Write-Host '== 4. removing a deployment name that does not exist succeeds without output =='
    $result = Remove-AzTenantDeployment -Name azclips-tenant-remove-does-not-exist -Confirm:$false -ErrorAction Stop
    Assert-Equal 'remove of a nonexistent deployment returns no output' 0 @($result).Count
} finally {
    # Best-effort: delete is idempotent, so names already removed by the test are safe to remove again.
    foreach ($createdName in $createdNames) {
        try { Remove-AzTenantDeployment -Name $createdName -Confirm:$false } catch { Write-Warning "cleanup of '$createdName' failed: $($_.Exception.Message)" }
    }
    Remove-Item -LiteralPath $templateFile -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    throw "Remove-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Remove-AzTenantDeployment smoke test finished =='
