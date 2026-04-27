# Step 2: Profile Schema and Storage

## Goal

Define the profile data schema and on-disk storage contract used by both azv2 CLI and PowerShell.

This document intentionally covers Step 2 only.

## Scope for Step 2

Included:

- Profile JSON schema
- Naming and normalization rules
- Storage layout and file naming
- Active profile pointer format
- Atomic write/read behavior
- Corruption tolerance rules

Excluded:

- Command UX and parameter contracts
- Authentication/token lifecycle semantics
- Migration from legacy context format
- Telemetry and test matrix

## Storage Layout

Root:

`~/.cirrus/`

Required paths:

- Profiles directory: `~/.cirrus/profiles/`
- Active profile pointer: `~/.cirrus/active-profile`

Profile file path:

- `~/.cirrus/profiles/<normalized-name>.json`

Rules:

- One profile per JSON file.
- File name uses normalized profile name, not display name.
- Implementations MUST create root/directories if missing.

## Profile Schema (Version 1)

```json
{
  "version": 1,
  "profileName": "prod",
  "displayName": "Production",
  "tenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
  "subscriptionId": "00000000-0000-0000-0000-000000000000",
  "accountId": "user@contoso.com",
  "accountType": "User",
  "environment": "AzureCloud",
  "createdAt": "2026-04-27T10:30:00Z",
  "lastUsedAt": "2026-04-27T14:22:00Z"
}
```

## Required Fields

- `version` (integer, MUST be `1` for this step)
- `profileName` (string)
- `tenantId` (string GUID)
- `subscriptionId` (string GUID)
- `accountId` (string)
- `accountType` (string; MVP value MUST be `User`)
- `environment` (string; default `AzureCloud`)
- `createdAt` (UTC timestamp)
- `lastUsedAt` (UTC timestamp)

Optional fields:

- `displayName` (string)

## Validation Rules

### Profile Name

- Length MUST be 1 to 64.
- Allowed characters: `a-z`, `A-Z`, `0-9`, `-`, `_`.
- Disallowed: spaces, path separators, traversal sequences (`..`), control characters.
- Reserved names not allowed: `.`, `..`.

### Identifiers

- `tenantId` MUST be a valid GUID string.
- `subscriptionId` MUST be a valid GUID string.
- `accountId` MUST be non-empty.

### Timestamps

- `createdAt` and `lastUsedAt` MUST be valid UTC ISO 8601 timestamps.
- `lastUsedAt` MUST be greater than or equal to `createdAt`.

## Name Normalization and File Naming

Normalization algorithm:

1. Validate input name with profile-name rules.
2. Preserve original casing in `profileName`.
3. Compute normalized key by lowercasing for uniqueness/file key.
4. Use normalized key for filename.

Examples:

- Input name `Prod` -> `profileName=Prod`, filename `prod.json`
- Input name `staging_us` -> filename `staging_us.json`

Uniqueness rule:

- Profiles are case-insensitively unique (`Prod` and `prod` conflict).

## Active Profile Pointer Contract

File: `~/.cirrus/active-profile`

Format:

- Plain text containing normalized profile key only (no JSON).
- UTF-8, trailing newline optional.

Behavior:

- Missing file means no active profile is set.
- Value MUST map to an existing profile file for valid state.
- Invalid or stale pointer MUST be treated as no active profile.

## Read/Write Contract

### Writes

- Profile writes MUST be atomic: write temp file in same directory, then replace.
- Active pointer updates MUST be atomic.
- Implementations SHOULD fsync before replace where supported.

### Reads

- List operations MUST ignore non-JSON files in profiles directory.
- Invalid JSON or schema-mismatched files MUST be skipped and surfaced as warnings.
- A single corrupt profile MUST NOT fail full list operation.

### Concurrency

- Last-write-wins is accepted for concurrent profile updates.
- Implementations SHOULD use advisory file locks when available.

## Corruption Handling

When a profile file is unreadable or invalid:

- Skip file from normal results.
- Emit warning with file name and reason.
- Continue processing remaining profiles.

When active pointer is invalid:

- Treat as no active profile.
- Do not auto-repair in Step 2.

## Sensitive Data Boundary

Profile schema MUST NOT contain:

- Access tokens
- Refresh tokens
- Client secrets
- Raw credential material

Profile schema MAY contain only non-secret metadata required for profile selection and defaults.

## Step 2 Definition of Done

Step 2 is complete when:

- Schema fields and types are finalized.
- Name rules and normalization behavior are finalized.
- Storage paths and active pointer format are finalized.
- Atomic read/write and corruption behavior are finalized.
- CLI and PowerShell teams confirm shared compatibility with this storage contract.
