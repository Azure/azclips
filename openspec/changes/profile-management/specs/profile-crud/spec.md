## ADDED Requirements

### Requirement: Profile data model
**Priority**: P0
**Rationale**: The `Profile` class is the foundation of the entire profile system. It must capture complete connection state and support extensible defaults without requiring model changes for new default fields.

The system SHALL define a `Core.Abstractions.Profile` class with the following properties:
- `Name` (string, required) — unique identifier for the profile, case-insensitive comparison
- `TenantId` (string, required) — Microsoft Entra tenant identifier
- `SubscriptionId` (string, required) — Azure subscription identifier
- `CloudName` (string, optional) — cloud environment name (default: `"AzureCloud"`)
- `AuthMethod` (string, optional) — authentication method used (e.g., `"interactive"`, `"device-code"`, `"managed-identity"`, `"service-principal"`)
- `UserPrincipalName` (string, optional) — UPN of the authenticated user
- `ClientId` (string, optional) — application/client ID for service principal auth
- `Defaults` (Dictionary<string, string>, optional) — extensible key-value pairs for default parameters (e.g., `"location"`, `"resource-group"`)

Profile names MUST be validated: non-empty, no whitespace-only, no path separator characters (`/`, `\`). The name `"default"` SHALL be reserved for the system-generated default profile.

#### Scenario: Valid profile creation
**Test mapping**: `Profile_ShouldAcceptValidProperties_WhenConstructed`

- **Given** a profile name "my-dev" with TenantId "t1" and SubscriptionId "s1"
- **When** a `Profile` instance is created
- **Then** the Name SHALL be "my-dev", TenantId SHALL be "t1", SubscriptionId SHALL be "s1"
- **And** CloudName SHALL default to "AzureCloud"
- **And** Defaults SHALL be an empty dictionary

#### Scenario: Invalid profile name rejected
**Test mapping**: `Profile_ShouldThrow_WhenNameContainsInvalidCharacters`

- **Given** a profile name containing "/" or "\" or whitespace-only
- **When** a `Profile` is constructed or validated
- **Then** the system SHALL throw an `ArgumentException` with a message identifying the invalid characters

#### Scenario: Profile serialization round-trip
**Test mapping**: `Profile_ShouldSerializeAndDeserialize_WhenUsingJsonSerializer`

- **Given** a `Profile` with all properties populated including Defaults {"location": "eastus2", "resource-group": "dev-rg"}
- **When** the profile is serialized to JSON and deserialized back
- **Then** all properties SHALL be preserved including the Defaults dictionary
- **And** the JSON SHALL use camelCase property naming

### Requirement: Profile persistence with atomic writes
**Priority**: P0
**Rationale**: Concurrent CLI invocations (e.g., parallel Terraform runs) must not corrupt the profile store. Atomic writes prevent partial-write corruption.

`Core.Runtime.ProfileService` SHALL persist all profiles and the active profile pointer in a single file at `{Environment.SpecialFolder.LocalApplicationData}/.cirrus/profiles.json`.

The file format SHALL be:
```json
{
  "activeProfile": "<name>",
  "profiles": {
    "<name>": { "name": "...", "tenantId": "...", ... }
  }
}
```

All writes to `profiles.json` MUST use atomic write: serialize to a temporary file in the same directory, then `File.Move` (overwrite) to the target path. The temporary file SHALL use a unique name (e.g., `profiles.json.tmp.<guid>`) to avoid collisions.

The service MUST create the `{LocalApplicationData}/.cirrus/` directory if it does not exist.

#### Scenario: Atomic write prevents corruption
**Test mapping**: `ProfileService_ShouldWriteAtomically_WhenSavingProfile`

- **Given** a `ProfileService` instance
- **When** `SaveProfileAsync` is called
- **Then** the service SHALL write to a temporary file first
- **And** the service SHALL move the temporary file to `profiles.json` atomically
- **And** if the process crashes during write, `profiles.json` SHALL remain in its previous valid state

#### Scenario: Directory creation on first write
**Test mapping**: `ProfileService_ShouldCreateDirectory_WhenDirectoryMissing`

- **Given** the `.cirrus` directory does not exist
- **When** `SaveProfileAsync` is called
- **Then** the service SHALL create the directory
- **And** the profile SHALL be persisted successfully

#### Scenario: Concurrent in-process writes serialized
**Test mapping**: `ProfileService_ShouldSerializeWrites_WhenCalledConcurrently`

- **Given** two concurrent calls to `SaveProfileAsync` within the same process
- **When** both writes execute
- **Then** the writes SHALL be serialized via `SemaphoreSlim`
- **And** both profiles SHALL be present in the resulting `profiles.json`
- **And** no data SHALL be lost

### Requirement: IProfileService interface
**Priority**: P0
**Rationale**: The service interface defines the contract for all profile operations. It must be defined in `Core.Abstractions` so that both Core and Extensions can depend on it.

`Core.Abstractions.IProfileService` SHALL define the following methods:

| Method | Returns | Description |
|---|---|---|
| `GetAllProfilesAsync(CancellationToken)` | `Task<IReadOnlyList<Profile>>` | Returns all defined profiles |
| `GetProfileAsync(string name, CancellationToken)` | `Task<Profile?>` | Returns a profile by name (case-insensitive) or null |
| `GetActiveProfileAsync(CancellationToken)` | `Task<Profile?>` | Returns the currently active profile or null |
| `SetActiveProfileAsync(string name, CancellationToken)` | `Task` | Sets the active profile pointer; throws if profile does not exist |
| `SaveProfileAsync(Profile profile, CancellationToken)` | `Task` | Creates or updates a profile (upsert by name) |
| `RemoveProfileAsync(string name, CancellationToken)` | `Task<bool>` | Removes a profile by name; returns false if not found. If the removed profile was active, active pointer is cleared. |

`IProfileService` SHALL be registered as a Singleton via `RegisterProfileServices()` in `Core.Runtime`.

#### Scenario: Create a new profile
**Test mapping**: `ProfileService_ShouldPersistProfile_WhenSaveProfileAsyncCalled`

- **Given** no profiles exist
- **When** `SaveProfileAsync` is called with a Profile named "dev"
- **Then** `GetProfileAsync("dev")` SHALL return the saved profile
- **And** `GetAllProfilesAsync()` SHALL return a list containing exactly one profile

#### Scenario: Update an existing profile
**Test mapping**: `ProfileService_ShouldUpdateProfile_WhenProfileAlreadyExists`

- **Given** a profile named "dev" exists with SubscriptionId "old-sub"
- **When** `SaveProfileAsync` is called with a Profile named "dev" and SubscriptionId "new-sub"
- **Then** `GetProfileAsync("dev")` SHALL return the profile with SubscriptionId "new-sub"
- **And** the total profile count SHALL remain unchanged

#### Scenario: Remove existing profile
**Test mapping**: `ProfileService_ShouldRemoveProfile_WhenProfileExists`

- **Given** profiles "dev" and "prod" exist, with "dev" as the active profile
- **When** `RemoveProfileAsync("dev")` is called
- **Then** the method SHALL return true
- **And** `GetProfileAsync("dev")` SHALL return null
- **And** the active profile pointer SHALL be cleared (no active profile)

#### Scenario: Remove non-existent profile
**Test mapping**: `ProfileService_ShouldReturnFalse_WhenRemovingNonExistentProfile`

- **Given** no profile named "staging" exists
- **When** `RemoveProfileAsync("staging")` is called
- **Then** the method SHALL return false
- **And** no changes SHALL be made to the profile store

#### Scenario: Set active profile
**Test mapping**: `ProfileService_ShouldSetActiveProfile_WhenProfileExists`

- **Given** profiles "dev" and "prod" exist
- **When** `SetActiveProfileAsync("prod")` is called
- **Then** `GetActiveProfileAsync()` SHALL return the "prod" profile
- **And** `profiles.json` SHALL contain `"activeProfile": "prod"`

#### Scenario: Set active profile when profile does not exist
**Test mapping**: `ProfileService_ShouldThrow_WhenSettingActiveToNonExistentProfile`

- **Given** no profile named "staging" exists
- **When** `SetActiveProfileAsync("staging")` is called
- **Then** the service SHALL throw an `InvalidOperationException`
- **And** the message SHALL include the profile name and available profile names

#### Scenario: Case-insensitive profile lookup
**Test mapping**: `ProfileService_ShouldFindProfile_WhenNameDiffersByCase`

- **Given** a profile named "MyDev" exists
- **When** `GetProfileAsync("mydev")` is called
- **Then** the service SHALL return the "MyDev" profile

### Requirement: Profile list command
**Priority**: P0
**Rationale**: Users must be able to discover available profiles and identify the active one.

The system SHALL provide a `profile list` CLI command (`Get-CirrusProfile` in PowerShell with no parameters) that lists all profiles with a marker indicating the active profile.

CLI syntax: `azv2 profile list`
PowerShell syntax: `Get-CirrusProfile`

#### Scenario: List profiles with active marker
**Test mapping**: `ListProfilesCommand_ShouldListAllProfiles_WhenProfilesExist`

- **Given** profiles "default", "dev", and "prod" exist with "dev" as the active profile
- **When** the user runs `azv2 profile list`
- **Then** the output SHALL list all three profiles
- **And** the active profile "dev" SHALL be visually marked (e.g., `* dev` or `dev (active)`)

#### Scenario: List profiles when none exist
**Test mapping**: `ListProfilesCommand_ShouldShowGuidance_WhenNoProfilesExist`

- **Given** no profiles exist
- **When** the user runs `azv2 profile list`
- **Then** the output SHALL include actionable guidance: "No profiles found. Run 'azv2 login --subscription-id <id>' to create one."

### Requirement: Profile show command
**Priority**: P1
**Rationale**: Users need to inspect profile details before switching or troubleshooting.

The system SHALL provide a `profile show` CLI command (`Get-CirrusProfile -Name <name>` in PowerShell) that displays all properties of a named profile.

CLI syntax: `azv2 profile show --name <name>`
PowerShell syntax: `Get-CirrusProfile -Name <name>`

If `--name` is omitted, the active profile SHALL be shown. If no active profile and no `--name`, the system SHALL display an error with guidance.

#### Scenario: Show named profile
**Test mapping**: `ShowProfileCommand_ShouldDisplayProfile_WhenProfileExists`

- **Given** a profile named "dev" exists with TenantId "t1", SubscriptionId "s1", and Defaults {"location": "eastus2"}
- **When** the user runs `azv2 profile show --name dev`
- **Then** the output SHALL display all profile properties including the Defaults dictionary

#### Scenario: Show active profile when name omitted
**Test mapping**: `ShowProfileCommand_ShouldShowActiveProfile_WhenNameOmitted`

- **Given** "dev" is the active profile
- **When** the user runs `azv2 profile show`
- **Then** the output SHALL display the "dev" profile details

#### Scenario: Show non-existent profile
**Test mapping**: `ShowProfileCommand_ShouldError_WhenProfileNotFound`

- **Given** no profile named "staging" exists
- **When** the user runs `azv2 profile show --name staging`
- **Then** the system SHALL display an error: "Profile 'staging' not found."
- **And** the error SHALL include available profile names

### Requirement: Profile modify command
**Priority**: P1
**Rationale**: Users need to update profile properties (e.g., change subscription, add defaults) without recreating the profile.

The system SHALL provide a `profile modify` CLI command (`Set-CirrusProfile` in PowerShell) that updates individual properties of an existing profile.

CLI syntax: `azv2 profile modify --name <name> [--subscription-id <id>] [--tenant-id <id>] [--set <key>=<value>] [--remove-default <key>]`
PowerShell syntax: `Set-CirrusProfile -Name <name> [-SubscriptionId <id>] [-TenantId <id>] [-Set @{key="value"}] [-RemoveDefault <key>]`

Only provided parameters SHALL be updated; omitted parameters SHALL be preserved.

#### Scenario: Modify subscription
**Test mapping**: `ModifyProfileCommand_ShouldUpdateSubscription_WhenProvided`

- **Given** a profile named "dev" exists with SubscriptionId "old-sub"
- **When** the user runs `azv2 profile modify --name dev --subscription-id new-sub`
- **Then** the profile's SubscriptionId SHALL be updated to "new-sub"
- **And** all other properties SHALL be unchanged

#### Scenario: Add a default
**Test mapping**: `ModifyProfileCommand_ShouldAddDefault_WhenSetProvided`

- **Given** a profile named "dev" exists with no defaults
- **When** the user runs `azv2 profile modify --name dev --set location=eastus2`
- **Then** the profile's Defaults dictionary SHALL contain {"location": "eastus2"}

#### Scenario: Modify non-existent profile
**Test mapping**: `ModifyProfileCommand_ShouldError_WhenProfileNotFound`

- **Given** no profile named "staging" exists
- **When** the user runs `azv2 profile modify --name staging --subscription-id s1`
- **Then** the system SHALL display an error: "Profile 'staging' not found."

### Requirement: Profile remove command
**Priority**: P1
**Rationale**: Users must be able to clean up stale or unwanted profiles.

The system SHALL provide a `profile remove` CLI command (`Remove-CirrusProfile` in PowerShell) that deletes a named profile.

CLI syntax: `azv2 profile remove --name <name>`
PowerShell syntax: `Remove-CirrusProfile -Name <name>`

If the removed profile was the active profile, the active pointer SHALL be cleared. The system SHALL confirm before removing (PowerShell `ShouldProcess`; CLI prompts unless `--yes` is provided).

#### Scenario: Remove existing profile
**Test mapping**: `RemoveProfileCommand_ShouldDeleteProfile_WhenConfirmed`

- **Given** a profile named "dev" exists
- **When** the user runs `azv2 profile remove --name dev --yes`
- **Then** the profile SHALL be removed from `profiles.json`
- **And** if "dev" was the active profile, the active pointer SHALL be cleared

#### Scenario: Remove non-existent profile
**Test mapping**: `RemoveProfileCommand_ShouldError_WhenProfileNotFound`

- **Given** no profile named "staging" exists
- **When** the user runs `azv2 profile remove --name staging`
- **Then** the system SHALL display an error: "Profile 'staging' not found."

### Requirement: DI registration for profile services
**Priority**: P0
**Rationale**: The profile service must be accessible via the DI container following existing Cirrus conventions.

`IProfileService` SHALL be registered as a Singleton in `Core.Runtime.DIRegisterExtensions.RegisterProfileServices()`. The `Core.Engine.Bootstrapper.RegisterServices()` method SHALL call `services.RegisterProfileServices()`.

`ProfileService` SHALL receive `ILoggingService` and `ITelemetryService` via primary constructor injection (matching `AuthContextService` pattern).

#### Scenario: ProfileService resolved from DI container
**Test mapping**: `Bootstrapper_ShouldRegisterProfileService_AsSingleton`

- **Given** the application starts
- **When** `CirrusServiceProvider.GetRequiredService<IProfileService>()` is called
- **Then** the service SHALL return a `ProfileService` instance
- **And** the same instance SHALL be returned on subsequent calls (Singleton)

### Requirement: Profiles.json migration from context.json
**Priority**: P1
**Rationale**: Existing users with `context.json` should not lose their auth context when upgrading to the profile system.

When `ProfileService` loads for the first time and `profiles.json` does not exist but `context.json` does exist, the service SHALL:
1. Read the existing `AuthContext` from `context.json`
2. Create a `"default"` profile from the `AuthContext` (TenantId, SubscriptionId)
3. Set the `"default"` profile as active
4. Write `profiles.json`
5. Not delete `context.json` (leave for backward compatibility until adapter is removed)

#### Scenario: Automatic migration from context.json
**Test mapping**: `ProfileService_ShouldMigrateFromContextJson_WhenProfilesJsonMissing`

- **Given** `context.json` exists with TenantId "t1" and SubscriptionId "s1"
- **And** `profiles.json` does not exist
- **When** `ProfileService` initializes and `GetActiveProfileAsync()` is called
- **Then** the service SHALL create a "default" profile with TenantId "t1" and SubscriptionId "s1"
- **And** the "default" profile SHALL be set as active
- **And** `profiles.json` SHALL be written to disk

#### Scenario: No migration when profiles.json already exists
**Test mapping**: `ProfileService_ShouldNotMigrate_WhenProfilesJsonExists`

- **Given** both `profiles.json` and `context.json` exist
- **When** `ProfileService` initializes
- **Then** the service SHALL load from `profiles.json` only
- **And** `context.json` SHALL be ignored
