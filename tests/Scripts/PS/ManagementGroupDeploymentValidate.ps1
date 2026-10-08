# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment validate - PowerShell (Test-AzManagementGroupDeployment)
# Equivalent of: Test-AzManagementGroupDeployment
#
# Note: Test-AzManagementGroupDeployment never throws on an ARM validation failure. On success it
# returns $null (output suppressed); on failure it returns an object whose .Error property holds
# the ARM error. Assertions below reflect that shape rather than try/catch.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID     - subscription used for the live test resources
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to validate against
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes the
#                        session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentValidate.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
}

$managementGroupId = $env:AZCLIPS_MANAGEMENT_GROUP_ID
if ([string]::IsNullOrWhiteSpace($managementGroupId)) {
    throw 'AZCLIPS_MANAGEMENT_GROUP_ID must be set.'
}

$tenantId = $env:AZCLIPS_TENANT_ID
if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
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

$templateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $templateFile -Encoding utf8

# Invalid template: the parameter's default value isn't one of its own allowed values, a
# static template-definition error ARM's validate rejects even with an empty resources array.
# (An undefined-parameter reference inside "outputs" was tried first, but with no resources ARM's
# management-group validate did not evaluate/report it as an error - this constraint violation is
# reliably caught instead.)
$invalidTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-invalid-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
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

Write-Host '== 1. happy path: validate succeeds and returns nothing (SuppressSuccessOutput) =='
$result = Test-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location WestUS -TemplateFile $templateFile
Assert-Equal 'successful validate returned $null' $true ($null -eq $result)

Write-Host '== 2. negative: invalid template (default value not in allowedValues) must report an error, not throw =='
# Reset before the call (instead of reusing case 1's $null) so a thrown/non-assigning call can't
# be mistaken for a clean "no error" result, and catch any exception so Test-* (which is
# documented to never throw) surfaces a clear failure instead of silently leaving $result stale.
$result = $null
try {
    $result = Test-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location WestUS -TemplateFile $invalidTemplateFile
} catch {
    Add-Failure "invalid template should not throw, but threw: $($_.Exception.Message)"
}
# ARM has been observed to report deployment-validation errors either at the top-level .Error or
# nested under .Properties.Error, depending on when the error is detected; check both.
$validationError = $result.Error
if ($null -eq $validationError) {
    $validationError = $result.Properties.Error
}
Assert-Equal 'invalid template returned a non-null result' $true ($null -ne $result)
Assert-Equal 'invalid template reported a populated .Error' $true ($null -ne $validationError)
if ($null -ne $validationError) {
    Write-Host "invalid template .Error: $($validationError.Code): $($validationError.Message)"
}

if ($failures.Count -gt 0) {
    throw "Test-AzManagementGroupDeployment smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Test-AzManagementGroupDeployment smoke test finished =='
