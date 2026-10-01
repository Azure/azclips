## ADDED Requirements

### Requirement: Self-contained API surface tool
The repository SHALL keep all API-surface wrapper source, scripts, MSBuild integration, documentation, and upstream provenance under `tools/ApiSurface`. The fetched upstream GenAPI source SHALL remain ignored by Git.

#### Scenario: Inspect the tool
- **WHEN** a maintainer opens `tools/ApiSurface`
- **THEN** the directory contains everything required to generate Core API surfaces and update the GenAPI implementation
- **AND** no Core project or repository-wide build file requires a persistent tool import
- **AND** the upstream GenAPI implementation source is not tracked

### Requirement: Generate Core API surfaces with one command
The repository SHALL provide `pwsh tools/ApiSurface/Generate-CoreApi.ps1` to generate public API listings for all product projects directly beneath `src/Core`.

#### Scenario: Generate API listings
- **WHEN** a developer runs `pwsh tools/ApiSurface/Generate-CoreApi.ps1`
- **AND** the ignored GenAPI source is present
- **THEN** the command discovers and builds the Core projects in Release configuration
- **AND** GenAPI uses each project's resolved framework, project, and package references
- **AND** the command writes one listing per assembly and target framework under `artifacts/api-surface/Core`

#### Scenario: Generate from a clean checkout
- **WHEN** a developer runs `pwsh tools/ApiSurface/Generate-CoreApi.ps1`
- **AND** the ignored GenAPI source is absent
- **THEN** the command invokes the GenAPI update command to populate it
- **AND** continues with API surface generation

#### Scenario: Generate after adding a Core project
- **WHEN** a project is added directly beneath `src/Core`
- **AND** the generation command is run
- **THEN** its API listing is generated without maintaining a separate project list

### Requirement: Update GenAPI capabilities with one command
The repository SHALL provide `pwsh tools/ApiSurface/Update-GenApi.ps1` to refresh the vendored GenAPI implementation from the `dotnet/sdk` revision matching the SDK version in `global.json`.

#### Scenario: Refresh the implementation
- **WHEN** a maintainer runs `pwsh tools/ApiSurface/Update-GenApi.ps1`
- **THEN** the command replaces the selected upstream GenAPI source under the ignored `tools/ApiSurface/GenApi/src` directory
- **AND** updates the recorded upstream tag, commit, copied paths, and local adaptations
- **AND** does not modify product source or generated API artifacts

### Requirement: Keep generated listings temporary
Generated API listings SHALL be written under the git-ignored `artifacts/api-surface/Core` directory and SHALL NOT be treated as checked-in compatibility baselines.

#### Scenario: Regenerate review output
- **WHEN** the generation command runs
- **THEN** it replaces the previous Core API surface output
- **AND** the generated files remain outside the Git working tree
