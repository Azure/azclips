# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment save template - PowerShell (Save-AzTenantDeploymentTemplate)
# Equivalent of: az deployment tenant export
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes
#                        the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentSave.ps1

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
@"
{
  "`$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
"@ | Set-Content -Path $templateFile -Encoding utf8
$saveDirectory = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-save-$([guid]::NewGuid())")

$DebugPreference = 'Continue'

$name = "azclips-tenant-save-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
try {
    New-AzTenantDeployment -Location eastus -Name $name -TemplateFile $templateFile | Out-Null

    # 1. Happy path: a directory -Path receives <name>.json holding the deployment's template.
    $saved = Save-AzTenantDeploymentTemplate -Name $name -Path $saveDirectory.FullName
    $saved | Out-Host
    $expectedPath = Join-Path $saveDirectory.FullName "$name.json"
    Assert-Equal 'saved path' $expectedPath $saved.Path
    Assert-Equal 'saved template contentVersion' '1.0.0.0' (Get-Content -Raw $expectedPath | ConvertFrom-Json).contentVersion

    # 2. Negative: saving again without -Force must not overwrite the existing file.
    try {
        $null = Save-AzTenantDeploymentTemplate -Name $name -Path $saveDirectory.FullName -ErrorAction Stop
        Add-Failure 'save over an existing file without -Force did not throw'
    } catch {
        Assert-Equal 'existing file error mentions -Force' $true ($_.Exception.Message -match '-Force')
    }

    # 3. -Force overwrites the existing file.
    Save-AzTenantDeploymentTemplate -Name $name -Path $saveDirectory.FullName -Force | Out-Host

    # 4. Negative: saving a deployment name that does not exist must be rejected.
    try {
        $null = Save-AzTenantDeploymentTemplate -Name azclips-tenant-save-does-not-exist -Path $saveDirectory.FullName -ErrorAction Stop
        Add-Failure 'save of a nonexistent deployment did not throw'
    } catch {
        Assert-Equal 'save of a nonexistent deployment error mentions NotFound' $true ($_.Exception.Message -match 'NotFound|not found')
    }
} finally {
    try {
        Remove-AzTenantDeployment -Name $name -Confirm:$false
    } finally {
        Remove-Item -LiteralPath $templateFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $saveDirectory.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    throw "Save-AzTenantDeploymentTemplate smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Save-AzTenantDeploymentTemplate smoke test finished =='