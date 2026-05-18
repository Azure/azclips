---
summary: Show details for a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieves the properties of a single virtual machine identified by name within the specified resource group and subscription.

### Show a VM (CLI)

```bash
azv2 vm show --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Get a VM (PowerShell)

```powershell
Get-VM -SubscriptionId "00000000-0000-0000-0000-000000000000" -ResourceGroupName "myrg" -Name "myvm"
```
