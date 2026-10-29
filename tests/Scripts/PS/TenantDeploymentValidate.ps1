# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment validate - PowerShell (Test-AzTenantDeployment)
# Equivalent of: Test-AzTenantDeployment
#
# Note: like legacy Test-AzTenantDeployment, on success this writes nothing (diagnostics go to the warning
# stream unless -SuppressDiagnostics); on an ARM validation failure it writes the ARM error object instead of throwing.
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes the
#                        session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentValidate.ps1

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

# Invalid template: the parameter's default value isn't one of its own allowed values, a static
# template-definition error ARM's validate rejects even with an empty resources array. (An
# undefined-parameter reference inside "outputs" was tried first on management-group scope, but
# with no resources ARM's validate did not evaluate/report it as an error there - this constraint
# violation is reliably caught instead, and is used here for the same reason.)
$invalidTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-invalid-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "mode": {
      "type": "string",
      "allowedValues": ["a", "b"],
      "defaultValue": "c"
    }
  },
  "resources": []
}
'@ | Set-Content -Path $invalidTemplateFile -Encoding utf8

$DebugPreference = 'Continue'

Write-Host '== 1. happy path: validate succeeds and writes nothing =='
$result = Test-AzTenantDeployment -Location WestUS -TemplateFile $templateFile
Assert-Equal 'valid template output count' 0 @($result).Count

Write-Host '== 2. negative: invalid template (default value not in allowedValues) writes the validation error object =='
$result = Test-AzTenantDeployment -Location WestUS -TemplateFile $invalidTemplateFile
Write-Host "invalid template error: $($result.Code) - $($result.Message)"
Assert-Equal 'invalid template writes one error' 1 @($result).Count
Assert-Equal 'invalid template error has a code' $true (-not [string]::IsNullOrEmpty($result.Code))
Remove-Item $templateFile, $invalidTemplateFile -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
    throw "Test-AzTenantDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Test-AzTenantDeployment smoke test finished =='
