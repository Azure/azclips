## 1. Model & Abstractions

- [x] 1.1 Create `Profile` class in `src/Core/Core.Abstractions/Models/Profile.cs` with Name, TenantId, SubscriptionId, CloudName, AuthMethod, UserPrincipalName, ClientId, Defaults dictionary, and name validation logic (REQ: Profile data model)
- [x] 1.2 Create `IProfileService` interface in `src/Core/Core.Abstractions/IProfileService.cs` with GetAllProfilesAsync, GetProfileAsync, GetActiveProfileAsync, SetActiveProfileAsync, SaveProfileAsync, RemoveProfileAsync (REQ: IProfileService interface)

## 2. Profile Service Implementation

- [x] 2.1 Create `ProfileService` in `src/Core/Core.Runtime/ProfileService.cs` with atomic file persistence (write-to-temp + rename), SemaphoreSlim for in-process concurrency, and JSON serialization for `profiles.json` (REQ: Profile persistence with atomic writes)
- [x] 2.2 Implement `context.json` → `profiles.json` migration logic in `ProfileService` — on first load, if `profiles.json` missing but `context.json` exists, create "default" profile and set active (REQ: Profiles.json migration from context.json)
- [x] 2.3 Create `StubProfileService` in `tests/Common/Stubs/StubProfileService.cs` for unit testing (in-memory, no file I/O)

## 3. DI Registration

- [x] 3.1 Create `RegisterProfileServices()` extension method in `src/Core/Core.Runtime/` (new file or extend existing DIRegisterExtensions) to register `IProfileService` as Singleton (REQ: DI registration for profile services)
- [x] 3.2 Add `services.RegisterProfileServices()` call in `src/Core/Core.Engine/Bootstrapper.cs` RegisterServices method

## 4. Profile CRUD Commands

- [x] 4.1 Create `ListProfilesCommand` in `src/Extensions/Login/Commands/ListProfiles.cs` — lists all profiles with active marker (REQ: Profile list command)
- [x] 4.2 Create `ShowProfileCommand` in `src/Extensions/Login/Commands/ShowProfile.cs` — shows single profile details, defaults to active if no name provided (REQ: Profile show command)
- [x] 4.3 Create `SwitchProfileCommand` in `src/Extensions/Login/Commands/SwitchProfile.cs` — calls SetActiveProfileAsync, error with available profiles if not found (REQ: Profile switch command)
- [x] 4.4 Create `ModifyProfileCommand` in `src/Extensions/Login/Commands/ModifyProfile.cs` — updates individual properties, --set for defaults (REQ: Profile modify command)
- [x] 4.5 Create `RemoveProfileCommand` in `src/Extensions/Login/Commands/RemoveProfile.cs` — deletes profile with confirmation (REQ: Profile remove command)

## 5. Login/Logout Integration

- [x] 5.1 Modify `LoginCommand` in `src/Extensions/Login/Commands/Login.cs` — add `--out-profile` / `OutProfile` parameter, create profile on login, auto-"default" when omitted, overwrite with warning on collision (REQ: Login creates profile via --out-profile)
- [x] 5.2 Modify `LogoutCommand` in `src/Extensions/Login/Commands/Logout.cs` — add `--profile` / `Profile` parameter, clear tokens for targeted profile, preserve profile definition (REQ: Logout with profile targeting)

## 6. Extension Index & Command Registration

- [x] 6.1 Update `ExtensionIndex` in `src/Extensions/Login/ExtensionIndex.cs` — add CLI and PS dispatch table entries for profile commands (list, show, switch, modify, remove), update LoginCommand/LogoutCommand parameter metadata (REQ: Login extension command registration for profiles)
- [x] 6.2 Add `profile` command group to `CommandGroupIndex` in `src/CLI/Parser/CommandGroupIndex.cs` so that `azv2 profile <subcommand>` routes to the Login extension

## 7. Engine Integration

- [x] 7.1 Modify `Engine.CreateCommandContext()` in `src/Core/Core.Engine/Engine.cs` — resolve profile via IProfileService (active or --profile override), derive transient AuthContext, error with guidance when no profile available (REQ: Profile resolution order, REQ-CTX-05 modified)
- [x] 7.2 Implement `--profile` argument stripping in CLI entry path (`src/CLI/Program.cs` or `ExtensionDispatcher`) — extract and remove `--profile <name>` from args before extension parsing, pass profile name to Engine (REQ: Engine-level --profile parameter stripping)

## 8. AuthContextService Adapter

- [x] 8.1 Refactor `AuthContextService` in `src/Core/Core.Authentication/AuthContextService.cs` to delegate to `IProfileService` — GetCurrentContextAsync reads active profile, SaveContextAsync updates active profile + writes context.json for compat, ClearContextAsync preserves profile definition (REQ: REQ-CTX-02, REQ-CTX-03, REQ-CTX-04 modified)

## 9. PowerShell Cmdlets

- [x] 9.1 Create `Get-CirrusProfile.ps1` in `src/PS/Login/exports/` — PowerShell wrapper for list/show profile (REQ: Profile list/show commands)
- [x] 9.2 Create `Switch-CirrusProfile.ps1` in `src/PS/Login/exports/` — PowerShell wrapper for switch (REQ: Profile switch command)
- [x] 9.3 Create `Set-CirrusProfile.ps1` in `src/PS/Login/exports/` — PowerShell wrapper for modify (REQ: Profile modify command)
- [x] 9.4 Create `Remove-CirrusProfile.ps1` in `src/PS/Login/exports/` — PowerShell wrapper for remove (REQ: Profile remove command)
- [x] 9.5 Update `Connect-CirrusAccount.ps1` — add `-OutProfile` parameter, pass to CommandInvocation (REQ: Login creates profile via --out-profile)
- [x] 9.6 Update `Disconnect-CirrusAccount.ps1` — add `-Profile` parameter, pass to CommandInvocation (REQ: Logout with profile targeting)

## 10. PowerShell Module Manifest

- [x] 10.1 Update `src/PS/Login/Login.psd1` — add Get-CirrusProfile, Switch-CirrusProfile, Set-CirrusProfile, Remove-CirrusProfile to FunctionsToExport (REQ: PowerShell module manifest updated for profile cmdlets)

## 11. Tests

- [x] 11.1 Create `ProfileServiceTests.cs` in `tests/Cli.Tests/` — unit tests for ProfileService CRUD, atomic writes, migration, case-insensitive lookup, concurrent writes (REQ: Profile persistence, IProfileService interface, migration)
- [x] 11.2 Create `ProfileModelTests.cs` in `tests/Cli.Tests/` — unit tests for Profile validation, serialization round-trip, invalid name rejection (REQ: Profile data model)
- [x] 11.3 Create `ProfileCommandTests.cs` in `tests/Cli.Tests/` — unit tests for list, show, switch, modify, remove commands (REQ: Profile CRUD commands)
- [x] 11.4 Add login/logout profile integration tests in `tests/Cli.Tests/` — test --out-profile on login, --profile on logout, default profile auto-creation (REQ: Login/Logout profile integration)
- [x] 11.5 Add engine profile resolution tests in `tests/Cli.Tests/` — test --profile override, active profile fallback, no-profile error with guidance (REQ: Profile resolution order)
- [x] 11.6 Update `StubAuthContextService` in `tests/Common/Stubs/` if needed for adapter compatibility tests
