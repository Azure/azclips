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
[CmdletBinding()]
param(
    [string] $Location = $(if ($env:AZCLIPS_TENANT_DEPLOYMENT_LOCATION) {
        $env:AZCLIPS_TENANT_DEPLOYMENT_LOCATION
    } else {
        'eastus'
    })
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')
$bicepDir = Join-Path $repoRoot 'tests' 'Scripts' 'Bicep'
$stamp = "$(Get-Date -Format 'yyMMddHHmmss')-$PID"
$prefix = if ($env:AZCLIPS_TENANT_DEPLOYMENT_NAME) {
    $env:AZCLIPS_TENANT_DEPLOYMENT_NAME
} else {
    "azclips-tenant-whatif-$stamp"
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
            Write-Host "EXPECTED FAILURE ($Name): $($_.Exception.Message)"
            return
        }
        throw
    }

    throw "$Name unexpectedly succeeded."
}

Write-Host '=== [WhatIf] JSON default result format ==='
New-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-json" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
    -Parameter 'marker=json' `
    -WhatIf | Out-Host

Write-Host '=== [WhatIf] Bicep + bicepparam + inline override ==='
New-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-bicep" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.bicepparam') `
    -Parameter 'marker=bicep-inline-wins' `
    -WhatIf | Out-Host

Write-Host '=== [WhatIf] template/parameter objects, ResourceIdOnly, excluded types ==='
$templateObject = Get-Content (Join-Path $bicepDir 'tenant-empty.json') -Raw |
    ConvertFrom-Json -AsHashtable
New-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-id-only" `
    -TemplateObject $templateObject `
    -TemplateParameterObject @{ marker = @{ value = 'object-base' } } `
    -Parameter 'marker=object-inline-wins' `
    -WhatIfResultFormat ResourceIdOnly `
    -WhatIfExcludeChangeType NoChange, Ignore `
    -WhatIf | Out-Host

Write-Host '=== [negative] malformed Bicep ==='
Assert-ExpectedFailure 'malformed Bicep what-if' 'Bicep compilation failed' {
    New-AzTenantDeployment `
        -Location $Location `
        -Name "$prefix-malformed" `
        -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
        -WhatIf `
        -ErrorAction Stop
}

Write-Host '=== [negative] invalid result format ==='
Assert-ExpectedFailure 'invalid result format' 'ResultFormat must be one of|allowed' {
    New-AzTenantDeployment `
        -Location $Location `
        -Name "$prefix-invalid-format" `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
        -WhatIfResultFormat Invalid `
        -WhatIf `
        -ErrorAction Stop
}

Write-Host '=== [negative] live ARM what-if diagnostic ==='
Assert-ExpectedFailure 'invalid tenant template what-if' 'InvalidTemplate|undefinedTenantParameter|what-if' {
    New-AzTenantDeployment `
        -Location $Location `
        -Name "$prefix-invalid-template" `
        -TemplateFile (Join-Path $bicepDir 'tenant-invalid-template.json') `
        -WhatIf `
        -ErrorAction Stop
}

Write-Host 'All tenant deployment what-if smoke checks completed.' -ForegroundColor Green
