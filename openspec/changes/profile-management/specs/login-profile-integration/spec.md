## ADDED Requirements

### Requirement: Login creates profile via --out-profile
**Priority**: P0
**Rationale**: Login is the entry point for creating profiles. The `--out-profile` flag allows users to name the profile during login, while bare login auto-creates a default profile for backward compatibility.

`Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand` SHALL accept a new optional parameter `--out-profile <name>` (CLI) / `-OutProfile <name>` (PowerShell).

**When `--out-profile <name>` is provided:**
1. After successful MSAL authentication, the command SHALL create a `Profile` with the given name
2. The profile SHALL be populated with: TenantId (from `AuthResult`), SubscriptionId (from input), AuthMethod (from login flow used), UserPrincipalName (from `AuthResult` if available)
3. The command SHALL call `IProfileService.SaveProfileAsync(profile)` to persist the profile
4. The command SHALL call `IProfileService.SetActiveProfileAsync(name)` to make it the active profile
5. If a profile with the same name already exists, it SHALL be overwritten with a warning message

**When `--out-profile` is omitted (bare login):**
1. The command SHALL auto-create/overwrite a profile named `"default"`
2. The `"default"` profile SHALL be set as active
3. This provides backward compatibility — `azv2 login --subscription-id <id>` works identically to before, but now creates a profile

In both cases, the command SHALL also call `IAuthContextService.SaveContextAsync()` for backward compatibility during migration, so that `context.json` is updated alongside `profiles.json`.

#### Scenario: Login with --out-profile creates named profile
**Test mapping**: `LoginCommand_ShouldCreateNamedProfile_WhenOutProfileProvided`

- **Given** no profiles exist
- **When** the user runs `azv2 login --subscription-id s1 --tenant-id t1 --out-profile dev`
- **Then** MSAL authentication SHALL proceed as normal
- **And** a profile named "dev" SHALL be created with TenantId from AuthResult, SubscriptionId "s1"
- **And** the profile's AuthMethod SHALL reflect the login flow used (e.g., "interactive")
- **And** "dev" SHALL be set as the active profile
- **And** the `AuthContext` SHALL be written to the command output

#### Scenario: Bare login creates default profile
**Test mapping**: `LoginCommand_ShouldCreateDefaultProfile_WhenOutProfileOmitted`

- **Given** no profiles exist
- **When** the user runs `azv2 login --subscription-id s1 --tenant-id t1`
- **Then** a profile named "default" SHALL be created
- **And** "default" SHALL be set as the active profile
- **And** behavior SHALL be identical to current login (backward compatible)

#### Scenario: --out-profile overwrites existing profile with warning
**Test mapping**: `LoginCommand_ShouldOverwriteProfile_WhenOutProfileCollision`

- **Given** a profile named "dev" already exists with SubscriptionId "old-sub"
- **When** the user runs `azv2 login --subscription-id new-sub --out-profile dev`
- **Then** the "dev" profile SHALL be overwritten with SubscriptionId "new-sub"
- **And** the command output SHALL include a warning: "Profile 'dev' already exists and will be overwritten."
- **And** "dev" SHALL remain the active profile

#### Scenario: PowerShell Connect-CirrusAccount with -OutProfile
**Test mapping**: `ConnectCirrusAccount_ShouldCreateProfile_WhenOutProfileProvided`

- **Given** the `Cirrus.Login` PowerShell module is imported
- **When** the user runs `Connect-CirrusAccount -SubscriptionId s1 -TenantId t1 -OutProfile dev`
- **Then** the PS wrapper SHALL include `OutProfile` in the `CommandInvocation` parameters
- **And** a profile named "dev" SHALL be created via `IProfileService`
- **And** output SHALL be written via `$PSCmdlet.WriteObject` on the runspace thread

### Requirement: Logout with profile targeting
**Priority**: P0
**Rationale**: Users must be able to clear MSAL tokens for a specific profile's identity without affecting other profiles or removing the profile definition itself.

`Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand` SHALL accept a new optional parameter `--profile <name>` (CLI) / `-Profile <name>` (PowerShell).

**When `--profile <name>` is provided:**
1. The command SHALL resolve the named profile via `IProfileService.GetProfileAsync(name)`
2. The command SHALL clear the MSAL token cache for the profile's identity (using TenantId and UserPrincipalName from the profile)
3. The profile definition SHALL NOT be removed from `profiles.json`
4. If the profile does not exist, the system SHALL display an error

**When `--profile` is omitted:**
1. The command SHALL use the active profile
2. If no active profile exists, the command SHALL clear all MSAL tokens (current behavior)
3. The active profile pointer SHALL NOT be cleared — the profile definition persists, only tokens are removed

This is a departure from the current behavior where `LogoutCommand` calls `IAuthContextService.ClearContextAsync()` which deletes `context.json`. With profiles, logout clears tokens but preserves profile definitions.

#### Scenario: Logout clears tokens for active profile
**Test mapping**: `LogoutCommand_ShouldClearTokens_WhenNoProfileFlagProvided`

- **Given** "dev" is the active profile with TenantId "t1"
- **And** the MSAL token cache contains tokens for tenant "t1"
- **When** the user runs `azv2 login logout`
- **Then** `IAuthService.LogoutAsync()` SHALL be called to clear tokens
- **And** the "dev" profile definition SHALL remain in `profiles.json`
- **And** the active profile pointer SHALL remain "dev"

#### Scenario: Logout targets specific profile
**Test mapping**: `LogoutCommand_ShouldClearTokensForProfile_WhenProfileFlagProvided`

- **Given** profiles "dev" (active) and "prod" exist
- **And** the MSAL token cache contains tokens for "prod"'s tenant
- **When** the user runs `azv2 login logout --profile prod`
- **Then** tokens for the "prod" profile's identity SHALL be cleared
- **And** the "prod" profile definition SHALL remain in `profiles.json`
- **And** the active profile SHALL remain "dev"

#### Scenario: Logout with non-existent profile
**Test mapping**: `LogoutCommand_ShouldError_WhenProfileNotFound`

- **Given** no profile named "staging" exists
- **When** the user runs `azv2 login logout --profile staging`
- **Then** the system SHALL display an error: "Profile 'staging' not found."
- **And** no tokens SHALL be cleared

#### Scenario: PowerShell Disconnect-CirrusAccount with -Profile
**Test mapping**: `DisconnectCirrusAccount_ShouldClearTokensForProfile_WhenProfileProvided`

- **Given** the `Cirrus.Login` PowerShell module is imported
- **And** profiles "dev" and "prod" exist
- **When** the user runs `Disconnect-CirrusAccount -Profile prod`
- **Then** the PS wrapper SHALL include `Profile` in the `CommandInvocation` parameters
- **And** tokens for the "prod" profile's identity SHALL be cleared
- **And** the "prod" profile definition SHALL remain

### Requirement: Login extension command registration for profiles
**Priority**: P0
**Rationale**: New profile commands must be registered in the Login extension's `ExtensionIndex` for both CLI and PowerShell dispatch tables.

`Microsoft.Azure.Cirrus.Extensions.Login.ExtensionIndex` SHALL be updated with the following new command registrations:

**CLI dispatch table additions:**
| Command Key | Maps To | Parameters |
|---|---|---|
| `"list"` (under `profile` group) | `ListProfilesCommand` | (none) |
| `"show"` (under `profile` group) | `ShowProfileCommand` | `--name` (optional) |
| `"switch"` (under `profile` group) | `SwitchProfileCommand` | `--name` (required) |
| `"modify"` (under `profile` group) | `ModifyProfileCommand` | `--name` (required), `--subscription-id`, `--tenant-id`, `--set`, `--remove-default` |
| `"remove"` (under `profile` group) | `RemoveProfileCommand` | `--name` (required), `--yes` |

**PowerShell dispatch table additions:**
| Command Key | Maps To |
|---|---|
| `Get-CirrusProfile` | `ListProfilesCommand` / `ShowProfileCommand` (overloaded by parameter set) |
| `Switch-CirrusProfile` | `SwitchProfileCommand` |
| `Set-CirrusProfile` | `ModifyProfileCommand` |
| `Remove-CirrusProfile` | `RemoveProfileCommand` |

**Modified CLI entries:**
| Command Key | Change |
|---|---|
| `""` (LoginCommand) | Add `--out-profile` parameter metadata |
| `"logout"` (LogoutCommand) | Add `--profile` parameter metadata |

**Modified PowerShell entries:**
| Command Key | Change |
|---|---|
| `Connect-CirrusAccount` | Add `OutProfile` parameter metadata |
| `Disconnect-CirrusAccount` | Add `Profile` parameter metadata |

#### Scenario: Profile commands discoverable via CLI
**Test mapping**: `ExtensionIndex_ShouldReturnProfileCommands_WhenQueriedForCLI`

- **Given** the Login extension is loaded
- **When** `GetAllCommands(ClientType.CLI)` is called
- **Then** the result SHALL include metadata for list, show, switch, modify, and remove profile commands
- **And** each command SHALL have correct parameter metadata with kebab-case names

#### Scenario: Profile commands discoverable via PowerShell
**Test mapping**: `ExtensionIndex_ShouldReturnProfileCmdlets_WhenQueriedForPS`

- **Given** the Login extension is loaded
- **When** `GetAllCommands(ClientType.PowerShell)` is called
- **Then** the result SHALL include metadata for Get-CirrusProfile, Switch-CirrusProfile, Set-CirrusProfile, and Remove-CirrusProfile

### Requirement: PowerShell module manifest updated for profile cmdlets
**Priority**: P1
**Rationale**: New PowerShell cmdlet functions must be exported from the module manifest for discoverability and tab-completion.

`src/PS/Login/Login.psd1` SHALL be updated to add new functions to `FunctionsToExport`:
- `Get-CirrusProfile`
- `Switch-CirrusProfile`
- `Set-CirrusProfile`
- `Remove-CirrusProfile`

The existing exports (`Connect-CirrusAccount`, `Disconnect-CirrusAccount`, `Get-CirrusAccount`) SHALL remain unchanged.

#### Scenario: New cmdlets available after module import
**Test mapping**: `LoginModule_ShouldExportProfileCmdlets_WhenImported`

- **Given** the user runs `Import-Module Cirrus.Login`
- **When** `Get-Command -Module Cirrus.Login` is run
- **Then** the output SHALL include `Get-CirrusProfile`, `Switch-CirrusProfile`, `Set-CirrusProfile`, `Remove-CirrusProfile`
- **And** the existing cmdlets SHALL still be present
