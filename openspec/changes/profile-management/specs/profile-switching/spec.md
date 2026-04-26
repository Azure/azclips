## ADDED Requirements

### Requirement: Profile switch command
**Priority**: P0
**Rationale**: Users must be able to persistently change the active profile without re-authentication. This is the primary multi-tenant/multi-subscription workflow.

The system SHALL provide a `profile switch` CLI command (`Switch-CirrusProfile` in PowerShell) that changes the active profile pointer in `profiles.json`.

CLI syntax: `azv2 profile switch --name <name>`
PowerShell syntax: `Switch-CirrusProfile -Name <name>`

The command SHALL call `IProfileService.SetActiveProfileAsync(name)`. If the profile does not exist, the system SHALL display an error with actionable guidance listing available profiles.

The command SHALL NOT trigger re-authentication. Token validity is an orthogonal concern handled by `Core.Authentication.MsalAuthService.AcquireTokenSilentlyAsync`.

#### Scenario: Switch to existing profile
**Test mapping**: `SwitchProfileCommand_ShouldSetActiveProfile_WhenProfileExists`

- **Given** profiles "dev" and "prod" exist, with "dev" as the active profile
- **When** the user runs `azv2 profile switch --name prod`
- **Then** `IProfileService.SetActiveProfileAsync("prod")` SHALL be called
- **And** the active profile SHALL be "prod"
- **And** the output SHALL confirm: "Switched to profile 'prod'."

#### Scenario: Switch to non-existent profile
**Test mapping**: `SwitchProfileCommand_ShouldError_WhenProfileNotFound`

- **Given** no profile named "staging" exists
- **And** profiles "dev" and "prod" exist
- **When** the user runs `azv2 profile switch --name staging`
- **Then** the system SHALL display an error: "Profile 'staging' not found."
- **And** the error SHALL list available profiles: "Available profiles: dev, prod"
- **And** the active profile SHALL remain unchanged

#### Scenario: PowerShell profile switch
**Test mapping**: `SwitchCirrusProfile_ShouldSetActiveProfile_WhenProfileExists`

- **Given** profiles "dev" and "prod" exist, with "dev" as the active profile
- **And** the `Cirrus.Login` PowerShell module is imported
- **When** the user runs `Switch-CirrusProfile -Name prod`
- **Then** the PS wrapper SHALL construct a `CommandInvocation` for extension "Login", command "Switch-CirrusProfile"
- **And** `Engine.Process` SHALL execute synchronously on the runspace thread
- **And** the active profile SHALL be "prod"

### Requirement: Per-command profile override
**Priority**: P0
**Rationale**: Users need to execute a single command against a non-active profile without switching context. This prevents the error-prone pattern of `switch → run → switch back`.

The system SHALL support a `--profile <name>` parameter (CLI) / `-Profile <name>` parameter (PowerShell) on every command. This parameter SHALL be handled at the engine level (`Core.Engine.Engine.CreateCommandContext()`), not per-command.

When `--profile` is provided:
1. `Engine.CreateCommandContext()` SHALL resolve the named profile via `IProfileService.GetProfileAsync(name)`
2. The resolved profile SHALL be used to derive the transient `AuthContext` for this invocation only
3. The active profile pointer in `profiles.json` SHALL NOT be modified
4. If the named profile does not exist, the engine SHALL throw an `InvalidOperationException` before command dispatch

For CLI: `ExtensionDispatcher` / `ExtensionParser` SHALL strip `--profile <name>` from the argument array before passing to extension command parsing. The stripped profile name SHALL be stored on `CommandInvocation` or passed via a separate channel to `Engine`.

For PowerShell: Each cmdlet wrapper `.ps1` SHALL accept a `-Profile` parameter and include it in the `CommandInvocation` parameters dictionary with a reserved key (e.g., `__profile`).

#### Scenario: CLI command with profile override
**Test mapping**: `Engine_ShouldUseOverrideProfile_WhenProfileFlagProvided`

- **Given** profiles "dev" (active) and "prod" exist
- **And** "prod" has TenantId "t-prod" and SubscriptionId "s-prod"
- **When** the user runs `azv2 group list --profile prod`
- **Then** `Engine.CreateCommandContext()` SHALL resolve the "prod" profile
- **And** the `CommandAuthHandler.AuthContext` SHALL have TenantId "t-prod" and SubscriptionId "s-prod"
- **And** the active profile SHALL remain "dev"

#### Scenario: PowerShell command with profile override
**Test mapping**: `Engine_ShouldUseOverrideProfile_WhenPSProfileParamProvided`

- **Given** profiles "dev" (active) and "prod" exist
- **And** the `Cirrus.Login` PowerShell module is imported
- **When** the user runs `Get-CirrusResourceGroup -Profile prod`
- **Then** the PS wrapper SHALL include `-Profile` in the `CommandInvocation` parameters
- **And** `Engine.CreateCommandContext()` SHALL resolve the "prod" profile for this invocation
- **And** output SHALL be written via `$PSCmdlet.WriteObject` on the runspace thread
- **And** the active profile SHALL remain "dev"

#### Scenario: Profile override with non-existent profile
**Test mapping**: `Engine_ShouldThrow_WhenOverrideProfileNotFound`

- **Given** no profile named "staging" exists
- **When** the user runs `azv2 group list --profile staging`
- **Then** the engine SHALL throw an `InvalidOperationException` before command dispatch
- **And** the error message SHALL include: "Profile 'staging' not found."
- **And** the error SHALL list available profiles

#### Scenario: Profile override does not persist
**Test mapping**: `Engine_ShouldNotModifyActiveProfile_WhenOverrideUsed`

- **Given** "dev" is the active profile
- **When** the user runs `azv2 group list --profile prod`
- **And** the command completes successfully
- **Then** `GetActiveProfileAsync()` SHALL still return "dev"
- **And** `profiles.json` SHALL not have been modified

### Requirement: Profile resolution order
**Priority**: P0
**Rationale**: AWS CLI's biggest usability pain point is confusing, poorly-documented credential precedence. Cirrus must have a single, unambiguous resolution chain.

The profile resolution order SHALL be:
1. Explicit `--profile <name>` flag (CLI) or `-Profile <name>` parameter (PowerShell) — highest priority
2. Active profile from `profiles.json` (`activeProfile` field)
3. Error with actionable guidance — if neither (1) nor (2) resolves a profile

There SHALL be no environment variable override in MVP. If added in the future, it SHALL be documented as slotting between (1) and (2).

The resolution SHALL occur in `Core.Engine.Engine.CreateCommandContext()`. The resolved `Profile` SHALL be used to derive a transient `Core.Abstractions.AuthContext` (TenantId + SubscriptionId) which is assigned to `Core.Runtime.Handler.CommandAuthHandler.AuthContext`.

#### Scenario: Resolution with explicit flag
**Test mapping**: `Engine_ShouldResolveExplicitProfile_WhenFlagProvided`

- **Given** "dev" is the active profile
- **And** profiles "dev" and "prod" both exist
- **When** a command is executed with `--profile prod`
- **Then** the engine SHALL use the "prod" profile
- **And** the "dev" active profile SHALL be ignored for this invocation

#### Scenario: Resolution with active profile
**Test mapping**: `Engine_ShouldResolveActiveProfile_WhenNoFlagProvided`

- **Given** "dev" is the active profile
- **And** no `--profile` flag is provided
- **When** a command is executed
- **Then** the engine SHALL use the "dev" profile
- **And** the derived `AuthContext` SHALL have "dev"'s TenantId and SubscriptionId

#### Scenario: Resolution error when no profile available
**Test mapping**: `Engine_ShouldThrowWithGuidance_WhenNoProfileAvailable`

- **Given** no `--profile` flag is provided
- **And** no active profile is set (or `profiles.json` does not exist)
- **When** a command is executed
- **Then** the engine SHALL throw an `InvalidOperationException`
- **And** the error message SHALL include: "No active profile. Run 'azv2 login --subscription-id <id>' or 'azv2 profile switch <name>' to set one."

#### Scenario: Profile resolution in PowerShell preserves thread-affinity
**Test mapping**: `Engine_ShouldResolveProfileSynchronously_WhenCalledFromPSRunspace`

- **Given** a PowerShell cmdlet invokes `Engine.Process()` on the runspace thread
- **When** `CreateCommandContext()` resolves the profile
- **Then** profile resolution SHALL execute synchronously (sync-over-async via `GetAwaiter().GetResult()`) on the caller's thread
- **And** the `Channel<T>` output pump pattern SHALL be preserved
- **And** `$PSCmdlet.WriteObject` SHALL execute on the original runspace thread

### Requirement: Engine-level --profile parameter stripping (CLI)
**Priority**: P0
**Rationale**: The `--profile` parameter must be consumed by the engine and not passed through to extension command parsers, which would not recognize it and throw errors.

For CLI invocations, the `--profile <name>` flag SHALL be stripped from the `args` array before the `ExtensionDispatcher` parses the command. The stripped profile name SHALL be made available to `Engine.CreateCommandContext()`.

Implementation approach: `CommandInvocation` (or the CLI entry path in `Program.cs`) SHALL extract and remove `--profile` from the argument array, passing the profile name as metadata alongside the invocation.

#### Scenario: --profile stripped before extension parsing
**Test mapping**: `ExtensionDispatcher_ShouldNotSeeProfileFlag_WhenStrippedByEngine`

- **Given** the user runs `azv2 group list --profile prod`
- **When** the CLI entry point processes the arguments
- **Then** `--profile prod` SHALL be stripped from the args array
- **And** the `ExtensionDispatcher` SHALL receive `["group", "list"]` only
- **And** the profile name "prod" SHALL be available for `Engine.CreateCommandContext()`

#### Scenario: --profile at different argument positions
**Test mapping**: `CLIParser_ShouldStripProfile_WhenAtAnyPosition`

- **Given** the user runs `azv2 --profile prod group list` or `azv2 group list --profile prod` or `azv2 group --profile prod list`
- **When** the CLI entry point processes the arguments
- **Then** `--profile` and its value SHALL be stripped regardless of position
- **And** the remaining arguments SHALL be in correct order for extension parsing
