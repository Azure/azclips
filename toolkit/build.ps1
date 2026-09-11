#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build the entire Azure Client Engine toolkit — both toolchains.

.DESCRIPTION
  Single entry point for building the whole toolkit, so tools and pipelines do
  not need to know the per-package build commands:

    - TypeScript packages (ir, typespec-emitter, compiler, transformers, cli) via `npm run build`.
    - The C# generator + its out-of-process host via `dotnet build` (the host's
      project reference pulls in the generator library).

  `npm run build` (tsc -b) remains the fast inner loop for TypeScript-only work;
  this script is the full cross-toolchain build that CI and other pipelines call.

  Runs from the toolkit root regardless of the caller's working directory.

.PARAMETER Configuration
  .NET build configuration (Debug or Release). Default: Debug. The CLI resolves
  the generator host from bin/<Configuration>/net10.0 (Release preferred).

.PARAMETER SkipTypeScript
  Skip the TypeScript build.

.PARAMETER SkipDotnet
  Skip the .NET build.

.EXAMPLE
  ./build.ps1

.EXAMPLE
  ./build.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$SkipTypeScript,

    [switch]$SkipDotnet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Always operate from the toolkit root (this script's own directory).
$toolkitRoot = $PSScriptRoot

function Invoke-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed (exit code $LASTEXITCODE)."
    }
}

Push-Location $toolkitRoot
try {
    if (-not $SkipTypeScript) {
        Invoke-Step 'Building TypeScript packages (npm run build)' { npm run build }
    }

    if (-not $SkipDotnet) {
        # Publish the generator host (its ProjectReference also builds the library)
        # and stage it next to the CLI at packages/cli/generator-host, where the CLI
        # resolves it at runtime. Build-config (Debug/Release) and dependency
        # gathering live here — the CLI runtime stays config-agnostic.
        $hostProject = Join-Path $toolkitRoot 'packages/generator/host/Ace.Generator.Host.csproj'
        $hostStage = Join-Path $toolkitRoot 'packages/cli/generator-host'
        Invoke-Step "Publishing .NET generator host -> packages/cli/generator-host ($Configuration)" {
            if (Test-Path $hostStage) { Remove-Item $hostStage -Recurse -Force }
            dotnet publish $hostProject -c $Configuration -o $hostStage --nologo
        }
    }

    Write-Host '==> Toolkit build complete.' -ForegroundColor Green
}
finally {
    Pop-Location
}
