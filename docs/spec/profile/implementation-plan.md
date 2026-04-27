# Profile Authentication Implementation Plan

## Objective

Deliver profile-based authentication for azv2 with shared behavior across CLI and PowerShell, aligned to [profile-rjs.md](profile-rjs.md), and implemented incrementally with tests-first scaffolding.

## Epic 1: Command Surface and Parity

### Story 1.1: CLI Profile Command Surface

Subtasks:

1. Add `profile` command group handlers: list, show, set, delete, rename.
2. Add `--profile` override parsing for profile-aware commands.
3. Normalize command output fields: profileName, tenantId, subscriptionId, active.
4. Map command failures to agreed exit codes.

### Story 1.2: PowerShell Parity Surface

Subtasks:

1. Add cmdlets: `Get-CirrusProfile`, `Select-CirrusProfile`, `Remove-CirrusProfile`, `Rename-CirrusProfile`.
2. Add `-Profile` override to profile-aware cmdlets.
3. Ensure parity with CLI semantics and validation behavior.

## Epic 2: Profile Schema and Storage

### Story 2.1: Profile Persistence Contract

Subtasks:

1. Create profile model type and serialization contract.
2. Implement profile directory and active-pointer file layout.
3. Implement name normalization and validation.
4. Implement atomic write + tolerant read behavior.

### Story 2.2: AuthContextService Profile APIs

Subtasks:

1. Extend `IAuthContextService` with profile CRUD/read APIs.
2. Implement profile read/list/set-active in `AuthContextService`.
3. Wire existing `GetCurrentContextAsync` to active profile precedence.
4. Keep legacy context fallback for compatibility.

## Epic 3: Session Semantics and Token Boundaries

### Story 3.1: Runtime Profile Resolution

Subtasks:

1. Implement precedence: override > active profile > error.
2. Ensure override is non-persistent.
3. Ensure active profile switching is explicit.

### Story 3.2: Silent Auth Fallback Behavior

Subtasks:

1. Attempt silent auth first on profile use.
2. Return auth-required failure classification when silent auth fails.
3. Keep tokens out of profile metadata.

## Epic 4: Migration and Compatibility

### Story 4.1: Legacy Context Migration

Subtasks:

1. Detect legacy context on first run.
2. Materialize `default` profile from legacy context.
3. Set active pointer to `default`.
4. Keep backup and ensure idempotent behavior.

## Epic 5: Verification and Test Matrix

### Story 5.1: xUnit Contract Matrix

Subtasks:

1. Add command parity matrix tests (CLI/PowerShell semantics).
2. Add storage contract matrix tests (schema/read-write/corruption).
3. Add session semantics matrix tests (override, active, auth-required paths).

### Story 5.2: Integration Tests

Subtasks:

1. Create profile -> switch -> run command with expected defaults.
2. Validate one-time override does not mutate active profile.
3. Validate corrupted profile files do not crash list operations.
4. Validate migration behavior from legacy context.

## Delivery Order

1. Epic 2 Story 2.2 (scaffolded now)
2. Epic 5 Story 5.1 (matrix scaffolded now)
3. Epic 1 Story 1.1 and 1.2
4. Epic 3 Story 3.1 and 3.2
5. Epic 4 Story 4.1
6. Epic 5 Story 5.2

## Current Status

- Completed: Spec breakup through Step 3.
- Completed: Engineering plan document.
- In progress: Initial profile storage and active profile scaffolding in auth service.
- In progress: xUnit matrix test scaffold files.