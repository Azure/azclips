# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Tenant-scope deployment validation smoke tests.
#
# Covers ARM JSON, Bicep, template/parameter objects, parameter precedence,
# all validation levels, derived names, malformed Bicep, structured ARM
# validation diagnostics, and conflicting parameter sources.
#
# Prerequisites (run in a fresh pwsh):
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  Import-Module ./src/PS/Accounts/Accounts.psd1
#                Connect-AzAccount
#   3. Module:   Import-Module ./src/PS/Resources/Resources.psd1
#   4. Use an identity permitted to validate tenant-scope deployments.
#
# Usage: ./tests/Scripts/PS/TenantDeploymentValidate.ps1
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
    "azclips-tenant-validate-$stamp"
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

# Validate JSON with a parameters file, inline override, and Provider level.
$providerResult = Test-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-provider" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.parameters.json') `
    -Parameter 'marker=inline-wins' `
    -ValidationLevel Provider
if ($providerResult.Error) {
    throw "Provider validation failed: $($providerResult.Error.Message)"
}

# Validate Bicep with a bicepparam file, inline override, and Template level.
$templateResult = Test-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-template" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.bicepparam') `
    -Parameter 'marker=bicep-inline-wins' `
    -ValidationLevel Template
if ($templateResult.Error) {
    throw "Template validation failed: $($templateResult.Error.Message)"
}

# Validate template and parameter objects with ProviderNoRbac level.
$templateObject = Get-Content (Join-Path $bicepDir 'tenant-empty.json') -Raw |
    ConvertFrom-Json -AsHashtable
$objectResult = Test-AzTenantDeployment `
    -Location $Location `
    -TemplateObject $templateObject `
    -TemplateParameterObject @{ marker = @{ value = 'object-base' } } `
    -Parameter 'marker=object-inline-wins' `
    -ValidationLevel ProviderNoRbac
if ($objectResult.Error) {
    throw "ProviderNoRbac validation failed: $($objectResult.Error.Message)"
}

# Verify malformed Bicep is rejected.
Assert-ExpectedFailure 'malformed Bicep validation' 'Bicep compilation failed' {
    Test-AzTenantDeployment `
        -Location $Location `
        -Name "$prefix-malformed" `
        -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
        -ErrorAction Stop
}

# Verify ARM returns structured validation diagnostics.
$invalidResult = Test-AzTenantDeployment `
    -Location $Location `
    -Name "$prefix-invalid-template" `
    -TemplateFile (Join-Path $bicepDir 'tenant-invalid-template.json')
if (-not $invalidResult.Error) {
    throw 'Invalid tenant template did not return validation diagnostics.'
}
if ($invalidResult.Error.Message -notmatch 'undefinedTenantParameter|parameter') {
    throw "Unexpected validation diagnostic: $($invalidResult.Error.Message)"
}

# Verify a parameters URI cannot be combined with an inline override.
Assert-ExpectedFailure 'parameters URI plus inline override' 'Inline parameters cannot be combined with a parameters URI' {
    Test-AzTenantDeployment `
        -Location $Location `
        -Name "$prefix-parameter-conflict" `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
        -TemplateParameterUri https://example.com/parameters.json `
        -Parameter 'marker=conflict' `
        -ErrorAction Stop
}

Write-Host 'All tenant deployment validation smoke checks completed.' -ForegroundColor Green
