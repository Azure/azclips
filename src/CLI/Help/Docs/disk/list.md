---
summary: List managed disks in a resource group.
status: ga
schema: 1.0.0
online version:
---

Description: Return all managed disks that live in the specified resource group, including each disk's name, size (GB), SKU tier, and attachment state. Use this command for inventory, capacity planning, or to identify orphaned disks before cleanup.

### List disks in a resource group

```bash
azv2 disk list --resource-group myrg34 --subscription-id 00000000-0000-0000-0000-000000000000
```

### List using shell variables

```bash
azv2 disk list --resource-group $rg --subscription-id $subId
```
