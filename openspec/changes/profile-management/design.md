## Context

Cirrus currently persists a single `Core.Abstractions.AuthContext` record (TenantId + SubscriptionId) to `{LocalApplicationData}/.cirrus/context.json` via `Core.Authentication.AuthContextService`. Every command invocation reads this file through `Core.Engine.Engine.CreateCommandContext()`, which derives a transient `Core.Runtime.Handler.CommandAuthHandler` with the loaded tenant for bearer token injection into the HTTP pipeline.

This single-context model has no concept of named profiles, multi-tenant switching, or default parameters (location, resource group). The `AuthContext` record is both the persisted state and the transient execution record — no separation of concerns.

The Login extension (`Microsoft.Azure.Cirrus.Extensions.Login`) provides three commands (`LoginCommand`, `LogoutCommand`, `GetContextCommand`) that directly call `IAuthContextService` via `CirrusServiceProvider.GetRequiredService<IAuthContextService>()`.

**Key constraints from the existing system:**
- `CirrusServiceProvider` is a static, thread-safe singleton — all services registered as Singletons
- `Core.Engine.Engine.CreateCommandContext()` is the single point where auth context enters the command pipeline (both CLI and PowerShell paths)
- PowerShell has strict thread-affinity: `IEngine.Process()` is synchronous, output pumped via `Channel<T>`
- CLI uses `ExtensionDispatcher` → `ExtensionParser` for structured parsing, with agent fallback on failure
- MSAL token cache (`~/.cirrus/MSAL.cache.plaintext`) is managed separately from auth context — tokens are not stored in profiles

## Goals / Non-Goals

**Goals:**
- Provide named profile CRUD (create, list, show, modify, remove) with a `Profile` model richer than `AuthContext`
- Support profile switching (persistent via `profile switch`, per-command via `--profile` engine-level override)
- Integrate profiles with login/logout (`--out-profile`, `--profile` on logout)
- Atomic file persistence to prevent corruption under concurrent access
- Preserve `IAuthContextService` as an internal adapter during migration
- Extensible defaults dictionary on profiles (location, resource group, etc.) — not a fixed struct

**Non-Goals:**
- Config service integration (separate future work — profiles are identity context, config is system settings)
- Encrypted profile storage (MVP uses OS file permissions; security review deferred)
- Global parameter framework for `--profile` (engine-level workaround until that exists)
- Token/credential storage in profiles — MSAL manages tokens separately
- Profile import/export or cloud sync
- NativeAOT compatibility (not yet implemented anywhere in Cirrus)

## Decisions

### ADR-1: Profile model as a class with extensible defaults dictionary

**Decision:** `Profile` is a `class` (not a record) with a `Dictionary<string, string> Defaults` property for extensible key-value pairs.

**Rationale:** A record with fixed properties (like `AuthContext`) would repeat the AWS CLI mistake — `endpoint_url` took 10 years because it wasn't in the profile struct. A dictionary allows adding `location`, `resource-group`, `output-format`, etc. without model changes. A class (vs record) allows mutable updates during `modify` without full reconstruction.

**Alternatives considered:**
- *Record with fixed properties:* Simpler serialization but inflexible — any new default requires a model change.
- *Typed `ProfileDefaults` class:* Better IntelliSense but defeats extensibility. Can be layered on later as a facade.

### ADR-2: Single `profiles.json` file with active-profile pointer

**Decision:** All profiles and the active profile name are stored in a single `{LocalApplicationData}/.cirrus/profiles.json` file. The file contains `{ "activeProfile": "<name>", "profiles": { "<name>": { ... } } }`.

**Rationale:** A single file simplifies atomic writes (write-to-temp + rename replaces the entire file). Per-profile files would require directory-level coordination and separate active-profile tracking. The number of profiles is small (typically < 20), so the file stays small.

**Alternatives considered:**
- *Per-profile files (`profiles/<name>.json`) + `active` symlink/pointer file:* Better for very large numbers of profiles but adds complexity for atomic active-profile switching.
- *SQLite:* Overkill for a small key-value store; adds a native dependency.

### ADR-3: `--profile` handled at engine level, not per-command

**Decision:** The `--profile` parameter is parsed and resolved by `Core.Engine.Engine.CreateCommandContext()` before command dispatch. It is not an `[ArgumentInfo]` on each `IProcessor`.

**Rationale:** Every command that hits the HTTP pipeline needs auth context. Having each command duplicate profile resolution is error-prone and violates DRY. The engine already owns context creation — profile resolution belongs there. For CLI, `ExtensionDispatcher`/`ExtensionParser` strips `--profile` from args before command parsing. For PowerShell, the `-Profile` parameter is added to each cmdlet wrapper `.ps1` and passed to `CommandInvocation` as metadata.

**Alternatives considered:**
- *Per-command `[ArgumentInfo]`:* Works but every command needs it — hundreds of commands eventually. Maintenance burden.
- *Middleware in HttpPipeline:* Too late — profile determines which tenant/subscription to use, which is needed before the pipeline is constructed.

### ADR-4: `IAuthContextService` preserved as adapter during migration

**Decision:** `Core.Authentication.AuthContextService` is refactored to delegate to `IProfileService` internally. `IAuthContextService` interface and its DI registration remain unchanged. Commands that resolve `IAuthContextService` continue to work.

**Rationale:** Breaking `IAuthContextService` immediately would require updating all existing consumers (Login commands, any future extensions, tests) in a single commit. An adapter allows incremental migration. New code uses `IProfileService` directly; old code works via the adapter.

**Thread safety:** `ProfileService` is a Singleton shared across all invocations. File writes use atomic temp-file + rename. In-memory state (`_profiles`, `_activeProfileName`) is guarded by a `SemaphoreSlim` for async access.

### ADR-5: Profile resolution order — unambiguous, no silent overrides

**Decision:** Resolution order is: (1) explicit `--profile <name>` flag → (2) active profile from `profiles.json` → (3) error with actionable message.

**Rationale:** AWS CLI's biggest usability issue is silent env-var override of explicit flags. Cirrus defines one unambiguous precedence chain. No environment variable override in MVP — if added later, it slots between (1) and (2) with explicit documentation.

The error message when no profile exists MUST include guidance: `"No active profile. Run 'azv2 login --subscription-id <id>' or 'azv2 profile switch <name>' to set one."`

### ADR-6: Login creates profiles via `--out-profile` flag

**Decision:** `LoginCommand` gains an `--out-profile <name>` parameter. If provided, the login result is saved as a named profile. If omitted, login creates/overwrites a disposable `"default"` profile and sets it as active. `--out-profile` collision (name already exists) overwrites with a warning.

**Rationale:** Bare `azv2 login` must work for first-time users — no extra flags required. The auto-`default` profile provides backward compatibility with the single-context model. Named profiles are opt-in via `--out-profile` for users who need multi-tenant/multi-subscription.

**Alternatives considered:**
- *Auto-name `"temp"`:* Confusing — suggests the profile is temporary and will be deleted. `"default"` is clearer.
- *Require `--out-profile` always:* Breaks backward compatibility and adds friction for simple use cases.

### ADR-7: `ProfileService` lives in `Core.Runtime`, not a new project

**Decision:** `IProfileService` interface is in `Core.Abstractions`. `ProfileService` implementation is in `Core.Runtime` alongside `CommandContext` and handlers.

**Rationale:** Profiles are a runtime concern (resolved during command execution). `Core.Runtime` already contains `CommandContext`, `CommandAuthHandler`, and the HTTP pipeline — all consumers of profile data. Adding a new `Core.Profile` project would add a csproj + DI registration surface for a single service. If the profile system grows significantly, extraction to a separate project is straightforward.

**DI registration:** `ProfileService` is registered via a new `services.RegisterProfileServices()` extension method in `Core.Runtime`, called from `Core.Engine.Bootstrapper.RegisterServices()` alongside `RegisterAuthenticationServices()`.

### ADR-8: Service locator access for `IProfileService` in new commands

**Decision:** New profile commands (`ListProfilesCommand`, `SwitchProfileCommand`, etc.) resolve `IProfileService` via `CirrusServiceProvider.GetRequiredService<IProfileService>()` — matching the existing pattern used by `LoginCommand`, `LogoutCommand`, and `GetContextCommand`.

**Rationale:** The codebase does not yet have constructor injection for `IProcessor` implementations. All existing commands use the service locator pattern. Introducing constructor injection for profile commands alone would create an inconsistent pattern. When constructor injection is implemented globally, profile commands will be migrated along with everything else.

## Risks / Trade-offs

**[Risk: Single-file contention under concurrent writes]** → Mitigation: Atomic write (write to temp file + `File.Move` with overwrite) prevents corruption. OS-level file rename is atomic on all supported platforms. A `SemaphoreSlim` in `ProfileService` serializes in-process writes. Cross-process contention (two CLI invocations writing simultaneously) relies on last-write-wins — acceptable for profile management which is low-frequency.

**[Risk: `profiles.json` grows unbounded]** → Mitigation: Profiles are user-managed and small (< 1KB each). Even 100 profiles fit in < 100KB. No automatic cleanup needed for MVP.

**[Risk: Adapter pattern for `IAuthContextService` leaks complexity]** → Mitigation: The adapter is intentionally thin (3 methods). It will be removed in a follow-up change once all consumers are migrated to `IProfileService`. A `[Obsolete]` attribute on `IAuthContextService` signals intent.

**[Risk: `--profile` parameter conflicts with extension-defined parameters]** → Mitigation: `--profile` is reserved at the engine level and stripped before extension parsing. If any extension already uses `--profile`, it will need to be renamed. A grep of the current codebase shows no conflicts.

**[Risk: PowerShell thread-affinity with async profile loading]** → Mitigation: `Engine.CreateCommandContext()` already uses sync-over-async (`GetAwaiter().GetResult()`) for auth context loading. Profile loading follows the same pattern — sync wrapper on the runspace thread, async I/O under the hood.

**[Risk: Breaking change if `context.json` users update without re-login]** → Mitigation: The `AuthContextService` adapter checks `profiles.json` first, falls back to reading `context.json` if it exists. On next login, data moves to `profiles.json`. This provides a seamless migration path.

## Migration Plan

**Phase 1 — Add profile infrastructure (non-breaking):**
1. Add `Profile` model and `IProfileService` to `Core.Abstractions`
2. Add `ProfileService` to `Core.Runtime` with atomic persistence
3. Register `IProfileService` in `Bootstrapper`
4. Add profile commands to Login extension
5. All existing behavior unchanged — `IAuthContextService` still active

**Phase 2 — Wire profiles into engine (behavior change):**
1. Modify `Engine.CreateCommandContext()` to resolve profile → `AuthContext`
2. Refactor `AuthContextService` to adapter over `IProfileService`
3. Add `--profile` engine-level parameter handling
4. Modify `LoginCommand` to create profiles via `--out-profile`
5. Modify `LogoutCommand` to accept `--profile`

**Phase 3 — Cleanup (follow-up change):**
1. Mark `IAuthContextService` as `[Obsolete]`
2. Migrate remaining consumers to `IProfileService`
3. Remove `context.json` compatibility shim
4. Remove `AuthContextService` adapter

**Rollback:** Each phase is independently deployable. Phase 1 is purely additive. Phase 2 can be reverted by restoring `Engine.CreateCommandContext()` to use `IAuthContextService` directly. The adapter pattern ensures Phase 2 rollback doesn't lose profile data.

## Open Questions

1. **Should `Profile` store `CloudName` (e.g., AzureCloud, AzureChinaCloud, AzureUSGovernment)?** The existing system hardcodes ARM scope and MSAL authority. Multi-cloud support may need profile-level cloud configuration. *Recommendation: include the field with `"AzureCloud"` default — no behavior change but future-proofs the model.*

2. **Should `profile switch` trigger re-authentication if the target profile's MSAL token is expired?** The current system silently fails with `MsalUiRequiredException`. *Recommendation: `profile switch` validates token availability and warns if re-login is needed, but does not block the switch.*

3. **Should `profiles.json` be human-editable or binary-only?** *Recommendation: human-readable indented JSON (matching `context.json` precedent). Users can hand-edit in emergencies.*

## File Changes

### New Files

| Path | Description |
|---|---|
| `src/Core/Core.Abstractions/Models/Profile.cs` | `Profile` class with Name, TenantId, SubscriptionId, CloudName, AuthMethod, UserPrincipalName, ClientId, Defaults dictionary |
| `src/Core/Core.Abstractions/IProfileService.cs` | Interface: GetAllProfilesAsync, GetProfileAsync, GetActiveProfileAsync, SetActiveProfileAsync, SaveProfileAsync, RemoveProfileAsync |
| `src/Core/Core.Runtime/ProfileService.cs` | Implementation with atomic file persistence, SemaphoreSlim concurrency, migration from context.json |
| `src/Core/Core.Runtime/DIRegisterExtensions.cs` | `RegisterProfileServices()` extension method (new file or extend existing) |
| `src/Extensions/Login/Commands/ListProfiles.cs` | `ListProfilesCommand : LoginCommandBase` |
| `src/Extensions/Login/Commands/ShowProfile.cs` | `ShowProfileCommand : LoginCommandBase` |
| `src/Extensions/Login/Commands/SwitchProfile.cs` | `SwitchProfileCommand : LoginCommandBase` |
| `src/Extensions/Login/Commands/ModifyProfile.cs` | `ModifyProfileCommand : LoginCommandBase` |
| `src/Extensions/Login/Commands/RemoveProfile.cs` | `RemoveProfileCommand : LoginCommandBase` |
| `src/PS/Login/exports/Get-CirrusProfile.ps1` | PowerShell wrapper for list/show profile |
| `src/PS/Login/exports/Switch-CirrusProfile.ps1` | PowerShell wrapper for switch |
| `src/PS/Login/exports/Set-CirrusProfile.ps1` | PowerShell wrapper for modify |
| `src/PS/Login/exports/Remove-CirrusProfile.ps1` | PowerShell wrapper for remove |
| `tests/Cli.Tests/ProfileServiceTests.cs` | Unit tests for ProfileService |
| `tests/Cli.Tests/ProfileCommandTests.cs` | Unit tests for profile commands |
| `tests/Common/Stubs/StubProfileService.cs` | In-memory stub for testing |

### Modified Files

| Path | Description |
|---|---|
| `src/Core/Core.Abstractions/Models/AuthContext.cs` | No structural change — retains record. Semantics shift from persisted to transient. |
| `src/Core/Core.Authentication/AuthContextService.cs` | Refactored to adapter: delegates to `IProfileService` internally, falls back to `context.json` for migration |
| `src/Core/Core.Authentication/DIRegisterExtensions.cs` | Unchanged registration, but `AuthContextService` implementation changes internally |
| `src/Core/Core.Engine/Bootstrapper.cs` | Add `services.RegisterProfileServices()` call |
| `src/Core/Core.Engine/Engine.cs` | `CreateCommandContext()` resolves profile → `AuthContext`; handles `--profile` override parameter |
| `src/Extensions/Login/Commands/Login.cs` | Add `--out-profile` / `OutProfile` parameter; create profile on login |
| `src/Extensions/Login/Commands/Logout.cs` | Add `--profile` / `Profile` parameter; target specific profile for MSAL cache clear |
| `src/Extensions/Login/ExtensionIndex.cs` | Add profile command entries to CLI and PS dispatch tables |
| `src/PS/Login/Login.psd1` | Add new cmdlets to `FunctionsToExport` |
| `src/PS/Login/exports/Connect-CirrusAccount.ps1` | Add `-OutProfile` parameter |
| `src/PS/Login/exports/Disconnect-CirrusAccount.ps1` | Add `-Profile` parameter |
| `tests/Common/Stubs/StubAuthContextService.cs` | Update to delegate to StubProfileService or remain as-is for backward compat tests |
