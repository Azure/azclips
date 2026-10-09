# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Management group scope deployment what-if - PowerShell (New-AzManagementGroupDeployment -WhatIf)
# Equivalent of: New-AzManagementGroupDeployment -WhatIf
#
# What-if is READ-ONLY: it predicts the changes a template would make and never creates or
# modifies resources.
#
# Shared test context:
#   AZCLIPS_MANAGEMENT_GROUP_ID - child management group ID to preview against
#   AZCLIPS_SUBSCRIPTION_ID     - nested subscription targeted by the create-preview cases
# AzClips does not expose a standalone Get-AzManagementGroupDeploymentWhatIfResult cmdlet; what-if
# at management-group scope is only reachable via New-AzManagementGroupDeployment -WhatIf.
#
# Optional:
#   AZCLIPS_TENANT_ID - tenant to sign in with before running commands; if unset, assumes the
#                        session already has azclips's own modules imported and signed in
#
# Usage:
#   pwsh -NoProfile -File .\tests\Scripts\PS\ManagementGroupDeploymentWhatIf.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Resources\Resources.psd1') -Force

$managementGroupId = $env:AZCLIPS_MANAGEMENT_GROUP_ID
if ([string]::IsNullOrWhiteSpace($managementGroupId)) {
    throw 'AZCLIPS_MANAGEMENT_GROUP_ID must be set.'
}

$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'AZCLIPS_SUBSCRIPTION_ID must be set.'
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

$basicTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
'@ | Set-Content -Path $basicTemplateFile -Encoding utf8

# Nested resource-group-create template so the what-if preview reports real "Create" changes.
$nestedTemplateFile = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-mg-nested-template-$([guid]::NewGuid()).json"
@'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
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

$DebugPreference = 'Continue'

function New-NestedParameterObject {
    @{
        targetSubscriptionId = $subscriptionId
        rgName               = "azclips-mg-nested-$((Get-Date).ToUniversalTime().ToString('yyMMddHHmmss'))"
        rgLocation           = 'WestUS'
    }
}

Write-Host '== 1. happy path (empty template): preview with no predicted changes =='
$result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name azclips-mg-whatif -TemplateFile $basicTemplateFile -WhatIf
Assert-Equal 'empty-template status' 'Succeeded' $result.Status
Assert-Equal 'empty-template change count' 0 $result.Properties.Changes.Count

Write-Host '== 2. -WhatIfResultFormat ResourceIdOnly: predicted changes carry resource ids only =='
$result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location WestUS -Name azclips-mg-whatif-idonly -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -WhatIfResultFormat ResourceIdOnly -WhatIf
Assert-Equal 'resource-id-only status' 'Succeeded' $result.Status
Assert-Equal 'resource-id-only reported at least one change' $true ($result.Properties.Changes.Count -gt 0)

Write-Host '== 3. -WhatIfExcludeChangeType Create: excludes the predicted "Create" change, leaving none =='
$result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location WestUS -Name azclips-mg-whatif-exclude -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -WhatIfExcludeChangeType Create -WhatIf
Assert-Equal 'exclude-create status' 'Succeeded' $result.Status
Assert-Equal 'exclude-create change count' 0 $result.Properties.Changes.Count

Write-Host '== 4. -ValidationLevel ProviderNoRbac: previews without an RBAC permission check =='
$result = New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location WestUS -Name azclips-mg-whatif-norbac -TemplateFile $nestedTemplateFile -TemplateParameterObject (New-NestedParameterObject) -ValidationLevel ProviderNoRbac -WhatIf
Assert-Equal 'no-rbac status' 'Succeeded' $result.Status

Write-Host '== 5. negative: invalid -WhatIfResultFormat value (Foo) must be rejected =='
try {
    New-AzManagementGroupDeployment -ManagementGroupId $managementGroupId -Location eastus -Name azclips-mg-whatif-rf-negative -TemplateFile $basicTemplateFile -WhatIfResultFormat Foo -WhatIf -ErrorAction Stop
    Add-Failure 'invalid -WhatIfResultFormat Foo did not throw'
} catch {
    Assert-Equal 'invalid -WhatIfResultFormat Foo error has the expected message' $true ($_.Exception.Message -match 'WhatIfResultFormat')
}

if ($failures.Count -gt 0) {
    throw "New-AzManagementGroupDeployment -WhatIf smoke test finished with $($failures.Count) failure(s)."
}
Write-Host '== New-AzManagementGroupDeployment -WhatIf smoke test finished =='
