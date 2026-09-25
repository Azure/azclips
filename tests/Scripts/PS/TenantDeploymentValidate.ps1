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

$ErrorActionPreference = 'Stop'
$subscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
if ([string]::IsNullOrWhiteSpace($subscriptionId)) {
    throw 'Set AZCLIPS_SUBSCRIPTION_ID to the signed-in subscription.'
}
$location = 'eastus'
# This dedicated test group is reused and intentionally not deleted by individual scripts.
$resourceGroupName = 'azclips-tenant-test-rg'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')
$bicepDir = Join-Path $repoRoot 'tests' 'Scripts' 'Bicep'
$stamp = "$(Get-Date -Format 'yyMMddHHmmss')-$PID"
$prefix = if ($env:AZCLIPS_TENANT_DEPLOYMENT_NAME) {
    $env:AZCLIPS_TENANT_DEPLOYMENT_NAME
} else {
    "azclips-tenant-validate-$stamp"
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

# Validate JSON with a parameters file, inline override, and Provider level.
$providerResult = Test-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-provider" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.parameters.json') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName", 'marker=inline-wins' `
    -ValidationLevel Provider
if ($providerResult.Error) {
    throw "Provider validation failed: $($providerResult.Error.Message)"
}
$providerResult

# Validate Bicep with a bicepparam file, inline override, and Template level.
$templateResult = Test-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-template" `
    -TemplateFile (Join-Path $bicepDir 'tenant-empty.bicep') `
    -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.bicepparam') `
    -Parameter "subscriptionId=$subscriptionId", "resourceGroupName=$resourceGroupName", 'marker=bicep-inline-wins' `
    -ValidationLevel Template
if ($templateResult.Error) {
    throw "Template validation failed: $($templateResult.Error.Message)"
}
$templateResult

# Validate template and parameter objects with ProviderNoRbac level.
$templateObject = Get-Content (Join-Path $bicepDir 'tenant-empty.json') -Raw |
    ConvertFrom-Json -AsHashtable
$objectResult = Test-AzTenantDeployment `
    -Location $location `
    -TemplateObject $templateObject `
    -TemplateParameterObject @{
        subscriptionId = @{ value = $subscriptionId }
        resourceGroupName = @{ value = $resourceGroupName }
        marker = @{ value = 'object-base' }
    } `
    -Parameter 'marker=object-inline-wins' `
    -ValidationLevel ProviderNoRbac
if ($objectResult.Error) {
    throw "ProviderNoRbac validation failed: $($objectResult.Error.Message)"
}
$objectResult

# Verify malformed Bicep is rejected.
Assert-ExpectedFailure 'malformed Bicep validation' 'Bicep compilation failed' {
    Test-AzTenantDeployment `
        -Location $location `
        -Name "$prefix-malformed" `
        -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
        -ErrorAction Stop
}

# Verify ARM returns structured validation diagnostics.
$invalidResult = Test-AzTenantDeployment `
    -Location $location `
    -Name "$prefix-invalid-template" `
    -TemplateFile (Join-Path $bicepDir 'tenant-invalid-template.json') `
    -TemplateParameterObject @{
        subscriptionId = @{ value = $subscriptionId }
        resourceGroupName = @{ value = $resourceGroupName }
    }
if (-not $invalidResult.Error) {
    throw 'Invalid tenant template did not return validation diagnostics.'
}
if ($invalidResult.Error.Message -notmatch 'undefinedTenantParameter|parameter') {
    throw "Unexpected validation diagnostic: $($invalidResult.Error.Message)"
}
$invalidResult

# Verify a parameters URI cannot be combined with an inline override.
Assert-ExpectedFailure 'parameters URI plus inline override' 'Inline parameters cannot be combined with a parameters URI' {
    Test-AzTenantDeployment `
        -Location $location `
        -Name "$prefix-parameter-conflict" `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
        -TemplateParameterUri https://example.com/parameters.json `
        -Parameter 'marker=conflict' `
        -ErrorAction Stop
}

Write-Host 'All tenant deployment validation smoke checks completed.' -ForegroundColor Green
