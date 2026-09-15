## Why

The existing PowerShell lifecycle installs dependencies before their consumers, which can hide broken package dependency resolution. Azure/CLIPS#787 also needs intentional runtime and dependency failures distinguished from incidental installation failures.

## What Changes

- Add isolated real-package resolution and import scenarios, including missing packages and version conflicts.
- Validate package dependency metadata against the source manifests.
- Add supported-runtime checks and isolated negative runtime probes.
- Run a Dependencies phase before the existing installation phase, with bounded subprocesses and structured diagnostics.
- Add offline regression fixtures and document scope and failure contracts.

## Capabilities

### New Capabilities

- `ps-dependency-validation`: Validate package resolution, dependency rejection, and runtime prerequisites without modifying normal module installations.

### Modified Capabilities

None.

## Impact

PowerShell packaging validation scripts, Azure Pipelines phase orchestration and artifacts, GitHub fixture workflow, and associated documentation. No change to public cmdlet behavior or existing installation/uninstallation semantics. Public PSGallery publication and PSResourceGet integration remain separate delivery work.
