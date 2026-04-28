# CLI Command Contracts: Profile Management

**Feature**: 001-profile-management
**Extension**: `Microsoft.Azure.Cirrus.Extensions.Login`

## Commands

All profile commands are registered under the `profile` command group in the Login extension's CLI dispatch table.

### `login` (Modified)

```text
azv2 login [--tenant-id <id>] [--subscription <id|name>] [--out-profile <name>] [--use-device-code]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--tenant-id` | string | No | Azure AD tenant ID |
| `--subscription` | string | No | Subscription ID or name. Skips interactive prompt. |
| `--out-profile` | string | No | Profile name to create/overwrite. Default: `temp` |
| `--use-device-code` | bool | No | Use device code flow |

**Output**: Profile summary (name, tenant, subscription, auth method)
**Errors**: Invalid profile name → error with allowed character set. `--subscription` not found → error listing available subscriptions.

### `logout` (Modified)

```text
azv2 logout [--profile <name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--profile` | string | No | Clear token for a specific profile instead of the active one |

**Output**: Confirmation message ("Logged out from profile: {name}")
**Errors**: No active profile and no `--profile` flag → error with guidance.

### `profile list`

```text
azv2 profile list
```

No parameters.

**Output**: Table of profiles. Active profile marked with `*`.
```text
  NAME    TENANT                                SUBSCRIPTION                          AUTH
* dev     aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee  11111111-2222-3333-4444-555555555555  browser
  prod    ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj  66666666-7777-8888-9999-000000000000  service-principal
```
**Errors**: No profiles → message with guidance to create one.

### `profile show`

```text
azv2 profile show [<name>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `<name>` | string | No | Profile to show. Default: active profile. |

**Output**: Full profile details in key-value format.
```text
Name:              dev
Tenant ID:         aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
Subscription ID:   11111111-2222-3333-4444-555555555555
Cloud:             AzureCloud
Auth Method:       browser
User:              user@contoso.com
Defaults:
  location:        eastus
```
**Errors**: Profile not found → error listing available profiles.

### `profile switch`

```text
azv2 profile switch <name>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `<name>` | string | Yes | Profile to set as active |

**Output**: Confirmation: `Switched to profile: {name}`
**Errors**: Profile not found → error listing available profiles.

### `profile modify`

```text
azv2 profile modify <name> [--subscription <id>] [--tenant-id <id>] [--default <key>=<value>] [--remove-default <key>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `<name>` | string | Yes | Profile to modify |
| `--subscription` | string | No | New subscription ID |
| `--tenant-id` | string | No | New tenant ID |
| `--default` | string | No | Set a default (`key=value`). Repeatable. |
| `--remove-default` | string | No | Remove a default by key. Repeatable. |

**Output**: Updated profile summary.
**Errors**: Profile not found → error. No modification flags → error with usage hint.

### `profile remove`

```text
azv2 profile remove <name>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `<name>` | string | Yes | Profile to delete |

**Output**: Confirmation: `Removed profile: {name}`
**Errors**: Profile not found → error. Removing active profile → profile removed, active pointer cleared, warning issued.

## Global Parameter (Engine-Level)

### `--profile`

```text
azv2 <any-command> --profile <name>
```

Stripped by `Program.cs` before extension dispatch. Injected as reserved `__profile` key. Resolved by `Engine.CreateCommandContext()`.

**Resolution order**: `--profile` flag → active profile → error with guidance.
