# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Tenant scope deployment what-if - PowerShell (New-AzTenantDeployment -WhatIf)
# Equivalent of: New-AzTenantDeployment -WhatIf
#
# What-if is READ-ONLY: it predicts the changes a template would make and never creates or
# modifies resources. AzClips does not expose a standalone Get-AzTenantDeploymentWhatIfResult
# cmdlet; what-if at tenant scope is only reachable via New-AzTenantDeployment -WhatIf.
#
# Unlike management-group scope, tenant deployments have no scope-id parameter at all (the
# tenant is implicit from the signed-in context), so there is no AZCLIPS_*_ID equivalent required
# here.
#
# Optional:
#   AZCLIPS_SUBSCRIPTION_ID - nested subscription targeted by the create-preview cases (skips
#                             those cases if unset)
#   AZCLIPS_TENANT_ID       - tenant to sign in with before running commands; if unset, assumes
#                             the session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\TenantDeploymentWhatIf.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID

$tenantId = $env:AZCLIPS_TENANT_ID
if (-not [string]::IsNullOrWhiteSpace($tenantId)) {
    if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
        Connect-AzAccount -TenantId $tenantId | Out-Host
    } else {
        Connect-AzAccount -TenantId $tenantId -SubscriptionId $subscriptionId | Out-Host
    }
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

$basicTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $basicTemplateFile -Encoding utf8

$DebugPreference = 'Continue'

Write-Host '== 1. happy path (empty template): preview with no predicted changes =='
$whatIfName = "azclips-tenant-whatif-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
$result = New-AzTenantDeployment -Location eastus -Name $whatIfName -TemplateFile $basicTemplateFile -WhatIf
Assert-Equal 'empty-template what-if status' 'Succeeded' $result.Status
Assert-Equal 'empty-template what-if change count' 0 $result.Properties.Changes.Count
try {
    $null = Get-AzTenantDeployment -Name $whatIfName -ErrorAction Stop
    Add-Failure "-WhatIf unexpectedly created a real deployment named '$whatIfName'"
} catch {
    Write-Host "confirmed -WhatIf did not create '$whatIfName': $($_.Exception.Message)"
}

if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    Write-Host 'Skipping nested-create what-if cases: AZCLIPS_SUBSCRIPTION_ID not set.'
} else {
    # Nested deployment that creates a new resource group under a target subscription, so the
    # what-if preview has a genuine "Create" change to report.
    $nestedTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-tenant-nested-template-$([guid]::NewGuid()).json"
    @'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "targetSubscriptionId": { "type": "string" },
    "rgName": { "type": "string" },
    "rgLocation": { "type": "string" }
  },
  "resources": [
    {
      "type": "Microsoft.Resources/deployments",
      "apiVersion": "2021-04-01",
      "name": "nestedRGDeployment",
      "location": "[parameters('rgLocation')]",
      "subscriptionId": "[parameters('targetSubscriptionId')]",
      "properties": {
        "mode": "Incremental",
        "template": {
          "$schema": "https://schema.management.azure.com/schemas/2019-08-01/subscriptionDeploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "resources": [
            {
              "type": "Microsoft.Resources/resourceGroups",
              "apiVersion": "2021-04-01",
              "name": "[parameters('rgName')]",
              "location": "[parameters('rgLocation')]"
            }
          ]
        }
      }
    }
  ]
}
'@ | Set-Content -Path $nestedTemplateFile -Encoding utf8

    function New-NestedParameterObject {
        @{
            targetSubscriptionId = $subscriptionId
            rgName               = "azclips-tenant-nested-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
            rgLocation           = 'WestUS'
        }
    }

    Write-Host '== 2. -WhatIfResultFormat ResourceIdOnly: predicted changes carry resource ids only =='
    $result = New-AzTenantDeployment -Location WestUS -Name azclips-tenant-whatif-idonly -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -WhatIfResultFormat ResourceIdOnly -WhatIf
    Assert-Equal 'resource-id-only status' 'Succeeded' $result.Status
    Assert-Equal 'resource-id-only reported at least one change' $true ($result.Properties.Changes.Count -gt 0)

    Write-Host '== 3. -WhatIfExcludeChangeType Create: excludes the predicted "Create" change, leaving none =='
    $result = New-AzTenantDeployment -Location WestUS -Name azclips-tenant-whatif-exclude -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -WhatIfExcludeChangeType Create -WhatIf
    Assert-Equal 'exclude-create status' 'Succeeded' $result.Status
    Assert-Equal 'exclude-create change count' 0 $result.Properties.Changes.Count

    Write-Host '== 4. -ValidationLevel ProviderNoRbac: previews without an RBAC permission check =='
    $result = New-AzTenantDeployment -Location WestUS -Name azclips-tenant-whatif-norbac -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -ValidationLevel ProviderNoRbac -WhatIf
    Assert-Equal 'no-rbac status' 'Succeeded' $result.Status
}

Write-Host '== 5. negative: invalid -WhatIfResultFormat value (Foo) must be rejected =='
try {
    New-AzTenantDeployment -Location eastus -Name azclips-tenant-whatif-rf-negative -TemplateFile $basicTemplateFile -WhatIfResultFormat Foo -WhatIf -ErrorAction Stop
    Add-Failure 'invalid -WhatIfResultFormat Foo did not throw'
} catch {
    Assert-Equal 'invalid -WhatIfResultFormat Foo error has the expected message' $true ($_.Exception.Message -match 'ResultFormat must be one of')
}

if ($failures.Count -gt 0) {
    throw "New-AzTenantDeployment -WhatIf smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== New-AzTenantDeployment -WhatIf smoke test finished =='
