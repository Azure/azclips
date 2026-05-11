# Login

Summary: Authenticate to Azure and set the current account context.
Description: Authenticate to Azure and persist account context for subsequent commands.

## CLI Examples

### Login with subscription id

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with device code

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

## PowerShell Examples

### Login with subscription id

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000
```

### Login with device code

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000 -UseDeviceCode
```
