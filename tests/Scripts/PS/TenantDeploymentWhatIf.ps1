# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment what-if - PowerShell (Get-AzTenantDeploymentWhatIfResult)
# Equivalent of: Get-AzTenantDeploymentWhatIfResult
#
# What-if is READ-ONLY: it predicts the changes a template would make and never creates
# or modifies resources.
#
# Optional:
#   AZCLIPS_TENANT_ID       - tenant to sign in with before running commands; if unset, assumes the
#                             session already has azclips's own modules imported and signed in
#   AZCLIPS_LOCATION        - defaults to westus
#   AZCLIPS_SUBSCRIPTION_ID - nested subscription targeted by the create-preview case;
#                             defaults to the signed-in account's subscription
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentWhatIf.ps1

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
function Assert-Contains([string]$Label, [string]$Needle, [string]$Haystack) {
    if ($Haystack.Contains($Needle)) {
        Write-Host "OK: ${Label} contains '$Needle'"
    } else {
        Add-Failure "${Label} expected to contain '$Needle' but got '$Haystack'"
    }
}

$location = if ($env:AZCLIPS_LOCATION) { $env:AZCLIPS_LOCATION } else { 'westus' }
$subscriptionId = if ($env:AZCLIPS_SUBSCRIPTION_ID) { $env:AZCLIPS_SUBSCRIPTION_ID } else { (Get-AzContext).Subscription.Id }

$emptyTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-whatif-empty-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $emptyTemplateFile -Encoding utf8

# Nested deployment that creates a new resource group under a target subscription,
# so the what-if preview has a genuine "Create" change to report.
$createTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-whatif-create-$([guid]::NewGuid()).json"
$rgName = "tenant-whatif-rg-$(Get-Random)"
@"
{
  "`$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.Resources/deployments",
      "apiVersion": "2021-04-01",
      "name": "nestedRGDeployment",
      "location": "$location",
      "subscriptionId": "$subscriptionId",
      "properties": {
        "mode": "Incremental",
        "template": {
          "`$schema": "https://schema.management.azure.com/schemas/2019-08-01/subscriptionDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "resources": [
            {
              "type": "Microsoft.Resources/resourceGroups",
              "apiVersion": "2021-04-01",
              "name": "$rgName",
              "location": "$location"
            }
          ]
        }
      }
    }
  ]
}
"@ | Set-Content -Path $createTemplateFile -Encoding utf8

$DebugPreference = 'Continue'

# 1. Happy path (empty template): the preview reports no changes.
$report = Get-AzTenantDeploymentWhatIfResult -Name "tenant-dep-whatif-$(Get-Random)" -Location $location -TemplateFile $emptyTemplateFile
$report | Out-Host
Assert-Contains 'empty template change report' 'Resource changes: no change.' ($report -join "`n")

# 2. Nested create: what-if reports a Create change.
$report = Get-AzTenantDeploymentWhatIfResult -Name "tenant-dep-whatif-create-$(Get-Random)" -Location $location -TemplateFile $createTemplateFile
$report | Out-Host
Assert-Contains 'what-if report shows the Create change' 'to create' ($report -join "`n")

# 3. -ValidationLevel ProviderNoRbac previews without an RBAC permission check.
$report = Get-AzTenantDeploymentWhatIfResult -Name "tenant-dep-whatif-norbac-$(Get-Random)" -Location $location -TemplateFile $createTemplateFile -ValidationLevel ProviderNoRbac
$report | Out-Host
Assert-Contains 'ValidationLevel ProviderNoRbac change report' 'Resource changes:' ($report -join "`n")

# 4. An invalid -ResultFormat is rejected.
try {
    Get-AzTenantDeploymentWhatIfResult -Name "tenant-dep-whatif-badformat-$(Get-Random)" -Location $location -TemplateFile $emptyTemplateFile -ResultFormat Foo -ErrorAction Stop
    Add-Failure 'invalid -ResultFormat did not throw'
} catch {
    Write-Host "invalid ResultFormat error: $($_.Exception.Message)"
    Assert-Contains 'invalid ResultFormat error mentions the parameter' 'ResultFormat' $_.Exception.Message
}
Remove-Item $emptyTemplateFile, $createTemplateFile -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
    throw "Get-AzTenantDeploymentWhatIfResult smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== Get-AzTenantDeploymentWhatIfResult smoke test finished =='
