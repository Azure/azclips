## ADDED Requirements

### Requirement: Switch active profile
The system SHALL provide a `profile switch <name>` command that changes the active profile persistently without re-authentication.

#### Scenario: Switch to an existing profile
- **WHEN** user runs `azv2 profile switch staging`
- **THEN** the active profile SHALL be set to "staging"
- **AND** subsequent commands SHALL use the "staging" profile's tenant, subscription, and cloud
- **AND** no MSAL login flow SHALL be triggered

#### Scenario: Switch to a non-existent profile
- **WHEN** user runs `azv2 profile switch nonexistent`
- **THEN** the system SHALL display an error indicating the profile does not exist
- **AND** the active profile SHALL remain unchanged

### Requirement: Per-command profile override
Resource and Compute processors SHALL accept a `--profile <name>` parameter (CLI) or `-Profile <name>` parameter (PowerShell) that overrides the active profile for that single execution without changing the active profile pointer. (The `login` command also accepts `--profile`, but with re-authentication semantics defined in the login-profile-integration capability.) Other extension modules will be updated via code generation in a follow-up change.

#### Scenario: Override with --profile flag
- **WHEN** user runs `azv2 group list --profile staging` (CLI) or `Get-ResourceGroup -Profile staging` (PowerShell)
- **THEN** the command SHALL execute using the "staging" profile's connection state
- **AND** the active profile pointer SHALL NOT change
- **AND** subsequent commands without `--profile` / `-Profile` SHALL use the original active profile

#### Scenario: Override with non-existent profile
- **WHEN** user runs `azv2 group list --profile nonexistent`
- **THEN** the system SHALL display an error indicating the profile does not exist

### Requirement: Profile resolution order
The Engine SHALL resolve the profile for each command execution by **pre-binding the global `--profile` parameter before context creation** (resolving command metadata, then extracting/normalizing `--profile` from `CommandInvocation.Parameters`) in the following order: (1) `--profile` parameter if provided, (2) active profile if set, (3) null (no profile). Commands that require a profile SHALL fail at point of use with a clear error message when `Profile` is null.

#### Scenario: Resolution with --profile flag
- **WHEN** a command is invoked with `--profile staging` and the active profile is "prod"
- **THEN** the Engine SHALL use the "staging" profile

#### Scenario: Resolution with active profile only
- **WHEN** a command is invoked without `--profile` and the active profile is "prod"
- **THEN** the Engine SHALL use the "prod" profile

#### Scenario: Resolution with no profile available
- **WHEN** a command is invoked without `--profile` and no active profile is set
- **THEN** the Engine SHALL set `ProcessorContext.Profile` to null
- **AND** processors that require a profile SHALL fail at point of use with a clear error message

### Requirement: Command context exposes nullable Profile
The Engine SHALL include the resolved `Profile?` (nullable) on `ProcessorContext` passed to processors. When a profile is resolved, processors SHALL access tenant, subscription, cloud, and all other connection fields from `Profile`. When no profile is available (first-time use, bootstrap commands), `Profile` SHALL be null. The HTTP auth pipeline (`AuthHandler`) SHALL read `TenantId`/`SubscriptionId` directly from the resolved `Profile` for token acquisition. There SHALL be no `AuthContext` type — `Profile` is the single source of connection state.

#### Scenario: Processor accesses Profile from context
- **WHEN** the Engine resolves profile "prod" with tenant "t1", subscription "s1", and cloud "AzureCloud"
- **THEN** the processor SHALL receive the full `Profile` via `ProcessorContext.Profile`
- **AND** the processor SHALL access `Profile.TenantId`, `Profile.SubscriptionId`, `Profile.CloudName`, and all other profile fields directly

#### Scenario: Processor operates without a profile
- **WHEN** the Engine resolves no profile (e.g., first-time login)
- **THEN** `ProcessorContext.Profile` SHALL be null
- **AND** the processor SHALL execute without a profile (e.g., login creates one)

#### Scenario: Auth pipeline uses Profile for token acquisition
- **WHEN** any processor executes with a non-null `Profile`
- **THEN** the `AuthHandler` SHALL read `TenantId` and `SubscriptionId` from the resolved `Profile`
- **AND** the HTTP auth pipeline SHALL use the `Profile`'s tenant for token acquisition

### Requirement: GetContext processor removed
The legacy `show` (CLI) / `Get-CirrusAccount` (PowerShell) processor SHALL be removed. Users SHALL use `profile list` / `Get-CirrusProfile` to view all profiles and `profile show <name>` / `Get-CirrusProfile -Name <name>` to view a specific profile.

#### Scenario: show command is not available
- **WHEN** user runs `azv2 login show`
- **THEN** the system SHALL return a "command not found" error
- **AND** the user SHALL use `azv2 profile list` or `azv2 profile show <name>` instead
