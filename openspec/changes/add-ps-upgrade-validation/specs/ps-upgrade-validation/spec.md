## ADDED Requirements

### Requirement: Select a released upgrade baseline
The pipeline SHALL select the latest stable PowerShell release when its version is lower than the target Az version.

#### Scenario: Latest release equals the pipeline version
- **WHEN** the latest stable release has the same Az version as the pipeline packages
- **THEN** the highest older distinct stable version is selected
- **AND** prereleases and drafts are excluded

#### Scenario: Invalid baseline
- **WHEN** no older release exists or the latest stable release is newer than the target
- **THEN** upgrade validation fails explicitly rather than reporting a no-op or downgrade as an upgrade

### Requirement: Use the recommended local-gallery workflow
The suite SHALL install the released bundle with its bundled InstallModule.ps1 and then add pipeline packages to the same local gallery.

#### Scenario: Pipeline packages replace the baseline
- **WHEN** the baseline is installed and the gallery receives the pipeline packages
- **THEN** the installer installs the exact target module versions and the target rollup from that gallery
- **AND** unchanged individual module versions are refreshed rather than silently retaining old payloads

### Requirement: Establish artifact provenance
The suite SHALL verify the downloaded bundle digest and the installed package payloads.

#### Scenario: Wrong package contents
- **WHEN** a target installation has an incorrect version, file content, import location or command surface
- **THEN** validation fails with the affected module and diagnostic

### Requirement: Safe cleanup and independent validation
The suite SHALL refuse preexisting exact baseline/target versions and cleanup only this transaction's attempted installations, repository and scratch state.

#### Scenario: Upgrade failure
- **WHEN** baseline installation, target installation or verification fails
- **THEN** cleanup still runs and the original failure and any cleanup failures are reported
- **AND** fresh installation can run independently after successful bootstrap

#### Scenario: Completed distribution
- **WHEN** the distribution report is finalized
- **THEN** Upgrade is required for success and its actual outcome is retained rather than replaced by a placeholder
