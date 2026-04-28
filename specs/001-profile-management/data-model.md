# Data Model: Profile Management

**Feature**: 001-profile-management
**Date**: 2026-04-29

## Entities

### Profile

Represents a named authentication/connection context. Does not store credentials or tokens.

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| Name | `string` | Yes | Unique, matches `[a-zA-Z0-9_-]+` | Human-readable identifier |
| TenantId | `string` | Yes | Non-empty GUID string | Azure AD tenant |
| SubscriptionId | `string` | Yes | Non-empty GUID string | Azure subscription |
| CloudName | `string` | No | Default: `"AzureCloud"` | Cloud environment name |
| AuthMethod | `string` | Yes | One of: `browser`, `device-code`, `managed-identity`, `service-principal` | Authentication flow used |
| UserPrincipalName | `string` | No | Email-format string | User identity (for interactive flows) |
| ClientId | `string` | No | GUID string | Application/service principal ID |
| Defaults | `Dictionary<string, string>` | No | Default: empty dictionary | Extensible key-value pairs (e.g., `location`, `resource-group`) |

**Validation rules**:
- `Name` is validated on creation and modification. Invalid names are rejected with an error listing the allowed character set.
- `TenantId` and `SubscriptionId` are required; empty values are rejected.
- `AuthMethod` must be one of the four allowed values.
- `Defaults` keys and values are arbitrary strings; extensions consume what they recognize.

### ProfileStore (Serialization Envelope)

Top-level structure of `profiles.json`. Wraps the profile collection and metadata.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| SchemaVersion | `int` | Yes | Starts at `1`. Incremented on breaking structural changes. |
| ActiveProfile | `string?` | No | Name of the currently active profile. `null` if no profile is active. |
| Profiles | `Dictionary<string, Profile>` | Yes | Map of profile name → Profile object. |

**Serialization**:
- JSON via `System.Text.Json` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and `WriteIndented = true`.
- Unknown properties are ignored on read (forward compatibility per R8).
- File location: `{Environment.SpecialFolder.LocalApplicationData}/.cirrus/profiles.json`

**Example**:
```json
{
  "schemaVersion": 1,
  "activeProfile": "dev",
  "profiles": {
    "dev": {
      "name": "dev",
      "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "subscriptionId": "11111111-2222-3333-4444-555555555555",
      "cloudName": "AzureCloud",
      "authMethod": "browser",
      "userPrincipalName": "user@contoso.com",
      "clientId": null,
      "defaults": {
        "location": "eastus"
      }
    },
    "prod": {
      "name": "prod",
      "tenantId": "ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj",
      "subscriptionId": "66666666-7777-8888-9999-000000000000",
      "cloudName": "AzureCloud",
      "authMethod": "service-principal",
      "userPrincipalName": null,
      "clientId": "abcdef01-2345-6789-abcd-ef0123456789",
      "defaults": {}
    }
  }
}
```

### AuthContext (Existing — Transient)

Unchanged record. Derived from the active or overridden Profile at command execution time. Never persisted under the new model.

| Field | Type | Description |
|-------|------|-------------|
| TenantId | `string` | From `Profile.TenantId` |
| SubscriptionId | `string` | From `Profile.SubscriptionId` |

**State transitions**: `AuthContext` is created fresh by `Engine.CreateCommandContext()` for each command invocation. It has no lifecycle — it is garbage-collected when the invocation completes.

## Relationships

```text
ProfileStore (1) ──contains──► Profile (0..N)
ProfileStore (1) ──points-to──► Profile (0..1)  [via ActiveProfile name]
Profile (1) ──derives──► AuthContext (transient, per-invocation)
```

## IProfileService Interface

```text
IProfileService
├── GetAllProfilesAsync() → IReadOnlyList<Profile>
├── GetProfileAsync(name) → Profile?
├── GetActiveProfileAsync() → Profile?
├── SetActiveProfileAsync(name) → void
├── SaveProfileAsync(profile, setActive?) → void
├── UpdateProfileAsync(name, updates) → void
├── DeleteProfileAsync(name) → void
└── (internal) LoadStoreAsync() / PersistStoreAsync()
```

**Concurrency**: All public methods acquire `SemaphoreSlim(1,1)` before read-modify-write sequences. The semaphore is released in `finally` blocks.

**Caching**: The `ProfileStore` is loaded from disk on first access and cached in memory. All mutations update the in-memory cache and persist to disk atomically.
