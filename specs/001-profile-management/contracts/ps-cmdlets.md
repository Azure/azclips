# PowerShell Cmdlet Contracts: Profile Management

**Feature**: 001-profile-management
**Module**: `Cirrus.Login` (existing module, extended)

## Cmdlets

All profile cmdlets follow the `Verb-CirrusNoun` naming convention and are exported from the Login PS module manifest.

### `Connect-CirrusAccount` (Modified)

```powershell
Connect-CirrusAccount [-TenantId <string>] [-SubscriptionId <string>] [-OutProfile <string>] [-UseDeviceCode]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-TenantId` | string | No | Azure AD tenant ID |
| `-SubscriptionId` | string | No | Subscription. Skips interactive prompt. |
| `-OutProfile` | string | No | Profile name to create/overwrite. Default: `temp` |
| `-UseDeviceCode` | switch | No | Use device code flow |

### `Disconnect-CirrusAccount` (Modified)

```powershell
Disconnect-CirrusAccount [-Profile <string>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Profile` | string | No | Clear token for specific profile instead of active |

### `Get-CirrusProfile`

```powershell
Get-CirrusProfile [[-Name] <string>] [-List]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | string | No | Show a specific profile. Default: active profile. |
| `-List` | switch | No | List all profiles instead of showing one. |

**Output**: `Profile` object(s) written to pipeline. When `-List`, outputs all profiles with an `IsActive` computed property.

### `Switch-CirrusProfile`

```powershell
Switch-CirrusProfile [-Name] <string>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | string | Yes | Profile to set as active |

**Output**: Writes the activated `Profile` object to pipeline. Writes confirmation to information stream.

### `Set-CirrusProfile`

```powershell
Set-CirrusProfile [-Name] <string> [-SubscriptionId <string>] [-TenantId <string>] [-Default <hashtable>] [-RemoveDefault <string[]>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | string | Yes | Profile to modify |
| `-SubscriptionId` | string | No | New subscription ID |
| `-TenantId` | string | No | New tenant ID |
| `-Default` | hashtable | No | Defaults to set (e.g., `@{location="eastus"}`) |
| `-RemoveDefault` | string[] | No | Default keys to remove |

**Output**: Updated `Profile` object to pipeline.

### `Remove-CirrusProfile`

```powershell
Remove-CirrusProfile [-Name] <string> [-Force]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | string | Yes | Profile to delete |
| `-Force` | switch | No | Skip confirmation prompt |

**Output**: None on success. ShouldProcess confirmation unless `-Force`.

## Common Parameter

### `-Profile` (Engine-Level)

All cmdlets implicitly support `-Profile <name>` as a common parameter, handled at the PS engine level (not per-cmdlet). The `IEngine.Process()` call extracts `-Profile` and injects it as `__profile` into the `CommandInvocation`.

## Module Manifest Changes

Add to `CmdletsToExport` in `Login.psd1`:
```powershell
CmdletsToExport = 'Connect-CirrusAccount', 'Disconnect-CirrusAccount', 'Get-CirrusAccount',
                  'Get-CirrusProfile', 'Switch-CirrusProfile', 'Set-CirrusProfile', 'Remove-CirrusProfile'
```
