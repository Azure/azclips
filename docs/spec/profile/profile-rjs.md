**Parent Feature:** #32

---

> Status: Draft ready for implementation planning

## Description

Cirrus must support profile-based authentication that enables users to create, store, and switch between named login profiles — each tied to a specific identity, tenant, and subscription. This unifies the sign-in experience across CLI and PowerShell, achieves SSO capabilities, and reduces friction for users who operate across multiple Azure environments.

This document defines deterministic behavior for profile lifecycle, persistence, activation, error handling, migration, and testability across both CLI and PowerShell.

Normative terms in this document follow RFC 2119 style usage: MUST, SHOULD, and MAY.

**Traces to:**
- §1.1 Goal: Unified authentication experience with profile-based login
- §3.1 KPI: ≥50% of preview users successfully complete login using profile-based authentication
- §6.12 in planning doc

## Scope

### In Scope (MVP)

- Local-machine user profiles shared between CLI and PowerShell.
- Create, list, show, set active, delete, and rename profile operations.
- Per-command profile override without changing active profile.
- SSO-backed switching when valid cached credentials exist.
- Migration from legacy single-context storage.

### Out of Scope (MVP)

- Cross-tenant profile traversal workflows.
- Service principal profiles.
- Enterprise policy-driven profile provisioning.
- Team/org shared profile templates.

## Terminology

- Profile: Named authentication context containing identity, tenant, and subscription defaults.
- Active Profile: The profile used by default when no profile override is provided.
- Profile Override: A profile explicitly selected for one command invocation only.
- Auth Context: Runtime account + default values used to execute commands.
- Account Identity: User principal identifier associated with profile authentication.

## User I Can Statements

- I can create a named login profile that remembers my identity, tenant, and default subscription.
- I can switch between profiles without re-authenticating each time (SSO-backed).
- I can list all my saved profiles and see which one is currently active.
- I can share profile configuration across CLI and PowerShell sessions on the same machine.

## User Journeys

### 1) Create First Profile

1. User executes login with profile name.
2. System authenticates and persists profile metadata.
3. System sets the created profile as active.
4. Subsequent commands use active profile defaults.

### 2) Switch to Existing Profile

1. User selects another profile.
2. System sets active profile pointer.
3. System attempts silent token acquisition for selected identity.
4. If silent auth fails, system prompts re-authentication.

### 3) Run One Command with Profile Override

1. User executes command with profile override.
2. System resolves that profile for current invocation only.
3. System does not mutate active profile pointer.

### 4) Delete Profile

1. User deletes non-active profile and operation succeeds.
2. If deleting active profile, behavior is controlled by defined delete policy (see Error Model).

## Command Contract

### CLI Commands

- `azv2 login --subscription <id> --tenant <id> --profile <name>`
- `azv2 profile list`
- `azv2 profile show`
- `azv2 profile set <name>`
- `azv2 profile delete <name> [--force]`
- `azv2 profile rename <oldName> <newName>`
- Any command may accept `--profile <name>` as one-time override.

CLI behavior requirements:

- `azv2 login ... --profile <name>` MUST create or update the named profile metadata after successful authentication.
- `azv2 profile list` MUST return all valid profiles and indicate the active profile.
- `azv2 profile show` MUST return the active profile; if none exists it MUST fail with actionable guidance.
- `azv2 profile set <name>` MUST fail if profile is missing; otherwise it MUST update only active-profile state.
- `azv2 profile delete <name>` MUST fail for active profile unless `--force` is provided.
- `azv2 profile rename <old> <new>` MUST preserve all profile metadata except profile name.
- `--profile <name>` override MUST apply to the current command only and MUST NOT mutate active-profile state.

### PowerShell Cmdlets

- `Connect-CirrusAccount -SubscriptionId <id> -TenantId <id> -Profile <name>`
- `Get-CirrusProfile [-Active]`
- `Select-CirrusProfile -Name <name>`
- `Remove-CirrusProfile -Name <name> [-Force]`
- `Rename-CirrusProfile -Name <oldName> -NewName <newName>`
- Profile-aware commands may accept `-Profile <name>` override.

PowerShell behavior requirements:

- Cmdlet semantics MUST be equivalent to the CLI semantics for create/list/show/set/delete/rename.
- `-Profile <name>` override MUST apply to the current command only and MUST NOT mutate active-profile state.
- Parameter validation MUST enforce the same profile-name constraints as CLI.

### Parity Rules

- CLI and PowerShell read/write the same profile store.
- Output contains equivalent semantic fields (name, tenant, subscription, active).
- Validation rules for names and existence are identical.

## Command Results and Exit Codes

CLI exit code contract:

- `0`: Operation succeeded.
- `2`: Validation error (invalid name, invalid arguments).
- `3`: Not found (profile does not exist).
- `4`: Conflict (already exists, active profile delete without force).
- `5`: Auth required or auth refresh failure.
- `10`: Storage or I/O failure (read/write/corruption unrecoverable).

PowerShell error contract:

- Cmdlets MUST emit terminating errors for validation, conflict, and unrecoverable storage failures.
- Cmdlets SHOULD emit non-terminating warnings for recoverable profile parse failures during list operations.
- Error categories SHOULD map consistently (`InvalidArgument`, `ObjectNotFound`, `ResourceExists`, `PermissionDenied`, `AuthenticationError`).

## Profile Data Model

### Required Fields

- `version` (integer schema version)
- `profileName` (unique key)
- `displayName` (optional user-facing label)
- `tenantId` (GUID)
- `subscriptionId` (GUID)
- `accountId` (identity principal identifier)
- `accountType` (`User` for MVP)
- `environment` (`AzureCloud` default unless explicitly set)
- `createdAt` (UTC timestamp)
- `lastUsedAt` (UTC timestamp)

### Naming Constraints

- Minimum length: 1; maximum length: 64.
- Allowed characters: letters, numbers, hyphen, underscore.
- Case-insensitive uniqueness; preserve original casing for display.
- Disallow reserved names: `.` and `..`.

Validation requirements:

- Implementations MUST normalize and validate names before file access.
- Implementations MUST reject names containing path separators or traversal sequences.

## Persistence Model

- Profile root directory: `~/.cirrus/profiles/`.
- Active profile pointer file: `~/.cirrus/active-profile`.
- One profile per JSON file: `~/.cirrus/profiles/<normalized-name>.json`.
- Writes are atomic (write temp + replace).
- Concurrent writes follow last-write-wins with atomic replacement.

Persistence requirements:

- Profile write operations MUST be atomic.
- Active-profile pointer updates MUST be atomic.
- Implementations SHOULD use file locking where available to reduce race windows.
- Implementations MUST tolerate a partially-written profile file by skipping it and continuing where safe.

## Auth and Session Semantics

- Creating a profile via login persists metadata and sets active profile.
- Switching active profile changes default state for future commands.
- `--profile` or `-Profile` override applies to current invocation only.
- Profile switch attempts silent auth first; interactive re-auth is fallback.
- Token material must remain in dedicated auth cache, not profile metadata.

Auth requirements:

- On profile set/use, implementation MUST attempt silent token resolution first.
- If silent resolution fails, implementation MUST return auth-required behavior and prompt/guide interactive sign-in.
- Profile metadata MUST NOT store token material.

## Error Model and UX

| Scenario | Required Behavior |
|---|---|
| Profile not found | Return non-zero; show `Profile '<name>' not found.` |
| Profile already exists on create | Return non-zero unless explicit overwrite behavior is requested later |
| Invalid profile name | Return non-zero; show naming rule summary |
| Corrupted profile file | Skip profile in list, log warning, continue processing |
| No active profile exists | Return actionable guidance to login or set profile |
| Silent auth failure on switch/use | Prompt interactive login flow |
| Deleting active profile | MVP policy: block unless `--force` / `-Force` is provided |

Error message requirements:

- Messages MUST include profile name for not-found and conflict cases.
- Messages SHOULD include a next-step hint (for example `run azv2 profile list`).
- Errors MUST be deterministic for identical input/state.

## Security and Privacy

- Profile files contain metadata only; no access or refresh tokens.
- Redact sensitive identifiers in verbose logs when practical.
- Do not log token contents under any condition.
- File permissions:
	- Profile directory should be user-only.
	- Profile files should be user read/write only.
	- Active profile pointer file should be user read/write only.

Security requirements:

- Implementations MUST enforce least-privilege file permissions when creating directories/files.
- Implementations MUST avoid writing PII beyond required account identifiers.
- Verbose logs SHOULD redact identifiers where possible.

## Migration and Compatibility

### Legacy Migration

On first run with profile feature enabled:

1. Detect legacy context store.
2. Create profile `default` from legacy context values.
3. Set `default` as active profile.
4. Backup legacy file before finalizing migration.

Migration requirements:

- Migration MUST be one-time and idempotent.
- Migration MUST NOT destroy legacy data unless new profile store write succeeds.
- If active profile already exists, migration MUST NOT overwrite it.

### Failure Handling

- If migration fails before completion, preserve legacy context and return actionable error.
- Migration is idempotent and safe to retry.

## Telemetry

Track these events without sensitive payloads:

- ProfileCreate (success/failure, reason)
- ProfileSwitch (success/failure, silent-auth-used)
- ProfileDelete (success/failure)
- ProfileMigration (success/failure)

Telemetry should support KPI measurement of successful profile-based sign-in completion.

## Acceptance Criteria

### Functional

- Create profile and run a command using stored defaults.
- Create second profile and switch between both profiles.
- Use profile override for one command without changing active profile.

### Parity

- Profile created in CLI is visible and usable in PowerShell.
- Active profile changed in PowerShell is visible in CLI.

### Negative Paths

- Non-existent profile selection returns deterministic error.
- Invalid profile name fails validation consistently across interfaces.
- Corrupted profile file does not crash profile listing.

### Migration

- Existing legacy context migrates to `default` profile on first run.
- Failed migration does not destroy original context.

### Performance

- List operation remains responsive with at least 10 profiles.
- Active profile switch completes without unnecessary re-auth when cache is valid.

## Explicitly Deferred

- Cross-tenant profile traversal workflows.
- Service principal profiles.
- Enterprise policy-driven profile provisioning.
- Team/org shared profile templates.

## Open Decisions

- Whether to support explicit overwrite on create (`--overwrite` / `-Overwrite`) in MVP.
- Whether profile rename should preserve or recalculate normalized file names.
- Whether delete-active should offer interactive fallback selection instead of hard block.

## Related Items

- **Parent:** #32 (Unified Auth, Identity, Onboarding)
- **Related:** #42 (Context & Profile Display)
- **Related:** #43 (First-Run Onboarding) — profile creation is part of onboarding