---
summary: List all resource groups in a subscription.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve a list of all resource groups in the specified Azure subscription. If no subscription ID is provided, the default subscription from your current login context is used. Output includes group names and locations.

### List all resource groups (CLI)

```bash
azv2 group list --subscription-id 00000000-0000-0000-0000-000000000000
```

### List using default subscription (CLI)

```bash
azv2 group list
```

### List resource groups (PowerShell)

```powershell
Get-ResourceGroup_List -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### List using default subscription (PowerShell)

```powershell
Get-ResourceGroup_List
```
