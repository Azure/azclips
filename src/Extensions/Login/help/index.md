---
summary: Authenticate to Azure and set the current account context.
status: ga
schema: 1.0.0
online version:
---

# Login

Description: Authenticate to Azure and persist account context for subsequent commands.

## CLI Examples

### Login with subscription id (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Login with device code (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

## PowerShell Examples

### Login with subscription id (PowerShell)

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000
```

### Login with device code (PowerShell)

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000 -UseDeviceCode
```
