# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Tenant-scope deployment what-if smoke tests.
#
# Covers ARM JSON, Bicep, template/parameter objects, parameter precedence,
# result formats, excluded change types, malformed Bicep, invalid result format,
# and live ARM template diagnostics.
#
# Prerequisites (run in a fresh pwsh):
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  Import-Module ./src/PS/Accounts/Accounts.psd1
#                Connect-AzAccount
#   3. Module:   Import-Module ./src/PS/Resources/Resources.psd1
#   4. Use an identity permitted to run tenant-scope deployment what-if.
#
# Usage: ./tests/Scripts/PS/TenantDeploymentWhatIf.ps1

$ErrorActionPreference = 'Stop'
$subscriptionId = 'f758ac53-3e63-4317-a956-0997793808d7'
$location = 'eastus'
# This dedicated test group is reused and intentionally not deleted by individual scripts.
$resourceGroupName = 'azclips-tenant-test-rg'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')
$bicepDir = Join-Path $repoRoot 'tests' 'Scripts' 'Bicep'
$stamp = "$(Get-Date -Format 'yyMMddHHmmss')-$PID"
$prefix = if ($env:AZCLIPS_TENANT_DEPLOYMENT_NAME) {
    $env:AZCLIPS_TENANT_DEPLOYMENT_NAME
} else {
    "azclips-tenant-whatif-$stamp"
}

try {
    Get-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -ErrorAction Stop | Out-Null
} catch {
    if ($_.Exception.Message -notmatch '404|ResourceGroupNotFound|could not be found') {
        throw
    }
    New-AzResourceGroup -SubscriptionId $subscriptionId -Name $resourceGroupName -Location $location
}

function Assert-ExpectedFailure {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][scriptblock] $Action
    )

    try {
        & $Action
    } catch {
        if ($_.Exception.Message -match $Pattern) {
            Write-Warning $_.Exception.Message
            return
        }
        throw
    }

    throw "$Name unexpectedly succeeded."
}

# Run what-if for JSON using the default result format.
New-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-json" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName", 'marker=json' `
    -WhatIf | Out-Host

# Run what-if for Bicep with a bicepparam file and inline override.
New-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-bicep" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.bicepparam') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName", 'marker=bicep-inline-wins' `
    -WhatIf | Out-Host

# Run what-if for object inputs with ResourceIdOnly and excluded change types.
$templateObject = Get-Content (Join-Path $bicepDir 'tenant-empty.json') -Raw |
    ConvertFrom-Json -AsHashtable
New-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-id-only" `
    -TemplateObject $templateObject `
    -TemplateParameterObject @{
        subscriptionId = @{ value = $subscriptionId }
        resourceGroupName = @{ value = $resourceGroupName }
        marker = @{ value = 'object-base' }
    } `
    -Parameter 'marker=object-inline-wins' `
    -WhatIfResultFormat ResourceIdOnly `
    -WhatIfExcludeChangeType NoChange, Ignore `
    -WhatIf | Out-Host

# Verify malformed Bicep is rejected.
Assert-ExpectedFailure 'malformed Bicep what-if' 'Bicep compilation failed' {
    New-AzTenantDeployment `
        -Location $location `
        -Name "$prefix-malformed" `
        -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
        -WhatIf `
        -ErrorAction Stop
}

# Verify an invalid result format is rejected.
Assert-ExpectedFailure 'invalid result format' 'ResultFormat must be one of|allowed' {
    New-AzTenantDeployment `
        -Location $location `
        -Name "$prefix-invalid-format" `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
        -WhatIfResultFormat Invalid `
        -WhatIf `
        -ErrorAction Stop
}

# Verify live ARM what-if diagnostics are surfaced.
Assert-ExpectedFailure 'invalid tenant template what-if' 'InvalidTemplate|undefinedTenantParameter|what-if' {
    New-AzTenantDeployment `
        -Location $location `
        -Name "$prefix-invalid-template" `
        -TemplateFile (Join-Path $bicepDir 'tenant-invalid-template.json') `
        -TemplateParameterObject @{
            subscriptionId = @{ value = $subscriptionId }
            resourceGroupName = @{ value = $resourceGroupName }
        } `
        -WhatIf `
        -ErrorAction Stop
}

Write-Host 'All tenant deployment what-if smoke checks completed.' -ForegroundColor Green
