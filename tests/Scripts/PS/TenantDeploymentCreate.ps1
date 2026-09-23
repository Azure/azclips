# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Tenant-scope deployment create/lifecycle smoke tests.
#
# Covers ARM JSON, Bicep, parameter file/object overlays, derived names, tags,
# debug settings, validation levels, Get by name/id, Stop terminal-state checks,
# Remove by name/id/input object, PassThru, and local input failures.
#
# Prerequisites (run in a fresh pwsh so installed gallery modules cannot shadow
# the repository modules):
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  Import-Module ./src/PS/Accounts/Accounts.psd1
#                Connect-AzAccount
#   3. Module:   Import-Module ./src/PS/Resources/Resources.psd1
#   4. Use an identity permitted to create tenant-scope deployments.
#
# Usage: ./tests/Scripts/PS/TenantDeploymentCreate.ps1
[CmdletBinding()]
param(
    [string] $Location = $(if ($env:AZCLIPS_TENANT_DEPLOYMENT_LOCATION) {
        $env:AZCLIPS_TENANT_DEPLOYMENT_LOCATION
    } else {
        'eastus'
    })
)

$ErrorActionPreference = 'Stop'
$DebugPreference = 'Continue'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')
$bicepDir = Join-Path $repoRoot 'tests' 'Scripts' 'Bicep'
$stamp = "$(Get-Date -Format 'yyMMddHHmmss')-$PID"
$prefix = if ($env:AZCLIPS_TENANT_DEPLOYMENT_NAME) {
    $env:AZCLIPS_TENANT_DEPLOYMENT_NAME
} else {
    "azclips-tenant-$stamp"
}
$derivedTemplate = Join-Path ([System.IO.Path]::GetTempPath()) "$prefix-derived.json"
$deployments = [System.Collections.Generic.List[string]]::new()

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

function Remove-TestDeployment {
    param([string] $Name)
    try {
        Remove-AzTenantDeployment `
            -Name $Name `
            -Confirm:$false `
            -ErrorAction Stop | Out-Null
    } catch {
        Write-Warning "Tenant deployment cleanup failed for '$Name': $($_.Exception.Message)"
    }
}

try {
    $jsonName = "$prefix-json"
    $deployments.Add($jsonName)
    # Create from JSON with a parameters file and an inline override.
    $jsonDeployment = New-AzTenantDeployment `
        -Location $Location `
        -Name $jsonName `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
        -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.parameters.json') `
        -Parameter 'marker=inline-wins' `
        -ValidationLevel Provider `
        -Tag @{ scenario = 'tenant-deployment'; source = 'json' } `
        -DeploymentDebugLogLevel None `
        -WhatIf:$false `
        -Confirm:$false

    if ($jsonDeployment.Properties.ProvisioningState -ne 'Succeeded') {
        throw "JSON deployment did not succeed: $($jsonDeployment.Properties.ProvisioningState)"
    }

    # Get the deployment by name and ID, then list tenant deployments.
    Get-AzTenantDeployment -Name $jsonName | Out-Host
    Get-AzTenantDeployment -Id $jsonDeployment.Id | Out-Host
    Get-AzTenantDeployment | Out-Host

    # Verify terminal deployments cannot be stopped by name, ID, or input object.
    Assert-ExpectedFailure 'Stop by name after completion' 'not running|cannot be canceled' {
        Stop-AzTenantDeployment -Name $jsonName -Confirm:$false -ErrorAction Stop
    }
    Assert-ExpectedFailure 'Stop by id after completion' 'not running|cannot be canceled' {
        Stop-AzTenantDeployment -Id $jsonDeployment.Id -Confirm:$false -ErrorAction Stop
    }
    Assert-ExpectedFailure 'Stop by input object after completion' 'not running|cannot be canceled' {
        $jsonDeployment | Stop-AzTenantDeployment -Confirm:$false -ErrorAction Stop
    }

    $bicepName = "$prefix-bicep"
    $deployments.Add($bicepName)
    # Create from Bicep with a bicepparam file and an inline override.
    $bicepDeployment = New-AzTenantDeployment `
        -Location $Location `
        -Name $bicepName `
        -TemplateFile (Join-Path $bicepDir 'tenant-empty.bicep') `
        -TemplateParameterFile (Join-Path $bicepDir 'tenant-empty.bicepparam') `
        -Parameter 'marker=bicep-inline-wins' `
        -ValidationLevel Template `
        -Confirm:$false

    $objectName = "$prefix-object"
    $deployments.Add($objectName)
    # Create from template and parameter objects.
    $templateObject = Get-Content (Join-Path $bicepDir 'tenant-empty.json') -Raw |
        ConvertFrom-Json -AsHashtable
    $objectDeployment = New-AzTenantDeployment `
        -Location $Location `
        -Name $objectName `
        -TemplateObject $templateObject `
        -TemplateParameterObject @{ marker = @{ value = 'object-base' } } `
        -Parameter 'marker=object-inline-wins' `
        -ValidationLevel ProviderNoRbac `
        -Confirm:$false

    # Verify the deployment name is derived when -Name is omitted.
    Copy-Item (Join-Path $bicepDir 'tenant-empty.json') $derivedTemplate
    $derivedDeployment = New-AzTenantDeployment `
        -Location $Location `
        -TemplateFile $derivedTemplate `
        -Parameter 'marker=derived-name' `
        -Confirm:$false
    $deployments.Add($derivedDeployment.Name)

    # Verify malformed Bicep is rejected.
    Assert-ExpectedFailure 'malformed Bicep create' 'Bicep compilation failed' {
        New-AzTenantDeployment `
            -Location $Location `
            -Name "$prefix-malformed" `
            -TemplateFile (Join-Path $bicepDir 'malformed.bicep') `
            -Confirm:$false `
            -ErrorAction Stop
    }

    # Verify a parameters URI cannot be combined with an inline override.
    Assert-ExpectedFailure 'parameters URI plus inline override' 'Inline parameters cannot be combined with a parameters URI' {
        New-AzTenantDeployment `
            -Location $Location `
            -Name "$prefix-parameter-conflict" `
            -TemplateFile (Join-Path $bicepDir 'tenant-empty.json') `
            -TemplateParameterUri https://example.com/parameters.json `
            -Parameter 'marker=conflict' `
            -Confirm:$false `
            -ErrorAction Stop
    }

    # Remove deployments by name, ID, and input object, including PassThru.
    if (-not (Remove-AzTenantDeployment -Name $jsonName -PassThru -Confirm:$false)) {
        throw 'Remove-AzTenantDeployment by name did not return true.'
    }
    $deployments.Remove($jsonName) | Out-Null

    if (-not (Remove-AzTenantDeployment -Id $bicepDeployment.Id -PassThru -Confirm:$false)) {
        throw 'Remove-AzTenantDeployment by id did not return true.'
    }
    $deployments.Remove($bicepName) | Out-Null

    if (-not ($objectDeployment | Remove-AzTenantDeployment -PassThru -Confirm:$false)) {
        throw 'Remove-AzTenantDeployment by input object did not return true.'
    }
    $deployments.Remove($objectName) | Out-Null

    Remove-AzTenantDeployment -Name $derivedDeployment.Name -Confirm:$false
    $deployments.Remove($derivedDeployment.Name) | Out-Null

    Write-Host 'All tenant deployment create/lifecycle smoke checks completed.' -ForegroundColor Green
} finally {
    foreach ($deploymentName in $deployments) {
        Remove-TestDeployment $deploymentName
    }
    Remove-Item $derivedTemplate -ErrorAction SilentlyContinue
}
