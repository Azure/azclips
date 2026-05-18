---
summary: List virtual machines in a resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Lists all virtual machines in the specified resource group and subscription.

### List all VMs in a resource group (CLI)

```bash
azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### List VMs (PowerShell)

```powershell
Get-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -ResourceGroupName "myrg"
```
