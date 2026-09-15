## Why

As the next generation of Azure CLI and Azure PowerShell, Azclips must learn from the profile management pain points that have plagued existing tools — and competitors — for over a decade.

**Azure CLI & Azure PowerShell pain points**
- **Token cache corruption & concurrency** is the #1 pain point. The shared `~/.azure` directory is a concurrency bottleneck — parallel Terraform runs, Airflow DAGs, and multi-subscription scripts trigger lockfile errors and JSON corruption.
- **Session/context management** is fragile. `az login` with "multiple accounts with same username" and PowerShell context file corruption during parallel imports show that the single-context model does not scale to real-world enterprise workflows.
- **Multi-tenant switching** is a recurring regression. PowerShell multi-tenant SP context switching broke in v2.10.4, GDAP tenant connections remain broken in Az.Accounts 3.0.0+. There is no stable, first-class multi-tenant story.

**AWS CLI pain points**
- **Profiles are too narrow**. The #1 most-reacted issue across the entire AWS CLI dataset was simply the inability to put `endpoint_url` in a profile — a feature that took 10 years to ship. Users expect a profile to fully describe "how I talk to a cloud service," not just credentials + region.
- **Credential precedence is confusing**. ~20 issues stem from `--profile` vs `AWS_PROFILE` vs `AWS_ACCESS_KEY_ID` having surprising, poorly-documented precedence. Env-var credentials silently override an explicit `--profile` flag.
- **Two ways to do the same thing**. `AWS_PROFILE` vs `AWS_DEFAULT_PROFILE`, `[profile X]` vs `[X]`, `--profile` vs `AWS_PROFILE` env var — every redundant mechanism generates confusion.

**Azclips opportunity**: Design profiles as a complete "connection context" from day one — not a rigid struct bolted on later. Define clear, unambiguous resolution precedence (`--profile` flag → active profile → error). Provide first-class multi-tenant/multi-subscription switching without re-authentication. Include profile context in error messages. Ship introspection tools (`list`, `show`, `validate`) early.

## What Changes

- Introduce a `Profile` model containing all connection state: name, tenant ID, subscription ID, cloud name, auth method, optional defaults (location, resource group), user principal name, and client ID. All fields use language defaults when not explicitly set.
- Add `IProfileService` / `ProfileService` in Core for profile CRUD and active-profile management, replacing the single-context `AuthContext` persistence. Writes to `profiles.json` use atomic temp-file-plus-rename to prevent corruption from concurrent access.
- Modify the `login` command to accept `--out-profile <name>` (create/overwrite a named profile) and `--profile <name>` (re-authenticate an existing profile, e.g. for MFA/token refresh). Bare login (no flag) auto-creates/overwrites a reserved disposable `"default"` profile. `--profile` and `--out-profile` are mutually exclusive and fail fast if combined. `login` returns the resulting `Profile` as output. `"default"` is a reserved profile name — users cannot create it directly but can modify it via login.
- `logout` command signs out the active profile: it removes the active profile's associated account from the shared MSAL token cache (identified by the profile's user principal name) and clears the active-profile pointer, retaining the profile definition. When no profile is active, `logout` is a no-op.
- Add new `profile` commands: `list`, `show`, `set`, `remove`. (`profile create` is deferred to a future configuration-management change.)
- Add a `--profile <name>` per-command override parameter (scoped to Resource, Compute, and Login processors for this change) that uses a named profile for a single execution without changing the active pointer.
- Modify `Engine.CreateProcessorContext()` to resolve the active profile (or `--profile` override) from a **global-parameter value pre-bound by `ExecutionSession.GetProcessor()` before context creation** and add the nullable `Profile?` to `ProcessorContext`. Processors that require a profile access it from `ProcessorContext.Profile` and fail at point of use if null — commands like `login` and profile management commands operate without a profile.
- Remove the `AuthContext` path entirely: delete the `AuthContext` record, `IAuthContextService`/`AuthContextService`, and `context.json`. `AuthHandler` reads `TenantId`/`SubscriptionId` directly from the resolved `Profile`, which becomes the single source of connection state for the HTTP auth pipeline and all processors.
- Remove the legacy `show` (CLI) / `Get-AzContext` (PowerShell) command (`GetContextProcessor`) and its `ExtensionIndex`/manifest/help/export registrations; superseded by `profile list` / `profile show`.

## Capabilities

### New Capabilities

- `profile-crud`: Read and delete named profiles. Includes the `Profile` model, `IProfileService`, atomic persistence to `{LocalApplicationData}/.azclips/profiles.json`, and active-profile pointer management. Profile names must be 1–100 characters, alphanumeric plus `-`, `_`, `.`; `"default"` is reserved for bare login.
- `profile-switching`: Set the active profile persistently (`profile set`) or per-command (`--profile` override on Resource, Compute, and Login processors). Includes profile resolution order: `--profile` flag → active profile → null (error at point of use).
- `login-profile-integration`: Modify login command to create profiles on login (`--out-profile` or auto-`temp`) or re-authenticate an existing profile (`--profile`), returning the resulting `Profile`. Logout signs out the active profile (removes its account from the MSAL token cache and clears the active pointer).

### Modified Capabilities

_(none — no existing specs to modify)_

## Impact

- **Core.Abstractions**: New `Profile` model, `IProfileService` interface, nullable `Profile?` added to `ProcessorContext` record. The `AuthContext` record and `IAuthContextService` are **removed**; `IAuthHandler.AuthContext` is replaced by a nullable `Profile?`.
- **Core.Runtime**: New `ProfileService` implementation with atomic file writes. Profiles are broader than auth and will expand to all connection config. `AuthHandler` reads `TenantId`/`SubscriptionId` from the resolved `Profile`.
- **Core.Authentication**: `AuthContextService` and its DI registration are **removed**.
- **Core.Engine**: `ExecutionSession.GetProcessor()` is restructured to pre-bind the global `--profile` parameter before context creation; `Engine.CreateProcessorContext()` resolves the profile from that pre-bound value (or the active profile), populates `AuthHandler` from it, and includes nullable `Profile?` in `ProcessorContext`. The `IAuthContextService` constructor dependency is replaced by `IProfileService`. Commands that don't need a profile (login, profile management) operate with null.
- **Login extension**: Modified `LoginProcessor` (saves a `Profile`), `LogoutProcessor` (signs out the active profile: removes its account from the MSAL token cache and clears the active pointer). `GetContextProcessor` (`login show`) is **removed**. New profile processors (list, show, switch, remove) added to `ExtensionIndex`.
- **Resources & Compute extensions**: `ResourcesProcessorBase` and `ComputeProcessorBase` expose nullable `Profile?` (propagated through `CreateExtendedProcessor<T>`) in place of `AuthContext`, and gain the `--profile` parameter.
- **CLI processors**: Resource, Compute, and Login processors gain a `--profile` parameter (workaround until a global parameter framework exists). Other modules to be updated via code generation.
- **PowerShell cmdlets**: `-Profile` parameter on Resource, Compute, and Login cmdlets (using the `OutputStreams`/`engine.OpenExecutionSession()` pattern). `Get-AzContext` cmdlet, manifest export, help, and export script are **removed**.
- **Storage**: New `{LocalApplicationData}/.azclips/profiles.json` file (atomic writes). Existing `context.json` is **removed** (no migration).
