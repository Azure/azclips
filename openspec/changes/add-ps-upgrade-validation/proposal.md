## Why

The Upgrade phase currently reports a successful no-op. Azure/CLIPS#777 needs a real released-version-to-pipeline-package upgrade using the documented GitHub bundle and local-gallery installer.

## What Changes

- Select the latest stable GitHub PowerShell bundle, or the previous distinct version when the latest equals the pipeline's Az version.
- Install the baseline with its bundled InstallModule.ps1, add pipeline packages to the same gallery, and install their exact versions.
- Verify installed payload hashes, imports and command exports, including modules whose versions did not change.
- Run upgrade as a self-cleaning transaction before fresh installation, retain all outcomes, and keep independent validation running after failures.

## Capabilities

### New Capabilities

- `ps-upgrade-validation`: Validate upgrades from published PowerShell bundles to pipeline artifacts with provenance and cleanup guarantees.

### Modified Capabilities

None.

## Impact

PowerShell validation scripts and fixtures, Azure Pipelines preparation/matrix jobs, GitHub fixture wiring, phase-report schema and documentation. No change to public cmdlets or release package versions.
