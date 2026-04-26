## MODIFIED Requirements

### Requirement: REQ-CTX-01: Single Context Model
**Priority**: P0
**Rationale**: The system transitions from a single persisted context to a profile-derived transient context. `AuthContext` is no longer the persisted source of truth — `Profile` is. `AuthContext` remains as the transient execution record passed to commands via `ICommandContext`.

The system SHALL derive a transient `Core.Abstractions.AuthContext` from the resolved `Core.Abstractions.Profile` for each command invocation. The `AuthContext` record (TenantId + SubscriptionId) SHALL NOT be persisted independently — the `Profile` in `profiles.json` is the source of truth.

Multiple named profiles MAY exist simultaneously in `profiles.json`. At most one profile SHALL be the "active" profile at any time. The transient `AuthContext` for a command invocation is derived from either the `--profile` override or the active profile.

#### Scenario: CTX-LOGIN-01 — Successful interactive login
**Test mapping**: `LoginCommand_ShouldCreateProfileAndDeriveContext_WhenInteractiveLoginSucceeds`

- **Given** no profiles exist
- **And** the user has valid Azure credentials
- **When** the user runs `azv2 login --subscription-id <sub-id> --tenant-id <tenant-id>`
- **Then** LoginCommand MUST call `IAuthService.LoginAsync` with the provided tenant and subscription
- **And** LoginCommand MUST create a `Profile` named "default" with TenantId from `AuthResult` and SubscriptionId from input
- **And** LoginCommand MUST call `IProfileService.SaveProfileAsync` and `SetActiveProfileAsync` to persist and activate the profile
- **And** LoginCommand MUST also call `IAuthContextService.SaveContextAsync` for backward compatibility during migration
- **And** the `AuthContext` MUST be written to the command output

#### Scenario: CTX-LOGIN-03 — Login overwrites existing context
**Test mapping**: `LoginCommand_ShouldOverwriteDefaultProfile_WhenProfileAlreadyExists`

- **Given** a "default" profile exists with TenantId "old-tenant" and SubscriptionId "old-sub"
- **When** the user runs `azv2 login --subscription-id new-sub --tenant-id new-tenant`
- **Then** the "default" profile MUST be overwritten with the new TenantId and SubscriptionId
- **And** `profiles.json` MUST be updated
- **And** `context.json` MUST also be updated for backward compatibility

### Requirement: REQ-CTX-02: Context Persistence
**Priority**: P0
**Rationale**: `AuthContext` is no longer independently persisted. Profile data in `profiles.json` is the canonical persistence. `context.json` is maintained during migration only.

`Core.Authentication.AuthContextService` SHALL delegate to `IProfileService` internally. When `SaveContextAsync` is called, the adapter SHALL update the active profile in `profiles.json` AND write `context.json` for backward compatibility. When `GetCurrentContextAsync` is called, the adapter SHALL load the active profile from `IProfileService` and derive an `AuthContext` from it.

The `AuthContextService` MUST continue to implement `IAuthContextService` with unchanged method signatures for backward compatibility during migration.

#### Scenario: AuthContextService adapter reads from profiles
**Test mapping**: `AuthContextService_ShouldDelegateToProfileService_WhenGetCurrentContextCalled`

- **Given** an active profile "dev" exists with TenantId "t1" and SubscriptionId "s1"
- **When** `IAuthContextService.GetCurrentContextAsync()` is called
- **Then** the adapter SHALL call `IProfileService.GetActiveProfileAsync()`
- **And** the adapter SHALL return `AuthContext(TenantId: "t1", SubscriptionId: "s1")`

#### Scenario: AuthContextService adapter writes to profiles
**Test mapping**: `AuthContextService_ShouldUpdateActiveProfile_WhenSaveContextAsyncCalled`

- **Given** an active profile "default" exists
- **When** `IAuthContextService.SaveContextAsync(new AuthContext("t2", "s2"))` is called
- **Then** the adapter SHALL update the active profile's TenantId and SubscriptionId
- **And** the adapter SHALL also write `context.json` for backward compatibility

### Requirement: REQ-CTX-03: Context Loading
**Priority**: P0
**Rationale**: Context loading now resolves through the profile system. The engine no longer reads `context.json` directly.

On every call to `GetCurrentContextAsync()`, the `AuthContextService` adapter MUST resolve the active profile from `IProfileService.GetActiveProfileAsync()` and derive the `AuthContext`. The adapter MUST NOT read `context.json` directly (except as a fallback during migration if `profiles.json` does not exist).

#### Scenario: Context loaded from active profile
**Test mapping**: `AuthContextService_ShouldLoadFromProfile_WhenProfilesJsonExists`

- **Given** `profiles.json` exists with active profile "dev" (TenantId "t1", SubscriptionId "s1")
- **When** `GetCurrentContextAsync()` is called
- **Then** the adapter MUST return `AuthContext(TenantId: "t1", SubscriptionId: "s1")`
- **And** `context.json` MUST NOT be read

#### Scenario: Context loaded from context.json during migration
**Test mapping**: `AuthContextService_ShouldFallbackToContextJson_WhenProfilesJsonMissing`

- **Given** `profiles.json` does not exist
- **And** `context.json` exists with TenantId "t1" and SubscriptionId "s1"
- **When** `GetCurrentContextAsync()` is called
- **Then** the adapter MUST trigger profile migration (create "default" profile from context.json)
- **And** the adapter MUST return `AuthContext(TenantId: "t1", SubscriptionId: "s1")`

### Requirement: REQ-CTX-04: Context Clearing
**Priority**: P0
**Rationale**: Logout no longer deletes `context.json` or clears the profile definition. It clears MSAL tokens only. Profile definitions persist across logout.

`ClearContextAsync()` on the `AuthContextService` adapter SHALL clear the MSAL tokens (via `IAuthService.LogoutAsync`) but SHALL NOT remove the active profile from `profiles.json`. The adapter MUST delete `context.json` for backward compatibility during migration but MUST NOT clear the active profile pointer.

#### Scenario: ClearContext clears tokens but preserves profile
**Test mapping**: `AuthContextService_ShouldPreserveProfile_WhenClearContextAsyncCalled`

- **Given** an active profile "dev" exists
- **When** `ClearContextAsync()` is called
- **Then** `context.json` MUST be deleted (if it exists)
- **And** the "dev" profile MUST remain in `profiles.json`
- **And** the active profile pointer MUST remain "dev"
- **And** MSAL token clearing is NOT the adapter's responsibility (handled by `LogoutCommand` directly)

### Requirement: REQ-CTX-05: Empty Context Default
**Priority**: P0
**Rationale**: When no profile is available, the engine must either provide a clear error or fall back to empty context depending on the code path.

When `Engine.CreateCommandContext()` resolves no profile (no `--profile` flag and no active profile), the engine SHALL throw an `InvalidOperationException` with actionable guidance: "No active profile. Run 'azv2 login --subscription-id <id>' or 'azv2 profile switch <name>' to set one."

The `AuthContextService` adapter's `GetCurrentContextAsync()` MAY still return `null` when no profile exists, to maintain interface compatibility. Callers that use the adapter directly (legacy code) SHALL handle `null` as before.

#### Scenario: Engine throws when no profile available
**Test mapping**: `Engine_ShouldThrowWithGuidance_WhenNoProfileAvailable`

- **Given** no `--profile` flag is provided
- **And** no active profile is set
- **When** `Engine.CreateCommandContext()` is called
- **Then** the engine MUST throw an `InvalidOperationException`
- **And** the message MUST include actionable guidance

#### Scenario: Adapter returns null when no profile
**Test mapping**: `AuthContextService_ShouldReturnNull_WhenNoActiveProfile`

- **Given** no profiles exist and no `context.json` exists
- **When** `GetCurrentContextAsync()` is called on the adapter
- **Then** the adapter MUST return `null`

### Requirement: REQ-CTX-06: Context Isolation per Invocation
**Priority**: P1
**Rationale**: Each command invocation must still receive an isolated context copy. Profile resolution adds a new derivation step but the isolation guarantee is unchanged.

`Engine.CreateCommandContext()` MUST resolve the profile, derive `AuthContext(TenantId, SubscriptionId)` from it, and create a defensive copy using `authContext with { }` before assigning to `Core.Runtime.Handler.CommandAuthHandler.AuthContext`. The profile object itself SHALL NOT be passed to commands — only the derived `AuthContext`.

#### Scenario: Context derived and isolated from profile
**Test mapping**: `Engine_ShouldDeriveIsolatedAuthContext_WhenResolvingProfile`

- **Given** the active profile "dev" has TenantId "t1" and SubscriptionId "s1"
- **When** two commands execute concurrently
- **Then** each command SHALL receive its own `AuthContext` copy
- **And** modifications to one command's `AuthContext` SHALL NOT affect the other
- **And** modifications SHALL NOT affect the source `Profile` in `IProfileService`
