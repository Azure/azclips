## 1. Core Model & Interface

- [x] 1.1 Add `AuthMethod` enum (Interactive, DeviceCode, ServicePrincipal, ManagedIdentity) to `Core.Abstractions`
- [x] 1.2 Add `Profile` record to `Core.Abstractions` with all fields: Name, TenantId, SubscriptionId, CloudName, AuthMethod, DefaultLocation, DefaultResourceGroup, UserPrincipalName, ClientId. All fields use language `default` values when not explicitly set. CloudName is a stored property with no behavioral impact.
- [x] 1.3 Add `IProfileService` interface to `Core.Abstractions` with methods: GetAllProfilesAsync, GetProfileAsync, SaveProfileAsync, RemoveProfileAsync, GetActiveProfileNameAsync, SetActiveProfileNameAsync
- [x] 1.4 Add profile name validation: 1–100 characters, alphanumeric plus `-`, `_`, `.`; reject `"default"` from user-facing creation paths
- [x] 1.5 Remove the `AuthContext` record (`Core.Abstractions/Models/AuthContext.cs`) and `IAuthContextService` (`Core.Abstractions/IAuthContextService.cs`)
- [x] 1.6 Extend `AuthResult` (`Core.Abstractions/Models/AuthResult.cs`) to surface the authenticated account's user principal name (from MSAL `IAccount.Username`) and update `MsalAuthService` to populate it, so `LoginProcessor` can set `Profile.UserPrincipalName`

## 2. Profile Service Implementation

- [x] 2.1 Implement `ProfileService` in `Core.Runtime` with JSON file persistence (`{LocalApplicationData}/.azclips/profiles.json` for profiles, `{LocalApplicationData}/.azclips/active-profile` for active pointer). All file writes use atomic temp-file-plus-rename.
- [x] 2.2 Register `ProfileService` as singleton in `Core.Runtime` DI registration (`DIRegisterExtensions.RegisterRuntimeServices`)
- [x] 2.3 Remove `AuthContextService` (`Core.Authentication/AuthContextService.cs`) and its DI registration in `Core.Authentication/DIRegisterExtensions.cs`

## 3. Engine Integration

- [x] 3.1 Add nullable `Profile?` property to `ProcessorContext` record in `Core.Abstractions/Models/ProcessorContext.cs`
- [x] 3.2 Restructure `ExecutionSession.GetProcessor()` to add a global-parameter pre-binding pass: resolve command metadata, then extract and normalize the global `--profile`/`-Profile` value from `CommandInvocation.Parameters` (using `commandMetadata.ParameterAliases`) **before** calling `CreateProcessorContext()`. Bind the remaining command-specific parameters after the processor is instantiated. (Note: `CreateProcessorContext()` currently runs before `BindProcessor()` and the context is injected via `Init()`, so the global value must be resolved in this pre-binding pass.)
- [x] 3.3 Modify `Engine.CreateProcessorContext()` to accept the pre-bound `--profile` value and resolve the profile: `--profile` value → fall back to active profile via `IProfileService` → null if neither exists. Replace the `IAuthContextService` constructor dependency on `Engine` with `IProfileService`, and populate `AuthHandler` from the resolved `Profile`.
- [x] 3.4 Replace `AuthContext` on `IAuthHandler`/`AuthHandler` with a nullable `Profile?`: read `Profile.TenantId` (and `Profile.SubscriptionId` where needed) for token acquisition; when `Profile` is null, acquire no token so profile-requiring commands fail at point of use.

## 4. Login Processor Changes

- [x] 4.1 Add `--out-profile` / `OutProfile` parameter to `LoginProcessor`
- [x] 4.2 Modify `LoginProcessor.ProcessAsync()` to build a `Profile` from the login inputs (tenant, subscription, cloud, `AuthMethod` derived from flags — Interactive by default, DeviceCode with `--use-device-code`; `UserPrincipalName` from the auth result), persist it via `IProfileService`, set it active, and **return the `Profile`** as output (replacing the removed `AuthContext` output). Target name: `--out-profile` if provided, otherwise `"default"`.
- [x] 4.3 Implement the `--profile` re-authentication path: when `--profile` is explicitly provided, source tenant/subscription/cloud from the named existing profile, run the auth flow, overwrite + activate it, and return it; error clearly if the profile does not exist.
- [x] 4.4 Enforce mutual exclusivity: reject `--profile` + `--out-profile` together (fail fast before authentication) and reject `"default"` as an `--out-profile` value. Use the bound `--profile`/`--out-profile` parameter values (not the resolved `ProcessorContext.Profile`) to distinguish explicit input from the active fallback.
- [x] 4.5 Modify `LogoutProcessor.ProcessAsync()` to sign out the active profile: when an active profile exists, remove its associated account from the shared MSAL token cache (via `IAuthService.LogoutAsync(profile.UserPrincipalName)`) and clear the active-profile pointer (`SetActiveProfileNameAsync(null)`); retain the profile definition. When no profile is active, make no changes. Narrow `IAuthService.LogoutAsync` to remove only the account matching the supplied user principal name.
- [x] 4.6 Update Login `ExtensionIndex.cs` with `--out-profile` / `OutProfile` and `--profile` / `Profile` parameter metadata for both CLI and PS tables

## 5. Profile Processors

- [x] 5.1 Add `ProfileProcessorBase.cs` in Login extension (similar to `LoginProcessorBase`)
- [x] 5.2 Implement `ProfileListProcessor` — list all profiles, indicate active
- [x] 5.3 Implement `ProfileShowProcessor` — show full details of a single profile by name
- [x] 5.4 Implement `ProfileSetProcessor` — set active profile (validate exists, no re-auth)
- [x] 5.5 Implement `ProfileRemoveProcessor` — delete profile, handle active removal (clear active pointer — no fallback to temp)
- [x] 5.6 Register all profile processors in Login `ExtensionIndex.cs` (CLI table under "profile" group, PS table with AzProfile noun)

## 6. Per-Processor --profile Override & Profile in Extension Bases

- [x] 6.1 Update `ResourcesProcessorBase`: replace the `AuthContext` field with nullable `Profile?` sourced from `ProcessorContext`, propagate it through `CreateExtendedProcessor<T>`, resolve `SubscriptionId` from `Profile.SubscriptionId`, and add the `--profile` / `Profile` parameter + resource processor metadata in `ExtensionIndex`.
- [x] 6.2 Update `ComputeProcessorBase`: same changes as 6.1 (replace `AuthContext` with nullable `Profile?`, propagate via `CreateExtendedProcessor<T>`, add `--profile` / `Profile` parameter + compute processor metadata in `ExtensionIndex`).
- [x] 6.3 Add `--profile` / `Profile` parameter to `LoginProcessorBase` and login processor metadata in `ExtensionIndex` (re-auth semantics per section 4).
- [x] 6.4 Document the `--profile` parameter pattern for future processor authors and code generation.

## 7. Remove Legacy `show` / `Get-AzContext`

- [x] 7.1 Delete `GetContextProcessor.cs` and remove its CLI (`login show`) and PS (`Get-AzContext`) entries from Login `ExtensionIndex.cs`.
- [x] 7.2 Delete PS `GetAzContextCmdlet.cs`, remove `Get-AzContext` from `Login.psd1` `CmdletsToExport`, and delete `help/Get-AzContext.md` and `exports/Get-AzContext.ps1`.

## 8. PowerShell Integration

- [x] 8.1 Add PowerShell cmdlets for profile processors using the `OutputStreams` / `engine.OpenExecutionSession()` pattern (Get-AzclipsProfile, Set-AzclipsProfile, Remove-AzclipsProfile)
- [x] 8.2 Update Login module manifest (`Login.psd1`) with new exported cmdlets and add `ModuleInitializer` registration if needed
- [x] 8.3 Add `-Profile` parameter to Resource, Compute, and Login PowerShell cmdlets (same resolution logic as CLI `--profile`)

## 9. Tests

- [x] 9.1 Unit tests for `ProfileService`: CRUD operations, active profile management, persistence, atomic writes, profile name validation
- [x] 9.2 Unit tests for profile processors: list, show, set, remove (including edge cases like remove-active clears pointer)
- [x] 9.3 Unit tests for login integration: --out-profile, default auto-create, default is reserved, --profile re-auth, --profile + --out-profile fails fast, login returns the Profile
- [x] 9.4 Integration tests for profile resolution order in Engine: --profile flag → active → null (error at point of use)
- [x] 9.5 Tests for first-run/bootstrap scenarios: login succeeds with no active profile, profile list returns empty with no profiles, resource and compute commands fail clearly with null profile
- [x] 9.6 Update or replace tests that referenced the removed `AuthContext` path (e.g., `StubAuthContextService`, benchmark bases, `ConfigCliTests`) to use `Profile` / `IProfileService`

## 10. Post-implementation Refinements

- [x] 10.1 Rename `AuthMethod.Interactive` to `Browser` and add a new `Broker` value; update all usages (`LoginProcessor` default, stubs, tests) and the `profile-crud` spec enum list
- [x] 10.2 Promote `GetActiveProfileAsync` from an extension method to a native `IProfileService` member implemented in `ProfileService` under a single lock; delete `ProfileServiceExtensions.cs`
- [x] 10.3 Add native `SaveAndActivateProfileAsync(profile)` to `IProfileService`/`ProfileService` (persist + activate under one lock) and use it from `LoginProcessor`; update stubs
- [x] 10.4 Add `--active` / `-Active` switch to `ProfileShowProcessor` (show the active profile); make `Name` optional, enforce mutual exclusivity and "exactly one of Name/--active" with clear errors; update CLI + PS metadata in `ExtensionIndex` and the `Get-AzProfile` cmdlet (new `ShowActive` parameter set)
- [x] 10.5 Add tests: `GetActiveProfileAsync` (active/none/dangling), `SaveAndActivateProfileAsync`, and `profile show --active` (success, no-active, name+active conflict, neither provided)
