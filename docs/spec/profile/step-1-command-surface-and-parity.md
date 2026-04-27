# Step 1: Command Surface and Parity

## Goal

Define the exact profile command surface and cross-interface parity contract for azv2 CLI and PowerShell.

This document intentionally covers Step 1 only.

## Scope for Step 1

Included:

- Command names and required parameters
- Per-command behavior
- Output expectations
- Exit/error contract
- CLI and PowerShell parity mapping

Excluded:

- Persistence internals
- Migration details
- Security requirements
- Test matrix beyond command-level expectations

## CLI Command Surface

### 1) Create/Update Profile via Login

Command:

`azv2 login --subscription <id> --tenant <id> --profile <name>`

Behavior:

- MUST authenticate user interactively when needed.
- MUST create profile when profile does not exist.
- MUST update profile metadata when profile already exists.
- MUST set profile as active on success.

Success output:

- Profile name
- Tenant id
- Subscription id
- Active state

Failure cases:

- Invalid profile name
- Authentication failure
- Storage write failure

### 2) List Profiles

Command:

`azv2 profile list`

Behavior:

- MUST return all valid profiles.
- MUST indicate active profile.
- SHOULD skip corrupted profiles and emit warning.

Success output:

- One row per profile with active indicator and key metadata

Failure cases:

- Unrecoverable profile store read failure

### 3) Show Active Profile

Command:

`azv2 profile show`

Behavior:

- MUST return active profile details.
- MUST fail with actionable message if no active profile exists.

### 4) Set Active Profile

Command:

`azv2 profile set <name>`

Behavior:

- MUST fail when profile does not exist.
- MUST update active-profile state only.
- MUST NOT rewrite unrelated profile metadata.

### 5) Delete Profile

Command:

`azv2 profile delete <name> [--force]`

Behavior:

- MUST delete named profile when it is not active.
- MUST fail when target is active profile and force not provided.
- MUST allow deletion with `--force`.

### 6) Rename Profile

Command:

`azv2 profile rename <oldName> <newName>`

Behavior:

- MUST fail if source does not exist.
- MUST fail if destination exists.
- MUST preserve profile metadata except name.

### 7) One-Time Profile Override

Pattern:

`azv2 <command> ... --profile <name>`

Behavior:

- MUST apply selected profile to current invocation only.
- MUST NOT mutate active profile state.
- MUST fail if profile does not exist.

## PowerShell Command Surface

### 1) Create/Update Profile via Connect

Cmdlet:

`Connect-CirrusAccount -SubscriptionId <id> -TenantId <id> -Profile <name>`

Behavior parity:

- Equivalent to CLI login behavior.
- Sets active profile on success.

### 2) List Profiles

Cmdlet:

`Get-CirrusProfile`

Behavior parity:

- Equivalent to CLI list behavior.
- Shows active indicator.

### 3) Show Active Profile

Cmdlet:

`Get-CirrusProfile -Active`

Behavior parity:

- Equivalent to CLI show behavior.

### 4) Set Active Profile

Cmdlet:

`Select-CirrusProfile -Name <name>`

Behavior parity:

- Equivalent to CLI set behavior.

### 5) Delete Profile

Cmdlet:

`Remove-CirrusProfile -Name <name> [-Force]`

Behavior parity:

- Equivalent to CLI delete behavior.

### 6) Rename Profile

Cmdlet:

`Rename-CirrusProfile -Name <oldName> -NewName <newName>`

Behavior parity:

- Equivalent to CLI rename behavior.

### 7) One-Time Profile Override

Pattern:

`<ProfileAwareCmdlet> ... -Profile <name>`

Behavior parity:

- Equivalent to CLI override behavior.

## CLI-PowerShell Parity Matrix

| Capability | CLI | PowerShell | Parity Requirement |
|---|---|---|---|
| Create/update profile | `azv2 login ... --profile` | `Connect-CirrusAccount ... -Profile` | MUST be semantically equivalent |
| List profiles | `azv2 profile list` | `Get-CirrusProfile` | MUST return same logical fields |
| Show active profile | `azv2 profile show` | `Get-CirrusProfile -Active` | MUST return same active profile |
| Set active | `azv2 profile set` | `Select-CirrusProfile` | MUST produce same state change |
| Delete profile | `azv2 profile delete` | `Remove-CirrusProfile` | MUST enforce same active delete policy |
| Rename profile | `azv2 profile rename` | `Rename-CirrusProfile` | MUST preserve same metadata |
| One-time override | `--profile` | `-Profile` | MUST NOT mutate active profile |

## Step 1 Exit and Error Contract

CLI exit codes:

- `0` success
- `2` validation error
- `3` profile not found
- `4` conflict (already exists, active delete without force)
- `5` auth required/auth failed
- `10` storage failure

PowerShell error mapping:

- Validation: `InvalidArgument`
- Missing profile: `ObjectNotFound`
- Conflict: `ResourceExists`
- Auth required/failure: `AuthenticationError`
- Storage failure: `WriteError` or terminating `InvalidOperation`

## Definition of Done for Step 1

Step 1 is complete when:

- Command names and parameters are finalized for both interfaces.
- Parity matrix is approved.
- Exit/error contract is approved.
- No open ambiguity remains on whether an operation mutates active profile state.
