## Why

Cirrus currently uses a single-context model (`AuthContext` with one tenant + subscription persisted to `context.json`). This works for basic "login, run one command" workflows but breaks down for enterprise users who manage multiple tenants, subscriptions, and environments daily. Existing tools (Azure CLI, Azure PowerShell, AWS CLI) have accumulated years of pain from bolting profiles onto a system designed for single-context — token cache corruption under concurrent use, confusing credential precedence, fragile multi-tenant switching. Cirrus has the opportunity to design profiles as a first-class "connection context" from day one, before the single-context model becomes entrenched.

## What Changes

- Introduce a `Profile` model containing complete connection state: name, tenant ID, subscription ID, cloud name, auth method, optional extensible defaults (location, resource group), user principal name, and client ID.
- Add `IProfileService` / `ProfileService` in Core for profile CRUD and active-profile management, persisting to `~/.cirrus/profiles.json` with atomic writes (write-to-temp + rename).
- Modify the `login` command to accept `--out-profile <name>`. Bare login (no flag) auto-creates/overwrites a disposable `"temp"` profile.
- Modify `logout` to accept `--profile <name>` to clear the MSAL token for a specific profile without changing profile definitions.
- Add new `profile` command group: `list`, `show`, `switch`, `modify`, `remove`.
- Add a `--profile <name>` engine-level parameter (handled by `Engine.CreateCommandContext()`, not per-command) that resolves a named profile for a single execution without changing the active pointer.
- Modify `Engine.CreateCommandContext()` to resolve the active profile (or `--profile` override), derive a transient `AuthContext` from it, and inject it into the command pipeline.
- `IAuthContextService` is preserved as an internal adapter during migration; `AuthContext` becomes a transient execution record derived from the active profile.
- Error when no profile exists must include actionable guidance (e.g., "No active profile. Run `azv2 login` or `azv2 profile switch <name>`").
- `--out-profile` collision must overwrite with warning.

## Capabilities

### New Capabilities

- `profile-crud`: Create, read, update, and delete named profiles. Includes the `Profile` model, `IProfileService`, persistence to `~/.cirrus/profiles.json` with atomic writes, active-profile pointer management, and extensible defaults dictionary.
- `profile-switching`: Switch the active profile persistently (`profile switch`) or per-command (`--profile` engine-level override). Includes profile resolution order: `--profile` flag → active profile → error with actionable guidance.
- `login-profile-integration`: Modify login/logout commands to create profiles on login (`--out-profile` or auto-`temp`) and target specific profiles on logout (`--profile`). Profiles persist identity context; tokens remain managed by MSAL.

### Modified Capabilities

- `auth-context`: `AuthContext` transitions from a persisted/cached model to a transient execution record derived from the active profile. `IAuthContextService` is retained as an internal adapter but no longer the source of truth. `Engine.CreateCommandContext()` changes its resolution path from `IAuthContextService → context.json` to `IProfileService → active profile → transient AuthContext`.

## Impact

- **Core.Abstractions**: New `Profile` model, `IProfileService` interface. `AuthContext` retained but semantics change (transient, not persisted). `IAuthContextService` marked for deprecation.
- **Core.Runtime** (or new Core.Profile project): `ProfileService` implementation with atomic file writes, profile validation, active-profile pointer.
- **Core.Engine**: `Engine.CreateCommandContext()` changes resolution from `IAuthContextService.GetCurrentContextAsync()` to `IProfileService.GetActiveProfileAsync()` → derive `AuthContext`. Must handle `--profile` override parameter at engine level before command dispatch.
- **Core.Authentication**: `AuthContextService` becomes a thin adapter over `IProfileService` for backward compatibility during migration. `MsalAuthService` unchanged — tokens still managed separately.
- **Login extension**: `LoginCommand` gains `--out-profile` / `OutProfile` parameter. `LogoutCommand` gains `--profile` / `Profile` parameter. New profile commands: `ListProfilesCommand`, `ShowProfileCommand`, `SwitchProfileCommand`, `ModifyProfileCommand`, `RemoveProfileCommand`. `ExtensionIndex` updated with new CLI/PS command tables.
- **PowerShell modules**: `Connect-CirrusAccount` gains `-OutProfile`. `Disconnect-CirrusAccount` gains `-Profile`. New cmdlets: `Get-CirrusProfile` (list/show), `Switch-CirrusProfile`, `Set-CirrusProfile` (modify), `Remove-CirrusProfile`. Module manifest updated.
- **Storage**: New `{LocalApplicationData}/.cirrus/profiles.json`. Existing `context.json` becomes obsolete (adapter reads from profiles.json).
- **DI**: `IProfileService` registered as Singleton. `IAuthContextService` registration changes to adapter implementation.
- **Tests**: New unit tests for `ProfileService`, profile commands, engine profile resolution. Existing `StubAuthContextService` updated or supplemented with `StubProfileService`.
