## Purpose

Validate PowerShell package and runtime dependency contracts independently of the normal module installation lifecycle.

## ADDED Requirements

### Requirement: Isolated automatic dependency resolution
The suite SHALL request each consumer without preinstalling dependencies and verify resolved packages and fresh-process imports against the source dependency contract.

#### Scenario: Complete local gallery
- **WHEN** a consumer is requested from a complete local gallery
- **THEN** its dependencies resolve and its import succeeds without preloading or using user-installed modules

#### Scenario: Incorrect dependency metadata
- **WHEN** package dependency metadata differs from the source contract
- **THEN** validation fails with the affected package and dependency

### Requirement: Intentional dependency rejection
The suite SHALL distinguish expected dependency errors from unrelated errors and unexpected success.

#### Scenario: Missing or incompatible dependency
- **WHEN** an isolated gallery lacks a required compatible dependency
- **THEN** the suite verifies the intended dependency failure and records resulting module state

#### Scenario: Conflicting loaded dependency
- **WHEN** a consumer is imported with an incompatible dependency loaded
- **THEN** validation requires the production conflict diagnostic

### Requirement: Runtime contract validation
The suite SHALL verify the supported runtime and deliberate rejection of incompatible or missing prerequisites without changing the primary toolchain.

#### Scenario: Unsupported PowerShell
- **WHEN** the installation helper and packaged manifest run under an older unsupported PowerShell
- **THEN** both reject the unsupported version with actionable diagnostics

#### Scenario: Missing framework
- **WHEN** the framework-dependent target is launched without its required .NET framework in an isolated toolchain
- **THEN** the launcher fails and its diagnostic identifies the missing framework

### Requirement: Bounded execution and persistent reporting
The pipeline SHALL run dependency validation before normal module installation and retain per-scenario outcomes and diagnostics.

#### Scenario: Unexpected failure or timeout
- **WHEN** a scenario produces the wrong error, unexpectedly succeeds, or times out
- **THEN** validation fails and records the failure while preserving completed scenario results

#### Scenario: Unchanged normal lifecycle
- **WHEN** dependency validation finishes
- **THEN** its private files and repository registrations are cleaned up without changing user modules or the shared gallery
