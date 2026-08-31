# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Local live integration smoke test for the azclips managed identity CLI surface.

.DESCRIPTION
    Builds the local Debug CLI unless -SkipBuild is supplied, changes to
    artifacts\Debug\Core.Cli, and invokes .\az.exe for:

      az identity create
      az identity list
      az identity show
      az identity update
      az identity delete

    The script intentionally runs every azclips command from artifacts\Debug\Core.Cli because
    the local Debug bundled-extension loader resolves extensions relative to that directory.

.EXAMPLE
    pwsh -NoProfile -File .\tests\Scripts\CLI\IdentityLifecycle.ps1 `
      -TenantId <test-tenant> `
      -SubscriptionId <test-subscription>

.EXAMPLE
    # If already signed in with the azclips CLI:
    pwsh -NoProfile -File .\tests\Scripts\CLI\IdentityLifecycle.ps1 `
      -SubscriptionId <test-subscription> `
      -SkipLogin
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SubscriptionId,

    [string] $TenantId,

    [string] $ResourceGroup,

    [string] $IdentityName,

    [string] $Location = 'westus2',

    [switch] $SkipBuild,

    [switch] $SkipLogin,

    [switch] $KeepIdentity,

    [switch] $CleanupResourceGroup
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$cliDir = Join-Path $repoRoot 'artifacts\Debug\Core.Cli'
$azExe = Join-Path $cliDir 'az.exe'

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    $ResourceGroup = "azclips-identity-it-$stamp"
}
if ([string]::IsNullOrWhiteSpace($IdentityName)) {
    $IdentityName = "azclips-id-$stamp"
}

$identityId = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IdentityName"

function Invoke-AzClips {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [switch] $AllowFailure
    )

    Push-Location $cliDir
    try {
        Write-Host "`n> .\az.exe $($Arguments -join ' ')" -ForegroundColor Cyan
        & .\az.exe @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Command failed with exit code $exitCode`: .\az.exe $($Arguments -join ' ')"
    }
}

if (-not $SkipBuild) {
    Write-Host "Building azclips.slnx..." -ForegroundColor Cyan
    dotnet build (Join-Path $repoRoot 'azclips.slnx') --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path $azExe)) {
    throw "az.exe not found at '$azExe'. Build the solution first or run without -SkipBuild."
}

if (-not $SkipLogin) {
    if ([string]::IsNullOrWhiteSpace($TenantId)) {
        throw "TenantId is required unless -SkipLogin is specified."
    }

    Invoke-AzClips @(
        'login',
        '--tenant-id', $TenantId,
        '--subscription-id', $SubscriptionId
    )
}

try {
    Invoke-AzClips @(
        'config', 'set',
        '--key', 'agent.enabled',
        '--value', 'false'
    )

    Invoke-AzClips @(
        'group', 'create',
        '--subscription-id', $SubscriptionId,
        '--name', $ResourceGroup,
        '--location', $Location
    )

    Invoke-AzClips @(
        'identity', 'create',
        '--subscription', $SubscriptionId,
        '--resource-group', $ResourceGroup,
        '--name', $IdentityName,
        '--location', $Location,
        '--tags', 'env=local-integration',
        '--isolation-scope', 'Regional',
        '--resource-restriction', '{"providers":["Microsoft.Compute"]}'
    )

    Invoke-AzClips @(
        'identity', 'list',
        '--subscription', $SubscriptionId,
        '--resource-group', $ResourceGroup
    )

    Invoke-AzClips @(
        'identity', 'show',
        '--ids', $identityId
    )

    Invoke-AzClips @(
        'identity', 'update',
        '--ids', $identityId,
        '--isolation-scope', 'None',
        '--tags', 'env=local-integration-updated'
    )

    Invoke-AzClips @(
        'identity', 'show',
        '--ids', $identityId
    )

    Write-Host "`nPASS: identity lifecycle completed for $identityId" -ForegroundColor Green
}
finally {
    if (-not $KeepIdentity) {
        Invoke-AzClips @(
            'identity', 'delete',
            '--ids', $identityId
        ) -AllowFailure
    }

    if ($CleanupResourceGroup) {
        Invoke-AzClips @(
            'rest',
            '--method', 'DELETE',
            '--uri', "/subscriptions/$SubscriptionId/resourcegroups/$ResourceGroup?api-version=2025-04-01"
        ) -AllowFailure
    }
}
