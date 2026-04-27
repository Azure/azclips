# Step 3: Session Semantics and Token Boundaries

## Goal

Define runtime behavior for profile activation, one-time overrides, SSO/silent auth, and token-handling boundaries for azv2 CLI and PowerShell.

This document intentionally covers Step 3 only.

## Scope for Step 3

Included:

- Active profile runtime semantics
- One-time profile override semantics
- Silent auth and interactive fallback behavior
- Profile-use behavior when auth state is stale
- Token/material boundaries between profile metadata and auth cache
- Cross-interface (CLI/PowerShell) behavioral parity at runtime

Excluded:

- Storage schema internals
- Migration from legacy context
- Telemetry contract
- Full acceptance test matrix

## Runtime Context Resolution

For each command invocation, implementations MUST resolve context in this order:

1. Explicit profile override (`--profile` or `-Profile`) if present
2. Active profile pointer
3. No profile (error state requiring user action)

Resolution requirements:

- If resolved profile does not exist, command MUST fail deterministically.
- If neither override nor active profile resolves, command MUST fail with guidance to create or select a profile.

## Active Profile Semantics

- Active profile is persistent state shared by CLI and PowerShell.
- `profile set` / `Select-CirrusProfile` MUST change only active-profile state.
- Active profile change MUST NOT mutate unrelated profile metadata.
- Commands without explicit override MUST use active profile defaults.

## One-Time Override Semantics

- `--profile <name>` and `-Profile <name>` MUST apply to current invocation only.
- Override MUST NOT modify active profile pointer.
- Override MUST use same name validation and existence checks as active profile selection.

## Authentication Flow Semantics

### Profile Create/Update via Login

- Successful login with profile target MUST bind authenticated identity to that profile metadata.
- Successful login with profile target MUST set that profile as active.

### Using a Profile for Command Execution

When a command executes with resolved profile:

1. Implementation MUST attempt silent token acquisition first.
2. If silent acquisition succeeds, command continues.
3. If silent acquisition fails due to expired/missing credentials, command MUST return auth-required behavior.
4. Interactive login prompt/flow MAY be launched immediately based on host UX policy.

### Profile Switching

- Switching profiles SHOULD NOT force immediate interactive login if silent auth is still possible.
- First command after switch MUST follow the same silent-then-interactive sequence.

## Identity Binding Semantics

- A profile represents one identity + tenant + subscription default set.
- Re-login targeting an existing profile MAY update identity binding if authentication succeeds and user confirms policy (policy decision deferred to later step).
- Runtime account used for token acquisition MUST correspond to resolved profile metadata.

## Token and Secret Boundaries

Profile metadata files MUST NOT contain:

- Access tokens
- Refresh tokens
- Client secrets
- Raw credential material

Token/credential handling requirements:

- Tokens MUST live only in dedicated authentication cache/provider.
- Session logic MUST reference profile metadata only for identity/default resolution, not secret retrieval from profile files.
- Logs and errors MUST NOT print token content.

## Stale Auth and Recovery Semantics

When auth cache state is stale or unavailable for resolved profile:

- Command MUST fail with explicit auth-required classification unless host initiates interactive flow inline.
- Guidance MUST identify next corrective action (`azv2 login --profile <name>` or `Connect-CirrusAccount -Profile <name>`).
- Profile metadata SHOULD remain unchanged when auth refresh fails.

## Cross-Interface Runtime Parity

CLI and PowerShell MUST be runtime-equivalent for:

- Context resolution precedence
- Override non-persistence behavior
- Silent-then-interactive auth ordering
- Auth-required failure classification

## Determinism Requirements

Given same profile store and auth cache state:

- CLI and PowerShell MUST resolve the same profile for the same command context.
- Repeated invocation with unchanged state MUST produce consistent success/failure classification.

## Step 3 Definition of Done

Step 3 is complete when:

- Resolution precedence is finalized and approved.
- Silent vs interactive auth fallback behavior is finalized.
- Override non-persistence behavior is finalized.
- Token/secret boundary rules are explicitly approved.
- CLI and PowerShell runtime parity for session semantics is approved.
