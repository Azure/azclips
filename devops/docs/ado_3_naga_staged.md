# ado_3_naga_staged Pipeline: Detailed Behavior

This document explains what happens in the Azure DevOps pipeline defined in ../../.azure-pipelines/ado_3_naga_staged.yml.

## Purpose

The pipeline builds, tests, packages, and publishes artifacts for:
- Core solution build outputs
- Windows CLI + MSIX
- Linux CLI tar.gz
- Extension outputs and NuGet packages
- PowerShell module validation

## Trigger Model

- CI trigger: none
- PR trigger: none

This pipeline is manual by default.

## High-Level Execution Flow

Execution order is stage-based:

1. Initialize workflow
2. Build and test
3. Build and publish CLI and extension outputs
4. Discover, build, and pack extensions from repository
5. Discover, build, and validate PowerShell modules
6. Finalize workflow and report completion

Dependency graph:

- Init runs first.
- Build, Publish, Extensions, and PowerShell each depend on Init.
- Build, Publish, Extensions, and PowerShell can run in parallel after Init completes.
- Finalize depends on Init, Build, Publish, Extensions, and PowerShell.
- Finalize uses always(), so it still runs even if an upstream stage fails.

## Variables

Pipeline variables used by jobs:

- Solution_Name = cirrus.slnx
- Publish_Root = ./publish
- Bin_Root = ./bin
- DOTNET_CLI_TELEMETRY_OPTOUT = 1
- DOTNET_NOLOGO = 1

Additional governance/static-analysis toggles are set in variables (for example CodeQL and NuGet analysis flags).

## Stage-by-Stage Breakdown

## Stage: Initialize workflow

Job: Start (ubuntu-latest)

Steps:
- Emit start marker

Purpose:
- Establish a common starting stage for all fan-out stages.

## Stage: Build and test

Job: Build artifacts (windows-latest, matrix)

Matrix legs:
- Debug_win_x64
- Release_win_x64

Per matrix leg:
- Checkout repository
- Install .NET SDK from global.json
- dotnet restore solution
- dotnet build solution with selected configuration
- dotnet test solution with selected configuration
- Publish build artifacts from artifacts/<configuration>

Published artifact name pattern:
- Build_Artifacts_<configuration>_<rid>

## Stage: Build and publish CLI and extension outputs

### Job: CLI Win + MSIX (windows-latest, matrix)

Matrix legs:
- Debug_win_x64
- Release_win_x64

Per matrix leg:
- Restore CLI project
- Build and publish Windows CLI (self-contained)
- Copy and rewrite appx manifest publisher fields
- Copy appx assets
- Build unsigned MSIX using MakeAppx
- Publish CLI output artifact
- Publish MSIX artifact

Published artifact name patterns:
- Published_CLI_<configuration>_<rid>
- MSIX_Package_<configuration>_<rid>

### Job: CLI Linux + tgz (ubuntu-latest, matrix)

Matrix legs:
- Release_linux_x64

Per matrix leg:
- Restore CLI project
- Build and publish Linux CLI (self-contained)
- Create tar.gz package
- Publish tar.gz artifact

Published artifact name pattern:
- Linux_Tarball_<configuration>_<rid>

### Job: Extension (windows-latest, matrix)

Matrix legs:
- Debug_win_x64
- Release_win_x64

Per matrix leg:
- Restore extension project
- Build and publish extension project
- Publish extension artifact

Published artifact name pattern:
- Published_Extension_<configuration>_<rid>

## Stage: Discover, build, and pack extensions from repository

### Job: Discover extension matrix (windows-latest)

Behavior:
- Scans src/Extensions recursively for *.csproj.
- Reads ExtName and PackageId from each project file.
- Produces two output matrices:
  - buildMatrix: Debug + Release entries per extension
  - packMatrix: Release entries per extension package

Outputs:
- setExtensionMatrices.buildMatrix
- setExtensionMatrices.packMatrix

### Job: Build extension matrix (windows-latest, dynamic matrix)

Condition:
- Runs only when buildMatrix is not {}.

Per matrix leg:
- Restore extension from discovered path
- Build extension
- Publish build artifact directory

Published artifact name pattern:
- Extension_<extName>_<configuration>

### Job: Pack NuGet (windows-latest, dynamic matrix)

Condition:
- Runs only when packMatrix is not {}.

Per matrix leg:
- Restore extension from discovered path
- Build extension (produces package output in artifacts)
- Publish NuGet artifact directory

Published artifact name pattern:
- Extension_NuGet_<packageIdentity>_<configuration>

## Stage: Discover, build, and validate PowerShell modules

### Job: Discover module matrix (windows-latest)

Behavior:
- Scans src/PS recursively for *.psd1.
- Builds module matrix values for:
  - moduleName
  - moduleManifestPath
  - moduleDir
  - cmdletsProjectPath
  - extensionProjectPath

Output:
- setModuleMatrix.moduleMatrix

### Job: Build modules + validate (windows-latest, dynamic matrix)

Condition:
- Runs only when moduleMatrix is not {}.

Per matrix leg:
- Verify PowerShell runtime
- Restore/build corresponding extension project
- Restore/build corresponding cmdlets project
- If moduleName is Resources:
  - Restore/build Login dependency projects
- Restore/build full solution for module import validation context
- Validate manifest dependencies recursively with Test-ManifestDependencies

Note:
- This stage validates module dependency correctness but does not publish new artifacts.

## Stage: Finalize workflow and report completion

Job: End (ubuntu-latest)

Condition:
- always()

Steps:
- Emit workflow end marker

Purpose:
- Always provide a deterministic final stage in the stage map.

## Artifact Inventory Summary

Artifacts produced by this pipeline include:

- Build_Artifacts_<configuration>_<rid>
- Published_CLI_<configuration>_<rid>
- MSIX_Package_<configuration>_<rid>
- Linux_Tarball_<configuration>_<rid>
- Published_Extension_<configuration>_<rid>
- Extension_<extName>_<configuration>
- Extension_NuGet_<packageIdentity>_<configuration>

## Important Behavioral Notes

- Publish jobs perform their own build work.
  - They run dotnet publish without --no-build.
  - They do not consume Build stage output artifacts.
- Build, Publish, Extensions, and PowerShell are orchestration-parallel after Init.
- Matrix leg names are appended by Azure DevOps in UI lists, which can make job entries look long even with short display names.

## Where to edit

If behavior changes are needed, update:
- Pipeline definition: ../../.azure-pipelines/ado_3_naga_staged.yml
- This documentation: ./ado_3_naga_staged.md
