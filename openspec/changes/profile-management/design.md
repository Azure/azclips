## Context

Azclips currently uses a single `AuthContext(TenantId, SubscriptionId)` record persisted in `~/.azclips/context.json` alongside a shared MSAL token cache at `~/.azclips/MSAL.cache`. The `Engine.CreateProcessorContext()` method loads this single context for every processor execution. There is no mechanism for storing multiple connection configurations or switching between them.

The codebase already has TODOs acknowledging the need for multi-account support (`AuthContextService.cs`, `MsalAuthService.cs`). The extension module pattern (Business/Processors/ExtensionIndex) is well-established via the Resources and Login modules.

Since PR #209, the Engine uses an event-driven session model: `Engine.OpenExecutionSession()` creates an `ExecutionSession` whose `GetProcessor()` calls `Engine.CreateProcessorContext()` (wired via the `session.CreateProcessorContext` delegate) to build a `ProcessorContext(HttpPipeline, Publisher, AuthHandler, OutputHandler)` record. Processors currently access auth via `context.AuthHandler.AuthContext`. Profile management must integrate at the `CreateProcessorContext()` level to resolve which profile populates the context.

## Goals / Non-Goals

**Goals:**
- Users can list, show, switch, and remove named profiles
- Users can switch between profiles persistently or override per-command
- Login automatically creates a profile (named or "default") and sets it active
- Profile is the single source of truth for connection state, replacing `context.json`
- `AuthContext`, `IAuthContextService`/`AuthContextService`, and `context.json` are removed entirely; the HTTP auth pipeline (`AuthHandler`) reads tenant/subscription directly from the resolved `Profile`
- Cloud name is a stored property on every profile (no behavioral impact — sovereign cloud MSAL wiring deferred)
- Profile writes are atomic (temp-file-plus-rename) to prevent corruption from concurrent access
- `"default"` is a reserved profile name used exclusively by bare login

**Non-Goals:**
- Commands for creating or modifying profiles directly (e.g. `profile create`) — deferred to a future configuration-management change
- Sovereign cloud MSAL endpoint wiring (follow-up work)
- Global parameter framework (dependency, not blocker — `--profile` implemented per-processor as workaround, scoped to Resource and Login for this change)
- Profile import/export or sharing between machines
- Profile credential storage (MSAL cache remains the token store)

## Decisions

### 1. Profile model lives in Core.Abstractions, service in Core.Runtime

**Decision**: `Profile` record and `IProfileService` interface in `Core.Abstractions`. `ProfileService` implementation in `Core.Runtime`.

**Rationale**: Profiles are not solely an authentication concern — they will expand to all configuration (defaults, preferences, service-specific settings). `Core.Runtime` is the appropriate home because it already handles command context creation and pipeline setup. The interface in `Core.Abstractions` ensures Engine and extensions can reference it without circular dependencies.

**Alternative considered**: `Core.Authentication` alongside `AuthContextService`. Rejected because profiles are broader than auth — placing them in Authentication would create a misleading coupling and require moving them later as the scope expands.

### 2. Profile persistence in `{LocalApplicationData}/.azclips/profiles.json`

**Decision**: All profiles stored in a single JSON file at `{LocalApplicationData}/.azclips/profiles.json` (consistent with existing `AuthContextService` and `MsalAuthService` storage paths). Active profile name stored in a separate `{LocalApplicationData}/.azclips/active-profile` text file. All writes use atomic temp-file-plus-rename to prevent corruption from concurrent access.

**Rationale**: Simple, human-readable, consistent with existing `context.json` pattern. Using `Environment.SpecialFolder.LocalApplicationData` aligns with the established codebase convention. Separating the active pointer from profile definitions avoids rewriting the full profiles file on every switch. Atomic writes address the concurrency corruption risks cited in the proposal's motivation.

**Alternative considered**: One file per profile (`{LocalApplicationData}/.azclips/profiles/<name>.json`). More filesystem overhead, harder to list atomically. SQLite also considered but overly complex for a small config store.

### 3. Bare login creates a "default" profile

> **Update (#490, Aug 2026):** The reserved name was changed from `"temp"` to `"default"` based on private-preview feedback that "temp" was confusing about the profile's purpose and lifecycle. The original decision and rationale below are preserved as history; wherever it says `"temp"`, the current behavior uses `"default"`.

**Decision**: `az login` without `--out-profile` saves the resulting connection as a profile named `"default"`, overwriting any previous default profile. `az login --out-profile <name>` creates/overwrites a named profile. `"default"` is a reserved profile name — users cannot create it via `profile create` (deferred) but bare login always uses it.

**Rationale (original, for "temp")**: Eliminates backward-compat complexity of maintaining both profile and non-profile code paths. "temp" is intentionally disposable, nudging users toward named profiles. The name "temp" avoids collision with "default" which could be confused with a pointer concept. Reserving "temp" prevents user confusion about its lifecycle. (Preview feedback later reversed the naming preference — see the update note above.)

**Alternative considered**: (A) Keep old `context.json` behavior for bare login — rejected to avoid dual code paths. (C) Auto-generate name from tenant/user — rejected because long names defeat the purpose of named profiles.

### 4. `--profile` is a global parameter resolved via a pre-binding pass

**Decision**: Until a general global-parameter framework exists, Resource, Compute, and Login processors declare `--profile` (CLI) / `-Profile` (PowerShell) as a regular `ArgumentInfo` parameter. `ExecutionSession.GetProcessor()` is restructured to perform a **global-parameter pre-binding pass**: it resolves command metadata, then extracts and normalizes the `--profile` value from `CommandInvocation.Parameters` (using `commandMetadata.ParameterAliases` for casing/alias handling) **before** `CreateProcessorContext()` is called. The resolved profile name flows into `CreateProcessorContext()`, which resolves the profile and populates `AuthHandler` from it. The remaining command-specific parameters are bound after the context is created and the processor is instantiated. Other extension modules will be updated via code generation in a follow-up change.

**Rationale**: In the current flow `CreateProcessorContext()` runs first and the resulting `ProcessorContext` (which carries `Profile?`) is injected into the processor via `Init()` **before** command-specific binding occurs. The profile must therefore be resolved earlier than full binding. A dedicated pre-binding pass reuses the existing metadata/alias normalization instead of duplicating CLI/PowerShell parsing, keeps a single resolution path across both surfaces, and establishes the minimal seam a future global-parameter framework can generalize. Scoping declarations to Resource and Login keeps this change manageable.

**Alternative considered**: (A) Implementing the full global-parameter framework first — rejected as out of scope and not a blocker; the pre-binding pass is the minimal seam. (B) Resolving `--profile` after full argument binding — rejected because the context (and thus `Profile?`) is already built and injected into the processor at `Init()` time, before binding. (C) Reading the raw `Invocation.Parameters` dictionary without metadata — rejected because it would miss alias/casing normalization that the binder already provides.

### 5. Profile replaces AuthContext on ProcessorContext and in the auth pipeline

**Decision**: `ProcessorContext` gains a nullable `Profile?` property, and `AuthHandler` holds the resolved `Profile?` in place of the removed `AuthContext`. `CreateProcessorContext()` resolves the profile from the **pre-bound global `--profile`/`-Profile` value** (see Decision 4) if present, falls back to the active profile, or sets `Profile` to null if neither exists. The HTTP auth pipeline reads `Profile.TenantId` (and `Profile.SubscriptionId` where needed) directly from the resolved `Profile` for token acquisition — there is no intermediate `AuthContext` projection. The `AuthContext` record, `IAuthContextService`/`AuthContextService`, and `context.json` are deleted. Processors that require a profile (e.g., resource and compute commands) access `context.Profile` (exposed via their `*ProcessorBase`) and fail at point of use if null. Commands that don't require a profile (login, profile management commands) operate with null.

**Rationale**: The event-driven architecture (PR #209) established `ProcessorContext` as the per-invocation context record and `AuthHandler` as the HTTP auth pipeline component. Keeping a separate `AuthContext` alongside `Profile` would reintroduce exactly the "two ways to do the same thing" problem the proposal warns against — two connection-state types kept in sync. Making `Profile` the single object the pipeline and every processor read removes that duplication. `Profile` is nullable so bootstrap commands (login, profile list) execute without one; the `AuthHandler` only dereferences `Profile.TenantId` when a profile is present, so resource/compute commands with a null profile fail at point of use with a clear message.

**Future consideration**: The `Profile` exposed via `ProcessorContext` should be read-only — only designated processors (e.g., `LoginProcessor`) should be able to mutate profile state. Guardrails to enforce this are out of scope for this change but should be addressed before the profile model expands further.

### 6. Logout signs out the active profile

**Decision**: `az logout` signs out the active profile. When an active profile exists, it (1) removes that profile's associated account from the shared MSAL token cache and (2) clears the active-profile pointer. The account is identified by the profile's `UserPrincipalName` (which maps to MSAL's `IAccount.Username`). The profile definition itself is retained — only the active pointer is cleared. When no profile is active, logout is a no-op. `IAuthService.LogoutAsync` is narrowed from "remove all cached accounts" to "remove the single account matching a supplied `UserPrincipalName`"; when the active profile has no `UserPrincipalName`, no account is removed but the active pointer is still cleared.

**Rationale**: The MSAL cache is shared across profiles and stores tokens keyed by tenant+user, so a blanket cache clear would sign out accounts belonging to other profiles. Targeting removal by the active profile's `UserPrincipalName` scopes logout to the account the user is actually signing out of. Clearing the active pointer (rather than deleting the profile) matches user expectations: logging out ends the session but preserves the named connection so it can be re-activated with `profile set` or refreshed with `login --profile`. Gating both steps on the presence of an active profile keeps `logout` safe to run at any time.

**Alternative considered**: (A) Keep the no-op skeleton — rejected now that profile semantics are settled. (B) Remove all cached accounts on logout — rejected because it signs out accounts shared by other profiles. (C) Also delete the active profile definition — rejected because it discards reusable connection state; `profile remove` is the explicit path for deletion.

### 7. Login returns the Profile; `--profile` re-authenticates; mixing with `--out-profile` is rejected

**Decision**: `LoginProcessor` is repurposed to build and **return the resulting `Profile`** as its output (replacing the removed `AuthContext` output). The auth method is derived from the login flags (Browser by default, DeviceCode with `--use-device-code`); `UserPrincipalName` is sourced from the authenticated account. Because the current `AuthResult` does not surface the account username, `AuthResult` is extended to expose it (from MSAL's `IAccount.Username`) so the returned `Profile` is complete. `Broker`/`ServicePrincipal`/`ManagedIdentity` enum values are defined for forward-compatibility but are not reachable through the current login flags in this change. Persistence and activation are performed in a single `SaveAndActivateProfileAsync` call.

`login` accepts `--profile <name>` to **re-authenticate an existing profile** (e.g., MFA/token refresh): tenant/subscription/cloud come from that profile, the auth flow runs, the profile is overwritten with the refreshed result and set active. `--profile` and `--out-profile` are **mutually exclusive** — supplying both fails fast before authentication. To distinguish an explicit `--profile` from the active-profile fallback, `LoginProcessor` reads its own bound `--profile`/`--out-profile` parameter values (not the resolved `ProcessorContext.Profile`, which includes the active fallback). When neither is supplied, login targets `"default"`.

**Rationale**: Returning the `Profile` gives `login`, `profile show`, and `profile list` a single consistent output shape and lets us delete the `GetContextProcessor`/`Get-AzContext` "show" path. Re-auth via `--profile` matches the real-world need to refresh a specific profile's tokens without re-typing tenant/subscription. Failing fast on `--profile`+`--out-profile` avoids the ambiguous "re-auth one profile but write another" case and follows the proposal's "no surprising precedence" principle.

**Alternative considered**: Relying on `ProcessorContext.Profile` for the re-auth target — rejected because resolution falls back to the active profile, so bare `login` with an active profile set would be indistinguishable from explicit `--profile` and would wrongly re-auth the active profile instead of creating `"default"`.

### 8. Active-profile and save-and-activate are native service methods; `AuthMethod.Browser`/`Broker`

**Decision**: `IProfileService` exposes `GetActiveProfileAsync()` and `SaveAndActivateProfileAsync(profile)` as native interface members rather than extension methods over the name-based primitives. `ProfileService` implements both under a single `_gate` acquisition (resolve active pointer + load profiles in one lock; persist profiles + write active pointer in one lock). `profile show` gains an `--active`/`-Active` switch that is mutually exclusive with the profile name. The `AuthMethod` enum value `Interactive` is renamed to `Browser`, and a new `Broker` value is added.

**Rationale**: The previous `GetActiveProfileAsync` extension method made two separately-locked calls (`GetActiveProfileNameAsync` then `GetProfileAsync`), doubling file reads and lock churn; folding them into native methods halves the I/O and removes a lost-update window between the two reads. `SaveAndActivateProfileAsync` similarly avoids loading `profiles.json` twice when login persists and activates a profile. Renaming `Interactive` → `Browser` removes ambiguity (device-code and broker flows are also interactive); `Broker` covers OS-broker (WAM) sign-in for forward-compatibility.

**Alternative considered**: Keeping the extension method and accepting the extra I/O — rejected for the performance reason above. A boolean `makeActive` overload of `SaveProfileAsync` was considered instead of a distinct method name, but a named method reads more clearly at call sites.

## Risks / Trade-offs

- **[Risk] MSAL cache is shared across profiles** → Tokens for different tenants coexist in one cache file. MSAL handles this natively (accounts are keyed by tenant+user), but logout must carefully target only the active profile's account. Mitigation: use MSAL's `GetAccountsAsync()` filtered by tenant/UPN to find the right account.

- **[Risk] "default" profile overwrite loses context** → A bare `az login` silently overwrites the previous default profile. Mitigation: This is by design — the default profile is disposable. Users who need persistence use `--out-profile`.

- **[Risk] `--profile` per-processor is maintenance burden** → Resource, Compute, and Login processors must add the parameter. Other modules to be updated via code generation. Mitigation: Acceptable short-term; global parameter framework is the long-term fix. Document the pattern clearly.

- **[Trade-off] CloudName stored but not wired** → Profiles carry cloud name as a stored property with no behavioral impact. Sovereign cloud endpoint wiring is deferred to a future change.

- **[Trade-off] Profile? is nullable on ProcessorContext** → Commands that don't need a profile (login, profile management) operate with null. Resource and Compute commands must guard against null and provide a clear error message.

## Open Questions

_(none — all resolved)_
