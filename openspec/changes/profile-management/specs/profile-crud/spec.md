## ADDED Requirements

### Requirement: Profile model
The system SHALL define a `Profile` record in `Core.Abstractions` containing: `Name` (string), `TenantId` (string), `SubscriptionId` (string), `CloudName` (string), `AuthMethod` (enum: Browser, Broker, DeviceCode, ServicePrincipal, ManagedIdentity), `DefaultLocation` (string, optional), `DefaultResourceGroup` (string, optional), `UserPrincipalName` (string, optional), and `ClientId` (string, optional). All fields SHALL use language `default` values when not explicitly set. `CloudName` is a stored property with no behavioral impact in this change.

#### Scenario: Profile record contains all connection fields
- **WHEN** a Profile instance is created with name "prod", tenant "t1", subscription "s1", cloud "AzureCloud", and auth method Interactive
- **THEN** all fields SHALL be accessible on the record and optional fields SHALL be null

#### Scenario: Profile fields default to language defaults
- **WHEN** a Profile instance is created with only a name
- **THEN** TenantId, SubscriptionId, CloudName SHALL be their `default` values
- **AND** AuthMethod SHALL be its `default` enum value (`Browser`)
- **AND** optional fields (DefaultLocation, DefaultResourceGroup, UserPrincipalName, ClientId) SHALL be null

### Requirement: Profile name validation
Profile names SHALL be 1–100 characters long, consisting of alphanumeric characters, hyphens (`-`), underscores (`_`), and periods (`.`). The name `"default"` is reserved for bare login and SHALL NOT be accepted by user-facing profile creation commands.

#### Scenario: Valid profile name
- **WHEN** a profile is saved with name "my-prod.env_01"
- **THEN** the name SHALL be accepted

#### Scenario: Invalid profile name (too long)
- **WHEN** a profile is saved with a name longer than 100 characters
- **THEN** the system SHALL reject it with a validation error

#### Scenario: Invalid profile name (disallowed characters)
- **WHEN** a profile is saved with name "my profile!" (contains space and exclamation mark)
- **THEN** the system SHALL reject it with a validation error

### Requirement: Profile service interface
The system SHALL define an `IProfileService` interface in `Core.Abstractions` with methods: `GetAllProfilesAsync`, `GetProfileAsync(name)`, `SaveProfileAsync(profile)`, `SaveAndActivateProfileAsync(profile)`, `RemoveProfileAsync(name)`, `GetActiveProfileNameAsync`, `GetActiveProfileAsync`, and `SetActiveProfileNameAsync(name)`. `GetActiveProfileAsync` and `SaveAndActivateProfileAsync` are native interface members (not extension methods) so implementations can resolve the active pointer and persist-plus-activate under a single lock, minimizing file I/O.

#### Scenario: IProfileService is registered in DI container
- **WHEN** `AzclipsServiceProvider.Initialize()` is called
- **THEN** `IProfileService` SHALL be resolvable from the service provider as a singleton registered by `Core.Runtime`

#### Scenario: Get the active profile
- **WHEN** profiles "prod" and "staging" exist and "prod" is active, and `GetActiveProfileAsync` is called
- **THEN** the system SHALL return the full "prod" profile

#### Scenario: Get the active profile when none is set
- **WHEN** no active profile pointer is set and `GetActiveProfileAsync` is called
- **THEN** the system SHALL return null

#### Scenario: Get the active profile when the pointer is dangling
- **WHEN** the active pointer references a profile that no longer exists and `GetActiveProfileAsync` is called
- **THEN** the system SHALL return null

#### Scenario: Save and activate a profile in one operation
- **WHEN** `SaveAndActivateProfileAsync` is called with a profile named "prod"
- **THEN** the "prod" profile SHALL be persisted to `profiles.json`
- **AND** "prod" SHALL become the active profile
- **AND** both writes SHALL be atomic

### Requirement: Profile persistence
The system SHALL persist all profiles in `{LocalApplicationData}/.azclips/profiles.json` as a JSON object keyed by profile name (consistent with existing `AuthContextService` and `MsalAuthService` storage paths). The active profile name SHALL be persisted separately in `{LocalApplicationData}/.azclips/active-profile` as plain text. The `ProfileService` implementation SHALL reside in `Core.Runtime` because profiles encompass configuration beyond authentication and will expand to cover all connection-related settings. All file writes SHALL be atomic — write to a temporary file then rename — to prevent corruption from concurrent access.

#### Scenario: Profiles are saved to disk atomically
- **WHEN** `SaveProfileAsync` is called with a profile named "prod"
- **THEN** `{LocalApplicationData}/.azclips/profiles.json` SHALL contain an entry keyed "prod" with all profile fields serialized
- **AND** the write SHALL use a temporary file plus rename to ensure atomicity

#### Scenario: Active profile is persisted separately
- **WHEN** `SetActiveProfileNameAsync("prod")` is called
- **THEN** `{LocalApplicationData}/.azclips/active-profile` SHALL contain the text "prod"
- **AND** `profiles.json` SHALL NOT be modified

### Requirement: List profiles
The system SHALL provide a `profile list` command (CLI) / `Get-AzProfile` cmdlet without `-Name` parameter (PowerShell) that displays all saved profiles.

#### Scenario: List all profiles
- **WHEN** user runs `az profile list`
- **THEN** the system SHALL display all profile names with their tenant, subscription, and cloud values
- **AND** the active profile SHALL be indicated

#### Scenario: No profiles exist
- **WHEN** user runs `az profile list` and no profiles are saved
- **THEN** the system SHALL display an empty result

### Requirement: Show a single profile
The system SHALL provide a `profile show <name>` command (CLI) / `Get-AzProfile -Name <name>` cmdlet (PowerShell) that displays the full details of one profile. The command SHALL also accept an `--active` (CLI) / `-Active` (PowerShell) switch that displays the active profile instead of a named one. The `--active` switch and a profile name are mutually exclusive, and exactly one of them SHALL be provided.

#### Scenario: Show existing profile
- **WHEN** user runs `az profile show prod`
- **THEN** the system SHALL display all fields of the "prod" profile

#### Scenario: Show non-existent profile
- **WHEN** user runs `az profile show nonexistent`
- **THEN** the system SHALL display an error indicating the profile does not exist

#### Scenario: Show the active profile
- **WHEN** user runs `az profile show --active` and a profile is active
- **THEN** the system SHALL display all fields of the active profile

#### Scenario: Show the active profile when none is active
- **WHEN** user runs `az profile show --active` and no profile is active
- **THEN** the system SHALL display an error indicating no active profile is set

#### Scenario: Name and --active are mutually exclusive
- **WHEN** user runs `az profile show prod --active`
- **THEN** the system SHALL display an error indicating that a name and `--active` cannot be used together

#### Scenario: Neither name nor --active provided
- **WHEN** user runs `az profile show` with no name and without `--active`
- **THEN** the system SHALL display an error indicating a profile name or `--active` is required

### Requirement: Remove a profile
The system SHALL provide a `profile remove <name>` command (CLI) / `Remove-AzProfile -Name <name>` cmdlet (PowerShell) that deletes a profile definition.

#### Scenario: Remove a non-active profile
- **WHEN** user runs `az profile remove staging` and "staging" is not the active profile
- **THEN** the "staging" profile SHALL be removed from `profiles.json`
- **AND** the active profile SHALL remain unchanged

#### Scenario: Remove the active profile
- **WHEN** user runs `az profile remove prod` and "prod" is the active profile
- **THEN** the "prod" profile SHALL be removed
- **AND** the active profile SHALL be cleared (no active profile)

#### Scenario: Remove non-existent profile
- **WHEN** user runs `az profile remove nonexistent`
- **THEN** the system SHALL display an error indicating the profile does not exist
